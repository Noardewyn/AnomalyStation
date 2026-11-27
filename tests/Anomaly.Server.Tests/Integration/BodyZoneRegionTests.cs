using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Package T, slice T4a (docs/ai/specs/ui-overhaul.md section 8): the eyes, the mouth and the groin by look, on tg's real pictures through a real
/// headless world. The regions are tg's targeting doll's (<c>/atom/movable/screen/zone_sel/proc/get_zone_at</c>), placed relative to the head and the
/// chest as they are drawn. The grids are printed (H head, E eyes, M mouth, C chest, G groin, l/r left/right arm, L/R left/right leg, '.' none).
/// </summary>
public class BodyZoneRegionTests(ITestOutputHelper output)
{
    // tg's get_zone_at (screen_objects.dm:786-822): the eyes are rows 25 to 27, columns 15 to 17, and on the eyeline (row 26) columns 14 to 18; the
    // mouth rows 23 and 24, columns 15 to 17; the groin rows 10 to 13, columns 12 to 20; the head rows 23 to 30, columns 12 to 20
    private static readonly HashSet<(int, int)> TgEyes = Square(15, 17, 25, 27).Concat(new[] { (14, 26), (18, 26) }).ToHashSet();
    private static readonly HashSet<(int, int)> TgMouth = Square(15, 17, 23, 24).ToHashSet();
    private const int TgGroinBottom = 10, TgGroinTop = 13, TgGroinLeft = 12, TgGroinRight = 20;

    [Fact]
    public void The_naked_humans_eyes_mouth_and_groin_are_tgs_doll_in_each_frame()
    {
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return; // no tg icons here
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        Naked(w, bobMob);
        (Tables tables, AppearanceCompositor compositor, TableRef look) = Picture(alice, bobMob);
        foreach (byte dir in new byte[] { 1, 2, 4, 8 })
        {
            Func<int, int, AimZone> top = BodyZoneProbeTests.TopPart(tables, look, dir);
            Func<int, int, AimZone> zone = (x, y) => BodyZoneAim.ZoneAt(tables, compositor, look, dir, (byte)x, (byte)y);
            output.WriteLine($"naked, dir {dir}:\n{Grid(zone, 1, 32, 1, 32)}");
            // every region lies on its own limb: the limb of each pixel is the part drawn on top there
            foreach ((int x, int y) in Square(1, 32, 1, 32))
                Assert.True(BodyZoneMap.LimbOf(zone(x, y)) == top(x, y), $"dir {dir} at {x},{y}: {zone(x, y)} where {top(x, y)} is drawn on top");
            HashSet<(int, int)> eyes = Where(zone, AimZone.Eyes), mouth = Where(zone, AimZone.Mouth), groin = Where(zone, AimZone.Groin);
            // the groin, in every frame: the chest's lowest four rows, tg's rows 10 to 13, wherever the chest is on top
            Assert.Equal(Square(1, 32, TgGroinBottom, TgGroinTop).Where(p => top(p.Item1, p.Item2) == AimZone.Chest).ToHashSet(), groin);
            if (dir is 1 or 2) // seen from the front or behind, tg's doll's columns too (from the side the hips reach either side of the arm)
                Assert.All(groin, p => Assert.InRange(p.Item1, TgGroinLeft, TgGroinRight));
            switch (dir)
            {
                case 2: // the front: exactly tg's doll
                    Assert.Equal(TgEyes, eyes);
                    Assert.Equal(TgMouth, mouth);
                    break;
                case 1: // from behind: no eyes and no mouth
                    Assert.Empty(eyes);
                    Assert.Empty(mouth);
                    break;
                default: // the sides: tg's rows, on each the three head pixels at the face's front edge (east: the highest x, west: the lowest)
                    for (int y = 23; y <= 27; y++)
                    {
                        List<int> headRow = Enumerable.Range(1, 32).Where(x => top(x, y) == AimZone.Head).ToList();
                        int edge = dir == 4 ? headRow.Max() : headRow.Min();
                        HashSet<int> expected = Enumerable.Range(0, 3).Select(i => dir == 4 ? edge - i : edge + i).ToHashSet();
                        Assert.Equal(expected, (y >= 25 ? eyes : mouth).Where(p => p.Item2 == y).Select(p => p.Item1).ToHashSet());
                    }
                    Assert.Equal(9, eyes.Count);
                    Assert.Equal(6, mouth.Count);
                    break;
            }
        }
    }

