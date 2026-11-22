// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/_preference.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice F (D267): the preference framework whole; the species features' base (/datum/preference/choiced/species_feature) came with
// gaps SP1 (D338); humans' looks are M10x slice D3's basic.cs. The choiced preferences' icons (should_generate_icons) are
// sent as /icon recipes in the constant data, not as a spritesheet (DV44, M10x slice D4). GLOB.preference_entries holds the translated
// preferences only
// (PORT: typesof_written, types-converter.md 8: a generated-only preference's procs are not translated, DV180).

public static partial class GlobalProcs
{
    // Priorities must be in order!
    /// The default priority level
    // tg: code/modules/client/preferences/_preference.dm:3
    public const int PREFERENCE_PRIORITY_DEFAULT = 1;

    /// For things that should be applied after the default prio, but before species to apply properly.
    // tg: code/modules/client/preferences/_preference.dm:6
    public const int PREFERENCE_PRIORITY_PRE_SPECIES = 2;

    /// The priority at which species runs, needed for external organs to apply properly.
    // tg: code/modules/client/preferences/_preference.dm:9
    public const int PREFERENCE_PRIORITY_SPECIES = 3;

    /**
     * Some preferences get applied directly to bodyparts (anything head_flags related right now).
     * These must apply after species, as species gaining might replace the bodyparts of the human.
     */
    // tg: code/modules/client/preferences/_preference.dm:15
    public const int PREFERENCE_PRIORITY_BODYPARTS = 4;

    /// The priority at which gender is determined, needed for proper randomization.
    // tg: code/modules/client/preferences/_preference.dm:18
    public const int PREFERENCE_PRIORITY_GENDER = 5;

    /// The priority at which body type is decided, applied after gender so we can
    /// support the "use gender" option.
    // tg: code/modules/client/preferences/_preference.dm:22
    public const int PREFERENCE_PRIORITY_BODY_TYPE = 6;

    /// Used for preferences that rely on body setup being finalized.
    // tg: code/modules/client/preferences/_preference.dm:25
    public const int PREFERENCE_PRORITY_LATE_BODY_TYPE = 7;

    /// Equipping items based on preferences.
    /// Should happen after species and body type to make sure it looks right.
    /// Mostly redundant, but a safety net for saving/loading.
    // tg: code/modules/client/preferences/_preference.dm:30
    public const int PREFERENCE_PRIORITY_LOADOUT = 8;

    /// The priority at which names are decided, needed for proper randomization.
    // tg: code/modules/client/preferences/_preference.dm:33
    public const int PREFERENCE_PRIORITY_NAMES = 9;

    /// Preferences that aren't names, but change the name changes set by PREFERENCE_PRIORITY_NAMES.
    // tg: code/modules/client/preferences/_preference.dm:36
    public const int PREFERENCE_PRIORITY_NAME_MODIFICATIONS = 10;

    /// The maximum preference priority, keep this updated, but don't use it for `priority`.
    // tg: code/modules/client/preferences/_preference.dm:39
    public const int MAX_PREFERENCE_PRIORITY = PREFERENCE_PRIORITY_NAME_MODIFICATIONS;

    /// For choiced preferences, this key will be used to set display names in constant data.
    // tg: code/modules/client/preferences/_preference.dm:42
    public const string CHOICED_PREFERENCE_DISPLAY_NAMES = "display_names";

    /// For main feature preferences, this key refers to a feature considered supplemental.
    /// For instance, hair color being supplemental to hair.
    // tg: code/modules/client/preferences/_preference.dm:46
    public const string SUPPLEMENTAL_FEATURE_KEY = "supplemental_feature";

    // tg: code/modules/client/preferences/_preference.dm:54
    public static DmList init_preference_entries()
    {
        DmList output = Dm.list();
        // DEVIATION(DV180): only the translated preferences (typesof_written): a generated-only one's procs are not translated
        foreach (Type preference_type in Dm.each<Type>(typesof_written(valid_subtypesof(typeof(datum_preference)))))
            output[preference_type] = Dm.@new(preference_type);
        return output;
    }

