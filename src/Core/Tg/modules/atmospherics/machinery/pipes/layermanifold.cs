// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/pipes/layermanifold.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the layer adaptor's init, its Destroy (nullify_all_nodes) and its init directions (M3 P4: the adaptors are live so
// their hiding under floor tiles runs, D115); M3 I1b (D123: the pipes' pictures): its connections (atmos_init,
// find_all_connections, disconnect, pipeline_expansion, set_piping_layer), its layer and its pictures (update_pipe_icon,
// update_overlays, get_attached_images, get_attached_image). Ventcrawling (relaymove) since gaps X5.

// tg: code/modules/atmospherics/machinery/pipes/layermanifold.dm:1
public partial class obj_machinery_atmospherics_pipe_layer_manifold : obj_machinery_atmospherics_pipe
{
    // tg: code/modules/atmospherics/machinery/pipes/layermanifold.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        front_nodes = Dm.list();
        back_nodes = Dm.list();
        icon_state = "manifoldlayer_center";
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/atmospherics/machinery/pipes/layermanifold.dm:28
    public override object Destroy(float? force = null)
    {
        nullify_all_nodes();
        return base.Destroy(force);
    }

    // tg: code/modules/atmospherics/machinery/pipes/layermanifold.dm:35
    public virtual void nullify_all_nodes()
    {
        foreach (obj_machinery_atmospherics node in Dm.each<obj_machinery_atmospherics>(nodes))
        {
            node.disconnect(this);
            SSair.add_to_rebuild_queue(node);
        }
        front_nodes = null;
        back_nodes = null;
        nodes = Dm.list();
    }

    // tg: code/modules/atmospherics/machinery/pipes/layermanifold.dm:32
    public override void update_pipe_icon()
    {
        return;
    }

    // tg: code/modules/atmospherics/machinery/pipes/layermanifold.dm:43
    public override void update_layer()
    {
        layer = (HAS_TRAIT(this, TRAIT_UNDERFLOOR) ? BELOW_CATWALK_LAYER : Dm.num(Dm.initial(this, (atom p) => p.layer))) + (PIPING_LAYER_MAX * PIPING_LAYER_LCHANGE); //This is above everything else.
    }

    // tg: code/modules/atmospherics/machinery/pipes/layermanifold.dm:46
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        foreach (object node in Dm.each(front_nodes))
        {
            DmList front_images = get_attached_images(node as obj_machinery_atmospherics) as DmList;
            if (Dm.truthy(Dm.length(front_images)))
                Dm.append(ref dot, front_images);
        }
        foreach (object node in Dm.each(back_nodes))
        {
            DmList back_images = get_attached_images(node as obj_machinery_atmospherics) as DmList;
            if (Dm.truthy(Dm.length(back_images)))
                Dm.append(ref dot, back_images);
        }
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/pipes/layermanifold.dm:58
    public virtual object get_attached_images(obj_machinery_atmospherics machine_check = null)
    {
        if (machine_check == null)
            return null;

        DmList dot = Dm.list();

        // Conecting layer manifold to layer manifold will always use omni color pipes in between
        if (istype(machine_check, typeof(obj_machinery_atmospherics_pipe_layer_manifold)))
        {
            string preferred_color = Dm.eq(machine_check.pipe_color, pipe_color) ? pipe_color : ATMOS_COLOR_OMNI;
            for (float i = PIPING_LAYER_MIN; i <= PIPING_LAYER_MAX; i++)
                Dm.append(ref dot, get_attached_image(get_dir(this, machine_check), i, preferred_color));
            return dot;
        }

        // Airlock pump has some snowflake behavior since it has two nodes
        if (istype(machine_check, typeof(obj_machinery_atmospherics_components_unary_airlock_pump)))
        {
            Dm.append(ref dot, get_attached_image(get_dir(this, machine_check), 4, COLOR_BLUE));
            //. += get_attached_image(get_dir(src, machine_check), 2, COLOR_RED) // Only the distro node is added currently to the pipenet, it doesn't merge the pipenet with the waste node
            return dot;
        }

        string connection_color = SELECT_ATMOS_NODE_COLOR(this, machine_check);
        Dm.append(ref dot, get_attached_image(get_dir(this, machine_check), machine_check.piping_layer, connection_color));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/pipes/layermanifold.dm:80
    public virtual object get_attached_image(object p_dir = null, object p_layer = null, object p_color = null)
    {
        // tg's precedence: (FLOAT_LAYER - HAS_TRAIT(...)) ? 1 : 0.01, so always 1 (kept as tg has it)
        float working_layer = Dm.truthy(FLOAT_LAYER - (HAS_TRAIT(this, TRAIT_UNDERFLOOR) ? 1 : 0)) ? 1 : 0.01f;
        mutable_appearance muta = (mutable_appearance)mutable_appearance(Dm.resource("icons/obj/pipes_n_cables/layer_manifold_underlays.dmi"), Dm.text("intact_[]_[]", p_dir, p_layer), layer: working_layer, appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART));
        muta.color = p_color;
        return muta;
    }

