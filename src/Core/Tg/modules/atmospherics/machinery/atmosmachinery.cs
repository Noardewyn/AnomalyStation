// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/atmosmachinery.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: M3 P4 (D115): the init with the pipe hiding setup, Destroy, normalize_cardinal_directions and the base
// set_init_directions; M3 I1b (D123: the pipes' pictures): the node search of atmos_init (get_node_connects, can_be_node,
// connection_check, is_connectable, find_connecting, get_init_directions), nullify_node, disconnect, update_icon, the base
// update_layer and the cap visuals; on_hide and get_pipe_image (the owner's play test 2026-10-09). M4b slice 2 (the
// pipenets): post_machine_initialize, set_on, the pipenet procs (rebuild_pipes, get_rebuild_target, pipeline_expansion,
// return_pipenet(s), return_pipenet_airs, set_pipenet, replace_pipenet), set_piping_layer, can_unwrench, update_name,
// Entered, can_see_pipes, paint, set_pipe_color, portable_device_connected and atmos_conditions_changed (/atom,
// /atom/movable, /turf/open). Generated live-partial stubs: the tool acts and construction (M4a, M5), the
// unsafe pressure release (the thrown user, M5 with the wrench), ventcrawling (relaymove, M10+), the admin var edit and
// map export (M10), the singularity (M10+) and the shuttles (M10).

