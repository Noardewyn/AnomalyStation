// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/farm_animals/pig.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

// tg: code/modules/mob/living/basic/farm_animals/pig.dm:3
public partial class mob_living_basic_pig : mob_living_basic
{
    // tg: code/modules/mob/living/basic/farm_animals/pig.dm:45
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_pet_bonus), "oink"));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_flee_while_injured)));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_PIG, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));
        make_tameable();
        return dot;
    }

    ///wrapper for the tameable component addition so you can have uniquely tamable pig subtypes
    // tg: code/modules/mob/living/basic/farm_animals/pig.dm:53
    public virtual void make_tameable()
    {
        DmList food_types = string_list(Dm.list(typeof(obj_item_food_grown_carrotlike_carrot))) as DmList;
        Waiting.NoSleep(AddComponent(typeof(datum_component_tameable), food_types, 25f, 15f));
    }

    // tg: code/modules/mob/living/basic/farm_animals/pig.dm:57
    public override async DmTask tamed(mob_living tamer = null, obj_item food = null, params object[] __extra)
    {
        await base.tamed(tamer, food, __extra);
        await AddElement(typeof(datum_element_ridable), typeof(datum_component_riding_creature_pig));
        visible_message(span_notice(Dm.text("[] snorts respectfully.", this)));
    }
}
