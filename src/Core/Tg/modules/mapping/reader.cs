// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapping/reader.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 P2: the whole file. The DM text parsing (New's dmm_regex loop over the grid, tgm_build_cache's and dmm_build_cache's
// model parsing, readlist, parse_constant) is BYOND-side work in the port: the engine reader of our format
// (Infrastructure/MapLoader/MapText.cs, docs/ai/specs/map-format.md 5) parses the file, and the procs below fill tg's
// datum from it in tg's shape: the TGM columns as grid sets (one per x and z, the keys top row first), a model key per
// distinct model (letters of one static length, as tg's keys), modelCache's list(members, members_attributes). Everything
// else (the bounds, the offsets and crops, the space key skip, build_coordinate, the preloader, the area cache, the
// load's tail) is tg's code.
// TURF-IDENTITY (M3 P2, the ChangeTurf identity decision): BYOND's turf reference is its location, so tg goes on using a
// turf reference after `new path(T)` replaced the turf and reaches the new one. The port's reference is the replaced
// object (off the map, deleted, MapGrid.Replace), so translated code that uses the reference after the replacement
// takes the new turf instead, marked TURF-IDENTITY at the site.

// tg: code/modules/mapping/reader.dm:72  (GLOBAL_LIST_EMPTY(map_model_default), :680, expanded inline: rules 10.6, 11.1 (b))
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/mapping/reader.dm:680
    public DmList map_model_default;

    // tg: code/modules/mapping/reader.dm:680  (GLOBAL_MANAGED(map_model_default, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmap_model_default()
    {
        map_model_default = Dm.list();
        Dm.append(ref gvars_datum_init_order, "map_model_default");
    }
}

// tg: code/modules/mapping/reader.dm:73
public partial class datum_parsed_map : datum
{
    // #define MAP_DMM "dmm" (:28), MAP_TGM "tgm" (:62), MAP_UNKNOWN "unknown" (:63): file-local defines (#undef at the end)
    private const string MAP_DMM = "dmm";
    private const string MAP_TGM = "tgm";

    // ENGINE (M3 P2): the parsed file behind grid_models and the grid sets (null for a datum made without a file).
    [NotHashed]
    internal ParsedMapText dm_text;

    // ENGINE: the model of each key of grid_models.
    [NotHashed]
    internal Dictionary<string, MapModel> dm_models;

    // tg: code/modules/mapping/reader.dm:116
    public virtual object copy()
    {
        // Avoids duped work just in case
        build_cache();
        var newfriend = Dm.@new<datum_parsed_map>();
        newfriend.original_path = original_path;
        newfriend.map_format = map_format;
        newfriend.key_len = key_len;
        newfriend.line_len = line_len;
        newfriend.grid_models = grid_models.Copy();
        newfriend.gridSets = gridSets.Copy();
        newfriend.modelCache = modelCache.Copy();
        newfriend.parsed_bounds = parsed_bounds.Copy();
        // Copy parsed bounds to reset to initial values
        newfriend.bounds = parsed_bounds.Copy();
        newfriend.turf_blacklist = turf_blacklist?.Copy();
        newfriend.dm_text = dm_text;
        newfriend.dm_models = dm_models;
        return newfriend;
    }

