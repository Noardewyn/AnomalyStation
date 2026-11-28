using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// ui-overhaul.md section 12, slice 4C-2: the right column with the other container's panel pinned at its bottom and the Nearby strip under it: where
/// they are at the reference size, the pinned panel's half-height cap and its own scroll, the own storages giving way first on a short column, the
/// strip's scroll and its empty cell, which part the wheel scrolls, and a fuzz over column sizes and entry counts (no throw, no overlap, all inside).
/// </summary>
public class StorageColumnPinnedTests
{
    private static readonly HudRect Column = new(1237, 141, 575, 843); // the reference column (StorageColumnTests): 1920 x 1080 at 100 %

    private static HudRect R(float x, float y, float w, float h) => new(x, y, w, h);

    private static HudRect[] Row(float left, float top, int count) => Enumerable.Range(0, count).Select(i => R(left + 52 * i, top, 48, 48)).ToArray();

    private static bool Overlaps(HudRect a, HudRect b) => a.Width > 0 && a.Height > 0 && b.Width > 0 && b.Height > 0 && a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;

    private static bool Inside(HudRect inner, HudRect outer) => inner.Left >= outer.Left && inner.Right <= outer.Right && inner.Top >= outer.Top && inner.Bottom <= outer.Bottom;

    private static bool Whole(HudRect r) => r.Left == MathF.Round(r.Left) && r.Top == MathF.Round(r.Top) && r.Width == MathF.Round(r.Width) && r.Height == MathF.Round(r.Height);

    private static CarriedStorage Box(int slots, int items, long id = 900)
        => StorageColumnTests.Storage(id, StoragePlace.Other, "box", slots, items, slots * 2, Enumerable.Range(1, items).Select(i => StorageColumnTests.Item(id + i)));

    // the carried storages of StorageColumnTests.Carried (a backpack of 21, a pocket box of 7, a locked box) and the other container
    private static List<StoragePanel> With(CarriedStorage other, List<StoragePanel> own = null)
    {
        (List<CarriedStorage> s, HotbarModel bar) = StorageColumnTests.Carried(7, 3);
        List<StoragePanel> panels = own != null ? new List<StoragePanel>(own) : StorageColumn.Panels(s, StorageColumn.BeltOnBar(bar));
        if (other != null)
            panels.Add(StorageColumn.Panels(new List<CarriedStorage> { other }, null)[0]);
        return panels;
    }

    private static List<HudDrawn> DrawnOf(StorageColumnLayout l)
    {
        var d = new List<HudDrawn>();
        StorageColumn.Drawn(l, d);
        return d;
    }

