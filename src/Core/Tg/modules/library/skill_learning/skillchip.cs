// Translated from tgstation (snapshot 1d99bd7f): code/modules/library/skill_learning/skillchip.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M8 slice F, D186). Chips are implanted by outfits (implant_skillchip), by the skill station (skill_station.dm, gaps
// SL1, D333) and cut out of a brain with a sharp tool (brain_item.dm item_interaction).

// tg: code/modules/library/skill_learning/skillchip.dm:1
public partial class obj_item_skillchip : obj_item
{
    // tg: code/modules/library/skill_learning/skillchip.dm:47
    [DmParams("mapload", "is_removable")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object is_removable = Dm.arg(__extra, 0);
        is_removable ??= TRUE;
        object dot = base.Initialize(mapload, Dm.forward(__extra, is_removable));
        removable = is_removable;
        return dot;
    }

    ///We don't grant actions outside of being activated when implanted
    // tg: code/modules/library/skill_learning/skillchip.dm:52
    public override object item_action_slot_check(object slot = null, mob user = null, datum_action action = null)
    {
        return FALSE;
    }

    /**
     * Activates the skillchip, if possible.
     *
     * Returns a message containing the reason if activation is not possible.
     * Arguments:
     * * silent - Boolean. Whether or not an activation message should be shown to the user.
     * * force - Boolean. Whether or not to just force de-activation if it would be prevented for any reason.
     */
    // tg: code/modules/library/skill_learning/skillchip.dm:62
    public virtual async DmTask<object> try_activate_skillchip(float? silent = null, float? force = null)
    {
        silent ??= FALSE;
        force ??= FALSE;
        // Should not happen. Holding brain is destroyed and the chip hasn't had its state set appropriately.
        if (QDELETED(holding_brain))
        {
            stack_trace("Skillchip's owner is null or qdeleted brain.", "code/modules/library/skill_learning/skillchip.dm", 65);
            return "Skillchip cannot detect viable brain.";
        }

        // Also should not happen. We're somehow activating skillchips in a bodyless brain.
        if (QDELETED(holding_brain.owner))
        {
            stack_trace("Skillchip's brain has no owner, owner is null or owner qdeleted.", "code/modules/library/skill_learning/skillchip.dm", 70);
            return "Skillchip cannot detect viable body.";
        }

        // We have a holding brain, the holding brain has an owner. If we're forcing this, do it hard and fast.
        if (Dm.truthy(force))
        {
            await on_activate((mob_living_carbon)holding_brain.owner, silent);
            return null;
        }

        // Is the chip still experiencing a cooldown period?
        if (!(chip_cooldown <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            return Dm.text("Skillchip is still recharging for []s", Dm.max(0, chip_cooldown - world.time) * 0.1f);

        // So, we have a brain and that brain has a body. Let's start checking for incompatibility.
        object activate_msg = has_activate_incompatibility(holding_brain);

        // If there's an activate_msg it means we can't activate for some reason. Return the feedback message.
        if (Dm.truthy(activate_msg))
            return activate_msg;

        // Either there's no incompatibility or we're forcing the activation. We're good to go!
        await on_activate((mob_living_carbon)holding_brain.owner, silent);
        return null;
    }

    /**
     * Deactivates the skillchip, if possible.
     *
     * Returns a message containing the reason if deactivation is not possible.
     * Arguments:
     * * silent - Boolean. Whether or not an activation message should be shown to the user.
     * * force - Boolean. Whether or not to just force de-activation if it would be prevented for any reason.
     * * brain_owner - the owner var of the brain is set to null on organ/on_mob_remove(), so we need this if owner is null.
     */
    // tg: code/modules/library/skill_learning/skillchip.dm:97
    public virtual async DmTask<object> try_deactivate_skillchip(float? silent = null, float? force = null, mob_living brain_owner = null)
    {
        silent ??= FALSE;
        force ??= FALSE;
        if (!Dm.truthy(active))
            return "Skillchip is not active.";

        // Should not happen. Holding brain is destroyed and the chip hasn't had its state set appropriately.
        if (holding_brain == null)
        {
            stack_trace("Skillchip doesn't have a holding brain.", "code/modules/library/skill_learning/skillchip.dm", 102);
            return "Skillchip cannot detect viable brain.";
        }

        if (brain_owner == null)
            brain_owner = holding_brain.owner as mob_living;
        // Also should not happen. We're somehow deactivating skillchips in a bodyless brain.
        if (QDELETED(brain_owner))
        {
            active = FALSE;
            stack_trace("Skillchip's brain has no owner, owner is null or owner qdeleted.", "code/modules/library/skill_learning/skillchip.dm", 110);
            return "Skillchip cannot detect viable body.";
        }

        // We have a holding brain, the holding brain has an owner. If we're forcing this, do it hard and fast.
        if (Dm.truthy(force))
        {
            await on_deactivate((mob_living_carbon)brain_owner, silent);
            return null;
        }

        // Is the chip still experiencing a cooldown period?
        if (!(chip_cooldown <= world.time)) // COOLDOWN_FINISHED, inline
            return Dm.text("Skillchip is still recharging for []s", Dm.max(0, chip_cooldown - world.time) * 0.1f);

        // We're good to go. Deactive this chip.
        await on_deactivate((mob_living_carbon)brain_owner, silent);
        return null;
    }

    /**
     * Called when a skillchip is inserted in a user's brain.
     *
     * Arguments:
     * * owner_brain - The brain that this skillchip was implanted in to.
     */
    // tg: code/modules/library/skill_learning/skillchip.dm:131
    [ShouldCallParent]
    public virtual void on_implant(obj_item_organ_brain owner_brain = null)
    {
        if (holding_brain != null)
            throw Dm.CRASH(Dm.text("Skillchip is trying to be implanted into [], but it's already implanted in []", owner_brain, holding_brain), "code/modules/library/skill_learning/skillchip.dm", 134);

        holding_brain = owner_brain;
        if (Dm.truthy(this._listen_lookup?[COMSIG_SKILLCHIP_IMPLANTED]))
            this._SendSignal(COMSIG_SKILLCHIP_IMPLANTED, Dm.list(this, holding_brain));
    }

    /**
     * Called when a skillchip is activated.
     *
     * Arguments:
     * * user - The user to apply skillchip effects to.
     * * silent - Boolean. Whether or not an activation message should be shown to the user.
     */
    // tg: code/modules/library/skill_learning/skillchip.dm:146
    [ShouldCallParent]
    public virtual async DmTask on_activate(mob_living_carbon user = null, float? silent = null)
    {
        silent ??= FALSE;
        if (!Dm.truthy(silent) && Dm.truthy(activate_message))
            to_chat(user, activate_message);

        if (Dm.truthy(length(auto_traits)))
            user.add_traits(auto_traits, SKILLCHIP_TRAIT);

        active = TRUE;

        foreach (datum_action action in Dm.each_cast<datum_action>(actions))
            await action.Grant(user);

        chip_cooldown = world.time + cooldown; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline
    }

    /**
     * Called when a skillchip is removed from the user's brain.
     *
     * Always deactivates the skillchip.
     * Arguments:
     * * user - The user to remove skillchip effects from.
     * * silent - Boolean. Whether or not a deactivation message should be shown to the user.
     */
    // tg: code/modules/library/skill_learning/skillchip.dm:167
    public virtual async DmTask on_removal(float? silent = null)
    {
        silent ??= FALSE;
        if (Dm.truthy(active))
            await try_deactivate_skillchip(silent, TRUE);

        chip_cooldown = 0; // COOLDOWN_RESET (__DEFINES/cooldowns.dm:115), inline
        if (Dm.truthy(this._listen_lookup?[COMSIG_SKILLCHIP_REMOVED]))
            this._SendSignal(COMSIG_SKILLCHIP_REMOVED, Dm.list(this, holding_brain));
        holding_brain = null;
    }

    /**
     * Called when a skillchip is deactivated.
     *
     * Arguments:
     * * user - The user to remove skillchip effects from.
     * * silent - Boolean. Whether or not a deactivation message should be shown to the user.
     */
    // tg: code/modules/library/skill_learning/skillchip.dm:182
    [ShouldCallParent]
    public virtual async DmTask on_deactivate(mob_living_carbon user = null, float? silent = null)
    {
        silent ??= FALSE;
        if (!Dm.truthy(silent) && Dm.truthy(deactivate_message))
            to_chat(user, deactivate_message);

        if (Dm.truthy(length(auto_traits)))
            user.remove_traits(auto_traits, SKILLCHIP_TRAIT);

        active = FALSE;

        foreach (datum_action action in Dm.each_cast<datum_action>(actions))
            await action.Remove(user);

        chip_cooldown = world.time + cooldown; // COOLDOWN_START, inline
    }

    /**
     * Checks whether a given skillchip has an incompatibility with a brain that should render it impossible
     * to activate.
     *
     * Returns a string with an explanation if the chip is not activatable. FALSE otherwise.
     * Arguments:
     * * skillchip - The skillchip you're intending to activate. Does not activate the chip.
     */
    // tg: code/modules/library/skill_learning/skillchip.dm:206
    public virtual object has_activate_incompatibility(obj_item_organ_brain brain = null)
    {
        if (QDELETED(brain))
            return "No brain detected.";

        // Check if there's enough complexity usage left to activate the skillchip.
        float max_complexity = Dm.num(brain.get_max_skillchip_complexity());
        float new_complexity = Dm.num(brain.get_used_skillchip_complexity()) + Dm.num(get_complexity());
        if (new_complexity > max_complexity)
            return Dm.text("Skillchip is too complex to activate: [] total out of [] max complexity.", new_complexity, max_complexity);

        return FALSE;
    }

    /**
     * Checks for skillchip incompatibility with another chip.
     *
     * Does *this* skillchip have incompatibility with the skillchip in the args?
     * Override this with any snowflake chip-vs-chip incompatibility checks.
     * Returns a string with an incompatibility explanation if the chip is not compatible, returns FALSE
     * if it is compatible.
     * Arguments:
     * * skillchip - The skillchip to test for incompatability.
     */
    // tg: code/modules/library/skill_learning/skillchip.dm:231
    public virtual object has_skillchip_incompatibility(obj_item_skillchip skillchip = null)
    {
        // Only allow multiple copies of a type if SKILLCHIP_ALLOWS_MULTIPLE flag is set
        if (!Dm.truthy(Dm.bitand(skillchip_flags, SKILLCHIP_ALLOWS_MULTIPLE)) && (skillchip.type == type))
            return Dm.text("Duplicate chip detected: []", skillchip.name);

        // Prevent implanting multiple chips of the same category.
        if (Dm.truthy(Dm.bitand(skillchip_flags, SKILLCHIP_RESTRICTED_CATEGORIES)) && Dm.@in(skillchip.chip_category, incompatibility_list))
            return Dm.text("Incompatible with implanted [] chip [].", skillchip.chip_category, skillchip.name);

        return FALSE;
    }

    /**
     * Performs a full sweep of checks that dictate if this chip can be implanted in a given target.
     *
     * Override this with any snowflake chip checks. An example of which would be checking if a target is
     * mindshielded if you've got a special security skillchip.
     * Returns a string with an incompatibility explanation if the chip is not compatible, returns FALSE
     * if it is compatible.
     * Arguments:
     * * target - The mob to check for implantability with.
     */
    // tg: code/modules/library/skill_learning/skillchip.dm:248
    public virtual object has_mob_incompatibility(mob_living_carbon target = null)
    {
        // No carbon/carbon of incorrect type
        if (!(target is mob_living_carbon))
            return "Incompatible lifeform detected.";

        // No brain
        obj_item_organ_brain brain = target.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
        if (QDELETED(brain))
            return "No brain detected.";

        // Check brain incompatibility. This also performs skillchip-to-skillchip incompatibility checks.
        object brain_message = has_brain_incompatibility(brain);
        if (Dm.truthy(brain_message))
            return brain_message;

        return FALSE;
    }

    /**
     * Performs a full sweep of checks that dictate if this chip can be implanted in a given brain.
     *
     * Override this with any snowflake chip checks.
     * Returns TRUE if the chip is fully compatible, FALSE otherwise.
     * Arguments:
     * * brain - The brain to check for implantability with.
     */
    // tg: code/modules/library/skill_learning/skillchip.dm:270
    public virtual object has_brain_incompatibility(object brain = null)
    {
        if (!(brain is obj_item_organ_brain))
        {
            stack_trace(Dm.text("Attempted to check incompatibility with invalid brain object []", brain), "code/modules/library/skill_learning/skillchip.dm", 272);
            return "Incompatible brain.";
        }
        obj_item_organ_brain brain__brain = (obj_item_organ_brain)brain;

        object chip_message;

        // Slot capacity check!
        float max_slots = Dm.num(brain__brain.get_max_skillchip_slots());
        float used_slots = Dm.num(brain__brain.get_used_skillchip_slots());

        if (used_slots + slot_use > max_slots)
            return Dm.text("Not enough free slots. You have [] free and need [].", max_slots - used_slots, slot_use);

        // Check if this chip is incompatible with any other chips in the brain.
        foreach (object skillchip in Dm.each(brain__brain.skillchips))
        {
            chip_message = has_skillchip_incompatibility(skillchip as obj_item_skillchip);
            if (Dm.truthy(chip_message))
                return chip_message;
        }

        return FALSE;
    }

    /**
     * Returns whether the chip is on cooldown. Chips ordinarily go on cooldown when activated.
     *
     * This does not mean the chip should be impossible to do anything with.
     * It's up to each individual piece of code to decide what it does with the result of this proc.
     *
     * Returns TRUE if the chip's extraction cooldown hasn't yet passed.
     */
    // tg: code/modules/library/skill_learning/skillchip.dm:301
    public virtual object is_on_cooldown()
    {
        return !(chip_cooldown <= world.time); // COOLDOWN_FINISHED, inline
    }

    /**
     * Returns whether the chip is active.
     *
     * Intended to be overriden.
     * Returns TRUE if the chip is active.
     */
    // tg: code/modules/library/skill_learning/skillchip.dm:310
    public virtual object is_active()
    {
        return active;
    }

    /**
     * Returns the chip's complexity.
     *
     * Intended to be overriden.
     */
    // tg: code/modules/library/skill_learning/skillchip.dm:318
    public virtual object get_complexity()
    {
        return complexity;
    }

    /**
     * Returns a list of basic chip info. Used by the skill station.
     */
    // tg: code/modules/library/skill_learning/skillchip.dm:323
    public virtual object get_chip_data()
    {
        return Dm.list(
            Dm.kv("name", skill_name),
            Dm.kv("icon", skill_icon),
            Dm.kv("desc", skill_description),
            Dm.kv("complexity", get_complexity()),
            Dm.kv("slot_use", slot_use),
            Dm.kv("removable", removable),
            Dm.kv("ref", REF(this)),
            Dm.kv("active", is_active()),
            Dm.kv("active_error", has_activate_incompatibility(holding_brain)),
            Dm.kv("cooldown", Dm.max(0, chip_cooldown - world.time)), // COOLDOWN_TIMELEFT, inline
            Dm.kv("actionable", is_on_cooldown()));
    }

    /**
     * Gets key metadata from this skillchip in an assoc list.
     *
     * If you override this proc, don't forget to also override set_metadata, which takes the output of
     * this proc and uses it to set the metadata.
     * Does not copy over any owner or brain status. Handle that externally.
     */
    // tg: code/modules/library/skill_learning/skillchip.dm:344
    public virtual object get_metadata()
    {
        DmList metadata = Dm.list();
        metadata["type"] = type;
        metadata["chip_cooldown"] = chip_cooldown;
        metadata["active"] = active;
        metadata["removable"] = removable;

        return metadata;
    }

    /**
     * Sets key metadata for this skillchip from an assoc list.
     *
     * Best used with the output from get_metadata() of another chip.
     * If you override this proc, don't forget to also override get_metadata, which is where you should
     * usually get the assoc list that feeds into this proc.
     * Does not set any owner or brain status. Handle that externally.
     * Arguments:
     * metadata - Ideally the output of another chip's get_metadata proc. Assoc list of metadata.
     */
    // tg: code/modules/library/skill_learning/skillchip.dm:362
    public virtual async DmTask<object> set_metadata(DmList metadata = null)
    {
        object active_msg = null;
        // Start by trying to activate.
        active = Dm.num(metadata["active"]);
        if (Dm.truthy(active))
            active_msg = await try_activate_skillchip(FALSE, TRUE);

        // Whether it worked or not, set the rest of the metadata and then return any activate message.
        chip_cooldown = Dm.num(metadata["chip_cooldown"]);
        removable = metadata["removable"];

        return active_msg;
    }
}
