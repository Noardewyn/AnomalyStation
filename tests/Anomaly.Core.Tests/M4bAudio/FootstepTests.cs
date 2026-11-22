using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tests.Tg.M2Locomotion.P4;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M4bAudio;

/// <summary>
/// The footsteps of M4b audio slice 4: datums/elements/footstep.dm on a human (human.dm:25: FOOTSTEP_MOB_HUMAN, volume 1,
/// e_range -6), the GLOB step tables of __DEFINES/footsteps.dm and a table's footstep_override (tables_racks.dm:93). The
/// walker is a joined player, so it hears its own steps at distance 0 (the volume is the table's times 1). Steps are tg's
/// cardinal Move, as client/Move and the capsule bridge call it.
/// </summary>
public class FootstepTests
{
    private const string Floor = "sound/effects/footstep/floor";
    private const string Plating = "sound/effects/footstep/plating";
    private const string HardBarefoot = "sound/effects/footstep/hardbarefoot";
    private const string Crawl = "sound/effects/footstep/crawl1.ogg";

    private static List<HeardSound> Steps(AudioRoom room, mob who)
        => room.Sounds.Heard.Where(h => ReferenceEquals(h.Target, who) && h.File != null && h.File.StartsWith("sound/effects/footstep/", StringComparison.Ordinal)).ToList();

    // tg's client/Move of a cardinal step: Move(get_step(mob, dir), dir).
    private static void WalkEast(AudioRoom room, mob_living who, int tiles)
    {
        room.In(() =>
        {
            for (int i = 0; i < tiles; i++)
            {
                turf from = who.loc as turf;
                Assert.True(Dm.truthy(Waiting.NoSleep(who.Move(get_step(who, EAST), EAST))), "the step was refused");
                Assert.NotSame(from, who.loc);
            }
        });
    }

