using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 3 (docs/ai/specs/protocol.md section 13): the cell flags, base lights, turf types, starlight and the entity light, at the caps (D40).</summary>
public class CodecV3Tests
{
    private static LightState Lamp => new(8f, 1.5f, 0xFFAA00FFu, true, 90f, 4, 1f, 0.5f, -0.5f, 1);

    private static MapGrid Grid(int w, int h)
    {
        MapGrid g = Msg.FlatMap(w, h);
        g.FlagRuns.Add(new TurfRun(w * h - 3, 0));
        g.FlagRuns.Add(new TurfRun(1, CellFlags.FaceNorth | CellFlags.FaceWest));
        g.FlagRuns.Add(new TurfRun(1, CellFlags.Opaque));
        g.FlagRuns.Add(new TurfRun(1, CellFlags.Starlight));
        g.BaseLights.Add(new BaseLight(0xFF800080u));
        g.BaseLights.Add(new BaseLight(0xFFFFFFFFu));
        g.BaseLightRuns.Add(new TurfRun(w, 1));
        g.BaseLightRuns.Add(new TurfRun(w * h - w, 2));
        g.Starlight = new LightState(2f, 1f, 0x8589FAFFu, true, 360f, 0, -0.5f, 0f, 0f, 1);
        return g;
    }

    [Fact]
    public void AMapGridCarriesFlagsBaseLightsAndStarlightAndExpandsThemLosslessly()
    {
        MapGrid grid = Grid(20, 10);
        byte[] bytes = Codec.Encode(grid);
        Assert.True(Codec.TryDecode(bytes, out MapGrid back, out ProtocolError error), error.ToString());
        Msg.AssertEqual(grid, back);
        byte[] flags = back.ExpandFlags();
        Assert.Equal(CellFlags.FaceNorth | CellFlags.FaceWest, flags[197]);
        Assert.Equal(CellFlags.Opaque, flags[198]);
        Assert.Equal(CellFlags.Starlight, flags[199]);
        Assert.Equal(1, back.ExpandBaseLights()[19]);
        Assert.Equal(2, back.ExpandBaseLights()[20]);
        Assert.Equal(bytes, Codec.Encode(back));
    }

    [Fact]
    public void ARunOfAMapWithoutFlagsCostsAFewBytes()
    {
        MapGrid plain = Msg.FlatMap(255, 255);
        int without = Codec.Encode(plain).Length;
        MapGrid withV3 = Msg.FlatMap(255, 255);
        withV3.FlagRuns.Add(new TurfRun(255 * 255, 0));
        withV3.BaseLightRuns.Add(new TurfRun(255 * 255, 0));
        Assert.True(Codec.Encode(withV3).Length - without < 16);
    }

    [Fact]
    public void AMapGridRefusesWhatTheDocumentationForbids()
    {
        MapGrid g = Grid(4, 4);
        g.FlagRuns[^1] = new TurfRun(1, 256); // past the flags byte (version 73's Flaps took its last bit, 128)
        Assert.Throws<ProtocolException>(() => Codec.Encode(g));
        g = Grid(4, 4);
        g.FlagRuns.RemoveAt(g.FlagRuns.Count - 1); // the runs must cover every cell
        Assert.Throws<ProtocolException>(() => Codec.Encode(g));
        g = Grid(4, 4);
        g.BaseLightRuns[0] = new TurfRun(4, 3); // an index past the table
        Assert.Throws<ProtocolException>(() => Codec.Encode(g));
        g = Grid(4, 4);
        for (int i = 0; i <= Limits.MaxBaseLightEntries; i++) g.BaseLights.Add(new BaseLight((uint)i));
        Assert.Throws<ProtocolException>(() => Codec.Encode(g));
        g = Grid(4, 4);
        g.Starlight = new LightState(Limits.MaxLightRange + 1, 1, 0, true, 360, 0, 1, 0, 0, 1);
        Assert.Throws<ProtocolException>(() => Codec.Encode(g));
        g = Grid(4, 4);
        g.Starlight = new LightState(2, 1, 0, true, 360, 3, 1, 0, 0, 1); // dir 3 is no BYOND direction
        Assert.Throws<ProtocolException>(() => Codec.Encode(g));
        g = Grid(4, 4);
        g.Starlight = new LightState(2, 1, 0, true, 360, 0, 1, 0, 0, 5); // light_system 5
        Assert.Throws<ProtocolException>(() => Codec.Encode(g));
    }

