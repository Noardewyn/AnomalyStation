// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/broken_flooring.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps ST2 (D333): the whole file (the broken tiling decals: on plating, or always for the floorplane kinds, they lie on the floor
// plane and show what is under the floor on their tile; elsewhere they are drawn as trash on the game plane; a crowbar reclaims a
// tile). The directional helpers (MAPPING_DIRECTIONAL_HELPERS) are var-only. The var blocks are generated (D111).

// tg: code/game/objects/structures/broken_flooring.dm:1
public partial class obj_structure_broken_flooring : obj_structure
{
    // tg: code/game/objects/structures/broken_flooring.dm:14
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/game/objects/structures/broken_flooring.dm:18
    public override void LateInitialize(params object[] __extra)
    {
        turf turf = get_turf(this) as turf;
        if (!isplatingturf(turf) && !Dm.truthy(always_floorplane)) // Render as trash if not on plating
        {
            plane = GAME_PLANE;
            layer = LOW_OBJ_LAYER;
            return;
        }
        foreach (obj @object in Dm.each<obj>(turf))
        {
            if (Dm.truthy(Dm.bitand(@object.flags_1, INITIALIZED_1)))
            {
                @object.SEND_SIGNAL(COMSIG_OBJ_HIDE, Dm.list(@object, UNDERFLOOR_VISIBLE));
                CHECK_TICK();
            }
        }
    }

    // tg: code/game/objects/structures/broken_flooring.dm:29
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        tool.play_tool_sound(this, 80);
        balloon_alert(user, I18n.T("tile reclaimed"));
        Dm.@new<obj_item_stack_tile_iron>(get_turf(this));
        qdel(this);
        await DmTask.CompletedTask;
        return ITEM_INTERACT_SUCCESS;
    }
}
