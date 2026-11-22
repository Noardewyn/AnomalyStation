// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/traitor/contractor/syndicate_contract.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X1a (D316): a syndicate contract (its target, payout, dropoff and wanted message; the extraction pod, the
// victim's stay in the holding facility and the pod home), whole but the souvenir achievement; the ransom cut came with the economy
// (gaps P4a), the black market's early return with the markets (gaps P4d); not-translated.md section 138. The var block is generated
// (D111).

// tg: code/modules/antagonists/traitor/contractor/syndicate_contract.dm:1
public partial class datum_syndicate_contract : datum
{
    // tg: code/modules/antagonists/traitor/contractor/syndicate_contract.dm:21
    [DmParams("contract_owner", "blacklist", "type")]
    public override async DmTask New(params object[] __extra)
    {
        object contract_owner = Dm.arg(__extra, 0);
        object blacklist = Dm.arg(__extra, 1);
        object type = Dm.arg(__extra, 2) ?? CONTRACT_PAYOUT_SMALL;
        contract = await Dm.new_async(typeof(datum_objective_contract), this) as datum_objective_contract;
        contract.owner = contract_owner as datum_mind;
        payout_type = type;

        await generate(blacklist as DmList);
    }

    // tg: code/modules/antagonists/traitor/contractor/syndicate_contract.dm:28
    public virtual async DmTask generate(DmList blacklist = null)
    {
        await contract.find_target(null, blacklist);

        datum_record_crew record = null;
        if (contract.target != null)
            record = find_record(contract.target.name) as datum_record_crew;

        if (record != null)
            target_rank = record.rank;
        else
            target_rank = "Unknown";

        if (Dm.eq(payout_type, CONTRACT_PAYOUT_LARGE))
            contract.payout_bonus = rand(9, 13);
        else if (Dm.eq(payout_type, CONTRACT_PAYOUT_MEDIUM))
            contract.payout_bonus = rand(6, 8);
        else
            contract.payout_bonus = rand(2, 4);

        contract.payout = rand(0, 2);
        contract.generate_dropoff();

        ransom = 100 * rand(18, 45);

        object @base = pick_list(WANTED_FILE, "basemessage");
        object verb_string = pick_list(WANTED_FILE, "verb");
        object noun = pick_list_weighted(WANTED_FILE, "noun");
        object location = pick_list_weighted(WANTED_FILE, "location");
        wanted_message = Dm.text("[] [] [] [].", @base, verb_string, noun, location);
    }

    // tg: code/modules/antagonists/traitor/contractor/syndicate_contract.dm:58
    public virtual async DmTask<object> handle_extraction(mob_living user = null)
    {
        if (contract.target != null && Dm.truthy(contract.dropoff_check(user, contract.target.current)))
        {
            var free_location = await find_obstruction_free_location(3, user, contract.dropoff) as turf;

            if (free_location != null)
            {
                // We've got a valid location, launch.
                launch_extraction_pod(free_location);
                return TRUE;
            }
        }

        return FALSE;
    }

    // Launch the pod to collect our victim.
    // tg: code/modules/antagonists/traitor/contractor/syndicate_contract.dm:71
    public virtual void launch_extraction_pod(turf empty_pod_turf = null)
    {
        var empty_pod = Dm.@new<obj_structure_closet_supplypod_extractionpod>();

        RegisterSignal(empty_pod, COMSIG_ATOM_ENTERED, nameof(enter_check));

        empty_pod.stay_after_drop = TRUE;
        empty_pod.reversing = TRUE;
        empty_pod.explosionSize = Dm.list(0, 0, 0, 1);
        empty_pod.leavingSound = Dm.resource("sound/effects/podwoosh.ogg");

        Dm.@new<obj_effect_pod_landingzone>(empty_pod_turf, empty_pod);
    }

