// Translated from tgstation (snapshot 1d99bd7f): code/datums/proximity_monitor/fields/projectile_dampener/projectile_dampener.dm and projectile_dampener_effects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole files (silicons SI2, D326): the projectile dampening field (the peacekeeper cyborg's) and the effects it applies to the
// caught bullets. The var blocks are generated (D111).

//Projectile dampening field that slows projectiles and lowers their damage for an energy cost deducted every 1/5 second.
// tg: code/datums/proximity_monitor/fields/projectile_dampener/projectile_dampener.dm:2
public partial class datum_proximity_monitor_advanced_bubble_projectile_dampener : datum_proximity_monitor_advanced_bubble
{
    ///overlay we apply to caught bullets
    private static image new_bullet_overlay => TgState.Current.datum_proximity_monitor_advanced_bubble_projectile_dampener__new_bullet_overlay ??= image(Dm.resource("icons/effects/fields.dmi"), "projectile_dampen_effect") as image;

    // tg: code/datums/proximity_monitor/fields/projectile_dampener/projectile_dampener.dm:8
    [DmParams("_host", "range", "_ignore_if_not_on_turf", "projector", "effects_typepath")]
    public override async DmTask New(params object[] __extra)
    {
        Type effects_typepath = Dm.arg(__extra, 4) as Type;
        await base.New(__extra);
        bullet_effects = effects_typepath != null ? Dm.@new(effects_typepath) as datum_dampener_projectile_effects : Dm.@new<datum_dampener_projectile_effects>();
    }

    // tg: code/datums/proximity_monitor/fields/projectile_dampener/projectile_dampener.dm:12
    public override object Destroy(float? force = null)
    {
        bullet_effects = null;
        return base.Destroy(force);
    }

    // tg: code/datums/proximity_monitor/fields/projectile_dampener/projectile_dampener.dm:16
    public override void field_edge_crossed(atom_movable movable = null, turf location = null, turf old_location = null)
    {
        base.field_edge_crossed(movable, location, old_location);
        if (!isprojectile(movable))
            return;
        determine_wobble(location);
    }

    // tg: code/datums/proximity_monitor/fields/projectile_dampener/projectile_dampener.dm:22
    public override void field_edge_uncrossed(atom_movable movable = null, turf old_location = null, turf new_location = null)
    {
        base.field_edge_uncrossed(movable, old_location, new_location);
        if (!isprojectile(movable))
            return;
        determine_wobble(old_location);
    }

    // tg: code/datums/proximity_monitor/fields/projectile_dampener/projectile_dampener.dm:28
    public override void field_turf_crossed(atom_movable movable = null, turf old_location = null, turf new_location = null)
    {
        if (!isprojectile(movable) || HAS_TRAIT_FROM(movable, TRAIT_GOT_DAMPENED, REF(this)))
            return;
        catch_bullet_effect(movable as obj_projectile);
    }

    // tg: code/datums/proximity_monitor/fields/projectile_dampener/projectile_dampener.dm:33
    public override void field_turf_uncrossed(atom_movable movable = null, turf old_location = null, turf new_location = null)
    {
        if (!isprojectile(movable) || get_dist(new_location, host) <= (Dm.truthy(edge_is_a_field) ? Dm.num(current_range) : Dm.num(current_range) - 1))
            return;
        release_bullet_effect(movable as obj_projectile);
    }

    // tg: code/datums/proximity_monitor/fields/projectile_dampener/projectile_dampener.dm:38
    public override void setup_field_turf(turf target = null)
    {
        foreach (atom possible_projectile in Dm.each<atom>(target))
        {
            if (isprojectile(possible_projectile))
                catch_bullet_effect(possible_projectile as obj_projectile);
        }
    }

    // tg: code/datums/proximity_monitor/fields/projectile_dampener/projectile_dampener.dm:43
    public override void cleanup_field_turf(turf target = null)
    {
        foreach (atom possible_projectile in Dm.each<atom>(target))
        {
            if (isprojectile(possible_projectile) && HAS_TRAIT_FROM(possible_projectile, TRAIT_GOT_DAMPENED, REF(this)))
                release_bullet_effect(possible_projectile as obj_projectile);
        }
    }

    ///proc that applies the wobbly effect on point of bullet entry
    // tg: code/datums/proximity_monitor/fields/projectile_dampener/projectile_dampener.dm:49
    public virtual void determine_wobble(turf location = null)
    {
        float coord_x = location.x - host.x;
        float coord_y = location.y - host.y;
        obj_effect_overlay_vis_field my_field = edgeturf_effects[Dm.text("[],[]", coord_x, coord_y)] as obj_effect_overlay_vis_field;
        my_field?.set_wobbly(0.15f * SECONDS);
    }

    // tg: code/datums/proximity_monitor/fields/projectile_dampener/projectile_dampener.dm:55
    [SignalHandler]
    public virtual void projectile_overlay_updated(atom source = null, DmList overlays = null)
    {
        if (new_bullet_overlay != null && HAS_TRAIT_FROM(source, TRAIT_GOT_DAMPENED, REF(this)))
            Dm.append(ref overlays, new_bullet_overlay);
    }

    ///a bullet has entered our field, apply the dampening effects to it
    // tg: code/datums/proximity_monitor/fields/projectile_dampener/projectile_dampener.dm:62
    public virtual void catch_bullet_effect(obj_projectile bullet = null)
    {
        ADD_TRAIT(bullet, TRAIT_GOT_DAMPENED, REF(this));
        RegisterSignal(bullet, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(projectile_overlay_updated));
        if (Dm.truthy(_listen_lookup?[COMSIG_DAMPENER_CAPTURE]))
            _SendSignal(COMSIG_DAMPENER_CAPTURE, Dm.list(this, bullet));
        bullet_effects.apply_effects(bullet);
        bullet.update_appearance();
    }

