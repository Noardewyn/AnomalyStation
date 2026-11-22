using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Replication;

/// <summary>The replicator of protocol version 3 (docs/ai/specs/replication.md section 13): faces, opacity, base light, starlight, turf types, lights, the entity threshold.</summary>
public class ReplicatorV3Tests
{
    private static mob NewMob(V2Run run, int x, int y) => run.In(() => Dm.@new<mob>(run.Turf(x, y)));

    // A directional window in tg's shape: an obj with ON_BORDER_1, dense, facing a direction.
    private static obj NewBorder(V2Run run, int x, int y, float dir)
    {
        return run.In(() =>
        {
            var o = Dm.@new<obj>(run.Turf(x, y));
            o.flags_1 = (o.flags_1 ?? 0) + ON_BORDER_1;
            o.density = 1;
            o.dir = dir;
            return o;
        });
    }

    /// <summary>The replica's cells against the world's raw predicates (faces, opacity, starlight, base light, turf type).</summary>
    private static List<string> V3Differences(ReplicaWorld replica, World world)
    {
        var differences = new List<string>();
        for (int y = 1; y <= replica.Map.Height; y++)
        {
            for (int x = 1; x <= replica.Map.Width; x++)
            {
                TileEdges faces = LocomotionSeam.FacesOf(world, x, y, 1);
                if (replica.FacesOf(x, y, 1) != faces) differences.Add($"cell {x},{y} faces: replica {replica.FacesOf(x, y, 1)}, world {faces}");
                if (replica.IsOpaque(x, y, 1) != CellState.IsOpaque(world, x, y, 1)) differences.Add($"cell {x},{y} opaque: replica {replica.IsOpaque(x, y, 1)}");
                if (replica.IsStarlit(x, y, 1) != CellState.IsStarlit(world, x, y, 1)) differences.Add($"cell {x},{y} starlit: replica {replica.IsStarlit(x, y, 1)}");
                uint baseLight = CellState.BaseLightOf(world, x, y, 1);
                int index = replica.BaseLightIndexAt(x, y, 1);
                uint have = index == 0 ? 0 : replica.BaseLights[index - 1].Rgba;
                if (have != baseLight) differences.Add($"cell {x},{y} base light: replica {have:X8}, world {baseLight:X8}");
                string type = MapView.TurfAt(world, x, y, 1)?.GetType().Name;
                if (replica.TurfTypeAt(x, y, 1) == null) differences.Add($"cell {x},{y} has no turf type");
                else if (type != null && !replica.TurfTypeAt(x, y, 1).Contains(type.Replace("turf_", "").Replace('_', '/'), StringComparison.Ordinal) && DmTypes.PathOf(MapView.TurfAt(world, x, y, 1).GetType()) != replica.TurfTypeAt(x, y, 1))
                    differences.Add($"cell {x},{y} turf type: replica {replica.TurfTypeAt(x, y, 1)}, world {DmTypes.PathOf(MapView.TurfAt(world, x, y, 1).GetType())}");
            }
        }

        return differences;
    }

