// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/experimentor/experiments.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps RD4, D333: the experimentor): the experimentor's reactions, one handler per scan type: its start message, its
// critical result on a critical item (a better focus, a clone, plasma, a cup of suspicious coffee, plasteel) and its malfunctions
// (lashing arms, a thrown item, radiation, toxic waste, a transformed item, chemical smoke, an EMP, a fireball, a fire, hot or cold
// air, space-time throws); the relic's Discover and the failure. The var blocks are generated (D111).

// tg: code/modules/research/experimentor/experiments.dm:1
public partial class datum_experimentor_result_handler : datum
{
    // tg: code/modules/research/experimentor/experiments.dm:19
    public virtual async DmTask execute(obj_machinery_rnd_experimentor machine = null, obj_item exp_on = null)
    {
        object final_message = replacetext(start_message_template, "%ITEM%", exp_on);
        machine.show_start_message(final_message, start_message_type);

        object critical = machine.is_critical_reaction(); // tg passes exp_on, which the proc does not take
        if (Dm.truthy(critical) && prob(Dm.num(critical_prob)))
        {
            await handle_critical(machine, exp_on);
            return;
        }

        await handle_malfunctions(machine, exp_on);
    }

    // tg: code/modules/research/experimentor/experiments.dm:30
    public virtual async DmTask handle_critical(obj_machinery_rnd_experimentor machine = null, obj_item exp_on = null)
    {
        await DmTask.CompletedTask;
        if (Dm.truthy(critical_message_template))
        {
            object final_critical_message = replacetext(critical_message_template, "%ITEM%", exp_on);
            machine.visible_message(span_notice(Dm.str(final_critical_message)));
        }
    }

    // tg: code/modules/research/experimentor/experiments.dm:35
    public virtual DmTask handle_malfunctions(obj_machinery_rnd_experimentor machine = null, obj_item exp_on = null)
    {
        return DmTask.CompletedTask;
    }
}

/// Pokes the object
// tg: code/modules/research/experimentor/experiments.dm:39
public partial class datum_experimentor_result_handler_scan_poke : datum_experimentor_result_handler_scan
{
    // tg: code/modules/research/experimentor/experiments.dm:47
    public override async DmTask handle_critical(obj_machinery_rnd_experimentor machine = null, obj_item exp_on = null)
    {
        await base.handle_critical(machine, exp_on);
        machine.critical_malfunction_counter++;
        await machine.RefreshParts();
    }

    // tg: code/modules/research/experimentor/experiments.dm:52
    public override async DmTask handle_malfunctions(obj_machinery_rnd_experimentor machine = null, obj_item exp_on = null)
    {
        float malf_chance = machine.get_malfunction_chance();

        if (prob(EFFECT_PROB_VERYLOW * malf_chance))
        {
            machine.visible_message(span_danger(I18n.T("[] malfunctions and destroys [], lashing its arms out at nearby people!", machine, exp_on)));
            foreach (mob_living nearby_mob in Dm.each<mob_living>(oview(1, machine)))
            {
                await nearby_mob.apply_damage(15, BRUTE, pick(BODY_ZONE_HEAD, BODY_ZONE_CHEST));
                machine.investigate_log(Dm.text("Experimentor dealt minor brute to [].", nearby_mob), INVESTIGATE_EXPERIMENTOR);
            }
            QDEL_NULL(ref machine.loaded_item);
        }
        else if (prob(EFFECT_PROB_LOW * malf_chance))
        {
            machine.visible_message(span_warning(I18n.T("[] malfunctions!", machine)));
            await machine.run_experiment__proc(SCANTYPE_OBLITERATE);
        }
        else if (prob(EFFECT_PROB_MEDIUM * malf_chance))
        {
            machine.visible_message(span_danger(I18n.T("[] malfunctions, throwing the []!", machine, exp_on)));
            mob_living target = Dm.locate<mob_living>(oview(7, machine));
            if (target != null)
            {
                obj_item throwing = machine.loaded_item;
                machine.investigate_log(Dm.text("Experimentor has thrown [] at []", machine.loaded_item, key_name(target)), INVESTIGATE_EXPERIMENTOR);
                machine.item_eject();
                if (throwing != null)
                    await throwing.throw_at(target, 10, 1);
            }
        }
    }
}

