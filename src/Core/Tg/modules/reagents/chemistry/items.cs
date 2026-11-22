// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the pH booklet and paper (M8, moved here from medigel.cs by gaps P5), the chemical analyzer (the pH meter), the
// burners and the thermometer with its Thermometer window (gaps P5).

/*
* a pH booklet that contains pH paper pages that will change color depending on the pH of the reagents datum it's attacked onto
*/
// tg: code/modules/reagents/chemistry/items.dm:10
public partial class obj_item_ph_booklet : obj_item
{
    //A little janky with pockets
    // tg: code/modules/reagents/chemistry/items.dm:24
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.get_held_index_of_item(this))) //Does this check pockets too..?
        {
            if (Dm.eq(number_of_pages, 50))
                icon_state = "pHbooklet_open";
            if (!Dm.truthy(number_of_pages))
            {
                to_chat(user, span_warning(I18n.T("[] is empty!", this)));
                add_fingerprint(user);
                return null;
            }
            obj_item_ph_paper page = Dm.@new<obj_item_ph_paper>(get_turf(user));
            page.add_fingerprint(user);
            await user.put_in_active_hand(page);
            to_chat(user, span_notice(I18n.T("You take [] out of \\the [].", page, this)));
            number_of_pages = Dm.num(number_of_pages) - 1;
            playsound(user.loc, Dm.resource("sound/items/poster/poster_ripped.ogg"), 50, TRUE);
            add_fingerprint(user);
            if (!Dm.truthy(number_of_pages))
                icon_state = "pHbooklet_empty";
            return null;
        }
        object I = user.get_active_held_item();
        if (I == null)
            await user.put_in_active_hand(this);
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/reagents/chemistry/items.dm:47
    public override async DmTask<object> mouse_drop_dragged(atom over = null, mob user = null, object src_location = null, object over_location = null, object @params = null)
    {
        if (!isliving(user))
            return null;
        if (!Dm.truthy(number_of_pages))
        {
            to_chat(user, span_warning(I18n.T("[] is empty!", this)));
            add_fingerprint(user);
            return null;
        }
        if (Dm.eq(number_of_pages, 50))
            icon_state = "pHbooklet_open";
        obj_item_ph_paper P = Dm.@new<obj_item_ph_paper>(get_turf(user));
        P.add_fingerprint(user);
        await user.put_in_active_hand(P);
        to_chat(user, span_notice(I18n.T("You take [] out of \\the [].", P, this)));
        number_of_pages = Dm.num(number_of_pages) - 1;
        playsound(user.loc, Dm.resource("sound/items/poster/poster_ripped.ogg"), 50, TRUE);
        add_fingerprint(user);
        if (!Dm.truthy(number_of_pages))
            icon_state = "pHbooklet_empty";
        return null;
    }
}

// tg: code/modules/reagents/chemistry/items.dm:70
public partial class obj_item_ph_paper : obj_item
{
    // tg: code/__HELPERS/reagents.dm:118  CONVERT_PH_TO_COLOR(pH, color)
    private static string ph_color(float pH)
    {
        if (pH >= 14) return "#462c83";
        if (pH >= 13 && pH <= 14) return "#63459b";
        if (pH >= 12 && pH <= 13) return "#5a51a2";
        if (pH >= 11 && pH <= 12) return "#3853a4";
        if (pH >= 10 && pH <= 11) return "#3f93cf";
        if (pH >= 9 && pH <= 10) return "#0bb9b7";
        if (pH >= 8 && pH <= 9) return "#23b36e";
        if (pH >= 7 && pH <= 8) return "#3aa651";
        if (pH >= 6 && pH <= 7) return "#4cb849";
        if (pH >= 5 && pH <= 6) return "#b5d335";
        if (pH >= 4 && pH <= 5) return "#f7ec1e";
        if (pH >= 3 && pH <= 4) return "#fbc314";
        if (pH >= 2 && pH <= 3) return "#f26724";
        if (pH >= 1 && pH <= 2) return "#ef1d26";
        return "#c6040c";
    }