    private static void Shoes(AudioRoom room, mob_living_carbon_human who)
        => room.In(() =>
        {
            var shoes = Dm.@new<obj_item_clothing_shoes_sneakers_black>(who.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(who.equip_to_slot_or_del(shoes, ITEM_SLOT_FEET))));
            Assert.Same(shoes, who.shoes);
        });

    [Fact]
    public void FourTilesOnAFloorPlayTwoSteps()
    {
        var room = new AudioRoom(16, 6, seed: 5501);
        mob_living_carbon_human walker = room.Join(3, 3, "Walker");
        Shoes(room, walker);
        room.Sounds.Heard.Clear();

        WalkEast(room, walker, 4);
        List<HeardSound> steps = Steps(room, walker);
        Assert.Equal(2, steps.Count); // every other move (footstep.dm:91)
        Assert.All(steps, s => Assert.StartsWith(Floor, s.File));
        Assert.All(steps, s => Assert.Equal(75, s.Volume, 3)); // GLOB.footstep[FOOTSTEP_FLOOR]: 75 * volume 1
        Assert.All(steps, s => Assert.Equal(0, s.Frequency)); // sound_vary FALSE
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ShoesAndBareFeetUseTheirSets()
    {
        var room = new AudioRoom(16, 6, seed: 5502);
        mob_living_carbon_human walker = room.Join(3, 3, "Walker");
        room.Sounds.Heard.Clear();

        WalkEast(room, walker, 2);
        HeardSound bare = Assert.Single(Steps(room, walker));
        Assert.StartsWith(HardBarefoot, bare.File); // the leg's FOOTSTEP_MOB_BAREFOOT on the turf's FOOTSTEP_HARD_BAREFOOT
        Assert.Equal(80, bare.Volume, 3);

        Shoes(room, walker);
        room.Sounds.Heard.Clear();
        WalkEast(room, walker, 2);
        HeardSound shod = Assert.Single(Steps(room, walker));
        Assert.StartsWith(Floor, shod.File);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void PlatingGivesThePlatingSet()
    {
        var room = new AudioRoom(16, 6, seed: 5503, mapExtras: _ =>
        {
            for (int x = 4; x <= 7; x++)
                Dm.@new<turf_open_indestructible_plating>(locate(x, 3, 1));
        });
        mob_living_carbon_human walker = room.Join(3, 3, "Walker");
        Shoes(room, walker);
        room.Sounds.Heard.Clear();

        WalkEast(room, walker, 4);
        List<HeardSound> steps = Steps(room, walker);
        Assert.Equal(2, steps.Count);
        Assert.All(steps, s => Assert.StartsWith(Plating, s.File));
        Assert.All(steps, s => Assert.Equal(100, s.Volume, 3)); // GLOB.footstep[FOOTSTEP_PLATING]: 100, extra range 1
        room.AssertNoRuntimes();
    }

    [Fact]
    public void LyingCrawlsOnEveryMove()
    {
        var room = new AudioRoom(16, 6, seed: 5504);
        mob_living_carbon_human walker = room.Join(3, 3, "Walker");
        room.In(() => Waiting.NoSleep(walker.set_resting(TRUE, silent: TRUE, instant: TRUE)));
        Assert.Equal(LYING_DOWN, room.In(() => walker.body_position));
        room.Sounds.Heard.Clear();

        WalkEast(room, walker, 3);
        List<HeardSound> steps = Steps(room, walker);
        Assert.Equal(3, steps.Count); // play_craw_step comes before prepare_step's every-other count
        Assert.All(steps, s => Assert.Equal(Crawl, s.File));
        Assert.All(steps, s => Assert.Equal(15, s.Volume, 3)); // 15 * volume 1
        room.AssertNoRuntimes();
    }

    [Fact]
    public void WalkIntentStepsAreHalfAsLoud()
    {
        var room = new AudioRoom(16, 6, seed: 5505);
        mob_living_carbon_human walker = room.Join(3, 3, "Walker");
        Shoes(room, walker);
        room.In(() => Waiting.NoSleep(walker.set_move_intent(MOVE_INTENT_WALK)));
        room.Sounds.Heard.Clear();

        WalkEast(room, walker, 4);
        List<HeardSound> steps = Steps(room, walker);
        // DV71: tg's SHOULD_MOB_DISABLE_FOOTSTEPS silences a walking carbon; here its steps play at half a run's 75 (GLOB.footstep[FOOTSTEP_FLOOR])
        Assert.Equal(2, steps.Count);
        Assert.All(steps, s => Assert.Equal(75 * WALK_FOOTSTEP_VOLUME, s.Volume, 3));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ZeroGravityStepsEverySixthMove()
    {
        var room = new AudioRoom(20, 6, seed: 5506);
        mob_living_carbon_human walker = room.Join(3, 3, "Walker");
        Shoes(room, walker);
        room.In(() =>
        {
            for (int x = 2; x <= 18; x++)
                locate(x, 3, 1).force_no_gravity = TRUE;
            Assert.False(Dm.truthy(walker.has_gravity()));
        });
        room.Sounds.Heard.Clear();

        WalkEast(room, walker, 12);
        Assert.Equal(2, Steps(room, walker).Count); // moves 6 and 12 (footstep.dm:95: steps % 6 without gravity)
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ATableOverridesThePlatingSet()
    {
        var room = new AudioRoom(16, 6, seed: 5507, mapExtras: _ =>
        {
            for (int x = 4; x <= 5; x++)
                Dm.@new<turf_open_indestructible_plating>(locate(x, 3, 1));
        });
        mob_living_carbon_human walker = room.Join(3, 3, "Walker");
        Shoes(room, walker);
        obj_structure_table table = room.In(() => Dm.@new<obj_structure_table>(locate(5, 3, 1)));
        room.In(() => Dm.combine(ref walker.pass_flags, PASSTABLE)); // onto the table without climbing
        room.Sounds.Heard.Clear();

        WalkEast(room, walker, 2); // plating (4, 3), then the table's plating (5, 3)
        HeardSound onTable = Assert.Single(Steps(room, walker));
        Assert.StartsWith(Floor, onTable.File); // footstep_override's default footstep FOOTSTEP_FLOOR at table priority

        // The table moved off: its turf sounds as plating again (on_moved vacates the turf).
        room.In(() => Waiting.NoSleep(table.forceMove(locate(10, 4, 1))));
        room.In(() => Waiting.NoSleep(walker.forceMove(locate(3, 3, 1)))); // forced: no step
        room.Sounds.Heard.Clear();
        WalkEast(room, walker, 2);
        HeardSound bare = Assert.Single(Steps(room, walker));
        Assert.StartsWith(Plating, bare.File);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheCapsuleStepsOncePerTileWalked()
    {
        var room = new CapsuleRoom(seed: 5508);
        var sounds = new SoundCapture();
        PresentationSinks.Of(room.World).Sounds = sounds;
        mob_living_carbon_human walker = room.Join(4, 8, "Walker");
        var moved = room.In(() => Dm.@new<datum_test_m2p4_listener>());
        room.In(() => moved.Watch(walker));
        sounds.Heard.Clear();
        room.In(() => LocomotionSeam.SetFootstride(room.World, walk: false, 0.5f)); // the half-tile stride these counts are for
        List<HeardSound> Steps() => sounds.Heard.Where(h => ReferenceEquals(h.Target, walker) && h.File != null && h.File.StartsWith("sound/effects/footstep/", StringComparison.Ordinal)).ToList();

        int ticks = 0;
        while (room.Tile(walker).X < 8)
        {
            room.Walk(walker, 127, 0, 1, yaw: 16384);
            Assert.True(++ticks < 200, "the walker did not reach x = 8");
        }
        Assert.Equal(4, moved.Count("moved")); // one Moved per tile
        Assert.Equal(3, Steps().Count); // DV233: 3.5 tiles walked are seven half-tile strides, not four crossings
        while (room.State(walker).X < 8 * MotionMath.SubunitsPerTile)
        {
            room.Walk(walker, 127, 0, 1, yaw: 16384);
            Assert.True(++ticks < 200, "the walker did not reach the centre of x = 8");
        }
        Assert.Equal(4, Steps().Count);
        Assert.All(Steps(), s => Assert.StartsWith(HardBarefoot, s.File));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void StoppingLandsAStepInTheAirOnly()
    {
        var room = new CapsuleRoom(seed: 5509);
        var sounds = new SoundCapture();
        PresentationSinks.Of(room.World).Sounds = sounds;
        mob_living_carbon_human walker = room.Join(4, 8, "Walker");
        sounds.Heard.Clear();
        room.In(() => LocomotionSeam.SetFootstride(room.World, walk: false, 0.5f)); // the half-tile stride these counts are for
        int Steps() => sounds.Heard.Count(h => ReferenceEquals(h.Target, walker) && h.File != null && h.File.StartsWith("sound/effects/footstep/", StringComparison.Ordinal));
        void WalkTo(int x)
        {
            for (int ticks = 0; room.State(walker).X < x; ticks++)
            {
                room.Walk(walker, 127, 0, 1, yaw: 16384);
                Assert.True(ticks < 200, "the walker did not get there");
            }
        }

        // DV233: 1.5 tiles are three half-tile strides, one step sounded and a foot in the air: the stop lands it
        WalkTo(4 * MotionMath.SubunitsPerTile + MotionMath.SubunitsPerTile * 3 / 2);
        Assert.Equal(1, Steps());
        room.Walk(walker, 0, 0, 20, yaw: 16384);
        Assert.Equal(2, Steps());

        // a fresh walk of one tile: two strides, the second sounded; the stop stays quiet
        int from = room.State(walker).X;
        WalkTo(from + MotionMath.SubunitsPerTile);
        int walked = Steps();
        room.Walk(walker, 0, 0, 20, yaw: 16384);
        Assert.Equal(walked, Steps());
        room.AssertNoRuntimes();
    }
}
