// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/transport.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// D325 (multi-z, elevators): the subsystem the elevators register with (transports_by_type). The var block is generated (D111).
// The tram port (2026-11-12): the tram dispatch procs (incoming_request, dispatch_transport, pre_departure, validate_and_dispatch).

// tg: code/controllers/subsystem/transport.dm:1 PROCESSING_SUBSYSTEM_DEF(transport)
public partial class datum_controller_subsystem_processing_transport : datum_controller_subsystem_processing
{
    // The New() that PROCESSING_SUBSYSTEM_DEF(transport) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SStransport)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/transport.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SStransport != this)
        {
            if (SStransport is datum_controller_subsystem_processing_transport)
            {
                Recover();
                qdel(SStransport);
            }
            SStransport = this;
        }
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(transport) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/transport.dm:1
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }

    /**
     * Registers the subsystem to listen for incoming requests from paired devices
     *
     * When a new device (such as a button, tram, signal etc) comes online
     * it calls this proc with the subsystem enabling two-way communication using
     * signals.
     */
    // tg: code/controllers/subsystem/transport.dm:44
    public virtual void hello(atom new_unit = null, object unit_name = null, object id_tag = null)
    {
        RegisterSignal(new_unit, COMSIG_TRANSPORT_REQUEST, nameof(incoming_request));
        log_transport(Dm.text("Sub: Registered new transport component [] [].", unit_name, id_tag));
    }

    // tg: code/controllers/subsystem/transport.dm:48
    public override void Recover()
    {
        _listen_lookup = SStransport._listen_lookup;
    }

    /**
     * Performs the request received from a registered transport device
     *
     * Currently the only supported request type is tram dispatch
     */
    // tg: code/controllers/subsystem/transport.dm:67
    public virtual void incoming_request(obj source = null, object transport_id = null, object platform = null, object options = null)
    {
        log_transport(Dm.text("Sub: Received request from [] []. Contents: [] [] []", source.name, source.id_tag, transport_id, platform, options));
        DmList relevant = null;
        object request_flags = options;
        datum_transport_controller_linear_tram transport_controller = null;
        obj_effect_landmark_transport_nav_beacon_tram_platform destination = null;
        foreach (datum_transport_controller_linear_tram candidate_controller in Dm.each<datum_transport_controller_linear_tram>(transports_by_type[TRANSPORT_TYPE_TRAM]))
        {
            if (Dm.eq(candidate_controller.specific_transport_id, transport_id))
            {
                transport_controller = candidate_controller;
                break;
            }
        }

        // We make a list of relevant devices (that should act/respond to this request) for when we send the signal at the end
        LAZYADD(ref relevant, source);

        // Check for various failure states
        // The transport controller datum is qdel'd
        if (transport_controller == null)
        {
            log_transport(Dm.text("Sub: Transport [] has no controller datum! Someone deleted it or something catastrophic happened.", transport_id));
            SEND_SIGNAL(COMSIG_TRANSPORT_RESPONSE, Dm.list(this, relevant, REQUEST_FAIL, BROKEN_BEYOND_REPAIR));
            log_transport(Dm.text("Sub: Sending response to []. Contents: [] [].", source.id_tag, REQUEST_FAIL, INTERNAL_ERROR));
            return;
        }

        // Non operational (such as power loss) or the controls cabinet is missing/destroyed
        if (!Dm.truthy(transport_controller.controller_operational) || transport_controller.paired_cabinet == null)
        {
            SEND_SIGNAL(COMSIG_TRANSPORT_RESPONSE, Dm.list(this, relevant, REQUEST_FAIL, NOT_IN_SERVICE));
            log_transport(Dm.text("Sub: Sending response to []. Contents: [] [].", source.id_tag, REQUEST_FAIL, NOT_IN_SERVICE));
            return;
        }

        // Someone emergency stopped the tram, or something went wrong and it needs to reset its landmarks.
        if ((transport_controller.status_bits & SYSTEM_FAULT) != 0 || (transport_controller.status_bits & EMERGENCY_STOP) != 0)
        {
            SEND_SIGNAL(COMSIG_TRANSPORT_RESPONSE, Dm.list(this, relevant, REQUEST_FAIL, INTERNAL_ERROR));
            log_transport(Dm.text("Sub: Sending response to []. Contents: [] []. Info: TS-[].", source.id_tag, REQUEST_FAIL, INTERNAL_ERROR, transport_controller.controller_status));
            return;
        }

        // Controller is 'active' (not accepting requests right now) someone already pushed button, hit by a rod, etc.
        if (Dm.truthy(transport_controller.controller_active))
        {
            SEND_SIGNAL(COMSIG_TRANSPORT_RESPONSE, Dm.list(this, relevant, REQUEST_FAIL, TRANSPORT_IN_USE));
            log_transport(Dm.text("Sub: Sending response to []. Contents: [] []. Info: TA-PROCESSING.", source.id_tag, REQUEST_FAIL, TRANSPORT_IN_USE));
            return;
        }

        // We've made it this far, tram is physically fine so let's trip plan
        // This is based on the destination nav beacon, the logical location
        // If Something Happens and the location the controller thinks it's at
        // gets out of sync with its actual physical location, it can be reset

        // Since players can set the platform ID themselves, make sure it's a valid platform we're aware of
        object network = LAZYACCESS(nav_beacons, transport_id);
        foreach (object candidate in Dm.each(network))
        {
            if (candidate is not obj_effect_landmark_transport_nav_beacon_tram_platform potential_destination)
                continue;
            if (Dm.eq(potential_destination.platform_code, platform))
            {
                destination = potential_destination;
                break;
            }
        }

        // The platform in the request doesn't exist (ie: Can't send a tram to East Wing when the map is Birdshot)
        if (destination == null)
        {
            SEND_SIGNAL(COMSIG_TRANSPORT_RESPONSE, Dm.list(this, relevant, REQUEST_FAIL, INVALID_PLATFORM));
            log_transport(Dm.text("Sub: Sending response to []. Contents: [] []. Info: RD0.", source.id_tag, REQUEST_FAIL, INVALID_PLATFORM));
            return;
        }

        // The controller thinks the tram is already there
        if (transport_controller.idle_platform == destination) //did you even look?
        {
            SEND_SIGNAL(COMSIG_TRANSPORT_RESPONSE, Dm.list(this, relevant, REQUEST_FAIL, NO_CALL_REQUIRED));
            log_transport(Dm.text("Sub: Sending response to []. Contents: [] []. Info: RD1.", source.id_tag, REQUEST_FAIL, NO_CALL_REQUIRED));
            return;
        }

        // Calculate the trip data, which will be stored on the controller datum, passed to the transport modules making up the tram
        // If for some reason the controller can't determine the distance/direction it needs to go, send a failure message
        if (!Dm.truthy(transport_controller.calculate_route(destination)))
        {
            SEND_SIGNAL(COMSIG_TRANSPORT_RESPONSE, Dm.list(this, relevant, REQUEST_FAIL, INTERNAL_ERROR));
            log_transport(Dm.text("Sub: Sending response to []. Contents: [] []. Info: NV0.", source.id_tag, REQUEST_FAIL, INTERNAL_ERROR));
            return;
        }

        // At this point we're sending the tram somewhere, so send a success response to the devices
        SEND_SIGNAL(COMSIG_TRANSPORT_RESPONSE, Dm.list(this, relevant, REQUEST_SUCCESS, destination.name));
        log_transport(Dm.text("Sub: Sending response to []. Contents: [] [].", source.id_tag, REQUEST_SUCCESS, destination.name));

        // Since this is a signal and we're done with the request, do the rest async
        Waiting.Detach(dispatch_transport(transport_controller, request_flags)); // INVOKE_ASYNC
    }

    /**
     * Dispatches the transport on a validated trip
     *
     * The subsystem at this point has confirmed a valid trip
     * Start the transport, wake up machinery running on
     * the subsystem (signals, etc.)
     *
     * Make tram go, basically.
     */
    // tg: code/controllers/subsystem/transport.dm:160
    // (tg's call passes the request flags second, into `destination`, so request_flags is null here, as in tg)
    public virtual async DmTask dispatch_transport(datum_transport_controller_linear_tram transport_controller = null, object destination = null, object request_flags = null)
    {
        log_transport(Dm.text("Sub: Sending dispatch request to []. []", transport_controller.specific_transport_id, Dm.truthy(request_flags) ? Dm.text("Contents: [].", request_flags) : "No request flags."));

        // This will generally be caught in the request validation, however an admin may try to force move the tram, or other actions bypassing the request process.
        if (transport_controller.idle_platform == transport_controller.destination_platform)
        {
            log_transport(Dm.text("Sub: [] dispatch failed. Info: DE-1 Transport Controller idle and destination are the same.", transport_controller.specific_transport_id));
            return;
        }

        // Set active, so no more requests will be accepted until we're in a safe state to change destination.
        transport_controller.set_active(TRUE);
        await pre_departure(transport_controller, request_flags);
    }

    /**
     * Pre-departure checks for the tram
     *
     * We do things slighly different based on the request_flags such as
     * door crushing, emag related things
     */
    // tg: code/controllers/subsystem/transport.dm:181
    public virtual async DmTask pre_departure(datum_transport_controller_linear_tram transport_controller = null, object request_flags = null)
    {
        log_transport(Dm.text("Sub: [] start pre-departure. Info: TS-[]", transport_controller.specific_transport_id, transport_controller.controller_status));

        // Lock the physical controls of the tram
        transport_controller.set_status_code(PRE_DEPARTURE, TRUE);
        transport_controller.set_status_code(CONTROLS_LOCKED, TRUE);

        // Tram door actions
        log_transport(Dm.text("Sub: [] requested door close. Info: TS-[].", transport_controller.specific_transport_id, transport_controller.controller_status));
        int flags = (int)Dm.num(request_flags);
        if ((flags & RAPID_MODE) != 0 || (flags & BYPASS_SENSORS) != 0 || (transport_controller.status_bits & BYPASS_SENSORS) != 0) // bypass for unsafe, rapid departure
        {
            transport_controller.cycle_doors(CYCLE_CLOSED, BYPASS_DOOR_CHECKS);
            if ((flags & RAPID_MODE) != 0)
            {
                log_transport(Dm.text("Sub: [] rapid mode enabled, bypassing validation.", transport_controller.specific_transport_id));
                transport_controller.dispatch_transport();
                return;
            }
        }
        else
        {
            playsound(transport_controller.nav_beacon, Dm.resource("sound/machines/tram/door_chime.ogg"), 45, vary: FALSE, extrarange: MEDIUM_RANGE_SOUND_EXTRARANGE);
            await stoplag(1.4f * SECONDS);
            transport_controller.set_status_code(DOORS_READY, FALSE);
            transport_controller.cycle_doors(CYCLE_CLOSED);
        }

        _addtimer(Dm.@new<datum_callback>(this, nameof(validate_and_dispatch), transport_controller), 3 * SECONDS, file: "code/controllers/subsystem/transport.dm", line: 206);
    }

    /**
     * Operational checks, then start moving
     *
     * Some check failures aren't worth halting the tram for, like no blocking the doors forever
     * Crush them instead!
     */
    // tg: code/controllers/subsystem/transport.dm:213
    public virtual void validate_and_dispatch(datum_transport_controller_linear_tram transport_controller = null, object attempt = null)
    {
        log_transport(Dm.text("Sub: [] start pre-departure validation. Attempts: [].", transport_controller.specific_transport_id, Dm.truthy(attempt) ? attempt : 0));
        float current_attempt;
        if (Dm.truthy(attempt))
            current_attempt = Dm.num(attempt);
        else
            current_attempt = 0;

        if (current_attempt >= 4)
        {
            log_transport(Dm.text("Sub: [] pre-departure validation failed, but dispatching tram anyways. Info: TS-[].", transport_controller.specific_transport_id, transport_controller.controller_status));
            transport_controller.dispatch_transport();
            return;
        }

        current_attempt++;

        transport_controller.update_status();
        if ((transport_controller.status_bits & DOORS_READY) == 0)
        {
            _addtimer(Dm.@new<datum_callback>(this, nameof(validate_and_dispatch), transport_controller, current_attempt), 3 * SECONDS, file: "code/controllers/subsystem/transport.dm", line: 229);
            return;
        }
        else
        {
            transport_controller.dispatch_transport();
            log_transport(Dm.text("Sub: [] pre-departure passed.", transport_controller.specific_transport_id));
        }
    }

    /// Give a list of destinations to the tram controls
    // tg: code/controllers/subsystem/transport.dm:238
    public virtual object detailed_destination_list(object specific_transport_id = null)
    {
        DmList dot = Dm.list();
        foreach (obj_effect_landmark_transport_nav_beacon_tram_platform destination in Dm.each<obj_effect_landmark_transport_nav_beacon_tram_platform>(SStransport.nav_beacons[specific_transport_id]))
        {
            DmList this_destination = Dm.list();
            this_destination["name"] = destination.name;
            this_destination["dest_icons"] = destination.tgui_icons;
            this_destination["id"] = destination.platform_code;
            Dm.append(ref dot, Dm.list(this_destination));
        }
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/transport.dm:1  PROCESSING_SUBSYSTEM_DEF(transport): GLOBAL_REAL(SStransport, /datum/controller/subsystem/processing/transport)
    internal datum_controller_subsystem_processing_transport SStransport;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_transport SStransport => ref TgState.Current.SStransport;
}
