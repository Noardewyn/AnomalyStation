using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 100 also (ui-overhaul.md section 10, slice 3A, DV693; MessagesV100Hotbar.cs, Codec.V100Hotbar.cs): the Hotbar message, the own human's ten
/// cells and the held items' names, and the HotbarPress action, a cell of 1 to 10 and nothing else.
/// </summary>
public class ProtocolV100HotbarTests
{
    private static HotbarMessage Sample(byte refused = 0)
    {
        var cells = new HotbarCell[HotbarMessage.CellCount];
        for (int i = 0; i < cells.Length; i++)
            cells[i] = HotbarCell.Empty;
        // slice 4B-2: each held or out cell names its item (two screwdrivers of one picture in cells 1 and 7 are told apart by it)
        cells[0] = new HotbarCell(HotbarCellState.Holding, new TableRef(5, true), 501);
        cells[2] = new HotbarCell(HotbarCellState.Out, new TableRef(300, true), 200000);
        cells[6] = new HotbarCell(HotbarCellState.Holding, new TableRef(5, true), 502);
        cells[7] = new HotbarCell(HotbarCellState.Holding, TableRef.None, 7); // an item that shows nothing
        cells[9] = new HotbarCell(HotbarCellState.Out, TableRef.Public(1), 1);
        return new HotbarMessage { Bar = true, BeltWorn = true, Cells = cells, LeftHand = "wrench", RightHand = null, RefusedCell = refused };
    }

    private static HotbarMessage RoundTrip(HotbarMessage message)
    {
        byte[] bytes = Codec.Encode(message);
        Assert.True(bytes.Length <= Limits.MaxHotbarBytes);
        Assert.True(Codec.TryDecode(bytes, out HotbarMessage read, out ProtocolError error), error.ToString());
        Assert.Equal(bytes, Codec.Encode(read));
        return read;
    }

