// Translated from tgstation (snapshot 1d99bd7f): code/modules/explorer_drone/exploration_events/danger.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the dangers a drone meets, which damage it unless it carries the tool that gets it out. The var blocks are
// generated (D111).

/// Danger event - unskippable, if you have appriopriate tool you can mitigate damage.
// tg: code/modules/explorer_drone/exploration_events/danger.dm:2
public partial class datum_exploration_event_simple_danger : datum_exploration_event_simple
{
    // tg: code/modules/explorer_drone/exploration_events/danger.dm:14
    public override object get_description(obj_item_exodrone drone = null)
    {
        object dot = base.get_description(drone);
        DmList desc_parts = Dm.list(dot);
        Dm.append(ref desc_parts, Dm.truthy(can_escape_danger(drone)) ? has_tool_description : no_tool_description);
        return desc_parts.Join("\n");
    }

    // tg: code/modules/explorer_drone/exploration_events/danger.dm:20
    public override object get_action_text(obj_item_exodrone drone = null)
    {
        return Dm.truthy(can_escape_danger(drone)) ? has_tool_action_text : no_tool_action_text;
    }

    // tg: code/modules/explorer_drone/exploration_events/danger.dm:23
    public virtual object can_escape_danger(obj_item_exodrone drone = null)
    {
        return Dm.num(!Dm.truthy(required_tool) || Dm.truthy(drone.has_tool(required_tool)));
    }

    // tg: code/modules/explorer_drone/exploration_events/danger.dm:26
    public override async DmTask fire(obj_item_exodrone drone = null)
    {
        if (Dm.truthy(can_escape_danger(drone)))
            drone.drone_log__proc(avoid_log);
        else
            await drone.damage(damage);
        end(drone);
    }
}

// tg: code/modules/explorer_drone/exploration_events/danger.dm:109
public partial class datum_exploration_event_simple_danger_beast : datum_exploration_event_simple_danger
{
    // tg: code/modules/explorer_drone/exploration_events/danger.dm:122
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        object beast_name = pick_list(EXODRONE_FILE, "alien_fauna");
        description = replacetext(description, "BEAST", beast_name);
        avoid_log = replacetext(avoid_log, "BEAST", beast_name);
    }
}
