// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice F, D267): GLOB.preferences_datums, New, the job preference level, safe_transfer_prefs_to, apply_prefs_to,
// should_be_random_hardcore, get_default_randomization, and the tgui menu's data and acts for the translated preferences (the
// character's name, gender and age, D267; the looks and set_color_preference's colour picker, M10x slice D3). safe_read_pref and get_chat_toggles keep DV161: the player preferences (the toggles,
// the chat settings) are not translated, so they read their defaults. M10x slice E: the character preview (char_preview,
// create_character_preview_view, rotate, and the loadout's rotate_dummy, DV210) and the randomization middleware's data and acts.
// Not translated: the quirks (SSquirks), the keybindings
// (the port's client binds its own keys) and BYOND membership.

public partial class datum_controller_global_vars
{
    // tg: code/modules/client/preferences.dm:1  GLOBAL_LIST_EMPTY(preferences_datums)
    public DmList preferences_datums;

    // tg: code/modules/client/preferences.dm:1  (GLOBAL_MANAGED(preferences_datums, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpreferences_datums()
    {
        preferences_datums = Dm.list();
        Dm.append(ref gvars_datum_init_order, "preferences_datums");
    }
}

// tg: code/modules/client/preferences.dm:3
[DmPath("/datum/preferences")]
public partial class datum_preferences : datum
{
    // tg: code/modules/client/preferences/middleware/names.dm:8
    public virtual DmList names_get_constant_data()
    {
        DmList data = Dm.list();

        DmList types = Dm.list();

        foreach (object preference_type in Dm.each(GLOB.preference_entries))
        {
            datum_preference_name name_preference = GLOB.preference_entries[preference_type] as datum_preference_name;
            if (name_preference == null)
                continue;

            types[name_preference.savefile_key] = Dm.list(
                Dm.kv("can_randomize", name_preference.is_randomizable()),
                Dm.kv("explanation", name_preference.explanation),
                Dm.kv("group", name_preference.group));
        }

        data["types"] = types;

        return data;
    }

    // tg: code/modules/client/preferences/middleware/names.dm:33
    public virtual object names_get_name_to_use()
    {
        datum_job highest_priority_job = get_highest_priority_job();

        foreach (object preference_type in Dm.each(GLOB.preference_entries))
        {
            datum_preference_name name_preference = GLOB.preference_entries[preference_type] as datum_preference_name;
            if (name_preference == null)
                continue;

            if (name_preference.relevant_job == null)
                continue;

            if (Dm.istype(highest_priority_job, name_preference.relevant_job))
                return name_preference.savefile_key;
        }

        return "real_name";
    }

    /// The path to the general savefile for this datum
    // tg: code/modules/client/preferences.dm:6 (a text: the types tool reads `var/path` as a type path)
    public string path;

    // tg: code/modules/client/preferences.dm:109
    public override async DmTask New(params object[] __extra)
    {
        object parent_or_mock = __extra.Length > 0 ? __extra[0] : null;
        // a mock client (randomize_human_appearance's) is not held as the parent: the var is typed /client, and nothing reads what
        // apply_all_client_preferences would write on a throwaway mock (its fps, hotkeys; its set_macros does nothing)
        client parent = parent_or_mock as client;
        this.parent = parent;

        // for (var/middleware_type in subtypesof(/datum/preference_middleware)) middleware += new middleware_type(src): the tgui menu's
        // middleware (antags, jobs, keybindings, names, quirks, random, species, tts, loadout) is not translated

        if (IS_CLIENT_OR_MOCK(parent_or_mock))
        {
            // is_guest_key(parent.key): the port's keys are not BYOND guest keys, so the actual savefile
            if (parent != null)
                load_path(parent.ckey); // not guest = load their actual savefile
            else
                // DEVIATION(DV381): a mock client's preferences are neither loaded nor saved (as a guest's on a live server): tg saves
                // them under the mock's new ckey, a player_saves folder never read again, at each randomize_human_appearance
                load_and_save = FALSE;
            // if (load_and_save && !fexists(path)) try_savefile_type_migration(): no BYOND savefiles to migrate
            // refresh_membership(): BYOND membership (more save slots) has no counterpart
        }
        else
            throw Dm.CRASH("attempted to create a preferences datum without a client or mock!", "code/modules/client/preferences.dm", 125);
        load_savefile();

        // give them default keybinds and update their movement keys: key_bindings = deep_copy_list(GLOB.default_hotkeys),
        // key_bindings_by_key: DEVIATION(DV161), the port's client binds its own keys
        randomise = get_default_randomization();

        object loaded_preferences_successfully = load_preferences();
        // DEVIATION(DV366): tg's default deadmin-on-role toggles are off: the port has neither their preferences nor the readmin verb, so an admin who took a
        // head, security or silicon job or became an antagonist would stay a normal player. The AUTO_DEADMIN_* config flags still apply.
        toggles = (int)toggles & ~TOGGLES_DEADMIN_DEFAULT;
        if (Dm.truthy(loaded_preferences_successfully))
        {
            if (Dm.truthy(load_character()))
                return;
        }
        //we couldn't load character data so just randomize the character appearance + name
        randomise_appearance_prefs(); //let's create a random character then - rather than a fat, bald and naked man.
        if (parent != null)
        {
            apply_all_client_preferences();
            // parent.set_macros(): the BYOND skin's macros (the port's client binds its own keys)
        }

        if (!Dm.truthy(loaded_preferences_successfully))
            save_preferences();
        save_character(); //let's save this new random character so it doesn't keep generating new ones.
    }

