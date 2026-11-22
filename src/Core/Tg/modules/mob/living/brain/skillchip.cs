// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/brain/skillchip.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M8 slice F, D186).

// tg: code/modules/mob/living/brain/skillchip.dm:1
public partial class obj_item_organ_brain : obj_item_organ
{
    /**
     * Attempts to remove target skillchip from the brain.
     *
     * Returns whether the skillchip was removed or not.
     * If you're removing the skillchip from a mob, use the remove_skillchip proc in mob/living/carbon instead.
     * Arguments:
     * * skillchip - The skillchip you'd like to remove.
     */
    // tg: code/modules/mob/living/brain/skillchip.dm:9
    public virtual async DmTask<object> remove_skillchip(obj_item_skillchip skillchip = null, float? silent = null)
    {
        silent ??= FALSE;
        // Check this skillchip is in the brain.
        if (!Dm.@in(skillchip, skillchips))
        {
            stack_trace(Dm.text("Attempted to remove skillchip [] that wasn't in [] skillchip list.", skillchip, this), "code/modules/mob/living/brain/skillchip.dm", 12);
            return FALSE;
        }

        LAZYREMOVE(ref skillchips, skillchip);
        await skillchip.on_removal(silent);
        return TRUE;
    }

    /**
     * Attempts to implant target skillchip into the brain.
     *
     * Returns whether the skillchip was implanted or not.
     * If you're implanting the skillchip into a mob, use the implant_skillchip proc in mob/living/carbon instead.
     * DANGEROUS - This proc assumes you've done the appropriate checks to make sure the skillchip should be implanted.
     * Where possible, call the mob/living/carbon version of this proc which does relevant checks.
     * Arguments:
     * * skillchip - The skillchip you'd like to implant.
     * * force - Whether or not to force the skillchip to be implanted, ignoring any checks.
     */
    // tg: code/modules/mob/living/brain/skillchip.dm:34
    public virtual async DmTask<object> implant_skillchip(obj_item_skillchip skillchip = null, float? force = null)
    {
        force ??= FALSE;
        // If we're not forcing the implant, so let's do some checks.
        if (!Dm.truthy(force))
        {
            // Slot capacity check!
            float max_slots = Dm.num(get_max_skillchip_slots());
            float used_slots = Dm.num(get_used_skillchip_slots());

            if (used_slots + skillchip.slot_use > max_slots)
                return Dm.text("Not enough free slots. You have [] free and need [].", max_slots - used_slots, skillchip.slot_use);
        }

        LAZYADD(ref skillchips, skillchip);
        skillchip.on_implant(this);
        await skillchip.forceMove(this);
        return null;
    }

    /**
     * Creates a list of assoc lists containing skillchip types and key metadata.
     *
     * Returns a complete list of new skillchip types with their metadata cloned from the brain's existing skillchip stock.
     * Rumour has it that Changelings just LOVE this proc.
     * Arguments:
     * * not_removable - Special override, whether or not to force cloned chips to be non-removable, i.e. to delete on removal.
     */
    // tg: code/modules/mob/living/brain/skillchip.dm:52
    public virtual object clone_skillchip_list(float? not_removable = null)
    {
        not_removable ??= FALSE;
        DmList skillchip_metadata = Dm.list();
        // Remove and call on_removal proc if successful.
        foreach (object chip in Dm.each(skillchips))
        {
            obj_item_skillchip skillchip = chip as obj_item_skillchip;

            if (skillchip == null)
            {
                stack_trace(Dm.text("[] contains an item of type [] and this is not a skillchip.", this, ((datum)chip).type), "code/modules/mob/living/brain/skillchip.dm", 58);
                continue;
            }

            // Grab chip metadata
            DmList chip_metadata = (DmList)skillchip.get_metadata();

            // If we're forcing non-removable status, set it after copying metadata.
            if (Dm.truthy(not_removable))
                chip_metadata["removable"] = FALSE;

            Dm.append(skillchip_metadata, Dm.list(chip_metadata));
        }

        return skillchip_metadata;
    }