    [Fact]
    public void ADeltaCarriesTheCellRecordsAndStarlightInTheOrderOfTheirLists()
    {
        Delta d = Msg.Delta(7);
        d.CellFlagsChanged.Add(new CellFlagsChange(3, 4, 1, CellFlags.FaceSouth | CellFlags.Opaque));
        d.CellFlagsChanged.Add(new CellFlagsChange(255, 255, 16, CellFlags.All));
        d.FirstNewBaseLight = 3;
        d.NewBaseLights.Add(new BaseLight(0x11223344));
        d.BaseLightChanged.Add(new CellBaseLightChange(1, 1, 1, 3));
        d.FirstNewTurfType = 9;
        d.NewTurfTypes.Add("/turf/open/floor/plating");
        d.TurfTypeChanged.Add(new CellTurfTypeChange(2, 2, 1, 9));
        d.StarlightChanged = LightState.None; // the starlight went out
        byte[] bytes = Codec.Encode(d);
        Assert.True(Codec.TryDecode(bytes, out Delta back, out ProtocolError error), error.ToString());
        Msg.AssertEqual(d, back);
        Assert.Equal(bytes, Codec.Encode(back));

        // The body alone (what the broadcast encodes once) carries them too.
        byte[] body = Codec.EncodeDeltaBody(d);
        byte[] whole = new byte[body.Length + Codec.MaxDeltaHeaderBytes];
        int n = Codec.EncodeDelta(7, 0, 0, body, whole);
        Assert.True(Codec.TryDecode(whole.AsSpan(0, n), out Delta viaBody, out error), error.ToString());
        Msg.AssertBodyEqual(d, viaBody);

        // An empty version-3 part is not written: a Delta with no cell records is the version-2 bytes plus nothing.
        Delta quiet = Msg.Delta(8);
        Assert.Equal(Codec.Encode(quiet).Length, Codec.Encode(Msg.Delta(8)).Length);
    }

    [Fact]
    public void ALastListOfFlagChangesInLowCellsDecodesWithNothingAfterIt()
    {
        // The owner's play test (2026-10-16): a ghost's Delta (an empty private block: no motion) whose last part is the cell
        // flag changes the singularity made near the map's corner. Each is four bytes (three one-byte coordinates and the flags);
        // the reader once asked five of each and refused the message as too short.
        Delta d = Msg.Delta(961);
        for (int i = 0; i < 11; i++)
            d.CellFlagsChanged.Add(new CellFlagsChange(38 + i, 120, 1, 0));
        byte[] bytes = Codec.Encode(d);
        Assert.True(Codec.TryDecode(bytes, out Delta back, out ProtocolError error), error.ToString());
        Msg.AssertEqual(d, back);
    }

