// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/withdrawal/_addiction.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps RG2, D339): the addiction singletons (GLOB.addictions), gaining and losing an addiction from a mind's
// addiction points, and the withdrawal's three stages. The withdrawal moodlets are live since gaps MS1. The var blocks
// are generated (D111).

// tg: code/modules/reagents/withdrawal/_addiction.dm:1  GLOBAL_LIST_INIT_TYPED(addictions, /datum/addiction, init_subtypes_w_path_keys(/datum/addiction))
public partial class datum_controller_global_vars
{
    public DmList addictions;

    // tg: code/modules/reagents/withdrawal/_addiction.dm:1  (GLOBAL_MANAGED, __DEFINES/__globals.dm:5)
    public virtual void InitGlobaladdictions()
    {
        addictions = Waiting.NoSleep(init_subtypes_w_path_keys(typeof(datum_addiction))) as DmList;
        Dm.append(ref gvars_datum_init_order, nameof(addictions));
    }
}

///base class for addiction, handles when you become addicted and what the effects of that are. By default you become addicted when you hit a certain threshold, and stop being addicted once you go below another one.
// tg: code/modules/reagents/withdrawal/_addiction.dm:6
public partial class datum_addiction : datum
{
    ///Called when you gain addiction points somehow. Takes a mind as argument and sees if you gained the addiction
    // tg: code/modules/reagents/withdrawal/_addiction.dm:31
    [ShouldNotOverride]
    public virtual void on_gain_addiction_points(datum_mind victim_mind = null, float? new_amount = null, float? last_amount = null)
    {
        new_amount ??= 0;
        last_amount ??= 0;
        if (new_amount < addiction_gain_threshold) // Not enough to become addicted
            return;
        if (last_amount >= addiction_gain_threshold) // Already addicted
            return;
        become_addicted(victim_mind);
    }

    ///Called when you become addicted
    // tg: code/modules/reagents/withdrawal/_addiction.dm:40
    public virtual void become_addicted(datum_mind victim_mind = null)
    {
        LAZYSET(ref victim_mind.active_addictions, type, 1); //Start at first cycle.
        victim_mind.current.SEND_SIGNAL(COMSIG_CARBON_GAIN_ADDICTION, Dm.list(victim_mind.current, victim_mind));
        victim_mind.current.log_message(Dm.text("has become addicted to [].", name), LOG_GAME);
    }

    ///Called when you lose addiction poitns somehow. Takes a mind as argument and sees if you lost the addiction
    // tg: code/modules/reagents/withdrawal/_addiction.dm:46
    [ShouldNotOverride]
    public virtual object on_lose_addiction_points(datum_mind victim_mind = null, float? new_amount = null, float? last_amount = null)
    {
        new_amount ??= 0;
        last_amount ??= 0;
        if (last_amount < addiction_loss_threshold) // Was not addicted last check
            return FALSE;
        if (new_amount >= addiction_loss_threshold) // Is still addicted
            return FALSE;
        lose_addiction(victim_mind);
        return TRUE;
    }

    // tg: code/modules/reagents/withdrawal/_addiction.dm:55
    public virtual void lose_addiction(datum_mind victim_mind = null)
    {
        ((mob_living)victim_mind.current).clear_mood_event(Dm.text("[]_addiction", type));
        victim_mind.current.SEND_SIGNAL(COMSIG_CARBON_LOSE_ADDICTION, Dm.list(victim_mind.current, victim_mind));
        to_chat(victim_mind.current, span_notice(I18n.T("You feel like you've gotten over your need for drugs.")));
        end_withdrawal(victim_mind.current as mob_living_carbon);
        LAZYREMOVE(ref victim_mind.active_addictions, type);
    }

