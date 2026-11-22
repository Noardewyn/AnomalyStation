// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_bioware.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated. The cortex folding's two on_success definitions are kept
// as tg has them: the later one's ..() calls the earlier (on_success__d0, rules 2.5).

// tg: code/modules/surgery/operations/operation_bioware.dm:1
public partial class datum_surgery_operation_limb_bioware : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_bioware.dm:16
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/hud/implants.dmi"), "lighting_bolt");
    }

    // tg: code/modules/surgery/operations/operation_bioware.dm:19
    public override object all_required_strings()
    {
        return Dm.add(Dm.list(I18n.T("operate on [] (target [])", parse_zone(required_zone), parse_zone(required_zone))), base.all_required_strings());
    }

    // tg: code/modules/surgery/operations/operation_bioware.dm:22
    public override object all_blocked_strings()
    {
        DmList incompatible_surgeries = Dm.list();
        foreach (Type other_bioware in Dm.each_cast<Type>(subtypesof(typeof(datum_surgery_operation_limb_bioware))))
        {
            Type other_effect = Dm.initial(other_bioware, (datum_surgery_operation_limb_bioware o) => o.status_effect_gained);
            if (!Dm.eq(Dm.initial(other_effect, (datum_status_effect o) => o.id), Dm.initial(status_effect_gained, (datum_status_effect o) => o.id)))
                continue;
            if (!Dm.eq(Dm.initial(other_bioware, (datum_surgery_operation_limb_bioware o) => o.required_bodytype), required_bodytype))
                continue;
            object other_rnd_name = Dm.initial(other_bioware, (datum_surgery_operation o) => o.rnd_name);
            Dm.append(ref incompatible_surgeries, Dm.truthy(other_rnd_name) ? other_rnd_name : Dm.initial(other_bioware, (datum_surgery_operation o) => o.name));
        }

        return Dm.add(base.all_blocked_strings(), Dm.list(I18n.T("the patient must not have undergone [] prior", english_list(incompatible_surgeries, and_text: I18n.T(" OR ")))));
    }

    // tg: code/modules/surgery/operations/operation_bioware.dm:33
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        if (!Dm.eq(limb.body_zone, required_zone))
            return FALSE;
        if (Dm.truthy(limb.owner.has_status_effect(status_effect_gained)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_bioware.dm:40
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        await limb.owner.apply_status_effect(status_effect_gained);
        if (Dm.truthy(limb.owner.ckey))
            SSblackbox.record_feedback("tally", "bioware", 1, status_effect_gained);
        return null;
    }
}

// tg: code/modules/surgery/operations/operation_bioware.dm:45
public partial class datum_surgery_operation_limb_bioware_vein_threading : datum_surgery_operation_limb_bioware
{
    // tg: code/modules/surgery/operations/operation_bioware.dm:51
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You start weaving []'s blood vessels.", limb.owner)),
            span_notice(I18n.T("[] starts weaving []'s blood vessels.", surgeon, limb.owner)),
            span_notice(I18n.T("[] starts manipulating []'s blood vessels.", surgeon, limb.owner)));
        await display_pain(limb.owner, I18n.T("Your entire body burns in agony!"));
    }

    // tg: code/modules/surgery/operations/operation_bioware.dm:61
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You weave []'s blood vessels into a resistant mesh!", limb.owner)),
            span_notice(I18n.T("[] weaves []'s blood vessels into a resistant mesh!", surgeon, limb.owner)),
            span_notice(I18n.T("[] finishes manipulating []'s blood vessels.", surgeon, limb.owner)));
        await display_pain(limb.owner, I18n.T("You can feel your blood pumping through reinforced veins!"));
        return dot;
    }
}

// tg: code/modules/surgery/operations/operation_bioware.dm:78
public partial class datum_surgery_operation_limb_bioware_muscled_veins : datum_surgery_operation_limb_bioware
{
    // tg: code/modules/surgery/operations/operation_bioware.dm:84
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You start wrapping muscles around []'s blood vessels.", limb.owner)),
            span_notice(I18n.T("[] starts wrapping muscles around []'s blood vessels.", surgeon, limb.owner)),
            span_notice(I18n.T("[] starts manipulating []'s blood vessels.", surgeon, limb.owner)));
        await display_pain(limb.owner, I18n.T("Your entire body burns in agony!"));
    }

    // tg: code/modules/surgery/operations/operation_bioware.dm:94
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You reshape []'s blood vessels, adding a muscled membrane!", limb.owner)),
            span_notice(I18n.T("[] reshapes []'s blood vessels, adding a muscled membrane!", surgeon, limb.owner)),
            span_notice(I18n.T("[] finishes manipulating []'s blood vessels.", surgeon, limb.owner)));
        await display_pain(limb.owner, I18n.T("You can feel your heartbeat's powerful pulses ripple through your body!"));
        return dot;
    }
}

