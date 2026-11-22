// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/gun.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice B: the gun core (D175). Whole but for the suicide (handle_suicide), the traitor mail (syndicate G4a, D316: translated),
// the fantasy bonuses (gaps GU1: translated; the fantasy component that calls them is not), the seclight and bayonet attachment points
// and the suppressor reset (clear_suppressor): those are
// live-partial rows (docs/ai/specs/live-partial.txt, section M7x slice B) and docs/ai/specs/not-translated.md section 47. M7x slice D: the
// gunpoint component (the right click's hold-up) and the automatic fire component's four gun procs. The var block is generated (D111):
// `pin` is an `object` that holds the pin's typepath until Initialize turns it into the pin object (types tool, ObjectHoldingVars).
// The /obj/item/gun skeleton of datums/components/fullauto.cs moved here.

// tg: code/modules/projectiles/gun.dm:3
[DmPath("/obj/item/gun")]
public partial class obj_item_gun : obj_item
{
    // tg: code/modules/projectiles/gun.dm:1  #define DUALWIELD_PENALTY_EXTRA_MULTIPLIER 1.4 (file-local)
    private const float DUALWIELD_PENALTY_EXTRA_MULTIPLIER = 1.4f;
    // tg: code/modules/projectiles/gun.dm:2  #define FIRING_PIN_REMOVAL_DELAY 50 (file-local)
    private const float FIRING_PIN_REMOVAL_DELAY = 50;

    // tg: code/modules/projectiles/gun.dm:118
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (ispath(pin))
        {
            pin = Dm.@new(pin as Type);
            Waiting.NoSleep((pin as obj_item_firing_pin).gun_insert(new_gun: this, starting: TRUE));
        }

