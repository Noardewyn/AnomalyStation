// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/spawners/random/structure.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M3 I1d, the map-preview spawners): only the procs that kept Meta's spawners inert, so they spawn their loot and
// delete themselves as tg's do (tg: "If you see this while in a shift, please create a bug report"). The var blocks are generated.

// tg: code/game/objects/effects/spawners/random/structure.dm:99
public partial class obj_effect_spawner_random_structure_closet_empty : obj_effect_spawner_random_structure
{
    // tg: code/game/objects/effects/spawners/random/structure.dm:108
    public override async DmTask<object> make_item(object spawn_loc = null, object type_path_to_make = null)
    {
        object peek_a_boo = await base.make_item(spawn_loc, type_path_to_make);
        if (istype(peek_a_boo, typeof(obj_structure_closet)) && prob(50))
            await ((obj_structure_closet)peek_a_boo).open(special_effects: FALSE); //the crate appears immediatly out of thin air so no need to animate anything

        return peek_a_boo;
    }
}

// tg: code/game/objects/effects/spawners/random/structure.dm:132
public partial class obj_effect_spawner_random_structure_closet_empty_crate_with_loot : obj_effect_spawner_random_structure_closet_empty_crate
{
    // tg: code/game/objects/effects/spawners/random/structure.dm:136
    public override async DmTask<object> make_item(object spawn_loc = null, object type_path_to_make = null)
    {
        object closet_to_fill = await base.make_item(spawn_loc, type_path_to_make);
        Dm.Deferred("code/game/objects/effects/spawners/random/structure.dm:138", "crate with_loot make_item: RegisterSignal(COMSIG_CLOSET_CONTENTS_INITIALIZED, populate_with_random_maint_loot), the crate's maintenance loot (closet contents, M4a)");

        return closet_to_fill;
    }
}
