// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/middleware/antags.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A3 (D316): the whole file, the lobby's Antagonists page. As the jobs' middleware (jobs.cs), the middleware datum
// itself is not translated: its procs are the preferences' own, prefixed antags_, and preferences.cs calls them where tg loops over its
// middleware. DEVIATION(DV44): the antagonists' spritesheet (create_spritesheets) is a list of each antagonist's picture, an /icon
// recipe the client draws, sent with the static data where tg sends the sheet as an asset; it is made the first time a menu asks for it
// (tg makes it at the assets' early init: shortcut, the dummies would shift every round's world ids from the start). DV346: an
// antagonist whose datum is inert (not ported) gets no picture, as tg's own antagonists' get_preview_icon overrides are theirs.

/// Antagonists that don't have a dynamic ruleset, but do have a preference
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/client/preferences/middleware/antags.dm:2  GLOBAL_LIST_INIT(non_ruleset_antagonists, list(...))
    public DmList non_ruleset_antagonists;

    // tg: code/modules/client/preferences/middleware/antags.dm:2  (GLOBAL_MANAGED(non_ruleset_antagonists, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalnon_ruleset_antagonists()
    {
        non_ruleset_antagonists = Dm.list(
            Dm.kv(ROLE_GLITCH, typeof(datum_antagonist_bitrunning_glitch)),
            Dm.kv(ROLE_FUGITIVE, typeof(datum_antagonist_fugitive)),
            Dm.kv(ROLE_LONE_OPERATIVE, typeof(datum_antagonist_nukeop_lone)),
            Dm.kv(ROLE_SENTIENCE, typeof(datum_antagonist_sentient_creature))
        );
        Dm.append(ref gvars_datum_init_order, "non_ruleset_antagonists");
    }
}

public partial class datum_preferences : datum
{
    // tg: code/modules/client/preferences/middleware/antags.dm:14
    public virtual DmList antags_get_ui_static_data(mob user = null)
    {
        if (!Dm.eq(current_window, PREFERENCE_TAB_CHARACTER_PREFERENCES))
            return Dm.list();

        DmList data = Dm.list();

        DmList selected_antags = Dm.list();

        foreach (object antag in Dm.each(be_special))
            Dm.append(ref selected_antags, serialize_antag_name(antag));

        data["selected_antags"] = selected_antags;

        DmList antag_bans = antags_get_antag_bans();
        if (Dm.truthy(antag_bans.len))
            data["antag_bans"] = antag_bans;

        DmList antag_days_left = antags_get_antag_days_left();
        if (Dm.truthy(antag_days_left?.len))
            data["antag_days_left"] = antag_days_left;

        return data;
    }

    // tg: code/modules/client/preferences/middleware/antags.dm:37
    // get_ui_assets: list(get_asset_datum(/datum/asset/spritesheet_batched/antagonists)), the antagonists' pictures (DV44: sent with the
    // static data by preferences.cs, antagonist_preview_icons below)

    // tg: code/modules/client/preferences/middleware/antags.dm:42
    public virtual object antags_set_antags(DmList @params = null, mob user = null)
    {
        // SHOULD_NOT_SLEEP(TRUE)

        object sent_antags = @params["antags"];
        object toggled = @params["toggled"];

        DmList antags = Dm.list();

        DmList serialized_antags = antags_get_serialized_antags();

        foreach (object sent_antag in Dm.each(sent_antags))
        {
            object special_role = serialized_antags[sent_antag];
            if (!Dm.truthy(special_role))
                continue;

            Dm.append(ref antags, special_role);
        }

        if (Dm.truthy(toggled))
            Dm.combine(ref be_special, antags);
        else
            Dm.remove(ref be_special, antags);

        // This is predicted on the client
        return FALSE;
    }

