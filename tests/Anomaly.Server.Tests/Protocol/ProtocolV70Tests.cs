using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol versions 70 and 71 (D320, docs/ai/specs/protocol.md sections 71 and 72): the MouseWheel action (a mouse action whose button is the
/// turn) and the BuildPreview message with its tile round-trip; a preview without an icon, past tg's dirs or off the map is refused. The
/// ghost shows only while the crosshair aims at the preview's tile (a turf or a thing on it).
/// </summary>
public class ProtocolV70Tests
{
    [Fact]
    public void TheWheelAndTheBuildPreviewRoundTripAndBadOnesAreRefused()
    {
        Assert.True(ProtocolVersion.Current >= 71);
        var wheel = new ActionMessage(7, ActionKind.MouseWheel, 0, ClickButton.Right, ClickModifiers.Shift, null) { TargetKind = TargetKind.Turf, TurfX = 4, TurfY = 5, TurfZ = 1 };
        var buffer = new byte[256];
        int n = Codec.Encode(wheel, buffer);
        Assert.True(Codec.TryDecode(buffer.AsSpan(0, n), out ActionMessage back, out ProtocolError error), error.ToString());
        Assert.Equal(wheel, back);

        foreach (BuildPreviewMessage m in new[] { BuildPreviewMessage.None, new BuildPreviewMessage("icons/obj/pipes_n_cables/pipe_item.dmi", "pump", 4, 0xFF0000FF, 0, -5, 200, 31, 2) })
        {
            Assert.True(Codec.TryDecode(Codec.Encode(m), out BuildPreviewMessage got, out error), error.ToString());
            Assert.Equal(m, got);
        }
        var good = new BuildPreviewMessage("a.dmi", "s", 2, 0xFFFFFFFF, 0, 0, 10, 10, 1);
        foreach (BuildPreviewMessage bad in new[] { good with { Dir = 11 }, good with { X = 0 }, good with { Y = Limits.MaxMapSize + 1 }, good with { Z = Limits.MaxMapLevels + 1 } })
            Assert.Throws<ProtocolException>(() => Codec.Encode(bad));
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.BuildPreview, 1, 0, 0, 2, 0, 0, 0, 0, 0, 0, 1, 1, 1 }, out BuildPreviewMessage _, out _)); // an empty icon
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.BuildPreview, 2 }, out BuildPreviewMessage _, out _));
    }

    [Fact]
    public void TheGhostShowsOnlyWhileTheCrosshairIsOnItsTile()
    {
        var preview = new BuildPreviewMessage("a.dmi", "s", 2, 0xFFFFFFFF, 0, 0, 11, 9, 1);
        PickHit Hit(TargetKind kind, int x, int y, int z) => new(PickSurface.Floor, kind, 0, x, y, z, 1, 0, 0, 0, 0, 0);
        Assert.True(BuildGhostPlacement.Shows(preview, Hit(TargetKind.Turf, 11, 9, 1)));
        Assert.True(BuildGhostPlacement.Shows(preview, Hit(TargetKind.Entity, 11, 9, 1))); // a thing on it
        Assert.False(BuildGhostPlacement.Shows(preview, Hit(TargetKind.Turf, 12, 9, 1))); // the crosshair moved on: the server has not answered yet
        Assert.False(BuildGhostPlacement.Shows(preview, Hit(TargetKind.Turf, 11, 9, 2)));
        Assert.False(BuildGhostPlacement.Shows(preview, Hit(TargetKind.Ceiling, 11, 9, 1)));
        Assert.False(BuildGhostPlacement.Shows(preview, default));
        Assert.False(BuildGhostPlacement.Shows(BuildPreviewMessage.None, Hit(TargetKind.Turf, 11, 9, 1)));
    }
}
