using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package GU1 (D333), the energy guns' leftovers, in a booted toy room with Bob as a player: every type made live spawns, examines
/// (the Type 1, 3, 4 and 6 lasers' and the antique laser's lore hint) and goes; the advanced energy gun charges itself, an EMP raises its
/// reactor's failure chance and its failures burn the holder until the reactor overloads (its fail overlays); the pulse rifle's beam
/// burns for 50 and blasts the walls and structures it hits, the destroyer has one setting; the compact laser shotgun sprays its
/// pellets and racks its pump 0.2 s later; the SC/FISHER disruptor breaks the light fixture it is fired at, and its melee hit and its
/// throw turn off a man's headset; the accelerator cannon's beam grows with each tile it flies; the kinetic accelerator's seclight mount
/// and the miniature energy gun's built-in flashlight; the flare round's mark until its target is wet; the emitter's lens beams.
/// </summary>
public class EnergyGunsGU1Tests
{
    private const int LineY = 4;

    /// <summary>Every type the package made live in its files (the guns, the beams and bolts, the laser gatling's casing).</summary>
    internal static readonly Type[] AllTypes =
    {
        typeof(obj_item_gun_energy_laser_assault), typeof(obj_item_gun_energy_laser_retro), typeof(obj_item_gun_energy_laser_retro_old),
        typeof(obj_item_gun_energy_laser_soul), typeof(obj_item_gun_energy_laser_hellgun), typeof(obj_item_gun_energy_laser_captain),
        typeof(obj_item_gun_energy_laser_captain_scattershot), typeof(obj_item_gun_energy_laser_scatter), typeof(obj_item_gun_energy_laser_scatter_shotty),
        typeof(obj_item_gun_energy_laser_scatter_shotty_compact), typeof(obj_item_gun_energy_laser_xray), typeof(obj_item_gun_energy_e_gun_mini),
        typeof(obj_item_gun_energy_e_gun_mini_practice_phaser), typeof(obj_item_gun_energy_e_gun_nuclear), typeof(obj_item_gun_energy_recharge_fisher),
        typeof(obj_item_gun_energy_pulse), typeof(obj_item_gun_energy_pulse_prize), typeof(obj_item_gun_energy_pulse_loyalpin),
        typeof(obj_item_gun_energy_pulse_carbine), typeof(obj_item_gun_energy_pulse_carbine_lethal), typeof(obj_item_gun_energy_pulse_carbine_loyalpin),
        typeof(obj_item_gun_energy_pulse_carbine_taserless), typeof(obj_item_gun_energy_pulse_destroyer), typeof(obj_item_gun_energy_pulse_pistol),
        typeof(obj_item_gun_energy_pulse_pistol_taserless), typeof(obj_item_gun_energy_pulse_pistol_loyalpin), typeof(obj_item_gun_energy_pulse_pistol_m1911),
        typeof(obj_item_gun_energy_e_gun_advtaser_cyborg), typeof(obj_item_ammo_casing_laser),
        typeof(obj_projectile_beam_laser_accelerator), typeof(obj_projectile_beam_laser_flare), typeof(obj_projectile_beam_pulse),
        typeof(obj_projectile_beam_pulse_shotgun), typeof(obj_projectile_beam_pulse_heavy), typeof(obj_projectile_beam_emitter_hitscan_bioregen),
        typeof(obj_projectile_beam_emitter_hitscan_incend), typeof(obj_projectile_beam_emitter_hitscan_psy), typeof(obj_projectile_beam_emitter_hitscan_magnetic),
        typeof(obj_projectile_beam_emitter_hitscan_blast), typeof(obj_projectile_energy_radiation), typeof(obj_projectile_energy_radiation_weak),
        typeof(obj_projectile_energy_fisher), typeof(obj_projectile_energy_fisher_melee),
    };

