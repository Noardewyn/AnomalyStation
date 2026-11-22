// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/nukeop/equipment/nuclear_bomb/beer_nuke.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1a, D316: Meta's beer nuke; N1c: its flood on the station, the every-vent scrubber overflow of beer, a
// round event). The var block is generated (D111).

/// A fake nuke that actually contains beer.
// tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/beer_nuke.dm:2
public partial class obj_machinery_nuclearbomb_beer : obj_machinery_nuclearbomb
{
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/beer_nuke.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        keg = Dm.@new<obj_structure_reagent_dispensers_keg_beer>(this);
        QDEL_NULL(ref core);
        overflow_control = Dm.locate<datum_round_event_control_scrubber_overflow_every_vent>(SSevents?.control);
        return dot;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/beer_nuke.dm:21
    public override object Destroy(float? force = null)
    {
        if (overflow_control != null)
            UnregisterSignal(overflow_control, COMSIG_CREATED_ROUND_EVENT);
        return base.Destroy(force);
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/beer_nuke.dm:25
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        float total_volume = ((datum_reagents)keg.reagents).total_volume;
        if (Dm.truthy(total_volume))
            dot.Add(span_notice(I18n.T("It has [] unit[] left.", total_volume, total_volume == 1 ? "" : "s")));
        else
            dot.Add(span_danger(I18n.T("It's empty.")));
        return dot;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/beer_nuke.dm:32
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(tool.is_refillable()))
            return await base.item_interaction(user, tool, modifiers, __extra);
        await tool.interact_with_atom(keg, user); // redirect refillable containers to the keg, allowing them to be filled
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/beer_nuke.dm:38
    public override async DmTask<object> actually_explode()
    {
        if (core != null)
            return await base.actually_explode();
        //Unblock roundend, we're not actually exploding.
        SSticker.roundend_check_paused = FALSE;
        var bomb_location = get_turf(this) as turf;
        if (bomb_location == null)
        {
            disarm_nuke();
            return null;
        }
        if (Dm.truthy(is_station_level(bomb_location.z)))
            _addtimer(Dm.@new<datum_callback>(this, nameof(really_actually_explode)), 11 * SECONDS,
                file: "code/modules/antagonists/nukeop/equipment/nuclear_bomb/beer_nuke.dm", line: 48);
        else
        {
            visible_message(span_notice(I18n.T("[] fizzes ominously.", this)));
            _addtimer(Dm.@new<datum_callback>(this, nameof(local_foam)), 11 * SECONDS,
                file: "code/modules/antagonists/nukeop/equipment/nuclear_bomb/beer_nuke.dm", line: 51);
        }
        return null;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/beer_nuke.dm:53
    public override void disarm_nuke(mob disarmer = null)
    {
        exploding = FALSE;
        exploded = TRUE;
        base.disarm_nuke(disarmer);
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/beer_nuke.dm:58
    public virtual async DmTask local_foam()
    {
        await do_foam(200, this, get_turf(this) as turf, flood_reagent, 100);
        disarm_nuke();
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/beer_nuke.dm:62
    public override async DmTask<object> really_actually_explode(object detonation_status = null)
    {
        if (core != null)
            return await base.really_actually_explode(detonation_status);
        //if it's always hooked in it'll override admin choices
        RegisterSignal(overflow_control, COMSIG_CREATED_ROUND_EVENT, nameof(on_created_round_event));
        disarm_nuke();
        await overflow_control.run_event(event_cause: "a beer nuke");
        return null;
    }

    /// signal sent from overflow control when it fires an event
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/beer_nuke.dm:71
    [SignalHandler]
    public virtual void on_created_round_event(datum_round_event_control source_event_control = null, datum_round_event_scrubber_overflow_every_vent created_event = null)
    {
        UnregisterSignal(overflow_control, COMSIG_CREATED_ROUND_EVENT);
        created_event.forced_reagent_type = flood_reagent as Type;
    }
}
