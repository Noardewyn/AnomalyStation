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
/// Gaps package OD1 (D333): the items' odds, in a booted toy room with Bob as a player: the ruins' puzzles (a button dropping a shield
/// gate and opening a one-way gate, the light mechanism powering its door, the hologrid taking its holobox for a cookie, the PIN panel
/// with its dotted board and scrambled note), the inhaler with its canisters and albuterol, the e-cigarette's cap, voltage and emag,
/// the instrument, augment and armaments beacons, the jerrycan's overlays, and the small leftovers (the taster, the bear armor, the
/// trash cans, the surgical razor, the balloon mallet, the party poster, the stingball's rebounds, the ID's var edits, the manuals'
/// and the seal's suicides).
/// </summary>
public class ItemsOD1Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T item = r.In(() => Dm.@new(typeof(T), who.loc) as T);
        r.Step(2);
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    /// <summary>An atom as the map loader makes it: the preloader's var edits, then Initialize with mapload (as FurnitureST2Tests').</summary>
    private static T Mapped<T>(AudioRoom r, int x, int y, DmList edits = null) where T : atom => r.In(() =>
    {
        float previous = SSatoms.initialized;
        SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD;
        if (edits != null)
            world.preloader_setup(edits, typeof(T));
        T made = Dm.@new<T>(locate(x, y, 1));
        SSatoms.initialized = previous;
        return made;
    });

    // the answer to tg's input window of that interface the player has open (as FurnitureST2Tests')
    private static void Answer(AudioRoom r, mob who, string iface, object entry)
    {
        r.Step(2);
        var open = r.In(() => Dm.each_cast<datum_tgui>(who.tgui_open_uis).Select(u => Dm.str(u.@interface)).ToList());
        Assert.True(open.Contains(iface), "no " + iface + " open; open: " + string.Join(", ", open));
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(who.tgui_open_uis).Last(u => Dm.str(u.@interface) == iface).window.id));
        r.Command(() => ClientSeam.TguiMessage(who.client, window, "act/submit", Dm.list(Dm.kv("entry", entry))));
        r.Step(4);
    }

    [Fact]
    public void AButtonDropsItsShieldGateAndOpensItsOneWayGate()
    {
        var (r, feed, bob) = Room(9301);
        var button = Mapped<obj_machinery_puzzle_button>(r, 5, 4, Dm.list(Dm.kv("id", "od1"), Dm.kv("queue_size", 3)));
        var gate = Mapped<obj_structure_puzzle_blockade>(r, 7, 4, Dm.list(Dm.kv("id", "od1")));
        var oneway = Mapped<obj_structure_puzzle_blockade_oneway>(r, 8, 4, Dm.list(Dm.kv("id", "od1")));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal("lockdown0", button.icon_state);
            Assert.True(Dm.truthy(gate.density));
            Assert.Null(SSqueuelinks.queues["od1"]); // three of three: the link popped
        });
        r.In(() => Waiting.NoSleep(button.attack_hand(bob)));
        r.Step(2);
        Assert.True(feed.Any("presses a button on")); // the button's visible_message: its self message is the button's own
        r.In(() =>
        {
            Assert.Equal("lockdown1", button.icon_state);
            Assert.True(QDELETED(gate));
            Assert.Equal("oneway-off", oneway.icon_state);
            Assert.False(Dm.truthy(oneway.density));
        });
        // single use: a second press does nothing
        r.In(() => Waiting.NoSleep(button.attack_hand(bob)));
        r.Step(2);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheLightMechanismLightsByTheClickedLightAndPowersItsDoor()
    {
        var (r, feed, bob) = Room(9302);
        var puzzle = Mapped<obj_structure_light_puzzle>(r, 5, 5, Dm.list(Dm.kv("puzzle_id", "lights"), Dm.kv("queue_size", 2)));
        var door = Mapped<obj_machinery_door_puzzle_light>(r, 7, 5, Dm.list(Dm.kv("puzzle_id", "lights")));
        r.Step(2);
        r.In(() =>
        {
            // the random board is never one of the banned (unsolvable or solved) ones
            float board = 0;
            for (int i = 0; i < 9; i++)
                board += Dm.truthy(puzzle.light_list[i + 1]) ? (1 << i) : 0;
            Assert.DoesNotContain(board, new float[] { 47, 95, 203, 311, 325, 422, 473, 488, 500, 511 });
            Assert.Equal(Dm.each(puzzle.light_list).Count(Dm.truthy), Dm.length(puzzle.overlays));
            // one click from solved: the centre's cross is dark, the corners lit
            foreach (int i in new[] { 1, 3, 7, 9 })
                puzzle.light_list[i] = 1;
            foreach (int i in new[] { 2, 4, 5, 6, 8 })
                puzzle.light_list[i] = 0;
            Assert.True(Dm.truthy(door.density));
        });
        // the centre light: icon-x 16 and icon-y 16 are light 5 by tg's arithmetic
        r.In(() => Waiting.NoSleep(puzzle.attack_hand(bob, Dm.list(Dm.kv(ICON_X, "16"), Dm.kv(ICON_Y, "16")))));
        r.Step(3 * Second);
        Assert.True(feed.Any("becomes fully charged!"));
        r.In(() =>
        {
            Assert.True(Dm.truthy(puzzle.powered));
            Assert.Equal(9, Dm.each(puzzle.light_list).Count(Dm.truthy));
            Assert.False(Dm.truthy(door.density));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHologridBurnsItsHoloboxIntoACookieOnlyOnce()
    {
        var (r, feed, bob) = Room(9303);
        var grid = r.In(() => Dm.@new<obj_item_pressure_plate_hologrid>(locate(6, 4, 1)));
        var box = r.In(() => Dm.@new<obj_structure_holobox>(locate(7, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(box.forceMove(locate(6, 4, 1))));
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.True(QDELETED(box));
            Assert.True(Dm.truthy(grid.claimed));
            Assert.Equal("lasergrid_full", grid.icon_state);
            Assert.Single(Dm.each(locate(6, 4, 1).contents).OfType<obj_item_food_cookie>());
        });
        var examined = r.In(() => Waiting.NoSleep(grid.examine(bob)) as DmList);
        r.In(() => Assert.Contains(Dm.each(examined).Select(Dm.str), e => e.Contains("spent already", StringComparison.Ordinal)));
        var second = r.In(() => Dm.@new<obj_structure_holobox>(locate(6, 4, 1)));
        r.Step(3 * Second);
        r.In(() => Assert.Single(Dm.each(locate(6, 4, 1).contents).OfType<obj_item_food_cookie>()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APinPanelHidesItsCodeInItsCluesAndOpensForIt()
    {
        var (r, feed, bob) = Room(9304);
        var pad = Mapped<obj_machinery_puzzle_password_pin>(r, 5, 4, Dm.list(Dm.kv("id", "pin"), Dm.kv("queue_size", 3)));
        var dots = Mapped<obj_effect_decal_puzzle_dots>(r, 6, 6, Dm.list(Dm.kv("id", "pin")));
        var note = Mapped<obj_item_paper_fluff_scrambled_pass>(r, 7, 6, Dm.list(Dm.kv("puzzle_id", "pin")));
        var gate = Mapped<obj_structure_puzzle_blockade>(r, 9, 4, Dm.list(Dm.kv("id", "pin2")));
        r.Step(2);
        string password = r.In(() => pad.password);
        r.In(() =>
        {
            Assert.Equal(6, password.Length);
            Assert.All(password, c => Assert.InRange(c, '1', '9'));
            Assert.Equal(10, Dm.length(pad.digit_to_color));
            // six coloured dots on two rows: the extra row's board and the dots
            string detail = "queue " + (SSqueuelinks.queues["pin"] != null) + "; overlays " + string.Join(", ", Dm.each(dots.overlays).Select(o => o is IDmAppearance x ? x.icon_state + "@" + x.pixel_w + "," + x.pixel_z : Dm.str(o)))
                + "; pixel_y " + dots.pixel_y + "; note " + Dm.str(note.get_raw_text()).Length;
            Assert.True(Dm.length(dots.overlays) == 1 + 6, detail);
            string text = Dm.str(note.get_raw_text()).TrimEnd('/'); // get_raw_text ends each input with "/" (paper.dm)
            Assert.InRange(text.Length, 200, 300);
            Assert.All(text, c => Assert.Contains(c, password));
        });
        // the gate listens to the pad too
        r.In(() => gate.MatchedLinks("pin2", Dm.list(pad)));
        r.In(() => Waiting.Detach(pad.interact(bob)));
        Answer(r, bob, "TextInputModal", "000000");
        r.In(() => Assert.Equal("passpad0", pad.icon_state));
        r.In(() => Waiting.Detach(pad.interact(bob)));
        Answer(r, bob, "TextInputModal", password);
        r.In(() =>
        {
            Assert.Equal("passpad1", pad.icon_state);
            Assert.True(QDELETED(gate));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheInhalerPuffsItsCanisterIntoTheLungsAndAlbuterolOpensThem()
    {
        var (r, feed, bob) = Room(9305);
        var inhaler = Hold<obj_item_inhaler_albuterol>(r, bob);
        var canister = r.In(() => inhaler.canister);
        r.In(() =>
        {
            Assert.NotNull(canister);
            Assert.Same(inhaler, canister.loc);
            Assert.Equal(1, Dm.length(inhaler.underlays));
            Assert.Equal(6f, Dm.num(canister.get_puffs_left())); // 30u at 5u a puff
        });
        var examined = r.In(() => Waiting.NoSleep(inhaler.examine(bob)) as DmList);
        r.In(() => Assert.Contains(Dm.each(examined).Select(Dm.str), e => e.Contains("more times", StringComparison.Ordinal)));
        r.In(() => Waiting.Detach(inhaler.interact_with_atom(bob, bob)));
        r.Step(2 * Second);
        Assert.True(feed.Any("You take a puff of"));
        var lungs = r.In(() => bob.get_organ_slot(ORGAN_SLOT_LUNGS) as obj_item_organ_lungs);
        r.In(() =>
        {
            Assert.Equal(5f, Dm.num(canister.get_puffs_left()));
            Assert.True(Dm.num(((datum_reagents)bob.reagents).get_reagent_amount(typeof(datum_reagent_medicine_albuterol))) > 0);
        });
        r.Step(4 * Second); // the albuterol metabolizes: the lungs take more of each breath
        r.In(() => Assert.Equal(1.4f, lungs.received_pressure_mult, 3));
        // the canister comes out into Bob's hands, and an empty inhaler refuses
        r.In(() => Waiting.Detach(inhaler.attack_self(bob)));
        r.Step(2 * Second);
        Assert.True(feed.Any("canister removed"));
        r.In(() =>
        {
            Assert.Null(inhaler.canister);
            Assert.Equal(0, Dm.length(inhaler.underlays));
            Assert.Contains(canister, Dm.each(bob.held_items));
            Assert.False(Dm.truthy(inhaler.can_puff(bob, bob, silent: true)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheVapeOpensForVoltageAndEmagAndPuffsWhenWorn()
    {
        var (r, feed, bob) = Room(9306);
        var vape = r.In(() => Dm.@new<obj_item_vape>(bob.loc));
        var screwdriver = Hold<obj_item_screwdriver>(r, bob);
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal(50f, Dm.num(((datum_reagents)vape.reagents).total_volume));
            Waiting.NoSleep(vape.screwdriver_act(bob, screwdriver));
            Assert.Equal("vapeopen_low", vape.icon_state);
            Assert.True(Dm.truthy(Dm.bitand(((datum_reagents)vape.reagents).flags, OPENCONTAINER)));
            Waiting.NoSleep(vape.multitool_act(bob, null));
            Assert.Equal("vapeopen_med", vape.icon_state);
            Assert.True(Dm.truthy(vape.super));
            Assert.True(Dm.truthy(Waiting.NoSleep(vape.emag_act(bob, null))));
            Assert.Equal("vapeopen_high", vape.icon_state);
            Assert.False(Dm.truthy(vape.super));
            Waiting.NoSleep(vape.screwdriver_act(bob, screwdriver));
            Assert.False(Dm.truthy(Dm.bitand(((datum_reagents)vape.reagents).flags, OPENCONTAINER)));
        });
        r.In(() => Waiting.NoSleep(bob.equip_to_slot_or_del(vape, ITEM_SLOT_MASK)));
        r.Step(2);
        Assert.True(feed.Any("You start puffing on the vape."));
        r.In(() => Assert.True(Dm.truthy(vape.light_on)));
        r.Step(4 * Second);
        r.In(() => Assert.True(Dm.num(((datum_reagents)bob.reagents).get_reagent_amount(typeof(datum_reagent_drug_nicotine))) > 0));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBeaconsOfferTheirInstrumentsAugmentsAndArmaments()
    {
        var (r, feed, bob) = Room(9307);
        r.In(() =>
        {
            var music = Dm.@new<obj_item_choice_beacon_music>(bob.loc);
            DmList instruments = music.generate_display_names() as DmList;
            Assert.Equal(13, Dm.length(instruments));
            Assert.Equal(typeof(obj_item_instrument_violin), instruments["space violin"]);
            var augments = Dm.@new<obj_item_choice_beacon_augments>(bob.loc);
            Assert.Equal(6, Dm.length(augments.generate_display_names() as DmList));
            Waiting.NoSleep(augments.spawn_option(typeof(obj_item_organ_cyberimp_brain_anti_drop), bob));
            Assert.Single(Dm.each(bob.loc.contents).OfType<obj_item_organ_cyberimp_brain_anti_drop>());
            // the armaments beacon is the chaplain's alone
            var holy = Dm.@new<obj_item_choice_beacon_holy>(bob.loc);
            Assert.False(Dm.truthy(Waiting.NoSleep(holy.can_use_beacon(bob))));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSmallOddsWork()
    {
        var (r, feed, bob) = Room(9308);
        // the taster tastes a glass of water, and finds nothing in an empty one
        var taster = Hold<obj_item_taster>(r, bob);
        var glass = r.In(() => Dm.@new<obj_item_reagent_containers_cup_glass_drinkingglass>(locate(5, 4, 1)));
        r.In(() => Waiting.NoSleep(taster.interact_with_atom(glass, bob)));
        Assert.True(feed.Any("are empty") || feed.Any("is empty"));
        r.In(() => Waiting.NoSleep(((datum_reagents)glass.reagents).add_reagent(typeof(datum_reagent_water), 10)));
        r.In(() => Waiting.NoSleep(taster.interact_with_atom(glass, bob)));
        Assert.True(feed.Any("tastes <i>"));
        // the bear armor: once
        var bear = r.In(() => Dm.@new<mob_living_basic_bear>(locate(8, 6, 1)));
        var armor = r.In(() => Dm.@new<obj_item_bear_armor>(bob.loc));
        r.In(() =>
        {
            float max = bear.maxHealth;
            Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(armor.interact_with_atom(bear, bob))));
            Assert.Equal(max + 60, bear.maxHealth);
            Assert.True(Dm.truthy(bear.armored));
            Assert.True(QDELETED(armor));
            var more = Dm.@new<obj_item_bear_armor>(bob.loc);
            Assert.True(Dm.eq(ITEM_INTERACT_BLOCKING, Waiting.NoSleep(more.interact_with_atom(bear, bob))));
        });
        // the trash: a crushed can lands off centre, grinds to aluminium; a food can taps
        r.In(() =>
        {
            var can = Dm.@new<obj_item_trash_can>(locate(6, 6, 1));
            Assert.InRange(can.pixel_x, -4, 4);
            Assert.Equal(10f, Dm.num((Waiting.NoSleep(can.grind_results()) as DmList)[typeof(datum_reagent_aluminium)]));
            var food = Dm.@new<obj_item_trash_can_food_beans>(locate(6, 6, 1));
            Assert.True(HAS_TRAIT(food, TRAIT_CUSTOM_TAP_SOUND));
            var bag = Dm.@new<obj_item_trash_boritos_green>(locate(6, 6, 1));
            Assert.Equal(1f, Dm.num((Waiting.NoSleep(bag.grind_results()) as DmList)[typeof(datum_reagent_aluminium)]));
            Assert.Equal("razor", Dm.@new<obj_item_razor_surgery>(locate(6, 6, 1)).get_surgery_tool_overlay());
        });
        // the stingball's rebounds hit prone targets
        r.In(() =>
        {
            var pellet = Dm.@new<obj_projectile_bullet_pellet_stingball>(locate(9, 6, 1));
            pellet.on_ricochet(null);
            Assert.True(Dm.truthy(pellet.hit_prone_targets));
            Assert.True(Dm.truthy(pellet.ignore_range_hit_prone_targets));
        });
        // the ID's var edit relabels it
        r.In(() =>
        {
            var id = Dm.@new<obj_item_card_id_advanced>(locate(9, 6, 1));
            id.registered_name = "Bob";
            id.assignment = "Clown";
            Waiting.NoSleep(id.vv_edit_var(nameof(obj_item_card_id.assignment), "Mime"));
            Assert.Contains("Mime", Dm.str(id.name));
        });
        // the suicides
        r.In(() =>
        {
            Assert.Equal(OXYLOSS, Dm.num(Waiting.NoSleep(Dm.@new<obj_item_book_manual_wiki_security_space_law>(locate(9, 6, 1)).suicide_act(bob))));
            Assert.Equal(BRUTELOSS, Dm.num(Waiting.NoSleep(Dm.@new<obj_item_door_seal>(locate(9, 6, 1)).suicide_act(bob))));
        });
        Assert.True(feed.Any("promptly dies of laughter"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMalletHumiliatesTheSaneAndTheJerrycanWearsItsCap()
    {
        var (r, feed, bob) = Room(9309);
        var alice = r.Join(5, 4, "Alice");
        var mallet = Hold<obj_item_balloon_mallet>(r, bob);
        // a sane target (neutral sanity) is humiliated (gaps MS1, clown_items.dm:341), and the combat hit lands with the mallet's own force
        float before = r.In(() => Dm.num(alice.get_brute_loss()));
        r.In(() => bob.set_combat_mode(TRUE));
        r.In(() => Waiting.Detach(mallet.attack__proc(alice, bob)));
        r.Step(2);
        r.In(() => Assert.True(Dm.num(alice.get_brute_loss()) > before));
        r.In(() => Assert.IsType<datum_mood_event_mallet_humiliation>(alice.mob_mood.mood_events["humiliated"]));
        r.In(() =>
        {
            var can = Dm.@new<obj_item_reagent_containers_cup_jerrycan_opaque_yellow>(locate(6, 6, 1));
            var states = Dm.each(can.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList();
            Assert.Contains(states, s => s != null && s.EndsWith("_highlight", StringComparison.Ordinal));
        });
        // the party poster takes the tail pin where it is clicked
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(mallet)));
        var poster = r.In(() => Dm.@new<obj_structure_sign_poster_party_game>(locate(4, 5, 1)));
        var pin = Hold<obj_item_tail_pin>(r, bob);
        r.In(() => Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(poster.item_interaction(bob, pin, Dm.list(Dm.kv(ICON_X, "20"), Dm.kv(ICON_Y, "10")))))));
        r.In(() =>
        {
            Assert.Same(poster.loc, pin.loc); // the poster's drop location
            Assert.Equal(4f, pin.pixel_x);
            Assert.Equal(-6f, pin.pixel_y);
        });
        r.AssertNoRuntimes();
    }
}
