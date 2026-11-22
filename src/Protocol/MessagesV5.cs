namespace Anomaly.Protocol;

// M4b slice 3 (the gas visuals, D18; the dev menu's gas overlay). Version 5 adds to version 4 (MessagesV4.cs):
// - the gas a cell shows: tg's GAS_OVERLAYS of the turf (update_visuals), one level per visible gas: in MapGrid (the gas
//   kinds table and every cell that shows gas) and as the Delta's cell gas changes;
// - ActionKind.DevGasProbe and the DevGasReport answer (MessageId 12): the air of the cells around a point, for the dev
//   menu's gas overlay (only when the server's DEV_COMMANDS is on).

/// <summary>
/// One gas a cell shows (tg's <c>/obj/effect/overlay/gas</c> in the turf's <c>vis_contents</c>): <see cref="Kind"/> is the
/// 1-based index of its icon state in <see cref="MapGrid.GasKinds"/>, <see cref="Level"/> tg's visible state, 1 to
/// <see cref="Limits.MaxGasLevel"/> (<c>ceil(moles / MOLES_GAS_VISIBLE_STEP)</c>, at most <c>TOTAL_VISIBLE_STATES</c>), whose
/// alpha is <c>log4((level + 0.4 * 80) / (0.35 * 80)) * 255</c> (<see cref="GasVisual.Alpha"/>).
/// </summary>
public readonly record struct GasLevel(byte Kind, byte Level);

/// <summary>The gas a cell shows: its levels in tg's order (<see cref="Gases"/> empty when the cell shows none).</summary>
public readonly record struct CellGasChange(int X, int Y, int Z, GasLevel[] Gases);

/// <summary>tg's numbers of the gas visuals the client needs.</summary>
public static class GasVisual
{
    /// <summary>tg's <c>TOTAL_VISIBLE_STATES</c>.</summary>
    public const int TotalVisibleStates = 80;

    /// <summary>The alpha (0-255) tg gives the overlay of <paramref name="level"/> (generate_gas_overlays, gas_types.dm:29).</summary>
    public static float Alpha(int level) => (float)(Math.Log((level + 0.4 * TotalVisibleStates) / (0.35 * TotalVisibleStates), 4) * 255);
}

/// <summary>Version 5 of <see cref="MapGrid"/>: the gas kinds and the cells that show gas.</summary>
public sealed partial class MapGrid
{
    /// <summary>Version 5: the icon states of tg's gas overlays (<c>gas_overlay</c> of the gases that have one: "plasma", "water_vapor", ...); <see cref="GasLevel.Kind"/> 1 is element 0. Fixed for the run.</summary>
    public List<string> GasKinds { get; } = new();

    /// <summary>Version 5: every cell that shows gas at the join (the rest show none); later changes come as <see cref="Delta.CellGasChanged"/>.</summary>
    public List<CellGasChange> GasCells { get; } = new();
}

/// <summary>Version 5 of <see cref="Delta"/>: the cells whose gas changed this tick.</summary>
public sealed partial class Delta
{
    /// <summary>Body: cells whose shown gas changed this tick (tg's <c>update_visuals</c> changed the turf's gas overlays).</summary>
    public List<CellGasChange> CellGasChanged { get; } = new();
}

/// <summary>What a cell of a <see cref="DevGasReport"/> is.</summary>
public enum DevCellKind : byte
{
    /// <summary>No turf (outside the map).</summary>
    None = 0,

    /// <summary>A closed turf (a wall): no air of its own.</summary>
    Closed = 1,

    /// <summary>An open turf with its air.</summary>
    Open = 2,

    /// <summary>A space turf (tg's space air: nothing at 2.7 K).</summary>
    Space = 3,
}

/// <summary>One gas of a <see cref="DevGasReport"/>'s table: tg's <c>id</c>, <c>name</c> and <c>primary_color</c> (0xRRGGBB).</summary>
public readonly record struct DevGasInfo(string Id, string Name, uint Rgb);

/// <summary>An amount of a gas in a cell: <see cref="Gas"/> is the index into <see cref="DevGasReport.Gases"/> (0-based).</summary>
public readonly record struct DevGasAmount(byte Gas, float Moles);

/// <summary>One cell of a <see cref="DevGasReport"/>: tg's <c>return_pressure()</c> in kPa, <c>temperature</c> in K and the moles of every gas present.</summary>
public readonly record struct DevGasCell(DevCellKind Kind, float Pressure, float Temperature, DevGasAmount[] Moles);

/// <summary>
/// Server to client, Control channel (version 5, a dev tool): the answer to <see cref="ActionKind.DevGasProbe"/>, the air of
/// the <see cref="Width"/> x <see cref="Height"/> cells from (<see cref="X0"/>, <see cref="Y0"/>) on level <see cref="Z"/>,
/// row by row from the south-west, x fastest.
/// </summary>
public sealed class DevGasReport
{
    public int X0 { get; set; }
    public int Y0 { get; set; }
    public int Z { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>The gases the cells name (at most <see cref="Limits.MaxDevGases"/>).</summary>
    public List<DevGasInfo> Gases { get; } = new();

    /// <summary><see cref="Width"/> x <see cref="Height"/> cells.</summary>
    public List<DevGasCell> Cells { get; } = new();

    /// <summary>The cell at (x, y), or a <see cref="DevCellKind.None"/> cell outside the report.</summary>
    public DevGasCell At(int x, int y)
    {
        int dx = x - X0, dy = y - Y0;
        if (dx < 0 || dy < 0 || dx >= Width || dy >= Height || Cells.Count != Width * Height) return default;
        return Cells[dy * Width + dx];
    }
}
