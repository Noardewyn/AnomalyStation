// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/pipes/bridge_pipe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The bridge pipe's init directions (M3 P4: the bridge pipes are live so their hiding under floor tiles runs, D115), its
// centre overlay and its layer (M3 I1b, D123: the pipes' pictures).

// tg: code/modules/atmospherics/machinery/pipes/bridge_pipe.dm:1
public partial class obj_machinery_atmospherics_pipe_bridge_pipe : obj_machinery_atmospherics_pipe
{
    // tg: code/modules/atmospherics/machinery/pipes/bridge_pipe.dm:19
    public override void set_init_directions(object init_dir = null)
    {
        float sw__1 = dir;
        if (sw__1 == NORTH || sw__1 == SOUTH)
            initialize_directions = Dm.bitor(SOUTH, NORTH);
        else if (sw__1 == EAST || sw__1 == WEST)
            initialize_directions = Dm.bitor(EAST, WEST);
    }

    // tg: code/modules/atmospherics/machinery/pipes/bridge_pipe.dm:26
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        mutable_appearance center = (mutable_appearance)mutable_appearance(Dm.resource("icons/obj/pipes_n_cables/bridge_pipe.dmi"), "bridge_center");
        PIPING_LAYER_DOUBLE_SHIFT(center, piping_layer);
        Dm.append(ref dot, center);
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/pipes/bridge_pipe.dm:32
    public override void update_layer()
    {
        layer = HAS_TRAIT(this, TRAIT_UNDERFLOOR) ? BELOW_CATWALK_LAYER + 1 : Dm.num(Dm.initial(this, (atom p) => p.layer));
    }
}
