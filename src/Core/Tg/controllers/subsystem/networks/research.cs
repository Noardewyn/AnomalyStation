// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/networks/research.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI3 (D326): the research subsystem, whole: the design and node tables, the station's science techweb (its starter nodes
// researched), the point generation and the research queue. Since gaps RD2 the R&D servers mine the points and the R&D consoles
// research and enqueue nodes. The var block is generated (D111).

// tg: code/controllers/subsystem/networks/research.dm:1 SUBSYSTEM_DEF(research)
public partial class datum_controller_subsystem_research : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(research) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL(SSresearch) expanded inline.
    // tg: code/controllers/subsystem/networks/research.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSresearch != this)
        {
            if (SSresearch is datum_controller_subsystem_research)
            {
                Recover();
                qdel(SSresearch);
            }
            SSresearch = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/networks/research.dm:66
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        initialize_designs();
        initialize_nodes();
        initialize_ordnance_experiments();
        Dm.@new<datum_techweb_science>();
        return SS_INIT_SUCCESS;
    }

    // tg: code/controllers/subsystem/networks/research.dm:73
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        foreach (datum_techweb techweb in Dm.each_cast<datum_techweb>(techwebs))
        {
            if (!Dm.truthy(techweb.should_generate_points))
                continue;

            // Point generation
            DmList bitcoins = Dm.list();
            foreach (obj_machinery_rnd_server miner in Dm.each_cast<obj_machinery_rnd_server>(techweb.techweb_servers))
            {
                if (Dm.truthy(miner.working))
                {
                    bitcoins = single_server_income.Copy();
                    break; //Just need one to work.
                }
            }

            if (techweb.last_income != null)
            {
                float income_time_difference = world.time - Dm.num(techweb.last_income);
                techweb.last_bitcoins = bitcoins; // Doesn't take tick drift into account
                foreach (object point_type in Dm.each(bitcoins))
                    bitcoins[point_type] = Dm.num(bitcoins[point_type]) * (income_time_difference / 10) * techweb.income_modifier;
                techweb.adjust_multiple_points(bitcoins);
            }

            techweb.last_income = world.time;

            // Research queue
            if (Dm.truthy(length(techweb.research_queue_nodes)))
            {
                await techweb.research_node(techweb.research_queue_nodes[1]); // Attempt to research the first node in queue if possible

                foreach (object node_path in Dm.each(techweb.research_queue_nodes))
                {
                    datum_techweb_node node = techweb_nodes[node_path] as datum_techweb_node;
                    if (node.is_free(techweb)) // Automatically research all free nodes in queue if any
                        await techweb.research_node(node);
                }
            }
        }
    }

    // tg: code/controllers/subsystem/networks/research.dm:103
    public virtual void initialize_designs()
    {
        foreach (Type design_path in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_design))))
        {
            datum_design new_design = Dm.@new(design_path) as datum_design;
            // This design doesn't want to exist. OK THEN!
            if (QDELING(new_design))
                continue;

            Type build_path = Dm.initial(design_path, (datum_design d) => d.build_path);
            if (build_path != null)
            {
                // LAZYADD(item_to_design[build_path], design_path)
                if (item_to_design[build_path] is not DmList builders)
                    item_to_design[build_path] = builders = Dm.list();
                Dm.append(ref builders, design_path);
            }

            techweb_designs[design_path] = new_design;
        }
    }

    // tg: code/controllers/subsystem/networks/research.dm:116
    public virtual void initialize_nodes()
    {
        foreach (Type node_path in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_techweb_node))))
        {
            datum_techweb_node new_node = Dm.@new(node_path) as datum_techweb_node;
            // This node doesn't want to exist. OK THEN!
            if (QDELING(new_node))
                continue;

            if (Dm.truthy(Dm.bitand(new_node.node_flags, TECHWEB_NODE_STARTER)))
                Dm.append(ref techweb_nodes_starting, node_path);
            if (Dm.truthy(Dm.bitand(new_node.node_flags, TECHWEB_NODE_HIDDEN)))
                techweb_nodes_hidden[node_path] = TRUE; // assoc for speed
            if (Dm.truthy(Dm.bitand(new_node.node_flags, TECHWEB_NODE_EXPERIMENTAL)))
                Dm.append(ref techweb_nodes_experimental, node_path);

            // Tell our unlocked designs that we unlock them
            foreach (object design_path in Dm.each(new_node.unlocked_designs))
            {
                datum_design unlocked_design = techweb_designs[design_path] as datum_design;
                LAZYADD(ref unlocked_design.unlocked_by, node_path);
            }

            // Register that our required items unlock us
            foreach (object required_item_path in Dm.each(new_node.required_items_to_unlock))
            {
                // LAZYADD(techweb_unlock_items[required_item_path], node_path)
                if (techweb_unlock_items[required_item_path] is not DmList unlockers)
                    techweb_unlock_items[required_item_path] = unlockers = Dm.list();
                Dm.append(ref unlockers, node_path);
            }

            techweb_nodes[node_path] = new_node;
        }

        // We have to do this after the above loop to ensure everything has been initialized
        foreach (object node_path in Dm.each(techweb_nodes))
        {
            datum_techweb_node node = techweb_nodes[node_path] as datum_techweb_node;
            // Tell our prerequisite nodes that we are unlocked by them
            foreach (object prerequisite_path in Dm.each(node.prerequisite_nodes))
            {
                datum_techweb_node prerequisite_node = techweb_nodes[prerequisite_path] as datum_techweb_node;
                LAZYADD(ref prerequisite_node.unlocked_nodes, node_path);
            }
        }
    }

    // tg: code/controllers/subsystem/networks/research.dm:149
    public virtual void initialize_ordnance_experiments()
    {
        foreach (Type experiment_path in Dm.each_cast<Type>(subtypesof(typeof(datum_experiment_ordnance))))
        {
            if (Dm.truthy(Dm.initial(experiment_path, (datum_experiment_ordnance e) => e.experiment_proper)))
                Dm.append(ref ordnance_experiments, Dm.@new(experiment_path));
        }

        foreach (Type partner_path in Dm.each_cast<Type>(subtypesof(typeof(datum_scientific_partner))))
        {
            datum_scientific_partner partner = Dm.@new(partner_path) as datum_scientific_partner;
            if (!Dm.truthy(length(partner.accepted_experiments)))
            {
                foreach (datum_experiment_ordnance ordnance_experiment in Dm.each_cast<datum_experiment_ordnance>(ordnance_experiments))
                    Dm.append(ref partner.accepted_experiments, ordnance_experiment.type);
            }
            Dm.append(ref scientific_partners, partner);
        }
    }

    /**
     * Goes through all techwebs and goes through their servers to find ones on a valid z-level
     * Returns the full list of all techweb servers.
     */
    // tg: code/controllers/subsystem/networks/research.dm:165
    public virtual DmList get_available_servers(turf location = null)
    {
        DmList local_servers = Dm.list();
        if (location != null)
        {
            foreach (datum_techweb individual_techweb in Dm.each_cast<datum_techweb>(techwebs))
                Dm.append(ref local_servers, find_valid_servers(location, individual_techweb));
        }
        return local_servers;
    }

    /**
     * Goes through an individual techweb's servers and finds one on a valid z-level
     * Returns a list of existing ones, or an empty list otherwise.
     * Args:
     * - checking_web - The techweb we're checking the servers of.
     */
    // tg: code/controllers/subsystem/networks/research.dm:178
    public virtual DmList find_valid_servers(turf location = null, datum_techweb checking_web = null)
    {
        DmList valid_servers = Dm.list();
        foreach (obj_machinery_rnd_server server in Dm.each_cast<obj_machinery_rnd_server>(checking_web.techweb_servers))
        {
            if (!Dm.truthy(is_valid_z_level(get_turf(server), location)))
                continue;
            Dm.append(ref valid_servers, server);
        }
        return valid_servers;
    }

    /// Returns true if you can make an anomaly core of the provided type
    // tg: code/controllers/subsystem/networks/research.dm:187
    public virtual bool is_core_available(object core_type = null)
    {
        if (!ispath(core_type, typeof(obj_item_assembly_signaler_anomaly)))
            return false; // The fuck are you checking this random object for?
        float already_made = Dm.num(created_anomaly_types[core_type] ?? 0);
        object hard_limit = anomaly_hard_limit_by_type[core_type];
        return already_made < Dm.num(hard_limit);
    }

    /// Increase our tracked number of cores of this type
    // tg: code/controllers/subsystem/networks/research.dm:195
    public virtual void increment_existing_anomaly_cores(object core_type = null)
    {
        float existing = Dm.num(created_anomaly_types[core_type] ?? 0);
        created_anomaly_types[core_type] = existing + 1;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/networks/research.dm:1  SUBSYSTEM_DEF(research): GLOBAL_REAL(SSresearch, /datum/controller/subsystem/research)
    internal datum_controller_subsystem_research SSresearch;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_research SSresearch => ref TgState.Current.SSresearch;

    /// Helper proc to turn an associative list of points into a more human readable format.
    // tg: code/controllers/subsystem/networks/research.dm:201
    public static object techweb_point_display_generic(DmList point_list = null)
    {
        DmList ret = Dm.list();
        foreach (object point_type in Dm.each(point_list))
        {
            object point_amount = point_list[point_type];
            Dm.append(ref ret, Dm.text("[]: []", SSresearch.point_types[point_type] ?? "ERRORED POINT TYPE", point_amount));
        }
        return ret.Join("<BR>");
    }
}
