// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/pipes/heat_exchange/manifold.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 2 (the heat-exchanging pipes): the whole file. The var block is generated.

//3-Way Manifold

// tg: code/modules/atmospherics/machinery/pipes/heat_exchange/manifold.dm:3
public partial class obj_machinery_atmospherics_pipe_heat_exchanging_manifold : obj_machinery_atmospherics_pipe_heat_exchanging
{
    // tg: code/modules/atmospherics/machinery/pipes/heat_exchange/manifold.dm:21
    public override void set_init_directions(object init_dir = null)
    {
        initialize_directions = ALL_CARDINALS;
        initialize_directions = Dm.bitand(initialize_directions, Dm.bitnot(dir));
    }

    // tg: code/modules/atmospherics/machinery/pipes/heat_exchange/manifold.dm:25
    public override void update_pipe_icon()
    {
        icon_state = Dm.text("[]-[]", base_icon_state, piping_layer);
    }

    // tg: code/modules/atmospherics/machinery/pipes/heat_exchange/manifold.dm:28
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        mutable_appearance center = (mutable_appearance)mutable_appearance(icon, "manifold_center");
        PIPING_LAYER_DOUBLE_SHIFT(center, piping_layer);

        //Add non-broken pieces
        for (float i = 1, i__end = Dm.num(device_type); i <= i__end; i++)
        {
            if (Dm.truthy(nodes[i]))
                Dm.append(ref dot, get_pipe_image(icon, Dm.text("pipe-[]", piping_layer), get_dir(this, nodes[i] as atom)));
        }
        return dot;
    }
}
