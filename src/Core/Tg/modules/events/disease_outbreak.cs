// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/disease_outbreak.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341) but the admins' setups (/datum/event_admin_setup, as the other events'): the classic outbreak
// gives one crewman a classic disease as a carrier, the advanced outbreak a random advanced disease of the asked severity and spread,
// and both are announced as a level 7 biohazard with the status displays' biohazard alert. The var blocks are generated (D111).

// tg: code/modules/events/disease_outbreak.dm:26
public partial class datum_round_event_control_disease_outbreak : datum_round_event_control
{
    // tg: code/modules/events/disease_outbreak.dm:40
    public override object can_spawn_event(object players_amt = null, float? allow_magic = null)
    {
        object dot = base.can_spawn_event(players_amt, allow_magic);
        if (!Dm.truthy(dot))
            return dot;
        generate_candidates();
        if (Dm.length(disease_candidates) > 0)
            return TRUE;
        return dot;
    }

    /**
     * Creates a list of people who are elligible to become disease carriers for the event
     *
     * Searches through the player list, adding anyone who is elligible to be a disease carrier for the event. This checks for
     * whether or not the candidate is alive, a crewmember, is able to receive a disease, and whether or not a disease is already present in them.
     * This proc needs to be run at some point to ensure the event has candidates to infect.
     */
    // tg: code/modules/events/disease_outbreak.dm:55
    public virtual object generate_candidates()
    {
        disease_candidates.Cut(); //We clear the list and rebuild it again.
        foreach (mob_living_carbon_human candidate in Dm.each<mob_living_carbon_human>(shuffle(GLOB.player_list))) //Player list is much more up to date and requires less checks(?)
        {
            if (!Dm.truthy(Dm.bitand(candidate.mind?.assigned_role?.job_flags, JOB_CREW_MEMBER)) || candidate.stat == DEAD)
                continue;
            if (HAS_TRAIT(candidate, TRAIT_VIRUSIMMUNE)) //Don't pick someone who's virus immune, only for it to not do anything.
                continue;
            if (Dm.length(candidate.diseases) > 0) //Is our candidate already sick?
                continue;
            if (!Dm.truthy(is_station_level(candidate.z)) && !Dm.truthy(is_mining_level(candidate.z))) //Diseases can't really spread if the vector is in deep space.
                continue;
            Dm.append(disease_candidates, candidate);
        }
        return null;
    }
}

// tg: code/modules/events/disease_outbreak.dm:95
public partial class datum_round_event_disease_outbreak : datum_round_event
{
    /// How long the virus stays hidden before announcement
    private const float ADV_ANNOUNCE_DELAY = 75;

    // tg: code/modules/events/disease_outbreak.dm:104
    public override async DmTask announce__proc(object fake = null)
    {
        if (!Dm.truthy(illness_type))
        {
            DmList virus_candidates = Dm.list(
                typeof(datum_disease_anxiety),
                typeof(datum_disease_beesease),
                typeof(datum_disease_brainrot),
                typeof(datum_disease_cold9),
                typeof(datum_disease_flu),
                typeof(datum_disease_fluspanish),
                typeof(datum_disease_magnitis),
                typeof(datum_disease_weightlessness),
                /// And here are some that will never roll for real, just to mess around.
                typeof(datum_disease_death_sandwich_poisoning),
                typeof(datum_disease_dna_retrovirus),
                typeof(datum_disease_gbs),
                typeof(datum_disease_rhumba_beat)
            );
            Type fake_virus = pick(virus_candidates) as Type;
            illness_type = Dm.str(Dm.initial(fake_virus, (datum_disease p) => p.name));
        }
        priority_announce(Dm.text("Confirmed outbreak of level 7 viral biohazard aboard []. All personnel must contain the outbreak.", station_name()), Dm.text("[] Alert", illness_type), ANNOUNCER_OUTBREAK7);

        // Set status displays to biohazard alert
        send_status_display_biohazard_alert();
    }

    // tg: code/modules/events/disease_outbreak.dm:128
    public override async DmTask<object> setup()
    {
        await DmTask.CompletedTask;
        announce_when = ADV_ANNOUNCE_DELAY;
        return null;
    }

