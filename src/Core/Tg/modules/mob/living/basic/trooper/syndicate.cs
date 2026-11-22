// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/trooper/syndicate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1a (D327): the viscerator (a strange vent clog's flying blade swarm); the file's syndicate troopers are translated with the other
// troopers in trooper.cs (gaps E2c). The var blocks are generated (D111).

// tg: code/modules/mob/living/basic/trooper/syndicate.dm:216
public partial class mob_living_basic_viscerator : mob_living_basic
{
    // tg: code/modules/mob/living/basic/trooper/syndicate.dm:251
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_flying)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_swarming)));
        return dot;
    }
}
