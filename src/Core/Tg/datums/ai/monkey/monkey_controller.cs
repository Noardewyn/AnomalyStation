// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/monkey/monkey_controller.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C5).

/*
AI controllers are a datumized form of AI that simulates the input a player would otherwise give to a mob. What this means is that these datums
have ways of interacting with a specific mob and control it.
*/
///OOK OOK OOK

// tg: code/datums/ai/monkey/monkey_controller.dm:29
public partial class datum_targeting_strategy_basic_monkey : datum_targeting_strategy_basic
{
    // tg: code/datums/ai/monkey/monkey_controller.dm:32
    public override object faction_check(datum_ai_controller controller = null, mob_living living_mob = null, mob_living the_target = null)
    {
        // if they wronged us, all bets are off
        if (Dm.truthy((controller.blackboard[BB_MONKEY_ENEMIES] as DmList)[the_target]))
            return FALSE;
        // target was forcibly set, all bets are off again
        if (Dm.eq(controller.blackboard[BB_CURRENT_TARGET], the_target))
            return FALSE;
        return base.faction_check(controller, living_mob, the_target);
    }
}

// tg: code/datums/ai/monkey/monkey_controller.dm:8
public partial class datum_ai_controller_monkey : datum_ai_controller
{
    // tg: code/datums/ai/monkey/monkey_controller.dm:41  var/static/list/control_examine (a proc-level static, rules 10.6)
    private static ref DmList New__control_examine => ref TgState.Current.datum_ai_controller_monkey__New__control_examine;

    // tg: code/datums/ai/monkey/monkey_controller.dm:40
    public override async DmTask New(params object[] __extra)
    {
        New__control_examine ??= Dm.list(
            Dm.kv(ORGAN_SLOT_EYES, span_monkey("%PRONOUN_They stare%PRONOUN_s around with wild, primal eyes.")));
        await AddElement(typeof(datum_element_ai_control_examine), New__control_examine);
        await base.New(__extra);
    }

    // tg: code/datums/ai/monkey/monkey_controller.dm:73
    public override async DmTask<object> TryPossessPawn(atom new_pawn = null)
    {
        if (!isliving(new_pawn))
            return AI_CONTROLLER_INCOMPATIBLE;

        mob_living living_pawn = new_pawn as mob_living;
        if (!HAS_TRAIT(living_pawn, TRAIT_RELAYING_ATTACKER))
            await living_pawn.AddElement(typeof(datum_element_relay_attackers));
        RegisterSignal(new_pawn, COMSIG_ATOM_WAS_ATTACKED, nameof(on_attacked));
        RegisterSignal(new_pawn, COMSIG_LIVING_START_PULL, nameof(on_startpulling));
        RegisterSignals(new_pawn, Dm.list(COMSIG_LIVING_TRY_SYRINGE_INJECT, COMSIG_LIVING_TRY_SYRINGE_WITHDRAW), nameof(on_try_syringe));
        RegisterSignal(new_pawn, COMSIG_CARBON_CUFF_ATTEMPTED, nameof(on_attempt_cuff));
        RegisterSignal(new_pawn, COMSIG_MOB_MOVESPEED_UPDATED, nameof(update_movespeed));

        movement_delay = Dm.num(living_pawn.cached_multiplicative_slowdown);
        return await base.TryPossessPawn(new_pawn); //Run parent at end
    }

    // tg: code/datums/ai/monkey/monkey_controller.dm:88
    public override async DmTask UnpossessPawn(object destroy = null)
    {
        UnregisterSignal(pawn, Dm.list(
            COMSIG_ATOM_WAS_ATTACKED,
            COMSIG_LIVING_START_PULL,
            COMSIG_LIVING_TRY_SYRINGE_INJECT,
            COMSIG_LIVING_TRY_SYRINGE_WITHDRAW,
            COMSIG_CARBON_CUFF_ATTEMPTED,
            COMSIG_MOB_MOVESPEED_UPDATED));

        await base.UnpossessPawn(destroy); //Run parent at end
    }

    // tg: code/datums/ai/monkey/monkey_controller.dm:101
    public override void on_sentience_lost()
    {
        base.on_sentience_lost();
        set_trip_mode(mode: TRUE);
    }

    // tg: code/datums/ai/monkey/monkey_controller.dm:105
    public override void on_stat_changed(mob_living source = null, object new_stat = null)
    {
        base.on_stat_changed(source, new_stat);
        update_able_to_run();
    }

    // tg: code/datums/ai/monkey/monkey_controller.dm:109
    public override void setup_able_to_run()
    {
        base.setup_able_to_run();
        RegisterSignal(pawn, COMSIG_MOB_INCAPACITATE_CHANGED, nameof(update_able_to_run));
    }

    // tg: code/datums/ai/monkey/monkey_controller.dm:113
    public override void clear_able_to_run()
    {
        UnregisterSignal(pawn, Dm.list(COMSIG_MOB_INCAPACITATE_CHANGED, COMSIG_MOB_STATCHANGE));
        base.clear_able_to_run();
    }

