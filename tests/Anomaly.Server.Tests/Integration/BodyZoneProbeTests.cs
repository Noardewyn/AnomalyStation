using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Package T (docs/ai/specs/ui-overhaul.md section 8), slices T0 and T1: what aiming by look finds on tg's real human pictures, through a
/// real headless world and tg's converted icons, and the body-only hit mask of a mob built from body parts. The zone grids are printed
/// so a run's log shows them (H head, C chest, l/r left/right arm, L/R left/right leg, '.' none; '#' a drawn pixel no body part covers,
/// which the mask leaves out).
/// </summary>
public class BodyZoneProbeTests(ITestOutputHelper output)
{
    private static readonly byte[] Frames = { 1, 2, 4, 8 };

    [Fact]
    public void A_naked_humans_limbs_are_found_in_each_frame()
    {
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return; // no tg icons here
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        w.Run(10);
        using (w.World.Enter())
        {
            // a joined player is dressed (tg's delete_equipment is not translated): everything worn and held goes to the floor
            foreach (obj_item item in Dm.each<obj_item>(bobMob.get_equipped_items(INCLUDE_HELD)))
                Waiting.NoSleep(bobMob.dropItemToGround(item, force: Dm.TRUE));
            bobMob.set_hairstyle("Bald", update: Dm.TRUE);
        }
        w.Run(10);
        Assert.Null(bobMob.w_uniform);
        Assert.Null(bobMob.back);
        (Tables tables, AppearanceCompositor compositor, TableRef look) = Picture(alice, bobMob);
        output.WriteLine(string.Join("\n", CrewOutfitTests.Icons(alice.Session.Replica, look).Select(i => $"{i.Icon} : {i.State} @ {i.Layer}")));
        foreach (byte dir in Frames)
        {
            Assert.True(compositor.TryCompose(tables, look, dir, 0, out ComposedRaster picture));
            output.WriteLine($"naked, dir {dir}:\n{Grid(tables, compositor, look, dir, picture)}");
        }

        // icon x, y from the bottom left; in N the mob's left arm is on the viewer's left
        AssertZone(AimZone.LeftArm, 1, 8, 16);
        AssertZone(AimZone.LeftArm, 1, 10, 17);
        AssertZone(AimZone.LeftArm, 1, 11, 19);
        AssertZone(AimZone.RightArm, 1, 24, 16);
        AssertZone(AimZone.RightArm, 1, 21, 19);
        AssertZone(AimZone.LeftArm, 1, 8, 13); // the left hand
        AssertZone(AimZone.RightArm, 1, 24, 13); // the right hand
        AssertZone(AimZone.LeftLeg, 1, 13, 5);
        AssertZone(AimZone.RightLeg, 1, 19, 5);
        // W: the mob faces the viewer's left, its right side away. Slice T2 fixes the legs where they overlap (W 17,5; E's overlap
        // columns): the lookup picks the wrong leg there, so those pixels are not asserted here.
        AssertZone(AimZone.RightLeg, 8, 15, 5);
        AssertZone(AimZone.RightArm, 8, 12, 13); // the far hand
        AssertZone(AimZone.LeftArm, 8, 18, 13); // the near hand
        AssertZone(AimZone.LeftArm, 8, 19, 17);
        // E
        AssertZone(AimZone.RightLeg, 4, 16, 5);
        AssertZone(AimZone.RightArm, 4, 14, 13);
        AssertZone(AimZone.LeftArm, 4, 21, 13);
        AssertZone(AimZone.RightArm, 4, 13, 17);

        void AssertZone(AimZone expected, byte dir, byte x, byte y)
            => Assert.True(expected == BodyZoneAim.ZoneAt(tables, compositor, look, dir, x, y),
                $"dir {dir} at {x},{y}: expected {expected}, found {BodyZoneAim.ZoneAt(tables, compositor, look, dir, x, y)}");
    }

