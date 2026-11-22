// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/anomalies/anomalies_dimensional.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;


namespace Anomaly.Core.Tg;

// The M7 delamination slice (G6, D167): the whole file, the dimensional anomaly the delamination's effect_anomaly makes, which
// turns the tiles around it into one of its themes (anomalies_dimensional_themes.cs) and then moves on. The relocation's station
// announcement came with M9 slice D; the anomaly core waits for the signalers (_anomalies.cs). `theme` is declared here as tg's /datum/dimension_theme
// (the generator's V3 grep took it for a typepath var: tg assigns typepaths to other vars named theme), so its constructor is here
// as well (types-converter.md 3.2, the Hand mode); the rest of the var block is generated (D111).

// tg: code/game/objects/effects/anomalies/anomalies_dimensional.dm:2
public partial class obj_effect_anomaly_dimensional : obj_effect_anomaly
{
    /// Current anomaly 'theme', dictates what tiles to create.
    // tg: code/game/objects/effects/anomalies/anomalies_dimensional.dm:13
    public datum_dimension_theme theme;

    // tg: code/game/objects/effects/anomalies/anomalies_dimensional.dm:2
    public obj_effect_anomaly_dimensional()
    {
        name = "dimensional anomaly";
        icon_state = "dimensional";
        anomaly_core = typeof(obj_item_assembly_signaler_anomaly_dimensional);
        lifespan = ANOMALY_COUNTDOWN_TIMER * 20; // will generally be killed off by reaching max teleports first
        move_chance = 0;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_dimensional.dm:24
    [DmParams("mapload", "new_lifespan")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        Dm.append(overlays, mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "dimensional_overlay"));

        Dm.animate(this, Dm.named("transform", Dm.mul(Dm.@new<matrix>(), 0.85f)), Dm.named("time", 3), Dm.named("loop", -1));
        Dm.animate(Dm.named("transform", Dm.@new<matrix>()), Dm.named("time", 3), Dm.named("loop", -1));

        teleports_left = rand(minimum_teleports, maximum_teleports);
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_dimensional.dm:33
    public override object Destroy(float? force = null)
    {
        theme = null;
        target_turfs = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/effects/anomalies/anomalies_dimensional.dm:38
    public override async DmTask<object> anomalyEffect(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.anomalyEffect(seconds_per_tick);
        await transmute_area();
        return dot;
    }

    /**
     * Transforms a turf in our prepared area.
     */
    // tg: code/game/objects/effects/anomalies/anomalies_dimensional.dm:45
    public virtual async DmTask transmute_area()
    {
        if (!Dm.truthy(theme))
            prepare_area();
        if (!Dm.truthy(target_turfs.len))
        {
            if (Dm.num(teleports_left) <= 0 && !Dm.truthy(immortal))
            {
                await detonate();
                return;
            }
            Dm.dec(ref teleports_left);
            await relocate();
            return;
        }

        turf affected_turf = target_turfs[1] as turf;
        // TURF-IDENTITY: tg's listed reference names its cell's turf, which a blast or a singularity may have replaced since
        // prepare_area (object-model.md 7.1); the list keeps the old reference, which the removal below matches
        await theme.apply_theme(turf.dm_identity(affected_turf) as turf, show_effect: TRUE);
        Dm.remove(ref target_turfs, affected_turf);
    }

    /**
     * Prepare a new area for transformation into a new theme.
     * Optionally pass in the typepath of an anomaly theme to use that one.
     */
    // tg: code/game/objects/effects/anomalies/anomalies_dimensional.dm:63
    public virtual void prepare_area(object new_theme_path = null)
    {
        if (!Dm.truthy(new_theme_path))
            new_theme_path = pick(subtypesof(typeof(datum_dimension_theme)));
        theme = SSmaterials.dimensional_themes[new_theme_path] as datum_dimension_theme;
        apply_theme_icon();

        target_turfs = Dm.list();
        foreach (turf turf in Dm.each_cast<turf>(spiral_range_turfs(range, this)))
        {
            if (Dm.truthy(theme.can_convert(turf)))
                Dm.append(ref target_turfs, turf);
        }
    }

    /**
     * Applies an overlay icon based on the current theme.
     */
    // tg: code/game/objects/effects/anomalies/anomalies_dimensional.dm:77
    public virtual void apply_theme_icon()
    {
        Dm.remove(overlays, theme_icon);
        theme_icon = mutable_appearance(theme.icon, (string)theme.icon_state, FLOAT_LAYER - 1, appearance_flags: Dm.bitor(appearance_flags, RESET_TRANSFORM)) as mutable_appearance;
        theme_icon.blend_mode = BLEND_INSET_OVERLAY;
        Dm.append(overlays, theme_icon);
    }

    /**
     * Moves the anomaly somewhere else and announces it.
     */
    // tg: code/game/objects/effects/anomalies/anomalies_dimensional.dm:86
    public virtual async DmTask relocate()
    {
        datum_anomaly_placer placer = Dm.@new<datum_anomaly_placer>();
        area new_area = placer.findValidArea() as area;
        turf new_turf = await placer.findValidTurf(new_area) as turf;

        priority_announce(I18n.T("Dimensional instability relocated. Expected location: [].", new_area.name), I18n.T("Anomaly Alert"));
        await forceMove(new_turf);
        prepare_area();
    }

    // tg: code/game/objects/effects/anomalies/anomalies_dimensional.dm:95
    public override async DmTask detonate()
    {
        qdel(this);
    }
}
