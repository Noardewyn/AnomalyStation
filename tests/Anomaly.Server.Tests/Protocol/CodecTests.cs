using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Round trips and refusals of every message (docs/ai/specs/protocol.md sections 3 to 5).</summary>
public class CodecTests
{
    private const string Floor = "/turf/open/floor";

    // ---- Hello ----

    [Fact]
    public void HelloRoundTripsWithAndWithoutAToken()
    {
        var plain = new Hello(ProtocolVersion.Current, "Alice", SessionToken.None);
        byte[] bytes = Codec.Encode(plain);
        Assert.Equal((byte)MessageId.Hello, bytes[0]);
        Assert.True(Codec.TryDecode(bytes, out Hello decoded, out ProtocolError error), error.ToString());
        Assert.Equal(plain, decoded);

        var withToken = new Hello(ProtocolVersion.Current, "Bob the Second", new SessionToken(0x1122334455667788UL, 0x99AABBCCDDEEFF00UL));
        Assert.True(Codec.TryDecode(Codec.Encode(withToken), out decoded, out error), error.ToString());
        Assert.Equal(withToken, decoded);
    }

    [Fact]
    public void HelloHasTheDocumentedBytes()
    {
        byte[] bytes = Codec.Encode(new Hello(1, "Al", SessionToken.None));
        Assert.Equal(new byte[] { 1, (byte)'A', (byte)'N', (byte)'O', (byte)'M', 1, 0, 3, (byte)'A', (byte)'l', 0 }, bytes);
        bytes = Codec.Encode(new Hello(1, "", new SessionToken(1, 2)));
        Assert.Equal(new byte[] { 1, (byte)'A', (byte)'N', (byte)'O', (byte)'M', 1, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0 }, bytes);
    }

    [Fact]
    public void HelloAtTheLongestNameFitsTheInboundCap()
    {
        string name = string.Concat(Enumerable.Repeat("\U0001F600", Limits.MaxNameChars));
        Assert.Equal(Limits.MaxNameBytes, System.Text.Encoding.UTF8.GetByteCount(name));
        // version 85: the longest Hello also carries the longest login token, under its own cap; without one it still fits the inbound cap
        var hello = new Hello(ProtocolVersion.Current, name, new SessionToken(5, 6), new string('t', Limits.MaxLoginTokenBytes));
        byte[] bytes = Codec.Encode(hello);
        Assert.Equal(Codec.MaxHelloBytes, bytes.Length);
        Assert.True(Codec.Encode(hello with { LoginToken = null }).Length <= Limits.MaxInboundMessageBytes);
        Assert.True(Codec.TryDecode(bytes, out Hello decoded, out _));
        Assert.Equal(hello, decoded);
        Assert.Throws<ProtocolException>(() => Codec.Encode(new Hello(ProtocolVersion.Current, name + "x", SessionToken.None)));
    }

    [Fact]
    public void HelloOfAnotherVersionDecodesWithoutItsRemainderSoTheHostCanSayWrongVersion()
    {
        byte[] bytes = Msg.Raw(MessageId.Hello, r => r.Bytes((byte)'A', (byte)'N', (byte)'O', (byte)'M', (byte)(ProtocolVersion.Current + 1), 0, 0xFF, 0xFF, 0xFF));
        Assert.True(Codec.TryDecode(bytes, out Hello hello, out ProtocolError error), error.ToString());
        Assert.Equal((ushort)(ProtocolVersion.Current + 1), hello.Protocol);
        Assert.Null(hello.Name);
        Assert.True(hello.ReconnectToken.IsNone);
        // The magic and the version are still required.
        bytes = Msg.Raw(MessageId.Hello, r => r.Bytes((byte)'A', (byte)'N', (byte)'O', (byte)'M', (byte)(ProtocolVersion.Current + 1)));
        Assert.False(Codec.TryDecode(bytes, out hello, out error));
        Assert.Equal(ProtocolError.Truncated, error);
    }

    [Fact]
    public void HelloRefusalsAreNamed()
    {
        byte[] good = Codec.Encode(new Hello(ProtocolVersion.Current, "Al", SessionToken.None));
        byte[] badMagic = (byte[])good.Clone();
        badMagic[2] = (byte)'X';
        Assert.False(Codec.TryDecode(badMagic, out Hello _, out ProtocolError error));
        Assert.Equal(ProtocolError.BadMagic, error);

        byte[] flags = (byte[])good.Clone();
        flags[^1] = 8; // (version 97 took bit 2: WaitsForLoad)
        Assert.False(Codec.TryDecode(flags, out Hello _, out error));
        Assert.Equal(ProtocolError.BadValue, error);

        // version 85: a login flag with no login token (a null or an empty string) is not canonical.
        foreach (byte none in new byte[] { 0, 1 })
        {
            byte[] noLogin = Msg.Raw(MessageId.Hello, r => r.Bytes((byte)'A', (byte)'N', (byte)'O', (byte)'M', (byte)ProtocolVersion.Current, 0).String("Al").Byte(2).Byte(none));
            Assert.False(Codec.TryDecode(noLogin, out Hello _, out error));
            Assert.Equal(ProtocolError.BadValue, error);
        }

        // A token flag with an all-zero token is not canonical.
        byte[] zeroToken = Msg.Raw(MessageId.Hello, r => r.Bytes((byte)'A', (byte)'N', (byte)'O', (byte)'M', (byte)ProtocolVersion.Current, 0).String("Al").Byte(1).Bytes(new byte[16]));
        Assert.False(Codec.TryDecode(zeroToken, out Hello _, out error));
        Assert.Equal(ProtocolError.BadValue, error);

        // A name over its byte cap, invalid UTF-8, a trailing byte, and a message over the inbound cap.
        byte[] longName = Msg.Raw(MessageId.Hello, r => r.Bytes((byte)'A', (byte)'N', (byte)'O', (byte)'M', (byte)ProtocolVersion.Current, 0).String(new string('x', Limits.MaxNameBytes + 1)).Byte(0));
        Assert.False(Codec.TryDecode(longName, out Hello _, out error));
        Assert.Equal(ProtocolError.BadString, error);
        byte[] badUtf8 = Msg.Raw(MessageId.Hello, r => r.Bytes((byte)'A', (byte)'N', (byte)'O', (byte)'M', (byte)ProtocolVersion.Current, 0, 2, 0xFF, 0));
        Assert.False(Codec.TryDecode(badUtf8, out Hello _, out error));
        Assert.Equal(ProtocolError.BadString, error);
        Assert.False(Codec.TryDecode(good.Append((byte)0).ToArray(), out Hello _, out error));
        Assert.Equal(ProtocolError.TrailingBytes, error);
        Assert.False(Codec.TryDecode(new byte[Codec.MaxHelloBytes + 1].Select((_, i) => i == 0 ? (byte)1 : (byte)0).ToArray(), out Hello _, out error));
        Assert.Equal(ProtocolError.TooLong, error);
    }

    [Fact]
    public void ANullOrEmptyNameDecodesAndTheNameRulesRefuseIt()
    {
        foreach (string name in new string[] { null, "" })
        {
            Assert.True(Codec.TryDecode(Codec.Encode(new Hello(ProtocolVersion.Current, name, SessionToken.None)), out Hello hello, out _));
            Assert.Equal(name, hello.Name);
            Assert.False(NameRules.IsValid(hello.Name));
        }
    }

