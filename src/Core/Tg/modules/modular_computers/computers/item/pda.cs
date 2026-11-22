// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/computers/item/pda.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice E: the crew's PDA (pda.dm:1-353): its pen slot, its default apps, its overlays, its screentips and the client's ringtone.
// Syndicate G3d (D316) added the hidden uplink's illiterate unlock, the clown virus disk's upload (pre_attack) and the rigged
// messages' explosion (explode). Left out with a comment: the client's PDA preferences (update_pda_prefs: the preferences and
// the achievements' themes, DV47; the job's ringtone is set). Syndicate N1: the nuclear operatives' PDA (its Fission360 radar program is
// installed but generated-only, docs/ai/specs/not-translated.md section 148). The contractor PDA is the antagonists' (generated-only); silicons SI2
// (D326) added the silicons' built-in tablet (pda.dm:310-440). The var block is generated (D111) but for inserted_item, which holds a type path until Initialize
// makes the pen (so the type's constructor and its list defaults are here too, the Hand mode).

// tg: code/modules/modular_computers/computers/item/pda.dm:1
public partial class obj_item_modular_computer_pda : obj_item_modular_computer
{
    ///The item currently inserted into the PDA, starts with a pen.
    // tg: code/modules/modular_computers/computers/item/pda.dm:39  (a type path until Initialize makes the item)
    public object inserted_item = typeof(obj_item_pen);

    ///Static list of default PDA apps to install on Initialize.
    // tg: code/modules/modular_computers/computers/item/pda.dm:44  var/static/list/datum/computer_file/pda_programs
    public DmList pda_programs => TgState.Current.obj_item_modular_computer_pda__pda_programs ??= Dm.list(
        typeof(datum_computer_file_program_messenger),
        typeof(datum_computer_file_program_nt_pay),
        typeof(datum_computer_file_program_notepad),
        typeof(datum_computer_file_program_crew_manifest));

    ///List of items that can be stored in a PDA
    // tg: code/modules/modular_computers/computers/item/pda.dm:51  var/static/list/contained_item
    public DmList contained_item => TgState.Current.obj_item_modular_computer_pda__contained_item ??= Dm.list(
        typeof(obj_item_pen),
        typeof(obj_item_toy_crayon),
        typeof(obj_item_lipstick),
        typeof(obj_item_flashlight_pen),
        typeof(obj_item_reagent_containers_hypospray_medipen),
        typeof(obj_item_cigarette));

    // tg: code/modules/modular_computers/computers/item/pda.dm:2  (the type's own vars, kept with the Hand mode)
    public obj_item_modular_computer_pda()
    {
        name = "pda";
        icon = Dm.resource("icons/map_icons/items/pda.dmi");
        icon_state = "/obj/item/modular_computer/pda";
        post_init_icon_state = "pda";
        worn_icon_state = "nothing";
        base_icon_state = "tablet";
        greyscale_config = typeof(datum_greyscale_config_tablet);
        greyscale_colors = "#999875#a92323";
        lefthand_file = Dm.resource("icons/mob/inhands/items/devices_lefthand.dmi");
        righthand_file = Dm.resource("icons/mob/inhands/items/devices_righthand.dmi");
        inhand_icon_state = "electronic";
        overlays_icon = Dm.resource("icons/obj/devices/modular_pda.dmi");
        steel_sheet_cost = 2;
        interaction_flags_atom = 1540;
        icon_state_menu = "menu";
        max_capacity = 64;
        allow_chunky = 1;
        hardware_flag = PROGRAM_PDA;
        max_idle_programs = 2;
        w_class = WEIGHT_CLASS_SMALL;
        slot_flags = ITEM_SLOT_ID | ITEM_SLOT_BELT;
        has_light = 1;
        comp_light_luminosity = 2.3f;
        looping_sound = 0;
        shell_capacity = SHELL_CAPACITY_SMALL;
        action_slots = ALL;
        sound_vary = 1;
        pickup_sound = SFX_GENERIC_DEVICE_PICKUP;
        drop_sound = SFX_GENERIC_DEVICE_DROP;
    }

