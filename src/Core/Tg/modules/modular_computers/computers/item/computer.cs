// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/computers/item/computer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice E: the PDA's modular computer): the computer item, its ID slots, its programs' start and shutdown, its
// NTNet signal, its flashlight, its ring and alerts, its tools, its examine and screentips, its overlays and the items it takes.
// Left out with a comment or a marked leaf: the TTS voice
// (LateInitialize, after the core version), the photos (save_photo, photo_act: the camera), the pAI card's messenger ability (pAIs),
// (the printer's paper came with the paperwork, gaps S3), the computer disks (computer_disk_act and the disk eject: the
// disks), attack_ghost's admin override (admin rights, M10), the saboteur's
// disruption (on_saboteur: the saboteur handgun) (the security level's relevancy since gaps R4).
// The var block is generated (D111) but for internal_cell and inserted_disk, which hold a type path until Initialize makes the
// item (so the type's constructor and its list defaults are here too, the Hand mode).

// This is the base type of computer
// Other types expand it - tablets and laptops are subtypes
// consoles use "procssor" item that is held inside it.
// tg: code/modules/modular_computers/computers/item/computer.dm:4
public partial class obj_item_modular_computer : obj_item
{
    ///The disk in this PDA. If set, this will be inserted on Initialize.
    // tg: code/modules/modular_computers/computers/item/computer.dm:23  (a type path until Initialize makes the disk)
    public object inserted_disk;
    ///The power cell the computer uses to run on.
    // tg: code/modules/modular_computers/computers/item/computer.dm:25  (a type path until Initialize makes the cell)
    public object internal_cell = typeof(obj_item_stock_parts_power_store_cell);

    ///Static list of default programs that come with ALL computers, here so computers don't have to repeat this.
    // tg: code/modules/modular_computers/computers/item/computer.dm:41  var/static/list/datum/computer_file/default_programs
    public DmList default_programs => TgState.Current.obj_item_modular_computer__default_programs ??= Dm.list(
        typeof(datum_computer_file_program_themeify),
        typeof(datum_computer_file_program_ntnetdownload),
        typeof(datum_computer_file_program_filemanager));

    // tg: code/modules/modular_computers/computers/item/computer.dm:4  (the type's own vars, kept with the Hand mode)
    public obj_item_modular_computer()
    {
        name = "modular microcomputer";
        desc = "A small portable microcomputer.";
        icon = Dm.resource("icons/obj/machines/computer.dmi");
        icon_state = "laptop";
        light_on = 0;
        light_power = 1.2f;
        integrity_failure = 0.5f;
        max_integrity = 100;
        armor_type = typeof(datum_armor_item_modular_computer);
        light_system = OVERLAY_LIGHT_DIRECTIONAL;
        interaction_flags_mouse_drop = NEED_HANDS | ALLOW_RESTING;
        voice_filter = "alimiter=0.9,acompressor=threshold=0.2:ratio=20:attack=10:release=50:makeup=2,highpass=f=1000";
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:36  (the type's list defaults, kept with the Hand mode)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        stored_files = Dm.list();
        starting_programs = Dm.list();
        idle_threads = Dm.list();
    }

    /// The power cell, once Initialize made it.
    public obj_item_stock_parts_power_store cell => internal_cell as obj_item_stock_parts_power_store;

    // tg: code/modules/modular_computers/computers/item/computer.dm:135
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSobj, this);
        if (physical == null)
        {
            physical = this;
            Waiting.NoSleep(add_shell_component(shell_capacity));
        }
        set_light_color(comp_light_color);
        set_light_range(comp_light_luminosity);
        if (Dm.truthy(looping_sound))
            soundloop = Dm.@new<datum_looping_sound_computer>(this, enabled);
        UpdateDisplay();
        if (Dm.truthy(has_light))
            Waiting.NoSleep(add_item_action(typeof(datum_action_item_action_toggle_computer_light)));
        if (inserted_disk is Type disk_type)
            inserted_disk = Dm.@new(disk_type, this);
        if (internal_cell is Type cell_type)
            internal_cell = Dm.@new(cell_type, this);

        Waiting.NoSleep(AddElement(typeof(datum_element_drag_to_activate)));
        install_default_programs();
        register_context();
        update_appearance();
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:159
    public override void LateInitialize(params object[] __extra)
    {
        if (Dm.truthy(SStts?.tts_enabled))
            voice = SStts.computer_voice;
    }

