namespace Anomaly.Client.Shared;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// D285: the poses and motions of the own held things in first person (Hud/ViewModel), Minecraft-like: a thing comes up from under the view when
// taken, goes down when put away, swings when it hits, kicks when it fires, dips and turns when reloaded. Offsets are in the camera's space:
// metres, x right, y up, z toward the eye (the camera looks down -z); angles in degrees. The left hand's are the right's mirrored.

/// <summary>How a held thing is held.</summary>
public enum HeldKind : byte
{
    /// <summary>A small thing, held by its middle, its picture turned a little toward the middle of the view.</summary>
    Small,

    /// <summary>A long thing (a tool, a blade), held by its handle, its picture seen at a slant.</summary>
    Long,

    /// <summary>A gun: its barrel points at the crosshair (Hud/ViewModel aims it).</summary>
    Gun,
}

/// <summary>DV96: the own gun's last view kick (CameraLayer.Kick): its size in degrees, how heavy (0 to 1) and the side it rolls to (1 right, -1 left).</summary>
public readonly record struct HeldKick(float Degrees, float Heavy, float Side);

/// <summary>A pose or a pose's change: a move and a turn (pitch about x, yaw about y, roll about z).</summary>
public readonly record struct HeldOffset(float X, float Y, float Z, float Pitch = 0f, float Yaw = 0f, float Roll = 0f)
{
    public static HeldOffset operator +(HeldOffset a, HeldOffset b) =>
        new(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.Pitch + b.Pitch, a.Yaw + b.Yaw, a.Roll + b.Roll);

    public static HeldOffset operator *(HeldOffset a, float k) => new(a.X * k, a.Y * k, a.Z * k, a.Pitch * k, a.Yaw * k, a.Roll * k);

    /// <summary>The same for the left hand: across the view the other way, turned the other way.</summary>
    public HeldOffset Mirrored => new(-X, Y, Z, Pitch, -Yaw, -Roll);
}

/// <summary>The held things' poses and motions as pure functions of time.</summary>
public static class HeldPose
{
    /// <summary>
    /// A picture's pixel in metres at the pose's distance (the owner, 2026-10-28: a crowbar was small and hung high enough to show nothing held it; it
    /// is big now and its handle goes under the view's edge, as Minecraft's; a gun was small too and is four times what it was (2026-10-28 and
    /// 2026-10-29), a 32-pixel gun about 0.9 m).
    /// </summary>
    public static float PixelMetres(HeldKind kind) => kind switch
    {
        HeldKind.Gun => 0.028f,
        HeldKind.Long => 0.012f,
        _ => 0.011f,
    };

    /// <summary>How far a thing goes under the view when it is taken or put away (metres).</summary>
    public const float Drop = 0.4f;

    public const double RaiseSeconds = 0.25, LowerSeconds = 0.2;

    /// <summary>An overhead swing's length: up and back, a chop down toward the crosshair, and back to rest.</summary>
    public const double SwingSeconds = 0.4;

    public const double ReloadSeconds = 0.7, RackSeconds = 0.35;

    /// <summary>The inactive hand's thing sits this much lower (metres), the owner's choice (2026-10-28: lower again, so it reads as not in use).</summary>
    public const float InactiveDrop = 0.12f;

    /// <summary>A long thing's head is turned this many degrees up from the side away from the middle of the view (its handle down), as Minecraft holds a tool.</summary>
    public const float LongLean = 60f;

    /// <summary>A gun's barrel meets the crosshair this far ahead (metres).</summary>
    public const float Converge = 10f;

    /// <summary>A thing's depth in pixels (the owner, 2026-10-28: about twice Minecraft's one) and a gun's (seen from behind its barrel it is still a solid).</summary>
    public const float Thickness = 2.1f, GunThickness = 2.4f;

