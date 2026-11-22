// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/prisoner_crime.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Which crime is the prisoner permabrigged for. For fluff!
// tg: code/modules/client/preferences/prisoner_crime.dm:2
public partial class datum_preference_choiced_prisoner_crime : datum_preference_choiced
{
    // tg: code/modules/client/preferences/prisoner_crime.dm:8
    public override DmList init_possible_values()
    {
        return Dm.add(assoc_to_keys(GLOB.prisoner_crimes), "Random") as DmList;
    }

    // tg: code/modules/client/preferences/prisoner_crime.dm:11
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        return;
    }

    // tg: code/modules/client/preferences/prisoner_crime.dm:14
    public override object create_default_value()
    {
        return "Random";
    }

    // tg: code/modules/client/preferences/prisoner_crime.dm:17
    public override object is_accessible(datum_preferences preferences = null)
    {
        return Dm.truthy(base.is_accessible(preferences)) && Dm.truthy(highest_priority_job_is(preferences, typeof(datum_job_prisoner))) ? TRUE : FALSE;
    }
}

// tg: code/modules/client/preferences/prisoner_crime.dm:22
public partial class datum_controller_global_vars : datum_controller
{
    /// Types of Crimes Prisoners will have on their record roundstart.
    /// (They also can choose Random, which picks from these options... randomly!)
    // tg: code/modules/client/preferences/prisoner_crime.dm:22  GLOBAL_LIST_INIT(prisoner_crimes, init_prisoner_crimes())
    public DmList prisoner_crimes;

    // tg: code/modules/client/preferences/prisoner_crime.dm:22  (GLOBAL_MANAGED(prisoner_crimes, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalprisoner_crimes()
    {
        prisoner_crimes = init_prisoner_crimes();
        Dm.append(ref gvars_datum_init_order, "prisoner_crimes");
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/client/preferences/prisoner_crime.dm:24
    public static DmList init_prisoner_crimes()
    {
        DmList instances = Dm.list();
        foreach (Type crime_type in Dm.each_cast<Type>(subtypesof(typeof(datum_prisoner_crime))))
        {
            datum_prisoner_crime crime = Dm.@new(crime_type) as datum_prisoner_crime;
            instances[crime.name] = crime;
        }
        return instances;
    }
}

// tg: code/modules/client/preferences/prisoner_crime.dm:31
public partial class datum_prisoner_crime : datum
{
    // tg: code/modules/client/preferences/prisoner_crime.dm:39
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (tattoos == -1)
        {
            tattoos = 1;
            stack_trace(Dm.text("\"[]\" prisoner_crime does not have a set amount of tattoos!", this), "code/modules/client/preferences/prisoner_crime.dm", 43);
        }
    }
}