    // tg: code/datums/ai/monkey/monkey_controller.dm:117
    public override object get_able_to_run()
    {
        mob_living living_pawn = pawn as mob_living;

        if (Dm.truthy(INCAPACITATED_IGNORING(living_pawn, INCAPABLE_RESTRAINTS | INCAPABLE_STASIS | INCAPABLE_GRAB)))
            return AI_UNABLE_TO_RUN;
        return base.get_able_to_run();
    }

    // tg: code/datums/ai/monkey/monkey_controller.dm:124
    public virtual void set_trip_mode(float? mode = null)
    {
        mode ??= TRUE;
        mob_living regressed_monkey = pawn as mob_living;
        object brain = regressed_monkey.get_organ_slot(ORGAN_SLOT_BRAIN);
        if (brain is obj_item_organ_brain_primate monkeybrain) // In case we are a monkey AI in a human brain by who was previously controlled by a client but it now not by some marvel
            monkeybrain.tripping = mode.Value;
    }

    ///Reactive events to being hit
    // tg: code/datums/ai/monkey/monkey_controller.dm:131
    public virtual void retaliate(mob_living living_mob = null)
    {
        // just to be safe
        if (QDELETED(living_mob))
            return;

        add_blackboard_key_assoc(BB_MONKEY_ENEMIES, living_mob, MONKEY_HATRED_AMOUNT);
    }

    // tg: code/datums/ai/monkey/monkey_controller.dm:138
    public virtual void on_attacked(datum source = null, mob attacker = null)
    {
        if (prob(MONKEY_RETALIATE_PROB))
            retaliate(attacker as mob_living);
    }

    // tg: code/datums/ai/monkey/monkey_controller.dm:143
    public virtual object on_startpulling(datum source = null, atom_movable puller = null, object state = null, object force = null)
    {
        mob_living living_pawn = pawn as mob_living;
        if (!Dm.truthy(living_pawn.incapacitated) && prob(MONKEY_PULL_AGGRO_PROB)) // nuh uh you don't pull me!
        {
            retaliate(living_pawn.pulledby as mob_living);
            return TRUE;
        }
        return null;
    }

    // tg: code/datums/ai/monkey/monkey_controller.dm:150
    public virtual void on_try_syringe(datum source = null, mob user = null)
    {
        // chance of monkey retaliation
        if (prob(MONKEY_SYRINGE_RETALIATION_PROB))
            retaliate(user as mob_living);
    }

    // tg: code/datums/ai/monkey/monkey_controller.dm:156
    public virtual void on_attempt_cuff(datum source = null, mob user = null)
    {
        // chance of monkey retaliation
        if (prob(MONKEY_CUFF_RETALIATION_PROB))
            retaliate(user as mob_living);
    }

    // tg: code/datums/ai/monkey/monkey_controller.dm:162
    public virtual void update_movespeed(mob_living pawn = null)
    {
        movement_delay = Dm.num(pawn.cached_multiplicative_slowdown);
    }
}

// tg: code/datums/ai/monkey/monkey_controller.dm:47
public partial class datum_ai_controller_monkey_pun_pun : datum_ai_controller_monkey
{
    // tg: code/datums/ai/monkey/monkey_controller.dm:47
    public override async DmTask<object> TryPossessPawn(atom new_pawn = null)
    {
        object dot = await base.TryPossessPawn(new_pawn);
        if (Dm.truthy(Dm.bitand(dot, AI_CONTROLLER_INCOMPATIBLE)))
            return dot;
        pawn = new_pawn;
        set_blackboard_key(BB_IGNORE_DRINKS, TRUE);
        set_blackboard_key(BB_MONKEY_TAMED, TRUE);
        set_blackboard_key(BB_MONKEY_GIVE_CHANCE, 30);
        set_blackboard_key(BB_MONKEY_PRESS_TYPEPATH, typeof(obj_structure_desk_bell));
        override_blackboard_key(BB_MONKEY_IDLE_COMMON_EMOTES, Dm.list("tunesing", "dance", "bow"));
        override_blackboard_key(BB_MONKEY_IDLE_RARE_EMOTES, Dm.list("clear", "sign", "tail"));
        set_trip_mode(mode: FALSE);
        return dot;
    }
}

// tg: code/datums/ai/monkey/monkey_controller.dm:62
public partial class datum_ai_controller_monkey_angry : datum_ai_controller_monkey
{
    // tg: code/datums/ai/monkey/monkey_controller.dm:64
    public override async DmTask<object> TryPossessPawn(atom new_pawn = null)
    {
        object dot = await base.TryPossessPawn(new_pawn);
        if (Dm.truthy(Dm.bitand(dot, AI_CONTROLLER_INCOMPATIBLE)))
            return dot;
        pawn = new_pawn;
        set_blackboard_key(BB_MONKEY_AGGRESSIVE, TRUE); //Angry cunt
        set_trip_mode(mode: FALSE);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/datums/ai/monkey/monkey_controller.dm:41  var/static/list/control_examine
    internal DmList datum_ai_controller_monkey__New__control_examine;
}
