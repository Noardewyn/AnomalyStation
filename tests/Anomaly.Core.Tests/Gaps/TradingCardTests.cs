using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The trading card game in a toy room (objects/items/tcg/, controllers/subsystem/tcgsetup.dm): SStrading_card_game loads tg's two
/// card sets and their keywords at boot; a pack torn open in the hand drops its six cards of its series with its wrapping; two cards
/// make a deck, which draws from its top, shuffles and flips face down; a card flips over in the hand; a creature card put into a
/// card holder summons its hologram with its power and resolve, which the holder's radial taps, marks and takes back out.
/// </summary>
public class TradingCardTests
{
    private sealed class Table
    {
        public AudioRoom R;
        public Feed Feed;
        public obj_machinery_trading_card_holder Holder;
        public mob_living_carbon_human Bob;
    }

    private static Table Room(ulong seed)
    {
        var t = new Table();
        t.R = new AudioRoom(13, 11, seed: seed, mapExtras: w => t.Holder = Dm.@new<obj_machinery_trading_card_holder>(locate(5, 5, 1)));
        t.R.In(() => (get_area(locate(4, 5, 1)) as area).requires_power = FALSE);
        t.Feed = Listen(t.R);
        t.Bob = t.R.Join(4, 5, "Bob");
        t.R.In(() => Waiting.NoSleep(t.Bob.fully_replace_character_name(t.Bob.real_name, "Bob")));
        t.R.Step(Second);
        return t;
    }

    private static void Hold(Table t, obj_item item) => t.R.In(() =>
    {
        foreach (obj_item held in Dm.each<obj_item>(t.Bob.held_items).ToList())
            if (held != item)
                Waiting.NoSleep(t.Bob.dropItemToGround(held));
        Assert.True(Dm.truthy(Waiting.NoSleep(t.Bob.put_in_active_hand(item))));
    });

