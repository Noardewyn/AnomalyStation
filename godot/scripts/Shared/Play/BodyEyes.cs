using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

/// <summary>
/// D346 (the owner, 2026-11-21: the admin plays any body, an NPC, a creature or a robot): the first-person eye of a body that is not a human's
/// follows its picture. A human keeps D157's standing eye (the option, 1.3 m by default) whatever it wears on its head; a smaller human (a
/// monkey) and every other mob (a corgi, a mouse, a cyborg, a slime) see from <see cref="EyeShare"/> of the visible top of their standing
/// picture over the floor, as tg's human's eyes (<c>human_eyes.dmi</c>, 25.5 pixels up) are of its head's top (about 29). A creature taller
/// than the walls (a megafauna, a xenomorph queen) sees from its own height where nothing is over it (open sky, open space), and from just
/// under the ceiling where something is (its cell's ceiling, or the floor of the level above): the camera would be in the ceiling otherwise.
/// Engine-free: the game scene measures the picture (EntityLayer.TryGetPictureTop) and sets <see cref="CameraLayer.BodyEye"/>.
/// </summary>
public static class BodyEyes
{
    /// <summary>The eye's share of the picture's height (tg's human: the eyes 25.5 pixels up of a 29-pixel picture).</summary>
    public const float EyeShare = 0.88f;

    /// <summary>The lowest eye, metres (a mouse's picture is about 0.25 m tall).</summary>
    public const float MinEye = 0.15f;

    /// <summary>A human picture at least this tall keeps the standing eye option (D157); a shorter one is a monkey's or a child's.</summary>
    public const float HumanTop = 1.2f;

    /// <summary>The highest eye under open sky, metres: a guard against a picture made tall by an effect over it (tg's largest mobs are 64 to 96 pixels, 3 to 5 m).</summary>
    public const float MaxEye = 6f;

    /// <summary>How far under a ceiling the eye stays, metres.</summary>
    public const float UnderCeiling = 0.15f;

    /// <summary>A small body lying sees from this share of its standing eye (the human's lying eye is 0.4 of 1.3).</summary>
    public const float LyingShare = 0.5f;

    /// <summary>
    /// The eye of a body of <paramref name="typePath"/> whose standing picture's visible top is <paramref name="pictureTop"/> metres over the
    /// floor, <paramref name="covered"/> when something is over its cell (<see cref="Covered"/>); null for the standing eye option (a human,
    /// or no picture measured).
    /// </summary>
    public static float? For(string typePath, float pictureTop, bool covered = true)
    {
        if (typePath == null || !typePath.StartsWith("/mob/living", StringComparison.Ordinal) || !(pictureTop > 0f))
            return null;
        if (typePath.StartsWith("/mob/living/carbon/human", StringComparison.Ordinal) && pictureTop >= HumanTop)
            return null;
        return Math.Clamp(pictureTop * EyeShare, MinEye, covered ? WorldScale.WallHeight - UnderCeiling : MaxEye);
    }

    /// <summary>
    /// Whether something is over the cell (x, y, z): its own ceiling (D295: a block, a lattice or a catwalk), or the floor of the level standing on
    /// it (a multi-z stack, D325) that is not open space.
    /// </summary>
    public static bool Covered(ReplicaWorld replica, int x, int y, int z)
    {
        if (replica == null)
            return true;
        if (replica.CeilingAt(x, y, z).Exists)
            return true;
        return replica.IsLinkedBelow(z + 1) && replica.TurfTypeAt(x, y, z + 1) is string above
            && !above.StartsWith("/turf/open/openspace", StringComparison.Ordinal) && !above.StartsWith("/turf/open/space", StringComparison.Ordinal);
    }

    /// <summary>The eye lying of a body whose standing eye is <paramref name="bodyEye"/>.</summary>
    public static float Lying(float bodyEye) => Math.Min(CameraLayer.LyingEye, bodyEye * LyingShare);
}
