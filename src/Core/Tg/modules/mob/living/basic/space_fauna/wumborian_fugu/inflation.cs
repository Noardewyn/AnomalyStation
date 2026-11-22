// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/wumborian_fugu/inflation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

/// Action which inflates you, making you larger and stronger for the duration. Also invulnerable.
// tg: code/modules/mob/living/basic/space_fauna/wumborian_fugu/inflation.dm:6
public partial class datum_action_cooldown_fugu_expand : datum_action_cooldown
{
    // tg: code/modules/mob/living/basic/space_fauna/wumborian_fugu/inflation.dm:15
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return FALSE;
        if (owner is not mob_living_basic_wumborian_fugu) // A shame but there's not any good way to make this work on other mobs
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, "not stretchy enough!");
            return FALSE;
        }
        if (HAS_TRAIT(owner, TRAIT_FUGU_GLANDED))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, "already large!");
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/space_fauna/wumborian_fugu/inflation.dm:29
    public override async DmTask<object> Activate(atom target = null)
    {
        object dot = await base.Activate(target);
        await (owner as mob_living).apply_status_effect(typeof(datum_status_effect_inflated));
        return dot;
    }
}

/// Status effect from the Expand action, makes you big and round and strong.
// tg: code/modules/mob/living/basic/space_fauna/wumborian_fugu/inflation.dm:38
public partial class datum_status_effect_inflated : datum_status_effect
{
    // tg: code/modules/mob/living/basic/space_fauna/wumborian_fugu/inflation.dm:49
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        if (new_owner is not mob_living_basic_wumborian_fugu)
            return FALSE;
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/modules/mob/living/basic/space_fauna/wumborian_fugu/inflation.dm:54
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (owner is not mob_living_basic_wumborian_fugu fugu)
            return FALSE;
        RegisterSignal(fugu, COMSIG_MOB_STATCHANGE, nameof(check_death));
        await fugu.add_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_inflated));
        fugu.add_traits(Dm.list(TRAIT_FUGU_GLANDED, TRAIT_GODMODE), TRAIT_STATUS_EFFECT(id));
        await fugu.AddElement(typeof(datum_element_wall_tearer), Dm.named("allow_reinforced", FALSE));
        fugu.mob_size = MOB_SIZE_LARGE;
        fugu.icon_state = "Fugu1";
        fugu.melee_damage_lower = 15;
        fugu.melee_damage_upper = 20;
        fugu.obj_damage = 60;
        datum_ai_controller controller = fugu.ai_controller as datum_ai_controller;
        controller.set_blackboard_key(BB_BASIC_MOB_STOP_FLEEING, TRUE);
        await controller.cancel_current_plan();
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/wumborian_fugu/inflation.dm:71
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        if (owner is not mob_living_basic_wumborian_fugu fugu)
            return dot; // Check again in case you changed mob after application but somehow kept the status effect
        UnregisterSignal(fugu, COMSIG_MOB_STATCHANGE);
        fugu.remove_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_inflated));
        fugu.remove_traits(Dm.list(TRAIT_FUGU_GLANDED, TRAIT_GODMODE), TRAIT_STATUS_EFFECT(id));
        fugu.RemoveElement(typeof(datum_element_wall_tearer), Dm.named("allow_reinforced", FALSE));
        fugu.mob_size = MOB_SIZE_SMALL;
        fugu.melee_damage_lower = 0;
        fugu.melee_damage_upper = 0;
        if (fugu.stat != DEAD)
            fugu.icon_state = "Fugu0";
        fugu.obj_damage = 0;
        datum_ai_controller controller = fugu.ai_controller as datum_ai_controller;
        controller.set_blackboard_key(BB_BASIC_MOB_STOP_FLEEING, FALSE);
        await controller.cancel_current_plan();
        return dot;
    }

    /// Remove status effect if we die
    // tg: code/modules/mob/living/basic/space_fauna/wumborian_fugu/inflation.dm:89
    [SignalHandler]
    public virtual void check_death(mob_living source = null, object new_stat = null)
    {
        if (Dm.eq(new_stat, DEAD))
            qdel(this);
    }
}
