// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/vatgrowing/biopsy_tool.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated (D111).

///Tool capable of taking biological samples from mobs
// tg: code/modules/research/xenobiology/vatgrowing/biopsy_tool.dm:2
public partial class obj_item_biopsy_tool : obj_item
{
    ///Adds the swabbing component to the biopsy tool
    // tg: code/modules/research/xenobiology/vatgrowing/biopsy_tool.dm:14
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_swabbing), can_swap_objects, FALSE, TRUE, Dm.@new<datum_callback>(this, nameof(update_swab_icon)), Dm.named("max_items", 1)));
        return dot;
    }

    // tg: code/modules/research/xenobiology/vatgrowing/biopsy_tool.dm:18
    public virtual void update_swab_icon(DmList swabbed_items = null)
    {
        if (Dm.truthy(LAZYLEN(swabbed_items)))
            icon_state = Dm.text("[]_full", base_icon_state);
        else
            icon_state = Dm.str(base_icon_state);
    }
}
