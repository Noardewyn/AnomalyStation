// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/farm_animals/rabbit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

// tg: code/modules/mob/living/basic/farm_animals/rabbit.dm:10
public partial class mob_living_basic_rabbit : mob_living_basic
{
    // tg: code/modules/mob/living/basic/farm_animals/rabbit.dm:52
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_pet_bonus), "hop"));
        Waiting.NoSleep(AddElement(typeof(datum_element_animal_variety), icon_prefix, pick("brown", "black", "white"), TRUE));
        Waiting.NoSleep(AddElement(typeof(datum_element_can_be_held)));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_RABBIT, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));
        if (prob(20)) // bunny
            name = "bunny";
        return dot;
    }
}

// tg: code/modules/mob/living/basic/farm_animals/rabbit.dm:78
public partial class mob_living_basic_rabbit_easter : mob_living_basic_rabbit
{
    // tg: code/modules/mob/living/basic/farm_animals/rabbit.dm:90
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        object eggs_added_from_eating = rand(1, 4);
        DmList feed_messages = Dm.list(Dm.text("[] nibble[] happily.", p_they(), p_s()), Dm.text("[] nom[] happily.", p_they(), p_s()));
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_egg_layer),
            typeof(obj_item_surprise_egg),
            Dm.list(typeof(obj_item_food_grown_carrotlike_carrot)),
            feed_messages,
            Dm.list("hides an egg.", "scampers around suspiciously.", "begins making a huge racket.", "begins shuffling."),
            initial_egg_amount,
            eggs_added_from_eating,
            max_eggs_held
        ));
        return dot;
    }
}
