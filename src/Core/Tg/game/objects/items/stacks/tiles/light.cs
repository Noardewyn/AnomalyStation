// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/tiles/light.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps G2 (D291): the whole file (the light floor's tile: a crowbar takes it apart, a placed tile passes its state on).

// tg: code/game/objects/items/stacks/tiles/light.dm:1
public partial class obj_item_stack_tile_light : obj_item_stack_tile
{
    // tg: code/game/objects/items/stacks/tiles/light.dm:14
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        Dm.@new<obj_item_stack_sheet_iron>(user.loc);
        amount = Dm.num(amount) - 1;
        Dm.@new<obj_item_stack_light_w>(user.loc);
        if (Dm.num(amount) <= 0)
            qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/stacks/tiles/light.dm:23
    public override async DmTask<object> place_tile(turf_open_floor_plating target_plating = null, mob user = null)
    {
        object dot = await base.place_tile(target_plating, user);
        turf_open_floor_light floor = dot as turf_open_floor_light;
        if (floor != null)
            floor.state = state;
        return dot;
    }
}
