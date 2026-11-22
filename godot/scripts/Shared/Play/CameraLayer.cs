namespace Anomaly.Client.Shared;

// Owner: M2 P8 (client play). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>What the camera layer asks of the camera for one frame, in metres (camera-relative: right, up, and forward along the view).</summary>
/// <param name="Right">Sideways offset of the eye.</param>
/// <param name="Up">Height of the eye above the floor, bob and shake included.</param>
/// <param name="Forward">Offset along the view (DV96: a heavy gun kick shoves the eye back).</param>
/// <param name="RollRadians">A small roll (shake).</param>
public readonly record struct CameraOffsets(float Right, float Up, float Forward, float RollRadians)
{
    /// <summary>Version 34 (DV96): the recoil kick's turn of the view up (positive) or down, radians.</summary>
    public float PitchRadians { get; init; }

    /// <summary>Version 34 (DV96): the recoil kick's turn of the view to the right (clockwise seen from above, positive) or left, radians.</summary>
    public float YawRadians { get; init; }

    /// <summary>DV96: a heavy gun kick's widening of the field of view, degrees.</summary>
    public float FovDegrees { get; init; }
}

/// <summary>
/// The first-person camera layer (D41: "the camera is a replaceable layer", D70: head bob and camera shake have an off
/// switch). It knows nothing of the world: each frame it is told the own capsule's speed and posture and says where the
/// eye is. The eye is at <see cref="StandingEye"/> (1.3 m, D157) standing, <see cref="LyingEye"/> (0.4 m) lying, and
/// eases between them; a walk bobs the eye by distance travelled (not by time: a stopped capsule stands still); a shake
/// (a shove, a knockdown, damage) is a decaying wobble. Pure logic: the caller passes the frame time.
/// </summary>
public sealed class CameraLayer
{
    /// <summary>The eye height standing, metres (D157: 1.3 m by default, the one setting <see cref="WorldScale.EyeHeight"/>).</summary>
    public static float StandingEye => WorldScale.EyeHeight;

    /// <summary>The eye height lying down, metres (D157: 0.4 m, the old 0.5 m at <see cref="WorldScale.MobScale"/>, as the drawn mob shrank).</summary>
    public const float LyingEye = 0.4f;

    /// <summary>How fast the eye changes height, metres per second (standing up takes about 0.4 s: 0.9 m at the D157 eye of 1.3 m, 1 m at D137's 1.5 m; it was 1.8 m/s for the 0.7 m of the 1.2 m eye and 2.8 m/s for the 1.1 m of the 1.6 m eye).</summary>
    public const float EyeSpeed = 2.5f;

    /// <summary>Metres walked per bob cycle (two steps).</summary>
    public const float StrideMetres = 3.2f;

    /// <summary>The bob's vertical amplitude at full effect, metres.</summary>
    public const float BobUp = 0.035f;

    /// <summary>The bob's sideways amplitude, metres.</summary>
    public const float BobSide = 0.018f;

    /// <summary>At this speed (m/s) and above the bob is at full amplitude; below it scales down.</summary>
    public const float BobFullSpeed = 5.0f;

    /// <summary>The bob's cycle rate never exceeds this many cycles per second (a 13 m/s run must not flicker).</summary>
    public const float MaxBobHz = 2.6f;

    /// <summary>The owner (2026-11-07): the eye of a pilot, at the head of an exosuit drawn at the tile scale, metres; held under the ceiling.</summary>
    public const float ExosuitEye = 1.8f;

    /// <summary>The bob in an exosuit is this much the walker's: each stomp lifts and drops the whole cabin.</summary>
    public const float ExosuitBobScale = 3.5f;

    /// <summary>Metres per bob cycle in an exosuit: two tiles, a stomp a tile, so the view falls with the step sound (DV358's stride).</summary>
    public static float ExosuitStrideMetres => 2f * TileSpace.TileSize;

    /// <summary>
    /// M4c slice 7 (DV41): the eye's float bob in zero gravity, from tg's floating animation (<c>DO_FLOATING_ANIM</c>: the body's
    /// <c>pixel_z</c> rises 2 pixels in a second and sinks back in the next, for ever): the same 2 pixels (2/32 of a tile) and 2 seconds a
    /// cycle, eased as a sine instead of tg's straight lines (a turn at full speed is a jolt for an eye, not for a sprite).
    /// </summary>
    public static float FloatBobMetres => 2f / 32f * TileSpace.TileSize;

