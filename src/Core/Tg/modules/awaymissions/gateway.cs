// Translated from tgstation (snapshot 1d99bd7f): code/modules/awaymissions/gateway.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The station's gateway, its destinations and its control console. The away missions behind it are not ported (PORT-CHECKLIST 12), so
// the console lists only what a mapped gateway or landmark adds. The emissive copy of the portal's light overlays is the glow of
// lighting planes (DEVIATION DV12): the plain overlay is drawn.

public partial class datum_controller_global_vars
{
    /// Station home gateway
    // tg: code/modules/awaymissions/gateway.dm:2  GLOBAL_DATUM(the_gateway, /obj/machinery/gateway/centerstation)
    public obj_machinery_gateway_centerstation the_gateway;

    // tg: code/modules/awaymissions/gateway.dm:2  (GLOBAL_UNMANAGED(the_gateway), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalthe_gateway()
    {
        return;
    }

    /// List of possible gateway destinations.
    // tg: code/modules/awaymissions/gateway.dm:4  GLOBAL_LIST_EMPTY(gateway_destinations)
    public DmList gateway_destinations;

    // tg: code/modules/awaymissions/gateway.dm:4  (GLOBAL_MANAGED(gateway_destinations, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalgateway_destinations()
    {
        gateway_destinations = Dm.list();
        Dm.append(ref gvars_datum_init_order, "gateway_destinations");
    }
}

/**
 * Corresponds to single entry in gateway control.
 *
 * Will NOT be added automatically to GLOB.gateway_destinations list.
 */
// tg: code/modules/awaymissions/gateway.dm:11
public partial class datum_gateway_destination : datum
{
    /* Can a gateway link to this destination right now. */
    // tg: code/modules/awaymissions/gateway.dm:18
    public virtual object is_available()
    {
        return Dm.truthy(enabled) && (world.time - Dm.num(SSticker.round_start_time) >= Dm.num(wait)) ? TRUE : FALSE;
    }

    /* Returns user-friendly description why you can't connect to this destination, displayed in UI */
    // tg: code/modules/awaymissions/gateway.dm:22
    public virtual object get_available_reason()
    {
        object dot = "Unreachable";
        if (world.time - Dm.num(SSticker.round_start_time) < Dm.num(wait))
        {
            // tg plays the sound "at" the destination datum, which is no atom: playsound finds no turf and plays nothing
            playsound(null, Dm.resource("sound/machines/gateway/gateway_calibrating.ogg"), 80, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
            dot = "Connection desynchronized. Recalibration in progress.";
        }
        return dot;
    }

    /* Check if the movable is allowed to arrive at this destination (exile implants mostly) */
    // tg: code/modules/awaymissions/gateway.dm:29
    public virtual object incoming_pass_check(atom_movable AM = null)
    {
        return TRUE;
    }

    /* Get the actual turf we'll arrive at */
    // tg: code/modules/awaymissions/gateway.dm:33
    public virtual object get_target_turf()
    {
        throw Dm.CRASH("get target turf not implemented for this destination type", "code/modules/awaymissions/gateway.dm", 34);
    }

    /* Called after moving the movable to target turf */
    // tg: code/modules/awaymissions/gateway.dm:37
    public virtual async DmTask<object> post_transfer(atom_movable AM = null)
    {
        if (ismob(AM))
        {
            mob M = AM as mob;
            if (M.client != null)
                M.client.move_delay = Dm.max(world.time + 5, M.client.move_delay);
        }
        await DmTask.CompletedTask;
        return null;
    }

    /* Called when gateway activates with this destination. */
    // tg: code/modules/awaymissions/gateway.dm:44  (tg types the argument as a gateway; the gateway passes its own destination)
    public virtual void activate(datum_gateway_destination activated = null)
    {
        return;
    }

    /* Called when gateway targeting this destination deactivates. */
    // tg: code/modules/awaymissions/gateway.dm:48
    public virtual void deactivate(obj_machinery_gateway deactivated = null)
    {
        return;
    }

    /* Returns data used by gateway controller ui */
    // tg: code/modules/awaymissions/gateway.dm:52
    public virtual DmList get_ui_data()
    {
        DmList dot = Dm.list();
        dot["ref"] = REF(this);
        dot["name"] = name;
        dot["available"] = is_available();
        dot["reason"] = get_available_reason();
        if (Dm.truthy(wait))
            dot["timeout"] = Dm.max(1 - (Dm.num(wait) - (world.time - Dm.num(SSticker.round_start_time))) / Dm.num(wait), 0);
        return dot;
    }
}

/* Destination is another gateway */
// tg: code/modules/awaymissions/gateway.dm:63
public partial class datum_gateway_destination_gateway : datum_gateway_destination
{
    /* We set the target gateway target to activator gateway */
    // tg: code/modules/awaymissions/gateway.dm:67
    public override void activate(datum_gateway_destination activated = null)
    {
        if (target_gateway.target == null)
            target_gateway.activate(activated);
    }

