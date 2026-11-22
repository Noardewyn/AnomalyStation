// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/robot/items/generic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the electrically-charged arm (:19-60, gaps package P6, D298: the arm electrification implant's and the combat
// implant's stun arm); silicons SI2 (D326) added the hugger, the power connector, the harm alarm and the shield module. The var
// blocks are generated (D111).

// tg: code/game/objects/items/robot/items/generic.dm:19
public partial class obj_item_borg_stun : obj_item_borg
{
    /// Cost to use the stun arm
    // tg: code/game/objects/items/robot/items/generic.dm:17  #define CYBORG_STUN_CHARGE_COST (0.2 * STANDARD_CELL_CHARGE)
    private const float CYBORG_STUN_CHARGE_COST = 0.2f * STANDARD_CELL_CHARGE;

    // tg: code/game/objects/items/robot/items/generic.dm:27
    public override async DmTask<object> attack__proc(mob_living target = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob_living attacked_mob = target;
        if (cooldown_check > world.time)
        {
            user.balloon_alert(user, I18n.T("still recharging!"));
            return null;
        }
        if (ishuman(attacked_mob))
        {
            mob_living_carbon_human human = (mob_living_carbon_human)attacked_mob;
            if (Dm.truthy(await human.check_block(this, 0, I18n.T("[]'s []", attacked_mob, name), MELEE_ATTACK)))
            {
                playsound(attacked_mob, Dm.resource("sound/items/weapons/genhit.ogg"), 50, TRUE);
                return FALSE;
            }
        }
        if (iscyborg(user))
        {
            mob_living_silicon_robot robot_user = user as mob_living_silicon_robot;
            if (!Dm.truthy(await (robot_user.cell as obj_item_stock_parts_power_store).use(CYBORG_STUN_CHARGE_COST)))
                return null;
        }

        await user.do_attack_animation(attacked_mob);
        await attacked_mob.adjust_stamina_loss(stamina_damage);
        await attacked_mob.set_confusion_if_lower(5 * SECONDS);
        await attacked_mob.adjust_stutter(20 * SECONDS);
        await attacked_mob.set_jitter_if_lower(5 * SECONDS);
        if (issilicon(attacked_mob))
        {
            await attacked_mob.emp_act(EMP_HEAVY);
            attacked_mob.visible_message(
                span_danger(I18n.T("[] shocks [] with []!", user, attacked_mob, this)),
                span_userdanger(I18n.T("[] shocks you with []!", user, this)));
        }
        else
        {
            attacked_mob.visible_message(
                span_danger(I18n.T("[] prods [] with []!", user, attacked_mob, this)),
                span_userdanger(I18n.T("[] prods you with []!", user, this)));
        }

        playsound(loc, Dm.resource("sound/items/weapons/egloves.ogg"), 50, TRUE, -1);
        cooldown_check = world.time + cooldown;
        log_combat(user, attacked_mob, "stunned", this, Dm.text("(Combat mode: [])", Dm.truthy(user.combat_mode) ? "On" : "Off"));
        return null;
    }
}

// tg: code/game/objects/items/robot/items/generic.dm:64
public partial class obj_item_borg_cyborghug : obj_item_borg
{
    private const int HUG_MODE_NICE = 0;
    private const int HUG_MODE_HUG = 1;
    private const int HUG_MODE_SHOCK = 2;
    private const int HUG_MODE_CRUSH = 3;

    private const float HUG_SHOCK_COOLDOWN = 2 * SECONDS;
    private const float HUG_CRUSH_COOLDOWN = 1 * SECONDS;