// tg: code/modules/surgery/operations/operation_bioware.dm:111
public partial class datum_surgery_operation_limb_bioware_nerve_splicing : datum_surgery_operation_limb_bioware
{
    // tg: code/modules/surgery/operations/operation_bioware.dm:118
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You start splicing together []'s nerves.", limb.owner)),
            span_notice(I18n.T("[] starts splicing together []'s nerves.", surgeon, limb.owner)),
            span_notice(I18n.T("[] starts manipulating []'s nervous system.", surgeon, limb.owner)));
        await display_pain(limb.owner, I18n.T("Your entire body goes numb!"));
    }

    // tg: code/modules/surgery/operations/operation_bioware.dm:128
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You successfully splice []'s nervous system!", limb.owner)),
            span_notice(I18n.T("[] successfully splices []'s nervous system!", surgeon, limb.owner)),
            span_notice(I18n.T("[] finishes manipulating []'s nervous system.", surgeon, limb.owner)));
        await display_pain(limb.owner, I18n.T("You regain feeling in your body; It feels like everything's happening around you in slow motion!"));
        return dot;
    }
}

// tg: code/modules/surgery/operations/operation_bioware.dm:145
public partial class datum_surgery_operation_limb_bioware_nerve_grounding : datum_surgery_operation_limb_bioware
{
    // tg: code/modules/surgery/operations/operation_bioware.dm:152
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You start rerouting []'s nerves.", limb.owner)),
            span_notice(I18n.T("[] starts rerouting []'s nerves.", surgeon, limb.owner)),
            span_notice(I18n.T("[] starts manipulating []'s nervous system.", surgeon, limb.owner)));
        await display_pain(limb.owner, I18n.T("Your entire body goes numb!"));
    }

    // tg: code/modules/surgery/operations/operation_bioware.dm:162
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You successfully reroute []'s nervous system!", limb.owner)),
            span_notice(I18n.T("[] successfully reroutes []'s nervous system!", surgeon, limb.owner)),
            span_notice(I18n.T("[] finishes manipulating []'s nervous system.", surgeon, limb.owner)));
        await display_pain(limb.owner, I18n.T("You regain feeling in your body! You feel energized!"));
        return dot;
    }
}

// tg: code/modules/surgery/operations/operation_bioware.dm:179
public partial class datum_surgery_operation_limb_bioware_ligament_hook : datum_surgery_operation_limb_bioware
{
    // tg: code/modules/surgery/operations/operation_bioware.dm:185
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You start reshaping []'s ligaments into a hook-like shape.", limb.owner)),
            span_notice(I18n.T("[] starts reshaping []'s ligaments into a hook-like shape.", surgeon, limb.owner)),
            span_notice(I18n.T("[] starts manipulating []'s ligaments.", surgeon, limb.owner)));
        await display_pain(limb.owner, I18n.T("Your limbs burn with severe pain!"));
    }

    // tg: code/modules/surgery/operations/operation_bioware.dm:195
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You reshape []'s ligaments into a connective hook!", limb.owner)),
            span_notice(I18n.T("[] reshapes []'s ligaments into a connective hook!", surgeon, limb.owner)),
            span_notice(I18n.T("[] finishes manipulating []'s ligaments.", surgeon, limb.owner)));
        await display_pain(limb.owner, I18n.T("Your limbs feel... strangely loose."));
        return dot;
    }
}

// tg: code/modules/surgery/operations/operation_bioware.dm:213
public partial class datum_surgery_operation_limb_bioware_ligament_reinforcement : datum_surgery_operation_limb_bioware
{
    // tg: code/modules/surgery/operations/operation_bioware.dm:219
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You start reinforcing []'s ligaments.", limb.owner)),
            span_notice(I18n.T("[] starts reinforcing []'s ligaments.", surgeon, limb.owner)),
            span_notice(I18n.T("[] starts manipulating []'s ligaments.", surgeon, limb.owner)));
        await display_pain(limb.owner, I18n.T("Your limbs burn with severe pain!"));
    }

    // tg: code/modules/surgery/operations/operation_bioware.dm:229
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You reinforce []'s ligaments!", limb.owner)),
            span_notice(I18n.T("[] reinforces []'s ligaments!", surgeon, limb.owner)),
            span_notice(I18n.T("[] finishes manipulating []'s ligaments.", surgeon, limb.owner)));
        await display_pain(limb.owner, I18n.T("Your limbs feel more secure, but also more frail."));
        return dot;
    }
}

// tg: code/modules/surgery/operations/operation_bioware.dm:246
public partial class datum_surgery_operation_limb_bioware_cortex_folding : datum_surgery_operation_limb_bioware
{
    // tg: code/modules/surgery/operations/operation_bioware.dm:254
    public override object state_check(atom_movable operating_on = null)
    {
        object dot = base.state_check(operating_on);
        if (!Dm.truthy(dot))
            return dot;
        obj_item_organ_brain brain = Dm.locate<obj_item_organ_brain>(operating_on);
        if (brain == null)
            return FALSE;
        return Dm.num(!HAS_TRAIT_FROM(brain, TRAIT_SPECIAL_TRAUMA_BOOST, BIOWARE_TRAIT));
    }

