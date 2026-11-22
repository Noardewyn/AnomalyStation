// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/telescreen.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// Gaps TL1 (D333's station order: security and command consoles): the whole file. The wall telescreens (camera consoles on a
// wall with their departments' networks, the CameraConsole window of computer/security), the entertainment monitors (a TV
// that shows the camera networks being broadcast, watched from across the room, with its speakers on the entertainment
// frequency), the global broadcast procs the broadcast camera and the showtime button drive, and the showtime button. The var
// blocks are generated; the MAPPING_DIRECTIONAL_HELPERS subtypes and the wall frames are vars only.

using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/game/machinery/computer/telescreen.dm:1
public partial class obj_machinery_computer_security_telescreen : obj_machinery_computer_security
{
    // tg: code/game/machinery/computer/telescreen.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        return dot;
    }

    // tg: code/game/machinery/computer/telescreen.dm:32
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        Dm.@new(frame_type, loc);
        return null;
    }

    // tg: code/game/machinery/computer/telescreen.dm:35
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.initial(this, (obj_machinery_computer_security_telescreen t) => t.icon_state);
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            icon_state = Dm.text("[]b", icon_state);
        return await base.update_icon_state(__extra);
    }
}

// tg: code/game/machinery/computer/telescreen.dm:41
public partial class obj_machinery_computer_security_telescreen_entertainment : obj_machinery_computer_security_telescreen
{
    // tg: code/game/machinery/computer/telescreen.dm:57
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        context[SCREENTIP_CONTEXT_CTRL_LMB] = I18n.T("Toggle mute button");
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/game/machinery/computer/telescreen.dm:61
    public override async DmTask<object> click_ctrl(mob user = null)
    {
        balloon_alert(user, Dm.truthy(speakers.should_be_listening) ? I18n.T("muted") : I18n.T("unmuted"));
        speakers.toggle_mute();
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/machinery/computer/telescreen.dm:78
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        RegisterSignal(SSdcs, COMSIG_GLOB_NETWORK_BROADCAST_UPDATED, nameof(on_network_broadcast_updated));
        speakers = Dm.@new<obj_item_radio_entertainment_speakers>(this);
        return dot;
    }

    // tg: code/game/machinery/computer/telescreen.dm:84
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(SSdcs, COMSIG_GLOB_NETWORK_BROADCAST_UPDATED);
        QDEL_NULL(ref speakers);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/computer/telescreen.dm:89
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, Dm.truthy(length(network)) ? span_notice(I18n.T("The TV is broadcasting something!")) : span_notice(I18n.T("<i>There's nothing on TV.</i>")));
        Dm.append(ref dot, span_notice(I18n.T("The volume is currently [].", Dm.truthy(speakers.should_be_listening) ? I18n.T("on") : I18n.T("off"))));
        return dot;
    }

    // tg: code/game/machinery/computer/telescreen.dm:94
    public override object ui_state(mob user = null) => GLOB.always_state;

    // Snowflake ui status to allow mobs to watch TV from across the room,
    // but only allow adjacent mobs / tk users / silicon to change the channel
    // tg: code/game/machinery/computer/telescreen.dm:99
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (!can_watch_tv(user))
            return UI_CLOSE;
        if (!isliving(user))
            return UI_UPDATE; // isAdminGhostAI(user) ? UI_INTERACTIVE : UI_UPDATE; GAP(lead): isAdminGhostAI needs admin rights (check_rights_for, M10), so no mob is one (is_helpers.cs)
        var living_user = (mob_living)user;
        if (Dm.truthy(living_user.incapacitated))
            return UI_UPDATE;

        bool can_range = false;
        if (living_user is mob_living_carbon carbon_user)
        {
            if (Dm.truthy(carbon_user.dna?.check_mutation(typeof(datum_mutation_telekinesis))) && Dm.truthy(tkMaxRangeCheck(user, this)))
                can_range = true;
        }
        if (HAS_SILICON_ACCESS(user) || (living_user.interaction_range != 0 && living_user.interaction_range >= get_dist(user, this)))
            can_range = true;

        if ((can_range || Dm.truthy(await IsReachableBy(user))) && ISADVANCEDTOOLUSER(user))
        {
            if (Dm.truthy(living_user.incapacitated))
                return UI_UPDATE;
            if (!can_range && Dm.truthy(living_user.can_hold_items()) && (living_user.usable_hands <= 0 || HAS_TRAIT(user, TRAIT_HANDS_BLOCKED)))
                return UI_UPDATE;
            return UI_INTERACTIVE;
        }
        return UI_UPDATE;
    }

    // tg: code/game/machinery/computer/telescreen.dm:123
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (world.time <= usr.next_click + 1)
            return null; // just so someone can't turn an auto clicker on and spam tvs

        object dot = await base.Click(location, control, @params);
        if (!can_watch_tv(usr))
            return dot;
        if ((!Dm.truthy(length(network)) && !Dm.truthy(await Adjacent(usr))) || Dm.truthy(LAZYACCESS(Dm.params2list(@params), SHIFT_CLICK))) // let people examine
            return dot;
        // Lets us see the tv regardless of click results
        Waiting.Detach(interact(usr)); // INVOKE_ASYNC(src, TYPE_PROC_REF(/atom, interact), usr)
        return dot;
    }

    // tg: code/game/machinery/computer/telescreen.dm:135
    public virtual bool can_watch_tv(mob watcher)
    {
        if (!Dm.truthy(is_operational))
            return false;
        if (Dm.truthy(Dm.bitand(watcher.sight, SEE_OBJS)) || HAS_SILICON_ACCESS(watcher))
        {
            if (get_dist(watcher, this) > 7)
                return false;
        }
        else
        {
            if (!Dm.truthy(can_see(watcher, this, 7)))
                return false;
        }
        if (Dm.truthy(watcher.is_blind()) || IS_UNCONSCIOUS(watcher))
            return false;
        return true;
    }

    /// Sets the monitor's icon to the selected state, and says an announcement
    // tg: code/game/machinery/computer/telescreen.dm:149
    public virtual async DmTask notify(object on, object announcement)
    {
        if (Dm.truthy(on))
            icon_screen = icon_state_on;
        else
            icon_screen = icon_state_off;
        update_appearance(UPDATE_OVERLAYS);
        if (Dm.truthy(announcement))
            await say(announcement);
    }

    /// Adds a camera network ID to the entertainment monitor, and turns off the monitor if network list is empty
    // tg: code/game/machinery/computer/telescreen.dm:159
    [SignalHandler]
    public virtual void on_network_broadcast_updated(datum source, object tv_show_id, object is_show_active, object announcement)
    {
        if (network == null)
            return;

        if (Dm.truthy(is_show_active))
            Dm.combine(ref network, tv_show_id);
        else
            Dm.remove(ref network, tv_show_id);

        Waiting.Detach(update_static_data_for_all_viewers()); // INVOKE_ASYNC(src, TYPE_PROC_REF(/datum, update_static_data_for_all_viewers))
        // notify says the announcement, which never sleeps in tg: run in place, as tg's direct call
        Waiting.Detach(notify(length(network), announcement));
    }
}

