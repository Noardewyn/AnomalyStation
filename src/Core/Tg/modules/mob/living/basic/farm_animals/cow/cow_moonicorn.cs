// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/farm_animals/cow/cow_moonicorn.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

// tg: code/modules/mob/living/basic/farm_animals/cow/cow_moonicorn.dm:3
public partial class mob_living_basic_cow_moonicorn : mob_living_basic_cow
{
    // tg: code/modules/mob/living/basic/farm_animals/cow/cow_moonicorn.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_venomous), typeof(datum_reagent_pax), 5f, Dm.bitor(INJECT_CHECK_PENETRATE_THICK, INJECT_CHECK_IGNORE_SPECIES)));
        Waiting.NoSleep(AddElement(typeof(datum_element_movement_turf_changer), typeof(turf_open_floor_grass_fairy)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/cow/cow_moonicorn.dm:29
    public override void setup_eating()
    {
        DmList food_types = TgState.Current.mob_living_basic_cow_moonicorn__setup_eating__food_types ??= this.food_types.Copy();
        Waiting.NoSleep(AddComponent(typeof(datum_component_tameable), food_types, 25f, 15f));
    }

    // tg: code/modules/mob/living/basic/farm_animals/cow/cow_moonicorn.dm:35
    public override async DmTask tamed(mob_living tamer = null, obj_item food = null, params object[] __extra)
    {
        await base.tamed(tamer, food, __extra);
        ///stops the moonicorn from being hostile to tamer and friends.
        add_faction(tamer.get_faction()); // APPLY_FACTION_AND_ALLIES_FROM (__DEFINES/mobfactions.dm:135), inline
        add_ally(LAZYLISTDUPLICATE(tamer.allies));
    }
}

// tg: code/modules/mob/living/basic/farm_animals/cow/cow_moonicorn.dm:60
public partial class datum_targeting_strategy_basic_allow_items_moonicorn : datum_targeting_strategy_basic_allow_items
{
    // tg: code/modules/mob/living/basic/farm_animals/cow/cow_moonicorn.dm:62
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom the_target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, the_target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;

        if (isliving(the_target)) //Targeting vs living mobs
        {
            mob_living living_target = the_target as mob_living;
            foreach (object tame_food in Dm.each(living_target.held_items))
                if (tame_food is obj_item_food_grown_galaxythistle)
                    return FALSE; //heyyy this can tame me! let's NOT fight
        }
        return dot;
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_cow_moonicorn__setup_eating__food_types;
}
