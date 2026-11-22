// Translated from tgstation (snapshot 1d99bd7f): code/modules/experisci/experiment/experiments.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RD3 (D333, the experiments): the whole file. Every experiment of tg's techweb is defined here by its vars (generated, D111);
// the procs are the people scans' tests (a mutation, organs a species does not have, augmented vital organs, a skill chip, a full
// android augmentation).

/// Scan a person with any mutation
// tg: code/modules/experisci/experiment/experiments.dm:341
public partial class datum_experiment_scanning_people_mutant : datum_experiment_scanning_people
{
    // tg: code/modules/experisci/experiment/experiments.dm:347
    public override async DmTask<object> is_valid_scan_target(mob_living_carbon_human check = null, datum_component_experiment_handler experiment_handler = null)
    {
        object dot = await base.is_valid_scan_target(check, experiment_handler);
        if (!Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(LAZYLEN(check.dna.mutations)))
            return FALSE;
        return TRUE;
    }
}

/// Scan for organs you didn't start the round with
// tg: code/modules/experisci/experiment/experiments.dm:356
public partial class datum_experiment_scanning_people_novel_organs : datum_experiment_scanning_people
{
    // tg: code/modules/experisci/experiment/experiments.dm:370  var/static/list/vital_organ_slots
    private static ref DmList is_valid_scan_target__vital_organ_slots => ref TgState.Current.datum_experiment_scanning_people_novel_organs__is_valid_scan_target__vital_organ_slots;

    // tg: code/modules/experisci/experiment/experiments.dm:365
    [DmParams("check")]
    public override async DmTask<object> is_valid_scan_target(mob_living_carbon_human check = null, datum_component_experiment_handler experiment_handler = null)
    {
        object dot = await base.is_valid_scan_target(check, experiment_handler);
        if (!Dm.truthy(dot))
            return dot;
        // Organs which are valid for get_mutant_organ_type_for_slot
        foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(check.organs))
        {
            if (Dm.truthy(organic_only) && !Dm.truthy(IS_ORGANIC_ORGAN(organ)))
                continue;
            datum_species target_species = check.dna.species as datum_species;
            if (Dm.@in(organ.slot, is_valid_scan_target__vital_organ_slots))
            {
                if (Dm.eq(organ.type, target_species.get_mutant_organ_type_for_slot(organ.slot)))
                    continue;
            }
            else
            {
                if (Dm.@in(organ.type, target_species.mutant_organs))
                    continue;
            }
            return TRUE;
        }
        return FALSE;
    }
}

/// Scan for cybernetic organs
// tg: code/modules/experisci/experiment/experiments.dm:396
public partial class datum_experiment_scanning_people_augmented_organs : datum_experiment_scanning_people
{
    // tg: code/modules/experisci/experiment/experiments.dm:407  var/static/list/vital_organ_slots
    private static ref DmList is_valid_scan_target__vital_organ_slots => ref TgState.Current.datum_experiment_scanning_people_augmented_organs__is_valid_scan_target__vital_organ_slots;

    // tg: code/modules/experisci/experiment/experiments.dm:403
    [DmParams("check")]
    public override async DmTask<object> is_valid_scan_target(mob_living_carbon_human check = null, datum_component_experiment_handler experiment_handler = null)
    {
        object dot = await base.is_valid_scan_target(check, experiment_handler);
        if (!Dm.truthy(dot))
            return dot;

        foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(check.organs))
        {
            if (Dm.@in(organ.slot, is_valid_scan_target__vital_organ_slots) && Dm.truthy(IS_ROBOTIC_ORGAN(organ)))
                return TRUE;
        }
        return FALSE;
    }
}

/// Scan for skillchips
// tg: code/modules/experisci/experiment/experiments.dm:422
public partial class datum_experiment_scanning_people_skillchip : datum_experiment_scanning_people
{
    // tg: code/modules/experisci/experiment/experiments.dm:428
    public override async DmTask<object> is_valid_scan_target(mob_living_carbon_human check = null, datum_component_experiment_handler experiment_handler = null)
    {
        object dot = await base.is_valid_scan_target(check, experiment_handler);
        if (!Dm.truthy(dot))
            return dot;
        obj_item_organ_brain scanned_brain = check.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
        if (scanned_brain == null)
        {
            await experiment_handler.announce_message(I18n.T("Subject is brainless!"));
            return FALSE;
        }
        if (Dm.eq(scanned_brain.get_used_skillchip_slots(), 0))
        {
            await experiment_handler.announce_message(I18n.T("No skill chips found!"));
            return FALSE;
        }
        return TRUE;
    }
}

