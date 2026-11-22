// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/pipes/color_adapter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 2 (the colour adapter): the whole file. The var block is generated.

// tg: code/modules/atmospherics/machinery/pipes/color_adapter.dm:1
public partial class obj_machinery_atmospherics_pipe_color_adapter : obj_machinery_atmospherics_pipe
{
    ///cache for the icons
    // tg: code/modules/atmospherics/machinery/pipes/color_adapter.dm:22  var/static/list/mutable_appearance/center_cache = list() (rules 10.6: a TgState field)
    public static ref DmList center_cache => ref TgState.Current.obj_machinery_atmospherics_pipe_color_adapter__center_cache;

    // tg: code/modules/atmospherics/machinery/pipes/color_adapter.dm:24
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        icon_state = "";
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/atmospherics/machinery/pipes/color_adapter.dm:28
    public override void set_init_directions(object init_dir = null)
    {
        float sw__1 = dir;
        if (sw__1 == NORTH || sw__1 == SOUTH)
            initialize_directions = Dm.bitor(SOUTH, NORTH);
        else if (sw__1 == EAST || sw__1 == WEST)
            initialize_directions = Dm.bitor(EAST, WEST);
    }

    // tg: code/modules/atmospherics/machinery/pipes/color_adapter.dm:35
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        center_cache ??= Dm.list();
        mutable_appearance center = center_cache[Dm.text("[]", piping_layer)] as mutable_appearance;
        if (center == null)
        {
            center = (mutable_appearance)mutable_appearance(Dm.initial(this, (atom p) => p.icon), "adapter_center");
            PIPING_LAYER_DOUBLE_SHIFT(center, piping_layer);
            center_cache[Dm.text("[]", piping_layer)] = center;
        }
        Dm.append(ref dot, center);

        update_layer();

        //Add non-broken pieces
        for (float i = 1, i__end = Dm.num(device_type); i <= i__end; i++)
        {
            if (!Dm.truthy(nodes[i]))
                continue;
            float node_dir = get_dir(this, nodes[i] as atom);
            image pipe = get_pipe_image(Dm.resource("icons/obj/pipes_n_cables/manifold.dmi"), "pipe-3", node_dir, get_node_color(nodes[i] as obj_machinery_atmospherics, node_dir) as string) as image;
            pipe.appearance_flags = Dm.bitor(pipe.appearance_flags, Dm.bitor(RESET_COLOR, KEEP_APART));
            PIPING_LAYER_DOUBLE_SHIFT(pipe, piping_layer);
            pipe.layer = layer + 0.01f;
            Dm.append(ref dot, pipe);
        }
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/pipes/color_adapter.dm:57
    public virtual object get_node_color(obj_machinery_atmospherics node = null, object node_dir = null)
    {
        node_dir ??= get_dir(this, node);
        // If we are omni always use input color
        if (Dm.eq(pipe_color, ATMOS_COLOR_OMNI))
            return node.pipe_color;

        // If we are connecting to a non-sharer (like another color adapter)
        if (Dm.truthy(Dm.bitand(node.pipe_flags, PIPING_DONT_SHARE_COLOR)))
        {
            // Use our own color if the other node is omni
            if (Dm.eq(node.pipe_color, ATMOS_COLOR_OMNI))
                return pipe_color;
            // South/east are prioritized, so it doesn't change based on update order
            if (Dm.truthy(Dm.bitand(node_dir, Dm.bitor(SOUTH, EAST))))
                return pipe_color;
        }

        // Otherwise just use the node's color
        return node.pipe_color;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/atmospherics/machinery/pipes/color_adapter.dm:22  /obj/machinery/atmospherics/pipe/color_adapter/var/static/list/center_cache
    internal DmList obj_machinery_atmospherics_pipe_color_adapter__center_cache;
}
