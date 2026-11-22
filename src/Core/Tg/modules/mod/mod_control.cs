// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/mod_control.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO1 (D333): the file whole. The MOD control unit: its theme sets up its parts at Initialize, it processes its charge drain
// and its modules, it is worn on the back and becomes its wearer's (set_wearer, unset_wearer, clean_up when it leaves them), its
// cover opens with a screwdriver for modules (installed by hand, removed with a crowbar), the core (a wrench), the access lock (an
// ID), the wires, the paint kit and a pAI card; its strippable alternate actions, the quick module radial (Ctrl and the chosen
// mouse button), its charge accessors and the spacesuit HUD's charge icon, the speed of its parts and the speed potion. tg's first
// definition of Initialize (:88) is Initialize__d0 here: mod_link.dm's later definition (mod_link.cs) overrides it and calls it as
// its ..() (rules 7.5). D132 seam (InertSeam, narrowed by gaps MO2): a module whose own tg code is not translated yet (the space
// ninja's modules, M10+, and the circuit module, wiremod) is not installed (install), so that an inert type never runs as the base
// module. The var block is generated (D111).

/// MODsuits, trade-off between armor and utility
// tg: code/modules/mod/mod_control.dm:8
public partial class obj_item_mod_control : obj_item_mod
{
    /// Delay between moves as AI.
    // tg: code/modules/mod/mod_control.dm:82  var/static/movedelay = 0
    public static ref float movedelay => ref TgState.Current.obj_item_mod_control__movedelay;

    // tg: code/modules/mod/mod_control.dm:88 (the first definition; mod_link.dm:66 overrides it, rules 7.5)
    [DmParams("mapload", "new_theme", "new_skin", "new_core")]
    public object Initialize__d0(object mapload = null, params object[] __extra)
    {
        object new_theme = Dm.arg(__extra, 0);
        object new_skin = Dm.arg(__extra, 1);
        obj_item_mod_core new_core = Dm.arg<obj_item_mod_core>(__extra, 2);
        object dot = null;
        dot = base.Initialize(mapload, Dm.forward(__extra, new_theme, new_skin, new_core));
        if (!Dm.truthy(movedelay))
            movedelay = Dm.num(config.Get(typeof(datum_config_entry_number_movedelay_run_delay))); /* CONFIG_GET(number/movedelay/run_delay) */
        if (Dm.truthy(new_theme))
            theme = new_theme;
        theme = GLOB.mod_themes[theme];
        Waiting.NoSleep(((datum_mod_theme)theme).set_up_parts(this, new_skin));
        foreach (obj_item part in Dm.each_cast<obj_item>(get_parts()))
            RegisterSignal(part, COMSIG_ATOM_DESTRUCTION, nameof(on_part_destruction));
        set_wires(Dm.@new<datum_wires_mod>(this));
        if (Dm.truthy(length(req_access)))
            locked = TRUE;
        if (new_core != null)
            Waiting.NoSleep(new_core.install(this));
        Waiting.NoSleep(update_speed());

        RegisterSignal(this, COMSIG_ATOM_EXITED, nameof(on_exit));
        RegisterSignal(this, COMSIG_SPEED_POTION_APPLIED, nameof(on_potion));
        RegisterSignal(this, COMSIG_ITEM_GET_STRIPPABLE_ALT_ACTIONS, nameof(get_strippable_alternate_actions));
        RegisterSignal(this, COMSIG_ITEM_STRIPPABLE_ALT_ACTION, nameof(do_strippable_action));

        foreach (Type module__it in Dm.each_cast<Type>(((datum_mod_theme)theme).inbuilt_modules))
        {
            // InertSeam (gaps MO1, D132; narrowed by gaps MO2, which translated the department modules every theme builds in): an
            // inbuilt module whose own tg code is not translated yet (the space ninja's, M10+) is not made, so that its inert type never
            // runs as the base module.
            if (InertSeam.IsInertType(module__it))
                continue;
            obj_item_mod_module module = Dm.@new(module__it, this) as obj_item_mod_module;
            Waiting.NoSleep(install(module));
        }
        START_PROCESSING(SSobj, this);
        Waiting.NoSleep(AddElement(typeof(datum_element_drag_pickup)));
        return dot;
    }

