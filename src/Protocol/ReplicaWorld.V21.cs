namespace Anomaly.Protocol;

// Version 21 (MessagesV21.cs, protocol.md section 30): the camera shake. The replica keeps nothing (a shake is an event, not state)
// and raises CameraShakeReceived; the play layer moves the eye along the path.

public sealed partial class ReplicaWorld
{
    private Action<CameraShakeMessage> _cameraShakeHandlers;

    /// <summary>Raised for each applied <see cref="CameraShakeMessage"/>.</summary>
    public event Action<CameraShakeMessage> CameraShakeReceived
    {
        add => _cameraShakeHandlers += value;
        remove => _cameraShakeHandlers -= value;
    }

    /// <summary>The camera shakes applied since the start (tests read it).</summary>
    public int CameraShakes { get; private set; }

    /// <summary>Applies a <see cref="CameraShakeMessage"/>: throws <see cref="ProtocolException"/> for one before a Snapshot.</summary>
    public void Apply(CameraShakeMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!_hasSnapshot)
            throw Bad("a CameraShake message arrives before any Snapshot");
        CameraShakes++;
        _cameraShakeHandlers?.Invoke(message);
    }
}