    // tg: code/modules/reagents/chemistry/items.dm:81
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!is_reagent_container(interacting_with))
            return null;
        obj_item_reagent_containers cont = interacting_with as obj_item_reagent_containers;
        datum_reagents holder = cont.reagents as datum_reagents;
        if (!Dm.truthy(length(holder?.reagent_list)))
            return NONE;
        if (Dm.truthy(used))
        {
            to_chat(user, span_warning(I18n.T("[] has already been used!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        float rounded_ph = round(Dm.num(holder.ph), 1);
        color = ph_color(rounded_ph);
        desc = Dm.text("[] The paper looks to be around a pH of []", desc, rounded_ph);
        name = Dm.text("used []", name);
        used = TRUE;
        return ITEM_INTERACT_SUCCESS;
    }
}

/*
* pH meter that will give a detailed or truncated analysis of all the reagents in of an object with a reagents datum attached to it. Only way of detecting purity for now.
*/
// tg: code/modules/reagents/chemistry/items.dm:99
public partial class obj_item_ph_meter : obj_item
{
    // tg: code/modules/reagents/chemistry/items.dm:2  #define DETAILED_CHEM_OUTPUT 1, :4 SHORTENED_CHEM_OUTPUT 0 (file-local)
    private const float DETAILED_CHEM_OUTPUT = 1;
    private const float SHORTENED_CHEM_OUTPUT = 0;

    // tg: code/modules/reagents/chemistry/items.dm:109
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (scanmode == SHORTENED_CHEM_OUTPUT)
        {
            to_chat(user, span_notice(I18n.T("You switch the chemical analyzer to provide a detailed description of each reagent.")));
            scanmode = DETAILED_CHEM_OUTPUT;
        }
        else
        {
            to_chat(user, span_notice(I18n.T("You switch the chemical analyzer to not include reagent descriptions in its report.")));
            scanmode = SHORTENED_CHEM_OUTPUT;
        }
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/modules/reagents/chemistry/items.dm:117
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!is_reagent_container(interacting_with))
            return NONE;
        obj_item_reagent_containers cont = interacting_with as obj_item_reagent_containers;
        datum_reagents holder = cont.reagents as datum_reagents;
        if (!Dm.truthy(LAZYLEN(holder.reagent_list)))
            return NONE;
        if (Dm.truthy(interacting_with._listen_lookup?[COMSIG_ON_REAGENT_SCAN]))
            interacting_with._SendSignal(COMSIG_ON_REAGENT_SCAN, Dm.list(interacting_with, user)); // SEND_SIGNAL(interacting_with, COMSIG_ON_REAGENT_SCAN, user)
        DmList out_message = Dm.list();
        to_chat(user, "<i>The chemistry meter beeps and displays:</i>");
        Dm.append(out_message, Dm.text("<b>Total volume: [] Current temperature: []K Total pH: []\n", round(Dm.num(cont.volume), 0.01f), round(holder.chem_temp, 0.1f), round(holder.ph, 0.01f)));
        Dm.append(out_message, Dm.text("Chemicals found in []:</b>\n", interacting_with.name));
        if (Dm.truthy(holder.is_reacting))
            Dm.append(out_message, Dm.text("[]<span class='notice'>\n", span_warning("A reaction appears to be occuring currently.")));
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(holder.reagent_list)) // bloodtyping if blood present in container
        {
            object blood_info = null;
            if (Dm.truthy(reagent.data))
            {
                object blood = (reagent.data as DmList)[BLOOD_DATA_TYPE];
                if (istype(blood, typeof(datum_blood_type)))
                {
                    datum_blood_type blood_type = blood as datum_blood_type;
                    object type = blood_type.get_type();
                    blood_info = Dm.text("[] []", blood_type.get_blood_name(), Dm.truthy(type) ? Dm.text("(type: [])", type) : "");
                }
                else if (Dm.truthy(blood))
                    blood_info = Dm.text("[] (type: [])", reagent.name, blood);
            }
            if (reagent.purity < Dm.num(reagent.inverse_chem_val) && reagent.inverse_chem != null) //If the reagent is impure
            {
                // var/datum/reagent/inverse_reagent = GLOB.chemical_reagents_list[reagent.inverse_chem]: the type's prototype stands
                // in (holder.cs's GAP; only its name, overdose and initial pH are read)
                datum_reagent inverse_reagent = Dm.initial(reagent.inverse_chem as Type, (datum_reagent p) => p);
                Dm.append(out_message, Dm.text("[]<span class='notice'><b>[]u of []</b>, <b>Purity:</b> []%, []\n", span_warning("Inverted reagent detected: "), round(Dm.num(reagent.volume), 0.01f), inverse_reagent.name, round(1 - reagent.purity, 0.000001f) * 100, Dm.truthy(scanmode) ? Dm.text("[]<b>Base pH:</b> [], <b>Current pH:</b> [].", Dm.truthy(inverse_reagent.overdose_threshold) ? Dm.text("<b>Overdose:</b> []u, ", inverse_reagent.overdose_threshold) : "", Dm.initial(reagent.inverse_chem as Type, (datum_reagent r) => r.ph), reagent.ph) : Dm.text("<b>Current pH:</b> [].", reagent.ph)));
            }
            else
                Dm.append(out_message, Dm.text("<b>[]u of []</b>, <b>Purity:</b> []%, []\n", round(Dm.num(reagent.volume), CHEMICAL_VOLUME_ROUNDING), Dm.truthy(blood_info) ? blood_info : reagent.name, round(reagent.purity, 0.000001f) * 100, Dm.truthy(scanmode) ? Dm.text("[]<b>Base pH:</b> [], <b>Current pH:</b> [].", Dm.truthy(reagent.overdose_threshold) ? Dm.text("<b>Overdose:</b> []u, ", reagent.overdose_threshold) : "", Dm.initial(reagent, (datum_reagent r) => r.ph), reagent.ph) : Dm.text("<b>Current pH:</b> [].", reagent.ph)));
            if (Dm.truthy(scanmode))
                Dm.append(out_message, Dm.text("<b>Analysis:</b> []\n", reagent.description));
        }
        to_chat(user, boxed_message(span_notice(Dm.text("[]", Dm.jointext(out_message, "")))));
        desc = Dm.text("An electrode attached to a small circuit box that will display details of a solution. Can be toggled to provide a description of each of the reagents. The screen currently displays detected vol: [] detected pH:[].", round(Dm.num(cont.volume), 0.01f), round(holder.ph, 0.1f));
        await DmTask.CompletedTask;
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/reagents/chemistry/items.dm:152
public partial class obj_item_burner : obj_item
{
    // tg: code/modules/reagents/chemistry/items.dm:168
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(max_volume, TRANSPARENT); //We have our own refillable - since we want to heat and pour
        if (reagent_type != null)
            Waiting.NoSleep((reagents as datum_reagents).add_reagent(reagent_type, 15));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/items.dm:174
    public override async DmTask<object> grind_results()
    {
        await DmTask.CompletedTask;
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_ethanol), 5), Dm.kv(typeof(datum_reagent_silicon), 10));
    }

    // tg: code/modules/reagents/chemistry/items.dm:177
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!is_reagent_container(tool))
        {
            if (Dm.num(tool.heat) >= 1000)
            {
                await set_lit(TRUE);
                user.visible_message(span_notice(I18n.T("[] lights up the [].", user, this)));
                return ITEM_INTERACT_SUCCESS;
            }
            return NONE;
        }

        if (Dm.truthy(lit))
        {
            await (tool.reagents as datum_reagents).expose_temperature(get_temperature());
            to_chat(user, span_notice(I18n.T("You heat up the [] with the [].", tool, this)));
            playsound(user.loc, Dm.resource("sound/effects/chemistry/heatdam.ogg"), 50, TRUE);
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(tool.is_drainable())) //Transfer FROM it TO us. Special code so it only happens when flame is off.
        {
            obj_item_reagent_containers container = tool as obj_item_reagent_containers;
            datum_reagents container_reagents = container.reagents as datum_reagents;
            if (!Dm.truthy(container_reagents.total_volume))
            {
                to_chat(user, span_warning(I18n.T("[] is empty and can't be poured!", container)));
                return ITEM_INTERACT_BLOCKING;
            }

            if (Dm.truthy((reagents as datum_reagents).holder_full()))
            {
                to_chat(user, span_warning(I18n.T("[] is full.", this)));
                return ITEM_INTERACT_BLOCKING;
            }

            object trans = await container_reagents.trans_to(this, Dm.num(container.amount_per_transfer_from_this), transferred_by: user);
            to_chat(user, span_notice(I18n.T("You fill [] with [] unit[] of the contents of [].", this, trans, Dm.num(trans) == 1 ? "" : "s", container)));
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/reagents/chemistry/items.dm:207
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(lit))
            return NONE;

        if (is_reagent_container(interacting_with))
        {
            obj_item_reagent_containers container = interacting_with as obj_item_reagent_containers;
            await (container.reagents as datum_reagents).expose_temperature(get_temperature());
            user.visible_message(span_notice(I18n.T("[] heats up [].", user, this)), span_notice(I18n.T("You heat up [].", this)));
            playsound(user, Dm.resource("sound/effects/chemistry/heatdam.ogg"), 50, TRUE);
            return ITEM_INTERACT_SUCCESS;
        }

        else if (isitem(interacting_with))
        {
            obj_item item = interacting_with as obj_item;
            if (Dm.num(item.get_temperature()) >= FIRE_MINIMUM_TEMPERATURE_TO_EXIST)
            {
                await set_lit(TRUE);
                user.visible_message(span_notice(I18n.T("[] lights up [].", user, this)), span_notice(I18n.T("You light up [].", this)));
                return ITEM_INTERACT_SUCCESS;
            }
        }

        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/reagents/chemistry/items.dm:227
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", Dm.initial(this, (atom p) => p.icon_state), Dm.truthy(lit) ? "-on" : "");
        return dot;
    }

    // tg: code/modules/reagents/chemistry/items.dm:231
    public virtual async DmTask set_lit(object new_lit = null)
    {
        if (Dm.eq(lit, new_lit))
            return;
        lit = Dm.num(new_lit);
        if (Dm.truthy(lit))
        {
            force = 5;
            damtype = BURN;
            hitsound = Dm.resource("sound/items/tools/welder.ogg");
            attack_verb_continuous = string_list(Dm.list("burns", "singes")) as DmList;
            attack_verb_simple = string_list(Dm.list("burn", "singe")) as DmList;
            START_PROCESSING(SSobj, this);
        }
        else
        {
            hitsound = SFX_SWING_HIT;
            force = 0;
            attack_verb_continuous = null; //human_defense.dm takes care of it
            attack_verb_simple = null;
            STOP_PROCESSING(SSobj, this);
        }
        await set_light_on(lit);
        await update_icon();
    }

    // tg: code/modules/reagents/chemistry/items.dm:251
    public override async DmTask<object> extinguish()
    {
        object dot = await base.extinguish();
        await set_lit(FALSE);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/items.dm:255
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(lit))
        {
            await set_lit(FALSE);
            user.visible_message(span_notice(I18n.T("[] snuffs out []'s flame.", user, this)));
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/items.dm:263
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob_living M = target_mob;
        if (Dm.truthy(lit) && Dm.truthy(M.ignite_mob()))
        {
            message_admins(Dm.text("[] set [] on fire with [] at []", ADMIN_LOOKUPFLW(user), key_name_admin(M), this, AREACOORD(user)));
            user.log_message(Dm.text("set [] on fire with []", key_name(M), this), LOG_GAME);
            M.log_message(Dm.text("was set on fire by [] with []", key_name(user), this), LOG_VICTIM, log_globally: FALSE);
        }
        return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
    }

    // tg: code/modules/reagents/chemistry/items.dm:270
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        float current_heat = 0;
        float number_of_burning_reagents = 0;
        datum_reagents holder = reagents as datum_reagents;
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(holder.reagent_list))
        {
            Waiting.NoSleep(reagent.burn(holder)); //burn can set temperatures of reagents
            if (reagent.burning_temperature != null)
            {
                current_heat += Dm.num(reagent.burning_temperature);
                number_of_burning_reagents += 1;
                Waiting.NoSleep(holder.remove_reagent(reagent.type, reagent.burning_volume));
                continue;
            }
        }

        if (!Dm.truthy(number_of_burning_reagents))
        {
            Waiting.NoSleep(set_lit(FALSE));
            heat = 0;
            return null;
        }
        open_flame();
        current_heat /= number_of_burning_reagents;
        heat = current_heat;
        return null;
    }

    // tg: code/modules/reagents/chemistry/items.dm:289
    public override object get_temperature()
    {
        return lit * Dm.num(heat);
    }
}

