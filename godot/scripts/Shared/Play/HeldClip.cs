using System.IO;
using System.Linq;
using System.Numerics;
using Anomaly.Client.Shared.Models;

namespace Anomaly.Client.Shared;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// D285: a first-person swing keyframed in Blender (godot/assets/models/held/held_swing.blend, tools/models/make_held_swing.py): the hands' move from
/// the rest pose and the held thing's turn about them, in the camera's space, the right hand's (the left plays its mirror image). The clip moves the
/// hands, not a model, so it plays for whatever thing is in the hand. At its start and its end it is at rest.
/// </summary>
public sealed class HeldClip
{
    /// <summary>The file of the held swings, under the models folder.</summary>
    public const string File = "held/held_swing.glb";

    /// <summary>The node the clips key: the point the hands hold.</summary>
    public const string GripNode = "Grip";

    /// <summary>The clips: a one-handed melee hit's chop (raised, then brought straight down), a wielded one's, a pointed thing's thrust (tg's SHARP_POINTY) and a wielded one's.</summary>
    public const string MeleeSwing = "melee_swing", WieldChop = "wield_chop", Stab = "stab", WieldStab = "wield_stab";

    /// <summary>Every clip the game plays.</summary>
    public static readonly string[] Names = { MeleeSwing, WieldChop, Stab, WieldStab };

    /// <summary>The clip of a melee hit with a held thing: held in both hands (tg's two_handed) it chops two-handed, a spear too (a stab only by the
    /// thing's own choice, held_items.json); in one hand a pointed thing stabs and any other chops.</summary>
    public static string For(bool pointy, bool wielded) => wielded ? WieldChop : pointy ? Stab : MeleeSwing;

    private readonly float[] _moveTimes, _turnTimes;
    private readonly Vector3[] _moves;
    private readonly Quaternion[] _turns;

    /// <summary>How long it plays (seconds).</summary>
    public float Length { get; }

    private HeldClip(float[] moveTimes, Vector3[] moves, float[] turnTimes, Quaternion[] turns)
    {
        (_moveTimes, _moves, _turnTimes, _turns) = (moveTimes, moves, turnTimes, turns);
        Length = Math.Max(moveTimes.LastOrDefault(), turnTimes.LastOrDefault());
    }

    /// <summary>The clip <paramref name="name"/> of a read file; throws <see cref="InvalidDataException"/> when it is missing or keys no grip.</summary>
    public static HeldClip Of(GlbFile file, string name)
    {
        GlbAnimation animation = file.Animations.FirstOrDefault(a => a.Name == name)
            ?? throw new InvalidDataException("no clip " + name + " in the held swings");
        GlbChannel move = animation.Channels.FirstOrDefault(c => c.Node == GripNode && c.Path == "translation");
        GlbChannel turn = animation.Channels.FirstOrDefault(c => c.Node == GripNode && c.Path == "rotation");
        if (move == null && turn == null)
            throw new InvalidDataException("the clip " + name + " keys no " + GripNode);
        if (move?.Interpolation == "CUBICSPLINE" || turn?.Interpolation == "CUBICSPLINE")
            throw new InvalidDataException("the clip " + name + " is cubic: export it sampled (linear keys)");
        return new HeldClip(
            move?.Times ?? new[] { 0f }, move?.Values.Select(v => new Vector3(v[0], v[1], v[2])).ToArray() ?? new[] { Vector3.Zero },
            turn?.Times ?? new[] { 0f }, turn?.Values.Select(v => new Quaternion(v[0], v[1], v[2], v[3])).ToArray() ?? new[] { Quaternion.Identity });
    }

    /// <summary>
    /// The hands' move (metres, the camera's space) and the thing's turn about them <paramref name="age"/> seconds into the clip, the left hand's mirrored;
    /// at rest before it and after it.
    /// </summary>
    public (HeldOffset Move, Quaternion Turn) At(double age, bool left)
    {
        if (age <= 0 || age >= Length)
            return (default, Quaternion.Identity);
        float t = (float)age;
        Vector3 move = Lerp(_moveTimes, _moves, t, Vector3.Lerp);
        Quaternion turn = Lerp(_turnTimes, _turns, t, Quaternion.Slerp);
        // the mirror image across the view's middle: x turned over, and so a turn's y and z
        return left
            ? (new HeldOffset(-move.X, move.Y, move.Z), new Quaternion(turn.X, -turn.Y, -turn.Z, turn.W))
            : (new HeldOffset(move.X, move.Y, move.Z), turn);
    }

    private static T Lerp<T>(float[] times, T[] values, float t, Func<T, T, float, T> blend)
    {
        int next = Array.FindIndex(times, k => k > t);
        if (next < 0)
            return values[^1];
        if (next == 0)
            return values[0];
        float span = times[next] - times[next - 1];
        return blend(values[next - 1], values[next], span > 0f ? (t - times[next - 1]) / span : 1f);
    }
}
