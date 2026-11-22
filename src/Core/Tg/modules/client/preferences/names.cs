// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/names.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice F, D267): the name preference and the real name and the backup human name; gaps J2 (D298): the clown's, mime's
// and chaplain's names; silicons SI2: the cyborg's and the AI's; gaps BR1: the hacker alias. The other names (the antagonists') come with their jobs.

/// A preference for a name. Used not just for normal names, but also for clown names, etc.
// tg: code/modules/client/preferences/names.dm:2
[DmPath("/datum/preference/name")]
public partial class datum_preference_name : datum_preference
{
    // tg: code/modules/client/preferences/names.dm:21
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        // Only real_name applies directly, everything else is applied by something else
        return;
    }

    // tg: code/modules/client/preferences/names.dm:26
    public override object deserialize(object input = null, datum_preferences preferences = null)
    {
        return reject_bad_name(Dm.text("[]", input), allow_numbers);
    }

    // tg: code/modules/client/preferences/names.dm:30
    public override object serialize(object input = null)
    {
        // `is_valid` should always be run before `serialize`, so it should not
        // be possible for this to return `null`.
        return reject_bad_name(input, allow_numbers);
    }

    // tg: code/modules/client/preferences/names.dm:80
    public override object included_in_randomization_flags(object randomize_flags = null)
    {
        return Dm.truthy(Dm.bitand(randomize_flags, RANDOMIZE_NAME)) ? TRUE : FALSE;
    }

    // tg: code/modules/client/preferences/names.dm:36
    public override object is_valid(object value = null, datum_preferences preferences = null)
    {
        return Dm.istext(value) && reject_bad_name(value, allow_numbers) != null ? TRUE : FALSE;
    }
}

/// A character's real name
// tg: code/modules/client/preferences/names.dm:41
[DmPath("/datum/preference/name/real_name")]
public partial class datum_preference_name_real_name : datum_preference_name
{
    // tg: code/modules/client/preferences/names.dm:47
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        target.real_name = Dm.str(value);
        target.name = Dm.str(value);
        target.log_mob_tag(Dm.text("TAG: [] RENAMED: []", target.tag, key_name(target)));
    }

    // tg: code/modules/client/preferences/names.dm:52
    public override object create_informed_default_value(datum_preferences preferences = null)
    {
        return generate_random_name_species_based(
            preferences.read_preference(typeof(datum_preference_choiced_gender)),
            TRUE,
            preferences.read_preference(typeof(datum_preference_choiced_species)));
    }

    // tg: code/modules/client/preferences/names.dm:59
    public override object deserialize(object input = null, datum_preferences preferences = null)
    {
        input = base.deserialize(input, preferences);
        if (!Dm.truthy(input))
            return input;

        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_humans_need_surnames))) && Dm.eq(preferences.read_preference(typeof(datum_preference_choiced_species)), typeof(datum_species_human)))
        {
            float first_space = Dm.findtext(input, " ");
            if (!Dm.truthy(first_space)) //we need a surname
                input = Dm.text("[] []", input, pick(GLOB.last_names));
            else if (first_space == length(input))
                input = Dm.text("[][]", input, pick(GLOB.last_names));
        }

        return reject_bad_name(input, allow_numbers);
    }
}

/// The name for a backup human, when nonhumans are made into head of staff
// tg: code/modules/client/preferences/names.dm:73
[DmPath("/datum/preference/name/backup_human")]
public partial class datum_preference_name_backup_human : datum_preference_name
{
    // tg: code/modules/client/preferences/names.dm:78
    public override object create_informed_default_value(datum_preferences preferences = null)
    {
        return generate_random_name(preferences.read_preference(typeof(datum_preference_choiced_gender)));
    }
}

// tg: code/modules/client/preferences/names.dm:83
public partial class datum_preference_name_clown : datum_preference_name
{
    // tg: code/modules/client/preferences/names.dm:89
    public override object create_default_value()
    {
        return pick(GLOB.clown_names);
    }
}

