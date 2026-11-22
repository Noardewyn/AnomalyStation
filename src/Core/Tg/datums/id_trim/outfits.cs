// Translated from tgstation (snapshot 1d99bd7f): code/datums/id_trim/outfits.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P5 (the lead): the outfit trims' New (the var blocks are generated). SSid_access creates every trim at its Initialize.

// tg: code/datums/id_trim/admin.dm:17
public partial class datum_id_trim_highlander : datum_id_trim
{
    // The later of tg's two definitions of New at this type (rules 2.5, 7.5): its ..() is the earlier one (admin.dm:23).
    // tg: code/datums/id_trim/outfits.dm:12
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await New__d0(__extra);
        access = SSid_access.get_region_access_list(Dm.list(REGION_ALL_STATION)) as DmList;
    }
}

// tg: code/datums/id_trim/outfits.dm:25
public partial class datum_id_trim_vr : datum_id_trim
{
    // tg: code/datums/id_trim/outfits.dm:25
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        Dm.combine(ref access, SSid_access.get_region_access_list(Dm.list(REGION_ALL_STATION)));
    }
}

// tg: code/datums/id_trim/outfits.dm:35
public partial class datum_id_trim_vr_operative : datum_id_trim_vr
{
    // tg: code/datums/id_trim/outfits.dm:35
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        Dm.combine(ref access, Dm.list(ACCESS_SYNDICATE, ACCESS_MAINT_TUNNELS));
    }
}

// tg: code/datums/id_trim/outfits.dm:46
public partial class datum_id_trim_tunnel_clown : datum_id_trim
{
    // tg: code/datums/id_trim/outfits.dm:46
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        Dm.combine(ref access, SSid_access.get_region_access_list(Dm.list(REGION_ALL_STATION)));
    }
}

// tg: code/datums/id_trim/outfits.dm:77
public partial class datum_id_trim_cyber_police : datum_id_trim
{
    // tg: code/datums/id_trim/outfits.dm:77
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        Dm.combine(ref access, SSid_access.get_region_access_list(Dm.list(REGION_ALL_GLOBAL)));
    }
}
