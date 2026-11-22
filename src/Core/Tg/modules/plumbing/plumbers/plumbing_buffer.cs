// Translated from tgstation (snapshot 1d99bd7f): code/modules/plumbing/plumbers/plumbing_buffer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file but add_context (live-partial, screentips). The var block is generated (D111).

// tg: code/modules/plumbing/plumbers/plumbing_buffer.dm:1
public partial class obj_machinery_plumbing_buffer : obj_machinery_plumbing
{
    // tg: code/modules/plumbing/plumbers/plumbing_buffer.dm:15
    [DmParams("mapload", "layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object layer = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_buffer), layer));
        RegisterSignal(reagents as datum, COMSIG_REAGENTS_HOLDER_UPDATED, nameof(on_reagent_change));
        return dot;
    }

    ///Removes this buffer from the list of connected buffers
    // tg: code/modules/plumbing/plumbers/plumbing_buffer.dm:21
    public virtual void disconnect()
    {
        if (!Dm.truthy(connections))
            return;

        Dm.remove(ref connections, this);
        foreach (obj_machinery_plumbing_buffer node in Dm.each_cast<obj_machinery_plumbing_buffer>(connections))
            node.on_reagent_change();
        connections = null;
    }

    // tg: code/modules/plumbing/plumbers/plumbing_buffer.dm:31
    public override object Destroy(float? force = null)
    {
        disconnect();
        return base.Destroy(force);
    }

    // tg: code/modules/plumbing/plumbers/plumbing_buffer.dm:35  add_context: live-partial (screentips).

    // tg: code/modules/plumbing/plumbers/plumbing_buffer.dm:42
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("It activates at a threshold of []u of reagents", activation_volume)));
        switch (mode)
        {
            case AB_UNREADY:
                Dm.append(ref dot, span_notice(I18n.T("It is filling up on reagents.")));
                break;
            case AB_IDLE:
                Dm.append(ref dot, span_notice(I18n.T("It is waiting on other buffers to activate.")));
                break;
            case AB_READY:
                Dm.append(ref dot, span_notice(I18n.T("It is sending reagents.")));
                break;
        }
        Dm.append(ref dot, span_notice(I18n.T("Its activation threshold can be changed with by [].", EXAMINE_HINT(I18n.T("hand")))));
        Dm.append(ref dot, span_notice(I18n.T("Its connections can be changed with a [].", EXAMINE_HINT(I18n.T("screwdriver")))));
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/plumbing_buffer.dm:55
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.initial(this, (atom p) => p.icon_state);
        if (!Dm.truthy(anchored) || !Dm.truthy(is_operational))
            return dot;

        switch (mode)
        {
            case AB_UNREADY:
                icon_state += "_red";
                break;
            case AB_IDLE:
                icon_state += "_yellow";
                break;
            case AB_READY:
                icon_state += "_green";
                break;
        }
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/plumbing_buffer.dm:69
    [SignalHandler]
    public virtual void on_reagent_change()
    {
        if (Dm.eq(mode, AB_UNREADY))
        {
            if (((datum_reagents)reagents).total_volume >= activation_volume)
            {
                mode = AB_IDLE;
                update_appearance(UPDATE_ICON_STATE);
            }
        }

        if (Dm.eq(mode, AB_IDLE))
        {
            foreach (obj_machinery_plumbing_buffer node in Dm.each_cast<obj_machinery_plumbing_buffer>(connections))
            {
                if (Dm.eq(node.mode, AB_UNREADY))
                    return;
            }

            foreach (obj_machinery_plumbing_buffer node in Dm.each_cast<obj_machinery_plumbing_buffer>(connections))
            {
                node.mode = AB_READY;
                node.update_appearance(UPDATE_ICON_STATE);
            }
        }

        if (Dm.eq(mode, AB_READY))
        {
            foreach (obj_machinery_plumbing_buffer node in Dm.each_cast<obj_machinery_plumbing_buffer>(connections))
            {
                if (Dm.truthy(((datum_reagents)node.reagents).total_volume))
                    return;
            }

            foreach (obj_machinery_plumbing_buffer node in Dm.each_cast<obj_machinery_plumbing_buffer>(connections))
            {
                node.mode = AB_UNREADY;
                node.update_appearance(UPDATE_ICON_STATE);
            }
        }
    }

    // tg: code/modules/plumbing/plumbers/plumbing_buffer.dm:95
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        object dot = ITEM_INTERACT_SUCCESS;

        //clear existing connections and ourself to it for other machines
        LAZYCLEARLIST(connections);

        //Discover new connections
        DmList queue = Dm.list(this);
        DmList visited = Dm.list();
        while (Dm.truthy(queue.len))
        {
            obj_machinery_plumbing_buffer node = popleft(queue) as obj_machinery_plumbing_buffer;
            node.connections = connections;
            LAZYADD(ref connections, node);
            visited[node] = TRUE;

            foreach (object direction in Dm.each(GLOB.cardinals))
            {
                foreach (obj_machinery_plumbing_buffer sub_node in Dm.each<obj_machinery_plumbing_buffer>(get_step(node, Dm.num(direction))))
                {
                    if (!Dm.truthy(sub_node.anchored) || Dm.truthy(visited[sub_node]))
                        continue;
                    Dm.append(ref queue, sub_node);
                }
            }
        }

        //Ping the alert to the player
        foreach (obj_machinery_plumbing_buffer node in Dm.each_cast<obj_machinery_plumbing_buffer>(connections))
        {
            node.is_operational = FALSE;
            node.update_appearance(UPDATE_ICON_STATE);
            node.add_overlay(icon_state + "_alert");
            _addtimer(Dm.@new<datum_callback>(node, nameof(cut_overlay), icon_state + "_alert"), 2 * SECONDS, file: "code/modules/plumbing/plumbers/plumbing_buffer.dm", line: 121);
            node.is_operational = TRUE;
            node.mode = AB_UNREADY;
            node.on_reagent_change();
            _addtimer(Dm.@new<datum_callback>(node, nameof(update_appearance), UPDATE_ICON_STATE), 2.5f * SECONDS, file: "code/modules/plumbing/plumbers/plumbing_buffer.dm", line: 125);
        }
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/plumbing_buffer.dm:127
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act(user, tool);
        if (Dm.eq(dot, ITEM_INTERACT_SUCCESS))
        {
            disconnect();
            update_appearance(UPDATE_ICON_STATE);
        }
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/plumbing_buffer.dm:133
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ChemAutomaticBuffer", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/plumbing/plumbers/plumbing_buffer.dm:139
    public override async DmTask<object> ui_data(mob user = null)
    {
        return Dm.list(
            Dm.kv("threshold", activation_volume),
            Dm.kv("connections", max(LAZYLEN(connections) - 1, 0)));
    }

    // tg: code/modules/plumbing/plumbers/plumbing_buffer.dm:145
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "set_threshold":
            {
                object value = @params["threshold"];
                value = text2num(value);
                if (!Dm.truthy(value))
                    return FALSE;

                activation_volume = clamp(Dm.num(value), 1, Dm.num(buffer));
                return TRUE;
            }

            case "sync":
                foreach (obj_machinery_plumbing_buffer node in Dm.each_cast<obj_machinery_plumbing_buffer>(connections))
                    node.activation_volume = activation_volume;
                break;
        }
        return dot;
    }
}
