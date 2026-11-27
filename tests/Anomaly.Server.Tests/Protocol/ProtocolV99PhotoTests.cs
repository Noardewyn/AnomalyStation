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

    private static RealHeadlessWorld PhotoWorld() => new(config: new ServerConfig { Seed = 7, RoomWidth = 24, RoomHeight = 24, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f });

    // a camera in the player's active hand, and the tile two to its east
    private static (obj_item_camera Camera, turf Target) Camera(RealHeadlessWorld w, RealHeadlessWorld.Player who)
    {
        mob_living mob = w.MobOf(who);
        using (w.World.Enter())
        {
            var camera = Dm.@new<obj_item_camera>(mob.loc);
            camera.can_customise = Dm.FALSE;
            Waiting.NoSleep(mob.put_in_active_hand(camera));
            return (camera, Builtins.locate(mob.x + 2, mob.y, mob.z) as turf);
        }
    }

    // the camera takes a shot and the server's request reaches the photographer's client
    private static PhotoRequestMessage Shoot(RealHeadlessWorld w, RealHeadlessWorld.Player who, obj_item_camera camera, turf target)
    {
        using (w.World.Enter())
        {
            Assert.True(Dm.truthy(camera.on), "the camera is ready");
            camera.attempt_picture(target, w.MobOf(who));
        }
        w.Run(20);
        Assert.True(who.Client.TryTakePhotoRequest(out PhotoRequestMessage request));
        return request;
    }

    private static int RefusalLines(RealHeadlessWorld w, string ckey)
        => w.Err.ToString().Split('\n').Count(l => l.Contains(" of " + ckey + " (") && l.Contains("was refused"));

    [Fact]
    public void A_flood_of_photos_answering_no_request_is_kicked_on_the_third_and_logged_once()
    {
        using RealHeadlessWorld w = PhotoWorld();
        RealHeadlessWorld.Player mallory = w.Join("Mallory");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        w.Run(20);
        Session flooder = w.Host.Sessions.Single(s => s.Ckey == "mallory");
        Session honest = w.Host.Sessions.Single(s => s.Ckey == "bob");
        for (uint i = 1; i <= 50; i++)
            mallory.Client.SendPhoto(i, new byte[] { 1 }); // each a whole one-byte "photo": one part, offset 0, total 1
        w.Run(20);
        Assert.Equal(DisconnectReason.ProtocolViolation, flooder.CloseReason);
        Assert.Equal((3, 3), (flooder.Strikes, flooder.PhotosRefused)); // the session is closed on the third strike: the rest are dropped
        Assert.Equal(1, RefusalLines(w, "mallory"));
        Assert.Contains("answers no request of this client", w.Err.ToString());

        // the server runs on and another client's real photo arrives
        var (camera, target) = Camera(w, bob);
        PhotoRequestMessage request = Shoot(w, bob, camera, target);
        bob.Client.SendPhoto(request.Id, Png(request.Width, request.Height));
        w.Run(20);
        Assert.Equal((1, 0, 0), (honest.PhotosTaken, honest.PhotosRefused, honest.Strikes));
        Assert.Equal(SessionState.Joined, honest.State);
        using (w.World.Enter())
            Assert.NotNull(Dm.each<obj_item_photo>().Single().picture.picture_image);
    }

    [Fact]
    public void A_photographer_shooting_at_the_cameras_cooldown_is_never_struck()
    {
        using RealHeadlessWorld w = PhotoWorld();
        RealHeadlessWorld.Player alice = w.Join("Alice");
        w.Run(20);
        Session session = w.Host.Sessions.Single();
        var (camera, target) = Camera(w, alice);
        Assert.Equal(64f, camera.cooldown); // tg's 6.4 s: 128 ticks, each shot here 20 + 120 ticks apart
        for (int shot = 1; shot <= 4; shot++)
        {
            PhotoRequestMessage request = Shoot(w, alice, camera, target);
            alice.Client.SendPhoto(request.Id, Png(request.Width, request.Height));
            w.Run(120);
            Assert.Equal((shot, 0, 0), (session.PhotosTaken, session.PhotosRefused, session.Strikes));
        }
        Assert.Equal(SessionState.Joined, session.State);
        Assert.Equal(0, RefusalLines(w, "alice"));
    }

    // that many requests for the player's client at once, as cameras with short cooldowns make them (the orbie's is a second), delivered
    private static List<PhotoRequestMessage> Requests(RealHeadlessWorld w, RealHeadlessWorld.Player who, int count)
    {
        using (w.World.Enter())
        {
            mob_living mob = w.MobOf(who);
            var target = Builtins.locate(mob.x + 2, mob.y, mob.z) as turf;
            for (int i = 0; i < count; i++)
                Assert.NotEqual(0u, PhotoSeam.Request(mob, Dm.@new<datum_picture>(), target, 3, 3));
        }
        w.Run(20);
        var requests = new List<PhotoRequestMessage>();
        while (who.Client.TryTakePhotoRequest(out PhotoRequestMessage request))
            requests.Add(request);
        Assert.Equal(count, requests.Count);
        return requests;
    }

    private static int Expired(RealHeadlessWorld w)
    {
        using (w.World.Enter())
            return PhotoSeam.ExpiredCount(w.World);
    }

    [Fact]
    public void A_stalled_clients_late_answers_to_its_own_requests_are_refused_once_each_without_a_strike()
    {
        using RealHeadlessWorld w = PhotoWorld();
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        w.Run(20);
        Session alices = w.Host.Sessions.Single(s => s.Ckey == "alice");
        Session bobs = w.Host.Sessions.Single(s => s.Ckey == "bob");
        List<PhotoRequestMessage> requests = Requests(w, alice, 4);
        w.Run((int)PhotoSeam.DeadlineTicks + 10); // the client stalls past every deadline, then sends its queued answers together
        for (int i = 0; i < 3; i++)
            alice.Client.SendPhoto(requests[i].Id, Png(requests[i].Width, requests[i].Height));
        w.Run(20);
        Assert.Equal((0, 3, 0), (alices.PhotosTaken, alices.PhotosRefused, alices.Strikes));
        Assert.Equal(SessionState.Joined, alices.State);
        Assert.Equal(1, RefusalLines(w, "alice"));
        Assert.Contains(PhotoSeam.Late, w.Err.ToString());
        Assert.Equal(1, Expired(w)); // the fourth, still unanswered

        // another client's expired request is no answer of Bob's: a strike, and it stays Alice's to answer
        bob.Client.SendPhoto(requests[3].Id, Png(192, 192));
        w.Run(20);
        Assert.Equal((1, 1), (bobs.Strikes, bobs.PhotosRefused));
        Assert.Equal(1, Expired(w));

        // a second answer to a forgiven request is a strike, logged once as the first answer to nothing
        alice.Client.SendPhoto(requests[0].Id, Png(192, 192));
        w.Run(20);
        Assert.Equal((4, 1), (alices.PhotosRefused, alices.Strikes));
        Assert.Equal(SessionState.Joined, alices.State);
        Assert.Equal(2, RefusalLines(w, "alice"));
    }

    [Fact]
    public void An_expired_request_is_forgotten_after_the_grace_and_a_flood_of_answers_to_nothing_is_never_remembered()
    {
        using RealHeadlessWorld w = PhotoWorld();
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player mallory = w.Join("Mallory");
        w.Run(20);
        Session alices = w.Host.Sessions.Single(s => s.Ckey == "alice");
        PhotoRequestMessage request = Requests(w, alice, 1).Single();
        w.Run((int)(PhotoSeam.DeadlineTicks + PhotoSeam.GraceTicks) + 20);
        alice.Client.SendPhoto(request.Id, Png(request.Width, request.Height));
        w.Run(20);
        Assert.Equal((0, 1, 1), (alices.PhotosTaken, alices.PhotosRefused, alices.Strikes));
        Assert.Equal(SessionState.Joined, alices.State);
        Assert.Equal(0, Expired(w));

        // answers to ids never issued add nothing to what is remembered
        for (uint i = 1000; i < 1050; i++)
            mallory.Client.SendPhoto(i, new byte[] { 1 });
        w.Run(20);
        Assert.Equal(0, Expired(w));
        using (w.World.Enter())
            Assert.Equal(0, PhotoSeam.PendingCount(w.World));
    }
}