    [Fact]
    public void A_dressed_human_is_hit_on_its_body_not_its_hair_backpack_or_held_item()
    {
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return; // no tg icons here
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var bobMob = Dressed(w, bob);
        (Tables tables, AppearanceCompositor compositor, TableRef emptyHanded) = Picture(alice, bobMob);
        var before = new Dictionary<byte, ComposedRaster>();
        foreach (byte dir in Frames)
        {
            Assert.True(compositor.TryCompose(tables, emptyHanded, dir, 0, out ComposedRaster picture));
            before[dir] = picture;
        }
        using (w.World.Enter())
            DevCommands.Give(bobMob, "/obj/item/crowbar/large");
        w.Run(10);
        Assert.NotNull(bobMob.get_active_held_item());
        (_, _, TableRef look) = Picture(alice, bobMob);
        Assert.NotEqual(emptyHanded, look);
        int heldOutside = 0;
        var holes = new Dictionary<byte, List<(byte X, byte Y)>>();
        var masks = new Dictionary<byte, (ComposedRaster Picture, Raster Mask)>();
        foreach (byte dir in Frames)
        {
            Assert.True(compositor.TryCompose(tables, look, dir, 0, out ComposedRaster picture));
            Raster mask = BodyZoneAim.BodyMask(tables, compositor, look, dir, picture);
            Assert.NotNull(mask);
            Assert.Equal((picture.Image.Width, picture.Image.Height), (mask.Width, mask.Height));
            masks[dir] = (picture, mask);
            holes[dir] = new List<(byte, byte)>();
            for (int y = 32; y >= 1; y--)
                for (int x = 1; x <= 32; x++)
                {
                    bool drawn = Drawn(picture.Image, picture, x, y), inMask = Drawn(mask, picture, x, y);
                    AimZone zone = BodyZoneAim.ZoneAt(tables, compositor, look, dir, (byte)x, (byte)y);
                    // the mask is the drawn pixels where a body part is: every one of them has a zone, and no other pixel is in it
                    Assert.True(inMask == (drawn && zone != AimZone.None), $"dir {dir} at {x},{y}: drawn {drawn}, zone {zone}, in the mask {inMask}");
                    if (drawn && zone == AimZone.None)
                        holes[dir].Add(((byte)x, (byte)y));
                    if (drawn && !inMask && !Drawn(before[dir].Image, before[dir], x, y))
                        heldOutside++; // drawn only with the crowbar in hand, and not hit
                }
            output.WriteLine($"dressed with a crowbar, dir {dir}:\n{Grid(tables, compositor, look, dir, picture)}");
            output.WriteLine($"dir {dir} drawn, no body part (not in the mask): {string.Join(" ", holes[dir].Select(p => $"{p.X},{p.Y}"))}");
        }
        // the top of the hair in N, the backpack beside the back in the side frames, the crowbar outside the body: not hit
        Assert.Contains(((byte)16, (byte)31), holes[1]);
        Assert.Contains(((byte)11, (byte)17), holes[4]);
        Assert.Contains(((byte)22, (byte)17), holes[8]);
        Assert.True(heldOutside > 0, "the crowbar is drawn somewhere outside the body");
        // the jumpsuit over the chest is hit, and is the chest; a body part under clothes is hit (the arm in its sleeve)
        Assert.True(Drawn(masks[1].Mask, masks[1].Picture, 16, 18));
        Assert.Equal(AimZone.Chest, BodyZoneAim.ZoneAt(tables, compositor, look, 1, 16, 18));
        Assert.True(Drawn(masks[1].Mask, masks[1].Picture, 8, 16));
        Assert.Equal(AimZone.LeftArm, BodyZoneAim.ZoneAt(tables, compositor, look, 1, 8, 16));
    }

