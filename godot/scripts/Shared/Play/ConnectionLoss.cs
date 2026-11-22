namespace Anomaly.Client.Shared;

/// <summary>What the client does when a connection ends (M3 I1c).</summary>
/// <param name="Quit">True: end the program with <paramref name="ExitCode"/> (a scripted run). False: go back to the connect screen.</param>
/// <param name="ExitCode">The exit code of a quitting run (1: the connection was lost).</param>
/// <param name="Notice">The sentence for the connect screen's status line (the server's own words, capitalised, with a full stop); set in both cases.</param>
/// <param name="LogLine">The line for <c>godot.log</c>, the same in every case.</param>
public readonly record struct ConnectionLoss(bool Quit, int ExitCode, string Notice, string LogLine)
{
    /// <summary>The exit code of a scripted run whose connection ended.</summary>
    public const int ScriptedExitCode = 1;

    /// <summary>
    /// The reaction to a connection that ended: <paramref name="reason"/> is <see cref="IWorldFeed.CloseReason"/>, the sentence made of the server's own
    /// words (<c>disconnected by the server: too many messages (Too many commands: ...)</c>). A <b>scripted</b> run (<see cref="ClientArgs.Automated"/>:
    /// <c>--autowalk</c>, <c>--quit-after</c>, a screenshot, the bench, scripted keys or dev commands, or a headless run, where no one is there to
    /// click) keeps its exit code, 1, because the soak and the smoke look at it; an <b>interactive</b> run, with or without <c>--connect</c>, goes back to the
    /// connect screen with the reason on it, the name and the address still filled in, so that the player can click Connect again and take the character
    /// back with the kept reconnect token (<see cref="ReconnectTokens"/>). Both log the reason.
    /// </summary>
    /// <param name="args">The run's arguments.</param>
    /// <param name="headless">True under <c>--headless</c> (the dummy display: no window to show a screen in).</param>
    /// <param name="reason">Why the connection ended; empty or null reads as "the connection ended".</param>
    /// <param name="duringGame">True when the connection ended while the world was on screen; false when a connect that was started never got in.</param>
    public static ConnectionLoss Of(ClientArgs args, bool headless, string reason, bool duringGame)
    {
        if (string.IsNullOrWhiteSpace(reason))
            reason = "the connection ended";
        reason = reason.Trim();
        bool scripted = args.Automated || headless;
        string notice = char.ToUpperInvariant(reason[0]) + reason.Substring(1) + (reason.EndsWith('.') ? string.Empty : ".");
        string what = duringGame ? "connection lost" : "connect failed";
        string line = scripted
            ? $"[anomaly] {what}: {reason} (a scripted run: exit {ScriptedExitCode})"
            : $"[anomaly] {what}: {reason} (back to the connect screen)";
        return scripted
            ? new ConnectionLoss(true, ScriptedExitCode, notice, line)
            : new ConnectionLoss(false, 0, notice, line);
    }
}
