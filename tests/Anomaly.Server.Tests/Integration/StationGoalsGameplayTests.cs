using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
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
/// Gaps R1 (the station goals) on MetaStation through the real server and a client's own stack: an engineer by an outer window, with a
/// satellite control console beside him and a meteor shield satellite in space outside, opens tg's SatelliteControl window with an empty
/// hand, ticks the satellite on, and sees it switch on (sat_active) and the station shield's coverage rise. A meteor flying at the station
/// past it is shot down by the satellite's beam before it gets there.
/// </summary>
public class StationGoalsGameplayTests(ITestOutputHelper output)
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

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    private static bool Clear(turf t) => t is turf_open_floor && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    private static string IconOf(ReplicaWorld replica, atom a)
    {
        ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
        return seen != null && replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? replica.ResolveString(look.IconState) : null;
    }

    [MetaServerFact]
    public void An_engineer_switches_on_a_meteor_shield_satellite_from_its_console_and_it_shoots_down_a_meteor()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 131, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        // a station floor by the hull with open space east of it: a row of 20 space tiles past the hull's tile
        (turf stand, turf consoleSpot) = In(w, () =>
        {
            foreach (turf_open_floor floor in Dm.each<turf_open_floor>(world))
            {
                if (floor.z != 1 || get_area(floor) is not area_station || !Clear(floor))
                    continue;
                if (!Enumerable.Range(2, 20).All(i => locate(floor.x + i, floor.y, 1) is turf_open_space))
                    continue;
                foreach (float dir in new float[] { WEST, NORTH, SOUTH })
                {
                    if (get_step(floor, dir) is turf t && Clear(t) && get_area(t) is area_station)
                        return ((turf)floor, t);
                }
            }
            return (null, null);
        });
        Assert.NotNull(stand);
        output.WriteLine($"the engineer at {stand.x},{stand.y}; the console at {consoleSpot.x},{consoleSpot.y}");

        // the station shield goal (made at the round start when the round picks it) and its parts: the console and a satellite outside
        var (goal, console, sat) = In(w, () =>
        {
            var g = SSstation.get_station_goal(typeof(datum_station_goal_station_shield)) as datum_station_goal_station_shield
                ?? Dm.@new<datum_station_goal_station_shield>();
            var c = Dm.@new<obj_machinery_computer_sat_control>(consoleSpot);
            var s = Dm.@new<obj_machinery_satellite_meteor_shield>(locate(stand.x + 3, stand.y, 1));
            Waiting.NoSleep(me.forceMove(stand));
            return (g, c, s);
        });
        w.Run(2 * Second);
        Assert.Equal("sat_inactive", IconOf(replica, sat));

        // the console's window with an empty hand: the satellite, off, and the shield's coverage
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, console.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("SatelliteControl", window.Interface);
        UiNode content = Shown(windows, replica, window.Window);
        string id = In(w, () => Dm.str(sat.id));
        UiNode tick = content.Walk().Single(n => n.Key == "satellite:" + id);
        Assert.Equal(("#" + id + " M-SHIELD", false), (tick.Text, tick.Checkbox));
        Assert.Contains(content.Walk(), n => n.Key == "coverage");

        // ticked on: the satellite switches on in space and bobs, the client sees it lit, and the coverage rises
        tick.OnClick();
        w.Run(2 * Second);
        Assert.Equal(1f, In(w, () => Dm.num(sat.active)));
        Assert.Equal("sat_active", IconOf(replica, sat));
        content = Shown(windows, replica, window.Window);
        Assert.True(content.Walk().Single(n => n.Key == "satellite:" + id).Checkbox);
        float coverage = In(w, () => Dm.num(goal.get_coverage()));
        output.WriteLine($"the shield covers {coverage} tiles");
        Assert.True(coverage > 100, "coverage " + coverage);
        Assert.True(content.Walk().Single(n => n.Key == "coverage").Value > 0.2);

        // a meteor from deep space at the station, along the row: the satellite shoots it down on its way
        var meteor = In(w, () => Dm.@new<obj_effect_meteor_medium>(locate(stand.x + 21, stand.y, 1), stand));
        float startX = In(w, () => meteor.x), lastX = startX, satX = In(w, () => sat.x);
        bool beamed = false;
        for (int i = 0; i < 20 * Second && !In(w, () => QDELETED(meteor)); i++)
        {
            lastX = In(w, () => meteor.x);
            w.Run(1);
            beamed |= In(w, () => Dm.each<datum_beam>().Any(b => b.icon_state == "sat_beam"));
        }
        Assert.True(In(w, () => QDELETED(meteor)), "the meteor is shot down");
        output.WriteLine($"the meteor from x {startX} was shot down at x {lastX}; the satellite at x {satX}");
        Assert.True(beamed, "by the satellite's beam");
        Assert.InRange(lastX, satX + 1, satX + 15); // in its range, before it reached the satellite
        Assert.False(In(w, () => QDELETED(sat)));
        Assert.True(In(w, () => stand is turf_open_floor && !QDELETED(me) && me.stat != DEAD), "the engineer is unharmed");

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
