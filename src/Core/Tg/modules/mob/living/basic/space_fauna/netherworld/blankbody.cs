// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/netherworld/blankbody.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

// tg: code/modules/mob/living/basic/space_fauna/netherworld/blankbody.dm:1
public partial class mob_living_basic_blankbody : mob_living_basic
{
    // tg: code/modules/mob/living/basic/space_fauna/netherworld/blankbody.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_NETHER, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 0f));
        Waiting.NoSleep(AddComponent(typeof(datum_component_health_scaling_effects), Dm.named("min_health_attack_modifier_lower", 8f), Dm.named("min_health_attack_modifier_upper", 14f)));
        return dot;
    }
}
