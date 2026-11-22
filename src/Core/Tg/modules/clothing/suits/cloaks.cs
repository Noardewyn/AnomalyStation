// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/suits/cloaks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The base cloak (gaps J4: the head of security's bag holds one) and the skill capes (gaps SL1, D333: worn only by a legend of their skill,
// the veteran's by a player of five thousand hours); the fancy cloaks' procs are their packages'.

// tg: code/modules/clothing/suits/cloaks.dm:5
public partial class obj_item_clothing_neck_cloak : obj_item_clothing_neck
{
    // tg: code/modules/clothing/suits/cloaks.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_surgery_aid), "cloak"));
        return dot;
    }
}

// tg: code/modules/clothing/suits/cloaks.dm:60
public partial class obj_item_clothing_neck_cloak_skill_reward : obj_item_clothing_neck_cloak
{
    // tg: code/modules/clothing/suits/cloaks.dm:65
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(element_type, associated_skill_path));
        return dot;
    }
}
