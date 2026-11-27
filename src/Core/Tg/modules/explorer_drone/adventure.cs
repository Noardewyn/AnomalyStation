// Translated from tgstation (snapshot 1d99bd7f): code/modules/explorer_drone/adventure.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: tg's text adventures, read as data from tg's strings/exoadventures/ JSON files at SSpersistence's Initialize (the
// host mounts them, TgStrings), their nodes, choices, requirements, effects on the qualities, triggers and delays, and the result
// an adventure ends with. The var blocks are generated (D111) but for the node's id, which holds the node's name.

public static partial class GlobalProcs
{
    // json field definitions bit verbose but i've had it with the typos
    // tg: code/modules/explorer_drone/adventure.dm:2-43  the #define ADVENTURE_NAME_FIELD .. REQ_OPERATOR_FIELD (file-local, #undef at the file's end),
    // written as their texts at each use
    // tg: code/modules/explorer_drone/adventure.dm:45  #define CURRENT_ADVENTURE_VERSION 1 (file-local, #undef at :441)
    internal const int CURRENT_ADVENTURE_VERSION = 1;
    // tg: code/modules/explorer_drone/adventure.dm:46  #define ADVENTURE_LOOK_PATH "strings/exoadventures/" (file-local, #undef at :439)
    private const string ADVENTURE_LOOK_PATH = "strings/exoadventures/";

    /// Loads all adventures from DB
    // tg: code/modules/explorer_drone/adventure.dm:52
    public static object load_adventures()
    {
        DmList dot = Dm.list();
        foreach (object filename in Dm.each(flist(ADVENTURE_LOOK_PATH)))
        {
            string raw_json = file2text(ADVENTURE_LOOK_PATH + Dm.str(filename));
            DmList json_decoded = json_decode(raw_json) as DmList;
            datum_adventure_db_entry entry = Dm.@new<datum_adventure_db_entry>();
            entry.filename = filename;
            entry.raw_json = raw_json;
            entry.uploader = json_decoded["author"];
            entry.extract_metadata();
            Dm.append(ref dot, entry);
        }
        GLOB.explorer_drone_adventure_db_entries = dot;
        return dot;
    }
}

public partial class datum_controller_global_vars
{
    /// All possible adventures in raw form
    // tg: code/modules/explorer_drone/adventure.dm:49  GLOBAL_LIST_EMPTY(explorer_drone_adventure_db_entries)
    public DmList explorer_drone_adventure_db_entries;

    // tg: code/modules/explorer_drone/adventure.dm:49  (GLOBAL_MANAGED(explorer_drone_adventure_db_entries, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalexplorer_drone_adventure_db_entries()
    {
        explorer_drone_adventure_db_entries = Dm.list();
        Dm.append(ref gvars_datum_init_order, "explorer_drone_adventure_db_entries");
    }
}

// tg: code/modules/explorer_drone/adventure.dm:65
public partial class datum_adventure_db_entry : datum
{
    /// Check if the adventure usable for given exploration site traits
    // tg: code/modules/explorer_drone/adventure.dm:87
    public virtual object valid_for_use(DmList site_traits = null)
    {
        if (!Dm.truthy(raw_json) || !Dm.eq(version, CURRENT_ADVENTURE_VERSION) || Dm.truthy(placed))
            return FALSE;
        if (Dm.truthy(required_site_traits) && length(Dm.sub(required_site_traits, site_traits)) != 0)
            return FALSE;
        return TRUE;
    }

    /// Extracts fields that are used by adventure browser / generation before instantiating
    // tg: code/modules/explorer_drone/adventure.dm:95
    public virtual void extract_metadata()
    {
        if (!Dm.truthy(raw_json))
            throw Dm.CRASH("Trying to extract metadata from empty adventure", "code/modules/explorer_drone/adventure.dm", 97);
        object json_data = json_decode(raw_json);
        if (!islist(json_data))
            throw Dm.CRASH(Dm.text("Invalid JSON for adventure with at path:[]", filename), "code/modules/explorer_drone/adventure.dm", 100);
        DmList json_list = (DmList)json_data;
        version = Dm.truthy(json_list["version"]) ? json_list["version"] : 0;
        name = json_list["adventure_name"];
        required_site_traits = json_list["required_site_traits"] as DmList;
    }

    /// Creates new adventure instance
    // tg: code/modules/explorer_drone/adventure.dm:106
    public virtual object create_adventure()
    {
        if (!Dm.eq(version, GlobalProcs.CURRENT_ADVENTURE_VERSION))
            throw Dm.CRASH("Trying to instance outdated adventure version", "code/modules/explorer_drone/adventure.dm", 108);
        return try_loading_adventure();
    }

