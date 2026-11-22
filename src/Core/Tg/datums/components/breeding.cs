// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/breeding.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

/*
 * A component to allow us to breed
 */
// tg: code/datums/components/breeding.dm:6
public partial class datum_component_breed : datum_component
{
    // tg: code/datums/components/breeding.dm:18
    [DmParams("can_breed_with", "breed_timer", "baby_paths", "post_birth")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        DmList can_breed_with = Dm.arg<DmList>(__extra, 0) ?? Dm.list();
        object breed_timer = Dm.arg(__extra, 1) ?? 40 * SECONDS;
        DmList baby_paths = Dm.arg<DmList>(__extra, 2) ?? Dm.list();
        datum_callback post_birth = Dm.arg<datum_callback>(__extra, 3);
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;

        if (ishuman(parent)) //sin detected
            return COMPONENT_INCOMPATIBLE;

        if (!Dm.truthy(length(baby_paths)))
        {
            stack_trace("attempted to add a breeding component with invalid baby paths!", "code/datums/components/breeding.dm", 28);
            return null;
        }

        this.can_breed_with = can_breed_with;
        this.breed_timer = breed_timer;
        this.baby_paths = baby_paths;
        this.post_birth = post_birth;

        ADD_TRAIT(parent, TRAIT_SUBTREE_REQUIRED_OPERATIONAL_DATUM, type);
        return null;
    }

    // tg: code/datums/components/breeding.dm:36
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_HOSTILE_PRE_ATTACKINGTARGET, nameof(breed_with_partner));
        ADD_TRAIT(parent, TRAIT_MOB_BREEDER, REF(this));
        mob_living parent_mob = parent as mob_living;
        (parent_mob.ai_controller as datum_ai_controller)?.set_blackboard_key(breed_key, TRUE);
    }

    // tg: code/datums/components/breeding.dm:42
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_HOSTILE_PRE_ATTACKINGTARGET);
        REMOVE_TRAIT(parent, TRAIT_MOB_BREEDER, REF(this));
        post_birth = null;
    }

    // tg: code/datums/components/breeding.dm:47
    [SignalHandler]
    public virtual object breed_with_partner(mob_living source = null, mob_living target = null)
    {
        if (Dm.truthy(source.combat_mode))
            return null;

        if (!ismob(target))
            return null;

        if (source.client != null || target.client != null)
            return null;

        if (!Dm.truthy(is_type_in_typecache(target, can_breed_with)))
            return null;

        if (!HAS_TRAIT(target, TRAIT_MOB_BREEDER) || Dm.eq(target.gender, source.gender))
            return null;

        if (!Dm.truthy(ready_to_breed))
        {
            source.balloon_alert(source, "not ready!");
            return COMPONENT_HOSTILE_NO_ATTACK;
        }

        object delivery_destination = get_turf(source);
        Type chosen_baby_path = pick_weight(baby_paths) as Type;
        atom baby = Dm.@new(chosen_baby_path, delivery_destination) as atom;
        Dm.@new<obj_effect_temp_visual_heart>(delivery_destination);
        toggle_status(source);

        _addtimer(Dm.@new<datum_callback>(this, nameof(toggle_status), source), Dm.num(breed_timer), file: "code/datums/components/breeding.dm", line: 72);
        if (post_birth != null)
            Waiting.NoSleep(post_birth.Invoke(baby, target));
        return COMPONENT_HOSTILE_NO_ATTACK;
    }

    // tg: code/datums/components/breeding.dm:79
    public virtual void toggle_status(mob_living source = null)
    {
        ready_to_breed = Dm.truthy(ready_to_breed) ? 0 : 1;
        (source.ai_controller as datum_ai_controller)?.set_blackboard_key(BB_BREED_READY, ready_to_breed);
    }
}