// tg: code/modules/reagents/chemistry/items.dm:293
public partial class obj_item_burner_oil : obj_item_burner
{
    // tg: code/modules/reagents/chemistry/items.dm:295
    public override async DmTask<object> grind_results()
    {
        await DmTask.CompletedTask;
        return Dm.list(Dm.kv(typeof(datum_reagent_fuel_oil), 5), Dm.kv(typeof(datum_reagent_silicon), 10));
    }
}

// tg: code/modules/reagents/chemistry/items.dm:299
public partial class obj_item_burner_fuel : obj_item_burner
{
    // tg: code/modules/reagents/chemistry/items.dm:301
    public override async DmTask<object> grind_results()
    {
        await DmTask.CompletedTask;
        return Dm.list(Dm.kv(typeof(datum_reagent_fuel), 5), Dm.kv(typeof(datum_reagent_silicon), 10));
    }
}

// tg: code/modules/reagents/chemistry/items.dm:305
public partial class obj_item_thermometer : obj_item
{
    // tg: code/modules/reagents/chemistry/items.dm:315
    public override object Destroy(float? force = null)
    {
        attached_to_reagents = null;
        return base.Destroy(force);
    }

    // tg: code/modules/reagents/chemistry/items.dm:319
    public override async DmTask<object> grind_results()
    {
        await DmTask.CompletedTask;
        return Dm.list(Dm.kv(typeof(datum_reagent_mercury), 5));
    }

