// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/pipes/heat_exchange/manifold4w.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 2 (the heat-exchanging pipes): the whole file. The var block is generated.

//4-Way Manifold

// tg: code/modules/atmospherics/machinery/pipes/heat_exchange/manifold4w.dm:3
public partial class obj_machinery_atmospherics_pipe_heat_exchanging_manifold4w : obj_machinery_atmospherics_pipe_heat_exchanging
{
    // tg: code/modules/atmospherics/machinery/pipes/heat_exchange/manifold4w.dm:20
    public override void set_init_directions(object init_dir = null)
    {
        initialize_directions = Dm.num(Dm.initial(this, (obj_machinery_atmospherics p) => p.initialize_directions));
    }

    // tg: code/modules/atmospherics/machinery/pipes/heat_exchange/manifold4w.dm:23
    public override void update_pipe_icon()
    {
        icon_state = Dm.text("[]-[]", base_icon_state, piping_layer);
    }

    // tg: code/modules/atmospherics/machinery/pipes/heat_exchange/manifold4w.dm:26
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        mutable_appearance center = (mutable_appearance)mutable_appearance(icon, "manifold4w_center");
        PIPING_LAYER_DOUBLE_SHIFT(center, piping_layer);
        Dm.append(ref dot, center);
        return dot;
    }
}
