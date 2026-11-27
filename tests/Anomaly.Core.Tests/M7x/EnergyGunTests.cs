using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M7;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7x;

/// <summary>
/// The M7x slice B (docs/ai/specs/weapons.md 3): tg's gun core and the station's energy guns in a booted toy room. The disabler's beam
/// takes a man to stamina crit in tg's number of hits (beams.dm disabler 30 STAMINA, HITS_TO_CRIT), a laser's burn goes through the
/// security vest's laser armour (run_armor_check), a gun without a pin (or with a pin that refuses) gives the pin's fail message and
/// does not fire (gun.dm handle_pins, pins.dm auth_fail), the energy gun's mode switch (energy.dm select_fire: its casing, shot,
/// sound and charge overlays), the recharger's cycle and states (recharger.dm) and an empty cell's dry fire (shoot_with_empty_chamber).
/// </summary>
public class EnergyGunTests
{
    private const int Second = 20;
    private const int Width = 14, Height = 7, LineY = 4;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(Width, Height, seed: seed);
        // tg's strings/ files (the scars' texts), as the server mounts them
        if (TgClone.Root != null)
            TgStrings.Mount(DmWorldState.Of(r.World).Files, TgClone.Root);
        r.Step(Second);
        return r;
    }

    private static BalloonChat Chat(AudioRoom r)
    {
        var chat = new BalloonChat();
        PresentationSinks.Of(r.World).Messages = chat;
        return chat;
    }

    // Bob (the shooter) holds the gun in his active hand and fires at the target: tg's try_fire_gun, as the click chain calls it.
    private static object Pull(AudioRoom r, mob_living_carbon_human shooter, obj_item_gun gun, atom target) =>
        r.In(() => Waiting.NoSleep(gun.try_fire_gun(target, shooter, "icon-x=16;icon-y=16")));

    private static T Arm<T>(AudioRoom r, mob_living_carbon_human holder, int x = 3) where T : obj_item_gun
    {
        T gun = r.In(() => Dm.@new<T>(locate(x, LineY, 1)));
        r.In(() => Waiting.NoSleep(holder.put_in_active_hand(gun)));
        r.Step();
        return gun;
    }

    private static List<obj_projectile> Shots(AudioRoom r) => r.In(() => Dm.each<obj_projectile>().Where(p => Dm.truthy(p.fired)).ToList()); // the loaded ones sit in their casings, unfired

    // Fire, then step until the beam is gone (hit, or out of range).
    private static obj_projectile Shoot(AudioRoom r, mob_living_carbon_human shooter, obj_item_gun gun, atom target)
    {
        Assert.True(Dm.truthy(Pull(r, shooter, gun, target)), "the gun did not fire");
        obj_projectile shot = Assert.Single(Shots(r));
        for (int i = 0; i < 4 * Second && !r.In(() => QDELETED(shot)); i++)
            r.Step();
        Assert.True(r.In(() => QDELETED(shot)), "the beam is still flying");
        return shot;
    }

    [Fact]
    public void ADisablerBeamTakesAManToStaminaCritInTgsNumberOfHits()
    {
        AudioRoom r = Room(7901);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(9, LineY);
        obj_item_gun_energy_disabler gun = Arm<obj_item_gun_energy_disabler>(r, bob);

        // the disabler's lens and tg's numbers: 30 STAMINA a hit (beams.dm), 20 shots to a full cell (LASER_SHOTS(20, ...))
        Assert.Equal(30f, r.In(() => Dm.initial(typeof(obj_projectile_beam_disabler), (obj_projectile p) => p.damage)));
        Assert.Equal(20f, r.In(() => Dm.num(gun.cell.maxcharge) / Dm.num((gun.ammo_type[1] as obj_item_ammo_casing_energy).e_cost)), 3);
        Assert.Equal(r.In(() => gun.cell.maxcharge), r.In(() => gun.cell.charge));
        float maxHealth = r.In(() => rob.maxHealth);
        float crit = r.In(() => rob.crit_threshold);
        // the number of 30-point hits that take staminaloss to maxHealth - crit_threshold (carbon/damage_procs.dm: stamcrit)
        int expected = (int)Math.Ceiling((maxHealth - crit) / 30f);
        Assert.InRange(expected, 2, 6);

        int hits = 0;
        for (; hits < 10 && !r.In(() => HAS_TRAIT_FROM(rob, TRAIT_INCAPACITATED, STAMINA)); hits++)
        {
            float before = r.In(() => Dm.num(rob.get_stamina_loss()));
            obj_projectile shot = Shoot(r, bob, gun, rob);
            r.Step(2 * Second); // the gun's click cooldown (CLICK_CD_RANGE) is the player's; the beam is gone, the next shot is a new pull
            Assert.Equal(before + 30, r.In(() => Dm.num(rob.get_stamina_loss())), 0.01);
            Assert.Equal(STAMINA, r.In(() => shot.damage_type));
        }
        Assert.Equal(expected, hits);
        Assert.True(r.In(() => HAS_TRAIT_FROM(rob, TRAIT_INCAPACITATED, STAMINA)), "stamina crit");
        // each shot drew its e_cost from the cell (LASER_SHOTS(20): a twentieth of the charge)
        Assert.Equal(1f - hits / 20f, r.In(() => gun.cell.charge / gun.cell.maxcharge), 0.001);
        r.AssertNoRuntimes();
    }

    // DV413: an aiming player (the click's anomaly-aim) shoots at what is in reach instead of bashing it: the turf it stands on (the ceiling over it
    // is that turf's click) and a thing beside it, through tg's click chain; without the aim tg's rule stands (a thing in reach is not fired at).
    [Fact]
    public void AnAimingPlayerShootsAtTheTurfAndTheThingInReachInsteadOfBashingThem()
    {
        AudioRoom r = Room(7911);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        obj_item_gun_energy_disabler gun = Arm<obj_item_gun_energy_disabler>(r, bob);
        obj_structure_window pane = r.In(() => Dm.@new<obj_structure_window_fulltile>(locate(4, LineY, 1)));
        float full = r.In(() => gun.cell.charge);
        string aim = "icon-x=16;icon-y=16;left=1;button=left;" + ClientSeam.AimParam(3 * 32, LineY * 32, 20.8, 90, 60);

        Assert.False(Dm.truthy(Pull(r, bob, gun, pane)), "tg: a thing in reach is not fired at");
        Assert.Equal(full, r.In(() => gun.cell.charge));

        // up at the ceiling: the click is the own turf's
        r.In(() => Waiting.NoSleep(gun.melee_attack_chain(bob, get_turf(bob), params2list(aim))));
        Assert.True(r.In(() => gun.cell.charge) < full, "the shot at the own turf was not fired");
        r.Step(2 * Second);

        // at the window beside: tg's chain reaches the gun's attack_atom, which fires
        float left = r.In(() => gun.cell.charge), whole = r.In(() => Dm.num(pane.get_integrity()));
        r.In(() => Waiting.NoSleep(gun.melee_attack_chain(bob, pane, params2list(aim))));
        Assert.True(r.In(() => gun.cell.charge) < left, "the shot at the window was not fired");
        Assert.Equal(whole, r.In(() => Dm.num(pane.get_integrity()))); // not bashed (a disabler's beam does no harm to glass)
        r.Step(2 * Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AShotPuffsTheBlueFiringEffectOnTheShooterForThreeDecisecondsAndFlashesTheMuzzle()
    {
        AudioRoom r = Room(7909);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(9, LineY);
        obj_item_gun_energy_disabler gun = Arm<obj_item_gun_energy_disabler>(r, bob);
        Assert.Empty(r.In(() => Dm.each(bob.vis_contents).Where(v => v is not obj_effect_abstract_eyelid_effect).ToList()));
        Assert.Equal(0f, r.In(() => gun.light_on));
        Assert.True(Dm.truthy(Pull(r, bob, gun, rob)));
        // tg's disabler lens: firing_effect/blue, a child of the shooter's vis_contents (the client draws it as part of his picture)
        obj_effect_temp_visual_dir_setting_firing_effect puff = Assert.IsType<obj_effect_temp_visual_dir_setting_firing_effect_blue>(Assert.Single(r.In(() => Dm.each(bob.vis_contents).Where(v => v is not obj_effect_abstract_eyelid_effect).ToList())));
        Assert.Equal("firing_effect_blue", r.In(() => puff.icon_state));
        Assert.Equal(1f, r.In(() => gun.light_on)); // muzzle_flash_on: the gun's light for light_time (0.1 s)
        r.Step(Second);
        Assert.Empty(r.In(() => Dm.each(bob.vis_contents).Where(v => v is not obj_effect_abstract_eyelid_effect).ToList()));
        Assert.True(r.In(() => QDELETED(puff)));
        Assert.Equal(0f, r.In(() => gun.light_on));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALaserBurnsThroughTheSecurityVestsLaserArmour()
    {
        AudioRoom r = Room(7902);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(4, LineY); // point blank: ran_zone keeps the chest 93 times in a hundred
        obj_item_clothing_suit_armor_vest vest = r.In(() => Dm.@new<obj_item_clothing_suit_armor_vest>(locate(4, LineY, 1)));
        r.In(() => Waiting.NoSleep(rob.equip_to_slot_or_del(vest, ITEM_SLOT_OCLOTHING)));
        Assert.Same(vest, r.In(() => rob.wear_suit));
        obj_item_gun_energy_laser gun = Arm<obj_item_gun_energy_laser>(r, bob);

        obj_projectile shot = Shoot(r, bob, gun, rob);
        Assert.IsType<obj_projectile_beam_laser>(shot);
        string zone = r.In(() => shot.def_zone);
        float armour = r.In(() => Dm.num(rob.run_armor_check(zone, LASER, "", "", 0, "", TRUE)));
        float expected = 25 * (1 - Math.Min(ARMOR_MAX_BLOCK, armour) / 100);
        Assert.Equal(expected, r.In(() => Dm.num(rob.get_fire_loss())), 3);
        if (zone == BODY_ZONE_CHEST)
        {
            Assert.True(armour > 0, "the vest has laser armour on the chest");
            Assert.True(r.In(() => Dm.num(rob.get_fire_loss())) < 25, "the vest took some of the laser");
        }
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AGunWithoutAPinGivesTheLockedTriggerMessageAndDoesNotFire()
    {
        AudioRoom r = Room(7903);
        BalloonChat chat = Chat(r);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(9, LineY);
        obj_item_gun_energy_temperature gun = Arm<obj_item_gun_energy_temperature>(r, bob); // tg: pin = null
        Assert.Null(r.In(() => gun.pin));

        r.In(() => Waiting.NoSleep(gun.try_fire_gun(rob, bob, "icon-x=16;icon-y=16")));
        r.Step(Second);
        Assert.Empty(Shots(r));
        Assert.Contains("trigger locked, firing pin needed!", chat.Balloons);
        Assert.Contains(chat.Html, h => h.Contains("trigger is locked. This weapon doesn't have a firing pin installed!", StringComparison.Ordinal));
        Assert.Equal(r.In(() => gun.cell.maxcharge), r.In(() => gun.cell.charge)); // nothing was spent
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APinThatRefusesGivesItsFailMessageAndTheGunDoesNotFire()
    {
        AudioRoom r = Room(7904);
        BalloonChat chat = Chat(r);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(9, LineY);
        obj_item_gun_energy_disabler gun = Arm<obj_item_gun_energy_disabler>(r, bob);
        // the disabler starts with the plain pin (which authorises everyone); a test-range pin replaces it and refuses outside the range
        obj_item_firing_pin plain = r.In(() => gun.pin as obj_item_firing_pin);
        Assert.NotNull(plain);
        Assert.Same(gun, r.In(() => plain.gun));
        r.In(() => qdel(plain));
        Assert.Null(r.In(() => gun.pin));
        obj_item_firing_pin_test_range pin = r.In(() => Dm.@new<obj_item_firing_pin_test_range>());
        r.In(() => Waiting.NoSleep(pin.gun_insert(bob, gun)));
        Assert.Same(pin, r.In(() => gun.pin));
        Assert.Same(gun, r.In(() => pin.gun));
        Assert.Same(gun, r.In(() => pin.loc));

        Assert.False(Dm.truthy(Pull(r, bob, gun, rob)));
        r.Step(Second);
        Assert.Empty(Shots(r));
        Assert.Contains("test range check failed!", chat.Balloons);

        // a pin removed by tools is destroyed with the gun's reference (gun.dm screwdriver_act): the pinless gun then locks its trigger
        r.In(() => qdel(pin));
        Assert.Null(r.In(() => gun.pin));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEnergyGunsModeSwitchChangesItsShotSoundAndOverlays()
    {
        AudioRoom r = Room(7905);
        BalloonChat chat = Chat(r);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        obj_item_gun_energy_e_gun gun = Arm<obj_item_gun_energy_e_gun>(r, bob);
        // disable first, kill second (energy_gun.dm ammo_type)
        Assert.Equal(1f, r.In(() => gun.select));
        Assert.IsType<obj_item_ammo_casing_energy_disabler>(r.In(() => gun.ammo_type[1]));
        Assert.IsType<obj_item_ammo_casing_energy_laser>(r.In(() => gun.ammo_type[2]));
        Assert.IsType<obj_projectile_beam_disabler>(r.In(() => gun.chambered.loaded_projectile));
        Assert.Equal(Dm.resource("sound/items/weapons/taser2.ogg").Path, r.In(() => (gun.fire_sound as DmResource).Path));
        List<string> overlays = Overlays(r, gun);
        Assert.Contains("energy_disable", overlays);
        Assert.Equal(4, overlays.Count(o => o == "energy_charge_disable")); // the full cell: four bars (charge_sections)

        r.In(() => Waiting.NoSleep(gun.attack_self(bob)));
        r.Step();

        Assert.Equal(2f, r.In(() => gun.select));
        Assert.IsType<obj_projectile_beam_laser>(r.In(() => gun.chambered.loaded_projectile));
        Assert.Equal("sound/items/weapons/laser.ogg", r.In(() => (gun.fire_sound as DmResource).Path));
        Assert.Contains("set to kill", chat.Balloons);
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.StartsWith("sound/items/weapons/gun_mode_switch", StringComparison.Ordinal));
        overlays = Overlays(r, gun);
        Assert.Contains("energy_kill", overlays);
        Assert.DoesNotContain("energy_disable", overlays);
        Assert.True(overlays.Count(o => o == "energy_charge_kill") >= 3, "the laser's bars: " + string.Join(",", overlays)); // 12 shots of 20 are 3.33 bars: ceil to 4 of the full cell
        // the light of the muzzle changes to the laser's (select_fire set_light_color)
        Assert.Equal(COLOR_SOFT_RED, r.In(() => Dm.str(gun.light_color)));

        // and a third pull of the switch comes back to the disabler (select wraps)
        r.In(() => Waiting.NoSleep(gun.attack_self(bob)));
        Assert.Equal(1f, r.In(() => gun.select));
        Assert.IsType<obj_projectile_beam_disabler>(r.In(() => gun.chambered.loaded_projectile));
        r.AssertNoRuntimes();
    }

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance i ? i.icon_state : Dm.str(o)).ToList());

    [Fact]
    public void AnEmptyCellDryFiresWithTgsClick()
    {
        AudioRoom r = Room(7906);
        BalloonChat chat = Chat(r);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(9, LineY);
        obj_item_gun_energy_disabler gun = Arm<obj_item_gun_energy_disabler>(r, bob);
        r.In(() => gun.cell.charge = 0);
        r.In(() => gun.update_appearance());
        r.Step();
        Assert.Contains("disabler_empty", Overlays(r, gun)); // display_empty: no charge bars, the empty light

        r.Sounds.Heard.Clear();
        Pull(r, bob, gun, rob);
        r.Step(Second);
        Assert.Empty(Shots(r));
        HeardSound click = Assert.Single(r.Sounds.Heard, h => h.File == "sound/items/weapons/gun/general/dry_fire.ogg");
        Assert.Equal(30f, click.Volume); // dry_fire_sound_volume
        r.AssertNoRuntimes();
    }

    private static obj_machinery_recharger Charger(AudioRoom r, int x = 5)
    {
        r.In(() => (get_area(locate(x, LineY, 1)) as area).requires_power = FALSE); // the toy room has no APC: free power, as use_energy gives shuttles
        obj_machinery_recharger charger = r.In(() => Dm.@new<obj_machinery_recharger>(locate(x, LineY, 1)));
        r.Step(Second);
        return charger;
    }

    [Fact]
    public void ARechargerChargesAGunByTgsCycleAndShowsItsStates()
    {
        AudioRoom r = Room(7907);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        obj_machinery_recharger charger = Charger(r);
        Assert.Contains("recharger-empty", Overlays(r, charger));

        obj_item_gun_energy_e_gun gun = Arm<obj_item_gun_energy_e_gun>(r, bob);
        r.In(() => gun.cell.charge = 0);
        r.In(() => gun.update_appearance());
        Assert.Contains("energy_empty", Overlays(r, gun));
        // the gun goes in through the item interaction (recharger.dm item_interaction)
        object result = r.In(() => Waiting.NoSleep(charger.item_interaction(bob, gun, Dm.list())));
        Assert.Equal(ITEM_INTERACT_SUCCESS, result);
        r.Step();
        Assert.Same(gun, r.In(() => charger.charging));
        Assert.Same(charger, r.In(() => gun.loc));
        Assert.True(r.In(() => Dm.truthy(charger.using_power)));
        Assert.Equal(ACTIVE_POWER_USE, r.In(() => charger.use_power));
        Assert.Contains("recharger-charging", Overlays(r, charger));

        // each SSmachines cycle gives the cell chargerate * recharge_coeff * seconds_per_tick (recharger.dm process)
        float step = r.In(() => gun.cell.chargerate * charger.recharge_coeff * Dm.num(SSmachines.wait) / 10f);
        float max = r.In(() => gun.cell.maxcharge);
        Assert.True(step > 0 && step < max, "the cell takes more than one cycle to fill");
        float last = 0;
        int cycles = 0;
        for (int i = 0; i < 400 * Second && r.In(() => gun.cell.charge) < max; i++)
        {
            r.Step();
            float now = r.In(() => gun.cell.charge);
            if (now != last)
            {
                cycles++;
                Assert.Equal(Math.Min(max, last + step), now, 0.01);
                last = now;
            }
        }
        Assert.Equal(max, r.In(() => gun.cell.charge));
        Assert.Equal((int)Math.Ceiling(max / step), cycles);
        r.Step(3 * Second);
        // full: the ping, the full state, the gun's bars back and a chambered shot
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/ping.ogg");
        Assert.Contains("recharger-full", Overlays(r, charger));
        Assert.False(r.In(() => Dm.truthy(charger.using_power)));
        Assert.NotNull(r.In(() => gun.chambered));
        Assert.Contains("energy_empty", Overlays(r, gun)); // the gun's picture is redrawn when it leaves (recharger.dm Exited)

        // taking it out (attack_hand) puts it in the hand, redraws the gun with its full bars and the charger shows empty
        r.In(() => Waiting.NoSleep(charger.attack_hand(bob, Dm.list())));
        r.Step();
        Assert.Null(r.In(() => charger.charging));
        Assert.Same(gun, r.In(() => bob.get_active_held_item()));
        Assert.Contains("recharger-empty", Overlays(r, charger));
        Assert.Equal(4, Overlays(r, gun).Count(o => o == "energy_charge_disable"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARechargerTakesOnlyTheDevicesOnItsListAndNeedsToBeAnchored()
    {
        AudioRoom r = Room(7908);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        BalloonChat chat = Chat(r);
        obj_machinery_recharger charger = Charger(r);
        obj_item_pen pen = r.In(() => Dm.@new<obj_item_pen>(locate(3, LineY, 1)));
        Assert.Equal(NONE, r.In(() => Waiting.NoSleep(charger.item_interaction(bob, pen, Dm.list()))));
        // a gun that has no external power connector is refused (can_charge)
        obj_item_gun_energy_disabler gun = Arm<obj_item_gun_energy_disabler>(r, bob);
        r.In(() => gun.can_charge = FALSE);
        Assert.Equal(ITEM_INTERACT_BLOCKING, r.In(() => Waiting.NoSleep(charger.item_interaction(bob, gun, Dm.list()))));
        Assert.Contains(chat.Html, h => h.Contains("no external power connector", StringComparison.Ordinal));
        r.In(() => gun.can_charge = TRUE);
        // unanchored it is not connected to anything
        r.In(() => Waiting.NoSleep(charger.set_anchored(FALSE)));
        Assert.Equal(ITEM_INTERACT_BLOCKING, r.In(() => Waiting.NoSleep(charger.item_interaction(bob, gun, Dm.list()))));
        Assert.Contains(chat.Html, h => h.Contains("isn't connected to anything!", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFracturedArmSpoilsTheAimAndAKickingGunHurtsIt()
    {
        AudioRoom r = Room(7910);
        BalloonChat chat = Chat(r);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(9, LineY);
        obj_item_gun_energy_laser gun = Arm<obj_item_gun_energy_laser>(r, bob);
        // a heavy gun uses both hands, so either arm's fracture counts (bones.dm firing_with_messed_up_hand); the arm drops the gun when it breaks
        r.In(() => gun.weapon_weight = WEAPON_HEAVY);
        datum_wound_blunt_bone wound = r.In(() => (datum_wound_blunt_bone)Waiting.NoSleep(bob.cause_wound_of_type_and_severity(WOUND_BLUNT, (obj_item_bodypart)bob.get_bodypart(BODY_ZONE_L_ARM), WOUND_SEVERITY_SEVERE)));
        Assert.NotNull(wound);
        // the shot's bonus spread: +15 * severity (no splint) to the top of the roll
        DmList values = r.In(() => Dm.list(0, 0));
        r.In(() => wound.firing_with_messed_up_hand(bob, gun, rob, null, "", values));
        Assert.Equal(15f * WOUND_SEVERITY_SEVERE, r.In(() => Dm.num(values[MAX_BONUS_SPREAD_INDEX])));
        Assert.Equal(0f, r.In(() => Dm.num(values[MIN_BONUS_SPREAD_INDEX])));
        // a laser does not kick (recoil 0): nothing hurts; a kicking one hurts the arm one time in two (prob(25 * (severity - 1)))
        float brute = r.In(() => Dm.num(bob.get_brute_loss()));
        r.In(() => wound.firing_with_messed_up_hand(bob, gun, rob, null, "", Dm.list(0, 0)));
        Assert.Equal(brute, r.In(() => Dm.num(bob.get_brute_loss())));
        r.In(() => gun.recoil = 5);
        for (int i = 0; i < 40; i++)
            r.In(() => wound.firing_with_messed_up_hand(bob, gun, rob, null, "", Dm.list(0, 0)));
        r.Step();
        Assert.Contains(chat.Html, h => h.Contains("explodes with pain as", StringComparison.Ordinal));
        Assert.True(r.In(() => Dm.num(bob.get_brute_loss())) > brute, "the kick hurt the arm");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALaserGunCauterizesACutThatBleedsAndBurnsTheLimb()
    {
        AudioRoom r = Room(7911);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(4, LineY);
        obj_item_gun_energy_laser gun = Arm<obj_item_gun_energy_laser>(r, bob);
        datum_wound_slash_flesh cut = r.In(() => (datum_wound_slash_flesh)Waiting.NoSleep(rob.cause_wound_of_type_and_severity(WOUND_SLASH, (obj_item_bodypart)rob.get_bodypart(BODY_ZONE_L_ARM), WOUND_SEVERITY_CRITICAL)));
        Assert.NotNull(cut);
        r.In(() => cut.blood_flow = 30); // a bleeding cut (the critical slash starts low)
        float flow = 30;
        float burn = r.In(() => Dm.num(rob.get_fire_loss()));
        // tg: a laser's 25 against the cut, bleeding 5 less (damage / 5) after the do_after of the wound's base treat time
        r.Command(() => Waiting.Detach(cut.las_cauterize(gun, bob)));
        r.Step(Second);
        Assert.Equal(flow, r.In(() => Dm.num(cut.blood_flow))); // still aiming
        r.Step(15 * Second);
        // the cauterize takes damage / 5 off the flow (slash.dm las_cauterize), and the burn that hits the limb cauterizes the cut further
        Assert.True(r.In(() => Dm.num(cut.blood_flow)) <= flow - 5, $"the cut bled {flow} before, {r.In(() => Dm.num(cut.blood_flow))} after");
        Assert.True(r.In(() => Dm.num(rob.get_fire_loss())) > burn, "the laser burnt the limb");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHeadOfSecuritysGunHasThreeModesAndAnIndestructibleCell()
    {
        AudioRoom r = Room(7912);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        obj_item_gun_energy_e_gun_hos gun = Arm<obj_item_gun_energy_e_gun_hos>(r, bob);
        Assert.IsType<obj_item_stock_parts_power_store_cell_hos_gun>(r.In(() => gun.cell));
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(gun.cell.resistance_flags, INDESTRUCTIBLE))), "the cell inherits the gun's INDESTRUCTIBLE");
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(gun.cell.resistance_flags, BOMB_PROOF))));
        Assert.Equal(3, r.In(() => gun.ammo_type.Count));
        Assert.IsType<obj_item_ammo_casing_energy_disabler_hos>(r.In(() => gun.ammo_type[1]));
        Assert.IsType<obj_item_ammo_casing_energy_laser_hos>(r.In(() => gun.ammo_type[2]));
        Assert.IsType<obj_item_ammo_casing_energy_ion_hos>(r.In(() => gun.ammo_type[3]));
        // the three pulls of the switch walk the lenses and come round: the third is the weak ion bolt
        r.In(() => Waiting.NoSleep(gun.attack_self(bob)));
        r.In(() => Waiting.NoSleep(gun.attack_self(bob)));
        Assert.Equal(3f, r.In(() => gun.select));
        Assert.IsType<obj_projectile_ion_weak>(r.In(() => gun.chambered.loaded_projectile));
        Assert.Equal("sound/items/weapons/ionrifle.ogg", r.In(() => (gun.fire_sound as DmResource).Path));
        r.In(() => Waiting.NoSleep(gun.attack_self(bob)));
        Assert.Equal(1f, r.In(() => gun.select));
        // the lenses cost by tg's LASER_SHOTS with the 1.2 cell factor: 20 disabler shots, 12 laser shots of a full 1.2x cell
        Assert.Equal(20f, r.In(() => Dm.num(gun.cell.maxcharge) / Dm.num((gun.ammo_type[1] as obj_item_ammo_casing_energy).e_cost)), 0.01);
        Assert.Equal(12f, r.In(() => Dm.num(gun.cell.maxcharge) / Dm.num((gun.ammo_type[2] as obj_item_ammo_casing_energy).e_cost)), 0.01);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTemperatureGunsBeamsCoolAndHeatAMansBodyAndTheIonBoltBurstsAnEmp()
    {
        AudioRoom r = Room(7913);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(4, LineY);
        obj_item_gun_energy_temperature_security gun = Arm<obj_item_gun_energy_temperature_security>(r, bob);
        Assert.IsType<obj_item_firing_pin>(r.In(() => gun.pin)); // the security kind has the plain pin
        float t0 = r.In(() => rob.bodytemperature);
        float protection = r.In(() => 1 - Dm.num(rob.get_insulation_protection(rob.bodytemperature - 50)));
        obj_projectile shot = Shoot(r, bob, gun, rob);
        Assert.IsType<obj_projectile_temp>(shot);
        // the freeze beam: -50 * thermal protection - 50 (temperature.dm on_hit)
        Assert.Equal(t0 + protection * -50 + -50, r.In(() => rob.bodytemperature), 0.5);
        r.Step(2 * Second);
        // the second lens heats: +100 and 10 fewer wet stacks
        r.In(() => Waiting.NoSleep(gun.attack_self(bob)));
        Assert.IsType<obj_projectile_temp_hot>(r.In(() => gun.chambered.loaded_projectile));
        // the hot beam right after Rob's Life has run, so his body's own warming and cooling stays out of the shot
        float fired = r.In(() => SSmobs.times_fired);
        for (int i = 0; i < 4 * Second && r.In(() => SSmobs.times_fired) == fired; i++)
            r.Step();
        float t1 = r.In(() => rob.bodytemperature);
        float protection2 = r.In(() => 1 - Dm.num(rob.get_insulation_protection(rob.bodytemperature + 100)));
        Shoot(r, bob, gun, rob);
        Assert.Equal(t1 + protection2 * 100 + 100, r.In(() => rob.bodytemperature), 0.5);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnIonBoltEmpsWhatItHits()
    {
        AudioRoom r = Room(7914);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(7, LineY);
        obj_item_stock_parts_power_store_cell cell = r.In(() => Dm.@new<obj_item_stock_parts_power_store_cell>(locate(7, LineY, 1)));
        r.In(() => Waiting.NoSleep(cell.give(cell.maxcharge)));
        obj_item_gun_energy_ionrifle gun = Arm<obj_item_gun_energy_ionrifle>(r, bob);
        obj_projectile shot = Shoot(r, bob, gun, rob);
        Assert.IsType<obj_projectile_ion>(shot);
        r.Step(Second);
        Assert.True(r.In(() => cell.charge) < r.In(() => cell.maxcharge), "the pulse (radius 1) drained the cell on the target's tile");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheClownPinHonksAndTheUltraPinLetsAClumsyManFire()
    {
        AudioRoom r = Room(7915);
        BalloonChat chat = Chat(r);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(9, LineY);
        obj_item_gun_energy_disabler gun = Arm<obj_item_gun_energy_disabler>(r, bob);
        // the honk pin replaces the plain one by force (pins.dm interact_with_atom: force_replace)
        obj_item_firing_pin_clown honk = r.In(() => Dm.@new<obj_item_firing_pin_clown>(locate(3, LineY, 1)));
        r.In(() => Waiting.NoSleep(bob.put_in_inactive_hand(honk)));
        r.In(() => Waiting.NoSleep(honk.interact_with_atom(gun, bob, Dm.list())));
        r.Step();
        Assert.Same(honk, r.In(() => gun.pin));
        Assert.False(Dm.truthy(Pull(r, bob, gun, rob)));
        Assert.Contains("honk!", chat.Balloons);
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/bikehorn.ogg");
        Assert.Empty(Shots(r));
        // the ultra pin turns the gun's clumsy check off while it sits in it and back on when it leaves (gun_insert, gun_remove)
        obj_item_firing_pin_clown_ultra ultra = r.In(() => Dm.@new<obj_item_firing_pin_clown_ultra>(locate(3, LineY, 1)));
        r.In(() => Waiting.NoSleep(ultra.gun_insert(bob, gun)));
        Assert.Equal(0f, r.In(() => gun.clumsy_check));
        r.In(() => ultra.gun_remove(bob));
        Assert.Equal(1f, r.In(() => gun.clumsy_check));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnEmpDrainsTheGunsCellAndRechambersWhatIsLeft()
    {
        AudioRoom r = Room(7916);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        obj_item_gun_energy_laser gun = Arm<obj_item_gun_energy_laser>(r, bob);
        float full = r.In(() => gun.cell.maxcharge);
        // the gun's emp_act pulses its contents first (gun.dm: the cell loses charge / severity itself), then energy.dm takes
        // round(charge / emp_resistance / severity) more, empties the chamber and fills it again from what is left
        r.In(() => Waiting.NoSleep(gun.emp_act(EMP_LIGHT)));
        float afterCell = full - full / EMP_LIGHT;
        Assert.Equal(afterCell - Math.Round(afterCell / 1f / EMP_LIGHT), r.In(() => gun.cell.charge), 0.5);
        Assert.NotNull(r.In(() => gun.chambered));
        r.In(() => gun.cell.charge = 100); // less than a laser shot, and what the pulses leave is less still
        r.In(() => Waiting.NoSleep(gun.emp_act(EMP_LIGHT)));
        Assert.Null(r.In(() => gun.chambered)); // the shot cannot be paid: the chamber stays empty
        Assert.False(Dm.truthy(r.In(() => gun.can_shoot())));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ExaminingAGunTellsItsPinAndItsDamage()
    {
        AudioRoom r = Room(7917);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        obj_item_gun_energy_disabler gun = Arm<obj_item_gun_energy_disabler>(r, bob);
        Func<string> look = () => string.Join("\n", r.In(() => Dm.each(Waiting.NoSleep(gun.examine(bob)) as DmList).Select(o => Dm.str(o)).ToList()));
        Assert.Contains("It has an electronic firing pin installed.", look(), StringComparison.Ordinal);
        Assert.Contains("could be removed with some <b>tools</b>", look(), StringComparison.Ordinal);
        r.In(() => gun.atom_integrity = gun.max_integrity * 0.5f);
        Assert.Contains("It appears heavily damaged.", look(), StringComparison.Ordinal);
        r.In(() => qdel(gun.pin));
        Assert.Contains("It doesn't have a <b>firing pin</b> installed, and won't fire.", look(), StringComparison.Ordinal);
        r.AssertNoRuntimes();
    }
}
