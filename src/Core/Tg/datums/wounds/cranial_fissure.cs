// Translated from tgstation (snapshot 1d99bd7f): code/datums/wounds/cranial_fissure.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M6 slice 3: the wound types). The var blocks are generated (D111). M6 slice 7 (D155): the split skull's
// displacement filter on the victim.

// tg: code/datums/wounds/cranial_fissure.dm:1
public partial class datum_wound_pregen_data_cranial_fissure : datum_wound_pregen_data
{
    // tg: code/datums/wounds/cranial_fissure.dm:14
    public override object get_weight(obj_item_bodypart limb = null, object woundtype = null, object damage = null, object attack_direction = null, object damage_source = null)
    {
        if (limb.owner == null)
            return base.get_weight(limb, woundtype, damage, attack_direction, damage_source);

        if (HAS_TRAIT(limb.owner, TRAIT_CURSED) && Dm.truthy(Dm.bitand(limb.mangled_state, BODYPART_MANGLED_INTERIOR)))
            return base.get_weight(limb, woundtype, damage, attack_direction, damage_source);

        if (limb.owner.stat >= HARD_CRIT)
            return base.get_weight(limb, woundtype, damage, attack_direction, damage_source);

        return 0;
    }
}

/// A wound applied when receiving significant enough damage to the head.
/// Will allow other players to take your eyes out of your head, and slipping
/// will cause your brain to fall out of your head.
// tg: code/datums/wounds/cranial_fissure.dm:29
public partial class datum_wound_cranial_fissure : datum_wound
{
    // tg: code/datums/wounds/cranial_fissure.dm:47  #define CRANIAL_FISSURE_FILTER_DISPLACEMENT (file-local, #undef at :141)
    private const string CRANIAL_FISSURE_FILTER_DISPLACEMENT = "cranial_fissure_displacement";

    // tg: code/datums/wounds/cranial_fissure.dm:49
    public override async DmTask wound_injury(datum_wound old_wound = null, object attack_direction = null)
    {
        ADD_TRAIT(limb, TRAIT_IMMUNE_TO_CRANIAL_FISSURE, type);
        ADD_TRAIT(victim, TRAIT_HAS_CRANIAL_FISSURE, type);

        victim.add_filter(CRANIAL_FISSURE_FILTER_DISPLACEMENT, 2, displacement_map_filter(Dm.icon(Dm.resource("icons/effects/cranial_fissure.dmi"), "displacement"), size: 3) as DmList);

        RegisterSignal(victim, COMSIG_MOB_SLIPPED, nameof(on_owner_slipped));
    }

    // tg: code/datums/wounds/cranial_fissure.dm:57
    public override async DmTask remove_wound(object ignore_limb = null, float? replaced = null, float? destroying = null)
    {
        REMOVE_TRAIT(limb, TRAIT_IMMUNE_TO_CRANIAL_FISSURE, type);
        if (victim != null)
        {
            REMOVE_TRAIT(victim, TRAIT_HAS_CRANIAL_FISSURE, type);
            victim.remove_filter(CRANIAL_FISSURE_FILTER_DISPLACEMENT);
            UnregisterSignal(victim, COMSIG_MOB_SLIPPED);
        }
        await base.remove_wound(ignore_limb, replaced, destroying);
    }

    // tg: code/datums/wounds/cranial_fissure.dm:65
    [SignalHandler]
    public virtual void on_owner_slipped(mob source = null)
    {
        if (source.stat == DEAD)
            return;

        obj_item_organ_brain brain = source.get_organ_by_type(typeof(obj_item_organ_brain)) as obj_item_organ_brain;
        if (brain == null)
            return;

        Waiting.NoSleep(brain.Remove(source as mob_living_carbon));

        turf source_turf = get_turf(source);
        Waiting.NoSleep(brain.forceMove(source_turf));
        Waiting.Detach(brain.throw_at(get_step(source_turf, source.dir) as atom, 1, 1));

        source.visible_message(
            span_boldwarning(I18n.T("[]'s brain spills right out of [] head!", source, source.p_their())),
            span_userdanger(I18n.T("Your brain spills right out of your head!")));
    }

    // tg: code/datums/wounds/cranial_fissure.dm:86
    public override async DmTask<object> try_handling(mob_living user = null)
    {
        if (user.usable_hands <= 0 || Dm.truthy(user.combat_mode))
            return FALSE;

        if (user.hud_used?.screen_objects[HUD_MOB_ZONE_SELECTOR] != null && (!Dm.eq(user.zone_selected, BODY_ZONE_HEAD) && !Dm.eq(user.zone_selected, BODY_ZONE_PRECISE_EYES)))
            return FALSE;

        if (!Dm.eq(victim.body_position, LYING_DOWN))
            return FALSE;

        obj_item_organ_eyes eyes = victim.get_organ_by_type(typeof(obj_item_organ_eyes)) as obj_item_organ_eyes;
        if (eyes == null)
        {
            victim.balloon_alert(user, I18n.T("no eyes to take!"));
            return TRUE;
        }

        playsound(victim, Dm.resource("sound/items/handling/surgery/organ2.ogg"), 50, TRUE);
        victim.balloon_alert(user, I18n.T("pulling out eyes..."));
        user.visible_message(
            span_boldwarning(I18n.T("[] reaches inside []'s skull...", user, victim)),
            ignored_mobs: user);
        victim.show_message(
            span_userdanger(I18n.T("[] starts to pull out your eyes!", victim)),
            MSG_VISUAL,
            span_userdanger(I18n.T("An arm reaches inside your brain, and starts pulling on your eyes!")));

        if (!Dm.truthy(await do_after(user, 10 * SECONDS, victim, extra_checks: Dm.@new<datum_callback>(this, nameof(still_has_eyes), eyes))))
            return TRUE;

        await eyes.Remove(victim);
        await user.put_in_hands(eyes);

        log_combat(user, victim, "pulled out the eyes of");

        playsound(victim, Dm.resource("sound/items/handling/surgery/organ1.ogg"), 75, TRUE);
        user.visible_message(
            span_boldwarning(I18n.T("[] rips out []'s eyes!", user, victim)),
            span_boldwarning(I18n.T("You rip out []'s eyes!", victim)),
            ignored_mobs: victim);

        victim.show_message(
            span_userdanger(I18n.T("[] rips out your eyes!", user)),
            MSG_VISUAL,
            span_userdanger(I18n.T("You feel an arm yank from inside your head, as you feel something very important is missing!")));

        return TRUE;
    }

    // tg: code/datums/wounds/cranial_fissure.dm:136
    [DmPrivate]
    public virtual object still_has_eyes(obj_item_organ_eyes eyes = null)
    {

        return victim?.get_organ_by_type(typeof(obj_item_organ_eyes)) == eyes ? TRUE : FALSE;
    }
}
