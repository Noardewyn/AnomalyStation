// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/dynamic/_dynamic_tier.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A3 (D316): the whole file (the five tiers are var blocks, generated).

/**
 * ## Dynamic tier datum
 *
 * These datums are essentially used to configure the dynamic system
 * They serve as a very simple way to see at a glance what dynamic is doing and what it is going to do
 *
 * For example, a tier will say "we will spawn 1-2 roundstart antags"
 */
public partial class datum_dynamic_tier : datum
{
    // tg: code/controllers/subsystem/dynamic/_dynamic_tier.dm:81
    public override async DmTask New(params object[] __extra)
    {
        DmList dynamic_config = __extra.Length > 0 ? __extra[0] as DmList : null;
        foreach (object new_var in Dm.each(dynamic_config?[config_tag]))
        {
            if (!Dm.has_var(this, Dm.str(new_var))) // !(new_var in vars)
                continue;
            set_config_value(new_var, (dynamic_config[config_tag] as DmList)[new_var]);
        }
    }

    /// Used for parsing config entries to validate them
    // tg: code/controllers/subsystem/dynamic/_dynamic_tier.dm:88
    public virtual object set_config_value(object new_var = null, object new_val = null)
    {
        if (Dm.eq(new_var, nameof(tier)) || Dm.eq(new_var, nameof(config_tag)) || Dm.eq(new_var, "vars"))
            return FALSE;
        if (Dm.eq(new_var, nameof(ruleset_type_settings)))
        {
            DmList new_settings = new_val as DmList;
            foreach (object category in Dm.each(new_settings))
            {
                DmList new_category = new_settings[category] as DmList;
                foreach (object rule in Dm.each(new_category))
                {
                    if (Dm.eq(rule, LOW_END) || Dm.eq(rule, HIGH_END))
                        (ruleset_type_settings[category] as DmList)[rule] = Dm.max(0, Dm.num(new_category[rule]));
                    else if (Dm.eq(rule, TIME_THRESHOLD) || Dm.eq(rule, EXECUTION_COOLDOWN_LOW) || Dm.eq(rule, EXECUTION_COOLDOWN_HIGH))
                        (ruleset_type_settings[category] as DmList)[rule] = Dm.num(new_category[rule]) * 1 * MINUTES;
                    else
                        (ruleset_type_settings[category] as DmList)[rule] = new_category[rule];
                }
            }
            return TRUE;
        }

        Dm.set_var(this, Dm.str(new_var), new_val); // vars[new_var] = new_val
        return TRUE;
    }
}
