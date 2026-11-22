// Translated from tgstation (snapshot 1d99bd7f): code/modules/uplink/uplink_items/stealthy_tools.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A2 (D316): the entries' procs (the entries and their categories are the generated var blocks). The telecomms and
// power blackouts' spawn_item (force_event of the round events, not ported) are not translated, so both stay hidden (DV343;
// not-translated.md section 131).

// tg: code/modules/uplink/uplink_items/stealthy_tools.dm:60
public partial class datum_uplink_item_stealthy_tools_emplight : datum_uplink_item_stealthy_tools
{
    // tg: code/modules/uplink/uplink_items/stealthy_tools.dm:68
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (HAS_TRAIT(SSstation, STATION_TRAIT_CYBERNETIC_REVOLUTION))
            cost *= 3;
    }
}