    [Fact]
    public void A_lying_body_keeps_its_eyes_and_mouth_on_its_turned_face()
    {
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return; // no tg icons here
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        Naked(w, bobMob);
        using (w.World.Enter())
            Waiting.NoSleep(bobMob.set_resting(Dm.TRUE, Dm.TRUE));
        w.Run(10);
        (Tables tables, AppearanceCompositor compositor, TableRef look) = Picture(alice, bobMob);
        Assert.True(tables.TryGetAppearance(look, out AppearanceEntry entry));
        Assert.NotNull(entry.Transform); // turned by tg's lying transform
        const byte dir = 2;
        Assert.True(compositor.TryCompose(tables, look, dir, 0, out ComposedRaster picture));
        (int left, int right, int bottom, int topRow) = Extent(picture);
        Func<int, int, AimZone> zone = (x, y) => BodyZoneAim.ZoneAtPixel(tables, compositor, look, dir, x, y);
        output.WriteLine($"lying, dir {dir}:\n{Grid(zone, left, right, bottom, topRow)}");
        Func<int, int, AimZone> top = BodyZoneProbeTests.TopPart(tables, look, dir);
        var pixels = Square(left, right, bottom, topRow).ToList();
        foreach ((int x, int y) in pixels)
            Assert.True(BodyZoneMap.LimbOf(zone(x, y)) == top(x, y), $"at {x},{y}: {zone(x, y)} where {top(x, y)} is drawn on top");
        // a quarter turn with nearest sampling is exact: the same 11 eye and 6 mouth pixels as standing, turned (the eyes beyond the mouth along the
        // body, which lies along x)
        List<(int X, int Y)> eyes = pixels.Where(p => zone(p.Item1, p.Item2) == AimZone.Eyes).ToList();
        List<(int X, int Y)> mouth = pixels.Where(p => zone(p.Item1, p.Item2) == AimZone.Mouth).ToList();
        Assert.Equal(11, eyes.Count);
        Assert.Equal(6, mouth.Count);
        Assert.Equal(5, eyes.Select(p => p.Y).Distinct().Count()); // the eyeline is now a column of five
        Assert.True(Math.Abs(eyes.Average(p => p.X) - mouth.Average(p => p.X)) >= 2, "the eyes and the mouth lie apart along the turned body");
    }

