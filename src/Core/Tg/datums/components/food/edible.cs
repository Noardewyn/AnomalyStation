// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/food/edible.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using System;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps S1 (D291): the whole file. Leaves: the good-food memory (the memories, G4) and the golem food buffs of
// GLOB.golem_stack_food_directory (golems) are Dm.Deferred; the var block is generated; the static loc_connections is a TgState
// field (rules 10.6).

/*!

This component makes it possible to make things edible. What this means is that you can take a bite or force someone to take a bite (in the case of items).
These items take a specific time to eat, and can do most of the things our original food items could.
*/

// tg: code/datums/components/food/edible.dm:18
public partial class datum_component_edible : datum_component
{
    // tg: code/datums/components/food/edible.dm:16  #define DEFAULT_EDIBLE_VOLUME 50 (file-local, #undef at :795)
    internal const float DEFAULT_EDIBLE_VOLUME = 50;
    /// Normal time to forcefeed someone something
    // tg: code/datums/components/food/edible.dm:366  #define EAT_TIME_FORCE_FEED (3 SECONDS)
    internal const float EAT_TIME_FORCE_FEED = 3 * SECONDS;
    /// Multiplier for eat time if the eater has TRAIT_VORACIOUS
    // tg: code/datums/components/food/edible.dm:368
    internal const float EAT_TIME_VORACIOUS_MULT = 0.65f; // voracious folk eat 35% faster
    /// Multiplier for how much longer it takes a voracious folk to eat while full
    // tg: code/datums/components/food/edible.dm:370
    internal const float EAT_TIME_VORACIOUS_FULL_MULT = 4; // Takes at least 4 times as long to eat while full, so dorks cant just clear out the kitchen before they get robusted
    // tg: code/datums/components/food/edible.dm:775  #define REQUIRED_MAT_FLAGS (MATERIAL_EFFECTS|MATERIAL_NO_EDIBILITY)
    internal const float REQUIRED_MAT_FLAGS = MATERIAL_EFFECTS | MATERIAL_NO_EDIBILITY;

    // tg: code/datums/components/food/edible.dm:85  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(on_entered))
    public static DmList loc_connections => TgState.Current.datum_component_edible__loc_connections;

    // tg: code/datums/components/food/edible.dm:50
    [DmParams("initial_reagents", "food_flags", "foodtypes", "volume", "eat_time", "tastes", "eatverbs", "bite_consumption", "junkiness", "after_eat", "on_consume", "check_liked", "reagent_purity", "handmade_complexity")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object food_flags = Dm.arg(__extra, 1) ?? NONE;
        object foodtypes = Dm.arg(__extra, 2) ?? NONE;
        object eat_time = Dm.arg(__extra, 4) ?? 1 * SECONDS;
        DmList eatverbs = Dm.arg(__extra, 6) as DmList ?? Dm.list("bite", "chew", "nibble", "gnaw", "gobble", "chomp");
        object bite_consumption = Dm.arg(__extra, 7) ?? 2;
        object handmade_complexity = Dm.arg(__extra, 13) ?? FOOD_COMPLEXITY_0;
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;

        // If these args are not explicitly stated when initializing the component
        // Use the defaults provided in this proc definition, so we don't have to worry
        // about these being null. We cannot rely on on_add_source() for this lest these
        // end up being unwantedly overriden by other sources.
        this.bite_consumption = Dm.num(bite_consumption);
        this.food_flags = Dm.num(food_flags);
        this.foodtypes = Dm.num(foodtypes);
        this.eat_time = Dm.num(eat_time);
        this.eatverbs = string_list(eatverbs) as DmList;
        this.handmade_complexity = Dm.num(handmade_complexity);
        return null;
    }