    /// <summary>Seconds of one float cycle (tg: 1 s up, 1 s down).</summary>
    public const float FloatPeriodSeconds = 2f;

    /// <summary>The float bob fades in and out over about this long (<c>STOP_FLOATING_ANIM</c> settles the body in 1 s).</summary>
    public const float FloatFadeSeconds = 1f;

    /// <summary>The largest lean of the view into a sideways glide in zero gravity (<see cref="ZeroGLevel.On"/>; half of it reduced), radians (4 degrees).</summary>
    public const float MaxLeanRadians = 0.07f;

    /// <summary>A sideways glide this fast (metres per second) leans the view fully.</summary>
    public const float LeanFullSpeed = 10f;

    private float _eye = StandingEye;
    private float _floatPhase;
    private float _floatAmount;
    private float _lean;
    private float _phase;
    private float _bobAmount;
    private float _speed;
    private float _shakeAmplitude;
    private float _shakeLeft;
    private float _shakeDuration = 1f;
    private float _shakeClock;
    private readonly float _seed;

    public CameraLayer(int seed = 1)
    {
        _seed = (seed & 0xFFFF) * 0.37f;
    }

    /// <summary>Head bob while walking (D70: off switch).</summary>
    public bool HeadBob { get; set; } = true;

    /// <summary>The own body is an exosuit: the eye rises to its head and the walk bobs harder.</summary>
    public bool InExosuit { get; set; }

    /// <summary>D346: the standing eye of a body that is not a human's (a possessed corgi, a mouse, a cyborg; <see cref="BodyEyes"/>), metres, or null for <see cref="StandingEye"/>.</summary>
    public float? BodyEye { get; set; }

    /// <summary>Camera shake (D70: off switch).</summary>
    public bool Shake { get; set; } = true;

    /// <summary>M4c slice 7 (D70): the float bob while weightless.</summary>
    public ZeroGLevel FloatBob { get; set; } = ZeroGLevel.On;

    /// <summary>M4c slice 7 (D70): the lean into a sideways glide while weightless, with a slight sway on the float.</summary>
    public ZeroGLevel Roll { get; set; } = ZeroGLevel.Off;

    /// <summary>The float bob's current strength, 0 to 1 (it fades in and out).</summary>
    public float FloatAmount => _floatAmount;

    /// <summary>The eye height now, bob and shake not included.</summary>
    public float EyeHeight => _eye;

    /// <summary>A shake is running.</summary>
    public bool Shaking => _shakeLeft > 0f;

    /// <summary>Starts a shake of <paramref name="amplitudeMetres"/> that dies away over <paramref name="seconds"/>; a stronger one replaces a weaker one that is running.</summary>
    public void AddShake(float amplitudeMetres, float seconds)
    {
        if (!Shake || amplitudeMetres <= 0f || seconds <= 0f)
            return;
        float current = _shakeLeft > 0f ? _shakeAmplitude * (_shakeLeft / _shakeDuration) : 0f;
        if (amplitudeMetres < current)
            return;
        _shakeAmplitude = amplitudeMetres;
        _shakeDuration = seconds;
        _shakeLeft = seconds;
    }

    // Protocol version 21 (M5x slice 2): tg's shake_camera path, BYOND animate steps of the client's pixel_x and pixel_y.
    private readonly List<(float X, float Y, float Seconds)> _path = new();
    private int _pathStep;
    private float _pathClock;
    private float _pathFromX, _pathFromY, _pathX, _pathY;

    /// <summary>A shake path is running.</summary>
    public bool FollowingPath => _pathStep < _path.Count;

    /// <summary>
    /// Version 21: moves the view along tg's shake path (shake_camera, mob_helpers.dm:164): each step eases linearly from where the view
    /// is to the step's <c>pixel_x</c> (to the view's right) and <c>pixel_y</c> (up) in its time, 32 pixels to a tile. A new path
    /// replaces the running one from the view's present offset, as BYOND's animate does. D70: nothing with the shake switch off.
    /// </summary>
    public void FollowShakePath(IReadOnlyList<(int PixelX, int PixelY, int Deciseconds)> steps)
    {
        if (!Shake || steps == null || steps.Count == 0)
            return;
        _pathFromX = _pathX;
        _pathFromY = _pathY;
        _path.Clear();
        foreach ((int x, int y, int ds) in steps)
            _path.Add((x / 32f * TileSpace.TileSize, y / 32f * TileSpace.TileSize, Math.Max(ds, 1) / 10f));
        _pathStep = 0;
        _pathClock = 0f;
    }

