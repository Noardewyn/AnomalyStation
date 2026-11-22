// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/rcd/RCD.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// E13 (the RCD family): the rapid construction device and its station subtypes (loaded, upgraded, the CE's professional RCD,
// the industrial, admin and advanced ranged ones), the RCD hologram and the matter cartridges. A left click builds the selected
// design on its target (rcd_vals gives the delay and the cost, the constructing effect plays, rcd_act builds), a right click
// deconstructs; the window is RapidConstructionDevice with the integrated airlock electronics' access settings (the client's
// godot/scripts/Shared/Tgui/Interfaces/RapidConstructionDevice.cs). The cyborg's RCDs (borg, borg/syndicate) came with the
// cyborgs; the exosuit's with gaps DV2 (D333, below); the malfunctioning AI's detonate_pulse is translated, unreached.
// E15 translated the examine (the owner's rule of 2026-10-29); the tool log lines are deferred leaves (the tool log is not declared).

//RAPID CONSTRUCTION DEVICE

// tg: code/game/objects/items/rcd/RCD.dm:6
public partial class obj_item_construction_rcd : obj_item_construction
{
    /// Multiplier applied on construction & deconstruction time when building multiple structures
    // tg: code/game/objects/items/rcd/RCD.dm:2  #define FREQUENT_USE_DEBUFF_MULTIPLIER 3 (file-local, rules 11.4)
    private const float FREQUENT_USE_DEBUFF_MULTIPLIER = 3;

    /// The path of the structure the rcd is currently creating
    // tg: code/game/objects/items/rcd/RCD.dm:38
    // The generated field would be an atom_movable (tg declares var/atom/movable/rcd_design_path for initial()), which a
    // typepath does not fit: the hand-written member holds the path, and the constructor with tg's overrides (RCD.dm:7-25) is
    // here.
    public Type rcd_design_path;

    // tg: code/game/objects/items/rcd/RCD.dm:6
    public obj_item_construction_rcd()
    {
        name = "rapid-construction-device (RCD)";
        icon = Dm.resource("icons/obj/tools.dmi");
        icon_state = "rcd";
        inhand_icon_state = "rcd";
        worn_icon_state = "RCD";
        inside_belt_icon_state = "rcd";
        lefthand_file = Dm.resource("icons/mob/inhands/equipment/tools_lefthand.dmi");
        righthand_file = Dm.resource("icons/mob/inhands/equipment/tools_righthand.dmi");
        custom_premium_price = PAYCHECK_COMMAND * 2;
        max_matter = 160;
        slot_flags = ITEM_SLOT_BELT;
        item_flags = Dm.bitor(NO_MAT_REDEMPTION, NOBLUDGEON);
        has_ammobar = TRUE;
        action_slots = ALL;
        drop_sound = Dm.resource("sound/items/handling/tools/rcd_drop.ogg");
        pickup_sound = Dm.resource("sound/items/handling/tools/rcd_pickup.ogg");
        sound_vary = TRUE;
    }

