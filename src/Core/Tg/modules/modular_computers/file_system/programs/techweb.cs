// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/techweb.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package RD2, D333: the R&D consoles and servers): the Nanotrasen Science Hub app, the R&D console on a tablet
// or laptop (the NtosTechweb window): researching and queueing nodes on the techweb it links to (a server's at install, or a
// multitool's buffered one), with its own lock that the inserted ID's research access toggles; no disks. Its window data are the
// console's (rdconsole.cs techweb_ui_data and techweb_ui_static_data, the same code in tg). The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/techweb.dm:1
public partial class datum_computer_file_program_science : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/techweb.dm:24
    public override void on_install(datum_computer_file source = null, obj_item_modular_computer computer_installing = null, mob user = null)
    {
        base.on_install(source, computer_installing, user);
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_no_default_techweb_link))) && stored_research == null) /* CONFIG_GET(flag/no_default_techweb_link) */
            CONNECT_TO_RND_SERVER_ROUNDSTART(ref stored_research, computer);
    }

    // tg: code/modules/modular_computers/file_system/programs/techweb.dm:29
    public override async DmTask<object> application_item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        await DmTask.CompletedTask;
        if (tool is obj_item_multitool multitool)
            return multitool_act(user, multitool);
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/techweb.dm:33
    public virtual object multitool_act(mob_living user = null, obj_item_multitool used_multitool = null)
    {
        if (QDELETED(used_multitool.buffer as datum) || used_multitool.buffer is not datum_techweb techweb)
            return ITEM_INTERACT_BLOCKING;
        stored_research = techweb;
        computer.balloon_alert(user, "buffer linked!");
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/modular_computers/file_system/programs/techweb.dm:40
    public override async DmTask<object> ui_assets(mob user = null)
    {
        await DmTask.CompletedTask;
        // list(get_asset_datum(/datum/asset/spritesheet_batched/sheetmaterials), get_asset_datum(/datum/asset/spritesheet_batched/research_designs)):
        // the asset transport is the client's (DV44).
        return Dm.list();
    }

    // heavy data from this proc should be moved to static data when possible
    // tg: code/modules/modular_computers/file_system/programs/techweb.dm:47
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["stored_research"] = stored_research != null ? TRUE : FALSE;
        if (stored_research == null) //lack of a research node is all we care about.
            return data;
        //Not doing disk operations on the app, use the console for that.
        await obj_machinery_computer_rdconsole.techweb_ui_data(data, stored_research, user, sec_protocols: !Dm.truthy(Dm.bitand(computer.obj_flags, EMAGGED)), t_disk: null, d_disk: null);
        data["locked"] = locked;
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/techweb.dm:99
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        // Check if the console is locked to block any actions occuring
        if (Dm.truthy(locked) && !Dm.eq(action, "toggleLock"))
        {
            await computer.say(I18n.T("Console is locked, cannot perform further actions."));
            return TRUE;
        }

        if (Dm.eq(action, "toggleLock"))
        {
            if (Dm.truthy(Dm.bitand(computer.obj_flags, EMAGGED)))
            {
                to_chat(usr, span_boldwarning(I18n.T("Security protocol error: Unable to access locking protocols.")));
                return TRUE;
            }
            if (Dm.@in(lock_access, computer?.stored_id?.access))
                locked = Dm.truthy(locked) ? FALSE : TRUE;
            else
                to_chat(usr, span_boldwarning(I18n.T("Unauthorized Access. Please insert research ID card.")));
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
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/techweb.dm:127
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        await DmTask.CompletedTask;
        return obj_machinery_computer_rdconsole.techweb_ui_static_data(compress_id, id_cache);
    }

    /**
     * Compresses an ID to an integer representation using the id_cache, used for deduplication
     * in sent JSON payloads
     *
     * Arguments:
     * * id - the ID to compress
     */
    // tg: code/modules/modular_computers/file_system/programs/techweb.dm:217
    public virtual object compress_id(object id = null)
    {
        if (!Dm.truthy(id_cache[id]))
        {
            id_cache[id] = id_cache_seq;
            id_cache_seq += 1;
        }
        return id_cache[id];
    }

    // tg: code/modules/modular_computers/file_system/programs/techweb.dm:223
    public virtual async DmTask<object> enqueue_node(object node_path = null, mob user = null)
    {
        if (stored_research == null || !Dm.truthy(stored_research.available_nodes[node_path]) || Dm.truthy(stored_research.researched_nodes[node_path]))
        {
            await computer.say(I18n.T("Node enqueue failed: Either no techweb is found, node is already researched or is not available!"));
            return FALSE;
        }
        stored_research.enqueue_node(node_path, user);
        return TRUE;
    }

    // tg: code/modules/modular_computers/file_system/programs/techweb.dm:230
    public virtual async DmTask<object> dequeue_node(object node_path = null, mob user = null)
    {
        if (stored_research == null || !Dm.truthy(stored_research.available_nodes[node_path]) || Dm.truthy(stored_research.researched_nodes[node_path]))
        {
            await computer.say(I18n.T("Node dequeue failed: Either no techweb is found, node is already researched or is not available!"));
            return FALSE;
        }
        stored_research.dequeue_node(node_path, user);
        return TRUE;
    }

    // tg: code/modules/modular_computers/file_system/programs/techweb.dm:237
    public virtual async DmTask<object> research_node(object node_path = null, mob user = null)
    {
        if (stored_research == null)
        {
            await computer.say(I18n.T("Node unlock failed: No techweb found!"));
            return FALSE;
        }
        if (!Dm.truthy(stored_research.available_nodes[node_path]))
        {
            await computer.say(I18n.T("Node unlock failed: Node is not available!"));
            return FALSE;
        }
        if (Dm.truthy(stored_research.researched_nodes[node_path]))
        {
            await computer.say(I18n.T("Node unlock failed: Node is already researched!"));
            return FALSE;
        }

        datum_techweb_node unlocked_node = SSresearch.techweb_nodes[node_path] as datum_techweb_node;
        if (unlocked_node == null)
        {
            await computer.say(I18n.T("Node unlock failed: Unknown error."));
            return FALSE;
        }

        DmList price = unlocked_node.get_price(stored_research);
        if (!stored_research.can_afford(price))
        {
            await computer.say(I18n.T("Not enough research points..."));
            return FALSE;
        }

        if (!await stored_research.research_node(unlocked_node, research_source: computer))
        {
            await computer.say(I18n.T("Failed to research node: Internal database error!"));
            return FALSE;
        }

        user.investigate_log(Dm.text("researched []([]) on techweb id [].", unlocked_node.display_name, json_encode(price), stored_research.id), INVESTIGATE_RESEARCH);
        if (stored_research is datum_techweb_science)
            SSblackbox.record_feedback("associative", "science_techweb_unlock", 1, Dm.list(Dm.kv("path", Dm.text("[]", node_path)), Dm.kv("name", unlocked_node.display_name), Dm.kv("price", Dm.text("[]", json_encode(price))), Dm.kv("time", ISOtime())));

        await computer.say(I18n.T("Successfully researched [].", unlocked_node.display_name));

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
            Dm.kv("node_research_location", Dm.text("[] ([],[],[])", get_area(computer), user.x, user.y, user.z)))));
        return TRUE;
    }
}