    /// Parse a map, possibly cropping it.
    // tg: code/modules/mapping/reader.dm:178
    [DmParams("tfile", "x_lower", "x_upper", "y_lower", "y_upper", "z_lower", "z_upper", "measureOnly")]
    public override DmTask New(params object[] __extra)
    {
        object tfile = Dm.arg(__extra, 0);
        float x_lower = __extra.Length > 1 && Dm.arg(__extra, 1) != null ? Dm.num(Dm.arg(__extra, 1)) : float.NegativeInfinity;
        float x_upper = __extra.Length > 2 && Dm.arg(__extra, 2) != null ? Dm.num(Dm.arg(__extra, 2)) : float.PositiveInfinity;
        float y_lower = __extra.Length > 3 && Dm.arg(__extra, 3) != null ? Dm.num(Dm.arg(__extra, 3)) : float.NegativeInfinity;
        float y_upper = __extra.Length > 4 && Dm.arg(__extra, 4) != null ? Dm.num(Dm.arg(__extra, 4)) : float.PositiveInfinity;
        float z_lower = __extra.Length > 5 && Dm.arg(__extra, 5) != null ? Dm.num(Dm.arg(__extra, 5)) : float.NegativeInfinity;
        float z_upper = __extra.Length > 6 && Dm.arg(__extra, 6) != null ? Dm.num(Dm.arg(__extra, 6)) : float.PositiveInfinity;
        bool measureOnly = __extra.Length > 7 && Dm.truthy(Dm.arg(__extra, 7));

        // This proc sleeps for like 6 seconds. why?
        // Is it file accesses? if so, can those be done ahead of time, async to save on time here? I wonder.
        // Love ya :)
        if (isfile(tfile))
        {
            original_path = Dm.text("[]", tfile is DmFileRef fileRef ? fileRef.Path : tfile);
            tfile = file2text(tfile);
        }
        else if (tfile == null)
        {
            // create a new datum without loading a map
            return DmTask.CompletedTask;
        }

        this.bounds = parsed_bounds = Dm.list(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        // ENGINE: no text (BYOND's file2text of a folder: preloadTemplates lists _maps/templates/, whose lazy_templates/ folder the port
        // mounts since syndicate X1a): tg's regex finds nothing, and the failure to parse any coordinates nulls the bounds (:208)
        if (tfile is not string read || read.Length == 0)
        {
            this.bounds = null;
            return DmTask.CompletedTask;
        }

        // ENGINE: tg tells TGM from DMM by matches_tgm; our format is read into the TGM shape, so it is MAP_TGM.
        map_format = MAP_TGM;

        // lists are structs don't you know :)
        DmList bounds = this.bounds;
        DmList grid_models = this.grid_models;
        float key_len = this.key_len;
        float line_len = this.line_len;

        // ENGINE: tg's `while(dmm_regex.Find(tfile, stored_index))` reads the models ("key" = (...)) and the grid sets
        // ((x,y,z) = {"..."}) out of the DM text; the engine reader gives the same records from our format, in tg's order
        // (the models first, then the columns, x fastest within each z).
        ParsedMapText text;
        try
        {
            text = MapText.Parse((string)tfile ?? "", original_path as string ?? "map");
        }
        catch (MapTextException e)
        {
            throw new DmRuntimeException("map file " + original_path + ": " + e.Message);
        }
        dm_text = text;
        dm_models = new Dictionary<string, MapModel>(StringComparer.Ordinal);
        int keyWidth = dm_key_width(text.Models.Count);
        for (int m = 0; m < text.Models.Count; m++)
        {
            // "aa" = (/type{vars=blah})
            string key = dm_key(m, keyWidth);
            if (Dm.truthy(grid_models[key])) // Duplicate model keys are ignored in DMMs
                continue;
            if (!Dm.eq(key_len, length(key)))
            {
                if (!Dm.truthy(key_len))
                    key_len = length(key);
                else
                    throw Dm.CRASH("Inconsistent key length in DMM", "code/modules/mapping/reader.dm", 218);
            }
            dm_models[key] = text.Models[m];
            if (!measureOnly)
                grid_models[key] = text.Models[m].Text;
        }
        var keyOfModel = new Dictionary<MapModel, string>(ReferenceEqualityComparer.Instance);
        for (int m = 0; m < text.Models.Count; m++) keyOfModel[text.Models[m]] = dm_key(m, keyWidth);

        for (int z = 1; z <= text.Levels; z++)
        {
            for (int x = 1; x <= text.Width; x++)
            {
                // (1,1,1) = {"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}
                if (!Dm.truthy(key_len))
                    throw Dm.CRASH("Coords before model definition in DMM", "code/modules/mapping/reader.dm", 225);

                float curr_x = x;
                if (curr_x < x_lower || curr_x > x_upper)
                    continue;

                float curr_y = 1;
                if (curr_y < y_lower || curr_y > y_upper)
                    continue;

                float curr_z = z;
                if (curr_z < z_lower || curr_z > z_upper)
                    continue;

                var gridSet = Dm.@new<datum_grid_set>();

                gridSet.xcrd = curr_x;
                gridSet.ycrd = curr_y;
                gridSet.zcrd = curr_z;

                bounds[MAP_MINX] = Math.Min(Dm.num(bounds[MAP_MINX]), curr_x);
                bounds[MAP_MINZ] = Math.Min(Dm.num(bounds[MAP_MINZ]), curr_y);
                bounds[MAP_MAXZ] = Math.Max(Dm.num(bounds[MAP_MAXZ]), curr_z);

                // ENGINE: splittext of the column's keys, top row first (TGM's column block)
                DmList gridLines = Dm.list();
                for (int y = text.Height; y >= 1; y--)
                    gridLines.Add(keyOfModel[text.ModelAt(x, y, z)]);
                gridSet.gridLines = gridLines;

                // tg: :252-262  the leading and trailing blank lines: a column of our format has none
                if (!Dm.truthy(length(gridLines))) // Skip it if only blank lines exist.
                    continue;

                Dm.append(ref gridSets, gridSet);

                bounds[MAP_MINY] = Math.Min(Dm.num(bounds[MAP_MINY]), Dm.num(gridSet.ycrd));
                gridSet.ycrd = Dm.num(gridSet.ycrd) + length(gridLines) - 1; // Start at the top and work down
                bounds[MAP_MAXY] = Math.Max(Dm.num(bounds[MAP_MAXY]), Dm.num(gridSet.ycrd));

                if (!Dm.truthy(line_len))
                    line_len = length(gridLines[1]);

                float maxx = curr_x;
                if (Dm.truthy(length(gridLines))) //Not an empty map
                    maxx = Math.Max(maxx, curr_x + line_len / key_len - 1);

                bounds[MAP_MAXX] = Math.Max(Dm.num(bounds[MAP_MAXX]), maxx);
            }
            CHECK_TICK();
        }

        // Indicate failure to parse any coordinates by nulling bounds
        if (Dm.eq(bounds[1], float.PositiveInfinity))
        {
            this.bounds = null;
        }
        else
        {
            // Clamp all our mins and maxes down to the proscribed limits
            bounds[MAP_MINX] = clamp(Dm.num(bounds[MAP_MINX]), x_lower, x_upper);
            bounds[MAP_MAXX] = clamp(Dm.num(bounds[MAP_MAXX]), x_lower, x_upper);
            bounds[MAP_MINY] = clamp(Dm.num(bounds[MAP_MINY]), y_lower, y_upper);
            bounds[MAP_MAXY] = clamp(Dm.num(bounds[MAP_MAXY]), y_lower, y_upper);
            bounds[MAP_MINZ] = clamp(Dm.num(bounds[MAP_MINZ]), z_lower, z_upper);
            bounds[MAP_MAXZ] = clamp(Dm.num(bounds[MAP_MAXZ]), z_lower, z_upper);
        }

        parsed_bounds = this.bounds;
        this.key_len = key_len;
        this.line_len = line_len;
        return DmTask.CompletedTask;
    }

    // ENGINE: the number of letters of a model key for `count` models (tg's keys are letters of one static length).
    private static int dm_key_width(int count)
    {
        int width = 1;
        for (long capacity = 52; capacity < count; capacity *= 52) width++;
        return width;
    }

    // ENGINE: the key of model `index` (a-z, then A-Z, the last letter fastest).
    private static string dm_key(int index, int width)
    {
        var letters = new char[width];
        for (int i = width - 1; i >= 0; i--)
        {
            int digit = index % 52;
            letters[i] = digit < 26 ? (char)('a' + digit) : (char)('A' + digit - 26);
            index /= 52;
        }
        return new string(letters);
    }
}

// tg: code/modules/mapping/reader.dm:151  (the global procs, rules 7.8)
public static partial class GlobalProcs
{
    /**
     * Helper and recommened way to load a map file
     */
    // tg: code/modules/mapping/reader.dm:151
    public static async DmTask<object> load_map(object dmm_file = null, float? x_offset = null, float? y_offset = null, float? z_offset = null, float? crop_map = null, float? measure_only = null, float? no_changeturf = null, float? x_lower = null, float? x_upper = null, float? y_lower = null, float? y_upper = null, float? z_lower = null, float? z_upper = null, float? place_on_top = null, float? new_z = null)
    {
        x_offset ??= 0;
        y_offset ??= 0;
        z_offset ??= 0;
        crop_map ??= FALSE;
        measure_only ??= FALSE;
        no_changeturf ??= FALSE;
        x_lower ??= float.NegativeInfinity;
        x_upper ??= float.PositiveInfinity;
        y_lower ??= float.NegativeInfinity;
        y_upper ??= float.PositiveInfinity;
        z_lower ??= float.NegativeInfinity;
        z_upper ??= float.PositiveInfinity;
        place_on_top ??= FALSE;
        new_z ??= FALSE;
        if (!Dm.@in(dmm_file, GLOB.cached_maps))
            GLOB.cached_maps[dmm_file] = Dm.@new<datum_parsed_map>(dmm_file);

        var parsed_map = (datum_parsed_map)GLOB.cached_maps[dmm_file];
        parsed_map = (datum_parsed_map)parsed_map.copy();
        if (!Dm.truthy(measure_only) && parsed_map.bounds != null)
            await parsed_map.load(x_offset, y_offset, z_offset, crop_map, no_changeturf, x_lower, x_upper, y_lower, y_upper, z_lower, z_upper, place_on_top, new_z);
        return parsed_map;
    }
}

// tg: code/modules/mapping/reader.dm:297
public partial class datum_parsed_map : datum
{
    /// Iterates over all grid sets and returns ones with z values within the given bounds. Inclusive
    // tg: code/modules/mapping/reader.dm:297
    public virtual object filter_grid_sets_based_on_z_bounds(object lower_z = null, object upper_z = null)
    {
        DmList filtered_sets = Dm.list();
        foreach (datum_grid_set grid_set in Dm.each<datum_grid_set>(gridSets))
        {
            if (Dm.lt(grid_set.zcrd, lower_z))
                continue;
            if (Dm.gt(grid_set.zcrd, upper_z))
                continue;
            Dm.append(ref filtered_sets, grid_set);
        }
        return filtered_sets;
    }

