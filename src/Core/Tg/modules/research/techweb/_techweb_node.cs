// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/techweb/_techweb_node.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI3 (D326): the techweb node, whole. The nodes themselves are var blocks (generated, D111); the few with procs are in nodes/.

/**
 * # Techweb Node
 *
 * A datum representing a researchable node in the techweb.
 *
 * Techweb nodes are GLOBAL, there should only be one instance of them in the game. Persistant
 * changes should never be made to them in-game. USE SSRESEARCH PROCS TO OBTAIN REFERENCES.
 */
// tg: code/modules/research/techweb/_techweb_node.dm:9
public partial class datum_techweb_node : datum
{
    // tg: code/modules/research/techweb/_techweb_node.dm:46
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        // Make lists associative for faster lookup
        foreach (object prerequisite_path in Dm.each(prerequisite_nodes))
            prerequisite_nodes[prerequisite_path] = TRUE;
        foreach (object design_path in Dm.each(unlocked_designs))
            unlocked_designs[design_path] = TRUE;
    }

    // tg: code/modules/research/techweb/_techweb_node.dm:54
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref SSresearch.techweb_nodes, type);
        return base.Destroy(force);
    }

    // tg: code/modules/research/techweb/_techweb_node.dm:58
    public virtual DmList get_price(datum_techweb host = null)
    {
        if (host == null)
            return research_costs;

        DmList actual_costs = LAZYCOPY(research_costs);

        if (Dm.truthy(LAZYLEN(discount_experiments)))
        {
            foreach (object cost_type in Dm.each(actual_costs))
            {
                foreach (object experiment_type in Dm.each(discount_experiments))
                {
                    object discount_amount = discount_experiments[experiment_type];
                    if (Dm.truthy(host.completed_experiments[experiment_type])) //do we have this discount_experiment unlocked?
                        actual_costs[cost_type] = Dm.num(actual_costs[cost_type]) - Dm.num(discount_amount);
                }
            }
        }

        if (Dm.truthy(LAZYLEN(discount_boosts)) && Dm.truthy(host.boosted_nodes[type])) // Boosts should be subservient to experiments.
        {
            foreach (object boost_type in Dm.each(discount_boosts))
            {
                object boost_amount = discount_boosts[boost_type];
                if (Dm.truthy(actual_costs[boost_type]))
                    actual_costs[boost_type] = max(Dm.num(actual_costs[boost_type]) - Dm.num(boost_amount), 0);
            }
        }

        return actual_costs;
    }

    /// If this node requires 0 points to research
    // tg: code/modules/research/techweb/_techweb_node.dm:78
    public virtual bool is_free(datum_techweb host = null)
    {
        return !Dm.truthy(Dm.values_sum(get_price(host)));
    }

    // tg: code/modules/research/techweb/_techweb_node.dm:81
    public virtual object price_display(datum_techweb techweb = null)
    {
        return techweb_point_display_generic(get_price(techweb));
    }

    ///Proc called when the Station (Science techweb specific) researches a node.
    // tg: code/modules/research/techweb/_techweb_node.dm:85
    public virtual async DmTask on_station_research(atom research_source = null)
    {
        DmList channels_to_use = announce_channels;
        if (research_source is obj_machinery_computer_rdconsole console)
        {
            // the board instance is circuit_value (GAP(types), _machinery.cs)
            obj_item_circuitboard_computer_rdconsole board = console.circuit_value as obj_item_circuitboard_computer_rdconsole;
            if (Dm.truthy(board.silence_announcements))
                return;
            if (Dm.truthy(Dm.bitand(board.obj_flags, EMAGGED)))
                channels_to_use = Dm.list(RADIO_CHANNEL_COMMON);
        }
        if (Dm.truthy(length(channels_to_use)) && !Dm.truthy(Dm.bitand(node_flags, TECHWEB_NODE_STARTER)))
            await aas_config_announce(typeof(datum_aas_config_entry_researched_node), Dm.list(Dm.kv("NODE", display_name)), null, channels_to_use);
    }
}
