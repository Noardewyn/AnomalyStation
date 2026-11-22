using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The M7 zaps slice through the real server on MetaStation's engine room: the dev console's <c>zap X Y 1 POWER</c> (tg's
/// tesla_zap, energy_ball.dm:204) next to one of the room's anchored tesla coils strikes the coil first: it stores its quarter
/// (coil.dm:88) and releases it onto its network as the energy accumulator does (energy_accumulator.dm:38-63), which is measured on
/// the network's available power, and the client sees the lightning beam's segments (beam.dm, lightning1 to 12). Then a bolt next to
/// the player in a corridor away from the engine shocks him (electrocute_act with SHOCK_TESLA: burnt and stunned) and he sees the
/// bolt that struck him. No new runtime.
/// </summary>
public class ZapGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const string BeamPath = "/obj/effect/ebeam";

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static string StateOf(ReplicaWorld replica, ReplicaEntity e)
        => replica.TryGetAppearance(e.State.Appearance, out AppearanceEntry look) ? replica.ResolveString(look.IconState) : null;

    private static string IconOf(ReplicaWorld replica, ReplicaEntity e)
        => replica.TryGetAppearance(e.State.Appearance, out AppearanceEntry look) ? replica.ResolveResource(look.Icon) : null;

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    // Watches the client for the bolt's segments for a few ticks (a zap's beam lives five deciseconds).
    private static List<ReplicaEntity> SeenBeams(RealHeadlessWorld w, ReplicaWorld replica, int ticks)
    {
        var seen = new Dictionary<long, ReplicaEntity>();
        for (int i = 0; i < ticks; i++)
        {
            w.Run(1);
            foreach (ReplicaEntity e in replica.Entities.Where(e => e.TypePath != null && e.TypePath.StartsWith(BeamPath, StringComparison.Ordinal)))
                seen[e.Id] = e;
        }
        return seen.Values.ToList();
    }

    [MetaServerFact]
    public void A_dev_zap_by_an_engine_coil_powers_its_network_and_is_seen_and_a_zap_by_a_player_shocks_him()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 81, TimeoutSteps = 12000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        w.Run(2 * Second);

        // the engine room's coils (MetaStation.dmm: the anchored tesla coils and grounding rods around the supermatter)
        List<obj_machinery_power_energy_accumulator_tesla_coil> coils = In(w, () => Dm.each<obj_machinery_power_energy_accumulator_tesla_coil>(Dm.world).ToList());
        int rods = In(w, () => Dm.each<obj_machinery_power_energy_accumulator_grounding_rod>(Dm.world).Count());
        output.WriteLine($"{coils.Count} tesla coils and {rods} grounding rods on the map");
        Assert.True(coils.Count >= 2 && rods >= 2);
        obj_machinery_power_energy_accumulator_tesla_coil coil = coils.First(c => In(w, () => c.powernet != null));
        datum_powernet net = In(w, () => (datum_powernet)coil.powernet);
        int cx = (int)In(w, () => Dm.num(coil.x)), cy = (int)In(w, () => Dm.num(coil.y));
        turf spot = In(w, () => new[] { NORTH, SOUTH, EAST, WEST }.Select(d => get_step(coil, d)).First(t => t is turf_open));
        int sx = (int)In(w, () => Dm.num(spot.x)), sy = (int)In(w, () => Dm.num(spot.y));
        output.WriteLine($"the coil at {cx},{cy} in {In(w, () => Dm.str(((area)get_area(coil)).name))}, its network {In(w, () => net.avail)} J a cycle, {In(w, () => net.nodes.len)} machines; the bolt from {sx},{sy}");
        // the player stands a few tiles away in the room, out of the bolt's way (the coils, rods and machines come first)
        In(w, () => Waiting.NoSleep(me.forceMove(Dm.each<turf>(orange(4, coil)).First(t => Clear(t) && get_dist(t, coil) >= 3))));
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        // the coil at rest: what its network carries before
        float stored0 = In(w, () => coil.stored_energy);
        Assert.Equal(0f, stored0);
        float multiplier = In(w, () => coil.input_power_multiplier);
        const float Power = 4_000_000;
        w.Host.EnqueueConsoleLine($"zap {sx} {sy} 1 {Power}");
        List<ReplicaEntity> beams = SeenBeams(w, replica, 4);
        float stored = In(w, () => coil.stored_energy);
        output.WriteLine($"the coil stored {stored} J (multiplier {multiplier}); the client saw {beams.Count} beam segments: {string.Join(",", beams.Select(b => StateOf(replica, b)).Distinct())}");
        // the coil took the bolt first and keeps its share (the release may already have begun this cycle)
        Assert.True(stored > 0 && stored <= Power * multiplier, $"the coil stores at most its {multiplier} of the bolt");
        Assert.NotEmpty(beams);
        // the segments show lightning1 to 12; the cut tip shows its own icon (the state's recipe, icon_state "", beam.dm:330)
        Assert.All(beams.Where(b => !string.IsNullOrEmpty(StateOf(replica, b))), b => Assert.Matches("^lightning([1-9]|1[0-2])$", StateOf(replica, b)));
        Assert.All(beams.Where(b => string.IsNullOrEmpty(StateOf(replica, b))), b => Assert.StartsWith("iconops:", IconOf(replica, b)));
        Assert.Contains(beams, b => Math.Abs(b.State.X - cx) + Math.Abs(b.State.Y - cy) <= 2);

        // the release onto the network: each machines cycle, energy_accumulator's calculate_energy_output, added as avail
        float released = 0, availBefore = In(w, () => net.avail);
        for (int i = 0; i < 4; i++)
        {
            w.Run(2 * Second);
            released = Math.Max(released, In(w, () => coil.processed_energy));
        }
        float availAfter = In(w, () => net.avail);
        output.WriteLine($"released {released} J a cycle; the network had {availBefore} J and now {availAfter} J");
        Assert.True(released > 0, "the coil releases its energy onto its network");
        Assert.True(In(w, () => coil.stored_energy) < stored, "the stored energy drains");
        Assert.True(availAfter > availBefore, "the engine's network (no other source) now carries the coils' release");
        Assert.True(In(w, () => net.avail) >= In(w, () => coil.processed_energy), "the network's available power includes the coil's release");
        // the beam is gone with its five deciseconds
        Assert.DoesNotContain(replica.Entities, e => e.TypePath != null && e.TypePath.StartsWith(BeamPath, StringComparison.Ordinal));

        // a bolt by the player in a station corridor with no coil, rod or vehicle near: he is the closest living thing
        turf corridor = In(w, () => Dm.each<turf>(Dm.world)
            .First(t => Dm.eq(t.z, 1) && get_area(t) is area_station_hallway && Clear(t) && get_dist(t, coil) > 20 && Clear(get_step(t, EAST))));
        In(w, () => Waiting.NoSleep(me.forceMove(corridor)));
        w.Run(2 * Second);
        float burn0 = In(w, () => Dm.num(me.get_fire_loss()));
        int px = (int)In(w, () => Dm.num(corridor.x)) + 1, py = (int)In(w, () => Dm.num(corridor.y));
        w.Host.EnqueueConsoleLine($"zap {px} {py} 1 1200000");
        beams = SeenBeams(w, replica, 4);
        float burn = In(w, () => Dm.num(me.get_fire_loss())) - burn0;
        output.WriteLine($"the bolt by the player: {burn} burn, the client saw {beams.Count} segments");
        Assert.True(burn > 0, "the bolt shocked the player");
        Assert.True(In(w, () => Dm.truthy(me.IsStun()) || Dm.truthy(me.IsParalyzed())), "the default bolt stuns (ZAP_MOB_STUN)");
        Assert.True(In(w, () => HAS_TRAIT(me, TRAIT_BEING_SHOCKED)), "a struck mob rests a second (energy_ball.dm:331)");
        Assert.NotEmpty(beams);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount); // nothing the zaps did ran into a runtime
    }

    // The client's own overlays of an entity: (icon state, colour) of every overlay appearance.
    private static List<(string State, uint Rgba)> OverlaysOf(ReplicaWorld replica, ReplicaEntity e)
    {
        var found = new List<(string, uint)>();
        if (!replica.TryGetAppearance(e.State.Appearance, out AppearanceEntry look))
            return found;
        foreach (TableRef o in look.Overlays)
        {
            if (replica.TryGetAppearance(o, out AppearanceEntry over))
                found.Add((replica.ResolveString(over.IconState), over.Rgba));
        }
        return found;
    }

    [MetaServerFact]
    public void An_engineer_turns_an_engine_emitter_on_and_locks_it_with_his_id()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 82, TimeoutSteps = 12000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        w.Run(2 * Second);

        // the engine room's emitters (MetaStation.dmm: welded ones facing the crystal and spares): a welded one on the network
        List<obj_machinery_power_emitter> emitters = In(w, () => Dm.each<obj_machinery_power_emitter>(Dm.world).ToList());
        output.WriteLine($"{emitters.Count} emitters, {emitters.Count(e => In(w, () => Dm.truthy(e.welded)))} welded");
        obj_machinery_power_emitter emitter = emitters.First(e => In(w, () => Dm.truthy(e.welded) && e.powernet != null));
        datum_powernet net = In(w, () => (datum_powernet)emitter.powernet);
        turf beside = In(w, () => Dm.each<turf>(orange(1, emitter)).First(t => Clear(t)));
        obj_item_card_id card = null;
        In(w, () =>
        {
            Waiting.NoSleep(me.forceMove(beside));
            card = Dm.@new<obj_item_card_id_advanced_debug>(me.loc);
        });
        w.Run(Second);
        output.WriteLine($"the emitter at {In(w, () => Dm.num(emitter.x))},{In(w, () => Dm.num(emitter.y))} facing {In(w, () => emitter.dir)}, its network {In(w, () => net.avail)} J a cycle");
        var errors = new List<Exception>();
        Action<Exception> sink = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { errors.Add(e); sink?.Invoke(e); };
        ClickTarget On(atom a) => new ClickTarget(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);
        ReplicaEntity Seen() => replica.Entities.Single(e => e.Id == emitter.dm_id);

        // the bare hand turns it on (interact): the client sees tg's overlay, lime with power and orange without
        Assert.True(p.Session.Click(On(emitter), ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second + 1);
        Assert.True(In(w, () => Dm.truthy(emitter.active)), "the emitter is on");
        bool powered = In(w, () => Dm.truthy(emitter.powered));
        List<(string State, uint Rgba)> overlays = OverlaysOf(replica, Seen());
        output.WriteLine($"powered {powered}; the client sees overlays {string.Join(", ", overlays.Select(o => o.State + " #" + o.Rgba.ToString("X8")))}");
        Assert.Contains(overlays, o => o.State == "emitter_overlay" && o.Rgba == (powered ? 0x00FF00FFu : 0xFF9900FFu));

        // the engineer's ID locks the controls while it is on (togglelock); a hand cannot switch it off then
        In(w, () => Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(card)))));
        w.Run(2);
        Assert.True(p.Session.Click(On(emitter), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Equal(1f, In(w, () => emitter.locked));
        In(w, () => Waiting.NoSleep(me.dropItemToGround(card)));
        w.Run(2);
        Assert.True(p.Session.Click(On(emitter), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(emitter.active)), "the locked controls keep it on");

        // its shot (the M7 projectiles slice: ProjectileGameplayTests sees and feels it): no runtime, with or without power
        w.Run(12 * Second);
        w.World.RuntimeErrorSink = sink;
        output.WriteLine($"runtimes: {string.Join(" | ", errors.Select(e => e.Message))}");
        Assert.Empty(errors);
    }
}

