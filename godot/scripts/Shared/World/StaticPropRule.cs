namespace Anomaly.Client.Shared.World;

/// <summary>
/// The client's static-versus-moving rule (docs/ai/specs/m3-ownership.md; apps.md 2.10): an entity is a static prop, drawn in its chunk's
/// batched MultiMesh, when it is not a mob, has no <c>Pos</c> (no capsule) and has received no X, Y or Z change since it spawned; the
/// first position change takes it out of the batch for good, as a node of the entity layer. Mobs are always nodes, and so are tg's
/// hotspots (M4b slice 3: a burning tile is drawn as flames, D18, never as its flat sprite).
/// </summary>
public static class StaticPropRule
{
    /// <summary>tg's hotspot type (LINDA_fire.dm): the entity layer draws its flames.</summary>
    public const string HotspotType = "/obj/effect/hotspot";

    /// <summary>tg's explosion: the entity layer draws it as a burst of puffs (D303), never baked as a flat sprite.</summary>
    public const string BlastType = "/obj/effect/explosion";

    /// <summary>tg's particle effects (the ion trail, the water puffs, smoke): short-lived, flicked, see-through, faded near the eye.</summary>
    public const string ParticlePrefix = "/obj/effect/particle_effect";

    /// <summary>tg's point arrow and its hologram (point.dm): the entity layer stands it on the pointed point (DV232), never in the batch.</summary>
    public const string PointArrowPrefix = "/obj/effect/temp_visual/point";

    /// <summary>A mining scanner's ping (mineral_scanner.dm): stands inside the rock, so the entity layer draws it through the rock (DV250), never in the batch.</summary>
    public const string MiningPingType = "/obj/effect/temp_visual/mining_overlay";

    /// <summary>tg's foamed metal (effects_foam.dm): the entity layer draws it as a block of its own picture (DV247), never in the batch.</summary>
    public const string FoamedMetalPrefix = "/obj/structure/foamedmetal";

    /// <summary>tg's wooden barricades (deployable.dm): a block of their picture like the foamed metal (DV247).</summary>
    public const string WoodenBarricadePrefix = "/obj/structure/barricade/wooden";

    /// <summary>A holopad's hologram (hologram.dm): moves with its projector's eye from the moment it appears (DV429), never in the batch.</summary>
    public const string HologramType = "/obj/effect/overlay/holo_pad_hologram";

    /// <summary>
    /// Whether an entity of this type and kind of position may be a static prop at all. A particle effect never is: it lives a second or two,
    /// flicks, blanks itself or glides away, and is drawn see-through and faded near the eye, all of which the entity layer does
    /// (the space play test, 2026-10-15: a trail particle blanked after its flick fell from the batch to a placeholder box). Nor is a shot or a
    /// shot's effect (D176, <see cref="ShotLook"/>: the entity layer draws them in 3D; a projectile spawned in the batch drew tg's sprite there
    /// for its first tile, a tracer, a muzzle flash or an impact never left it), nor any other projectile, projectile effect or beam segment (DV77, DV78),
    /// nor tg's point arrow (DV232: in the batch it stood on the pointer's own tile, where tg spawns it), nor tg's foamed metal (DV247: a block of its picture).
    /// </summary>
    public static bool IsCandidate(string typePath, bool hasPos)
        => !hasPos && !(typePath != null && (typePath.StartsWith("/mob", StringComparison.Ordinal) || typePath == HotspotType || typePath == BlastType
            || typePath.StartsWith(ParticlePrefix, StringComparison.Ordinal) || typePath.StartsWith(PointArrowPrefix, StringComparison.Ordinal) || typePath == MiningPingType || typePath == HologramType || ShotLook.KindOf(typePath) != ShotKind.None
            || typePath.StartsWith(FoamedMetalPrefix, StringComparison.Ordinal) || typePath.StartsWith(WoodenBarricadePrefix, StringComparison.Ordinal)
            || typePath.StartsWith("/obj/structure/barricade/security", StringComparison.Ordinal)
            || typePath.StartsWith("/obj/projectile", StringComparison.Ordinal) || typePath.StartsWith("/obj/effect/projectile", StringComparison.Ordinal)
            || typePath.StartsWith("/obj/effect/ebeam", StringComparison.Ordinal)));

    /// <summary>Whether the entity left the cell it spawned in (so it is no longer static, for good).</summary>
    public static bool HasMoved((int X, int Y, int Z) spawnCell, int x, int y, int z) => spawnCell != (x, y, z);

    /// <summary>
    /// Whether a moved thing is planned anew where it now stands instead of leaving the batch: a shaped piece always (it has no
    /// node form), anything that jumped to another level or further than one tile (a shuttle's cargo, which stands still again).
    /// </summary>
    public static bool Replans(bool piece, (int X, int Y, int Z) from, int x, int y, int z)
        => piece || from.Z != z || Math.Abs(from.X - x) > 1 || Math.Abs(from.Y - y) > 1;
}
