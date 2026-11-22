// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/farm_animals/chicken/chicken.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

public partial class datum_controller_global_vars
{
    // tg: code/modules/mob/living/basic/farm_animals/chicken/chicken.dm:2  GLOBAL_VAR_INIT(chicken_count, 0)
    public float chicken_count;
    public virtual void InitGlobalchicken_count()
    {
        chicken_count = 0;
        Dm.append(ref gvars_datum_init_order, "chicken_count");
    }
}

// tg: code/modules/mob/living/basic/farm_animals/chicken/chicken.dm:10
public partial class mob_living_basic_chicken : mob_living_basic
{
    // tg: code/modules/mob/living/basic/farm_animals/chicken/chicken.dm:58
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        GLOB.chicken_count++;
        ADD_TRAIT(this, TRAIT_VENTCRAWLER_ALWAYS, INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_pet_bonus), "cluck"));
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), FOOTSTEP_MOB_CLAW));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_CHICKEN, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));
        Waiting.NoSleep(AddElement(typeof(datum_element_animal_variety), "chicken", pick("brown", "black", "white"), TRUE));
        Waiting.NoSleep(AddElement(typeof(datum_element_can_be_held)));

        Waiting.NoSleep(AddComponent(
            typeof(datum_component_egg_layer),
            typeof(obj_item_food_egg_organic),
            Dm.list(typeof(obj_item_food_grown_wheat)),
            Dm.list("She clucks contently."),
            Dm.list("lays an egg.", "squats down and croons.", "begins making a huge racket.", "begins clucking raucously."), // EGG_LAYING_MESSAGES (__DEFINES/botany.dm:101)
            0f,
            rand(1, 4),
            8f,
            Dm.@new<datum_callback>(this, nameof(egg_laid))
        ));
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/chicken/chicken.dm:79
    public override object Destroy(float? force = null)
    {
        GLOB.chicken_count--;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/farm_animals/chicken/chicken.dm:83
    public virtual void egg_laid(obj_item egg = null)
    {
        if (GLOB.chicken_count <= MAX_CHICKENS && Dm.truthy(fertile) && prob(25))
            Waiting.NoSleep(egg.AddComponent(typeof(datum_component_fertile_egg), typeof(mob_living_basic_chick), 1f, 2f, 200f, 0f, typecacheof(Dm.list(typeof(turf))), TRUE));
    }
}
