using System.IO;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Server;

/// <summary>The replay log's text format (CommandLog.cs).</summary>
public class CommandLogTests
{
    [Fact]
    public void TheTextRoundTrips()
    {
        var log = CommandLog.For(new ServerConfig { Seed = 42, RoomWidth = 30, RoomHeight = 20, SmokeActors = 2, RoomLayout = TestRoomLayout.Classic, RoomSpace = 5, MoveSpeedScale = 0.75f });
        log.Add(new QueuedCommand(21, 1, QueuedCommand.JoinSeq, CommandKind.Join, "Jane Doe"));
        log.Add(new QueuedCommand(22, 1, 1, CommandKind.Input, null, HeldDirs.North | HeldDirs.West));
        log.Add(new QueuedCommand(22, 1, 7, CommandKind.Motion, Motion: new MotionInput(7, -127, 64, 40000, MotionInputFlags.None)));
        log.Add(new QueuedCommand(22, 1, 7, CommandKind.Action, null, Action: ActionKind.Click, Target: 1234567890123, Button: ClickButton.Right, Modifiers: ClickModifiers.Shift | ClickModifiers.Alt));
        log.Add(new QueuedCommand(22, 1, 7, CommandKind.Action, "damage me brute 5 chest", Action: ActionKind.DevCommand));
        // version 34: an aimed click and a mouse release over nothing keep their aim
        log.Add(new QueuedCommand(22, 1, 7, CommandKind.Action, null, Action: ActionKind.Click, Target: 9, Button: ClickButton.Left, TargetKind: TargetKind.Entity, IconX: 16, IconY: 27,
            Aim: new ShotAim(2048, 1024, 1300, 16384, -300)));
        log.Add(new QueuedCommand(22, 1, 7, CommandKind.Action, null, Action: ActionKind.MouseUp, Button: ClickButton.Left, Aim: new ShotAim(2048, 1024, 1300, 0, 16384)));
        log.Add(new QueuedCommand(23, 0, 1, CommandKind.Console, "kick Jane \\ tab\there"));
        log.Add(new QueuedCommand(24, 0, 2, CommandKind.Console, ""));
        log.Add(new QueuedCommand(25, 1, QueuedCommand.LeaveSeq, CommandKind.Leave));
        log.Finish(25, 0x0123456789ABCDEF);

        string text = log.ToText();
        CommandLog back = CommandLog.Parse(text, out string error);
        Assert.Null(error);
        Assert.Equal(log.Commands, back.Commands);
        Assert.Equal((42UL, 30, 20, 2, TestRoomLayout.Classic, 0.75f, false), (back.Seed, back.RoomWidth, back.RoomHeight, back.SmokeActors, back.RoomLayout, back.MoveSpeedScale, back.Empty));
        Assert.Equal(25, back.EndTick);
        Assert.Equal(0x0123456789ABCDEFUL, back.FinalHash);
        Assert.Contains("c 23 0 1 Console - :kick Jane \\\\ tab\\there", text);
        Assert.Contains("c 25 1 4294967295 Leave - -", text);
        Assert.Contains("c 22 1 1 Input d9 -", text);
        Assert.Contains("c 22 1 7 Motion m-127,64,40000,0 -", text);
        Assert.Contains("c 22 1 7 Action a4,1234567890123,2,5,0,0,0,0,0,0,0,0 -", text);
        Assert.Contains("c 22 1 7 Action a5,0,0,0,0,0,0,0,0,0,0,0 :damage me brute 5 chest", text);
        Assert.Contains("layout classic", text);
        Assert.Contains("space 5", text);
        Assert.Equal(5, back.RoomSpace);
        // a log written before the space ring has no space line: a room without one, as it was recorded
        Assert.Equal(0, CommandLog.Parse(text.Replace("space 5\n", "").Replace("space 5\r\n", ""), out _).RoomSpace);
        Assert.Contains("speedscale 0.75", text);

        ServerConfig replay = back.ReplayConfig();
        Assert.Equal((42UL, 30, 20, 25L, false), (replay.Seed, replay.RoomWidth, replay.RoomHeight, replay.Ticks.Value, replay.Console));
        // gaps R6 (D332): a run on the fixed clock writes no realtime line
        Assert.DoesNotContain("realtime", text);
        Assert.Equal(0d, replay.RealtimeStart);
    }

