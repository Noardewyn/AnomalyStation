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
/// Syndicate stage G4d (D316): the turret toolbox bashed with its combat wrench unfolds into a sentry gun that shoots a stranger but
/// spares its builder and folds back into the toolbox; a station turret wakes when someone without a crew record walks in, pops out
/// of its cover and tases them, and its control panel switches it to lethal and off; the turret frame is built step by step into a
/// turret holding the gun put in it.
/// </summary>
public class SyndicateG4dTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed = 71, int width = 13)
    {
        var r = new AudioRoom(width, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.In(() => Waiting.NoSleep(bob.equipOutfit(typeof(datum_outfit_job_assistant))));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Held<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        T item = Dm.@new<T>(who.loc);
        Waiting.NoSleep(who.put_in_active_hand(item));
        return item;
    });

    [Fact]
    public void TheTypesAreLiveAndTheSentryGunSells()
    {
        foreach (Type type in new[]
        {
            typeof(obj_machinery_porta_turret), typeof(obj_machinery_porta_turret_ai), typeof(obj_machinery_porta_turret_syndicate_toolbox),
            typeof(obj_machinery_porta_turret_cover), typeof(obj_machinery_porta_turret_construct), typeof(obj_machinery_turretid),
            typeof(obj_machinery_porta_turret_lasertag_red), typeof(datum_proximity_monitor_advanced_turret_tracking),
            typeof(datum_controller_subsystem_processing_turrets), typeof(datum_element_hostile_machine), typeof(datum_action_turret_toggle),
            typeof(datum_action_turret_quit), typeof(obj_item_storage_toolbox_emergency_turret), typeof(obj_item_wrench_combat),
            typeof(obj_item_gun_energy_e_gun_turret),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
        var r = new AudioRoom(5, 5, seed: 3);
        Assert.False(r.In(() => uplink_item_hidden(Dm.@new<datum_uplink_item_role_restricted_turretbox>())), "the turretbox is hidden");
    }

    [Fact]
    public void TheToolboxUnfoldsIntoASentryThatShootsAStrangerSparesItsBuilderAndFoldsBack()
    {
        var (r, feed, bob) = Room(71);
        var toolbox = Held<obj_item_storage_toolbox_emergency_turret>(r, bob);
        Assert.True(r.In(() => Dm.each<obj_item_wrench_combat>(toolbox.contents).Any()), "the toolbox lacks its combat wrench");
        obj_item_wrench_combat wrench = r.In(() =>
        {
            obj_item_wrench_combat w = Dm.@new<obj_item_wrench_combat>(bob.loc);
            Waiting.NoSleep(bob.put_in_inactive_hand(w));
            bob.set_combat_mode(TRUE);
            Waiting.Detach(toolbox.item_interaction(bob, w, null));
            return w;
        });
        r.Step(3 * Second);
        obj_machinery_porta_turret_syndicate_toolbox turret = r.In(() => Dm.each<obj_machinery_porta_turret_syndicate_toolbox>(bob.loc).SingleOrDefault());
        Assert.NotNull(turret);
        Assert.True(feed.Any("You bash"));
        Assert.Same(turret, r.In(() => toolbox.loc));
        Assert.Same(toolbox, r.In(() => turret.toolbox));
        Assert.Contains(r.In(() => Dm.each(turret.underlays).Select(o => Dm.str(o is IDmAppearance a ? a.icon_state : o)).ToList()), s => s == "toolbox_frame");

        // a stranger walks in: the sentry turns and shoots them; Bob, its builder, is its ally
        mob_living_carbon_human alice = r.Dummy(7, 4);
        r.Step(4 * Second);
        Assert.True(r.In(() => Dm.num(alice.get_brute_loss())) > 0, "the sentry did not shoot the stranger");
        Assert.Equal(0f, r.In(() => Dm.num(bob.get_brute_loss())));
        Assert.Equal("toolbox_lethal", r.In(() => turret.icon_state));
        Assert.Equal((float)EAST, r.In(() => (float)turret.dir));
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.Contains("gun/pistol/shot"));

        // the combat wrench in combat mode folds it back into the toolbox
        r.In(() => Waiting.Detach(turret.item_interaction(bob, wrench, null)));
        r.Step(6 * Second);
        Assert.True(r.In(() => QDELETED(turret)), "the sentry was not folded");
        Assert.True(r.In(() => toolbox.loc is turf), "the toolbox did not drop");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AStationTurretPopsOutOfItsCoverTasesAStrangerAndItsPanelSwitchesItToLethalAndOff()
    {
        var (r, feed, bob) = Room(72);
        obj_machinery_porta_turret turret = r.In(() => Dm.@new<obj_machinery_porta_turret>(locate(10, 4, 1)));
        obj_machinery_turretid panel = r.In(() => Dm.@new<obj_machinery_turretid>(locate(10, 7, 1)));
        r.Step(2);
        obj_machinery_porta_turret_cover cover = r.In(() => turret.cover);
        Assert.NotNull(cover);
        Assert.Same(turret.loc, r.In(() => cover.loc));
        Assert.Equal(60f, r.In(() => turret.invisibility)); // hidden under its cover
        Assert.IsType<obj_item_gun_energy_e_gun_turret>(r.In(() => turret.stored_gun));
        Assert.Equal(typeof(obj_projectile_energy_electrode_ai_turrets), r.In(() => turret.stun_projectile));
        Assert.Equal(1, r.In(() => panel.turrets.len));

        // a stranger (no crew record) walks in range: it pops up and tases them
        mob_living_carbon_human alice = r.Dummy(6, 4);
        r.Step(5 * Second);
        Assert.Equal(1f, r.In(() => turret.raised));
        Assert.Equal(0f, r.In(() => turret.invisibility));
        Assert.Equal("openTurretCover", r.In(() => cover.icon_state));
        Assert.Equal("standard_stun", r.In(() => turret.icon_state));
        Assert.True(r.In(() => Dm.truthy(alice.has_status_effect(typeof(datum_status_effect_tased))) || Dm.num(alice.get_stamina_loss()) > 0), "the turret did not tase the stranger");
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.Contains("taser"));

        // the panel: lethal, then off (the turret pops down behind its cover again)
        r.In(() => panel.toggle_lethal(bob));
        Assert.Equal(TURRET_LETHAL_MODE, r.In(() => turret.mode));
        Assert.Contains(r.In(() => Dm.each(panel.overlays).Select(o => Dm.str(o is IDmAppearance a ? a.icon_state : o)).ToList()), s => s == "kill");
        r.In(() => panel.toggle_on(bob));
        r.Step(2 * Second);
        Assert.Equal(0f, r.In(() => turret.on));
        Assert.Equal(0f, r.In(() => turret.raised));
        Assert.Equal("turretCover", r.In(() => cover.icon_state));
        Assert.Contains(r.In(() => Dm.each(panel.overlays).Select(o => Dm.str(o is IDmAppearance a ? a.icon_state : o)).ToList()), s => s == "standby");
        r.AssertNoRuntimes();
    }

    private const float TURRET_LETHAL_MODE = 1;

    [Fact]
    public void TheTurretFrameIsBuiltIntoATurretHoldingItsGun()
    {
        var (r, feed, bob) = Room(73);
        obj_machinery_porta_turret_construct frame = r.In(() => Dm.@new<obj_machinery_porta_turret_construct>(locate(4, 4, 1)));
        obj_item_wrench wrench = r.In(() => Dm.@new<obj_item_wrench>(bob.loc));
        obj_item_screwdriver screwdriver = r.In(() => Dm.@new<obj_item_screwdriver>(bob.loc));
        obj_item_weldingtool welder = r.In(() => Dm.@new<obj_item_weldingtool_largetank>(bob.loc));
        r.In(() => welder.set_welding(TRUE));
        obj_item_stack_sheet_iron iron = r.In(() => Dm.@new<obj_item_stack_sheet_iron>(bob.loc, 10));
        obj_item_gun_energy_laser gun = r.In(() => Dm.@new<obj_item_gun_energy_laser>(bob.loc));
        obj_item_assembly_prox_sensor prox = r.In(() => Dm.@new<obj_item_assembly_prox_sensor>(bob.loc));

        void Use(Func<DmTask<object>> act, int ticks = 2)
        {
            r.In(() => Waiting.Detach(act()));
            r.Step(ticks);
        }

        Use(() => frame.wrench_act(bob, wrench));
        Use(() => frame.item_interaction(bob, iron, null));
        Use(() => frame.wrench_act(bob, wrench));
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(gun)));
        Use(() => frame.item_interaction(bob, gun, null));
        Assert.Same(frame, r.In(() => gun.loc));
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(prox)));
        Use(() => frame.item_interaction(bob, prox, null));
        Use(() => frame.screwdriver_act(bob, screwdriver));
        Use(() => frame.item_interaction(bob, iron, null));
        Assert.Equal("turret_frame2", r.In(() => frame.icon_state));
        Use(() => frame.welder_act(bob, welder), 4 * Second);

        obj_machinery_porta_turret turret = r.In(() => Dm.each<obj_machinery_porta_turret>(locate(4, 4, 1)).SingleOrDefault());
        Assert.NotNull(turret);
        Assert.True(r.In(() => QDELETED(frame)));
        Assert.Same(gun, r.In(() => turret.stored_gun));
        Assert.Equal(0f, r.In(() => turret.locked));
        Assert.Equal(6f, r.In(() => iron.amount));
        Assert.True(feed.Any("You weld the turret's armor down."));
        r.AssertNoRuntimes();
    }
}