    // tg: code/modules/antagonists/traitor/contractor/syndicate_contract.dm:83
    [SignalHandler]
    public virtual void enter_check(datum source = null, object sent_mob = null)
    {
        if (source is not obj_structure_closet_supplypod_extractionpod pod)
            return;
        if (sent_mob is not mob_living person_sent)
            return;
        var traitor_data = contract.owner.has_antag_datum(typeof(datum_antagonist_traitor)) as datum_antagonist_traitor;
        datum_contractor_hub hub = traitor_data.uplink_handler.contractor_hub;
        if (person_sent == contract.target.current)
        {
            hub.contract_TC_to_redeem += contract.payout;
            hub.contracts_completed++;
            if (!Dm.eq(person_sent.stat, DEAD))
                hub.contract_TC_to_redeem += contract.payout_bonus;
            status = CONTRACT_STATUS_COMPLETE;
            if (hub.current_contract == this)
                hub.current_contract = null;
        }
        else
        {
            status = CONTRACT_STATUS_ABORTED; // Sending a target that wasn't even yours is as good as just aborting it
            if (hub.current_contract == this)
                hub.current_contract = null;
        }

        foreach (obj_item person_contents in Dm.each_cast<obj_item>(person_sent.gather_belongings(FALSE, FALSE)))
        {
            if (person_sent is mob_living_carbon_human human_sent)
            {
                if (person_contents == human_sent.w_uniform)
                    continue; //So all they're left with are shoes and uniform.
                if (person_contents == human_sent.shoes)
                    continue;
            }
            object unequipped = Waiting.NoSleep(person_sent.temporarilyRemoveItemFromInventory(person_contents));
            if (!Dm.truthy(unequipped))
                continue;
            Waiting.NoSleep(person_contents.moveToNullspace());
            Dm.append(ref victim_belongings, WEAKREF(person_contents));
        }

        // Handle the pod returning
        Waiting.NoSleep(pod.start_exit_sequence(pod));

        if (person_sent is mob_living_carbon_human target)
        {
            // After we remove items, at least give them what they need to live.
            Waiting.NoSleep(((datum_species)target.dna.species).give_important_for_life(target));
        }

        //we'll start the effects in a few seconds since it takes a moment for the pod to leave.
        _addtimer(Dm.@new<datum_callback>(this, nameof(handle_victim_experience), person_sent), 3 * SECONDS, file: "code/modules/antagonists/traitor/contractor/syndicate_contract.dm", line: 128);

        if (Waiting.NoSleep(person_sent.process_capture(ransom * (rand(11, 13) / 10f))) is datum datum_market_item)
            RegisterSignal(datum_market_item, COMSIG_MARKET_ITEM_SPAWNED, nameof(on_victim_shipped));

        _addtimer(Dm.@new<datum_callback>(this, nameof(finish_enter)), 3 * SECONDS, file: "code/modules/antagonists/traitor/contractor/syndicate_contract.dm", line: 133);
    }

    // tg: code/modules/antagonists/traitor/contractor/syndicate_contract.dm:135
    public virtual void finish_enter()
    {
        // Pay contractor their portion of ransom
        if (!Dm.eq(status, CONTRACT_STATUS_COMPLETE))
            return;
        var contractor_id = (contract.owner.current as mob_living)?.get_idcard(TRUE) as obj_item_card_id;
        if (contractor_id == null || contractor_id.registered_account == null)
            return;
        contractor_id.registered_account.adjust_money(Dm.num(ransom) * 0.35f);
        Waiting.Detach(contractor_id.registered_account.bank_card_talk(I18n.T("We've processed the ransom, agent. Here's your cut - your balance is now [] [].", contractor_id.registered_account.account_balance, MONEY_SYMBOL), TRUE));
    }

    // tg: code/modules/antagonists/traitor/contractor/syndicate_contract.dm:146-150  #define VICTIM_EXPERIENCE_START 0 ... (file-local, #undef at :207-211)
    private const int VICTIM_EXPERIENCE_START = 0;
    private const int VICTIM_EXPERIENCE_FIRST_HIT = 1;
    private const int VICTIM_EXPERIENCE_SECOND_HIT = 2;
    private const int VICTIM_EXPERIENCE_THIRD_HIT = 3;
    private const int VICTIM_EXPERIENCE_LAST_HIT = 4;

