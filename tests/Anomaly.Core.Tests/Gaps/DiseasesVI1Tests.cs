using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package VI1 (D341): the diseases and virology in a booted toy room of iron floor on the station's level (the outbreak's crew
/// test reads the level), with SSdisease made and initialized as tg's MC does (the archive of the common diseases). The cold spreads
/// by contact and by air, shows its stage messages and is cured by spaceacillin or by lying down, and leaves its host immune; brain
/// rot is cured by mannitol; two advanced diseases mix into one with both symptoms and the summed stats; the cough, sneeze, fever,
/// itching, vomit and toxolysis symptoms act on their host; a carrier shows no symptom; the PanD.E.M.I.C. prints a culture tube and
/// a vaccine tube from a blood sample; the outbreak events infect a crewman; the med HUD shows the virus and the health analyzer its
/// lines; and every live disease and symptom type is made, acts at its last stage and is cured without a runtime.
/// </summary>
public class DiseasesVI1Tests
{
    private const int W = 13, H = 11;

    private static (AudioRoom R, Feed Feed) Room(ulong seed)
    {
        var r = new AudioRoom(W, H, seed: seed, mapExtras: _ =>
        {
            for (int y = 2; y < H; y++)
                for (int x = 2; x < W; x++)
                    Dm.@new<turf_open_floor_iron>(locate(x, y, 1));
        });
        Feed feed = Listen(r);
        r.In(() =>
        {
            GLOB.station_levels_cache.len = 1;
            GLOB.station_levels_cache[1] = TRUE;
            SSmapping.z_list = Dm.list(Dm.@new<datum_space_level>(1, "station", Dm.list(Dm.kv(ZTRAIT_STATION, TRUE))));
            var station = (area)Dm.@new(typeof(area_station_medical_virology));
            foreach (turf t in Dm.each<turf>(block(2, 2, 1, W - 1, H - 1, 1)))
                t.change_area((area)t.loc, station);
            station.requires_power = FALSE;
            station.reg_in_areas_in_z();
            GLOB.the_station_areas = Dm.list(station.type);
            if (SSdisease == null)
                Waiting.NoSleep(Dm.@new<datum_controller_subsystem_disease>().Initialize());
        });
        r.Step(Second);
        return (r, feed);
    }

    private static T Infect<T>(AudioRoom r, mob_living who, Action<T> setup = null) where T : datum_disease => r.In(() =>
    {
        T disease = Dm.@new<T>();
        setup?.Invoke(disease);
        Assert.True(Dm.truthy(Waiting.NoSleep(who.ForceContractDisease(disease, FALSE))), typeof(T).Name + " did not take");
        return disease;
    });

    private static bool Has<T>(mob_living who) where T : datum_disease => Dm.locate<T>(who.diseases) != null;

    private static datum_reagents Blood(mob_living who) => (datum_reagents)who.reagents;

    private static string Said(Feed feed) => string.Join("\n", feed.Chat.Select(c => c.Html));

    /// <summary>An advanced disease of the given symptoms (tg's admin panel and the presets: symptoms, then Refresh).</summary>
    private static datum_disease_advance Advanced(params Type[] symptoms)
    {
        var disease = Dm.@new<datum_disease_advance>();
        foreach (Type s in symptoms)
            Dm.append(disease.symptoms, Dm.@new(s));
        Waiting.NoSleep(disease.Refresh());
        return disease;
    }

    /// <summary>Activates every symptom at the disease's stage, ignoring the symptoms' own delay, n times.</summary>
    private static void Activate(datum_disease_advance disease, int times)
    {
        for (int i = 0; i < times; i++)
        {
            foreach (datum_symptom s in Dm.each<datum_symptom>(disease.symptoms))
            {
                s.next_activation = 0;
                Waiting.NoSleep(s.Activate(disease));
            }
        }
    }