    // tg: code/modules/client/preferences.dm:148
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        // There used to be code here that readded the preview view if you "rejoined"
        // I'm making the assumption that ui close will be called whenever a user logs out, or loses a window
        // If this isn't the case, kill me and restore the code, thanks

        // We need IconForge and the assets to be ready before allowing the menu to open: if(SSearly_assets.initialized != ...) return:
        // GAP(lead), SSearly_assets (the asset transport) is not declared; the client's assets are its own (DV44)

        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            tainted_character_profiles = TRUE;
            character_preview_view = await create_character_preview_view(user);
            ui = Dm.@new<datum_tgui>(user, this, "PreferencesMenu");
            ui.set_autoupdate(FALSE);
            await ui.open();
            character_preview_view.display_to(user, ui.window);
        }
        return null;
    }

    // DV215: the character is edited only by a player in the lobby (a new player); a mob in the round or an observer looks at it
    public virtual object read_only_for(mob user) => user is mob_dead_new_player ? FALSE : TRUE;

    // tg: code/modules/client/preferences.dm:173
    public override object ui_state(mob user = null)
    {
        return GLOB.always_state;
    }

    // Without this, a hacker would be able to edit other people's preferences if
    // they had the ref to Topic to.
    // tg: code/modules/client/preferences.dm:178
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        return user.client == parent ? UI_INTERACTIVE : UI_CLOSE;
    }

    // tg: code/modules/client/preferences.dm:183
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        if (Dm.truthy(tainted_character_profiles) || cached_character_profiles == null)
        {
            cached_character_profiles = create_character_profiles();
            tainted_character_profiles = FALSE;
        }

        data["character_profiles"] = cached_character_profiles;

        data["character_preferences"] = compile_character_preferences(user);

        data["active_slot"] = default_slot;

        // DEVIATION(DV215): the character is edited in the lobby only (D286); in the round the menu shows it to look at
        data["read_only"] = read_only_for(user);

        // for (var/datum/preference_middleware/preference_middleware as anything in middleware) data += get_ui_data(user): of the
        // middleware, the names' (its name_to_use, middleware/names.dm:31) is translated here,
        // and the jobs' (middleware/jobs.cs)
        data["name_to_use"] = names_get_name_to_use();
        data.Add(jobs_get_ui_data(user));
        // DEVIATION(DV210): the loadout middleware's get_ui_data (loadout_menu.dm:94), its preview's Job Clothes checkbox
        data["job_clothes"] = character_preview_view?.show_job_clothes;

        return data;
    }

    // tg: code/modules/client/preferences.dm:198
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();

        data["character_preview_view"] = character_preview_view?.assigned_map;
        data["overflow_role"] = (SSjob.get_job_type(SSjob.overflow_role) as datum_job)?.title;
        data["window"] = current_window;

        data["content_unlocked"] = unlock_content;

        // DEVIATION(DV44): tg's page reads the preferences' constant data from an asset (/datum/asset/json/preferences, assets.dm:40:
        // each preference's compile_constant_data by its savefile key); the native window takes it with the static data
        DmList constant_data = Dm.list();
        foreach (object preference_type in Dm.each(GLOB.preference_entries))
        {
            datum_preference preference_entry = GLOB.preference_entries[preference_type] as datum_preference;
            object entry = preference_entry.compile_constant_data();
            if (entry != null)
                constant_data[preference_entry.savefile_key] = entry;
        }
        // the middleware's get_constant_data by its key (assets.dm:44): the jobs' and the randomization's
        constant_data["jobs"] = jobs_get_constant_data();
        constant_data["names"] = names_get_constant_data();
        constant_data["random"] = random_get_constant_data();
        data["constant_data"] = constant_data;

        // the middleware's get_ui_static_data (preferences.dm:209): the jobs' and the antagonists' (syndicate A3)
        DmList jobs_static = jobs_get_ui_static_data(user);
        if (jobs_static != null)
            data.Add(jobs_static);
        data.Add(antags_get_ui_static_data(user));
        // DEVIATION(DV44): the antagonists middleware's get_ui_assets, its spritesheet's pictures (middleware/antags.cs)
        data["antagonist_icons"] = await antagonist_preview_icons();
        // DEVIATION(DV44): the species middleware's get_ui_assets, its spritesheet's pictures (gaps SP1, middleware/species.cs)
        data["species_icons"] = await species_preview_icons();

        return data;
    }

    // tg: code/modules/client/preferences.dm:216
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        // DEVIATION(DV215): in the round only the preview turns and shows or hides the clothes (D286); the antagonists' choices are not the
        // character's and stay open as tg's (syndicate A3)
        if (Dm.truthy(read_only_for(ui?.user)) && !Dm.eq(action, "rotate") && !Dm.eq(action, "rotate_dummy") && !Dm.eq(action, "toggle_job_clothes") && !Dm.eq(action, "set_antags"))
            return FALSE;

        if (Dm.eq(action, "change_slot"))
        {
            // Save existing character
            save_character();
            // SAFETY: `switch_to_slot` performs sanitization on the slot number
            switch_to_slot(@params["slot"]);
            return TRUE;
        }
        if (Dm.eq(action, "remove_current_slot"))
        {
            remove_current_slot();
            return TRUE;
        }
        if (Dm.eq(action, "rotate"))
        {
            await character_preview_view.setDir(turn(character_preview_view.dir, -90));
            return TRUE;
        }
        // DEVIATION(DV210): the loadout middleware's rotate_dummy (loadout_menu.dm:42 action_rotate_model_dir; the loadout is not
        // translated) turns the preview both ways; the Character page's arrows under the preview send it (D284)
        if (Dm.eq(action, "rotate_dummy"))
        {
            object sw__1 = @params["dir"];
            if (Dm.eq(sw__1, "left"))
                await character_preview_view.setDir(turn(character_preview_view.dir, -90));
            else if (Dm.eq(sw__1, "right"))
                await character_preview_view.setDir(turn(character_preview_view.dir, 90));
            return TRUE;
        }
        // DEVIATION(DV210): and its toggle_job_clothes (loadout_menu.dm:36 action_toggle_job_outfit)
        if (Dm.eq(action, "toggle_job_clothes"))
        {
            character_preview_view.show_job_clothes = Dm.truthy(character_preview_view.show_job_clothes) ? FALSE : TRUE;
            await character_preview_view.update_body();
            return TRUE;
        }
        if (Dm.eq(action, "set_preference"))
        {
            object requested_preference_key = @params["preference"];
            object value = @params["value"];

            // the middleware's pre_set_preference (the quirks' and the legacy toggles'): not translated

            datum_preference requested_preference = GLOB.preference_entries_by_key[requested_preference_key] as datum_preference;
            if (requested_preference == null)
                return FALSE;

            // SAFETY: `update_preference` performs validation checks
            if (!Dm.truthy(update_preference(requested_preference, value)))
                return FALSE;

            if (requested_preference is datum_preference_name)
                tainted_character_profiles = TRUE;

            return TRUE;
        }
        if (Dm.eq(action, "set_color_preference"))
        {
            object requested_preference_key = @params["preference"];

            datum_preference requested_preference = GLOB.preference_entries_by_key[requested_preference_key] as datum_preference;
            if (requested_preference == null)
                return FALSE;

            if (requested_preference is not datum_preference_color)
                return FALSE;

            object default_value = read_preference(requested_preference.type);

            // Yielding
            // DEVIATION(DV230): the colour being picked shows on the preview at once; a cancel puts the old one back
            object new_color = await tgui_color_picker(
                World.Current.usr,
                "Select new color",
                null,
                Dm.truthy(default_value) ? default_value : COLOR_WHITE,
                on_preview: color =>
                {
                    update_preference(requested_preference, color);
                    SStgui.update_uis(this);
                });

            if (!Dm.truthy(new_color))
            {
                update_preference(requested_preference, default_value);
                return FALSE;
            }

            if (!Dm.truthy(update_preference(requested_preference, new_color)))
                return FALSE;

            return TRUE;
        }

        // the middleware's action delegations: the names' randomize_name (middleware/names.dm:50) is translated here, the jobs'
        // set_job_preference (middleware/jobs.cs), the randomization's (middleware/random.cs) and the voice previews (middleware/tts.cs)
        if (Dm.eq(action, "set_job_preference"))
            return jobs_set_job_preference(@params, ui?.user);
        if (Dm.eq(action, "set_job_to_profile"))
            return jobs_set_job_to_profile(@params, ui?.user);
        if (Dm.eq(action, "set_antags")) // the antagonists' (middleware/antags.cs, syndicate A3)
            return antags_set_antags(@params, ui?.user);

        if (Dm.eq(action, "randomize_character"))
            return await random_randomize_character();
        if (Dm.eq(action, "set_random_preference"))
            return random_set_random_preference(@params, ui?.user);

        if (Dm.eq(action, "play_voice"))
            return tts_play_voice(@params, ui?.user);
        if (Dm.eq(action, "play_voice_robot"))
            return tts_play_voice_robot(@params, ui?.user);
        if (Dm.eq(action, "play_voice_radio")) // D312: port-only
            return tts_play_voice_radio(@params, ui?.user);

        if (Dm.eq(action, "randomize_name"))
        {
            datum_preference_name name_preference = GLOB.preference_entries_by_key[@params["preference"]] as datum_preference_name;
            if (name_preference == null)
                return FALSE;

            return update_preference(name_preference, name_preference.create_random_value(this));
        }

        return FALSE;
    }

    // tg: code/modules/client/preferences.dm:290
    public override void ui_close(mob user = null)
    {
        save_character();
        save_preferences();
        QDEL_NULL(ref character_preview_view);
        cached_character_profiles = null;
    }

    // tg: code/modules/client/preferences.dm:309
    public virtual async DmTask<atom_movable_screen_map_view_char_preview> create_character_preview_view(mob user = null)
    {
        character_preview_view = Dm.@new<atom_movable_screen_map_view_char_preview>(null, null, this);
        character_preview_view.generate_view(Dm.text("character_preview_[]", REF(character_preview_view)));
        await character_preview_view.update_body();

        return character_preview_view;
    }

    // tg: code/modules/client/preferences.dm:316
    public virtual DmList compile_character_preferences(mob user = null)
    {
        DmList preferences = Dm.list();

        foreach (datum_preference preference in Dm.each<datum_preference>(get_preferences_in_priority_order()))
        {
            if (!Dm.truthy(preference.is_accessible(this)))
                continue;

            object value = read_preference(preference.type);
            object data = preference.compile_ui_data(user, value);

            DmList category = preferences[preference.category] as DmList;
            LAZYINITLIST(ref category);
            preferences[preference.category] = category;
            category[preference.savefile_key] = data;
        }

        // the middleware's get_character_preferences: of the translated middleware, the randomization's (middleware/random.cs); the
        // species' is not translated
        DmList append_character_preferences = random_get_character_preferences(user);
        foreach (object category in Dm.each(append_character_preferences))
        {
            if (Dm.@in(category, preferences))
                Dm.append_at(preferences, category, append_character_preferences[category]);
            else
                preferences[category] = append_character_preferences[category];
        }

        return preferences;
    }

    // tg: code/modules/client/preferences.dm:387
    public virtual DmList create_character_profiles()
    {
        DmList profiles = Dm.list();

        for (int index = 1; index <= max_save_slots; index++)
        {
            // It won't be updated in the savefile yet, so just read the name directly
            if (index == default_slot)
            {
                Dm.append(ref profiles, read_preference(typeof(datum_preference_name_real_name)));
                continue;
            }

            string tree_key = Dm.text("character[]", index);
            DmList save_data = savefile.get_entry(tree_key) as DmList;
            object name = save_data?["real_name"];

            if (name == null)
            {
                Dm.append(ref profiles, (object)null);
                continue;
            }

            Dm.append(ref profiles, name);
        }

        return profiles;
    }

    /// Applies all PREFERENCE_PLAYER preferences
    // tg: code/modules/client/preferences.dm:344
    public virtual void apply_all_client_preferences()
    {
        foreach (datum_preference preference in Dm.each<datum_preference>(get_preferences_in_priority_order()))
        {
            if (!Dm.eq(preference.savefile_identifier, PREFERENCE_PLAYER))
                continue;

            Dm.remove(ref value_cache, preference.type);
            object value = read_preference(preference.type);
            // a mock client is not held as the parent (New): what apply_to_client writes would land on the throwaway mock
            if (parent != null)
                preference.apply_to_client(parent, value);
        }
    }

    // tg: code/modules/client/preferences.dm:408
    public virtual object set_job_preference_level(datum_job job = null, object level = null)
    {
        if (job == null)
            return FALSE;

        if (Dm.eq(level, JP_HIGH))
        {
            Type overflow_role = SSjob.overflow_role;
            object overflow_role_title = Dm.initial(overflow_role, (datum_job p) => p.title);

            foreach (object other_job in Dm.each(job_preferences.Copy()))
            {
                object other_level = job_preferences[other_job];
                if (Dm.eq(other_level, JP_HIGH))
                {
                    // Overflow role needs to go to NEVER, not medium!
                    if (Dm.eq(other_job, overflow_role_title))
                        Dm.remove(ref job_preferences, other_job);
                    else
                        job_preferences[other_job] = JP_MEDIUM;
                }
            }
        }

        if (Dm.eq(level, null))
            Dm.remove(ref job_preferences, job.title);
        else
            job_preferences[job.title] = level;

        return TRUE;
    }

    // tg: code/modules/client/preferences.dm:514
    public virtual void safe_transfer_prefs_to(mob_living_carbon_human character = null, object icon_updates = null, object is_antag = null)
    {
        icon_updates ??= TRUE;
        is_antag ??= FALSE;
        apply_character_randomization_prefs(is_antag);
        apply_prefs_to(character, icon_updates);
    }

    /**
     * Applies the given preferences to a human mob.
     *
     * Arguments:
     * * character - The human mob to apply the preferences to
     * * icon_updates - Whether to update the mob's icons after applying preferences.
     * Is often skipped to save processing when an update will happen later anyway.
     * * do_not_apply - A list of preference types to skip when applying preferences.
     */
    // tg: code/modules/client/preferences.dm:527
    public virtual void apply_prefs_to(mob_living_carbon_human character = null, object icon_updates = null, DmList do_not_apply = null)
    {
        icon_updates ??= TRUE;
        character.dna.features = Dm.list();

        foreach (datum_preference preference in Dm.each<datum_preference>(get_preferences_in_priority_order()))
        {
            if (!Dm.eq(preference.savefile_identifier, PREFERENCE_CHARACTER))
                continue;
            if (do_not_apply != null && Dm.@in(preference.type, do_not_apply))
                continue;

            preference.apply_to_human(character, read_preference(preference.type), this);
        }

        character.dna.real_name = character.real_name;

        if (Dm.truthy(icon_updates))
        {
            character.icon_render_keys = Dm.list();
            Waiting.NoSleep(character.update_body(TRUE)); // is_creating = TRUE
        }

        // SEND_SIGNAL(character, COMSIG_HUMAN_PREFS_APPLIED) (rules 11.1 (e), a statement)
        if (Dm.truthy(character._listen_lookup?[COMSIG_HUMAN_PREFS_APPLIED]))
            character._SendSignal(COMSIG_HUMAN_PREFS_APPLIED, Dm.list(character));
    }

    /// Returns whether the parent mob should have the random hardcore settings enabled. Assumes it has a mind.
    // tg: code/modules/client/preferences.dm:547
    public virtual object should_be_random_hardcore(datum_job job = null, datum_mind mind = null)
    {
        if (!Dm.truthy(read_preference(typeof(datum_preference_toggle_random_hardcore))))
            return FALSE;
        if (Dm.truthy(Dm.bitand(job.job_flags, JOB_HEAD_OF_STAFF))) //No heads of staff
            return FALSE;
        // for(var/datum/antagonist/antag as anything in mind.antag_datums) if(antag.get_team()) return FALSE //No team antags:
        // the antagonists are the alpha's (ROADMAP M10, not-translated.md)
        if (Dm.truthy(length(mind.antag_datums)))
            Dm.NotTranslated("code/modules/client/preferences.dm:552", "should_be_random_hardcore: the team antagonists (antagonists, the alpha)");
        return TRUE;
    }

    /// Returns the default `randomise` variable ouptut
    // tg: code/modules/client/preferences.dm:568
    public virtual DmList get_default_randomization()
    {
        DmList default_randomization = Dm.list();

        foreach (object preference_key in Dm.each(GLOB.preference_entries_by_key))
        {
            datum_preference preference = GLOB.preference_entries_by_key[preference_key] as datum_preference;
            if (Dm.truthy(preference.is_randomizable()) && Dm.truthy(preference.randomize_by_default))
                default_randomization[preference_key] = RANDOM_ENABLED;
        }

        return default_randomization;
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/client/preferences.dm:477
    public static object safe_read_pref(client prefs_holder = null, Type pref_to_read = null)
    {
        if (prefs_holder == null)
            return FALSE;
        // DEVIATION(DV161): the preferences are not translated (client.prefs is null), so a preference reads its default value, which a
        // new player's savefile holds (tg: create_default_value): a toggle's default_value, max_chat_length's CHAT_MESSAGE_MAX_LENGTH,
        // and any other game preference's create_default_value (a volume's maximum, silicons SI4).
        if (Dm.ispath(pref_to_read, typeof(datum_preference_toggle)))
            return Dm.initial(pref_to_read, (datum_preference_toggle p) => p.default_value);
        if (pref_to_read == typeof(datum_preference_numeric_max_chat_length))
            return CHAT_MESSAGE_MAX_LENGTH;
        // read_preference's CRASH on a type with no entry (preferences/_preference.dm:270)
        if (GLOB.preference_entries[pref_to_read] is not datum_preference preference_entry)
            throw Dm.CRASH($"Preference type `{Dm.str(pref_to_read)}` is invalid!", "code/modules/client/preferences/_preference.dm", 270);
        return preference_entry.create_default_value();
    }

    /**
     * Get the given client's chat toggle prefs.
     *
     * Getter function for prefs.chat_toggles which guards against null client and null prefs.
     * The client object is fickle and can go null at times, so use this instead of directly accessing the var
     * if you want to ensure no runtimes.
     *
     * returns client.prefs.chat_toggles or FALSE if something went wrong.
     *
     * Arguments:
     * * client/prefs_holder - the client to get the chat_toggles pref from.
     */
    // tg: code/modules/client/preferences.dm:498
    public static object get_chat_toggles(object target = null)
    {
        if (ismob(target))
        {
            mob target_mob = target as mob;
            target = target_mob.client;
        }

        if (target == null)
            return NONE;

        // DEVIATION(DV161): the preferences are not translated: a new player's chat toggles, TOGGLES_DEFAULT_CHAT
        return TOGGLES_DEFAULT_CHAT;
    }
}

/// A preview of a character for use in the preferences menu
// tg: code/modules/client/preferences.dm:354
public partial class atom_movable_screen_map_view_char_preview : atom_movable_screen_map_view
{
    // tg: code/modules/client/preferences.dm:363
    [DmParams("mapload", "hud_owner", "preferences")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_preferences preferences = Dm.arg<datum_preferences>(__extra, 1);
        object dot = base.Initialize(mapload, __extra);
        this.preferences = preferences;
        return dot;
    }

    // tg: code/modules/client/preferences.dm:367
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref body);
        if (preferences != null)
            preferences.character_preview_view = null;
        preferences = null;
        return base.Destroy(force);
    }

    /// Updates the currently displayed body
    // tg: code/modules/client/preferences.dm:374
    public virtual async DmTask update_body()
    {
        if (body == null)
            create_body();
        else
            await body.wipe_state();

        appearance = await preferences.render_new_preview_appearance(body, show_job_clothes);
    }

    // tg: code/modules/client/preferences.dm:382
    public virtual void create_body()
    {
        QDEL_NULL(ref body);

        body = Dm.@new<mob_living_carbon_human_dummy>();
    }
}
