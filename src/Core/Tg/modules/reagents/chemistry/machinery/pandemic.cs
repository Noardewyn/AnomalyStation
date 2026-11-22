// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/machinery/pandemic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341): the PanD.E.M.I.C 2200 with its Pandemic window. It reads the viruses and the resistances of
// the blood in its beaker, prints culture tubes of the archived advance diseases and vaccine tubes, renames an unnamed advance
// disease, and takes droppers and syringes to the loaded sample. The var block is generated (D111).

// tg: code/modules/reagents/chemistry/machinery/pandemic.dm:2
public partial class obj_machinery_computer_pandemic : obj_machinery_computer
{
    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();


        DmList hovering_item_typechecks = TgState.Current.obj_machinery_computer_pandemic__Initialize__hovering_item_typechecks;

        Waiting.NoSleep(AddElement(typeof(datum_element_contextual_screentip_item_typechecks), hovering_item_typechecks));

        Waiting.NoSleep(AddElement(
            typeof(datum_element_contextual_screentip_bare_hands),
            Dm.named("lmb_text", I18n.T("Open interface")),
            Dm.named("rmb_text", I18n.T("Remove beaker"))
        ));
        return dot;
    }


    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:47
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref beaker);
        return base.Destroy(force);
    }

    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:51
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (beaker != null)
        {
            bool is_close = false;
            if (Dm.truthy(await Adjacent(user))) //don't reveal exactly what's inside unless they're close enough to see the UI anyway.
            {
                Dm.append(dot, I18n.T("It contains \\a [].", beaker));
                is_close = true;
            }
            else
                Dm.append(dot, I18n.T("It has a beaker inside it."));
            Dm.append(dot, span_info(I18n.T("Alt-click to eject [].", is_close ? (object)beaker : I18n.T("the beaker"))));
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:62
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;
        if (!Dm.truthy(await can_interact(user)) || !Dm.truthy(await user.can_perform_action(this, Dm.bitor(ALLOW_SILICON_REACH, FORBID_TELEKINESIS_REACH))))
            return dot;
        await eject_beaker();
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:71
    public override async DmTask<object> attack_robot_secondary(mob user = null, DmList modifiers = null)
    {
        return await attack_hand_secondary(user, modifiers);
    }

    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:74
    public override async DmTask<object> attack_ai_secondary(mob user = null, DmList modifiers = null)
    {
        return await attack_hand_secondary(user, modifiers);
    }

    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:77
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == beaker)
        {
            beaker = null;
            update_appearance();
            SStgui.update_uis(this);
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:84
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        //Advanced science! Precision instruments (eg droppers and syringes) are precise enough to modify the loaded sample!
        if (tool is obj_item_reagent_containers_dropper || tool is obj_item_reagent_containers_syringe)
        {
            if (beaker == null)
            {
                balloon_alert(user, "no beaker!");
                return ITEM_INTERACT_BLOCKING;
            }
            if (tool is obj_item_reagent_containers_syringe && Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
                await tool.interact_with_atom_secondary(beaker, user);
            else
                await tool.interact_with_atom(beaker, user);
            SStgui.update_uis(this);
            return ITEM_INTERACT_SUCCESS;
        }

        if (!is_reagent_container(tool) || Dm.truthy(Dm.bitand(tool.item_flags, ABSTRACT)) || !Dm.truthy(tool.is_open_container()))
            return NONE;
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN)))
            return ITEM_INTERACT_BLOCKING;
        if (beaker != null)
        {
            balloon_alert(user, "beaker swapped");
            await try_put_in_hand(beaker, usr as mob_living);
        }
        else
            balloon_alert(user, "beaker loaded");
        await user.transferItemToLoc(tool, this);
        beaker = (obj_item_reagent_containers)tool;
        update_appearance();
        SStgui.update_uis(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:112
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        await eject_beaker();
        object dot = await base.on_deconstruction(disassembled);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:116
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][][]", base_icon_state, beaker != null ? 1 : 0, Dm.truthy(Dm.bitand(machine_stat, BROKEN)) ? "_b" : (Dm.truthy(powered__proc()) ? null : "_nopower"));
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:120
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(wait))
            Dm.append(ref dot, "waitlight");
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:125
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Pandemic", name);
            await ui.open();
            ui.set_autoupdate(FALSE);
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:133
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        data["is_ready"] = Dm.num(!Dm.truthy(wait));
        if (beaker == null)
        {
            data["has_beaker"] = FALSE;
            data["has_blood"] = FALSE;
            return data;
        }
        data["has_beaker"] = TRUE;
        object volume = round(Dm.num((beaker.reagents as datum_reagents)?.total_volume), 0.01f);
        data["beaker"] = Dm.list(
            Dm.kv("volume", Dm.truthy(volume) ? volume : 0),
            Dm.kv("capacity", beaker.volume)
        );
        datum_reagent blood = get_blood_reagent() as datum_reagent;
        if (blood == null)
        {
            data["has_blood"] = FALSE;
            return data;
        }
        data["has_blood"] = TRUE;
        data["blood"] = Dm.list();
        object blood_dna = ((DmList)blood.data)[BLOOD_DATA_DNA];
        ((DmList)data["blood"])["dna"] = Dm.truthy(blood_dna) ? blood_dna : "none";
        object blood_type = ((DmList)blood.data)[BLOOD_DATA_TYPE];
        ((DmList)data["blood"])["type"] = Dm.truthy(blood_type) ? blood_type : "none";
        data["viruses"] = get_viruses_data(blood);
        data["resistances"] = get_resistance_data(blood as datum_reagent_blood);
        return data;
    }

    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:157
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "eject_beaker":
                await eject_beaker();
                return TRUE;
            case "empty_beaker":
                if (beaker != null)
                    await ((datum_reagents)beaker.reagents).clear_reagents();
                return TRUE;
            case "empty_eject_beaker":
                if (beaker != null)
                {
                    await ((datum_reagents)beaker.reagents).clear_reagents();
                    await eject_beaker();
                }
                return TRUE;
            case "rename_disease":
                await rename_disease(@params["index"], @params["name"]);
                return TRUE;
            case "create_culture_bottle":
                if (Dm.truthy(wait))
                    return FALSE;
                await create_culture_bottle(@params["index"]);
                return TRUE;
            case "create_vaccine_bottle":
                if (Dm.truthy(wait))
                    return FALSE;
                await create_vaccine_bottle(@params["index"]);
                return TRUE;
        }
        return FALSE;
    }

    /**
     * Creates a culture bottle (ie: replicates) of the the specified disease.
     *
     * @param {number} index - The index of the disease to replicate.
     *
     * @returns {boolean} - Success or failure.
     */
    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:196
    public virtual async DmTask<object> create_culture_bottle(object index = null)
    {
        object id = get_virus_id_by_index(text2num(index));
        datum_disease_advance adv_disease = SSdisease.archive_diseases[id] as datum_disease_advance;

        if (!(adv_disease is datum_disease_advance) || !Dm.truthy(adv_disease.mutable))
        {
            to_chat(usr, span_warning(I18n.T("ERROR: Cannot replicate virus strain.")));
            return FALSE;
        }
        await use_energy(active_power_usage);
        adv_disease = (datum_disease_advance)await adv_disease.Copy();
        DmList data = Dm.list(Dm.kv("viruses", Dm.list(adv_disease)));

        obj_item_reagent_containers_cup_tube bottle = Dm.@new<obj_item_reagent_containers_cup_tube>(drop_location());
        bottle.name = I18n.T("[] culture tube", adv_disease.name);
        bottle.desc = I18n.T("A small test tube containing [] culture in synthblood medium.", adv_disease.agent);
        await ((datum_reagents)bottle.reagents).add_reagent(typeof(datum_reagent_blood), 20, data);
        wait = TRUE;
        update_appearance();
        turf source_turf = get_turf(this);
        log_virus(Dm.text("A culture tube was printed for the virus [] at [] by []", adv_disease.admin_details(), loc_name(source_turf), key_name(usr)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(reset_replicator_cooldown)), 5 * SECONDS, file: "code/modules/reagents/chemistry/machinery/pandemic.dm", line: 215);
        return TRUE;
    }

    /// Tries to locate a reagent with valid blood_type data
    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:219
    public virtual object get_blood_reagent()
    {
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>((beaker?.reagents as datum_reagents)?.reagent_list))
        {
            if (Dm.truthy((reagent.data as DmList)?[BLOOD_DATA_TYPE]))
                return reagent;
        }
        return null;
    }

    /**
     * Creates a vaccine bottle for the specified disease.
     *
     * @param {number} index - The index of the disease to replicate.
     *
     * @returns {boolean} - Success or failure.
     */
    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:231
    public virtual async DmTask<object> create_vaccine_bottle(object index = null)
    {
        await use_energy(active_power_usage);
        object id = index;
        datum_disease disease = SSdisease.archive_diseases[id] as datum_disease;
        obj_item_reagent_containers_cup_tube bottle = Dm.@new<obj_item_reagent_containers_cup_tube>(drop_location());
        bottle.name = I18n.T("[] vaccine tube", disease.name);
        await ((datum_reagents)bottle.reagents).add_reagent(typeof(datum_reagent_vaccine), 15, Dm.list(id));
        wait = TRUE;
        update_appearance();
        _addtimer(Dm.@new<datum_callback>(this, nameof(reset_replicator_cooldown)), 20 * SECONDS, file: "code/modules/reagents/chemistry/machinery/pandemic.dm", line: 240);
        return TRUE;
    }

    /**
     * Supporting proc to eject a beaker from the machine.
     *
     * Places it in hand if possible.
     *
     * @returns {boolean} - Success or failure.
     */
    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:250
    public virtual async DmTask<object> eject_beaker()
    {
        if (beaker == null)
            return FALSE;
        await try_put_in_hand(beaker, usr as mob_living);
        beaker = null;
        update_appearance();
        return TRUE;
    }

    /**
     * Displays a thing if it exists within the contents of a beaker.
     *
     * @param {any} thing - The key to look for.
     *
     * @param {any} index - Nested objects within the thing.
     *
     * @returns {any | boolean} The thing found or FALSE if unsuccessful.
     */
    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:267
    public virtual object get_by_index(object thing = null, object index = null)
    {
        if (beaker == null || beaker.reagents == null)
            return FALSE;
        datum_reagent_blood blood = Dm.locate<datum_reagent_blood>(((datum_reagents)beaker.reagents).reagent_list);
        if (Dm.truthy((blood?.data as DmList)?[thing]))
            return ((DmList)((DmList)blood.data)[thing])[index];
        return FALSE;
    }

    /**
     * Gets resistances of a given blood sample as a list
     *
     * @param {reagent/blood} blood - The sample.
     *
     * @returns {list} - The resistances.
     */
    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:282
    public virtual object get_resistance_data(datum_reagent_blood blood = null)
    {
        DmList data = Dm.list();
        if (!islist(((DmList)blood.data)["resistances"]))
            return data;
        DmList resistances = (DmList)((DmList)blood.data)["resistances"];
        foreach (object id in Dm.each(resistances))
        {
            DmList resistance = Dm.list();
            datum_disease disease = SSdisease.archive_diseases[id] as datum_disease;
            if (disease != null)
            {
                resistance["id"] = id;
                resistance["name"] = disease.name;
            }
            Dm.append(data, Dm.list(resistance));
        }
        return data;
    }

    /**
     * A very hefty proc that I am not proud to see.
     *
     * Given a blood sample, this proc will return a list of viruses that are present in the sample.
     *
     * Contains traits, symptoms, thresholds etc.
     *
     * @param {reagent/blood} blood - The sample to analyze.
     *
     * @returns {list} - A list of virus info present in the sample.
     */
    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:307
    public virtual object get_viruses_data(datum_reagent blood = null)
    {
        DmList data = Dm.list();
        DmList viruses = (blood.data as DmList)?["viruses"] as DmList;
        float index = 1;
        foreach (object disease__any in Dm.each(viruses))
        {
            datum_disease disease = disease__any as datum_disease;
            if (disease == null || Dm.truthy(Dm.bitand(disease.visibility_flags, HIDDEN_PANDEMIC)))
                continue;
            DmList traits = Dm.list();
            traits["agent"] = disease.agent;
            traits["cure"] = Dm.truthy(disease.cure_text) ? disease.cure_text : "none";
            traits["description"] = Dm.truthy(disease.desc) ? disease.desc : "none";
            traits["index"] = index++;
            traits["name"] = disease.name;
            traits["spread"] = Dm.truthy(disease.spread_text) ? disease.spread_text : "none";
            if (disease is datum_disease_advance) // Advanced diseases get more info
            {
                datum_disease_advance adv_disease = (datum_disease_advance)disease;
                object disease_name = SSdisease.get_disease_name(adv_disease.GetDiseaseID());
                traits["can_rename"] = Dm.num((Dm.eq(disease_name, "Unknown")) && Dm.truthy(adv_disease.mutable));
                traits["is_adv"] = TRUE;
                traits["name"] = disease_name;
                traits["resistance"] = adv_disease.totalResistance();
                traits["stage_speed"] = adv_disease.totalStageSpeed();
                traits["stealth"] = adv_disease.totalStealth();
                traits["severity"] = adv_disease.totalSeverity();
                traits["symptoms"] = Dm.list();
                foreach (datum_symptom symptom in Dm.each_cast<datum_symptom>(adv_disease.symptoms))
                {
                    DmList this_symptom = Dm.list();
                    this_symptom = symptom.get_symptom_data() as DmList;
                    Dm.append((DmList)traits["symptoms"], Dm.list(this_symptom));
                }
                traits["transmission"] = adv_disease.totalTransmittable();
            }
            Dm.append(data, Dm.list(traits));
        }
        return data;
    }

    /**
     * Gets the ID of the virus by its index in the list of viruses.
     *
     * @param {number} index - The index of the virus in the list of viruses.
     *
     * @returns {string | boolean} - The ID of the virus or FALSE if unable
     * to find the virus.
     */
    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:348
    public virtual object get_virus_id_by_index(object index = null)
    {
        datum_disease disease = get_by_index("viruses", index) as datum_disease;
        if (disease == null)
            return FALSE;
        return disease.GetDiseaseID();
    }

    /**
     * Renames an advanced disease after running it through sanitize_name().
     *
     * @param {string} id - The ID of the disease to rename.
     *
     * @param {string} name - The new name of the disease.
     *
     * @returns {boolean} - Success or failure.
     */
    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:363
    public virtual async DmTask<object> rename_disease(object index = null, object name = null)
    {
        object id = get_virus_id_by_index(text2num(index));
        datum_disease_advance adv_disease = SSdisease.archive_diseases[id] as datum_disease_advance;
        if (!Dm.truthy(adv_disease.mutable))
            return FALSE;
        if (adv_disease != null)
        {
            object new_name = await sanitize_name(name, allow_numbers: TRUE, cap_after_symbols: FALSE);
            if (!Dm.truthy(new_name))
                return FALSE;
            adv_disease.AssignName(Dm.str(new_name));
            return TRUE;
        }
        return FALSE;
    }

    /**
     * Allows a user to create another vaccine/culture bottle again.
     *
     * @returns {boolean} - Success or failure.
     */
    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:381
    public virtual object reset_replicator_cooldown()
    {
        wait = FALSE;
        SStgui.update_uis(this);
        update_appearance();
        playsound(this, Dm.resource("sound/machines/ping.ogg"), 30, TRUE);
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/reagents/chemistry/machinery/pandemic.dm:26  /obj/machinery/computer/pandemic/Initialize's var/static/list/hovering_item_typechecks
    internal DmList obj_machinery_computer_pandemic__Initialize__hovering_item_typechecks = Dm.list(
        Dm.kv(typeof(obj_item_reagent_containers_dropper), Dm.list(
            Dm.kv(SCREENTIP_CONTEXT_LMB, "Use dropper")
        )),

        Dm.kv(typeof(obj_item_reagent_containers_syringe), Dm.list(
            Dm.kv(SCREENTIP_CONTEXT_LMB, "Inject sample"),
            Dm.kv(SCREENTIP_CONTEXT_RMB, "Draw sample")
        ))
    );
}
