using System.IO;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Server;

/// <summary>The server's options (docs/ai/specs/apps.md section 1): config/server.txt in tg's KEY value style, then the command line.</summary>
public class ServerConfigTests
{
    private static ServerConfig Parse(string[] args, string file = null) => ServerConfig.Parse(args, path => path == "config/server.txt" ? file : null, out _);

    private static string Error(string[] args, string file = null)
    {
        Assert.Null(ServerConfig.Parse(args, path => path == "config/server.txt" ? file : null, out string error));
        return error;
    }

    [Fact]
    public void Defaults()
    {
        ServerConfig c = Parse(new[] { "--ticks", "10" });
        Assert.Equal(7777, c.Port);
        Assert.Equal("127.0.0.1", c.BindAddress);
        Assert.Equal(Limits.DefaultMaxPlayers, c.MaxPlayers);
        Assert.Equal(1UL, c.Seed);
        Assert.Equal(48, c.RoomWidth);
        Assert.Equal(48, c.RoomHeight);
        Assert.False(c.Empty); // the tg world boots by default
        Assert.False(c.Realtime);
        Assert.Equal(0, c.HashEvery); // the per-second hash is off (scope risk 1)
        Assert.Equal(200, c.StatusEveryTicks);
        Assert.Equal(TestRoomLayout.Rooms, c.RoomLayout);  // M2: the rooms layout
        Assert.Equal(0.5f, c.MoveSpeedScale);               // DV70: half of tg's tiles per second
        Assert.Null(c.DevCommands);
        Assert.True(c.DevCommandsEnabled);                  // null on a loopback bind means on (Q9)
        Assert.True(c.Console);
        Assert.Null(c.MetricsFile);
        Assert.Null(c.RecordPath);
        Assert.Null(c.ReplayPath);
        Assert.Equal(10, c.Ticks);
    }

    [Fact]
    public void TheM0OptionsKeepWorking()
    {
        ServerConfig c = Parse(new[] { "--ticks", "200", "--seed", "18446744073709551615", "--realtime", "--smoke", "3", "--boot" });
        Assert.Equal(200, c.Ticks);
        Assert.Equal(ulong.MaxValue, c.Seed);
        Assert.True(c.Realtime);
        Assert.Equal(3, c.SmokeActors);
        Assert.False(c.Empty);
        c = Parse(new[] { "--ticks=5", "--seed=9" });
        Assert.Equal(5, c.Ticks);
        Assert.Equal(9UL, c.Seed);
        Assert.Null(Parse(new[] { "--realtime" }).Ticks); // runs until stopped
    }

    [Fact]
    public void TheNewOptions()
    {
        ServerConfig c = Parse(new[]
        {
            "--ticks", "1", "--port", "0", "--bind", "0.0.0.0", "--max-players", "64", "--room", "32x40", "--empty",
            "--metrics", "m.csv", "--hash-every", "20", "--record", "r.log", "--status-every=100", "--no-console", "--room-layout", "classic", "--move-speed-scale", "0.5", "--dev-commands",
        });
        Assert.Equal(0, c.Port);
        Assert.Equal("0.0.0.0", c.BindAddress);
        Assert.Equal(64, c.MaxPlayers);
        Assert.Equal((32, 40), (c.RoomWidth, c.RoomHeight));
        Assert.True(c.Empty);
        Assert.Equal("m.csv", c.MetricsFile);
        Assert.Equal(20, c.HashEvery);
        Assert.Equal("r.log", c.RecordPath);
        Assert.Equal(100, c.StatusEveryTicks);
        Assert.False(c.Console);
        Assert.Equal(TestRoomLayout.Classic, c.RoomLayout);
        Assert.Equal(0.5f, c.MoveSpeedScale);
        Assert.True(c.DevCommands);
        Assert.True(c.DevCommandsEnabled); // on for a 0.0.0.0 bind because it was asked for
        ServerConfig square = Parse(new[] { "--ticks", "1", "--room", "24" });
        Assert.Equal((24, 24), (square.RoomWidth, square.RoomHeight));
        Assert.Equal(7, Parse(new[] { "--ticks", "1", "--max_players", "7" }).MaxPlayers); // snake case too
        Assert.Equal("x.log", Parse(new[] { "--replay", "x.log" }).ReplayPath);       // a replay needs no --ticks
    }

