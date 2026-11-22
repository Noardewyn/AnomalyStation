// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences_savefile.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice F, D267): the savefile's version check, its path, and loading and saving the preferences and the character
// through the json savefile (the world's file seam). Not translated: the version updaters (update_preferences, update_character:
// the port never wrote an older version), the keybinding checks (the port's client binds its own keys), the slot removal (one
// slot, the tgui menu's slot list is not translated). Syndicate A3: sanitize_be_special (the antagonists' preferences).

public partial class datum_preferences : datum
{
    /// Placeholder to check against the SAVE_DATA_EMPTY and SAVE_DATA_OBSOLETE values.
    /// _Any_ generated save data version will be zero or a positive integer, so it's only necessary to check against this value for anything negative (error states).
    // tg: code/modules/client/preferences_savefile.dm:3  #define SAVE_DATA_NO_ERROR 0 (file-local, #undef at :502)
    private const int SAVE_DATA_NO_ERROR = 0;
    /// Typically signifies an empty list, where the savefile is not loaded or the character is new. Will just trigger a regeneration.
    // tg: code/modules/client/preferences_savefile.dm:5  #define SAVE_DATA_EMPTY -1 (file-local, #undef at :503)
    private const int SAVE_DATA_EMPTY = -1;
    /// The save data is below the accepted minimum and should be reset.
    // tg: code/modules/client/preferences_savefile.dm:7  #define SAVE_DATA_OBSOLETE -2 (file-local, #undef at :504)
    private const int SAVE_DATA_OBSOLETE = -2;
    /// This is the lowest supported version, anything below this is completely obsolete and the entire savefile will be wiped.
    // tg: code/modules/client/preferences_savefile.dm:10  #define SAVEFILE_VERSION_MIN 32 (file-local, #undef at :505)
    private const int SAVEFILE_VERSION_MIN = 32;
    /// This is the current version, anything below this will attempt to update (if it's not obsolete)
    // tg: code/modules/client/preferences_savefile.dm:16  #define SAVEFILE_VERSION_MAX 52 (file-local, #undef at :506)
    private const int SAVEFILE_VERSION_MAX = 52;

    // tg: code/modules/client/preferences_savefile.dm:18  #define IS_DATA_OBSOLETE(version) (version == SAVE_DATA_OBSOLETE)
    private static bool IS_DATA_OBSOLETE(object version) => Dm.eq(version, SAVE_DATA_OBSOLETE);
    // tg: code/modules/client/preferences_savefile.dm:19  #define SHOULD_UPDATE_DATA(version) (version >= SAVE_DATA_NO_ERROR && version < SAVEFILE_VERSION_MAX)
    private static bool SHOULD_UPDATE_DATA(object version) => Dm.ge(version, SAVE_DATA_NO_ERROR) && Dm.lt(version, SAVEFILE_VERSION_MAX);

    // tg: code/__DEFINES/preferences.dm:42  #define PREFS_BACKUP_PATH(base_path) "[base_path].updatebac"
    private static string PREFS_BACKUP_PATH(object base_path) => Dm.text("[].updatebac", base_path);

    // tg: code/modules/client/preferences_savefile.dm:37
    public virtual object check_savedata_version(DmList save_data = null)
    {
        if (save_data == null)
            return SAVE_DATA_EMPTY;
        object save_version = save_data["version"];

        if (Dm.lt(save_version, SAVEFILE_VERSION_MIN))
            return SAVE_DATA_OBSOLETE;
        if (Dm.lt(save_version, SAVEFILE_VERSION_MAX))
            return save_version;
        return SAVE_DATA_EMPTY;
    }

    // tg: code/modules/client/preferences_savefile.dm:213
    public virtual void load_path(object ckey = null, object filename = null)
    {
        filename ??= "preferences.json";
        if (!Dm.truthy(ckey) || !Dm.truthy(load_and_save))
            return;
        path = Dm.text("data/player_saves/[]/[]/[]", Dm.textchar(ckey, 1), ckey, filename);
    }