    [Fact]
    public void A_giant_and_a_bar_stool_sitter_keep_the_regions_on_the_drawn_head()
    {
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return; // no tg icons here
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        RealHeadlessWorld.Player carol = w.Join("Carol");
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var carolMob = (mob_living_carbon_human)w.MobOf(carol);
        Naked(w, bobMob);
        Naked(w, carolMob);
        using (w.World.Enter())
        {
            Waiting.NoSleep(bobMob.dna.add_mutation(typeof(datum_mutation_gigantism), MUTATION_SOURCE_ACTIVATED));
            var stool = (atom_movable)DevCommands.Spawn(typeof(obj_structure_chair_stool_bar), (turf)carolMob.loc, 1, 2, out _).Single();
            Waiting.NoSleep(stool.buckle_mob(carolMob, force: Dm.TRUE));
        }
        w.Run(10);
        const byte dir = 2;

        // the giant: its head drawn past the icon square, the regions scaled with it and still on it, the eyes above the mouth
        (Tables tables, AppearanceCompositor compositor, TableRef giant) = Picture(alice, bobMob);
        Assert.True(compositor.TryCompose(tables, giant, dir, 0, out ComposedRaster picture));
        (int left, int right, int bottom, int topRow) = Extent(picture);
        Assert.True(topRow > 32, "the giant's head reaches past the icon square");
        Func<int, int, AimZone> zone = (x, y) => BodyZoneAim.ZoneAtPixel(tables, compositor, giant, dir, x, y);
        output.WriteLine($"giant, dir {dir}:\n{Grid(zone, left, right, bottom, topRow)}");
        Func<int, int, AimZone> top = BodyZoneProbeTests.TopPart(tables, giant, dir);
        var pixels = Square(left, right, bottom, topRow).ToList();
        foreach ((int x, int y) in pixels)
            Assert.True(BodyZoneMap.LimbOf(zone(x, y)) == top(x, y), $"giant at {x},{y}: {zone(x, y)} where {top(x, y)} is drawn on top");
        List<(int X, int Y)> eyes = pixels.Where(p => zone(p.Item1, p.Item2) == AimZone.Eyes).ToList();
        List<(int X, int Y)> mouth = pixels.Where(p => zone(p.Item1, p.Item2) == AimZone.Mouth).ToList();
        Assert.True(eyes.Count > TgEyes.Count && mouth.Count > TgMouth.Count, $"the giant's eyes ({eyes.Count}) and mouth ({mouth.Count}) are drawn larger");
        Assert.True(eyes.Min(p => p.Y) > mouth.Max(p => p.Y), "the eyes are above the mouth");
        Assert.Contains(eyes, p => p.Y > 32);

        // the bar stool sitter: tg raises the body (pixel_z); the regions are tg's doll moved with it
        (_, _, TableRef seated) = Picture(alice, carolMob);
        Assert.True(tables.TryGetAppearance(seated, out AppearanceEntry entry));
        Assert.True(entry.PixelZ != 0, "the bar stool raises the body");
        Assert.True(compositor.TryCompose(tables, seated, dir, 0, out picture));
        (left, right, bottom, topRow) = Extent(picture);
        zone = (x, y) => BodyZoneAim.ZoneAtPixel(tables, compositor, seated, dir, x, y);
        output.WriteLine($"bar stool (pixel_w {entry.PixelW}, pixel_z {entry.PixelZ}), dir {dir}:\n{Grid(zone, left, right, bottom, topRow)}");
        pixels = Square(left, right, bottom, topRow).ToList();
        Assert.Equal(TgEyes.Select(p => (p.Item1 + entry.PixelW, p.Item2 + entry.PixelZ)).ToHashSet(), pixels.Where(p => zone(p.Item1, p.Item2) == AimZone.Eyes).ToHashSet());
        Assert.Equal(TgMouth.Select(p => (p.Item1 + entry.PixelW, p.Item2 + entry.PixelZ)).ToHashSet(), pixels.Where(p => zone(p.Item1, p.Item2) == AimZone.Mouth).ToHashSet());
    }

    [Fact]
    public void A_monkeys_eyes_and_mouth_stay_on_its_own_head()
    {
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return; // no tg icons here
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        mob_living aliceMob = w.MobOf(alice);
        w.Run(10);
        mob_living_carbon_human monkey;
        using (w.World.Enter())
            monkey = (mob_living_carbon_human)DevCommands.Spawn(typeof(mob_living_carbon_human_species_monkey), (turf)aliceMob.loc, 1, 2, out _).Single();
        w.Run(10);
        (Tables tables, AppearanceCompositor compositor, TableRef look) = Picture(alice, monkey);
        foreach (byte dir in new byte[] { 1, 2, 4, 8 })
        {
            Func<int, int, AimZone> zone = (x, y) => BodyZoneAim.ZoneAt(tables, compositor, look, dir, (byte)x, (byte)y);
            Func<int, int, AimZone> top = BodyZoneProbeTests.TopPart(tables, look, dir);
            output.WriteLine($"monkey, dir {dir}:\n{Grid(zone, 1, 32, 1, 32)}");
            foreach ((int x, int y) in Square(1, 32, 1, 32))
                Assert.True(BodyZoneMap.LimbOf(zone(x, y)) == top(x, y), $"monkey dir {dir} at {x},{y}: {zone(x, y)} where {top(x, y)} is drawn on top");
            HashSet<(int, int)> eyes = Where(zone, AimZone.Eyes), mouth = Where(zone, AimZone.Mouth);
            if (dir == 1)
            {
                Assert.Empty(eyes);
                Assert.Empty(mouth);
                continue;
            }
            // the head's own top row t: the eyes are rows t-4 to t-2, below the human's 25 to 27 where the monkey's head is lower
            int t = Square(1, 32, 1, 32).Where(p => top(p.Item1, p.Item2) == AimZone.Head).Max(p => p.Item2);
            Assert.NotEmpty(eyes);
            Assert.Equal(t - 2, eyes.Max(p => p.Item2));
            Assert.True(t < 29, $"the monkey's head (top row {t}) is drawn lower than the human's (29)");
            Assert.All(mouth, p => Assert.InRange(p.Item2, t - 6, t - 5));
        }
    }

