// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/brand_intelligence.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1b (D327): the whole file, the brand intelligence (a station vendor, patient zero, starts throwing its stock and infects the
// others one by one, some refusing the poor; silence patient zero's speaker or cut its wires to end it, or every vendor rises up
// against the crew), but its admin option, the admin's choice of the vendor type (/datum/event_admin_setup/listed_options/
// brand_intelligence: the admins' Trigger Event panel, D275). The var blocks are generated (D111).

// tg: code/modules/events/brand_intelligence.dm:13
public partial class datum_round_event_brand_intelligence : datum_round_event
{
    // tg: code/modules/events/brand_intelligence.dm:35
    public override async DmTask<object> setup()
    {
        //select our origin machine (which will also be the type of vending machine affected.)
        foreach (obj_machinery_vending vendor in Dm.each<obj_machinery_vending>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_vending))))
        {
            if (!Dm.truthy(vendor.onstation))
                continue;
            if (!Dm.truthy(vendor.density))
                continue;
            if (chosen_vendor_type is Type chosen_type && !istype(vendor, chosen_type))
                continue;
            Dm.append(ref vending_machines, vendor);
            RegisterSignal(vendor, COMSIG_QDELETING, nameof(clear_from_lists));
        }
        if (!Dm.truthy(length(vending_machines))) //If somehow there are still no elligible vendors, give up.
        {
            kill();
            return null;
        }
        origin_machine = pick_n_take(vending_machines) as obj_machinery_vending;
        return null;
    }

    // tg: code/modules/events/brand_intelligence.dm:51
    public override async DmTask announce__proc(object fake = null)
    {
        object machine_name = Dm.initial(origin_machine, (obj_machinery_vending p) => p.name);
        if (Dm.truthy(fake))
        {
            var prototype = (Type)pick(subtypesof(typeof(obj_machinery_vending)));
            machine_name = Dm.initial(prototype, (obj_machinery_vending p) => p.name);
        }
        priority_announce(Dm.text("Rampant brand intelligence has been detected aboard []. Please inspect any [] brand vendors for aggressive marketing tactics, and reboot them if necessary.", station_name(), machine_name), "Machine Learning Alert");
    }

    // tg: code/modules/events/brand_intelligence.dm:58
    public override async DmTask<object> start()
    {
        origin_machine.shut_up = FALSE;
        origin_machine.shoot_inventory = TRUE;
        announce_to_ghosts(origin_machine);
        return null;
    }

    // tg: code/modules/events/brand_intelligence.dm:63
    public override async DmTask tick()
    {
        if (QDELETED(origin_machine) || Dm.truthy(origin_machine.shut_up) || Dm.truthy(origin_machine.wires.is_all_cut())) //if the original vending machine is missing or has its voice switch flipped
        {
            foreach (obj_machinery_vending saved in Dm.each_cast<obj_machinery_vending>(infected_machines.Copy()))
            {
                saved.shoot_inventory = FALSE;
                clear_from_lists(saved);
            }
            if (!QDELETED(origin_machine))
            {
                await origin_machine.speak(I18n.T("I am... vanquished. My people will remem...ber...meeee."));
                origin_machine.visible_message(span_notice(I18n.T("[] beeps and seems lifeless.", origin_machine)));
                clear_from_lists(origin_machine);
            }
            kill();
            return;
        }
        if (!Dm.truthy(length(vending_machines))) //if every machine is infected
        {
            foreach (obj_machinery_vending upriser in Dm.each_cast<obj_machinery_vending>(infected_machines))
                upriser.ai_controller = Dm.@new<datum_ai_controller_vending_machine_eventspawn>(upriser);
            kill();
            return;
        }
        if (activeFor % 2 == 0) // ISMULTIPLE(activeFor, 2)
        {
            var rebel = pick(vending_machines) as obj_machinery_vending;
            Dm.remove(ref vending_machines, rebel);
            Dm.append(ref infected_machines, rebel);
            rebel.shut_up = FALSE;
            rebel.shoot_inventory = TRUE;
            if (prob(50))
                RegisterSignal(rebel, COMSIG_VENDING_UI_INTERACT, nameof(deny_vending_interact));

            if (activeFor % 4 == 0) // ISMULTIPLE(activeFor, 4)
                await origin_machine.speak(pick(rampant_speeches));
        }
    }

    // tg: code/modules/events/brand_intelligence.dm:91
    public override void kill()
    {
        base.kill();
        foreach (obj_machinery_vending leftover in Dm.each_cast<obj_machinery_vending>(Dm.add(vending_machines, infected_machines)))
            clear_from_lists(leftover);
    }

    // tg: code/modules/events/brand_intelligence.dm:96
    [SignalHandler]
    public virtual void clear_from_lists(obj_machinery_vending vending_machine = null)
    {
        Dm.remove(ref vending_machines, vending_machine);
        Dm.remove(ref infected_machines, vending_machine);
        if (Dm.eq(vending_machines, origin_machine)) // tg compares the list with the machine, which never matches
            origin_machine = null;
        UnregisterSignal(vending_machine, COMSIG_QDELETING);
        UnregisterSignal(vending_machine, COMSIG_VENDING_UI_INTERACT);
    }

    // tg: code/modules/events/brand_intelligence.dm:105
    [SignalHandler]
    public virtual object deny_vending_interact(obj_machinery_vending vending_machine = null, mob user = null, datum_tgui ui = null)
    {
        // don't block usage if the ui is already open
        // primarily to stop insta-denying people who pass the threshold -> buy something -> drop out of threshold
        if (ui != null)
            return NONE;

        float cash = 0;
        if (isliving(user))
        {
            var living_user = (mob_living)user;
            cash += Dm.num(living_user.tally_physical_credits());
            var card = living_user.get_idcard(TRUE) as obj_item_card_id;
            cash += card?.registered_account?.account_balance ?? 0;
        }

        if (cash >= PAYCHECK_COMMAND * 10)
            return NONE;

        Waiting.NoSleep(vending_machine.speak(pick(
            I18n.T("Come back when you're a little... richer!"),
            I18n.T("Don't you have any money?"),
            I18n.T("You look poor, get a better job!"),
            I18n.T("You should've gone to college!"))));
        return VENDING_DENIED;
    }
}
