// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/dna_infuser/organ_sets/roach_organs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3b, D327): the roach's infused organs (the shelled heart that halves blows to the back, with its shell overlay,
// the disgust-proof stomach, the toxin-tolerant but fragile liver, the appendix that cannot inflame) and their set bonus, the inner
// armour against blasts and viruses. The colour defines are file-local; the var blocks are generated (D111).

// tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:9
public partial class datum_status_effect_organ_set_bonus_roach : datum_status_effect_organ_set_bonus
{
    // tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:23
    public override async DmTask<object> enable_bonus(obj_item_organ inserted_organ = null, object visuals_only = null)
    {
        object dot = await base.enable_bonus(inserted_organ, visuals_only);
        owner.add_inner_armor(given_armor);
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:27
    public override async DmTask disable_bonus(obj_item_organ removed_organ = null, object visuals_only = null)
    {
        await base.disable_bonus(removed_organ, visuals_only);
        if (QDELETED(owner))
            return;

        owner.remove_inner_armor(given_armor);
    }
}

/// Roach heart:
/// Reduces damage taken from brute attacks from behind,
/// but increases duration of knockdowns
// tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:37
public partial class obj_item_organ_heart_roach : obj_item_organ_heart
{
    // tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:53
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_noticable_organ), I18n.T("%PRONOUN_They %PRONOUN_have hardened, somewhat translucent skin.")));
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_roach)));
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_blocker)));
        roach_shell = Dm.@new<datum_bodypart_overlay_simple_roach_shell>();
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:60
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref roach_shell);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:64
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        if (!ishuman(organ_owner))
            return dot;

        mob_living_carbon_human human_owner = organ_owner as mob_living_carbon_human;

        RegisterSignal(human_owner, COMSIG_MOB_APPLY_DAMAGE_MODIFIERS, nameof(modify_damage));
        RegisterSignal(human_owner, COMSIG_MOB_AFTER_APPLY_DAMAGE, nameof(do_block_effect));
        MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_KNOCKDOWN, 3);
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:75
    public override async DmTask on_bodypart_insert(obj_item_bodypart limb = null)
    {
        await base.on_bodypart_insert(limb);
        await limb.add_bodypart_overlay(roach_shell);
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:79
    public override async DmTask on_mob_remove(object organ_owner__arg = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon organ_owner = organ_owner__arg as mob_living_carbon;
        await base.on_mob_remove(organ_owner__arg, special, movement_flags);
        if (QDELETED(organ_owner))
            return;

        UnregisterSignal(organ_owner, Dm.list(COMSIG_MOB_APPLY_DAMAGE_MODIFIERS, COMSIG_MOB_AFTER_APPLY_DAMAGE));
        MODIFY_PHYSIOLOGY(organ_owner, PHYS_COEFF_KNOCKDOWN, 1f / 3);
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:87
    public override async DmTask on_bodypart_remove(obj_item_bodypart limb = null, object movement_flags = null)
    {
        await base.on_bodypart_remove(limb, movement_flags);

        await limb.remove_bodypart_overlay(roach_shell);
    }

    /**
     * Signal proc for [COMSIG_MOB_APPLY_DAMAGE_MODIFIERS]
     *
     * Adds a 0.5 modifier to attacks from the back
     */
    // tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:97
    [SignalHandler]
    public virtual void modify_damage(mob_living_carbon_human source = null, DmList damage_mods = null, object damage_amount = null, object damagetype = null, object def_zone = null, object sharpness = null, object attack_direction = null, obj_item attacking_item = null)
    {
        if (!Dm.truthy(is_blocking(source, damage_amount, damagetype, attack_direction)))
            return;

        Dm.append(ref damage_mods, 0.5f);
    }

    /**
     * Signal proc for [COMSIG_MOB_AFTER_APPLY_DAMAGE]
     *
     * Does a special effect if we blocked damage with our back
     */
    // tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:110
    [SignalHandler]
    public virtual void do_block_effect(mob_living_carbon_human source = null, object damage_dealt = null, object damagetype = null, object def_zone = null, object blocked = null, object wound_bonus = null, object exposed_wound_bonus = null, object sharpness = null, object attack_direction = null, obj_item attacking_item = null)
    {
        if (!Dm.truthy(is_blocking(source, damage_dealt, damagetype, attack_direction)))
            return;

        if (harden_effect_cd <= world.time) // COOLDOWN_FINISHED, inline (rules 11.1 (a))
        {
            source.visible_message(span_warning(I18n.T("[]'s back hardens against the blow!", source)));
            playsound(source, "sound/effects/constructform.ogg", 25, vary: TRUE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
        }

        harden_effect_cd = world.time + 5 * SECONDS; // Cooldown resets EVERY time we get hit
    }

    /// Checks if the passed mob is in a valid state to be blocking damage with the roach shell
    // tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:124
    public virtual object is_blocking(mob_living_carbon_human blocker = null, object damage_amount = null, object damagetype = null, object attack_direction = null)
    {
        if (Dm.num(damage_amount) < 5 || !Dm.eq(damagetype, BRUTE) || !Dm.truthy(attack_direction))
            return null;
        if (!ishuman(blocker) || IS_UNCONSCIOUS(blocker))
            return FALSE;
        // No tactical spinning
        if (HAS_TRAIT(blocker, TRAIT_SPINNING))
            return FALSE;
        if (blocker.body_position == LYING_DOWN || Dm.truthy(Dm.bitand(blocker.dir, attack_direction)))
            return TRUE;
        return FALSE;
    }
}

// Simple overlay so we can add a roach shell to guys with roach hearts
// tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:137
public partial class datum_bodypart_overlay_simple_roach_shell : datum_bodypart_overlay_simple
{
    // tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:146
    public override object get_image(obj_item_bodypart limb = null, object layer_index = null, object layer_real = null)
    {
        return image(icon: icon, icon_state: Dm.text("[]_[]", icon_state, layer_index), layer: layer_real);
    }
}

/// Roach stomach:
/// Makes disgust a non-issue, very slightly worse at passing off reagents
/// Also makes you more hungry
// tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:156
public partial class obj_item_organ_stomach_roach : obj_item_organ_stomach
{
    // tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:170
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_roach)));
        return dot;
    }
}

/// Roach liver:
/// Purges toxins at a higher threshold, but takes more damage from them if not purged
// tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:176
public partial class obj_item_organ_liver_roach : obj_item_organ_liver
{
    // tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:189
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_roach)));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:193
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        MODIFY_PHYSIOLOGY(owner, TOX, 2);
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:197
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        if (QDELETED(organ_owner))
            return;

        MODIFY_PHYSIOLOGY(organ_owner, TOX, 0.5f);
    }
}

/// Roach appendix:
/// No appendicitus! weee!
// tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:206
public partial class obj_item_organ_appendix_roach : obj_item_organ_appendix
{
    // tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:217
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_roach)));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/roach_organs.dm:221
    public override void become_inflamed()
    {
    }
}
