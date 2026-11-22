using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Server;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 16 (M5 slice 3, docs/ai/specs/protocol.md section 25, DV44): tg's tgui windows. The UiWindow message (server to client,
/// State) carries a window's update (tg's config, data and static data as a UiValue tree) or its close; the UiAct (kind 11) and UiClose
/// (kind 12) actions are tg's act and close messages. Bytes, round trips, the value tags, refusals, the replica's merge, the host applying
/// an act and a close and logging them, and a version-15 client refused.
/// </summary>
public class ProtocolV16Tests
{
    private static ActionMessage Act(int window, string action, UiValue parameters = null, uint seq = 0) =>
        new ActionMessage(seq, ActionKind.UiAct, 0, ClickButton.None, ClickModifiers.None, null) { UiWindow = window, UiAction = action, UiParams = parameters };

    private static ActionMessage Close(int window, uint seq = 0) =>
        new ActionMessage(seq, ActionKind.UiClose, 0, ClickButton.None, ClickModifiers.None, null) { UiWindow = window };

    private static UiWindowMessage Update(int window, UiValue data = null, UiValue statics = null, string @interface = "Canister") => new()
    {
        Kind = UiWindowKind.Update,
        Window = window,
        Interface = @interface,
        Title = "Air canister",
        Status = 2,
        Data = data,
        StaticData = statics,
    };

    private static MapGrid Grid()
    {
        var grid = new MapGrid { Tick = 5, Width = 8, Height = 8, Levels = 1 };
        grid.TurfTypes.Add("/turf/open/floor");
        grid.Runs.Add(new TurfRun(grid.CellCount, 0));
        return grid;
    }

    private static ReplicaWorld ReplicaWithSnapshot()
    {
        var replica = new ReplicaWorld();
        replica.Apply(Grid());
        replica.Apply(new Snapshot { Tick = 6 });
        return replica;
    }

    internal static UiValue RandomValue(Random rng, int depth = 0)
    {
        int kind = depth >= 3 ? rng.Next(0, 5) : rng.Next(0, 7);
        return kind switch
        {
            0 => UiValue.Null,
            1 => UiValue.Of(rng.Next(2) == 0),
            2 => UiValue.Of(rng.Next(-100000, 100000)),
            3 => UiValue.Of(rng.NextDouble() * 1000 - 500),
            4 => UiValue.Of("s" + rng.Next(0, 1000)),
            5 => UiValue.ListOf(Enumerable.Range(0, rng.Next(0, 4)).Select(_ => RandomValue(rng, depth + 1))),
            _ => UiValue.ObjectOf(Enumerable.Range(0, rng.Next(0, 4)).Select(i => new KeyValuePair<string, UiValue>("k" + i, RandomValue(rng, depth + 1)))),
        };
    }

    internal static UiWindowMessage RandomWindow(Random rng)
    {
        if (rng.Next(4) == 0)
            return new UiWindowMessage { Kind = UiWindowKind.Close, Window = rng.Next(1, Limits.MaxUiWindows + 1) };
        return new UiWindowMessage
        {
            Kind = UiWindowKind.Update,
            Window = rng.Next(1, Limits.MaxUiWindows + 1),
            Interface = "Iface" + rng.Next(0, 10),
            Title = rng.Next(3) == 0 ? "" : "Title " + rng.Next(0, 100),
            Status = rng.Next(-1, 3),
            Refreshing = rng.Next(2) == 0,
            Data = rng.Next(3) == 0 ? null : UiValue.ObjectOf(("a", RandomValue(rng, 1)), ("b", RandomValue(rng, 1))),
            StaticData = rng.Next(2) == 0 ? null : UiValue.ObjectOf(("s", RandomValue(rng, 1))),
        };
    }

    internal static ActionMessage RandomUiAction(Random rng) => rng.Next(3) == 0
        ? Close(rng.Next(1, Limits.MaxUiWindows + 1), (uint)rng.Next(0, 100000))
        : Act(rng.Next(1, Limits.MaxUiWindows + 1), "act" + rng.Next(0, 10), rng.Next(2) == 0 ? null : UiValue.ObjectOf(("pressure", UiValue.Of(rng.Next(0, 3000)))), (uint)rng.Next(0, 100000));

