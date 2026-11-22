// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/experimentor/experimentor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps RD4, D333: the experimentor): the E.X.P.E.R.I-MENTOR, which takes an item, gives its type a random reaction
// (a relic always reacts to Discover), runs the experiment picked in tg's Experimentor window or pulsed on its wires, reveals one of
// the item's hidden techweb nodes when the experiment matches the reaction, and on a malfunction steals Ian or Runtime, bleeds the
// crew nearby, drains its power or blows up. The loaded item's picture goes to the client as its icon and state, not as tg's
// flattened PNG (DV496). The var block is generated (D111).

// tg: code/modules/research/experimentor/experimentor.dm:1
public partial class obj_machinery_rnd_experimentor : obj_machinery_rnd
{
    /// Items that we can get by transforming (var/static/list/valid_items)
    // tg: code/modules/research/experimentor/experimentor.dm:27
    public static DmList valid_items => TgState.Current.obj_machinery_rnd_experimentor__valid_items ??= Dm.list();
    /// Items that will cause critical reactions (var/static/list/critical_items_typecache)
    // tg: code/modules/research/experimentor/experimentor.dm:29
    public static ref DmList critical_items_typecache => ref TgState.Current.obj_machinery_rnd_experimentor__critical_items_typecache;
    /// Items that the machine shouldn't interact with (var/static/list/banned_typecache)
    // tg: code/modules/research/experimentor/experimentor.dm:31
    public static ref DmList banned_typecache => ref TgState.Current.obj_machinery_rnd_experimentor__banned_typecache;

    // tg: code/modules/research/experimentor/experimentor.dm:35
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_wires(Dm.@new<datum_wires_experimentor>(this));

        load_handlers();

        tracked_ian_ref = WEAKREF(Dm.locate<mob_living_basic_pet_dog_corgi_ian>(GLOB.mob_living_list)) as datum_weakref;
        tracked_runtime_ref = WEAKREF(Dm.locate<mob_living_basic_pet_cat_runtime>(GLOB.mob_living_list)) as datum_weakref;

        if (critical_items_typecache == null)
            critical_items_typecache = typecacheof(Dm.list(
                typeof(obj_item_construction_rcd),
                typeof(obj_item_grenade),
                typeof(obj_item_aicard),
                typeof(obj_item_slime_extract),
                typeof(obj_item_transfer_valve)
            )) as DmList;

        if (banned_typecache == null)
            banned_typecache = typecacheof(Dm.list(
                typeof(obj_item_stock_parts_power_store_cell_infinite),
                typeof(obj_item_stock_parts_power_store_cell_ethereal),
                typeof(obj_item_grenade_chem_grenade_tuberculosis)
            )) as DmList;