    ///Initialize the shell for this item, or the physical machinery it belongs to.
    // tg: code/modules/modular_computers/computers/item/computer.dm:164
    public virtual async DmTask add_shell_component(float? capacity = null, float? shell_flags = null)
    {
        capacity ??= SHELL_CAPACITY_MEDIUM;
        shell_flags ??= NONE;
        shell = await physical.AddComponent(typeof(datum_component_shell), Dm.list(Dm.@new<obj_item_circuit_component_modpc>()), capacity, shell_flags) as datum_component_shell;
        RegisterSignal(shell, COMSIG_SHELL_CIRCUIT_ATTACHED, nameof(on_circuit_attached));
        RegisterSignal(shell, COMSIG_SHELL_CIRCUIT_REMOVED, nameof(on_circuit_removed));
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:169
    [SignalHandler]
    public virtual void on_circuit_attached(datum source = null)
    {
        RegisterSignal(shell.attached_circuit, COMSIG_CIRCUIT_PRE_POWER_USAGE, nameof(use_energy_for_circuits));
    }

    ///Try to draw power from our internal cell first, before switching to that of the circuit.
    // tg: code/modules/modular_computers/computers/item/computer.dm:174
    [SignalHandler]
    public virtual object use_energy_for_circuits(datum source = null, object energy_usage_per_input = null)
    {
        if (Dm.truthy(use_energy(energy_usage_per_input, check_programs: FALSE)))
            return COMPONENT_OVERRIDE_POWER_USAGE;
        return null;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:179
    [SignalHandler]
    public virtual void on_circuit_removed(datum source = null)
    {
        UnregisterSignal(shell.attached_circuit, COMSIG_CIRCUIT_PRE_POWER_USAGE);
    }


    // tg: code/modules/modular_computers/computers/item/computer.dm:183
    public virtual void install_default_programs()
    {
        foreach (Type programs in Dm.each_cast<Type>(Dm.add(default_programs, starting_programs)))
        {
            datum_computer_file program_type = Dm.@new(programs) as datum_computer_file;
            store_file(program_type);
        }
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:189
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        close_all_programs();
        //Some components will actually try and interact with this, so let's do it later
        QDEL_NULL(ref soundloop);
        looping_sound = FALSE; // Necessary to stop a possible runtime trying to call soundloop.stop() when soundloop has been qdel'd
        QDEL_LIST(stored_files);

        if (inserted_disk is obj_item)
        {
            qdel(inserted_disk);
            inserted_disk = null;
        }
        if (inserted_pai is obj_item_pai_card)
            QDEL_NULL(ref inserted_pai);

        QDEL_NULL(ref stored_id);
        QDEL_NULL(ref alt_stored_id);

        shell = null;
        physical = null;
        return base.Destroy(force);
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:209
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (active_program != null && Dm.truthy(await active_program.tap(interacting_with, user, modifiers)))
        {
            await user.do_attack_animation(interacting_with); //Emulate this animation since we kill the attack in three lines
            playsound(loc, Dm.resource("sound/items/weapons/tap.ogg"), get_clamped_volume(), TRUE, -1); //Likewise for the tap sound
            _addtimer(Dm.@new<datum_callback>(this, nameof(play_ping)), 0.5f * SECONDS, TIMER_UNIQUE, file: "code/modules/modular_computers/computers/item/computer.dm", line: 213); //Slightly delayed ping to indicate success
            return ITEM_INTERACT_SUCCESS;
        }
        return await base.interact_with_atom_secondary(interacting_with, user, modifiers);
    }

    // shameless copy of newscaster photo saving
    // tg: code/modules/modular_computers/computers/item/computer.dm:219  save_photo: the photos (the camera and the log directory's
    // photos) are not ported

    /**
     * Plays a ping sound.
     *
     * Timers runtime if you try to make them call playsound. Yep.
     */
    // tg: code/modules/modular_computers/computers/item/computer.dm:233
    public virtual void play_ping()
    {
        playsound(loc, Dm.resource("sound/machines/ping.ogg"), get_clamped_volume(), FALSE, -1);
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:236
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        return internal_cell is obj_item ? internal_cell : null;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:239
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (issilicon(user))
            return NONE;

        if (Dm.truthy(await remove_id(user)))
            return CLICK_ACTION_SUCCESS;

        if (inserted_pai is obj_item_pai_card) // Remove pAI
        {
            remove_pai(user);
            return CLICK_ACTION_SUCCESS;
        }

        return CLICK_ACTION_BLOCKING;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:252
    public override async DmTask<object> click_alt_secondary(mob user = null)
    {
        if (issilicon(user))
            return NONE;

        if (Dm.truthy(remove_secondary_id(user)))
            return CLICK_ACTION_SUCCESS;

        return NONE;
    }

    // Gets IDs/access levels from card slot. Would be useful when/if PDAs would become modular PCs. //guess what
    // tg: code/modules/modular_computers/computers/item/computer.dm:262
    public override object GetAccess()
    {
        if (stored_id != null)
            return stored_id.GetAccess();
        return base.GetAccess();
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:267
    public override object GetID()
    {
        if (stored_id != null)
            return stored_id;
        return base.GetID();
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:272
    public override object get_id_examine_strings(mob user = null)
    {
        object dot = base.get_id_examine_strings(user);
        if (stored_id != null)
        {
            Dm.append(ref dot, Dm.text("[] is displaying []:", this, stored_id));
            Dm.append(ref dot, stored_id.get_id_examine_strings(user));
        }
        return dot;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:278  (gaps S3: the printed paper)
    public virtual object print_text(object text_to_print = null, object paper_title = null)
    {
        paper_title ??= "";
        if (!Dm.truthy(stored_paper))
            return FALSE;

        obj_item_paper printed_paper = Dm.@new<obj_item_paper>(drop_location());
        printed_paper.add_raw_text(text_to_print);
        if (Dm.truthy(paper_title))
            printed_paper.name = Dm.str(paper_title);
        printed_paper.update_appearance();
        stored_paper--;
        return TRUE;
    }

    /**
     * Attempt to insert the ID in either card slot, if ID is present - attempts swap
     *
     * Args:
     * inserting_id - the ID being inserted
     * user - The person inserting the ID
     */
    // tg: code/modules/modular_computers/computers/item/computer.dm:297
    [DmParams("inserting_id", "user")]
    public override async DmTask<object> insert_id(params object[] __extra)
    {
        obj_item_card inserting_id = Dm.arg(__extra, 0) as obj_item_card;
        mob user = Dm.arg(__extra, 1) as mob;
        if (user != null && !Dm.truthy(await user.transferItemToLoc(inserting_id, this)))
            return FALSE;
        else
            await inserting_id.forceMove(this);

        if (stored_id != null)
            await remove_id(user, TRUE);

        stored_id = inserting_id as obj_item_card_id;

        if (user != null)
        {
            to_chat(user, span_notice(I18n.T("You insert \\the [] into the card slot.", inserting_id)));
            balloon_alert(user, I18n.T("inserted ID"));
        }

        playsound(this, Dm.resource("sound/machines/terminal/terminal_insert_disc.ogg"), 50, FALSE);

        if (loc is mob_living_carbon_human human_wearer)
        {
            if (human_wearer.wear_id == this)
                human_wearer.update_ID_card();
        }

        update_appearance();
        update_slot_icon();
        if (Dm.truthy(this._listen_lookup?[COMSIG_MODULAR_COMPUTER_INSERTED_ID]))
            this._SendSignal(COMSIG_MODULAR_COMPUTER_INSERTED_ID, Dm.list(this, inserting_id, user));
        return TRUE;
    }

    /**
     * Attempts to insert a secondary ID card into the computer. If there is already a secondary ID card, attempts to swap
     *
     * Args:
     * * secondary_id - The ID card to insert into the secondary slot.
     * * user - The mob trying to insert the ID, if there is one.
     */
    // tg: code/modules/modular_computers/computers/item/computer.dm:332
    public virtual async DmTask<object> insert_secondary_id(obj_item_card_id secondary_id = null, mob user = null)
    {
        if (alt_stored_id != null)
            remove_secondary_id(user, TRUE);

        if (!Dm.truthy(await user.transferItemToLoc(secondary_id, this)))
            return FALSE;

        alt_stored_id = secondary_id;
        if (user != null)
        {
            to_chat(user, span_notice(I18n.T("You insert \\the [] into the secondary card slot.", secondary_id)));
            balloon_alert(user, I18n.T("inserted secondary ID"));
        }
        playsound(this, Dm.resource("sound/machines/terminal/terminal_insert_disc.ogg"), 50, FALSE);

        return TRUE;
    }

    /**
     * Removes the alt ID card from the computer, and puts it in loc's hand if it's a mob
     *
     * Args:
     * * user - The mob trying to remove the ID, if there is one
     * * silent - Boolean, determines whether fluff text would be printed
     */
    // tg: code/modules/modular_computers/computers/item/computer.dm:354
    public virtual object remove_secondary_id(mob user = null, object silent = null)
    {
        if (alt_stored_id == null)
            return FALSE;

        if (!issilicon(user) && Dm.truthy(in_range(this, user)))
            Waiting.NoSleep(user.put_in_hands(alt_stored_id));
        else
            Waiting.NoSleep(alt_stored_id.forceMove(drop_location() as atom));

        obj_item lost_id = alt_stored_id;
        alt_stored_id = null;

        if (!Dm.truthy(silent) && user != null)
        {
            to_chat(user, span_notice(I18n.T("You remove \\the [] from the secondary card slot.", lost_id)));
            balloon_alert(user, I18n.T("removed secondary ID"));
        }
        playsound(this, Dm.resource("sound/machines/terminal/terminal_insert_disc.ogg"), 50, FALSE);

        return TRUE;
    }

    /**
     * Removes the ID card from the computer, and puts it in loc's hand if it's a mob
     * Args:
     * * user - The mob trying to remove the ID, if there is one
     * * silent - Boolean, determines whether fluff text would be printed
     */
    // tg: code/modules/modular_computers/computers/item/computer.dm:379
    [DmParams("user", "silent")]
    public override async DmTask<object> remove_id(params object[] __extra)
    {
        mob user = Dm.arg(__extra, 0) as mob;
        object silent = Dm.arg(__extra, 1);
        obj_item lost_id = stored_id;
        if (stored_id == null)
            return await base.remove_id(__extra);

        if (Dm.truthy(crew_manifest_update))
            GLOB.manifest.modify(stored_id.registered_name, stored_id.assignment, stored_id.get_trim_assignment());

        if (user != null && !issilicon(user) && Dm.truthy(in_range(this, user)))
            await user.put_in_hands(stored_id);
        else
            await stored_id.forceMove(drop_location() as atom);
        stored_id = null;

        if (!Dm.truthy(silent) && user != null)
        {
            to_chat(user, span_notice(I18n.T("You remove \\the [] from the card slot.", lost_id)));
            balloon_alert(user, I18n.T("removed ID"));
        }
        playsound(this, Dm.resource("sound/machines/terminal/terminal_insert_disc.ogg"), 50, FALSE);

        if (loc is mob_living_carbon_human human_wearer)
        {
            if (human_wearer.wear_id == this)
                human_wearer.update_ID_card();
        }

        update_slot_icon();
        update_appearance();
        return TRUE;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:407  attack_ai: the silicons' clicks (a later milestone)
    // tg: code/modules/modular_computers/computers/item/computer.dm:410
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        object dot = await base.attack_ghost(user);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(enabled))
            await ui_interact(user);
        // else if(isAdminGhostAI(user)) ... turn_on(user): the admin ghost's override (admin tooling, M10+; FALSE as in states.cs)
        return dot;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:421
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_CONTENTS)))
            return dot;

        if (cell != null)
        {
            await cell.emp_act(severity);
            handle_power(1 * SECONDS);
        }
        return dot;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:430
    [DmParams("user", "emag_card", "forced")]
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        object forced = Dm.arg(__extra, 0);
        if (!Dm.truthy(enabled) && !Dm.truthy(forced))
        {
            balloon_alert(user, "turn it on first!");
            return FALSE;
        }
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            balloon_alert(user, "already emagged!");
            if (emag_card != null)
                to_chat(user, span_notice(I18n.T("You swipe \\the [] with []. A console window fills the screen, but it quickly closes itself after only a few lines are written to it.", this, emag_card)));
            return FALSE;
        }

        await base.emag_act(user, emag_card, __extra);
        if (!Dm.truthy(forced))
            add_log("manual overriding of permissions and modification of device firmware detected. Reboot and reinstall required.");
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        device_theme = PDA_THEME_SYNDICATE;
        if (user != null)
        {
            balloon_alert(user, "syndieOS loaded");
            if (emag_card != null)
                to_chat(user, span_notice(I18n.T("You swipe \\the [] with []. A console window momentarily fills the screen, with white text rapidly scrolling past.", this, emag_card)));
        }
        return TRUE;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:451
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        float healthpercent = Dm.round(Dm.num(atom_integrity) / Dm.num(max_integrity) * 100, 1);
        if (healthpercent >= 50 && healthpercent <= 99)
            Dm.append(ref dot, span_info(I18n.T("It looks slightly damaged.")));
        else if (healthpercent >= 25 && healthpercent <= 50)
            Dm.append(ref dot, span_info(I18n.T("It appears heavily damaged.")));
        else if (healthpercent >= 0 && healthpercent <= 25)
            Dm.append(ref dot, span_warning(I18n.T("It's falling apart!")));

        if (Dm.truthy(long_ranged))
            Dm.append(ref dot, I18n.T("It is upgraded with an experimental long-ranged network capabilities, picking up NTNet frequencies while further away."));
        Dm.append(ref dot, span_notice(I18n.T("It has [] GQ of storage capacity.", max_capacity)));

        if (stored_id != null)
        {
            if (Dm.truthy(await Adjacent(user)))
                Dm.append(ref dot, I18n.T("It has \\the [] card inserted in its card slot.[]", stored_id, alt_stored_id != null ? "" : Dm.text(" []", span_info(I18n.T("Alt-click to eject it.")))));
            else
                Dm.append(ref dot, I18n.T("Its identification card slot is currently occupied."));
        }

        if (alt_stored_id != null)
        {
            if (Dm.truthy(await Adjacent(user)))
                Dm.append(ref dot, I18n.T("It has \\the [] card stored in its secondary card slot. []", alt_stored_id, span_info(I18n.T("Alt-click to eject it."))));
            else
                Dm.append(ref dot, I18n.T("Its secondary identification card slot is currently occupied."));
        }

        if (cell != null)
            Dm.append(ref dot, span_info(I18n.T("Right-click it with a screwdriver to eject the [].", cell)));
        return dot;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:481
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        Dm.append(ref dot, I18n.T("Storage capacity: []/[]GQ", used_capacity, max_capacity));

        foreach (datum_computer_file app_examine in Dm.each<datum_computer_file>(stored_files))
        {
            if (Dm.truthy(app_examine.on_examine(this, user)))
                Dm.append(ref dot, app_examine.on_examine(this, user));
        }

        if (Dm.truthy(await Adjacent(user)))
            Dm.append(ref dot, span_notice(I18n.T("Paper level: [] / [].", stored_paper, max_paper)));
        return dot;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:492
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);

        if (held_item is obj_item_card_id) // isidcard
        {
            context[SCREENTIP_CONTEXT_LMB] = stored_id != null ? "Swap ID" : "Insert ID";
            if (HAS_TRAIT(this, TRAIT_MODPC_TWO_ID_SLOTS))
                context[SCREENTIP_CONTEXT_RMB] = alt_stored_id != null ? "Swap Secondary ID" : "Insert Secondary ID";
            dot = CONTEXTUAL_SCREENTIP_SET;
        }

        if (Dm.eq(held_item?.tool_behaviour, TOOL_SCREWDRIVER) && cell != null)
        {
            context[SCREENTIP_CONTEXT_RMB] = "Remove Cell";
            dot = CONTEXTUAL_SCREENTIP_SET;
        }
        if (Dm.eq(held_item?.tool_behaviour, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_RMB] = "Deconstruct";
            dot = CONTEXTUAL_SCREENTIP_SET;
        }

        if (alt_stored_id != null)
        {
            context[SCREENTIP_CONTEXT_ALT_RMB] = "Remove Secondary ID";
            dot = CONTEXTUAL_SCREENTIP_SET;
        }
        if (stored_id != null) // ID get removed first before pAIs
        {
            context[SCREENTIP_CONTEXT_ALT_LMB] = "Remove ID";
            dot = CONTEXTUAL_SCREENTIP_SET;
        }
        else if (inserted_pai != null)
        {
            context[SCREENTIP_CONTEXT_ALT_LMB] = "Remove pAI";
            dot = CONTEXTUAL_SCREENTIP_SET;
        }

        if (inserted_disk is obj_item)
        {
            context[SCREENTIP_CONTEXT_CTRL_SHIFT_LMB] = "Remove Disk";
            dot = CONTEXTUAL_SCREENTIP_SET;
        }
        return Dm.truthy(dot) ? dot : NONE;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:523
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (!Dm.truthy(icon_state_powered) || !Dm.truthy(icon_state_unpowered)) //no valid icon, don't update.
            return await base.update_icon_state(__extra);
        icon_state = Dm.str(Dm.truthy(enabled) ? icon_state_powered : icon_state_unpowered);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:529
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(enabled))
            Dm.append(ref dot, active_program != null ? mutable_appearance(overlays_icon, Dm.str(active_program.program_open_overlay)) : mutable_appearance(overlays_icon, icon_state_menu));
        if (Dm.num(atom_integrity) <= Dm.num(integrity_failure) * Dm.num(max_integrity))
        {
            Dm.append(ref dot, mutable_appearance(overlays_icon, "bsod"));
            Dm.append(ref dot, mutable_appearance(overlays_icon, "broken"));
        }
        return dot;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:537
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (internal_cell == gone)
        {
            internal_cell = null;
            if (Dm.truthy(enabled) && !Dm.truthy(use_energy()))
                shutdown_computer();
        }
        if (stored_id == gone)
        {
            stored_id = null;
            update_slot_icon();
            if (loc is mob_living_carbon_human human_wearer)
                human_wearer.update_ID_card();
        }
        if (alt_stored_id == gone)
            alt_stored_id = null;
        if (inserted_pai == gone)
            update_appearance(UPDATE_ICON);
        if (inserted_disk == gone)
        {
            inserted_disk = null;
            update_appearance(UPDATE_ICON);
        }
        return await base.Exited(gone, direction);
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:557
    public override async DmTask<object> click_ctrl_shift(mob user = null)
    {
        if (!(inserted_disk is obj_item disk))
            return null;
        await user.put_in_hands(disk);
        inserted_disk = null;
        playsound(this, Dm.resource("sound/machines/card_slide.ogg"), 50);
        return null;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:564
    public virtual async DmTask<object> turn_on(mob user = null, object open_ui = null)
    {
        open_ui ??= TRUE;
        bool issynth = false; // Robots and AIs get different activation messages.
        if (user != null)
            issynth = Dm.truthy(HAS_SILICON_ACCESS(user));

        if (Dm.num(atom_integrity) <= Dm.num(integrity_failure) * Dm.num(max_integrity))
        {
            if (user != null)
            {
                if (issynth)
                    to_chat(user, span_warning(I18n.T("You send an activation signal to \\the [], but it responds with an error code. It must be damaged.", this)));
                else
                    to_chat(user, span_warning(I18n.T("You press the power button, but the computer fails to boot up, displaying variety of errors before shutting down again.")));
            }
            return FALSE;
        }

        if (Dm.truthy(use_energy(base_active_power_usage))) // checks if the PC is powered
        {
            if (Dm.truthy(looping_sound))
                soundloop.start();
            enabled = TRUE;
            update_appearance();
            if (user != null)
            {
                if (issynth)
                    to_chat(user, span_notice(I18n.T("You send an activation signal to \\the [], turning it on.", this)));
                else
                    to_chat(user, span_notice(I18n.T("You press the power button and start up \\the [].", this)));
                if (Dm.truthy(open_ui))
                    await update_tablet_open_uis(user);
            }
            if (Dm.truthy(this._listen_lookup?[COMSIG_MODULAR_COMPUTER_TURNED_ON]))
                this._SendSignal(COMSIG_MODULAR_COMPUTER_TURNED_ON, Dm.list(this, user));
            return TRUE;
        }
        else // Unpowered
        {
            if (user != null)
            {
                if (issynth)
                    to_chat(user, span_warning(I18n.T("You send an activation signal to \\the [] but it does not respond.", this)));
                else
                    to_chat(user, span_warning(I18n.T("You press the power button but \\the [] does not respond.", this)));
            }
            return FALSE;
        }
    }

    // Process currently calls handle_power(), may be expanded in future if more things are added.
    // tg: code/modules/modular_computers/computers/item/computer.dm:600
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(enabled)) // The computer is turned off
            return null;