    // tg: code/modules/client/preferences/_preference.dm:60
    public static DmList init_preference_entries_by_key()
    {
        DmList output = Dm.list();
        foreach (Type preference_type in Dm.each<Type>(typesof_written(valid_subtypesof(typeof(datum_preference)))))
            output[Dm.initial(preference_type, (datum_preference p) => p.savefile_key)] = GLOB.preference_entries[preference_type];
        return output;
    }

    /// Returns a flat list of preferences in order of their priority
    // tg: code/modules/client/preferences/_preference.dm:67
    public static DmList get_preferences_in_priority_order()
    {
        DmList preferences = Dm.list();
        preferences.len = MAX_PREFERENCE_PRIORITY;

        foreach (object preference_type in Dm.each(GLOB.preference_entries))
        {
            datum_preference preference = GLOB.preference_entries[preference_type] as datum_preference;
            DmList at = preferences[preference.priority] as DmList;
            LAZYADD(ref at, preference);
            preferences[preference.priority] = at;
        }

        DmList flattened = Dm.list();
        for (int index = 1; index <= MAX_PREFERENCE_PRIORITY; index++)
        {
            if (Dm.truthy(preferences[index]))
                Dm.append(ref flattened, preferences[index]);
        }
        return flattened;
    }
}

public partial class datum_controller_global_vars
{
    /// An assoc list list of types to instantiated `/datum/preference` instances
    // tg: code/modules/client/preferences/_preference.dm:49  GLOBAL_LIST_INIT(preference_entries, init_preference_entries())
    public DmList preference_entries;

    // tg: code/modules/client/preferences/_preference.dm:49  (GLOBAL_MANAGED(preference_entries, init_preference_entries()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpreference_entries()
    {
        preference_entries = init_preference_entries();
        Dm.append(ref gvars_datum_init_order, "preference_entries");
    }

    /// An assoc list of preference entries by their `savefile_key`
    // tg: code/modules/client/preferences/_preference.dm:52  GLOBAL_LIST_INIT(preference_entries_by_key, init_preference_entries_by_key())
    public DmList preference_entries_by_key;

    // tg: code/modules/client/preferences/_preference.dm:52  (GLOBAL_MANAGED(preference_entries_by_key, init_preference_entries_by_key()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpreference_entries_by_key()
    {
        preference_entries_by_key = init_preference_entries_by_key();
        Dm.append(ref gvars_datum_init_order, "preference_entries_by_key");
    }
}

/// Represents an individual preference.
// tg: code/modules/client/preferences/_preference.dm:81
[DmPath("/datum/preference")]
public partial class datum_preference : datum
{
    /// Called on the saved input when retrieving.
    /// Also called by the value sent from the user through UI. Do not trust it.
    /// Input is the value inside the savefile, output is to tell other code
    /// what the value is.
    /// This is useful either for more optimal data saving or for migrating
    /// older data.
    /// Must be overridden by subtypes.
    /// Can return null if no value was found.
    // tg: code/modules/client/preferences/_preference.dm:143
    public virtual object deserialize(object input = null, datum_preferences preferences = null)
    {
        throw Dm.CRASH(Dm.text("`deserialize()` was not implemented on []!", type), "code/modules/client/preferences/_preference.dm", 146);
    }

    /// Called on the input while saving.
    /// Input is the current value, output is what to save in the savefile.
    // tg: code/modules/client/preferences/_preference.dm:150
    public virtual object serialize(object input = null)
    {
        return input;
    }

    /// Produce a default, potentially random value for when no value for this
    /// preference is found in the savefile.
    /// Either this or create_informed_default_value must be overriden by subtypes.
    // tg: code/modules/client/preferences/_preference.dm:157
    public virtual object create_default_value()
    {
        throw Dm.CRASH(Dm.text("`create_default_value()` was not implemented on []!", type), "code/modules/client/preferences/_preference.dm", 160);
    }

    /// Produce a default, potentially random value for when no value for this
    /// preference is found in the savefile.
    /// Unlike create_default_value(), will provide the preferences object if you
    /// need to use it.
    /// If not overriden, will call create_default_value() instead.
    // tg: code/modules/client/preferences/_preference.dm:167
    public virtual object create_informed_default_value(datum_preferences preferences = null)
    {
        return create_default_value();
    }

