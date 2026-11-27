// Translated from tgstation (snapshot 1d99bd7f): code/modules/explorer_drone/exploration_events/_exploration_event.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: what a drone meets at a site (its first discovery's log line) and the simple events' single screen with its act
// and ignore buttons. The var blocks are generated (D111).

/// Exploration event
// tg: code/modules/explorer_drone/exploration_events/_exploration_event.dm:2
public partial class datum_exploration_event : datum
{
    /// Main event functionality, called when exploring randomly/revisiting.
    // tg: code/modules/explorer_drone/exploration_events/_exploration_event.dm:25
    public virtual void encounter(obj_item_exodrone drone = null)
    {
        if (!Dm.truthy(visited))
        {
            object log = get_discovery_message(drone);
            if (Dm.truthy(log))
                drone.drone_log__proc(log);
        }
        visited = TRUE;
    }

    /// Override this if you need to modify discovery message
    // tg: code/modules/explorer_drone/exploration_events/_exploration_event.dm:34
    public virtual object get_discovery_message(obj_item_exodrone drone = null)
    {
        return discovery_log;
    }

    /// Should this event show up on site exploration list.
    // tg: code/modules/explorer_drone/exploration_events/_exploration_event.dm:38
    public virtual object is_targetable()
    {
        return FALSE;
    }
}

/// Simple events, not a full fledged adventure, consist only of single encounter screen
// tg: code/modules/explorer_drone/exploration_events/_exploration_event.dm:42
public partial class datum_exploration_event_simple : datum_exploration_event
{
    /// On exploration, only display our information with the act/ignore options
    // tg: code/modules/explorer_drone/exploration_events/_exploration_event.dm:55
    public override void encounter(obj_item_exodrone drone = null)
    {
        base.encounter(drone);
        drone.current_event_ui_data = build_ui_event(drone) as DmList;
    }

    /// After choosing not to ignore the event, THIS IS DONE AFTER UNKNOWN DELAY SO YOU NEED TO VALIDATE IF ACTION IS POSSIBLE AGAIN
    // tg: code/modules/explorer_drone/exploration_events/_exploration_event.dm:60
    public virtual async DmTask fire(obj_item_exodrone drone = null)
    {
        return;
    }

    /// Ends simple event and cleans up display data
    // tg: code/modules/explorer_drone/exploration_events/_exploration_event.dm:64
    public virtual void end(obj_item_exodrone drone = null)
    {
        drone.current_event_ui_data = null;
    }

    /// Description shown below image
    // tg: code/modules/explorer_drone/exploration_events/_exploration_event.dm:68
    public virtual object get_description(obj_item_exodrone drone = null)
    {
        return description;
    }

    /// Text on the act button
    // tg: code/modules/explorer_drone/exploration_events/_exploration_event.dm:72
    public virtual object get_action_text(obj_item_exodrone drone = null)
    {
        return action_text;
    }

    /// Button to act disabled or not
    // tg: code/modules/explorer_drone/exploration_events/_exploration_event.dm:76
    public virtual object action_enabled(obj_item_exodrone drone = null)
    {
        return TRUE;
    }

    /// Creates ui data for displaying the event
    // tg: code/modules/explorer_drone/exploration_events/_exploration_event.dm:80
    public virtual object build_ui_event(obj_item_exodrone drone = null)
    {
        DmList dot = Dm.list();
        dot["image"] = ui_image;
        dot["description"] = get_description(drone);
        dot["action_enabled"] = action_enabled(drone);
        dot["action_text"] = get_action_text(drone);
        dot["skippable"] = skippable;
        dot["ignore_text"] = ignore_text;
        dot["ref"] = REF(this);
        return dot;
    }
}
