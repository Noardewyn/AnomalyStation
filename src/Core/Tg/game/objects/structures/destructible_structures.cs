// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/destructible_structures.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST5 (D333): the file whole. The base of the destructible structures (the cult's and the heretic's, the maintenance pants
// altar): broken, it drops its debris and shows its break message and sound. The var block is generated (D111).

//a base for destructible structures
// tg: code/game/objects/structures/destructible_structures.dm:1
public partial class obj_structure_destructible : obj_structure
{
    // tg: code/game/objects/structures/destructible_structures.dm:7
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        if (!Dm.truthy(disassembled))
        {
            if (islist(debris))
            {
                foreach (object I in Dm.each(debris))
                {
                    for (float i = 1; i <= Dm.num(debris[I]); i++)
                        Dm.@new((Type)I, get_turf(this));
                }
            }
        }
        if (Dm.truthy(break_message))
            visible_message(break_message);
        if (Dm.truthy(break_sound))
            playsound(this, break_sound, 50, TRUE);
        await DmTask.CompletedTask;
    }
}
