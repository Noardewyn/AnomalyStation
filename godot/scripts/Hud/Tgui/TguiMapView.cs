using Godot;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client.Hud.Tgui;

/// <summary>
/// M10 slice D (D268): tgui-core's ByondUi map as a second 3D view: a SubViewport sharing the game's world, its camera placed by
/// <see cref="CameraViewPose"/> at the entity <see cref="From"/> names (the camera console's active camera, which the server's interest
/// sends with the turfs it sees). With no camera, or one the client does not hold, it shows tg's static as a dark screen.
/// </summary>
public partial class TguiMapView : SubViewportContainer
{
    private readonly Func<ReplicaWorld> _replica;
    private readonly SubViewport _viewport;
    private readonly Camera3D _camera;
    private readonly ColorRect _static;

    /// <summary>The REF of the atom the view is drawn from (<c>"[0x1f]"</c>), null for static.</summary>
    public string From { get; set; }

    public TguiMapView(Func<ReplicaWorld> replica)
    {
        _replica = replica;
        Stretch = true;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        CustomMinimumSize = new Vector2(320, 240);
        MouseFilter = MouseFilterEnum.Ignore;
        _viewport = new SubViewport { HandleInputLocally = false, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        _camera = new Camera3D { Fov = 70f, Near = 0.05f, Far = 60f, Current = true };
        _viewport.AddChild(_camera);
        AddChild(_viewport);
        _static = new ColorRect { Color = new Color(0.07f, 0.08f, 0.09f), MouseFilter = MouseFilterEnum.Ignore };
        _static.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_static);
    }

    public override void _Process(double delta)
    {
        long id = CameraViewPose.IdOfRef(From);
        ReplicaWorld replica = _replica();
        if (id == 0 || replica == null || !replica.TryGet(id, out ReplicaEntity seen))
        {
            _static.Visible = true;
            _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
            return;
        }
        _static.Visible = false;
        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        (float x, float y, float z, float yaw, float pitch) = CameraViewPose.Of(seen.State.X, seen.State.Y, seen.State.Z, seen.State.Dir);
        _camera.Position = new Vector3(x, y, z);
        _camera.Rotation = new Vector3(pitch, yaw, 0f);
    }
}
