// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/weaponry/melee/powerfist.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G2 (D316): the power fist whole: a gas tank behind a screwdriver, three valve strengths on the
// wrench, the punch that burns the tank's gas and flings the target. The var block is hand-written: the generator types
// `tank` by its name as a Type (tank holders hold a typepath there), here it holds the tank itself.

// tg: code/game/objects/items/weaponry/melee/powerfist.dm:9
public partial class obj_item_melee_powerfist : obj_item_melee
{
    ///Defines for the pressure strength of the fist
    // tg: code/game/objects/items/weaponry/melee/powerfist.dm:2
    private const float LOW_PRESSURE = 1;
    private const float MID_PRESSURE = 2;
    private const float HIGH_PRESSURE = 3;
    ///Defines for the tank change action
    // tg: code/game/objects/items/weaponry/melee/powerfist.dm:6
    private const float TANK_INSERTING = 0;
    private const float TANK_REMOVING = 1;

    /// Tank used for the gauntlet's piston-ram.
    // tg: code/game/objects/items/weaponry/melee/powerfist.dm:34
    public obj_item_tank_internals tank;

    // tg: code/game/objects/items/weaponry/melee/powerfist.dm:10
    public obj_item_melee_powerfist()
    {
        name = "power-fist";
        desc = "A metal gauntlet with a piston-powered ram on top for that extra 'oomph' in your punch.";
        icon = Dm.resource("icons/obj/antags/syndicate_tools.dmi");
        icon_state = "powerfist";
        inhand_icon_state = "powerfist";
        icon_angle = 180;
        lefthand_file = Dm.resource("icons/mob/inhands/weapons/melee_lefthand.dmi");
        righthand_file = Dm.resource("icons/mob/inhands/weapons/melee_righthand.dmi");
        obj_flags = CONDUCTS_ELECTRICITY;
        force = 20;
        throwforce = 10;
        throw_range = 7;
        w_class = WEIGHT_CLASS_NORMAL;
        armor_type = typeof(datum_armor_melee_powerfist);
        resistance_flags = FIRE_PROOF;
    }