    [Fact]
    public void NoLobbyTurnsTheConfigFilesLobbyOff()
    {
        Assert.True(Parse(new[] { "--ticks", "1" }, "LOBBY").Lobby);
        Assert.False(Parse(new[] { "--ticks", "1", "--no-lobby" }, "LOBBY").Lobby);
        Assert.False(Parse(new[] { "--ticks", "1", "--lobby", "--no-lobby" }).Lobby);
        ServerConfig file = Parse(new[] { "--ticks", "1" }, "LOBBY\nNO_LOBBY"); // only on the command line: in the file the key is unknown
        Assert.True(file.Lobby);
        Assert.Contains(file.Warnings, w => w.Contains("no_lobby", StringComparison.Ordinal));
    }

    [Fact]
    public void DevCommandsAreOnForALoopbackBindAndOffForTheRestUnlessSet()
    {
        Assert.True(Parse(new[] { "--ticks", "1" }).DevCommandsEnabled);
        Assert.True(Parse(new[] { "--ticks", "1", "--bind", "localhost" }).DevCommandsEnabled);
        Assert.True(Parse(new[] { "--ticks", "1", "--bind", "::1" }).DevCommandsEnabled);
        Assert.False(Parse(new[] { "--ticks", "1", "--bind", "0.0.0.0" }).DevCommandsEnabled);
        Assert.False(Parse(new[] { "--ticks", "1", "--bind", "192.168.1.5" }).DevCommandsEnabled);
        Assert.True(Parse(new[] { "--ticks", "1", "--bind", "0.0.0.0", "--dev-commands" }).DevCommandsEnabled);
        Assert.False(Parse(new[] { "--ticks", "1", "--no-dev-commands" }).DevCommandsEnabled);
        // The file says it, the command line's off wins over the file's on, and a file's 0 turns it off on a loopback bind.
        Assert.False(Parse(new[] { "--ticks", "1" }, "DEV_COMMANDS 0").DevCommandsEnabled);
        Assert.True(Parse(new[] { "--ticks", "1", "--bind", "0.0.0.0" }, "DEV_COMMANDS 1").DevCommandsEnabled);
        Assert.False(Parse(new[] { "--ticks", "1", "--no-dev-commands" }, "DEV_COMMANDS 1").DevCommandsEnabled);
        Assert.True(Parse(new[] { "--ticks", "1", "--bind", "0.0.0.0" }, "DEV_COMMANDS").DevCommandsEnabled); // a bare key is a flag
        // 1.6b: the console's commands from players are off by default, whatever the bind
        Assert.False(Parse(new[] { "--ticks", "1" }).ClientConsole);
        Assert.True(Parse(new[] { "--ticks", "1", "--client-console" }).ClientConsole);
        Assert.True(Parse(new[] { "--ticks", "1" }, "CLIENT_CONSOLE").ClientConsole);
    }

    [Fact]
    public void TheMoveSpeedScaleAndTheRoomLayoutComeFromTheFileToo()
    {
        ServerConfig c = Parse(new[] { "--ticks", "1" }, "MOVE_SPEED_SCALE 0.75\nROOM_LAYOUT Classic");
        Assert.Equal(0.75f, c.MoveSpeedScale);
        Assert.Equal(TestRoomLayout.Classic, c.RoomLayout);
        Assert.Equal(4f, Parse(new[] { "--ticks", "1", "--move-speed-scale", "4" }).MoveSpeedScale);
        Assert.Contains("no_dev_commands", Parse(new[] { "--ticks", "1" }, "NO_DEV_COMMANDS").Warnings.Single()); // a command-line flag only
    }

