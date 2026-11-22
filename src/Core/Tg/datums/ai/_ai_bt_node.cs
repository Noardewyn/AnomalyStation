// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/_ai_bt_node.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1). The var block is generated.

///Base node for behavior tree nodes
// tg: code/datums/ai/_ai_bt_node.dm:2
public partial class datum_bt_node : datum
{
    // tg: code/datums/ai/_ai_bt_node.dm:17
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (!Dm.truthy(label))
        {
            string t = Dm.text("[]", type);
            t = replacetext(t, "/datum/bt_node/decorator/", "");
            t = replacetext(t, "/datum/bt_node/ai_behavior/", "");
            t = replacetext(t, "/datum/bt_node/subtree/", "");
            label = t;
        }
    }

    ///Ticked by the ai_controller. Returns BT_SUCCESS, BT_FAILURE, or BT_RUNNING which can change how the parent responds.
    // tg: code/datums/ai/_ai_bt_node.dm:27
    public virtual object tick(datum_ai_controller controller = null, object seconds_per_tick = null)
    {
        return BT_FAILURE;
    }

    /// Resets per-tick state for this node instance. Override in subtypes that hold tick state.
    // tg: code/datums/ai/_ai_bt_node.dm:32
    public virtual async DmTask reset_tick_state()
    {
    }

    /// Resets this node and all of its descendants, cancelling any behaviors still running in the subtree.
    // tg: code/datums/ai/_ai_bt_node.dm:36
    public virtual async DmTask reset_subtree_tick_states()
    {
        DmList to_visit = Dm.list(this);
        float index = 1;
        while (index <= length(to_visit))
        {
            datum_bt_node node = to_visit[index++] as datum_bt_node;
            await node.reset_tick_state();
            node.collect_reset_children(to_visit);
        }
    }

    /**
     * Assigns pre-order depth-first execution indices to this node and its subtree.
     * Called once per controller tree by finalize_tree().
     */
    // tg: code/datums/ai/_ai_bt_node.dm:48
    public virtual object assign_execution_indices(object counter = null)
    {
        execution_index = Dm.num(counter);
        last_execution_index = Dm.num(counter);
        return Dm.num(counter) + 1;
    }

    /// Apply a configuration list to this node instance by assigning vars directly.
    // tg: code/datums/ai/_ai_bt_node.dm:54
    public virtual void configure(DmList config = null)
    {
        foreach (object var_name in Dm.each(config))
            Dm.set_var(this, (string)var_name, config[var_name]);
    }

    /**
     * Returns the list of direct child bt_node instances for tree traversal.
     * Returns null for leaf nodes (default). Overridden in composites, decorators, and subtrees.
     */
    // tg: code/datums/ai/_ai_bt_node.dm:62
    public virtual object get_children()
    {
        return null;
    }

    /// Returns TRUE if this node or any descendant has an active (running) ai_behavior leaf.
    // tg: code/datums/ai/_ai_bt_node.dm:66
    public virtual object has_active_descendants()
    {
        return FALSE;
    }

    /// Walks descendants to find the node with the given execution_index. Returns null if not found.
    // tg: code/datums/ai/_ai_bt_node.dm:70
    public virtual object find_by_index(object target_index = null)
    {
        if (Dm.eq(execution_index, target_index))
            return this;
        DmList ch = get_children() as DmList;
        if (!Dm.truthy(ch))
            return null;
        foreach (datum_bt_node child in Dm.each_cast<datum_bt_node>(ch))
        {
            object found = child.find_by_index(target_index);
            if (Dm.truthy(found))
                return found;
        }
        return null;
    }

    /// Appends this node's active/upcoming state to lines for display. No-op for plain leaf nodes.
    // tg: code/datums/ai/_ai_bt_node.dm:83
    public virtual void append_active_nodes(DmList lines = null, object indent = null)
    {
    }

    /// Called during finalize_tree() to set owning_controller, register overrides, and enqueue children.
    // tg: code/datums/ai/_ai_bt_node.dm:87
    public virtual void finalize_node(datum_ai_controller controller = null, DmList to_visit = null)
    {
        owning_controller = controller;
    }

    /// Called during build_node_from_descriptor() to resolve and assign child nodes from JSON descriptors.
    // tg: code/datums/ai/_ai_bt_node.dm:91
    public virtual void set_descriptor_children(DmList children_descs = null, datum_ai_controller controller = null)
    {
    }

    /// Returns a single-character status marker for display. Overridden by ai_behavior to check running.
    // tg: code/datums/ai/_ai_bt_node.dm:95
    public virtual object get_status_marker()
    {
        return "o";
    }

    /// Appends this node's full tree state (status + label + children) to lines for display.
    // tg: code/datums/ai/_ai_bt_node.dm:99
    public virtual void append_full_tree_state(DmList lines = null, object indent = null)
    {
        Dm.append(ref lines, Dm.text("[][] []", indent, get_status_marker(), label));
    }

    /// Adds all children that must be visited during reset to to_visit. No-op for leaf nodes.
    // tg: code/datums/ai/_ai_bt_node.dm:103
    public virtual void collect_reset_children(DmList to_visit = null)
    {
    }

    // tg: code/datums/ai/_ai_bt_node.dm:106
    public override object Destroy(float? force = null)
    {
        parent_node = null;
        owning_controller = null;
        return base.Destroy(force);
    }
}
