// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/robot/items/hypo.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326): the cyborgs' hyposprays, shakers and condiment synthesizer, their windows BorgHypo and
// BorgShaker. The reagent lists (BASE_MEDICAL_REAGENTS and the rest) are expanded in the generated var blocks (D111).

///Borg Hypospray
// tg: code/game/objects/items/robot/items/hypo.dm:105
public partial class obj_item_reagent_containers_borghypo : obj_item_reagent_containers
{
    internal const string REAGENT_CONTAINER_INTERNAL = "internal_beaker";
    internal const string REAGENT_CONTAINER_BEVAPPARATUS = "beverage_apparatus";

    // tg: code/game/objects/items/robot/items/hypo.dm:144
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        stored_reagents = Dm.@new<datum_reagents>(Dm.named("new_flags", NO_REACT));
        stored_reagents.maximum_volume = length(default_reagent_types) * (max_volume_per_reagent + 1);
        foreach (object reagent in Dm.each(default_reagent_types))
            Waiting.NoSleep(add_new_reagent(reagent));
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/game/objects/items/robot/items/hypo.dm:152
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    /// Every [recharge_time] seconds, recharge some reagents for the cyborg
    // tg: code/game/objects/items/robot/items/hypo.dm:157
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        charge_timer += Dm.num(seconds_per_tick);
        if (charge_timer >= recharge_time)
        {
            Waiting.NoSleep(regenerate_reagents(default_reagent_types));
            if (Dm.truthy(upgraded))
                Waiting.NoSleep(regenerate_reagents(expanded_reagent_types));
            charge_timer = 0;
        }
        return 1;
    }

    /// Use this to add more chemicals for the borghypo to produce.
    // tg: code/game/objects/items/robot/items/hypo.dm:167
    public virtual async DmTask add_new_reagent(object reagent = null) // decided (Appendix A): tg's callers pass a typepath (hypo.dm:149)
    {
        await stored_reagents.add_reagent(reagent, max_volume_per_reagent + 1, reagtemp: dispensed_temperature, no_react: TRUE);
    }

    /// Regenerate our supply of all reagents (if they're not full already)
    // tg: code/game/objects/items/robot/items/hypo.dm:171
    public virtual async DmTask regenerate_reagents(DmList reagents_to_regen = null)
    {
        if (iscyborg(loc))
        {
            mob_living_silicon_robot cyborg = loc as mob_living_silicon_robot;
            if (cyborg?.cell is obj_item_stock_parts_power_store cyborg_cell)
            {
                foreach (object reagent in Dm.each(reagents_to_regen))
                {
                    object reagent_to_regen = reagent;
                    if (!Dm.truthy(stored_reagents.has_reagent(reagent_to_regen, max_volume_per_reagent)))
                    {
                        await cyborg_cell.use(charge_cost);
                        await stored_reagents.add_reagent(reagent_to_regen, 5, reagtemp: dispensed_temperature, no_react: TRUE);
                    }
                }
            }
        }
    }

    // tg: code/game/objects/items/robot/items/hypo.dm:181
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!iscarbon(interacting_with))
            return NONE;

        mob_living_carbon injectee = interacting_with as mob_living_carbon;
        if (selected_reagent == null)
        {
            balloon_alert(user, I18n.T("no reagent selected!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(stored_reagents.has_reagent(selected_reagent.GetType(), Dm.num(amount_per_transfer_from_this))))
        {
            balloon_alert(user, I18n.T("not enough []!", selected_reagent.name));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(injectee.try_inject(user, user.zone_selected, injection_flags: Dm.bitor(INJECT_TRY_SHOW_ERROR_MESSAGE, Dm.truthy(bypass_protection) ? INJECT_CHECK_PENETRATE_THICK : 0))))
        {
            balloon_alert(user, I18n.T("[] is blocked!", injectee.parse_zone_with_bodypart(user.zone_selected)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (injectee.reagents == null)
        {
            balloon_alert(user, I18n.T("unable to inject!"));
            return ITEM_INTERACT_BLOCKING;
        }

        to_chat(injectee, span_warning(I18n.T("You feel a tiny prick!")));
        to_chat(user, span_notice(I18n.T("You inject [] with the injector ([]).", injectee, selected_reagent.name)));
        user.changeNext_move(CLICK_CD_MELEE);
        await stored_reagents.trans_to(injectee, Dm.num(amount_per_transfer_from_this), target_id: selected_reagent.GetType(), transferred_by: user, methods: INJECT);
        balloon_alert(user, I18n.T("[] unit\\s injected", amount_per_transfer_from_this));
        log_combat(user, injectee, "injected", this, Dm.text("(CHEMICALS: [])", selected_reagent));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/robot/items/hypo.dm:210
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "BorgHypo", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/robot/items/hypo.dm:216
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList available_reagents = Dm.list();
        foreach (datum_reagent reagent in Dm.each<datum_reagent>(stored_reagents.reagent_list))
        {
            available_reagents.Add(Dm.list(Dm.list(
                Dm.kv("name", reagent.name),
                Dm.kv("volume", round(Dm.num(reagent.volume), 0.01f) - 1),
                Dm.kv("description", reagent.description)
            ))); // list in a list because Byond merges the first list...
        }

        DmList data = Dm.list();
        data["theme"] = tgui_theme;
        data["maxVolume"] = max_volume_per_reagent;
        data["reagents"] = available_reagents;
        data["selectedReagent"] = selected_reagent?.name;
        return data;
    }

    // tg: code/game/objects/items/robot/items/hypo.dm:232
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await ui_interact(user);
        return null;
    }

    // tg: code/game/objects/items/robot/items/hypo.dm:235
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        foreach (datum_reagent reagent in Dm.each<datum_reagent>(stored_reagents.reagent_list))
        {
            if (Dm.eq(reagent.name, action))
            {
                selected_reagent = reagent;
                dot = TRUE;
                mob_living_silicon_robot cyborg = loc as mob_living_silicon_robot;
                if (loc is obj_item_robot_model container_model)
                    cyborg = container_model.robot;
                playsound(cyborg, Dm.resource("sound/effects/pop.ogg"), 50, FALSE);
                balloon_alert(cyborg, I18n.T("dispensing []", selected_reagent.name));
                break;
            }
        }
        return dot;
    }

    // tg: code/game/objects/items/robot/items/hypo.dm:252
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, selected_reagent != null ? I18n.T("Currently loaded: []. []", selected_reagent, selected_reagent.description) : I18n.T("Currently loaded: nothing."));
        Dm.append(ref dot, span_notice(I18n.T("<i>Alt+Click</i> to change transfer amount. Currently set to []u.", amount_per_transfer_from_this)));
        return dot;
    }

    // tg: code/game/objects/items/robot/items/hypo.dm:257
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        change_transfer_amount(user);
        return CLICK_ACTION_SUCCESS;
    }
}

