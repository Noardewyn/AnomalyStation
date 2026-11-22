using System.Text.Json;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Tools.Bots;

namespace Anomaly.Server.Tests.Bots;

/// <summary>The report math (scope 2i, P5): sums, rates and RTT percentiles over the bots' counters, and the JSON the soak script reads.</summary>
public class BotReportTests
{
    private static BotStats Stats(string name, long connectedUpdates, long bytesIn = 0, long bytesOut = 0, long joins = 1)
    {
        var s = new BotStats(name)
        {
            Joins = joins,
            ConnectedUpdates = connectedUpdates,
            BytesIn = bytesIn,
            BytesOut = bytesOut,
        };
        return s;
    }

    [Fact]
    public void ItSumsTheCountersOfEveryBot()
    {
        BotStats a = Stats("A", 100, 1000, 100);
        a.InputsSent = 5;
        a.OwnMoves = 10;
        a.OtherMoves = 100;
        a.DeltasApplied = 90;
        a.SpawnsSeen = 3;
        a.DespawnsSeen = 1;
        a.ServerTexts = 2;
        a.DecodeErrors = 1;
        BotStats b = Stats("B", 300, 3000, 300);
        b.InputsSent = 7;
        b.OwnMoves = 20;
        b.OtherMoves = 200;
        b.DeltasApplied = 290;
        b.Desyncs = 2;

        BotReport report = BotReport.Build(new[] { a, b }, new[] { "Joined", "Disconnected" }, 0.05);
        BotAggregate g = report.Aggregate;

        Assert.Equal(2, g.Bots);
        Assert.Equal(2, g.BotsEverJoined);
        Assert.Equal(1, g.BotsJoinedAtEnd);
        Assert.Equal(2, g.Joins);
        Assert.Equal(12, g.InputsSent);
        Assert.Equal(30, g.OwnMoves);
        Assert.Equal(300, g.OtherMoves);
        Assert.Equal(380, g.DeltasApplied);
        Assert.Equal(3, g.SpawnsSeen);
        Assert.Equal(1, g.DespawnsSeen);
        Assert.Equal(2, g.ServerTexts);
        Assert.Equal(4000, g.BytesIn);
        Assert.Equal(400, g.BytesOut);
        Assert.Equal(2, g.Desyncs);
        Assert.Equal(1, g.DecodeErrors);
    }

    [Fact]
    public void ConnectedTimeAndBytesPerBotPerSecondFollowTheUpdateRate()
    {
        // 100 and 300 updates at 0.05 s: 5 s and 15 s connected.
        BotStats a = Stats("A", 100, bytesIn: 1000, bytesOut: 200);
        BotStats b = Stats("B", 300, bytesIn: 3000, bytesOut: 600);
        BotReport report = BotReport.Build(new[] { a, b }, new[] { "Joined", "Joined" }, 0.05);

        Assert.Equal(20.0, report.Aggregate.ConnectedSecondsTotal, 6);
        Assert.Equal(10.0, report.Aggregate.ConnectedSecondsMean, 6);
        Assert.Equal(200.0, report.Aggregate.BytesInPerBotPerSecond, 6);  // 4000 bytes over 20 bot-seconds
        Assert.Equal(40.0, report.Aggregate.BytesOutPerBotPerSecond, 6);
        Assert.Equal(5.0, report.PerBot[0].ConnectedSeconds, 6);
        Assert.Equal(15.0, report.PerBot[1].ConnectedSeconds, 6);
        Assert.Equal(1000, report.PerBot[0].BytesIn);
    }

    [Fact]
    public void ARateIsNotDividedByZeroWhenNobodyConnected()
    {
        BotReport report = BotReport.Build(new[] { Stats("A", 0, 10, 10, joins: 0) }, new[] { "Disconnected" }, 0.05);
        Assert.Equal(0, report.Aggregate.BytesInPerBotPerSecond);
        Assert.Equal(0, report.Aggregate.BotsEverJoined);
        Assert.Equal(0, report.Aggregate.RttP99Ms);
    }

    [Fact]
    public void ThisManyDisconnectsAreCountedByKindAndReason()
    {
        BotStats a = Stats("A", 10);
        a.AddDisconnect(TransportDisconnectKind.RemoteClose, DisconnectReason.Kicked);
        a.AddDisconnect(TransportDisconnectKind.LocalClose, DisconnectReason.None);
        a.AddDisconnect(TransportDisconnectKind.LocalClose, DisconnectReason.None);
        BotStats b = Stats("B", 10);
        b.AddDisconnect(TransportDisconnectKind.RemoteClose, DisconnectReason.Kicked);
        b.AddDisconnect(TransportDisconnectKind.Rejected, DisconnectReason.WrongVersion);

        BotReport report = BotReport.Build(new[] { a, b }, null, 0.05);
        Assert.Equal(5, report.Aggregate.DisconnectsTotal);
        Assert.Equal(2, report.Aggregate.Disconnects["RemoteClose:Kicked"]);
        Assert.Equal(2, report.Aggregate.Disconnects["LocalClose:None"]);
        Assert.Equal(1, report.Aggregate.Disconnects["Rejected:WrongVersion"]);
        Assert.Equal(3, report.Aggregate.Disconnects.Count);
    }

