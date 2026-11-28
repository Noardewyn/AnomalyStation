using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>ui-overhaul.md section 12, slice 4E: the open-backpack (V) and open-belt (Shift+V) keys bring their storage's panel into view.</summary>
public class OpenStorageKeyTests
{
    private static StoragePanel Panel(StoragePlace place, int cells) => new(100 + (int)place, place, place.ToString(), "0 / 0", false, new StorageItem[cells], new int[cells]);

    [Fact]
    public void TheKeysPanelIsScrolledToTheColumnsTopAndAStorageWithNoPanelGivesNone()
    {
        var panels = new List<StoragePanel> { Panel(StoragePlace.Back, 21), Panel(StoragePlace.SuitStorage, 14), Panel(StoragePlace.Belt, 3) };
        var column = new HudRect(0, 0, 300, 200); // shorter than the three panels: the own storages scroll
        StorageColumnLayout top = StorageColumn.Layout(column, 40, 1f, panels, 0f);
        Assert.True(top.MaxScroll > 0f, "the panels overflow the column");

        Assert.Equal(0f, StorageColumn.ScrollTo(top, panels, StoragePlace.Back));
        float? belt = StorageColumn.ScrollTo(top, panels, StoragePlace.Belt);
        Assert.NotNull(belt);
        StorageColumnLayout scrolled = StorageColumn.Layout(column, 40, 1f, panels, belt.Value);
        // the belt's panel at the column's top, or as far as the scroll goes (its bottom then inside the column)
        Assert.True(scrolled.Panels[2].Backing.Top == column.Top || scrolled.Scroll == scrolled.MaxScroll && scrolled.Panels[2].Backing.Bottom <= column.Bottom);
        Assert.True(scrolled.Panels[2].Backing.Top >= column.Top && scrolled.Panels[2].Backing.Top < column.Bottom, "the belt's panel is in view");
        // from a scrolled place the backpack's key goes back to the top
        Assert.Equal(0f, StorageColumn.ScrollTo(scrolled, panels, StoragePlace.Back));

        // a belt of seven items or fewer has no panel (the bar shows it): nothing to scroll to
        Assert.Null(StorageColumn.ScrollTo(top, panels.Take(2).ToList(), StoragePlace.Belt));
        Assert.Null(StorageColumn.ScrollTo(StorageColumnLayout.None, panels, StoragePlace.Back));
    }
}
