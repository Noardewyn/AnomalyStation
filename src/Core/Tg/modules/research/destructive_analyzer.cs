// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/destructive_analyzer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RD3 (D333, the experiments): the whole file: the destructive analyzer, which takes an item in hand, shows it in tg's
// DestructiveAnalyzer window with the research points its destruction recovers and the hidden nodes it reveals, and destroys it (and
// all it holds) for the points or to reveal one of those nodes. The item's picture goes to the client as its icon and state, not as
// tg's flattened PNG (DV496). The var block is generated (D111).

/**
 * ## Destructive Analyzer
 * It is used to destroy hand-held objects and advance technological research.
 */
// tg: code/modules/research/destructive_analyzer.dm:10
public partial class obj_machinery_rnd_destructive_analyzer : obj_machinery_rnd
{
    ///How much power it costs to deconstruct an item.
    // tg: code/modules/research/destructive_analyzer.dm:2  #define DESTRUCTIVE_ANALYZER_POWER_USAGE (BASE_MACHINE_IDLE_CONSUMPTION * 2.5) (file-local, #undef at :218)
    private const float DESTRUCTIVE_ANALYZER_POWER_USAGE = BASE_MACHINE_IDLE_CONSUMPTION * 2.5f;
    ///The 'ID' for deconstructing items for Research points instead of nodes.
    // tg: code/modules/research/destructive_analyzer.dm:4  #define DESTRUCTIVE_ANALYZER_DESTROY_POINTS "research_points" (file-local, #undef at :217; unused in tg)
    private const string DESTRUCTIVE_ANALYZER_DESTROY_POINTS = "research_points";

