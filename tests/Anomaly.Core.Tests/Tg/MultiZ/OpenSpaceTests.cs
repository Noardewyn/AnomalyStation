using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Motion;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tests.Tg.M3Maps.T2;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.MultiZ;

/// <summary>
/// D325: tg's open space and falls (openspace.dm, turf.dm zFall/zImpact, living.dm onZImpact/ZImpactDamage), on the fixture
/// tests/fixtures/maps/t2/multiz.dmm: two linked levels, a 3 x 3 room on each, open space at (3, 3, 2) over the lower room.
/// </summary>
public class OpenSpaceTests
{
    private static World Boot(out TgTestWorld.Log log) => T2Maps.Boot("multiz", out log);

    /// <summary>DV435: ticks a fall of one level (2 m, 0.64 s at 20 ticks a second) takes to reach the floor, with a margin.</summary>
    internal const int LandingTicks = 15;

    [Fact]
    public void TheTwoLevelsAreOneStackAndOpenSpaceIsTransparentOverTheLowerRoom()
    {
        World world = Boot(out TgTestWorld.Log log);
        using (world.Enter())
        {
            Assert.Empty(log.Runtimes);
            turf hole = locate(3, 3, 2);
            Assert.IsType<turf_open_openspace>(hole);
            Assert.Same(hole, GET_TURF_ABOVE(locate(3, 3, 1)));
            Assert.Same(locate(3, 3, 1), GET_TURF_BELOW(hole));
            Assert.True(istransparentturf(hole)); // LateInitialize's ADD_TURF_TRANSPARENCY
            Assert.False(istransparentturf(locate(2, 2, 2)));
            // update_plane_tracking: tg's stack from the top level down; the plane offsets stay 0 (DV367)
            Assert.Equal(new object[] { 2f, 1f }, SSmapping.get_connected_levels(1f).Items().ToArray());
            Assert.Equal(new object[] { 2f, 1f }, SSmapping.get_connected_levels(2f).Items().ToArray());
            Assert.Equal(0f, SSmapping.max_plane_offset);
            // vertical atmos: the open space and the floor under it share air (LINDA's cardinals_multiz)
            Assert.True(Dm.@in(locate(3, 3, 1), ((turf_open)hole).atmos_adjacent_turfs));
            Assert.False(Dm.@in(locate(2, 2, 1), ((turf_open)locate(2, 2, 2)).atmos_adjacent_turfs));
        }
    }

    [Fact]
    public void HotGasOnTheLowerFloorRisesThroughTheOpenSpaceIntoTheRoomAbove()
    {
        World world = Boot(out TgTestWorld.Log log);
        TgTestWorld.RunLevel(world, RUNLEVEL_GAME); // SSair runs in the round, not in the lobby
        turf_open below, hole, upper;
        float before, molesBefore;
        using (world.Enter())
        {
            below = (turf_open)locate(2, 2, 1);
            hole = (turf_open)locate(3, 3, 2);
            upper = (turf_open)locate(4, 4, 2); // a corner of the upper room, not over the hole
            before = Dm.num(upper.air.temperature);
            molesBefore = Dm.num(upper.air.total_moles());
            below.air = SSair.parse_gas_string("o2=22;n2=82;plasma=200;TEMP=1000") as datum_gas_mixture;
            SSair.add_to_active(below);
        }
        world.Step(20 * 20);
        using (world.Enter())
        {
            // LINDA shares across levels (cardinals_multiz): the plasma climbs a level and spreads along the upper room
            Assert.True(Dm.num(hole.air.temperature) > before + 20, "the open space warms");
            Assert.True(Dm.num(upper.air.temperature) > before + 20, "the room above warms");
            Assert.True(Dm.num(upper.air.total_moles()) > molesBefore, "the plasma reaches the room above");
            Assert.Empty(log.Runtimes);
        }
    }

    [Fact]
    public void AnItemPutOnOpenSpaceFallsToTheFloorBelow()
    {
        World world = Boot(out TgTestWorld.Log log);
        using (world.Enter())
        {
            obj_item_wrench wrench = Dm.@new<obj_item_wrench>(locate(2, 2, 2));
            Waiting.NoSleep(wrench.forceMove(locate(3, 3, 2)));
            Assert.Same(locate(3, 3, 1), wrench.loc);
            Assert.Equal(FALSE, Dm.num(wrench.currently_z_moving));
            // an item made on open space falls when it is initialized (COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON)
            obj_item_crowbar crowbar = Dm.@new<obj_item_crowbar>(locate(3, 3, 2));
            Assert.Same(locate(3, 3, 1), crowbar.loc);
            Assert.Empty(log.Runtimes);
        }
    }

