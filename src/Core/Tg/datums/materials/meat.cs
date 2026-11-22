// Translated from tgstation (snapshot 1d99bd7f): code/datums/materials/meat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the meat material and its bespoke mob, species and blood meats (the gibber's and the
// meat spike's meat, the flesh toilet). The var blocks are generated (D111).

///It's gross, gets the name of its owner, and is all kinds of fucked up
// tg: code/datums/materials/meat.dm:2
public partial class datum_material_meat : datum_material
{
    // tg: code/datums/materials/meat.dm:24
    public override async DmTask on_main_applied(atom source = null, object mat_amount = null, object multiplier = null)
    {
        await base.on_main_applied(source, mat_amount, multiplier);
        await make_edible(source, mat_amount, multiplier);
        ADD_TRAIT(source, TRAIT_ROD_REMOVE_FISHING_DUD, REF(this)); //The rod itself is the bait... sorta.
        if (isbodypart(source))
        {
            obj_item_bodypart bodypart = (obj_item_bodypart)source;
            if (!Dm.truthy(Dm.bitand(Dm.initial(bodypart, (obj_item_bodypart b) => b.bodytype), BODYTYPE_ORGANIC)))
                Dm.combine(ref bodypart.bodytype, BODYTYPE_ORGANIC);
        }
        if (isorgan(source))
        {
            obj_item_organ organ = (obj_item_organ)source;
            if (!Dm.truthy(Dm.bitand(Dm.initial(organ, (obj_item_organ o) => o.organ_flags), ORGAN_ORGANIC)))
                Dm.combine(ref organ.organ_flags, ORGAN_ORGANIC);
        }
    }

    // tg: code/datums/materials/meat.dm:37
    public override async DmTask on_applied(atom source = null, object mat_amount = null, object multiplier = null, object from_slot = null)
    {
        await base.on_applied(source, mat_amount, multiplier, from_slot);
        if (IS_EDIBLE(source))
            await make_edible(source, mat_amount, multiplier);
    }

    // tg: code/datums/materials/meat.dm:42
    public override async DmTask on_edible_applied(atom source = null, datum_component_edible edible = null)
    {
        datum_reagents reagents = (datum_reagents)source.reagents;
        await reagents.convert_reagent(typeof(datum_reagent_consumable_nutriment), typeof(datum_reagent_consumable_nutriment_protein), keep_data: TRUE);
        datum_reagent meaty_chem = reagents.has_reagent(typeof(datum_reagent_consumable_nutriment_protein)) as datum_reagent;
        if (meaty_chem != null)
        {
            DmList data__1 = meaty_chem.data as DmList;
            LAZYSET(ref data__1, "meat", (Dm.length(meaty_chem.data) is var or__1 && Dm.truthy(or__1) ? or__1 : 1)); //adds meatiness to its tastes
            meaty_chem.data = data__1;
        }
        await reagents.convert_reagent(typeof(datum_reagent_consumable_nutriment_fat_oil), typeof(datum_reagent_consumable_nutriment_fat), include_source_subtypes: TRUE, keep_data: TRUE);
        meaty_chem = reagents.has_reagent(typeof(datum_reagent_consumable_nutriment_fat)) as datum_reagent;
        if (meaty_chem != null)
        {
            DmList data__2 = meaty_chem.data as DmList;
            LAZYSET(ref data__2, "meat", (Dm.length(meaty_chem.data) is var or__2 && Dm.truthy(or__2) ? or__2 : 1)); //adds meatiness to its tastes
            meaty_chem.data = data__2;
        }
        await source.AddComponentFrom(SOURCE_EDIBLE_MEAT_MAT, typeof(datum_component_edible), Dm.named("foodtypes", MEAT));
    }