    // tg: code/modules/modular_computers/computers/item/pda.dm:18  (the type's list defaults, kept with the Hand mode)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        custom_materials = Dm.list(Dm.kv(typeof(datum_material_iron), SMALL_MATERIAL_AMOUNT * 3), Dm.kv(typeof(datum_material_glass), SMALL_MATERIAL_AMOUNT), Dm.kv(typeof(datum_material_plastic), SMALL_MATERIAL_AMOUNT));
    }

    // tg: code/modules/modular_computers/computers/item/pda.dm:60
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (inserted_item is Type item_type)
            inserted_item = Dm.@new(item_type, this);
        return dot;
    }

    // tg: code/modules/modular_computers/computers/item/pda.dm:65
    public override object Destroy(float? force = null)
    {
        if (inserted_item is obj_item)
        {
            qdel(inserted_item);
            inserted_item = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/modular_computers/computers/item/pda.dm:70
    public override void install_default_programs()
    {
        DmList apps_to_download = Dm.list();
        if (Dm.truthy(has_pda_programs))
            Dm.append(ref apps_to_download, Dm.add(default_programs, pda_programs));
        Dm.append(ref apps_to_download, starting_programs);

        foreach (Type programs in Dm.each_cast<Type>(apps_to_download))
        {
            datum_computer_file_program program_type = Dm.@new(programs) as datum_computer_file_program;
            store_file(program_type);
        }
    }

    // tg: code/modules/modular_computers/computers/item/pda.dm:80
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (stored_id != null)
            Dm.append(ref dot, mutable_appearance(overlays_icon, "id_overlay"));
        if (Dm.truthy(light_on))
            Dm.append(ref dot, mutable_appearance(overlays_icon, "light_overlay"));
        if (inserted_pai != null)
            Dm.append(ref dot, mutable_appearance(overlays_icon, "pai_inserted"));
        return dot;
    }

    // tg: code/modules/modular_computers/computers/item/pda.dm:89
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        if (HAS_TRAIT(this, TRAIT_PDA_MESSAGE_MENU_RIGGED))
            await explode(user, from_message_menu: TRUE);
        return dot;
    }

    // tg: code/modules/modular_computers/computers/item/pda.dm:94
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        // bypass literacy checks to access syndicate uplink
        datum_component_uplink hidden_uplink = GetComponent(typeof(datum_component_uplink)) as datum_component_uplink;
        if (Dm.truthy(hidden_uplink?.owner) && HAS_TRAIT(user, TRAIT_ILLITERATE))
        {
            if (!Dm.eq(hidden_uplink.owner, user.key))
                return await base.attack_self(user, modifiers, __extra);

            hidden_uplink.locked = FALSE;
            hidden_uplink.interact(null, user);
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }

        return await base.attack_self(user, modifiers, __extra);
    }

    // tg: code/modules/modular_computers/computers/item/pda.dm:107
    public override async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (!(inserted_disk is obj_item) || !ismachinery(target))
            return await base.pre_attack(target, user, modifiers, attack_modifiers);

        obj_machinery target_machine = target as obj_machinery;
        if (!Dm.truthy(target_machine.panel_open) && target is not obj_machinery_computer)
            return await base.pre_attack(target, user, modifiers, attack_modifiers);

        if (inserted_disk is not obj_item_disk_computer_virus_clown installed_cartridge)
            return await base.pre_attack(target, user, modifiers, attack_modifiers);
        if (installed_cartridge.charges == 0)
        {
            to_chat(user, span_notice(I18n.T("Out of virus charges.")));
            return await base.pre_attack(target, user, modifiers, attack_modifiers);
        }

        to_chat(user, span_notice(I18n.T("You upload the virus to []!", target)));
        DmList sig_list = Dm.list(COMSIG_ATOM_ATTACK_HAND);
        if (target is obj_machinery_door_airlock)
            sig_list = Dm.list(COMSIG_AIRLOCK_OPEN, COMSIG_AIRLOCK_CLOSE);

        installed_cartridge.charges--;
        await target.AddComponent(
            typeof(datum_component_sound_player),
            Dm.named("uses", rand(15, 20)),
            Dm.named("signal_list", sig_list));
        return TRUE;
    }

    // tg: code/modules/modular_computers/computers/item/pda.dm:135
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);

        if (inserted_item is obj_item item)
        {
            context[SCREENTIP_CONTEXT_CTRL_LMB] = Dm.text("Remove []", item);
            dot = CONTEXTUAL_SCREENTIP_SET;
        }
        else if (held_item != null && Dm.truthy(is_type_in_list(held_item, contained_item)))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("Insert []", held_item);
            dot = CONTEXTUAL_SCREENTIP_SET;
        }

        return Dm.truthy(dot) ? dot : NONE;
    }

    // tg: code/modules/modular_computers/computers/item/pda.dm:147
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (iscash(interacting_with))
            return money_act(user, interacting_with as obj_item);
        return NONE;
    }

    // tg: code/modules/modular_computers/computers/item/pda.dm:152
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(is_type_in_list(tool, contained_item)))
            return NONE;
        if (tool.w_class >= WEIGHT_CLASS_SMALL) // Anything equal to or larger than small won't work
        {
            user.balloon_alert(user, I18n.T("too big!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;
        if (inserted_item is obj_item)
            await swap_pen(user, tool);
        else
        {
            balloon_alert(user, I18n.T("inserted []", tool));
            inserted_item = tool;
            playsound(this, Dm.resource("sound/machines/pda_button/pda_button1.ogg"), 50, TRUE);
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/modular_computers/computers/item/pda.dm:172
    public override async DmTask<object> item_ctrl_click(mob user = null)
    {
        await remove_pen(user);
        return CLICK_ACTION_SUCCESS;
    }

    ///Finds how hard it is to send a virus to this tablet, checking all programs downloaded.
    // tg: code/modules/modular_computers/computers/item/pda.dm:177
    public virtual object get_detomatix_difficulty()
    {
        object detomatix_difficulty = null;

        foreach (datum_computer_file_program downloaded_apps in Dm.each<datum_computer_file_program>(stored_files))
            detomatix_difficulty = Dm.add(detomatix_difficulty, downloaded_apps.detomatix_resistance);

        return detomatix_difficulty;
    }

    // tg: code/modules/modular_computers/computers/item/pda.dm:185
    public virtual async DmTask remove_pen(mob user = null)
    {
        if (issilicon(user) || !Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH | NEED_DEXTERITY))) //TK doesn't work even with this removed but here for readability
            return;

        if (inserted_item is obj_item item)
        {
            balloon_alert(user, I18n.T("removed []", item));
            await user.put_in_hands(item);
            inserted_item = null;
            update_appearance();
            playsound(this, Dm.resource("sound/machines/pda_button/pda_button2.ogg"), 50, TRUE);
        }
    }

    // tg: code/modules/modular_computers/computers/item/pda.dm:197
    public virtual async DmTask swap_pen(mob user = null, obj_item tool = null)
    {
        if (inserted_item is obj_item item)
        {
            balloon_alert(user, I18n.T("swapped pens"));
            await user.put_in_hands(item);
            inserted_item = tool;
            update_appearance();
            playsound(this, Dm.resource("sound/machines/pda_button/pda_button1.ogg"), 50, TRUE);
        }
    }

    // tg: code/modules/modular_computers/computers/item/pda.dm:205
    public virtual async DmTask explode(mob target = null, mob bomber = null, float? from_message_menu = null)
    {
        turf current_turf = get_turf(this) as turf;

        if (Dm.truthy(from_message_menu))
            log_bomber(null, null, target, Dm.text("'s tablet exploded as [] tried to open their tablet message menu because of a recent tablet bomb.", target.p_they()));
        else
            log_bomber(bomber, "successfully tablet-bombed", target, Dm.text("as [] tried to reply to a rigged tablet message []", target.p_they(), Dm.truthy(bomber?.is_antag()) ? "" : "(SENT BY NON-ANTAG)"));

        if (loc is mob loc_mob)
        {
            loc_mob.show_message(
                msg: span_userdanger(I18n.T("Your [] explodes!", this)),
                type: MSG_VISUAL,
                alt_msg: span_warning(I18n.T("You hear a loud *pop*!")),
                alt_type: MSG_AUDIBLE);
        }
        else
            visible_message(span_danger(I18n.T("[] explodes!", this)), span_warning(I18n.T("You hear a loud *pop*!")));

        // target.client?.give_award(/datum/award/achievement/misc/clickbait, target) (:224): the achievements (not ported)

        if (current_turf != null)
        {
            current_turf.hotspot_expose(700, 125);
            if (inserted_disk is obj_item_disk_computer_virus_detomatix)
                await explosion(this, devastation_range: -1, heavy_impact_range: 1, light_impact_range: 3, flash_range: 4);
            else
                await explosion(this, devastation_range: -1, heavy_impact_range: -1, light_impact_range: 2, flash_range: 3);
        }
        qdel(this);
    }

    /**
     * A simple helper proc that applies the client's ringtone prefs to the tablet's messenger app,
     * if it has one.
     *
     * Arguments:
     * * owner_client - The client whose prefs we'll use to set the ringtone of this PDA.
     */
    // tg: code/modules/modular_computers/computers/item/pda.dm:242
    public virtual void update_pda_prefs(client owner_client = null)
    {
        if (owner_client == null)
            return;

        // DEVIATION(DV47): the preferences are not translated: the client's ringtone (/datum/preference/text/pda_ringtone) and theme
        // (/datum/preference/choiced/pda_theme) are their defaults, MESSENGER_RINGTONE_DEFAULT and no theme, and the achievements'
        // unlocked themes (get_award_status) are none, so nothing changes
    }

    /// A simple proc to set the ringtone from a pda.
    // tg: code/modules/modular_computers/computers/item/pda.dm:263
    public virtual void update_ringtone(object new_ringtone = null)
    {
        if (!istext(new_ringtone))
            return;
        foreach (datum_computer_file_program_messenger messenger_app in Dm.each<datum_computer_file_program_messenger>(stored_files))
        {
            messenger_app.ringtone = Dm.str(new_ringtone);
            return; // locate() in stored_files: the first one
        }
    }
}

public sealed partial class TgState
{
    // tg: code/modules/modular_computers/computers/item/pda.dm:44  /obj/item/modular_computer/pda's var/static/list/pda_programs
    internal DmList obj_item_modular_computer_pda__pda_programs;

    // tg: code/modules/modular_computers/computers/item/pda.dm:51  /obj/item/modular_computer/pda's var/static/list/contained_item
    internal DmList obj_item_modular_computer_pda__contained_item;
}

// tg: code/modules/modular_computers/computers/item/pda.dm:276
public partial class obj_item_modular_computer_pda_nukeops : obj_item_modular_computer_pda
{
    // tg: code/modules/modular_computers/computers/item/pda.dm:289
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(emag_act(null, null, TRUE));
        var msg = Dm.locate<datum_computer_file_program_messenger>(stored_files);
        if (msg != null)
            msg.invisible = TRUE;
        return dot;
    }
}