    /// Parses adventure JSON and returns /datum/adventure instance on success
    // tg: code/modules/explorer_drone/adventure.dm:112
    public virtual object try_loading_adventure()
    {
        object json_decoded = json_decode(raw_json);
        if (!islist(json_decoded))
            throw Dm.CRASH(Dm.text("Invalid JSON in adventure with path:[], name:[]", filename, name), "code/modules/explorer_drone/adventure.dm", 115);
        DmList json_data = (DmList)json_decoded;

        //Basic validation of required fields, don't even bother loading if they are missing.
        DmList required_fields = try_loading_adventure__required_fields ??= Dm.list("adventure_name", "starting_node", "nodes");
        foreach (object field in Dm.each(required_fields))
        {
            if (!Dm.truthy(json_data[field]))
                throw Dm.CRASH(Dm.text("Adventure path:[], name:[] missing [] value", filename, name, field), "code/modules/explorer_drone/adventure.dm", 121);
        }

        datum_adventure loaded_adventure = Dm.@new<datum_adventure>();
        //load properties
        loaded_adventure.starting_node = json_data["starting_node"];
        loaded_adventure.name = json_data["adventure_name"];
        loaded_adventure.required_site_traits = json_data["required_site_traits"] as DmList;
        loaded_adventure.band_modifiers = json_data["scan_band_mods"] as DmList;
        loaded_adventure.loot_categories = json_data["loot_categories"] as DmList;
        loaded_adventure.starting_qualities = json_data["starting_qualities"] as DmList;
        loaded_adventure.deep_scan_description = json_data["deep_scan_description"];

        foreach (DmList node_data in Dm.each<DmList>(json_data["nodes"]))
        {
            datum_adventure_node node = try_loading_node(node_data) as datum_adventure_node;
            if (Dm.truthy(node))
            {
                if (Dm.truthy(loaded_adventure.nodes[node.id]))
                    throw Dm.CRASH(Dm.text("Duplicate [] node in path:[], name:[] adventure", node.id, filename, name), "code/modules/explorer_drone/adventure.dm", 137);
                loaded_adventure.nodes[node.id] = node;
            }
        }
        loaded_adventure.triggers = json_data["triggers"] as DmList;
        if (!Dm.truthy(loaded_adventure.validate()))
            throw Dm.CRASH(Dm.text("Validation failed for path:[], name:[] adventure", filename, name), "code/modules/explorer_drone/adventure.dm", 141);
        return loaded_adventure;
    }

    // tg: code/modules/explorer_drone/adventure.dm:144
    public virtual object try_loading_node(object node_data = null)
    {
        if (!islist(node_data))
            throw Dm.CRASH(Dm.text("Invalid adventure node data in path:[], name:[] adventure.", filename, name), "code/modules/explorer_drone/adventure.dm", 146);
        DmList node_list = (DmList)node_data;
        datum_adventure_node fresh_node = Dm.@new<datum_adventure_node>();
        fresh_node.id = node_list["name"];
        fresh_node.description = node_list["description"];
        fresh_node.image_name = node_list["image"];
        fresh_node.raw_image = node_list["raw_image"];
        fresh_node.choices = Dm.list();
        foreach (DmList choice_data in Dm.each<DmList>(node_list["choices"]))
            fresh_node.choices[choice_data["key"]] = choice_data;
        fresh_node.on_enter_effects = node_list["on_enter_effects"] as DmList;
        fresh_node.on_exit_effects = node_list["on_exit_effects"] as DmList;
        return fresh_node;
    }

    // tg: code/modules/explorer_drone/adventure.dm:118  var/static/list/required_fields (try_loading_adventure)
    private static ref DmList try_loading_adventure__required_fields => ref TgState.Current.datum_adventure_db_entry__try_loading_adventure__required_fields;
}

public sealed partial class TgState
{
    // tg: code/modules/explorer_drone/adventure.dm:118  var/static/list/required_fields (try_loading_adventure)
    internal DmList datum_adventure_db_entry__try_loading_adventure__required_fields;
}

