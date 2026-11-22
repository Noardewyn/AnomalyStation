// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/binary_devices/passive_gate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b slice 2: the passive gate): the init, the ctrl and alt clicks, the picture, process_atmos and
// can_unwrench. Deferred leaves: the screentip context (M9) and the investigate logs (M10). Generated live-partial stubs:
// add_context (M9) and the tgui window (ui_interact, ui_data, ui_act: M9); the ventcrawling relaymove since gaps X5. The var block
// is generated.

/*
Passive gate is similar to the regular pump except:
* It doesn't require power
* Can not transfer low pressure to higher pressure (so it's more like a valve where you can control the flow)
* Passes gas when output pressure lower than target pressure
*/

// tg: code/modules/atmospherics/machinery/components/binary_devices/passive_gate.dm:10
public partial class obj_machinery_atmospherics_components_binary_passive_gate : obj_machinery_atmospherics_components_binary
{
    // tg: code/modules/atmospherics/machinery/components/binary_devices/passive_gate.dm:25
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // register_context() (datums/screentips/atom_context.dm:5): the screentip context of the held tool
        Dm.Deferred("code/modules/atmospherics/machinery/components/binary_devices/passive_gate.dm:28", "Initialize: register_context(): screentip contexts (M10+)");
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/passive_gate.dm:35
    public override async DmTask<object> click_ctrl(mob user = null)
    {
        if (Dm.truthy(is_operational))
        {
            set_on(Dm.truthy(on) ? FALSE : TRUE);
            balloon_alert(user, Dm.text("turned []", Dm.truthy(on) ? "on" : "off"));
            investigate_log(Dm.text("was turned [] by []", Dm.truthy(on) ? "on" : "off", key_name(user)), INVESTIGATE_ATMOS);
            return CLICK_ACTION_SUCCESS;
        }
        return CLICK_ACTION_BLOCKING;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/passive_gate.dm:43
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (target_pressure == MAX_OUTPUT_PRESSURE)
            return CLICK_ACTION_BLOCKING;

        target_pressure = MAX_OUTPUT_PRESSURE;
        investigate_log(Dm.text("was set to [] kPa by []", target_pressure, key_name(user)), INVESTIGATE_ATMOS);
        balloon_alert(user, Dm.text("pressure output set to [] kPa", target_pressure));
        update_appearance(UPDATE_ICON);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/passive_gate.dm:53
    public override void update_icon_nopipes(params object[] __extra)
    {
        string new_icon_state = Dm.text("passgate_[]-[]", Dm.truthy(on) && Dm.truthy(is_operational) ? "on" : "off", set_overlay_offset(piping_layer));
        string old_icon_state = icon_state;
        icon_state = new_icon_state;
        if (new_icon_state != old_icon_state)
            update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/passive_gate.dm:60
    public override object process_atmos(object seconds_per_tick = null)
    {
        if (!Dm.truthy(on))
            return null;

        datum_gas_mixture input_air = airs[1] as datum_gas_mixture;
        datum_gas_mixture output_air = airs[2] as datum_gas_mixture;
        datum_gas_mixture output_pipenet_air = (parents[2] as datum_pipeline).air;

        if (Dm.truthy(input_air.release_gas_to(output_air, target_pressure, output_pipenet_air: output_pipenet_air)))
            update_parents();
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/passive_gate.dm:111
    public override object can_unwrench__proc(mob user = null)
    {
        object dot = base.can_unwrench__proc(user);
        if (Dm.truthy(dot) && Dm.truthy(on))
        {
            to_chat(user, span_warning(Dm.text("You cannot unwrench [], turn it off first!", this)));
            return FALSE;
        }
        return dot;
    }
}

// tg: code/modules/atmospherics/machinery/components/binary_devices/passive_gate.dm:71  (gaps X5: ventcrawling)
public partial class obj_machinery_atmospherics_components_binary_passive_gate : obj_machinery_atmospherics_components_binary
{
    // tg: code/modules/atmospherics/machinery/components/binary_devices/passive_gate.dm:71
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (!Dm.truthy(on) || !Dm.eq(direction, dir))
            return null;
        return await base.relaymove(user, direction);
    }
}
