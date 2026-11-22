// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/grillable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333). The var block is generated (D111).

// tg: code/datums/components/grillable.dm:3
public partial class datum_component_grillable : datum_component
{
    // tg: code/datums/components/grillable.dm:1  #define IDEAL_GRILLING_TEMPERATURE 200 + T0C (file-local, #undef at :232)
    private const float IDEAL_GRILLING_TEMPERATURE = 200 + T0C;

    // tg: code/datums/components/grillable.dm:24
    [DmParams("cook_result", "required_cook_time", "positive_result", "use_large_steam_sprite", "added_reagents")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        Type cook_result = Dm.arg(__extra, 0) as Type;
        object required_cook_time = Dm.arg(__extra, 1);
        object positive_result = Dm.arg(__extra, 2);
        object use_large_steam_sprite = Dm.arg(__extra, 3);
        DmList added_reagents = Dm.arg(__extra, 4) as DmList;
        object dot = await base.Initialize(__extra);
        if (!isitem(parent)) //Only items support grilling at the moment
            return COMPONENT_INCOMPATIBLE;

        this.cook_result = cook_result;
        this.required_cook_time = Dm.num(required_cook_time);
        if (Dm.truthy(positive_result))
            ADD_TRAIT(parent, TRAIT_GRILLABLE, REF(this));
        this.use_large_steam_sprite = Dm.num(use_large_steam_sprite);
        this.added_reagents = added_reagents;

        obj_item item_parent = (obj_item)parent;
        // PERFORM_ALL_TESTS(focus_only/check_materials_when_processed) is FALSE in production (rules 1.7)
        if (!Dm.truthy(FALSE) || !Dm.truthy(positive_result) || !Dm.truthy(item_parent.custom_materials) || isstack(parent))
            return dot;

        atom result = Dm.@new(cook_result) as atom;
        if (!Dm.truthy(item_parent.compare_materials(result)))
        {
            string warning = Dm.text("custom_materials of [] when grilled compared to just spawned don't match", result.type);
            object what_it_should_be = item_parent.transcribe_materials_list();
            stack_trace(Dm.text("[]. should be: custom_materials = [].", warning, what_it_should_be), "code/datums/components/grillable.dm", 44);
        }
        qdel(result);
        return dot;
    }

