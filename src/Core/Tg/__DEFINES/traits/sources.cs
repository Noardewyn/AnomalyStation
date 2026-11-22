// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/traits/sources.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The 189 trait source constants of this file are generated into Defines
// (src/Core/Tg/Generated/Defines/__DEFINES/traits/sources.g.cs); its one parametrized source follows.

public static partial class GlobalProcs
{
    /// Trait granted from the metabolization of a reagent
    // tg: code/__DEFINES/traits/sources.dm:347
    public static string METABOLIZATION_TRAIT(object type) => Dm.text("metabolize:[]", type);
}
