// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/food/microwavable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333). The var block is hand-written: tg's `var/atom/result_typepath` holds the typepath the
// element is given (rules 3.2 V3), which the generator would type as an atom.

/// Atoms that can be microwaved from one type to another.
// tg: code/datums/elements/food/microwavable.dm:2
public partial class datum_element_microwavable : datum_element
{
    /// Resulting atom typepath on a completed microwave.
    // tg: code/datums/elements/food/microwavable.dm:6
    public Type result_typepath;

    // tg: code/datums/elements/food/microwavable.dm:2 (the type's own var defaults: a hand-written var block, types-converter.md 3.2)
    public datum_element_microwavable()
    {
        element_flags = ELEMENT_BESPOKE;
        argument_hash_start_idx = 2;
    }

    // tg: code/datums/elements/food/microwavable.dm:10
    [DmParams("target", "microwave_type", "reagents", "bad_recipe")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object microwave_type = Dm.arg(__extra, 0);
        DmList reagents = Dm.arg(__extra, 1) as DmList;
        object bad_recipe = Dm.arg(__extra, 2) ?? FALSE;
        object dot = await base.Attach(target, __extra);
        if (target is not obj_item target__item)
            return ELEMENT_INCOMPATIBLE;
        if (!Dm.truthy(microwave_type))
            throw Dm.CRASH("microwavable element attached without a microwave_type arg", "code/datums/elements/food/microwavable.dm", 15);

        result_typepath = microwave_type as Type;
        added_reagents = reagents;
        if (!Dm.truthy(bad_recipe))
        {
            ADD_TRAIT(target, TRAIT_MICROWAVABLE, REF(this));
            RegisterSignal(target, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        }

        RegisterSignal(target, COMSIG_ITEM_MICROWAVE_ACT, nameof(on_microwaved));

        // PERFORM_ALL_TESTS(focus_only/check_materials_when_processed) is FALSE in production (rules 1.7)
        if (!Dm.truthy(FALSE) || Dm.truthy(bad_recipe) || !Dm.truthy(target__item.custom_materials) || isstack(target))
            return dot;

        atom result = Dm.@new(result_typepath) as atom;
        if (!Dm.truthy(target__item.compare_materials(result)))
        {
            string warning = Dm.text("custom_materials of [] when microwaved compared to just spawned don't match", result.type);
            object what_it_should_be = target__item.transcribe_materials_list();
            stack_trace(Dm.text("[]. should be: custom_materials = [].", warning, what_it_should_be), "code/datums/elements/food/microwavable.dm", 32);
        }
        qdel(result);
        return dot;
    }

    // tg: code/datums/elements/food/microwavable.dm:35
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, Dm.list(COMSIG_ITEM_MICROWAVE_ACT, COMSIG_ATOM_EXAMINE));
        return base.Detach(source, __extra);
    }

    /**
     * Signal proc for [COMSIG_ITEM_MICROWAVE_ACT].
     * Handles the actual microwaving part.
     */
    // tg: code/datums/elements/food/microwavable.dm:43
    [SignalHandler]
    public virtual object on_microwaved(atom source = null, obj_machinery_microwave used_microwave = null, mob microwaver = null, object randomize_pixel_offset = null)
    {
        atom result;
        turf result_loc = get_turf(used_microwave != null ? used_microwave : source);
        if (isstack(source))
        {
            obj_item_stack stack_source = (obj_item_stack)source;
            result = Dm.@new(result_typepath, result_loc, stack_source.amount) as atom;
        }
        else
        {
            result = Dm.@new(result_typepath, result_loc) as atom;
            Waiting.NoSleep(result.set_custom_materials(source.custom_materials));
        }

        float efficiency = used_microwave is obj_machinery_microwave ? used_microwave.efficiency : 1;

        if (IS_EDIBLE(result) && HAS_TRAIT(source, TRAIT_MICROWAVABLE))
        {
            // BLACKBOX_LOG_FOOD_MADE(result.type): SSblackbox.record_feedback("tally", "food_made", 1, type) (__DEFINES/food.dm)
            SSblackbox.record_feedback("tally", "food_made", 1, result.type);

            if (source is obj_item_food && result is obj_item_food)
            {
                obj_item_food original_food = (obj_item_food)source;
                obj_item_food microwaved_food = (obj_item_food)result;
                LAZYADD(ref microwaved_food.intrinsic_food_materials, original_food.intrinsic_food_materials);
            }

            if (microwaver != null && microwaver.mind != null)
                ADD_TRAIT(result, TRAIT_HANDMADE, REF(microwaver.mind));
        }

        //make space and tranfer reagents if it has any, also let any bad result handle removing or converting the transferred reagents on its own terms
        if (result.reagents != null && source.reagents != null)
        {
            Waiting.NoSleep(((datum_reagents)result.reagents).clear_reagents());
            Waiting.NoSleep(((datum_reagents)source.reagents).trans_to(result, ((datum_reagents)source.reagents).total_volume));
            if (Dm.truthy(added_reagents)) // Add any new reagents that should be added
                Waiting.NoSleep(((datum_reagents)result.reagents).add_reagent_list(added_reagents));
        }

        if (Dm.truthy(source._listen_lookup?[COMSIG_ITEM_MICROWAVE_COOKED]))
            source._SendSignal(COMSIG_ITEM_MICROWAVE_COOKED, Dm.list(source, result, efficiency));
        if (Dm.truthy(result._listen_lookup?[COMSIG_ITEM_MICROWAVE_COOKED_RESULT]))
            result._SendSignal(COMSIG_ITEM_MICROWAVE_COOKED_RESULT, Dm.list(result, source, efficiency));

        float recipe_result = COMPONENT_MICROWAVE_SUCCESS;
        if (!HAS_TRAIT(source, TRAIT_MICROWAVABLE))
            Dm.combine(ref recipe_result, COMPONENT_MICROWAVE_BAD_RECIPE);

        qdel(source);

        if (Dm.truthy(randomize_pixel_offset) && isitem(result))
        {
            obj_item result_item = (obj_item)result;
            if (!Dm.truthy(Dm.bitand(result_item.item_flags, NO_PIXEL_RANDOM_DROP)))
            {
                result_item.pixel_x = result_item.base_pixel_x + rand(-6, 6);
                result_item.pixel_y = result_item.base_pixel_y + rand(-5, 6);
            }
        }

        return recipe_result;
    }

    /**
     * Signal proc for [COMSIG_ATOM_EXAMINE].
     * Lets examiners know we can be microwaved if we're not the default mess type
     */
    // tg: code/datums/elements/food/microwavable.dm:96
    [SignalHandler]
    public virtual void on_examine(atom source = null, mob user = null, DmList examine_list = null)
    {
        if (Dm.eq(Dm.initial(result_typepath, (atom a) => a.gender), PLURAL))
            Dm.append(ref examine_list, span_notice(I18n.T("[] can be [] into some [].", source, span_bold(I18n.T("microwaved")), Dm.initial(result_typepath, (atom a) => a.name))));
        else
            Dm.append(ref examine_list, span_notice(I18n.T("[] can be [] into \\a [].", source, span_bold(I18n.T("microwaved")), Dm.initial(result_typepath, (atom a) => a.name))));
    }
}
