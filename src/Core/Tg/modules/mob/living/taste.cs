// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/taste.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: tasting reagents translated (M8 slice A: a mob that swallows or breathes reagents tastes them,
// living_defense.dm:782): get_taste_sensitivity, taste_container, taste_list, check_tasting_blocks and send_taste_message.
// Gaps S1 (D291): the food type likes and dislikes, allergies and the food taste reaction (:88-187); the anaphylaxis of an
// allergic eater came with the diseases (gaps VI1; only the inert food allergy quirk reaches it). The vars last_taste_time and
// last_taste_text are generated (D111).

// tg: code/modules/mob/living/taste.dm:3
public partial class mob_living : mob
{
    // tg: code/modules/mob/living/taste.dm:1  #define DEFAULT_TASTE_SENSITIVITY 15 (file-local, #undef at :209)
    internal const int DEFAULT_TASTE_SENSITIVITY = 15;

    /**
     * Gets taste sensitivity of given mob
     *
     * This is used in calculating what flavours the mob can pick up,
     * with a lower number being able to pick up more distinct flavours.
     */
    // tg: code/modules/mob/living/taste.dm:13
    public virtual object get_taste_sensitivity()
    {
        return DEFAULT_TASTE_SENSITIVITY;
    }

    /**
     * Non-destructively tastes a reagent container
     * and gives feedback to the user.
     * Arguments:
     * * datum/reagents/from - Reagent holder to taste from.
     **/
    // tg: code/modules/mob/living/taste.dm:31
    public virtual void taste_container(datum_reagents from = null)
    {
        if (Dm.truthy(check_tasting_blocks()))
            return;

        object taste_sensitivity = get_taste_sensitivity();
        object text_output = from.generate_taste_message(this, taste_sensitivity);
        send_taste_message(text_output);
    }

    /**
     * Non-destructively tastes a reagent list
     * and gives feedback to the user.
     * Arguments:
     * * list/from - List of reagents to taste from.
     **/
    // tg: code/modules/mob/living/taste.dm:44
    public virtual void taste_list(DmList from = null)
    {
        if (Dm.truthy(check_tasting_blocks()))
            return;

        object taste_sensitivity = get_taste_sensitivity();
        object text_output = generate_reagents_taste_message(from, this, taste_sensitivity);
        send_taste_message(text_output);
    }

    /**
     * Check for anything blocking/overriding our tasting.
     * Returns TRUE on a block, FALSE if not.
     **/
    // tg: code/modules/mob/living/taste.dm:56
    public virtual object check_tasting_blocks()
    {
        if (HAS_TRAIT(this, TRAIT_AGEUSIA))
            return TRUE;
        if (Dm.num(last_taste_time) + 50 >= world.time)
            return TRUE;

        // Sometimes, try send a replacement message if we're hallucinating
        if (Dm.num(get_timed_status_effect_duration(typeof(datum_status_effect_hallucination))) > 100 * SECONDS && prob(25))
        {
            object text_output = pick("spiders", "dreams", "nightmares", "the future", "the past", "victory",
                "defeat", "pain", "bliss", "revenge", "poison", "time", "space", "death", "life", "truth", "lies", "justice", "memory",
                "regrets", "your soul", "suffering", "music", "noise", "blood", "hunger", "the american way");
            send_taste_message(text_output);
            return TRUE;
        }

        return FALSE;
    }

    /**
     * Attempt to send a taste message using given tastes text.
     **/
    // tg: code/modules/mob/living/taste.dm:76
    public virtual void send_taste_message(object tastes_text = null)
    {
        if (Dm.eq(tastes_text, last_taste_text) && Dm.num(last_taste_time) + 100 >= world.time)
            return;

        to_chat(this, span_notice(I18n.T("You can taste [].", tastes_text)));
        // "something indescribable" -> too many tastes, not enough flavor.

        last_taste_time = world.time;
        last_taste_text = tastes_text;
    }
}

// tg: code/modules/mob/living/taste.dm:16 (interleaved types, rules 1.9)
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/taste.dm:16
    public override object get_taste_sensitivity()
    {
        object dot = null;
        obj_item_organ_tongue tongue = get_organ_slot(ORGAN_SLOT_TONGUE) as obj_item_organ_tongue;
        if (tongue != null)
            dot = tongue.taste_sensitivity;
        else
            // carbons without tongues normally have TRAIT_AGEUSIA but sensible fallback
            dot = DEFAULT_TASTE_SENSITIVITY;
        return dot;
    }
}

// tg: code/modules/mob/living/taste.dm:88 (interleaved types, rules 1.9)
public partial class mob_living : mob
{
    /**
     * Gets food flags that this mob likes
     **/
    // tg: code/modules/mob/living/taste.dm:88
    public virtual object get_liked_foodtypes()
    {
        return NONE;
    }

    /**
     * Gets food flags that this mob dislikes
     **/
    // tg: code/modules/mob/living/taste.dm:105
    public virtual object get_disliked_foodtypes()
    {
        if (HAS_TRAIT(this, TRAIT_VEGETARIAN))
            return MEAT;
        return NONE;
    }

    /**
     * Gets food flags that this mob hates
     * Toxic food is the only category that ignores ageusia, KEEP IT LIKE THAT!
     **/
    // tg: code/modules/mob/living/taste.dm:122
    public virtual object get_toxic_foodtypes()
    {
        return TOXIC;
    }

