using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// ui-overhaul.md section 12, slice 4B-2: the Tab inventory's right column, the storage panels. Engine-free: no Godot API here (it is compiled into
// tests/Anomaly.Server.Tests too). The panels are the client's own drawing of the server's Storages message (DV696); their cells are ours (negative
// ids, LiveHud.IsOurs), never sent to the server as screen entries.
// DEVIATION(DV694): the panels are not tg's storage screen (tg shows one storage at a time); every carried storage is shown at once. Slice 4C-2: the
// other container the player has open (the message's last entry) is a panel pinned at the column's bottom, over the Nearby strip of the items lying
// round the player (NearbyItems).

/// <summary>
/// One storage panel of the right column: the storage (its entity), its header's title and room, and its cells, each with the item it shows (an empty
/// cell's entity is 0) and the cell a move onto it wishes for in the storage (the item's place in tg's contents from 1; 0 for an empty cell: simply into it).
/// </summary>
public sealed record StoragePanel(long Storage, StoragePlace Place, string Title, string Room, bool Locked, StorageItem[] Cells, int[] Wishes);

/// <summary>Where one panel is drawn: its backing, its header (and the storage's picture in it) and its cells, screen pixels, scrolled.</summary>
public readonly record struct StoragePanelRects(HudRect Backing, HudRect Header, HudRect Picture, HudRect[] Cells);

/// <summary>
/// Slice 4C-2: the Nearby strip at the column's bottom: its backing, its title's line, the row its cells are seen in (<see cref="Clip"/>) and the cells,
/// moved left by <see cref="Scroll"/> (0 to <see cref="MaxScroll"/>) when they do not fit the row.
/// </summary>
public sealed record NearbyStripRects(HudRect Backing, HudRect Title, HudRect Clip, HudRect[] Cells, float Scroll, float MaxScroll);

/// <summary>
/// The right column this frame (<see cref="StorageColumn.Layout"/>): the own storages' area (<see cref="Column"/>), the panels in it, scrolled by
/// <see cref="Scroll"/> (0 to <see cref="MaxScroll"/>) when their whole height <see cref="Content"/> is more than the area's, and the scroll bar's track
/// and thumb then. Slice 4C-2: the other container's panel pinned under them (<see cref="Pinned"/>: its index in <see cref="Panels"/>, -1 for none),
/// its rows seen in <see cref="PinnedClip"/> and scrolled by <see cref="PinnedScroll"/>, the line over what is pinned (<see cref="Line"/>), and the
/// Nearby strip at the very bottom (<see cref="Strip"/>, null for none).
/// </summary>
public sealed record StorageColumnLayout(HudRect Column, StoragePanelRects[] Panels, float Content, float Scroll, float MaxScroll, HudRect Track, HudRect Thumb, float Cell)
{
    /// <summary>No column (first person, another HUD, no room).</summary>
    public static readonly StorageColumnLayout None = new(default, Array.Empty<StoragePanelRects>(), 0f, 0f, 0f, default, default, 0f);

    /// <summary>The whole right column the layout was made in.</summary>
    public HudRect Whole { get; init; }

    /// <summary>The line between the own storages and what is pinned under them; empty for none.</summary>
    public HudRect Line { get; init; }

    /// <summary>The pinned panel's index in <see cref="Panels"/> (the other container's, always the last), -1 for none.</summary>
    public int Pinned { get; init; } = -1;

    /// <summary>Where the pinned panel's cells are seen (its backing under its header); a cell scrolled out of it is cut.</summary>
    public HudRect PinnedClip { get; init; }

    /// <summary>How far the pinned panel's rows are scrolled (0 to <see cref="PinnedMax"/>).</summary>
    public float PinnedScroll { get; init; }

    /// <summary>How far the pinned panel's rows can be scrolled.</summary>
    public float PinnedMax { get; init; }

    /// <summary>The Nearby strip, null for none.</summary>
    public NearbyStripRects Strip { get; init; }

