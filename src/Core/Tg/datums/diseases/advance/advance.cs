// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/advance.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341): the advance disease. Its stats are the sums of its symptoms' (stealth, resistance, stage speed,
// transmission, severity); its id is the sorted symptom ids; the archive in SSdisease names it; mixing, evolving, devolving and
// neutering; the symptom immunities after a cure. The var block is generated (D111). The [DmPath] is on the part in
// code/__HELPERS/logging/virus.dm, the earliest .dme file that declares /datum/disease/advance (rules 2.3).

/*

	Advance Disease is a system for medical to Engineer their own disease with symptoms that have effects and properties
	which add onto the overall disease.

	If you need help with creating new symptoms or expanding the advance disease, ask for Giacom on #coderbus.

*/

// tg: code/datums/diseases/advance/advance.dm:19
public partial class datum_disease_advance : datum_disease
{
    /*

        OLD PROCS

     */

    // tg: code/datums/diseases/advance/advance.dm:43
    public override async DmTask New(params object[] __extra)
    {
        await Refresh();
    }

    // tg: code/datums/diseases/advance/advance.dm:46
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(processing))
        {
            foreach (datum_symptom S in Dm.each<datum_symptom>(symptoms))
                Waiting.NoSleep(S.End(this));
        }
        return base.Destroy(force);
    }

    // tg: code/datums/diseases/advance/advance.dm:52
    public override async DmTask<object> try_infect(mob_living infectee = null, float? make_copy = null)
    {
        make_copy ??= TRUE;
        //see if we are more transmittable than enough diseases to replace them
        //diseases replaced in this way do not confer immunity
        DmList advance_diseases = Dm.list();
        foreach (datum_disease_advance P in Dm.each<datum_disease_advance>(infectee.diseases))
            Dm.append(advance_diseases, P);
        float replace_num = advance_diseases.len + 1 - DISEASE_LIMIT; //amount of diseases that need to be removed to fit this one
        if (replace_num > 0)
        {
            sortTim(advance_diseases, "/proc/" + nameof(GlobalProcs.cmp_advdisease_resistance_asc));
            for (float i = 1, i__end = replace_num; i <= i__end; i++)
            {
                datum_disease_advance competition = (datum_disease_advance)advance_diseases[i];
                if (!Dm.truthy(competition.bypasses_immunity))
                {
                    if (Dm.truthy(bypasses_immunity)) //viruses with bypasses_immunity get a free pass on beating normal advanced diseases
                        await competition.cure(FALSE);
                    if (Dm.gt(totalTransmittable(), competition.totalResistance()))
                        await competition.cure(FALSE);
                    else
                        return FALSE; //we are not strong enough to bully our way in
                }
            }
        }
        await infect(infectee, make_copy);
        return TRUE;
    }

    // tg: code/datums/diseases/advance/advance.dm:73
    public override object get_recovery_failure_chance()
    {
        return clamp((Dm.num(properties["resistance"]) * 1.5f), 0, 50);
    }

    // Randomly pick a symptom to activate.
    // tg: code/datums/diseases/advance/advance.dm:77
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(length(symptoms)))
            return dot;

        if (!Dm.truthy(processing))
        {
            processing = TRUE;
            foreach (object s in Dm.each(symptoms))
            {
                datum_symptom symptom_datum = (datum_symptom)s;
                if (Dm.truthy(symptom_datum.Start(this))) //this will return FALSE if the symptom is neutered
                    symptom_datum.next_activation = world.time + (rand(symptom_datum.symptom_delay * (1 - symptom_datum.delay_variation) * SECONDS, symptom_datum.symptom_delay * (1 + symptom_datum.delay_variation) * SECONDS) * DISEASE_SYMPTOM_FREQUENCY_MODIFIER);
                symptom_datum.on_stage_change(this);
            }
        }

        foreach (object s in Dm.each(symptoms))
        {
            datum_symptom symptom_datum = (datum_symptom)s;
            if (!Dm.truthy(symptom_datum.neutered))
                await symptom_datum.Activate(this);
        }
        return dot;
    }


    // Tell symptoms stage changed
    // tg: code/datums/diseases/advance/advance.dm:100
    public override async DmTask<object> update_stage(object new_stage = null)
    {
        await base.update_stage(new_stage);
        foreach (datum_symptom S in Dm.each<datum_symptom>(symptoms))
            S.on_stage_change(this);
        return null;
    }

    // Compares type then ID.
    // tg: code/datums/diseases/advance/advance.dm:106
    public override object IsSame(datum_disease D = null)
    {

        if (!(D is datum_disease_advance))
            return FALSE;

        if (!Dm.eq(GetDiseaseID(), D.GetDiseaseID()))
            return FALSE;
        return TRUE;
    }

    // Returns the advance disease with a different reference memory.
    // tg: code/datums/diseases/advance/advance.dm:116
    public override async DmTask<object> Copy()
    {
        datum_disease_advance A = (datum_disease_advance)await base.Copy();
        QDEL_LIST(A.symptoms);
        foreach (datum_symptom S in Dm.each<datum_symptom>(symptoms))
            Dm.append(A.symptoms, S.Copy());
        A.properties = properties.Copy();
        A.id = id;
        A.mutable = mutable;
        A.cure_text = cure_text;
        //this is a new disease starting over at stage 1, so processing is not copied
        return A;
    }

    // tg: code/datums/diseases/advance/advance.dm:128
    public override object has_cure()
    {
        if (!Dm.truthy(Dm.bitand(disease_flags, CURABLE | CHRONIC)))
            return FALSE;
        float remedied_symptoms = 0;
        float non_remedied_symptoms = 0;
        foreach (datum_symptom each_symptom in Dm.each_cast<datum_symptom>(symptoms))
        {
            if (Dm.truthy(each_symptom.remedied)) // Neutered symptoms can't be remedied, so don't worry about that here
                remedied_symptoms++;
            else if (!Dm.truthy(each_symptom.neutered))
                non_remedied_symptoms++;
        }
        if (remedied_symptoms + non_remedied_symptoms == 0) // In case all symptoms are neutered, we don't want to divide by 0
            return 0;
        return remedied_symptoms / (remedied_symptoms + non_remedied_symptoms);
    }

    // Mix the symptoms of two diseases (the src and the argument)
    // tg: code/datums/diseases/advance/advance.dm:143
    public virtual void Mix(datum_disease_advance D = null)
    {
        if (!Dm.truthy(IsSame(D)))
        {
            DmList possible_symptoms = shuffle(D.symptoms) as DmList;
            foreach (datum_symptom S in Dm.each<datum_symptom>(possible_symptoms))
                AddSymptom((datum_symptom)S.Copy());
        }
    }

    // tg: code/datums/diseases/advance/advance.dm:149
    public virtual object HasSymptom(datum_symptom S = null)
    {
        foreach (datum_symptom symp in Dm.each<datum_symptom>(symptoms))
        {
            if (symp.type == S.type)
                return TRUE;
        }
        return FALSE;
    }

    // Will generate new unique symptoms, use this if there are none. Returns a list of symptoms that were generated.
    // tg: code/datums/diseases/advance/advance.dm:156
    public virtual object GenerateSymptoms(object level_min = null, object level_max = null, float? amount_get = null)
    {
        amount_get ??= 0;

        DmList dot = null;
        dot = Dm.list(); // Symptoms we generated.

        // Generate symptoms. By default, we only choose non-deadly symptoms.
        DmList possible_symptoms = Dm.list();
        foreach (object symp in Dm.each(datum_controller_subsystem_disease.list_symptoms))
        {
            datum_symptom S = (datum_symptom)Dm.@new((Type)symp);
            if (Dm.truthy(S.can_generate_randomly()) && Dm.ge(S.level, level_min) && Dm.le(S.level, level_max))
            {
                if (!Dm.truthy(HasSymptom(S)))
                    Dm.append(possible_symptoms, S);
            }
        }

        if (!Dm.truthy(possible_symptoms.len))
            return dot;

        // Random chance to get more than one symptom
        float number_of = Dm.num(amount_get);
        if (!Dm.truthy(amount_get))
        {
            number_of = 1;
            while (prob(20))
                number_of += 1;
        }

        for (float i = 1; number_of >= i && Dm.truthy(possible_symptoms.len); i++)
            Dm.append(dot, pick_n_take(possible_symptoms));
        return dot;
    }

    // tg: code/datums/diseases/advance/advance.dm:181
    public virtual async DmTask Refresh(float? new_name = null)
    {
        new_name ??= FALSE;
        GenerateProperties();
        assign_properties();
        if (Dm.truthy(processing) && Dm.truthy(symptoms?.len))
        {
            foreach (datum_symptom S in Dm.each<datum_symptom>(symptoms))
            {
                S.Start(this);
                S.on_stage_change(this);
            }
        }
        id = null;

        object the_id = GetDiseaseID();
        if (SSdisease != null && !Dm.truthy(SSdisease.archive_diseases[the_id])) // rules 10.6: a harness world may lack SSdisease
        {
            SSdisease.archive_diseases[the_id] = this; // So we don't infinite loop
            SSdisease.archive_diseases[the_id] = await Copy();
            if (Dm.truthy(new_name))
                AssignName();
        }
    }

    //Generate disease properties based on the effects. Returns an associated list.
    // tg: code/datums/diseases/advance/advance.dm:198
    public virtual void GenerateProperties()
    {
        properties = Dm.list(Dm.kv("resistance", 0), Dm.kv("stealth", 0), Dm.kv("stage_rate", 0), Dm.kv("transmittable", 0), Dm.kv("severity", 0));

        foreach (datum_symptom S in Dm.each<datum_symptom>(symptoms))
        {
            properties["resistance"] = Dm.add(properties["resistance"], S.resistance);
            properties["stealth"] = Dm.add(properties["stealth"], S.stealth);
            properties["stage_rate"] = Dm.add(properties["stage_rate"], S.stage_speed);
            properties["transmittable"] = Dm.add(properties["transmittable"], S.transmittable);
            if (!Dm.truthy(S.neutered))
                properties["severity"] = Dm.add(properties["severity"], S.severity); // severity is based on the sum of all non-neutered symptoms' severity
        }
        if (Dm.num(properties["severity"]) > 0)
        {
            properties["severity"] = Dm.num(properties["severity"]) + round((Dm.num(properties["resistance"]) / 12), 1);
            properties["severity"] = Dm.num(properties["severity"]) + round((Dm.num(properties["stage_rate"]) / 11), 1);
            properties["severity"] = Dm.num(properties["severity"]) + round((Dm.num(properties["transmittable"]) / 8), 1);
            properties["severity"] = round((Dm.num(properties["severity"]) / 2), 1);
            properties["severity"] = Dm.num(properties["severity"]) * ((float)symptoms.len / VIRUS_SYMPTOM_LIMIT); //fewer symptoms, less severity
            properties["severity"] = round(clamp(Dm.num(properties["severity"]), 1, 7), 1);
        }
    }

    // Assign the properties that are in the list.
    // tg: code/datums/diseases/advance/advance.dm:217
    public virtual void assign_properties()
    {

        if (Dm.truthy(properties?.len))
        {
            if (Dm.ge(properties["stealth"], properties["severity"]) && Dm.num(properties["severity"]) > 0)
                Dm.combine(ref visibility_flags, HIDDEN_SCANNER);
            else
                Dm.mask(ref visibility_flags, Dm.bitnot(HIDDEN_SCANNER));

            if (Dm.num(properties["transmittable"]) >= 11)
                set_spread(DISEASE_SPREAD_AIRBORNE);
            else if (Dm.num(properties["transmittable"]) >= 7)
                set_spread(DISEASE_SPREAD_CONTACT_SKIN);
            else if (Dm.num(properties["transmittable"]) >= 3)
                set_spread(DISEASE_SPREAD_CONTACT_FLUIDS);
            else
                set_spread(DISEASE_SPREAD_BLOOD);

            spreading_modifier = max(CEILING(0.4f * Dm.num(properties["transmittable"]), 1), 1);
            cure_chance = clamp(10 * Dm.pow(0.94f, properties["resistance"]), 3.5f, 12); // Capped at between -3 and 17 resistance
            stage_prob = max(0.2f * Dm.num(properties["stage_rate"]), 0) + 1.5f;
            set_severity(round(Dm.num(properties["severity"])));
            cure_text = "If you can see this, something has gone wrong.";
        }
        else
            throw Dm.CRASH("Our properties were empty or null!", "code/datums/diseases/advance/advance.dm", 240);
    }


    // Assign the spread type and give it the correct description.
    // tg: code/datums/diseases/advance/advance.dm:244
    public virtual void set_spread(object spread_id = null)
    {
        if (Dm.eq(spread_id, DISEASE_SPREAD_NON_CONTAGIOUS))
        {
            update_spread_flags(DISEASE_SPREAD_NON_CONTAGIOUS);
            spread_text = "None";
        }
        else if (Dm.eq(spread_id, DISEASE_SPREAD_SPECIAL))
        {
            update_spread_flags(DISEASE_SPREAD_SPECIAL);
            spread_text = "None";
        }
        else if (Dm.eq(spread_id, DISEASE_SPREAD_BLOOD))
        {
            update_spread_flags(DISEASE_SPREAD_BLOOD);
            spread_text = "Blood";
        }
        else if (Dm.eq(spread_id, DISEASE_SPREAD_CONTACT_FLUIDS))
        {
            update_spread_flags(DISEASE_SPREAD_BLOOD | DISEASE_SPREAD_CONTACT_FLUIDS);
            spread_text = "Fluids";
        }
        else if (Dm.eq(spread_id, DISEASE_SPREAD_CONTACT_SKIN))
        {
            update_spread_flags(DISEASE_SPREAD_BLOOD | DISEASE_SPREAD_CONTACT_FLUIDS | DISEASE_SPREAD_CONTACT_SKIN);
            spread_text = "Skin contact";
        }
        else if (Dm.eq(spread_id, DISEASE_SPREAD_AIRBORNE))
        {
            update_spread_flags(DISEASE_SPREAD_BLOOD | DISEASE_SPREAD_CONTACT_FLUIDS | DISEASE_SPREAD_CONTACT_SKIN | DISEASE_SPREAD_AIRBORNE);
            spread_text = "Respiration";
        }
    }

    // tg: code/datums/diseases/advance/advance.dm:265
    public virtual void set_severity(object level_sev = null)
    {

        float sw__1 = Dm.num(level_sev);
        if (level_sev != null && Dm.in_to(sw__1, -INFINITY, 0))
            severity = DISEASE_SEVERITY_POSITIVE;
        else if (Dm.eq(level_sev, 1))
            severity = DISEASE_SEVERITY_NONTHREAT;
        else if (Dm.eq(level_sev, 2))
            severity = DISEASE_SEVERITY_MINOR;
        else if (Dm.eq(level_sev, 3))
            severity = DISEASE_SEVERITY_MEDIUM;
        else if (Dm.eq(level_sev, 4))
            severity = DISEASE_SEVERITY_HARMFUL;
        else if (Dm.eq(level_sev, 5))
            severity = DISEASE_SEVERITY_DANGEROUS;
        else if (level_sev != null && Dm.in_to(sw__1, 6, INFINITY))
            severity = DISEASE_SEVERITY_BIOHAZARD;
        else
            severity = "Unknown";
    }

    // Randomly generate a symptom, has a chance to lose or gain a symptom.
    // tg: code/datums/diseases/advance/advance.dm:287
    public virtual async DmTask Evolve(object min_level = null, object max_level = null, float? ignore_mutable = null)
    {
        ignore_mutable ??= FALSE;
        if (!Dm.truthy(mutable) && !Dm.truthy(ignore_mutable))
            return;
        DmList generated_symptoms = GenerateSymptoms(min_level, max_level, 1) as DmList;
        if (Dm.truthy(length(generated_symptoms)))
        {
            datum_symptom S = (datum_symptom)pick(generated_symptoms);
            AddSymptom(S);
            await Refresh(TRUE);
        }
    }

    // Randomly remove a symptom.
    // tg: code/datums/diseases/advance/advance.dm:297
    public virtual async DmTask Devolve(float? ignore_mutable = null)
    {
        ignore_mutable ??= FALSE;
        if (!Dm.truthy(mutable) && !Dm.truthy(ignore_mutable))
            return;
        if (Dm.length(symptoms) > 1)
        {
            datum_symptom S = (datum_symptom)pick(symptoms);
            if (S != null)
            {
                RemoveSymptom(S);
                await Refresh(TRUE);
            }
        }
    }

    // Randomly neuter a symptom.
    // tg: code/datums/diseases/advance/advance.dm:307
    public virtual async DmTask Neuter(float? ignore_mutable = null)
    {
        ignore_mutable ??= FALSE;
        if (!Dm.truthy(mutable) && !Dm.truthy(ignore_mutable))
            return;
        if (Dm.truthy(length(symptoms)))
        {
            datum_symptom S = (datum_symptom)pick(symptoms);
            if (S != null)
            {
                NeuterSymptom(S);
                await Refresh(TRUE);
            }
        }
    }

    // Name the disease.
    // tg: code/datums/diseases/advance/advance.dm:317
    public virtual void AssignName(string name = null)
    {
        name ??= "Unknown";
        datum_disease_advance A = SSdisease.archive_diseases[GetDiseaseID()] as datum_disease_advance;
        A.name = name;
    }

    // Return a unique ID of the disease.
    // tg: code/datums/diseases/advance/advance.dm:322
    public override object GetDiseaseID()
    {
        if (!Dm.truthy(id))
        {
            DmList L = Dm.list();
            foreach (datum_symptom S in Dm.each<datum_symptom>(symptoms))
            {
                if (Dm.truthy(S.neutered))
                    Dm.append(L, Dm.text("[]N", S.id));
                else
                    Dm.append(L, S.id);
            }
            L = sort_list(L) as DmList; // Sort the list so it doesn't matter which order the symptoms are in.
            string result = jointext(L, ":");
            id = result;
        }
        return id;
    }


    // Add a symptom, if it is over the limit we take a random symptom away and add the new one.
    // tg: code/datums/diseases/advance/advance.dm:337
    public virtual void AddSymptom(datum_symptom S = null)
    {
        if (Dm.truthy(HasSymptom(S)))
            return;
        if (symptoms.len >= VIRUS_SYMPTOM_LIMIT)
            RemoveSymptom((datum_symptom)pick(symptoms));
        Dm.append(symptoms, S);
        S.OnAdd(this);
    }

    // Simply removes the symptom.
    // tg: code/datums/diseases/advance/advance.dm:346
    public virtual void RemoveSymptom(datum_symptom S = null)
    {
        Dm.remove(symptoms, S);
        S.OnRemove(this);
    }

    // Neuter a symptom, so it will only affect stats
    // tg: code/datums/diseases/advance/advance.dm:351
    public virtual void NeuterSymptom(datum_symptom S = null)
    {
        if (!Dm.truthy(S.neutered))
        {
            S.neutered = TRUE;
            S.name += " (neutered)";
            S.OnRemove(this);
        }
    }

    // tg: code/datums/diseases/advance/advance.dm:467
    public virtual object totalStageSpeed()
    {
        return properties["stage_rate"];
    }

    // tg: code/datums/diseases/advance/advance.dm:470
    public virtual object totalStealth()
    {
        return properties["stealth"];
    }

    // tg: code/datums/diseases/advance/advance.dm:473
    public virtual object totalResistance()
    {
        return properties["resistance"];
    }

    // tg: code/datums/diseases/advance/advance.dm:476
    public virtual object totalTransmittable()
    {
        return properties["transmittable"];
    }

    // tg: code/datums/diseases/advance/advance.dm:479
    public virtual object totalSeverity()
    {
        return properties["severity"];
    }

    /**
     *  If the disease has an incubation time (such as event diseases) start the timer, let properties determine if there's no timer set.
     */
    // tg: code/datums/diseases/advance/advance.dm:485
    public override async DmTask after_add()
    {
        await base.after_add();

        if (incubation_time == null)
            return;

        if (Dm.num(incubation_time) < world.time)
        {
            make_visible();
            return;
        }

        _addtimer(Dm.@new<datum_callback>(this, nameof(make_visible)), Dm.num(incubation_time) - world.time, file: "code/datums/diseases/advance/advance.dm", line: 495);
    }


    /**
     *  Make virus visible to heath scanners
     */
    // tg: code/datums/diseases/advance/advance.dm:501
    public virtual void make_visible()
    {
        Dm.mask(ref visibility_flags, Dm.bitnot(HIDDEN_SCANNER));
        affected_mob.med_hud_set_status();
    }

    // tg: code/datums/diseases/advance/advance.dm:505
    public override async DmTask cure(float? add_resistance = null)
    {
        add_resistance ??= TRUE;
        if (severity == DISEASE_SEVERITY_UNCURABLE)
            return;
        if (add_resistance == TRUE)
        {
            foreach (datum_symptom each_symptom in Dm.each_cast<datum_symptom>(symptoms))
            {
                if (!Dm.truthy(each_symptom.neutered) && !Dm.truthy(each_symptom.immunity_proof))
                    LAZYOR(ref affected_mob.symptom_resistances, each_symptom.name);
            }
        }
        await base.cure(add_resistance);
    }

    // tg: code/datums/diseases/advance/advance.dm:514
    public override object get_immunity_recovery()
    {
        float recovery_bonus = 0;
        foreach (datum_symptom each_symptom in Dm.each_cast<datum_symptom>(symptoms))
        {
            if (Dm.@in(each_symptom.name, affected_mob.symptom_resistances) && !Dm.truthy(each_symptom.neutered))
                recovery_bonus += DISEASE_SYMPTOM_IMMUNITY_RECOVERY_BONUS;
        }
        return recovery_bonus;
    }

    // tg: code/datums/diseases/advance/advance.dm:521
    public virtual object generate_cure_text(object cure_count = null)
    {
        DmList remedies = Dm.list();
        foreach (datum_symptom each_symptom in Dm.each_cast<datum_symptom>(symptoms))
        {
            if (Dm.length(remedies) >= Dm.num(cure_count))
                break;
            if (each_symptom.symptom_cure == null)
                continue;
            Type each_cure = each_symptom.symptom_cure;
            object cure_name = Dm.initial(each_cure, (datum_reagent r) => r.name);
            if (!Dm.truthy(each_symptom.neutered) && !Dm.@in(cure_name, remedies))
                Dm.append(ref remedies, cure_name);
        }
        return english_list(remedies, nothing_text: "Nothing");
    }
}