    // tg: code/modules/mod/mod_control.dm:115
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        foreach (obj_item_mod_module module in Dm.each_cast<obj_item_mod_module>(modules))
            Waiting.NoSleep(uninstall(module, deleting: TRUE));
        if (core != null)
            QDEL_NULL(ref core);
        QDEL_NULL(ref mod_link);
        foreach (object part_key in Dm.each(mod_parts))
        {
            datum_mod_part part_datum = mod_parts[part_key] as datum_mod_part;
            Dm.remove(ref mod_parts, part_key);
            qdel(part_datum);
        }
        return base.Destroy(force);
    }

    // tg: code/modules/mod/mod_control.dm:128
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        atom visible_atom = (atom)wearer ?? this;
        if (wearer != null)
            await clean_up();
        visible_atom.visible_message(span_bolddanger(I18n.T("[] fall[] apart, completely destroyed!", this, p_s())), vision_distance: COMBAT_MESSAGE_RANGE);
        foreach (obj_item_mod_module module in Dm.each_cast<obj_item_mod_module>(modules))
            await uninstall(module);
        if (ai_assistant != null)
        {
            if (ispAI(ai_assistant))
                Waiting.Detach(remove_pai(null, TRUE)); // async to appease spaceman DMM because the branch we don't run has a do_after
            else
            {
                foreach (datum_action action in Dm.each_cast<datum_action>(actions))
                {
                    if (action.owner == ai_assistant)
                        await action.Remove(ai_assistant);
                }
                Dm.@new<obj_item_mod_ai_minicard>(drop_location(), ai_assistant);
            }
        }
        await base.atom_destruction(damage_flag);
    }

    // tg: code/modules/mod/mod_control.dm:145
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(active))
        {
            Dm.append(ref dot, span_notice(I18n.T("Charge: [].", core != null ? Dm.text("[]%", get_charge_percent()) : I18n.T("No core"))));
            Dm.append(ref dot, span_notice(I18n.T("Selected module: [].", (selected_module is var or__1 && Dm.truthy(or__1) ? or__1 : (object)I18n.T("None")))));
        }
        if (!Dm.truthy(open) && !Dm.truthy(active))
        {
            if (wearer == null)
                Dm.append(ref dot, span_notice(I18n.T("You could equip it to turn it on.")));
            Dm.append(ref dot, span_notice(I18n.T("You could open the cover with a <b>screwdriver</b>.")));
        }
        else if (Dm.truthy(open))
        {
            Dm.append(ref dot, span_notice(I18n.T("You could close the cover with a <b>screwdriver</b>.")));
            Dm.append(ref dot, span_notice(I18n.T("You could use <b>modules</b> on it to install them.")));
            Dm.append(ref dot, span_notice(I18n.T("You could remove modules with a <b>crowbar</b>.")));
            Dm.append(ref dot, span_notice(I18n.T("You could update the access lock with an <b>ID</b>.")));
            Dm.append(ref dot, span_notice(I18n.T("You could access the wire panel with a <b>wire tool</b>.")));
            if (core != null)
                Dm.append(ref dot, span_notice(I18n.T("You could remove [] with a <b>wrench</b>.", core)));
            else
                Dm.append(ref dot, span_notice(I18n.T("You could use a <b>MOD core</b> on it to install one.")));
            if (ai_assistant == null)
                Dm.append(ref dot, span_notice(I18n.T("You could install an AI or pAI using their <b>storage card</b>.")));
            else if (isAI(ai_assistant))
                Dm.append(ref dot, span_notice(I18n.T("You could remove [] with an <b>intellicard</b>.", ai_assistant)));
        }
        Dm.append(ref dot, span_notice(I18n.T("You could copy/set link frequency with a <b>multitool</b>.")));
        Dm.append(ref dot, span_notice(I18n.T("<i>You could examine it more thoroughly...</i>")));
        return dot;
    }

    // tg: code/modules/mod/mod_control.dm:171
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        Dm.append(ref dot, I18n.T("<i>[]</i>", extended_desc));
        return dot;
    }

    // tg: code/modules/mod/mod_control.dm:175
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (seconds_electrified > MACHINE_NOT_ELECTRIFIED)
            seconds_electrified--;
        if (mod_link.link_call != null)
            Waiting.NoSleep(subtract_charge(0.25f * DEFAULT_CHARGE_DRAIN * Dm.num(seconds_per_tick)));
        if (!Dm.truthy(active))
            return null;
        if (!Dm.truthy(get_charge()) && Dm.truthy(active) && !Dm.truthy(activating))
        {
            Waiting.NoSleep(power_off());
            return null;
        }
        float malfunctioning_charge_drain = 0;
        if (Dm.truthy(malfunctioning))
            malfunctioning_charge_drain = rand(0.2f * DEFAULT_CHARGE_DRAIN, 4 * DEFAULT_CHARGE_DRAIN); // About triple power usage on average.
        Waiting.NoSleep(subtract_charge((charge_drain + malfunctioning_charge_drain) * Dm.num(seconds_per_tick)));
        foreach (obj_item_mod_module module in Dm.each_cast<obj_item_mod_module>(modules))
        {
            if (Dm.truthy(malfunctioning) && Dm.truthy(module.active) && SPT_PROB(5, seconds_per_tick))
                Waiting.NoSleep(module.deactivate(display_message: TRUE));
            Waiting.NoSleep(module.on_process(seconds_per_tick));
        }
        return null;
    }

    //needs to be visual because we wanna show it in select equipment
    // tg: code/modules/mod/mod_control.dm:194
    public override async DmTask<object> visual_equipped(mob user = null, object slot = null, float? initial = null)
    {
        initial ??= FALSE;
        if (Dm.truthy(Dm.bitand(slot, slot_flags)))
            await set_wearer(user as mob_living_carbon_human);
        else if (wearer != null)
            unset_wearer();
        return await base.visual_equipped(user, slot, initial);
    }

    // tg: code/modules/mod/mod_control.dm:201
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (wearer == null)
            return dot;
        await clean_up();
        return dot;
    }

    // Grant pinned actions to pin owners, gives AI pinned actions to the AI and not the wearer
    // tg: code/modules/mod/mod_control.dm:208
    public override async DmTask grant_action_to_bearer(datum_action action = null)
    {
        if (!(action is datum_action_item_action_mod_pinnable))
        {
            await base.grant_action_to_bearer(action);
            return;
        }
        datum_action_item_action_mod_pinnable pinned = action as datum_action_item_action_mod_pinnable;
        await give_item_action(action, pinned.pinner, slot_flags);
    }

    // tg: code/modules/mod/mod_control.dm:214
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (wearer == null || old_loc != wearer || loc == wearer)
            return dot;
        await clean_up();
        return dot;
    }

    // tg: code/modules/mod/mod_control.dm:220
    public override object can_mob_unequip(mob user = null)
    {
        if (user != wearer)
            return base.can_mob_unequip(user);

        if (Dm.truthy(active))
        {
            balloon_alert(wearer, I18n.T("unit active!"));
            playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, FALSE, SILENCED_SOUND_EXTRARANGE);
            return FALSE;
        }

        foreach (obj_item part in Dm.each_cast<obj_item>(get_parts()))
        {
            if (part.loc != this)
            {
                balloon_alert(user, I18n.T("parts extended!"));
                playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, FALSE, SILENCED_SOUND_EXTRARANGE);
                return FALSE;
            }
        }

        return base.can_mob_unequip(user);
    }

    // tg: code/modules/mod/mod_control.dm:237
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        obj_item wrench = tool;
        if (Dm.truthy(seconds_electrified) && Dm.truthy(get_charge()) && Dm.truthy(await shock__proc(user, 100)))
            return ITEM_INTERACT_BLOCKING;
        if (Dm.truthy(open))
        {
            if (core == null)
            {
                balloon_alert(user, I18n.T("no core!"));
                return ITEM_INTERACT_BLOCKING;
            }
            balloon_alert(user, I18n.T("removing core..."));
            wrench.play_tool_sound(this, 100);
            if (!Dm.truthy(await wrench.use_tool(this, user, 3 * SECONDS)) || !Dm.truthy(open))
            {
                balloon_alert(user, I18n.T("interrupted!"));
                return ITEM_INTERACT_BLOCKING;
            }
            wrench.play_tool_sound(this, 100);
            balloon_alert(user, I18n.T("core removed"));
            await core.forceMove(drop_location() as atom);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/modules/mod/mod_control.dm:255
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        obj_item screwdriver = tool;
        if (Dm.truthy(active) || Dm.truthy(activating) || Dm.truthy(ai_controller))
        {
            balloon_alert(user, I18n.T("unit active!"));
            playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
            return ITEM_INTERACT_BLOCKING;
        }
        balloon_alert(user, I18n.T("[] cover...", Dm.truthy(open) ? I18n.T("closing") : I18n.T("opening")));
        screwdriver.play_tool_sound(this, 100);
        if (Dm.truthy(await screwdriver.use_tool(this, user, 1 * SECONDS)))
        {
            if (Dm.truthy(active) || Dm.truthy(activating))
            {
                balloon_alert(user, I18n.T("unit active!"));
                return ITEM_INTERACT_SUCCESS;
            }
            screwdriver.play_tool_sound(this, 100);
            balloon_alert(user, I18n.T("cover []", Dm.truthy(open) ? I18n.T("closed") : I18n.T("opened")));
            open = Dm.not(open);
        }
        else
        {
            balloon_alert(user, I18n.T("interrupted!"));
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mod/mod_control.dm:273
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        obj_item crowbar = tool;
        if (!Dm.truthy(open))
        {
            balloon_alert(user, I18n.T("cover closed!"));
            playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await allowed__proc(user)))
        {
            balloon_alert(user, I18n.T("insufficient access!"));
            playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(Dm.bitand(SEND_SIGNAL(COMSIG_MOD_MODULE_REMOVAL, Dm.list(this, user)), MOD_CANCEL_REMOVAL)))
        {
            playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(length(modules)))
        {
            DmList removable_modules = Dm.list();
            foreach (obj_item_mod_module module in Dm.each_cast<obj_item_mod_module>(modules))
            {
                if (!Dm.truthy(module.removable))
                    continue;
                Dm.append(ref removable_modules, module);
            }
            obj_item_mod_module module_to_remove = await tgui_input_list(user, I18n.T("Which module to remove?"), I18n.T("Module Removal"), removable_modules) as obj_item_mod_module;
            if (module_to_remove?.mod == null)
                return ITEM_INTERACT_BLOCKING;
            await uninstall(module_to_remove);
            await module_to_remove.forceMove(drop_location() as atom);
            crowbar.play_tool_sound(this, 100);
            SEND_SIGNAL(COMSIG_MOD_MODULE_REMOVED, Dm.list(this, user));
            return ITEM_INTERACT_SUCCESS;
        }
        balloon_alert(user, I18n.T("no modules!"));
        playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
        return ITEM_INTERACT_BLOCKING;
    }

    // Makes use of tool act to prevent shoving stuff into our internal storage
    // tg: code/modules/mod/mod_control.dm:304
    public override async DmTask<object> tool_act(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (tool is obj_item_pai_card)
        {
            if (!Dm.truthy(open))
            {
                balloon_alert(user, I18n.T("cover closed!"));
                return NONE; // shoves the card in the storage anyways
            }
            await insert_pai(user, tool as obj_item_pai_card);
            return ITEM_INTERACT_SUCCESS;
        }
        if (tool is obj_item_mod_paint)
        {
            obj_item_mod_paint paint_kit = tool as obj_item_mod_paint;
            if (Dm.truthy(active) || Dm.truthy(activating))
            {
                balloon_alert(user, I18n.T("unit active!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK))) // Right click
            {
                if (paint_kit.editing_mod == this)
                    return ITEM_INTERACT_BLOCKING;
                paint_kit.editing_mod = this;
                paint_kit.proxy_view = Dm.@new<atom_movable_screen_map_view>();
                paint_kit.proxy_view.generate_view(Dm.text("color_matrix_proxy_[]", REF(user.client)));

                paint_kit.proxy_view.appearance = paint_kit.editing_mod.appearance;
                paint_kit.proxy_view.color = null;
                await paint_kit.ui_interact(user);
                return ITEM_INTERACT_SUCCESS;
            }
            else // Left click
            {
                await paint_kit.paint_skin(this, user);
                return ITEM_INTERACT_SUCCESS;
            }
        }
        if (tool is obj_item_mod_module)
        {
            if (!Dm.truthy(open))
            {
                balloon_alert(user, I18n.T("cover closed!"));
                playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
                return ITEM_INTERACT_BLOCKING;
            }
            await install(tool as obj_item_mod_module, user);
            SEND_SIGNAL(COMSIG_MOD_MODULE_ADDED, Dm.list(this, user));
            return ITEM_INTERACT_SUCCESS;
        }
        if (tool is obj_item_mod_core)
        {
            if (!Dm.truthy(open))
            {
                balloon_alert(user, I18n.T("cover closed!"));
                playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
                return ITEM_INTERACT_BLOCKING;
            }
            if (core != null)
            {
                balloon_alert(user, I18n.T("already has core!"));
                playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
                return ITEM_INTERACT_BLOCKING;
            }
            obj_item_mod_core attacking_core = tool as obj_item_mod_core;
            await attacking_core.install(this);
            balloon_alert(user, I18n.T("core installed"));
            playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE, SILENCED_SOUND_EXTRARANGE);
            return ITEM_INTERACT_SUCCESS;
        }
        if (Dm.truthy(open))
        {
            if (Dm.truthy(is_wire_tool(tool)))
            {
                await wires.interact(user);
                return ITEM_INTERACT_SUCCESS;
            }
            obj_item id = tool.GetID() as obj_item;
            if (id != null)
            {
                update_access(user, id as obj_item_card_id);
                return ITEM_INTERACT_SUCCESS;
            }
        }
        return await base.tool_act(user, tool, modifiers);
    }

    // tg: code/modules/mod/mod_control.dm:362
    [DmParams()]
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        obj_item_stock_parts_power_store cell = get_charge_source() as obj_item_stock_parts_power_store;
        if (!(cell is obj_item_stock_parts_power_store))
            return null;
        return cell;
    }

    // tg: code/modules/mod/mod_control.dm:368
    public override object GetAccess()
    {
        if (Dm.truthy(ai_controller) && Dm.truthy(req_access))
            return req_access.Copy();
        else
            return base.GetAccess();
    }

    // tg: code/modules/mod/mod_control.dm:374
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        locked = Dm.num(!Dm.truthy(locked));
        balloon_alert(user, I18n.T("access []", Dm.truthy(locked) ? I18n.T("locked") : I18n.T("unlocked")));
        return TRUE;
    }

    // tg: code/modules/mod/mod_control.dm:379
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (!Dm.truthy(active) || wearer == null)
            return dot;
        to_chat(wearer, span_notice(I18n.T("[] electromagnetic pulse detected!", Dm.gt(severity, 1) ? I18n.T("Light") : I18n.T("Strong"))));
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_CONTENTS)))
            return dot;
        if (selected_module != null)
            await selected_module.deactivate(display_message: TRUE);
        await wearer.apply_damage(Dm.num(Dm.div(5, severity)), BURN, spread_damage: TRUE);
        to_chat(wearer, span_danger(I18n.T("You feel [] heat up from the EMP, burning you slightly.", this)));
        if (!IS_UNCONSCIOUS(wearer) && prob(10))
            await wearer.emote("scream");
        return dot;
    }

    // tg: code/modules/mod/mod_control.dm:392
    public override async DmTask on_outfit_equip(mob_living_carbon_human outfit_wearer = null, object visuals_only = null, object item_slot = null)
    {
        await base.on_outfit_equip(outfit_wearer, visuals_only, item_slot);
        await quick_activation();
    }

    // tg: code/modules/mod/mod_control.dm:396
    public override async DmTask<object> doStrip(mob stripper = null, mob owner = null)
    {
        if (Dm.truthy(active) && !Dm.truthy(await toggle_activate(stripper, force_deactivate: TRUE)))
            return null;
        foreach (obj_item part in Dm.each_cast<obj_item>(get_parts()))
        {
            if (part.loc == this)
                continue;
            await retract(null, part);
        }
        return await base.doStrip(stripper, owner);
    }

    // tg: code/modules/mod/mod_control.dm:405
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]-[][]", skin, base_icon_state, Dm.truthy(active) ? "-sealed" : "");
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/mod/mod_control.dm:409
    [SignalHandler]
    public virtual void get_strippable_alternate_actions(obj_item source = null, atom owner = null, mob user = null, DmList alt_actions = null)
    {
        if (Dm.truthy(active))
            Dm.append(ref alt_actions, "deactivate_mod");
        else
            Dm.append(ref alt_actions, "activate_mod");
        if (Dm.truthy(check_retracted()))
            Dm.append(ref alt_actions, "deploy");
        else
            Dm.append(ref alt_actions, "undeploy");
    }

    // tg: code/modules/mod/mod_control.dm:421
    [SignalHandler]
    public virtual object do_strippable_action(obj_item source = null, atom owner = null, mob user = null, object action_key = null)
    {
        if (!isliving(user))
            return NONE;
        object sw__1 = action_key;
        if (Dm.eq(sw__1, "deploy") || Dm.eq(sw__1, "undeploy"))
        {
            owner.visible_message(
                span_warning(I18n.T("[] tries to [] []'s []...", user, action_key, owner, this)),
                span_userdanger(I18n.T("[] is trying to [] your []!", user, action_key, this)),
                blind_message: span_hear(I18n.T("You hear rustling.")),
                ignored_mobs: user);
            Waiting.Detach(attempt_strip_deploy(owner, user, action_key));
            return COMPONENT_ALT_ACTION_DONE;
        }
        else if (Dm.eq(sw__1, "activate_mod") || Dm.eq(sw__1, "deactivate_mod"))
        {
            owner.visible_message(
                span_warning(I18n.T("[] tries to press []'s []'s power button...", user, owner, this)),
                span_userdanger(I18n.T("[] is trying to press your []'s power button!", user, this)),
                blind_message: span_hear(I18n.T("You hear rustling.")),
                ignored_mobs: user);
            Waiting.Detach(attempt_strip_activate(owner, user));
            return COMPONENT_ALT_ACTION_DONE;
        }
        else
        {
            return NONE;
        }
    }

    // tg: code/modules/mod/mod_control.dm:450
    public virtual async DmTask attempt_strip_deploy(atom owner = null, mob user = null, object message = null)
    {
        if (!Dm.truthy(await do_after(user, strip_delay, owner)))
            return;
        owner.visible_message(
            span_warning(I18n.T("[] []s []'s [].", user, message, owner, this)),
            span_userdanger(I18n.T("[] []s your []!", user, message, this)),
            ignored_mobs: user);
        await quick_deploy(user);
    }

    // tg: code/modules/mod/mod_control.dm:460
    public virtual async DmTask attempt_strip_activate(atom owner = null, mob user = null)
    {
        if (!Dm.truthy(await do_after(user, strip_delay, owner)))
            return;
        owner.visible_message(
            span_warning(I18n.T("[] presses []'s []'s power button.", user, owner, this)),
            span_userdanger(I18n.T("[] presses your []'s power button!", user, this)),
            ignored_mobs: user);
        await toggle_activate(user);
    }

    // tg: code/modules/mod/mod_control.dm:471
    public virtual object get_parts(float? all = null)
    {
        all ??= FALSE;
        DmList dot = null;
        dot = Dm.list();
        foreach (object key in Dm.each(mod_parts))
        {
            datum_mod_part part = mod_parts[key] as datum_mod_part;
            if (!Dm.truthy(all) && part.part_item == this)
                continue;
            Dm.append(ref dot, part.part_item);
        }
        return dot;
    }

    // tg: code/modules/mod/mod_control.dm:479
    public virtual object get_part_datums(float? all = null)
    {
        all ??= FALSE;
        DmList dot = null;
        dot = Dm.list();
        foreach (object key in Dm.each(mod_parts))
        {
            datum_mod_part part = mod_parts[key] as datum_mod_part;
            if (!Dm.truthy(all) && part.part_item == this)
                continue;
            Dm.append(ref dot, part);
        }
        return dot;
    }

    // tg: code/modules/mod/mod_control.dm:487
    public virtual object get_part_datum(obj_item part = null)
    {
        datum_mod_part potential_part = mod_parts[Dm.text("[]", part.slot_flags)] as datum_mod_part;
        if (potential_part?.part_item == part)
            return potential_part;
        foreach (datum_mod_part mod_part in Dm.each<datum_mod_part>(get_part_datums()))
        {
            if (mod_part.part_item == part)
                return mod_part;
        }
        throw Dm.CRASH(Dm.text("get_part_datum called with incorrect item [] passed.", part), "code/modules/mod/mod_control.dm", 495);
    }

    // tg: code/modules/mod/mod_control.dm:497
    public virtual object get_part_from_slot(object slot = null)
    {
        return (get_part_datum_from_slot(slot) as datum_mod_part)?.part_item;
    }

    // tg: code/modules/mod/mod_control.dm:501
    public virtual object get_part_datum_from_slot(object slot = null)
    {
        foreach (object part_key in Dm.each(mod_parts))
        {
            if (Dm.truthy(Dm.bitand(text2num(part_key), slot)))
                return mod_parts[part_key];
        }
        return null;
    }

    // tg: code/modules/mod/mod_control.dm:507
    public virtual async DmTask set_wearer(mob_living_carbon_human user = null)
    {
        if (wearer == user)
            throw Dm.CRASH(Dm.text("set_wearer() was called with the new wearer being the current wearer: []", wearer), "code/modules/mod/mod_control.dm", 509);
        else if (wearer != null)
            stack_trace("set_wearer() was called with a new wearer without unset_wearer() being called", "code/modules/mod/mod_control.dm", 511);

        wearer = user;
        SEND_SIGNAL(COMSIG_MOD_WEARER_SET, Dm.list(this, wearer));
        RegisterSignal(wearer, COMSIG_ATOM_EXITED, nameof(on_exit));
        RegisterSignal(wearer, COMSIG_SPECIES_GAIN, nameof(on_species_gain));
        RegisterSignal(wearer, COMSIG_MOB_CLICKON, nameof(click_on));
        update_charge_alert();
        foreach (obj_item_mod_module module in Dm.each_cast<obj_item_mod_module>(modules))
            module.on_equip();
    }

    // tg: code/modules/mod/mod_control.dm:522
    public virtual void unset_wearer()
    {
        foreach (obj_item_mod_module module in Dm.each_cast<obj_item_mod_module>(modules))
            module.on_unequip();
        UnregisterSignal(wearer, Dm.list(COMSIG_ATOM_EXITED, COMSIG_SPECIES_GAIN, COMSIG_MOB_CLICKON));
        SEND_SIGNAL(COMSIG_MOD_WEARER_UNSET, Dm.list(this, wearer));
        atom_movable_screen_spacesuit spacesuit_hud = wearer.hud_used?.screen_objects?[HUD_MOB_SPACESUIT] as atom_movable_screen_spacesuit;
        if (spacesuit_hud != null)
            spacesuit_hud.update_spacesuit_hud_icon();
        wearer = null;
    }

    // tg: code/modules/mod/mod_control.dm:532
    public virtual object get_sealed_slots(DmList parts = null)
    {
        object covered_slots = NONE;
        foreach (obj_item part in Dm.each_cast<obj_item>(parts))
        {
            if (!Dm.truthy(((datum_mod_part)get_part_datum(part)).@sealed))
            {
                Dm.remove(ref parts, part);
                continue;
            }
            Dm.combine(ref covered_slots, part.slot_flags);
        }
        return covered_slots;
    }

    // tg: code/modules/mod/mod_control.dm:541
    public virtual async DmTask clean_up()
    {
        if (QDELING(this))
        {
            unset_wearer();
            return;
        }
        if (Dm.truthy(active) || Dm.truthy(activating))
        {
            foreach (obj_item_mod_module module in Dm.each_cast<obj_item_mod_module>(modules))
            {
                if (!Dm.truthy(module.active))
                    continue;
                await module.deactivate(display_message: FALSE);
            }
            foreach (obj_item part in Dm.each_cast<obj_item>(get_parts()))
                await seal_part(part as obj_item_clothing, is_sealed: FALSE);
        }
        foreach (obj_item part in Dm.each_cast<obj_item>(get_parts()))
        {
            if (part.loc == this)
                continue;
            Waiting.Detach(retract(wearer, part, TRUE)); // async to appease spaceman DMM because the branch we don't run has a do_after
        }
        if (Dm.truthy(active))
        {
            await control_activation(is_on: FALSE);
            mod_link?.end_call();
        }
        mob old_wearer = wearer;
        unset_wearer();
        await old_wearer.temporarilyRemoveItemFromInventory(this);
    }

    // tg: code/modules/mod/mod_control.dm:563
    [SignalHandler]
    public virtual void on_species_gain(datum source = null, datum_species new_species = null, datum_species old_species = null, object pref_load = null, object regenerate_icons = null)
    {
        foreach (obj_item part in Dm.each<obj_item>(get_parts(all: TRUE)))
        {
            if (!Dm.truthy(Dm.bitand(new_species.no_equip_flags, part.slot_flags)) || Dm.truthy(is_type_in_list(new_species, part.species_exception)))
                continue;
            Waiting.NoSleep(forceMove(drop_location() as atom));
            return;
        }
    }

    // tg: code/modules/mod/mod_control.dm:572
    [SignalHandler]
    public virtual object click_on(mob source = null, atom A = null, DmList modifiers = null)
    {
        if (Dm.truthy(LAZYACCESS(modifiers, CTRL_CLICK)) && Dm.truthy(LAZYACCESS(modifiers, (source.client?.prefs.read_preference(typeof(datum_preference_choiced_mod_select)) is var or__1 && Dm.truthy(or__1) ? or__1 : MIDDLE_CLICK))))
        {
            Waiting.Detach(quick_module(source, get_turf(A)));
            return COMSIG_MOB_CANCEL_CLICKON;
        }
        return null;
    }

    // tg: code/modules/mod/mod_control.dm:579
    public virtual async DmTask quick_module(mob user = null, object anchor_override = null)
    {
        if (!Dm.truthy(length(modules)))
            return;
        DmList display_names = Dm.list();
        DmList items = Dm.list();
        foreach (obj_item_mod_module module in Dm.each_cast<obj_item_mod_module>(modules))
        {
            if (module.module_type == MODULE_PASSIVE)
                continue;
            display_names[module.name] = REF(module);
            image module_image = image(icon: module.icon, icon_state: module.icon_state);
            if (module == selected_module)
                Dm.append(ref module_image.underlays, image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "module_selected"));
            else if (Dm.truthy(module.active))
                Dm.append(ref module_image.underlays, image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "module_active"));
            if (!(module.cooldown_timer <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline
                // module_image.add_overlay(...): /image/proc/add_overlay(x) is `overlays |= x` (controllers/subsystem/overlays.dm:113)
                Dm.combine(ref module_image.overlays, image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "module_cooldown"));
            Dm.append(ref items, Dm.list(Dm.kv(module.name, module_image)));
        }
        if (!Dm.truthy(length(items)))
            return;
        atom radial_anchor = this;
        if (user.loc is obj_effect_dummy_phased_mob)
            radial_anchor = get_turf(user.loc); //they're phased out via some module, anchor the radial on the turf so it may still display
        if (anchor_override != null)
            radial_anchor = anchor_override as atom;
        object pick = await show_radial_menu(user, radial_anchor, items, custom_check: null, require_near: Dm.num(anchor_override == null), tooltips: TRUE, user_space: Dm.num(anchor_override != null));
        if (!Dm.truthy(pick))
            return;
        object module_reference = display_names[pick];
        obj_item_mod_module picked_module = Dm.locate_ref(module_reference) is obj_item_mod_module found && Dm.@in(found, modules) ? found : null; // locate(module_reference) in modules
        if (!(picked_module is obj_item_mod_module))
            return;
        await picked_module.on_select(user);
    }

    // tg: code/modules/mod/mod_control.dm:612
    public override async DmTask<object> shock__proc(mob_living shocking = null, float? chance = null, object shock_source = null, float? siemens_coeff = null)
    {
        if (Dm.lt(get_charge(), 1))
            return FALSE;
        if (siemens_coeff == null)
            siemens_coeff = 0.7f;
        return await base.shock__proc(shocking, chance, shock_source, siemens_coeff);
    }

    // tg: code/modules/mod/mod_control.dm:619
    public virtual async DmTask install(obj_item_mod_module new_module = null, mob user = null)
    {
        // InertSeam (gaps MO1, D132; narrowed by gaps MO2): a module whose own tg code is not translated yet (a space ninja's
        // module, M10+, or the circuit module, wiremod) is refused with tg's "not ported yet" balloon, so that its inert type never runs
        // as the base module.
        if (InertSeam.MissingInitialize(new_module, "install", user))
            return;
        foreach (obj_item_mod_module old_module in Dm.each_cast<obj_item_mod_module>(modules))
        {
            if (Dm.truthy(is_type_in_list(new_module, old_module.incompatible_modules)) || Dm.truthy(is_type_in_list(old_module, new_module.incompatible_modules)))
            {
                if (user != null)
                {
                    balloon_alert(user, I18n.T("incompatible with []!", old_module));
                    playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
                }
                return;
            }
        }
        float complexity_with_module = complexity;
        complexity_with_module += new_module.complexity;
        if (complexity_with_module > complexity_max)
        {
            if (user != null)
            {
                balloon_alert(user, I18n.T("above complexity max!"));
                playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
            }
            return;
        }
        if (!Dm.truthy(new_module.has_required_parts(mod_parts)))
        {
            if (user != null)
            {
                balloon_alert(user, I18n.T("lacking required parts!"));
                playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
            }
            return;
        }
        if (!Dm.truthy(new_module.can_install(this)))
        {
            if (user != null)
            {
                balloon_alert(user, I18n.T("can't install!"));
                playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
            }
            return;
        }
        if (Dm.truthy(Dm.bitand(SEND_SIGNAL(COMSIG_MOD_TRY_INSTALL_MODULE, Dm.list(this, new_module, user)), MOD_ABORT_INSTALL)))
            return;
        if (Dm.truthy(Dm.bitand(new_module.SEND_SIGNAL(COMSIG_MODULE_TRY_INSTALL, Dm.list(new_module, this, user)), MOD_ABORT_INSTALL)))
            return;
        await finish_install(new_module, user);
    }

    // tg: code/modules/mod/mod_control.dm:649
    public virtual async DmTask finish_install(obj_item_mod_module new_module = null, mob user = null)
    {
        await new_module.forceMove(this);
        Dm.append(ref modules, new_module);
        complexity += new_module.complexity;
        new_module.mod = this;
        await new_module.on_install();
        if (wearer != null)
            new_module.on_equip();
        if (Dm.truthy(active) && Dm.truthy(new_module.has_required_parts(mod_parts, need_active: TRUE)))
        {
            await new_module.on_part_activation();
            new_module.part_activated = TRUE;
        }
        if (user != null)
        {
            balloon_alert(user, I18n.T("[] added", new_module));
            playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE, SILENCED_SOUND_EXTRARANGE);
        }
    }

    // tg: code/modules/mod/mod_control.dm:664
    public virtual async DmTask uninstall(obj_item_mod_module old_module = null, float? deleting = null)
    {
        deleting ??= FALSE;
        Dm.remove(ref modules, old_module);
        complexity -= old_module.complexity;
        if (wearer != null)
            old_module.on_unequip();
        if (Dm.truthy(active))
        {
            await old_module.on_part_deactivation(deleting: deleting);
            if (Dm.truthy(old_module.active))
                await old_module.deactivate(display_message: Dm.not(deleting), deleting: deleting);
        }
        await old_module.on_uninstall(deleting: deleting);
        QDEL_LIST_ASSOC_VAL(old_module.pinned_to);
        old_module.mod = null;
    }

    /// Intended for callbacks, don't use normally, just get wearer by itself.
    // tg: code/modules/mod/mod_control.dm:678
    public virtual object get_wearer()
    {
        return wearer;
    }

    // tg: code/modules/mod/mod_control.dm:681
    public virtual void update_access(mob user = null, obj_item_card_id card = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(allowed__proc(user))))
        {
            balloon_alert(user, I18n.T("insufficient access!"));
            playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
            return;
        }
        req_access = card.access.Copy();
        balloon_alert(user, I18n.T("access updated"));
    }

    // tg: code/modules/mod/mod_control.dm:689
    public virtual object get_charge_source()
    {
        return core?.charge_source();
    }

    // tg: code/modules/mod/mod_control.dm:692
    public virtual object get_charge()
    {
        return (core?.charge_amount() is var or__1 && Dm.truthy(or__1) ? or__1 : 0);
    }

    // tg: code/modules/mod/mod_control.dm:695
    public virtual object get_max_charge()
    {
        return (core?.max_charge_amount() is var or__1 && Dm.truthy(or__1) ? or__1 : 1); //avoid dividing by 0
    }

    // tg: code/modules/mod/mod_control.dm:698
    public virtual object get_charge_percent()
    {
        return ROUND_UP(Dm.num(Dm.div(get_charge(), get_max_charge())) * 100);
    }

    // tg: code/modules/mod/mod_control.dm:701
    public virtual async DmTask<object> add_charge(object amount = null)
    {
        object result = core != null ? await core.add_charge(amount) : null;
        return (result is var or__1 && Dm.truthy(or__1) ? or__1 : FALSE);
    }

    // tg: code/modules/mod/mod_control.dm:704
    public virtual async DmTask<object> subtract_charge(object amount = null)
    {
        object result = core != null ? await core.subtract_charge(amount) : null;
        return (result is var or__1 && Dm.truthy(or__1) ? or__1 : FALSE);
    }

    // tg: code/modules/mod/mod_control.dm:707
    public virtual object check_charge(object amount = null)
    {
        return (core?.check_charge(amount) is var or__1 && Dm.truthy(or__1) ? or__1 : FALSE);
    }

    // tg: code/modules/mod/mod_control.dm:710
    public virtual object get_chargebar_color()
    {
        return (core?.get_chargebar_color() is var or__1 && Dm.truthy(or__1) ? or__1 : "transparent");
    }

    // tg: code/modules/mod/mod_control.dm:713
    public virtual object get_chargebar_string()
    {
        return (core?.get_chargebar_string() is var or__1 && Dm.truthy(or__1) ? or__1 : "No Core Detected");
    }

    /**
     * Updates the wearer's hud according to the current state of the MODsuit
     */
    // tg: code/modules/mod/mod_control.dm:719
    public virtual void update_charge_alert()
    {
        if (wearer == null || wearer.client == null)
            return;
        object state_to_use = null;
        if (!Dm.truthy(active))
            state_to_use = SPACESUIT_NO_ICON;
        else if (core == null)
            state_to_use = "coreless";
        else
            state_to_use = core.get_charge_icon_state();

        atom_movable_screen_spacesuit spacesuit_hud = wearer.hud_used?.screen_objects?[HUD_MOB_SPACESUIT] as atom_movable_screen_spacesuit;
        if (spacesuit_hud != null)
            spacesuit_hud.update_spacesuit_hud_icon(state_to_use is var or__1 && Dm.truthy(or__1) ? or__1 : SPACESUIT_NO_ICON);
    }

    // tg: code/modules/mod/mod_control.dm:734
    public virtual async DmTask update_speed()
    {
        float total_slowdown = 0;
        bool prevent_slowdown = HAS_TRAIT(this, TRAIT_SPEED_POTIONED);
        if (!prevent_slowdown)
            total_slowdown += slowdown_deployed;

        DmList module_slowdowns = Dm.list();
        SEND_SIGNAL(COMSIG_MOD_UPDATE_SPEED, Dm.list(this, module_slowdowns, Dm.num(prevent_slowdown)));
        foreach (object module_slow in Dm.each(module_slowdowns))
            total_slowdown += Dm.num(module_slow);

        foreach (datum_mod_part part_datum in Dm.each_cast<datum_mod_part>(get_part_datums(all: TRUE)))
        {
            obj_item part = part_datum.part_item;
            part.slowdown = Dm.div(total_slowdown, length(mod_parts));
            if (!Dm.truthy(part_datum.@sealed))
                part.slowdown = max(part.slowdown, 0);
            if (part is obj_item_clothing_shoes_mod)
            {
                obj_item_clothing_shoes_mod shoe_part = part as obj_item_clothing_shoes_mod;
                await shoe_part.update_footstep_sounds();
            }
        }
        if (wearer != null)
            await wearer.update_equipment_speed_mods();
    }

    // tg: code/modules/mod/mod_control.dm:755
    public virtual async DmTask power_off()
    {
        balloon_alert(wearer, I18n.T("no power!"));
        await toggle_activate(wearer, force_deactivate: TRUE);
    }

    // tg: code/modules/mod/mod_control.dm:759
    public virtual async DmTask set_mod_color(object new_color = null)
    {
        foreach (obj_item part in Dm.each_cast<obj_item>(get_parts(all: TRUE)))
        {
            await part.remove_atom_colour(WASHABLE_COLOUR_PRIORITY);
            await part.add_atom_colour(new_color, FIXED_COLOUR_PRIORITY);
        }
        if (wearer != null)
            await wearer.regenerate_icons();
    }

    // tg: code/modules/mod/mod_control.dm:765
    [SignalHandler]
    public virtual void on_exit(datum source = null, atom_movable part = null, object direction = null)
    {
        if (part.loc == this)
            return;
        if (part == core)
        {
            Waiting.NoSleep(core.uninstall());
            return;
        }
        if (part.loc == wearer)
            return;
        if (Dm.@in(part, modules))
        {
            Waiting.NoSleep(uninstall(part as obj_item_mod_module));
            return;
        }
        if (Dm.@in(part, get_parts()))
        {
            if (QDELING(part) && !QDELING(this))
            {
                qdel(this);
                return;
            }
            datum_mod_part part_datum = get_part_datum(part as obj_item) as datum_mod_part;
            if (Dm.truthy(part_datum.@sealed))
                Waiting.NoSleep(seal_part(part as obj_item_clothing, is_sealed: FALSE));
            if (part.loc == null)
                return;
            if (wearer == null)
            {
                Waiting.NoSleep(part.forceMove(this));
                return;
            }
            Waiting.Detach(retract(wearer, part as obj_item, TRUE)); // async to appease spaceman DMM because the branch we don't run has a do_after
        }
    }

    // tg: code/modules/mod/mod_control.dm:792
    [SignalHandler]
    public virtual void on_part_destruction(obj_item part = null, object damage_flag = null)
    {
        if (QDELING(this))
            return;
        Waiting.NoSleep(atom_destruction(damage_flag));
    }

    // tg: code/modules/mod/mod_control.dm:799
    [SignalHandler]
    public virtual void on_overslot_exit(obj_item part = null, atom_movable overslot = null, object direction = null)
    {
        datum_mod_part part_datum = get_part_datum(part) as datum_mod_part;
        if (overslot != part_datum.overslotting)
            return;
        UnregisterSignal(part, COMSIG_ATOM_EXITED);
        part_datum.overslotting = null;
    }

    // tg: code/modules/mod/mod_control.dm:808
    [SignalHandler]
    public virtual object on_potion(atom_movable source = null, obj_item_slimepotion_speed speed_potion = null, mob_living user = null)
    {
        if (HAS_TRAIT(this, TRAIT_SPEED_POTIONED))
        {
            to_chat(user, span_warning(I18n.T("[] has already been coated with red, that's as fast as it'll go!", this)));
            return SPEED_POTION_STOP;
        }

        if (Dm.truthy(active))
        {
            to_chat(user, span_warning(I18n.T("It's too dangerous to smear [] on [] while it's active!", speed_potion, this)));
            return SPEED_POTION_STOP;
        }

        to_chat(user, span_notice(I18n.T("You slather the red gunk over [], making it faster.", this)));
        Waiting.NoSleep(set_mod_color(color_transition_filter(COLOR_RED)));
        ADD_TRAIT(this, TRAIT_SPEED_POTIONED, SLIME_POTION_TRAIT);
        Waiting.NoSleep(update_speed());
        qdel(speed_potion);
        return SPEED_POTION_STOP;
    }

    /// Disables the mod link frequency attached to this unit.
    // tg: code/modules/mod/mod_control.dm:827
    public virtual void disable_modlink()
    {
        if (mod_link == null)
            return;

        mod_link.end_call();
        mod_link.frequency = null;
    }

    // tg: code/modules/mod/mod_control.dm:834
    public virtual object get_visor_overlay(mutable_appearance standing = null)
    {
        DmList overrides = Dm.list();
        SEND_SIGNAL(COMSIG_MOD_GET_VISOR_OVERLAY, Dm.list(this, standing, overrides));
        if (Dm.truthy(length(overrides)))
            return overrides[1];
        return mutable_appearance(worn_icon, Dm.text("[]-helmet-visor", skin), layer: standing.layer + 0.1f);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mod/mod_control.dm:82  var/static/movedelay = 0
    internal float obj_item_mod_control__movedelay = 0;
}