    /// Produce a random value for the purposes of character randomization.
    /// Will just create a default value by default.
    // tg: code/modules/client/preferences/_preference.dm:172
    public virtual object create_random_value(datum_preferences preferences = null)
    {
        return create_informed_default_value(preferences);
    }

    /// Returns whether or not a preference can be randomized.
    // tg: code/modules/client/preferences/_preference.dm:176
    public object is_randomizable()
    {
        return Dm.eq(savefile_identifier, PREFERENCE_CHARACTER) && Dm.truthy(can_randomize) ? TRUE : FALSE;
    }

    /// Given a savefile, return either the saved data or an acceptable default.
    /// This will write to the savefile if a value was not found with the new value.
    // tg: code/modules/client/preferences/_preference.dm:182
    public object read(DmList save_data = null, datum_preferences preferences = null)
    {
        object value = null;

        if (save_data != null)
            value = save_data[savefile_key];

        if (value == null)
            return null;
        else
            return deserialize(value, preferences);
    }

    /// Given a savefile, writes the inputted value.
    /// Returns TRUE for a successful application.
    /// Return FALSE if it is invalid.
    // tg: code/modules/client/preferences/_preference.dm:197
    public object write(DmList save_data = null, object value = null, datum_preferences preferences = null)
    {
        if (!Dm.truthy(is_valid(value, preferences)))
            return FALSE;

        if (save_data != null)
            save_data[savefile_key] = serialize(value);

        post_write(value, preferences);

        return TRUE;
    }

    /// Called after a preference has been updated
    // tg: code/modules/client/preferences/_preference.dm:211
    public virtual void post_write(object value = null, datum_preferences preferences = null)
    {
        return;
    }

    /// Apply this preference onto the given client.
    /// Called when the savefile_identifier == PREFERENCE_PLAYER.
    // tg: code/modules/client/preferences/_preference.dm:217
    public virtual void apply_to_client(client client = null, object value = null)
    {
        return;
    }

    /// Fired when the preference is updated.
    /// Calls apply_to_client by default, but can be overridden.
    // tg: code/modules/client/preferences/_preference.dm:224
    public virtual void apply_to_client_updated(client client = null, object value = null)
    {
        apply_to_client(client, value);
    }

    /// Apply this preference onto the given human.
    /// Must be overriden by subtypes.
    /// Called when the savefile_identifier == PREFERENCE_CHARACTER.
    // tg: code/modules/client/preferences/_preference.dm:231
    public virtual void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        throw Dm.CRASH(Dm.text("`apply_to_human()` was not implemented for []!", type), "code/modules/client/preferences/_preference.dm", 234);
    }

    /// Checks that a given value is valid.
    /// Must be overriden by subtypes.
    /// Any type can be passed through.
    // tg: code/modules/client/preferences/_preference.dm:308
    public virtual object is_valid(object value = null, datum_preferences preferences = null)
    {
        throw Dm.CRASH(Dm.text("`is_valid()` was not implemented for []!", type), "code/modules/client/preferences/_preference.dm", 311);
    }

    /// Returns data to be sent to users in the menu
    // tg: code/modules/client/preferences/_preference.dm:314
    public virtual object compile_ui_data(mob user = null, object value = null)
    {
        return serialize(value);
    }

    /// Returns data compiled into the preferences JSON asset
    // tg: code/modules/client/preferences/_preference.dm:320
    public virtual object compile_constant_data()
    {
        return null;
    }

    /// Checks the species currently selected by the passed preferences object to see if it has this preference's key as a feature.
    // tg: code/modules/client/preferences/_preference.dm:326
    public virtual object current_species_has_savekey(datum_preferences preferences = null)
    {
        object species_type = preferences.read_preference(typeof(datum_preference_choiced_species));
        datum_species species = GLOB.species_prototypes[species_type] as datum_species;
        return Dm.@in(savefile_key, species.get_features()) ? TRUE : FALSE;
    }

    /// Checks if this preference is relevant and thus visible to the passed preferences object.
    // tg: code/modules/client/preferences/_preference.dm:332
    public virtual object has_relevant_feature(datum_preferences preferences = null)
    {
        if (relevant_inherent_trait == null && relevant_organ == null && relevant_head_flag == null && relevant_body_markings == null)
            return TRUE;

        return current_species_has_savekey(preferences);
    }

    /// Returns whether or not this preference is accessible.
    /// If FALSE, will not show in the UI and will not be editable (by update_preference).
    // tg: code/modules/client/preferences/_preference.dm:340
    public virtual object is_accessible(datum_preferences preferences = null)
    {
        if (!Dm.truthy(has_relevant_feature(preferences)))
            return FALSE;

        if (!Dm.truthy(should_show_on_page(preferences.current_window)))
            return FALSE;

        return TRUE;
    }

    /// Returns whether or not, given the PREFERENCE_TAB_*, this preference should
    /// appear.
    // tg: code/modules/client/preferences/_preference.dm:354
    public virtual object should_show_on_page(object preference_tab = null)
    {
        bool is_on_character_page = Dm.eq(preference_tab, PREFERENCE_TAB_CHARACTER_PREFERENCES);
        bool is_character_preference = Dm.eq(savefile_identifier, PREFERENCE_CHARACTER);
        return is_on_character_page == is_character_preference ? TRUE : FALSE;
    }

    /// Checks that a given preference's highest priority job is one of the given job types (tg: the proc's own comment)
    // tg: code/modules/client/preferences/_preference.dm:375
    public virtual object highest_priority_job_is(datum_preferences preferences = null, object job_type_or_types = null)
    {
        if (Dm.islist(job_type_or_types))
            return is_type_in_list(preferences.get_highest_priority_job(), (DmList)job_type_or_types);

        return Dm.istype(preferences.get_highest_priority_job(), (Type)job_type_or_types) ? TRUE : FALSE;
    }

    /// Given randomization flags, will return whether or not this preference should be randomized.
    // tg: code/modules/client/preferences/middleware/random.dm:77
    public virtual object included_in_randomization_flags(object randomize_flags = null)
    {
        return TRUE;
    }
}

