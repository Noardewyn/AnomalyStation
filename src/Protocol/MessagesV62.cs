namespace Anomaly.Protocol;

// Version 62 (D306, the host's lobby between rounds): when tg's world/Reboot ends a round the server keeps every connection
// (BYOND's reboot made every client reconnect, the port's version 45 closed them with DisconnectReason.RoundRestart). It sends a
// ServerText of BetweenRounds.Type (the status line) and one of BetweenRounds.MusicType (the ended round's title track, when it had
// one) and drops what the client sends while the next world builds, then joins each session to the new world on the same
// connection: Welcome, MapGrid and Snapshot again, into its lobby.

/// <summary>Version 62: the <see cref="ServerText.Type"/>s of the host's lobby between rounds.</summary>
public static class BetweenRounds
{
    /// <summary>The round ended and the next world is being built: the client leaves the world and waits; <see cref="ServerText.Html"/> is the status line (plain text).</summary>
    public const string Type = "anomaly_between_rounds";

    /// <summary>The title track to play while waiting: <see cref="ServerText.Html"/> is tg's sound path (the ended round's <c>SSticker.login_music</c>).</summary>
    public const string MusicType = "anomaly_lobby_music";
}
