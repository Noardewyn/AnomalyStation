// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/rdconsole.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package RD2, D333: the R&D consoles and servers; CallMaterialName since silicons SI3): the R&D console and
// its Techweb window: the techweb it reads (a server's at round start, or a multitool's buffered one), researching, queueing and
// dequeueing nodes with the research log and the radio announcement its board allows, the technology and design disks it uploads
// and downloads, the lock that research access toggles, the emag that breaks the lock and announces every node on the common
// channel. The var block is generated (D111).

/*
Research and Development (R&D) Console

This is the main work horse of the R&D system. It contains the menus/controls for the Destructive Analyzer, Protolathe, and Circuit
imprinter.

Basic use: When it first is created, it will attempt to link up to related devices within 3 squares. It'll only link up if they
aren't already linked to another console. Any consoles it cannot link up with (either because all of a certain type are already
linked or there aren't any in range), you'll just not have access to that menu. In the settings menu, there are menu options that
allow a player to attempt to re-sync with nearby consoles. You can also force it to disconnect from a specific console.

The only thing that requires ordnance access is locking and unlocking the console on the settings menu.
Nothing else in the console has ID requirements.

*/
// tg: code/modules/research/rdconsole.dm:19
public partial class obj_machinery_computer_rdconsole : obj_machinery_computer
{
    // RND_TECH_DISK and RND_DESIGN_DISK (rdconsole.dm:1-2), the file's own defines
    internal const string RND_TECH_DISK = "tech";
    internal const string RND_DESIGN_DISK = "design";

    /// Cooldown that prevents hanging the MC when tech disks are copied
    // tg: code/modules/research/rdconsole.dm:40  STATIC_COOLDOWN_DECLARE(cooldowncopy)
    private static float cooldowncopy
    {
        get => TgState.Current.obj_machinery_computer_rdconsole__cooldowncopy;
        set => TgState.Current.obj_machinery_computer_rdconsole__cooldowncopy = value;
    }

