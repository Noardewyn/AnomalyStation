// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/cuff_n_stun.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

/*
 * A component to stun and cuff targets
 */
// tg: code/datums/components/cuff_n_stun.dm:4
public partial class datum_component_stun_n_cuff : datum_component
{
    // tg: code/datums/components/cuff_n_stun.dm:26
    [DmParams("blacklist_mobs", "stun_sound", "stun_timer", "handcuff_timer", "stun_cooldown_timer", "handcuff_type", "post_stun_callback", "post_arrest_callback")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object blacklist_mobs = Dm.arg(__extra, 0) ?? Dm.list();
        object stun_sound = Dm.arg(__extra, 1) ?? Dm.resource("sound/items/weapons/egloves.ogg");
        object stun_timer = Dm.arg(__extra, 2) ?? 8 * SECONDS;
        object handcuff_timer = Dm.arg(__extra, 3) ?? 4 * SECONDS;
        object stun_cooldown_timer = Dm.arg(__extra, 4) ?? 10 * SECONDS;
        object handcuff_type = Dm.arg(__extra, 5) ?? typeof(obj_item_restraints_handcuffs_cable_zipties_used);
        datum_callback post_stun_callback = Dm.arg<datum_callback>(__extra, 6);
        datum_callback post_arrest_callback = Dm.arg<datum_callback>(__extra, 7);
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;

        this.blacklist_mobs = blacklist_mobs as DmList;
        this.stun_sound = stun_sound;
        this.stun_timer = stun_timer;
        this.handcuff_timer = handcuff_timer;
        this.handcuff_type = handcuff_type as Type;
        this.stun_cooldown_timer = stun_cooldown_timer;
        this.post_stun_callback = post_stun_callback;
        this.post_arrest_callback = post_arrest_callback;
        return null;
    }

    // tg: code/datums/components/cuff_n_stun.dm:49
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_HOSTILE_PRE_ATTACKINGTARGET, nameof(on_unarmed_attack));
    }

    // tg: code/datums/components/cuff_n_stun.dm:52
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_HOSTILE_PRE_ATTACKINGTARGET);
        REMOVE_TRAIT(parent, TRAIT_MOB_BREEDER, REF(this));
        post_stun_callback = null;
        post_arrest_callback = null;
    }

    // tg: code/datums/components/cuff_n_stun.dm:58
    [SignalHandler]
    public virtual object on_unarmed_attack(mob_living source = null, atom target = null)
    {
        if (target == source || !iscarbon(target))
            return NONE;

        if (Dm.truthy(is_type_in_typecache(target, blacklist_mobs)))
            return NONE;

        mob_living_carbon living_target = (mob_living_carbon)target;
        if (Dm.truthy(living_target.IsParalyzed()))
            Waiting.Detach(cuff_target(living_target));
        else
            Waiting.NoSleep(stun_target(living_target));

        return COMPONENT_HOSTILE_NO_ATTACK;
    }

    // tg: code/datums/components/cuff_n_stun.dm:75
    public virtual async DmTask cuff_target(mob_living_carbon human_target = null)
    {
        mob_living living_parent = (mob_living)parent;
        if (human_target.handcuffed != null)
        {
            living_parent.balloon_alert(human_target, "already cuffed!");
            return;
        }

        playsound(living_parent, Dm.resource("sound/items/weapons/cablecuff.ogg"), 30, TRUE);
        human_target.visible_message(span_danger(Dm.text("[] is trying to put zipties on []!", parent, human_target)),
            span_danger(Dm.text("[] is trying to put zipties on you!", parent)));

        if (!Dm.truthy(await do_after(living_parent, Dm.num(handcuff_timer), target: human_target)))
            return;
        await human_target.set_handcuffed(Dm.@new(handcuff_type, human_target) as obj_item);
        if (post_arrest_callback != null)
            await post_arrest_callback.Invoke(human_target);
    }

    // tg: code/datums/components/cuff_n_stun.dm:88
    public virtual async DmTask stun_target(mob_living_carbon human_target = null)
    {
        if (!(stun_cooldown <= world.time)) // COOLDOWN_FINISHED, inline
            return;
        playsound(parent as atom, stun_sound, 50, TRUE);
        await human_target.Paralyze(stun_timer);
        await human_target.set_stutter(40 * SECONDS);
        log_combat(parent as atom, human_target, "honked");

        human_target.visible_message(
            span_danger(Dm.text("[] stuns []!", parent, human_target)),
            span_userdanger(Dm.text("[] stuns you!", parent)));
        stun_cooldown = world.time + Dm.num(stun_cooldown_timer); // COOLDOWN_START, inline
        if (post_stun_callback != null)
            await post_stun_callback.Invoke(human_target);
    }
}
