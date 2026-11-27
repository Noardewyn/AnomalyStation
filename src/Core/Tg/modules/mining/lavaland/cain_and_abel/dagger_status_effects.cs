// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/cain_and_abel/dagger_status_effects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

///status effect applied to us when we're wildly swinging
// tg: code/modules/mining/lavaland/cain_and_abel/dagger_status_effects.dm:2
public partial class datum_status_effect_dagger_swinging : datum_status_effect
{
    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_status_effects.dm:10
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return dot;
        obj_effect_temp_visual_dagger_slash slash_effect = Dm.@new<obj_effect_temp_visual_dagger_slash>();
        Dm.append(owner.vis_contents, slash_effect);
        ADD_TRAIT(owner, TRAIT_TENTACLE_IMMUNE, REF(this));
        RegisterSignal(owner, COMSIG_ATOM_PRE_BULLET_ACT, nameof(hit_by_projectile));
        RegisterSignal(owner, COMSIG_LIVING_CHECK_BLOCK, nameof(block_attack));
        return dot;
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_status_effects.dm:20
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (!isturf(owner.loc))
            return null;

        float mob_hit = FALSE;
        foreach (mob_living target_mob in Dm.each<mob_living>(oview(owner, 1)))
        {
            await target_mob.apply_damage(Dm.num(target_mob.mob_size) < MOB_SIZE_LARGE ? base_damage : base_damage * 5, BRUTE);
            mob_hit = TRUE;
        }

        if (Dm.truthy(mob_hit))
            playsound(owner, Dm.resource("sound/items/weapons/bladeslice.ogg"), 75, FALSE); //just play it once
        return null;
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_status_effects.dm:32
    [SignalHandler]
    public virtual object hit_by_projectile(mob_living swinger = null, obj_projectile projectile = null, object hit_area = null)
    {
        if (!isturf(owner.loc))
            return NONE;

        playsound(swinger, Dm.resource("sound/items/weapons/parry.ogg"), 75, TRUE);

        obj_effect_temp_visual_guardian_phase_out parry_effect = Dm.@new<obj_effect_temp_visual_guardian_phase_out>();
        parry_effect.pixel_x = rand(-4, 4);
        parry_effect.pixel_y = rand(-10, 10);
        Dm.append(owner.vis_contents, parry_effect);

        projectile.firer = swinger;
        projectile.set_angle(-Dm.num(projectile.angle));
        return COMPONENT_BULLET_PIERCED;
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_status_effects.dm:49
    [SignalHandler]
    public virtual object block_attack(mob_living source = null, atom hitby = null, object damage = null, object attack_text = null, object attack_type = null, object armour_penetration = null, object damage_type = null, object attack_flag = null)
    {
        if (Dm.eq(attack_type, PROJECTILE_ATTACK) || Dm.num(damage) >= 75 || Dm.num(damage) <= 0 || Dm.eq(damage_type, STAMINA))
            return NONE;

        playsound(owner, Dm.resource("sound/items/weapons/parry.ogg"), 75, TRUE);
        Dm.@new<obj_effect_temp_visual_guardian_phase_out>(get_turf(owner));
        return SUCCESSFUL_BLOCK;
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_status_effects.dm:68
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        REMOVE_TRAIT(owner, TRAIT_TENTACLE_IMMUNE, REF(this));
        return dot;
    }
}

///status effect applied to enemies who step on crystals
// tg: code/modules/mining/lavaland/cain_and_abel/dagger_status_effects.dm:73
public partial class datum_status_effect_dagger_stun : datum_status_effect
{
    ///overlay we apply to stunned enemies
    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_status_effects.dm:82 (var/static/mutable_appearance/stun_lightning)
    public static mutable_appearance stun_lightning => TgState.Current.datum_status_effect_dagger_stun__stun_lightning ??= (mutable_appearance)mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "lightning", layer: ABOVE_ALL_MOB_LAYER);

    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_status_effects.dm:81
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return dot;

        RegisterSignal(owner, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_overlays_updated));
        ADD_TRAIT(owner, TRAIT_AI_PAUSED, REF(this));
        owner.update_appearance();
        return dot;
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_status_effects.dm:90
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        REMOVE_TRAIT(owner, TRAIT_AI_PAUSED, REF(this));
        UnregisterSignal(owner, COMSIG_ATOM_UPDATE_OVERLAYS);
        owner.update_appearance();
        return dot;
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_status_effects.dm:96
    [SignalHandler]
    public virtual void on_overlays_updated(atom parent_atom = null, DmList overlays = null)
    {
        Dm.append(overlays, stun_lightning);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_status_effects.dm:82 (var/static/mutable_appearance/stun_lightning)
    internal mutable_appearance datum_status_effect_dagger_stun__stun_lightning;
}