    // tg: code/modules/research/rdconsole.dm:55
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_no_default_techweb_link))) && stored_research == null) /* CONFIG_GET(flag/no_default_techweb_link) */
            CONNECT_TO_RND_SERVER_ROUNDSTART(ref stored_research, this);
        if (stored_research != null)
            Dm.append(ref stored_research.consoles_accessing, this);
    }

    // tg: code/modules/research/rdconsole.dm:62
    public override object Destroy(float? force = null)
    {
        if (stored_research != null)
        {
            Dm.remove(ref stored_research.consoles_accessing, this);
            stored_research = null;
        }
        if (t_disk != null)
        {
            Waiting.NoSleep(t_disk.forceMove(get_turf(this)));
            t_disk = null;
        }
        if (d_disk != null)
        {
            Waiting.NoSleep(d_disk.forceMove(get_turf(this)));
            d_disk = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/research/rdconsole.dm:74
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_disk)
            return NONE;

        if (tool is obj_item_disk_tech_disk tech_disk)
        {
            if (t_disk != null)
            {
                to_chat(user, span_warning(I18n.T("A technology disk is already loaded!")));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            {
                to_chat(user, span_warning(I18n.T("[] is stuck to your hand!", tool)));
                return ITEM_INTERACT_BLOCKING;
            }

            t_disk = tech_disk;
            to_chat(user, span_notice(I18n.T("You insert [] into \\the []!", tool, this)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is not obj_item_disk_design_disk design_disk)
        {
            to_chat(user, span_warning(I18n.T("Machine cannot accept disks in that format.")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (d_disk != null)
        {
            to_chat(user, span_warning(I18n.T("A design disk is already loaded!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
        {
            to_chat(user, span_warning(I18n.T("[] is stuck to your hand!", tool)));
            return ITEM_INTERACT_BLOCKING;
        }

        d_disk = design_disk;
        to_chat(user, span_notice(I18n.T("You insert [] into \\the []!", tool, this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/research/rdconsole.dm:107
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        await base.multitool_act(user, tool);
        var multitool = tool as obj_item_multitool;
        if (multitool != null && !QDELETED(multitool.buffer as datum) && multitool.buffer is datum_techweb techweb)
            stored_research = techweb;
        return TRUE;
    }

    // tg: code/modules/research/rdconsole.dm:113
    public virtual async DmTask<object> enqueue_node(object id = null, mob user = null)
    {
        if (stored_research == null || !Dm.truthy(stored_research.available_nodes[id]) || Dm.truthy(stored_research.researched_nodes[id]))
        {
            await say(I18n.T("Node enqueue failed: Either no techweb is found, node is already researched or is not available!"));
            return FALSE;
        }
        stored_research.enqueue_node(id, user);
        return TRUE;
    }

    // tg: code/modules/research/rdconsole.dm:120
    public virtual async DmTask<object> dequeue_node(object id = null, mob user = null)
    {
        if (stored_research == null || !Dm.truthy(stored_research.available_nodes[id]) || Dm.truthy(stored_research.researched_nodes[id]))
        {
            await say(I18n.T("Node dequeue failed: Either no techweb is found, node is already researched or is not available!"));
            return FALSE;
        }
        stored_research.dequeue_node(id, user);
        return TRUE;
    }

    // tg: code/modules/research/rdconsole.dm:127
    public virtual async DmTask<object> research_node(object node_path = null, mob user = null)
    {
        if (stored_research == null)
        {
            await say(I18n.T("Node unlock failed: No techweb found!"));
            return FALSE;
        }
        if (!Dm.truthy(stored_research.available_nodes[node_path]))
        {
            await say(I18n.T("Node unlock failed: Node is not available!"));
            return FALSE;
        }
        if (Dm.truthy(stored_research.researched_nodes[node_path]))
        {
            await say(I18n.T("Node unlock failed: Node is already researched!"));
            return FALSE;
        }

        datum_techweb_node unlocked_node = SSresearch.techweb_nodes[node_path] as datum_techweb_node;
        if (unlocked_node == null)
        {
            await say(I18n.T("Node unlock failed: Unknown error."));
            return FALSE;
        }

        DmList price = unlocked_node.get_price(stored_research);
        if (!stored_research.can_afford(price))
        {
            await say(I18n.T("Not enough research points..."));
            return FALSE;
        }

        if (!await stored_research.research_node(unlocked_node, research_source: this))
        {
            await say(I18n.T("Failed to research node: Internal database error!"));
            return FALSE;
        }

        user.investigate_log(Dm.text("researched []([]) on techweb id [].", unlocked_node.display_name, json_encode(price), stored_research.id), INVESTIGATE_RESEARCH);
        if (stored_research is datum_techweb_science)
            SSblackbox.record_feedback("associative", "science_techweb_unlock", 1, Dm.list(Dm.kv("path", Dm.text("[]", node_path)), Dm.kv("name", unlocked_node.display_name), Dm.kv("price", Dm.text("[]", json_encode(price))), Dm.kv("time", ISOtime())));

        await say(I18n.T("Successfully researched [].", unlocked_node.display_name));

        object logname = "Unknown";
        if (HAS_AI_ACCESS(user))
            logname = Dm.text("AI []", user.name);
        if (iscyborg(user))
            logname = Dm.text("CYBORG []", user.name);

        if (ishuman(user))
        {
            mob_living_carbon_human human_user = user as mob_living_carbon_human;
            obj_item_card_id id = human_user.wear_id?.GetID() as obj_item_card_id;
            if (id != null)
                logname = Dm.text("[]", id.registered_name);
        }
        else if (iscarbon(user))
        {
            obj_item_card_id idcard = user.get_active_held_item() as obj_item_card_id;
            if (idcard != null)
                logname = Dm.text("[]", idcard.registered_name);
        }

        Dm.append(ref stored_research.research_logs, Dm.list(Dm.list(
            Dm.kv("node_name", unlocked_node.display_name),
            Dm.kv("node_cost", price[TECHWEB_POINT_TYPE_GENERIC]),
            Dm.kv("node_researcher", logname),
            Dm.kv("node_research_location", Dm.text("[] ([],[],[])", get_area(this), x, y, z)))));
        return TRUE;
    }

    // tg: code/modules/research/rdconsole.dm:182
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        object dot = await base.emag_act(user, emag_card, __extra);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return dot;
        balloon_alert(user, "security protocols disabled");
        playsound(this, SFX_SPARKS, 75, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        Dm.combine(ref obj_flags, EMAGGED);
        // the board instance is circuit_value (GAP(types), _machinery.cs)
        obj_item_circuitboard_computer_rdconsole board = circuit_value as obj_item_circuitboard_computer_rdconsole;
        if (!Dm.truthy(Dm.bitand(board.obj_flags, EMAGGED)))
            board.silence_announcements = TRUE;
        board.locked = FALSE;
        return TRUE;
    }

    // tg: code/modules/research/rdconsole.dm:195
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "Techweb", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/modules/research/rdconsole.dm:202
    public override async DmTask<object> ui_assets(mob user = null)
    {
        await DmTask.CompletedTask;
        // list(get_asset_datum(/datum/asset/spritesheet_batched/sheetmaterials), get_asset_datum(/datum/asset/spritesheet_batched/research_designs)):
        // the asset transport is the client's (DV44); the Techweb window draws the materials and the designs' pictures from its own copy
        // of tg's .dmi files.
        return Dm.list();
    }

    // heavy data from this proc should be moved to static data when possible
    // tg: code/modules/research/rdconsole.dm:209
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        // the board instance is circuit_value (GAP(types), _machinery.cs)
        obj_item_circuitboard_computer_rdconsole board = circuit_value as obj_item_circuitboard_computer_rdconsole;

        data["stored_research"] = stored_research != null ? TRUE : FALSE;
        data["locked"] = board.locked;
        if (stored_research == null) //lack of a research node is all we care about.
            return data;
        return await techweb_ui_data(data, stored_research, user, sec_protocols: !Dm.truthy(Dm.bitand(obj_flags, EMAGGED)), t_disk, d_disk);
    }

    /// The body of ui_data after its techweb check, shared with the NTOS Nanotrasen Science Hub (file_system/programs/techweb.dm:47),
    /// whose copy differs only in its lock and its absent disks.
    // tg: code/modules/research/rdconsole.dm:209  (rdconsole.dm:220-271; programs/techweb.dm:52-97)
    internal static async DmTask<object> techweb_ui_data(DmList data, datum_techweb stored_research, mob user, bool sec_protocols, obj_item_disk_tech_disk t_disk, obj_item_disk_design_disk d_disk)
    {
        DmList queued_nodes = Dm.list();
        foreach (object node in Dm.each(stored_research.research_queue_nodes))
            queued_nodes[node] = (stored_research.research_queue_nodes[node] as datum_weakref)?.resolve();
        DmList nodes = Dm.list();
        DmList experiments = Dm.list();
        Dm.append(ref data, Dm.list(
            Dm.kv("nodes", nodes),
            Dm.kv("queue_nodes", queued_nodes),
            Dm.kv("experiments", experiments),
            Dm.kv("researched_designs", stored_research.researched_designs),
            Dm.kv("points", stored_research.research_points),
            Dm.kv("points_last_tick", stored_research.last_bitcoins),
            Dm.kv("web_org", stored_research.organization),
            Dm.kv("sec_protocols", sec_protocols ? TRUE : FALSE),
            Dm.kv("t_disk", null),
            Dm.kv("d_disk", null)));

        if (t_disk != null)
            data["t_disk"] = Dm.list(Dm.kv("stored_research", t_disk.stored_nodes));
        if (d_disk != null)
            data["d_disk"] = Dm.list(Dm.kv("blueprints", d_disk.blueprints));

        // Serialize all nodes to display
        foreach (object node_path in Dm.each(stored_research.tiers))
        {
            object node_tier = stored_research.tiers[node_path];
            datum_techweb_node node = SSresearch.techweb_nodes[node_path] as datum_techweb_node;

            // Ensure node is supposed to be visible
            if (Dm.truthy(stored_research.hidden_nodes[node_path]))
                continue;

            datum_weakref queuer_weakref = stored_research.research_queue_nodes[node_path] as datum_weakref;
            mob node_queuer = queuer_weakref?.resolve() as mob;
            bool enqueued_by_user = node_queuer != null && node_queuer == user;

            Dm.append(ref nodes, Dm.list(Dm.list(
                Dm.kv("path", node_path),
                Dm.kv("is_free", node.is_free(stored_research) ? TRUE : FALSE),
                Dm.kv("can_unlock", stored_research.can_unlock_node(node) ? TRUE : FALSE),
                Dm.kv("have_experiments_done", stored_research.have_experiments_for_node(node) ? TRUE : FALSE),
                Dm.kv("tier", node_tier),
                Dm.kv("enqueued_by_user", enqueued_by_user ? TRUE : FALSE),
                Dm.kv("discount_boosted", Dm.truthy(stored_research.boosted_nodes[node_path]) ? TRUE : FALSE))));
        }

        // Get experiments and serialize them
        DmList exp_to_process = stored_research.available_experiments.Copy();
        foreach (object experiment_type in Dm.each(stored_research.completed_experiments))
            Dm.append(ref exp_to_process, stored_research.completed_experiments[experiment_type]);
        foreach (datum_experiment experiment in Dm.each_cast<datum_experiment>(exp_to_process))
            experiments[experiment.type] = await experiment.to_ui_data();
        return data;
    }

    /**
     * Compresses an ID to an integer representation using the id_cache, used for deduplication
     * in sent JSON payloads
     *
     * Arguments:
     * * id - the ID to compress
     */
    // tg: code/modules/research/rdconsole.dm:280
    public virtual object compress_id(object id = null)
    {
        if (!Dm.truthy(id_cache[id]))
        {
            id_cache[id] = id_cache_seq;
            id_cache_seq += 1;
        }
        return id_cache[id];
    }

    // tg: code/modules/research/rdconsole.dm:286
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        await DmTask.CompletedTask;
        return techweb_ui_static_data(compress_id, id_cache);
    }

    /// The body of ui_static_data, shared with the NTOS Nanotrasen Science Hub (file_system/programs/techweb.dm:127), whose copy is the
    /// same over its own id cache.
    // tg: code/modules/research/rdconsole.dm:286  (programs/techweb.dm:127)
    internal static DmList techweb_ui_static_data(Func<object, object> compress_id, DmList id_cache)
    {
        // Build node cache...
        // Note this looks a bit ugly but its to reduce the size of the JSON payload
        // by the greatest amount that we can, as larger JSON payloads result in
        // hanging when the user opens the UI
        DmList node_cache = Dm.list();
        foreach (object node_path in Dm.each(SSresearch.techweb_nodes))
        {
            datum_techweb_node node = SSresearch.techweb_nodes[node_path] as datum_techweb_node;

            DmList node_data = Dm.list(
                Dm.kv("name", node.display_name),
                Dm.kv("description", node.description));

            if (Dm.truthy(LAZYLEN(node.research_costs)))
            {
                DmList costs = Dm.list();
                node_data["costs"] = costs;
                foreach (object point_type in Dm.each(node.research_costs))
                    costs[Dm.text("[]", compress_id(point_type))] = node.research_costs[point_type];
            }
            if (Dm.truthy(LAZYLEN(node.prerequisite_nodes)))
            {
                DmList prerequisites = Dm.list();
                foreach (object prerequisite_node in Dm.each(node.prerequisite_nodes))
                    prerequisites.Add(compress_id(prerequisite_node));
                node_data["prerequisite_nodes"] = prerequisites;
            }
            if (Dm.truthy(LAZYLEN(node.unlocked_designs)))
            {
                DmList designs = Dm.list();
                foreach (object unlocked_design in Dm.each(node.unlocked_designs))
                    designs.Add(compress_id(unlocked_design));
                node_data["unlocked_designs"] = designs;
            }
            if (Dm.truthy(LAZYLEN(node.unlocked_nodes)))
            {
                DmList unlocked = Dm.list();
                foreach (object unlocked_node in Dm.each(node.unlocked_nodes))
                    unlocked.Add(compress_id(unlocked_node));
                node_data["unlocked_nodes"] = unlocked;
            }
            if (Dm.truthy(LAZYLEN(node.required_experiments)))
                node_data["required_experiments"] = node.required_experiments;
            if (Dm.truthy(LAZYLEN(node.discount_experiments)))
                node_data["discount_experiments"] = node.discount_experiments;
            if (Dm.truthy(LAZYLEN(node.discount_boosts)))
                node_data["discount_boosts"] = node.discount_boosts;

            object compressed_id = Dm.text("[]", compress_id(node_path));
            node_cache[compressed_id] = node_data;
        }

        // Build design cache
        DmList design_cache = Dm.list();
        // the research_designs spritesheet's 32x32 size (DV44: the client draws every design from its own "design32x32" sheet, as the
        // exosuit fabricator's), so every icon is the design's asset id with no size prefix
        foreach (object design_path in Dm.each(SSresearch.techweb_designs))
        {
            datum_design design = SSresearch.techweb_designs[design_path] as datum_design;

            DmList cost = Dm.list();
            foreach (datum_material mat in Dm.each<datum_material>(design.materials))
                cost[mat.name] = OPTIMAL_COST(design.materials[mat]);

            object compressed_id = Dm.text("[]", compress_id(design_path));
            design_cache[compressed_id] = Dm.list(
                design.name,
                cost,
                design.build_type,
                design.departmental_flags,
                Dm.text("[]", design.asset_id));
        }

        DmList department_flags = Dm.list();
        foreach (Type department in Dm.each_cast<Type>(subtypesof(typeof(datum_job_department))))
        {
            object bitflags = Dm.initial(department, (datum_job_department d) => d.department_bitflags);
            if (Dm.truthy(bitflags))
                department_flags[Dm.text("[]", bitflags)] = Dm.initial(department, (datum_job_department d) => d.department_name);
        }

        // Don't pass away flags as those are irrelevant to the station
        DmList build_types = GLOB.build_types_to_string.Copy();
        Dm.remove(ref build_types, Dm.text("[]", AWAY_IMPRINTER));
        Dm.remove(ref build_types, Dm.text("[]", AWAY_LATHE));

        return Dm.list(
            Dm.kv("point_types_abbreviations", SSresearch.point_types),
            Dm.kv("static_data", Dm.list(
                Dm.kv("node_cache", node_cache),
                Dm.kv("design_cache", design_cache),
                Dm.kv("id_cache", assoc_to_keys(id_cache)),
                Dm.kv("SHEET_MATERIAL_AMOUNT", SHEET_MATERIAL_AMOUNT),
                Dm.kv("build_types", build_types),
                Dm.kv("department_flags", department_flags))));
    }

    // tg: code/modules/research/rdconsole.dm:369
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        add_fingerprint(usr);

        // the board instance is circuit_value (GAP(types), _machinery.cs)
        obj_item_circuitboard_computer_rdconsole board = circuit_value as obj_item_circuitboard_computer_rdconsole;

        // Check if the console is locked to block any actions occuring
        if (Dm.truthy(board.locked) && !Dm.eq(action, "toggleLock"))
        {
            await say(I18n.T("Console is locked, cannot perform further actions."));
            return TRUE;
        }

        if (Dm.eq(action, "toggleLock"))
        {
            if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            {
                to_chat(usr, span_boldwarning(I18n.T("Security protocol error: Unable to access locking protocols.")));
                return TRUE;
            }
            if (Dm.truthy(await allowed__proc(usr)))
                board.locked = Dm.truthy(board.locked) ? FALSE : TRUE;
            else
                to_chat(usr, span_boldwarning(I18n.T("Unauthorized Access.")));
            return TRUE;
        }
        else if (Dm.eq(action, "researchNode"))
        {
            await research_node(text2path(Dm.str(@params["node_path"])), usr);
            return TRUE;
        }
        else if (Dm.eq(action, "enqueueNode"))
        {
            await enqueue_node(text2path(Dm.str(@params["node_path"])), usr);
            return TRUE;
        }
        else if (Dm.eq(action, "dequeueNode"))
        {
            await dequeue_node(text2path(Dm.str(@params["node_path"])), usr);
            return TRUE;
        }
        else if (Dm.eq(action, "ejectDisk"))
        {
            await eject_disk(@params["type"]);
            return TRUE;
        }
        else if (Dm.eq(action, "uploadDisk"))
        {
            if (Dm.eq(@params["type"], RND_DESIGN_DISK))
            {
                if (QDELETED(d_disk))
                {
                    await say(I18n.T("No design disk inserted!"));
                    return TRUE;
                }
                foreach (object design_path in Dm.each(d_disk.blueprints))
                    stored_research.add_design(design_path, TRUE);
                await say(I18n.T("Uploading blueprints from disk."));
                await d_disk.on_upload(stored_research, this);
                return TRUE;
            }
            if (Dm.eq(@params["type"], RND_TECH_DISK))
            {
                // COOLDOWN_FINISHED(src, cooldowncopy) (__DEFINES/cooldowns.dm:113), inline
                if (!(cooldowncopy <= world.time)) // prevents MC hang
                {
                    await say(I18n.T("Servers busy!"));
                    return null;
                }
                if (QDELETED(t_disk))
                {
                    await say(I18n.T("No tech disk inserted!"));
                    return TRUE;
                }
                cooldowncopy = world.time + 5 * SECONDS; // COOLDOWN_START(src, cooldowncopy, 5 SECONDS), inline
                await say(I18n.T("Uploading technology disk."));
                await stored_research.absorb_techdisk(t_disk);
            }
            return TRUE;
        }
        //Tech disk-only action.
        else if (Dm.eq(action, "loadTech"))
        {
            // COOLDOWN_FINISHED(src, cooldowncopy) (__DEFINES/cooldowns.dm:113), inline
            if (!(cooldowncopy <= world.time)) // prevents MC hang
            {
                await say(I18n.T("Servers busy!"));
                return null;
            }
            if (QDELETED(t_disk))
            {
                await say(I18n.T("No tech disk inserted!"));
                return null;
            }
            cooldowncopy = world.time + 5 * SECONDS; // COOLDOWN_START(src, cooldowncopy, 5 SECONDS), inline
            await say(I18n.T("Downloading to technology disk."));
            Dm.combine(ref t_disk.stored_nodes, stored_research.researched_nodes);
            return TRUE;
        }
        return null;
    }

    // tg: code/modules/research/rdconsole.dm:445
    public virtual async DmTask eject_disk(object type = null)
    {
        if (Dm.eq(type, RND_DESIGN_DISK) && d_disk != null)
        {
            await d_disk.forceMove(get_turf(this));
            d_disk = null;
        }
        if (Dm.eq(type, RND_TECH_DISK) && t_disk != null)
        {
            await t_disk.forceMove(get_turf(this));
            t_disk = null;
        }
    }
}

public sealed partial class TgState
{
    // tg: code/modules/research/rdconsole.dm:40  STATIC_COOLDOWN_DECLARE(cooldowncopy)
    internal float obj_machinery_computer_rdconsole__cooldowncopy;
}

public static partial class GlobalProcs
{
    // tg: code/modules/research/rdconsole.dm:46
    public static object CallMaterialName(object ID = null)
    {
        if (ID is datum_material material)
            return material.name;
        else if (Dm.truthy(GLOB.chemical_reagents_list[ID]))
        {
            datum_reagent reagent = GLOB.chemical_reagents_list[ID] as datum_reagent;
            return reagent.name;
        }
        return ID;
    }
}
