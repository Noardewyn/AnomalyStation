// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/dna_infuser/dna_infusion.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3b, D327): who can be infused, which entry a source matches, and the organ an infusion grows.

// tg: code/game/machinery/dna_infuser/dna_infusion.dm:2 (interleaved types, rules 1.9)
public partial class atom_movable : atom
{
    ///returns a boolean whether a machine occupant can be infused
    // tg: code/game/machinery/dna_infuser/dna_infusion.dm:2
    public virtual object can_infuse(mob feedback_target = null)
    {
        if (feedback_target != null)
            balloon_alert(feedback_target, I18n.T("no dna!"));
        return FALSE;
    }

    ///returns /datum/infuser_entry that matches an item being used for infusion, returns a fly mutation on failure
    // tg: code/game/machinery/dna_infuser/dna_infusion.dm:23
    public virtual datum_infuser_entry get_infusion_entry()
    {
        datum_infuser_entry found = null;
        foreach (datum_infuser_entry entry in Dm.each_cast<datum_infuser_entry>(assoc_to_values(GLOB.infuser_entries) as DmList))
        {
            if (Dm.eq(entry.tier, DNA_MUTANT_UNOBTAINABLE))
                continue;
            if (Dm.truthy(is_type_in_list(this, entry.input_obj_or_mob)))
            {
                found = entry;
                break;
            }
        }
        if (found == null)
            found = GLOB.infuser_entries[typeof(datum_infuser_entry_fly)] as datum_infuser_entry;
        return found;
    }
}

// tg: code/game/machinery/dna_infuser/dna_infusion.dm:8
public partial class mob_living : mob
{
    // tg: code/game/machinery/dna_infuser/dna_infusion.dm:8
    public override object can_infuse(mob feedback_target = null)
    {
        if (feedback_target != null)
            balloon_alert(feedback_target, I18n.T("dna too simple!"));
        return FALSE;
    }
}

// tg: code/game/machinery/dna_infuser/dna_infusion.dm:13
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/game/machinery/dna_infuser/dna_infusion.dm:13
    public override object can_infuse(mob feedback_target = null)
    {
        // Checked by can_mutate but explicit feedback for this issue is good
        if (HAS_TRAIT(this, TRAIT_BADDNA))
        {
            if (feedback_target != null)
                balloon_alert(feedback_target, I18n.T("dna is corrupted!"));
            return FALSE;
        }
        if (!Dm.truthy(can_mutate()))
        {
            if (feedback_target != null)
                balloon_alert(feedback_target, I18n.T("dna is missing!"));
            return FALSE;
        }
        return TRUE;
    }

    /// Attempt to replace/add-to the occupant's organs with "mutated" equivalents.
    /// Returns TRUE on success, FALSE on failure.
    /// Requires the target mob to have an existing organic organ to "mutate".
    // TODO: In the future, this should have more logic:
    // - Replace non-mutant organs before mutant ones.
    // tg: code/game/machinery/dna_infuser/dna_infusion.dm:42
    public virtual async DmTask<object> infuse_organ(datum_infuser_entry entry = null, atom_movable infused_from = null)
    {
        object new_organ_type = pick_infusion_organ(entry, infused_from);
        if (!Dm.truthy(new_organ_type))
            return FALSE;
        // Valid organ successfully picked.
        obj_item_organ new_organ = Dm.@new(new_organ_type as Type) as obj_item_organ;
        await new_organ.replace_into(this);
        new_organ.organ_flags = Dm.bitor(new_organ.organ_flags, ORGAN_MUTANT);
        return TRUE;
    }

    /// Picks a random mutated organ from the given infuser entry which is also compatible with this human.
    /// Tries to return a typepath of a valid mutant organ if all of the following criteria are true:
    /// 1. Target must have a pre-existing organ in the same organ slot as the new organ;
    ///   - or the new organ must be external.
    /// 2. Target's pre-existing organ must be organic / not robotic.
    /// 3. Target must not have the same/identical organ.
    // tg: code/game/machinery/dna_infuser/dna_infusion.dm:58
    public virtual object pick_infusion_organ(datum_infuser_entry entry = null, atom_movable infused_from = null)
    {
        if (entry == null)
            return FALSE;
        DmList potential_new_organs = entry.get_output_organs(this, infused_from);
        // Remove organ typepaths from the list if they're incompatible with target.
        foreach (object new_organ in Dm.each(entry.output_organs))
        {
            obj_item_organ old_organ = get_organ_slot(Dm.initial(new_organ, (obj_item_organ o) => o.slot)) as obj_item_organ;
            if (old_organ != null)
            {
                if (!Dm.eq(old_organ.type, new_organ) && !Dm.truthy(IS_ROBOTIC_ORGAN(old_organ)))
                    continue; // Old organ can be mutated!
            }
            else if (Dm.truthy(Dm.bitand(Dm.initial(new_organ, (obj_item_organ o) => o.organ_flags), ORGAN_EXTERNAL)))
                continue; // External organ can be grown!
            // Internal organ is either missing, or is non-organic.
            Dm.remove(ref potential_new_organs, new_organ);
        }
        // Pick a random organ from the filtered list.
        if (Dm.truthy(length(potential_new_organs)))
            return pick(potential_new_organs);
        return FALSE;
    }
}
