// Translated from tgstation (snapshot 1d99bd7f): code/modules/plumbing/plumbers/destroyer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file but add_context (live-partial, screentips). The var block is generated (D111).

// tg: code/modules/plumbing/plumbers/destroyer.dm:4
public partial class obj_machinery_plumbing_disposer : obj_machinery_plumbing
{
    //Maximum disposal rate
    // tg: code/modules/plumbing/plumbers/destroyer.dm:2  #define MAX_DISPOSAL_RATE 25 (file-local, rules 11.4)
    private const float MAX_DISPOSAL_RATE = 25;

    // tg: code/modules/plumbing/plumbers/destroyer.dm:17
    [DmParams("mapload", "layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object layer = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_simple_demand_disposer), layer));
        RegisterSignal(reagents as datum, COMSIG_REAGENTS_HOLDER_UPDATED, nameof(update));
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/destroyer.dm:22
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("It is disposing []u reagents per second.", disposal_rate)));
        Dm.append(ref dot, span_notice(I18n.T("Use hand to change disposal rate.")));
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/destroyer.dm:27  add_context: live-partial (screentips).

    // tg: code/modules/plumbing/plumbers/destroyer.dm:34
    [SignalHandler]
    public virtual void update()
    {
        update_appearance(UPDATE_ICON_STATE);
    }

    // tg: code/modules/plumbing/plumbers/destroyer.dm:39
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(is_operational) && Dm.truthy(anchored) && Dm.truthy(on) && Dm.truthy(((datum_reagents)reagents).total_volume) ? "_working" : "");
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/plumbing/plumbers/destroyer.dm:43
    public override async DmTask on_set_is_operational(object old_value = null)
    {
        await base.on_set_is_operational(old_value);
        update_appearance(UPDATE_ICON_STATE);
    }

    // tg: code/modules/plumbing/plumbers/destroyer.dm:47
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act(user, tool);
        if (Dm.eq(dot, ITEM_INTERACT_SUCCESS))
            update_appearance(UPDATE_ICON_STATE);
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/destroyer.dm:52
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ChemDisposer", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/plumbing/plumbers/destroyer.dm:58
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        return Dm.list(
            Dm.kv("max_volume", MAX_DISPOSAL_RATE));
    }

    // tg: code/modules/plumbing/plumbers/destroyer.dm:63
    public override async DmTask<object> ui_data(mob user = null)
    {
        return Dm.list(
            Dm.kv("enabled", on),
            Dm.kv("disposal_rate", disposal_rate));
    }

    // tg: code/modules/plumbing/plumbers/destroyer.dm:69
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "toggle_power":
                on = !Dm.truthy(on) ? TRUE : FALSE;
                update_appearance(UPDATE_ICON_STATE);
                return TRUE;

            case "change_volume":
            {
                float? num = text2num(@params["volume"]);
                if (!isnum(num))
                    return FALSE;

                disposal_rate = round(clamp(num.Value, 0.1f, MAX_DISPOSAL_RATE), CHEMICAL_VOLUME_ROUNDING);
                return TRUE;
            }
        }
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/destroyer.dm:88
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        datum_reagents holder = (datum_reagents)reagents;
        if (!Dm.truthy(is_operational) || !Dm.truthy(holder.total_volume) || !Dm.truthy(on))
            return null;
        Waiting.NoSleep(holder.remove_all(disposal_rate * Dm.num(seconds_per_tick)));
        Waiting.NoSleep(use_energy((disposal_rate / MAX_DISPOSAL_RATE) * active_power_usage * Dm.num(seconds_per_tick)));
        return null;
    }
}