/// Infuses it with radiation
// tg: code/modules/research/experimentor/experiments.dm:77
public partial class datum_experimentor_result_handler_scan_irradiate : datum_experimentor_result_handler_scan
{
    // tg: code/modules/research/experimentor/experiments.dm:86
    public override async DmTask handle_critical(obj_machinery_rnd_experimentor machine = null, obj_item exp_on = null)
    {
        await base.handle_critical(machine, exp_on);
        machine.investigate_log(Dm.text("Experimentor has made a clone of []", exp_on), INVESTIGATE_EXPERIMENTOR);
        machine.item_eject(TRUE);
    }

    // tg: code/modules/research/experimentor/experiments.dm:91
    public override async DmTask handle_malfunctions(obj_machinery_rnd_experimentor machine = null, obj_item exp_on = null)
    {
        float malf_chance = machine.get_malfunction_chance();

        if (prob(EFFECT_PROB_VERYLOW * malf_chance))
        {
            machine.visible_message(span_danger(I18n.T("[] malfunctions, melting [] and leaking radiation!", machine, exp_on)));
            playsound(machine, Dm.resource("sound/effects/supermatter.ogg"), 50, TRUE, -3);
            radiation_pulse(machine, max_range: 6, threshold: 0.3f);
            QDEL_NULL(ref machine.loaded_item);
        }
        else if (prob(EFFECT_PROB_LOW * malf_chance))
        {
            machine.visible_message(span_warning(I18n.T("[] malfunctions, spewing toxic waste!", machine)));
            foreach (turf T in Dm.each<turf>(oview(1, machine)))
            {
                if (!Dm.truthy(T.density) && prob(EFFECT_PROB_VERYHIGH) && Dm.locate<obj_effect_decal_cleanable_greenglow>(T.contents) == null)
                    Dm.@new<obj_effect_decal_cleanable_greenglow_filled>(T);
            }
            QDEL_NULL(ref machine.loaded_item);
        }
        else if (prob(EFFECT_PROB_MEDIUM * malf_chance))
        {
            string savedName = Dm.text("[]", exp_on);
            QDEL_NULL(ref machine.loaded_item);
            Type newPath = text2path(pick_weight(obj_machinery_rnd_experimentor.valid_items) as string);
            machine.loaded_item = Dm.@new(newPath, machine) as obj_item;
            machine.visible_message(span_warning(I18n.T("[] malfunctions, transforming [] into []!", machine, savedName, machine.loaded_item)));
            machine.investigate_log(Dm.text("Experimentor has transformed [] into []", savedName, machine.loaded_item), INVESTIGATE_EXPERIMENTOR);

            if (machine.loaded_item is obj_item_grenade_chem_grenade CG)
                await CG.detonate();

            machine.item_eject();
        }
    }
}

/// Fills the chamber with gas
// tg: code/modules/research/experimentor/experiments.dm:122
public partial class datum_experimentor_result_handler_scan_gas : datum_experimentor_result_handler_scan
{
    // tg: code/modules/research/experimentor/experiments.dm:131
    public override async DmTask handle_critical(obj_machinery_rnd_experimentor machine = null, obj_item exp_on = null)
    {
        await base.handle_critical(machine, exp_on);
        Dm.@new<obj_item_stack_sheet_mineral_plasma>(get_turf(pick(oview(1, machine)) as atom));
    }

