namespace Anomaly.Protocol;

// Version 40 (MessagesV40.cs, protocol.md section 49): the worn headset's radio prefixes. The replica keeps the last list (the chat
// window's Radio list reads it) and raises RadioChannelsChanged.

public sealed partial class ReplicaWorld
{
    private Action<IReadOnlyList<string>> _radioChannelsHandlers;

    /// <summary>Raised for each applied <see cref="RadioChannelsMessage"/>, with its prefixes.</summary>
    public event Action<IReadOnlyList<string>> RadioChannelsChanged
    {
        add => _radioChannelsHandlers += value;
        remove => _radioChannelsHandlers -= value;
    }

    /// <summary>The radio prefixes of the worn headset's channels (":e "), the last the server sent; empty before any.</summary>
    public IReadOnlyList<string> RadioPrefixes { get; private set; } = Array.Empty<string>();

    /// <summary>Applies a <see cref="RadioChannelsMessage"/>.</summary>
    public void Apply(RadioChannelsMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        RadioPrefixes = message.Prefixes.ToArray();
        _radioChannelsHandlers?.Invoke(RadioPrefixes);
    }
}
