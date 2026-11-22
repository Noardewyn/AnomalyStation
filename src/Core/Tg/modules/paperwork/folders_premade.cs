// Translated from tgstation (snapshot 1d99bd7f): code/modules/paperwork/folders_premade.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S3, D298): the folders that come with their documents. The var blocks are generated.

// tg: code/modules/paperwork/folders_premade.dm:21
public partial class obj_item_folder_documents : obj_item_folder
{
    // tg: code/modules/paperwork/folders_premade.dm:25
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.@new<obj_item_documents_nanotrasen>(this);
        update_appearance();
        return dot;
    }
}

// tg: code/modules/paperwork/folders_premade.dm:36
public partial class obj_item_folder_syndicate_red : obj_item_folder_syndicate
{
    // tg: code/modules/paperwork/folders_premade.dm:39
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.@new<obj_item_documents_syndicate_red>(this);
        update_appearance();
        return dot;
    }
}

// tg: code/modules/paperwork/folders_premade.dm:44
public partial class obj_item_folder_syndicate_blue : obj_item_folder_syndicate
{
    // tg: code/modules/paperwork/folders_premade.dm:47
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.@new<obj_item_documents_syndicate_blue>(this);
        update_appearance();
        return dot;
    }
}

// tg: code/modules/paperwork/folders_premade.dm:52
public partial class obj_item_folder_syndicate_mining : obj_item_folder_syndicate
{
    // tg: code/modules/paperwork/folders_premade.dm:52
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.@new<obj_item_documents_syndicate_mining>(this);
        update_appearance();
        return dot;
    }
}

// tg: code/modules/paperwork/folders_premade.dm:57
public partial class obj_item_folder_ancient_paperwork : obj_item_folder
{
    // tg: code/modules/paperwork/folders_premade.dm:57
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.@new<obj_item_paperwork_ancient>(this);
        update_appearance();
        return dot;
    }
}
