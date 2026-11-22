// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/spawners/random/bedsheet.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3c, D316, with the bedsheets): the random single (or double) bedsheet of any kind. The var blocks are
// generated (D111).

// tg: code/game/objects/effects/spawners/random/bedsheet.dm:46
public partial class obj_effect_spawner_random_bedsheet_any : obj_effect_spawner_random_bedsheet
{
    // tg: code/game/objects/effects/spawners/random/bedsheet.dm:52
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        DmList bedsheet_list = TgState.Current.obj_effect_spawner_random_bedsheet_any__bedsheet_list ??= Dm.list();
        if (bedsheet_list[spawn_type] == null)
        {
            DmList spawn_list = Dm.list();
            foreach (Type sheet in Dm.each_cast<Type>(typesof(typeof(obj_item_bedsheet))))
            {
                if (Dm.eq(Dm.initial(sheet, (obj_item_bedsheet b) => b.bedsheet_type), spawn_type))
                    Dm.append(ref spawn_list, sheet);
            }
            bedsheet_list[spawn_type] = spawn_list;
        }
        loot = bedsheet_list[spawn_type] as DmList;
        return base.Initialize(mapload, __extra);
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/effects/spawners/random/bedsheet.dm:49  var/static/list/bedsheet_list
    internal DmList obj_effect_spawner_random_bedsheet_any__bedsheet_list;
}
