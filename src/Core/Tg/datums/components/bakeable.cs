// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/bakeable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333). The var block is hand-written: tg's `var/atom/bake_result` holds the typepath the
// component is given (rules 3.2 V3, typepaths read from an argument), which the generator would type as an atom.

///This component indicates this object can be baked in an oven.
// tg: code/datums/components/bakeable.dm:2
public partial class datum_component_bakeable : datum_component
{
    ///Result atom type of baking this object
    // tg: code/datums/components/bakeable.dm:5
    public Type bake_result;

    // tg: code/datums/components/bakeable.dm:2 (the type's own var defaults: a hand-written var block, types-converter.md 3.2)
    public datum_component_bakeable()
    {
        dupe_mode = COMPONENT_DUPE_UNIQUE_PASSARGS; // So you can change bake results with various cookstuffs
    }

    // tg: code/datums/components/bakeable.dm:20
    [DmParams("bake_result", "required_bake_time", "positive_result", "use_large_steam_sprit", "added_reagents")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        Type bake_result = Dm.arg(__extra, 0) as Type;
        object required_bake_time = Dm.arg(__extra, 1);
        object positive_result = Dm.arg(__extra, 2);
        DmList added_reagents = Dm.arg(__extra, 4) as DmList;
        object dot = await base.Initialize(__extra);
        if (!isitem(parent)) //Only items support baking at the moment
            return COMPONENT_INCOMPATIBLE;

        this.bake_result = bake_result;
        this.required_bake_time = Dm.num(required_bake_time);
        this.positive_result = Dm.num(positive_result);
        this.added_reagents = added_reagents;
        if (Dm.truthy(positive_result))
            ADD_TRAIT(parent, TRAIT_BAKEABLE, REF(this));

        obj_item item_target = (obj_item)parent;
        // PERFORM_ALL_TESTS(focus_only/check_materials_when_processed) is FALSE in production (rules 1.7)
        if (!Dm.truthy(FALSE) || !Dm.truthy(positive_result) || !Dm.truthy(item_target.custom_materials))
            return dot;

        atom result = Dm.@new(bake_result) as atom;
        if (!Dm.truthy(item_target.compare_materials(result)))
        {
            string warning = Dm.text("custom_materials of [] when baked compared to just spawned don't match", result.type);
            object what_it_should_be = item_target.transcribe_materials_list();
            stack_trace(Dm.text("[]. should be: custom_materials = [].", warning, what_it_should_be), "code/datums/components/bakeable.dm", 41);
        }
        qdel(result);
        return dot;
    }

