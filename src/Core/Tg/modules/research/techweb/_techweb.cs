// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/techweb/_techweb.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI3 (D326): the techweb, whole but the scientific papers' publication (add_scientific_paper: the NT Frontier program and the
// ordnance papers, a later research campaign, a live-partial row). The station's science techweb is made by SSresearch with its starter
// nodes researched; the exosuit fabricator reads its designs; a researched node pays the science budget its bounty (gaps P4a).
// The var block is generated (D111).

/**
 * # Techweb
 *
 * A datum representing a research techweb
 *
 * Techweb datums are meant to store unlocked research, being able to be stored
 * on research consoles, servers, and disks. They are NOT global.
 */
// tg: code/modules/research/techweb/_techweb.dm:9
public partial class datum_techweb : datum
{
    // tg: code/modules/research/techweb/_techweb.dm:77
    public override async DmTask New(params object[] __extra)
    {
        Dm.append(ref SSresearch.techwebs, this);
        foreach (object node_path in Dm.each(SSresearch.techweb_nodes_starting))
            await research_node(SSresearch.techweb_nodes[node_path], TRUE, FALSE, FALSE);
        hidden_nodes = SSresearch.techweb_nodes_hidden.Copy();
        initialize_published_papers();
        await base.New(__extra);
    }

    // tg: code/modules/research/techweb/_techweb.dm:85
    public override object Destroy(float? force = null)
    {
        researched_nodes = null;
        researched_designs = null;
        available_nodes = null;
        visible_nodes = null;
        custom_designs = null;
        Dm.remove(ref SSresearch.techwebs, this);
        return base.Destroy(force);
    }

    // tg: code/modules/research/techweb/_techweb.dm:94
    public virtual void recalculate_nodes(float? recalculate_designs = null)
    {
        recalculate_designs ??= FALSE;
        if (Dm.truthy(recalculate_designs))
            researched_designs = custom_designs.Copy();
        DmList all_of_our_nodes = Dm.bitor(Dm.bitor(researched_nodes, visible_nodes), available_nodes) as DmList;
        foreach (object node_path in Dm.each(all_of_our_nodes))
        {
            update_node_status(SSresearch.techweb_nodes[node_path] as datum_techweb_node);
            CHECK_TICK();
        }
    }

    /// Adjusts the amount of a certain point type
    // tg: code/modules/research/techweb/_techweb.dm:103
    public virtual void adjust_points(object point_type = null, object point_amount = null)
    {
        if (!Dm.truthy(SSresearch.point_types[point_type]) || !Dm.truthy(point_amount))
            return;
        research_points[point_type] = max(FLOOR(Dm.num(research_points[point_type]) + Dm.num(point_amount), 0.1f), 0);
    }

    /// Adjusts the amount of multiple point types
    // tg: code/modules/research/techweb/_techweb.dm:109
    public virtual void adjust_multiple_points(DmList point_list = null)
    {
        foreach (object point_type in Dm.each(point_list))
            adjust_points(point_type, point_list[point_type]);
    }

    /// Adjust all point types by this amount
    // tg: code/modules/research/techweb/_techweb.dm:114
    public virtual void adjust_all_points(object point_amount = null)
    {
        DmList new_points = SSresearch.point_types.Copy();
        foreach (object point_type in Dm.each(new_points))
            new_points[point_type] = point_amount;
        adjust_multiple_points(new_points);
    }

    /// Takes all of the research from a tech disk
    // tg: code/modules/research/techweb/_techweb.dm:121
    public virtual async DmTask absorb_techdisk(obj_item_disk_tech_disk disk = null)
    {
        foreach (object node_path in Dm.each(Dm.sub(disk.stored_nodes, researched_nodes)))
        {
            CHECK_TICK();
            await research_node(node_path, TRUE, FALSE, FALSE);
        }
        recalculate_nodes();
    }

    // tg: code/modules/research/techweb/_techweb.dm:127
    public virtual object get_visible_nodes() //The way this is set up is shit but whatever.
    {
        return Dm.sub(visible_nodes, hidden_nodes);
    }

    // tg: code/modules/research/techweb/_techweb.dm:130
    public virtual object get_available_nodes()
    {
        return Dm.sub(available_nodes, hidden_nodes);
    }

    // tg: code/modules/research/techweb/_techweb.dm:133
    public virtual object get_researched_nodes()
    {
        return Dm.sub(researched_nodes, hidden_nodes);
    }

