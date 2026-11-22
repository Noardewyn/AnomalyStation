using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tests.M7;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Engineering;

/// <summary>
/// Engineering package E10: tg's shield generators in a booted toy room. The anti-breach projector (shieldgen.dm:142-285) fills the
/// space tiles within four of it with emergency shields that hold the air, locks with an ID and is mended with cable; the emergency
/// shield takes damage with tg's ion spark and sound, an EMP breaks it, and the regenerating one heals five a second. The shield wall
/// generators (:290-543) on a fed cable raise a wall between two active generators, which blocks a mob and the singularity and drains
/// both generators when struck; they shut down without power. The atmospheric shield generators (atmos_field_gen.dm), paired on
/// one tile as Meta maps them, make one holofield that blocks the gas and lets a person through. The modular shield generator
/// (modular_shield.dm) charges, links to its console by multitool, projects its ring, is drained by blows and collapses, and its
/// modules add their parts' boosts by facing it.
/// </summary>
public class ShieldGeneratorTests
{
    private const int Second = 20;

    private static void MachinesTick(AudioRoom r, int runs = 1) => r.Step(runs * r.In(() => (int)Dm.num(SSmachines.wait)) * 2);

    private static void Click(AudioRoom r, mob who, atom target, float seconds = 1, bool right = false)
    {
        r.Command(() => ClientSeam.Click(who.client, target, right ? "icon-x=16;icon-y=16;right=1;button=right" : "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(Math.Max(2, (int)(seconds * Second)));
    }

    private static void EmptyHands(AudioRoom r, mob_living_carbon_human who) => r.In(() =>
    {
        foreach (object held in Dm.each(who.held_items.Copy()))
        {
            if (held is obj_item i)
                qdel(i);
        }
    });

    private static void MoveTo(AudioRoom r, mob who, int x, int y) => r.In(() => Waiting.NoSleep(who.forceMove(locate(x, y, 1))));

    private static List<T> On<T>(AudioRoom r, int x, int y) where T : atom => r.In(() => Dm.each<T>(locate(x, y, 1)).ToList());

    private static bool Shares(AudioRoom r, int x1, int y1, int x2, int y2) => r.In(() => Dm.@in(locate(x2, y2, 1), locate(x1, y1, 1).atmos_adjacent_turfs));

    // ---- the anti-breach shielding projector ----

    [Fact]
    public void TheProjectorShieldsTheSpaceTilesInItsRangeAndHoldsTheAir()
    {
        obj_machinery_shieldgen gen = null;
        var r = new AudioRoom(13, 10, seed: 1001, mapExtras: w =>
        {
            // a breach: two columns of space at three and four tiles east of the projector, and one beyond its range
            for (int y = 2; y <= 8; y++)
            {
                Dm.@new<turf_open_space>(locate(7, y, 1));
                Dm.@new<turf_open_space>(locate(8, y, 1));
                Dm.@new<turf_open_space>(locate(9, y, 1));
            }
            gen = Dm.@new<obj_machinery_shieldgen>(locate(4, 5, 1));
        });
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_GODMODE, "test")); // the room vents into the breach
        r.Step(Second);
        Assert.Equal("shieldoff", r.In(() => gen.icon_state));
        Assert.True(Shares(r, 6, 5, 7, 5), "the floor and the breach share their air before the shields");

        // a loose projector refuses (interact :206), the wrench secures it (wrench_act :228)
        Click(r, bob, gen);
        Assert.False(r.In(() => Dm.truthy(gen.active)));
        WallTests.Hold<obj_item_wrench>(r, bob);
        Click(r, bob, gen);
        Assert.True(r.In(() => Dm.truthy(gen.anchored)));
        EmptyHands(r, bob);

        // on: a shield on every space tile within four (RANGE_TURFS) and none beyond (shields_up :168)
        Click(r, bob, gen);
        Assert.True(r.In(() => Dm.truthy(gen.active)));
        Assert.Equal("shieldon", r.In(() => gen.icon_state));
        Assert.Equal(INFINITY, r.In(() => gen.move_resist));
        int expected = 0;
        for (int x = 7; x <= 9; x++)
        {
            for (int y = 2; y <= 8; y++)
            {
                bool inRange = Math.Abs(x - 4) <= 4 && Math.Abs(y - 5) <= 4;
                Assert.Equal(inRange ? 1 : 0, On<obj_structure_emergency_shield>(r, x, y).Count);
                expected += inRange ? 1 : 0;
            }
        }
        Assert.Equal(expected, r.In(() => (int)length(gen.deployed_shields)));
        Assert.Empty(On<obj_structure_emergency_shield>(r, 5, 5)); // only space tiles
        // the shield is dense with ATMOS_PASS_DENSITY: the floor no longer shares its air with the shielded tile
        Assert.False(Shares(r, 6, 5, 7, 5), "the shield holds the air");
        obj_structure_emergency_shield sample = On<obj_structure_emergency_shield>(r, 7, 5)[0];
        Assert.True(r.In(() => Dm.@in(sample.dir, GLOB.cardinals)), "a random cardinal dir (Initialize :17)");

        // an engineering ID locks the controls (item_interaction :262): a hand cannot switch it off, a wrench cannot loosen it
        WallTests.Hold<obj_item_card_id_advanced_debug>(r, bob);
        Click(r, bob, gen);
        Assert.Equal(1f, r.In(() => gen.locked));
        EmptyHands(r, bob);
        Click(r, bob, gen);
        Assert.True(r.In(() => Dm.truthy(gen.active)), "locked");
        WallTests.Hold<obj_item_wrench>(r, bob);
        Click(r, bob, gen);
        Assert.True(r.In(() => Dm.truthy(gen.anchored)), "the bolts are covered");
        WallTests.Hold<obj_item_card_id_advanced_debug>(r, bob);
        Click(r, bob, gen);
        Assert.Equal(0f, r.In(() => gen.locked));

        // off: the shields go and the air flows again (shields_down :178)
        EmptyHands(r, bob);
        Click(r, bob, gen);
        Assert.False(r.In(() => Dm.truthy(gen.active)));
        Assert.Equal(0, r.In(() => (int)length(gen.deployed_shields)));
        Assert.Empty(On<obj_structure_emergency_shield>(r, 7, 5));
        Assert.True(Shares(r, 6, 5, 7, 5));
        Assert.Equal("shieldoff", r.In(() => gen.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABrokenProjectorIsMendedWithCableBehindItsOpenPanel()
    {
        obj_machinery_shieldgen gen = null;
        var r = new AudioRoom(11, 9, seed: 1002, mapExtras: w => gen = Dm.@new<obj_machinery_shieldgen>(locate(4, 5, 1)));
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        r.In(() =>
        {
            Waiting.NoSleep(gen.set_machine_stat(Dm.bitor(gen.machine_stat, BROKEN)));
            gen.atom_integrity = 10;
            gen.update_appearance();
        });
        Assert.Equal("shieldoffbr", r.In(() => gen.icon_state));
        WallTests.Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, gen);
        Assert.True(r.In(() => Dm.truthy(gen.panel_open)));
        obj_item_stack_cable_coil coil = WallTests.Hold<obj_item_stack_cable_coil>(r, bob, 5);
        Click(r, bob, gen, 4); // three seconds of do_after (:251)
        Assert.False(r.In(() => Dm.truthy(Dm.bitand(gen.machine_stat, BROKEN))));
        Assert.Equal(r.In(() => gen.max_integrity), r.In(() => gen.atom_integrity));
        Assert.Equal(4f, r.In(() => Dm.num(coil.get_amount())));
        Assert.Equal("shieldoff", r.In(() => gen.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEmergencyShieldSparksAndSoundsWhenHitAndAnEmpBreaksIt()
    {
        var r = new AudioRoom(11, 9, seed: 1003);
        r.Join(3, 3, "Bob");
        r.Step(Second);
        obj_structure_emergency_shield shield = r.In(() => Dm.@new<obj_structure_emergency_shield>(locate(5, 5, 1)));
        r.In(() => Waiting.NoSleep(shield.take_damage(30, BRUTE, MELEE)));
        Assert.Equal(170f, r.In(() => Dm.num(shield.atom_integrity)));
        Assert.Single(On<obj_effect_temp_visual_impact_effect_ion>(r, 5, 5)); // :49
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/empulse.ogg"); // play_attack_sound :39
        // a light EMP takes fifty, a heavy one deletes it (emp_act :29)
        r.In(() => Waiting.NoSleep(shield.emp_act(EMP_LIGHT)));
        Assert.Equal(120f, r.In(() => Dm.num(shield.atom_integrity)));
        r.In(() => Waiting.NoSleep(shield.emp_act(EMP_HEAVY)));
        Assert.True(r.In(() => QDELETED(shield)));

        // the regenerating shield ignores EMPs and heals five a second until it is whole (:59-77)
        obj_structure_emergency_shield_regenerating regen = r.In(() => Dm.@new<obj_structure_emergency_shield_regenerating>(locate(6, 5, 1)));
        r.In(() => Waiting.NoSleep(regen.emp_act(EMP_HEAVY)));
        Assert.False(r.In(() => QDELETED(regen)));
        r.In(() => Waiting.NoSleep(regen.take_damage(100, BRUTE, MELEE)));
        Assert.Equal(300f, r.In(() => Dm.num(regen.atom_integrity)));
        r.Step(10 * Second);
        float healed = r.In(() => Dm.num(regen.atom_integrity));
        Assert.InRange(healed, 335f, 360f);
        r.Step(20 * Second);
        Assert.Equal(400f, r.In(() => Dm.num(regen.atom_integrity)));
        Assert.False(r.In(() => Dm.@in(regen, SSobj.processing)), "a whole shield stops processing");
        r.AssertNoRuntimes();
    }

    // ---- the shield wall generators ----

    private const int LineY = 3, GenA = 3, GenB = 8, SourceX = 11;

    private static int Walls(AudioRoom r) => Enumerable.Range(2, 10).Sum(x => On<obj_machinery_shieldwall>(r, x, LineY).Count);

    private sealed record WallRig(AudioRoom R, mob_living_carbon_human Bob, obj_machinery_power_test_m7apc_source Source, obj_machinery_power_shieldwallgen A, obj_machinery_power_shieldwallgen B);

    private static WallRig WallRoom(ulong seed)
    {
        obj_machinery_power_test_m7apc_source source = null;
        obj_machinery_power_shieldwallgen a = null, b = null;
        var r = new AudioRoom(13, 8, seed: seed, mapExtras: w =>
        {
            for (int x = 2; x <= SourceX; x++)
            {
                Dm.@new<turf_open_floor_plating>(locate(x, LineY, 1));
                Dm.@new<obj_structure_cable>(locate(x, LineY, 1));
            }
            source = Dm.@new<obj_machinery_power_test_m7apc_source>(locate(SourceX, LineY, 1));
            a = Dm.@new<obj_machinery_power_shieldwallgen>(locate(GenA, LineY, 1)); // Meta's: loose and locked
            b = Dm.@new<obj_machinery_power_shieldwallgen_unlocked_anchored>(locate(GenB, LineY, 1));
        });
        r.In(() => source.output_energy = 2_000_000);
        mob_living_carbon_human bob = r.Join(GenA, LineY + 1, "Bob");
        r.Step(Second);
        MachinesTick(r, 2);
        return new WallRig(r, bob, source, a, b);
    }

    [Fact]
    public void TwoActiveWallGeneratorsOnAFedCableRaiseAWallThatBlocksAMobAndDrainsThem()
    {
        WallRig g = WallRoom(1004);
        AudioRoom r = g.R;
        Assert.True(r.In(() => Dm.@in(g.A, SSmachines.processing_early)), "the generator draws early (Initialize :334)");
        Assert.Null(r.In(() => g.A.powernet));
        Assert.NotNull(r.In(() => g.B.powernet));

        // loose: "not secured!"; the wrench anchors it on the cable and joins the network (wrench_act :450)
        Click(r, g.Bob, g.A);
        Assert.False(r.In(() => Dm.truthy(g.A.active)));
        WallTests.Hold<obj_item_wrench>(r, g.Bob);
        Click(r, g.Bob, g.A);
        Assert.True(r.In(() => Dm.truthy(g.A.anchored)));
        Assert.Same(r.In(() => g.B.powernet), r.In(() => g.A.powernet));
        // locked by default: a hand refuses until a card with the teleporter access unlocks it (item_interaction :470)
        EmptyHands(r, g.Bob);
        Click(r, g.Bob, g.A);
        Assert.False(r.In(() => Dm.truthy(g.A.active)), "locked");
        r.In(() => Assert.True(Dm.@in(ACCESS_TELEPORTER, g.A.req_access)));
        WallTests.Hold<obj_item_card_id>(r, g.Bob);
        Click(r, g.Bob, g.A);
        Assert.Equal(1f, r.In(() => g.A.locked));
        WallTests.Hold<obj_item_card_id_advanced_debug>(r, g.Bob);
        Click(r, g.Bob, g.A);
        Assert.Equal(0f, r.In(() => g.A.locked));

        // A on alone: it waits for a partner (ACTIVE_SETUPFIELDS) and draws its 150 from the grid
        EmptyHands(r, g.Bob);
        Click(r, g.Bob, g.A);
        Assert.Equal(1f, r.In(() => Dm.num(g.A.active)));
        datum_powernet net = r.In(() => (datum_powernet)g.A.powernet);
        MachinesTick(r);
        Assert.Equal(1f, r.In(() => Dm.num(g.A.active)));
        Assert.Equal(0, Walls(r));
        Assert.Equal(SINGULARITY_TRY_MOVE_BLOCK, r.In(() => Dm.num(g.A._SendSignal(COMSIG_ATOM_SINGULARITY_TRY_MOVE, Dm.list(g.A)))));
        Assert.Equal(FAILED_UNFASTEN, r.In(() => Dm.num(g.A.can_be_unfasten_wrench(g.Bob, TRUE))));

        // B on too: the wall stands on the four tiles between them, belonging to both (setup_field :391)
        MoveTo(r, g.Bob, GenB, LineY + 1);
        Click(r, g.Bob, g.B);
        MachinesTick(r);
        Assert.Equal((2f, 2f), r.In(() => (Dm.num(g.A.active), Dm.num(g.B.active))));
        for (int x = GenA + 1; x < GenB; x++)
        {
            obj_machinery_shieldwall wall = Assert.Single(On<obj_machinery_shieldwall>(r, x, LineY));
            Assert.True(r.In(() => (wall.gen_primary == g.A || wall.gen_primary == g.B) && (wall.gen_secondary == g.A || wall.gen_secondary == g.B) && wall.gen_primary != wall.gen_secondary));
            Assert.True(r.In(() => Dm.truthy(wall.needs_power) && Dm.truthy(wall.density)));
            Assert.Equal(SINGULARITY_TRY_MOVE_BLOCK, r.In(() => Dm.num(wall._SendSignal(COMSIG_ATOM_SINGULARITY_TRY_MOVE, Dm.list(wall)))));
        }
        Assert.Equal(("shield_wall_gen_on", "shield_wall_gen_on"), r.In(() => (g.A.icon_state, g.B.icon_state)));
        Assert.True(r.In(() => Dm.truthy(g.A.light_on)));

        // a mob cannot walk into the wall
        mob_living_carbon_human dummy = r.Dummy(5, LineY + 1);
        r.In(() => Waiting.NoSleep(dummy.Move(locate(5, LineY, 1), SOUTH)));
        Assert.Same(r.In(() => locate(5, LineY + 1, 1)), r.In(() => dummy.loc));

        // a blow on the wall drains half of it from each generator (take_damage :599, drain_power :605) with tg's sound
        r.In(() =>
        {
            obj_machinery_shieldwall wall = Dm.locate<obj_machinery_shieldwall>(locate(5, LineY, 1));
            float before = Dm.num(net.load);
            Waiting.NoSleep(wall.take_damage(40, BRUTE, MELEE));
            Assert.Equal(before + 40, Dm.num(net.load));
            Assert.False(QDELETED(wall), "the wall is indestructible");
        });
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/empulse.ogg");

        // A off: its walls go (deactivate :537)
        MoveTo(r, g.Bob, GenA, LineY + 1);
        Click(r, g.Bob, g.A);
        Assert.False(r.In(() => Dm.truthy(g.A.active)));
        Assert.Equal(0, Walls(r));
        Assert.Equal("shield_wall_gen", r.In(() => g.A.icon_state));

        // no power: B shuts down at its next draw (process_early :380)
        r.In(() => g.Source.output_energy = 0);
        MachinesTick(r, 3);
        Assert.False(r.In(() => Dm.truthy(g.B.active)));
        Assert.Equal("shield_wall_gen", r.In(() => g.B.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheXenobiologyGeneratorTakesTheXenobiologyAccessAndAnOpenPanelShowsItsWires()
    {
        obj_machinery_power_shieldwallgen_xenobiologyaccess gen = null;
        var r = new AudioRoom(11, 8, seed: 1005, mapExtras: w => gen = Dm.@new<obj_machinery_power_shieldwallgen_xenobiologyaccess>(locate(4, 4, 1)));
        mob_living_carbon_human bob = r.Join(4, 3, "Bob");
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(Dm.@in(ACCESS_XENOBIOLOGY, gen.req_access));
            Assert.False(Dm.@in(ACCESS_TELEPORTER, gen.req_access));
        });
        WallTests.Hold<obj_item_card_id>(r, bob);
        Click(r, bob, gen);
        Assert.Equal(1f, r.In(() => gen.locked));
        // a locked generator's panel stays shut (screwdriver_act :457); unlocked, the screwdriver opens it and the wires show
        WallTests.Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, gen);
        Assert.False(r.In(() => Dm.truthy(gen.panel_open)));
        WallTests.Hold<obj_item_card_id_advanced_debug>(r, bob);
        Click(r, bob, gen);
        Assert.Equal(0f, r.In(() => gen.locked));
        WallTests.Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, gen);
        Assert.True(r.In(() => Dm.truthy(gen.panel_open)));
        Assert.Contains("shieldgen_wires", r.In(() => Dm.each(gen.overlays).Select(o => o switch
        {
            mutable_appearance m => m.icon_state as string,
            DmAppearance d => d.icon_state as string,
            string s => s,
            _ => null,
        }).ToList()));
        r.AssertNoRuntimes();
    }

    // ---- the atmospheric shield generators ----

    [Fact]
    public void APairOfAtmosphericGeneratorsOnOneTileMakesAHolofieldThatBlocksGasAndLetsAPersonThrough()
    {
        obj_machinery_atmos_shield_gen east = null, west = null;
        var r = new AudioRoom(11, 9, seed: 1006, mapExtras: w =>
        {
            // as Meta maps them in a mass driver's doorway: two locked generators on one tile, facing each other
            east = Dm.@new<obj_machinery_atmos_shield_gen_active>(locate(5, 5, 1));
            east.dir = EAST;
            west = Dm.@new<obj_machinery_atmos_shield_gen_active>(locate(5, 5, 1));
            west.dir = WEST;
        });
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        r.Step(Second);
        MachinesTick(r);

        // one is the master, the other its slave; the master's field covers the line between them: the one tile
        obj_machinery_atmos_shield_gen master = r.In(() => Dm.truthy(length(east.fields)) ? east : west);
        obj_machinery_atmos_shield_gen slave = master == east ? west : east;
        Assert.Equal((2f, 2f), r.In(() => (master.on, slave.on)));
        Assert.Same(master, r.In(() => slave.master));
        obj_effect_atmos_shield field = Assert.Single(On<obj_effect_atmos_shield>(r, 5, 5));
        Assert.Same(master, r.In(() => field.owner));
        Assert.Equal(1, r.In(() => (int)length(master.fields)));
        Assert.Equal(3f, r.In(() => Dm.num(master.max_range))); // a tier-1 micro laser + 2 (RefreshParts :97)
        Assert.Contains("active", r.In(() => Dm.each(master.overlays).Select(o => o is DmAppearance d ? d.icon_state as string : o as string).ToList()));
        // the holofield blocks gas (ATMOS_PASS_NO) and is not dense
        Assert.Equal(0, r.In(() => (int)LAZYLEN(locate(5, 5, 1).atmos_adjacent_turfs)));
        Assert.False(r.In(() => Dm.truthy(field.density)));
        Assert.True(r.In(() => HAS_TRAIT(locate(5, 5, 1), TRAIT_FIREDOOR_STOP)));

        // a person walks through it
        mob_living_carbon_human dummy = r.Dummy(5, 4);
        r.In(() => Waiting.NoSleep(dummy.Move(locate(5, 5, 1), NORTH)));
        Assert.Same(r.In(() => locate(5, 5, 1)), r.In(() => dummy.loc));
        r.In(() => Waiting.NoSleep(dummy.forceMove(locate(7, 7, 1))));

        // locked: a hand does nothing (attack_hand :136); the card unlocks it (item_interaction :160) and the hand turns it off
        Click(r, bob, master);
        Assert.Equal(2f, r.In(() => master.on));
        WallTests.Hold<obj_item_card_id_advanced_debug>(r, bob);
        Click(r, bob, master);
        Assert.Equal(0f, r.In(() => master.locked));
        EmptyHands(r, bob);
        Click(r, bob, master);
        Assert.Equal(0f, r.In(() => master.on));
        Assert.Empty(On<obj_effect_atmos_shield>(r, 5, 5));
        MachinesTick(r);
        Assert.Equal(0f, r.In(() => slave.on));
        Assert.Null(r.In(() => slave.master));
        Assert.True(r.In(() => LAZYLEN(locate(5, 5, 1).atmos_adjacent_turfs) > 0), "the air flows again");

        // on again: it wants power, finds its partner and raises the field at its next early process
        Click(r, bob, master);
        MachinesTick(r);
        Assert.Single(On<obj_effect_atmos_shield>(r, 5, 5));
        Assert.Equal((2f, 2f), r.In(() => (master.on, slave.on)));

        // a power failure: tg's warning sound; the generator waits for power and its field stays until the next process (turn_off :218)
        r.In(() => master.turn_off(power_failure: TRUE));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/cryo_warning.ogg");
        Assert.Equal(1f, r.In(() => master.on));
        Assert.Single(On<obj_effect_atmos_shield>(r, 5, 5));

        // the right click with the access locks it again (attack_hand_secondary :142)
        EmptyHands(r, bob);
        r.In(() =>
        {
            Waiting.NoSleep(bob.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(bob), ITEM_SLOT_ICLOTHING));
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(Dm.@new<obj_item_card_id_advanced_debug>(bob), ITEM_SLOT_ID))), "the ID is worn");
        });
        Click(r, bob, master, right: true);
        Assert.Equal(1f, r.In(() => master.locked));
        r.AssertNoRuntimes();
    }

