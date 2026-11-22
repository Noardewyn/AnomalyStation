// Translated from tgstation (snapshot 1d99bd7f): code/game/shuttle_engines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (D301).

// tg: code/game/shuttle_engines.dm:7
public partial class obj_machinery_power_shuttle_engine : obj_machinery_power
{
    private const int ENGINE_UNWRENCHED = 0;
    private const int ENGINE_WRENCHED = 1;
    private const int ENGINE_WELDED = 2;
    ///How long it takes to weld/unweld an engine in place.
    private const float ENGINE_WELDTIME = 20 * SECONDS;

    // tg: code/game/shuttle_engines.dm:29  var/static/list/connections: per engine here, only those built off a shuttle use it
    private DmList connections => _connections ??= Dm.list(Dm.kv(COMSIG_TURF_ADDED_TO_SHUTTLE, nameof(on_turf_added_to_shuttle)));
    private DmList _connections;

    // tg: code/game/shuttle_engines.dm:38
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        register_context();
        if (!Dm.truthy(mapload))
        {
            engine_state = ENGINE_UNWRENCHED;
            anchored = FALSE;
        }
        return dot;
    }

    // tg: code/game/shuttle_engines.dm:46
    public override object Destroy(float? force = null)
    {
        if (Dm.eq(engine_state, ENGINE_WELDED))
            alter_engine_power(-engine_power);
        unsync_ship();
        return base.Destroy(force);
    }

    // tg: code/game/shuttle_engines.dm:52
    public override async DmTask on_construction(mob user = null, params object[] __extra)
    {
        await base.on_construction(user, __extra);
        if (Dm.truthy(anchored))
        {
            engine_state = ENGINE_WRENCHED;
            await connect_to_shuttle(port: SSshuttle.get_containing_shuttle(this) as obj_docking_port_mobile); //connect to a new ship, if needed
            if (connected_ship == null)
                await AddElement(typeof(datum_element_connect_loc), connections);
        }
    }

    // tg: code/game/shuttle_engines.dm:60
    public override async DmTask<object> connect_to_shuttle(object mapload = null, obj_docking_port_mobile port = null, obj_docking_port_stationary dock = null)
    {
        await base.connect_to_shuttle(mapload, port, dock);
        if (port == null)
            return FALSE;
        connected_ship = port;
        Dm.append(ref connected_ship.engine_list, this);
        if (Dm.truthy(mapload))
            connected_ship.initial_engine_power += engine_power;
        if (Dm.eq(engine_state, ENGINE_WELDED))
            alter_engine_power(engine_power);
        return null;
    }

    // tg: code/game/shuttle_engines.dm:71
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.eq(engine_state, ENGINE_UNWRENCHED))
            Dm.append(ref dot, span_notice(Dm.text("\\The [] is unbolted from the floor. It needs to be wrenched to the floor to be installed.", this)));
        else if (Dm.eq(engine_state, ENGINE_WRENCHED))
            Dm.append(ref dot, span_notice(Dm.text("\\The [] is bolted to the floor and can be unbolted with a wrench. It needs to be welded to the floor to finish installation.", this)));
        else if (Dm.eq(engine_state, ENGINE_WELDED))
            Dm.append(ref dot, span_notice(Dm.text("\\The [] is welded to the floor and can be unwelded. It is currently fully installed.", this)));
        return dot;
    }

    // tg: code/game/shuttle_engines.dm:81
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (Dm.eq(held_item?.tool_behaviour, TOOL_WELDER) && Dm.eq(engine_state, ENGINE_WRENCHED))
            context[SCREENTIP_CONTEXT_LMB] = "Weld to Floor";
        if (Dm.eq(held_item?.tool_behaviour, TOOL_WELDER) && Dm.eq(engine_state, ENGINE_WELDED))
            context[SCREENTIP_CONTEXT_LMB] = "Unweld from Floor";
        if (Dm.eq(held_item?.tool_behaviour, TOOL_WRENCH) && Dm.eq(engine_state, ENGINE_UNWRENCHED))
            context[SCREENTIP_CONTEXT_LMB] = "Wrench to Floor";
        if (Dm.eq(held_item?.tool_behaviour, TOOL_WRENCH) && Dm.eq(engine_state, ENGINE_WRENCHED))
            context[SCREENTIP_CONTEXT_LMB] = "Unwrench from Floor";
        return CONTEXTUAL_SCREENTIP_SET;
    }

    /**
     * Called on destroy and when we need to unsync an engine from their ship.
     */
    // tg: code/game/shuttle_engines.dm:95
    public virtual void unsync_ship()
    {
        if (connected_ship != null)
        {
            Dm.remove(ref connected_ship.engine_list, this);
            connected_ship = null;
        }
        RemoveElement(typeof(datum_element_connect_loc), connections);
    }

    //Ugh this is a lot of copypasta from emitters, welding need some boilerplate reduction
    // tg: code/game/shuttle_engines.dm:102
    public override object can_be_unfasten_wrench(mob user = null, object silent = null)
    {
        if (Dm.eq(engine_state, ENGINE_WELDED))
        {
            if (!Dm.truthy(silent))
                to_chat(user, span_warning(Dm.text("[] is welded to the floor!", this)));
            return FAILED_UNFASTEN;
        }
        return base.can_be_unfasten_wrench(user, silent);
    }

    // tg: code/game/shuttle_engines.dm:109
    public override async DmTask<object> default_unfasten_wrench(mob user = null, obj_item wrench = null, float? time = null)
    {
        time ??= 20;
        object dot = await base.default_unfasten_wrench(user, wrench, time);
        if (Dm.eq(dot, SUCCESSFUL_UNFASTEN))
        {
            if (Dm.truthy(anchored))
            {
                await connect_to_shuttle(port: SSshuttle.get_containing_shuttle(this) as obj_docking_port_mobile); //connect to a new ship, if needed
                if (connected_ship == null)
                    await AddElement(typeof(datum_element_connect_loc), connections);
                engine_state = ENGINE_WRENCHED;
            }
            else
            {
                unsync_ship(); //not part of the ship anymore
                engine_state = ENGINE_UNWRENCHED;
            }
        }
        return dot;
    }

    // tg: code/game/shuttle_engines.dm:121
    public virtual void on_turf_added_to_shuttle(turf source = null, obj_docking_port_mobile port = null)
    {
        Waiting.NoSleep(connect_to_shuttle(port: port));
    }

    // tg: code/game/shuttle_engines.dm:125
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/shuttle_engines.dm:130
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        await base.welder_act(user, tool);
        if (Dm.eq(engine_state, ENGINE_UNWRENCHED))
        {
            to_chat(user, span_warning(Dm.text("\\The [] needs to be wrenched to the floor!", this)));
        }
        else if (Dm.eq(engine_state, ENGINE_WRENCHED))
        {
            if (!Dm.truthy(tool.tool_start_check(user, heat_required: HIGH_TEMPERATURE_REQUIRED)))
                return TRUE;

            user.visible_message(span_notice(Dm.text("[] starts to weld \\the [] to the floor.", user.name, this)),
                span_notice(Dm.text("You start to weld \\the [] to the floor...", this)),
                span_hear("You hear welding."));

            if (Dm.truthy(await tool.use_tool(this, user, ENGINE_WELDTIME, volume: 50)))
            {
                engine_state = ENGINE_WELDED;
                to_chat(user, span_notice(Dm.text("You weld \\the [] to the floor.", this)));
                alter_engine_power(engine_power);
            }
        }
        else if (Dm.eq(engine_state, ENGINE_WELDED))
        {
            if (!Dm.truthy(tool.tool_start_check(user, heat_required: HIGH_TEMPERATURE_REQUIRED)))
                return TRUE;

            user.visible_message(span_notice(Dm.text("[] starts to cut \\the [] free from the floor.", user.name, this)),
                span_notice(Dm.text("You start to cut \\the [] free from the floor...", this)),
                span_hear("You hear welding."));

            if (Dm.truthy(await tool.use_tool(this, user, ENGINE_WELDTIME, volume: 50)))
            {
                engine_state = ENGINE_WRENCHED;
                to_chat(user, span_notice(Dm.text("You cut \\the [] free from the floor.", this)));
                alter_engine_power(-engine_power);
            }
        }
        return TRUE;
    }

    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:384
    public override object hypotheticalShuttleMove(object rotation = null, object move_mode = null, obj_docking_port_mobile moving_dock = null)
    {
        object dot = base.hypotheticalShuttleMove(rotation, move_mode, moving_dock);
        if (Dm.truthy(Dm.bitand(dot, MOVE_AREA)))
            dot = Dm.bitor(dot, MOVE_CONTENTS);
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:389
    public override async DmTask<object> beforeShuttleMove(turf newT = null, object rotation = null, object move_mode = null, obj_docking_port_mobile moving_dock = null)
    {
        object dot = await base.beforeShuttleMove(newT, rotation, move_mode, moving_dock);
        if (Dm.truthy(Dm.bitand(dot, MOVE_AREA)))
            dot = Dm.bitor(dot, MOVE_CONTENTS);
        return dot;
    }

    //Propagates the change to the shuttle.
    // tg: code/game/shuttle_engines.dm:163
    public virtual void alter_engine_power(float mod = 0)
    {
        if (mod != 0 && connected_ship != null)
            connected_ship.alter_engines(mod);
    }
}