public static partial class GlobalProcs
{
    /**
     * Adds a camera network to all entertainment monitors.
     *
     * * camera_net - The camera network ID to add to the monitors.
     * * announcement - Optional, what announcement to make when the show starts.
     */
    // tg: code/game/machinery/computer/telescreen.dm:178
    public static void start_broadcasting_network(object camera_net, object announcement = null)
    {
        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_NETWORK_BROADCAST_UPDATED, camera_net, TRUE, announcement) (__DEFINES/dcs/helpers.dm:7), expanded inline
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_NETWORK_BROADCAST_UPDATED]))
            SSdcs._SendSignal(COMSIG_GLOB_NETWORK_BROADCAST_UPDATED, Dm.list(SSdcs, camera_net, TRUE, announcement));
    }

    /**
     * Removes a camera network from all entertainment monitors.
     *
     * * camera_net - The camera network ID to remove from the monitors.
     * * announcement - Optional, what announcement to make when the show ends.
     */
    // tg: code/game/machinery/computer/telescreen.dm:187
    public static void stop_broadcasting_network(object camera_net, object announcement = null)
    {
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_NETWORK_BROADCAST_UPDATED]))
            SSdcs._SendSignal(COMSIG_GLOB_NETWORK_BROADCAST_UPDATED, Dm.list(SSdcs, camera_net, FALSE, announcement));
    }

    /**
     * Sets the camera network status on all entertainment monitors.
     * A way to force a network to a status if you are unsure of the current state.
     *
     * * camera_net - The camera network ID to set on the monitors.
     * * is_show_active - Whether the show is active or not.
     * * announcement - Optional, what announcement to make.
     * Note this announcement will be made regardless of the current state of the show:
     * This means if it's currently on and you set it to on, the announcement will still be made.
     * Likewise, there's no way to differentiate off -> on and on -> off, unless you handle that yourself.
     */
    // tg: code/game/machinery/computer/telescreen.dm:201
    public static void set_network_broadcast_status(object camera_net, object is_show_active, object announcement = null)
    {
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_NETWORK_BROADCAST_UPDATED]))
            SSdcs._SendSignal(COMSIG_GLOB_NETWORK_BROADCAST_UPDATED, Dm.list(SSdcs, camera_net, is_show_active, announcement));
    }
}

// tg: code/game/machinery/computer/telescreen.dm:68
public partial class obj_item_wallframe_telescreen_entertainment : obj_item_wallframe_telescreen
{
    // tg: code/game/machinery/computer/telescreen.dm:74
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // transform.Scale(0.8): BYOND's transform read is a copy, scaled and discarded, so the frame keeps its size; nothing to do
        return dot;
    }
}

/// A button that adds a camera network to the entertainment monitors
// tg: code/game/machinery/computer/telescreen.dm:518
public partial class obj_machinery_button_showtime : obj_machinery_button
{
    // tg: code/game/machinery/computer/telescreen.dm:525
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (device_instance != null)
        {
            var ours = (obj_item_assembly_control_showtime)device_instance;
            ours.id = id;
        }
        return dot;
    }
}

// tg: code/game/machinery/computer/telescreen.dm:531
public partial class obj_item_assembly_control_showtime : obj_item_assembly_control
{
    // tg: code/game/machinery/computer/telescreen.dm:555
    public override async DmTask<object> activate(mob activator = null)
    {
        is_show_active = Dm.num(!Dm.truthy(is_show_active));
        await say(I18n.T("The [] show has []", tv_show_name, Dm.truthy(is_show_active) ? I18n.T("begun") : I18n.T("ended")));
        object announcement = Dm.truthy(is_show_active) ? pick(tv_starters) : pick(tv_enders);
        set_network_broadcast_status(tv_network_id, is_show_active, announcement);
        return null;
    }
}
