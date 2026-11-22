using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// The M7 zaps slice: tg's tesla_zap (energy_ball.dm:204) with its target priorities (the coil, then the grounding rod, the
/// ridden vehicle, the living, the machine, the blob and the structure), the zap_act of /obj (obj_defense.dm:148), the machine
/// (_machinery.dm:1275), the structure (structures.dm:65), the tesla coil and the grounding rod (coil.dm:88, :155), the energy
/// accumulator's release onto the network (energy_accumulator.dm:38-63), the mob's electrocute_act with SHOCK_TESLA, the lightning
/// beam (beam.dm: the datum, its segments, its cut tip and its five-decisecond life) and the coil's own zap (coil.dm:103).
/// </summary>
public class ZapTests
{
    private const int Second = 20;
    private const float Power = 1_000_000;
    private const float GeneratingFlags = ZAP_DEFAULT_FLAGS | ZAP_GENERATES_POWER;

    private static void MachinesTick(AudioRoom r, int runs = 1) => r.Step(runs * r.In(() => (int)Dm.num(SSmachines.wait)) * 2);

    // The zap's beams made after the watermark, in creation order: (origin, target).
    private static List<(atom From, atom To)> Beams(AudioRoom r, long after) => r.In(() => Dm.each<datum_beam>()
        .Where(b => b.dm_id > after && b.origin != null).Select(b => (b.origin, b.target)).ToList());

    private static long Watermark(AudioRoom r) => r.In(() => Dm.each<datum>().Select(d => d.dm_id).DefaultIfEmpty(0L).Max());

    private static void Zap(AudioRoom r, atom source, float power, float flags = GeneratingFlags, float cutoff = 4e5f, float range = 3) =>
        r.In(() => Waiting.NoSleep(tesla_zap(source, range, power, cutoff, flags)));

