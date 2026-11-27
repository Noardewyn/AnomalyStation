// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapfluff/ruins/spaceruin_code/oldstation/oldstation_cytology.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the old station ruin's petri dish with its cow and grape sample; its cultivation broth is a var-only beaker. The
// var blocks are generated (D111).

// tg: code/modules/mapfluff/ruins/spaceruin_code/oldstation/oldstation_cytology.dm:1
public partial class obj_item_petri_dish_oldstation : obj_item_petri_dish
{
    // tg: code/modules/mapfluff/ruins/spaceruin_code/oldstation/oldstation_cytology.dm:5
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        sample = Dm.@new<datum_biological_sample>();
        Waiting.NoSleep(sample.GenerateSample(CELL_LINE_TABLE_COW, null, 1, 0));
        datum_biological_sample contamination = Dm.@new<datum_biological_sample>();
        Waiting.NoSleep(contamination.GenerateSample(CELL_LINE_TABLE_GRAPE, null, 1, 0));
        sample.Merge(contamination);
        sample.sample_color = COLOR_SAMPLE_BROWN;
        update_appearance();
        return dot;
    }
}
