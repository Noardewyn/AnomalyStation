// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/rcd/RTD.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// E13 (the RCD family): the rapid tiling device (and its loaded and admin variants), its tile designs (/datum/tile_info) and the
// decals it copies (/datum/overlay_info): a click on a plating lays the selected tile with its direction and the copied decals
// after tg's delay and constructing effect, a click on a tiled floor picks that floor's design (and its decals), a right click
// tears a supported floor down to its base. The window is RapidTilingDevice (the client's
// godot/scripts/Shared/Tgui/Interfaces/RapidTilingDevice.cs). The cyborg's RTD (borg) stays inert with the cyborgs.

//RAPID TILING DEVICE

/**
 * An tool used to create, destroy, and copy & clear decals of floor tiles
 * Great for janitor but can be made only in engineering
 * Supports silo link upgrade and refill with glass, plasteel & iron
 */
// tg: code/game/objects/items/rcd/RTD.dm:13
public partial class obj_item_construction_rtd : obj_item_construction
{
    /// time taken to create tile
    // tg: code/game/objects/items/rcd/RTD.dm:4  #define CONSTRUCTION_TIME(cost)((cost * 0.15) SECONDS) (file-local, rules 11.4)
    private static float CONSTRUCTION_TIME(object cost) => (Dm.num(cost) * 0.15f) * SECONDS;

    /// time taken to destroy a tile
    // tg: code/game/objects/items/rcd/RTD.dm:6  #define DECONSTRUCTION_TIME(cost)((cost * 0.25) SECONDS)
    private static float DECONSTRUCTION_TIME(object cost) => (Dm.num(cost) * 0.25f) * SECONDS;