public partial class datum_preferences : datum
{
    /// Returns which savefile to use for a given savefile identifier
    // tg: code/modules/client/preferences/_preference.dm:237
    public virtual DmList get_save_data_for_savefile_identifier(object savefile_identifier = null)
    {
        if (parent == null)
            return null;
        if (savefile == null)
            throw Dm.CRASH(Dm.text("Attempted to get the savedata for [] of [] without a savefile. This should have been handled by load_preferences()", savefile_identifier, parent), "code/modules/client/preferences/_preference.dm", 243);

        // Both of these will cache savefiles, but only for a tick.
        // This is because storing a savefile will lock it, causing later issues down the line.
        // Do not change them to addtimer, since the timer SS might not be running at this time.
        if (Dm.eq(savefile_identifier, PREFERENCE_CHARACTER))
            return savefile.get_entry(Dm.text("character[]", default_slot)) as DmList;
        if (Dm.eq(savefile_identifier, PREFERENCE_PLAYER))
            return savefile.get_entry() as DmList;
        throw Dm.CRASH(Dm.text("Unknown savefile identifier []", savefile_identifier), "code/modules/client/preferences/_preference.dm", 254);
    }

    /// Read a /datum/preference type and return its value.
    /// This will write to the savefile if a value was not found with the new value.
    // tg: code/modules/client/preferences/_preference.dm:258
    public virtual object read_preference(object preference_type = null)
    {
        datum_preference preference_entry = GLOB.preference_entries[preference_type] as datum_preference;
        if (preference_entry == null)
        {
            string extra_info = "";

            // Current initializing subsystem is important to know because it might be a problem with
            // things running pre-assets-initialization.
            if (Master.current_initializing_subsystem != null)
                extra_info = Dm.text("Info was attempted to be retrieved while [] was initializing.", Master.current_initializing_subsystem);
            else if (!Dm.truthy(MC_RUNNING()))
                extra_info = "Info was attempted to be retrieved before the MC started, but not while it was actively initializing a subsystem";

            throw Dm.CRASH(Dm.text("Preference type `[]` is invalid! []", preference_type, extra_info), "code/modules/client/preferences/_preference.dm", 270);
        }

        if (Dm.@in(preference_type, value_cache))
            return value_cache[preference_type];

        object value = preference_entry.read(get_save_data_for_savefile_identifier(preference_entry.savefile_identifier), this);
        if (value == null)
        {
            value = preference_entry.create_informed_default_value(this);
            if (Dm.truthy(write_preference(preference_entry, value)))
                return value;
            else
                throw Dm.CRASH(Dm.text("Couldn't write the default value for [] (received [])", preference_type, value), "code/modules/client/preferences/_preference.dm", 281);
        }
        value_cache[preference_type] = value;
        return value;
    }