    [Fact]
    public void TheConfigFileIsReadFirstInTgsStyleAndTheCommandLineWins()
    {
        string file = string.Join('\n',
            "## The M1 server (tg style: KEY value, # comments)",
            "PORT 7000",
            "BIND_ADDRESS 0.0.0.0",
            "  MAX_PLAYERS 12  ",
            "# SEED 99",
            "",
            "REALTIME",
            "HASH_EVERY 40",
            "room_width 30",
            "ROOM_HEIGHT 32",
            "CONSOLE 0",
            "METRICS_FILE logs/metrics.csv",
            "FLY_SPEED 3");
        ServerConfig c = Parse(new[] { "--port", "7001" }, file);
        Assert.Equal(7001, c.Port);              // the command line wins
        Assert.Equal("0.0.0.0", c.BindAddress);
        Assert.Equal(12, c.MaxPlayers);
        Assert.Equal(1UL, c.Seed);               // commented out
        Assert.True(c.Realtime);                 // a bare flag
        Assert.Null(c.Ticks);
        Assert.Equal(40, c.HashEvery);
        Assert.Equal((30, 32), (c.RoomWidth, c.RoomHeight));
        Assert.False(c.Console);
        Assert.Equal("logs/metrics.csv", c.MetricsFile);
        Assert.Equal("config/server.txt", c.ConfigFile);
        Assert.Single(c.Warnings);               // tg logs an unknown setting and carries on
        Assert.Contains("fly_speed", c.Warnings[0]);
        Assert.Contains("config/server.txt:13", c.Warnings[0]);
    }

    [Fact]
    public void ConfigNamesAnotherFile()
    {
        ServerConfig c = ServerConfig.Parse(new[] { "--config", "other.txt" }, path => path == "other.txt" ? "TICKS 30\nSEED 5" : null, out string error);
        Assert.Null(error);
        Assert.Equal(30, c.Ticks);
        Assert.Equal(5UL, c.Seed);
        Assert.Equal("other.txt", c.ConfigFile);
        Assert.Null(ServerConfig.Parse(new[] { "--config=missing.txt", "--ticks", "1" }, _ => null, out error));
        Assert.Contains("missing.txt", error);
    }

    [Fact]
    public void ARealServerWithoutASeedDrawsARandomOneAndAGivenSeedWins()
    {
        Assert.Equal(1UL, ServerConfig.Parse(new[] { "--ticks", "1" }, _ => null, out _).Seed); // tests keep the fixed default
        Assert.Equal(99UL, ServerConfig.Parse(new[] { "--ticks", "1" }, _ => null, out _, () => 99).Seed);
        Assert.Equal(5UL, ServerConfig.Parse(new[] { "--ticks", "1", "--seed", "5" }, _ => null, out _, () => 99).Seed);
        Assert.Equal(7UL, ServerConfig.Parse(new[] { "--ticks", "1" }, p => p == "config/server.txt" ? "SEED 7" : null, out _, () => 99).Seed);
    }

    [Fact]
    public void TheBugReportOptionsComeFromTheCommandLineAndTheFile()
    {
        Assert.Null(Parse(new[] { "--ticks", "1" }).BugReportsFolder);
        ServerConfig c = Parse(new[] { "--ticks", "1", "--bug-reports", "reports", "--bug-report-webhook", "https://example.invalid/hook" });
        Assert.Equal(("reports", "https://example.invalid/hook"), (c.BugReportsFolder, c.BugReportWebhook));
        c = Parse(new[] { "--ticks", "1" }, "BUG_REPORTS data/bug_reports\nBUG_REPORT_WEBHOOK_URL https://example.invalid/x");
        Assert.Equal(("data/bug_reports", "https://example.invalid/x"), (c.BugReportsFolder, c.BugReportWebhook));
    }

    [Fact]
    public void TheSampleConfigInTheRepositoryParses()
    {
        string path = FindRepoFile("config/server.txt");
        ServerConfig c = ServerConfig.Parse(new[] { "--ticks", "1" }, p => p == "config/server.txt" ? File.ReadAllText(path) : null, out string error);
        Assert.Null(error);
        Assert.Empty(c.Warnings);
        Assert.Equal(7777, c.Port);
        Assert.Equal("127.0.0.1", c.BindAddress);
        Assert.Equal(48, c.RoomWidth);
        Assert.Equal(0, c.HashEvery);
        Assert.Equal(TestRoomLayout.Rooms, c.RoomLayout);
        Assert.Equal(0.5f, c.MoveSpeedScale);
        Assert.Null(c.DevCommands);
        Assert.True(c.StationTraits); // gaps R2: tg's station traits, on as tgstation ships them
        Assert.False(ServerConfig.Parse(new[] { "--ticks", "1", "--no-station-traits" }, p => p == "config/server.txt" ? File.ReadAllText(path) : null, out _).StationTraits);
        Assert.True(c.RealDate); // gaps R6 (D332): the round's clock at the real date, with tg's holidays
        Assert.False(ServerConfig.Parse(new[] { "--ticks", "1", "--no-real-date" }, p => p == "config/server.txt" ? File.ReadAllText(path) : null, out _).RealDate);
    }

