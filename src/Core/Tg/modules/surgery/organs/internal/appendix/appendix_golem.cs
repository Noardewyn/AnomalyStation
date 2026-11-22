// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/appendix/appendix_golem.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1, D339, D341): the golem's internal forge, which works in any carbon standing in lava. The var blocks are
// generated (D111).

// tg: code/modules/surgery/organs/internal/appendix/appendix_golem.dm:2
public partial class obj_item_organ_appendix_golem : obj_item_organ_appendix
{
    // tg: code/modules/surgery/organs/internal/appendix/appendix_golem.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        smelter = Dm.@new<datum_action_cooldown_internal_smelting>(this);
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/appendix/appendix_golem.dm:15
    [DmParams("organ_owner")]
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        RegisterSignal(owner, COMSIG_MOVABLE_MOVED, nameof(check_for_lava));
        return dot;
    }

    /// Give the action while in lava
    // tg: code/modules/surgery/organs/internal/appendix/appendix_golem.dm:20
    [SignalHandler]
    public virtual void check_for_lava(mob_living owner = null)
    {
        if (!islava(owner.loc))
        {
            Waiting.NoSleep(smelter.Remove(owner));
            return;
        }
        if (smelter.owner != owner)
            Waiting.NoSleep(smelter.Grant(owner));
    }

    // tg: code/modules/surgery/organs/internal/appendix/appendix_golem.dm:28
    [DmParams("organ_owner")]
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        UnregisterSignal(organ_owner as datum, COMSIG_MOVABLE_MOVED);
        if (smelter != null)
            await smelter.Remove(organ_owner as mob); // Might have been deleted by Destroy already
        await base.on_mob_remove(organ_owner, special, movement_flags);
    }

    // tg: code/modules/surgery/organs/internal/appendix/appendix_golem.dm:33
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref smelter);
        return base.Destroy(force);
    }
}

/// Lets golems smelt ore with their organs
// tg: code/modules/surgery/organs/internal/appendix/appendix_golem.dm:38
public partial class datum_action_cooldown_internal_smelting : datum_action_cooldown
{
    // tg: code/modules/surgery/organs/internal/appendix/appendix_golem.dm:51
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return FALSE;
        if (!islava(owner.loc))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, I18n.T("requires lava!"));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/surgery/organs/internal/appendix/appendix_golem.dm:61
    public override async DmTask<object> Activate(atom target = null)
    {
        object dot = await base.Activate(target);
        smelt_speed = Dm.initial(this, (datum_action_cooldown_internal_smelting p) => p.smelt_speed);
        await smelt_held(target as mob);
        return dot;
    }

    /// Smelt an item held in one hand and put the result in the other
    // tg: code/modules/surgery/organs/internal/appendix/appendix_golem.dm:67
    public virtual async DmTask smelt_held(mob target = null)
    {
        obj_item_stack_ore held_ore = Dm.locate<obj_item_stack_ore>(target.held_items);
        if (held_ore?.refined_type == null)
        {
            target.balloon_alert(target, I18n.T("nothing to smelt!"));
            return;
        }
        target.balloon_alert(owner, I18n.T("smelting..."));
        if (!Dm.truthy(await do_after(target, smelt_speed, target: held_ore, timed_action_flags: IGNORE_USER_LOC_CHANGE, extra_checks: Dm.@new<datum_callback>(this, nameof(IsAvailable), FALSE), interaction_key: REF(this))))
            return;
        obj_item smelted = Dm.@new(held_ore.refined_type) as obj_item;
        await held_ore.use(1);
        await target.put_in_hands(smelted);
        if (held_ore == null)
        {
            target.balloon_alert(target, I18n.T("no ore left!"));
            return;
        }
        smelt_speed = Math.Max(smelt_speed - speed_up_interval, minimum_smelt_speed);
        await smelt_held(target);
    }
}
