// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/guardian/guardian_types/protector.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file. The var blocks are generated.

/// Very durable, and reverses the usual leash dynamic. Can slow down to become extremely durable.
// tg: code/modules/mob/living/basic/guardian/guardian_types/protector.dm:2
public partial class mob_living_basic_guardian_protector : mob_living_basic_guardian
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/protector.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        shield = Dm.@new<datum_action_cooldown_mob_cooldown_protector_shield>(this);
        Waiting.NoSleep(shield.Grant(this));
        return dot;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/protector.dm:21
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref shield);
        return base.Destroy(force);
    }

    // Invert the order
    // tg: code/modules/mob/living/basic/guardian/guardian_types/protector.dm:26
    public override async DmTask leash_to(atom_movable leashed = null, atom_movable leashed_to = null)
    {
        await base.leash_to(leashed_to, leashed);
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/protector.dm:29
    public override void unleash()
    {
        qdel(summoner?.GetComponent(typeof(datum_component_leash)));
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/protector.dm:32
    public override async DmTask toggle_modes()
    {
        await shield.Trigger();
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/protector.dm:35
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        if (Dm.num(severity) >= EXPLODE_DEVASTATE)
        {
            Waiting.NoSleep(adjust_brute_loss(400)); //if in protector mode, will do 20 damage and not actually necessarily kill the summoner
            return TRUE;
        }
        return base.ex_act(severity, target, __extra);
    }
}

/// Toggle a status effect which makes you slow but defensive
// tg: code/modules/mob/living/basic/guardian/guardian_types/protector.dm:42
public partial class datum_action_cooldown_mob_cooldown_protector_shield : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/protector.dm:53
    public override async DmTask<object> Activate(atom target = null)
    {
        mob_living living_target = target as mob_living;
        if (living_target == null)
            return FALSE;
        if (Dm.truthy(living_target.has_status_effect(typeof(datum_status_effect_protector_shield))))
        {
            living_target.remove_status_effect(typeof(datum_status_effect_protector_shield));
            return null;
        }
        await living_target.apply_status_effect(typeof(datum_status_effect_protector_shield));
        StartCooldown();
        return TRUE;
    }
}

/// Makes the guardian even more durable, but slower
// tg: code/modules/mob/living/basic/guardian/guardian_types/protector.dm:64
public partial class datum_status_effect_protector_shield : datum_status_effect
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/protector.dm:75
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (isguardian(owner))
        {
            mob_living_basic_guardian guardian_owner = (mob_living_basic_guardian)owner;
            overlay_colour = Dm.str(guardian_owner.guardian_colour);
        }
        shield_overlay = mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "shield-grey") as mutable_appearance;
        shield_overlay.color = overlay_colour;

        owner.melee_damage_lower -= damage_penalty;
        owner.melee_damage_upper -= damage_penalty;
        await owner.add_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_guardian_shield));

        MODIFY_PHYSIOLOGY(owner, BRUTE, 0.125f);
        MODIFY_PHYSIOLOGY(owner, BURN, 0.125f);
        MODIFY_PHYSIOLOGY(owner, TOX, 0.125f);
        MODIFY_PHYSIOLOGY(owner, OXY, 0.125f);

        to_chat(owner, span_bolddanger("You enter protection mode."));
        RegisterSignal(owner, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_update_overlays));
        RegisterSignals(owner, Dm.list(COMSIG_LIVING_ADJUST_BRUTE_DAMAGE, COMSIG_LIVING_ADJUST_BURN_DAMAGE, COMSIG_LIVING_ADJUST_OXY_DAMAGE, COMSIG_LIVING_ADJUST_TOX_DAMAGE), nameof(on_health_changed)); // COMSIG_LIVING_ADJUST_STANDARD_DAMAGE_TYPES (signals_mob_living.dm:95), a list literal
        owner.update_appearance(UPDATE_ICON);
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/protector.dm:97
    public override async DmTask<object> on_remove()
    {
        owner.melee_damage_lower += damage_penalty;
        owner.melee_damage_upper += damage_penalty;
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_guardian_shield));

        MODIFY_PHYSIOLOGY(owner, BRUTE, 8);
        MODIFY_PHYSIOLOGY(owner, BURN, 8);
        MODIFY_PHYSIOLOGY(owner, TOX, 8);
        MODIFY_PHYSIOLOGY(owner, OXY, 8);

        to_chat(owner, span_bolddanger("You return to your normal mode."));
        UnregisterSignal(owner, Dm.list(COMSIG_ATOM_UPDATE_OVERLAYS, COMSIG_LIVING_ADJUST_BRUTE_DAMAGE, COMSIG_LIVING_ADJUST_BURN_DAMAGE, COMSIG_LIVING_ADJUST_OXY_DAMAGE, COMSIG_LIVING_ADJUST_TOX_DAMAGE));
        owner.update_appearance(UPDATE_ICON);
        await DmTask.CompletedTask;
        return null;
    }

    /// Show an extra overlay when we're in shield mode
    // tg: code/modules/mob/living/basic/guardian/guardian_types/protector.dm:112
    [SignalHandler]
    public virtual void on_update_overlays(atom source = null, DmList overlays = null)
    {
        Dm.append(overlays, shield_overlay);
    }

    /// Flash an animation when someone tries to hurt us
    // tg: code/modules/mob/living/basic/guardian/guardian_types/protector.dm:117
    [SignalHandler]
    public virtual void on_health_changed(mob_living our_mob = null, object type = null, object amount = null, object forced = null)
    {
        if (Dm.num(amount) <= 0 && !QDELETED(our_mob))
            return;
        image flash_overlay = image(Dm.resource("icons/effects/effects.dmi"), owner, "shield-flash", dir: pick(GLOB.cardinals));
        flash_overlay.color = overlay_colour;
        owner.flick_overlay_view(flash_overlay, 0.5f * SECONDS);
    }
}