    ///removing the effects after it has exited our field
    // tg: code/datums/proximity_monitor/fields/projectile_dampener/projectile_dampener.dm:70
    public virtual void release_bullet_effect(obj_projectile bullet = null)
    {
        REMOVE_TRAIT(bullet, TRAIT_GOT_DAMPENED, REF(this));
        if (Dm.truthy(_listen_lookup?[COMSIG_DAMPENER_RELEASE]))
            _SendSignal(COMSIG_DAMPENER_RELEASE, Dm.list(this, bullet));
        bullet_effects.remove_effects(bullet);
        bullet.update_appearance();
        UnregisterSignal(bullet, COMSIG_ATOM_UPDATE_OVERLAYS);
    }
}

// tg: code/datums/proximity_monitor/fields/projectile_dampener/projectile_dampener.dm:77
public partial class datum_proximity_monitor_advanced_bubble_projectile_dampener_peaceborg : datum_proximity_monitor_advanced_bubble_projectile_dampener
{
    // tg: code/datums/proximity_monitor/fields/projectile_dampener/projectile_dampener.dm:79
    public override void field_turf_crossed(atom_movable movable = null, turf old_location = null, turf new_location = null)
    {
        base.field_turf_crossed(movable, old_location, new_location);
        if (!iscyborg(movable) || !HAS_TRAIT_FROM(movable, TRAIT_GOT_DAMPENED, REF(this)))
            ADD_TRAIT(movable, TRAIT_GOT_DAMPENED, REF(this));
    }

    // tg: code/datums/proximity_monitor/fields/projectile_dampener/projectile_dampener.dm:84
    public override void field_turf_uncrossed(atom_movable movable = null, turf old_location = null, turf new_location = null)
    {
        if (!iscyborg(movable) || get_dist(new_location, host) <= (Dm.truthy(edge_is_a_field) ? Dm.num(current_range) : Dm.num(current_range) - 1))
            return;
        REMOVE_TRAIT(movable, TRAIT_GOT_DAMPENED, REF(this));
    }

    // tg: code/datums/proximity_monitor/fields/projectile_dampener/projectile_dampener.dm:89
    public override void setup_field_turf(turf target = null)
    {
        foreach (atom interesting_atom in Dm.each_cast<atom>(target))
        {
            if (iscyborg(interesting_atom))
                ADD_TRAIT(interesting_atom, TRAIT_GOT_DAMPENED, REF(this));
            if (isprojectile(interesting_atom))
                catch_bullet_effect(interesting_atom as obj_projectile);
        }
    }

    // tg: code/datums/proximity_monitor/fields/projectile_dampener/projectile_dampener.dm:96
    public override void cleanup_field_turf(turf target = null)
    {
        foreach (atom interesting_atom in Dm.each_cast<atom>(target))
        {
            if (iscyborg(interesting_atom))
                REMOVE_TRAIT(interesting_atom, TRAIT_GOT_DAMPENED, REF(this));
            if (isprojectile(interesting_atom))
                release_bullet_effect(interesting_atom as obj_projectile);
        }
    }
}

///datum that holds the effects we have on bullet catching
// tg: code/datums/proximity_monitor/fields/projectile_dampener/projectile_dampener_effects.dm:2
public partial class datum_dampener_projectile_effects : datum
{
    // tg: code/datums/proximity_monitor/fields/projectile_dampener/projectile_dampener_effects.dm:14
    public virtual void apply_effects(obj_projectile bullet = null)
    {
        if (Dm.truthy(projectile_speed_multiplier))
            bullet.speed = Dm.num(bullet.speed) * projectile_speed_multiplier;
        if (Dm.truthy(projectile_damage_multiplier))
            bullet.damage = Dm.num(bullet.damage) * projectile_damage_multiplier;
        if (Dm.truthy(projectile_knockdown_multiplier))
            bullet.knockdown = Dm.num(bullet.knockdown) * projectile_knockdown_multiplier;
        if (Dm.truthy(projectile_stamina_multiplier))
            bullet.stamina = Dm.num(bullet.stamina) * projectile_stamina_multiplier;
        if (Dm.truthy(projectile_stun_multiplier))
            bullet.stun = Dm.num(bullet.stun) * projectile_stun_multiplier;
    }

    // tg: code/datums/proximity_monitor/fields/projectile_dampener/projectile_dampener_effects.dm:26
    public virtual void remove_effects(obj_projectile bullet = null)
    {
        bullet.speed = Dm.num(bullet.speed) / projectile_speed_multiplier;
        bullet.damage = Dm.num(bullet.damage) / projectile_damage_multiplier;
        bullet.knockdown = Dm.num(bullet.knockdown) / projectile_knockdown_multiplier;
        bullet.stamina = Dm.num(bullet.stamina) / projectile_stamina_multiplier;
        bullet.stun = Dm.num(bullet.stun) / projectile_stun_multiplier;
    }
}

// tg: code/datums/proximity_monitor/fields/projectile_dampener/projectile_dampener.dm:4  (the type's static new_bullet_overlay, rules 7.6)
public sealed partial class TgState
{
    internal image datum_proximity_monitor_advanced_bubble_projectile_dampener__new_bullet_overlay;
}