    [Fact]
    public void AtTheReferenceSizeTheStripIsAtTheBottomThePinnedPanelOverItAndTheLineOverThat()
    {
        List<StoragePanel> panels = With(Box(7, 2));
        StorageColumnLayout l = StorageColumn.Layout(Column, 48f, 1f, panels, 0f, nearby: 3);
        // the strip: a backing as wide as the column, 96 tall at its bottom (984): its title's line (36) 4 inside it, under it one row of 48
        NearbyStripRects strip = l.Strip;
        Assert.Equal(R(1237, 888, 575, 96), strip.Backing);
        Assert.Equal(R(1241, 892, 567, 36), strip.Title);
        Assert.Equal(R(1241, 932, 567, 48), strip.Clip);
        Assert.Equal(Row(1241, 932, 3), strip.Cells);
        Assert.Equal((0f, 0f), (strip.Scroll, strip.MaxScroll));
        // the box (seven slots, one row) pinned 12 over it, as a carried storage's panel is drawn: its header and one row of seven cells
        Assert.Equal(panels.Count - 1, l.Pinned);
        StoragePanelRects box = l.Panels[l.Pinned];
        Assert.Equal(R(1237, 780, 575, 96), box.Backing);
        Assert.Equal(R(1241, 784, 567, 36), box.Header);
        Assert.Equal(R(1241, 784, 36, 36), box.Picture);
        Assert.Equal(R(1241, 824, 567, 48), l.PinnedClip);
        Assert.Equal(Row(1241, 824, 7), box.Cells);
        Assert.Equal(0f, l.PinnedMax);
        // the line, 1 thick, in the middle of the gap of 12 over it; the own storages above it, from the column's top, as before
        Assert.Equal(R(1237, 773, 575, 1), l.Line);
        Assert.Equal(R(1237, 141, 575, 627), l.Column);
        Assert.Equal(R(1237, 141, 575, 200), l.Panels[0].Backing);
        Assert.Equal(Column, l.Whole);
        // what the HUD pick gets: the box's backing and cells under its panel's ids, the strip's backing and cells under the strip's
        List<HudDrawn> drawn = DrawnOf(l);
        Assert.Equal(box.Backing, drawn.Single(d => d.EntryId == StorageColumn.PanelId(l.Pinned)).Rect);
        Assert.Equal(box.Cells[6], drawn.Single(d => d.EntryId == StorageColumn.CellId(l.Pinned, 6)).Rect);
        Assert.Equal(strip.Backing, drawn.Single(d => d.EntryId == StorageColumn.StripId).Rect);
        Assert.Equal(strip.Cells[2], drawn.Single(d => d.EntryId == StorageColumn.StripCellId(2)).Rect);
        // with no other container the line stands over the strip; with no strip and no other container the column is as slice 4B-2 made it
        StorageColumnLayout plain = StorageColumn.Layout(Column, 48f, 1f, With(null), 0f, nearby: 0);
        Assert.Equal(-1, plain.Pinned);
        Assert.Equal(R(1237, 881, 575, 1), plain.Line);
        Assert.Equal(R(1237, 141, 575, 735), plain.Column);
        StorageColumnLayout before = StorageColumn.Layout(Column, 48f, 1f, With(null), 0f);
        Assert.Null(before.Strip);
        Assert.Equal(Column, before.Column);
        Assert.Equal(default, before.Line);
    }

    [Fact]
    public void ThePinnedPanelTakesAtMostHalfTheHeightOverTheStripAndScrollsItsRowsUnderItsHeader()
    {
        // a bag of 64 slots: seven rows (360 of grid, 408 of panel) are more than half of the 735 over the strip (367)
        List<StoragePanel> panels = With(Box(64, 40));
        StorageColumnLayout top = StorageColumn.Layout(Column, 48f, 1f, panels, 0f, nearby: 0);
        StoragePanelRects bag = top.Panels[top.Pinned];
        Assert.Equal(R(1237, 888 - 12 - 367, 575, 367), bag.Backing);
        Assert.Equal(R(1241, 553, 567, 319), top.PinnedClip); // under the header (513 to 549) and its gap, 4 inside the backing's bottom
        Assert.Equal(360f - 319f, top.PinnedMax);
        Assert.Equal(top.PinnedClip.Top, bag.Cells[0].Top);
        // scrolled to its end: the last row ends at the clip's bottom, the header stays; the pick gets each cell only on its part inside the clip
        StorageColumnLayout end = StorageColumn.Layout(Column, 48f, 1f, panels, 0f, nearby: 0, otherScroll: float.MaxValue);
        StoragePanelRects scrolled = end.Panels[end.Pinned];
        Assert.Equal(end.PinnedMax, end.PinnedScroll);
        Assert.Equal(end.PinnedClip.Bottom, scrolled.Cells[^1].Bottom);
        Assert.Equal(bag.Header, scrolled.Header);
        Assert.Equal(bag.Cells[0].Top - end.PinnedMax, scrolled.Cells[0].Top);
        List<HudDrawn> drawn = DrawnOf(end);
        HudDrawn first = drawn.Single(d => d.EntryId == StorageColumn.CellId(end.Pinned, 0));
        Assert.Equal(R(1241, 553, 48, 48 - 41), first.Rect);
        Assert.All(Enumerable.Range(0, 64).Select(c => StorageColumn.CellId(end.Pinned, c)),
            id => Assert.All(drawn.Where(d => d.EntryId == id), d => Assert.True(Inside(d.Rect, end.PinnedClip), $"{id} at {d.Rect}")));
        // the scroll kept between its ends; the wheel's step is a row
        Assert.Equal(0f, StorageColumn.Layout(Column, 48f, 1f, panels, 0f, nearby: 0, otherScroll: -30f).PinnedScroll);
        Assert.Equal(41f, StorageColumn.Scrolled(0f, 1, 1f, top.PinnedMax));
        // the own storages keep the rest above it
        Assert.Equal(R(1237, 141, 575, 509 - 12 - 141), top.Column);
    }

