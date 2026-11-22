// Translated from tgstation (snapshot 1d99bd7f): code/game/atom/atom_tool_acts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P1: the whole file (25 procs, docs/ai/specs/m4a-ownership.md). The interaction guard of D132 (docs/ai/specs/interaction.md 7,
// DV27) is at the top of base_item_interaction (the target's tool_act and item_interaction) and of the tool's
// interact_with_atom and ranged_interact_with_atom bases; a refusal is ITEM_INTERACT_BLOCKING (tg's "handled, stop").

// tg: code/game/atom/atom_tool_acts.dm:8
public partial class atom : datum
{
    /**
     * ## Item interaction
     *
     * Handles non-combat interactions of a tool on this atom,
     * such as using a tool on a wall to deconstruct it,
     * or scanning someone with a health analyzer
     */
    // tg: code/game/atom/atom_tool_acts.dm:8
    public virtual async DmTask<object> base_item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        // DEVIATION(DV27): the interaction guard (D132) on the target's own tool_act and item_interaction.
        // DEVIATION(DV255): C-4 sticks to anything, its target's own code ported or not.
        if (tool is not obj_item_grenade_c4 && !Dm.truthy(user.combat_mode) && InertSeam.MissingOverride(this, "tool_act", user))
            return ITEM_INTERACT_BLOCKING;
        if (tool is not obj_item_grenade_c4 && InertSeam.MissingOverride(this, Dm.truthy(text2num(LAZYACCESS(modifiers, RIGHT_CLICK))) ? "item_interaction_secondary" : "item_interaction", user))
            return ITEM_INTERACT_BLOCKING;

        // DEVIATION(DV255): a left click sticks C-4 before the target's own code takes it (a vending machine would refuse it as stock); a storage still takes it in.
        if (tool is obj_item_grenade_c4 && atom_storage == null && !Dm.truthy(text2num(LAZYACCESS(modifiers, RIGHT_CLICK))))
        {
            object stuck = await tool.interact_with_atom(this, user, modifiers);
            if (Dm.truthy(stuck))
                return stuck;
        }

        if (!Dm.truthy(user.combat_mode))
        {
            object tool_return = await tool_act(user, tool, modifiers);
            if (Dm.truthy(tool_return))
                return tool_return;
        }