    // tg: code/game/objects/items/rcd/RTD.dm:128
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        DmList design = ((DmList)((DmList)GLOB.floor_designs[root_category])[design_category])[1] as DmList;
        if (!Dm.truthy(design["datum"]))
            populate_rtd_datums();
        selected_design = design["datum"] as datum_tile_info;
        return dot;
    }

    // tg: code/game/objects/items/rcd/RTD.dm:135
    public override object Destroy(float? force = null)
    {
        selected_design = null;
        QDEL_LIST(design_overlays);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/rcd/RTD.dm:140
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "RapidTilingDevice", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/rcd/RTD.dm:147
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/spritesheet_batched/rtd)): the asset transport is the client's (DV44); the client draws
        // the "rtd" sheet's pictures from its own copy of tg's .dmi files (TguiSpritesheets).
        return Dm.list();
    }

    // tg: code/game/objects/items/rcd/RTD.dm:152
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        await ui_interact(user);
        return dot;
    }

    // tg: code/game/objects/items/rcd/RTD.dm:156
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = await base.ui_static_data(user) as DmList;

        data["root_categories"] = Dm.list();
        foreach (object category in Dm.each(GLOB.floor_designs))
            Dm.append_at(data, "root_categories", category);
        data["selected_root"] = root_category;

        data["categories"] = Dm.list();
        foreach (object sub_category in Dm.each(GLOB.floor_designs[root_category]))
        {
            DmList target_category = ((DmList)GLOB.floor_designs[root_category])[sub_category] as DmList;

            DmList designs = Dm.list(); //initialize all designs under this category
            foreach (DmList design in Dm.each_cast<DmList>(target_category))
            {
                datum_tile_info tile_design = design["datum"] as datum_tile_info;
                if (!istype(tile_design, typeof(datum_tile_info)))
                {
                    populate_rtd_datums();
                    tile_design = design["datum"] as datum_tile_info;
                }
                Dm.append(ref designs, Dm.list(Dm.list(Dm.kv("name", tile_design.name), Dm.kv("icon", tile_design.icon_css_class))));
            }

            Dm.append_at(data, "categories", Dm.list(Dm.list(Dm.kv("category_name", sub_category), Dm.kv("recipes", designs))));
        }

        return data;
    }

    // tg: code/game/objects/items/rcd/RTD.dm:180
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = await base.ui_data(user) as DmList;

        data["selected_category"] = design_category;
        selected_design.fill_ui_data(data, selected_direction);

        return data;
    }

    // tg: code/game/objects/items/rcd/RTD.dm:188
    public override async DmTask<object> handle_ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        playsound(this, SFX_TOOL_SWITCH, 20, TRUE);

        DmList floor_designs = GLOB.floor_designs;
        switch (action)
        {
            case "root_category":
            {
                object new_root = @params["root_category"];
                if (floor_designs[new_root] != null) //is a valid category
                {
                    root_category = Dm.str(new_root);
                    await update_static_data_for_all_viewers();
                }
                break;
            }

            case "set_dir":
            {
                object direction = text2dir(@params["dir"]);
                if (!Dm.truthy(direction))
                    return FALSE;
                selected_direction = Dm.num(direction);
                break;
            }

            case "recipe":
            {
                DmList main_root = floor_designs[root_category] as DmList;
                if (main_root == null)
                    return FALSE;
                DmList sub_category = main_root[@params["category_name"]] as DmList;
                if (sub_category == null)
                    return FALSE;
                DmList target_design = sub_category[text2num(@params["id"])] as DmList;
                if (target_design == null)
                    return FALSE;

                QDEL_LIST(design_overlays);
                design_category = Dm.str(@params["category_name"]);
                if (!Dm.truthy(target_design["datum"]))
                    populate_rtd_datums();
                selected_design = target_design["datum"] as datum_tile_info;
                selected_direction = SOUTH;
                blueprint_changed = TRUE;
                break;
            }
        }

        return TRUE;
    }

    // tg: code/game/objects/items/rcd/RTD.dm:226
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(ranged) || !Dm.truthy(range_check(interacting_with, user)))
            return NONE;
        return await try_tiling(interacting_with, user);
    }

    // tg: code/game/objects/items/rcd/RTD.dm:231
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(Dm.num(dot), ITEM_INTERACT_ANY_BLOCKER)))
            return dot;

        return await try_tiling(interacting_with, user);
    }

    /**
     * put plating on the turf
     * Arguments
     *
     * * turf/open/floor/floor - the turf we are trying to put plating on
     * * mob/living/user - the mob trying to do the plating
     */
    // tg: code/game/objects/items/rcd/RTD.dm:245
    public virtual async DmTask<object> try_tiling(atom interacting_with = null, mob_living user = null)
    {
        if (HAS_TRAIT(interacting_with, TRAIT_COMBAT_MODE_SKIP_INTERACTION))
            return NONE;

        turf_open_floor floor = interacting_with as turf_open_floor;
        if (!istype(floor, typeof(turf_open_floor)))
            return NONE;

        DmList floor_designs = GLOB.floor_designs;
        if (!istype(floor, typeof(turf_open_floor_plating))) //we infer what floor type it is if its not the usual plating
        {
            if (Dm.truthy(ranged))
                user.Beam(floor, icon_state: "light_beam", time: 5);
            foreach (object main_root in Dm.each(floor_designs))
            {
                foreach (object sub_category in Dm.each(floor_designs[main_root]))
                {
                    foreach (DmList design_info in Dm.each<DmList>(((DmList)floor_designs[main_root])[sub_category]))
                    {
                        Type tile_type = design_info["type"] as Type;
                        if (Dm.initial(tile_type, (obj_item_stack_tile t) => t.turf_type) != floor.type)
                            continue;

                        //infer available overlays on the floor to recreate them to the best extent
                        QDEL_LIST(design_overlays);
                        object floor_overlays = floor.managed_overlays;
                        if (floor_overlays == null)
                            floor_overlays = Dm.list();
                        else if (!Dm.islist(floor_overlays))
                            floor_overlays = Dm.list(floor.managed_overlays);
                        foreach (mutable_appearance appearance in Dm.each_cast<mutable_appearance>(floor_overlays))
                            Dm.append(ref design_overlays, Dm.@new<datum_overlay_info>(appearance));

                        //store all information about this tile
                        root_category = Dm.str(main_root);
                        design_category = Dm.str(sub_category);
                        selected_design = design_info["datum"] as datum_tile_info;
                        selected_direction = Dm.num(floor.dir);
                        balloon_alert(user, I18n.T("tile changed to []", selected_design.name));

                        return ITEM_INTERACT_SUCCESS;
                    }
                }
            }

            //can't infer floor type!
            balloon_alert(user, I18n.T("design not supported!"));
            return ITEM_INTERACT_BLOCKING;
        }

        //resource sanity check before & after delay along with special effects
        if (!Dm.truthy(await useResource(selected_design.cost, user, TRUE)))
            return ITEM_INTERACT_BLOCKING;
        float delay = CONSTRUCTION_TIME(selected_design.cost);
        obj_effect_constructing_effect rcd_effect = Dm.@new<obj_effect_constructing_effect>(floor, delay, RCD_TURF);
        object beam = null;
        if (Dm.truthy(ranged))
        {
            beam = user.Beam(floor, icon_state: "light_beam", time: delay);
            playsound(loc as atom, Dm.resource("sound/effects/light_flicker.ogg"), 50, FALSE);
        }
        else
            playsound(loc as atom, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        if (!Dm.truthy(await build_delay(user, delay, target: floor)))
        {
            qdel(beam);
            qdel(rcd_effect);
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await useResource(selected_design.cost, user, TRUE)))
        {
            qdel(rcd_effect);
            return ITEM_INTERACT_BLOCKING;
        }

        //do the tilling
        //step 1 create tile
        obj_item_stack_tile final_tile = selected_design.new_tile(user.drop_location(), selected_direction) as obj_item_stack_tile;
        if (QDELETED(final_tile)) //if you were standing on a stack of tiles this newly spawned tile could get merged with it cause its spawned on your location
        {
            qdel(rcd_effect);
            balloon_alert(user, I18n.T("tile got merged with the stack beneath you!"));
            return ITEM_INTERACT_BLOCKING;
        }
        //step 2 lay tile
        turf_open new_turf = await final_tile.place_tile(floor as turf_open_floor_plating, user) as turf_open;
        if (Dm.truthy(new_turf)) //apply infered overlays
        {
            foreach (datum_overlay_info info in Dm.each<datum_overlay_info>(design_overlays))
                await info.add_decal(new_turf);
        }
        rcd_effect.end_animation();
        await useResource(selected_design.cost, user);

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/rcd/RTD.dm:325
    public override async DmTask<object> ranged_interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(ranged) || !Dm.truthy(range_check(interacting_with, user)))
            return NONE;
        return await interact_with_atom_secondary(interacting_with, user, modifiers);
    }

    // tg: code/game/objects/items/rcd/RTD.dm:330
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        turf_open_floor floor = interacting_with as turf_open_floor;
        if (!istype(floor, typeof(turf_open_floor)))
            return NONE;

        if (istype(floor, typeof(turf_open_floor_plating))) //cant deconstruct normal plating thats the RCD's job
        {
            balloon_alert(user, I18n.T("nothing to deconstruct!"));
            return ITEM_INTERACT_BLOCKING;
        }

        DmList floor_designs = GLOB.floor_designs;

        //we only deconstruct floors which are supported by the RTD
        object cost = 0;
        foreach (object main_root in Dm.each(floor_designs))
        {
            if (Dm.truthy(cost))
                break;
            foreach (object sub_category in Dm.each(floor_designs[main_root]))
            {
                if (Dm.truthy(cost))
                    break;
                foreach (DmList design_info in Dm.each<DmList>(((DmList)floor_designs[main_root])[sub_category]))
                {
                    Type tile_type = design_info["type"] as Type;
                    if (Dm.initial(tile_type, (obj_item_stack_tile t) => t.turf_type) == floor.type)
                    {
                        cost = design_info["tile_cost"];
                        break;
                    }
                }
            }
        }
        if (!Dm.truthy(cost))
        {
            balloon_alert(user, I18n.T("can't deconstruct this type!"));
            return ITEM_INTERACT_BLOCKING;
        }

        //resource sanity check before & after delay along with beam effects
        if (!Dm.truthy(await useResource(Dm.num(cost) * 0.7f, user, TRUE))) //no ballon alert for checkResource as it already spans an alert to chat
            return ITEM_INTERACT_BLOCKING;
        float delay = DECONSTRUCTION_TIME(cost);
        obj_effect_constructing_effect rcd_effect = Dm.@new<obj_effect_constructing_effect>(floor, delay, RCD_DECONSTRUCT);
        object beam = null;
        if (Dm.truthy(ranged))
        {
            beam = user.Beam(floor, icon_state: "light_beam", time: delay);
            playsound(loc as atom, Dm.resource("sound/effects/light_flicker.ogg"), 50, FALSE);
        }
        else
            playsound(loc as atom, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        if (!Dm.truthy(await do_after(user, delay, target: floor)))
        {
            qdel(beam);
            qdel(rcd_effect);
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await useResource(Dm.num(cost) * 0.7f, user, TRUE)))
        {
            qdel(rcd_effect);
            return ITEM_INTERACT_BLOCKING;
        }

        //begin deconstruction
        //find & collect all decals
        DmList all_decals = Dm.list();
        foreach (obj_effect decal in Dm.each<obj_effect>(floor.contents))
            Dm.append(ref all_decals, decal);
        //delete all decals
        foreach (obj_effect decal in Dm.each<obj_effect>(all_decals))
        {
            DmList floor_contents = floor.contents;
            Dm.remove(ref floor_contents, decal);
            qdel(decal);
        }
        if (Dm.eq(floor.baseturf_at_depth(1), typeof(turf_baseturf_bottom))) //for turfs whose base is open space we put regular plating in its place else everyone dies
            await floor.ChangeTurf(typeof(turf_open_floor_plating), flags: CHANGETURF_INHERIT_AIR);
        else //for every other turf we scrape away exposing base turf underneath
            await floor.ScrapeAway(flags: CHANGETURF_INHERIT_AIR);
        rcd_effect.end_animation();
        await useResource(Dm.num(cost) * 0.7f, user);

        return ITEM_INTERACT_SUCCESS;
    }
}