    /// Set a /datum/preference entry.
    /// Returns TRUE for a successful preference application.
    /// Returns FALSE if it is invalid.
    // tg: code/modules/client/preferences/_preference.dm:288
    public virtual object write_preference(datum_preference preference = null, object preference_value = null)
    {
        DmList save_data = get_save_data_for_savefile_identifier(preference.savefile_identifier);
        object new_value = preference.deserialize(preference_value, this);
        object success = preference.write(save_data, new_value, this);
        if (Dm.truthy(success))
            value_cache[preference.type] = new_value;
        return success;
    }

    /// Will perform an update on the preference, but not write to the savefile.
    /// This will, for instance, update the character preference view.
    /// Performs sanity checks.
    // tg: code/modules/client/preferences/_preference.dm:299
    public virtual object update_preference(datum_preference preference = null, object preference_value = null)
    {
        if (!Dm.truthy(preference.is_accessible(this)))
            return FALSE;

        object new_value = preference.deserialize(preference_value, this);
        object success = preference.write(null, new_value, this);

        if (!Dm.truthy(success))
            return FALSE;

        Dm.combine(ref recently_updated_keys, preference.type);
        value_cache[preference.type] = new_value;

        // D311: a new gender or species may not fit the voice: the voice's deserialize picks a fitting one
        if (preference is datum_preference_choiced_gender or datum_preference_choiced_species
            && GLOB.preference_entries[typeof(datum_preference_choiced_voice)] is datum_preference voice)
            update_preference(voice, read_preference(typeof(datum_preference_choiced_voice)));

        if (Dm.eq(preference.savefile_identifier, PREFERENCE_PLAYER))
            preference.apply_to_client_updated(parent, read_preference(preference.type));
        else if (Dm.truthy(preference.should_update_preview))
        {
            if (character_preview_view != null)
                Waiting.NoSleep(character_preview_view.update_body());
        }

        return TRUE;
    }
}

/// A preference that is a choice of one option among a fixed set.
/// Used for preferences such as clothing.
// tg: code/modules/client/preferences/_preference.dm:383
[DmPath("/datum/preference/choiced")]
public partial class datum_preference_choiced : datum_preference
{
    /// Returns a list of every possible value.
    /// The first time this is called, will run `init_values()`.
    /// Return value can be in the form of:
    /// - A flat list of raw values, such as list(MALE, FEMALE, PLURAL).
    /// - An assoc list of raw values to atoms/icons.
    // tg: code/modules/client/preferences/_preference.dm:402
    public DmList get_choices()
    {
        // Override `init_values()` instead.
        if (cached_values == null)
        {
            cached_values = init_possible_values();
            // ASSERT(cached_values.len) (__DEFINES/assert.dm:4, rules 9.5)
            if (!Dm.truthy(cached_values.len))
                throw Dm.CRASH("assertion failed: cached_values.len", "code/modules/client/preferences/_preference.dm", 408);
        }

        return cached_values;
    }

    /// Returns a list of every possible value, serialized.
    // tg: code/modules/client/preferences/_preference.dm:413
    public DmList get_choices_serialized()
    {
        // Override `init_values()` instead.
        DmList serialized_choices = Dm.list();

        foreach (object choice in Dm.each(get_choices()))
            Dm.append(ref serialized_choices, serialize(choice));

        return serialized_choices;
    }

    /// Returns a list of every possible value.
    /// This must be overriden by `/datum/preference/choiced` subtypes.
    /// If `should_generate_icons` is TRUE, then you will also need to implement `icon_for(value)`
    /// for every possible value.
    // tg: code/modules/client/preferences/_preference.dm:428
    public virtual DmList init_possible_values()
    {
        throw Dm.CRASH(Dm.text("`init_possible_values()` was not implemented for []!", type), "code/modules/client/preferences/_preference.dm", 429);
    }

