using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Anomaly.Core.DmShim;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server;

// D335: the server's side of the launcher. Its status for the launcher's list (STATUS: the name, the description, the players, the
// round's time and the client build this server's players run), and the launcher's login (HUB_KEY): the Hello's login token names
// the player in place of a typed name. Neither is on unless the config asks; a server without them takes names as before.
public sealed partial class ServerHost
{
    private StatusEndpoint _status;
    private string _clientBuildJson;
    private ECDsa _hubKey;

    // Login tokens that have joined, until they expire: a token read off the wire is worth nothing once its owner is in.
    private readonly Dictionary<string, long> _usedLogins = new(StringComparer.Ordinal);

    /// <summary>The TCP port of the status answer (the game's port number), or 0 when the server gives none.</summary>
    public int StatusPort => _status?.Port ?? 0;

    private void StartLauncherSide()
    {
        if (_config.HubKeyFile != null)
        {
            // A key that cannot be read stops the start: the server must not run open when it was told to take logins only.
            _hubKey = ECDsa.Create();
            _hubKey.ImportFromPem(File.ReadAllText(_config.HubKeyFile));
            _out.WriteLine("players join through the launcher's login (for " + string.Join(", ", _config.PublicAddresses) + ")");
        }
        if (_config.ClientBuildFile != null)
        {
            _clientBuildJson = File.ReadAllText(_config.ClientBuildFile);
            using JsonDocument checkedJson = JsonDocument.Parse(_clientBuildJson);
        }
        if (_config.Status)
        {
            _status = new StatusEndpoint(_config.BindAddress, _config.StatusPort ?? _transport.Port);
            UpdateStatus(inWorld: false);
            _out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"status on http://{_config.BindAddress}:{_status.Port}/status"));
        }
    }

    private void StopLauncherSide()
    {
        _status?.Dispose();
        _hubKey?.Dispose();
    }

    /// <summary>The status the next callers get: once a second from the command phase, so the listener's thread never touches the world.</summary>
    private void UpdateStatus(bool inWorld = true)
    {
        if (_status == null) return;
        int? round = null;
        string map = "";
        if (inWorld && !_config.Empty && MasterInitialized())
        {
            map = SSmapping.current_map?.map_name ?? ""; // tg's status topic: .["map_name"]
            if (Dm.truthy(SSticker.IsRoundInProgress()))
                round = Math.Max(0, (int)((World.Time - SSticker.round_start_time) / 10)); // world.time is in deciseconds
        }
        _status.Json = StatusEndpoint.Compose(_config.ServerName, _config.ServerDescription, _config.ServerMode, map, LiveSessionCount(), _config.MaxPlayers,
            round, _clientBuildJson);
    }

    /// <summary>The account a Hello's login token names, or null: no token, not the hub's, for another server, expired, or used before.</summary>
    private string LoginName(string token, out long expires)
    {
        expires = 0;
        if (_hubKey == null || token == null) return null;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (_usedLogins.TryGetValue(token, out long usedUntil) && usedUntil > now) return null;
        return LoginToken.TryVerify(_hubKey, token, _config.PublicAddresses, now, out string name, out expires) ? name : null;
    }

    private void MarkLoginUsed(string token, long expires)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (_usedLogins.Count > 64)
        {
            var over = new List<string>();
            foreach (var used in _usedLogins)
                if (used.Value <= now) over.Add(used.Key);
            foreach (string one in over) _usedLogins.Remove(one);
        }
        _usedLogins[token] = expires;
    }
}
