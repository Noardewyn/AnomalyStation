// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/drug_effects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M8 slice B): only the saline glucose dilution (:90-150), which the saline-glucose solution applies; gaps RG2 (D339): the
// woozy, the high blood pressure and the seizure (:1-48), which the stimulant, opioid and alcohol withdrawals apply. The drugs'
// own effects (the cannabis and the stoned effects) come with the drugs (gaps RG3). The var blocks and the alerts are generated.

// tg: code/datums/status_effects/drug_effects.dm:94
public partial class datum_status_effect_stacking_saline_glucose_dilution : datum_status_effect_stacking
{
    /// The amount taken away from saline's blood volume multiplier per second, from its base of 5x.
    // tg: code/datums/status_effects/drug_effects.dm:91  #define SALINE_GLUCOSE_STACK_DILUTION 0.02 (file-local, #undef at :143)
    private const float SALINE_GLUCOSE_STACK_DILUTION = 0.02f;

    // tg: code/datums/status_effects/drug_effects.dm:102
    [DmParams("new_owner", "stacks_to_apply")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object dot = await base.on_creation(new_owner, __extra);
        object blood_type = new_owner.get_bloodtype();
        if (istype(blood_type, typeof(datum_blood_type_human)) || istype(blood_type, typeof(datum_blood_type_animal)))
            return dot;
        if (istype(blood_type, typeof(datum_blood_type_lizard)))
        {
            linked_alert.name = I18n.T("Green blood cell deficiency");
            return dot;
        }
        if (istype(blood_type, typeof(datum_blood_type_clown)))
        {
            linked_alert.name = I18n.T("Potassium deficiency");
            return dot;
        }
        if (istype(blood_type, typeof(datum_blood_type_ethereal)))
        {
            linked_alert.name = I18n.T("Low specific conductance");
            return dot;
        }
        if (istype(blood_type, typeof(datum_blood_type_xeno)))
            linked_alert.name = I18n.T("Alkalosis");
        return dot;
    }

    // tg: code/datums/status_effects/drug_effects.dm:119
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (Dm.truthy(owner.has_reagent(typeof(datum_reagent_medicine_salglu_solution))))
        {
            if (owner.stat != DEAD)
                add_stacks(seconds_between_ticks);
            linked_alert.desc = Dm.initial(linked_alert, (atom a) => a.desc);
            return null;
        }
        linked_alert.desc = I18n.T("Saline-Glucose Solution was supporting your bloodstream, and you are now recovering.");
        return await base.tick(seconds_between_ticks); // Parent is stack decay, only decay while blood is pure
    }

    // tg: code/datums/status_effects/drug_effects.dm:128
    public override object can_have_status()
    {
        return stacks > 0;
    }

    // tg: code/datums/status_effects/drug_effects.dm:131
    public override object add_stacks(object stacks_added = null)
    {
        object dot = base.add_stacks(stacks_added);
        if (linked_alert == null)
            return dot;
        linked_alert.maptext = MAPTEXT_TINY_UNICODE(Dm.text("<span style='text-align:center'>[]%</span>", round(100 - (stacks / Dm.num(max_stacks) * 80), 1)));
        return dot;
    }

    /// returns a number 1 - 5 that represents how many units of blood one unit of saline is worth within the mob's bloodstream.
    // tg: code/datums/status_effects/drug_effects.dm:139
    public virtual float get_blood_multiplier()
    {
        return max(5 - (stacks * SALINE_GLUCOSE_STACK_DILUTION), 0);
    }
}

// Gaps RG2 (D339): the withdrawals' status effects.

// tg: code/datums/status_effects/drug_effects.dm:2
public partial class datum_status_effect_woozy : datum_status_effect
{
    // tg: code/datums/status_effects/drug_effects.dm:7
    public override object nextmove_modifier()
    {
        return 1.5f;
    }
}

// tg: code/datums/status_effects/drug_effects.dm:17
public partial class datum_status_effect_high_blood_pressure : datum_status_effect
{
    // tg: code/datums/status_effects/drug_effects.dm:22
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_BLEED, 1.25f);
        return TRUE;
    }

    // tg: code/datums/status_effects/drug_effects.dm:26
    public override async DmTask<object> on_remove()
    {
        MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_BLEED, 0.8f);
        return null;
    }
}

// tg: code/datums/status_effects/drug_effects.dm:36
public partial class datum_status_effect_seizure : datum_status_effect
{
    // tg: code/datums/status_effects/drug_effects.dm:41
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (!iscarbon(owner))
            return FALSE;
        float amplitude = rand(1 * SECONDS, 3 * SECONDS);
        duration = amplitude;
        await owner.set_jitter_if_lower(100 * SECONDS);
        await owner.Paralyze(duration);
        owner.visible_message(span_warning(I18n.T("[] drops to the ground as [] start[] seizing up.", owner, owner.p_they(), owner.p_s())),
            span_warning(I18n.T(Dm.str(pick("You can't collect your thoughts...", "You suddenly feel extremely dizzy...", "You can't think straight...", "You can't move your face properly anymore...")))));
        return TRUE;
    }
}

// Gaps RG3 (D339): the cannabis high.

// tg: code/datums/status_effects/drug_effects.dm:58
public partial class datum_status_effect_stoned : datum_status_effect
{
    // tg: code/datums/status_effects/drug_effects.dm:64
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (!ishuman(owner))
            return FALSE;
        var human_owner = (mob_living_carbon_human)owner;
        await human_owner.add_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_cannabis)); //slows you down
        await human_owner.add_eye_color(BLOODCULT_EYE, EYE_COLOR_WEED_PRIORITY); //makes cult eyes less obvious
        human_owner.add_traits(Dm.list(TRAIT_CLUMSY, TRAIT_BLOODSHOT_EYES), TRAIT_STATUS_EFFECT(id)); // impairs motor coordination and dilates blood vessels in eyes
        await human_owner.add_mood_event("stoned", typeof(datum_mood_event_stoned)); //improves mood
        human_owner.sound_environment_override = SOUND_ENVIRONMENT_DRUGGED; //not realistic but very immersive
        return TRUE;
    }

    // tg: code/datums/status_effects/drug_effects.dm:75
    public override async DmTask<object> on_remove()
    {
        if (!ishuman(owner))
            return null;
        var human_owner = (mob_living_carbon_human)owner;
        human_owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_cannabis));
        await human_owner.remove_eye_color(EYE_COLOR_WEED_PRIORITY);
        human_owner.remove_traits(Dm.list(TRAIT_CLUMSY, TRAIT_BLOODSHOT_EYES), TRAIT_STATUS_EFFECT(id));
        human_owner.clear_mood_event("stoned");
        human_owner.sound_environment_override = SOUND_ENVIRONMENT_NONE;
        return null;
    }
}
