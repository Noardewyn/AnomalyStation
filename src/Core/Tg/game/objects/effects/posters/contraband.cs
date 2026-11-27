// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/posters/contraband.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the random contraband poster's Initialize; the contraband posters are data (their var blocks are generated), and the
// pinup poster's pick is not on MetaStation.

// These icon_states may be overridden, but are for mapper's convinence
// tg: code/game/objects/effects/posters/contraband.dm:2
public partial class obj_item_poster_random_contraband : obj_item_poster
{
    // tg: code/game/objects/effects/posters/contraband.dm:7
    [DmParams("mapload", "new_poster_structure")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_CONTRABAND, INNATE_TRAIT);
        return dot;
    }
}
