// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tcg/tcg_machines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the card mat's machines: the card slot that projects a creature card's holographic summon with its power and
// resolve, tapped, marked or modified, the blank card, and the mana and life control panels with their gem displays. The summon's
// card picture in the examine name chaser is a chat icon (icon2html), a deferred leaf. The var blocks are generated (D111).

// tg: code/game/objects/items/tcg/tcg_machines.dm:8
public partial class obj_machinery_trading_card_holder : obj_machinery
{
    // tg: code/game/objects/items/tcg/tcg_machines.dm:27
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_tcgcard)) || current_summon != null)
            return NONE;
        current_card = (obj_item_tcgcard)tool;
        card_template = current_card.extract_datum() as datum_card;
        if (card_template.cardtype != "Creature")
        {
            to_chat(user, span_notice(I18n.T("The [] smartly rejects the non-creature card.", this)));
            current_card = null;
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.transferItemToLoc(current_card, this)))
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_notice(I18n.T("You put the [] card in [].", current_card, this)));
        icon_state = "card_holder_active";
        update_appearance();
        current_summon = Dm.@new<obj_structure_trading_card_summon>(locate(x + summon_offset_x, y + summon_offset_y, z));
        current_summon.template = card_template;
        current_summon.card_ref = current_card;
        current_summon.team_color = team_color;
        await current_summon.load_model();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:54
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (current_summon != null)
        {
            DmList choices = GLOB.tcgcard_machine_radial_choices;
            if (!Dm.truthy(length(choices)))
            {
                choices = GLOB.tcgcard_machine_radial_choices = Dm.list(
                    Dm.kv("Pickup", image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_pickup")),
                    Dm.kv("Tap", image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_tap")),
                    Dm.kv("Mark", image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_mark")),
                    Dm.kv("Modify", image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_modify")));
            }
            object choice = await show_radial_menu(user, this, choices, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), require_near: TRUE, tooltips: TRUE);
            if (!Dm.truthy(await check_menu(user as mob_living)))
                return COMPONENT_CANCEL_ATTACK_CHAIN;
            switch (choice)
            {
                case "Tap":
                    current_summon.update_tapped();
                    break;
                case "Pickup":
                    if (current_card != null)
                    {
                        await user.put_in_hands(current_card);
                        to_chat(user, span_notice(I18n.T("You take the [] card out of [].", current_card, this)));
                        current_card = null;
                    }
                    else
                        to_chat(user, span_notice(I18n.T("The blank card dematerializes.")));
                    card_template = null;
                    icon_state = "card_holder_inactive";
                    update_appearance();
                    if (current_summon != null)
                    {
                        qdel(current_summon);
                        current_summon = null;
                    }
                    break;
                case "Modify":
                    await current_summon.modify_stats(user as mob_living);
                    break;
                case "Mark":
                    await current_summon.update_marked();
                    break;
                case null:
                    return null;
            }
        }
        else
            to_chat(user, span_warning(I18n.T("[] is empty!", this)));
        add_fingerprint(user);
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:93
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        if (current_summon == null)
        {
            object card_name = await tgui_input_text(user, I18n.T("Insert card name"), I18n.T("Blank Card Naming"), I18n.T("blank card"), max_length: MAX_NAME_LEN);
            if (card_name == null || !Dm.truthy(await user.can_perform_action(this)))
                return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
            current_summon = Dm.@new<obj_structure_trading_card_summon_blank>(locate(x + summon_offset_x, y + summon_offset_y, z));
            icon_state = "card_holder_active";
            current_summon.name = (string)card_name;
            current_summon.team_color = team_color;
            await current_summon.load_model();
        }
        else
            to_chat(user, span_notice(I18n.T("The [] already contains a card.", this)));
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:108
    public virtual async DmTask<object> check_menu(mob_living user = null)
    {
        if (!istype(user, typeof(mob_living)))
            return FALSE;
        if (Dm.truthy(user.incapacitated) || !Dm.truthy(await user.Adjacent(this)))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:115
    public override object Destroy(float? force = null)
    {
        if (current_summon != null)
            qdel(current_summon);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:120
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (card_template != null)
            Dm.append(ref dot, span_notice(I18n.T("There is currently a [] card inserted.", card_template.name)));
        else
        {
            if (current_summon != null)
                Dm.append(ref dot, span_notice(I18n.T("There is currently a blank card inserted.")));
            else
                Dm.append(ref dot, span_notice(I18n.T("There is no card currently inserted.")));
        }
        return dot;
    }
}

// tg: code/game/objects/items/tcg/tcg_machines.dm:52, :304, :365
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/game/objects/items/tcg/tcg_machines.dm:52  GLOBAL_LIST_EMPTY(tcgcard_machine_radial_choices)
    public DmList tcgcard_machine_radial_choices;
    ///Global list containing all options used for the TGC mana button.
    // tg: code/game/objects/items/tcg/tcg_machines.dm:304  GLOBAL_LIST_EMPTY(tcgcard_mana_bar_radial_choices)
    public DmList tcgcard_mana_bar_radial_choices;
    ///Global list containing all options used for the TGC health button.
    // tg: code/game/objects/items/tcg/tcg_machines.dm:365  GLOBAL_LIST_EMPTY(tcgcard_health_bar_radial_choices)
    public DmList tcgcard_health_bar_radial_choices;

    // tg: code/game/objects/items/tcg/tcg_machines.dm:52
    public virtual void InitGlobaltcgcard_machine_radial_choices()
    {
        tcgcard_machine_radial_choices = Dm.list();
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:304
    public virtual void InitGlobaltcgcard_mana_bar_radial_choices()
    {
        tcgcard_mana_bar_radial_choices = Dm.list();
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:365
    public virtual void InitGlobaltcgcard_health_bar_radial_choices()
    {
        tcgcard_health_bar_radial_choices = Dm.list();
    }
}

// tg: code/game/objects/items/tcg/tcg_machines.dm:133
public partial class obj_structure_trading_card_summon : obj_structure
{
    // tg: code/game/objects/items/tcg/tcg_machines.dm:1-6
    protected const float STAT_Y = -23;
    protected const float POWER_X = -14;
    protected const float RESOLVE_X = 12;
    protected const string DEFAULT_POWER_COLOR = "#af2323";
    protected const string DEFAULT_RESOLVE_COLOR = "#3492d0";
    protected const string DEFAULT_MODIFIED_COLOR = "#1db327";

    // tg: code/game/objects/items/tcg/tcg_machines.dm:167
    public virtual async DmTask load_model()
    {
        hologram = Dm.@new<obj_effect_overlay_card_summon>(loc);

        hologram.icon = Dm.file(template.summon_icon_file);
        hologram.icon_state = template.summon_icon_state;
        hologram.name = name;
        hologram.alpha = 170;
        await hologram.add_atom_colour(team_color, FIXED_COLOUR_PRIORITY);
        name = template.name;
        desc = template.desc;
        summon_power = template.power;
        summon_resolve = template.resolve;
        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:182
    public override object get_name_chaser(mob user = null, DmList name_chaser = null)
    {
        name_chaser ??= Dm.list();
        Dm.append(ref name_chaser, I18n.T("Faction: []", template.faction));
        Dm.append(ref name_chaser, I18n.T("Cost: []", template.summoncost));
        Dm.append(ref name_chaser, I18n.T("Type: [] - []", template.cardtype, template.cardsubtype));
        Dm.append(ref name_chaser, I18n.T("Power/Resolve: []/[]", summon_power, summon_resolve));
        if (Dm.truthy(template.rules)) //This can sometimes be empty
            Dm.append(ref name_chaser, I18n.T("Ruleset: []", template.rules));
        // name_chaser += list("[icon2html(card_ref.get_cached_flat_icon(), user, "extra_classes" = "hugeicon")]"): the card's picture in the chat line
        Dm.Deferred("code/game/objects/items/tcg/tcg_machines.dm:189", "get_name_chaser: icon2html of the card's flat icon, its picture in the chat line (the 3D client's chat is text: chat icons, M10+)");
        return name_chaser;
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:192
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        var overlay = update_stats(power_overlay, STAT_Y, summon_power, power_color, x_offset: POWER_X) as obj_effect_overlay_status_display_text;

        if (overlay != null)
            power_overlay = overlay;
        overlay = update_stats(resolve_overlay, STAT_Y, summon_resolve, resolve_color, x_offset: RESOLVE_X) as obj_effect_overlay_status_display_text;
        if (overlay != null)
            resolve_overlay = overlay;

        if (Dm.truthy(marked))
        {
            var mark_overlay = mutable_appearance(Dm.resource("icons/obj/toys/tcgmisc.dmi"), "gem_green", 9) as mutable_appearance;
            mark_overlay.pixel_w = 12;
            mark_overlay.pixel_z = 12;
            Dm.append(ref dot, mark_overlay);
        }
        return dot;
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:208
    public virtual object update_stats(obj_effect_overlay_status_display_text overlay = null, object pos_y = null, object stats = null, object text_color = null, object x_offset = null)
    {
        if (overlay != null && Dm.eq(stats, overlay.message))
            return null;

        if (overlay != null)
            qdel(overlay);

        var stats_display = Dm.@new<obj_effect_overlay_status_display_text>(this, pos_y, stats, text_color, text_color, x_offset);

        stats_display.pixel_y = -32;
        stats_display.pixel_z = 32;
        Dm.append(vis_contents, stats_display);
        return stats_display;
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:222
    public virtual void update_tapped()
    {
        if (Dm.truthy(tapped))
            hologram.transform = Dm.turn(hologram.transform, 90);
        else
            hologram.transform = Dm.turn(hologram.transform, -90);
        tapped = Dm.num(!Dm.truthy(tapped));
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:229
    public virtual async DmTask update_marked()
    {
        marked = Dm.num(!Dm.truthy(marked));
        await update_icon(UPDATE_OVERLAYS);
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:233
    public virtual async DmTask modify_stats(mob_living user = null)
    {
        summon_power = num2text(Dm.num(await tgui_input_number(user, I18n.T("Please input power value"), I18n.T("Stat Modification"), text2num(template.power), 25)));
        if (Dm.eq(summon_power, template.power))
            power_color = DEFAULT_POWER_COLOR;
        else
            power_color = modified_color;
        summon_resolve = num2text(Dm.num(await tgui_input_number(user, I18n.T("Please input resolve value"), I18n.T("Stat Modification"), text2num(template.resolve), 25)));
        if (Dm.eq(summon_resolve, template.resolve))
            resolve_color = DEFAULT_RESOLVE_COLOR;
        else
            resolve_color = modified_color;
        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:246
    public override object Destroy(float? force = null)
    {
        if (hologram != null)
            qdel(hologram);
        return base.Destroy(force);
    }
}

// tg: code/game/objects/items/tcg/tcg_machines.dm:251
public partial class obj_structure_trading_card_summon_blank : obj_structure_trading_card_summon
{
    // tg: code/game/objects/items/tcg/tcg_machines.dm:259
    public override async DmTask load_model()
    {
        hologram = Dm.@new<obj_effect_overlay_card_summon>(loc);

        hologram.icon = Dm.resource("icons/obj/toys/tcgmisc.dmi");
        hologram.icon_state = "cardback";
        hologram.name = name;
        hologram.alpha = 170;
        await hologram.add_atom_colour(team_color, FIXED_COLOUR_PRIORITY);
        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:269
    public override object get_name_chaser(mob user = null, DmList name_chaser = null)
    {
        Dm.append(ref name_chaser, I18n.T("Power/Resolve: []/[]", summon_power, summon_resolve));
        return name_chaser;
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:273
    public override async DmTask modify_stats(mob_living user = null)
    {
        summon_power = num2text(Dm.num(await tgui_input_number(user, I18n.T("Please input power value"), I18n.T("Stat Modification"), text2num(summon_power), 25)));
        summon_resolve = num2text(Dm.num(await tgui_input_number(user, I18n.T("Please input resolve value"), I18n.T("Stat Modification"), text2num(summon_resolve), 25)));
        update_appearance(UPDATE_OVERLAYS);
    }
}

///A button that generates a player manipulable bar of icons, in this case a mana bar.
// tg: code/game/objects/items/tcg/tcg_machines.dm:289
public partial class obj_machinery_trading_card_button : obj_machinery
{
    // tg: code/game/objects/items/tcg/tcg_machines.dm:306
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        display_panel_ref = Dm.@new(display_panel_type, locate(x + panel_offset_x, y + panel_offset_y, z)) as obj_effect_trading_card_panel;
        return dot;
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:310
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref display_panel_ref);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:314
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        var choices = setup_global() as DmList;
        if (!Dm.truthy(length(choices)))
            choices = setup_radial() as DmList;
        object choice = await show_radial_menu(user, this, choices, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), require_near: TRUE, tooltips: TRUE);
        if (!Dm.truthy(await check_menu(user as mob_living)))
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        await handle_choice(choice, user);
        await display_panel_ref.update_icon(UPDATE_OVERLAYS);
        add_fingerprint(user);
        return await base.attack_hand(user, modifiers, __extra);
    }

    ///Proc that determines which global list of radial choices is used.
    // tg: code/game/objects/items/tcg/tcg_machines.dm:326
    public virtual object setup_global()
    {
        return GLOB.tcgcard_mana_bar_radial_choices;
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:329
    public virtual object setup_radial()
    {
        object radial_choices;
        radial_choices = GLOB.tcgcard_mana_bar_radial_choices = Dm.list(
            Dm.kv("Set Mana", image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_set_mana")),
            Dm.kv("Set Mana Slots", image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_set_slots")),
            Dm.kv("Next Turn", image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_next")));
        return radial_choices;
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:338
    public virtual async DmTask handle_choice(object choice = null, mob user = null)
    {
        object input_value;
        switch (choice)
        {
            case "Set Mana":
                input_value = await tgui_input_number(user, I18n.T("Please input total mana"), I18n.T("Set mana"), display_panel_ref.gems, display_panel_ref.gem_slots, 0);
                if (input_value != null)
                    display_panel_ref.gems = Dm.num(input_value);
                break;
            case "Set Mana Slots":
                input_value = await tgui_input_number(user, I18n.T("Please input total mana slots"), I18n.T("Set mana slots"), display_panel_ref.gem_slots, 10, 1);
                if (Dm.truthy(input_value))
                    display_panel_ref.gem_slots = Dm.num(input_value);
                break;
            case "Next Turn":
                if (display_panel_ref.gem_slots <= 9)
                    display_panel_ref.gem_slots += 1;
                display_panel_ref.gems = display_panel_ref.gem_slots;
                break;
        }
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:353
    public virtual async DmTask<object> check_menu(mob_living user = null)
    {
        if (!istype(user, typeof(mob_living)))
            return FALSE;
        if (Dm.truthy(user.incapacitated) || !Dm.truthy(await user.Adjacent(this)))
            return FALSE;
        return TRUE;
    }
}

// tg: code/game/objects/items/tcg/tcg_machines.dm:360
public partial class obj_machinery_trading_card_button_health : obj_machinery_trading_card_button
{
    // tg: code/game/objects/items/tcg/tcg_machines.dm:367
    public override object setup_global()
    {
        return GLOB.tcgcard_health_bar_radial_choices;
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:370
    public override object setup_radial()
    {
        object radial_choices;
        radial_choices = GLOB.tcgcard_health_bar_radial_choices = Dm.list(
            Dm.kv("Set Life", image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_set_life")),
            Dm.kv("Inflict Damage", image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_damage")));
        return radial_choices;
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:378
    public override async DmTask handle_choice(object choice = null, mob user = null)
    {
        object input_value;
        switch (choice)
        {
            case "Set Life":
                input_value = await tgui_input_number(user, I18n.T("Please input total life"), I18n.T("Set life"), display_panel_ref.gems, display_panel_ref.gem_slots, 0);
                if (input_value != null)
                    display_panel_ref.gems = Dm.num(input_value);
                break;
            case "Inflict Damage":
                display_panel_ref.gems -= Dm.num(await tgui_input_number(user, I18n.T("Please input total damage"), I18n.T("Inflict damage"), 1, display_panel_ref.gem_slots, 0));
                break;
        }
    }
}

///A display panel that renders a set of icons (in this case mana crystals), this is generated by /obj/machinery/trading_card_button and can be manipulated by the button which generates it.
// tg: code/game/objects/items/tcg/tcg_machines.dm:396
public partial class obj_effect_trading_card_panel : obj_effect
{
    // tg: code/game/objects/items/tcg/tcg_machines.dm:430
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(update_icon(UPDATE_OVERLAYS));
        return dot;
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:434
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(gem_slots))
            return dot;
        gems = clamp(gems, 0, gem_slots);
        for (float gem_row = 1; gem_row <= number_of_rows; gem_row++)
        {
            for (float gem = 1; gem <= number_of_columns; gem++)
            {
                if (gem_slots >= (gem_row - 1) * number_of_columns + gem)
                {
                    var gem_overlay = mutable_appearance(gem_icon_file, empty_gem_icon) as mutable_appearance;
                    if (gems >= (gem_row - 1) * number_of_columns + gem)
                        gem_overlay.icon_state = gem_icon;
                    gem_overlay.pixel_z = gem_row * individual_gem_offset_y + gem_bar_offset_z;
                    gem_overlay.pixel_w = (gem - 1) * individual_gem_offset_x + gem_bar_offset_w;
                    Dm.append(ref dot, gem_overlay);
                }
            }
        }
        return dot;
    }

    // tg: code/game/objects/items/tcg/tcg_machines.dm:449
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("It is currently showing [] out of [] [].", gems, gem_slots, gem_title)));
        return dot;
    }
}