    [Fact]
    public void A_ray_from_behind_hits_the_left_arm_and_passes_through_the_hair()
    {
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return; // no tg icons here
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var bobMob = Dressed(w, bob);
        (Tables tables, AppearanceCompositor compositor, TableRef look) = Picture(alice, bobMob);
        // Bob faces south at tile (10, 10); the viewer stands two tiles north of him, behind him: the frame drawn is N (EntityLayer's
        // BillboardMath.ChooseDir over the picture's directions)
        const int tileX = 10, tileY = 10;
        int directions = compositor.Directions(tables, look);
        byte dir = BillboardMath.ChooseDir(BillboardMath.YawOfDir(2), tileX, tileY, tileX, tileY + 2, directions);
        output.WriteLine($"directions {directions}, frame {dir}");
        Assert.Equal(1, dir);
        // the pick shape as EntityLayer.CollectPickShapes makes it: the standing picture at the feet, its rectangle as the billboard's (a mob
        // at WorldScale.MobScale, its lowest drawn row on the floor), hit on its body mask
        Assert.True(compositor.TryCompose(tables, look, dir, 0, out ComposedRaster picture));
        SpriteRect rect = AtlasBuilder.PictureRect(picture, false, WorldScale.MobScale, null, out int trim);
        double feetEast = (tileX - 0.5) * PickGrid.Tile, feetNorth = (tileY - 0.5) * PickGrid.Tile;
        PickShape shape = PickShape.StandingPicture(bobMob.dm_id, PickSurface.Billboard, tileX, tileY, 1, feetEast, feetNorth, 0, rect, picture.Image, picture.OriginX, picture.OriginY);
        shape.HitMask = BodyZoneAim.BodyMask(tables, compositor, look, dir, picture);
        Assert.NotNull(shape.HitMask);

        Assert.True(shape.TryHit(RayAt(8, 16), 10, out _, out _, out _, out _, out byte iconX, out byte iconY));
        output.WriteLine($"trim {trim}, rect {rect}, hit {iconX},{iconY}, zone {BodyZoneAim.ZoneAt(tables, compositor, look, dir, iconX, iconY)}");
        Assert.Equal(((byte)8, (byte)16), (iconX, iconY));
        Assert.Equal(AimZone.LeftArm, BodyZoneAim.ZoneAt(tables, compositor, look, dir, iconX, iconY));

        // the top of the afro: the picture is drawn there, the body is not, so the ray goes on past Bob
        Assert.True(Drawn(picture.Image, picture, 16, 31));
        Assert.False(shape.TryHit(RayAt(16, 31), 10, out _, out _, out _, out _, out _, out _));
        shape.HitMask = null;
        Assert.True(shape.TryHit(RayAt(16, 31), 10, out _, out _, out _, out _, out _, out _), "the whole picture would have been hit there");

        // the eye's ray at the world point of the centre of icon pixel (x, y): looking south, the picture's x runs west
        PickRay RayAt(int x, int y)
        {
            double pixel = rect.PixelSize;
            double u = rect.Left + (picture.OriginX + x - 0.5) * pixel, v = rect.Bottom + (picture.OriginY + y - 0.5) * pixel;
            (double e, double n, double h) eye = (feetEast, feetNorth + 2 * PickGrid.Tile, 1.3);
            double de = feetEast - u - eye.e, dn = feetNorth - eye.n, dh = v - eye.h, length = Math.Sqrt(de * de + dn * dn + dh * dh);
            return new PickRay(eye.e, eye.n, eye.h, 1, de / length, dn / length, dh / length, 10);
        }
    }