        if (Dm.length(valid_items) == 0)
        {
            foreach (Type item_path in Dm.each<Type>(valid_subtypesof(typeof(obj_item))))
            {
                if (ispath(item_path, typeof(obj_item_stock_parts)) || ispath(item_path, typeof(obj_item_grenade_chem_grenade)) || ispath(item_path, typeof(obj_item_knife)))
                {
                    string key = Dm.text("[]", item_path);
                    valid_items[key] = Dm.num(valid_items[key]) + 15;
                }

                if (ispath(item_path, typeof(obj_item_food)))
                {
                    string key = Dm.text("[]", item_path);
                    valid_items[key] = Dm.num(valid_items[key]) + rand(1, 4);
                }
            }
        }
        return dot;
    }

    // tg: code/modules/research/experimentor/experimentor.dm:73
    public virtual void load_handlers()
    {
        foreach (Type handler_type in Dm.each<Type>(valid_subtypesof(typeof(datum_experimentor_result_handler_scan))))
        {
            var handler = Dm.@new(handler_type) as datum_experimentor_result_handler_scan;
            experimentor_result_handlers[handler.scantype] = handler;
        }

        experimentor_result_handlers[FAIL] = Dm.@new<datum_experimentor_result_handler_fail>();
    }

    // tg: code/modules/research/experimentor/experimentor.dm:80
    public virtual DmList get_available_reactions()
    {
        DmList all_reactions = Dm.list();
        foreach (object scantype in Dm.each(experimentor_result_handlers))
        {
            var handler = experimentor_result_handlers[scantype] as datum_experimentor_result_handler;
            if (Dm.truthy(handler.is_special))
                continue;
            Dm.append(ref all_reactions, scantype);
        }

        return all_reactions;
    }

    // tg: code/modules/research/experimentor/experimentor.dm:90
    public virtual object is_special_reaction(object reaction_type = null)
    {
        var handler = experimentor_result_handlers[reaction_type] as datum_experimentor_result_handler;
        return handler.is_special;
    }

    // tg: code/modules/research/experimentor/experimentor.dm:94
    public virtual async DmTask run_experiment__proc(object experiment_type = null)
    {
        if (loaded_item == null)
            return;

        icon_state = Dm.text("[]_wloop", base_icon_state);

        var handler = experimentor_result_handlers[experiment_type] as datum_experimentor_result_handler;
        if (handler != null)
            await handler.execute(this, loaded_item);
        else
            await fail_experiment();

        await handle_global_reactions();
        update_appearance();
        run_experiment = world.time + cooldown; // COOLDOWN_START(src, run_experiment, cooldown) (__DEFINES/cooldowns.dm:111), inline
    }

    // tg: code/modules/research/experimentor/experimentor.dm:109
    public virtual void show_start_message(object message = null, object type = null)
    {
        object sw__1 = type;
        if (Dm.eq(sw__1, MSG_TYPE_NOTICE))
            visible_message(span_notice(Dm.text("[] []", this, message)));
        else if (Dm.eq(sw__1, MSG_TYPE_WARNING))
            visible_message(span_warning(Dm.text("[] []", this, message)));
        else if (Dm.eq(sw__1, MSG_TYPE_DANGER))
            visible_message(span_danger(Dm.text("[] []", this, message)));
        else
            visible_message(span_notice(Dm.text("[] []", this, message)));
    }

    // tg: code/modules/research/experimentor/experimentor.dm:120
    public virtual object is_critical_reaction()
    {
        return is_type_in_typecache(loaded_item, critical_items_typecache);
    }

    // tg: code/modules/research/experimentor/experimentor.dm:123
    public virtual float get_malfunction_chance()
    {
        return (100 - malfunction_probability_coeff) * 0.01f;
    }

    // tg: code/modules/research/experimentor/experimentor.dm:126
    public virtual async DmTask fail_experiment()
    {
        var fail_handler = experimentor_result_handlers[FAIL] as datum_experimentor_result_handler;
        if (fail_handler != null)
            await fail_handler.execute(this, loaded_item);
    }

    // tg: code/modules/research/experimentor/experimentor.dm:131
    public virtual void try_generate_reaction_for_item(obj_item some_item = null)
    {
        string key = Dm.text("[]", some_item.type);
        if (Dm.truthy(is_type_in_typecache(some_item.type, banned_typecache)) || Dm.truthy(item_reactions[key]))
            return;

        if (some_item is obj_item_assembly_relic)
            item_reactions[key] = SCANTYPE_DISCOVER;
        else
            item_reactions[key] = pick(get_available_reactions());
    }

    // tg: code/modules/research/experimentor/experimentor.dm:140
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
            cooldown = clamp(EXPERIMENT_COOLDOWN_BASE / Dm.num(servo.tier), 0.5f, 2);

        foreach (datum_stock_part_scanning_module scanning_module in Dm.each<datum_stock_part_scanning_module>(component_parts))
            malfunction_probability_coeff += Dm.num(scanning_module.tier) * 2;

        foreach (datum_stock_part_micro_laser micro_laser in Dm.each<datum_stock_part_micro_laser>(component_parts))
            malfunction_probability_coeff += Dm.num(micro_laser.tier);
        return dot;
    }

    // tg: code/modules/research/experimentor/experimentor.dm:151
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (in_range(user, this) || isobserver(user))
        {
            Dm.append(ref dot, span_notice(I18n.T("The status display reads:<br>")));
            Dm.append(ref dot, span_notice(I18n.T("Malfunction probability reduced by [].", span_bold(Dm.text("[]", malfunction_probability_coeff)))));
            Dm.append(ref dot, span_notice(I18n.T("Cooldown interval between experiments at [] seconds.", span_bold(Dm.text("[]", cooldown)))));
        }
        return dot;
    }

    // tg: code/modules/research/experimentor/experimentor.dm:158
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        object dot = await base.on_deconstruction(disassembled);
        item_eject();
        return dot;
    }

    // tg: code/modules/research/experimentor/experimentor.dm:162
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Experimentor");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/research/experimentor/experimentor.dm:168
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();

        data["hasItem"] = Dm.not(Dm.not(loaded_item));
        data["isOnCooldown"] = Dm.num(!(run_experiment <= world.time)); // !COOLDOWN_FINISHED(src, run_experiment)
        data["isServerConnected"] = Dm.not(Dm.not(stored_research));

        DmList available_experiments = Dm.list();
        foreach (object scantype in Dm.each(experimentor_result_handlers))
        {
            if (Dm.eq(scantype, FAIL))
                continue;

            var handler = experimentor_result_handlers[scantype] as datum_experimentor_result_handler_scan;

            float is_available = TRUE;
            float is_discover = Dm.num(Dm.eq(scantype, SCANTYPE_DISCOVER));

            if (loaded_item != null)
            {
                if (loaded_item is obj_item_assembly_relic)
                    is_available = is_discover;
                else
                    is_available = Dm.num(!Dm.truthy(is_discover));
                Dm.append(ref available_experiments, Dm.list(Dm.list(
                    Dm.kv("id", scantype),
                    Dm.kv("name", Dm.truthy(handler.name) ? handler.name : capitalize(scantype)),
                    Dm.kv("fa_icon", Dm.truthy(handler.fa_icon) ? handler.fa_icon : "question"),
                    Dm.kv("isAvailable", is_available),
                    Dm.kv("isDiscover", is_discover)
                )));
            }
        }

        data["availableExperiments"] = available_experiments;

        if (loaded_item != null)
        {
            DmList item_data = Dm.list();

            item_data["name"] = loaded_item.name;
            // DEVIATION(DV496): tg's icon2base64(getFlatIcon(loaded_item, no_anim = TRUE)), a PNG the server makes; the port sends the
            // icon and state and the client draws tg's sprite (the server reads no .dmi, D104).
            item_data["icon"] = Dm.list(Dm.kv("icon", Dm.text("[]", loaded_item.icon)), Dm.kv("icon_state", loaded_item.icon_state));
            item_data["isRelic"] = Dm.num(loaded_item is obj_item_assembly_relic);

            item_data["associatedNodes"] = Dm.list();
            DmList unlockable_nodes = SSresearch.techweb_unlock_items[loaded_item.type] as DmList;
            foreach (object node_path in Dm.each(unlockable_nodes))
            {
                var node = SSresearch.techweb_nodes[node_path] as datum_techweb_node;

                Dm.append_at(item_data, "associatedNodes", Dm.list(Dm.list(
                    Dm.kv("name", node.display_name),
                    Dm.kv("isUnlocked", Dm.not(stored_research.hidden_nodes[node_path]))
                )));
            }

            data["loadedItem"] = item_data;
        }

        return data;
    }

    // tg: code/modules/research/experimentor/experimentor.dm:220
    [DmParams("action", "params", "ui", "state")]
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        object sw__1 = action;
        if (Dm.eq(sw__1, "eject"))
        {
            item_eject();
            return TRUE;
        }
        else if (Dm.eq(sw__1, "experiment"))
        {
            object reaction = @params["id"];
            if (reaction == null)
                return dot;

            await try_perform_experiment(reaction);
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/research/experimentor/experimentor.dm:238
    public virtual void item_eject(float? should_clone = FALSE)
    {
        if (loaded_item == null)
            return;

        atom drop_atom = (atom)get_step(this, EAST) ?? drop_location() as atom;
        if (Dm.truthy(should_clone))
        {
            visible_message(span_notice(I18n.T("A duplicate of \\the [] pops out!", loaded_item)));
            Dm.@new(loaded_item.type, drop_atom);
            return;
        }

        Waiting.NoSleep(loaded_item.forceMove(drop_atom));
        loaded_item = null;
    }

    // tg: code/modules/research/experimentor/experimentor.dm:251
    private object match_reaction(obj_item matching = null, object target_reaction = null)
    {
        // PRIVATE_PROC(TRUE)
        if (matching == null || target_reaction == null || Dm.eq(target_reaction, SCANTYPE_DISCOVER))
            return FAIL;

        string key = Dm.text("[]", matching.type);
        if (Dm.eq(item_reactions[key], target_reaction))
            return item_reactions[key];
        return FAIL;
    }

    // tg: code/modules/research/experimentor/experimentor.dm:260
    public virtual async DmTask<object> try_perform_experiment(object reaction = null)
    {
        if (stored_research == null || loaded_item == null || !(run_experiment <= world.time))
            return FALSE;

        if (loaded_item is obj_item_assembly_relic)
            reaction = SCANTYPE_DISCOVER;
        else
            reaction = match_reaction(loaded_item, reaction);

        if (!Dm.eq(reaction, FAIL))
        {
            DmList boostable_nodes = SSresearch.techweb_unlock_items[loaded_item.type] as DmList;
            if (Dm.length(boostable_nodes) > 0)
            {
                object picked_node_path = pick(boostable_nodes);
                stored_research.unhide_node(SSresearch.techweb_nodes[picked_node_path] as datum_techweb_node);
            }
        }

        await run_experiment__proc(reaction);
        await use_energy(750 * JOULES);
        return null;
    }

    // tg: code/modules/research/experimentor/experimentor.dm:276
    public virtual async DmTask handle_global_reactions()
    {
        if (!prob(EFFECT_PROBABILITY * (100 - malfunction_probability_coeff) * 0.01f) || loaded_item == null)
            return;

        float malf_coefficient = rand(1, 100);
        if (malf_coefficient >= 1 && malf_coefficient <= 15)
        {
            visible_message(span_warning(I18n.T("[]'s onboard detection system has malfunctioned!", this)));
            item_reactions[Dm.text("[]", loaded_item.type)] = pick(get_available_reactions());
            item_eject();
        }
        else if (malf_coefficient >= 16 && malf_coefficient <= 35)
        {
            visible_message(span_warning(I18n.T("[] melts [], ian-izing the air around it!", this, loaded_item)));
            await do_smoke(1, this, this);
            var tracked_ian = tracked_ian_ref?.resolve() as mob_living;
            if (tracked_ian != null)
            {
                await do_smoke(1, this, tracked_ian.loc);
                await tracked_ian.forceMove(loc);
                investigate_log("Experimentor has stolen Ian!", INVESTIGATE_EXPERIMENTOR);
            }
            else
            {
                Dm.@new<mob_living_basic_pet_dog_corgi>(loc);
                investigate_log("Experimentor has spawned a new corgi.", INVESTIGATE_EXPERIMENTOR);
            }
        }
        else if (malf_coefficient >= 36 && malf_coefficient <= 50)
        {
            visible_message(span_warning(I18n.T("Experimentor draws the life essence of those nearby!")));
            foreach (mob_living m in Dm.each<mob_living>(view(4, this)))
            {
                to_chat(m, span_danger(I18n.T("You feel your flesh being torn from you, mists of blood drifting to []!", this)));
                playsound(this, pick(Dm.resource("sound/effects/curse/curse1.ogg"), Dm.resource("sound/effects/curse/curse2.ogg"), Dm.resource("sound/effects/curse/curse3.ogg")), 30);
                await m.apply_damage(50, BRUTE, BODY_ZONE_CHEST);
                investigate_log(Dm.text("Experimentor has taken 50 brute a blood sacrifice from []", m), INVESTIGATE_EXPERIMENTOR);
            }
        }
        else if (malf_coefficient >= 51 && malf_coefficient <= 75)
        {
            visible_message(span_warning(I18n.T("[] encounters a run-time error!", this)));
            await do_smoke(1, this, this);
            var tracked_runtime = tracked_runtime_ref?.resolve() as mob_living;
            if (tracked_runtime != null)
            {
                await do_smoke(1, this, tracked_runtime.loc);
                await tracked_runtime.forceMove(drop_location() as atom);
                investigate_log("Experimentor has stolen Runtime!", INVESTIGATE_EXPERIMENTOR);
            }
            else
            {
                Dm.@new<mob_living_basic_pet_cat>(loc);
                investigate_log("Experimentor failed to steal Runtime the cat and instead spawned a new cat.", INVESTIGATE_EXPERIMENTOR);
            }
        }
        else if (malf_coefficient >= 76 && malf_coefficient <= 98)
        {
            visible_message(span_warning(I18n.T("[] emits a low hum.", this)));
            do_sparks(3, FALSE, this, this);
            await use_energy(500 * KILO * JOULES);
            investigate_log("Experimentor has drained power from its APC", INVESTIGATE_EXPERIMENTOR);
        }
        else if (malf_coefficient == 99)
        {
            visible_message(span_warning(I18n.T("[] begins to glow and vibrate. It's going to blow!", this)));
            _addtimer(Dm.@new<datum_callback>(this, nameof(boom)), 5 * SECONDS, file: "code/modules/research/experimentor/experimentor.dm", line: 318);
        }
        else if (malf_coefficient == 100)
        {
            visible_message(span_warning(I18n.T("[] begins to glow and vibrate. It's going to blow!", this)));
            _addtimer(Dm.@new<datum_callback>(this, nameof(honk)), 5 * SECONDS, file: "code/modules/research/experimentor/experimentor.dm", line: 321);
        }
    }

    // tg: code/modules/research/experimentor/experimentor.dm:324
    public virtual async DmTask boom()
    {
        await explosion(this, devastation_range: 1, heavy_impact_range: 5, light_impact_range: 10, flash_range: 5, adminlog: TRUE);
    }

    // tg: code/modules/research/experimentor/experimentor.dm:327
    public virtual void honk()
    {
        playsound(this, Dm.resource("sound/items/bikehorn.ogg"), 500);
        Dm.@new<obj_item_grown_bananapeel>(loc);
    }

    // tg: code/modules/research/experimentor/experimentor.dm:332
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        obj_item weapon = tool;
        if (Dm.truthy(user.combat_mode))
            return NONE;

        if (Dm.truthy(panel_open) && Dm.truthy(is_wire_tool(weapon)))
        {
            await wires.interact(user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(is_insertion_ready(user)))
            return ITEM_INTERACT_BLOCKING;

        if (!Dm.truthy(await user.transferItemToLoc(weapon, this)))
        {
            to_chat(user, span_warning(I18n.T("\\The [] is stuck to your hand!", weapon)));
            return ITEM_INTERACT_BLOCKING;
        }

        loaded_item = weapon;
        to_chat(user, span_notice(I18n.T("You put [] in the chamber.", weapon)));
        flick("h_lathe_load", this);
        try_generate_reaction_for_item(loaded_item);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/research/experimentor/experimentor.dm:352
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(panel_open))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;

        if (!Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;

        await wires.interact(user);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/research/experimentor/experimentor.dm:362
    public override async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/research/experimentor/experimentor.dm:365
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(panel_open))
        {
            await wires.interact(user);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/modules/research/experimentor/experimentor.dm:371
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(panel_open))
        {
            await wires.interact(user);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/modules/research/experimentor/experimentor.dm:377
    public virtual void warn_admins(object user = null, object ReactionName = null)
    {
        turf T = get_turf(user as atom);
        message_admins(Dm.text("Experimentor reaction: [] generated by [] at []", ReactionName, ADMIN_LOOKUPFLW(user), ADMIN_VERBOSEJMP(T)));
        log_game(Dm.text("Experimentor reaction: [] generated by [] in []", ReactionName, key_name(user), AREACOORD(T)));
    }
}

public sealed partial class TgState
{
    // tg: code/modules/research/experimentor/experimentor.dm:27  var/static/list/valid_items = list()
    internal DmList obj_machinery_rnd_experimentor__valid_items;
    // tg: code/modules/research/experimentor/experimentor.dm:29  var/static/list/critical_items_typecache
    internal DmList obj_machinery_rnd_experimentor__critical_items_typecache;
    // tg: code/modules/research/experimentor/experimentor.dm:31  var/static/list/banned_typecache
    internal DmList obj_machinery_rnd_experimentor__banned_typecache;
}
