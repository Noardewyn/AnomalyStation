namespace Anomaly.Protocol;

// Owner: M1 P2 (contract: M1 contracts step).

/// <summary>
/// The first byte of every protocol message (scope 2e "Framing"): one transport message carries exactly one protocol
/// message, <c>[u8 MessageId][payload]</c>. An id outside this enum, or one that does not belong where it arrived (a
/// server message sent by a client, a client message on the wrong channel), is a protocol violation: the server counts a
/// strike (<see cref="Limits.MaxStrikes"/>), a client drops the connection.
/// </summary>
/// <remarks>
/// Where each message travels (<c>Anomaly.Net.Channel</c>):
/// <list type="table">
/// <item><term><see cref="Hello"/></term><description>client to server, in the transport's connection request data (not on a channel).</description></item>
/// <item><term><see cref="Welcome"/>, <see cref="MapGrid"/>, <see cref="Snapshot"/>, <see cref="Delta"/></term><description>server to client, on the State channel (reliable ordered), in that order: the join sequence, then one Delta per tick.</description></item>
/// <item><term><see cref="ServerText"/></term><description>server to client, on the Control channel (reliable ordered).</description></item>
/// <item><term><see cref="InputFrame"/></term><description>client to server, on the Control channel (reliable ordered, like tg's keyDown and keyUp verbs) in M1.</description></item>
/// <item><term><see cref="Disconnect"/></term><description>server to client, in the transport's reject data (a refused connection request) or disconnect data (a kick).</description></item>
/// </list>
/// </remarks>
public enum MessageId : byte
{
    /// <summary>Never on the wire: the default of an unread id.</summary>
    None = 0,

    /// <summary><see cref="Anomaly.Protocol.Hello"/>: client to server, in the connection request.</summary>
    Hello = 1,

    /// <summary><see cref="Anomaly.Protocol.Welcome"/>: server to client, State channel, first of the join sequence.</summary>
    Welcome = 2,

    /// <summary><see cref="Anomaly.Protocol.MapGrid"/>: server to client, State channel, after Welcome.</summary>
    MapGrid = 3,

    /// <summary><see cref="Anomaly.Protocol.Snapshot"/>: server to client, State channel, after MapGrid.</summary>
    Snapshot = 4,

    /// <summary><see cref="Anomaly.Protocol.Delta"/>: server to client, State channel, one per tick after the Snapshot.</summary>
    Delta = 5,

    /// <summary><see cref="Anomaly.Protocol.ServerText"/>: server to client, Control channel.</summary>
    ServerText = 6,

    /// <summary><see cref="Anomaly.Protocol.InputFrame"/>: client to server, Control channel.</summary>
    InputFrame = 7,

    /// <summary><see cref="DisconnectInfo"/>: server to client, in reject or disconnect data.</summary>
    Disconnect = 8,

    /// <summary>Version 2: <see cref="MotionInputMessage"/>, client to server, Input channel (unreliable), the newest frames repeated.</summary>
    MotionInput = 9,

    /// <summary>Version 2: <see cref="ActionMessage"/>, client to server, Control channel.</summary>
    Action = 10,

    /// <summary>Version 4: <see cref="ContextHintsMessage"/>, server to client, Control channel (tg's screentip for a hover).</summary>
    ContextHints = 11,

    /// <summary>Version 5: <see cref="DevGasReport"/>, server to client, Control channel (the answer to a <see cref="ActionKind.DevGasProbe"/>).</summary>
    DevGasReport = 12,

    /// <summary>Version 6: <see cref="AirflowUpdate"/>, server to client, State channel (the air flow around the player, after the tick's Delta).</summary>
    Airflow = 13,

    /// <summary>Version 13: <see cref="Anomaly.Protocol.Sounds"/>, server to client, State channel (the session's sounds of the tick, after its Delta and Airflow).</summary>
    Sounds = 14,

    /// <summary>Version 15: <see cref="RadialMenuMessage"/>, server to client, State channel (a radial menu of the session opened, turned its page or closed, after the tick's Delta).</summary>
    Radial = 15,

    /// <summary>Version 16: <see cref="UiWindowMessage"/>, server to client, State channel (a tgui window of the session updated or closed, after the tick's Delta).</summary>
    UiWindow = 16,

    /// <summary>Version 19: <see cref="TooltipMessage"/>, server to client, State channel (the session's tooltip shown or hidden, after the tick's Delta).</summary>
    Tooltip = 17,

    /// <summary>Version 21: <see cref="CameraShakeMessage"/>, server to client, State channel (the view's shake path, after the tick's Delta).</summary>
    CameraShake = 18,

    /// <summary>Version 38: <see cref="Anomaly.Protocol.SayModalMessage"/>: server to client, Control channel.</summary>
    SayModal = 19,

    /// <summary>Version 38: <see cref="Anomaly.Protocol.RunechatMessage"/>: server to client, Control channel.</summary>
    Runechat = 20,

    /// <summary>Version 40: <see cref="Anomaly.Protocol.RadioChannelsMessage"/>: server to client, State channel (the worn headset's channels changed, after the tick's Delta).</summary>
    RadioChannels = 21,

    /// <summary>Version 42: <see cref="Anomaly.Protocol.MotionMessage"/>: server to client, Input channel (unreliable: the capsule mobs' positions of the tick).</summary>
    Motion = 22,

    /// <summary>Version 53: <see cref="Anomaly.Protocol.BugReportPart"/>: client to server, State channel (a part of the Esc menu's bug report, its screenshot up to 2 MB).</summary>
    BugReport = 23,

    /// <summary>Version 54: <see cref="Anomaly.Protocol.HeldMotionMessage"/>: server to client, State channel (the own gun loaded, unloaded or racked, after the tick's Delta).</summary>
    HeldMotion = 24,

    /// <summary>Version 63: <see cref="Anomaly.Protocol.TtsMessage"/>: server to client, Control channel (a line the client voices itself, D307).</summary>
    Tts = 25,

    /// <summary>Version 67: <see cref="Anomaly.Protocol.MinimapMessage"/>: server to client, State channel (a z-level's minimap, after the tick's Delta, D315).</summary>
    Minimap = 26,

    /// <summary>Version 68: <see cref="Anomaly.Protocol.MinimapMobsMessage"/>: server to client, State channel (the dev trackers of the minimap, after the tick's Delta, D315).</summary>
    MinimapMobs = 27,

    /// <summary>Version 70: <see cref="Anomaly.Protocol.BuildPreviewMessage"/>: server to client, State channel (what the held tool would build, when it changes).</summary>
    BuildPreview = 28,
    /// <summary>Version 79: <see cref="Anomaly.Protocol.RuntimeResourceMessage"/>: server to client, State channel (an image the world made at run time, before the messages that name it).</summary>
    RuntimeResource = 29,

    /// <summary>Version 84: <see cref="Anomaly.Protocol.WeatherMessage"/>: server to client, State channel (the weather of the own mob's level, after the tick's Delta, when it changes).</summary>
    Weather = 30,

    /// <summary>Version 92: <see cref="Anomaly.Protocol.GunSightMessage"/>: server to client, State channel (the own gun's cone and hits, after the tick's Delta, when it changes).</summary>
    GunSight = 31,

    /// <summary>Version 98: <see cref="Anomaly.Protocol.HotbarMessage"/>: server to client, State channel (the own hotbar's ten cells and the held items' names, after the tick's Delta, when they change or a press was refused).</summary>
    Hotbar = 32,
}
