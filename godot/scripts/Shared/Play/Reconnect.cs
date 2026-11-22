using System.Globalization;
using System.IO;
using System.Text;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

/// <summary>
/// M3 R4 (scope 2j, the owner's "I cannot come back with the same character"): the reconnect tokens the client keeps across
/// launches, one per server address and name, in a small text file (the connect screen's is <c>user://reconnect-tokens.txt</c>;
/// tests give their own path). The server takes a known name back only with its session's token (tg's reconnect to an SSD
/// mob); the token comes in Welcome, so a client that crashed or quit and is launched again sends it and gets its character
/// back. Engine-free; a file that cannot be read or written only loses the tokens (the next join of a known name is then
/// refused as NameInUse, and the connect screen says why).
/// </summary>
public sealed class ReconnectTokens
{
    /// <summary>The most entries the file keeps (the oldest go first).</summary>
    public const int MaxEntries = 64;

    /// <summary>The largest file read (bytes); a bigger one is ignored.</summary>
    public const int MaxFileBytes = 64 * 1024;

    private readonly string _path;
    private readonly List<(string Key, SessionToken Token)> _entries = new();

    /// <summary>The store at <paramref name="path"/>; null keeps the tokens in memory only.</summary>
    public ReconnectTokens(string path)
    {
        _path = path;
        Load();
    }

    /// <summary>The file, or null for a store in memory.</summary>
    public string Path => _path;

    /// <summary>The number of entries.</summary>
    public int Count => _entries.Count;

    /// <summary>The token kept for <paramref name="name"/> on <paramref name="server"/> (host:port), or none.</summary>
    public SessionToken Get(string server, string name)
    {
        string key = KeyOf(server, name);
        foreach ((string k, SessionToken t) in _entries)
            if (k == key) return t;
        return SessionToken.None;
    }

    /// <summary>Keeps <paramref name="token"/> for <paramref name="name"/> on <paramref name="server"/> and saves the file.</summary>
    public void Set(string server, string name, SessionToken token)
    {
        if (token.IsNone)
        {
            Forget(server, name);
            return;
        }
        string key = KeyOf(server, name);
        _entries.RemoveAll(e => e.Key == key);
        _entries.Add((key, token));
        while (_entries.Count > MaxEntries) _entries.RemoveAt(0);
        Save();
    }

    /// <summary>Drops the token of <paramref name="name"/> on <paramref name="server"/> and saves the file.</summary>
    public void Forget(string server, string name)
    {
        string key = KeyOf(server, name);
        if (_entries.RemoveAll(e => e.Key == key) > 0) Save();
    }

    /// <summary>
    /// The entry key: the server as given (host:port, lower case) and the name as the server compares names (BYOND's
    /// <c>ckey</c>: letters and digits, lower case, and '@').
    /// </summary>
    public static string KeyOf(string server, string name)
    {
        var sb = new StringBuilder((server?.Length ?? 0) + 1 + (name?.Length ?? 0));
        sb.Append((server ?? "").Trim().ToLowerInvariant()).Append('|');
        foreach (char c in name ?? "")
            if (char.IsAsciiLetterOrDigit(c) || c == '@') sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    private void Load()
    {
        if (_path == null) return;
        try
        {
            var info = new FileInfo(_path);
            if (!info.Exists || info.Length > MaxFileBytes) return;
            foreach (string line in File.ReadAllLines(_path))
            {
                string[] parts = line.Split('\t');
                if (parts.Length != 3 || parts[0].Length == 0 || parts[0].Length > 300) continue;
                if (!ulong.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong low)) continue;
                if (!ulong.TryParse(parts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong high)) continue;
                var token = new SessionToken(low, high);
                if (token.IsNone) continue;
                _entries.RemoveAll(e => e.Key == parts[0]);
                _entries.Add((parts[0], token));
            }
            while (_entries.Count > MaxEntries) _entries.RemoveAt(0);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _entries.Clear();
        }
    }

