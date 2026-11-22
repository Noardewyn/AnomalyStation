// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/false_alarm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1b (D327): the whole file, the false alarm (another fakeable event's or a midround antagonist's announcement, nothing behind
// it), but its admin option, the admin's choice of the fake (/datum/event_admin_setup/listed_options/false_alarm: the admins' Trigger
// Event panel, D275). DEVIATION(DV380): a midround ruleset whose own procs are not ported (inert, DV346: it never runs) is no false
// alarm either, as the events not ported are left out of the pool. The var blocks are generated (D111).

// tg: code/modules/events/false_alarm.dm:1
public partial class datum_round_event_control_falsealarm : datum_round_event_control
{
    // tg: code/modules/events/false_alarm.dm:10
    public override object can_spawn_event(object players_amt = null, float? allow_magic = null)
    {
        object dot = base.can_spawn_event(players_amt, allow_magic);
        if (!Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(length(get_potential_false_alarm())))
            return FALSE;
        return TRUE;
    }
}

// tg: code/modules/events/false_alarm.dm:19
public partial class datum_round_event_falsealarm : datum_round_event
{
    // tg: code/modules/events/false_alarm.dm:26
    public override async DmTask announce__proc(object fake = null)
    {
        if (Dm.truthy(fake)) //What are you doing
            return;

        object players_amt = get_active_player_count(alive_check: TRUE, afk_check: TRUE, human_check: TRUE);
        object picked_trigger = forced_type;
        if (forced_type is Type forced && forced.IsSubclassOf(typeof(datum_dynamic_ruleset_midround)))
            picked_trigger = Dm.@new(forced);

        DmList event_pool = get_potential_false_alarm();

        while (Dm.truthy(length(event_pool)) && picked_trigger == null)
        {
            object potential_trigger = pick_n_take(event_pool);
            if (potential_trigger is datum_round_event_control event_control)
            {
                if (Dm.truthy(event_control.can_spawn_event(players_amt)))
                {
                    picked_trigger = event_control;
                    break;
                }
            }
            else if (potential_trigger is Type ruleset_type && ruleset_type.IsSubclassOf(typeof(datum_dynamic_ruleset_midround)))
            {
                var ruleset = Dm.@new(ruleset_type, SSdynamic.get_config()) as datum_dynamic_ruleset_midround;
                if (Dm.truthy(ruleset.can_be_selected()))
                {
                    picked_trigger = ruleset;
                    break;
                }
                qdel(ruleset);
            }
            else
                stack_trace(Dm.text("Unknown false alarm candidate type: []", potential_trigger ?? "null"), "code/modules/events/false_alarm.dm", 53);
        }

        if (picked_trigger is datum_round_event_control picked_control)
        {
            var fake_event = Dm.@new(picked_control.typepath) as datum_round_event;
            message_admins(Dm.text("False Alarm: []", fake_event));
            fake_event.kill(); //do not process this event - no starts, no ticks, no ends
            await fake_event.announce__proc(TRUE); //just announce it like it's happening
        }
        else if (picked_trigger is datum_dynamic_ruleset_midround picked_ruleset)
        {
            message_admins(Dm.text("False Alarm: []", picked_ruleset));
            await picked_ruleset.false_alarm();
            qdel(picked_ruleset);
        }
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/events/false_alarm.dm:68
    public static DmList get_potential_false_alarm()
    {
        DmList dot = Dm.list();
        foreach (datum_round_event_control controller in Dm.each_cast<datum_round_event_control>(SSevents.control))
        {
            if (controller is datum_round_event_control_falsealarm)
                continue;
            if (!Dm.truthy(Dm.initial(controller.typepath, (datum_round_event p) => p.fakeable)))
                continue;
            Dm.append(ref dot, controller);
        }

        foreach (Type midround in Dm.each_cast<Type>(subtypesof(typeof(datum_dynamic_ruleset_midround))))
        {
            if (!Dm.truthy(Dm.initial(midround, (datum_dynamic_ruleset_midround p) => p.false_alarm_able)))
                continue;
            if (InertSeam.IsInertType(midround)) // DEVIATION(DV380): see the file's header
                continue;
            Dm.append(ref dot, midround);
        }
        return dot;
    }
}
