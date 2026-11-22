// Translated from tgstation (snapshot 1d99bd7f): code/controllers/configuration/config_entry.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The VV procs of config_entry.dm (can_vv_get :52, vv_edit_var :57, :95, :124, :297) are not translated: VV is admin tooling
// (translation-rules.md 13) and their /datum roots do not exist in the port.

// tg: code/controllers/configuration/config_entry.dm:1
[DmPath("/datum/config_entry")]
public partial class datum_config_entry : datum
{
    public datum_config_entry()
    {
        /// Do not instantiate if type matches this
        abstract_type = typeof(datum_config_entry);
    }

    /// Read-only, this is determined by the last portion of the derived entry type
    // tg: code/controllers/configuration/config_entry.dm:5
    public object name;
    /// The configured value for this entry. This shouldn't be initialized in code, instead set default
    // tg: code/controllers/configuration/config_entry.dm:7
    public object config_entry_value;
    /// Read-only default value for this config entry, used for resetting value to defaults when necessary. This is what config_entry_value is initially set to
    // tg: code/controllers/configuration/config_entry.dm:9
    public object @default;
    /// The file which this was loaded from, if any
    // tg: code/controllers/configuration/config_entry.dm:11
    public object resident_file;
    /// Set to TRUE if the default has been overridden by a config entry
    // tg: code/controllers/configuration/config_entry.dm:13
    public float modified = FALSE;
    /// The config name of a configuration type that depricates this, if it exists
    // tg: code/controllers/configuration/config_entry.dm:15
    public Type deprecated_by;
    /// The /datum/config_entry type that supersedes this one
    // tg: code/controllers/configuration/config_entry.dm:17
    public float protection = NONE;
    /// Force validate and set on VV. VAS proccall guard will run regardless.
    // tg: code/controllers/configuration/config_entry.dm:19
    public float vv_VAS = TRUE;
    /// Controls if error is thrown when duplicate configuration values for this entry type are encountered
    // tg: code/controllers/configuration/config_entry.dm:21
    public float dupes_allowed = FALSE;
    /// Stores the original protection configuration, used for set_default()
    // tg: code/controllers/configuration/config_entry.dm:23
    public object default_protection;

    // tg: code/controllers/configuration/config_entry.dm:25
    public override async DmTask New(params object[] __extra)
    {
        if (type == abstract_type)
            throw Dm.CRASH(Dm.text("Abstract config entry [] instatiated!", type), "code/controllers/configuration/config_entry.dm", 27);
        name = LOWER_TEXT(type2top(type));
        default_protection = protection;
        set_default();
    }

    // tg: code/controllers/configuration/config_entry.dm:32
    [DmParams()]
    public override object Destroy(float? force = null)
    {
        config.RemoveEntry(this);
        return base.Destroy(force);
    }

    /**
     * Returns the value of the configuration datum to its default, used for resetting a config value. Note this also sets the protection back to default.
     */
    // tg: code/controllers/configuration/config_entry.dm:39
    public virtual void set_default()
    {
        if (Dm.truthy(Dm.bitand(protection, CONFIG_ENTRY_LOCKED)) && Dm.truthy(IsAdminAdvancedProcCall()))
        {
            Dm.NotTranslated("code/controllers/configuration/config_entry.dm:41", "set_default: log_admin_private of an admin proc call on a locked entry (admin logging, M10+)");
            return;
        }
        if (islist(@default))
        {
            DmList L = @default as DmList;
            config_entry_value = L.Copy();
        }
        else
            config_entry_value = @default;
        protection = Dm.num(default_protection);
        resident_file = null;
        modified = FALSE;
    }

    // tg: code/controllers/configuration/config_entry.dm:73
    public virtual object VASProcCallGuard(object str_val = null)
    {
        object dot = null;
        dot = Dm.num(!(Dm.truthy(Dm.bitand(protection, CONFIG_ENTRY_LOCKED)) && Dm.truthy(IsAdminAdvancedProcCall())));
        if (!Dm.truthy(dot))
            Dm.NotTranslated("code/controllers/configuration/config_entry.dm:76", "VASProcCallGuard: log_admin_private of an admin proc call on a locked entry (admin logging, M10+)");
        return dot;
    }