/// stores the name, type, icon & cost for each tile type
// tg: code/game/objects/items/rcd/RTD.dm:46
public partial class datum_tile_info : datum
{
    /// decompress a single tile design list element from GLOB.floor_designs into its individual variables
    // tg: code/game/objects/items/rcd/RTD.dm:68
    public override async DmTask New(params object[] __extra)
    {
        DmList design = Dm.arg(__extra, 0) as DmList;
        name = design["name"];
        tile_type = design["type"] as Type;
        turf_type = Dm.initial(tile_type, (obj_item_stack_tile t) => t.turf_type);
        icon_file = Dm.initial(turf_type, (atom a) => a.icon);
        icon_state = Dm.initial(turf_type, (atom a) => a.icon_state);
        icon_css_class = sanitize_css_class_name(Dm.text("[]-[]", icon_file, icon_state));
        obj_item_stack_tile tile_obj = Dm.@new(tile_type) as obj_item_stack_tile; // lists stored on types compile to be inside New()
        tile_directions_text = assoc_to_keys(tile_obj.tile_rotate_dirs) as DmList;
        tile_directions_numbers = tile_obj.tile_rotate_dirs_number;
        qdel(tile_obj);
        cost = design["tile_cost"];
    }

    /// fill all information to be sent to the UI
    // tg: code/game/objects/items/rcd/RTD.dm:82
    public virtual void fill_ui_data(DmList data = null, object selected_direction = null)
    {
        data["selected_recipe"] = name;
        data["selected_icon"] = icon_css_class;

        if (!Dm.truthy(tile_directions_text))
        {
            data["selected_direction"] = null;
            return;
        }

        data["tile_dirs"] = tile_directions_text;
        data["selected_direction"] = dir2text(selected_direction);
    }

