using System.Linq;
using System.Numerics;
using Anomaly.Client.Shared.World;

namespace Anomaly.Client.Shared.Models;

// Engine-free. Attach points (docs/ai/specs/models.md, "Attach points"): the places on a thing where another thing is stuck (C-4's charge, ModelRule.Attach; any
// later sticker, a sign, a mounted part). A model names its own: empties named Attach_<name> in its file (the artist's, their +Z the way out of the thing,
// glTF's front as the models' is), or the catalog's attachPoints; a thing without any gets the faces of its bounds (a wall's open sides, a window block, a
// locker's sides) and the top of a low one. What is stuck picks the point that faces the side it was stuck from.

/// <summary>A place to stick something on a thing: where (metres) and the way out of the thing there (a unit vector; up for a top).</summary>
public readonly record struct AttachPoint(string Name, Vector3 Position, Vector3 Normal)
{
    /// <summary>True for a point on a thing's top (its normal points up): what is stuck there lies on it.</summary>
    public bool IsTop => Normal.Y > 0.7f;
}

/// <summary>Making and choosing attach points.</summary>
public static class AttachPoints
{
    /// <summary>The name prefix of a model file's attach nodes (<c>Attach_front</c>, <c>Attach_top</c>).</summary>
    public const string NodePrefix = "Attach_";

    /// <summary>The names of the points made from bounds.</summary>
    public const string North = "north", South = "south", East = "east", West = "west", Top = "top";

    /// <summary>A thing lower than this (metres) has its top as its one point: a table, a crate.</summary>
    public const float LowThing = 1.0f;

    /// <summary>How far inside a face's top and bottom edges a side point stays (metres): half a charge's height.</summary>
    public const float Inset = 0.25f;

    /// <summary>
    /// The points of a box (world metres: x east, y up, z south): its top, and unless it is low (<see cref="LowThing"/>) the middle of each side at
    /// <paramref name="height"/> above the floor kept <see cref="Inset"/> inside the face; <paramref name="sides"/> (null: all) says which sides may take one
    /// (a wall's open ones).
    /// </summary>
    public static AttachPoint[] OfBox(Vector3 min, Vector3 max, float height, Func<byte, bool> sides = null)
    {
        Vector3 c = (min + max) / 2f;
        var points = new List<AttachPoint> { new(Top, new Vector3(c.X, max.Y, c.Z), Vector3.UnitY) };
        if (max.Y - min.Y < LowThing)
            return points.ToArray();
        float y = Math.Clamp(height, min.Y + Inset, Math.Max(min.Y + Inset, max.Y - Inset));
        foreach ((byte dir, string name, Vector3 at) in new[]
                 {
                     (ByondDirConst.North, North, new Vector3(c.X, y, min.Z)), (ByondDirConst.South, South, new Vector3(c.X, y, max.Z)),
                     (ByondDirConst.East, East, new Vector3(max.X, y, c.Z)), (ByondDirConst.West, West, new Vector3(min.X, y, c.Z)),
                 })
        {
            if (sides == null || sides(dir))
                points.Add(new AttachPoint(name, at, Outward(dir)));
        }
        return points.ToArray();
    }

    /// <summary>
    /// A model file's own points (its <see cref="GlbFile.AttachNodes"/>) fitted as its mesh is: <paramref name="horizontal"/> and <paramref name="vertical"/> are the
    /// model's scale (<see cref="ModelMesh.Scale"/>); the normal is the node's +Z.
    /// </summary>
    public static IEnumerable<AttachPoint> OfFile(GlbFile file, float horizontal, float vertical)
    {
        foreach ((string name, Matrix4x4 world) in file.AttachNodes)
        {
            Vector3 at = world.Translation;
            Vector3 n = Vector3.TransformNormal(Vector3.UnitZ, world);
            n = new Vector3(n.X * horizontal, n.Y * vertical, n.Z * horizontal);
            yield return new AttachPoint(name, new Vector3(at.X * horizontal, at.Y * vertical, at.Z * horizontal), n.LengthSquared() > 0f ? Vector3.Normalize(n) : Vector3.UnitZ);
        }
    }

    /// <summary>A point in a model's own frame placed in the world: turned by <paramref name="yaw"/> (radians about +Y, as the model is) and moved to <paramref name="origin"/>.</summary>
    public static AttachPoint Placed(AttachPoint local, Vector3 origin, float yaw)
    {
        var turn = Matrix4x4.CreateRotationY(yaw);
        return local with { Position = origin + Vector3.Transform(local.Position, turn), Normal = Vector3.TransformNormal(local.Normal, turn) };
    }

    /// <summary>
    /// The point what is stuck from <paramref name="dir"/> (the side its planter stood on, a BYOND dir; 0: none) takes: the side point that faces that way best,
    /// when one faces it at all; else the top; else the side that faces it best anyway. Null when there are none.
    /// </summary>
    public static AttachPoint? Choose(IReadOnlyList<AttachPoint> points, byte dir)
    {
        if (points == null || points.Count == 0)
            return null;
        Vector3 toward = Outward(dir);
        AttachPoint? top = points.Where(p => p.IsTop).Select(p => (AttachPoint?)p).FirstOrDefault();
        AttachPoint[] sides = points.Where(p => !p.IsTop).ToArray();
        if (sides.Length == 0)
            return top;
        AttachPoint best = sides.MaxBy(p => Vector3.Dot(Flat(p.Normal), toward));
        if (toward != Vector3.Zero && Vector3.Dot(Flat(best.Normal), toward) > 0.25f)
            return best;
        return top ?? best;
    }

    /// <summary>The world vector of a BYOND dir, out of the thing toward it (x east, z south; diagonals unit length); zero for none.</summary>
    public static Vector3 Outward(byte dir)
    {
        float x = ((dir & ByondDirConst.East) != 0 ? 1 : 0) - ((dir & ByondDirConst.West) != 0 ? 1 : 0);
        float z = ((dir & ByondDirConst.South) != 0 ? 1 : 0) - ((dir & ByondDirConst.North) != 0 ? 1 : 0);
        var v = new Vector3(x, 0, z);
        return v == Vector3.Zero ? v : Vector3.Normalize(v);
    }

    private static Vector3 Flat(Vector3 v)
    {
        var flat = new Vector3(v.X, 0, v.Z);
        return flat == Vector3.Zero ? flat : Vector3.Normalize(flat);
    }
}
