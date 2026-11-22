using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M5x;

/// <summary>
/// M5x slice 1: tg's explosion engine (controllers/subsystem/explosions.dm, __DEFINES/explosions.dm, atom_act.dm's roots,
/// datums/elements/blocks_explosives.dm and the ex_act chain of turfs and objects) in a booted toy room whose middle is made a
/// station patch (space under plating under iron floor, as on the station): the spiral of prepare_explosion_turfs, the severity
/// by distance and by the explosion block of reactionary explosions (a wall in between adds EXPLOSION_BLOCK_LIGHT 2.5 per block
/// to the light range's distance), the blocks of walls, reinforced walls, windows and doors, ex_act per severity on walls,
/// reinforced walls, floors, plating, windows, grilles, items, structures and doors (+1 severity; pod doors shrug a light
/// blast), the throws away from the epicenter after the ex_acts, the hotspots of the flame range and the same state hash for the
/// same seed.
/// </summary>
public class ExplosionTests
{
    private const int Second = 20;
    private const int W = 24, H = 16;

    private static AudioRoom Room(ulong seed, bool reactionary = true)
    {
        var r = new AudioRoom(W, H, seed: seed);
        r.In(() =>
        {
            Assert.NotNull(SSexplosions);
            Waiting.NoSleep(config.Set(typeof(datum_config_entry_flag_reactionary_explosions), reactionary ? TRUE : FALSE));
            for (int x = 3; x <= W - 2; x++)
            {
                for (int y = 3; y <= H - 2; y++)
                    Floor(x, y);
            }
        });
        r.Step(Second);
        return r;
    }