        float? is_right_clicking = text2num(LAZYACCESS(modifiers, RIGHT_CLICK));
        float is_left_clicking = Dm.num(!Dm.truthy(is_right_clicking));
        object early_sig_return = NONE;
        if (Dm.truthy(is_left_clicking))
        {
            /*
             * This is intentionally using `||` instead of `|` to short-circuit the signal calls
             * This is because we want to return early if ANY of these signals return a value
             *
             * This puts priority on the atom's signals, then the tool's signals, then the user's signals,
             * so we can avoid doing two interactions at once
             */
            early_sig_return = ((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_ITEM_INTERACTION]) ? (object)NONE : this._SendSignal(COMSIG_ATOM_ITEM_INTERACTION, Dm.list(this, user, tool, modifiers))) is var or__1 && Dm.truthy(or__1) ? or__1
                : ((!Dm.truthy(tool._listen_lookup?[COMSIG_ITEM_INTERACTING_WITH_ATOM]) ? (object)NONE : tool._SendSignal(COMSIG_ITEM_INTERACTING_WITH_ATOM, Dm.list(tool, user, this, modifiers))) is var or__2 && Dm.truthy(or__2) ? or__2
                : (!Dm.truthy(user._listen_lookup?[COMSIG_USER_ITEM_INTERACTION]) ? (object)NONE : user._SendSignal(COMSIG_USER_ITEM_INTERACTION, Dm.list(user, this, tool, modifiers)))));
        }
        else
        {
            // See above
            early_sig_return = ((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_ITEM_INTERACTION_SECONDARY]) ? (object)NONE : this._SendSignal(COMSIG_ATOM_ITEM_INTERACTION_SECONDARY, Dm.list(this, user, tool, modifiers))) is var or__3 && Dm.truthy(or__3) ? or__3
                : ((!Dm.truthy(tool._listen_lookup?[COMSIG_ITEM_INTERACTING_WITH_ATOM_SECONDARY]) ? (object)NONE : tool._SendSignal(COMSIG_ITEM_INTERACTING_WITH_ATOM_SECONDARY, Dm.list(tool, user, this, modifiers))) is var or__4 && Dm.truthy(or__4) ? or__4
                : (!Dm.truthy(user._listen_lookup?[COMSIG_USER_ITEM_INTERACTION_SECONDARY]) ? (object)NONE : user._SendSignal(COMSIG_USER_ITEM_INTERACTION_SECONDARY, Dm.list(user, this, tool, modifiers)))));
        }
        if (Dm.truthy(early_sig_return))
            return early_sig_return;

        object self_interaction = Dm.truthy(is_left_clicking)
            ? await item_interaction(user, tool, modifiers)
            : await item_interaction_secondary(user, tool, modifiers);
        if (Dm.truthy(self_interaction))
            return self_interaction;

        object interact_return = Dm.truthy(is_left_clicking)
            ? await tool.interact_with_atom(this, user, modifiers)
            : await tool.interact_with_atom_secondary(this, user, modifiers);
        if (Dm.truthy(interact_return))
            return interact_return;

        // We have to manually handle storage in item_interaction because storage is blocking in 99% of interactions, which stifles a lot
        // Yeah it sucks not being able to signalize this, but the other option is to have a second signal here just for storage which is also not great
        if (Dm.truthy(atom_storage))
        {
            if (Dm.truthy(is_left_clicking))
            {
                if (Dm.truthy(atom_storage.insert_on_attack))
                    return atom_storage.item_interact_insert(user, tool);
            }
            else
            {
                if (Dm.truthy(await atom_storage.open_storage(user)) && Dm.truthy(atom_storage.display_contents))
                    return ITEM_INTERACT_SUCCESS;
            }
        }

        return NONE;
    }

    /**
     *
     * ## Tool Act
     *
     * Handles using specific tools on this atom directly.
     * Only called when combat mode is off.
     *
     * Handles the tool_acts in particular, such as wrenches and screwdrivers.
     *
     * This can be overridden to handle unique "tool interactions"
     * IE using an item like a tool (when it's not actually one)
     * This is particularly useful for things that shouldn't be inserted into storage
     * (because tool acting runs before storage checks)
     * but otherwise does nothing that [item_interaction] doesn't already do.
     *
     * In other words, use sparingly. It's harder to use (correctly) than [item_interaction].
     */
    // tg: code/game/atom/atom_tool_acts.dm:79
    public virtual async DmTask<object> tool_act(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        object tool_type = tool.tool_behaviour;
        if (!Dm.truthy(tool_type))
            return NONE;

        object is_right_clicking = LAZYACCESS(modifiers, RIGHT_CLICK);
        float is_left_clicking = Dm.num(!Dm.truthy(is_right_clicking));

        DmList processing_recipes = Dm.list();
        object signal_result = Dm.truthy(is_left_clicking)
            ? (!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_TOOL_ACT(tool_type)]) ? (object)NONE : this._SendSignal(COMSIG_ATOM_TOOL_ACT(tool_type), Dm.list(this, user, tool, processing_recipes)))
            : (!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_SECONDARY_TOOL_ACT(tool_type)]) ? (object)NONE : this._SendSignal(COMSIG_ATOM_SECONDARY_TOOL_ACT(tool_type), Dm.list(this, user, tool)));
        if (Dm.truthy(signal_result))
            return signal_result;
        if (Dm.truthy(Dm.length(processing_recipes)))
        {
            await process_recipes(user, tool, processing_recipes);
            return ITEM_INTERACT_SUCCESS;
        }
        if (QDELETED(tool))
            return ITEM_INTERACT_SUCCESS; // Safe-ish to assume that if we deleted our item something succeeded

        object act_result = NONE; // or FALSE, or null, as some things may return

        object sw__1 = tool_type;
        if (Dm.eq(sw__1, TOOL_CROWBAR))
            act_result = Dm.truthy(is_left_clicking) ? await crowbar_act(user, tool) : await crowbar_act_secondary(user, tool);
        else if (Dm.eq(sw__1, TOOL_MULTITOOL))
            act_result = Dm.truthy(is_left_clicking) ? await multitool_act(user, tool) : await multitool_act_secondary(user, tool);
        else if (Dm.eq(sw__1, TOOL_SCREWDRIVER))
            act_result = Dm.truthy(is_left_clicking) ? await screwdriver_act(user, tool) : await screwdriver_act_secondary(user, tool);
        else if (Dm.eq(sw__1, TOOL_WRENCH))
            act_result = Dm.truthy(is_left_clicking) ? await wrench_act(user, tool) : await wrench_act_secondary(user, tool);
        else if (Dm.eq(sw__1, TOOL_WIRECUTTER))
            act_result = Dm.truthy(is_left_clicking) ? await wirecutter_act(user, tool) : await wirecutter_act_secondary(user, tool);
        else if (Dm.eq(sw__1, TOOL_WELDER))
            act_result = Dm.truthy(is_left_clicking) ? await welder_act(user, tool) : await welder_act_secondary(user, tool);
        else if (Dm.eq(sw__1, TOOL_ANALYZER))
        {
            // analyzer_act_secondary's slot returns nothing (void): its call's value is null, as DM's of a proc returning nothing.
            if (Dm.truthy(is_left_clicking))
                act_result = analyzer_act(user, tool);
            else
            {
                analyzer_act_secondary(user, tool);
                act_result = null;
            }
        }

        if (!Dm.truthy(act_result))
            return NONE;

        // A tooltype_act has completed successfully
        if (Dm.truthy(is_left_clicking))
        {
            log_tool(Dm.text("[] used [] on [] at []", key_name(user), tool, this, AREACOORD(this)));
            if (Dm.truthy(tool._listen_lookup?[COMSIG_TOOL_ATOM_ACTED_PRIMARY(tool_type)]))
                tool._SendSignal(COMSIG_TOOL_ATOM_ACTED_PRIMARY(tool_type), Dm.list(tool, this));
        }
        else
        {
            log_tool(Dm.text("[] used [] on [] (right click) at []", key_name(user), tool, this, AREACOORD(this)));
            if (Dm.truthy(tool._listen_lookup?[COMSIG_TOOL_ATOM_ACTED_SECONDARY(tool_type)]))
                tool._SendSignal(COMSIG_TOOL_ATOM_ACTED_SECONDARY(tool_type), Dm.list(tool, this));
        }
        if (Dm.truthy(tool._listen_lookup?[COMSIG_ITEM_TOOL_ACTED]))
            tool._SendSignal(COMSIG_ITEM_TOOL_ACTED, Dm.list(tool, this, user, tool_type, act_result));
        return act_result;
    }

    /**
     * Called when this atom has an item used on it.
     * IE, a mob is clicking on this atom with an item.
     *
     * Return an ITEM_INTERACT_ flag in the event the interaction was handled, to cancel further interaction code.
     * Return NONE to allow default interaction / tool handling.
     */
    // tg: code/game/atom/atom_tool_acts.dm:134
    public virtual async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        return NONE;
    }

    /**
     * Called when this atom has an item used on it WITH RIGHT CLICK,
     * IE, a mob is right clicking on this atom with an item.
     * Default behavior has it run the same code as left click.
     *
     * Return an ITEM_INTERACT_ flag in the event the interaction was handled, to cancel further interaction code.
     * Return NONE to allow default interaction / tool handling.
     */
    // tg: code/game/atom/atom_tool_acts.dm:146
    public virtual async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        return await item_interaction(user, tool, modifiers);
    }
}