    [Fact]
    public void OnAShortColumnTheOwnStoragesGiveWayFirstThenThePinnedPanelAndAColumnTooShortForTheStripPlacesNothing()
    {
        List<StoragePanel> panels = With(Box(64, 40), StorageColumnTests.Many());
        // 300 tall: 192 over the strip; half (96) would leave the own storages 84, less than their first header and row (96 and the gap): the
        // pinned panel keeps its header and one row (96) and the own storages get what is left
        StorageColumnLayout l = StorageColumn.Layout(R(1237, 141, 575, 300), 48f, 1f, panels, 0f, nearby: 2);
        Assert.Equal(96f, l.Panels[l.Pinned].Backing.Height);
        Assert.Equal(84f, l.Column.Height);
        Assert.True(l.MaxScroll > 0f);
        // 200 tall: 92 over the strip, all of it the pinned panel's (its header and less than a row, which scrolls); the own storages none
        l = StorageColumn.Layout(R(1237, 141, 575, 200), 48f, 1f, panels, 0f, nearby: 2);
        Assert.Equal(92f, l.Panels[l.Pinned].Backing.Height);
        Assert.Equal(0f, l.Column.Height);
        Assert.Equal(default, l.Line);
        Assert.True(l.PinnedMax > 0f);
        Assert.DoesNotContain(DrawnOf(l), d => StorageColumn.Decode(d.EntryId, out int p, out _) && p != l.Pinned);
        // 100 tall: the strip alone; the pinned panel has no room even for its header
        l = StorageColumn.Layout(R(1237, 141, 575, 100), 48f, 1f, panels, 0f, nearby: 2);
        Assert.NotNull(l.Strip);
        Assert.Equal(-1, l.Pinned);
        Assert.DoesNotContain(DrawnOf(l), d => StorageColumn.Decode(d.EntryId, out _, out _));
        // 95 tall: too short for the strip: nothing
        l = StorageColumn.Layout(R(1237, 141, 575, 95), 48f, 1f, panels, 0f, nearby: 2);
        Assert.Null(l.Strip);
        Assert.Empty(l.Panels);
        Assert.Empty(DrawnOf(l));
    }

    [Fact]
    public void TheStripMovesAlongItsRowWhenItsItemsDoNotFitAndShowsOneEmptyCellWithNothingNearby()
    {
        // twenty items: 1036 of row in 567: the wheel moves it along by a cell, kept between its ends
        StorageColumnLayout l = StorageColumn.Layout(Column, 48f, 1f, With(null), 0f, nearby: 20);
        Assert.Equal(20, l.Strip.Cells.Length);
        Assert.Equal(1036f - 567f, l.Strip.MaxScroll);
        StorageColumnLayout end = StorageColumn.Layout(Column, 48f, 1f, With(null), 0f, nearby: 20, nearbyScroll: float.MaxValue);
        Assert.Equal(end.Strip.MaxScroll, end.Strip.Scroll);
        Assert.Equal(end.Strip.Clip.Right, end.Strip.Cells[^1].Right);
        Assert.Equal(52f, StorageColumn.Scrolled(0f, 1, 1f, end.Strip.MaxScroll));
        // the pick gets only the parts inside the row
        List<HudDrawn> drawn = DrawnOf(end);
        Assert.All(drawn.Where(d => StorageColumn.DecodeStrip(d.EntryId, out int c) && c >= 0), d => Assert.True(Inside(d.Rect, end.Strip.Clip), $"{d.Rect}"));
        Assert.DoesNotContain(drawn, d => d.EntryId == StorageColumn.StripCellId(0)); // moved wholly out
        // nothing nearby: its title and one empty cell, the drop target; at most NearbyItems.MaxItems cells
        StorageColumnLayout empty = StorageColumn.Layout(Column, 48f, 1f, With(null), 0f, nearby: 0);
        Assert.Equal(Row(1241, 932, 1), empty.Strip.Cells);
        Assert.Equal(0f, empty.Strip.MaxScroll);
        Assert.Equal(NearbyItems.MaxItems, StorageColumn.Layout(Column, 48f, 1f, With(null), 0f, nearby: 500).Strip.Cells.Length);
        // with no storage at all the strip still stands (the column is the human HUD's under Tab)
        StorageColumnLayout bare = StorageColumn.Layout(Column, 48f, 1f, new List<StoragePanel>(), 0f, nearby: 0);
        Assert.NotNull(bare.Strip);
        Assert.False(bare.HasOwn);
        Assert.Equal(default, bare.Line);
    }

