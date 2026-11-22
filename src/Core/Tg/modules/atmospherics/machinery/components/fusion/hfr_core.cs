// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/fusion/hfr_core.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Package E12b (docs/ai/specs/engineering.md): the whole file, the HFR core's Initialize, Destroy and deconstruction. The investigate log
// stays a deferred leaf (admin tooling, not-translated.md 92). The var block is generated but for the radio:
//
// GAP(types): tg's `var/obj/item/radio/radio` (:132) is generated as a Type (rules 3.2 V3: other types assign typepaths to a var of that
// name), which the core's radio instance does not fit; as the supermatter's (M9 slice D), the class declares it, so the type is in the
// Hand mode (types-converter.md 3.2) and its constructor is hand-written here.

/**
 * This section contain the hfr core with all the variables and the Initialize() and Destroy() procs
 */
// tg: code/modules/atmospherics/machinery/components/fusion/hfr_core.dm:4
public partial class obj_machinery_atmospherics_components_unary_hypertorus_core : obj_machinery_atmospherics_components_unary_hypertorus
{
    ///Our internal radio
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_core.dm:132  (hand-written: see the GAP(types) above)
    public obj_item_radio radio;

    // The type's overrides of inherited vars (the Hand mode, types-converter.md 3.2: the class declares radio)
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_core.dm:4
    public obj_machinery_atmospherics_components_unary_hypertorus_core()
    {
        name = Dm.text("\\improper HFR core");
        desc = "This is the Hypertorus Fusion Reactor core, an advanced piece of technology to finely tune the reaction inside of the machine. It has I/O for cooling gases.";
        icon = Dm.resource("icons/obj/machines/atmospherics/hypertorus.dmi");
        icon_state = "core";
        base_icon_state = "core";
        circuit = typeof(obj_item_circuitboard_machine_HFR_core);
        use_power = 1;
        idle_power_usage = 100;
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_core.dm:176  var/static/list/turf_traits (a proc-level static, rules 10.6)
    private static ref DmList Initialize__turf_traits => ref TgState.Current.obj_machinery_atmospherics_components_unary_hypertorus_core__Initialize__turf_traits;

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_core.dm:162
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        internal_fusion = Dm.@new<datum_gas_mixture>();
        internal_fusion.volume = 5000;
        moderator_internal = Dm.@new<datum_gas_mixture>();
        moderator_internal.volume = 10000;

        radio = Dm.@new<obj_item_radio>(this);
        radio.keyslot = Dm.@new(radio_key);
        radio.set_listening(FALSE);
        radio.recalculateChannels();
        investigate_log("has been created.", INVESTIGATE_HYPERTORUS);

        // Our center is unreachable, so prevent stuff from getting stuck in there
        Waiting.NoSleep(AddElement(typeof(datum_element_give_turf_traits), Initialize__turf_traits));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_core.dm:179
    public override object Destroy(float? force = null)
    {
        unregister_signals(TRUE);
        if (internal_fusion != null)
            internal_fusion = null;
        if (moderator_internal != null)
            moderator_internal = null;
        if (linked_input != null)
            QDEL_NULL(ref linked_input);
        if (linked_output != null)
            QDEL_NULL(ref linked_output);
        if (linked_moderator != null)
            QDEL_NULL(ref linked_moderator);
        if (linked_interface != null)
            QDEL_NULL(ref linked_interface);
        foreach (obj_machinery_hypertorus_corner corner in Dm.each<obj_machinery_hypertorus_corner>(corners))
            qdel(corner); // QDEL_NULL(corner): the loop's own var
        QDEL_NULL(ref radio);
        QDEL_NULL(ref soundloop);
        machine_parts = null;
        return base.Destroy(force);
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_core.dm:200
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        turf local_turf = get_turf(loc) as turf;
        datum_gas_mixture to_release = moderator_internal ?? internal_fusion;
        if (to_release == moderator_internal)
            to_release.merge(internal_fusion);
        if (to_release != null)
            await local_turf.assume_air(to_release);
        return await base.on_deconstruction(disassembled);
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_core.dm:209
    public override async DmTask<object> crowbar_deconstruction_act(mob_living user = null, obj_item tool = null, float? internal_pressure = null)
    {
        internal_pressure = max(Dm.num(internal_fusion.return_pressure()), Dm.num(moderator_internal.return_pressure()));
        if (Dm.truthy(internal_pressure))
            await say(I18n.T("WARNING - Core can contain hazardous gases, deconstruct with caution!"));
        return await base.crowbar_deconstruction_act(user, tool, internal_pressure);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_core.dm:176  var/static/list/turf_traits = list(TRAIT_SECLUDED_LOCATION)
    internal DmList obj_machinery_atmospherics_components_unary_hypertorus_core__Initialize__turf_traits = Dm.list(TRAIT_SECLUDED_LOCATION);
}