    // tg: code/game/objects/items/rcd/RCD.dm:20  (a hand-written var block has its own dm_init_vars, types-converter.md 3.4)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        actions_types = Dm.list(typeof(datum_action_item_action_rcd_scan));
        custom_materials = Dm.list(Dm.kv(typeof(datum_material_iron), SHEET_MATERIAL_AMOUNT * 6), Dm.kv(typeof(datum_material_glass), SHEET_MATERIAL_AMOUNT * 3));
    }

    // tg: code/game/objects/items/rcd/RCD.dm:200  can_place's var/static/list/small_structures (rules 10.6)
    private static ref DmList can_place__small_structures => ref TgState.Current.obj_item_construction_rcd__can_place__small_structures;

    // tg: code/game/objects/items/rcd/RCD.dm:68
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        airlock_electronics = Dm.@new<obj_item_electronics_airlock_rcd>(this);
        airlock_electronics.name = "Access Control";
        airlock_electronics.holder = this;

        root_category = GLOB.rcd_designs[1];
        design_category = ((DmList)GLOB.rcd_designs[root_category])[1];
        DmList design = ((DmList)((DmList)GLOB.rcd_designs[root_category])[design_category])[1] as DmList;

        rcd_design_path = design[RCD_DESIGN_PATH] as Type;
        design_title = Dm.initial(rcd_design_path, (atom_movable a) => a.name);
        mode = design[RCD_DESIGN_MODE];
        construction_mode = mode;

        Dm.append(ref GLOB.rcd_list, this);
        Waiting.NoSleep(AddElement(typeof(datum_element_openspace_item_click_handler)));
        return dot;
    }

    // tg: code/game/objects/items/rcd/RCD.dm:86  (E15)
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(construction_upgrades))
        {
            Dm.append(ref dot, I18n.T("It has the following upgrades installed:"));
            if (Dm.truthy(Dm.bitand(construction_upgrades, RCD_UPGRADE_FRAMES)))
                Dm.append(ref dot, Dm.initial(typeof(obj_item_rcd_upgrade_frames), (atom a) => a.name));
            if (Dm.truthy(Dm.bitand(construction_upgrades, RCD_UPGRADE_SIMPLE_CIRCUITS)))
                Dm.append(ref dot, Dm.initial(typeof(obj_item_rcd_upgrade_simple_circuits), (atom a) => a.name));
            if (Dm.truthy(Dm.bitand(construction_upgrades, RCD_UPGRADE_SILO_LINK)))
                Dm.append(ref dot, Dm.initial(typeof(obj_item_rcd_upgrade_silo_link), (atom a) => a.name));
            if (Dm.truthy(Dm.bitand(construction_upgrades, RCD_UPGRADE_FURNISHING)))
                Dm.append(ref dot, Dm.initial(typeof(obj_item_rcd_upgrade_furnishing), (atom a) => a.name));
            if (Dm.truthy(Dm.bitand(construction_upgrades, RCD_UPGRADE_ANTI_INTERRUPT)))
                Dm.append(ref dot, Dm.initial(typeof(obj_item_rcd_upgrade_anti_interrupt), (atom a) => a.name));
            if (Dm.truthy(Dm.bitand(construction_upgrades, RCD_UPGRADE_NO_FREQUENT_USE_COOLDOWN)))
                Dm.append(ref dot, Dm.initial(typeof(obj_item_rcd_upgrade_cooling), (atom a) => a.name));
        }
        return dot;
    }

    // tg: code/game/objects/items/rcd/RCD.dm:103
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref airlock_electronics);
        Dm.remove(ref GLOB.rcd_list, this);
        object dot = base.Destroy(force);
        return dot;
    }

    // tg: code/game/objects/items/rcd/RCD.dm:108
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        // COOLDOWN_FINISHED(src, destructive_scan_cooldown) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (!(destructive_scan_cooldown <= world.time))
        {
            to_chat(user, span_warning(I18n.T("[] lets out a low buzz.", this)));
            return null;
        }

        // COOLDOWN_START(src, destructive_scan_cooldown, RCD_DESTRUCTIVE_SCAN_COOLDOWN) (__DEFINES/cooldowns.dm:110), inline
        destructive_scan_cooldown = world.time + RCD_DESTRUCTIVE_SCAN_COOLDOWN;
        rcd_scan(this);
        return null;
    }

    // tg: code/game/objects/items/rcd/RCD.dm:116
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        turf T = get_turf(user);

        if (!isopenturf(T)) // Oh fuck
        {
            user.visible_message(span_suicide(I18n.T("[] is beating []self to death with []! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
            return BRUTELOSS;
        }

        mode = RCD_TURF;
        user.visible_message(span_suicide(I18n.T("[] sets the RCD to 'Wall' and points it down [] throat! It looks like [] trying to commit suicide!", user, user.p_their(), user.p_theyre())));
        if (Dm.truthy(await useResource(16, user))) // It takes 16 resources to construct a wall
        {
            object success = await T.rcd_act(user, this, Dm.list(Dm.kv(Dm.text("[]", RCD_DESIGN_MODE), RCD_TURF), Dm.kv(Dm.text("[]", RCD_DESIGN_PATH), typeof(turf_open_floor_plating_rcd))));
            T = get_turf(user);
            // If the RCD placed a floor instead of a wall, having a wall without plating under it is cursed
            // There isn't an easy programmatical way to check if rcd_act will place a floor or a wall, so just repeat using it for free
            if (Dm.truthy(success) && isopenturf(T))
                await T.rcd_act(user, this, Dm.list(Dm.kv(Dm.text("[]", RCD_DESIGN_MODE), RCD_TURF), Dm.kv(Dm.text("[]", RCD_DESIGN_PATH), typeof(turf_open_floor_plating_rcd))));
            playsound(get_turf(user), SFX_TOOL_SWITCH, 20, TRUE);
            await user.gib(DROP_ALL_REMAINS);
            return MANUAL_SUICIDE;
        }

        user.visible_message(span_suicide(I18n.T("[] pulls the trigger... But there is not enough ammo!", user)));
        return SHAME;
    }

    /**
     * checks if we can build the structure
     * Arguments
     *
     * * [atom][target]- the target we are trying to build on/deconstruct e.g. turf, wall etc
     * * rcd_results- list of params specifically the build type of our structure
     * * [mob][user]- the user
     */
    // tg: code/game/objects/items/rcd/RCD.dm:147
    public virtual async DmTask<object> can_place(atom target = null, DmList rcd_results = null, mob user = null)
    {
        object rcd_mode = rcd_results[RCD_DESIGN_MODE];
        Type rcd_structure = rcd_results[RCD_DESIGN_PATH] as Type;
        /**
         *For anything that does not go an a wall we have to make sure that turf is clear for us to put the structure on it
         *If we are just trying to destroy something then this check is not necessary
         *RCD_WALLFRAME is also returned as the rcd_mode when upgrading apc, airalarm, firealarm using simple circuits upgrade
         */
        if (!Dm.eq(rcd_mode, RCD_WALLFRAME) && !Dm.eq(rcd_mode, RCD_DECONSTRUCT))
        {
            turf target_turf = get_turf(target);
            //if we are trying to build a window we check for specific edge cases
            if (Dm.eq(rcd_mode, RCD_WINDOWGRILLE))
            {
                float is_full_tile = Dm.initial(rcd_structure, (obj_structure_window w) => w.fulltile);

                DmList structures_to_ignore;
                if (istype(target, typeof(obj_structure_grille)))
                {
                    if (Dm.truthy(is_full_tile)) //if we are trying to build full-tile windows we ignore the grille
                        structures_to_ignore = Dm.list(typeof(obj_structure_grille));
                    else //when building directional windows we ignore the grill and other directional windows
                        structures_to_ignore = Dm.list(typeof(obj_structure_grille), typeof(obj_structure_window));
                }
                else //for directional windows we ignore other directional windows as they can be in different directions on the turf.
                    structures_to_ignore = Dm.list(typeof(obj_structure_window));

                //check if we can build our window on the grill
                if (Dm.truthy(await target_turf.is_blocked_turf(exclude_mobs: Dm.not(is_full_tile), source_atom: null, ignore_atoms: structures_to_ignore, type_list: TRUE)))
                {
                    playsound(user, SFX_TOOL_SWITCH, 20, TRUE);
                    balloon_alert(user, I18n.T("tile is blocked!"));
                    return FALSE;
                }
            }

            /**
             * if we are trying to create plating on turf which is not a proper floor then don't check for objects on top of the turf just allow that turf to be converted into plating. e.g. create plating beneath a player or underneath a machine frame/any dense object
             * if we are trying to finish a wall girder then let it finish then make sure no one/nothing is stuck in the girder
             */
            else if (Dm.eq(rcd_mode, RCD_TURF) && rcd_structure == typeof(turf_open_floor_plating_rcd))
            {
                //if a player builds a wallgirder on top of himself manually with iron sheets he can't finish the wall if he is still on the girder. Exclude the girder itself when checking for other dense objects on the turf
                DmList ignore_types = Dm.list();
                if (isturf(target))
                {
                    if (isfloorturf(target))
                        Dm.append(ref ignore_types, typeof(obj_structure_girder));
                    else
                        ignore_types = null;
                }
                else if (istype(target, typeof(obj_structure_girder)))
                    Dm.append(ref ignore_types, typeof(obj_structure_girder));

                if (Dm.truthy(ignore_types) && Dm.truthy(await target_turf.is_blocked_turf(exclude_mobs: FALSE, source_atom: null, ignore_atoms: ignore_types, type_list: TRUE)))
                {
                    playsound(get_turf(user), SFX_TOOL_SWITCH, 20, TRUE);
                    balloon_alert(user, I18n.T("something is on the girder!"));
                    return FALSE;
                }
            }

            //check if turf is blocked in for dense structures
            else
            {
                //edge cases for what we can/can't ignore
                float ignore_mobs = FALSE;
                DmList ignored_types = null;
                if (Dm.eq(rcd_mode, RCD_WINDOWGRILLE) || Dm.truthy(is_path_in_list(rcd_structure, can_place__small_structures)))
                {
                    ignored_types = Dm.list(typeof(obj_structure_window));
                    //if we are trying to create grills/windoors we can go ahead and further ignore other windoors on the turf
                    if (Dm.eq(rcd_mode, RCD_WINDOWGRILLE) || (Dm.eq(rcd_mode, RCD_AIRLOCK) && Dm.ispath(rcd_structure, typeof(obj_machinery_door_window))))
                    {
                        //only ignore mobs if we are trying to create windoors and not grills. We don't want to drop a grill on top of somebody
                        ignore_mobs = Dm.eq(rcd_mode, RCD_AIRLOCK) ? TRUE : FALSE;
                        Dm.append(ref ignored_types, typeof(obj_machinery_door_window));
                    }
                    //if we are trying to create full airlock doors then we do the regular checks and make sure we have the full space for them. i.e. don't ignore anything dense on the turf
                    else if (Dm.eq(rcd_mode, RCD_AIRLOCK))
                        ignored_types = Dm.list();
                }

                //check if the structure can fit on this turf
                if (Dm.truthy(await target_turf.is_blocked_turf(exclude_mobs: ignore_mobs, source_atom: null, ignore_atoms: ignored_types, type_list: TRUE)))
                {
                    playsound(get_turf(user), SFX_TOOL_SWITCH, 20, TRUE);
                    balloon_alert(user, I18n.T("something is on the tile!"));
                    return FALSE;
                }
            }
        }

        return TRUE;
    }

    // tg: code/game/objects/items/rcd/RCD.dm:234
    public override async DmTask<object> build_delay(mob user = null, object delay = null, atom target = null)
    {
        //unaffected by ui changes
        if (Dm.eq(mode, RCD_DECONSTRUCT))
        {
            if (Dm.num(delay) <= 0)
                return TRUE;

            return await do_after(user, delay, target);
        }

        //checks for ui changes
        return await base.build_delay(user, delay, target);
    }

    /**
     * actual proc to create the structure
     *
     * Arguments
     * * [atom][target]- the target we are trying to build on/deconstruct e.g. turf, wall etc
     * * [mob][user]- the user building this structure
     */
    // tg: code/game/objects/items/rcd/RCD.dm:252
    public virtual async DmTask<object> rcd_create(atom target = null, mob user = null)
    {
        if (isopenturf(target))
        {
            turf_open open = target as turf_open;
            if (!Dm.truthy(open.CanBuildHere()))
                return NONE;
        }

        DmList rcd_results = target.rcd_vals(user, this) as DmList; // does this atom allow for rcd actions?
        if (!Dm.truthy(rcd_results)) // nope
            return NONE;

        //straight up can't touch this
        if (Dm.eq(mode, RCD_DECONSTRUCT) && Dm.truthy(Dm.bitand(target.resistance_flags, INDESTRUCTIBLE)))
        {
            balloon_alert(user, I18n.T("too durable!"));
            return ITEM_INTERACT_BLOCKING;
        }

        rcd_results[RCD_DESIGN_MODE] = mode;
        rcd_results[RCD_DESIGN_PATH] = rcd_design_path;

        float delay = Dm.num(rcd_results["delay"]) * delay_mod;
        if (
            !Dm.truthy(Dm.bitand(construction_upgrades, RCD_UPGRADE_NO_FREQUENT_USE_COOLDOWN))
                && !Dm.truthy(rcd_results[RCD_RESULT_BYPASS_FREQUENT_USE_COOLDOWN])
                && current_active_effects > 0
        )
            delay *= FREQUENT_USE_DEBUFF_MULTIPLIER;

        current_active_effects += 1;

        object target_name = target.name; //Store this information before it gets mutated by the rcd.
        Type target_path = target.type;
        var design_path = rcd_results[RCD_DESIGN_PATH] as Type;
        string location = AREACOORD(target);
        if (Dm.truthy(await _rcd_create_effect(target, user, delay, rcd_results)))
            log_tool(Dm.text("[] used [] to [] at []", key_name(user), this, !Dm.eq(rcd_results[RCD_DESIGN_MODE], RCD_DECONSTRUCT) ? Dm.text("construct []([])", Dm.initial(design_path, (atom p) => p.name), design_path) : Dm.text("deconstruct []([])", target_name, target_path), location));

        current_active_effects -= 1;
        return ITEM_INTERACT_SUCCESS;
    }

    /**
     * Internal proc which creates the rcd effects & creates the structure
     *
     * Arguments
     * * [atom][target]- the target we are trying to build on/deconstruct e.g. turf, wall etc
     * * [mob][user]- the user trying to build the structure
     * * delay- the delay with the disk upgrades applied
     * * rcd_results- list of params which contains the cost & build mode to create the structure
     */
    // tg: code/game/objects/items/rcd/RCD.dm:299
    public virtual async DmTask<object> _rcd_create_effect(atom target = null, mob user = null, object delay = null, DmList rcd_results = null)
    {
        obj_effect_constructing_effect rcd_effect = Dm.@new<obj_effect_constructing_effect>(get_turf(target), delay, rcd_results[RCD_DESIGN_MODE], construction_upgrades);

        //resource & structure placement sanity checks before & after delay along with beam effects
        if (!Dm.truthy(await useResource(rcd_results["cost"], user, TRUE)) || !Dm.truthy(await can_place(target, rcd_results, user)))
        {
            qdel(rcd_effect);
            return FALSE;
        }
        object beam = null;
        if (Dm.truthy(ranged))
        {
            atom beam_source = Dm.truthy(owner) ? owner : user;
            beam = beam_source.Beam(target, icon_state: "rped_upgrade", time: Dm.num(delay));
        }
        if (!Dm.truthy(await build_delay(user, delay, target: target))) // no need for do_after with no delay
        {
            qdel(rcd_effect);
            if (beam != null)
                qdel(beam);
            return FALSE;
        }
        if (QDELETED(rcd_effect))
            return FALSE;
        if (!Dm.truthy(await useResource(rcd_results["cost"], user, TRUE)) || !Dm.truthy(await can_place(target, rcd_results, user)))
        {
            qdel(rcd_effect);
            return FALSE;
        }

        if (!Dm.truthy(await target.rcd_act(user, this, rcd_results)))
        {
            qdel(rcd_effect);
            return FALSE;
        }
        await useResource(rcd_results["cost"], user);
        playsound(loc as atom, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        rcd_effect.end_animation();
        return TRUE;
    }

    // tg: code/game/objects/items/rcd/RCD.dm:331
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/spritesheet_batched/rcd)): the asset transport is the client's (DV44); the client draws
        // the "rcd-tgui" sheet's pictures from its own copy of tg's .dmi files (TguiSpritesheets).
        return Dm.list();
    }

    // tg: code/game/objects/items/rcd/RCD.dm:336
    public override object ui_host(mob user = null)
    {
        return Dm.truthy(owner) ? owner : base.ui_host(user);
    }

    // tg: code/game/objects/items/rcd/RCD.dm:339
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "RapidConstructionDevice", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/rcd/RCD.dm:345
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = await base.ui_static_data(user) as DmList;

        DmList electronics_data = await airlock_electronics.ui_static_data(user) as DmList;
        foreach (object key in Dm.each(electronics_data))
            data[key] = electronics_data[key];

        data["root_categories"] = Dm.list();
        foreach (object category in Dm.each(GLOB.rcd_designs))
            Dm.append_at(data, "root_categories", category);
        data["selected_root"] = root_category;

        data["categories"] = Dm.list();
        foreach (object sub_category in Dm.each(GLOB.rcd_designs[root_category]))
        {
            DmList target_category = ((DmList)GLOB.rcd_designs[root_category])[sub_category] as DmList;
            if (!Dm.truthy(Dm.length(target_category)))
                continue;

            //skip category if upgrades were not installed for these
            if (Dm.eq(sub_category, "Machines") && !Dm.truthy(Dm.bitand(construction_upgrades, RCD_UPGRADE_FRAMES)))
                continue;
            if (Dm.eq(sub_category, "Furniture") && !Dm.truthy(Dm.bitand(construction_upgrades, RCD_UPGRADE_FURNISHING)))
                continue;

            DmList designs = Dm.list(); //initialize all designs under this category
            foreach (DmList design in Dm.each_cast<DmList>(target_category))
            {
                Type design_path = design[RCD_DESIGN_PATH] as Type;

                object design_name = Dm.initial(design_path, (atom_movable a) => a.name);

                Dm.append(ref designs, Dm.list(Dm.list(Dm.kv("title", design_name), Dm.kv("icon", sanitize_css_class_name(design_name)))));
            }
            Dm.append_at(data, "categories", Dm.list(Dm.list(Dm.kv("cat_name", sub_category), Dm.kv("designs", designs))));
        }

        return data;
    }

    // tg: code/game/objects/items/rcd/RCD.dm:380
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = await base.ui_data(user) as DmList;

        //main categories
        data["selected_category"] = design_category;
        data["selected_design"] = design_title;

        //merge airlock_electronics ui data with this
        DmList airlock_data = await airlock_electronics.ui_data(user) as DmList;
        foreach (object key in Dm.each(airlock_data))
            data[key] = airlock_data[key];

        return data;
    }

    // tg: code/game/objects/items/rcd/RCD.dm:394
    public override async DmTask<object> handle_ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        playsound(this, SFX_TOOL_SWITCH, 20, TRUE);

        switch (action)
        {
            case "root_category":
            {
                object new_root = @params["root_category"];
                if (GLOB.rcd_designs[new_root] != null) //is a valid category
                {
                    root_category = new_root;
                    await update_static_data_for_all_viewers();
                }
                break;
            }

            case "design":
            {
                //read and validate params from UI
                object category_name = @params["category"];
                object index = @params["index"];
                DmList root = GLOB.rcd_designs[root_category] as DmList;
                if (root == null) //not a valid root
                    return TRUE;
                DmList category = root[category_name] as DmList;
                if (category == null) //not a valid category
                    return TRUE;

                /**
                 * The advantage of organizing designs into categories is that
                 * You can ignore an complete category if the design disk upgrade for that category isn't installed.
                 */
                // (tg compares the category's list, not its name, with "Machines" and "Furniture": these two checks never match, as written)
                //You can't select designs from the Machines category if you don't have the frames upgrade installed.
                if (Dm.eq(category, "Machines") && !Dm.truthy(Dm.bitand(construction_upgrades, RCD_UPGRADE_FRAMES)))
                    return TRUE;
                //You can't select designs from the Furniture category if you don't have the furnishing upgrade installed.
                if (Dm.eq(category, "Furniture") && !Dm.truthy(Dm.bitand(construction_upgrades, RCD_UPGRADE_FURNISHING)))
                    return TRUE;

                //use UI params to set variables
                DmList design = category[index] as DmList;
                if (design == null) //not a valid design
                    return TRUE;
                design_category = category_name;
                mode = design[RCD_DESIGN_MODE];
                construction_mode = mode;
                rcd_design_path = design[RCD_DESIGN_PATH] as Type;
                design_title = Dm.initial(rcd_design_path, (atom_movable a) => a.name);
                blueprint_changed = TRUE;
                break;
            }

            default:
                airlock_electronics.do_action(action, @params);
                break;
        }

        return TRUE;
    }

    // tg: code/game/objects/items/rcd/RCD.dm:442
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        await ui_interact(user);
        return dot;
    }

    // tg: code/game/objects/items/rcd/RCD.dm:446
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(Dm.num(dot), ITEM_INTERACT_ANY_BLOCKER)))
            return dot;

        mode = construction_mode;
        return await rcd_create(interacting_with, user);
    }

    // tg: code/game/objects/items/rcd/RCD.dm:454
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(ranged) || !Dm.truthy(range_check(interacting_with, user)))
            return ITEM_INTERACT_BLOCKING;

        mode = construction_mode;
        return await rcd_create(interacting_with, user);
    }

    // tg: code/game/objects/items/rcd/RCD.dm:461
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        mode = RCD_DECONSTRUCT;
        return await rcd_create(interacting_with, user);
    }

    // tg: code/game/objects/items/rcd/RCD.dm:465
    public override async DmTask<object> ranged_interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(ranged) || !Dm.truthy(range_check(interacting_with, user)))
            return ITEM_INTERACT_BLOCKING;

        mode = RCD_DECONSTRUCT;
        return await rcd_create(interacting_with, user);
    }

    // tg: code/game/objects/items/rcd/RCD.dm:472
    public override async DmTask handle_openspace_click(turf target = null, mob user = null, DmList modifiers = null)
    {
        await interact_with_atom(target, user as mob_living, modifiers);
    }

    // tg: code/game/objects/items/rcd/RCD.dm:475
    public virtual void detonate_pulse()
    {
        audible_message(span_danger(I18n.T("<b>[] begins to vibrate and buzz loudly!</b>", this)),
            span_danger(I18n.T("<b>[] begins vibrating violently!</b>", this)));
        // 5 seconds to get rid of it
        _addtimer(Dm.@new<datum_callback>(this, nameof(detonate_pulse_explode)), 5 * SECONDS, file: "code/game/objects/items/rcd/RCD.dm", line: 479);
    }

    // tg: code/game/objects/items/rcd/RCD.dm:481
    public virtual async DmTask detonate_pulse_explode()
    {
        await explosion(this, light_impact_range: 3, flame_range: 1, flash_range: 1);
        qdel(this);
    }
}

