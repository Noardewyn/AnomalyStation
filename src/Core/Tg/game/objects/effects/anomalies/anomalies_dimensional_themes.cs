// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/anomalies/anomalies_dimensional_themes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;


namespace Anomaly.Core.Tg;

// The M7 delamination slice (G6, D167): the whole file, the dimensional anomaly's themes. The floors, walls and objects a theme
// puts in are tg's types; those whose own code is not translated yet are made inert (D112: drawn with their sprite, their own
// behaviour comes with their milestone; not-translated.md 38). The disco floor's colour waits for the light floor (M10+).
// `window_colour` is declared here as tg's untyped var (object: a material's colour may be a colour matrix, a list, as
// tg's glass's); the rest of the var blocks is generated (D111).

/**
 * Datum which describes a theme and replaces turfs and objects in specified locations to match that theme
 */
// tg: code/game/objects/effects/anomalies/anomalies_dimensional_themes.dm:4
public partial class datum_dimension_theme : datum
{
    /// Colour to recolour windows with, replaced by material colour if material was specified.
    // tg: code/game/objects/effects/anomalies/anomalies_dimensional_themes.dm:35
    public object window_colour = "#ffffff";

    // tg: code/game/objects/effects/anomalies/anomalies_dimensional_themes.dm:37
    public override async DmTask New(params object[] __extra)
    {
        if (Dm.truthy(material))
        {
            datum_material using_mat = await SSmaterials.get_material(material) as datum_material;
            // GAP(lead): the meat and pizza materials are generated-only (DV25; food, M8), so SSmaterials makes none: their themes
            // keep the default window colour (and apply_materials puts no material on their tiles)
            if (using_mat != null)
                window_colour = using_mat.color;
        }
    }

    /**
     * Applies themed transformation to the provided turf.
     *
     * Arguments
     * * affected_turf - Turf to transform.
     * * skip_sound - If the sound shouldn't be played.
     * * show_effect - if the temp visual effect should be shown.
     */
    // tg: code/game/objects/effects/anomalies/anomalies_dimensional_themes.dm:50
    public virtual async DmTask apply_theme(turf affected_turf = null, float? skip_sound = null, float? show_effect = null)
    {
        skip_sound ??= FALSE;
        show_effect ??= FALSE;
        if (!Dm.truthy(await replace_turf(affected_turf)))
            return;
        affected_turf = turf.dm_identity(affected_turf) as turf; // TURF-IDENTITY: tg's reference names its cell's turf, the one replace_turf put there
        if (!Dm.truthy(skip_sound))
            playsound(affected_turf, sound, 100, TRUE);
        if (Dm.truthy(show_effect))
            Dm.@new<obj_effect_temp_visual_transmute_tile_flash>(affected_turf);
        foreach (obj @object in Dm.each<obj>(affected_turf))
            await replace_object(@object);
        if (Dm.truthy(length(random_spawns)) && prob(random_spawn_chance) && !Dm.truthy(await affected_turf.is_blocked_turf(exclude_mobs: TRUE)))
        {
            object random_spawn_picked = pick(random_spawns);
            Dm.@new((Type)random_spawn_picked, affected_turf);
        }
        if (Dm.truthy(material))
            await apply_materials(affected_turf);
    }

    /**
     * Applies the transformation to a list of turfs, ensuring a sound is only played every few turfs to reduce noice spam
     *
     * Arguments
     * * list/turf/all_turfs - List of turfs to transform.
     */
    // tg: code/game/objects/effects/anomalies/anomalies_dimensional_themes.dm:71
    public virtual async DmTask apply_theme_to_list_of_turfs(DmList all_turfs = null)
    {
        float every_nth_turf = 0;
        foreach (turf turf in Dm.each_cast<turf>(all_turfs))
        {
            if (Dm.truthy(can_convert(turf)))
            {
                await apply_theme(turf, skip_sound: Dm.num(Dm.mod(every_nth_turf, 7) != 0));
                every_nth_turf++;
            }
            // CHECK_TICK: not a suspension (rules 11.3, DV3)
        }
    }

    /**
     * Returns true if you actually can transform the provided turf.
     *
     * Arguments
     * * affected_turf - Turf to transform.
     */
    // tg: code/game/objects/effects/anomalies/anomalies_dimensional_themes.dm:85
    public virtual object can_convert(turf affected_turf = null)
    {
        if (isspaceturf(affected_turf))
            return FALSE;
        if (isfloorturf(affected_turf))
        {
            if (isindestructiblefloor(affected_turf))
                return FALSE;
            if (Dm.truthy(affected_turf.holodeck_compatible))
                return FALSE;
            return Dm.num(replace_floors.len > 0);
        }
        if (iswallturf(affected_turf))
        {
            if (isindestructiblewall(affected_turf))
                return FALSE;
            return TRUE;
        }
        return FALSE;
    }