    // tg: code/game/objects/items/robot/items/generic.dm:78
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (iscyborg(user))
        {
            mob_living_silicon_robot robot_user = user as mob_living_silicon_robot;
            if (Dm.truthy(robot_user.emagged) && Dm.eq(shockallowed, 1))
            {
                if (mode < HUG_MODE_CRUSH)
                    mode++;
                else
                    mode = HUG_MODE_NICE;
            }
            else if (mode < HUG_MODE_HUG)
                mode++;
            else
                mode = HUG_MODE_NICE;
        }
        switch ((int)mode)
        {
            case HUG_MODE_NICE:
                to_chat(user, span_infoplain(I18n.T("Power reset. Hugs!")));
                break;
            case HUG_MODE_HUG:
                to_chat(user, span_infoplain(I18n.T("Power increased!")));
                break;
            case HUG_MODE_SHOCK:
                to_chat(user, Dm.text("<span class='warningplain'>[]</span>", I18n.T("BZZT. Electrifying arms...")));
                break;
            case HUG_MODE_CRUSH:
                to_chat(user, Dm.text("<span class='warningplain'>[]</span>", I18n.T("ERROR: ARM ACTUATORS OVERLOADED.")));
                break;
        }
        return null;
    }

    // tg: code/game/objects/items/robot/items/generic.dm:100
    public override async DmTask<object> attack__proc(mob_living attacked_mob = null, mob_living user__living = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob_living_silicon_robot user = user__living as mob_living_silicon_robot;
        if (attacked_mob == user__living)
            return null;
        if (Dm.num(attacked_mob.health) < 0)
            return null;
        switch ((int)mode)
        {
            case HUG_MODE_NICE:
                if (isanimal_or_basicmob(attacked_mob))
                {
                    if (!Dm.truthy(user__living.combat_mode) && !Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
                        await attacked_mob.attack_hand(user__living, modifiers); //This enables borgs to get the floating heart icon and mob emote from simple_animal's that have petbonus == true.
                    return null;
                }
                if (Dm.eq(user__living.zone_selected, BODY_ZONE_HEAD))
                {
                    user__living.visible_message(
                        span_notice(I18n.T("[] playfully boops [] on the head!", user__living, attacked_mob)),
                        span_notice(I18n.T("You playfully boop [] on the head!", attacked_mob))
                    );
                    await user__living.do_attack_animation(attacked_mob, ATTACK_EFFECT_BOOP);
                    playsound(loc, Dm.resource("sound/items/weapons/tap.ogg"), 50, TRUE, -1);
                }
                else if (ishuman(attacked_mob))
                {
                    if (Dm.eq(user__living.body_position, LYING_DOWN))
                    {
                        user__living.visible_message(
                            span_notice(I18n.T("[] shakes [] trying to get [] up!", user__living, attacked_mob, attacked_mob.p_them())),
                            span_notice(I18n.T("You shake [] trying to get [] up!", attacked_mob, attacked_mob.p_them()))
                        );
                    }
                    else
                    {
                        user__living.visible_message(
                            span_notice(I18n.T("[] hugs [] to make [] feel better!", user__living, attacked_mob, attacked_mob.p_them())),
                            span_notice(I18n.T("You hug [] to make [] feel better!", attacked_mob, attacked_mob.p_them()))
                        );
                    }
                    if (Dm.truthy(attacked_mob.resting))
                        await attacked_mob.set_resting(FALSE, TRUE);
                }
                else
                {
                    user__living.visible_message(
                        span_notice(I18n.T("[] pets []!", user__living, attacked_mob)),
                        span_notice(I18n.T("You pet []!", attacked_mob))
                    );
                }
                playsound(loc, Dm.resource("sound/items/weapons/thudswoosh.ogg"), 50, TRUE, -1);
                break;
            case HUG_MODE_HUG:
                if (ishuman(attacked_mob))
                {
                    await attacked_mob.adjust_status_effects_on_shake_up();
                    if (Dm.eq(attacked_mob.body_position, LYING_DOWN))
                    {
                        user__living.visible_message(
                            span_notice(I18n.T("[] shakes [] trying to get [] up!", user__living, attacked_mob, attacked_mob.p_them())),
                            span_notice(I18n.T("You shake [] trying to get [] up!", attacked_mob, attacked_mob.p_them()))
                        );
                    }
                    else if (Dm.eq(user__living.zone_selected, BODY_ZONE_HEAD))
                    {
                        user__living.visible_message(span_warning(I18n.T("[] bops [] on the head!", user__living, attacked_mob)),
                            span_warning(I18n.T("You bop [] on the head!", attacked_mob))
                        );
                        await user__living.do_attack_animation(attacked_mob, ATTACK_EFFECT_PUNCH);
                    }
                    else
                    {
                        if (!Dm.truthy(Dm.bitand(!Dm.truthy(attacked_mob._listen_lookup?[COMSIG_BORG_HUG_MOB]) ? NONE : attacked_mob._SendSignal(COMSIG_BORG_HUG_MOB, Dm.list(attacked_mob, user__living)), COMSIG_BORG_HUG_HANDLED)))
                        {
                            user__living.visible_message(
                                span_warning(I18n.T("[] hugs [] in a firm bear-hug! [] looks uncomfortable...", user__living, attacked_mob, attacked_mob)),
                                span_warning(I18n.T("You hug [] firmly to make [] feel better! [] looks uncomfortable...", attacked_mob, attacked_mob.p_them(), attacked_mob))
                            );
                        }
                    }
                    if (Dm.truthy(attacked_mob.resting))
                        await attacked_mob.set_resting(FALSE, TRUE);
                }
                else
                {
                    user__living.visible_message(
                        span_warning(I18n.T("[] bops [] on the head!", user__living, attacked_mob)),
                        span_warning(I18n.T("You bop [] on the head!", attacked_mob))
                    );
                }
                playsound(loc, Dm.resource("sound/items/weapons/tap.ogg"), 50, TRUE, -1);
                break;
            case HUG_MODE_SHOCK:
                if (!(shock_cooldown <= world.time)) // COOLDOWN_FINISHED, inline
                    return null;
                if (ishuman(attacked_mob))
                {
                    await attacked_mob.electrocute_act(5, Dm.text("[]", user__living), flags: Dm.bitor(SHOCK_NOGLOVES, SHOCK_NOSTUN));
                    await attacked_mob.dropItemToGround(attacked_mob.get_active_held_item() as obj_item);
                    await attacked_mob.dropItemToGround(attacked_mob.get_inactive_held_item() as obj_item);
                    user__living.visible_message(
                        span_userdanger(I18n.T("[] electrocutes [] with [] touch!", user__living, attacked_mob, user__living.p_their())),
                        span_danger(I18n.T("You electrocute [] with your touch!", attacked_mob))
                    );
                }
                else
                {
                    if (!iscyborg(attacked_mob))
                    {
                        await attacked_mob.adjust_fire_loss(10);
                        user__living.visible_message(
                            span_userdanger(I18n.T("[] shocks []!", user__living, attacked_mob)),
                            span_danger(I18n.T("You shock []!", attacked_mob))
                        );
                    }
                    else
                    {
                        user__living.visible_message(
                            span_userdanger(I18n.T("[] shocks []. It does not seem to have an effect", user__living, attacked_mob)),
                            span_danger(I18n.T("You shock [] to no effect.", attacked_mob))
                        );
                    }
                }
                playsound(loc, Dm.resource("sound/effects/sparks/sparks2.ogg"), 50, TRUE, -1);
                await (user.cell as obj_item_stock_parts_power_store).use(0.5f * STANDARD_CELL_CHARGE, Dm.named("force", TRUE));
                shock_cooldown = world.time + HUG_SHOCK_COOLDOWN; // COOLDOWN_START, inline
                break;
            case HUG_MODE_CRUSH:
                if (!(crush_cooldown <= world.time)) // COOLDOWN_FINISHED, inline
                    return null;
                if (ishuman(attacked_mob))
                {
                    user__living.visible_message(
                        span_userdanger(I18n.T("[] crushes [] in [] grip!", user__living, attacked_mob, user__living.p_their())),
                        span_danger(I18n.T("You crush [] in your grip!", attacked_mob))
                    );
                }
                else
                {
                    user__living.visible_message(
                        span_userdanger(I18n.T("[] crushes []!", user__living, attacked_mob)),
                        span_danger(I18n.T("You crush []!", attacked_mob))
                    );
                }
                playsound(loc, Dm.resource("sound/items/weapons/smash.ogg"), 50, TRUE, -1);
                await attacked_mob.adjust_brute_loss(15);
                await (user.cell as obj_item_stock_parts_power_store).use(0.3f * STANDARD_CELL_CHARGE, Dm.named("force", TRUE));
                crush_cooldown = world.time + HUG_CRUSH_COOLDOWN; // COOLDOWN_START, inline
                break;
        }
        return null;
    }
}

