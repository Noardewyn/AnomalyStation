namespace Anomaly.Protocol;

// Version 38 (M9 slice A, docs/ai/specs/social.md, docs/ai/specs/protocol.md section 47): speech.
// - ActionKind.Say (client to server, Control): a message of tg's say window (tgui-say) to the server, what tg's window sends to
//   /datum/tgui_say/proc/on_message(type, payload) (tgui_input/say_modal/modal.dm): open, close, thinking, typing, entry, force, save.
//   An entry's text is at most tg's MAX_MESSAGE_LEN (1024) characters, so a Say action may be longer than the other actions
//   (Limits.MaxSayActionBytes).
// - SayModalMessage (server to client, Control): what tg's server sends to the window (window.send_message): props (the length cap and the
//   light look), force (send what is typed now), save (report what is typed now), close.
// - RunechatMessage (server to client, Control): one runechat line for this player (tg's /datum/chatmessage, datums/chatmessage.dm): the
//   speaker, the line's maptext and its lifespan. tg builds the image on the server after asking the client to measure the text
//   (client.MeasureText); the port measures, stacks and fades it on the client (DV160).

/// <summary>Version 38: the types of a <see cref="ActionKind.Say"/>, tg's window message types (<c>modal.dm</c> on_message).</summary>
public static class SayTypes
{
    /// <summary>The window opened on <see cref="ActionMessage.SayChannel"/> (tg's <c>open</c>).</summary>
    public const string Open = "open";

    /// <summary>The window closed (tg's <c>close</c>).</summary>
    public const string Close = "close";

    /// <summary>The thinking indicator on or off (<see cref="ActionMessage.SayVisible"/>, tg's <c>thinking</c>).</summary>
    public const string Thinking = "thinking";

    /// <summary>A key was typed (tg's <c>typing</c>).</summary>
    public const string Typing = "typing";

    /// <summary>The player sent the text on the channel (tg's <c>entry</c>).</summary>
    public const string Entry = "entry";

    /// <summary>The window's answer to a <see cref="SayModalMessage"/> force: the typed text, said cut short (tg's <c>force</c>).</summary>
    public const string Force = "force";

    /// <summary>The window's answer to a <see cref="SayModalMessage"/> save: the typed text (tg's <c>save</c>).</summary>
    public const string Save = "save";

    /// <summary>Whether <paramref name="type"/> is one of the types.</summary>
    public static bool IsKnown(string type) => type is Open or Close or Thinking or Typing or Entry or Force or Save;
}

/// <summary>Version 38: tg's say window channels (<c>__DEFINES/speech_channels.dm</c>).</summary>
public static class SayChannels
{
    public const string Say = "Say";
    public const string Radio = "Radio";
    public const string Me = "Me";
    public const string Ooc = "OOC";
    public const string Admin = "Admin";
    public const string Pray = "Pray";

    /// <summary>Whether <paramref name="channel"/> is one of the channels.</summary>
    public static bool IsKnown(string channel) => channel is Say or Radio or Me or Ooc or Admin or Pray;
}

/// <summary>Version 38: what tg's server sends to the say window (<c>window.send_message(type, payload)</c>).</summary>
public enum SayModalCommand : byte
{
    None = 0,

    /// <summary>tg's <c>props</c>: <see cref="SayModalMessage.MaxLength"/> and <see cref="SayModalMessage.LightMode"/> (the window's load).</summary>
    Props = 1,

    /// <summary>tg's <c>force</c>: the window sends what is typed as a <see cref="SayTypes.Force"/> entry and closes.</summary>
    Force = 2,

    /// <summary>tg's <c>save</c>: the window reports what is typed as a <see cref="SayTypes.Save"/> entry and stays open.</summary>
    Save = 3,

    /// <summary>tg's <c>close</c>: the window closes.</summary>
    Close = 4,

    /// <summary>The highest value.</summary>
    Last = Close,
}

/// <summary>Version 38: server to client, Control channel: a message to the say window.</summary>
/// <param name="Command">What to do.</param>
/// <param name="MaxLength">For <see cref="SayModalCommand.Props"/>: the most characters an entry may have (tg's <c>max_length</c>), 1 to <see cref="Limits.MaxSayEntryChars"/>; else 0.</param>
/// <param name="LightMode">For <see cref="SayModalCommand.Props"/>: tg's <c>tgui_say_light_mode</c> preference; else false.</param>
public readonly record struct SayModalMessage(SayModalCommand Command, int MaxLength, bool LightMode);

/// <summary>
/// Version 38: server to client, Control channel: one runechat line this player sees over <see cref="Target"/> (tg's <c>/datum/chatmessage</c>
/// after <c>generate_image</c>: the text cleaned, clipped, prefixed and coloured). The client places it over the target, measures it at tg's
/// <c>CHAT_MESSAGE_WIDTH</c>, pushes the older lines over the same target up and fades them as tg's <c>finish_image_generation</c> does (DV160).
/// </summary>
/// <param name="Target">The entity the line hangs on (tg's <c>message_loc</c>, the speaker or what holds it on its turf), above 0.</param>
/// <param name="Maptext">The line as tg's maptext (<c>MAPTEXT(complete_text)</c>), 1 to <see cref="Limits.MaxRunechatBytes"/> bytes.</param>
/// <param name="Lifespan">How long it lasts in deciseconds (tg's <c>lifespan</c>, CHAT_MESSAGE_LIFESPAN by default), 1 to <see cref="Limits.MaxRunechatLifespan"/>.</param>
public readonly record struct RunechatMessage(long Target, string Maptext, int Lifespan);
