// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/melon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/melon.dm:2
public partial class obj_item_food_grown_melonlike : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/melon.dm:26
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_kitchen_spoon)
            return NONE;

        float pulp_count = 1;
        obj_item_seeds our_seed = seed as obj_item_seeds;
        if (our_seed != null)
            pulp_count += Dm.round(Dm.num(our_seed.potency) / 25);

        user.balloon_alert(user, I18n.T("scooped out [] pulp(s)", pulp_count));
        for (float i = 1; i <= pulp_count; i++)
            Dm.@new(pulp_type, user.loc);

        /// The piece of armour melon turns into; either chetsplate or helmet
        obj_item_clothing received_armor;
        if (prob(Dm.max(0, Dm.num(our_seed.potency) - 50) / 50))
        {
            if (Dm.truthy(Dm.bitand(our_seed.resistance_flags, FIRE_PROOF)))
                received_armor = Dm.@new(fire_resistant_chestplate_type) as obj_item_clothing;
            else
                received_armor = Dm.@new(chestplate_type) as obj_item_clothing;
            to_chat(user, span_notice(I18n.T("You hollow \\the [] into a helmet with [].", this, tool)));
        }
        else
        {
            if (Dm.truthy(Dm.bitand(our_seed.resistance_flags, FIRE_PROOF)))
                received_armor = Dm.@new(fire_resistant_helmet_type) as obj_item_clothing;
            else
                received_armor = Dm.@new(helmet_type) as obj_item_clothing;
            to_chat(user, span_notice(I18n.T("You hollow \\the [] into a chestplate with [].", this, tool)));
        }

        await remove_item_from_storage(user);
        qdel(this);
        await user.put_in_hands(received_armor);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/hydroponics/grown/melon.dm:61
public partial class obj_item_seeds_watermelon : obj_item_seeds
{
    // tg: code/modules/hydroponics/grown/melon.dm:76
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is swallowing []! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        await user.gib(DROP_ALL_REMAINS);
        Dm.@new(product, drop_location());
        qdel(this);
        return MANUAL_SUICIDE;
    }
}

// tg: code/modules/hydroponics/grown/melon.dm:84
public partial class obj_item_food_grown_melonlike_watermelon : obj_item_food_grown_melonlike
{
    // tg: code/modules/hydroponics/grown/melon.dm:101
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_watermelonjuice);
    }
}

// tg: code/modules/hydroponics/grown/melon.dm:119
public partial class obj_item_food_grown_melonlike_holymelon : obj_item_food_grown_melonlike
{
    // tg: code/modules/hydroponics/grown/melon.dm:137
    public override object juice_typepath()
    {
        return typeof(datum_reagent_water_holywater);
    }

    // tg: code/modules/hydroponics/grown/melon.dm:140
    public override async DmTask make_edible()
    {
        await base.make_edible();
        await AddComponentFrom(SOURCE_EDIBLE_INNATE, typeof(datum_component_edible), Dm.named("check_liked", Dm.@new<datum_callback>(this, nameof(check_holyness))));
    }

    // tg: code/modules/hydroponics/grown/melon.dm:149
    public virtual async DmTask<object> check_holyness(mob mob_eating = null)
    {
        if (!ishuman(mob_eating))
            return null;
        mob_living_carbon_human holy_person = mob_eating as mob_living_carbon_human;
        if (!Dm.truthy(holy_person.mind?.holy_role) || HAS_TRAIT(holy_person, TRAIT_AGEUSIA))
            return null;
        to_chat(holy_person, span_notice(I18n.T("Truly, a piece of heaven!")));
        await holy_person.add_mood_event("Divine_chew", typeof(datum_mood_event_holy_consumption));
        return FOOD_LIKED;
    }
}

// Gaps H2 (D333): the kitchen links of the food (the live-partial rows S1 left for the kitchen).

public partial class obj_item_food_grown_melonlike : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/melon.dm:20
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, slice_type, 5, 20, Dm.named("screentip_verb", "Slice"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }

    // tg: code/modules/hydroponics/grown/melon.dm:23
    public override async DmTask make_dryable()
    {
        return; //No drying
    }
}