    // Version 34 (DV96): tg's recoil_camera kick turns the view (GunKick: a shooter's kick, part aim, part a spring's punch). DV106: tg's
    // dizziness wobble comes the same way, a longer chain of view offsets: the turn passes through each step's point in its time and the last
    // step brings it back.
    private readonly List<(float Pitch, float Yaw, float Seconds)> _turn = new();
    private int _turnStep;
    private float _turnFromPitch, _turnFromYaw, _recoilPitch, _recoilYaw, _recoilClock;
    private bool _recoiling;

    /// <summary>Degrees of view kick per tile of tg's recoil offset (D179: 3).</summary>
    public const float RecoilDegreesPerTile = 3f;

    /// <summary>The gun kick (DV96, the owner 2026-11-21): its aim part is taken by the caller (<see cref="GunKick.TakeAim"/>) and turns the view's own pitch and yaw.</summary>
    public GunKick Kick { get; } = new();

    /// <summary>D285: tg's dizziness wobble (DV106) is turning the view.</summary>
    public bool Recoiling => _recoiling;

    /// <summary>D285: tg's dizziness wobble (a chain of more than a kick's two steps, DV106) is turning the view.</summary>
    public bool Wobbling => _recoiling;

    /// <summary>D285: the head bob's phase, radians (a step every pi), and how much it bobs (0 to 1; 0 with the head bob off): the held things bob with it.</summary>
    public float BobPhase => _phase * 2f * MathF.PI;

    public float BobAmount => _bobAmount;

    /// <summary>D288: how hard one walks for the own flashlight's sway: the bob's amount, more past <see cref="BobFullSpeed"/> (a run).</summary>
    public float Stride => _bobAmount * Math.Max(1f, _speed / BobFullSpeed);

    /// <summary>The view turn's pitch now, radians (up positive): the wobble's and the kick's punch.</summary>
    public float TurnPitch => _recoilPitch + Kick.PunchUp;

    /// <summary>The view turn's yaw now, radians (right positive).</summary>
    public float TurnYaw => _recoilYaw + Kick.PunchRight;

    /// <summary>
    /// Version 34 (DV96): tg's <c>recoil_camera</c> path (mob_helpers.dm:212: the view pushed by <c>pixel_x</c>, <c>pixel_y</c> away from the
    /// shot, then back) as a turn of the first-person view: the part of the push behind the facing <paramref name="facingDegrees"/> (compass)
    /// pitches the view up, the part to its right yaws it right, <see cref="RecoilDegreesPerTile"/> a tile (32 pixels). A kick (a push and its
    /// return) goes to <see cref="Kick"/>, played the shooter's way whatever tg's times: part of it turns the aim, so the next shot flies where the
    /// view now points, as tg's kicked view spoils a follow-up click. Not the shake switch's (D70): the kick is aim, not wobble. DV106: a chain
    /// of more steps (tg's dizziness wobble) turns through every step's offset linearly, and its last step returns the view with BYOND's
    /// BACK_EASING.
    /// </summary>
    public void Recoil(IReadOnlyList<(int PixelX, int PixelY, int Deciseconds)> steps, double facingDegrees)
    {
        if (steps == null || steps.Count == 0)
            return;
        double a = facingDegrees * Math.PI / 180.0;
        (float Pitch, float Yaw) Turn(int i)
        {
            double east = steps[i].PixelX / 32.0, north = steps[i].PixelY / 32.0;
            double back = -(east * Math.Sin(a) + north * Math.Cos(a)), right = east * Math.Cos(a) - north * Math.Sin(a);
            return ((float)(back * RecoilDegreesPerTile * Math.PI / 180.0), (float)(right * RecoilDegreesPerTile * Math.PI / 180.0));
        }
        if (steps.Count <= 2)
        {
            (float up, float right) = Turn(0);
            Kick.Kick(up, right);
            return;
        }
        _turn.Clear();
        for (int i = 0; i < steps.Count; i++)
        {
            float seconds = Math.Max(steps[i].Deciseconds, 1) / 10f;
            if (i == steps.Count - 1)
            {
                _turn.Add((0f, 0f, seconds)); // the last step brings the view back
                break;
            }
            (float pitch, float yaw) = Turn(i);
            _turn.Add((pitch, yaw, seconds));
        }
        _turnFromPitch = _recoilPitch;
        _turnFromYaw = _recoilYaw;
        _turnStep = 0;
        _recoilClock = 0f;
        _recoiling = true;
    }

