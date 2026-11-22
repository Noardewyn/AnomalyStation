// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/shuttle_insurance.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E3 (D327): the whole file, the shuttle insurance (a rogue ship's offer at every communications console: buying it from the cargo
// budget makes a later shuttle catastrophe pay the budget instead of replacing the shuttle). The var blocks are generated (D111).

// tg: code/modules/events/shuttle_insurance.dm:3
public partial class datum_round_event_control_shuttle_insurance : datum_round_event_control
{
    // tg: code/modules/events/shuttle_insurance.dm:10
    public override object can_spawn_event(object players_amt = null, float? allow_magic = null)
    {
        object dot = base.can_spawn_event(players_amt, allow_magic);
        if (!Dm.truthy(dot))
            return dot;

        if (SSeconomy.get_dep_account(ACCOUNT_CAR) == null)
            return FALSE; //They can't pay?
        if (Dm.eq(SSshuttle.shuttle_purchased, SHUTTLEPURCHASE_FORCED))
            return FALSE; //don't do it if there's nothing to insure
        if (SSshuttle.emergency is obj_docking_port_mobile_emergency_shuttle_build)
            return FALSE; //this shuttle prevents the catastrophe event from happening making this event effectively useless
        if (datum_controller_subsystem_shuttle.EMERGENCY_AT_LEAST_DOCKED())
            return FALSE; //catastrophes won't trigger so no point
        return TRUE;
    }
}

// tg: code/modules/events/shuttle_insurance.dm:25
public partial class datum_round_event_shuttle_insurance : datum_round_event
{
    // tg: code/modules/events/shuttle_insurance.dm:30
    public override async DmTask announce__proc(object fake = null)
    {
        priority_announce("Incoming subspace communication. Secure channel opened at all communication consoles.", "Incoming Message", (SSstation.announcer as datum_centcom_announcer)?.get_rand_report_sound());
    }

    // tg: code/modules/events/shuttle_insurance.dm:33
    public override async DmTask<object> setup()
    {
        ship_name = Dm.str(pick(strings(PIRATE_NAMES_FILE, "rogue_names")));
        foreach (object shuttle_id in Dm.each(SSmapping.shuttle_templates))
        {
            datum_map_template_shuttle template = SSmapping.shuttle_templates[shuttle_id] as datum_map_template_shuttle;
            if (Dm.eq(template.name, SSshuttle.emergency.name)) //found you slackin
            {
                insurance_evaluation = template.credit_cost / 2;
                break;
            }
        }
        if (!Dm.truthy(insurance_evaluation))
            insurance_evaluation = 5000; //gee i dunno
        return null;
    }

    // tg: code/modules/events/shuttle_insurance.dm:43
    public override async DmTask<object> start()
    {
        insurance_message = (datum_comm_message)await Dm.new_async(typeof(datum_comm_message), "Shuttle Insurance", Dm.text("Hey, pal, this is the []. Can't help but notice you're rocking a wild and crazy shuttle there with NO INSURANCE! Crazy. What if something happened to it, huh?! We've done a quick evaluation on your rates in this sector and we're offering [] to cover for your shuttle in case of any disaster.", ship_name, insurance_evaluation), Dm.list("Purchase Insurance.", "Reject Offer."));
        insurance_message.answer_callback = Dm.@new<datum_callback>(this, nameof(answered));
        GLOB.communications_controller.send_message(insurance_message, unique: TRUE);
        return null;
    }

    // tg: code/modules/events/shuttle_insurance.dm:48
    public virtual async DmTask answered()
    {
        if (datum_controller_subsystem_shuttle.EMERGENCY_AT_LEAST_DOCKED())
        {
            priority_announce("You are definitely too late to purchase insurance, my friends. Our agents don't work on site.", sender_override: ship_name, color_override: "red");
            return;
        }
        if (insurance_message != null && Dm.eq(insurance_message.answered, 1))
        {
            datum_bank_account station_balance = SSeconomy.get_dep_account(ACCOUNT_CAR);
            if (!Dm.truthy(station_balance?.adjust_money(-insurance_evaluation)))
            {
                priority_announce("You didn't send us enough money for shuttle insurance. This, in the space layman's terms, is considered scamming. We're keeping your money, scammers!", sender_override: ship_name, color_override: "red");
                return;
            }
            priority_announce("Thank you for purchasing shuttle insurance!", sender_override: ship_name, color_override: "red");
            SSshuttle.shuttle_insurance = TRUE;
        }
    }
}