// tg: code/game/objects/items/robot/items/generic.dm:214
public partial class obj_item_borg_charger : obj_item_borg
{
    /// Whitelist of charging machines
    private static ref DmList charge_machines => ref TgState.Current.obj_item_borg_charger__charge_machines;
    /// Whitelist of chargable items
    private static ref DmList charge_items => ref TgState.Current.obj_item_borg_charger__charge_items;

    // tg: code/game/objects/items/robot/items/generic.dm:214  (the type's static lists, rules 7.6)
    private static void init_static_lists()
    {
        charge_machines ??= typecacheof(Dm.list(typeof(obj_machinery_cell_charger), typeof(obj_machinery_recharger), typeof(obj_machinery_recharge_station), typeof(obj_machinery_mech_bay_recharge_port))) as DmList;
        charge_items ??= typecacheof(Dm.list(typeof(obj_item_stock_parts_power_store), typeof(obj_item_gun_energy))) as DmList;
    }

    // tg: code/game/objects/items/robot/items/generic.dm:225
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("charger_[]", mode);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/robot/items/generic.dm:229
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.eq(mode, "draw"))
            mode = "charge";
        else
            mode = "draw";
        to_chat(user, span_notice(I18n.T("You toggle [] to \"[]\" mode.", this, mode)));
        update_appearance();
        return null;
    }

    // tg: code/game/objects/items/robot/items/generic.dm:237
    public override async DmTask<object> interact_with_atom(atom target = null, mob_living user__living = null, DmList modifiers = null)
    {
        init_static_lists();
        if (!iscyborg(user__living))
            return NONE;
        mob_living_silicon_robot user = user__living as mob_living_silicon_robot;

        object dot = ITEM_INTERACT_BLOCKING;
        if (Dm.eq(mode, "draw"))
        {
            if (Dm.truthy(is_type_in_list(target, charge_machines)))
            {
                obj_machinery target_machine = target as obj_machinery;
                if (Dm.truthy(Dm.bitand(target_machine.machine_stat, Dm.bitor(NOPOWER, BROKEN))) || !Dm.truthy(target_machine.anchored))
                {
                    to_chat(user, span_warning(I18n.T("[] is unpowered!", target_machine)));
                    return dot;
                }

                to_chat(user, span_notice(I18n.T("You connect to []'s power line...", target_machine)));
                while (Dm.truthy(await do_after(user, 1.5f * SECONDS, target: target_machine, show_progress: FALSE)))
                {
                    if (user == null || user.cell == null || !Dm.eq(mode, "draw"))
                        return dot;

                    if (Dm.truthy(Dm.bitand(target_machine.machine_stat, Dm.bitor(NOPOWER, BROKEN))) || !Dm.truthy(target_machine.anchored))
                        break;

                    await target_machine.charge_cell__proc(0.15f * STANDARD_CELL_CHARGE, user.cell as obj_item_stock_parts_power_store);
                }

                to_chat(user, span_notice(I18n.T("You stop charging yourself.")));
            }

            else if (Dm.truthy(is_type_in_list(target, charge_items)))
            {
                obj_item_stock_parts_power_store cell = target as obj_item_stock_parts_power_store;
                if (cell is not obj_item_stock_parts_power_store)
                    cell = Dm.locate<obj_item_stock_parts_power_store>(target);
                if (cell == null)
                {
                    to_chat(user, span_warning(I18n.T("[] has no power cell!", target)));
                    return dot;
                }

                if (target is obj_item_gun_energy energy_gun)
                {
                    if (!Dm.truthy(energy_gun.can_charge))
                    {
                        to_chat(user, span_warning(I18n.T("[] has no power port!", target)));
                        return dot;
                    }
                }

                if (!Dm.truthy(cell.charge))
                    to_chat(user, span_warning(I18n.T("[] has no power!", target)));


                to_chat(user, span_notice(I18n.T("You connect to []'s power port...", target)));

                while (Dm.truthy(await do_after(user, 1.5f * SECONDS, target: target, show_progress: FALSE)))
                {
                    if (user == null || user.cell == null || !Dm.eq(mode, "draw"))
                        return dot;

                    if (cell == null || target == null)
                        return dot;

                    if (cell != target && cell.loc != target)
                        return dot;

                    obj_item_stock_parts_power_store user_cell = user.cell as obj_item_stock_parts_power_store;
                    float draw = min(Dm.num(cell.charge), Dm.num(cell.chargerate) * 0.5f, Dm.num(user_cell.maxcharge) - Dm.num(user_cell.charge));
                    if (!Dm.truthy(await cell.use(draw)))
                        break;
                    if (!Dm.truthy(await user_cell.give(draw)))
                        break;
                    target.update_appearance();
                }

                to_chat(user, span_notice(I18n.T("You stop charging yourself.")));
            }
        }

        else if (Dm.truthy(is_type_in_list(target, charge_items)))
        {
            obj_item_stock_parts_power_store cell = target as obj_item_stock_parts_power_store;
            if (cell is not obj_item_stock_parts_power_store)
                cell = Dm.locate<obj_item_stock_parts_power_store>(target);
            if (cell == null)
            {
                to_chat(user, span_warning(I18n.T("[] has no power cell!", target)));
                return dot;
            }

            if (target is obj_item_gun_energy energy_gun)
            {
                if (!Dm.truthy(energy_gun.can_charge))
                {
                    to_chat(user, span_warning(I18n.T("[] has no power port!", target)));
                    return dot;
                }
            }

            if (Dm.num(cell.charge) >= Dm.num(cell.maxcharge))
                to_chat(user, span_warning(I18n.T("[] is already charged!", target)));

            to_chat(user, span_notice(I18n.T("You connect to []'s power port...", target)));

            while (Dm.truthy(await do_after(user, 1.5f * SECONDS, target: target, show_progress: FALSE)))
            {
                if (user == null || user.cell == null || !Dm.eq(mode, "charge"))
                    return dot;

                if (cell == null || target == null)
                    return dot;

                if (cell != target && cell.loc != target)
                    return dot;

                obj_item_stock_parts_power_store user_cell = user.cell as obj_item_stock_parts_power_store;
                float draw = min(Dm.num(user_cell.charge), Dm.num(cell.chargerate) * 0.5f, Dm.num(cell.maxcharge) - Dm.num(cell.charge));
                if (!Dm.truthy(await user_cell.use(draw)))
                    break;
                if (!Dm.truthy(await cell.give(draw)))
                    break;
                target.update_appearance();
            }

            to_chat(user, span_notice(I18n.T("You stop charging [].", target)));
        }
        return dot;
    }
}