// tg: code/modules/atmospherics/machinery/atmosmachinery.dm:13
public partial class obj_machinery_atmospherics : obj_machinery
{
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:81
    [DmParams("mapload", "process", "setdir", "init_dir")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object process = Dm.arg(__extra, 0);
        object setdir = Dm.arg(__extra, 1);
        object init_dir = Dm.arg(__extra, 2);
        process ??= TRUE;
        init_dir ??= initialize_directions;
        if (setdir != null)
            Waiting.NoSleep(setDir(setdir));
        if (Dm.truthy(Dm.bitand(pipe_flags, PIPING_CARDINAL_AUTONORMALIZE)))
            normalize_cardinal_directions();
        nodes = Dm.list_n(Dm.num(device_type));
        set_init_directions(init_dir);

        if (Dm.truthy(mapload) && !Dm.eq(name, Dm.initial(this, (obj_machinery_atmospherics p) => p.name)))
            override_naming = TRUE;
        turf turf_loc = null;
        if (isturf(loc))
        {
            turf_loc = loc as turf;
            turf_loc.add_blueprints_preround(this);
        }

        if (Dm.truthy(hide))
            Waiting.NoSleep(setup_hiding());

        SSspatial_grid.add_grid_awareness(this, SPATIAL_GRID_CONTENTS_TYPE_ATMOS);
        SSspatial_grid.add_grid_membership(this, turf_loc, SPATIAL_GRID_CONTENTS_TYPE_ATMOS);
        if (Dm.truthy(process))
            SSair.start_processing_machine(this);
        return base.Initialize(mapload, Dm.forward(__extra, process, setdir, init_dir));
    }

    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:105
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        update_name();
    }

    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:109
    public override object Destroy(float? force = null)
    {
        // DEVIATION(DV31): an inert device (D112) never ran its Initialize, so it has no nodes, parents or airs to let go of
        if (!InertSeam.IsInert(this))
            for (float i = 1, i__end = Dm.num(device_type); i <= i__end; i++)
                nullify_node(i);

        SSair.stop_processing_machine(this);
        Dm.remove(ref SSair.rebuild_queue, this);

        QDEL_NULL(ref pipe_vision_img);
        QDEL_NULL(ref cap_overlay);

        return base.Destroy(force);
    }

    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:121
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("[] is on layer [].", this, piping_layer)));
        if (Dm.truthy(Dm.bitand(vent_movement, VENTCRAWL_ENTRANCE_ALLOWED)) && isliving(user))
        {
            mob_living L = (mob_living)user;
            if (HAS_TRAIT(L, TRAIT_VENTCRAWLER_NUDE) || HAS_TRAIT(L, TRAIT_VENTCRAWLER_ALWAYS))
                Dm.append(ref dot, span_notice(I18n.T("Alt-click to crawl through it.")));
        }
        return dot;
    }

    /**
     * Sets up our pipe hiding logic, consolidated in one place so subtypes may override it.
     * This lets subtypes implement their own hiding logic without needing to worry about conflicts with the parent hiding logic.
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:133
    public virtual async DmTask setup_hiding()
    {
        // Register pipe cap updating when hidden/unhidden
        RegisterSignal(this, COMSIG_OBJ_HIDE, nameof(on_hide));
    }

    /**
     * Signal handler. Updates both our pipe cap visuals and those of adjacent nodes.
     * We update adjacent nodes as their pipe caps are based partially on our state, so they need updating as well.
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:141
    [ShouldCallParent, SignalHandler]
    public virtual void on_hide(datum source = null, params object[] __extra)
    {
        foreach (obj_machinery_atmospherics node in Dm.each<obj_machinery_atmospherics>(nodes))
            Waiting.NoSleep(node.update_cap_visuals());

        Waiting.NoSleep(update_cap_visuals());
    }

    /**
     * Called by the machinery disconnect(), custom for each type
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:173
    public virtual void destroy_network()
    {
        return;
    }

    /**
     * Turns the machine on/off
     * Arguments
     *
     * * active - the state of the machine
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:182
    [ShouldCallParent]
    public virtual void set_on(object active = null)
    {
        if (Dm.eq(active, on))
            return;

        on = Dm.num(active);
        update_appearance(UPDATE_ICON);
        if (Dm.truthy(_listen_lookup?[COMSIG_ATMOS_MACHINE_SET_ON])) // SEND_SIGNAL (__DEFINES/dcs/helpers.dm:7)
            _SendSignal(COMSIG_ATMOS_MACHINE_SET_ON, Dm.list(this, on));
    }

    /// This should only be called by SSair as part of the rebuild queue.
    /// Handles rebuilding pipelines after init or they've been changed.
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:194
    public virtual void rebuild_pipes()
    {
        rebuilding = FALSE;
        datum_pipeline build_target = get_rebuild_target() as datum_pipeline;
        if (build_target == null)
            return;
        build_target.build_pipeline(this); //This'll add to the expansion queue
        SSair.add_to_rebuild_queue(this); // expansion can connect several ports. revisit this machine after the expansion queue has been processed
    }

    /**
     * Assigns and returns the next pipeline that needs to be built up. Make sure to expand it before requesting another
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:205
    public virtual object get_rebuild_target()
    {
        return null;
    }

    /**
     * Called on destroy(mostly deconstruction) and when moving nodes around, disconnect the nodes from the network
     * Arguments:
     * * i - is the current iteration of the node, based on the device_type (from 1 to 4)
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:213
    public virtual void nullify_node(object i = null)
    {
        if (nodes == null || !Dm.truthy(nodes[i]))
            return;
        obj_machinery_atmospherics node_machine = nodes[i] as obj_machinery_atmospherics;
        node_machine.disconnect(this);
        nodes[i] = null;
    }

    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:225
    public virtual void normalize_cardinal_directions()
    {
        float sw__1 = dir;
        if (sw__1 == SOUTH)
            Waiting.NoSleep(setDir(NORTH));
        else if (sw__1 == WEST)
            Waiting.NoSleep(setDir(EAST));
    }

    /**
     * setter for pipe layers
     *
     * Set the layer of the pipe that the device has to a new_layer
     * Arguments:
     * * new_layer - the layer at which we want the piping_layer to be (1 to 5)
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:239
    public virtual void set_piping_layer(object new_layer = null)
    {
        piping_layer = Dm.truthy(Dm.bitand(pipe_flags, PIPING_DEFAULT_LAYER_ONLY)) ? PIPING_LAYER_DEFAULT : Dm.num(new_layer);
        update_appearance();
    }

    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:243
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        update_layer();
        await update_cap_visuals();
        return await base.update_icon(updates, __extra);
    }

    /**
     * Find a connecting /obj/machinery/atmospherics in specified direction, called by relaymove()
     * used by ventcrawling mobs to check if they can move inside a pipe in a specific direction
     * Arguments:
     * * direction - the direction we are checking against
     * * prompted_layer - the piping_layer we are inside
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:255
    public virtual object find_connecting(object direction = null, object prompted_layer = null)
    {
        foreach (obj_machinery_atmospherics target in Dm.each<obj_machinery_atmospherics>(get_step_multiz(this, direction)))
        {
            if (!Dm.truthy(Dm.bitand(target.initialize_directions, get_dir(target, this))) && !istype(target, typeof(obj_machinery_atmospherics_pipe_multiz)))
                continue;
            if (Dm.truthy(connection_check(target, prompted_layer)))
                return target;
        }
        return null;
    }

    /**
     * Getter for node_connects
     *
     * Return a list of the nodes that can connect to other machines, get called by atmos_init()
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:267
    public virtual object get_node_connects()
    {
        DmList node_connects = Dm.list_n(Dm.num(device_type)); //empty list of size device_type

        object init_directions = get_init_directions();
        for (float i = 1, i__end = Dm.num(device_type); i <= i__end; i++)
        {
            foreach (object direction in Dm.each(GLOB.cardinals))
            {
                if (!Dm.truthy(Dm.bitand(Dm.num(direction), Dm.num(init_directions))))
                    continue;
                if (Dm.@in(direction, node_connects))
                    continue;
                node_connects[i] = direction;
                break;
            }
        }

        return node_connects;
    }

    /**
     * Initialize for atmos devices
     *
     * initialize the nodes for each pipe/device, this is called just after the air controller sets up turfs
     * Arguments:
     * * list/node_connects - a list of the nodes on the device that can make a connection to other machines
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:289
    public virtual void atmos_init(DmList node_connects = null)
    {
        if (!Dm.truthy(node_connects)) //for pipes where order of nodes doesn't matter
            node_connects = get_node_connects() as DmList;

        for (float i = 1, i__end = Dm.num(device_type); i <= i__end; i++)
        {
            foreach (obj_machinery_atmospherics target in Dm.each<obj_machinery_atmospherics>(get_step(this, Dm.num(node_connects[i]))))
            {
                // DEVIATION(DV31): an inert device (D112: its tg code is not all translated, so it has no gas mixtures and
                // no pipenet procs of its own) is no node; the pipe ends open at it until its type is translated.
                if (InertSeam.IsInert(target))
                    continue;
                if (Dm.truthy(can_be_node(target)))
                {
                    nodes[i] = target;
                    break;
                }
            }
        }

        update_appearance();
    }

    /**
     * Check if a node can actually exists by connecting to another machine
     * called on atmos_init()
     * Arguments:
     * * obj/machinery/atmospherics/target - the machine we are connecting to
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:307
    public virtual object can_be_node(obj_machinery_atmospherics target = null)
    {
        return connection_check(target, piping_layer);
    }

    /**
     * Check the connection between two nodes
     *
     * Check if our machine and the target machine are connectable by both calling isConnectable and by checking that the directions and piping_layer are compatible
     * called by can_be_node() (for building a network) and find_connecting() (for ventcrawling)
     * Arguments:
     * * obj/machinery/atmospherics/target - the machinery we want to connect to
     * * given_layer - the piping_layer we are checking
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:319
    public virtual object connection_check(obj_machinery_atmospherics target = null, object given_layer = null)
    {
        //if target is not multiz then we have to check if the target & src connect in the same direction
        if (!istype(target, typeof(obj_machinery_atmospherics_pipe_multiz)) && !(Dm.truthy(Dm.bitand(initialize_directions, get_dir(this, target))) && Dm.truthy(Dm.bitand(target.initialize_directions, get_dir(target, this)))))
            return FALSE;

        //both target & src can't be connected either way
        if (!Dm.truthy(is_connectable(target, given_layer)) || !Dm.truthy(target.is_connectable(this, given_layer)))
            return FALSE;

        return TRUE;
    }

    /**
     * check if the piping layer and color are the same on both sides (grey can connect to all colors)
     * returns TRUE or FALSE if the connection is possible or not
     * Arguments:
     * * obj/machinery/atmospherics/target - the machinery we want to connect to
     * * given_layer - the piping_layer we are connecting to
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:337
    public virtual object is_connectable(obj_machinery_atmospherics target = null, object given_layer = null, params object[] __extra)
    {
        if (given_layer == null)
            given_layer = piping_layer;

        // you can't place the machine on the same location as the target cause it blocks
        if (target.loc == loc)
            return FALSE;

        //if the target is not in the same piping layer & it does not have the all layer connection flag[which allows it to be connected regardless of layer] then we are out
        if (Dm.truthy(Dm.bitand(target.pipe_flags, PIPING_DISTRO_AND_WASTE_LAYERS)))
        {
            if (Dm.num(given_layer) % 2 != 0) // ISODD(given_layer) (__DEFINES/maths.dm:86, hand-expanded, rules 11.1)
                return FALSE;
        }
        else if (!Dm.eq(target.piping_layer, given_layer) && !Dm.truthy(Dm.bitand(target.pipe_flags, PIPING_ALL_LAYER)))
            return FALSE;

        //if the target does not have the same color and it does not have all color connection flag[which allows it to be connected regardless of color] & one of the pipes is not gray[allowing for connection regardless] then we are out
        if (!Dm.eq(target.pipe_color, pipe_color) && !Dm.truthy(Dm.bitand(Dm.bitor(target.pipe_flags, pipe_flags), PIPING_ALL_COLORS)) && !Dm.eq(target.pipe_color, ATMOS_COLOR_OMNI) && !Dm.eq(pipe_color, ATMOS_COLOR_OMNI))
            return FALSE;

        return TRUE;
    }

    /**
     * Called on construction and when expanding the datum_pipeline, returns the nodes of the device
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:361
    public virtual object pipeline_expansion(params object[] __extra)
    {
        return nodes;
    }

    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:367
    public virtual void set_init_directions(object init_dir = null)
    {
        return;
    }

    /**
     * Getter of initial directions
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:373
    public virtual object get_init_directions()
    {
        return initialize_directions;
    }

    /**
     * Called by add_member() in datum_pipeline.dm, returns the parent network the device is connected to
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:379
    public virtual object return_pipenet(params object[] __extra)
    {
        return null;
    }

    /**
     * Called by add_machinery_member() in datum_pipeline.dm, returns a list of gas_mixtures and assigns them into other_airs (by addMachineryMember) to allow pressure redistribution for the machineries.
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:385
    public virtual object return_pipenet_airs(params object[] __extra)
    {
        return null;
    }

    /**
     * Called by build_pipeline() and add_member() in datum_pipeline.dm, set the network the device is connected to, to the datum pipeline it has reference
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:391
    public virtual void set_pipenet(params object[] __extra)
    {
        return;
    }

    /**
     * Replaces the connection to the old_pipenet with the new_pipenet
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:397
    public virtual void replace_pipenet(datum_pipeline old_pipenet = null, datum_pipeline new_pipenet = null)
    {
        return;
    }

    /**
     * Disconnects the nodes
     *
     * Called by nullify_node(), it disconnects two nodes by removing the reference id from the node itself that called this proc
     * Arguments:
     * * obj/machinery/atmospherics/reference - the machinery we are removing from the node connection
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:407
    public virtual void disconnect(obj_machinery_atmospherics reference = null)
    {
        if (istype(reference, typeof(obj_machinery_atmospherics_pipe)))
        {
            obj_machinery_atmospherics_pipe P = reference as obj_machinery_atmospherics_pipe;
            P.destroy_network();
        }
        nodes[nodes.Find(reference)] = null;
        update_appearance();
    }

    // M5 slice 7 (pipes): dropping a pipe fitting onto a device, unwrenching a device into its fitting (the pressure check and
    // the thrown user), and building one from a fitting (on_construction joins the pipenet).
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:414
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_pipe) //lets you autodrop
            return NONE;

        obj_item_pipe pipe = (obj_item_pipe)tool;
        if (!Dm.truthy(await user.dropItemToGround(pipe)))
            return ITEM_INTERACT_BLOCKING;

        pipe.set_piping_layer(piping_layer); //align it with us
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:425
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item I = null)
    {
        if (!Dm.truthy(can_unwrench__proc(user)))
            return ITEM_INTERACT_BLOCKING;

        datum_gas_mixture int_air = (await return_air()) as datum_gas_mixture;
        datum_gas_mixture env_air = (await loc.return_air()) as datum_gas_mixture;
        // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:430
        add_fingerprint(user);

        float unsafe_wrenching = FALSE;
        float internal_pressure = Dm.num(int_air.return_pressure()) - Dm.num(env_air.return_pressure());
        float empty_pipe = FALSE;
        if (this is obj_machinery_atmospherics_components)
        {
            DmList all_gas_mixes = return_analyzable_air() as DmList;
            float empty_mixes = 0;
            for (float gas_mix_number = 1, gas_mix_number__end = Dm.num(device_type); gas_mix_number <= gas_mix_number__end; gas_mix_number++)
            {
                datum_gas_mixture gas_mix = all_gas_mixes[gas_mix_number] as datum_gas_mixture;
                if (!Dm.truthy(gas_mix.total_moles()))
                    empty_mixes++;
                if (!Dm.truthy(nodes[gas_mix_number]) || (nodes[gas_mix_number] is obj_machinery_atmospherics_components_unary_portables_connector && !Dm.truthy(portable_device_connected(gas_mix_number))))
                {
                    float pressure_delta = Dm.num(((datum_gas_mixture)all_gas_mixes[gas_mix_number]).return_pressure()) - Dm.num(env_air.return_pressure());
                    internal_pressure = internal_pressure > pressure_delta ? internal_pressure : pressure_delta;
                }
            }
            if (empty_mixes == Dm.num(device_type))
                empty_pipe = TRUE;
        }
        if (!Dm.truthy(int_air.total_moles()))
            empty_pipe = TRUE;

        if (!Dm.truthy(empty_pipe))
            to_chat(user, span_notice(I18n.T("You begin to unfasten \\the []...", this)));

        if (internal_pressure > 2 * ONE_ATMOSPHERE)
        {
            to_chat(user, span_warning(I18n.T("As you begin unwrenching \\the [] a gush of air blows in your face... maybe you should reconsider?", this)));
            unsafe_wrenching = TRUE; //Oh dear oh dear
        }

        if (Dm.truthy(await I.use_tool(this, user, Dm.truthy(empty_pipe) ? 0 : 2 * SECONDS, volume: 50)))
        {
            user.visible_message(
                I18n.T("[] unfastens \\the [].", user, this),
                span_notice(I18n.T("You unfasten \\the [].", this)),
                span_hear(I18n.T("You hear ratchet.")));
            investigate_log(Dm.text("was [] by []", span_warning("REMOVED"), key_name(usr)), INVESTIGATE_ATMOS);

            //You unwrenched a pipe full of pressure? Let's splat you into the wall, silly.
            if (Dm.truthy(unsafe_wrenching))
                await unsafe_pressure_release(user, internal_pressure);
            dm_unwrencher = user; // DEVIATION(DV356)
            await deconstruct(TRUE);
            return ITEM_INTERACT_SUCCESS;
        }

        return ITEM_INTERACT_BLOCKING;
    }

    /**
     * Getter for can_unwrench
     *
     * Called by wrench_act() to check if the device can be unwrenched, each device override this with custom code (like if on/operating can't unwrench)
     * Arguments:
     * * mob/user - the mob doing the act
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:480
    public virtual object can_unwrench__proc(mob user = null)
    {
        return can_unwrench;
    }

    /**
     * Pipe pressure release calculations
     *
     * Throws the user when they unwrench a pipe with a major difference between the internal and environmental pressure.
     * Called by wrench_act() before deconstruct()
     * Arguments:
     * * mob_user - the mob doing the act
     * * pressures - it can be passed on from wrench_act(), it's the pressure difference between the environment pressure and the pipe internal pressure
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:492
    public virtual async DmTask unsafe_pressure_release(mob user = null, object pressures = null)
    {
        if (user == null)
            return;
        if (!Dm.truthy(pressures))
        {
            datum_gas_mixture int_air = (await return_air()) as datum_gas_mixture;
            datum_gas_mixture env_air = (await loc.return_air()) as datum_gas_mixture;
            pressures = Dm.num(int_air.return_pressure()) - Dm.num(env_air.return_pressure());
        }

        user.visible_message(span_danger(I18n.T("[] is sent flying by pressure!", user)), span_userdanger(I18n.T("The pressure sends you flying!")));

        // if get_dir(src, user) is not 0, target is the edge_target_turf on that dir
        // otherwise, edge_target_turf uses a random cardinal direction
        // range is pressures / 250
        // speed is pressures / 1250
        object throw_dir = get_dir(this, user);
        if (!Dm.truthy(throw_dir))
            throw_dir = pick(GLOB.cardinals);
        await user.throw_at(get_edge_target_turf(user, throw_dir) as atom, Dm.num(pressures) / 250, Dm.num(pressures) / 1250);
    }

    /**
     * Pipe deconstruction
     *
     * Called by wrench_act(), create a pipe fitting and remove the pipe
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:513
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        disassembled ??= TRUE;
        if (!Dm.truthy(can_unwrench))
            return null;

        obj_item_pipe stored = Dm.@new(construction_type, dm_out_of_wall(loc, dm_unwrencher), null, dir, this, pipe_color) as obj_item_pipe;
        stored.set_piping_layer(piping_layer);
        if (!Dm.truthy(disassembled))
            await stored.take_damage(stored.max_integrity * 0.5f, sound_effect: FALSE);
        // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:521
        transfer_fingerprints_to(stored);
        return stored;
    }

    // DEVIATION(DV356): port-only, who wrenched it off (on_deconstruction drops the fitting at their feet when it was in a wall)
    [NotHashed]
    public mob dm_unwrencher;

    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:536
    public virtual object get_pipe_image(object iconfile = null, object iconstate = null, object direction = null, string color = null, float? piping_layer = null, float? trinary = null)
    {
        color ??= ATMOS_COLOR_OMNI;
        piping_layer ??= 3;
        trinary ??= FALSE;
        image pipe_overlay = Dm.image(iconfile, icon_state: iconstate, dir: direction);
        pipe_overlay.color = color;
        PIPING_LAYER_SHIFT(pipe_overlay, piping_layer.Value);
        if (trinary == TRUE && (piping_layer == 1 || piping_layer == 5))
            PIPING_FORWARD_SHIFT(pipe_overlay, piping_layer.Value, 2);
        return pipe_overlay;
    }

    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:544
    [DmParams("user", "obj_color", "set_layer")]
    public override async DmTask on_construction(mob user = null, params object[] __extra)
    {
        object obj_color = Dm.arg(__extra, 0);
        object set_layer = Dm.arg(__extra, 1);
        set_layer ??= PIPING_LAYER_DEFAULT;
        if (Dm.truthy(can_unwrench))
        {
            await add_atom_colour(obj_color, FIXED_COLOUR_PRIORITY);
            set_pipe_color(obj_color);
        }
        set_piping_layer(set_layer);
        atmos_init();
        DmList nodes = pipeline_expansion() as DmList;
        foreach (obj_machinery_atmospherics A in Dm.each<obj_machinery_atmospherics>(nodes))
        {
            A.atmos_init();
            A.add_member(this);
        }
        SSair.add_to_rebuild_queue(this);
    }

    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:556
    public override object update_name(float? updates = null)
    {
        updates ??= ALL;
        if (!Dm.truthy(override_naming) && !HAS_TRAIT(this, TRAIT_WAS_RENAMED))
            name = Dm.text("[] []", GLOB.pipe_color_name[pipe_color], Dm.initial(this, (atom p) => p.name));
        return base.update_name(updates);
    }

    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:566
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (isliving(arrived))
        {
            mob_living L = arrived as mob_living;
            L.ventcrawl_layer = Dm.num(piping_layer);
        }
        return await base.Entered(arrived, old_loc, old_locs);
    }

    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:577  #define VENT_SOUND_DELAY 30 (#undef at :728)
    private const int VENT_SOUND_DELAY = 30;

    // Handles mob movement inside a pipenet
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:580  (gaps X5: ventcrawling)
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (!Dm.truthy(direction)) //can't go this way.
            return null;
        if (Dm.@in(user, buckled_mobs))// fixes buckle ventcrawl edgecase fuck bug
            return null;

        // We want to support holding two directions at once, so we do this
        obj_machinery_atmospherics target_move = null;
        foreach (object canon_direction in Dm.each(GLOB.cardinals_multiz))
        {
            if (!Dm.truthy(Dm.bitand(direction, canon_direction)))
                continue;
            obj_machinery_atmospherics temp_target = find_connecting(canon_direction, user.ventcrawl_layer) as obj_machinery_atmospherics;
            if (temp_target == null)
                continue;
            target_move = temp_target;
            // If you're at a fork with two directions held, we will always prefer the direction you didn't last use
            // This way if you find a direction you've not used before, you take it, and if you don't, you take the other
            if (Dm.eq(user.last_vent_dir, canon_direction))
                continue;
            user.last_vent_dir = Dm.num(canon_direction);
            break;
        }

        if (target_move == null)
        {
            // If we couldn't find a target to move to and we're ventcrawling, try to exit if this vent allows it
            if (HAS_TRAIT(user, TRAIT_MOVE_VENTCRAWLING) && Dm.truthy(Dm.bitand(vent_movement, VENTCRAWL_ENTRANCE_ALLOWED)))
                await user.handle_ventcrawl(this as obj_machinery_atmospherics_components);
            return null;
        }

        if (!Dm.truthy(Dm.bitand(target_move.vent_movement, VENTCRAWL_ALLOWED)))
            return null;
        await user.forceMove(target_move);
        DmList pipenetdiff = Dm.bitxor(return_pipenets(), target_move.return_pipenets()) as DmList;
        if (Dm.truthy(pipenetdiff.len))
            user.update_pipe_vision(full_refresh: TRUE);
        if (world.time - user.last_played_vent > VENT_SOUND_DELAY)
        {
            user.last_played_vent = world.time;
            playsound(this, Dm.resource("sound/machines/ventcrawl.ogg"), 50, TRUE, -3);
        }

        //Would be great if this could be implemented when someone alt-clicks the image.
        if (Dm.truthy(Dm.bitand(target_move.vent_movement, VENTCRAWL_ENTRANCE_ALLOWED)))
        {
            await user.handle_ventcrawl(target_move as obj_machinery_atmospherics_components);
            return null;
        }

        client our_client = user.client;
        if (our_client == null)
            return null;
        await our_client.set_eye(target_move);
        // Let's smooth out that movement with an animate yeah?
        // If the new x is greater (move is left to right) we get a negative offset. vis versa
        our_client.pixel_x = (x - target_move.x) * ICON_SIZE_X;
        our_client.pixel_y = (y - target_move.y) * ICON_SIZE_Y;
        animate(our_client, Dm.named("pixel_x", 0), Dm.named("pixel_y", 0), Dm.named("time", 0.05f * SECONDS));
        our_client.move_delay = world.time + 0.05f * SECONDS;
        return null;
    }

    /**
     * Getter of a list of pipenets
     *
     * called in relaymove() to create the image for vent crawling
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:640
    public virtual object return_pipenets()
    {
        return Dm.list();
    }

    /**
     * Used for certain children of obj/machinery/atmospherics to not show pipe vision when mob is inside it.
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:646
    public virtual object can_see_pipes()
    {
        return TRUE;
    }

    /**
     * Update the layer in which the pipe/device is in, that way pipes have consistent layer depending on piping_layer
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:652
    public virtual void update_layer()
    {
        return;
    }

    /**
     * Handles cap overlay addition and removal, won't do anything if `has_cap_visuals` is set to `FALSE`
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:658
    public virtual async DmTask update_cap_visuals()
    {
        if (!Dm.truthy(has_cap_visuals))
            return;

        if (cap_overlay != null)
            await cap_overlay.moveToNullspace();

        if (!HAS_TRAIT(this, TRAIT_UNDERFLOOR))
            return;

        float connections = NONE;
        foreach (obj_machinery_atmospherics node in Dm.each<obj_machinery_atmospherics>(nodes))
        {
            if (HAS_TRAIT(node, TRAIT_UNDERFLOOR))
                continue;

            turf node_turf = get_turf(node);
            if (node_turf.underfloor_accessibility > UNDERFLOOR_HIDDEN)
                continue;

            float connected_dir = get_dir(this, node);
            connections = Dm.bitor(connections, connected_dir);
        }

        if (connections == NONE)
            return;

        float bitfield = Dm.bitor(CARDINAL_TO_PIPECAPS(connections), Dm.bitand(Dm.bitnot(connections), ALL_CARDINALS));
        turf our_turf = get_turf(this);

        if (cap_overlay == null)
            cap_overlay = (obj_effect_overlay_cap_visual)Dm.@new(typeof(obj_effect_overlay_cap_visual));

        SET_PLANE_EXPLICIT(cap_overlay, Dm.num(Dm.initial(this, (atom p) => p.plane)), our_turf);

        obj_machinery_atmospherics node1 = nodes[1] as obj_machinery_atmospherics;
        object node1_color = node1?.pipe_color;
        cap_overlay.color = Dm.eq(pipe_color, ATMOS_COLOR_OMNI) && Dm.truthy(node1_color) ? node1_color : pipe_color;
        cap_overlay.layer = Dm.num(Dm.initial(this, (atom p) => p.layer));
        cap_overlay.icon_state = Dm.text("[]_[]", bitfield, piping_layer);

        await cap_overlay.forceMove(our_turf);
    }
    /**
     * Called by the RPD.dm pre_attack()
     * Arguments:
     * * paint_color - color that the pipe will be painted in (colors in hex like #4f4f4f)
     */
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:707
    public virtual async DmTask<object> paint(object paint_color = null)
    {
        if (Dm.truthy(paintable))
        {
            await add_atom_colour(paint_color, FIXED_COLOUR_PRIORITY);
            set_pipe_color(paint_color);
            await update_node_icon();
        }
        return paintable;
    }

    /// Setter for pipe color, so we can ensure it's all uniform and save cpu time
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:715
    public virtual void set_pipe_color(object pipe_colour = null)
    {
        this.pipe_color = uppertext(pipe_colour) as string;
        update_name();
    }

    /// Return TRUE if there is device connected to portables_connector
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:720
    public virtual object portable_device_connected(object node = null)
    {
        obj_machinery_atmospherics_components_unary_portables_connector portable_devices_connector = nodes[node] as obj_machinery_atmospherics_components_unary_portables_connector;
        if (portable_devices_connector.connected_device != null)
            return TRUE;
        return FALSE;
    }
}

