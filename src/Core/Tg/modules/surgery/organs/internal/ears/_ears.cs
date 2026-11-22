// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/ears/_ears.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the ears' insertion hooks, on_life, the temporary deafness and failure translated (M2 P2, plan.md Q7: the ears
// as a type with its insert hooks and a thin body) and adjust_speech (M9 slice A: a deafened speaker shouts), the cat ears' overlay and
// the penguin ears (gaps P3b: the cat and penguin infusions); the cybernetic cat ears' overlay (gaps SP1). The scanner texts are M9.

// tg: code/modules/surgery/organs/internal/ears/_ears.dm:1
[DmPath("/obj/item/organ/ears")]
public partial class obj_item_organ_ears : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:2-16
    public obj_item_organ_ears()
    {
        name = "ears";
        icon_state = "ears";
        desc = "There are three parts to the ear. Inner, middle and outer. Only one of these parts should be normally visible.";
        zone = BODY_ZONE_HEAD;
        slot = ORGAN_SLOT_EARS;
        gender = PLURAL;

        healing_factor = STANDARD_ORGAN_HEALING;
        decay_factor = STANDARD_ORGAN_DECAY;

        low_threshold_passed = span_info("Your ears begin to resonate with an internal ring sometimes.");
        now_failing = span_warning("You are unable to hear at all!");
        now_fixed = span_info("Noise slowly begins filling your ears once more.");
        low_threshold_cleared = span_info("The ringing in your ears has died down.");
        visual = FALSE;
    }

    /// temporary deafness, measured in seconds. While > 0, the person is unable to hear anything.
    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:19
    public float temporary_deafness = 0;

    // `damage` in this case measures long term damage to the ears, if too high,
    // the person will not have either `deaf` or `ear_damage` decrease
    // without external aid (earmuffs, drugs)

    /// Resistance against loud noises
    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:26
    public float bang_protect = EAR_PROTECTION_NONE;
    /// Multiplier for both long term and short term ear damage
    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:28
    public float damage_multiplier = 1;
}

// tg: code/modules/surgery/organs/internal/ears/_ears.dm:30 (interleaved types, rules 1.9)
public partial class obj_item_organ_ears : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:30
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        // only inform when things got worse, needs to happen before we heal
        if ((Dm.num(damage) > low_threshold && prev_damage < low_threshold) || (Dm.num(damage) > high_threshold && prev_damage < high_threshold))
            to_chat(owner, span_warning(I18n.T("The ringing in your ears grows louder, blocking out any external noises for a moment.")));

        await base.on_life(seconds_per_tick);
        // if we have non-damage related deafness like mutations, quirks or clothing (earmuffs), don't bother processing here.
        // Ear healing from earmuffs or chems happen elsewhere
        if (HAS_TRAIT_NOT_FROM(owner, TRAIT_DEAF, EAR_DAMAGE))
            return;
        // no healing if failing
        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
            return;
        adjust_temporary_deafness(-Dm.num(seconds_per_tick) * SECONDS);
        if ((Dm.num(damage) > low_threshold) && SPT_PROB(Dm.num(damage) / 60, seconds_per_tick))
        {
            adjust_temporary_deafness(4 * SECONDS);
            Dm.output_to(owner, sound(Dm.resource("sound/items/weapons/flash_ring.ogg"))); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
        }
    }

    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:48
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = null;
        dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        if (Dm.truthy(temporary_deafness))
            on_deafened();
        REMOVE_TRAIT(organ_owner, TRAIT_DEAF, NO_EARS);
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:54
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        if (Dm.truthy(temporary_deafness))
            on_undeafened(organ_owner as mob_living);
        // Do not apply with special flag, even if it would ultimately be redundant by new ears being hot-swapped in.
        // This is so we don't trip signal_addtrait when hot-swapping ears, which could cause inappropriate behavior like nuking sound effects.
        if (!Dm.truthy(special))
            ADD_TRAIT(organ_owner, TRAIT_DEAF, NO_EARS);
    }
}

