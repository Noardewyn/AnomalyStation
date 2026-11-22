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

// M7x slice D, the second half: full auto (fullauto.dm), the WT-550 (automatic.dm:66) with its magazine, the battle rifle (automatic.dm:342)
// with its degradation, its recharger repair and its scope (scope.dm, DV98), the electrode's tase (stun.dm) and the gunpoint component.
public partial class ShotgunTests
{
    private const string MouseParams = "icon-x=16;icon-y=16;left=1;button=left";

    private static void Press(AudioRoom r, mob_living_carbon_human who, atom target, string @params = MouseParams)
    {
        r.In(() => who.client.click_intercept_time = 0);
        r.In(() => ClientSeam.MouseDown(who.client, target, @params));
    }

    private static void Release(AudioRoom r, mob_living_carbon_human who, atom target, string @params = MouseParams) =>
        r.In(() => ClientSeam.MouseUp(who.client, target, @params));

    private static float Live(AudioRoom r, obj_item_gun_ballistic gun) => r.In(() => gun.magazine.ammo_count(FALSE)) + r.In(() => gun.chambered?.loaded_projectile != null ? 1f : 0f);

    [Fact]
    public void TheWt550FiresEveryThreeDecisecondsWhileTheButtonIsHeldAndStopsWhenItIsReleased()
    {
        AudioRoom r = Room(7991);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(10, LineY);
        obj_item_gun_ballistic_automatic_wt550 gun = Arm<obj_item_gun_ballistic_automatic_wt550>(r, bob);
        datum_component_automatic_fire auto = r.In(() => gun.GetComponent(typeof(datum_component_automatic_fire)) as datum_component_automatic_fire);
        Assert.NotNull(auto);
        Assert.Equal(3f, r.In(() => auto.autofire_shot_delay)); // automatic_fire(0.3 SECONDS)
        // held in the hand, the component waits for the mouse (wake_up on equip, autofire_on)
        Assert.Equal(AUTOFIRE_STAT_ALERT, r.In(() => auto.autofire_stat));
        Assert.Same(bob.client, r.In(() => auto.clicker));
        float full = Live(r, gun);
        Assert.Equal(20f, full); // a 20 round magazine, one of the rounds in the chamber
        r.In(() => bob.combat_mode = FALSE);

        // the press: the first shot at once (process_shot), then one every 0.3 seconds while it is held
        Press(r, bob, rob);
        Assert.Equal(AUTOFIRE_STAT_FIRING, r.In(() => auto.autofire_stat));
        Assert.Equal(full - 1, Live(r, gun));
        r.Step(3 * Second);
        float shots = full - Live(r, gun);
        Assert.InRange(shots, 10f, 11f); // t = 0, 0.3, ... 3.0 seconds
        // the shots are bullets of the 4.6x30mm casing, ejected casings lie around
        Assert.True(Shots(r).All(p => p is obj_projectile_bullet_c46x30mm));
        Assert.True(r.In(() => Dm.each<obj_item_ammo_casing_c46x30mm>(locate(3, LineY, 1).contents).Count()) >= 9);

        // the release stops it (MouseUp: on_mouse_up, stop_autofiring)
        Release(r, bob, rob);
        Assert.Equal(AUTOFIRE_STAT_ALERT, r.In(() => auto.autofire_stat));
        float after = Live(r, gun);
        r.Step(2 * Second);
        Assert.Equal(after, Live(r, gun));

        // a fresh press fires again; a drag moves the aim to another man; the gun running dry ends it by itself
        mob_living_carbon_human sam = r.Dummy(10, LineY - 1);
        // (tg's WT-550 magazine has no iron left over its twenty rounds, wt550m9 SHEET * 2 against SMALL * 20: its last round leaving is a
        // tg stack_trace about non-positive materials, _box_magazine.dm:74; the run to empty is done on a magazine without materials)
        r.In(() => gun.magazine.custom_materials = null);
        Press(r, bob, rob);
        r.In(() => ClientSeam.MouseDrag(bob.client, sam, MouseParams));
        Assert.Same(sam, r.In(() => auto.target));
        r.Step(10 * Second);
        Assert.Equal(AUTOFIRE_STAT_ALERT, r.In(() => auto.autofire_stat)); // the empty gun's click stopped the burst
        Assert.Equal(0f, Live(r, gun));
        Release(r, bob, rob);
        r.AssertNoRuntimes();
    }