    [Fact]
    public void TheCellsTheBeltTheNamesAndARefusalRoundTrip()
    {
        HotbarMessage sample = Sample(refused: 4);
        HotbarMessage read = RoundTrip(sample);
        Assert.Equal(sample, read);
        Assert.Equal(HotbarCellState.Out, read.Cells[2].State);
        Assert.Equal(new TableRef(300, true), read.Cells[2].Appearance);
        Assert.Equal(new long[] { 501, 0, 200000, 0, 0, 0, 502, 7, 0, 1 }, read.Cells.Select(c => c.Entity));
        Assert.Equal(read.Cells[0].Appearance, read.Cells[6].Appearance); // one picture, two items
        // a cell's bytes: the state, the picture, the item: cell 1 holding private picture 5 (0B), item 501 (F5 03)
        Assert.Equal(new byte[] { 34, 3, 1, 0x0B, 0xF5, 0x03, 0 }, Codec.Encode(sample)[..7]);
        Assert.NotEqual(Sample(), new HotbarMessage { Bar = true, BeltWorn = true, Cells = Sample().Cells.Select((c, i) => i == 6 ? c with { Entity = 501 } : c).ToArray(), LeftHand = "wrench" });
        Assert.Equal("wrench", read.LeftHand);
        Assert.Null(read.RightHand);
        Assert.Equal(4, read.RefusedCell);
        Assert.Equal(Sample(), read.WithoutRefusal());
        Assert.NotEqual(Sample(), read);
        Assert.Equal(HotbarMessage.None, RoundTrip(HotbarMessage.None));
        Assert.Equal(new byte[] { 34, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, Codec.Encode(HotbarMessage.None)); // protocol.md section 99
        Assert.Equal(MessageId.Hotbar, (MessageId)Codec.Encode(sample)[0]);
    }

    [Fact]
    public void EveryReplicaTakesTheHotbarAndCountsEachRefusalOnce()
    {
        // the State channel's readers (the game client, the fake feed, the test rigs and the bots) apply it through the replica
        var replica = new ReplicaWorld();
        Assert.True(replica.TryApply(Codec.Encode(HotbarMessage.None), out MessageId id, out ProtocolError error), error.ToString());
        Assert.Equal(MessageId.Hotbar, id);
        Assert.True(replica.TryApply(Codec.Encode(Sample(refused: 6)), out _, out error), error.ToString());
        Assert.Equal(Sample(), replica.Hotbar); // the state, without the refusal
        Assert.Equal((1, 6), (replica.HotbarRefusals, replica.HotbarRefusedCell));
        Assert.True(replica.TryApply(Codec.Encode(Sample()), out _, out _));
        Assert.Equal(1, replica.HotbarRefusals); // a state message is no refusal
        byte[] bad = Codec.Encode(Sample());
        bad[1] = 4;
        Assert.False(replica.TryApply(bad, out _, out error));
        Assert.Equal(ProtocolError.BadValue, error);
        // the longest names fit the cap
        string longest = new('x', Limits.MaxHotbarNameBytes);
        var cells = Sample().Cells.Select(c => new HotbarCell(HotbarCellState.Holding, new TableRef((uint)Limits.MaxAppearanceTableEntries, true), long.MaxValue)).ToArray();
        HotbarMessage widest = new() { Bar = true, BeltWorn = true, Cells = cells, LeftHand = longest, RightHand = longest, RefusedCell = 10 };
        Assert.Equal(widest, RoundTrip(widest));
    }

    [Fact]
    public void ABadHotbarIsRefusedAtBothEnds()
    {
        HotbarMessage good = Sample();
        HotbarCell[] Cells(int index, HotbarCell cell)
        {
            HotbarCell[] copy = good.Cells.ToArray();
            copy[index] = cell;
            return copy;
        }
        // the encoder
        Assert.Throws<ProtocolException>(() => Codec.Encode(new HotbarMessage { Bar = false, BeltWorn = true, Cells = Cells(0, HotbarCell.Empty) }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new HotbarMessage { Bar = false, Cells = good.Cells })); // cells without the bar
        Assert.Throws<ProtocolException>(() => Codec.Encode(new HotbarMessage { Bar = true, BeltWorn = false, Cells = good.Cells })); // belt cells without a belt
        Assert.Throws<ProtocolException>(() => Codec.Encode(new HotbarMessage { Bar = true, BeltWorn = true, Cells = Cells(1, new HotbarCell(HotbarCellState.Empty, new TableRef(3, true))) }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new HotbarMessage { Bar = true, BeltWorn = true, Cells = Cells(1, new HotbarCell((HotbarCellState)3, TableRef.None)) }));
        // slice 4B-2: a held or out cell names its item (1 or more); an empty cell none
        Assert.Throws<ProtocolException>(() => Codec.Encode(new HotbarMessage { Bar = true, BeltWorn = true, Cells = Cells(1, new HotbarCell(HotbarCellState.Holding, new TableRef(3, true))) }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new HotbarMessage { Bar = true, BeltWorn = true, Cells = Cells(1, new HotbarCell(HotbarCellState.Out, new TableRef(3, true), -4)) }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new HotbarMessage { Bar = true, BeltWorn = true, Cells = Cells(1, new HotbarCell(HotbarCellState.Empty, TableRef.None, 9)) }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new HotbarMessage { Bar = true, Cells = Cells(0, HotbarCell.Empty).Select((c, i) => i < 7 ? HotbarCell.Empty : c).ToArray(), RefusedCell = 11 }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new HotbarMessage { Bar = true, BeltWorn = true, Cells = good.Cells, LeftHand = "" }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new HotbarMessage { Bar = true, BeltWorn = true, Cells = good.Cells, LeftHand = new string('x', Limits.MaxHotbarNameBytes + 1) }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new HotbarMessage { RefusedCell = 3 })); // a refusal without the bar
        Assert.Throws<ArgumentException>(() => new HotbarMessage { Cells = new HotbarCell[9] });

        // the decoder: [id] [flags] then cell 1's state at byte 2
        byte[] bytes = Codec.Encode(good);
        Assert.True(Codec.TryDecode(bytes, out HotbarMessage _, out _));
        byte[] bad = (byte[])bytes.Clone();
        bad[1] = 4; // an unknown flag bit
        Assert.False(Codec.TryDecode(bad, out HotbarMessage _, out _));
        bad = (byte[])bytes.Clone();
        bad[1] = 1; // the bar without a belt, yet cell 1 holds an item
        Assert.False(Codec.TryDecode(bad, out HotbarMessage _, out _));
        bad = (byte[])bytes.Clone();
        bad[1] = 2; // a belt without the bar
        Assert.False(Codec.TryDecode(bad, out HotbarMessage _, out _));
        bad = (byte[])bytes.Clone();
        bad[2] = 3; // an unknown cell state
        Assert.False(Codec.TryDecode(bad, out HotbarMessage _, out _));
        // slice 4B-2: cell 1's item of 0 (its two bytes F5 03 made one 00): refused
        bad = bytes[..4].Concat(new byte[] { 0 }).Concat(bytes[6..]).ToArray();
        Assert.False(Codec.TryDecode(bad, out HotbarMessage _, out ProtocolError zero));
        Assert.Equal(ProtocolError.BadValue, zero);
        bad = (byte[])bytes.Clone();
        bad[^1] = 11; // a refused cell out of range
        Assert.False(Codec.TryDecode(bad, out HotbarMessage _, out ProtocolError error));
        Assert.Equal(ProtocolError.BadValue, error);
        bad[^1] = 255;
        Assert.False(Codec.TryDecode(bad, out HotbarMessage _, out _));
        Assert.False(Codec.TryDecode(bytes[..^1], out HotbarMessage _, out error)); // truncated
        Assert.Equal(ProtocolError.Truncated, error);
        Assert.False(Codec.TryDecode(bytes.Append((byte)0).ToArray(), out HotbarMessage _, out error));
        Assert.Equal(ProtocolError.TrailingBytes, error);
        for (int length = 0; length < bytes.Length; length++)
            Assert.False(Codec.TryDecode(bytes.AsSpan(0, length), out HotbarMessage _, out _), "cut at " + length);
    }

    [Fact]
    public void AHotbarPressCarriesItsCellAndNothingElse()
    {
        Assert.True(ActionKinds.Last >= ActionKind.HotbarPress); // version 100 also adds InventoryMove and InventoryOpen after it
        for (int cell = 1; cell <= HotbarMessage.CellCount; cell++)
        {
            var press = new ActionMessage(7, ActionKind.HotbarPress, 0, ClickButton.None, ClickModifiers.None, null) { HotbarCell = cell };
            byte[] encoded = Codec.Encode(press);
            Assert.True(Codec.TryDecode(encoded, out ActionMessage back, out ProtocolError error), error.ToString());
            Assert.Equal(press, back);
            Assert.Equal(cell, encoded[^1]);
        }
        var good = new ActionMessage(7, ActionKind.HotbarPress, 0, ClickButton.None, ClickModifiers.None, null) { HotbarCell = 3 };
        // the encoder refuses a cell of 0, 11 and 255, any other field, and a cell on another kind
        foreach (int cell in new[] { 0, 11, 255, -1 })
            Assert.Throws<ProtocolException>(() => Codec.Encode(good with { HotbarCell = cell }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(good with { Target = 5, TargetKind = TargetKind.Entity }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(good with { Text = "x" }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(good with { Button = ClickButton.Left }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new ActionMessage(7, ActionKind.Rest, 0, ClickButton.None, ClickModifiers.None, null) { HotbarCell = 3 }));
        // the decoder refuses a cell of 0, 11 and 255, a truncated press and a trailing byte
        byte[] bytes = Codec.Encode(good);
        foreach (byte cell in new byte[] { 0, 11, 255 })
        {
            byte[] bad = (byte[])bytes.Clone();
            bad[^1] = cell;
            Assert.False(Codec.TryDecode(bad, out ActionMessage _, out ProtocolError error), "cell " + cell);
            Assert.Equal(ProtocolError.BadValue, error);
        }
        Assert.False(Codec.TryDecode(bytes[..^1], out ActionMessage _, out ProtocolError cut));
        Assert.Equal(ProtocolError.Truncated, cut);
        Assert.False(Codec.TryDecode(bytes.Append((byte)1).ToArray(), out ActionMessage _, out ProtocolError trailing));
        Assert.Equal(ProtocolError.TrailingBytes, trailing);
    }
}