    /// Load the parsed map into the world. You probably want [/proc/load_map]. Keep the signature the same.
    // tg: code/modules/mapping/reader.dm:308
    public virtual async DmTask<object> load(float? x_offset = null, float? y_offset = null, float? z_offset = null, float? crop_map = null, float? no_changeturf = null, float? x_lower = null, float? x_upper = null, float? y_lower = null, float? y_upper = null, float? z_lower = null, float? z_upper = null, float? place_on_top = null, float? new_z = null)
    {
        x_offset ??= 0;
        y_offset ??= 0;
        z_offset ??= 0;
        crop_map ??= FALSE;
        no_changeturf ??= FALSE;
        x_lower ??= float.NegativeInfinity;
        x_upper ??= float.PositiveInfinity;
        y_lower ??= float.NegativeInfinity;
        y_upper ??= float.PositiveInfinity;
        z_lower ??= float.NegativeInfinity;
        z_upper ??= float.PositiveInfinity;
        place_on_top ??= FALSE;
        new_z ??= FALSE;
        //How I wish for RAII
        await Master.StartLoadingMap();
        object dot = await _load_impl(x_offset, y_offset, z_offset, crop_map, no_changeturf, x_lower, x_upper, y_lower, y_upper, z_lower, z_upper, place_on_top, new_z);
        Master.StopLoadingMap();
        return dot;
    }

    // #define MAPLOADING_CHECK_TICK (:314), as a member (rules 11.1): TICK_CHECK is false with the virtual tick usage (DV3)
    private async DmTask MAPLOADING_CHECK_TICK()
    {
        if (TICK_CHECK())
        {
            if (Dm.truthy(loading))
            {
                SSatoms.map_loader_stop(REF(this));
                await stoplag();
                SSatoms.map_loader_begin(REF(this));
            }
            else
            {
                await stoplag();
            }
        }
    }