    /**
     * Adds a design to this techweb
     *
     * Arguments:
     * * design - The instance or typepath of the design to add
     * * custom - Boolean on whether the node should also be added to custom_designs
     * * add_to - A custom list to add the node to, overwriting research_designs.
     */
    // tg: code/modules/research/techweb/_techweb.dm:144
    public virtual bool add_design(object design = null, float? custom = null, DmList add_to = null)
    {
        custom ??= FALSE;
        if (ispath(design))
            design = SSresearch.techweb_designs[design];
        if (design is not datum_design added)
            return false;

        if (Dm.truthy(custom))
            custom_designs[added.type] = TRUE;

        if (add_to != null)
            add_to[added.type] = TRUE;
        else
            researched_designs[added.type] = TRUE;

        foreach (object unlocked_by in Dm.each(added.unlocked_by))
            Dm.remove(ref hidden_nodes, unlocked_by);

        if (Dm.truthy(_listen_lookup?[COMSIG_TECHWEB_ADD_DESIGN]))
            _SendSignal(COMSIG_TECHWEB_ADD_DESIGN, Dm.list(added, custom));
        return true;
    }

    /**
     * Removes a design from this techweb
     *
     * Arguments:
     * * design - The instance or typepath of the design to remove
     * * custom - Boolean on whether the node should also be removed from custom_designs
     */
    // tg: code/modules/research/techweb/_techweb.dm:171
    public virtual bool remove_design(object design = null, float? custom = null)
    {
        custom ??= FALSE;
        if (ispath(design))
            design = SSresearch.techweb_designs[design];
        if (design is not datum_design removed)
            return false;

        if (Dm.truthy(custom))
            Dm.remove(ref custom_designs, removed.type);

        Dm.remove(ref researched_designs, removed.type);

        if (Dm.truthy(_listen_lookup?[COMSIG_TECHWEB_REMOVE_DESIGN]))
            _SendSignal(COMSIG_TECHWEB_REMOVE_DESIGN, Dm.list(removed, custom));
        return true;
    }

    // tg: code/modules/research/techweb/_techweb.dm:185
    public virtual bool can_afford(DmList point_list = null)
    {
        foreach (object point_type in Dm.each(point_list))
        {
            if (Dm.lt(research_points[point_type] ?? 0, point_list[point_type]))
                return false;
        }
        return true;
    }

    /**
     * Checks if all experiments have been completed for a given node on this techweb
     *
     * Arguments:
     * * node - the node to check
     */
    // tg: code/modules/research/techweb/_techweb.dm:197
    public virtual bool have_experiments_for_node(datum_techweb_node node = null)
    {
        foreach (object experiment_type in Dm.each(node.required_experiments))
        {
            if (!Dm.truthy(completed_experiments[experiment_type]))
                return false;
        }
        return true;
    }

    /**
     * Checks if a node can be unlocked on this techweb, having the required points and experiments
     *
     * Arguments:
     * * node - the node to check
     */
    // tg: code/modules/research/techweb/_techweb.dm:209
    public virtual bool can_unlock_node(datum_techweb_node node = null)
    {
        return can_afford(node.get_price(this)) && have_experiments_for_node(node);
    }

    /**
     * Adds an experiment to this techweb by its type, ensures that no duplicates are added.
     *
     * Arguments:
     * * experiment_type - the type of the experiment to add
     */
    // tg: code/modules/research/techweb/_techweb.dm:218
    public virtual bool add_experiment(object experiment_type = null)
    {
        // check active experiments for experiment of this type
        foreach (datum_experiment experiment in Dm.each_cast<datum_experiment>(available_experiments))
        {
            if (Dm.eq(experiment.type, experiment_type))
                return false;
        }
        // check completed experiments for experiments of this type
        foreach (object completed_experiment_type in Dm.each(completed_experiments))
        {
            if (Dm.eq(completed_experiment_type, experiment_type))
                return false;
        }
        Dm.append(ref available_experiments, Dm.@new((Type)experiment_type, this));
        return true;
    }

    /**
     * Adds a list of experiments to this techweb by their types, ensures that no duplicates are added.
     *
     * Arguments:
     * * experiment_list - the list of types of experiments to add
     */
    // tg: code/modules/research/techweb/_techweb.dm:236
    public virtual bool add_experiments(DmList experiment_list = null)
    {
        bool dot = true;
        foreach (object experiment in Dm.each(experiment_list))
        {
            if (!add_experiment(experiment))
                dot = false; // don't return here because add_experiment() does important stuff
        }
        return dot;
    }