/**
 * Silicon PDA
 *
 * PDAs that are built-in to Silicons and should not exist at any point without being inside of one.
 */
// tg: code/modules/modular_computers/computers/item/pda.dm:316
public partial class obj_item_modular_computer_pda_silicon : obj_item_modular_computer_pda
{
    // tg: code/modules/modular_computers/computers/item/pda.dm:354
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        vis_flags = Dm.bitor(vis_flags, VIS_INHERIT_ID);
        silicon_owner = loc as mob_living_silicon;
        if (silicon_owner is not mob_living_silicon)
        {
            silicon_owner = null;
            stack_trace(Dm.text("[] initialized outside of a silicon, deleting.", GetType()), "code/modules/modular_computers/computers/item/pda.dm", 360);
            return INITIALIZE_HINT_QDEL;
        }
        return dot;
    }

    // tg: code/modules/modular_computers/computers/item/pda.dm:363
    public override object Destroy(float? force = null)
    {
        silicon_owner = null;
        robotact = null;
        return base.Destroy(force);
    }

    ///Silicons don't have the tools (or hands) to make circuits setups with their own PDAs.
    // tg: code/modules/modular_computers/computers/item/pda.dm:369  (its root, the circuit shell, is wiremod's: not ported)
    public virtual void add_shell_component(object capacity = null)
    {
        return;
    }

    // tg: code/modules/modular_computers/computers/item/pda.dm:372
    public override async DmTask<object> turn_on(mob user = null, object open_ui = null)
    {
        open_ui ??= FALSE;
        if (silicon_owner?.stat != DEAD)
            return await base.turn_on(user, open_ui);
        return FALSE;
    }

    // tg: code/modules/modular_computers/computers/item/pda.dm:377
    public override object get_ntnet_status()
    {
        //No borg found
        if (silicon_owner == null)
            return FALSE;
        // no AIs/pAIs
        if (silicon_owner is not mob_living_silicon_robot cyborg_check)
            return base.get_ntnet_status();
        //lockdown restricts borg networking
        if (Dm.truthy(cyborg_check.lockcharge))
            return FALSE;
        //borg cell dying restricts borg networking
        if (cyborg_check.cell == null || Dm.num((cyborg_check.cell as obj_item_stock_parts_power_store).charge) == 0)
            return FALSE;

        return base.get_ntnet_status();
    }

    /**
     * Returns a ref to the RoboTact app, creating the app if need be.
     *
     * The RoboTact app is important for borgs, and so should always be available.
     * This proc will look for it in the tablet's robotact var, then check the
     * hard drive if the robotact var is unset, and finally attempt to create a new
     * copy if the hard drive does not contain the app. If the hard drive rejects
     * the new copy (such as due to lack of space), the proc will crash with an error.
     * RoboTact is supposed to be undeletable, so these will create runtime messages.
     */
    // tg: code/modules/modular_computers/computers/item/pda.dm:404
    public virtual object get_robotact()
    {
        if (robotact != null)
            return robotact;
        robotact = find_file_by_name("robotact") as datum_computer_file_program_robotact;
        if (robotact != null)
            return robotact;
        stack_trace(Dm.text("Cyborg [] ( [] ) was somehow missing their self-manage app in their tablet. A new copy has been created.", silicon_owner, silicon_owner.GetType()), "code/modules/modular_computers/computers/item/pda.dm", 410);
        robotact = Dm.@new<datum_computer_file_program_robotact>(this);
        if (Dm.truthy(store_file(robotact)))
            return robotact;
        qdel(robotact);
        robotact = null;
        throw Dm.CRASH(Dm.text("Cyborg []'s tablet hard drive rejected receiving a new copy of the self-manage app. To fix, check the hard drive's space remaining. Please make a bug report about this.", silicon_owner), "code/modules/modular_computers/computers/item/pda.dm", 416);
    }

    //Makes the flashlight button affect the borg rather than the tablet
    // tg: code/modules/modular_computers/computers/item/pda.dm:428
    public override object toggle_flashlight(mob user = null)
    {
        if (silicon_owner == null || QDELETED(silicon_owner))
            return FALSE;
        if (silicon_owner is mob_living_silicon_robot robo)
            Waiting.NoSleep(robo.toggle_headlamp());
        return TRUE;
    }

    //Makes the flashlight color setting affect the borg rather than the tablet
    // tg: code/modules/modular_computers/computers/item/pda.dm:437
    public override object set_flashlight_color(object color = null)
    {
        if (silicon_owner == null || QDELETED(silicon_owner) || !Dm.truthy(color))
            return FALSE;
        if (silicon_owner is mob_living_silicon_robot robo)
        {
            robo.lamp_color = Dm.str(color);
            Waiting.NoSleep(robo.toggle_headlamp(FALSE, TRUE));
        }
        return TRUE;
    }

    // tg: code/modules/modular_computers/computers/item/pda.dm:446
    public override object ui_state(mob user = null)
    {
        return GLOB.deep_inventory_state;
    }
}

// tg: code/modules/modular_computers/computers/item/pda.dm:346
public partial class obj_item_modular_computer_pda_silicon_cyborg : obj_item_modular_computer_pda_silicon
{
    //Makes the light settings reflect the borg's headlamp settings
    // tg: code/modules/modular_computers/computers/item/pda.dm:419
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = await base.ui_data(user) as DmList;
        dot["has_light"] = TRUE;
        if (silicon_owner is mob_living_silicon_robot robo)
        {
            dot["light_on"] = robo.lamp_enabled;
            dot["comp_light_color"] = robo.lamp_color;
        }
        return dot;
    }
}

// tg: code/modules/modular_computers/computers/item/pda.dm:449
public partial class obj_item_modular_computer_pda_silicon_cyborg_syndicate : obj_item_modular_computer_pda_silicon_cyborg
{
    // tg: code/modules/modular_computers/computers/item/pda.dm:453
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (silicon_owner is mob_living_silicon_robot robo)
            robo.lamp_color = COLOR_RED; //Syndicate likes it red
        return dot;
    }
}
