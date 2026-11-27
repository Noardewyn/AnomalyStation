using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
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
/// Lavaland's ruins and the ash walkers through the real server on MetaStation with a headless client: Gina ghosts, clicks an ash
/// walker egg by a nest on Lavaland and answers tg's prompt, and hatches beside the nest as an ash walker of its tribe; Mina, a miner
/// on Lavaland, walks from the wastes into one of the ruins the round seeded.
/// </summary>
public class LavalandRuinGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static mob MobOfKey(RealHeadlessWorld w, string key) => In(w, () => Dm.each<client>(GLOB.clients).First(c => c.key as string == key).mob);

    private static bool Walkable(turf t) => t is turf_open && t is not turf_open_lava && t is not turf_open_chasm && !Dm.truthy(t.density)
        && !Dm.each<atom_movable>(t.contents).Any(a => Dm.truthy(a.density));

    // Steers the player to (x, y) from its own predicted position, as a player would (BuckleGameplayTests).
    private static void WalkTo(RealHeadlessWorld w, RealHeadlessWorld.Player p, double x, double y, int maxTicks = 10 * 20)
    {
        for (int ticks = 0; ticks < maxTicks; ticks++)
        {
            MotionState predicted = p.Session.Prediction.Predicted;
            double dx = x - predicted.X / 256.0, dy = y - predicted.Y / 256.0;
            if (Math.Sqrt(dx * dx + dy * dy) < 0.15)
                break;
            p.Keys = MoveKeys.Forward;
            p.Yaw = Math.Atan2(-dx, dy); // Godot's yaw: 0 faces north, +90 degrees west
            w.Run(1);
        }
        p.Keys = MoveKeys.None;
        w.Run(10);
    }

    [MetaServerFact]
    public void A_ghost_takes_an_ash_walker_egg_and_hatches_beside_the_nest_as_one_of_its_tribe()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 13661, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player gina = w.Join("Gina");
        w.Run(4 * Second);
        var runtimes = new List<string>();
        Action<Exception> previous = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.ToString()); previous?.Invoke(e); };
        string Runtimes() => string.Join("\n----\n", runtimes);
        var texts = new List<string>();
        gina.Client.ServerTextReceived += t => texts.Add(t.Html);

        // the round's nest if the seeding placed one; else one on open ash of Lavaland's wastes
        obj_structure_lavaland_ash_walker nest = In(w, () =>
        {
            var seeded = Dm.each<obj_structure_lavaland_ash_walker>().FirstOrDefault();
            if (seeded != null)
                return seeded;
            int lavaland = (int)Dm.num(Dm.each(SSmapping.levels_by_trait(ZTRAIT_LAVA_RUINS)).First());
            turf site = Dm.each<turf>(block(40, 40, lavaland, 200, 200, lavaland))
                .First(t => t.loc is area_lavaland_surface_outdoors && Dm.each<turf>(range(1, t)).All(Walkable));
            return Dm.@new<obj_structure_lavaland_ash_walker>(site);
        });
        output.WriteLine("the nest at " + In(w, () => $"{nest.x},{nest.y},{nest.z}"));
        w.Run(5 * Second); // its first eggs
        var egg = In(w, () => Dm.each<obj_effect_mob_spawn_ghost_role_human_ash_walker>().First(e => e.team == nest.ashies));

        Do(w, () => ((mob_living)w.MobOf(gina)).ghostize(TRUE));
        w.Run(2 * Second);
        var ghost = Assert.IsType<mob_dead_observer>(MobOfKey(w, "Gina"));
        Do(w, () => Waiting.NoSleep(ghost.forceMove(get_step(egg.eggshell, SOUTH) ?? get_turf(egg.eggshell))));
        w.Run(Second);

        // a ghost's click on the egg's shell asks (tg's attack_ghost, passed on to the egg); Yes
        long shell = In(w, () => egg.eggshell.dm_id);
        Assert.True(gina.Session.Click(new ClickTarget(TargetKind.Entity, shell, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second);
        UiWindowState prompt = gina.Session.Replica.UiWindows.Values.Single(v => v.Interface == "AlertModal");
        Assert.True(gina.Session.UiAct(prompt.Window, "choose", UiValue.ObjectOf(("choice", UiValue.Of("Yes")))));
        w.Run(3 * Second);

        var walker = Assert.IsType<mob_living_carbon_human>(MobOfKey(w, "Gina"));
        output.WriteLine("the ash walker " + In(w, () => walker.real_name));
        Do(w, () =>
        {
            Assert.IsType<datum_species_lizard_ashwalker>(walker.dna.species);
            Assert.True(get_dist(walker, nest) <= 2, "hatched beside the nest");
            Assert.NotNull(walker.mind.has_antag_datum(typeof(datum_antagonist_ashwalker)));
            Assert.True(QDELETED(egg), "the egg hatched; runtimes: " + Runtimes());
        });
        Assert.Contains(texts, t => t.Contains("Glory to the Necropolis!"));
        Assert.True(runtimes.Count == 0, Runtimes());
    }

    [MetaServerFact]
    public void A_miner_walks_from_the_wastes_into_a_seeded_ruin()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 13662, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player mina = w.Join("Mina");
        var me = (mob_living_carbon_human)w.MobOf(mina);
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;

        // a ruin's floor tile whose neighbour is open ground of the wastes
        (turf outside, turf inside, string ruin) = In(w, () =>
        {
            foreach (obj_effect_landmark_ruin landmark in Dm.each<obj_effect_landmark_ruin>(GLOB.ruin_landmarks))
            {
                var template = landmark.ruin_template;
                foreach (turf t in Dm.each<turf>(range((int)Math.Max(template.width, template.height) / 2 + 1, landmark)))
                {
                    if (t.loc is area_lavaland_surface || !Walkable(t))
                        continue;
                    foreach (object dir in Dm.each(GLOB.cardinals))
                    {
                        turf n = get_step(t, Dm.num(dir));
                        if (n != null && n.loc is area_lavaland_surface && Walkable(n))
                            return (n, t, template.name);
                    }
                }
            }
            return ((turf)null, (turf)null, (string)null);
        });
        Assert.True(inside != null, "no ruin with a way in from the wastes");
        output.WriteLine($"into {ruin} at {inside.x},{inside.y},{inside.z} ({In(w, () => get_area(inside).GetType().Name)})");
        Do(w, () => Waiting.NoSleep(me.forceMove(outside)));
        w.Run(Second);
        WalkTo(w, mina, inside.x, inside.y);
        Do(w, () =>
        {
            Assert.Same(inside, me.loc);
            Assert.Same(get_area(inside), get_area(me));
            Assert.Equal(0f, Dm.num(me.stat)); // conscious
        });
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
