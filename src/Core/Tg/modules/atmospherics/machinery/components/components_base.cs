// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/components_base.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b slice 2: the devices and their pipenets): the init with the gas mixtures, the pictures (update_icon_nopipes,
// update_overlays, on_hide, hide_pipe, update_icon's pipe underlays, get_pipe_image, update_layer, get_pipe_layer_offset),
// the pipenet housekeeping (nullify_node, rebuild_pipes, get_rebuild_target, nullify_pipenet, return_pipenet_airs,
// pipeline_expansion, set_pipenet, return_pipenet, replace_pipenet, update_parents, return_pipenets and the reconcilation
// hooks), return_analyzable_air, the node (dis)connection helpers and relocate_airs. M5 slice 7: on_construction,
// on_deconstruction, crowbar_deconstruction_act and default_change_direction_wrench. The air alarm window (2026-10-16): the
// UI access check (ui_status). Generated live-partial stub: the map export (M10). The var block is generated.

// So much of atmospherics.dm was used solely by components, so separating this makes things all a lot cleaner.
// On top of that, now people can add component-speciic procs/vars if they want!

// tg: code/modules/atmospherics/machinery/components/components_base.dm:4
public partial class obj_machinery_atmospherics_components : obj_machinery_atmospherics
{
    // tg: code/modules/atmospherics/machinery/components/components_base.dm:34
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        parents = Dm.list_n(Dm.num(device_type));
        airs = Dm.list_n(Dm.num(device_type));

        object dot = base.Initialize(mapload, __extra);

        for (float i = 1, i__end = Dm.num(device_type); i <= i__end; i++)
        {
            if (Dm.truthy(airs[i]))
                continue;
            datum_gas_mixture component_mixture = Dm.@new<datum_gas_mixture>();
            component_mixture.volume = 200;
            airs[i] = component_mixture;
        }

