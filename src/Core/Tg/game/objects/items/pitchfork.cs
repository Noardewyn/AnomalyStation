// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/pitchfork.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps OD2 (D333): the file whole. The pitchfork jousts, is a walking aid and is wielded in two hands for 15 force ("pitchfork1"
// when wielded, "pitchfork0" in one hand). The var block and its armour are generated (D111).

/**
 * Pitchfork item
 *
 * Essentially spears with different stats and sprites.
 * Also fireproof for some reason.
 */
// tg: code/game/objects/items/pitchfork.dm:7
public partial class obj_item_pitchfork : obj_item
{
    // tg: code/game/objects/items/pitchfork.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_jousting)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed), Dm.named("force_unwielded", 7), Dm.named("force_wielded", 15), Dm.named("icon_wielded", Dm.text("[]1", base_icon_state))));
        Waiting.NoSleep(AddComponent(typeof(datum_component_walking_aid)));
        return dot;
    }

    // tg: code/game/objects/items/pitchfork.dm:38
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]0", base_icon_state);
        return await base.update_icon_state(__extra);
    }
}
