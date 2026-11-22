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
/// Engineering E7 (singularity containment) on MetaStation through the real server: an engineer takes two of the field generators
/// and an emitter from engineering's storage to clear floor with a powered cable under the emitter: the near generator in its line
/// of fire, the far one four tiles across (the setup's moves are the test's, as dragging them there would be). With his own clicks
/// he wrenches and welds all three to the floor (the welded emitter joins the cable's network) and turns them on by hand. The
/// generators warm up (+a1..+a3) and make the containment field between them (three tiles); the emitter's beams charge the near
/// generator (bullet_act, the +p overlays). His client sees the generators' overlays and the fields (tg's Contain_F of
/// singularity.dmi). Bob walks into the field: he is paralysed and thrown back, and they hear the field's gravhit. The engineer
/// turns the emitter off; the pair runs dry, a generator shuts down and the fields go, on the server and in the client. No new
/// runtime.
/// </summary>
public class FieldGeneratorGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const string FieldPath = "/obj/machinery/field/containment";

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

    private static bool Clear(turf t) => t is turf_open && t is not turf_open_space && !Dm.truthy(t.density)
        && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density) || a is mob || a is obj_machinery_door);

    private static ClickTarget On(atom a) => new ClickTarget(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private sealed record Layout(turf Emitter, float BeamDir, turf[] Beam, turf Near, turf[] Field, turf Far, turf Bob, float BobDir);

    // An engine's layout on clear floor of the station's level: the emitter's tile has a cable on a powered network (the emitter
    // draws first in the machines cycle, SSmachines' early queue); its beam crosses one or two clear tiles to the near generator;
    // the field runs at a right angle from it, three clear tiles, to the far one; Bob stands two clear tiles from the middle field
    // tile, off the beam's path.
    private static Layout FindLayout(int z)
    {
        for (int x = 3; x <= 250; x++)
            for (int y = 3; y <= 250; y++)
            {
                turf start = locate(x, y, z);
                if (start == null || !Clear(start) || Dm.locate<obj_structure_cable>(start) is not obj_structure_cable cable
                    || cable.powernet is not datum_powernet net || net.avail < 50_000)
                    continue;
                foreach (float dir in new float[] { EAST, NORTH, WEST, SOUTH })
                    for (int k = 2; k <= 3; k++)
                    {
                        var beam = new List<turf>();
                        turf t = start;
                        bool ok = true;
                        for (int i = 1; i <= k && ok; i++)
                        {
                            t = get_step(t, dir);
                            ok = t != null && Clear(t);
                            beam.Add(t);
                        }
                        if (!ok)
                            continue;
                        turf near = beam[^1];
                        foreach (float across in new[] { turn(dir, 90), turn(dir, -90) })
                        {
                            var field = new turf[3];
                            turf f = near;
                            for (int i = 0; i < 4 && ok; i++)
                            {
                                f = get_step(f, across);
                                ok = f != null && Clear(f);
                                if (i < 3)
                                    field[i] = f;
                            }
                            if (!ok)
                            {
                                ok = true;
                                continue;
                            }
                            foreach (float side in new[] { dir, turn(dir, 180) })
                            {
                                turf by = get_step(field[1], side), back = by == null ? null : get_step(by, side);
                                if (by != null && Clear(by) && back != null && Clear(back))
                                    return new Layout(start, dir, beam.Take(k - 1).ToArray(), near, field, f, by, turn(side, 180));
                            }
                        }
                    }
            }
        return null;
    }

    private static turf Beside(atom thing, IEnumerable<turf> avoid)
    {
        foreach (float dir in new float[] { SOUTH, NORTH, EAST, WEST, NORTHEAST, NORTHWEST, SOUTHEAST, SOUTHWEST })
        {
            turf t = get_step(thing, dir);
            if (t != null && Clear(t) && Dm.locate<obj_machinery_field_containment>(t) == null && !avoid.Contains(t))
                return t;
        }
        return null;
    }

    private static void Use(RealHeadlessWorld w, RealHeadlessWorld.Player p, mob_living_carbon_human me, string tool, atom target, int seconds, Layout at)
    {
        In(w, () =>
        {
            foreach (object held in Dm.each(me.held_items.Copy()))
                if (held is obj_item old)
                    qdel(old);
            if (tool != null)
            {
                DevCommands.Give(me, tool);
                if (me.get_active_held_item() is obj_item_weldingtool welder)
                    Waiting.NoSleep(welder.attack_self(me));
            }
            Waiting.NoSleep(me.forceMove(Beside(target, at.Beam.Concat(at.Field).Append(at.Emitter).Append(at.Near).Append(at.Far))));
        });
        w.Run(2);
        Assert.True(p.Session.Click(On(target), ClickButton.Left, ClickModifiers.None));
        w.Run(Math.Max(4, seconds * Second));
    }

    private static List<string> Overlays(ReplicaWorld replica, long id)
    {
        var list = new List<string>();
        ReplicaEntity e = replica.Entities.FirstOrDefault(x => x.Id == id);
        if (e == null || !replica.TryGetAppearance(e.State.Appearance, out AppearanceEntry root))
            return list;
        foreach (TableRef r in root.Overlays)
            if (replica.TryGetAppearance(r, out AppearanceEntry o))
                list.Add(replica.ResolveString(o.IconState));
        return list;
    }

    [MetaServerFact]
    public void Two_welded_generators_powered_by_an_emitter_make_a_field_that_repels_Bob_and_drop_it_when_their_power_runs_out()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 97, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        RealHeadlessWorld.Player q = w.Join("Bob");
        var me = (mob_living_carbon_human)w.MobOf(p);
        var bob = (mob_living_carbon_human)w.MobOf(q);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        var errors = new List<Exception>();
        Action<Exception> sink = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { errors.Add(e); sink?.Invoke(e); };

        // engineering's storage: MetaStation.dmm has four loose field generators and four loose emitters there
        int z = In(w, () => (int)me.z);
        List<obj_machinery_field_generator> gens = In(w, () => Dm.each<obj_machinery_field_generator>(Dm.world).Where(g => g.z == z).OrderBy(g => g.dm_id).ToList());
        List<obj_machinery_power_emitter> loose = In(w, () => Dm.each<obj_machinery_power_emitter>(Dm.world).Where(e => e.z == z && !Dm.truthy(e.anchored)).OrderBy(e => e.dm_id).ToList());
        output.WriteLine($"{gens.Count} field generators: {string.Join(", ", gens.Select(g => In(w, () => $"{g.x},{g.y}")))}; {loose.Count} loose emitters");
        Assert.True(gens.Count >= 2);
        Assert.NotEmpty(loose);
        obj_machinery_field_generator near = gens[0], far = gens[1];
        obj_machinery_power_emitter emitter = loose[0];
        Assert.All(gens, g => Assert.False(In(w, () => Dm.truthy(g.anchored))));

        // the station's networks have power once their SMES have run a few machines cycles
        w.Run(10 * Second);
        Layout at = In(w, () => FindLayout(z));
        Assert.NotNull(at);
        output.WriteLine($"the layout: the emitter at {In(w, () => $"{at.Emitter.x},{at.Emitter.y}")} firing {at.BeamDir} in {In(w, () => Dm.str(((area)get_area(at.Emitter)).name))}, the generators at {In(w, () => $"{at.Near.x},{at.Near.y} and {at.Far.x},{at.Far.y}")}, Bob at {In(w, () => $"{at.Bob.x},{at.Bob.y}")}");
        In(w, () =>
        {
            Waiting.NoSleep(emitter.forceMove(at.Emitter));
            Waiting.NoSleep(emitter.setDir(at.BeamDir));
            Waiting.NoSleep(near.forceMove(at.Near));
            Waiting.NoSleep(far.forceMove(at.Far));
        });
        w.Run(Second);

        // the wrench secures all three (the generators FG_SECURED); the welder welds them to the floor (two seconds each, the
        // generators FG_WELDED; the welded emitter joins the cable's network)
        foreach (atom thing in new atom[] { near, far, emitter })
            Use(w, p, me, "/obj/item/wrench", thing, 3, at);
        Assert.True(In(w, () => Dm.truthy(near.anchored)));
        Assert.Equal(1f, In(w, () => Dm.num(near.state)));
        Assert.Equal(1f, In(w, () => Dm.num(far.state)));
        foreach (atom thing in new atom[] { near, far, emitter })
            Use(w, p, me, "/obj/item/weldingtool", thing, 4, at);
        Assert.Equal(2f, In(w, () => Dm.num(near.state)));
        Assert.Equal(2f, In(w, () => Dm.num(far.state)));
        Assert.True(In(w, () => Dm.truthy(emitter.welded)), "the emitter is welded");
        Assert.NotNull(In(w, () => emitter.powernet));
        Assert.Contains(heard, e => e.File != null && e.File.StartsWith("sound/items/tools/welder", StringComparison.Ordinal));

        // an empty hand turns on the generators and the emitter
        foreach (atom thing in new atom[] { near, far, emitter })
            Use(w, p, me, null, thing, 0, at);
        w.Run(2);
        Assert.Equal(1f, In(w, () => Dm.num(near.active)));
        Assert.Equal(1f, In(w, () => Dm.num(far.active)));
        Assert.True(In(w, () => Dm.truthy(emitter.active)), "the emitter is on");
        In(w, () => Waiting.NoSleep(me.forceMove(Beside(emitter, at.Beam.Concat(at.Field).Append(at.Bob)))));

        // they warm up and make the field: three containment fields between them
        var seenA = new HashSet<string>();
        float maxPower = 0;
        for (int t = 0; t < 18 * Second; t++)
        {
            w.Run(1);
            foreach (string o in Overlays(replica, near.dm_id))
                seenA.Add(o);
            maxPower = Math.Max(maxPower, In(w, () => Dm.num(near.power)));
        }
        List<obj_machinery_field_containment> fields = In(w, () => Dm.each<obj_machinery_field_containment>(Dm.world).ToList());
        output.WriteLine($"after 18 s: {fields.Count} fields; near {In(w, () => near.power)} power, far {In(w, () => far.power)}; the client saw {string.Join(" ", seenA)}; the emitter shot {In(w, () => emitter.shot_number)} ({In(w, () => Dm.truthy(emitter.powered))} powered)");
        Assert.Equal(3, fields.Count);
        Assert.All(fields, f => Assert.Contains(In(w, () => get_turf(f)), at.Field));
        Assert.Equal(2f, In(w, () => Dm.num(near.active)));
        Assert.Contains("+a1", seenA);
        Assert.Contains("+a3", seenA);
        Assert.Contains("+on", Overlays(replica, near.dm_id));
        Assert.Contains("+on", Overlays(replica, far.dm_id));
        // the client sees the fields: tg's sprite
        foreach (obj_machinery_field_containment f in fields)
        {
            ReplicaEntity seen = replica.Entities.SingleOrDefault(e => e.Id == f.dm_id);
            Assert.NotNull(seen);
            Assert.Equal(FieldPath, seen.TypePath);
            Assert.True(replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look));
            Assert.Equal("icons/obj/machines/engine/singularity.dmi", replica.ResolveResource(look.Icon));
            Assert.Equal("Contain_F", replica.ResolveString(look.IconState));
        }

        // the emitter keeps the near generator charged (its beams' 30 each, bullet_act)
        for (int t = 0; t < 12 * Second; t++)
        {
            w.Run(1);
            maxPower = Math.Max(maxPower, In(w, () => Dm.num(near.power)));
        }
        output.WriteLine($"the near generator peaked at {maxPower} power (level {In(w, () => near.power_level)}); the emitter shot {In(w, () => emitter.shot_number)}");
        Assert.True(maxPower > 20, "the emitter's beams charge the generator");
        Assert.Contains(Overlays(replica, near.dm_id), o => o.StartsWith("+p", StringComparison.Ordinal));
        Assert.Equal(3, In(w, () => Dm.each<obj_machinery_field_containment>(Dm.world).Count()));

        // Bob walks into the middle field: it repels him (yeet_shock, bump_field)
        In(w, () => Waiting.NoSleep(bob.forceMove(at.Bob)));
        w.Run(Second);
        int bx = In(w, () => (int)bob.x), by = In(w, () => (int)bob.y);
        q.Yaw = at.BobDir == NORTH ? 0 : at.BobDir == WEST ? Math.PI / 2 : at.BobDir == SOUTH ? Math.PI : -Math.PI / 2; // Godot's yaw: 0 north, +90 degrees west
        q.Keys = MoveKeys.Forward;
        for (int t = 0; t < 2 * Second && !In(w, () => Dm.truthy(bob.IsParalyzed())); t++)
            w.Run(1);
        q.Keys = MoveKeys.None;
        w.Run(Second);
        output.WriteLine($"Bob from {bx},{by} to {In(w, () => $"{bob.x},{bob.y}")}: paralysed {In(w, () => Dm.truthy(bob.IsParalyzed()))}, burn {In(w, () => bob.get_fire_loss())}");
        Assert.True(In(w, () => Dm.truthy(bob.IsParalyzed())), "the field paralyses Bob (Paralyze(10 SECONDS))");
        Assert.True(In(w, () => get_dist(bob, at.Field[1])) > 1, "and throws him back");
        Assert.Null(In(w, () => Dm.locate<mob_living>(at.Field[1])));
        Assert.Contains(heard, e => e.File == "sound/effects/gravhit.ogg");

        // the emitter off: the pair runs dry, a generator shuts down and the fields go
        Use(w, p, me, null, emitter, 0, at);
        Assert.False(In(w, () => Dm.truthy(emitter.active)), "the emitter is off");
        int waited = 0;
        while (waited < 200 * Second && In(w, () => Dm.each<obj_machinery_field_containment>(Dm.world).Any()))
        {
            w.Run(Second);
            waited += Second;
        }
        w.Run(2);
        output.WriteLine($"the fields went after {waited / Second} s; near {In(w, () => near.active)} ({In(w, () => near.power)}), far {In(w, () => far.active)} ({In(w, () => far.power)})");
        Assert.Empty(In(w, () => Dm.each<obj_machinery_field_containment>(Dm.world).ToList()));
        Assert.True(In(w, () => Dm.num(near.active) == 0 || Dm.num(far.active) == 0), "a generator shut down");
        Assert.DoesNotContain(replica.Entities, e => e.TypePath == FieldPath);
        Assert.DoesNotContain("+on", Overlays(replica, near.dm_id));

        w.World.RuntimeErrorSink = sink;
        output.WriteLine($"runtimes: {string.Join(" | ", errors.Select(e => e.ToString()[..Math.Min(e.ToString().Length, 2500)]).Distinct())}");
        Assert.Empty(errors);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
