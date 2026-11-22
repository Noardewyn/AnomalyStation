namespace Anomaly.Protocol;

// Version 42 (M10 slice B, protocol.md section 51): the capsule mobs' positions on the unreliable channel.
// - Motion: the position and yaw of every capsule mob whose position or yaw changed in the last Limits.MotionResendTicks ticks, as of the
//   tick, sent after the tick's Delta on the Input channel (unreliable); several messages when they do not fit one. A Delta's change
//   record carries Pos and Yaw only while the entity is not yet a capsule (its first record); a spawn record carries them as before.

/// <summary>Version 42: one capsule mob's continuous position (1/256 tile, as <see cref="NetField.Pos"/>) and yaw at the message's tick.</summary>
public readonly record struct MotionEntry(long Id, int PosX, int PosY, byte Yaw);

/// <summary>
/// Version 42: server to client, Input channel (unreliable): the capsule mobs' positions at <see cref="Tick"/>. The client applies an
/// entry to an entity it holds when the tick is newer than that entity's last position, and ignores an id it does not hold (its spawn
/// may still be on the reliable channel). Entries are in increasing id order.
/// </summary>
public sealed class MotionMessage
{
    public long Tick { get; set; }

    public List<MotionEntry> Entries { get; } = new();
}
