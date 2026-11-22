using Anomaly.Client.Shared;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Server;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 15 (M4c slice 4, docs/ai/specs/protocol.md section 24, DV40): tg's radial menu. The RadialMenu message (server to
/// client, State) carries a menu's page (its slices' names, flags and pictures, the centre button) or its close; the RadialPick action
/// (kind 10) is the client's answer. Bytes, round trips, refusals, the replica's checks, the host applying a pick and logging it, the
/// client model of the menu, and a version-14 client refused.
/// </summary>
public class ProtocolV15Tests
{
    private static ActionMessage Pick(long menu, int element, uint seq = 0) =>
        new ActionMessage(seq, ActionKind.RadialPick, 0, ClickButton.None, ClickModifiers.None, null) { RadialMenu = menu, RadialElement = element };

    private static RadialMenuMessage Open(long id, params RadialSlice[] slices)
    {
        var m = new RadialMenuMessage
        {
            Kind = RadialMenuKind.Open,
            MenuId = id,
            Flags = RadialMenuFlags.CloseButton | RadialMenuFlags.SlideIn,
            Page = 1,
            Pages = 1,
            Center = new TableRef(1, true),
            CenterFocus = new TableRef(2, true),
        };
        m.Elements.AddRange(slices);
        return m;
    }

    private static RadialSlice Slice(string name, uint icon = 3) => new(name, RadialElementFlags.None, new TableRef(icon, true), new TableRef(1, true), new TableRef(2, true));

    private static MapGrid Grid()
    {
        var grid = new MapGrid { Tick = 5, Width = 8, Height = 8, Levels = 1 };
        grid.TurfTypes.Add("/turf/open/floor");
        grid.Runs.Add(new TurfRun(grid.CellCount, 0));
        return grid;
    }

    internal static RadialMenuMessage RandomRadial(Random rng)
    {
        if (rng.Next(4) == 0)
            return new RadialMenuMessage { Kind = RadialMenuKind.Close, MenuId = rng.Next(1, 1_000_000) };
        var m = new RadialMenuMessage
        {
            Kind = RadialMenuKind.Open,
            MenuId = rng.Next(1, 1_000_000),
            Flags = (RadialMenuFlags)rng.Next(0, 32) & ~RadialMenuFlags.CloseButton,
            Pages = (byte)rng.Next(1, 4),
        };
        m.Page = (byte)rng.Next(1, m.Pages + 1);
        if (rng.Next(2) == 0)
        {
            m.Flags |= RadialMenuFlags.CloseButton;
            m.Center = new TableRef((uint)rng.Next(1, 500), rng.Next(2) == 0);
            m.CenterFocus = new TableRef((uint)rng.Next(1, 500), rng.Next(2) == 0);
        }
        int count = rng.Next(1, Limits.MaxRadialElements + 1);
        for (int i = 0; i < count; i++)
            m.Elements.Add(new RadialSlice("slice" + rng.Next(0, 1000), (RadialElementFlags)rng.Next(0, 4),
                rng.Next(3) == 0 ? TableRef.None : new TableRef((uint)rng.Next(1, 500), rng.Next(2) == 0),
                new TableRef((uint)rng.Next(1, 500), true), new TableRef((uint)rng.Next(1, 500), true)));
        return m;
    }

    [Fact]
    public void TheVersionIsAtLeastFifteenAndRadialPickIsKindTen()
    {
        Assert.True(ProtocolVersion.Current >= 15); // version 16: UiAct (11) and UiClose (12) follow it, version 17: Drop (13)
        Assert.Equal(10, (int)ActionKind.RadialPick);
        Assert.True(ActionKinds.Last >= ActionKind.RadialPick);
        Assert.Equal(15, (int)MessageId.Radial);
        Assert.True(Limits.MaxActionMessageBytes <= Limits.MaxInboundMessageBytes);
    }

    [Fact]
    public void ARadialPickIsItsMenuAndElementAndRoundTrips()
    {
        byte[] bytes = Codec.Encode(Pick(300, 2, 5));
        Assert.Equal(new byte[] { (byte)MessageId.Action, 5, 10, 0xAC, 0x02, 2 }, bytes); // [id] [seq 5] [kind 10] [varuint menu 300] [element 2]
        Assert.True(Codec.TryDecode(bytes, out ActionMessage back, out _));
        Assert.Equal((5u, ActionKind.RadialPick, 300L, 2, TargetKind.None), (back.FrameSeq, back.Kind, back.RadialMenu, back.RadialElement, back.TargetKind));
        Assert.False(back.IsTargeted);
        Assert.True(Codec.TryDecode(Codec.Encode(Pick(7, 0)), out ActionMessage centre, out _)); // the centre button
        Assert.Equal(0, centre.RadialElement);
    }