    // tg: code/modules/client/preferences_savefile.dm:218
    public virtual void load_savefile()
    {
        if (Dm.truthy(load_and_save) && !Dm.truthy(path))
            throw Dm.CRASH("Attempted to load savefile without first loading a path!", "code/modules/client/preferences_savefile.dm", 220);
        savefile = Dm.@new<datum_json_savefile>(Dm.truthy(load_and_save) ? path : null);
    }

    // tg: code/modules/client/preferences_savefile.dm:223
    public virtual object load_preferences()
    {
        if (savefile == null)
        {
            stack_trace(Dm.text("Attempted to load the preferences of [] without a savefile; did you forget to call load_savefile?", parent), "code/modules/client/preferences_savefile.dm", 225);
            load_savefile();
            if (savefile == null)
            {
                stack_trace(Dm.text("Failed to load the savefile for [] after manually calling load_savefile; something is very wrong.", parent), "code/modules/client/preferences_savefile.dm", 228);
                return FALSE;
            }
        }

        object data_validity_integer = check_savedata_version(savefile.get_entry() as DmList);
        if (Dm.truthy(load_and_save) && IS_DATA_OBSOLETE(data_validity_integer)) //fatal, can't load any data
        {
            string bacpath = PREFS_BACKUP_PATH(path); //todo: if the savefile version is higher then the server, check the backup, and give the player a prompt to load the backup
            if (Dm.truthy(fexists(bacpath)))
                fdel(bacpath); //only keep 1 version of backup
            fcopy(savefile.path, bacpath); //byond helpfully lets you use a savefile for the first arg.
            return FALSE;
        }

        apply_all_client_preferences();

        //general preferences
        lastchangelog = Dm.str(savefile.get_entry("lastchangelog", lastchangelog));
        be_special = savefile.get_entry("be_special", be_special) as DmList;
        default_slot = Dm.num(savefile.get_entry("default_slot", default_slot));
        chat_toggles = Dm.num(savefile.get_entry("chat_toggles", chat_toggles));
        toggles = Dm.num(savefile.get_entry("toggles", toggles));
        ignoring = savefile.get_entry("ignoring", ignoring) as DmList;
        job_assigned_profiles = savefile.get_entry("job_assigned_profiles") as DmList;

        // OOC commendations (gaps R5)
        hearted_until = savefile.get_entry("hearted_until", hearted_until);
        if (Dm.num(hearted_until) > world.realtime)
            hearted = TRUE;
        // favorite outfits (the admin outfit menu) and the custom hotkeys: not translated

        //try to fix any outdated data if necessary
        if (SHOULD_UPDATE_DATA(data_validity_integer))
            Dm.NotTranslated("code/modules/client/preferences_savefile.dm:266", "load_preferences: update_preferences, the savefile version updaters (the port never wrote an older version)");

        //Sanitize
        lastchangelog = Dm.str(sanitize_text(lastchangelog, Dm.initial(this, (datum_preferences p) => p.lastchangelog)));
        default_slot = Dm.num(sanitize_integer(default_slot, 1, max_save_slots, Dm.initial(this, (datum_preferences p) => p.default_slot)));
        toggles = Dm.num(sanitize_integer(toggles, 0, SHORT_REAL_LIMIT - 1, Dm.initial(this, (datum_preferences p) => p.toggles)));
        be_special = sanitize_be_special(SANITIZE_LIST(be_special) as DmList) as DmList;
        job_assigned_profiles = SANITIZE_LIST(job_assigned_profiles) as DmList;

        return TRUE;
    }