    [Fact]
    public void TheVersionIsAtLeastSixteenAndUiActAndUiCloseAreElevenAndTwelve()
    {
        Assert.True(ProtocolVersion.Current >= 16); // version 17: Drop (13) is the last kind (ProtocolV17Tests)
        Assert.Equal(11, (int)ActionKind.UiAct);
        Assert.Equal(12, (int)ActionKind.UiClose);
        Assert.Equal(16, (int)MessageId.UiWindow);
        // A UiAct's body (window 5 + action 1 + 48 + params 140) fits in the dev command's 200 bytes: the action cap holds.
        Assert.True(5 + 1 + Limits.MaxUiActionBytes + Limits.MaxUiParamsBytes <= Limits.MaxDevCommandBytes);
        Assert.True(Limits.MaxActionMessageBytes <= Limits.MaxInboundMessageBytes);
    }

    [Fact]
    public void AUiWindowUpdateAndCloseHaveTheirBytesAndRoundTrip()
    {
        var update = Update(3, UiValue.ObjectOf(("valveOpen", UiValue.Of(1)), ("tankPressure", UiValue.Of(4559.5)), ("holdingTank", UiValue.Null)),
            UiValue.ObjectOf(("buttons", UiValue.ListOf(new[] { UiValue.Of("Yes"), UiValue.Of("No") })), ("ok", UiValue.True)));
        byte[] bytes = Codec.Encode(update);
        Assert.Equal((byte)MessageId.UiWindow, bytes[0]);
        Assert.Equal(0, bytes[1]); // update
        Assert.Equal(3, bytes[2]); // window
        Assert.True(Codec.TryDecode(bytes, out UiWindowMessage back, out ProtocolError error), error.ToString());
        Assert.Equal("Canister", back.Interface);
        Assert.Equal("Air canister", back.Title);
        Assert.Equal(2, back.Status);
        Assert.Equal(update.Data.ToJson(), back.Data.ToJson());
        Assert.Equal(update.StaticData.ToJson(), back.StaticData.ToJson());
        Assert.Equal(bytes, Codec.Encode(back));
        Assert.Equal("{\"valveOpen\":1,\"tankPressure\":4559.5,\"holdingTank\":null}", back.Data.ToJson());

        byte[] close = Codec.Encode(new UiWindowMessage { Kind = UiWindowKind.Close, Window = 200 });
        Assert.Equal(new byte[] { 16, 1, 200, 1 }, close);
        Assert.True(Codec.TryDecode(close, out UiWindowMessage closed, out _));
        Assert.Equal(UiWindowKind.Close, closed.Kind);
    }

    [Fact]
    public void ValuesChooseTheirTagAndKeepTheirOrder()
    {
        // tag 3 integer (zigzag varint), tag 4 a double, tag 5 a string, 6 an array, 7 an object, 0/1/2 null/false/true
        var data = UiValue.ObjectOf(("i", UiValue.Of(-2)), ("d", UiValue.Of(0.5)), ("s", UiValue.Of("x")), ("t", UiValue.True));
        byte[] bytes = Codec.Encode(Update(1, data));
        int start = 1 + 1 + 1 + 1 + "Canister".Length + 1 + "Air canister".Length + 1 + 1;
        Assert.Equal(7, bytes[start]); // object
        Assert.Equal(4, bytes[start + 1]); // four fields
        Assert.Equal(3, bytes[start + 4]); // "i": integer
        Assert.Equal(3, bytes[start + 5]); // zigzag(-2) = 3
        Assert.True(Codec.TryDecode(bytes, out UiWindowMessage back, out _));
        Assert.Equal(new[] { "i", "d", "s", "t" }, back.Data.Fields.Select(f => f.Key).ToArray());
        Assert.Equal(0.5, back.Data.Get("d").Number);
    }