    /// Returns an icon for the given value (a /datum/universal_icon, here the port's /icon, or an atom type).
    // tg: code/modules/client/preferences/_preference.dm:432
    public virtual object icon_for(object value = null)
    {
        throw Dm.CRASH(Dm.text("`icon_for()` was not implemented for [], even though should_generate_icons = TRUE!", type), "code/modules/client/preferences/_preference.dm", 435);
    }

    // tg: code/modules/client/preferences/_preference.dm:438
    public override object is_valid(object value = null, datum_preferences preferences = null)
    {
        return Dm.@in(value, get_choices()) ? TRUE : FALSE;
    }

    // tg: code/modules/client/preferences/_preference.dm:441
    public override object deserialize(object input = null, datum_preferences preferences = null)
    {
        return sanitize_inlist(input, get_choices(), create_default_value());
    }

    // tg: code/modules/client/preferences/_preference.dm:444
    public override object create_default_value()
    {
        return pick(get_choices());
    }

    // tg: code/modules/client/preferences/_preference.dm:447
    public override object compile_constant_data()
    {
        DmList data = Dm.list();

        DmList choices = Dm.list();

        foreach (object choice in Dm.each(get_choices()))
            Dm.append(ref choices, choice);

        data["choices"] = choices;

        if (Dm.truthy(should_generate_icons))
        {
            DmList icons = Dm.list();

            // DEVIATION(DV44): each choice's picture, list(resource, icon state), where tg gives its spritesheet key
            // (get_spritesheet_key, preferences/assets.dm:34) and draws icon_for into the spritesheet (assets.dm:6-31); the client
            // draws the resource (an /icon recipe, protocol.md 34.3)
            foreach (object choice in Dm.each(choices))
            {
                object create_icon_of = icon_for(choice);
                icon picture = create_icon_of is Type atom_icon_source ? get_display_icon_for(atom_icon_source) : create_icon_of as icon;
                if (picture == null)
                    throw Dm.CRASH(Dm.text("[] is an invalid preference value (from []:[]).", create_icon_of, savefile_key, choice), "code/modules/client/preferences/assets.dm", 27);
                icons[choice] = Dm.list(picture.dm_resource(), picture.dm_state);
            }

            data["icons"] = icons;
        }

        if (main_feature_name != null)
            data["name"] = main_feature_name;

        return data;
    }
}

/// This subtype handles a lot of boilerplate for implementing a species preference tied to a feature key / sprite accessory
// tg: code/modules/client/preferences/_preference.dm:471 (gaps SP1)
public partial class datum_preference_choiced_species_feature : datum_preference_choiced
{
    // tg: code/modules/client/preferences/_preference.dm:477
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (relevant_organ != null && Dm.truthy(Dm.initial(relevant_organ, (obj_item_organ p) => p.bodypart_overlay)))
        {
            Type overlay_type = Dm.initial(relevant_organ, (obj_item_organ p) => p.bodypart_overlay) as Type;
            feature_key = Dm.truthy(feature_key) ? feature_key : Dm.initial(overlay_type, (datum_bodypart_overlay_mutant p) => p.feature_key);
            main_feature_name = Dm.truthy(main_feature_name) ? main_feature_name : capitalize(Dm.initial(relevant_organ, (obj_item_organ p) => p.name));
        }
        if (relevant_body_markings != null)
        {
            feature_key = Dm.truthy(feature_key) ? feature_key : Dm.initial(relevant_body_markings, (datum_bodypart_overlay_simple_body_marking p) => p.dna_feature_key);
            main_feature_name = Dm.truthy(main_feature_name) ? main_feature_name : "Body markings";
        }
        if (feature_key == null)
            throw Dm.CRASH(Dm.text("`feature_key` was not set or inferable for []!", type), "code/modules/client/preferences/_preference.dm", 486);
    }

    // tg: code/modules/client/preferences/_preference.dm:488
    public override DmList init_possible_values()
    {
        return assoc_to_keys_features(get_accessory_list() as DmList) as DmList;
    }

    // tg: code/modules/client/preferences/_preference.dm:491
    public override object create_default_value()
    {
        return get_consistent_feature_entry(get_accessory_list() as DmList);
    }

    // tg: code/modules/client/preferences/_preference.dm:494
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        target.dna.features[feature_key] = value;
    }

    /// Returns what acessory list to draw from
    // tg: code/modules/client/preferences/_preference.dm:498
    public virtual object get_accessory_list()
    {
        return SSaccessories.feature_list[feature_key];
    }

    /// Get a specific accessory for a given value
    // tg: code/modules/client/preferences/_preference.dm:502
    public virtual object get_accessory_for_value(object value = null)
    {
        return ((DmList)get_accessory_list())[value];
    }
}