/// Scan an android
// tg: code/modules/experisci/experiment/experiments.dm:442
public partial class datum_experiment_scanning_people_android : datum_experiment_scanning_people
{
    // tg: code/modules/experisci/experiment/experiments.dm:456  var/static/list/augmented_organ_slots
    private static ref DmList is_valid_scan_target__augmented_organ_slots => ref TgState.Current.datum_experiment_scanning_people_android__is_valid_scan_target__augmented_organ_slots;

    // tg: code/modules/experisci/experiment/experiments.dm:449
    public override async DmTask<object> is_valid_scan_target(mob_living_carbon_human check = null, datum_component_experiment_handler experiment_handler = null)
    {
        object dot = await base.is_valid_scan_target(check, experiment_handler);
        if (!Dm.truthy(dot))
            return dot;
        if (length(check.get_missing_limbs()) > 1)
            return FALSE;

        // some species don't need a stomach, lungs, heart or liver. This is the case for androids, which are fully augged humies with a few extra quirks.
        float organs_left_to_check = length(is_valid_scan_target__augmented_organ_slots);
        foreach (object skip_organ_trait in Dm.each(Dm.list(TRAIT_NOHUNGER, TRAIT_NOBREATH, TRAIT_NOBLOOD, TRAIT_LIVERLESS_METABOLISM)))
        {
            if (HAS_TRAIT(check, (string)skip_organ_trait))
                organs_left_to_check--;
        }

        foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(check.organs))
        {
            if (!Dm.@in(organ.slot, is_valid_scan_target__augmented_organ_slots))
                continue;
            if (!Dm.truthy(IS_ROBOTIC_ORGAN(organ)))
                return FALSE;
            organs_left_to_check--;
        }

        if (organs_left_to_check > 0) //we're missing some organs like ears, eyes, liver etc.
            return FALSE;

        foreach (obj_item_bodypart bodypart in Dm.each_cast<obj_item_bodypart>(check.get_bodyparts()))
        {
            if (!Dm.truthy(IS_ROBOTIC_LIMB(bodypart)))
                return FALSE;
        }
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/experisci/experiment/experiments.dm:370  var/static/list/vital_organ_slots (Organs which are valid for get_mutant_organ_type_for_slot)
    internal DmList datum_experiment_scanning_people_novel_organs__is_valid_scan_target__vital_organ_slots = Dm.list(
        ORGAN_SLOT_BRAIN,
        ORGAN_SLOT_HEART,
        ORGAN_SLOT_LUNGS,
        ORGAN_SLOT_APPENDIX,
        ORGAN_SLOT_EYES,
        ORGAN_SLOT_EARS,
        ORGAN_SLOT_TONGUE,
        ORGAN_SLOT_LIVER,
        ORGAN_SLOT_STOMACH);

    // tg: code/modules/experisci/experiment/experiments.dm:407  var/static/list/vital_organ_slots
    internal DmList datum_experiment_scanning_people_augmented_organs__is_valid_scan_target__vital_organ_slots = Dm.list(
        ORGAN_SLOT_HEART,
        ORGAN_SLOT_LUNGS,
        ORGAN_SLOT_EYES,
        ORGAN_SLOT_EARS,
        ORGAN_SLOT_LIVER,
        ORGAN_SLOT_STOMACH);

    // tg: code/modules/experisci/experiment/experiments.dm:456  var/static/list/augmented_organ_slots
    internal DmList datum_experiment_scanning_people_android__is_valid_scan_target__augmented_organ_slots = Dm.list(
        ORGAN_SLOT_EYES,
        ORGAN_SLOT_EARS,
        ORGAN_SLOT_HEART,
        ORGAN_SLOT_LUNGS,
        ORGAN_SLOT_LIVER,
        ORGAN_SLOT_STOMACH);
}