    /// <summary>Whether the own storages have a panel here (the panels less the pinned one).</summary>
    public bool HasOwn => Panels.Length > (Pinned >= 0 ? 1 : 0);
}

/// <summary>Which part of the right column a point is in (<see cref="StorageColumn.AreaAt"/>): what the mouse wheel there scrolls.</summary>
public enum ColumnArea : byte
{
    None = 0,
    Own,
    Other,
    Nearby,
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
        if (k < 0 || k >= IdsPerPanel * (long)Limits.MaxStorageEntries)
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

    /// <summary>Slice 4C-2: the HUD pick's id of the Nearby strip's backing and title: ours, below zero and clear of the panels' ids.</summary>
    public const long StripId = -3000;

    /// <summary>The HUD pick's id of the Nearby strip's cell <paramref name="cell"/> (from 0).</summary>
    public static long StripCellId(int cell) => StripId - 1 - cell;

    /// <summary>The strip's cell (-1 for its backing or title) a pick id of <see cref="StripId"/> or <see cref="StripCellId"/> names; false for any other id.</summary>
    public static bool DecodeStrip(long id, out int cell)
    {
        long k = StripId - id;
        cell = k >= 0 && k <= NearbyItems.MaxItems ? (int)k - 1 : -1;
        return k >= 0 && k <= NearbyItems.MaxItems;
    }

