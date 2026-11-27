// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/monster_organs/monster_organ.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * Stabilising serum prevents monster organs from decaying before you can use them.
 */
// tg: code/modules/mining/equipment/monster_organs/monster_organ.dm:4
public partial class obj_item_mining_stabilizer : obj_item
{
    // tg: code/modules/mining/equipment/monster_organs/monster_organ.dm:11
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isorgan(interacting_with))
            return NONE;
        obj_item_organ_monster_core target_core = interacting_with as obj_item_organ_monster_core;
        if (target_core == null)
        {
            balloon_alert(user, I18n.T("invalid target!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(target_core.preserve()))
        {
            balloon_alert(user, I18n.T("organ decayed!"));
            return ITEM_INTERACT_BLOCKING;
        }

        balloon_alert(user, I18n.T("organ stabilized"));
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

/**
 * Useful organs which drop as loot from a mining creature.
 * Generalised behaviour is that they will decay and become useless unless provided with serum.
 * These should usually do something both when used in-hand, or when implanted into someone.
 */
// tg: code/modules/mining/equipment/monster_organs/monster_organ.dm:32
public partial class obj_item_organ_monster_core : obj_item_organ
{
    // tg: code/modules/mining/equipment/monster_organs/monster_organ.dm:61
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        decay_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(go_inert)), Dm.num(time_to_decay), TIMER_STOPPABLE, file: "code/modules/mining/equipment/monster_organs/monster_organ.dm", line: 63);
        Waiting.NoSleep(AddElement(typeof(datum_element_above_mob_drop)));
        return dot;
    }

    // tg: code/modules/mining/equipment/monster_organs/monster_organ.dm:66
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!Dm.truthy(decay_timer))
            return dot;
        float estimated_time_left = round(Dm.num(timeleft(decay_timer)), 1 * MINUTES);
        if (estimated_time_left == 4 * MINUTES)
            Dm.append(ref dot, span_notice(I18n.T("It's fresh and still pulsating with the last vestiges of life.")));
        else if (estimated_time_left == 3 * MINUTES)
            Dm.append(ref dot, span_notice(I18n.T("It still looks pretty fresh.")));
        else if (estimated_time_left == 2 * MINUTES)
            Dm.append(ref dot, span_notice(I18n.T("It's not as fresh as could be.")));
        else if (estimated_time_left == 1 * MINUTES)
            Dm.append(ref dot, span_notice(I18n.T("Signs of decay are starting to set in. It might not be good for much longer.")));
        else if (estimated_time_left >= 0 * SECONDS && estimated_time_left <= 1 * MINUTES)
            Dm.append(ref dot, span_warning(I18n.T("Signs of decay have set in, but it still looks alive. It's probably about to become unusable really quickly.")));
        return dot;
    }

    // tg: code/modules/mining/equipment/monster_organs/monster_organ.dm:83
    public override object Destroy(float? force = null)
    {
        deltimer(decay_timer);
        return base.Destroy(force);
    }

    // tg: code/modules/mining/equipment/monster_organs/monster_organ.dm:87
    public override async DmTask<object> on_mob_insert(mob_living_carbon target_carbon = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(target_carbon, special, movement_flags);

        if (Dm.truthy(inert))
        {
            to_chat(target_carbon, span_notice(I18n.T("[] breaks down as you try to insert it.", this)));
            qdel(this);
            return FALSE;
        }
        if (!Dm.truthy(decay_timer))
            return TRUE;
        preserve(TRUE);
        target_carbon.visible_message(span_notice(I18n.T("[] stabilizes as it's inserted.", this)));
        return TRUE;
    }

    // tg: code/modules/mining/equipment/monster_organs/monster_organ.dm:100
    public override async DmTask on_mob_remove(object target_carbon = null, float? special = null, object movement_flags = null)
    {
        if (!Dm.truthy(inert) && !Dm.truthy(special))
        {
            (target_carbon as mob_living_carbon)?.visible_message(span_notice(I18n.T("[] rapidly decays as it's removed.", this)));
            go_inert();
        }
        await base.on_mob_remove(target_carbon, special, movement_flags);
    }

    /**
     * Preserves the organ so that it will not decay.
     * Returns true if successful.
     * * Implanted - If true, organ has just been inserted into someone.
     */
    // tg: code/modules/mining/equipment/monster_organs/monster_organ.dm:111
    public virtual object preserve(float? implanted = null)
    {
        implanted ??= FALSE;
        if (Dm.truthy(inert))
            return FALSE;
        deltimer(decay_timer);
        decay_timer = null;
        update_appearance();
        return TRUE;
    }

    /**
     * Decays the organ, it is now useless.
     */
    // tg: code/modules/mining/equipment/monster_organs/monster_organ.dm:122
    public virtual object go_inert()
    {
        if (Dm.truthy(inert))
            return FALSE;
        inert = TRUE;
        deltimer(decay_timer);
        decay_timer = null;
        name = I18n.T("decayed []", name);
        update_appearance();
        return TRUE;
    }

    // tg: code/modules/mining/equipment/monster_organs/monster_organ.dm:132
    public override object update_desc(float? updates = null)
    {
        if (Dm.truthy(inert))
        {
            desc = Dm.truthy(desc_inert) ? desc_inert as string : Dm.initial(type, (obj_item_organ_monster_core m) => m.desc);
            return base.update_desc(updates);
        }
        if (!Dm.truthy(decay_timer))
        {
            desc = Dm.truthy(desc_preserved) ? desc_preserved as string : Dm.initial(type, (obj_item_organ_monster_core m) => m.desc);
            return base.update_desc(updates);
        }
        desc = Dm.initial(type, (obj_item_organ_monster_core m) => m.desc);
        return base.update_desc(updates);
    }

    // tg: code/modules/mining/equipment/monster_organs/monster_organ.dm:142
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(inert))
        {
            icon_state = Dm.truthy(icon_state_inert) ? icon_state_inert as string : Dm.initial(type, (obj_item_organ_monster_core m) => m.icon_state);
            return await base.update_icon_state(__extra);
        }
        if (!Dm.truthy(decay_timer))
        {
            icon_state = Dm.truthy(icon_state_preserved) ? icon_state_preserved as string : Dm.initial(type, (obj_item_organ_monster_core m) => m.icon_state);
            return await base.update_icon_state(__extra);
        }
        icon_state = Dm.initial(type, (obj_item_organ_monster_core m) => m.icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/mining/equipment/monster_organs/monster_organ.dm:152
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isliving(interacting_with))
            return NONE;

        try_apply(interacting_with, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mining/equipment/monster_organs/monster_organ.dm:159
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(await user.can_perform_action(this, Dm.bitor(FORBID_TELEKINESIS_REACH, ALLOW_RESTING))))
            return null;
        try_apply(user, user);
        return null;
    }

    /**
     * Tries to apply organ effect to target.
     * Usually you should not need to override this, only apply_to.
     * Arguments
     * * target - Person you are applying this to.
     * * user - Person who is doing the applying.
     */
    // tg: code/modules/mining/equipment/monster_organs/monster_organ.dm:171
    public virtual void try_apply(atom target = null, mob user = null)
    {
        if (!isliving(target))
        {
            balloon_alert(user, I18n.T("invalid target!"));
            return;
        }
        if (Dm.truthy(inert))
        {
            balloon_alert(user, I18n.T("organ decayed!"));
            return;
        }
        mob_living live_target = target as mob_living;
        if (live_target.stat == DEAD)
        {
            balloon_alert(user, I18n.T("they're dead!"));
            return;
        }
        balloon_alert(user, I18n.T("applied organ"));
        apply_to(live_target, user);
    }

    /**
     * Applies the effect of this organ to the target.
     * Arguments
     * * target - Person you are applying this to.
     * * user - Person who is doing the applying.
     */
    // tg: code/modules/mining/equipment/monster_organs/monster_organ.dm:191
    public virtual void apply_to(mob_living target = null, mob user = null)
    {
        if (user_status != null)
            Waiting.NoSleep(target.apply_status_effect(user_status));
        qdel(this);
    }

    /**
     * Utility proc to find the associated monster organ action and trigger it.
     * Call this instead of on_triggered_internal() if the action needs to trigger automatically, or the cooldown won't happen.
     */
    // tg: code/modules/mining/equipment/monster_organs/monster_organ.dm:200
    public virtual async DmTask trigger_organ_action(object trigger_flags = null)
    {
        datum_action_cooldown_monster_core_action action = Dm.locate<datum_action_cooldown_monster_core_action>(actions) as datum_action_cooldown_monster_core_action;
        if (action != null)
            await action.Trigger(trigger_flags: trigger_flags);
    }

    /**
     * Called when activated while implanted inside someone.
     * This could be via clicking the associated action button or through the above method.
     */
    // tg: code/modules/mining/equipment/monster_organs/monster_organ.dm:208
    public virtual void on_triggered_internal()
    {
        throw Dm.CRASH("Someone forgot to make their organ do something when you implant it.", "code/modules/mining/equipment/monster_organs/monster_organ.dm", 210);
    }

    // tg: code/modules/mining/equipment/monster_organs/monster_organ.dm:212
    public override object feel_for_damage(object self_aware = null)
    {
        return "";
    }
}

/**
 * Boilerplate to set the name and icon of the cooldown action.
 * Makes it call 'ui_action_click' when the action is activated.
 */
// tg: code/modules/mining/equipment/monster_organs/monster_organ.dm:219
public partial class datum_action_cooldown_monster_core_action : datum_action_cooldown
{
    // tg: code/modules/mining/equipment/monster_organs/monster_organ.dm:224
    public override async DmTask<object> Activate(atom trigger_flags = null)
    {
        object dot = await base.Activate(trigger_flags);
        if (this.target == null)
            return dot;
        obj_item_organ_monster_core organ = this.target as obj_item_organ_monster_core;
        if (organ == null)
            return dot;
        organ.on_triggered_internal();
        return dot;
    }
}
