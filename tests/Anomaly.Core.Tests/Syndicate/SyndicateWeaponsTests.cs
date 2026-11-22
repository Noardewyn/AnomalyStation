using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Syndicate;

/// <summary>
/// The syndicate campaign G2 (D316): the traitor's weapons and explosives in a booted toy room. The energy sword turns on and
/// blocks, the double-bladed sword needs both hands, the minibomb and the frag grenade blow with tg's ranges, the syndicate bomb
/// counts down, is defused by its activation wire and explodes, the power sink drains an APC's cell and heats, an active jammer
/// stops a radio line, the microlaser knocks out after its delay, the crossbow's bolt knocks down; every type made live spawns
/// live and is destroyed with no runtimes.
/// </summary>
public class SyndicateWeaponsTests
{
    private static readonly Type[] AllTypes =
    {
        typeof(obj_item_melee_energy_axe), typeof(obj_item_melee_energy_sword), typeof(obj_item_melee_energy_sword_saber), typeof(obj_item_melee_energy_sword_saber_red),
        typeof(obj_item_melee_energy_sword_saber_blue), typeof(obj_item_melee_energy_sword_saber_green), typeof(obj_item_melee_energy_sword_saber_purple),
        typeof(obj_item_melee_energy_sword_pirate), typeof(obj_item_melee_energy_blade), typeof(obj_item_melee_energy_blade_hardlight), typeof(obj_item_melee_energy_sword_surplus),
        typeof(obj_item_melee_energy_sword_nullrod), typeof(obj_item_melee_energy_sword_nullrod_red), typeof(obj_item_melee_energy_sword_nullrod_pirate),
        typeof(obj_item_dualsaber), typeof(obj_item_dualsaber_green), typeof(obj_item_dualsaber_red), typeof(obj_item_dualsaber_blue), typeof(obj_item_dualsaber_purple),
        typeof(obj_item_melee_powerfist), typeof(obj_item_clothing_gloves_rapid), typeof(obj_item_throwing_star), typeof(obj_item_restraints_legcuffs_bola_tactical),
        typeof(obj_item_storage_toolbox_guncase), typeof(obj_item_storage_toolbox_guncase_traitor), typeof(obj_item_storage_toolbox_guncase_traitor_ammunition),
        typeof(obj_item_storage_toolbox_guncase_traitor_donksoft), typeof(obj_item_storage_toolbox_guncase_traitor_ammunition_donksoft),
        typeof(obj_item_gun_energy_recharge_ebow), typeof(obj_item_gun_energy_recharge_ebow_halloween), typeof(obj_item_gun_energy_recharge_ebow_large),
        typeof(obj_item_grenade_syndieminibomb), typeof(obj_item_grenade_syndieminibomb_concussion), typeof(obj_item_grenade_frag), typeof(obj_item_grenade_frag_mega),
        typeof(obj_item_grenade_gluon), typeof(obj_item_grenade_empgrenade), typeof(obj_item_grenade_hypnotic), typeof(obj_item_grenade_c4), typeof(obj_item_grenade_c4_x4),
        typeof(obj_item_pizzabox), typeof(obj_item_pizzabox_bomb), typeof(obj_item_pizzabox_bomb_armed), typeof(obj_item_pizzabox_margherita), typeof(obj_item_pizzabox_meat),
        typeof(obj_item_bombcore_miniature_pizza), typeof(obj_item_earthcracker), typeof(obj_item_earthcracker_small),
        typeof(obj_item_bombcore), typeof(obj_item_bombcore_syndicate), typeof(obj_item_bombcore_syndicate_large), typeof(obj_item_bombcore_training), typeof(obj_item_bombcore_large),
        typeof(obj_item_bombcore_miniature), typeof(obj_item_bombcore_chemical), typeof(obj_item_bombcore_emp), typeof(obj_item_syndicatedetonator),
        typeof(obj_item_sbeacondrop), typeof(obj_item_sbeacondrop_bomb), typeof(obj_item_sbeacondrop_emp), typeof(obj_item_sbeacondrop_powersink), typeof(obj_item_powersink),
        typeof(obj_item_assembly_flash_hypnotic), typeof(obj_item_syndicate_teleporter), typeof(obj_item_storage_box_syndie_kit_syndicate_teleporter),
        typeof(obj_item_storage_briefcase_launchpad), typeof(obj_item_batterer), typeof(obj_item_healthanalyzer_rad_laser), typeof(obj_item_shadowcloak), typeof(obj_item_shadowcloak_weaker),
        typeof(obj_item_jammer), typeof(obj_item_jammer_makeshift), typeof(obj_item_pen_penbang), typeof(obj_item_camera_flash), typeof(obj_item_clothing_shoes_jackboots_dagger),
    };