// tg: code/game/objects/items/rcd/RCD.dm:56
public partial class obj_effect_rcd_hologram : obj_effect
{
    // tg: code/game/objects/items/rcd/RCD.dm:64
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // QDEL_IN(src, RCD_HOLOGRAM_FADE_TIME) (__DEFINES/qdel.dm:59, inline: rules 11.1 (f))
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), RCD_HOLOGRAM_FADE_TIME > GC_FILTER_QUEUE ? WEAKREF(this) : this), RCD_HOLOGRAM_FADE_TIME, file: "code/game/objects/items/rcd/RCD.dm", line: 66);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/rcd/RCD.dm:200  /obj/item/construction/rcd/can_place's var/static/list/small_structures
    internal DmList obj_item_construction_rcd__can_place__small_structures = Dm.list(
        typeof(obj_machinery_door),
        typeof(obj_structure_frame_computer_rcd),
        typeof(obj_structure_floodlight_frame_completed),
        typeof(obj_structure_chair),
        typeof(obj_structure_table),
        typeof(obj_structure_rack),
        typeof(obj_structure_bed),
        typeof(obj_structure_frame_machine_secured),
        typeof(obj_structure_reflector));
}

// Silicons SI2 (D326): the cyborg's RCD, which runs on its cell.
// tg: code/game/objects/items/rcd/RCD.dm:485
public partial class obj_item_construction_rcd_borg : obj_item_construction_rcd
{
    // tg: code/game/objects/items/rcd/RCD.dm:491
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

