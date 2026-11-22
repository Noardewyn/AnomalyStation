// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/under/jobs/civilian/clown_mime.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/clothing/under/jobs/civilian/clown_mime.dm:28
public partial class obj_item_clothing_under_rank_civilian_clown : obj_item_clothing_under_rank_civilian
{
    // tg: code/modules/clothing/under/jobs/civilian/clown_mime.dm:38
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_squeak), Dm.list(Dm.kv(Dm.resource("sound/items/bikehorn.ogg"), 1)), 50, Dm.named("falloff_exponent", 20))); //die off quick please
        Dm.Deferred("code/modules/clothing/under/jobs/civilian/clown_mime.dm:41", "Initialize: AddElement(/datum/element/swabable, CELL_LINE_TABLE_CLOWN, ...): the cell lines (the sample swab, not ported)");
        return dot;
    }
}
