// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/traits/declarations.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The 804 trait constants of this file are generated into Defines
// (src/Core/Tg/Generated/Defines/__DEFINES/traits/declarations.g.cs); its two parametrized trait names follow.

public static partial class GlobalProcs
{
    /// GLOB trait, applied whenever something in the world wants to use the distortion plane
    /// Distortion is an expensive effect, so it's worthwhile to keep it off until we care
    // tg: code/__DEFINES/traits/declarations.dm:15
    public static string TRAIT_DISTORTION_IN_USE(object z_layer) => Dm.text("distortion_in_use_#[]", z_layer);

    // Client traits
    /// This client has parallax displayed on it on the specified map
    // tg: code/__DEFINES/traits/declarations.dm:58
    public static string TRAIT_PARALLAX_DISPLAYED(object submap) => Dm.text("parallax_displayed_[]_map", submap);
}
