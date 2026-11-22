// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/machine_unloading.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L2b (D304): the whole file. The var block is generated (D111).

/**********************Unloading unit**************************/

// tg: code/modules/mining/machine_unloading.dm:4
public partial class obj_machinery_mineral_unloading_machine : obj_machinery_mineral
{
    // tg: code/modules/mining/machine_unloading.dm:14
    public override void pickup_item(datum source = null, atom_movable target = null, atom old_loc = null, DmList old_locs = null)
    {
        if (QDELETED(target))
            return;
        Waiting.NoSleep(pickup_item__body(target));
    }

    private async DmTask pickup_item__body(atom_movable target)
    {
        if (istype(target, typeof(obj_structure_ore_box)))
        {
            obj_structure_ore_box box = (obj_structure_ore_box)target;
            foreach (obj_item_stack_ore O in Dm.each<obj_item_stack_ore>(box))
                await unload_mineral(O);
        }
        else if (istype(target, typeof(obj_item_stack_ore)))
        {
            obj_item_stack_ore O = (obj_item_stack_ore)target;
            await unload_mineral(O);
        }
    }
}