    [Fact]
    public void A_giants_body_past_the_icon_square_is_hit_and_aimed_at()
    {
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return; // no tg icons here
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        w.Run(10);
        using (w.World.Enter())
        {
            foreach (obj_item item in Dm.each<obj_item>(bobMob.get_equipped_items(INCLUDE_HELD)))
                Waiting.NoSleep(bobMob.dropItemToGround(item, force: Dm.TRUE));
            bobMob.set_hairstyle("Bald", update: Dm.TRUE);
            Waiting.NoSleep(bobMob.dna.add_mutation(typeof(datum_mutation_gigantism), MUTATION_SOURCE_ACTIVATED));
        }
        RealHeadlessWorld.Player carol = w.Join("Carol");
        var carolMob = Dressed(w, carol); // a second giant with an afro, then a crowbar in hand
        using (w.World.Enter())
            Waiting.NoSleep(carolMob.dna.add_mutation(typeof(datum_mutation_gigantism), MUTATION_SOURCE_ACTIVATED));
        w.Run(10);
        (Tables tables, AppearanceCompositor compositor, TableRef emptyHanded) = Picture(alice, carolMob);
        Assert.True(compositor.TryCompose(tables, emptyHanded, 1, 0, out ComposedRaster before));
        using (w.World.Enter())
            DevCommands.Give(carolMob, "/obj/item/crowbar/large");
        w.Run(10);
        (_, _, TableRef standing) = Picture(alice, bobMob);
        foreach (byte dir in Frames)
            CheckGiant(standing, dir, false, "standing");
        using (w.World.Enter())
            Waiting.NoSleep(bobMob.set_resting(Dm.TRUE, Dm.TRUE));
        w.Run(10);
        (_, _, TableRef lying) = Picture(alice, bobMob);
        Assert.NotEqual(standing, lying);
        foreach (byte dir in new byte[] { 1, 2 }) // ChooseLyingDir's frames
            CheckGiant(lying, dir, true, "lying");

        // the dressed giant from behind: the top of its head above row 32 is hit and is the head; its afro and crowbar outside the body are not hit
        Assert.NotNull(carolMob.get_active_held_item());
        (_, _, TableRef dressed) = Picture(alice, carolMob);
        Assert.True(compositor.TryCompose(tables, dressed, 1, 0, out ComposedRaster seen));
        Raster body = BodyZoneAim.BodyMask(tables, compositor, dressed, 1, seen);
        PickShape shape = ShapeOf(seen, body, false);
        var head = Pixels(seen).First(p => p.Y > 32 && Drawn(body, seen, p.X, p.Y));
        Assert.Equal(AimZone.Head, ClickedZone(shape, dressed, 1, head.X, head.Y));
        var passes = Pixels(seen).Where(p => Drawn(seen.Image, seen, p.X, p.Y) && !Drawn(body, seen, p.X, p.Y)).ToList();
        Assert.Contains(passes, p => p.Y > 32); // the afro over the head
        Assert.Contains(passes, p => !Drawn(before.Image, before, p.X, p.Y)); // the crowbar, drawn only once it is in hand
        foreach ((int x, int y) in passes)
            Assert.False(shape.TryHit(RayAt(shape, x, y), 20, out _, out _, out _, out _, out _, out _), $"the ray at {x},{y} passes the giant");

        void CheckGiant(TableRef look, byte dir, bool lies, string pose)
        {
            Assert.True(compositor.TryCompose(tables, look, dir, 0, out ComposedRaster picture));
            Raster mask = BodyZoneAim.BodyMask(tables, compositor, look, dir, picture);
            Assert.NotNull(mask);
            PickShape giant = ShapeOf(picture, mask, lies);
            int outside = 0, inside = 0, unhit = 0;
            foreach ((int x, int y) in Pixels(picture))
            {
                bool drawn = Drawn(picture.Image, picture, x, y), inMask = Drawn(mask, picture, x, y);
                AimZone part = BodyZoneAim.ZoneAtPixel(tables, compositor, look, dir, x, y);
                // every drawn pixel with a body part under it is in the mask, and only those
                Assert.True(inMask == (drawn && part != AimZone.None), $"{pose} dir {dir} at {x},{y}: drawn {drawn}, part {part}, in the mask {inMask}");
                if (drawn && !inMask)
                    unhit++;
                if (!inMask)
                    continue;
                if (x < 1 || x > 32 || y < 1 || y > 32)
                    outside++;
                else
                    inside++;
                // the zone a click there really gets (the pick, its target, EntityLayer.AimZoneAt's lookup) is the part drawn there
                Assert.True(part == ClickedZone(giant, look, dir, x, y), $"{pose} dir {dir} at {x},{y}: the part {part}, the click {ClickedZone(giant, look, dir, x, y)}");
            }
            output.WriteLine($"{pose} giant, dir {dir}: picture {picture.Image.Width}x{picture.Image.Height} at {picture.OriginX},{picture.OriginY}: "
                + $"{inside} body pixels in the square, {outside} past it, {unhit} drawn pixels with no body part");
            Assert.True(outside > 0, "the giant's body reaches past the icon square");
        }

        // A click's zone at icon pixel (x, y): a ray from behind at it through the pick shape, the hit as WorldPicker and ClickTarget carry it, and the zone as
        // EntityLayer.AimZoneAt looks it up (the unclamped picture pixel); the pixel sent to the server stays clamped.
        AimZone ClickedZone(PickShape at, TableRef look, byte dir, int x, int y) => Clicked(tables, compositor, at, look, dir, x, y);
    }