    private void AdvanceRecoil(float dt)
    {
        if (!_recoiling)
            return;
        _recoilClock += dt;
        while (_turnStep < _turn.Count)
        {
            (float pitch, float yaw, float seconds) = _turn[_turnStep];
            bool last = _turnStep == _turn.Count - 1;
            if (_recoilClock < seconds)
            {
                float k = _recoilClock / seconds;
                float eased = last ? BackEaseIn(k) : k; // tg's linear steps and its back easing
                _recoilPitch = _turnFromPitch + (pitch - _turnFromPitch) * eased;
                _recoilYaw = _turnFromYaw + (yaw - _turnFromYaw) * eased;
                return;
            }
            _recoilClock -= seconds;
            _turnFromPitch = _recoilPitch = pitch;
            _turnFromYaw = _recoilYaw = yaw;
            _turnStep++;
        }
        _recoilPitch = _recoilYaw = 0f;
        _recoiling = false;
    }

    /// <summary>BYOND's <c>BACK_EASING</c> (ease in): it first moves a little the other way, t^2 ((s + 1) t - s) with s = 1.70158.</summary>
    public static float BackEaseIn(float t)
    {
        const float s = 1.70158f;
        return t * t * ((s + 1f) * t - s);
    }

    private void AdvancePath(float dt)
    {
        if (!Shake)
        {
            _path.Clear();
            _pathX = _pathY = 0f;
            return;
        }
        _pathClock += dt;
        while (_pathStep < _path.Count)
        {
            (float x, float y, float seconds) = _path[_pathStep];
            if (_pathClock < seconds)
            {
                float k = _pathClock / seconds;
                _pathX = _pathFromX + (x - _pathFromX) * k;
                _pathY = _pathFromY + (y - _pathFromY) * k;
                return;
            }
            _pathClock -= seconds;
            _pathFromX = _pathX = x;
            _pathFromY = _pathY = y;
            _pathStep++;
        }
        _pathClock = 0f;
    }

    /// <summary>Starts the eye at the right height at once (a join, a respawn).</summary>
    public void Reset(bool lying)
    {
        _eye = lying ? LyingEye : StandingEye;
        _phase = 0f;
        _bobAmount = 0f;
        _shakeLeft = 0f;
        _floatAmount = 0f;
        _lean = 0f;
        _path.Clear();
        _pathX = _pathY = _pathFromX = _pathFromY = 0f;
        _recoiling = false;
        _recoilPitch = _recoilYaw = 0f;
        Kick.Reset();
    }