    // A station tile: the iron floor over plating over space (the floor's baseturfs, as on the station). One ChangeTurf with the
    // baseturfs (a space turf replaced again in the same tick leaves its neighbours' atmos lists pointing at it).
    private static turf Floor(int x, int y)
        => (turf)Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));

    // A wall of the type on plating (over space), as a station wall stands.
    private static turf Wall(Type type, int x, int y)
        => (turf)Waiting.NoSleep(locate(x, y, 1).ChangeTurf(type, Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));

    private static turf At(int x, int y) => locate(x, y, 1);

    private static List<T> On<T>(AudioRoom r, int x, int y) => r.In(() => Dm.each_cast<atom>(locate(x, y, 1).contents).OfType<T>().ToList());

    private static void Explode(AudioRoom r, int x, int y, float dev, float heavy, float light, float flame = 0, bool cap = false)
        => r.In(() => Waiting.Detach(explosion(At(x, y), dev, heavy, light, flame_range: flame, flash_range: 0, adminlog: FALSE, ignorecap: cap ? FALSE : TRUE, silent: TRUE)));

    private static float Chebyshev(turf a, turf b) => Math.Max(Math.Abs(a.x - b.x), Math.Abs(a.y - b.y));

    [Fact]
    public void ThePropagationSpiralsOutwardRingByRingAndADirectionalBlastKeepsToItsArc()
    {
        var r = Room(6101);
        r.In(() =>
        {
            turf center = At(12, 8);
            DmList all = (DmList)prepare_explosion_turfs(3, center, FALSE, 0, 360);
            Assert.Equal(49, all.len); // 7 x 7, the centre first
            Assert.Same(center, all[1]);
            float ring = 0;
            foreach (turf t in Dm.each_cast<turf>(all))
            {
                Assert.True(Chebyshev(t, center) >= ring, "the turfs come ring by ring, closest first");
                ring = Chebyshev(t, center);
            }
            DmList unprotected = (DmList)prepare_explosion_turfs(3, center, TRUE, 0, 360);
            Assert.Equal(48, unprotected.len); // protect_epicenter leaves the centre out

            // A 90-degree arc pointed east (tg's angle: 0 north, 90 east): only candidates within 45..135 degrees.
            DmList east = (DmList)prepare_explosion_turfs(3, center, FALSE, 90, 90);
            Assert.True(east.len > 1 && east.len < 49);
            foreach (turf t in Dm.each_cast<turf>(east))
            {
                if (t == center)
                    continue;
                float angle = Dm.num(get_angle(center, t));
                Assert.InRange(angle, 45f, 135f);
                Assert.True(t.x > center.x);
            }
        });
    }

    [Fact]
    public void ABigBlastIsRoundAndC4sLightRangeOfThreeIsTgsFiveByFiveSquare()
    {
        // the owner's play test (2026-10-16): "explosions hit a square". tg's severity is the euclidean distance (CHEAP_HYPOTENUSE,
        // explosions.dm:411) below the range: a light range of 7 is a disc (no corner at (5, 5), 7.07 away; (6, 3) at 6.7 is in), while
        // C4's light range of 3 (plastic.dm) takes every turf within 2 tiles both ways, the diagonal (2, 2) at 2.83 included: a 5 x 5 square
        var r = Room(6110, reactionary: false);
        Explode(r, 12, 9, 0, 0, 7);
        r.In(() =>
        {
            bool Hit(int dx, int dy) => Dm.@in(At(12 + dx, 9 + dy), SSexplosions.lowturf);
            Assert.True(Hit(6, 3) && Hit(-6, -3) && Hit(0, 6) && Hit(4, 5));
            Assert.False(Hit(5, 5) || Hit(-5, 5) || Hit(6, 4) || Hit(0, 7));
        });
        var c4 = Room(6111, reactionary: false);
        Explode(c4, 12, 9, 0, 0, 3);
        c4.In(() =>
        {
            bool Hit(int dx, int dy) => Dm.@in(At(12 + dx, 9 + dy), SSexplosions.lowturf);
            for (int dx = -3; dx <= 3; dx++)
                for (int dy = -3; dy <= 3; dy++)
                    Assert.Equal(Math.Abs(dx) <= 2 && Math.Abs(dy) <= 2, Hit(dx, dy)); // the epicenter too
        });
    }

    [Fact]
    public void TheSeverityFollowsTheDistanceAsTgComputesIt()
    {
        var r = Room(6102);
        turf center = r.In(() => At(12, 8));
        Explode(r, 12, 8, 1, 2, 4);
        r.In(() =>
        {
            // propagate_blastwave queued the turfs; SSexplosions processes them at its next fire
            foreach (turf t in Dm.each_cast<turf>(prepare_explosion_turfs(4, center, FALSE, 0, 360)))
            {
                float dist = CHEAP_HYPOTENUSE(t.x, t.y, center.x, center.y);
                bool high = Dm.@in(t, SSexplosions.highturf), med = Dm.@in(t, SSexplosions.medturf), low = Dm.@in(t, SSexplosions.lowturf);
                Assert.Equal(dist < 1, high);
                Assert.Equal(dist >= 1 && dist < 2, med);
                Assert.Equal(dist >= 2 && dist < 4, low);
            }
            Assert.Equal(1f, SSexplosions.highturf.len);
            Assert.Equal(8f, SSexplosions.medturf.len); // the 4 sides at 1 and the 4 diagonals at 1.41
            Assert.True(Dm.truthy(SSexplosions.is_exploding()));
        });
        r.Step(1);
        r.In(() =>
        {
            Assert.False(Dm.truthy(SSexplosions.is_exploding()), "one fire processes the turfs, the movables and the throws");
        });
        Assert.True(r.In(() => At(12, 8) is turf_open_space), "the devastated floor scraped down twice, to space");
    }

    [Fact]
    public void AWallInBetweenStopsTheLightRangeByItsBlockOnlyWithReactionaryExplosions()
    {
        foreach (bool reactionary in new[] { true, false })
        {
            var r = Room(6103, reactionary);
            r.In(() =>
            {
                Wall(typeof(turf_closed_wall), 14, 8);       // east of the epicenter, at 3
                Wall(typeof(turf_closed_wall_r_wall), 8, 8); // west, at 3
            });
            Explode(r, 11, 8, 0, 0, 7);
            r.In(() =>
            {
                Assert.Equal(1f, At(14, 8).explosive_resistance);
                Assert.Equal(2f, At(8, 8).explosive_resistance);
                // the walls themselves are reached (a turf's own block counts only behind it)
                Assert.True(Dm.@in(At(14, 8), SSexplosions.lowturf));
                Assert.True(Dm.@in(At(8, 8), SSexplosions.lowturf));
                // tg: dist + block * EXPLOSION_BLOCK_LIGHT < light_impact_range, the block flowing outward from the turf one step
                // closer (get_step_towards): behind the wall 4 + 2.5 < 7, one further 5 + 2.5 is not; behind the reinforced wall
                // 4 + 5 is not
                Assert.True(Dm.@in(At(15, 8), SSexplosions.lowturf));
                Assert.Equal(!reactionary, Dm.@in(At(16, 8), SSexplosions.lowturf));
                Assert.Equal(!reactionary, Dm.@in(At(7, 8), SSexplosions.lowturf));
                Assert.True(Dm.@in(At(11, 13), SSexplosions.lowturf), "5 tiles north, nothing in between: 5 < 7");
            });
        }
    }

    [Fact]
    public void WindowsAndDoorsAddTheirExplosionBlockToTheirTurf()
    {
        var r = Room(6104);
        var (rwindow, window, airlock) = r.In(() => (
            Dm.@new<obj_structure_window_reinforced_fulltile>(At(6, 6)),
            Dm.@new<obj_structure_window_fulltile>(At(7, 6)),
            Dm.@new<obj_machinery_door_airlock>(At(8, 6))));
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(rwindow, TRAIT_BLOCKING_EXPLOSIVES));
            Assert.Equal(1f, At(6, 6).explosive_resistance); // a reinforced full-tile window blocks 1 (window.dm:502)
            Assert.Equal(0f, At(7, 6).explosive_resistance); // windows only block while reinforced and fulltile (window.dm:67)
            Assert.Equal(1f, At(8, 6).explosive_resistance); // a closed airlock blocks 1 (airlock.dm:85)
        });
        r.In(() => Waiting.NoSleep(airlock.open()));
        r.Step(2 * Second);
        Assert.Equal(0f, r.In(() => At(8, 6).explosive_resistance)); // an open door blocks nothing (door.dm:783)
        r.In(() => Waiting.NoSleep(airlock.close()));
        r.Step(2 * Second);
        Assert.Equal(1f, r.In(() => At(8, 6).explosive_resistance));
        r.In(() => qdel(rwindow));
        r.Step(1);
        Assert.Equal(0f, r.In(() => At(6, 6).explosive_resistance)); // the element detaches with its host
    }

    [Fact]
    public void WallsFallByTheirSeverityAndAReinforcedWallResistsALightBlastMoreOften()
    {
        var r = Room(6105);
        int walls = 0, rwalls = 0;
        r.In(() =>
        {
            // devastate: the wall scrapes away to the plating under it
            turf wall = Wall(typeof(turf_closed_wall), 4, 4);
            Waiting.NoSleep(EX_ACT(wall, EXPLODE_DEVASTATE));
            Assert.IsType<turf_open_floor_plating>(At(4, 4));
            // heavy: always dismantled (devastated or not), plating left
            wall = Wall(typeof(turf_closed_wall), 5, 4);
            Waiting.NoSleep(EX_ACT(wall, EXPLODE_HEAVY));
            Assert.IsType<turf_open_floor_plating>(At(5, 4));
            Assert.NotEmpty(Dm.each_cast<atom>(At(5, 4).contents).OfType<obj_item_stack_sheet_iron>());
            // the target itself (a planted charge): dismantle_wall(1, 1), devastate_wall's sheets and the girder's iron
            wall = Wall(typeof(turf_closed_wall), 6, 4);
            Waiting.NoSleep(EX_ACT(wall, EXPLODE_NONE, wall));
            Assert.IsType<turf_open_floor_plating>(At(6, 4));
            Assert.Equal(3f, Dm.each_cast<atom>(At(6, 4).contents).OfType<obj_item_stack_sheet_iron>().Sum(s => Dm.num(s.amount)));

            // light: prob(hardness), 40 for a wall, 10 for a reinforced wall
            for (int i = 0; i < 16; i++)
            {
                turf w = Wall(typeof(turf_closed_wall), 4 + i, 6);
                Waiting.NoSleep(EX_ACT(w, EXPLODE_LIGHT));
                if (At(4 + i, 6) is not turf_closed_wall)
                    walls++;
                turf rw = Wall(typeof(turf_closed_wall_r_wall), 4 + i, 8);
                Waiting.NoSleep(EX_ACT(rw, EXPLODE_LIGHT));
                if (At(4 + i, 8) is not turf_closed_wall_r_wall)
                    rwalls++;
            }
        });
        Assert.True(walls > rwalls, $"walls dismantled {walls}, reinforced walls {rwalls}");
        Assert.InRange(walls, 2, 14);
        Assert.InRange(rwalls, 0, 6);
    }

    [Fact]
    public void FloorsBecomePlatingOrSpaceAndADenseObjectShieldsTheFloorBelowDevastation()
    {
        var r = Room(6106);
        int broken = 0, intact = 0;
        r.In(() =>
        {
            // devastate: ScrapeAway(2): floor -> space; plating -> space
            Waiting.NoSleep(EX_ACT(At(4, 4), EXPLODE_DEVASTATE));
            Assert.IsType<turf_open_space>(At(4, 4));
            turf plating = (turf)Waiting.NoSleep(At(5, 4).ScrapeAway());
            Assert.IsType<turf_open_floor_plating>(plating);
            Waiting.NoSleep(EX_ACT(plating, EXPLODE_DEVASTATE));
            Assert.IsType<turf_open_space>(At(5, 4));

            // heavy: lattice-on-space, space, plating or a broken tile: never an intact floor
            for (int x = 4; x <= 20; x++)
            {
                Waiting.NoSleep(EX_ACT(At(x, 6), EXPLODE_HEAVY));
                turf t = At(x, 6);
                Assert.False(t is turf_open_floor_iron iron && !Dm.truthy(iron.broken), $"({x},6) is an intact floor after a heavy blast");
            }
            // light: half the tiles break (prob 50), none change type
            for (int x = 4; x <= 20; x++)
            {
                Waiting.NoSleep(EX_ACT(At(x, 8), EXPLODE_LIGHT));
                var t = Assert.IsType<turf_open_floor_iron>(At(x, 8));
                if (Dm.truthy(t.broken)) broken++; else intact++;
            }
            // a dense object on the floor shields it below devastation (floor.dm:89)
            Dm.@new<obj_structure_grille>(At(4, 10));
            Assert.True(Dm.truthy(At(4, 10).is_explosion_shielded(EXPLODE_HEAVY)));
            Assert.False(Dm.truthy(At(4, 10).is_explosion_shielded(EXPLODE_DEVASTATE)));
            Waiting.NoSleep(EX_ACT(At(4, 10), EXPLODE_HEAVY));
            var shielded = Assert.IsType<turf_open_floor_iron>(At(4, 10));
            Assert.False(Dm.truthy(shielded.broken));
        });
        Assert.True(broken > 0 && intact > 0, $"light: {broken} broken, {intact} intact");
    }

    [Fact]
    public void WindowsAndGrillesBreakItemsAndStructuresAreDamagedOrDestroyed()
    {
        var r = Room(6107);
        var (window, grille, rwindow, crowbar, toolbox, chair) = r.In(() => (
            Dm.@new<obj_structure_window_fulltile>(At(5, 5)),
            Dm.@new<obj_structure_grille>(At(6, 5)),
            Dm.@new<obj_structure_window_reinforced_fulltile>(At(7, 5)),
            Dm.@new<obj_item_crowbar>(At(8, 5)),
            Dm.@new<obj_item_storage_toolbox_mechanical>(At(9, 5)),
            Dm.@new<obj_structure_chair>(At(10, 5))));
        r.Step(Second);
        float crowbarMax = r.In(() => crowbar.max_integrity);
        r.In(() =>
        {
            Waiting.NoSleep(EX_ACT(window, EXPLODE_HEAVY));   // 100..250 bomb damage on a 50-integrity window
            Waiting.NoSleep(EX_ACT(grille, EXPLODE_DEVASTATE)); // INFINITY
            Waiting.NoSleep(EX_ACT(rwindow, EXPLODE_DEVASTATE));
            Waiting.NoSleep(EX_ACT(crowbar, EXPLODE_LIGHT));  // 10..90
            Waiting.NoSleep(EX_ACT(toolbox, EXPLODE_DEVASTATE));
            Waiting.NoSleep(EX_ACT(chair, EXPLODE_DEVASTATE));
        });
        r.Step(1);
        r.In(() =>
        {
            Assert.True(QDELETED(window), "the window broke");
            Assert.True(QDELETED(rwindow), "the reinforced window broke");
            // tg: the grille breaks at its integrity_failure, and its atom_break (grille.dm) leaves a broken grille of 20
            // integrity, so even a devastating blast leaves it broken, not gone
            Assert.False(QDELETED(grille));
            Assert.True(Dm.truthy(grille.broken) && !Dm.truthy(grille.density), "the grille is broken");
            Assert.True(QDELETED(toolbox), "the toolbox is destroyed");
            Assert.True(QDELETED(chair), "the chair is destroyed");
            Assert.False(QDELETED(crowbar));
            float lost = crowbarMax - Dm.num(crowbar.get_integrity());
            float bomb = Dm.num(crowbar.get_armor_rating(BOMB));
            Assert.InRange(lost, 10 * (1 - bomb / 100) - 0.01f, 90 * (1 - bomb / 100) + 0.01f);
        });
        Assert.NotEmpty(On<obj_item_shard>(r, 5, 5)); // the window's debris
        Assert.NotEmpty(On<obj_item_shard>(r, 7, 5));
    }

    [Fact]
    public void ADoorTakesOneSeverityMoreAndAPodDoorShrugsALightBlast()
    {
        var r = Room(6108);
        var (airlock, poddoor) = r.In(() => (Dm.@new<obj_machinery_door_airlock>(At(6, 6)), Dm.@new<obj_machinery_door_poddoor>(At(8, 6))));
        r.Step(Second);
        float max = r.In(() => airlock.max_integrity), podMax = r.In(() => poddoor.max_integrity);
        float bomb = r.In(() => Dm.num(airlock.get_armor_rating(BOMB)));
        r.In(() =>
        {
            Waiting.NoSleep(EX_ACT(airlock, EXPLODE_LIGHT)); // door.dm:755: a light blast hits it as a heavy one (100..250)
            Waiting.NoSleep(EX_ACT(poddoor, EXPLODE_LIGHT)); // poddoor.dm:217: nothing
        });
        r.Step(1);
        r.In(() =>
        {
            float lost = QDELETED(airlock) ? max : max - Dm.num(airlock.get_integrity());
            Assert.True(lost >= 100 * (1 - bomb / 100) - 0.01f, $"the airlock lost {lost} of {max} (bomb armour {bomb}): a heavy blast's damage");
            Assert.Equal(podMax, Dm.num(poddoor.get_integrity()));
        });
    }

    [Fact]
    public void ABlastThrowsWhatItLeavesAwayFromTheEpicenterAfterTheExActs()
    {
        var r = Room(6109);
        var (east, west, north) = r.In(() => (Dm.@new<obj_item_crowbar>(At(14, 8)), Dm.@new<obj_item_wrench>(At(10, 8)), Dm.@new<obj_item_crowbar>(At(12, 10))));
        Explode(r, 12, 8, 0, 0, 4); // all light: the floors stay (a heavy epicenter could open it to space, whose wind pulls)
        r.In(() =>
        {
            // the throw details of every turf in range: (max_range - dist, epicenter, max_range, severity)
            DmList details = At(14, 8).explosion_throw_details;
            Assert.Equal(4f, details.len);
            Assert.Equal(2f, Dm.num(details[1]));
            Assert.Same(At(12, 8), details[2]);
            Assert.Equal(4f, Dm.num(details[3]));
            Assert.Equal((float)EXPLODE_LIGHT, Dm.num(details[4]));
        });
        // DV416: they fly arcs (the server's own segments), off the floor, and rest where they land
        double highest = 0;
        for (int i = 0; i < 3 * Second; i++)
        {
            r.Step();
            r.In(() =>
            {
                Anomaly.Core.Motion.FlightArc arc = east.dm_flight;
                if (arc.IsMoving)
                    highest = Math.Max(highest, arc.At(r.World.Tick).Lift);
            });
        }
        Assert.True(highest > 4, "the east crowbar flew an arc off the floor: " + highest);
        r.In(() =>
        {
            Assert.False(east.dm_flight.IsMoving);
            Assert.Equal(0, east.dm_flight.Lift0, 2);
            Assert.Null(At(14, 8).explosion_throw_details);
            Assert.True(east.x > 14, $"the east crowbar flew east: x {east.x}");
            Assert.True(west.x < 10, $"the west wrench flew west: x {west.x}");
            Assert.True(north.y > 10, $"the north crowbar flew north: y {north.y}");
        });
    }

    [Fact]
    public void TheFlameRangeQueuesHotspotsOnOpenTurfsAndTheBlastIsHeardAndShakesTheListener()
    {
        var r = Room(6110);
        var bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        r.Sounds.Heard.Clear();
        var looks = new RecordingSinks(r.World);
        PresentationSinks.Of(r.World).Appearance = looks;
        r.In(() => Waiting.Detach(explosion(At(12, 8), 0, 1, 3, flame_range: 3, flash_range: 0, adminlog: FALSE, ignorecap: TRUE)));
        r.AssertNoRuntimes();
        r.In(() =>
        {
            Assert.True(SSexplosions.flameturf.len > 0, "prob(40) of the open turfs within the flame range");
            foreach (turf t in Dm.each_cast<turf>(SSexplosions.flameturf))
                Assert.True(CHEAP_HYPOTENUSE(t.x, t.y, 12, 8) < 3 && !isspaceturf(t) && !Dm.truthy(t.density));
        });
        // shake_the_room: Bob is 8.9 tiles away, within near_distance (3) + world.view (7) - 2 = 8 by get_dist 8: the near sound
        Assert.Contains(r.Sounds.Heard, h => h.Target == bob && h.File != null && h.File.StartsWith("sound/effects/explosion/explosion", StringComparison.Ordinal) && h.Volume > 0);
        // shake_camera(Bob, 1.5 s, sqrt(3 / 9) = 0.58 tiles): the client's pixel_x/pixel_y steps to the animate sink, the last one
        // back to where the camera was
        var shake = looks.Lines.Where(l => l.Contains("animate", StringComparison.Ordinal) && l.Contains("pixel_x", StringComparison.Ordinal)).ToList();
        Assert.True(shake.Count >= 2, string.Join("\n", looks.Lines));
        Assert.Equal(0f, r.In(() => bob.client.pixel_x));
        Assert.Equal(0f, r.In(() => bob.client.pixel_y));
        r.Step(1);
        Assert.False(r.In(() => Dm.truthy(SSexplosions.is_exploding())));
    }

    [Fact]
    public void AQueuedTurfThatAnExActReplacesKeepsItsFlamesAndThrowsOnTheNewTurf()
    {
        var r = Room(6112);
        obj_item crowbar = r.In(() => Dm.@new<obj_item_crowbar>(At(13, 8)));
        r.In(() =>
        {
            turf t = At(13, 8);
            Dm.append(ref SSexplosions.flameturf, t);
            t.explosion_throw_details = Dm.list(3, At(12, 8), 4, EXPLODE_LIGHT);
            Dm.append(SSexplosions.held_throwturf, t);
            // TURF-IDENTITY: tg's list entry is the location; the port's entry becomes the turf that replaced it
            turf plating = (turf)Waiting.NoSleep(t.ScrapeAway());
            Assert.Same(plating, SSexplosions.flameturf[1]);
            Assert.Same(plating, SSexplosions.held_throwturf[1]);
            Assert.Equal(4f, plating.explosion_throw_details.len); // ChangeTurf carries the details (change_turf.dm:138)
            Dm.append(ref SSexplosions.lowturf, At(5, 5)); // something to process, so the fire runs
        });
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.Null(At(13, 8).explosion_throw_details);
            Assert.True(crowbar.x > 13, $"the crowbar on the replaced turf was thrown east: x {crowbar.x}");
        });
    }

    [Fact]
    public void TheSameSeedGivesTheSameStateHash()
    {
        string Run()
        {
            var r = Room(6111);
            r.In(() =>
            {
                Wall(typeof(turf_closed_wall), 14, 8);
                Dm.@new<obj_structure_window_reinforced_fulltile>(At(10, 10));
                Dm.@new<obj_item_crowbar>(At(13, 9));
                Dm.@new<obj_item_storage_toolbox_mechanical>(At(11, 7));
            });
            Explode(r, 12, 8, 1, 2, 5, flame: 4);
            r.Step(5 * Second);
            return r.In(() => r.World.ComputeStateHash().ToString());
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void FixturesOfAFloorBlownIntoSpaceAndTheLampOfAFallenWallDoNotStayHanging()
    {
        // DV178: tg leaves a wired light frame on the fallen wall and the floor fixtures in space; the 3D view drew them in the air
        var r = Room(6199);
        obj_machinery_light lamp = null;
        obj_machinery_holopad pad = null;
        r.In(() =>
        {
            Wall(typeof(turf_closed_wall), 10, 9);
            lamp = Dm.@new<obj_machinery_light_directional_north>(At(10, 8));
            Waiting.NoSleep(lamp.find_and_mount_on_atom());
            pad = Dm.@new<obj_machinery_holopad>(At(13, 8));
        });
        r.Step(Second);
        r.In(() => Waiting.NoSleep(At(10, 9).ScrapeAway()));
        r.In(() => Waiting.NoSleep(At(13, 8).ChangeTurf(typeof(turf_open_space))));
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(QDELETED(lamp));
            Assert.Empty(Dm.each_cast<atom>(At(10, 8).contents).OfType<obj_structure_light_construct>());
            Assert.True(QDELETED(pad));
            Assert.DoesNotContain(Dm.each_cast<atom>(At(13, 8).contents), a => a is obj o && Dm.truthy(o.anchored));
        });
        r.AssertNoRuntimes();
    }
}
