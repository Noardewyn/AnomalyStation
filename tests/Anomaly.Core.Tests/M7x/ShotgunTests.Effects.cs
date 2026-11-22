using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M7;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7x;

// M7x slice D, the third part: the electrode's tase (stun.dm /datum/status_effect/tased), the gunpoint component (a right click with a gun
// holds a man up) and the battle rifle's scope (scope.dm, DV98: the "scope" fullscreen and its zoom in the tracker's layer).
public partial class ShotgunTests
{
    // the modifiers of a right click (the click chain's base_ranged_item_interaction sends the item's secondary signal)
    private static DmList RightClick() => params2list("icon-x=16;icon-y=16;right=1;button=right");

    [Fact]
    public void AnElectrodeTasesAManUntilItsFirerClicksTheCordOffOrTheGunLeavesTheHand()
    {
        AudioRoom r = Room(8001);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(6, LineY);
        obj_item_gun_energy_e_gun gun = Arm<obj_item_gun_energy_e_gun>(r, bob);
        float charge = r.In(() => gun.cell.charge);

        // the electrode flies from the gun (fired_from) and the firer (a taser's bolt: ammo/energy/stun.dm)
        obj_projectile_energy_electrode bolt = r.In(() =>
        {
            var p = Dm.@new<obj_projectile_energy_electrode>(get_turf(bob));
            p.firer = bob;
            p.fired_from = gun;
            Waiting.NoSleep(p.aim_projectile(rob, bob, params2list("icon-x=16;icon-y=16")));
            Waiting.NoSleep(p.fire());
            return p;
        });
        for (int i = 0; i < 2 * Second && !r.In(() => QDELETED(bolt)); i++)
            r.Step();
        Assert.True(r.In(() => QDELETED(bolt)));
        datum_status_effect_tased tase = r.In(() => rob.has_status_effect(typeof(datum_status_effect_tased)) as datum_status_effect_tased);
        Assert.NotNull(tase);
        Assert.Same(gun, r.In(() => tase.taser));
        Assert.Same(bob, r.In(() => tase.firer));
        Assert.Equal(60f, r.In(() => tase.stamina_per_second)); // tase_stamina = 60
        Assert.True(r.In(() => rob.alerts?.Contains(typeof(datum_status_effect_tased)) == true || Dm.truthy(rob.alerts?[typeof(datum_status_effect_tased)])), "the Tased! alert");
        Assert.NotNull(r.In(() => rob.has_movespeed_modifier(typeof(datum_movespeed_modifier_being_tased)) as object));
        Assert.True(r.In(() => Dm.truthy(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_tasing_someone)))), "the tasing man is slowed");
        Assert.NotNull(r.In(() => tase.tase_line)); // the cord between them

        // each tick drains the gun's cell and the man's stamina (tick, do_tase_with)
        float stamina0 = r.In(() => Dm.num(rob.get_stamina_loss()));
        for (int i = 0; i < Second + Second / 2 && r.In(() => Dm.num(rob.get_stamina_loss())) - stamina0 <= 40; i++) // two ticks, however the Master's stagger falls
            r.Step();
        Assert.True(r.In(() => Dm.num(rob.get_stamina_loss())) - stamina0 > 40, "the man weakens: " + r.In(() => Dm.num(rob.get_stamina_loss())));
        Assert.True(r.In(() => gun.cell.charge) < charge, "the gun pays for it");
        Assert.True(r.In(() => Dm.truthy(rob.has_status_effect(typeof(datum_status_effect_jitter)))), "he jitters");

        // the firer clicks on the man: end_tase (user_cancel_tase, COMSIG_MOB_CLICKON)
        r.In(() => bob.next_move = -1);
        r.In(() => bob.next_click = -1);
        r.In(() => Waiting.NoSleep(bob.ClickOn(rob, "icon-x=16;icon-y=16;left=1")));
        r.Step();
        Assert.Null(r.In(() => rob.has_status_effect(typeof(datum_status_effect_tased))));
        Assert.False(r.In(() => Dm.truthy(rob.has_movespeed_modifier(typeof(datum_movespeed_modifier_being_tased)))));
        Assert.False(r.In(() => Dm.truthy(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_tasing_someone)))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AGunsRightClickHoldsAManUpAndTheHolderFiresChargedWhenHeMoves()
    {
        AudioRoom r = Room(8002);
        BalloonChat chat = Chat(r);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(5, LineY); // a hold-up reaches two tiles (GUNPOINT_SHOOTER_STRAY_RANGE)
        r.In(() => rob.dir = WEST);
        obj_item_gun_ballistic_revolver_c38_detective gun = Arm<obj_item_gun_ballistic_revolver_c38_detective>(r, bob);
        r.In(() => bob.set_combat_mode(FALSE));

        // the right click (interact_with_atom_secondary): half a second of aiming, then the component
        r.In(() => Waiting.Detach(gun.interact_with_atom_secondary(rob, bob, Dm.list())));
        r.Step(Second);
        datum_component_gunpoint point = r.In(() => bob.GetComponent(typeof(datum_component_gunpoint)) as datum_component_gunpoint);
        Assert.NotNull(point);
        Assert.Same(rob, r.In(() => point.target));
        Assert.Same(gun, r.In(() => point.weapon));
        Assert.NotNull(r.In(() => rob.has_status_effect(typeof(datum_status_effect_grouped_heldup))));
        Assert.NotNull(r.In(() => rob.has_status_effect(typeof(datum_status_effect_grouped_surrender))));
        Assert.NotNull(r.In(() => bob.has_status_effect(typeof(datum_status_effect_holdup))));
        Assert.Contains(chat.Html, h => h.Contains("aims", StringComparison.Ordinal));
        Assert.Equal(1.25f, r.In(() => point.damage_mult));
        // a second right click: already holding him up
        Assert.Equal(ITEM_INTERACT_BLOCKING, r.In(() => Waiting.NoSleep(gun.interact_with_atom_secondary(rob, bob, Dm.list()))));
        Assert.Contains("already holding them up!", chat.Balloons);

        // steadied: the second stage doubles the damage (GUNPOINT_DELAY_STAGE_2 = 2.5 seconds after the start)
        r.Step(3 * Second);
        Assert.Equal(2, r.In(() => point.stage));
        Assert.Equal(2f, r.In(() => point.damage_mult));

        // he moves: the charged shot (trigger_reaction: COMSIG_MOVABLE_MOVED of the target), bullet damage 25 times the stage
        r.In(() => bob.next_click = -1);
        r.In(() => bob.next_move = -1);
        float hp = r.In(() => Dm.num(rob.get_brute_loss()));
        r.In(() => Waiting.NoSleep(rob.forceMove(locate(6, LineY, 1))));
        List<obj_projectile> shots = Shots(r);
        Assert.Single(shots);
        Assert.Equal(50f, r.In(() => Dm.num(shots[0].damage)));
        for (int i = 0; i < 4 * Second && Shots(r).Count > 0; i++)
            r.Step();
        Assert.True(r.In(() => Dm.num(rob.get_brute_loss())) > hp, "he was shot");
        Assert.Null(r.In(() => bob.GetComponent(typeof(datum_component_gunpoint))));
        Assert.Null(r.In(() => rob.has_status_effect(typeof(datum_status_effect_grouped_heldup))));
        Assert.Null(r.In(() => bob.has_status_effect(typeof(datum_status_effect_holdup))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADroppedGunBreaksTheHoldUpWithoutAShot()
    {
        AudioRoom r = Room(8003);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(5, LineY);
        obj_item_gun_ballistic_revolver_c38_detective gun = Arm<obj_item_gun_ballistic_revolver_c38_detective>(r, bob);
        r.In(() => bob.set_combat_mode(FALSE));
        r.In(() => Waiting.Detach(gun.interact_with_atom_secondary(rob, bob, Dm.list())));
        r.Step(Second);
        Assert.NotNull(r.In(() => bob.GetComponent(typeof(datum_component_gunpoint))));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(gun)));
        r.Step();
        Assert.Null(r.In(() => bob.GetComponent(typeof(datum_component_gunpoint))));
        Assert.Null(r.In(() => rob.has_status_effect(typeof(datum_status_effect_grouped_heldup))));
        Assert.Empty(Shots(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBattleRifleScopesInWithARightClickAndTheClientZoomsByItsRangeModifier()
    {
        AudioRoom r = Room(8004);
        BalloonChat chat = Chat(r);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(10, LineY);
        obj_item_gun_ballistic_automatic_battle_rifle gun = Arm<obj_item_gun_ballistic_automatic_battle_rifle>(r, bob);
        // examine: the right click zooms (on_examine)
        DmList lines = r.In(() => Waiting.NoSleep(gun.examine(bob)) as DmList);
        Assert.Contains(Dm.each(lines).Select(Dm.str), l => l.Contains("scope in", StringComparison.Ordinal));
        r.Sounds.Heard.Clear();
        Assert.Equal((float)ITEM_INTERACT_BLOCKING, Dm.num(r.In(() => Waiting.NoSleep(rob.base_ranged_item_interaction(bob, gun, RightClick())))));
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_USER_SCOPED)));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/weapons/scope.ogg");
        // the tg screen: the "scope1" fullscreen (a gun's), carrying the range modifier (2) in the hundredths of its layer
        atom_movable_screen_fullscreen_cursor_catcher_scope tracker = r.In(() => bob.screens?["scope"] as atom_movable_screen_fullscreen_cursor_catcher_scope);
        Assert.NotNull(tracker);
        Assert.Equal("scope1", r.In(() => tracker.icon_state));
        Assert.Equal(2f, r.In(() => tracker.range_modifier));
        Assert.Equal(FULLSCREEN_LAYER + 0.02f, r.In(() => tracker.layer), 5);
        Assert.Contains(tracker, r.In(() => Dm.each<atom_movable_screen_fullscreen_cursor_catcher_scope>(bob.client.screen).ToList()));
        // the crosshair stays the aim: no redirect of the shot to a cursor turf (DV98)
        Assert.Null(r.In(() => tracker.given_turf));
        // already zoomed
        Assert.Equal((float)ITEM_INTERACT_BLOCKING, Dm.num(r.In(() => Waiting.NoSleep(rob.base_ranged_item_interaction(bob, gun, RightClick())))));
        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_USER_SCOPED))); // the second right click scopes out (do_secondary_zoom)
        Assert.Null(r.In(() => bob.screens?["scope"]));
        // (clear_fullscreen fades the vignette out over a second, tg's animated = 10, then takes it off the screen)
        r.Step(2 * Second);
        Assert.DoesNotContain(tracker, r.In(() => Dm.each<atom_movable_screen_fullscreen_cursor_catcher_scope>(bob.client.screen).ToList()));

        // scoped in again, a drop of the rifle ends it (swap hands and moving the item do too)
        r.In(() => Waiting.NoSleep(rob.base_ranged_item_interaction(bob, gun, RightClick())));
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_USER_SCOPED)));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(gun)));
        r.Step();
        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_USER_SCOPED)));
        Assert.Null(r.In(() => bob.screens?["scope"]));
        r.AssertNoRuntimes();
    }
}