    private static string FindRepoFile(string relative)
    {
        string dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "AnomalyStation.sln"))) dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        return Path.Combine(dir, relative);
    }

    [Theory]
    [InlineData(new[] { "--ticks", "-1" }, "--ticks expects a non-negative integer, got '-1'")]
    [InlineData(new[] { "--ticks", "ten" }, "--ticks expects a non-negative integer, got 'ten'")]
    [InlineData(new[] { "--ticks" }, "--ticks needs a value")]
    [InlineData(new[] { "--ticks", "1", "--seed", "-3" }, "--seed expects an unsigned 64-bit integer, got '-3'")]
    [InlineData(new[] { "--ticks", "1", "--realtime=yes" }, "--realtime takes no value")]
    [InlineData(new[] { "--ticks", "1", "--boot=yes" }, "--boot takes no value")]
    [InlineData(new[] { "--ticks", "1", "--fps", "30" }, "unknown argument '--fps'")]
    [InlineData(new[] { "--ticks", "1", "-x" }, "unknown argument '-x'")]
    [InlineData(new[] { "--seed", "1" }, "--ticks N is required")]
    [InlineData(new[] { "--ticks", "1", "--port", "70000" }, "--port expects an integer from 0 to 65535")]
    [InlineData(new[] { "--ticks", "1", "--room", "65" }, "the room is 3 to 64 turfs on a side")]
    [InlineData(new[] { "--ticks", "1", "--room", "2x10" }, "the room is 3 to 64 turfs on a side")]
    [InlineData(new[] { "--ticks", "1", "--room", "big" }, "--room expects N or WxH")]
    [InlineData(new[] { "--ticks", "1", "--max-players", "0" }, "--max-players expects an integer from 1")]
    [InlineData(new[] { "--ticks", "1", "--empty", "--boot" }, "--empty and --boot exclude each other")]
    [InlineData(new[] { "--replay", "a", "--record", "b" }, "--replay and --record exclude each other")]
    [InlineData(new[] { "--replay", "a", "--realtime" }, "--replay runs fast-forward")]
    [InlineData(new[] { "--ticks", "1", "--move-speed-scale", "0" }, "--move-speed-scale expects a number above 0 and up to 4")]
    [InlineData(new[] { "--ticks", "1", "--move-speed-scale", "4.5" }, "--move-speed-scale expects a number above 0 and up to 4")]
    [InlineData(new[] { "--ticks", "1", "--move-speed-scale", "fast" }, "--move-speed-scale expects a number")]
    [InlineData(new[] { "--ticks", "1", "--move-speed-scale", "-1" }, "--move-speed-scale expects a number")]
    [InlineData(new[] { "--ticks", "1", "--room-layout", "maze" }, "--room-layout expects rooms or classic")]
    [InlineData(new[] { "--ticks", "1", "--dev-commands=yes" }, "--dev-commands takes no value")]
    [InlineData(new[] { "--ticks", "1", "--move-pace-ticks", "3" }, "unknown argument '--move-pace-ticks'")]
    public void RejectsBadArguments(string[] args, string expected)
    {
        Assert.Contains(expected, Error(args));
    }

    [Fact]
    public void BadValuesInTheFileAreErrorsWithTheirLine()
    {
        Assert.Contains("config/server.txt:2: realtime expects 0 or 1", Error(new[] { "--ticks", "1" }, "PORT 1\nREALTIME maybe"));
        Assert.Contains("config/server.txt:1: port needs a value", Error(new[] { "--ticks", "1" }, "PORT"));
        Assert.Contains("--port expects", Error(new[] { "--ticks", "1" }, "PORT abc"));
    }

    [Fact]
    public void HelpReturnsNoConfigAndNoError()
    {
        Assert.Null(ServerConfig.Parse(new[] { "--help" }, _ => null, out string error));
        Assert.Null(error);
        Assert.Contains("--hash-every", ServerConfig.Usage);
        Assert.Contains("--replay", ServerConfig.Usage);
    }
}
