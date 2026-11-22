using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: the contract is the M2 contracts step's (frozen, docs/ai/specs/m2-ownership.md): M2 P8 implements it (the sample
// interpolation with a hold at the newest sample, and the own capsule's predicted pose), M2 P7's renderers read it.
// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// Where an entity is drawn at a render time: BYOND tile coordinates with a fraction (a tile centre is a whole number,
/// TileSpace's convention), the level, the facing for the billboard's frame and the posture for the lying quad.
/// </summary>
/// <param name="X">BYOND x with a fraction.</param>
/// <param name="Y">BYOND y with a fraction.</param>
/// <param name="Z">The level.</param>
/// <param name="Dir">tg's dir (NORTH 1, SOUTH 2, EAST 4, WEST 8; tile-driven movables and the fallback of the frame choice).</param>
/// <param name="Yaw">Where a capsule mob looks, 0 to 255 per turn clockwise from north (NetField.Yaw), interpolated; the billboard picks its frame from the viewer's angle to it (D104).</param>
/// <param name="Posture">tg's body position and lying angle (Anomaly.Protocol.PostureBits): lying mobs are drawn on a floor-aligned quad (D104).</param>
/// <param name="Rise">DV371: metres above (below, negative) the level's floor while the entity changes level (<see cref="LevelRise"/>).</param>
public readonly record struct RenderPose(float X, float Y, int Z, byte Dir, byte Yaw, byte Posture, float Rise = 0f);

/// <summary>
/// The poses the renderer draws (M2 scope 2d, 1.6): every other entity from its samples at the render time (the newest
/// sample held, others extrapolated by at most a tick), the own capsule from the predictor's newest state plus the visible
/// error and a render-only partial step. P8 implements it; P7's entity layer asks it once per entity per frame.
/// </summary>
public interface IPoseSource
{
    /// <summary>The pose of <paramref name="entity"/> at the frame being drawn; false when it has none yet (not drawn).</summary>
    bool TryGetPose(ReplicaEntity entity, out RenderPose pose);
}
