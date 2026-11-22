// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/dna_infuser/organ_sets/gondola_organs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3b, D327): the gondola's infused organs (the pacifist heart the hostile fauna take for their own, the mute zen
// tongue, the needle-armed liver whose hugs inject pax and whose arms hold only tiny things) and their set bonus, indifference to the
// environment. The colour defines are file-local; the var blocks are generated (D111).

/// makes you a pacifist and turns most mobs neutral towards you
// tg: code/game/machinery/dna_infuser/organ_sets/gondola_organs.dm:20
public partial class obj_item_organ_heart_gondola : obj_item_organ_heart
{
    // tg: code/game/machinery/dna_infuser/organ_sets/gondola_organs.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_gondola)));
        Waiting.NoSleep(AddElement(typeof(datum_element_noticable_organ), I18n.T("%PRONOUN_They radiate%PRONOUN_s an aura of serenity.")));
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_blocker)));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/gondola_organs.dm:38
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon receiver = organ_owner;
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        if (!Dm.truthy(receiver.has_faction(FACTION_HOSTILE)))
            LAZYADD(ref factions_to_remove, FACTION_HOSTILE);
        if (!Dm.truthy(receiver.has_faction(FACTION_MINING)))
            LAZYADD(ref factions_to_remove, FACTION_MINING);
        receiver.add_faction(Dm.list(FACTION_HOSTILE, FACTION_MINING));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/gondola_organs.dm:46
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon heartless = organ_owner as mob_living_carbon;
        await base.on_mob_remove(organ_owner, special, movement_flags);
        if (Dm.truthy(LAZYLEN(factions_to_remove)))
            heartless.remove_faction(factions_to_remove);
    }
}

/// Zen (tounge): You can no longer speak, but get a powerful positive moodlet
// tg: code/game/machinery/dna_infuser/organ_sets/gondola_organs.dm:52
public partial class obj_item_organ_tongue_gondola : obj_item_organ_tongue
{
    // tg: code/game/machinery/dna_infuser/organ_sets/gondola_organs.dm:64
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_noticable_organ), I18n.T("%PRONOUN_Their mouth is permanently affixed into a relaxed smile."), BODY_ZONE_PRECISE_MOUTH));
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_gondola)));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/gondola_organs.dm:69
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon tongue_owner = organ_owner;
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        await tongue_owner.add_mood_event("gondola_zen", typeof(datum_mood_event_gondola_serenity));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/gondola_organs.dm:73
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon tongue_owner = organ_owner as mob_living_carbon;
        tongue_owner.clear_mood_event("gondola_zen");
        await base.on_mob_remove(organ_owner, special, movement_flags);
    }
}

/// Loving arms: your hands become unable to hold much of anything but your hugs now infuse the subject with pax.
// tg: code/game/machinery/dna_infuser/organ_sets/gondola_organs.dm:78
public partial class obj_item_organ_liver_gondola : obj_item_organ_liver
{
    // tg: code/game/machinery/dna_infuser/organ_sets/gondola_organs.dm:87
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_gondola)));
        Waiting.NoSleep(AddElement(typeof(datum_element_noticable_organ), I18n.T("%PRONOUN_Their left arm has small needles breaching the skin all over it."), BODY_ZONE_L_ARM));
        Waiting.NoSleep(AddElement(typeof(datum_element_noticable_organ), I18n.T("%PRONOUN_Their right arm has small needles breaching the skin all over it."), BODY_ZONE_R_ARM));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/gondola_organs.dm:93
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon liver_owner = organ_owner;
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        bool has_left = Dm.truthy(liver_owner.has_left_hand(check_disabled: FALSE));
        bool has_right = Dm.truthy(liver_owner.has_right_hand(check_disabled: FALSE));
        if (has_left && has_right)
            to_chat(liver_owner, span_warning(I18n.T("Your arms grow terribly weak as small, needle-like pricks grow all over them!")));
        else if (has_left || has_right)
            to_chat(liver_owner, span_warning(I18n.T("Your arm grows terribly weak as small, needle-like pricks grow all over it!")));
        else
            to_chat(liver_owner, span_warning(I18n.T("You feel like something would be happening to your arms right now... if you still had them.")));
        to_chat(liver_owner, span_notice(I18n.T("Hugging a target will pacify them, but you won't be able to carry much of anything anymore.")));
        RegisterSignal(liver_owner, COMSIG_HUMAN_EQUIPPING_ITEM, nameof(on_owner_equipping_item));
        RegisterSignal(liver_owner, COMSIG_LIVING_TRY_PULL, nameof(on_owner_try_pull));
        RegisterSignal(liver_owner, COMSIG_CARBON_HELPED, nameof(on_hug));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/gondola_organs.dm:108
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon liver_owner = organ_owner as mob_living_carbon;
        await base.on_mob_remove(organ_owner, special, movement_flags);
        UnregisterSignal(liver_owner, Dm.list(COMSIG_HUMAN_EQUIPPING_ITEM, COMSIG_LIVING_TRY_PULL, COMSIG_CARBON_HELPED));
    }

    /// signal sent when prompting if an item can be equipped
    // tg: code/game/machinery/dna_infuser/organ_sets/gondola_organs.dm:113
    [SignalHandler]
    public virtual object on_owner_equipping_item(mob_living_carbon_human owner = null, obj_item equip_target = null, object slot = null)
    {
        if (Dm.num(equip_target.w_class) > WEIGHT_CLASS_TINY)
        {
            equip_target.balloon_alert(owner, I18n.T("too weak to hold this!"));
            return COMPONENT_BLOCK_EQUIP;
        }
        return null;
    }

    /// signal sent when owner tries to pull an item
    // tg: code/game/machinery/dna_infuser/organ_sets/gondola_organs.dm:120
    [SignalHandler]
    public virtual object on_owner_try_pull(mob_living_carbon owner = null, atom_movable target = null, object force = null)
    {
        if (isliving(target))
        {
            mob_living living_target = target as mob_living;
            if (living_target.mob_size > MOB_SIZE_TINY)
            {
                living_target.balloon_alert(owner, I18n.T("too weak to pull this!"));
                return COMSIG_LIVING_CANCEL_PULL;
            }
        }
        if (isitem(target))
        {
            obj_item item_target = target as obj_item;
            if (Dm.num(item_target.w_class) > WEIGHT_CLASS_TINY)
            {
                item_target.balloon_alert(owner, I18n.T("too weak to pull this!"));
                return COMSIG_LIVING_CANCEL_PULL;
            }
        }
        return null;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/gondola_organs.dm:134
    [SignalHandler]
    public virtual void on_hug(mob_living_carbon_human source = null, mob_living_carbon hugged = null)
    {
        DmList covered_body_zones = source.get_covered_body_zones() as DmList;
        float pax_injected = 4;
        if (Dm.@in(BODY_ZONE_L_ARM, covered_body_zones))
            pax_injected -= 2;
        if (Dm.@in(BODY_ZONE_R_ARM, covered_body_zones))
            pax_injected -= 2;
        if (pax_injected > 0 && hugged.reagents is datum_reagents reagents && Dm.truthy(Waiting.NoSleep(reagents.add_reagent(typeof(datum_reagent_pax), pax_injected))))
            to_chat(hugged, span_warning(I18n.T("You feel a tiny prick!")));
    }
}