        update_appearance();
        return dot;
    }

    // Iconnery

    /**
     * Called by update_icon(), used individually by each component to determine the icon state without the pipe in consideration
     */
    // tg: code/modules/atmospherics/machinery/components/components_base.dm:54
    public virtual void update_icon_nopipes(params object[] __extra)
    {
        return;
    }

    // tg: code/modules/atmospherics/machinery/components/components_base.dm:57
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);

        if (istype(this, typeof(obj_machinery_atmospherics_components_trinary)))
        {
            bool on_state = Dm.truthy(on) && Dm.truthy(nodes[1]) && Dm.truthy(nodes[2]) && Dm.truthy(nodes[3]) && Dm.truthy(is_operational);
            // during Initialize() trinary devices will be technically "on" but their icon_state
            // updates to off via update_icon_nopipes() before atmos nodes process which breaks their emissives
            if (!on_state)
                return dot;
        }

        cut_overlays();
        if (Dm.truthy(is_operational) && ((Dm.truthy(on) && Dm.truthy(light_mask_on)) || (!Dm.truthy(on) && Dm.truthy(light_mask_off))))
        {
            // DEVIATION(DV12): emissive_appearance (__HELPERS/lighting.dm:2), the device's glow on the emissive plane (both the
            // returned overlay and the add_overlay of :70-71), is not created (lighting planes and emissives are not ported).
            Dm.Deferred("code/modules/atmospherics/machinery/components/components_base.dm:70", "update_overlays: emissive_appearance(icon, \"[icon_state]-emissive\", src), the device's emissive glow (DEVIATION DV12)");
        }
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/components_base.dm:74
    public override void on_hide(datum source = null, params object[] __extra)
    {
        object underfloor_accessibility = Dm.arg(__extra, 0);
        hide_pipe(underfloor_accessibility);
        base.on_hide(source, __extra);
    }

    /**
     * Called in on_hide(), set the underfloor_state var to true or false depending on the situation, calls update_icon()
     */
    // tg: code/modules/atmospherics/machinery/components/components_base.dm:81
    public virtual void hide_pipe(object underfloor_accessibility = null)
    {
        underfloor_state = Dm.num(underfloor_accessibility);
        if (Dm.truthy(underfloor_state))
            REMOVE_TRAIT(this, TRAIT_UNDERFLOOR, REF(this));
        else
            ADD_TRAIT(this, TRAIT_UNDERFLOOR, REF(this));
        update_appearance(UPDATE_ICON);
    }

    // tg: code/modules/atmospherics/machinery/components/components_base.dm:89
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        update_icon_nopipes();

        underlays.Cut();

        color = null;
        bool uncovered_turf = loc != null && HAS_TRAIT(loc, TRAIT_UNCOVERED_TURF);
        SET_PLANE_IMPLICIT(this, (underfloor_state == UNDERFLOOR_INTERACTABLE && !uncovered_turf) ? GAME_PLANE : FLOOR_PLANE);

        // Layer is handled in update_layer()
        if (!Dm.truthy(underfloor_state))
            return await base.update_icon(updates, __extra);

        if (Dm.truthy(Dm.bitand(pipe_flags, PIPING_DISTRO_AND_WASTE_LAYERS)))
            return await base.update_icon(updates, __extra);

        // DEVIATION(DV31): an inert device (D112: its init never ran, so it has no nodes; the atmos tanks still smooth their
        // icons) gets no pipe underlays.
        if (InertSeam.IsInert(this))
            return await base.update_icon(updates, __extra);

        float connected = 0; //Direction bitset
        object underlay_pipe_layer = Dm.truthy(shift_underlay_only) ? piping_layer : 3;

        for (float i = 1, i__end = Dm.num(device_type); i <= i__end; i++) //adds intact pieces
        {
            if (!Dm.truthy(nodes[i]))
                continue;
            obj_machinery_atmospherics node = nodes[i] as obj_machinery_atmospherics;
            float node_dir = get_dir(this, node);
            mutable_appearance pipe_appearance = mutable_appearance(Dm.resource("icons/obj/pipes_n_cables/pipe_underlays.dmi"), Dm.text("intact_[]_[]", node_dir, underlay_pipe_layer), appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)) as mutable_appearance;
            pipe_appearance.color = SELECT_ATMOS_NODE_COLOR(this, node);
            if (underfloor_state == UNDERFLOOR_VISIBLE || uncovered_turf)
            {
                pipe_appearance.layer = BELOW_CATWALK_LAYER + Dm.num(get_pipe_layer_offset());
                SET_PLANE_EXPLICIT(pipe_appearance, FLOOR_PLANE, this);
            }
            Dm.append(underlays, pipe_appearance);
            connected = Dm.bitor(connected, node_dir);
        }

        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            if (Dm.truthy(Dm.bitand(initialize_directions, direction)) && !Dm.truthy(Dm.bitand(connected, direction)))
            {
                mutable_appearance pipe_appearance = mutable_appearance(Dm.resource("icons/obj/pipes_n_cables/pipe_underlays.dmi"), Dm.text("exposed_[]_[]", direction, underlay_pipe_layer), appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)) as mutable_appearance;
                pipe_appearance.color = pipe_color;
                if (underfloor_state == UNDERFLOOR_VISIBLE || uncovered_turf)
                {
                    pipe_appearance.layer = BELOW_CATWALK_LAYER + Dm.num(get_pipe_layer_offset());
                    SET_PLANE_EXPLICIT(pipe_appearance, FLOOR_PLANE, this);
                }
                Dm.append(underlays, pipe_appearance);
            }
        }

        if (!Dm.truthy(shift_underlay_only))
            PIPING_LAYER_SHIFT(this, Dm.num(piping_layer));
        return await base.update_icon(updates, __extra);
    }

    // tg: code/modules/atmospherics/machinery/components/components_base.dm:134
    public override object get_pipe_image(object iconfile = null, object iconstate = null, object direction = null, string color = null, float? piping_layer = null, float? trinary = null)
    {
        image pipe_appearance = base.get_pipe_image(iconfile, iconstate, direction, color, piping_layer, trinary) as image;
        if (underfloor_state == UNDERFLOOR_VISIBLE || (loc != null && HAS_TRAIT(loc, TRAIT_UNCOVERED_TURF)))
        {
            pipe_appearance.layer = BELOW_CATWALK_LAYER + Dm.num(get_pipe_layer_offset());
            SET_PLANE_EXPLICIT(pipe_appearance, FLOOR_PLANE, this);
        }
        return pipe_appearance;
    }

    // Pipenet stuff; housekeeping

    // tg: code/modules/atmospherics/machinery/components/components_base.dm:143
    public override void nullify_node(object i = null)
    {
        if (parents != null && Dm.truthy(parents[i]))
            nullify_pipenet(parents[i] as datum_pipeline);
        if (airs != null)
            airs[i] = null;
        base.nullify_node(i);
    }

    // tg: code/modules/atmospherics/machinery/components/components_base.dm:149  (M5 slice 7)
    public override async DmTask on_construction(mob user = null, params object[] __extra)
    {
        await base.on_construction(user, __extra);
        update_parents();
    }

    // tg: code/modules/atmospherics/machinery/components/components_base.dm:153  (M5 slice 7)
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        relocate_airs();
        return await base.on_deconstruction(disassembled);
    }

    // tg: code/modules/atmospherics/machinery/components/components_base.dm:157
    public override void rebuild_pipes()
    {
        base.rebuild_pipes();
        if (Dm.truthy(update_parents_after_rebuild))
            update_parents();
    }

    // tg: code/modules/atmospherics/machinery/components/components_base.dm:162
    public override object get_rebuild_target()
    {
        for (float port = 1, port__end = Dm.num(device_type); port <= port__end; port++)
        {
            if (Dm.truthy(parents[port]))
                continue;
            parents[port] = Dm.@new<datum_pipeline>();
            return parents[port];
        }
        return null;
    }

    /**
     * Called by nullify_node(), used to remove the pipeline the component is attached to
     * Arguments:
     * * -reference: the pipeline the component is attached to
     */
    // tg: code/modules/atmospherics/machinery/components/components_base.dm:174
    public virtual void nullify_pipenet(datum_pipeline reference = null)
    {
        if (reference == null)
            throw Dm.CRASH(Dm.text("nullify_pipenet(null) called by [] on []", type, this), "code/modules/atmospherics/machinery/components/components_base.dm", 176); // COORD(src) (the admin coordinates, M10) is the atom itself here

        for (float i = 1; i <= parents.len; i++)
        {
            if (parents[i] == reference)
            {
                Dm.remove(ref reference.other_airs, airs[i]); // Disconnects from the pipeline side
                parents[i] = null; // Disconnects from the machinery side.
            }
        }

        Dm.remove(ref reference.other_atmos_machines, this);
        if (Dm.truthy(custom_reconcilation))
            Dm.remove(ref reference.require_custom_reconcilation, this);

        /**
         *  We explicitly qdel pipeline when this particular pipeline
         *  is projected to have no member and cause GC problems.
         *  We have to do this because components don't qdel pipelines
         *  while pipes must and will happily wreck and rebuild everything
         * again every time they are qdeleted.
         */

        if (!Dm.truthy(Dm.length(reference.other_atmos_machines)) && !Dm.truthy(Dm.length(reference.members)))
        {
            if (QDESTROYING(reference))
                throw Dm.CRASH(Dm.text("nullify_pipenet() called on qdeleting []", reference), "code/modules/atmospherics/machinery/components/components_base.dm", 196);
            qdel(reference);
        }
    }

    // tg: code/modules/atmospherics/machinery/components/components_base.dm:200
    public override object return_pipenet_airs(params object[] __extra)
    {
        datum_pipeline reference = Dm.arg(__extra, 0) as datum_pipeline;
        DmList returned_air = Dm.list();

        for (float i = 1; i <= parents.len; i++)
        {
            if (parents[i] == reference)
                Dm.append(ref returned_air, airs[i]);
        }
        return returned_air;
    }

    // tg: code/modules/atmospherics/machinery/components/components_base.dm:208
    public override object pipeline_expansion(params object[] __extra)
    {
        datum_pipeline reference = Dm.arg(__extra, 0) as datum_pipeline;
        if (reference == null)
            return base.pipeline_expansion(__extra);
        float port = parents.Find(reference);
        if (Dm.truthy(port))
            return Dm.list(nodes[port]);
        // no port means another pipeline took our port while this one waited to expand somehow
        Dm.remove(ref reference.other_atmos_machines, this);
        Dm.remove(ref reference.require_custom_reconcilation, this);
        Dm.remove(ref reference.other_airs, airs);
        return Dm.list();
    }

    // tg: code/modules/atmospherics/machinery/components/components_base.dm:220
    public override void set_pipenet(params object[] __extra)
    {
        datum_pipeline reference = Dm.arg(__extra, 0) as datum_pipeline;
        obj_machinery_atmospherics target_component = Dm.arg(__extra, 1) as obj_machinery_atmospherics;
        parents[nodes.Find(target_component)] = reference;
    }

    // tg: code/modules/atmospherics/machinery/components/components_base.dm:223
    public override object return_pipenet(params object[] __extra)
    {
        obj_machinery_atmospherics target_component = __extra.Length > 0 ? Dm.arg(__extra, 0) as obj_machinery_atmospherics : nodes[1] as obj_machinery_atmospherics; //returns parents[1] if called without argument
        return parents[nodes.Find(target_component)];
    }

    // tg: code/modules/atmospherics/machinery/components/components_base.dm:226
    public override void replace_pipenet(datum_pipeline old_pipenet = null, datum_pipeline new_pipenet = null)
    {
        parents[parents.Find(old_pipenet)] = new_pipenet;
    }

    // Helpers

    /**
     * Called in most atmos processes and gas handling situations, update the parents pipelines of the devices connected to the source component
     * This way gases won't get stuck
     */
    // tg: code/modules/atmospherics/machinery/components/components_base.dm:235
    public virtual void update_parents()
    {
        if (!Dm.truthy(SSair.initialized))
            return;
        if (Dm.truthy(rebuilding))
        {
            update_parents_after_rebuild = TRUE;
            return;
        }
        for (float i = 1, i__end = Dm.num(device_type); i <= i__end; i++)
        {
            datum_pipeline parent = parents[i] as datum_pipeline;
            if (parent == null)
            {
                // WARNING(MSG) (__HELPERS/logging/_logging.dm:2), expanded inline (rules 11.1 (d), (f): src, usr, __FILE__, __LINE__)
                warning(Dm.text("[] in [] at line [] src: [] usr: [].", "Component is missing a pipenet! Rebuilding...", "code/modules/atmospherics/machinery/components/components_base.dm", 244, this, usr));
                SSair.add_to_rebuild_queue(this);
            }
            else
                parent.update = TRUE;
        }
    }

    // tg: code/modules/atmospherics/machinery/components/components_base.dm:249
    public override object return_pipenets()
    {
        DmList dot = Dm.list();
        for (float i = 1, i__end = Dm.num(device_type); i <= i__end; i++)
            Dm.append(ref dot, return_pipenet(nodes[i]));
        return dot;
    }

    /// When this machine is in a pipenet that is reconciling airs, this proc can add pipelines to the calculation.
    /// Can be either a list of pipenets or a single pipenet.
    // tg: code/modules/atmospherics/machinery/components/components_base.dm:256
    public virtual async DmTask<object> return_pipenets_for_reconcilation(datum_pipeline requester = null)
    {
        return Dm.list();
    }

    /// When this machine is in a pipenet that is reconciling airs, this proc can add airs to the calculation.
    /// Can be either a list of airs or a single air mix.
    // tg: code/modules/atmospherics/machinery/components/components_base.dm:261
    public virtual object return_airs_for_reconcilation(datum_pipeline requester = null)
    {
        return Dm.list();
    }

    // tg: code/modules/atmospherics/machinery/components/components_base.dm:266
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (Dm.truthy(await allowed__proc(user)))
            return await base.ui_status(user, state);
        to_chat(user, span_danger(I18n.T("Access denied.")));
        return UI_CLOSE;
    }

    // Tool acts

    // tg: code/modules/atmospherics/machinery/components/components_base.dm:274
    public override object return_analyzable_air()
    {
        return airs;
    }

    /**
     * Handles machinery deconstruction and unsafe pressure release
     */
    // tg: code/modules/atmospherics/machinery/components/components_base.dm:280  (M5 slice 7)
    public virtual async DmTask<object> crowbar_deconstruction_act(mob_living user = null, obj_item tool = null, float? internal_pressure = null)
    {
        internal_pressure ??= 0;
        if (!Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("open panel!"));
            return ITEM_INTERACT_SUCCESS;
        }

        float unsafe_wrenching = FALSE;
        float filled_pipe = FALSE;
        datum_gas_mixture environment_air = (await loc.return_air()) as datum_gas_mixture;

        for (float i = 1, i__end = Dm.num(device_type); i <= i__end; i++)
        {
            datum_gas_mixture inside_air = airs[i] as datum_gas_mixture;
            if (Dm.num(inside_air.total_moles()) > 0 || Dm.truthy(internal_pressure))
                filled_pipe = TRUE;
            if (!Dm.truthy(nodes[i]) || (nodes[i] is obj_machinery_atmospherics_components_unary_portables_connector && !Dm.truthy(portable_device_connected(i))))
            {
                float air_pressure = Dm.num(((datum_gas_mixture)airs[i]).return_pressure());
                internal_pressure = internal_pressure > air_pressure ? internal_pressure : air_pressure;
            }
        }

        if (!Dm.truthy(filled_pipe))
            return await default_deconstruction_crowbar(user, tool);

        to_chat(user, span_notice(I18n.T("You begin to unfasten \\the []...", this)));

        internal_pressure -= Dm.num(environment_air.return_pressure());

        if (internal_pressure > 2 * ONE_ATMOSPHERE)
        {
            to_chat(user, span_warning(I18n.T("As you begin deconstructing \\the [] a gush of air blows in your face... maybe you should reconsider?", this)));
            unsafe_wrenching = TRUE;
        }

        if (!Dm.truthy(await do_after(user, 2 * SECONDS, this)))
            return null;
        if (Dm.truthy(unsafe_wrenching))
            await unsafe_pressure_release(user, internal_pressure);
        tool.play_tool_sound(this, 50);
        await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/components_base.dm:315  (M5 slice 7)
    public override async DmTask<object> default_change_direction_wrench(mob user = null, obj_item I = null)
    {
        object dot = await base.default_change_direction_wrench(user, I);
        if (!Dm.truthy(dot))
            return FALSE;
        set_init_directions();
        reconnect_nodes();
        return TRUE;
    }

    // tg: code/modules/atmospherics/machinery/components/components_base.dm:323
    public virtual void reconnect_nodes()
    {
        for (float i = 1, i__end = Dm.num(device_type); i <= i__end; i++)
        {
            obj_machinery_atmospherics node = nodes[i] as obj_machinery_atmospherics;
            if (node != null)
            {
                if (Dm.@in(this, node.nodes))
                    node.disconnect(this);
                nodes[i] = null;
            }
            if (Dm.truthy(parents[i]))
                nullify_pipenet(parents[i] as datum_pipeline);
        }
        for (float i = 1, i__end = Dm.num(device_type); i <= i__end; i++)
        {
            obj_machinery_atmospherics node = nodes[i] as obj_machinery_atmospherics;
            atmos_init();
            node = nodes[i] as obj_machinery_atmospherics;
            if (node != null)
            {
                node.atmos_init();
                node.add_member(this);
                update_parents();
            }
            SSair.add_to_rebuild_queue(this);
        }
    }

    /**
     * Disconnects all nodes from ourselves, remove us from the node's nodes.
     * Nullify our parent pipenet
     */
    // tg: code/modules/atmospherics/machinery/components/components_base.dm:346
    public virtual void disconnect_nodes()
    {
        for (float i = 1, i__end = Dm.num(device_type); i <= i__end; i++)
        {
            obj_machinery_atmospherics node = nodes[i] as obj_machinery_atmospherics;
            if (node != null)
            {
                if (Dm.@in(this, node.nodes)) //Only if it's actually connected. On-pipe version would is one-sided.
                    node.disconnect(this);
                nodes[i] = null;
            }
            if (Dm.truthy(parents[i]))
                nullify_pipenet(parents[i] as datum_pipeline);
        }
    }

    /**
     * Connects all nodes to ourselves, add us to the node's nodes.
     * Calls atmos_init() on the node and on us.
     */
    // tg: code/modules/atmospherics/machinery/components/components_base.dm:360
    public virtual void connect_nodes()
    {
        atmos_init();
        for (float i = 1, i__end = Dm.num(device_type); i <= i__end; i++)
        {
            obj_machinery_atmospherics node = nodes[i] as obj_machinery_atmospherics;
            if (node != null)
            {
                node.atmos_init();
                node.add_member(this);
            }
        }
        SSair.add_to_rebuild_queue(this);
    }

    /**
     * Easy way to toggle nodes connection and disconnection.
     *
     * Arguments:
     * * disconnect - if TRUE, disconnects all nodes. If FALSE, connects all nodes.
     */
    // tg: code/modules/atmospherics/machinery/components/components_base.dm:375
    public virtual void change_nodes_connection(object disconnect = null)
    {
        if (Dm.truthy(disconnect))
        {
            disconnect_nodes();
            return;
        }
        connect_nodes();
    }

    // tg: code/modules/atmospherics/machinery/components/components_base.dm:381
    public override void update_layer()
    {
        if (!Dm.truthy(underfloor_state))
            layer = BELOW_CATWALK_LAYER;
        else if (Dm.eq(PLANE_TO_TRUE(plane), FLOOR_PLANE))
            layer = ABOVE_OPEN_TURF_LAYER;
        else
            layer = Dm.num(Dm.initial(this, (atom p) => p.layer));
        layer += Dm.num(get_pipe_layer_offset());
    }

    // tg: code/modules/atmospherics/machinery/components/components_base.dm:390
    public virtual object get_pipe_layer_offset()
    {
        return (Dm.num(piping_layer) - PIPING_LAYER_DEFAULT) * PIPING_LAYER_LCHANGE + (Dm.num(GLOB.pipe_colors_ordered[pipe_color]) * 0.001f);
    }

    /**
     * Handles air relocation to the pipenet/environment
     */
    // tg: code/modules/atmospherics/machinery/components/components_base.dm:396
    public virtual void relocate_airs(datum_gas_mixture to_release = null)
    {
        for (float i = 1, i__end = Dm.num(device_type); i <= i__end; i++)
        {
            datum_gas_mixture air = airs[i] as datum_gas_mixture;
            obj_machinery_atmospherics node = nodes[i] as obj_machinery_atmospherics;
            // A surviving node may be waiting for its pipeline to rebuild after nearby pipe destruction.
            datum_pipeline parent = node != null ? parents[i] as datum_pipeline : null;
            if (parent == null || (istype(node, typeof(obj_machinery_atmospherics_components_unary_portables_connector)) && !Dm.truthy(portable_device_connected(i))))
            {
                if (to_release == null)
                {
                    to_release = air;
                    continue;
                }
                to_release.merge(air);
                continue;
            }
            datum_gas_mixture parents_air = parent.air;
            parents_air.merge(air);
        }
        if (to_release != null)
        {
            turf local_turf = get_turf(this);
            Waiting.NoSleep(local_turf.assume_air(to_release));
        }
    }
}