    // tg: code/controllers/configuration/config_entry.dm:78
    public virtual async DmTask<object> ValidateAndSet(object str_val = null)
    {
        VASProcCallGuard(str_val);
        throw Dm.CRASH("Invalid config entry type!", "code/controllers/configuration/config_entry.dm", 80);
    }

    // tg: code/controllers/configuration/config_entry.dm:82
    public virtual object ValidateListEntry(object key_name = null, object key_value = null)
    {
        return TRUE;
    }

    // tg: code/controllers/configuration/config_entry.dm:85
    public virtual object DeprecationUpdate(object value = null)
    {
        return null;
    }
}

// tg: code/controllers/configuration/config_entry.dm:88
[DmPath("/datum/config_entry/string")]
public partial class datum_config_entry_string : datum_config_entry
{
    public datum_config_entry_string()
    {
        @default = "";
        abstract_type = typeof(datum_config_entry_string);
    }

    // tg: code/controllers/configuration/config_entry.dm:91
    public float auto_trim = TRUE;
    /// whether the string will be lowercased on ValidateAndSet or not.
    // tg: code/controllers/configuration/config_entry.dm:93
    public float lowercase = FALSE;

    // tg: code/controllers/configuration/config_entry.dm:98
    public override async DmTask<object> ValidateAndSet(object str_val = null)
    {
        if (!Dm.truthy(VASProcCallGuard(str_val)))
            return FALSE;
        config_entry_value = (Dm.truthy(auto_trim) ? trim(str_val) : str_val);
        if (Dm.truthy(lowercase))
            config_entry_value = LOWER_TEXT(config_entry_value);
        return TRUE;
    }
}

// tg: code/controllers/configuration/config_entry.dm:106
[DmPath("/datum/config_entry/number")]
public partial class datum_config_entry_number : datum_config_entry
{
    public datum_config_entry_number()
    {
        @default = 0;
        abstract_type = typeof(datum_config_entry_number);
    }

    // tg: code/controllers/configuration/config_entry.dm:109
    public float integer = TRUE;
    // tg: code/controllers/configuration/config_entry.dm:110
    public float max_val = INFINITY;
    // tg: code/controllers/configuration/config_entry.dm:111
    public float min_val = -INFINITY;

    // tg: code/controllers/configuration/config_entry.dm:113
    public override async DmTask<object> ValidateAndSet(object str_val = null)
    {
        if (!Dm.truthy(VASProcCallGuard(str_val)))
            return FALSE;
        object temp = text2num(trim(str_val));
        if (temp != null)
        {
            config_entry_value = clamp((Dm.truthy(integer) ? (object)round(Dm.num(temp)) : temp), min_val, max_val);
            if (!Dm.eq(config_entry_value, temp) && !Dm.truthy(Dm.bitand(datum_flags, DF_VAR_EDITED)))
                log_config(Dm.text("Changing [] from [] to []!", name, temp, config_entry_value));
            return TRUE;
        }
        return FALSE;
    }
}

// tg: code/controllers/configuration/config_entry.dm:128
[DmPath("/datum/config_entry/flag")]
public partial class datum_config_entry_flag : datum_config_entry
{
    public datum_config_entry_flag()
    {
        @default = FALSE;
        abstract_type = typeof(datum_config_entry_flag);
    }

    // tg: code/controllers/configuration/config_entry.dm:132
    public override async DmTask<object> ValidateAndSet(object str_val = null)
    {
        if (!Dm.truthy(VASProcCallGuard(str_val)))
            return FALSE;
        config_entry_value = Dm.num(!Dm.eq(text2num(trim(str_val)), 0));
        return TRUE;
    }
}

/// List config entry, used for configuring a list of strings
// tg: code/controllers/configuration/config_entry.dm:139
[DmPath("/datum/config_entry/str_list")]
public partial class datum_config_entry_str_list : datum_config_entry
{
    public datum_config_entry_str_list()
    {
        abstract_type = typeof(datum_config_entry_str_list);
        dupes_allowed = TRUE;
    }

