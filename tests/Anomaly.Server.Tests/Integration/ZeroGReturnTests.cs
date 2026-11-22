using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The owner's play test (2026-10-24): a player who comes back from space onto a station floor with gravity stops being weightless,
/// on the server (the capsule's driver) and in the own view (no float bob).
/// </summary>
public class ZeroGReturnTests
{
    private const int Second = 20;

    private readonly ITestOutputHelper _output;

    public ZeroGReturnTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    [MetaServerFact]
    public void Stepping_from_space_onto_a_floor_with_gravity_ends_the_weightlessness()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 21, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        int z = In(w, () => (int)me.z);

        // The Auxiliary Airlock: the dock in space (39,114), the outer door (39,118) taken away, the airlock's floor with gravity (39,119).
        (int X, int Y) space = (39, 114), floor = (39, 119);
        In(w, () =>
        {
            var d = Dm.locate<obj_machinery_door_airlock>(((turf)locate(39, 118, z)).contents);
            _output.WriteLine($"door {d.type} locked {d.locked} powered {d.hasPower()} welded {d.welded} operating {d.operating} area {((turf)d.loc).loc.type}; 117 {((turf)locate(39, 117, z)).type} in {((turf)locate(39, 117, z)).loc.type}; 119 {((turf)locate(39, 119, z)).type} in {((turf)locate(39, 119, z)).loc.type}");
            qdel(d); // its cycling and access are not the point
            return 0;
        });
        w.Run(Second);
        Assert.False(In(w, () => Dm.truthy(me.has_gravity((turf)locate(space.X, space.Y, z)))));
        Assert.True(In(w, () => Dm.truthy(me.has_gravity((turf)locate(floor.X, floor.Y, z)))));

        In(w, () => Waiting.NoSleep(me.forceMove(locate(space.X, space.Y, z))));
        w.Run(Second, _ => p.Session.CameraFrame(RealHeadlessWorld.TickSeconds));
        Assert.Equal(MotionDriver.ZeroG, w.ServerState(p).Driver);
        Assert.True(p.Session.Prediction.Weightless);