// tg: code/game/objects/items/robot/items/generic.dm:338
public partial class obj_item_harmalarm : obj_item
{
    private const float HARM_ALARM_NO_SAFETY_COOLDOWN = 60 * SECONDS;
    private const float HARM_ALARM_SAFETY_COOLDOWN = 20 * SECONDS;

    // tg: code/game/objects/items/robot/items/generic.dm:346
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        obj_flags = Dm.bitxor(obj_flags, EMAGGED);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            balloon_alert(user, I18n.T("safeties shorted"));
        else
            balloon_alert(user, I18n.T("safeties reset"));
        return TRUE;
    }

    // tg: code/game/objects/items/robot/items/generic.dm:354
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        bool safety = !Dm.truthy(Dm.bitand(obj_flags, EMAGGED));
        if (!(alarm_cooldown <= world.time)) // COOLDOWN_FINISHED, inline
        {
            to_chat(user, Dm.text("<font color='red'>[]</font>", I18n.T("The device is still recharging!")));
            return null;
        }

        if (iscyborg(user))
        {
            mob_living_silicon_robot robot_user = user as mob_living_silicon_robot;
            obj_item_stock_parts_power_store robot_cell = robot_user.cell as obj_item_stock_parts_power_store;
            if (robot_cell == null || Dm.num(robot_cell.charge) < 1200)
            {
                to_chat(user, span_warning(I18n.T("You don't have enough charge to do this!")));
                return null;
            }
            robot_cell.charge = Dm.num(robot_cell.charge) - 1000;
            if (Dm.truthy(robot_user.emagged))
                safety = false;
        }

        if (safety)
        {
            user.visible_message(
                Dm.text("<font color='red' size='2'>[]</font>", I18n.T("[] blares out a near-deafening siren from its speakers!", user)),
                span_userdanger(iscyborg(user) ? I18n.T("Your siren blares around you!") : I18n.T("Your siren blares around and confuses you!")),
                span_danger(I18n.T("The siren pierces your hearing!"))
            );
            foreach (mob_living_carbon carbon in Dm.each<mob_living_carbon>(get_hearers_in_view(9, user)))
            {
                if (Dm.num(carbon.get_ear_protection()) > 0)
                    continue;
                await carbon.adjust_confusion(6 * SECONDS);
            }

            audible_message(Dm.text("<font color='red' size='7'>[]</font>", I18n.T("HUMAN HARM")));
            playsound(get_turf(this), Dm.resource("sound/mobs/non-humanoids/cyborg/harmalarm.ogg"), 70, 3);
            alarm_cooldown = world.time + HARM_ALARM_SAFETY_COOLDOWN; // COOLDOWN_START, inline
            user.log_message("used a Cyborg Harm Alarm", LOG_ATTACK);
            if (iscyborg(user))
            {
                mob_living_silicon_robot robot_user = user as mob_living_silicon_robot;
                to_chat(robot_user.connected_ai, Dm.text("<br>[]<br>", span_notice(I18n.T("NOTICE - Peacekeeping 'HARM ALARM' used by: []", user))));
            }
        }
        else
        {
            user.audible_message(Dm.text("<font color='red' size='7'>[]</font>", I18n.T("BZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZT")));
            foreach (mob_living living in Dm.each<mob_living>(get_hearers_in_view(9, user)))
            {
                object bang_effect = await living.soundbang_act(SOUNDBANG_STRONG, 0, 0, 5);
                switch ((int)Dm.num(bang_effect))
                {
                    case 0:
                        continue;
                    case 1:
                        await living.adjust_confusion(5 * SECONDS);
                        await living.adjust_stutter(20 * SECONDS);
                        await living.adjust_jitter(20 * SECONDS);
                        break;
                    default:
                        await living.Paralyze(4 * SECONDS);
                        await living.adjust_confusion(10 * SECONDS);
                        await living.adjust_stutter(30 * SECONDS);
                        await living.adjust_jitter(50 * SECONDS);
                        break;
                }
            }
            playsound(get_turf(this), Dm.resource("sound/machines/warning-buzzer.ogg"), 130, 3);
            alarm_cooldown = world.time + HARM_ALARM_NO_SAFETY_COOLDOWN; // COOLDOWN_START, inline
            user.log_message("used an emagged Cyborg Harm Alarm", LOG_ATTACK);
        }
        return null;
    }
}