/// Default Medborg Hypospray
// tg: code/game/objects/items/robot/items/hypo.dm:262
public partial class obj_item_reagent_containers_borghypo_medical : obj_item_reagent_containers_borghypo
{
    /// Upgrade our hypospray to hold even more new reagents!
    // tg: code/game/objects/items/robot/items/hypo.dm:267
    public virtual async DmTask upgrade_hypo()
    {
        upgraded = TRUE;
        // Expand the holder's capacity to allow for our new suite of reagents
        stored_reagents.maximum_volume = Dm.num(stored_reagents.maximum_volume) + (length(expanded_reagent_types) * (max_volume_per_reagent + 1));
        foreach (object reagent in Dm.each(expanded_reagent_types))
            await add_new_reagent(reagent);
    }

    /// Remove the reagents we got from the expansion, back to our base reagents
    // tg: code/game/objects/items/robot/items/hypo.dm:276
    public virtual async DmTask remove_hypo_upgrade()
    {
        upgraded = FALSE;
        foreach (object reagent in Dm.each(expanded_reagent_types))
            await stored_reagents.del_reagent(reagent);
        // Reduce the holder's capacity because we no longer need the room for those reagents
        stored_reagents.maximum_volume = Dm.num(stored_reagents.maximum_volume) - (length(expanded_reagent_types) * (max_volume_per_reagent + 1));
    }
}