    // tg: code/datums/components/food/edible.dm:75
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(examine));
        RegisterSignal(parent, COMSIG_ATOM_ATTACK_ANIMAL, nameof(UseByAnimal));
        RegisterSignal(parent, COMSIG_ATOM_ON_CRAFT, nameof(OnCraft));
        RegisterSignal(parent, COMSIG_OOZE_EAT_ATOM, nameof(on_ooze_eat));
        RegisterSignal(parent, COMSIG_FOOD_INGREDIENT_ADDED, nameof(edible_ingredient_added));
        RegisterSignal(parent, COMSIG_ATOM_CREATEDBY_PROCESSING, nameof(created_by_processing));
        RegisterSignal(parent, COMSIG_ATOM_FINALIZE_MATERIAL_EFFECTS, nameof(on_material_effects));
        RegisterSignal(parent, COMSIG_ATOM_FINALIZE_REMOVE_MATERIAL_EFFECTS, nameof(on_remove_material_effects));

        if (isturf(parent))
            RegisterSignal(parent, COMSIG_ATOM_ENTERED, nameof(on_entered));
        else
            await AddComponent(typeof(datum_component_connect_loc_behalf), parent, loc_connections);

        if (isitem(parent))
        {
            RegisterSignal(parent, COMSIG_ITEM_ATTACK, nameof(UseFromHand));
            RegisterSignal(parent, COMSIG_ITEM_USED_AS_INGREDIENT, nameof(used_to_customize));
        }
        else if (isturf(parent) || isstructure(parent))
            RegisterSignal(parent, COMSIG_ATOM_ATTACK_HAND, nameof(TryToEatIt));

        if (Dm.truthy(Dm.bitand(foodtypes, GORE)))
            ADD_TRAIT(parent, TRAIT_VALID_DNA_INFUSION, REF(this));
    }

    // tg: code/datums/components/food/edible.dm:102
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_ATOM_ATTACK_ANIMAL,
            COMSIG_ATOM_ATTACK_HAND,
            COMSIG_ATOM_ON_CRAFT,
            COMSIG_ATOM_CREATEDBY_PROCESSING,
            COMSIG_ATOM_ENTERED,
            COMSIG_FOOD_INGREDIENT_ADDED,
            COMSIG_ITEM_ATTACK,
            COMSIG_ITEM_USED_AS_INGREDIENT,
            COMSIG_OOZE_EAT_ATOM,
            COMSIG_ATOM_EXAMINE
        ));

        qdel(GetComponent(typeof(datum_component_connect_loc_behalf)));

        if (Dm.truthy(Dm.bitand(foodtypes, GORE)))
            REMOVE_TRAIT(parent, TRAIT_VALID_DNA_INFUSION, REF(this));
    }

    // tg: code/datums/components/food/edible.dm:121
    public override object allow_source_update(object source = null)
    {
        return Dm.eq(source, SOURCE_EDIBLE_INNATE) ? TRUE : FALSE;
    }

    // tg: code/datums/components/food/edible.dm:124
    [DmParams("source", "initial_reagents", "food_flags", "foodtypes", "volume", "eat_time", "tastes", "eatverbs", "bite_consumption", "junkiness", "after_eat", "on_consume", "check_liked", "reagent_purity", "handmade_complexity")]
    public override async DmTask<object> on_source_add(object source = null, params object[] __extra)
    {
        DmList initial_reagents = Dm.arg(__extra, 0) as DmList;
        object food_flags = Dm.arg(__extra, 1);
        object foodtypes = Dm.arg(__extra, 2);
        object volume = Dm.arg(__extra, 3);
        object eat_time = Dm.arg(__extra, 4);
        DmList tastes = Dm.arg(__extra, 5) as DmList;
        object eatverbs = Dm.arg(__extra, 6);
        object bite_consumption = Dm.arg(__extra, 7);
        object junkiness = Dm.arg(__extra, 8);
        datum_callback after_eat = Dm.arg(__extra, 9) as datum_callback;
        datum_callback on_consume = Dm.arg(__extra, 10) as datum_callback;
        datum_callback check_liked = Dm.arg(__extra, 11) as datum_callback;
        object reagent_purity = Dm.arg(__extra, 12) ?? 0.5f;
        object dot = await base.on_source_add(source, __extra);

        float recalculate = FALSE;
        if (foodtypes != null)
        {
            if (Dm.truthy(LAZYACCESS(foodtypes_by_source, source))) //foodtypes being overriden
                recalculate = TRUE;
            LAZYSET(ref foodtypes_by_source, source, foodtypes);
        }
        if (food_flags != null)
        {
            if (Dm.truthy(LAZYACCESS(food_flags_by_source, source))) //food_flags being overridden
                recalculate = TRUE;
            LAZYSET(ref food_flags_by_source, source, food_flags);
        }
        if (junkiness != null)
        {
            this.junkiness = Dm.num(this.junkiness) + Dm.num(junkiness) - Dm.num(LAZYACCESS(junkiness_by_source, source));
            LAZYSET(ref junkiness_by_source, source, junkiness);
        }

        if (Dm.truthy(recalculate))
            recalculate_food_flags();
        else
        {
            // nothing is being removed
            this.food_flags = Dm.bitor(Dm.num(this.food_flags), Dm.num(food_flags));
            this.foodtypes = Dm.bitor(Dm.num(this.foodtypes), Dm.num(foodtypes));
            if (Dm.truthy(Dm.bitand(foodtypes, GORE)))
                ADD_TRAIT(parent, TRAIT_VALID_DNA_INFUSION, REF(this));
        }

        // add newly passed in reagents
        await setup_initial_reagents(initial_reagents, reagent_purity, tastes, volume);

        //Only the innate source is allowed to change the following vars if there are a plurality of sources.
        if (!Dm.eq(source, SOURCE_EDIBLE_INNATE) && Dm.length(sources) > 1)
            return dot;

        // add all new eatverbs to the list
        if (Dm.islist(eatverbs))
        {
            DmList cached_verbs = this.eatverbs;
            if (Dm.islist(cached_verbs))
                // eatverbs becomes a combination of existing verbs and new ones
                this.eatverbs = string_list(Dm.bitor(cached_verbs, eatverbs) as DmList) as DmList;
            else
                this.eatverbs = string_list((DmList)eatverbs) as DmList;
        }
        // just set these directly
        if (bite_consumption != null)
            this.bite_consumption = Dm.num(bite_consumption);
        if (eat_time != null)
            this.eat_time = Dm.num(eat_time);
        if (after_eat != null)
            this.after_eat = after_eat;
        if (on_consume != null)
            this.on_consume = on_consume;
        if (check_liked != null)
            this.check_liked = check_liked;
        return dot;
    }

    // tg: code/datums/components/food/edible.dm:186
    public override void on_source_remove(object source = null)
    {
        //rebuild the foodtypes and food_flags bitfields without the removed source
        LAZYREMOVE(ref foodtypes_by_source, source);
        LAZYREMOVE(ref food_flags_by_source, source);
        junkiness = Dm.num(junkiness) - Dm.num(LAZYACCESS(junkiness_by_source, source));
        LAZYREMOVE(ref junkiness_by_source, source);
        recalculate_food_flags();
        base.on_source_remove(source);
    }

    // tg: code/datums/components/food/edible.dm:195
    public virtual void recalculate_food_flags()
    {
        foodtypes = NONE;
        food_flags = NONE;
        foreach (object source_key in Dm.each(foodtypes_by_source))
        {
            foodtypes = Dm.bitor(Dm.num(foodtypes), Dm.num(LAZYACCESS(foodtypes_by_source, source_key)));
            food_flags = Dm.bitor(Dm.num(food_flags), Dm.num(LAZYACCESS(food_flags_by_source, source_key)));
        }
        if (Dm.truthy(Dm.bitand(foodtypes, GORE)))
            ADD_TRAIT(parent, TRAIT_VALID_DNA_INFUSION, REF(this));
        else
            REMOVE_TRAIT(parent, TRAIT_VALID_DNA_INFUSION, REF(this));
    }

    // tg: code/datums/components/food/edible.dm:206
    public override object Destroy(float? force = null)
    {
        after_eat = null;
        on_consume = null;
        check_liked = null;
        return base.Destroy(force);
    }

    /// Sets up the initial reagents of the food.
    // tg: code/datums/components/food/edible.dm:213
    public virtual async DmTask setup_initial_reagents(DmList reagents = null, object reagent_purity = null, DmList tastes = null, object volume = null)
    {
        atom owner = (atom)parent;
        if (owner.reagents == null)
            owner.create_reagents(Dm.truthy(volume) ? volume : DEFAULT_EDIBLE_VOLUME, INJECTABLE);
        else if (Dm.num(volume) > ((datum_reagents)owner.reagents).maximum_volume)
            ((datum_reagents)owner.reagents).maximum_volume = Dm.num(volume);

        foreach (object rid in Dm.each(reagents))
        {
            object amount = reagents[rid];
            if (Dm.truthy(Dm.length(tastes)) && ispath(rid, typeof(datum_reagent_consumable_nutriment)))
            {
                if (Dm.truthy(Dm.initial((Type)rid, (datum_reagent_consumable_nutriment p) => p.carry_food_tastes)))
                {
                    await ((datum_reagents)owner.reagents).add_reagent(rid, amount, tastes.Copy(), added_purity: reagent_purity);
                    continue;
                }
            }
            await ((datum_reagents)owner.reagents).add_reagent(rid, amount, added_purity: reagent_purity);
        }
    }

    // tg: code/datums/components/food/edible.dm:228
    [SignalHandler]
    public virtual void examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        atom owner = (atom)parent;
        if (Dm.truthy(Dm.bitand(food_flags, FOOD_NO_EXAMINE)))
            return;
        if (Dm.truthy(foodtypes))
        {
            object types = bitfield_to_list(foodtypes, FOOD_FLAGS);
            Dm.append(examine_list, span_notice(I18n.T("It is [].", LOWER_TEXT(english_list(types as DmList)))));
        }

        float quality = Dm.num(Waiting.NoSleep(get_perceived_food_quality(user as mob_living)));
        if (quality > 0)
        {
            object quality_label = GLOB.food_quality_description[quality];
            Dm.append(examine_list, span_green(I18n.T("You find this meal [].", quality_label)));
        }
        else if (quality == 0)
            Dm.append(examine_list, span_notice(I18n.T("You find this meal edible.")));
        else if (quality <= FOOD_QUALITY_DANGEROUS)
            Dm.append(examine_list, span_warning(I18n.T("You may die from eating this meal.")));
        else if (quality <= TOXIC_FOOD_QUALITY_THRESHOLD)
            Dm.append(examine_list, span_warning(I18n.T("You find this meal disgusting!")));
        else
            Dm.append(examine_list, span_warning(I18n.T("You find this meal inedible.")));

        datum_reagents holder = (datum_reagents)owner.reagents;
        if (holder.total_volume > 0)
        {
            float purity = Dm.num(holder.get_average_purity(typeof(datum_reagent_consumable)));
            // switch(purity): the first matching range runs
            if (purity >= 0 && purity <= 0.2f)
                Dm.append(examine_list, span_warning(I18n.T("It is made of terrible ingredients shortening the effect...")));
            else if (purity >= 0.2f && purity <= 0.4f)
                Dm.append(examine_list, span_warning(I18n.T("It is made of synthetic ingredients shortening the effect.")));
            else if (purity >= 0.4f && purity <= 0.6f)
                Dm.append(examine_list, span_notice(I18n.T("It is made of average quality ingredients.")));
            else if (purity >= 0.6f && purity <= 0.8f)
                Dm.append(examine_list, span_green(I18n.T("It is made of organic ingredients prolonging the effect.")));
            else if (purity >= 0.8f && purity <= 1)
                Dm.append(examine_list, span_green(I18n.T("It is made of finest ingredients prolonging the effect!")));
        }

        datum_mind mind = user.mind;
        if (mind != null && HAS_TRAIT_FROM(owner, TRAIT_HANDMADE, REF(mind)))
            Dm.append(examine_list, span_green(I18n.T("[] was made by you!", owner)));

        if (!Dm.truthy(Dm.bitand(food_flags, FOOD_IN_CONTAINER)))
        {
            // switch(bitecount)
            if (bitecount == 0)
                pass();
            else if (bitecount == 1)
                Dm.append(examine_list, span_notice(I18n.T("[] was bitten by someone!", owner)));
            else if (bitecount == 2 || bitecount == 3)
                Dm.append(examine_list, span_notice(I18n.T("[] was bitten [] times!", owner, bitecount)));
            else
                Dm.append(examine_list, span_notice(I18n.T("[] was bitten multiple times!", owner)));
        }

        if (Dm.truthy(GLOB.debugging_enabled))
        {
            Dm.append(examine_list, span_notice(I18n.T("Reagent purities:")));
            foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(holder.reagent_list))
                Dm.append(examine_list, span_notice(I18n.T("- [] []u: []% pure", reagent.name, reagent.volume, round(reagent.purity * 100))));
        }

        if (!HAS_TRAIT(user, TRAIT_REMOTE_TASTING))
            return;
        float fraction = min(Dm.num(bite_consumption) / holder.total_volume, 1);
        Waiting.NoSleep(checkLiked(fraction, user));
        if (!Dm.truthy(holder.get_reagent_amount(typeof(datum_reagent_consumable_salt))))
            Dm.append(examine_list, span_notice(I18n.T("It could use a little more Sodium Chloride...")));
        if (isliving(user))
        {
            mob_living living_user = (mob_living)user;
            living_user.taste_container(holder);
        }
    }

    // tg: code/datums/components/food/edible.dm:306
    [SignalHandler]
    public virtual object UseFromHand(obj_item source = null, mob_living M = null, mob_living user = null)
    {
        return TryToEat(M, user);
    }

    // tg: code/datums/components/food/edible.dm:311
    [SignalHandler]
    public virtual object TryToEatIt(datum source = null, mob user = null)
    {
        if (!in_range(source, user))
            return null;
        return TryToEat(user as mob_living, user as mob_living);
    }

    ///Called when food is created through processing (Usually this means it was sliced). We use this to pass the OG items reagents.
    // tg: code/datums/components/food/edible.dm:318
    [SignalHandler]
    public virtual void created_by_processing(datum source = null, atom original_atom = null, DmList chosen_processing_option = null)
    {
        if (original_atom.reagents == null)
            return;

        atom this_food = (atom)parent;
        datum_reagents original = (datum_reagents)original_atom.reagents;

        //Make sure we have a reagent container large enough to fit the original atom's reagents.
        float volume = ROUND_UP(original.maximum_volume / Dm.num(chosen_processing_option[TOOL_PROCESSING_AMOUNT]));

        this_food.create_reagents(volume, (this_food.reagents as datum_reagents)?.flags);
        Waiting.NoSleep(original.trans_to(this_food, original.total_volume / Dm.num(chosen_processing_option[TOOL_PROCESSING_AMOUNT]), copy_only: TRUE));

        if (!HAS_TRAIT(this_food, TRAIT_FOOD_DONT_INHERIT_NAME_FROM_PROCESSED) && !Dm.eq(original_atom.name, Dm.initial(original_atom, (atom p) => p.name)))
        {
            this_food.name = Dm.text("slice of []", original_atom.name);
            //It inherits the name of the original, which may already have a prefix
            //So we need to make sure we don't double up on prefixes
            //This is called before set_custom_materials() anyway
            this_food.material_flags = Dm.bitand(this_food.material_flags, Dm.bitnot(MATERIAL_ADD_PREFIX));
        }
        if (!Dm.eq(original_atom.desc, Dm.initial(original_atom, (atom p) => p.desc)))
            this_food.desc = Dm.text("[]", original_atom.desc);
    }

    ///Called when food is crafted through a crafting recipe datum.
    // tg: code/datums/components/food/edible.dm:343
    [SignalHandler]
    public virtual void OnCraft(datum source = null, DmList components = null, datum_crafting_recipe_food recipe = null)
    {
        atom this_food = (atom)parent;
        datum_reagents food_reagents = (datum_reagents)this_food.reagents;
        foreach (obj_item_food crafted_part in Dm.each<obj_item_food>(components))
        {
            if (crafted_part.reagents == null)
                continue;
            datum_reagents part_reagents = (datum_reagents)crafted_part.reagents;
            food_reagents.maximum_volume += part_reagents.maximum_volume;
            Waiting.NoSleep(part_reagents.trans_to(food_reagents, part_reagents.maximum_volume));
        }

        food_reagents.maximum_volume = ROUND_UP(food_reagents.maximum_volume); // Just because I like whole numbers for this.

        // BLACKBOX_LOG_FOOD_MADE(parent.type): SSblackbox.record_feedback("tally", "food_made", 1, type) (__DEFINES/food.dm)
        SSblackbox.record_feedback("tally", "food_made", 1, parent.type);
    }

    ///Makes sure the thing hasn't been destroyed or fully eaten to prevent eating phantom edibles
    // tg: code/datums/components/food/edible.dm:358
    public virtual object IsFoodGone(atom owner = null, mob_living feeder = null)
    {
        if (QDELETED(owner) || !Dm.truthy(owner.GetComponent(typeof(datum_component_edible)))) // IS_EDIBLE(owner), __DEFINES/is_helpers.dm
            return TRUE;
        if (Dm.truthy(((datum_reagents)owner.reagents).total_volume))
            return FALSE;
        return TRUE;
    }

    ///All the checks for the act of eating itself and
    // tg: code/datums/components/food/edible.dm:373  (set waitfor = FALSE: the callee-side wrapper, waiting-model 5.4)
    public virtual object TryToEat(mob_living eater = null, mob_living feeder = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(TryToEat__body(eater, feeder, dot), dot);
    }

    private async DmTask<object> TryToEat__body(mob_living eater, mob_living feeder, DotBox<object> dot)
    {
        atom owner = (atom)parent;

        if (Dm.truthy(feeder.combat_mode))
            return dot.Value;

        dot.Value = COMPONENT_CANCEL_ATTACK_CHAIN; //Point of no return I suppose

        if (Dm.truthy(IsFoodGone(owner, feeder)))
            return dot.Value;

        if (!Dm.truthy(CanConsume(eater as mob_living_carbon, feeder)))
            return dot.Value;
        float fullness = Dm.num(eater.get_fullness()) + 10; //The theoretical fullness of the person eating if they were to eat this

        float time_to_eat = (eater == feeder) ? Dm.num(eat_time) : EAT_TIME_FORCE_FEED;
        if (HAS_TRAIT(eater, TRAIT_VORACIOUS) && !HAS_TRAIT(eater, TRAIT_GLUTTON)) //with TRAIT_GLUTTON you consume food without delay
        {
            if (fullness < NUTRITION_LEVEL_FAT || (eater != feeder)) // No extra delay when being forcefed
                time_to_eat *= EAT_TIME_VORACIOUS_MULT;
            else
                time_to_eat *= (fullness / NUTRITION_LEVEL_FAT) * EAT_TIME_VORACIOUS_FULL_MULT; // takes longer to eat the more well fed you are
        }
        if (eater == feeder) //If you're eating it yourself.
        {
            if (Dm.num(eat_time) > 0 && !Dm.truthy(await do_after(feeder, time_to_eat, eater, timed_action_flags: Dm.truthy(Dm.bitand(food_flags, FOOD_FINGER_FOOD)) ? Dm.bitor(IGNORE_USER_LOC_CHANGE, IGNORE_TARGET_LOC_CHANGE) : NONE))) //Gotta pass the minimal eat time
                return dot.Value;
            if (Dm.truthy(IsFoodGone(owner, feeder)))
                return dot.Value;
            object eatverb = pick(eatverbs);

            object message_to_nearby_audience = "";
            object message_to_consumer = "";
            object message_to_blind_consumer = "";

            if (Dm.truthy(junkiness) && eater.satiety < -150 && eater.nutrition > NUTRITION_LEVEL_STARVING + 50 && !HAS_TRAIT(eater, TRAIT_VORACIOUS) && !HAS_TRAIT(eater, TRAIT_GLUTTON))
            {
                to_chat(eater, span_warning(I18n.T("You don't feel like eating any more junk food at the moment!")));
                return dot.Value;
            }
            else if (fullness > (600 * (1 + eater.overeatduration / (4000 * SECONDS)))) // The more you eat - the more you can eat
            {
                if (HAS_TRAIT(eater, TRAIT_VORACIOUS) || HAS_TRAIT(eater, TRAIT_GLUTTON))
                {
                    message_to_nearby_audience = span_notice(I18n.T("[] voraciously forces \\the [] down [] throat.", eater, parent, eater.p_their()));
                    message_to_consumer = span_notice(I18n.T("You voraciously force \\the [] down your throat.", parent));
                }
                else
                {
                    message_to_nearby_audience = span_warning(I18n.T("[] cannot force any more of \\the [] to go down [] throat!", eater, parent, eater.p_their()));
                    message_to_consumer = span_warning(I18n.T("You cannot force any more of \\the [] to go down your throat!", parent));
                    message_to_blind_consumer = message_to_consumer;
                    eater.show_message(message_to_consumer, MSG_VISUAL, message_to_blind_consumer);
                    eater.visible_message(message_to_nearby_audience, ignored_mobs: eater);
                    //if we're too full, return because we can't eat whatever it is we're trying to eat
                    return dot.Value;
                }
            }
            else if (fullness > 500)
            {
                if (HAS_TRAIT(eater, TRAIT_VORACIOUS))
                {
                    message_to_nearby_audience = span_notice(I18n.T("[] []s \\the [].", eater, eatverb, parent));
                    message_to_consumer = span_notice(I18n.T("You [] \\the [].", eatverb, parent));
                }
                else
                {
                    message_to_nearby_audience = span_notice(I18n.T("[] unwillingly []s a bit of \\the [].", eater, eatverb, parent));
                    message_to_consumer = span_notice(I18n.T("You unwillingly [] a bit of \\the [].", eatverb, parent));
                }
            }
            else if (fullness > 150)
            {
                message_to_nearby_audience = span_notice(I18n.T("[] []s \\the [].", eater, eatverb, parent));
                message_to_consumer = span_notice(I18n.T("You [] \\the [].", eatverb, parent));
            }
            else if (fullness > 50)
            {
                message_to_nearby_audience = span_notice(I18n.T("[] hungrily []s \\the [].", eater, eatverb, parent));
                message_to_consumer = span_notice(I18n.T("You hungrily [] \\the [].", eatverb, parent));
            }
            else
            {
                message_to_nearby_audience = span_notice(I18n.T("[] hungrily []s \\the [], gobbling it down!", eater, eatverb, parent));
                message_to_consumer = span_notice(I18n.T("You hungrily [] \\the [], gobbling it down!", eatverb, parent));
            }

            //if we're blind, we want to feel how hungrily we ate that food
            message_to_blind_consumer = message_to_consumer;
            eater.show_message(message_to_consumer, MSG_VISUAL, message_to_blind_consumer);
            eater.visible_message(message_to_nearby_audience, ignored_mobs: eater);
        }
        else //If you're feeding it to someone else.
        {
            if (isbrain(eater))
            {
                to_chat(feeder, span_warning(I18n.T("[] doesn't seem to have a mouth!", eater)));
                return dot.Value;
            }
            if (fullness <= (600 * (1 + eater.overeatduration / (2000 * SECONDS))) || HAS_TRAIT(eater, TRAIT_VORACIOUS))
            {
                bool has_head = Dm.truthy(eater.get_bodypart(BODY_ZONE_HEAD));
                eater.visible_message(
                    span_danger(I18n.T("[] attempts to [].", feeder, has_head ? I18n.T("feed [] [].", eater, parent) : I18n.T("stuff [] down []'s throat hole! Gross.", parent, eater))),
                    span_userdanger(I18n.T("[] attempts to [].", feeder, has_head ? I18n.T("feed you [].", parent) : I18n.T("stuff [] down your throat hole! Gross.", parent)))
                );
                if (Dm.truthy(eater.is_blind()))
                    to_chat(eater, span_userdanger(I18n.T("You feel someone trying to feed you something!")));
            }
            else
            {
                bool has_head = Dm.truthy(eater.get_bodypart(BODY_ZONE_HEAD));
                eater.visible_message(
                    span_danger(I18n.T("[] cannot force any more of [] down []'s []", feeder, parent, eater, has_head ? I18n.T("throat!") : I18n.T("throat hole! Eugh."))),
                    span_userdanger(I18n.T("[] cannot force any more of [] down your []", feeder, parent, has_head ? I18n.T("throat!") : I18n.T("throat hole! Eugh.")))
                );
                if (Dm.truthy(eater.is_blind()))
                    to_chat(eater, span_userdanger(I18n.T("You're too full to eat what's being fed to you!")));
                return dot.Value;
            }
            if (!Dm.truthy(await do_after(feeder, delay: time_to_eat, target: eater))) //Wait 3-ish seconds before you can feed
                return dot.Value;
            if (Dm.truthy(IsFoodGone(owner, feeder)))
                return dot.Value;
            log_combat(feeder, eater, "fed", ((datum_reagents)owner.reagents).get_reagent_log_string());
            eater.visible_message(
                span_danger(I18n.T("[] forces [] to eat []!", feeder, eater, parent)),
                span_userdanger(I18n.T("[] forces you to eat []!", feeder, parent))
            );
            if (Dm.truthy(eater.is_blind()))
                to_chat(eater, span_userdanger(I18n.T("You're forced to eat something!")));
        }

        await TakeBite(eater, feeder);

        //If we're not force-feeding and there's an eat delay, try take another bite
        if (eater == feeder && Dm.num(eat_time) > 0)
            Waiting.DetachSync(() => TryToEat(eater, feeder));
        return dot.Value;
    }

    ///This function lets the eater take a bite and transfers the reagents to the eater.
    // tg: code/datums/components/food/edible.dm:487
    public virtual async DmTask<object> TakeBite(mob_living eater = null, mob_living feeder = null)
    {
        atom owner = (atom)parent;

        if (owner.reagents == null)
        {
            stack_trace(Dm.text("[] failed to bite [], because [] had no reagents.", eater, owner, owner), "code/datums/components/food/edible.dm", 492);
            return FALSE;
        }
        datum_reagents holder = (datum_reagents)owner.reagents;
        if (eater.satiety > -200)
            eater.satiety -= Dm.num(junkiness);
        playsound(eater.loc as atom, Dm.resource("sound/items/eatfood.ogg"), rand(10, 50), TRUE);
        if (!Dm.truthy(holder.total_volume))
            return null;
        if (Dm.truthy(eater._listen_lookup?[COMSIG_LIVING_EAT_FOOD]))
            eater._SendSignal(COMSIG_LIVING_EAT_FOOD, Dm.list(eater, owner, foodtypes));
        object sig_return = !Dm.truthy(parent._listen_lookup?[COMSIG_FOOD_EATEN]) ? NONE : parent._SendSignal(COMSIG_FOOD_EATEN, Dm.list(parent, eater, feeder, bitecount, bite_consumption));
        if (Dm.truthy(Dm.bitand(sig_return, DESTROY_FOOD)))
        {
            qdel(owner);
            return null;
        }

        //Give a buff when the dish is hand-crafted and unbitten
        if (bitecount == 0)
            await apply_buff(eater);

        float fraction = 0.3f;
        fraction = min(Dm.num(bite_consumption) / holder.total_volume, 1);
        await holder.trans_to(eater, Dm.num(bite_consumption), transferred_by: feeder, methods: INGEST);
        if (eater.hud_used?.screen_objects?[HUD_MOB_HUNGER] is atom_movable_screen_hunger hunger_bar)
            hunger_bar.update_hunger_bar();
        bitecount++;

        await checkLiked(fraction, eater);

        await check_materials(eater as mob_living_carbon, fraction);

        if (!Dm.truthy(holder.total_volume))
            await On_Consume(eater, feeder);

        //Invoke our after eat callback if it is valid
        if (after_eat != null)
            await after_eat.Invoke(eater, feeder, bitecount);

        //Invoke the eater's stomach's after_eat callback if valid
        if (iscarbon(eater))
        {
            mob_living_carbon carbon_eater = (mob_living_carbon)eater;
            if (carbon_eater.get_organ_slot(ORGAN_SLOT_STOMACH) is obj_item_organ_stomach stomach)
                await stomach.after_eat(owner);
        }

        return TRUE;
    }

    ///Perform operations based on materials and/or if the parent object is a stack.
    // tg: code/datums/components/food/edible.dm:532
    public virtual async DmTask check_materials(mob_living_carbon eater = null, object fraction = null)
    {
        atom owner = (atom)parent;
        bool is_stack = isstack(owner);

        //food may also apply golem buffs if it contains certain materials and the mob has the required trait
        if (Dm.truthy(owner.custom_materials) && HAS_TRAIT(eater, TRAIT_ROCK_EATER))
        {
            foreach (datum_material material in Dm.each_cast<datum_material>(owner.custom_materials))
            {
                float effect_stack_amount = Dm.num(owner.custom_materials[material]) * Dm.num(fraction) / SHEET_MATERIAL_AMOUNT;
                datum_golem_food_buff effect = GLOB.golem_stack_food_directory[Dm.truthy(material.sheet_type) ? material.sheet_type : material.ore_type] as datum_golem_food_buff;
                if (effect != null && Dm.truthy(effect.can_consume(eater)))
                    effect.on_consumption(eater, owner as atom_movable, effect_stack_amount);
            }
        }

        if (Dm.num(fraction) >= 1) //don't bother if the item is about to be deleted anyway...
            return;

        if (is_stack) //stacks use up sheets, which recalulates the materials already.
        {
            obj_item_stack stack = (obj_item_stack)owner;
            await stack.use(CEILING(Dm.num(stack.amount) * Dm.num(fraction), 1));
        }
        else if (Dm.truthy(owner.custom_materials))
            await owner.set_custom_materials(owner.custom_materials, 1 - Dm.num(fraction));
    }

    ///Checks whether or not the eater can actually consume the food
    // tg: code/datums/components/food/edible.dm:560
    public virtual object CanConsume(mob_living_carbon eater = null, mob_living feeder = null)
    {
        if (!iscarbon(eater))
            return FALSE;
        if (Dm.truthy(eater.is_mouth_covered()))
        {
            eater.balloon_alert(feeder, I18n.T("mouth is covered!"));
            return FALSE;
        }

        atom food = (atom)parent;

        if (Dm.truthy(Dm.bitand(food.flags_1, HOLOGRAM_1)))
        {
            if (eater == feeder)
                to_chat(eater, span_notice(I18n.T("You try to take a bite out of [], but it fades away!", food)));
            else
                to_chat(feeder, span_notice(I18n.T("You try to feed [] [], but it fades away!", eater, food)));

            qdel(food);
            return FALSE;
        }

        if (Dm.truthy(Dm.bitand(!Dm.truthy(eater._listen_lookup?[COMSIG_CARBON_ATTEMPT_EAT]) ? NONE : eater._SendSignal(COMSIG_CARBON_ATTEMPT_EAT, Dm.list(eater, food)), BLOCK_EAT_ATTEMPT)))
            return FALSE;
        if (Dm.truthy(Dm.bitand(!Dm.truthy(food._listen_lookup?[COMSIG_FOOD_ATTEMPT_EAT]) ? NONE : food._SendSignal(COMSIG_FOOD_ATTEMPT_EAT, Dm.list(food, eater, feeder)), BLOCK_EAT_ATTEMPT)))
            return FALSE;
        return TRUE;
    }

    ///Applies food buffs according to the crafting complexity
    // tg: code/datums/components/food/edible.dm:585
    public virtual async DmTask apply_buff(mob eater = null)
    {
        object buff = null;
        float recipe_complexity = Dm.num(get_recipe_complexity());
        if (recipe_complexity <= 0)
            return;
        if (parent is obj_item_food food && food.crafted_food_buff != null)
            buff = food.crafted_food_buff;
        else
            buff = pick_weight(GLOB.food_buffs[min(recipe_complexity, FOOD_COMPLEXITY_5)] as DmList);
        if (buff != null)
        {
            mob_living living_eater = (mob_living)eater;
            atom owner = (atom)parent;
            float timeout_mod = Dm.num(((datum_reagents)owner.reagents).get_average_purity(typeof(datum_reagent_consumable))) * 2; // buff duration is 100% at average purity of 50%
            float strength = recipe_complexity;
            await living_eater.apply_status_effect((Type)buff, timeout_mod, strength);
        }
    }

    ///Check foodtypes to see if we should send a moodlet
    // tg: code/datums/components/food/edible.dm:604
    public virtual async DmTask<object> checkLiked(object fraction = null, mob eater = null)
    {
        if (Dm.num(last_check_time) + 50 > world.time)
            return FALSE;
        if (!ishuman(eater))
            return FALSE;
        mob_living_carbon_human gourmand = (mob_living_carbon_human)eater;

        if (parent is obj_item_food food)
        {
            if (Dm.num(food.venue_value) >= FOOD_PRICE_EXOTIC)
                gourmand.add_mob_memory(typeof(datum_memory_good_food), Dm.named("food", parent));
        }

        //Bruh this breakfast thing is cringe and shouldve been handled separately from food-types, remove this in the future (Actually, just kill foodtypes in general)
        if (Dm.truthy(Dm.bitand(foodtypes, BREAKFAST)) && world.time - SSticker.round_start_time < STOP_SERVING_BREAKFAST)
            await gourmand.add_mood_event("breakfast", typeof(datum_mood_event_breakfast));
        last_check_time = world.time;

        float food_quality = Dm.num(await get_perceived_food_quality(gourmand));
        if (food_quality <= FOOD_QUALITY_DANGEROUS && Dm.truthy(await gourmand.check_allergic_reaction(Dm.num(foodtypes), chance: 100, histamine_add: 10)))
            return null;

        if (food_quality <= TOXIC_FOOD_QUALITY_THRESHOLD)
        {
            to_chat(gourmand, span_warning(I18n.T("What the hell was that thing?!")));
            gourmand.adjust_disgust(25 + 30 * Dm.num(fraction));
            await gourmand.add_mood_event("toxic_food", typeof(datum_mood_event_disgusting_food));
            return null;
        }

        if (food_quality < 0)
        {
            to_chat(gourmand, span_notice(I18n.T("That didn't taste very good...")));
            gourmand.adjust_disgust(11 + 15 * Dm.num(fraction));
            await gourmand.add_mood_event("gross_food", typeof(datum_mood_event_gross_food));
            return null;
        }

        if (food_quality == 0)
            return null; // meh

        atom owner = (atom)parent;
        float timeout_mod = Dm.num(((datum_reagents)owner.reagents).get_average_purity(typeof(datum_reagent_consumable))) * 2; // mood event duration is 100% at average purity of 50%
        await gourmand.add_mood_event("quality_food", typeof(datum_mood_event_food), food_quality, timeout_mod);
        gourmand.adjust_disgust(-5 + -2 * food_quality * Dm.num(fraction));
        object quality_label = GLOB.food_quality_description[food_quality];
        to_chat(gourmand, span_notice(I18n.T("That's \\an [] meal.", quality_label)));
        return null;
    }

    /// Get the complexity of the crafted food. Some ingredients may influence this value.
    // tg: code/datums/components/food/edible.dm:652
    public virtual object get_recipe_complexity()
    {
        float complexity = FOOD_COMPLEXITY_0;
        if (HAS_TRAIT(parent, TRAIT_HANDMADE))
            complexity += handmade_complexity;
        DmList complexity_holder = Dm.list(complexity);
        if (Dm.truthy(parent._listen_lookup?[COMSIG_FOOD_GET_EXTRA_COMPLEXITY]))
            parent._SendSignal(COMSIG_FOOD_GET_EXTRA_COMPLEXITY, Dm.list(parent, complexity_holder));
        return complexity_holder[1];
    }

    /// Get food quality adjusted according to eater's preferences
    // tg: code/datums/components/food/edible.dm:661
    public virtual async DmTask<object> get_perceived_food_quality(mob_living eater = null)
    {
        float food_quality = Dm.num(get_recipe_complexity());
        DmList extra_quality = Dm.list();
        if (Dm.truthy(eater._listen_lookup?[COMSIG_LIVING_GET_PERCEIVED_FOOD_QUALITY]))
            eater._SendSignal(COMSIG_LIVING_GET_PERCEIVED_FOOD_QUALITY, Dm.list(eater, this, extra_quality));
        foreach (object quality in Dm.each(extra_quality))
            food_quality += Dm.num(quality);

        if (HAS_TRAIT(parent, TRAIT_FOOD_SILVER)) // it's not real food
        {
            if (!Dm.truthy(Dm.bitand(eater.mob_biotypes, MOB_SLIME))) //if you aren't a jellyperson, it makes you sick no matter how nice it looks
                return TOXIC_FOOD_QUALITY_THRESHOLD;
            food_quality += LIKED_FOOD_QUALITY_CHANGE;
        }

        if (check_liked != null) //Callback handling; use this as an override for special food like donuts
        {
            object special_reaction = await check_liked.Invoke(eater);
            // switch(special_reaction): return early for special foods
            if (Dm.eq(special_reaction, FOOD_LIKED))
                return LIKED_FOOD_QUALITY_CHANGE;
            if (Dm.eq(special_reaction, FOOD_DISLIKED))
                return DISLIKED_FOOD_QUALITY_CHANGE;
            if (Dm.eq(special_reaction, FOOD_TOXIC))
                return TOXIC_FOOD_QUALITY_THRESHOLD;
            if (Dm.eq(special_reaction, FOOD_ALLERGIC))
                return FOOD_QUALITY_DANGEROUS;
        }

        if (ishuman(eater))
        {
            if (Dm.truthy(Dm.bitand(foodtypes, eater.get_allergic_foodtypes())))
                return FOOD_QUALITY_DANGEROUS;
            if (Dm.truthy(count_matching_foodtypes(foodtypes, eater.get_toxic_foodtypes()))) //if the food is toxic, we don't care about anything else
                return TOXIC_FOOD_QUALITY_THRESHOLD;
            if (HAS_TRAIT(eater, TRAIT_AGEUSIA)) //if you can't taste it, it doesn't taste good
                return 0;
            food_quality += DISLIKED_FOOD_QUALITY_CHANGE * Dm.num(count_matching_foodtypes(foodtypes, eater.get_disliked_foodtypes()));
            food_quality += LIKED_FOOD_QUALITY_CHANGE * Dm.num(count_matching_foodtypes(foodtypes, eater.get_liked_foodtypes()));
        }

        return min(food_quality, FOOD_QUALITY_TOP);
    }

    /// Get the number of matching food types in provided bitfields
    // tg: code/datums/components/food/edible.dm:694
    public virtual object count_matching_foodtypes(object bitfield_one = null, object bitfield_two = null)
    {
        float count = 0;
        float matching_bits = Dm.num(Dm.bitand(bitfield_one, bitfield_two));
        while (matching_bits > 0)
        {
            if (Dm.truthy(Dm.bitand(matching_bits, 1)))
                count++;
            matching_bits = Dm.shr(matching_bits, 1);
        }
        return count;
    }

    ///Delete the item when it is fully eaten
    // tg: code/datums/components/food/edible.dm:703
    public virtual async DmTask On_Consume(mob_living eater = null, mob_living feeder = null)
    {
        if (Dm.truthy(parent._listen_lookup?[COMSIG_FOOD_CONSUMED]))
            parent._SendSignal(COMSIG_FOOD_CONSUMED, Dm.list(parent, eater, feeder));
        if (Dm.truthy(eater._listen_lookup?[COMSIG_LIVING_FINISH_EAT]))
            eater._SendSignal(COMSIG_LIVING_FINISH_EAT, Dm.list(eater, parent, feeder));

        if (on_consume != null)
            await on_consume.Invoke(eater, feeder);
        if (QDELETED(parent)) // might be destroyed by the callback
            return;

        to_chat(feeder, span_warning(I18n.T("There is nothing left of [], oh no!", parent)));
        if (isturf(parent))
        {
            turf T = (turf)parent;
            await T.ScrapeAway(1, CHANGETURF_INHERIT_AIR);
        }
        else
            qdel(parent);
    }

    ///Ability to feed food to puppers
    // tg: code/datums/components/food/edible.dm:719
    [SignalHandler]
    public virtual object UseByAnimal(datum source = null, mob_living_basic_pet_dog doggy = null)
    {
        // isdog(A) is istype(A, /mob/living/basic/pet/dog) (__DEFINES/is_helpers.dm)
        if (!(doggy is mob_living_basic_pet_dog) || Dm.truthy(Dm.bitand(food_flags, FOOD_NO_BITECOUNT))) //this entirely relies on bitecounts alas
            return null;

        atom food = (atom)parent;

        if (Dm.truthy(Dm.bitand(food.flags_1, HOLOGRAM_1)))
        {
            to_chat(doggy, span_notice(I18n.T("You try to take a bite out of [], but it fades away!", food)));
            qdel(food);
            return null;
        }

        if (bitecount == 0 || prob(50))
            doggy.manual_emote(I18n.T("nibbles away at \\the [].", food));
        bitecount++;
        object dot = COMPONENT_CANCEL_ATTACK_CHAIN;

        doggy.taste_container((datum_reagents)food.reagents); // why should carbons get all the fun?
        if (bitecount >= 5)
        {
            object satisfaction_text = pick(I18n.T("burps from enjoyment."), I18n.T("yaps for more!"), I18n.T("woofs twice."), I18n.T("looks at the area where \\the [] was.", food));
            doggy.manual_emote(satisfaction_text);
            qdel(food);
        }
        return dot;
    }

    ///Ability to feed food to puppers
    // tg: code/datums/components/food/edible.dm:745
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (QDELETED(parent))
            return;
        if (Dm.truthy(parent._listen_lookup?[COMSIG_FOOD_CROSSED]))
            parent._SendSignal(COMSIG_FOOD_CROSSED, Dm.list(parent, arrived, bitecount));
    }

    ///Response to being used to customize something
    // tg: code/datums/components/food/edible.dm:752
    [SignalHandler]
    public virtual void used_to_customize(datum source = null, atom customized = null)
    {
        if (Dm.truthy(customized._listen_lookup?[COMSIG_FOOD_INGREDIENT_ADDED]))
            customized._SendSignal(COMSIG_FOOD_INGREDIENT_ADDED, Dm.list(customized, this));
    }

    ///Response to an edible ingredient being added to parent.
    // tg: code/datums/components/food/edible.dm:758
    [SignalHandler]
    public virtual void edible_ingredient_added(datum source = null, datum_component_edible ingredient = null)
    {
        Waiting.NoSleep(InheritComponent(ingredient, TRUE));
    }

    /// Response to oozes trying to eat something edible
    // tg: code/datums/components/food/edible.dm:764
    [SignalHandler]
    public virtual object on_ooze_eat(datum source = null, mob eater = null, object edible_flags = null)
    {
        atom food = (atom)parent;

        if (Dm.truthy(Dm.bitand(food.flags_1, HOLOGRAM_1)))
        {
            to_chat(eater, span_notice(I18n.T("You try to take a bite out of [], but it fades away!", food)));
            qdel(food);
            return COMPONENT_ATOM_EATEN;
        }

        if (Dm.truthy(Dm.bitand(foodtypes, edible_flags)))
        {
            datum_reagents holder = (datum_reagents)food.reagents;
            Waiting.NoSleep(holder.trans_to(eater, holder.total_volume, transferred_by: eater));
            eater.visible_message(span_warning(I18n.T("[] eats []!", eater, food)), span_notice(I18n.T("You eat [].", food)));
            playsound(get_turf(eater), Dm.resource("sound/items/eatfood.ogg"), rand(30, 50), TRUE);
            qdel(food);
            return COMPONENT_ATOM_EATEN;
        }
        return null;
    }

    ///Calls on_edible_applied() for the main material composing the atom parent
    // tg: code/datums/components/food/edible.dm:778
    [SignalHandler]
    public virtual void on_material_effects(atom source = null, DmList materials = null, datum_material main_material = null)
    {
        if (Dm.eq(Dm.bitand(source.material_flags, REQUIRED_MAT_FLAGS), REQUIRED_MAT_FLAGS))
            Waiting.NoSleep(main_material.on_edible_applied(source, this));
    }

    ///Calls on_edible_removed() for the main material no longer composing the atom parent
    // tg: code/datums/components/food/edible.dm:784
    [SignalHandler]
    public virtual void on_remove_material_effects(atom source = null, DmList materials = null, datum_material main_material = null)
    {
        if (Dm.eq(Dm.bitand(source.material_flags, REQUIRED_MAT_FLAGS), REQUIRED_MAT_FLAGS))
            Waiting.NoSleep(main_material.on_edible_removed(source, this));
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/food/edible.dm:85  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(on_entered))
    internal DmList datum_component_edible__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(datum_component_edible.on_entered)));
}
