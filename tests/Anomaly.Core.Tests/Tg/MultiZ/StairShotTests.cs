using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.T2;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.MultiZ;

/// <summary>
/// DV526: aimed shots and the stairs' flight the 3D client draws (DV370), on the fixture tests/fixtures/maps/t2/multiz (a 3 x 3 room on two levels,
/// open space at (3, 3, 2)) with north stairs put at (3, 3, 1), under the open space: the flight rises from y 80 px (the tile's south edge) to a
/// level's 32 px at y 112.
/// </summary>
public class StairShotTests
{
    private static World Boot(out TgTestWorld.Log log, bool stairs = true)
    {
        World world = T2Maps.Boot("multiz", out log);
        if (stairs)
            using (world.Enter())
                Dm.@new<obj_structure_stairs_north>(locate(3, 3, 1));
        return world;
    }

    private static obj_projectile_beam_emitter_hitscan Shoot(World world, int x, int y, int z, double eyeX, double eyeY, double eyeUp, double yaw, double pitch)
    {
        obj_projectile_beam_emitter_hitscan p;
        using (world.Enter())
        {
            mob_living_carbon_human alice = Dm.@new<mob_living_carbon_human>(locate(x, y, z));
            p = Dm.@new<obj_projectile_beam_emitter_hitscan>(locate(x, y, z));
            p.firer = alice;
            p.fired_from = alice;
            string aim = "icon-x=16;icon-y=16;left=1;button=left;" + ClientSeam.AimParam(eyeX, eyeY, eyeUp, yaw, pitch);
            Waiting.NoSleep(p.aim_projectile(locate(3, 4, z), alice, Dm.params2list(aim), 0f));
            Waiting.NoSleep(p.fire());
        }
        world.Step(2);
        return p;
    }

    private static (int X, int Y, int Z) Impact(World world, obj_projectile p)
    {
        using (world.Enter())
            return ((int)Dm.num(p.last_impact_turf.x), (int)Dm.num(p.last_impact_turf.y), (int)Dm.num(p.last_impact_turf.z));
    }

    [Theory]
    [InlineData(true, 3, 3)] // a level line 20.8 px up from (3, 2, 1) north meets the flight at y 100.8
    [InlineData(false, 3, 5)] // with no stairs it flies on to the north wall
    public void ALevelShotUpTheStairsHitsTheFlight(bool stairs, int impactX, int impactY)
    {
        World world = Boot(out TgTestWorld.Log log, stairs);
        obj_projectile p = Shoot(world, 3, 2, 1, 96, 64, 20.8, 0, 0);
        Assert.Equal((impactX, impactY, 1), Impact(world, p));
        Assert.Empty(log.Runtimes);
    }

    [Fact]
    public void AShotFromAnEyeHighOnTheFlightStartsOnTheLevelAbove()
    {
        // an eye 40 px over level 1's floor at y 104 (the flight is 24 px high there): 8 px over level 2's, level north to level 2's north wall
        World world = Boot(out TgTestWorld.Log log);
        obj_projectile p = Shoot(world, 3, 3, 1, 96, 104, 40, 0, 0);
        Assert.Equal((3, 5, 2), Impact(world, p));
        Assert.Empty(log.Runtimes);
    }

    [Fact]
    public void AMobOnTheFlightIsHitWhereItIsDrawn()
    {
        World world = Boot(out TgTestWorld.Log log);
        using (world.Enter())
        {
            // Bob in the middle of the flight, drawn 16 px up; a level line north at 37 px (his head's rows 16 px up) and one at 6 px (his
            // legs' rows on the floor, under the drawn flight)
            mob_living_carbon_human bob = Dm.@new<mob_living_carbon_human>(locate(3, 3, 1));
            Assert.True(ShotGeometry.TryFlight(bob.loc as turf, 96, 96, out double lift, out int dir));
            Assert.Equal(16, lift, 3);
            Assert.Equal(1, dir);
            obj_projectile Line(float h)
            {
                var p = Dm.@new<obj_projectile_beam_emitter_hitscan>(locate(3, 2, 1));
                p.dm_aimed = p.dm_shot = true;
                p.dm_origin_x = 96;
                p.dm_origin_y = 64;
                p.dm_origin_h = h;
                p.set_angle(0);
                return p;
            }
            Assert.Equal(ShotGeometry.Crossing.Hit, ShotGeometry.Cross(Line(37), bob, out string zone));
            Assert.Equal(BODY_ZONE_HEAD, zone);
            Assert.Equal(ShotGeometry.Crossing.Miss, ShotGeometry.Cross(Line(6), bob, out _));
        }
        Assert.Empty(log.Runtimes);
    }
}
