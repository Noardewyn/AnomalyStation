// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapping/mail_sorting_helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps G3, D291): the helper base; its 35 subtypes only set sort_type and icon_state (generated var blocks).

// tg: code/modules/mapping/mail_sorting_helpers.dm:1
public partial class obj_effect_mapping_helpers_mail_sorting : obj_effect_mapping_helpers
{
    // tg: code/modules/mapping/mail_sorting_helpers.dm:6
    public override void LateInitialize(params object[] __extra)
    {
        obj_structure_disposalpipe_sorting_mail mail_sorter = Dm.locate<obj_structure_disposalpipe_sorting_mail>(loc);
        if (mail_sorter != null)
            Dm.combine(ref mail_sorter.sortTypes, sort_type);
        else
            log_mapping(Dm.text("[] failed to find a mail sorting disposal pipe at []", this, AREACOORD(this)));
        qdel(this);
    }
}