        // A lit jetpack, the stabilizer off: a push north, then the drift carries the player through the doorway onto the floor.
        ZeroGPredictionTests.SuitUp(w, p, me, out _, out long toggle, out _);
        ZeroGPredictionTests.ClickScreen(p, toggle);
        w.Run(Second, _ => p.Session.CameraFrame(RealHeadlessWorld.TickSeconds));
        p.Yaw = 0; // north
        p.Keys = MoveKeys.Forward;
        w.Run(3, _ => p.Session.CameraFrame(RealHeadlessWorld.TickSeconds));
        p.Keys = MoveKeys.None;
        for (int t = 0; t < 3 * Second && In(w, () => ((int)me.x, (int)me.y)) != floor; t++)
            w.Run(1, _ => p.Session.CameraFrame(RealHeadlessWorld.TickSeconds));
        w.Run(2 * Second, _ => p.Session.CameraFrame(RealHeadlessWorld.TickSeconds));
        _output.WriteLine($"at {In(w, () => ((int)me.x, (int)me.y))}, server {w.ServerState(p).Driver}, predicted {p.Session.Prediction.Predicted.Driver}, float {p.Session.Camera.FloatAmount}");
        Assert.True(In(w, () => Dm.truthy(me.has_gravity())));
        Assert.True(In(w, () => Dm.truthy(me.gravity_state)), "tg's cached gravity follows the turf");
        Assert.False(In(w, () => HAS_TRAIT(me, Defines.TRAIT_MOVE_FLOATING)), "the float animation and the weightless alert end");
        Assert.Equal(MotionDriver.Grounded, w.ServerState(p).Driver);
        Assert.False(p.Session.Prediction.Weightless);
        Assert.Equal(0f, p.Session.Camera.FloatAmount);
    }

    [MetaServerFact]
    public void Walking_in_through_an_external_airlock_ends_the_weightlessness()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 22, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        int z = In(w, () => (int)me.z);

        // An external airlock that is not bolted, with space on one side and a station floor with gravity on the other.
        (int X, int Y) space = default, door = default, floor = default;
        In(w, () =>
        {
            foreach (var d in Dm.each_cast<obj_machinery_door_airlock_external>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_door_airlock_external))))
            {
                if (Dm.truthy(d.locked) || d.loc is not turf t || (int)t.z != z || space != default)
                    continue;
                foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    if (locate((int)t.x - dx, (int)t.y - dy, z) is turf_open_space out_
                        && locate((int)t.x + dx, (int)t.y + dy, z) is turf_open_floor in_ && Dm.truthy(me.has_gravity(in_))
                        && !Dm.each_cast<atom>(in_.contents).Any(o => Dm.truthy(o.density)))
                    {
                        (space, door, floor) = (((int)out_.x, (int)out_.y), ((int)t.x, (int)t.y), ((int)in_.x, (int)in_.y));
                        break;
                    }
                }
            }
            return 0;
        });
        Assert.NotEqual(default, space);
        _output.WriteLine($"space {space}, door {door}, floor {floor}");

        In(w, () => Waiting.NoSleep(me.forceMove(locate(space.X, space.Y, z))));
        w.Run(Second, _ => p.Session.CameraFrame(RealHeadlessWorld.TickSeconds));
        Assert.False(In(w, () => Dm.truthy(me.gravity_state)));
        Assert.True(In(w, () => HAS_TRAIT(me, Defines.TRAIT_MOVE_FLOATING)), "tg floats a mob without gravity");
        long alertId = In(w, () => ((atom)me.alerts[Defines.ALERT_GRAVITY]).dm_id);
        Assert.Contains(p.Session.Replica.Screen.Values, e => e.Id == alertId);
        Assert.Equal(long.MaxValue, p.Session.Replica.Entities.Single(e => e.Id == me.dm_id).Animation?.EndTick(Second));

        // Into the door (the walls beside it are something to push off): it opens, the player goes through onto the floor.
        (int dx, int dy) = (Math.Sign(floor.X - space.X), Math.Sign(floor.Y - space.Y));
        p.Yaw = Math.Atan2(-dx, dy);
        p.Keys = MoveKeys.Forward;
        for (int t = 0; t < 10 * Second && In(w, () => ((int)me.x, (int)me.y)) != floor; t++)
            w.Run(1, _ => p.Session.CameraFrame(RealHeadlessWorld.TickSeconds));
        // DV357: the open door vents the station's air into space, a wind that would carry a player standing still back out: keep leaning in
        w.Run(Second, _ => p.Session.CameraFrame(RealHeadlessWorld.TickSeconds));
        p.Keys = MoveKeys.None;
        _output.WriteLine($"at {In(w, () => ((int)me.x, (int)me.y))}, gravity_state {In(w, () => me.gravity_state)}, has_gravity {In(w, () => me.has_gravity())}, server {w.ServerState(p).Driver}");
        Assert.True(In(w, () => Dm.truthy(me.has_gravity())), "inside the station");
        Assert.True(In(w, () => Dm.truthy(me.gravity_state)), "tg's cached gravity follows the turf");
        // What the client shows: the weightless alert (top right) leaves the screen, the body stops bobbing (DO_FLOATING_ANIM's loop).
        long alert = alertId;
        Assert.DoesNotContain(p.Session.Replica.Screen.Values, e => e.Id == alert);
        EntityAnimation anim = p.Session.Replica.Entities.Single(e => e.Id == me.dm_id).Animation;
        _output.WriteLine($"the body's animation after: {(anim == null ? "none" : anim.EndTick(Second).ToString())}");
        Assert.True(anim == null || anim.EndTick(Second) != long.MaxValue, "the body no longer floats for ever");
        Assert.False(In(w, () => HAS_TRAIT(me, Defines.TRAIT_MOVE_FLOATING)), "the float animation and the weightless alert end");
        Assert.Equal(MotionDriver.Grounded, w.ServerState(p).Driver);
    }

    // DV185: a floor in an area without gravity (tg's NO_GRAVITY: nearstation's or solars' plating outside an airlock) and the airlock's
    // turf in the station: the same turf flags and default gravity, only the area's NO_GRAVITY differs, which tg's Moved does not check.
    [MetaServerFact]
    public void Walking_from_nearstation_plating_into_the_station_ends_the_weightlessness()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 23, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        int z = In(w, () => (int)me.z);
        (int X, int Y) outside = default, inside = default;
        In(w, () =>
        {
            bool Clear(turf t) => !Dm.each_cast<atom>(t.contents).Any(o => Dm.truthy(o.density) && o is not obj_machinery_door_airlock);
            for (int x = 2; x < 254 && outside == default; x++)
            for (int y = 2; y < 254 && outside == default; y++)
            {
                if (locate(x, y, z) is not turf_open_floor a || Dm.truthy(me.has_gravity(a)) || !Clear(a))
                    continue;
                foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    if (outside == default && locate(x + dx, y + dy, z) is turf_open_floor b && Dm.truthy(me.has_gravity(b)) && Clear(b))
                        (outside, inside) = ((x, y), (x + dx, y + dy));
            }
            return 0;
        });
        Assert.NotEqual(default, outside);
        In(w, () =>
        {
            foreach (var door in Dm.each_cast<atom>(((turf)locate(inside.X, inside.Y, z)).contents).OfType<obj_machinery_door_airlock>().ToList())
            {
                door.locked = 0f; // the airlock's bolts and access are not the point: the step from its outside onto its turf is
                Waiting.Detach(door.open());
            }
            return 0;
        });
        w.Run(3 * Second);
        _output.WriteLine($"{outside} in {In(w, () => ((turf)locate(outside.X, outside.Y, z)).loc.type.Name)} -> {inside} in {In(w, () => ((turf)locate(inside.X, inside.Y, z)).loc.type.Name)}");

        In(w, () => Waiting.NoSleep(me.forceMove(locate(outside.X, outside.Y, z))));
        w.Run(Second);
        Assert.True(In(w, () => HAS_TRAIT(me, Defines.TRAIT_MOVE_FLOATING)), "tg floats a mob without gravity");

        // The step onto the airlock's turf; checked at once (the air of the station rushes out of the open airlock and blows the player back).
        p.Yaw = Math.Atan2(-(inside.X - outside.X), inside.Y - outside.Y);
        p.Keys = MoveKeys.Forward;
        for (int t = 0; t < 3 * Second && In(w, () => ((int)me.x, (int)me.y)) != inside; t++)
            w.Run(1);
        p.Keys = MoveKeys.None;
        Assert.Equal(inside, In(w, () => ((int)me.x, (int)me.y)));
        Assert.True(In(w, () => Dm.truthy(me.gravity_state)), "tg's cached gravity follows the area");
        Assert.False(In(w, () => HAS_TRAIT(me, Defines.TRAIT_MOVE_FLOATING)), "the float animation and the weightless alert end");
        Assert.Null(In(w, () => me.alerts[Defines.ALERT_GRAVITY]));
    }
}
