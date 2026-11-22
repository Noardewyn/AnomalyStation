using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: the M4a contracts step (frozen interfaces: docs/ai/specs/m4a-ownership.md); M4a P7 implements them (WorldPicker,
// HudPicker). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too). docs/ai/specs/interaction.md
// section 4: one pick function, so the highlight, the crosshair, the radial and the click agree.

/// <summary>What the camera ray hit (interaction.md 4.1). The click target follows from it: <see cref="PickHit.Target"/>.</summary>
public enum PickSurface : byte
{
    /// <summary>Nothing within reach of the ray (or the first wall is beyond it).</summary>
    None = 0,

    /// <summary>A billboard (a mob or an item that has moved): its opaque pixel. Target: the entity.</summary>
    Billboard = 1,

    /// <summary>A static prop of a chunk batch: the prop's quad and its picture's alpha (items at D121's half scale). Target: the entity.</summary>
    StaticProp = 2,

    /// <summary>A D113 piece (an airlock, a window, a grille, a table, a firelock): the piece's box; a table top is a box at the table's height. Target: the entity.</summary>
    Piece = 3,

    /// <summary>A floor cell (the floor plane of the level). Target: the turf.</summary>
    Floor = 4,

    /// <summary>A wall face (the wall grid). Target: the wall's turf.</summary>
    Wall = 5,

    /// <summary>A ceiling cell. Target: the turf under it.</summary>
    Ceiling = 6,

    /// <summary>A HUD or window entry in cursor mode (<see cref="IHudPicker"/>). Target: the screen object.</summary>
    Screen = 7,
}

/// <summary>
/// A ray in the client's world metres: the eye (east, north, up above level <see cref="Z"/>'s floor), the unit direction and the farthest distance.
/// The view is the camera's unit forward, which a picture that faces the eye is square to (<see cref="PickShape.FacesEye"/>); none (zero): the ray's
/// own direction (a ray through the screen's middle). The right is the camera's right on the ground (east, north); none: square to the view's
/// ground track, which a view straight down (the editor's top view) has not.
/// </summary>
public readonly record struct PickRay(double OriginEast, double OriginNorth, double OriginUp, int Z, double DirEast, double DirNorth, double DirUp, double MaxMetres,
    double ViewEast = 0, double ViewNorth = 0, double ViewUp = 0, double RightEast = 0, double RightNorth = 0);

/// <summary>
/// The result of a pick (interaction.md 4.1-4.3). <see cref="Target"/> is what a click sends: <see cref="TargetKind.Entity"/>
/// with the entity id, <see cref="TargetKind.Turf"/> with the cell, <see cref="TargetKind.Screen"/> with the entry id.
/// </summary>
/// <param name="Surface">What was hit.</param>
/// <param name="Kind">The click target's kind.</param>
/// <param name="Id">The entity or screen entry id (Entity, Screen), else 0.</param>
/// <param name="X">The hit cell's x (Turf; for an entity its tile), else 0.</param>
/// <param name="Y">The hit cell's y.</param>
/// <param name="Z">The hit cell's z.</param>
/// <param name="Distance">Metres from the eye to the hit point.</param>
/// <param name="HitEast">The hit point, east (world metres).</param>
/// <param name="HitNorth">The hit point, north.</param>
/// <param name="HitUp">The hit point, up from the level's floor.</param>
/// <param name="IconX">tg's <c>icon-x</c> of the hit, 1 to 32 (0: none): the picture pixel of a billboard or prop (left is 1), or the tile-relative point of a floor or table top (east is x).</param>
/// <param name="IconY">tg's <c>icon-y</c>, 1 to 32: up in a picture (bottom is 1), north on a floor or table top.</param>
public readonly record struct PickHit(PickSurface Surface, TargetKind Kind, long Id, int X, int Y, int Z, double Distance,
    double HitEast, double HitNorth, double HitUp, byte IconX, byte IconY)
{
    /// <summary>No hit.</summary>
    public static PickHit None => default;

    /// <summary>Whether the ray hit something.</summary>
    public bool IsHit => Surface != PickSurface.None;
}

/// <summary>
/// The world pick (interaction.md 4.1): the nearest of a billboard, a static prop, a piece, a floor, wall or ceiling cell
/// along the ray, stopped by the first wall (a wall face is itself a hit). Pure over the replica and the renderers'
/// geometry; the server decides reach (D40), this only chooses what is under the crosshair.
/// </summary>
public interface IWorldPicker
{
    /// <summary>
    /// The nearest hit of <paramref name="ray"/> within its <see cref="PickRay.MaxMetres"/>, never the entity
    /// <paramref name="exclude"/> (the own mob). <paramref name="entitiesOnly"/>: billboards, props and pieces only (cells
    /// stop the ray but are not hits), for the hover of an empty hand on mobs.
    /// </summary>
    PickHit Pick(in PickRay ray, long exclude, bool entitiesOnly = false);
}

/// <summary>The HUD pick in cursor mode (interaction.md 4.4): the screen entry under a point of the screen.</summary>
public interface IHudPicker
{
    /// <summary>
    /// The topmost screen entry (by tg's layer, then the native window over the HUD) whose drawn picture has an opaque pixel
    /// at (<paramref name="x"/>, <paramref name="y"/>) in screen pixels, top left origin; <see cref="PickHit.None"/> when none.
    /// The hit's <see cref="PickHit.IconX"/> and <see cref="PickHit.IconY"/> are the pixel within the entry's 32 x 32 icon
    /// (tg's zone selector reads them).
    /// </summary>
    PickHit PickScreen(float x, float y);
}
