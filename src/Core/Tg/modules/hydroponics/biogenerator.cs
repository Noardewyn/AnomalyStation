// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/biogenerator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file: converting food into biomass, printing the biogenerator's designs, the beaker, the Biogenerator
// window, with its auto-unlocking techweb and SSresearch's design table since silicons SI3 (D326; DV306 retired). The emissive
// overlays are DV12's.

// tg: code/modules/hydroponics/biogenerator.dm:6
public partial class obj_machinery_biogenerator : obj_machinery
{
    /// How many more items does `max_items` get increased by per rating point.
    // tg: code/modules/hydroponics/biogenerator.dm:2
    private const float MAX_ITEMS_PER_RATING = 10;
    /// How many items are converted per cycle, per rating point of the manipulator used.
    // tg: code/modules/hydroponics/biogenerator.dm:4
    private const float PROCESSED_ITEMS_PER_RATING = 5;

    // tg: code/modules/hydroponics/biogenerator.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        GLOB.autounlock_techwebs[typeof(datum_techweb_autounlocking_biogenerator)] ??= Dm.@new<datum_techweb_autounlocking_biogenerator>();
        stored_research = GLOB.autounlock_techwebs[typeof(datum_techweb_autounlocking_biogenerator)] as datum_techweb;
        soundloop = Dm.@new<datum_looping_sound_generator>(this, processing);
        if (Dm.truthy(mapload))
            welded_down = TRUE;
        return dot;
    }

    // tg: code/modules/hydroponics/biogenerator.dm:58
    public override object can_be_unfasten_wrench(mob user = null, object silent = null)
    {
        if (Dm.truthy(welded_down))
        {
            to_chat(user, span_warning(I18n.T("[] is welded to the floor!", this)));
            return FAILED_UNFASTEN;
        }
        return base.can_be_unfasten_wrench(user, silent);
    }

    // tg: code/modules/hydroponics/biogenerator.dm:64
    public override async DmTask<object> set_anchored(object anchorvalue = null)
    {
        object dot = await base.set_anchored(anchorvalue);
        if (!Dm.truthy(anchored) && Dm.truthy(welded_down)) //make sure they're keep in sync in case it was forcibly unanchored by badmins or by a megafauna.
            welded_down = FALSE;
        return dot;
    }

    // tg: code/modules/hydroponics/biogenerator.dm:69
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        await base.welder_act(user, tool);
        if (Dm.truthy(welded_down))
        {
            if (!Dm.truthy(tool.tool_start_check(user, amount: 2)))
                return TRUE;
            user.visible_message(
                span_notice(I18n.T("[] starts to cut \\the [] free from the floor.", user.name, this)),
                span_notice(I18n.T("You start to cut [] free from the floor...", this)),
                span_hear(I18n.T("You hear welding.")));
            if (!Dm.truthy(await tool.use_tool(this, user, 10 * SECONDS, volume: 100)))
                return FALSE;
            welded_down = FALSE;
            to_chat(user, span_notice(I18n.T("You cut [] free from the floor.", this)));
            return TRUE;
        }
        if (!Dm.truthy(anchored))
        {
            to_chat(user, span_warning(I18n.T("[] needs to be wrenched to the floor!", this)));
            return TRUE;
        }
        if (!Dm.truthy(tool.tool_start_check(user, amount: 2)))
            return TRUE;
        user.visible_message(
            span_notice(I18n.T("[] starts to weld \\the [] to the floor.", user.name, this)),
            span_notice(I18n.T("You start to weld [] to the floor...", this)),
            span_hear(I18n.T("You hear welding.")));
        if (!Dm.truthy(await tool.use_tool(this, user, 10 * SECONDS, volume: 100)))
        {
            balloon_alert(user, I18n.T("cancelled!"));
            return FALSE;
        }
        welded_down = TRUE;
        to_chat(user, span_notice(I18n.T("You weld [] to the floor.", this)));
        return TRUE;
    }

    // tg: code/modules/hydroponics/biogenerator.dm:101
    public override object Destroy(float? force = null)
    {
        qdel(beaker);
        beaker = null;
        qdel(soundloop);
        soundloop = null;
        return base.Destroy(force);
    }

    // tg: code/modules/hydroponics/biogenerator.dm:106
    public override async DmTask contents_explosion(object severity = null, object target = null)
    {
        await base.contents_explosion(severity, target);
        if (beaker == null)
            return;

        switch ((int)Dm.num(severity))
        {
            case EXPLODE_DEVASTATE:
                Dm.append(ref SSexplosions.high_mov_atom, beaker);
                break;
            case EXPLODE_HEAVY:
                Dm.append(ref SSexplosions.med_mov_atom, beaker);
                break;
            case EXPLODE_LIGHT:
                Dm.append(ref SSexplosions.low_mov_atom, beaker);
                break;
        }
    }

    // tg: code/modules/hydroponics/biogenerator.dm:119
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone != null && gone == beaker)
        {
            beaker = null;
            update_appearance();
        }
        return dot;
    }

    // tg: code/modules/hydroponics/biogenerator.dm:125
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();

        float new_efficiency = 0;
        float new_productivity = 0;
        float new_max_items = 10;
        float new_processed_items_per_cycle = 0;

        foreach (datum_stock_part_matter_bin bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
            new_max_items += MAX_ITEMS_PER_RATING * Dm.num(bin.tier);

        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
        {
            new_productivity += Dm.num(servo.tier);
            new_efficiency += Dm.num(servo.tier);
            new_processed_items_per_cycle += PROCESSED_ITEMS_PER_RATING * Dm.num(servo.tier);
        }

        max_items = new_max_items;
        efficiency = new_efficiency;
        productivity = new_productivity;
        processed_items_per_cycle = new_processed_items_per_cycle;

        update_appearance();
        return dot;
    }

    // tg: code/modules/hydroponics/biogenerator.dm:149
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        if (Dm.truthy(in_range(user, this)) || isobserver(user))
        {
            Dm.append(ref dot, span_notice(I18n.T("The status display reads:")));
            Dm.append(ref dot, span_notice(I18n.T(" - Productivity at <b>[]%</b>.", productivity * 100)));
            Dm.append(ref dot, span_notice(I18n.T(" - Converting <b>[]</b> pieces of food per cycle.", processed_items_per_cycle)));
            Dm.append(ref dot, span_notice(I18n.T(" - Matter consumption at <b>[]</b>%.", 1 / efficiency * 100)));
            float count = Dm.num(get_content_count());
            Dm.append(ref dot, span_notice(I18n.T(" - Internal biomass converter capacity at <b>[]</b> pieces of food, and currently holding <b>[] piece[]</b>.", max_items, count, count == 1 ? "" : "s")));
        }

        if (Dm.truthy(welded_down))
            Dm.append(ref dot, span_info(I18n.T("It's moored firmly to the floor. You can unsecure its moorings with a <b>welder</b>.")));
        return dot;
    }

    // tg: code/modules/hydroponics/biogenerator.dm:162
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);

        float power = Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))) ? 0 : 1 + Dm.min(biomass / max_visual_biomass, 1) + Dm.bitand(processing, 1);
        Waiting.NoSleep(set_light(MINIMUM_USEFUL_LIGHT_RANGE, power, LIGHT_COLOR_CYAN));
        return dot;
    }

    // tg: code/modules/hydroponics/biogenerator.dm:169
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (Dm.truthy(panel_open))
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_o_panel", icon_state)));

        if (beaker != null)
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_o_container", icon_state)));

        if (biomass > 0)
        {
            // Get current biomass volume adjusted with sine function (more biomass = less frequent icon changes)
            float biomass_volume_sin = Dm.sin(Dm.min(biomass / max_visual_biomass, 1) * 90);
            // Round up to get the corresponding overlay icon
            float biomass_level = ROUND_UP(biomass_volume_sin * 7);
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_o_biomass_[]", icon_state, biomass_level)));
            // DEVIATION(DV12): . += emissive_appearance(icon, "[icon_state]_o_biomass_[biomass_level]", src)
        }

        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
            return dot;

        if (Dm.truthy(processing))
        {
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_o_process", icon_state)));
            // DEVIATION(DV12): . += emissive_appearance(icon, "[icon_state]_o_process", src)
        }

        Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_o_screen", icon_state)));
        // DEVIATION(DV12): . += emissive_appearance(icon, "[icon_state]_o_screen", src)
        return dot;
    }

    // tg: code/modules/hydroponics/biogenerator.dm:196
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object result = await default_unfasten_wrench(user, tool);
        if (Dm.eq(result, SUCCESSFUL_UNFASTEN))
            return ITEM_INTERACT_SUCCESS;
        if (Dm.eq(result, FAILED_UNFASTEN))
            return ITEM_INTERACT_BLOCKING;
        return NONE;
    }

    // tg: code/modules/hydroponics/biogenerator.dm:204
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        object dot = default_deconstruction_screwdriver(user, tool);
        if (Dm.truthy(processing))
            stop_process(FALSE);

        if (beaker != null)
        {
            await beaker.forceMove(drop_location() as atom);
            beaker = null;
        }

        return dot;
    }

    // tg: code/modules/hydroponics/biogenerator.dm:215
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await default_deconstruction_crowbar(user, tool);
        if (!Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_SUCCESS)))
            return dot;
        turf drop_location = this.drop_location() as turf;
        if (biomass > 0)
        {
            drop_location.visible_message(span_warning(I18n.T("Biomass spills from \\the []'s biomass tank!", this)));
            playsound(drop_location, Dm.resource("sound/effects/slosh.ogg"), 25, vary: TRUE);
            Dm.@new<obj_effect_decal_cleanable_greenglow>(drop_location);
        }
        return dot;
    }

    // tg: code/modules/hydroponics/biogenerator.dm:225
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;

        if (tool is obj_item_reagent_containers_cup cup)
        {
            if (Dm.truthy(panel_open))
            {
                to_chat(user, span_warning(I18n.T("Close the maintenance panel first!")));
                return ITEM_INTERACT_BLOCKING;
            }

            await insert_beaker(user, cup);
            return ITEM_INTERACT_SUCCESS;
        }

        float content_count = Dm.num(get_content_count());
        if (tool is obj_item_storage_bag bag)
        {
            if (content_count >= max_items)
            {
                to_chat(user, span_warning(I18n.T("\\The [] is already full! Activate it to free up some space.", this)));
                return ITEM_INTERACT_FAILURE;
            }

            foreach (obj_item_food item in Dm.each<obj_item_food>(bag.contents))
            {
                if (content_count >= max_items)
                    break;
                bag.atom_storage.attempt_remove(item, this);
            }

            content_count = Dm.num(get_content_count()); // Refresh the cache for UI
            if (bag.contents.len == 0)
                to_chat(user, span_info(I18n.T("You empty \\the [] into \\the [].", bag, this)));
            else if (content_count >= max_items)
                to_chat(user, span_info(I18n.T("You fill \\the [] from \\the [] to its capacity.", this, bag)));
            else
                to_chat(user, span_info(I18n.T("You fill \\the [] from \\the [].", this, bag)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_food)
        {
            if (content_count >= max_items)
            {
                to_chat(user, span_warning(I18n.T("\\The [] is already full! Activate it to free up some space.", this)));
                return ITEM_INTERACT_FAILURE;
            }

            if (Dm.truthy(await user.transferItemToLoc(tool, this)))
            {
                to_chat(user, span_info(I18n.T("You insert \\the [] in \\the []", tool, this)));
                get_content_count(); // Refresh the cache for UI
            }
            return ITEM_INTERACT_SUCCESS;
        }

        to_chat(user, span_warning(I18n.T("You cannot put \\the [] in \\the []!", tool, this)));
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/hydroponics/biogenerator.dm:271
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await eject_beaker(user as mob_living);
        return CLICK_ACTION_SUCCESS;
    }

    /// Activates biomass processing and converts all inserted food products into biomass
    // tg: code/modules/hydroponics/biogenerator.dm:276
    public virtual async DmTask start_process()
    {
        if (!Dm.eq(machine_stat, NONE) || Dm.truthy(panel_open))
            return;

        if (Dm.truthy(processing))
        {
            await say("Already working!");
            return;
        }

        if (Dm.locate<obj_item_food>(contents) == null)
        {
            await say("No food items found!");
            return;
        }

        begin_processing();
        processing = TRUE;
        soundloop.start();
        update_appearance();
    }

    // tg: code/modules/hydroponics/biogenerator.dm:293
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        if (!Dm.truthy(processing))
            return null;

        if (!Dm.eq(machine_stat, NONE) || Dm.truthy(panel_open))
        {
            stop_process();
            return null;
        }

        if (!Dm.truthy(get_content_count()))
        {
            stop_process();
            return null;
        }

        for (float i = 1; i <= processed_items_per_cycle; i++)
        {
            obj_item_food food_to_convert = Dm.locate<obj_item_food>(contents);

            if (food_to_convert == null)
                break;

            if (Dm.truthy(Dm.bitand(Dm.num(food_to_convert.flags_1), HOLOGRAM_1)))
                qdel(food_to_convert);
            else
                convert_to_biomass(food_to_convert);
        }

        await use_energy(Dm.num(active_power_usage) * Dm.num(seconds_per_tick));

        if (!Dm.truthy(get_content_count()))
            stop_process(FALSE);

        update_appearance();
        return null;
    }

    // tg: code/modules/hydroponics/biogenerator.dm:323
    public virtual object get_content_count()
    {
        content_count_cache = 0;
        foreach (obj_item_food food in Dm.each<obj_item_food>(contents))
            content_count_cache += 1;
        return content_count_cache;
    }

    /**
     * Simple helper proc that converts the given food item into biomass for the generator, while also handling removing it
     *
     * Arguments:
     * * food_to_convert - The food item that will be converted into biomass and
     * subsequently be deleted.
     */
    // tg: code/modules/hydroponics/biogenerator.dm:336
    public virtual void convert_to_biomass(obj_item_food food_to_convert = null)
    {
        // an inert food has no reagent holder (it never ran its Initialize): it gives no nutriment
        float nutriments = ROUND_UP(Dm.num((food_to_convert.reagents as datum_reagents)?.get_reagent_amount(typeof(datum_reagent_consumable_nutriment), type_check: REAGENT_PARENT_TYPE)));
        biomass += nutriments * productivity;
        qdel(food_to_convert);
    }

    /**
     * Simple helper to handle stopping the process of the biogenerator.
     *
     * Arguments:
     * * update_appearance - Whether or not we call `update_appearance()` here.
     * Defaults to `TRUE`.
     */
    // tg: code/modules/hydroponics/biogenerator.dm:348
    public virtual void stop_process(float? update_appearance = null)
    {
        update_appearance ??= TRUE;
        end_processing();
        processing = FALSE;
        soundloop.stop();

        if (Dm.truthy(update_appearance))
            this.update_appearance();
    }

    // tg: code/modules/hydroponics/biogenerator.dm:357
    public virtual async DmTask<object> use_biomass(DmList materials = null, float? amount = null, float? remove_biomass = null)
    {
        amount ??= 1;
        remove_biomass ??= TRUE;
        datum_material biomass_material = await SSmaterials.get_material(typeof(datum_material_biomass)) as datum_material;
        if (materials.len != 1 || materials[1] != biomass_material)
            return FALSE;

        float cost = Dm.num(materials[biomass_material]) * Dm.num(amount) / efficiency;
        if (cost > biomass)
            return FALSE;

        if (Dm.truthy(remove_biomass))
            biomass -= cost;

        update_appearance();
        return TRUE;
    }

    // tg: code/modules/hydroponics/biogenerator.dm:373
    public virtual async DmTask<object> create_product(datum_design design = null, object amount = null)
    {
        if (design.make_reagent != null)
        {
            if (beaker == null)
                return FALSE;

            datum_reagents beaker_reagents = beaker.reagents as datum_reagents;
            if (Dm.num(beaker_reagents.maximum_volume) - Dm.num(beaker_reagents.total_volume) < Dm.num(amount))
            {
                await say("Warning: Attached container does not have enough free capacity!");
                return FALSE;
            }

            if (!Dm.truthy(await use_biomass(design.materials, Dm.num(amount))))
                return FALSE;

            await beaker_reagents.add_reagent(design.make_reagent, amount, added_purity: BIOGEN_REAGENT_PURITY);
        }

        if (design.build_path != null)
        {
            if (!Dm.truthy(await use_biomass(design.materials, Dm.num(amount))))
                return FALSE;

            atom drop_location = this.drop_location() as atom;
            // istype(design.build_path, /obj/item/stack/sheet): a typepath is never an instance, so tg always takes the else branch
            for (float i = 1; i <= Dm.num(amount); i++)
                await design.create_result(drop_location);
        }

        return TRUE;
    }

    /*
     * Insert a new beaker into the biogenerator, replacing/swapping our current beaker if there is one.
     *
     * user - the mob inserting the beaker
     * inserted_beaker - the beaker we're inserting into the biogen
     */
    // tg: code/modules/hydroponics/biogenerator.dm:407
    public virtual async DmTask insert_beaker(mob_living user = null, obj_item_reagent_containers_cup inserted_beaker = null)
    {
        if (!Dm.truthy(await can_interact(user)))
            return;

        if (!Dm.truthy(await user.transferItemToLoc(inserted_beaker, this)))
            return;

        if (beaker != null)
        {
            to_chat(user, span_notice(I18n.T("You swap out [] in [] for [].", beaker, this, inserted_beaker)));
            await eject_beaker(user, silent: TRUE);
        }
        else
        {
            to_chat(user, span_notice(I18n.T("You add [] to [].", inserted_beaker, this)));
        }

        beaker = inserted_beaker;
        update_appearance(UPDATE_ICON);
    }

    /*
     * Eject the current stored beaker either into the user's hands or onto the ground.
     *
     * user - the mob ejecting the beaker
     * silent - whether to give a message to the user that the beaker was ejected.
     */
    // tg: code/modules/hydroponics/biogenerator.dm:431
    public virtual async DmTask eject_beaker(mob_living user = null, float? silent = null)
    {
        silent ??= FALSE;
        if (beaker == null)
            return;

        if (!Dm.truthy(await can_interact(user)))
            return;

        obj_item ejected_beaker = beaker;

        if (Dm.truthy(await user.put_in_hands(beaker)))
        {
            if (!Dm.truthy(silent))
                to_chat(user, span_notice(I18n.T("You eject [] from [].", ejected_beaker, this)));
        }
        else
        {
            if (!Dm.truthy(silent))
                to_chat(user, span_notice(I18n.T("You eject [] from [] onto the ground.", ejected_beaker, this)));

            await ejected_beaker.forceMove(drop_location() as atom);
        }

        beaker = null;
        update_appearance(UPDATE_ICON);
    }

    // tg: code/modules/hydroponics/biogenerator.dm:454
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)) || Dm.truthy(panel_open))
            return UI_CLOSE;

        return await base.ui_status(user, state);
    }

    // tg: code/modules/hydroponics/biogenerator.dm:461
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/spritesheet_batched/research_designs)): the asset transport is the client's (DV44); the
        // Biogenerator window draws each design's picture from its own copy of tg's .dmi files.
        return Dm.list();
    }

    // tg: code/modules/hydroponics/biogenerator.dm:467
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Biogenerator", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/hydroponics/biogenerator.dm:474
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["beaker"] = beaker != null ? TRUE : FALSE;
        data["biomass"] = biomass;
        data["processing"] = processing;
        data["max_output"] = max_output;
        data["efficiency"] = efficiency;
        data["can_process"] = Dm.truthy(content_count_cache) ? TRUE : FALSE;

        if (beaker != null)
        {
            datum_reagents beaker_reagents = beaker.reagents as datum_reagents;
            data["beakerCurrentVolume"] = Dm.round(Dm.num(beaker_reagents.total_volume), 0.01f);
            data["beakerMaxVolume"] = beaker.volume;
            data["reagent_color"] = mix_color_from_reagents(beaker_reagents.reagent_list);
        }

        return data;
    }

    // tg: code/modules/hydroponics/biogenerator.dm:491
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        DmList data_categories = Dm.list();
        data["max_visual_biomass"] = max_visual_biomass;

        DmList categories = show_categories.Copy();
        foreach (object category in Dm.each(categories.Copy()))
            categories[category] = Dm.list();

        foreach (object design_path in Dm.each(stored_research.researched_designs))
        {
            datum_design design = SSresearch.techweb_designs[design_path] as datum_design;
            foreach (object category in Dm.each(categories))
            {
                if (Dm.@in(category, design.category))
                    (categories[category] as DmList).Add(design);
            }
        }

        datum_material biomass_material = await SSmaterials.get_material(typeof(datum_material_biomass)) as datum_material;
        foreach (object category in Dm.each(categories))
        {
            DmList category_designs = categories[category] as DmList;
            DmList cat = Dm.list(
                Dm.kv("name", category),
                Dm.kv("items", Dm.eq(category, selected_cat) ? Dm.list() : null));

            DmList items = cat["items"] as DmList;
            foreach (datum_design design in Dm.each_cast<datum_design>(category_designs))
            {
                Dm.append(ref items, Dm.list(Dm.list(
                    Dm.kv("path", design.type),
                    Dm.kv("name", design.name),
                    Dm.kv("icon", design.asset_id),
                    Dm.kv("is_reagent", design.make_reagent != null ? TRUE : FALSE),
                    Dm.kv("cost", Dm.num(design.materials[biomass_material]) / efficiency))));
            }
            cat["items"] = items;
            Dm.append(ref data_categories, Dm.list(cat));
        }
        data["categories"] = data_categories;

        return data;
    }

    // tg: code/modules/hydroponics/biogenerator.dm:524
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action as string)
        {
            case "activate":
                await start_process();
                return TRUE;

            case "eject":
                await eject_beaker(usr as mob_living);
                return TRUE;

            case "create":
            {
                object design_path = text2path(@params?["design_path"] as string);
                if (design_path == null)
                    return null;

                if (!Dm.truthy(stored_research.researched_designs[design_path]))
                    return null;

                datum_design design = SSresearch.techweb_designs[design_path] as datum_design;
                if (design == null)
                {
                    stack_trace("Invalid design ID passed into biogenerator ui_act()", "code/modules/hydroponics/biogenerator.dm", 541);
                    return null;
                }

                object amount = text2num(@params?["amount"]);
                if (!Dm.truthy(amount))
                    return null;
                datum_reagents beaker_reagents = beaker?.reagents as datum_reagents;
                amount = Dm.clamp(Dm.num(amount), 1, (design.make_reagent != null && beaker != null ? Dm.num(beaker_reagents.maximum_volume) - Dm.num(beaker_reagents.total_volume) : max_output));
                await create_product(design, amount);

                return TRUE;
            }

            case "select":
                selected_cat = @params?["category"];
                return TRUE;
        }
        return dot;
    }
}
