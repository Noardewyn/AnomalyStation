// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/ai_core/law.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI1, D326) but add_context (the screentips, D264) and vv_edit_var (the admin variable editor), which
// are live-partial rows.

// tg: code/game/machinery/ai_core/law.dm:1-6  the file-local #defines (#undef at :1068-1073)
// tg: code/game/machinery/ai_core/law.dm:21
public partial class obj_machinery_ai_law_rack : obj_machinery
{
    internal const float MODULE_UNSECURED = 0;
    internal const float MODULE_SCREWED = 1;
    internal const float MODULE_WELDED = 2;

    /// Formats a module name for display
    internal static object MODULE_NAME(DmList all_modules, obj_item_ai_module named_module) =>
        (Dm.eq(all_modules[named_module], MODULE_WELDED) ? "unidentified welded module" : (Dm.truthy(named_module?.name) ? named_module.name : "Empty"));

    // tg: code/game/machinery/ai_core/law.dm:53
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ai_modules = Dm.list_n(law_slots);
        update_appearance();
        if (!Dm.truthy(mapload))
        {
            log_silicon(Dm.text("\\A [] was created at [].", name, loc_name(this)));
            message_admins(Dm.text("\\A [] was created at [].", name, ADMIN_VERBOSEJMP(this)));
        }
        register_context();
        return dot;
    }

    // tg: code/game/machinery/ai_core/law.dm:62
    public override object Destroy(float? force = null)
    {
        ai_modules = null;
        return base.Destroy(force);
    }

    /// To be used in logging to specify the status of the law rack
    // tg: code/game/machinery/ai_core/law.dm:105
    public virtual object log_status()
    {
        return "";
    }

    /// Returns a list of all modules that will contribute to the combined lawset
    // tg: code/game/machinery/ai_core/law.dm:109
    public virtual object get_law_affecting_modules()
    {
        DmList affecting_modules = Dm.list();
        // Filter nulls and non-law modules
        foreach (obj_item_ai_module_law module in Dm.each<obj_item_ai_module_law>(ai_modules))
            Dm.append(affecting_modules, module);

        return affecting_modules;
    }

    /// Returns the core module if it exists, otherwise returns null
    // tg: code/game/machinery/ai_core/law.dm:118
    public virtual object get_core_module()
    {
        return Dm.truthy(has_core_slot) ? ai_modules[1] : null;
    }

    // tg: code/game/machinery/ai_core/law.dm:121
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (Dm.@in(gone, ai_modules))
            remove_ai_module(gone as obj_item_ai_module);
        return dot;
    }

    // tg: code/game/machinery/ai_core/law.dm:126
    public override async DmTask dump_inventory_contents(DmList subset = null)
    {
        await base.dump_inventory_contents(subset);
        foreach (obj_item_ai_module installed in Dm.each<obj_item_ai_module>(ai_modules))
            await installed.forceMove(get_turf(this));
    }

    // tg: code/game/machinery/ai_core/law.dm:131
    public virtual object can_secure_check(mob_living user = null)
    {
        if (!Dm.truthy(anchored))
        {
            balloon_alert(user, I18n.T("fasten it first!"));
            return FALSE;
        }
        if (!is_anchorable_floor(loc))
        {
            balloon_alert(user, I18n.T("nothing to secure to!"));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/game/machinery/ai_core/law.dm:140
    public override object can_be_unfasten_wrench(mob user = null, object silent = null)
    {
        if (Dm.truthy(secured))
        {
            if (!Dm.truthy(silent))
                balloon_alert(user, I18n.T("unsecure it first!"));
            return FAILED_UNFASTEN;
        }
        return base.can_be_unfasten_wrench(user, silent);
    }

    // tg: code/game/machinery/ai_core/law.dm:147
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, this)))
            return ITEM_INTERACT_BLOCKING;
        object sw__1 = await default_unfasten_wrench(user, tool, 6 * SECONDS);
        if (Dm.eq(sw__1, CANT_UNFASTEN))
            return NONE;
        else if (Dm.eq(sw__1, FAILED_UNFASTEN))
            return ITEM_INTERACT_BLOCKING;
        else if (Dm.eq(sw__1, SUCCESSFUL_UNFASTEN))
        {
            balloon_alert_to_viewers(I18n.T("[]fastened", Dm.truthy(anchored) ? "" : "un"));
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/machinery/ai_core/law.dm:161
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return await wrench_act(user, tool);
    }

    // tg: code/game/machinery/ai_core/law.dm:164
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, this)))
            return ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(can_secure_check(user)))
            return ITEM_INTERACT_BLOCKING;
        balloon_alert_to_viewers(I18n.T("[]securing to floor...", Dm.truthy(secured) ? "un" : ""));
        if (!Dm.truthy(await tool.use_tool(this, user, 6 * SECONDS, volume: 50, extra_checks: Dm.@new<datum_callback>(this, nameof(can_secure_check), user))))
            return ITEM_INTERACT_BLOCKING;
        secured = Dm.truthy(secured) ? FALSE : TRUE;
        balloon_alert_to_viewers(I18n.T("[]secured", Dm.truthy(secured) ? "" : "un"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/ai_core/law.dm:176
    public override async DmTask<object> wirecutter_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return await wirecutter_act(user, tool);
    }

    // tg: code/game/machinery/ai_core/law.dm:179
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        await ui_interact(user);
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/game/machinery/ai_core/law.dm:183
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await ui_interact(user);
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/game/machinery/ai_core/law.dm:187
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        obj_item_multitool multitool = tool as obj_item_multitool;
        if (!(tool is obj_item_multitool) || !(multitool.buffer is obj_structure_ai_core))
        {
            await ui_interact(user);
            return ITEM_INTERACT_BLOCKING;
        }

        obj_structure_ai_core to_link = multitool.buffer as obj_structure_ai_core;
        to_link.default_link_ref = WEAKREF(this) as datum_weakref;
        tool.play_tool_sound(this, 20);
        balloon_alert(user, I18n.T("linked core to rack"));
        return ITEM_INTERACT_SUCCESS;
    }

    /// Range at which you can see all the modules by name
    // tg: code/game/machinery/ai_core/law.dm:212  #define LAW_EXAMINE_RANGE (#undef at :260)
    private const float LAW_EXAMINE_RANGE = 3;

    // tg: code/game/machinery/ai_core/law.dm:214
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (isAI(user))
            return dot;

        if (!isobserver(user) && get_dist(user, this) > LAW_EXAMINE_RANGE)
            Dm.append(ref dot, span_notice(I18n.T("If you got a bit closer, you could probably [] to see what modules are installed.", EXAMINE_HINT("examine closer"))));
        else
            Dm.append(ref dot, span_notice(I18n.T("[] to see what modules are installed.", EXAMINE_HINT("Examine closer"))));
        float filled = 0;
        foreach (obj_item_ai_module module in Dm.each<obj_item_ai_module>(ai_modules))
            filled++;
        Dm.append(ref dot, span_info(I18n.T("Otherwise, you can see that [] out of [] slots are filled.", filled, Dm.length(ai_modules))));
        if (Dm.truthy(has_core_slot) && get_core_module() == null)
            Dm.append(ref dot, span_warning(I18n.T("You also note that the core slot is empty!")));
        if (Dm.truthy(anchored))
            Dm.append(ref dot, span_notice(I18n.T("It is [] to the floor[].", EXAMINE_HINT("anchored"), Dm.truthy(secured) ? I18n.T(" and []", EXAMINE_HINT("secured with metal cables")) : I18n.T(", but not []", EXAMINE_HINT("secured by metal cables")))));
        return dot;
    }

    // tg: code/game/machinery/ai_core/law.dm:232
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        if (isAI(user))
            return dot;
        if (!isobserver(user) && get_dist(user, this) > LAW_EXAMINE_RANGE)
        {
            Dm.append(ref dot, span_warning(I18n.T("You can't quite make out which modules are installed in [] from here.", this)));
            return dot;
        }
        for (float i = 1, i__end = Dm.length(ai_modules); i <= i__end; i++)
            Dm.append(ref dot, get_slot_examine(i));
        return dot;
    }

    // tg: code/game/machinery/ai_core/law.dm:242
    public virtual object get_slot_examine(object slot = null)
    {
        obj_item_ai_module module = ai_modules[slot] as obj_item_ai_module;
        DmList text = Dm.list();
        Dm.append(text, span_info(I18n.T("&bull; []: []", (Dm.eq(slot, 1) && Dm.truthy(has_core_slot)) ? "Core" : Dm.text("Slot []", Dm.sub(slot, 1)), MODULE_NAME(ai_modules, module))));
        if (module != null)
        {
            string secure_desc = "Bugged (report this)";
            object sw__1 = ai_modules[module];
            if (Dm.eq(sw__1, MODULE_UNSECURED))
                secure_desc = "Unsecured";
            else if (Dm.eq(sw__1, MODULE_SCREWED))
                secure_desc = "Screwed";
            else if (Dm.eq(sw__1, MODULE_WELDED))
                secure_desc = "Welded";
            Dm.append(text, span_notice(I18n.T(secure_desc)));
        }
        if (Dm.truthy((module as obj_item_ai_module_law)?.ioned))
            Dm.append(text, span_warning(I18n.T("It's smoking.")));
        return jointext(text, span_info(" - "));
    }

    // tg: code/game/machinery/ai_core/law.dm:262
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        for (float i = 1, i__end = min(Dm.length(ai_modules), max_slot_overlays); i <= i__end; i++)
        {
            if (ai_modules[i] == null)
                continue;
            // slot 1 is offset 0 - then 1 pixel break, and 1 slot every 2 pixels
            image card = image(icon, icon_state: "pcie");
            card.pixel_z = first_slot_offset + ((i == 1 || !Dm.truthy(has_core_slot)) ? 0 : -1) + ((i - 1) * -3);
            Dm.append(ref dot, card);
        }
        return dot;
    }

    // tg: code/game/machinery/ai_core/law.dm:272
    public virtual async DmTask<object> add_ai_module(obj_item_ai_module module = null, float? slot = null, float? security = null)
    {
        slot ??= 1;
        security ??= MODULE_UNSECURED;
        if (!(module is obj_item_ai_module))
            throw Dm.CRASH(assertion_message("code/game/machinery/ai_core/law.dm", 273, "istype(module)"), "code/game/machinery/ai_core/law.dm", 273);
        if (!isnum(slot))
            throw Dm.CRASH(assertion_message("code/game/machinery/ai_core/law.dm", 274, "isnum(slot)"), "code/game/machinery/ai_core/law.dm", 274);
        if (Dm.lt(slot, 1) || Dm.gt(slot, Dm.length(ai_modules)))
            return FALSE;

        obj_machinery_ai_law_rack_base parent_rack = get_parent_rack() as obj_machinery_ai_law_rack_base;

        module.on_rack_install(this);
        foreach (mob_living_silicon linked_ref in Dm.each<mob_living_silicon>(assoc_to_values(parent_rack?.linked_mobs)))
            await module.silicon_linked_to_installed(linked_ref);
        ai_modules[slot] = module;
        ai_modules[module] = security;
        update_appearance();
        parent_rack?.update_lawset();
        return TRUE;
    }

    // tg: code/game/machinery/ai_core/law.dm:289
    public virtual object remove_ai_module(obj_item_ai_module module = null)
    {
        if (!(module is obj_item_ai_module))
            throw Dm.CRASH(assertion_message("code/game/machinery/ai_core/law.dm", 290, "istype(module)"), "code/game/machinery/ai_core/law.dm", 290);
        float index = ai_modules.Find(module);
        if (index == 0 || ai_modules[index] == null)
            return FALSE;

        obj_machinery_ai_law_rack_base parent_rack = get_parent_rack() as obj_machinery_ai_law_rack_base;

        ai_modules[index] = null;
        module.on_rack_uninstall(this);
        foreach (mob_living_silicon linked_ref in Dm.each<mob_living_silicon>(assoc_to_values(parent_rack?.linked_mobs)))
            module.silicon_unlinked_from_installed(linked_ref);
        if (!QDELING(this))
            update_appearance();
        if (!(parent_rack != null && QDELING(parent_rack)))
            parent_rack?.update_lawset();
        return TRUE;
    }

    // tg: code/game/machinery/ai_core/law.dm:307
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_ai_module)
        {
            await ui_interact(user);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/game/machinery/ai_core/law.dm:313
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (issilicon(user))
        {
            to_chat(user, span_warning(I18n.T("Your programming forbids you from using this.")));
            return null;
        }
        if (refresh_cooldown <= world.time) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm), inline
            refresh_linkable_lists();

        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "LawRack");
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/ai_core/law.dm:325
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        obj_item holding = user.get_active_held_item() as obj_item;
        data["holding_module"] = holding is obj_item_ai_module;
        data["holding_screwdriver"] = Dm.eq(holding?.tool_behaviour, TOOL_SCREWDRIVER);
        data["holding_welder"] = Dm.eq(holding?.tool_behaviour, TOOL_WELDER);
        data["holding_multitool"] = Dm.eq(holding?.tool_behaviour, TOOL_MULTITOOL);
        data["has_core_slot"] = has_core_slot;
        data["depowered"] = !Dm.truthy(is_operational);
        data["refresh_cooldown"] = max(0, refresh_cooldown - world.time); // COOLDOWN_TIMELEFT (__DEFINES/cooldowns.dm), inline

        data["slots"] = Dm.list_n(Dm.length(ai_modules));
        for (float i = 1, i__end = Dm.length(ai_modules); i <= i__end; i++)
        {
            obj_item_ai_module module = ai_modules[i] as obj_item_ai_module;

            ((DmList)data["slots"])[i] = Dm.list(
                Dm.kv("empty", module == null),
                Dm.kv("name", MODULE_NAME(ai_modules, module)),
                Dm.kv("security", Dm.truthy(ai_modules[module]) ? ai_modules[module] : MODULE_UNSECURED),
                Dm.kv("ioned", Dm.truthy((module as obj_item_ai_module_law)?.ioned) ? (module as obj_item_ai_module_law).ioned : FALSE));
        }

        return data;
    }

    // tg: code/game/machinery/ai_core/law.dm:350
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        mob_living user = ui.user as mob_living;
        if (!(user is mob_living) || issilicon(user))
            return dot;
        object sw__1 = action;
        if (Dm.eq(sw__1, "insert_module"))
        {
            float index = Dm.num(clamp(text2num(@params["slot"]), 1, Dm.length(ai_modules)));
            obj_item_ai_module module = user.get_active_held_item() as obj_item_ai_module;
            if (!(module is obj_item_ai_module))
            {
                to_chat(user, span_warning(I18n.T("You need to hold an AI module to insert it!")));
                return TRUE;
            }
            if (!Dm.truthy(module.can_install_to_rack(user, this)))
                return TRUE;
            if (module is obj_item_ai_module_law_core)
            {
                if (!Dm.truthy(has_core_slot))
                {
                    to_chat(user, span_warning(I18n.T("[] has no slots for core modules!", this)));
                    return TRUE;
                }
                if (!Dm.eq(index, 1))
                {
                    to_chat(user, span_warning(I18n.T("You can't install a core module in a non-core slot!")));
                    return TRUE;
                }
            }
            else if (Dm.truthy(has_core_slot))
            {
                if (Dm.eq(index, 1))
                {
                    to_chat(user, span_warning(I18n.T("You can only install core modules in the core slot!")));
                    return TRUE;
                }
            }
            if (!Dm.truthy(await user.transferItemToLoc(module, this)))
            {
                to_chat(user, span_warning(I18n.T("You can't seem to insert [] into []!", module.name, this)));
                return TRUE;
            }
            balloon_alert_to_viewers(I18n.T("inserted slot []", index));
            playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            module.pre_user_install_to_rack(user, this);
            module.log_install(user, this);
            return await add_ai_module(module, index);
        }

        else if (Dm.eq(sw__1, "remove_module"))
        {
            object index = clamp(text2num(@params["slot"]), 1, Dm.length(ai_modules));
            obj_item_ai_module module = ai_modules[index] as obj_item_ai_module;
            if (module == null || Dm.truthy(ai_modules[module]))
                // These have feedback in the UI, the checks are only for sanity
                return TRUE;
            await module.pre_user_uninstall_from_rack(user, this);
            module.log_uninstall(user, this);
            // calls exited which handles updating laws and such
            await try_put_in_hand(module, user);
            balloon_alert_to_viewers(I18n.T("removed slot []", index));
            playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            return TRUE;
        }

        else if (Dm.eq(sw__1, "screw_module"))
        {
            object index = clamp(text2num(@params["slot"]), 1, Dm.length(ai_modules));
            obj_item_ai_module module = ai_modules[index] as obj_item_ai_module;
            if (module == null || Dm.eq(ai_modules[module], MODULE_WELDED))
                // These have feedback in the UI, the checks are only for sanity
                return TRUE;
            obj_item screwer = user.get_active_held_item() as obj_item;
            if (!Dm.eq(screwer.tool_behaviour, TOOL_SCREWDRIVER) || Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, this)))
                return TRUE;
            if (!Dm.truthy(screwer.tool_start_check(user)))
                return TRUE;
            balloon_alert_to_viewers(I18n.T("[]screwing slot []...", Dm.eq(ai_modules[module], MODULE_SCREWED) ? "un" : "", index));
            if (!Dm.truthy(await screwer.use_tool(this, user, 3 * SECONDS, volume: 25)))
                return TRUE;
            module = ai_modules[index] as obj_item_ai_module;
            if (module == null || Dm.eq(ai_modules[module], MODULE_WELDED))
                return TRUE;
            if (Dm.eq(ai_modules[module], MODULE_UNSECURED))
                ai_modules[module] = MODULE_SCREWED;
            else if (Dm.eq(ai_modules[module], MODULE_SCREWED))
                ai_modules[module] = MODULE_UNSECURED;
            return TRUE;
        }

        else if (Dm.eq(sw__1, "weld_module"))
        {
            object index = clamp(text2num(@params["slot"]), 1, Dm.length(ai_modules));
            obj_item_ai_module module = ai_modules[index] as obj_item_ai_module;
            if (module == null || Dm.eq(ai_modules[module], MODULE_UNSECURED))
                // These have feedback in the UI, the checks are only for sanity
                return TRUE;
            obj_item welder = user.get_active_held_item() as obj_item;
            if (!Dm.eq(welder.tool_behaviour, TOOL_WELDER) || Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, this)))
                return TRUE;
            if (!Dm.truthy(welder.tool_start_check(user, amount: 1, heat_required: HIGH_TEMPERATURE_REQUIRED)))
                return TRUE;
            balloon_alert_to_viewers(I18n.T("[]welding slot []...", Dm.eq(ai_modules[module], MODULE_WELDED) ? "un" : "", index));
            if (!Dm.truthy(await welder.use_tool(this, user, 3 * SECONDS, volume: 25, amount: 1)))
                return TRUE;
            module = ai_modules[index] as obj_item_ai_module;
            if (module == null || Dm.eq(ai_modules[module], MODULE_UNSECURED))
                return TRUE;
            if (Dm.eq(ai_modules[module], MODULE_SCREWED))
                ai_modules[module] = MODULE_WELDED;
            else if (Dm.eq(ai_modules[module], MODULE_WELDED))
                ai_modules[module] = MODULE_SCREWED;
            return TRUE;
        }

        else if (Dm.eq(sw__1, "multitool_module"))
        {
            object index = clamp(text2num(@params["slot"]), 1, Dm.length(ai_modules));
            obj_item_ai_module module = ai_modules[index] as obj_item_ai_module;
            if (module == null || Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, this)))
                return TRUE;
            await module.multitool_act(user, user.get_inactive_hand() as obj_item);
            return TRUE;
        }

        else if (Dm.eq(sw__1, "link_silicon") || Dm.eq(sw__1, "unlink_silicon"))
            return FALSE; // handled by subtypes
        else if (Dm.eq(sw__1, "link_rack") || Dm.eq(sw__1, "unlink_rack"))
            return FALSE; // handled by subtypes

        else if (Dm.eq(sw__1, "refresh"))
        {
            if (!Dm.truthy(is_operational) || !(refresh_cooldown <= world.time))
                return TRUE;
            refresh_cooldown = world.time + 30 * SECONDS; // COOLDOWN_START (__DEFINES/cooldowns.dm), inline
            refresh_linkable_lists();
            await update_static_data_for_all_viewers();
            return TRUE;
        }
        return dot;
    }

    // tg: code/game/machinery/ai_core/law.dm:467
    public virtual object format_linkable(atom linkable = null)
    {
        return Dm.list(Dm.kv("name", linkable.name), Dm.kv("ref", REF(linkable)));
    }

    /// Handles updating the lists of things the rack can link to
    // tg: code/game/machinery/ai_core/law.dm:471
    public virtual void refresh_linkable_lists()
    {
        return;
    }

    /// Finds a core law rack this rack is linked to, if any
    // tg: code/game/machinery/ai_core/law.dm:475
    public virtual object get_parent_rack()
    {
        foreach (obj_machinery_ai_law_rack_base rack in Dm.each(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_ai_law_rack_base))))
        {
            if (Dm.@in(this, rack.linked_racks))
                return rack;
        }
        return null;
    }

    /**
     * Scrambles the modules on the rack, messing up their laws
     *
     * the crew will have to remove the modules and use a multitool on them to restore their laws to normal
     *
     * * new_lawset_prob - chance the core lawset is replaced with a new lawset entirely
     * * remove_law_prob - chance a random law is removed from the core lawset / chance a supplied law is removed entirely
     * * shuffle_prob - chance the core lawset is shuffled
     * * base_ion_prob - chance the first law in the core lawset is replaced with an ion law
     * * sub_ion_prob - chance a random law in the core lawset is replaced with an ion law / chance a supplied law is replaced with an ion law entirely
     * * ion_limit - max number of ion laws that can be added. does nothing if "base_ion_prob" and a "sub_ion_prob" are both 0
     * * ion_message - if set, uses this message instead of a random ion law. does nothing if "base_ion_prob" and a "sub_ion_prob" are both 0
     *
     * Returns TRUE if any law was affected, FALSE otherwise.
     */
    // tg: code/game/machinery/ai_core/law.dm:496
    public virtual async DmTask<object> scramble_ai_rack(float? new_lawset_prob = null, float? remove_law_prob = null, float? shuffle_prob = null, float? base_ion_prob = null, float? sub_ion_prob = null, float? ion_limit = null, object ion_message = null)
    {
        new_lawset_prob ??= 0;
        remove_law_prob ??= 0;
        shuffle_prob ??= 0;
        base_ion_prob ??= 100;
        sub_ion_prob ??= 0;
        ion_limit ??= 1;

        object dot = FALSE;

        // Core lawset is affected primarily, but other laws can be touched as well.
        obj_item_ai_module_law_core core = get_core_module() as obj_item_ai_module_law_core;
        bool core_sub_ion = prob(sub_ion_prob.Value);
        float ions_added = 0;
        if (core is obj_item_ai_module_law_core && !Dm.truthy(core.ion_storm_immune))
        {
            // Chance the core lawset's laws are overwritten with a new lawset
            if (prob(new_lawset_prob.Value))
            {
                object ion_lawset_type = pick_weighted_lawset();
                datum_ai_laws ion_lawset = Dm.@new((Type)ion_lawset_type) as datum_ai_laws;
                await core.set_ioned(TRUE);
                core.laws = ion_lawset.inherent.Copy();
                log_law_change(null, Dm.text("ion replaced core lawset entirely in [] ([]) with [] ([])", this, log_status(), ion_lawset, ion_lawset.id));
                qdel(ion_lawset);
                dot = TRUE;
            }
            // Chance a random law is removed from the core lawset
            if (prob(remove_law_prob.Value))
            {
                object removed_index = rand(1, Dm.length(core.laws));
                object removed_law = core.laws[removed_index];
                await core.set_ioned(TRUE);
                core.laws.Cut(Dm.num(removed_index), Dm.num(removed_index) + 1);
                log_law_change(null, Dm.text("ion removed law from [] ([], text: \"[]\")", this, log_status(), removed_law));
                dot = TRUE;
            }
            // Chance the core lawset is shuffled entirely
            if (prob(shuffle_prob.Value))
            {
                await core.set_ioned(TRUE);
                core.laws = shuffle(core.laws) as DmList;
                log_law_change(null, Dm.text("ion shuffled laws in [] ([])", this, log_status()));
                dot = TRUE;
            }
            // Chance the first law in the core lawset is replaced with an ion law
            // Don't add this one if we're replacing a random law later
            if (!core_sub_ion && prob(base_ion_prob.Value))
            {
                await core.set_ioned(TRUE);
                object final_ion_message = Dm.truthy(ion_message) ? ion_message : generate_ion_law();
                core.laws.Insert(1, final_ion_message);
                core.laws[final_ion_message] = TRUE;
                ion_message = null;
                log_law_change(null, Dm.text("ion added law to [] ([], text: \"[]\")", this, log_status(), core.laws[1]));
                ions_added++;
            }
            dot = TRUE;
        }

        // This can double dip and affect the core lawset again - that's fine
        foreach (obj_item_ai_module_law law in Dm.each<obj_item_ai_module_law>(ai_modules))
        {
            if (Dm.truthy(law.ion_storm_immune))
                continue;
            // For core lawsets: Chance that a random law (EXCEPT the newly added ion law) is replaced with another ion law.
            // For supplied laws: Chance the entire supplied law is replaced with a new ion law.
            // If we had no core law and thus added no ion law, this is guaranteed to replace the first supplied law
            if (ions_added < ion_limit && (Dm.truthy(ion_message) || (law == core && core_sub_ion) || prob(sub_ion_prob.Value)))
            {
                object picked_law_index = Dm.length(law.laws) <= 1 ? 1 : rand(2, Dm.length(law.laws));
                object replaced_law = law.laws[picked_law_index];
                object final_ion_message = Dm.truthy(ion_message) ? ion_message : generate_ion_law();
                await law.set_ioned(TRUE);
                law.laws[picked_law_index] = final_ion_message;
                law.laws[final_ion_message] = TRUE;
                ion_message = null;
                log_law_change(null, Dm.text("ion replaced law in [] ([], text: \"[]\" -> \"[]\")", this, log_status(), replaced_law, final_ion_message));
                ions_added++;
                dot = TRUE;
            }

            /// Chance for any supplied law to be lost entirely
            else if (law != core && prob(remove_law_prob.Value))
            {
                object wiped = law.laws[1];
                await law.set_ioned(TRUE);
                law.laws.Cut(1, 2);
                log_law_change(null, Dm.text("ion wiped law from [] ([], text: \"[]\")", this, log_status(), wiped));
                dot = TRUE;
            }
        }

        if (Dm.truthy(dot))
        {
            obj_machinery_ai_law_rack_base parent_rack = get_parent_rack() as obj_machinery_ai_law_rack_base;
            parent_rack?.update_lawset();
        }
        return dot;
    }
}

