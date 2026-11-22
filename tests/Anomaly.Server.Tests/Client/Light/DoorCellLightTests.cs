using Anomaly.Client.Shared.Light;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Light;

/// <summary>
/// The owner's playtest (2026-10-21): a closed airlock between a lit room and a dark maintenance tunnel was lit from the room on its tunnel side. In a closed
/// door's cell a point reads the corners of its own side of the door's plane (<see cref="DoorCellLight"/>, light_grid.gdshaderinc).
/// </summary>
public class DoorCellLightTests
{
    private const byte North = 1, East = 4;

    [Fact]
    public void OnlyAnOpaqueCellWithADoorMovesThePoint()
    {
        Assert.Equal(DoorCellLight.SnapY, DoorCellLight.Code(true, North));
        Assert.Equal(DoorCellLight.SnapX, DoorCellLight.Code(true, East));
        Assert.Equal(DoorCellLight.None, DoorCellLight.Code(false, North)); // open, or a glass door: light passes it in tg
        Assert.Equal(DoorCellLight.None, DoorCellLight.Code(true, 0));      // a wall: its faces lie on the edges already
    }

    [Fact]
    public void APointReadsTheEdgeOnItsSideOfThePlane()
    {
        Assert.Equal((4.5f, 4f), DoorCellLight.SamplePoint(4.5f, 4.45f, DoorCellLight.SnapY));
        Assert.Equal((4.5f, 5f), DoorCellLight.SamplePoint(4.5f, 4.55f, DoorCellLight.SnapY));
        Assert.Equal((4f, 4.3f), DoorCellLight.SamplePoint(4.2f, 4.3f, DoorCellLight.SnapX));
        Assert.Equal((5f, 4.3f), DoorCellLight.SamplePoint(4.8f, 4.3f, DoorCellLight.SnapX));
        Assert.Equal((4.2f, 4.3f), DoorCellLight.SamplePoint(4.2f, 4.3f, DoorCellLight.None));
    }

    [Fact]
    public void AClosedDoorsDarkSideIsNotLitByTheRoomBehindIt()
    {
        // A wall row at y = 5 with a closed door at (5, 5) (its plane along x: face NORTH); a lamp in the north room, the south room dark.
        var cells = new ArrayCells(9, 9);
        cells.Fill(1, 5, 9, 5, true);
        var grid = new LightGrid(cells, 1);
        grid.SetSource(1, 5, 7, new LightState(3f, 1f, 0xFFFFFFFFu, true, 360f, 0, TgLightMath.LightingHeight, 0f, 0f, TgLightMath.ComplexLight));
        var texels = new byte[10 * 10];
        DoorCellLight.Fill(texels, cells, 1, (x, y, _) => x == 5 && y == 5 ? North : (byte)0);
        Assert.Equal(254, texels[5 * 10 + 5]);
        Assert.Equal(0, texels[5 * 10 + 4]); // the wall beside it
        byte code = (byte)((texels[5 * 10 + 5] / 255f * 2f) + 0.5f);

        // The leaf's two faces, a hair south and north of the tile's middle (corner units: tile (5, 5) covers 4 to 5).
        float before = Red(grid, 4.5f, 4.45f);
        (float sx, float sy) = DoorCellLight.SamplePoint(4.5f, 4.45f, code);
        (float nx, float ny) = DoorCellLight.SamplePoint(4.5f, 4.55f, code);
        Assert.True(before > 0.1f, $"before the fix the dark face took {before} of the room's light");
        Assert.Equal(0f, Red(grid, sx, sy));
        Assert.True(Red(grid, nx, ny) > before, "the lit face reads the room's corners");
    }

    // The include's bilinear blend of the four corners around the point.
    private static float Red(LightGrid grid, float px, float py)
    {
        int i = (int)MathF.Floor(px), j = (int)MathF.Floor(py);
        float fx = px - i, fy = py - j;
        float At(int a, int b) => grid.LightAt(a, b, out _).R;
        return (At(i, j) * (1 - fx) + At(i + 1, j) * fx) * (1 - fy) + (At(i, j + 1) * (1 - fx) + At(i + 1, j + 1) * fx) * fy;
    }
}