    // tg: code/modules/reagents/withdrawal/_addiction.dm:62
    public virtual async DmTask<object> process_addiction(mob_living_carbon affected_carbon = null, object seconds_per_tick = null)
    {
        // Acts as if you're on the drug at all times, while also forcibly preventing the effects of withdrawal by returning early.
        if (HAS_TRAIT(affected_carbon, TRAIT_NO_WITHDRAWALS))
        {
            end_withdrawal(affected_carbon);
            return FALSE;
        }

        object current_addiction_cycle = LAZYACCESS(affected_carbon.mind.active_addictions, type); //If this is null, we're not addicted
        float on_drug_of_this_addiction = FALSE;
        foreach (datum_reagent possible_drug in Dm.each<datum_reagent>(((datum_reagents)affected_carbon.reagents).reagent_list)) //Go through the drugs in our system
        {
            foreach (object addiction in Dm.each(possible_drug.addiction_types)) //And check all of their addiction types
            {
                if (Dm.eq(addiction, type) && Dm.num(possible_drug.volume) >= addiction_relief_treshold) //If one of them matches, and we have enough of it in our system, we're not losing addiction
                {
                    if (Dm.truthy(current_addiction_cycle))
                        end_withdrawal(affected_carbon); //stop the pain
                    on_drug_of_this_addiction = TRUE;
                    break;
                }
            }
        }

        float withdrawal_stage;

        float sw__1 = Dm.num(current_addiction_cycle);
        if (current_addiction_cycle != null && Dm.in_to(sw__1, WITHDRAWAL_STAGE1_START_CYCLE, WITHDRAWAL_STAGE1_END_CYCLE))
            withdrawal_stage = 1;
        else if (current_addiction_cycle != null && Dm.in_to(sw__1, WITHDRAWAL_STAGE2_START_CYCLE, WITHDRAWAL_STAGE2_END_CYCLE))
            withdrawal_stage = 2;
        else if (current_addiction_cycle != null && Dm.in_to(sw__1, WITHDRAWAL_STAGE3_START_CYCLE, INFINITY))
            withdrawal_stage = 3;
        else
            withdrawal_stage = 0;

        if (!Dm.truthy(on_drug_of_this_addiction) && !HAS_TRAIT(affected_carbon, TRAIT_HOPELESSLY_ADDICTED))
        {
            if (Dm.truthy(affected_carbon.mind.remove_addiction_points(type, Dm.num(addiction_loss_per_stage[withdrawal_stage + 1]) * Dm.num(seconds_per_tick)))) //If true was returned, we lost the addiction!
                return null;
        }

        if (!Dm.truthy(on_drug_of_this_addiction) && HAS_TRAIT(affected_carbon, TRAIT_ADDICTIONRESILIENT) && Dm.truthy(affected_carbon.mind.remove_addiction_points(type, Dm.num(addiction_loss_per_stage[withdrawal_stage + 2]) * Dm.num(seconds_per_tick))))
            return null;

        if (!Dm.truthy(current_addiction_cycle)) //Dont do the effects if were not on drugs
            return FALSE;

        float sw__2 = Dm.num(current_addiction_cycle);
        if (sw__2 == WITHDRAWAL_STAGE1_START_CYCLE)
            await withdrawal_enters_stage_1(affected_carbon);
        else if (sw__2 == WITHDRAWAL_STAGE2_START_CYCLE)
            await withdrawal_enters_stage_2(affected_carbon);
        else if (sw__2 == WITHDRAWAL_STAGE3_START_CYCLE)
            await withdrawal_enters_stage_3(affected_carbon);

        ///One cycle is 2 seconds
        if (withdrawal_stage == 1)
            await withdrawal_stage_1_process(affected_carbon, seconds_per_tick);
        else if (withdrawal_stage == 2)
            await withdrawal_stage_2_process(affected_carbon, seconds_per_tick);
        else if (withdrawal_stage == 3)
            await withdrawal_stage_3_process(affected_carbon, seconds_per_tick);

        LAZYADDASSOC(ref affected_carbon.mind.active_addictions, type, 1 * Dm.num(seconds_per_tick)); //Next cycle!
        return null;
    }

    /// Called when addiction enters stage 1
    // tg: code/modules/reagents/withdrawal/_addiction.dm:120
    public virtual async DmTask withdrawal_enters_stage_1(mob_living_carbon affected_carbon = null, params object[] __extra)
    {
        await affected_carbon.add_mood_event(Dm.text("[]_addiction", type), light_withdrawal_moodlet, name);
    }

    /// Called when addiction enters stage 2
    // tg: code/modules/reagents/withdrawal/_addiction.dm:124
    public virtual async DmTask withdrawal_enters_stage_2(mob_living_carbon affected_carbon = null)
    {
        await affected_carbon.add_mood_event(Dm.text("[]_addiction", type), medium_withdrawal_moodlet, name);
    }

    /// Called when addiction enters stage 3
    // tg: code/modules/reagents/withdrawal/_addiction.dm:128
    public virtual async DmTask withdrawal_enters_stage_3(mob_living_carbon affected_carbon = null)
    {
        await affected_carbon.add_mood_event(Dm.text("[]_addiction", type), severe_withdrawal_moodlet, name);
    }

    // tg: code/modules/reagents/withdrawal/_addiction.dm:131
    public virtual void end_withdrawal(mob_living_carbon affected_carbon = null)
    {
        LAZYSET(ref affected_carbon.mind.active_addictions, type, 1); //Keeps withdrawal at first cycle.
        affected_carbon.clear_mood_event(Dm.text("[]_addiction", type));
    }

    /// Called when addiction is in stage 1 every process
    // tg: code/modules/reagents/withdrawal/_addiction.dm:136
    public virtual async DmTask withdrawal_stage_1_process(mob_living_carbon affected_carbon = null, object seconds_per_tick = null)
    {
        if (SPT_PROB(5, seconds_per_tick))
            to_chat(affected_carbon, span_danger(I18n.T(Dm.str(withdrawal_stage_messages[1]))));
    }

    /// Called when addiction is in stage 2 every process
    // tg: code/modules/reagents/withdrawal/_addiction.dm:141
    public virtual async DmTask withdrawal_stage_2_process(mob_living_carbon affected_carbon = null, object seconds_per_tick = null)
    {
        if (SPT_PROB(10, seconds_per_tick))
            to_chat(affected_carbon, span_danger(I18n.T(Dm.str(withdrawal_stage_messages[2]))));
    }

    /// Called when addiction is in stage 3 every process
    // tg: code/modules/reagents/withdrawal/_addiction.dm:146
    public virtual async DmTask withdrawal_stage_3_process(mob_living_carbon affected_carbon = null, object seconds_per_tick = null)
    {
        if (SPT_PROB(15, seconds_per_tick))
            to_chat(affected_carbon, span_danger(I18n.T(Dm.str(withdrawal_stage_messages[3]))));
    }
}
