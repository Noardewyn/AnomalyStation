// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/frontier.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the NT Frontier app (the NtosScipaper window): it writes a paper from an ordnance file on the computer or its
// disk (the doppler array's explosion records, the tank compressor's gas records), with its experiment, tier and sponsor, publishes
// it to the techweb it links to (a server's at install, or a multitool's buffered one; publishing completes the ordnance
// experiment and pays the science budget), lists the published papers and buys the partners' node boosts. The var block is
// generated (D111).

// tg: code/modules/modular_computers/file_system/programs/frontier.dm:1
public partial class datum_computer_file_program_scipaper_program : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/frontier.dm:21
    public override void on_install(datum_computer_file source = null, obj_item_modular_computer computer_installing = null, mob user = null)
    {
        base.on_install(source, computer_installing, user);
        paper_to_be = Dm.@new<datum_scientific_paper>();
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_no_default_techweb_link))) && linked_techweb == null) /* CONFIG_GET(flag/no_default_techweb_link) */
            CONNECT_TO_RND_SERVER_ROUNDSTART(ref linked_techweb, computer);
    }

    // tg: code/modules/modular_computers/file_system/programs/frontier.dm:27
    public override async DmTask<object> application_item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        await DmTask.CompletedTask;
        if (istype(tool, typeof(obj_item_multitool)))
            return multitool_act(user, tool as obj_item_multitool);
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/frontier.dm:31
    public virtual object multitool_act(mob_living user = null, obj_item_multitool used_multitool = null)
    {
        if (QDELETED(used_multitool.buffer as datum) || !istype(used_multitool.buffer, typeof(datum_techweb)))
            return ITEM_INTERACT_BLOCKING;
        linked_techweb = used_multitool.buffer as datum_techweb;
        computer.balloon_alert(user, I18n.T("buffer linked!"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/modular_computers/file_system/programs/frontier.dm:38
    [SignalHandler]
    public virtual object recheck_file_presence()
    {
        if (Dm.@in(selected_file, computer.get_files(include_disk_files: TRUE)))
            return FALSE;
        UnregisterSignal(selected_file, COMSIG_COMPUTER_FILE_DELETE);
        selected_file = null;
        paper_to_be.set_experiment();
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/frontier.dm:45
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        DmList parsed_experiments = Dm.list();
        foreach (datum_experiment_ordnance experiment in Dm.each<datum_experiment_ordnance>(SSresearch.ordnance_experiments))
        {
            DmList singular_experiment = Dm.list();
            singular_experiment["path"] = experiment.type;
            singular_experiment["name"] = experiment.name;
            singular_experiment["description"] = experiment.description;
            singular_experiment["target"] = experiment.target_amount;
            if (istype(experiment, typeof(datum_experiment_ordnance_explosive)))
            {
                singular_experiment["suffix"] = "tiles";
                singular_experiment["prefix"] = "Range";
            }
            else if (istype(experiment, typeof(datum_experiment_ordnance_gaseous)))
            {
                singular_experiment["suffix"] = "moles";
                singular_experiment["prefix"] = "Gas";
            }
            Dm.append(ref parsed_experiments, Dm.list(singular_experiment));
        }

        DmList parsed_partners = Dm.list();
        foreach (datum_scientific_partner partner in Dm.each<datum_scientific_partner>(SSresearch.scientific_partners))
        {
            DmList singular_partner = Dm.list();
            singular_partner["name"] = partner.name;
            singular_partner["flufftext"] = partner.flufftext;
            singular_partner["multipliers"] = partner.multipliers;
            singular_partner["path"] = partner.type;
            singular_partner["boostedNodes"] = Dm.list();
            singular_partner["acceptedExperiments"] = Dm.list();
            foreach ((object node_path, object discount) in Dm.pairs(partner.boostable_nodes))
            {
                datum_techweb_node node = SSresearch.techweb_nodes[node_path] as datum_techweb_node;
                Dm.append(singular_partner["boostedNodes"] as DmList, Dm.list(Dm.list(Dm.kv("name", node.display_name), Dm.kv("discount", discount), Dm.kv("id", node_path))));
            }
            foreach (object ordnance_experiment in Dm.each(partner.accepted_experiments))
                Dm.append(singular_partner["acceptedExperiments"] as DmList, Dm.initial(ordnance_experiment, (datum_experiment_ordnance p) => p.name));
            Dm.append(ref parsed_partners, Dm.list(singular_partner));
        }

        data["experimentInformation"] = parsed_experiments;
        data["partnersInformation"] = parsed_partners;
        data["coopIndex"] = SCIPAPER_COOPERATION_INDEX;
        data["fundingIndex"] = SCIPAPER_FUNDING_INDEX;
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/frontier.dm:84
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        // Program Headers:
        DmList data = Dm.list();
        data["currentTab"] = current_tab;
        data["has_techweb"] = Dm.num(linked_techweb != null);

        // First page. Form submission.
        if (current_tab == 1)
        {
            data["fileList"] = Dm.list();
            data["expList"] = Dm.list();
            data["allowedTiers"] = Dm.list();
            data["allowedPartners"] = Dm.list();
            // Both the file and experiment list are assoc lists. ID as value, display name as keys.
            foreach (datum_computer_file_data_ordnance ordnance_file in Dm.each<datum_computer_file_data_ordnance>(computer.get_files(include_disk_files: TRUE)))
                Dm.append(data["fileList"] as DmList, Dm.list(Dm.kv(ordnance_file.filename, ordnance_file.uid)));
            if (selected_file != null)
            {
                foreach (object possible_experiment in Dm.each(selected_file.possible_experiments))
                {
                    object experiment = possible_experiment;
                    Dm.append(data["expList"] as DmList, Dm.list(Dm.kv(Dm.initial(experiment, (datum_experiment_ordnance p) => p.name), experiment)));
                }
            }
            data["allowedTiers"] = paper_to_be.calculate_tier();
            foreach (object partner in Dm.each(SSresearch.scientific_partners))
            {
                datum_scientific_partner scientific_partner = partner as datum_scientific_partner;
                if (Dm.@in(paper_to_be.experiment_path, scientific_partner.accepted_experiments))
                    Dm.append(data["allowedPartners"] as DmList, Dm.list(Dm.kv(scientific_partner.name, scientific_partner.type)));
            }

            Dm.append(ref data, paper_to_be.return_gist());
            data["selectedFile"] = selected_file?.filename;
            // Renamed both of these to be more topical.
            data["selectedExperiment"] = data["experimentName"];
            Dm.remove(ref data, "experimentName");
            data["selectedPartner"] = data["partner"];
            Dm.remove(ref data, "partner");
        }
        // Second page. View previous
        else if (current_tab == 2)
        {
            data["publishedPapers"] = Dm.list();
            if (linked_techweb == null)
                return data;
            foreach (object experiment_types in Dm.each(linked_techweb.published_papers))
            {
                foreach (datum_scientific_paper paper in Dm.each<datum_scientific_paper>(linked_techweb.published_papers[experiment_types]))
                    Dm.append(data["publishedPapers"] as DmList, Dm.list(paper.return_gist()));
            }
        }
        else if (current_tab == 4)
        {
            data["purchaseableBoosts"] = Dm.list();
            data["relations"] = Dm.list();
            data["visibleNodes"] = Dm.list();
            if (linked_techweb == null)
                return data;
            DmList visible_nodes = Dm.list();
            Dm.append(ref visible_nodes, linked_techweb.get_available_nodes());
            Dm.append(ref visible_nodes, linked_techweb.get_researched_nodes());
            foreach (object id in Dm.each(visible_nodes))
            {
                if (Dm.truthy(visible_nodes[id]))
                    Dm.append(data["visibleNodes"] as DmList, id);
            }

            foreach (datum_scientific_partner partner in Dm.each_cast<datum_scientific_partner>(SSresearch.scientific_partners))
            {
                object relations = linked_techweb.scientific_cooperation[partner.type];
                DmList relations_data = data["relations"] as DmList;
                float sw__1 = round(Dm.num(relations) / SCIENTIFIC_COOPERATION_PURCHASE_MULTIPLIER); // We use points to determine these
                if (Dm.in_to(sw__1, -INFINITY, 0))
                    relations_data[partner.type] = "Nonexistant";
                else if (Dm.in_to(sw__1, 1, 2499))
                    relations_data[partner.type] = "Negligible";
                else if (Dm.in_to(sw__1, 2500, 4999))
                    relations_data[partner.type] = "Limited";
                else if (Dm.in_to(sw__1, 5000, 9999))
                    relations_data[partner.type] = "Cordial";
                else if (Dm.in_to(sw__1, 10000, 19999))
                    relations_data[partner.type] = "Partners";
                else if (Dm.in_to(sw__1, 20000, INFINITY))
                    relations_data[partner.type] = "Devoted";
                else
                    relations_data[partner.type] = "Undefined";
                DmList boosts_data = data["purchaseableBoosts"] as DmList;
                boosts_data[partner.type] = Dm.list();
                object displayable_nodes = Dm.add(linked_techweb.get_available_nodes(), linked_techweb.get_researched_nodes());
                foreach (object node_id in Dm.each(displayable_nodes))
                {
                    // Not from our partner
                    if (!Dm.@in(node_id, partner.boostable_nodes))
                        continue;
                    if (!Dm.truthy(partner.allowed_to_boost(linked_techweb, node_id)))
                        continue;
                    Dm.append(boosts_data[partner.type] as DmList, node_id);
                }
            }
        }
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/frontier.dm:168
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.eq(action, "et_alia"))
        {
            paper_to_be.et_alia = Dm.num(!Dm.truthy(paper_to_be.et_alia));
            return TRUE;
        }
        // Handle the publication
        else if (Dm.eq(action, "publish"))
        {
            await publish();
            return TRUE;
        }
        // For every change in the input, we correspond it with the paper_data list and update it.
        else if (Dm.eq(action, "rewrite"))
        {
            if (Dm.truthy(length(@params)))
            {
                foreach (object changed_entry in Dm.each(@params))
                {
                    if (Dm.eq(changed_entry, "title"))
                        paper_to_be.title = sanitize(@params[changed_entry]);
                    if (Dm.eq(changed_entry, "author"))
                        paper_to_be.author = sanitize(@params[changed_entry]);
                    if (Dm.eq(changed_entry, "abstract"))
                        paper_to_be.@abstract = sanitize(@params[changed_entry]);
                }
                return TRUE;
            }
        }
        else if (Dm.eq(action, "change_tab"))
        {
            current_tab = Dm.num(@params["new_tab"]);
            return TRUE;
        }
        else if (Dm.eq(action, "select_file")) // Selecting new file will necessitate a change in paper type. This will be done on select_experiment and not here.
        {
            if (selected_file != null)
                UnregisterSignal(selected_file, COMSIG_COMPUTER_FILE_DELETE);
            paper_to_be.set_experiment(); // Clears the paper info.
            foreach (datum_computer_file_data_ordnance ordnance_data in Dm.each<datum_computer_file_data_ordnance>(computer.get_files(include_disk_files: TRUE)))
            {
                if (Dm.eq(ordnance_data.uid, @params["selected_uid"]))
                {
                    selected_file = ordnance_data;
                    RegisterSignal(selected_file, COMSIG_COMPUTER_FILE_DELETE, nameof(recheck_file_presence));
                    return TRUE;
                }
            }
        }
        else if (Dm.eq(action, "select_experiment"))
        {
            object ex_path = text2path(Dm.str(@params["selected_expath"]));
            object variable = selected_file.possible_experiments[text2path(Dm.str(@params["selected_expath"]))];
            object data = null;
            if (ispath(ex_path, typeof(datum_experiment_ordnance_explosive)))
                paper_to_be = paper_to_be.clone_into(typeof(datum_scientific_paper_explosive)) as datum_scientific_paper;
            if (ispath(ex_path, typeof(datum_experiment_ordnance_gaseous)))
                paper_to_be = paper_to_be.clone_into(typeof(datum_scientific_paper_gaseous)) as datum_scientific_paper;
            data = selected_file.return_data();
            paper_to_be.set_experiment(ex_path, variable, data);
            return TRUE;
        }
        else if (Dm.eq(action, "select_tier"))
        {
            paper_to_be.set_tier(@params["selected_tier"]);
            return TRUE;
        }
        else if (Dm.eq(action, "select_partner"))
        {
            paper_to_be.set_partner(text2path(Dm.str(@params["selected_partner"]))); // tg passes linked_techweb too, which set_partner does not take
            return TRUE;
        }
        else if (Dm.eq(action, "purchase_boost"))
        {
            datum_scientific_partner partner = Dm.locate_type(text2path(Dm.str(@params["boost_seller"])), SSresearch.scientific_partners) as datum_scientific_partner;
            datum_techweb_node node = SSresearch.techweb_nodes[text2path(Dm.str(@params["purchased_boost"]))] as datum_techweb_node;
            if (partner != null && node != null)
            {
                object possible_boost = partner.purchase_boost(linked_techweb, node);
                if (Dm.truthy(possible_boost))
                {
                    await computer.say(I18n.T("Purchase successful[].", Dm.eq(possible_boost, SCIPAPER_ALREADY_BOUGHT) ? I18n.T(", refunding [] points", partner.boostable_nodes[@params["purchased_boost"]]) : ""));
                    playsound(computer, Dm.resource("sound/machines/ping.ogg"), 25);
                    return TRUE;
                }
            }
            playsound(computer, Dm.resource("sound/machines/terminal/terminal_error.ogg"), 25);
            return TRUE;
        }
        return dot;
    }

    /// Publication and adding points.
    // tg: code/modules/modular_computers/file_system/programs/frontier.dm:231
    public virtual async DmTask<object> publish()
    {
        if (Dm.truthy(await linked_techweb.add_scientific_paper(paper_to_be)))
        {
            await computer.say(I18n.T("\"[]\" has been published!", paper_to_be.title));
            paper_to_be = Dm.@new<datum_scientific_paper>();
            UnregisterSignal(selected_file, COMSIG_COMPUTER_FILE_DELETE);
            selected_file = null;
            SStgui.update_uis(this);
            playsound(computer, Dm.resource("sound/machines/ping.ogg"), 25);
            return TRUE;
        }
        playsound(computer, Dm.resource("sound/machines/terminal/terminal_error.ogg"), 25);
        return FALSE;
    }
}