    // Do not call except via load() above.
    // tg: code/modules/mapping/reader.dm:326
    public virtual async DmTask<object> _load_impl(object x_offset = null, object y_offset = null, object z_offset = null, object crop_map = null, object no_changeturf = null, object x_lower = null, object x_upper = null, object y_lower = null, object y_upper = null, object z_lower = null, object z_upper = null, object place_on_top = null, object new_z = null)
    {
        // PRIVATE_PROC(TRUE)
        // Tell ss atoms that we're doing maploading
        // We'll have to account for this in the following tick_checks so it doesn't overflow
        loading = TRUE;
        SSatoms.map_loader_begin(REF(this));

        // Loading used to be done in this proc
        // We make the assumption that if the inner procs runtime, we WANT to do cleanup on them, but we should stil tell our parents we failed
        // Since well, we did
        object sucessful = FALSE;
        try
        {
            switch (map_format)
            {
                case MAP_TGM:
                    sucessful = await _tgm_load(x_offset, y_offset, z_offset, crop_map, no_changeturf, x_lower, x_upper, y_lower, y_upper, z_lower, z_upper, place_on_top, new_z);
                    break;
                default:
                    sucessful = await _dmm_load(x_offset, y_offset, z_offset, crop_map, no_changeturf, x_lower, x_upper, y_lower, y_upper, z_lower, z_upper, place_on_top, new_z);
                    break;
            }
        }
        catch (Exception e)
        {
            // DM: a runtime of the inner load ends only that proc, so tg's cleanup below runs and the caller is told of the failure (tg's note above; rules 9.3).
            // Without it SSatoms would stay in its map loading state, and no atom made after it would be initialized.
            World.Current.ReportRuntime(e);
            sucessful = FALSE;
        }

        // And we are done lads, call it off
        SSatoms.map_loader_stop(REF(this));
        loading = FALSE;

        if (Dm.truthy(new_z))
        {
            for (float z_index = Dm.num(bounds[MAP_MINZ]), z_index__end = Dm.num(bounds[MAP_MAXZ]); z_index <= z_index__end; z_index++)
                SSmapping.build_area_turfs(z_index);
        }

        if (!Dm.truthy(no_changeturf))
        {
            DmList turfs = block(
                Dm.num(bounds[MAP_MINX]), Dm.num(bounds[MAP_MINY]), Dm.num(bounds[MAP_MINZ]),
                Dm.num(bounds[MAP_MAXX]), Dm.num(bounds[MAP_MAXY]), Dm.num(bounds[MAP_MAXZ])
            );
            foreach (turf T in Dm.each<turf>(turfs))
            {
                //we do this after we load everything in. if we don't, we'll have weird atmos bugs regarding atmos adjacent turfs
                T.AfterChange(CHANGETURF_IGNORE_AIR);
            }
        }

        if (Dm.truthy(expanded_x) || Dm.truthy(expanded_y))
        {
            // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_EXPANDED_WORLD_BOUNDS, expanded_x, expanded_y), expanded inline (rules 11.1 (e))
            if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_EXPANDED_WORLD_BOUNDS]))
                SSdcs._SendSignal(COMSIG_GLOB_EXPANDED_WORLD_BOUNDS, Dm.list(SSdcs, expanded_x, expanded_y));
        }

        // tg: #ifdef TESTING ... #endif not translated (inactive in production)

        return sucessful;
    }

    // Wanna clear something up about maps, talking in 255x255 here
    // In the tgm format, each gridset contains 255 lines, each line representing one tile, with 255 total gridsets
    // In the dmm format, each gridset contains 255 lines, each line representing one row of tiles, containing 255 * line length characters, with one gridset per z
    // You can think of dmm as storing maps in rows, whereas tgm stores them in columns
    // tg: code/modules/mapping/reader.dm:374
    public virtual async DmTask<object> _tgm_load(object x_offset = null, object y_offset = null, object z_offset = null, object crop_map = null, object no_changeturf = null, object x_lower = null, object x_upper = null, object y_lower = null, object y_upper = null, object z_lower = null, object z_upper = null, object place_on_top = null, object new_z = null)
    {
        // setup
        var modelCache = (DmList)build_cache(no_changeturf);
        object space_key = modelCache[SPACE_KEY];
        DmList bounds;
        this.bounds = bounds = Dm.list(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        // Building y coordinate ranges
        float y_relative_to_absolute = Dm.num(y_offset) - 1;
        float x_relative_to_absolute = Dm.num(x_offset) - 1;

        // Ok so like. something important
        // We talk in "relative" coords here, so the coordinate system of the map datum
        // This is so we can do offsets, but it is NOT the same as positions in game
        // That's why there's some uses of - y_relative_to_absolute here, to turn absolute positions into relative ones
        // TGM maps process in columns, so the starting y will always be the max size
        // We know y starts at 1
        var first_column = (datum_grid_set)gridSets[1];
        float relative_y = Dm.num(first_column.ycrd);
        float highest_y = relative_y + y_relative_to_absolute;

        if (!Dm.truthy(crop_map) && highest_y > Dm.world.maxy)
        {
            if (Dm.truthy(new_z))
            {
                // Need to avoid improperly loaded area/turf_contents
                Dm.world.increase_max_y(highest_y, map_load_z_cutoff: Dm.num(z_offset) - 1);
            }
            else
            {
                Dm.world.increase_max_y(highest_y);
            }
            expanded_y = TRUE;
        }

        // Skip Y coords that are above the smallest of the three params
        // So maxy and y_upper get to act as thresholds, and relative_y can play
        float y_skip_above = Math.Min(Math.Min(Dm.world.maxy - y_relative_to_absolute, Dm.num(y_upper)), relative_y);
        // How many lines to skip because they'd be above the y cuttoff line
        float y_starting_skip = relative_y - y_skip_above;
        highest_y -= y_starting_skip;

        // Y is the LOWEST it will ever be here, so we can easily set a threshold for how low to go
        float line_count = length(first_column.gridLines);
        float lowest_y = relative_y - (line_count - 1); // -1 because we decrement at the end of the loop, not the start
        float y_ending_skip = Math.Max(Math.Max(Dm.num(y_lower), 1 - y_relative_to_absolute) - lowest_y, 0);

        // X setup
        float x_delta_with = Dm.num(x_upper);
        if (Dm.truthy(crop_map))
        {
            // Take our smaller crop threshold yes?
            x_delta_with = Math.Min(x_delta_with, Dm.world.maxx);
        }

        // We're gonna skip all the entries above the upper x, or maxx if cropMap is set
        // The last column is guarenteed to have the highest x value we;ll encounter
        // Even if z scales, this still works
        var last_column = (datum_grid_set)gridSets[length(gridSets)];
        float final_x = Dm.num(last_column.xcrd) + x_relative_to_absolute;

        if (final_x > x_delta_with)
        {
            // If our relative x is greater then X upper, well then we've gotta limit our expansion
            float delta = Math.Max(final_x - x_delta_with, 0);
            final_x -= delta;
        }
        if (final_x > Dm.world.maxx && !Dm.truthy(crop_map))
        {
            if (Dm.truthy(new_z))
            {
                // Need to avoid improperly loaded area/turf_contents
                Dm.world.increase_max_x(final_x, map_load_z_cutoff: Dm.num(z_offset) - 1);
            }
            else
            {
                Dm.world.increase_max_x(final_x);
            }
            expanded_x = TRUE;
        }

        float lowest_x = Math.Max(Dm.num(x_lower), 1 - x_relative_to_absolute);

        // Amount we offset the grid zcrd to get the true zcrd
        float grid_z_offset = Dm.num(z_offset) - 1;
        bool z_upper_set = Dm.num(z_upper) < float.PositiveInfinity;
        bool z_lower_set = Dm.num(z_lower) > float.NegativeInfinity;

        // We make the assumption that the last block of turfs will have the highest embedded z in it
        // true max zcrd
        float map_bounds_z_max = Dm.num(last_column.zcrd);
        float z_upper_parsed = map_bounds_z_max + Dm.num(z_offset) - 1;
        if (z_upper_set)
            z_upper_parsed -= map_bounds_z_max - Dm.num(z_upper);
        if (z_lower_set)
        {
            float offset_amount = Dm.num(z_lower) - 1;
            z_upper_parsed -= offset_amount;
            grid_z_offset -= offset_amount;
        }

        DmList target_grid_sets = gridSets;
        if (z_lower_set || z_upper_set) // bounds are set, filter out gridsets for z levels we don't want
            target_grid_sets = (DmList)filter_grid_sets_based_on_z_bounds(z_lower, z_upper);

        float z_threshold = Dm.world.maxz;
        if (z_upper_parsed > z_threshold && Dm.truthy(crop_map))
        {
            for (float i = z_threshold + 1; i <= z_upper_parsed; i++) //create a new z_level if needed
                Dm.world.incrementMaxZ();
            if (!Dm.truthy(no_changeturf))
                warning("Z-level expansion occurred without no_changeturf set, this may cause problems when /turf/AfterChange is called");
        }

        foreach (datum_grid_set gset in Dm.each<datum_grid_set>(target_grid_sets))
        {
            float true_xcrd = Dm.num(gset.xcrd) + x_relative_to_absolute;

            // any cutoff of x means we just shouldn't iterate this gridset
            if (final_x < true_xcrd || lowest_x > Dm.num(gset.xcrd))
                continue;

            float zcrd = Dm.num(gset.zcrd) + grid_z_offset;
            // If we're using changeturf, we disable it if we load into a z level we JUST created
            object no_afterchange = Dm.truthy(no_changeturf) || zcrd > z_threshold ? TRUE : FALSE;

            // We're gonna track the first and last pairs of coords we find
            // Since x is always incremented in steps of 1, we only need to deal in y
            // The first x is guarenteed to be the lowest, the first y the highest, and vis versa
            // This is faster then doing mins and maxes inside the hot loop below
            bool first_found = false;
            float first_y = 0;
            float last_y = 0;

            float ycrd = highest_y;
            var gridLines = (DmList)gset.gridLines;
            // Everything following this line is VERY hot.
            for (float i = 1 + y_starting_skip, i__end = line_count - y_ending_skip; i <= i__end; i++)
            {
                if (Dm.eq(gridLines[i], space_key) && Dm.truthy(no_afterchange))
                {
                    // tg: #ifdef TESTING ++turfsSkipped #endif
                    ycrd--;
                    await MAPLOADING_CHECK_TICK();
                    continue;
                }

                var cache = modelCache[gridLines[i]] as DmList;
                if (cache == null)
                {
                    SSatoms.map_loader_stop(REF(this));
                    throw Dm.CRASH(Dm.text("Undefined model key in DMM: []", gridLines[i]), "code/modules/mapping/reader.dm", 503);
                }
                await build_coordinate(cache, locate(true_xcrd, ycrd, zcrd), no_afterchange, place_on_top, new_z);

                // only bother with bounds that actually exist
                if (!first_found)
                {
                    first_found = true;
                    first_y = ycrd;
                }
                last_y = ycrd;
                ycrd--;
                await MAPLOADING_CHECK_TICK();
            }

            // The x coord never changes, so not tracking first x is safe
            // If no ycrd is found, we assume this row is totally empty and just continue on
            if (first_found)
            {
                bounds[MAP_MINX] = Math.Min(Dm.num(bounds[MAP_MINX]), true_xcrd);
                bounds[MAP_MINY] = Math.Min(Dm.num(bounds[MAP_MINY]), last_y);
                bounds[MAP_MINZ] = Math.Min(Dm.num(bounds[MAP_MINZ]), zcrd);
                bounds[MAP_MAXX] = Math.Max(Dm.num(bounds[MAP_MAXX]), true_xcrd);
                bounds[MAP_MAXY] = Math.Max(Dm.num(bounds[MAP_MAXY]), first_y);
                bounds[MAP_MAXZ] = Math.Max(Dm.num(bounds[MAP_MAXZ]), zcrd);
            }
        }
        return TRUE;
    }

    /// Stanrdard loading, not used in production
    /// Doesn't take advantage of any tgm optimizations, which makes it slower but also more general
    /// Use this if for some reason your map format is messy
    // tg: code/modules/mapping/reader.dm:526
    public virtual async DmTask<object> _dmm_load(object x_offset = null, object y_offset = null, object z_offset = null, object crop_map = null, object no_changeturf = null, object x_lower = null, object x_upper = null, object y_lower = null, object y_upper = null, object z_lower = null, object z_upper = null, object place_on_top = null, object new_z = null)
    {
        // ENGINE: the engine reader always gives the TGM shape (map_format is MAP_TGM), so the row-wise DMM loader is never
        // reached: a .dmm in DMM syntax is converted to our format first (tools/Anomaly.Tools.Maps reads both syntaxes).
        Dm.Deferred("code/modules/mapping/reader.dm:526", "_dmm_load: unreachable, the engine reader gives the TGM shape (DMM syntax is the converter's)");
        await DmTask.CompletedTask;
        return FALSE;
    }

    // tg: code/modules/mapping/reader.dm:682
    public virtual object build_cache(object no_changeturf = null, object bad_paths = null)
    {
        if (Dm.eq(map_format, MAP_TGM))
            return tgm_build_cache(no_changeturf, bad_paths);
        return dmm_build_cache(no_changeturf, bad_paths);
    }

    // tg: code/modules/mapping/reader.dm:687
    public virtual object tgm_build_cache(object no_changeturf = null, object bad_paths = null)
    {
        if (modelCache != null && !Dm.truthy(bad_paths))
            return modelCache;
        DmList dot = modelCache = Dm.list();
        DmList grid_models = this.grid_models;
        bool set_space = false;
        // Use where a list is needed, but where it will not be modified
        // Used here to remove the cost of needing to make a new list for each fields entry when it's set manually later
        DmList default_list = GLOB.map_model_default; // It's stupid, but it saves += list(list)

        foreach (object model_key in Dm.each(grid_models))
        {
            // ENGINE: tg splits the model's text into lines and reads each path and var edit with var_edits_tgm and
            // parse_constant (:709-772); the engine reader has parsed them (MapModel's members and MapValues).
            MapModel model = dm_models[(string)model_key];
            //will contain all members (paths) in model (in our example : /turf/unsimulated/wall)
            DmList members = Dm.list();
            //will contain lists filled with corresponding variables, if any (in our example : list(icon_state = "rock") and list())
            DmList members_attributes = Dm.list();

            foreach (MapMember member in model.Members)
            {
                MAPLOADING_CHECK_TICK_SYNC();
                if (member.Overrides.Count > 0)
                {
                    DmList current_attributes = Dm.list(); // Init the list we'll be filling
                    members_attributes.Add(Dm.list(current_attributes));
                    foreach (MapOverride edit in member.Overrides)
                    {
                        object value = edit.Value.ToDm();
                        if (istext(value))
                            value = dm_apply_text_macros((string)value);
                        current_attributes[edit.Var] = value;
                    }
                }
                else
                {
                    members_attributes.Add(Dm.list(default_list)); // We know this is a path, and we also know it has no vv's. so we'll just set this to the default list
                }

                // Alright, if we've gotten to this point, our string is a path
                Type atom_def = text2path(member.Path); //path definition, e.g /obj/foo/bar

                if (!ispath(atom_def, typeof(atom))) // Skip the item if the path does not exist.  Fix your crap, mappers!
                {
                    if (Dm.truthy(bad_paths))
                    {
                        // Rare case, avoid the var to save time most of the time
                        // LAZYOR(bad_paths[path], model_key) (__DEFINES/_lists.dm), expanded inline
                        var bad = (DmList)bad_paths;
                        if (bad[member.Path] is not DmList keys) bad[member.Path] = keys = Dm.list();
                        if (!Dm.@in(model_key, keys)) keys.Add(model_key);
                    }
                    continue;
                }
                // Index is already incremented either way, just gotta set the path and all
                Dm.append(ref members, atom_def);
            }

            //check and see if we can just skip this turf
            //So you don't have to understand this horrid statement, we can do this if
            // 1. the space_key isn't set yet
            // 2. no_changeturf is set
            // 3. there are exactly 2 members
            // 4. with no attributes
            // 5. and the members are world.turf and world.area
            // Basically, if we find an entry like this: "XXX" = (/turf/default, /area/default)
            // We can skip calling this proc every time we see XXX
            if (!set_space
                && Dm.truthy(no_changeturf)
                && members_attributes.len == 2
                && members.len == 2
                && ReferenceEquals(members_attributes[1], default_list)
                && ReferenceEquals(members_attributes[2], default_list)
                && Dm.eq(members[2], Dm.world.area)
                && Dm.eq(members[1], Dm.world.turf)
            )
            {
                set_space = true;
                dot[SPACE_KEY] = model_key;
                continue;
            }

            dot[model_key] = Dm.list(members, members_attributes);
        }
        return dot;
    }

    // MAPLOADING_CHECK_TICK in a synchronous proc: TICK_CHECK is false with the virtual tick usage (DV3), so it never sleeps.
    private void MAPLOADING_CHECK_TICK_SYNC() => Waiting.NoSleep(MAPLOADING_CHECK_TICK());

    /// Builds key caches for general formats
    /// Slower then the proc above, tho it could still be optimized slightly. it's just not a priority
    /// Since we don't run DMM maps, ever.
    // tg: code/modules/mapping/reader.dm:813
    public virtual object dmm_build_cache(object no_changeturf = null, object bad_paths = null)
    {
        // ENGINE: the model texts are the engine reader's in either syntax (map_format is MAP_TGM), so the DMM cache is
        // the TGM one.
        return tgm_build_cache(no_changeturf, bad_paths);
    }

    // tg: code/modules/mapping/reader.dm:892
    public virtual async DmTask build_coordinate(DmList model = null, turf crds = null, object no_changeturf = null, object placeOnTop = null, object new_z = null)
    {
        // If we don't have a turf, nothing we will do next will actually acomplish anything, so just go back
        // Note, this would actually drop area vvs in the tile, but like, why tho
        if (crds == null)
            return;
        float index;
        var members = (DmList)model[1];
        var members_attributes = (DmList)model[2];

        // We use static lists here because it's cheaper then passing them around
        DmList default_list = GLOB.map_model_default;
        ////////////////
        //Instanciation
        ////////////////

        if (Dm.truthy(turf_blacklist?[crds]))
            return;

        //The next part of the code assumes there's ALWAYS an /area AND a /turf on a given tile
        //first instance the /area and remove it from the members list
        index = members.len;
        area old_area = null;
        area listed_area = null; // TURF-IDENTITY: the area_instance below, for the list fix after the turf is replaced
        float cell_z = crds.z;
        if (!Dm.eq(members[index], typeof(area_template_noop)))
        {
            if (!ReferenceEquals(members_attributes[index], default_list))
                Dm.world.preloader_setup((DmList)members_attributes[index], members[index]);//preloader for assigning  set variables on atom creation
            var area_instance = loaded_areas[members[index]] as area;
            if (area_instance == null)
            {
                var area_type = (Type)members[index];
                // If this parsed map doesn't have that area already, we check the global cache
                area_instance = GLOB.areas_by_type[area_type] as area;
                // If the global list DOESN'T have this area it's either not a unique area, or it just hasn't been created yet
                if (area_instance == null)
                {
                    area_instance = (area)Dm.@new(area_type, (object)null);
                    if (area_instance == null)
                        throw Dm.CRASH(Dm.text("[] failed to be new'd, what'd you do?", area_type), "code/modules/mapping/reader.dm", 925);
                }
                loaded_areas[area_type] = area_instance;
            }

            if (!Dm.truthy(new_z))
            {
                old_area = (area)crds.loc;
                // LISTASSERTLEN(old_area.turfs_to_uncontain_by_zlevel, crds.z, list()) (__HELPERS/_lists.dm:114), expanded inline
                dm_list_assert_len(old_area.turfs_to_uncontain_by_zlevel, crds.z);
                dm_list_assert_len(area_instance.turfs_by_zlevel, crds.z);
                Dm.append_at(old_area.turfs_to_uncontain_by_zlevel, crds.z, crds);
                Dm.append_at(area_instance.turfs_by_zlevel, crds.z, crds);
            }
            area_instance.contents.Add(crds);
            listed_area = area_instance;
            if (old_area != null && !Dm.truthy(old_area.has_contained_turfs()))
                qdel(old_area);

            if (Dm.truthy(GLOB.use_preloader))
                Dm.world.preloader_load(area_instance);
        }

        // Index right before /area is /turf
        index--;
        atom instance = null;
        //then instance the /turf
        //NOTE: this used to place any turfs before the last "underneath" it using .appearance and underlays
        //We don't actually use this, and all it did was cost cpu, so we don't do this anymore
        if (!Dm.eq(members[index], typeof(turf_template_noop)))
        {
            if (!ReferenceEquals(members_attributes[index], default_list))
                Dm.world.preloader_setup((DmList)members_attributes[index], members[index]);

            // Note: we make the assertion that the last path WILL be a turf. if it isn't, this will fail.
            if (Dm.truthy(placeOnTop))
            {
                // M10 slice G2: the templates' (shuttles, ruins) way, on top of the turf stack already there
                instance = await crds.load_on_top((Type)members[index], Dm.bitor(CHANGETURF_DEFER_CHANGE, Dm.truthy(no_changeturf) ? CHANGETURF_SKIP : NONE)) as atom;
            }
            else if (Dm.truthy(no_changeturf))
            {
                instance = create_atom(members[index], crds) as atom;//first preloader pass
            }
            else
            {
                instance = await crds.ChangeTurf(members[index], null, CHANGETURF_DEFER_CHANGE) as atom;
            }

            if (Dm.truthy(GLOB.use_preloader) && instance != null)//second preloader pass, for those atoms that don't ..() in New()
                Dm.world.preloader_load(instance);
            // TURF-IDENTITY: crds names its location's turf in tg, the new one from here on
            if (instance is turf replacement)
            {
                // The area lists that took crds above (:928-932), and the old area's own listing of it, name the cell's turf in
                // tg, which the replacement now is; the port's lists hold the replaced object, so they get the new turf here
                // (object-model.md 7.1; the station path's new_z skips the first, build_area_turfs fills the rest after the load).
                if (!Dm.truthy(new_z) && !ReferenceEquals(replacement, crds))
                {
                    dm_replace_listed_turf(old_area, cell_z, crds, replacement);
                    dm_replace_listed_turf(listed_area, cell_z, crds, replacement);
                }
                crds = replacement;
            }
        }
        // If this isn't template work, we didn't change our turf and we changed area, then we've gotta handle area lighting transfer
        else if (!Dm.truthy(no_changeturf) && old_area != null)
        {
            // Don't do contain/uncontain stuff, this happens a few lines up when the area actally changes
            crds.on_change_area(old_area, (area)crds.loc);
        }
        await MAPLOADING_CHECK_TICK();

        //finally instance all remainings objects/mobs
        for (float atom_index = 1, atom_index__end = index - 1; atom_index <= atom_index__end; atom_index++)
        {
            if (!ReferenceEquals(members_attributes[atom_index], default_list))
                Dm.world.preloader_setup((DmList)members_attributes[atom_index], members[atom_index]);

            // We make the assertion that only /atom s will be in this portion of the code. if that isn't true, this will fail
            instance = create_atom(members[atom_index], crds) as atom;//first preloader pass

            if (Dm.truthy(GLOB.use_preloader) && instance != null)//second preloader pass, for those atoms that don't ..() in New()
                Dm.world.preloader_load(instance);
            await MAPLOADING_CHECK_TICK();
        }
    }

    // TURF-IDENTITY: swaps a replaced turf for its replacement in the nested turf lists of an area (both of its lists hold the
    // turfs of the cell's z-level in the list at that z).
    private static void dm_replace_listed_turf(area a, float z, turf old, turf replacement)
    {
        if (a == null)
            return;
        foreach (DmList by_zlevel in new[] { a.turfs_by_zlevel, a.turfs_to_uncontain_by_zlevel })
        {
            if (by_zlevel == null || length(by_zlevel) < z)
                continue;
            if (by_zlevel[z] is not DmList turfs)
                continue;
            for (float i = 1; i <= turfs.len; i++)
            {
                if (ReferenceEquals(turfs[i], old))
                    turfs[i] = replacement;
            }
        }
    }

    // LISTASSERTLEN(list, index, list()) (__HELPERS/_lists.dm:114), expanded as turf.cs's change_area expands it inline.
    private static void dm_list_assert_len(DmList list, float index)
    {
        if (length(list) < index)
        {
            float _OLD_LENGTH = length(list);
            list.len = index;
            for (float _INDEX_TO_ASSIGN_TO = _OLD_LENGTH + 1; _INDEX_TO_ASSIGN_TO <= index; _INDEX_TO_ASSIGN_TO++)
                list[_INDEX_TO_ASSIGN_TO] = Dm.list();
        }
    }

    ////////////////
    //Helpers procs
    ////////////////

    // tg: code/modules/mapping/reader.dm:984
    public virtual object create_atom(object path = null, object crds = null)
    {
        // set waitfor = FALSE: new runs the atom's New; a New that sleeps is detached by the shim's new (rules 6.8)
        return Dm.@new((Type)path, crds);
    }

    //find the position of the next delimiter,skipping whatever is comprised between opening_escape and closing_escape
    //returns 0 if reached the last delimiter
    // tg: code/modules/mapping/reader.dm:990
    public virtual object find_next_delimiter_position(object text = null, object initial_position = null, string delimiter = null, string opening_escape = null, string closing_escape = null)
    {
        delimiter ??= ",";
        opening_escape ??= "\"";
        closing_escape ??= "\"";
        float position = Dm.num(initial_position);
        float next_delimiter = findtext(text, delimiter, position, 0);
        float next_opening = findtext(text, opening_escape, position, 0);

        while ((next_opening != 0) && (next_opening < next_delimiter))
        {
            position = findtext(text, closing_escape, next_opening + 1, 0) + 1;
            next_delimiter = findtext(text, delimiter, position, 0);
            next_opening = findtext(text, opening_escape, position, 0);
        }

        return next_delimiter;
    }

    //build a list from variables in text form (e.g {var1="derp"; var2; var3=7} => list(var1="derp", var2, var3=7))
    //return the filled list
    // tg: code/modules/mapping/reader.dm:1004
    public virtual object readlist(object text = null, string delimiter = null)
    {
        delimiter ??= ",";
        DmList dot = Dm.list();
        if (!Dm.truthy(text))
            return dot;
        // ENGINE: tg's readlist scans the DM text with find_next_delimiter_position; the engine reader's list parsing
        // (MapTextReader, map-format.md 4.4 rule 4) splits it outside strings and parentheses, with an item's "=" inside the
        // item, and parse_constant reads each side.
        foreach (string item in MapTextReader.SplitOutside((string)text, delimiter, (string)text))
        {
            string trimmed = item.Trim();
            if (trimmed.Length == 0) continue;
            int equal = MapTextReader.IndexOutside(trimmed, "=", 0, trimmed);
            if (equal < 0)
            {
                dot.Add(Dm.list(parse_constant(trimmed)));
                continue;
            }
            dot[parse_constant(trimmed.Substring(0, equal).Trim())] = parse_constant(trimmed.Substring(equal + 1).Trim());
        }
        return dot;
    }

    // tg: code/modules/mapping/reader.dm:1063
    public virtual object parse_constant(object text = null)
    {
        // ENGINE: tg's parse_constant over DM text, in its order (empty, number, string, list, typepath, file, null, the
        // text itself), is the engine reader's MapValue (map-format.md 4.4); its DM value is MapValue.ToDm.
        return MapValue.Parse((string)text ?? "").ToDm();
    }

    // tg: code/modules/mapping/reader.dm:1104
    public override object Destroy(float? force = null)
    {
        base.Destroy(force);
        SSatoms.map_loader_stop(REF(this)); // Just in case, I don't want to double up here
        if (Dm.truthy(turf_blacklist))
            turf_blacklist.Cut();
        parsed_bounds?.Cut();
        bounds?.Cut();
        grid_models.Cut();
        gridSets.Cut();
        return QDEL_HINT_HARDDEL_NOW;
    }

    // LOCAL COPY (M3 P2) of tg's /proc/apply_text_macros (code/__HELPERS/text.dm:797), which tgm_build_cache applies to
    // every text value of a map: the __HELPERS/text.dm mirror is not this package's (rules 11.1, I1's cleanup moves it).
    // The text macro of a map value ("\improper Bob") becomes DM's runtime text macro, as tg's `"\improper [rest]"`.
    // tg: code/__HELPERS/text.dm:797
    internal static string dm_apply_text_macros(string @string)
    {
        float next_backslash = findtext(@string, "\\");
        if (!Dm.truthy(next_backslash))
            return @string;

        float leng = length(@string);

        float next_space = findtext(@string, " ", next_backslash + 1);
        if (!Dm.truthy(next_space))
            next_space = leng - next_backslash;

        if (!Dm.truthy(next_space)) //trailing bs
            return @string;

        string @base = next_backslash == 1 ? "" : copytext(@string, 1, next_backslash);
        string macro = lowertext(copytext(@string, next_backslash + 1, next_space));
        string rest = next_backslash > leng ? "" : copytext(@string, next_space + 1);

        //See https://secure.byond.com/docs/ref/info.html#/DM/text/macros
        switch (macro)
        {
            //prefixes/agnostic
            case "the": rest = Dm.text("\\the []", rest); break;
            case "a": rest = Dm.text("\\a []", rest); break;
            case "an": rest = Dm.text("\\an []", rest); break;
            case "proper": rest = Dm.text("\\proper []", rest); break;
            case "improper": rest = Dm.text("\\improper []", rest); break;
            case "roman": rest = Dm.text("\\roman []", rest); break;
            //postfixes
            case "th": @base = Dm.text("[]\\th", rest); break;
            case "s": @base = Dm.text("[]\\s", rest); break;
            case "he": @base = Dm.text("[]\\he", rest); break;
            case "she": @base = Dm.text("[]\\she", rest); break;
            case "his": @base = Dm.text("[]\\his", rest); break;
            case "himself": @base = Dm.text("[]\\himself", rest); break;
            case "herself": @base = Dm.text("[]\\herself", rest); break;
            case "hers": @base = Dm.text("[]\\hers", rest); break;
            default: // Someone fucked up, if you're not a macro just go home yeah?
                // This does technically break parsing, but at least it's better then what it used to do
                return @base;
        }

        string dot = @base;
        if (Dm.truthy(rest))
            dot += dm_apply_text_macros(rest);
        return dot;
    }
}
