// Translated from tgstation (snapshot 1d99bd7f): code/datums/map_config.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 P2: the map config, translated in full but the map vote's and the unit tests' parts (UNIT_TESTS is not defined in
// production). The station path reads it from SSmapping's PreInit (controllers/subsystem/mapping.cs); the map files it
// names are the host's converted maps, mounted at tg's paths (MapSeam.Select, docs/ai/specs/map-format.md 5).

// tg: code/datums/map_config.dm:81  (the global procs, rules 7.8)
public static partial class GlobalProcs
{
    /**
     * Proc that simply loads the default map config, which should always be functional.
     */
    // tg: code/datums/map_config.dm:81
    public static object load_default_map_config()
    {
        return Dm.@new<datum_map_config>();
    }

    /**
     * Proc handling the loading of map configs. Will return the default map config using [/proc/load_default_map_config] if the loading of said file fails for any reason whatsoever, so we always have a working map for the server to run.
     */
    // tg: code/datums/map_config.dm:94
    public static object load_map_config(object filename = null, object directory = null, float? error_if_missing = null)
    {
        error_if_missing ??= TRUE;
        var configuring_map = (datum_map_config)load_default_map_config();

        if (Dm.truthy(filename)) // If none is specified, then go to look for next_map.json, for map rotation purposes.
        {
            //Default to MAP_DIRECTORY_MAPS if no directory is passed
            if (Dm.truthy(directory))
            {
                // MAP_DIRECTORY_WHITELIST (__DEFINES/maps.dm:69), expanded inline (rules 11.1 (e))
                if (!Dm.@in(directory, Dm.list(MAP_DIRECTORY_MAPS, MAP_DIRECTORY_DATA)))
                {
                    log_world(Dm.text("map directory not in whitelist: [] for map []", directory, filename));
                    return configuring_map;
                }
            }
            else
            {
                directory = MAP_DIRECTORY_MAPS;
            }

            filename = Dm.text("[]/[].json", directory, filename);
        }
        else
        {
            filename = PATH_TO_NEXT_MAP_JSON;
        }

        if (!Dm.truthy(configuring_map.LoadConfig(filename, error_if_missing)))
        {
            qdel(configuring_map);
            return load_default_map_config();
        }
        return configuring_map;
    }
}

// tg: code/datums/map_config.dm:120
public partial class datum_map_config : datum
{
    /// <summary>DEVIATION(DV583): the map's deathmatch settings, the json's <c>deathmatch</c> object (docs/ai/specs/deathmatch); null on a map without one.</summary>
    public DmList deathmatch;

