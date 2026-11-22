// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/equipment/tools/medical_tools.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Sleeper, Medical Beam, and Syringe gun

// tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:3
public partial class obj_item_mecha_parts_mecha_equipment_medical : obj_item_mecha_parts_mecha_equipment
{
    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:6
    public override async DmTask attach(obj_vehicle_sealed_mecha new_mecha = null, object attach_right = null)
    {
        await base.attach(new_mecha, attach_right);
        START_PROCESSING(SSobj, this);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:14
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (chassis == null)
            return PROCESS_KILL;
        return null;
    }
}

// tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:18
public partial class obj_item_mecha_parts_mecha_equipment : obj_item_mecha_parts
{
    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:18
    public virtual DmList get_reagent_data(DmList reagent_list = null)
    {
        DmList contained_reagents = Dm.list();
        if (Dm.truthy(length(reagent_list)))
        {
            foreach (datum_reagent reagent in Dm.each<datum_reagent>(reagent_list))
                contained_reagents.Add(Dm.list(Dm.kv("name", reagent.name), Dm.kv("volume", round(Dm.num(reagent.volume), 0.01f)))); // list in a list because Byond merges the first list...
        }
        return contained_reagents;
    }
}

//---- Mecha sleeper, medical subtype has the chemical functionality
// tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:26
public partial class obj_item_mecha_parts_mecha_equipment_sleeper : obj_item_mecha_parts_mecha_equipment
{
    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:37
    public override object Destroy(float? force = null)
    {
        foreach (atom_movable content in Dm.each<atom_movable>(contents.Copy()))
            Waiting.Detach(content.forceMove(get_turf(this)));
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:42
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        await go_out();
        return null;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:45
    public override async DmTask<object> action(mob source = null, atom atomtarget = null, DmList modifiers = null)
    {
        if (!Dm.truthy(action_checks(atomtarget)))
            return null;
        if (!iscarbon(atomtarget))
            return null;
        mob_living_carbon target = atomtarget as mob_living_carbon;
        if (!patient_insertion_check(target, source))
            return null;
        to_chat(source, span_notice(I18n.T("You start putting [] into []...", target, this)));
        chassis.visible_message(span_warning(I18n.T("[] starts putting [] into the [].", chassis, target, this)));
        if (!Dm.truthy(await do_after(source, get_equip_cooldown(atomtarget), target, extra_checks: Dm.@new<datum_callback>(this, nameof(patient_insertion_check), target, source))))
            return null;
        // the side of the chassis the patient is on: facing follows the pilot's view (DV358), so the check is the tg one on the mech's current dir
        if (chassis == null || !Dm.truthy(Dm.bitand(get_dir(chassis, target), chassis.dir)))
            return null;
        await target.forceMove(this);
        patient = target;
        START_PROCESSING(SSobj, this);
        to_chat(source, span_notice(I18n.T("[] successfully loaded into []. Life support functions engaged.", target, this)));
        chassis.visible_message(span_warning(I18n.T("[] loads [] into [].", chassis, target, this)));
        log_message(Dm.text("[] loaded. Life support functions engaged.", target), LOG_MECHA);
        return await base.action(source, atomtarget, modifiers);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:67
    public virtual bool patient_insertion_check(mob_living_carbon target = null, mob user = null)
    {
        if (target.buckled != null)
        {
            to_chat(user, span_warning(I18n.T("[] will not fit into the sleeper because [] buckled to []!", target, target.p_theyre(), target.buckled)));
            return false;
        }
        if (Dm.truthy(target.has_buckled_mobs()))
        {
            to_chat(user, span_warning(I18n.T("[] will not fit into the sleeper because of the creatures attached to it!", target)));
            return false;
        }
        if (patient != null)
        {
            to_chat(user, span_warning(I18n.T("The sleeper is already occupied!")));
            return false;
        }
        return true;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:79
    public virtual async DmTask go_out()
    {
        if (patient == null)
            return;
        await patient.forceMove(get_turf(this));
        to_chat(chassis.occupants, span_notice(I18n.T("[] ejected. Life support functions disabled.", patient)));
        log_message(Dm.text("[] ejected. Life support functions disabled.", patient), LOG_MECHA);
        STOP_PROCESSING(SSobj, this);
        patient = null;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:88
    public override async DmTask detach(atom moveto = null)
    {
        if (patient != null)
        {
            to_chat(chassis.occupants, span_warning(I18n.T("Unable to detach [] - equipment occupied!", this)));
            return;
        }
        STOP_PROCESSING(SSobj, this);
        await base.detach(moveto);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:95
    public override DmList get_snowflake_data()
    {
        DmList data = Dm.list(Dm.kv("snowflake_id", MECHA_SNOWFLAKE_ID_SLEEPER));
        if (patient == null)
            return data;
        string patient_state;
        float stat__1 = Dm.num(patient.stat);
        if (stat__1 == STABLE)
            patient_state = "Stable";
        else if (stat__1 == DEAD)
            patient_state = "*Dead*";
        else if (stat__1 == SOFT_CRIT || stat__1 == HARD_CRIT)
            patient_state = "Critical";
        else
            patient_state = "Unknown";
        object core_temp = "";
        if (ishuman(patient))
            core_temp = Dm.num(patient.bodytemperature) - T0C;
        data["patient"] = Dm.list(
            Dm.kv("patient_name", patient.name),
            Dm.kv("patient_health", Dm.num(patient.health) / Dm.num(patient.maxHealth)),
            Dm.kv("patient_state", patient_state),
            Dm.kv("core_temp", core_temp),
            Dm.kv("brute_loss", patient.get_brute_loss()),
            Dm.kv("burn_loss", patient.get_fire_loss()),
            Dm.kv("toxin_loss", patient.get_tox_loss()),
            Dm.kv("oxygen_loss", patient.get_oxy_loss()));
        data["contained_reagents"] = get_reagent_data((patient.reagents as datum_reagents).reagent_list);
        data["has_brain_damage"] = Dm.num(patient.get_organ_loss(ORGAN_SLOT_BRAIN)) != 0 ? TRUE : FALSE;
        data["has_traumas"] = Dm.num(length(patient.get_traumas())) != 0 ? TRUE : FALSE;

        return data;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:129
    public override async DmTask<object> handle_ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        if (Dm.eq(action, "eject"))
        {
            await go_out();
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:135
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        Waiting.Detach(life_support(Dm.num(seconds_per_tick)));
        return null;
    }

    private async DmTask life_support(float seconds_per_tick)
    {
        if (!chassis.has_charge(energy_drain))
        {
            log_message("Deactivated.", LOG_MECHA);
            to_chat(chassis.occupants, span_warning(I18n.T("[] deactivated - no power.", this)));
            STOP_PROCESSING(SSobj, this);
            return;
        }
        mob_living_carbon ex_patient = patient;
        if (ex_patient == null)
            return;
        if (ex_patient.loc != this)
        {
            to_chat(chassis.occupants, span_notice(I18n.T("[] no longer detected. Life support functions diabled.", patient)));
            log_message(Dm.text("[] no longer detected - Life support functions disabled.", patient), LOG_MECHA);
            STOP_PROCESSING(SSobj, this);
            patient = null;
        }
        await ex_patient.adjust_oxy_loss(-2 * seconds_per_tick);
        await ex_patient.AdjustStun(-4 * SECONDS * seconds_per_tick);
        await ex_patient.AdjustKnockdown(-4 * SECONDS * seconds_per_tick);
        await ex_patient.AdjustParalyzed(-4 * SECONDS * seconds_per_tick);
        await ex_patient.AdjustImmobilized(-4 * SECONDS * seconds_per_tick);
        await ex_patient.AdjustUnconscious(-4 * SECONDS * seconds_per_tick);
        datum_reagents held = ex_patient.reagents as datum_reagents;
        if (Dm.num(held.get_reagent_amount(typeof(datum_reagent_medicine_epinephrine))) < 5
            && Dm.num(held.get_reagent_amount(typeof(datum_reagent_medicine_c2_penthrite))) <= 0
            && Dm.num(ex_patient.stat) >= SOFT_CRIT)
            await held.add_reagent(typeof(datum_reagent_medicine_epinephrine), 5);
        if (Dm.num(held.get_reagent_amount(typeof(datum_reagent_toxin_formaldehyde))) <= 0 && Dm.eq(ex_patient.stat, DEAD))
            await held.add_reagent(typeof(datum_reagent_toxin_formaldehyde), 3);
        chassis.use_energy(energy_drain);
    }
}

//Medical subtype with the chems
// tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:164
public partial class obj_item_mecha_parts_mecha_equipment_sleeper_medical : obj_item_mecha_parts_mecha_equipment_sleeper
{
    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:172
    public override DmList get_snowflake_data()
    {
        DmList data = base.get_snowflake_data();
        if (patient == null)
            return data;
        obj_item_mecha_parts_mecha_equipment_medical_syringe_gun shooter = Dm.locate<obj_item_mecha_parts_mecha_equipment_medical_syringe_gun>(chassis.contents);
        if (shooter != null)
            data["injectible_reagents"] = get_reagent_data((shooter.reagents as datum_reagents).reagent_list);
        return data;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:181
    public override async DmTask<object> handle_ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.handle_ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return TRUE;
        obj_item_mecha_parts_mecha_equipment_medical_syringe_gun shooter = Dm.locate<obj_item_mecha_parts_mecha_equipment_medical_syringe_gun>(chassis.contents);
        if (shooter != null)
        {
            foreach (datum_reagent medication in Dm.each<datum_reagent>((shooter.reagents as datum_reagents).reagent_list))
            {
                if (Dm.eq(action, "inject_reagent_" + medication.name))
                {
                    await inject_reagent(medication, shooter);
                    break; // or maybe return TRUE? i'm not certain
                }
            }
        }
        return dot;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:192
    public virtual async DmTask inject_reagent(datum_reagent reagent_to_inject = null, obj_item_mecha_parts_mecha_equipment_medical_syringe_gun SG = null)
    {
        if (reagent_to_inject == null || patient == null || SG == null || !Dm.@in(SG, chassis.flat_equipment))
            return;
        float to_inject = Math.Min(Dm.num(reagent_to_inject.volume), inject_amount);
        if (Dm.truthy(to_inject) && Dm.num((patient.reagents as datum_reagents).get_reagent_amount(reagent_to_inject.type)) + to_inject <= inject_amount * 2)
        {
            to_chat(chassis.occupants, span_notice(I18n.T("Injecting [] with [] units of [].", patient, to_inject, reagent_to_inject.name)));
            log_message(Dm.text("Injecting [] with [] units of [].", patient, to_inject, reagent_to_inject.name), LOG_MECHA);
            foreach (mob driver in Dm.each_cast<mob>(chassis.return_drivers()))
                log_combat(driver, patient, "injected", Dm.text("[] ([] - [] units)", name, reagent_to_inject, to_inject));
            await (SG.reagents as datum_reagents).trans_to(patient, to_inject, target_id: reagent_to_inject.type);
        }
    }
}

///////////////////////////////// Syringe Gun ///////////////////////////////////////////////////////////////

// tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:208
public partial class obj_item_mecha_parts_mecha_equipment_medical_syringe_gun : obj_item_mecha_parts_mecha_equipment_medical
{
    private const float FIRE_SYRINGE_MODE = 0;
    private const float ANALYZE_SYRINGE_MODE = 1;

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:231
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(max_volume, NO_REACT);
        known_reagents = Dm.list(Dm.kv(typeof(datum_reagent_medicine_epinephrine), "Epinephrine"), Dm.kv(typeof(datum_reagent_medicine_c2_multiver), "Multiver"));
        return dot;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:236
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:240
    public override async DmTask detach(atom moveto = null)
    {
        STOP_PROCESSING(SSobj, this);
        await base.detach(moveto);
    }

    // The display name known_reagents keeps for a type (known_reagents[i].name in tg reads the type's initial name).
    private static string reagent_name(object reagent_type) => Dm.initial(reagent_type, "name") as string;

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:244
    public override DmList get_snowflake_data()
    {
        datum_reagents held = reagents as datum_reagents;
        DmList analyzed_reagents = Dm.list(); // we need to make this list because .tsk wont map over an indexed array
        for (int i = 1; i <= known_reagents.Count; i++)
        {
            bool enabled = Dm.@in(known_reagents[i], processed_reagents);
            analyzed_reagents.Add(Dm.list(Dm.kv("name", reagent_name(known_reagents[i])), Dm.kv("enabled", enabled ? TRUE : FALSE)));
        }
        DmList data = Dm.list(
            Dm.kv("snowflake_id", MECHA_SNOWFLAKE_ID_SYRINGE),
            Dm.kv("mode", mode == FIRE_SYRINGE_MODE ? "Launch" : "Analyze"),
            Dm.kv("mode_label", "Action"),
            Dm.kv("syringe", LAZYLEN(syringes)),
            Dm.kv("max_syringe", max_syringes),
            Dm.kv("reagents", held.total_volume),
            Dm.kv("total_reagents", held.maximum_volume),
            Dm.kv("analyzed_reagents", analyzed_reagents));
        data["contained_reagents"] = get_reagent_data(held.reagent_list);
        return data;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:264
    public override async DmTask<object> handle_ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        datum_reagents held = reagents as datum_reagents;
        if (Dm.eq(action, "change_mode"))
        {
            mode = Dm.truthy(mode) ? 0 : 1;
            return TRUE;
        }
        else if (Dm.eq(action, "purge_all"))
            await held.clear_reagents();
        else
        {
            for (int i = 1; i <= known_reagents.Count; i++)
            {
                object reagent_id = known_reagents[i];
                if (Dm.eq(action, "purge_reagent_" + reagent_name(reagent_id)))
                    await held.del_reagent(reagent_id);
                else if (Dm.eq(action, "toggle_reagent_" + reagent_name(reagent_id)))
                    synthesize(reagent_id);
            }
        }
        return null;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:278
    public virtual void synthesize(object reagent = null)
    {
        if (Dm.@in(reagent, processed_reagents))
        {
            LAZYREMOVE(ref processed_reagents, reagent);
            return;
        }
        string message = Dm.text("[]", known_reagents[reagent]);
        LAZYADD(ref processed_reagents, reagent);
        if (!Dm.truthy(LAZYLEN(processed_reagents)))
            return;

        message += " added to production";
        START_PROCESSING(SSobj, this);
        to_chat(usr, message);
        to_chat(usr, span_notice(I18n.T("Reagent processing started.")));
        log_message("Reagent processing started.", LOG_MECHA);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:293
    public override async DmTask<object> action(mob source = null, atom target = null, DmList modifiers = null)
    {
        if (!Dm.truthy(action_checks(target)))
            return null;
        if (target is obj_item_reagent_containers_syringe syringe)
            return await load_syringe(syringe);
        if (istype(target, typeof(obj_item_storage)))//Loads syringes from boxes
        {
            foreach (obj_item_reagent_containers_syringe S in Dm.each<obj_item_reagent_containers_syringe>(target.contents.Copy()))
                await load_syringe(S, source);
            return null;
        }
        if (mode == ANALYZE_SYRINGE_MODE)
            return await analyze_reagents(target, source);
        //we're in syringe mode so lets do syringe stuff
        if (!Dm.truthy(LAZYLEN(syringes)))
        {
            to_chat(source, "<span class='alert'>" + I18n.T("No syringes loaded.") + "</span>");
            return null;
        }
        if (Dm.num((reagents as datum_reagents).total_volume) <= 0)
        {
            to_chat(source, "<span class='alert'>" + I18n.T("No available reagents to load syringe with.") + "</span>");
            return null;
        }
        if (HAS_TRAIT(source, TRAIT_PACIFISM))
        {
            to_chat(source, span_alert(I18n.T("The [] might be lethally chambered! You don't want to risk harming anyone...", this)));
            return null;
        }
        obj_item_ammo_casing_syringegun chambered = Dm.@new<obj_item_ammo_casing_syringegun>(this);
        log_message(Dm.text("Fired [] from [] by [], targeting [].", chambered, this, source, target), LOG_MECHA);
        await chambered.fire_casing(target, source as mob_living, null, 0, 0, null, 0, this);
        return await base.action(source, target, modifiers);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:319
    public virtual async DmTask<object> load_syringe(obj_item_reagent_containers_syringe S = null, mob user = null)
    {
        if (Dm.num(LAZYLEN(syringes)) >= max_syringes)
        {
            to_chat(user, span_warning(I18n.T("[]'s syringe chamber is full!", this)));
            return FALSE;
        }
        if (!Dm.truthy(await chassis.Adjacent(S)))
        {
            to_chat(user, span_warning(I18n.T("Unable to load syringe!")));
            return FALSE;
        }
        datum_reagents held = S.reagents as datum_reagents;
        await held.trans_to(this, Dm.num(held.total_volume), transferred_by: user);
        await S.forceMove(this);
        LAZYADD(ref syringes, S);
        to_chat(user, span_notice(I18n.T("Syringe loaded.")));
        return TRUE;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:332
    public virtual DmTask<object> analyze_reagents(atom A = null, mob user = null)
    {
        if (Dm.num(get_dist(this, A)) >= 4)
        {
            to_chat(user, span_notice(I18n.T("The object is too far away!")));
            return DmTask<object>.FromResult(FALSE);
        }
        if (A.reagents == null || ismob(A))
        {
            to_chat(user, span_warning(I18n.T("No reagent info gained from [].", A)));
            return DmTask<object>.FromResult(FALSE);
        }
        to_chat(user, span_notice(I18n.T("Analyzing reagents...")));
        foreach (datum_reagent R in Dm.each<datum_reagent>((A.reagents as datum_reagents).reagent_list))
        {
            if (Dm.truthy(Dm.bitand(R.chemical_flags, REAGENT_CAN_BE_SYNTHESIZED)) && add_known_reagent(R.type, R.name))
                to_chat(user, span_notice(I18n.T("Reagent analyzed, identified as [] and added to database.", R.name)));
        }
        to_chat(user, span_notice(I18n.T("Analysis complete.")));
        return DmTask<object>.FromResult(TRUE);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:346
    public virtual bool add_known_reagent(object r_id = null, object r_name = null)
    {
        if (Dm.@in(r_id, known_reagents))
            return false;
        known_reagents.Add(r_id);
        known_reagents[r_id] = r_name;
        return true;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:353
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        object dot = base.process(seconds_per_tick, __extra);
        if (Dm.truthy(dot))
            return dot;
        datum_reagents held = reagents as datum_reagents;
        if (!Dm.truthy(LAZYLEN(processed_reagents)) || Dm.num(held.total_volume) >= Dm.num(held.maximum_volume) || !chassis.has_charge(energy_drain))
        {
            to_chat(chassis.occupants, span_alert(I18n.T("Reagent processing stopped.")));
            log_message("Reagent processing stopped.", LOG_MECHA);
            return PROCESS_KILL;
        }
        float amount = Dm.num(seconds_per_tick) * synth_speed / Dm.num(LAZYLEN(processed_reagents));
        foreach (object reagent in Dm.each<object>(processed_reagents))
        {
            Waiting.Detach(held.add_reagent(reagent, amount));
            chassis.use_energy(energy_drain);
        }
        return dot;
    }
}

///////////////////////////////// Medical Beam ///////////////////////////////////////////////////////////////

// tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:371
public partial class obj_item_mecha_parts_mecha_equipment_medical_mechmedbeam : obj_item_mecha_parts_mecha_equipment_medical
{
    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:382
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        medigun = Dm.@new<obj_item_gun_medbeam_mech>(this);
        return dot;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:386
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref medigun);
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:390
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        object dot = base.process(seconds_per_tick, __extra);
        if (Dm.truthy(dot) || !Dm.truthy(length(chassis.occupants)))
            return dot;
        if (Dm.truthy(chassis.weapons_safety))
            medigun.LoseTarget();
        medigun.process(SSOBJ_DT());
        return dot;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:398
    public override async DmTask<object> action(mob source = null, atom target = null, DmList modifiers = null)
    {
        await medigun.process_fire(target, loc as mob_living);
        return null;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/medical_tools.dm:10 and :401
    public override async DmTask detach(atom moveto = null)
    {
        STOP_PROCESSING(SSobj, this);
        medigun.LoseTarget();
        await base.detach(moveto);
    }
}
