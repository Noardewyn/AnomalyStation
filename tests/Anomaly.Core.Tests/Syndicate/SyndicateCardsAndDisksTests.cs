using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Syndicate;

/// <summary>
/// Syndicate stage G3d (D316), the uplink's suspicious deck of cards and PDA disks with the systems they need: the deck deals face
/// down and face up, two cards make a hand, the hand goes back into the deck, a wielded deck shuffles, a thrown deck forces 52 card
/// pickup, a pen marks a card; the clown's disk honks a PDA and an airlock, the mime's silences a messenger, the F.R.A.M.E. disk takes
/// telecrystals and opens an unlocked uplink on its target, the Detomatix disk cannot bomb the captain and a rigged PDA explodes when
/// its owner opens it; the SyndEye disk goes into a PDA and its camera program copies off through the file manager; a sweep spawns and deletes each newly live item with no runtimes.
/// </summary>
public class SyndicateCardsAndDisksTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed = 47)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        r.In(() => Waiting.NoSleep(bob.equipOutfit(typeof(datum_outfit_job_assistant))));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Pda<T>(AudioRoom r, Func<atom> loc, string owner) where T : obj_item_modular_computer_pda => r.In(() =>
    {
        T pda = Dm.@new<T>(loc());
        pda.imprint_id(owner, "Assistant");
        return pda;
    });

    [Fact]
    public void TheTypesAreLiveAndTheirEntriesSell()
    {
        foreach (Type type in new[]
        {
            typeof(obj_item_toy_cards_deck_syndicate), typeof(obj_item_toy_singlecard), typeof(obj_item_toy_cards_cardhand), typeof(datum_deck_card_blank),
            typeof(obj_item_toy_cards_deck_tarot_haunted), typeof(obj_item_toy_cards_deck_syndicate_holographic), typeof(obj_item_disk_computer_virus_detomatix),
            typeof(obj_item_disk_computer_virus_frame), typeof(obj_item_disk_computer_virus_clown), typeof(obj_item_disk_computer_virus_mime),
            typeof(obj_item_disk_computer_syndicate_camera_app), typeof(datum_computer_file_program_secureye_syndicate), typeof(datum_component_sound_player),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
        var r = new AudioRoom(5, 5, seed: 3);
        foreach (Type entry in new[]
        {
            typeof(datum_uplink_item_badass_syndiecards), typeof(datum_uplink_item_explosives_detomatix), typeof(datum_uplink_item_device_tools_frame),
            typeof(datum_uplink_item_device_tools_camera_app),
        })
            Assert.False(r.In(() => uplink_item_hidden(Dm.@new(entry) as datum_uplink_item)), entry.Name + " is hidden");
    }

    [Fact]
    public void TheDeckDealsMakesAHandTakesItBackAndShuffles()
    {
        var (r, feed, bob) = Room();
        var deck = r.In(() => Dm.@new<obj_item_toy_cards_deck_syndicate>(locate(6, 5, 1)));
        Assert.Equal(54, r.In(() => deck.count_cards()));
        Assert.Equal("deck_syndicate_full", r.In(() => deck.icon_state));

        // a draw is face down, the right click one face up, both with the deck's edge
        r.In(() => Waiting.NoSleep(deck.attack_hand(bob)));
        r.Step(2);
        var down = r.In(() => Dm.each<obj_item_toy_singlecard>(bob.contents).Single());
        Assert.Equal("card", r.In(() => Dm.str(down.name)));
        Assert.Equal("singlecard_down_syndicate", r.In(() => down.icon_state));
        Assert.Equal(5, r.In(() => Dm.num(down.force)));
        Assert.True(feed.Any("balloon: draws a card"));
        r.In(() => Waiting.NoSleep(deck.attack_hand_secondary(bob)));
        r.Step(2);
        var up = r.In(() => Dm.each<obj_item_toy_singlecard>(bob.contents).Single(c => c != down));
        Assert.Equal("Joker Mime", r.In(() => Dm.str(up.name)));
        Assert.Equal("sc_Joker Mime_syndicate", r.In(() => up.icon_state));
        Assert.Equal(52, r.In(() => deck.count_cards()));

        // card on card makes a hand in the active hand, which fans the cards out
        r.In(() => Waiting.NoSleep(up.item_interaction(bob, down)));
        r.Step(2);
        var hand = r.In(() => Dm.each<obj_item_toy_cards_cardhand>(bob.contents).Single());
        Assert.Equal(2, r.In(() => hand.count_cards()));
        Assert.Equal(2, r.In(() => hand.overlays.len));
        Assert.Empty(r.In(() => Dm.each<obj_item_toy_singlecard>(bob.contents).ToList()));

        // the hand goes back into the deck face down
        r.In(() => Waiting.NoSleep(deck.item_interaction(bob, hand)));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(hand)));
        Assert.Equal(54, r.In(() => deck.count_cards()));
        Assert.True(r.In(() => Dm.each<obj_item_toy_singlecard>(deck.card_atoms).All(c => c.flipped == CARD_FACEDOWN)));
        Assert.True(feed.Any("balloon: puts cards in deck"));

        // only a wielded deck shuffles, and not again within its cooldown
        r.In(() => Waiting.NoSleep(deck.click_alt(bob)));
        Assert.True(feed.Any("with both hands to shuffle."));
        string before = r.In(() => string.Join(",", Dm.each<obj_item_toy_singlecard>(deck.card_atoms).Select(c => c.cardname)));
        r.In(() => ADD_TRAIT(deck, TRAIT_WIELDED, "test"));
        r.In(() => Waiting.NoSleep(deck.click_alt(bob)));
        string after = r.In(() => string.Join(",", Dm.each<obj_item_toy_singlecard>(deck.card_atoms).Select(c => c.cardname)));
        Assert.NotEqual(before, after);
        Assert.True(feed.Any("balloon: shuffles the deck"));
        r.In(() => Waiting.NoSleep(deck.click_alt(bob)));
        Assert.Equal(after, r.In(() => string.Join(",", Dm.each<obj_item_toy_singlecard>(deck.card_atoms).Select(c => c.cardname))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AThrownDeckForces52CardPickupAndAPenMarksACard()
    {
        var (r, feed, bob) = Room();
        mob_living_carbon_human alice = r.Join(8, 5, "Alice");
        r.Step(Second);
        var deck = r.In(() => Dm.@new<obj_item_toy_cards_deck_syndicate>(bob.loc));
        r.In(() => Waiting.Detach(deck.throw_at(alice, 7, 1, bob)));
        r.Step(2 * Second);
        Assert.Equal(54, r.In(() => Dm.each<obj_item_toy_singlecard>(alice.loc).Count()));
        Assert.Equal(0, r.In(() => deck.count_cards()));
        Assert.Equal("deck_syndicate_empty", r.In(() => deck.icon_state));
        Assert.True(feed.Any("is forced to play 52 card pickup!"));

        var card = r.In(() => Dm.each<obj_item_toy_singlecard>(alice.loc).First());
        var pen = r.In(() => Dm.@new<obj_item_pen>(bob.loc));
        r.In(() => Waiting.NoSleep(card.item_interaction(bob, pen)));
        Assert.Equal("#000000", r.In(() => card.marked_color));
        Assert.True(feed.Any("Cheat to win!"));
        Assert.Equal("#000000", r.In(() => card.getMarkedColor(bob)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheClownsAndTheMimesDisks()
    {
        var (r, feed, bob) = Room();
        var clown_pda = Pda<obj_item_modular_computer_pda_crew_clown>(r, () => bob.loc, "Bob Doe");
        var target = Pda<obj_item_modular_computer_pda_crew_assistant>(r, () => locate(2, 2, 1) as atom, "Alice Doe");
        var honk = r.In(() => clown_pda.inserted_disk as obj_item_disk_computer_virus_clown);
        Assert.NotNull(honk);
        Assert.Equal(TRUE, r.In(() => Waiting.NoSleep(honk.send_virus(clown_pda, target, bob, "hi"))));
        Assert.InRange(r.In(() => target.honkvirus_amount), 15, 25);
        Assert.Equal(4, r.In(() => honk.charges));

        // an airlock with its panel open honks when it opens or closes
        var airlock = r.In(() => Dm.@new<obj_machinery_door_airlock>(locate(6, 5, 1)));
        r.Step(2);
        r.In(() => airlock.panel_open = TRUE);
        Assert.Equal(TRUE, r.In(() => Waiting.NoSleep(clown_pda.pre_attack(airlock, bob))));
        Assert.NotNull(r.In(() => airlock.GetComponent(typeof(datum_component_sound_player))));
        Assert.Equal(3, r.In(() => honk.charges));
        Assert.True(feed.Any("You upload the virus to"));
        // a slip by the clown's PDA gives a charge back
        mob_living_carbon_human alice = r.Join(3, 3, "Alice");
        r.In(() => ((obj_item_modular_computer_pda_crew_clown)clown_pda).AfterSlip(alice));
        Assert.Equal(4, r.In(() => honk.charges));

        var silence = r.In(() => Dm.@new<obj_item_disk_computer_virus_mime>(bob.loc));
        Assert.Equal(TRUE, r.In(() => Waiting.NoSleep(silence.send_virus(clown_pda, target, bob, "hi"))));
        var messenger = r.In(() => Dm.locate<datum_computer_file_program_messenger>(target.stored_files));
        Assert.Equal(TRUE, r.In(() => messenger.alert_silenced));
        Assert.Equal("", r.In(() => messenger.ringtone));
        Assert.Equal(4, r.In(() => silence.charges));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheFrameDiskOpensAnUplinkWithItsTelecrystals()
    {
        var (r, feed, bob) = Room();
        var source = Pda<obj_item_modular_computer_pda_crew_assistant>(r, () => bob.loc, "Bob Doe");
        var target = Pda<obj_item_modular_computer_pda_crew_assistant>(r, () => locate(2, 2, 1) as atom, "Alice Doe");
        var frame = r.In(() => Dm.@new<obj_item_disk_computer_virus_frame>(bob.loc));
        var tc = r.In(() => Dm.@new<obj_item_stack_telecrystal>(bob.loc, 5));
        r.In(() => Waiting.NoSleep(frame.item_interaction(bob, tc)));
        r.Step(2);
        Assert.Equal(5, r.In(() => frame.telecrystals));
        Assert.True(r.In(() => QDELETED(tc)));

        Assert.Equal(TRUE, r.In(() => Waiting.NoSleep(frame.send_virus(source, target, bob, "hi"))));
        var uplink = r.In(() => target.GetComponent(typeof(datum_component_uplink)) as datum_component_uplink);
        Assert.NotNull(uplink);
        Assert.Equal(0, r.In(() => uplink.locked));
        Assert.Equal(TRUE, r.In(() => uplink.active));
        Assert.Equal(5, r.In(() => uplink.uplink_handler.telecrystals));
        string code = r.In(() => Dm.str(uplink.unlock_code));
        Assert.Matches(@"^\d{3} \w+$", code);
        Assert.True(feed.Any("The unlock code to the target is: " + code));
        Assert.Equal(0, r.In(() => frame.telecrystals));
        Assert.Equal(4, r.In(() => frame.charges));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDetomatixCannotBombTheCaptainAndARiggedPdaExplodesWhenOpened()
    {
        var (r, feed, bob) = Room();
        var source = Pda<obj_item_modular_computer_pda_crew_assistant>(r, () => bob.loc, "Bob Doe");
        var captain = Pda<obj_item_modular_computer_pda_crew_heads_captain>(r, () => locate(2, 2, 1) as atom, "Cap Doe");
        var bomb = r.In(() => Dm.@new<obj_item_disk_computer_virus_detomatix>(bob.loc));
        Assert.Null(r.In(() => Waiting.NoSleep(bomb.send_virus(source, captain, bob, "hi"))));
        Assert.True(feed.Any("ERROR: Target could not be bombed."));
        Assert.Equal(5, r.In(() => bomb.charges));

        // a PDA the virus rigged explodes in its owner's hands when they open it
        mob_living_carbon_human alice = r.Join(3, 3, "Alice");
        r.Step(Second);
        var rigged = Pda<obj_item_modular_computer_pda_crew_assistant>(r, () => alice.loc, "Alice Doe");
        r.In(() => Waiting.NoSleep(alice.put_in_active_hand(rigged)));
        r.In(() => rigged.add_traits(Dm.list(TRAIT_PDA_CAN_EXPLODE, TRAIT_PDA_MESSAGE_MENU_RIGGED), REF(bomb)));
        r.In(() => Waiting.NoSleep(rigged.interact(alice)));
        r.Step(Second);
        Assert.True(r.In(() => QDELETED(rigged)));
        Assert.True(feed.Any("explodes!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSyndEyeDiskGoesIntoAPdaAndItsProgramCopiesOff()
    {
        var (r, feed, bob) = Room();
        var disk = r.In(() => Dm.@new<obj_item_disk_computer_syndicate_camera_app>(bob.loc));
        Assert.Equal("SyndEye", r.In(() => Dm.str(Dm.each<datum_computer_file_program_secureye_syndicate>(disk.stored_files).Single().filedesc)));
        var pda = Pda<obj_item_modular_computer_pda_crew_assistant>(r, () => bob.loc, "Bob Doe");
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(disk)));
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(pda.item_interaction(bob, disk))));
        Assert.Same(disk, r.In(() => pda.inserted_disk));
        Assert.True(feed.Any("balloon: disk inserted"));

        // the file manager lists the disk's program and copies it off; a unique copy leaves the disk
        var files = r.In(() => Dm.locate<datum_computer_file_program_filemanager>(pda.stored_files));
        DmList data = r.In(() => Waiting.NoSleep(files.ui_data(bob)) as DmList);
        Assert.Equal(TRUE, r.In(() => data["usbconnected"]));
        Assert.Equal("syndeye", r.In(() => Dm.str(((data["usbfiles"] as DmList)[1] as DmList)["name"])));
        Assert.Equal(TRUE, r.In(() => Waiting.NoSleep(files.ui_act("PRG_copyfromusb", Dm.list(Dm.kv("name", "syndeye"))))));
        var installed = r.In(() => Dm.each<datum_computer_file_program_secureye_syndicate>(pda.stored_files).Single());
        Assert.Empty(r.In(() => Dm.each<datum_computer_file>(disk.stored_files).ToList()));
        Assert.Equal("NtosSecurEye", r.In(() => Dm.str(installed.tgui_id)));
        Assert.NotNull(r.In(() => installed.cam_screen));
        DmList static_data = r.In(() => Waiting.NoSleep(installed.ui_static_data(bob)) as DmList);
        Assert.Equal(TRUE, r.In(() => static_data["can_spy"]));
        Assert.Equal(6, r.In(() => (static_data["network"] as DmList).len));

        // control-shift-click ejects the disk into the hand
        r.In(() => Waiting.NoSleep(pda.click_ctrl_shift(bob)));
        Assert.Null(r.In(() => pda.inserted_disk));
        Assert.Contains(disk, r.In(() => Dm.each<obj_item_disk_computer>(bob.contents).ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EveryNewItemSpawnsAndDeletesCleanly()
    {
        var (r, _, bob) = Room();
        IEnumerable<Type> Live(Type root) => root.Assembly.GetTypes()
            .Where(t => (t == root || t.IsSubclassOf(root)) && !Attribute.IsDefined(t, typeof(DmInertAttribute), inherit: false));
        var types = Live(typeof(obj_item_toy_cards)).Concat(Live(typeof(obj_item_toy_singlecard))).Concat(Live(typeof(obj_item_disk_computer_virus))).Concat(new[]
        {
            typeof(obj_item_disk_computer_syndicate_camera_app),
        });
        foreach (Type t in types)
        {
            var made = r.In(() => Dm.@new(t, bob.loc) as atom);
            r.Step(1);
            r.In(() => qdel(made));
            r.Step(1);
        }
        r.AssertNoRuntimes();
    }
}
