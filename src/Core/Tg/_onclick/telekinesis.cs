// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/telekinesis.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/*
    Telekinesis

    This needs more thinking out, but I might as well.
*/

// tg: code/_onclick/telekinesis.dm:17
public partial class atom : datum
{
    /**
     * Telekinesis attack act, happens when the TK user clicks on a non-adjacent target in range.
     *
     * * By default, emulates the user's unarmed attack.
     * * Called indirectly by the `COMSIG_MOB_ATTACK_RANGED` signal.
     * * Returns `COMPONENT_CANCEL_ATTACK_CHAIN` when it performs any action, to further acts on the attack chain.
     */
    // tg: code/_onclick/telekinesis.dm:17
    public virtual async DmTask<object> attack_tk(mob user = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(user) || !Dm.truthy(tkMaxRangeCheck(user, this)))
            return null;
        Dm.@new<obj_effect_temp_visual_telekinesis>(get_turf(this));
        add_hiddenprint(user);
        await user.UnarmedAttack(this, FALSE); // attack_hand, attack_paw, etc
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    /**
     * Telekinesis item attack_self act.
     *
     * * This is similar to item attack_self, but applies to anything that you can grab with a telekinetic grab.
     * * It is used for manipulating things at range, for example, opening and closing closets..
     * * Defined at the `/atom` level but only used at the `/obj/item` one.
     * * Returns `COMPONENT_CANCEL_ATTACK_CHAIN` when it performs any action, to further acts on the attack chain.
     */
    // tg: code/_onclick/telekinesis.dm:68
    public virtual async DmTask<object> attack_self_tk(mob user = null)
    {
        return null;
    }

    // tg: code/_onclick/telekinesis.dm:76
    public virtual async DmTask<object> attack_self_secondary_tk(mob user = null)
    {
        return null;
    }
}

// tg: code/_onclick/telekinesis.dm:26
public partial class obj : atom_movable
{
    // tg: code/_onclick/telekinesis.dm:26
    public override async DmTask<object> attack_tk(mob user = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(user))
            return null;
        if (Dm.truthy(anchored))
            return await base.attack_tk(user);
        return await attack_tk_grab(user);
    }

    /**
     * Telekinesis object grab act.
     *
     * * Called by `/obj/attack_tk()`.
     * * Returns `COMPONENT_CANCEL_ATTACK_CHAIN` when it performs any action, to further acts on the attack chain.
     */
    // tg: code/_onclick/telekinesis.dm:47
    public virtual async DmTask<object> attack_tk_grab(mob user = null)
    {
        obj_item_tk_grab O = Dm.@new<obj_item_tk_grab>(this);
        O.tk_user = user as mob_living_carbon;
        if (!Dm.truthy(O.focus_object(this)))
            return null;
        await user.put_in_active_hand(O);
        add_hiddenprint(user);
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }
}

// tg: code/_onclick/telekinesis.dm:34
public partial class obj_item : obj
{
    // tg: code/_onclick/telekinesis.dm:34
    public override async DmTask<object> attack_tk(mob user = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(user))
            return null;
        return await attack_tk_grab(user);
    }

    // tg: code/_onclick/telekinesis.dm:72
    public override async DmTask<object> attack_self_tk(mob user = null)
    {
        if (Dm.truthy(await attack_self(user)))
            return ITEM_INTERACT_BLOCKING;
        return null;
    }

    // tg: code/_onclick/telekinesis.dm:80
    public override async DmTask<object> attack_self_secondary_tk(mob user = null)
    {
        if (Dm.truthy(await attack_self_secondary(user)))
            return ITEM_INTERACT_BLOCKING;
        return null;
    }
}

// tg: code/_onclick/telekinesis.dm:58
public partial class mob : atom_movable
{
    // tg: code/_onclick/telekinesis.dm:58
    public override async DmTask<object> attack_tk(mob user = null)
    {
        return null;
    }
}