    [Fact]
    public void ARadialPickWithoutAMenuOrPastAPageOrOnAnotherKindIsRefused()
    {
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(Pick(0, 1)));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(Pick(5, Limits.MaxRadialElements + 1)));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(Pick(5, -1)));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(new ActionMessage(1, ActionKind.Rest, 0, ClickButton.None, ClickModifiers.None, null) { RadialMenu = 5 }));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(Pick(5, 1) with { Target = 3 }));
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.Action, 1, 10, 0, 1 }, out ActionMessage _, out _)); // menu 0
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.Action, 1, 10, 5, 17 }, out ActionMessage _, out _)); // element 17
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.Action, 1, 10, 5 }, out ActionMessage _, out _)); // no element
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.Action, 1, 10, 5, 1, 0 }, out ActionMessage _, out _)); // a byte after it
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.Action, 1, 14 }, out ActionMessage _, out _)); // kind 14 without its href (version 24's Topic)
    }

    [Fact]
    public void ARadialMenuOpenAndCloseHaveTheirBytesAndRoundTrip()
    {
        RadialMenuMessage open = Open(9, Slice("suit"), new RadialSlice("Next Page", RadialElementFlags.NextPage, new TableRef(4, false), new TableRef(1, true), new TableRef(2, true)));
        byte[] bytes = Codec.Encode(open);
        Assert.Equal(new byte[]
        {
            (byte)MessageId.Radial, 0, 9,     // open, menu 9
            (byte)(RadialMenuFlags.CloseButton | RadialMenuFlags.SlideIn), 1, 1,
            3, 5,                             // centre: private 1, private 2
            2,                                // two slices
            5, (byte)'s', (byte)'u', (byte)'i', (byte)'t', 0, 7, 3, 5,   // a string is [length + 1] then its bytes
            10, (byte)'N', (byte)'e', (byte)'x', (byte)'t', (byte)' ', (byte)'P', (byte)'a', (byte)'g', (byte)'e', 1, 8, 3, 5,
        }, bytes);
        Assert.True(Codec.TryDecode(bytes, out RadialMenuMessage back, out ProtocolError error), error.ToString());
        Assert.Equal(bytes, Codec.Encode(back));
        Assert.Equal(open.Elements, back.Elements);
        Assert.Equal((open.Center, open.CenterFocus, open.Flags), (back.Center, back.CenterFocus, back.Flags));

        byte[] close = Codec.Encode(new RadialMenuMessage { Kind = RadialMenuKind.Close, MenuId = 9 });
        Assert.Equal(new byte[] { (byte)MessageId.Radial, 1, 9 }, close);
        Assert.True(Codec.TryDecode(close, out RadialMenuMessage closed, out _));
        Assert.Equal((RadialMenuKind.Close, 9L, 0), (closed.Kind, closed.MenuId, closed.Elements.Count));
    }

    [Fact]
    public void ABadRadialMenuIsRefused()
    {
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(Open(0, Slice("a"))));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(Open(1)));                                             // no slices
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(Open(1, Slice(""))));                                  // no name
        RadialMenuMessage paged = Open(1, Slice("a"));
        paged.Page = 3;
        paged.Pages = 2;
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(paged));
        RadialMenuMessage noFlag = Open(1, Slice("a"));
        noFlag.Flags = RadialMenuFlags.None;                                                                         // a centre without its flag
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(noFlag));
        RadialMenuMessage noBackground = Open(1, new RadialSlice("a", RadialElementFlags.None, TableRef.None, TableRef.None, new TableRef(2, true)));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(noBackground));
        RadialMenuMessage tooMany = Open(1);
        for (int i = 0; i <= Limits.MaxRadialElements; i++) tooMany.Elements.Add(Slice("s" + i));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(tooMany));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(new RadialMenuMessage { Kind = RadialMenuKind.Close, MenuId = 1, Page = 1 }));

        byte[] good = Codec.Encode(Open(9, Slice("a")));
        byte[] badFlags = (byte[])good.Clone();
        badFlags[3] = 0x80;
        Assert.False(Codec.TryDecode(badFlags, out RadialMenuMessage _, out _));
        byte[] badKind = (byte[])good.Clone();
        badKind[1] = 2;
        Assert.False(Codec.TryDecode(badKind, out RadialMenuMessage _, out _));
        Assert.False(Codec.TryDecode(good.AsSpan(0, good.Length - 1), out RadialMenuMessage _, out _));
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.Radial, 1, 9, 0 }, out RadialMenuMessage _, out _)); // a byte after a close
    }

    [Fact]
    public void TheReplicaKeepsTheOpenMenusAndChecksTheirPictures()
    {
        var replica = new ReplicaWorld();
        Assert.Throws<ProtocolException>(() => replica.Apply(Open(9, Slice("a")))); // before a Snapshot
        replica.Apply(Grid());
        Snapshot s = Msg.Snapshot(5, 0, 0, new[] { "/obj" });
        s.Own.FirstNewPrivateAppearance = 1;
        for (int i = 0; i < 3; i++) s.Own.NewPrivateAppearances.Add(new AppearanceEntry { Alpha = (byte)(100 + i) });
        replica.Apply(s);
        var changes = new List<RadialMenuMessage>();
        replica.RadialChanged += m => changes.Add(m);

        replica.Apply(Open(9, Slice("a")));
        replica.Apply(Open(12, Slice("b"), Slice("c")));
        Assert.Equal(12, replica.TopRadial.MenuId);
        Assert.Equal(2, replica.Radials.Count);
        replica.Apply(Open(9, Slice("a2"))); // a new page of the older menu brings it on top
        Assert.Equal(9, replica.TopRadial.MenuId);
        Assert.Throws<ProtocolException>(() => replica.Apply(Open(14, Slice("x", icon: 4)))); // past the private table
        Assert.Throws<ProtocolException>(() => replica.Apply(new RadialMenuMessage { Kind = RadialMenuKind.Close, MenuId = 77 }));
        replica.Apply(new RadialMenuMessage { Kind = RadialMenuKind.Close, MenuId = 9 });
        Assert.Equal(12, replica.TopRadial.MenuId);
        Assert.Equal(4, changes.Count);
        Assert.True(replica.TryApply(Codec.Encode(new RadialMenuMessage { Kind = RadialMenuKind.Close, MenuId = 12 }), out MessageId id, out _));
        Assert.Equal(MessageId.Radial, id);
        Assert.Null(replica.TopRadial);
        replica.Apply(Open(20, Slice("z")));
        replica.Apply(Grid()); // a join forgets them
        Assert.Empty(replica.Radials);
    }

    [Fact]
    public void TheHostAppliesARadialPickAndLogsIt()
    {
        using TestServer server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        alice.Motion(0, 0);
        server.Tick(2);
        alice.ActionV4(Pick(41, 3));
        server.Tick(2);
        Assert.Contains(server.Clients.Actions, a => a.Action == "radial 41 3");
        Assert.Equal(0, server.Host.SessionOf(1).Strikes);
        Assert.Equal(1, server.Host.SessionOf(1).RadialPicksApplied);
        QueuedCommand logged = Assert.Single(server.Host.Log.Commands, c => c.Kind == CommandKind.Action && c.Action == ActionKind.RadialPick);
        Assert.Equal((41L, 3), (logged.RadialMenu, logged.RadialElement));
        CommandLog parsed = CommandLog.Parse(server.Host.Log.ToText(), out string error);
        Assert.Null(error);
        QueuedCommand back = Assert.Single(parsed.Commands, c => c.Action == ActionKind.RadialPick);
        Assert.Equal(logged, back);
    }

    [Fact]
    public void A_client_of_version_14_is_refused()
    {
        using var server = TestServer.Start();
        FakeRemote remote = server.Connect("Alice", protocol: 14);
        server.Tick();
        Assert.Equal(DisconnectReason.WrongVersion, remote.Rejected?.Reason);
    }

    [Fact]
    public void TheClientModelShowsTheServerMenuAndPicksBySliceNumberOrAim()
    {
        var model = new RadialModel();
        RadialMenuMessage menu = Open(9, Slice("open"), Slice("disinfect"), Slice("lock"));
        model.OpenServer(menu);
        Assert.Equal(RadialPhase.Open, model.Phase);
        Assert.Equal(9, model.ServerMenu);
        Assert.True(model.ServerCloseButton);
        Assert.Equal(new[] { "open", "disinfect", "lock" }, model.Entries.Select(e => e.Label));
        Assert.Equal(new[] { 1, 2, 3 }, model.Entries.Select(e => e.Element));
        Assert.Equal(-1, model.Highlight);
        Assert.Equal(RadialOutcome.Cancel, model.Confirm(out _)); // the centre: the close button

        model.OpenServer(menu);
        Assert.True(model.SelectNumber(2));
        Assert.False(model.SelectNumber(4));
        Assert.Equal(RadialOutcome.Choice, model.Confirm(out RadialEntry picked));
        Assert.Equal((RadialKind.ServerChoice, 2, "disinfect"), (picked.Kind, picked.Element, picked.Label));

        // aimed with the pointer: the first slice is at the top, clockwise (tg's starting angle 0)
        model.OpenServer(menu);
        model.AimAt(100, 0); // east: the second of three
        Assert.Equal(1, model.Highlight);
        model.AimAt(0, -100);
        Assert.Equal(0, model.Highlight);
        model.Reset();
        Assert.Equal(0, model.ServerMenu);
    }
}