    /**
     * Notifies the techweb that an experiment has been completed, updating internal state of the techweb to reflect this.
     *
     * Arguments:
     * * completed_experiment - the experiment which was completed
     */
    // tg: code/modules/research/techweb/_techweb.dm:248
    public virtual object complete_experiment(datum_experiment completed_experiment = null)
    {
        Dm.remove(ref available_experiments, completed_experiment);
        completed_experiments[completed_experiment.type] = completed_experiment;

        string result_text = Dm.text("[] has been completed", completed_experiment);
        object refund = skipped_experiment_types[completed_experiment.type] ?? 0;
        if (Dm.num(refund) > 0)
        {
            adjust_points(TECHWEB_POINT_TYPE_GENERIC, refund);
            result_text += Dm.text(", refunding [] points", refund);
            // Nothing more to gain here, but we keep it in the list to prevent double dipping
            skipped_experiment_types[completed_experiment.type] = -1;
        }
        string points_rewarded = null;
        if (Dm.truthy(LAZYLEN(completed_experiment.points_reward)))
        {
            adjust_multiple_points(completed_experiment.points_reward);
            points_rewarded = Dm.text(",[] rewarding []", Dm.num(refund) > 0 ? " and" : "", completed_experiment.get_points_reward_text());
            result_text += points_rewarded;
        }
        result_text += "!";

        if (Dm.truthy(_listen_lookup?[COMSIG_TECHWEB_EXPERIMENT_COMPLETED]))
            _SendSignal(COMSIG_TECHWEB_EXPERIMENT_COMPLETED, Dm.list(completed_experiment));
        log_research(Dm.text("[] ([]) has been completed on techweb []/[][][].", completed_experiment.name, completed_experiment.type, id, organization, Dm.truthy(refund) ? Dm.text(", refunding [] points", refund) : "", points_rewarded));
        return result_text;
    }

    // tg: code/modules/research/techweb/_techweb.dm:270
    public virtual object printout_points()
    {
        return techweb_point_display_generic(research_points);
    }

    /// Adds a node to this techweb's research queue
    // tg: code/modules/research/techweb/_techweb.dm:274
    public virtual bool enqueue_node(object node_path = null, mob user = null)
    {
        bool queue_first = false;
        if (ishuman(user))
        {
            mob_living_carbon_human human_user = user as mob_living_carbon_human;
            DmList access = human_user.wear_id?.GetAccess() as DmList;
            if (Dm.@in(ACCESS_RD, access))
                queue_first = true;
        }

        if (Dm.truthy(research_queue_nodes[node_path]))
        {
            if (!queue_first)
                return false;
            // Remove to be able to place first
            Dm.remove(ref research_queue_nodes, node_path); // Remove to be able to place first
        }

        object user_weakref = WEAKREF(user);
        foreach (object queued_node in Dm.each(research_queue_nodes.Copy()))
        {
            object node_queuer = research_queue_nodes[queued_node];
            if (Dm.eq(node_queuer, user_weakref))
                Dm.remove(ref research_queue_nodes, queued_node);
        }

        if (queue_first)
            research_queue_nodes.Insert(1, node_path);
        research_queue_nodes[node_path] = user_weakref;

        return true;
    }

    // tg: code/modules/research/techweb/_techweb.dm:299
    public virtual bool dequeue_node(object node_path = null, mob user = null)
    {
        datum_weakref queuer_weakref = research_queue_nodes[node_path] as datum_weakref;
        mob queuer = queuer_weakref?.resolve() as mob;
        if (queuer == null || queuer != user)
            return false;
        Dm.remove(ref research_queue_nodes, node_path);
        return true;
    }