    // tg: code/modules/client/preferences/middleware/antags.dm:67
    public virtual DmList antags_get_antag_bans()
    {
        DmList antag_bans = Dm.list();

        object is_banned_from_all = is_banned_from(parent.ckey, ROLE_SYNDICATE);
        foreach (object antag_flag in Dm.each(get_all_antag_flags()))
        {
            if (Dm.truthy(is_banned_from_all) || Dm.truthy(is_banned_from(parent.ckey, antag_flag)))
                Dm.append(ref antag_bans, serialize_antag_name(antag_flag));
        }

        return antag_bans;
    }

    // tg: code/modules/client/preferences/middleware/antags.dm:77
    public virtual DmList antags_get_antag_days_left()
    {
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_use_age_restriction_for_jobs)))) // CONFIG_GET(flag/use_age_restriction_for_jobs)
            return null;

        DmList antag_days_left = Dm.list();
        foreach (object antag_flag in Dm.each(get_all_antag_flags()))
        {
            object days_needed = parent?.get_days_to_play_antag(antag_flag) is var days && Dm.truthy(days) ? days : 0f;
            if (Dm.gt(days_needed, 0))
                antag_days_left[serialize_antag_name(antag_flag)] = days_needed;
        }

        return antag_days_left;
    }

    // tg: code/modules/client/preferences/middleware/antags.dm:89
    public virtual DmList antags_get_serialized_antags()
    {
        DmList serialized_antags = null;

        if (serialized_antags == null)
        {
            serialized_antags = Dm.list();

            foreach (object special_role in Dm.each(get_all_antag_flags()))
                serialized_antags[serialize_antag_name(special_role)] = special_role;
        }

        return serialized_antags;
    }

    // DEVIATION(DV44): the antagonists' spritesheet's pictures, list(key = list(resource, icon state)) as the preferences' pictures
    // (_preference.cs compile_constant_data), from tg's create_spritesheets below
    public virtual async DmTask<DmList> antagonist_preview_icons()
    {
        TgState state = TgState.Current;
        if (state.antagonists_spritesheet == null)
            state.antagonists_spritesheet = await datum_asset_spritesheet_batched_antagonists_create_spritesheets();
        DmList icons = Dm.list();
        foreach (object key in Dm.each(state.antagonists_spritesheet))
        {
            icon picture = state.antagonists_spritesheet[key] as icon;
            icons[key] = Dm.list(picture.dm_resource(), picture.dm_state);
        }
        return icons;
    }

    // /datum/asset/spritesheet_batched/antagonists/create_spritesheets (antags.dm:146): the sheet's pictures by key (antag_icons), as
    // insert_icon would add them (DV44)
    // tg: code/modules/client/preferences/middleware/antags.dm:146
    private static async DmTask<DmList> datum_asset_spritesheet_batched_antagonists_create_spritesheets()
    {
        DmList antag_icons = Dm.list();
        DmList antagonists = GLOB.non_ruleset_antagonists.Copy();

        foreach (Type ruleset in Dm.each_cast<Type>(subtypesof(typeof(datum_dynamic_ruleset))))
        {
            object antagonist_type = Dm.initial(ruleset, "preview_antag_datum");
            object antag_flag = Dm.initial(ruleset, "pref_flag");
            if (antagonist_type == null || antag_flag == null)
                continue;

            // antag_flag is guaranteed to be unique for all non-RULESET_VARIATION rulesets
            // the ||= covers specifically those rulesets - prefer the first one over variations
            if (!Dm.truthy(antagonists[antag_flag]))
                antagonists[antag_flag] = antagonist_type;
        }

        DmList generated_icons = Dm.list();

        foreach (object antag_flag in Dm.each(antagonists))
        {
            Type antagonist_type = antagonists[antag_flag] as Type;

            // antag_flag is guaranteed to be unique by unit tests.
            object spritesheet_key = serialize_antag_name(antag_flag);

            if (generated_icons[antagonist_type] != null)
            {
                antag_icons[spritesheet_key] = generated_icons[antagonist_type];
                // make sure IconForge also knows to generate this duplicate for the given key
                // internal caching will prevent double generation of the same icon
                continue;
            }

            // DEVIATION(DV346): an inert antagonist (not ported) has no picture
            if (Attribute.IsDefined(antagonist_type, typeof(DmInertAttribute), inherit: false))
                continue;

            datum_antagonist antagonist = await Dm.new_async(antagonist_type) as datum_antagonist;
            object preview_icon = await antagonist.get_preview_icon();

            if (preview_icon == null)
                continue;

            qdel(antagonist);

            // preview_icons are not scaled at this stage INTENTIONALLY.
            // If an icon is not prepared to be scaled to that size, it looks really ugly, and this
            // makes it harder to figure out what size it *actually* is.

            // make sure no antagonists are rebuilt
            generated_icons[antagonist_type] = preview_icon;
            // store for screenshot tests
            antag_icons[spritesheet_key] = preview_icon;
        }
        return antag_icons;
    }
}