    /**
     * Gets food this mob is allergic to
     * Essentially toxic food+, not only disgusting but outright lethal
     */
    // tg: code/modules/mob/living/taste.dm:137
    public virtual object get_allergic_foodtypes()
    {
        datum_quirk_item_quirk_food_allergic allergy = get_quirk(typeof(datum_quirk_item_quirk_food_allergic)) as datum_quirk_item_quirk_food_allergic;
        return Dm.truthy(allergy?.target_foodtypes) ? allergy.target_foodtypes : NONE;
    }

    /**
     * Checks if the mob has an allergic reaction to the given food type.
     * If so, the mob will contract anaphylaxis.
     *
     * Returns TRUE if the mob had an allergic reaction, FALSE otherwise.
     */
    // tg: code/modules/mob/living/taste.dm:151
    public virtual async DmTask<object> check_allergic_reaction(float? to_foodtype = null, float? chance = null, float? histamine_add = null)
    {
        to_foodtype ??= NONE;
        chance ??= 100;
        histamine_add ??= 0;
        if (!Dm.truthy(Dm.bitand(get_allergic_foodtypes(), to_foodtype)))
            return FALSE;
        if (!prob(chance.Value))
            return FALSE;
        // gaps VI1: the food allergy quirk, which alone gives get_allergic_foodtypes a value, is inert, so nothing reaches this yet
        if (Dm.truthy(await ForceContractDisease(Dm.@new<datum_disease_anaphylaxis>(), make_copy: FALSE, del_on_fail: TRUE)))
        {
            to_chat(this, span_warning(I18n.T("You feel your throat start to itch.")));
            await add_mood_event("allergic_food", typeof(datum_mood_event_allergic_food));
        }
        else if (Dm.truthy(histamine_add))
            await ((datum_reagents)reagents).add_reagent(typeof(datum_reagent_toxin_histamine), histamine_add);
        return TRUE;
    }

    /**
     * Gets the food reaction a mob would normally have from the given food item,
     * assuming that no check_liked callback was used in the edible component.
     *
     * Does not get called if the owner has ageusia.
     **/
    // tg: code/modules/mob/living/taste.dm:169
    public virtual object get_food_taste_reaction(obj_item food = null, object foodtypes = null)
    {
        object food_taste_reaction = null;
        if (Dm.truthy(Dm.bitand(foodtypes, get_toxic_foodtypes())))
            food_taste_reaction = FOOD_TOXIC;
        else if (Dm.truthy(Dm.bitand(foodtypes, get_disliked_foodtypes())))
            food_taste_reaction = FOOD_DISLIKED;
        else if (Dm.truthy(Dm.bitand(foodtypes, get_liked_foodtypes())))
            food_taste_reaction = FOOD_LIKED;
        return food_taste_reaction;
    }
}

// tg: code/modules/mob/living/taste.dm:91 (interleaved types, rules 1.9)
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/taste.dm:91
    public override object get_liked_foodtypes()
    {
        if (HAS_TRAIT(this, TRAIT_AGEUSIA))
            return NONE;
        // Handled in here since the brain trauma can't modify taste directly (/datum/brain_trauma/severe/flesh_desire)
        if (HAS_TRAIT(this, TRAIT_FLESH_DESIRE))
            return Dm.bitor(GORE, MEAT);
        obj_item_organ_tongue tongue = get_organ_slot(ORGAN_SLOT_TONGUE) as obj_item_organ_tongue;
        object dot = tongue.liked_foodtypes;
        if (HAS_TRAIT(this, TRAIT_VEGETARIAN))
            dot = Dm.bitand(dot, Dm.bitnot(MEAT));
        return dot;
    }

    // tg: code/modules/mob/living/taste.dm:110
    public override object get_disliked_foodtypes()
    {
        if (HAS_TRAIT(this, TRAIT_AGEUSIA))
            return NONE;
        obj_item_organ_tongue tongue = get_organ_slot(ORGAN_SLOT_TONGUE) as obj_item_organ_tongue;
        object dot = tongue.disliked_foodtypes;
        if (HAS_TRAIT(this, TRAIT_VEGETARIAN))
            dot = Dm.bitor(dot, MEAT);
        return dot;
    }

    // tg: code/modules/mob/living/taste.dm:125
    public override object get_toxic_foodtypes()
    {
        obj_item_organ_tongue tongue = get_organ_slot(ORGAN_SLOT_TONGUE) as obj_item_organ_tongue;
        if (tongue == null)
            return base.get_toxic_foodtypes();
        if (HAS_TRAIT(this, TRAIT_FLESH_DESIRE))
            return Dm.bitor(Dm.bitor(VEGETABLES, DAIRY), Dm.bitor(FRUIT, FRIED));
        return tongue.toxic_foodtypes;
    }

    // tg: code/modules/mob/living/taste.dm:179
    public override object get_food_taste_reaction(obj_item food = null, object foodtypes = null)
    {
        obj_item_organ_tongue tongue = get_organ_slot(ORGAN_SLOT_TONGUE) as obj_item_organ_tongue;
        // No tongue, no tastin'
        if (!Dm.truthy(tongue?.sense_of_taste) || HAS_TRAIT(this, TRAIT_AGEUSIA))
        {
            // i hate that i have to do this, but we want to ensure toxic food is still BAD
            if (Dm.truthy(Dm.bitand(foodtypes, get_toxic_foodtypes())))
                return FOOD_TOXIC;
            return null;
        }
        return tongue.get_food_taste_reaction(food, Dm.num(foodtypes));
    }
}
