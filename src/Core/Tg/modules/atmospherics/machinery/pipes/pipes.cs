// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/pipes/pipes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: /obj/machinery/atmospherics/pipe's init, its hiding under floor tiles (the undertile element) and its Destroy
// (M3 P4, D115); M3 I1b (D123: the pipes' pictures): set_volume, nullify_node, destroy_network, the appearance procs
// (update_icon, update_pipe_icon, update_node_icon, update_layer); M4b slice 2 (the pipenets): get_rebuild_target, the gas
// (return_air, return_analyzable_air, remove_air) and the pipenet procs (return_pipenet(s), replace_pipenet, with the gas
// visuals a deferred leaf). M5 slice 7: on_deconstruction (the pipe's air out) and item_interaction (the meter fitting's layer;
// M7 slice 4: the meter is ported).

// tg: code/modules/atmospherics/machinery/pipes/pipes.dm:1
public partial class obj_machinery_atmospherics_pipe : obj_machinery_atmospherics
{
    // tg: code/modules/atmospherics/machinery/pipes/pipes.dm:25
    [DmParams("mapload", "process", "setdir", "init_dir")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(add_atom_colour(pipe_color, FIXED_COLOUR_PRIORITY));
        if (!Dm.truthy(volume)) // Pipes can have specific volumes or have it determined by their device_type.
            volume = UNARY_PIPE_VOLUME * Dm.num(device_type);
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/atmospherics/machinery/pipes/pipes.dm:39
    public override async DmTask setup_hiding()
    {
        await AddElement(typeof(datum_element_undertile), TRAIT_T_RAY_VISIBLE); //if changing this, change the subtypes RemoveElements too, because thats how bespoke works

        // Registering on `COMSIG_OBJ_HIDE` would cause order of operations issues with undertile, so we register to run when undertile updates instead
        RegisterSignal(this, COMSIG_UNDERTILE_UPDATED, nameof(on_hide));
    }

    // tg: code/modules/atmospherics/machinery/pipes/pipes.dm:45  (M5 slice 7: an unwrenched pipe lets its share of the pipenet out)
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        //we delete the parent here so it initializes air_temporary for us. See /datum/pipeline/Destroy() which calls temporarily_store_air()
        QDEL_NULL(ref parent);

        if (air_temporary != null)
        {
            turf T = loc as turf;
            await T.assume_air(air_temporary);
        }

        return await base.on_deconstruction(disassembled);
    }

