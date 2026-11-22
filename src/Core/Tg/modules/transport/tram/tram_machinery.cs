// Translated from tgstation (snapshot 1d99bd7f): code/modules/transport/tram/tram_machinery.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The tram port (2026-11-12): the whole file but the button's attack_ai (AI and cyborgs, live-partial.txt).

// tg: code/modules/transport/tram/tram_machinery.dm:1
public partial class obj_item_assembly_control_transport : obj_item_assembly_control
{
    // tg: code/modules/transport/tram/tram_machinery.dm:8
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        DmList available_platforms = Dm.list();
        foreach (obj_effect_landmark_transport_nav_beacon_tram_platform platform in Dm.each<obj_effect_landmark_transport_nav_beacon_tram_platform>(SStransport.nav_beacons[specific_transport_id]))
            LAZYADD(ref available_platforms, platform.name);

        object selected_platform = await tgui_input_list(user, I18n.T("Set the platform ID"), I18n.T("Platform"), available_platforms);
        obj_effect_landmark_transport_nav_beacon_tram_platform change_platform = null;
        foreach (obj_effect_landmark_transport_nav_beacon_tram_platform destination in Dm.each<obj_effect_landmark_transport_nav_beacon_tram_platform>(SStransport.nav_beacons[specific_transport_id]))
        {
            if (Dm.eq(destination.name, selected_platform))
            {
                change_platform = destination;
                break;
            }
        }

        if (change_platform == null || QDELETED(user) || QDELETED(this) || !Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return null;

        if (Dm.num(get_dist(change_platform, this)) > 15)
        {
            balloon_alert(user, I18n.T("out of range!"));
            return null;
        }

        id = change_platform.platform_code;
        balloon_alert(user, I18n.T("platform changed"));
        to_chat(user, span_notice(I18n.T("You change the platform ID to [].", change_platform.name)));
        return null;
    }

    // tg: code/modules/transport/tram/tram_machinery.dm:48
    public virtual void call_response(object controller = null, DmList relevant = null, object response_code = null, object response_info = null)
    {
        if (!Dm.truthy(LAZYFIND(relevant, this)))
            return;
        if (Dm.truthy(SStts.tts_enabled))
            voice = SStts.tram_voice;
        if (Dm.eq(response_code, REQUEST_SUCCESS))
        {
            Waiting.Detach(say(I18n.T("The tram has been called to the platform.")));
        }
        else if (Dm.eq(response_code, REQUEST_FAIL))
        {
            if (Dm.eq(response_info, BROKEN_BEYOND_REPAIR))
                Waiting.Detach(say(I18n.T("The tram has suffered a catastrophic failure. Please seek alternate modes of travel.")));
            else if (Dm.eq(response_info, NOT_IN_SERVICE)) //tram has no power or other fault, but it's not broken forever
                Waiting.Detach(say(I18n.T("The tram is not in service due to loss of power or system problems. Please contact the nearest engineer to check power and controller.")));
            else if (Dm.eq(response_info, INVALID_PLATFORM)) //engineer needs to fix button
                Waiting.Detach(say(I18n.T("Button configuration error. Please contact the nearest engineer.")));
            else if (Dm.eq(response_info, TRANSPORT_IN_USE))
                Waiting.Detach(say(I18n.T("The tram is tramversing the station, please wait.")));
            else if (Dm.eq(response_info, INTERNAL_ERROR))
                Waiting.Detach(say(I18n.T("Tram controller error. Please contact the nearest engineer or crew member with telecommunications access to reset the controller.")));
            else if (Dm.eq(response_info, NO_CALL_REQUIRED)) //already here
                Waiting.Detach(say(I18n.T("The tram is already here. Please board the tram and select a destination.")));
            else
                Waiting.Detach(say(I18n.T("Tram controller error. Please contact the nearest engineer or crew member with telecommunications access to reset the controller.")));
        }
    }
}

// tg: code/modules/transport/tram/tram_machinery.dm:32
public partial class obj_item_assembly_control_transport_call_button : obj_item_assembly_control_transport
{
    // tg: code/modules/transport/tram/tram_machinery.dm:38
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/modules/transport/tram/tram_machinery.dm:42
    public override void LateInitialize(params object[] __extra)
    {
        if (!Dm.truthy(id_tag))
            id_tag = assign_random_name();
        SStransport.hello(this, name, id_tag);
        RegisterSignal(SStransport, COMSIG_TRANSPORT_RESPONSE, nameof(call_response));
    }

    // tg: code/modules/transport/tram/tram_machinery.dm:73
    public override async DmTask<object> activate(mob activator = null)
    {
        if (Dm.truthy(cooldown))
            return null;
        cooldown = TRUE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(cooldown_varset)), 2 * SECONDS, file: "code/modules/transport/tram/tram_machinery.dm", line: 77); // VARSET_CALLBACK(src, cooldown, FALSE)

        // INVOKE_ASYNC(SStransport, TYPE_PROC_REF(/datum/controller/subsystem/processing/transport, call_request), src, specific_transport_id, id)
        SEND_SIGNAL(COMSIG_TRANSPORT_REQUEST, Dm.list(this, specific_transport_id, id));
        return null;
    }

    // VARSET_CALLBACK(src, cooldown, FALSE)'s target (tg: code/modules/transport/tram/tram_machinery.dm:77)
    public virtual void cooldown_varset() => cooldown = FALSE;
}

// tg: code/modules/transport/tram/tram_machinery.dm:84
public partial class obj_machinery_button_transport_tram : obj_machinery_button_transport
{
    /// We allow borgs to use the button locally, but not the AI remotely
    // tg: code/modules/transport/tram/tram_machinery.dm:97
    public override async DmTask<object> attack_ai(mob user = null)
    {
        if (isAI(user) || Dm.truthy(panel_open))
            return null;
        if (HAS_SILICON_ACCESS(user) && !issilicon(user)) //admins and remote controls can use it at a distance
            return await attack_hand(user);
        if (Dm.truthy(in_range(user, this)))
            return await attack_hand(user);
        to_chat(user, span_warning("You are too far away to activate the button!"));
        return null;
    }

    // tg: code/modules/transport/tram/tram_machinery.dm:107
    public override void setup_device(object mapload = null)
    {
        base.setup_device(mapload);
        if (device_instance is obj_item_assembly_control_transport_call_button tram_device) // GAP(types): the device instance (buttons.cs)
            tram_device.specific_transport_id = specific_transport_id;
    }

    // tg: code/modules/transport/tram/tram_machinery.dm:113
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("There's a small inscription on the button...")));
        Dm.append(ref dot, span_notice(I18n.T("THIS CALLS THE TRAM! IT DOES NOT OPERATE IT! The console on the tram tells it where to go!")));
        return dot;
    }
}
