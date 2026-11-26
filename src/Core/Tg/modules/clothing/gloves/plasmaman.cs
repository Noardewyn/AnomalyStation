// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/gloves/plasmaman.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP2a (D349): the clown envirogloves' swabbable cell line (the cell lines are not ported); the other envirogloves are var-only,
// generated (D111).

// tg: code/modules/clothing/gloves/plasmaman.dm:80
public partial class obj_item_clothing_gloves_color_plasmaman_clown : obj_item_clothing_gloves_color_plasmaman
{
    // tg: code/modules/clothing/gloves/plasmaman.dm:84
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/gloves/plasmaman.dm:86", "Initialize: AddElement(/datum/element/swabable, CELL_LINE_TABLE_CLOWN, ...): the cell lines (the sample swab, not ported)");
        return dot;
    }
}