    [Fact]
    public void ADenseBorderObjectBlocksItsFaceNotTheTileAndTheFaceFollowsItAndItsDirection()
    {
        V2Run run = V2Run.Quiet(size: 14, cellSweep: 100000);
        run.Step();
        SessionView session = run.Connect("Early", NewMob(run, 2, 2));
        Assert.Empty(V3Differences(session.Replica, run.World.World));

        obj window = NewBorder(run, 6, 6, Dm.NORTH);
        run.Step();
        Assert.Equal(TileEdges.North, session.Replica.FacesOf(6, 6, 1));
        Assert.False(session.Replica.IsSolid(6, 6, 1)); // a face, not the tile
        Assert.Equal(CellFlags.FaceNorth, session.Replica.CellFlagsAt(6, 6, 1));
        Delta delta = Wire.DecodeDelta(run.LastFrame);
        Assert.Contains(delta.CellFlagsChanged, c => c is { X: 6, Y: 6, Flags: CellFlags.FaceNorth });
        Assert.True(TileFaces.IsFaceBlocked(session.Replica.CollisionView, 6, 6, 1, MotionDir.North));
        Assert.True(TileFaces.IsFaceBlocked(session.Replica.CollisionView, 6, 7, 1, MotionDir.South)); // from the other side
        Assert.False(TileFaces.IsFaceBlocked(session.Replica.CollisionView, 6, 6, 1, MotionDir.East));

        run.In(() => window.dir = Dm.EAST);
        run.Step();
        Assert.Equal(TileEdges.East, session.Replica.FacesOf(6, 6, 1));
        run.In(() => window.dir = 9f);
        run.Step();
        Assert.Equal(TileEdges.North | TileEdges.West, session.Replica.FacesOf(6, 6, 1));
        run.In(() => Waiting.NoSleep(window.forceMove(run.Turf(8, 6))));
        run.Step();
        Assert.Equal(TileEdges.None, session.Replica.FacesOf(6, 6, 1));
        Assert.Equal(TileEdges.North | TileEdges.West, session.Replica.FacesOf(8, 6, 1));
        run.In(() => window.density = 0); // a windoor opening: density off, the face is gone (the object is polled by prefix in a real door)
        for (int i = 0; i < 80; i++) run.Step(); // the sweep finds a density change within its period
        Assert.Equal(TileEdges.None, session.Replica.FacesOf(8, 6, 1));
        run.In(() => qdel(window));
        run.Step();
        Assert.Empty(V3Differences(session.Replica, run.World.World));

        // Two windows on one tile union; a late joiner gets the faces as they are.
        NewBorder(run, 9, 9, Dm.SOUTH);
        obj second = NewBorder(run, 9, 9, Dm.WEST);
        run.Step();
        Assert.Equal(TileEdges.South | TileEdges.West, session.Replica.FacesOf(9, 9, 1));
        SessionView late = run.Connect("Late", NewMob(run, 3, 3));
        Assert.Empty(V3Differences(late.Replica, run.World.World));
        Assert.Equal(TileEdges.South | TileEdges.West, late.Replica.FacesOf(9, 9, 1));
        Assert.NotNull(second);

        // A mob with ON_BORDER_1 is no face (mobs are capsules).
        mob border = NewMob(run, 4, 4);
        run.In(() => { border.flags_1 = (border.flags_1 ?? 0) + ON_BORDER_1; border.density = 1; });
        run.Step();
        Assert.Equal(TileEdges.None, session.Replica.FacesOf(4, 4, 1));
    }

    [Fact]
    public void OpacityIsTgsRuleOfTheTurfOrAnyAtomOnIt()
    {
        V2Run run = V2Run.Quiet(size: 14, cellSweep: 100000);
        SessionView session = run.Connect("Early", NewMob(run, 2, 2));
        Assert.True(session.Replica.IsOpaque(1, 1, 1)); // the room's wall
        Assert.False(session.Replica.IsOpaque(5, 5, 1));
        Assert.True(session.Replica.IsOpaque(0, 5, 1)); // outside the map
        obj door = run.In(() => Dm.@new<obj>(run.Turf(5, 5)));
        run.Step();
        Assert.False(session.Replica.IsOpaque(5, 5, 1));
        run.In(() => door.opacity = 1); // a hint: VisibilityChanged
        run.Step();
        Assert.True(session.Replica.IsOpaque(5, 5, 1));
        Assert.Contains(Wire.DecodeDelta(run.LastFrame).CellFlagsChanged, c => c is { X: 5, Y: 5 } && (c.Flags & CellFlags.Opaque) != 0);
        run.In(() => door.opacity = 0);
        run.Step();
        Assert.False(session.Replica.IsOpaque(5, 5, 1));
        run.In(() => run.Turf(7, 7).opacity = 1);
        run.Step();
        Assert.True(session.Replica.IsOpaque(7, 7, 1));
        Assert.Empty(V3Differences(session.Replica, run.World.World));
    }

