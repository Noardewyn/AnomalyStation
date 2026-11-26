using System;
using Godot;
using Anomaly.Client.Shared;

namespace Anomaly.Client;

/// <summary>
/// The camera (D101, D73, D41: a replaceable layer). First person is the default: the camera sits at the own capsule's
/// predicted position at the height the camera layer says (<see cref="CameraLayer"/>: 1.2 m standing, about 0.5 m lying, with
/// the head bob and the shake as options, D70) and follows it; mouse look (yaw and pitch) is local and the yaw goes to the server
/// in every input frame (M2). F1 switches to a free fly camera (inspection and screenshots) or the third-person orbit, a play view (DV53).
/// <para>
/// Yaw is Godot's: radians about +Y, counter-clockwise seen from above; 0 looks north (-Z), +90 degrees looks west. The
/// pitch is positive looking up, limited to <see cref="LookDown.MaxUpDegrees"/> up and <see cref="LookDown.MaxDownDegrees"/> down: past straight
/// down it is one continuous turn about the camera's own right axis, so the screen's right stays the player's right (slice T5a).
/// </para>
/// </summary>
public partial class CameraRig : Node3D
{
    private const float FlySpeed = 9f;
    private const float FastFactor = 3.5f;

    private Camera3D _camera;
    private Vector3 _freePosition;

    /// <summary>Radians of view turn per pixel of mouse movement.</summary>
    public float MouseSensitivity { get; set; } = 0.0025f;

    /// <summary>The vertical field of view in degrees (D70: adjustable).</summary>
    public float FovDegrees
    {
        get => _camera != null ? _camera.Fov : 75f;
        set
        {
            if (_camera != null && !Mathf.IsEqualApprox(_camera.Fov, value))
                _camera.Fov = value;
        }
    }

    public CameraMode Mode { get; private set; } = CameraMode.FirstPerson;

    public float Yaw { get; set; }

    public float Pitch { get; set; }

    /// <summary>Metres behind the own capsule in orbit mode.</summary>
    public float OrbitDistance { get; set; } = 5.0f;

    /// <summary>
    /// The third-person view (the owner, 2026-10-16: orbit is a play mode): it looks at a point this far ahead of the own mob along
    /// the view, so the crosshair aims at the floor in front of the mob, not at its head.
    /// </summary>
    public const float OrbitAimLead = 1.5f;

    /// <summary>
    /// The owner (2026-10-16): looking straight down the mob stands in the middle of the view, at any other pitch the crosshair is above it. The lead is whole
    /// down to this pitch (radians, about 45 degrees) and fades smoothly to nothing at the steepest the orbit looks (<see cref="OrbitMaxDown"/>).
    /// </summary>
    private const float OrbitLeadFadeFrom = 0.8f;

    /// <summary>How far ahead of the mob the view centres at <paramref name="down"/> radians below level: <see cref="OrbitAimLead"/>, fading out toward straight down.</summary>
    public static float OrbitLeadAt(float down)
    {
        float t = Mathf.Clamp((down - OrbitLeadFadeFrom) / (OrbitMaxDown - OrbitLeadFadeFrom), 0f, 1f);
        return OrbitAimLead * (1f - t * t * (3f - 2f * t));
    }

    /// <summary>
    /// The third-person free look (F5; the owner, 2026-10-16): the camera turns on its own and the mob does not turn with it; the keys walk
    /// the mob relative to the camera (A walks it to the screen's left, seen from the side) and the mob faces the way it walks
    /// (<c>GameScene.FreeLookMove</c>). Off: the mob faces where the camera looks, as in the first person.
    /// </summary>
    public bool OrbitFreeLook { get; set; }

    /// <summary>The mouse wheel's range for <see cref="OrbitDistance"/> in the third-person view, metres.</summary>
    public const float OrbitMinDistance = 3.5f, OrbitMaxDistance = 12f;

    /// <summary>Wheel notches in or out of the third-person view (fractional from a trackpad's two-finger scroll).</summary>
    public void Zoom(float notches) => OrbitDistance = Mathf.Clamp(OrbitDistance * Mathf.Pow(1.12f, notches), OrbitMinDistance, OrbitMaxDistance);

    /// <summary>The orbit's look point above the floor, looking well down; at a shallower pitch it rises (<see cref="OrbitClearHead"/>).</summary>
    public const float OrbitLookHeight = 0.6f;

    /// <summary>A level view looks at this height (over the drawn human's head, 1.45 m, and the 1.3 m eye), so the camera is well under the 2 m ceiling.</summary>
    public const float OrbitLevelHeight = 1.6f;

