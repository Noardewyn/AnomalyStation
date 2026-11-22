// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/floor_diseases/gastritium.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

/// Caused by dirty food. Makes you burp out Tritium, sometimes burning hot!
// tg: code/datums/diseases/floor_diseases/gastritium.dm:2
public partial class datum_disease_gastritium : datum_disease
{
    // tg: code/datums/diseases/floor_diseases/gastritium.dm:18
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 2)
        {
            if (SPT_PROB(1, seconds_per_tick))
                await affected_mob.emote("burp");
        }
        else if (stage == 3)
        {
            if (SPT_PROB(1, seconds_per_tick) && !IS_UNCONSCIOUS_OR_CRIT(affected_mob))
                to_chat(affected_mob, span_warning(I18n.T("Your stomach makes turbine noises...")));
            else if (SPT_PROB(1, seconds_per_tick))
                await affected_mob.emote("burp");
        }
        else if (stage == 4)
        {
            if (SPT_PROB(1, seconds_per_tick) && !IS_UNCONSCIOUS_OR_CRIT(affected_mob))
                to_chat(affected_mob, span_warning(I18n.T("You're starting to feel like a burn chamber...")));
            else if (SPT_PROB(1, seconds_per_tick))
                await tritium_burp();
        }
        else if (stage == 5)
        {
            if (SPT_PROB(1, seconds_per_tick) && !IS_UNCONSCIOUS_OR_CRIT(affected_mob))
                to_chat(affected_mob, span_warning(I18n.T("You feel like you're about to delam...")));
            else if (SPT_PROB(1, seconds_per_tick))
                await tritium_burp(hot_chance: TRUE);
        }
        float change_limit = max(Dm.num(affected_mob.get_body_temp_heat_damage_limit()) - 5 - Dm.num(affected_mob.get_body_temp_normal(apply_change: FALSE)), 0);
        affected_mob.add_body_temperature_change("gastritium", min(15 * stage, change_limit));
        return dot;
    }

    // tg: code/datums/diseases/floor_diseases/gastritium.dm:45
    public virtual async DmTask tritium_burp(float? hot_chance = null)
    {
        hot_chance ??= FALSE;
        datum_gas_mixture burp = Dm.@new<datum_gas_mixture>();
        burp.set_gas(typeof(datum_gas_tritium), MOLES_GAS_VISIBLE);
        burp.temperature = affected_mob.bodytemperature;
        if (Dm.truthy(hot_chance) && prob(tritium_burp_hot_chance))
        {
            burp.set_temperature(TRITIUM_MINIMUM_BURN_TEMPERATURE);
            if (!IS_UNCONSCIOUS_OR_CRIT(affected_mob))
                to_chat(affected_mob, span_warning(I18n.T("Your throat feels hot!")));
        }
        affected_mob.visible_message(I18n.T("burps out green gas."), visible_message_flags: EMOTE_MESSAGE);
        await ((atom)affected_mob.loc).assume_air(burp);
    }
}