    // tg: code/modules/atmospherics/machinery/pipes/layermanifold.dm:86
    public override void set_init_directions(object init_dir = null)
    {
        float sw__1 = dir;
        if (sw__1 == NORTH || sw__1 == SOUTH)
            initialize_directions = Dm.bitor(NORTH, SOUTH);
        else if (sw__1 == EAST || sw__1 == WEST)
            initialize_directions = Dm.bitor(EAST, WEST);
    }

    // tg: code/modules/atmospherics/machinery/pipes/layermanifold.dm:93
    public virtual object find_all_connections()
    {
        front_nodes = Dm.list();
        back_nodes = Dm.list();
        nodes = Dm.list();
        for (float iter = PIPING_LAYER_MIN; iter <= PIPING_LAYER_MAX; iter++)
        {
            obj_machinery_atmospherics foundfront = find_connecting(dir, iter) as obj_machinery_atmospherics;
            obj_machinery_atmospherics foundback = find_connecting(REVERSE_DIR(dir), iter) as obj_machinery_atmospherics;
            Dm.append(ref front_nodes, foundfront);
            Dm.append(ref back_nodes, foundback);
            if (foundfront != null && !QDELETED(foundfront))
                Dm.append(ref nodes, foundfront);
            if (foundback != null && !QDELETED(foundback))
                Dm.append(ref nodes, foundback);
        }
        update_appearance();
        return nodes;
    }

    // tg: code/modules/atmospherics/machinery/pipes/layermanifold.dm:109
    public override void atmos_init(DmList node_connects = null)
    {
        normalize_cardinal_directions();
        find_all_connections();
    }

    // tg: code/modules/atmospherics/machinery/pipes/layermanifold.dm:113
    public override void set_piping_layer(object new_layer = null)
    {
        piping_layer = PIPING_LAYER_DEFAULT;
    }

    // tg: code/modules/atmospherics/machinery/pipes/layermanifold.dm:116
    public override object pipeline_expansion(params object[] __extra)
    {
        return nodes;
    }

    // tg: code/modules/atmospherics/machinery/pipes/layermanifold.dm:119
    public override void disconnect(obj_machinery_atmospherics reference = null)
    {
        if (istype(reference, typeof(obj_machinery_atmospherics_pipe)))
        {
            obj_machinery_atmospherics_pipe pipe_reference = reference as obj_machinery_atmospherics_pipe;
            pipe_reference.destroy_network();
        }
        while (Dm.@in(reference, nodes))
        {
            float i = nodes.Find(reference);
            nodes[i] = null;
            i = front_nodes.Find(reference);
            if (Dm.truthy(i))
                front_nodes[i] = null;
            i = back_nodes.Find(reference);
            if (Dm.truthy(i))
                back_nodes[i] = null;
        }
        update_appearance();
    }
}

// tg: code/modules/atmospherics/machinery/pipes/layermanifold.dm:134  (gaps X5: ventcrawling)
public partial class obj_machinery_atmospherics_pipe_layer_manifold : obj_machinery_atmospherics_pipe
{
    // tg: code/modules/atmospherics/machinery/pipes/layermanifold.dm:134
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (Dm.truthy(Dm.bitand(initialize_directions, direction)))
            return await base.relaymove(user, direction);
        if (Dm.truthy(Dm.bitand(Dm.bitor(NORTH, EAST), direction)))
            user.ventcrawl_layer = clamp(user.ventcrawl_layer + 1, PIPING_LAYER_MIN, PIPING_LAYER_MAX);
        if (Dm.truthy(Dm.bitand(Dm.bitor(SOUTH, WEST), direction)))
            user.ventcrawl_layer = clamp(user.ventcrawl_layer - 1, PIPING_LAYER_MIN, PIPING_LAYER_MAX);
        to_chat(user, I18n.T("You align yourself with the []\\th output.", user.ventcrawl_layer));
        return null;
    }
}
