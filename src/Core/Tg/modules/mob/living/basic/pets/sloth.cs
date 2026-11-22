// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/pets/sloth.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

// tg: code/modules/mob/living/basic/pets/sloth.dm:4
public partial class mob_living_basic_sloth : mob_living_basic
{
    // tg: code/modules/mob/living/basic/pets/sloth.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_pet_bonus), "ssmile"));
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), FOOTSTEP_MOB_CLAW));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_can_be_held)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_tree_climber)));

        if (!Dm.truthy(mapload) || GLOB.cargo_sloth != null || !Dm.truthy(is_station_level(z)))
            return dot;

        // If someone adds non-cargo sloths to maps we'll have a problem but we're fine for now
        GLOB.cargo_sloth = this;
        Dm.append(ref GLOB.gorilla_start, get_turf(this));
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/sloth.dm:65
    public override object Destroy(float? force = null)
    {
        if (GLOB.cargo_sloth == this)
            GLOB.cargo_sloth = null;

        return base.Destroy(force);
    }
}