    /// <summary>The guns whose add_deep_lore puts tg's lore hint on their examine.</summary>
    private static readonly Type[] Lore =
    {
        typeof(obj_item_gun_energy_laser_retro), typeof(obj_item_gun_energy_laser_soul), typeof(obj_item_gun_energy_laser_hellgun),
        typeof(obj_item_gun_energy_laser_captain), typeof(obj_item_gun_energy_laser_xray),
    };

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(14, 9, seed: seed);
        // tg's strings/ files (the scars' texts), as the server mounts them
        if (TgClone.Root != null)
            TgStrings.Mount(DmWorldState.Of(r.World).Files, TgClone.Root);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Arm<T>(AudioRoom r, mob_living_carbon_human holder) where T : obj_item
    {
        T gun = r.In(() => Dm.@new<T>(holder.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(holder.put_in_active_hand(gun)));
        r.In(() => Assert.Same(gun, holder.get_active_held_item()));
        return gun;
    }

    // Bob pulls the trigger at the target: tg's try_fire_gun, as the click chain calls it.
    private static bool Pull(AudioRoom r, mob_living_carbon_human shooter, obj_item_gun gun, atom target) =>
        Dm.truthy(r.In(() => Waiting.NoSleep(gun.try_fire_gun(target, shooter, "icon-x=16;icon-y=16"))));

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    private static string Examine(AudioRoom r, atom a, mob who) => r.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(a.examine(who))).Select(o => Dm.str(o))));

    private static bool Heard(AudioRoom r, string file) => r.Sounds.Heard.Any(h => (h.File ?? "").Contains(file, StringComparison.Ordinal));

    private static float Burn(AudioRoom r, mob_living m) => r.In(() => Dm.num(m.get_fire_loss()));

    [Fact]
    public void EveryTypeSpawnsLiveIsExaminedAndGoes()
    {
        var (r, _, bob) = Room(9601);
        var failures = new List<string>();
        foreach (Type type in AllTypes)
        {
            if (InertSeam.IsInertType(type))
                failures.Add(type.Name + " is inert");
            atom thing = r.In(() => (atom)Dm.@new(type, locate(6, LineY, 1)));
            r.Step(2);
            if (thing is obj_item)
            {
                string text = Examine(r, thing, bob);
                if (Lore.Contains(type) && !text.Contains("look closer", StringComparison.Ordinal))
                    failures.Add(type.Name + " has no lore hint: " + text);
            }
            r.In(() => qdel(thing));
            r.Step(2);
        }
        Assert.Empty(failures);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAdvancedEnergyGunChargesItselfAndItsReactorBurnsItsHolderUntilItOverloads()
    {
        var (r, feed, bob) = Room(9602);
        mob_living_carbon_human dummy = r.Dummy(10, LineY);
        var gun = Arm<obj_item_gun_energy_e_gun_nuclear>(r, bob);
        Assert.Contains("nucgun_fail_0", Overlays(r, gun));

        // the reactor gives a spent shot back (selfcharge, charge_delay 10 s)
        float full = r.In(() => Dm.num(gun.cell.charge));
        Assert.True(Pull(r, bob, gun, dummy), "the gun did not fire");
        r.Step(Second);
        float spent = r.In(() => Dm.num(gun.cell.charge));
        Assert.True(spent < full, "the shot spent no charge");
        r.Step(16 * Second);
        Assert.True(r.In(() => Dm.num(gun.cell.charge)) > spent, "the reactor did not charge the cell");

        // an EMP (severity 1) raises the failure chance by round(15 / 1)
        r.In(() => Waiting.NoSleep(gun.emp_act(EMP_HEAVY)));
        Assert.Equal(15f, r.In(() => gun.fail_chance));

        // a certain failure: every shot heats the reactor (3 burn) until it overloads (10 burn) and stops charging
        r.In(() =>
        {
            gun.fail_chance = 100;
            Waiting.NoSleep(gun.cell.give(gun.cell.maxcharge));
            Waiting.NoSleep(gun.recharge_newshot());
        });
        float burnt = Burn(r, bob);
        bool sawFail2 = false;
        for (int shot = 0; shot < 6 && !r.In(() => Dm.truthy(gun.reactor_overloaded)); shot++)
        {
            r.Step(2 * Second);
            Assert.True(Pull(r, bob, gun, dummy), "the gun did not fire shot " + shot);
            r.Step(2);
            if (!r.In(() => Dm.truthy(gun.reactor_overloaded)) && Overlays(r, gun).Contains("nucgun_fail_2"))
                sawFail2 = true;
        }
        Assert.True(r.In(() => Dm.truthy(gun.reactor_overloaded)), "the reactor never overloaded");
        Assert.True(sawFail2, "the hot reactor's overlay (fail_2) was not shown");
        Assert.Contains("nucgun_fail_3", Overlays(r, gun));
        Assert.True(feed.Any("feels warmer."));
        Assert.True(feed.Any("reactor overloads!"));
        Assert.True(Burn(r, bob) >= burnt + 13, "the reactor did not burn its holder");
        Assert.False(r.In(() => Dm.@in(gun, SSobj.processing)), "an overloaded reactor still charges");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePulseBeamBurnsForFiftyAndBlastsWallsAndStructuresAndTheDestroyerHasOneSetting()
    {
        var (r, feed, bob) = Room(9603);
        mob_living_carbon_human dummy = r.Dummy(8, LineY);
        var gun = Arm<obj_item_gun_energy_pulse>(r, bob);
        Assert.Equal(typeof(obj_item_ammo_casing_energy_laser_pulse), r.In(() => gun.ammo_type[1]?.GetType()));
        Assert.True(Pull(r, bob, gun, dummy), "the pulse rifle did not fire");
        // read the burn as the beam lands: the dummy's Life starts healing it a few ticks later
        for (int t = 0; t < Second && Burn(r, dummy) == 0; t++)
            r.Step();
        Assert.Equal(50f, Burn(r, dummy), 0.01);

        // what the beam's on_hit does to a wall and a structure: they go to SSexplosions' heavy lists (EXPLODE_HEAVY)
        turf wall = r.In(() => (turf)locate(12, 2, 1));
        r.In(() => Waiting.NoSleep(wall.ChangeTurf(typeof(turf_closed_wall))));
        wall = r.In(() => (turf)locate(12, 2, 1));
        var closet = r.In(() => Dm.@new<obj_structure_closet>(locate(11, 6, 1)));
        r.In(() =>
        {
            var beam = Dm.@new<obj_projectile_beam_pulse>(locate(10, 6, 1));
            beam.damage = 0; // an unfired beam: no wall dent (the base on_hit's dent needs a flight line), only the pulse's own blast
            Waiting.NoSleep(beam.on_hit(closet));
            Assert.True(Dm.@in(closet, SSexplosions.med_mov_atom), "the structure was not blasted");
            Waiting.NoSleep(beam.on_hit(wall));
            Assert.True(Dm.@in(wall, SSexplosions.medturf), "the wall was not blasted");
            qdel(beam);
        });
        r.Step(Second);

        r.In(() => Waiting.NoSleep(bob.dropItemToGround(gun)));
        var destroyer = Arm<obj_item_gun_energy_pulse_destroyer>(r, bob);
        r.In(() => Waiting.NoSleep(destroyer.attack_self(bob)));
        Assert.True(feed.Any("has three settings, and they are all DESTROY."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCompactLaserShotgunSpraysItsPelletsAndRacksItsPump()
    {
        var (r, _, bob) = Room(9604);
        mob_living_carbon_human dummy = r.Dummy(7, LineY);
        var gun = Arm<obj_item_gun_energy_laser_scatter_shotty_compact>(r, bob);
        Assert.True(Pull(r, bob, gun, dummy), "the shotgun did not fire");
        int pellets = r.In(() => Dm.each<obj_projectile>().Count(p => Dm.truthy(p.fired)));
        Assert.True(pellets > 1, "one pellet: " + pellets);
        Assert.False(Heard(r, "shotgun/rack.ogg"), "racked before its 0.2 s");
        r.Step(Second);
        Assert.True(Heard(r, "shotgun/rack.ogg"), "the pump was not racked");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheFisherBreaksTheLightItIsFiredAtAndItsHitAndThrowSilenceAHeadset()
    {
        var (r, feed, bob) = Room(9605);
        var light = r.In(() => Dm.@new<obj_machinery_light>(locate(8, LineY, 1)));
        r.Step(2);
        Assert.NotEqual(LIGHT_BROKEN, r.In(() => light.status));
        var gun = Arm<obj_item_gun_energy_recharge_fisher>(r, bob);
        Assert.True(Pull(r, bob, gun, light), "the disruptor did not fire");
        r.Step(Second);
        Assert.Equal(LIGHT_BROKEN, r.In(() => light.status));
        Assert.True(feed.Any("balloon: disrupted"));

        // its melee hit disrupts all the gear of the man it strikes (25 s)
        mob_living_carbon_human alice = r.Dummy(4, LineY);
        var headset = r.In(() => Dm.@new<obj_item_radio_headset>(alice.loc));
        r.In(() => Waiting.NoSleep(alice.equip_to_slot_or_del(headset, ITEM_SLOT_EARS)));
        r.In(() => headset.set_broadcasting(TRUE));
        Assert.Equal(1f, r.In(() => headset.broadcasting));
        r.In(() => Waiting.NoSleep(gun.attack__proc(alice, bob)));
        Assert.Equal(0f, r.In(() => headset.broadcasting));

        // and so does the gun thrown at him
        r.In(() => headset.set_broadcasting(TRUE));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(gun)));
        r.In(() => Waiting.Detach(gun.throw_at(alice, 7, 1, bob)));
        r.Step(2 * Second);
        Assert.Equal(0f, r.In(() => headset.broadcasting));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAcceleratorBeamGrowsWithEachTileItFlies()
    {
        var (r, _, bob) = Room(9606);
        // tg's reduce_range: +7 damage a tile, the sprite scaled by 1 + 0.1 a tile (at most 4)
        r.In(() =>
        {
            var beam = Dm.@new<obj_projectile_beam_laser_accelerator>(locate(6, 7, 1));
            Assert.Equal(6f, Dm.num(beam.damage));
            for (int i = 0; i < 3; i++)
                Waiting.NoSleep(beam.reduce_range());
            Assert.Equal(27f, Dm.num(beam.damage), 0.01);
            Assert.Equal(1.3f, ((matrix)beam.transform).a, 0.001);
            qdel(beam);
        });

        // the accelerator cannon (pin = null in tg: a pin is put in) burns a far man more than a near one
        var gun = Arm<obj_item_gun_energy_lasercannon>(r, bob);
        r.In(() => Waiting.NoSleep(Dm.@new<obj_item_firing_pin>(bob.loc).gun_insert(new_gun: gun, starting: TRUE)));
        mob_living_carbon_human near = r.Dummy(5, LineY);
        Assert.True(Pull(r, bob, gun, near), "the cannon did not fire");
        r.Step(Second);
        float nearBurn = Burn(r, near);
        Assert.True(nearBurn > 0, "the near man was not hit");
        r.In(() => qdel(near));
        r.Step(2 * Second);
        mob_living_carbon_human far = r.Dummy(12, LineY);
        Assert.True(Pull(r, bob, gun, far), "the cannon did not fire again");
        r.Step(2 * Second);
        Assert.True(Burn(r, far) > nearBurn, $"far {Burn(r, far)} <= near {nearBurn}");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheKineticAcceleratorTakesASecliteAndTheMiniEnergyGunHasItsOwn()
    {
        var (r, _, bob) = Room(9607);
        var ka = Arm<obj_item_gun_energy_recharge_kinetic_accelerator>(r, bob);
        var mount = r.In(() => ka.GetComponent(typeof(datum_component_seclite_attachable)) as datum_component_seclite_attachable);
        Assert.NotNull(mount);
        var light = r.In(() => Dm.@new<obj_item_flashlight_seclite>(bob.loc));
        r.In(() => mount.add_light(light));
        r.Step(2);
        Assert.Same(ka, r.In(() => light.loc));
        Assert.Contains("flight", Overlays(r, ka));

        var mini = r.In(() => Dm.@new<obj_item_gun_energy_e_gun_mini>(bob.loc));
        r.Step(2);
        r.AssertNoRuntimes(); // a runtime in the mini's Initialize leaves it without its light
        var builtIn = r.In(() => mini.GetComponent(typeof(datum_component_seclite_attachable)) as datum_component_seclite_attachable);
        Assert.NotNull(builtIn);
        Assert.NotNull(r.In(() => builtIn.light));
        Assert.Same(mini, r.In(() => builtIn.light.loc));
        Assert.Equal(0f, r.In(() => builtIn.is_light_removable));
        Assert.Contains("mini-light", Overlays(r, mini));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheFlareMarksItsTargetUntilItIsWet()
    {
        var (r, _, bob) = Room(9608);
        mob_living_carbon_human dummy = r.Dummy(8, LineY);
        r.In(() =>
        {
            var flare = Dm.@new<obj_projectile_beam_laser_flare>(locate(7, LineY, 1));
            Waiting.NoSleep(flare.on_hit(dummy));
            qdel(flare);
        });
        r.Step(2);
        Assert.True(r.In(() => HAS_TRAIT(dummy, TRAIT_DESIGNATED_TARGET)), "not marked");
        Assert.True(r.In(() => Dm.truthy(dummy.has_status_effect(typeof(datum_status_effect_designated_target)))));
        r.In(() => Waiting.NoSleep(dummy.adjust_wet_stacks(5)));
        r.Step(5 * Second);
        Assert.False(r.In(() => HAS_TRAIT(dummy, TRAIT_DESIGNATED_TARGET)), "the wet target is still marked");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEmittersLensBeamsHealBurnAndSadden()
    {
        var (r, feed, bob) = Room(9609);
        mob_living_carbon_human dummy = r.Dummy(8, LineY);
        r.In(() => Waiting.NoSleep(dummy.apply_damage(damage: 20, damagetype: BRUTE, def_zone: BODY_ZONE_CHEST, wound_bonus: CANT_WOUND))); // one bodypart: heal_bodypart_damage heals one damaged part
        float brute = r.In(() => Dm.num(dummy.get_brute_loss()));
        r.In(() =>
        {
            var bioregen = Dm.@new<obj_projectile_beam_emitter_hitscan_bioregen>(locate(7, LineY, 1));
            Waiting.NoSleep(bioregen.on_hit(dummy));
            qdel(bioregen);
        });
        r.Step(2);
        Assert.Equal(brute - 5, r.In(() => Dm.num(dummy.get_brute_loss())), 0.01);

        r.In(() =>
        {
            var incend = Dm.@new<obj_projectile_beam_emitter_hitscan_incend>(locate(7, LineY, 1));
            Waiting.NoSleep(incend.on_hit(dummy));
            qdel(incend);
        });
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(dummy.on_fire)), "the incendiary beam did not set him on fire");

        r.In(() =>
        {
            var psy = Dm.@new<obj_projectile_beam_emitter_hitscan_psy>(locate(7, LineY, 1));
            Waiting.NoSleep(psy.on_hit(dummy));
            qdel(psy);
        });
        Assert.True(feed.Any("Suddenly, everything feels just a little bit worse!"));
        r.AssertNoRuntimes();
    }
}
