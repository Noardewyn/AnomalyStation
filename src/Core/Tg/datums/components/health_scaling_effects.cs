// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/health_scaling_effects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2): a wounded spider slows down.

/**
 * ### Enrage buffs component
 *
 * Scales some statistics of a living mob (speed or attack power or such) based on how hurt it is.
 */
// tg: code/datums/components/health_scaling_effects.dm:6
public partial class datum_component_health_scaling_effects : datum_component
{
    // tg: code/datums/components/health_scaling_effects.dm:27
    [DmParams("max_health_threshold", "min_health_threshold", "max_health_attack_modifier_lower", "min_health_attack_modifier_lower", "max_health_attack_modifier_upper", "min_health_attack_modifier_upper", "max_health_slowdown", "min_health_slowdown", "additional_status_callback")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;

        max_health_threshold = Dm.arg(__extra, 0) ?? 1f;
        min_health_threshold = Dm.arg(__extra, 1) ?? 0.25f;
        max_health_attack_modifier_lower = Dm.arg(__extra, 2) ?? 0f;
        min_health_attack_modifier_lower = Dm.arg(__extra, 3) ?? 0f;
        max_health_attack_modifier_upper = Dm.arg(__extra, 4) ?? 0f;
        min_health_attack_modifier_upper = Dm.arg(__extra, 5) ?? 0f;
        max_health_slowdown = Dm.arg(__extra, 6) ?? 0f;
        min_health_slowdown = Dm.arg(__extra, 7) ?? 0f;
        additional_status_callback = Dm.arg<datum_callback>(__extra, 8);

        RegisterSignal(parent, COMSIG_LIVING_HEALTH_UPDATE, nameof(on_health_changed));
        return null;
    }

    // tg: code/datums/components/health_scaling_effects.dm:52
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_LIVING_HEALTH_UPDATE);
        await base.UnregisterFromParent(__extra);
    }

    // tg: code/datums/components/health_scaling_effects.dm:56
    public override object Destroy(float? force = null)
    {
        additional_status_callback = null;
        return base.Destroy(force);
    }

    /// Called when mob health changes, recalculates the ratio between maximum and minimum
    // tg: code/datums/components/health_scaling_effects.dm:61
    [SignalHandler]
    public virtual void on_health_changed(mob_living source = null)
    {
        float current_health_percentage = Dm.num(source.health) / Dm.num(source.maxHealth);
        float lowest = Dm.num(min_health_threshold), highest = Dm.num(max_health_threshold);
        float max_min_ratio = Dm.clamp((current_health_percentage - lowest) / (highest - lowest), 0, 1); // INVERSE_LERP

        Waiting.Detach(update_stats(source, max_min_ratio));
    }

    /// Update statistics based on provided interpolator between maximum and minimum values
    // tg: code/datums/components/health_scaling_effects.dm:69
    public virtual async DmTask update_stats(mob_living source = null, float max_min_ratio = 0)
    {
        if (!Dm.eq(max_health_attack_modifier_lower, 0f) || !Dm.eq(min_health_attack_modifier_lower, 0f))
        {
            float lower_modifier = LERP(Dm.num(min_health_attack_modifier_lower), Dm.num(max_health_attack_modifier_lower), max_min_ratio);
            source.melee_damage_lower = Dm.num(initial(source, "melee_damage_lower")) + lower_modifier;
        }
        if (!Dm.eq(max_health_attack_modifier_upper, 0f) || !Dm.eq(min_health_attack_modifier_upper, 0f))
        {
            float upper_modifier = LERP(Dm.num(min_health_attack_modifier_upper), Dm.num(max_health_attack_modifier_upper), max_min_ratio);
            source.melee_damage_upper = Dm.num(initial(source, "melee_damage_upper")) + upper_modifier;
        }

        if (!Dm.eq(max_health_slowdown, 0f) || !Dm.eq(min_health_slowdown, 0f))
            await source.add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_health_scaling_speed_buff), multiplicative_slowdown: LERP(Dm.num(min_health_slowdown), Dm.num(max_health_slowdown), max_min_ratio));

        if (additional_status_callback != null)
            await additional_status_callback.Invoke(max_min_ratio);
    }
}
