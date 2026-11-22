// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/farm_animals/cow/_cow.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

// tg: code/modules/mob/living/basic/farm_animals/cow/_cow.dm:3
public partial class mob_living_basic_cow : mob_living_basic
{
    // tg: code/modules/mob/living/basic/farm_animals/cow/_cow.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_tippable), 0.5f * SECONDS, 0.5f * SECONDS, rand(25 * SECONDS, 50 * SECONDS), null, Dm.@new<datum_callback>(this, nameof(after_cow_tipped))));
        Waiting.NoSleep(AddElement(typeof(datum_element_pet_bonus), "moo"));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_COW, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));
        ADD_TRAIT(this, TRAIT_ABDUCTOR_QUICK_SCAN, INNATE_TRAIT); //cow ufo abduction ahaha
        setup_udder();
        setup_eating();
        object dot = base.Initialize(mapload, __extra);
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_BASIC_FOODS, typecacheof(food_types));
        return dot;
    }

    ///wrapper for the udder component addition so you can have uniquely uddered cow subtypes
    // tg: code/modules/mob/living/basic/farm_animals/cow/_cow.dm:66
    public virtual void setup_udder()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_udder), null, null, null, milked_reagent));
    }

    ///wrapper for the tameable component addition so you can have uniquely tamable cow subtypes
    // tg: code/modules/mob/living/basic/farm_animals/cow/_cow.dm:70
    public virtual void setup_eating()
    {
        DmList food_types = TgState.Current.mob_living_basic_cow__setup_eating__food_types ??= this.food_types.Copy();
        Waiting.NoSleep(AddComponent(typeof(datum_component_tameable), food_types, 25f, 15f));
    }

    // tg: code/modules/mob/living/basic/farm_animals/cow/_cow.dm:76
    public override async DmTask tamed(mob_living tamer = null, obj_item food = null, params object[] __extra)
    {
        await base.tamed(tamer, food, __extra);
        visible_message(Dm.text("[] [] as it seems to bond with [].", this, tame_message, tamer), Dm.text("You [], recognizing [] as your new pal.", self_tame_message, tamer));
        await AddElement(typeof(datum_element_ridable), typeof(datum_component_riding_creature_cow));
    }

    // tg: code/modules/mob/living/basic/farm_animals/cow/_cow.dm:81
    public virtual void after_cow_tipped(mob_living_carbon tipper = null)
    {
        _addtimer(Dm.@new<datum_callback>(this, nameof(set_tip_react_blackboard), tipper), rand(10 * SECONDS, 20 * SECONDS), file: "code/modules/mob/living/basic/farm_animals/cow/_cow.dm", line: 83);
    }

    /// We've been tipped, react to it
    // tg: code/modules/mob/living/basic/farm_animals/cow/_cow.dm:88
    public virtual void set_tip_react_blackboard(mob_living_carbon tipper = null)
    {
        if (!HAS_TRAIT_FROM(this, TRAIT_IMMOBILIZED, TIPPED_OVER) || ai_controller == null)
            return;
        datum_ai_controller controller = ai_controller as datum_ai_controller;
        controller.set_blackboard_key(BB_BASIC_MOB_TIP_REACTING, TRUE);
        controller.set_blackboard_key(BB_BASIC_MOB_TIPPER, tipper);
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_cow__setup_eating__food_types;
}