    [Fact]
    public void TheWheelScrollsThePartOfTheColumnItIsOver()
    {
        StorageColumnLayout l = StorageColumn.Layout(Column, 48f, 1f, With(Box(64, 40), StorageColumnTests.Many()), 0f, nearby: 30);
        Assert.Equal(ColumnArea.Own, StorageColumn.AreaAt(l, 1300, 200));
        Assert.Equal(ColumnArea.Other, StorageColumn.AreaAt(l, 1300, l.Panels[l.Pinned].Backing.Top + 1));
        Assert.Equal(ColumnArea.Nearby, StorageColumn.AreaAt(l, 1300, 950));
        Assert.Equal(ColumnArea.None, StorageColumn.AreaAt(l, 1300, l.Line.Top)); // the gap with the line
        Assert.Equal(ColumnArea.None, StorageColumn.AreaAt(l, 1000, 500)); // left of the column
        Assert.Equal(ColumnArea.None, StorageColumn.AreaAt(StorageColumnLayout.None, 1300, 200));
        // no own storages: their empty area is not the wheel's
        StorageColumnLayout bare = StorageColumn.Layout(Column, 48f, 1f, new List<StoragePanel>(), 0f, nearby: 0);
        Assert.Equal(ColumnArea.None, StorageColumn.AreaAt(bare, 1300, 200));
    }

    public static IEnumerable<object[]> Configs()
    {
        foreach (string own in new[] { "none", "carried", "many" })
            foreach (int other in new[] { -1, 0, 7, 64 })
                foreach (int nearby in new[] { -1, 0, 3, 64, 200 })
                    yield return new object[] { own, other, nearby };
    }