    // tg: code/modules/atmospherics/machinery/pipes/pipes.dm:55
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref parent);
        return base.Destroy(force);
    }

    // tg: code/modules/atmospherics/machinery/pipes/pipes.dm:31
    public virtual void set_volume(object new_volume = null)
    {
        if (Dm.eq(volume, new_volume))
            return;
        datum_gas_mixture gasmix = parent?.air;
        if (gasmix != null)
            gasmix.volume = gasmix.volume + Dm.num(new_volume) - Dm.num(volume);
        volume = Dm.num(new_volume);
    }

    // tg: code/modules/atmospherics/machinery/pipes/pipes.dm:62
    public override void nullify_node(object i = null)
    {
        obj_machinery_atmospherics old_node = nodes[i] as obj_machinery_atmospherics;
        base.nullify_node(i);
        if (old_node != null)
            SSair.add_to_rebuild_queue(old_node);
    }

    // tg: code/modules/atmospherics/machinery/pipes/pipes.dm:68
    public override void destroy_network()
    {
        QDEL_NULL(ref parent);
    }

    // tg: code/modules/atmospherics/machinery/pipes/pipes.dm:71
    public override object get_rebuild_target()
    {
        if (!QDELETED(parent))
            return null;
        replace_pipenet(parent, Dm.@new<datum_pipeline>());
        return parent;
    }

    // tg: code/modules/atmospherics/machinery/pipes/pipes.dm:77
    public override async DmTask<object> return_air()
    {
        if (air_temporary != null)
            return air_temporary;
        return parent.air;
    }

    // tg: code/modules/atmospherics/machinery/pipes/pipes.dm:82
    public override object return_analyzable_air()
    {
        if (air_temporary != null)
            return air_temporary;
        return parent.air;
    }

    // tg: code/modules/atmospherics/machinery/pipes/pipes.dm:87
    public override object remove_air(object amount = null)
    {
        if (air_temporary != null)
            return air_temporary.remove(amount);
        return parent.air.remove(amount);
    }

    // tg: code/modules/atmospherics/machinery/pipes/pipes.dm:92  (M5 slice 7; M7 slice 4: the meter fitting's layer)
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_pipe_meter)
            return await base.item_interaction(user, tool, modifiers, __extra);

        obj_item_pipe_meter meter = (obj_item_pipe_meter)tool;
        await user.dropItemToGround(meter);
        meter.setAttachLayer(piping_layer);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/pipes/pipes.dm:101
    public override object return_pipenet(params object[] __extra)
    {
        return parent;
    }

    // tg: code/modules/atmospherics/machinery/pipes/pipes.dm:104
    public override void replace_pipenet(datum_pipeline old_pipenet = null, datum_pipeline new_pipenet = null)
    {
        // The gas visuals (vis_contents -= / += parent.GetGasVisual('icons/obj/pipes_n_cables/!pipe_gas_overlays.dmi'),
        // :105-106 and :110-111) are later in M4b: GetGasVisual is a deferred leaf that returns null, and null is no
        // vis_contents member.
        if (parent != null && Dm.truthy(has_gas_visuals) && parent.GetGasVisual(null /* 'icons/obj/pipes_n_cables/!pipe_gas_overlays.dmi': the deferred gas visuals take no icon yet */) is object old_visual)
            Dm.remove(vis_contents, old_visual);

        parent = new_pipenet;

        if (parent != null && Dm.truthy(has_gas_visuals) && parent.GetGasVisual(null /* 'icons/obj/pipes_n_cables/!pipe_gas_overlays.dmi': the deferred gas visuals take no icon yet */) is object new_visual) // null is a valid argument here
            Dm.append(vis_contents, new_visual);
    }

    // tg: code/modules/atmospherics/machinery/pipes/pipes.dm:113
    public override object return_pipenets()
    {
        return Dm.list(parent);
    }

    //--------------------
    // APPEARANCE STUFF

    // tg: code/modules/atmospherics/machinery/pipes/pipes.dm:119
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        update_pipe_icon();
        update_layer();
        return await base.update_icon(updates, __extra);
    }

    // tg: code/modules/atmospherics/machinery/pipes/pipes.dm:124
    public virtual void update_pipe_icon()
    {
        float sw__1 = initialize_directions;
        if (sw__1 == NORTH || sw__1 == EAST || sw__1 == SOUTH || sw__1 == WEST) // Pipes with only a single connection aren't handled by this system
        {
            icon = null;
            return;
        }
        else
            icon = Dm.resource("icons/obj/pipes_n_cables/!pipes_bitmask.dmi");
        float connections = NONE;
        float bitfield = NONE;
        for (float i = 1, i__end = Dm.num(device_type); i <= i__end; i++)
        {
            if (!Dm.truthy(nodes[i]))
                continue;
            obj_machinery_atmospherics node = nodes[i] as obj_machinery_atmospherics;
            float connected_dir = get_dir(this, node);
            connections = Dm.bitor(connections, connected_dir);
        }
        bitfield = CARDINAL_TO_FULLPIPES(connections);
        bitfield = Dm.bitor(bitfield, CARDINAL_TO_SHORTPIPES(Dm.bitand(initialize_directions, Dm.bitnot(connections))));
        icon_state = Dm.text("[]_[]", bitfield, piping_layer);
    }

    // tg: code/modules/atmospherics/machinery/pipes/pipes.dm:150
    public override void update_layer()
    {
        layer = (HAS_TRAIT(this, TRAIT_UNDERFLOOR) ? BELOW_CATWALK_LAYER : Dm.num(Dm.initial(this, (atom p) => p.layer))) + (piping_layer - PIPING_LAYER_DEFAULT) * PIPING_LAYER_LCHANGE + (Dm.num(GLOB.pipe_colors_ordered[pipe_color]) * 0.0001f);
    }
}

// tg: code/modules/atmospherics/machinery/pipes/pipes.dm:143 (a proc of /obj/machinery/atmospherics defined in pipes.dm)
public partial class obj_machinery_atmospherics
{
    // tg: code/modules/atmospherics/machinery/pipes/pipes.dm:143
    public virtual async DmTask update_node_icon()
    {
        for (float i = 1, i__end = Dm.num(device_type); i <= i__end; i++)
        {
            if (!Dm.truthy(nodes[i]))
                continue;
            obj_machinery_atmospherics current_node = nodes[i] as obj_machinery_atmospherics;
            await current_node.update_icon();
        }
    }
}