    // tg: code/datums/materials/meat.dm:53
    public override async DmTask on_edible_removed(atom source = null, datum_component_edible edible = null)
    {
        datum_reagents reagents = (datum_reagents)source.reagents;
        await reagents.convert_reagent(typeof(datum_reagent_consumable_nutriment_protein), typeof(datum_reagent_consumable_nutriment), keep_data: TRUE);
        datum_reagent unmeaty_chem = reagents.has_reagent(typeof(datum_reagent_consumable_nutriment)) as datum_reagent;
        if (unmeaty_chem != null)
        {
            DmList data__1 = unmeaty_chem.data as DmList;
            LAZYREMOVE(ref data__1, "meat");
            unmeaty_chem.data = data__1;
        }
        await reagents.convert_reagent(typeof(datum_reagent_consumable_nutriment_fat), typeof(datum_reagent_consumable_nutriment_fat_oil), keep_data: TRUE);
        unmeaty_chem = reagents.has_reagent(typeof(datum_reagent_consumable_nutriment_fat_oil)) as datum_reagent;
        if (unmeaty_chem != null)
        {
            DmList data__2 = unmeaty_chem.data as DmList;
            LAZYREMOVE(ref data__2, "meat");
            unmeaty_chem.data = data__2;
        }
        //the edible source is removed by on_removed()
    }

    // tg: code/datums/materials/meat.dm:64
    public virtual async DmTask make_edible(atom source = null, object mat_amount = null, object multiplier = null)
    {
        if (Dm.truthy(Dm.bitand(source.material_flags, MATERIAL_NO_EDIBILITY)))
            return;
        float protein_count = 3 * (Dm.num(mat_amount) / SHEET_MATERIAL_AMOUNT) * Dm.num(multiplier);
        float fat_count = 2 * (Dm.num(mat_amount) / SHEET_MATERIAL_AMOUNT) * Dm.num(multiplier);
        await source.AddComponentFrom(
            SOURCE_EDIBLE_MEAT_MAT,
            typeof(datum_component_edible),
            Dm.named("initial_reagents", Dm.list(Dm.kv(typeof(datum_reagent_consumable_nutriment_protein), protein_count), Dm.kv(typeof(datum_reagent_consumable_nutriment_fat), fat_count))),
            Dm.named("foodtypes", Dm.bitor(RAW, MEAT)),
            Dm.named("eat_time", 3 * SECONDS),
            Dm.named("tastes", Dm.list(Dm.kv("meat", 1))),
            Dm.named("handmade_complexity", Dm.initial(typeof(obj_item_food_meat_steak), (obj_item_food f) => f.crafting_complexity)));

        await source.AddComponent(
            typeof(datum_component_bloody_spreader),
            Dm.named("blood_left", (protein_count + fat_count) * 0.3f * Dm.num(multiplier)),
            Dm.named("blood_dna", blood_dna));

        // Turfs can't handle the meaty goodness of blood walk.
        if (!ismovable(source))
            return;

        await source.AddComponent(
            typeof(datum_component_blood_walk),
            Dm.named("blood_type", typeof(obj_effect_decal_cleanable_blood)),
            Dm.named("blood_spawn_chance", 35),
            Dm.named("max_blood", (protein_count + fat_count) * 0.3f * Dm.num(multiplier)),
            Dm.named("blood_dna_info", blood_dna));
    }

    // tg: code/datums/materials/meat.dm:96
    public override void on_removed(atom source = null, object mat_amount = null, object material_flags = null, object from_slot = null)
    {
        base.on_removed(source, mat_amount, material_flags, from_slot);
        source.RemoveComponentSource(SOURCE_EDIBLE_MEAT_MAT, typeof(datum_component_edible));
        qdel(source.GetComponent(typeof(datum_component_blood_walk)));
        qdel(source.GetComponent(typeof(datum_component_bloody_spreader)));
        source.RemoveComponentSource(SOURCE_EDIBLE_MEAT_MAT, typeof(datum_component_edible));
    }

    // tg: code/datums/materials/meat.dm:103
    public override void on_main_removed(atom source = null, object mat_amount = null, object multiplier = null)
    {
        base.on_main_removed(source, mat_amount, multiplier);
        REMOVE_TRAIT(source, TRAIT_ROD_REMOVE_FISHING_DUD, REF(this));
        if (isbodypart(source))
        {
            obj_item_bodypart bodypart = (obj_item_bodypart)source;
            if (!Dm.truthy(Dm.bitand(Dm.initial(bodypart, (obj_item_bodypart b) => b.bodytype), BODYTYPE_ORGANIC)))
                Dm.mask(ref bodypart.bodytype, Dm.bitnot(BODYTYPE_ORGANIC));
        }
        if (isorgan(source))
        {
            obj_item_organ organ = (obj_item_organ)source;
            if (!Dm.truthy(Dm.bitand(Dm.initial(organ, (obj_item_organ o) => o.organ_flags), ORGAN_ORGANIC)))
                Dm.mask(ref organ.organ_flags, Dm.bitnot(ORGAN_ORGANIC));
        }
    }
}

