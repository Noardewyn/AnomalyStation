// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/kneecapping.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps WP1 (D333): the file whole, with the baseball bat (weaponry/melee/baseball_bat.dm). A weapon's right-button swing aimed at
// a leg of a carbon that is down, held or buckled takes three seconds and lands on that leg with a wound bonus between the leg's
// severe and critical blunt thresholds (plus ten): a scream, tg's hit sound. The var block and the element flags are generated
// (D111).

/**
 * Kneecapping element replaces the item's secondary attack with an aimed attack at the kneecaps under certain circumstances.
 *
 * Element is incompatible with non-items. Requires the parent item to have a force equal to or greater than WOUND_MINIMUM_DAMAGE.
 * Also requires that the parent can actually get past pre_secondary_attack without the attack chain cancelling.
 *
 * Kneecapping attacks have a wounding bonus between severe and critical+10 wound thresholds. Without some serious wound protecting
 * armour this all but guarantees a wound of some sort. The attack is directed specifically at a limb and the limb takes the damage.
 *
 * Requires the attacker to be aiming for either leg zone, which will be targeted specifically. They will than have a 3-second long
 * do_after before executing the attack.
 *
 * Kneecapping requires the target to either be on the floor, immobilised or buckled to something. And also to have an appropriate leg.
 *
 * Passing all the checks will cancel the entire attack chain.
 */
// tg: code/datums/elements/kneecapping.dm:18
public partial class datum_element_kneecapping : datum_element
{
    // tg: code/datums/elements/kneecapping.dm:20
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        if (!isitem(target))
        {
            stack_trace("Kneecapping element added to non-item object: [" + Dm.text("[]", target) + "]", "code/datums/elements/kneecapping.dm", 22);
            return ELEMENT_INCOMPATIBLE;
        }

        obj_item target_item = (obj_item)target;
        if (target_item.force < WOUND_MINIMUM_DAMAGE)
        {
            stack_trace("Kneecapping element added to item with too little force to wound: [" + Dm.text("[]", target) + "]", "code/datums/elements/kneecapping.dm", 28);
            return ELEMENT_INCOMPATIBLE;
        }

        object dot = await base.Attach(target, __extra);
        if (Dm.eq(dot, ELEMENT_INCOMPATIBLE))
            return dot;

        RegisterSignal(target, COMSIG_ITEM_ATTACK_SECONDARY, nameof(try_kneecap_target));
        return dot;
    }

    // tg: code/datums/elements/kneecapping.dm:38
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, COMSIG_ITEM_ATTACK_SECONDARY);
        return base.Detach(source, __extra);
    }

    /**
     * Signal handler for COMSIG_ITEM_ATTACK_SECONDARY. Does checks for pacifism, zones and target state before either returning nothing
     * if the special attack could not be attempted, performing the ordinary attack procs instead - Or cancelling the attack chain if
     * the attack can be started.
     */
    // tg: code/datums/elements/kneecapping.dm:47
    [SignalHandler]
    public virtual object try_kneecap_target(obj_item source = null, mob_living_carbon target = null, mob attacker = null, DmList modifiers = null)
    {
        if (!Dm.eq(attacker.zone_selected, BODY_ZONE_L_LEG) && !Dm.eq(attacker.zone_selected, BODY_ZONE_R_LEG))
            return null;

        if (HAS_TRAIT(attacker, TRAIT_PACIFISM))
            return null;

        if (!iscarbon(target))
            return null;

        if (target.buckled == null && !HAS_TRAIT(target, TRAIT_FLOORED) && !HAS_TRAIT(target, TRAIT_IMMOBILIZED))
            return null;

        obj_item_bodypart leg = target.get_bodypart(attacker.zone_selected) as obj_item_bodypart;

        if (leg == null)
            return null;

        Waiting.Detach(do_kneecap_target(source, leg, target, attacker));
        return COMPONENT_SECONDARY_CANCEL_ATTACK_CHAIN;
    }

    /**
     * After a short do_after, attacker applies damage to the given leg with a significant wounding bonus, applying the weapon's force as damage.
     */
    // tg: code/datums/elements/kneecapping.dm:74
    public virtual async DmTask do_kneecap_target(obj_item weapon = null, obj_item_bodypart leg = null, mob_living_carbon target = null, mob attacker = null)
    {
        if (Dm.truthy(LAZYACCESS(attacker.do_afters, weapon)))
            return;

        attacker.visible_message(span_warning(I18n.T("[] carefully aims [] [] for a swing at []'s kneecaps!", attacker, attacker.p_their(), weapon, target)), span_danger(I18n.T("You carefully aim \\the [] for a swing at []'s kneecaps!", weapon, target)));
        log_combat(attacker, target, "started aiming a swing to break the kneecaps of", weapon);

        if (!Dm.truthy(await do_after(attacker, 3 * SECONDS, target, interaction_key: weapon)))
            return;

        attacker.visible_message(span_warning(I18n.T("[] swings [] [] at []'s kneecaps!", attacker, attacker.p_their(), weapon, target)), span_danger(I18n.T("You swing \\the [] at []'s kneecaps!", weapon, target)));

        float min_wound = Dm.num(leg.get_wound_threshold_of_wound_type(WOUND_BLUNT, WOUND_SEVERITY_SEVERE, return_value_if_no_wound: 30, wound_source: weapon));
        float max_wound = Dm.num(leg.get_wound_threshold_of_wound_type(WOUND_BLUNT, WOUND_SEVERITY_CRITICAL, return_value_if_no_wound: 50, wound_source: weapon));

        await target.apply_damage(weapon.force, weapon.damtype, leg, wound_bonus: rand(min_wound, max_wound + 10), attacking_item: weapon);
        await target.emote("scream");
        log_combat(attacker, target, "broke the kneecaps of", weapon);
        await attacker.do_attack_animation(target, used_item: weapon);
        playsound(source: weapon, soundin: weapon.hitsound, vol: weapon.get_clamped_volume(), vary: TRUE);
    }
}
