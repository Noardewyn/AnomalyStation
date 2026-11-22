namespace Anomaly.Protocol;

// Version 75 (D322, the owner 2026-11-08): the dev menu is an admin's. The server tells each player whether it may open it, with a ServerText of
// DevAccess.Type, at the join and whenever that changes (an admin's rights). It only decides what the client shows: every dev command is still checked on the server.

/// <summary>Version 75: the <see cref="ServerText"/> that says whether this player may open the dev menu.</summary>
public static class DevAccess
{
    /// <summary>The <see cref="ServerText.Type"/>; <see cref="ServerText.Html"/> is <see cref="Allowed"/> or <see cref="Denied"/>.</summary>
    public const string Type = "anomaly_dev_access";

    public const string Allowed = "1";

    public const string Denied = "0";
}
