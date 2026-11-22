// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/dynamic/__dynamic_defines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A3 (D316): the whole file. Its defines are global (the file never #undefs them, rules 11.4); the two href macros
// (MIDROUND_CANCEL_HREF, MIDROUND_REROLL_HREF) read the caller's src and picked_ruleset, so they are inline in dynamic.cs (rules 11.1 (d)),
// and GET_DYNAMIC_CONFIG names a var (11.1 (a)): inline at its uses.

public static partial class GlobalProcs
{
    // Config values, don't change these randomly
    /// Configuring "roundstart" type rulesets
    // tg: code/controllers/subsystem/dynamic/__dynamic_defines.dm:3
    public const string ROUNDSTART = "roundstart";
    /// Configuring "light midround" type rulesets
    // tg: code/controllers/subsystem/dynamic/__dynamic_defines.dm:5
    public const string LIGHT_MIDROUND = "light_midround";
    /// Configuring "heavy midround" type rulesets
    // tg: code/controllers/subsystem/dynamic/__dynamic_defines.dm:7
    public const string HEAVY_MIDROUND = "heavy_midround";
    /// Configuring "latejoin" type rulesets
    // tg: code/controllers/subsystem/dynamic/__dynamic_defines.dm:9
    public const string LATEJOIN = "latejoin";

    /// Lower end for how many of a ruleset type can be selected
    // tg: code/controllers/subsystem/dynamic/__dynamic_defines.dm:12
    public const string LOW_END = "low";
    /// Upper end for how many of a ruleset type can be selected
    // tg: code/controllers/subsystem/dynamic/__dynamic_defines.dm:14
    public const string HIGH_END = "high";
    /// Population threshold for ruleset types - below this, only a quarter of the low to high end is used
    // tg: code/controllers/subsystem/dynamic/__dynamic_defines.dm:16
    public const string HALF_RANGE_POP_THRESHOLD = "half_range_pop_threshold";
    /// Population threshold for ruleset types - below this, only a half of the low to high end is used
    // tg: code/controllers/subsystem/dynamic/__dynamic_defines.dm:18
    public const string FULL_RANGE_POP_THRESHOLD = "full_range_pop_threshold";
    /// Round time threshold for which a ruleset type will be selected
    // tg: code/controllers/subsystem/dynamic/__dynamic_defines.dm:20
    public const string TIME_THRESHOLD = "time_threshold";
    /// Lower end for cooldown duration for a ruleset type
    // tg: code/controllers/subsystem/dynamic/__dynamic_defines.dm:22
    public const string EXECUTION_COOLDOWN_LOW = "execution_cooldown_low";
    /// Upper end for cooldown duration for a ruleset type
    // tg: code/controllers/subsystem/dynamic/__dynamic_defines.dm:24
    public const string EXECUTION_COOLDOWN_HIGH = "execution_cooldown_high";

    // Tiers, don't change these randomly
    /// Tier 0, no antags at all
    // tg: code/controllers/subsystem/dynamic/__dynamic_defines.dm:28
    public const int DYNAMIC_TIER_GREEN = 0;
    /// Tier 1, low amount of antags
    // tg: code/controllers/subsystem/dynamic/__dynamic_defines.dm:30
    public const int DYNAMIC_TIER_LOW = 1;
    /// Tier 2, medium amount of antags
    // tg: code/controllers/subsystem/dynamic/__dynamic_defines.dm:32
    public const int DYNAMIC_TIER_LOWMEDIUM = 2;
    /// Tier 3, high amount of antags
    // tg: code/controllers/subsystem/dynamic/__dynamic_defines.dm:34
    public const int DYNAMIC_TIER_MEDIUMHIGH = 3;
    /// Tier 4, maximum amount of antags
    // tg: code/controllers/subsystem/dynamic/__dynamic_defines.dm:36
    public const int DYNAMIC_TIER_HIGH = 4;

    // Ruleset flags
    /// Ruleset denotes that it involves an outside force spawning in to attack the station
    // tg: code/controllers/subsystem/dynamic/__dynamic_defines.dm:40
    public const int RULESET_INVADER = 1 << 0;
    /// Multiple high impact rulesets cannot be selected unless we're at the highest tier
    // tg: code/controllers/subsystem/dynamic/__dynamic_defines.dm:42
    public const int RULESET_HIGH_IMPACT = 1 << 1;
    /// Ruleset can be configured by admins (implements /proc/configure_ruleset)
    /// Only implemented for midrounds currently
    // tg: code/controllers/subsystem/dynamic/__dynamic_defines.dm:45
    public const int RULESET_ADMIN_CONFIGURABLE = 1 << 2;
    /// Applied to rulesets which intentionally share a pref_flag with another rulesets
    /// Denotes multiple rulesets which abide by the same preference, but are each a variation of some base ruleset
    // tg: code/controllers/subsystem/dynamic/__dynamic_defines.dm:48
    public const int RULESET_VARIATION = 1 << 3;

    // tg: code/controllers/subsystem/dynamic/__dynamic_defines.dm:58
    public const string RULESET_CONFIG_CANCEL = "Cancel";
}
