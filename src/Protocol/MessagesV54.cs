namespace Anomaly.Protocol;

// Protocol version 54 (D285, docs/ai/specs/protocol.md section 59; the codec: Codec.V54.cs).
// - HeldMotion (server to client, State channel, after the tick's Delta): the player's own gun was loaded, unloaded or racked this tick
//   (tg's ballistic gun procs, DV213), so the first-person gun moves as it does; before, the client guessed it from tg's sounds.

/// <summary>Version 54: what the own gun did (the core's <c>HeldMotion</c>).</summary>
public enum HeldMotionKind : byte
{
    /// <summary>A magazine or rounds went in.</summary>
    Load = 0,

    /// <summary>A magazine or rounds came out.</summary>
    Unload = 1,

    /// <summary>The bolt, slide or pump was worked.</summary>
    Rack = 2,
}

/// <summary>Version 54: the player worked a gun in their hands this tick.</summary>
public sealed record HeldMotionMessage(HeldMotionKind Kind);