/*

	Static Procs

*/

public static partial class GlobalProcs
{
    // Mix a list of advance diseases and return the mixed result.
    // tg: code/datums/diseases/advance/advance.dm:364
    public static async DmTask<object> Advance_Mix(DmList D_list = null)
    {
        DmList diseases = Dm.list();

        foreach (datum_disease_advance A in Dm.each<datum_disease_advance>(D_list))
            Dm.append(diseases, await A.Copy());

        if (!Dm.truthy(diseases.len))
            return null;
        if (diseases.len <= 1)
            return pick(diseases); // Just return the only entry.

        float i = 0;
        // Mix our diseases until we are left with only one result.
        while (i < 20 && diseases.len > 1)
        {

            i++;

            datum_disease_advance D1 = (datum_disease_advance)pick(diseases);
            Dm.remove(diseases, D1);

            datum_disease_advance D2 = (datum_disease_advance)pick(diseases);
            D2.Mix(D1);
        }

        // Should be only 1 entry left, but if not let's only return a single entry
        datum_disease_advance to_return = (datum_disease_advance)pick(diseases);
        await to_return.Refresh(TRUE);
        return to_return;
    }

    // tg: code/datums/diseases/advance/advance.dm:392
    public static async DmTask SetViruses(datum_reagent R = null, DmList data = null)
    {
        if (data != null)
        {
            DmList preserve = Dm.list();
            if (data is DmList && Dm.truthy(data["viruses"]))
            {
                foreach (datum_disease A in Dm.each<datum_disease>(data["viruses"]))
                    Dm.append(preserve, await A.Copy());
                R.data = data.Copy();
            }
            if (Dm.truthy(preserve.len))
                ((DmList)R.data)["viruses"] = preserve;
        }
    }

