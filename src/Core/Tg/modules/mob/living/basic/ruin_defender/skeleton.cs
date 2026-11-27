// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/ruin_defender/skeleton.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the reanimated skeletons (the base, the settler, the templar, the ice skeleton and the shambling miners), dressed
// in their outfits, dropping their gear as they fall to bones, healed by milk and burnt by soy milk. The var blocks, the outfits and
// the AI controller's behaviour tree are generated.

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/ruin_defender/skeleton.dm:36 (var/static/list/good_drinks)
    internal DmList mob_living_basic_skeleton__good_drinks;
    // tg: code/modules/mob/living/basic/ruin_defender/skeleton.dm:40 (var/static/list/bad_drinks)
    internal DmList mob_living_basic_skeleton__bad_drinks;
}

// tg: code/modules/mob/living/basic/ruin_defender/skeleton.dm:1
public partial class mob_living_basic_skeleton : mob_living_basic
{
    // tg: code/modules/mob/living/basic/ruin_defender/skeleton.dm:44
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        DmList good_drinks = TgState.Current.mob_living_basic_skeleton__good_drinks ??= Dm.list(
            typeof(obj_item_reagent_containers_condiment_milk));
        DmList bad_drinks = TgState.Current.mob_living_basic_skeleton__bad_drinks ??= Dm.list(
            typeof(obj_item_reagent_containers_condiment_soymilk));
        apply_dynamic_human_appearance(this, outfit, species, r_hand: held_item);
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), FOOTSTEP_MOB_SHOE));
        if (Dm.truthy(LAZYLEN(loot)))
        {
            loot = string_list(loot) as DmList;
            Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), loot));
        }
        Waiting.NoSleep(AddElement(typeof(datum_element_basic_eating), Dm.named("heal_amt", 50), Dm.named("drinking", TRUE), Dm.named("food_types", good_drinks)));
        Waiting.NoSleep(AddElement(typeof(datum_element_basic_eating), Dm.named("heal_amt", 0), Dm.named("damage_amount", 25), Dm.named("damage_type", BURN), Dm.named("drinking", TRUE), Dm.named("food_types", bad_drinks)));
        add_traits(Dm.list(TRAIT_SPOOKY_INSTRUMENT_PLAYER, TRAIT_SNOWSTORM_IMMUNE), INNATE_TRAIT);
        DmList foods_list = Dm.add(good_drinks, bad_drinks) as DmList;
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_BASIC_FOODS, typecacheof(foods_list));
        return dot;
    }

    // tg: code/modules/mob/living/basic/ruin_defender/skeleton.dm:57
    public override object get_unconscious_appearance()
    {
        return get_generic_humanoid_static_appearance();
    }
}
