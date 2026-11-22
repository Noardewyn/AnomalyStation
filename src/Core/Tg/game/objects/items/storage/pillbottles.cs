// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/pillbottles.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice C: the whole file but the suicide (a live-partial row, M10): the pill bottles fill with spawn_count pills of
// spawn_type (the subtypes are var blocks, generated), the maintenance pill bottle names itself after its pills.

/*
 * Pill Bottles
 */
// tg: code/game/objects/items/storage/pillbottles.dm:4
public partial class obj_item_storage_pill_bottle : obj_item_storage
{
    // tg: code/game/objects/items/storage/pillbottles.dm:29
    public override async DmTask PopulateContents()
    {
        if (!Dm.truthy(spawn_count))
            return;

        for (float i = 1; i <= Dm.num(spawn_count); i++)
            Dm.@new(spawn_type, this);
    }
}

// tg: code/game/objects/items/storage/pillbottles.dm:149
public partial class obj_item_storage_pill_bottle_maintenance_pill : obj_item_storage_pill_bottle
{
    // tg: code/game/objects/items/storage/pillbottles.dm:154
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (!Dm.truthy(spawn_count))
            spawn_count = rand(1, 7);
        object dot = base.Initialize(mapload, __extra);
        obj_item_reagent_containers_applicator_pill P = Dm.locate<obj_item_reagent_containers_applicator_pill>(this);
        name = I18n.T("bottle of []s", P.name);
        return dot;
    }
}