    [Theory]
    [InlineData(typeof(obj_item_gun_ballistic_automatic_proto_unrestricted), 2f)]
    [InlineData(typeof(obj_item_gun_ballistic_automatic_tommygun), 1f)]
    [InlineData(typeof(obj_item_gun_ballistic_automatic_l6_saw_unrestricted), 2f)]
    [InlineData(typeof(obj_item_gun_energy_laser_cybersun_unrestricted), 1.5f)]
    [InlineData(typeof(obj_item_gun_energy_disabler_smg), 1.5f)]
    public void TheOtherFullAutoGunsFireWhileTheButtonIsHeld(Type type, float delay)
    {
        AudioRoom r = Room(7996);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(10, LineY);
        obj_item_gun gun = r.In(() => Dm.@new(type, locate(3, LineY, 1)) as obj_item_gun);
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(gun)));
        r.Step();
        datum_component_automatic_fire auto = r.In(() => gun.GetComponent(typeof(datum_component_automatic_fire)) as datum_component_automatic_fire);
        Assert.NotNull(auto);
        Assert.Equal(delay, r.In(() => auto.autofire_shot_delay));
        r.In(() => bob.combat_mode = FALSE);
        // rounds left of a ballistic gun, charge left of an energy gun
        float Left() => gun is obj_item_gun_ballistic b ? Live(r, b) : r.In(() => Dm.num((gun as obj_item_gun_energy).cell.charge));
        float full = Left();
        Press(r, bob, rob);
        Assert.Equal(AUTOFIRE_STAT_FIRING, r.In(() => auto.autofire_stat));
        r.Step(Second);
        float held = Left();
        Assert.True(held < full, "nothing was fired while the button was held");
        if (gun is obj_item_gun_ballistic)
            Assert.InRange(full - held, 10f / delay - 1, 10f / delay + 1);
        Release(r, bob, rob);
        r.Step(Second);
        Assert.Equal(held, Left());
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheL6SawsDustCoverOpensByAltClickAndShowsOnTheGun()
    {
        AudioRoom r = Room(7997);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        obj_item_gun_ballistic_automatic_l6_saw_unrestricted gun = Arm<obj_item_gun_ballistic_automatic_l6_saw_unrestricted>(r, bob);
        Assert.Contains("l6_door_closed", Overlays(r, gun));
        Assert.Equal("l6closedmag", r.In(() => gun.inhand_icon_state));
        r.In(() => Waiting.NoSleep(gun.click_alt(bob)));
        r.Step();
        Assert.Equal(TRUE, gun.cover_open);
        Assert.Contains("l6_door_open", Overlays(r, gun));
        Assert.Equal("l6openmag", r.In(() => gun.inhand_icon_state));
        // a plain click with the cover open does not fire (try_fire_gun; tg's held full auto goes past it, fullauto.dm do_autofire)
        Assert.False(Dm.truthy(r.In(() => Waiting.NoSleep(gun.try_fire_gun(r.Dummy(10, LineY), bob, MouseParams)))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHeldGunShowsItsShotsLeftAsANumberOnItsPicture()
    {
        AudioRoom r = Room(7998);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(10, LineY);
        // on the floor: no number (DV401, as a stack)
        obj_item_gun_ballistic_automatic_wt550 gun = r.In(() => Dm.@new<obj_item_gun_ballistic_automatic_wt550>(locate(3, LineY, 1)));
        r.Step();
        Assert.Equal("", r.In(() => gun.maptext?.ToString() ?? ""));
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(gun)));
        r.Step();
        Assert.Contains(">20/21<", r.In(() => gun.maptext.ToString())); // a 20-round magazine with one of them chambered: room for one more
        r.In(() => bob.combat_mode = FALSE);
        Press(r, bob, rob);
        Release(r, bob, rob);
        r.Step();
        Assert.Contains(">19/21<", r.In(() => gun.maptext.ToString()));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(gun)));
        r.Step();
        Assert.Equal("", r.In(() => gun.maptext?.ToString() ?? ""));
        // an energy gun: the shots its cell holds
        obj_item_gun_energy_laser laser = Arm<obj_item_gun_energy_laser>(r, bob);
        float shots = r.In(() => Dm.floor(laser.cell.charge / (laser.ammo_type[1] as obj_item_ammo_casing_energy).e_cost));
        Assert.True(shots > 0);
        Assert.Contains(">" + shots + "/" + shots + "<", r.In(() => laser.maptext.ToString()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AShiftedOrDroppedPressDoesNotAutofireAndADroppedGunStopsListening()
    {
        AudioRoom r = Room(7992);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(10, LineY);
        obj_item_gun_ballistic_automatic_wt550 gun = Arm<obj_item_gun_ballistic_automatic_wt550>(r, bob);
        datum_component_automatic_fire auto = r.In(() => gun.GetComponent(typeof(datum_component_automatic_fire)) as datum_component_automatic_fire);
        float full = Live(r, gun);
        // tg: a shift-click (an examine) never starts a burst; a press right beside the man is a plain click
        Press(r, bob, rob, MouseParams + ";shift=1");
        r.Step(Second);
        Assert.Equal(full, Live(r, gun));
        Assert.Equal(AUTOFIRE_STAT_ALERT, r.In(() => auto.autofire_stat));
        mob_living_carbon_human near = r.Dummy(4, LineY);
        Press(r, bob, near);
        r.Step(Second);
        Assert.Equal(full, Live(r, gun));
        // dropped: autofire_off, the press no longer reaches the component
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(gun)));
        r.Step();
        Assert.Equal(AUTOFIRE_STAT_IDLE, r.In(() => auto.autofire_stat));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheWt550TakesAMagazineByTheClickSwapsItAndShowsItsRoundsInThePictures()
    {
        AudioRoom r = Room(7993);
        BalloonChat chat = Chat(r);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(10, LineY);
        obj_item_gun_ballistic_automatic_wt550 gun = Arm<obj_item_gun_ballistic_automatic_wt550>(r, bob);
        obj_item_ammo_box_magazine_wt550m9 mag = r.In(() => gun.magazine as obj_item_ammo_box_magazine_wt550m9);
        Assert.NotNull(mag);
        // the magazine's picture steps by four rounds, the gun shows the magazine and its fill (mag_display, mag_display_ammo)
        Assert.Equal("46x30mmt-20", r.In(() => mag.icon_state));
        Assert.Contains("wt550_mag", Overlays(r, gun));
        Assert.Contains("wt550_mag_100", Overlays(r, gun));
        for (int i = 0; i < 4; i++)
        {
            r.In(() => bob.next_click = -1);
            r.In(() => bob.next_move = -1);
            r.In(() => Waiting.NoSleep(gun.try_fire_gun(rob, bob, MouseParams)));
            r.Step(Second);
        }
        Assert.Equal(15f, r.In(() => gun.magazine.ammo_count())); // four shots: 20 rounds, one chambered at first
        Assert.Equal("46x30mmt-16", r.In(() => mag.icon_state)); // round(15, 4) = 16
        Assert.DoesNotContain("wt550_mag_100", Overlays(r, gun));
        Assert.Contains("wt550_mag_80", Overlays(r, gun));

        // a full magazine swaps with the used one (tac_reloads), the old one comes to the hand
        obj_item_ammo_box_magazine_wt550m9 fresh = r.In(() => Dm.@new<obj_item_ammo_box_magazine_wt550m9>(locate(3, LineY, 1)));
        r.Sounds.Heard.Clear();
        Assert.Equal(ITEM_INTERACT_SUCCESS, Click(r, bob, gun, fresh));
        Assert.Same(fresh, r.In(() => gun.magazine));
        Assert.Contains("magazine swapped", chat.Balloons);
        Assert.Same(mag, r.In(() => bob.get_inactive_held_item()));
        Assert.Equal(20f, r.In(() => gun.magazine.ammo_count()));
        // the armour piercing magazine has its own rounds
        Assert.Equal(typeof(obj_item_ammo_casing_c46x30mm_ap), r.In(() => Dm.initial(typeof(obj_item_ammo_box_magazine_wt550m9_wtap), (obj_item_ammo_box_magazine p) => p.ammo_type)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBattleRifleDegradesAfterTenShotsAndTheRechargerRepairsIt()
    {
        AudioRoom r = Room(7994);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(10, LineY);
        obj_item_gun_ballistic_automatic_battle_rifle gun = Arm<obj_item_gun_ballistic_automatic_battle_rifle>(r, bob);
        r.In(() => gun.degradation_probability = 100); // tg rolls 10% a shot once the ten clean shots are used: the repair test wants it certain
        Assert.Equal(10f, r.In(() => gun.shots_before_degradation));
        Assert.Equal(15f, r.In(() => gun.magazine.max_ammo));
        Assert.Equal(2f, r.In(() => gun.fire_delay));
        Assert.Equal(1.2f, r.In(() => gun.projectile_speed_multiplier), 4);
        Assert.Contains("battle_rifle_stage_3", Overlays(r, gun)); // CEILING(10/10 * 3): the rifle shows how many clean shots are left
        r.In(() => gun.fire_delay = 0); // the shots in a row, without tg's delay between them (fire_cd)

        for (int i = 0; i < 10; i++)
        {
            r.In(() => bob.next_click = -1);
            r.In(() => bob.next_move = -1);
            r.In(() => Waiting.NoSleep(gun.try_fire_gun(rob, bob, MouseParams)));
            r.Step(2); // the fire delay timer resets the gun a tick later (reset_fire_cd)
        }
        r.Step(Second);
        Assert.Equal(0f, r.In(() => gun.shots_before_degradation));
        Assert.Equal(0f, r.In(() => gun.degradation_stage));
        Assert.Equal(4f, r.In(() => gun.magazine.ammo_count())); // 15 rounds, one chambered, ten fired
        // the next shot degrades it one stage: slower rounds and a longer delay (attempt_degradation: 0.1 and 0.5 a stage), the damaged picture
        r.In(() => gun.fire_delay = 2);
        r.In(() => bob.next_click = -1);
        r.In(() => bob.next_move = -1);
        r.In(() => Waiting.NoSleep(gun.try_fire_gun(rob, bob, MouseParams)));
        r.Step(Second);
        Assert.Equal(1f, r.In(() => gun.degradation_stage));
        Assert.Equal(2.5f, r.In(() => gun.fire_delay));
        Assert.Contains("battle_rifle_empty", Overlays(r, gun));
        Assert.Equal("battle_rifle-empty", r.In(() => gun.inhand_icon_state));
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.Contains("spark", StringComparison.Ordinal));

        // in the recharger it is recalibrated, a stage a cycle, then its clean shots come back (recharger.dm)
        r.In(() => (get_area(locate(5, LineY, 1)) as area).requires_power = FALSE);
        obj_machinery_recharger charger = r.In(() => Dm.@new<obj_machinery_recharger>(locate(5, LineY, 1)));
        r.Step(Second);
        Assert.Equal(ITEM_INTERACT_SUCCESS, Dm.num(r.In(() => Waiting.NoSleep(charger.item_interaction(bob, gun, Dm.list())))));
        for (int i = 0; i < 12 * Second && r.In(() => gun.degradation_stage) != 0; i++)
            r.Step();
        Assert.Equal(0f, r.In(() => gun.degradation_stage));
        Assert.Equal(2f, r.In(() => gun.fire_delay));
        Assert.Equal(1.2f, r.In(() => gun.projectile_speed_multiplier), 4);
        r.Sounds.Heard.Clear();
        for (int i = 0; i < 60 * Second && r.In(() => gun.shots_before_degradation) < 10; i++)
            r.Step();
        Assert.Equal(10f, r.In(() => gun.shots_before_degradation));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/ping.ogg");
        Assert.Contains("battle_rifle_stage_3", Overlays(r, gun));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABattleRifleAtItsWorstClicksMostOfTheTimeAnAnEmpMakesItMalfunctionAndAnEmagHardensItsRounds()
    {
        AudioRoom r = Room(7995);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(10, LineY);
        obj_item_gun_ballistic_automatic_battle_rifle gun = Arm<obj_item_gun_ballistic_automatic_battle_rifle>(r, bob);
        r.In(() => gun.fire_delay = 0);
        r.In(() => gun.degradation_stage = gun.degradation_stage_max);
        // stage 5 of 5: a click on three shots in four (process_fire: prob(75))
        int fired = 0;
        for (int i = 0; i < 40; i++)
        {
            float before = r.In(() => gun.magazine.ammo_count());
            r.In(() => bob.next_click = -1);
            r.In(() => bob.next_move = -1);
            r.In(() => Waiting.NoSleep(gun.try_fire_gun(rob, bob, MouseParams)));
            if (r.In(() => gun.magazine.ammo_count()) < before || Shots(r).Count > 0)
                fired++;
            Settle(r);
        }
        Assert.InRange(fired, 1, 25);

        // the emag: heat distribution off, 1.6 damage; a second one does nothing
        obj_item_card_emag card = r.In(() => Dm.@new<obj_item_card_emag>(locate(3, LineY, 1)));
        Assert.Equal(TRUE, r.In(() => Waiting.NoSleep(gun.emag_act(bob, card))));
        Assert.Equal(1.6f, r.In(() => gun.projectile_damage_multiplier), 4);
        Assert.Equal(FALSE, r.In(() => Waiting.NoSleep(gun.emag_act(bob, card))));
        // the EMP: half the time at severity 1, the rifle's clean shots are gone and it starts degrading
        r.In(() => gun.degradation_stage = 0);
        for (int i = 0; i < 20 && !r.In(() => Dm.truthy(gun.emp_malfunction)); i++)
            r.In(() => Waiting.NoSleep(gun.emp_act(1)));
        Assert.True(r.In(() => Dm.truthy(gun.emp_malfunction)));
        Assert.Equal(0f, r.In(() => gun.shots_before_degradation));
        Assert.True(r.In(() => gun.degradation_stage) >= 1);
        r.AssertNoRuntimes();
    }
}
