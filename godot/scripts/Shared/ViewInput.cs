using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

/// <summary>The movement keys as the player sees them: relative to the view (W forward, S back, A left, D right).</summary>
[Flags]
public enum MoveKeys : byte
{
    None = 0,
    Forward = 1,
    Back = 2,
    Left = 4,
    Right = 8,
}

/// <summary>
/// The view-relative direction mapping (D101): WASD and the arrows are relative to where the player looks, the world's
/// movement is absolute tile directions. The camera yaw is quantized to the nearest cardinal direction and the held keys
/// are rotated by it, so W walks "forward" in the grid direction closest to the view. Two held non-opposite keys give a
/// diagonal (tg allows it, <c>keyLoop</c>); opposite keys cancel, as tg's own sanity check does
/// (<c>bindings_atom.dm</c>: N and S, E and W held together are both dropped).
/// <para>
/// Yaw is Godot's: radians about +Y, counter-clockwise seen from above, 0 faces north (-Z), +90 degrees faces west. A
/// heading is a number of quarter turns from north: 0 north, 1 west, 2 south, 3 east.
/// </para>
/// </summary>
public static class ViewInput
{
    private const double Quarter = Math.PI / 2;

    // Counter-clockwise from north, the order in which a quarter turn rotates a direction.
    private static readonly HeldDirs[] CounterClockwise = { HeldDirs.North, HeldDirs.West, HeldDirs.South, HeldDirs.East };

    /// <summary>The cardinal heading nearest to <paramref name="yawRadians"/>: 0 north, 1 west, 2 south, 3 east.</summary>
    public static int QuarterTurns(double yawRadians)
    {
        if (double.IsNaN(yawRadians) || double.IsInfinity(yawRadians))
            return 0;
        int turns = (int)Math.Round(yawRadians / Quarter, MidpointRounding.AwayFromZero);
        return ((turns % 4) + 4) % 4;
    }

    /// <summary>Drops opposite keys (forward with back, left with right): neither counts, like tg.</summary>
    public static MoveKeys Cancel(MoveKeys keys)
    {
        if ((keys & MoveKeys.Forward) != 0 && (keys & MoveKeys.Back) != 0)
            keys &= ~(MoveKeys.Forward | MoveKeys.Back);
        if ((keys & MoveKeys.Left) != 0 && (keys & MoveKeys.Right) != 0)
            keys &= ~(MoveKeys.Left | MoveKeys.Right);
        return keys;
    }

    /// <summary>
    /// The absolute held directions for <paramref name="keys"/> when the view faces heading <paramref name="quarterTurns"/>.
    /// </summary>
    public static HeldDirs ToAbsolute(MoveKeys keys, int quarterTurns)
    {
        keys = Cancel(keys);
        int turns = ((quarterTurns % 4) + 4) % 4;
        HeldDirs result = HeldDirs.None;
        if ((keys & MoveKeys.Forward) != 0)
            result |= CounterClockwise[(0 + turns) % 4];
        if ((keys & MoveKeys.Left) != 0)
            result |= CounterClockwise[(1 + turns) % 4];
        if ((keys & MoveKeys.Back) != 0)
            result |= CounterClockwise[(2 + turns) % 4];
        if ((keys & MoveKeys.Right) != 0)
            result |= CounterClockwise[(3 + turns) % 4];
        return result;
    }

    /// <summary>A short text of a held mask ("NW", "E", "-"), for the debug HUD and logs.</summary>
    public static string Describe(HeldDirs dirs)
    {
        if (dirs == HeldDirs.None)
            return "-";
        string text = string.Empty;
        if ((dirs & HeldDirs.North) != 0) text += "N";
        if ((dirs & HeldDirs.South) != 0) text += "S";
        if ((dirs & HeldDirs.East) != 0) text += "E";
        if ((dirs & HeldDirs.West) != 0) text += "W";
        return text;
    }
}

/// <summary>
/// Turns the held keys and the camera yaw into the stream of absolute masks to send: an <see cref="InputFrame"/> goes out
/// whenever <see cref="Current"/> differs from the last one reported, including when turning the mouse changes the heading
/// while a key is held (the same key now means another absolute direction).
/// <para>
/// The heading has a hysteresis: once the view is at a heading it stays there until the yaw is more than 45 degrees plus
/// <see cref="HysteresisRadians"/> away from it, so a view that rests on the diagonal between two headings does not make
/// the held key flicker between two directions (one frame per flip) as the mouse jitters.
/// </para>
/// </summary>
public sealed class InputTracker
{
    /// <summary>How far past the 45 degree boundary the yaw must go before the heading flips (5 degrees).</summary>
    public const double HysteresisRadians = 5.0 * Math.PI / 180.0;

    private MoveKeys _keys;
    private int _turns;
    private HeldDirs _reported;

    /// <summary>The current heading in quarter turns (0 north, 1 west, 2 south, 3 east).</summary>
    public int Turns => _turns;

    /// <summary>The keys held now, view-relative.</summary>
    public MoveKeys Keys => _keys;

    /// <summary>The absolute directions the held keys mean at the current heading.</summary>
    public HeldDirs Current => ViewInput.ToAbsolute(_keys, _turns);

    /// <summary>The last mask <see cref="TryTakeChange"/> reported (what the server holds now).</summary>
    public HeldDirs Reported => _reported;

    /// <summary>Takes the camera yaw (radians, Godot's convention).</summary>
    public void SetYaw(double yawRadians)
    {
        int nearest = ViewInput.QuarterTurns(yawRadians);
        if (nearest == _turns)
            return;
        // Angular distance from the centre of the current heading, folded to [-pi, pi].
        double delta = yawRadians - _turns * (Math.PI / 2);
        delta = Math.IEEERemainder(delta, 2 * Math.PI);
        if (Math.Abs(delta) > Math.PI / 4 + HysteresisRadians)
            _turns = nearest;
    }

    /// <summary>Takes the held movement keys (view-relative).</summary>
    public void SetKeys(MoveKeys keys) => _keys = keys;

    /// <summary>
    /// True and the new mask when <see cref="Current"/> differs from the last one reported (and that it is now reported);
    /// false when nothing changed.
    /// </summary>
    public bool TryTakeChange(out HeldDirs dirs)
    {
        dirs = Current;
        if (dirs == _reported)
            return false;
        _reported = dirs;
        return true;
    }

    /// <summary>Forgets the held keys (a lost focus, a camera mode that takes the keys); the next <see cref="TryTakeChange"/> reports the release.</summary>
    public void ReleaseAll() => _keys = MoveKeys.None;
}