    /**
     * Adds a node to this techweb
     *
     * Arguments:
     * * node - The typepath or instance of the node to research
     * * force - If TRUE, availability checks are ignored
     * * auto_adjust_cost - If TRUE, the node's point cost(s) are deducted from this techweb
     * * get_that_dosh - If TRUE, the science budget gains [SSeconomy.techweb_bounty] moneys
     * * research_source - The thing responsible for researching this node. Used in overrides.
     */
    // tg: code/modules/research/techweb/_techweb.dm:317
    public virtual async DmTask<bool> research_node(object node = null, float? force = null, float? auto_adjust_cost = null, float? get_that_dosh = null, atom research_source = null)
    {
        force ??= FALSE;
        auto_adjust_cost ??= TRUE;
        get_that_dosh ??= TRUE;
        if (ispath(node))
            node = SSresearch.techweb_nodes[node];
        if (node is not datum_techweb_node researched)
            return false;

        update_node_status(researched);
        if (!Dm.truthy(force) && (!Dm.truthy(available_nodes[researched.type]) || (Dm.truthy(auto_adjust_cost) && !can_afford(researched.get_price(this))) || !have_experiments_for_node(researched)))
            return false;

        string log_message = Dm.text("[]/[] researched node []", id, organization, researched.type);
        if (Dm.truthy(auto_adjust_cost))
        {
            DmList node_cost = researched.get_price(this);
            foreach (object point_type in Dm.each(node_cost))
                node_cost[point_type] = Dm.num(node_cost[point_type]) * -1;
            adjust_multiple_points(node_cost);
            log_message += Dm.text(" at the cost of []", json_encode(node_cost));
        }

        //Add to our researched list
        researched_nodes[researched.type] = TRUE;

        // Track any experiments we skipped relating to this
        foreach (object missed_experiment in Dm.each(researched.discount_experiments))
        {
            if (Dm.truthy(completed_experiments[missed_experiment]) || Dm.truthy(skipped_experiment_types[missed_experiment]))
                continue;
            skipped_experiment_types[missed_experiment] = researched.discount_experiments[missed_experiment];
        }

        // Gain the experiments from the new node
        foreach (object unlocked_path in Dm.each(researched.unlocked_nodes))
        {
            visible_nodes[unlocked_path] = TRUE;
            datum_techweb_node unlocked_node = SSresearch.techweb_nodes[unlocked_path] as datum_techweb_node;
            if (Dm.truthy(LAZYLEN(unlocked_node.required_experiments)))
                add_experiments(unlocked_node.required_experiments);
            if (Dm.truthy(LAZYLEN(unlocked_node.discount_experiments)))
                add_experiments(unlocked_node.discount_experiments);
            update_node_status(unlocked_node);
        }

        // Gain more new experiments
        if (Dm.truthy(LAZYLEN(researched.experiments_to_unlock)))
            add_experiments(researched.experiments_to_unlock);

        // Unlock what the research actually unlocks
        foreach (object design_path in Dm.each(researched.unlocked_designs))
            add_design(design_path);
        update_node_status(researched);
        if (Dm.truthy(get_that_dosh))
        {
            datum_bank_account science_department_bank_account = SSeconomy.get_dep_account(ACCOUNT_SCI);
            science_department_bank_account?.adjust_money(SSeconomy.techweb_bounty);
            log_message += Dm.text(", gaining [] to [] for it.", SSeconomy.techweb_bounty, science_department_bank_account);
        }

        // Avoid logging the same 300+ lines at the beginning of every round
        if (MC_RUNNING())
            log_research(log_message);

        // Dequeue
        Dm.remove(ref research_queue_nodes, researched.type);

        return true;
    }

    /**
     * Removes a node from this techweb
     *
     * Arguments:
     * * node_path - The instance or typepath of the node to remove
     */
    // tg: code/modules/research/techweb/_techweb.dm:382
    public virtual void unresearch_node(object node_path = null)
    {
        if (node_path is datum_techweb_node node)
            node_path = node.type;

        Dm.remove(ref researched_nodes, node_path);
        recalculate_nodes(recalculate_designs: TRUE); //Fully rebuild the tree.
    }

    /// Boosts a techweb node.
    // tg: code/modules/research/techweb/_techweb.dm:390
    public virtual bool boost_techweb_node(datum_techweb_node node = null, DmList point_list = null)
    {
        if (node == null)
            return false;
        LAZYINITLIST(ref node.discount_boosts);
        foreach (object point_type in Dm.each(point_list)) // Essentially applies the greater boost(s) between the newer and any existing.
            node.discount_boosts[point_type] = max(Dm.num(node.discount_boosts[point_type]), Dm.num(point_list[point_type]));
        boosted_nodes[node.type] = TRUE;
        unhide_node(node);
        update_node_status(node);
        return true;
    }

