namespace Anomaly.Protocol;

// Protocol version 51 (M10x slice C, docs/ai/specs/protocol.md section 56; the codec: Codec.V43.cs and Codec.V2.cs).
// - The private block's eye: bit 1 of the version 43 flags byte, then [varuint eye] after the access list: tg's client.eye when it is
//   a mob other than the own mob (a ghost observing a player through his eyes, do_observe), else 0; sent when it changed (every
//   Snapshot names it). The client's camera then looks from that mob (DV201).
// - ActionKind.DblClick (MessagesV2.cs): BYOND's DblClick, the second press of a double click, with a mouse action's fields.
// - The motion parameters end with [varuint push speed] (MotionParams.ImpulseSpeed, Codec.V2.cs): the drift a push from
//   rest would start where it surely would, so the predictor starts it (DV177).

public sealed partial class OwnBlock
{
    /// <summary>
    /// Version 51: the world id of tg's <c>client.eye</c> when it is a mob other than the own mob (0 when it is the own mob, a turf or
    /// any other thing), when it changed since the last block (every Snapshot carries it); null when unchanged.
    /// </summary>
    public long? Eye { get; set; }
}

public sealed partial class ReplicaWorld
{
    /// <summary>Version 51: the mob whose eyes the client looks through (tg's <c>client.eye</c>, another mob), 0 for the own view.</summary>
    public long EyeEntity { get; private set; }
}