    private static void PickRadial(AudioRoom r, mob who, string name)
    {
        r.Step(2);
        (long id, int index) = r.In(() =>
        {
            datum_radial_menu menu = Dm.each_cast<datum_radial_menu>(who.client.dm_radial_menus).SingleOrDefault();
            Assert.True(menu != null, "no radial menu");
            int i = menu.dm_elements.FindIndex(e => e.Name != null && e.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            Assert.True(i >= 0, "no slice " + name + " in " + string.Join(", ", menu.dm_elements.Select(e => e.Name)));
            return (menu.dm_id, i + 1);
        });
        r.Command(() => ClientSeam.RadialPick(who.client, id, index));
        r.Step(6);
    }

    private static List<obj_item_tcgcard> CardsAt(Table t, atom near) => t.R.In(() => Dm.each<obj_item_tcgcard>(get_turf(near)).ToList());

    [Fact]
    public void The_subsystem_loads_both_sets_and_a_pack_opens_into_its_cards()
    {
        var t = Room(14330);
        t.R.In(() =>
        {
            Assert.Equal(1f, SStrading_card_game.loaded);
            var core = SStrading_card_game.cached_cards["coreset2020"] as DmList;
            Assert.NotNull(core);
            var ai = ((DmList)core["ALL"])["AI"] as datum_card;
            Assert.Equal("AI", ai.name);
            Assert.Equal("Creature", ai.cardtype);
            Assert.Equal("icons/runtime/tcg/default.dmi", Dm.str(ai.icon)); // the set's default template
            Assert.Contains("tooltip", Dm.str(ai.rules)); // the {$keyword}s resolved into tooltips
            Assert.NotNull(SStrading_card_game.cached_cards["resinfront"]);
        });

        var pack = t.R.In(() => Dm.@new<obj_item_cardpack_series_one>(t.Bob.loc));
        Hold(t, pack);
        t.R.In(() => Waiting.NoSleep(pack.attack_self(t.Bob)));
        t.R.Step(2);
        Assert.True(QDELETED(pack));
        List<obj_item_tcgcard> cards = CardsAt(t, t.Bob);
        Assert.Equal(6, cards.Count); // card_count 5 and guaranteed_count 1
        Assert.All(cards, c => Assert.Equal("coreset2020", c.series));
        Assert.All(cards, c => Assert.NotEqual("Trading Card", c.name));
        Assert.Single(t.R.In(() => Dm.each<obj_effect_decal_cleanable_wrapping>(get_turf(t.Bob)).ToList()));
        Assert.True(t.Feed.Any("Wow! Check out these cards!"));
        Assert.Contains(t.R.Sounds.Heard, h => h.File == "sound/items/poster/poster_ripped.ogg");
        t.R.AssertNoRuntimes();
    }

    [Fact]
    public void Two_cards_make_a_deck_that_draws_shuffles_and_flips()
    {
        var t = Room(14331);
        var (first, second, third) = t.R.In(() => (
            Dm.@new<obj_item_tcgcard>(t.Bob.loc, "coreset2020", "AI"),
            Dm.@new<obj_item_tcgcard>(t.Bob.loc, "coreset2020", "AI"),
            Dm.@new<obj_item_tcgcard>(t.Bob.loc, "coreset2020", "AI")));
        Assert.Equal("AI", t.R.In(() => first.name));
        Assert.Equal("ai", t.R.In(() => first.icon_state));

        // a card flips over in the hand
        Hold(t, first);
        t.R.In(() => Waiting.NoSleep(first.attack_self(t.Bob)));
        Assert.Equal("Trading Card", t.R.In(() => first.name));
        Assert.Equal("cardback", t.R.In(() => first.icon_state));
        t.R.In(() => Waiting.NoSleep(first.attack_self(t.Bob)));
        Assert.Equal("AI", t.R.In(() => first.name));

        // one card used on another: a deck of both in the hand
        Hold(t, second);
        object done = t.R.In(() => Waiting.NoSleep(first.item_interaction(t.Bob, second)));
        Assert.Equal(ITEM_INTERACT_SUCCESS, Dm.num(done));
        obj_item_tcgcard_deck deck = t.R.In(() => t.Bob.get_active_held_item() as obj_item_tcgcard_deck);
        Assert.NotNull(deck);
        Assert.Equal(2, t.R.In(() => deck.contents.len));
        Assert.Equal("deck_up", t.R.In(() => deck.icon_state));

        // a third card goes on the pile
        t.R.In(() => Waiting.NoSleep(deck.item_interaction(t.Bob, third)));
        Assert.Equal(3, t.R.In(() => deck.contents.len));

        // flipped face down: a low pile of backs
        t.R.In(() => deck.flip_deck());
        Assert.Equal("deck_tcg_low", t.R.In(() => deck.icon_state));
        Assert.All(t.R.In(() => Dm.each<obj_item_tcgcard>(deck.contents).ToList()), c => Assert.Equal(1f, c.flipped));

        // the deck on the floor: its radial draws the top card face down into the hand
        t.R.In(() =>
        {
            Waiting.NoSleep(t.Bob.dropItemToGround(deck));
            foreach (obj_item held in Dm.each<obj_item>(t.Bob.held_items).ToList())
                Waiting.NoSleep(t.Bob.dropItemToGround(held));
        });
        Click(t.R, t.Bob, deck);
        PickRadial(t.R, t.Bob, "Draw");
        var drawn = t.R.In(() => t.Bob.get_active_held_item() as obj_item_tcgcard);
        Assert.NotNull(drawn);
        Assert.Equal("cardback", t.R.In(() => drawn.icon_state));
        Assert.Equal(2, t.R.In(() => deck.contents.len));
        Assert.True(t.Feed.Any("You draw a card from Trading Card Pile!")); // a capitalized name is proper: \the drops its article

        // shuffle in the hand
        t.R.In(() => Waiting.NoSleep(t.Bob.dropItemToGround(drawn)));
        Hold(t, deck);
        t.R.In(() => Waiting.NoSleep(deck.attack_self(t.Bob)));
        Assert.True(t.Feed.Any("You shuffle Trading Card Pile!"));
        t.R.AssertNoRuntimes();
    }

    [Fact]
    public void A_creature_card_in_the_holder_summons_its_hologram()
    {
        var t = Room(14332);
        var card = t.R.In(() => Dm.@new<obj_item_tcgcard>(t.Bob.loc, "coreset2020", "AI"));
        Hold(t, card);
        Click(t.R, t.Bob, t.Holder);
        t.R.Step(2);
        Assert.Same(t.Holder, t.R.In(() => card.loc));
        Assert.Equal("card_holder_active", t.R.In(() => t.Holder.icon_state));
        obj_structure_trading_card_summon summon = t.R.In(() => t.Holder.current_summon);
        Assert.NotNull(summon);
        Assert.Equal("AI", t.R.In(() => summon.name));
        Assert.Equal("5", t.R.In(() => Dm.str(summon.summon_power)));
        Assert.Equal("6", t.R.In(() => Dm.str(summon.summon_resolve)));
        Assert.Equal("ai", t.R.In(() => summon.hologram.icon_state)); // the card's own summon sprite, icons/mob/silicon/ai.dmi
        Assert.Equal(170f, t.R.In(() => summon.hologram.alpha));
        Assert.Equal(2, t.R.In(() => Dm.each<obj_effect_overlay_status_display_text>(summon.vis_contents).Count()));

        // the radial: marked with its gem, then taken back out
        t.R.In(() =>
        {
            foreach (obj_item held in Dm.each<obj_item>(t.Bob.held_items).ToList())
                Waiting.NoSleep(t.Bob.dropItemToGround(held));
        });
        Click(t.R, t.Bob, t.Holder);
        PickRadial(t.R, t.Bob, "Mark");
        Assert.Equal(1f, t.R.In(() => summon.marked));
        Click(t.R, t.Bob, t.Holder);
        PickRadial(t.R, t.Bob, "Pickup");
        Assert.True(QDELETED(summon));
        Assert.Same(card, t.R.In(() => t.Bob.get_active_held_item()));
        Assert.Equal("card_holder_inactive", t.R.In(() => t.Holder.icon_state));
        t.R.AssertNoRuntimes();
    }
}