    ///Removes a node from the hidden_nodes list, making it viewable and researchable (if no experiments are required).
    // tg: code/modules/research/techweb/_techweb.dm:402
    public virtual bool unhide_node(datum_techweb_node node = null)
    {
        if (node == null)
            return false;
        Dm.remove(ref hidden_nodes, node.type);
        ///Make it available if the prereq ids are already researched
        update_node_status(node);
        return true;
    }

    // tg: code/modules/research/techweb/_techweb.dm:410
    public virtual void update_tiers(datum_techweb_node @base = null)
    {
        DmList current = Dm.list(@base);
        while (Dm.truthy(length(current)))
        {
            DmList next = Dm.list();
            foreach (datum_techweb_node node in Dm.each_cast<datum_techweb_node>(current))
            {
                float tier = 0;
                if (!Dm.truthy(researched_nodes[node.type])) // researched is tier 0
                {
                    foreach (object prerequisite_path in Dm.each(node.prerequisite_nodes))
                    {
                        float prereq_tier = Dm.num(tiers[prerequisite_path]);
                        tier = max(tier, prereq_tier + 1);
                    }
                }

                if (!Dm.eq(tier, tiers[node.type]))
                {
                    tiers[node.type] = tier;
                    foreach (object unlocked_path in Dm.each(node.unlocked_nodes))
                        Dm.append(ref next, SSresearch.techweb_nodes[unlocked_path]);
                }
            }
            current = next;
        }
    }

    // tg: code/modules/research/techweb/_techweb.dm:427
    public virtual void update_node_status(datum_techweb_node node = null)
    {
        bool researched = false;
        bool available = false;
        bool visible = false;
        if (Dm.truthy(researched_nodes[node.type]))
            researched = true;
        float needed = LAZYLEN(node.prerequisite_nodes);
        foreach (object prerequisite_path in Dm.each(node.prerequisite_nodes))
        {
            if (Dm.truthy(researched_nodes[prerequisite_path]))
            {
                visible = true;
                needed--;
            }
        }
        if (!Dm.truthy(needed))
            available = true;
        Dm.remove(ref researched_nodes, node.type);
        Dm.remove(ref available_nodes, node.type);
        Dm.remove(ref visible_nodes, node.type);
        if (Dm.truthy(hidden_nodes[node.type])) //Hidden.
            return;

        if (researched)
        {
            researched_nodes[node.type] = TRUE;
            DmList Dear_tg_station_13_maintainers_COMMA_I_am_writing_this_variable_like_this_because_I_am_curious_if_you_guys_actually_review_the_code_that_comes_across_your_desk_PERIOD_If_you_see_this_COMMA_please_let_me_know = LAZYCOPY(node.unlocked_designs);
            foreach (object design_path in Dm.each(Dm.sub(Dear_tg_station_13_maintainers_COMMA_I_am_writing_this_variable_like_this_because_I_am_curious_if_you_guys_actually_review_the_code_that_comes_across_your_desk_PERIOD_If_you_see_this_COMMA_please_let_me_know, researched_designs)))
                add_design(SSresearch.techweb_designs[design_path]);
        }
        else if (available)
            available_nodes[node.type] = TRUE;
        else if (visible)
            visible_nodes[node.type] = TRUE;

        update_tiers(node);
    }

    /// Fill published_papers with nulls.
    // tg: code/modules/research/techweb/_techweb.dm:459
    public virtual void initialize_published_papers()
    {
        published_papers = Dm.list();
        scientific_cooperation = Dm.list();
        foreach (datum_experiment_ordnance ordnance_experiment in Dm.each_cast<datum_experiment_ordnance>(SSresearch.ordnance_experiments))
        {
            float max_tier = min(length(ordnance_experiment.gain), length(ordnance_experiment.target_amount));
            DmList tier_list = Dm.list_n(max_tier);
            published_papers[ordnance_experiment.type] = tier_list;
        }
        foreach (datum_scientific_partner partner in Dm.each_cast<datum_scientific_partner>(SSresearch.scientific_partners))
            scientific_cooperation[partner.type] = 0;
    }

    /// Returns a flat list of all design datums this techweb has researched.
    // tg: code/modules/research/techweb/_techweb.dm:493
    public virtual DmList get_researched_design_datums()
    {
        DmList designs = Dm.list();
        foreach (object design_path in Dm.each(researched_designs))
            Dm.append(ref designs, SSresearch.techweb_designs[design_path]);
        return designs;
    }
}