    /// <summary>The right hand's rest pose of a kind; a wielded thing is held nearer the middle.</summary>
    public static HeldOffset Rest(HeldKind kind, bool left, bool wielded)
    {
        HeldOffset rest = kind switch
        {
            HeldKind.Gun => new HeldOffset(0.25f, -0.3f, -0.45f), // turned and aimed by the view model; its grip under the view's edge
            HeldKind.Long => new HeldOffset(0.3f, -0.3f, -0.42f, Pitch: -15f, Yaw: -55f), // the handle's end under the view's edge
            _ => new HeldOffset(0.26f, -0.19f, -0.42f, Yaw: -20f, Roll: -8f), // its lower part under the edge
        };
        if (wielded)
            rest += new HeldOffset(-0.1f, -0.02f, 0f, Roll: kind == HeldKind.Gun ? 0f : -15f);
        return left ? rest.Mirrored : rest;
    }

    /// <summary>How far under the view a thing is (0 in place, 1 out of sight) <paramref name="age"/> seconds after it was taken: up fast, then easing in.</summary>
    public static float Raised(double age)
    {
        double t = Math.Clamp(age / RaiseSeconds, 0, 1);
        return (float)Math.Pow(1 - t, 3);
    }

    /// <summary>How far under the view a thing put away is <paramref name="age"/> seconds after (out of sight from <see cref="LowerSeconds"/>).</summary>
    public static float Lowered(double age)
    {
        double t = Math.Clamp(age / LowerSeconds, 0, 1);
        return (float)(t * t);
    }

    /// <summary>The swing's phases as parts of <see cref="SwingSeconds"/>: the wind-up ends at the first, the chop lands at the second.</summary>
    public const double SwingWindUp = 0.35, SwingChop = 0.55;

    /// <summary>
    /// The swing of a hit (the right hand's), a Minecraft-like chop (the owner, 2026-10-30: it read as a pull toward the eye): the thing is raised and
    /// cocked back a little, chops hard forward and down toward the crosshair, and eases back to rest. Its turn is in the camera's space about the grip
    /// (Hud/ViewModel), not the thing's own, so a pitch is a chop whatever way the thing is held.
    /// </summary>
    public static HeldOffset Swing(double age)
    {
        if (age <= 0 || age >= SwingSeconds)
            return default;
        double p = age / SwingSeconds;
        var up = new HeldOffset(0.03f, 0.08f, 0.03f, Pitch: 30f, Roll: -10f);
        var down = new HeldOffset(-0.08f, -0.1f, -0.14f, Pitch: -70f, Roll: 15f);
        if (p < SwingWindUp)
        {
            double t = p / SwingWindUp;
            return up * (float)(1 - (1 - t) * (1 - t));
        }
        if (p < SwingChop)
        {
            float t = (float)((p - SwingWindUp) / (SwingChop - SwingWindUp));
            return up + (down + up * -1f) * (t * t); // fastest as it lands
        }
        return down * (1f - Smooth((p - SwingChop) / (1 - SwingChop)));
    }

    private static float Smooth(double t) => (float)(t * t * (3 - 2 * t));

    /// <summary>How fresh the view's kick must be for the held gun to kick with it, seconds (an older one is another shot's).</summary>
    public const double KickFresh = 0.3;

    /// <summary>
    /// A gun's fire kick (ShotLook.Kick): back and its muzzle up. DV96 (the owner, 2026-11-21: every gun kicked alike, the anti-tank rifle snapped):
    /// as big as the view's kick (<paramref name="kick"/>: up to three times at 8 degrees), slower out and back when heavy (up to 80 ms and 0.5 s),
    /// and rolled toward the kick's side.
    /// </summary>
    public static HeldOffset Kick(double age, HeldKick kick = default)
    {
        float heavy = Math.Clamp(kick.Heavy, 0f, 1f);
        float k = ShotLook.Kick(age, 0.03 + 0.05 * heavy, 0.15 + 0.35 * heavy) * (1f + 0.25f * Math.Clamp(kick.Degrees, 0f, 8f));
        float side = kick.Side < 0f ? -1f : 1f;
        return new HeldOffset(0f, 0.012f * k, 0.06f * k, Pitch: 10f * k, Roll: -side * (1f + 4f * heavy) * k);
    }