    // ---- the modular shield generator ----

    private static DmList Data(datum host, mob who) => (DmList)Waiting.NoSleep(host.ui_data(who));

    private static object Act(datum host, mob who, string action, params (string Key, object Value)[] args)
    {
        // the act comes through the user's open window, as tgui sends it
        if (SStgui.get_open_ui(who, host) is not datum_tgui ui)
        {
            Waiting.NoSleep(host.ui_interact(who));
            ui = Assert.IsType<datum_tgui>(SStgui.get_open_ui(who, host));
        }
        DmList p = Dm.list();
        foreach ((string key, object value) in args)
            p[key] = value;
        return Waiting.NoSleep(host.ui_act(action, p, ui, ui.state));
    }

    [Fact]
    public void TheModularGeneratorChargesLinksToItsConsoleProjectsItsRingAndCollapsesWhenDrained()
    {
        obj_machinery_modular_shield_generator gen = null;
        obj_machinery_computer_modular_shield console = null;
        var r = new AudioRoom(15, 13, seed: 1007, mapExtras: w =>
        {
            gen = Dm.@new<obj_machinery_modular_shield_generator>(locate(7, 7, 1));
            console = Dm.@new<obj_machinery_computer_modular_shield>(locate(2, 2, 1));
        });
        mob_living_carbon_human bob = r.Join(7, 6, "Bob");
        r.Step(Second);

        // tier-1 parts (RefreshParts :88): 40 + 20 strength, 3 + 2 regeneration, 3.25 radius halved for the inside to the minimum 3
        Assert.Equal((60f, 5f, 3f, 3f), r.In(() => (gen.max_strength, gen.max_regeneration, gen.max_radius, Dm.num(gen.radius))));
        Assert.Equal(1.25f, r.In(() => Dm.num(gen.current_regeneration))); // recovering: a quarter
        Assert.Equal("gen_recovering_closed", r.In(() => gen.icon_state));
        r.In(() => Waiting.NoSleep(gen.toggle_shields()));
        Assert.False(r.In(() => Dm.truthy(gen.active)), "a recovering generator cannot be switched on");

        // it charges 1.25 a second to 60 (process :379), then it is ready at the full regeneration
        r.Step(30 * Second);
        Assert.InRange(r.In(() => gen.stored_strength), 32f, 42f);
        r.Step(25 * Second);
        Assert.Equal(60f, r.In(() => gen.stored_strength));
        Assert.Equal(0f, r.In(() => gen.recovering));
        Assert.Equal(5f, r.In(() => Dm.num(gen.current_regeneration)));
        Assert.Equal("gen_ready_closed", r.In(() => gen.icon_state));

        // the multitool saves it and uploads it to the console (multitool_act :153, computer :18)
        WallTests.Hold<obj_item_multitool>(r, bob);
        obj_item_multitool tool = r.In(() => (obj_item_multitool)bob.get_active_held_item());
        Click(r, bob, gen);
        Assert.Same(gen, r.In(() => tool.buffer));
        MoveTo(r, bob, 3, 2);
        Click(r, bob, console);
        Assert.Null(r.In(() => tool.buffer));
        Assert.Equal(1, r.In(() => (int)length(console.generators)));
        DmList row = r.In(() => (DmList)((DmList)Data(console, bob)["generators"])[1]);
        Assert.Equal(("Shield Generator", 1f, 60f, 60f, 0f), r.In(() => (Dm.str(row["name"]), Dm.num(row["id"]), Dm.num(row["max_strength"]), Dm.num(row["current_strength"]), Dm.num(row["recovering"]))));

        // the console switches it on: a ring of radius 3 outside the radius-2 circle, fading in for two seconds (activate_shields :175)
        Assert.Equal(TRUE, r.In(() => Act(console, bob, "toggle_shields", ("id", 1f))));
        Assert.True(r.In(() => Dm.truthy(gen.active) && Dm.truthy(gen.initiating)));
        int ring = r.In(() =>
        {
            int count = 0;
            foreach (turf t in Dm.each<turf>(RANGE_TURFS(3, gen)))
            {
                float dx = t.x - 7, dy = t.y - 7;
                if (t is turf_open && dx * dx + dy * dy <= 3 * 3.5f && dx * dx + dy * dy > 2 * 2.5f)
                    count++;
            }
            return count;
        });
        Assert.Equal(16, ring);
        Assert.Equal(ring, r.In(() => (int)length(gen.deployed_shields)));
        obj_structure_emergency_shield_modular piece = Assert.Single(On<obj_structure_emergency_shield_modular>(r, 10, 7));
        Assert.Empty(On<obj_structure_emergency_shield_modular>(r, 9, 7)); // inside the ring
        Assert.Equal((0f, 100f), r.In(() => (Dm.num(piece.density), Dm.num(piece.alpha))));
        Assert.Equal(0, r.In(() => (int)LAZYLEN(locate(10, 7, 1).atmos_adjacent_turfs))); // ATMOS_PASS_NO
        Assert.Equal(1f, r.In(() => Dm.num(gen.current_regeneration))); // 5 / (0.5 + 6 / 3), halved for the inside
        r.Step(3 * Second);
        Assert.Equal((1f, 255f, 0f), r.In(() => (Dm.num(piece.density), Dm.num(piece.alpha), gen.initiating)));

        // a blow drains the generator (take_damage :816); the console renames it (ui_act :72)
        r.In(() => Waiting.NoSleep(piece.take_damage(20, BRUTE, MELEE)));
        Assert.InRange(r.In(() => gen.stored_strength), 40f, 42f);
        Assert.False(r.In(() => QDELETED(piece)), "the shield itself is indestructible");
        Assert.Equal(TRUE, r.In(() => Act(console, bob, "rename", ("id", 1f), ("name", "Bay"))));
        Assert.Equal("Bay", r.In(() => gen.display_name));

        // drained under five it collapses and recovers (shield_drain :370)
        r.In(() => Waiting.NoSleep(piece.take_damage(60, BRUTE, MELEE)));
        Assert.False(r.In(() => Dm.truthy(gen.active)));
        Assert.Equal((0f, 1f), r.In(() => (gen.stored_strength, gen.recovering)));
        Assert.True(r.In(() => QDELETED(piece)));
        Assert.Equal("gen_recovering_closed", r.In(() => gen.icon_state));

        // the generator's own window: the radius is set while it is off and the outside-only mode lifts the inside penalty
        MoveTo(r, bob, 7, 6);
        Assert.Equal(("ModularShieldGen", 3f), r.In(() =>
        {
            Waiting.NoSleep(gen.ui_interact(bob));
            return (((datum_tgui)SStgui.get_open_ui(bob, gen)).@interface as string, Dm.num(Data(gen, bob)["max_radius"]));
        }));
        r.In(() => Act(gen, bob, "set_radius", ("new_radius", 2f)));
        Assert.Equal(2f, r.In(() => Dm.num(gen.radius)));
        r.In(() => Act(gen, bob, "toggle_exterior"));
        Assert.Equal((1f, 3.25f), r.In(() => (gen.exterior_only, gen.max_radius)));

        // a deleted generator leaves the console's list (generator_deleted)
        r.In(() => qdel(gen));
        Assert.Equal(0, r.In(() => (int)length(console.generators)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALargeShieldRingIsTgsMidpointPerimeter()
    {
        var r = new AudioRoom(27, 27, seed: 1009);
        r.In(() =>
        {
            turf centre = locate(14, 14, 1);
            Assert.Null(get_perimeter(centre, 0.5f)); // under 1: nothing (maths.dm:94)
            DmList ring = (DmList)get_perimeter(centre, 10.7f); // rounded down to 10
            var tiles = Dm.each<turf>(ring).Distinct().ToList();
            Assert.Equal(0, ring.len % 8);
            Assert.All(tiles, t => Assert.InRange(MathF.Sqrt((t.x - 14) * (t.x - 14) + (t.y - 14) * (t.y - 14)), 9.5f, 10.5f));
            foreach ((int x, int y) in new[] { (24, 14), (4, 14), (14, 24), (14, 4) })
                Assert.Contains(locate(x, y, 1), tiles);
            // circle_range_turfs: the turfs within radius * (radius + 0.5) squared distance (spatial_info.dm:341)
            Assert.Equal(21, (int)length(circle_range_turfs(centre, 2))); // d² <= 5: the centre, 8 neighbours, 4 at two, 8 knight steps
        });
    }

    [Fact]
    public void TheModulesFacingTheGeneratorOrANodeAddTheirPartsBoosts()
    {
        obj_machinery_modular_shield_generator gen = null;
        obj_machinery_modular_shield_module_relay relay = null;
        obj_machinery_modular_shield_module_well well = null;
        obj_machinery_modular_shield_module_node node = null;
        obj_machinery_modular_shield_module_charger charger = null;
        obj_machinery_modular_shield_module_node_cable cable = null;
        obj_machinery_modular_shield_module_relay stray = null;
        var r = new AudioRoom(13, 11, seed: 1008, mapExtras: w =>
        {
            gen = Dm.@new<obj_machinery_modular_shield_generator>(locate(6, 5, 1));
            relay = Dm.@new<obj_machinery_modular_shield_module_relay>(locate(6, 6, 1));
            relay.dir = SOUTH;
            well = Dm.@new<obj_machinery_modular_shield_module_well>(locate(5, 5, 1));
            well.dir = EAST;
            node = Dm.@new<obj_machinery_modular_shield_module_node>(locate(7, 5, 1));
            node.dir = WEST;
            charger = Dm.@new<obj_machinery_modular_shield_module_charger>(locate(8, 5, 1));
            charger.dir = WEST;
            cable = Dm.@new<obj_machinery_modular_shield_module_node_cable>(locate(6, 4, 1));
            cable.dir = NORTH;
            stray = Dm.@new<obj_machinery_modular_shield_module_relay>(locate(6, 3, 1));
            stray.dir = NORTH;
        });
        r.Join(2, 2, "Bob");
        r.Step(Second);
        r.In(() =>
        {
            Assert.Same(gen, relay.shield_generator);
            Assert.Same(gen, well.shield_generator);
            Assert.Same(gen, node.shield_generator);
            Assert.Same(gen, charger.shield_generator); // through the node (connect_connected_through_us :645)
            Assert.Same(node, charger.connected_node);
            Assert.Same(gen, cable.shield_generator);
            Assert.Null(stray.connected_node); // a booster cannot connect through a cable (try_connect :573)
            Assert.Null(stray.shield_generator);
            Assert.Equal(new object[] { relay, well, node, charger, cable }.OrderBy(o => ((datum)o).dm_id), Dm.each(gen.connected_modules).OrderBy(o => ((datum)o).dm_id));
            // three tier-1 lasers: 0.75 radius; three capacitors: 60 strength; three servos: 6 regeneration (calculate_boost :310)
            Assert.Equal((0.75f, 60f, 6f), (relay.range_boost, well.strength_boost, charger.charge_boost));
            Assert.Equal((0.75f, 60f, 6f), (gen.radius_boost, gen.max_strength_boost, gen.regen_boost));
            Assert.Equal((120f, 11f, 3f), (gen.max_strength, gen.max_regeneration, gen.max_radius)); // (3.25 + 0.75) / 2 = 2, at least 3
            Assert.Equal(("relay_on_closed", "well_on_closed", "node_on_closed", "charger_on_closed", "relay_off_closed"), (relay.icon_state, well.icon_state, node.icon_state, charger.icon_state, stray.icon_state));
            Assert.Equal("cable_node_closed_nr_nb_nl", cable.icon_state);

            // the node gone, the charger loses its link and the generator its boost (node Destroy :635)
            qdel(node);
            Assert.Null(charger.shield_generator);
            Assert.Null(charger.connected_node);
            Assert.Equal((0f, 5f), (gen.regen_boost, gen.max_regeneration));
            Assert.Equal("charger_off_closed", charger.icon_state);
        });
        r.AssertNoRuntimes();
    }
}