    // tg: code/modules/research/destructive_analyzer.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_SCREWDRIVER, TOOL_ACT_PRIMARY)); //This allows people to put syndicate screwdrivers in the machine. Secondary act still passes.
        return dot;
    }

    // tg: code/modules/research/destructive_analyzer.dm:21
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);

        float screentip_set = FALSE;
        if (loaded_item != null)
        {
            context[SCREENTIP_CONTEXT_ALT_LMB] = "Remove Item";
            screentip_set = TRUE;
        }
        else if (held_item != null)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Insert Item";
            screentip_set = TRUE;
        }

        if (Dm.truthy(screentip_set))
            dot = CONTEXTUAL_SCREENTIP_SET;
        return dot;
    }

    // tg: code/modules/research/destructive_analyzer.dm:35
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!Dm.truthy(in_range(user, this)) && !isobserver(user))
            return dot;

        if (loaded_item != null)
            Dm.append(ref dot, span_notice(I18n.T("[] to remove loaded item inside.", EXAMINE_HINT("Left-Click"))));
        else
            Dm.append(ref dot, span_notice(I18n.T("An item can be loaded inside via [].", EXAMINE_HINT("Left-Click"))));
        return dot;
    }

    // tg: code/modules/research/destructive_analyzer.dm:45
    public override async DmTask<object> base_item_interaction(mob_living user = null, obj_item weapon = null, DmList modifiers = null)
    {
        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
            return await base.base_item_interaction(user, weapon, modifiers);
        if (Dm.truthy(user.combat_mode))
            return await base.base_item_interaction(user, weapon, modifiers);
        if (!Dm.truthy(is_insertion_ready(user)))
            return await base.base_item_interaction(user, weapon, modifiers);
        if (!Dm.truthy(await user.transferItemToLoc(weapon, this)))
        {
            to_chat(user, span_warning(I18n.T("\\The [] is stuck to your hand, you cannot put it in \\the []!", weapon, this)));
            return ITEM_INTERACT_BLOCKING;
        }

        busy = TRUE;
        loaded_item = weapon;
        to_chat(user, span_notice(I18n.T("You place \\the [] inside \\the [].", weapon, this)));
        flick(Dm.text("[]_la", base_icon_state), this);
        _addtimer(Dm.@new<datum_callback>(this, nameof(finish_loading)), 1 * SECONDS, file: "code/modules/research/destructive_analyzer.dm", line: 60);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/research/destructive_analyzer.dm:63
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await unload_item();
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/research/destructive_analyzer.dm:67
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(panel_open) && loaded_item == null)
            return dot; // use parent call state

        icon_state = Dm.text("[][]", base_icon_state, loaded_item != null ? "_l" : "");
        return dot;
    }

    // tg: code/modules/research/destructive_analyzer.dm:74
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "DestructiveAnalyzer");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/research/destructive_analyzer.dm:80
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["server_connected"] = Dm.not(Dm.not(stored_research));
        data["node_data"] = Dm.list();
        if (loaded_item != null)
        {
            // DEVIATION(DV496): tg's icon2base64(getFlatIcon(image(icon = loaded_item.icon, icon_state = loaded_item.icon_state), no_anim = TRUE)),
            // a PNG the server makes; the port sends the icon and state and the client draws tg's sprite (the server reads no .dmi, D104).
            data["item_icon"] = Dm.list(Dm.kv("icon", Dm.text("[]", loaded_item.icon)), Dm.kv("icon_state", loaded_item.icon_state));
            data["indestructible"] = Dm.not(Dm.bitand(loaded_item.resistance_flags, INDESTRUCTIBLE));
            data["loaded_item"] = loaded_item;
            data["already_deconstructed"] = Dm.not(Dm.not(stored_research.deconstructed_items[loaded_item.type]));
            DmList points = SSresearch.techweb_point_items[loaded_item.type] as DmList;
            data["recoverable_points"] = techweb_point_display_generic(points);

            DmList boostable_nodes = SSresearch.techweb_unlock_items[loaded_item.type] as DmList;
            foreach (object node_path in Dm.each(boostable_nodes))
            {
                datum_techweb_node unlockable_node = SSresearch.techweb_nodes[node_path] as datum_techweb_node;
                Dm.append_at(data, "node_data", Dm.list(Dm.list(
                    Dm.kv("node_name", unlockable_node.display_name),
                    Dm.kv("node_path", node_path),
                    Dm.kv("node_hidden", Dm.not(Dm.not(stored_research.hidden_nodes[node_path]))))));
            }
        }
        else
            data["loaded_item"] = null;
        return data;
    }

    // tg: code/modules/research/destructive_analyzer.dm:104
    [DmParams("action", "params", "ui")]
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        mob user = usr;
        object sw__1 = action;
        if (Dm.eq(sw__1, "eject_item"))
        {
            if (Dm.truthy(busy))
            {
                balloon_alert(user, "already busy!");
                return TRUE;
            }
            if (loaded_item != null)
            {
                await unload_item();
                return TRUE;
            }
        }
        else if (Dm.eq(sw__1, "deconstruct"))
        {
            Type node_path = text2path(@params["deconstruct_path"] as string);
            if (!Dm.truthy(await try_deconstruct_loaded_item(node_path)))
                await say(I18n.T("Destructive analysis failed!"));
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/research/destructive_analyzer.dm:124
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        // Cringe way to let emags insert on RMB because we still use attackby to insert
        if (tool is obj_item_card_emag)
            return ITEM_INTERACT_SKIP_TO_ATTACK;
        return NONE;
    }

    //We need to call default_deconstruction_screwdriver here since its parent will call screwdriver_act on this level which will stop us from ever deconstructing.
    // tg: code/modules/research/destructive_analyzer.dm:131
    public override async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    //We need to let wire cutter in (not block) so we can analyze alien wirecutters.
    // tg: code/modules/research/destructive_analyzer.dm:135
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(panel_open))
        {
            await wires.interact(user);
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }

    ///Drops the loaded item where it can and nulls it.
    // tg: code/modules/research/destructive_analyzer.dm:141
    public virtual async DmTask<object> unload_item()
    {
        if (loaded_item == null)
            return FALSE;
        playsound(loc, Dm.resource("sound/machines/terminal/terminal_insert_disc.ogg"), 30, FALSE);
        await loaded_item.forceMove(drop_location() as atom);
        loaded_item = null;
        update_appearance(UPDATE_ICON);
        return TRUE;
    }

    ///Called in a timer callback after loading something into it, this handles resetting the 'busy' state back to its initial state
    ///So the machine can be used.
    // tg: code/modules/research/destructive_analyzer.dm:152
    public virtual void finish_loading()
    {
        update_appearance(UPDATE_ICON);
        reset_busy();
    }

    /**
     * Destroys an item by going through all its contents (including itself) and calling destroy_item_individual
     * Args:
     * gain_research_points - Whether deconstructing each individual item should check for research points to boost.
     */
    // tg: code/modules/research/destructive_analyzer.dm:161
    public virtual async DmTask<object> destroy_item(float? gain_research_points = null)
    {
        gain_research_points ??= FALSE;
        if (QDELETED(loaded_item) || QDELETED(this))
            return FALSE;
        flick(Dm.text("[]_process", base_icon_state), this);
        busy = TRUE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(reset_busy)), 2.4f * SECONDS, file: "code/modules/research/destructive_analyzer.dm", line: 166);
        await use_energy(DESTRUCTIVE_ANALYZER_POWER_USAGE);
        DmList all_contents = loaded_item.get_all_contents() as DmList;
        foreach (object innerthing in Dm.each(all_contents))
            await destroy_item_individual(innerthing, gain_research_points);

        loaded_item = null;
        update_appearance(UPDATE_ICON);
        return TRUE;
    }

    /**
     * Destroys the individual provided item
     * Args:
     * thing - The thing being destroyed. Generally an object, but it can be a mob too, such as intellicards and pAIs.
     * gain_research_points - Whether deconstructing this should give research points to the stored techweb, if applicable.
     */
    // tg: code/modules/research/destructive_analyzer.dm:182
    public virtual async DmTask destroy_item_individual(object thing = null, float? gain_research_points = null)
    {
        gain_research_points ??= FALSE;
        if (isliving(thing))
        {
            mob_living mob_thing = thing as mob_living;
            if (mob_thing.stat != DEAD)
                mob_thing.investigate_log("has been killed by a destructive analyzer.", INVESTIGATE_DEATHS);
            await mob_thing.death();
        }
        DmList points = SSresearch.techweb_point_items[((datum)thing).type] as DmList;
        if (Dm.truthy(length(points)) && !Dm.truthy(stored_research.deconstructed_items[((datum)thing).type]))
        {
            stored_research.deconstructed_items[((datum)thing).type] = TRUE;
            stored_research.adjust_multiple_points(points);
        }
        qdel(thing);
    }

    /**
     * Attempts to destroy the loaded item using a provided techweb node path.
     * If no node path is provided, we will deconstruct for points instead
     * Args:
     * id - The techweb ID node that we're meant to unlock if applicable.
     */
    // tg: code/modules/research/destructive_analyzer.dm:200
    public virtual async DmTask<object> try_deconstruct_loaded_item(object node_path = null)
    {
        if (!(loaded_item is obj_item))
            return FALSE;

        if (node_path == null)
            return await destroy_item(gain_research_points: TRUE);

        datum_techweb_node node_to_discover = SSresearch.techweb_nodes[node_path] as datum_techweb_node;
        if (!(node_to_discover is datum_techweb_node))
            return FALSE;
        Type item_type = loaded_item.type;
        if (!Dm.truthy(await destroy_item()))
            return FALSE;
        SSblackbox.record_feedback("nested tally", "item_deconstructed", 1, Dm.list(Dm.text("[]", node_path), Dm.text("[]", item_type)));
        stored_research.unhide_node(node_to_discover);
        return TRUE;
    }
}