    [Fact]
    public void TheRealDateRoundTripsAndAReplayStartsAtIt()
    {
        var log = CommandLog.For(new ServerConfig { Seed = 7 });
        log.RealtimeStart = 8_400_000_000d; // 2026-08-14, deciseconds since 2000-01-01
        log.Finish(3, 1);
        string text = log.ToText();
        Assert.Contains("realtime 8400000000", text);
        CommandLog back = CommandLog.Parse(text, out string error);
        Assert.Null(error);
        Assert.Equal(8_400_000_000d, back.RealtimeStart);
        ServerConfig replay = back.ReplayConfig();
        Assert.Equal(8_400_000_000d, replay.RealtimeStart);
        Assert.Contains("ALLOW_HOLIDAYS\n", ServerHost.TgConfigText(replay)); // the replay keeps the run's holidays
    }

    [Fact]
    public void TheSoundLengthDigestRoundTripsAndAReplayWithAnotherTableIsRefused()
    {
        var log = CommandLog.For(new ServerConfig { Seed = 7 });
        log.Finish(3, 9, soundsDigest: 0xFEDCBA9876543210);
        string text = log.ToText();
        Assert.Contains("sounds FEDCBA9876543210", text);
        CommandLog back = CommandLog.Parse(text, out string error);
        Assert.Null(error);
        Assert.Equal(0xFEDCBA9876543210UL, back.SoundsDigest);
        Assert.Equal(0UL, CommandLog.Parse(text.Replace("sounds FEDCBA9876543210\n", ""), out _).SoundsDigest); // an older log

        using var replay = new ServerHost(back.ReplayConfig(), replay: back);
        Assert.Throws<InvalidOperationException>(() => replay.Start()); // the replay's world has no table, so another digest
    }

    [Fact]
    public void WritingStreamsTheCommandsAndClosesAtTheEnd()
    {
        var file = new StringWriter();
        var log = CommandLog.For(new ServerConfig());
        log.Add(new QueuedCommand(1, 0, 1, CommandKind.Console, "list"));
        log.WriteTo(file);
        log.Add(new QueuedCommand(2, 0, 2, CommandKind.Console, "quit"));
        log.Flush();
        Assert.Contains("c 2 0 2 Console - :quit", file.ToString());
        log.Finish(2, 7);
        Assert.EndsWith("end 2\nhash 0000000000000007\n", file.ToString()); // '\n' on every OS
        Assert.Equal(file.ToString(), log.ToText());
    }

    [Theory]
    [InlineData("seed 1", "not a command log of format 2")]
    [InlineData("format 1\nseed 1", "not a command log of format 2")] // M1's log: D98's pace and InputFrames
    [InlineData("format 2\nc 1 2 3 Fly - -", "unknown command kind 'Fly'")]
    [InlineData("format 2\nc 1 2 3", "a command has 6 fields")]
    [InlineData("format 2\nwhat 1", "unknown item 'what'")]
    [InlineData("format 2\nc x 2 3 Join - -", "line 2")]
    [InlineData("format 2\nc 1 2 3 Join - noColon", "line 2")]
    [InlineData("format 2\nc 1 2 3 Motion m1,2 -", "line 2")]
    [InlineData("format 2\nc 1 2 3 Action q1 -", "line 2")]
    [InlineData("format 2\nlayout maze", "line 2")]
    public void BadLogsAreRefused(string text, string error)
    {
        Assert.Null(CommandLog.Parse(text, out string why));
        Assert.Contains(error, why);
    }
}