        if (Dm.num(atom_integrity) <= Dm.num(integrity_failure) * Dm.num(max_integrity))
        {
            shutdown_computer();
            return null;
        }

        if (active_program != null && Dm.truthy(Dm.bitand(active_program.program_flags, PROGRAM_REQUIRES_NTNET)) && !Dm.truthy(get_ntnet_status()))
            active_program.event_networkfailure(FALSE); // Active program requires NTNet to run but we've just lost connection. Crash.

        foreach (datum_computer_file_program idle_programs in Dm.each<datum_computer_file_program>(idle_threads))
        {
            idle_programs.process_tick(seconds_per_tick);
            idle_programs.ntnet_status = Dm.num(get_ntnet_status());
            if (Dm.truthy(Dm.bitand(idle_programs.program_flags, PROGRAM_REQUIRES_NTNET)) && !Dm.truthy(idle_programs.ntnet_status))
                idle_programs.event_networkfailure(TRUE);
        }

        if (active_program != null)
        {
            active_program.process_tick(seconds_per_tick);
            active_program.ntnet_status = Dm.num(get_ntnet_status());
        }

        handle_power(seconds_per_tick); // Handles all computer power interaction
        return null;
    }

    /**
     * Displays notification text alongside a soundbeep when requested to by a program.
     *
     * After checking that the requesting program is allowed to send an alert, creates
     * a visible message of the requested text alongside a soundbeep. This proc adds
     * text to indicate that the message is coming from this device and the program
     * on it, so the supplied text should be the exact message and ending punctuation.
     *
     * Arguments:
     * The program calling this proc.
     * The message that the program wishes to display.
     */
    // tg: code/modules/modular_computers/computers/item/computer.dm:635
    public virtual object alert_call(datum_computer_file_program call_source = null, object alerttext = null, object sound = null)
    {
        sound ??= Dm.resource("sound/machines/beep/twobeep_high.ogg");
        if (call_source == null || !Dm.truthy(call_source.alert_able) || Dm.truthy(call_source.alert_silenced) || !Dm.truthy(alerttext)) //Yeah, we're checking alert_able. No, you don't get to make alerts that the user can't silence.
            return FALSE;
        playsound(this, sound, 50, TRUE);
        // [icon2html(physical, viewers(physical.loc))]: the chat icons (as at the other icon2html sites)
        (physical.loc as atom)?.visible_message(span_notice(I18n.T("\\The [] displays a [] notification: []", this, call_source.filedesc, alerttext)));
        return null;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:641
    public virtual void ring(object ringtone = null, DmList balloon_alertees = null) // bring bring
    {
        if (!Dm.truthy(use_energy(check_programs: FALSE)))
            return;
        if (HAS_TRAIT(SSstation, STATION_TRAIT_PDA_GLITCHED))
        {
            playsound(this, pick(
                Dm.resource("sound/machines/beep/twobeep_voice1.ogg"),
                Dm.resource("sound/machines/beep/twobeep_voice2.ogg")
                ), 50, TRUE);
        }
        else
            playsound(this, Dm.resource("sound/machines/beep/twobeep_high.ogg"), 50, TRUE);
        ringtone = Dm.text("*[]*", ringtone);
        audible_message(ringtone);
        foreach (mob_living alertee in Dm.each<mob_living>(balloon_alertees))
            alertee.balloon_alert(alertee, ringtone);
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:656
    public virtual void send_sound()
    {
        playsound(this, Dm.resource("sound/machines/terminal/terminal_success.ogg"), 15, TRUE);
    }

    // Function used by NanoUI's to obtain data for header. All relevant entries begin with "PC_"
    // tg: code/modules/modular_computers/computers/item/computer.dm:660
    public virtual DmList get_header_data()
    {
        DmList data = Dm.list();

        data["PC_device_theme"] = device_theme;

        if (cell != null)
        {
            data["PC_lowpower_mode"] = Dm.truthy(cell.charge) ? FALSE : TRUE;
            float percent = Dm.num(cell.percent());
            if (percent >= 80)
                data["PC_batteryicon"] = "batt_100.gif";
            else if (percent >= 60 && percent <= 80)
                data["PC_batteryicon"] = "batt_80.gif";
            else if (percent >= 40 && percent <= 60)
                data["PC_batteryicon"] = "batt_60.gif";
            else if (percent >= 20 && percent <= 40)
                data["PC_batteryicon"] = "batt_40.gif";
            else if (percent >= 5 && percent <= 20)
                data["PC_batteryicon"] = "batt_20.gif";
            else
                data["PC_batteryicon"] = "batt_5.gif";
            data["PC_batterypercent"] = Dm.text("[]%", Dm.round(percent));
        }
        else
        {
            data["PC_lowpower_mode"] = FALSE;
            data["PC_batteryicon"] = null;
            data["PC_batterypercent"] = null;
        }

        object ntnet = get_ntnet_status();
        if (Dm.eq(ntnet, NTNET_NO_SIGNAL))
            data["PC_ntneticon"] = "sig_none.gif";
        else if (Dm.eq(ntnet, NTNET_LOW_SIGNAL))
            data["PC_ntneticon"] = "sig_low.gif";
        else if (Dm.eq(ntnet, NTNET_GOOD_SIGNAL))
            data["PC_ntneticon"] = "sig_high.gif";
        else if (Dm.eq(ntnet, NTNET_ETHERNET_SIGNAL))
            data["PC_ntneticon"] = "sig_lan.gif";

        DmList program_headers = Dm.list();
        if (idle_threads.len > 0)
        {
            foreach (datum_computer_file_program idle_programs in Dm.each<datum_computer_file_program>(idle_threads))
            {
                if (!Dm.truthy(idle_programs.ui_header))
                    continue;
                program_headers.Add(Dm.list(Dm.list(Dm.kv("icon", idle_programs.ui_header))));
            }
        }

        data["PC_programheaders"] = program_headers;

        data["PC_stationtime"] = round_timestamp();
        data["PC_stationdate"] = Dm.text("[], []", time2text(world.realtime, "DDD, Month DD", NO_TIMEZONE), CURRENT_STATION_YEAR);
        data["PC_showexitprogram"] = active_program != null ? TRUE : FALSE; // Hides "Exit Program" button on mainscreen
        return data;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:710
    public virtual async DmTask<object> open_program(mob user = null, datum_computer_file_program program = null, object open_ui = null)
    {
        open_ui ??= TRUE;
        if (program?.computer != this)
            throw Dm.CRASH("tried to open program that does not belong to this computer", "code/modules/modular_computers/computers/item/computer.dm", 712);

        if (program == null) // Program not found or it's not executable program.
        {
            if (user != null)
                to_chat(user, span_danger(I18n.T("\\The []'s screen shows \"I/O ERROR - Unable to run program\" warning.", this)));
            return FALSE;
        }

        if (active_program == program)
            return FALSE;

        // The program is already running. Resume it.
        if (Dm.@in(program, idle_threads))
        {
            active_program?.background_program();
            active_program = program;
            program.alert_pending = FALSE;
            idle_threads.Remove(program);
            await program.on_made_active_program(user);
            if (Dm.truthy(open_ui))
                Waiting.Detach(update_tablet_open_uis(user));
            update_appearance(UPDATE_ICON);
            return TRUE;
        }

        if (!Dm.truthy(program.is_supported_by_hardware(hardware_flag, loud: TRUE, user: user)))
            return FALSE;

        if (idle_threads.len > max_idle_programs)
        {
            if (user != null)
                to_chat(user, span_danger(I18n.T("\\The [] displays a \"Maximal CPU load reached. Unable to run another program.\" error.", this)));
            return FALSE;
        }

        if (Dm.truthy(Dm.bitand(program.program_flags, PROGRAM_REQUIRES_NTNET)) && !Dm.truthy(get_ntnet_status())) // The program requires NTNet connection, but we are not connected to NTNet.
        {
            if (user != null)
                to_chat(user, span_danger(I18n.T("\\The []'s screen shows \"Unable to connect to NTNet. Please retry. If problem persists contact your system administrator.\" warning.", this)));
            return FALSE;
        }

        if (!Dm.truthy(await program.on_start(user as mob_living)))
            return FALSE;

        active_program?.background_program();

        active_program = program;
        program.alert_pending = FALSE;
        await program.on_made_active_program(user);
        if (Dm.truthy(open_ui))
            Waiting.Detach(update_tablet_open_uis(user));
        update_appearance(UPDATE_ICON);
        return TRUE;
    }

    // Returns 0 for No Signal, 1 for Low Signal and 2 for Good Signal. 3 is for wired connection (always-on)
    // tg: code/modules/modular_computers/computers/item/computer.dm:761
    public virtual object get_ntnet_status()
    {
        // computers are connected through ethernet
        if (Dm.truthy(Dm.bitand(hardware_flag, PROGRAM_CONSOLE)))
            return NTNET_ETHERNET_SIGNAL;

        // NTNet is down and we are not connected via wired connection. No signal.
        if (!Dm.truthy(find_functional_ntnet_relay()))
            return NTNET_NO_SIGNAL;

        turf current_turf = get_turf(this);
        if (current_turf == null)
            return NTNET_NO_SIGNAL;
        if (Dm.truthy(is_station_level(current_turf.z)))
        {
            if (Dm.truthy(Dm.bitand(hardware_flag, PROGRAM_LAPTOP))) //laptops can connect to ethernet but they have to be on station for that
                return NTNET_ETHERNET_SIGNAL;
            return NTNET_GOOD_SIGNAL;
        }
        else if (Dm.truthy(is_mining_level(current_turf.z)))
            return NTNET_LOW_SIGNAL;
        else if (Dm.truthy(long_ranged))
            return NTNET_LOW_SIGNAL;
        return NTNET_NO_SIGNAL;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:783
    public virtual object add_log(object text = null)
    {
        if (!Dm.truthy(get_ntnet_status()))
            return FALSE;

        SSmodular_computers.add_log(Dm.text("[]: []", this, text));
        return null;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:789
    public virtual void close_all_programs()
    {
        active_program?.kill_program();
        foreach (datum_computer_file_program idle in Dm.each<datum_computer_file_program>(idle_threads.Copy()))
            idle.kill_program();
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:794
    public virtual void shutdown_computer(object loud = null)
    {
        loud ??= TRUE;
        close_all_programs();
        if (Dm.truthy(looping_sound))
            soundloop.stop();
        if (physical != null && Dm.truthy(loud))
        {
            playsound(this, Dm.resource("sound/machines/terminal/terminal_off.ogg"), 25, FALSE);
            physical.visible_message(span_notice(I18n.T("\\The [] shuts down.", this)));
        }
        enabled = FALSE;
        update_appearance();
        if (Dm.truthy(this._listen_lookup?[COMSIG_MODULAR_COMPUTER_SHUT_DOWN]))
            this._SendSignal(COMSIG_MODULAR_COMPUTER_SHUT_DOWN, Dm.list(this, loud));
    }

    ///Imprints name and job into the modular computer, and calls back to necessary functions.
    ///Acts as a replacement to directly setting the imprints fields. All fields are optional, the proc will try to fill in missing gaps.
    // tg: code/modules/modular_computers/computers/item/computer.dm:807
    public virtual void imprint_id(object name = null, object job_name = null)
    {
        saved_identification = Dm.truthy(name) ? name : Dm.truthy(stored_id?.registered_name) ? stored_id.registered_name : saved_identification;
        saved_job = Dm.truthy(job_name) ? job_name : Dm.truthy(stored_id?.assignment) ? stored_id.assignment : saved_job;
        if (Dm.truthy(this._listen_lookup?[COMSIG_MODULAR_PDA_IMPRINT_UPDATED]))
            this._SendSignal(COMSIG_MODULAR_PDA_IMPRINT_UPDATED, Dm.list(this, saved_identification, saved_job));
        UpdateDisplay();
    }

    ///Resets the imprinted name and job back to null.
    // tg: code/modules/modular_computers/computers/item/computer.dm:814
    public virtual void reset_imprint()
    {
        saved_identification = null;
        saved_job = null;
        if (Dm.truthy(this._listen_lookup?[COMSIG_MODULAR_PDA_IMPRINT_RESET]))
            this._SendSignal(COMSIG_MODULAR_PDA_IMPRINT_RESET, Dm.list(this));
        UpdateDisplay();
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:820
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        if (!issilicon(user))
            playsound(this, SFX_KEYBOARD_CLICKS, 10, TRUE, FALSE);
        if (actiontype is datum_action_item_action_toggle_computer_light)
        {
            toggle_flashlight(user);
            return null;
        }

        return await base.ui_action_click(user, actiontype);
    }

    /**
     * Toggles the computer's flashlight, if it has one.
     *
     * Called from ui_act(), does as the name implies.
     * It is separated from ui_act() to be overwritten as needed.
    */
    // tg: code/modules/modular_computers/computers/item/computer.dm:835
    public virtual object toggle_flashlight(mob user = null)
    {
        if (!Dm.truthy(has_light) || !Dm.truthy(cell?.charge))
            return FALSE;
        if (!(disabled_time <= Dm.num(world.time))) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        {
            if (user != null)
                balloon_alert(user, I18n.T("disrupted!"));
            return FALSE;
        }
        Waiting.NoSleep(set_light_on(!Dm.truthy(light_on) ? TRUE : FALSE));
        update_appearance();
        Waiting.NoSleep(update_item_action_buttons(force: TRUE)); //force it because we added an overlay, not changed its icon
        return TRUE;
    }

    //Disables the computer's flashlight/LED light, if it has one, for a given disrupt_duration.
    // tg: code/modules/modular_computers/computers/item/computer.dm:848  on_saboteur: the saboteur handgun (an antagonist's item;
    // the /atom root comes with it)

    /**
     * Sets the computer's light color, if it has a light.
     *
     * Called from ui_act(), this proc takes a color string and applies it.
     * It is separated from ui_act() to be overwritten as needed.
     * Arguments:
     ** color is the string that holds the color value that we should use. Proc auto-fails if this is null.
    */
    // tg: code/modules/modular_computers/computers/item/computer.dm:866
    public virtual object set_flashlight_color(object color = null)
    {
        if (!Dm.truthy(has_light) || !Dm.truthy(color))
            return FALSE;
        comp_light_color = Dm.str(color);
        set_light_color(color);
        return TRUE;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:873
    public virtual void UpdateDisplay()
    {
        if (!Dm.truthy(saved_identification) && !Dm.truthy(saved_job))
        {
            name = Dm.initial(this, (obj_item_modular_computer p) => p.name);
            return;
        }
        name = Dm.text("[] ([])", saved_identification, saved_job);
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:879
    public override async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.screwdriver_act_secondary(user, tool);
        if (cell != null)
        {
            user.balloon_alert(user, I18n.T("cell removed"));
            await cell.forceMove(drop_location() as atom);
            internal_cell = null;
            return ITEM_INTERACT_SUCCESS;
        }
        else
            user.balloon_alert(user, I18n.T("no cell!"));
        return dot;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:889
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act_secondary(user, tool);
        // tool.play_tool_sound(src, user, 20, volume=20): the named volume is the one that holds
        tool.play_tool_sound(this, 20);
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: physical)))
            return ITEM_INTERACT_BLOCKING;
        await deconstruct(TRUE);
        user.balloon_alert(user, I18n.T("disassembled"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:898
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        await base.welder_act(user, tool);
        if (Dm.eq(atom_integrity, max_integrity))
        {
            to_chat(user, span_warning(I18n.T("\\The [] does not require repairs.", this)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
            return ITEM_INTERACT_SUCCESS;

        to_chat(user, span_notice(I18n.T("You begin repairing damage to \\the []...", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 20, volume: 50)))
            return ITEM_INTERACT_SUCCESS;
        atom_integrity = max_integrity;
        to_chat(user, span_notice(I18n.T("You repair \\the [].", this)));
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:915
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (tool is obj_item_card_id secondary_id && HAS_TRAIT(this, TRAIT_MODPC_TWO_ID_SLOTS))
            return Dm.truthy(await insert_secondary_id(secondary_id, user)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;

        return await item_interaction(user, tool, modifiers);
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:921
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_card_id) // isidcard
            return Dm.truthy(await insert_id(tool, user)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;

        if (iscash(tool))
            return money_act(user, tool);

        if (tool is obj_item_pai_card card)
            return await pai_act(user, card);

        if (tool is obj_item_stock_parts_power_store_cell new_cell)
            return await cell_act(user, new_cell);

        if (tool is obj_item_photo)
        {
            // photo_act: scanning a photo into an image file (the camera and its photos are not ported)
            Dm.NotTranslated("code/modules/modular_computers/computers/item/computer.dm:935", "item_interaction: photo_act, a photo scanned into an image file (the camera, after the core version)");
            return ITEM_INTERACT_BLOCKING;
        }

        // Check if any Applications need our item
        foreach (datum_computer_file item_holding_app in Dm.each<datum_computer_file>(stored_files))
        {
            object app_return = await item_holding_app.application_item_interaction(user, tool, modifiers);
            if (Dm.truthy(app_return))
                return app_return;
        }

        if (istype(tool, typeof(obj_item_paper)))
            return await paper_act(user, tool as obj_item_paper);

        if (istype(tool, typeof(obj_item_paper_bin)))
            return paper_bin_act(user, tool as obj_item_paper_bin);

        if (tool is obj_item_disk_computer disk)
            return await computer_disk_act(user, disk);

        return NONE;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:954
    public virtual object money_act(mob user = null, obj_item money = null)
    {
        obj_item_card_id inserted_id = stored_id?.GetID() as obj_item_card_id;
        if (inserted_id == null)
        {
            balloon_alert(user, I18n.T("no ID!"));
            return ITEM_INTERACT_BLOCKING;
        }
        return Dm.truthy(inserted_id.insert_money(money, user)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:961
    public virtual async DmTask<object> pai_act(mob user = null, obj_item_pai_card card = null)
    {
        if (inserted_pai != null)
            return ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(await user.transferItemToLoc(card, this)))
            return ITEM_INTERACT_BLOCKING;
        inserted_pai = card;
        balloon_alert(user, I18n.T("inserted pai"));
        // if(inserted_pai.pai) inserted_pai.pai.give_messenger_ability(): the pAI mob (pAIs, a later milestone): a card holds none
        update_appearance(UPDATE_ICON);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:973
    public virtual async DmTask<object> cell_act(mob user = null, obj_item_stock_parts_power_store_cell new_cell = null)
    {
        if (ismachinery(physical))
            return ITEM_INTERACT_BLOCKING;
        if (cell != null)
        {
            to_chat(user, span_warning(I18n.T("You try to connect \\the [] to \\the [], but its connectors are occupied.", new_cell, this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(new_cell, this)))
            return ITEM_INTERACT_BLOCKING;
        internal_cell = new_cell;
        to_chat(user, span_notice(I18n.T("You plug \\the [] to \\the [].", new_cell, this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:985  photo_act: the camera's photos are not ported (item_interaction
    // marks them)

    // tg: code/modules/modular_computers/computers/item/computer.dm:1023  (syndicate G3d, D316: the PDA disks)
    public virtual async DmTask<object> computer_disk_act(mob user = null, obj_item_disk_computer disk = null)
    {
        if (!Dm.truthy(await user.transferItemToLoc(disk, this)))
            return ITEM_INTERACT_BLOCKING;
        if (inserted_disk is obj_item old_disk)
        {
            await user.put_in_hands(old_disk);
            balloon_alert(user, I18n.T("disks swapped"));
        }
        else
            balloon_alert(user, I18n.T("disk inserted"));
        inserted_disk = disk;
        playsound(this, Dm.resource("sound/machines/card_slide.ogg"), 50);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:994  (gaps S3: the printer's paper)
    public virtual async DmTask<object> paper_act(mob user = null, obj_item_paper new_paper = null)
    {
        if (Dm.num(stored_paper) >= max_paper)
        {
            balloon_alert(user, I18n.T("no more room!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(new_paper)))
            return ITEM_INTERACT_BLOCKING;
        balloon_alert(user, I18n.T("inserted paper"));
        qdel(new_paper);
        playsound(this, Dm.resource("sound/machines/computer/paper_insert.ogg"), 40, vary: TRUE);
        stored_paper = Dm.num(stored_paper) + 1;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:1006  (gaps S3)
    public virtual object paper_bin_act(mob user = null, obj_item_paper_bin bin = null)
    {
        if (bin.total_paper <= 0)
        {
            balloon_alert(user, I18n.T("empty bin!"));
            return ITEM_INTERACT_BLOCKING;
        }
        float papers_added = 0; //just to keep track
        while ((bin.total_paper > 0) && (Dm.num(stored_paper) < max_paper))
        {
            papers_added++;
            stored_paper = Dm.num(stored_paper) + 1;
            bin.remove_paper();
        }
        if (papers_added == 0)
            return ITEM_INTERACT_BLOCKING;
        balloon_alert(user, I18n.T("inserted paper"));
        to_chat(user, span_notice(I18n.T("Added in [] new sheets. You now have [] / [] printing paper stored.", papers_added, stored_paper, max_paper)));
        playsound(this, Dm.resource("sound/machines/computer/paper_insert.ogg"), 40, vary: TRUE);
        bin.update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:1035
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        atom droploc = drop_location() as atom;
        remove_pai();
        await eject_aicard();
        if (cell != null)
            await cell.forceMove(droploc);
        if (stored_id != null)
            await stored_id.forceMove(droploc);
        if (alt_stored_id != null)
            await alt_stored_id.forceMove(droploc);
        if (inserted_disk is obj_item disk)
            await disk.forceMove(droploc);
        if (!Dm.truthy(disassembled))
            physical.visible_message(span_notice(I18n.T("\\The [] breaks apart!", this)));
        Dm.@new<obj_item_stack_sheet_iron>(droploc, steel_sheet_cost * (Dm.truthy(disassembled) ? 1 : 0.5f));
        relay_qdel();
    }

    // Ejects the inserted intellicard, if one exists. Used when the computer is deconstructed.
    // tg: code/modules/modular_computers/computers/item/computer.dm:1049  (silicons SI4)
    public virtual async DmTask<object> eject_aicard()
    {
        datum_computer_file_program_ai_restorer program = Dm.locate<datum_computer_file_program_ai_restorer>(stored_files);
        if (program != null)
            return await program.try_eject(forced: TRUE);
        return FALSE;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:407  (silicons SI4)
    public override async DmTask<object> attack_ai(mob user = null)
    {
        return await attack_self(user);
    }

    // Used by processor to relay qdel() to machinery type.
    // tg: code/modules/modular_computers/computers/item/computer.dm:1056
    public virtual void relay_qdel()
    {
    }

    // Perform adjacency checks on our physical counterpart, if any.
    // tg: code/modules/modular_computers/computers/item/computer.dm:1060
    public override async DmTask<object> Adjacent(atom neighbor = null, atom target = null, atom_movable mover = null, params object[] __extra)
    {
        if (physical != null && physical != this)
            return await physical.Adjacent(neighbor);
        return await base.Adjacent(neighbor, target, mover, __extra);
    }

    ///Returns a string of what to send at the end of messenger's messages.
    // tg: code/modules/modular_computers/computers/item/computer.dm:1066
    public virtual object get_messenger_ending()
    {
        return "Sent from my PDA";
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:1069
    public virtual object remove_pai(mob user = null)
    {
        if (inserted_pai == null)
            return FALSE;
        // if(inserted_pai.pai) inserted_pai.pai.remove_messenger_ability(): the pAI mob (pAIs, a later milestone)
        if (user != null)
        {
            Waiting.NoSleep(user.put_in_hands(inserted_pai));
            balloon_alert(user, I18n.T("removed pAI"));
        }
        else
            Waiting.NoSleep(inserted_pai.forceMove(drop_location() as atom));
        inserted_pai = null;
        update_appearance(UPDATE_ICON);
        return TRUE;
    }

    /// Get all stored files, including external disk files optionaly
    // tg: code/modules/modular_computers/computers/item/computer.dm:1084
    public virtual DmList get_files(object include_disk_files = null)
    {
        if (!Dm.truthy(include_disk_files) || inserted_disk is not obj_item_disk_computer disk)
            return stored_files;
        return Dm.add(stored_files, disk.stored_files) as DmList;
    }

    /// Returns how relevant the current security level is:
    // tg: code/modules/modular_computers/computers/item/computer.dm:1093
    public virtual object get_security_level_relevancy()
    {
        switch ((int)Dm.num(SSsecurity_level.get_current_level_as_number()))
        {
            case SEC_LEVEL_DELTA:
                return ALERT_RELEVANCY_PERTINENT;
            case SEC_LEVEL_RED: // all-hands-on-deck situations, everyone is responsible for combatting a threat
                return ALERT_RELEVANCY_PERTINENT;
            case SEC_LEVEL_BLUE: // suspected threat. security needs to be alert and possibly preparing for it, no further concerns
                if (Dm.@in(ACCESS_SECURITY, (stored_id as obj_item_card_id)?.access))
                    return ALERT_RELEVANCY_PERTINENT;
                else
                    return ALERT_RELEVANCY_WARN;
            case SEC_LEVEL_GREEN: // no threats, no concerns
                return ALERT_RELEVANCY_SAFE;
        }
        return 0;
    }

    // tg: code/modules/modular_computers/computers/item/computer.dm:1090-1092  (file-local defines, #undef at the end of the file)
    private const int ALERT_RELEVANCY_SAFE = 0; /// * 0: User is not in immediate danger and not needed for some station-critical task.
    private const int ALERT_RELEVANCY_WARN = 1; /// * 1: Danger is around, but the user is not directly needed to handle it.
    private const int ALERT_RELEVANCY_PERTINENT = 2; /// * 2: Danger is around and the user is responsible for handling it.
}

/**
 * Debug ModPC
 * Used to spawn all programs for Create and Destroy unit test.
 */
// tg: code/modules/modular_computers/computers/item/computer.dm:1117
public partial class obj_item_modular_computer_debug : obj_item_modular_computer
{
    // tg: code/modules/modular_computers/computers/item/computer.dm:1120
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Dm.append(ref starting_programs, subtypesof(typeof(datum_computer_file_program)));
        return base.Initialize(mapload, __extra);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/modular_computers/computers/item/computer.dm:41  /obj/item/modular_computer's var/static/list/default_programs
    internal DmList obj_item_modular_computer__default_programs;
}