    [Theory]
    [MemberData(nameof(Configs))]
    public void AtAnySizeNothingThrowsNothingOverlapsAndEverythingIsInsideTheColumn(string own, int otherSlots, int nearby)
    {
        List<StoragePanel> ownPanels = own switch { "none" => new List<StoragePanel>(), "many" => StorageColumnTests.Many(), _ => null };
        List<StoragePanel> panels = With(otherSlots < 0 ? null : Box(otherSlots, Math.Min(otherSlots, 5)), ownPanels);
        bool pinnedWanted = otherSlots >= 0;
        IEnumerable<int> widths = Enumerable.Range(0, 21).Select(i => i * 3).Concat(Enumerable.Range(0, 28).Select(i => 61 + i * 23));
        IEnumerable<int> heights = Enumerable.Range(0, 66).Select(i => i * 2).Concat(Enumerable.Range(0, 31).Select(i => 131 + i * 29));
        foreach ((float cell, float unit) in new[] { (48f, 1f), (60f, 1.25f), (144f, 3f) })
        {
            foreach (int width in widths)
            {
                foreach (int height in heights)
                {
                    var column = new HudRect(1237, 141, width, height);
                    float scroll = (width * 7 + height) % 3 == 0 ? float.MaxValue : 37f;
                    StorageColumnLayout l = StorageColumn.Layout(column, cell, unit, panels, scroll, nearby, scroll, scroll);
                    string at = $"{width}x{height} at {unit}";
                    Assert.True(l.Panels.Length == 0 || l.Panels.Length == panels.Count, at);
                    Assert.True(l.Pinned == -1 || pinnedWanted && l.Pinned == panels.Count - 1, at);
                    Assert.InRange(l.Scroll, 0f, l.MaxScroll);
                    Assert.InRange(l.PinnedScroll, 0f, l.PinnedMax);
                    // the parts: the own storages' area (while they have panels), the line, the pinned panel, the strip: whole pixels, inside the
                    // column, none over another
                    var parts = new List<HudRect>();
                    if (l.HasOwn && l.Column.Height > 0f)
                        parts.Add(l.Column);
                    parts.Add(l.Line);
                    if (l.Pinned >= 0)
                        parts.Add(l.Panels[l.Pinned].Backing);
                    if (l.Strip != null)
                    {
                        Assert.True(nearby >= 0, at);
                        parts.Add(l.Strip.Backing);
                        Assert.InRange(l.Strip.Scroll, 0f, l.Strip.MaxScroll);
                        Assert.True(Inside(l.Strip.Title, l.Strip.Backing) && Inside(l.Strip.Clip, l.Strip.Backing) && !Overlaps(l.Strip.Title, l.Strip.Clip), at);
                        Assert.Equal(Math.Max(1, Math.Min(nearby, NearbyItems.MaxItems)), l.Strip.Cells.Length);
                        Assert.Equal(column.Bottom, l.Strip.Backing.Bottom);
                    }
                    for (int i = 0; i < parts.Count; i++)
                    {
                        if (parts[i].Width <= 0f || parts[i].Height <= 0f)
                            continue;
                        Assert.True(Whole(parts[i]) && Inside(parts[i], column), $"{at}: part {parts[i]}");
                        for (int j = 0; j < i; j++)
                            Assert.False(Overlaps(parts[i], parts[j]), $"{at}: {parts[i]} over {parts[j]}");
                    }
                    if (l.Pinned >= 0)
                    {
                        StoragePanelRects p = l.Panels[l.Pinned];
                        Assert.True(Inside(p.Header, p.Backing) && Inside(l.PinnedClip, p.Backing) && !Overlaps(p.Header, l.PinnedClip), at);
                        // at most half the height over the strip, unless that is less than its header and a row (the own storages give way)
                        float usable = (l.Strip?.Backing.Top ?? column.Bottom) - (l.Strip != null ? MathF.Round(UiTokens.StoragePanelGap * unit) : 0f) - column.Top;
                        float headerAndRow = l.PinnedClip.Top - p.Backing.Top + (p.Cells.Length > 0 ? p.Cells[0].Height : 0f) + Math.Max(1f, MathF.Round(UiTokens.StoragePanelPad * unit));
                        Assert.True(p.Backing.Height <= Math.Max(MathF.Floor(usable / 2f), headerAndRow), $"{at}: pinned {p.Backing} over {usable}");
                        Assert.True(p.Backing.Height <= usable, at);
                    }
                    // what the pick gets: whole pixels inside the column; the own panels' parts inside their area, the pinned cells inside its clip,
                    // the strip's cells inside its row
                    foreach (HudDrawn d in DrawnOf(l))
                    {
                        Assert.True(Whole(d.Rect) && Inside(d.Rect, column), $"{at}: {d.EntryId} at {d.Rect}");
                        if (StorageColumn.Decode(d.EntryId, out int panel, out int c))
                            Assert.True(Inside(d.Rect, panel == l.Pinned ? (c >= 0 ? l.PinnedClip : l.Panels[panel].Backing) : l.Column), $"{at}: {d.EntryId} at {d.Rect}");
                        else if (StorageColumn.DecodeStrip(d.EntryId, out c) && c >= 0)
                            Assert.True(Inside(d.Rect, l.Strip.Clip), $"{at}: strip cell at {d.Rect}");
                    }
                    Assert.All(l.Panels.SelectMany(p => p.Cells), c => Assert.True(c.Width >= UiTokens.TabCellMinPixels, $"{at}: {c}"));
                }
            }
        }
    }
}
