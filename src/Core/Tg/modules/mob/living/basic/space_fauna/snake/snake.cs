// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/snake/snake.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

/**
 * # Snake
 *
 * Slithery boys, poison on bite, tameable with dead mice.
 */
// tg: code/modules/mob/living/basic/space_fauna/snake/snake.dm:5
public partial class mob_living_basic_snake : mob_living_basic
{
    /// Things we want to eat
    // tg: code/modules/mob/living/basic/space_fauna/snake/snake.dm:42
    private static DmList edibles => TgState.Current.mob_living_basic_snake__edibles ??= Dm.list(typeof(mob_living_basic_mouse), typeof(obj_item_food_deadmouse));

    // tg: code/modules/mob/living/basic/space_fauna/snake/snake.dm:48
    [DmParams("mapload", "special_reagent")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object special_reagent = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload);
        ADD_TRAIT(this, TRAIT_VENTCRAWLER_ALWAYS, INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_SNAKE, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));
        Waiting.NoSleep(AddElement(typeof(datum_element_basic_eating), Dm.named("heal_amt", 2f), Dm.named("food_types", edibles)));
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_BASIC_FOODS, typecacheof(edibles));
        Waiting.NoSleep(AddComponent(typeof(datum_component_tameable), Dm.list(typeof(obj_item_food_deadmouse)), 75f, 10f)); // snakes are really fond of food, especially in the cold darkness of space :)

        if (special_reagent == null)
            special_reagent = typeof(datum_reagent_toxin);

        Waiting.NoSleep(AddElement(typeof(datum_element_venomous), special_reagent, venom_dose));
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/snake/snake.dm:68
    public override async DmTask<object> befriend(mob_living new_friend = null)
    {
        object dot = await base.befriend(new_friend);
        if (!Dm.truthy(dot))
            return dot;
        visible_message(Dm.text("[] hisses happily as it seems to bond with [].", this, new_friend));
        return dot;
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_snake__edibles;
}