// Broadcasts laws to another law rack, can't link directly to AIs
// tg: code/game/machinery/ai_core/law.dm:596
public partial class obj_machinery_ai_law_rack_broadcaster : obj_machinery_ai_law_rack
{
    // tg: code/game/machinery/ai_core/law.dm:610
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        return dot;
    }

    // tg: code/game/machinery/ai_core/law.dm:614
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(is_operational))
        {
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_[]", base_icon_state, get_parent_rack() != null ? "linked" : "unlinked"), alpha: this.alpha));
            // DEVIATION(DV12): . += emissive_appearance(icon, "[base_icon_state]_em", src, alpha = src.alpha), the glow on the emissive plane
        }
        return dot;
    }

    // tg: code/game/machinery/ai_core/law.dm:620
    public override void refresh_linkable_lists()
    {
        last_linkable_rack_list = Dm.list();
        foreach (obj_machinery_ai_law_rack_base possible_parent_rack in Dm.each(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_ai_law_rack_base))))
        {
            if (!Dm.truthy(possible_parent_rack.is_operational))
                continue;
            if (!Dm.truthy(can_broadcast_to(possible_parent_rack)))
                continue;
            Dm.append(last_linkable_rack_list, Dm.list(format_linkable(possible_parent_rack)));
        }
    }

    // tg: code/game/machinery/ai_core/law.dm:629
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["linkable_racks"] = last_linkable_rack_list ?? Dm.list();
        data["parent_rack"] = null;
        obj_machinery_ai_law_rack_base parent_rack = get_parent_rack() as obj_machinery_ai_law_rack_base;
        if (parent_rack != null)
            data["parent_rack"] = format_linkable(parent_rack);
        return data;
    }

    // tg: code/game/machinery/ai_core/law.dm:638
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        mob_living user = ui.user as mob_living;
        if (!(user is mob_living) || issilicon(user))
            return dot;

        object sw__1 = action;
        if (Dm.eq(sw__1, "unlink_rack"))
        {
            if (!Dm.truthy(is_operational))
                return TRUE;
            obj_machinery_ai_law_rack_base parent_rack = get_parent_rack() as obj_machinery_ai_law_rack_base;
            if (parent_rack == null)
                return TRUE;
            if (!Dm.truthy(parent_rack.is_operational))
            {
                balloon_alert(user, I18n.T("failed to unlink!"));
                return TRUE;
            }
            parent_rack.unlink_child_law_rack(this);
            balloon_alert_to_viewers(I18n.T("unlinked from []", parent_rack.name));
            playsound(this, Dm.resource("sound/machines/terminal/terminal_off.ogg"), 50, TRUE);
            return TRUE;
        }

        else if (Dm.eq(sw__1, "link_rack"))
        {
            if (!Dm.truthy(is_operational))
                return TRUE;
            obj_machinery_ai_law_rack_base parent_rack = find_parent_rack_by_ref(@params["rack_ref"]) as obj_machinery_ai_law_rack_base;
            if (parent_rack == null)
                return TRUE;
            if (!Dm.truthy(parent_rack.is_operational))
            {
                balloon_alert(user, I18n.T("failed to link!"));
                return TRUE;
            }
            await parent_rack.link_child_law_rack(this);
            balloon_alert_to_viewers(I18n.T("linked to []", parent_rack.name));
            playsound(this, Dm.resource("sound/machines/terminal/terminal_on.ogg"), 50, TRUE);
            return TRUE;
        }
        return dot;
    }

    /// When given a ref(), finds a core law rack it belongs to
    // tg: code/game/machinery/ai_core/law.dm:676
    public virtual object find_parent_rack_by_ref(object rack_ref = null)
    {
        foreach (obj_machinery_ai_law_rack_base rack in Dm.each(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_ai_law_rack_base))))
        {
            if (Dm.eq(REF(rack), rack_ref))
                return rack;
        }
        return null;
    }

    // tg: code/game/machinery/ai_core/law.dm:683
    public override object log_status()
    {
        obj_machinery_ai_law_rack_base parent_rack = get_parent_rack() as obj_machinery_ai_law_rack_base;
        if (parent_rack == null)
            return "unlinked";
        return Dm.text("linked to []", parent_rack.name);
    }

    /// Checks if we can broadcast to another rack
    // tg: code/game/machinery/ai_core/law.dm:690
    public virtual object can_broadcast_to(obj_machinery_ai_law_rack_base possible_parent_rack = null)
    {
        if (!Dm.truthy(is_valid_z_level(get_turf(this), get_turf(possible_parent_rack))))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/machinery/ai_core/law.dm:696
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
        if (QDELING(this))
            return;
        obj_machinery_ai_law_rack_base parent_rack = get_parent_rack() as obj_machinery_ai_law_rack_base;
        if (parent_rack == null || Dm.truthy(can_broadcast_to(parent_rack)))
            return;
        parent_rack.unlink_child_law_rack(this);
        playsound(this, Dm.resource("sound/machines/terminal/terminal_off.ogg"), 50, TRUE);
    }

    // tg: code/game/machinery/ai_core/law.dm:706
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (QDELING(this))
            return dot;
        obj_machinery_ai_law_rack_base parent_rack = get_parent_rack() as obj_machinery_ai_law_rack_base;
        if (parent_rack == null)
            return dot;
        parent_rack.unlink_child_law_rack(this);
        playsound(this, Dm.resource("sound/machines/terminal/terminal_off.ogg"), 50, TRUE);
        return dot;
    }
}

