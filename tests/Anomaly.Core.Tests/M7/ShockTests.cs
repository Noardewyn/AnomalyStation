using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// The M7 shocks slice: tg's electrocute_mob (power.dm:478) with get_powernet_info_from_source (:442), the living, carbon and
/// human electrocute_act (living_defense.dm:617, carbon_defense.dm:218, human_defense.dm:363) with the stun, the secondary
/// shock and the electrocution animation (human.dm:634), the gloves' siemens_coefficient (insulated.dm), /obj/shock with its
/// sparks (objs.dm:402) and the sites that call it: the grille over a live cable (grille.dm:354, :409), cutting a live cable
/// (cable.dm:215, :282), the light socket (light.dm:425), the APC's fork outlet (apc_tool_act.dm:34), the reinforced table
/// (tables_racks.dm:821) and the electrified airlock (airlock.dm:485). A toy room with a cable run on plating fed by a test
/// power source (ApcTests' fixture), and for the area's power an APC.
/// </summary>
public class ShockTests
{
    private const int Second = 20;
    private const int LineY = 3, SourceX = 7, GrilleX = 5, TableX = 3;

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Bob, obj_machinery_power_test_m7apc_source Source, obj_structure_grille Grille);

    // A cable run at y = 3 from x = 2 to the source at x = 7, on plating; a grille over the cable at x = 5; Bob north of x = 4.
    private static Rig Room(ulong seed, float output = 2_000_000, Action<World> extras = null)
    {
        obj_machinery_power_test_m7apc_source source = null;
        obj_structure_grille grille = null;
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: w =>
        {
            for (int x = 2; x <= SourceX; x++)
            {
                Dm.@new<turf_open_floor_plating>(locate(x, LineY, 1));
                Dm.@new<obj_structure_cable>(locate(x, LineY, 1));
            }
            source = Dm.@new<obj_machinery_power_test_m7apc_source>(locate(SourceX, LineY, 1));
            grille = Dm.@new<obj_structure_grille>(locate(GrilleX, LineY, 1));
            extras?.Invoke(w);
        });
        r.In(() => source.output_energy = output);
        mob_living_carbon_human bob = r.Join(4, LineY + 1, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        MachinesTick(r, 2); // the source's power is the network's avail from the next reset (powernet.dm:92), every cycle after
        return new Rig(r, bob, source, grille);
    }

    private static void MachinesTick(AudioRoom r, int runs = 1) => r.Step(runs * r.In(() => (int)Dm.num(SSmachines.wait)) * 2);

    private static obj_structure_cable CableAt(AudioRoom r, int x, int y = LineY) => r.In(() => Dm.locate<obj_structure_cable>(locate(x, y, 1)));

    private static float Burn(Rig g) => g.R.In(() => Dm.num(g.Bob.get_fire_loss()));

    // ELECTROCUTE_DAMAGE without its rand(-5, 5) (combat.dm:400) of a network's avail as power (powernet.dm:95, maths.dm:185).
    private static float BaseDamage(AudioRoom r, float avail) => r.In(() =>
    {
        float power = avail * SECONDS / Dm.num(SSmachines.wait);
        return power >= 1 * KILO * JOULES ? clamp(20 + round(power / JOULES_PER_DAMAGE), 20, 195) : 0;
    });

    private static obj_item_clothing_gloves Wear<T>(Rig g) where T : obj_item_clothing_gloves => g.R.In(() =>
    {
        T gloves = Dm.@new<T>(get_turf(g.Bob));
        Assert.True(Dm.truthy(Waiting.NoSleep(g.Bob.equip_to_slot_if_possible(gloves, ITEM_SLOT_GLOVES))), "the gloves are worn");
        return (obj_item_clothing_gloves)gloves;
    });

    private static bool HasZap(Rig g) => g.R.In(() => Dm.each(g.Bob.overlays).Any(o => o switch
    {
        mutable_appearance m => m.icon_state == "electrocuted_base",
        DmAppearance d => d.icon_state == "electrocuted_base",
        _ => false,
    }));

    // The sparks of a do_sparks (effects_sparks.dm:8): each spark plays tg's sparks sound where it starts (effects_sparks.dm:43);
    // the shock is the only spark source of these rooms.
    private static int Sparks(AudioRoom r) => r.Sounds.Heard.Count(h => h.File != null && h.File.Contains("sparks", StringComparison.Ordinal));

    [Fact]
    public void ElectrocuteMobDealsTgsDamageFromTheNetworksPowerThenStunsAndFlashesTheHuman()
    {
        Rig g = Room(7301);
        datum_powernet net = g.R.In(() => CableAt(g.R, 3).powernet);
        float avail = g.R.In(() => net.avail);
        Assert.Equal(2_000_000f, avail);
        float expected = BaseDamage(g.R, avail);
        Assert.Equal(60f, expected); // 1 MW: 20 + round(1e6 / 25 kJ)
        float delayed0 = g.R.In(() => net.delayedload), newavail = g.R.In(() => net.newavail);

        object drained = g.R.In(() => Waiting.NoSleep(electrocute_mob(g.Bob, CableAt(g.R, 3), CableAt(g.R, 3), 1, FALSE)));
        float burn = Burn(g);
        Assert.InRange(burn, expected - 5, expected + 5);
        // the human returns the shock's damage, which drains twenty joules a point from the network's delayed load
        Assert.Equal(burn * 20, Dm.num(drained));
        Assert.Equal(delayed0 + min(burn * 20, max(newavail - delayed0, 0)), g.R.In(() => net.delayedload));

        // the stun: upright first (Stun, 4 seconds), floored two seconds later (secondary_shock: Paralyze, 6 seconds)
        Assert.InRange(g.R.In(() => Dm.num(g.Bob.AmountStun())), 39f, 40f);
        Assert.False(g.R.In(() => Dm.truthy(g.Bob.IsParalyzed())));
        // the electrocution animation: black, with the skeleton overlay of human.dmi
        Assert.True(HasZap(g), "the skeleton flash is on");
        Assert.Equal("#000000", g.R.In(() => Dm.str(g.Bob.color)));
        g.R.Step(2 * Second + 1);
        Assert.True(g.R.In(() => Dm.truthy(g.Bob.IsParalyzed())), "the secondary shock floors him");
        Assert.InRange(g.R.In(() => Dm.num(g.Bob.AmountParalyzed())), 58f, 60f);
        g.R.Step(2 * Second);
        Assert.False(HasZap(g), "the flash ends after four seconds");
        Assert.NotEqual("#000000", g.R.In(() => Dm.str(g.Bob.color)));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void BelowAKilowattNothingShocksAndInsulatedGlovesStopIt()
    {
        Rig weak = Room(7302, output: 1500); // 750 W
        Assert.Equal(0f, BaseDamage(weak.R, weak.R.In(() => CableAt(weak.R, 3).powernet.avail)));
        Assert.Equal(0f, weak.R.In(() => Dm.num(Waiting.NoSleep(electrocute_mob(weak.Bob, CableAt(weak.R, 3), CableAt(weak.R, 3))))));
        Assert.Equal(0f, Burn(weak));
        Assert.False(weak.R.In(() => Dm.truthy(weak.Bob.IsStun())));

        Rig g = Room(7303);
        Wear<obj_item_clothing_gloves_color_yellow>(g);
        Assert.Equal(0f, g.R.In(() => Dm.num(Waiting.NoSleep(electrocute_mob(g.Bob, CableAt(g.R, 3), CableAt(g.R, 3))))));
        Assert.Equal(0f, Burn(g));
        Assert.False(g.R.In(() => Dm.truthy(g.Bob.IsStun())));
        Assert.False(HasZap(g));
        // no power source at all: nothing to shock with
        Assert.False(g.R.In(() => Dm.truthy(get_powernet_info_from_source(null))));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void BudgetGlovesRollTgsCoefficientsAndHalfInsulationHalvesTheShock()
    {
        Rig g = Room(7304);
        var budget = new List<float>();
        var old = new List<float>();
        g.R.In(() =>
        {
            for (int i = 0; i < 40; i++)
            {
                budget.Add(Dm.@new<obj_item_clothing_gloves_color_fyellow>(locate(9, 7, 1)).siemens_coefficient);
                old.Add(Dm.@new<obj_item_clothing_gloves_color_fyellow_old>(locate(9, 7, 1)).siemens_coefficient);
            }
            Assert.Equal(0f, Dm.@new<obj_item_clothing_gloves_color_yellow>(locate(9, 7, 1)).siemens_coefficient);
        });
        Assert.All(budget, s => Assert.Contains(s, new[] { 0f, 0.5f, 0.75f, 1.5f }));
        Assert.All(old, s => Assert.Contains(s, new[] { 0f, 0.5f, 0.75f }));
        Assert.True(budget.Distinct().Count() > 1, "budget gloves are random");
        Assert.True(old.Distinct().Count() > 1, "worn out gloves are random");

        obj_item_clothing_gloves gloves = Wear<obj_item_clothing_gloves_color_fyellow>(g);
        g.R.In(() => gloves.siemens_coefficient = 0.5f);
        float expected = BaseDamage(g.R, g.R.In(() => CableAt(g.R, 3).powernet.avail));
        Waiting.NoSleep(g.R.In(() => electrocute_mob(g.Bob, CableAt(g.R, 3), CableAt(g.R, 3))));
        Assert.InRange(Burn(g), (expected - 5) * 0.5f, (expected + 5) * 0.5f); // human_defense.dm:375: siemens_coeff *= gloves'
        Assert.True(g.R.In(() => Dm.truthy(g.Bob.IsStun())), "a half-insulated hand is still stunned");
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AGrilleOverALiveCableShocksAHumanWhoTouchesItWithSparksAndNoneWithoutPower()
    {
        Rig g = Room(7305);
        Assert.True(g.R.In(() => Dm.truthy(g.Grille.is_shocked())));
        // a punch: grille.dm:169, shock(user, 70) on every hit; Bob tries until it bites
        for (int i = 0; i < 8 && Burn(g) == 0; i++)
            WallTests.Click(g.R, g.Bob, g.Grille);
        Assert.True(Burn(g) > 0, "the grille shocked Bob");
        Assert.True(Sparks(g.R) > 0, "the grille sparks (objs.dm:409)");
        Assert.True(g.R.In(() => Dm.truthy(g.Bob.IsStun()) || Dm.truthy(g.Bob.IsParalyzed())));

        // without power: the source stops and the network's avail is zero after the next reset
        g.R.In(() => g.Source.output_energy = 0);
        MachinesTick(g.R, 2);
        Assert.False(g.R.In(() => Dm.truthy(g.Grille.is_shocked())));
        mob_living_carbon_human ann = g.R.In(() => Dm.@new<mob_living_carbon_human>(locate(GrilleX, LineY + 1, 1)));
        Assert.False(g.R.In(() => Dm.truthy(Waiting.NoSleep(g.Grille.shock__proc(ann, 100)))));
        Assert.Equal(0f, g.R.In(() => Dm.num(ann.get_fire_loss())));
        // a grille that is not anchored is never connected
        g.R.In(() => g.Source.output_energy = 2_000_000);
        MachinesTick(g.R, 2);
        g.R.In(() => g.Grille.anchored = FALSE);
        Assert.False(g.R.In(() => Dm.truthy(Waiting.NoSleep(g.Grille.shock__proc(ann, 100)))));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void CuttingALiveCableBareHandedShocksAndInsulatedGlovesCutItSafely()
    {
        // wirecutter_act's shock is a 50 % coin (cable.dm:215): the first of these seeds whose coin falls heads on the first cut
        // (a pinned seed moves whenever a subsystem joins the Master's boot and draws from the RNG)
        Rig bare = null;
        obj_structure_cable cable = null;
        for (ulong seed = 7308; seed < 7340; seed++)
        {
            bare = Room(seed);
            cable = CableAt(bare.R, 3);
            WallTests.Hold<obj_item_wirecutters>(bare.R, bare.Bob);
            WallTests.Click(bare.R, bare.Bob, cable);
            if (Burn(bare) > 0)
                break;
        }
        Assert.True(Burn(bare) > 0, "the live cable shocked the bare hand");
        Assert.False(bare.R.In(() => QDELETED(cable)), "the shock stopped the cut (ITEM_INTERACT_BLOCKING)");
        Assert.True(Sparks(bare.R) > 0);

        Rig gloved = Room(7308);
        Wear<obj_item_clothing_gloves_color_yellow>(gloved);
        obj_structure_cable cable2 = CableAt(gloved.R, 3);
        WallTests.Hold<obj_item_wirecutters>(gloved.R, gloved.Bob);
        WallTests.Click(gloved.R, gloved.Bob, cable2);
        Assert.Equal(0f, Burn(gloved));
        Assert.True(gloved.R.In(() => QDELETED(cable2)), "the insulated hand cut the cable");
        bare.R.AssertNoRuntimes();
        gloved.R.AssertNoRuntimes();
    }

    [Fact]
    public void AReinforcedTableOverALiveCableShocksItsDeconstructor()
    {
        obj_structure_table_reinforced table = null;
        Rig g = Room(7307, extras: _ => table = Dm.@new<obj_structure_table_reinforced>(locate(TableX, LineY, 1)));
        Assert.True(g.R.In(() => Dm.truthy(Waiting.NoSleep(table.attempt_electrocution(g.Bob)))));
        Assert.True(Burn(g) > 0);
        Assert.True(Sparks(g.R) > 0);
        g.R.AssertNoRuntimes();
    }

    // An area that requires power with its APC (tg's charged cell) south of (3, 2), a light fixture and an airlock.
    private sealed record AreaRig(AudioRoom R, mob_living_carbon_human Bob, obj_machinery_power_apc Apc, obj_machinery_light Lamp, obj_machinery_door_airlock Door);

    private static AreaRig AreaRoom(ulong seed)
    {
        obj_machinery_power_apc apc = null;
        obj_machinery_light lamp = null;
        obj_machinery_door_airlock door = null;
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: w =>
        {
            ((area)GLOB.areas_by_type[typeof(area_misc_testroom)]).requires_power = TRUE;
            Dm.@new<obj_structure_cable>(locate(3, 2, 1));
            apc = Dm.@new<obj_machinery_power_apc_auto_name_directional_south>(locate(3, 2, 1));
            lamp = Dm.@new<obj_machinery_light_directional_south>(locate(5, 2, 1));
            door = Dm.@new<obj_machinery_door_airlock>(locate(9, 5, 1));
        });
        mob_living_carbon_human bob = r.Join(4, 3, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return new AreaRig(r, bob, apc, lamp, door);
    }

    [Fact]
    public void AConductiveItemStuckIntoALiveEmptySocketSparksAndShocks()
    {
        AreaRig g = AreaRoom(7308);
        g.R.In(() =>
        {
            g.Lamp.status = LIGHT_EMPTY;
            Waiting.NoSleep(g.Lamp.update());
            Assert.True(Dm.truthy(g.Lamp.has_power()));
        });
        WallTests.Hold<obj_item_crowbar>(g.R, g.Bob);
        // light.dm:425-427: the sparks every time, the shock three times in four, from the area's APC (power.dm:444)
        for (int i = 0; i < 6 && g.R.In(() => Dm.num(g.Bob.get_fire_loss())) == 0; i++)
            WallTests.Click(g.R, g.Bob, g.Lamp);
        Assert.True(g.R.In(() => Dm.num(g.Bob.get_fire_loss())) > 0, "the socket shocked Bob");
        Assert.True(Sparks(g.R) > 0);
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AForkShovedIntoAnOpenApcShocksAndDischargesItsCell()
    {
        AreaRig g = AreaRoom(7309);
        // ADAPTED: the fork (kitchen_utensils.dm:21, TRAIT_APC_SHOCKING) is generated only; an iron item that is no tool (a tool
        // acts first, atom_tool_acts.dm:12) with its trait stands in
        obj_item fork = WallTests.Hold<obj_item_stack_rods>(g.R, g.Bob, 2);
        g.R.In(() =>
        {
            ADD_TRAIT(fork, TRAIT_APC_SHOCKING, INNATE_TRAIT);
            Assert.True(Dm.truthy(LAZYACCESS(fork.custom_materials, Waiting.NoSleep(SSmaterials.get_material(typeof(datum_material_iron))))), "the stand-in is iron");
            g.Apc.opened = APC_COVER_OPENED;
            Assert.True(g.Apc.cell.charge > 0);
        });
        WallTests.Click(g.R, g.Bob, g.Apc);
        Assert.True(g.R.In(() => Dm.num(g.Bob.get_fire_loss())) > 0, "the APC shocked Bob");
        Assert.True(Sparks(g.R) > 0);
        Assert.Equal(0f, g.R.In(() => g.Apc.cell.charge)); // the cell was the source: fully discharged (apc_tool_act.dm:50)
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AnElectrifiedAirlockShocksOnceASecondAndNotWithoutPower()
    {
        AreaRig g = AreaRoom(7310);
        mob_living_carbon_human ann = g.R.In(() => Dm.@new<mob_living_carbon_human>(locate(9, 4, 1)));
        g.R.In(() =>
        {
            g.Door.secondsElectrified = MACHINE_ELECTRIFIED_PERMANENT; // what an EMP or the shock wire sets (airlock.dm:1620)
            Assert.True(Dm.truthy(g.Door.hasPower()));
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Door.shock__proc(ann, 100))));
            Assert.True(Dm.num(ann.get_fire_loss()) > 0);
            Assert.True(HAS_TRAIT(ann, TRAIT_AIRLOCK_SHOCKIMMUNE));
            Assert.False(Dm.truthy(Waiting.NoSleep(g.Door.shock__proc(ann, 100)))); // the cooldown and the immunity (airlock.dm:490-499)
        });
        g.R.Step(Second + 2);
        g.R.In(() =>
        {
            Assert.False(HAS_TRAIT(ann, TRAIT_AIRLOCK_SHOCKIMMUNE));
            g.Door.machine_stat = Dm.bitor(g.Door.machine_stat, NOPOWER);
            Assert.False(Dm.truthy(g.Door.hasPower()));
            Assert.False(Dm.truthy(Waiting.NoSleep(g.Door.shock__proc(ann, 100)))); // unpowered, no shock
        });
        g.R.AssertNoRuntimes();
    }
}