    // Inherit the new values passed to the component
    // tg: code/datums/components/bakeable.dm:45
    [DmParams("new_comp", "original", "bake_result", "required_bake_time", "positive_result", "use_large_steam_sprite")]
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        Type bake_result = Dm.arg(__extra, 0) as Type;
        object required_bake_time = Dm.arg(__extra, 1);
        object positive_result = Dm.arg(__extra, 2);
        if (!Dm.truthy(i_am_original))
            return;
        if (bake_result != null)
            this.bake_result = bake_result;
        if (Dm.truthy(required_bake_time))
            this.required_bake_time = Dm.num(required_bake_time);
        if (Dm.truthy(positive_result))
            this.positive_result = Dm.num(positive_result);
    }

    // tg: code/datums/components/bakeable.dm:55
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ITEM_OVEN_PLACED_IN, nameof(on_baking_start));
        RegisterSignal(parent, COMSIG_ITEM_OVEN_PROCESS, nameof(on_bake));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
    }

    // tg: code/datums/components/bakeable.dm:60
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_ITEM_OVEN_PLACED_IN, COMSIG_ITEM_OVEN_PROCESS, COMSIG_ATOM_EXAMINE));
        REMOVE_TRAIT(parent, TRAIT_BAKEABLE, REF(this));
    }

    /// Signal proc for [COMSIG_ITEM_OVEN_PLACED_IN] when baking starts (parent enters an oven)
    // tg: code/datums/components/bakeable.dm:65
    [SignalHandler]
    public virtual void on_baking_start(datum source = null, atom used_oven = null, mob baker = null)
    {
        if (baker != null && baker.mind != null)
            who_baked_us = REF(baker.mind);
    }

    ///Ran every time an item is baked by something
    // tg: code/datums/components/bakeable.dm:72
    [SignalHandler]
    public virtual object on_bake(datum source = null, atom used_oven = null, float? seconds_per_tick = null)
    {
        seconds_per_tick ??= 1;
        // Let our signal know if we're baking something good or ... burning something
        float baking_result = Dm.truthy(positive_result) ? COMPONENT_BAKING_GOOD_RESULT : COMPONENT_BAKING_BAD_RESULT;

        current_bake_time += Dm.num(seconds_per_tick) * 10; //turn it into ds
        if (current_bake_time >= required_bake_time)
            Waiting.NoSleep(finish_baking(used_oven));

        return Dm.bitor(COMPONENT_HANDLED_BAKING, baking_result);
    }

    ///Ran when an object finished baking
    // tg: code/datums/components/bakeable.dm:85
    public virtual async DmTask finish_baking(atom used_oven = null)
    {
        atom original_object = (atom)parent;
        obj_item_plate_oven_tray used_tray = original_object.loc as obj_item_plate_oven_tray;
        atom baked_result = Dm.@new(bake_result, used_tray) as atom;
        if (Dm.truthy(positive_result) && original_object is obj_item_food && baked_result is obj_item_food)
        {
            obj_item_food original_food = (obj_item_food)original_object;
            obj_item_food baked_food = (obj_item_food)baked_result;
            LAZYADD(ref baked_food.intrinsic_food_materials, original_food.intrinsic_food_materials);
        }
        //make space and tranfer reagents if it has any, also let any bad result handle removing or converting the transferred reagents on its own terms
        if (baked_result.reagents != null && original_object.reagents != null)
        {
            await ((datum_reagents)baked_result.reagents).clear_reagents();
            await ((datum_reagents)original_object.reagents).trans_to(baked_result, ((datum_reagents)original_object.reagents).total_volume);
            if (Dm.truthy(added_reagents)) // Add any new reagents that should be added
                await ((datum_reagents)baked_result.reagents).add_reagent_list(added_reagents);
        }

        if (Dm.truthy(who_baked_us))
            ADD_TRAIT(baked_result, TRAIT_HANDMADE, who_baked_us);

        if (Dm.truthy(original_object.custom_materials))
            await baked_result.set_custom_materials(original_object.custom_materials, 1);

        baked_result.pixel_x = original_object.pixel_x;
        baked_result.pixel_y = original_object.pixel_y;
        used_tray.AddToPlate(baked_result as obj_item);

        DmList asomnia_hadders = Dm.list();
        foreach (mob smeller in Dm.each<mob>(get_hearers_in_view(DEFAULT_MESSAGE_RANGE, used_oven)))
        {
            if (HAS_TRAIT(smeller, TRAIT_ANOSMIA))
                Dm.append(ref asomnia_hadders, smeller);
        }

        if (Dm.truthy(positive_result))
        {
            used_oven.visible_message(
                span_notice(I18n.T("You smell something great coming from [].", used_oven)),
                blind_message: span_notice(I18n.T("You smell something great...")),
                ignored_mobs: asomnia_hadders);
            // BLACKBOX_LOG_FOOD_MADE(baked_result.type): SSblackbox.record_feedback("tally", "food_made", 1, type) (__DEFINES/food.dm)
            SSblackbox.record_feedback("tally", "food_made", 1, baked_result.type);
        }
        else
        {
            used_oven.visible_message(
                span_warning(I18n.T("You smell a burnt smell coming from [].", used_oven)),
                blind_message: span_warning(I18n.T("You smell a burnt smell...")),
                ignored_mobs: asomnia_hadders);
        }
        if (Dm.truthy(parent._listen_lookup?[COMSIG_ITEM_BAKED]))
            parent._SendSignal(COMSIG_ITEM_BAKED, Dm.list(parent, baked_result));
        if (Dm.truthy(baked_result._listen_lookup?[COMSIG_ITEM_BAKED_RESULT]))
            baked_result._SendSignal(COMSIG_ITEM_BAKED_RESULT, Dm.list(baked_result, parent));
        qdel(parent);
    }

    ///Gives info about the items baking status so you can see if its almost done
    // tg: code/datums/components/bakeable.dm:133
    [SignalHandler]
    public virtual void on_examine(atom source = null, mob user = null, DmList examine_list = null)
    {
        if (!Dm.truthy(current_bake_time)) //Not baked yet
        {
            if (Dm.truthy(positive_result))
            {
                if (Dm.eq(Dm.initial(bake_result, (atom a) => a.gender), PLURAL))
                    Dm.append(ref examine_list, span_notice(I18n.T("[] can be [] into some [].", parent, span_bold(I18n.T("baked")), Dm.initial(bake_result, (atom a) => a.name))));
                else
                    Dm.append(ref examine_list, span_notice(I18n.T("[] can be [] into \\a [].", parent, span_bold(I18n.T("baked")), Dm.initial(bake_result, (atom a) => a.name))));
            }
            return;
        }

        if (Dm.truthy(positive_result))
        {
            if (current_bake_time <= required_bake_time * 0.75f)
                Dm.append(ref examine_list, span_notice(I18n.T("[] probably needs to be baked a bit longer!", parent)));
            else if (current_bake_time <= required_bake_time)
                Dm.append(ref examine_list, span_notice(I18n.T("[] seems to be almost finished baking!", parent)));
        }
        else
            Dm.append(ref examine_list, span_danger(I18n.T("[] should probably not be put in the oven.", parent)));
    }
}
