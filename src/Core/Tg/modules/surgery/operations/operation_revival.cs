// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_revival.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated.

// tg: code/modules/surgery/operations/operation_revival.dm:1
public partial class datum_surgery_operation_basic_revival : datum_surgery_operation_basic
{
    // tg: code/modules/surgery/operations/operation_revival.dm:22
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_shockpaddles));
    }

    // tg: code/modules/surgery/operations/operation_revival.dm:25
    public override object all_required_strings()
    {
        return Dm.add(base.all_required_strings(), Dm.list(I18n.T("the patient must be deceased"), I18n.T("the patient must be in a revivable state")));
    }

    // tg: code/modules/surgery/operations/operation_revival.dm:28
    public override object state_check(atom_movable operating_on = null)
    {
        mob_living patient = (mob_living)operating_on;
        if (!Dm.eq(patient.stat, DEAD))
            return FALSE;
        if (HAS_TRAIT(patient, TRAIT_SUICIDED) || HAS_TRAIT(patient, TRAIT_HUSK) || HAS_TRAIT(patient, TRAIT_DEFIB_BLACKLISTED))
            return FALSE;
        if (Dm.truthy(patient.has_limbs))
        {
            obj_item_organ_brain brain = patient.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
            return Dm.num(brain != null && Dm.truthy(brain_check(brain)));
        }
        return mob_check(patient);
    }

    // tg: code/modules/surgery/operations/operation_revival.dm:38
    public virtual object brain_check(obj_item_organ_brain brain = null)
    {
        return Dm.num(!Dm.truthy(IS_ROBOTIC_ORGAN(brain)));
    }

    // tg: code/modules/surgery/operations/operation_revival.dm:41
    public virtual object mob_check(mob_living patient = null)
    {
        return Dm.num(!Dm.truthy(Dm.bitand(patient.mob_biotypes, MOB_ROBOTIC)));
    }

    // tg: code/modules/surgery/operations/operation_revival.dm:44
    public override object tool_check(obj_item tool = null)
    {
        if (istype(tool, typeof(obj_item_shockpaddles)))
        {
            obj_item_shockpaddles paddles = (obj_item_shockpaddles)tool;
            if ((Dm.truthy(paddles.req_defib) && !Dm.truthy(paddles.defib.powered)) || !HAS_TRAIT(paddles, TRAIT_WIELDED) || Dm.truthy(paddles.cooldown) || Dm.truthy(paddles.busy))
                return FALSE;
        }

        if (istype(tool, typeof(obj_item_melee_baton_security)))
        {
            obj_item_melee_baton_security baton = (obj_item_melee_baton_security)tool;
            return baton.active;
        }

        if (istype(tool, typeof(obj_item_gun_energy)))
        {
            obj_item_gun_energy egun = (obj_item_gun_energy)tool;
            return Dm.num(istype(egun.chambered, typeof(obj_item_ammo_casing_energy_electrode)));
        }

        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_revival.dm:60
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        mob_living patient = (mob_living)operating_on;
        display_results(
            surgeon,
            patient,
            span_notice(I18n.T("You prepare to give []'s brain the spark of life with [].", patient, tool)),
            span_notice(I18n.T("[] prepares to give []'s brain the spark of life with [].", surgeon, patient, tool)),
            span_notice(I18n.T("[] prepares to give []'s brain the spark of life.", surgeon, patient)));
        patient.notify_revival(I18n.T("Someone is trying to zap your brain."), source: patient);
    }

    // tg: code/modules/surgery/operations/operation_revival.dm:70
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        mob_living patient = (mob_living)operating_on;
        display_results(
            surgeon,
            patient,
            span_notice(I18n.T("You successfully shock []'s brain with []...", patient, tool)),
            span_notice(I18n.T("[] sends a powerful shock to []'s brain with []...", surgeon, patient, tool)),
            span_notice(I18n.T("[] sends a powerful shock to []'s brain...", surgeon, patient)));
        patient.grab_ghost();
        await patient.adjust_oxy_loss(-50);
        if (iscarbon(patient))
        {
            mob_living_carbon carbon_patient = (mob_living_carbon)patient;
            carbon_patient.set_heartattack(FALSE);
        }
        if (!Dm.truthy(await patient.revive()))
        {
            await on_no_revive(surgeon, patient);
            return null;
        }

        await on_revived(surgeon, patient);
        return null;
    }

    /// Called when you have been successfully raised from the dead
    // tg: code/modules/surgery/operations/operation_revival.dm:90
    public virtual async DmTask on_revived(mob_living surgeon = null, mob_living patient = null)
    {
        patient.visible_message(span_notice(I18n.T("...[] wakes up, alive and aware!", patient)));
        await patient.emote("gasp");
        if (HAS_MIND_TRAIT(surgeon, TRAIT_MORBID)) // Contrary to their typical hatred of resurrection, it wouldn't be very thematic if morbid people didn't love playing god
            await surgeon.add_mood_event("morbid_revival_success", typeof(datum_mood_event_morbid_revival_success));
        await patient.adjust_organ_loss(ORGAN_SLOT_BRAIN, 15, 180);
    }

    /// Called when revival fails
    // tg: code/modules/surgery/operations/operation_revival.dm:98
    public virtual async DmTask on_no_revive(mob_living surgeon = null, mob_living patient = null)
    {
        patient.visible_message(span_warning(I18n.T("...[] convulse[], then lie[] still.", patient.p_they(), patient.p_s(), patient.p_s())));
        await patient.adjust_organ_loss(ORGAN_SLOT_BRAIN, 50, 199); // MAD SCIENCE
    }

    // tg: code/modules/surgery/operations/operation_revival.dm:102
    public override async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        mob_living patient = (mob_living)operating_on;
        display_results(
            surgeon,
            patient,
            span_warning(I18n.T("You shock []'s brain with [], but [] []n't react.", patient, tool, patient.p_they(), patient.p_do())),
            span_warning(I18n.T("[] shocks []'s brain with [], but [] []n't react.", surgeon, patient, tool, patient.p_they(), patient.p_do())),
            span_warning(I18n.T("[] shocks []'s brain with [], but [] []n't react.", surgeon, patient, tool, patient.p_they(), patient.p_do())));
    }
}

// tg: code/modules/surgery/operations/operation_revival.dm:111
public partial class datum_surgery_operation_basic_revival_mechanic : datum_surgery_operation_basic_revival
{
    // tg: code/modules/surgery/operations/operation_revival.dm:115
    public override object brain_check(obj_item_organ_brain brain = null)
    {
        return Dm.num(!Dm.truthy(base.brain_check(brain)));
    }

    // tg: code/modules/surgery/operations/operation_revival.dm:118
    public override object mob_check(mob_living patient = null)
    {
        return Dm.num(!Dm.truthy(base.mob_check(patient)));
    }
}