    [Fact]
    public void TheColdIsCaughtByContactAndByAir()
    {
        var (r, _) = Room(10301);
        mob_living_carbon_human sick = r.Dummy(4, 5), toucher = r.Dummy(5, 5), neighbour = r.Dummy(6, 7);
        var cold = Infect<datum_disease_cold>(r, sick);
        r.In(() =>
        {
            Assert.Same(cold.affected_mob, sick);
            Assert.True(Dm.@in(cold, SSdisease.active_diseases));
            // contact: the toucher's zone, its clothes' bio armour and the cold's spreading modifier (0.5: prob(15 / 0.5) to shrug it off)
            for (int i = 0; i < 100 && !Has<datum_disease_cold>(toucher); i++)
                Waiting.NoSleep(toucher.ContactContractDisease(cold));
            Assert.True(Has<datum_disease_cold>(toucher), "the toucher caught the cold");
            Assert.NotSame(cold, Dm.locate<datum_disease_cold>(toucher.diseases)); // a copy
            // air: two tiles away, nothing covering the mouths; the walk between the turfs is open (DV558: a host who can spread does)
            for (int i = 0; i < 200 && !Has<datum_disease_cold>(neighbour); i++)
                Waiting.NoSleep(cold.airborne_spread());
            Assert.True(Has<datum_disease_cold>(neighbour), "the neighbour caught the cold through the air");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheColdShowsItsStageAndIsCuredBySpaceacillinOrRestAndLeavesImmunity()
    {
        var (r, feed) = Room(10302);
        mob_living_carbon_human patient = r.Join(4, 5, "patient");
        mob_living_carbon_human sleeper = r.Join(8, 5, "sleeper");
        var cold = Infect<datum_disease_cold>(r, patient);
        r.In(() =>
        {
            cold.stage = 2;
            // the cold can step down and recover on its own before it says anything (_disease.dm:189-262): held at stage 2 below, and
            // the rolls pinned so the test does not lean on the stream the room's boot left (gaps train 13: CI1's and BR1's subsystems)
            r.World.Rng.Reseed(10311, 1);
            for (int i = 0; i < 400 && !feed.Any("Your throat feels sore.") && !feed.Any("Mucous runs down the back of your throat."); i++)
            {
                cold.carrier = FALSE;
                cold.stage = 2; // held at stage 2: the natural recovery steps it down and cures it from stage 1 (_disease.dm:248-262)
                Waiting.NoSleep(cold.stage_act(SSMOBS_DT()));
                if (QDELETED(cold))
                    break;
            }
            Assert.True(feed.Any("Your throat feels sore.") || feed.Any("Mucous runs down the back of your throat.") || feed.Any("sneezes") || feed.Any("coughs"), Said(feed));

            // spaceacillin in the blood: has_cure, then SPT_PROB(cure_chance) cures it (_disease.dm:144), with the cold's resistance
            object id = cold.GetDiseaseID();
            Waiting.NoSleep(Blood(patient).add_reagent(typeof(datum_reagent_medicine_spaceacillin), 30));
            for (int i = 0; i < 500 && Has<datum_disease_cold>(patient); i++)
                Waiting.NoSleep(Dm.locate<datum_disease_cold>(patient.diseases).stage_act(SSMOBS_DT()));
            Assert.False(Has<datum_disease_cold>(patient), "spaceacillin cured the cold");
            Assert.True(Dm.@in(id, patient.disease_resistances), "the cure left a resistance");
            // a cure by medicine passes no add_resistance to the cold's cure, so no free cold9 resistance (cold.dm:14: tg's default is null)
            Assert.False(Dm.@in(Dm.text("[]", typeof(datum_disease_cold9)), patient.disease_resistances));
            Assert.False(Dm.truthy(Waiting.NoSleep(patient.ForceContractDisease(Dm.@new<datum_disease_cold>(), FALSE, TRUE))), "immune to the cold");
        });

        // rest: a sleeper lying down at stage 2 feels better (cold.dm:30, prob 23% a second)
        var rest = Infect<datum_disease_cold>(r, sleeper, d => d.stage = 2);
        r.In(() =>
        {
            rest.stage = 2;
            Waiting.NoSleep(sleeper.set_body_position(LYING_DOWN));
            for (int i = 0; i < 200 && Has<datum_disease_cold>(sleeper); i++)
                Waiting.NoSleep(Dm.locate<datum_disease_cold>(sleeper.diseases).stage_act(SSMOBS_DT()));
            Assert.False(Has<datum_disease_cold>(sleeper), "rest cured the cold");
        });
        Assert.True(feed.Any("You feel better."), Said(feed));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void BrainRotIsCuredByMannitol()
    {
        var (r, _) = Room(10303);
        mob_living_carbon_human d = r.Dummy(4, 5);
        var rot = Infect<datum_disease_brainrot>(r, d);
        r.In(() =>
        {
            for (int i = 0; i < 50; i++)
                Waiting.NoSleep(rot.stage_act(SSMOBS_DT()));
            Assert.True(Has<datum_disease_brainrot>(d), "no cure, no recovery: still sick");
            Waiting.NoSleep(Blood(d).add_reagent(typeof(datum_reagent_medicine_mannitol), 50));
            for (int i = 0; i < 500 && Has<datum_disease_brainrot>(d); i++)
                Waiting.NoSleep(Dm.locate<datum_disease_brainrot>(d.diseases).stage_act(SSMOBS_DT()));
            Assert.False(Has<datum_disease_brainrot>(d), "mannitol cured the brain rot");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TwoAdvancedDiseasesMixIntoOneWithBothSymptomsAndTheirStats()
    {
        var (r, _) = Room(10304);
        r.In(() =>
        {
            var cold = Dm.@new<datum_disease_advance_cold>();
            var flu = Dm.@new<datum_disease_advance_flu>();
            Assert.Equal("Cold", cold.name);
            var mixed = (datum_disease_advance)Waiting.NoSleep(Advance_Mix(Dm.list(cold, flu)));
            Assert.NotNull(mixed);
            Assert.NotNull(Dm.locate<datum_symptom_sneeze>(mixed.symptoms));
            Assert.NotNull(Dm.locate<datum_symptom_cough>(mixed.symptoms));
            // the stats are the symptoms' sums (advance.dm GenerateProperties)
            float stealth = 0, resistance = 0, speed = 0, transmission = 0;
            foreach (datum_symptom s in Dm.each<datum_symptom>(mixed.symptoms))
            {
                stealth += s.stealth;
                resistance += s.resistance;
                speed += s.stage_speed;
                transmission += s.transmittable;
            }
            Assert.Equal(stealth, Dm.num(mixed.totalStealth()));
            Assert.Equal(resistance, Dm.num(mixed.totalResistance()));
            Assert.Equal(speed, Dm.num(mixed.totalStageSpeed()));
            Assert.Equal(transmission, Dm.num(mixed.totalTransmittable()));
            // its id is the sorted symptom ids, and it is archived under it with the unknown name until the PanD.E.M.I.C. names it
            Assert.Same(mixed.GetType(), typeof(datum_disease_advance));
            Assert.NotNull(SSdisease.archive_diseases[mixed.GetDiseaseID()]);
            Assert.Equal("Unknown", Dm.str(SSdisease.get_disease_name(mixed.GetDiseaseID())));
        });
        r.AssertNoRuntimes();
    }

    /// <summary>Infects the host with the advanced disease (one advanced disease a host: DISEASE_LIMIT) and returns it.</summary>
    private static datum_disease_advance Give(mob_living host, datum_disease_advance disease, float stage = 5)
    {
        Assert.True(Dm.truthy(Waiting.NoSleep(host.ForceContractDisease(disease, FALSE))), "the advanced disease did not take");
        Assert.Same(host, disease.affected_mob);
        disease.stage = stage;
        return disease;
    }

    [Fact]
    public void TheSymptomsActOnTheirHost()
    {
        var (r, feed) = Room(10305);
        mob_living_carbon_human cougher = r.Join(4, 5, "cougher");
        mob_living_carbon_human itcher = r.Join(4, 8, "itcher");
        r.Join(5, 6, "witness");
        datum_disease_advance respiratory = null;
        mob_living_carbon_human feverish = r.Dummy(8, 5), vomiter = r.Dummy(9, 7), healed = r.Dummy(10, 9);
        r.In(() =>
        {
            // cough and sneeze: the emotes (and an airborne spread to whoever is near)
            respiratory = Give(cougher, Advanced(typeof(datum_symptom_cough), typeof(datum_symptom_sneeze)));
            Activate(respiratory, 40);
        });
        r.Step(2); // the emotes' messages reach the chat on the next ticks
        // the emotes are heard: their sounds (emote.dm's cough and sneeze sounds of a human)
        string heard = string.Join("\n", r.Sounds.Heard.Select(h => h.File));
        Assert.True(r.Sounds.Heard.Any(h => h.File != null && h.File.Contains("/cough/", StringComparison.Ordinal)), heard);
        // the sneeze: its emote and its cooldown, started only past the emote (sneeze.dm:57-66)
        Assert.True(r.In(() => Dm.locate<datum_symptom_sneeze>(respiratory.symptoms).sneeze_cooldown) > 0);
        r.In(() =>
        {

            // fever: the body's normal temperature is raised (fever.dm set_body_temp: add_body_temperature_change, 6 * power * stage)
            var fever = Give(feverish, Advanced(typeof(datum_symptom_fever)));
            float normal = Dm.num(feverish.get_body_temp_normal());
            Activate(fever, 1);
            Assert.True(Dm.num(feverish.get_body_temp_normal_change()) > 0, "the fever's change");
            Assert.True(Dm.num(feverish.get_body_temp_normal()) > normal, $"{normal} -> {Dm.num(feverish.get_body_temp_normal())}");

            // itching: "Your <limb> itches."
            var itch = Give(itcher, Advanced(typeof(datum_symptom_itching)));
            Activate(itch, 5);
            Assert.True(feed.Any("itches"), Said(feed));

            // vomit: a vomit decal under the host
            var vomit = Give(vomiter, Advanced(typeof(datum_symptom_vomit)));
            for (int i = 0; i < 40 && Dm.locate<obj_effect_decal_cleanable_vomit>(vomiter.loc) == null; i++)
            {
                vomiter.nutrition = NUTRITION_LEVEL_FED;
                Activate(vomit, 1);
            }
            Assert.NotNull(Dm.locate<obj_effect_decal_cleanable_vomit>(vomiter.loc));

            // toxolysis, a healing symptom: the chemicals of the blood are broken down
            var heal = Give(healed, Advanced(typeof(datum_symptom_heal_chem)));
            Waiting.NoSleep(Blood(healed).add_reagent(typeof(datum_reagent_water), 10));
            Activate(heal, 3);
            Assert.True(Dm.num(Blood(healed).get_reagent_amount(typeof(datum_reagent_water))) < 10);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACarrierShowsNoSymptom()
    {
        var (r, feed) = Room(10306);
        mob_living_carbon_human carrier = r.Join(4, 5, "carrier");
        var cold = Infect<datum_disease_cold>(r, carrier, d => d.carrier = TRUE);
        r.In(() =>
        {
            cold.stage = 3;
            for (int i = 0; i < 200 && !QDELETED(cold); i++)
                Assert.False(Dm.truthy(Waiting.NoSleep(cold.stage_act(SSMOBS_DT()))));
        });
        Assert.False(feed.Any("Your throat feels sore."), Said(feed));
        Assert.False(feed.Any("Mucous runs down"), Said(feed));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePandemicPrintsACultureTubeAndAVaccineTube()
    {
        var (r, feed) = Room(10307);
        mob_living_carbon_human chemist = r.Join(4, 5, "chemist");
        var pandemic = Place<obj_machinery_computer_pandemic>(r, 5, 5);
        object id = null;
        r.In(() =>
        {
            Assert.False(InertSeam.IsInert(pandemic));
            // a beaker of blood with an advanced cold and the cold's antibodies
            var cold = Dm.@new<datum_disease_advance_cold>();
            id = cold.GetDiseaseID();
            var beaker = Dm.@new<obj_item_reagent_containers_cup_beaker>(pandemic);
            Waiting.NoSleep(((datum_reagents)beaker.reagents).add_reagent(typeof(datum_reagent_blood), 20,
                Dm.list(Dm.kv("viruses", Dm.list(cold)), Dm.kv("resistances", Dm.list(cold.GetDiseaseID())), Dm.kv(BLOOD_DATA_TYPE, "O-"), Dm.kv(BLOOD_DATA_DNA, "abc"))));
            pandemic.beaker = beaker;
            pandemic.update_appearance();
            Assert.Equal("pandemic1", pandemic.icon_state);

            var data = (DmList)Waiting.NoSleep(pandemic.ui_data(chemist));
            Assert.Equal(TRUE, data["has_blood"]);
            var viruses = (DmList)data["viruses"];
            Assert.Equal(1, viruses.len);
            Assert.Equal("Cold", ((DmList)viruses[1])["name"]);

            Assert.Equal(TRUE, Waiting.NoSleep(pandemic.create_culture_bottle(1)));
            var culture = Dm.each<obj_item_reagent_containers_cup_tube>(pandemic.drop_location()).Single();
            Assert.Equal("Cold culture tube", culture.name);
            var blood = Dm.locate<datum_reagent_blood>(((datum_reagents)culture.reagents).reagent_list);
            Assert.NotNull(Dm.locate<datum_disease_advance>((DmList)((DmList)blood.data)["viruses"]));
            Assert.Equal(TRUE, pandemic.wait);
            Assert.Contains("waitlight", Dm.each<object>((DmList)Waiting.NoSleep(pandemic.update_overlays())).Select(o => Dm.str(o)));
        });
        r.Step(6 * Second); // the 5 second cooldown: ping
        r.In(() =>
        {
            Assert.Equal(FALSE, pandemic.wait);
            Assert.Equal(TRUE, Waiting.NoSleep(pandemic.create_vaccine_bottle(id)));
            var vaccine = Dm.each<obj_item_reagent_containers_cup_tube>(pandemic.drop_location()).Single(t => t.name.EndsWith("vaccine tube", StringComparison.Ordinal));
            Assert.Equal(15f, Dm.num(((datum_reagents)vaccine.reagents).get_reagent_amount(typeof(datum_reagent_vaccine))));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheOutbreakEventsInfectACrewman()
    {
        var (r, feed) = Room(10308);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        mob_living_carbon_human alice = r.Join(8, 5, "Alice");
        r.In(() =>
        {
            foreach (mob_living_carbon_human crew in new[] { bob, alice })
                crew.mind.set_assigned_role(SSjob.get_job_type(typeof(datum_job_assistant)) as datum_job);
        });

        // the classic outbreak: a carrier of a classic disease
        var control = r.In(() => Dm.@new<datum_round_event_control_disease_outbreak>());
        r.In(() => control.generate_candidates());
        Assert.Equal(2, r.In(() => control.disease_candidates.len));
        var classic = r.In(() => Dm.@new<datum_round_event_disease_outbreak>(FALSE, control));
        r.In(() =>
        {
            classic.virus_type = typeof(datum_disease_flu);
            Waiting.NoSleep(classic.start());
            Waiting.NoSleep(classic.announce__proc(FALSE));
        });
        datum_disease_flu flu = r.In(() => Dm.locate<datum_disease_flu>(bob.diseases) ?? Dm.locate<datum_disease_flu>(alice.diseases));
        Assert.NotNull(flu);
        Assert.Equal(TRUE, flu.carrier);
        Assert.True(feed.Any("Confirmed outbreak of level 7 viral biohazard"), Said(feed));

        // the advanced outbreak: the healthy crewman gets a random advanced disease of 3 or 4 symptoms
        var advanced_control = r.In(() => Dm.@new<datum_round_event_control_disease_outbreak_advanced>());
        r.In(() => advanced_control.generate_candidates());
        Assert.Equal(1, r.In(() => advanced_control.disease_candidates.len));
        var advanced = r.In(() => Dm.@new<datum_round_event_disease_outbreak_advanced>(FALSE, advanced_control));
        r.In(() => Waiting.NoSleep(advanced.start()));
        var adv = r.In(() => Dm.locate<datum_disease_advance_random_event>(bob.diseases) ?? Dm.locate<datum_disease_advance_random_event>(alice.diseases));
        Assert.NotNull(adv);
        r.In(() =>
        {
            Assert.InRange(adv.symptoms.len, 3, 6); // 3-4 picked, plus an optional modifier and youth
            Assert.False(string.IsNullOrEmpty(adv.name));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMedHudShowsTheVirusAndTheAnalyzerItsLines()
    {
        var (r, _) = Room(10309);
        mob_living_carbon_human doctor = r.Join(4, 5, "doctor");
        mob_living_carbon_human patient = r.Dummy(5, 5);
        r.In(() =>
        {
            patient.med_hud_set_status();
            Assert.Equal("hudhealthy", ((image)patient.hud_list[STATUS_HUD]).icon_state);
        });
        Infect<datum_disease_flu>(r, patient);
        r.In(() =>
        {
            // the flu is minor: hudill1 (data_huds.dm:205)
            patient.med_hud_set_status();
            Assert.Equal("hudill1", ((image)patient.hud_list[STATUS_HUD]).icon_state);
            string scan = Dm.str(healthscan(doctor, patient, SCANNER_VERBOSE, SCANPOWER_ADVANCED, tochat: FALSE));
            Assert.Contains("Warning: Virus detected", scan);
            Assert.Contains("Name: The Flu.", scan);
            Assert.Contains("Stage: 1/3.", scan);
            Assert.Contains("Cure: Spaceacillin", scan);
            // a hidden disease is not shown (HIDDEN_SCANNER)
            var hidden = Dm.@new<datum_disease_advance_cold>();
            hidden.visibility_flags = HIDDEN_SCANNER;
            Assert.True(Dm.truthy(Waiting.NoSleep(patient.ForceContractDisease(hidden, FALSE))));
            Assert.DoesNotContain("Name: Cold.", Dm.str(healthscan(doctor, patient, SCANNER_VERBOSE, SCANPOWER_ADVANCED, tochat: FALSE)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EveryLiveDiseaseAndSymptomActsAndIsCuredWithoutARuntime()
    {
        var (r, _) = Room(10310);
        var diseases = r.In(() => Dm.each<Type>(subtypesof(typeof(datum_disease))).Where(t => !InertSeam.IsInertType(t)).ToList());
        var symptoms = r.In(() => Dm.each<Type>(subtypesof(typeof(datum_symptom))).Where(t => !InertSeam.IsInertType(t) && Dm.num(Dm.initial(t, (datum_symptom s) => s.level)) > 0).ToList());
        Assert.True(diseases.Count >= 30, "live diseases: " + diseases.Count);
        Assert.True(symptoms.Count >= 40, "live symptoms: " + symptoms.Count);
        var skipped = new List<string>();
        int x = 2;
        foreach (Type type in diseases)
        {
            // a transformation moves the host's mind into its new form and may give it an antagonist datum: a joined host has a mind
            mob_living_carbon_human host = typeof(datum_disease_transformation).IsAssignableFrom(type)
                ? r.Join(2 + (x++ % (W - 3)), 4, "host" + x)
                : r.Dummy(2 + (x++ % (W - 3)), 4);
            r.In(() =>
            {
                // the random advanced diseases take their symptom count and level (presets.dm:24, disease_outbreak.dm:318)
                object[] args = typeof(datum_disease_advance_random).IsAssignableFrom(type) ? new object[] { 3, 3, null } : Array.Empty<object>();
                var disease = (datum_disease)Waiting.NoSleep(Dm.new_async(type, args));
                if (!Dm.truthy(Waiting.NoSleep(host.ForceContractDisease(disease, FALSE, TRUE))))
                {
                    skipped.Add(type.Name);
                    return;
                }
                disease.stage = disease.max_stages;
                for (int i = 0; i < 10 && !QDELETED(disease) && !QDELETED(host); i++)
                    Waiting.NoSleep(disease.stage_act(SSMOBS_DT()));
                if (!QDELETED(disease))
                    Waiting.NoSleep(disease.cure(FALSE));
            });
            r.Step(2);
        }
        foreach (Type type in symptoms)
        {
            mob_living_carbon_human host = r.Dummy(2 + (x++ % (W - 3)), 6);
            r.In(() =>
            {
                var disease = Advanced(type);
                if (!Dm.truthy(Waiting.NoSleep(host.ForceContractDisease(disease, FALSE))))
                {
                    skipped.Add(type.Name);
                    return;
                }
                disease.stage = 5;
                Activate(disease, 3);
                for (int i = 0; i < 5 && !QDELETED(disease) && !QDELETED(host); i++)
                    Waiting.NoSleep(disease.stage_act(SSMOBS_DT()));
                if (!QDELETED(disease))
                    Waiting.NoSleep(disease.cure(FALSE));
            });
            r.Step(2);
        }
        r.Step(10 * Second); // the transformations' and the symptoms' timers
        r.AssertNoRuntimes();
    }
}
