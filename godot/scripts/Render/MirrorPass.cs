using System.Collections.Generic;
using Godot;
using Anomaly.Client.Light;
using Anomaly.Client.Shared;

namespace Anomaly.Client.Render;

/// <summary>
/// The mirrors' reflection (the owner, 2026-11-13): the nearest whole mirror turned to the eye shows the room and, in first person, the own
/// body. A second camera stands where the eye is behind the mirror's plane and looks out through the glass's outline (an off-axis frustum
/// whose near plane is the glass), and its picture is laid on a quad over tg's mirror picture (mirror_surface.gdshader). The screen-space
/// reflections cannot do this: a mirror faced head-on reflects what is behind the viewer, which is not on the screen.
/// shortcut: one mirror at a time is live, and the other mobs' pictures keep the side the eye sees; several live mirrors need a viewport each.
/// </summary>
public partial class MirrorPass : Node3D
{
    /// <summary>The glass of tg's mirror picture (its <c>mirror_mask</c>: 12 by 17 pixels of the 32-pixel tile), in metres.</summary>
    public static readonly Vector2 GlassSize = new(12f / 32f * TileSpace.TileSize, 17f / 32f * TileSpace.TileSize);

    /// <summary>How far a mirror is live from, in metres, and how often the mirrors near the eye are looked up, in seconds.</summary>
    public const float Range = 10f;
    private const double ScanEvery = 0.5;

    private SubViewport _viewport;
    private Camera3D _camera;
    private MeshInstance3D _glass;
    private readonly List<(Vector3 Centre, Vector3 Normal)> _near = new();
    private double _sinceScan = ScanEvery;

    public override void _Ready()
    {
        _viewport = new SubViewport
        {
            Name = "MirrorView", Size = new Vector2I(264, 374), HandleInputLocally = false, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
        };
        AddChild(_viewport);
        _camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Frustum, KeepAspect = Camera3D.KeepAspectEnum.Height, Size = GlassSize.Y, Far = 60f, Current = true,
            CullMask = 0xFFFFF & ~(LightLayer.MainViewLayer | LightLayer.HeldLayer | LightLayer.MirrorHiddenLayer),
        };
        _viewport.AddChild(_camera);
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/mirror_surface.gdshader") };
        material.SetShaderParameter("view", _viewport.GetTexture());
        _glass = new MeshInstance3D
        {
            Name = "MirrorGlass", Mesh = new QuadMesh { Size = GlassSize }, MaterialOverride = material, Visible = false, TopLevel = true,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Layers = LightLayer.MainViewLayer,
        };
        AddChild(_glass);
    }

    /// <summary>
    /// Makes the nearest mirror the eye is before live, or none. Returns where its camera stands in tile coordinates (the side of the own body it
    /// sees), null with no live mirror.
    /// </summary>
    public Vector2? Update(Camera3D eye, MapRenderer map, double delta)
    {
        if (eye == null || map == null)
            return Idle();
        Vector3 from = eye.GlobalPosition;
        _sinceScan += delta;
        if (_sinceScan >= ScanEvery)
        {
            _sinceScan = 0;
            map.MirrorsNear(from, Range, _near);
        }
        Vector3 forward = -eye.GlobalBasis.Z;
        float best = float.MaxValue;
        Vector3 centre = default, normal = default;
        foreach ((Vector3 c, Vector3 n) in _near)
        {
            float before = (from - c).Dot(n), distance = from.DistanceTo(c);
            // the eye is before the glass, and the glass is before the eye
            if (before < 0.06f || distance > Range || (c - from).Dot(forward) < 0f || distance >= best)
                continue;
            (best, centre, normal) = (distance, c, n);
        }
        if (best == float.MaxValue)
            return Idle();
        float near = (from - centre).Dot(normal);
        Vector3 behind = from - 2f * near * normal; // the eye's image in the mirror
        var basis = Basis.LookingAt(normal, Vector3.Up);
        _camera.GlobalTransform = new Transform3D(basis, behind);
        Vector3 local = basis.Inverse() * (centre - behind);
        _camera.Near = near;
        _camera.FrustumOffset = new Vector2(local.X, local.Y);
        _glass.GlobalTransform = new Transform3D(Basis.LookingAt(-normal, Vector3.Up), centre);
        _glass.Visible = true;
        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        return new Vector2(behind.X / TileSpace.TileSize + 0.5f, -behind.Z / TileSpace.TileSize + 0.5f);
    }

    private Vector2? Idle()
    {
        if (_glass.Visible)
        {
            _glass.Visible = false;
            _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        }
        return null;
    }
}