/**
 * Run when you update the conditions in which an /atom might want to start reacting to its turf's air
 */
// tg: code/modules/atmospherics/machinery/atmosmachinery.dm:153
public partial class atom
{
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:153
    public virtual void atmos_conditions_changed()
    {
        return;
    }
}

public partial class atom_movable
{
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:156
    public override void atmos_conditions_changed()
    {
        turf_open open_loc = loc as turf_open;
        if (!isopenturf(open_loc))
            return;
        datum_gas_mixture turf_gas = open_loc.air;
        if (turf_gas == null)
            return;
        check_atmos_process(open_loc, turf_gas, turf_gas.temperature);
    }
}

public partial class turf_open
{
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:165
    public override void atmos_conditions_changed()
    {
        if (air == null)
            return;
        check_atmos_process(this, air, air.temperature);
    }
}

// tg: code/modules/atmospherics/machinery/atmosmachinery.dm:572 (interleaved types, rules 1.9; M5x slice 5)
public partial class obj_machinery_atmospherics : obj_machinery
{
    // tg: code/modules/atmospherics/machinery/atmosmachinery.dm:572
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        if (Dm.num(current_size) >= STAGE_FIVE)
            await deconstruct(FALSE);
        return await base.singularity_pull(singularity, current_size);
    }
}
