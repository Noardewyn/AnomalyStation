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
/// The M7 projectiles slice (D165) through the real server on MetaStation's engine room: an engineer turns on a welded engine
/// emitter by hand; powered, it shoots (fire_beam, emitter.dm:252): the client hears emitter.ogg and sees the hitscan beam's tracer
/// (/obj/effect/projectile/tracer/laser/emitter, turned and scaled along the shot), its muzzle flash and its impact for a third of a
/// second; a crewman standing in the line of fire is burnt by it (30 BURN through his laser armour). No new runtime.
/// </summary>
public class ProjectileGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const string EffectPath = "/obj/effect/projectile/";

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

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    [MetaServerFact]
    public void An_engineer_turns_on_a_welded_emitter_and_its_beam_is_seen_heard_and_burns_a_man_in_its_way()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 83, TimeoutSteps = 14000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), seed: 202), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        w.Run(2 * Second);

        // a welded engine emitter on a network, with two clear tiles in front of it (the man stands on the second)
        List<obj_machinery_power_emitter> emitters = In(w, () => Dm.each<obj_machinery_power_emitter>(Dm.world).ToList());
        obj_machinery_power_emitter emitter = emitters.First(e => In(w, () => Dm.truthy(e.welded) && e.powernet != null
            && Clear(get_step(e, e.dir)) && Clear(get_step(get_step(e, e.dir), e.dir))));
        datum_powernet net = In(w, () => (datum_powernet)emitter.powernet);
        turf front = In(w, () => get_step(get_step(emitter, emitter.dir), emitter.dir));
        turf beside = In(w, () => Dm.each<turf>(orange(1, emitter)).First(t => Clear(t) && get_dist(t, front) > 1 && t != get_step(emitter, emitter.dir)));
        mob_living_carbon_human victim = null;
        In(w, () =>
        {
            Waiting.NoSleep(me.forceMove(beside));
            victim = Dm.@new<mob_living_carbon_human>(front);
        });
        w.Run(Second);
        output.WriteLine($"the emitter at {In(w, () => Dm.num(emitter.x))},{In(w, () => Dm.num(emitter.y))} facing {In(w, () => emitter.dir)}, its network {In(w, () => net.avail)} J a cycle; the man at {In(w, () => Dm.num(front.x))},{In(w, () => Dm.num(front.y))}");
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        float burn0 = In(w, () => Dm.num(victim.get_fire_loss()));

        // the bare hand turns it on (interact); the engine's network carries power at round start or the test gives it some
        ClickTarget On(atom a) => new ClickTarget(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);
        Assert.True(p.Session.Click(On(emitter), ClickButton.Left, ClickModifiers.None));
        w.Run(2);
        Assert.True(In(w, () => Dm.truthy(emitter.active)), "the emitter is on");

        // its first shot comes once its ten seconds since the last have passed; watch the client for the beam's effects
        var seen = new Dictionary<long, (string Type, float[] Transform, int X, int Y)>();
        for (int i = 0; i < 16 * Second && !seen.Values.Any(s => s.Type.Contains("/tracer/", StringComparison.Ordinal)); i++)
        {
            if (i % (2 * Second) == 0 && !In(w, () => Dm.truthy(emitter.powered)))
                In(w, () => net.newavail = Math.Max(net.newavail, 1_000_000)); // the engine's network without a running engine: a test's supply
            w.Run(1);
            foreach (ReplicaEntity e in replica.Entities.Where(e => e.TypePath != null && e.TypePath.StartsWith(EffectPath, StringComparison.Ordinal)))
            {
                replica.TryGetAppearance(e.State.Appearance, out AppearanceEntry look);
                seen[e.Id] = (e.TypePath, look?.Transform, e.State.X, e.State.Y);
            }
        }
        output.WriteLine($"powered {In(w, () => Dm.truthy(emitter.powered))}; the client saw {string.Join(", ", seen.Values.Select(s => s.Type + "@" + s.X + "," + s.Y))}");
        Assert.Contains(seen.Values, s => s.Type == "/obj/effect/projectile/tracer/laser/emitter");
        Assert.Contains(seen.Values, s => s.Type == "/obj/effect/projectile/muzzle/laser/emitter");
        Assert.Contains(seen.Values, s => s.Type == "/obj/effect/projectile/impact/laser/emitter");
        // the tracer is laid along the emitter's facing (its transform's north axis turned by dir2angle)
        (string _, float[] t, int tx, int ty) = seen.Values.First(s => s.Type.Contains("/tracer/", StringComparison.Ordinal));
        Assert.NotNull(t);
        float angle = In(w, () => Dm.num(dir2angle(emitter.dir)));
        Assert.InRange(t[1] * MathF.Cos(angle * MathF.PI / 180) - t[4] * MathF.Sin(angle * MathF.PI / 180), -0.05f, 0.05f);
        Assert.Contains(heard, e => e.File == "sound/items/weapons/emitter.ogg");

        // the man in front of it is burnt (30 BURN through his armour; he wears what he was made with)
        float burn = In(w, () => Dm.num(victim.get_fire_loss())) - burn0;
        output.WriteLine($"the man took {burn} burn");
        Assert.True(burn > 0, "the beam burns the man in its way");
        Assert.True(burn <= 30.01f);
        // the effects go with their 0.3 s
        w.Run(Second);
        Assert.DoesNotContain(replica.Entities, e => e.TypePath != null && e.TypePath.StartsWith(EffectPath, StringComparison.Ordinal) && seen.ContainsKey(e.Id));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount); // nothing the shot did ran into a runtime
    }
}
