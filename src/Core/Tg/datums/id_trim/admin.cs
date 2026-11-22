// Translated from tgstation (snapshot 1d99bd7f): code/datums/id_trim/admin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P5 (the lead): the trims' New (the var blocks are generated). SSid_access creates every trim at its Initialize.

// tg: code/datums/id_trim/admin.dm:2
public partial class datum_id_trim_admin : datum_id_trim
{
    // tg: code/datums/id_trim/admin.dm:11
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        // Every single access in the game, all on one handy trim.
        access = SSid_access.get_region_access_list(Dm.list(REGION_ALL_GLOBAL)) as DmList;
    }
}

// tg: code/datums/id_trim/admin.dm:17
public partial class datum_id_trim_highlander : datum_id_trim
{
    // The earlier of tg's two definitions of New at this type (rules 2.5, 7.5); the later one (outfits.dm:12, outfits.cs)
    // calls it with ..().
    // tg: code/datums/id_trim/admin.dm:23
    public async DmTask New__d0(params object[] __extra)
    {
        await base.New(__extra);
        access = SSid_access.get_region_access_list(Dm.list(REGION_CENTCOM, REGION_ALL_STATION)) as DmList;
    }
}