    /// <summary>A reload: the gun dips, tilts onto its side to take the magazine and comes back.</summary>
    public static HeldOffset Reload(double age)
    {
        if (age <= 0 || age >= ReloadSeconds)
            return default;
        float e = (float)Math.Sin(Math.PI * age / ReloadSeconds);
        float twist = (float)Math.Sin(2 * Math.PI * age / ReloadSeconds);
        return new HeldOffset(-0.03f * e, -0.1f * e, 0.02f * e, Pitch: -18f * e, Roll: 40f * e + 6f * twist);
    }

    /// <summary>A rack: the gun jerks back and its muzzle up as the bolt, slide or pump is worked, and comes back.</summary>
    public static HeldOffset Rack(double age)
    {
        if (age <= 0 || age >= RackSeconds)
            return default;
        float e = (float)Math.Sin(Math.PI * Math.Sqrt(age / RackSeconds));
        return new HeldOffset(0f, -0.02f * e, 0.07f * e, Pitch: 12f * e, Roll: -10f * e);
    }

    /// <summary>The walk's bob: a figure eight in step with <paramref name="stridePhase"/> (radians, a step every pi), scaled by how fast one walks (0 to 1).</summary>
    public static HeldOffset Bob(double stridePhase, float amount)
    {
        if (amount <= 0f)
            return default;
        float s = (float)Math.Sin(stridePhase), c = (float)Math.Abs(Math.Cos(stridePhase));
        return new HeldOffset(0.012f * s, -0.014f * (1 - c), 0f, Roll: 1.5f * s) * amount;
    }

    /// <summary>The idle breath: a slow small rise and fall.</summary>
    public static HeldOffset Breath(double now) => new(0f, 0.004f * (float)Math.Sin(now * 1.7), 0f, Pitch: 0.4f * (float)Math.Sin(now * 1.7 + 1));

    /// <summary>A dizzy or drunk float: the thing drifts round in a slow circle, <paramref name="strength"/> 0 to 1.</summary>
    public static HeldOffset Float(double now, float strength)
    {
        if (strength <= 0f)
            return default;
        return new HeldOffset(0.035f * (float)Math.Sin(now * 1.3), 0.025f * (float)Math.Cos(now * 1.3), 0f, Roll: 6f * (float)Math.Sin(now * 0.9), Yaw: 4f * (float)Math.Cos(now * 0.7)) * strength;
    }

    /// <summary>A hurt arm (<paramref name="hurt"/> 0 to 1, HeldHands.ArmHurt) holds its thing lower, tilted, and it trembles.</summary>
    public static HeldOffset Hurt(double now, float hurt, bool left)
    {
        if (hurt <= 0f)
            return default;
        var low = new HeldOffset(0f, -0.05f * hurt, 0f, Roll: 6f * hurt);
        return (left ? low.Mirrored : low) + Tremble(now, 0.006f * hurt, left ? 1 : 2);
    }

    /// <summary>
    /// A tremble of <paramref name="metres"/> (a hurt arm): a jump to a new random place every 1/20 s (BYOND's tick), the same for the
    /// same <paramref name="seed"/> and time.
    /// </summary>
    public static HeldOffset Tremble(double now, float metres, int seed)
    {
        if (metres <= 0f)
            return default;
        long step = (long)Math.Floor(now * 20);
        float Rand(int salt)
        {
            unchecked
            {
                uint h = (uint)(step * 73856093) ^ (uint)(seed * 19349663) ^ (uint)(salt * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFF) / 32767.5f - 1f;
            }
        }
        return new HeldOffset(metres * Rand(1), metres * Rand(2), 0f, Roll: 40f * metres * Rand(3));
    }
}