// tg: code/game/atom/atom_tool_acts.dm:156
public partial class obj_item : obj
{
    /**
     * Called when this item is being used to interact with an atom,
     * IE, a mob is clicking on an atom with this item.
     *
     * Return an ITEM_INTERACT_ flag in the event the interaction was handled, to cancel further interaction code.
     * Return NONE to allow default interaction / tool handling.
     */
    // tg: code/game/atom/atom_tool_acts.dm:156
    public virtual async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        // DEVIATION(DV27): the interaction guard (D132) on the tool.
        if (InertSeam.MissingOverride(this, "interact_with_atom", user))
            return ITEM_INTERACT_BLOCKING;
        return NONE;
    }

    /**
     * Called when this item is being used to interact with an atom WITH RIGHT CLICK,
     * IE, a mob is right clicking on an atom with this item.
     *
     * Default behavior has it run the same code as left click.
     *
     * Return an ITEM_INTERACT_ flag in the event the interaction was handled, to cancel further interaction code.
     * Return NONE to allow default interaction / tool handling.
     */
    // tg: code/game/atom/atom_tool_acts.dm:168
    public virtual async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        // DEVIATION(DV27): the interaction guard (D132) on the tool.
        if (InertSeam.MissingOverride(this, "interact_with_atom_secondary", user))
            return ITEM_INTERACT_BLOCKING;
        return await interact_with_atom(interacting_with, user, modifiers);
    }
}