// tg: code/game/objects/items/robot/items/generic.dm:407
public partial class obj_item_shield_module : obj_item
{
    // tg: code/game/objects/items/robot/items/generic.dm:414
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        shield_overlay = mutable_appearance(Dm.resource("icons/mob/effects/durand_shield.dmi"), "borg_shield") as mutable_appearance;
        return dot;
    }

    // tg: code/game/objects/items/robot/items/generic.dm:418
    public override async DmTask<object> attack_self(mob borg = null, object modifiers = null, params object[] __extra)
    {
        active = Dm.not(active);
        if (Dm.truthy(active))
        {
            playsound(this, Dm.resource("sound/vehicles/mecha/mech_shield_raise.ogg"), 50, FALSE);
            RegisterSignal(borg, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_shield_overlay_update), @override: TRUE);
        }
        else
        {
            playsound(this, Dm.resource("sound/vehicles/mecha/mech_shield_drop.ogg"), 50, FALSE);
            UnregisterSignal(borg, COMSIG_ATOM_UPDATE_OVERLAYS);
        }
        borg.update_appearance();
        return null;
    }

    // tg: code/game/objects/items/robot/items/generic.dm:428
    public override async DmTask cyborg_unequip(mob borg = null)
    {
        active = FALSE;
        playsound(this, Dm.resource("sound/vehicles/mecha/mech_shield_drop.ogg"), 50, FALSE);
        borg.cut_overlay(shield_overlay);
    }

    // tg: code/game/objects/items/robot/items/generic.dm:433
    [SignalHandler]
    public virtual void on_shield_overlay_update(atom source = null, DmList overlays = null)
    {
        if (Dm.truthy(active))
            Dm.append(ref overlays, shield_overlay);
    }
}

// tg: code/game/objects/items/robot/items/generic.dm:221  (the charger's static lists, rules 7.6)
public sealed partial class TgState
{
    internal DmList obj_item_borg_charger__charge_machines;
    internal DmList obj_item_borg_charger__charge_items;
}
