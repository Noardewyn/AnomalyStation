using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Admin;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server;

// M10 slice I (D275, multiplayer.md 13): the host's admin tools. The server's database (ServerDatabase) answers tg's SQL procs,
// which are not translated (rules 13): the round id (RoundDatabaseSeam), the keys and their tokens, tg's world/IsBanned at
// connection, create_ban, unban, create_message, delete_message, display_unread_notes and scream_about_watchlists, with tg's texts,
// logs and message_admins. The admins are tg's (load_admins from config/admins.txt and config/admin_ranks.txt, the client's holder);
// a player's console command needs the right of tg's matching verb (check_rights_for), unless DEV_COMMANDS or CLIENT_CONSOLE open it
// to everyone. A replay has no database: its round id is the recorded one, and its admin commands do their world part only.
public sealed partial class ServerHost
{
    private ServerDatabase _db;
    private string _adminsText, _adminRanksText;

    /// <summary>The server's database (null in a replay).</summary>
    public ServerDatabase Database => _db;

    private void OpenAdmin()
    {
        // tg's admin files are world input (the admins' datums roll the world's RNG), read once for every round of the run
        _adminsText = ReadConfigText("admins.txt");
        _adminRanksText = ReadConfigText("admin_ranks.txt");
        if (!_replay)
            _db = new ServerDatabase(_config.DatabasePath, _config.Port);
    }