    // tg: code/modules/surgery/operations/operation_bioware.dm:263  (the earlier of the type's two on_success definitions, rules 2.5)
    public async DmTask<object> on_success__d0(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        obj_item_organ_brain brain = Dm.locate<obj_item_organ_brain>(operating_on);
        if (brain != null)
            ADD_TRAIT(brain, TRAIT_SPECIAL_TRAUMA_BOOST, BIOWARE_TRAIT);
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_bioware.dm:269
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You start folding []'s cerebral cortex.", limb.owner)),
            span_notice(I18n.T("[] starts folding []'s cerebral cortex.", surgeon, limb.owner)),
            span_notice(I18n.T("[] starts performing surgery on []'s brain.", surgeon, limb.owner)));
        await display_pain(limb.owner, I18n.T("Your head throbs with gruesome pain, it's nearly too much to handle!"));
    }

    // tg: code/modules/surgery/operations/operation_bioware.dm:279
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await on_success__d0(operating_on, surgeon, tool, operation_args, __extra);
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You fold []'s cerebral cortex into a fractal pattern!", limb.owner)),
            span_notice(I18n.T("[] folds []'s cerebral cortex into a fractal pattern!", surgeon, limb.owner)),
            span_notice(I18n.T("[] completes the surgery on []'s brain.", surgeon, limb.owner)));
        await display_pain(limb.owner, I18n.T("Your brain feels stronger... and more flexible!"));
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_bioware.dm:290
    public override async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        obj_item_organ_brain brain = Dm.locate<obj_item_organ_brain>(limb);
        if (brain == null)
        {
            await base.on_failure(operating_on, surgeon, tool, operation_args);
            return;
        }
        display_results(
            surgeon,
            limb.owner,
            span_warning(I18n.T("You screw up, damaging the brain!")),
            span_warning(I18n.T("[] screws up, damaging the brain!", surgeon)),
            span_notice(I18n.T("[] completes the surgery on []'s brain.", surgeon, limb.owner)));
        await display_pain(limb.owner, I18n.T("Your head throbs with excruciating pain!"));
        await brain.apply_organ_damage(60);
        await brain.gain_trauma_type(typeof(datum_brain_trauma_severe) /* BRAIN_TRAUMA_SEVERE */, TRAUMA_RESILIENCE_LOBOTOMY);
    }
}

// tg: code/modules/surgery/operations/operation_bioware.dm:311
public partial class datum_surgery_operation_limb_bioware_cortex_imprint : datum_surgery_operation_limb_bioware
{
    // tg: code/modules/surgery/operations/operation_bioware.dm:318
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You start carving []'s outer cerebral cortex into a self-imprinting pattern.", limb.owner)),
            span_notice(I18n.T("[] starts carving []'s outer cerebral cortex into a self-imprinting pattern.", surgeon, limb.owner)),
            span_notice(I18n.T("[] starts performing surgery on []'s brain.", surgeon, limb.owner)));
        await display_pain(limb.owner, I18n.T("Your head throbs with gruesome pain, it's nearly too much to handle!"));
    }

    // tg: code/modules/surgery/operations/operation_bioware.dm:328
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You reshape []'s outer cerebral cortex into a self-imprinting pattern!", limb.owner)),
            span_notice(I18n.T("[] reshapes []'s outer cerebral cortex into a self-imprinting pattern!", surgeon, limb.owner)),
            span_notice(I18n.T("[] completes the surgery on []'s brain.", surgeon, limb.owner)));
        await display_pain(limb.owner, I18n.T("Your brain feels stronger... and more resilient!"));
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_bioware.dm:339
    public override async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        if (!Dm.truthy(limb.owner.get_organ_slot(ORGAN_SLOT_BRAIN)))
        {
            await base.on_failure(operating_on, surgeon, tool, operation_args);
            return;
        }
        display_results(
            surgeon,
            limb.owner,
            span_warning(I18n.T("You screw up, damaging the brain!")),
            span_warning(I18n.T("[] screws up, damaging the brain!", surgeon)),
            span_notice(I18n.T("[] completes the surgery on []'s brain.", surgeon, limb.owner)));
        await display_pain(limb.owner, I18n.T("Your brain throbs with intense pain; Thinking hurts!"));
        await limb.owner.adjust_organ_loss(ORGAN_SLOT_BRAIN, 60);
        await limb.owner.gain_trauma_type(typeof(datum_brain_trauma_severe) /* BRAIN_TRAUMA_SEVERE */, TRAUMA_RESILIENCE_LOBOTOMY);
    }
}