    [Fact]
    public void TheBaseLightComesFromTheAreaAndIsATableOfDistinctColoursThatGrowsInDeltas()
    {
        V2Run run = V2Run.Quiet(size: 14, cellSweep: 100000);
        SessionView session = run.Connect("Early", NewMob(run, 2, 2));
        Assert.NotEmpty(session.Replica.BaseLights); // the test room: base_lighting_alpha 255, white
        Assert.Equal(0xFFFFFFFFu, session.Replica.BaseLights[session.Replica.BaseLightIndexAt(5, 5, 1) - 1].Rgba);
        Assert.Empty(V3Differences(session.Replica, run.World.World));

        area room = run.In(() => (area)run.Turf(5, 5).loc);
        run.In(() =>
        {
            room.base_lighting_color = "#ff8000";
            room.base_lighting_alpha = 128;
        });
        run.Step();
        Delta delta = Wire.DecodeDelta(run.LastFrame);
        Assert.Single(delta.NewBaseLights);
        Assert.Equal(0xFF800080u, delta.NewBaseLights[0].Rgba);
        Assert.NotEmpty(delta.BaseLightChanged);
        Assert.Equal(0xFF800080u, session.Replica.BaseLights[session.Replica.BaseLightIndexAt(5, 5, 1) - 1].Rgba);
        run.In(() => room.base_lighting_alpha = 0);
        run.Step();
        Assert.Equal(0, session.Replica.BaseLightIndexAt(5, 5, 1)); // alpha 0: none
        Assert.Empty(V3Differences(session.Replica, run.World.World));
        SessionView late = run.Connect("Late", NewMob(run, 3, 3));
        Assert.Equal(session.Replica.BaseLights.Count, late.Replica.BaseLights.Count);
        Assert.Empty(V3Differences(late.Replica, run.World.World));
    }

    [Fact]
    public void ALightSourceIsAFieldOfTheEntityHintedByUpdateLightAndFoundByTheSweepOtherwise()
    {
        V2Run run = V2Run.Quiet(size: 14, cellSweep: 100000);
        SessionView session = run.Connect("Early", NewMob(run, 2, 2));
        var lights = new List<(long Id, LightState Light)>();
        session.Replica.LightChanged += e => lights.Add((e.Id, e.Light));
        obj lamp = run.In(() =>
        {
            var o = Dm.@new<obj>(run.Turf(6, 6));
            o.light_range = 4;
            o.light_power = 1.5f;
            o.light_color = "#ff0000";
            o.light_on = 1;
            return o;
        });
        run.Step();
        Assert.True(session.Replica.TryGet(lamp.dm_id, out ReplicaEntity replicaLamp));
        Assert.True(replicaLamp.HasLight);
        LightState light = replicaLamp.Light;
        Assert.Equal(4f, light.Range);
        Assert.Equal(1.5f, light.Power);
        Assert.Equal(0xFF0000FFu, light.Rgba);
        Assert.True(light.On);
        Assert.Equal(360f, light.Angle);
        Assert.Equal((byte)COMPLEX_LIGHT, light.System);
        Assert.Contains(lights, l => l.Id == lamp.dm_id && l.Light == light);

        // tg's update_light() as the presentation call: found in the tick it happens, with no sweep.
        lights.Clear();
        run.In(() =>
        {
            lamp.light_on = 0;
            PresentationSinks.Of(run.World.World).Appearance.LightChanged(lamp);
        });
        run.Step();
        Assert.False(replicaLamp.Light.On);
        Assert.Single(lights);
        Assert.Contains(Wire.DecodeDelta(run.LastFrame).Changed, c => c.Id == lamp.dm_id && c.Mask == NetField.Light);
        Assert.Equal(0, run.Replicator.LastDetail.SweepCatches); // the hint found it, not the sweep

        // Range 0 or power 0 is no light; the field goes to none and the event fires with it.
        lights.Clear();
        run.In(() =>
        {
            lamp.light_range = 0;
            PresentationSinks.Of(run.World.World).Appearance.LightChanged(lamp);
        });
        run.Step();
        Assert.False(replicaLamp.HasLight);
        Assert.Single(lights);
        Assert.True(lights[0].Light.IsNone);

        // No hint at all: the sweep finds it.
        run.In(() =>
        {
            lamp.light_range = 3;
            lamp.light_power = -1;
            lamp.light_angle = 90;
            lamp.light_dir = Dm.EAST;
        });
        for (int i = 0; i < 80; i++) run.Step();
        Assert.True(replicaLamp.HasLight);
        Assert.Equal(-1f, replicaLamp.Light.Power);
        Assert.Equal(90f, replicaLamp.Light.Angle);
        Assert.Equal((byte)4, replicaLamp.Light.Dir);

        // A joiner gets the Snapshot with the light; a despawn clears it and raises the event.
        SessionView late = run.Connect("Late", NewMob(run, 3, 3));
        Assert.True(late.Replica.TryGet(lamp.dm_id, out ReplicaEntity lateLamp));
        Assert.Equal(replicaLamp.Light, lateLamp.Light);
        lights.Clear();
        run.In(() => qdel(lamp));
        run.Step();
        Assert.Contains(lights, l => l.Id == lamp.dm_id && l.Light.IsNone);
    }