    [Fact]
    public void AFullTileWindowStackedByTheDevMenuStandsOnTheOpenSpaceItWasGiven()
    {
        // the owner (2026-11-10): a window stuck on the top of one below stands on open space, no floor between; tg's anchored
        // structures do not fall (can_z_move without ZMOVE_ALLOW_ANCHORED)
        World world = Boot(out TgTestWorld.Log log);
        using (world.Enter())
        {
            DevCommands.Spawn(typeof(turf_open_openspace), locate(2, 2, 2), 1, 0, out _);
            Assert.IsType<turf_open_openspace>(locate(2, 2, 2));
            List<atom> made = DevCommands.Spawn(typeof(obj_structure_window_reinforced_fulltile), locate(2, 2, 2), 1, 0, out _);
            world.Step(2);
            Assert.Same(locate(2, 2, 2), Assert.Single(made).loc);
            Assert.Empty(log.Runtimes);
        }
    }

    [Fact]
    public void AHumanWalkingOntoOpenSpaceFallsALevelAndHurtsBothLegsAndIsKnockedDown()
    {
        World world = Boot(out TgTestWorld.Log log);
        using (world.Enter())
        {
            mob_living_carbon_human bob = Dm.@new<mob_living_carbon_human>(locate(2, 3, 2));
            world.Step(2);
            float before = Dm.num(bob.get_brute_loss());
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.Move(locate(3, 3, 2), EAST))));
            Assert.Same(locate(3, 3, 1), bob.loc);
            Assert.Equal(FALSE, Dm.num(bob.currently_z_moving));
            // DV435: tg's impact waits for the floor (a level, 2 m, falls in 0.64 s)
            Assert.Equal(before, Dm.num(bob.get_brute_loss()));
            Assert.True(FlightSeam.FallsToImpact(bob));
            world.Step(LandingTicks);
            Assert.False(FlightSeam.FallsToImpact(bob));
            // living.dm:107 (5 * levels) ** 1.5 = 11.18, round(11.18 / 2) = 5 on each leg standing up
            Assert.Equal(10f, Dm.num(bob.get_brute_loss()) - before);
            var left = (obj_item_bodypart)bob.get_bodypart(BODY_ZONE_L_LEG);
            var right = (obj_item_bodypart)bob.get_bodypart(BODY_ZONE_R_LEG);
            Assert.Equal(5f, left.brute_dam);
            Assert.Equal(5f, right.brute_dam);
            Assert.InRange(Dm.num(bob.AmountKnockdown()), 4 * SECONDS, 5 * SECONDS);
            Assert.Empty(log.Runtimes);
        }
    }

    [Fact]
    public void AParkourMasterLandsAShortFallUnhurt()
    {
        World world = Boot(out TgTestWorld.Log log);
        using (world.Enter())
        {
            mob_living_carbon_human bob = Dm.@new<mob_living_carbon_human>(locate(2, 3, 2));
            ADD_TRAIT(bob, TRAIT_FREERUNNING, "test");
            world.Step(2);
            float before = Dm.num(bob.get_brute_loss());
            Waiting.NoSleep(bob.Move(locate(3, 3, 2), EAST));
            Assert.Same(locate(3, 3, 1), bob.loc);
            world.Step(LandingTicks);
            Assert.Equal(before, Dm.num(bob.get_brute_loss()));
            // living.dm:103: no catlike instinct, so a 4 second knockdown and a spin
            Assert.InRange(Dm.num(bob.AmountKnockdown()), 3 * SECONDS, 4 * SECONDS);
            Assert.Empty(log.Runtimes);
        }
    }

    [Fact]
    public void ACatwalkOverOpenSpaceHoldsAWalker()
    {
        World world = Boot(out TgTestWorld.Log log);
        using (world.Enter())
        {
            Dm.@new<obj_structure_lattice_catwalk>(locate(3, 3, 2));
            mob_living_carbon_human bob = Dm.@new<mob_living_carbon_human>(locate(2, 3, 2));
            world.Step(2);
            Waiting.NoSleep(bob.Move(locate(3, 3, 2), EAST));
            Assert.Same(locate(3, 3, 2), bob.loc); // the catwalk's BLOCK_Z_OUT_DOWN (zPassOut)
            Assert.Empty(log.Runtimes);
        }
    }

    [Fact]
    public void APointerSeesThroughOpenSpaceAndUpToTheFloorOverItAndTheArrowLandsOnThatLevel()
    {
        World world = Boot(out TgTestWorld.Log log);
        using (world.Enter())
        {
            // DV393: tg's view() is one level; the 3D client shows the lower room through the open space at (3, 3, 2) and the floor above as the ceiling
            mob_living_carbon_human above = Dm.@new<mob_living_carbon_human>(locate(2, 3, 2));
            mob_living_carbon_human below = Dm.@new<mob_living_carbon_human>(locate(2, 2, 1));
            Assert.True(PointGeometry.SeenThroughLevels(above, locate(3, 3, 1), 7f)); // down through the hole
            Assert.False(PointGeometry.SeenThroughLevels(above, locate(2, 2, 1), 7f)); // under the floor of z 2
            Assert.True(PointGeometry.SeenThroughLevels(below, locate(2, 2, 2), 7f)); // the floor over it, its ceiling
            obj_item_wrench upstairs = Dm.@new<obj_item_wrench>(locate(2, 2, 2));
            Assert.False(PointGeometry.SeenThroughLevels(below, upstairs, 7f)); // a thing on that floor is hidden by it
            Assert.False(PointGeometry.SeenThroughLevels(above, locate(2, 2, 2), 7f)); // the own level is view()'s
            // along the click's aim (DV393): straight up from (2, 2, 1) at the floor over it, and down at an angle through the hole onto (4, 3, 1), a tile past it
            below.dm_attack_modifiers = Dm.params2list(ClientSeam.AimParam(64, 64, 26, 0, 89));
            Assert.True(PointGeometry.SeenThroughLevels(below, locate(2, 2, 2), 7f));
            above.dm_attack_modifiers = Dm.params2list(ClientSeam.AimParam(64, 96, 26, 90, -Math.Atan2(58, 64) * 180 / Math.PI));
            Assert.True(PointGeometry.SeenThroughLevels(above, locate(4, 3, 1), 7f));
            Assert.False(PointGeometry.SeenThroughLevels(above, locate(2, 2, 1), 7f)); // off the aim, under the floor
            above.dm_attack_modifiers = null;
            // the arrow's point: aimed from 26 px over (2, 3, 2)'s floor east and down onto the middle of (3, 3, 1), a level (32 px) lower
            turf target = locate(3, 3, 1);
            above.dm_attack_target = target;
            above.dm_attack_modifiers = Dm.params2list(ClientSeam.AimParam(64, 96, 26, 90, -Math.Atan2(58, 32) * 180 / Math.PI));
            Assert.True(PointGeometry.TryAimedPoint(above, target, out double x, out double y, out double h));
            Assert.Equal(96, x, 1);
            Assert.Equal(96, y, 1);
            Assert.Equal(-32, h, 1); // the lower floor, under the pointer's
            Assert.Empty(log.Runtimes);
        }
    }

    // DV394: from an eye 20.8 px over level 2's floor in (2, 3, 2), east and down 39.8 degrees: the line reaches level 2's floor 25 px out
    [Theory]
    [InlineData(64, 4, 1)] // at x 89, over the open space (3, 3, 2): on into level 1, whose floor it reaches 38 px further, at x 127 in (4, 3, 1)
    [InlineData(50, 2, 2)] // at x 75, still over (2, 3, 2)'s floor: it hits that floor
    [InlineData(64, 5, 2, 1)] // fired up 24.1 degrees from (2, 3, 1): level 1's top (no ceiling under level 2) at x 89, under the open space: on up into level 2, to its east wall (5, 3, 2)
    public void AnAimedShotFliesOnThroughOpenSpaceIntoTheLevelBelowOrAbove(int eyeX, int impactX, int impactZ, int shooterZ = 2)
    {
        World world = Boot(out TgTestWorld.Log log);
        obj_projectile_beam_emitter_hitscan p;
        using (world.Enter())
        {
            mob_living_carbon_human alice = Dm.@new<mob_living_carbon_human>(locate(2, 3, shooterZ));
            p = Dm.@new<obj_projectile_beam_emitter_hitscan>(locate(2, 3, shooterZ));
            p.firer = alice;
            p.fired_from = alice;
            string aim = "icon-x=16;icon-y=16;left=1;button=left;" + ClientSeam.AimParam(eyeX, 96, 20.8, 90, (shooterZ == 2 ? -Math.Atan2(20.8, 25) : Math.Atan2(11.2, 25)) * 180 / Math.PI);
            Waiting.NoSleep(p.aim_projectile(locate(4, 3, shooterZ), alice, Dm.params2list(aim), 0f));
            Waiting.NoSleep(p.fire());
        }
        world.Step(2);
        using (world.Enter())
        {
            Assert.Equal((impactX, 3, impactZ), ((int)Dm.num(p.last_impact_turf.x), (int)Dm.num(p.last_impact_turf.y), (int)Dm.num(p.last_impact_turf.z)));
            Assert.Empty(log.Runtimes);
        }
    }

    // DV595: the shot of the case above that hit (2, 3, 2)'s floor at x 75: made a glass floor, a laser goes on through it into level 1
    [Fact]
    public void AnAimedLaserFliesThroughAGlassFloorIntoTheLevelBelow()
    {
        World world = Boot(out TgTestWorld.Log log);
        obj_projectile_beam_emitter_hitscan p;
        using (world.Enter())
        {
            Waiting.NoSleep(locate(2, 3, 2).ChangeTurf(typeof(turf_open_floor_glass)));
            mob_living_carbon_human alice = Dm.@new<mob_living_carbon_human>(locate(2, 3, 2));
            p = Dm.@new<obj_projectile_beam_emitter_hitscan>(locate(2, 3, 2));
            p.firer = alice;
            p.fired_from = alice;
            string aim = "icon-x=16;icon-y=16;left=1;button=left;" + ClientSeam.AimParam(50, 96, 20.8, 90, -Math.Atan2(20.8, 25) * 180 / Math.PI);
            Waiting.NoSleep(p.aim_projectile(locate(4, 3, 2), alice, Dm.params2list(aim), 0f));
            Waiting.NoSleep(p.fire());
        }
        world.Step(2);
        using (world.Enter())
        {
            Assert.Equal(1f, Dm.num(p.last_impact_turf.z));
            Assert.Empty(log.Runtimes);
        }
    }

    // DV97 (the owner, 2026-11-20): fired up 24.1 degrees north from (2, 3, 1), the line reaches level 1's top 25 px out, at y 121 in (2, 4, 1), under
    // the floor of (2, 4, 2): the shot hits that floor from under it (it was deleted there unseen), and its impact is on that floor's level, at its floor.
    [Fact]
    public void AnAimedShotUpUnderTheLevelAboveHitsItsFloor()
    {
        World world = Boot(out TgTestWorld.Log log);
        obj_projectile_beam_laser p;
        using (world.Enter())
        {
            CeilingSystem.Of(world).Initialize();
            mob_living_carbon_human alice = Dm.@new<mob_living_carbon_human>(locate(2, 3, 1));
            p = Dm.@new<obj_projectile_beam_laser>(locate(2, 3, 1));
            p.firer = alice;
            p.fired_from = alice;
            string aim = "icon-x=16;icon-y=16;left=1;button=left;" + ClientSeam.AimParam(64, 96, 20.8, 0, Math.Atan2(11.2, 25) * 180 / Math.PI);
            Waiting.NoSleep(p.aim_projectile(locate(2, 4, 1), alice, Dm.params2list(aim), 0f));
            Waiting.NoSleep(p.fire());
        }
        bool Landed()
        {
            using (world.Enter())
                return p.last_impact_turf != null;
        }
        for (int tick = 0; tick < 30 && !Landed(); tick++)
            world.Step(1); // until the shot lands, while its impact effect is still there
        using (world.Enter())
        {
            Assert.Equal((2, 4, 2), ((int)Dm.num(p.last_impact_turf.x), (int)Dm.num(p.last_impact_turf.y), (int)Dm.num(p.last_impact_turf.z)));
            obj_effect impact = Assert.Single(Dm.each<obj_effect_temp_visual_impact_effect>().ToList());
            Assert.Equal(2f, Dm.num(impact.z));
            Assert.Equal(0f, impact.dm_origin_h, 3);
            // DV577: its dent is on the top of the cell it flew in, the underside of that floor
            Assert.Equal(new[] { UP }, Dm.each<mutable_appearance>((locate(2, 4, 1) as turf_open).dm_dent_decals).Select(d => Dm.num(d.dir)).ToArray());
            Assert.Null((locate(2, 4, 2) as turf_open).dm_dent_decals);
            Assert.Empty(log.Runtimes);
        }
    }

    // DV578 (the owner, 2026-11-21): a rocket fired up into the floor of the level above goes off under it, on level 1 where the shooter sees it,
    // and that floor, the ceiling of the cell, takes the blast's epicentre severity
    [Fact]
    public void ARocketUpIntoTheFloorAboveGoesOffUnderItAndBreaksThatFloor()
    {
        World world = Boot(out TgTestWorld.Log log);
        turf before;
        using (world.Enter())
        {
            CeilingSystem.Of(world).Initialize();
            before = locate(2, 4, 2);
            mob_living_carbon_human alice = Dm.@new<mob_living_carbon_human>(locate(2, 3, 1));
            var p = Dm.@new<obj_projectile_bullet_rocket>(locate(2, 3, 1));
            p.firer = alice;
            p.fired_from = alice;
            string aim = "icon-x=16;icon-y=16;left=1;button=left;" + ClientSeam.AimParam(64, 96, 20.8, 0, Math.Atan2(11.2, 25) * 180 / Math.PI);
            Waiting.NoSleep(p.aim_projectile(locate(2, 4, 1), alice, Dm.params2list(aim), 0f));
            Waiting.NoSleep(p.fire());
        }
        var blasts = new List<(int Z, bool Shot)>();
        for (int tick = 0; tick < 20; tick++)
        {
            world.Step(1);
            using (world.Enter())
                blasts.AddRange(Dm.each<obj_effect_explosion>().Select(e => ((int)Dm.num(e.z), e.dm_shot)));
        }
        using (world.Enter())
        {
            Assert.NotEmpty(blasts);
            Assert.All(blasts, b => Assert.True(b.Z == 1 && b.Shot, $"a blast drawn on level {b.Z}"));
            turf over = locate(2, 4, 2);
            Assert.True(!ReferenceEquals(over, before) || over is turf_open_floor { broken: > 0 }, $"the floor over the blast is still {over.GetType().Name}");
            Assert.Empty(log.Runtimes);
        }
    }

    // DV525: a ladder from (2, 4, 1) up to (2, 4, 2) cuts its hatch out of (2, 4, 2)'s floor (8 px north of its centre is in the opening, 8 px south
    // is not). From an eye 20.8 px up at (4, 4, z), 8 px north or south of its centre, west at the hatch: down from level 2 the line reaches the
    // floor at x 64 and goes on below to the west wall (1, 4, 1); up from level 1 it reaches the top at x 64 and goes on to (1, 4, 2).
    [Theory]
    [InlineData(2, 8, 1)]
    [InlineData(2, -8, 2)] // beside the opening: the hole's floor stops it
    [InlineData(1, 8, 2)]
    public void AnAimedShotFliesThroughALadderHatch(int shooterZ, int northOffset, int impactZ)
    {
        World world = Boot(out TgTestWorld.Log log);
        obj_projectile_beam_emitter_hitscan p;
        using (world.Enter())
        {
            Dm.@new<obj_structure_ladder>(locate(2, 4, 1));
            Dm.@new<obj_structure_ladder>(locate(2, 4, 2));
        }
        world.Step(1);
        using (world.Enter())
        {
            Assert.Single(Dm.each<obj_effect_abstract_ladder_hole>(locate(2, 4, 2)));
            mob_living_carbon_human alice = Dm.@new<mob_living_carbon_human>(locate(4, 4, shooterZ));
            p = Dm.@new<obj_projectile_beam_emitter_hitscan>(locate(4, 4, shooterZ));
            p.firer = alice;
            p.fired_from = alice;
            double pitch = (shooterZ == 2 ? -Math.Atan2(20.8, 64) : Math.Atan2(11.2, 64)) * 180 / Math.PI;
            string aim = "icon-x=16;icon-y=16;left=1;button=left;" + ClientSeam.AimParam(128, 128 + northOffset, 20.8, 270, pitch);
            Waiting.NoSleep(p.aim_projectile(locate(2, 4, shooterZ), alice, Dm.params2list(aim), 0f));
            Waiting.NoSleep(p.fire());
        }
        world.Step(2);
        using (world.Enter())
        {
            int impactX = impactZ == shooterZ ? 2 : 1;
            Assert.Equal((impactX, 4, impactZ), ((int)Dm.num(p.last_impact_turf.x), (int)Dm.num(p.last_impact_turf.y), (int)Dm.num(p.last_impact_turf.z)));
            Assert.Empty(log.Runtimes);
        }
    }

    [Fact]
    public void OurCeilingsLeaveOutEveryTurfWithALevelAbove()
    {
        World world = Boot(out _);
        using (world.Enter())
        {
            CeilingSystem ceilings = CeilingSystem.Of(world);
            ceilings.Initialize();
            Assert.False(ceilings.At(2, 2, 1).Exists); // the floor of z 2 is its boundary
            Assert.False(ceilings.At(3, 3, 1).Exists); // under the open space
            Assert.True(ceilings.At(2, 2, 2).Exists); // the top level keeps its roof
            Assert.True(ceilings.At(3, 3, 2).Exists); // its open space too: in tg nothing above it lets the air out
            Assert.False(ceilings.Set(2, 2, 1, CeilingState.Of(CeilingState.Plating)));
        }
    }
}
