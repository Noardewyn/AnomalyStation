using System;
using System.Collections.Generic;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

/// <summary>
/// D301, the port's own look: the jets behind a flying shuttle's engines, and the streak a shuttle leaves at its dock or lands in (the
/// server's <c>circle_wave/orange</c> and <c>circle_wave/star_blast</c> marks, a grid over the hull's tiles, each with the flight's direction).
/// </summary>
public partial class ShuttleFx : Node3D
{
    private const string Departure = "/obj/effect/temp_visual/circle_wave/orange", Arrival = "/obj/effect/temp_visual/circle_wave/star_blast";
    private const string Engine = "/obj/machinery/power/shuttle_engine/propulsion";
    private const float JetLength = 7f, BlobRadius = 3.5f, StreakLength = 60f;
    private const double StreakSeconds = 0.8;

    private readonly Dictionary<long, MeshInstance3D> _jets = new();
    private readonly Mesh _jetMesh = new CylinderMesh { TopRadius = 0.75f, BottomRadius = 0.08f, Height = JetLength, RadialSegments = 12, Rings = 1, CapTop = false, CapBottom = false };
    private readonly Shader _jetShader = GD.Load<Shader>("res://shaders/shuttle_jet.gdshader");
    private readonly Mesh _blob = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 12, Rings = 6 };
    private double _scan;

    /// <summary>The level the viewer is on: a departure elsewhere is not drawn over it.</summary>
    public Func<int> OwnZ { get; set; }

    /// <summary>Draws the streak of a departure that has just appeared.</summary>
    public void OnSpawned(ReplicaEntity entity)
    {
        bool arriving = entity.TypePath == Arrival;
        if ((!arriving && entity.TypePath != Departure) || OwnZ == null || entity.State.Z != OwnZ())
            return;
        EntityState state = entity.State;
        Vector3 way = state.Dir switch
        {
            1 => Vector3.Forward, // NORTH is -Z
            2 => Vector3.Back,
            4 => Vector3.Right,
            _ => Vector3.Left,
        };
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            AlbedoColor = new Color(0.45f, 0.72f, 1f, 0.55f),
        };
        var node = new MeshInstance3D { Mesh = _blob, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(node);
        Vector3 start = new(TileSpace.WorldX(state.X), TileSpace.WorldY(state.Z) + 1f, TileSpace.WorldZ(state.Y));
        Vector3 stretched = new(way.X != 0 ? StreakLength : 0.6f, 0.6f, way.Z != 0 ? StreakLength : 0.6f);
        Vector3 round = Vector3.One * BlobRadius;
        // leaving: from the hull shooting ahead; arriving: from behind the hull rushing onto it
        Vector3 far = arriving ? start - way * StreakLength * 1.2f : start + way * StreakLength * 1.2f;
        node.Position = arriving ? far : start;
        node.Scale = arriving ? stretched : round;
        Godot.Tween tween = node.CreateTween().SetParallel().SetEase(arriving ? Godot.Tween.EaseType.Out : Godot.Tween.EaseType.In).SetTrans(Godot.Tween.TransitionType.Cubic);
        tween.TweenProperty(node, "scale", arriving ? round : stretched, StreakSeconds);
        tween.TweenProperty(node, "position", arriving ? start : far, StreakSeconds);
        tween.TweenProperty(material, "albedo_color:a", 0f, StreakSeconds);
        tween.Chain().TweenCallback(Callable.From(node.QueueFree));
    }

    /// <summary>The jets of the viewer's ship: each engine with hyperspace behind it, while <paramref name="flow"/> (where hyperspace streams past) is set.</summary>
    public void StepJets(Vector3? flow, ReplicaWorld replica, int z, Func<int, int, string> turfAt, double delta)
    {
        if (flow is not { } way)
        {
            foreach (MeshInstance3D jet in _jets.Values)
                jet.QueueFree();
            _jets.Clear();
            return;
        }
        if ((_scan -= delta) > 0)
            return;
        _scan = 0.5;
        var seen = new HashSet<long>();
        foreach (ReplicaEntity entity in replica.Entities)
        {
            if (!entity.TypePath.StartsWith(Engine, StringComparison.Ordinal))
                continue;
            EntityState state = entity.State;
            // only an engine that opens on hyperspace: Z is south, y north
            string behind = state.Z == z ? turfAt(state.X + (int)way.X, state.Y - (int)way.Z) : null;
            if (behind == null || !behind.StartsWith("/turf/open/space/transit", StringComparison.Ordinal))
                continue;
            seen.Add(entity.Id);
            if (!_jets.TryGetValue(entity.Id, out MeshInstance3D jet))
            {
                var material = new ShaderMaterial { Shader = _jetShader };
                material.SetShaderParameter("seed", (float)(entity.Id % 97));
                AddChild(jet = new MeshInstance3D { Mesh = _jetMesh, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
                _jets[entity.Id] = jet;
            }
            // the cone's wide top at the nozzle, its point downstream
            Vector3 up = -way;
            Vector3 side = up.Cross(Vector3.Up).Normalized();
            Vector3 cell = new(TileSpace.WorldX(state.X), TileSpace.WorldY(z) + 1f, TileSpace.WorldZ(state.Y));
            jet.Transform = new Transform3D(new Basis(side, up, side.Cross(up)), cell + way * (TileSpace.TileSize / 2f + JetLength / 2f));
        }
        foreach (long id in new List<long>(_jets.Keys))
        {
            if (!seen.Contains(id))
            {
                _jets[id].QueueFree();
                _jets.Remove(id);
            }
        }
    }
}
