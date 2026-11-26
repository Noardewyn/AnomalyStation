using Anomaly.Protocol;

namespace Anomaly.Server.Tests.M4b;

/// <summary>
/// Protocol version 5 (M4b slice 3, docs/ai/specs/protocol.md section 15): the gas a cell shows in MapGrid and the Delta, the
/// DevGasProbe action and the DevGasReport message: round trips, the layouts, the caps, and the replica's gas state.
/// </summary>
public class ProtocolV5Tests
{
    private static MapGrid Grid(int width = 48, int height = 48)
    {
        var grid = new MapGrid { Tick = 5, Width = width, Height = height, Levels = 1 };
        grid.TurfTypes.Add("/turf/open/floor");
        grid.Runs.Add(new TurfRun(grid.CellCount, 0));
        grid.GasKinds.AddRange(new[] { "plasma", "water_vapor", "freon" });
        return grid;
    }

    [Fact]
    public void TheVersionIsAtLeastFive() => Assert.True(ProtocolVersion.Current >= 5); // D141: version 6

    [Fact]
    public void AMapGridWithEveryCellShowingGasRoundTrips()
    {
        // every cell of a 48 x 48 map shows one or two gases: the decoder's per-cell minimum (a cell, a count, one gas) must
        // let the smallest cells through (a join into a room full of plasma)
        MapGrid grid = Grid();
        for (int y = 1; y <= 48; y++)
            for (int x = 1; x <= 48; x++)
                grid.GasCells.Add(new CellGasChange(x, y, 1, (x + y) % 3 == 0
                    ? new[] { new GasLevel(1, 20), new GasLevel(2, 80) }
                    : new[] { new GasLevel(1, (byte)(1 + (x * y) % 80)) }));
        byte[] bytes = Codec.Encode(grid);
        Assert.True(Codec.TryDecode(bytes, out MapGrid decoded, out ProtocolError error), error.ToString());
        Assert.Equal(grid.GasKinds, decoded.GasKinds);
        Assert.Equal(grid.GasCells.Count, decoded.GasCells.Count);
        for (int i = 0; i < grid.GasCells.Count; i++)
        {
            Assert.Equal((grid.GasCells[i].X, grid.GasCells[i].Y, grid.GasCells[i].Z), (decoded.GasCells[i].X, decoded.GasCells[i].Y, decoded.GasCells[i].Z));
            Assert.Equal(grid.GasCells[i].Gases, decoded.GasCells[i].Gases);
        }
        Assert.Equal(bytes, Codec.Encode(decoded));
    }