public static partial class GlobalProcs
{
    /**
     * Returns a list of all antag flags that are available to the player
     *
     * So this includes stuff like traitor, wizard, fugitive, but does not include wizard apprentice or hypnotized
     */
    // tg: code/modules/client/preferences/middleware/antags.dm:105
    public static object get_all_antag_flags()
    {
        ref DmList antag_flags = ref TgState.Current.get_all_antag_flags__antag_flags;
        if (antag_flags != null)
            return antag_flags;

        DmList ruleset_antags = Dm.list();
        foreach (Type ruleset in Dm.each_cast<Type>(subtypesof(typeof(datum_dynamic_ruleset))))
        {
            object antag_flag = Dm.initial(ruleset, "pref_flag");
            object jobban_flag = Dm.initial(ruleset, "jobban_flag");

            if (Dm.truthy(antag_flag))
                Dm.combine(ref ruleset_antags, antag_flag);
            if (Dm.truthy(jobban_flag))
                Dm.combine(ref ruleset_antags, jobban_flag);
        }

        antag_flags = Dm.bitor(ruleset_antags, GLOB.non_ruleset_antagonists) as DmList;
        return antag_flags;
    }

    /// Serializes an antag name to be used for preferences UI
    // tg: code/modules/client/preferences/middleware/antags.dm:193
    public static object serialize_antag_name(object antag_name = null)
    {
        // These are sent through CSS, so they need to be safe to use as class names.
        return LOWER_TEXT(sanitize_css_class_name(antag_name));
    }
}

public partial class client : datum
{
    /**
     * Returns the number of days more the client's account must be to play the passed in antag
     */
    // tg: code/modules/client/preferences/middleware/antags.dm:126
    public virtual object get_days_to_play_antag(object checked_antag_flag = null)
    {
        ref DmList antag_time_limits = ref TgState.Current.client__get_days_to_play_antag__antag_time_limits;
        if (antag_time_limits == null)
        {
            antag_time_limits = Dm.list();
            foreach (Type ruleset in Dm.each_cast<Type>(subtypesof(typeof(datum_dynamic_ruleset))))
            {
                object antag_flag = Dm.initial(ruleset, "pref_flag");
                object min_days = SSdynamic.get_config_value(ruleset, "minimum_required_age", Dm.initial(ruleset, "minimum_required_age")); // GET_DYNAMIC_CONFIG(ruleset, minimum_required_age)

                antag_time_limits[antag_flag] = min_days;
            }
        }

        return get_remaining_days(antag_time_limits[checked_antag_flag] is var limit && Dm.truthy(limit) ? limit : 0f);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/client/preferences/middleware/antags.dm:105  the proc-level var/static/list/antag_flags of get_all_antag_flags
    internal DmList get_all_antag_flags__antag_flags;

    // tg: code/modules/client/preferences/middleware/antags.dm:127  the proc-level var/static/list/antag_time_limits of get_days_to_play_antag
    internal DmList client__get_days_to_play_antag__antag_time_limits;

    // DV44: the antagonists' spritesheet's pictures (/datum/asset/spritesheet_batched/antagonists' antag_icons)
    internal DmList antagonists_spritesheet;
}
