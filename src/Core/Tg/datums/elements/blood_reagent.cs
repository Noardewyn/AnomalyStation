// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/blood_reagent.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice C (D192): the whole file (drawn blood keeps its type, colour, DNA and donor data, bloodies what it touches,
// mixes and moves with its data). The viruses' copies, infections, cures and mixing came with the diseases (gaps VI1).
// `blood_type` is declared here (the generator types it
// Type), so the type's overrides are hand-written too.

/// Element that blood reagents use to apply their data (not blood regen!) to mobs
/// Only added to blood drawn *from* someone, so don't put behavior that should work with any reagent onto this
// tg: code/datums/elements/blood_reagent.dm:3
public partial class datum_element_blood_reagent : datum_element
{
    /// Blood type associated with this element
    // tg: code/datums/elements/blood_reagent.dm:7
    public datum_blood_type blood_type;

    // tg: code/datums/elements/blood_reagent.dm:4-5  The type's overrides (rules 10.2).
    public datum_element_blood_reagent()
    {
        element_flags = ELEMENT_BESPOKE;
        argument_hash_start_idx = 3;
    }

    /*
     * Arguments:
     * * blood_source - mob this blood came from, can be null
     * * blood_type - blood type datum we grab data and behavior from, cannot be null
     */
    // tg: code/datums/elements/blood_reagent.dm:14
    [DmParams("target", "blood_source", "blood_type")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        mob_living blood_source = Dm.arg(__extra, 0) as mob_living;
        datum_blood_type blood_type = Dm.arg(__extra, 1) as datum_blood_type;
        object dot = await base.Attach(target, __extra);
        if (target is not datum_reagent reagent || blood_type == null)
            return ELEMENT_INCOMPATIBLE;

        this.blood_type = blood_type;
        RegisterSignal(reagent, COMSIG_REAGENT_EXPOSE_MOB, nameof(on_mob_expose));
        RegisterSignal(reagent, COMSIG_REAGENT_EXPOSE_TURF, nameof(on_turf_expose));
        RegisterSignal(reagent, COMSIG_REAGENT_EXPOSE_OBJ, nameof(on_obj_expose));
        RegisterSignal(reagent, COMSIG_REAGENT_ON_MERGE, nameof(on_merge));
        RegisterSignal(reagent, COMSIG_REAGENT_ON_TRANSFER, nameof(on_transfer));

        if (!Dm.truthy(reagent.data))
            reagent.data = Dm.list();
        DmList target_data_all = (DmList)reagent.data;
        target_data_all[BLOOD_DATA_TYPE] = blood_type;
        if (Dm.truthy(blood_type.desc))
            reagent.description = Dm.text("[]", blood_type.desc);
        reagent.color = Dm.text("[]", await blood_type.get_color());

        if (blood_source == null)
        {
            reagent.material = await SSmaterials.get_material(typeof(datum_material_meat_blood_meat), reagent);
            return dot;
        }

        reagent.material = await SSmaterials.get_material(typeof(datum_material_meat_mob_meat), blood_source);
        DmList blood_data = await blood_source.get_blood_data() as DmList;
        if (Dm.truthy(blood_data["viruses"]))
        {
            DmList to_preserve = Dm.list();
            foreach (datum_disease disease in Dm.each<datum_disease>(blood_data["viruses"]))
                Dm.append(to_preserve, await disease.Copy()); // gaps VI1
            blood_data["viruses"] = to_preserve;
        }

        foreach (object key in Dm.each(blood_data))
        {
            if (!islist(blood_data[key]))
            {
                target_data_all[key] = blood_data[key];
                continue;
            }

            DmList data_list = (DmList)blood_data[key];
            DmList target_data = target_data_all[key] as DmList;
            if (target_data == null)
                target_data_all[key] = data_list.Copy();
            else
            {
                // Concat viruses, resistances, etc
                Dm.combine(ref target_data, data_list);
            }
        }
        return dot;
    }

