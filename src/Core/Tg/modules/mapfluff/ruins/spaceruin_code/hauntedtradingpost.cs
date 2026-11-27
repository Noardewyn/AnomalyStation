// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapfluff/ruins/spaceruin_code/hauntedtradingpost.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the office aquarium with its two donkfish; the trading post's other gimmicks wait for the space ruins. The var
// blocks are generated (D111).

//aquarium with two donkfish in it
// tg: code/modules/mapfluff/ruins/spaceruin_code/hauntedtradingpost.dm:85
public partial class obj_structure_aquarium_donkfish : obj_structure_aquarium
{
    // tg: code/modules/mapfluff/ruins/spaceruin_code/hauntedtradingpost.dm:90
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.@new<obj_item_aquarium_prop_rocks>(this);
        Dm.@new<obj_item_aquarium_prop_seaweed>(this);
        Dm.@new<obj_item_fish_donkfish>(this);
        Dm.@new<obj_item_fish_donkfish>(this);
        return dot;
    }
}
