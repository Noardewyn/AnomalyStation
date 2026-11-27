using System.Globalization;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: M4a P7 (client interaction). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// docs/ai/specs/interaction.md 5, D131: a native window over one HUD group's screen entries. The server stays 100% tg: the cells, the close
// button and the items of an open storage are tg's own screen objects with tg's orient_storage screen_locs; this only lays them out.

/// <summary>
/// <see cref="IContainerView"/>: lays out the entries of one HUD group as a grid from their tg <c>screen_loc</c>s. For the storage group (tg's
/// <c>storage_interface</c>): the <c>cells</c> entry (<c>storage_cell</c>, a range <c>"x:px,y:py to x2:px2,y2:py2"</c> over the whole grid) gives the
/// grid and its size, an item entry (<c>"x:px,y:py"</c>) is the cell it stands on, <c>storage_close</c> is the close button, the corners, row
/// joins and the up button are other.
/// </summary>
public sealed class ContainerLayout : IContainerView
{
    // tg's icon states of the storage interface (icons/hud/screen_midnight.dmi, screen_objects.dm:312, :643-:700).
    private const string CellState = "storage_cell", CloseState = "storage_close", UpState = "storage_up", CornerPrefix = "storage_corner", RowjoinPrefix = "storage_rowjoin";

    private readonly List<ContainerCell> _cells = new();

    public ContainerLayout(byte group) => Group = group;

    /// <inheritdoc />
    public byte Group { get; }

    /// <inheritdoc />
    public bool IsOpen => _cells.Count > 0;

    /// <inheritdoc />
    public IReadOnlyList<ContainerCell> Cells => _cells;

    /// <inheritdoc />
    public int Columns { get; private set; }

    /// <inheritdoc />
    public int Rows { get; private set; }

    /// <summary>The pixel box of the cells entry in tg pixels (storage only): where the grid starts and how big it is.</summary>
    public bool HasGridBox { get; private set; }

    /// <inheritdoc />
    public void Rebuild(IReadOnlyList<ScreenEntry> entries, ReplicaWorld world)
    {
        _cells.Clear();
        Columns = Rows = 0;
        HasGridBox = false;
        if (entries == null || entries.Count == 0)
            return;
        var placed = new List<(ScreenEntry Entry, ScreenLocSpec Spec, string State)>();
        foreach (ScreenEntry entry in entries.OrderBy(e => e.Id))
        {
            if (!ScreenLoc.TryParse(entry.ScreenLoc, out ScreenLocSpec spec))
                continue;
            string state = null;
            if (world != null && world.TryGetAppearance(entry.Appearance, out AppearanceEntry appearance))
                state = world.ResolveString(appearance.IconState);
            placed.Add((entry, spec, state));
        }
        if (placed.Count == 0)
            return;
        LayoutStorage(placed);
        _cells.Sort(static (a, b) => a.Row != b.Row ? a.Row.CompareTo(b.Row) : a.Column != b.Column ? a.Column.CompareTo(b.Column) : a.EntryId.CompareTo(b.EntryId));
    }

    private void LayoutStorage(List<(ScreenEntry Entry, ScreenLocSpec Spec, string State)> placed)
    {
        // The grid: the cells entry's range; without it (a foreign build) the extent of the items.
        int originX = int.MaxValue, originY = int.MaxValue;
        int columns = 0, rows = 0;
        foreach (var p in placed)
        {
            if (p.State == CellState && p.Spec.HasRange)
            {
                originX = PixelX(p.Spec.X);
                originY = PixelY(p.Spec.Y);
                columns = Math.Max(1, (int)Math.Round((PixelX(p.Spec.X2) - originX) / 32.0) + 1);
                rows = Math.Max(1, (int)Math.Round((PixelY(p.Spec.Y2) - originY) / 32.0) + 1);
                HasGridBox = true;
                break;
            }
        }
        if (!HasGridBox)
        {
            int maxX = int.MinValue, maxY = int.MinValue;
            foreach (var p in placed)
            {
                if (!IsItem(p.State))
                    continue;
                originX = Math.Min(originX, PixelX(p.Spec.X));
                originY = Math.Min(originY, PixelY(p.Spec.Y));
                maxX = Math.Max(maxX, PixelX(p.Spec.X));
                maxY = Math.Max(maxY, PixelY(p.Spec.Y));
            }
            if (originX == int.MaxValue)
            {
                originX = originY = 0;
                columns = rows = 1;
            }
            else
            {
                columns = (int)Math.Round((maxX - originX) / 32.0) + 1;
                rows = (int)Math.Round((maxY - originY) / 32.0) + 1;
            }
        }
        Columns = columns;
        Rows = rows;
        foreach (var p in placed)
        {
            if (p.State == CellState)
            {
                // The one stretched cell sprite is the whole grid's background: a click on it inserts the held item.
                _cells.Add(new ContainerCell(p.Entry.Id, 0, 0, ContainerCellRole.Slot, "Storage"));
            }
            else if (p.State == CloseState)
                _cells.Add(new ContainerCell(p.Entry.Id, Math.Max(0, columns), 0, ContainerCellRole.Close, "Close"));
            else if (p.State == UpState || (p.State != null && (p.State.StartsWith(CornerPrefix, StringComparison.Ordinal) || p.State.StartsWith(RowjoinPrefix, StringComparison.Ordinal))))
                _cells.Add(new ContainerCell(p.Entry.Id, 0, 0, ContainerCellRole.Other, Humanise(p.State)));
            else
            {
                int column = Math.Clamp((int)Math.Round((PixelX(p.Spec.X) - originX) / 32.0), 0, Math.Max(0, columns - 1));
                int row = Math.Clamp(rows - 1 - (int)Math.Round((PixelY(p.Spec.Y) - originY) / 32.0), 0, Math.Max(0, rows - 1));
                _cells.Add(new ContainerCell(p.Entry.Id, column, row, ContainerCellRole.Item, LabelOf(p.State, "Item")));
            }
        }
    }

    private static bool IsItem(string state)
        => state != CellState && state != CloseState && state != UpState
           && !(state != null && (state.StartsWith(CornerPrefix, StringComparison.Ordinal) || state.StartsWith(RowjoinPrefix, StringComparison.Ordinal)));

    // A position on the 15 x 15 HUD grid in tg pixels (32 a tile) from the grid's lower left: the anchor's base tile (the middle for CENTER, the far
    // edge for EAST and NORTH, so that entries of one anchor line up and a pair of anchors keep their order).
    private static int PixelX(in ScreenAxis axis) => Pixel(axis);

    private static int PixelY(in ScreenAxis axis) => Pixel(axis);

    private static int Pixel(in ScreenAxis axis)
    {
        int baseTile = axis.Anchor switch { ScreenAnchor.Center => 7, ScreenAnchor.High => 14, _ => 0 };
        return (baseTile + axis.Tile) * 32 + axis.Pixels;
    }

    private static string LabelOf(string iconState, string fallback)
        => string.IsNullOrEmpty(iconState) ? fallback : Humanise(iconState);

    /// <summary>An icon state as a label: underscores and dashes are spaces, the first letter is a capital.</summary>
    public static string Humanise(string iconState)
    {
        if (string.IsNullOrEmpty(iconState))
            return string.Empty;
        string text = iconState.Replace('_', ' ').Replace('-', ' ').Trim();
        return text.Length == 0 ? string.Empty : char.ToUpper(text[0], CultureInfo.InvariantCulture) + text.Substring(1);
    }
}