/// A preference that represents an RGB color of something.
/// Will give the value as 6 hex digits, without a hash.
// tg: code/modules/client/preferences/_preference.dm:504
[DmPath("/datum/preference/color")]
public partial class datum_preference_color : datum_preference
{
    // tg: code/modules/client/preferences/_preference.dm:507
    public override object deserialize(object input = null, datum_preferences preferences = null)
    {
        return sanitize_hexcolor(input);
    }

    // tg: code/modules/client/preferences/_preference.dm:510
    public override object create_default_value()
    {
        return random_color();
    }

    // tg: code/modules/client/preferences/_preference.dm:513
    public override object serialize(object input = null)
    {
        return sanitize_hexcolor(input);
    }

    // tg: code/modules/client/preferences/_preference.dm:516
    public override object is_valid(object value = null, datum_preferences preferences = null)
    {
        // findtext(value, GLOB.is_color): the regex (a shim gap) is its pattern, ^#[0-9a-fA-F]{6}$ (_globalvars/_regexes.dm:8)
        return value is string text && text.Length == 7 && text[0] == '#' && System.Linq.Enumerable.All(text[1..], char.IsAsciiHexDigit) ? TRUE : FALSE;
    }
}

/// A numeric preference with a minimum and maximum value
// tg: code/modules/client/preferences/_preference.dm:520
[DmPath("/datum/preference/numeric")]
public partial class datum_preference_numeric : datum_preference
{
    // tg: code/modules/client/preferences/_preference.dm:532
    public override object deserialize(object input = null, datum_preferences preferences = null)
    {
        if (Dm.istext(input)) // Sometimes TGUI will return a string instead of a number, so we take that into account.
            input = text2num(input); // Worst case, it's null, it'll just use create_default_value()
        return sanitize_float(input, minimum, maximum, step, create_default_value());
    }

    // tg: code/modules/client/preferences/_preference.dm:537
    public override object serialize(object input = null)
    {
        return sanitize_float(input, minimum, maximum, step, create_default_value());
    }

    // tg: code/modules/client/preferences/_preference.dm:540
    public override object create_default_value()
    {
        return rand(Dm.num(minimum), Dm.num(maximum));
    }

    // tg: code/modules/client/preferences/_preference.dm:543
    public override object is_valid(object value = null, datum_preferences preferences = null)
    {
        return Dm.isnum(value) && Dm.ge(value, Dm.round(Dm.num(minimum), Dm.num(step))) && Dm.le(value, Dm.round(Dm.num(maximum), Dm.num(step))) ? TRUE : FALSE;
    }

    // tg: code/modules/client/preferences/_preference.dm:546
    public override object compile_constant_data()
    {
        return Dm.list(
            Dm.kv("minimum", minimum),
            Dm.kv("maximum", maximum),
            Dm.kv("step", step));
    }
}

/// A preference whose value is always TRUE or FALSE
// tg: code/modules/client/preferences/_preference.dm:554
[DmPath("/datum/preference/toggle")]
public partial class datum_preference_toggle : datum_preference
{
    // tg: code/modules/client/preferences/_preference.dm:560
    public override object create_default_value()
    {
        return default_value;
    }

    // tg: code/modules/client/preferences/_preference.dm:563
    public override object deserialize(object input = null, datum_preferences preferences = null)
    {
        return Dm.truthy(input) ? TRUE : FALSE;
    }

    // tg: code/modules/client/preferences/_preference.dm:566
    public override object is_valid(object value = null, datum_preferences preferences = null)
    {
        return Dm.eq(value, TRUE) || Dm.eq(value, FALSE) ? TRUE : FALSE;
    }
}