    /**
     * Replaces the provided turf with a different one.
     *
     * Arguments
     * * affected_turf - Turf to transform.
     */
    // tg: code/game/objects/effects/anomalies/anomalies_dimensional_themes.dm:106  (PROTECTED_PROC is a lint macro, rules 7.9)
    public virtual async DmTask<object> replace_turf(turf affected_turf = null)
    {
        if (isfloorturf(affected_turf))
        {
            if (isindestructiblefloor(affected_turf))
                return FALSE;
            if (Dm.truthy(affected_turf.holodeck_compatible))
                return FALSE;
            return await transform_floor((turf_open_floor)affected_turf);
        }

        if (!iswallturf(affected_turf))
            return FALSE;
        if (isindestructiblewall(affected_turf))
            return FALSE;
        await affected_turf.ChangeTurf(replace_walls, flags: CHANGETURF_INHERIT_MOUNTS);
        return TRUE;
    }

    /**
     * Replaces the provided floor turf with a different one.
     *
     * Arguments
     * * affected_floor - Floor turf to transform.
     */
    // tg: code/game/objects/effects/anomalies/anomalies_dimensional_themes.dm:130  (PROTECTED_PROC is a lint macro, rules 7.9)
    public virtual async DmTask<object> transform_floor(turf_open_floor affected_floor = null)
    {
        if (replace_floors.len == 0)
            return FALSE;
        await affected_floor.replace_floor(pick_weight(replace_floors) as Type, flags: Dm.bitor(CHANGETURF_INHERIT_AIR, CHANGETURF_INHERIT_MOUNTS));
        return TRUE;
    }

    /**
     * Replaces the provided object with a different one.
     *
     * Arguments
     * * object - Object to replace.
     */
    // tg: code/game/objects/effects/anomalies/anomalies_dimensional_themes.dm:144  (PROTECTED_PROC is a lint macro, rules 7.9)
    public virtual async DmTask replace_object(obj @object = null)
    {
        if (@object is obj_structure_window)
        {
            await transform_window((obj_structure_window)@object);
            return;
        }

        Type replace_path = get_replacement_object_typepath(@object) as Type;
        if (replace_path == null)
            return;
        obj new_object = Dm.@new(replace_path, @object.loc) as obj;
        await new_object.setDir(@object.dir);
        if (@object is obj_machinery_door_airlock)
        {
            if (new_object is obj_machinery_door_airlock) //carry over access-related and adjacent variables. The rest is not as important
            {
                obj_machinery_door_airlock airlock = (obj_machinery_door_airlock)@object;
                obj_machinery_door_airlock new_airlock = (obj_machinery_door_airlock)new_object;
                new_airlock.closeOtherId = airlock.closeOtherId;
                new_airlock.unres_sides = airlock.unres_sides;
                new_airlock.req_access = airlock.req_access?.Copy();
                new_airlock.req_one_access = airlock.req_one_access?.Copy();
                new_airlock.locked = airlock.locked;
                new_airlock.emergency = airlock.emergency;
                new_airlock.update_appearance();
            }
            new_object.name = @object.name;
        }
        qdel(@object);
    }

    /**
     * Returns the typepath of an object to replace the provided object.
     *
     * Arguments
     * * object - Object to transform.
     */
    // tg: code/game/objects/effects/anomalies/anomalies_dimensional_themes.dm:175  (PROTECTED_PROC is a lint macro, rules 7.9)
    public virtual object get_replacement_object_typepath(obj @object = null)
    {
        foreach (object type in Dm.each(replace_objs))
        {
            if (istype(@object, (Type)type))
                return pick_weight(replace_objs[type] as DmList);
        }
        return null;
    }

    /**
     * Replaces a window with a different window and recolours it.
     * This needs its own function because we only want to replace full tile windows.
     *
     * Arguments
     * * object - Object to transform.
     */
    // tg: code/game/objects/effects/anomalies/anomalies_dimensional_themes.dm:189  (PROTECTED_PROC is a lint macro, rules 7.9)
    public virtual async DmTask transform_window(obj_structure_window window = null)
    {
        if (!Dm.truthy(window.fulltile))
            return;
        if (replace_window == null)
        {
            await window.add_atom_colour(window_colour, FIXED_COLOUR_PRIORITY);
            return;
        }

        obj_structure_window new_window = Dm.@new(replace_window, window.loc) as obj_structure_window;
        await new_window.add_atom_colour(window_colour, FIXED_COLOUR_PRIORITY);
        qdel(window);
    }

    // tg: code/game/objects/effects/anomalies/anomalies_dimensional_themes.dm:202  #define PERMITTED_MATERIAL_REPLACE_TYPES list(...) (file-local, rules 11.4)
    private static DmList PERMITTED_MATERIAL_REPLACE_TYPES() => Dm.list(
        typeof(obj_structure_chair),
        typeof(obj_machinery_door_airlock),
        typeof(obj_structure_table),
        typeof(obj_structure_toilet),
        typeof(obj_structure_window),
        typeof(obj_structure_sink),
        typeof(obj_structure_platform));

