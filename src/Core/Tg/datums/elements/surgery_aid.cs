// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/surgery_aid.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The drapes (and a bedsheet, a cloak, a cape) prepare the targeted body part for surgery:
// the surgery_prepped status effect gives the patient and the part TRAIT_READY_TO_OPERATE, and they fall off a patient who walks
// around. The screentip (on_context) is translated with it; the client shows no screentips yet (D264).

/// Item can be applied to mobs to prepare them for surgery (allowing people to operate on them)
// tg: code/datums/elements/surgery_aid.dm:2
public partial class datum_element_surgery_aid : datum_element
{
    // tg: code/datums/elements/surgery_aid.dm:8
    [DmParams("target", "aid_name")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object aid_name = Dm.arg(__extra, 0) ?? "things";
        object dot = await base.Attach(target, Dm.forward(__extra, aid_name));
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;

        this.aid_name = aid_name;
        RegisterSignal(target, COMSIG_ITEM_REQUESTING_CONTEXT_FOR_TARGET, nameof(on_context));
        RegisterSignal(target, COMSIG_ITEM_INTERACTING_WITH_ATOM, nameof(on_item_interaction));

        obj_item realtarget = (obj_item)target;
        Dm.combine(ref realtarget.item_flags, ITEM_HAS_CONTEXTUAL_SCREENTIPS);
        return dot;
    }