    [Theory]
    [InlineData("stool", false)]
    [InlineData("stool", true)]
    [InlineData("bar stool", false)]
    [InlineData("bar stool", true)]
    [InlineData("operating table", false)]
    [InlineData("operating table", true)]
    [InlineData("resting on a bed's tile", false)]
    [InlineData("resting on a bed's tile", true)]
    [InlineData("standing on an operating table's tile", false)]
    [InlineData("standing on an operating table's tile", true)]
    [InlineData("handrail", false)] // pixel_w
    [InlineData("giant on a bar stool", false)]
    public void A_body_raised_by_furniture_is_hit_and_aimed_at_where_it_is_drawn(string state, bool dressed)
    {
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return; // no tg icons here
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        mob_living_carbon_human bobMob;
        if (dressed)
        {
            bobMob = Dressed(w, bob);
            using (w.World.Enter())
                DevCommands.Give(bobMob, "/obj/item/crowbar/large");
        }
        else
        {
            bobMob = (mob_living_carbon_human)w.MobOf(bob);
            w.Run(10);
            using (w.World.Enter())
            {
                foreach (obj_item item in Dm.each<obj_item>(bobMob.get_equipped_items(INCLUDE_HELD)))
                    Waiting.NoSleep(bobMob.dropItemToGround(item, force: Dm.TRUE));
                bobMob.set_hairstyle("Bald", update: Dm.TRUE);
            }
        }
        w.Run(10);
        using (w.World.Enter())
        {
            var here = (turf)bobMob.loc;
            if (state.StartsWith("giant", StringComparison.Ordinal))
                Waiting.NoSleep(bobMob.dna.add_mutation(typeof(datum_mutation_gigantism), MUTATION_SOURCE_ACTIVATED));
            Type furniture = state switch
            {
                "stool" => typeof(obj_structure_chair_stool),
                "bar stool" or "giant on a bar stool" => typeof(obj_structure_chair_stool_bar),
                "resting on a bed's tile" => typeof(obj_structure_bed),
                "handrail" => typeof(obj_structure_handrail),
                _ => typeof(obj_structure_table_optable),
            };
            var made = (atom_movable)DevCommands.Spawn(furniture, here, 1, state == "handrail" ? (byte)4 : (byte)2, out _).Single();
            if (state.Contains("tile", StringComparison.Ordinal))
            {
                // unbuckled: stepped off the tile and back on, as a walk onto it
                Waiting.NoSleep(bobMob.forceMove(Builtins.get_step(bobMob, 4)));
                Waiting.NoSleep(bobMob.forceMove(here));
                if (state.StartsWith("resting", StringComparison.Ordinal))
                    Waiting.NoSleep(bobMob.set_resting(Dm.TRUE, Dm.TRUE));
            }
            else
                Waiting.NoSleep(made.buckle_mob(bobMob, force: Dm.TRUE));
        }
        w.Run(10);
        (Tables tables, AppearanceCompositor compositor, TableRef look) = Picture(alice, bobMob);
        ReplicaEntity seen = alice.Session.Replica.Entities.Single(e => e.Id == bobMob.dm_id);
        Assert.True(tables.TryGetAppearance(look, out AppearanceEntry rootEntry));
        output.WriteLine($"{state}{(dressed ? ", dressed" : "")}: pixel_w {rootEntry.PixelW}, pixel_z {rootEntry.PixelZ}, pixel_y {rootEntry.PixelY}, transform {(rootEntry.Transform == null ? "none" : string.Join(" ", rootEntry.Transform))}");
        Assert.True(rootEntry.PixelW != 0 || rootEntry.PixelZ != 0, "the furniture raised or moved the body");
        bool lying = PostureBits.IsLying(seen.State.Posture);
        foreach (byte dir in lying ? new byte[] { 1, 2 } : Frames)
        {
            Assert.True(compositor.TryCompose(tables, look, dir, 0, out ComposedRaster picture));
            Raster mask = BodyZoneAim.BodyMask(tables, compositor, look, dir, picture);
            Assert.NotNull(mask);
            // the truth from the compositor itself: the root drawn with its body parts alone, all of them and each one
            List<TableRef> limbs = rootEntry.Overlays.Where(o => tables.TryGetAppearance(o, out AppearanceEntry part)
                && LimbIcons.IsLimbFile(tables.ResolveResource(part.Icon)) && BodyZoneAim.ZoneOfState(tables.ResolveString(part.IconState)) != AimZone.None).ToList();
            Assert.NotEmpty(limbs);
            ComposedRaster body = Only(tables, look, dir, limbs.Contains);
            var parts = limbs.Select(l => (Zone: ZoneOf(tables, l), Picture: Only(tables, look, dir, o => o == l))).ToList();
            PickShape shape = ShapeOf(picture, mask, lying);
            int bodyPixels = 0, missing = 0, extra = 0;
            foreach ((int x, int y) in Pixels(picture))
            {
                bool drawn = Drawn(picture.Image, picture, x, y), onBody = drawn && Drawn(body.Image, body, x, y), inMask = Drawn(mask, picture, x, y);
                if (onBody)
                    bodyPixels++;
                if (onBody && !inMask)
                    missing++;
                if (inMask && !onBody)
                    extra++;
                if (!inMask)
                    continue;
                // the click there names a part drawn at that pixel (two where the legs overlap in a side frame: which one wins is slice T2's)
                AimZone clicked = Clicked(tables, compositor, shape, look, dir, x, y);
                List<AimZone> drawnThere = parts.Where(p => Drawn(p.Picture.Image, p.Picture, x, y)).Select(p => p.Zone).ToList();
                Assert.True(drawnThere.Contains(clicked), $"dir {dir} at {x},{y}: the click names {clicked}, drawn there: {string.Join(" ", drawnThere)}");
            }
            output.WriteLine($"  dir {dir}: picture {picture.Image.Width}x{picture.Image.Height} at {picture.OriginX},{picture.OriginY}: body {bodyPixels}, missing {missing}, not on the body {extra}");
            Assert.Equal((0, 0), (missing, extra));
            if (state.Contains("bar stool", StringComparison.Ordinal))
            {
                // the top of the seated head
                ComposedRaster head = parts.First(p => p.Zone == AimZone.Head).Picture;
                (int X, int Y) top = Pixels(picture).Where(p => Drawn(picture.Image, picture, p.X, p.Y) && Drawn(head.Image, head, p.X, p.Y)).OrderByDescending(p => p.Y).First();
                Assert.Equal(AimZone.Head, Clicked(tables, compositor, shape, look, dir, top.X, top.Y));
            }
            if (dressed)
            {
                // the afro and the crowbar outside the body are drawn and not hit
                ComposedRaster hair = Only(tables, look, dir, o => tables.TryGetAppearance(o, out AppearanceEntry e) && (tables.ResolveString(e.IconState) ?? "").StartsWith("hair", StringComparison.Ordinal));
                ComposedRaster held = Only(tables, look, dir, o => tables.TryGetAppearance(o, out AppearanceEntry e) && (tables.ResolveResource(e.Icon) ?? "").Contains("inhands", StringComparison.Ordinal));
                var passes = Pixels(picture).Where(p => Drawn(picture.Image, picture, p.X, p.Y) && !Drawn(mask, picture, p.X, p.Y)).ToList();
                Assert.Contains(passes, p => Drawn(hair.Image, hair, p.X, p.Y));
                Assert.Contains(passes, p => Drawn(held.Image, held, p.X, p.Y));
                foreach ((int x, int y) in passes)
                    Assert.False(shape.TryHit(RayAt(shape, x, y), 20, out _, out _, out _, out _, out _, out _), $"dir {dir}: the ray at {x},{y} passes");
            }
        }

        AimZone ZoneOf(Tables t, TableRef limb) => t.TryGetAppearance(limb, out AppearanceEntry e) ? BodyZoneAim.ZoneOfState(t.ResolveString(e.IconState)) : AimZone.None;
    }