    [Fact]
    public void ABadUiWindowIsRefused()
    {
        Assert.Throws<ProtocolException>(() => Codec.Encode(new UiWindowMessage { Kind = UiWindowKind.Close, Window = 0 }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new UiWindowMessage { Kind = UiWindowKind.Close, Window = 201 }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new UiWindowMessage { Kind = UiWindowKind.Close, Window = 1, Title = "x" }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new UiWindowMessage { Kind = UiWindowKind.Update, Window = 1, Interface = "" }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new UiWindowMessage { Kind = UiWindowKind.Update, Window = 1, Interface = "A", Status = 3 }));
        UiValue deep = UiValue.Null;
        for (int i = 0; i < Limits.MaxUiDepth + 2; i++)
            deep = UiValue.ListOf(new[] { deep });
        Assert.Throws<ProtocolException>(() => Codec.Encode(Update(1, UiValue.ObjectOf(("deep", deep)))));

        byte[] good = Codec.Encode(Update(2, UiValue.ObjectOf(("a", UiValue.Of(1)))));
        Assert.True(Codec.TryDecode(good, out UiWindowMessage _, out _));
        foreach (byte[] bad in new[]
        {
            new byte[] { 16, 2, 1 },                     // unknown kind
            new byte[] { 16, 1, 0 },                     // window 0
            new byte[] { 16, 1, 201, 1 },                // window 201
            new byte[] { 16, 1, 1, 0 },                  // a close with a trailing byte
            new byte[] { 16, 0, 1, 1 },                  // an update with an empty interface
            new byte[] { 16, 0, 1, 2, 65, 1, 4, 0 },     // status 3
            new byte[] { 16, 0, 1, 2, 65, 1, 3, 8 },     // unknown flag bits
            new byte[] { 16, 0, 1, 2, 65, 1, 3, 2, 9 },  // an unknown value tag
            new byte[] { 16, 0, 1, 2, 65, 1, 3, 2, 4, 0, 0, 0, 0, 0, 0, 240, 63 }, // the double 1.0: a whole number must be tag 3
            new byte[] { 16, 0, 1, 2, 65, 1, 3, 2, 7, 5 }, // an object of 5 fields with no bytes
        })
        {
            Assert.False(Codec.TryDecode(bad, out UiWindowMessage value, out _), BitConverter.ToString(bad));
            Assert.Null(value);
        }
    }