    [Fact]
    public void RttPercentilesComeFromTheMergedHistogram()
    {
        // Bot A: 90 samples of 1 ms. Bot B: 9 samples of 5 ms and one of 40 ms. All 100: p50 1, p95 5, p99 5, max 40.
        BotStats a = Stats("A", 10);
        for (int i = 0; i < 90; i++) a.AddRtt(1);
        BotStats b = Stats("B", 10);
        for (int i = 0; i < 9; i++) b.AddRtt(5);
        b.AddRtt(40);

        BotAggregate g = BotReport.Build(new[] { a, b }, null, 0.05).Aggregate;
        Assert.Equal(100, g.RttSamples);
        Assert.Equal(1, g.RttP50Ms);
        Assert.Equal(5, g.RttP95Ms);
        Assert.Equal(5, g.RttP99Ms);
        Assert.Equal(40, g.RttMaxMs);
        Assert.Equal((90 * 1 + 9 * 5 + 40) / 100.0, g.RttMeanMs, 6);
    }

    [Fact]
    public void RttAboveTheLastBucketAnswersWithTheMax()
    {
        BotStats a = Stats("A", 10);
        a.AddRtt(2);
        a.AddRtt(5000);
        BotAggregate g = BotReport.Build(new[] { a }, null, 0.05).Aggregate;
        Assert.Equal(5000, g.RttMaxMs);
        Assert.Equal(5000, g.RttP99Ms);
        Assert.Equal(2, g.RttP50Ms);
    }

    [Fact]
    public void PercentileUsesTheNearestRank()
    {
        var histogram = new long[BotStats.RttBuckets];
        histogram[3] = 1;
        histogram[7] = 1;
        histogram[9] = 1;
        histogram[20] = 1;
        Assert.Equal(3, BotReport.Percentile(histogram, 4, 0.25, 20));
        Assert.Equal(7, BotReport.Percentile(histogram, 4, 0.50, 20));
        Assert.Equal(9, BotReport.Percentile(histogram, 4, 0.75, 20));
        Assert.Equal(20, BotReport.Percentile(histogram, 4, 1.0, 20));
        Assert.Equal(3, BotReport.Percentile(histogram, 4, 0.0, 20));
        Assert.Equal(0, BotReport.Percentile(histogram, 0, 0.5, 0));
    }

    [Fact]
    public void ANegativeRttCountsAsZero()
    {
        var s = new BotStats("A");
        s.AddRtt(-5);
        Assert.Equal(1, s.RttHistogram[0]);
        Assert.Equal(0, s.RttMaxMs);
    }

    [Fact]
    public void ExceptionsAreCountedAndTheFirstOneIsKept()
    {
        BotStats a = Stats("A", 1);
        a.Exceptions = 2;
        a.LastException = "first";
        BotStats b = Stats("B", 1);
        b.Exceptions = 1;
        b.LastException = "second";
        BotAggregate g = BotReport.Build(new[] { a, b }, null, 0.05).Aggregate;
        Assert.Equal(3, g.Exceptions);
        Assert.Equal("first", g.FirstException);
    }