// tg: code/modules/client/preferences/names.dm:93
public partial class datum_preference_name_mime : datum_preference_name
{
    // tg: code/modules/client/preferences/names.dm:99
    public override object create_default_value()
    {
        return pick(GLOB.mime_names);
    }
}

// Silicons SI2 (D326): the cyborg's and the AI's name preferences.
// tg: code/modules/client/preferences/names.dm:102
public partial class datum_preference_name_cyborg : datum_preference_name
{
    // tg: code/modules/client/preferences/names.dm:112
    public override object create_default_value()
    {
        return DEFAULT_CYBORG_NAME;
    }
}

// tg: code/modules/client/preferences/names.dm:115
public partial class datum_preference_name_ai : datum_preference_name
{
    // tg: code/modules/client/preferences/names.dm:123
    public override object create_default_value()
    {
        return pick(GLOB.ai_names);
    }
}

// tg: code/modules/client/preferences/names.dm:127
public partial class datum_preference_name_religion : datum_preference_name
{
    // tg: code/modules/client/preferences/names.dm:134
    public override object create_default_value()
    {
        return pick(GLOB.religion_names);
    }
}

// tg: code/modules/client/preferences/names.dm:138
public partial class datum_preference_name_deity : datum_preference_name
{
    // tg: code/modules/client/preferences/names.dm:146
    public override object create_default_value()
    {
        return DEFAULT_DEITY;
    }
}

// tg: code/modules/client/preferences/names.dm:150
public partial class datum_preference_name_bible : datum_preference_name
{
    // tg: code/modules/client/preferences/names.dm:158
    public override object create_default_value()
    {
        return DEFAULT_BIBLE;
    }
}

/// The first name given to nuclear operative antagonists. The last name will be chosen by the team leader.
// tg: code/modules/client/preferences/names.dm:162  (syndicate N1b, D316)
public partial class datum_preference_name_operative_alias : datum_preference_name
{
    // tg: code/modules/client/preferences/names.dm:168
    public override object create_default_value()
    {
        return pick(GLOB.operative_aliases);
    }

    // tg: code/modules/client/preferences/names.dm:171
    public override object is_accessible(datum_preferences preferences = null)
    {
        object dot = base.is_accessible(preferences);
        if (!Dm.truthy(dot))
            return FALSE;

        // If one of the roles is ticked in the antag prefs menu, this option will show.
        TgState.Current.datum_preference_name_operative_alias__is_accessible__ops_roles ??= Dm.list(ROLE_OPERATIVE, ROLE_LONE_OPERATIVE, ROLE_OPERATIVE_MIDROUND, ROLE_CLOWN_OPERATIVE, ROLE_CLOWN_OPERATIVE_MIDROUND);
        if (Dm.truthy(length(Dm.bitand(TgState.Current.datum_preference_name_operative_alias__is_accessible__ops_roles, preferences.be_special))))
            return TRUE;

        return FALSE;
    }
}

/// The name to use while bitrunning
// Gaps BR1 (D339): the bitrunner's hacker alias, the avatar's name
// tg: code/modules/client/preferences/names.dm:185
public partial class datum_preference_name_hacker_alias : datum_preference_name
{
    // tg: code/modules/client/preferences/names.dm:192
    public override object create_default_value()
    {
        return pick(GLOB.hacker_aliases);
    }

    // tg: code/modules/client/preferences/names.dm:196
    public override object is_valid(object value = null, datum_preferences preferences = null)
    {
        return permissive_sanitize_name(value) != null ? TRUE : FALSE;
    }

    // tg: code/modules/client/preferences/names.dm:200
    public override object deserialize(object input = null, datum_preferences preferences = null)
    {
        return permissive_sanitize_name(input);
    }

    // tg: code/modules/client/preferences/names.dm:204
    public override object serialize(object input = null)
    {
        return permissive_sanitize_name(input);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/client/preferences/names.dm:176  var/static/list/ops_roles
    internal DmList datum_preference_name_operative_alias__is_accessible__ops_roles;
    // tg: code/modules/client/preferences/operative_species.dm:17  var/static/list/ops_roles
    internal DmList datum_preference_toggle_nuke_ops_species__is_accessible__ops_roles;
}