    /**
     * Returns true if the provided object can have its material modified.
     *
     * Arguments
     * * object - Object to transform.
     */
    // tg: code/game/objects/effects/anomalies/anomalies_dimensional_themes.dm:218  (PROTECTED_PROC is a lint macro, rules 7.9)
    public virtual object permit_replace_material(obj @object = null)
    {
        return is_type_in_list(@object, PERMITTED_MATERIAL_REPLACE_TYPES());
    }

    /**
     * Applies a new custom material to the contents of a provided turf.
     *
     * Arguments
     * * affected_turf - Turf to transform.
     */
    // tg: code/game/objects/effects/anomalies/anomalies_dimensional_themes.dm:229  (PROTECTED_PROC is a lint macro, rules 7.9)
    public virtual async DmTask apply_materials(turf affected_turf = null)
    {
        object material_ref = await SSmaterials.get_material(material);
        // GAP(lead): no meat or pizza material is made (DV25; food, M8; New above)
        if (material_ref == null)
            return;
        DmList custom_materials = Dm.list(Dm.kv(material_ref, SHEET_MATERIAL_AMOUNT));

        if (affected_turf is turf_open_floor_material || affected_turf is turf_closed_wall_material)
            await affected_turf.set_custom_materials(custom_materials);
        foreach (obj thing in Dm.each<obj>(affected_turf))
        {
            if (!Dm.truthy(permit_replace_material(thing)))
                continue;
            await thing.set_custom_materials(custom_materials);
            thing.update_appearance(updates: UPDATE_ICON);
        }
    }
}

// tg: code/game/objects/effects/anomalies/anomalies_dimensional_themes.dm:395
public partial class datum_dimension_theme_fancy : datum_dimension_theme
{
    // tg: code/game/objects/effects/anomalies/anomalies_dimensional_themes.dm:406
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        valid_carpets = Dm.list(
            typeof(turf_open_floor_carpet_black),
            typeof(turf_open_floor_carpet_blue),
            typeof(turf_open_floor_carpet_cyan),
            typeof(turf_open_floor_carpet_green),
            typeof(turf_open_floor_carpet_lone_star),
            typeof(turf_open_floor_carpet_orange),
            typeof(turf_open_floor_carpet_purple),
            typeof(turf_open_floor_carpet_red),
            typeof(turf_open_floor_carpet_royalblack),
            typeof(turf_open_floor_carpet_royalblue),
            typeof(turf_open_floor_eighties),
            typeof(turf_open_floor_eighties_red)
        );
        valid_tables = subtypesof(typeof(obj_structure_table_wood_fancy));
        randomize_theme();
    }

    // tg: code/game/objects/effects/anomalies/anomalies_dimensional_themes.dm:424
    public virtual void randomize_theme()
    {
        replace_floors = Dm.list(Dm.kv(pick(valid_carpets), 1));
        replace_objs[typeof(obj_structure_table_wood)] = Dm.list(Dm.kv(pick(valid_tables), 1));
    }

    // tg: code/game/objects/effects/anomalies/anomalies_dimensional_themes.dm:428
    public override async DmTask apply_theme(turf affected_turf = null, float? skip_sound = null, float? show_effect = null)
    {
        skip_sound ??= FALSE;
        show_effect ??= FALSE;
        if (carpet_switch_cd <= world.time) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        {
            randomize_theme();
            carpet_switch_cd = world.time + 90 * SECONDS; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline (rules 11.1 (a))
        }
        await base.apply_theme(affected_turf, skip_sound, show_effect);
    }
}

// tg: code/game/objects/effects/anomalies/anomalies_dimensional_themes.dm:434
public partial class datum_dimension_theme_disco : datum_dimension_theme
{
    // tg: code/game/objects/effects/anomalies/anomalies_dimensional_themes.dm:441
    public override async DmTask<object> transform_floor(turf_open_floor affected_floor = null)
    {
        object dot = null;
        dot = await base.transform_floor(affected_floor);
        if (!Dm.truthy(dot))
            return dot;
        // var/turf/open/floor/light/disco_floor = affected_floor; disco_floor.currentcolor = pick(disco_floor.coloredlights); disco_floor.update_appearance()
        // GAP(lead): the light floor (game/turfs/open/floor/light_floor.dm) is not translated: its static coloredlights is not
        // declared and its own update_appearance would not run (inert), so the disco floor keeps its default cyan light
        Dm.Deferred("code/game/objects/effects/anomalies/anomalies_dimensional_themes.dm:446", "transform_floor: the disco floor's random colour (the light floor, light_floor.dm, is not translated; M10+)");
        return dot;
    }
}
