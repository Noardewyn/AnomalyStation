// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/modules/modules_science.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO2 (D333): the file whole. The science MOD modules: the reagent scanner (science goggles' reagent sight) and the research
// suit's advanced kind (the research scanner and the bomb test readout of nearby explosions), the anti-gravity module and the
// teleporter (a short bluespace jump to a seen tile), both locked behind an anomaly core (datums/components/anomalock_module.dm).
// The var blocks are generated (D111).

///Reagent Scanner - Lets the user scan reagents.
// tg: code/modules/mod/modules/modules_science.dm:4
public partial class obj_item_mod_module_reagent_scanner : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_science.dm:17
    public override async DmTask on_activation(mob activator = null)
    {
        ADD_TRAIT(mod.wearer, TRAIT_REAGENT_SCANNER, REF(this));
    }

    // tg: code/modules/mod/modules/modules_science.dm:20
    public override async DmTask on_deactivation(mob activator = null, float? display_message = null, float? deleting = null)
    {
        REMOVE_TRAIT(mod.wearer, TRAIT_REAGENT_SCANNER, REF(this));
    }
}

// tg: code/modules/mod/modules/modules_science.dm:23
public partial class obj_item_mod_module_reagent_scanner_advanced : obj_item_mod_module_reagent_scanner
{
    // tg: code/modules/mod/modules/modules_science.dm:33
    public override async DmTask on_activation(mob activator = null)
    {
        await base.on_activation(activator);
        ADD_TRAIT(mod.wearer, TRAIT_RESEARCH_SCANNER, REF(this));
        RegisterSignal(SSdcs, COMSIG_GLOB_EXPLOSION, nameof(sense_explosion));
    }

    // tg: code/modules/mod/modules/modules_science.dm:38
    public override async DmTask on_deactivation(mob activator = null, float? display_message = null, float? deleting = null)
    {
        await base.on_deactivation(activator, display_message, deleting);
        REMOVE_TRAIT(mod.wearer, TRAIT_RESEARCH_SCANNER, REF(this));
        UnregisterSignal(SSdcs, COMSIG_GLOB_EXPLOSION);
    }

    // tg: code/modules/mod/modules/modules_science.dm:43
    [SignalHandler]
    public virtual void sense_explosion(datum source = null, turf epicenter = null, object devastation_range = null, object heavy_impact_range = null, object light_impact_range = null, object took = null, object orig_dev_range = null, object orig_heavy_range = null, object orig_light_range = null)
    {
        turf wearer_turf = get_turf(mod.wearer);
        if (!Dm.truthy(is_valid_z_level(wearer_turf, epicenter)))
            return;
        if (get_dist(epicenter, wearer_turf) > explosion_detection_dist)
            return;
        to_chat(mod.wearer, span_notice(I18n.T("Explosion detected! Epicenter: [], Outer: [], Shock: []", devastation_range, heavy_impact_range, light_impact_range)));
    }
}

///Anti-Gravity - Makes the user weightless.
// tg: code/modules/mod/modules/modules_science.dm:54
public partial class obj_item_mod_module_anomaly_locked_antigrav : obj_item_mod_module_anomaly_locked
{
    // tg: code/modules/mod/modules/modules_science.dm:66
    public override async DmTask on_activation(mob activator = null)
    {
        if (Dm.truthy(mod.wearer.has_gravity()))
            Dm.@new<obj_effect_temp_visual_mook_dust>(get_turf(this));
        await mod.wearer.AddElement(typeof(datum_element_forced_gravity), 0);
        playsound(this, Dm.resource("sound/effects/gravhit.ogg"), 50);
    }

    // tg: code/modules/mod/modules/modules_science.dm:72
    public override async DmTask on_deactivation(mob activator = null, float? display_message = null, float? deleting = null)
    {
        deleting ??= FALSE;
        mod.wearer.RemoveElement(typeof(datum_element_forced_gravity), 0);
        if (Dm.truthy(deleting))
            return;
        if (Dm.truthy(mod.wearer.has_gravity()))
            Dm.@new<obj_effect_temp_visual_mook_dust>(get_turf(this));
        playsound(this, Dm.resource("sound/effects/gravhit.ogg"), 50);
    }
}

///Teleporter - Lets the user teleport to a nearby location.
// tg: code/modules/mod/modules/modules_science.dm:87
public partial class obj_item_mod_module_anomaly_locked_teleporter : obj_item_mod_module_anomaly_locked
{
    // tg: code/modules/mod/modules/modules_science.dm:104
    public override async DmTask<object> on_select_use(atom target = null)
    {
        object dot = await base.on_select_use(target);
        if (!Dm.truthy(dot))
            return dot;
        turf_open target_turf = get_turf(target) as turf_open;
        if (get_dist(get_turf(target), mod.wearer) > Dm.num(max_range))
        {
            balloon_alert(mod.wearer, I18n.T("too far!"));
            return dot;
        }
        if (!istype(target_turf, typeof(turf_open)))
        {
            balloon_alert(mod.wearer, I18n.T("invalid target!"));
            return dot;
        }
        if (Dm.truthy(target_turf.is_blocked_turf_ignore_climbable()) || !Dm.truthy(await los_check(mod.wearer, target, pass_args: Dm.num(Dm.bitor(Dm.bitor(Dm.bitor(Dm.bitor(Dm.bitor(Dm.bitor(Dm.bitor(PASSTABLE, PASSGLASS), PASSGRILLE), PASSMOB), PASSMACHINE), PASSSTRUCTURE), PASSFLAPS), PASSWINDOW)))))
        {
            balloon_alert(mod.wearer, I18n.T("blocked destination!"));
            return dot;
        }
        // check early so we don't go through the whole loops
        if (!Dm.truthy(check_teleport_valid(mod.wearer, target_turf, channel: TELEPORT_CHANNEL_BLUESPACE, original_destination: target_turf)))
        {
            balloon_alert(mod.wearer, I18n.T("something holds you back!"));
            return dot;
        }
        balloon_alert(mod.wearer, I18n.T("teleporting..."));
        matrix pre_matrix = matrix();
        pre_matrix.Scale(4, 0.25f);
        matrix post_matrix = matrix();
        post_matrix.Scale(0.25f, 4);
        animate(mod.wearer, Dm.named("time", teleport_time), Dm.named("color", COLOR_CYAN), Dm.named("transform", pre_matrix.Multiply(mod.wearer.transform)), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_OUT)));
        if (!Dm.truthy(await do_after(mod.wearer, teleport_time, target: mod)))
        {
            balloon_alert(mod.wearer, I18n.T("interrupted!"));
            animate(mod.wearer, Dm.named("time", teleport_time * 0.1f), Dm.named("color", null), Dm.named("transform", post_matrix.Multiply(mod.wearer.transform)), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_IN)));
            return dot;
        }
        animate(mod.wearer, Dm.named("time", teleport_time * 0.1f), Dm.named("color", null), Dm.named("transform", post_matrix.Multiply(mod.wearer.transform)), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_IN)));
        if (!Dm.truthy(await do_teleport(mod.wearer, target_turf, asoundin: Dm.resource("sound/effects/phasein.ogg"))))
            return dot;
        await drain_power(use_energy_cost);
        return dot;
    }
}