// tg: code/modules/surgery/organs/internal/ears/_ears.dm:80
public partial class obj_item_organ_ears : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:80
    public virtual void adjust_temporary_deafness(object amount = null)
    {
        // organ failure makes us permanently deafened. Also, doesn't do anything if not in someone or during godmode
        if (Dm.gt(amount, 0) && owner != null && HAS_TRAIT(owner, TRAIT_GODMODE))
            return;

        temporary_deafness = max(temporary_deafness + Dm.num(amount) * damage_multiplier, 0);

        if (owner == null)
            return;

        if (Dm.truthy(temporary_deafness) && !HAS_TRAIT_FROM(owner, TRAIT_DEAF, EAR_DAMAGE))
            on_deafened();
        else if (!Dm.truthy(temporary_deafness) && HAS_TRAIT_FROM(owner, TRAIT_DEAF, EAR_DAMAGE))
            on_undeafened();
    }

    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:96
    public virtual void on_deafened()
    {
        RegisterSignal(owner, COMSIG_MOB_SAY, nameof(adjust_speech));
        ADD_TRAIT(owner, TRAIT_DEAF, EAR_DAMAGE);
    }

    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:101
    public virtual void on_undeafened(mob_living organ_owner = null)
    {
        organ_owner ??= owner;
        REMOVE_TRAIT(organ_owner, TRAIT_DEAF, EAR_DAMAGE);
        UnregisterSignal(organ_owner, COMSIG_MOB_SAY);
    }

    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:112
    /// Being deafened by loud noises makes you shout
    [SignalHandler]
    public virtual object adjust_speech(datum source = null, DmList speech_args = null)
    {
        if (HAS_TRAIT_NOT_FROM(owner, TRAIT_DEAF, EAR_DAMAGE) || HAS_TRAIT(owner, TRAIT_SIGN_LANG))
            return null;

        object message = speech_args[SPEECH_MESSAGE];
        // Replace only end-of-sentence punctuation with exclamation marks (hence the empty space)
        // We don't wanna mess with things like ellipses
        message = replacetext(message, ". ", "! ");
        message = replacetext(message, "? ", "?! ");
        // Special case for the last character
        switch (copytext_char(message, -1))
        {
            case ".":
                if (!Dm.eq(copytext_char(message, -2), "..")) // Once again ignoring ellipses, let people trail off
                    message = Dm.add(copytext_char(message, 1, -1), "!");
                break;
            case "?":
                message = Dm.add(copytext_char(message, 1, -1), "?!");
                break;
            case "!":
                break;
            default:
                message = Dm.add(message, "!");
                break;
        }

        speech_args[SPEECH_MESSAGE] = message;
        return COMPONENT_UPPERCASE_SPEECH;
    }
}

// tg: code/modules/surgery/organs/internal/ears/_ears.dm:105 (interleaved types, rules 1.9)
public partial class obj_item_organ_ears : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:105
    public override async DmTask on_begin_failure()
    {
        add_organ_trait(TRAIT_DEAF);
    }

    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:108
    public override void on_failure_recovery()
    {
        remove_organ_trait(TRAIT_DEAF);
    }
}

// tg: code/modules/surgery/organs/internal/ears/_ears.dm:138 (M6 slice 2: the self-check's organ lines)
public partial class obj_item_organ_ears : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:138
    public override object feel_for_damage(object self_aware = null)
    {
        // Ear damage has audible effects, so we don't really need to "feel" it when self-examining
        return "";
    }
}

// tg: code/modules/surgery/organs/internal/ears/_ears.dm:63 (interleaved types, rules 1.9; M8 slice D)
public partial class obj_item_organ_ears : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:63
    public override object get_status_appendix(object scanpower = null, object add_tooltips = null)
    {
        if (owner.stat == DEAD || !HAS_TRAIT(owner, TRAIT_DEAF))
            return null;
        if (Dm.num(scanpower) >= SCANPOWER_ADVANCED)
        {
            if (HAS_TRAIT_FROM(owner, TRAIT_DEAF, QUIRK_TRAIT))
                return conditional_tooltip(I18n.T("Subject is permanently deaf."), I18n.T("Irreparable under normal circumstances."), add_tooltips);
            if (HAS_TRAIT_FROM(owner, TRAIT_DEAF, GENETIC_MUTATION))
                return conditional_tooltip(I18n.T("Subject is genetically deaf."), I18n.T("Use medication such as [].", Dm.initial(typeof(datum_reagent_medicine_mutadone), (datum_reagent_medicine_mutadone r) => r.name)), add_tooltips);
            if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)) || HAS_TRAIT_FROM(owner, TRAIT_DEAF, EAR_DAMAGE))
                return conditional_tooltip(I18n.T("Subject is [] deaf from ear damage.", Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)) ? I18n.T("permanently") : I18n.T("temporarily")), I18n.T("Repair surgically, use medication such as [], or protect ears with earmuffs.", Dm.initial(typeof(datum_reagent_medicine_inacusiate), (datum_reagent_medicine_inacusiate r) => r.name)), add_tooltips);
        }
        return I18n.T("Subject is deaf.");
    }

    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:75
    public override object show_on_condensed_scans()
    {
        // Always show if we have an appendix
        object dot = base.show_on_condensed_scans();
        return Dm.truthy(dot) ? dot : Dm.num(owner.stat != DEAD && HAS_TRAIT(owner, TRAIT_DEAF));
    }
}