    [Fact]
    public void ASecondPerUpdateOfZeroIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BotReport.Build(Array.Empty<BotStats>(), null, 0));
    }

    [Fact]
    public void TheJsonCarriesTheNumbersTheSoakScriptReads()
    {
        BotStats a = Stats("Bot001", 200, bytesIn: 4000, bytesOut: 400);
        a.OwnMoves = 12;
        a.OtherMoves = 34;
        a.Desyncs = 1;
        a.AddRtt(2);
        a.AddDisconnect(TransportDisconnectKind.RemoteClose, DisconnectReason.Kicked);
        a.LastDisconnect = "RemoteClose:Kicked bye";
        BotReport report = BotReport.Build(new[] { a }, new[] { "Disconnected" }, 0.05);
        report.Behaviour = "wander";
        report.Host = "127.0.0.1";
        report.Port = 7777;
        report.Seed = 9;
        report.RunSeconds = 10.5;
        report.Iterations = 210;
        report.LateIterations = 2;
        report.MaxIterationMs = 61.25;

        using JsonDocument doc = JsonDocument.Parse(report.ToJson());
        JsonElement root = doc.RootElement;
        Assert.Equal("wander", root.GetProperty("behaviour").GetString());
        Assert.Equal(7777, root.GetProperty("port").GetInt32());
        Assert.Equal(10.5, root.GetProperty("run_seconds").GetDouble());
        Assert.Equal(2, root.GetProperty("late_iterations").GetInt64());

        JsonElement g = root.GetProperty("aggregate");
        Assert.Equal(1, g.GetProperty("bots").GetInt32());
        Assert.Equal(10.0, g.GetProperty("connected_seconds_total").GetDouble());
        Assert.Equal(12, g.GetProperty("own_moves").GetInt64());
        Assert.Equal(34, g.GetProperty("other_moves").GetInt64());
        Assert.Equal(1, g.GetProperty("desyncs").GetInt64());
        Assert.Equal(400.0, g.GetProperty("bytes_in_per_bot_per_second").GetDouble());
        Assert.Equal(1, g.GetProperty("disconnects").GetProperty("RemoteClose:Kicked").GetInt64());
        Assert.Equal(2, g.GetProperty("rtt_ms").GetProperty("p99").GetInt32());

        JsonElement row = root.GetProperty("per_bot")[0];
        Assert.Equal("Bot001", row.GetProperty("name").GetString());
        Assert.Equal("Disconnected", row.GetProperty("state").GetString());
        Assert.Equal("RemoteClose:Kicked bye", row.GetProperty("last_disconnect").GetString());
    }

    [Fact]
    public void ThePerBotRowsCanBeLeftOut()
    {
        BotReport report = BotReport.Build(new[] { Stats("A", 1) }, new[] { "Joined" }, 0.05);
        using JsonDocument doc = JsonDocument.Parse(report.ToJson(includePerBot: false));
        Assert.False(doc.RootElement.TryGetProperty("per_bot", out _));
        Assert.True(doc.RootElement.TryGetProperty("aggregate", out _));
    }

    [Fact]
    public void TheM2CountersFramesActionsShovesRestsAndPredictionAreSummedAndInTheJson()
    {
        BotStats a = Stats("A", 100);
        a.Behaviour = "shove";
        a.FramesSent = 100;
        a.ActionsByKind[(int)Anomaly.Protocol.ActionKind.Click] = 4;
        a.ActionsByKind[(int)Anomaly.Protocol.ActionKind.ToggleCombatMode] = 1;
        a.ShovesSent = 4;
        BotStats b = Stats("B", 100);
        b.Behaviour = "rest";
        b.FramesSent = 90;
        b.ActionsByKind[(int)Anomaly.Protocol.ActionKind.Rest] = 6;
        b.RestsStarted = 3;
        b.Predicted = true;
        b.Reconciliations = 80;
        b.Corrections = 2;
        b.Snaps = 1;
        b.Mismatches = 3;
        b.MaxErrorTiles = 0.25;
        BotReport report = BotReport.Build(new[] { a, b }, new[] { "Joined", "Joined" }, 0.05);
        report.Predict = true;
        BotAggregate g = report.Aggregate;
        Assert.Equal((190, 11, 4, 3), (g.FramesSent, g.ActionsSent, g.Shoves, g.Rests));
        Assert.Equal(4, g.ActionsByKind["click"]);
        Assert.Equal(6, g.ActionsByKind["rest"]);
        Assert.Equal(new[] { "rest", "shove" }, g.Behaviours.Keys);
        Assert.Equal((1, 80, 2, 1, 3), (g.PredictingBots, g.Reconciliations, g.Corrections, g.Snaps, g.Mismatches));

        using JsonDocument doc = JsonDocument.Parse(report.ToJson());
        JsonElement root = doc.RootElement;
        Assert.True(root.GetProperty("predict").GetBoolean());
        JsonElement agg = root.GetProperty("aggregate");
        Assert.Equal(4, agg.GetProperty("shoves").GetInt64());
        Assert.Equal(4, agg.GetProperty("actions").GetProperty("click").GetInt64());
        Assert.Equal(1, agg.GetProperty("behaviours").GetProperty("shove").GetInt32());
        Assert.Equal(2, agg.GetProperty("prediction").GetProperty("corrections").GetInt64());
        Assert.Equal(0.25, agg.GetProperty("prediction").GetProperty("max_error_tiles").GetDouble());
        Assert.Equal("rest", root.GetProperty("per_bot")[1].GetProperty("behaviour").GetString());
        Assert.Equal(2, root.GetProperty("per_bot")[1].GetProperty("corrections").GetInt64());
    }
}