    // Shouldn't realistically happen but just in case
    // tg: code/datums/elements/blood_reagent.dm:55
    public override object Detach(datum target = null, params object[] __extra)
    {
        object dot = base.Detach(target, __extra);
        UnregisterSignal(target, Dm.list(
            COMSIG_REAGENT_EXPOSE_MOB,
            COMSIG_REAGENT_EXPOSE_TURF,
            COMSIG_REAGENT_EXPOSE_OBJ,
            COMSIG_REAGENT_ON_MERGE,
            COMSIG_REAGENT_ON_TRANSFER
        ));
        return dot;
    }

    /// Cover the mob in blood and transfer our viruses and resistances to them
    // tg: code/datums/elements/blood_reagent.dm:66
    [SignalHandler]
    public virtual void on_mob_expose(datum_reagent source = null, mob_living exposed_mob = null, object methods = null, object reac_volume = null, object show_message = null, object touch_protection = null)
    {
        methods ??= TOUCH;
        DmList data = source.data as DmList;
        if (Dm.truthy(Dm.bitand(methods, Dm.bitor(TOUCH, VAPOR))) && Dm.num(reac_volume) >= 3 && Dm.truthy(Dm.bitand(blood_type.blood_flags, Dm.bitor(BLOOD_ADD_DNA, BLOOD_COVER_MOBS))))
            Waiting.NoSleep(exposed_mob.add_blood_DNA(Dm.list(Dm.kv(Dm.text("[]", data?[BLOOD_DATA_DNA] is var d && Dm.truthy(d) ? d : blood_type.dna_string), blood_type))));

        // Somehow got a no-data reagent, probably artificially created blood
        if (data == null)
            return;

        if (!Dm.truthy(Dm.bitand(blood_type.blood_flags, BLOOD_TRANSFER_VIRAL_DATA)))
            return;

        foreach (datum_disease strain in Dm.each_cast<datum_disease>(data["viruses"])) // gaps VI1
        {
            if (Dm.truthy(Dm.bitand(strain.spread_flags, DISEASE_SPREAD_SPECIAL | DISEASE_SPREAD_NON_CONTAGIOUS)))
                continue;

            if (Dm.truthy(Dm.bitand(methods, INGEST)))
            {
                if (!Dm.truthy(strain.has_required_infectious_organ(exposed_mob as mob_living_carbon, ORGAN_SLOT_STOMACH)))
                    continue;
                Waiting.NoSleep(exposed_mob.ForceContractDisease(strain));
            }

            else if (Dm.truthy(Dm.bitand(methods, Dm.bitor(INJECT, PATCH))))
            {
                if (!Dm.truthy(strain.has_required_infectious_organ(exposed_mob as mob_living_carbon, ORGAN_SLOT_HEART)))
                    continue;
                Waiting.NoSleep(exposed_mob.ForceContractDisease(strain));
            }

            else if (Dm.truthy(Dm.bitand(methods, Dm.bitor(VAPOR, INHALE))) && Dm.truthy(Dm.bitand(strain.spread_flags, DISEASE_SPREAD_CONTACT_FLUIDS)))
            {
                if (!Dm.truthy(strain.has_required_infectious_organ(exposed_mob as mob_living_carbon, ORGAN_SLOT_LUNGS)))
                    continue;
                Waiting.NoSleep(exposed_mob.ContactContractDisease(strain));
            }

            else if (Dm.truthy(Dm.bitand(methods, TOUCH)) && Dm.truthy(Dm.bitand(strain.spread_flags, DISEASE_SPREAD_CONTACT_FLUIDS)))
                Waiting.NoSleep(exposed_mob.ContactContractDisease(strain));
        }

        /// Have to inject, inhale or ingest it. No curefoam/cheap curesprays
        if (Dm.truthy(data["resistances"]) && Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(INGEST, INJECT), INHALE))))
        {
            foreach (datum_disease infection in Dm.each<datum_disease>(exposed_mob.diseases)) // gaps VI1
            {
                if (!Dm.truthy(infection.bypasses_immunity) && Dm.@in(infection.GetDiseaseID(), data["resistances"]))
                    Waiting.NoSleep(infection.cure(add_resistance: FALSE));
            }
        }
    }

    /// Create or mix in a blood splatter and transfer our diseases to it
    // tg: code/datums/elements/blood_reagent.dm:113
    [SignalHandler]
    public virtual void on_turf_expose(datum_reagent source = null, turf exposed_turf = null, object reac_volume = null)
    {
        if (Dm.num(reac_volume) < 3 || !Dm.truthy(Dm.bitand(blood_type.blood_flags, Dm.bitor(BLOOD_ADD_DNA, BLOOD_COVER_TURFS))))
            return;

        DmList data = source.data as DmList;
        DmList dna_list = Dm.list(Dm.kv(Dm.text("[]", data?[BLOOD_DATA_DNA] is var d && Dm.truthy(d) ? d : blood_type.dna_string), blood_type));
        obj_effect_decal_cleanable_blood splatter = Dm.locate<obj_effect_decal_cleanable_blood>(exposed_turf);
        if (splatter == null)
        {
            if (!Dm.truthy(Dm.bitand(blood_type.blood_flags, BLOOD_COVER_TURFS)))
                return;
            splatter = Dm.@new<obj_effect_decal_cleanable_blood>(exposed_turf, Dm.truthy(Dm.bitand(blood_type.blood_flags, BLOOD_TRANSFER_VIRAL_DATA)) ? data?["viruses"] : null, dna_list);
            Waiting.NoSleep(splatter.adjust_bloodiness(-Dm.num(splatter.bloodiness) + Dm.num(reac_volume) / BLOOD_TO_UNITS_MULTIPLIER));
            return;
        }

        Waiting.NoSleep(splatter.add_blood_DNA(dna_list));
        if (Dm.truthy(Dm.bitand(blood_type.blood_flags, BLOOD_COVER_TURFS)))
            Waiting.NoSleep(splatter.adjust_bloodiness(Dm.num(reac_volume) / BLOOD_TO_UNITS_MULTIPLIER));

        if (!Dm.truthy(Dm.bitand(blood_type.blood_flags, BLOOD_TRANSFER_VIRAL_DATA)) || !Dm.truthy(data?["viruses"]))
            return;

        DmList viruses_to_add = Dm.list();
        foreach (datum_disease virus in Dm.each<datum_disease>(data["viruses"]))
        {
            if (Dm.truthy(Dm.bitand(virus.spread_flags, DISEASE_SPREAD_CONTACT_FLUIDS)))
                viruses_to_add.Add(virus);
        }
        if (Dm.truthy(Dm.length(viruses_to_add)))
            Waiting.NoSleep(splatter.AddComponent(typeof(datum_component_infective), viruses_to_add));
    }

    // tg: code/datums/elements/blood_reagent.dm:142
    [SignalHandler]
    public virtual void on_obj_expose(datum_reagent source = null, obj exposed_obj = null, object reac_volume = null, object methods = null, object show_message = null)
    {
        methods ??= TOUCH;
        if (Dm.num(reac_volume) < 3 || !Dm.truthy(Dm.bitand(methods, Dm.bitor(VAPOR, TOUCH))))
            return;

        DmList data = source.data as DmList;
        if (Dm.truthy(Dm.bitand(blood_type.blood_flags, Dm.bitor(BLOOD_ADD_DNA, BLOOD_COVER_ITEMS))))
            Waiting.NoSleep(exposed_obj.add_blood_DNA(Dm.list(Dm.kv(Dm.text("[]", data?[BLOOD_DATA_DNA] is var d && Dm.truthy(d) ? d : blood_type.dna_string), blood_type))));

        if (!Dm.truthy(Dm.bitand(blood_type.blood_flags, BLOOD_TRANSFER_VIRAL_DATA)) || !Dm.truthy(data?["viruses"]))
            return;

        DmList viruses_to_add = Dm.list();
        foreach (datum_disease virus in Dm.each<datum_disease>(data["viruses"]))
        {
            if (Dm.truthy(Dm.bitand(virus.spread_flags, DISEASE_SPREAD_CONTACT_FLUIDS)))
                viruses_to_add.Add(virus);
        }
        if (Dm.truthy(Dm.length(viruses_to_add)))
            Waiting.NoSleep(exposed_obj.AddComponent(typeof(datum_component_infective), viruses_to_add));
    }

    // tg: code/datums/elements/blood_reagent.dm:161
    [SignalHandler]
    public virtual void on_merge(datum_reagent source = null, DmList mix_data = null, object amount = null)
    {
        DmList data = source.data as DmList;
        // Proportionally mix synth contents
        if (Dm.truthy(data?[BLOOD_DATA_SYNTH_CONTENT]) || Dm.truthy(mix_data?[BLOOD_DATA_SYNTH_CONTENT]))
        {
            float added_synth_volume = Dm.num(amount) * Dm.num(mix_data?[BLOOD_DATA_SYNTH_CONTENT]);
            float existing_synth_volume = Dm.num(source.volume) * Dm.num(data?[BLOOD_DATA_SYNTH_CONTENT]);
            if (data == null)
            {
                data = Dm.list();
                source.data = data;
            }
            // A simple weighted average that simplifies down to "total synth volume / total blood volume" i.e. "how much of the blood is synthetic"
            data[BLOOD_DATA_SYNTH_CONTENT] = (added_synth_volume + existing_synth_volume) / (Dm.num(amount) + Dm.num(source.volume));
        }

        // Presumably artificially generated blood
        if (data == null || mix_data == null)
            return;

        // Mixed blood cannot be used for cloning
        if (!Dm.eq(data[BLOOD_DATA_DNA], mix_data[BLOOD_DATA_DNA]))
            data["cloneable"] = FALSE;

        DmList source_viruses = data["viruses"] as DmList;
        DmList mix_viruses = mix_data["viruses"] as DmList;
        if (Dm.truthy(source_viruses) && Dm.truthy(mix_viruses))
        {
            DmList mix_target = Dm.list(); // gaps VI1
            DmList to_preserve = Dm.list();
            foreach (datum_disease disease in Dm.each_cast<datum_disease>(source_viruses))
            {
                if (disease is datum_disease_advance)
                    Dm.append(mix_target, disease);
                else
                    Dm.append(to_preserve, disease);
            }

            foreach (datum_disease disease in Dm.each_cast<datum_disease>(mix_viruses))
            {
                if (disease is datum_disease_advance)
                    Dm.append(mix_target, disease);
                else
                    Dm.append(to_preserve, disease);
            }

            datum_disease_advance mixed = Waiting.NoSleep(Advance_Mix(mix_target)) as datum_disease_advance;
            if (mixed != null)
                Dm.append(to_preserve, mixed);
            data["viruses"] = to_preserve;
        }
        else if (Dm.truthy(mix_viruses))
            data["viruses"] = mix_viruses.Copy();

        if (Dm.truthy(mix_data["resistances"]))
        {
            if (!Dm.truthy(data["resistances"]))
                data["resistances"] = Dm.list();
            Dm.combine_at(data, "resistances", mix_data["resistances"]);
        }

        // Features are randomly recombinated based on amount mixed in
        DmList source_features = data["features"] as DmList;
        DmList mix_features = mix_data["features"] as DmList;
        if (Dm.truthy(source_features) && Dm.truthy(mix_features))
        {
            foreach (object feature_key in Dm.each(mix_features))
            {
                if (!Dm.truthy(source_features[feature_key]) || prob(Dm.num(amount) / (Dm.num(source.volume) + Dm.num(amount)) * 100))
                    source_features[feature_key] = mix_features[feature_key];
            }
        }

        if (Dm.truthy(mix_data["trace_chem"]))
        {
            if (!Dm.truthy(data["trace_chem"]))
                data["trace_chem"] = mix_data["trace_chem"];
            else
            {
                data["trace_chem"] = list2params((DmList)Dm.bitor(params2list(data["trace_chem"]), params2list(mix_data["trace_chem"])));
            }
        }
    }

    // tg: code/datums/elements/blood_reagent.dm:236
    [SignalHandler]
    public virtual void on_transfer(datum_reagent reagent = null, datum_reagents target_holder = null, datum_reagent new_reagent = null)
    {
        Waiting.NoSleep(new_reagent.AddElement(typeof(datum_element_blood_reagent), null, blood_type));
    }
}
