// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/supermatter/supermatter_delamination/common_delams.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 supermatter slice (G5, D163) and the delamination slice (G6): the whole file. The var blocks are generated (D111).

// Singulo, tesla, and explosive delam

/// When we have too much gas.
// tg: code/modules/power/supermatter/supermatter_delamination/common_delams.dm:4
public partial class datum_sm_delam_singularity : datum_sm_delam
{
    // tg: code/modules/power/supermatter/supermatter_delamination/common_delams.dm:7
    public override object can_select(obj_machinery_power_supermatter_crystal sm = null)
    {
        return Dm.num(Dm.num(sm.absorbed_gasmix.total_moles()) >= MOLE_PENALTY_THRESHOLD);
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/common_delams.dm:10
    public override async DmTask<object> delam_progress(obj_machinery_power_supermatter_crystal sm = null)
    {
        if (!Dm.truthy(await base.delam_progress(sm)))
            return FALSE;
        sm.radio.talk_into(
            sm,
            I18n.T("Warning: Critical coolant mass reached."),
            Dm.gt(sm.damage, sm.emergency_point) ? sm.emergency_channel : sm.warning_channel
        );
        return TRUE;
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/common_delams.dm:20
    public override async DmTask delaminate(obj_machinery_power_supermatter_crystal sm = null)
    {
        log_delamination(sm);
        await effect_irradiate(sm);
        await effect_demoralize(sm);
        if (Dm.truthy(sm.is_main_engine))
            effect_anomaly(sm);
        if (!Dm.truthy(await effect_singulo(sm)))
            await effect_explosion(sm);
        await base.delaminate(sm);
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/common_delams.dm:30
    public override void filters(obj_machinery_power_supermatter_crystal sm = null)
    {
        base.filters(sm);

        sm.modify_filter(name: "ray", new_params: Dm.list(
            Dm.kv("color", SUPERMATTER_SINGULARITY_RAYS_COLOUR)
        ));

        sm.add_filter(name: "outline", priority: 2, @params: Dm.list(
            Dm.kv("type", "outline"),
            Dm.kv("size", 1),
            Dm.kv("color", SUPERMATTER_SINGULARITY_LIGHT_COLOUR)
        ));

        if (Dm.truthy(sm.final_countdown))
        {
            sm.add_filter(name: "icon", priority: 3, @params: Dm.list(
                Dm.kv("type", "layer"),
                Dm.kv("icon", Dm.icon(Dm.resource("icons/effects/96x96.dmi"), "singularity_s3", frame: rand(1, 8))),
                Dm.kv("flags", FILTER_OVERLAY)
            ));
        }
        else
            sm.remove_filter("icon");
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/common_delams.dm:52
    public override void on_deselect(obj_machinery_power_supermatter_crystal sm = null)
    {
        base.on_deselect(sm);
        sm.remove_filter(Dm.list("outline", "icon"));
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/common_delams.dm:56
    public override object overlays(obj_machinery_power_supermatter_crystal sm = null)
    {
        return Dm.list();
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/common_delams.dm:59
    public override async DmTask lights(obj_machinery_power_supermatter_crystal sm = null)
    {
        await base.lights(sm);
        sm.set_light_color(SUPERMATTER_SINGULARITY_LIGHT_COLOUR);
    }
}

/// When we have too much power.
// tg: code/modules/power/supermatter/supermatter_delamination/common_delams.dm:64
public partial class datum_sm_delam_tesla : datum_sm_delam
{
    // tg: code/modules/power/supermatter/supermatter_delamination/common_delams.dm:67
    public override object can_select(obj_machinery_power_supermatter_crystal sm = null)
    {
        return Dm.num(sm.internal_energy > POWER_PENALTY_THRESHOLD);
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/common_delams.dm:70
    public override async DmTask<object> delam_progress(obj_machinery_power_supermatter_crystal sm = null)
    {
        if (!Dm.truthy(await base.delam_progress(sm)))
            return FALSE;
        sm.radio.talk_into(
            sm,
            I18n.T("DANGER: CHARGE INERTIA CHAIN REACTION IN PROGRESS."),
            Dm.gt(sm.damage, sm.emergency_point) ? sm.emergency_channel : sm.warning_channel
        );
        return TRUE;
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/common_delams.dm:80
    public override async DmTask delaminate(obj_machinery_power_supermatter_crystal sm = null)
    {
        log_delamination(sm);
        await effect_irradiate(sm);
        await effect_demoralize(sm);
        if (Dm.truthy(sm.is_main_engine))
            effect_anomaly(sm);
        effect_tesla(sm);
        await effect_explosion(sm);
        await base.delaminate(sm);
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/common_delams.dm:91
    public override void filters(obj_machinery_power_supermatter_crystal sm = null)
    {
        base.filters(sm);

        sm.modify_filter(name: "ray", new_params: Dm.list(
            Dm.kv("color", SUPERMATTER_TESLA_COLOUR)
        ));

        sm.add_filter(name: "icon", priority: 2, @params: Dm.list(
            Dm.kv("type", "layer"),
            Dm.kv("icon", Dm.icon(Dm.resource("icons/obj/machines/engine/energy_ball.dmi"), "energy_ball", frame: rand(1, 12))),
            Dm.kv("flags", FILTER_UNDERLAY)
        ));
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/common_delams.dm:104
    public override void on_deselect(obj_machinery_power_supermatter_crystal sm = null)
    {
        base.on_deselect(sm);
        sm.remove_filter(Dm.list("icon"));
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/common_delams.dm:108
    public override async DmTask lights(obj_machinery_power_supermatter_crystal sm = null)
    {
        await base.lights(sm);
        sm.set_light_color(SUPERMATTER_TESLA_COLOUR);
    }
}

/// Default delam.
// tg: code/modules/power/supermatter/supermatter_delamination/common_delams.dm:113
public partial class datum_sm_delam_explosive : datum_sm_delam
{
    // tg: code/modules/power/supermatter/supermatter_delamination/common_delams.dm:116
    public override object can_select(obj_machinery_power_supermatter_crystal sm = null)
    {
        return TRUE;
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/common_delams.dm:119
    public override async DmTask delaminate(obj_machinery_power_supermatter_crystal sm = null)
    {
        log_delamination(sm);
        await effect_irradiate(sm);
        await effect_demoralize(sm);
        if (Dm.truthy(sm.is_main_engine))
            effect_anomaly(sm);
        await effect_explosion(sm);
        await base.delaminate(sm);
    }
}
