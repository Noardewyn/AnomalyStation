// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/wumborian_fugu/wumborian_fugu.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

// tg: code/modules/mob/living/basic/space_fauna/wumborian_fugu/wumborian_fugu.dm:8
public partial class mob_living_basic_wumborian_fugu : mob_living_basic
{
    // tg: code/modules/mob/living/basic/space_fauna/wumborian_fugu/wumborian_fugu.dm:51
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_seethrough_mob)));
        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), typeof(obj_item_fugu_gland)));
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_flying)));
        add_traits(Dm.list(TRAIT_LAVA_IMMUNE, TRAIT_ASHSTORM_IMMUNE), ROUNDSTART_TRAIT);
        expand = Dm.@new<datum_action_cooldown_fugu_expand>(this);
        Waiting.NoSleep(expand.Grant(this));
        (ai_controller as datum_ai_controller).set_blackboard_key(BB_FUGU_INFLATE, expand);
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/wumborian_fugu/wumborian_fugu.dm:61
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref expand);
        return base.Destroy(force);
    }
}
