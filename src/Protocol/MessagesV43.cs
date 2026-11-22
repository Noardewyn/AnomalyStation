namespace Anomaly.Protocol;

// Version 43 (M10 slice E, protocol.md section 52): what the client needs to guess its own hands and doors (D38 stage 1).
// - The private block's ActionsDone: the connection's Action messages the server has applied or dropped, so the client knows when the
//   replica holds the server's answer to its n-th action; and the own mob's access list (tg's get_access) when it changes.
// - An airlock's Door field (NetField.Door): its access requirements and whether a bump would open it for an authorised mob now.
// - A screen entry's inventory box (tg's slot_id, held_index) and an item entry's slot_flags and equip delay.

/// <summary>
/// Version 43: an airlock's <see cref="NetField.Door"/>: tg's <c>req_access</c> (every one needed), <c>req_one_access</c> (one of them
/// needed), <c>unres_sides</c> (the directions from the door it opens toward without access) and <see cref="DoorFlags"/>. Both lists
/// empty: it opens for anyone (tg's <c>check_access_list</c>).
/// </summary>
public sealed record DoorState(DoorFlags Flags, byte UnresSides, string[] ReqAll, string[] ReqAny)
{
    public bool Equals(DoorState other)
        => other is not null && Flags == other.Flags && UnresSides == other.UnresSides
            && ReqAll.AsSpan().SequenceEqual(other.ReqAll) && ReqAny.AsSpan().SequenceEqual(other.ReqAny);

    public override int GetHashCode() => HashCode.Combine(Flags, UnresSides, ReqAll.Length, ReqAny.Length);
}

/// <summary>Version 43: the bits of <see cref="DoorState.Flags"/>.</summary>
[Flags]
public enum DoorFlags : byte
{
    None = 0,

    /// <summary>A bump or a click by a mob it lets in would open it now: closed, ID scan on, powered, not operating, bolted, welded, sealed or emagged, and openable by hand (door.dm Bumped, try_to_activate_door; airlock.dm open).</summary>
    Openable = 1,

    /// <summary>tg's emergency access: it opens for anyone (door.dm allowed).</summary>
    Emergency = 2,

    All = Openable | Emergency,
}

public sealed partial class OwnBlock
{
    /// <summary>
    /// Version 43: how many of the connection's Action messages the server has applied or dropped since it joined (Actions are reliable
    /// and ordered, so the client's n-th action is settled when this reaches n, and the replica then holds its result).
    /// </summary>
    public uint ActionsDone { get; set; }

    /// <summary>
    /// Version 43: the own mob's access (tg's <c>get_access()</c>: the held and worn IDs), in the server's order, when it changed since
    /// the last block (every Snapshot carries it); null when unchanged.
    /// </summary>
    public List<string> Access { get; set; }
}