    // The root drawn by a fresh compositor with only the root's overlays keep takes (its placement, transform and offsets as they are).
    private static ComposedRaster Only(Tables tables, TableRef root, byte dir, Func<TableRef, bool> keep)
    {
        var only = new OnlyOverlays(tables, root, keep);
        string icons = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        Assert.True(new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(icons))).TryCompose(only, root, dir, 0, out ComposedRaster picture));
        return picture;
    }

    private sealed class OnlyOverlays(Tables inner, TableRef root, Func<TableRef, bool> keep) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry)
        {
            if (!inner.TryGetAppearance(reference, out entry))
                return false;
            if (reference == root)
                entry = entry.WithOverlays(keep);
            return true;
        }

        public string ResolveString(TableRef reference) => inner.ResolveString(reference);

        public string ResolveResource(TableRef reference) => inner.ResolveResource(reference);
    }

    // A click's zone at icon pixel (x, y): a ray from behind at it through the pick shape, the hit as WorldPicker and ClickTarget carry it, and the zone as
    // EntityLayer.AimZoneAt looks it up (the unclamped picture pixel); the pixel sent to the server stays clamped.
    private static AimZone Clicked(Tables tables, AppearanceCompositor compositor, PickShape at, TableRef look, byte dir, int x, int y)
    {
        Assert.True(at.TryHit(RayAt(at, x, y), 20, out double t, out double e, out double n, out double u, out byte iconX, out byte iconY, out int px, out int py), $"the ray at {x},{y} hits");
        Assert.Equal((x, y), (px, py));
        ClickTarget target = ClickTarget.Of(new PickHit(PickSurface.Billboard, TargetKind.Entity, at.EntityId, at.TileX, at.TileY, at.TileZ, t, e, n, u, iconX, iconY, (px, py)));
        Assert.Equal(((byte)Math.Clamp(x, 1, 32), (byte)Math.Clamp(y, 1, 32)), (target.IconX, target.IconY));
        return target.Picture is (int cx, int cy) ? BodyZoneAim.ZoneAtPixel(tables, compositor, look, dir, cx, cy) : AimZone.None;
    }

    // A mob's standing pick shape at tile (10, 10) as EntityLayer.CollectPickShapes makes it (the billboard's rectangle at WorldScale.MobScale), hit on its mask.
    private static PickShape ShapeOf(ComposedRaster picture, Raster mask, bool lying)
    {
        SpriteRect rect = AtlasBuilder.PictureRect(picture, lying, WorldScale.MobScale, null, out _);
        PickShape shape = PickShape.StandingPicture(1, PickSurface.Billboard, 10, 10, 1, 9.5 * PickGrid.Tile, 9.5 * PickGrid.Tile, 0, rect, picture.Image, picture.OriginX, picture.OriginY);
        shape.HitMask = mask;
        return shape;
    }

    // The ray from an eye two tiles north, straight behind the world point of the centre of icon pixel (x, y), looking south (so the turned picture
    // squares to it and its x runs west).
    private static PickRay RayAt(PickShape shape, int x, int y)
    {
        double pixel = shape.Rect.PixelSize;
        double u = shape.Rect.Left + (shape.OriginX + x - 0.5) * pixel, v = shape.Rect.Bottom + (shape.OriginY + y - 0.5) * pixel;
        (double e, double n, double h) eye = (shape.East - u, shape.North + 2 * PickGrid.Tile, 1.3);
        double dn = shape.North - eye.n, dh = v - eye.h, length = Math.Sqrt(dn * dn + dh * dh);
        return new PickRay(eye.e, eye.n, eye.h, 1, 0, dn / length, dh / length, 20);
    }

    // Every pixel of a composed picture, in tg's icon coordinates (not limited to the icon square).
    private static IEnumerable<(int X, int Y)> Pixels(ComposedRaster picture)
    {
        for (int fromBottom = 0; fromBottom < picture.Image.Height; fromBottom++)
            for (int col = 0; col < picture.Image.Width; col++)
                yield return (col - picture.OriginX + 1, fromBottom - picture.OriginY + 1);
    }

    [Fact]
    public void A_mob_with_no_body_parts_keeps_its_whole_picture()
    {
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return; // no tg icons here
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        mob_living aliceMob = w.MobOf(alice);
        w.Run(10);
        mob_living roach;
        using (w.World.Enter())
            roach = (mob_living)DevCommands.Spawn(typeof(mob_living_basic_cockroach), (turf)aliceMob.loc, 1, 2, out _).Single();
        w.Run(10);
        (Tables tables, AppearanceCompositor compositor, TableRef look) = Picture(alice, roach);
        Assert.True(compositor.TryCompose(tables, look, 2, 0, out ComposedRaster picture));
        Assert.Null(BodyZoneAim.BodyMask(tables, compositor, look, 2, picture));
    }

    [Theory]
    [InlineData("icons/mob/human/bodyparts_greyscale.dmi", true)]
    [InlineData("icons/mob/human/bodyparts.dmi", true)]
    [InlineData("icons/mob/human/species/lizard/bodyparts.dmi", true)]
    [InlineData("icons/mob/augmentation/augments.dmi", true)]
    [InlineData("iconops:icons/mob/human/bodyparts_greyscale.dmi|human_l_leg|0|2,1,1,i,icons/mob/leg_masks.dmi,left_leg", true)] // a leg cut by its mask at run time
    [InlineData("iconops:icons%2Fmob%2Fhuman%2Fbodyparts.dmi|human_chest_m|0", true)]
    [InlineData("icons/obj/tools.dmi", false)] // screwdriver_head
    [InlineData("icons/mob/human/species_extra.dmi", false)] // only a folder's own files
    [InlineData("iconops:icons/obj/tools.dmi|screwdriver_head|0", false)]
    [InlineData("gags:/datum/greyscale_config/jumpsuit|#ffffff", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void A_body_part_is_a_picture_from_a_limb_file(string resource, bool limb) => Assert.Equal(limb, LimbIcons.IsLimbFile(resource));

    // A human in tg's assistant outfit (a jumpsuit, a backpack) with an afro.
    private static mob_living_carbon_human Dressed(RealHeadlessWorld w, RealHeadlessWorld.Player player)
    {
        var mob = (mob_living_carbon_human)w.MobOf(player);
        w.Run(10);
        using (w.World.Enter())
        {
            Waiting.NoSleep(mob.equipOutfit(typeof(datum_outfit_job_assistant)));
            mob.set_hairstyle("Afro", update: Dm.TRUE);
        }
        w.Run(10);
        Assert.NotNull(mob.back);
        Assert.NotNull(mob.w_uniform);
        return mob;
    }

    // Whether tg's icon pixel (x, y) of a raster laid out as the picture is (its origin) has alpha of PickAlpha or more.
    private static bool Drawn(Raster image, ComposedRaster picture, int x, int y)
    {
        int col = picture.OriginX + x - 1, fromBottom = picture.OriginY + y - 1;
        if (col < 0 || col >= image.Width || fromBottom < 0 || fromBottom >= image.Height)
            return false;
        int row = image.Height - 1 - fromBottom;
        return image.Pixels[(row * image.Width + col) * 4 + 3] >= BillboardMath.PickAlpha;
    }

    private static string Grid(Tables tables, AppearanceCompositor compositor, TableRef look, byte dir, ComposedRaster picture)
    {
        var text = new System.Text.StringBuilder();
        for (int y = 32; y >= 1; y--)
        {
            text.Append($"{y,2} ");
            for (int x = 1; x <= 32; x++)
            {
                AimZone zone = BodyZoneAim.ZoneAt(tables, compositor, look, dir, (byte)x, (byte)y);
                text.Append(zone == AimZone.None && Drawn(picture.Image, picture, x, y) ? '#' : Letter(zone));
            }
            text.Append('\n');
        }
        text.Append("   ");
        for (int x = 1; x <= 32; x++)
            text.Append(x % 10);
        return text.ToString();
    }

    private static char Letter(AimZone zone) => zone switch
    {
        AimZone.Head => 'H', AimZone.Chest => 'C', AimZone.LeftArm => 'l', AimZone.RightArm => 'r',
        AimZone.LeftLeg => 'L', AimZone.RightLeg => 'R', _ => '.',
    };

    private static (Tables, AppearanceCompositor, TableRef) Picture(RealHeadlessWorld.Player viewer, mob_living mob)
    {
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        ReplicaEntity seen = viewer.Session.Replica.Entities.Single(e => e.Id == mob.dm_id);
        return (new Tables(viewer.Session.Replica), new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(root))), seen.State.Appearance);
    }

    private sealed class Tables(ReplicaWorld replica) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }
}
