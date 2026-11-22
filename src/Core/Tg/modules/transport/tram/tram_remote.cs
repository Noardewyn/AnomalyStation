// Translated from tgstation (snapshot 1d99bd7f): code/modules/transport/tram/tram_remote.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The tram port (2026-11-12): the whole file but add_context (the screentips stay deferred, live-partial.txt).

// tg: code/modules/transport/tram/tram_remote.dm:1
public partial class obj_item_assembly_control_transport_remote : obj_item_assembly_control_transport
{
    /// GAP(types): tg's `var/destination` (a platform code), which the types converter generates as a Type.
    public object destination_value;

    // tg: code/modules/transport/tram/tram_remote.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(id_tag))
            id_tag = assign_random_name();
        SStransport.hello(this, name, id_tag);
        return dot;
    }

    //set tram destination
    // tg: code/modules/transport/tram/tram_remote.dm:33
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        DmList available_platforms = Dm.list();
        foreach (obj_effect_landmark_transport_nav_beacon_tram_platform platform in Dm.each<obj_effect_landmark_transport_nav_beacon_tram_platform>(SStransport.nav_beacons[specific_transport_id]))
            LAZYADD(ref available_platforms, platform.name);

        object selected_platform = await tgui_input_list(user, I18n.T("Available destinations"), I18n.T("Where to?"), available_platforms);
        foreach (obj_effect_landmark_transport_nav_beacon_tram_platform potential_platform in Dm.each<obj_effect_landmark_transport_nav_beacon_tram_platform>(SStransport.nav_beacons[specific_transport_id]))
        {
            if (Dm.eq(potential_platform.name, selected_platform))
            {
                destination_value = potential_platform.platform_code;
                break;
            }
        }

        balloon_alert(user, I18n.T("set []", selected_platform));
        to_chat(user, span_notice(I18n.T("You change the platform ID on [] to [].", this, selected_platform)));
        return null;
    }

    ///set safety bypass
    // tg: code/modules/transport/tram/tram_remote.dm:48
    public override async DmTask<object> item_ctrl_click(mob user = null)
    {
        // tg's switch(options) with the cases if(!RAPID_MODE) (0) and if(RAPID_MODE)
        if (Dm.eq(options, 0))
            options = Dm.num(Dm.bitor(options, RAPID_MODE));
        else if (Dm.eq(options, RAPID_MODE))
            options = Dm.num(Dm.bitand(options, ~RAPID_MODE));
        update_appearance();
        balloon_alert(user, I18n.T("mode: []", Dm.truthy(options) ? I18n.T("fast") : I18n.T("safe")));
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/transport/tram/tram_remote.dm:58
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(specific_transport_id))
        {
            Dm.append(ref dot, I18n.T("There is an X showing on the display."));
            Dm.append(ref dot, I18n.T("Left-click to link to a tram."));
            return dot;
        }
        Dm.append(ref dot, I18n.T("The rapid mode light is [].", Dm.truthy(options) ? I18n.T("on") : I18n.T("off")));
        if (Dm.truthy(cooldown))
            Dm.append(ref dot, I18n.T("The number on the display shows [].", DisplayTimeText(cooldown, 1)));
        else
            Dm.append(ref dot, I18n.T("The display indicates ready."));
        Dm.append(ref dot, I18n.T("Left-click to dispatch tram."));
        Dm.append(ref dot, I18n.T("Right-click to set destination."));
        Dm.append(ref dot, I18n.T("Ctrl-click to toggle safety bypass."));
        Dm.append(ref dot, I18n.T("Alt-click to change configured tram."));
        return dot;
    }

    // tg: code/modules/transport/tram/tram_remote.dm:76
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);

        if (!Dm.truthy(specific_transport_id))
        {
            icon_state = "tramremote_nis";
            return dot;
        }

        icon_state = "tramremote_ob";
        return dot;
    }

    // tg: code/modules/transport/tram/tram_remote.dm:85
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(Dm.bitand(options, RAPID_MODE)))
            Dm.append(ref dot, mutable_appearance(icon, "tramremote_emag"));
        return dot;
    }

    // tg: code/modules/transport/tram/tram_remote.dm:90
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(specific_transport_id))
        {
            await link_tram(user);
            return null;
        }

        if (Dm.truthy(cooldown))
        {
            balloon_alert(user, I18n.T("cooldown: []", DisplayTimeText(cooldown, 1)));
            return null;
        }

        await activate(user);
        tram_remote = world.time + 2 * MINUTES; // COOLDOWN_START(src, tram_remote, 2 MINUTES)
        return null;
    }

    ///send our selected commands to the tram
    // tg: code/modules/transport/tram/tram_remote.dm:103
    public override async DmTask<object> activate(mob activator = null)
    {
        mob user = activator;
        if (!Dm.truthy(specific_transport_id))
        {
            balloon_alert(user, I18n.T("no tram linked!"));
            return null;
        }
        if (!Dm.truthy(destination_value))
        {
            balloon_alert(user, I18n.T("no destination!"));
            return null;
        }

        SEND_SIGNAL(COMSIG_TRANSPORT_REQUEST, Dm.list(this, specific_transport_id, destination_value, options));
        return null;
    }

    // tg: code/modules/transport/tram/tram_remote.dm:113
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await link_tram(user);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/transport/tram/tram_remote.dm:117
    public virtual async DmTask link_tram(mob user = null)
    {
        specific_transport_id = null;
        DmList transports_available = null;
        foreach (datum_transport_controller_linear_tram tram in Dm.each<datum_transport_controller_linear_tram>(SStransport.transports_by_type[TRANSPORT_TYPE_TRAM]))
            LAZYADD(ref transports_available, tram.specific_transport_id);

        object picked = await tgui_input_list(user, I18n.T("Available transports"), I18n.T("Select a transport"), transports_available);
        specific_transport_id = picked == null ? null : Dm.text("[]", picked);

        if (Dm.truthy(specific_transport_id))
            balloon_alert(user, I18n.T("tram linked"));
        else
            balloon_alert(user, I18n.T("link failed!"));

        update_appearance();
    }
}
