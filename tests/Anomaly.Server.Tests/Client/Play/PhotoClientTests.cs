using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// DV685 on the client without the engine: where a photo looks from the own eye (PhotoPose) and the pictures of a browse() page (HtmlImages:
/// tg's photo window, whose source the server's browse_rsc made the picture's resource).
/// </summary>
public class PhotoClientTests
{
    [Fact]
    public void APhotoLooksAtTheTargetTileAndWidensToFitItsFrame()
    {
        // the eye in the middle of tile (5, 5) at eye height, the target two tiles east
        float eyeX = TileSpace.WorldX(5), eyeZ = TileSpace.WorldZ(5), eyeY = TileSpace.WorldY(1) + TileSpace.EyeHeight;
        (float yaw, float pitch, float fov) = PhotoPose.Of(eyeX, eyeY, eyeZ, 0f, 7, 5, 1, 3);
        Assert.Equal(-MathF.PI / 2, yaw, 3); // east is +X: Godot's yaw of -90 degrees
        Assert.InRange(pitch, -0.6f, 0.1f);
        Assert.InRange(fov, PhotoPose.MinFovDegrees, PhotoPose.MaxFovDegrees);

        (_, _, float far) = PhotoPose.Of(eyeX, eyeY, eyeZ, 0f, 5, 15, 1, 3); // ten tiles north: narrower
        Assert.True(far < fov);
        (float northYaw, _, _) = PhotoPose.Of(eyeX, eyeY, eyeZ, 0f, 5, 15, 1, 3);
        Assert.Equal(0f, northYaw, 3);

        (float ownYaw, float ownPitch, _) = PhotoPose.Of(eyeX, eyeY, eyeZ, 1.25f, 5, 5, 1, 3); // the own tile: along the view, down
        Assert.Equal((1.25f, PhotoPose.OwnTilePitch), (ownYaw, ownPitch));

        Assert.Equal((336, 336), PhotoPose.Shrunk(448, 448));
        Assert.Null(PhotoPose.Shrunk(20, 20));
    }

    [Fact]
    public void ABrowsePageIsSplitAtThePicturesTheClientDraws()
    {
        string page = "<html><body style='margin:0'><img src='rsc/0123456789abcdef0123456789abcdef.png' width='480' style='image-rendering:pixelated' />"
            + "<br>Written on the back:<br><i>hi</i><img src='tmp_photo.png'></body></html>";
        List<HtmlPiece> pieces = HtmlImages.Split(page);
        Assert.Equal(3, pieces.Count);
        Assert.Equal("<html><body style='margin:0'>", pieces[0].Html);
        Assert.Equal(new HtmlPiece(null, "rsc/0123456789abcdef0123456789abcdef.png", 480, 0), pieces[1]);
        Assert.Contains("Written on the back", pieces[2].Html);
        Assert.Contains("tmp_photo.png", pieces[2].Html); // a picture the client has no resource for stays in the text, which drops it

        Assert.Single(HtmlImages.Split("no pictures"));
        Assert.Equal(new HtmlPiece(null, "iconops:a|b|0", 0, 32), Assert.Single(HtmlImages.Split("<IMG height=\"32\" src=\"iconops:a|b|0\">")));
    }
}
