using Anomaly.Client.Shared.Icons;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Integration;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 99 (DV685, docs/ai/specs/protocol.md section 94): the PhotoRequest and PhotoPart messages, and a camera's shot through
/// the real server: the photographer's client is asked for the picture, its PNG comes back in parts and becomes the picture's image, which
/// the clients receive as a runtime resource; a broken image is refused without a strike.
/// </summary>
public class ProtocolV99PhotoTests
{
    [Fact]
    public void APhotoRequestRoundTripsAndOneOutOfRangeIsRefused()
    {
        foreach (var request in new[]
        {
            new PhotoRequestMessage(1, 7, 5, 1, 3, 3, 192, 192),
            new PhotoRequestMessage(uint.MaxValue, -4, 300, 12, Limits.MaxPhotoTiles, 1, Limits.MaxPhotoSide, 1),
        })
        {
            byte[] bytes = Codec.Encode(request);
            Assert.True(bytes.Length <= Limits.MaxPhotoRequestBytes);
            Assert.True(Codec.TryDecode(bytes, out PhotoRequestMessage decoded, out ProtocolError error), error.ToString());
            Assert.Equal(request, decoded);
            Assert.Equal(bytes, Codec.Encode(decoded));
        }
        Assert.Throws<ProtocolException>(() => Codec.Encode(new PhotoRequestMessage(0, 1, 1, 1, 3, 3, 192, 192)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new PhotoRequestMessage(1, 1, 1, 1, 0, 3, 192, 192)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new PhotoRequestMessage(1, 1, 1, 1, 3, 3, Limits.MaxPhotoSide + 1, 192)));
        byte[] good = Codec.Encode(new PhotoRequestMessage(2, 1, 1, 1, 3, 3, 192, 192));
        Assert.False(Codec.TryDecode(good.AsSpan(0, good.Length - 1), out PhotoRequestMessage _, out _));
        Assert.False(Codec.TryDecode(good.Concat(new byte[] { 0 }).ToArray(), out PhotoRequestMessage _, out _));
    }

    [Fact]
    public void APhotosPartsFitTheServersFragmentsAndPutBackTheWholePng()
    {
        var png = new byte[Limits.MaxPhotoBytes];
        new Random(5).NextBytes(png);
        var whole = new List<byte>();
        List<byte[]> parts = Codec.EncodePhotoParts(9, png);
        Assert.Equal(Limits.MaxPhotoBytes / Limits.MaxPhotoPartBytes, parts.Count);
        foreach (byte[] message in parts)
        {
            Assert.True(message.Length <= 8 * 993, "a part fits the 8 fragments a server takes from a client");
            Assert.True(Codec.TryDecode(message, out PhotoPart part, out ProtocolError error), error.ToString());
            Assert.Equal((9u, png.Length, whole.Count), (part.Id, part.Total, part.Offset));
            Assert.Equal(message, Codec.Encode(part));
            whole.AddRange(part.Bytes);
        }
        Assert.Equal(png, whole.ToArray());
        Assert.Throws<ProtocolException>(() => Codec.EncodePhotoParts(1, new byte[Limits.MaxPhotoBytes + 1]));
        Assert.Throws<ProtocolException>(() => Codec.EncodePhotoParts(1, Array.Empty<byte>()));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new PhotoPart(0, 4, 0, new byte[4])));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new PhotoPart(1, 10, 8, new byte[3])));
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.PhotoPart, 1, 4, 2, 3, 1, 2, 3 }, out PhotoPart _, out _)); // past the total
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.PhotoPart, 0, 4, 0, 1, 1 }, out PhotoPart _, out _)); // id 0
        Assert.Equal(512 * 1024, Limits.MaxRuntimeResourceBytes);
    }

    private static byte[] Png(int width, int height)
    {
        var rgba = new uint[width * height];
        for (int i = 0; i < rgba.Length; i++)
            rgba[i] = 0x336699FFu ^ (uint)(i * 2654435761u & 0xFFFFFF00u);
        return DmPng.Encode(width, height, rgba);
    }

    [Fact]
    public void A_cameras_shot_is_rendered_by_the_photographers_client_and_its_png_becomes_the_picture()
    {
        using var w = new RealHeadlessWorld(config: new ServerConfig { Seed = 7, RoomWidth = 24, RoomHeight = 24, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        w.Run(20);
        mob_living alices = w.MobOf(alice);
        obj_item_camera camera;
        turf target;
        using (w.World.Enter())
        {
            camera = Dm.@new<obj_item_camera>(alices.loc);
            camera.can_customise = Dm.FALSE;
            Waiting.NoSleep(alices.put_in_active_hand(camera));
            target = Builtins.locate(alices.x + 2, alices.y, alices.z) as turf;
        }
        using (w.World.Enter())
            camera.attempt_picture(target, alices);
        w.Run(20);
        Assert.True(alice.Client.TryTakePhotoRequest(out PhotoRequestMessage request));
        Assert.Equal(((int)target.x, (int)target.y, (int)target.z), (request.X, request.Y, request.Z));
        Assert.Equal((3, 3, 192, 192), ((int)request.TilesX, (int)request.TilesY, (int)request.Width, (int)request.Height));
        obj_item_photo photo = null;
        using (w.World.Enter())
        {
            photo = Dm.each<obj_item_photo>().Single();
            Assert.Null(photo.picture.picture_image);
        }

        byte[] png = Png(192, 192);
        alice.Client.SendPhoto(request.Id, png);
        w.Run(20);
        Session session = w.Host.Sessions.Single();
        Assert.Equal((1, 0, 0), (session.PhotosTaken, session.PhotosRefused, session.Strikes));
        string path;
        using (w.World.Enter())
        {
            path = (photo.picture.picture_image.dm_file as DmResource)?.Path;
            Assert.NotNull(path);
            Assert.Contains(path, Dm.str((photo.icon as icon)?.dm_resource()));
        }
        Assert.True(RuntimeImages.TryRead(path, out byte[] received), "the client was sent the photo's image");
        Assert.Equal(png, received);

        // a second shot whose answer is no PNG: refused and logged, no strike, the picture blank
        using (w.World.Enter())
        {
            camera.on = Dm.TRUE;
            camera.blending = Dm.FALSE;
            camera.attempt_picture(target, alices);
        }
        w.Run(20);
        Assert.True(alice.Client.TryTakePhotoRequest(out PhotoRequestMessage second));
        alice.Client.SendPhoto(second.Id, new byte[5000]);
        w.Run(20);
        Assert.Equal((1, 1, 0), (session.PhotosTaken, session.PhotosRefused, session.Strikes));
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}
