using Godot;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

// Owner: the contract is the M2 contracts step's (frozen, docs/ai/specs/m2-ownership.md): M2 P7 implements it (the
// IconAtlas over godot/assets/tg, the appearance compositor with GAGS, the cache per appearance and dir), M2 P8's HUD
// draws tg's screen objects with it (D107), M2 P7's billboards and turf kit use it themselves.

/// <summary>A composed appearance: the texture, its size in tg pixels (32 per tile) and where the atom's own 32x32 icon sits in it (overlays can reach out of the icon's square).</summary>
/// <param name="Texture">The composed image (nearest filtering; alpha where nothing is drawn).</param>
/// <param name="PixelWidth">Width in tg pixels.</param>
/// <param name="PixelHeight">Height in tg pixels.</param>
/// <param name="OriginX">The x of the icon square's lower left corner inside the texture, in tg pixels.</param>
/// <param name="OriginY">The y of it, in tg pixels (from the bottom).</param>
public readonly record struct ComposedAppearance(Texture2D Texture, int PixelWidth, int PixelHeight, int OriginX, int OriginY);

/// <summary>
/// The client's view of tg's icons and appearances (D104): an appearance of the replica's tables (public or the session's
/// private ones) composed into one texture for one direction, the way BYOND draws it (overlays and underlays by layer,
/// colour and alpha with RESET_COLOR and RESET_ALPHA, blend modes, transforms, pixel offsets, KEEP_TOGETHER), cached per
/// appearance and direction. Main thread only.
/// </summary>
public interface IAppearanceTextures
{
    /// <summary>
    /// The appearance <paramref name="appearance"/> of <paramref name="replica"/>'s tables drawn facing <paramref name="dir"/>
    /// (BYOND's NORTH 1, SOUTH 2, EAST 4, WEST 8 and the diagonals for 8-direction icons) at animation time
    /// <paramref name="seconds"/>; false while an icon file is missing or still loading (draw nothing then).
    /// </summary>
    bool TryCompose(ReplicaWorld replica, TableRef appearance, byte dir, double seconds, out ComposedAppearance composed);

    /// <summary>Drops every cached texture (a reconnect: a new run's tables).</summary>
    void Clear();
}