    [Fact]
    public void TheEntityRuleKeepsMoviesBelowTheThresholdAndDropsTheHiddenOnes()
    {
        V2Run run = V2Run.Quiet(size: 14, cellSweep: 100000);
        SessionView session = run.Connect("Early", NewMob(run, 2, 2));
        obj living = run.In(() => { var o = Dm.@new<obj>(run.Turf(5, 5)); o.invisibility = ReplicatedFields.MaxEntityInvisibility; return o; });
        obj pipe = run.In(() => { var o = Dm.@new<obj>(run.Turf(6, 5)); o.invisibility = INVISIBILITY_MAXIMUM; return o; });
        obj landmark = run.In(() => { var o = Dm.@new<obj>(run.Turf(7, 5)); o.invisibility = INVISIBILITY_ABSTRACT; return o; });
        run.Step();
        Assert.True(session.Replica.TryGet(living.dm_id, out _)); // 25: seen by every living mob
        Assert.False(session.Replica.TryGet(pipe.dm_id, out _)); // 100: undertile pipes and cables
        Assert.False(session.Replica.TryGet(landmark.dm_id, out _));
        run.In(() => living.invisibility = ReplicatedFields.MaxEntityInvisibility + 1);
        run.Step();
        Assert.False(session.Replica.TryGet(living.dm_id, out _));
        run.In(() => pipe.invisibility = 0);
        run.Step();
        Assert.True(session.Replica.TryGet(pipe.dm_id, out _));
    }

    [Fact]
    public void ATurfReplacedAfterTheJoinIsATurfTypeChangeThatExtendsTheTable()
    {
        V2Run run = V2Run.Quiet(size: 14, cellSweep: 100000);
        SessionView session = run.Connect("Early", NewMob(run, 2, 2));
        int types = session.Replica.TurfTypes.Count;
        string before = session.Replica.TurfTypeAt(6, 6, 1);
        run.In(() => Dm.@new<turf_open_space>(run.Turf(6, 6)));
        run.Step();
        string after = session.Replica.TurfTypeAt(6, 6, 1);
        Assert.NotEqual(before, after);
        Assert.Equal(DmTypes.PathOf(typeof(turf_open_space)), after);
        Delta delta = Wire.DecodeDelta(run.LastFrame);
        Assert.Contains(delta.TurfTypeChanged, c => c is { X: 6, Y: 6 });
        Assert.True(session.Replica.TurfTypes.Count >= types);
        SessionView late = run.Connect("Late", NewMob(run, 3, 3));
        Assert.Equal(after, late.Replica.TurfTypeAt(6, 6, 1));
        Assert.Equal(session.Replica.TurfTypes, late.Replica.TurfTypes);
        // Starlight: a space cell next to the room's floor, once its light is on (tg's enable_starlight), is a flagged cell.
        run.In(() =>
        {
            var space = (turf_open_space)run.Turf(6, 6);
            space.light_on = 1;
            PresentationSinks.Of(run.World.World).Appearance.LightChanged(space);
        });
        run.Step();
        Assert.True(session.Replica.IsStarlit(6, 6, 1));
        Assert.Equal(2f, session.Replica.Starlight.Range);
        Assert.Equal(-0.5f, session.Replica.Starlight.Height);
    }

    [Fact]
    public void TheWholeV3MapSurvivesTheWireRoundTripAndTheJoinIsLossless()
    {
        V2Run run = V2Run.Quiet(size: 16, cellSweep: 100000);
        NewBorder(run, 5, 5, Dm.NORTH);
        NewBorder(run, 6, 6, 10f);
        run.Step();
        MapGrid grid = run.Replicator.ReadMapGrid();
        byte[] bytes = Codec.Encode(grid);
        Assert.True(Codec.TryDecode(bytes, out MapGrid decoded, out ProtocolError error), error.ToString());
        Assert.Equal(grid.FlagRuns, decoded.FlagRuns);
        Assert.Equal(grid.BaseLights, decoded.BaseLights);
        Assert.Equal(grid.BaseLightRuns, decoded.BaseLightRuns);
        Assert.Equal(grid.Starlight, decoded.Starlight);
        Assert.Equal(grid.ExpandFlags(), decoded.ExpandFlags());
        Assert.NotEmpty(grid.FlagRuns);
    }
}
