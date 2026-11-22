// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/is_helpers.dm and __DEFINES/food.dm (the AI's few macros)
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tg;

// PARTIAL: only iswaterturf and IS_EDIBLE (gaps package C1).
public static partial class GlobalProcs
{
    // tg: code/__DEFINES/is_helpers.dm  #define iswaterturf(A) (istype(A, /turf/open/water))
    public static bool iswaterturf(object A) => A is turf_open_water;

    // tg: code/__DEFINES/food.dm  #define IS_EDIBLE(O) (O.GetComponent(/datum/component/edible))
    public static bool IS_EDIBLE(object O) => Dm.truthy(((datum)O).GetComponent(typeof(datum_component_edible)));

    /// can_see with a value a blackboard or a var holds (null keeps tg's default length).
    public static object can_see(atom source, atom target, object length) => can_see(source, target, length == null ? (float?)null : Dm.num(length));
}