    // tg: code/modules/events/disease_outbreak.dm:131
    public override async DmTask<object> start()
    {
        datum_round_event_control_disease_outbreak disease_event = (datum_round_event_control_disease_outbreak)control;
        Dm.append(afflicted, disease_event.disease_candidates);
        disease_event.disease_candidates.Cut(); //Clean the list after use
        if (virus_type == null)
        {
            DmList virus_candidates = Dm.list();

            //Practically harmless diseases. Mostly just gives medical something to do.
            Dm.append(virus_candidates, Dm.list(typeof(datum_disease_flu), typeof(datum_disease_cold9)));

            //The more dangerous ones
            Dm.append(virus_candidates, Dm.list(typeof(datum_disease_beesease), typeof(datum_disease_brainrot), typeof(datum_disease_fluspanish)));

            //The wacky ones
            Dm.append(virus_candidates, Dm.list(typeof(datum_disease_magnitis), typeof(datum_disease_anxiety), typeof(datum_disease_weightlessness)));

            //The rest of the diseases either aren't conventional "diseases" or are too unique/extreme to be considered for a normal event
            virus_type = pick(virus_candidates) as Type;
        }

        datum_disease new_disease;
        new_disease = (datum_disease)await Dm.new_async(virus_type);
        new_disease.carrier = TRUE;
        illness_type = Dm.str(new_disease.name);

        mob_living_carbon_human victim = null;
        while (Dm.length(afflicted) > 0)
        {
            victim = pick_n_take(afflicted) as mob_living_carbon_human;
            if (Dm.truthy(await victim.ForceContractDisease(new_disease, FALSE)))
            {
                message_admins(Dm.text("Event triggered: Disease Outbreak - [] starting with patient zero []!", new_disease.name, ADMIN_LOOKUPFLW(victim)));
                log_game(Dm.text("Event triggered: Disease Outbreak - [] starting with patient zero [].", new_disease.name, key_name(victim)));
                announce_to_ghosts(victim);
                return null;
            }
            CHECK_TICK(); //don't lag the server to death
        }
        if (victim == null)
        {
            message_admins("Event Disease Outbreak: Classic attempted to start, but failed to find a candidate target.");
            log_game("Event Disease Outbreak: Classic attempted to start, but failed to find a candidate target");
        }
        return null;
    }
}

// tg: code/modules/events/disease_outbreak.dm:250
public partial class datum_round_event_disease_outbreak_advanced : datum_round_event_disease_outbreak
{
    /// Advanced virus lower limit for symptoms
    private const float ADV_MIN_SYMPTOMS = 3;
    /// Advanced virus upper limit for symptoms
    private const float ADV_MAX_SYMPTOMS = 4;
    /// Numerical define for medium severity advanced virus
    private const float ADV_DISEASE_MEDIUM = 1;
    /// Numerical define for harmful severity advanced virus
    private const float ADV_DISEASE_HARMFUL = 3;
    /// Numerical define for dangerous severity advanced virus
    private const float ADV_DISEASE_DANGEROUS = 5;
    /// Percentile for low severity advanced virus
    private const float ADV_RNG_LOW = 40;
    /// Percentile for mid severity advanced virus
    private const float ADV_RNG_MID = 85;

    /**
     * Generate virus base values
     *
     * Generates a virus with either the admin selected parameters for severity and symptoms
     * or if it was not selected, randomly pick between the MIX and MAX configured in the defines.
     */
    // tg: code/modules/events/disease_outbreak.dm:264
    public override async DmTask<object> start()
    {
        datum_round_event_control_disease_outbreak_advanced disease_event = (datum_round_event_control_disease_outbreak_advanced)control;
        Dm.append(afflicted, disease_event.disease_candidates);
        disease_event.disease_candidates.Cut();

        if (max_symptoms == null)
            max_symptoms = rand(ADV_MIN_SYMPTOMS, ADV_MAX_SYMPTOMS);

        if (requested_severity == null)
        {
            float rng_severity = rand(1, 100);
            if (rng_severity < ADV_RNG_LOW)
                requested_severity = ADV_DISEASE_MEDIUM;

            else if (rng_severity < ADV_RNG_MID)
                requested_severity = ADV_DISEASE_HARMFUL;

            else
                requested_severity = ADV_DISEASE_DANGEROUS;
        }

        datum_disease_advance advanced_disease = await Dm.new_async<datum_disease_advance_random_event>(max_symptoms, requested_severity, requested_transmissibility);

        DmList name_symptoms = Dm.list();
        foreach (datum_symptom new_symptom in Dm.each_cast<datum_symptom>(advanced_disease.symptoms))
            Dm.append(name_symptoms, new_symptom.name);

        illness_type = Dm.str(advanced_disease.name);

        mob_living_carbon_human victim = null;
        while (Dm.length(afflicted) > 0)
        {
            victim = pick_n_take(afflicted) as mob_living_carbon_human;
            if (Dm.truthy(await victim.ForceContractDisease(advanced_disease, FALSE)))
            {
                message_admins(Dm.text("Event triggered: Disease Outbreak: Advanced - starting with patient zero []! Details: [] sp:[] ([])", ADMIN_LOOKUPFLW(victim), advanced_disease.admin_details(), advanced_disease.spread_flags, advanced_disease.spread_text));
                log_game(Dm.text("Event triggered: Disease Outbreak: Advanced - starting with patient zero []. Details: [] sp:[] ([])", key_name(victim), advanced_disease.admin_details(), advanced_disease.spread_flags, advanced_disease.spread_text));
                log_virus(Dm.text("Disease Outbreak: Advanced has triggered a custom virus outbreak of [] in []!", advanced_disease.admin_details(), victim));
                announce_to_ghosts(victim);
                return null;
            }
            CHECK_TICK(); //don't lag the server to death
        }
        if (victim == null)
        {
            message_admins("Event Disease Outbreak: Advanced attempted to start, but failed to find a candidate target.");
            log_game("Event Disease Outbreak: Advanced attempted to start, but failed to find a candidate target.");
        }
        return null;
    }
}

