using System.Text.Json;
using System.Text.RegularExpressions;
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
/// Gaps package ST5 (D333): the structures' leftovers and the mines, in a booted toy room with Bob as a player: a landmine's arming
/// beep and a stun mine set off by the foot stepping off it, the mystery box's prize rising and claimed, the tripwire laid between two
/// stanchions stopping a runner, the shock trap's flare and its reveal, a signboard written with a pen, the voting box's operator
/// window, ballots and tally, the steam vent's hiss, the brass chair's spin and the plastic chair under a heavy sitter, the fence cut
/// open, the gun locker's radial, the petrified statue and its release, the paper frame torn and patched, the eye chart read, the ore
/// container's window and the false material wall's name.
/// </summary>
public class StructuresST5Tests
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

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who, Action<T> setup = null) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        if (setup != null)
            r.In(() => setup(item));
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    // a left click after tg's click cooldown of the one before
    private static void Use(AudioRoom r, mob who, atom target, string modifiers = "")
    {
        r.Step(Second);
        Click(r, who, target, modifiers);
    }

    // the answer to tg's input window of that interface the player has open (TextInputModal, ListInputWindow)
    private static void Answer(AudioRoom r, mob who, string iface, object entry)
    {
        r.Step(2);
        var open = r.In(() => Dm.each_cast<datum_tgui>(who.tgui_open_uis).Select(u => Dm.str(u.@interface)).ToList());
        Assert.True(open.Contains(iface), "no " + iface + " open; open: " + string.Join(", ", open));
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(who.tgui_open_uis).Last(u => Dm.str(u.@interface) == iface).window.id));
        r.Command(() => ClientSeam.TguiMessage(who.client, window, "act/submit", Dm.list(Dm.kv("entry", entry))));
        r.Step(4);
    }

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
        r.Step(6); // the radial's wait (a stoplag loop) resumes, then the pick runs
    }

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    private static string Examine(AudioRoom r, atom a, mob who) => r.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(a.examine(who))).Select(o => Dm.str(o))));

    private static bool Heard(AudioRoom r, string file) => r.Sounds.Heard.Any(h => (h.File ?? "").Contains(file, StringComparison.Ordinal));

    // the last browse() page the player got: its window's name and its HTML
    private static (string Window, string Html) LastPage(AudioRoom r, mob who) => r.In(() =>
    {
        TguiOutput page = who.client.dm_tgui_output.LastOrDefault(o => o.Kind == TguiOutputKind.Browse);
        Assert.True(page != null, "no browse() page");
        using JsonDocument doc = JsonDocument.Parse(page.Json);
        return (page.WindowId, doc.RootElement.GetProperty("html").GetString());
    });

    private static void FollowLink(AudioRoom r, mob who, string html, string act)
    {
        Match link = Regex.Match(html, "href='(byond://\\?[^']*act=" + act + ")'");
        Assert.True(link.Success, "no link " + act + " in " + html);
        r.Command(() => ClientSeam.Topic(who.client, link.Groups[1].Value));
        r.Step(4);
    }

    [Fact]
    public void ALandmineArmsWithABeepAndAStunMineGoesOffWhenTheFootLeavesIt()
    {
        var (r, feed, bob) = Room(8501);
        var arming = r.In(() => Dm.@new<obj_effect_mine_shrapnel_capspawn>(locate(9, 7, 1)));
        r.Step(2);
        r.In(() =>
        {
            Assert.False(Dm.truthy(arming.armed));
            Assert.Equal("landmine-inactive", arming.icon_state);
        });
        r.Step(4 * Second); // arm_delay 3 s
        r.In(() =>
        {
            Assert.True(Dm.truthy(arming.armed));
            Assert.Equal("landmine", arming.icon_state);
            Assert.True(Dm.truthy(arming.light_on)); // the capspawn mine's red light
        });
        Assert.True(Heard(r, "angry_beep"));
        r.In(() => qdel(arming));

        var mine = Place<obj_effect_mine_stun>(r, 6, 6);
        mob_living_carbon_human dummy = r.Dummy(6, 5);
        r.In(() => Waiting.NoSleep(dummy.Move(locate(6, 6, 1), NORTH)));
        r.Step(2);
        r.In(() =>
        {
            Assert.Same(dummy, mine.foot_on_mine?.resolve());
            Assert.False(QDELETED(mine)); // the pressure plate is held down
        });
        Assert.True(feed.Any("*click*"));
        Assert.Contains("The pressure plate is depressed by", Examine(r, mine, bob));

        r.In(() => Waiting.NoSleep(dummy.Move(locate(7, 6, 1), EAST)));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(QDELETED(mine));
            Assert.True(Dm.truthy(dummy.IsParalyzed()));
        });
        Assert.True(feed.Any("sets off"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMysteryBoxRaisesAPrizeThatBobClaims()
    {
        var (r, feed, bob) = Room(8502);
        var box = Place<obj_structure_mystery_box_tdome>(r, 5, 4);
        r.In(() =>
        {
            Assert.Equal(obj_structure_mystery_box.MYSTERY_BOX_STANDBY, box.box_state);
            Assert.Equal("wooden", box.icon_state);
            Assert.True(Dm.length(box.valid_types) > 0);
        });

        Use(r, bob, box);
        r.In(() =>
        {
            Assert.Equal(obj_structure_mystery_box.MYSTERY_BOX_CHOOSING, box.box_state);
            Assert.Equal("woodenopen", box.icon_state);
            Assert.NotNull(box.presented_item);
            Assert.True(Dm.@in(box.presented_item, box.vis_contents));
        });
        Assert.True(Heard(r, "crate_open"));
        Assert.True(Heard(r, "mbox_full"));

        r.Step(6 * Second); // MBOX_DURATION_CHOOSING, 5 s
        Type prize = r.In(() =>
        {
            Assert.Equal(obj_structure_mystery_box.MYSTERY_BOX_PRESENTING, box.box_state);
            Assert.True(Dm.truthy(box.presented_item.claimable));
            Assert.True(Dm.@in(box.presented_item.selected_path, box.valid_types));
            return box.presented_item.selected_path;
        });

        Use(r, bob, box);
        r.In(() =>
        {
            Assert.True(Dm.each<obj_item>(bob.held_items).Any(h => h.GetType() == prize), "Bob does not hold the prize " + prize.Name);
            Assert.Null(box.presented_item);
        });
        Assert.True(Heard(r, "mbox_end"));
        r.Step(5 * Second);
        r.In(() =>
        {
            Assert.Equal(obj_structure_mystery_box.MYSTERY_BOX_STANDBY, box.box_state);
            Assert.Equal("wooden", box.icon_state);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ATripwireLaidBetweenTwoStanchionsStopsARunnerButNotAWalker()
    {
        var (r, feed, bob) = Room(8503);
        var west = Place<obj_structure_tripwire_post>(r, 3, 4);
        var east = Place<obj_structure_tripwire_post>(r, 5, 4);
        Hold<obj_item_tripwire_cable>(r, bob);

        Use(r, bob, west);
        Assert.True(feed.Any("You tie one end"));
        Use(r, bob, east);
        r.Step(7 * Second); // three seconds a tile, two tiles
        obj_structure_tripwire_cable wire = r.In(() => Dm.locate<obj_structure_tripwire_cable>(locate(4, 4, 1)));
        Assert.NotNull(wire);
        r.In(() =>
        {
            Assert.Equal("tripwire_1", wire.icon_state);
            Assert.Same(east, west.opposing_post);
            Assert.Same(west, east.opposing_post);
            Assert.Equal("tripwire_post_tied", west.icon_state);
            Assert.Equal("tripwire_post_tied", east.icon_state);
            Assert.Null(bob.get_active_held_item()); // the cable went into the wire
        });
        Assert.True(feed.Any("You successfully wire together the two posts."));

        r.In(() => Waiting.NoSleep(bob.forceMove(locate(8, 7, 1))));
        mob_living_carbon_human runner = r.Dummy(4, 3);
        r.In(() =>
        {
            runner.move_intent = MOVE_INTENT_RUN;
            Waiting.NoSleep(runner.Move(locate(4, 4, 1), NORTH));
        });
        r.Step(2);
        r.In(() => Assert.Same(locate(4, 3, 1), runner.loc));
        r.Step(Second);
        r.In(() =>
        {
            runner.move_intent = MOVE_INTENT_WALK;
            Waiting.NoSleep(runner.Move(locate(4, 4, 1), NORTH));
        });
        r.Step(2);
        r.In(() => Assert.Same(locate(4, 4, 1), runner.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AShockTrapFlaresAndShocksWhoStepsOnItAndIsRevealedWhenExaminedClose()
    {
        var (r, feed, bob) = Room(8504);
        var trap = Place<obj_structure_trap_stun>(r, 7, 6);
        r.In(() =>
        {
            Assert.Equal(30f, Dm.num(trap.alpha));
            trap.last_trigger = -trap.time_between_triggers; // the toy room's clock starts at 0: a trap is not set off in a world's first minute
        });
        mob_living_carbon_human dummy = r.Dummy(7, 5);
        r.In(() => Waiting.NoSleep(dummy.Move(locate(7, 6, 1), NORTH)));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(Dm.truthy(dummy.IsParalyzed()));
            Assert.True(trap.last_trigger > 0);
        });
        Assert.True(feed.Any("flares brightly!"));

        var hidden = Place<obj_structure_trap_stun>(r, 5, 4);
        Assert.Contains("You reveal", Examine(r, hidden, bob));
        r.In(() => Assert.True(hidden.last_trigger > 0, "examined from a step away, the trap did not flare"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASignboardIsWrittenWithAPenAndReadOnExamine()
    {
        var (r, feed, bob) = Room(8505);
        var sign = Place<obj_structure_signboard>(r, 5, 4);
        r.In(() => Assert.Equal("sign_blank", sign.icon_state));
        Hold<obj_item_pen>(r, bob);

        Use(r, bob, sign);
        Answer(r, bob, "TextInputModal", "Free donuts");
        r.In(() =>
        {
            Assert.Equal("Free donuts", Dm.str(sign.sign_text));
            Assert.Equal("sign", sign.icon_state);
            Assert.NotNull(sign.text_image);
        });
        Assert.Contains("Free donuts", Examine(r, sign, bob));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheVotingBoxRegistersItsOperatorTakesBallotsAndPrintsTheTally()
    {
        var (r, feed, bob) = Room(8506);
        var box = Place<obj_structure_votebox>(r, 5, 4);
        r.In(() => Assert.Equal("votebox_maint", box.icon_state));
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(bob.loc), ITEM_SLOT_ICLOTHING)))));
        var card = Hold<obj_item_card_id_advanced>(r, bob, c => c.registered_name = "Bob");
        Use(r, bob, box);
        r.In(() => Assert.Same(card, box.owner));
        r.In(() => Waiting.NoSleep(bob.equip_to_slot_or_del(card, ITEM_SLOT_ID)));
        r.Step(2);
        r.In(() => Assert.Same(card, bob.wear_id));

        (string window, string html) = LastPage(r, bob);
        Assert.Equal("votebox", window);
        Assert.Contains("Maintenance Mode", html);
        FollowLink(r, bob, html, "toggle_vote");
        r.In(() =>
        {
            Assert.True(Dm.truthy(box.voting_active));
            Assert.Equal("votebox_active", box.icon_state);
        });
        Assert.Contains("Active", LastPage(r, bob).Html);

        foreach (string vote in new[] { "Pizza", "Pizza", "Tacos" })
        {
            Hold<obj_item_paper>(r, bob, p => p.add_raw_text(vote));
            Use(r, bob, box);
        }
        r.In(() => Assert.Equal(3, Dm.each<obj_item_paper>(box.contents).Count()));

        FollowLink(r, bob, LastPage(r, bob).Html, "tally");
        r.In(() =>
        {
            obj_item_paper tally = Dm.each<obj_item_paper>(bob.held_items).SingleOrDefault(p => p.name == "Voting Results");
            Assert.True(tally != null, "no tally in Bob's hands");
            string text = string.Concat(Dm.each_cast<datum_paper_input>(tally.raw_text_inputs).Select(t => Dm.str(t.raw_text)));
            Assert.Contains("Pizza</span>\" - 2 Votes.", text);
            Assert.Contains("Tacos</span>\" - 1 Vote.", text);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnOpenSteamVentHissesWhenSomeoneWalksOffIt()
    {
        var (r, feed, bob) = Room(8507);
        var vent = Place<obj_structure_steam_vent>(r, 5, 4);
        r.In(() =>
        {
            vent.vent_active = TRUE;
            Waiting.NoSleep(vent.update_icon_state());
            Assert.Equal("steam_vent", vent.icon_state);
        });
        mob_living_carbon_human dummy = r.Dummy(5, 4);
        r.Step(2);
        r.In(() => Waiting.NoSleep(dummy.Move(locate(6, 4, 1), EAST)));
        r.Step(2);
        Assert.True(Heard(r, "steam_hiss"));
        r.In(() => Assert.NotNull(Dm.locate<obj_effect_particle_effect_fluid_smoke>(locate(5, 4, 1))));

        Use(r, bob, vent); // the valve shut, on its cooldown: not ready
        Assert.True(feed.Any("balloon: not ready to adjust!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBrassChairSpinsEightTimesAndAHeavySitterBreaksAPlasticChair()
    {
        var (r, feed, bob) = Room(8508);
        var brass = Place<obj_structure_chair_bronze>(r, 5, 4);
        object startDir = r.In(() => (object)brass.dir);
        r.In(() => Waiting.NoSleep(brass.click_alt(bob)));
        r.In(() => Assert.True(Dm.truthy(Dm.bitand(brass.datum_flags, DF_ISPROCESSING))));
        r.Step(4 * Second);
        r.In(() =>
        {
            Assert.Equal(8f, brass.turns);
            Assert.False(Dm.truthy(Dm.bitand(brass.datum_flags, DF_ISPROCESSING)));
            Assert.True(Dm.eq(brass.dir, startDir), "eight quarter turns bring the chair round twice");
        });
        Assert.True(Heard(r, "servostep"));
        Assert.True(feed.Any("Automated spinny chairs."));

        var plastic = Place<obj_structure_chair_plastic>(r, 7, 6);
        mob_living_carbon_human heavy = r.Dummy(7, 6);
        r.In(() => Waiting.NoSleep(heavy.set_nutrition(NUTRITION_LEVEL_FAT + 50)));
        r.In(() => Waiting.NoSleep(plastic.buckle_mob(heavy, force: TRUE)));
        r.Step(7 * Second);
        r.In(() =>
        {
            Assert.True(QDELETED(plastic));
            Assert.NotNull(Dm.locate<obj_effect_decal_cleanable_plastic>(locate(7, 6, 1)));
        });
        Assert.True(feed.Any("The plastic chair snaps under"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WirecuttersCutAFenceOpenTillItIsWalkedThrough()
    {
        var (r, feed, bob) = Room(8509);
        var fence = Place<obj_structure_fence>(r, 5, 4);
        Hold<obj_item_wirecutters>(r, bob);
        Use(r, bob, fence);
        r.Step(11 * Second); // CUT_TIME, ten seconds
        r.In(() =>
        {
            Assert.Equal(obj_structure_fence.MEDIUM_HOLE, fence.hole_size);
            Assert.Equal("straight_cut2", fence.icon_state);
            Assert.True(Dm.truthy(fence.density));
        });
        Assert.Contains("There is a large hole in", Examine(r, fence, bob));
        Use(r, bob, fence);
        r.Step(11 * Second);
        r.In(() =>
        {
            Assert.Equal(obj_structure_fence.LARGE_HOLE, fence.hole_size);
            Assert.Equal("straight_cut3", fence.icon_state);
            Assert.False(Dm.truthy(fence.density));
        });

        var door = Place<obj_structure_fence_door>(r, 3, 4);
        r.In(() => Assert.Equal("door_closed", door.icon_state));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(bob.get_active_held_item() as obj_item)));
        Use(r, bob, door);
        r.In(() =>
        {
            Assert.Equal("door_opened", door.icon_state);
            Assert.False(Dm.truthy(door.density));
        });
        Assert.True(Heard(r, "machines/click"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheShotgunLockerShowsItsGunsAndHandsOneOutOnItsRadial()
    {
        var (r, feed, bob) = Room(8510);
        var locker = Place<obj_structure_guncase_shotgun>(r, 5, 4);
        r.In(() => Assert.Contains("shotguncase_open", Overlays(r, locker)));
        var first = Hold<obj_item_gun_ballistic_shotgun>(r, bob);
        Use(r, bob, locker);
        var second = Hold<obj_item_gun_ballistic_shotgun>(r, bob);
        Use(r, bob, locker);
        r.In(() =>
        {
            Assert.Same(locker, first.loc);
            Assert.Same(locker, second.loc);
            List<string> overlays = Overlays(r, locker);
            Assert.Equal(2, overlays.Count(o => o == "shotgun")); // one gun drawn per slot
            Assert.Contains("shotguncase_open", overlays);
        });

        Use(r, bob, locker); // two guns: tg's radial offers them by name and slot
        PickRadial(r, bob, "(2)");
        r.In(() =>
        {
            Assert.Same(bob, second.loc);
            Assert.Same(locker, first.loc);
            Assert.Single(Overlays(r, locker), o => o == "shotgun");
        });

        r.In(() => Waiting.NoSleep(bob.dropItemToGround(second)));
        Use(r, bob, locker); // one gun left: the radial picks it alone (autopick_single_option)
        r.In(() =>
        {
            Assert.Same(bob, first.loc);
            Assert.DoesNotContain("shotgun", Overlays(r, locker));
        });

        r.In(() => Waiting.NoSleep(bob.dropItemToGround(first)));
        Use(r, bob, locker); // empty: the hand shuts the door
        r.In(() => Assert.Contains("shotguncase_door", Overlays(r, locker)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APetrifiedHumanIsHeldInAStatueUntilItLapses()
    {
        var (r, feed, bob) = Room(8511);
        mob_living_carbon_human victim = r.Dummy(6, 6);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(victim.petrify(3 * SECONDS)))));
        obj_structure_statue_petrified statue = r.In(() => victim.loc as obj_structure_statue_petrified);
        Assert.NotNull(statue);
        r.In(() =>
        {
            Assert.Equal("statue of " + victim.name, statue.name);
            Assert.True(HAS_TRAIT(victim, TRAIT_GODMODE));
            Assert.True(HAS_TRAIT(victim, TRAIT_MUTE));
            Assert.True(Dm.@in(FACTION_MIMIC, victim.faction));
            Assert.Equal(Dm.num(victim.health) + 100, Dm.num(statue.atom_integrity));
        });
        Assert.True(feed.Any("skin rapidly turns to marble!"));
        r.Step(5 * Second);
        r.In(() =>
        {
            Assert.True(QDELETED(statue));
            Assert.Same(locate(6, 6, 1), victim.loc);
            Assert.False(HAS_TRAIT(victim, TRAIT_GODMODE));
            Assert.True(Dm.truthy(victim.IsParalyzed()));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APaperFrameTornByAPunchIsPatchedWithPaper()
    {
        var (r, feed, bob) = Room(8512);
        var frame = Place<obj_structure_window_paperframe>(r, 5, 4);
        r.In(() =>
        {
            Assert.True(Dm.truthy(frame.opacity));
            Assert.Contains("paper-whole", Overlays(r, frame));
        });
        r.In(() => bob.set_combat_mode(TRUE));
        Use(r, bob, frame);
        r.In(() =>
        {
            Assert.True(Dm.num(frame.atom_integrity) < frame.max_integrity);
            Assert.False(Dm.truthy(frame.opacity));
            Assert.Contains("paper-torn", Overlays(r, frame));
        });
        Assert.Contains("you may be able to fix it with some", Examine(r, frame, bob));

        r.In(() => bob.set_combat_mode(FALSE));
        r.In(() => frame.atom_integrity = frame.max_integrity - 3);
        Hold<obj_item_paper>(r, bob);
        Use(r, bob, frame);
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.Equal(frame.max_integrity, Dm.num(frame.atom_integrity));
            Assert.True(Dm.truthy(frame.opacity));
            Assert.Contains("paper-whole", Overlays(r, frame));
        });
        Assert.True(feed.Any("patches some of the holes in"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEyeChartIsReadDownToTheRedLineByGoodEyes()
    {
        var (r, feed, bob) = Room(8513);
        var chart = Place<obj_structure_sign_eyechart>(r, 6, 4);
        string text = Examine(r, chart, bob);
        Assert.Contains("You read through the chart", text);
        Assert.Contains("you can read down to the red line.", text);
        r.In(() => ADD_TRAIT(bob, TRAIT_COLORBLIND, "test"));
        Assert.Contains("brown - wait, isn't it supposed to be red?", Examine(r, chart, bob));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheOreContainersWindowListsItsOresAndDropsAWithdrawnStack()
    {
        var (r, feed, bob) = Room(8514);
        var container = Place<obj_structure_ore_container>(r, 5, 4);
        var iron = Hold<obj_item_stack_ore_iron>(r, bob, s => Waiting.NoSleep(s.add(4)));
        Use(r, bob, container);
        r.In(() => Assert.Same(container, iron.loc));

        Take(bob);
        r.In(() => Waiting.NoSleep(container.ui_interact(bob)));
        r.Step(2);
        JsonElement payload = LastUpdate(Take(bob), out _);
        Assert.Equal("OreContainer", InterfaceOf(payload));
        JsonElement ore = payload.GetProperty("data").GetProperty("ores")[0];
        Assert.Equal("iron ore", ore.GetProperty("name").GetString());
        Assert.Equal(5, ore.GetProperty("amount").GetInt32());
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(bob.tgui_open_uis).Last(u => Dm.str(u.@interface) == "OreContainer").window.id));
        Act(r, bob, window, "withdraw", ("reference", ore.GetProperty("id").GetString()));
        r.In(() => Assert.Same(bob.loc, iron.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMaterialFalseWallIsNamedForItsMaterialAndOpensOnAHand()
    {
        var (r, feed, bob) = Room(8515);
        var wall = r.In(() =>
        {
            var made = Dm.@new<obj_structure_falsewall_material>(locate(5, 4, 1));
            Waiting.NoSleep(made.set_custom_materials(Dm.list(Dm.kv(typeof(datum_material_iron), SHEET_MATERIAL_AMOUNT * 2))));
            return made;
        });
        r.Step(Second);
        r.In(() =>
        {
            Assert.Contains("iron", wall.desc);
            Assert.True(Dm.truthy(wall.density));
        });
        Use(r, bob, wall);
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.False(Dm.truthy(wall.density));
            Assert.False(Dm.truthy(wall.opacity));
        });
        r.AssertNoRuntimes();
    }
}
