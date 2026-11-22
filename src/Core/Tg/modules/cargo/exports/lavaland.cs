// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/exports/lavaland.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4b (D327): the whole file: the artifact's name by the map's mine, and the megafauna loot's thank-you note. The var blocks are generated.

// tg: code/modules/cargo/exports/lavaland.dm:3
public partial class datum_export_lavaland : datum_export
{
    // tg: code/modules/cargo/exports/lavaland.dm:9
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        // (SSmapping?: a test harness that builds GLOB without SSmapping, rules 10.6)
        switch (SSmapping?.current_map?.minetype)
        {
            case MINETYPE_NONE:
                unit_name = I18n.T("unknown artifact");
                break;
            case MINETYPE_LAVALAND:
                unit_name = I18n.T("lava planet artifact");
                break;
            case MINETYPE_ICE:
                unit_name = I18n.T("ice moon artifact");
                break;
        }

        if (Dm.truthy(prefix))
            unit_name = Dm.text("[] []", prefix, unit_name);
    }
}

// tg: code/modules/cargo/exports/lavaland.dm:67
public partial class datum_export_lavaland_megafauna : datum_export_lavaland
{
    //in the unlikely case a miner feels like selling megafauna loot
    // tg: code/modules/cargo/exports/lavaland.dm:87
    public override object total_printout(datum_export_report ex = null, object notes = null)
    {
        notes ??= TRUE;
        object dot = base.total_printout(ex, notes);
        if (Dm.truthy(dot) && Dm.truthy(notes))
            dot = Dm.add(dot, I18n.T(" On behalf of the Nanotrasen RnD division: Thank you for your hard work."));
        return dot;
    }
}