// tg: code/modules/events/disease_outbreak.dm:309  (tg's typo: /advance, not /advanced; no event runs this type)
public partial class datum_round_event_disease_outbreak_advance : datum_round_event_disease_outbreak
{
    // tg: code/modules/events/disease_outbreak.dm:309
    public override async DmTask<object> setup()
    {
        await DmTask.CompletedTask;
        announce_when = 75; // ADV_ANNOUNCE_DELAY
        return null;
    }
}

// tg: code/modules/events/disease_outbreak.dm:305
public partial class datum_disease_advance_random_event : datum_disease_advance_random
{
    /// How long the virus stays hidden before announcement
    private const float ADV_ANNOUNCE_DELAY = 75;
    /// Numerical define for harmful severity advanced virus
    private const float ADV_DISEASE_HARMFUL = 3;
    /// Numerical define for dangerous severity advanced virus
    private const float ADV_DISEASE_DANGEROUS = 5;
    /// Percentile for high vs. low transmissibility
    private const float ADV_SPREAD_THRESHOLD = 85;
    /// Admin custom low spread
    private const float ADV_SPREAD_FORCED_LOW = 0;
    /// Admin custom high spread
    private const float ADV_SPREAD_FORCED_HIGH = 90;

    /**
     * Generate advanced virus
     *
     * Uses the parameters to create a list of symptoms, picking from various severities
     * Viral Evolution and Eternal Youth are special modifiers, so we roll separately.
     */
    // tg: code/modules/events/disease_outbreak.dm:318
    [DmParams("max_symptoms", "requested_severity", "requested_transmissibility")]
    public override async DmTask New(params object[] __extra)
    {
        object max_symptoms = Dm.arg(__extra, 0);
        object requested_severity = Dm.arg(__extra, 1);
        object requested_transmissibility = Dm.arg(__extra, 2);
        DmList possible_symptoms = Dm.list(
            typeof(datum_symptom_beard),
            typeof(datum_symptom_chills),
            typeof(datum_symptom_confusion),
            typeof(datum_symptom_cough),
            typeof(datum_symptom_disfiguration),
            typeof(datum_symptom_dizzy),
            typeof(datum_symptom_fever),
            typeof(datum_symptom_hallucigen),
            typeof(datum_symptom_headache),
            typeof(datum_symptom_itching),
            typeof(datum_symptom_polyvitiligo),
            typeof(datum_symptom_shedding),
            typeof(datum_symptom_sneeze),
            typeof(datum_symptom_voice_change)
        );

        if (Dm.eq(requested_severity, ADV_DISEASE_HARMFUL))
        {
            Dm.append(possible_symptoms, Dm.list(
                typeof(datum_symptom_choking),
                typeof(datum_symptom_deafness),
                typeof(datum_symptom_genetic_mutation),
                typeof(datum_symptom_narcolepsy),
                typeof(datum_symptom_vomit),
                typeof(datum_symptom_weight_loss)
            ));
        }
        else if (Dm.eq(requested_severity, ADV_DISEASE_DANGEROUS))
        {
            Dm.append(possible_symptoms, Dm.list(
                typeof(datum_symptom_alkali),
                typeof(datum_symptom_asphyxiation),
                typeof(datum_symptom_fire),
                typeof(datum_symptom_flesh_death),
                typeof(datum_symptom_flesh_eating),
                typeof(datum_symptom_visionloss)
            ));
        }

        float current_severity = 0;

        while (Dm.lt(symptoms.len, max_symptoms))
        {
            Type chosen_symptom = pick_n_take(possible_symptoms) as Type;

            if (chosen_symptom == null)
            {
                stack_trace("Advanced disease could not pick a symptom!", "code/modules/events/disease_outbreak.dm", 363);
                return;
            }

            //Checks if the chosen symptom is severe enough to meet requested severity. If not, pick a new symptom.
            //If we've met requested severity already, we don't care and will keep the chosen symptom.
            datum_symptom new_symptom = (datum_symptom)await Dm.new_async(chosen_symptom);

            if (Dm.lt(current_severity, requested_severity) && Dm.lt(new_symptom.severity, requested_severity))
                continue;

            Dm.append(symptoms, new_symptom);

            if (Dm.num(new_symptom.severity) > current_severity)
                current_severity = Dm.num(new_symptom.severity);
        }

        Dm.combine(ref visibility_flags, HIDDEN_SCANNER);
        object transmissibility = requested_transmissibility;

        if (transmissibility == null)
            transmissibility = rand(1, 100);

        if (Dm.eq(requested_transmissibility, ADV_SPREAD_FORCED_LOW)) // Admin forced
            set_spread(DISEASE_SPREAD_CONTACT_FLUIDS);

        else if (Dm.eq(requested_transmissibility, ADV_SPREAD_FORCED_HIGH)) // Admin forced
            set_spread(DISEASE_SPREAD_AIRBORNE);

        //If severe enough, alert immediately on scanners, limit transmissibility
        else if (current_severity >= ADV_DISEASE_DANGEROUS)
        {
            Dm.mask(ref visibility_flags, Dm.bitnot(HIDDEN_SCANNER));
            set_spread(DISEASE_SPREAD_CONTACT_SKIN);
        }

        else if (Dm.lt(transmissibility, ADV_SPREAD_THRESHOLD))
            set_spread(DISEASE_SPREAD_CONTACT_SKIN);

        else
        {
            Dm.mask(ref visibility_flags, Dm.bitnot(HIDDEN_SCANNER));
            set_spread(DISEASE_SPREAD_AIRBORNE);
        }


        //Illness name from one of the symptoms
        datum_symptom picked_name = (datum_symptom)pick(symptoms);
        name = picked_name.illness;

        //Modifiers to keep the disease base stats above 0 (unless RNG gets a really bad roll.)
        //Eternal Youth for +4 to resistance and stage speed.
        //Viral modifiers to slow down/resist or go fast and loud.
        if (prob(66))
        {
            DmList possible_modifiers = Dm.list(
                typeof(datum_symptom_viraladaptation),
                typeof(datum_symptom_viralevolution)
            );
            Type chosen_modifier = pick(possible_modifiers) as Type;
            Dm.append(symptoms, await Dm.new_async(chosen_modifier));
            Dm.append(symptoms, await Dm.new_async<datum_symptom_youth>());
        }

        await Refresh();
    }