// Links to AIs to provide their laws
// tg: code/game/machinery/ai_core/law.dm:717
public partial class obj_machinery_ai_law_rack_base : obj_machinery_ai_law_rack
{
    // tg: code/game/machinery/ai_core/law.dm:100
    public override async DmTask on_set_is_operational(object old_value = null)
    {
        obj_machinery_ai_law_rack_base parent_rack = get_parent_rack() as obj_machinery_ai_law_rack_base;
        parent_rack?.update_lawset();
    }

    // tg: code/game/machinery/ai_core/law.dm:736
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        combined_lawset = Dm.@new<datum_ai_laws>();
        return dot;
    }

    // tg: code/game/machinery/ai_core/law.dm:740
    public override object Destroy(float? force = null)
    {
        foreach (mob linked_ref in Dm.each<mob>(assoc_to_values(linked_mobs)))
            Waiting.NoSleep(unlink_silicon(linked_ref as mob_living_silicon));
        foreach (obj_machinery_ai_law_rack_broadcaster linked_rack in Dm.each(linked_racks))
            unlink_child_law_rack(linked_rack);
        QDEL_NULL(ref combined_lawset);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/ai_core/law.dm:748
    public virtual async DmTask link_child_law_rack(obj_machinery_ai_law_rack_broadcaster child = null)
    {
        if ((object)child == this || Dm.@in(child, linked_racks))
            return;

        foreach (obj_item_ai_module installed in Dm.each<obj_item_ai_module>(child.ai_modules))
        {
            foreach (mob_living_silicon linked_ref in Dm.each<mob_living_silicon>(assoc_to_values(linked_mobs)))
                await installed.silicon_linked_to_installed(linked_ref);
        }

        LAZYADD(ref linked_racks, child);
        RegisterSignal(child, COMSIG_QDELETING, nameof(unlink_child_law_rack));
        update_lawset();
        await update_static_data_for_all_viewers();
        await child.AddComponent(typeof(datum_component_gps), "Active Broadcaster Rack");
        child.update_appearance();
        await child.update_static_data_for_all_viewers();
    }

    // tg: code/game/machinery/ai_core/law.dm:764
    public virtual void unlink_child_law_rack(obj_machinery_ai_law_rack_broadcaster child = null)
    {
        foreach (obj_item_ai_module installed in Dm.each<obj_item_ai_module>(child.ai_modules))
        {
            foreach (mob_living_silicon linked_ref in Dm.each<mob_living_silicon>(assoc_to_values(linked_mobs)))
                installed.silicon_unlinked_from_installed(linked_ref);
        }

        UnregisterSignal(child, COMSIG_QDELETING);
        LAZYREMOVE(ref linked_racks, child);
        if (!QDELING(this))
        {
            update_lawset();
            Waiting.NoSleep(update_static_data_for_all_viewers());
        }
        if (!QDELING(child))
        {
            qdel(child.GetComponent(typeof(datum_component_gps)));
            child.update_appearance();
            Waiting.NoSleep(child.update_static_data_for_all_viewers());
        }
    }

    // tg: code/game/machinery/ai_core/law.dm:781
    public override void refresh_linkable_lists()
    {
        last_linkable_silicon_list = Dm.list();
        foreach (mob_living_silicon linkable in Dm.each(GLOB.silicon_mobs))
        {
            if (!Dm.truthy(can_link_to(linkable)))
                continue;
            Dm.append(last_linkable_silicon_list, Dm.list(format_linkable(linkable)));
        }

        last_linked_rack_module_count = 0;
        foreach (obj_machinery_ai_law_rack_broadcaster linked_rack in Dm.each(linked_racks))
        {
            if (Dm.truthy(linked_rack.is_operational))
                last_linked_rack_module_count += Dm.length(linked_rack.get_law_affecting_modules());
        }
    }

    // tg: code/game/machinery/ai_core/law.dm:793
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["linkable_silicons"] = last_linkable_silicon_list ?? Dm.list();
        data["linked_rack_module_count"] = last_linked_rack_module_count;
        return data;
    }

    // tg: code/game/machinery/ai_core/law.dm:799
    public override async DmTask<object> ui_data(mob user = null)
    {
        object dot = await base.ui_data(user);
        ((DmList)dot)["linked_mobs"] = assoc_to_keys(linked_mobs);
        return dot;
    }

    // tg: code/game/machinery/ai_core/law.dm:803
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        mob_living user = ui.user as mob_living;
        if (!(user is mob_living) || issilicon(user))
            return dot;
        object sw__1 = action;
        if (Dm.eq(sw__1, "unlink_silicon"))
        {
            if (!Dm.truthy(Dm.length(linked_mobs)) || !Dm.truthy(is_operational))
                return TRUE;
            object index = clamp(Dm.add(text2num(@params["silicon_index"]), 1), 1, Dm.length(linked_mobs));
            if (Dm.lt(index, 1) || Dm.gt(index, Dm.length(linked_mobs)))
                return TRUE;
            object removed_name = linked_mobs[index];
            object removed_bot = linked_mobs[removed_name];
            if (Dm.truthy(removed_bot))
                await unlink_silicon(removed_bot as mob_living_silicon);
            Dm.remove(linked_mobs, removed_name);
            balloon_alert_to_viewers(I18n.T("unlinked silicon"), I18n.T("unlinked []", removed_name));
            playsound(this, Dm.resource("sound/machines/terminal/terminal_off.ogg"), 50, TRUE);
            return TRUE;
        }
        else if (Dm.eq(sw__1, "link_silicon"))
        {
            if (!Dm.truthy(is_operational))
                return TRUE;
            mob_living_silicon new_bot = find_silicon_by_ref(@params["silicon_ref"]) as mob_living_silicon;
            // Adding a qdeleted bot is valid (this is to prevent people from using law racks as a "dead ai radar")
            // But we still need to verify it's a silicon they saw in the ui (to prevent href exploits)
            if (QDELETED(new_bot))
            {
                foreach (DmList linkable in Dm.each(last_linkable_silicon_list))
                {
                    if (Dm.eq(linkable["name"], @params["silicon_name"]))
                        linked_mobs[linkable["name"]] = null;
                }
            }
            // Otherwise we can just verify can_link_to (this was already ran in ui data)
            else if (Dm.truthy(can_link_to(new_bot)))
            {
                await link_silicon(new_bot);
                balloon_alert_to_viewers(I18n.T("linked silicon"), I18n.T("linked to []", new_bot.name));
                playsound(this, Dm.resource("sound/machines/terminal/terminal_on.ogg"), 50, TRUE);
            }
            return TRUE;
        }
        return dot;
    }

    // tg: code/game/machinery/ai_core/law.dm:842
    public override object get_law_affecting_modules()
    {
        object dot = base.get_law_affecting_modules();
        foreach (obj_machinery_ai_law_rack_broadcaster linked_rack in Dm.each(linked_racks))
        {
            if (Dm.truthy(linked_rack.is_operational))
                Dm.combine(ref dot, linked_rack.get_law_affecting_modules());
        }
        return dot;
    }

    // tg: code/game/machinery/ai_core/law.dm:848
    public override object log_status()
    {
        DmList mob_names = Dm.list();
        foreach ((object link_name, object link_mob) in Dm.pairs(linked_mobs))
            Dm.append(mob_names, Dm.text("[] ([])", link_name, link_mob == null ? "no mob" : key_name(link_mob)));
        if (Dm.truthy(Dm.length(mob_names)))
            return Dm.text("linked to []", english_list(mob_names));
        return "unlinked";
    }

    /// When given a ref(), finds a silicon mob it belongs to
    // tg: code/game/machinery/ai_core/law.dm:857
    public virtual object find_silicon_by_ref(object silicon_ref = null)
    {
        foreach (mob_living_silicon silicon in Dm.each(GLOB.silicon_mobs))
        {
            if (Dm.eq(REF(silicon), silicon_ref))
                return silicon;
        }
        return null;
    }

    // tg: code/game/machinery/ai_core/law.dm:864
    public virtual object can_link_to(mob_living_silicon new_bot = null)
    {
        if (!Dm.truthy(is_valid_z_level(get_turf(this), get_turf(new_bot))))
            return FALSE; // Can't link to bots on different z-levels
        if (Dm.truthy(new_bot.control_disabled) || Dm.truthy(new_bot.no_law_rack_link))
            return FALSE;
        if (iscyborg(new_bot))
        {
            mob_living_silicon_robot new_borg = new_bot as mob_living_silicon_robot;
            if (Dm.truthy(new_borg.scrambledcodes) || Dm.truthy(new_borg.emagged) || Dm.truthy(new_borg.shell))
                return FALSE;
            return TRUE;
        }
        if (isAI(new_bot))
            return TRUE;
        return FALSE;
    }

    // tg: code/game/machinery/ai_core/law.dm:880
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
        if (QDELING(this))
            return;
        // if the rack leaves the z-level, unlinks relevant silicons.
        // currently this is a one way check (ie silicons don't get unlinked if they leave the z) so as to prevent random unlinking from visiting lavaland.
        foreach (mob_living_silicon linked in Dm.each<mob_living_silicon>(assoc_to_values(linked_mobs)))
        {
            if (!Dm.truthy(can_link_to(linked)) || !Dm.truthy(await rack_stun(linked)))
                continue;
            to_chat(linked, span_userdanger(I18n.T("Rack connection lost. Recalculating directives...")));
            await unlink_silicon(linked);
        }
    }

    // tg: code/game/machinery/ai_core/law.dm:892
    public virtual async DmTask link_silicon(mob_living_silicon new_bot = null, float? announce = null)
    {
        announce ??= TRUE;
        if (!Dm.truthy(can_link_to(new_bot)))
            return;
        if (new_bot.laws == null)
        {
            stack_trace(Dm.text("Tried to link a [] silicon with no law datum to a law rack!", new_bot.type), "code/game/machinery/ai_core/law.dm", 896);
            new_bot.make_laws();
        }

        RegisterSignal(new_bot, COMSIG_QDELETING, nameof(clear_silicon_ref));
        RegisterSignal(new_bot, COMSIG_MOB_FULLY_RENAMED, nameof(silicon_renamed));
        linked_mobs[new_bot.name] = new_bot;
        new_bot.no_law_rack_link = TRUE; // don't let other racks link to us
        apply_lawset(new_bot, announce);
        refresh_linkable_lists();
        await update_static_data_for_all_viewers();
        foreach (obj_item_ai_module installed in Dm.each<obj_item_ai_module>(ai_modules))
            await installed.silicon_linked_to_installed(new_bot);
        await AddComponent(typeof(datum_component_gps), "Active Module Rack");
    }

    // tg: code/game/machinery/ai_core/law.dm:910
    public virtual async DmTask unlink_silicon(mob_living_silicon unlinked_bot = null)
    {
        if (!QDELING(unlinked_bot) && issilicon(unlinked_bot)) // TBH I don't know why this issilicon is here.
        {
            unlinked_bot.no_law_rack_link = FALSE;
            unlinked_bot.laws.set_zeroth_law(null, null);
            unlinked_bot.laws.clear_hacked_laws();
            unlinked_bot.laws.clear_inherent_laws();
            unlinked_bot.laws.clear_supplied_laws();
            unlinked_bot.announce_law_change();
            foreach (obj_item_ai_module installed in Dm.each<obj_item_ai_module>(ai_modules))
                installed.silicon_unlinked_from_installed(unlinked_bot);
        }
        clear_silicon_ref(unlinked_bot);
        refresh_linkable_lists();
        await update_static_data_for_all_viewers();
        if (!Dm.truthy(Dm.length(linked_mobs)))
            qdel(GetComponent(typeof(datum_component_gps)));
    }

    // tg: code/game/machinery/ai_core/law.dm:926
    public virtual void clear_silicon_ref(mob source = null)
    {
        UnregisterSignal(source, COMSIG_QDELETING);
        UnregisterSignal(source, COMSIG_MOB_FULLY_RENAMED);
        foreach ((object link_name, object link_mob) in Dm.pairs(linked_mobs))
        {
            if (link_mob == source)
            {
                linked_mobs[link_name] = null;
                break;
            }
        }
    }

    // tg: code/game/machinery/ai_core/law.dm:935
    public virtual void silicon_renamed(mob source = null, object oldname = null, object newname = null)
    {
        foreach ((object link_name, object link_mob) in Dm.pairs(linked_mobs))
        {
            if (link_mob == source)
            {
                linked_mobs[newname] = linked_mobs[link_name];
                Dm.remove(linked_mobs, link_name);
                break;
            }
        }
    }

    // tg: code/game/machinery/ai_core/law.dm:943
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        foreach (mob_living_silicon bot in Dm.each<mob_living_silicon>(assoc_to_values(linked_mobs)))
        {
            if (!Dm.truthy(await rack_stun(bot)))
                continue;
            to_chat(bot, span_userdanger(I18n.T("Rack connection lost. Recalculating directives...")));
            await unlink_silicon(bot);
        }
        return null;
    }

    /// Attempts to stun a linked bot and prevent it from interacting with certain devices
    // tg: code/game/machinery/ai_core/law.dm:951
    public virtual async DmTask<object> rack_stun(mob_living bot = null, float? stun_duration = null, float? firewall_duration = null)
    {
        stun_duration ??= 10 * SECONDS;
        firewall_duration ??= 30 * SECONDS;
        if (Dm.truthy(IS_MALF_AI(bot)))
            return FALSE; // lol
        if (!Dm.truthy(is_valid_z_level(get_turf(bot), get_turf(this))))
            return FALSE; // out of range, but still connected - like a mining borg on lavaland. avoid stunning and getting them killed
        if (Dm.gt(bot.AmountStun(), stun_duration / 2))
            return FALSE; // give them a break

        await bot.Stun(stun_duration, ignore_canstun: TRUE);
        await bot.apply_status_effect(typeof(datum_status_effect_firewalled), stun_duration + firewall_duration);
        return TRUE;
    }

    // tg: code/game/machinery/ai_core/law.dm:963
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!isAI(user))
            return dot;
        foreach ((object link_name, object link_mob) in Dm.pairs(linked_mobs))
        {
            if (link_mob == user)
                Dm.append(ref dot, span_notice(I18n.T("This is <b>your</b> module rack.")));
        }
        return dot;
    }

    /// Refreshes the combined lawset with all the modules currently installed and syncs it to all linked silicons
    // tg: code/game/machinery/ai_core/law.dm:972
    public virtual void update_lawset()
    {
        if (!Dm.truthy(is_operational)) // updates won't happen while depowered or broken
            return;

        combined_lawset.clear_zeroth_law();
        combined_lawset.clear_hacked_laws();
        combined_lawset.clear_inherent_laws();
        combined_lawset.clear_supplied_laws();

        foreach (obj_item_ai_module_law installed in Dm.each(get_law_affecting_modules()))
            installed.apply_to_combined_lawset(combined_lawset);

        foreach ((object link_name, object link_mob) in Dm.pairs(linked_mobs))
            apply_lawset(link_mob as mob_living_silicon);
    }

    /// Applies the current combined lawset to a silicon, optionally announcing the change to them/their borgs/deadchat
    // tg: code/game/machinery/ai_core/law.dm:988
    public virtual void apply_lawset(mob_living_silicon linked_ref = null, float? announce = null)
    {
        announce ??= TRUE;
        if (!(linked_ref is mob_living_silicon))
            return;

        object old_zeroth = linked_ref.laws.zeroth;
        object old_zeroth_borg = linked_ref.laws.zeroth_borg;
        DmList old_hacked = linked_ref.laws.hacked.Copy();
        DmList old_inherent = linked_ref.laws.inherent.Copy();
        DmList old_supplied = linked_ref.laws.supplied.Copy();

        linked_ref.laws.set_zeroth_law(combined_lawset.zeroth, combined_lawset.zeroth_borg);
        linked_ref.laws.hacked = combined_lawset.hacked.Copy();
        linked_ref.laws.inherent = combined_lawset.inherent.Copy();
        linked_ref.laws.supplied = combined_lawset.supplied.Copy();
        // avoid spamming the ai if nothing changed
        if (Dm.eq(old_zeroth, combined_lawset.zeroth)
            && Dm.eq(old_zeroth_borg, combined_lawset.zeroth_borg)
            && Dm.equiv(old_hacked, combined_lawset.hacked)
            && Dm.equiv(old_inherent, combined_lawset.inherent)
            && Dm.equiv(old_supplied, combined_lawset.supplied))
            return;

        if (Dm.truthy(announce))
        {
            linked_ref.announce_law_change();
            linked_ref.law_change_counter++;
        }
        if (Dm.truthy(linked_ref._listen_lookup?[COMSIG_SILICON_MODULE_RACK_LAWSET_UPDATE]))
            linked_ref._SendSignal(COMSIG_SILICON_MODULE_RACK_LAWSET_UPDATE, Dm.list(linked_ref, this, announce));
    }

    // tg: code/game/machinery/ai_core/law.dm:1016
    public override object get_parent_rack()
    {
        return this;
    }
}