    // tg: code/datums/components/grillable.dm:47
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ITEM_GRILL_PLACED, nameof(on_grill_placed));
        RegisterSignal(parent, COMSIG_ITEM_GRILL_TURNED_ON, nameof(on_grill_turned_on));
        RegisterSignal(parent, COMSIG_ITEM_GRILL_TURNED_OFF, nameof(on_grill_turned_off));
        RegisterSignal(parent, COMSIG_ITEM_GRILL_PROCESS, nameof(on_grill));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(on_location_changed));
        on_location_changed(parent as atom);
    }

    // tg: code/datums/components/grillable.dm:56
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        if (listening_turf != null)
            UnregisterSignal(listening_turf, COMSIG_TURF_EXPOSE);

        UnregisterSignal(parent, Dm.list(
            COMSIG_ATOM_EXAMINE,
            COMSIG_ITEM_GRILL_TURNED_ON,
            COMSIG_ITEM_GRILL_TURNED_OFF,
            COMSIG_ITEM_GRILL_PROCESS,
            COMSIG_ITEM_GRILL_PLACED,
            COMSIG_MOVABLE_MOVED
        ));
    }

    // Inherit the new values passed to the component
    // tg: code/datums/components/grillable.dm:70
    [DmParams("new_comp", "original", "cook_result", "required_cook_time", "positive_result", "use_large_steam_sprite")]
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        Type cook_result = Dm.arg(__extra, 0) as Type;
        object required_cook_time = Dm.arg(__extra, 1);
        object positive_result = Dm.arg(__extra, 2);
        object use_large_steam_sprite = Dm.arg(__extra, 3);
        if (!Dm.truthy(i_am_original))
            return;
        if (cook_result != null)
            this.cook_result = cook_result;
        if (Dm.truthy(required_cook_time))
            this.required_cook_time = Dm.num(required_cook_time);
        if (Dm.truthy(positive_result))
            ADD_TRAIT(parent, TRAIT_GRILLABLE, REF(this));
        else
            REMOVE_TRAIT(parent, TRAIT_GRILLABLE, REF(this));
        if (Dm.truthy(use_large_steam_sprite))
            this.use_large_steam_sprite = Dm.num(use_large_steam_sprite);
    }

    // tg: code/datums/components/grillable.dm:84
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        STOP_PROCESSING(SSmachines, this);
        listening_turf = null;
        return dot;
    }

    /// Signal proc for [COMSIG_MOVABLE_MOVED], our location has changed and we should register for temperature information
    // tg: code/datums/components/grillable.dm:90
    [SignalHandler]
    public virtual void on_location_changed(atom source = null)
    {
        if (Dm.truthy(is_grilling))
        {
            on_grill_turned_off(source);
            STOP_PROCESSING(SSmachines, this);
        }

        if (listening_turf != null)
            UnregisterSignal(listening_turf, COMSIG_TURF_EXPOSE);

        if (source == null)
            return;

        turf_open current_turf = source.loc as turf_open;
        if (!isopenturf(source.loc))
            return;
        listening_turf = current_turf;
        RegisterSignal(current_turf, COMSIG_TURF_EXPOSE, nameof(on_turf_atmos_changed));
        on_turf_atmos_changed(current_turf, current_turf.air, (current_turf.air?.temperature is var or__1 && Dm.truthy(or__1) ? or__1 : 0));
    }

    /// Signal proc for [COMSIG_ITEM_GRILL_PLACED], item is placed on the grill.
    // tg: code/datums/components/grillable.dm:111
    [SignalHandler]
    public virtual void on_grill_placed(datum source = null, mob griller = null)
    {
        if (griller != null && griller.mind != null)
            who_placed_us = REF(griller.mind);
    }

    /// Signal proc for [COMSIG_ITEM_GRILL_TURNED_ON], starts the grilling process.
    // tg: code/datums/components/grillable.dm:118
    public virtual void on_grill_turned_on(datum source = null)
    {
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(add_grilled_item_overlay));

        is_grilling = TRUE;
        atom atom_parent = (atom)parent;
        atom_parent.update_appearance();
    }

    /// Signal proc for [COMSIG_ITEM_GRILL_TURNED_OFF], stops the grilling process.
    // tg: code/datums/components/grillable.dm:126
    public virtual void on_grill_turned_off(datum source = null)
    {
        UnregisterSignal(parent, COMSIG_ATOM_UPDATE_OVERLAYS);

        is_grilling = FALSE;
        atom atom_parent = (atom)parent;
        atom_parent.update_appearance();
    }

    ///Ran every time an item is grilled by something
    // tg: code/datums/components/grillable.dm:134
    [SignalHandler]
    public virtual object on_grill(datum source = null, atom used_grill = null, float? seconds_per_tick = null)
    {
        seconds_per_tick ??= 1;
        object dot = COMPONENT_HANDLED_GRILLING;

        current_cook_time += Dm.num(seconds_per_tick) * 10; //turn it into ds
        if (current_cook_time >= required_cook_time)
            Waiting.NoSleep(finish_grilling(used_grill));
        return dot;
    }

    ///Ran when an object finished grilling
    // tg: code/datums/components/grillable.dm:144
    public virtual async DmTask finish_grilling(atom grill_source = null)
    {
        atom original_object = (atom)parent;
        atom grilled_result;

        if (isstack(parent)) //Check if its a sheet, for grilling multiple things in a stack
        {
            obj_item_stack stack_parent = (obj_item_stack)original_object;
            grilled_result = Dm.@new(cook_result, original_object.loc, stack_parent.amount) as atom;
        }
        else
        {
            grilled_result = Dm.@new(cook_result, original_object.loc) as atom;
            if (original_object is obj_item_food && grilled_result is obj_item_food)
            {
                obj_item_food original_food = (obj_item_food)original_object;
                obj_item_food grilled_food = (obj_item_food)grilled_result;
                LAZYADD(ref grilled_food.intrinsic_food_materials, original_food.intrinsic_food_materials);
            }
            await grilled_result.set_custom_materials(original_object.custom_materials);
        }

        if (IS_EDIBLE(grilled_result) && HAS_TRAIT(parent, TRAIT_GRILLABLE))
            // BLACKBOX_LOG_FOOD_MADE(grilled_result.type): SSblackbox.record_feedback("tally", "food_made", 1, type) (__DEFINES/food.dm)
            SSblackbox.record_feedback("tally", "food_made", 1, grilled_result.type);
        //make space and tranfer reagents if it has any, also let any bad result handle removing or converting the transferred reagents on its own terms
        if (grilled_result.reagents != null && original_object.reagents != null)
        {
            if (grilled_result.reagents != null)
                await ((datum_reagents)grilled_result.reagents).clear_reagents();
            if (original_object.reagents != null)
                await ((datum_reagents)original_object.reagents).trans_to(grilled_result, ((datum_reagents)original_object.reagents).total_volume);
            if (Dm.truthy(added_reagents)) // Add any new reagents that should be added
                await ((datum_reagents)grilled_result.reagents).add_reagent_list(added_reagents);
        }

        if (Dm.truthy(parent._listen_lookup?[COMSIG_ITEM_GRILLED]))
            parent._SendSignal(COMSIG_ITEM_GRILLED, Dm.list(parent, grilled_result));
        if (Dm.truthy(grilled_result._listen_lookup?[COMSIG_ITEM_GRILLED_RESULT]))
            grilled_result._SendSignal(COMSIG_ITEM_GRILLED_RESULT, Dm.list(grilled_result, parent));
        if (Dm.truthy(who_placed_us))
            ADD_TRAIT(grilled_result, TRAIT_HANDMADE, who_placed_us);

        grill_source.visible_message(I18n.T("<span class='[]'>[] turns into \\a []!</span>", HAS_TRAIT(parent, TRAIT_GRILLABLE) ? "notice" : "warning", parent, grilled_result));
        grilled_result.pixel_x = original_object.pixel_x;
        grilled_result.pixel_y = original_object.pixel_y;
        qdel(parent);
    }

    ///Ran when an object almost finishes grilling
    // tg: code/datums/components/grillable.dm:179
    [SignalHandler]
    public virtual void on_examine(atom A = null, mob user = null, DmList examine_list = null)
    {
        if (!Dm.truthy(current_cook_time)) //Not grilled yet
        {
            if (HAS_TRAIT(parent, TRAIT_GRILLABLE))
            {
                // tg compares the result's initial name, not its gender, with PLURAL here (bakeable and microwavable read the gender)
                if (Dm.eq(Dm.initial(cook_result, (atom a) => a.name), PLURAL))
                    Dm.append(ref examine_list, span_notice(I18n.T("[] can be [] into some [].", parent, span_bold(I18n.T("grilled")), Dm.initial(cook_result, (atom a) => a.name))));
                else
                    Dm.append(ref examine_list, span_notice(I18n.T("[] can be [] into \\a [].", parent, span_bold(I18n.T("grilled")), Dm.initial(cook_result, (atom a) => a.name))));
            }
            return;
        }

        if (HAS_TRAIT(parent, TRAIT_GRILLABLE))
        {
            if (current_cook_time <= required_cook_time * 0.75f)
                Dm.append(ref examine_list, span_notice(I18n.T("[] probably needs to be cooked a bit longer!", parent)));
            else if (current_cook_time <= required_cook_time)
                Dm.append(ref examine_list, span_notice(I18n.T("[] seems to be almost finished cooking!", parent)));
        }
        else
            Dm.append(ref examine_list, span_danger(I18n.T("[] should probably not be put on the grill.", parent)));
    }

    // tg: code/datums/components/grillable.dm:198
    [SignalHandler]
    public virtual void add_grilled_item_overlay(datum source = null, DmList overlays = null)
    {
        Dm.append(ref overlays, mutable_appearance(Dm.resource("icons/effects/steam.dmi"), Dm.text("[]", Dm.truthy(use_large_steam_sprite) ? "steam_triple" : "steam_single"), ABOVE_OBJ_LAYER));
    }

    /// Signal proc for [COMSIG_TURF_EXPOSE], atmosphere might be hot enough for grilling.
    // tg: code/datums/components/grillable.dm:204
    [SignalHandler]
    public virtual void on_turf_atmos_changed(turf_open source = null, datum_gas_mixture air = null, object exposed_temperature = null)
    {
        if (!Dm.truthy(is_grilling))
        {
            if (Dm.num(exposed_temperature) < FIRE_MINIMUM_TEMPERATURE_TO_EXIST)
                return;
            on_grill_turned_on(source);
            START_PROCESSING(SSmachines, this);
            current_temperature = Dm.num(exposed_temperature);
        }
        else
        {
            if (Dm.num(exposed_temperature) >= FIRE_MINIMUM_TEMPERATURE_TO_EXIST)
            {
                current_temperature = Dm.num(exposed_temperature);
                return;
            }
            on_grill_turned_off(source);
            STOP_PROCESSING(SSmachines, this);
        }
    }

    // Grill while exposed to hot air
    // tg: code/datums/components/grillable.dm:221
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        atom atom_parent = (atom)parent;

        // Grill faster as we approach 200 degrees celsius
        float check_temperature = clamp(Dm.num(current_temperature), FIRE_MINIMUM_TEMPERATURE_TO_EXIST, IDEAL_GRILLING_TEMPERATURE);
        float temp_scale = (check_temperature - FIRE_MINIMUM_TEMPERATURE_TO_EXIST) / (IDEAL_GRILLING_TEMPERATURE - FIRE_MINIMUM_TEMPERATURE_TO_EXIST);
        float speed_modifier = LERP(0.5f, 1, temp_scale);

        on_grill(parent, atom_parent.loc as atom, Dm.num(seconds_per_tick) * speed_modifier);
        return null;
    }
}
