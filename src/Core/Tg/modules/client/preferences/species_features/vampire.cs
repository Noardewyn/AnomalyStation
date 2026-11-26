// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/species_features/vampire.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3b (D349): the whole file, the vampire's clan in Character Setup: the Inoculated take their department's house name as their
// surname. The choices' pictures reach the client as /icon recipes (DV44, universal_icon.cs). The var block is generated (D111).

// GLOBAL_LIST_EMPTY(vampire_houses) (__DEFINES/__globals.dm:51), expanded inline (rules 10.6, 11.1 (b))
public partial class datum_controller_global_vars : datum_controller
{
    ///list that stores a vampire house name for each department
    // tg: code/modules/client/preferences/species_features/vampire.dm:25
    public DmList vampire_houses;

    // tg: code/modules/client/preferences/species_features/vampire.dm:25  (GLOBAL_MANAGED(vampire_houses, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalvampire_houses()
    {
        vampire_houses = Dm.list();
        Dm.append(ref gvars_datum_init_order, "vampire_houses");
    }
}

// tg: code/modules/client/preferences/species_features/vampire.dm:2
public partial class datum_preference_choiced_vampire_status : datum_preference_choiced
{
    // tg: code/modules/client/preferences/species_features/vampire.dm:11
    public override object create_default_value()
    {
        return "Inoculated"; //eh, have em try out the mechanic first
    }

    // tg: code/modules/client/preferences/species_features/vampire.dm:14
    public override DmList init_possible_values()
    {
        return Dm.list("Inoculated", "Outcast");
    }

    // tg: code/modules/client/preferences/species_features/vampire.dm:17
    public override object icon_for(object value = null)
    {
        switch (Dm.str(value))
        {
            case "Inoculated":
                return Dm.icon(Dm.resource("icons/obj/drinks/drinks.dmi"), "bloodglass");
            case "Outcast":
                return Dm.icon(Dm.resource("icons/obj/medical/bloodpack.dmi"), "generic_bloodpack");
        }
        return null;
    }

    // tg: code/modules/client/preferences/species_features/vampire.dm:27
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        if (!HAS_TRAIT(target, TRAIT_BLOOD_CLANS))
            return;

        if (!Dm.eq(value, "Inoculated"))
            return;

        //find and setup the house (department) this vampire is joining
        datum_job_department vampire_house = null;
        datum_job vampire_job = SSjob.get_job(target.job) as datum_job;
        if (vampire_job == null) //no job or no mind LOSERS
            return;
        DmList valid_departments = Dm.sub(SSjob.joinable_departments.Copy(), Dm.list(typeof(datum_job_department_silicon), typeof(datum_job_department_undefined))) as DmList;
        foreach (datum_job_department potential_house in Dm.each<datum_job_department>(valid_departments))
        {
            if (Dm.truthy(Dm.@in(vampire_job, potential_house.department_jobs)))
            {
                vampire_house = potential_house;
                break;
            }
        }
        if (vampire_house == null) //sillycones
            return;
        if (!Dm.truthy(GLOB.vampire_houses[vampire_house.department_name]))
            GLOB.vampire_houses[vampire_house.department_name] = pick(GLOB.vampire_house_names);
        object house_name = GLOB.vampire_houses[vampire_house.department_name];

        //modify name (Platos Syrup > Platos de Lioncourt)
        float first_space_index = findtextEx(target.real_name, " ");
        string new_name = copytext(target.real_name, 1, first_space_index + 1);
        new_name += Dm.str(house_name);
        Waiting.NoSleep(target.fully_replace_character_name(target.real_name, new_name));
        return;
    }
}
