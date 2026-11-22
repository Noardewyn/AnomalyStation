namespace Anomaly.Protocol;

// Version 54 (MessagesV54.cs, protocol.md section 59): the own gun's held motion. The replica keeps nothing (an event, not state) and
// raises HeldMotionReceived; the first-person view model moves the gun.

public sealed partial class ReplicaWorld
{
    /// <summary>Raised for each applied <see cref="HeldMotionMessage"/>.</summary>
    public event Action<HeldMotionMessage> HeldMotionReceived;

    /// <summary>Applies a <see cref="HeldMotionMessage"/>: throws <see cref="ProtocolException"/> for one before a Snapshot.</summary>
    public void Apply(HeldMotionMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!_hasSnapshot)
            throw Bad("a HeldMotion message arrives before any Snapshot");
        HeldMotionReceived?.Invoke(message);
    }
}
