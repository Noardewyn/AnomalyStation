using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Server.Tests.M3Maps.R4;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>tg's meteors: one flies at its target and blows up against what it hits; the meteor flight event spawns them around the shuttle in transit;
/// the space dust and immovable rod events (gaps E2b) send theirs across MetaStation.</summary>
public class MeteorGameplayTests
{
    private static T In<T>(RealHeadlessWorld w, Func<T> f) { using (w.World.Enter()) return f(); }

    [MetaServerFact]
    public void A_meteor_flies_and_breaks_up_against_a_wall()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 81, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        w.Join("Alice");
        w.Run(10);
        int bootRuntimes = w.World.RuntimeErrorCount;
        obj_effect_meteor_medium meteor = null;
        (float X, float Y) start = default;
        using (w.World.Enter())
        {
            // a space tile with a wall a few tiles along the row: the meteor comes from the space side
            turf wall = Dm.each<turf_closed_wall>(Dm.world).First(t => t.z == 1 && locate(t.x - 6, t.y, 1) is turf_open_space
                && Enumerable.Range(1, 6).All(i => locate(t.x - i, t.y, 1) is turf_open_space));
            turf from = locate(wall.x - 6, wall.y, 1);
            start = (from.x, from.y);
            meteor = (obj_effect_meteor_medium)Dm.@new(typeof(obj_effect_meteor_medium), from, locate(wall.x + 20, wall.y, 1));
        }
        for (int i = 0; i < 40 && In(w, () => meteor.x == start.X); i++)
            w.Run(1);
        Assert.True(In(w, () => QDELETED(meteor) || meteor.x > start.X), "the meteor moves toward its target");
        for (int i = 0; i < 600 && !In(w, () => QDELETED(meteor)); i++)
            w.Run(1);
        Assert.True(In(w, () => QDELETED(meteor)), "it breaks up");
        Assert.True(In(w, () => !Dm.each<obj_effect_meteor>(GLOB.meteor_list).Contains(meteor)));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    [MetaServerFact]
    public void The_meteor_event_is_selectable_and_spawns_in_transit()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 82, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        using (w.World.Enter())
        {
            SSshuttle.emergency_call_time = 100;
            SSshuttle.emergency_dock_time = 100;
            SSshuttle.emergency_escape_time = 600;
            SSticker.round_start_time = Dm.num(Dm.world.time) - 12000;
            SSshuttle.emergency.event_list.Cut();
            SSshuttle.emergency.add_shuttle_event(typeof(datum_shuttle_event_simple_spawner_meteor_safe));
        }
        alice.Session.DevCommand("shuttle call");
        for (int i = 0; i < 2000 && !In(w, () => Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ESCAPE)); i++)
            w.Run(1);
        Assert.True(In(w, () => Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ESCAPE)));
        bool seen = false;
        for (int i = 0; i < 300 && !seen; i++)
        {
            w.Run(1);
            seen = In(w, () => Dm.each<obj_effect_meteor>(GLOB.meteor_list).Any(m => m.z == SSshuttle.emergency.z));
        }
        Assert.True(seen, "meteors fly past the shuttle");
        Assert.True(In(w, () => Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ESCAPE)), "the safe event leaves the shuttle flying");
    }

    [MetaServerFact]
    public void The_space_events_send_dust_and_a_rod_across_the_station()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 83, TimeoutSteps = 60000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        w.Join("Alice");
        w.Run(10);
        int bootRuntimes = w.World.RuntimeErrorCount;
        using (w.World.Enter())
        {
            foreach (datum_round_event_control control in Dm.each<datum_round_event_control>(SSevents.control)
                         .Where(c => c is datum_round_event_control_space_dust || c is datum_round_event_control_immovable_rod).ToList())
                Waiting.Detach(control.run_event());
        }
        obj_effect_immovablerod rod = null;
        bool dust = false;
        for (int i = 0; i < 400 && (rod == null || !dust); i++)
        {
            w.Run(1);
            rod ??= In(w, () => Dm.each<obj_effect_immovablerod>(Dm.world).FirstOrDefault());
            dust |= In(w, () => Dm.each<obj_effect_meteor_dust>(GLOB.meteor_list).Any());
        }
        Assert.True(dust, "the space dust event throws a dust meteor");
        Assert.NotNull(rod);
        Assert.True(In(w, () => get_turf(rod) is turf_open_space), "the rod starts in space");
        (float X, float Y) start = In(w, () => (rod.x, rod.y));
        w.Run(40);
        Assert.True(In(w, () => QDELETED(rod) || rod.x != start.X || rod.y != start.Y), "the rod flies");
        for (int i = 0; i < 6000 && !In(w, () => QDELETED(rod)); i++)
            w.Run(1);
        Assert.True(In(w, () => QDELETED(rod)), "the rod crosses the station and leaves it");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
