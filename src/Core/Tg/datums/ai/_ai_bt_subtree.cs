// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/_ai_bt_subtree.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1). The var block is generated.

/**
 * Subtree node: a named, re-usable BT subgraph.
 *
 * Subtypes override New() to build an arbitrary internal tree and assign its root node
 * to `root`. tick() delegates entirely to root.tick() and returns whatever it returns.
 *
 * This decouples the subtree's identity (its type path) from any specific composite
 * semantics (selector/parallel/sequence). The root can be any node type.
 *
 * The controller's tree-walk helpers (setup_bt_observers, reset_bt_tick_states) descend
 * through the `root` pointer to reach all internal nodes.
 */
// tg: code/datums/ai/_ai_bt_subtree.dm:13
public partial class datum_bt_node_subtree : datum_bt_node
{
    // tg: code/datums/ai/_ai_bt_subtree.dm:31
    public override object Destroy(float? force = null)
    {
        if (override_id != null && LAZYACCESS(owning_controller?.override_slots, override_id) == this)
            LAZYREMOVE(ref owning_controller.override_slots, override_id);
        QDEL_NULL(ref root);
        QDEL_NULL(ref override_node);
        return base.Destroy(force);
    }

    // tg: code/datums/ai/_ai_bt_subtree.dm:38
    public override object tick(datum_ai_controller controller = null, object seconds_per_tick = null)
    {
        if (override_node != null)
            return override_node.tick(controller, seconds_per_tick);
        if (root == null)
            return BT_FAILURE;
        return root.tick(controller, seconds_per_tick);
    }

    // tg: code/datums/ai/_ai_bt_subtree.dm:45
    public override object get_children()
    {
        if (override_node != null)
            return override_node.root != null ? Dm.list(override_node.root) : null;
        return root != null ? Dm.list(root) : null;
    }

    // tg: code/datums/ai/_ai_bt_subtree.dm:50
    public override object has_active_descendants()
    {
        if (override_node != null)
            return override_node.has_active_descendants();
        return Dm.num(root != null && Dm.truthy(root.has_active_descendants()));
    }

    // tg: code/datums/ai/_ai_bt_subtree.dm:55
    public override void finalize_node(datum_ai_controller controller = null, DmList to_visit = null)
    {
        base.finalize_node(controller, to_visit);
        if (override_id != null)
        {
            LAZYINITLIST(ref controller.override_slots);
            controller.override_slots[override_id] = this;
        }
        if (root != null)
        {
            root.parent_node = this;
            Dm.append(ref to_visit, root);
        }
        if (override_node != null)
        {
            override_node.parent_node = this;
            Dm.append(ref to_visit, override_node);
        }
    }

    // tg: code/datums/ai/_ai_bt_subtree.dm:67
    public override void append_active_nodes(DmList lines = null, object indent = null)
    {
        if (root != null && Dm.truthy(root.has_active_descendants()))
            root.append_active_nodes(lines, indent);
    }

    // tg: code/datums/ai/_ai_bt_subtree.dm:71
    public override void collect_reset_children(DmList to_visit = null)
    {
        if (root != null)
            Dm.append(ref to_visit, root);
        if (override_node != null)
            Dm.append(ref to_visit, override_node);
    }

    // tg: code/datums/ai/_ai_bt_subtree.dm:77
    public override void append_full_tree_state(DmList lines = null, object indent = null)
    {
        base.append_full_tree_state(lines, indent);
        if (root != null)
            root.append_full_tree_state(lines, Dm.text("[]  ", indent));
    }

    // tg: code/datums/ai/_ai_bt_subtree.dm:82
    public override object assign_execution_indices(object counter = null)
    {
        execution_index = Dm.num(counter);
        float c = Dm.num(counter) + 1;
        if (root != null)
            c = Dm.num(root.assign_execution_indices(c));
        if (override_node != null)
            c = Dm.num(override_node.assign_execution_indices(c));
        last_execution_index = c - 1;
        return c;
    }
}