    /// <summary>
    /// Slice T4c (narrows the floor rule: conscious, back up; limp, by the viewer's side): a conscious patient lying buckled to an operating table or a
    /// bed is drawn face up from every side, the frame the client chooses from the replicated posture, so its eyes and mouth are there to aim at.
    /// </summary>
    [Theory]
    [InlineData("operating table")]
    [InlineData("bed")]
    public void A_conscious_patient_on_furniture_shows_its_eyes_and_mouth_from_every_side(string furniture)
    {
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return; // no tg icons here
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        Naked(w, bobMob);
        using (w.World.Enter())
        {
            var made = (atom_movable)DevCommands.Spawn(furniture == "bed" ? typeof(obj_structure_bed) : typeof(obj_structure_table_optable), (turf)bobMob.loc, 1, 2, out _).Single();
            Waiting.NoSleep(made.buckle_mob(bobMob, force: Dm.TRUE));
            Assert.Equal(0f, Dm.num(bobMob.stat)); // conscious
        }
        w.Run(10);
        ReplicaEntity seen = alice.Session.Replica.Entities.Single(e => e.Id == bobMob.dm_id);
        Assert.True(PostureBits.IsLying(seen.State.Posture) && PostureBits.IsBuckled(seen.State.Posture) && !PostureBits.IsLimp(seen.State.Posture));
        AssertFace(alice, bobMob, seen, expectFace: true, furniture);
    }