    /// <summary>
    /// One frame: <paramref name="lying"/> is the own posture, <paramref name="speedMetresPerSecond"/> the capsule's speed
    /// over the ground (0 when it cannot move), <paramref name="walking"/> says the feet are on the floor and moving by
    /// their own power. M4c slice 7: <paramref name="floating"/> says the body is weightless (no walk bob; the float bob and the lean
    /// by their levels), <paramref name="sidewaysMetresPerSecond"/> its glide to the view's right (negative: left).
    /// </summary>
    public CameraOffsets Update(double dtSeconds, bool lying, float speedMetresPerSecond, bool walking, bool floating = false, float sidewaysMetresPerSecond = 0f)
    {
        float dt = (float)Math.Clamp(dtSeconds, 0.0, 0.25);
        _speed = speedMetresPerSecond;
        float target = InExosuit ? Math.Min(ExosuitEye, WorldScale.WallHeight - 0.15f)
            : BodyEye is float body ? (lying ? BodyEyes.Lying(body) : body)
            : lying ? LyingEye : StandingEye;
        if (_eye < target)
            _eye = Math.Min(target, _eye + EyeSpeed * dt);
        else if (_eye > target)
            _eye = Math.Max(target, _eye - EyeSpeed * dt);

        float right = 0f, up = 0f, roll = 0f;

        // Head bob: the phase advances with distance, the amount follows the speed smoothly.
        float wantBob = HeadBob && walking && !lying && !floating ? Math.Clamp(speedMetresPerSecond / BobFullSpeed, 0f, 1f) : 0f;
        _bobAmount += (wantBob - _bobAmount) * Math.Min(1f, dt * 8f);
        if (_bobAmount > 0.001f)
        {
            float hz = Math.Min(speedMetresPerSecond / (InExosuit ? ExosuitStrideMetres : StrideMetres), MaxBobHz);
            _phase += hz * dt;
            if (_phase > 1000f)
                _phase -= 1000f;
            float angle = _phase * 2f * MathF.PI;
            float weight = InExosuit ? ExosuitBobScale : 1f;
            // Up twice per cycle (each step), side once.
            up += (MathF.Abs(MathF.Sin(angle)) * BobUp * _bobAmount - BobUp * 0.5f * _bobAmount) * weight;
            right += MathF.Sin(angle) * BobSide * _bobAmount * weight;
            roll += MathF.Sin(angle) * 0.004f * _bobAmount * weight;
        }

        // Zero gravity (DV41): the float bob, by time like tg's animation, fading in and out; the lean into a sideways glide and a slight
        // sway on the float. Both by their levels (off, half, full).
        float wantFloat = floating ? Strength(FloatBob) : 0f;
        _floatAmount = wantFloat > _floatAmount
            ? Math.Min(wantFloat, _floatAmount + dt / FloatFadeSeconds)
            : Math.Max(wantFloat, _floatAmount - dt / FloatFadeSeconds);
        if (_floatAmount > 0f)
        {
            _floatPhase += dt / FloatPeriodSeconds;
            if (_floatPhase > 1000f)
                _floatPhase -= 1000f;
            up += MathF.Sin(_floatPhase * 2f * MathF.PI) * FloatBobMetres * 0.5f * _floatAmount;
        }
        else
        {
            _floatPhase = 0f;
        }
        float rollStrength = floating ? Strength(Roll) : 0f;
        float wantLean = -MaxLeanRadians * rollStrength * Math.Clamp(sidewaysMetresPerSecond / LeanFullSpeed, -1f, 1f);
        _lean += (wantLean - _lean) * Math.Min(1f, dt * 4f);
        roll += _lean + MaxLeanRadians * 0.15f * rollStrength * _floatAmount * MathF.Sin(_floatPhase * 2f * MathF.PI + 1.3f);

        // Shake: a few unrelated sines scaled by the remaining strength.
        if (_shakeLeft > 0f)
        {
            _shakeClock += dt;
            _shakeLeft = Math.Max(0f, _shakeLeft - dt);
            float strength = _shakeAmplitude * (_shakeLeft / _shakeDuration);
            float t = _shakeClock + _seed;
            right += strength * (MathF.Sin(t * 47.1f) * 0.6f + MathF.Sin(t * 29.3f + 1.7f) * 0.4f);
            up += strength * (MathF.Sin(t * 53.9f + 0.6f) * 0.6f + MathF.Sin(t * 31.7f + 2.9f) * 0.4f);
            roll += strength * 0.35f * MathF.Sin(t * 37.3f + 4.1f);
        }
        else
        {
            _shakeClock = 0f;
        }

        // Version 21: tg's shake path (the view's pixel offset as the client's pixel_x and pixel_y move it)
        AdvancePath(dt);
        right += _pathX;
        up += _pathY;

        // Version 34 (DV96): the wobble and the gun kick's punch (its eye shove, wider field of view and roll when heavy)
        AdvanceRecoil(dt);
        Kick.Advance(dt);

        return new CameraOffsets(right, _eye + up, Kick.Forward, roll + Kick.Roll)
        {
            PitchRadians = _recoilPitch + Kick.PunchUp,
            YawRadians = _recoilYaw + Kick.PunchRight,
            FovDegrees = Kick.FovDegrees,
        };
    }

    private static float Strength(ZeroGLevel level) => level switch
    {
        ZeroGLevel.On => 1f,
        ZeroGLevel.Reduced => 0.5f,
        _ => 0f,
    };
}