    private static readonly Type[] MachineTypes =
    {
        typeof(obj_machinery_syndicatebomb), typeof(obj_machinery_syndicatebomb_training), typeof(obj_machinery_syndicatebomb_emp), typeof(obj_machinery_syndicatebomb_empty),
        typeof(obj_machinery_syndicatebomb_self_destruct), typeof(obj_machinery_power_singularity_beacon), typeof(obj_machinery_power_singularity_beacon_syndicate),
        typeof(obj_machinery_power_singularity_beacon_syndicate_no_escape), typeof(obj_effect_weakpoint), typeof(obj_effect_weakpoint_big),
    };

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed, int w = 15, int h = 13, Action<World> extras = null)
    {
        var r = new AudioRoom(w, h, seed: seed, mapExtras: extras);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Make<T>(AudioRoom r, atom where) where T : atom
    {
        T thing = r.In(() => Dm.@new<T>(where));
        r.Step(2);
        return thing;
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T thing = Make<T>(r, who.loc as atom);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(thing)))));
        return thing;
    }

    [Fact]
    public void EveryTypeSpawnsLiveAndIsDestroyedWithNoRuntimes()
    {
        var (r, _, bob) = Room(31601);
        var failures = new List<string>();
        foreach (Type type in AllTypes.Concat(MachineTypes))
        {
            atom_movable thing = r.In(() => (atom_movable)Dm.@new(type, bob.loc));
            r.Step(2);
            if (r.In(() => InertSeam.IsInert(thing)))
                failures.Add(type.Name + " is inert");
            r.In(() => Waiting.NoSleep(thing.examine(bob)));
            r.In(() => qdel(thing));
            r.Step(2);
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
        // tg's own stack_trace: a full Donksoft riot magazine (top_off's starting rounds add no materials) loses each chambered dart's
        // iron below zero (_box_magazine.dm:77, atom_materials.dm:46)
        Assert.All(r.Room.RuntimeText, t => Assert.Contains("materials with non-positive values found in /obj/item/ammo_box/magazine/toy/pistol/riot", t));
    }

    [Fact]
    public void TheEnergySwordTurnsOnWithItsColourAndBlocksOnlyWhileOn()
    {
        var (r, _, bob) = Room(31602);
        var sword = Hold<obj_item_melee_energy_sword_saber_red>(r, bob);
        Assert.Equal(3f, r.In(() => sword.force));
        Assert.False(r.In(() => Dm.truthy(Waiting.NoSleep(sword.hit_reaction(bob, bob, "the punch", 100)))), "off, it blocks nothing");

        r.In(() => Waiting.NoSleep(sword.attack_self(bob)));
        r.Step(2);
        Assert.True(r.In(() => HAS_TRAIT(sword, TRAIT_TRANSFORM_ACTIVE)));
        Assert.Equal(30f, r.In(() => sword.force));
        Assert.Equal("e_sword_on_red", r.In(() => sword.icon_state));
        Assert.True(r.In(() => Dm.truthy(sword.light_on)));
        Assert.Equal(3500f, r.In(() => sword.heat));
        Assert.Contains(r.Sounds.Heard, h => h.File?.EndsWith("saberon.ogg", StringComparison.Ordinal) == true);

        // on: tg's block (the item's hit_reaction rolls final_block_chance); a leap is 25 points harder, an overwhelming attack is never blocked
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(sword.hit_reaction(bob, bob, "the punch", 100)))));
        Assert.Contains(r.Sounds.Heard, h => h.File?.EndsWith("block_blade.ogg", StringComparison.Ordinal) == true);
        Assert.False(r.In(() => Dm.truthy(Waiting.NoSleep(sword.hit_reaction(bob, bob, "the tackle", 100, attack_type: OVERWHELMING_ATTACK)))));
        Assert.False(r.In(() => Dm.truthy(Waiting.NoSleep(sword.hit_reaction(bob, bob, "the leap", 25, attack_type: LEAP_ATTACK)))));

        // off again
        r.In(() => Waiting.NoSleep(sword.attack_self(bob)));
        r.Step(2);
        Assert.Equal("e_sword", r.In(() => sword.icon_state));
        Assert.Equal(3f, r.In(() => sword.force));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDoubleBladedSwordNeedsBothHandsToLightAndCut()
    {
        var (r, _, bob) = Room(31603);
        var saber = Hold<obj_item_dualsaber_red>(r, bob);
        Assert.Equal("dualsaber0", r.In(() => saber.icon_state));
        Assert.Equal(NONE, r.In(() => Dm.num(saber.get_sharpness())));
        Assert.False(r.In(() => Dm.truthy(Waiting.NoSleep(saber.IsReflect()))));

        // a full other hand: no wield
        var other = Hold<obj_item_crowbar>(r, bob);
        r.In(() => Waiting.NoSleep(saber.attack_self(bob)));
        r.Step(2);
        Assert.False(r.In(() => HAS_TRAIT(saber, TRAIT_WIELDED)));
        r.In(() => qdel(other));
        r.Step(2);

        // both hands: wielded, lit, 40 force, sharp, the red saber's sprite
        r.In(() => Waiting.NoSleep(saber.attack_self(bob)));
        r.Step(2);
        Assert.True(r.In(() => HAS_TRAIT(saber, TRAIT_WIELDED)));
        Assert.Equal(40f, r.In(() => saber.force));
        Assert.Equal("dualsaberred1", r.In(() => saber.icon_state));
        Assert.True(r.In(() => Dm.truthy(saber.light_on)));
        Assert.Equal(SHARP_EDGED, r.In(() => Dm.num(saber.get_sharpness())));
        Assert.Contains(r.Sounds.Heard, h => h.File?.EndsWith("saberon.ogg", StringComparison.Ordinal) == true);

        // a hulk cannot hold it properly
        r.In(() => Waiting.NoSleep(saber.attack_self(bob)));
        r.Step(2);
        Assert.False(r.In(() => HAS_TRAIT(saber, TRAIT_WIELDED)));
        r.In(() => ADD_TRAIT(bob, TRAIT_HULK, "test"));
        r.In(() => Waiting.NoSleep(saber.attack_self(bob)));
        r.Step(2);
        Assert.False(r.In(() => HAS_TRAIT(saber, TRAIT_WIELDED)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMinibombAndTheFragGrenadeBlowWithTgsRanges()
    {
        var (r, _, bob) = Room(31604, 21, 19);
        turf center = r.In(() => locate(10, 9, 1));
        var minibomb = Make<obj_item_grenade_syndieminibomb>(r, center);
        r.In(() => Waiting.Detach(minibomb.detonate()));
        r.In(() =>
        {
            // ex_dev 1, ex_heavy 2, ex_light 4: by tg's distance, high below 1, medium below 2, light below 4
            Assert.True(Dm.@in(center, SSexplosions.highturf));
            Assert.Equal(1f, SSexplosions.highturf.len);
            Assert.Equal(8f, SSexplosions.medturf.len);
            foreach (turf t in Dm.each_cast<turf>(SSexplosions.lowturf))
            {
                float dist = CHEAP_HYPOTENUSE(t.x, t.y, center.x, center.y);
                Assert.InRange(dist, 2f, 3.99f);
            }
            Assert.True(QDELETED(minibomb));
        });
        r.Step(Second);

        var frag = Make<obj_item_grenade_frag>(r, center);
        r.In(() => Waiting.Detach(frag.detonate()));
        r.In(() =>
        {
            // the shrapnel: the pellet cloud's ring at shrapnel_radius 4 (9 x 9 less 7 x 7 turfs), one piece each
            var cloud = (datum_component_pellet_cloud)frag.GetComponent(typeof(datum_component_pellet_cloud));
            Assert.Equal(32f, Dm.num(cloud.num_pellets));
            Assert.Null(frag.loc); // nullspaced by the cloud until its pellets are done

            // ex_heavy 1, ex_light 3: the centre is the only medium turf, nothing devastated
            Assert.Equal(0f, SSexplosions.highturf.len);
            Assert.Equal(1f, SSexplosions.medturf.len);
            Assert.True(Dm.@in(center, SSexplosions.medturf));
            foreach (turf t in Dm.each_cast<turf>(SSexplosions.lowturf))
                Assert.InRange(CHEAP_HYPOTENUSE(t.x, t.y, center.x, center.y), 1f, 2.99f);
        });
        r.Step(2 * Second);
        Assert.True(r.In(() => QDELETED(frag)));
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("explosion") == true || h.File?.Contains("explode") == true);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSyndicateBombCountsDownIsDefusedByItsActivationWireAndExplodes()
    {
        var (r, feed, bob) = Room(31605, 21, 19);
        turf spot = r.In(() => locate(10, 9, 1));
        var bomb = Make<obj_machinery_syndicatebomb>(r, spot);
        Assert.Equal("syndicate-bomb-inactive", r.In(() => bomb.icon_state));
        Assert.IsType<obj_item_bombcore_syndicate>(r.In(() => bomb.payload));
        Assert.Equal(6f, r.In(() => bomb.wires.wires.len)); // activate, delay, proceed, unbolt and two boom wires

        // started with the lowest timer: tg's 90 seconds, the beeps, the countdown
        r.In(() => bomb.activate());
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(bomb.active)));
        Assert.Equal("syndicate-bomb-active", r.In(() => bomb.icon_state));
        Assert.True(r.In(() => Dm.truthy(bomb.countdown.started)));
        float before = r.In(() => Dm.num(bomb.seconds_remaining()));
        Assert.InRange(before, 89f, 90f);
        r.Step(3 * Second);
        Assert.InRange(r.In(() => Dm.num(bomb.seconds_remaining())), before - 4, before - 2);
        Assert.Contains(r.Sounds.Heard, h => h.File?.EndsWith("timer.ogg", StringComparison.Ordinal) == true);
        r.Step(Second);
        // the countdown's display (the ghosts' maptext) follows the timer on SSfastprocess
        Assert.InRange(r.In(() => Dm.num(bomb.countdown.displayed_text)), r.In(() => Dm.num(bomb.seconds_remaining())), r.In(() => Dm.num(bomb.seconds_remaining())) + 1);

        // the activation wire cut: defused
        r.In(() => Waiting.NoSleep(bomb.wires.cut(WIRE_ACTIVATE, bob)));
        r.Step(2);
        Assert.False(r.In(() => Dm.truthy(bomb.active)));
        Assert.Equal("syndicate-bomb-inactive", r.In(() => bomb.icon_state));
        Assert.True(feed.Any("The timer stops! The bomb has been defused!"));

        // started again and left to its time: the payload's explosion takes the bomb with it
        r.In(() => Waiting.NoSleep(bomb.wires.cut(WIRE_ACTIVATE, bob))); // mended (a second cut mends it)
        r.In(() => bomb.activate());
        r.In(() => bomb.detonation_timer = world.time + 1);
        r.Step(Second);
        Assert.True(r.In(() => QDELETED(bomb)));
        Assert.True(r.In(() => Dm.truthy(SSexplosions.is_exploding()) || Dm.num(bob.get_brute_loss()) > 0 || QDELETED(bob)), "the payload went off");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePowerSinkDrainsTheApcOnItsCableAndHeats()
    {
        const int Y = 2;
        obj_machinery_power_apc apc = null;
        var (r, _, bob) = Room(31606, extras: w =>
        {
            ((area)GLOB.areas_by_type[typeof(area_misc_testroom)]).requires_power = TRUE;
            for (int x = 2; x <= 7; x++)
            {
                Dm.@new<turf_open_floor_plating>(locate(x, Y, 1));
                Dm.@new<obj_structure_cable>(locate(x, Y, 1));
            }
            apc = Dm.@new<obj_machinery_power_apc_auto_name_directional_south>(locate(3, Y, 1));
        });
        float charge0 = r.In(() => Dm.num(apc.cell.charge));
        Assert.True(charge0 > 0);
        var sink = Make<obj_item_powersink>(r, r.In(() => locate(5, Y, 1)));
        var wrench = Hold<obj_item_wrench>(r, bob);

        r.In(() => Waiting.NoSleep(sink.wrench_act(bob, wrench)));
        Assert.Equal(1f, r.In(() => Dm.num(sink.mode))); // CLAMPED_OFF, on the cable
        Assert.True(r.In(() => Dm.truthy(sink.anchored)));
        r.In(() => Waiting.NoSleep(sink.attack_hand(bob)));
        Assert.Equal(2f, r.In(() => Dm.num(sink.mode))); // OPERATING
        r.Step(2);
        Assert.Equal("powersink1", r.In(() => sink.icon_state));
        r.Step(5 * Second);
        Assert.True(r.In(() => sink.internal_heat) > 0, "the drained energy turns into heat");
        Assert.True(r.In(() => Dm.num(apc.cell.charge)) < charge0, "the APC's cell is drained");

        // switched off, it keeps its heat while above the minimum and cools down
        r.In(() => Waiting.NoSleep(sink.attack_hand(bob)));
        Assert.Equal(1f, r.In(() => Dm.num(sink.mode)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnActiveJammerStopsARadioLine()
    {
        var (r, feed, bob) = Room(31607);
        mob_living_carbon_human carol = r.Join(11, 4, "Carol");
        r.Step(2);
        var talker = Hold<obj_item_radio>(r, bob);
        var listener = Make<obj_item_radio>(r, carol.loc as atom);
        r.In(() =>
        {
            talker.set_broadcasting(TRUE);
            talker.set_listening(TRUE);
            listener.set_listening(TRUE);
        });

        r.In(() => talker.talk_into(bob, "the first line", null));
        r.Step(4 * Second);
        Assert.Contains(feed.Chat, c => c.Target == carol && c.Html?.Contains("the first line") == true);

        var jammer = Make<obj_item_jammer>(r, bob.loc as atom);
        r.In(() => Waiting.NoSleep(jammer.attack_self_secondary(bob)));
        Assert.True(r.In(() => Dm.truthy(jammer.active) && Dm.@in(jammer, GLOB.active_jammers)));
        Assert.True(r.In(() => is_within_radio_jammer_range(talker)));
        r.In(() => talker.talk_into(bob, "the jammed line", null));
        r.Step(4 * Second);
        Assert.DoesNotContain(feed.Chat, c => c.Html?.Contains("the jammed line") == true);

        // the disruptor wave switches the radios in view off
        r.In(() => Waiting.NoSleep(jammer.attack_self(bob)));
        Assert.False(r.In(() => Dm.truthy(talker.get_broadcasting())));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMicrolaserKnocksItsTargetOutAfterItsDelay()
    {
        var (r, feed, bob) = Room(31608);
        var laser = Hold<obj_item_healthanalyzer_rad_laser>(r, bob);
        mob_living_carbon_human carol = r.Dummy(5, 4);
        r.Step(2);
        r.In(() => Waiting.NoSleep(laser.interact_with_atom(carol, bob)));
        Assert.True(r.In(() => Dm.truthy(laser.used)));
        Assert.Equal("health1", r.In(() => laser.icon_state));
        Assert.True(feed.Any("Successfully irradiated"));

        // the delay: (intensity + wavelength * 4) * 5 = 250 deciseconds; the cooldown (48) is over well before
        r.Step(10 * Second);
        Assert.False(r.In(() => Dm.truthy(laser.used)));
        Assert.Equal("health", r.In(() => laser.icon_state));
        Assert.False(r.In(() => IS_UNCONSCIOUS(carol)));
        r.Step(16 * Second);
        Assert.True(r.In(() => IS_UNCONSCIOUS(carol)), "a round(10 / 0.075) knockout");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCrossbowsBoltKnocksDownAndPoisons()
    {
        var (r, _, bob) = Room(31609);
        var bow = Hold<obj_item_gun_energy_recharge_ebow>(r, bob);
        mob_living_carbon_human carol = r.Dummy(7, 4);
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(bow.can_shoot())));
        object fired = r.In(() => Waiting.NoSleep(bow.try_fire_gun(carol, bob, "icon-x=16;icon-y=16")));
        Assert.True(Dm.truthy(fired), "the crossbow did not fire");
        // the bolt's knockdown lasts tg's 1 second, so follow the hit's own tick rather than a fixed step
        for (int i = 0; i < Second && r.In(() => Dm.num(carol.get_tox_loss())) <= 0; i++)
            r.Step(1);
        Assert.True(r.In(() => Dm.num(carol.get_tox_loss())) > 0, "the bolt's toxin damage");
        Assert.NotNull(r.In(() => carol.has_status_effect(typeof(datum_status_effect_incapacitating_knockdown))));
        Assert.Equal("crossbow_empty", r.In(() => bow.icon_state));
        r.Step(3 * Second);
        Assert.Equal("crossbow", r.In(() => bow.icon_state)); // recharged after tg's 2 seconds
        r.AssertNoRuntimes();
    }
}