    [Theory]
    [InlineData("Alice", true)]
    [InlineData("A", true)]
    [InlineData("Dr. Mary-Jane O'Neil", true)]
    [InlineData("\u0410\u043B\u0435\u043A\u0441\u0435\u0439", true)]
    [InlineData("a b", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("tab\there", false)]
    [InlineData("new\nline", false)]
    [InlineData("nul\0", false)]
    [InlineData("bell\a", false)]
    [InlineData("\u200Bzero width", false)]
    [InlineData("\u2028sep", false)]
    [InlineData("\uE000private", false)]
    public void NameRulesRefuseControlCharactersAndEmptyNames(string name, bool valid)
    {
        Assert.Equal(valid, NameRules.IsValid(name));
    }

    [Fact]
    public void NameRulesCountCharactersNotBytes()
    {
        Assert.True(NameRules.IsValid(new string('a', Limits.MaxNameChars)));
        Assert.False(NameRules.IsValid(new string('a', Limits.MaxNameChars + 1)));
        Assert.True(NameRules.IsValid(string.Concat(Enumerable.Repeat("\U0001F600", Limits.MaxNameChars))));
        Assert.False(NameRules.IsValid(string.Concat(Enumerable.Repeat("\U0001F600", Limits.MaxNameChars + 1))));
        Assert.False(NameRules.IsValid(null));
    }

    // ---- InputFrame ----

    [Theory]
    [InlineData(0u, HeldDirs.None)]
    [InlineData(1u, HeldDirs.North)]
    [InlineData(127u, HeldDirs.North | HeldDirs.East)]
    [InlineData(128u, HeldDirs.South | HeldDirs.West)]
    [InlineData(uint.MaxValue, HeldDirs.All)]
    public void InputFrameRoundTrips(uint seq, HeldDirs dirs)
    {
        byte[] bytes = Codec.Encode(new InputFrame(seq, dirs));
        Assert.True(bytes.Length <= Codec.MaxInputFrameBytes);
        Assert.True(Codec.TryDecode(bytes, out InputFrame frame, out ProtocolError error), error.ToString());
        Assert.Equal(new InputFrame(seq, dirs), frame);
    }

    [Fact]
    public void InputFrameHasTheDocumentedBytesAndRefusesBadBits()
    {
        Assert.Equal(new byte[] { 7, 0x2C, 0x01 | 0x04 }, Codec.Encode(new InputFrame(44, HeldDirs.North | HeldDirs.East)));
        Assert.Equal(Codec.MaxInputFrameBytes, Codec.Encode(new InputFrame(uint.MaxValue, HeldDirs.All)).Length);
        Assert.Throws<ProtocolException>(() => Codec.Encode(new InputFrame(1, (HeldDirs)16)));
        Assert.False(Codec.TryDecode(new byte[] { 7, 1, 16 }, out InputFrame _, out ProtocolError error));
        Assert.Equal(ProtocolError.BadValue, error);
        Assert.False(Codec.TryDecode(new byte[] { 7, 1, 0xFF }, out InputFrame _, out error));
        Assert.Equal(ProtocolError.BadValue, error);
        // A seq over 32 bits.
        Assert.False(Codec.TryDecode(Msg.Raw(MessageId.InputFrame, r => r.VarUInt(1UL << 32).Byte(0)), out InputFrame _, out error));
        Assert.Equal(ProtocolError.BadValue, error);
        Assert.False(Codec.TryDecode(new byte[] { 7, 1 }, out InputFrame _, out error));
        Assert.Equal(ProtocolError.Truncated, error);
    }

    // ---- Welcome ----

    [Fact]
    public void WelcomeRoundTripsAtItsExtremes()
    {
        foreach (var welcome in new[]
                 {
                     new Welcome(1, new SessionToken(1, 2), 0, 20, 0),
                     new Welcome(int.MaxValue, new SessionToken(ulong.MaxValue, ulong.MaxValue), long.MaxValue, 255, long.MaxValue),
                     new Welcome(0, SessionToken.None, 123456, 30, 98765),
                 })
        {
            byte[] bytes = Codec.Encode(welcome);
            Assert.True(bytes.Length <= Codec.MaxWelcomeBytes);
            Assert.True(Codec.TryDecode(bytes, out Welcome decoded, out ProtocolError error), error.ToString());
            Assert.Equal(welcome, decoded);
        }
    }

    [Fact]
    public void WelcomeRefusesBadValues()
    {
        Assert.Throws<ProtocolException>(() => Codec.Encode(new Welcome(-1, default, 0, 20, 0)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new Welcome(1, default, -1, 20, 0)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new Welcome(1, default, 0, 0, 0)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new Welcome(1, default, 0, 20, -5)));
        // A session over 31 bits, a tick over 63 bits, a tick rate of 0.
        Assert.False(Codec.TryDecode(Msg.Raw(MessageId.Welcome, r => r.VarUInt(1UL << 31).Bytes(new byte[16]).VarUInt(0).Byte(20).VarUInt(0)), out Welcome _, out ProtocolError error));
        Assert.Equal(ProtocolError.BadValue, error);
        Assert.False(Codec.TryDecode(Msg.Raw(MessageId.Welcome, r => r.VarUInt(1).Bytes(new byte[16]).VarUInt(1UL << 63).Byte(20).VarUInt(0)), out Welcome _, out error));
        Assert.Equal(ProtocolError.BadValue, error);
        Assert.False(Codec.TryDecode(Msg.Raw(MessageId.Welcome, r => r.VarUInt(1).Bytes(new byte[16]).VarUInt(0).Byte(0).VarUInt(0)), out Welcome _, out error));
        Assert.Equal(ProtocolError.BadValue, error);
    }

    // ---- MapGrid ----

    [Fact]
    public void MapGridRoundTripsAndExpandsToCells()
    {
        MapGrid grid = Msg.Map(4, 3, 2, new[] { "/turf/closed/wall", Floor, "/turf/open/space" },
            new TurfRun(4, 0), new TurfRun(8, 1), new TurfRun(4, 0), new TurfRun(7, 2), new TurfRun(1, 1));
        byte[] bytes = Codec.Encode(grid);
        Assert.True(Codec.TryDecode(bytes, out MapGrid decoded, out ProtocolError error), error.ToString());
        Msg.AssertEqual(grid, decoded);
        ushort[] cells = decoded.ExpandCells();
        Assert.Equal(24, cells.Length);
        Assert.Equal(new ushort[] { 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 2, 2, 2, 2, 2, 2, 2, 1 }, cells);
    }

    [Fact]
    public void MapGridOfTheLargestMapIsOneRunOrAMillionRuns()
    {
        int cells = Limits.MaxMapSize * Limits.MaxMapSize * Limits.MaxMapLevels;
        MapGrid one = Msg.FlatMap(Limits.MaxMapSize, Limits.MaxMapSize, Limits.MaxMapLevels);
        byte[] bytes = Codec.Encode(one);
        Assert.True(Codec.TryDecode(bytes, out MapGrid decoded, out _));
        Msg.AssertEqual(one, decoded);
        Assert.Equal(cells, decoded.ExpandCells().Length);

        // Alternating types: the most runs a map can have.
        var many = new MapGrid { Tick = 1, Width = Limits.MaxMapSize, Height = Limits.MaxMapSize, Levels = Limits.MaxMapLevels };
        many.TurfTypes.AddRange(new[] { "/turf/a", "/turf/b" });
        for (int i = 0; i < cells; i++)
            many.Runs.Add(new TurfRun(1, i & 1));
        bytes = Codec.Encode(many);
        Assert.True(bytes.Length < Limits.MaxOutboundMessageBytes);
        Assert.True(Codec.TryDecode(bytes, out decoded, out ProtocolError error), error.ToString());
        Assert.Equal(cells, decoded.Runs.Count);
        Assert.Equal(many.Runs[1000], decoded.Runs[1000]);
    }

    [Fact]
    public void MapGridWithTheMostTypesAndTheLongestPaths()
    {
        var grid = new MapGrid { Tick = 0, Width = 2, Height = 1, Levels = 1 };
        for (int i = 0; i < Limits.MaxTypeTableEntries; i++)
            grid.TurfTypes.Add("/t/" + i);
        grid.Runs.Add(new TurfRun(1, 0));
        grid.Runs.Add(new TurfRun(1, Limits.MaxTypeTableEntries - 1));
        Assert.True(Codec.TryDecode(Codec.Encode(grid), out MapGrid decoded, out ProtocolError error), error.ToString());
        Msg.AssertEqual(grid, decoded);

        string longest = "/" + new string('a', Limits.MaxTypePathBytes - 1);
        MapGrid one = Msg.Map(1, 1, 1, new[] { longest }, new TurfRun(1, 0));
        Assert.True(Codec.TryDecode(Codec.Encode(one), out decoded, out _));
        Assert.Equal(longest, decoded.TurfTypes[0]);
        MapGrid over = Msg.Map(1, 1, 1, new[] { longest + "a" }, new TurfRun(1, 0));
        Assert.Throws<ProtocolException>(() => Codec.Encode(over));
        grid.TurfTypes.Add("overflow");
        Assert.Throws<ProtocolException>(() => Codec.Encode(grid));
    }

    [Fact]
    public void MapGridInconsistenciesAreRefusedAtBothEnds()
    {
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Map(0, 1, 1, new[] { Floor }, new TurfRun(1, 0))));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Map(256, 1, 1, new[] { Floor }, new TurfRun(256, 0))));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Map(1, 1, Limits.MaxMapLevels + 1, new[] { Floor }, new TurfRun(Limits.MaxMapLevels + 1, 0))));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Map(2, 2, 1, new[] { Floor }, new TurfRun(3, 0))));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Map(2, 2, 1, new[] { Floor }, new TurfRun(5, 0))));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Map(2, 2, 1, new[] { Floor }, new TurfRun(0, 0), new TurfRun(4, 0))));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Map(2, 2, 1, new[] { Floor }, new TurfRun(4, 1))));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Map(2, 2, 1, new string[] { null }, new TurfRun(4, 0))));

        byte[] Craft(int w, int h, int l, string[] types, params (int count, int type)[] runs) => Msg.Raw(MessageId.MapGrid, r =>
        {
            r.VarUInt(0).VarUInt((ulong)w).VarUInt((ulong)h).VarUInt((ulong)l).VarUInt((ulong)types.Length);
            foreach (string t in types) r.String(t);
            r.VarUInt((ulong)runs.Length);
            foreach (var (count, type) in runs) r.VarUInt((ulong)count).VarUInt((ulong)type);
            r.VarUInt(0).VarUInt(0); // version 2: no appearance runs, no solidity bits
            r.VarUInt(0).VarUInt(0).VarUInt(0).Bytes(0, 0, 0, 0); // version 3: no flag runs, no base lights, no base-light runs, no starlight
            r.VarUInt(0).VarUInt(0); // version 5: no gas kinds, no gas cells
            r.VarUInt(0); // version 44: no ceilings
            r.VarUInt(0); // version 60: no levels without parallax
            r.VarUInt(0); // version 76: no linked levels
            r.VarUInt(0); // version 87: the game's own sky and sun
            r.Bytes(0xD0, 0x07); // version 91: walls and ceiling 2000 mm high
            r.VarUInt(0); // version 96: no turf lights
        });

        Assert.True(Codec.TryDecode(Craft(2, 2, 1, new[] { Floor }, (4, 0)), out MapGrid _, out _));
        foreach (byte[] bad in new[]
                 {
                     Craft(0, 2, 1, new[] { Floor }, (0, 0)),
                     Craft(256, 2, 1, new[] { Floor }, (512, 0)),
                     Craft(2, 2, Limits.MaxMapLevels + 1, new[] { Floor }, (4 * (Limits.MaxMapLevels + 1), 0)),
                     Craft(2, 2, 1, new[] { Floor }, (3, 0)),
                     Craft(2, 2, 1, new[] { Floor }, (5, 0)),
                     Craft(2, 2, 1, new[] { Floor }, (0, 0), (4, 0)),
                     Craft(2, 2, 1, new[] { Floor }, (4, 1)),
                     Craft(2, 2, 1, new string[0]),
                     Craft(2, 2, 1, new string[] { null }, (4, 0)),
                 })
        {
            Assert.False(Codec.TryDecode(bad, out MapGrid value, out ProtocolError error));
            Assert.Null(value);
            Assert.NotEqual(ProtocolError.None, error);
        }
    }

    // ---- Snapshot ----

    [Fact]
    public void SnapshotRoundTrips()
    {
        Snapshot s = Msg.Snapshot(100, 3, 17, new[] { "/mob", "/obj/item/pen" },
            Msg.State(3, 0, 5, 6, 1, 2, "Alice", 10.666667f, 90),
            Msg.State(4, 1, 255, 255, 16, 10, null, 32f, 100),
            Msg.State(1_000_000, 1, 1, 1, 1, 0, "", 0f, 0));
        Assert.True(Codec.TryDecode(Codec.Encode(s), out Snapshot decoded, out ProtocolError error), error.ToString());
        Msg.AssertEqual(s, decoded);
        Snapshot empty = Msg.Snapshot(0, 0, 0, new string[0]);
        Assert.True(Codec.TryDecode(Codec.Encode(empty), out decoded, out _));
        Msg.AssertEqual(empty, decoded);
    }

    [Fact]
    public void SnapshotAtItsListCaps()
    {
        // The most entities one message may hold, with the longest names where the size cap allows.
        var s = new Snapshot { Tick = 5000, OwnEntity = 1 };
        s.Types.AddRange(new[] { "/mob" });
        for (int i = 1; i <= Limits.MaxEntitiesPerMessage; i++)
            s.Entities.Add(Msg.State(i, 0, 1 + i % 255, 1 + i % 200, 1 + i % 16, 4, null, 0f, i % 1000));
        byte[] bytes = Codec.Encode(s);
        Assert.True(bytes.Length < Limits.MaxOutboundMessageBytes);
        Assert.True(Codec.TryDecode(bytes, out Snapshot decoded, out ProtocolError error), error.ToString());
        Assert.Equal(Limits.MaxEntitiesPerMessage, decoded.Entities.Count);
        Assert.Equal(s.Entities[12345], decoded.Entities[12345]);
        s.Entities.Add(Msg.State(Limits.MaxEntitiesPerMessage + 1));
        Assert.Throws<ProtocolException>(() => Codec.Encode(s));

        var names = new Snapshot { Tick = 5 };
        names.Types.Add("/mob");
        string longest = new('n', Limits.MaxEntityNameBytes);
        for (int i = 1; i <= 100; i++)
            names.Entities.Add(Msg.State(i, 0, 3, 3, 1, 2, longest));
        Assert.True(Codec.TryDecode(Codec.Encode(names), out decoded, out _));
        Assert.Equal(longest, decoded.Entities[99].Name);
        names.Entities[0] = Msg.State(1, 0, 3, 3, 1, 2, longest + "n");
        Assert.Throws<ProtocolException>(() => Codec.Encode(names));

        var types = new Snapshot { Tick = 5 };
        for (int i = 0; i < Limits.MaxTypeTableEntries; i++)
            types.Types.Add("/t/" + i);
        types.Entities.Add(Msg.State(1, Limits.MaxTypeTableEntries - 1));
        Assert.True(Codec.TryDecode(Codec.Encode(types), out decoded, out _));
        Assert.Equal(Limits.MaxTypeTableEntries, decoded.Types.Count);
        types.Types.Add("/one/too/many");
        Assert.Throws<ProtocolException>(() => Codec.Encode(types));
    }

    [Fact]
    public void SnapshotRefusesWhatTheDocumentationForbids()
    {
        string[] types = { "/mob" };
        // Out-of-order and repeated ids, id 0, a type outside the table, coordinates and directions outside their ranges, NaN.
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Snapshot(1, 0, 0, types, Msg.State(5), Msg.State(3))));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Snapshot(1, 0, 0, types, Msg.State(5), Msg.State(5))));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Snapshot(1, 0, 0, types, Msg.State(0))));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Snapshot(1, 0, 0, types, Msg.State(-4))));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Snapshot(-1, 0, 0, types)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Snapshot(1, -1, 0, types)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Snapshot(1, 0, 0, new string[] { null })));
        foreach (EntityState bad in new[]
                 {
                     Msg.State(1, x: 0), Msg.State(1, x: 256), Msg.State(1, y: 0), Msg.State(1, y: 256), Msg.State(1, z: 0), Msg.State(1, z: Limits.MaxMapLevels + 1),
                     Msg.State(1, dir: 3), Msg.State(1, dir: 16), Msg.State(1, dir: 255), Msg.State(1, glide: float.NaN), Msg.State(1, glide: float.PositiveInfinity),
                     Msg.State(1, glide: -1f), Msg.State(1, glide: Limits.MaxGlideSize + 1), Msg.State(1, lastMove: -1), Msg.State(1, type: -1),
                 })
        {
            Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Snapshot(1, 0, 0, types, bad)));
        }

        // The decoder checks the type against the Snapshot's table.
        byte[] craft = Msg.Raw(MessageId.Snapshot, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(1).String("/mob").VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(1)
            .VarInt(1).VarUInt(1).VarUInt(1).VarUInt(1).VarUInt(1).Byte(2).String("").Single(0f).VarUInt(0).Byte(0).VarUInt(0).Byte(0).Byte(0).VarUInt(0).Byte(0));
        Assert.False(Codec.TryDecode(craft, out Snapshot _, out ProtocolError error));
        Assert.Equal(ProtocolError.BadValue, error);
        // Same bytes with type 0 decode.
        byte[] ok = Msg.Raw(MessageId.Snapshot, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(1).String("/mob").VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(1)
            .VarInt(1).VarUInt(0).VarUInt(1).VarUInt(1).VarUInt(1).Byte(2).String("").Single(0f).VarUInt(0).Byte(0).VarUInt(0).Byte(0).Byte(0).VarUInt(0).Byte(0));
        Assert.True(Codec.TryDecode(ok, out Snapshot decoded, out error), error.ToString());
        Assert.Equal(Msg.State(1, lastMove: 1), decoded.Entities[0]);
        // An entity cannot have moved after the Snapshot's own tick, and its appearance must be in the Snapshot's table.
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Snapshot(1, 0, 0, types, Msg.State(1, lastMove: 2))));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Snapshot(1, 0, 0, types, Msg.State(1) with { Appearance = new TableRef(1, true) })));
    }

    // ---- Delta ----

    private static Delta SampleDelta() => Msg.Delta(4242, own: 3, seq: 9, firstNewType: 2, newTypes: new[] { "/obj/item/pen", "/obj/structure/table" },
        spawned: new[] { Msg.State(70, 2, 5, 5, 1, 1, "pen", 0f, 4242), Msg.State(71, 3, 6, 5, 1, 2, "table", 0f, 4242) },
        changed: new[]
        {
            Msg.Move(3, 10, 11, 4242, dir: 4, glide: 10.666667f),
            new EntityChange { Id = 9, Mask = NetField.Name, Name = "Bob" },
            new EntityChange { Id = 12, Mask = NetField.Z | NetField.Dir, Z = 2, Dir = 9 },
            new EntityChange { Id = 20, Mask = Msg.Version1Fields, X = 3, Y = 4, Z = 5, Dir = 6, Name = null, GlideSize = 32f, LastMoveTick = 4242 },
        },
        despawned: new long[] { 8, 15, 14, 100000 });

    [Fact]
    public void DeltaRoundTrips()
    {
        Delta d = SampleDelta();
        byte[] bytes = Codec.Encode(d);
        Assert.True(Codec.TryDecode(bytes, out Delta decoded, out ProtocolError error), error.ToString());
        Msg.AssertEqual(d, decoded);
        Delta empty = Msg.Delta(1);
        Assert.True(Codec.TryDecode(Codec.Encode(empty), out decoded, out _));
        Msg.AssertEqual(empty, decoded);
    }

    [Fact]
    public void DeltaBodyEncodedOnceAndHeadersPerConnectionEqualTheWholeMessage()
    {
        Delta d = SampleDelta();
        byte[] body = Codec.EncodeDeltaBody(d);
        byte[] whole = Codec.Encode(d);
        var destination = new byte[Codec.MaxDeltaHeaderBytes + body.Length];
        int length = Codec.EncodeDelta(d.Tick, d.LastInputSeq, d.OwnEntity, body, destination);
        Assert.Equal(whole, destination.AsSpan(0, length).ToArray());

        // Another connection: only the header differs, the body bytes are the same.
        var other = new byte[destination.Length];
        int otherLength = Codec.EncodeDelta(d.Tick, 77, 5, body, other);
        Assert.True(Codec.TryDecode(other.AsSpan(0, otherLength), out Delta decoded, out _));
        Assert.Equal(77u, decoded.LastInputSeq);
        Assert.Equal(5, decoded.OwnEntity);
        Msg.AssertBodyEqual(d, decoded);
        Assert.True(other.AsSpan(otherLength - Codec.EmptyOwnBlockBytes - body.Length, body.Length).SequenceEqual(body));
    }

    [Fact]
    public void DeltaBodyIgnoresTheHeaderFieldsButTheTick()
    {
        Delta a = SampleDelta();
        Delta b = SampleDelta();
        b.LastInputSeq = 5;
        b.OwnEntity = 999;
        b.Own.InputBufferDepth = 3; // the private block is no part of the body
        Assert.Equal(Codec.EncodeDeltaBody(a), Codec.EncodeDeltaBody(b));
        // The tick is: an entity's last move tick travels relative to it.
        b.Tick = a.Tick + 1;
        Assert.NotEqual(Codec.EncodeDeltaBody(a), Codec.EncodeDeltaBody(b));
    }

    [Fact]
    public void DeltaBodyReusesACallerBufferWithoutAllocatingInTheSteadyState()
    {
        Delta d = SampleDelta();
        byte[] buffer = null;
        int length = Codec.EncodeDeltaBody(d, ref buffer);
        Assert.NotNull(buffer);
        Assert.True(length > 0 && length <= buffer.Length);
        Assert.Equal(Codec.EncodeDeltaBody(d), buffer.AsSpan(0, length).ToArray());
        byte[] first = buffer;
        for (int i = 0; i < 10; i++)
            Assert.Equal(length, Codec.EncodeDeltaBody(d, ref buffer));
        Assert.Same(first, buffer);

        // A bigger body replaces the buffer; a Span destination too small is reported.
        for (int i = 0; i < 500; i++)
            d.Spawned.Add(Msg.State(1000 + i, 2, 3, 3, 1, 2, "more"));
        int bigger = Codec.EncodeDeltaBody(d, ref buffer);
        Assert.True(bigger > length);
        Assert.NotSame(first, buffer);
        Assert.True(Codec.MaxDeltaBodySize(d) >= bigger);
        var small = new byte[bigger - 1];
        ProtocolException e = Assert.Throws<ProtocolException>(() => Codec.EncodeDeltaBody(d, small));
        Assert.Equal(ProtocolError.BufferTooSmall, e.Error);
    }

    [Fact]
    public void DeltaWithTheDocumentedBytes()
    {
        // tick 300, seq 1, own 5; types 2.."/a"; spawn id 6 (type 2, 3,4,1, dir 2, name "n", glide 0, last move 300);
        // change id 5 moves to 4,4 at tick 300 (mask X|Y|LastMoveTick: wire bits 2, 3, 6 = 0x4C); despawn 9 and 7.
        var d = Msg.Delta(300, 5, 1, 2, new[] { "/a" }, new[] { Msg.State(6, 2, 3, 4, 1, 2, "n", 0f, 300) },
            new[] { Msg.Move(5, 4, 4, 300) }, new long[] { 9, 7 });
        byte[] expected =
        {
            5,
            0xAC, 0x02, // tick 300
            1, // last input seq
            5, // own entity
            2, // first new type
            1, 3, (byte)'/', (byte)'a', // one new type
            0, // parts: no new table entries, no cell changes
            1, // one spawned
            12, // id 6: zigzag(6 - 0) = 12
            2, 3, 4, 1, 2, // type, x, y, z, dir
            2, (byte)'n', // name
            0, 0, 0, 0, // glide
            0, // last move tick: 0 ticks ago
            0, // flags: not a capsule, standing
            0, // appearance: none
            1, // one changed
            10, // id 5: zigzag(5) = 10
            0x4C, // mask: X, Y, LastMoveTick in wire order
            4, 4, 0, // x, y, last move tick (0 ticks ago)
            2, // two despawned
            18, // id 9: zigzag(9) = 18
            3, // id 7: 7 - 9 = -2: zigzag 3
            0, // version 44: no ceilings
            0, // version 47: no sun
            0, // version 96: no turf lights
            0, 0, 0, 0, // the private block: empty (flags, input buffer depth, version 43's actions done and flags)
        };
        Assert.Equal(expected, Codec.Encode(d));
        Assert.True(Codec.TryDecode(expected, out Delta decoded, out ProtocolError error), error.ToString());
        Msg.AssertEqual(d, decoded);
    }

    [Fact]
    public void ADeltaWithCapsulesTablesAndCellChangesHasTheDocumentedBytes()
    {
        // tick 10: one new resource "r", one new string "s", one new appearance (icon 1, state 1); a capsule mob spawns at
        // (3,4,1) standing at (3.5 tiles, 4 tiles) looking east and lying (angle 90) with appearance 1; entity 6 changes pos
        // and yaw only (mask 0b11); cell (2,2,1) becomes solid and cell (3,2,1) takes the turf appearance 1.
        var d = new Delta { Tick = 10, FirstNewType = 1, FirstNewResource = 1, FirstNewString = 1, FirstNewAppearance = 1 };
        d.NewResources.Add("r");
        d.NewStrings.Add("s");
        d.NewAppearances.Add(new AppearanceEntry { Icon = TableRef.Public(1), IconState = TableRef.Public(1) });
        d.Spawned.Add(Msg.State(6, 0, 3, 4, 1, 4, "", 0f, 9) with { HasPos = true, PosX = 896, PosY = 1024, Yaw = 64, Posture = PostureBits.Encode(true, 90), Appearance = TableRef.Public(1) });
        d.Changed.Add(new EntityChange { Id = 6, Mask = NetField.Pos | NetField.Yaw, PosX = 900, PosY = 1030, Yaw = 65 });
        d.SolidChanged.Add(new CellSolidChange(2, 2, 1, true));
        d.TurfAppearanceChanged.Add(new CellAppearanceChange(3, 2, 1, TableRef.Public(1)));
        byte[] expected =
        {
            5, 10, 0, 0, // header: tick 10, seq 0, own 0
            1, 0, // first new type 1, no new types
            0x1F, // parts: resources, strings, appearances, solid changes, turf appearance changes
            1, 1, 2, (byte)'r', // resources: one, from index 1
            1, 1, 2, (byte)'s', // strings
            1, 1, 0b11, 2, 2, // appearances: one, from index 1, presence icon + state, both public index 1 (1 << 1)
            // spawned: one; id 6 (zigzag 12), type 0, 3,4,1, dir 4, empty name, glide 0, last move 1 tick ago,
            // flags capsule + posture, pos 896,1024 as u16 pairs, yaw 64, posture lying + 90 degrees, appearance 1
            1, 12, 0, 3, 4, 1, 4, 1, 0, 0, 0, 0, 1, 3, 0x80, 0x03, 0x00, 0x04, 64, 0b011, 1,
            // changed: one; id 6, mask Pos|Yaw (wire bits 0 and 1), pos 900,1030, yaw 65
            1, 12, 0b11, 0x84, 0x03, 0x06, 0x04, 65,
            0, // despawned: none
            1, 2, 2, 1, 1, // solid changes: (2,2,1) is solid
            1, 3, 2, 1, 1, // turf appearance changes: (3,2,1) has appearance 1
            0, // version 44: no ceilings
            0, // version 47: no sun
            0, // version 96: no turf lights
            0, 0, 0, 0, // the private block: empty
        };
        Assert.Equal(expected, Codec.Encode(d));
        Assert.True(Codec.TryDecode(expected, out Delta decoded, out ProtocolError error), error.ToString());
        Msg.AssertEqual(d, decoded);
    }

    [Fact]
    public void DeltaAtItsListCaps()
    {
        // Spawned, changed and despawned share the cap of MaxEntitiesPerMessage.
        var d = new Delta { Tick = 9, FirstNewType = Limits.MaxTypeTableEntries - 2 };
        d.NewTypes.AddRange(new[] { "/x", "/y" });
        int perList = Limits.MaxEntitiesPerMessage / 3;
        for (int i = 1; i <= perList; i++)
        {
            d.Spawned.Add(Msg.State(3L * i, 1, 1 + i % 255, 2, 1, 2));
            d.Changed.Add(Msg.Move(3L * i + 1, 1 + i % 255, 2, 9));
            d.Despawned.Add(3L * i + 2);
        }

        d.Despawned.Add(Limits.MaxEntitiesPerMessage * 4L);
        Assert.Equal(Limits.MaxEntitiesPerMessage, d.Spawned.Count + d.Changed.Count + d.Despawned.Count);
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta decoded, out ProtocolError error), error.ToString());
        Msg.AssertEqual(d, decoded);
        d.Despawned.Add(Limits.MaxEntitiesPerMessage * 4L + 1);
        Assert.Throws<ProtocolException>(() => Codec.Encode(d));

        // The type table may not pass its cap.
        var types = Msg.Delta(1, firstNewType: Limits.MaxTypeTableEntries - 1, newTypes: new[] { "/a", "/b" });
        Assert.Throws<ProtocolException>(() => Codec.Encode(types));
        Assert.False(Codec.TryDecode(Msg.Raw(MessageId.Delta, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(Limits.MaxTypeTableEntries - 1).VarUInt(2).String("/a").String("/b").Byte(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(0).Byte(0)), out Delta _, out error));
        Assert.Equal(ProtocolError.CountTooLarge, error);
    }

    [Fact]
    public void DeltaListsMayCarryIdsInAnyOrder()
    {
        Delta d = Msg.Delta(5, spawned: new[] { Msg.State(900), Msg.State(20), Msg.State(500) },
            changed: new[] { Msg.Move(40, 2, 2, 5), Msg.Move(7, 3, 3, 5) }, despawned: new long[] { 1000, 1, 999 });
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta decoded, out ProtocolError error), error.ToString());
        Msg.AssertEqual(d, decoded);
    }

    [Fact]
    public void DeltaRefusesWhatTheDocumentationForbids()
    {
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Delta(-1)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Delta(1, own: -1)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Delta(1, despawned: new long[] { 0 })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Delta(1, despawned: new long[] { 4, 4 })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Delta(1, changed: new[] { new EntityChange { Id = 4, Mask = NetField.None } })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Delta(1, changed: new[] { new EntityChange { Id = 4, Mask = NetField.Id | NetField.X, X = 3 } })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Delta(1, changed: new[] { new EntityChange { Id = 4, Mask = NetField.Type } })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Delta(1, changed: new[] { new EntityChange { Id = 4, Mask = NetField.OwnMob } })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Delta(1, changed: new[] { new EntityChange { Id = 4, Mask = NetField.X, X = 0 } })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Delta(1, changed: new[] { new EntityChange { Id = 4, Mask = NetField.GlideSize, GlideSize = float.NaN } })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Delta(1, changed: new[] { new EntityChange { Id = 4, Mask = NetField.Dir, Dir = 7 } })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Delta(1, firstNewType: -1)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Delta(1, newTypes: new string[] { null })));

        // Crafted bytes: a zero mask, a mask with the high bit, a NaN glide, a negative first id.
        byte[] Body(Action<Msg.RawWriter> changed) => Msg.Raw(MessageId.Delta, r =>
        {
            r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(0).VarUInt(0);
            changed(r);
            r.VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(0).Byte(0).VarUInt(0).Byte(0); // despawned, ceilings, sun (version 47), turf lights (version 96), the private block
        });
        const ulong MaskX = 1 << 2, MaskGlide = 1 << 10;

        Assert.True(Codec.TryDecode(Body(r => r.VarUInt(1).VarInt(4).VarUInt(MaskX).VarUInt(5)), out Delta _, out ProtocolError error), error.ToString());
        Assert.False(Codec.TryDecode(Body(r => r.VarUInt(1).VarInt(4).VarUInt(0)), out Delta _, out error));
        Assert.Equal(ProtocolError.BadValue, error);
        Assert.False(Codec.TryDecode(Body(r => r.VarUInt(1).VarInt(4).VarUInt(1 << 13).Byte(0)), out Delta _, out error));
        Assert.Equal(ProtocolError.BadValue, error);
        Assert.False(Codec.TryDecode(Body(r => r.VarUInt(1).VarInt(4).VarUInt(MaskGlide).Bytes(0x00, 0x00, 0xC0, 0x7F)), out Delta _, out error));
        Assert.Equal(ProtocolError.BadValue, error);
        Assert.False(Codec.TryDecode(Body(r => r.VarUInt(1).VarInt(-4).VarUInt(MaskX).VarUInt(5)), out Delta _, out error));
        Assert.Equal(ProtocolError.BadValue, error);
        Assert.False(Codec.TryDecode(Body(r => r.VarUInt(1).VarInt(0).VarUInt(MaskX).VarUInt(5)), out Delta _, out error));
        Assert.Equal(ProtocolError.BadValue, error);
        // The largest id is valid; an id after it overflows.
        Assert.True(Codec.TryDecode(Body(r => r.VarUInt(1).VarInt(long.MaxValue).VarUInt(MaskX).VarUInt(5)), out Delta _, out error), error.ToString());
        Assert.False(Codec.TryDecode(Body(r => r.VarUInt(2).VarInt(long.MaxValue).VarUInt(MaskX).VarUInt(5).VarInt(1).VarUInt(MaskX).VarUInt(5)), out Delta _, out error));
        Assert.Equal(ProtocolError.BadValue, error);
    }

    // ---- ServerText and DisconnectInfo ----

    [Fact]
    public void ServerTextRoundTripsAtItsCaps()
    {
        foreach (var text in new[]
                 {
                     new ServerText(null, null),
                     new ServerText("", ""),
                     new ServerText("chat", "<span class='notice'>You see a pen.</span>"),
                     new ServerText(new string('t', Limits.MaxServerTextTypeBytes), new string('h', Limits.MaxServerTextBytes)),
                     new ServerText("t", string.Concat(Enumerable.Repeat("\U0001F600", Limits.MaxServerTextBytes / 4))),
                 })
        {
            Assert.True(Codec.TryDecode(Codec.Encode(text), out ServerText decoded, out ProtocolError error), error.ToString());
            Assert.Equal(text, decoded);
        }

        Assert.Throws<ProtocolException>(() => Codec.Encode(new ServerText(new string('t', Limits.MaxServerTextTypeBytes + 1), "")));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new ServerText("", new string('h', Limits.MaxServerTextBytes + 1))));
    }

    [Fact]
    public void DisconnectInfoRoundTripsForEveryReason()
    {
        foreach (DisconnectReason reason in Enum.GetValues<DisconnectReason>())
        {
            var info = new DisconnectInfo(reason, reason == DisconnectReason.None ? null : "because " + reason);
            byte[] bytes = Codec.Encode(info);
            Assert.Equal((byte)MessageId.Disconnect, bytes[0]);
            Assert.Equal((byte)reason, bytes[1]);
            Assert.True(Codec.TryDecode(bytes, out DisconnectInfo decoded, out ProtocolError error), error.ToString());
            Assert.Equal(info, decoded);
        }

        var longest = new DisconnectInfo(DisconnectReason.Kicked, new string('k', Limits.MaxDisconnectTextBytes));
        Assert.True(Codec.TryDecode(Codec.Encode(longest), out DisconnectInfo again, out _));
        Assert.Equal(longest, again);
        Assert.Throws<ProtocolException>(() => Codec.Encode(new DisconnectInfo(DisconnectReason.Kicked, new string('k', Limits.MaxDisconnectTextBytes + 1))));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new DisconnectInfo((DisconnectReason)18, null)));
        Assert.False(Codec.TryDecode(new byte[] { 8, 18, 0 }, out DisconnectInfo _, out ProtocolError e));
        Assert.Equal(ProtocolError.BadValue, e);
    }

    // ---- ids, framing, truncation ----

    [Fact]
    public void EveryMessageStartsWithItsIdAndAWrongOrUnknownIdIsNamed()
    {
        var all = new (MessageId id, byte[] bytes)[]
        {
            (MessageId.Hello, Codec.Encode(new Hello(ProtocolVersion.Current, "A", SessionToken.None))),
            (MessageId.Welcome, Codec.Encode(new Welcome(1, default, 1, 20, 1))),
            (MessageId.MapGrid, Codec.Encode(Msg.FlatMap(2, 2))),
            (MessageId.Snapshot, Codec.Encode(Msg.Snapshot(1, 0, 0, new string[0]))),
            (MessageId.Delta, Codec.Encode(Msg.Delta(1))),
            (MessageId.ServerText, Codec.Encode(new ServerText("a", "b"))),
            (MessageId.InputFrame, Codec.Encode(new InputFrame(1, HeldDirs.North))),
            (MessageId.Disconnect, Codec.Encode(new DisconnectInfo(DisconnectReason.Kicked, "x"))),
        };
        foreach (var (id, bytes) in all)
        {
            Assert.Equal((byte)id, bytes[0]);
            Assert.True(Codec.TryPeekId(bytes, out MessageId peeked));
            Assert.Equal(id, peeked);
        }

        Assert.False(Codec.TryPeekId(ReadOnlySpan<byte>.Empty, out MessageId none));
        Assert.Equal(MessageId.None, none);
        Assert.False(Codec.TryPeekId(new byte[] { 0 }, out _));
        Assert.True(Codec.TryPeekId(new byte[] { 9 }, out MessageId motion));
        Assert.Equal(MessageId.MotionInput, motion);
        Assert.True(Codec.TryPeekId(new byte[] { 10 }, out MessageId action));
        Assert.Equal(MessageId.Action, action);
        Assert.True(Codec.TryPeekId(new byte[] { 11 }, out MessageId hints));
        Assert.Equal(MessageId.ContextHints, hints);
        Assert.True(Codec.TryPeekId(new byte[] { 12 }, out MessageId report)); // version 5
        Assert.Equal(MessageId.DevGasReport, report);
        Assert.True(Codec.TryPeekId(new byte[] { 13 }, out MessageId airflow)); // version 6
        Assert.Equal(MessageId.Airflow, airflow);
        Assert.True(Codec.TryPeekId(new byte[] { 14 }, out MessageId sounds)); // version 13
        Assert.Equal(MessageId.Sounds, sounds);
        Assert.True(Codec.TryPeekId(new byte[] { 15 }, out MessageId radial)); // version 15
        Assert.Equal(MessageId.Radial, radial);
        Assert.True(Codec.TryPeekId(new byte[] { 16 }, out MessageId uiWindow)); // version 16
        Assert.Equal(MessageId.UiWindow, uiWindow);
        Assert.True(Codec.TryPeekId(new byte[] { 17 }, out MessageId tooltip)); // version 19
        Assert.Equal(MessageId.Tooltip, tooltip);
        Assert.True(Codec.TryPeekId(new byte[] { 18 }, out MessageId cameraShake)); // version 21
        Assert.Equal(MessageId.CameraShake, cameraShake);
        Assert.True(Codec.TryPeekId(new byte[] { 19 }, out MessageId sayModal)); // version 38
        Assert.Equal(MessageId.SayModal, sayModal);
        Assert.True(Codec.TryPeekId(new byte[] { 20 }, out MessageId runechat)); // version 38
        Assert.Equal(MessageId.Runechat, runechat);
        Assert.True(Codec.TryPeekId(new byte[] { 21 }, out MessageId radioChannels)); // version 40
        Assert.Equal(MessageId.RadioChannels, radioChannels);
        Assert.True(Codec.TryPeekId(new byte[] { 22 }, out MessageId motionMessage)); // version 42
        Assert.Equal(MessageId.Motion, motionMessage);
        Assert.True(Codec.TryPeekId(new byte[] { 23 }, out MessageId bugReport)); // version 53
        Assert.Equal(MessageId.BugReport, bugReport);
        Assert.True(Codec.TryPeekId(new byte[] { 24 }, out MessageId heldMotion)); // version 54
        Assert.Equal(MessageId.HeldMotion, heldMotion);
        Assert.True(Codec.TryPeekId(new byte[] { 25 }, out MessageId tts)); // version 63
        Assert.Equal(MessageId.Tts, tts);
        Assert.True(Codec.TryPeekId(new byte[] { 26 }, out MessageId minimap)); // version 67
        Assert.Equal(MessageId.Minimap, minimap);
        Assert.True(Codec.TryPeekId(new byte[] { 27 }, out MessageId minimapMobs)); // version 68
        Assert.Equal(MessageId.MinimapMobs, minimapMobs);
        Assert.True(Codec.TryPeekId(new byte[] { 28 }, out MessageId buildPreview)); // version 70
        Assert.Equal(MessageId.BuildPreview, buildPreview);
        Assert.True(Codec.TryPeekId(new byte[] { 29 }, out MessageId runtimeResource)); // version 79
        Assert.Equal(MessageId.RuntimeResource, runtimeResource);
        Assert.True(Codec.TryPeekId(new byte[] { 30 }, out MessageId weather)); // version 84
        Assert.Equal(MessageId.Weather, weather);
        Assert.True(Codec.TryPeekId(new byte[] { 31 }, out MessageId gunSight)); // version 92
        Assert.Equal(MessageId.GunSight, gunSight);
        Assert.False(Codec.TryPeekId(new byte[] { 32 }, out _));
        Assert.False(Codec.TryPeekId(new byte[] { 255 }, out _));

        // Each decoder refuses every other message as WrongMessage, an unknown id as UnknownMessage, and nothing as Truncated.
        foreach (var (id, bytes) in all)
        {
            Assert.False(id == MessageId.Hello ? !Codec.TryDecode(bytes, out Hello _, out ProtocolError _) : Codec.TryDecode(bytes, out Hello _, out ProtocolError _), "Hello vs " + id);
        }

        ProtocolError error;
        Assert.False(Codec.TryDecode(all[1].bytes, out Hello _, out error));
        Assert.Equal(ProtocolError.WrongMessage, error);
        Assert.False(Codec.TryDecode(all[0].bytes, out Welcome _, out error));
        Assert.Equal(ProtocolError.WrongMessage, error);
        Assert.False(Codec.TryDecode(all[0].bytes, out MapGrid _, out error));
        Assert.Equal(ProtocolError.WrongMessage, error);
        Assert.False(Codec.TryDecode(all[0].bytes, out Snapshot _, out error));
        Assert.Equal(ProtocolError.WrongMessage, error);
        Assert.False(Codec.TryDecode(all[0].bytes, out Delta _, out error));
        Assert.Equal(ProtocolError.WrongMessage, error);
        Assert.False(Codec.TryDecode(all[0].bytes, out ServerText _, out error));
        Assert.Equal(ProtocolError.WrongMessage, error);
        Assert.False(Codec.TryDecode(all[0].bytes, out InputFrame _, out error));
        Assert.Equal(ProtocolError.WrongMessage, error);
        Assert.False(Codec.TryDecode(all[0].bytes, out DisconnectInfo _, out error));
        Assert.Equal(ProtocolError.WrongMessage, error);
        Assert.False(Codec.TryDecode(new byte[] { 99, 0 }, out InputFrame _, out error));
        Assert.Equal(ProtocolError.UnknownMessage, error);
        Assert.False(Codec.TryDecode(new byte[] { 0, 0 }, out InputFrame _, out error));
        Assert.Equal(ProtocolError.UnknownMessage, error);
        Assert.False(Codec.TryDecode(ReadOnlySpan<byte>.Empty, out InputFrame _, out error));
        Assert.Equal(ProtocolError.Truncated, error);
    }

    [Fact]
    public void ABytePastTheEndOfAnyMessageIsRefused()
    {
        foreach (byte[] bytes in new[]
                 {
                     Codec.Encode(new Welcome(1, default, 1, 20, 1)),
                     Codec.Encode(Msg.FlatMap(2, 2)),
                     Codec.Encode(Msg.Snapshot(1, 0, 0, new[] { "/mob" }, Msg.State(1))),
                     Codec.Encode(SampleDelta()),
                     Codec.Encode(new ServerText("a", "b")),
                     Codec.Encode(new InputFrame(1, HeldDirs.North)),
                     Codec.Encode(new DisconnectInfo(DisconnectReason.Kicked, "x")),
                     Codec.Encode(new ActionMessage(4, ActionKind.Rest, 0, ClickButton.None, ClickModifiers.None, null)),
                     Codec.Encode(CodecV2Tests.Input(5, 2)),
                     Codec.Encode(new ContextHintsMessage { TargetKind = TargetKind.Entity, Target = 3, Name = "airlock", Hints = { new ContextHint(ClickButton.Left, ClickModifiers.None, "Open") } }),
                 })
        {
            byte[] longer = bytes.Append((byte)0).ToArray();
            Assert.False(TryDecodeAny(longer, out ProtocolError error), "trailing byte accepted");
            Assert.Equal(ProtocolError.TrailingBytes, error);
        }
    }

    [Fact]
    public void EveryTruncationOfEveryMessageIsRefusedNotThrown()
    {
        foreach (byte[] bytes in new[]
                 {
                     Codec.Encode(new Hello(ProtocolVersion.Current, "Alice", new SessionToken(1, 2))),
                     Codec.Encode(new Welcome(300, new SessionToken(1, 2), 70000, 20, 99)),
                     Codec.Encode(Msg.Map(3, 2, 1, new[] { Floor, "/turf/closed/wall" }, new TurfRun(2, 0), new TurfRun(4, 1))),
                     Codec.Encode(Msg.Snapshot(100, 3, 4, new[] { "/mob", "/obj" }, Msg.State(3, 0, 4, 4, 1, 2, "Alice", 10f, 5), Msg.State(9, 1, 2, 2, 1, 1, null, 0f, 0))),
                     Codec.Encode(SampleDelta()),
                     Codec.Encode(new ServerText("chat", "<b>hello</b>")),
                     Codec.Encode(new InputFrame(300, HeldDirs.North | HeldDirs.West)),
                     Codec.Encode(new DisconnectInfo(DisconnectReason.Flooding, "slow down")),
                     Codec.Encode(new ActionMessage(300, ActionKind.Click, 77, ClickButton.Right, ClickModifiers.Shift | ClickModifiers.Alt, null)),
                     Codec.Encode(new ActionMessage(300, ActionKind.DevCommand, 0, ClickButton.None, ClickModifiers.None, "damage brute 20 chest")),
                     Codec.Encode(CodecV2Tests.Input(300, 4)),
                     Codec.Encode(new ActionMessage(300, ActionKind.Click, 0, ClickButton.Left, ClickModifiers.Ctrl, null) { TargetKind = TargetKind.Turf, TurfX = 200, TurfY = 100, TurfZ = 3, IconX = 32, IconY = 1 }),
                     Codec.Encode(new ActionMessage(300, ActionKind.Hover, 90000, ClickButton.None, ClickModifiers.None, null) { TargetKind = TargetKind.Screen }),
                     Codec.Encode(new ActionMessage(300, ActionKind.Keybinding, 0, ClickButton.None, ClickModifiers.None, null) { Keybinding = KeybindingCode.HoldThrowMode, KeyUp = true }),
                     Codec.Encode(new ContextHintsMessage { TargetKind = TargetKind.Turf, TurfX = 9, TurfY = 8, TurfZ = 1, Name = null }),
                     Codec.Encode(new ContextHintsMessage { TargetKind = TargetKind.Screen, Target = 40, Name = "Hand", Hints = { new ContextHint(ClickButton.Right, ClickModifiers.Alt, "Weld shut"), new ContextHint(ClickButton.Left, ClickModifiers.Shift | ClickModifiers.Ctrl, "x") } }),
                 })
        {
            Assert.True(TryDecodeAny(bytes, out _));
            for (int length = 0; length < bytes.Length; length++)
            {
                // A Hello of another version is accepted early, so this loop only uses the current version's prefixes.
                bool ok = TryDecodeAny(bytes.AsSpan(0, length), out ProtocolError error);
                Assert.False(ok, "a " + length + "-byte prefix of a " + bytes.Length + "-byte message " + (MessageId)bytes[0] + " was accepted");
                Assert.NotEqual(ProtocolError.None, error);
            }
        }
    }

    /// <summary>Runs the decoder that matches the first byte.</summary>
    internal static bool TryDecodeAny(ReadOnlySpan<byte> message, out ProtocolError error)
    {
        MessageId id = message.Length == 0 ? MessageId.None : (MessageId)message[0];
        switch (id)
        {
            case MessageId.Hello: return Codec.TryDecode(message, out Hello _, out error);
            case MessageId.Welcome: return Codec.TryDecode(message, out Welcome _, out error);
            case MessageId.MapGrid: return Codec.TryDecode(message, out MapGrid _, out error);
            case MessageId.Snapshot: return Codec.TryDecode(message, out Snapshot _, out error);
            case MessageId.Delta: return Codec.TryDecode(message, out Delta _, out error);
            case MessageId.ServerText: return Codec.TryDecode(message, out ServerText _, out error);
            case MessageId.InputFrame: return Codec.TryDecode(message, out InputFrame _, out error);
            case MessageId.Disconnect: return Codec.TryDecode(message, out DisconnectInfo _, out error);
            case MessageId.MotionInput: return Codec.TryDecode(message, out MotionInputMessage _, out error);
            case MessageId.Action: return Codec.TryDecode(message, out ActionMessage _, out error);
            case MessageId.ContextHints: return Codec.TryDecode(message, out ContextHintsMessage _, out error);
            case MessageId.Sounds: return Codec.TryDecode(message, out Sounds _, out error);
            case MessageId.Radial: return Codec.TryDecode(message, out RadialMenuMessage _, out error);
            case MessageId.UiWindow: return Codec.TryDecode(message, out UiWindowMessage _, out error);
            case MessageId.Tooltip: return Codec.TryDecode(message, out TooltipMessage _, out error);
            case MessageId.CameraShake: return Codec.TryDecode(message, out CameraShakeMessage _, out error);
            case MessageId.RadioChannels: return Codec.TryDecode(message, out RadioChannelsMessage _, out error);
            case MessageId.Motion: return Codec.TryDecode(message, out MotionMessage _, out error);
            default: return Codec.TryDecode(message, out InputFrame _, out error);
        }
    }

    [Fact]
    public void EncodersReportASmallDestinationAndTheSizeBoundsHold()
    {
        Delta d = SampleDelta();
        byte[] whole = Codec.Encode(d);
        Assert.True(Codec.MaxDeltaBodySize(d) + Codec.MaxDeltaHeaderBytes >= whole.Length);
        for (int size = 0; size < whole.Length; size++)
        {
            int s = size;
            ProtocolException e = Assert.Throws<ProtocolException>(() => Codec.Encode(d, new byte[s]));
            Assert.Equal(ProtocolError.BufferTooSmall, e.Error);
        }

        Assert.Equal(whole.Length, Codec.Encode(d, new byte[whole.Length]));
        MapGrid grid = Msg.FlatMap(10, 10);
        Assert.True(Codec.MaxEncodedSize(grid) >= Codec.Encode(grid).Length);
        Snapshot snap = Msg.Snapshot(1, 1, 1, new[] { "/mob" }, Msg.State(1, name: "xyz"), Msg.State(2, name: new string('\u20AC', 100)));
        Assert.True(Codec.MaxEncodedSize(snap) >= Codec.Encode(snap).Length);
        Assert.Throws<ProtocolException>(() => Codec.Encode(new Hello(ProtocolVersion.Current, "A", SessionToken.None), new byte[3]));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new Welcome(1, default, 1, 20, 1), new byte[3]));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new ServerText("a", "b"), new byte[3]));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new DisconnectInfo(DisconnectReason.Kicked, "x"), new byte[2]));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new InputFrame(1, HeldDirs.None), new byte[2]));
    }

    [Fact]
    public void EncodeDeltaNeedsItsHeaderRoom()
    {
        byte[] body = Codec.EncodeDeltaBody(SampleDelta());
        ProtocolException e = Assert.Throws<ProtocolException>(() => Codec.EncodeDelta(1, 0, 0, body, new byte[body.Length]));
        Assert.Equal(ProtocolError.BufferTooSmall, e.Error);
        Assert.Throws<ProtocolException>(() => Codec.EncodeDelta(-1, 0, 0, body, new byte[body.Length + Codec.MaxDeltaHeaderBytes]));
        Assert.Throws<ProtocolException>(() => Codec.EncodeDelta(1, 0, -1, body, new byte[body.Length + Codec.MaxDeltaHeaderBytes]));
    }
}
