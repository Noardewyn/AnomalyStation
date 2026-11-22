// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/machinery/gibber.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the gibber and the autogibber. The var blocks are generated (D111).

// tg: code/modules/food_and_drinks/machinery/gibber.dm:1
public partial class obj_machinery_gibber : obj_machinery
{
    // tg: code/modules/food_and_drinks/machinery/gibber.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_COMPONENT_CLEAN_ACT, nameof(on_cleaned));
        if (prob(5))
        {
            name = I18n.T("meat grinder");
            desc = I18n.T("Okay, if I... if I chop you up in a meat grinder, and the only thing that comes out, that's left of you, is your eyeball, you'r- you're PROBABLY DEAD! You're probably going to - not you, I'm just sayin', like, if you- if somebody were to, like, push you into a meat grinder, and, like, your- one of your finger bones is still intact, they're not gonna pick it up and go, Well see, yeah it wasn't deadly, it wasn't an instant kill move! You still got, like, this part of your finger left!");
            dirty = TRUE;
            update_appearance(UPDATE_OVERLAYS);
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/gibber.dm:36
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        gibtime = 40;
        efficiency = Dm.initial(this, (obj_machinery_gibber g) => g.efficiency);
        foreach (datum_stock_part_matter_bin matter_bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
            efficiency += Dm.num(matter_bin.tier) * 0.25f;
        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
        {
            gibtime -= 5 * Dm.num(servo.tier);
            if (Dm.num(servo.tier) >= 2)
                ignore_clothing = TRUE;
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/gibber.dm:47
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(in_range(user, this)) || isobserver(user))
        {
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: Outputting <b>[]%</b> of meat after <b>[]</b> seconds of processing.", efficiency, gibtime * 0.1f)));
            foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
            {
                if (Dm.num(servo.tier) >= 2)
                    Dm.append(ref dot, span_notice(I18n.T("[] has been upgraded to process inorganic materials.", this)));
            }
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/gibber.dm:55
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(dirty))
        {
            mutable_appearance blood_overlay = mutable_appearance(icon, "grinder_bloody", appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)) as mutable_appearance;
            if (Dm.truthy(blood_dna_info))
                blood_overlay.color = await get_color_from_blood_list(blood_dna_info as DmList);
            else
                blood_overlay.color = BLOOD_COLOR_RED;
            Dm.append(ref dot, blood_overlay);
        }
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN)) || Dm.truthy(panel_open))
            return dot;
        // DEVIATION(DV12): the emissive_appearance copies of grinder_empty, grinder_active and grinder_loaded (the glow on the emissive
        // plane) are not created
        if (occupant == null)
        {
            Dm.append(ref dot, "grinder_empty");
            return dot;
        }
        if (Dm.truthy(operating))
        {
            Dm.append(ref dot, "grinder_active");
            Dm.append(ref dot, "grinder_jaws_active");
            return dot;
        }
        Dm.append(ref dot, "grinder_loaded");
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/gibber.dm:78
    public override void on_set_panel_open(object old_value = null)
    {
        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/food_and_drinks/machinery/gibber.dm:81
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/modules/food_and_drinks/machinery/gibber.dm:84
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        await go_out();
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/gibber.dm:87
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        await go_out();
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/gibber.dm:90
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN)))
            return dot;
        if (Dm.truthy(operating))
        {
            to_chat(user, span_danger(I18n.T("It's locked and running.")));
            return dot;
        }

        if (!Dm.truthy(anchored))
        {
            to_chat(user, span_warning(I18n.T("[] cannot be used unless bolted to the ground!", this)));
            return dot;
        }

        if (user.pulling != null && isliving(user.pulling))
        {
            mob_living L = (mob_living)user.pulling;
            if (!iscarbon(L))
            {
                to_chat(user, span_warning(I18n.T("This item is not suitable for []!", this)));
                return dot;
            }
            mob_living_carbon C = (mob_living_carbon)L;
            if (C.buckled != null || Dm.truthy(C.has_buckled_mobs()))
            {
                to_chat(user, span_warning(I18n.T("[] is attached to something!", C)));
                return dot;
            }

            if (!Dm.truthy(ignore_clothing))
            {
                foreach (obj_item I in Dm.each<obj_item>(Dm.add(C.held_items, C.get_equipped_items())))
                {
                    if (!HAS_TRAIT(I, TRAIT_NODROP))
                    {
                        to_chat(user, span_warning(I18n.T("Subject may not have abiotic items on!")));
                        return dot;
                    }
                }
            }

            user.visible_message(span_danger(I18n.T("[] starts to put [] into []!", user, C, this)));

            add_fingerprint(user);

            if (Dm.truthy(await do_after(user, gibtime, target: this)))
            {
                if (C != null && user.pulling == C && C.buckled == null && !Dm.truthy(C.has_buckled_mobs()) && occupant == null)
                {
                    user.visible_message(span_danger(I18n.T("[] stuffs [] into []!", user, C, this)));
                    await C.forceMove(this);
                    set_occupant(C);
                    update_appearance();
                }
            }
        }
        else
            await start_gibbing(user);
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/gibber.dm:133
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/machinery/gibber.dm:138
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/food_and_drinks/machinery/gibber.dm:141
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_pry_open(user, tool, close_after_pry: TRUE, deconstruct_on_fail: TRUE);
    }

    // tg: code/modules/food_and_drinks/machinery/gibber.dm:144
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(panel_open) ? Dm.text("[]_open", base_icon_state) : base_icon_state as string;
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/gibber.dm:148  GAME_VERB_SRC(/obj/machinery/gibber, eject, oview(1), "Empty gibber", null)
    // The verb hands its arguments to SSverbs.invoke_verb, which runs the body for a caller with a client (observer.cs's
    // reenter_corpse); the verb panel and SSverbs are M9.
    public virtual void eject(params object[] __extra)
    {
        mob resolved_caller = usr;
        if (resolved_caller?.client == null)
            return;
        Waiting.Detach(__gvb_eject(Dm.list()));
    }

    // tg: code/modules/food_and_drinks/machinery/gibber.dm:148  (the verb's body, __gvb_eject)
    public virtual async DmTask __gvb_eject(DmList structured_args = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(usr) || HAS_TRAIT(usr, TRAIT_HANDS_BLOCKED))
            return;
        if (!Dm.truthy(await usr.can_perform_action(this)))
            return;
        await this.go_out();
        add_fingerprint(usr);
        return;
    }

    // tg: code/modules/food_and_drinks/machinery/gibber.dm:158
    public virtual async DmTask go_out()
    {
        await dump_inventory_contents();
        update_appearance();
    }

    // tg: code/modules/food_and_drinks/machinery/gibber.dm:162
    public virtual async DmTask start_gibbing(mob user = null)
    {
        if (Dm.truthy(operating))
            return;

        if (occupant == null)
        {
            audible_message(span_hear(I18n.T("You hear a loud metallic grinding sound.")));
            return;
        }

        if (Dm.truthy(Dm.bitand(occupant.flags_1, HOLOGRAM_1)))
        {
            audible_message(span_hear(I18n.T("You hear a very short metallic grinding sound.")));
            playsound(loc, Dm.resource("sound/machines/hiss.ogg"), 20, TRUE);
            qdel(occupant);
            set_occupant(null);
            return;
        }

        await use_energy(active_power_usage);
        audible_message(span_hear(I18n.T("You hear a loud squelchy grinding sound.")));
        playsound(loc, Dm.resource("sound/machines/juicer.ogg"), 50, TRUE);
        operating = TRUE;
        update_appearance();
        Shake(pixelshiftx: 1, pixelshifty: 0, duration: gibtime);

        mob_living victim = occupant as mob_living;
        DmList results = Dm.list();
        DmList diseases = Dm.list();
        foreach (datum_disease disease in Dm.each_cast<datum_disease>(await victim.get_static_viruses()))
        {
            // admin or special viruses that should not be reproduced
            if (!Dm.truthy(Dm.bitand(disease.spread_flags, DISEASE_SPREAD_SPECIAL | DISEASE_SPREAD_NON_CONTAGIOUS)))
                Dm.append(ref diseases, disease);
        }

        blood_dna_info = victim.get_blood_dna_list();

        if (!ishuman(victim))
        {
            Type meat_type = typeof(obj_item_food_meat_slab);
            Type skin_type = null;
            object meat_produced = 2;
            object skin_produced = 1;
            if (Dm.truthy(victim.butcher_results) || Dm.truthy(victim.guaranteed_butcher_results))
            {
                foreach (object possible_drop in Dm.each(Dm.bitor(victim.butcher_results, victim.guaranteed_butcher_results)))
                {
                    if (ispath(possible_drop, typeof(obj_item_food_meat_slab)))
                    {
                        meat_type = (Type)possible_drop;
                        meat_produced = Dm.add(victim.butcher_results?[possible_drop], victim.guaranteed_butcher_results?[possible_drop]);
                    }
                    else if (ispath(possible_drop, typeof(obj_item_stack_sheet_animalhide)))
                    {
                        skin_type = (Type)possible_drop;
                        skin_produced = Dm.add(victim.butcher_results?[possible_drop], victim.guaranteed_butcher_results?[possible_drop]);
                    }
                }
            }
            else if (iscarbon(victim))
            {
                mob_living_carbon as_carbon = (mob_living_carbon)victim;
                meat_type = as_carbon.type_of_meat;
                if (isalien(victim))
                    skin_type = typeof(obj_item_stack_sheet_animalhide_xeno);
            }

            float initial_efficiency = Dm.initial(this, (obj_machinery_gibber g) => g.efficiency);
            for (float i = 1, i__end = rand(floor(Dm.num(meat_produced) * efficiency / initial_efficiency), ceil(Dm.num(meat_produced) * efficiency / initial_efficiency)); i <= i__end; i++)
                Dm.append(ref results, await spawn_meat(victim, meat_type, diseases));

            if (skin_type != null && Dm.truthy(skin_produced))
            {
                for (float i = 1, i__end = rand(floor(Dm.num(skin_produced) * efficiency / initial_efficiency), ceil(Dm.num(skin_produced) * efficiency / initial_efficiency)); i <= i__end; i++)
                    Dm.append(ref results, Dm.@new(skin_type, this));
            }

            if (Dm.truthy(victim._listen_lookup?[COMSIG_LIVING_GIBBER_ACT]))
                victim._SendSignal(COMSIG_LIVING_GIBBER_ACT, Dm.list(victim, user, this, results));
            await process_results(victim, results);
            _addtimer(Dm.@new<datum_callback>(this, nameof(finish_gibbing), results, victim.get_gibs_type(), diseases), gibtime, file: "code/modules/food_and_drinks/machinery/gibber.dm", line: 223);
            log_combat(user, victim, "gibbed");
            victim.investigate_log(Dm.text("has been gibbed by [].", this), INVESTIGATE_DEATHS);
            await victim.death(TRUE);
            victim.ghostize();
            set_occupant(null);
            qdel(victim);
            return;
        }

        mob_living_carbon_human agent_whiskey = (mob_living_carbon_human)victim;
        float drop_chance = 0;
        foreach (obj_item_bodypart limb in Dm.each_cast<obj_item_bodypart>(agent_whiskey.get_bodyparts()))
        {
            DmList limb_butcher_drops = limb.get_butcher_drops() as DmList;
            foreach (Type drop_type in Dm.each_cast<Type>(limb_butcher_drops))
            {
                float amount = Dm.num(limb_butcher_drops[drop_type] is var or__1 && Dm.truthy(or__1) ? or__1 : 1);
                drop_chance += amount * efficiency;
                if (drop_chance > 1)
                {
                    amount = floor(drop_chance);
                    if (prob(drop_chance * 100))
                        amount += 1;
                }
                else
                {
                    amount = 1;
                    if (!prob(drop_chance * 100))
                        continue;
                }
                drop_chance = 0;

                if (ispath(drop_type, typeof(obj_item_food_meat)))
                {
                    for (float i = 1, i__end = amount; i <= i__end; i++)
                        Dm.append(ref results, await spawn_meat(victim, drop_type, diseases));
                    continue;
                }

                if (ispath(drop_type, typeof(obj_item_stack)))
                {
                    if (ispath(drop_type, typeof(obj_item_stack_sheet_animalhide_carbon)))
                        Dm.append(ref results, Dm.@new(drop_type, this, amount, /*merge = */TRUE, /*mat_override = */null, /*mat_amount = */1, (Dm.truthy(limb.skin_tone) ? limb.skin_tone : limb.species_color)));
                    else
                        Dm.append(ref results, Dm.@new(drop_type, this, amount));
                    continue;
                }

                for (float i = 1, i__end = amount; i <= i__end; i++)
                    Dm.append(ref results, Dm.@new(drop_type, this));
            }
        }

        if (Dm.truthy(victim._listen_lookup?[COMSIG_LIVING_GIBBER_ACT]))
            victim._SendSignal(COMSIG_LIVING_GIBBER_ACT, Dm.list(victim, user, this, results));
        await process_results(victim, results);
        _addtimer(Dm.@new<datum_callback>(this, nameof(finish_gibbing), results, victim.get_gibs_type(), diseases), gibtime, file: "code/modules/food_and_drinks/machinery/gibber.dm", line: 266);
        log_combat(user, victim, "gibbed");
        victim.investigate_log(Dm.text("has been gibbed by [].", this), INVESTIGATE_DEATHS);
        await victim.death(TRUE);
        victim.ghostize();
        set_occupant(null);
        qdel(victim);
    }

    // tg: code/modules/food_and_drinks/machinery/gibber.dm:274
    public virtual async DmTask<object> spawn_meat(mob_living victim = null, Type meat_type = null, DmList diseases = null)
    {
        meat_type ??= typeof(obj_item_food_meat_slab);
        obj_item_food_meat meat = Dm.@new(meat_type, this, blood_dna_info) as obj_item_food_meat;
        meat.name = I18n.T("[]'s []", victim.real_name, meat.name);
        await meat.set_custom_materials(Dm.list(Dm.kv(await SSmaterials.get_material(typeof(datum_material_meat_mob_meat), victim), 4 * SHEET_MATERIAL_AMOUNT)));
        if (meat is not obj_item_food_meat)
            return null;
        meat.subjectname = Dm.str(victim.real_name);
        meat.subjectjob = victim.job;
        if (Dm.truthy(Dm.length(diseases)))
            await meat.AddComponent(typeof(datum_component_infective), diseases);
        return meat;
    }

    // tg: code/modules/food_and_drinks/machinery/gibber.dm:286
    public virtual async DmTask process_results(mob_living victim = null, DmList results = null)
    {
        float reagents_in_produced = 0;
        foreach (obj_item result in Dm.each_cast<obj_item>(results))
        {
            if (result.reagents != null)
                reagents_in_produced += 1;
        }

        foreach (obj_item result in Dm.each_cast<obj_item>(results))
        {
            if (victim.reagents != null)
                await ((datum_reagents)victim.reagents).trans_to(result, Dm.num(Dm.div(((datum_reagents)victim.reagents).total_volume, reagents_in_produced)), remove_blacklisted: TRUE);
            if (result.reagents != null)
                await ((datum_reagents)result.reagents).add_reagent(typeof(datum_reagent_consumable_nutriment_fat), Dm.div(Dm.div(victim.nutrition, Dm.initial(typeof(datum_reagent_consumable_nutriment_fat), (datum_reagent_consumable r) => r.nutriment_factor)), reagents_in_produced));
        }
    }

    // tg: code/modules/food_and_drinks/machinery/gibber.dm:297
    public virtual async DmTask finish_gibbing(DmList results = null, object gibs_type = null, DmList diseases = null)
    {
        playsound(this.loc, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
        operating = FALSE;
        if (!Dm.truthy(dirty) && prob(50))
            dirty = TRUE;

        if (Dm.truthy(blood_dna_info))
            await add_blood_DNA(blood_dna_info as DmList);

        turf our_turf = get_turf(this);
        DmList nearby_turfs = Dm.sub(RANGE_TURFS(3, our_turf), our_turf) as DmList;
        foreach (obj_item result in Dm.each_cast<obj_item>(results))
        {
            await result.forceMove(our_turf);
            await result.throw_at(pick(nearby_turfs) as atom, rand(1, Dm.length(results)), 1);
        }

        if (!Dm.truthy(gibs_type))
        {
            pixel_x = base_pixel_x;
            update_appearance();
            return;
        }

        obj_effect_gibspawner spawner = Dm.@new((Type)gibs_type) as obj_effect_gibspawner;
        DmList gibs = spawner.gibtypes;
        if (!Dm.truthy(Dm.length(gibs)))
        {
            pixel_x = base_pixel_x;
            update_appearance();
            return;
        }

        for (float i = 1, i__end = Dm.length(results); i <= i__end; i++)
        {
            object gib_dir = pick(GLOB.alldirs);
            turf gib_turf = get_step(this, Dm.num(gib_dir));
            if (Dm.truthy(await gib_turf.is_blocked_turf(TRUE)))
                continue;
            object gib_type = pick(gibs);
            obj_effect_decal_cleanable_blood_gibs gib = Dm.@new((Type)gib_type, gib_turf, diseases, blood_dna_info) as obj_effect_decal_cleanable_blood_gibs;
            await gib.streak(Dm.list(gib_dir)); // tg passes the one direction, which streak picks from (pick() of one value is that value)
        }

        pixel_x = base_pixel_x;
        update_appearance();
    }

    // tg: code/modules/food_and_drinks/machinery/gibber.dm:349
    [SignalHandler]
    public virtual object on_cleaned(obj source_component = null, obj source = null)
    {
        object dot = NONE;

        dirty = FALSE;
        update_appearance(UPDATE_OVERLAYS);
        Dm.combine(ref dot, COMPONENT_CLEANED | COMPONENT_CLEANED_GAIN_XP);
        return dot;
    }
}

//auto-gibs anything that bumps into it
// tg: code/modules/food_and_drinks/machinery/gibber.dm:337
public partial class obj_machinery_gibber_autogibber : obj_machinery_gibber
{
    // tg: code/modules/food_and_drinks/machinery/gibber.dm:340
    public override object Bumped(atom_movable AM = null)
    {
        atom input = get_step(this, input_dir);
        if (isliving(AM))
        {
            mob_living victim = (mob_living)AM;

            if (victim.loc == input)
            {
                Waiting.NoSleep(victim.forceMove(this));
                Waiting.NoSleep(victim.gib(DROP_ALL_REMAINS));
            }
        }
        return null;
    }
}
