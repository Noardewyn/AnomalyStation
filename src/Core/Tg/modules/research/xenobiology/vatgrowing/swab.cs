// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/vatgrowing/swab.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated (D111).

///Tool capable of taking biological samples from mobs
// tg: code/modules/research/xenobiology/vatgrowing/swab.dm:2
public partial class obj_item_swab : obj_item
{
    ///Adds the swabbing component to the biopsy tool
    // tg: code/modules/research/xenobiology/vatgrowing/swab.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_swabbing), TRUE, TRUE, FALSE, null, Dm.@new<datum_callback>(this, nameof(update_swab_icon)), Dm.named("max_items", 1)));
        return dot;
    }

    // tg: code/modules/research/xenobiology/vatgrowing/swab.dm:15
    public virtual void update_swab_icon(object overlays = null, DmList swabbed_items = null)
    {
        if (Dm.truthy(LAZYLEN(swabbed_items)))
        {
            datum_biological_sample sample = LAZYACCESS(swabbed_items, 1) as datum_biological_sample; //Use the first one as our target
            object swab_overlay = mutable_appearance(icon, Dm.text("swab_[]", sample.sample_color), appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART));
            Dm.append(ref overlays, swab_overlay);
        }
    }
}