    // tg: code/modules/research/experimentor/experiments.dm:135
    public override async DmTask handle_malfunctions(obj_machinery_rnd_experimentor machine = null, obj_item exp_on = null)
    {
        float malf_chance = machine.get_malfunction_chance();
        object chosenchem;

        if (prob(EFFECT_PROB_VERYLOW * malf_chance))
        {
            machine.visible_message(span_danger(I18n.T("[] destroys [], leaking dangerous gas!", machine, exp_on)));
            chosenchem = pick(
                typeof(datum_reagent_carbon),
                typeof(datum_reagent_uranium_radium),
                typeof(datum_reagent_toxin),
                typeof(datum_reagent_consumable_condensedcapsaicin),
                typeof(datum_reagent_drug_mushroomhallucinogen),
                typeof(datum_reagent_drug_space_drugs),
                typeof(datum_reagent_consumable_ethanol),
                typeof(datum_reagent_consumable_ethanol_beepsky_smash)
            );
            await do_chem_smoke(0, machine, machine.loc, chosenchem, 50);
            machine.investigate_log(Dm.text("Experimentor has released [] smoke.", chosenchem), INVESTIGATE_EXPERIMENTOR);
            playsound(machine, Dm.resource("sound/effects/smoke.ogg"), 50, TRUE, -3);
            QDEL_NULL(ref machine.loaded_item);
        }
        else if (prob(EFFECT_PROB_VERYLOW * malf_chance))
        {
            machine.visible_message(span_danger(I18n.T("[]'s chemical chamber has sprung a leak!", machine)));
            chosenchem = pick(
                typeof(datum_reagent_mutationtoxin_classic),
                typeof(datum_reagent_cyborg_mutation_nanomachines),
                typeof(datum_reagent_toxin_acid)
            );
            await do_chem_smoke(0, machine, machine.loc, chosenchem, 50);
            playsound(machine, Dm.resource("sound/effects/smoke.ogg"), 50, TRUE, -3);
            QDEL_NULL(ref machine.loaded_item);
            machine.warn_admins(usr, Dm.text("[] smoke", chosenchem));
            machine.investigate_log(Dm.text("Experimentor has released <font color='red'>[]</font> smoke!", chosenchem), INVESTIGATE_EXPERIMENTOR);
        }
        else if (prob(EFFECT_PROB_LOW * malf_chance))
        {
            machine.visible_message(span_warning(I18n.T("[] malfunctions, spewing harmless gas.", machine)));
            await do_smoke(1, machine, machine.loc);
        }
        else if (prob(EFFECT_PROB_MEDIUM * malf_chance))
        {
            machine.visible_message(span_warning(I18n.T("[] melts [], ionizing the air around it!", machine, exp_on)));
            await empulse(machine.loc, 4, 6, emp_source: machine);
            machine.investigate_log("Experimentor has generated an Electromagnetic Pulse.", INVESTIGATE_EXPERIMENTOR);
            QDEL_NULL(ref machine.loaded_item);
        }
    }
}

/// Heats the object
// tg: code/modules/research/experimentor/experiments.dm:181
public partial class datum_experimentor_result_handler_scan_heat : datum_experimentor_result_handler_scan
{
    // tg: code/modules/research/experimentor/experiments.dm:190
    public override async DmTask handle_critical(obj_machinery_rnd_experimentor machine = null, obj_item exp_on = null)
    {
        await base.handle_critical(machine, exp_on);
        playsound(machine, Dm.resource("sound/machines/ding.ogg"), 50, TRUE);

        var C = Dm.@new<obj_item_reagent_containers_cup_glass_coffee>(get_turf(pick(oview(1, machine)) as atom));
        object chosenchem = pick(
            typeof(datum_reagent_toxin_plasma),
            typeof(datum_reagent_consumable_capsaicin),
            typeof(datum_reagent_consumable_ethanol)
        );
        await (C.reagents as datum_reagents).remove_all(25);
        await (C.reagents as datum_reagents).add_reagent(chosenchem, 50);
        C.name = I18n.T("Cup of Suspicious Liquid");
        C.desc = I18n.T("It has a large hazard symbol printed on the side in fading ink.");
        machine.investigate_log(Dm.text("Experimentor has made a cup of [] coffee.", chosenchem), INVESTIGATE_EXPERIMENTOR);
    }

