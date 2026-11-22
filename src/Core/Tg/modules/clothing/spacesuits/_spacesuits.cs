// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/spacesuits/_spacesuits.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4c slice 2: the space helmet and the space suit (pressure and cold protection by their flags, the suit's thermal
// regulator on a cell, its cell cover, the spacesuit HUD display, the helmet's visor). Left to their milestones in
// docs/ai/specs/live-partial.txt: the helmet's suicide_act (suicides, M10); emp_act is translated (M7 EMP), emag_act (G1a, D316). The visor dirt (clothing_dirt: pepper spray, chemistry M8), the hat stabilizer (a hat worn on the helmet,
// M9), the fishing difficulty (fishing, M10+) and the mesh texture on the wearer's limbs (equipment_bodypart_texture:
// bodypart textures are layering filters, species features under suits, M9) are deferred leaves. The item action buttons
// (toggle_spacesuit, adjust_visor) came with M4c slice 3 (add_item_action). The var blocks are generated.
//
// GAP(types): tg's `cell` (_spacesuits.dm:114, var/obj/item/stock_parts/power_store/cell = /obj/item/stock_parts/power_store/cell/high)
// is generated as Type (types-converter.md 3.5, V3: its default is a typepath), which the cell instance does not fit. The
// suit keeps its cell in `cell_instance` below, read and written where tg reads and writes `cell` once Initialize made it;
// `cell` keeps the typepath until Initialize, which sets it to null when it makes the instance (as obj_machinery_button's
// device_instance, buttons.cs).

// tg: code/modules/clothing/spacesuits/_spacesuits.dm:6
public partial class obj_item_clothing_head_helmet_space : obj_item_clothing_head_helmet
{
    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:47
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(visor_dirt))
        {
            // AddComponent(/datum/component/clothing_dirt, visor_dirt): pepper spray dirtying the visor (chemistry, M8)
            Dm.Deferred("code/modules/clothing/spacesuits/_spacesuits.dm:50", "Initialize: AddComponent(/datum/component/clothing_dirt), the visor dirtied by pepper spray (chemistry, M8)");
        }
        if (Dm.truthy(fishing_modifier))
        {
            // AddElement(/datum/element/adjust_fishing_difficulty, fishing_modifier): fishing (M10+)
            Dm.Deferred("code/modules/clothing/spacesuits/_spacesuits.dm:52", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty), the fishing difficulty (fishing, M10+)");
        }
        Waiting.NoSleep(add_stabilizer());
        // AddElement(/datum/element/equipment_bodypart_texture, BODY_ZONE_HEAD, /datum/bodypart_texture/mesh/space): the mesh
        // texture filter on the wearer's limbs (bodypart textures, M9)
        Dm.Deferred("code/modules/clothing/spacesuits/_spacesuits.dm:54", "Initialize: AddElement(/datum/element/equipment_bodypart_texture), the mesh texture on the wearer's head (bodypart textures, M10+)");
        return dot;
    }

    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:56
    public virtual async DmTask add_stabilizer(float? loose_hat = null)
    {
        loose_hat ??= TRUE;
        await AddComponent(typeof(datum_component_hat_stabilizer), Dm.named("loose_hat", loose_hat)); // gaps MO1: the component is live
    }

    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:59
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot) || !Dm.truthy(has_visor))
            return dot;

        return await adjust_visor(user as mob_living);
    }

    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:66
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(has_visor))
            return NONE;

        return Dm.truthy(await adjust_visor(user as mob_living)) ? CLICK_ACTION_SUCCESS : CLICK_ACTION_BLOCKING;
    }

    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:72
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", Dm.initial(this, (obj_item_clothing_head_helmet_space p) => p.icon_state), Dm.truthy(up) ? "-novisor" : "");
        return dot;
    }
}

// tg: code/modules/clothing/spacesuits/_spacesuits.dm:81
public partial class obj_item_clothing_suit_space : obj_item_clothing_suit
{
    /// Charge per tick consumed by the thermal regulator
    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:2  #define THERMAL_REGULATOR_COST (0.018 * STANDARD_CELL_CHARGE) (file-local, rules 11.4)
    private const float THERMAL_REGULATOR_COST = 0.018f * STANDARD_CELL_CHARGE;

    /// tg's `cell` (_spacesuits.dm:114) once Initialize made it: the suit's power cell (see the GAP(types) above).
    public obj_item_stock_parts_power_store cell_instance;

    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:129
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (ispath(cell))
        {
            cell_instance = Dm.@new(cell, this) as obj_item_stock_parts_power_store;
            cell = null;
        }