    // tg: code/datums/diseases/advance/advance.dm:402
    public static async DmTask AdminCreateVirus(client user = null)
    {

        if (user == null)
            return;

        float i = VIRUS_SYMPTOM_LIMIT;

        datum_disease_advance D = await Dm.new_async<datum_disease_advance>();
        D.symptoms = Dm.list();

        DmList symptoms = Dm.list();
        Dm.append(symptoms, "Done");
        Dm.append(symptoms, datum_controller_subsystem_disease.list_symptoms.Copy());
        do
        {
            if (user != null)
            {
                object symptom = await tgui_input_list(user, Dm.text("Choose a symptom to add ([] remaining)", i), "Choose a Symptom", sort_list(symptoms, "/proc/" + nameof(GlobalProcs.cmp_typepaths_asc)) as DmList);
                if (symptom == null)
                    return;
                else if (istext(symptom))
                    i = 0;
                else if (ispath(symptom))
                {
                    datum_symptom S = (datum_symptom)Dm.@new((Type)symptom);
                    if (!Dm.truthy(D.HasSymptom(S)))
                    {
                        D.AddSymptom(S);
                        i -= 1;
                    }
                }
            }
        }
        while (i > 0);

        if (D.symptoms.len > 0)
        {

            object new_name = await tgui_input_text(user, "Name your new disease", "New Name", max_length: MAX_NAME_LEN);
            if (!Dm.truthy(new_name))
                return;
            await D.Refresh();
            D.AssignName(Dm.str(new_name)); //Updates the master copy
            D.name = Dm.str(new_name); //Updates our copy

            DmList targets = Dm.list("Random");
            Dm.append(targets, sort_names(GLOB.human_list));
            object target = await tgui_input_list(user, "Viable human target", "Disease Target", targets);
            if (target == null)
                return;
            mob_living_carbon_human H = null;
            if (Dm.eq(target, "Random"))
            {
                foreach (object human in Dm.each(shuffle(GLOB.human_list)))
                {
                    H = (mob_living_carbon_human)human;
                    object found = FALSE;
                    if (!Dm.truthy(is_station_level(H.z)))
                        continue;
                    if (!Dm.truthy(H.HasDisease(D)))
                    {
                        found = await H.ForceContractDisease(D);
                        break;
                    }
                    if (!Dm.truthy(found))
                        to_chat(user, I18n.T("Could not find a valid target for the disease."));
                }
            }
            else
            {
                H = target as mob_living_carbon_human;
                if (H is mob_living_carbon_human && Dm.truthy(Dm.bitand(D.infectable_biotypes, H.mob_biotypes)))
                    await H.ForceContractDisease(D);
                else
                {
                    to_chat(user, I18n.T("Target could not be infected. Check mob biotype compatibility or resistances."));
                    return;
                }
            }

            message_admins(Dm.text("[] has triggered a custom virus outbreak of [] in []", key_name_admin(user), D.admin_details(), ADMIN_LOOKUPFLW(H)));
            log_virus(Dm.text("[] has triggered a custom virus outbreak of [] in []!", key_name(user), D.admin_details(), H));
        }
    }
}