    // tg: code/modules/research/experimentor/experiments.dm:206
    public override async DmTask handle_malfunctions(obj_machinery_rnd_experimentor machine = null, obj_item exp_on = null)
    {
        float malf_chance = machine.get_malfunction_chance();

        if (prob(EFFECT_PROB_VERYLOW * malf_chance))
        {
            turf start = get_turf(machine);
            mob target_mob = Dm.locate<mob_living>(view(machine, 3));
            turf target_turf = get_turf(target_mob);

            if (target_turf != null)
            {
                machine.visible_message(span_danger(I18n.T("[] dangerously overheats, launching a flaming fuel orb!", machine)));
                machine.investigate_log(Dm.text("Experimentor has launched a <font color='red'>fireball</font> at []!", target_mob), INVESTIGATE_EXPERIMENTOR);
                var FB = Dm.@new<obj_projectile_magic_fireball>(start);
                await FB.aim_projectile(target_turf, start);
                await FB.fire();
            }
        }
        else if (prob(EFFECT_PROB_LOW * malf_chance))
        {
            machine.visible_message(span_danger(I18n.T("[] malfunctions, melting [] and releasing a burst of flame!", machine, exp_on)));
            await explosion(machine, devastation_range: -1, flame_range: 2, adminlog: FALSE);
            machine.investigate_log("Experimentor started a fire.", INVESTIGATE_EXPERIMENTOR);
            QDEL_NULL(ref machine.loaded_item);
        }
        else if (prob(EFFECT_PROB_MEDIUM * malf_chance))
        {
            machine.visible_message(span_warning(I18n.T("[] malfunctions, melting [] and leaking hot air!", machine, exp_on)));
            var env = await ((atom)machine.loc).return_air() as datum_gas_mixture;
            if (env != null)
            {
                float heat_capacity = max(Dm.num(env.heat_capacity()), 1);
                env.temperature = min((env.temperature * heat_capacity + 100000) / heat_capacity, 1000);
            }
            machine.air_update_turf(FALSE, FALSE);
            machine.investigate_log("Experimentor has released hot air.", INVESTIGATE_EXPERIMENTOR);
            QDEL_NULL(ref machine.loaded_item);
        }
        else if (prob(EFFECT_PROB_MEDIUM * malf_chance))
        {
            machine.visible_message(span_warning(I18n.T("[] malfunctions, activating its emergency coolant systems!", machine)));
            await do_smoke(1, machine, machine.loc);
            foreach (mob_living nearby_mob in Dm.each<mob_living>(oview(1, machine)))
            {
                await nearby_mob.apply_damage(5, BURN, pick(BODY_ZONE_HEAD, BODY_ZONE_CHEST));
                machine.investigate_log(Dm.text("Experimentor has dealt minor burn damage to []", key_name(nearby_mob)), INVESTIGATE_EXPERIMENTOR);
            }
            machine.item_eject();
        }
    }
}

/// Cools the object
// tg: code/modules/research/experimentor/experiments.dm:252
public partial class datum_experimentor_result_handler_scan_cold : datum_experimentor_result_handler_scan
{
    // tg: code/modules/research/experimentor/experiments.dm:261
    public override async DmTask handle_critical(obj_machinery_rnd_experimentor machine = null, obj_item exp_on = null)
    {
        await base.handle_critical(machine, exp_on);
        playsound(machine, Dm.resource("sound/machines/ding.ogg"), 50, TRUE);

        var C = Dm.@new<obj_item_reagent_containers_cup_glass_coffee>(get_turf(pick(oview(1, machine)) as atom));
        object chosenchem = pick(
            typeof(datum_reagent_uranium),
            typeof(datum_reagent_consumable_frostoil),
            typeof(datum_reagent_medicine_ephedrine)
        );
        await (C.reagents as datum_reagents).remove_all(25);
        await (C.reagents as datum_reagents).add_reagent(chosenchem, 50);
        C.name = I18n.T("Cup of Suspicious Liquid");
        C.desc = I18n.T("It has a large hazard symbol printed on the side in fading ink.");
        machine.investigate_log(Dm.text("Experimentor has made a cup of [] coffee.", chosenchem), INVESTIGATE_EXPERIMENTOR);
    }

    // tg: code/modules/research/experimentor/experiments.dm:277
    public override async DmTask handle_malfunctions(obj_machinery_rnd_experimentor machine = null, obj_item exp_on = null)
    {
        float malf_chance = machine.get_malfunction_chance();

        if (prob(EFFECT_PROB_VERYLOW * malf_chance))
        {
            machine.visible_message(span_danger(I18n.T("[] malfunctions, shattering [] and releasing a dangerous cloud of coolant!", machine, exp_on)));
            await do_chem_smoke(0, machine, machine.loc, typeof(datum_reagent_consumable_frostoil), 50);
            machine.investigate_log("Experimentor has released frostoil gas.", INVESTIGATE_EXPERIMENTOR);
            playsound(machine, Dm.resource("sound/effects/smoke.ogg"), 50, TRUE, -3);
            QDEL_NULL(ref machine.loaded_item);
        }
        else if (prob(EFFECT_PROB_LOW * malf_chance))
        {
            machine.visible_message(span_warning(I18n.T("[] malfunctions, shattering [] and leaking cold air!", machine, exp_on)));
            var env = await ((atom)machine.loc).return_air() as datum_gas_mixture;
            if (env != null)
            {
                float heat_capacity = max(Dm.num(env.heat_capacity()), 1);
                env.temperature = max((env.temperature * heat_capacity - 75000) / heat_capacity, TCMB);
            }
            machine.air_update_turf(FALSE, FALSE);
            machine.investigate_log("Experimentor has released cold air.", INVESTIGATE_EXPERIMENTOR);
            QDEL_NULL(ref machine.loaded_item);
        }
        else if (prob(EFFECT_PROB_MEDIUM * malf_chance))
        {
            machine.visible_message(span_warning(I18n.T("[] malfunctions, releasing a flurry of chilly air as [] pops out!", machine, exp_on)));
            await do_smoke(1, machine, machine.loc);
            machine.item_eject();
        }
    }
}

