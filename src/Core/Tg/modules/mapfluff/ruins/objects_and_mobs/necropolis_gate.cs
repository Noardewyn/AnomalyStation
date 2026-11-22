// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L1 (D304): the stone tiles of the boss arenas. The necropolis gates, arches and the legion's gate come with the
// megafauna (L7).

//stone tiles for boss arenas
// tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:251
public partial class obj_structure_stone_tile : obj_structure
{
    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:262
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("[][]", tile_key, rand(1, tile_random_sprite_max));

        TgState state = TgState.Current;
        state.obj_structure_stone_tile_Initialize__give_turf_traits ??= string_list(Dm.list(TRAIT_TURF_IGNORE_SLOWDOWN, TRAIT_LAVA_STOPPED, TRAIT_CHASM_STOPPED, TRAIT_IMMERSE_STOPPED));
        Waiting.NoSleep(AddElement(typeof(datum_element_give_turf_traits), state.obj_structure_stone_tile_Initialize__give_turf_traits));
        return dot;
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:271
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        await DmTask.CompletedTask;
        return null;
    }
}

// rules 10.6: the proc-level static above, made on first use
public sealed partial class TgState
{
    internal object obj_structure_stone_tile_Initialize__give_turf_traits;
}
