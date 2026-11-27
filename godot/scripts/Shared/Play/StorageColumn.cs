using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// ui-overhaul.md section 12, slice 4B-2: the Tab inventory's right column, the storage panels. Engine-free: no Godot API here (it is compiled into
// tests/Anomaly.Server.Tests too). The panels are the client's own drawing of the server's Storages message (DV692); their cells are ours (negative
// ids, LiveHud.IsOurs), never sent to the server as screen entries.
// DEVIATION(DV690): the panels are not tg's storage screen (tg shows one storage at a time); every carried storage is shown at once.

/// <summary>
/// One storage panel of the right column: the storage (its entity), its header's title and room, and its cells, each with the item it shows (an empty
/// cell's entity is 0) and the cell a move onto it wishes for in the storage (the item's place in tg's contents from 1; 0 for an empty cell: simply into it).
/// </summary>
public sealed record StoragePanel(long Storage, StoragePlace Place, string Title, string Room, bool Locked, StorageItem[] Cells, int[] Wishes);

/// <summary>Where one panel is drawn: its backing, its header (and the storage's picture in it) and its cells, screen pixels, scrolled.</summary>
public readonly record struct StoragePanelRects(HudRect Backing, HudRect Header, HudRect Picture, HudRect[] Cells);

/// <summary>
/// The right column this frame (<see cref="StorageColumn.Layout"/>): the column, the panels in it, scrolled by <see cref="Scroll"/> (0 to
/// <see cref="MaxScroll"/>) when their whole height <see cref="Content"/> is more than the column's, and the scroll bar's track and thumb then.
/// </summary>
public sealed record StorageColumnLayout(HudRect Column, StoragePanelRects[] Panels, float Content, float Scroll, float MaxScroll, HudRect Track, HudRect Thumb, float Cell)
{
    /// <summary>No column (first person, another HUD, no room).</summary>
    public static readonly StorageColumnLayout None = new(default, Array.Empty<StoragePanelRects>(), 0f, 0f, 0f, default, default, 0f);
}

/// <summary>The right column's panels, layout, scrolling and pick ids (ui-overhaul.md section 12, slice 4B-2).</summary>
public static class StorageColumn
{
    private const long FirstId = -1000;
    private const int IdsPerPanel = 100;

    /// <summary>The HUD pick's id of panel <paramref name="panel"/>'s header and backing: ours, below zero (<see cref="LiveHud.IsOurs"/>).</summary>
    public static long PanelId(int panel) => FirstId - IdsPerPanel * panel;

    /// <summary>The HUD pick's id of cell <paramref name="cell"/> (from 0) of panel <paramref name="panel"/>.</summary>
    public static long CellId(int panel, int cell) => PanelId(panel) - 1 - cell;

