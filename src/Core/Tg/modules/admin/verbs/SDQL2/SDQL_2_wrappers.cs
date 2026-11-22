// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/verbs/SDQL2/SDQL_2_wrappers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only _step_towards and _step_away translated (gaps P5: goonchem_vortex's second step, recipes.dm:247-250, calls them
// by a global callback) and _has_trait (gaps X5: the alien's picky claws' callback); the rest of the SDQL wrappers are admin tooling (M10+).

public static partial class GlobalProcs
{
    // tg: code/modules/admin/verbs/SDQL2/SDQL_2_wrappers.dm:259
    public static async DmTask _step_towards(object @ref = null, object trg = null)
    {
        await step_towards(@ref as atom_movable, trg);
    }

    // tg: code/modules/admin/verbs/SDQL2/SDQL_2_wrappers.dm:262
    public static async DmTask _step_away(object @ref = null, object trg = null, object max = null)
    {
        await step_away(@ref as atom_movable, trg, max == null ? 5 : Dm.num(max));
    }

    // tg: code/modules/admin/verbs/SDQL2/SDQL_2_wrappers.dm:265
    public static object _has_trait(datum thing = null, object trait = null)
    {
        return Dm.num(HAS_TRAIT(thing, trait));
    }
}
