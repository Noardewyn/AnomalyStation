namespace Anomaly.Protocol;

// Version 40 (M9 slice E, protocol.md section 49): the PDA and the chat window's radio.
// - KeybindingCode.OpenMessenger: the big chat window's Message button (D251), sent as a Keybinding action.
// - RadioChannels: the radio prefixes (":e ") of the channels the player's worn headset has on (tg's radio.channels and
//   GLOB.department_radio_keys), sent when they change; the big chat window's Radio list (chat.md 3).

/// <summary>Version 40: a radio prefix of tg's say: a colon, one lowercase ASCII letter or digit, a space (<c>":e "</c>).</summary>
public static class RadioPrefixes
{
    public static bool IsValid(string prefix)
        => prefix is { Length: 3 } && prefix[0] == ':' && prefix[2] == ' ' && (prefix[1] is >= 'a' and <= 'z' or >= '0' and <= '9');
}

/// <summary>
/// Version 40: server to client, State channel: the radio prefixes of the channels of the player's worn headset, in its channels'
/// order, sent whenever they change (and on join); empty when it wears none or its channels are all off.
/// </summary>
public sealed class RadioChannelsMessage
{
    /// <summary>The prefixes, 0 to <see cref="Limits.MaxRadioChannels"/>, each <see cref="RadioPrefixes.IsValid"/>, none twice.</summary>
    public List<string> Prefixes { get; } = new();
}
