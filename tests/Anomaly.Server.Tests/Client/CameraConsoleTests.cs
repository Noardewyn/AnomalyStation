using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client;

/// <summary>M10 slice D: the camera console window's camera stepping and search (tg's CameraConsole.tsx) and the second view's pose.</summary>
public class CameraConsoleTests
{
    private static UiValue Camera(string name, string reference)
        => UiValue.ObjectOf(new[] { new KeyValuePair<string, UiValue>("name", UiValue.Of(name)), new KeyValuePair<string, UiValue>("ref", UiValue.Of(reference)) });

    [Fact]
    public void Prev_and_next_wrap_around_the_list_as_tgs()
    {
        var cams = new[] { Camera("A", "[0x1]"), Camera("B", "[0x2]"), Camera("C", "[0x3]") };
        Assert.Equal(("[0x3]", "[0x2]"), CameraConsole.PrevNextCamera(cams, cams[0]));
        Assert.Equal(("[0x1]", "[0x3]"), CameraConsole.PrevNextCamera(cams, cams[1]));
        Assert.Equal(("[0x2]", "[0x1]"), CameraConsole.PrevNextCamera(cams, cams[2]));
        Assert.Equal(("[0x3]", "[0x1]"), CameraConsole.PrevNextCamera(cams, Camera("Z", "[0x9]"))); // not in the list
        Assert.Equal(("[0x2]", "[0x2]"), CameraConsole.PrevNextCamera(cams[..2], cams[0]));
        Assert.Equal((null, null), CameraConsole.PrevNextCamera(cams[..1], cams[0]));
        Assert.Equal((null, null), CameraConsole.PrevNextCamera(cams, null));
    }

    [Fact]
    public void Cameras_are_searched_and_sorted_by_name()
    {
        var cams = new[] { Camera("Medbay", "[0x1]"), Camera("", "[0x2]"), Camera("Bridge", "[0x3]"), Camera("Med Storage", "[0x4]") };
        Assert.Equal(new[] { "Bridge", "Med Storage", "Medbay" }, CameraConsole.SelectCameras(cams).Select(c => c.Str("name")));
        Assert.Equal(new[] { "Med Storage", "Medbay" }, CameraConsole.SelectCameras(cams, "med").Select(c => c.Str("name")));
    }

    [Fact]
    public void The_view_hangs_by_the_cameras_wall_and_looks_away_from_it()
    {
        // a camera on the north wall (dir NORTH) of tile 10,20 looks south, from near the north edge of the tile, under the ceiling
        (float x, float y, float z, float yaw, float pitch) = CameraViewPose.Of(10, 20, 1, 1);
        Assert.Equal(TileSpace.WorldX(10), x, 3);
        Assert.Equal(TileSpace.WorldZ(20) - CameraViewPose.WallOffset, z, 3);
        Assert.True(y > TileSpace.LevelHeight / 2 && y < TileSpace.LevelHeight);
        Assert.Equal(TileSpace.DirYaw(2), yaw, 3);
        Assert.True(pitch < 0);
        Assert.Equal((byte)8, CameraViewPose.Opposite(4));
        Assert.Equal((byte)6, CameraViewPose.Opposite(9));
        Assert.Equal(31, CameraViewPose.IdOfRef("[0x1f]"));
        Assert.Equal(0, CameraViewPose.IdOfRef("camera"));
    }
}