    // tg: code/controllers/configuration/config_entry.dm:141 (an object-creating default, rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        @default = Dm.list();
    }

    /// whether the string elements will be lowercased on ValidateAndSet or not.
    // tg: code/controllers/configuration/config_entry.dm:144
    public float lowercase = FALSE;

    // tg: code/controllers/configuration/config_entry.dm:146
    public override async DmTask<object> ValidateAndSet(object str_val = null)
    {
        if (!Dm.truthy(VASProcCallGuard(str_val)))
            return FALSE;
        str_val = trim(str_val);
        if (!Dm.eq(str_val, ""))
            Dm.append(ref config_entry_value, (Dm.truthy(lowercase) ? LOWER_TEXT(str_val) : str_val));
        return TRUE;
    }
}

// tg: code/controllers/configuration/config_entry.dm:154
[DmPath("/datum/config_entry/number_list")]
public partial class datum_config_entry_number_list : datum_config_entry
{
    public datum_config_entry_number_list()
    {
        abstract_type = typeof(datum_config_entry_number_list);
    }

    // tg: code/controllers/configuration/config_entry.dm:156 (an object-creating default, rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        @default = Dm.list();
    }

    // tg: code/controllers/configuration/config_entry.dm:158
    public override async DmTask<object> ValidateAndSet(object str_val = null)
    {
        if (!Dm.truthy(VASProcCallGuard(str_val)))
            return FALSE;
        str_val = trim(str_val);
        DmList new_list = Dm.list();
        DmList values = splittext(str_val, " ");
        foreach (object I in Dm.each(values))
        {
            object temp = text2num(I);
            if (temp == null)
                return FALSE;
            Dm.append(ref new_list, temp);
        }
        if (!Dm.truthy(new_list.len))
            return FALSE;
        config_entry_value = new_list;
        return TRUE;
    }
}

// tg: code/controllers/configuration/config_entry.dm:174
[DmPath("/datum/config_entry/keyed_list")]
public partial class datum_config_entry_keyed_list : datum_config_entry
{
    public datum_config_entry_keyed_list()
    {
        abstract_type = typeof(datum_config_entry_keyed_list);
        dupes_allowed = TRUE;
        vv_VAS = FALSE; //VAS will not allow things like deleting from lists, it'll just bug horribly.
    }