// tg: code/game/atom/atom_tool_acts.dm:182
public partial class atom : datum
{
    /**
     * ## Ranged item interaction
     *
     * Handles non-combat ranged interactions of a tool on this atom,
     * such as shooting a gun in the direction of someone*,
     * having a scanner you can point at someone to scan them at any distance,
     * or pointing a laser pointer at something.
     *
     * *While this intuitively sounds combat related, it is not,
     * because a "combat use" of a gun is gun-butting.
     */
    // tg: code/game/atom/atom_tool_acts.dm:182
    public virtual async DmTask<object> base_ranged_item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        object is_right_clicking = LAZYACCESS(modifiers, RIGHT_CLICK);
        float is_left_clicking = Dm.num(!Dm.truthy(is_right_clicking));
        object early_sig_return = NONE;
        if (Dm.truthy(is_left_clicking))
        {
            // See [base_item_interaction] for defails on why this is using `||` (TL;DR it's short circuiting)
            early_sig_return = ((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_RANGED_ITEM_INTERACTION]) ? (object)NONE : this._SendSignal(COMSIG_ATOM_RANGED_ITEM_INTERACTION, Dm.list(this, user, tool, modifiers))) is var or__1 && Dm.truthy(or__1) ? or__1
                : (!Dm.truthy(tool._listen_lookup?[COMSIG_RANGED_ITEM_INTERACTING_WITH_ATOM]) ? (object)NONE : tool._SendSignal(COMSIG_RANGED_ITEM_INTERACTING_WITH_ATOM, Dm.list(tool, user, this, modifiers))));
        }
        else
        {
            // See above
            early_sig_return = ((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_RANGED_ITEM_INTERACTION_SECONDARY]) ? (object)NONE : this._SendSignal(COMSIG_ATOM_RANGED_ITEM_INTERACTION_SECONDARY, Dm.list(this, user, tool, modifiers))) is var or__2 && Dm.truthy(or__2) ? or__2
                : (!Dm.truthy(tool._listen_lookup?[COMSIG_RANGED_ITEM_INTERACTING_WITH_ATOM_SECONDARY]) ? (object)NONE : tool._SendSignal(COMSIG_RANGED_ITEM_INTERACTING_WITH_ATOM_SECONDARY, Dm.list(tool, user, this, modifiers))));
        }
        if (Dm.truthy(early_sig_return))
            return early_sig_return;

        object self_interaction = Dm.truthy(is_left_clicking)
            ? await ranged_item_interaction(user, tool, modifiers)
            : await ranged_item_interaction_secondary(user, tool, modifiers);
        if (Dm.truthy(self_interaction))
            return self_interaction;

        object interact_return = Dm.truthy(is_left_clicking)
            ? await tool.ranged_interact_with_atom(this, user, modifiers)
            : await tool.ranged_interact_with_atom_secondary(this, user, modifiers);
        if (Dm.truthy(interact_return))
            return interact_return;

        return NONE;
    }

    /**
     * Called when this atom has an item used on it from a distance.
     * IE, a mob is clicking on this atom with an item and is not adjacent.
     *
     * Does NOT include Telekinesis users, they are considered adjacent generally.
     *
     * Return an ITEM_INTERACT_ flag in the event the interaction was handled, to cancel further interaction code.
     */
    // tg: code/game/atom/atom_tool_acts.dm:222
    public virtual async DmTask<object> ranged_item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        return NONE;
    }

    /**
     * Called when this atom has an item used on it from a distance WITH RIGHT CLICK,
     * IE, a mob is right clicking on this atom with an item and is not adjacent.
     *
     * Default behavior has it run the same code as left click.
     *
     * Return an ITEM_INTERACT_ flag in the event the interaction was handled, to cancel further interaction code.
     */
    // tg: code/game/atom/atom_tool_acts.dm:233
    public virtual async DmTask<object> ranged_item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        return await ranged_item_interaction(user, tool, modifiers);
    }
}

