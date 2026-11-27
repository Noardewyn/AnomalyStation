// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapfluff/ruins/icemoonruin_code/wrath.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the butchering gloves (a Lavaland ruin's loot as well as the ice moon's), which let their wearer butcher bodies
// with bare hands. The var block is generated.

// tg: code/modules/mapfluff/ruins/icemoonruin_code/wrath.dm:1
public partial class obj_item_clothing_gloves_butchering : obj_item_clothing_gloves
{
    // tg: code/modules/mapfluff/ruins/icemoonruin_code/wrath.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering_wearable),
            Dm.named("speed", 0.5f * SECONDS),
            Dm.named("effectiveness", 125),
            Dm.named("bonus_modifier", 0),
            Dm.named("butcher_sound", null),
            Dm.named("disabled", TRUE),
            Dm.named("can_be_blunt", TRUE)));
        return dot;
    }
}
