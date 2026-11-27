// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/watcher/watcher_overwatch.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * Automatically shoot at a target if they do anything while this is active on them.
 * Currently not given to any mob, but retained so admins can use it.
 */
// tg: code/modules/mob/living/basic/lavaland/watcher/watcher_overwatch.dm:5
public partial class datum_action_cooldown_mob_cooldown_watcher_overwatch : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_overwatch.dm:24
    [DmParams("Target", "original")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        melee_cooldown_time = overwatch_duration;
    }

    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_overwatch.dm:28
    public override async DmTask<object> PreActivate(atom target = null)
    {
        if (target == owner)
            return null;
        if (ismecha(target))
        {
            obj_vehicle_sealed_mecha mech = target as obj_vehicle_sealed_mecha;
            DmList drivers = mech.return_drivers() as DmList;
            if (!Dm.truthy(Dm.length(drivers)))
                return null;
            target = drivers[1] as atom;
        }
        if (!isliving(target))
            return null;
        if (Dm.num(get_dist(owner, target)) > max_range)
            return null;
        return await base.PreActivate(target);
    }

    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_overwatch.dm:43
    public override async DmTask<object> Activate(atom target__atom = null)
    {
        mob_living target = target__atom as mob_living;
        mob_living living_owner = owner as mob_living;
        living_owner.face_atom(target);
        await living_owner.Stun(overwatch_duration, ignore_canstun: TRUE);
        await target.apply_status_effect(typeof(datum_status_effect_overwatch), overwatch_duration, owner, projectile_type, projectile_sound);
        owner.visible_message(span_warning(I18n.T("[]'s eye locks on to []!", owner, target)));
        StartCooldown();
        return TRUE;
    }
}

/// Status effect which tracks whether our overwatched mob moves or acts
// tg: code/modules/mob/living/basic/lavaland/watcher/watcher_overwatch.dm:53
public partial class datum_status_effect_overwatch : datum_status_effect
{
    /// Signals which trigger a hostile response
    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_overwatch.dm:71 (var/static/list/forbidden_actions)
    public static DmList forbidden_actions => TgState.Current.datum_status_effect_overwatch__forbidden_actions ??= Dm.list(
        COMSIG_MOB_ABILITY_FINISHED,
        COMSIG_MOB_ATTACK_HAND,
        COMSIG_MOB_DROVE_MECH,
        COMSIG_MOB_FIRED_GUN,
        COMSIG_MOB_ITEM_ATTACK,
        COMSIG_MOB_THROW,
        COMSIG_MOB_USED_MECH_EQUIPMENT,
        COMSIG_MOB_USED_CLICK_MECH_MELEE,
        COMSIG_MOVABLE_MOVED);

    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_overwatch.dm:83
    [DmParams("new_owner", "set_duration", "watcher", "projectile_type", "projectile_sound")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object set_duration = Dm.arg(__extra, 0);
        mob_living watcher = Dm.arg<mob_living>(__extra, 1);
        object projectile_type = Dm.arg(__extra, 2);
        object projectile_sound = Dm.arg(__extra, 3);
        if (watcher == null || projectile_type == null)
            return FALSE;
        if (HAS_TRAIT(new_owner, TRAIT_OVERWATCH_IMMUNE))
            return FALSE;
        this.watcher = watcher;
        this.projectile_type = projectile_type as Type;
        this.projectile_sound = projectile_sound;
        if (set_duration != null)
            duration = Dm.num(set_duration);
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_overwatch.dm:95
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return FALSE;
        owner.add_traits(Dm.list(TRAIT_OVERWATCHED, TRAIT_OVERWATCH_IMMUNE), REF(this));
        owner.do_alert_animation();
        await owner.Immobilize(0.25f * SECONDS); // Just long enough that they don't trigger it by mistake
        owner.playsound_local(owner, Dm.resource("sound/machines/chime.ogg"), 50, TRUE);
        atom beam_origin = ismecha(owner.loc) ? owner.loc : owner;
        link = beam_origin.Beam(watcher, icon_state: "r_beam", override_target_pixel_x: 0) as datum_beam;
        RegisterSignals(owner, forbidden_actions, nameof(opportunity_attack));
        RegisterSignals(owner, Dm.list(COMSIG_QDELETING, COMSIG_LIVING_DEATH), nameof(on_participant_died));
        RegisterSignals(watcher, Dm.list(COMSIG_QDELETING, COMSIG_LIVING_DEATH), nameof(on_participant_died));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_overwatch.dm:109
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, Dm.add(forbidden_actions, Dm.list(COMSIG_QDELETING, COMSIG_LIVING_DEATH)) as DmList);
        QDEL_NULL(ref link);
        owner.remove_traits(Dm.list(TRAIT_OVERWATCHED, TRAIT_OVERWATCH_IMMUNE), REF(this));
        if (!QDELETED(owner))
            await owner.apply_status_effect(typeof(datum_status_effect_overwatch_immune));
        return await base.on_remove();
    }

    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_overwatch.dm:117
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref link);
        if (watcher != null) // Side effects in Destroy? Well it turns out `on_remove` is also just called on Destroy. But only if the owner isn't deleting.
        {
            Waiting.Detach(unregister_watcher(watcher));
            watcher = null;
        }

        return base.Destroy(force);
    }

    /// Clean up our association with the caster of this ability.
    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_overwatch.dm:126
    public virtual async DmTask unregister_watcher(mob_living former_overwatcher = null)
    {
        if (!Dm.truthy(overwatch_triggered))
            await former_overwatcher.Stun(2 * SECONDS, ignore_canstun: TRUE);
        UnregisterSignal(former_overwatcher, Dm.list(COMSIG_QDELETING, COMSIG_LIVING_DEATH));
    }

    /// Uh oh, you did something within my threat radius, now we're going to shoot you
    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_overwatch.dm:132
    [SignalHandler]
    public virtual void opportunity_attack()
    {
        if (!Dm.truthy(can_see(watcher, owner, length: watch_range)))
        {
            qdel(this);
            return;
        }
        overwatch_triggered = TRUE;
        watcher.do_alert_animation();
        Waiting.Detach(watcher.fire_projectile(projectile_type, owner, projectile_sound));
    }

    /// Can't overwatch you if I don't exist
    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_overwatch.dm:142
    [SignalHandler]
    public virtual void on_participant_died()
    {
        qdel(this);
    }
}

/// Blocks further applications of the ability for a little while
// tg: code/modules/mob/living/basic/lavaland/watcher/watcher_overwatch.dm:153
public partial class datum_status_effect_overwatch_immune : datum_status_effect
{
    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_overwatch.dm:158
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        ADD_TRAIT(owner, TRAIT_OVERWATCH_IMMUNE, TRAIT_STATUS_EFFECT(id));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher_overwatch.dm:162
    public override async DmTask<object> on_remove()
    {
        REMOVE_TRAIT(owner, TRAIT_OVERWATCH_IMMUNE, TRAIT_STATUS_EFFECT(id));
        return await base.on_remove();
    }
}

public sealed partial class TgState
{
    internal DmList datum_status_effect_overwatch__forbidden_actions;
}