    /* We turn off the target gateway if it's linked with us */
    // tg: code/modules/awaymissions/gateway.dm:72
    public override void deactivate(obj_machinery_gateway deactivated = null)
    {
        if (target_gateway.target == deactivated.destination)
            target_gateway.deactivate();
    }

    // tg: code/modules/awaymissions/gateway.dm:76
    public override object is_available()
    {
        return Dm.truthy(base.is_available()) && Dm.truthy(target_gateway.calibrated) && target_gateway.target == null && Dm.truthy(target_gateway.powered__proc()) ? TRUE : FALSE;
    }

    // tg: code/modules/awaymissions/gateway.dm:79
    public override object get_available_reason()
    {
        object dot = base.get_available_reason();
        if (!Dm.truthy(target_gateway.calibrated))
            dot = "Exit gateway malfunction. Manual recalibration required.";
        if (target_gateway.target != null)
            dot = "Exit gateway in use.";
        if (!Dm.truthy(target_gateway.powered__proc()))
            dot = "Exit gateway unpowered.";
        return dot;
    }

    // tg: code/modules/awaymissions/gateway.dm:88
    public override object get_target_turf()
    {
        return get_step(target_gateway.portal, target_gateway.dir);
    }

    // tg: code/modules/awaymissions/gateway.dm:91
    public override async DmTask<object> post_transfer(atom_movable AM = null)
    {
        object dot = await base.post_transfer(AM);
        _addtimer(Dm.@new<datum_callback>(AM, nameof(atom_movable.setDir), target_gateway.dir), 0, file: "code/modules/awaymissions/gateway.dm", line: 93);
        return dot;
    }
}

/* Special home destination, so we can check exile implants */
// tg: code/modules/awaymissions/gateway.dm:96
public partial class datum_gateway_destination_gateway_home : datum_gateway_destination_gateway
{
    // tg: code/modules/awaymissions/gateway.dm:98
    public override object incoming_pass_check(atom_movable AM = null)
    {
        if (isliving(AM))
        {
            if (Dm.truthy(check_exile_implant(AM as mob_living)))
                return FALSE;
        }
        else
        {
            foreach (mob_living L in Dm.each<mob_living>(AM.contents))
            {
                if (Dm.truthy(check_exile_implant(L)))
                {
                    Waiting.Detach(target_gateway.say(Dm.text("Rejecting []: Exile implant detected in contained lifeform.", AM)));
                    return FALSE;
                }
            }
        }
        if (Dm.truthy(AM.has_buckled_mobs()))
        {
            foreach (mob_living L in Dm.each<mob_living>(AM.buckled_mobs))
            {
                if (Dm.truthy(check_exile_implant(L)))
                {
                    Waiting.Detach(target_gateway.say(Dm.text("Rejecting []: Exile implant detected in close proximity lifeform.", AM)));
                    return FALSE;
                }
            }
        }
        return TRUE;
    }

