// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/shuttle_loan/shuttle_loan_event.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E3 (D327): the whole file but its admin option (the admin's pick of the loan offer, /datum/event_admin_setup/listed_options/
// shuttle_loan: the admins' Trigger Event panel, D275): the shuttle loan (Central Command asks for the cargo shuttle; cargo accepts at the
// supply console, the budget is paid, the shuttle comes back from CentCom five minutes later with the loan's cargo, creatures or threat).
// The var blocks are generated (D111).

/// Gives the choice to "loan" the shuttle to central command, giving a big delay on its return to the station in exchange for money and loot/threats in the cargo hold. Only one can be available at a time.
// tg: code/modules/events/shuttle_loan/shuttle_loan_event.dm:2
public partial class datum_round_event_control_shuttle_loan : datum_round_event_control
{
    // tg: code/modules/events/shuttle_loan/shuttle_loan_event.dm:14
    public override object can_spawn_event(object players_amt = null, float? allow_magic = null)
    {
        object dot = base.can_spawn_event(players_amt, allow_magic);
        foreach (datum_round_event running_event in Dm.each<datum_round_event>(SSevents.running))
        {
            if (running_event is datum_round_event_shuttle_loan) //Make sure two of these don't happen at once.
                return FALSE;
        }
        return dot;
    }
}

// tg: code/modules/events/shuttle_loan/shuttle_loan_event.dm:20
public partial class datum_round_event_shuttle_loan : datum_round_event
{
    // tg: code/modules/events/shuttle_loan/shuttle_loan_event.dm:30
    public override async DmTask<object> setup()
    {
        datum_round_event_control_shuttle_loan loan_control = control as datum_round_event_control_shuttle_loan;
        //by this point if situation is admin picked, it is a type, not an instance.
        // tg keeps the picked typepath in `situation` until it makes the datum; the port's var is typed (the generated var block), so the
        // path waits in a local (the admin's pick, which tg stores there, is not ported: D275)
        Type situation_path = null;
        if (situation == null)
        {
            DmList valid_situations = Dm.sub(subtypesof(typeof(datum_shuttle_loan_situation)), loan_control.unavailable_situations) as DmList;
            if (!Dm.truthy(valid_situations.len))
            {
                //If we somehow run out of loans (fking campbell), they all become available again
                loan_control.unavailable_situations.Cut();
                valid_situations = subtypesof(typeof(datum_shuttle_loan_situation));
            }
            situation_path = (Type)pick(valid_situations);
        }

        loan_control.unavailable_situations.Add(situation_path);
        situation = (datum_shuttle_loan_situation)await Dm.new_async(situation_path);
        return null;
    }

    // tg: code/modules/events/shuttle_loan/shuttle_loan_event.dm:44
    public override async DmTask announce__proc(object fake = null)
    {
        if (Dm.truthy(fake))
        {
            Type fake_situation = (Type)pick(subtypesof(typeof(datum_shuttle_loan_situation)));
            situation = (datum_shuttle_loan_situation)await Dm.new_async(fake_situation);
        }
        else
        {
            SSshuttle.shuttle_loan = this;
        }
        priority_announce(Dm.text("Cargo: []", situation.announcement_text), situation.sender);
        if (Dm.truthy(fake))
            qdel(situation);
    }

    ///Triggered when accepting the shuttle loan. Gives payment and delays shuttle. Ensures the event won't be deleted from event controller until after the cargo arrives at the station.
    // tg: code/modules/events/shuttle_loan/shuttle_loan_event.dm:55
    public virtual async DmTask loan_shuttle()
    {
        priority_announce(situation.thanks_msg, Dm.text("Cargo shuttle commandeered by [].", command_name()));

        dispatched = TRUE;
        datum_bank_account dep_account = SSeconomy.get_dep_account(ACCOUNT_CAR);
        dep_account?.adjust_money(situation.bonus_points);
        end_when = activeFor + 1;

        SSshuttle.supply.mode = SHUTTLE_CALL;
        SSshuttle.supply.destination = SSshuttle.getDock("cargo_home") as obj_docking_port_stationary;
        SSshuttle.supply.setTimer(delay_time);
        SSshuttle.centcom_message += situation.shuttle_transit_text;

        log_game(Dm.text("Shuttle loan event firing with type '[]'.", situation.logging_desc));
    }

    // tg: code/modules/events/shuttle_loan/shuttle_loan_event.dm:70
    public override async DmTask tick()
    {
        if (Dm.truthy(dispatched))
        {
            if (!Dm.eq(SSshuttle.supply.mode, SHUTTLE_IDLE))
                end_when = activeFor;
            else
                end_when = activeFor + 1;
        }
    }

    // tg: code/modules/events/shuttle_loan/shuttle_loan_event.dm:77
    public override async DmTask end()
    {
        if (SSshuttle.shuttle_loan == null)
            return;
        if (!Dm.truthy(SSshuttle.shuttle_loan.dispatched)) //Haven't dispatched in time? Too bad. Clean it up and move on without spawning anything.
        {
            SSshuttle.shuttle_loan = null;
            return;
        }

        SSshuttle.shuttle_loan = null;

        //get empty turfs
        DmList empty_shuttle_turfs = Dm.list();
        DmList blocked_shutte_turfs = Dm.list();
        DmList shuttle_areas = SSshuttle.supply.shuttle_areas;
        foreach (area_shuttle shuttle_area in Dm.each_cast<area_shuttle>(shuttle_areas))
        {
            foreach (turf_open_floor shuttle_turf in Dm.each<turf_open_floor>(shuttle_area.get_turfs_from_all_zlevels()))
            {
                if (Dm.truthy(await shuttle_turf.is_blocked_turf()))
                {
                    Dm.append(ref blocked_shutte_turfs, shuttle_turf);
                    continue;
                }
                Dm.append(ref empty_shuttle_turfs, shuttle_turf);
            }
        }
        if (!Dm.truthy(empty_shuttle_turfs.len))
            return;

        //let the situation spawn its items
        DmList spawn_list = Dm.list();
        await situation.spawn_items(spawn_list, empty_shuttle_turfs, blocked_shutte_turfs);

        float false_positive = 0;
        while (Dm.truthy(spawn_list.len) && Dm.truthy(empty_shuttle_turfs.len))
        {
            turf spawn_turf = pick_n_take(empty_shuttle_turfs) as turf;
            if (Dm.truthy(spawn_turf.contents.len) && false_positive < 5)
            {
                false_positive++;
                continue;
            }
            object spawn_type = pick_n_take(spawn_list);
            await Dm.new_async((Type)spawn_type, spawn_turf);
        }
    }
}