// tg: code/datums/materials/meat.dm:115
public partial class datum_material_meat_mob_meat : datum_material_meat
{
    // tg: code/datums/materials/meat.dm:120
    public override async DmTask<object> Initialize(object _id = null, params object[] __extra)
    {
        mob_living source = Dm.arg(__extra, 0) as mob_living;
        if (source == null)
            return FALSE;

        name = Dm.text("[] []", Dm.truthy(source?.name) ? Dm.text("[]'s", source.name) : "mystery", Dm.initial(this, (datum_material m) => m.name));

        if (Dm.truthy(source.real_name))
            subjectname = Dm.str(source.real_name);
        else if (Dm.truthy(source.name))
            subjectname = source.name;

        datum_blood_type blood_type = source.get_bloodtype() as datum_blood_type;
        if (blood_type != null && !Dm.eq(await blood_type.get_color(), BLOOD_COLOR_RED))
        {
            DmList transition_filter = color_transition_filter(await blood_type.get_color()) as DmList;
            color = transition_filter["color"];
            blood_dna = source.get_blood_dna_list() as DmList;
        }

        if (ishuman(source))
        {
            mob_living_carbon_human human_source = (mob_living_carbon_human)source;
            subjectjob = human_source.job;
        }

        return await base.Initialize(_id, __extra);
    }
}

// tg: code/datums/materials/meat.dm:143
public partial class datum_material_meat_species_meat : datum_material_meat
{
    // tg: code/datums/materials/meat.dm:146
    public override async DmTask<object> Initialize(object _id = null, params object[] __extra)
    {
        datum_species source = Dm.arg(__extra, 0) as datum_species;
        if (source == null)
            return FALSE;

        name = Dm.text("[] []", (source?.name is var or__1 && Dm.truthy(or__1) ? or__1 : "mystery"), Dm.initial(this, (datum_material m) => m.name));

        if (source.exotic_bloodtype != null)
        {
            datum_blood_type blood_type = get_blood_type(source.exotic_bloodtype) as datum_blood_type;
            if (blood_type != null && !Dm.eq(await blood_type.get_color(), BLOOD_COLOR_RED))
            {
                DmList transition_filter = color_transition_filter(await blood_type.get_color()) as DmList;
                color = transition_filter["color"];
                blood_dna = Dm.list(Dm.kv(Dm.text("[]", blood_type.dna_string), blood_type));
            }
        }

        return await base.Initialize(_id, __extra);
    }
}

// tg: code/datums/materials/meat.dm:161
public partial class datum_material_meat_blood_meat : datum_material_meat
{
    // tg: code/datums/materials/meat.dm:164
    public override async DmTask<object> Initialize(object _id = null, params object[] __extra)
    {
        datum_reagent source = Dm.arg(__extra, 0) as datum_reagent;
        if (source == null)
            return FALSE;

        DmList blood_data = source.data as DmList;
        name = Dm.text("[] []", (blood_data["real_name"] is var or__1 && Dm.truthy(or__1) ? or__1 : "mystery"), Dm.initial(this, (datum_material m) => m.name));
        datum_blood_type blood_type = blood_data[BLOOD_DATA_TYPE] as datum_blood_type;
        if (blood_type != null && !Dm.eq(await blood_type.get_color(), BLOOD_COLOR_RED))
        {
            DmList transition_filter = color_transition_filter(await blood_type.get_color()) as DmList;
            color = transition_filter["color"];
            blood_dna = Dm.list(Dm.kv(Dm.text("[]", blood_type.dna_string), blood_type));
        }
        else
            color = source.color;

        return await base.Initialize(_id, __extra);
    }
}