    // tg: code/modules/awaymissions/gateway.dm:114
    public virtual object check_exile_implant(mob_living L = null)
    {
        foreach (obj_item_implant_exile E in Dm.each<obj_item_implant_exile>(L.implants)) //Checking that there is an exile implant
        {
            to_chat(L, span_userdanger(I18n.T("The station gate has detected your exile implant and is blocking your entry.")));
            return TRUE;
        }
        return FALSE;
    }
}

/* Destination is one ore more turfs - created by landmarks */
// tg: code/modules/awaymissions/gateway.dm:123
public partial class datum_gateway_destination_point : datum_gateway_destination
{
    // tg: code/modules/awaymissions/gateway.dm:127
    public override object get_target_turf()
    {
        return pick(target_turfs);
    }
}

/* Dense invisible object starting the teleportation. Created by gateways on activation. */
// tg: code/modules/awaymissions/gateway.dm:131
public partial class obj_effect_gateway_portal_bumper : obj_effect
{
    // tg: code/modules/awaymissions/gateway.dm:136
    public override object Bumped(atom_movable AM = null)
    {
        if (Dm.eq(get_dir(this, AM), gateway?.dir))
        {
            playsound(this, Dm.resource("sound/machines/gateway/gateway_travel.ogg"), 70, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
            Waiting.Detach(gateway.Transfer(AM));
        }
        return null;
    }

    // tg: code/modules/awaymissions/gateway.dm:141
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        gateway = null;
        return dot;
    }
}

// tg: code/modules/awaymissions/gateway.dm:146
[DmPath("/obj/machinery/gateway")]
public partial class obj_machinery_gateway : obj_machinery
{
    // The var block is hand-written: the types converter's rule V3 makes `destination` a typepath (tg assigns typepaths to vars
    // of that name elsewhere), but it holds the gateway's own destination datum.
    public float calibrated = 1; // tg: code/modules/awaymissions/gateway.dm:163
    /// Type of instanced gateway destination, needs to be subtype of /datum/gateway_destination/gateway
    public Type destination_type = typeof(datum_gateway_destination_gateway); // tg: code/modules/awaymissions/gateway.dm:165
    /// Name of the generated destination
    public string destination_name = "Unknown Gateway"; // tg: code/modules/awaymissions/gateway.dm:167
    /// This is our own destination, pointing at this gateway
    public datum_gateway_destination_gateway destination; // tg: code/modules/awaymissions/gateway.dm:169
    /// This is current active destination
    public datum_gateway_destination target; // tg: code/modules/awaymissions/gateway.dm:171
    /// bumper object, the thing that starts actual teleport
    public obj_effect_gateway_portal_bumper portal; // tg: code/modules/awaymissions/gateway.dm:173
    /// Visual object for handling the viscontents
    public atom_movable_screen_map_view_gateway_port portal_visuals; // tg: code/modules/awaymissions/gateway.dm:175
    public float teleportion_possible = 0; // tg: code/modules/awaymissions/gateway.dm:176
    public float transport_active = 0; // tg: code/modules/awaymissions/gateway.dm:177

    // tg: code/modules/awaymissions/gateway.dm:146
    public obj_machinery_gateway()
    {
        name = "gateway";
        desc = "A mysterious gateway built by unknown hands, it allows for faster than light travel to far-flung locations.";
        icon = Dm.resource("icons/obj/machines/gateway.dmi");
        icon_state = "portal_frame";
        resistance_flags = INDESTRUCTIBLE | LAVA_PROOF | FIRE_PROOF | UNACIDABLE | ACID_PROOF;

        // 3x2 offset by one row
        pixel_x = -32;
        pixel_y = -32;
        bound_height = 64;
        bound_width = 96;
        bound_x = -32;
        bound_y = 0;
        density = 1;

        active_power_usage = BASE_MACHINE_ACTIVE_CONSUMPTION * 5;
    }

