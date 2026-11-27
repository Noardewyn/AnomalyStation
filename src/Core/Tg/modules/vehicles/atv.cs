// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/atv.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the all-terrain vehicle, ridden with its key (its riding component in riding_vehicle.cs), its cover drawn over
// the rider, smoking while broken, welded back to health, exploding when destroyed, and its riders hit by half of the bullets
// fired at it; and the turret ATV, whose mounted turret follows it. The var blocks are generated.

public sealed partial class TgState
{
    // tg: code/modules/vehicles/atv.dm:9 (var/static/mutable_appearance/atvcover)
    internal mutable_appearance obj_vehicle_ridden_atv__atvcover;
}

// tg: code/modules/vehicles/atv.dm:1
public partial class obj_vehicle_ridden_atv : obj_vehicle_ridden
{
    // tg: code/modules/vehicles/atv.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_ridable), typeof(datum_component_riding_vehicle_atv)));
        TgState.Current.obj_vehicle_ridden_atv__atvcover ??= (mutable_appearance)mutable_appearance(icon, "atvcover", MOB_LAYER + 0.1f);
        return dot;
    }

    // tg: code/modules/vehicles/atv.dm:25
    public override async DmTask post_buckle_mob(mob_living M = null)
    {
        add_overlay(TgState.Current.obj_vehicle_ridden_atv__atvcover);
        await base.post_buckle_mob(M);
    }

    // tg: code/modules/vehicles/atv.dm:29
    public override async DmTask post_unbuckle_mob(mob_living M = null)
    {
        if (!Dm.truthy(has_buckled_mobs()))
            cut_overlay(TgState.Current.obj_vehicle_ridden_atv__atvcover);
        await base.post_unbuckle_mob(M);
    }

    // tg: code/modules/vehicles/atv.dm:76
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
            return null;
        if (Dm.truthy(DOING_INTERACTION(user, this)))
        {
            balloon_alert(user, I18n.T("you're already repairing it!"));
            return TRUE;
        }
        if (Dm.num(atom_integrity) >= max_integrity)
        {
            balloon_alert(user, I18n.T("it's not damaged!"));
            return TRUE;
        }
        if (!Dm.truthy(tool.tool_start_check(user, amount: 1, heat_required: HIGH_TEMPERATURE_REQUIRED)))
            return TRUE;
        user.balloon_alert_to_viewers(I18n.T("started welding []", this), I18n.T("started repairing []", this));
        audible_message(span_hear(I18n.T("You hear welding.")));
        bool did_the_thing = false;
        while (Dm.num(atom_integrity) < max_integrity)
        {
            if (Dm.truthy(await tool.use_tool(this, user, 2.5f * SECONDS, volume: 50)))
            {
                did_the_thing = true;
                atom_integrity = Dm.num(atom_integrity) + Dm.min(10, max_integrity - Dm.num(atom_integrity));
                audible_message(span_hear(I18n.T("You hear welding.")));
            }
            else
                break;
        }
        if (did_the_thing)
            user.balloon_alert_to_viewers(I18n.T("[] repaired []", Dm.num(atom_integrity) >= max_integrity ? I18n.T("fully") : I18n.T("partially"), this));
        else
            user.balloon_alert_to_viewers(I18n.T("stopped welding []", this), I18n.T("interrupted the repair!"));
        return TRUE;
    }

    // tg: code/modules/vehicles/atv.dm:101
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        START_PROCESSING(SSobj, this);
        return await base.atom_break(damage_flag);
    }

    // tg: code/modules/vehicles/atv.dm:105
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.num(atom_integrity) >= integrity_failure * max_integrity)
            return PROCESS_KILL;
        if (SPT_PROB(10, seconds_per_tick))
            return null;
        Waiting.NoSleep(do_smoke(0, this, this));
        return null;
    }

    // tg: code/modules/vehicles/atv.dm:112
    public override async DmTask<object> projectile_hit(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, object blocked = null)
    {
        if (prob(50) || !Dm.truthy(LAZYLEN(buckled_mobs)))
            return await base.projectile_hit(hitting_projectile, def_zone, piercing_hit, blocked);
        foreach (mob buckled_mob in Dm.each<mob>(buckled_mobs))
            return await buckled_mob.projectile_hit(hitting_projectile);
        return await base.projectile_hit(hitting_projectile, def_zone, piercing_hit, blocked);
    }

    // tg: code/modules/vehicles/atv.dm:119
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        await explosion(this, devastation_range: -1, light_impact_range: 2, flame_range: 3, flash_range: 4);
        await base.atom_destruction(damage_flag);
    }

    // tg: code/modules/vehicles/atv.dm:123
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }
}

// tg: code/modules/vehicles/atv.dm:35
public partial class obj_vehicle_ridden_atv_turret : obj_vehicle_ridden_atv
{
    // tg: code/modules/vehicles/atv.dm:44
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        turret = Dm.@new<obj_machinery_porta_turret_syndicate_vehicle_turret>(loc);
        turret.@base = this;
        return dot;
    }

    // tg: code/modules/vehicles/atv.dm:49
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (turret == null)
            return dot;
        turf our_turf = get_turf(this);
        if (our_turf == null)
            return dot;
        await turret.forceMove(our_turf);
        switch (Dm.num(dir))
        {
            case NORTH:
                turret.pixel_x = base_pixel_x;
                turret.pixel_y = base_pixel_y + 4;
                turret.layer = ABOVE_MOB_LAYER;
                break;
            case EAST:
                turret.pixel_x = base_pixel_x - 12;
                turret.pixel_y = base_pixel_y + 4;
                turret.layer = OBJ_LAYER;
                break;
            case SOUTH:
                turret.pixel_x = base_pixel_x;
                turret.pixel_y = base_pixel_y + 4;
                turret.layer = OBJ_LAYER;
                break;
            case WEST:
                turret.pixel_x = base_pixel_x + 12;
                turret.pixel_y = base_pixel_y + 4;
                turret.layer = OBJ_LAYER;
                break;
        }
        return dot;
    }
}
