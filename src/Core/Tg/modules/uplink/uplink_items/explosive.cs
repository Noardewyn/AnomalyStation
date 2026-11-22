// Translated from tgstation (snapshot 1d99bd7f): code/modules/uplink/uplink_items/explosive.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A2 (D316): the entries' procs (the entries and their categories are the generated var blocks).

// tg: code/modules/uplink/uplink_items/explosive.dm:59
public partial class datum_uplink_item_explosives_emp : datum_uplink_item_explosives
{
    // tg: code/modules/uplink/uplink_items/explosive.dm:65
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (HAS_TRAIT(SSstation, STATION_TRAIT_CYBERNETIC_REVOLUTION))
            cost *= 3;
    }
}

// tg: code/modules/uplink/uplink_items/explosive.dm:110
public partial class datum_uplink_item_explosives_syndicate_bomb : datum_uplink_item_explosives
{
    // tg: code/modules/uplink/uplink_items/explosive.dm:131
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        desc = replacetext(desc, "%MIN_BOMB_TIMER", SYNDIEBOMB_MIN_TIMER_SECONDS);
    }
}

// tg: code/modules/uplink/uplink_items/explosive.dm:97
public partial class datum_uplink_item_explosives_syndicate_bomb_emp : datum_uplink_item_explosives_syndicate_bomb
{
    // tg: code/modules/uplink/uplink_items/explosive.dm:104
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (HAS_TRAIT(SSstation, STATION_TRAIT_CYBERNETIC_REVOLUTION))
            cost *= 2;
    }
}
