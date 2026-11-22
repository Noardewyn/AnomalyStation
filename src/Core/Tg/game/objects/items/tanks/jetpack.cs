// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tanks/jetpack.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M4c slice 6: the jetpack; Meta's EVA storage holds /obj/item/tank/jetpack/carbondioxide) but one proc
// left for its milestone in docs/ai/specs/live-partial.txt: suicide_act (the suicide verb, M10); emp_act (M7 EMP) and remove_emp are
// translated. tg plays no sound of its own here or in the component: the jetpack's buttons are its item
// actions (set_internals plays the tank's internals_on/off, tanks.dm:93, :100). The improvised, void, harness, captain's,
// security and carbon dioxide jetpacks are var-only but the improvised one's allow_thrust. The var blocks are generated
// (D111). One port proc of the locomotion bridge's preview, preview_thrust, follows (see its comment).

// tg: code/game/objects/items/tanks/jetpack.dm:1
public partial class obj_item_tank_jetpack : obj_item_tank
{
    // tg: code/game/objects/items/tanks/jetpack.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob), ITEM_SLOT_SUITSTORE));
        thrust_callback = Dm.@new<datum_callback>(this, nameof(allow_thrust), 0.01f);
        Waiting.NoSleep(configure_jetpack(stabilize));
        return dot;
    }

    // tg: code/game/objects/items/tanks/jetpack.dm:32
    public override object Destroy(float? force = null)
    {
        thrust_callback = null;
        return base.Destroy(force);
    }

    /**
     * configures/re-configures the jetpack component
     *
     * Arguments
     * stabilize - Should this jetpack be stabalized
     */
    // tg: code/game/objects/items/tanks/jetpack.dm:42
    public virtual async DmTask configure_jetpack(object stabilize = null, mob user = null)
    {
        this.stabilize = Dm.num(stabilize);

        await AddComponent(
            typeof(datum_component_jetpack),
            this.stabilize,
            drift_force,
            COMSIG_JETPACK_ACTIVATED,
            COMSIG_JETPACK_DEACTIVATED,
            JETPACK_ACTIVATION_FAILED,
            thrust_callback,
            thrust_callback,
            typeof(datum_effect_system_trail_follow_ion));

        if (user != null && user.get_item_by_slot(slot_flags) == this)
        {
            if (!Dm.truthy(stabilize))
                ADD_TRAIT(user, TRAIT_NOGRAV_ALWAYS_DRIFT, JETPACK_TRAIT);
            else
                REMOVE_TRAIT(user, TRAIT_NOGRAV_ALWAYS_DRIFT, JETPACK_TRAIT);
        }
    }

    // tg: code/game/objects/items/tanks/jetpack.dm:63
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(on) && !Dm.truthy(Dm.bitand(slot, slot_flags)))
            turn_off(user);
    }

    // tg: code/game/objects/items/tanks/jetpack.dm:68
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.dropped(user, silent, __extra);
        if (Dm.truthy(on))
            turn_off(user);
        return dot;
    }

    // tg: code/game/objects/items/tanks/jetpack.dm:73
    public override void populate_gas()
    {
        if (Dm.truthy(gas_type))
        {
            datum_gas_mixture our_mix = Waiting.NoSleep(return_air()) as datum_gas_mixture;
            our_mix.set_gas(gas_type, (6 * ONE_ATMOSPHERE) * Dm.num(volume) / (R_IDEAL_GAS_EQUATION * T20C));
        }
    }

    // tg: code/game/objects/items/tanks/jetpack.dm:78
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        if (istype(actiontype, typeof(datum_action_item_action_toggle_jetpack)))
        {
            await cycle(user);
        }
        else if (istype(actiontype, typeof(datum_action_item_action_jetpack_stabilization)))
        {
            if (Dm.truthy(on))
            {
                await configure_jetpack(Dm.truthy(stabilize) ? FALSE : TRUE, user);
                to_chat(user, span_notice(I18n.T("You turn the jetpack stabilization [].", Dm.truthy(stabilize) ? I18n.T("on") : I18n.T("off"))));
            }
        }
        else
        {
            await toggle_internals(user as mob_living_carbon);
        }
        return null;
    }

    // tg: code/game/objects/items/tanks/jetpack.dm:88
    public virtual async DmTask cycle(mob user = null)
    {
        if (Dm.truthy(user.incapacitated))
            return;

        if (!Dm.truthy(on))
        {
            if (Dm.truthy(await turn_on(user)))
            {
                to_chat(user, span_notice(I18n.T("You turn the jetpack on.")));
            }
            else
            {
                to_chat(user, span_notice(I18n.T("You fail to turn the jetpack on.")));
                return;
            }
        }
        else
        {
            turn_off(user);
            to_chat(user, span_notice(I18n.T("You turn the jetpack off.")));
        }

        await update_item_action_buttons();
    }

    // tg: code/game/objects/items/tanks/jetpack.dm:104
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", Dm.initial(this, (obj_item_tank_jetpack p) => p.icon_state), Dm.truthy(on) ? "-on" : "");
        return dot;
    }

    // tg: code/game/objects/items/tanks/jetpack.dm:108
    public virtual async DmTask<object> turn_on(mob user = null)
    {
        if (Dm.truthy(disabled))
            return FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_JETPACK_ACTIVATED]) ? (object)NONE : _SendSignal(COMSIG_JETPACK_ACTIVATED, Dm.list(this, user))), JETPACK_ACTIVATION_FAILED)))
            return FALSE;
        on = TRUE;
        Waiting.NoSleep(update_icon(UPDATE_ICON_STATE));
        if (Dm.truthy(full_speed))
            await user.add_movespeed_modifier(typeof(datum_movespeed_modifier_jetpack_full_speed));
        if (!Dm.truthy(stabilize))
            ADD_TRAIT(user, TRAIT_NOGRAV_ALWAYS_DRIFT, JETPACK_TRAIT);
        return TRUE;
    }

    // tg: code/game/objects/items/tanks/jetpack.dm:121
    public virtual void turn_off(mob user = null)
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_JETPACK_DEACTIVATED]))
            _SendSignal(COMSIG_JETPACK_DEACTIVATED, Dm.list(this, user));
        on = FALSE;
        Waiting.NoSleep(update_icon(UPDATE_ICON_STATE));
        if (Dm.truthy(user))
        {
            user.remove_movespeed_modifier(typeof(datum_movespeed_modifier_jetpack_full_speed));
            REMOVE_TRAIT(user, TRAIT_NOGRAV_ALWAYS_DRIFT, JETPACK_TRAIT);
        }
    }

    // tg: code/game/objects/items/tanks/jetpack.dm:129
    public virtual async DmTask<object> allow_thrust(object num = null, object use_fuel = null)
    {
        use_fuel ??= TRUE;
        if (!ismob(loc))
            return FALSE;
        mob user = loc as mob;

        if (Dm.num(num) < 0.005f || Dm.num(air_contents.total_moles()) < Dm.num(num))
        {
            turn_off(user);
            return FALSE;
        }

        // We've got the gas, it's chill
        if (!Dm.truthy(use_fuel))
            return TRUE;

        datum_gas_mixture removed = remove_air(num) as datum_gas_mixture;
        if (Dm.num(removed.total_moles()) < 0.005f)
        {
            turn_off(user);
            return FALSE;
        }

        turf T = get_turf(this);
        await T.assume_air(removed);
        return TRUE;
    }

    // tg: code/game/objects/items/tanks/jetpack.dm:151  suicide_act: live-partial (the suicide verb, M10).

    // tg: code/game/objects/items/tanks/jetpack.dm:159
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_CONTENTS)))
            return dot;
        if (ismob(loc) && Dm.truthy(Dm.bitand(item_flags, IN_INVENTORY)))
        {
            mob wearer = loc as mob;
            turn_off(wearer);
        }
        else
        {
            turn_off();
        }
        await update_item_action_buttons();
        disabled = TRUE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(remove_emp)), 4 * SECONDS, file: "code/game/objects/items/tanks/jetpack.dm", line: 170);
        return dot;
    }

    ///Removes the disabled flag after getting EMPd
    // tg: code/game/objects/items/tanks/jetpack.dm:173
    public virtual void remove_emp()
    {
        disabled = FALSE;
    }

    /**
     * The port's own (the locomotion bridge's preview, through datum_component_jetpack.preview_stabilize): allow_thrust's
     * answer with use_fuel FALSE without its side effect (tg turns the jetpack off there when the gas has run out; the
     * preview leaves that to the real call). The improvised jetpack's random cut-out (:194) is a side effect too and is left
     * to the real call.
     */
    internal bool preview_thrust(object num)
    {
        if (!ismob(loc))
            return false;
        return !(Dm.num(num) < 0.005f || Dm.num(air_contents.total_moles()) < Dm.num(num));
    }
}

// tg: code/game/objects/items/tanks/jetpack.dm:176
public partial class obj_item_tank_jetpack_improvised : obj_item_tank_jetpack
{
    // tg: code/game/objects/items/tanks/jetpack.dm:189
    public override async DmTask<object> allow_thrust(object num = null, object use_fuel = null)
    {
        if (!ismob(loc))
            return FALSE;

        mob user = loc as mob;
        if (rand(0, 250) == 0)
        {
            to_chat(user, span_notice(I18n.T("You feel your jetpack's engines cut out.")));
            turn_off(user);
            return null;
        }
        return await base.allow_thrust(num, use_fuel);
    }
}