    // tg: code/modules/client/preferences_savefile.dm:308
    public virtual object save_preferences()
    {
        if (savefile == null)
            throw Dm.CRASH(Dm.text("Attempted to save the preferences of [] without a savefile. This should have been handled by load_preferences()", parent), "code/modules/client/preferences_savefile.dm", 310);
        if (Dm.eq(path, DEV_PREFS_PATH))
        {
            // Don't save over dev preferences
            return TRUE;
        }

        savefile.set_entry("version", SAVEFILE_VERSION_MAX); //updates (or failing that the sanity checks) will ensure data is not invalid at load. Assume up-to-date

        foreach (object preference_type in Dm.each(GLOB.preference_entries))
        {
            datum_preference preference = GLOB.preference_entries[preference_type] as datum_preference;
            if (!Dm.eq(preference.savefile_identifier, PREFERENCE_PLAYER))
                continue;

            if (!Dm.@in(preference.type, recently_updated_keys))
                continue;

            Dm.remove(ref recently_updated_keys, preference.type);

            if (Dm.@in(preference_type, value_cache))
                write_preference(preference, preference.serialize(value_cache[preference_type]));
        }

        savefile.set_entry("lastchangelog", lastchangelog);
        savefile.set_entry("be_special", be_special);
        savefile.set_entry("default_slot", default_slot);
        savefile.set_entry("toggles", toggles);
        savefile.set_entry("chat_toggles", chat_toggles);
        savefile.set_entry("ignoring", ignoring);
        savefile.set_entry("key_bindings", key_bindings);
        savefile.set_entry("hearted_until", Dm.gt(hearted_until, Dm.world.realtime) ? hearted_until : null);
        savefile.set_entry("favorite_outfits", favorite_outfits);
        savefile.set_entry("job_assigned_profiles", job_assigned_profiles);
        savefile.save();
        return TRUE;
    }

    // tg: code/modules/client/preferences_savefile.dm:343
    public virtual object load_character(object slot = null)
    {
        slot ??= default_slot;
        slot = sanitize_integer(slot, 1, max_save_slots, Dm.initial(this, (datum_preferences p) => p.default_slot));
        float original_default_slot = default_slot;
        if (!Dm.eq(slot, default_slot))
        {
            default_slot = Dm.num(slot);
            savefile.set_entry("default_slot", slot);
        }

        string tree_key = Dm.text("character[]", slot);
        DmList save_data = savefile.get_entry(tree_key) as DmList;
        if (save_data == null) // This is the case where we have a new character slot being switched to
        {
            foreach (datum_preference preference in Dm.each<datum_preference>(get_preferences_in_priority_order())) // clear the cache in this case
            {
                if (!Dm.eq(preference.savefile_identifier, PREFERENCE_CHARACTER))
                    continue;
                Dm.remove(ref value_cache, preference.type);
            }
            return FALSE;
        }

        object data_validity_integer = check_savedata_version(save_data);
        if (IS_DATA_OBSOLETE(data_validity_integer)) //fatal, can't load any data
        {
            default_slot = original_default_slot;
            savefile.set_entry("default_slot", original_default_slot);
            return FALSE;
        }

        // Read everything into cache
        // Uses priority order as some values may rely on others for creating default values
        foreach (datum_preference preference in Dm.each<datum_preference>(get_preferences_in_priority_order()))
        {
            if (!Dm.eq(preference.savefile_identifier, PREFERENCE_CHARACTER))
                continue;

            Dm.remove(ref value_cache, preference.type);
            read_preference(preference.type);
        }

        //Character
        randomise = save_data["randomise"] as DmList;

        //Load prefs
        job_preferences = save_data["job_preferences"] as DmList;

        //Quirks
        all_quirks = save_data["all_quirks"] as DmList;

        //try to fix any outdated data if necessary
        //preference updating will handle saving the updated data for us.
        if (SHOULD_UPDATE_DATA(data_validity_integer))
            Dm.NotTranslated("code/modules/client/preferences_savefile.dm:390", "load_character: update_character, the savefile version updaters (the port never wrote an older version)");

        //Sanitize
        randomise = SANITIZE_LIST(randomise) as DmList;
        job_preferences = SANITIZE_LIST(job_preferences) as DmList;
        all_quirks = SANITIZE_LIST(all_quirks) as DmList;

        //Validate job prefs
        foreach (object job in Dm.each(job_preferences.Copy()))
        {
            object priority = job_preferences[job];
            if (!Dm.eq(priority, JP_LOW) && !Dm.eq(priority, JP_MEDIUM) && !Dm.eq(priority, JP_HIGH))
                Dm.remove(ref job_preferences, job);
        }

        // all_quirks = SSquirks.filter_invalid_quirks(...); validate_quirks(): the quirks (SSquirks) are not translated

        return TRUE;
    }

