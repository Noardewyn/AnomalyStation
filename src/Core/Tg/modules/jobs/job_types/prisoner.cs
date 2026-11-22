// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/prisoner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4a P5, the lead): New, which SSjob's setup_occupations runs, and the head of its joining handler. A prisoner's
// crime record comes with the late join and the records (M10).

// tg: code/modules/jobs/job_types/prisoner.dm:1
public partial class datum_job_prisoner : datum_job
{
    // tg: code/modules/jobs/job_types/prisoner.dm:28
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        RegisterSignal(SSdcs, COMSIG_GLOB_CREWMEMBER_JOINED, nameof(handle_prisoner_joining));
    }

    // tg: code/modules/jobs/job_types/prisoner.dm:32
    [SignalHandler]
    public virtual void handle_prisoner_joining(datum source = null, mob_living crewmember = null, object rank = null)
    {
        if (!Dm.eq(rank, title))
            return; //not a prisoner

        object crime_name = crewmember.client?.prefs?.read_preference(typeof(datum_preference_choiced_prisoner_crime));
        if (!Dm.truthy(crime_name))
        {
            stack_trace(Dm.text("[] joined as a Prisoner without having a prisoner crime set.", crewmember), "code/modules/jobs/job_types/prisoner.dm", 39);
            crime_name = pick(assoc_to_keys(GLOB.prisoner_crimes) as DmList);
        }
        else if (Dm.eq(crime_name, "Random"))
        {
            crime_name = pick(assoc_to_keys(GLOB.prisoner_crimes) as DmList);
        }

        datum_prisoner_crime crime = GLOB.prisoner_crimes[crime_name] as datum_prisoner_crime;
        datum_crime past_crime = Dm.@new<datum_crime>(crime.name, crime.desc, "Central Command", "Indefinite.");
        datum_record_crew target_record = find_record(crewmember.real_name) as datum_record_crew;
        Dm.append(ref target_record.crimes, past_crime);
        target_record.recreate_manifest_photos(add_height_chart: TRUE);
        to_chat(crewmember, span_warning(I18n.T("You are imprisoned for \"[]\".", crime_name)));
        crewmember.add_mob_memory(typeof(datum_memory_key_permabrig_crimes), Dm.named("crimes", crime_name));
    }
}

// tg: code/modules/jobs/job_types/prisoner.dm:53
public partial class datum_outfit_job_prisoner : datum_outfit_job
{
    // tg: code/modules/jobs/job_types/prisoner.dm:65
    public override async DmTask pre_equip(mob_living_carbon_human user = null, float? visuals_only = null)
    {
        await base.pre_equip(user, visuals_only);
        if (prob(1)) // D BOYYYYSSSSS
            head = typeof(obj_item_clothing_head_beanie_black_dboy);
    }

    // tg: code/modules/jobs/job_types/prisoner.dm:70
    public override async DmTask post_equip(mob_living_carbon_human new_prisoner = null, float? visuals_only = null)
    {
        await base.post_equip(new_prisoner, visuals_only);

        object crime_name = new_prisoner.client?.prefs?.read_preference(typeof(datum_preference_choiced_prisoner_crime));
        datum_prisoner_crime crime = crime_name == null ? null : GLOB.prisoner_crimes[crime_name] as datum_prisoner_crime;
        if (crime == null)
            return;
        // the crime's tattoos from SSpersistence.prison_tattoos_to_use (:77-83): the persisted art (the tattoo kit, not ported)
        Dm.Deferred("code/modules/jobs/job_types/prisoner.dm:77", "post_equip: the prison tattoos (SSpersistence, /datum/component/tattoo)");
    }
}