    /**
     * Destroys all skillchips in the brain, calling on_removal if the brain has an owner.
     * Arguments:
     * * silent - Whether to give the user a chat notification with the removal flavour text.
     */
    // tg: code/modules/mob/living/brain/skillchip.dm:79
    public virtual async DmTask destroy_all_skillchips(float? silent = null)
    {
        silent ??= TRUE;
        if (!QDELETED(owner))
        {
            foreach (object chip in Dm.each(skillchips))
            {
                obj_item_skillchip skillchip = (obj_item_skillchip)chip;
                await skillchip.on_removal(silent);
            }
        }
        QDEL_LIST(skillchips);
    }

    /**
     * Returns the total maximum skillchip complexity supported by this brain.
     */
    // tg: code/modules/mob/living/brain/skillchip.dm:90
    public virtual object get_max_skillchip_complexity()
    {
        if (!QDELETED(owner))
            return max_skillchip_complexity + ((mob_living_carbon)owner).skillchip_complexity_modifier;

        return max_skillchip_complexity;
    }

    /**
     * Returns the total current skillchip complexity used in this brain.
     */
    // tg: code/modules/mob/living/brain/skillchip.dm:99
    public virtual object get_used_skillchip_complexity()
    {
        float complexity_tally = 0;

        foreach (object chip in Dm.each(skillchips))
        {
            obj_item_skillchip skillchip = (obj_item_skillchip)chip;

            if (!Dm.truthy(skillchip.is_active()))
                continue;

            complexity_tally += Dm.num(skillchip.get_complexity());
        }

        return complexity_tally;
    }

    /**
     * Returns the total maximum skillchip slot capacity supported by this brain.
     */
    // tg: code/modules/mob/living/brain/skillchip.dm:115
    public virtual object get_max_skillchip_slots()
    {
        return max_skillchip_slots;
    }

    /**
     * Returns the total current skillchip slot capacity used in this brain.
     */
    // tg: code/modules/mob/living/brain/skillchip.dm:121
    public virtual object get_used_skillchip_slots()
    {
        float slot_tally = 0;

        foreach (object chip in Dm.each(skillchips))
        {
            obj_item_skillchip skillchip = (obj_item_skillchip)chip;

            slot_tally += skillchip.slot_use;
        }

        return slot_tally;
    }

    /**
     * Deactivates all chips currently in the brain.
     */
    // tg: code/modules/mob/living/brain/skillchip.dm:133
    public virtual async DmTask activate_skillchip_failsafe()
    {
        if (QDELETED(owner))
            return;

        float chip_tally = 0;

        foreach (object chip in Dm.each(skillchips))
        {
            obj_item_skillchip skillchip = (obj_item_skillchip)chip;

            // If the chip isn't activated, skip.
            if (!Dm.truthy(skillchip.active))
                continue;

            // Try force-deactivate it.
            await skillchip.try_deactivate_skillchip(FALSE, TRUE);

            // If it's now no longer active, up the tally.
            if (!Dm.truthy(skillchip.active))
                chip_tally++;
        }

        if (Dm.truthy(chip_tally))
            to_chat(owner, span_warning(I18n.T("Unusual brain biology detected. Failsafe procedure engaged. [] skillchip[] been deactivated.", chip_tally, chip_tally == 1 ? " has" : "s have")));
    }

    /// Disables or re-enables any extra skillchips after skillchip limit changes.
    // tg: code/modules/mob/living/brain/skillchip.dm:156
    public virtual async DmTask update_skillchips()
    {
        float limit = Dm.num(get_max_skillchip_complexity());
        float dt = limit - Dm.num(get_used_skillchip_complexity());

        // We can return early if there's no negative difference to worry about.
        // Don't try to automatically activate skillchips. The user can do this themselves in the skill station.
        if (dt >= 0)
            return;

        // Let's start hacking away at skillchips to lower the complexity.
        foreach (object chip in Dm.each(skillchips))
        {
            obj_item_skillchip skillchip = (obj_item_skillchip)chip;
            // If the skillchip is active and try_deactive doesn't return any failure message, we can assume the
            // chip has now been deactivated.
            if (Dm.truthy(skillchip.is_active()) && !Dm.truthy(await skillchip.try_deactivate_skillchip(FALSE, TRUE)))
            {
                dt += Dm.num(skillchip.get_complexity());
                if (dt >= 0)
                    return;
            }
        }
    }
}
