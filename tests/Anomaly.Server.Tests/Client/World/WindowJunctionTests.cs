using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// M5 slice 5 (D142): the client's look of construction states. Between two full-tile windows the arm follows tg's smoothing junction
/// in the icon state (an unanchored window is not smoothed with, icon_smoothing.dm:201), the walls, doors and grilles keep D121's rule;
/// a broken grille ("brokengrille", grille.dm:53) is tg's sprite standing in its tile, not a joined piece; the planner's index forgets
/// a window or grille that is gone.
/// </summary>
public sealed class WindowJunctionTests
{
    private const string RWindow = "/obj/structure/window/reinforced/fulltile";

    private static PlanEntity Entity(string path, int x, int y, string state)
        => new(1, path, x, y, 1, 2, new TableRef(1, false), 0, 0, default, state);

    // A window at (5,5) with full-tile windows north (5,6) and south (5,4) and a wall east (6,5).
    private static PlanProbe Probe() => new()
    {
        IsWall = (x, y, z) => (x, y) == (6, 5),
        Connects = (x, y, z) => (x, y) is (6, 5) or (5, 6) or (5, 4),
        IsFullWindow = (x, y, z) => (x, y) is (5, 6) or (5, 4),
    };

    private static byte Arms(string state)
    {
        Assert.True(ChunkPlanner.TryPlace(Entity(RWindow, 5, 5, state), Probe(), out Placed placed));
        Assert.Equal(RenderKind.FullWindow, placed.Shape);
        return placed.Joints;
    }

    [Fact]
    public void TheJunctionDecidesTheArmsBetweenWindowsAndTheWallStillJoins()
    {
        Assert.True(WindowJunction.TryParse("reinforced_window-3", out int j));
        Assert.Equal(3, j);
        Assert.False(WindowJunction.TryParse("rwindow", out _));
        Assert.False(WindowJunction.TryParse(null, out _));

        // smoothed with both windows (north 1, south 2): both arms, and the wall's
        Assert.Equal(Joints.North | Joints.South | Joints.East, Arms("reinforced_window-3"));
        // the south window unanchored: tg's junction lost its bit, the client its arm
        Assert.Equal(Joints.North | Joints.East, Arms("reinforced_window-1"));
        // no junction in the state (a picture without smoothing): D121's rule alone
        Assert.Equal(Joints.North | Joints.South | Joints.East, Arms("rwindow"));
    }

    [Fact]
    public void ABrokenGrilleIsTgsSpriteAndNotJoined()
    {
        Assert.Equal(RenderKind.Grille, PropKinds.Classify("/obj/structure/grille", "grille"));
        Assert.Equal(RenderKind.Billboard, PropKinds.Classify("/obj/structure/grille", "brokengrille"));
        Assert.Equal(RenderKind.Billboard, PropKinds.Classify("/obj/structure/grille/broken", "brokengrille"));
        Assert.True(ChunkPlanner.TryPlace(Entity("/obj/structure/grille", 5, 5, "brokengrille"), Probe(), out Placed placed));
        Assert.NotEqual(PlacedKind.Piece, placed.Kind);

        var index = new PlanIndex();
        index.Add(Entity("/obj/structure/grille", 5, 5, "brokengrille"));
        Assert.Empty(index.Grilles);
        index.Add(Entity("/obj/structure/grille", 5, 5, "grille"));
        index.Add(Entity(RWindow, 5, 5, "reinforced_window-0"));
        Assert.Contains((5, 5, 1), index.Grilles);
        Assert.Contains((5, 5, 1), index.Windows);
        index.Forget((5, 5, 1), windowLeft: false, grilleLeft: true);
        Assert.Empty(index.Windows);
        Assert.Contains((5, 5, 1), index.Grilles);
    }
}
