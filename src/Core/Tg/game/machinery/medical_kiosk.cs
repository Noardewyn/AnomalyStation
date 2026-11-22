// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/medical_kiosk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MD1: the whole file. The medical kiosk (MetaStation has one, in the lobby of medbay) opens tg's MedicalKiosk window: four paid
// scans of the patient (the general one, the symptoms, the brain and radiation, the chemicals and hallucinations), free for the medical
// staff, of the user or of whoever its scanner wand last targeted; an emag (or a hallucinating user) scrambles the readout. Its wand
// comes off with a right click and snaps back on. The var blocks are generated (D111).

// tg: code/game/machinery/medical_kiosk.dm:13
public partial class obj_machinery_medical_kiosk : obj_machinery
{
    // tg: code/game/machinery/medical_kiosk.dm:4-11 (file-local, #undef at :424-427)
    /// Shows if the machine is being used for a general scan.
    private const int KIOSK_SCANNING_GENERAL = 1 << 0;
    /// Shows if the machine is being used for a disease scan.
    private const int KIOSK_SCANNING_SYMPTOMS = 1 << 1;
    /// Shows if the machine is being used for a radiation/brain trauma scan.
    private const int KIOSK_SCANNING_NEURORAD = 1 << 2;
    /// Shows if the machine is being used for a reagent scan.
    private const int KIOSK_SCANNING_REAGENTS = 1 << 3;