    // tg: code/game/objects/items/weaponry/melee/powerfist.dm:19
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        attack_verb_continuous = Dm.list("whacks", "fists", "power-punches");
        attack_verb_simple = Dm.list("whack", "fist", "power-punch");
    }

    // tg: code/game/objects/items/weaponry/melee/powerfist.dm:40
    public virtual object pressure_setting_to_text(object fist_pressure_setting = null)
    {
        object sw__1 = fist_pressure_setting;
        if (Dm.eq(sw__1, LOW_PRESSURE))
            return "low";
        else if (Dm.eq(sw__1, MID_PRESSURE))
            return "medium";
        else if (Dm.eq(sw__1, HIGH_PRESSURE))
            return "high";
        else
            throw Dm.CRASH(Dm.text("Invalid pressure setting: []!", fist_pressure_setting), "code/game/objects/items/weaponry/melee/powerfist.dm", 49);
    }

    // tg: code/game/objects/items/weaponry/melee/powerfist.dm:51
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!in_range(user, this))
        {
            dot.Add(span_notice(I18n.T("You'll need to get closer to see any more.")));
            return dot;
        }
        if (tank != null)
        {
            // [icon2html(tank, user)]: the tank's picture in the chat line (chat icons, not-translated section 76)
            dot.Add(span_notice(I18n.T("It has \\a [] mounted onto it.", tank)));
            dot.Add(span_notice(I18n.T("Can be removed with a <b>screwdriver</b>.")));
        }

        dot.Add(span_notice(I18n.T("Use a <b>wrench</b> to change the valve strength. Current strength is at <b>[]</b> level.", pressure_setting_to_text(fist_pressure_setting))));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/powerfist.dm:62
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        fist_pressure_setting = fist_pressure_setting >= HIGH_PRESSURE ? LOW_PRESSURE : fist_pressure_setting + 1;
        tool.play_tool_sound(this);
        balloon_alert(user, I18n.T("piston strength set to []", pressure_setting_to_text(fist_pressure_setting)));
        return TRUE;
    }

    // tg: code/game/objects/items/weaponry/melee/powerfist.dm:68
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (tank == null)
        {
            balloon_alert(user, I18n.T("no tank present"));
            return null;
        }
        await update_tank(tank, TANK_REMOVING, user as mob_living_carbon_human);
        return TRUE;
    }

    // tg: code/game/objects/items/weaponry/melee/powerfist.dm:75
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_tank_internals)))
            return NONE;

        if (tank != null)
        {
            to_chat(user, span_notice(I18n.T("A tank is already present, remove it with a screwdriver first.")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.num(((obj_item_tank_internals)tool).volume) <= 3)
        {
            to_chat(user, span_warning(I18n.T("\\The [] is too small for \\the [].", tool, this)));
            return ITEM_INTERACT_BLOCKING;
        }

        await update_tank((obj_item_tank_internals)tool, TANK_INSERTING, user as mob_living_carbon_human);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/weaponry/melee/powerfist.dm:90
    public virtual async DmTask update_tank(obj_item_tank_internals the_tank = null, float? removing = null, mob_living_carbon_human user = null)
    {
        removing ??= TANK_INSERTING;
        if (Dm.truthy(removing))
        {
            if (tank == null)
            {
                to_chat(user, span_notice(I18n.T("\\The [] currently has no tank attached to it.", this)));
                return;
            }
            to_chat(user, span_notice(I18n.T("You detach \\the [] from \\the [].", the_tank, this)));
            await tank.forceMove(get_turf(user));
            await user.put_in_hands(tank);
            tank = null;
            return;
        }

        if (tank != null)
        {
            to_chat(user, span_warning(I18n.T("\\The [] already has a tank.", this)));
            return;
        }
        if (!Dm.truthy(await user.transferItemToLoc(the_tank, this)))
            return;
        to_chat(user, span_notice(I18n.T("You hook \\the [] up to \\the [].", the_tank, this)));
        tank = the_tank;
    }

    // tg: code/game/objects/items/weaponry/melee/powerfist.dm:109
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob_living target = target_mob;
        if (tank == null)
        {
            to_chat(user, span_warning(I18n.T("\\The [] can't operate without a source of gas!", this)));
            return null;
        }
        if (HAS_TRAIT(user, TRAIT_PACIFISM))
        {
            to_chat(user, span_warning(I18n.T("You don't want to harm other living beings!")));
            return null;
        }
        turf our_turf = get_turf(this) as turf;
        if (our_turf == null)
            return null;

        datum_gas_mixture gas_used = tank.remove_air(gas_per_fist * fist_pressure_setting) as datum_gas_mixture;
        if (gas_used == null)
        {
            to_chat(user, span_warning(I18n.T("\\The []'s tank is empty!", this)));
            await target.apply_damage(force / 5, BRUTE);
            playsound(loc, Dm.resource("sound/items/weapons/punch1.ogg"), 50, TRUE);
            target.visible_message(span_danger(I18n.T("[]'s powerfist lets out a dull thunk as [] punch[] []!", user, user.p_they(), user.p_es(), target.name)),
                span_userdanger(I18n.T("[]'s punches you!", user)));
            return null;
        }

        if (!Dm.truthy(molar_cmp_equals(gas_used.total_moles(), gas_per_fist * fist_pressure_setting)))
        {
            await our_turf.assume_air(gas_used);
            to_chat(user, span_warning(I18n.T("\\The []'s piston-ram lets out a weak hiss, it needs more gas!", this)));
            playsound(loc, Dm.resource("sound/items/weapons/punch4.ogg"), 50, TRUE);
            await target.apply_damage(force / 2, BRUTE);
            target.visible_message(span_danger(I18n.T("[]'s powerfist lets out a weak hiss as [] punch[] []!", user, user.p_they(), user.p_es(), target.name)),
                span_userdanger(I18n.T("[]'s punch strikes with force!", user)));
            return null;
        }

        target.visible_message(span_danger(I18n.T("[]'s powerfist lets out a loud hiss as [] punch[] []!", user, user.p_they(), user.p_es(), target.name)),
            span_userdanger(I18n.T("You cry out in pain as []'s punch flings you backwards!", user)));
        Dm.@new<obj_effect_temp_visual_kinetic_blast>(target.loc);
        await target.apply_damage(force * fist_pressure_setting, BRUTE, wound_bonus: CANT_WOUND);
        playsound(this, Dm.resource("sound/items/weapons/resonator_blast.ogg"), 50, TRUE);
        playsound(this, Dm.resource("sound/items/weapons/genhit2.ogg"), 50, TRUE);

        if (!QDELETED(target))
        {
            atom throw_target = get_edge_target_turf(target, get_dir(this, get_step_away(target, this))) as atom;

            await target.throw_at(throw_target, 5 * fist_pressure_setting, 0.5f + fist_pressure_setting / 2);
        }

        log_combat(user, target, "power fisted", this);

        user.changeNext_move(CLICK_CD_MELEE * click_delay);

        await our_turf.assume_air(gas_used);
        return null;
    }
}