    // tg: code/modules/reagents/chemistry/items.dm:322
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (interacting_with.reagents == null)
            return NONE;
        if (!Dm.truthy(await user.transferItemToLoc(this, interacting_with)))
            return ITEM_INTERACT_BLOCKING;
        attached_to_reagents = interacting_with.reagents as datum_reagents;
        to_chat(user, span_notice(I18n.T("You add the [] to [].", this, interacting_with)));
        await ui_interact(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/chemistry/items.dm:332
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Thermometer", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/reagents/chemistry/items.dm:338
    public override void ui_close(mob user = null)
    {
        base.ui_close(user);
        Waiting.Detach(remove_thermometer(user)); // INVOKE_ASYNC(src, PROC_REF(remove_thermometer), user)
    }

    // tg: code/modules/reagents/chemistry/items.dm:342
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        await DmTask.CompletedTask;
        if (!Dm.truthy(in_range(this, user)))
            return UI_CLOSE;
        return UI_INTERACTIVE;
    }

    // tg: code/modules/reagents/chemistry/items.dm:347
    public override object ui_state(mob user = null)
    {
        return GLOB.physical_state;
    }

    // tg: code/modules/reagents/chemistry/items.dm:350
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        if (attached_to_reagents == null)
        {
            ui_close(user);
            return null;
        }

        DmList data = Dm.list();
        data["Temperature"] = round(attached_to_reagents.chem_temp);
        return data;
    }

    // tg: code/modules/reagents/chemistry/items.dm:359
    public virtual async DmTask remove_thermometer(mob target = null)
    {
        await try_put_in_hand(this, target as mob_living);
        attached_to_reagents = null;
    }

    // tg: code/modules/reagents/chemistry/items.dm:363
    public virtual async DmTask try_put_in_hand(obj @object = null, mob_living user = null)
    {
        to_chat(user, span_notice(I18n.T("You remove the [] from [].", this, attached_to_reagents?.my_atom)));
        if (!issilicon(user) && Dm.truthy(in_range(loc, user)))
            await user.put_in_hands(@object as obj_item);
        else
            await @object.forceMove(drop_location() as atom);
    }
}