    // tg: code/datums/elements/surgery_aid.dm:20
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, Dm.list(COMSIG_ITEM_INTERACTING_WITH_ATOM, COMSIG_ITEM_REQUESTING_CONTEXT_FOR_TARGET));
        return dot;
    }

    // tg: code/datums/elements/surgery_aid.dm:24
    [SignalHandler]
    public virtual object on_context(obj_item source = null, DmList context = null, atom target = null, mob_living user = null)
    {
        if (!isliving(target))
            return NONE;

        mob_living target_mob = (mob_living)target;
        if (!Dm.truthy(target_mob.has_limbs))
        {
            context[SCREENTIP_CONTEXT_LMB] = HAS_TRAIT(source, TRAIT_READY_TO_OPERATE) ? I18n.T("Remove []", aid_name) : I18n.T("Prepare for surgery");
            return CONTEXTUAL_SCREENTIP_SET;
        }

        obj_item_bodypart precise_part = (target_mob.get_bodypart((string)deprecise_zone(user.zone_selected)) ?? target_mob.get_bodypart(BODY_ZONE_CHEST)) as obj_item_bodypart;
        context[SCREENTIP_CONTEXT_LMB] = HAS_TRAIT(precise_part, TRAIT_READY_TO_OPERATE) ? I18n.T("Remove []", aid_name) : I18n.T("Prepare [] for surgery", precise_part.plaintext_zone);
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/datums/elements/surgery_aid.dm:39
    [SignalHandler]
    public virtual object on_item_interaction(datum source = null, mob_living user = null, atom target = null, params object[] __extra)
    {
        if (!isliving(target))
            return NONE;

        mob_living target_mob = (mob_living)target;
        obj_item_bodypart precise_part = (target_mob.get_bodypart((string)deprecise_zone(user.zone_selected)) ?? target_mob.get_bodypart(BODY_ZONE_CHEST)) as obj_item_bodypart;
        Waiting.NoSleep(surgery_prep(target_mob, user, Dm.truthy(precise_part?.body_zone) ? precise_part.body_zone : BODY_ZONE_CHEST));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/datums/elements/surgery_aid.dm:50
    public virtual async DmTask surgery_prep(mob_living target_mob = null, mob_living surgeon = null, object body_zone = null)
    {
        datum_status_effect_surgery_prepped prep = target_mob.has_status_effect(typeof(datum_status_effect_surgery_prepped)) as datum_status_effect_surgery_prepped;
        if (prep == null || !Dm.@in(body_zone, prep.zones))
        {
            await target_mob.apply_status_effect(typeof(datum_status_effect_surgery_prepped), body_zone, aid_name);
            target_mob.balloon_alert(surgeon, I18n.T("[] surgery prepared", parse_zone(body_zone)));
            return;
        }
        prep.untrack_surgery(body_zone);
        target_mob.balloon_alert(surgeon, I18n.T("surgery cleared"));
    }
}

/// Tracks which body zones have been prepped for surgery
// tg: code/datums/elements/surgery_aid.dm:61
public partial class datum_status_effect_surgery_prepped : datum_status_effect
{
    // tg: code/datums/elements/surgery_aid.dm:74
    [DmParams("new_owner", "target_zone", "aid_name")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object target_zone = Dm.arg(__extra, 0);
        object aid_name = Dm.arg(__extra, 1) ?? "things";
        object dot = await base.on_creation(new_owner, Dm.forward(__extra, target_zone, aid_name));
        track_surgery(target_zone);
        LAZYOR(ref surgical_aids, aid_name);
        ADD_TRAIT(owner, TRAIT_READY_TO_OPERATE, TRAIT_STATUS_EFFECT(id)); // needs to happen after tracking starts
        return dot;
    }

    // tg: code/datums/elements/surgery_aid.dm:80
    [DmParams("new_owner", "target_zone", "aid_name")]
    public override async DmTask refresh(object effect = null, params object[] __extra)
    {
        object target_zone = Dm.arg(__extra, 0);
        object aid_name = Dm.arg(__extra, 1) ?? "things";
        track_surgery(target_zone);
        LAZYOR(ref surgical_aids, aid_name);
    }

    // tg: code/datums/elements/surgery_aid.dm:84
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        RegisterSignal(owner, COMSIG_MOVABLE_MOVED, nameof(on_move));
        RegisterSignal(owner, COMSIG_CARBON_POST_ATTACH_LIMB, nameof(on_attach_limb));
        RegisterSignal(owner, COMSIG_CARBON_POST_REMOVE_LIMB, nameof(on_detach_limb));
        return TRUE;
    }

    // tg: code/datums/elements/surgery_aid.dm:90
    public override async DmTask<object> on_remove()
    {
        foreach (object zone in Dm.each(zones))
            untrack_surgery(zone);
        REMOVE_TRAIT(owner, TRAIT_READY_TO_OPERATE, TRAIT_STATUS_EFFECT(id));
        UnregisterSignal(owner, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_CARBON_POST_ATTACH_LIMB, COMSIG_CARBON_POST_REMOVE_LIMB));
        return null;
    }

    // tg: code/datums/elements/surgery_aid.dm:96
    public override object get_examine_text(mob examiner = null)
    {
        DmList zones_readable = Dm.list();
        // give the body zones a consistent order, the same order as GLOB.all_body_zones
        foreach (object zone in Dm.each(Dm.bitand(GLOB.all_body_zones, zones)))
            Dm.append(ref zones_readable, parse_zone(zone));

        DmList aid_readable = Dm.list();
        foreach (object aid in Dm.each(surgical_aids))
            Dm.append(ref aid_readable, copytext_char(aid, -1) == "s" ? aid : I18n.T("\\a []", aid));

        // "They have surgial drapes and a bedsheet adorning their chest, arms, and legs."
        return I18n.T("[] [] [] adorning [] [].", owner.p_They(), owner.p_have(), english_list(aid_readable), owner.p_their(), english_list(zones_readable));
    }

    // tg: code/datums/elements/surgery_aid.dm:109
    [SignalHandler]
    public virtual void on_move(datum source = null, params object[] __extra)
    {
        if (Dm.eq(owner.body_position, STANDING_UP))
            movement_counter += 1;
        if (movement_counter < 4)
            return;
        // "The surgical drapes and bedsheets adorning John fall off!"
        owner.visible_message(span_warning(I18n.T("The [] adorning [] fall off!", english_list(surgical_aids), owner)));
        qdel(this);
    }

    // tg: code/datums/elements/surgery_aid.dm:120
    [SignalHandler]
    public virtual void on_attach_limb(datum source = null, obj_item_bodypart limb = null)
    {
        if (Dm.@in(limb.body_zone, zones))
            ADD_TRAIT(limb, TRAIT_READY_TO_OPERATE, TRAIT_STATUS_EFFECT(id));
    }

    // tg: code/datums/elements/surgery_aid.dm:126
    [SignalHandler]
    public virtual void on_detach_limb(datum source = null, obj_item_bodypart limb = null)
    {
        REMOVE_TRAIT(limb, TRAIT_READY_TO_OPERATE, TRAIT_STATUS_EFFECT(id));
    }

    // tg: code/datums/elements/surgery_aid.dm:131
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (Dm.eq(owner.body_position, LYING_DOWN) && movement_counter > 0)
            movement_counter -= 1;
        return null;
    }

    // tg: code/datums/elements/surgery_aid.dm:135
    public virtual void track_surgery(object body_zone = null)
    {
        LAZYADD(ref zones, body_zone);
        if (iscarbon(owner))
        {
            obj_item_bodypart precise_part = owner.get_bodypart((string)body_zone) as obj_item_bodypart;
            if (precise_part != null)
                ADD_TRAIT(precise_part, TRAIT_READY_TO_OPERATE, TRAIT_STATUS_EFFECT(id));
        }
        else if (!Dm.eq(body_zone, BODY_ZONE_CHEST))
            stack_trace("Attempting to track surgery on a non-carbon mob with a non-chest body zone! This should not happen.", "code/datums/elements/surgery_aid.dm", 142);
    }

    // tg: code/datums/elements/surgery_aid.dm:144
    public virtual void untrack_surgery(object body_zone = null)
    {
        LAZYREMOVE(ref zones, body_zone);
        if (iscarbon(owner))
        {
            obj_item_bodypart precise_part = owner.get_bodypart((string)body_zone) as obj_item_bodypart;
            if (precise_part != null)
                REMOVE_TRAIT(precise_part, TRAIT_READY_TO_OPERATE, TRAIT_STATUS_EFFECT(id));
        }
        if (!Dm.truthy(LAZYLEN(zones)) && !QDELETED(this))
            qdel(this); // no more zones to track, remove the status effect
    }
}