    [Fact]
    public void TeslaZapStrikesTheCoilFirstThenTheRodThenTheLivingThenMachinesThenStructures()
    {
        obj_machinery_power_energy_accumulator_tesla_coil coil = null;
        obj_machinery_power_energy_accumulator_grounding_rod rod = null;
        obj_machinery_power_port_gen_pacman pacman = null;
        obj_structure_table table = null;
        var r = new AudioRoom(13, 10, seed: 7401, mapExtras: w =>
        {
            coil = Dm.@new<obj_machinery_power_energy_accumulator_tesla_coil_anchored>(locate(9, 5, 1));
            rod = Dm.@new<obj_machinery_power_energy_accumulator_grounding_rod_anchored>(locate(4, 5, 1));
            pacman = Dm.@new<obj_machinery_power_port_gen_pacman>(locate(7, 5, 1));
            table = Dm.@new<obj_structure_table>(locate(5, 4, 1));
        });
        mob_living_carbon_human bob = r.Dummy(6, 6);
        r.Step(Second);
        turf source = r.In(() => locate(6, 5, 1));

        // the coil, three tiles off, before the rod (two), Bob (one), the generator and the table (one): it takes the whole bolt
        // without a network (coil.dm:97) and the chain ends there
        long mark = Watermark(r);
        Zap(r, source, Power);
        List<(atom From, atom To)> beams = Beams(r, mark);
        Assert.Equal(new (atom, atom)[] { (source, coil) }, beams);
        Assert.Equal(Power, r.In(() => coil.stored_energy));
        Assert.True(r.In(() => HAS_TRAIT(coil, TRAIT_BEING_SHOCKED)), "a struck coil rests a second (coil.dm:91)");

        // the coil resting, the rod: it grounds the whole bolt (coil.dm:155)
        mark = Watermark(r);
        Zap(r, source, Power);
        Assert.Equal(new (atom, atom)[] { (source, rod) }, Beams(r, mark));
        Assert.Equal(Power, r.In(() => rod.stored_energy));

        // without the rod: Bob, then the generator from him (MACHINERY before STRUCTURE), then the table from the generator (a
        // weaker bolt, which breaks neither: a broken one would end the chain, obj_defense.dm:149)
        r.In(() => qdel(rod));
        mark = Watermark(r);
        Zap(r, source, 100_000, cutoff: 1e3f);
        beams = Beams(r, mark);
        Assert.True(beams.Count >= 3, string.Join(", ", beams.Select(b => b.From?.type + " -> " + b.To?.type)));
        Assert.Equal((source, (atom)bob), beams[0]);
        Assert.Equal(((atom)bob, (atom)pacman), beams[1]);
        Assert.Equal(((atom)pacman, (atom)table), beams[2]);
        Assert.True(r.In(() => Dm.num(bob.get_fire_loss())) > 0, "Bob is shocked");
        Assert.True(r.In(() => HAS_TRAIT(pacman, TRAIT_BEING_SHOCKED)), "obj/zap_act marks the generator (obj_defense.dm:151)");
        Assert.True(r.In(() => Dm.num(pacman.get_integrity()) < pacman.max_integrity), "ZAP_OBJ_DAMAGE burns the machine (_machinery.dm:1279)");
        r.Step(Second + 1);
        Assert.False(r.In(() => HAS_TRAIT(coil, TRAIT_BEING_SHOCKED)), "the coil's rest ends after a second");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACoilOnTheNetworkStoresItsShareAndReleasesItAsTheAccumulatorComputes()
    {
        const int LineY = 3, SourceX = 7, CoilX = 3;
        obj_machinery_power_test_m7apc_source powerSource = null;
        obj_machinery_power_energy_accumulator_tesla_coil coil = null;
        var r = new AudioRoom(13, 10, seed: 7402, mapExtras: w =>
        {
            for (int x = 2; x <= SourceX; x++)
            {
                Dm.@new<turf_open_floor_plating>(locate(x, LineY, 1));
                Dm.@new<obj_structure_cable>(locate(x, LineY, 1));
            }
            powerSource = Dm.@new<obj_machinery_power_test_m7apc_source>(locate(SourceX, LineY, 1));
            coil = Dm.@new<obj_machinery_power_energy_accumulator_tesla_coil_anchored>(locate(CoilX, LineY, 1));
        });
        r.In(() => powerSource.output_energy = 2_000_000);
        r.Step(Second);
        MachinesTick(r, 2);
        datum_powernet net = r.In(() => coil.powernet as datum_powernet);
        Assert.NotNull(net);
        Assert.Same(net, r.In(() => Dm.locate<obj_structure_cable>(locate(SourceX, LineY, 1)).powernet));
        // the coil's board has one tier-1 capacitor: a quarter of the bolt (coil.dm:52)
        Assert.Equal(0.25f, r.In(() => coil.input_power_multiplier));
        Assert.Equal("coil1", r.In(() => coil.icon_state));
        Assert.Equal(2_000_000f, r.In(() => net.avail));

        // a supermatter-like bolt (it generates power): the coil keeps its share and gives the rest back
        object left = r.In(() => Waiting.NoSleep(coil.zap_act(Power, ZAP_GENERATES_POWER)));
        Assert.Equal(Power * 0.75f, Dm.num(left));
        Assert.Equal(Power * 0.25f, r.In(() => coil.stored_energy));
        // ZAP_LOW_POWER_GEN takes a tenth first (coil.dm:96); a bolt that makes no power is taken and nothing stored (coil.dm:94)
        r.In(() => coil.stored_energy = 0);
        Assert.Equal(Power / 10 * 0.75f, r.In(() => Dm.num(Waiting.NoSleep(coil.zap_act(Power, ZAP_GENERATES_POWER | ZAP_LOW_POWER_GEN)))));
        Assert.Equal(Power / 10 * 0.25f, r.In(() => coil.stored_energy));
        Assert.Equal(0f, r.In(() => Dm.num(Waiting.NoSleep(coil.zap_act(Power, ZAP_DEFAULT_FLAGS)))));
        Assert.Equal(Power / 10 * 0.25f, r.In(() => coil.stored_energy));

        // the accumulator's release, dE/dt = -0.04 E - 2000 over the machines' two seconds (energy_accumulator.dm:38), onto the
        // network as avail (add_avail), which the next reset makes the network's available power
        r.In(() => coil.stored_energy = 5_000_000);
        float seconds = r.In(() => Dm.num(SSmachines.wait) / 10f);
        static float Output(float e, float t) => MathF.Min(e, e - ((0.04f * e + 2000) * MathF.Pow(MathF.E, -0.04f * t) - 2000) / 0.04f);
        float expected = Output(5_000_000, seconds);
        MachinesTick(r);
        float processed = r.In(() => coil.processed_energy);
        Assert.InRange(processed, expected * 0.999f, expected * 1.001f);
        Assert.InRange(r.In(() => coil.stored_energy), 5_000_000 - processed - 1, 5_000_000 - processed + 1);
        MachinesTick(r);
        float delivered = r.In(() => net.avail) - 2_000_000;
        float second = Output(5_000_000 - processed, seconds);
        Assert.True(delivered >= second * 0.99f && delivered <= processed * 1.01f, $"the network has the coil's release on top of the source: {delivered} (released {processed}, then {second})");
        // the zap's sound grows with what it processes: 1 volume per 4 kW, 1 tile of range per 80 kW (coil.dm:85)
        float watts = r.In(() => Dm.num(energy_to_power(coil.processed_energy)));
        Assert.Equal(MathF.Min(watts / 4000, 100), r.In(() => coil.zap_sound_volume), 3);
        Assert.Equal(MathF.Min(watts / 80000, 10), r.In(() => coil.zap_sound_range), 3);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AGroundingRodAbsorbsTheBoltAndReleasesNothingToTheGrid()
    {
        obj_machinery_power_energy_accumulator_grounding_rod rod = null;
        obj_machinery_power_energy_accumulator_grounding_rod loose = null;
        var r = new AudioRoom(13, 10, seed: 7403, mapExtras: w =>
        {
            rod = Dm.@new<obj_machinery_power_energy_accumulator_grounding_rod_anchored>(locate(8, 5, 1));
            loose = Dm.@new<obj_machinery_power_energy_accumulator_grounding_rod>(locate(2, 8, 1));
        });
        mob_living_carbon_human bob = r.Dummy(4, 5);
        r.Step(Second);
        turf source = r.In(() => locate(6, 5, 1));
        long mark = Watermark(r);
        Zap(r, source, Power);
        Assert.Equal(new (atom, atom)[] { (source, rod) }, Beams(r, mark));
        Assert.Equal(Power, r.In(() => rod.stored_energy));
        Assert.Equal(0f, r.In(() => Dm.num(bob.get_fire_loss())));
        Assert.Equal("grounding_rod1", r.In(() => rod.icon_state));

        // its stored energy drains as an accumulator's, onto no network (coil.dm:163: FALSE, no powernet)
        MachinesTick(r);
        Assert.True(r.In(() => rod.processed_energy) > 0, "the rod releases its grounded energy");
        Assert.True(r.In(() => rod.stored_energy) < Power);
        Assert.Null(r.In(() => rod.powernet));
        // a rod that is not anchored takes the bolt as any machine: it is damaged and halves the bolt (obj_defense.dm:154)
        object left = r.In(() => Waiting.NoSleep(loose.zap_act(100_000, ZAP_DEFAULT_FLAGS)));
        Assert.Equal(100_000 * (1 - 5e-4f) / 2, Dm.num(left), 0);
        Assert.True(r.In(() => Dm.num(loose.get_integrity()) < loose.max_integrity), "a quarter of a thousandth of the bolt as burn damage");
        Assert.Equal(0f, r.In(() => loose.stored_energy));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABoltShocksTheLivingWithSHOCK_TESLAAndItsStunFollowsZAP_MOB_STUN()
    {
        var r = new AudioRoom(13, 10, seed: 7404);
        mob_living_carbon_human bob = r.Dummy(7, 5);
        mob_living_carbon_human immune = r.Dummy(5, 5);
        r.In(() => ADD_TRAIT(immune, TRAIT_TESLA_SHOCKIMMUNE, "test"));
        r.Step(Second);
        turf source = r.In(() => locate(6, 5, 1));
        // the shock: min(round(power / 600), 90) + rand(-5, 5) (energy_ball.dm:333), so 50 +- 5 for 30 kJ
        long mark = Watermark(r);
        Zap(r, source, 30_000, ZAP_DEFAULT_FLAGS, cutoff: 1e3f);
        Assert.Equal((source, (atom)bob), Beams(r, mark)[0]);
        Assert.InRange(r.In(() => Dm.num(bob.get_fire_loss())), 45f, 55f);
        Assert.True(r.In(() => Dm.truthy(bob.IsStun()) || Dm.truthy(bob.IsParalyzed())), "ZAP_MOB_STUN stuns (no SHOCK_NOSTUN)");
        Assert.Equal(0f, r.In(() => Dm.num(immune.get_fire_loss())));
        Assert.DoesNotContain(Beams(r, mark), b => b.To == immune);

        // without ZAP_MOB_STUN the shock is SHOCK_NOSTUN; without ZAP_MOB_DAMAGE it does no damage
        mob_living_carbon_human ann = r.Dummy(10, 8);
        r.Step(Second * 2);
        turf near = r.In(() => locate(10, 7, 1));
        Zap(r, near, 30_000, ZAP_MOB_DAMAGE, cutoff: 1e3f);
        Assert.InRange(r.In(() => Dm.num(ann.get_fire_loss())), 45f, 55f);
        Assert.False(r.In(() => Dm.truthy(ann.IsStun()) || Dm.truthy(ann.IsParalyzed())), "SHOCK_NOSTUN");
        r.Step(Second * 2);
        float before = r.In(() => Dm.num(ann.get_fire_loss()));
        Zap(r, near, 30_000, ZAP_MOB_STUN, cutoff: 1e3f);
        Assert.Equal(before, r.In(() => Dm.num(ann.get_fire_loss())));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBoltIsALightningBeamOfTgsSegmentsThatLastsFiveDeciseconds()
    {
        obj_machinery_power_energy_accumulator_grounding_rod rod = null;
        var r = new AudioRoom(13, 10, seed: 7405, mapExtras: w => rod = Dm.@new<obj_machinery_power_energy_accumulator_grounding_rod_anchored>(locate(9, 5, 1)));
        r.Step(Second);
        turf source = r.In(() => locate(5, 5, 1));
        Zap(r, source, Power);
        datum_beam beam = r.In(() => Dm.each<datum_beam>().Single(b => b.target == rod));
        Assert.Matches("^lightning([1-9]|1[0-2])$", r.In(() => beam.icon_state));
        Assert.Equal("icons/effects/beam.dmi", r.In(() => (beam.icon as DmResource)?.Path));
        // four tiles east: 128 pixels, four segments of 32 (beam.dm:278), facing east (a 90 degree turn of the north-pointing sprite)
        List<obj_effect_ebeam> segments = r.In(() => Dm.each<obj_effect_ebeam>(beam.elements).ToList());
        Assert.Equal(4, segments.Count);
        Assert.All(segments, s => Assert.Equal(beam.icon_state, r.In(() => s.icon_state)));
        Assert.Equal(new[] { 5f, 6f, 7f, 8f }, r.In(() => segments.Select(s => s.x).ToArray()));
        r.In(() =>
        {
            matrix m = (matrix)segments[0].transform;
            Assert.Equal(0f, m.a, 3);
            Assert.Equal(1f, Math.Abs(m.b), 3);
            Assert.Equal(ABOVE_ALL_MOB_LAYER, segments[0].layer);
        });
        r.Step(11); // 5 ds: ten ticks of half a decisecond
        Assert.True(r.In(() => QDELETED(beam)), "the zap's beam lives five deciseconds (energy_ball.dm:325, time = 5)");
        Assert.All(segments, s => Assert.True(r.In(() => QDELETED(s))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AShortBeamCutsItsTerminalSegmentWithTgsFadingTip()
    {
        var r = new AudioRoom(13, 10, seed: 7406);
        obj_item_wrench a = null, b = null;
        r.In(() =>
        {
            a = Dm.@new<obj_item_wrench>(locate(4, 4, 1));
            b = Dm.@new<obj_item_wrench>(locate(4, 5, 1));
            b.pixel_y = -12; // 20 pixels apart: one segment, cut at row 20 with a four-row fade (beam.dm:299-313)
        });
        datum_beam beam = r.In(() => (datum_beam)a.Beam(b, icon_state: "lightning3", time: 10));
        r.Step();
        obj_effect_ebeam tip = r.In(() => Dm.each<obj_effect_ebeam>(beam.elements).Single());
        icon cut = r.In(() => tip.icon as icon);
        Assert.NotNull(cut);
        Assert.Equal("lightning3", cut.dm_state);
        string recipe = cut.dm_resource();
        Assert.StartsWith("iconops:", recipe);
        // the cut (DrawBox null from row 20 up) and the alpha mask's multiply
        // the alpha mask's own operations follow its blend: 6 boxes (the white, the four fading rows, the cut)
        string[] parts = recipe.Substring("iconops:".Length).Split('|').Select(Uri.UnescapeDataString).ToArray();
        Assert.Equal(3 + 2 + 6, parts.Length);
        Assert.Equal("100,1,20,b,,32,32", parts[3]);
        Assert.Equal("2,1,1,i,icons/effects/beam.dmi,lightning3,6", parts[4]); // a field escapes only %, | and ,
        Assert.Equal("100,1,1,b,#ffffffff,32,32", parts[5]);
        Assert.Equal("100,1,20,b,,32,32", parts[10]);
        Assert.True(recipe.Length <= 512, $"the recipe fits the protocol's resource cap (Limits.MaxResourceBytes): {recipe.Length}");
        Assert.Equal(2, cut.dm_ops.Count);
        Assert.Equal((int)Dm.ICON_MULTIPLY, cut.dm_ops[1].Function);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePulsedCoilZapsWithAFifthOfTheNetworksPowerAndItsLightningSound()
    {
        const int LineY = 3, SourceX = 7, CoilX = 3;
        obj_machinery_power_test_m7apc_source powerSource = null;
        obj_machinery_power_energy_accumulator_tesla_coil coil = null;
        var r = new AudioRoom(13, 10, seed: 7407, mapExtras: w =>
        {
            for (int x = 2; x <= SourceX; x++)
            {
                Dm.@new<turf_open_floor_plating>(locate(x, LineY, 1));
                Dm.@new<obj_structure_cable>(locate(x, LineY, 1));
            }
            powerSource = Dm.@new<obj_machinery_power_test_m7apc_source>(locate(SourceX, LineY, 1));
            coil = Dm.@new<obj_machinery_power_energy_accumulator_tesla_coil_anchored>(locate(CoilX, LineY, 1));
        });
        r.In(() => powerSource.output_energy = 2_000_000);
        mob_living_carbon_human bob = r.Join(CoilX, LineY + 3, "Bob");
        r.Step(Second * 10); // past the coil's first cooldown: last_zap 0 + zap_cooldown 8 s (coil.dm:104, RefreshParts)
        // a coil that processes energy is heard: its zap's volume and range follow what it released (coil.dm:85); one that
        // released nothing zaps at volume 0, unheard
        r.In(() => coil.stored_energy = 5_000_000);
        MachinesTick(r, 2);
        Assert.True(r.In(() => coil.zap_sound_volume) > 0);
        datum_powernet net = r.In(() => coil.powernet as datum_powernet);
        float load = r.In(() => Dm.num(net.load));
        // WIRE_ZAP's pulse (wires/tesla_coil.dm:10): zap() takes min(surplus, avail * 0.2 * multiplier) from the network
        r.In(() => Waiting.NoSleep(coil.wires.pulse(WIRE_ZAP)));
        float taken = r.In(() => Dm.num(net.load)) - load;
        float avail = r.In(() => net.avail);
        Assert.Equal(avail * 0.2f * 0.25f, taken, 0);
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/magic/lightningshock.ogg");
        Assert.True(r.In(() => Dm.num(bob.get_fire_loss())) > 0, "the coil's bolt finds Bob");
        // the cooldown: a second pulse within zap_cooldown does nothing
        float load2 = r.In(() => Dm.num(net.load));
        r.In(() => Waiting.NoSleep(coil.wires.pulse(WIRE_ZAP)));
        Assert.Equal(load2, r.In(() => Dm.num(net.load)));
        r.AssertNoRuntimes();
    }
}