    /// <summary>
    /// A level view hangs the camera this far to the side, over the mob's right shoulder, so the mob does not cover the crosshair; looking
    /// down by <see cref="OrbitShoulderDown"/> or more the line passes over its head and the camera is centred again.
    /// </summary>
    public const float OrbitShoulder = 0.4f;

    private const float OrbitShoulderDown = 0.55f;

    // The owner (2026-10-16): the orbit turns from straight down to the level and a little up; the camera stops short of a wall
    // (OrbitBlocked) instead of hanging inside it.
    private const float OrbitMaxUp = 0.35f;
    private const float OrbitMaxDown = 1.45f;

    /// <summary>
    /// The third-person view's walls: whether a world point is inside something the camera must not go into (a wall, a closed airlock).
    /// Null: nothing stops it.
    /// </summary>
    public Func<Vector3, bool> OrbitBlocked { get; set; }

    public Camera3D Camera => _camera;

    /// <summary>T5a: the first-person eye's height above the feet at a level look, as the last <see cref="UpdateRig"/> placed it (LookDown.Pivot).</summary>
    public float EyeUp { get; private set; } = TileSpace.EyeHeight;

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("Camera");
        _camera.Current = true;
        _camera.CullMask &= ~Light.LightLayer.MirrorOnlyLayer; // the own body's reflection is the mirrors' alone
    }

    // How far back from the look point the camera may hang along back: the zoom, or less where a wall is in the way (a hand's breadth
    // short of it). The look point itself is on the mob's side: the mob's own cell and the floor ahead of it are open.
    private float OrbitReach(Vector3 look, Vector3 back, float from)
    {
        if (OrbitBlocked == null)
            return OrbitDistance;
        const float Step = 0.1f, Margin = 0.35f;
        for (float d = from + Step; d <= OrbitDistance; d += Step)
        {
            if (OrbitBlocked(look + back * d))
                return Mathf.Max(from + 0.3f, d - Margin);
        }
        return OrbitDistance;
    }

    /// <summary>Turns the view by a mouse movement (pixels); returns the turn, radians up and to the right.</summary>
    public (float Up, float Right) Look(Vector2 relative)
    {
        float pitch = Pitch;
        Turn(-relative.Y * MouseSensitivity, relative.X * MouseSensitivity);
        return (Pitch - pitch, relative.X * MouseSensitivity);
    }

    /// <summary>Turns the view <paramref name="up"/> and to the <paramref name="right"/>, radians (the mouse, DV96's gun kick).</summary>
    public void Turn(float up, float right)
    {
        Yaw = Mathf.Wrap(Yaw - right, -Mathf.Pi, Mathf.Pi);
        Pitch = (float)LookDown.ClampPitch(Pitch + up, Mode == CameraMode.FirstPerson);
    }

    public void SetMode(CameraMode mode)
    {
        if (mode == Mode)
            return;
        if (mode == CameraMode.Free)
            _freePosition = GlobalPosition; // a free camera starts where the view is now
        else if (mode == CameraMode.Orbit)
            Pitch = Mathf.Min(Pitch, -0.7f);
        else if (mode == CameraMode.FirstPerson && Mode == CameraMode.Orbit)
            Pitch = 0f; // back to the eye: level, not the orbit's look down
        Pitch = (float)LookDown.ClampPitch(Pitch, mode == CameraMode.FirstPerson); // T5a: past 89 degrees down is the first person's alone
        Mode = mode;
    }

    public void CycleMode()
    {
        SetMode(Mode switch
        {
            CameraMode.FirstPerson => CameraMode.Free,
            CameraMode.Free => CameraMode.Orbit,
            _ => CameraMode.FirstPerson,
        });
    }

    /// <summary>Starts a free camera at <paramref name="position"/> (when there is no own capsule to follow).</summary>
    public void PlaceFree(Vector3 position) => _freePosition = position;

    /// <summary>The free camera's flight speed as a share of its own (the map editor's wheel changes it).</summary>
    public float FlySpeedScale { get; set; } = 1f;

    /// <summary>Where the free camera is (the rig is placed there by the next <see cref="UpdateRig"/>).</summary>
    public Vector3 FreePosition => _freePosition;

    /// <summary>The turn of the view by its yaw and pitch, as <see cref="UpdateRig"/> sets it.</summary>
    public Basis LookBasis => new Basis(Vector3.Up, Yaw) * new Basis(Vector3.Right, Pitch);

    /// <summary>The way the view looks.</summary>
    public Vector3 Forward => LookBasis * Vector3.Forward;

    /// <summary>
    /// The free camera's keys move it level, by the yaw alone (the map editor's top view, which looks straight down: forward is up the
    /// screen, not down into the floor).
    /// </summary>
    public bool FlyLevel { get; set; }

    /// <summary>
    /// Turns the free camera around a point by a mouse movement (pixels): the view turns as <see cref="Look"/> turns it and the camera goes
    /// around the point by the same turn, so the point stays where it is on the screen.
    /// </summary>
    public void OrbitFree(Vector3 pivot, Vector2 relative)
    {
        Basis was = LookBasis;
        Look(relative);
        _freePosition = pivot + LookBasis * (was.Inverse() * (_freePosition - pivot));
    }

    /// <summary>
    /// Places the camera for this frame. <paramref name="ownFeet"/> is where the own capsule stands (floor height),
    /// <paramref name="hasOwn"/> says whether it exists; <paramref name="fly"/> is the free camera's wish (x right, y up,
    /// z forward), each -1 to 1.
    /// </summary>
    public void UpdateRig(double delta, Vector3 ownFeet, bool hasOwn, Vector3 fly, bool fast, CameraOffsets offsets = default)
    {
        float up = offsets.Up > 0f ? offsets.Up : TileSpace.EyeHeight;
        if (Mode == CameraMode.Orbit && hasOwn)
            Pitch = Mathf.Clamp(Pitch, -OrbitMaxDown, OrbitMaxUp);
        // version 34 (DV96): the recoil kick turns the view (Godot's yaw turns left), so the crosshair's next pick follows it
        Rotation = new Vector3(Pitch + offsets.PitchRadians, Yaw - offsets.YawRadians, offsets.RollRadians);
        switch (Mode)
        {
            case CameraMode.FirstPerson when hasOwn:
                // The camera layer's sideways offset is along the view's right, level with the floor. T5a: the eye turns about the neck
                // (LookDown.EyeOffset): looking down it goes forward along the view's yaw and down.
                var level = new Basis(Vector3.Up, Yaw);
                EyeUp = up;
                (double eyeAhead, double eyeDrop) = LookDown.EyeOffset(Pitch + offsets.PitchRadians, up);
                GlobalPosition = ownFeet + level.X * offsets.Right + new Vector3(0, up - (float)eyeDrop, 0) - level.Z * (float)eyeAhead;
                if (offsets.Forward != 0f) // DV96: a heavy gun kick shoves the eye back along the view
                    GlobalPosition += Transform.Basis * new Vector3(0f, 0f, -offsets.Forward);
                break;
            case CameraMode.Orbit when hasOwn:
                // Behind and above the point ahead of the mob that the view centres on (straight above the mob when looking straight down).
                float down = -Pitch;
                Vector3 ahead = new Basis(Vector3.Up, Yaw) * new Vector3(0, 0, -OrbitLeadAt(down));
                float lookHeight = Mathf.Clamp(OrbitLevelHeight - OrbitAimLead * Mathf.Tan(down), OrbitLookHeight, OrbitLevelHeight + 0.3f);
                float shoulder = OrbitShoulder * Mathf.Clamp(1f - down / OrbitShoulderDown, 0f, 1f);
                Vector3 look = ownFeet + ahead + new Vector3(0, lookHeight, 0) + GlobalTransform.Basis.X * shoulder;
                Vector3 back = GlobalTransform.Basis.Z;
                // the walls are looked for from where the line passes over the mob's own (open) cell: the look point and the line
                // up to there may be in the wall the mob faces, and a camera stopped there would see through that wall
                Vector3 backFlat = new(back.X, 0, back.Z), toMob = ownFeet - look;
                float fromMob = Mathf.Max(0f, new Vector3(toMob.X, 0, toMob.Z).Dot(backFlat) / Mathf.Max(backFlat.LengthSquared(), 1e-4f));
                GlobalPosition = look + back * OrbitReach(look, back, fromMob);
                break;
            default:
                // Free, or nothing to follow yet.
                Vector3 wish = (FlyLevel ? new Basis(Vector3.Up, Yaw) : GlobalTransform.Basis) * new Vector3(fly.X, 0, -fly.Z) + Vector3.Up * fly.Y;
                _freePosition += wish * (FlySpeed * FlySpeedScale * (fast ? FastFactor : 1f) * (float)delta);
                GlobalPosition = _freePosition;
                break;
        }
    }
}
