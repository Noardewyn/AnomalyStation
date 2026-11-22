using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>D282: Tab owns cursor mode; a window opened frees the pointer and its closing takes it back, unless Tab was pressed.</summary>
public class MouseOwnerTests
{
    [Fact]
    public void AWindowFreesThePointerAndItsClosingTakesItBack()
    {
        var mouse = new MouseOwner();
        Assert.False(mouse.Free(overlay: false));
        Assert.True(mouse.Windows(true));
        Assert.True(mouse.CursorMode);
        Assert.False(mouse.Windows(true)); // still open: nothing changes
        Assert.True(mouse.Windows(false));
        Assert.False(mouse.CursorMode);
    }

    [Fact]
    public void TabWinsOverTheWindows()
    {
        var mouse = new MouseOwner();
        mouse.Windows(true);
        mouse.Set(false); // Tab while the window is open: back to the view, the window stays
        Assert.False(mouse.Windows(false));
        Assert.False(mouse.CursorMode);

        mouse.Set(true); // cursor mode by Tab, then a window opens and closes: cursor mode stays
        mouse.Windows(true);
        mouse.Windows(false);
        Assert.True(mouse.CursorMode);
    }

    [Fact]
    public void AnOverlayFreesThePointerWithoutCursorMode()
    {
        var mouse = new MouseOwner();
        Assert.True(mouse.Free(overlay: true));
        Assert.False(mouse.CursorMode);
    }
}