    // tg: code/controllers/configuration/config_entry.dm:176 (an object-creating default, rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        @default = Dm.list();
    }

    // tg: code/controllers/configuration/config_entry.dm:179
    public object key_mode;
    // tg: code/controllers/configuration/config_entry.dm:180
    public object value_mode;
    // tg: code/controllers/configuration/config_entry.dm:181
    public string splitter = " ";
    /// whether the key names will be lowercased on ValidateAndSet or not.
    // tg: code/controllers/configuration/config_entry.dm:183
    public float lowercase_key = TRUE;

    // tg: code/controllers/configuration/config_entry.dm:185
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (key_mode == null || value_mode == null)
            throw Dm.CRASH(Dm.text("Keyed list of type [] created with null key or value mode!", type), "code/controllers/configuration/config_entry.dm", 188);
    }

    // tg: code/controllers/configuration/config_entry.dm:190
    public override async DmTask<object> ValidateAndSet(object str_val = null)
    {
        if (!Dm.truthy(VASProcCallGuard(str_val)))
            return FALSE;

        str_val = trim(str_val);

        DmList new_entry = parse_key_and_value(str_val) as DmList;

        object new_key = new_entry["config_key"];
        object new_value = new_entry["config_value"];

        if (new_value != null && new_key != null && Dm.truthy(ValidateListEntry(new_key, new_value)))
        {
            ((DmList)config_entry_value)[new_key] = new_value;
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/controllers/configuration/config_entry.dm:206
    public virtual object parse_key_and_value(object option_string = null)
    {
        // Blank or null option string? Bad mojo!
        if (!Dm.truthy(option_string))
        {
            log_config("ERROR: Keyed list config tried to parse with no key or value data.");
            return null;
        }

        DmList config_entry_words = splittext(option_string, splitter);
        object config_value = null;
        object config_key = null;
        float is_ambiguous = FALSE;

        // If this config entry's value mode is flag, the value can either be TRUE or FALSE.
        // However, the config supports implicitly setting a config entry to TRUE by omitting the value.
        // This value mode should also support config overrides disabling it too.
        // The following code supports config entries as such:
        // Implicitly enable the config entry: CONFIG_ENTRY config key goes here
        // Explicitly enable the config entry: CONFIG_ENTRY config key goes here 1
        // Explicitly disable the config entry: CONFIG_ENTRY config key goes here 0
        if (Dm.eq(value_mode, VALUE_MODE_FLAG))
        {
            object value = peek(config_entry_words);
            config_value = TRUE;

            if (Dm.eq(value, "0"))
            {
                config_key = jointext(config_entry_words, splitter, length(config_entry_words) - 1);
                config_value = FALSE;
                is_ambiguous = Dm.num(length(config_entry_words) > 2);
            }
            else if (Dm.eq(value, "1"))
            {
                config_key = jointext(config_entry_words, splitter, length(config_entry_words) - 1);
                is_ambiguous = Dm.num(length(config_entry_words) > 2);
            }
            else
            {
                config_key = option_string;
                is_ambiguous = Dm.num(length(config_entry_words) > 1);
            }
        }
        // Else it has to be a key value pair and we parse it under that assumption.
        else
        {
            // If config_entry_words only has 1 or 0 words in it and isn't value_mode == VALUE_MODE_FLAG then it's an invalid config entry.
            if (length(config_entry_words) <= 1)
            {
                log_config(Dm.text("ERROR: Could not parse value from config entry string: []", option_string));
                return null;
            }

            config_value = pop(config_entry_words);
            config_key = jointext(config_entry_words, splitter);

            if (Dm.truthy(lowercase_key))
                config_key = LOWER_TEXT(config_key);

            is_ambiguous = Dm.num(length(config_entry_words) > 2);
        }

        config_key = validate_config_key(config_key);
        config_value = validate_config_value(config_value);

        // If there are multiple splitters, it's definitely ambiguous and we'll warn about how we parsed it. Helps with debugging config issues.
        if (Dm.truthy(is_ambiguous))
            log_config(Dm.text("WARNING: Multiple splitter characters (\"[]\") found. Using \"[]\" as config key and \"[]\" as config value.", splitter, config_key, config_value));

        return Dm.list(Dm.kv("config_key", config_key), Dm.kv("config_value", config_value));
    }

    /// Takes a given config key and validates it. If successful, returns the formatted key. If unsuccessful, returns null.
    // tg: code/controllers/configuration/config_entry.dm:263
    public virtual object validate_config_key(object key = null)
    {
        object sw__1 = key_mode;
        if (Dm.eq(sw__1, KEY_MODE_TEXT))
            return key;
        else if (Dm.eq(sw__1, KEY_MODE_TYPE))
        {
            if (ispath(key))
                return key;

            object key_path = text2path((string)key);
            if (key_path == null)
            {
                log_config(Dm.text("ERROR: Invalid KEY_MODE_TYPE typepath. Is not a valid typepath: []", key));
                return null;
            }

            return key_path;
        }
        return null;
    }

    /// Takes a given config value and validates it. If successful, returns the formatted key. If unsuccessful, returns null.
    // tg: code/controllers/configuration/config_entry.dm:280
    public virtual object validate_config_value(object value = null)
    {
        object sw__1 = value_mode;
        if (Dm.eq(sw__1, VALUE_MODE_FLAG))
            return value;
        else if (Dm.eq(sw__1, VALUE_MODE_NUM))
        {
            if (isnum(value))
                return value;

            object value_num = text2num(value);
            if (value_num == null)
            {
                log_config(Dm.text("ERROR: Invalid VALUE_MODE_NUM number. Could not parse a valid number: []", value));
                return null;
            }

            return value_num;
        }
        else if (Dm.eq(sw__1, VALUE_MODE_TEXT))
            return value;
        return null;
    }
}
