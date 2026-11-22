using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M5;

/// <summary>
/// M5 slice 10: the construction rows left labelled M5, by a joined player's clicks in a booted toy room: the gravity generator
/// broken and repaired through tg's four stages (gravitygenerator.dm:200-279), a firelock unbolted into its frame and rebuilt
/// (firedoor.dm:502-527, :701, the frame :808-975), a plain box flattened (flat_boxes.dm:12), a cable's damage
/// (cable.dm:156-170), the pipe scrubber taken apart (pipe_scrubber.dm:34-46), the blast door's construction steps
/// (poddoor.dm:91-212), a controller's ID set with a multitool (doorcontrol.dm:42) and the flamethrower built from a welder,
/// rods, an igniter and a plasma tank and fired (weldingtool.dm:119-330, flamethrower.dm).
/// </summary>
public class Slice10Tests
{
    private const int Second = 20;

    private static (AudioRoom r, mob_living_carbon_human bob) Room(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, bob);
    }

    private static T Spawn<T>(AudioRoom r, int x, int y, float? amount = null) where T : atom
        => r.In(() => (T)(amount is float a ? Dm.@new(typeof(T), locate(x, y, 1), a, FALSE) : Dm.@new(typeof(T), locate(x, y, 1))));

    private static List<T> On<T>(AudioRoom r, int x, int y) => r.In(() => Dm.each_cast<atom>(locate(x, y, 1).contents).OfType<T>().ToList());

    private static void Give(AudioRoom r, mob_living_carbon_human who, obj_item item) => r.In(() =>
    {
        foreach (object held in Dm.each(who.held_items.Copy()))
        {
            if (held is obj_item old && old != item)
                qdel(old);
        }
        Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(item, merge_stacks: FALSE))), "the item reaches a hand");
    });

    private static int Heard(AudioRoom r, string file) => r.Sounds.Heard.Count(h => h.File == file);

    // ---- the gravity generator (gravitygenerator.dm:200-279)

    [Fact]
    public void TheBrokenGravityGeneratorIsRepairedThroughTgsFourStages()
    {
        var (r, bob) = Room(10001);
        // the main part at (6,4): its 3x3 block is x 5..7, y 4..6 (setup_parts, CORNER_BLOCK_OFFSET(turf, 3, 3, -1, 0))
        var main = Spawn<obj_machinery_gravity_generator_main>(r, 6, 4);
        r.Step(2);
        var part = Assert.Single(On<obj_machinery_gravity_generator_part>(r, 5, 4));
        // Bob stands below the main part (tg's part forwards only item_interaction; the tool acts are the main part's)
        r.In(() => Waiting.NoSleep(((atom_movable)bob).forceMove(locate(6, 3, 1))));
        Assert.True(r.In(() => Dm.truthy(main.on)));

        r.In(() => Waiting.NoSleep(main.set_broken()));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(main.machine_stat, BROKEN))));
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(part.machine_stat, BROKEN))), "set_broken breaks every part");
        Assert.False(r.In(() => Dm.truthy(main.on)), "set_broken disables the generator");
        Assert.Equal(0f, r.In(() => Dm.num(main.charge_count)));
        Assert.False(r.In(() => Dm.truthy(main.breaker)));
        Assert.Equal("fix0_8", r.In(() => (string)main.icon_state));
        Assert.Equal("fix0_9", r.In(() => (string)part.icon_state));

        // a wrong tool at a stage does nothing: the wrench at stage 0
        WallTests.Hold<obj_item_wrench>(r, bob);
        WallTests.Click(r, bob, part, 1);
        Assert.Equal(0f, r.In(() => main.broken_state));

        // stage 0: the screwdriver (through the part: item_interaction and the tool acts reach the main part)
        WallTests.Hold<obj_item_screwdriver>(r, bob);
        WallTests.Click(r, bob, main, 1);
        Assert.Equal(1f, r.In(() => main.broken_state));
        Assert.Equal("fix1_8", r.In(() => (string)main.icon_state));
        // stage 1: the welder
        WallTests.Hold<obj_item_weldingtool>(r, bob);
        WallTests.Click(r, bob, main, 1);
        Assert.Equal(2f, r.In(() => main.broken_state));
        // stage 2: 10 plasteel; 9 are not enough
        WallTests.Hold<obj_item_stack_sheet_plasteel>(r, bob, 9);
        WallTests.Click(r, bob, part, 1);
        Assert.Equal(2f, r.In(() => main.broken_state));
        var plasteel = WallTests.Hold<obj_item_stack_sheet_plasteel>(r, bob, 12);
        int clicks = Heard(r, "sound/machines/click.ogg");
        WallTests.Click(r, bob, part, 1);
        Assert.Equal(3f, r.In(() => main.broken_state));
        Assert.Equal(2f, r.In(() => Dm.num(plasteel.get_amount())));
        Assert.True(Heard(r, "sound/machines/click.ogg") > clicks, "the plating's click");
        Assert.Equal("fix3_8", r.In(() => (string)main.icon_state));
        // stage 3: the wrench fixes it
        WallTests.Hold<obj_item_wrench>(r, bob);
        WallTests.Click(r, bob, main, 1);
        Assert.False(r.In(() => Dm.truthy(Dm.bitand(main.machine_stat, BROKEN))));
        Assert.False(r.In(() => Dm.truthy(Dm.bitand(part.machine_stat, BROKEN))), "set_fix fixes every part");
        Assert.Equal(0f, r.In(() => main.broken_state));
        // the breaker stays off (set_broken turned it off; the panel is M9): the generator discharges, its picture "off" once
        // SSmachines' next process finds the empty charge idle (get_status shows "on" while charging_state is POWER_DOWN; the
        // subsystems' phase decides whether that process ran by now: M6 slice 1's SSblood_drying moved it)
        for (int i = 0; i < 3 * 20 && r.In(() => (string)main.icon_state) != "off_8"; i++)
            r.Step(1);
        Assert.Equal("off_8", r.In(() => (string)main.icon_state));
    }

    // ---- the firelock and its frame (firedoor.dm:502-527, :701, :808-975)

    [Fact]
    public void AFirelockIsUnboltedIntoItsFrameAndBuiltAgain()
    {
        var (r, bob) = Room(10002);
        var door = Spawn<obj_machinery_door_firedoor_closed>(r, 5, 4);
        r.Step(4);
        // an unwelded firelock refuses the wrench and the screwdriver (FALSE: the tool's attack goes on)
        WallTests.Hold<obj_item_screwdriver>(r, bob);
        WallTests.Click(r, bob, door, 1);
        Assert.True(r.In(() => Dm.truthy(door.boltslocked)));

        r.In(() => door.welded = TRUE);
        // the bolts are locked: the wrench only tells so
        WallTests.Hold<obj_item_wrench>(r, bob);
        WallTests.Click(r, bob, door, 3);
        Assert.False(r.In(() => QDELETED(door)));
        // the screwdriver unlocks them
        WallTests.Hold<obj_item_screwdriver>(r, bob);
        WallTests.Click(r, bob, door, 1);
        Assert.False(r.In(() => Dm.truthy(door.boltslocked)));
        // the wrench: DEFAULT_STEP_TIME (2 s), deconstruct.ogg, deconstruct(TRUE): the frame with its panel open
        WallTests.Hold<obj_item_wrench>(r, bob);
        int decon = Heard(r, "sound/items/deconstruct.ogg");
        WallTests.Click(r, bob, door, 3);
        Assert.True(r.In(() => QDELETED(door)));
        Assert.True(Heard(r, "sound/items/deconstruct.ogg") > decon);
        var frame = Assert.Single(On<obj_structure_firelock_frame>(r, 5, 4));
        Assert.Equal(2f, r.In(() => frame.constructionStep));
        Assert.Equal("frame2", r.In(() => (string)frame.icon_state));

        // the crowbar takes the circuit out: the firelock electronics drop, the frame's step 1
        WallTests.Hold<obj_item_crowbar>(r, bob);
        WallTests.Click(r, bob, frame, 3);
        Assert.Equal(1f, r.In(() => frame.constructionStep));
        Assert.Equal("frame1", r.In(() => (string)frame.icon_state));
        var electronics = r.In(() => Dm.each_cast<atom>(locate(5, 4, 1).contents).Concat(Dm.each_cast<atom>(locate(4, 4, 1).contents)).OfType<obj_item_electronics_firelock>().Single());

        // the electronics back in (2 s), then 2 plasteel reinforce it, then the wrench makes a heavy firelock
        Give(r, bob, electronics);
        WallTests.Click(r, bob, frame, 3);
        Assert.Equal(2f, r.In(() => frame.constructionStep));
        Assert.True(r.In(() => QDELETED(electronics)));
        var plasteel = WallTests.Hold<obj_item_stack_sheet_plasteel>(r, bob, 5);
        WallTests.Click(r, bob, frame, 3);
        Assert.Equal(1f, r.In(() => frame.reinforced));
        Assert.Equal(3f, r.In(() => Dm.num(plasteel.get_amount())));
        WallTests.Hold<obj_item_wrench>(r, bob);
        WallTests.Click(r, bob, frame, 3);
        Assert.True(r.In(() => QDELETED(frame)));
        var heavy = Assert.Single(On<obj_machinery_door_firedoor>(r, 5, 4));
        Assert.IsType<obj_machinery_door_firedoor_heavy>(heavy);
    }

    [Fact]
    public void ALooseFirelockFrameIsCutIntoIron()
    {
        var (r, bob) = Room(10003);
        var frame = Spawn<obj_structure_firelock_frame>(r, 5, 4);
        r.Step(2);
        Assert.Equal(1f, r.In(() => frame.constructionStep));
        WallTests.Hold<obj_item_weldingtool>(r, bob);
        WallTests.Click(r, bob, frame, 3);
        Assert.True(r.In(() => QDELETED(frame)));
        var iron = Assert.Single(On<obj_item_stack_sheet_iron>(r, 5, 4));
        Assert.Equal(3f, r.In(() => Dm.num(iron.amount)));
    }

    // ---- the box (flat_boxes.dm:12)

    [Fact]
    public void AnEmptyPlainBoxFlattensIntoAFlatBox()
    {
        var (r, _) = Room(10004);
        var box = Spawn<obj_item_storage_box>(r, 5, 4);
        var full = Spawn<obj_item_storage_box>(r, 6, 4);
        var survival = Spawn<obj_item_storage_box_survival>(r, 7, 4);
        r.In(() => Dm.@new<obj_item_screwdriver>(full));
        r.In(() => { box.pixel_x = 3; box.pixel_y = -2; });
        r.Step(2);
        r.In(() => { box.flatten_box(); full.flatten_box(); survival.flatten_box(); });
        r.Step(2);
        Assert.True(r.In(() => QDELETED(box)));
        var flat = Assert.Single(On<obj_item_storage_box_flat>(r, 5, 4));
        Assert.Equal((3f, -2f), r.In(() => (Dm.num(flat.pixel_x), Dm.num(flat.pixel_y))));
        Assert.Equal(1, Heard(r, "sound/items/handling/materials/cardboard_drop.ogg"));
        Assert.False(r.In(() => QDELETED(full)), "a box with contents stays");
        Assert.False(r.In(() => QDELETED(survival)), "only the plain box type flattens");
        // the flat box is live: its undertile element (TRAIT_T_RAY_VISIBLE, use_anchor, tilt_tile)
        Assert.True(r.In(() => Dm.truthy(flat._listen_lookup?[COMSIG_OBJ_HIDE])), "the undertile element listens for floor tiles");
    }

    // ---- the cable (cable.dm:156-170, :783)

    [Fact]
    public void ACableDeconstructsIntoAPieceOfCoilOfItsColour()
    {
        var (r, _) = Room(10005);
        var cable = Spawn<obj_structure_cable>(r, 5, 4);
        r.In(() => cable.cable_color = CABLE_COLOR_GREEN);
        r.Step(2);
        r.In(() => Waiting.NoSleep(cable.deconstruct()));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(cable)));
        var coil = Assert.Single(On<obj_item_stack_cable_coil>(r, 5, 4));
        Assert.Equal(1f, r.In(() => Dm.num(coil.amount)));
        Assert.Equal(CABLE_COLOR_GREEN, r.In(() => coil.cable_color));
        // run_atom_armor: a bomb's damage to a cable under the floor is a quarter
        var hidden = Spawn<obj_structure_cable>(r, 6, 4);
        r.In(() => ADD_TRAIT(hidden, TRAIT_UNDERFLOOR, "test"));
        float bombed = r.In(() => Dm.num(Waiting.NoSleep(hidden.run_atom_armor(40, BRUTE, BOMB))));
        float hit = r.In(() => Dm.num(Waiting.NoSleep(hidden.run_atom_armor(40, BRUTE, MELEE))));
        Assert.Equal(hit / 4, bombed, 3);
        // a multilayer hub removes the cable on its layers in its turf
        var under = Spawn<obj_structure_cable>(r, 7, 4);
        Spawn<obj_structure_cable_multilayer>(r, 7, 4);
        r.Step(2);
        Assert.True(r.In(() => QDELETED(under)));
        Assert.Single(On<obj_item_stack_cable_coil>(r, 7, 4));
    }

    // ---- the pipe scrubber (pipe_scrubber.dm:34-46)

    [Fact]
    public void ThePipeScrubbersInternalTankGoingTakesTheScrubberApart()
    {
        var (r, _) = Room(10006);
        var scrubber = Spawn<obj_machinery_portable_atmospherics_pipe_scrubber>(r, 5, 4);
        r.Step(2);
        var tank = r.In(() => scrubber.internal_tank);
        float before = r.In(() => Dm.num(((datum_gas_mixture)Waiting.NoSleep(locate(5, 4, 1).return_air())).total_moles()));
        float inside = r.In(() => Dm.num(scrubber.air_contents.total_moles()) + Dm.num(tank.air_contents.total_moles()));
        r.In(() => qdel(tank));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(scrubber)), "COMSIG_QDELETING of the canister deconstructs the scrubber");
        float after = r.In(() => Dm.num(((datum_gas_mixture)Waiting.NoSleep(locate(5, 4, 1).return_air())).total_moles()));
        Assert.True(after >= before + inside - 0.01f, $"the scrubber's air reaches the turf ({before} + {inside} -> {after})");
    }

    // ---- the blast door (poddoor.dm:91-212) and its controller (doorcontrol.dm:42)

    [Fact]
    public void ACraftedBlastDoorFrameIsWiredFittedAndTakenApart()
    {
        var (r, bob) = Room(10007);
        var door = Spawn<obj_machinery_door_poddoor_preopen>(r, 5, 4);
        r.Step(4);
        r.In(() => { door.deconstruction = BLASTDOOR_NEEDS_WIRES; door.panel_open = TRUE; });
        // on_craft_completion: the crafter owns the door
        r.In(() => Waiting.NoSleep(door.on_craft_completion(Dm.list(), null, bob)));
        Assert.Same(bob, r.In(() => door.owner?.resolve()));

        // 15 cables (the recipe's reqs), 5 s
        var coil = WallTests.Hold<obj_item_stack_cable_coil>(r, bob, 20);
        WallTests.Click(r, bob, door, 6);
        Assert.Equal((float)BLASTDOOR_NEEDS_ELECTRONICS, r.In(() => Dm.num(door.deconstruction)));
        Assert.Equal(5f, r.In(() => Dm.num(coil.get_amount())));
        // the airlock electronics, 10 s
        var electronics = Spawn<obj_item_electronics_airlock>(r, 4, 4);
        Give(r, bob, electronics);
        WallTests.Click(r, bob, door, 11);
        Assert.Equal((float)BLASTDOOR_FINISHED, r.In(() => Dm.num(door.deconstruction)));
        Assert.True(r.In(() => QDELETED(electronics)));

        // back: the crowbar takes the electronics (10 s), the wirecutters the cables (10 s, 15 coil), the welder the frame (15 s)
        WallTests.Hold<obj_item_crowbar>(r, bob);
        WallTests.Click(r, bob, door, 11);
        Assert.Equal((float)BLASTDOOR_NEEDS_ELECTRONICS, r.In(() => Dm.num(door.deconstruction)));
        WallTests.Hold<obj_item_wirecutters>(r, bob);
        WallTests.Click(r, bob, door, 11);
        Assert.Equal((float)BLASTDOOR_NEEDS_WIRES, r.In(() => Dm.num(door.deconstruction)));
        Assert.Equal(15f, r.In(() => On<obj_item_stack_cable_coil>(r, 5, 4).Sum(c => Dm.num(c.amount))));
        WallTests.Hold<obj_item_weldingtool>(r, bob);
        WallTests.Click(r, bob, door, 16);
        Assert.True(r.In(() => QDELETED(door)));
        Assert.Equal(15f, r.In(() => On<obj_item_stack_sheet_plasteel>(r, 5, 4).Sum(c => Dm.num(c.amount))));
    }

    [Fact]
    public void AMultitoolSetsAControllersIdThroughTheNumberInput()
    {
        var (r, bob) = Room(10008);
        // the plain controller is not generically adjustable (doorcontrol.dm:14); the igniter controller is
        var plain = Spawn<obj_item_assembly_control>(r, 5, 5);
        var controller = Spawn<obj_item_assembly_control_igniter>(r, 5, 4);
        Assert.Equal(1f, r.In(() => Dm.num(controller.get_part_rating())));
        Assert.Null(r.In(() => Waiting.NoSleep(plain.multitool_act(bob, null))));
        WallTests.Hold<obj_item_multitool>(r, bob);
        bob.client.dm_tgui_output.Clear();
        WallTests.Click(r, bob, controller, 0.2f);
        string window = null;
        foreach (TguiOutput o in bob.client.dm_tgui_output)
        {
            if (o.Kind == TguiOutputKind.Message && o.Json.Contains("NumberInputModal"))
                window = o.WindowId;
        }
        Assert.NotNull(window);
        r.Command(() => ClientSeam.TguiMessage(bob.client, window, "act/submit", Dm.list(Dm.kv("entry", 42))));
        r.Step(4);
        Assert.Equal(42f, r.In(() => Dm.num(controller.id)));
    }

    [Fact]
    public void AMultitoolSetsABlastDoorControllersIdFromTheOwnersDoorsInAListWindow()
    {
        // doorcontrol.dm:72 (the M5 leftovers, 2026-10-16): UNIQUE and the doors the user built ("area(id)"); a door of another
        // builder is not listed
        var (r, bob) = Room(10009);
        var mine = Spawn<obj_machinery_door_poddoor_preopen>(r, 7, 4);
        var other = Spawn<obj_machinery_door_poddoor_preopen>(r, 7, 6);
        r.Step(4);
        r.In(() =>
        {
            mine.id = "7"; // one character: tg's copytext(change_id, start, end) drops the last character of a longer id
            mine.owner = (datum_weakref)WEAKREF(bob);
            other.id = "12";
        });
        var controller = Spawn<obj_item_assembly_control_blast_door>(r, 5, 4);
        string Ask()
        {
            bob.client.dm_tgui_output.Clear();
            WallTests.Click(r, bob, controller, 0.2f);
            string window = null;
            foreach (TguiOutput o in bob.client.dm_tgui_output)
            {
                if (o.Kind == TguiOutputKind.Message && o.Json.Contains("ListInputWindow"))
                    window = o.WindowId;
            }
            Assert.NotNull(window);
            return window;
        }

        WallTests.Hold<obj_item_multitool>(r, bob);
        string area = r.In(() => Dm.str(((area)get_area(mine)).name));
        string first = Ask();
        // the list's items are the window's static data, sent with its first update; a later update of SStgui's autoupdate
        // (tgui.dm:322, ui_interact's send_update) carries none, and whether SStgui fires within the click depends on its phase
        string items = bob.client.dm_tgui_output.Last(o => o.WindowId == first && o.Json.Contains("\"static_data\"", StringComparison.Ordinal)).Json;
        Assert.Contains("\"UNIQUE\"", items);
        Assert.Contains(area + "(7)", items);
        Assert.DoesNotContain("(12)", items);
        r.Command(() => ClientSeam.TguiMessage(bob.client, first, "act/submit", Dm.list(Dm.kv("entry", area + "(7)"))));
        r.Step(4);
        Assert.Equal("7", r.In(() => controller.id));
        r.Command(() => ClientSeam.TguiMessage(bob.client, Ask(), "act/submit", Dm.list(Dm.kv("entry", "UNIQUE"))));
        r.Step(4);
        Assert.Equal(-1f, r.In(() => Dm.num(controller.id)));
        r.AssertNoRuntimes();
    }

    // ---- the flamethrower (weldingtool.dm:119-330, flamethrower.dm, igniter.dm)

    [Fact]
    public void AFlamethrowerIsBuiltFromAWelderAndFiredAtTheFloor()
    {
        var (r, bob) = Room(10009);
        // the screwdriver on an unlit welder opens its tank (status FALSE), then rods start the flamethrower
        var welder = Spawn<obj_item_weldingtool>(r, 4, 4);
        Give(r, bob, welder);
        var screwdriver = Spawn<obj_item_screwdriver>(r, 4, 4);
        r.In(() => Waiting.NoSleep(welder.screwdriver_act(bob, screwdriver)));
        Assert.False(r.In(() => Dm.truthy(welder.status)));
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(((datum_reagents)welder.reagents).flags, OPENCONTAINER))));
        var rods = Spawn<obj_item_stack_rods>(r, 4, 4, 5);
        r.In(() => Waiting.NoSleep(welder.item_interaction(bob, rods)));
        r.Step(2);
        Assert.Equal(4f, r.In(() => Dm.num(rods.amount)));
        var flamethrower = r.In(() => Dm.each(bob.held_items).OfType<obj_item_flamethrower>().Single());
        Assert.Same(welder, r.In(() => flamethrower.weldtool));
        Assert.Same(flamethrower, r.In(() => welder.loc));

        // the igniter in, secured with the screwdriver, the plasma tank in
        var igniter = Spawn<obj_item_assembly_igniter>(r, 4, 4);
        r.In(() => igniter.secured = FALSE);
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Dm.num(Waiting.NoSleep(flamethrower.item_interaction(bob, igniter)))));
        Assert.Same(igniter, r.In(() => flamethrower.igniter));
        r.In(() => Waiting.NoSleep(flamethrower.screwdriver_act(bob, screwdriver)));
        Assert.True(r.In(() => Dm.truthy(flamethrower.status)));
        var tank = Spawn<obj_item_tank_internals_plasma_full>(r, 4, 4);
        r.In(() => Waiting.NoSleep(flamethrower.item_interaction(bob, tank)));
        Assert.Same(tank, r.In(() => flamethrower.ptank));
        r.In(() => flamethrower.update_appearance());
        var overlays = r.In(() => Dm.each(flamethrower.managed_overlays is DmList l ? l : Dm.list(flamethrower.managed_overlays)).Select(o => o switch { string t => t, mutable_appearance m => m.icon_state, DmAppearance d => d.icon_state, _ => o?.ToString() }).ToList());
        Assert.Contains("+igniter1", overlays);
        Assert.Contains("+ptank", overlays);

        // lit: welderactivate, processing, the light; fired at (7,4): plasma from the tank on the turfs of the line, a hotspot
        int activate = Heard(r, "sound/items/tools/welderactivate.ogg");
        r.In(() => Waiting.NoSleep(flamethrower.attack_self(bob)));
        Assert.True(r.In(() => Dm.truthy(flamethrower.lit)));
        Assert.True(Heard(r, "sound/items/tools/welderactivate.ogg") > activate);
        Assert.Equal("flamethrower_1", r.In(() => (string)flamethrower.inhand_icon_state));
        float tankPlasma = r.In(() => Dm.num(((datum_gas_mixture)Waiting.NoSleep(tank.return_air())).moles[typeof(datum_gas_plasma)]));
        r.Command(() => Waiting.Detach(Fire()));
        async DmTask Fire() => await flamethrower.ranged_interact_with_atom(locate(7, 4, 1), bob);
        r.Step(Second);
        float left = r.In(() => Dm.num(((datum_gas_mixture)Waiting.NoSleep(tank.return_air())).moles[typeof(datum_gas_plasma)]));
        Assert.True(left < tankPlasma * 0.95f * 0.95f + 0.001f, $"5% of the tank per turf ({tankPlasma} -> {left})");
        Assert.NotEmpty(On<obj_effect_hotspot>(r, 5, 4).Concat(On<obj_effect_hotspot>(r, 6, 4)).Concat(On<obj_effect_hotspot>(r, 7, 4)));

        // off, the igniter unsecured, then the wrench takes it apart: the welder, igniter, tank and a rod on the floor
        r.In(() => Waiting.NoSleep(flamethrower.attack_self(bob)));
        Assert.False(r.In(() => Dm.truthy(flamethrower.lit)));
        r.In(() => Waiting.NoSleep(flamethrower.screwdriver_act(bob, screwdriver)));
        var wrench = Spawn<obj_item_wrench>(r, 4, 4);
        r.In(() => Waiting.NoSleep(flamethrower.wrench_act(bob, wrench)));
        Assert.True(r.In(() => QDELETED(flamethrower)));
        // the parts drop where the flamethrower is, the holder's turf (flamethrower.dm wrench_act: drop_location); the burning
        // line next to Bob may push him a tile with the airflow, a roll of the seed's stream
        Assert.Same(r.In(() => bob.loc), r.In(() => welder.loc));
        Assert.Same(r.In(() => bob.loc), r.In(() => igniter.loc));
        Assert.Same(r.In(() => bob.loc), r.In(() => tank.loc));
    }

    [Fact]
    public void TheIndustrialAndEmergencyWeldersRefuseTheFlamethrowerScrewdriver()
    {
        var (r, bob) = Room(10010);
        var large = Spawn<obj_item_weldingtool_largetank>(r, 4, 4);
        var mini = Spawn<obj_item_weldingtool_mini>(r, 4, 4);
        var screwdriver = Spawn<obj_item_screwdriver>(r, 4, 4);
        r.In(() => { Waiting.NoSleep(large.screwdriver_act(bob, screwdriver)); Waiting.NoSleep(mini.screwdriver_act(bob, screwdriver)); });
        Assert.True(r.In(() => Dm.truthy(large.status) && Dm.truthy(mini.status)));
    }
}