    private void Save()
    {
        if (_path == null) return;
        try
        {
            var sb = new StringBuilder();
            foreach ((string key, SessionToken token) in _entries)
                sb.Append(key).Append('\t').Append(token.Low.ToString("x16", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(token.High.ToString("x16", CultureInfo.InvariantCulture)).Append('\n');
            string dir = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string temp = _path + ".tmp";
            File.WriteAllText(temp, sb.ToString());
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A store that cannot be written keeps its tokens for this launch only.
        }
    }
}

/// <summary>
/// M3 R4: one join of the connect screen with the kept reconnect token (engine-free, so the headless tests run the screen's
/// logic). It connects with the token kept for the server and name; once the game plays it keeps the session's token; a
/// <see cref="DisconnectReason.BadToken"/> refusal of a kept token (the server restarted, or the name was taken back with
/// another token) forgets it and connects once more without one, as a new player or to a NameInUse refusal that
/// <see cref="Advice"/> explains.
/// </summary>
public sealed class JoinAttempt : IDisposable
{
    private readonly Func<IClientTransport> _transport;
    private readonly ReconnectTokens _tokens;
    private readonly string _loginToken;
    private readonly string _server;
    private bool _usedKeptToken;
    private bool _retried;
    private bool _stored;
    private int _notReadyTries;
    private double _retryAt = -1;

    /// <summary>M10 slice H: how many times a <see cref="DisconnectReason.NotReady"/> refusal (a new round's world still booting) is tried again, every <see cref="NotReadyRetrySeconds"/>.</summary>
    public const int MaxNotReadyTries = 60;

    /// <summary>The wait before trying a <see cref="DisconnectReason.NotReady"/> refusal again.</summary>
    public const double NotReadyRetrySeconds = 2;

    /// <param name="loginToken">D335: the launcher's game token for this server, or null; every try sends it (<see cref="GameClient"/>).</param>
    public JoinAttempt(Func<IClientTransport> transport, ReconnectTokens tokens, string host, int port, string name, string loginToken = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _loginToken = loginToken;
        _tokens = tokens ?? new ReconnectTokens(null);
        Host = host;
        Port = port;
        Name = name;
        _server = string.Create(CultureInfo.InvariantCulture, $"{host}:{port}");
        Start(_tokens.Get(_server, name));
    }

    public string Host { get; }
    public int Port { get; }
    public string Name { get; }

    /// <summary>The client of the current try (a new one after a retry).</summary>
    public GameClient Client { get; private set; }

    /// <summary>Whether the try that runs sends a kept token.</summary>
    public bool UsedKeptToken => _usedKeptToken;

    /// <summary>Whether a refused kept token was dropped and the join tried again without it.</summary>
    public bool Retried => _retried;

    /// <summary>Polls the client: keeps the token once the game plays; retries once after a refused kept token.</summary>
    public FeedPhase Poll(double now)
    {
        if (_retryAt >= 0)
        {
            if (now < _retryAt)
                return FeedPhase.Connecting;
            _retryAt = -1;
            Client.Dispose();
            Start(_tokens.Get(_server, Name));
        }
        Client.Poll(now);
        if (Client.Phase == FeedPhase.Playing && !_stored)
        {
            _stored = true;
            _tokens.Set(_server, Name, Client.Token);
        }
        else if (Client.Phase == FeedPhase.Closed && Client.CloseCode == DisconnectReason.BadToken && _usedKeptToken && !_retried)
        {
            _retried = true;
            _tokens.Forget(_server, Name);
            Client.Dispose();
            Start(SessionToken.None);
            Client.Poll(now);
        }
        else if (Client.Phase == FeedPhase.Closed && Client.CloseCode == DisconnectReason.NotReady && _notReadyTries < MaxNotReadyTries)
        {
            // M10 slice H: the server is starting (the next round's world boots after tg's world/Reboot): try again shortly
            _notReadyTries++;
            _retryAt = now + NotReadyRetrySeconds;
            return FeedPhase.Connecting;
        }
        return Client.Phase;
    }

    /// <summary>The connect screen's text for a closed join: the client's reason, and for a refusal what to do about it.</summary>
    public string Advice => Explain(Client.CloseReason, Client.CloseCode, Name);

    /// <summary>The connect screen's text for a connection that ended with <paramref name="reason"/> (<paramref name="code"/>).</summary>
    public static string Explain(string reason, DisconnectReason code, string name) => code switch
    {
        DisconnectReason.NameInUse =>
            $"{reason}. The server already has a character called \"{name}\": it is taken back only by the client that played it " +
            "(the client keeps its reconnect token in its user data folder; the server keeps the name for it across restarts). Choose " +
            "another name, or connect from the computer and user account that played it.",
        DisconnectReason.BadToken =>
            $"{reason}. Choose another name.",
        DisconnectReason.LoginRequired =>
            $"{reason}. Start the game from the launcher: log in there and pick this server.",
        _ => reason,
    };

    private void Start(SessionToken token)
    {
        _usedKeptToken = !token.IsNone;
        Client = new GameClient(_transport(), Host, Port, Name, token, _loginToken);
        Client.Connect();
    }

    public void Dispose() => Client?.Dispose();
}
