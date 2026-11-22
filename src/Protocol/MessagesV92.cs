namespace Anomaly.Protocol;

// Protocol version 92 (DV96, the owner 2026-11-21: a shooter's crosshair; docs/ai/specs/protocol.md section 89; the codec: Codec.V92.cs).
// - GunSight (server to client, State channel, after the tick's Delta, when it changes): whether the own mob holds a gun in its active hand,
//   how wide that gun's shots scatter (the half angle of the 3D cone, the server's share of tg's spread included), and how many of the
//   mob's shots have hit a living thing (a counter that wraps), for the crosshair's gap and its hit marker.

/// <summary>Version 92: the own gun's sight.</summary>
public sealed record GunSightMessage(bool Gun, float ConeDegrees, byte Hits)
{
    /// <summary>No gun in hand and no hit yet: what a client starts from.</summary>
    public static readonly GunSightMessage None = new(false, 0f, 0);

    /// <summary>The widest cone a gun's sight names, degrees off the aim.</summary>
    public const float MaxConeDegrees = 90f;
}