    // tg: code/datums/map_config.dm:120
    public virtual object LoadConfig(object filename = null, object error_if_missing = null)
    {
        if (!Dm.truthy(fexists(filename)))
        {
            if (Dm.truthy(error_if_missing))
                log_world(Dm.text("map_config not found: []", filename));
            return null;
        }

        object json = file(filename);
        if (!Dm.truthy(json))
        {
            log_world(Dm.text("Could not open map_config: []", filename));
            return null;
        }

        json = file2text(json);
        if (!Dm.truthy(json))
        {
            log_world(Dm.text("map_config is not text: []", filename));
            return null;
        }

        json = json_decode(json);
        if (!Dm.truthy(json))
        {
            log_world(Dm.text("map_config is not json: []", filename));
            return null;
        }
        var J = (DmList)json;

        config_filename = (string)filename;

        if (!Dm.truthy(J["version"]))
        {
            log_world("map_config missing version!");
            return null;
        }

        if (!Dm.eq(J["version"], MAP_CURRENT_VERSION))
        {
            log_world(Dm.text("map_config has invalid version []!", J["version"]));
            return null;
        }

        // CHECK_EXISTS(X) (:118), expanded inline (rules 11.1 (e))
        if (!istext(J["map_name"])) { log_world("\"map_name\" missing from json!"); return null; }
        map_name = (string)J["map_name"];
        if (!istext(J["map_path"])) { log_world("\"map_path\" missing from json!"); return null; }
        map_path = (string)J["map_path"];

        object map_file = J["map_file"];
        // "map_file": "MetaStation.dmm"
        if (istext(map_file))
        {
            if (!Dm.truthy(fexists(Dm.text("_maps/[]/[]", map_path, map_file))))
            {
                log_world(Dm.text("Map file ([]/[]) does not exist!", map_path, map_file));
                return null;
            }
        }
        // "map_file": ["Lower.dmm", "Upper.dmm"]
        else if (islist(map_file))
        {
            foreach (object file in Dm.each(map_file))
            {
                if (!Dm.truthy(fexists(Dm.text("_maps/[]/[]", map_path, file))))
                {
                    log_world(Dm.text("Map file ([]/[]) does not exist!", map_path, file));
                    return null;
                }
            }
            // PORT: the generated var is a string (the converter types map_file by its default, "MetaStation.dmm"); a
            // list of files is a multi-z map, which D108 does not load (Meta's station level only).
            log_world(Dm.text("map_config [] names several map files: multi-z maps are not loaded in M3 (D108)", filename));
            return null;
        }
        else
        {
            log_world("map_file missing from json!");
            return null;
        }
        this.map_file = (string)map_file;

        if (islist(J["shuttles"]))
        {
            var L = (DmList)J["shuttles"];
            foreach (object key in Dm.each(L))
            {
                object value = L[key];
                shuttles[key] = value;
            }
        }
        else if (Dm.@in("shuttles", J))
        {
            log_world("map_config shuttles is not a list!");
            return null;
        }

        traits = J["traits"];
        // "traits": [{"Linkage": "Cross"}, {"Space Ruins": true}]
        if (islist(traits))
        {
            // "Station" is set by default, but it's assumed if you're setting
            // traits you want to customize which level is cross-linked
            foreach (object level in Dm.each(traits))
            {
                if (!Dm.@in(ZTRAIT_STATION, level))
                    ((DmList)level)[ZTRAIT_STATION] = TRUE;
            }
        }
        // "traits": null or absent -> default
        else if ((traits != null))
        {
            log_world("map_config traits is not a list!");
            return null;
        }

        object temp = J["space_ruin_levels"];
        if (isnum(temp))
            space_ruin_levels = Dm.num(temp);
        else if ((temp != null))
        {
            log_world("map_config space_ruin_levels is not a number!");
            return null;
        }

        temp = J["space_empty_levels"];
        if (isnum(temp))
            space_empty_levels = Dm.num(temp);
        else if ((temp != null))
        {
            log_world("map_config space_empty_levels is not a number!");
            return null;
        }

        temp = J["wilderness_levels"];
        if (isnum(temp))
            wilderness_levels = Dm.num(temp);
        else if ((temp != null))
        {
            log_world("map_config wilderness_levels is not a number!");
            return null;
        }

        if (Dm.@in("minetype", J))
            minetype = (string)J["minetype"];

        if (Dm.@in("planetary", J))
            planetary = Dm.num(J["planetary"]);

        if (Dm.@in("blacklist_file", J))
            blacklist_file = J["blacklist_file"];

        if (Dm.@in("load_all_away_missions", J))
            load_all_away_missions = Dm.num(J["load_all_away_missions"]);

        if (Dm.@in("give_players_hooks", J))
            give_players_hooks = Dm.num(J["give_players_hooks"]);

        if (Dm.@in("bonus_weakpoints", J))
            bonus_weakpoints = Dm.num(J["bonus_weakpoints"]);

        // DEVIATION(DV583): the port's own key
        if (islist(J["deathmatch"]))
            deathmatch = (DmList)J["deathmatch"];
        else if (Dm.@in("deathmatch", J))
        {
            log_world("map_config deathmatch is not an object!");
            return null;
        }

        allow_custom_shuttles = !Dm.eq(J["allow_custom_shuttles"], FALSE) ? 1 : 0;

        if (Dm.@in("job_changes", J))
        {
            if (!islist(J["job_changes"]))
            {
                log_world("map_config \"job_changes\" field is missing or invalid!");
                return null;
            }
            job_changes = (DmList)J["job_changes"];
        }

        if (Dm.@in("library_areas", J))
        {
            if (!islist(J["library_areas"]))
            {
                log_world("map_config \"library_areas\" field is missing or invalid!");
                return null;
            }
            foreach (object path_as_text in Dm.each(J["library_areas"]))
            {
                Type path = text2path((string)path_as_text);
                if (!ispath(path, typeof(area)))
                {
                    stack_trace(Dm.text("Invalid path in mapping config for additional library areas: \\[[]\\]", path_as_text), "code/datums/map_config.dm", 248);
                    continue;
                }
                Dm.append(ref library_areas, path);
            }
        }

        if (Dm.@in("height_autosetup", J))
            height_autosetup = Dm.num(J["height_autosetup"]);

        DmList wilderness = J["wilderness"] as DmList;
        // If we got wilderness levels, fetch them from the config
        if (islist(wilderness))
        {
            wilderness_directory = wilderness["directory"];
            wilderness.Remove("directory");

            // Just pick and take based on weight
            for (float i = 1, i__end = wilderness_levels; i <= i__end; i++)
                Dm.append(ref wilderness_maps_to_spawn, pick_weight_take(wilderness));
            shuffle_inplace(wilderness_maps_to_spawn);
        }

        DmList wilderness_level_traits = J["wilderness_level_traits"] as DmList;
        if (islist(wilderness_level_traits))
            wilderness_z_traits = wilderness_level_traits;

        // tg: #ifdef UNIT_TESTS ... #endif not translated (inactive in production)

        defaulted = FALSE;
        return json;
    }

    // tg: code/datums/map_config.dm:288
    public virtual object GetFullMapPaths()
    {
        if (istext(map_file))
            return Dm.list(Dm.text("_maps/[]/[]", map_path, map_file));
        DmList dot = Dm.list();
        foreach (object file in Dm.each(map_file))
            Dm.append(ref dot, Dm.text("_maps/[]/[]", map_path, file));
        return dot;
    }

    // tg: code/datums/map_config.dm:295
    public virtual object MakeNextMap()
    {
        return Dm.truthy(config_filename == PATH_TO_NEXT_MAP_JSON) || Dm.truthy(fcopy(config_filename, PATH_TO_NEXT_MAP_JSON)) ? TRUE : FALSE;
    }
}