    // tg: code/modules/awaymissions/gateway.dm:179
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        generate_destination();
        update_appearance();
        portal_visuals = Dm.@new<atom_movable_screen_map_view_gateway_port>();
        portal_visuals.generate_view(Dm.text("gateway_popup_[]", REF(this)));
        portal_visuals.update_portal_filters();
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/awaymissions/gateway.dm:187
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref portal_visuals);
        destination.target_gateway = null;
        Dm.remove(ref GLOB.gateway_destinations, destination);
        destination = null;
        return base.Destroy(force);
    }

    // tg: code/modules/awaymissions/gateway.dm:194
    public virtual void generate_destination()
    {
        destination = Dm.@new(destination_type) as datum_gateway_destination_gateway;
        destination.name = destination_name;
        destination.target_gateway = this;
        Dm.append(ref GLOB.gateway_destinations, destination);
    }

    // tg: code/modules/awaymissions/gateway.dm:200
    public virtual void deactivate()
    {
        datum_gateway_destination dest = target;
        target = null;
        playsound(this, Dm.resource("sound/machines/gateway/gateway_close.ogg"), 140, TRUE, TRUE, SOUND_RANGE);
        dest.deactivate(this);
        QDEL_NULL(ref portal);
        update_use_power(IDLE_POWER_USE);
        transport_active = FALSE;
        update_appearance();
        portal_visuals.reset_visuals();
    }

    // tg: code/modules/awaymissions/gateway.dm:211
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)) && Dm.truthy(use_power))
        {
            teleportion_possible = FALSE;
            if (target != null)
                deactivate();
            return null;
        }
        if (Dm.truthy(teleportion_possible))
            return null;
        foreach (datum_gateway_destination possible_destination in Dm.each<datum_gateway_destination>(GLOB.gateway_destinations))
        {
            if (!Dm.truthy(valid_destination(possible_destination)) || !Dm.truthy(possible_destination.is_available()))
                continue;
            teleportion_possible = TRUE;
            update_appearance();
            break;
        }
        return null;
    }

    // tg: code/modules/awaymissions/gateway.dm:226
    public virtual object valid_destination(datum_gateway_destination possible_destination = null)
    {
        if (possible_destination == destination)
            return FALSE;
        if (istype(possible_destination, typeof(datum_gateway_destination_gateway)))
        {
            datum_gateway_destination_gateway gateway_dest = possible_destination as datum_gateway_destination_gateway;
            if (gateway_dest.target_gateway == this)
                return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/awaymissions/gateway.dm:235
    public virtual DmList show_light_overlays(object light_state = null, object toggle = null)
    {
        if (!Dm.truthy(toggle))
            return Dm.list();

        DmList to_animate = Dm.list();
        Dm.append(ref to_animate, image(Dm.resource("icons/obj/machines/gateway.dmi"), light_state));
        // DEVIATION(DV12): the second image, GLOB.emissive_color on EMISSIVE_PLANE, is the glow of lighting planes
        return to_animate;
    }

    // tg: code/modules/awaymissions/gateway.dm:247
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        Dm.append(ref dot, show_light_overlays("portal_light", teleportion_possible));
        Dm.append(ref dot, show_light_overlays("portal_effect", transport_active));
        return dot;
    }

    // tg: code/modules/awaymissions/gateway.dm:252
    public override async DmTask<object> safe_throw_at(atom target = null, object range = null, object speed = null, atom thrower = null, float? spin = null, float? diagonals_first = null, datum_callback callback = null, float? force = null, float? gentle = null)
    {
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/modules/awaymissions/gateway.dm:255
    public virtual void generate_bumper()
    {
        portal = Dm.@new<obj_effect_gateway_portal_bumper>(get_turf(this));
        portal.gateway = this;
    }

    // tg: code/modules/awaymissions/gateway.dm:259
    public virtual void activate(datum_gateway_destination D = null)
    {
        if (!Dm.truthy(powered__proc()) || target != null)
            return;
        target = D;
        target.activate(destination);
        portal_visuals.setup_visuals(target);
        transport_active = TRUE;
        playsound(this, Dm.resource("sound/machines/gateway/gateway_open.ogg"), 140, TRUE, TRUE, SOUND_RANGE);
        generate_bumper();
        update_use_power(ACTIVE_POWER_USE);
        update_appearance();
    }

    // tg: code/modules/awaymissions/gateway.dm:271
    public virtual async DmTask Transfer(atom_movable AM = null)
    {
        if (target == null || !Dm.truthy(target.incoming_pass_check(AM)))
            return;
        await AM.forceMove(target.get_target_turf() as atom);
        await target.post_transfer(AM);
    }

    // tg: code/modules/awaymissions/gateway.dm:277
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        object dot = await base.attack_ghost(user);
        if (Dm.truthy(dot))
            return dot;
        turf tar_turf = target?.get_target_turf() as turf;
        if (tar_turf == null)
        {
            to_chat(user, span_warning(I18n.T("There's no active destination for the gateway... or it's broken. Maybe try again later?")));
            return dot;
        }
        if (Dm.truthy(is_secret_level(tar_turf.z)) && !Dm.truthy(user.client?.holder))
        {
            to_chat(user, span_warning(I18n.T("The gateway destination is secret.")));
            return dot;
        }
        await Transfer(user);
        return dot;
    }

    // tg: code/modules/awaymissions/gateway.dm:305
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(calibrated))
            to_chat(user, span_alert(I18n.T("The gate is already calibrated, there is no work for you to do here.")));
        else
        {
            playsound(this, Dm.resource("sound/machines/gateway/gateway_calibrated.ogg"), 80, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
            to_chat(user, Dm.text("[]: \\black This gate's systems have been fine tuned. Travel to this gate will now be on target.", span_boldnotice(I18n.T("Recalibration successful!"))));
            calibrated = TRUE;
        }
        return TRUE;
    }
}