// tg: code/game/machinery/ai_core/law.dm:1019
public partial class obj_machinery_ai_law_rack_base_core : obj_machinery_ai_law_rack_base
{
    /// Designations for the core law rack, used to name it
    // tg: code/game/machinery/ai_core/law.dm:1031  VAR_PRIVATE/static/list/core_designations
    private static ref DmList core_designations => ref TgState.Current.obj_machinery_ai_law_rack_base_core__core_designations;

    // tg: code/game/machinery/ai_core/law.dm:1033
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(Dm.length(core_designations)))
            core_designations = GLOB.greek_letters.Copy();

        object designation;
        if (Dm.truthy(mapload))
        {
            anchored = TRUE;
            secured = TRUE;
            Waiting.NoSleep(load_config_law());
            designation = popleft(core_designations);
            Dm.@new<obj_item_paper_law_rack_tutorial>(loc);
        }
        else
            designation = pick_n_take(core_designations);

        name = Dm.text("\\proper [] '[]'", name, LOWER_TEXT(designation));
        return dot;
    }

    // tg: code/game/machinery/ai_core/law.dm:1052
    public virtual async DmTask load_config_law()
    {
        object default_laws = get_round_default_lawset();

        foreach (object core in Dm.each(subtypesof(typeof(obj_item_ai_module_law_core_full))))
        {
            if (Dm.eq(Dm.initial((Type)core, (obj_item_ai_module_law_core_full p) => p.law_id), Dm.initial((Type)default_laws, (datum_ai_laws p) => p.id)))
                await add_ai_module(Dm.@new((Type)core, this) as obj_item_ai_module, 1, MODULE_WELDED);
        }
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/ai_core/law.dm:1031
    internal DmList obj_machinery_ai_law_rack_base_core__core_designations;
}
