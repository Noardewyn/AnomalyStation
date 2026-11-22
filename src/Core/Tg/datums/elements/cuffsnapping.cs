// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/cuffsnapping.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice E: the whole file. The branches of the cuffed item status effect (an item cuffed to a wrist, cuffable_item.dm) look
// the effect up, as tg does, but nothing applies it (the effect is not translated: section 46 of not-translated.md), so they
// are never reached. The var block and the element flags are generated.

/**
 * Cuffsnapping element! When added to an item allows it to attempt to break cuffs.
 * Depending on certain parameters and variables it might only be able to cut through cable, or take time, etc.
 *
 * Element is only compatible with items.
 */
// tg: code/datums/elements/cuffsnapping.dm:25
public partial class datum_element_cuffsnapping : datum_element
{
    // tg: code/datums/elements/cuffsnapping.dm:34
    [DmParams("target", "snap_time_weak", "snap_time_strong")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        float? snap_time_weak = Dm.arg<float?>(__extra, 0) ?? 0;
        object snap_time_strong = Dm.arg(__extra, 1);
        object dot = await base.Attach(target, Dm.forward(__extra, snap_time_weak, snap_time_strong));

        if (!isitem(target))
        {
            stack_trace(Dm.text("cuffsnapping element added to non-item object: \\[[]\\]", target), "code/datums/elements/cuffsnapping.dm", 42);
            return ELEMENT_INCOMPATIBLE;
        }

        this.snap_time_weak = snap_time_weak;
        this.snap_time_strong = snap_time_strong;

        RegisterSignal(target, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(target, COMSIG_ITEM_ATTACK_SECONDARY, nameof(try_cuffsnap_target));
        RegisterSignal(target, COMSIG_ITEM_REQUESTING_CONTEXT_FOR_TARGET, nameof(add_item_context));
        return dot;
    }

    // tg: code/datums/elements/cuffsnapping.dm:48
    public override object Detach(datum target = null, params object[] __extra)
    {
        UnregisterSignal(target, Dm.list(COMSIG_ITEM_ATTACK_SECONDARY, COMSIG_ATOM_EXAMINE, COMSIG_ITEM_REQUESTING_CONTEXT_FOR_TARGET));
        return base.Detach(target, __extra);
    }

    // tg: code/datums/elements/cuffsnapping.dm:52
    [SignalHandler]
    public virtual object add_item_context(obj_item source = null, DmList context = null, atom target = null, mob_living user = null)
    {
        if (!isliving(target)) //Removing restraints takes precedence
            return NONE;
        mob_living living_target = target as mob_living;
        if (iscarbon(living_target))
        {
            mob_living_carbon carbon_target = living_target as mob_living_carbon;
            if (Dm.truthy(carbon_target.handcuffed))
            {
                context[SCREENTIP_CONTEXT_RMB] = "Cut Restraints";
                return CONTEXTUAL_SCREENTIP_SET;
            }
        }
        if (Dm.truthy(living_target.has_status_effect(typeof(datum_status_effect_cuffed_item))))
        {
            context[SCREENTIP_CONTEXT_RMB] = "Remove Binds From Item";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return NONE;
    }

    ///signal called on parent being examined
    // tg: code/datums/elements/cuffsnapping.dm:68
    [SignalHandler]
    public virtual void on_examine(datum target = null, mob user = null, DmList examine_list = null)
    {
        string examine_string;
        if (snap_time_weak == null)
            return;
        examine_string = Dm.text("It looks like it could be used to cut zipties or cable restraints off someone in [] seconds", snap_time_weak);

        if (snap_time_strong != null)
            examine_string += Dm.text(", and handcuffs in [] seconds.", snap_time_strong);
        else
            examine_string += ".";

        Dm.append(ref examine_list, span_notice(examine_string));
    }

    ///Signal called on parent when it right-clicks another mob.
    // tg: code/datums/elements/cuffsnapping.dm:84
    [SignalHandler]
    public virtual object try_cuffsnap_target(obj_item cutter = null, mob_living target = null, mob_living cutter_user = null, DmList modifiers = null)
    {
        if (Dm.truthy(LAZYACCESS(cutter_user.do_afters, cutter)))
            return null;

        mob_living_carbon carbon_target = target as mob_living_carbon;
        if (carbon_target == null || !Dm.truthy(carbon_target.handcuffed))
        {
            object cuffed_status = target.has_status_effect(typeof(datum_status_effect_cuffed_item));
            if (!Dm.truthy(cuffed_status))
                return NONE;
            datum_status_effect_cuffed_item cuffed_status_effect = cuffed_status as datum_status_effect_cuffed_item;
            Waiting.Detach(try_cuffsnap_item(cutter, target, cutter_user, cuffed_status_effect.cuffed, cuffed_status_effect.cuffs));
            return COMPONENT_SKIP_ATTACK;
        }

        obj_item_restraints_handcuffs cuffs = carbon_target.handcuffed as obj_item_restraints_handcuffs;

        if (cuffs == null)
            return NONE;

        if (Dm.truthy(check_cuffs_strength(cutter, target, cutter_user, cuffs, span_notice(I18n.T("[] tries to cut through []'s restraints with [], but fails!", cutter_user, target, cutter)))))
            Waiting.Detach(do_cuffsnap_target(cutter, carbon_target, cutter_user, cuffs));

        return COMPONENT_SKIP_ATTACK;
    }

    ///Check that the type of restraints can be cut by this element.
    // tg: code/datums/elements/cuffsnapping.dm:109
    public virtual object check_cuffs_strength(obj_item cutter = null, mob_living target = null, mob_living cutter_user = null, obj_item_restraints_handcuffs cuffs = null, object message = null)
    {
        object snap_time = Dm.truthy(cuffs.restraint_strength) ? snap_time_strong : snap_time_weak;
        if (snap_time != null)
            return TRUE;
        cutter_user.visible_message(message);
        playsound(get_turf(cutter), Dm.truthy(cutter.usesound) ? cutter.usesound : cutter.hitsound, cutter.get_clamped_volume(), TRUE);
        return FALSE;
    }

    ///Called when a player tries to remove the cuffs binding an item to their owner
    // tg: code/datums/elements/cuffsnapping.dm:126
    public virtual async DmTask try_cuffsnap_item(obj_item cutter = null, mob_living target = null, mob_living cutter_user = null, obj_item cuffed = null, obj_item_restraints_handcuffs cuffs = null)
    {
        if (!Dm.truthy(check_cuffs_strength(cutter, target, cutter_user, cuffs, span_notice(I18n.T("[] tries to cut through the restraints binding [] to [], but fails!", cutter_user, cuffed, target)))))
            return;

        log_combat(cutter_user, target, Dm.text("cut or tried to cut restraints binding [] to", cuffed));

        await do_snip_snap(cutter, target, cutter_user, cuffs, span_notice(I18n.T("[] cuts the restraints binding [] to [] with []!", cutter_user, cuffed, target, cutter)));
    }

    ///Called when a player tries to remove the cuffs restraining another mob.
    // tg: code/datums/elements/cuffsnapping.dm:118
    public virtual async DmTask do_cuffsnap_target(obj_item cutter = null, mob_living_carbon target = null, mob cutter_user = null, obj_item_restraints_handcuffs cuffs = null)
    {
        if (Dm.truthy(LAZYACCESS(cutter_user.do_afters, cutter)))
            return;
        log_combat(cutter_user, target, Dm.text("cut or tried to cut []'s cuffs", target), cutter);

        await do_snip_snap(cutter, target, cutter_user, cuffs, span_notice(I18n.T("[] cuts []'s restraints with []!", cutter_user, target, cutter)));
    }

    ///The proc responsible for the very timed action that deletes the cuffs
    // tg: code/datums/elements/cuffsnapping.dm:135
    public virtual async DmTask do_snip_snap(obj_item cutter = null, mob_living target = null, mob cutter_user = null, obj_item_restraints_handcuffs cuffs = null, object message = null)
    {
        object snap_time = Dm.truthy(cuffs.restraint_strength) ? snap_time_strong : snap_time_weak;

        object target_was_restrained = FALSE;
        if (iscarbon(target))
        {
            mob_living_carbon carbon_target = target as mob_living_carbon;
            target_was_restrained = carbon_target.handcuffed;
        }

        if (snap_time != null && Dm.num(snap_time) > 0)
        {
            if (!Dm.truthy(await do_after(cutter_user, snap_time, target, interaction_key: cutter))) //No doafter if time = 0
                return;
            if (Dm.truthy(target_was_restrained)) //Removing restraints takes priority over cuffed items. This only applies for carbon mobs, but we need to make sure the restraints are still the same.
            {
                mob_living_carbon carbon_target = target as mob_living_carbon;
                if (carbon_target.handcuffed != cuffs)
                    return;
            }
        }

        await cutter_user.do_attack_animation(target, used_item: cutter);
        cutter_user.visible_message(message);
        qdel(cuffs);
        playsound(get_turf(cutter), Dm.truthy(cutter.usesound) ? cutter.usesound : cutter.hitsound, cutter.get_clamped_volume(), TRUE);
    }
}