        if (Dm.truthy(fishing_modifier))
        {
            // AddElement(/datum/element/adjust_fishing_difficulty, fishing_modifier): fishing (M10+)
            Dm.Deferred("code/modules/clothing/spacesuits/_spacesuits.dm:135", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty), the fishing difficulty (fishing, M10+)");
        }
        // AddElement(/datum/element/equipment_bodypart_texture, BODY_ZONE_CHEST, /datum/bodypart_texture/mesh/space): the mesh
        // texture filter on the wearer's limbs (bodypart textures, M9)
        Dm.Deferred("code/modules/clothing/spacesuits/_spacesuits.dm:136", "Initialize: AddElement(/datum/element/equipment_bodypart_texture), the mesh texture on the wearer's chest (bodypart textures, M10+)");
        return dot;
    }

    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:138
    public override async DmTask on_outfit_equip(mob_living_carbon_human outfit_wearer = null, object visuals_only = null, object item_slot = null)
    {
        await base.on_outfit_equip(outfit_wearer, visuals_only, item_slot);
        if (cell_instance == null)
            return;
        await toggle_spacesuit(toggler: null, manual_toggle: FALSE); //turn on the thermal regulator by default.
    }

    /// Start Processing on the space suit when it is worn to heat the wearer
    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:145
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_OCLOTHING))) // Check that the slot is valid
        {
            START_PROCESSING(SSobj, this);
            update_hud_icon(user); // update the hud
        }
    }

    // On removal stop processing, save battery
    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:152
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.dropped(user, silent, __extra);
        STOP_PROCESSING(SSobj, this);
        mob_living_carbon_human human_user = user as mob_living_carbon_human;
        if (human_user is mob_living_carbon_human && human_user.hud_used != null)
        {
            atom_movable_screen_spacesuit spacesuit_hud = human_user.hud_used.screen_objects[HUD_MOB_SPACESUIT] as atom_movable_screen_spacesuit;
            if (spacesuit_hud != null)
                spacesuit_hud.update_spacesuit_hud_icon(SPACESUIT_NO_ICON);
        }
        return dot;
    }

    // Space Suit temperature regulation and power usage
    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:162
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        mob_living_carbon_human user = loc as mob_living_carbon_human;
        if (!Dm.truthy(user) || !ishuman(user) || user.wear_suit != this)
            return null;

        // Do nothing if thermal regulators are off
        if (!Dm.truthy(thermal_on))
            return null;

        // If we got here, thermal regulators are on. If there's no cell, turn them off
        if (cell_instance == null)
        {
            Waiting.NoSleep(toggle_spacesuit(user, FALSE));
            update_hud_icon(user);
            return null;
        }

        // cell.use will return FALSE if charge is lower than THERMAL_REGULATOR_COST
        if (!Dm.truthy(Waiting.NoSleep(cell_instance.use(THERMAL_REGULATOR_COST))))
        {
            Waiting.NoSleep(toggle_spacesuit(user, FALSE));
            update_hud_icon(user);
            to_chat(user, span_warning(I18n.T("The thermal regulator cuts off as [] runs out of charge.", cell_instance)));
            return null;
        }

        // If we got here, it means thermals are on, the cell is in and the cell has
        // just had enough charge subtracted from it to power the thermal regulator
        user.adjust_bodytemperature(get_temp_change_amount((temperature_setting - user.bodytemperature), BODYTEMP_SUIT_CHANGE_RATE * Dm.num(seconds_per_tick)));
        update_hud_icon(user);
        return null;
    }

    // Clean up the cell on destroy
    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:190
    public override object Destroy(float? force = null)
    {
        if (isatom(cell_instance))
            QDEL_NULL(ref cell_instance);
        mob_living_carbon_human human = this.loc as mob_living_carbon_human;
        if (human is mob_living_carbon_human && human.hud_used != null)
        {
            atom_movable_screen_spacesuit spacesuit_hud = human.hud_used.screen_objects[HUD_MOB_SPACESUIT] as atom_movable_screen_spacesuit;
            if (spacesuit_hud != null)
                spacesuit_hud.update_spacesuit_hud_icon(SPACESUIT_NO_ICON);
        }
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // Clean up the cell on destroy
    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:202
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = null;
        dot = await base.Exited(gone, direction);
        if (gone != null && gone == cell_instance)
        {
            cell_instance = null;
            thermal_on = FALSE;
        }
        return dot;
    }

    // support for items that interact with the cell
    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:209
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        if (@interface is obj_item_inducer)
        {
            to_chat(user, span_alert(I18n.T("Error: unable to interface with [].", @interface)));
            return null;
        }
        return cell_instance;
    }

    // Show the status of the suit and the cell
    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:216
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        if (Dm.truthy(in_range(this, user)) || isobserver(user))
        {
            Dm.append(ref dot, I18n.T("The thermal regulator is [] and the temperature is set to [] &deg;C ([] &deg;F)",
                Dm.truthy(thermal_on) ? I18n.T("on") : I18n.T("off"), round(temperature_setting - T0C, 0.1f), round(temperature_setting * 1.8f - 459.67f, 0.1f)));
            Dm.append(ref dot, I18n.T("The power meter shows [] charge remaining.", cell_instance != null ? Dm.text("[]%", round(Dm.num(cell_instance.percent()), 0.1f)) : I18n.T("!invalid!")));
            if (Dm.truthy(cell_cover_open))
            {
                Dm.append(ref dot, I18n.T("The cell cover is open exposing the cell and setting knobs."));
                if (cell_instance == null)
                    Dm.append(ref dot, I18n.T("The slot for a cell is empty."));
                else
                    Dm.append(ref dot, I18n.T("\\The [] is firmly in place.", cell_instance));
            }
        }
        return dot;
    }

    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:229
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        toggle_spacesuit_cell(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:233
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        float range_low = 20; // Default min temp c
        float range_high = 45; // default max temp c
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            range_low = -20; // emagged min temp c
            range_high = 120; // emagged max temp c
        }

        // input(...) as null|num: the shim's input completes with null until the UI seam (Dm.Waiting.cs, M9)
        object deg_c = await input(user, I18n.T("What temperature would you like to set the thermal regulator to? ([]-[] degrees celcius)", range_low, range_high));
        if (Dm.truthy(deg_c) && Dm.num(deg_c) >= range_low && Dm.num(deg_c) <= range_high)
        {
            temperature_setting = round(T0C + Dm.num(deg_c), 0.1f);
            to_chat(user, span_notice(I18n.T("You see the readout change to [] c.", deg_c)));
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // object handling for accessing features of the suit
    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:248
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(cell_cover_open) || !(tool is obj_item_stock_parts_power_store_cell))
            return await base.item_interaction(user, tool, modifiers, __extra);

        if (cell_instance != null)
        {
            to_chat(user, span_warning(I18n.T("[] already has a cell installed.", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;
        cell_instance = tool as obj_item_stock_parts_power_store;
        to_chat(user, span_notice(I18n.T("You successfully install \\the [] into [].", cell_instance, this)));
        update_hud_icon(user);
        return ITEM_INTERACT_SUCCESS;
    }

    /// Open the cell cover when ALT+Click on the suit
    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:264
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        toggle_spacesuit_cell(user);
        return CLICK_ACTION_SUCCESS;
    }

    /// Remove the cell when the cover is open on CTRL+Click
    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:269
    public override async DmTask<object> item_ctrl_click(mob user = null)
    {
        object dot = null;
        dot = CLICK_ACTION_BLOCKING;
        if (Dm.truthy(cell_cover_open) && cell_instance != null)
        {
            await remove_cell(user);
            return CLICK_ACTION_SUCCESS;
        }
        return dot;
    }

    // Remove the cell when using the suit on its self
    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:276
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await remove_cell(user);
        return null;
    }

    /// Remove the cell from the suit if the cell cover is open
    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:280
    public virtual async DmTask remove_cell(mob user = null)
    {
        if (!Dm.truthy(cell_cover_open) || cell_instance == null)
            return;
        user.visible_message(span_notice(I18n.T("[] removes \\the [] from []!", user, cell_instance, this)),
            span_notice(I18n.T("You remove [].", cell_instance)));
        // tg: code/modules/clothing/spacesuits/_spacesuits.dm:285
        cell_instance.add_fingerprint(user);
        await user.put_in_hands(cell_instance);
        cell_instance = null;
        update_hud_icon(user);
    }

    /// Toggle the space suit's cell cover
    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:291
    public virtual void toggle_spacesuit_cell(mob user = null)
    {
        cell_cover_open = Dm.truthy(cell_cover_open) ? FALSE : TRUE;
        to_chat(user, span_notice(I18n.T("You [] the cell cover on \\the [].", Dm.truthy(cell_cover_open) ? I18n.T("open") : I18n.T("close"), this)));
    }

    /**
     * Toggle the space suit's thermal regulator status
     *
     * Toggle the space suit's thermal regulator status...
     * Can't do it if it has no charge.
     * Arguments:
     * * toggler - User mob who receives the to_chat messages.
     * * manual_toggle - If false get a differently-flavored message about it being disabled by itself
     */
    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:304
    public virtual async DmTask toggle_spacesuit(mob toggler = null, float? manual_toggle = null)
    {
        manual_toggle ??= TRUE;
        // If we're turning thermal protection on, check for valid cell and for enough
        // charge that cell. If it's too low, we shouldn't bother with setting the
        // thermal protection value and should just return out early.
        if (!Dm.truthy(thermal_on) && (cell_instance == null || cell_instance.charge < THERMAL_REGULATOR_COST))
        {
            if (Dm.truthy(toggler))
                to_chat(toggler, span_warning(I18n.T("The thermal regulator on [] has no charge.", this)));
            return;
        }

        thermal_on = Dm.truthy(thermal_on) ? FALSE : TRUE;
        min_cold_protection_temperature = Dm.truthy(thermal_on) ? SPACE_SUIT_MIN_TEMP_PROTECT : SPACE_SUIT_MIN_TEMP_PROTECT_OFF;

        await update_item_action_buttons();
        if (toggler == null && ishuman(loc))
            update_hud_icon(loc as mob);

        if (!Dm.truthy(toggler))
            return;
        if (Dm.truthy(manual_toggle))
            to_chat(toggler, span_notice(I18n.T("You turn [] []'s thermal regulator.", Dm.truthy(thermal_on) ? I18n.T("on") : I18n.T("off"), this)));
        else
            to_chat(toggler, span_danger(I18n.T("You feel []'s thermal regulator switch [] by itself!", this, Dm.truthy(thermal_on) ? I18n.T("on") : I18n.T("off"))));
    }

    // zap the cell if we get hit with an emp
    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:373
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_CONTENTS)))
            return dot;
        if (cell_instance != null)
            await cell_instance.emp_act(severity);
        return dot;
    }

    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:327
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        await toggle_spacesuit(user);
        return null;
    }

    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:331
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        if (user != null)
        {
            balloon_alert(user, "thermal regulator restrictions overridden");
            user.log_message(Dm.text("emagged [], overwriting thermal regulator restrictions.", this), LOG_GAME);
        }
        playsound(this, SFX_SPARKS, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        return TRUE;
    }

    // update the HUD icon
    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:342
    public virtual void update_hud_icon(mob user = null)
    {
        mob_living_carbon_human human = user as mob_living_carbon_human;
        if (!Dm.truthy(show_hud) || human.wear_suit != this || human.hud_used == null)
            return;

        atom_movable_screen_spacesuit spacesuit_hud = human.hud_used.screen_objects[HUD_MOB_SPACESUIT] as atom_movable_screen_spacesuit;
        if (!Dm.truthy(spacesuit_hud))
            return;

        if (cell_instance == null)
        {
            spacesuit_hud.update_spacesuit_hud_icon(SPACESUIT_CELL_MISSING, 0, thermal_on);
            return;
        }

        float cell_percent = Dm.num(cell_instance.percent());
        // Check if there's enough charge to trigger a thermal regulator tick and
        // if there is, whethere the cell's capacity indicates high, medium or low
        // charge based on it.
        if (cell_instance.charge < THERMAL_REGULATOR_COST)
        {
            spacesuit_hud.update_spacesuit_hud_icon(SPACESUIT_CELL_EMPTY, cell_percent, thermal_on);
            return;
        }
        // switch(cell_percent) with ranges: DM's `if(a to b)` matches a <= x <= b (rules 6.7)
        if (cell_percent >= 0 && cell_percent <= 20)
            spacesuit_hud.update_spacesuit_hud_icon(SPACESUIT_CELL_VERY_LOW, cell_percent, thermal_on);
        else if (cell_percent >= 21 && cell_percent <= 40)
            spacesuit_hud.update_spacesuit_hud_icon(SPACESUIT_CELL_LOW, cell_percent, thermal_on);
        else if (cell_percent >= 41 && cell_percent <= 60)
            spacesuit_hud.update_spacesuit_hud_icon(SPACESUIT_CELL_MID, cell_percent, thermal_on);
        else if (cell_percent >= 61 && cell_percent <= 100)
            spacesuit_hud.update_spacesuit_hud_icon(SPACESUIT_CELL_HIGH, cell_percent, thermal_on);
    }
}