/* Station's primary gateway */
// tg: code/modules/awaymissions/gateway.dm:291
public partial class obj_machinery_gateway_centerstation : obj_machinery_gateway
{
    // tg: code/modules/awaymissions/gateway.dm:295
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (GLOB.the_gateway == null)
            GLOB.the_gateway = this;
        return dot;
    }

    // tg: code/modules/awaymissions/gateway.dm:300
    public override object Destroy(float? force = null)
    {
        if (GLOB.the_gateway == this)
            GLOB.the_gateway = null;
        return base.Destroy(force);
    }
}

/* Doesn't need control console or power, always links to home when interacting. */
// tg: code/modules/awaymissions/gateway.dm:315
public partial class obj_machinery_gateway_away : obj_machinery_gateway
{
    // tg: code/modules/awaymissions/gateway.dm:319
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        if (target == null)
        {
            if (GLOB.the_gateway == null)
                to_chat(user, span_warning(I18n.T("Home gateway is not responding!")));
            if (GLOB.the_gateway.target != null)
                GLOB.the_gateway.deactivate(); //this will turn the home gateway off so that it's free for us to connect to
            activate(GLOB.the_gateway.destination);
        }
        else
            deactivate();
        return dot;
    }
}

/* Gateway control computer */
// tg: code/modules/awaymissions/gateway.dm:331
public partial class obj_machinery_computer_gateway_control : obj_machinery_computer
{
    // tg: code/modules/awaymissions/gateway.dm:337
    [DmParams("mapload", "C")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        try_to_linkup();
        return dot;
    }

    // tg: code/modules/awaymissions/gateway.dm:341
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Gateway", name);
            G.portal_visuals.display_to(user, ui.window);
            await ui.open();
        }
        return dot;
    }

    // tg: code/modules/awaymissions/gateway.dm:349
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = await base.ui_data(user) as DmList ?? Dm.list();
        dot["gateway_present"] = G;
        dot["gateway_status"] = G != null ? G.powered__proc() : FALSE;
        dot["current_target"] = G?.target?.get_ui_data();
        dot["gateway_mapkey"] = G.portal_visuals.assigned_map;
        DmList destinations = Dm.list();
        if (G != null)
        {
            foreach (datum_gateway_destination possible_destination in Dm.each<datum_gateway_destination>(GLOB.gateway_destinations))
            {
                if (!Dm.truthy(G.valid_destination(possible_destination)))
                    continue;
                Dm.append(ref destinations, Dm.list(possible_destination.get_ui_data()));
            }
        }
        dot["destinations"] = destinations;
        return dot;
    }

    // tg: code/modules/awaymissions/gateway.dm:363
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        object sw__1 = action;
        if (Dm.eq(sw__1, "linkup"))
        {
            try_to_linkup();
            return TRUE;
        }
        else if (Dm.eq(sw__1, "activate"))
        {
            datum_gateway_destination D = Dm.locate_ref(@params["destination"]) is datum_gateway_destination found && Dm.@in(found, GLOB.gateway_destinations) ? found : null; // locate(params["destination"]) in GLOB.gateway_destinations
            try_to_connect(D);
            return TRUE;
        }
        else if (Dm.eq(sw__1, "deactivate"))
        {
            if (G?.target != null)
                G.deactivate();
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/awaymissions/gateway.dm:380
    public override void ui_close(mob user = null)
    {
        base.ui_close(user);
        G.portal_visuals.hide_from(user);
    }

    // tg: code/modules/awaymissions/gateway.dm:384
    public virtual void try_to_linkup()
    {
        G = Dm.locate<obj_machinery_gateway>(view(7, get_turf(this)));
    }

    // tg: code/modules/awaymissions/gateway.dm:387
    public virtual void try_to_connect(datum_gateway_destination D = null)
    {
        if (D == null || G == null)
            return;
        if (!Dm.truthy(D.is_available()) || G.target != null)
            return;
        G.activate(D);
    }
}