// tg: code/game/atom/atom_tool_acts.dm:245
public partial class obj_item : obj
{
    /**
     * Called when this item is being used to interact with an atom from a distance,
     * IE, a mob is clicking on an atom with this item and is not adjacent.
     *
     * Does NOT include Telekinesis users, they are considered adjacent generally
     * (so long as this item is adjacent to the atom).
     *
     * Return an ITEM_INTERACT_ flag in the event the interaction was handled, to cancel further interaction code.
     */
    // tg: code/game/atom/atom_tool_acts.dm:245
    public virtual async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        // DEVIATION(DV27): the interaction guard (D132) on the tool.
        if (InertSeam.MissingOverride(this, "ranged_interact_with_atom", user))
            return ITEM_INTERACT_BLOCKING;
        return NONE;
    }

    /**
     * Called when this item is being used to interact with an atom from a distance WITH RIGHT CLICK,
     * IE, a mob is right clicking on an atom with this item and is not adjacent.
     *
     * Default behavior has it run the same code as left click.
     */
    // tg: code/game/atom/atom_tool_acts.dm:254
    public virtual async DmTask<object> ranged_interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        // DEVIATION(DV27): the interaction guard (D132) on the tool.
        if (InertSeam.MissingOverride(this, "ranged_interact_with_atom_secondary", user))
            return ITEM_INTERACT_BLOCKING;
        return await ranged_interact_with_atom(interacting_with, user, modifiers);
    }
}

/*
 * Tool-specific behavior procs.
 *
 * Return an ITEM_INTERACT_ flag to handle the event, or NONE to allow the mob to attack the atom.
 * Returning TRUE will also cancel attacks. It is equivalent to an ITEM_INTERACT_ flag. (This is legacy behavior, and is not to be relied on)
 * Returning FALSE or null will also allow the mob to attack the atom. (This is also legacy behavior)
 */

// tg: code/game/atom/atom_tool_acts.dm:267
public partial class atom : datum
{
    /// Called on an object when a tool with crowbar capabilities is used to left click an object
    // tg: code/game/atom/atom_tool_acts.dm:267
    public virtual async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return null;
    }

    /// Called on an object when a tool with crowbar capabilities is used to right click an object
    // tg: code/game/atom/atom_tool_acts.dm:271
    public virtual async DmTask<object> crowbar_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return null;
    }

    /// Called on an object when a tool with multitool capabilities is used to left click an object
    // tg: code/game/atom/atom_tool_acts.dm:275
    public virtual async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        return null;
    }

    /// Called on an object when a tool with multitool capabilities is used to right click an object
    // tg: code/game/atom/atom_tool_acts.dm:279
    public virtual async DmTask<object> multitool_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return null;
    }

    /// Called on an object when a tool with screwdriver capabilities is used to left click an object
    // tg: code/game/atom/atom_tool_acts.dm:283
    public virtual async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return null;
    }

    /// Called on an object when a tool with screwdriver capabilities is used to right click an object
    // tg: code/game/atom/atom_tool_acts.dm:287
    public virtual async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return null;
    }

    /// Called on an object when a tool with wrench capabilities is used to left click an object
    // tg: code/game/atom/atom_tool_acts.dm:291
    public virtual async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        return null;
    }

    /// Called on an object when a tool with wrench capabilities is used to right click an object
    // tg: code/game/atom/atom_tool_acts.dm:295
    public virtual async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return null;
    }

    /// Called on an object when a tool with wirecutter capabilities is used to left click an object
    // tg: code/game/atom/atom_tool_acts.dm:299
    public virtual async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        return null;
    }

    /// Called on an object when a tool with wirecutter capabilities is used to right click an object
    // tg: code/game/atom/atom_tool_acts.dm:303
    public virtual async DmTask<object> wirecutter_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return null;
    }

    /// Called on an object when a tool with welder capabilities is used to left click an object
    // tg: code/game/atom/atom_tool_acts.dm:307
    public virtual async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        return null;
    }

    /// Called on an object when a tool with welder capabilities is used to right click an object
    // tg: code/game/atom/atom_tool_acts.dm:311
    public virtual async DmTask<object> welder_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return null;
    }

    /// Called on an object when a tool with analyzer capabilities is used to left click an object
    // tg: code/game/atom/atom_tool_acts.dm:315
    public virtual object analyzer_act(mob_living user = null, obj_item tool = null)
    {
        return null;
    }

    /// Called on an object when a tool with analyzer capabilities is used to right click an object
    // tg: code/game/atom/atom_tool_acts.dm:319
    public virtual void analyzer_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return;
    }
}
