// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/skillchip.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M8 slice F, D186).

// tg: code/modules/mob/living/carbon/skillchip.dm:1
public partial class mob_living_carbon : mob_living
{
    /**
     * Attempts to implant this skillchip into the target carbon's brain.
     *
     * Returns whether the skillchip was inserted or not. Can optionally give chat message notification to the mob.
     * Arguments:
     * * skillchip - The skillchip you want to insert.
     * * silent - Whether or not to display the implanting message.
     * * force - Whether to force the implant to happen, including forcing activating if activate = TRUE. Ignores incompatibility checks. Used by changelings.
     */
    // tg: code/modules/mob/living/carbon/skillchip.dm:10
    public virtual async DmTask<object> implant_skillchip(obj_item_skillchip skillchip = null, float? force = null)
    {
        force ??= FALSE;
        // Grab the brain.
        obj_item_organ_brain brain = get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;

        // Check for the brain. No brain = no implant.
        if (QDELETED(brain))
            return "Brain not found.";

        if (Dm.truthy(force))
            return await brain.implant_skillchip(skillchip, force);

        // Check the chip can actually be implanted.
        object mob_incompat_msg = skillchip.has_mob_incompatibility(this);
        if (Dm.truthy(mob_incompat_msg))
            return mob_incompat_msg;

        // Implant in the brain.
        return await brain.implant_skillchip(skillchip, force);
    }

    /**
     * Attempts to remove this skillchip from the target carbon's brain.
     *
     * Returns FALSE when the skillchip couldn't be removed for some reason,
     * including the target or brain not existing or the skillchip not being in the brain.
     * Arguments:
     * * target - The living carbon whose brain you want to remove the chip from.
     * * silent - Whether or not to display the removal message.
     */
    // tg: code/modules/mob/living/carbon/skillchip.dm:34
    public virtual async DmTask<object> remove_skillchip(obj_item_skillchip skillchip = null, float? silent = null)
    {
        silent ??= FALSE;
        // Check the target's brain, making sure the target exists and has a brain.
        obj_item_organ_brain brain = get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
        if (QDELETED(brain))
            return FALSE;

        // Remove and call on_removal proc
        if (!Dm.truthy(await brain.remove_skillchip(skillchip, silent)))
        {
            stack_trace(Dm.text("Failed to remove skillchip [] from [].", skillchip, this), "code/modules/mob/living/carbon/skillchip.dm", 43);
            return FALSE;
        }

        return TRUE;
    }

    /**
     * Creates a list of new skillchips cloned from old skillchips in the mob's brain.
     *
     * Returns a complete list of new skillchips cloned from the mob's brain's existing skillchip stock.
     * Rumour has it that Changelings just LOVE this proc.
     * Arguments:
     * * cloned_chip_holder - The new holder for the cloned chips. Please don't be null.
     * * not_removable - Special override, whether or not to force cloned chips to be non-removable, i.e. to delete on removal.
     */
    // tg: code/modules/mob/living/carbon/skillchip.dm:57
    public virtual object clone_skillchip_list(float? not_removable = null)
    {
        not_removable ??= FALSE;
        // Check the target's brain, making sure the target exists and has a brain.
        obj_item_organ_brain brain = get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
        if (QDELETED(brain))
            return Dm.list();

        return brain.clone_skillchip_list(not_removable);
    }

    /**
     * Destroys all skillchips in the brain, handling appropriate cleanup and event calls.
     */
    // tg: code/modules/mob/living/carbon/skillchip.dm:70
    public virtual async DmTask<object> destroy_all_skillchips(float? silent = null)
    {
        silent ??= FALSE;
        // Check the target's brain, making sure the target exists and has a brain.
        obj_item_organ_brain brain = get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;

        if (QDELETED(brain))
            return FALSE;

        await brain.destroy_all_skillchips(silent);

        return TRUE;
    }
}