    /// <summary>
    /// Slice T4c (DV600): a conscious body on the floor keeps the floor rule, back up with no eyes and no mouth from every side; turned over from
    /// the radial it shows its face from every side; turned again, its back.
    /// </summary>
    [Fact]
    public void A_conscious_body_on_the_floor_is_back_up_until_turned_over()
    {
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return; // no tg icons here
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 4000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        mob_living aliceMob = w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        Naked(w, bobMob);
        using (w.World.Enter())
        {
            var start = (turf)aliceMob.loc;
            Waiting.NoSleep(bobMob.forceMove(Builtins.locate(Dm.num(start.x) + 1, Dm.num(start.y), Dm.num(start.z))));
            Waiting.NoSleep(bobMob.set_resting(Dm.TRUE, Dm.TRUE));
        }
        w.Run(20);
        ReplicaEntity Seen() => alice.Session.Replica.Entities.Single(e => e.Id == bobMob.dm_id);
        AssertFace(alice, bobMob, Seen(), expectFace: false, "on the floor");
        var target = new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 16, AimZone.None);
        Assert.True(alice.Session.ContextPick(target, "Turn over"));
        w.Run(40);
        Assert.True(PostureBits.TurnedFaceUp(Seen().State.Posture) == true, "turned face up");
        AssertFace(alice, bobMob, Seen(), expectFace: true, "turned over");
        Assert.True(alice.Session.ContextPick(target, "Turn over"));
        w.Run(40);
        Assert.True(PostureBits.TurnedFaceUp(Seen().State.Posture) == false, "turned back up");
        AssertFace(alice, bobMob, Seen(), expectFace: false, "turned again");
    }

    // The frame the client draws for viewers on the four sides of the lying body (BillboardMath.ChooseLyingDir from the replicated posture) and
    // the zone map of that frame: the front with tg's 11 eye and 6 mouth pixels, or the back with none.
    private void AssertFace(RealHeadlessWorld.Player viewer, mob_living mob, ReplicaEntity seen, bool expectFace, string what)
    {
        (Tables tables, AppearanceCompositor compositor, TableRef look) = Picture(viewer, mob);
        byte posture = seen.State.Posture;
        foreach ((double dx, double dy) in new[] { (0.0, 3.0), (0.0, -3.0), (3.0, 0.0), (-3.0, 0.0) })
        {
            byte dir = BillboardMath.ChooseLyingDir(seen.State.Yaw, 10, 10, 10 + dx, 10 + dy, 4, PostureBits.IsLimp(posture), PostureBits.IsBuckled(posture), PostureBits.TurnedFaceUp(posture));
            Assert.True((expectFace ? ByondDir.South : ByondDir.North) == dir, $"{what}, viewer at {dx},{dy}: frame {dir}");
            Assert.True(compositor.TryCompose(tables, look, dir, 0, out ComposedRaster picture));
            (int left, int right, int bottom, int topRow) = Extent(picture);
            Func<int, int, AimZone> zone = (x, y) => BodyZoneAim.ZoneAtPixel(tables, compositor, look, dir, x, y);
            var pixels = Square(left, right, bottom, topRow).ToList();
            int eyes = pixels.Count(p => zone(p.Item1, p.Item2) == AimZone.Eyes), mouth = pixels.Count(p => zone(p.Item1, p.Item2) == AimZone.Mouth);
            if (dx == 0 && dy > 0)
                output.WriteLine($"{what}, dir {dir}:\n{Grid(zone, left, right, bottom, topRow)}");
            Assert.Equal(expectFace ? (11, 6) : (0, 0), (eyes, mouth));
        }
    }

    // ---- helpers ----

    private static IEnumerable<(int, int)> Square(int left, int right, int bottom, int top)
    {
        for (int y = bottom; y <= top; y++)
            for (int x = left; x <= right; x++)
                yield return (x, y);
    }

    private static HashSet<(int, int)> Where(Func<int, int, AimZone> zone, AimZone wanted)
        => Square(1, 32, 1, 32).Where(p => zone(p.Item1, p.Item2) == wanted).ToHashSet();

    // The picture's pixels in tg's icon coordinates (past the icon square for a giant or a turned body).
    private static (int Left, int Right, int Bottom, int Top) Extent(ComposedRaster picture)
        => (1 - picture.OriginX, picture.Image.Width - picture.OriginX, 1 - picture.OriginY, picture.Image.Height - picture.OriginY);

    internal static string Grid(Func<int, int, AimZone> zone, int left, int right, int bottom, int top)
    {
        var text = new System.Text.StringBuilder();
        for (int y = top; y >= bottom; y--)
        {
            text.Append($"{y,3} ");
            for (int x = left; x <= right; x++)
                text.Append(Letter(zone(x, y)));
            text.Append('\n');
        }
        return text.ToString();
    }

    internal static char Letter(AimZone zone) => zone switch
    {
        AimZone.Head => 'H', AimZone.Eyes => 'E', AimZone.Mouth => 'M', AimZone.Chest => 'C', AimZone.Groin => 'G',
        AimZone.LeftArm => 'l', AimZone.RightArm => 'r', AimZone.LeftLeg => 'L', AimZone.RightLeg => 'R', _ => '.',
    };

    // Everything worn and held dropped and the hair shaved, so the picture is the body alone.
    private static void Naked(RealHeadlessWorld w, mob_living_carbon_human mob)
    {
        w.Run(10);
        using (w.World.Enter())
        {
            foreach (obj_item item in Dm.each<obj_item>(mob.get_equipped_items(INCLUDE_HELD)))
                Waiting.NoSleep(mob.dropItemToGround(item, force: Dm.TRUE));
            mob.set_hairstyle("Bald", update: Dm.TRUE);
        }
        w.Run(10);
    }

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
