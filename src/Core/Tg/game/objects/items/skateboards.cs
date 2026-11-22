// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/skateboards.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the holy skateboard's Initialize translated (gaps OD2, D333): it needs nothing of the vehicles. The skateboard's
// attack_self (:17), which puts the board on its wheels as a ridden /obj/vehicle/ridden/scooter/skateboard and buckles its user on
// it, waits for the scooters (a later area; not-translated.md 218), so the boards stay inert until then. The var blocks are
// generated (D111).

// tg: code/game/objects/items/skateboards.dm:50
public partial class obj_item_melee_skateboard_holyboard : obj_item_melee_skateboard
{
    // tg: code/game/objects/items/skateboards.dm:63
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_nullrod_core)));
        return dot;
    }
}