    // tg: code/modules/client/preferences_savefile.dm:404
    public virtual object save_character()
    {
        if (!Dm.truthy(path))
            return FALSE;
        string tree_key = Dm.text("character[]", default_slot);
        if (!Dm.@in(tree_key, savefile.get_entry()))
            savefile.set_entry(tree_key, Dm.list());
        DmList save_data = savefile.get_entry(tree_key) as DmList;

        foreach (datum_preference preference in Dm.each<datum_preference>(get_preferences_in_priority_order()))
        {
            if (!Dm.eq(preference.savefile_identifier, PREFERENCE_CHARACTER))
                continue;

            if (!Dm.@in(preference.type, recently_updated_keys))
                continue;

            Dm.remove(ref recently_updated_keys, preference.type);

            if (Dm.@in(preference.type, value_cache))
                write_preference(preference, preference.serialize(value_cache[preference.type]));
        }

        save_data["version"] = SAVEFILE_VERSION_MAX; //load_character will sanitize any bad data, so assume up-to-date.

        //Character
        save_data["randomise"] = randomise;

        //Write prefs
        save_data["job_preferences"] = job_preferences;

        //Quirks
        save_data["all_quirks"] = all_quirks;

        return TRUE;
    }

    // tg: code/modules/client/preferences_savefile.dm:444
    public virtual void switch_to_slot(object new_slot = null)
    {
        if (Dm.eq(new_slot, default_slot)) // sanity check, nothing to do here.
            return;
        // SAFETY: `load_character` performs sanitization on the slot number
        if (!Dm.truthy(load_character(new_slot)))
        {
            tainted_character_profiles = TRUE;
            randomise_appearance_prefs();
            all_quirks = Dm.list();
            Dm.combine(ref recently_updated_keys, typeof(datum_preference_name_real_name));
            save_character();
        }

        // for (var/datum/preference_middleware/preference_middleware as anything in middleware) preference_middleware.on_new_character(usr):
        // of the translated middleware (the names', the jobs') none has on_new_character
        Waiting.NoSleep(character_preview_view.update_body());
    }

    // tg: code/modules/client/preferences_savefile.dm:460
    public virtual void remove_current_slot()
    {
        object closest_slot = null;
        for (float other_slot = default_slot - 1; other_slot >= 1; other_slot--)
        {
            object save_data = savefile.get_entry(Dm.text("character[]", other_slot));
            if (save_data != null)
            {
                closest_slot = other_slot;
                break;
            }
        }

        if (closest_slot == null)
        {
            for (float other_slot = default_slot + 1; other_slot <= max_save_slots; other_slot++)
            {
                object save_data = savefile.get_entry(Dm.text("character[]", other_slot));
                if (save_data != null)
                {
                    closest_slot = other_slot;
                    break;
                }
            }
        }

        if (closest_slot == null)
        {
            stack_trace("remove_current_slot() being called when there are no slots to go to, the client should prevent this", "code/modules/client/preferences_savefile.dm", 478);
            return;
        }

        savefile.remove_entry(Dm.text("character[]", default_slot));
        tainted_character_profiles = TRUE;
        switch_to_slot(closest_slot);
    }

    // tg: code/modules/client/preferences_savefile.dm:485  (syndicate A3: the antagonists' preferences)
    public virtual object sanitize_be_special(DmList input_be_special = null)
    {
        DmList output = Dm.list();

        foreach (object role in Dm.each(input_be_special))
        {
            if (Dm.@in(role, get_all_antag_flags()))
                Dm.append(ref output, role);
        }

        return output.len == input_be_special.len ? input_be_special : output;
    }
}