// tg: code/modules/awaymissions/gateway.dm:405
public partial class atom_movable_screen_map_view_gateway_port : atom_movable_screen_map_view
{
    // tg: code/modules/awaymissions/gateway.dm:410
    [DmParams("mapload", "hud_owner")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        cam_background = Dm.@new<atom_movable_screen_background>();
        cam_background.del_on_map_removal = FALSE;
        // Draw above everything
        cam_background.layer = 200;
        cam_background.plane = HIGHEST_EVER_PLANE;
        cam_background.blend_mode = BLEND_OVERLAY;
        return dot;
    }

    // tg: code/modules/awaymissions/gateway.dm:420
    public override void generate_view(object map_key = null)
    {
        base.generate_view(map_key);
        cam_background.assigned_map = assigned_map;
        cam_background.fill_rect(1, 1, 3, 3);
    }

    // tg: code/modules/awaymissions/gateway.dm:425
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref cam_background);
        return base.Destroy(force);
    }

    // tg: code/modules/awaymissions/gateway.dm:429
    public override void display_on_ui_visible(datum_tgui_window window = null, client show_to = null)
    {
        base.display_on_ui_visible(window, show_to);
        show_to.register_map_obj(cam_background);
    }

    // tg: code/modules/awaymissions/gateway.dm:433
    public virtual void setup_visuals(datum_gateway_destination D = null)
    {
        our_destination = D;
        set_display(our_destination?.get_target_turf() as atom);
        update_portal_filters();
    }

    // tg: code/modules/awaymissions/gateway.dm:438
    public virtual void reset_visuals()
    {
        our_destination = null;
        set_display(our_destination?.get_target_turf() as atom);
        update_portal_filters();
    }

    // tg: code/modules/awaymissions/gateway.dm:443
    public virtual void update_portal_filters()
    {
        cam_background.clear_filters();
        // ok so what this used to do was render the tiles "on the other side" of the gateway onto the gateway mask
        // Unfortunately since I've removed the plane inheriting from /atom vis_flags, this no longer works
        // You could setup gateways to draw onto "lower then everything" z layers, but generating a whole stack of plane masters
        // Just for this one effect is kinda silly. Maybe next time
        // Rather then that, let's just render a little preview port to the console, because for reasons that's trivial
        vis_contents = null;

        turf center_turf = our_destination?.get_target_turf() as turf;
        if (center_turf == null)
        {
            // Draw static
            cam_background.icon_state = "scanline2";
            cam_background.color = null;
            cam_background.alpha = 255;
            return;
        }

        cam_background.add_filter("portal_blur", 1, Dm.list(Dm.kv("type", "blur"), Dm.kv("size", 0.5f)));

        vis_contents.Add(Dm.sub(CORNER_BLOCK_OFFSET(center_turf, 3, 3, -1, -1), center_turf)); // TURF_NEIGHBORS (__DEFINES/turfs.dm:47), inline
        cam_background.icon_state = "scanline4";
        cam_background.color = "#adadff";
        cam_background.alpha = 128;
    }
}
