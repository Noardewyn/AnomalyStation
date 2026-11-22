// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/pipes/multiz.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice D172 (the multi deck pipe adapter, which the rapid pipe dispenser dispenses): the whole file. MetaStation has one deck,
// so GET_TURF_ABOVE and GET_TURF_BELOW give null and the adapter joins no other deck (multiz.dm:49-60 as tg writes it). The var block
// is generated.

/// This is an atmospherics pipe which can relay air up/down a deck.
// tg: code/modules/atmospherics/machinery/pipes/multiz.dm:2
public partial class obj_machinery_atmospherics_pipe_multiz : obj_machinery_atmospherics_pipe
{
    // tg: code/modules/atmospherics/machinery/pipes/multiz.dm:25
    [DmParams("mapload", "process", "setdir", "init_dir")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        icon_state = "";
        center = (mutable_appearance)mutable_appearance(icon, "adapter_center", layer: HIGH_OBJ_LAYER);
        pipe = (mutable_appearance)mutable_appearance(icon, Dm.text("pipe-[]", piping_layer));
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/atmospherics/machinery/pipes/multiz.dm:31
    public override void set_init_directions(object init_dir = null)
    {
        initialize_directions = dir;
    }

    // tg: code/modules/atmospherics/machinery/pipes/multiz.dm:34
    public override void update_layer()
    {
        return; // Noop because we're moving this to /obj/machinery/atmospherics/pipe
    }

    // tg: code/modules/atmospherics/machinery/pipes/multiz.dm:37
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        pipe.appearance_flags = Dm.bitor(pipe.appearance_flags, Dm.bitor(RESET_COLOR, KEEP_APART));
        pipe.color = SELECT_ATMOS_NODE_COLOR(this, nodes[1] as obj_machinery_atmospherics);
        pipe.icon_state = Dm.text("pipe-[]", piping_layer);
        Dm.append(ref dot, pipe);

        center.pixel_w = PIPING_LAYER_P_X * (Dm.num(piping_layer) - PIPING_LAYER_DEFAULT);
        Dm.append(ref dot, center);
        return dot;
    }

    ///Attempts to locate a multiz pipe that's above us, if it finds one it merges us into its pipenet
    // tg: code/modules/atmospherics/machinery/pipes/multiz.dm:49
    public override object pipeline_expansion(params object[] __extra)
    {
        turf local_turf = get_turf(this) as turf;
        foreach (obj_machinery_atmospherics_pipe_multiz above in Dm.each<obj_machinery_atmospherics_pipe_multiz>(GET_TURF_ABOVE(local_turf)))
        {
            if (!Dm.truthy(is_connectable(above, piping_layer)))
                continue;
            nodes[2] = above;
            above.nodes[3] = this; //Two way travel :)
        }
        foreach (obj_machinery_atmospherics_pipe_multiz below in Dm.each<obj_machinery_atmospherics_pipe_multiz>(GET_TURF_BELOW(local_turf)))
        {
            if (!Dm.truthy(is_connectable(below, piping_layer)))
                continue;
            below.pipeline_expansion(); //If we've got one below us, force it to add us on facebook
        }
        return base.pipeline_expansion(__extra);
    }
}