    ///convinience proc to quickly convert the tile design into an physical tile to lay on the plating
    // tg: code/game/objects/items/rcd/RTD.dm:94
    public virtual object new_tile(object loc = null, object selected_direction = null)
    {
        obj_item_stack_tile final_tile = Dm.@new(tile_type, loc, 1) as obj_item_stack_tile;
        final_tile.turf_dir = Dm.num(selected_direction);
        return final_tile;
    }
}

/**
 * Stores the decal & overlays on the floor to preserve texture of the design
 * in short it's just an wrapper for mutable appearance where we retrieve the nessassary information
 * to recreate an mutable appearance
 */
// tg: code/game/objects/items/rcd/RTD.dm:104
public partial class datum_overlay_info : datum
{
    /// icon var of the mutable appearance
    // tg: code/game/objects/items/rcd/RTD.dm:106
    // The generated field would be a builtin icon (tg declares var/icon/icon), which the appearance's icon file does not fit: the
    // hand-written member holds it.
    public object icon;

    //decompressing nessasary information required to re-create an mutable appearance
    // tg: code/game/objects/items/rcd/RTD.dm:117
    public override async DmTask New(params object[] __extra)
    {
        mutable_appearance appearance = Dm.arg(__extra, 0) as mutable_appearance;
        icon = appearance.icon;
        icon_state = appearance.icon_state;
        alpha = appearance.alpha;
        direction = appearance.dir;
        color = appearance.color;
    }