/// Bodypart overlay for the horrible cat ears
// tg: code/modules/surgery/organs/internal/ears/_ears.dm:161 (gaps P3b: the cat infusion)
public partial class datum_bodypart_overlay_mutant_cat_ears : datum_bodypart_overlay_mutant
{
    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:173
    public override object can_draw_on_bodypart(obj_item_bodypart bodypart_owner = null, mob_living_carbon owner = null)
    {
        return Dm.truthy(base.can_draw_on_bodypart(bodypart_owner, owner)) && !Dm.truthy(Dm.bitand(bodypart_owner.owner?.obscured_slots, HIDEHAIR)) ? TRUE : FALSE;
    }

    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:176
    public override object get_image(obj_item_bodypart limb = null, object layer_index = null, object layer_real = null)
    {
        mutable_appearance base_ears = base.get_image(limb, layer_index, layer_real) as mutable_appearance;
        base_ears.color = Dm.truthy(dye_color) ? dye_color : draw_color;

        // Only add inner ears on the inner layer
        if (!Dm.eq(layer_index, inner_layer))
            return base_ears;

        // Construct image of inner ears, apply to base ears as an overlay
        feature_key += "inner";
        mutable_appearance inner_ears = base.get_image(limb, layer_index, layer_real) as mutable_appearance;
        feature_key = Dm.initial(this, (datum_bodypart_overlay_mutant p) => p.feature_key);
        mutable_appearance ear_holder = mutable_appearance(layer: Dm.num(layer_real)) as mutable_appearance;
        Dm.append(ref ear_holder.overlays, base_ears);
        Dm.append(ref ear_holder.overlays, inner_ears);
        return ear_holder;
    }

    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:194
    public override async DmTask color_image(image overlay = null, obj_item_bodypart limb = null, object layer_index = null)
    {
        return; // We color base ears manually above in get_image
    }
}

// tg: code/modules/surgery/organs/internal/ears/_ears.dm:236 (gaps SP1: the cybernetic cat ears' coloured inner ear)
public partial class datum_bodypart_overlay_mutant_cat_ears_cybernetic : datum_bodypart_overlay_mutant_cat_ears
{
    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:242
    public override object get_image(obj_item_bodypart limb = null, object layer_index = null, object layer_real = null)
    {
        if (!Dm.eq(layer_index, inner_layer))
            return base.get_image(limb, layer_index, layer_real);
        mutable_appearance ear_holder = base.get_image(limb, layer_index, layer_real) as mutable_appearance;
        mutable_appearance inner = ear_holder.overlays[2] as mutable_appearance;
        inner.color = inner_color;
        return ear_holder;
    }

    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:250
    public override async DmTask<object> get_overlay(obj_item_bodypart limb = null, object layer_index = null, object layer_real = null)
    {
        if (!Dm.eq(layer_index, inner_layer))
            return await base.get_overlay(limb, layer_index, layer_real);
        DmList all_images = await base.get_overlay(limb, layer_index, layer_real) as DmList;
        // tg: :255-257  the inner ear's emissive overlay (LIMB_OVERLAY_META): DEVIATION(DV12), emissives are not ported
        return all_images;
    }
}

// tg: code/modules/surgery/organs/internal/ears/_ears.dm:280 (gaps P3b: the penguin infusion)
public partial class obj_item_organ_ears_penguin : obj_item_organ_ears
{
    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:284
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon_human ear_owner = organ_owner as mob_living_carbon_human;
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        to_chat(ear_owner, span_notice(I18n.T("You suddenly feel like you've lost your balance.")));
        await ear_owner.AddElementTrait(TRAIT_WADDLING, ORGAN_TRAIT, typeof(datum_element_waddling));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:289
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon_human ear_owner = organ_owner as mob_living_carbon_human;
        await base.on_mob_remove(organ_owner, special, movement_flags);
        to_chat(ear_owner, span_notice(I18n.T("Your sense of balance comes back to you.")));
        REMOVE_TRAIT(ear_owner, TRAIT_WADDLING, ORGAN_TRAIT);
    }
}

// tg: code/modules/surgery/organs/internal/ears/_ears.dm:294
public partial class obj_item_organ_ears_cybernetic : obj_item_organ_ears
{
    // tg: code/modules/surgery/organs/internal/ears/_ears.dm:339  (silicons SI3: the exosuit fabricator prints it)
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        await apply_organ_damage(20 / Dm.num(severity));
        return dot;
    }
}