    /// <summary>The Nearby strip's title.</summary>
    public const string NearbyTitle = "Nearby";

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
    /// Slice 4C-2: the other container's panel (<see cref="StoragePlace.Other"/>) is always the last, the one <see cref="Layout"/> pins.
    /// </summary>
    public static List<StoragePanel> Panels(IReadOnlyList<CarriedStorage> storages, IReadOnlyList<long> onBar)
    {
        var panels = new List<StoragePanel>();
        StoragePanel other = null;
        foreach (CarriedStorage s in storages ?? Array.Empty<CarriedStorage>())
        {
            if (s.Hidden)
                continue;
            if (s.Locked)
            {
                var locked = new StoragePanel(s.Entity, s.Place, s.Name, "Locked", true, Array.Empty<StorageItem>(), Array.Empty<int>());
                if (s.Place == StoragePlace.Other)
                    other = locked;
                else
                    panels.Add(locked);
                continue;
            }
            var items = new List<(StorageItem Item, int Wish)>();
            for (int i = 0; i < s.Items.Count; i++)
            {
                if (s.Place != StoragePlace.Belt || onBar == null || !System.Linq.Enumerable.Contains(onBar, s.Items[i].Entity))
                    // the belt's wish is a bar cell (DV693, DV696): its panel's cells, which are off the bar, wish for none
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
            var panel = new StoragePanel(s.Entity, s.Place, s.Name, Room(s), false, cells, wishes);
            if (s.Place == StoragePlace.Other)
                other = panel;
            else
                panels.Add(panel);
        }
        if (other != null)
            panels.Add(other);
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
    /// <para>
    /// Slice 4C-2: with <paramref name="nearby"/> of 0 or more, the Nearby strip stands at the column's very bottom: a backing like a panel's with the
    /// title's line and one row of <c>max(1, nearby)</c> cells, moved left by <paramref name="nearbyScroll"/> when they do not fit. A last panel of the
    /// other container (<see cref="StoragePlace.Other"/>) is pinned above it, <see cref="UiTokens.StoragePanelGap"/> apart, as tall as its cells need
    /// but at most half of the height left over the strip, and never less than its header and one row while the column holds them (the own storages
    /// give way first) nor so tall that the own storages lose their first header and row where both fit; its rows scroll by
    /// <paramref name="otherScroll"/> under its header. The own storages take what is left above, with a line of
    /// <see cref="UiTokens.StorageLineWidth"/> in the gap over what is pinned. A column too short for the strip places nothing.
    /// </para>
    /// </summary>
    public static StorageColumnLayout Layout(HudRect column, float cell, float unit, IReadOnlyList<StoragePanel> panels, float scroll,
        int nearby = -1, float otherScroll = 0f, float nearbyScroll = 0f)
    {
        panels ??= Array.Empty<StoragePanel>();
        if (column.Width <= 0f || column.Height <= 0f || (panels.Count == 0 && nearby < 0))
            return StorageColumnLayout.None with { Column = column };
        float pad = Math.Max(1f, MathF.Round(UiTokens.StoragePanelPad * unit)), gap = Math.Max(1f, MathF.Round(UiTokens.StorageCellGap * unit));
        float between = MathF.Round(UiTokens.StoragePanelGap * unit), bar = Math.Max(1f, MathF.Round(UiTokens.StorageScrollBar * unit));
        float line = Math.Max(1f, MathF.Round(UiTokens.StorageLineWidth * unit)), over = Math.Max(between, line);
        cell = MathF.Floor(cell);
        // a column too narrow for one cell of the least size beside a scroll bar has no panels (as the character panel is left out below it)
        if (column.Width - 2 * pad - bar - gap < UiTokens.TabCellMinPixels)
            return StorageColumnLayout.None with { Column = column };
        (int perRow, float side) = Grid(column.Width, cell, pad, gap);
        float header = HeaderHeight(side, unit);
        int pinned = panels.Count > 0 && panels[^1].Place == StoragePlace.Other ? panels.Count - 1 : -1;
        int own = pinned >= 0 ? panels.Count - 1 : panels.Count;
        float floor = column.Bottom; // the top of what is pinned so far

        // the Nearby strip at the very bottom; a column too short for it places nothing
        NearbyStripRects strip = null;
        if (nearby >= 0)
        {
            float height = pad + header + pad + side + pad;
            if (height > column.Height)
                return StorageColumnLayout.None with { Column = column };
            var backing = new HudRect(column.Left, column.Bottom - height, column.Width, height);
            var title = new HudRect(column.Left + pad, backing.Top + pad, column.Width - 2 * pad, header);
            var clip = new HudRect(title.Left, title.Bottom + pad, title.Width, side);
            int n = Math.Max(1, Math.Min(nearby, NearbyItems.MaxItems));
            float max = Math.Max(0f, n * side + (n - 1) * gap - clip.Width);
            float moved = Math.Clamp(MathF.Round(nearbyScroll), 0f, max);
            var cells = new HudRect[n];
            for (int i = 0; i < n; i++)
                cells[i] = new HudRect(clip.Left + i * (side + gap) - moved, clip.Top, side, side);
            strip = new NearbyStripRects(backing, title, clip, cells, moved, max);
            floor = backing.Top;
        }

        // the other container's panel pinned over it
        var rects = new StoragePanelRects[panels.Count];
        HudRect pinnedClip = default;
        float pinnedScroll = 0f, pinnedMax = 0f;
        if (pinned >= 0)
        {
            rects[pinned] = new StoragePanelRects(default, default, default, Array.Empty<HudRect>());
            float below = strip != null ? between : 0f;
            float usable = floor - below - column.Top;
            int n = panels[pinned].Cells.Length;
            float full = PanelHeight(n, perRow, side, header, pad, gap), oneRow = PanelHeight(Math.Min(n, perRow), perRow, side, header, pad, gap);
            float ownFirst = own > 0 ? over + PanelHeight(Math.Min(panels[0].Cells.Length, perRow), perRow, side, header, pad, gap) : 0f;
            float height = Math.Min(Math.Min(full, usable), Math.Max(oneRow, Math.Min(MathF.Floor(usable / 2f), usable - ownFirst)));
            if (height >= pad + header + pad) // else no room for even its header: not placed
            {
                float top = floor - below - height;
                var backing = new HudRect(column.Left, top, column.Width, height);
                var headerRect = new HudRect(column.Left + pad, top + pad, column.Width - 2 * pad, header);
                pinnedClip = new HudRect(headerRect.Left, headerRect.Bottom + pad, headerRect.Width, Math.Max(0f, backing.Bottom - pad - (headerRect.Bottom + pad)));
                int rows = (n + perRow - 1) / perRow;
                pinnedMax = Math.Max(0f, rows * side + Math.Max(0, rows - 1) * gap - pinnedClip.Height);
                pinnedScroll = Math.Clamp(MathF.Round(otherScroll), 0f, pinnedMax);
                var cells = new HudRect[n];
                for (int i = 0; i < n; i++)
                    cells[i] = new HudRect(pinnedClip.Left + i % perRow * (side + gap), pinnedClip.Top - pinnedScroll + i / perRow * (side + gap), side, side);
                rects[pinned] = new StoragePanelRects(backing, headerRect, new HudRect(headerRect.Left, headerRect.Top, header, header), cells);
                floor = top;
            }
        }

        // the own storages above, with the line in the gap over what is pinned
        HudRect lineRect = default;
        float ownBottom = column.Bottom;
        if (floor < column.Bottom)
        {
            ownBottom = floor - over;
            if (own > 0 && ownBottom > column.Top)
                lineRect = new HudRect(column.Left, ownBottom + MathF.Floor((over - line) / 2f), column.Width, line);
        }
        var area = new HudRect(column.Left, column.Top, column.Width, Math.Max(0f, ownBottom - column.Top));
        (float content, float ownScroll, float ownMax, HudRect track, HudRect thumb) = PlaceOwn(area, cell, unit, pad, gap, between, bar, panels, own, scroll, rects);
        return new StorageColumnLayout(area, rects, content, ownScroll, ownMax, track, thumb, cell)
        {
            Whole = column,
            Line = lineRect,
            Pinned = rects.Length > 0 && pinned >= 0 && rects[pinned].Backing.Height > 0f ? pinned : -1,
            PinnedClip = pinnedClip,
            PinnedScroll = pinnedScroll,
            PinnedMax = pinnedMax,
            Strip = strip,
        };
    }

    // The first `count` panels in `area`, top to bottom, scrolled (the slice 4B-2 column): their rects into `rects`; the content's height, the scroll
    // place kept from 0 to the overflow, and the scroll bar's track and thumb when they overflow.
    private static (float Content, float Scroll, float Max, HudRect Track, HudRect Thumb) PlaceOwn(HudRect area, float cell, float unit, float pad, float gap,
        float between, float bar, IReadOnlyList<StoragePanel> panels, int count, float scroll, StoragePanelRects[] rects)
    {
        if (count <= 0)
            return (0f, 0f, 0f, default, default);
        float content = Measure(area.Width, cell, unit, pad, gap, between, panels, count);
        bool overflow = content > area.Height;
        float width = overflow ? area.Width - bar - gap : area.Width;
        if (overflow)
            content = Measure(width, cell, unit, pad, gap, between, panels, count);
        float max = Math.Max(0f, content - area.Height);
        scroll = Math.Clamp(MathF.Round(scroll), 0f, max);
        float y = area.Top - scroll;
        (int perRow, float side) = Grid(width, cell, pad, gap);
        float header = HeaderHeight(side, unit);
        for (int p = 0; p < count; p++)
        {
            int n = panels[p].Cells.Length;
            var cells = new HudRect[n];
            float gridTop = y + pad + header + pad;
            for (int i = 0; i < n; i++)
                cells[i] = new HudRect(area.Left + pad + i % perRow * (side + gap), gridTop + i / perRow * (side + gap), side, side);
            float height = PanelHeight(n, perRow, side, header, pad, gap);
            var headerRect = new HudRect(area.Left + pad, y + pad, width - 2 * pad, header);
            rects[p] = new StoragePanelRects(new HudRect(area.Left, y, width, height), headerRect, new HudRect(headerRect.Left, headerRect.Top, header, header), cells);
            y += height + between;
        }
        HudRect track = default, thumb = default;
        if (overflow)
        {
            track = new HudRect(area.Right - bar, area.Top, bar, area.Height);
            float thumbHeight = Math.Min(area.Height, Math.Max(bar * 4f, MathF.Round(area.Height * area.Height / content))); // never taller than its track
            float thumbTop = area.Top + (max > 0f ? MathF.Round((area.Height - thumbHeight) * scroll / max) : 0f);
            thumb = new HudRect(track.Left, thumbTop, bar, thumbHeight);
        }
        return (content, scroll, max, track, thumb);
    }

    /// <summary>Slice 4C-2: the part of the column at the screen point (<paramref name="x"/>, <paramref name="y"/>): the strip, the pinned panel, the own storages' area, or none.</summary>
    public static ColumnArea AreaAt(StorageColumnLayout layout, float x, float y)
    {
        static bool In(HudRect r, float x, float y) => r.Width > 0f && r.Height > 0f && x >= r.Left && x < r.Right && y >= r.Top && y < r.Bottom;
        if (layout == null)
            return ColumnArea.None;
        if (layout.Strip != null && In(layout.Strip.Backing, x, y))
            return ColumnArea.Nearby;
        if (layout.Pinned >= 0 && In(layout.Panels[layout.Pinned].Backing, x, y))
            return ColumnArea.Other;
        return layout.HasOwn && In(layout.Column, x, y) ? ColumnArea.Own : ColumnArea.None;
    }

    /// <summary>The scroll place after <paramref name="steps"/> wheel steps (positive: down) from <paramref name="scroll"/>, kept from 0 to <paramref name="max"/>.</summary>
    public static float Scrolled(float scroll, int steps, float unit, float max)
        => Math.Clamp(scroll + steps * MathF.Round(UiTokens.StorageScrollStep * unit), 0f, Math.Max(0f, max));

    /// <summary>
    /// What the HUD pick gets for the column: each panel's backing under its <see cref="PanelId"/> (its header included), each cell over it under its
    /// <see cref="CellId"/>, every one cut at the column's edges, so a cell scrolled half out is picked only on its part inside the column. Slice 4C-2:
    /// the pinned panel's cells cut at <see cref="StorageColumnLayout.PinnedClip"/>; the Nearby strip's backing under <see cref="StripId"/> and its cells
    /// under <see cref="StripCellId"/>, cut at its row.
    /// </summary>
    public static void Drawn(StorageColumnLayout layout, List<HudDrawn> into)
    {
        for (int p = 0; p < layout.Panels.Length; p++)
        {
            StoragePanelRects r = layout.Panels[p];
            bool pinned = p == layout.Pinned;
            if (Cut(r.Backing, pinned ? layout.Whole : layout.Column) is HudRect backing)
                into.Add(new HudDrawn(PanelId(p), backing, null, 0, 0));
            for (int c = 0; c < r.Cells.Length; c++)
            {
                if (Cut(r.Cells[c], pinned ? layout.PinnedClip : layout.Column) is HudRect cell)
                    into.Add(new HudDrawn(CellId(p, c), cell, null, 0, 0));
            }
        }
        if (layout.Strip is not NearbyStripRects strip)
            return;
        into.Add(new HudDrawn(StripId, strip.Backing, null, 0, 0));
        for (int c = 0; c < strip.Cells.Length; c++)
        {
            if (Cut(strip.Cells[c], strip.Clip) is HudRect cell)
                into.Add(new HudDrawn(StripCellId(c), cell, null, 0, 0));
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

    // a header is as tall as an item's picture in a cell (LiveHud.HandPicture: the cell less the hand's inset each side); slice 4D-1's strip title too
    internal static float HeaderHeight(float side, float unit) => Math.Max(1f, side - 2 * MathF.Round(UiTokens.HandInset * unit));

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

    private static float Measure(float width, float cell, float unit, float pad, float gap, float between, IReadOnlyList<StoragePanel> panels, int count)
    {
        (int perRow, float side) = Grid(width, cell, pad, gap);
        float header = HeaderHeight(side, unit), total = 0f;
        for (int p = 0; p < count; p++)
            total += PanelHeight(panels[p].Cells.Length, perRow, side, header, pad, gap);
        return total + between * (count - 1);
    }
}