/*
    TK Grab Item (the workhorse of old TK)

    * If you have not grabbed something, do a normal tk attack
    * If you have something, throw it at the target.  If it is already adjacent, do a normal attackby()
    * If you click what you are holding, or attack_self(), do an attack_self_tk() on it.
    * Deletes itself if it is ever not in your hand, or if you should have no access to TK.
*/
// tg: code/_onclick/telekinesis.dm:93
public partial class obj_item_tk_grab : obj_item
{
    // tg: code/_onclick/telekinesis.dm:107
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSfastprocess, this);
        return dot;
    }

    // tg: code/_onclick/telekinesis.dm:111
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSfastprocess, this);
        if (!QDELETED(focus))
            REMOVE_TRAIT(focus, TRAIT_TELEKINESIS_CONTROLLED, REF(tk_user));
        focus = null;
        tk_user = null;
        return base.Destroy(force);
    }

    // tg: code/_onclick/telekinesis.dm:118
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(check_if_focusable(focus as obj))) //if somebody grabs your thing, no waiting for them to put it down and hitting them again.
            update_appearance();
        return null;
    }

    // tg: code/_onclick/telekinesis.dm:122
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        if (focus != null && user != null && loc != user && loc != user.loc) // drop_item() gets called when you tk-attack a table/closet with an item
        {
            if (Dm.truthy(await focus.Adjacent(loc as atom)))
                await focus.forceMove(loc);
        }
        return await base.dropped(user, silent, __extra);
    }

    //stops TK grabs being equipped anywhere but into hands
    // tg: code/_onclick/telekinesis.dm:129
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HANDS)))
            return;
        qdel(this);
    }

    // tg: code/_onclick/telekinesis.dm:135
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        if (focus != null)
            return await focus.examine(user);
        return await base.examine(user, __extra);
    }

    // tg: code/_onclick/telekinesis.dm:142
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = null;
        if (focus == null)
            return dot;
        if (QDELING(focus))
        {
            qdel(this);
            return dot;
        }
        if (Dm.truthy(Dm.bitand(await focus.attack_self_tk(user), ITEM_INTERACT_ANY_BLOCKER)))
            dot = TRUE;
        update_appearance();
        return dot;
    }

    // tg: code/_onclick/telekinesis.dm:151
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return await ranged_interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/_onclick/telekinesis.dm:154
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = null;
        if (focus == null)
        {
            focus_object(interacting_with as obj);
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(check_if_focusable(focus as obj)))
            return NONE;

        if (interacting_with == focus)
        {
            if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
            {
                object or__1 = await focus.attack_self_secondary_tk(user);
                dot = Dm.truthy(or__1) ? or__1 : NONE;
            }
            else
            {
                object or__2 = await interacting_with.attack_self_tk(user);
                dot = Dm.truthy(or__2) ? or__2 : NONE;
            }
        }
        else if (isitem(focus))
        {
            obj_item focused_item = focus as obj_item;
            apply_focus_overlay();
            if (Dm.truthy(await interacting_with.Adjacent(focus)))
            {
                dot = Dm.truthy(await focused_item.melee_attack_chain(user, interacting_with, modifiers)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
                if (Dm.truthy(check_if_focusable(focus as obj)))
                    await focus.do_attack_animation(interacting_with, null, focus as obj_item);
            }

            // isgun check lets us shoot guns at range
            // quoting the old comment: "I've only tested this with guns, and it took some doing to make it work"
            // reader beware if trying to add other snowflake cases
            else if (isgun(focused_item))
                dot = await interacting_with.base_ranged_item_interaction(user, focus as obj_item, modifiers);
        }

        user.changeNext_move(CLICK_CD_MELEE);
        update_appearance();
        return dot;
    }

    // tg: code/_onclick/telekinesis.dm:186
    public override async DmTask<object> on_thrown(mob_living_carbon user = null, atom target = null)
    {
        if (target == null || user == null)
            return null;

        if (focus == null)
            return null;

        if (!Dm.truthy(check_if_focusable(focus as obj)))
            return null;

        if (target == focus)
        {
            if (Dm.truthy(Dm.bitand(await target.attack_self_tk(user), ITEM_INTERACT_ANY_BLOCKER)))
                return null;
            update_appearance();
            return null;
        }

        apply_focus_overlay();
        //Only items can be thrown 10 tiles everything else only 1 tile
        await focus.throw_at(target, focus.tk_throw_range, 1, user);
        var start_turf = get_turf(focus) as turf;
        var end_turf = get_turf(target) as turf;
        user.log_message(Dm.text("has thrown [] from [] towards [] using Telekinesis.", focus, AREACOORD(start_turf), AREACOORD(end_turf)), LOG_ATTACK);
        user.changeNext_move(CLICK_CD_MELEE);
        update_appearance();
        return null;
    }

    // tg: code/_onclick/telekinesis.dm:218
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        return null;
    }

    // tg: code/_onclick/telekinesis.dm:221
    public virtual object focus_object(obj target = null)
    {
        if (!Dm.truthy(check_if_focusable(target)))
            return null;
        focus = target;
        ADD_TRAIT(focus, TRAIT_TELEKINESIS_CONTROLLED, REF(tk_user));
        update_appearance();
        apply_focus_overlay();
        return TRUE;
    }

    // tg: code/_onclick/telekinesis.dm:229
    public virtual object check_if_focusable(obj target = null)
    {
        if (tk_user == null || !istype(tk_user, typeof(mob_living_carbon)) || QDELETED(target) || !istype(target, typeof(obj)) || !Dm.truthy(tk_user.dna.check_mutation(typeof(datum_mutation_telekinesis))))
        {
            qdel(this);
            return null;
        }
        if (!Dm.truthy(tkMaxRangeCheck(tk_user, target)) || Dm.truthy(target.anchored) || !isturf(target.loc))
        {
            qdel(this);
            return null;
        }
        return TRUE;
    }

    // tg: code/_onclick/telekinesis.dm:238
    public virtual void apply_focus_overlay()
    {
        if (focus == null)
            return;
        Dm.@new<obj_effect_temp_visual_telekinesis>(get_turf(focus));
    }

    // tg: code/_onclick/telekinesis.dm:243
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (focus == null)
            return dot;

        mutable_appearance focus_overlay = Dm.@new<mutable_appearance>(focus);
        focus_overlay.layer = layer + 0.01f;
        SET_PLANE_EXPLICIT(focus_overlay, ABOVE_HUD_PLANE, focus);
        Dm.append(ref dot, focus_overlay);
        return dot;
    }

    // tg: code/_onclick/telekinesis.dm:253
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is using [] telekinesis to choke []self! It looks like [] trying to commit suicide!", user, user.p_their(), user.p_them(), user.p_theyre())));
        return OXYLOSS;
    }
}

public static partial class GlobalProcs
{
    // tg: code/_onclick/telekinesis.dm:7
    private const int TK_MAXRANGE = 15;

    // tg: code/_onclick/telekinesis.dm:211
    public static object tkMaxRangeCheck(mob user = null, atom target = null)
    {
        float d = get_dist(user, target);
        if (d > TK_MAXRANGE)
        {
            user.balloon_alert(user, I18n.T("can't TK, too far!"));
            return null;
        }
        return TRUE;
    }
}