    [Fact]
    public void ADeltaRefusesWhatTheDocumentationForbids()
    {
        Delta Base() => Msg.Delta(1);
        Delta d = Base();
        // every bit of a cell's flags byte is defined from version 73 (128: Flaps), so no flags change is refused
        d.NewBaseLights.Add(new BaseLight(1)); // FirstNewBaseLight 0: indices start at 1
        Assert.Throws<ProtocolException>(() => Codec.Encode(d));
        d = Base();
        d.BaseLightChanged.Add(new CellBaseLightChange(1, 1, 1, (ushort)(Limits.MaxBaseLightEntries + 1)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(d));
        d = Base();
        d.TurfTypeChanged.Add(new CellTurfTypeChange(1, 1, 1, -1));
        Assert.Throws<ProtocolException>(() => Codec.Encode(d));
        d = Base();
        d.NewTurfTypes.Add(null);
        Assert.Throws<ProtocolException>(() => Codec.Encode(d));
        d = Base();
        d.FirstNewTurfType = Limits.MaxTurfTypeEntries;
        d.NewTurfTypes.Add("/turf");
        Assert.Throws<ProtocolException>(() => Codec.Encode(d));
        d = Base();
        d.CellFlagsChanged.Add(new CellFlagsChange(0, 1, 1, 0)); // outside the map's range
        Assert.Throws<ProtocolException>(() => Codec.Encode(d));
        d = Base();
        for (int i = 0; i < Limits.MaxCellChangesPerDelta; i++) d.CellFlagsChanged.Add(new CellFlagsChange(1 + i % 255, 1 + i / 255 % 255, 1, 1));
        d.TurfTypeChanged.Add(new CellTurfTypeChange(1, 1, 1, 0)); // one more than the cap, all lists together
        Assert.Throws<ProtocolException>(() => Codec.Encode(d));
        d = Base();
        d.SolidChanged.Add(new CellSolidChange(1, 1, 1, true));
        for (int i = 0; i < Limits.MaxCellChangesPerDelta; i++) d.BaseLightChanged.Add(new CellBaseLightChange(1 + i % 255, 1 + i / 255 % 255, 1, 0));
        Assert.Throws<ProtocolException>(() => Codec.Encode(d));
    }

    [Fact]
    public void AnEntityLightTravelsInTheSpawnRecordAndInTheChangeMask()
    {
        EntityState lit = Msg.State(5, 0, 3, 3) with { Light = Lamp };
        Snapshot snapshot = Msg.Snapshot(10, 5, 0, new[] { "/obj" }, lit, Msg.State(6, 0, 4, 4));
        byte[] bytes = Codec.Encode(snapshot);
        Assert.True(Codec.TryDecode(bytes, out Snapshot back, out ProtocolError error), error.ToString());
        Assert.Equal(Lamp, back.Entities[0].Light);
        Assert.True(back.Entities[1].Light.IsNone);

        Delta d = Msg.Delta(11);
        d.Changed.Add(new EntityChange { Id = 5, Mask = NetField.Light, Light = LightState.None });
        d.Changed.Add(new EntityChange { Id = 6, Mask = NetField.Light | NetField.X, X = 5, Light = Lamp });
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta delta, out error), error.ToString());
        Assert.Equal(NetField.Light, delta.Changed[0].Mask);
        Assert.True(delta.Changed[0].Light.IsNone);
        Assert.Equal(Lamp, delta.Changed[1].Light);
        Assert.Equal(5, delta.Changed[1].X);
        // The mask now has Light as its twelfth wire bit; an unknown bit above it is refused.
        Assert.Equal(NetField.Light, NetField.Changeable & NetField.Light);
    }

    [Fact]
    public void AnEntityLightRefusesWhatTheDocumentationForbids()
    {
        foreach (LightState bad in new[]
                 {
                     Lamp with { Range = float.NaN }, Lamp with { Range = Limits.MaxLightRange * 2 }, Lamp with { Range = -1f },
                     Lamp with { Power = float.PositiveInfinity }, Lamp with { Power = Limits.MaxLightPower * 2 }, Lamp with { Angle = 361f },
                     Lamp with { Angle = -1f }, Lamp with { Dir = 3 }, Lamp with { Height = Limits.MaxLightHeight * 2 },
                     Lamp with { OffsetX = Limits.MaxLightOffset * 2 }, Lamp with { OffsetY = float.NaN }, Lamp with { System = 0 }, Lamp with { System = 5 },
                 })
        {
            Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Snapshot(1, 0, 0, new[] { "/obj" }, Msg.State(1, 0) with { Light = bad })));
            Delta d = Msg.Delta(1);
            d.Changed.Add(new EntityChange { Id = 1, Mask = NetField.Light, Light = bad });
            Assert.Throws<ProtocolException>(() => Codec.Encode(d));
        }

        // Crafted: a light flag with range 0 (a spawn record that says "light" and has none) and negative zero.
        byte[] Crafted(Action<Msg.RawWriter> light) => Msg.Raw(MessageId.Snapshot, r =>
        {
            r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(1).String("/obj").VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(1);
            r.VarInt(1).VarUInt(0).VarUInt(1).VarUInt(1).VarUInt(1).Byte(0).String("").Bytes(0, 0, 0, 0).VarUInt(0).Byte(4).VarUInt(0);
            light(r);
            r.Byte(0).Byte(0).Byte(0).Byte(0);
        });
        Assert.False(Codec.TryDecode(Crafted(r => r.Bytes(0, 0, 0, 0)), out Snapshot _, out _));
        Assert.False(Codec.TryDecode(Crafted(r => r.Bytes(0, 0, 0, 0x80)), out Snapshot _, out _));
    }

    [Fact]
    public void ADeltaAtTheCellCapRoundTripsAndStaysUnderTheMessageCap()
    {
        Delta d = Msg.Delta(1);
        for (int i = 0; i < Limits.MaxCellChangesPerDelta; i++)
            d.CellFlagsChanged.Add(new CellFlagsChange(1 + i % 255, 1 + i / 255 % 255, 1 + i / (255 * 255), (byte)(i & 31)));
        byte[] bytes = Codec.Encode(d);
        Assert.True(bytes.Length < Limits.MaxOutboundMessageBytes);
        Assert.True(Codec.TryDecode(bytes, out Delta back, out ProtocolError error), error.ToString());
        Assert.Equal(d.CellFlagsChanged, back.CellFlagsChanged);
    }
}
