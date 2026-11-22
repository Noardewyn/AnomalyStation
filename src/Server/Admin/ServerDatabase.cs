using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Admin;

/// <summary>
/// M10 slice I (D76, D275): the server's database, an embedded SQLite file (or memory, without a path): tg's <c>round</c>,
/// <c>player</c>, <c>ban</c> and <c>messages</c> tables (tg's SQL/tgstation_schema.sql) with the columns the port fills, and the
/// key's reconnect token (D275, the port's stand-in for BYOND's hub). The schema grows by <see cref="Migrations"/>, counted in
/// <c>PRAGMA user_version</c>. tg's queries are SQL through rust-g, which is not translated (rules 13): the host runs these in their
/// place, with tg's columns and conditions. Times are the server's local time as <c>yyyy-MM-dd HH:mm:ss</c> (tg's DATETIME, NOW()).
/// </summary>
public sealed class ServerDatabase : IRoundDatabase, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Func<DateTime> _now;

    // Each entry takes the schema from its index to the next version; never edit one that shipped, add a new one.
    private static readonly string[] Migrations =
    {
        // 1: tg's round, player, ban and messages tables (SQL/tgstation_schema.sql), and the key's token (D275)
        """
        CREATE TABLE round (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            initialize_datetime TEXT NOT NULL,
            start_datetime TEXT NULL,
            end_datetime TEXT NULL,
            server_port INTEGER NOT NULL,
            game_mode_result TEXT NULL,
            station_name TEXT NULL);
        CREATE TABLE player (
            ckey TEXT PRIMARY KEY,
            byond_key TEXT NULL,
            firstseen TEXT NOT NULL,
            firstseen_round_id INTEGER NULL,
            lastseen TEXT NOT NULL,
            lastseen_round_id INTEGER NULL,
            ip TEXT NULL,
            token TEXT NOT NULL);
        CREATE INDEX idx_player_ip_ckey ON player (ip, ckey);
        CREATE TABLE ban (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            bantime TEXT NOT NULL,
            round_id INTEGER NULL,
            role TEXT NULL,
            expiration_time TEXT NULL,
            applies_to_admins INTEGER NOT NULL DEFAULT 0,
            reason TEXT NOT NULL,
            ckey TEXT NULL,
            ip TEXT NULL,
            a_ckey TEXT NOT NULL,
            a_ip TEXT NULL,
            who TEXT NOT NULL,
            adminwho TEXT NOT NULL,
            edits TEXT NULL,
            unbanned_datetime TEXT NULL,
            unbanned_ckey TEXT NULL,
            unbanned_ip TEXT NULL,
            unbanned_round_id INTEGER NULL);
        CREATE INDEX idx_ban_isbanned_details ON ban (ckey, ip, role, unbanned_datetime, expiration_time);
        CREATE TABLE messages (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            type TEXT NOT NULL,
            targetckey TEXT NOT NULL,
            adminckey TEXT NOT NULL,
            text TEXT NOT NULL,
            timestamp TEXT NOT NULL,
            round_id INTEGER NULL,
            secret INTEGER NOT NULL,
            expire_timestamp TEXT NULL,
            severity TEXT NULL,
            deleted INTEGER NOT NULL DEFAULT 0,
            deleted_ckey TEXT NULL);
        CREATE INDEX idx_msg_type_ckey_time ON messages (type, targetckey, timestamp, deleted);
        """,
    };

    /// <summary>Opens (and migrates) the database at <paramref name="path"/>; null: a database in memory, gone with the host.</summary>
    public ServerDatabase(string path, int port = 0, Func<DateTime> now = null)
    {
        _now = now ?? (() => DateTime.Now);
        Port = port;
        if (path != null && Path.GetDirectoryName(Path.GetFullPath(path)) is string folder)
        {
            Directory.CreateDirectory(folder);
            TakeOldFile(folder, path);
        }
        _connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path ?? ":memory:", Pooling = false }.ToString());
        _connection.Open();
        Migrate();
    }

    /// <summary>
    /// D347: the shipped config named the database data/ss3d.sqlite before the game was named Anomaly Station. When the new name
    /// (anomaly.sqlite) has no file yet and the old one is beside it, the old file (with SQLite's side files) takes the new name,
    /// so the keys, bans and notes stay.
    /// </summary>
    private static void TakeOldFile(string folder, string path)
    {
        if (!Path.GetFileName(path).Equals("anomaly.sqlite", StringComparison.OrdinalIgnoreCase) || File.Exists(path))
            return;
        string old = Path.Combine(folder, "ss3d.sqlite");
        if (!File.Exists(old))
            return;
        foreach (string side in new[] { "-wal", "-shm", "-journal" })
            if (File.Exists(old + side))
                File.Move(old + side, path + side);
        File.Move(old, path);
    }

    /// <summary>The port the rounds are recorded with (tg's server_port).</summary>
    public int Port { get; }

    /// <summary>The schema version (<c>PRAGMA user_version</c>).</summary>
    public int SchemaVersion => (int)(long)Scalar("PRAGMA user_version");

    /// <summary>The newest schema version this build knows.</summary>
    public static int LatestVersion => Migrations.Length;

    private void Migrate()
    {
        int version = SchemaVersion;
        if (version > Migrations.Length)
            throw new InvalidOperationException($"the database's schema is version {version}, newer than this server's {Migrations.Length}");
        for (; version < Migrations.Length; version++)
        {
            using SqliteTransaction tx = _connection.BeginTransaction();
            Execute(Migrations[version]);
            Execute("PRAGMA user_version = " + (version + 1).ToString(CultureInfo.InvariantCulture));
            tx.Commit();
        }
    }

    /// <summary>The time now as the database writes it.</summary>
    public string Now() => Time(_now());

    private static string Time(DateTime t) => t.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    // ---- the round (tg's SSdbcore, dbcore.dm:325-358) ----

    /// <inheritdoc/>
    public string InitializeRound()
    {
        long id = (long)Scalar("INSERT INTO round (initialize_datetime, server_port) VALUES ($now, $port); SELECT last_insert_rowid();",
            ("$now", Now()), ("$port", Port));
        return id.ToString(CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    public void SetRoundStart(string roundId) => Execute("UPDATE round SET start_datetime = $now WHERE id = $id", ("$now", Now()), ("$id", roundId));

    /// <inheritdoc/>
    public void SetRoundEnd(string roundId, string modeResult, string stationName)
        => Execute("UPDATE round SET end_datetime = $now, game_mode_result = $result, station_name = $station WHERE id = $id",
            ("$now", Now()), ("$result", modeResult), ("$station", stationName), ("$id", roundId));

    // ---- keys (D275; tg's player table, client_procs.dm:719-791) ----

    /// <summary>A key's row: its first spelling, its token, its last address and when it was last seen.</summary>
    public sealed record PlayerRow(string Ckey, string Key, SessionToken Token, string Ip, string LastSeen);

    /// <summary>The row of <paramref name="ckey"/>, or null for a key never seen.</summary>
    public PlayerRow FindPlayer(string ckey)
    {
        using SqliteCommand c = Command("SELECT ckey, byond_key, token, ip, lastseen FROM player WHERE ckey = $ckey", ("$ckey", ckey));
        using SqliteDataReader r = c.ExecuteReader();
        return r.Read() ? new PlayerRow(r.GetString(0), r.IsDBNull(1) ? r.GetString(0) : r.GetString(1), ParseToken(r.GetString(2)), r.IsDBNull(3) ? null : r.GetString(3), r.GetString(4)) : null;
    }

    /// <summary>A key's first connection: its row with its token (tg's INSERT INTO player, client_procs.dm:734).</summary>
    public void AddPlayer(string ckey, string key, SessionToken token, string ip, string roundId)
        => Execute("INSERT INTO player (ckey, byond_key, firstseen, firstseen_round_id, lastseen, lastseen_round_id, ip, token) VALUES ($ckey, $key, $now, $round, $now, $round, $ip, $token)",
            ("$ckey", ckey), ("$key", key), ("$now", Now()), ("$round", roundId), ("$ip", ip), ("$token", FormatToken(token)));

    /// <summary>A key's later connection (tg's UPDATE player SET lastseen ..., client_procs.dm:778).</summary>
    public void UpdatePlayerSeen(string ckey, string ip, string roundId)
        => Execute("UPDATE player SET lastseen = $now, lastseen_round_id = $round, ip = $ip WHERE ckey = $ckey",
            ("$now", Now()), ("$round", roundId), ("$ip", ip), ("$ckey", ckey));

    private static string FormatToken(SessionToken t) => t.Low.ToString("X16", CultureInfo.InvariantCulture) + t.High.ToString("X16", CultureInfo.InvariantCulture);

    private static SessionToken ParseToken(string text)
        => new(ulong.Parse(text.AsSpan(0, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture), ulong.Parse(text.AsSpan(16, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture));

    // ---- bans (tg's sql_ban_system.dm) ----

    /// <summary>A ban row as tg's is_banned_from_with_details lists it.</summary>
    public sealed record BanRow(long Id, string BanTime, string RoundId, string ExpirationTime, long? DurationMinutes, bool AppliesToAdmins,
        string Reason, string Key, string Ip, string AdminKey);

    /// <summary>tg's create_ban for one role (sql_ban_system.dm:549): the new ban's id.</summary>
    public long CreateBan(string role, string ckey, string ip, long? durationMinutes, bool appliesToAdmins, string reason, string adminCkey, string adminIp,
        string who, string adminWho, string roundId)
    {
        DateTime now = _now();
        return (long)Scalar("""
            INSERT INTO ban (bantime, round_id, role, expiration_time, applies_to_admins, reason, ckey, ip, a_ckey, a_ip, who, adminwho)
            VALUES ($now, $round, $role, $expires, $admins, $reason, $ckey, $ip, $a_ckey, $a_ip, $who, $adminwho);
            SELECT last_insert_rowid();
            """,
            ("$now", Time(now)), ("$round", roundId), ("$role", role), ("$expires", durationMinutes is long m ? Time(now.AddMinutes(m)) : null),
            ("$admins", appliesToAdmins ? 1 : 0), ("$reason", reason), ("$ckey", ckey), ("$ip", ip), ("$a_ckey", adminCkey), ("$a_ip", adminIp),
            ("$who", who), ("$adminwho", adminWho));
    }

    /// <summary>tg's is_banned_from_with_details (sql_ban_system.dm:91): the active bans of the role that match the key or the address, newest first.</summary>
    public List<BanRow> ActiveBans(string ckey, string ip, string role)
    {
        if (string.IsNullOrEmpty(ckey) && string.IsNullOrEmpty(ip))
            return new List<BanRow>();
        return Bans("""
            WHERE role = $role AND (ckey = $ckey OR ip = $ip) AND unbanned_datetime IS NULL AND (expiration_time IS NULL OR expiration_time > $now)
            ORDER BY bantime DESC, id DESC
            """, ("$role", role), ("$ckey", ckey), ("$ip", ip), ("$now", Now()));
    }

    /// <summary>The bans listed for an admin (tg's unban panel's search): active ones of the key or address, or all active ones.</summary>
    public List<BanRow> ListBans(string ckey, string ip)
        => Bans("""
            WHERE unbanned_datetime IS NULL AND (expiration_time IS NULL OR expiration_time > $now)
            AND ($ckey IS NULL OR ckey = $ckey) AND ($ip IS NULL OR ip = $ip)
            ORDER BY id
            """, ("$ckey", ckey), ("$ip", ip), ("$now", Now()));

    /// <summary>A ban by id (whether active or not), or null.</summary>
    public BanRow FindBan(long id) => Bans("WHERE id = $id", ("$id", id)).FirstOrDefault();

    /// <summary>tg's unban (sql_ban_system.dm:798): false when the ban does not exist or was already lifted.</summary>
    public bool Unban(long id, string adminCkey, string adminIp, string roundId, string changeMessage)
        => Execute("""
            UPDATE ban SET unbanned_datetime = $now, unbanned_ckey = $a_ckey, unbanned_ip = $a_ip, unbanned_round_id = $round,
            edits = IFNULL(edits, '') || $change WHERE id = $id AND unbanned_datetime IS NULL
            """, ("$now", Now()), ("$a_ckey", adminCkey), ("$a_ip", adminIp), ("$round", roundId), ("$change", changeMessage), ("$id", id)) == 1;

    private List<BanRow> Bans(string where, params (string, object)[] args)
    {
        var rows = new List<BanRow>();
        using SqliteCommand c = Command("""
            SELECT id, bantime, round_id, expiration_time,
                CAST((julianday(expiration_time) - julianday(bantime)) * 1440 + 0.5 AS INTEGER),
                applies_to_admins, reason, IFNULL((SELECT byond_key FROM player WHERE player.ckey = ban.ckey), ban.ckey), ip,
                IFNULL((SELECT byond_key FROM player WHERE player.ckey = ban.a_ckey), ban.a_ckey)
            FROM ban
            """ + "\n" + where, args);
        using SqliteDataReader r = c.ExecuteReader();
        while (r.Read())
        {
            rows.Add(new BanRow(r.GetInt64(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetInt64(2).ToString(CultureInfo.InvariantCulture),
                r.IsDBNull(3) ? null : r.GetString(3), r.IsDBNull(4) ? null : r.GetInt64(4), r.GetInt64(5) != 0, r.GetString(6),
                r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8), r.IsDBNull(9) ? null : r.GetString(9)));
        }
        return rows;
    }

    // ---- notes, watchlist entries (tg's sql_message_system.dm) ----

    /// <summary>A message row as tg's get_message_output reads it (with its type and target for the lists).</summary>
    public sealed record MessageRow(long Id, string Type, string TargetKey, string AdminKey, string Text, string Timestamp, bool Secret);

    /// <summary>tg's create_message (sql_message_system.dm:1): the new row's id.</summary>
    public long CreateMessage(string type, string targetCkey, string adminCkey, string text, bool secret, string severity, string roundId)
        => (long)Scalar("""
            INSERT INTO messages (type, targetckey, adminckey, text, timestamp, round_id, secret, severity)
            VALUES ($type, $target, $admin, $text, $now, $round, $secret, $severity);
            SELECT last_insert_rowid();
            """, ("$type", type), ("$target", targetCkey), ("$admin", adminCkey), ("$text", text), ("$now", Now()), ("$round", roundId),
            ("$secret", secret ? 1 : 0), ("$severity", severity));

    /// <summary>A message that is not deleted, or null.</summary>
    public MessageRow FindMessage(long id) => Messages("WHERE id = $id AND deleted = 0", ("$id", id)).FirstOrDefault();

    /// <summary>tg's delete_message (sql_message_system.dm:111): the row is kept, marked deleted.</summary>
    public bool DeleteMessage(long id, string deletedByCkey)
        => Execute("UPDATE messages SET deleted = 1, deleted_ckey = $by WHERE id = $id AND deleted = 0", ("$by", deletedByCkey), ("$id", id)) == 1;

    /// <summary>tg's get_message_output (sql_message_system.dm:645): the messages of a type for a key, oldest first.</summary>
    public List<MessageRow> GetMessages(string type, string targetCkey, bool showSecret, string afterTimestamp = null)
        => Messages("""
            WHERE type = $type AND deleted = 0 AND (expire_timestamp > $now OR expire_timestamp IS NULL)
            AND (type = 'memo' OR targetckey = $target) AND ($after IS NULL OR timestamp > $after) AND ($secret = 1 OR secret = 0)
            ORDER BY timestamp, id
            """, ("$type", type), ("$now", Now()), ("$target", targetCkey), ("$after", afterTimestamp), ("$secret", showSecret ? 1 : 0));

    /// <summary>Every note and watchlist entry of a key that is not deleted (tg's browse_messages for one key), oldest first.</summary>
    public List<MessageRow> MessagesOf(string targetCkey)
        => Messages("WHERE targetckey = $target AND deleted = 0 ORDER BY timestamp, id", ("$target", targetCkey));

    private List<MessageRow> Messages(string where, params (string, object)[] args)
    {
        var rows = new List<MessageRow>();
        using SqliteCommand c = Command("""
            SELECT id, type, IFNULL((SELECT byond_key FROM player WHERE ckey = targetckey), targetckey),
                IFNULL((SELECT byond_key FROM player WHERE ckey = adminckey), adminckey), text, timestamp, secret
            FROM messages
            """ + "\n" + where, args);
        using SqliteDataReader r = c.ExecuteReader();
        while (r.Read())
            rows.Add(new MessageRow(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetInt64(6) != 0));
        return rows;
    }

    // ---- plumbing ----

    private SqliteCommand Command(string sql, params (string Name, object Value)[] args)
    {
        SqliteCommand c = _connection.CreateCommand();
        c.CommandText = sql;
        foreach ((string name, object value) in args)
            c.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return c;
    }

    private int Execute(string sql, params (string, object)[] args)
    {
        using SqliteCommand c = Command(sql, args);
        return c.ExecuteNonQuery();
    }

    private object Scalar(string sql, params (string, object)[] args)
    {
        using SqliteCommand c = Command(sql, args);
        return c.ExecuteScalar();
    }

    /// <inheritdoc/>
    public void Dispose() => _connection.Dispose();
}