    [Fact]
    public void AUiActAndCloseRoundTripAndBadOnesAreRefused()
    {
        ActionMessage act = Act(4, "pressure", UiValue.ObjectOf(("pressure", UiValue.Of(500))), seq: 7);
        byte[] bytes = Codec.Encode(act);
        Assert.True(Codec.TryDecode(bytes, out ActionMessage back, out ProtocolError error), error.ToString());
        Assert.Equal(ActionKind.UiAct, back.Kind);
        Assert.Equal(4, back.UiWindow);
        Assert.Equal("pressure", back.UiAction);
        Assert.Equal("{\"pressure\":500}", back.UiParams.ToJson());
        Assert.Equal(bytes, Codec.Encode(back));

        byte[] noParams = Codec.Encode(Act(1, "valve"));
        Assert.True(Codec.TryDecode(noParams, out ActionMessage valve, out _));
        Assert.Null(valve.UiParams);

        byte[] close = Codec.Encode(Close(9));
        Assert.Equal(new byte[] { (byte)MessageId.Action, 0, 12, 9 }, close);
        Assert.True(Codec.TryDecode(close, out ActionMessage closed, out _));
        Assert.Equal((ActionKind.UiClose, 9), (closed.Kind, closed.UiWindow));

        Assert.Throws<ProtocolException>(() => Codec.Encode(Act(0, "valve")));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Act(1, "")));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Act(1, new string('a', Limits.MaxUiActionBytes + 1))));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Act(1, "x", UiValue.Of(5)))); // params must be an object
        Assert.Throws<ProtocolException>(() => Codec.Encode(Act(1, "x", UiValue.ObjectOf(("long", UiValue.Of(new string('z', 200))))))); // over 140 bytes
        Assert.Throws<ProtocolException>(() => Codec.Encode(Close(1) with { UiAction = "x" }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new ActionMessage(1, ActionKind.Rest, 0, ClickButton.None, ClickModifiers.None, null) { UiWindow = 3 }));
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.Action, 0, 11, 1, 1 }, out ActionMessage _, out _)); // an empty action
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.Action, 0, 11, 1, 2, 120, 2 }, out ActionMessage _, out _)); // params true: not an object
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.Action, 0, 12, 0 }, out ActionMessage _, out _)); // window 0
        Assert.True(Codec.UiValueBytes(UiValue.ObjectOf(("long", UiValue.Of(new string('z', 200))))) > Limits.MaxUiParamsBytes);
    }

    [Fact]
    public void TheReplicaMergesUpdatesAsTguiDoesAndForgetsClosedWindows()
    {
        var early = new ReplicaWorld();
        Assert.Throws<ProtocolException>(() => early.Apply(Update(1, UiValue.ObjectOf())));

        ReplicaWorld replica = ReplicaWithSnapshot();
        var seen = new List<(int, UiWindowState)>();
        replica.UiWindowChanged += (w, s) => seen.Add((w, s));
        replica.Apply(Update(2, UiValue.ObjectOf(("a", UiValue.Of(1)), ("b", UiValue.Of(2))), UiValue.ObjectOf(("max", UiValue.Of(9)))));
        replica.Apply(Update(2, UiValue.ObjectOf(("b", UiValue.Of(3)))));
        UiWindowState state = replica.UiWindows[2];
        Assert.Equal("{\"a\":1,\"b\":3}", state.Data.ToJson());
        Assert.Equal("{\"max\":9,\"a\":1,\"b\":3}", state.Merged.ToJson());
        Assert.Equal(2, state.Serial);

        // Another interface in the same window starts afresh.
        replica.Apply(Update(2, UiValue.ObjectOf(("x", UiValue.Of(1))), @interface: "AlertModal"));
        Assert.Equal("{\"x\":1}", replica.UiWindows[2].Merged.ToJson());

        replica.Apply(new UiWindowMessage { Kind = UiWindowKind.Close, Window = 2 });
        Assert.Empty(replica.UiWindows.Tgui());
        Assert.Null(seen[^1].Item2);
        Assert.Throws<ProtocolException>(() => replica.Apply(new UiWindowMessage { Kind = UiWindowKind.Close, Window = 2 }));
        Assert.Throws<ProtocolException>(() => replica.Apply(Update(1, UiValue.ListOf(Array.Empty<UiValue>()))));

        replica.Apply(Update(5, UiValue.ObjectOf()));
        replica.Apply(Grid()); // a join forgets them
        Assert.Empty(replica.UiWindows.Tgui());

        ReplicaWorld viaBytes = ReplicaWithSnapshot();
        Assert.True(viaBytes.TryApply(Codec.Encode(Update(7, UiValue.ObjectOf(("v", UiValue.Of("w"))))), out _, out _));
        Assert.Equal("w", viaBytes.UiWindows[7].Data.Get("v").Text);
    }

    [Fact]
    public void TheHostAppliesAUiActAndCloseAndLogsThem()
    {
        using TestServer server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        alice.Motion(0, 0);
        server.Tick(2);
        alice.ActionV4(Act(3, "pressure", UiValue.ObjectOf(("pressure", UiValue.Of(500)))));
        alice.ActionV4(Close(3));
        server.Tick(2);
        Assert.Contains(server.Clients.Actions, a => a.Action == "tgui tgui-window-3 act/pressure {\"pressure\":500}");
        Assert.Contains(server.Clients.Actions, a => a.Action == "tgui tgui-window-3 close -");
        Assert.Equal(0, server.Host.SessionOf(1).Strikes);
        Assert.Equal(1, server.Host.SessionOf(1).UiActsApplied);
        Assert.Equal(1, server.Host.SessionOf(1).UiClosesApplied);
        QueuedCommand logged = Assert.Single(server.Host.Log.Commands, c => c.Kind == CommandKind.Action && c.Action == ActionKind.UiAct);
        Assert.Equal((3, "pressure", "{\"pressure\":500}"), (logged.UiWindow, logged.Text, logged.UiParams));
        CommandLog parsed = CommandLog.Parse(server.Host.Log.ToText(), out string error);
        Assert.Null(error);
        Assert.Equal(logged, Assert.Single(parsed.Commands, c => c.Action == ActionKind.UiAct));
        Assert.Equal(Assert.Single(server.Host.Log.Commands, c => c.Action == ActionKind.UiClose), Assert.Single(parsed.Commands, c => c.Action == ActionKind.UiClose));
    }

    [Fact]
    public void ABadUiActionIsAStrike()
    {
        using TestServer server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        alice.Motion(0, 0);
        server.Tick(2);
        alice.ActionV4(Act(0, "valve"));
        server.Tick(2);
        Assert.Equal(1, server.Host.SessionOf(1).Strikes);
        alice.ActionV4(Act(1, "x", UiValue.Of(5)));
        server.Tick(2);
        Assert.Equal(2, server.Host.SessionOf(1).Strikes);
    }

    [Fact]
    public void A_client_of_version_15_is_refused()
    {
        using var server = TestServer.Start();
        FakeRemote remote = server.Connect("Alice", protocol: 15);
        server.Tick();
        Assert.Equal(DisconnectReason.WrongVersion, remote.Rejected?.Reason);
    }
}
