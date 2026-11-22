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
/// Gaps package DV2 (D333): devices, implants, the RCD family and the disks, in a booted toy room with Bob as a player: the boombox
/// playing a tape from its radial, the forcefield projector's field drained by hits and taken down, a pressure plate clicking under a
/// foot, two linked swappers trading their holders' places, the table clock broken and fixed, the machine wand's bug taking an
/// airlock, the wind turbine on a back turning and charging a cell, the implant pad's window, the mindshield implanter's window, the
/// grounding implant flinging a teleport back, the RWD's cable, the floppy disks' stickers and stacks, the penlight's holosign and
/// the black market and theme disks' programs.
/// </summary>
public class DevicesDV2Tests
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

    // the answer to tg's input window of that interface the player has open (NumberInputModal)
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
            int i = menu.dm_elements.FindIndex(e => e.Name != null && e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            Assert.True(i >= 0, "no slice " + name + " in " + string.Join(", ", menu.dm_elements.Select(e => e.Name)));
            return (menu.dm_id, i + 1);
        });
        r.Command(() => ClientSeam.RadialPick(who.client, id, index));
        r.Step(6); // the radial's wait (a stoplag loop) resumes, then the pick runs
    }

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    private static string Examine(AudioRoom r, atom a, mob who) => r.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(a.examine(who))).Select(o => Dm.str(o))));

    private static bool Heard(AudioRoom r, string file) => r.Sounds.Heard.Any(h => (h.File ?? "").Contains(file, StringComparison.Ordinal));

    [Fact]
    public void TheBoomboxPlaysATapeFromItsRadialAndStopsIt()
    {
        var (r, feed, bob) = Room(9201);
        var box = Hold<obj_item_boombox>(r, bob);
        var tape = r.In(() => Dm.@new<obj_item_music_tape_rock>(bob.loc));
        r.Step(2);
        Assert.Contains("no tapedeck inside", Examine(r, box, bob));
        r.In(() => Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(box.item_interaction(bob, tape)))));
        r.In(() =>
        {
            Assert.Same(tape, box.tapedeck);
            Assert.Same(box, tape.loc);
            Assert.IsAssignableFrom<datum_looping_sound_boombox_rock>(tape.song_inside); // the tape's song made at its Initialize
        });
        Assert.True(Heard(r, "taperecorder_close"));
        Assert.True(feed.Any("balloon: tape inserted"));

        r.Command(() => Waiting.Detach(box.attack_self(bob)));
        PickRadial(r, bob, "Play");
        r.Step(Second);
        r.In(() =>
        {
            Assert.Equal("boombox_on", box.icon_state);
            Assert.True(Dm.truthy(box.active));
            Assert.Same(tape.song_inside, box.boombox_audio);
        });
        Assert.True(Heard(r, "sound/music/boombox/bainmack-rock.ogg"), "the tape's song plays");

        // the radial now offers Stop and Eject
        r.Command(() => Waiting.Detach(box.attack_self(bob)));
        PickRadial(r, bob, "Stop");
        r.In(() =>
        {
            Assert.Equal("boombox", box.icon_state);
            Assert.False(Dm.truthy(box.active));
            Assert.Null(box.boombox_audio);
        });

        // Alt-Right-click: worn on the shoulder
        r.In(() => Waiting.NoSleep(box.click_alt_secondary(bob)));
        r.In(() => Assert.Equal("boombox_swag", box.inhand_icon_state));
        Assert.True(feed.Any("balloon: wearing on shoulder"));

        r.Command(() => Waiting.Detach(box.attack_self(bob)));
        PickRadial(r, bob, "Eject");
        r.In(() =>
        {
            Assert.Null(box.tapedeck);
            Assert.Same(bob.loc, tape.loc);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheForcefieldProjectorRaisesAFieldThatHitsDrainAndTakesItDown()
    {
        var (r, feed, bob) = Room(9202);
        var projector = Hold<obj_item_forcefield_projector>(r, bob);
        turf target = r.In(() => locate(7, 4, 1));
        Use(r, bob, target);
        r.Step(2 * Second); // creation_time, a second's do_after
        var field = r.In(() => Dm.locate_type(typeof(obj_structure_projected_forcefield), target) as obj_structure_projected_forcefield);
        Assert.NotNull(field);
        r.In(() =>
        {
            Assert.Equal("forcefield", field.icon_state);
            Assert.True(Dm.truthy(field.density));
            Assert.Same(projector, field.generator);
            Assert.Equal(1, Dm.length(projector.current_fields));
        });
        Assert.True(Heard(r, "resonator_fire"));
        Assert.Contains("1/3 fields", Examine(r, projector, bob));

        // a hit drains the projector, not the field
        r.In(() => Waiting.NoSleep(field.take_damage(40, BRUTE, MELEE)));
        r.In(() =>
        {
            Assert.False(QDELETED(field));
            Assert.True(projector.shield_integrity <= 210 && projector.shield_integrity > 200, "integrity " + projector.shield_integrity);
        });
        Assert.True(Heard(r, "egloves"));

        // used in hand: every field goes
        r.In(() => Waiting.NoSleep(projector.attack_self(bob)));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(QDELETED(field));
            Assert.Equal(0, Dm.length(projector.current_fields));
        });
        Assert.True(Heard(r, "resonator_blast"));
        Assert.True(feed.Any("flickers and disappears"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APressurePlateTurnedOnClicksUnderAFoot()
    {
        var (r, feed, bob) = Room(9203);
        var plate = Place<obj_item_pressure_plate>(r, 6, 4);
        r.In(() =>
        {
            Assert.False(Dm.truthy(plate.active));
            Assert.Equal("pp_overlay", plate.tile_overlay.icon_state);
        });
        r.In(() => Waiting.NoSleep(plate.item_ctrl_click(bob)));
        r.In(() => Assert.True(Dm.truthy(plate.active)));
        Assert.True(feed.Any("You turn"));

        r.Step(Second);
        r.In(() => Waiting.NoSleep(bob.Move(locate(5, 4, 1), EAST)));
        r.Step(Second);
        r.In(() => Waiting.NoSleep(bob.Move(locate(6, 4, 1), EAST)));
        r.Step(4);
        r.AssertNoRuntimes();
        Assert.True(feed.Any("You feel something click beneath you!"), r.In(() => $"bob at {bob.x},{bob.y}; plate at {plate.x},{plate.y} in {plate.loc}; active {plate.active}, "
            + $"can_trigger {plate.can_trigger}; signals: " + string.Join("; ", Dm.each(plate._signal_procs).Select(t => t?.GetType().Name + " " + string.Join(",", Dm.each(plate._signal_procs[t] as DmList).Select(k => Dm.str(k) + "=" + Dm.str((plate._signal_procs[t] as DmList)[k])))))
            + "; chat: " + string.Join(" | ", feed.Chat.Select(c => c.Html))));
        r.In(() => Assert.False(Dm.truthy(plate.can_trigger)));
        r.Step(2 * Second); // trigger_delay, a second
        r.In(() => Assert.True(Dm.truthy(plate.can_trigger)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TwoLinkedSwappersTradeTheirHoldersPlaces()
    {
        var (r, feed, bob) = Room(9204);
        r.Step(31 * Second); // tg's swap reads the linked swapper's cooldown (30 s) against world.time
        var mine = Hold<obj_item_swapper>(r, bob);
        var other = Place<obj_item_swapper>(r, 10, 6);
        r.In(() => Waiting.NoSleep(other.item_interaction(bob, mine)));
        r.In(() =>
        {
            Assert.Same(other, mine.linked_swapper);
            Assert.Same(mine, other.linked_swapper);
            Assert.Equal("swapper-linked", mine.icon_state);
            Assert.Equal("swapper-linked", other.icon_state);
        });
        Assert.True(feed.Any("You establish a quantum link"));
        Assert.Contains("Linked.", Examine(r, mine, bob));

        turf start = r.In(() => bob.loc as turf);
        r.In(() => Waiting.NoSleep(mine.attack_self(bob)));
        r.Step(4 * Second); // the swap comes 2.5 seconds later
        r.In(() =>
        {
            Assert.Same(locate(10, 6, 1), bob.loc);
            Assert.Same(start, other.loc);
        });
        Assert.True(Heard(r, "swapper/swap_a"));
        Assert.True(Heard(r, "swapper/swap_b"));

        // Alt-click breaks the link
        r.In(() => Waiting.NoSleep(mine.click_alt(bob)));
        r.In(() =>
        {
            Assert.Null(mine.linked_swapper);
            Assert.Null(other.linked_swapper);
            Assert.Equal("swapper", other.icon_state);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTableClockTellsTheTimeBreaksAndIsFixedByHand()
    {
        var (r, feed, bob) = Room(9205);
        var clock = Place<obj_item_table_clock>(r, 5, 4);
        Assert.Contains("The current NST (local) time is", Examine(r, clock, bob));
        r.In(() => clock.break_clock(Dm.resource("sound/effects/footstep/glass_step.ogg")));
        r.In(() =>
        {
            Assert.True(Dm.truthy(clock.broken));
            Assert.Equal("table_clock_broken", clock.icon_state);
            Assert.Equal(1, Dm.num(clock.times_broken));
        });
        Assert.True(Heard(r, "glass_step"));
        Assert.Contains("currently broken", Examine(r, clock, bob));

        r.Command(() => Waiting.Detach(clock.interact(bob)));
        r.Step(11 * Second); // ten seconds of fixing
        r.In(() =>
        {
            Assert.False(Dm.truthy(clock.broken));
            Assert.Equal("table_clock", clock.icon_state);
        });
        Assert.True(feed.Any("balloon: clock repaired!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMachineWandSendsABugThatTakesControlOfAnAirlock()
    {
        var (r, feed, bob) = Room(9206);
        var wand = Hold<obj_item_machine_remote>(r, bob);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(bob, TRAIT_AI_ACCESS)); // held in the active hand
            Assert.True(HAS_TRAIT(bob, TRAIT_SILICON_ACCESS));
        });
        var door = r.In(() => Dm.@new<obj_machinery_door_airlock>(locate(10, 4, 1)));
        r.Step(Second);
        Use(r, bob, door);
        var bug = r.In(() => wand.moving_bug);
        Assert.NotNull(bug);
        r.In(() =>
        {
            Assert.Equal("fly", bug.icon_state);
            Assert.Same(door, bug.thing_moving_towards);
        });
        r.Step(6 * Second); // the bug homes onto the door at a step every half second
        r.In(() =>
        {
            Assert.True(QDELETED(bug));
            Assert.Null(wand.moving_bug);
            Assert.Same(door, wand.controlling_machine_or_bot);
        });
        Assert.Contains("fly-surrounding", Overlays(r, door));
        Assert.Contains("It is currently controlling", Examine(r, wand, bob));

        // Alt-click lets go
        r.In(() => Waiting.NoSleep(wand.click_alt(bob)));
        r.In(() => Assert.Null(wand.controlling_machine_or_bot));
        Assert.DoesNotContain("fly-surrounding", Overlays(r, door));

        // an EMP'd remote is disabled for two minutes
        r.In(() => wand.disable_remote(2 * MINUTES));
        Use(r, bob, door);
        r.In(() => Assert.Null(wand.moving_bug));
        Assert.True(Heard(r, "synth_no"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheWindTurbineOnABackTurnsAsItsWearerWalksAndChargesACell()
    {
        var (r, feed, bob) = Room(9207);
        var turbine = r.In(() => Dm.@new<obj_item_portable_wind_turbine_loaded>(bob.loc));
        r.Step(2);
        r.In(() =>
        {
            Assert.NotNull(turbine.cap);
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(turbine, ITEM_SLOT_BACK))));
        });
        Assert.Contains("rotor_0", Overlays(r, turbine));
        for (int x = 5; x <= 9; x++)
        {
            int step = x;
            r.In(() => Waiting.NoSleep(bob.Move(locate(step, 4, 1), EAST)));
            r.Step(4);
        }
        r.In(() =>
        {
            Assert.True(turbine.available_power > 0, "power " + turbine.available_power);
            Assert.True(turbine.rotor_tick < 4);
        });
        Assert.True(Heard(r, "woosh"), "a full turn of the rotor wooshes");
        Assert.Contains("currently storing", Examine(r, turbine, bob));

        // a cell hung on it takes the stored power
        var cell = Hold<obj_item_stock_parts_power_store_cell_empty>(r, bob);
        r.In(() => Waiting.NoSleep(turbine.item_interaction(bob, cell)));
        r.In(() =>
        {
            Assert.Same(cell, turbine.charging);
            Assert.Same(turbine, cell.loc);
        });
        r.Step(4 * Second);
        r.In(() => Assert.True(cell.charge > 0, "the cell charged"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheImplantPadReadsACaseInItsWindowAndEjectsIt()
    {
        var (r, feed, bob) = Room(9208);
        var pad = Hold<obj_item_implantpad>(r, bob);
        var implantCase = r.In(() => Dm.@new<obj_item_implantcase_tracking>(bob.loc));
        r.Step(2);
        Assert.Contains("currently empty", Examine(r, pad, bob));
        r.In(() => Waiting.NoSleep(pad.item_interaction(bob, implantCase)));
        r.In(() =>
        {
            Assert.Same(implantCase, pad.inserted_case);
            Assert.Equal("implantpad-1", pad.icon_state);
        });

        r.In(() => Take(bob));
        r.Command(() => Waiting.Detach(pad.ui_interact(bob)));
        var data = Data(r, bob, out string window);
        Assert.Equal(1, data.GetProperty("has_case").GetInt32());
        Assert.Equal(1, data.GetProperty("has_implant").GetInt32());
        Assert.False(string.IsNullOrEmpty(data.GetProperty("case_information").GetString()));

        Act(r, bob, window, "eject_implant");
        r.In(() =>
        {
            Assert.Null(pad.inserted_case);
            Assert.Equal("implantpad-0", pad.icon_state);
            Assert.True(Dm.truthy(bob.is_holding(implantCase)));
        });
        Assert.True(feed.Any("balloon: case removed"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMindshieldImplanterImplantsItsOccupantFromItsWindow()
    {
        var (r, feed, bob) = Room(9209);
        var chair = Place<obj_machinery_implantchair>(r, 6, 4);
        mob_living_carbon_human al = r.Join(7, 4, "Al");
        r.Step(Second);
        Assert.Contains("ready", Overlays(r, chair));
        r.In(() => Waiting.NoSleep(chair.close_machine(bob)));
        r.In(() =>
        {
            Assert.Same(bob, chair.occupant);
            Assert.Same(chair, bob.loc);
        });

        r.In(() => Take(al));
        r.Command(() => Waiting.Detach(chair.ui_interact(al)));
        var data = Data(r, al, out string window);
        Assert.Equal(1, data.GetProperty("occupied").GetInt32());
        Assert.Equal(r.In(() => bob.name), data.GetProperty("occupant").GetProperty("name").GetString());
        Assert.Equal(5, data.GetProperty("ready_implants").GetInt32());

        Act(r, al, window, "implant");
        r.In(() =>
        {
            Assert.Contains(Dm.each(bob.implants), i => i is obj_item_implant_mindshield);
            Assert.Equal(4, chair.ready_implants);
            Assert.False(Dm.truthy(chair.ready));
            Assert.True(Dm.truthy(chair.replenishing));
        });
        Assert.DoesNotContain("ready", Overlays(r, chair));
        r.Step(61 * Second); // a minute to be ready again
        r.In(() => Assert.True(Dm.truthy(chair.ready)));
        Assert.Contains("ready", Overlays(r, chair));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AGroundingImplantFlingsATeleportBack()
    {
        var (r, feed, bob) = Room(9210);
        var implant = r.In(() => Dm.@new<obj_item_implant_teleport_blocker>(bob.loc));
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(implant.implant(bob, bob, TRUE)))));
        turf start = r.In(() => bob.loc as turf);
        object moved = r.In(() => Waiting.NoSleep(do_teleport(bob, locate(9, 6, 1), channel: TELEPORT_CHANNEL_BLUESPACE)));
        r.Step(2);
        r.In(() =>
        {
            Assert.False(Dm.truthy(moved));
            Assert.Same(start, bob.loc);
            Assert.NotNull(bob.has_status_effect(typeof(datum_status_effect_incapacitating_paralyzed)));
        });
        Assert.True(feed.Any("suddenly flung back to where you just were"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRwdTakesCableAndDispensesACoilOfAChosenLength()
    {
        var (r, feed, bob) = Room(9211);
        var rwd = Hold<obj_item_rwd>(r, bob);
        var coil = r.In(() => Dm.@new<obj_item_stack_cable_coil>(bob.loc));
        r.Step(2);
        float fed = r.In(() => Dm.num(coil.amount));
        r.In(() => Waiting.NoSleep(rwd.item_interaction(bob, coil)));
        r.In(() =>
        {
            Assert.Equal(fed, rwd.current_amount);
            Assert.Equal("rwd-10-layer2", rwd.icon_state);
        });

        r.Command(() => Waiting.Detach(rwd.attack_self_secondary(bob)));
        Answer(r, bob, "NumberInputModal", 5);
        r.In(() =>
        {
            Assert.Equal(fed - 5, rwd.current_amount);
            var dispensed = Dm.each<obj_item_stack_cable_coil>(bob.held_items).Concat(Dm.each<obj_item_stack_cable_coil>(((turf)bob.loc).contents)).ToList();
            Assert.Contains(dispensed, c => Dm.num(c.amount) == 5);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void FloppyDisksTakeAStickerFromTheRadialAndStackUp()
    {
        var (r, feed, bob) = Room(9212);
        var disk = Hold<obj_item_disk>(r, bob);
        r.Command(() => Waiting.Detach(disk.click_alt_secondary(bob)));
        PickRadial(r, bob, "Seven");
        r.In(() => Assert.Equal("o_seven", disk.sticker_icon_state));
        Assert.Contains("o_seven", Overlays(r, disk));
        Assert.True(feed.Any("You change the sticker"));

        var other = r.In(() => Dm.@new<obj_item_disk>(locate(5, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(other.item_interaction(bob, disk)));
        var stack = r.In(() => Dm.locate_type(typeof(obj_item_disk_stack), locate(5, 4, 1)) as obj_item_disk_stack);
        Assert.NotNull(stack);
        r.In(() =>
        {
            Assert.Equal(2, Dm.length(stack.stacked_disks));
            Assert.Same(stack, disk.loc);
            Assert.Same(stack, other.loc);
        });
        Assert.Contains("There are", Examine(r, stack, bob));
        Assert.Equal(2, Overlays(r, stack).Count(o => o != null && o.StartsWith("datadisk", StringComparison.Ordinal)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePenlightsHolosignAsksAPatientToHalt()
    {
        var (r, feed, bob) = Room(9213);
        var pen = Hold<obj_item_flashlight_pen>(r, bob);
        mob_living_carbon_human al = r.Join(9, 4, "Al");
        r.Step(Second);
        Use(r, bob, al);
        var sign = r.In(() => Dm.locate_type(typeof(obj_effect_temp_visual_medical_holosign), al.loc));
        Assert.NotNull(sign);
        Assert.Contains(feed.Chat, c => c.Target == al && c.Html.Contains("is offering medical assistance", StringComparison.Ordinal));
        Assert.True(feed.Any("created a medical hologram"));
        Assert.True(Heard(r, "ping"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBlackMarketAndThemeDisksHoldRandomPrograms()
    {
        var (r, _, _) = Room(9214);
        var market = Place<obj_item_disk_computer_black_market>(r, 6, 4);
        var theme = Place<obj_item_disk_computer_maintenance_theme>(r, 7, 4);
        r.In(() =>
        {
            float programs = Dm.length(market.stored_files);
            Assert.InRange(programs, 2f, 4f);
            Assert.StartsWith("datadisk", market.icon_state);
            Assert.DoesNotContain(Dm.each(market.stored_files), f => f.GetType() == typeof(datum_computer_file_program_maintenance_theme));
            Assert.Equal(1, Dm.length(theme.stored_files));
            Assert.IsAssignableFrom<datum_computer_file_program_maintenance_theme>(Dm.each(theme.stored_files).Single());
        });
        Assert.Contains("o_code", Overlays(r, market));
        r.AssertNoRuntimes();
    }
}
