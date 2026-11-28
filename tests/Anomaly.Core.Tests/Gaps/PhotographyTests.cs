using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using System.Text.Json;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The camera's pictures, the photo booths and the picture frames: a camera's shot asks the photographer's client for the picture's image
/// (DV685), which becomes the picture's image, the photo's small picture and the window the photo shows, and a late, foreign or broken image
/// is refused (PhotoSeam, camera.dm, _pictures.dm, photo.dm); a booth opens and closes its curtain, takes its occupant's record photos after the
/// countdown when someone with access asks (or its remote button does), and refuses a stranger (photobooth.dm); a frame takes a photo,
/// shows it, gives it up to wirecutters and comes off the wall as a frame that keeps it; the bar's portrait shows one of four
/// pictures until a photo is slipped in (frame.dm).
/// </summary>
public class PhotographyTests
{
    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(16, 11, seed: seed);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        r.In(() => (get_area(locate(5, 5, 1)) as area).requires_power = FALSE);
        r.Step(Second);
        return (r, bob);
    }

    private static datum_record_crew Record(AudioRoom r, mob_living_carbon_human who) => r.In(() =>
    {
        who.mind.set_assigned_role(SSjob.get_job_type(typeof(datum_job_assistant)) as datum_job);
        GLOB.manifest.inject(who);
        return Dm.each<datum_record_crew>(GLOB.manifest.general).Single(x => Dm.eq(x.name, who.real_name));
    });

    [Fact]
    public void TheBoothShutsItsCurtainOnItsOccupantAndRetakesTheRecordPhotosForSomeoneWithAccess()
    {
        var (r, bob) = Room(15301);
        var booth = Place<obj_machinery_photobooth>(r, 6, 5);
        Assert.Equal("booth_open", r.In(() => Dm.str(booth.icon_state)));
        mob_living_carbon_human carol = r.Join(6, 5, "Carol");
        datum_record_crew record = Record(r, carol);
        object before = r.In(() => record.character_appearance);

        r.Command(() => Waiting.Detach(booth.interact(bob)));
        r.Step(2);
        r.In(() =>
        {
            Assert.False(Dm.truthy(booth.state_open));
            Assert.Same(carol, booth.occupant);
            Assert.Equal("booth_closed", Dm.str(booth.icon_state));
        });

        // a stranger is refused
        r.Command(() => Waiting.Detach(booth.attack_hand_secondary(bob)));
        r.Step(2);
        Assert.False(r.In(() => Dm.truthy(booth.taking_pictures)));

        r.In(() =>
        {
            var card = Dm.@new<obj_item_card_id_advanced>(bob.loc);
            Waiting.NoSleep(SSid_access.apply_trim_to_card(card, typeof(datum_id_trim_job_security_officer)));
            Waiting.NoSleep(bob.put_in_active_hand(card)); // allowed() reads the active hand's card
        });
        r.Command(() => Waiting.Detach(booth.attack_hand_secondary(bob)));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(booth.taking_pictures)));
        r.Step(9 * Second);
        r.In(() =>
        {
            Assert.False(Dm.truthy(booth.taking_pictures));
            Assert.NotSame(before, record.character_appearance);
            Assert.True(Dm.truthy(booth.state_open));
            Assert.Null(booth.occupant);
        });
    }

    [Fact]
    public void TheBoothsRemoteButtonStartsTheCountdown()
    {
        var (r, bob) = Room(15302);
        var booth = Place<obj_machinery_photobooth>(r, 6, 5);
        var button = Place<obj_machinery_button_photobooth>(r, 4, 5);
        // a mapped button makes its assembly (buttons.dm:75, mapload only): here it is put in as the map would
        var controller = r.In(() =>
        {
            button.device_instance = Dm.@new<obj_item_assembly_control_photobooth_control>(button);
            button.setup_device();
            var control = button.device_instance as obj_item_assembly_control_photobooth_control;
            control.find_machine();
            return control;
        });
        Assert.NotNull(controller);
        Assert.Same(booth, r.In(() => controller.booth_machine_ref?.resolve()));
        mob_living_carbon_human carol = r.Join(6, 5, "Carol");
        Record(r, carol);
        r.Command(() => Waiting.Detach(booth.interact(bob)));
        r.Step(2);
        r.Command(() => Waiting.Detach(controller.activate(bob)));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(booth.taking_pictures)));
    }

    [Fact]
    public void AFrameTakesAPhotoAndGivesItUpToWirecuttersOrComesOffTheWallWithIt()
    {
        var (r, bob) = Room(15303);
        var frame = Place<obj_structure_sign_picture_frame>(r, 5, 6);
        var photo = r.In(() => Dm.@new<obj_item_photo>(bob.loc));
        r.In(() =>
        {
            Waiting.NoSleep(bob.put_in_active_hand(photo));
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(frame.item_interaction(bob, photo)));
            Assert.Same(photo, frame.framed);
            Assert.Same(frame, photo.loc);
            var second = Dm.@new<obj_item_photo>(bob.loc);
            Assert.Equal(ITEM_INTERACT_BLOCKING, Waiting.NoSleep(frame.item_interaction(bob, second)));

            var cutters = Dm.@new<obj_item_wirecutters>(bob.loc);
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(frame.wirecutter_act(bob, cutters)));
            Assert.Null(frame.framed);
            Assert.NotSame(frame, photo.loc);
            Waiting.NoSleep(frame.item_interaction(bob, second));
        });
        r.Command(() => Waiting.Detach(frame.screwdriver_act(bob, r.In(() => Dm.@new<obj_item_screwdriver>(bob.loc)))));
        r.Step(5 * Second);
        r.In(() =>
        {
            Assert.True(QDELETED(frame));
            var wallframe = Dm.locate<obj_item_wallframe_picture>(locate(5, 6, 1)) ?? Dm.each<obj_item_wallframe_picture>().FirstOrDefault();
            Assert.NotNull(wallframe);
            Assert.NotNull(wallframe.displayed);
        });
    }

    [Fact]
    public void TheBarPortraitShowsOneOfFourPicturesUntilAPhotoIsSlippedIn()
    {
        var (r, bob) = Room(15304);
        var portrait = Place<obj_structure_sign_picture_frame_portrait_bar>(r, 5, 6);
        string state = r.In(() => Dm.str(portrait.icon_state));
        Assert.Contains(state, new[] { "frame-monkey", "frame-fruit", "frame-rat", "frame-ratvar" });
        string portraitName = r.In(() => Dm.str(portrait.name));
        r.In(() =>
        {
            var photo = Dm.@new<obj_item_photo>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(photo));
            Waiting.NoSleep(portrait.item_interaction(bob, photo));
            Assert.Equal("frame-overlay", Dm.str(portrait.icon_state));
            Assert.Equal("Every time you look it makes you laugh.", Dm.str(portrait.desc));
            Assert.False(Dm.truthy(portrait.can_decon));
            var cutters = Dm.@new<obj_item_wirecutters>(bob.loc);
            Waiting.NoSleep(portrait.wirecutter_act(bob, cutters));
            Assert.Equal(state, Dm.str(portrait.icon_state));
            Assert.Equal(portraitName, Dm.str(portrait.name));
        });
    }

    // a grey PNG of that size, as a client's render
    private static byte[] Png(int width, int height)
    {
        var rgba = new uint[width * height];
        for (int i = 0; i < rgba.Length; i++)
            rgba[i] = 0x808080FFu + (uint)(i % 7 << 24);
        return DmPng.Encode(width, height, rgba);
    }

    private static (obj_item_photo Photo, PhotoRequest Request) Shoot(AudioRoom r, mob_living_carbon_human who, int x, int y)
    {
        var camera = r.In(() => Dm.@new<obj_item_camera>(who.loc));
        var before = r.In(() => Dm.each<obj_item_photo>().ToList());
        r.In(() =>
        {
            camera.can_customise = FALSE;
            Waiting.NoSleep(who.put_in_active_hand(camera));
            who.client.dm_photo_requests.Clear();
        });
        r.Command(() => camera.attempt_picture(locate(x, y, 1), who));
        r.Step(Second);
        return r.In(() =>
        {
            Assert.Single(who.client.dm_photo_requests);
            var photo = Dm.each<obj_item_photo>().Single(p => !before.Contains(p));
            return (photo, who.client.dm_photo_requests[0]);
        });
    }

    [Fact]
    public void ACamerasShotAsksThePhotographersClientAndItsImageBecomesThePicture()
    {
        var (r, bob) = Room(15305);
        var (photo, request) = Shoot(r, bob, 7, 5);
        Assert.Equal((7, 5, 1), (request.X, request.Y, request.Z));
        Assert.Equal((3, 3, 192, 192), (request.TilesX, request.TilesY, request.Width, request.Height)); // tg's 3 x 3 m at 64 pixels a tile
        r.In(() =>
        {
            Assert.Null(photo.picture.picture_image); // the photo is printed at once; its image comes later
            Assert.Contains("This is a photo of an area of 3 meters by 3 meters.", photo.picture.picture_desc);
            bob.client.dm_tgui_output.Clear();
            photo.show(bob);
            Assert.DoesNotContain(bob.client.dm_tgui_output, o => o.Kind == TguiOutputKind.Browse);

            byte[] png = Png(192, 192);
            Assert.Null(PhotoSeam.Develop(bob.client, request.Id, png));
            string path = Dm.str((photo.picture.picture_image.dm_file as DmResource)?.Path);
            Assert.StartsWith("rsc/", path);
            Assert.Same(png, DmWorldState.Current.RuntimeResources[path]);
            // tg's small picture: the photo sprite with the image scaled to 8 x 8 pixels on it, which the photo item now shows
            string small = photo.picture.picture_icon.dm_resource();
            Assert.StartsWith("iconops:icons/obj/art/camera.dmi|photo|", small);
            Assert.Contains(path, small);
            Assert.Equal(small, Dm.str((photo.icon as icon)?.dm_resource()));

            photo.show(bob);
            TguiOutput page = bob.client.dm_tgui_output.Last(o => o.Kind == TguiOutputKind.Browse);
            Assert.Equal("photo_showing", page.WindowId);
            using JsonDocument doc = JsonDocument.Parse(page.Json);
            Assert.Contains("<img src='" + path + "' width='480'", doc.RootElement.GetProperty("html").GetString());
            Assert.Equal(480, doc.RootElement.GetProperty("height").GetInt32());

            // the same answer again is refused, as is one for no request
            Assert.NotNull(PhotoSeam.Develop(bob.client, request.Id, png));
            Assert.NotNull(PhotoSeam.Develop(bob.client, 999, png));
        });
    }

    [Fact]
    public void AnImageTooLargeBrokenFromAnotherClientOrTooLateIsRefusedAndThePictureStaysBlank()
    {
        var (r, bob) = Room(15306);
        mob_living_carbon_human carol = r.Join(9, 5, "Carol");
        var (photo, request) = Shoot(r, bob, 7, 5);
        r.In(() =>
        {
            Assert.NotNull(PhotoSeam.Develop(carol.client, request.Id, Png(192, 192))); // not Carol's request: it stays Bob's
            Assert.NotNull(PhotoSeam.Develop(bob.client, request.Id, Png(256, 192)));
            Assert.Null(photo.picture.picture_image);
        });
        (photo, request) = Shoot(r, bob, 6, 6);
        r.In(() =>
        {
            byte[] whole = Png(64, 64);
            byte[] broken = whole[..(whole.Length - 20)]; // cut short
            Assert.NotNull(PhotoSeam.Develop(bob.client, request.Id, broken));
            Assert.Null(photo.picture.picture_image);
        });
        (photo, request) = Shoot(r, bob, 7, 6);
        r.Step(11 * Second);
        r.In(() =>
        {
            Assert.NotNull(PhotoSeam.Develop(bob.client, request.Id, Png(192, 192)));
            Assert.Null(photo.picture.picture_image);
        });
        // a smaller image than asked is taken (a client shrinks one that does not fit the byte cap)
        (photo, request) = Shoot(r, bob, 8, 6);
        r.In(() => Assert.Null(PhotoSeam.Develop(bob.client, request.Id, Png(144, 144))));
    }

    // a 1 x 1 PNG whose header claims another size (the CRC is left stale: the decoder reads none): its image data is a few bytes
    private static byte[] ClaimedPng(uint width, uint height)
    {
        byte[] png = DmPng.Encode(1, 1, new[] { 0x808080FFu });
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16), width);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20), height);
        return png;
    }

    [Fact]
    public void APhotoPngWhoseHeaderClaimsAWrongSizeIsRefusedBeforeItIsDecoded()
    {
        var (r, bob) = Room(15308);
        r.In(() =>
        {
            datum_picture picture = Dm.@new<datum_picture>();
            // Develop answering a fresh 3 x 3 tile (192 x 192 pixel) request, with the bytes this thread allocated meanwhile: a decode of the
            // claimed size would allocate its rows (900 MB for 15000 x 15000), so a refusal under a megabyte was never decoded
            (string Refused, long Allocated) Answer(byte[] png)
            {
                uint id = PhotoSeam.Request(bob, picture, locate(7, 5, 1) as turf, 3, 3);
                Assert.NotEqual(0u, id);
                long before = GC.GetAllocatedBytesForCurrentThread();
                string refused = PhotoSeam.Develop(bob.client, id, png);
                return (refused, GC.GetAllocatedBytesForCurrentThread() - before);
            }
            foreach ((uint w, uint h, string shown) in new[]
            {
                (15000u, 15000u, "15000 x 15000"), (0u, 0u, "0 x 0"), (193u, 192u, "193 x 192"), (192u, 193u, "192 x 193"),
                ((uint)int.MaxValue, (uint)int.MaxValue, "2147483647 x 2147483647"), (0x80000000u, 0x80000000u, "-2147483648 x -2147483648"),
            })
            {
                (string refused, long allocated) = Answer(ClaimedPng(w, h));
                Assert.Equal("it is " + shown + " pixels, not 16 to 192 x 192", refused);
                Assert.True(allocated < 1024 * 1024, shown + ": " + allocated + " bytes allocated");
            }
            // a real 1 x 1 image is below the smallest side taken
            (string small, long smallAllocated) = Answer(Png(1, 1));
            Assert.Equal("it is 1 x 1 pixels, not 16 to 192 x 192", small);
            Assert.True(smallAllocated < 1024 * 1024);
            // a header cut at 23 bytes is no PNG
            (string cut, long cutAllocated) = Answer(Png(192, 192)[..23]);
            Assert.StartsWith("it is not a PNG the server reads (", cut);
            Assert.True(cutAllocated < 1024 * 1024);
            Assert.Null(picture.picture_image);
        });
        // exactly the requested size with real data is taken, as before
        var (photo, request) = Shoot(r, bob, 8, 6);
        r.In(() =>
        {
            Assert.Null(PhotoSeam.Develop(bob.client, request.Id, Png(request.Width, request.Height)));
            Assert.NotNull(photo.picture.picture_image);
        });
    }

    [Fact]
    public void APhotocopiersGreyCopyOfAPhotoKeepsItsImageInGrey()
    {
        var (r, bob) = Room(15307);
        var (photo, request) = Shoot(r, bob, 7, 5);
        r.In(() =>
        {
            Assert.Null(PhotoSeam.Develop(bob.client, request.Id, Png(192, 192)));
            var copy = (datum_picture)photo.picture.Copy(TRUE);
            Assert.NotSame(photo.picture.picture_image, copy.picture_image);
            Assert.Equal(photo.picture.picture_image.dm_file, copy.picture_image.dm_file);
            Assert.StartsWith("iconops:", copy.picture_image.dm_resource()); // the grey MapColors on the copy
            Assert.Empty(photo.picture.picture_image.dm_ops);
        });
    }
}