    // tg: code/game/objects/items/rcd/RCD.dm:500
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
        if (Dm.num(borgy.cell__obj.charge) < (Dm.num(amount) * energyfactor))
        {
            balloon_alert(user, I18n.T("insufficient charge!"));
            return FALSE;
        }
        if (!Dm.truthy(dry_run))
        {
            playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            return await borgy.cell__obj.use(Dm.num(amount) * energyfactor);
        }
        return TRUE;
    }
}

// Gaps DV2 (D333): the exosuit's mounted RCD (the mech's RCD module holds it; the mechs are live): its window follows the mech's,
// its build waits are the module's do_after_mecha, and its matter is the mech's charge (1.6 kJ a sheet's worth) unless it has a
// silo link; a malfunctioning AI's pulse makes the module buzz and blow up five seconds later.
// tg: code/game/objects/items/rcd/RCD.dm:573
public partial class obj_item_construction_rcd_exosuit : obj_item_construction_rcd
{
    private const float MASS_TO_ENERGY = 0.016f * STANDARD_CELL_CHARGE; // tg: RCD.dm:571

    // tg: code/game/objects/items/rcd/RCD.dm:584
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (ismecha(owner))
            return await owner.ui_status(user);
        return UI_CLOSE;
    }

    // tg: code/game/objects/items/rcd/RCD.dm:589
    public override async DmTask<object> build_delay(mob user = null, object delay = null, atom target = null)
    {
        if (Dm.num(delay) <= 0)
            return TRUE;

        obj_item_mecha_parts_mecha_equipment_rcd module = loc as obj_item_mecha_parts_mecha_equipment_rcd;

        //deconstruction can't be cancelled by ui changes
        if (!Dm.eq(mode, RCD_DECONSTRUCT))
            blueprint_changed = FALSE;

        return await module.do_after_mecha(target, user, delay);
    }

    // tg: code/game/objects/items/rcd/RCD.dm:601
    public override object get_matter(mob user = null)
    {
        if (Dm.truthy(silo_link))
            return base.get_matter(user);
        if (!ismecha(owner))
            return 0;
        obj_vehicle_sealed_mecha gundam = (obj_vehicle_sealed_mecha)owner;
        return round(Dm.num(gundam.get_charge()) / MASS_TO_ENERGY);
    }

    // tg: code/game/objects/items/rcd/RCD.dm:609
    public override async DmTask<object> useResource(object amount = null, mob user = null, float? dry_run = null)
    {
        if (Dm.truthy(silo_link))
            return await base.useResource(amount, user, dry_run);
        if (!ismecha(owner))
            return FALSE;
        obj_vehicle_sealed_mecha gundam = (obj_vehicle_sealed_mecha)owner;
        if (!gundam.has_charge(Dm.num(amount) * MASS_TO_ENERGY))
        {
            gundam.balloon_alert(user, I18n.T("insufficient charge!"));
            return FALSE;
        }
        if (!Dm.truthy(dry_run))
        {
            playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            return gundam.use_energy(Dm.num(amount) * MASS_TO_ENERGY);
        }
        return TRUE;
    }

    // tg: code/game/objects/items/rcd/RCD.dm:623
    public override void detonate_pulse()
    {
        obj_item_mecha_parts_mecha_equipment_rcd ourshell = loc as obj_item_mecha_parts_mecha_equipment_rcd;
        if (ourshell == null)
            return;
        ourshell.audible_message(span_danger(I18n.T("<b>[] begins to vibrate and buzz loudly!</b>", ourshell)),
            span_danger(I18n.T("<b>[] begins vibrating violently!</b>", ourshell)));
        // 5 seconds to get rid of it
        _addtimer(Dm.@new<datum_callback>(this, nameof(detonate_pulse_explode)), 5 * SECONDS, file: "code/game/objects/items/rcd/RCD.dm", line: 630);
    }

    // tg: code/game/objects/items/rcd/RCD.dm:632
    public override async DmTask detonate_pulse_explode()
    {
        obj_item_mecha_parts_mecha_equipment_rcd ourshell = loc as obj_item_mecha_parts_mecha_equipment_rcd;
        await explosion(ourshell, light_impact_range: 3, flame_range: 1, flash_range: 1);
        if (Dm.truthy(owner))
            await ourshell.detach();
        qdel(ourshell);
    }
}