    [Fact]
    public void AMapGridRefusesAGasOutsideItsKindsOrLevels()
    {
        MapGrid grid = Grid(4, 4);
        grid.GasCells.Add(new CellGasChange(2, 2, 1, new[] { new GasLevel(4, 10) }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(grid));
        grid.GasCells[0] = new CellGasChange(2, 2, 1, new[] { new GasLevel(1, 81) });
        Assert.Throws<ProtocolException>(() => Codec.Encode(grid));
        grid.GasCells[0] = new CellGasChange(2, 2, 1, Array.Empty<GasLevel>());
        Assert.Throws<ProtocolException>(() => Codec.Encode(grid)); // a join lists only the cells that show gas

        grid.GasCells[0] = new CellGasChange(2, 2, 1, new[] { new GasLevel(1, 80) });
        byte[] bytes = Codec.Encode(grid);
        Assert.Equal(80, bytes[^8]);
        bytes[^8] = 81; // the level byte is last before version 44's empty ceiling list, version 60's and version 76's empty level lists, version 87's look flag, version 91's two-byte wall height and version 96's empty turf light list
        Assert.False(Codec.TryDecode(bytes, out MapGrid _, out ProtocolError error));
        Assert.Equal(ProtocolError.BadValue, error);
    }

    [Fact]
    public void ADeltaCarriesGasChangesAndAClearedCell()
    {
        var delta = new Delta { Tick = 9 };
        delta.CellGasChanged.Add(new CellGasChange(3, 4, 1, new[] { new GasLevel(1, 7) }));
        delta.CellGasChanged.Add(new CellGasChange(3, 5, 1, Array.Empty<GasLevel>()));
        byte[] bytes = Codec.Encode(delta);
        Assert.True(Codec.TryDecode(bytes, out Delta decoded, out ProtocolError error), error.ToString());
        Assert.Equal(2, decoded.CellGasChanged.Count);
        Assert.Equal(new[] { new GasLevel(1, 7) }, decoded.CellGasChanged[0].Gases);
        Assert.Empty(decoded.CellGasChanged[1].Gases);
        Assert.Equal(bytes, Codec.Encode(decoded));
    }

    [Fact]
    public void TheReplicaKeepsTheGasOfEachCellAndSaysWhichChanged()
    {
        var replica = new ReplicaWorld();
        MapGrid grid = Grid(8, 8);
        grid.GasCells.Add(new CellGasChange(2, 3, 1, new[] { new GasLevel(1, 20) }));
        replica.Apply(grid);
        Assert.Equal(new[] { "plasma", "water_vapor", "freon" }, replica.GasKinds);
        Assert.Equal(new[] { new GasLevel(1, 20) }, replica.GasAt(2, 3, 1));
        Assert.Empty(replica.GasAt(3, 3, 1));
        Assert.Empty(replica.GasAt(99, 3, 1));

        var changed = new List<(int X, int Y, int Z)>();
        replica.GasChanged += cells => changed.AddRange(cells);
        replica.Apply(new Snapshot { Tick = 5 });
        var delta = new Delta { Tick = 6 };
        delta.CellGasChanged.Add(new CellGasChange(2, 3, 1, Array.Empty<GasLevel>()));
        delta.CellGasChanged.Add(new CellGasChange(5, 5, 1, new[] { new GasLevel(2, 3), new GasLevel(1, 80) }));
        replica.Apply(delta);
        Assert.Equal(new[] { (2, 3, 1), (5, 5, 1) }, changed);
        Assert.Empty(replica.GasAt(2, 3, 1));
        Assert.Equal(new[] { new GasLevel(2, 3), new GasLevel(1, 80) }, replica.GasAt(5, 5, 1));
        Assert.Single(replica.GasCells);

        var bad = new Delta { Tick = 7 };
        bad.CellGasChanged.Add(new CellGasChange(5, 5, 1, new[] { new GasLevel(9, 3) })); // a kind the table does not have
        Assert.Throws<ProtocolException>(() => replica.Apply(bad));
    }

    [Fact]
    public void ADevGasProbeIsATurfActionWithItsLayout()
    {
        var probe = new ActionMessage(7, ActionKind.DevGasProbe, 0, ClickButton.None, ClickModifiers.None, null) { TargetKind = TargetKind.Turf, TurfX = 12, TurfY = 200, TurfZ = 1 };
        byte[] bytes = Codec.Encode(probe);
        //                     id frame kind turf x   y    z
        Assert.Equal(new byte[] { 10, 7, 8, 2, 12, 200, 1 }, bytes);
        Assert.True(Codec.TryDecode(bytes, out ActionMessage decoded, out ProtocolError error), error.ToString());
        Assert.Equal(probe, decoded);
        Assert.Throws<ProtocolException>(() => Codec.Encode(probe with { TargetKind = TargetKind.Entity, Target = 5, TurfX = 0, TurfY = 0, TurfZ = 0 }));
        Assert.False(Codec.TryDecode(new byte[] { 10, 7, 8, 1, 5 }, out ActionMessage _, out error)); // an entity probe
        Assert.False(Codec.TryDecode(new byte[] { 10, 7, 10 }, out ActionMessage _, out _)); // no kind 10 (version 14: 9 is Unhover)
    }

    private static DevGasReport Report()
    {
        var report = new DevGasReport { X0 = 3, Y0 = 4, Z = 1, Width = 3, Height = 2 };
        report.Gases.Add(new DevGasInfo("o2", "Oxygen", 0x0000FF));
        report.Gases.Add(new DevGasInfo("plasma", "Plasma", 0xFFC0CB));
        report.Cells.Add(new DevGasCell(DevCellKind.Open, 101.325f, 293.15f, new[] { new DevGasAmount(0, 21.8f), new DevGasAmount(1, 5f) }));
        report.Cells.Add(new DevGasCell(DevCellKind.Closed, 0, 0, Array.Empty<DevGasAmount>()));
        report.Cells.Add(new DevGasCell(DevCellKind.Space, 0, 2.7f, Array.Empty<DevGasAmount>()));
        report.Cells.Add(new DevGasCell(DevCellKind.None, 0, 0, Array.Empty<DevGasAmount>()));
        report.Cells.Add(new DevGasCell(DevCellKind.Open, 0.5f, 1000f, new[] { new DevGasAmount(1, 0.25f) }));
        report.Cells.Add(new DevGasCell(DevCellKind.Open, 0, 0, Array.Empty<DevGasAmount>()));
        return report;
    }

    [Fact]
    public void ADevGasReportRoundTrips()
    {
        DevGasReport report = Report();
        byte[] bytes = Codec.Encode(report);
        Assert.True(bytes.Length <= Limits.MaxDevGasReportBytes);
        Assert.True(Codec.TryDecode(bytes, out DevGasReport decoded, out ProtocolError error), error.ToString());
        Assert.Equal((3, 4, 1, 3, 2), (decoded.X0, decoded.Y0, decoded.Z, decoded.Width, decoded.Height));
        Assert.Equal(report.Gases, decoded.Gases);
        for (int i = 0; i < report.Cells.Count; i++)
        {
            Assert.Equal((report.Cells[i].Kind, report.Cells[i].Pressure, report.Cells[i].Temperature), (decoded.Cells[i].Kind, decoded.Cells[i].Pressure, decoded.Cells[i].Temperature));
            Assert.Equal(report.Cells[i].Moles, decoded.Cells[i].Moles);
        }
        Assert.Equal(bytes, Codec.Encode(decoded));
        Assert.Equal(DevCellKind.Space, decoded.At(5, 4).Kind);
        Assert.Equal(1000f, decoded.At(4, 5).Temperature);
        Assert.Equal(DevCellKind.None, decoded.At(9, 9).Kind);
    }

    [Fact]
    public void ADevGasReportRefusesWhatItCannotHold()
    {
        DevGasReport report = Report();
        report.Cells.RemoveAt(0);
        Assert.Throws<ProtocolException>(() => Codec.Encode(report)); // the cells must fill it
        report = Report();
        report.Width = 16;
        Assert.Throws<ProtocolException>(() => Codec.Encode(report)); // at most 15 x 15
        report = Report();
        report.Cells[0] = report.Cells[0] with { Moles = new[] { new DevGasAmount(2, 1f) } };
        Assert.Throws<ProtocolException>(() => Codec.Encode(report)); // a gas outside the table
        report = Report();
        report.Cells[1] = new DevGasCell(DevCellKind.Closed, 1f, 0, Array.Empty<DevGasAmount>());
        Assert.Throws<ProtocolException>(() => Codec.Encode(report)); // a wall has no air

        byte[] bytes = Codec.Encode(Report());
        Assert.False(Codec.TryDecode(bytes.AsSpan(0, bytes.Length - 1), out DevGasReport _, out _));
        Assert.False(Codec.TryDecode(bytes.Concat(new byte[] { 0 }).ToArray(), out DevGasReport _, out _));
    }

    [Fact]
    public void TheGasAlphaIsTgs()
    {
        // generate_gas_overlays: log(4, (j + 0.4 * 80) / (0.35 * 80)) * 255
        Assert.Equal(255.0, GasVisual.Alpha(80), 3);
        Assert.Equal(Math.Log(33.0 / 28.0, 4) * 255, GasVisual.Alpha(1), 3);
    }
}