    /**
     * Assign virus properties
     *
     * Now that we've picked our symptoms and severity, we determine the other stats
     * (Stage Speed, Resistance, Transmissibility)
     * The LOW/MID percentiles can be adjusted in the defines.
     * If the virus is severity DANGEROUS we do not hide it from health scanners at event start.
     * If the virus is airborne, also don't hide it.
     */
    // tg: code/modules/events/disease_outbreak.dm:430
    public override void assign_properties()
    {

        if (Dm.length(properties) == 0)
        {
            stack_trace("Advanced virus properties were empty or null!", "code/modules/events/disease_outbreak.dm", 433);
            return;
        }

        incubation_time = round(world.time + (((ADV_ANNOUNCE_DELAY * 2) - 10) * SECONDS));
        properties["transmittable"] = rand(4, 7);
        spreading_modifier = max(CEILING(0.4f * Dm.num(properties["transmittable"]), 1), 1);
        cure_chance = clamp(10 * Dm.pow(0.94f, properties["resistance"]), 3.5f, 12);
        stage_prob = max(0.4f * Dm.num(properties["stage_rate"]), 1); // Faster than regular advanced diseases
        set_severity(properties["severity"]);

        //If we have an advanced (high stage) disease, add it to the name.
        if (Dm.num(properties["stage_rate"]) >= 7)
            name = Dm.text("Advanced []", name);

        cure_text = "If you can see this, something has gone wrong.";
    }

    /**
     * Set the transmission methods on the generated virus
     *
     * Apply the transmission methods we rolled in the assign_properties proc
     */
    // tg: code/modules/events/disease_outbreak.dm:454
    public override void set_spread(object spread_id = null)
    {
        if (Dm.eq(spread_id, DISEASE_SPREAD_CONTACT_FLUIDS))
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
}
