// Translated from tgstation (snapshot 1d99bd7f): code/datums/id_trim/centcom.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P5 (the lead): the Central Command trims' New (the var blocks are generated). SSid_access creates every trim at its
// Initialize.

// tg: code/datums/id_trim/centcom.dm:54
public partial class datum_id_trim_centcom_intern_head : datum_id_trim_centcom_intern
{
    // tg: code/datums/id_trim/centcom.dm:54
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        Dm.combine(ref access, SSid_access.get_flag_access_list(ACCESS_FLAG_COMMON));
    }
}

// tg: code/datums/id_trim/centcom.dm:88
public partial class datum_id_trim_centcom_specops_officer : datum_id_trim_centcom
{
    // tg: code/datums/id_trim/centcom.dm:88
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        access = SSid_access.get_region_access_list(Dm.list(REGION_CENTCOM, REGION_ALL_STATION)) as DmList;
    }
}

// tg: code/datums/id_trim/centcom.dm:97
public partial class datum_id_trim_centcom_admiral : datum_id_trim_centcom
{
    // tg: code/datums/id_trim/centcom.dm:97
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        access = SSid_access.get_region_access_list(Dm.list(REGION_CENTCOM, REGION_ALL_STATION)) as DmList;
    }
}

// tg: code/datums/id_trim/centcom.dm:108
public partial class datum_id_trim_centcom_commander : datum_id_trim_centcom
{
    // tg: code/datums/id_trim/centcom.dm:108
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        access = SSid_access.get_region_access_list(Dm.list(REGION_CENTCOM, REGION_ALL_STATION)) as DmList;
    }
}

// tg: code/datums/id_trim/centcom.dm:122
public partial class datum_id_trim_centcom_deathsquad : datum_id_trim_centcom
{
    // tg: code/datums/id_trim/centcom.dm:122
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        access = Dm.bitor(Dm.sub(SSid_access.get_region_access_list(Dm.list(REGION_CENTCOM)), ACCESS_CENT_CAPTAIN), SSid_access.get_region_access_list(Dm.list(REGION_ALL_STATION))) as DmList;
    }
}

// tg: code/datums/id_trim/centcom.dm:133
public partial class datum_id_trim_centcom_ert : datum_id_trim_centcom
{
    // tg: code/datums/id_trim/centcom.dm:133
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        access = Dm.bitor(Dm.list(ACCESS_CENT_GENERAL), Dm.sub(SSid_access.get_region_access_list(Dm.list(REGION_ALL_STATION)), ACCESS_CHANGE_IDS)) as DmList;
    }
}

// tg: code/datums/id_trim/centcom.dm:144
public partial class datum_id_trim_centcom_ert_commander : datum_id_trim_centcom_ert
{
    // tg: code/datums/id_trim/centcom.dm:144
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        access = Dm.bitor(Dm.sub(SSid_access.get_region_access_list(Dm.list(REGION_CENTCOM)), ACCESS_CENT_CAPTAIN), SSid_access.get_region_access_list(Dm.list(REGION_ALL_STATION))) as DmList;
    }
}

// tg: code/datums/id_trim/centcom.dm:159
public partial class datum_id_trim_centcom_ert_security : datum_id_trim_centcom_ert
{
    // tg: code/datums/id_trim/centcom.dm:159
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        access = Dm.bitor(Dm.list(ACCESS_CENT_GENERAL, ACCESS_CENT_SPECOPS, ACCESS_CENT_LIVING), Dm.sub(SSid_access.get_region_access_list(Dm.list(REGION_ALL_STATION)), ACCESS_CHANGE_IDS)) as DmList;
    }
}

// tg: code/datums/id_trim/centcom.dm:172
public partial class datum_id_trim_centcom_ert_engineer : datum_id_trim_centcom_ert
{
    // tg: code/datums/id_trim/centcom.dm:172
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        access = Dm.bitor(Dm.list(ACCESS_CENT_GENERAL, ACCESS_CENT_SPECOPS, ACCESS_CENT_LIVING, ACCESS_CENT_STORAGE), Dm.sub(SSid_access.get_region_access_list(Dm.list(REGION_ALL_STATION)), ACCESS_CHANGE_IDS)) as DmList;
    }
}

// tg: code/datums/id_trim/centcom.dm:188
public partial class datum_id_trim_centcom_ert_medical : datum_id_trim_centcom_ert
{
    // tg: code/datums/id_trim/centcom.dm:188
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        access = Dm.bitor(Dm.list(ACCESS_CENT_GENERAL, ACCESS_CENT_SPECOPS, ACCESS_CENT_MEDICAL, ACCESS_CENT_LIVING), Dm.sub(SSid_access.get_region_access_list(Dm.list(REGION_ALL_STATION)), ACCESS_CHANGE_IDS)) as DmList;
    }
}

// tg: code/datums/id_trim/centcom.dm:204
public partial class datum_id_trim_centcom_ert_chaplain : datum_id_trim_centcom_ert
{
    // tg: code/datums/id_trim/centcom.dm:204
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        access = Dm.bitor(Dm.list(ACCESS_CENT_GENERAL, ACCESS_CENT_SPECOPS, ACCESS_CENT_LIVING), Dm.sub(SSid_access.get_region_access_list(Dm.list(REGION_ALL_STATION)), ACCESS_CHANGE_IDS)) as DmList;
    }
}

// tg: code/datums/id_trim/centcom.dm:220
public partial class datum_id_trim_centcom_ert_janitor : datum_id_trim_centcom_ert
{
    // tg: code/datums/id_trim/centcom.dm:220
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        access = Dm.bitor(Dm.list(ACCESS_CENT_GENERAL, ACCESS_CENT_LIVING), Dm.sub(SSid_access.get_region_access_list(Dm.list(REGION_ALL_STATION)), ACCESS_CHANGE_IDS)) as DmList;
    }
}

// tg: code/datums/id_trim/centcom.dm:233
public partial class datum_id_trim_centcom_ert_clown : datum_id_trim_centcom_ert
{
    // tg: code/datums/id_trim/centcom.dm:233
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        access = Dm.bitor(Dm.list(ACCESS_CENT_GENERAL, ACCESS_CENT_LIVING), Dm.sub(SSid_access.get_region_access_list(Dm.list(REGION_ALL_STATION)), ACCESS_CHANGE_IDS)) as DmList;
    }
}
