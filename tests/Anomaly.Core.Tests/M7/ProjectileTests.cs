using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// The M7 projectiles slice (D165): tg's projectile base (modules/projectiles/projectile.dm) on SSprojectiles in a booted toy room:
/// a moving beam's pixel flight through the tiles at its speed (process, process_movement) and its range (reduce_range, on_range),
/// a hitscan beam's flight in one call with its tracer, muzzle and impact (generate_hitscan_tracers, the 0.3 s effects), the hit
/// chain (Bump, impact, process_hit_loop, projectile_hit, bullet_act, on_hit) on a wall (its sound and dent), on glass and a grille
/// (a laser passes them unless aimed at them), on a man (tg's armour through run_armor_check, the burn and the eye blur) and on a
/// machine (obj/bullet_act's take_damage), and tg's emitter (emitter.dm) shooting at its rate while on and powered.
/// </summary>
public class ProjectileTests
{
    private const int Second = 20;
    private const int Width = 14, Height = 7, LineY = 4, From = 3;

    private static AudioRoom Room(ulong seed, Action<World> extras = null)
    {
        var r = new AudioRoom(Width, Height, seed: seed, mapExtras: extras);
        r.Step(Second);
        return r;
    }

    private static T Shoot<T>(AudioRoom r, float angle = 90, int x = From, Action<T> setup = null) where T : obj_projectile => r.In(() =>
    {
        T p = Dm.@new<T>(locate(x, LineY, 1));
        setup?.Invoke(p);
        Waiting.NoSleep(p.fire(angle));
        return p;
    });

    private static List<T> All<T>(AudioRoom r) where T : datum => r.In(() => Dm.each<T>().ToList());