    // tg: code/game/machinery/medical_kiosk.dm:39
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // SSeconomy?: a test harness world that makes machines without Master has none (rules 10.6, as the payment component)
        Waiting.NoSleep(AddComponent(typeof(datum_component_payment), get_cost(), SSeconomy?.get_dep_account(ACCOUNT_MED), PAYMENT_FRIENDLY));
        register_context();
        scanner_wand = Dm.@new<obj_item_scanner_wand>(this);
        return dot;
    }

    // tg: code/game/machinery/medical_kiosk.dm:45
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);

        if (held_item == null && scanner_wand != null)
        {
            context[SCREENTIP_CONTEXT_RMB] = "Pick up scanner wand";
            return TRUE;
        }

        if (held_item != null && Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.truthy(anchored) ? "Unsecure" : "Secure";
            return TRUE;
        }
        if (held_item != null && Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR) && Dm.truthy(panel_open))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Deconstruct";
            return TRUE;
        }
        if (held_item != null && Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.truthy(panel_open) ? "Close panel" : "Open panel";
            return TRUE;
        }
        if (held_item is obj_item_scanner_wand)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Return the scanner wand";
            return TRUE;
        }
        return dot;
    }

    //Verifies that the user can use the interface, followed by showing medical information.
    // tg: code/game/machinery/medical_kiosk.dm:66
    public virtual async DmTask inuse()
    {
        var paying = paying_ref?.resolve() as mob_living_carbon_human;
        if (paying == null)
        {
            paying_ref = null;
            return;
        }

        var card = paying.get_idcard(TRUE) as obj_item_card_id;
        if (card?.registered_account?.account_job != null && Dm.eq(card.registered_account.account_job.paycheck_department, payment_department))
        {
            await use_energy(active_power_usage);
            paying_customer = TRUE;
            await say(I18n.T("Hello, esteemed medical staff!"));
            return;
        }
        float bonus_fee = Dm.truthy(pandemonium) ? rand(10, 30) : 0;
        if (Dm.truthy(Dm.bitand(attempt_charge(this, paying, bonus_fee), COMPONENT_OBJ_CANCEL_CHARGE)))
            return;
        await use_energy(active_power_usage);
        paying_customer = TRUE;
        icon_state = Dm.text("[]_active", base_icon_state);
        await say(I18n.T("Thank you for your patronage!"));
    }

    //Called it enough times to be it's own proc
    // tg: code/game/machinery/medical_kiosk.dm:87
    public virtual void clearScans()
    {
        scan_active = NONE;
        update_appearance();
    }

    // tg: code/game/machinery/medical_kiosk.dm:92
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(panel_open))
        {
            icon_state = Dm.text("[]_open", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        if (!Dm.truthy(is_operational))
        {
            icon_state = Dm.text("[]_off", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        icon_state = Dm.text("[][]", base_icon_state, scan_active != 0 ? "_active" : null);
        return await base.update_icon_state(__extra);
    }

    //Allows for wrenching/unwrenching the machine.
    // tg: code/game/machinery/medical_kiosk.dm:102
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool, time: 0.1f * SECONDS);
        return ITEM_INTERACT_SUCCESS;
    }

    ///Returns the active cost of the board
    // tg: code/game/machinery/medical_kiosk.dm:108
    private float get_cost()
    {
        var board = circuit_value as obj_item_circuitboard_machine_medical_kiosk; // the board instance (GAP(types): circuit is a Type, _machinery.cs)
        return board.custom_cost;
    }

    // tg: code/game/machinery/medical_kiosk.dm:115
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/medical_kiosk.dm:118
    public override DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
        => default_deconstruction_crowbar(user, tool);

    // tg: code/game/machinery/medical_kiosk.dm:121
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_scanner_wand wand)
            return NONE;

        if (scanner_wand != null)
        {
            balloon_alert(user, I18n.T("already has a wand!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
        {
            balloon_alert(user, I18n.T("stuck to your hand!"));
            return ITEM_INTERACT_BLOCKING;
        }
        user.visible_message(span_notice(I18n.T("[] snaps [] onto []!", user, tool, this)));
        balloon_alert(user, I18n.T("wand returned"));
        //This will be the scanner returning scanner_wand's selected_target variable and assigning it to the altPatient var
        if (Dm.truthy(wand.selected_target))
        {
            var target_ref = WEAKREF(wand.return_patient() as datum) as datum_weakref;
            if (!ReferenceEquals(patient_ref, target_ref))
                clearScans();
            patient_ref = target_ref;
            user.visible_message(span_notice(I18n.T("[] has been set as the current patient.", wand.return_patient())));
            wand.selected_target = null;
        }
        playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        scanner_wand = tool;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/medical_kiosk.dm:146
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        if (!ishuman(user) || !Dm.truthy(await user.can_perform_action(this)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        if (scanner_wand == null)
        {
            balloon_alert(user, I18n.T("no scanner wand!"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        if (!Dm.truthy(await user.put_in_hands(scanner_wand)))
        {
            balloon_alert(user, I18n.T("scanner wand falls!"));
            scanner_wand = null;
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        user.visible_message(span_notice(I18n.T("[] unhooks the [] from [].", user, scanner_wand, this)));
        balloon_alert(user, I18n.T("scanner pulled"));
        playsound(this, Dm.resource("sound/machines/click.ogg"), 60, TRUE);
        scanner_wand = null;
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/machinery/medical_kiosk.dm:165
    public override object Destroy(float? force = null)
    {
        qdel(scanner_wand);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/medical_kiosk.dm:169
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        object dot = await base.emag_act(user, emag_card, __extra);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return dot;
        if (user != null)
        {
            if (emag_card != null)
                user.visible_message(span_warning(I18n.T("[] waves a suspicious card by the []'s biometric scanner!", user, this)));
            balloon_alert(user, I18n.T("sensors overloaded"));
        }
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        var board = circuit_value;
        board.obj_flags = Dm.bitor(board.obj_flags, EMAGGED); //Mirrors emag status onto the board as well.
        pandemonium = TRUE;
        return TRUE;
    }

    // tg: code/game/machinery/medical_kiosk.dm:183
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (scanner_wand == null)
            Dm.append(ref dot, span_notice(I18n.T("\\The [] is missing its scanner.", this)));
        else
            Dm.append(ref dot, span_notice(I18n.T("\\The [] has its scanner clipped to the side. Right Click to remove.", this)));
        return dot;
    }

    // tg: code/game/machinery/medical_kiosk.dm:190
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (!ishuman(user))
        {
            to_chat(user, span_warning(I18n.T("[] is unable to interface with non-humanoids!", this)));
            if (ui != null)
                ui.close();
            return null;
        }
        var patient = patient_ref?.resolve() as mob_living_carbon_human;
        float patient_distance = Dm.num(get_dist(loc, patient));
        if (patient == null)
        {
            await say(I18n.T("Scanner reset."));
            patient_ref = WEAKREF(user) as datum_weakref;
        }
        else if (patient_distance > 5)
        {
            patient_ref = null;
            await say(I18n.T("Patient out of range. Resetting biometrics."));
            clearScans();
            return null;
        }
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "MedicalKiosk", name);
            await ui.open();
            icon_state = Dm.text("[]_active", base_icon_state);
            var paying = (mob_living_carbon_human)user;
            paying_ref = WEAKREF(paying) as datum_weakref;
        }
        return null;
    }

    // tg: code/game/machinery/medical_kiosk.dm:215
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        var patient = patient_ref?.resolve() as mob_living_carbon_human;
        DmList data = Dm.list();
        if (patient == null)
            return null;
        object patient_name = patient.name;
        string patient_status = I18n.T("Alive.");
        float max_health = Dm.num(patient.maxHealth);
        float total_health = Dm.num(patient.health);
        float brute_loss = Dm.num(patient.get_brute_loss());
        float fire_loss = Dm.num(patient.get_fire_loss());
        float tox_loss = Dm.num(patient.get_tox_loss());
        float oxy_loss = Dm.num(patient.get_oxy_loss());
        float chaos_modifier = 0;

        string sickness = I18n.T("Patient does not show signs of disease.");
        string sickness_data = I18n.T("Not Applicable.");

        string bleed_status = I18n.T("Patient is not currently bleeding.");
        string blood_status = I18n.T(" Patient either has no blood, or does not require it to function.");
        float blood_percent = Dm.round(Dm.num(patient.get_blood_volume(apply_modifiers: TRUE)) / BLOOD_VOLUME_NORMAL * 100);
        var blood_type = patient.get_bloodtype() as datum_blood_type;
        string blood_name = "error";
        string blood_warning = " ";
        object blood_alcohol = patient.get_blood_alcohol_content();

        foreach (datum_disease disease in Dm.each<datum_disease>(patient.diseases)) //Disease Information
        {
            if (!Dm.truthy(Dm.bitand(disease.visibility_flags, HIDDEN_SCANNER)))
            {
                sickness = I18n.T("Warning: Patient is harboring some form of viral disease. Seek further medical attention.");
                object cure_text = disease.cure_text;
                if (disease is datum_disease_advance advanced_disease)
                    cure_text = advanced_disease.generate_cure_text(2);
                sickness_data = I18n.T("\nName: [].\nType: [].\nStage: []/[].\nCure: []", disease.name, disease.spread_text, disease.stage, disease.max_stages, cure_text);
            }
        }

        if (Dm.truthy(patient.can_bleed())) //Blood levels Information
        {
            blood_name = LOWER_TEXT(blood_type.get_blood_name());
            if (Dm.truthy(patient.is_bleeding()))
                bleed_status = I18n.T(" Patient is currently bleeding!");

            if (blood_percent <= 80)
            {
                blood_warning = I18n.T(" Patient has [] [] levels.", blood_percent <= 60 ? I18n.T("DANGEROUSLY low") : I18n.T("low"), blood_name);
                DmList treatments = Dm.list();
                if (blood_percent <= 60)
                    Dm.append(ref treatments, I18n.T("[] transfusion", blood_name));
                else if (!HAS_TRAIT(patient, TRAIT_NOHUNGER))
                    Dm.append(ref treatments, I18n.T("a large meal"));
                if (blood_type.restoration_chem != null)
                {
                    Dm.append(ref treatments, I18n.T("[] supplements", LOWER_TEXT(Dm.initial(blood_type.restoration_chem, (datum_reagent p) => p.name))));
                    if (blood_percent <= 60 && blood_type.restoration_chem == typeof(datum_reagent_iron))
                        Dm.append(ref treatments, I18n.T("saline-glucose immediately"));
                }

                if (Dm.length(treatments) > 0)
                    blood_warning += I18n.T(" Seek []", english_list(treatments, and_text: I18n.T(" or ")));

                if (blood_percent <= 60)
                    blood_warning += I18n.T(" Ignoring treatment may lead to death!");
            }

            object type_of_blood = blood_type.get_type();
            blood_status = I18n.T("Patient [] levels are currently reading []%.[][]", blood_name, blood_percent,
                Dm.truthy(type_of_blood) ? I18n.T(" Patient has [] type [].", type_of_blood, blood_name) : "", blood_warning);
        }

        string trauma_status = I18n.T("Patient is free of unique brain trauma.");
        float brain_loss = Dm.num(patient.get_organ_loss(ORGAN_SLOT_BRAIN));
        string brain_status = I18n.T("Brain patterns normal.");
        if (Dm.truthy(LAZYLEN(patient.get_traumas() as DmList)))
        {
            DmList trauma_text = Dm.list();
            foreach (datum_brain_trauma trauma in Dm.each<datum_brain_trauma>(patient.get_traumas()))
            {
                string trauma_desc = "";
                switch (Dm.num(trauma.resilience))
                {
                    case TRAUMA_RESILIENCE_SURGERY:
                        trauma_desc += I18n.T("severe ");
                        break;
                    case TRAUMA_RESILIENCE_LOBOTOMY:
                        trauma_desc += I18n.T("deep-rooted ");
                        break;
                    case TRAUMA_RESILIENCE_MAGIC:
                    case TRAUMA_RESILIENCE_ABSOLUTE:
                        trauma_desc += I18n.T("permanent ");
                        break;
                }
                trauma_desc += Dm.str(trauma.scan_desc);
                Dm.append(ref trauma_text, trauma_desc);
            }
            trauma_status = I18n.T("Cerebral traumas detected: patient appears to be suffering from [].", english_list(trauma_text));
        }

        DmList chemical_list = Dm.list();
        DmList overdose_list = Dm.list();
        DmList addict_list = Dm.list();
        string hallucination_status = I18n.T("Patient is not hallucinating.");

        var reagents_of_patient = (datum_reagents)patient.reagents;
        if (Dm.length(reagents_of_patient.reagent_list) > 0) //Chemical Analysis details.
        {
            foreach (datum_reagent reagent in Dm.each<datum_reagent>(reagents_of_patient.reagent_list))
            {
                if (Dm.truthy(Dm.bitand(reagent.chemical_flags, REAGENT_INVISIBLE))) //Don't show hidden chems
                    continue;
                Dm.append(ref chemical_list, Dm.list(Dm.list(Dm.kv("name", reagent.name), Dm.kv("volume", Dm.round(Dm.num(reagent.volume), 0.01f)))));
                if (Dm.truthy(reagent.overdosed))
                    Dm.append(ref overdose_list, Dm.list(Dm.list(Dm.kv("name", reagent.name))));
            }
        }
        var belly = patient.get_organ_slot(ORGAN_SLOT_STOMACH) as obj_item_organ_stomach;
        var belly_reagents = belly?.reagents as datum_reagents;
        if (belly_reagents != null && Dm.length(belly_reagents.reagent_list) > 0) //include the stomach contents if it exists
        {
            foreach (datum_reagent bit in Dm.each<datum_reagent>(belly_reagents.reagent_list))
            {
                if (Dm.truthy(Dm.bitand(bit.chemical_flags, REAGENT_INVISIBLE))) //Don't show hidden chems
                    continue;
                if (!Dm.truthy(belly.food_reagents?[bit.GetType()]))
                    Dm.append(ref chemical_list, Dm.list(Dm.list(Dm.kv("name", bit.name), Dm.kv("volume", Dm.round(Dm.num(bit.volume), 0.01f)))));
                else
                {
                    float bit_vol = Dm.num(bit.volume) - Dm.num(belly.food_reagents[bit.GetType()]);
                    if (bit_vol > 0)
                        Dm.append(ref chemical_list, Dm.list(Dm.list(Dm.kv("name", bit.name), Dm.kv("volume", Dm.round(bit_vol, 0.01f)))));
                }
            }
        }
        foreach (datum_addiction addiction_type in Dm.each<datum_addiction>(patient.mind?.active_addictions))
            Dm.append(ref addict_list, Dm.list(Dm.list(Dm.kv("name", Dm.initial(addiction_type, (datum_addiction p) => p.name)))));

        if (Dm.truthy(patient.has_status_effect(typeof(datum_status_effect_hallucination))))
        {
            hallucination_status = I18n.T("Subject appears to be hallucinating. Suggested treatments: Antipsychotic medication, [] or [].",
                Dm.initial(typeof(datum_reagent_medicine_haloperidol), (datum_reagent p) => p.name),
                Dm.initial(typeof(datum_reagent_medicine_synaptizine), (datum_reagent p) => p.name));
        }

        if (IS_DEAD_OR_FAKING(patient)) //Patient status checks.
            patient_status = I18n.T("Dead.");
        if (brute_loss + fire_loss + tox_loss + oxy_loss >= 80)
            patient_status = I18n.T("Gravely Injured");
        else if (brute_loss + fire_loss + tox_loss + oxy_loss >= 40)
            patient_status = I18n.T("Injured");
        else if (brute_loss + fire_loss + tox_loss + oxy_loss >= 20)
            patient_status = I18n.T("Lightly Injured");
        bool user_hallucinating = user is mob_living living_user && Dm.truthy(living_user.has_status_effect(typeof(datum_status_effect_hallucination)));
        if (Dm.truthy(pandemonium) || user_hallucinating)
        {
            patient_status = Dm.str(pick(
                I18n.T("The only kiosk is kiosk, but is the only patient, patient?"),
                I18n.T("Breathing manually."),
                I18n.T("Contact NTOS site admin."),
                I18n.T("97% carbon, 3% natural flavoring"),
                I18n.T("The ebb and flow wears us all in time."),
                I18n.T("It's Lupus. You have Lupus."),
                I18n.T("Undergoing monkey disease.")));
        }

        if (brain_loss >= 100) //Brain status checks.
            brain_status = I18n.T("Grave brain damage detected.");
        else if (brain_loss >= 50)
            brain_status = I18n.T("Severe brain damage detected.");
        else if (brain_loss >= 20)
            brain_status = I18n.T("Brain damage detected.");
        else if (brain_loss >= 1)
            brain_status = I18n.T("Mild brain damage detected."); //You may have a miiiild case of severe brain damage.

        if (Dm.truthy(pandemonium))
            chaos_modifier = 1;
        else if (user_hallucinating)
            chaos_modifier = 0.3f;

        data["kiosk_cost"] = get_cost() + chaos_modifier * rand(1, 25);
        data["patient_name"] = patient_name;
        data["patient_health"] = Dm.round((total_health - chaos_modifier * rand(1, 50)) / max_health * 100, 0.001f);
        data["brute_health"] = Dm.round(brute_loss + chaos_modifier * rand(1, 30), 0.001f); //To break this down for easy reading, all health values are rounded to the .001 place
        data["burn_health"] = Dm.round(fire_loss + chaos_modifier * rand(1, 30), 0.001f); //then a random number is added, which is multiplied by chaos modifier.
        data["toxin_health"] = Dm.round(tox_loss + chaos_modifier * rand(1, 30), 0.001f); //That allows for a weaker version of the affect to be applied while hallucinating as opposed to emagged.
        data["suffocation_health"] = Dm.round(oxy_loss + chaos_modifier * rand(1, 30), 0.001f); //It's not the cleanest but it does make for a colorful window.
        data["brain_health"] = brain_status;
        data["brain_damage"] = brain_loss + chaos_modifier * rand(1, 30);
        data["patient_status"] = patient_status;
        data["trauma_status"] = trauma_status;
        data["patient_illness"] = sickness;
        data["illness_info"] = sickness_data;
        data["bleed_status"] = bleed_status;
        data["blood_name"] = capitalize(blood_name);
        data["blood_levels"] = blood_percent - chaos_modifier * rand(1, 35);
        data["blood_status"] = blood_status;
        data["blood_alcohol"] = blood_alcohol;
        data["chemical_list"] = chemical_list;
        data["overdose_list"] = overdose_list;
        data["addict_list"] = addict_list;
        data["hallucinating_status"] = hallucination_status;

        data["active_status_1"] = Dm.bitand(scan_active, KIOSK_SCANNING_GENERAL); // General Scan Check
        data["active_status_2"] = Dm.bitand(scan_active, KIOSK_SCANNING_SYMPTOMS); // Symptom Scan Check
        data["active_status_3"] = Dm.bitand(scan_active, KIOSK_SCANNING_NEURORAD); // Radio-Neuro Scan Check
        data["active_status_4"] = Dm.bitand(scan_active, KIOSK_SCANNING_REAGENTS); // Reagents/hallucination Scan Check
        return data;
    }

    // tg: code/game/machinery/medical_kiosk.dm:388
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (Dm.str(action))
        {
            case "beginScan_1":
                if (!Dm.truthy(Dm.bitand(scan_active, KIOSK_SCANNING_GENERAL)))
                    await inuse();
                if (paying_customer == TRUE)
                {
                    scan_active = Dm.bitor(scan_active, KIOSK_SCANNING_GENERAL);
                    paying_customer = FALSE;
                }
                break;
            case "beginScan_2":
                if (!Dm.truthy(Dm.bitand(scan_active, KIOSK_SCANNING_SYMPTOMS)))
                    await inuse();
                if (paying_customer == TRUE)
                {
                    scan_active = Dm.bitor(scan_active, KIOSK_SCANNING_SYMPTOMS);
                    paying_customer = FALSE;
                }
                break;
            case "beginScan_3":
                if (!Dm.truthy(Dm.bitand(scan_active, KIOSK_SCANNING_NEURORAD)))
                    await inuse();
                if (paying_customer == TRUE)
                {
                    scan_active = Dm.bitor(scan_active, KIOSK_SCANNING_NEURORAD);
                    paying_customer = FALSE;
                }
                break;
            case "beginScan_4":
                if (!Dm.truthy(Dm.bitand(scan_active, KIOSK_SCANNING_REAGENTS)))
                    await inuse();
                if (paying_customer == TRUE)
                {
                    scan_active = Dm.bitor(scan_active, KIOSK_SCANNING_REAGENTS);
                    paying_customer = FALSE;
                }
                break;
            case "clearTarget":
                patient_ref = null;
                clearScans();
                dot = TRUE;
                break;
        }
        return dot;
    }
}
