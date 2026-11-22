using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Admin;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M10 slice I (D275) through the real server: a key is claimed by its first connection and kept across a restart of the server on
/// one database file; tg's server bans refuse a key and an address with tg's text until they expire or are lifted, and disconnect
/// the banned player; an admin of tg's admins.txt passes a ban that does not apply to admins and types the console's commands with
/// the rights of its rank; a note reaches the player at the next connection and a watchlist entry tells the admins; the database
/// counts the rounds.
/// </summary>
public sealed class AdminGameplayTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "anomaly-admin-" + Guid.NewGuid().ToString("N"));

    public AdminGameplayTests()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "admin_ranks.txt"), """
            Name = Moderator
            Include = ADMIN
            Exclude =
            Edit =

            Name = Host
            Include = EVERYTHING
            Exclude =
            Edit = EVERYTHING
            """.Replace("\r\n", "\n"));
        File.WriteAllText(Path.Combine(_folder, "admins.txt"), "Boss = Host\nMod = Moderator\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private ServerConfig Config(string database = null) => new()
    {
        Seed = 7, RoomWidth = 24, RoomHeight = 24, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f,
        DevCommands = false, ConfigFile = Path.Combine(_folder, "server.txt"), DatabasePath = database,
    };

    private static GameClient Client(RealHeadlessWorld w, string name, string address = null, SessionToken token = default)
    {
        LoopbackClientTransport transport = w.Network.CreateClient();
        transport.Address = address;
        return new GameClient(transport, "127.0.0.1", w.Server.Port, name, token);
    }

    private static (RealHeadlessWorld.Player Player, List<string> Texts) Join(RealHeadlessWorld w, string name, string address = null)
    {
        GameClient client = Client(w, name, address);
        var texts = new List<string>();
        client.ServerTextReceived += t => texts.Add(t.Html);
        Assert.Equal(FeedPhase.Playing, w.Connect(client));
        return (w.Adopt(name, client), texts);
    }

    private static void Console(RealHeadlessWorld w, string line)
    {
        w.Host.EnqueueConsoleLine(line);
        w.Run(3);
    }

    private static string RoundId(RealHeadlessWorld w)
    {
        using (w.World.Enter())
            return Dm.text("[]", GLOB.round_id);
    }

    // D322: the dev menu is an admin's on a server with the dev commands off
    [Fact]
    public void Only_an_admin_may_open_the_dev_menu_and_it_ends_with_the_rights()
    {
        using var w = new RealHeadlessWorld(config: Config());
        (RealHeadlessWorld.Player boss, _) = Join(w, "Boss");
        (RealHeadlessWorld.Player plain, _) = Join(w, "Plain");
        w.Run(15);
        Assert.True(boss.Client.DevAccessAllowed);
        Assert.False(plain.Client.DevAccessAllowed);

        using (w.World.Enter())
            ((client)GLOB.directory["boss"]).holder.deactivate();
        w.Run(15);
        Assert.False(boss.Client.DevAccessAllowed);
    }

    // D322: with DEV_COMMANDS on the dev menu is everyone's, as the commands are
    [Fact]
    public void Everyone_may_open_the_dev_menu_on_a_server_with_the_dev_commands_on()
    {
        using var w = new RealHeadlessWorld(config: new ServerConfig
        {
            Seed = 7, RoomWidth = 24, RoomHeight = 24, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f,
            DevCommands = true, ConfigFile = Path.Combine(_folder, "server.txt"),
        });
        (RealHeadlessWorld.Player plain, _) = Join(w, "Plain");
        w.Run(15);
        Assert.True(plain.Client.DevAccessAllowed);
    }

    // DV366: tg's default toggles would deadmin an admin in a head job, and the port has no readmin
    [Fact]
    public void An_admin_who_takes_a_head_job_stays_an_admin()
    {
        using var w = new RealHeadlessWorld(config: Config(), strict: false);
        Join(w, "Boss", "10.1.2.3"); // not localhost, which tg never deadmins
        w.Run(3);
        using (w.World.Enter())
        {
            var boss = (client)GLOB.directory["boss"];
            Assert.NotNull(boss.holder);
            SSjob.handle_auto_deadmin_roles(boss, "Captain");
            Assert.NotNull(boss.holder);

            // the path is live: the toggle tg sets by default would have deadminned this admin
            boss.prefs.toggles = (int)boss.prefs.toggles | Anomaly.Core.Tg.Defines.DEADMIN_POSITION_HEAD;
            SSjob.handle_auto_deadmin_roles(boss, "Captain");
            Assert.Null(boss.holder);
        }
    }

    [Fact]
    public void A_key_is_claimed_by_its_first_connection_and_kept_across_a_restart_of_the_server()
    {
        string database = Path.Combine(_folder, "data", "anomaly.sqlite");
        SessionToken token;
        using (var first = new RealHeadlessWorld(config: Config(database)))
        {
            RealHeadlessWorld.Player alice = first.Join("Alice");
            token = alice.Client.Token;
            Assert.Equal("1", RoundId(first));
        }
        using var second = new RealHeadlessWorld(config: Config(database));
        Assert.Equal("2", RoundId(second));
        Assert.Equal(ServerDatabase.LatestVersion, second.Host.Database.SchemaVersion);

        GameClient stranger = Client(second, "alice");
        Assert.Equal(FeedPhase.Closed, second.Connect(stranger));
        Assert.Equal(DisconnectReason.NameInUse, stranger.CloseCode);

        GameClient owner = Client(second, "Alice", token: token);
        Assert.Equal(FeedPhase.Playing, second.Connect(owner));
        second.Adopt("Alice", owner);
    }

    [Fact]
    public void A_server_ban_disconnects_the_key_and_refuses_it_and_its_address_with_tgs_text_until_it_is_lifted()
    {
        using var w = new RealHeadlessWorld(config: Config(), strict: false);
        (RealHeadlessWorld.Player bob, List<string> bobTexts) = Join(w, "Bob", "10.1.2.3");
        SessionToken token = bob.Client.Token;

        Console(w, "ban Bob perm griefing the bar");
        Assert.Equal(FeedPhase.Closed, bob.Client.Phase);
        Assert.Equal(DisconnectReason.Banned, bob.Client.CloseCode);
        bob.Gone = true;
        Assert.Contains(bobTexts, t => t.Contains("You have been banned by the server console from the server.", StringComparison.Ordinal) && t.Contains("griefing the bar", StringComparison.Ordinal));
        Assert.Contains("ban #1: Bob", w.Out.ToString(), StringComparison.Ordinal);
        // tg's create_ban leaves a note on the key
        Assert.Contains(w.Host.Database.MessagesOf("bob"), m => m.Type == "note" && m.Text == "Banned from the server permanently - griefing the bar");

        GameClient again = Client(w, "Bob", "10.1.2.3", token);
        Assert.Equal(FeedPhase.Closed, w.Connect(again));
        Assert.Equal(DisconnectReason.Banned, again.CloseCode);
        Assert.Contains("You, or another user of this computer or connection (Bob) is banned from playing here.", again.CloseReason, StringComparison.Ordinal);
        Assert.Contains("The ban reason is: griefing the bar", again.CloseReason, StringComparison.Ordinal);
        Assert.True(again.CloseReason.Contains("This ban (BanID #1) was applied by the server console", StringComparison.Ordinal), again.CloseReason);
        Assert.Contains("This is a permanent ban.", again.CloseReason, StringComparison.Ordinal);

        // the key's last address is banned with it (tg's "use the last connection"), another address is not
        GameClient sameMachine = Client(w, "Bobby", "10.1.2.3");
        Assert.Equal(FeedPhase.Closed, w.Connect(sameMachine));
        Assert.Equal(DisconnectReason.Banned, sameMachine.CloseCode);
        Join(w, "Carl", "10.9.9.9");

        Console(w, "unban 1");
        Assert.Contains("unbanned #1: Bob", w.Out.ToString(), StringComparison.Ordinal);
        GameClient back = Client(w, "Bob", "10.1.2.3", token);
        Assert.Equal(FeedPhase.Playing, w.Connect(back));
        w.Adopt("Bob", back);
    }

    [Fact]
    public void An_ip_ban_refuses_every_key_of_the_address_and_says_how_long_it_lasts()
    {
        using var w = new RealHeadlessWorld(config: Config(), strict: false);
        (RealHeadlessWorld.Player carol, _) = Join(w, "Carol", "10.0.0.7");
        Console(w, "ban-ip 10.0.0.7 2h ban evasion");
        Assert.Equal(DisconnectReason.Banned, carol.Client.CloseCode);
        carol.Gone = true;

        GameClient dave = Client(w, "Dave", "10.0.0.7");
        Assert.Equal(FeedPhase.Closed, w.Connect(dave));
        Assert.Equal(DisconnectReason.Banned, dave.CloseCode);
        Assert.Single(w.Host.Database.ListBans(null, "10.0.0.7"));
        Assert.Contains("The ban is for 2 hours and expires on", dave.CloseReason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_temporary_ban_expires_with_the_servers_clock()
    {
        var now = new DateTime(2026, 10, 26, 12, 0, 0);
        using var db = new ServerDatabase(null, now: () => now);
        long id = db.CreateBan("Server", "bob", null, 30, false, "spam", "console", null, "", "", "1");
        Assert.Equal(30, Assert.Single(db.ActiveBans("bob", null, "Server")).DurationMinutes);
        now = now.AddMinutes(29);
        Assert.Single(db.ActiveBans("bob", null, "Server"));
        now = now.AddMinutes(2);
        Assert.Empty(db.ActiveBans("bob", null, "Server"));
        Assert.Equal(id, db.FindBan(id).Id);
    }

    [Fact]
    public void An_admin_passes_a_ban_and_types_the_commands_its_rank_allows()
    {
        using var w = new RealHeadlessWorld(config: Config(), strict: false);
        (RealHeadlessWorld.Player boss, List<string> bossTexts) = Join(w, "Boss");
        (RealHeadlessWorld.Player mod, List<string> modTexts) = Join(w, "Mod");
        (RealHeadlessWorld.Player pleb, List<string> plebTexts) = Join(w, "Pleb");

        pleb.Session.DevCommand("list");
        w.Run(3);
        Assert.Contains(plebTexts, t => t.Contains("Dev commands are off on this server.", StringComparison.Ordinal));

        mod.Session.DevCommand("ban Pleb perm testing");
        w.Run(3);
        Assert.Contains(modTexts, t => t.Contains("You require one of the following flags: +BAN.", StringComparison.Ordinal));
        Assert.Equal(FeedPhase.Playing, pleb.Client.Phase);
        mod.Session.DevCommand("list");
        w.Run(3);
        Assert.Contains(modTexts, t => t.Contains("list: 3 session(s)", StringComparison.Ordinal));

        // the Host's ban of the moderator: an admin stays (the ban does not apply to admins) and connects again past it
        boss.Session.DevCommand("ban Mod 1d testing");
        w.Run(3);
        Assert.Contains(bossTexts, t => t.Contains("has created a temporary 1440 minutes server ban for Mod.", StringComparison.Ordinal));
        Assert.Equal(FeedPhase.Playing, mod.Client.Phase);
        Assert.Contains(modTexts, t => t.Contains("You have been banned by Boss from the server.", StringComparison.Ordinal));
        GameClient modAgain = Client(w, "Mod", token: mod.Client.Token);
        mod.Gone = true;
        Assert.Equal(FeedPhase.Playing, w.Connect(modAgain));
        Assert.Contains(bossTexts, t => t.Contains("has been allowed to bypass a matching non-admin ban on mod", StringComparison.Ordinal));
    }

    [Fact]
    public void An_admin_adds_gas_and_sees_it_on_the_gas_overlay_where_the_players_dev_tools_are_off()
    {
        using var w = new RealHeadlessWorld(config: Config(), strict: false);
        (RealHeadlessWorld.Player boss, _) = Join(w, "Boss");
        (RealHeadlessWorld.Player pleb, List<string> plebTexts) = Join(w, "Pleb");
        var bossReports = new List<DevGasReport>();
        var plebReports = new List<DevGasReport>();
        boss.Client.DevGasReportReceived += r => bossReports.Add(r);
        pleb.Client.DevGasReportReceived += r => plebReports.Add(r);
        (int x, int y, int z) = (12, 12, 1);
        float Plasma()
        {
            using (w.World.Enter())
                return Dm.num(((turf_open)Builtins.locate(x, y, z)).air.moles[typeof(datum_gas_plasma)]);
        }

        pleb.Session.DevCommand($"air add plasma 50 {x} {y} {z}");
        w.Run(3);
        Assert.Contains(plebTexts, t => t.Contains("Dev commands are off on this server.", StringComparison.Ordinal));
        Assert.Equal(0f, Plasma());

        boss.Session.DevCommand($"air add plasma 50 {x} {y} {z}");
        w.Run(3);
        Assert.True(Plasma() > 0f, "the admin's plasma is in the air");

        boss.Session.DevGasProbe(x, y, z);
        pleb.Session.DevGasProbe(x, y, z);
        w.Run(3);
        DevGasReport report = Assert.Single(bossReports);
        Assert.Contains(report.Gases, g => g.Id == "plasma");
        Assert.Empty(plebReports);
        Assert.Contains(plebTexts, t => t.Contains("no gas overlay", StringComparison.Ordinal));
    }

    [Fact]
    public void A_note_reaches_the_player_at_the_next_connection_and_a_watchlist_entry_tells_the_admins()
    {
        using var w = new RealHeadlessWorld(config: Config(), strict: false);
        (_, List<string> bossTexts) = Join(w, "Boss");
        Console(w, "note Eve please be nice to the clown");
        Console(w, "note Eve secret only the admins see this");
        Console(w, "watch Eve stole the captain's spare");
        Assert.Contains(bossTexts, t => t.Contains("has created a note for Eve", StringComparison.Ordinal) && t.Contains("please be nice to the clown", StringComparison.Ordinal));

        (_, List<string> eveTexts) = Join(w, "Eve");
        Assert.Contains(eveTexts, t => t.Contains("Note left by", StringComparison.Ordinal) && t.Contains("please be nice to the clown", StringComparison.Ordinal));
        Assert.DoesNotContain(eveTexts, t => t.Contains("only the admins see this", StringComparison.Ordinal));
        Assert.DoesNotContain(eveTexts, t => t.Contains("captain's spare", StringComparison.Ordinal));
        Assert.Contains(bossTexts, t => t.Contains("has been on the watchlist since", StringComparison.Ordinal) && t.Contains("stole the captain's spare", StringComparison.Ordinal));

        Console(w, "notes Eve");
        Assert.Contains("notes: 3 for Eve", w.Out.ToString(), StringComparison.Ordinal);
        Console(w, "note-delete 2");
        Assert.Equal(2, w.Host.Database.MessagesOf("eve").Count);
    }

    [Fact]
    public void The_database_counts_the_rounds_across_a_restart_and_the_log_keeps_the_first()
    {
        using var w = new RealHeadlessWorld(config: Config(), strict: false);
        Assert.Equal("1", RoundId(w));
        using (w.World.Enter())
            DmWorldState.Current.RebootRequested = true;
        for (int i = 0; i < 400 && w.Host.Restarts == 0; i++)
            w.Run(1);
        Assert.Equal(1, w.Host.Restarts);
        for (int i = 0; i < 400 && !w.Host.JoinsOpen; i++)
            w.Run(1);
        Assert.Equal("2", RoundId(w));
        Assert.Equal("1", w.Host.Log.RoundId);
    }

    [Fact]
    public void The_command_log_carries_the_round_id_a_replay_gives_its_world()
    {
        CommandLog log = CommandLog.For(Config());
        log.RoundId = "7";
        log.Finish(10, 0xAB);
        CommandLog parsed = CommandLog.Parse(log.ToText(), out string error);
        Assert.Null(error);
        Assert.Equal("7", parsed.RoundId);
    }
}
