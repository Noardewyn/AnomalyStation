// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/persistent_paintings.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI4d (D326): the whole file. The paintings' json and PNG files live in the world's data folder through its file seam (a
// server with real files keeps them between rounds, as tg); the PNGs are the server's own (DmPng, rust-g's dmi_create_png). The var
// blocks are generated.

// tg: code/controllers/subsystem/persistent_paintings.dm:1  #define PAINTINGS_DATA_FORMAT_VERSION 3 (file-local, #undef at :356)
public partial class datum_controller_subsystem_persistent_paintings
{
    private const int PAINTINGS_DATA_FORMAT_VERSION = 3;
}

// tg: code/controllers/subsystem/persistent_paintings.dm:27
public partial class datum_painting : datum
{
    // tg: code/controllers/subsystem/persistent_paintings.dm:59
    public virtual void load_from_json(DmList json_data = null)
    {
        md5 = json_data["md5"];
        title = json_data["title"];
        creator_ckey = json_data["creator_ckey"];
        creator_name = json_data["creator_name"];
        creation_date = json_data["creation_date"];
        creation_round_id = json_data["creation_round_id"];
        tags = json_data["tags"] as DmList;
        patron_ckey = json_data["patron_ckey"];
        patron_name = json_data["patron_name"];
        credit_value = Dm.num(json_data["credit_value"]);
        width = json_data["width"];
        height = json_data["height"];
        medium = json_data["medium"];
        frame_type = Dm.str(json_data["frame_type"]);
        loaded_from_json = TRUE;
    }

    // tg: code/controllers/subsystem/persistent_paintings.dm:76
    public virtual DmList to_json()
    {
        DmList new_data = Dm.list();
        new_data["md5"] = md5;
        new_data["title"] = title;
        new_data["creator_ckey"] = creator_ckey;
        new_data["creator_name"] = creator_name;
        new_data["creation_date"] = creation_date;
        new_data["creation_round_id"] = creation_round_id;
        new_data["tags"] = tags;
        new_data["patron_ckey"] = patron_ckey;
        new_data["patron_name"] = patron_name;
        new_data["credit_value"] = credit_value;
        new_data["width"] = width;
        new_data["height"] = height;
        new_data["medium"] = medium;
        new_data["frame_type"] = frame_type;
        return new_data;
    }

    // tg: code/controllers/subsystem/persistent_paintings.dm:94
    public virtual icon get_icon()
    {
        return Dm.icon(Dm.text("data/paintings/images/[].png", md5));
    }

    // tg: code/controllers/subsystem/persistent_paintings.dm:97
    public virtual obj_item_canvas spawn_canvas(atom spawn_loc = null)
    {
        icon art_icon = get_icon();
        float art_width = art_icon.Width();
        float art_height = art_icon.Height();
        obj_item_canvas printed_canvas = null;
        foreach (Type canvas_type in Dm.each_cast<Type>(typesof(typeof(obj_item_canvas))))
        {
            if (Dm.eq(Dm.initial(canvas_type, (obj_item_canvas c) => c.width), art_width) && Dm.eq(Dm.initial(canvas_type, (obj_item_canvas c) => c.height), art_height))
                printed_canvas = Dm.@new(canvas_type, spawn_loc) as obj_item_canvas;
        }
        if (printed_canvas == null)
            return null;
        fill_canvas(printed_canvas, art_icon);
        return printed_canvas;
    }

    // tg: code/controllers/subsystem/persistent_paintings.dm:110
    public virtual void fill_canvas(obj_item_canvas canvas = null, icon icon = null)
    {
        icon ??= get_icon();
        canvas.painting_metadata = this;
        canvas.fill_grid_from_icon(icon);
        canvas.generated_icon = icon;
        canvas.icon_generated = TRUE;
        canvas.finalized = TRUE;
        canvas.name = Dm.text("painting - []", title);
        ///this is a copy of something that is already in the database- it should not be able to be saved.
        canvas.no_save = TRUE;
        Waiting.NoSleep(canvas.update_icon());
    }
}