/// text adventure instance, holds data about nodes/choices/etc and of current play state.
// tg: code/modules/explorer_drone/adventure.dm:160
public partial class datum_adventure : datum
{
    /// Basic sanity checks to ensure broken adventures are not used.
    // tg: code/modules/explorer_drone/adventure.dm:193
    public virtual object validate()
    {
        ///Check all nodes have choices
        foreach (object node_id in Dm.each(nodes))
        {
            datum_adventure_node node = nodes[node_id] as datum_adventure_node;
            if (!Dm.truthy(length(node.choices)))
                return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/explorer_drone/adventure.dm:201
    public virtual void start_adventure()
    {
        initialize_qualities();
        previous_node_id = starting_node;
        navigate_to_node(starting_node);
    }

    /// Finish adventure
    // tg: code/modules/explorer_drone/adventure.dm:207
    public virtual void end_adventure(object result = null)
    {
        SEND_SIGNAL(COMSIG_ADVENTURE_FINISHED, Dm.list(this, result));
    }

    // tg: code/modules/explorer_drone/adventure.dm:210
    public virtual void initialize_qualities()
    {
        qualities = Dm.truthy(starting_qualities) ? starting_qualities : Dm.list();
        SEND_SIGNAL(COMSIG_ADVENTURE_QUALITY_INIT, Dm.list(this, qualities));
    }

    // tg: code/modules/explorer_drone/adventure.dm:214
    public virtual void navigate_to_node(object node_id = null)
    {
        if (Dm.truthy(current_node))
        {
            if (Dm.truthy(current_node.on_exit(this))) //Trigger on exit caused node change <- I don't really see much use for this so might want to warn about it ?
                return;
            if (!Dm.eq(current_node.id, previous_node_id))
                previous_node_id = current_node.id;
        }
        if (Dm.truthy(handle_special_nodes(node_id)))
            return;
        if (!Dm.truthy(nodes[node_id]))
            stack_trace(Dm.text("Invalid adventure node navigation from node []", current_node.id), "code/modules/explorer_drone/adventure.dm", 223);
        current_node = nodes[node_id] as datum_adventure_node;
        current_node.on_enter(this);
    }

    /// Handles special node ID's
    // tg: code/modules/explorer_drone/adventure.dm:228
    public virtual object handle_special_nodes(object node_id = null)
    {
        switch (node_id)
        {
            case FAIL_NODE:
                end_adventure(ADVENTURE_RESULT_DAMAGE);
                return TRUE;
            case FAIL_DEATH_NODE:
                end_adventure(ADVENTURE_RESULT_DEATH);
                return TRUE;
            case WIN_NODE:
                end_adventure(ADVENTURE_RESULT_SUCCESS);
                return TRUE;
            case GO_BACK_NODE:
                if (Dm.truthy(previous_node_id))
                {
                    navigate_to_node(previous_node_id);
                    return TRUE;
                }
                else
                    return FALSE;
            default:
                return FALSE;
        }
    }

    // tg: code/modules/explorer_drone/adventure.dm:248
    public virtual void select_choice(object choice_id = null)
    {
        if (!Dm.truthy(current_node) || !islist(current_node.choices[choice_id]))
            return;
        DmList choice_data = current_node.choices[choice_id] as DmList;
        if (!Dm.truthy(check_requirements(choice_data["requirements"])))
            return;
        if (Dm.truthy(choice_data["on_selection_effects"]))
        {
            if (Dm.truthy(apply_adventure_effect(choice_data["on_selection_effects"] as DmList, TRUE)))
                return; //Trigger forced node change. (tg passes src as process_triggers: a true value)
        }
        object exit_id = choice_data["exit_node"];
        if (!Dm.truthy(exit_id))
            throw Dm.CRASH(Dm.text("No exit node for choice [] in adventure []", choice_id, name), "code/modules/explorer_drone/adventure.dm", 259);
        if (Dm.truthy(choice_data["delay"]))
        {
            object delay_message = choice_data["delay_message"];
            object delay_time = choice_data["delay"];
            if (!isnum(delay_time))
                throw Dm.CRASH(Dm.text("Invalid delay in adventure []", name), "code/modules/explorer_drone/adventure.dm", 264);
            SEND_SIGNAL(COMSIG_ADVENTURE_DELAY_START, Dm.list(this, delay_time, delay_message));
            delayed_action = Dm.list(delay_time, delay_message);
            _addtimer(Dm.@new<datum_callback>(this, nameof(finish_delay), exit_id), Dm.num(delay_time), file: "code/modules/explorer_drone/adventure.dm", line: 267);
            return;
        }
        navigate_to_node(exit_id);
    }

    // tg: code/modules/explorer_drone/adventure.dm:272
    public virtual void finish_delay(object exit_id = null)
    {
        delayed_action = null;
        navigate_to_node(exit_id);
        SEND_SIGNAL(COMSIG_ADVENTURE_DELAY_END, Dm.list(this));
    }

    // tg: code/modules/explorer_drone/adventure.dm:276
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = await base.ui_data(user) as DmList;
        dot["description"] = current_node?.description;
        dot["image"] = current_node?.image_name;
        dot["raw_image"] = current_node?.raw_image;
        dot["choices"] = current_node?.get_available_choices(this);
        return dot;
    }

    ///Applies changes encoded in effect data and processes triggers, returns TRUE if the change forced node change.
    // tg: code/modules/explorer_drone/adventure.dm:325
    public virtual object apply_adventure_effect(DmList effect_data = null, float? process_triggers = null)
    {
        process_triggers ??= TRUE;
        if (!islist(effect_data))
            throw Dm.CRASH(Dm.text("Invalid effect data [] in adventure []", json_encode(effect_data), name), "code/modules/explorer_drone/adventure.dm", 327);
        foreach (DmList effect_group in Dm.each<DmList>(effect_data))
        {
            object effect_keyword = effect_group["effect_type"];
            object quality_name = effect_group["quality"];
            object value = process_adventure_value(effect_group["value"]);
            switch (effect_keyword)
            {
                case ADVENTURE_EFFECT_TYPE_REMOVE: //remove quality doesn't care about value for now
                    Dm.remove(ref qualities, quality_name);
                    break;
                case ADVENTURE_EFFECT_TYPE_ADD:
                    if (!isnum(value))
                        throw Dm.CRASH(Dm.text("Invalid add quality effect value in effect [] in adventure []", json_encode(effect_data), name), "code/modules/explorer_drone/adventure.dm", 337);
                    if (!Dm.truthy(qualities[quality_name]))
                        qualities[quality_name] = 0;
                    Dm.append_at(qualities, quality_name, value);
                    break;
                case ADVENTURE_EFFECT_TYPE_SET:
                    qualities[quality_name] = value;
                    break;
                default:
                    throw Dm.CRASH(Dm.text("Invalid effect keyword in effect [] in adventure []", json_encode(effect_data), name), "code/modules/explorer_drone/adventure.dm", 344);
            }
        }
        ///Check Triggers
        if (Dm.truthy(process_triggers))
        {
            foreach (DmList trigger_data in Dm.each<DmList>(triggers))
            {
                if (!Dm.truthy(check_requirements(trigger_data["requirements"])))
                    continue;
                if (Dm.truthy(LAZYACCESS(trigger_loop_safety, trigger_data["name"])))
                {
                    stack_trace(Dm.text("Loop in trigger processing detected in adventure []", name), "code/modules/explorer_drone/adventure.dm", 351);
                    continue;
                }
                LAZYADD(ref trigger_loop_safety, trigger_data["name"]);
                if (Dm.truthy(trigger_data["on_trigger_effects"]))
                    apply_adventure_effect(trigger_data["on_trigger_effects"] as DmList, FALSE); //Let's keep this simple
                if (Dm.truthy(trigger_data["target_node"]))
                {
                    navigate_to_node(trigger_data["target_node"]);
                    return TRUE;
                }
            }
        }
        //We're out of trigger processing
        LAZYCLEARLIST(trigger_loop_safety);
        return FALSE;
    }

    /// Extracts raw value from special value objects
    // tg: code/modules/explorer_drone/adventure.dm:364
    public virtual object process_adventure_value(object raw_value = null)
    {
        if (islist(raw_value))
        {
            DmList value_as_list = (DmList)raw_value;
            switch (value_as_list["value_type"])
            {
                case ADVENTURE_QUALITY_TYPE_RANDOM:
                    return rand(Dm.num(value_as_list[ADVENTURE_RANDOM_QUALITY_LOW_FIELD]), Dm.num(value_as_list[ADVENTURE_RANDOM_QUALITY_HIGH_FIELD]));
                default:
                    throw Dm.CRASH(Dm.text("Invalid special value type in adventure []", name), "code/modules/explorer_drone/adventure.dm", 371);
            }
        }
        else
            return raw_value;
    }

    /// Checks if current qualities satisfy passed in requirements
    // tg: code/modules/explorer_drone/adventure.dm:376
    public virtual object check_requirements(object raw_requirements = null)
    {
        if (!islist(raw_requirements))
            return TRUE;
        DmList req_groups = (DmList)raw_requirements;
        // Top level list - can contain either req groups or single requirements and is AND type group
        foreach (DmList group_data in Dm.each<DmList>(req_groups))
        {
            if (Dm.truthy(group_data["requirements"])) //It's a group
            {
                if (!Dm.truthy(check_requirement_group(group_data)))
                    return FALSE;
            }
            else //It's a single requirement
            {
                if (!Dm.truthy(check_single_requirement(group_data)))
                    return FALSE;
            }
        }
        return TRUE;
    }

    /// Recursively validates group requirements.
    // tg: code/modules/explorer_drone/adventure.dm:391
    public virtual object check_requirement_group(object raw_group_data = null)
    {
        if (!islist(raw_group_data))
            throw Dm.CRASH(Dm.text("Invalid group requirement in adventure []", name), "code/modules/explorer_drone/adventure.dm", 393);
        DmList group_data = (DmList)raw_group_data;
        object group_type = group_data["group_type"];
        DmList group_elements = group_data["requirements"] as DmList;
        switch (group_type)
        {
            case "OR": //Just one out of subgroups/reqs need to be true for this to return true
                foreach (DmList subgroup_data in Dm.each<DmList>(group_elements))
                {
                    if (Dm.truthy(subgroup_data["requirements"])) //It's a group
                    {
                        if (Dm.truthy(check_requirement_group(subgroup_data)))
                            return TRUE;
                    }
                    else //It's a single requirement
                    {
                        if (Dm.truthy(check_single_requirement(subgroup_data)))
                            return TRUE;
                    }
                }
                return FALSE;
            case "AND": //All subgroups/reqs need to be true for this to return true
                foreach (DmList subgroup_data in Dm.each<DmList>(group_elements))
                {
                    if (Dm.truthy(subgroup_data["requirements"])) //It's a group
                    {
                        if (!Dm.truthy(check_requirement_group(subgroup_data)))
                            return FALSE;
                    }
                    else //It's a single requirement
                    {
                        if (!Dm.truthy(check_single_requirement(subgroup_data)))
                            return FALSE;
                    }
                }
                return TRUE;
            default:
                throw Dm.CRASH(Dm.text("Invalid requirement group in adventure []", name), "code/modules/explorer_drone/adventure.dm", 417);
        }
    }

    //Checks if unit requirement {"quality": "a","op": "==","value": "something"} is met.
    // tg: code/modules/explorer_drone/adventure.dm:420
    public virtual object check_single_requirement(object raw_requirement = null)
    {
        DmList requirement = (DmList)raw_requirement;
        object qkey = requirement["quality"];
        object qval = requirement["value"];
        switch (requirement["operator"])
        {
            case "==":
                return Dm.num(Dm.eq(qualities[qkey], qval));
            case "!=":
                return Dm.num(!Dm.eq(qualities[qkey], qval));
            case ">":
                return Dm.num(Dm.gt(qualities[qkey], qval));
            case ">=":
                return Dm.num(Dm.ge(qualities[qkey], qval));
            case "<=":
                return Dm.num(Dm.le(qualities[qkey], qval));
            case "<":
                return Dm.num(Dm.lt(qualities[qkey], qval));
            case "exists":
                return Dm.num(Dm.@in(qkey, qualities));
        }
        return null;
    }
}

// tg: code/modules/explorer_drone/adventure.dm:284
public partial class datum_adventure_node : datum
{
    /// Unique identifier for this node (the node's name in the adventure's JSON: text, which the generated typepath field cannot hold)
    // tg: code/modules/explorer_drone/adventure.dm:286
    public object id;

    // tg: code/modules/explorer_drone/adventure.dm:305
    public virtual object on_enter(datum_adventure context = null)
    {
        if (Dm.truthy(on_enter_effects))
        {
            if (Dm.truthy(context.apply_adventure_effect(on_enter_effects)))
                return TRUE;
        }
        return null;
    }

    // tg: code/modules/explorer_drone/adventure.dm:311
    public virtual object on_exit(datum_adventure context = null)
    {
        if (Dm.truthy(on_exit_effects))
        {
            if (Dm.truthy(context.apply_adventure_effect(on_exit_effects)))
                return TRUE;
        }
        return null;
    }

    // tg: code/modules/explorer_drone/adventure.dm:317
    public virtual object get_available_choices(datum_adventure context = null)
    {
        DmList dot = Dm.list();
        foreach (object choice_key in Dm.each(choices))
        {
            DmList choice_data = choices[choice_key] as DmList;
            if (Dm.truthy(context.check_requirements(choice_data["requirements"])))
                Dm.append(ref dot, Dm.list(Dm.list(Dm.kv("key", choice_key), Dm.kv("text", choice_data["name"]))));
        }
        return dot;
    }
}
