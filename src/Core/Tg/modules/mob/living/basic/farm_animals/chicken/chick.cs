// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/farm_animals/chicken/chick.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

// tg: code/modules/mob/living/basic/farm_animals/chicken/chick.dm:7
public partial class mob_living_basic_chick : mob_living_basic
{
    // tg: code/modules/mob/living/basic/farm_animals/chicken/chick.dm:46
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        pixel_x = base_pixel_x + rand(-6, 6);
        pixel_y = base_pixel_y + rand(0, 10);
        ADD_TRAIT(this, TRAIT_VENTCRAWLER_ALWAYS, INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_pet_bonus), "chirp"));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_CHICKEN, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), FOOTSTEP_MOB_CLAW));
        Waiting.NoSleep(AddElement(typeof(datum_element_can_be_held)));

        if (grow_as != null) // we don't have a set time to grow up beyond whatever RNG dictates, and if we somehow get a client, all growth halts.
            Waiting.NoSleep(AddComponent(typeof(datum_component_growth_and_differentiation), null, grow_as, 100f, 0.5f, 1f, null, Dm.list(COMSIG_MOB_CLIENT_LOGIN), Dm.@new<datum_callback>(this, nameof(ready_to_grow))));
        return dot;
    }

    /// We don't grow into a chicken if we're not conscious.
    // tg: code/modules/mob/living/basic/farm_animals/chicken/chick.dm:70
    public virtual object ready_to_grow()
    {
        return !IS_UNCONSCIOUS_OR_CRIT(this) && loc is not obj_item_mob_holder ? TRUE : FALSE;
    }
}
