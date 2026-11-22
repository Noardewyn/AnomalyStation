// Translated from tgstation (snapshot 1d99bd7f): code/modules/cards/deck/tarot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3d, D316).

//These cards certainly won't tell the future, but you can play some nice games with them.
// tg: code/modules/cards/deck/tarot.dm:4
public partial class obj_item_toy_cards_deck_tarot : obj_item_toy_cards_deck
{
    // tg: code/modules/cards/deck/tarot.dm:11
    public override void initialize_cards()
    {
        foreach (string suit in new[] { "Hearts", "Pikes", "Clovers", "Tiles" })
        {
            for (int i = 1; i <= 10; i++)
                initial_cards.Add(Dm.text("[] of []", i, suit));
            foreach (string person in new[] { "Valet", "Chevalier", "Dame", "Roi" })
                initial_cards.Add(Dm.text("[] of []", person, suit));
        }
        foreach (string trump in new[] { "The Magician", "The High Priestess", "The Empress", "The Emperor", "The Hierophant", "The Lover", "The Chariot", "Justice", "The Hermit", "The Wheel of Fortune", "Strength", "The Hanged Man", "Death", "Temperance", "The Devil", "The Tower", "The Star", "The Moon", "The Sun", "Judgement", "The World", "The Fool" })
            initial_cards.Add(trump);
    }

    // tg: code/modules/cards/deck/tarot.dm:20
    public override async DmTask<obj_item_toy_singlecard> draw(mob_living user = null, obj_item_toy_singlecard card = null)
    {
        obj_item_toy_singlecard dot = await base.draw(user, card);
        if (prob(50))
        {
            if (dot == null)
                return dot;

            matrix M = matrix();
            M.Turn(180);
            dot.transform = M;
        }
        return dot;
    }

    // tg: code/modules/cards/deck/tarot.dm:31
    public override obj_item_toy_singlecard pick_card(mob_living user = null, DmList cards = null)
    {
        // If the user is cursed they have increase chance of drawing Death or The Tower
        if (!HAS_TRAIT(user, TRAIT_CURSED))
            return base.pick_card(user, cards);

        float total_card = cards.len;
        // give a boosted chance if they're using the full deck
        float chance_modifier = total_card >= 56 ? 24 : 4;
        if (!prob(Math.Min(33, chance_modifier / total_card * 100)))
            return base.pick_card(user, cards);

        foreach (obj_item_toy_singlecard card in Dm.each<obj_item_toy_singlecard>(cards))
        {
            if (Dm.eq(card.cardname, "Death") || Dm.eq(card.cardname, "The Tower"))
                return card;
        }

        return base.pick_card(user, cards);
    }
}

// tg: code/modules/cards/deck/tarot.dm:48
public partial class obj_item_toy_cards_deck_tarot_haunted : obj_item_toy_cards_deck_tarot
{
    private const float TAROT_GHOST_TIMER = 666 * SECONDS; // this translates into 11 mins and 6 seconds

    // tg: code/modules/cards/deck/tarot.dm:54
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_two_handed),
            Dm.named("attacksound", Dm.resource("sound/items/cards/cardflip.ogg")),
            Dm.named("wield_callback", Dm.@new<datum_callback>(this, nameof(on_wield))),
            Dm.named("unwield_callback", Dm.@new<datum_callback>(this, nameof(on_unwield)))));
        return dot;
    }

    // tg: code/modules/cards/deck/tarot.dm:63
    public virtual void on_wield(obj_item source = null, mob_living_carbon user = null)
    {
        ADD_TRAIT(user, TRAIT_SIXTHSENSE, MAGIC_TRAIT);
        to_chat(user, span_notice(I18n.T("The veil to the underworld is opened. You can sense the dead souls calling out...")));

        if (!(ghost_alert_cooldown <= Dm.num(world.time))) // COOLDOWN_FINISHED
            return;

        ghost_alert_cooldown = Dm.num(world.time) + TAROT_GHOST_TIMER; // COOLDOWN_START
        notify_ghosts(
            I18n.T("Someone has begun playing with a [] in []!", name, get_area(this)),
            source: this,
            header: I18n.T("Haunted Tarot Deck"),
            ghost_sound: "sound/effects/ghost2.ogg",
            notify_volume: 75);
    }

    // tg: code/modules/cards/deck/tarot.dm:79
    public virtual void on_unwield(obj_item source = null, mob_living_carbon user = null)
    {
        REMOVE_TRAIT(user, TRAIT_SIXTHSENSE, MAGIC_TRAIT);
        to_chat(user, span_notice(I18n.T("The veil to the underworld closes shut. You feel your senses returning to normal.")));
    }
}