/// Crushes the object
// tg: code/modules/research/experimentor/experiments.dm:305
public partial class datum_experimentor_result_handler_scan_obliterate : datum_experimentor_result_handler_scan
{
    // tg: code/modules/research/experimentor/experiments.dm:314
    public override async DmTask handle_critical(obj_machinery_rnd_experimentor machine = null, obj_item exp_on = null)
    {
        await base.handle_critical(machine, exp_on);
        Dm.@new<obj_item_stack_sheet_plasteel>(get_turf(pick(oview(1, machine)) as atom));
    }

    // tg: code/modules/research/experimentor/experiments.dm:318
    public override async DmTask handle_malfunctions(obj_machinery_rnd_experimentor machine = null, obj_item exp_on = null)
    {
        float malf_chance = machine.get_malfunction_chance();

        if (prob(EFFECT_PROB_VERYLOW * malf_chance))
        {
            machine.visible_message(span_danger(I18n.T("[]'s crusher goes way too many levels too high, crushing right through space-time!", machine)));
            playsound(machine, Dm.resource("sound/effects/supermatter.ogg"), 50, TRUE, -3);
            machine.investigate_log("Experimentor has triggered the 'throw things' reaction.", INVESTIGATE_EXPERIMENTOR);

            foreach (atom_movable AM in Dm.each<atom_movable>(oview(7, machine)))
            {
                if (!Dm.truthy(AM.anchored))
                    await AM.throw_at(machine, 10, 1);
            }
        }
        else if (prob(EFFECT_PROB_LOW * malf_chance))
        {
            machine.visible_message(span_danger(I18n.T("[]'s crusher goes one level too high, crushing right into space-time!", machine)));
            playsound(machine, Dm.resource("sound/effects/supermatter.ogg"), 50, TRUE, -3);
            machine.investigate_log("Experimentor has triggered the 'minor throw things' reaction.", INVESTIGATE_EXPERIMENTOR);

            DmList throw_at = Dm.list();
            foreach (atom_movable AM in Dm.each<atom_movable>(oview(7, machine)))
            {
                if (!Dm.truthy(AM.anchored))
                    throw_at.Add(AM);
            }

            for (int counter = 1; counter <= throw_at.len; counter++)
            {
                var cast = throw_at[counter] as atom_movable;
                await cast.throw_at(pick(throw_at) as atom, 10, 1);
            }
        }

        QDEL_NULL(ref machine.loaded_item);
    }
}

/// Discovers relic properties
// tg: code/modules/research/experimentor/experiments.dm:342
public partial class datum_experimentor_result_handler_scan_discover : datum_experimentor_result_handler_scan
{
    // tg: code/modules/research/experimentor/experiments.dm:348
    public override async DmTask execute(obj_machinery_rnd_experimentor machine = null, obj_item exp_on = null)
    {
        await DmTask.CompletedTask;
        object final_message = replacetext(start_message_template, "%ITEM%", exp_on);
        machine.show_start_message(final_message, start_message_type);
        playsound(machine, Dm.resource("sound/effects/supermatter.ogg"), 50, 3, -1);

        var loaded_artifact = machine.loaded_item as obj_item_assembly_relic;
        if (loaded_artifact != null)
        {
            loaded_artifact.reveal();
            machine.investigate_log(Dm.text("Experimentor has revealed a relic with [] effect.", span_danger(Dm.text("[]", loaded_artifact.hidden_power))), INVESTIGATE_EXPERIMENTOR);
        }
        machine.item_eject();
    }
}

/// Experiment failure
// tg: code/modules/research/experimentor/experiments.dm:356
public partial class datum_experimentor_result_handler_fail : datum_experimentor_result_handler
{
    // tg: code/modules/research/experimentor/experiments.dm:360
    public override async DmTask execute(obj_machinery_rnd_experimentor machine = null, obj_item exp_on = null)
    {
        await DmTask.CompletedTask;
        object a = pick("rumbles", "shakes", "vibrates", "shudders", "honks");
        object b = pick("crushes", "spins", "viscerates", "smashes", "insults");
        machine.visible_message(span_warning(I18n.T("[] [], and [], the experiment was a failure.", exp_on, a, b)));
    }
}