    /// <summary>The panel and cell (-1 for the header or backing) a pick id of <see cref="PanelId"/> or <see cref="CellId"/> names; false for any other id.</summary>
    public static bool Decode(long id, out int panel, out int cell)
    {
        long k = FirstId - id;
        if (k < 0 || k >= IdsPerPanel * (long)Limits.MaxStorages)
        {
            (panel, cell) = (-1, -1);
            return false;
        }
        panel = (int)(k / IdsPerPanel);
        cell = (int)(k % IdsPerPanel) - 1;
        if (cell >= UiTokens.StorageMaxCells)
        {
            (panel, cell) = (-1, -1);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Which item each of the bar's seven belt cells holds (0: none), as the Hotbar message names it: the item in the belt, or the one out from the
    /// cell in a hand; none for an empty cell or without a worn belt.
    /// </summary>
    public static long[] BeltOnBar(HotbarModel bar)
    {
        var shown = new long[HotbarMessage.BeltCells];
        if (bar == null || !bar.BeltWorn)
            return shown;
        for (int cell = 0; cell < HotbarMessage.BeltCells; cell++)
        {
            if (bar.Cells[cell].State != HotbarCellState.Empty)
                shown[cell] = bar.Cells[cell].Entity;
        }
        return shown;
    }

    /// <summary>
    /// The panels of the carried storages (in the message's order): one cell for every slot tg gives the storage (at most
    /// <see cref="UiTokens.StorageMaxCells"/>), its items in tg's order from the first; the room as tg counts it ("14 / 21"). A locked storage is a
    /// header reading "Locked" with no cells; a storage whose contents tg hides has no panel. The belt has a panel only for its slots beyond the
    /// bar's seven cells, holding the items no bar cell shows (<paramref name="onBar"/>, <see cref="BeltOnBar"/>), titled with the belt's name.
    /// </summary>
    public static List<StoragePanel> Panels(IReadOnlyList<CarriedStorage> storages, IReadOnlyList<long> onBar)
    {
        var panels = new List<StoragePanel>();
        foreach (CarriedStorage s in storages ?? Array.Empty<CarriedStorage>())
        {
            if (s.Hidden)
                continue;
            if (s.Locked)
            {
                panels.Add(new StoragePanel(s.Entity, s.Place, s.Name, "Locked", true, Array.Empty<StorageItem>(), Array.Empty<int>()));
                continue;
            }
            var items = new List<(StorageItem Item, int Wish)>();
            for (int i = 0; i < s.Items.Count; i++)
            {
                if (s.Place != StoragePlace.Belt || onBar == null || !System.Linq.Enumerable.Contains(onBar, s.Items[i].Entity))
                    // the belt's wish is a bar cell (DV689, DV692): its panel's cells, which are off the bar, wish for none
                    items.Add((s.Items[i], s.Place == StoragePlace.Belt ? 0 : i + 1));
            }
            int slots = s.Place == StoragePlace.Belt ? s.Slots - HotbarMessage.BeltCells : s.Slots;
            int count = Math.Min(UiTokens.StorageMaxCells, Math.Max(slots, items.Count));
            if (s.Place == StoragePlace.Belt && count <= 0)
                continue;
            var cells = new StorageItem[Math.Max(0, count)];
            var wishes = new int[cells.Length];
            for (int i = 0; i < items.Count && i < cells.Length; i++)
                (cells[i], wishes[i]) = items[i];
            panels.Add(new StoragePanel(s.Entity, s.Place, s.Name, Room(s), false, cells, wishes));
        }
        return panels;
    }

    /// <summary>A storage's room as tg counts it: its items' summed weight classes against its limit.</summary>
    public static string Room(CarriedStorage s)
        => s.Used.ToString(System.Globalization.CultureInfo.InvariantCulture) + " / " + s.Limit.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// The panels in <paramref name="column"/> (<see cref="TabInventoryLayout.Column"/>), top to bottom, <see cref="UiTokens.StoragePanelGap"/>
    /// apart: each a backing (<see cref="UiTokens.StoragePanelPad"/> round its content), a header as tall as an item's picture in a cell of
    /// <paramref name="cell"/> (the bar's cell side) with the storage's picture at its left, and under it the cells, <see cref="UiTokens.StorageCellGap"/>
    /// apart, as many to a row as fit. When the panels are taller than the column a scroll bar of <see cref="UiTokens.StorageScrollBar"/> stands at
    /// its right edge (the panels narrower by it and a gap) and they are moved up by <paramref name="scroll"/>, kept from 0 to the overflow. At
    /// <paramref name="unit"/> screen pixels per reference pixel; everything at whole pixels.
    /// </summary>
    public static StorageColumnLayout Layout(HudRect column, float cell, float unit, IReadOnlyList<StoragePanel> panels, float scroll)
    {
        if (column.Width <= 0f || column.Height <= 0f || panels == null || panels.Count == 0)
            return StorageColumnLayout.None with { Column = column };
        float pad = Math.Max(1f, MathF.Round(UiTokens.StoragePanelPad * unit)), gap = Math.Max(1f, MathF.Round(UiTokens.StorageCellGap * unit));
        float between = MathF.Round(UiTokens.StoragePanelGap * unit), bar = Math.Max(1f, MathF.Round(UiTokens.StorageScrollBar * unit));
        cell = MathF.Floor(cell);
        // a column too narrow for one cell of the least size beside a scroll bar has no panels (as the character panel is left out below it)
        if (column.Width - 2 * pad - bar - gap < UiTokens.TabCellMinPixels)
            return StorageColumnLayout.None with { Column = column };
        float content = Measure(column.Width, cell, unit, pad, gap, between, panels);
        bool overflow = content > column.Height;
        float width = overflow ? column.Width - bar - gap : column.Width;
        if (overflow)
            content = Measure(width, cell, unit, pad, gap, between, panels);
        float max = Math.Max(0f, content - column.Height);
        scroll = Math.Clamp(MathF.Round(scroll), 0f, max);
        var rects = new StoragePanelRects[panels.Count];
        float y = column.Top - scroll;
        for (int p = 0; p < panels.Count; p++)
        {
            (int perRow, float side) = Grid(width, cell, pad, gap);
            float header = HeaderHeight(side, unit);
            int n = panels[p].Cells.Length;
            var cells = new HudRect[n];
            float gridTop = y + pad + header + pad;
            for (int i = 0; i < n; i++)
                cells[i] = new HudRect(column.Left + pad + i % perRow * (side + gap), gridTop + i / perRow * (side + gap), side, side);
            float height = PanelHeight(n, perRow, side, header, pad, gap);
            var headerRect = new HudRect(column.Left + pad, y + pad, width - 2 * pad, header);
            rects[p] = new StoragePanelRects(new HudRect(column.Left, y, width, height), headerRect, new HudRect(headerRect.Left, headerRect.Top, header, header), cells);
            y += height + between;
        }
        HudRect track = default, thumb = default;
        if (overflow)
        {
            track = new HudRect(column.Right - bar, column.Top, bar, column.Height);
            float thumbHeight = Math.Min(column.Height, Math.Max(bar * 4f, MathF.Round(column.Height * column.Height / content))); // never taller than its track
            float thumbTop = column.Top + (max > 0f ? MathF.Round((column.Height - thumbHeight) * scroll / max) : 0f);
            thumb = new HudRect(track.Left, thumbTop, bar, thumbHeight);
        }
        return new StorageColumnLayout(column, rects, content, scroll, max, track, thumb, cell);
    }

    /// <summary>The scroll place after <paramref name="steps"/> wheel steps (positive: down) from <paramref name="scroll"/>, kept from 0 to <paramref name="max"/>.</summary>
    public static float Scrolled(float scroll, int steps, float unit, float max)
        => Math.Clamp(scroll + steps * MathF.Round(UiTokens.StorageScrollStep * unit), 0f, Math.Max(0f, max));

    /// <summary>
    /// What the HUD pick gets for the column: each panel's backing under its <see cref="PanelId"/> (its header included), each cell over it under its
    /// <see cref="CellId"/>, every one cut at the column's edges, so a cell scrolled half out is picked only on its part inside the column.
    /// </summary>
    public static void Drawn(StorageColumnLayout layout, List<HudDrawn> into)
    {
        for (int p = 0; p < layout.Panels.Length; p++)
        {
            StoragePanelRects r = layout.Panels[p];
            if (Cut(r.Backing, layout.Column) is HudRect backing)
                into.Add(new HudDrawn(PanelId(p), backing, null, 0, 0));
            for (int c = 0; c < r.Cells.Length; c++)
            {
                if (Cut(r.Cells[c], layout.Column) is HudRect cell)
                    into.Add(new HudDrawn(CellId(p, c), cell, null, 0, 0));
            }
        }
    }

    /// <summary>The part of <paramref name="r"/> inside <paramref name="clip"/>, null when none.</summary>
    public static HudRect? Cut(HudRect r, HudRect clip)
    {
        float left = Math.Max(r.Left, clip.Left), top = Math.Max(r.Top, clip.Top), right = Math.Min(r.Right, clip.Right), bottom = Math.Min(r.Bottom, clip.Bottom);
        return right > left && bottom > top ? new HudRect(left, top, right - left, bottom - top) : null;
    }

    /// <summary>The storage's picture: the item drawn in a hand box or a slot whose child it is (a worn or held storage is one); none when no entry shows it.</summary>
    public static TableRef PictureOf(IReadOnlyDictionary<long, ScreenEntry> screen, long storage)
    {
        if (screen == null)
            return TableRef.None;
        foreach (ScreenEntry e in screen.Values)
        {
            if (e.VisChildren == null)
                continue;
            foreach (ScreenChild c in e.VisChildren.Items)
            {
                if (c.Id == storage)
                    return c.Appearance;
            }
        }
        return TableRef.None;
    }

    // a header is as tall as an item's picture in a cell (LiveHud.HandPicture: the cell less the hand's inset each side)
    private static float HeaderHeight(float side, float unit) => Math.Max(1f, side - 2 * MathF.Round(UiTokens.HandInset * unit));

    private static (int PerRow, float Side) Grid(float width, float cell, float pad, float gap)
    {
        float inner = width - 2 * pad, side = Math.Max(1f, Math.Min(cell, MathF.Floor(inner)));
        return (Math.Max(1, (int)MathF.Floor((inner + gap) / (side + gap))), side);
    }

    private static float PanelHeight(int cells, int perRow, float side, float header, float pad, float gap)
    {
        int rows = (cells + perRow - 1) / perRow;
        return pad + header + (rows > 0 ? pad + rows * side + (rows - 1) * gap : 0f) + pad;
    }

    private static float Measure(float width, float cell, float unit, float pad, float gap, float between, IReadOnlyList<StoragePanel> panels)
    {
        (int perRow, float side) = Grid(width, cell, pad, gap);
        float header = HeaderHeight(side, unit), total = 0f;
        foreach (StoragePanel p in panels)
            total += PanelHeight(p.Cells.Length, perRow, side, header, pad, gap);
        return total + between * (panels.Count - 1);
    }
}