    private string ReadConfigText(string name)
    {
        string path = Path.Combine(_config.ConfigFolder, name);
        return File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal) : null;
    }

    private void InstallAdmin(World world, DmWorldState state)
    {
        if (_adminsText != null)
            state.Files.WriteText("config/admins.txt", _adminsText);
        if (_adminRanksText != null)
            state.Files.WriteText("config/admin_ranks.txt", _adminRanksText);
        RoundDatabaseSeam.Install(world, new RoundRows(this));
    }

    // The round rows of the database; a replay's round id is the recorded run's first (CommandLog.RoundId).
    private sealed class RoundRows(ServerHost host) : IRoundDatabase
    {
        public string InitializeRound()
        {
            string id = host._db != null ? host._db.InitializeRound() : host._replayLog?.RoundId;
            host.Log.RoundId ??= id;
            return id;
        }

        public void SetRoundStart(string roundId) => host._db?.SetRoundStart(roundId);

        public void SetRoundEnd(string roundId, string modeResult, string stationName) => host._db?.SetRoundEnd(roundId, modeResult, stationName);
    }

    /// <summary>The IP address of a transport end point (<c>ADDRESS:PORT</c>), or null for one that names none (a loopback test client).</summary>
    internal static string AddressOf(string endPoint)
    {
        if (!IPEndPoint.TryParse(endPoint ?? "", out IPEndPoint ep))
            return null;
        IPAddress address = ep.Address.IsIPv4MappedToIPv6 ? ep.Address.MapToIPv4() : ep.Address;
        return address.ToString();
    }

    private string RoundId
    {
        get
        {
            using (World.Enter())
                return GLOB.round_id == null ? null : Dm.text("[]", GLOB.round_id);
        }
    }

    // ---- connection: tg's world/IsBanned (IsBanned.dm:9), its database part ----

    // The refusal text of a server ban that matches the key or the address, or null. An admin passes a ban that does not apply to
    // admins (tg logs it and tells the admins).
    private string BanRefusal(string ckey, string address)
    {
        if (_db == null)
            return null;
        using (World.Enter())
        {
            bool admin = GLOB.admin_datums[ckey] != null || Dm.truthy(GLOB.deadmins[ckey]);
            foreach (ServerDatabase.BanRow i in _db.ActiveBans(ckey, address, "Server"))
            {
                if (admin)
                {
                    if (i.AppliesToAdmins)
                    {
                        string msg = Dm.text("Admin [] is admin banned, and has been disallowed access.", ckey);
                        log_admin(msg);
                        message_admins(msg);
                    }
                    else
                    {
                        string msg = Dm.text("Admin [] has been allowed to bypass a matching non-admin ban on [] []-[].", ckey, ckey_of(i.Key), i.Ip, null);
                        log_admin(msg);
                        message_admins(msg);
                        continue;
                    }
                }
                string expires = "This is a permanent ban.";
                if (i.ExpirationTime != null)
                    expires = Dm.text(" The ban is for [] and expires on [] (server time).", DisplayTimeText((float)((i.DurationMinutes ?? 0) * 600)), i.ExpirationTime);
                string desc = Dm.text("You, or another user of this computer or connection ([]) is banned from playing here.\nThe ban reason is: []\nThis ban (BanID #[]) was applied by [] on [] during round ID [].\n[]",
                    i.Key, i.Reason, i.Id.ToString(CultureInfo.InvariantCulture), i.AdminKey, i.BanTime, i.RoundId, expires);
                log_suspicious_login(Dm.text("Failed Login: [] [] [] - Banned (#[])", ckey, null, address, i.Id.ToString(CultureInfo.InvariantCulture)));
                return desc;
            }
        }
        return null;
    }

    private static string ckey_of(string key) => key == null ? null : Dm.ckey(key);

    // ---- after client/New: tg's display_unread_notes and scream_about_watchlists (client_procs.dm:526, :555) ----

    private void AfterClientNew(Session s)
    {
        if (_db == null || s.Client == null)
            return;
        // tg: display_unread_notes(src, time_stamp): the key's notes that are not secret, newer than its last connection
        var text = new System.Text.StringBuilder();
        foreach (ServerDatabase.MessageRow message in _db.GetMessages("note", s.Ckey, showSecret: false, s.LastSeenBefore))
        {
            text.Append(Dm.text("<font color='[]' size='3'><b>Note left by [] on []</b></font>", COLOR_RED, span_prefix(message.AdminKey), message.Timestamp));
            text.Append(Dm.text("<br><font color='[]'>[]</font><br>", COLOR_RED, message.Text));
        }
        if (text.Length > 0)
            to_chat(s.Client, text.ToString());
        // tg: scream_about_watchlists(src)
        foreach (ServerDatabase.MessageRow message in _db.GetMessages("watchlist entry", s.Ckey, showSecret: true))
            message_admins(Dm.text("<font color='[]'><B>Notice: </B></font><font color='[]'>[] has been on the watchlist since [] and has just connected - Reason: []</font>",
                COLOR_RED, COLOR_ADMIN_PINK, key_name_admin(s.Client), message.Timestamp, message.Text));
    }

    // ---- rights ----

    /// <summary>The rights a console verb needs from a player (tg's verb that matches it); 0: any admin.</summary>
    internal static int RightsFor(ConsoleVerb verb, string line) => verb switch
    {
        ConsoleVerb.Spawn => R_SPAWN,                                                       // tg: ADMIN_VERB(spawn_atom, R_SPAWN, ...)
        ConsoleVerb.Teleport or ConsoleVerb.List or ConsoleVerb.Kick => R_ADMIN,            // Jump To Coordinate, the player panel, boot2
        ConsoleVerb.Quit => R_SERVER,                                                       // tg's Shutdown Server
        ConsoleVerb.Status or ConsoleVerb.Hash => R_DEBUG,                                  // the debug tools
        ConsoleVerb.Ban or ConsoleVerb.BanIp or ConsoleVerb.Unban or ConsoleVerb.Bans => R_BAN, // the banning and unbanning panels
        ConsoleVerb.Note or ConsoleVerb.Watch or ConsoleVerb.Notes or ConsoleVerb.NoteDelete => R_ADMIN, // the notes of the player panel
        ConsoleVerb.Tickets => R_ADMIN,                                                     // the tickets' Topic (topic.dm: check_rights(R_ADMIN))
        ConsoleVerb.Pm => 0,                                                                // ADMIN_VERB(cmd_admin_pm_panel, R_NONE, ...)
        ConsoleVerb.CheckAntagonists or ConsoleVerb.TraitorPanel or ConsoleVerb.ComboHud => R_ADMIN, // ADMIN_VERB(check_antagonists | show_traitor_panel | combo_hud, R_ADMIN, ...)
        ConsoleVerb.CheckTeams => R_ADMIN,                                                  // the check_teams Topic (topic.dm:1171: check_rights(R_ADMIN))
        ConsoleVerb.SkillPanel => R_ADMIN,                                                  // ADMIN_VERB(show_skill_panel, R_ADMIN, ...)
        ConsoleVerb.DynamicPanel => R_ADMIN,                                                // ADMIN_VERB(dynamic_panel, R_ADMIN, ...)
        ConsoleVerb.DynamicTester => R_DEBUG,                                               // ADMIN_VERB(dynamic_tester, R_DEBUG, ...)
        ConsoleVerb.Secrets => 0,                                                           // ADMIN_VERB(secrets, R_NONE, ...)
        ConsoleVerb.StationTraits => R_FUN,                                                 // ADMIN_VERB(station_traits_panel, R_FUN, ...)
        ConsoleVerb.Circuits => R_ADMIN,                                                    // ADMIN_VERB(view_all_circuits, R_ADMIN, ...)
        ConsoleVerb.LoadCircuit => R_VAREDIT,                                               // ADMIN_VERB(load_circuit, R_VAREDIT, ...)
        ConsoleVerb.Dev => !DevCommandLine.TryParse(line, out DevCommand dev, out _) ? R_DEBUG : dev.Verb switch
        {
            DevVerb.Shuttle => R_ADMIN, // Call Shuttle
            DevVerb.Reboot => R_SERVER, // Reboot World
            DevVerb.ChangeMap => R_SERVER, // Change Map
            _ => R_DEBUG,               // the debug tools
        },
        _ => 0,
    };

    // Whether a player's client is an admin (tg's client.holder).
    private bool IsAdmin(Session s)
    {
        if (s.Client == null)
            return false;
        using (World.Enter())
            return s.Client.holder != null;
    }

    private const int DevAccessEveryTicks = 10;

    // D322: tells each player whether it may open the dev menu, at the join and when that changes: an admin, or everyone on a server with DEV_COMMANDS on.
    // The commands are checked on their own. A session between rounds has no client yet and waits.
    private void SendDevAccess()
    {
        foreach (Session s in _sessions)
        {
            if (s.Client == null || (s.DevAccessSent != null && World.Tick % DevAccessEveryTicks != 0))
                continue;
            bool allowed = _config.DevCommandsEnabled || IsAdmin(s);
            if (s.DevAccessSent == allowed)
                continue;
            s.DevAccessSent = allowed;
            s.Outbox.Enqueue(new ServerText(DevAccess.Type, allowed ? DevAccess.Allowed : DevAccess.Denied));
        }
    }

    // tg's check_rights for a player's command: true when the player's admin holder has one of the rights; otherwise the player
    // hears tg's refusal.
    private bool CheckRights(Session s, int rights)
    {
        if (HasRights(s, rights))
            return true;
        if (s.Client != null)
            using (World.Enter())
                ReplyTo(s, Dm.text("Error: You do not have sufficient rights to do that. You require one of the following flags:[].", rights2text(rights, " ")));
        return false;
    }

    // tg's check_rights_for without the refusal (a tool that asks over and over, the gas overlay).
    private bool HasRights(Session s, int rights)
    {
        if (s.Client == null)
            return false;
        using (World.Enter())
            return Dm.truthy(check_rights_for(s.Client, rights));
    }

    // ---- the admin's identity in the database and the logs ----

    private string AdminCkey => _consoleRequester?.Ckey ?? ConsoleName; // the console stands in the admin columns by its name

    private string AdminKey => _consoleRequester?.Key ?? ConsoleName;

    private string AdminIp => _consoleRequester?.Address;

    private string ConsoleActorAdmin => _consoleRequester == null ? ConsoleName
        : MobOf(_consoleRequester) is mob m ? Dm.str(key_name_admin(m)) : _consoleRequester.Key;

    private string NoDatabase(string verb)
    {
        Say(verb + ": no database (a replay)");
        return null;
    }

    // ---- bans: tg's create_ban (sql_ban_system.dm:549), unban (:798), notify_all_banned_players (:1079) ----

    private void ApplyBan(in ConsoleCommand command, bool ipOnly)
    {
        string playerKey = null, playerCkey = null, playerIp = null;
        if (ipOnly)
        {
            playerIp = command.Args[0];
        }
        else
        {
            playerKey = command.Args[0];
            playerCkey = Dm.ckey(playerKey);
            ServerDatabase.PlayerRow row = _db?.FindPlayer(playerCkey);
            if (row != null)
            {
                playerKey = row.Key;
                playerIp = row.Ip; // tg's "use the IP of the last connection" (the panel's default)
            }
            else
            {
                Say($"ban: {playerKey}/({playerCkey}) has not been seen before; banning the key alone");
            }
        }
        ConsoleCommand.TryDuration(command.Args[1], out long? duration);
        string reason = command.Rest(2);
        string time_message = duration is long minutes ? minutes.ToString(CultureInfo.InvariantCulture) + " minute" + (minutes > 1 ? "s" : "") : null;
        string roundId = RoundId;
        var clientsOnline = _sessions.Where(x => x.Client != null).ToList();
        string who = string.Join(", ", clientsOnline.Select(x => x.Key));
        string adminwho = string.Join(", ", clientsOnline.Where(IsAdmin).Select(x => x.Key));
        long id = _db?.CreateBan("Server", playerCkey, playerIp, duration, false, reason, AdminCkey, AdminIp, who, adminwho, roundId) ?? 0;

        string target = ban_target_string(playerKey, playerIp);
        string msg = Dm.text("has created a [] server ban for [].", duration == null ? "permanent" : "temporary " + time_message, target);
        log_admin_private(Dm.text("[] [] Reason: []", ConsoleActor, msg, reason));
        message_admins(Dm.text("[] []\nReason: []", ConsoleActorAdmin, msg, reason));
        if (playerCkey != null)
            _db?.CreateMessage("note", playerCkey, AdminCkey, Dm.text("Banned from the server [] - []", duration == null ? "permanently" : "for " + time_message, reason), false, null, roundId);

        string expiry = duration == null ? "permanent." : Dm.text("temporary, it will be removed in [].", time_message);
        string player_ban_notification = span_boldannounce(Dm.text("You have been banned by [] from the server.\nReason: []</span><br>[]", AdminKey, reason, span_danger(Dm.text("This ban is [] The round ID is [].", expiry, roundId))));
        string other_ban_notification = span_boldannounce(Dm.text("Another player sharing your IP or CID has been banned by [] from the server.\nReason: []</span><br>[]", AdminKey, reason, span_danger(Dm.text("This ban is [] The round ID is [].", expiry, roundId))));
        notify_all_banned_players(playerCkey, playerIp, player_ban_notification, other_ban_notification);
        Say(id != 0 ? $"ban #{id.ToString(CultureInfo.InvariantCulture)}: {target}" : $"ban: {target} (no database: the players were told and disconnected only)");
    }

    // tg: sql_ban_system.dm:1012
    private static string ban_target_string(string player_key, string player_ip)
        => player_key ?? (player_ip ?? "NULL") + "/NULL";

    // tg: sql_ban_system.dm:1079  notify_all_banned_players (kick_banned_players for a server ban; applies_to_admins is FALSE: an admin stays)
    private void notify_all_banned_players(string banned_player_ckey, string banned_player_ip, string banned_player_message, string banned_other_message)
    {
        object appeal_url = config.Get(typeof(datum_config_entry_string_banappeals)) ?? "No ban appeal url set!";
        foreach (Session s in _sessions.ToList())
        {
            if (s.Client == null || s.State == SessionState.Closed)
                continue;
            bool own = banned_player_ckey != null && s.Ckey == banned_player_ckey;
            if (!own && (banned_player_ip == null || s.Address != banned_player_ip))
                continue;
            client C = s.Client;
            to_chat(C, span_boldannounce(Dm.text("[]<br><span class='danger'>To appeal this ban go to []", own ? banned_player_message : banned_other_message, appeal_url)), confidential: Dm.TRUE);
            if (C.holder != null || Dm.truthy(GLOB.deadmins[s.Ckey]))
                continue;
            if (s.CloseReason == DisconnectReason.None)
            {
                s.CloseReason = DisconnectReason.Banned;
                s.CloseText = Dm.text("You have been banned by [] from the server.", AdminKey);
            }
            qdel(C);
        }
    }

    private void ApplyUnban(in ConsoleCommand command)
    {
        if (_db == null) { NoDatabase("unban"); return; }
        long id = long.Parse(command.Args[0].TrimStart('#'), NumberStyles.None, CultureInfo.InvariantCulture);
        ServerDatabase.BanRow ban = _db.FindBan(id);
        string target = ban == null ? null : ban_target_string(ban.Key, ban.Ip);
        string change_message = Dm.text("[] unbanned [] from Server on [] during round #[]<hr>", AdminKey, target, _db.Now(), RoundId);
        if (ban == null || !_db.Unban(id, AdminCkey, AdminIp, RoundId, change_message))
        {
            Say($"unban: no active ban #{id.ToString(CultureInfo.InvariantCulture)}");
            return;
        }
        log_admin_private(Dm.text("[] has unbanned [] from Server.", ConsoleActor, target));
        message_admins(Dm.text("[] has unbanned [] from Server.", ConsoleActorAdmin, target));
        foreach (Session s in _sessions)
        {
            if (s.Client == null || s.State == SessionState.Closed)
                continue;
            if (ban.Key != null && s.Ckey == Dm.ckey(ban.Key))
                to_chat(s.Client, span_boldannounce(Dm.text("[] has removed a ban from Server for your key.", AdminKey)), confidential: Dm.TRUE);
            else if (ban.Ip != null && s.Address == ban.Ip)
                to_chat(s.Client, span_boldannounce(Dm.text("[] has removed a ban from Server for your IP or CID.", AdminKey)), confidential: Dm.TRUE);
        }
        Say($"unbanned #{id.ToString(CultureInfo.InvariantCulture)}: {target}");
    }

    private void ApplyBans(in ConsoleCommand command)
    {
        if (_db == null) { NoDatabase("bans"); return; }
        string filter = command.Args.Count > 0 ? command.Args[0] : null;
        bool ip = filter != null && IPAddress.TryParse(filter, out _);
        List<ServerDatabase.BanRow> bans = _db.ListBans(filter == null || ip ? null : Dm.ckey(filter), ip ? filter : null);
        foreach (ServerDatabase.BanRow b in bans)
        {
            Say(string.Create(CultureInfo.InvariantCulture,
                $"  #{b.Id} {b.Key ?? "-"} {b.Ip ?? "-"} by {b.AdminKey} on {b.BanTime} (round {b.RoundId ?? "-"}), {(b.ExpirationTime == null ? "permanent" : "until " + b.ExpirationTime)}: {b.Reason}"));
        }
        Say(string.Create(CultureInfo.InvariantCulture, $"bans: {bans.Count} active"));
    }

    // ---- notes: tg's create_message (sql_message_system.dm:1) and delete_message (:111) ----

    private void ApplyNote(in ConsoleCommand command, string type)
    {
        string target_key = command.Args[0];
        string target_ckey = Dm.ckey(target_key);
        bool secret = type == "watchlist entry" || command.Args[1] == "secret";
        string text = command.Rest(type == "note" && command.Args[1] == "secret" ? 2 : 1);
        if (_db == null) { NoDatabase(type); return; }
        if (_db.FindPlayer(target_ckey) is ServerDatabase.PlayerRow row)
            target_key = row.Key;
        else
            Say($"{(type == "note" ? "note" : "watch")}: {target_key}/({target_ckey}) has not been seen before");
        long id = _db.CreateMessage(type, target_ckey, AdminCkey, text, secret, type == "note" ? "none" : null, RoundId);
        string pm = Dm.text("[] has created a [][] for []: []", ConsoleActor, secret ? "secret " : "", type, target_key, text);
        string header = Dm.text("[] has created a [][] for []", ConsoleActorAdmin, secret ? "secret " : "", type, target_key);
        log_admin_private(pm);
        message_admins(Dm.text("[]:<br>[]", header, text));
        Say($"{type} #{id.ToString(CultureInfo.InvariantCulture)} for {target_key}");
    }

    private void ApplyNotes(in ConsoleCommand command)
    {
        if (_db == null) { NoDatabase("notes"); return; }
        List<ServerDatabase.MessageRow> rows = _db.MessagesOf(Dm.ckey(command.Args[0]));
        foreach (ServerDatabase.MessageRow m in rows)
            Say(string.Create(CultureInfo.InvariantCulture, $"  #{m.Id} {m.Type}{(m.Secret ? " (secret)" : "")} by {m.AdminKey} on {m.Timestamp}: {m.Text}"));
        Say(string.Create(CultureInfo.InvariantCulture, $"notes: {rows.Count} for {command.Args[0]}"));
    }

    private void ApplyNoteDelete(in ConsoleCommand command)
    {
        if (_db == null) { NoDatabase("note-delete"); return; }
        long id = long.Parse(command.Args[0].TrimStart('#'), NumberStyles.None, CultureInfo.InvariantCulture);
        ServerDatabase.MessageRow message = _db.FindMessage(id);
        if (message == null || !_db.DeleteMessage(id, AdminCkey))
        {
            Say($"note-delete: no note #{id.ToString(CultureInfo.InvariantCulture)}");
            return;
        }
        string forOrBy = message.Type is "note" or "message" or "watchlist entry" ? " for" : " made by";
        log_admin_private(Dm.text("[] has deleted a [][] []: []", ConsoleActor, message.Type, forOrBy, message.TargetKey, message.Text));
        message_admins(Dm.text("[] has deleted a [][] []:<br>[]", ConsoleActorAdmin, message.Type, forOrBy, message.TargetKey, message.Text));
        Say($"deleted {message.Type} #{id.ToString(CultureInfo.InvariantCulture)}");
    }
}