    /**
     * handle_victim_experience
     *
     * Handles the effects given to victims upon being contracted.
     * We heal them up and cause them immersive effects, just for fun.
     * Args:
     * victim - The person we're harassing
     * level - The current stage of harassement they are facing. This increases by itself, looping until finished.
     */
    // tg: code/modules/antagonists/traitor/contractor/syndicate_contract.dm:161
    public virtual async DmTask handle_victim_experience(mob_living victim = null, float level = VICTIM_EXPERIENCE_START)
    {
        // Ship 'em back - dead or alive
        // Even if they weren't the target, we're still treating them the same.
        if (level == 0)
            victim_timerid = _addtimer(Dm.@new<datum_callback>(this, nameof(return_victim), victim), COME_BACK_FROM_CAPTURE_TIME, TIMER_STOPPABLE, file: "code/modules/antagonists/traitor/contractor/syndicate_contract.dm", line: 165);
        if (Dm.eq(victim.stat, DEAD))
            return;

        float time_until_next = 0;
        switch ((int)level)
        {
            case VICTIM_EXPERIENCE_START:
                // Heal them up - gets them out of crit/soft crit. If omnizine is removed in the future, this needs to be replaced with a
                // method of healing them, consequence free, to a reasonable amount of health.
                await ((datum_reagents)victim.reagents).add_reagent(typeof(datum_reagent_medicine_omnizine), amount: 20);
                await victim.flash_act();
                await victim.adjust_confusion(1 * SECONDS);
                await victim.adjust_eye_blur(5 * SECONDS);
                to_chat(victim, span_warning(I18n.T("You feel strange...")));
                time_until_next = 6 * SECONDS;
                break;
            case VICTIM_EXPERIENCE_FIRST_HIT:
                to_chat(victim, span_warning(I18n.T("That pod did something to you...")));
                await victim.adjust_dizzy(3.5f * SECONDS);
                time_until_next = 6.5f * SECONDS;
                break;
            case VICTIM_EXPERIENCE_SECOND_HIT:
                to_chat(victim, span_warning(I18n.T("Your head pounds... It feels like it's going to burst out your skull!")));
                await victim.flash_act();
                await victim.adjust_confusion(2 * SECONDS);
                await victim.adjust_eye_blur(3 * SECONDS);
                time_until_next = 3 * SECONDS;
                break;
            case VICTIM_EXPERIENCE_THIRD_HIT:
                to_chat(victim, span_warning(I18n.T("Your head pounds...")));
                time_until_next = 10 * SECONDS;
                break;
            case VICTIM_EXPERIENCE_LAST_HIT:
                await victim.flash_act();
                await victim.Unconscious(200);
                to_chat(victim, span_hypnophrase(I18n.T("A million voices echo in your head... <i>\"Your mind held many valuable secrets - we thank you for providing them. Your value is expended, and you will be ransomed back to your station. We always get paid, so it's only a matter of time before we ship you back...\"</i>")));
                await victim.adjust_eye_blur(10 * SECONDS);
                await victim.adjust_dizzy(1.5f * SECONDS);
                await victim.adjust_confusion(2 * SECONDS);
                break;
        }

        level++; //move onto the next level.
        if (time_until_next != 0)
            victim_timerid = _addtimer(Dm.@new<datum_callback>(this, nameof(handle_victim_experience), victim, level), time_until_next, TIMER_STOPPABLE, file: "code/modules/antagonists/traitor/contractor/syndicate_contract.dm", line: 205);
    }

    /// We're returning the victim to the station
    // tg: code/modules/antagonists/traitor/contractor/syndicate_contract.dm:214
    public virtual async DmTask return_victim(mob_living victim = null)
    {
        DmList possible_drop_loc = Dm.list();
        foreach (turf possible_drop in Dm.each<turf>(shuffle(contract.dropoff.contents)))
        {
            if (!isspaceturf(possible_drop) && !isclosedturf(possible_drop))
            {
                if (!Dm.truthy(await possible_drop.is_blocked_turf()))
                    Dm.append(ref possible_drop_loc, possible_drop);
            }
        }

        turf destination = null;
        if (length(possible_drop_loc) > 0)
            destination = pick(possible_drop_loc) as turf;

        var return_pod = Dm.@new<obj_structure_closet_supplypod_back_to_station>();
        await return_pod.return_from_capture(victim, destination);
        await returnal_side_effects(return_pod, victim);
    }

    ///Called if the victim is being returned to the station early, when from the black market.
    // tg: code/modules/antagonists/traitor/contractor/syndicate_contract.dm:230
    [SignalHandler]
    public virtual void on_victim_shipped(datum source = null, obj_item uplink = null, object shipping_method = null, turf shipping_loc = null)
    {
        deltimer(victim_timerid);
        Waiting.Detach(returnal_side_effects(shipping_loc, ((datum_market_item)source).item as mob_living));
    }

    ///The annoying negative effects applied to the victim when returned to the station.
    // tg: code/modules/antagonists/traitor/contractor/syndicate_contract.dm:236
    public virtual async DmTask returnal_side_effects(atom dropoff_location = null, mob_living victim = null)
    {
        foreach (datum_weakref belonging_ref in Dm.each_cast<datum_weakref>(victim_belongings))
        {
            if (belonging_ref.resolve() is not obj_item belonging)
                continue;
            if (victim is mob_living_carbon_human human_victim)
            {
                //So all they're left with are shoes and uniform.
                if (belonging == human_victim.w_uniform)
                    continue;
                if (belonging == human_victim.shoes)
                    continue;
            }
            await belonging.forceMove(dropoff_location);
        }

        foreach (obj_item item in Dm.each<obj_item>(victim_belongings))
            await item.forceMove(dropoff_location);

        if (victim is mob_living_carbon_human souvenir_victim)
        {
            if (souvenir_victim.w_uniform is obj_item_clothing_under_misc_syndicate_souvenir)
                Dm.Deferred("code/modules/antagonists/traitor/contractor/syndicate_contract.dm:256", "returnal_side_effects: victim.client.give_award(/datum/award/achievement/misc/souvenir, victim) (the achievements)");
        }

        await victim.flash_act();
        await victim.adjust_eye_blur(3 * SECONDS);
        await victim.adjust_dizzy(3.5f * SECONDS);
        await victim.adjust_confusion(2 * SECONDS);
    }
}
