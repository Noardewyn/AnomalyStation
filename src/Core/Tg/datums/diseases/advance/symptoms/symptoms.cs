// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/symptoms.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341): the symptom base. Its id is its index in SSdisease's symptom list; it activates at its own
// delay, is remedied while its cure is in the host, and does nothing once neutered. The var block is generated (D111).

// Symptoms are the effects that engineered advanced diseases do.

// tg: code/datums/diseases/advance/symptoms/symptoms.dm:3
public partial class datum_symptom : datum
{
    // tg: code/datums/diseases/advance/symptoms/symptoms.dm:49
    public override async DmTask New(params object[] __extra)
    {
        DmList S = datum_controller_subsystem_disease.list_symptoms;
        for (float i = 1; i <= S.len; i++)
        {
            if (Dm.eq(type, S[i]))
            {
                id = Dm.text("[]", i);
                return;
            }
        }
        await DmTask.CompletedTask;
        throw Dm.CRASH("We couldn't assign an ID!", "code/datums/diseases/advance/symptoms/symptoms.dm", 55);
    }

    ///Called when processing of the advance disease that holds this symptom infects a host and upon each Refresh() of that advance disease.
    // tg: code/datums/diseases/advance/symptoms/symptoms.dm:58
    public virtual object Start(datum_disease_advance advanced_disease = null)
    {
        if (Dm.truthy(neutered))
            return FALSE;
        if (Dm.@in(name, advanced_disease.affected_mob.symptom_resistances))
            symptom_delay *= 1.75f;
        return TRUE;
    }

    ///Called when the advance disease is going to be deleted or when the advance disease stops processing.
    // tg: code/datums/diseases/advance/symptoms/symptoms.dm:66
    public virtual async DmTask<object> End(datum_disease_advance A = null)
    {
        await DmTask.CompletedTask;
        if (Dm.truthy(neutered))
            return FALSE;
        return TRUE;
    }

    // tg: code/datums/diseases/advance/symptoms/symptoms.dm:71
    public virtual async DmTask<object> Activate(datum_disease_advance advanced_disease = null)
    {
        await DmTask.CompletedTask;
        if (Dm.truthy(neutered))
            return FALSE;
        if (Dm.truthy(required_organ))
        {
            if (!Dm.truthy(advanced_disease.has_required_infectious_organ(advanced_disease.affected_mob, required_organ)))
                return FALSE;
        }
        if (symptom_cure != null)
        {
            if (symptom_cure != null && Dm.truthy(advanced_disease.affected_mob.has_reagent(symptom_cure)))
            {
                remedied = TRUE;
                return FALSE;
            }
            remedied = FALSE;
        }
        if (world.time < next_activation)
            return FALSE;
        else
        {
            next_activation = world.time + rand(symptom_delay * (1 - delay_variation) * SECONDS, symptom_delay * (1 + delay_variation) * SECONDS) * DISEASE_SYMPTOM_FREQUENCY_MODIFIER;
            return TRUE;
        }
    }

    // tg: code/datums/diseases/advance/symptoms/symptoms.dm:88
    public virtual object on_stage_change(datum_disease_advance A = null)
    {
        if (Dm.truthy(neutered))
            return FALSE;
        return TRUE;
    }

    // tg: code/datums/diseases/advance/symptoms/symptoms.dm:93
    public virtual object Copy()
    {
        datum_symptom new_symp = (datum_symptom)Dm.@new(type);
        new_symp.name = name;
        new_symp.id = id;
        new_symp.neutered = neutered;
        return new_symp;
    }

    // tg: code/datums/diseases/advance/symptoms/symptoms.dm:100
    public virtual void generate_threshold_desc()
    {
        return;
    }

    ///Overload when a symptom needs to be active before processing, like changing biotypes.
    // tg: code/datums/diseases/advance/symptoms/symptoms.dm:104
    public virtual void OnAdd(datum_disease_advance A = null)
    {
        return;
    }

    ///Overload for running after processing.
    // tg: code/datums/diseases/advance/symptoms/symptoms.dm:108
    public virtual void OnRemove(datum_disease_advance A = null)
    {
        return;
    }

    /**
     * Returns a list for all of the traits of this symptom.
     *
     *
     * @returns {list} symptom - The desired symptoms as a list.
     */
    // tg: code/datums/diseases/advance/symptoms/symptoms.dm:117
    public virtual object get_symptom_data()
    {
        DmList data = Dm.list();
        data["name"] = name;
        data["desc"] = desc;
        data["stealth"] = stealth;
        data["resistance"] = resistance;
        data["stage_speed"] = stage_speed;
        data["transmission"] = transmittable;
        data["level"] = level;
        data["neutered"] = neutered;
        data["threshold_desc"] = threshold_descs;
        if (symptom_cure != null)
        {
            // tg reads cure.name off the typepath held in a /datum/reagent var; the port reads the type's initial name.
            Type cure = symptom_cure;
            data["symptom_cure"] = Dm.initial(cure, (datum_reagent r) => r.name);
        }
        else
            data["symptom_cure"] = "Nothing";
        data["cure_color"] = cure_color;
        return data;
    }

    /// Check if we can generate randomly
    // tg: code/datums/diseases/advance/symptoms/symptoms.dm:137
    public virtual object can_generate_randomly()
    {
        return naturally_occuring;
    }
}