    /// re create the appearance
    // tg: code/game/objects/items/rcd/RTD.dm:125
    public virtual async DmTask add_decal(turf the_turf = null)
    {
        await the_turf.AddElement(typeof(datum_element_decal), icon, icon_state, direction, null, null, alpha, color, null, FALSE, null);
    }
}

// Silicons SI2 (D326): the cyborg's RTD, which runs on its cell.
// tg: code/game/objects/items/rcd/RTD.dm:398
public partial class obj_item_construction_rtd_borg : obj_item_construction_rtd
{
    ///Converting tile cost into joules
    // tg: code/game/objects/items/rcd/RTD.dm:396  #define RTD_BORG_ENERGY_FACTOR (0.03 * STANDARD_CELL_CHARGE)
    private const float RTD_BORG_ENERGY_FACTOR = 0.03f * STANDARD_CELL_CHARGE;

    ///Cannot deconstruct floors
    // tg: code/game/objects/items/rcd/RTD.dm:402
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return NONE;
    }

    // tg: code/game/objects/items/rcd/RTD.dm:405
    public override object get_matter(mob user = null)
    {
        if (!iscyborg(user))
            return 0f;
        mob_living_silicon_robot borgy = (mob_living_silicon_robot)user;
        if (borgy.cell__obj == null)
            return 0f;
        max_matter = Dm.num(borgy.cell__obj.maxcharge);
        return borgy.cell__obj.charge;
    }

    // tg: code/game/objects/items/rcd/RTD.dm:414
    public override async DmTask<object> useResource(object amount = null, mob user = null, float? dry_run = null)
    {
        mob_living_silicon_robot borgy = user as mob_living_silicon_robot;
        if (!iscyborg(borgy))
            return FALSE;
        if (borgy.cell__obj == null)
        {
            balloon_alert(user, I18n.T("no cell found!"));
            return FALSE;
        }
        if (Dm.num(borgy.cell__obj.charge) < (Dm.num(amount) * RTD_BORG_ENERGY_FACTOR))
        {
            balloon_alert(user, I18n.T("insufficient charge!"));
            return FALSE;
        }
        if (!Dm.truthy(dry_run))
        {
            playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            return await borgy.cell__obj.use(Dm.num(amount) * RTD_BORG_ENERGY_FACTOR);
        }
        return TRUE;
    }
}
