// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/base_basic_controller.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

// tg: code/datums/ai/basic_mobs/base_basic_controller.dm:1
public partial class datum_ai_controller_basic_controller : datum_ai_controller
{
    // tg: code/datums/ai/basic_mobs/base_basic_controller.dm:5
    public override async DmTask<object> TryPossessPawn(atom new_pawn = null)
    {
        if (!isliving(new_pawn))
            return AI_CONTROLLER_INCOMPATIBLE;
        mob_living basic_mob = (mob_living)new_pawn;

        update_speed(basic_mob);
        RegisterSignal(basic_mob, COMSIG_LIVING_BEFRIENDED, nameof(on_tamed));
        RegisterSignal(basic_mob, COMSIG_LIVING_UNFRIENDED, nameof(on_untamed));
        RegisterSignals(basic_mob, Dm.list(POST_BASIC_MOB_UPDATE_VARSPEED, COMSIG_MOB_MOVESPEED_UPDATED), nameof(update_speed));
        RegisterSignal(basic_mob, COMSIG_MOB_ATE, nameof(on_mob_eat));

        return await base.TryPossessPawn(new_pawn); //Run parent at end
    }

    // tg: code/datums/ai/basic_mobs/base_basic_controller.dm:18
    [SignalHandler]
    public override void on_stat_changed(mob_living source = null, object new_stat = null)
    {
        base.on_stat_changed(source, new_stat);
        update_able_to_run();
    }

    // tg: code/datums/ai/basic_mobs/base_basic_controller.dm:22
    public override void setup_able_to_run()
    {
        base.setup_able_to_run();
        RegisterSignal(pawn, COMSIG_MOB_INCAPACITATE_CHANGED, nameof(update_able_to_run));
        if (Dm.truthy(Dm.bitand(ai_traits, PAUSE_DURING_DO_AFTER)))
            RegisterSignals(pawn, Dm.list(COMSIG_DO_AFTER_BEGAN, COMSIG_DO_AFTER_ENDED), nameof(update_able_to_run));
    }

    // tg: code/datums/ai/basic_mobs/base_basic_controller.dm:29
    public override void clear_able_to_run()
    {
        UnregisterSignal(pawn, Dm.list(COMSIG_MOB_INCAPACITATE_CHANGED, COMSIG_MOB_STATCHANGE, COMSIG_DO_AFTER_BEGAN, COMSIG_DO_AFTER_ENDED));
        base.clear_able_to_run();
    }

    // tg: code/datums/ai/basic_mobs/base_basic_controller.dm:33
    public override object get_able_to_run()
    {
        object dot = base.get_able_to_run();
        if (Dm.truthy(Dm.bitand(dot, AI_UNABLE_TO_RUN)))
            return dot;
        mob_living living_pawn = pawn as mob_living;
        if (IS_UNCONSCIOUS_OR_CRIT(living_pawn) && !Dm.truthy(Dm.bitand(ai_traits, CAN_ACT_WHILE_DEAD)))
            return AI_UNABLE_TO_RUN;

        float ignore_incap_flags = NONE;
        if (Dm.truthy(Dm.bitand(ai_traits, CAN_ACT_IN_STASIS)))
            ignore_incap_flags = Dm.bitor(ignore_incap_flags, INCAPABLE_STASIS);
        if (Dm.truthy(Dm.bitand(ai_traits, CAN_ACT_WHILE_GRABBED)))
            ignore_incap_flags = Dm.bitor(ignore_incap_flags, INCAPABLE_GRAB);

        if (Dm.truthy(INCAPACITATED_IGNORING(living_pawn, ignore_incap_flags)))
            return AI_UNABLE_TO_RUN;

        if (Dm.truthy(Dm.bitand(ai_traits, PAUSE_DURING_DO_AFTER)) && Dm.truthy(LAZYLEN(living_pawn.do_afters)))
            return AI_UNABLE_TO_RUN | AI_PREVENT_CANCEL_ACTIONS; //dont erase targets post a do_after
        return dot;
    }

    // tg: code/datums/ai/basic_mobs/base_basic_controller.dm:54
    [SignalHandler]
    public virtual void update_speed(mob_living basic_mob = null)
    {
        movement_delay = Dm.num(basic_mob.cached_multiplicative_slowdown);
    }

    // tg: code/datums/ai/basic_mobs/base_basic_controller.dm:58
    [SignalHandler]
    public virtual void on_mob_eat()
    {
        float food_cooldown = Dm.truthy(blackboard[BB_EAT_FOOD_COOLDOWN]) ? Dm.num(blackboard[BB_EAT_FOOD_COOLDOWN]) : EAT_FOOD_COOLDOWN;
        set_blackboard_key(BB_NEXT_FOOD_EAT, world.time + food_cooldown);
    }
}

// tg: code/datums/ai/basic_mobs/base_basic_controller.dm:64 (procs of /datum/ai_controller)
public partial class datum_ai_controller : datum
{
    // tg: code/datums/ai/basic_mobs/base_basic_controller.dm:64
    [SignalHandler]
    public virtual void on_tamed(datum source = null, mob_living new_friend = null)
    {
        forgive_target(new_friend);
        clear_blackboard_key(BB_CURRENT_TARGET);
        clear_blackboard_key(BB_BASIC_MOB_RETALIATE_LIST); //we have just been tamed by a new party, clean slate for everyone!
        Waiting.NoSleep(cancel_current_plan());
        RegisterSignal(new_friend, COMSIG_LIVING_MADE_NEW_FRIEND, nameof(on_master_tame));
    }

    // tg: code/datums/ai/basic_mobs/base_basic_controller.dm:72
    [SignalHandler]
    public virtual void on_untamed(datum source = null, mob_living old_friend = null)
    {
        UnregisterSignal(old_friend, COMSIG_LIVING_MADE_NEW_FRIEND);
    }

    // tg: code/datums/ai/basic_mobs/base_basic_controller.dm:76
    [SignalHandler]
    public virtual void on_master_tame(datum master = null, mob_living master_new_friend = null)
    {
        forgive_target(master_new_friend);
    }

    // tg: code/datums/ai/basic_mobs/base_basic_controller.dm:80
    public virtual void forgive_target(atom target = null)
    {
        foreach (object key in Dm.each(Dm.list(BB_CURRENT_TARGET, BB_CURRENT_PET_TARGET)))
        {
            if (Dm.eq(target, blackboard[key]))
                clear_blackboard_key(key);
        }
        remove_from_blackboard_lazylist_key(BB_BASIC_MOB_RETALIATE_LIST, target); //make peace with our friend's new pet!
    }
}