/// Borg Shaker for the serviceborgs
// tg: code/game/objects/items/robot/items/hypo.dm:326
public partial class obj_item_reagent_containers_borghypo_borgshaker : obj_item_reagent_containers_borghypo
{
    // tg: code/game/objects/items/robot/items/hypo.dm:341
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "BorgShaker", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/robot/items/hypo.dm:347
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        mob_living_silicon_robot user = usr as mob_living_silicon_robot;
        switch (action as string)
        {
            case "reaction_lookup":
                if (!iscyborg(usr))
                    return dot;
                if (Dm.eq(reagent_search_container, REAGENT_CONTAINER_BEVAPPARATUS))
                {
                    obj_item_borg_apparatus_beaker_service beverage_apparatus = Dm.locate<obj_item_borg_apparatus_beaker_service>(user.model.modules) ?? Dm.locate<obj_item_borg_apparatus_beaker_service>(user.held_items);
                    if (beverage_apparatus != null && beverage_apparatus.stored != null)
                        await (beverage_apparatus.stored.reagents as datum_reagents).ui_interact(user);
                }
                else if (Dm.eq(reagent_search_container, REAGENT_CONTAINER_INTERNAL))
                {
                    obj_item_reagent_containers_cup_beaker_large internal_beaker = Dm.locate<obj_item_reagent_containers_cup_beaker_large>(user.model.modules) ?? Dm.locate<obj_item_reagent_containers_cup_beaker_large>(user.held_items);
                    if (internal_beaker != null)
                        await (internal_beaker.reagents as datum_reagents).ui_interact(user);
                }
                break;
            case "set_preferred_container":
                reagent_search_container = Dm.str(@params["value"]);
                break;
        }
        return TRUE;
    }

    // tg: code/game/objects/items/robot/items/hypo.dm:368
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList drink_reagents = Dm.list();
        DmList alcohol_reagents = Dm.list();
        foreach (datum_reagent reagent in Dm.each<datum_reagent>(stored_reagents.reagent_list))
        {
            // Split the reagents into alcoholic/non-alcoholic
            if (reagent is datum_reagent_consumable_ethanol)
            {
                alcohol_reagents.Add(Dm.list(Dm.list(
                    Dm.kv("name", reagent.name),
                    Dm.kv("volume", round(Dm.num(reagent.volume), 0.01f) - 1)
                ))); // list in a list because Byond merges the first list...
            }
            else
            {
                drink_reagents.Add(Dm.list(Dm.list(
                    Dm.kv("name", reagent.name),
                    Dm.kv("volume", round(Dm.num(reagent.volume), 0.01f) - 1)
                )));
            }
        }

        DmList data = Dm.list();
        data["theme"] = tgui_theme;
        data["minVolume"] = amount_per_transfer_from_this;
        data["sodas"] = drink_reagents;
        data["alcohols"] = alcohol_reagents;
        data["selectedReagent"] = selected_reagent?.name;
        data["reagentSearchContainer"] = reagent_search_container;

        if (iscyborg(user))
        {
            mob_living_silicon_robot cyborg = user as mob_living_silicon_robot;
            obj_item_borg_apparatus_beaker_service beverage_apparatus = Dm.locate<obj_item_borg_apparatus_beaker_service>(cyborg.model.modules) ?? Dm.locate<obj_item_borg_apparatus_beaker_service>(cyborg.held_items);

            if (beverage_apparatus == null)
            {
                to_chat(user, span_warning(I18n.T("This unit has no beverage apparatus. This shouldn't be possible. Delete yourself, NOW!")));
                data["apparatusHasItem"] = FALSE;
            }
            else
                data["apparatusHasItem"] = beverage_apparatus.stored != null ? TRUE : FALSE;
        }
        return data;
    }

    // tg: code/game/objects/items/robot/items/hypo.dm:403
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return await pour_into(this, interacting_with, user);
    }

    // tg: code/game/objects/items/robot/items/hypo.dm:404  (the shaker's and the condiment synthesizer's identical pour)
    internal static async DmTask<object> pour_into(obj_item_reagent_containers_borghypo hypo, atom interacting_with, mob_living user)
    {
        if (!Dm.truthy(interacting_with.is_refillable()))
            return NONE;
        if (hypo.selected_reagent == null)
        {
            hypo.balloon_alert(user, I18n.T("no reagent selected!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(hypo.stored_reagents.has_reagent(hypo.selected_reagent.GetType(), Dm.num(hypo.amount_per_transfer_from_this))))
        {
            hypo.balloon_alert(user, I18n.T("not enough []!", hypo.selected_reagent.name));
            return ITEM_INTERACT_BLOCKING;
        }
        datum_reagents target_reagents = interacting_with.reagents as datum_reagents;
        if (Dm.num(target_reagents.total_volume) >= Dm.num(target_reagents.maximum_volume))
        {
            hypo.balloon_alert(user, I18n.T("it's full!"));
            return ITEM_INTERACT_BLOCKING;
        }

        // This is the in-between where we're storing the reagent we're going to pour into the container
        // because we cannot specify a singular reagent to transfer in trans_to
        datum_reagents shaker = Dm.@new<datum_reagents>();
        await hypo.stored_reagents.remove_reagent(hypo.selected_reagent.GetType(), hypo.amount_per_transfer_from_this);
        await shaker.add_reagent(hypo.selected_reagent.GetType(), hypo.amount_per_transfer_from_this, reagtemp: hypo.dispensed_temperature, no_react: TRUE);

        await shaker.trans_to(interacting_with, Dm.num(hypo.amount_per_transfer_from_this), transferred_by: user);
        hypo.balloon_alert(user, I18n.T("[] unit\\s poured", hypo.amount_per_transfer_from_this));
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/objects/items/robot/items/hypo.dm:428
public partial class obj_item_reagent_containers_borghypo_condiment_synthesizer : obj_item_reagent_containers_borghypo // Solids! Condiments! The borger uprising!
{
    // tg: code/game/objects/items/robot/items/hypo.dm:440
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "BorgHypo", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/robot/items/hypo.dm:446
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList condiments = Dm.list();
        foreach (datum_reagent reagent in Dm.each<datum_reagent>(stored_reagents.reagent_list))
        {
            condiments.Add(Dm.list(Dm.list(
                Dm.kv("name", reagent.name),
                Dm.kv("volume", round(Dm.num(reagent.volume), 0.01f) - 1),
                Dm.kv("description", reagent.description)
            ))); // list in a list because Byond merges the first list...
        }

        DmList data = Dm.list();
        data["theme"] = tgui_theme;
        data["minVolume"] = amount_per_transfer_from_this;
        data["maxVolume"] = max_volume_per_reagent;
        data["reagents"] = condiments;
        data["selectedReagent"] = selected_reagent?.name;
        return data;
    }

    // tg: code/game/objects/items/robot/items/hypo.dm:463
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return await obj_item_reagent_containers_borghypo_borgshaker.pour_into(this, interacting_with, user);
    }
}
