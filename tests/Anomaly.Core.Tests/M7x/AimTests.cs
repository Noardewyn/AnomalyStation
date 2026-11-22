using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7x;

/// <summary>
/// M7x slice A (D177): a shot aimed by a player's camera (the click params' <c>anomaly-aim</c>, DV94) starts at the eye and flies along the
/// crosshair (aim_projectile, calculate_projectile_angle_and_pixel_offsets), scatters in a 3D cone (DV96), stops where its line reaches the
/// floor (DV97), and hurts the body part it crosses or flies past the body to what is behind (DV95), through tg's whole hit chain.
/// </summary>
public class AimTests
{
    private const int Width = 14, Height = 7, LineY = 4, From = 3, Target = 8;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(Width, Height, seed: seed);
        r.Step(20);
        return r;
    }

    private static string Params(double east, double north, double up, double yaw, double pitch)
        => "icon-x=16;icon-y=16;left=1;button=left;" + ClientSeam.AimParam(east, north, up, yaw, pitch);

    // tg's aim_projectile with the click's params, then fire() (a gun's casing does both: fire_casing, throw_proj)
    private static T Aim<T>(AudioRoom r, mob shooter, atom target, string @params, float deviation = 0, bool fire = true) where T : obj_projectile => r.In(() =>
    {
        T p = Dm.@new<T>(get_turf(shooter));
        p.firer = shooter;
        p.fired_from = shooter;
        Waiting.NoSleep(p.aim_projectile(target, shooter, params2list(@params), deviation));
        if (fire)
            Waiting.NoSleep(p.fire());
        return p;
    });

    [Fact]
    public void UpwardShotStopsAtTheCeilingAndLeavesItAndTheFloorWhole()
    {
        var r = Room(7850);
        var alice = r.Dummy(From, LineY);
        turf floor = r.In(() => locate(7, LineY, 1));
        r.In(() => CeilingSystem.Of(r.World).Initialize());
        Aim<obj_projectile_beam_emitter_hitscan>(r, alice, r.In(() => locate(Width, LineY, 1)), Params(From * 32, LineY * 32, 20.8, 90, 5));
        r.Step();
        // D295: tg's floors take no bullet damage, and neither does the ceiling's floor block
        Assert.Equal(Anomaly.Core.Motion.CeilingState.Of(Anomaly.Core.Motion.CeilingState.Plating), r.In(() => CeilingSystem.Of(r.World).At(7, LineY, 1)) with { Supported = true });
        Assert.Same(floor, r.In(() => locate(7, LineY, 1)));
        r.AssertNoRuntimes();
    }

    // The owner (2026-11-20): the ceiling is where the server's wall height puts it. A laser from an eye 20.8 px up, east and up 6 degrees,
    // reaches a 2 m ceiling (32 px) 106.6 px out, at x 202.6 in (6, 4); a 3 m one (48 px) 258.8 px out, at x 354.8 in (11, 4).
    [Theory]
    [InlineData(2f, 6)]
    [InlineData(3f, 11)]
    public void AShotMeetsTheCeilingAtTheServersWallHeight(float wallHeight, int impactX)
    {
        var r = Room(7851);
        r.In(() => LevelHeight.Of(r.World).Set(wallHeight));
        var alice = r.Dummy(From, LineY);
        r.In(() => CeilingSystem.Of(r.World).Initialize());
        Aim<obj_projectile_beam_laser>(r, alice, r.In(() => locate(Width, LineY, 1)), Params(From * 32, LineY * 32, 20.8, 90, 6));
        r.Step(4);
        Assert.Equal(new[] { impactX }, r.In(() => Dm.each<obj_effect_temp_visual_impact_effect>().Select(e => (int)Dm.num(e.x)).ToArray()));
        r.AssertNoRuntimes();
    }

    // DV577 (the owner, 2026-11-20): shots dent the floor and the ceiling as tg's dent a wall (75%): a laser down 30 degrees meets the floor 36 px
    // out, in (4, 4), whose turf takes a dent on its floor (DOWN) where the line met it; one up 30 degrees meets the 2 m ceiling 19.4 px out, in
    // (4, 4) too, a dent on its top (UP). A changed ceiling takes its dents away.
    [Fact]
    public void ShotsDentTheFloorAndTheCeiling()
    {
        var r = Room(7852);
        var alice = r.Dummy(From, LineY);
        r.In(() => CeilingSystem.Of(r.World).Initialize());
        turf_open cell = r.In(() => locate(From + 1, LineY, 1) as turf_open);
        object[] Dents() => r.In(() => Dm.each<mutable_appearance>(cell.dm_dent_decals).Select(d => (object)(Dm.num(d.dir), d.pixel_w, d.pixel_z)).ToArray());
        for (int shot = 0; shot < 4; shot++)
        {
            Aim<obj_projectile_beam_laser>(r, alice, r.In(() => locate(Width, LineY, 1)), Params(From * 32, LineY * 32, 20.8, 90, -30));
            Aim<obj_projectile_beam_laser>(r, alice, r.In(() => locate(Width, LineY, 1)), Params(From * 32, LineY * 32, 20.8, 90, 30));
            r.Step(6);
        }
        object[] dents = Dents();
        Assert.Contains(((float)DOWN, 4f, 0f), dents); // 36 px out of the eye at x 96: x 132, 4 px east of (4, 4)'s centre
        Assert.Contains(((float)UP, -13f, 0f), dents); // 19.4 px out: x 115.4, 12.6 px west of it
        Assert.InRange(dents.Length, 2, 8);
        r.In(() => CeilingSystem.Of(r.World).Set(From + 1, LineY, 1, default));
        Assert.DoesNotContain(Dents(), d => ((ValueTuple<float, float, float>)d).Item1 == UP);
        Assert.Contains(((float)DOWN, 4f, 0f), Dents());
        r.AssertNoRuntimes();
    }

    // DV577 (the owner, 2026-11-20): what breaks a wall breaks the ceiling it hits too: tg's pulse (beams.dm:225, a heavy blast on the turf it hit)
    // up 30 degrees from (3, 4) meets the ceiling over (4, 4), which takes the blast; the floor under it stays as it was.
    [Fact]
    public void APulseBreaksTheCeilingItHitsNotTheFloorUnderIt()
    {
        var r = Room(7853);
        var alice = r.Dummy(From, LineY);
        r.In(() => CeilingSystem.Of(r.World).Initialize());
        Anomaly.Core.Motion.CeilingState before = r.In(() => CeilingSystem.Of(r.World).At(From + 1, LineY, 1));
        turf floor = r.In(() => locate(From + 1, LineY, 1));
        Aim<obj_projectile_beam_pulse>(r, alice, r.In(() => locate(Width, LineY, 1)), Params(From * 32, LineY * 32, 20.8, 90, 30));
        r.Step(10);
        Assert.NotEqual(before, r.In(() => CeilingSystem.Of(r.World).At(From + 1, LineY, 1)));
        Assert.Same(floor, r.In(() => locate(From + 1, LineY, 1)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAimParamsGiveTheAngleTheStartAndTheSlope()
    {
        AudioRoom r = Room(7811);
        mob_living_carbon_human alice = r.Dummy(From, LineY);
        turf east = r.In(() => locate(Width, LineY, 1));
        // the eye 5 pixels north of its tile's centre at 1.3 m, the crosshair 30 degrees east of north and 10 degrees down
        obj_projectile p = Aim<obj_projectile_beam_emitter>(r, alice, east, Params(From * 32, LineY * 32 + 5, 20.8, 30, -10), fire: false);
        Assert.True(r.In(() => p.dm_aimed));
        Assert.Equal(30f, r.In(() => Dm.num(p.angle)), 3); // not tg's icon-x angle toward the east wall
        Assert.Equal(5f, r.In(() => p.pixel_y));
        Assert.Equal(0f, r.In(() => p.pixel_x));
        Assert.Equal(LineY * 32 + 5f, r.In(() => p.dm_origin_y));
        Assert.Equal(20.8f, r.In(() => p.dm_origin_h), 3);
        Assert.Equal((float)Math.Tan(-10 * Math.PI / 180), r.In(() => p.dm_slope), 4);
        // an eye reported two tiles off is not taken: the shot starts from the source's tile
        obj_projectile far = Aim<obj_projectile_beam_emitter>(r, alice, east, Params(From * 32 + 70, LineY * 32, 20.8, 90, 0), fire: false);
        Assert.Equal(r.In(() => get_turf(alice)), r.In(() => far.loc));
        Assert.Equal(16f, r.In(() => far.pixel_x)); // clamped to the tile's edge
        // without the key tg's own aim: the icon-x geometry toward the target, an unaimed level line at a mob's middle once fired
        obj_projectile plain = Aim<obj_projectile_beam_emitter>(r, alice, east, "icon-x=16;icon-y=16;left=1;button=left");
        Assert.False(r.In(() => plain.dm_aimed));
        Assert.Equal(90f, r.In(() => Dm.num(plain.angle)), 3);
        Assert.Equal(ShotGeometry.UnaimedHeightPixels, r.In(() => plain.dm_origin_h));
        Assert.Equal(0f, r.In(() => plain.dm_slope));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TgsDeviationIsADeterministicConeOfItsSize()
    {
        (double, double, float) Draw(ulong seed, double deviation)
        {
            AudioRoom r = Room(seed);
            return r.In(() =>
            {
                (double yaw, double pitch) = ShotGeometry.Scatter(90, 0, deviation);
                return (yaw, pitch, Dm.rand()); // the next draw shows how many the cone took
            });
        }
        (double yaw, double pitch, float next) = Draw(7812, 8);
        Assert.Equal((yaw, pitch, next), Draw(7812, 8)); // the same seed, the same cone
        Assert.Equal(8, Math.Sqrt((yaw - 90) * (yaw - 90) + pitch * pitch), 6); // turned by |d| about a drawn direction
        Assert.Equal(8, Math.Sqrt((Draw(7812, -8).Item1 - 90) * (Draw(7812, -8).Item1 - 90) + Draw(7812, -8).Item2 * Draw(7812, -8).Item2), 6);
        // one draw for a deviation, none without: the draw after a zero deviation is the cone's own draw
        (double y0, double p0, float afterNone) = Draw(7812, 0);
        Assert.Equal((90.0, 0.0), (y0, p0));
        AudioRoom control = Room(7812);
        Assert.Equal(control.In(() => Dm.rand()), afterNone);
        Assert.NotEqual(afterNone, next);
        // the owner (2026-11-20): the server's spread scale shrinks the cone, drawing the same direction
        AudioRoom halved = Room(7812);
        halved.In(() => ShotSpread.Of(halved.World).Set(0.5f));
        (double hy, double hp) = halved.In(() => ShotGeometry.Scatter(90, 0, 8));
        Assert.Equal(4, Math.Sqrt((hy - 90) * (hy - 90) + hp * hp), 6);
        Assert.Equal(Math.Atan2(pitch, yaw - 90), Math.Atan2(hp, hy - 90), 6);
    }

    // A hitscan from the eye at 1.3 m on tile 3, heading east, in a room of its own: the turf its impact was made on.
    private static float ImpactX(ulong seed, double pitch)
    {
        AudioRoom r = Room(seed);
        mob_living_carbon_human alice = r.Dummy(From, LineY);
        turf east = r.In(() => locate(Width, LineY, 1));
        obj_projectile_beam_emitter_hitscan p = Aim<obj_projectile_beam_emitter_hitscan>(r, alice, east, Params(From * 32, LineY * 32, 20.8, 90, pitch));
        r.Step();
        r.AssertNoRuntimes();
        Assert.Equal(LineY, r.In(() => p.last_impact_turf.y));
        return r.In(() => p.last_impact_turf.x);
    }

    [Fact]
    public void AShotPitchedDownHitsTheFloorWhereItsLineReachesIt()
    {
        // 10 degrees down: the floor 118 px out, in the fourth tile east of the eye's (x 6.7)
        Assert.Equal(7, ImpactX(7813, -10));
        // level: on to the east wall
        Assert.Equal(Width, ImpactX(7813, 0));
        // 5 degrees up: the ceiling (32 px) 128 px out (x 7)
        Assert.Equal(7, ImpactX(7815, 5));
    }

    // The bounces of a bouncy 7mm bullet (ricochets_max 40, chance 500) aimed from the eye on tile 3, a few ticks into its flight.
    private static (float Ricochets, bool Gone, float Slope) Bounce(ulong seed, double yaw, double pitch, int steps = 6)
    {
        AudioRoom r = Room(seed);
        mob_living_carbon_human alice = r.Dummy(From, LineY);
        turf east = r.In(() => locate(Width, LineY, 1));
        for (int x = 2; x < Width; x++) // the room's own walls are indestructible ones, which nothing bounces off
            r.In(() => Waiting.NoSleep(locate(x, Height, 1).ChangeTurf(typeof(turf_closed_wall))));
        obj_projectile_bullet_a7mm_bouncy p = Aim<obj_projectile_bullet_a7mm_bouncy>(r, alice, east, Params(From * 32, LineY * 32, 20.8, yaw, pitch));
        r.Step(steps);
        r.AssertNoRuntimes();
        return r.In(() => (p.ricochets, QDELETED(p), p.dm_slope));
    }

    [Fact]
    public void AnAimedBulletBouncesOffAWallTheFloorAndTheCeiling()
    {
        (float wall, bool wallGone, _) = Bounce(7821, 60, -3, steps: 3);
        Assert.True(wall >= 1 && !wallGone, $"a wall: {wall} bounces, gone {wallGone}");
        (float floor, bool floorGone, float up) = Bounce(7822, 90, -20, steps: 1);
        Assert.True(floor >= 1 && !floorGone && up > 0, $"the floor: {floor} bounces, gone {floorGone}, slope {up}");
        (float ceiling, bool ceilingGone, float down) = Bounce(7823, 90, 20, steps: 1);
        Assert.True(ceiling >= 1 && !ceilingGone && down < 0, $"the ceiling: {ceiling} bounces, gone {ceilingGone}, slope {down}");
    }

    [Fact]
    public void AHeadShotHurtsTheHeadAndAShotBetweenTheLegsFliesOnToTheWall()
    {
        AudioRoom r = Room(7814);
        mob_living_carbon_human alice = r.Dummy(From, LineY);
        mob_living_carbon_human bob = r.Dummy(Target, LineY);
        r.In(() => bob.dir = WEST);
        turf wall = r.In(() => locate(Width, LineY, 1));
        // level at the eye's 1.3 m along the line through the tile centres: the head (ShotGeometryTests' front frame)
        // the hitscan head shot's rolls pinned: tg's 3% eye puncture (_eyes.dm:240) waits for vision, and the live systems move the stream
        r.World.Rng.Reseed(7814, 1);
        obj_projectile_beam_emitter_hitscan head = Aim<obj_projectile_beam_emitter_hitscan>(r, alice, bob, Params(From * 32, LineY * 32, 20.8, 90, 0));
        r.Step();
        Assert.Equal(r.In(() => get_turf(bob)), r.In(() => head.last_impact_turf));
        Assert.Equal(BODY_ZONE_HEAD, r.In(() => head.def_zone)); // the crossed zone, no ran_zone roll
        Assert.True(r.In(() => Dm.num(((obj_item_bodypart)bob.get_bodypart(BODY_ZONE_HEAD)).burn_dam)) > 0, "the head burns");
        Assert.Equal(0f, r.In(() => Dm.num(((obj_item_bodypart)bob.get_bodypart(BODY_ZONE_CHEST)).burn_dam)));
        float burned = r.In(() => Dm.num(bob.get_fire_loss()));
        // between the legs (icon-x 16, icon-y 5 of the front frame: 0.4 px left of the centre line, 3.6 px up): past him to the wall
        double y = LineY * 32 + 0.4; // the viewer's right is south heading east: 0.4 px north is 0.4 px to the left
        obj_projectile_beam_emitter_hitscan legs = Aim<obj_projectile_beam_emitter_hitscan>(r, alice, wall, Params(From * 32, y, 3.6, 90, 0));
        r.Step();
        Assert.Equal(wall, r.In(() => legs.last_impact_turf));
        // no new burn (a burn wound the head shot rolled may heal its flesh a little meanwhile: tg's wound rolls follow the seed)
        Assert.True(r.In(() => Dm.num(bob.get_fire_loss())) <= burned, "the shot past him burned him");
        // aimed at him (the clicked target) but past him: tg's direct target no longer hits by itself
        obj_projectile_beam_emitter_hitscan over = Aim<obj_projectile_beam_emitter_hitscan>(r, alice, bob, Params(From * 32, LineY * 32, 30, 90, 0));
        r.Step();
        Assert.Equal(wall, r.In(() => over.last_impact_turf));
        Assert.True(r.In(() => Dm.num(bob.get_fire_loss())) <= burned, "the shot over him burned him");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheOutfitDevCommandIsTgsSelectEquipment()
    {
        AudioRoom r = Room(7816);
        mob_living_carbon_human ann = r.Dummy(From, LineY);
        // (the security officer's outfit reaches its mindshield implant and the engineer's its skillchip, not translated until M8)
        Assert.Null(r.In(() => ann.w_uniform));
        string line = r.In(() => DevCommands.Outfit(ann, "/datum/outfit/job/assistant"));
        Assert.Contains("equipped", line);
        Assert.NotNull(r.In(() => ann.w_uniform));
        Assert.NotNull(r.In(() => ann.shoes));
        // over a worn outfit: tg's admin_apply_outfit deletes the worn things first, so the new outfit's take their slots
        obj_item assistants = r.In(() => (obj_item)ann.w_uniform);
        Assert.Contains("equipped", r.In(() => DevCommands.Outfit(ann, "/datum/outfit/job/paramedic")));
        Assert.IsType<obj_item_clothing_under_rank_medical_paramedic>(r.In(() => ann.w_uniform));
        Assert.IsType<obj_item_storage_belt_medical_paramedic>(r.In(() => ann.belt));
        Assert.True(r.In(() => Dm.truthy(assistants.gc_destroyed)));
        Assert.Contains("no /datum/outfit", r.In(() => DevCommands.Outfit(ann, "/obj/item/crowbar")));
        Assert.Contains("has no client", r.In(() => DevCommands.Shoot(ann, "/obj/projectile/beam/emitter/hitscan"))); // shoot needs a player: AimedShotGameplayTests
    }

    [Fact]
    public void AnAimedBulletDentsTheWallAtTheHeightItsLineMeetsIt()
    {
        AudioRoom r = Room(7816);
        mob_living_carbon_human alice = r.Dummy(From, LineY);
        r.In(() => Waiting.NoSleep(locate(Target, LineY, 1).ChangeTurf(typeof(turf_closed_wall))));
        turf_closed_wall wall = r.In(() => locate(Target, LineY, 1) as turf_closed_wall);
        // tg dents a wall on 75% of hits (projectile.dm:373): shoot level at eye height until one dents it
        for (int shot = 0; shot < 12 && r.In(() => Dm.num(LAZYLEN(wall.dent_decals))) == 0; shot++)
        {
            Aim<obj_projectile_bullet_c38>(r, alice, wall, Params(From * 32, LineY * 32, 20.8, 90, 0));
            r.Step(20);
        }
        mutable_appearance dent = r.In(() => wall.dent_decals?[1f] as mutable_appearance);
        Assert.NotNull(dent);
        Assert.Equal(5f, r.In(() => dent.pixel_z)); // 20.8 px up, from the face's middle at 16
        r.AssertNoRuntimes();
    }
}
