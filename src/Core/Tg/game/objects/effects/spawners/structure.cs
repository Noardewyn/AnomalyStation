// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/spawners/structure.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/*
Because mapping is already tedious enough this spawner let you spawn generic
"sets" of objects rather than having to make the same object stack again and
again.
*/

// PARTIAL: the structure spawner, the window spawner and the flipped table spawner translated (M3 P4: Meta's window
// spawners, a grille and its windows); the hollow window spawners (end, middle, directional of each material), which
// pick their spawn_list by dir in their own Initialize, come with their maps (they stay inert, invisible spawners).

// tg: code/game/objects/effects/spawners/structure.dm:7
public partial class obj_effect_spawner_structure : obj_effect_spawner
{
    // tg: code/game/objects/effects/spawners/structure.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        foreach (object spawn_type in Dm.each(spawn_list))
            Dm.@new((Type)spawn_type, loc);
        return dot;
    }
}

// tg: code/game/objects/effects/spawners/structure.dm:18
public partial class obj_effect_spawner_structure_window : obj_effect_spawner_structure
{
    // tg: code/game/objects/effects/spawners/structure.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);

        turf current_turf = loc as turf;
        current_turf.rcd_memory = RCD_MEMORY_WINDOWGRILLE;
        return dot;
    }
}

// tg: code/game/objects/effects/spawners/structure.dm:446
public partial class obj_effect_spawner_structure_flipped_table : obj_effect_spawner_structure
{
    // tg: code/game/objects/effects/spawners/structure.dm:453
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        obj_structure_table table_to_flip = Dm.@new(table_to_spawn, loc) as obj_structure_table;
        table_to_flip.dir = dir;
        RegisterSignal(table_to_flip, COMSIG_ATOM_SMOOTHED_ICON, nameof(on_icon_smoothed));
        return dot;
    }

    /// The flip_table() proc HAS to be run after smooth_icon() is completed or else we will get runtimes.
    // tg: code/game/objects/effects/spawners/structure.dm:460
    [SignalHandler]
    public virtual void on_icon_smoothed(obj_structure_table table_to_flip = null)
    {
        Waiting.NoSleep(table_to_flip.flip_table(table_to_flip.dir));
        UnregisterSignal(table_to_flip, COMSIG_ATOM_SMOOTHED_ICON);
    }
}