// tg: code/controllers/subsystem/persistent_paintings.dm:122 SUBSYSTEM_DEF(persistent_paintings)
public partial class datum_controller_subsystem_persistent_paintings : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(persistent_paintings) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSpersistent_paintings)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/persistent_paintings.dm:122
    public override async DmTask New(params object[] __extra)
    {
        if (SSpersistent_paintings != this)
        {
            if (SSpersistent_paintings is datum_controller_subsystem_persistent_paintings)
            {
                Recover();
                qdel(SSpersistent_paintings);
            }
            SSpersistent_paintings = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/persistent_paintings.dm:158
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        DmFileRef json_file = Dm.file("data/paintings.json");
        if (Dm.truthy(Dm.fexists(json_file)))
        {
            DmList raw_data = update_format(json_decode(file2text(json_file)) as DmList);
            foreach (DmList painting_data in Dm.each_cast<DmList>(raw_data["paintings"] as DmList))
            {
                var loaded_painting = Dm.@new<datum_painting>();
                loaded_painting.load_from_json(painting_data);
                paintings.Add(loaded_painting);
            }
        }

        foreach (obj_structure_sign_painting painting_frame in Dm.each_cast<obj_structure_sign_painting>(painting_frames))
            painting_frame.load_persistent();

        cache_paintings();

        return SS_INIT_SUCCESS;
    }

    // tg: code/controllers/subsystem/persistent_paintings.dm:174
    public virtual void cache_paintings()
    {
        cached_painting_data = Dm.list();
        admin_painting_data = Dm.list();

        foreach (datum_painting painting in Dm.each_cast<datum_painting>(paintings))
        {
            cached_painting_data.Add(Dm.list(Dm.list(
                Dm.kv("title", painting.title),
                Dm.kv("creator", painting.creator_name),
                Dm.kv("md5", painting.md5),
                Dm.kv("ref", REF(painting)),
                Dm.kv("width", painting.width),
                Dm.kv("height", painting.height),
                Dm.kv("ratio", Dm.num(painting.width) / Dm.num(painting.height))
            )));

            DmList pdata = painting.to_json();
            pdata["ref"] = REF(painting);
            admin_painting_data.Add(Dm.list(pdata)); // UNTYPED_LIST_ADD
        }
    }

    /**
     * Generates painting data ready to be consumed by ui.
     * Args:
     * * filter: a bitfield argument is used to filter out paintings that don't match certain requisites.
     * * admin : whether all the json data of the painting is added to the return value or only the more IC details
     * * search_text : text to search for if the PAINTINGS_FILTER_SEARCH_TITLE or PAINTINGS_FILTER_SEARCH_CREATOR filters are enabled.
     */
    // tg: code/controllers/subsystem/persistent_paintings.dm:200
    public virtual DmList painting_ui_data(float filter = NONE, object admin = null, object search_text = null)
    {
        bool searching = Dm.truthy(Dm.bitand(filter, Dm.bitor(PAINTINGS_FILTER_SEARCH_TITLE, PAINTINGS_FILTER_SEARCH_CREATOR))) && Dm.truthy(search_text);
        DmList paintings = Dm.truthy(admin) ? admin_painting_data : cached_painting_data;

        if (!searching && !Dm.truthy(Dm.bitand(filter, PAINTINGS_FILTER_AI_PORTRAIT)))
            return paintings;

        DmList filtered_paintings = Dm.list();

        foreach (DmList painting in Dm.each_cast<DmList>(paintings))
        {
            if (Dm.truthy(Dm.bitand(filter, PAINTINGS_FILTER_AI_PORTRAIT))
                && ((!Dm.eq(painting["width"], 24) && !Dm.eq(painting["width"], 23)) || (!Dm.eq(painting["height"], 24) && !Dm.eq(painting["height"], 23))))
                continue;
            if (searching)
            {
                object haystack_text = "";
                if (Dm.truthy(Dm.bitand(filter, PAINTINGS_FILTER_SEARCH_TITLE)))
                    haystack_text = painting["title"];
                else if (Dm.truthy(Dm.bitand(filter, PAINTINGS_FILTER_SEARCH_CREATOR)))
                    haystack_text = painting["creator"];
                if (!Dm.truthy(findtext(haystack_text, search_text)))
                    continue;
            }
            filtered_paintings.Add(Dm.list(painting));
        }
        return filtered_paintings;
    }

    /// Returns paintings with given tag.
    // tg: code/controllers/subsystem/persistent_paintings.dm:224
    public virtual DmList get_paintings_with_tag(object tag_name = null)
    {
        DmList dot = Dm.list();
        foreach (datum_painting painting in Dm.each_cast<datum_painting>(paintings))
        {
            if (!Dm.truthy(painting.tags) || !Dm.@in(tag_name, painting.tags))
                continue;
            dot.Add(painting);
        }
        return dot;
    }

    /// Updates paintings data format to latest if necessary
    // tg: code/controllers/subsystem/persistent_paintings.dm:232
    public virtual DmList update_format(DmList current_data = null)
    {
        if (Dm.truthy(current_data["version"]) && Dm.eq(current_data["version"], PAINTINGS_DATA_FORMAT_VERSION))
            return current_data;

        float version = Dm.num(current_data["version"]);
        if (version < 1)
            current_data = migrate_to_version_1(current_data);
        if (version < 2) //Makes sure old paintings get a cosmetic frame type from their patronage tiers.
            current_data = migrate_to_version_2(current_data);
        if (version < 3) //Reduces the allowed length of titles from 1000 characters circa to 42.
        {
            current_data["version"] = 3;
            object old_title = current_data["title"];
            object new_title = reject_bad_name(old_title, allow_numbers: TRUE, ascii_only: FALSE, strict: TRUE, cap_after_symbols: FALSE);
            current_data["title"] = Dm.truthy(new_title) ? new_title : "Illegibly Titled Artwork";
        }

        return current_data;
    }

    // tg: code/controllers/subsystem/persistent_paintings.dm:249
    public virtual DmList migrate_to_version_1(DmList current_data = null)
    {
        Dm.fcopy("data/paintings.json", "data/paintings_migration_backup_0.json"); //Better safe than losing all metadata
        DmList result = Dm.list();
        result["version"] = 1;
        DmList data = Dm.list();
        // Squash categories into tags
        foreach (object category in Dm.each(current_data))
        {
            foreach (DmList old_data in Dm.each_cast<DmList>(current_data[category] as DmList))
            {
                bool duplicate_found = false;
                foreach (DmList entry in Dm.each<DmList>(data))
                {
                    if (Dm.eq(entry["md5"], old_data["md5"]))
                    {
                        var entry_tags = entry["tags"] as DmList;
                        Dm.combine(ref entry_tags, category);
                        entry["tags"] = entry_tags;
                        duplicate_found = true;
                        break;
                    }
                }
                if (duplicate_found)
                    continue;
                string old_png_path = Dm.text("data/paintings/[]/[].png", category, old_data["md5"]);
                string new_png_path = Dm.text("data/paintings/images/[].png", old_data["md5"]);
                Dm.fcopy(old_png_path, new_png_path);
                Dm.fdel(old_png_path);
                icon painting_icon = Dm.icon(new_png_path);
                float width = painting_icon.Width();
                float height = painting_icon.Height();
                DmList new_data = Dm.list();
                new_data["md5"] = old_data["md5"];
                new_data["title"] = Dm.truthy(old_data["title"]) ? old_data["title"] : "Untitled Artwork";
                new_data["creator_ckey"] = Dm.truthy(old_data["ckey"]) ? old_data["ckey"] : "";
                new_data["creator_name"] = "Anonymous";
                new_data["creation_date"] = time2text(world.realtime, "DDD MMM DD hh:mm:ss YYYY", TIMEZONE_UTC); // Could use creation/modified file helpers in rustg
                new_data["creation_round_id"] = GLOB.round_id;
                new_data["tags"] = Dm.list(category, "Migrated from version 0");
                new_data["patron_ckey"] = "";
                new_data["patron_name"] = "";
                new_data["credit_value"] = 0;
                new_data["width"] = width;
                new_data["height"] = height;
                new_data["medium"] = "Spraypaint on canvas"; //Let's go with most common tool.
                data.Add(Dm.list(new_data));
            }
        }
        result["paintings"] = data;
        //We're going to save this immidiately this is non-recoverable operation
        DmFileRef json_file = Dm.file("data/paintings.json");
        Dm.fdel(json_file);
        Dm.output_to(json_file, json_encode(result)); // WRITE_FILE
        return result;
    }

    // tg: code/controllers/subsystem/persistent_paintings.dm:294
    public virtual DmList migrate_to_version_2(DmList current_data = null)
    {
        Dm.fcopy("data/paintings.json", "data/paintings_migration_backup_1.json"); //Better safe than sorry
        current_data["version"] = 2;
        foreach (DmList painting_data in Dm.each<DmList>(current_data["paintings"] as DmList))
        {
            var painting_tags = painting_data["tags"] as DmList ?? Dm.list();
            Dm.combine(ref painting_tags, "Migrated from version 1");
            painting_data["tags"] = painting_tags;
            object credit_value = painting_data["credit_value"];
            DmList possible_frame_types = get_available_frames(credit_value, only_current_tier: TRUE);
            object picked = pick(possible_frame_types);
            painting_data["frame_type"] = Dm.truthy(picked) ? picked : "simple";
        }
        return current_data;
    }

    /**
     * returns a list of cosmetic frames which patronage tier values are are within credit_value.
     * If only_current_tier is TRUE, the list will be populated only by the ones from the highest tier.
     */
    // tg: code/controllers/subsystem/persistent_paintings.dm:308
    public virtual DmList get_available_frames(object credit_value = null, object only_current_tier = null)
    {
        DmList frame_candidates = Dm.list();
        float current_tier_patronage = 0;
        foreach (object frametype in Dm.each(frame_types_by_patronage_tier))
        {
            float required_patronage = Dm.num(frame_types_by_patronage_tier[frametype]);
            if (Dm.num(credit_value) >= required_patronage)
            {
                if (Dm.truthy(only_current_tier) && current_tier_patronage < required_patronage)
                {
                    current_tier_patronage = required_patronage;
                    frame_candidates.Cut();
                }
                frame_candidates.Add(frametype);
            }
        }
        return frame_candidates;
    }

    /// Saves all persistent paintings
    // tg: code/controllers/subsystem/persistent_paintings.dm:321
    public virtual void save_paintings()
    {
        // Collect new painting data
        foreach (obj_structure_sign_painting painting_frame in Dm.each_cast<obj_structure_sign_painting>(painting_frames))
            painting_frame.save_persistent();

        save_to_file();
    }

    /// Saves all currently tracked painting data to file
    // tg: code/controllers/subsystem/persistent_paintings.dm:329
    public virtual void save_to_file()
    {
        DmFileRef json_file = Dm.file("data/paintings.json");

        DmList collated_data = Dm.list();
        if (Dm.truthy(Dm.fexists(json_file)))
        {
            var old_data = json_decode(file2text(json_file)) as DmList;
            foreach (DmList painting_data in Dm.each_cast<DmList>(old_data["paintings"] as DmList))
                collated_data[painting_data["md5"]] = painting_data;
        }

        DmList painting_data_list = Dm.list();
        foreach (datum_painting painting in Dm.each_cast<datum_painting>(paintings))
            collated_data[painting.md5] = painting.to_json(); //Current data has priority over old data

        // Remove deleted paintings from the list
        Dm.remove(ref collated_data, deleted_paintings_md5s);

        // Flatten the resulting list
        foreach (object key in Dm.each(collated_data))
            painting_data_list.Add(Dm.list(collated_data[key]));

        DmList all_data = Dm.list(Dm.kv("version", PAINTINGS_DATA_FORMAT_VERSION));
        all_data["paintings"] = painting_data_list;
        string payload = json_encode(all_data);
        Dm.fdel(json_file);
        Dm.output_to(json_file, payload); // WRITE_FILE
        cache_paintings();
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/persistent_paintings.dm:122  SUBSYSTEM_DEF(persistent_paintings): GLOBAL_REAL(SSpersistent_paintings, ...)
    internal datum_controller_subsystem_persistent_paintings SSpersistent_paintings;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_persistent_paintings SSpersistent_paintings => ref TgState.Current.SSpersistent_paintings;
}