    [Fact]
    public void AMovingBeamGlidesThroughTheTilesAtItsSpeedAndEndsAtItsRange()
    {
        AudioRoom r = Room(7701);
        // DV211: the port flies them at 3.9 times tg's speed; tg's own flight is checked at tg's knob
        Assert.Equal(124.8f, r.In(() => SSprojectiles.pixels_per_decisecond));
        r.In(() => DevCommands.ProjectileSpeed(1f));
        obj_projectile_beam_emitter beam = Shoot<obj_projectile_beam_emitter>(r, setup: p => p.range = 4);
        Assert.True(r.In(() => Dm.@in(beam, SSprojectiles.processing)), "fire() starts it on SSprojectiles (projectile.dm:817)");
        Assert.Equal(1.25f, r.In(() => beam.speed));
        // tg's speed: 1.25 tiles a decisecond, 32 pixels a tile (SSprojectiles' pixels_per_decisecond): 20 pixels a tick
        var xs = new List<float>();
        var pixels = new List<float>();
        for (int i = 0; i < 12 && !r.In(() => QDELETED(beam)); i++)
        {
            xs.Add(r.In(() => beam.x));
            pixels.Add(r.In(() => (beam.x - From) * ICON_SIZE_X + beam.pixel_x));
            r.Step();
        }
        Assert.True(r.In(() => QDELETED(beam)), "it ends at its range");
        // its way along the line: one pixel at fire() (process_movement(max(floor(speed)))), then 20 a tick
        Assert.Equal(1f, pixels[0]);
        for (int i = 1; i < pixels.Count - 1; i++)
            Assert.Equal(pixels[i - 1] + 20, pixels[i]);
        Assert.Equal(xs.OrderBy(v => v), xs);
        // a range of 4 tiles: it counts a tile every 32 pixels and is queued at the fourth, in the middle of the fourth tile east
        Assert.Equal(From + 4, xs[^1]);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHitscanBeamHitsAtOnceAndLeavesItsTracerMuzzleAndImpactForAThirdOfASecond()
    {
        AudioRoom r = Room(7702);
        mob_living_carbon_human bob = r.Join(From, LineY + 1, "Bob");
        obj_projectile_beam_emitter_hitscan beam = Shoot<obj_projectile_beam_emitter_hitscan>(r);
        // the whole flight in fire(): it struck the room's east wall and is gone (impact: hitscan qdels at once)
        Assert.True(r.In(() => QDELETED(beam)));
        Assert.Equal(r.In(() => locate(Width, LineY, 1)), r.In(() => beam.last_impact_turf));
        List<obj_effect_projectile_tracer_laser_emitter> tracers = All<obj_effect_projectile_tracer_laser_emitter>(r);
        Assert.Single(tracers);
        // the tracer's transform: Scale(1, length / 32) then Turn(90): tg's north axis laid east, the length from the muzzle point
        // (17 pixels out, MUZZLE_EFFECT_PIXEL_INCREMENT) to the impact on the wall's face
        matrix m = r.In(() => tracers[0].transform as matrix);
        Assert.InRange(m.e, -0.01f, 0.01f);
        Assert.InRange(m.b * ICON_SIZE_ALL, (Width - From - 1) * ICON_SIZE_ALL - 17 - 1, (Width - From) * ICON_SIZE_ALL);
        Assert.Equal("emitter", r.In(() => tracers[0].icon_state));
        Assert.Equal(GAME_PLANE, r.In(() => tracers[0].plane));
        obj_effect_projectile_muzzle_laser_emitter muzzle = Assert.Single(All<obj_effect_projectile_muzzle_laser_emitter>(r));
        obj_effect_projectile_impact_laser_emitter impact = Assert.Single(All<obj_effect_projectile_impact_laser_emitter>(r));
        Assert.Equal(r.In(() => locate(From, LineY, 1)), r.In(() => muzzle.loc));
        Assert.Equal(17f, r.In(() => muzzle.pixel_x));
        Assert.Equal(r.In(() => locate(Width, LineY, 1)), r.In(() => impact.loc));
        // tg's lights: the muzzle's flash 2 tiles at 6, the impact's 2.5 at 7, lime; and a light on each tile of the line
        Assert.Equal(2f, r.In(() => Dm.num(muzzle.light_range)));
        Assert.Equal(COLOR_LIME, r.In(() => Dm.str(impact.light_color)));
        Assert.Equal(Width - From + 1, All<obj_effect_abstract_projectile_lighting>(r).Count);
        // the hit sounds of a wall (on_hit: hitsound_wall, then the hitsound)
        Assert.NotEmpty(r.Sounds.Of(bob, "sound/items/weapons/effects/searwall.ogg"));
        Assert.NotEmpty(r.Sounds.Of(bob, "sound/items/weapons/sear.ogg"));
        // PROJECTILE_TRACER_DURATION: 0.3 s
        r.Step(4);
        Assert.Single(All<obj_effect_projectile_tracer_laser_emitter>(r));
        r.Step(4);
        Assert.Empty(All<obj_effect_projectile_tracer_laser_emitter>(r));
        Assert.Empty(All<obj_effect_projectile_muzzle_laser_emitter>(r));
        Assert.Empty(All<obj_effect_projectile_impact_laser_emitter>(r));
        Assert.Empty(All<obj_effect_abstract_projectile_lighting>(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMovingBeamTakesTicksWhereTheHitscanTakesNone()
    {
        AudioRoom r = Room(7703);
        obj_projectile_beam_emitter moving = Shoot<obj_projectile_beam_emitter>(r);
        obj_projectile_beam_emitter_hitscan hitscan = Shoot<obj_projectile_beam_emitter_hitscan>(r);
        Assert.True(r.In(() => QDELETED(hitscan)));
        Assert.False(r.In(() => QDELETED(moving)));
        Assert.DoesNotContain(All<obj_effect_projectile_tracer>(r), t => r.In(() => t.y) != LineY);
        // ten tiles to the wall at the port's 78 pixels a tick (DV211, tg's 20): about five ticks, then it is queued at the impact and deleted the next
        int ticks = 0;
        while (!r.In(() => QDELETED(moving)) && ticks < 40)
        {
            r.Step();
            ticks++;
        }
        Assert.InRange(ticks, 5, 8);
        Assert.Equal(r.In(() => locate(Width, LineY, 1)), r.In(() => moving.last_impact_turf));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABeamOnAWallDentsItAndSoundsItsSear()
    {
        turf_closed_wall wall = null;
        AudioRoom r = Room(7704, w => wall = Dm.@new<turf_closed_wall>(locate(8, LineY, 1)));
        mob_living_carbon_human bob = r.Join(From, LineY + 1, "Bob");
        // on_hit: a BURN beam on a wall turf dents it three times in four (prob(75)), WALL_DENT_SHOT, at the beam's impact point
        for (int shot = 0; shot < 8 && r.In(() => Dm.num(LAZYLEN(wall.dent_decals))) == 0; shot++)
            Shoot<obj_projectile_beam_emitter_hitscan>(r);
        Assert.Equal(wall, r.In(() => locate(8, LineY, 1)));
        mutable_appearance dent = r.In(() => wall.dent_decals[1] as mutable_appearance);
        Assert.Equal("bullet_hole", r.In(() => dent.icon_state));
        Assert.Equal(WEST, r.In(() => dent.dir)); // DV214: on the face toward the shooter, west of the wall
        Assert.NotEmpty(r.Sounds.Of(bob, "sound/items/weapons/effects/searwall.ogg"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALaserPassesGlassAndAGrilleButAnAimedShotHitsTheWindow()
    {
        obj_structure_window_reinforced_fulltile window = null;
        obj_structure_grille grille = null;
        AudioRoom r = Room(7705, w =>
        {
            grille = Dm.@new<obj_structure_grille>(locate(6, LineY, 1));
            window = Dm.@new<obj_structure_window_reinforced_fulltile>(locate(8, LineY, 1));
        });
        float full = r.In(() => Dm.num(window.atom_integrity));
        // the beam's pass_flags (PASSTABLE | PASSGLASS | PASSGRILLE, beams.dm:4): through both to the wall
        obj_projectile_beam_emitter_hitscan beam = Shoot<obj_projectile_beam_emitter_hitscan>(r);
        Assert.Equal(r.In(() => locate(Width, LineY, 1)), r.In(() => beam.last_impact_turf));
        Assert.Equal(full, r.In(() => Dm.num(window.atom_integrity)));
        // aimed at the window (fire_projectile: original is the window, a direct target, can_hit_target :629): it takes the hit
        // through its laser armour (obj/bullet_act: take_damage(30, BURN, LASER))
        float armour = r.In(() => Dm.num(window.get_armor_rating(LASER)));
        obj_machinery_power_emitter gun = null;
        r.In(() => gun = Dm.@new<obj_machinery_power_emitter>(locate(From, LineY, 1)));
        obj_projectile aimed = r.In(() => Waiting.NoSleep(gun.fire_projectile(typeof(obj_projectile_beam_emitter_hitscan), window)) as obj_projectile);
        Assert.Equal(r.In(() => locate(8, LineY, 1)), r.In(() => aimed.last_impact_turf));
        Assert.Equal(full - 30 * (1 - armour / 100), r.In(() => Dm.num(window.atom_integrity)), 3);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABeamBurnsAManInItsWayThroughHisArmourAndBlursHisEyes()
    {
        AudioRoom r = Room(7706);
        mob_living_carbon_human victim = r.Join(8, LineY, "Victim");
        obj_projectile_beam_emitter_hitscan beam = Shoot<obj_projectile_beam_emitter_hitscan>(r);
        Assert.Equal(r.In(() => locate(8, LineY, 1)), r.In(() => beam.last_impact_turf));
        // the zone: ran_zone of the beam's "" with tg's accuracy, 100 - 7 a tile (impact :503); the armour of that zone against
        // LASER through run_armor_check (living_defense.dm:229), capped at 90 (apply_projectile_effects :178)
        string zone = r.In(() => beam.def_zone);
        float blocked = r.In(() => Dm.num(victim.run_armor_check(zone, LASER, "", "", 0, "", TRUE)));
        float expected = 30 * (1 - Math.Min(ARMOR_MAX_BLOCK, blocked) / 100);
        Assert.Equal(expected, r.In(() => Dm.num(victim.get_fire_loss())), 3);
        // the beam's eye blur (4 s, beams.dm:10) and tg's hit message
        Assert.True(r.In(() => victim.has_status_effect(typeof(datum_status_effect_eye_blur)) != null));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABeamDamagesAMachineInItsWay()
    {
        obj_machinery_power_port_gen_pacman pacman = null;
        AudioRoom r = Room(7707, w => pacman = Dm.@new<obj_machinery_power_port_gen_pacman>(locate(9, LineY, 1)));
        float full = r.In(() => Dm.num(pacman.atom_integrity));
        float armour = r.In(() => Dm.num(pacman.get_armor_rating(LASER)));
        obj_projectile_beam_emitter_hitscan beam = Shoot<obj_projectile_beam_emitter_hitscan>(r);
        Assert.Equal(r.In(() => locate(9, LineY, 1)), r.In(() => beam.last_impact_turf));
        Assert.Equal(full - 30 * (1 - armour / 100), r.In(() => Dm.num(pacman.atom_integrity)), 3);
        r.AssertNoRuntimes();
    }

    // ---- the emitter (emitter.dm:252 fire_beam, :222 process_early) ----

    private static void MachinesTick(AudioRoom r, int runs = 1) => r.Step(runs * r.In(() => (int)Dm.num(SSmachines.wait)) * 2);

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Bob, obj_machinery_power_test_m7apc_source Source, obj_machinery_power_emitter Emitter);

    private static Rig EmitterRoom(ulong seed)
    {
        obj_machinery_power_test_m7apc_source source = null;
        obj_machinery_power_emitter emitter = null;
        var r = new AudioRoom(Width, Height, seed: seed, mapExtras: w =>
        {
            // the source behind the emitter (west), out of its line of fire
            for (int x = 2; x <= From; x++)
            {
                Dm.@new<turf_open_floor_plating>(locate(x, LineY - 1, 1));
                Dm.@new<obj_structure_cable>(locate(x, LineY - 1, 1));
            }
            source = Dm.@new<obj_machinery_power_test_m7apc_source>(locate(2, LineY - 1, 1));
            emitter = Dm.@new<obj_machinery_power_emitter_welded>(locate(From, LineY - 1, 1));
            emitter.dir = EAST;
        });
        r.In(() => source.output_energy = 2_000_000);
        mob_living_carbon_human bob = r.Join(From, LineY, "Bob");
        r.Step(Second);
        MachinesTick(r, 2);
        return new Rig(r, bob, source, emitter);
    }

    private static int Shots(Rig g) => g.R.Sounds.Of(g.Bob, "sound/items/weapons/emitter.ogg").Count;

    [Fact]
    public void TheEmitterShootsAtItsRateWhileOnAndPoweredAndStopsWhenOffOrUnpowered()
    {
        Rig g = EmitterRoom(7708);
        mob_living_carbon_human victim = g.R.Dummy(9, LineY - 1);
        // a beam to the head punctures an eye one time in thirty (_eyes.dm:235): the wound is vision's (M10+), not this test's
        g.R.In(() => qdel(victim.get_organ_slot(ORGAN_SLOT_EYES)));
        g.R.Command(() => ClientSeam.Click(g.Bob.client, g.Emitter, "icon-x=16;icon-y=16;left=1;button=left"));
        g.R.Step(2);
        Assert.True(g.R.In(() => Dm.truthy(g.Emitter.active)));
        Assert.Equal(100f, g.R.In(() => g.Emitter.fire_delay)); // interact :185: maximum_fire_delay
        // the first shot comes once the delay since the last shot has passed, then tg's rhythm: three more at 2 s (shot_number
        // < 3), then a delay of rand(2, 10) s (emitter.dm:266-271)
        var shotAt = new List<long>();
        int before = Shots(g);
        for (int cycle = 0; cycle < 8; cycle++)
        {
            MachinesTick(g.R);
            int now = Shots(g);
            if (now > before)
                shotAt.Add(cycle);
            before = now;
        }
        Assert.True(shotAt.Count >= 4, "shots on cycles " + string.Join(",", shotAt));
        Assert.Equal(new long[] { shotAt[0], shotAt[0] + 1, shotAt[0] + 2, shotAt[0] + 3 }, shotAt.Take(4));
        Assert.InRange(g.R.In(() => g.Emitter.fire_delay), 20, 100);
        // the beam struck the man in front of it, east of the emitter
        Assert.True(g.R.In(() => Dm.num(victim.get_fire_loss())) > 0, "the beam burns the man in front of it");
        // emitter.ogg at 50 (fire_beam :254), heard a tile off
        Assert.All(g.R.Sounds.Of(g.Bob, "sound/items/weapons/emitter.ogg"), s => Assert.InRange(s.Volume, 1, 50));

        // off: no more shots
        g.R.Command(() => ClientSeam.Click(g.Bob.client, g.Emitter, "icon-x=16;icon-y=16;left=1;button=left"));
        g.R.Step(2);
        Assert.False(g.R.In(() => Dm.truthy(g.Emitter.active)));
        int off = Shots(g);
        MachinesTick(g.R, 6);
        Assert.Equal(off, Shots(g));

        // on again without power: it waits unpowered and does not shoot
        g.R.In(() => g.Source.output_energy = 0);
        MachinesTick(g.R, 2);
        g.R.Command(() => ClientSeam.Click(g.Bob.client, g.Emitter, "icon-x=16;icon-y=16;left=1;button=left"));
        g.R.Step(2);
        MachinesTick(g.R, 6);
        Assert.True(g.R.In(() => Dm.truthy(g.Emitter.active)));
        Assert.False(g.R.In(() => Dm.truthy(g.Emitter.powered)));
        Assert.Equal(off, Shots(g));
        g.R.AssertNoRuntimes();
    }
}