        Waiting.NoSleep(add_seclight_point());
        Waiting.NoSleep(add_bayonet_point());
        Waiting.NoSleep(add_deep_lore());
        RegisterSignal(this, COMSIG_ITEM_IN_UNWRAPPED_TRAITOR_MAIL, nameof(on_mail_unwrap));
        return dot;
    }

    // tg: code/modules/projectiles/gun.dm:130
    public override object Destroy(float? force = null)
    {
        if (isobj(pin)) //Can still be the initial path, then we skip
            QDEL_NULL(ref pin);
        QDEL_NULL(ref chambered);
        return base.Destroy(force);
    }

    // tg: code/modules/projectiles/gun.dm:136
    public override async DmTask apply_fantasy_bonuses(object bonus = null)
    {
        await base.apply_fantasy_bonuses(bonus);
        fire_delay = Dm.num(modify_fantasy_variable("fire_delay", fire_delay, -Dm.num(bonus), 0));
        burst_delay = Dm.num(modify_fantasy_variable("burst_delay", burst_delay, -Dm.num(bonus), 0));
        projectile_damage_multiplier = Dm.num(modify_fantasy_variable("projectile_damage_multiplier", projectile_damage_multiplier, Dm.num(bonus) / 10, 0.1f));
    }

    // tg: code/modules/projectiles/gun.dm:142
    public override async DmTask remove_fantasy_bonuses(object bonus = null)
    {
        fire_delay = Dm.num(reset_fantasy_variable("fire_delay", fire_delay));
        burst_delay = Dm.num(reset_fantasy_variable("burst_delay", burst_delay));
        projectile_damage_multiplier = Dm.num(reset_fantasy_variable("projectile_damage_multiplier", projectile_damage_multiplier));
        await base.remove_fantasy_bonuses(bonus);
    }

    /// Handles adding [the seclite mount component][/datum/component/seclite_attachable] to the gun.
    /// If the gun shouldn't have a seclight mount, override this with a return.
    /// Or, if a child of a gun with a seclite mount has slightly different behavior or icons, extend this.
    // tg: code/modules/projectiles/gun.dm:151
    public virtual async DmTask add_seclight_point()
    {
        return;
    }

    /// Similarly to add_seclight_point(), handles [the bayonet attachment component][/datum/component/bayonet_attachable]
    // tg: code/modules/projectiles/gun.dm:155
    public virtual async DmTask add_bayonet_point()
    {
        return;
    }

    /// For when you want to add the lore element to a gun, this is the proc to use.
    // tg: code/modules/projectiles/gun.dm:159
    public virtual async DmTask add_deep_lore()
    {
        return;
    }

    // tg: code/modules/projectiles/gun.dm:162
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == pin)
            pin = null;
        if (gone == chambered)
        {
            chambered = null;
            update_appearance();
        }
        return dot;
    }

    /// Clears var and updates icon.
    // tg: code/modules/projectiles/gun.dm:171
    public virtual void clear_suppressor()
    {
        Dm.NotTranslated("code/modules/projectiles/gun.dm:171", "clear_suppressor: the suppressor attachments come with the ballistic guns' suppressors (ROADMAP)");
    }

    // tg: code/modules/projectiles/gun.dm:177
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        if (isliving(hit_atom))
        {
            mob_living thrower = throwingdatum?.get_thrower() as mob_living;
            if (!isliving(thrower))
                return dot;
            Waiting.NoSleep(toss_gun_hard(thrower, hit_atom as mob_living));
        }
        return dot;
    }

    // tg: code/modules/projectiles/gun.dm:185
    public virtual async DmTask<object> toss_gun_hard(mob_living thrower = null, mob_living target = null) //throw a gun at them. They don't expect it.
    {
        if (thrower == null)
            return FALSE;
        if (!HAS_TRAIT(thrower, TRAIT_TOSS_GUN_HARD))
            return FALSE;
        await target.Knockdown(0.5f * SECONDS);
        await target.apply_damage(damage: max(w_class * 5 - throwforce, 10), damagetype: BRUTE, def_zone: thrower.zone_selected, wound_bonus: CANT_WOUND, attacking_item: this);
        return TRUE;
    }

    // tg: code/modules/projectiles/gun.dm:194
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        // DEVIATION(DV27): an inert gun's pin is still the typepath its Initialize did not make (D112): it has no pin to describe
        if (!Dm.truthy(pinless) && !InertSeam.IsInert(this))
        {
            if (Dm.truthy(pin))
            {
                obj_item_firing_pin pin_item = pin as obj_item_firing_pin;
                Dm.append(ref dot, I18n.T("It has \\a [] installed.", pin_item));
                if (Dm.truthy(pin_item.pin_removable))
                    Dm.append(ref dot, span_info(I18n.T("[] looks like [] could be removed with some <b>tools</b>.", pin_item, pin_item.p_they())));
                else
                    Dm.append(ref dot, span_info(I18n.T("[] looks like [] firmly locked in, [] look[] impossible to remove.", pin_item, pin_item.p_theyre(), pin_item.p_they(), pin_item.p_s())));
            }
            else
            {
                Dm.append(ref dot, I18n.T("It doesn't have a <b>firing pin</b> installed, and won't fire."));
            }
        }

        float healthpercent = (Dm.num(atom_integrity) / Dm.num(max_integrity)) * 100;
        if (healthpercent >= 60 && healthpercent <= 95)
            Dm.append(ref dot, span_info(I18n.T("It looks slightly damaged.")));
        else if (healthpercent >= 25 && healthpercent <= 60)
            Dm.append(ref dot, span_warning(I18n.T("It appears heavily damaged.")));
        else if (healthpercent >= 0 && healthpercent <= 25)
            Dm.append(ref dot, span_boldwarning(I18n.T("It's falling apart!")));
        return dot;
    }

    //called after the gun has successfully fired its chambered ammo.
    // tg: code/modules/projectiles/gun.dm:216
    public virtual async DmTask process_chamber(float? empty_chamber = null, float? from_firing = null, float? chamber_next_round = null)
    {
        empty_chamber ??= TRUE;
        from_firing ??= TRUE;
        chamber_next_round ??= TRUE;
        await handle_chamber(empty_chamber, from_firing, chamber_next_round);
        if (Dm.truthy(_listen_lookup?[COMSIG_GUN_CHAMBER_PROCESSED]))
            _SendSignal(COMSIG_GUN_CHAMBER_PROCESSED, Dm.list(this));
    }

    // tg: code/modules/projectiles/gun.dm:220
    public virtual async DmTask handle_chamber(float? empty_chamber = null, float? from_firing = null, float? chamber_next_round = null)
    {
        empty_chamber ??= TRUE;
        from_firing ??= TRUE;
        chamber_next_round ??= TRUE;
        return;
    }

    //check if there's enough ammo/energy/whatever to shoot one time
    //i.e if clicking would make it shoot
    // tg: code/modules/projectiles/gun.dm:225
    public virtual object can_shoot()
    {
        return TRUE;
    }

    // tg: code/modules/projectiles/gun.dm:228
    public virtual object tk_firing(mob_living user = null)
    {
        return Dm.truthy(user.contains(this)) ? FALSE : TRUE;
    }

    // tg: code/modules/projectiles/gun.dm:231
    public virtual async DmTask shoot_with_empty_chamber(mob_living user = null)
    {
        balloon_alert_to_hearers(I18n.T("*click*"));
        playsound(this, dry_fire_sound, dry_fire_sound_volume, TRUE);
    }

    // tg: code/modules/projectiles/gun.dm:235
    public virtual void fire_sounds()
    {
        if (Dm.truthy(suppressed))
            playsound(this, suppressed_sound, suppressed_volume, vary_fire_sound, ignore_walls: FALSE, extrarange: SILENCED_SOUND_EXTRARANGE, falloff_distance: 0);
        else
            playsound(this, fire_sound, fire_sound_volume, vary_fire_sound);
    }

    // tg: code/modules/projectiles/gun.dm:241
    public virtual void muzzle_flash_on()
    {
        if (Dm.truthy(can_muzzle_flash))
        {
            Waiting.NoSleep(set_light_on(TRUE));
            _addtimer(Dm.@new<datum_callback>(this, nameof(muzzle_flash_off)), light_time, TIMER_UNIQUE | TIMER_OVERRIDE, file: "code/modules/projectiles/gun.dm", line: 244);
        }
        else
        {
            muzzle_flash_off();
        }
    }

    // tg: code/modules/projectiles/gun.dm:248
    public virtual void muzzle_flash_off()
    {
        Waiting.NoSleep(set_light_on(FALSE));
    }

    // tg: code/modules/projectiles/gun.dm:251
    public virtual async DmTask<object> shoot_live_shot(mob_living user = null, float? pointblank = null, atom pbtarget = null, float? message = null)
    {
        pointblank ??= FALSE;
        message ??= TRUE;
        if (!Dm.truthy(tk_firing(user)))
        {
            object actual_angle = get_angle((Dm.truthy(user) ? (atom)user : get_turf(this)), pbtarget);
            simulate_recoil(user, recoil, actual_angle);
        }
        fire_sounds();
        muzzle_flash_on();
        if (Dm.truthy(suppressed) || !Dm.truthy(message))
            return FALSE;
        if (Dm.truthy(tk_firing(user)))
        {
            visible_message(
                span_danger(I18n.T("[] fires itself[]", this, Dm.truthy(pointblank) ? Dm.text(" point blank at []!", pbtarget) : "!")),
                blind_message: span_hear(I18n.T("You hear a gunshot!")),
                vision_distance: COMBAT_MESSAGE_RANGE
            );
        }
        else if (Dm.truthy(pointblank))
        {
            if (user == pbtarget)
            {
                user.visible_message(
                    span_danger(I18n.T("[] fires [] point blank at []self!", user, this, user.p_them())),
                    span_userdanger(I18n.T("You fire [] point blank at yourself!", this)),
                    span_hear(I18n.T("You hear a gunshot!")),
                    vision_distance: COMBAT_MESSAGE_RANGE,
                    visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
                );
            }
            else
            {
                user.visible_message(
                    span_danger(I18n.T("[] fires [] point blank at []!", user, this, pbtarget)),
                    span_danger(I18n.T("You fire [] point blank at []!", this, pbtarget)),
                    span_hear(I18n.T("You hear a gunshot!")),
                    vision_distance: COMBAT_MESSAGE_RANGE,
                    ignored_mobs: pbtarget,
                    visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
                );
                to_chat(pbtarget, span_userdanger(I18n.T("[] fires [] point blank at you!", user, this)));
            }
            if (pb_knockback > 0 && ismob(pbtarget))
            {
                mob PBT = pbtarget as mob;
                turf throw_target = get_edge_target_turf(PBT, user.dir) as turf;
                await PBT.throw_at(throw_target, pb_knockback, 2);
            }
        }
        else if (!Dm.truthy(tk_firing(user)))
        {
            user.visible_message(
                span_danger(I18n.T("[] fires []!", user, this)),
                span_danger(I18n.T("You fire []!", this)),
                span_hear(I18n.T("You hear a gunshot!")),
                vision_distance: COMBAT_MESSAGE_RANGE,
                ignored_mobs: user,
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
            );
        }

        if (Dm.truthy(chambered?.integrity_damage))
            await take_damage(chambered.integrity_damage, sound_effect: FALSE);
        return TRUE;
    }

    // tg: code/modules/projectiles/gun.dm:302
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        if (!isliving(loc))
        {
            await base.atom_destruction(damage_flag);
            return;
        }
        mob_living holder = loc as mob_living;
        if (Dm.truthy(holder.is_holding(this)) && !IS_UNCONSCIOUS(holder))
        {
            to_chat(holder, span_boldwarning(I18n.T("[] breaks down!", this)));
            holder.playsound_local(get_turf(this), Dm.resource("sound/items/weapons/smash.ogg"), 50, TRUE);
        }
        await base.atom_destruction(damage_flag);
    }

    // tg: code/modules/projectiles/gun.dm:311
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (!Dm.truthy(Dm.bitand(dot, EMP_PROTECT_CONTENTS)))
        {
            foreach (obj inside in Dm.each<obj>(contents))
                await inside.emp_act(severity);
        }
        return dot;
    }

    // tg: code/modules/projectiles/gun.dm:317
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        object dot = await base.attack_self_secondary(user, modifiers);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.truthy(pinless))
            return null;

        if (!HAS_TRAIT(user, TRAIT_GUNFLIP))
            return null;

        SpinAnimation(4, 2); // The spin happens regardless of the cooldown

        if (!(flip_cooldown <= world.time)) // !COOLDOWN_FINISHED(src, flip_cooldown)
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;

        flip_cooldown = world.time + 3 * SECONDS; // COOLDOWN_START(src, flip_cooldown, 3 SECONDS)
        if (HAS_TRAIT(user, TRAIT_CLUMSY) && prob(40))
        {
            // yes this will sound silly for bows and wands, but that's a "gun" moment for you
            user.visible_message(
                span_danger(I18n.T("While trying to flip [] [] pulls the trigger accidentally!", this, user)),
                span_userdanger(I18n.T("While trying to flip [] you pull the trigger accidentally!", this))
            );
            await process_fire(user, user as mob_living, FALSE, null, (user as mob_living).get_random_valid_zone(even_weights: TRUE) as string);
            await user.dropItemToGround(this, TRUE);
        }
        else
        {
            user.visible_message(
                span_notice(I18n.T("[] spins [] around [] finger by the trigger. That's pretty badass.", user, this, user.p_their())),
                span_notice(I18n.T("You spin [] around your finger by the trigger. That's pretty badass.", this))
            );
            playsound(this, Dm.resource("sound/items/handling/ammobox_pickup.ogg"), 20, FALSE);
        }

        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/projectiles/gun.dm:351
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object fired = await try_fire_gun(interacting_with, user, list2params(modifiers));
        if (!Dm.truthy(fired) && Dm.eq(chambered_attack_block, TRUE) && Dm.truthy(can_shoot()) && isliving(interacting_with))
            return ITEM_INTERACT_BLOCKING;
        return fired;
    }

    // tg: code/modules/projectiles/gun.dm:357
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (Dm.truthy(user.combat_mode) && isliving(interacting_with))
            return ITEM_INTERACT_SKIP_TO_ATTACK; // Gun bash / bayonet attack

        if (!Dm.truthy(can_hold_up) || !isliving(interacting_with))
            return await interact_with_atom(interacting_with, user, modifiers);

        datum_component_gunpoint gunpoint_component = user.GetComponent(typeof(datum_component_gunpoint)) as datum_component_gunpoint;
        if (gunpoint_component != null)
        {
            balloon_alert(user, I18n.T("already holding [] up!", gunpoint_component.target == interacting_with ? "them" : "someone"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (user == interacting_with)
        {
            balloon_alert(user, I18n.T("can't hold yourself up!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(await do_after(user, 0.5f * SECONDS, interacting_with)))
            await user.AddComponent(typeof(datum_component_gunpoint), interacting_with, this);
        return ITEM_INTERACT_SUCCESS;
    }

    // DEVIATION(DV413): an aiming player's click on a thing in reach that nothing took (tg's chain ran: its tool acts, its item interactions,
    // its storage, its own attackby) fires the gun along the crosshair instead of bashing the thing with it. Only a gun that cannot shoot
    // in combat mode (tg's ITEM_INTERACT_SKIP_TO_ATTACK) still bashes.
    public override async DmTask<object> attack_atom(atom attacked_atom = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (ClientSeam.HasAim(modifiers) && !Dm.eq(await fire_gun(attacked_atom, user, FALSE, list2params(modifiers)), ITEM_INTERACT_SKIP_TO_ATTACK))
            return TRUE;
        return await base.attack_atom(attacked_atom, user, modifiers, attack_modifiers);
    }

    // tg: code/modules/projectiles/gun.dm:376
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return await try_fire_gun(interacting_with, user, list2params(modifiers));
    }

    // tg: code/modules/projectiles/gun.dm:379
    public override async DmTask<object> ranged_interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (IN_GIVEN_RANGE(user, interacting_with, GUNPOINT_SHOOTER_STRAY_RANGE))
            return await interact_with_atom_secondary(interacting_with, user, modifiers);
        return await base.ranged_interact_with_atom_secondary(interacting_with, user, modifiers);
    }

    // tg: code/modules/projectiles/gun.dm:384
    public virtual async DmTask<object> try_fire_gun(atom target = null, mob_living user = null, object @params = null)
    {
        return await fire_gun(target, user, await user.Adjacent(target), @params);
    }

    // tg: code/modules/projectiles/gun.dm:387
    public virtual async DmTask<object> fire_gun(atom target = null, object user = null, object flag = null, object @params = null)
    {
        mob_living shooter = user as mob_living; // the slot's `user` is object: a turret firing is not a mob_living (3.4)
        if (QDELETED(target))
            return NONE;
        if (Dm.truthy(firing_burst))
            return NONE;

        datum user_datum = user as datum;
        if (Dm.truthy(Dm.bitand(!Dm.truthy(user_datum._listen_lookup?[COMSIG_MOB_TRYING_TO_FIRE_GUN]) ? (object)NONE : user_datum._SendSignal(COMSIG_MOB_TRYING_TO_FIRE_GUN, Dm.list(user_datum, this, target, flag, @params)), COMPONENT_CANCEL_GUN_FIRE)))
            return NONE;

        if (Dm.truthy(Dm.bitand(!Dm.truthy(this._listen_lookup?[COMSIG_GUN_TRY_FIRE]) ? (object)NONE : this._SendSignal(COMSIG_GUN_TRY_FIRE, Dm.list(this, user, target, flag, @params)), COMPONENT_CANCEL_GUN_FIRE)))
            return NONE;
        if (Dm.truthy(flag)) //It's adjacent, is the user, or is on the user's person
        {
            if (Dm.truthy(Dm.@in(target, shooter.contents))) //can't shoot stuff inside us.
                return NONE;
            // DEVIATION(DV413): a turf next to an aiming player (the ceiling over it, the floor at its feet, a wall) is shot at, not bashed
            if (!ismob(target) && !(target is turf && ClientSeam.HasAim(params2list(@params) as DmList))) //melee attack
                return NONE;
            if (target == shooter && (shooter.zone_selected != BODY_ZONE_PRECISE_MOUTH && Dm.truthy(doafter_self_shoot))) //so we can't shoot ourselves (unless mouth selected)
                return NONE;
        }

        if (user is mob_living)//Check if the user can use the gun, if the user isn't alive(turrets) assume it can.
        {
            mob_living L = shooter;
            if (!Dm.truthy(await can_trigger_gun(L)))
                return NONE;
        }

        if (Dm.truthy(flag) && Dm.truthy(doafter_self_shoot) && shooter.zone_selected == BODY_ZONE_PRECISE_MOUTH)
            return await handle_suicide(shooter as mob_living_carbon_human, target as mob_living_carbon_human, @params);

        if (!Dm.truthy(can_shoot())) //Just because you can pull the trigger doesn't mean it can shoot.
        {
            await shoot_with_empty_chamber(shooter);
            return Dm.truthy(shooter.combat_mode) ? ITEM_INTERACT_SKIP_TO_ATTACK : ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(await check_botched(shooter, target)))
            return NONE;

        object other_hand = shooter.has_hand_for_held_index(shooter.get_inactive_hand_index()); //returns non-disabled inactive hands
        if (weapon_weight == WEAPON_HEAVY && (Dm.truthy(shooter.get_inactive_held_item()) || !Dm.truthy(other_hand)))
        {
            balloon_alert(shooter, I18n.T("use both hands!"));
            return ITEM_INTERACT_BLOCKING;
        }
        //DUAL (or more!) WIELDING
        float bonus_spread = 0;
        float loop_counter = 0;
        if (Dm.truthy(shooter.combat_mode) && !HAS_TRAIT(shooter, TRAIT_NO_GUN_AKIMBO))
        {
            foreach (obj_item_gun gun in Dm.each<obj_item_gun>(shooter.held_items))
            {
                if (gun == this || gun.weapon_weight >= WEAPON_MEDIUM)
                {
                    continue;
                }
                else if (Dm.truthy(await gun.can_trigger_gun(shooter, akimbo_usage: TRUE)))
                {
                    bonus_spread += dual_wield_spread;
                    loop_counter++;
                    _addtimer(Dm.@new<datum_callback>(gun, nameof(obj_item_gun.process_fire), target, shooter, TRUE, @params, null, bonus_spread), loop_counter, file: "code/modules/projectiles/gun.dm", line: 433);
                }
            }
        }

        return await process_fire(target, shooter, TRUE, @params, null, bonus_spread);
    }

    // tg: code/datums/components/fullauto.dm:287
    public virtual async DmTask<object> on_autofire_start(mob_living shooter = null)
    {
        if (Dm.truthy(fire_cd) || Dm.truthy(shooter.incapacitated) || !Dm.truthy(await can_trigger_gun(shooter)))
            return FALSE;
        if (!Dm.truthy(can_shoot()))
        {
            await shoot_with_empty_chamber(shooter);
            return FALSE;
        }
        object other_hand = shooter.has_hand_for_held_index(shooter.get_inactive_hand_index());
        if (weapon_weight == WEAPON_HEAVY && (Dm.truthy(shooter.get_inactive_held_item()) || !Dm.truthy(other_hand)))
        {
            balloon_alert(shooter, I18n.T("use both hands!"));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/datums/components/fullauto.dm:300
    [SignalHandler]
    public virtual object autofire_bypass_check(datum source = null, client clicker = null, atom target = null, turf location = null, object control = null, object @params = null)
    {
        if (clicker.mob.get_active_held_item() != this)
            return COMPONENT_AUTOFIRE_ONMOUSEDOWN_BYPASS;
        return null;
    }

    // tg: code/datums/components/fullauto.dm:306
    [SignalHandler]
    public virtual object do_autofire(datum source = null, atom target = null, mob_living shooter = null, object allow_akimbo = null, object @params = null)
    {
        if (Dm.truthy(fire_cd) || Dm.truthy(shooter.incapacitated))
            return NONE;
        if (!Dm.truthy(can_shoot()))
        {
            Waiting.NoSleep(shoot_with_empty_chamber(shooter));
            return NONE;
        }
        Waiting.Detach(do_autofire_shot(source, target, shooter, allow_akimbo, @params));
        return COMPONENT_AUTOFIRE_SHOT_SUCCESS; //All is well, we can continue shooting.
    }

    // tg: code/datums/components/fullauto.dm:317
    public virtual async DmTask do_autofire_shot(datum source = null, atom target = null, mob_living shooter = null, object allow_akimbo = null, object @params = null)
    {
        obj_item_gun akimbo_gun = shooter.get_inactive_held_item() as obj_item_gun;
        float bonus_spread = 0;
        if (akimbo_gun != null && weapon_weight < WEAPON_MEDIUM && Dm.truthy(allow_akimbo))
        {
            if (akimbo_gun.weapon_weight < WEAPON_MEDIUM && Dm.truthy(await akimbo_gun.can_trigger_gun(shooter)))
            {
                if (!Dm.truthy(akimbo_gun.can_shoot()))
                    _addtimer(Dm.@new<datum_callback>(akimbo_gun, nameof(obj_item_gun.shoot_with_empty_chamber), shooter), 0.1f * SECONDS, file: "code/datums/components/fullauto.dm", line: 324);
                else
                {
                    bonus_spread = dual_wield_spread;
                    _addtimer(Dm.@new<datum_callback>(akimbo_gun, nameof(obj_item_gun.process_fire), target, shooter, TRUE, @params, null, bonus_spread), 0.1f * SECONDS, file: "code/datums/components/fullauto.dm", line: 327);
                }
            }
        }
        await process_fire(target, shooter, TRUE, @params, null, bonus_spread);
    }

    // tg: code/modules/projectiles/gun.dm:439
    public virtual async DmTask<object> check_botched(mob_living user = null, atom target = null)
    {
        if (Dm.truthy(clumsy_check))
        {
            if (user != null)
            {
                if (HAS_TRAIT(user, TRAIT_CLUMSY) && prob(40))
                {
                    object target_zone = user.get_random_valid_zone(blacklisted_parts: Dm.list(BODY_ZONE_CHEST, BODY_ZONE_HEAD, BODY_ZONE_L_ARM, BODY_ZONE_R_ARM), even_weights: TRUE, bypass_warning: TRUE);
                    if (!Dm.truthy(target_zone))
                        return null;
                    to_chat(user, span_userdanger(I18n.T("You shoot yourself in the foot with []!", this)));
                    await process_fire(user, user, FALSE, null, target_zone as string);
                    if (Dm.truthy(user._listen_lookup?[COMSIG_MOB_CLUMSY_SHOOT_FOOT]))
                        user._SendSignal(COMSIG_MOB_CLUMSY_SHOOT_FOOT, Dm.list(user));
                    if (!Dm.truthy(tk_firing(user)) && !HAS_TRAIT(this, TRAIT_NODROP))
                        await user.dropItemToGround(this, TRUE);
                    return TRUE;
                }
            }
        }
        return null;
    }

    // tg: code/modules/projectiles/gun.dm:453
    public override async DmTask<object> can_trigger_gun(mob_living user = null, object akimbo_usage = null)
    {
        // DEVIATION(DV27): an inert gun (the captain's antique laser, a cycler shotgun) never made its pin, cell or magazine (D112)
        if (InertSeam.MissingInitialize(this, "can_trigger_gun", user))
            return FALSE;
        object dot = await base.can_trigger_gun(user, akimbo_usage);
        if (!Dm.truthy(await handle_pins(user)))
            return FALSE;
        return dot;
    }

    // tg: code/modules/projectiles/gun.dm:458
    public virtual async DmTask<object> handle_pins(mob_living user = null)
    {
        if (Dm.truthy(pinless))
            return TRUE;
        if (Dm.truthy(pin))
        {
            obj_item_firing_pin pin_item = pin as obj_item_firing_pin;
            if (Dm.truthy(await pin_item.pin_auth(user)) || Dm.truthy(Dm.bitand(pin_item.obj_flags, EMAGGED)))
            {
                return TRUE;
            }
            else
            {
                await pin_item.auth_fail(user);
                return FALSE;
            }
        }
        else
        {
            to_chat(user, span_warning(I18n.T("[]'s trigger is locked. This weapon doesn't have a firing pin installed!", this)));
            balloon_alert(user, I18n.T("trigger locked, firing pin needed!"));
        }
        return FALSE;
    }

    /// Called to put ammo back in a gun which recharges itself, should call super if successful
    // tg: code/modules/projectiles/gun.dm:473
    public virtual async DmTask recharge_newshot(params object[] __extra)
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_GUN_REPLENISHED_CHARGE]))
            _SendSignal(COMSIG_GUN_REPLENISHED_CHARGE, Dm.list(this));
    }

    // tg: code/modules/projectiles/gun.dm:476
    public virtual async DmTask<object> process_burst(mob_living user = null, atom target = null, float? message = null, object @params = null, string zone_override = null, float? random_spread = null, float? burst_spread_mult = null, float? iteration = null, params object[] __extra)
    {
        message ??= TRUE;
        zone_override ??= "";
        random_spread ??= 0;
        burst_spread_mult ??= 0;
        iteration ??= 0;
        if (user == null || !Dm.truthy(firing_burst))
        {
            firing_burst = FALSE;
            return FALSE;
        }
        if (!issilicon(user))
        {
            if (iteration > 1 && !Dm.truthy(user.is_holding(this))) //for burst firing
            {
                firing_burst = FALSE;
                return FALSE;
            }
        }
        if (chambered?.loaded_projectile == null)
        {
            await shoot_with_empty_chamber(user);
            firing_burst = FALSE;
            return FALSE;
        }

        if (HAS_TRAIT(user, TRAIT_PACIFISM) && Dm.truthy(chambered.harmful)) // Is the bullet chambered harmful?
        {
            to_chat(user, span_warning(I18n.T("[] is lethally chambered! You don't want to risk harming anyone...", this)));
            firing_burst = FALSE;
            return FALSE;
        }

        float sprd = 0;
        if (Dm.truthy(randomspread))
            sprd = round((rand(0, 1) - 0.5f) * DUALWIELD_PENALTY_EXTRA_MULTIPLIER * (random_spread.Value));
        else //Smart spread
            sprd = round((((burst_spread_mult.Value / burst_size) * iteration.Value) - (0.5f + (burst_spread_mult.Value * 0.25f))) * (random_spread.Value));
        before_firing(target, user);
        if (!Dm.truthy(await chambered.fire_casing(target, user, @params, 0, suppressed, zone_override, sprd, this)))
        {
            await shoot_with_empty_chamber(user);
            firing_burst = FALSE;
            return FALSE;
        }
        await shoot_live_shot(user, get_dist(user, target) <= 1 ? TRUE : FALSE, target, message);
        if (iteration >= burst_size)
            firing_burst = FALSE;

        await process_chamber();
        update_appearance();
        return TRUE;
    }

    /**
     * Calculates the final recoil value applied when firing a gun.
     *
     * Arguments:
     * * user - The living mob attempting to fire the gun. Used for preference lookups.
     * * recoil_amount - The raw recoil value to be processed before clamping.
     *
     * Returns:
     * The clamped recoil value after applying all modifiers.
     */
    // tg: code/modules/projectiles/gun.dm:522
    public virtual object calculate_recoil(mob_living user = null, float? recoil_amount = null)
    {
        recoil_amount ??= 0;
        float used_min_recoil = min_recoil;
        if (Dm.truthy(user.client))
        {
            // GAP(lead, modules/client/preferences.cs): user.client.prefs.read_preference(/datum/preference/numeric/min_recoil_multiplier):
            // the preferences are not translated before M9 (client_procs.cs:105) and the preference's default is 100, so the
            // multiplier is 100 / 100 (DV41 pattern; slice A's recoil path reads the same default).
            used_min_recoil *= (100 / 100f);
        }
        return clamp(recoil_amount.Value, used_min_recoil, INFINITY);
    }

    /**
     * Simulates firearm recoil and applies camera feedback when firing.
     *
     * Arguments:
     * * user - The mob firing the gun. Used for recoil calculation and camera shake.
     * * recoil_amount - The base recoil value before modifiers.
     * * firing_angle - The firing direction used to determine camera kick direction.
     */
    // tg: code/modules/projectiles/gun.dm:536
    public virtual void simulate_recoil(mob_living user = null, float? recoil_amount = null, object firing_angle = null)
    {
        recoil_amount ??= 0;
        // DEVIATION(DV96): the first-person kick of the gun's kind and round on top of tg's own recoil (FeltRecoil)
        float total_recoil = Dm.num(calculate_recoil(user, recoil_amount + FeltRecoil.TilesOf(this, user)));

        float actual_angle = Dm.num(firing_angle) + rand(-recoil_deviation, recoil_deviation) + 180;
        if (actual_angle > 360)
            actual_angle -= 360;
        if (total_recoil > 0)
            recoil_camera(user, total_recoil + 1, (total_recoil * recoil_backtime_multiplier) + 1, total_recoil, actual_angle);
    }

    ///returns true if the gun successfully fires
    // tg: code/modules/projectiles/gun.dm:546
    public virtual async DmTask<object> process_fire(atom target = null, mob_living user = null, float? message = null, object @params = null, string zone_override = null, float? bonus_spread = null)
    {
        message ??= TRUE;
        zone_override ??= "";
        bonus_spread ??= 0;
        float base_bonus_spread = 0;
        if (user != null)
        {
            DmList bonus_spread_values = Dm.list(base_bonus_spread, bonus_spread);
            if (Dm.truthy(user._listen_lookup?[COMSIG_MOB_FIRED_GUN]))
                user._SendSignal(COMSIG_MOB_FIRED_GUN, Dm.list(user, this, target, @params, zone_override, bonus_spread_values));
            base_bonus_spread = Dm.num(bonus_spread_values[MIN_BONUS_SPREAD_INDEX]);
            bonus_spread = Dm.num(bonus_spread_values[MAX_BONUS_SPREAD_INDEX]);
        }

        if (Dm.truthy(_listen_lookup?[COMSIG_GUN_FIRED]))
            _SendSignal(COMSIG_GUN_FIRED, Dm.list(this, user, target, @params, zone_override));

        add_fingerprint(user);

        if (Dm.truthy(fire_cd))
            return NONE;

        //Vary by at least this much
        float randomized_bonus_spread = rand(base_bonus_spread, bonus_spread.Value);
        float randomized_gun_spread = Dm.truthy(spread) ? rand(0, spread) : 0;
        float total_random_spread = max(0, randomized_bonus_spread + randomized_gun_spread);
        float burst_spread_mult = rand();

        float modified_burst_delay = burst_delay;
        float modified_fire_delay = fire_delay;
        if (user != null && HAS_TRAIT(user, TRAIT_DOUBLE_TAP))
        {
            modified_burst_delay = ROUND_UP(burst_delay * 0.5f);
            modified_fire_delay = ROUND_UP(fire_delay * 0.5f);
        }

        if (burst_size > 1)
        {
            firing_burst = TRUE;
            fire_cd = TRUE;
            for (float i = 1; i <= burst_size; i++)
            {
                _addtimer(Dm.@new<datum_callback>(this, nameof(process_burst), user, target, message, @params, zone_override, total_random_spread, burst_spread_mult, i), modified_burst_delay * (i - 1), file: "code/modules/projectiles/gun.dm", line: 581);
                _addtimer(Dm.@new<datum_callback>(this, nameof(reset_fire_cd)), modified_fire_delay, file: "code/modules/projectiles/gun.dm", line: 582); // for the case of fire delay longer than burst
            }
        }
        else
        {
            if (chambered == null)
            {
                await shoot_with_empty_chamber(user);
                return Dm.truthy(user.combat_mode) ? ITEM_INTERACT_SKIP_TO_ATTACK : NONE;
            }
            if (HAS_TRAIT(user, TRAIT_PACIFISM) && Dm.truthy(chambered.harmful)) // If the user has the pacifist trait, then they won't be able to fire [src] if the round chambered inside of [src] is lethal.
            {
                to_chat(user, span_warning(I18n.T("[] is lethally chambered! You don't want to risk harming anyone...", this)));
                return NONE;
            }
            float sprd = round((rand(0, 1) - 0.5f) * DUALWIELD_PENALTY_EXTRA_MULTIPLIER * total_random_spread);
            before_firing(target, user);
            if (!Dm.truthy(await chambered.fire_casing(target, user, @params, 0, suppressed, zone_override, sprd, this)))
            {
                await shoot_with_empty_chamber(user);
                return Dm.truthy(user.combat_mode) ? ITEM_INTERACT_SKIP_TO_ATTACK : NONE;
            }

            await shoot_live_shot(user, get_dist(user, target) <= 1 ? TRUE : FALSE, target, message);
            // If gun gets destroyed as a result of firing
            if (!QDELETED(this))
            {
                await process_chamber();
                update_appearance();
                fire_cd = TRUE;
                _addtimer(Dm.@new<datum_callback>(this, nameof(reset_fire_cd)), modified_fire_delay, file: "code/modules/projectiles/gun.dm", line: 601);
            }
        }

        if (user != null)
            await user.update_held_items();
        SSblackbox.record_feedback("tally", "gun_fired", 1, type);
        return TRUE;
    }

    // tg: code/modules/projectiles/gun.dm:605
    public virtual void reset_fire_cd()
    {
        fire_cd = FALSE;
    }

    // tg: code/modules/projectiles/gun.dm:608
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item I = null)
    {
        object dot = await base.screwdriver_act(user, I);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return null;
        obj_item_firing_pin pin_item = pin as obj_item_firing_pin;
        if (Dm.truthy(pin_item?.pin_removable) && Dm.truthy(user.is_holding(this)))
        {
            user.visible_message(span_warning(I18n.T("[] attempts to remove [] from [] with [].", user, pin_item, this, I)),
            span_notice(I18n.T("You attempt to remove [] from []. (It will take [].)", pin_item, this, DisplayTimeText(FIRING_PIN_REMOVAL_DELAY))), null, 3);
            if (Dm.truthy(await I.use_tool(this, user, FIRING_PIN_REMOVAL_DELAY, volume: 50)))
            {
                if (pin == null) //check to see if the pin is still there, or we can spam messages by clicking multiple times during the tool delay
                    return null;
                user.visible_message(span_notice(I18n.T("[] is pried out of [] by [], destroying the pin in the process.", pin_item, this, user)),
                                    span_warning(I18n.T("You pry [] out with [], destroying the pin in the process.", pin_item, I)), null, 3);
                QDEL_NULL(ref pin);
                return ITEM_INTERACT_SUCCESS;
            }
        }
        return null;
    }

    // tg: code/modules/projectiles/gun.dm:625
    public override async DmTask<object> welder_act(mob_living user = null, obj_item I = null)
    {
        object dot = await base.welder_act(user, I);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return null;
        obj_item_firing_pin pin_item = pin as obj_item_firing_pin;
        if (Dm.truthy(pin_item?.pin_removable) && Dm.truthy(user.is_holding(this)))
        {
            user.visible_message(span_warning(I18n.T("[] attempts to remove [] from [] with [].", user, pin_item, this, I)),
            span_notice(I18n.T("You attempt to remove [] from []. (It will take [].)", pin_item, this, DisplayTimeText(FIRING_PIN_REMOVAL_DELAY))), null, 3);
            if (Dm.truthy(await I.use_tool(this, user, FIRING_PIN_REMOVAL_DELAY, 5, volume: 50)))
            {
                if (pin == null) //check to see if the pin is still there, or we can spam messages by clicking multiple times during the tool delay
                    return null;
                user.visible_message(span_notice(I18n.T("[] is spliced out of [] by [], melting part of the pin in the process.", pin_item, this, user)),
                                    span_warning(I18n.T("You splice [] out of [] with [], melting part of the pin in the process.", pin_item, this, I)), null, 3);
                QDEL_NULL(ref pin);
                return TRUE;
            }
        }
        return null;
    }

    // tg: code/modules/projectiles/gun.dm:642
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item I = null)
    {
        object dot = await base.wirecutter_act(user, I);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return null;
        obj_item_firing_pin pin_item = pin as obj_item_firing_pin;
        if (Dm.truthy(pin_item?.pin_removable) && Dm.truthy(user.is_holding(this)))
        {
            user.visible_message(span_warning(I18n.T("[] attempts to remove [] from [] with [].", user, pin_item, this, I)),
            span_notice(I18n.T("You attempt to remove [] from []. (It will take [].)", pin_item, this, DisplayTimeText(FIRING_PIN_REMOVAL_DELAY))), null, 3);
            if (Dm.truthy(await I.use_tool(this, user, FIRING_PIN_REMOVAL_DELAY, volume: 50)))
            {
                if (pin == null) //check to see if the pin is still there, or we can spam messages by clicking multiple times during the tool delay
                    return null;
                user.visible_message(span_notice(I18n.T("[] is ripped out of [] by [], mangling the pin in the process.", pin_item, this, user)),
                                    span_warning(I18n.T("You rip [] out of [] with [], mangling the pin in the process.", pin_item, this, I)), null, 3);
                QDEL_NULL(ref pin);
                return TRUE;
            }
        }
        return null;
    }

    // tg: code/modules/projectiles/gun.dm:662
    public virtual async DmTask<object> handle_suicide(mob_living_carbon_human user = null, mob_living_carbon_human target = null, object @params = null, object bypass_timer = null)
    {
        Dm.NotTranslated("code/modules/projectiles/gun.dm:662", "handle_suicide: pointing a gun at a head or mouth, the 12 second do_after and the x5 shot (docs/ai/specs/not-translated.md section 47)");
        return default;
    }

    // tg: code/modules/projectiles/gun.dm:707
    public virtual async DmTask unlock() //used in summon guns and as a convience for admins
    {
        if (Dm.truthy(pin))
            qdel(pin);
        obj_item_firing_pin new_pin = Dm.@new<obj_item_firing_pin>();
        await new_pin.gun_insert(new_gun: this, starting: TRUE);
    }

    //Happens before the actual projectile creation
    // tg: code/modules/projectiles/gun.dm:714
    public virtual void before_firing(atom target = null, mob user = null)
    {
        return;
    }

    // tg: code/modules/projectiles/gun.dm:717
    [SignalHandler]
    public virtual object on_mail_unwrap(atom source = null, mob user = null, obj_item_mail_traitor letter = null)
    {
        Waiting.Detach(fire_at_opener(user, letter)); // INVOKE_ASYNC
        return COMPONENT_TRAITOR_MAIL_HANDLED;
    }

    // tg: code/modules/projectiles/gun.dm:722
    public virtual async DmTask fire_at_opener(mob user = null, obj_item_mail_traitor letter = null)
    {
        if (!Dm.truthy(await user.put_in_hands(this))) //this won't ever fail under normal circumstances, but will happen with the admin versions
            await forceMove(user.loc);
        to_chat(user, span_danger(I18n.T("As you open [], you see [] inside! []", letter, this, about_to_shoot_inside_mail_text)));
        if (!Dm.truthy(can_shoot()))
        {
            await shoot_with_empty_chamber(user as mob_living);
            return;
        }
        if (Dm.truthy(await process_fire(user, user as mob_living, FALSE, zone_override: BODY_ZONE_HEAD)))
        {
            await forceMove(user.loc);
            await throw_at(get_step(user, user.dir) as atom, 1, 3); // tg: pick() of the one step
        }
    }
}
