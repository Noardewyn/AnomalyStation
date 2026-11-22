// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/recipes/others.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the foam reactions (gaps F1: the foam family, metal, iron and smart foam, the foaming agents) and the others
// (gaps P5: the solidifications, soaps, candles and meat, nitrous oxide, the virus mixes, the life and mob spawns, the fish, the
// monkeys, the screams, plastic, slime extracts, metalgen, bone gel, eigenstasium, the ants, hauntium, glitter, the carnivorous
// blood). The var-only reactions are live under a live parent; their vars are generated. The virus mixes' Evolve, Devolve and
// Neuter came with the diseases (gaps VI1). Left (Dm.Deferred): the carnivorous blood's feed_dna_list (an inert toxin); the
// admin messages and game logs are comments (logs, M10+).

// tg: code/modules/reagents/chemistry/recipes/others.dm:358
public partial class datum_chemical_reaction_foam : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:388
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        await holder.create_foam(typeof(datum_effect_system_fluid_spread_foam), 2 * Dm.num(created_volume), notification: span_danger("The solution spews out foam!"), log: TRUE, lifetime: lifetime, slippery: slippery);
        reaction_tags = Dm.bitor(REACTION_TAG_EASY, REACTION_TAG_UNIQUE); // tg sets it here, inside on_reaction (others.dm:390)
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:367
public partial class datum_chemical_reaction_foam_hollow : datum_chemical_reaction_foam
{
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:371
public partial class datum_chemical_reaction_foam_salt : datum_chemical_reaction_foam
{
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:376
public partial class datum_chemical_reaction_foam_ice : datum_chemical_reaction_foam
{
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:380
public partial class datum_chemical_reaction_foam_soda : datum_chemical_reaction_foam
{
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:384
public partial class datum_chemical_reaction_foam_holy : datum_chemical_reaction_foam
{
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:393
public partial class datum_chemical_reaction_metalfoam : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:398
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        await holder.create_foam(typeof(datum_effect_system_fluid_spread_foam_metal), 5 * Dm.num(created_volume), typeof(obj_structure_foamedmetal), span_danger("The solution spews out a metallic foam!"), log: TRUE);
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:402
public partial class datum_chemical_reaction_smart_foam : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:407
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        await holder.create_foam(typeof(datum_effect_system_fluid_spread_foam_metal_smart), 5 * Dm.num(created_volume), typeof(obj_structure_foamedmetal), span_danger("The solution spews out metallic foam!"), log: TRUE);
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:411
public partial class datum_chemical_reaction_ironfoam : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:416
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        await holder.create_foam(typeof(datum_effect_system_fluid_spread_foam_metal_iron), 5 * Dm.num(created_volume), typeof(obj_structure_foamedmetal_iron), span_danger("The solution spews out a metallic foam!"), log: TRUE);
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:420
public partial class datum_chemical_reaction_foaming_agent : datum_chemical_reaction
{
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:425
public partial class datum_chemical_reaction_smart_foaming_agent : datum_chemical_reaction
{
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:46
public partial class datum_chemical_reaction_sodiumchloride : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:52
    public override async DmTask<object> pre_reaction_other_checks(datum_reagents holder = null)
    {
        // Snowflake check to allow electrolysis to work
        await DmTask.CompletedTask;
        return !Dm.truthy(holder.has_reagent(typeof(datum_reagent_consumable_liquidelectricity))) ? TRUE : FALSE;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:62
public partial class datum_chemical_reaction_plasma_solidification : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:68
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_stack_sheet_mineral_plasma>(get_turf(holder.my_atom), round(Dm.num(created_volume)));
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:71
public partial class datum_chemical_reaction_gold_solidification : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:77
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_stack_sheet_mineral_gold>(get_turf(holder.my_atom), round(Dm.num(created_volume)));
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:80
public partial class datum_chemical_reaction_uranium_solidification : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:86
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_stack_sheet_mineral_uranium>(get_turf(holder.my_atom), round(Dm.num(created_volume)));
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:94
public partial class datum_chemical_reaction_soapification : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:101
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf location = get_turf(holder.my_atom);
        for (float i = 1; i <= Dm.num(created_volume); i++)
            Dm.@new<obj_item_soap_homemade>(location);
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:106
public partial class datum_chemical_reaction_omegasoapification : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:115
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf location = get_turf(holder.my_atom);
        for (float i = 1; i <= Dm.num(created_volume); i++)
            Dm.@new<obj_item_soap_omega>(location);
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:120
public partial class datum_chemical_reaction_candlefication : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:127
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf location = get_turf(holder.my_atom);
        for (float i = 1; i <= Dm.num(created_volume); i++)
            Dm.@new<obj_item_flashlight_flare_candle>(location);
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:132
public partial class datum_chemical_reaction_meatification : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:138
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf location = get_turf(holder.my_atom);
        for (float i = 1; i <= Dm.num(created_volume); i++)
            Dm.@new<obj_item_food_meat_slab_meatproduct>(location);
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:149
public partial class datum_chemical_reaction_nitrous_oxide : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:161
    public override async DmTask<object> overly_impure(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null)
    {
        object dot = await base.overly_impure(holder, equilibrium, step_volume_added);
        turf exposed_turf = get_turf(holder.my_atom);
        if (exposed_turf == null)
            return dot;
        exposed_turf.atmos_spawn_air(Dm.text("[]=[];[]", GAS_N2O, equilibrium.step_target_vol / 2, TURF_TEMPERATURE(holder.chem_temp)));
        await clear_products(holder, equilibrium.step_target_vol);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/recipes/others.dm:169
    public override async DmTask<object> overheated(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        return null; //This is empty because the explosion reaction will occur instead (see pyrotechnics.dm). This is just here to update the lookup ui.
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:234
public partial class datum_chemical_reaction_mix_virus : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:243
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        datum_reagent_blood B = Dm.locate_type(typeof(datum_reagent_blood), holder.reagent_list) as datum_reagent_blood;
        if (B == null)
            return null;
        if (!Dm.truthy(B.data))
            B.data = Dm.list();

        datum_disease_advance D = Dm.locate_type(typeof(datum_disease_advance), (B.data as DmList)["viruses"]) as datum_disease_advance;
        if (D != null)
        {
            await D.Evolve(level_min, level_max); // gaps VI1
        }

        // All blood duplicated via this reaction is synthetic.
        float added_synth_volume = Dm.num(created_volume);

        // Blood reagents handle their merger code BEFORE this code runs. So the existing synth volume is already averaged into the total volume.
        float existing_synth_volume = Dm.num(B.volume) * Dm.num((B.data as DmList)[BLOOD_DATA_SYNTH_CONTENT]);

        // A simple weighted average that simplifies down to "total synth volume / total blood volume" i.e. "how much of the blood is synthetic"
        (B.data as DmList)[BLOOD_DATA_SYNTH_CONTENT] = (added_synth_volume + existing_synth_volume) / Dm.num(B.volume);
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:323
public partial class datum_chemical_reaction_mix_virus_rem_virus : datum_chemical_reaction_mix_virus
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:327
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        datum_reagent_blood B = Dm.locate_type(typeof(datum_reagent_blood), holder.reagent_list) as datum_reagent_blood;
        if (Dm.truthy(B?.data))
        {
            datum_disease_advance D = Dm.locate_type(typeof(datum_disease_advance), (B.data as DmList)["viruses"]) as datum_disease_advance;
            if (D != null)
                await D.Devolve(); // gaps VI1
        }
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:334
public partial class datum_chemical_reaction_mix_virus_neuter_virus : datum_chemical_reaction_mix_virus
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:338
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        datum_reagent_blood B = Dm.locate_type(typeof(datum_reagent_blood), holder.reagent_list) as datum_reagent_blood;
        if (Dm.truthy(B?.data))
        {
            datum_disease_advance D = Dm.locate_type(typeof(datum_disease_advance), (B.data as DmList)["viruses"]) as datum_disease_advance;
            if (D != null)
                await D.Neuter(); // gaps VI1
        }
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:601
public partial class datum_chemical_reaction_life : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:607
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        await chemical_mob_spawn(holder, rand(1, round(Dm.num(created_volume), 1)), "Life (hostile)"); //defaults to HOSTILE_SPAWN
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:610
public partial class datum_chemical_reaction_life_friendly : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:616
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        await chemical_mob_spawn(holder, rand(1, round(Dm.num(created_volume), 1)), "Life (friendly)", FRIENDLY_SPAWN, mob_faction: FACTION_NEUTRAL);
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:619
public partial class datum_chemical_reaction_corgium : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:625
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf location = get_turf(holder.my_atom);
        for (float i = 1, i_end = rand(1, Dm.num(created_volume)); i <= i_end; i++) // More lulz.
            Dm.@new<mob_living_basic_pet_dog_corgi>(location);
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:631
public partial class datum_chemical_reaction_lifish : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:637
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf location = get_turf(holder.my_atom);

        // create fish possibles
        DmList fish_types = Dm.list();
        foreach (Type path in Dm.each_cast<Type>(subtypesof(typeof(obj_item_fish))))
        {
            if (Dm.initial(path, (obj_item_fish f) => f.random_case_rarity) == FISH_RARITY_NOPE) // means they aren't mean to be randomly available
                continue;
            Dm.combine(ref fish_types, path);
        }

        // spawn from popssible fishes
        for (float i = 1, i_end = rand(1, Dm.num(created_volume)); i <= i_end; i++) // More flop.
        {
            Type spawned_fish = pick(fish_types) as Type;
            obj_item_fish new_fish = Dm.@new(spawned_fish, location) as obj_item_fish;
            ADD_TRAIT(new_fish, TRAIT_NO_FISHING_ACHIEVEMENT, TRAIT_GENERIC);
        }
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:662
public partial class datum_chemical_reaction_monkey : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:667
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        mob_living_carbon maybe_monkey = holder.my_atom as mob_living_carbon;
        object location = holder.my_atom.drop_location();
        if (iscarbon(maybe_monkey))
        {
            if (HAS_TRAIT(maybe_monkey, TRAIT_LESSER_HUMANOID))
            {
                await maybe_monkey.gib(DROP_ALL_REMAINS);
                to_chat(maybe_monkey, span_danger(I18n.T("You body is torn to shreds as a monkey bursts out of you!")));
            }
            else
            {
                await maybe_monkey.vomit(VOMIT_CATEGORY_BLOOD);
                to_chat(maybe_monkey, span_danger(I18n.T("You vomit out blood, making you feel grossly monkeyish.")));
            }

            object monkey_cap = config.Get(typeof(datum_config_entry_number_monkeycap));
            for (float _ = 1; _ <= Dm.num(created_volume); _++)
            {
                if (Dm.num(LAZYLEN(datum_controller_subsystem_mobs.cubemonkeys)) < Dm.num(monkey_cap))
                    Dm.@new<mob_living_carbon_human_species_monkey>(location, null, TRUE);
            }
        }
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:683
public partial class datum_chemical_reaction_angry_monkey : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:688
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        mob_living_carbon maybe_monkey = holder.my_atom as mob_living_carbon;
        object location = holder.my_atom.drop_location();
        if (iscarbon(maybe_monkey))
        {
            if (HAS_TRAIT(maybe_monkey, TRAIT_LESSER_HUMANOID))
            {
                await maybe_monkey.gib(DROP_ALL_REMAINS);
                to_chat(maybe_monkey, span_danger(I18n.T("You body is torn to shreds as a pissed off monkey bursts out of you!")));
            }
            else
            {
                await maybe_monkey.vomit(VOMIT_CATEGORY_BLOOD);
                to_chat(maybe_monkey, span_danger(I18n.T("You vomit out blood, making you feel grossly monkeyish.")));
            }

            object monkey_cap = config.Get(typeof(datum_config_entry_number_monkeycap));
            for (float _ = 1; _ <= Dm.num(created_volume); _++)
            {
                if (Dm.num(LAZYLEN(datum_controller_subsystem_mobs.cubemonkeys)) < Dm.num(monkey_cap))
                    Dm.@new<mob_living_carbon_human_species_monkey_angry>(location, null, TRUE);
            }
        }
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:719
public partial class datum_chemical_reaction_butterflium : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:724
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf location = get_turf(holder.my_atom);
        for (float i = 1, i_end = rand(1, Dm.num(created_volume)); i <= i_end; i++)
            Dm.@new<mob_living_basic_butterfly>(location);
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:730
public partial class datum_chemical_reaction_scream : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:736
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        /// List of screams to play.
        // var/static/list/screams (rules 10.6: an immutable list, rebuilt per call)
        DmList screams = Dm.list(
            Dm.resource("sound/mobs/humanoids/human/scream/femalescream_1.ogg"),
            Dm.resource("sound/mobs/humanoids/human/scream/femalescream_2.ogg"),
            Dm.resource("sound/mobs/humanoids/human/scream/femalescream_3.ogg"),
            Dm.resource("sound/mobs/humanoids/human/scream/femalescream_4.ogg"),
            Dm.resource("sound/mobs/humanoids/human/scream/femalescream_5.ogg"),
            Dm.resource("sound/mobs/humanoids/human/scream/malescream_1.ogg"),
            Dm.resource("sound/mobs/humanoids/human/scream/malescream_2.ogg"),
            Dm.resource("sound/mobs/humanoids/human/scream/malescream_3.ogg"),
            Dm.resource("sound/mobs/humanoids/human/scream/malescream_4.ogg"),
            Dm.resource("sound/mobs/humanoids/human/scream/malescream_5.ogg"),
            Dm.resource("sound/mobs/humanoids/human/scream/malescream_6.ogg"),
            Dm.resource("sound/mobs/humanoids/human/scream/wilhelm_scream.ogg"));

        playsound(holder.my_atom, pick(screams), Dm.num(created_volume) * 5, TRUE);
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:807
public partial class datum_chemical_reaction_plastic_polymer_hardening : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:813
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_stack_sheet_plastic>(get_turf(holder.my_atom), round(Dm.num(created_volume)));
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:835
public partial class datum_chemical_reaction_slime_extractification : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:841
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf location = get_turf(holder.my_atom);
        Dm.@new<obj_item_slime_extract_grey>(location);
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:845
public partial class datum_chemical_reaction_metalgen_imprint : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:851
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        datum_reagent_metalgen metalgen = holder.has_reagent(typeof(datum_reagent_metalgen)) as datum_reagent_metalgen;
        foreach (datum_reagent metal in Dm.each<datum_reagent>(holder.reagent_list))
        {
            if (metal.material == null || Dm.num(metal.volume) < 40)
                continue;

            (metalgen.data as DmList)["material"] = metal.material;
            await holder.remove_reagent(metal.type, 40);
            atom container = holder.my_atom;
            area container_area = get_area(container) as area;
            string blame_msg = "with no known fingerprints";
            object lastkey = container.fingerprintslast;
            if (Dm.truthy(lastkey))
            {
                mob scapegoat = get_mob_by_key(lastkey) as mob;
                blame_msg = Dm.text("last touched by []", ADMIN_LOOKUPFLW(scapegoat));
            }

            if (!istype(container, typeof(obj_machinery_plumbing)) && !Dm.truthy(Dm.bitand(container_area?.area_flags, QUIET_LOGS)))
                message_admins(Dm.text("[]u of Metalgen have been imprinted with [] in [] at [] []", metalgen.volume, Dm.initial(metal.material, (datum_material m) => m.name), container, ADMIN_VERBOSEJMP(container), blame_msg));
            log_game(Dm.text("[]u of Metalgen have been imprinted with [] in [] at [] []", metalgen.volume, Dm.initial(metal.material, (datum_material m) => m.name), container, AREACOORD(container), blame_msg));
            break;
        }
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:920
public partial class datum_chemical_reaction_silver_solidification : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:927
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_stack_sheet_mineral_silver>(get_turf(holder.my_atom), round(Dm.num(created_volume)));
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:930
public partial class datum_chemical_reaction_bone_gel : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:938
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_stack_medical_bone_gel_one>(get_turf(holder.my_atom), round(Dm.num(created_volume)));
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:989
public partial class datum_chemical_reaction_eigenstate : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:1010
    public override async DmTask<object> reaction_finish(datum_reagents holder = null, datum_equilibrium reaction = null, object react_vol = null)
    {
        object dot = await base.reaction_finish(holder, reaction, react_vol);
        turf location = get_turf(holder.my_atom);
        if (Dm.eq(reaction.data["ducts_teleported"], TRUE)) //If we teleported a duct, then we reconnect it at the end
        {
            foreach (obj_item_stack_ducts duct in Dm.each<obj_item_stack_ducts>(range(location, 3)))
                await duct.check_attach_turf(duct.loc as turf);
        }

        datum_reagent_eigenstate eigen = holder.has_reagent(typeof(datum_reagent_eigenstate)) as datum_reagent_eigenstate;
        if (eigen == null)
            return dot;
        if (location != null)
        {
            eigen.location_created = location;
            (eigen.data as DmList)["location_created"] = location;
        }

        do_sparks(5, FALSE, location);
        playsound(location, Dm.resource("sound/effects/phasein.ogg"), 80, TRUE);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/recipes/others.dm:1027
    public override async DmTask<object> overheated(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null, params object[] __extra)
    {
        object dot = await base.overheated(holder, equilibrium, step_volume_added, __extra);
        if (!Dm.truthy(off_cooldown(holder, equilibrium, 0.5f, "eigen")))
            return dot;
        turf location = get_turf(holder.my_atom);
        do_sparks(3, FALSE, location);
        playsound(location, Dm.resource("sound/effects/phasein.ogg"), 80, TRUE);
        foreach (mob_living nearby_mob in Dm.each<mob_living>(range(location, 3)))
        {
            do_sparks(3, FALSE, nearby_mob);
            await do_teleport(nearby_mob, get_turf(holder.my_atom), 3, no_effects: TRUE);
            await nearby_mob.Knockdown(20, ignore_canstun: TRUE);
            await nearby_mob.add_atom_colour("#cebfff", WASHABLE_COLOUR_PRIORITY);
            do_sparks(3, FALSE, nearby_mob);
        }
        await clear_products(holder, Dm.num(step_volume_added));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/recipes/others.dm:1042
    public override async DmTask<object> overly_impure(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null)
    {
        if (!Dm.truthy(off_cooldown(holder, equilibrium, 1, "eigen")))
            return null;
        turf location = get_turf(holder.my_atom);
        do_sparks(3, FALSE, location);
        holder.chem_temp += 10;
        playsound(location, Dm.resource("sound/effects/phasein.ogg"), 80, TRUE);
        foreach (obj_machinery_duct duct in Dm.each<obj_machinery_duct>(range(location, 3)))
        {
            await do_teleport(duct, location, 3, no_effects: TRUE);
            equilibrium.data["ducts_teleported"] = TRUE; //If we teleported a duct - call the process in
        }
        float lets_not_go_crazy = 15; //Teleport 15 items at max
        DmList items = Dm.list();
        foreach (obj_item item in Dm.each<obj_item>(range(location, 3)))
            Dm.append(items, item);
        shuffle_inplace(items);
        foreach (obj_item item in Dm.each_cast<obj_item>(items))
        {
            await do_teleport(item, location, 3, no_effects: TRUE);
            lets_not_go_crazy -= 1;
            await item.add_atom_colour("#c4b3fd", WASHABLE_COLOUR_PRIORITY);
            if (!Dm.truthy(lets_not_go_crazy))
            {
                await clear_products(holder, Dm.num(step_volume_added));
                return null;
            }
        }
        await clear_products(holder, Dm.num(step_volume_added));
        // [icon2html(holder.my_atom, viewers(DEFAULT_MESSAGE_RANGE, src))]: the container's picture in the chat line (the chat icons)
        holder.my_atom.audible_message(span_notice(" The reaction gives out a fizz, teleporting items everywhere!"));
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:1077
public partial class datum_chemical_reaction_ant_slurry : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:1083
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf location = get_turf(holder.my_atom);
        for (float i = 1, i_end = rand(1, Dm.num(created_volume)); i <= i_end; i++)
            Dm.@new<mob_living_basic_ant>(location);
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:1089
public partial class datum_chemical_reaction_hauntium_solidification : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:1095
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_stack_sheet_hauntium>(get_turf(holder.my_atom), round(Dm.num(created_volume)));
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:1117
public partial class datum_chemical_reaction_glitter_pigmentation : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:1124
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        object dot = await base.on_reaction(holder, reaction, created_volume, __extra);

        float excluded_volume = 0;
        object accumulated_color = null;

        foreach (datum_reagent reagent in Dm.each<datum_reagent>(holder.reagent_list))
        {
            if (Dm.truthy(is_type_in_list(reagent, required_reagents)))
                excluded_volume += Dm.num(reagent.volume);
        }

        foreach (datum_reagent reagent in Dm.each<datum_reagent>(holder.reagent_list))
        {
            if (!Dm.truthy(is_type_in_list(reagent, required_reagents)))
            {
                if (Dm.truthy(accumulated_color))
                    accumulated_color = BlendRGB(reagent.color, accumulated_color, Dm.num(reagent.volume) / (holder.total_volume - excluded_volume));
                else
                    accumulated_color = reagent.color;
            }
        }

        datum_reagent_glitter glitter = Dm.locate_type(typeof(datum_reagent_glitter), holder.reagent_list) as datum_reagent_glitter;

        (glitter.data as DmList)["colors"] = Dm.list(Dm.kv(Dm.text("[]", accumulated_color), 100));
        glitter.color = accumulated_color as string;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/recipes/others.dm:1147
public partial class datum_chemical_reaction_pair_carnivorous_blood : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/others.dm:1153
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        object hungryblood = holder.has_reagent(typeof(datum_reagent_toxin_carnivorousblood));
        DmList new_blood_dna = Dm.list();
        foreach (datum_reagent_blood bloodinstance in Dm.each<datum_reagent_blood>(holder.reagent_list))
            Dm.append(new_blood_dna, (bloodinstance.data as DmList)?[BLOOD_DATA_DNA]);
        // hungryblood.feed_dna_list(new_blood_dna): the carnivorous blood is an inert toxin (its own procs come with the toxins)
        Dm.Deferred("code/modules/reagents/chemistry/recipes/others.dm:1158", "pair_carnivorous_blood on_reaction: the carnivorous blood's feed_dna_list (an inert toxin reagent)");
        _ = hungryblood;
        await DmTask.CompletedTask;
        return null;
    }
}
