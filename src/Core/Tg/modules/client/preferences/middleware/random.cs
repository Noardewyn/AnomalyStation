// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/middleware/random.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice F (D267): should_randomize (and included_in_randomization_flags, on /datum/preference in _preference.cs). M10x: the
// middleware's procs (the menu's randomization buttons), as the jobs' (jobs.cs): the middleware datum itself is not translated; its
// procs are the preferences' own, prefixed random_, and preferences.cs calls them where tg loops over its middleware.

public partial class datum_preferences : datum
{
    // tg: code/modules/client/preferences/middleware/random.dm:8
    public virtual DmList random_get_character_preferences(mob user = null)
    {
        DmList data = Dm.list();
        data["randomization"] = randomise;
        return data;
    }

    // tg: code/modules/client/preferences/middleware/random.dm:13
    public static DmList random_get_constant_data()
    {
        DmList randomizable = Dm.list();

        foreach (object preference_type in Dm.each(GLOB.preference_entries))
        {
            datum_preference preference = GLOB.preference_entries[preference_type] as datum_preference;
            if (!Dm.truthy(preference.is_randomizable()))
                continue;

            Dm.append(ref randomizable, preference.savefile_key);
        }

        DmList data = Dm.list();
        data["randomizable"] = randomizable;
        return data;
    }

    // tg: code/modules/client/preferences/middleware/random.dm:27
    public virtual async DmTask<object> random_randomize_character()
    {
        foreach (datum_preference preference in Dm.each<datum_preference>(get_preferences_in_priority_order()))
            if (Dm.truthy(should_randomize(preference)))
                write_preference(preference, preference.create_random_value(this));

        await character_preview_view.update_body();

        return TRUE;
    }

    // tg: code/modules/client/preferences/middleware/random.dm:38
    public virtual object random_set_random_preference(DmList @params = null, mob user = null)
    {
        object requested_preference_key = @params["preference"];
        object value = @params["value"];

        datum_preference requested_preference = GLOB.preference_entries_by_key[requested_preference_key] as datum_preference;
        if (requested_preference == null)
            return FALSE;

        if (!Dm.truthy(requested_preference.is_randomizable()))
            return FALSE;

        if (Dm.eq(value, RANDOM_ANTAG_ONLY))
            randomise[requested_preference_key] = RANDOM_ANTAG_ONLY;
        else if (Dm.eq(value, RANDOM_ENABLED))
            randomise[requested_preference_key] = RANDOM_ENABLED;
        else if (Dm.eq(value, RANDOM_DISABLED))
            Dm.remove(ref randomise, requested_preference_key);
        else
            return FALSE;

        return TRUE;
    }

    // tg: code/modules/client/preferences/middleware/random.dm:59
    public virtual object should_randomize(datum_preference preference = null, object is_antag = null)
    {
        if (!Dm.truthy(preference.is_randomizable()))
            return FALSE;

        object requested_randomization = randomise[preference.savefile_key];

        if (preference is datum_preference_name)
            requested_randomization = read_preference(typeof(datum_preference_choiced_random_name));

        if (Dm.eq(requested_randomization, RANDOM_ENABLED))
            return TRUE;
        if (Dm.eq(requested_randomization, RANDOM_ANTAG_ONLY))
            return is_antag;
        return FALSE;
    }
}
