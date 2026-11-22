// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/machinery/portable_chem_mixer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P5): the portable chemical mixer, the belt that dispenses from the containers locked inside it, with its
// PortableChemMixer window. Its screentips (add_context) are a live-partial row (D264); the var block is generated.

// tg: code/modules/reagents/chemistry/machinery/portable_chem_mixer.dm:1
public partial class obj_item_storage_portable_chem_mixer : obj_item_storage
{
    // tg: code/modules/reagents/chemistry/machinery/portable_chem_mixer.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        Waiting.NoSleep(AddElement(typeof(datum_element_drag_pickup)));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/portable_chem_mixer.dm:29
    public override object Destroy(float? force = null)
    {
        dispensable_reagents.Cut();
        QDEL_NULL(ref beaker);
        return base.Destroy(force);
    }

    // tg: code/modules/reagents/chemistry/machinery/portable_chem_mixer.dm:51
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!Dm.truthy(atom_storage.locked))
            Dm.append(dot, span_notice(I18n.T("Use [] to lock in order to use its interface.", EXAMINE_HINT("Ctrl Click"))));
        else
            Dm.append(dot, span_notice(I18n.T("Its storage is locked, use [] to unlock it.", EXAMINE_HINT("Ctrl Click"))));
        if (QDELETED(beaker))
            Dm.append(dot, span_notice(I18n.T("A beaker can be inserted to dispense reagents after it is locked.")));
        else
        {
            Dm.append(dot, span_notice(I18n.T("A beaker of []u capacity is inserted.", (beaker.reagents as datum_reagents).maximum_volume)));
            Dm.append(dot, span_notice(I18n.T("It can be ejected with [].", EXAMINE_HINT("Alt Click"))));
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/portable_chem_mixer.dm:63
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (!Dm.truthy(atom_storage.locked))
        {
            icon_state = "portablechemicalmixer_open";
            return await base.update_icon_state(__extra);
        }
        if (!QDELETED(beaker))
        {
            icon_state = "portablechemicalmixer_full";
            return await base.update_icon_state(__extra);
        }
        icon_state = "portablechemicalmixer_empty";
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/reagents/chemistry/machinery/portable_chem_mixer.dm:73
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        if (!Dm.truthy(atom_storage.locked))
            update_contents();
        return dot;
    }

    /// Reload dispensable reagents from new contents
    // tg: code/modules/reagents/chemistry/machinery/portable_chem_mixer.dm:79
    public virtual void update_contents()
    {
        dispensable_reagents.Cut();
        foreach (obj_item_reagent_containers container in Dm.each<obj_item_reagent_containers>(contents))
        {
            datum_reagent key = (container.reagents as datum_reagents).get_master_reagent() as datum_reagent;
            if (key == null) //no reagent inside container
                continue;

            Type key_type = key.type;
            if (!Dm.@in(key_type, dispensable_reagents))
            {
                dispensable_reagents[key_type] = Dm.list();
                ((DmList)dispensable_reagents[key_type])["reagents"] = Dm.list();
            }
            Dm.append((DmList)((DmList)dispensable_reagents[key_type])["reagents"], container.reagents);
        }
    }

    // tg: code/modules/reagents/chemistry/machinery/portable_chem_mixer.dm:94
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == beaker)
            beaker = null;
        else
            update_contents();
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/portable_chem_mixer.dm:101
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        return Dm.num(severity) > EXPLODE_LIGHT ? base.ex_act(severity, target, __extra) : FALSE;
    }

    // tg: code/modules/reagents/chemistry/machinery/portable_chem_mixer.dm:104
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(atom_storage.locked) ||
            Dm.truthy(Dm.bitand(tool.item_flags, ABSTRACT)) ||
            Dm.truthy(Dm.bitand(tool.flags_1, HOLOGRAM_1)) ||
            !is_reagent_container(tool) ||
            !Dm.truthy(tool.is_open_container()))
            return NONE; // continue with regular storage handling

        await replace_beaker(user, tool as obj_item_reagent_containers);
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    /**
     * Replaces the beaker of the portable chemical mixer with another beaker, or simply adds the new beaker if none is in currently
     *
     * Checks if a valid user and a valid new beaker exist and attempts to replace the current beaker in the portable chemical mixer with the one in hand. Simply places the new beaker in if no beaker is currently loaded
     * Arguments:
     * * mob/living/user - The user who is trying to exchange beakers
     * * obj/item/reagent_containers/new_beaker - The new beaker that the user wants to put into the device
     */
    // tg: code/modules/reagents/chemistry/machinery/portable_chem_mixer.dm:125
    public virtual async DmTask replace_beaker(mob_living user = null, obj_item_reagent_containers new_beaker = null)
    {
        if (!QDELETED(beaker))
            await user.put_in_hands(beaker);

        if (!QDELETED(new_beaker))
        {
            if (!Dm.truthy(await user.transferItemToLoc(new_beaker, this, silent: FALSE)))
                return;
            beaker = new_beaker;
        }
    }

    // tg: code/modules/reagents/chemistry/machinery/portable_chem_mixer.dm:136
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (loc != user)
            return UI_CLOSE;
        return await base.ui_status(user, state);
    }

    // tg: code/modules/reagents/chemistry/machinery/portable_chem_mixer.dm:141
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (!Dm.truthy(atom_storage.locked))
        {
            balloon_alert(user, "lock it first!");
            return null;
        }

        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "PortableChemMixer", name);
            await ui.open();
        }

        float is_hallucinating = FALSE;
        if (isliving(user))
        {
            mob_living living_user = (mob_living)user;
            is_hallucinating = Dm.truthy(living_user.has_status_effect(typeof(datum_status_effect_hallucination))) ? TRUE : FALSE;
        }
        ui.set_autoupdate(!Dm.truthy(is_hallucinating) ? TRUE : FALSE); // to not ruin the immersion by constantly changing the fake chemicals
        return null;
    }

    // tg: code/modules/reagents/chemistry/machinery/portable_chem_mixer.dm:157
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list();
        dot["amount"] = amount;

        float is_hallucinating = FALSE;
        if (isliving(user))
        {
            mob_living living_user = (mob_living)user;
            is_hallucinating = Dm.truthy(living_user.has_status_effect(typeof(datum_status_effect_hallucination))) ? TRUE : FALSE;
        }

        dot["chemicals"] = Dm.list();
        foreach (Type reagent_type in Dm.each_cast<Type>(dispensable_reagents))
        {
            // var/datum/reagent/temp = GLOB.chemical_reagents_list[reagent_type]: the type's prototype stands in (holder.cs's GAP)
            datum_reagent temp = ispath(reagent_type, typeof(datum_reagent)) && reagent_type != typeof(datum_reagent) && !Dm.@in(reagent_type, get_abstract_types()) ? Dm.initial(reagent_type, (datum_reagent p) => p) : null;
            if (temp != null)
            {
                object chemname = temp.name;
                float total_volume = 0;
                float total_ph = 0;
                foreach (datum_reagents rs in Dm.each_cast<datum_reagents>(((DmList)dispensable_reagents[reagent_type])["reagents"]))
                {
                    total_volume += rs.total_volume;
                    total_ph = rs.ph;
                }
                if (Dm.truthy(is_hallucinating) && prob(5))
                    chemname = Dm.text("[]", pick_list_replacements("hallucination.json", "chemicals"));
                Dm.append((DmList)dot["chemicals"], Dm.list(Dm.list(Dm.kv("title", chemname), Dm.kv("id", temp.name), Dm.kv("volume", total_volume), Dm.kv("pH", total_ph))));
            }
        }

        DmList beaker_data = null;
        if (!QDELETED(beaker))
        {
            datum_reagents r = beaker.reagents as datum_reagents;
            beaker_data = Dm.list();
            beaker_data["maxVolume"] = beaker.volume;
            beaker_data["transferAmounts"] = beaker.possible_transfer_amounts;
            beaker_data["pH"] = round(r.ph, 0.01f);
            beaker_data["currentVolume"] = r.total_volume;
            DmList beakerContents = Dm.list();
            if (Dm.truthy(length(r.reagent_list)))
            {
                foreach (datum_reagent reagent in Dm.each<datum_reagent>(r.reagent_list))
                    Dm.append(beakerContents, Dm.list(Dm.list(Dm.kv("name", reagent.name), Dm.kv("volume", round(Dm.num(reagent.volume), CHEMICAL_VOLUME_ROUNDING))))); // list in a list because Byond merges the first list...
            }
            beaker_data["contents"] = beakerContents;
        }
        dot["beaker"] = beaker_data;
        await DmTask.CompletedTask;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/portable_chem_mixer.dm:194
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "amount":
            {
                object target = @params["target"];
                if (target == null)
                    return null;

                float? number = text2num(target);
                if (number == null)
                    return null;

                amount = round(number.Value, CHEMICAL_VOLUME_ROUNDING);
                return TRUE;
            }

            case "dispense":
            {
                Type reagent = GLOB.name2reagent[@params["reagent"]] as Type;
                if (reagent == null)
                    return null;

                if (!QDELETED(beaker))
                {
                    datum_reagents container = beaker.reagents as datum_reagents;
                    float actual = Dm.min(Dm.num(amount), container.maximum_volume - container.total_volume);
                    foreach (datum_reagents source in Dm.each_cast<datum_reagents>(((DmList)dispensable_reagents[reagent])?["reagents"]))
                    {
                        actual -= Dm.num(await source.trans_to(beaker, Dm.min(source.total_volume, actual), transferred_by: ui.user));
                        if (actual <= 0)
                            break;
                    }
                    return TRUE;
                }
                break;
            }

            case "remove":
            {
                object target = @params["amount"];
                if (target == null)
                    return null;

                float? number = text2num(target);
                if (number == null)
                    return null;

                await (beaker.reagents as datum_reagents).remove_all(number);
                return TRUE;
            }

            case "eject":
                await replace_beaker(ui.user as mob_living);
                update_appearance();
                return TRUE;
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/portable_chem_mixer.dm:243
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(atom_storage.locked))
        {
            balloon_alert(user, "lock first to use alt eject!");
            return CLICK_ACTION_BLOCKING;
        }

        await replace_beaker(user as mob_living);
        update_appearance();
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/reagents/chemistry/machinery/portable_chem_mixer.dm:252
    public override async DmTask<object> item_ctrl_click(mob user = null)
    {
        if (Dm.num(atom_storage.locked) == STORAGE_FULLY_LOCKED)
        {
            await replace_beaker(user as mob_living);
            SStgui.close_uis(this);
        }
        atom_storage.set_locked(Dm.truthy(atom_storage.locked) ? STORAGE_NOT_LOCKED : STORAGE_FULLY_LOCKED);
        return CLICK_ACTION_SUCCESS;
    }
}
