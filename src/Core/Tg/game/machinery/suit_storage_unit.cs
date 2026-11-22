// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/suit_storage_unit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4c slice 4: the whole file (Meta's EVA room, its eight standard units, and the heads' and departments' units on the map).
// The player uses it through tg's radial (interact, show_radial_menu: protocol version 15, DV40); the unit has no tgui window
// in this snapshot. Left out, with rows: the wires (set_wires, the hacking that turns uv_super or the safeties, M7), the
// cells' charging (charge_cell, power M7), the black smoke of a hacked cycle
// (smoke, M10+: fluid spread is not in the core version), tgui_input_list for the card reader's access type (tgui, M9). The MOD's modlink came with gaps MO1 (D333).
// The var block is generated (D111).
//
// GAP(types): tg's `suit` and `mask` vars (:19, :21) are generated as Types (V3: tg assigns typepaths to vars of those names
// elsewhere, the outfits), and the declared classes of `helmet` and `mod` are narrower than what tg stores in them
// (try_insert_item puts any /obj/item/clothing/head in helmet and a backpack in mod, :756, :774): the unit keeps the four
// in the *_value fields below, and `vars[key]` (interact :364, :419, :421) reads and writes them by tg's var name (dm_slot).

// tg: code/game/machinery/suit_storage_unit.dm:3
public partial class obj_machinery_suit_storage_unit : obj_machinery
{
    /// GAP(types): tg's `var/obj/item/clothing/suit/space/suit` (:19).
    public obj_item suit_value;

    /// GAP(types): tg's `var/obj/item/clothing/head/helmet/space/helmet` (:20).
    public obj_item helmet_value;

    /// GAP(types): tg's `var/obj/item/clothing/mask/mask` (:21).
    public obj_item mask_value;

    /// GAP(types): tg's `var/obj/item/mod/control/mod` (:22).
    public obj_item mod_value;

    // tg: code/game/machinery/suit_storage_unit.dm:338  /obj/machinery/suit_storage_unit/interact's var/static/list/items (rules 10.6)
    public static ref DmList interact__items => ref TgState.Current.obj_machinery_suit_storage_unit__interact__items;

    // tg's vars[key] for the unit's storage slots (see the GAP(types) note above).
    internal object dm_slot(object key) => key switch
    {
        "suit" => suit_value,
        "helmet" => helmet_value,
        "mask" => mask_value,
        "mod" => mod_value,
        "storage" => storage,
        _ => Dm.get_var(this, Dm.str(key)),
    };

    internal void dm_set_slot(object key, obj_item value)
    {
        switch (key)
        {
            case "suit": suit_value = value; break;
            case "helmet": helmet_value = value; break;
            case "mask": mask_value = value; break;
            case "mod": mod_value = value; break;
            case "storage": storage = value; break;
            default: Dm.set_var(this, Dm.str(key), value); break;
        }
    }

    // tg: code/game/machinery/suit_storage_unit.dm:181
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);

        set_access();
        // set_wires(new /datum/wires/suit_storage_unit(src)): the unit's wires (WIRE_HACK, WIRE_SAFETY, WIRE_ZAP, two duds)
        Dm.Deferred("code/game/machinery/suit_storage_unit.dm:185", "Initialize: set_wires(new /datum/wires/suit_storage_unit(src)): the unit's wires (hacking: uv_super, the safeties, the zap; M7)");
        if (Dm.truthy(suit_type))
            suit_value = Dm.@new(suit_type, this) as obj_item;
        if (Dm.truthy(helmet_type))
            helmet_value = Dm.@new(helmet_type, this) as obj_item;
        if (Dm.truthy(mask_type))
            mask_value = Dm.@new(mask_type, this) as obj_item;
        if (Dm.truthy(mod_type))
            mod_value = Dm.@new(mod_type, this) as obj_item;
        if (Dm.truthy(storage_type))
            storage = Dm.@new(storage_type, this) as obj_item;
        update_appearance();

        register_context();
        return dot;
    }

    // tg: code/game/machinery/suit_storage_unit.dm:200
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref suit_value);
        QDEL_NULL(ref helmet_value);
        QDEL_NULL(ref mask_value);
        QDEL_NULL(ref mod_value);
        QDEL_NULL(ref storage);
        id_card = null;
        return base.Destroy(force);
    }

    // tg: code/game/machinery/suit_storage_unit.dm:209
    [SignalHandler]
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        base.add_context(source, context, held_item, user);

        if (held_item == null)
            return NONE;

        bool screentip_change = false;
        if (held_item is obj_item_stock_parts_card_reader && !Dm.truthy(locked) && Dm.truthy(can_install_card_reader(user)))
        {
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Install Reader");
            screentip_change = true;
        }

        if (Dm.eq(held_item.tool_behaviour, TOOL_MULTITOOL) && !Dm.truthy(locked) && !Dm.truthy(panel_open) && !Dm.truthy(state_open) && Dm.truthy(card_reader_installed))
        {
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("[] Access Panel", Dm.truthy(access_locked) ? I18n.T("Unlock") : I18n.T("Lock"));
            screentip_change = true;
        }

        if (!Dm.truthy(state_open) && Dm.truthy(is_operational) && Dm.truthy(card_reader_installed) && held_item.GetID() != null)
        {
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Change Access");
            screentip_change = true;
        }

        return screentip_change ? CONTEXTUAL_SCREENTIP_SET : NONE;
    }

    // tg: code/game/machinery/suit_storage_unit.dm:231
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        //if things aren't powered, these show anyways
        if (Dm.truthy(panel_open))
            Dm.append(ref dot, Dm.text("[]_panel", base_icon_state));
        if (Dm.truthy(state_open))
        {
            Dm.append(ref dot, Dm.text("[]_open", base_icon_state));
            if (Dm.truthy(suit_value) || Dm.truthy(mod_value))
                Dm.append(ref dot, Dm.text("[]_suit", base_icon_state));
            if (Dm.truthy(helmet_value))
                Dm.append(ref dot, Dm.text("[]_helm", base_icon_state));
            if (Dm.truthy(storage))
                Dm.append(ref dot, Dm.text("[]_storage", base_icon_state));
        }
        if (!(Dm.truthy(Dm.bitand(machine_stat, BROKEN)) || Dm.truthy(Dm.bitand(machine_stat, NOPOWER))))
        {
            if (Dm.truthy(state_open))
                Dm.append(ref dot, Dm.text("[]_lights_open", base_icon_state));
            else
            {
                if (Dm.truthy(uv))
                {
                    if (Dm.truthy(uv_super))
                        Dm.append(ref dot, Dm.text("[]_super", base_icon_state));
                    Dm.append(ref dot, Dm.text("[]_lights_red", base_icon_state));
                }
                else
                    Dm.append(ref dot, Dm.text("[]_lights_closed", base_icon_state));
            }
            //top lights
            if (Dm.truthy(uv))
            {
                if (Dm.truthy(uv_super))
                    Dm.append(ref dot, Dm.text("[]_uvstrong", base_icon_state));
                else
                    Dm.append(ref dot, Dm.text("[]_uv", base_icon_state));
            }
            else if (Dm.truthy(locked))
                Dm.append(ref dot, Dm.text("[]_locked", base_icon_state));
            else
                Dm.append(ref dot, Dm.text("[]_ready", base_icon_state));
        }
        return dot;
    }

    // tg: code/game/machinery/suit_storage_unit.dm:265
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        if (Dm.truthy(card_reader_installed))
        {
            Dm.append(ref dot, span_notice(I18n.T("Swipe your ID to change access levels.")));
            Dm.append(ref dot, span_notice(I18n.T("Use a multitool to [] access panel after opening panel.", Dm.truthy(access_locked) ? I18n.T("unlock") : I18n.T("lock"))));
        }
        else
            Dm.append(ref dot, span_notice(I18n.T("A card reader can be installed for further control access after opening its panel.")));
        return dot;
    }

    /// copy over access of electronics
    // tg: code/game/machinery/suit_storage_unit.dm:274
    public virtual void set_access(DmList accesses = null)
    {
        obj_item_electronics_airlock electronics = Dm.locate<obj_item_electronics_airlock>(component_parts);
        if (QDELETED(electronics))
            return;

        if (accesses != null)
            electronics.accesses = accesses;
        if (Dm.truthy(electronics.one_access))
        {
            req_one_access = electronics.accesses;
            req_access = null;
        }
        else
        {
            req_access = electronics.accesses;
            req_one_access = null;
        }
    }

    // tg: code/game/machinery/suit_storage_unit.dm:288
    public override async DmTask<object> RefreshParts()
    {
        object dot = null;
        dot = await base.RefreshParts();

        foreach (datum_stock_part_capacitor capacitor in Dm.each<datum_stock_part_capacitor>(component_parts))
            final_charge_rate = base_charge_rate + (Dm.num(capacitor.tier) * 0.05f * STANDARD_CELL_RATE);

        set_access();
        return dot;
    }

    // tg: code/game/machinery/suit_storage_unit.dm:296
    [SignalHandler]
    public override object power_change()
    {
        object dot = null;
        dot = base.power_change();
        if (!Dm.truthy(is_operational) && Dm.truthy(state_open))
        {
            Waiting.NoSleep(open_machine());
            Waiting.NoSleep(dump_inventory_contents());
        }
        update_appearance();
        return dot;
    }

    // tg: code/game/machinery/suit_storage_unit.dm:303
    public override async DmTask dump_inventory_contents(DmList subset = null)
    {
        await base.dump_inventory_contents(subset);
        helmet_value = null;
        suit_value = null;
        mask_value = null;
        mod_value = null;
        storage = null;
        set_occupant(null);
    }

    // tg: code/game/machinery/suit_storage_unit.dm:312
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        if (Dm.truthy(card_reader_installed))
            Dm.@new<obj_item_stock_parts_card_reader>(loc);
        return null;
    }

    // tg: code/game/machinery/suit_storage_unit.dm:316
    public virtual object access_check(mob_living user = null)
    {
        if (id_card != null)
        {
            obj_item_card_id id = id_card?.resolve() as obj_item_card_id;
            if (!Dm.truthy(id)) // reset to defaults
            {
                name = Dm.initial(this, (atom p) => p.name);
                desc = Dm.initial(this, (atom p) => p.desc);
                id_card = null;
                req_access = Dm.list();
                req_one_access = null;
                set_access(Dm.list());
                return TRUE;
            }
            if (user.get_idcard() != id)
            {
                balloon_alert(user, I18n.T("not your unit!"));
                return FALSE;
            }
        }
        if (!Dm.truthy(Waiting.NoSleep(allowed__proc(user))))
        {
            balloon_alert(user, I18n.T("access denied!"));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/game/machinery/suit_storage_unit.dm:337
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = null;
        mob_living living_user = user as mob_living; // tg declares mob/living/user

        if (!Dm.truthy(interact__items))
        {
            interact__items = Dm.list(
                Dm.kv("suit", create_silhouette_of(typeof(obj_item_clothing_suit_space_eva))),
                Dm.kv("helmet", create_silhouette_of(typeof(obj_item_clothing_head_helmet_space_eva))),
                Dm.kv("mask", create_silhouette_of(typeof(obj_item_clothing_mask_breath))),
                Dm.kv("mod", create_silhouette_of(typeof(obj_item_mod_control))),
                Dm.kv("storage", create_silhouette_of(typeof(obj_item_tank_internals_oxygen)))
            );
        }

        dot = await base.interact(user, __extra);
        if (Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(await check_interactable(user)))
            return dot;

        DmList choices = Dm.list();

        if (Dm.truthy(locked))
            choices["unlock"] = Dm.icon(Dm.resource("icons/hud/radial.dmi"), "radial_unlock");
        else if (Dm.truthy(state_open))
        {
            choices["close"] = Dm.icon(Dm.resource("icons/hud/radial.dmi"), "radial_close");

            foreach (object item_key in Dm.each(interact__items))
            {
                object item = dm_slot(item_key);
                if (Dm.truthy(item))
                    choices[item_key] = item;
                else
                {
                    // If the item doesn't exist, put a silhouette in its place
                    choices[item_key] = interact__items[item_key];
                }
            }
        }
        else
        {
            choices["open"] = Dm.icon(Dm.resource("icons/hud/radial.dmi"), "radial_open");
            choices["disinfect"] = Dm.icon(Dm.resource("icons/hud/radial.dmi"), "radial_disinfect");
            choices["lock"] = Dm.icon(Dm.resource("icons/hud/radial.dmi"), "radial_lock");
        }

        // HAS_SILICON_ACCESS(user) (__DEFINES/is_helpers.dm:178); isAdminGhostAI is not declared (no admin ghosts yet, as _machinery.cs)
        bool silicon_access = HAS_TRAIT(user, TRAIT_SILICON_ACCESS);
        object choice = await show_radial_menu(
            user,
            this,
            choices,
            custom_check: Dm.@new<datum_callback>(this, nameof(check_interactable), user),
            require_near: Dm.num(!silicon_access),
            autopick_single_option: FALSE
        );

        if (!Dm.truthy(choice))
            return dot;

        object sw__1 = choice;
        if (Dm.eq(sw__1, "open"))
        {
            if (!Dm.truthy(state_open))
            {
                if (!Dm.truthy(access_check(living_user)))
                    return dot;
                await open_machine(drop: FALSE);
                if (Dm.truthy(occupant))
                    await dump_inventory_contents();
            }
        }
        else if (Dm.eq(sw__1, "close"))
        {
            if (Dm.truthy(state_open))
                await close_machine();
        }
        else if (Dm.eq(sw__1, "disinfect"))
        {
            if (!Dm.truthy(access_check(living_user)))
                return dot;
            if (Dm.truthy(occupant) && Dm.truthy(safeties))
            {
                await say(I18n.T("Alert: safeties triggered, occupant detected!"));
                return dot;
            }
            else if (!Dm.truthy(helmet_value) && !Dm.truthy(mask_value) && !Dm.truthy(suit_value) && !Dm.truthy(mod_value) && !Dm.truthy(storage) && !Dm.truthy(occupant))
            {
                to_chat(user, I18n.T("There's nothing inside [] to disinfect!", this));
                return dot;
            }
            else
            {
                if (Dm.truthy(occupant))
                {
                    mob_living mob_occupant = occupant as mob_living;
                    to_chat(mob_occupant, span_userdanger(I18n.T("[]'s confines grow warm, then hot, then scorching. You're being burned []!", this, mob_occupant.stat == DEAD ? I18n.T("away") : I18n.T("alive"))));
                }
                await cook();
            }
        }
        else if (Dm.eq(sw__1, "lock") || Dm.eq(sw__1, "unlock"))
        {
            if (Dm.truthy(locked) && !Dm.truthy(access_check(living_user)))
                return dot;
            if (!Dm.truthy(state_open))
            {
                locked = Dm.not(locked);
                await update_icon();
            }
        }
        else
        {
            obj_item item_to_dispense = dm_slot(choice) as obj_item;
            if (Dm.truthy(item_to_dispense))
            {
                dm_set_slot(choice, null);
                await try_put_in_hand(item_to_dispense, living_user);
                await update_icon();
            }
            else
            {
                obj_item in_hands = user.get_active_held_item() as obj_item;
                if (Dm.truthy(in_hands))
                    await try_insert_item(living_user, in_hands);
                await update_icon();
            }
        }

        await interact(user);
        return dot;
    }

    // tg: code/game/machinery/suit_storage_unit.dm:432
    public virtual async DmTask<object> check_interactable(mob user = null)
    {
        if (!Dm.truthy(state_open) && !Dm.truthy(await can_interact(user)))
            return FALSE;

        if (Dm.truthy(panel_open))
            return FALSE;

        if (Dm.truthy(uv))
            return FALSE;

        return TRUE;
    }

    // tg: code/game/machinery/suit_storage_unit.dm:444  (Appendix A: item is object, the callers pass typepaths)
    public virtual object create_silhouette_of(object item = null)
    {
        image image = Dm.image(Dm.initial((Type)item, (atom p) => p.icon), Dm.initial((Type)item, (atom p) => p.icon_state));
        image.alpha = 128;
        image.color = COLOR_RED;
        return image;
    }

    // tg: code/game/machinery/suit_storage_unit.dm:450
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        atom A = dropped;
        mob_living living_user = user as mob_living;
        if (!isliving(A))
            return null;
        mob_living target = A as mob_living;
        if (!Dm.truthy(state_open))
        {
            to_chat(user, span_warning(I18n.T("The unit's doors are shut!")));
            return null;
        }
        if (!Dm.truthy(is_operational))
        {
            to_chat(user, span_warning(I18n.T("The unit is not operational!")));
            return null;
        }
        if (Dm.truthy(occupant) || Dm.truthy(helmet_value) || Dm.truthy(suit_value) || Dm.truthy(storage))
        {
            to_chat(user, span_warning(I18n.T("It's too cluttered inside to fit in!")));
            return null;
        }

        if (target == user)
            user.visible_message(span_warning(I18n.T("[] starts squeezing into []!", user, this)), span_notice(I18n.T("You start working your way into []...", this)));
        else
            target.visible_message(span_warning(I18n.T("[] starts shoving [] into []!", user, target, this)), span_userdanger(I18n.T("[] starts shoving you into []!", user, this)));

        if (Dm.truthy(await do_after(user, 3 * SECONDS, target)))
        {
            if (Dm.truthy(occupant) || Dm.truthy(helmet_value) || Dm.truthy(suit_value) || Dm.truthy(storage))
                return null;
            if (target == user)
                user.visible_message(span_warning(I18n.T("[] slips into [] and closes the door behind []!", user, this, user.p_them())), span_notice(I18n.T("You slip into []'s cramped space and shut its door.", this)));
            else
                target.visible_message(span_warning(I18n.T("[] pushes [] into [] and shuts its door!", user, target, this)), span_userdanger(I18n.T("[] shoves you into [] and shuts the door!", user, this)));
            await close_machine(target);
            // tg: code/game/machinery/suit_storage_unit.dm:477
            add_fingerprint(user);
        }
        return null;
    }

    /**
     * UV decontamination sequence.
     * Duration is determined by the uv_cycles var.
     * Effects determined by the uv_super var.
     * * If FALSE, all atoms (and their contents) contained are cleared of radiation. If a mob is inside, they are burned every cycle.
     * * If TRUE, all items contained are destroyed, and burn damage applied to the mob is increased. All wires will be cut at the end.
     * All atoms still inside at the end of all cycles are ejected from the unit.
    */
    // tg: code/game/machinery/suit_storage_unit.dm:487
    public virtual async DmTask cook()
    {
        mob_living mob_occupant = occupant as mob_living;
        if (Dm.truthy(uv_cycles))
        {
            uv_cycles--;
            uv = TRUE;
            locked = TRUE;
            update_appearance();
            if (Dm.truthy(mob_occupant))
            {
                if (Dm.truthy(uv_super))
                    await mob_occupant.adjust_fire_loss(rand(20, 36));
                else
                    await mob_occupant.adjust_fire_loss(rand(10, 16));
                if (iscarbon(mob_occupant) && !IS_UNCONSCIOUS(mob_occupant))
                {
                    //Awake, organic and screaming
                    await mob_occupant.emote("scream");
                }
            }
            _addtimer(Dm.@new<datum_callback>(this, nameof(cook)), 5 * SECONDS, file: "code/game/machinery/suit_storage_unit.dm", line: 502);
        }
        else
        {
            uv_cycles = Dm.num(Dm.initial(this, (obj_machinery_suit_storage_unit p) => p.uv_cycles));
            uv = FALSE;
            locked = FALSE;
            if (Dm.truthy(uv_super))
            {
                visible_message(span_warning(I18n.T("[]'s door creaks open with a loud whining noise. A cloud of foul black smoke escapes from its chamber.", this)));
                playsound(this, Dm.resource("sound/machines/airlock/airlock_alien_prying.ogg"), 50, TRUE);
                await do_smoke(0, this, this, smoke_type: typeof(datum_effect_system_fluid_spread_smoke_bad_black));
                QDEL_NULL(ref helmet_value);
                QDEL_NULL(ref suit_value);
                QDEL_NULL(ref mask_value);
                QDEL_NULL(ref mod_value);
                QDEL_NULL(ref storage);
                // The wires get damaged too.
                Dm.Deferred("code/game/machinery/suit_storage_unit.dm:517", "cook: wires.cut_all(), the unit's wires (hacking, M7)");
            }
            else
            {
                if (!Dm.truthy(mob_occupant))
                    visible_message(span_notice(I18n.T("[]'s door slides open. The glowing yellow lights dim to a gentle green.", this)));
                else
                {
                    visible_message(span_warning(I18n.T("[]'s door slides open, barraging you with the nauseating smell of charred flesh.", this)));
                    qdel(mob_occupant.GetComponent(typeof(datum_component_irradiated)));
                }
                playsound(this, Dm.resource("sound/machines/airlock/airlockclose.ogg"), 25, TRUE);
                DmList things_to_clear = Dm.list(); //Done this way since using GetAllContents on the SSU itself would include circuitry and such.
                if (Dm.truthy(suit_value))
                {
                    Dm.append(ref things_to_clear, suit_value);
                    Dm.append(ref things_to_clear, suit_value.get_all_contents());
                }
                if (Dm.truthy(helmet_value))
                {
                    Dm.append(ref things_to_clear, helmet_value);
                    Dm.append(ref things_to_clear, helmet_value.get_all_contents());
                }
                if (Dm.truthy(mask_value))
                {
                    Dm.append(ref things_to_clear, mask_value);
                    Dm.append(ref things_to_clear, mask_value.get_all_contents());
                }
                if (Dm.truthy(mod_value))
                {
                    Dm.append(ref things_to_clear, mod_value);
                    Dm.append(ref things_to_clear, mod_value.get_all_contents());
                }
                if (Dm.truthy(storage))
                {
                    Dm.append(ref things_to_clear, storage);
                    Dm.append(ref things_to_clear, storage.get_all_contents());
                }
                if (Dm.truthy(mob_occupant))
                {
                    Dm.append(ref things_to_clear, mob_occupant);
                    Dm.append(ref things_to_clear, mob_occupant.get_all_contents());
                }
                foreach (object am in Dm.each(things_to_clear)) //Scorches away blood and forensic evidence, although the SSU itself is unaffected
                {
                    atom_movable dirty_movable = am as atom_movable;
                    await dirty_movable.wash(CLEAN_ALL);
                }
            }
            await open_machine(FALSE);
            if (Dm.truthy(mob_occupant))
                await dump_inventory_contents();
        }
    }

    // tg: code/game/machinery/suit_storage_unit.dm:551
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        DmList cells_to_charge = Dm.list();
        foreach (obj_item charging in Dm.each<obj_item>(Dm.list(mod_value, suit_value, helmet_value, mask_value, storage)))
        {
            obj_item_stock_parts_power_store cell_charging = charging.get_cell() as obj_item_stock_parts_power_store;
            if (!istype(cell_charging, typeof(obj_item_stock_parts_power_store)) || cell_charging.charge == cell_charging.maxcharge)
                continue;

            Dm.append(ref cells_to_charge, cell_charging);
        }

        float cell_count = Dm.length(cells_to_charge);
        if (cell_count <= 0)
            return null;

        float charge_per_item = (final_charge_rate * Dm.num(seconds_per_tick)) / cell_count;
        foreach (obj_item_stock_parts_power_store cell in Dm.each_cast<obj_item_stock_parts_power_store>(cells_to_charge))
        {
            // charge_cell(charge_per_item, cell, grid_only = TRUE) (modules/power/power.dm): the machine draws the charge from the grid
            Dm.Deferred("code/game/machinery/suit_storage_unit.dm:566", "process: charge_cell(charge_per_item, cell, grid_only = TRUE), charging a stored suit's cell from the grid (power, M7)");
        }
        return null;
    }

    // tg: code/game/machinery/suit_storage_unit.dm:568
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (Dm.truthy(locked))
        {
            if (Dm.num(message_cooldown) <= world.time)
            {
                message_cooldown = world.time + 50;
                to_chat(user, span_warning(I18n.T("[]'s door won't budge!", this)));
            }
            return null;
        }
        await open_machine();
        await dump_inventory_contents();
        return null;
    }

    // tg: code/game/machinery/suit_storage_unit.dm:577
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        if (!Dm.truthy(locked))
        {
            await open_machine();
            await dump_inventory_contents();
            return null;
        }
        user.change_next_special_move(CLICK_CD_BREAKOUT);
        user.visible_message(span_notice(I18n.T("You see [] kicking against the doors of []!", user, this)),
            span_notice(I18n.T("You start kicking against the doors... (this will take about [].)", DisplayTimeText(breakout_time))),
            span_hear(I18n.T("You hear a thump from [].", this)));
        if (Dm.truthy(await do_after(user, breakout_time, target: this)))
        {
            if (!Dm.truthy(user) || IS_UNCONSCIOUS_OR_CRIT(user) || user.loc != this)
                return null;
            user.visible_message(span_warning(I18n.T("[] successfully broke out of []!", user, this)),
                span_notice(I18n.T("You successfully break out of []!", this)));
            await open_machine();
            await dump_inventory_contents();
        }

        // tg: code/game/machinery/suit_storage_unit.dm:594
        add_fingerprint(user);
        if (Dm.truthy(locked))
        {
            visible_message(span_notice(I18n.T("You see [] kicking against the doors of []!", user, this)),
                span_notice(I18n.T("You start kicking against the doors...")));
            _addtimer(Dm.@new<datum_callback>(this, nameof(resist_open), user), 30 * SECONDS, file: "code/game/machinery/suit_storage_unit.dm", line: 598);
        }
        else
        {
            await open_machine();
            await dump_inventory_contents();
        }
        return null;
    }

    // tg: code/game/machinery/suit_storage_unit.dm:603
    public virtual async DmTask resist_open(mob user = null)
    {
        if (!Dm.truthy(state_open) && Dm.truthy(occupant) && Dm.@in(user, this) && !IS_UNCONSCIOUS_OR_CRIT(user)) // Check they're still here.
        {
            visible_message(span_notice(I18n.T("You see [] burst out of []!", user, this)),
                span_notice(I18n.T("You escape the cramped confines of []!", this)));
            await open_machine();
        }
    }

    // tg: code/game/machinery/suit_storage_unit.dm:609
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(card_reader_installed) || Dm.truthy(state_open))
            return ITEM_INTERACT_BLOCKING;

        if (Dm.truthy(locked))
        {
            balloon_alert(user, I18n.T("unlock first!"));
            return ITEM_INTERACT_BLOCKING;
        }

        access_locked = Dm.not(access_locked);
        balloon_alert(user, I18n.T("access panel []", Dm.truthy(access_locked) ? I18n.T("locked") : I18n.T("unlocked")));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/suit_storage_unit.dm:621
    public virtual object can_install_card_reader(mob user = null)
    {
        if (Dm.truthy(card_reader_installed) || !Dm.truthy(panel_open) || Dm.truthy(state_open) || !Dm.truthy(is_operational))
            return FALSE;

        if (Dm.truthy(locked))
        {
            balloon_alert(user, I18n.T("unlock first!"));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/game/machinery/suit_storage_unit.dm:631
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(user.combat_mode))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        if (tool is obj_item_stock_parts_card_reader && Dm.truthy(can_install_card_reader(user)))
        {
            user.visible_message(span_notice(I18n.T("[] is installing a card reader.", user)),
                span_notice(I18n.T("You begin installing the card reader.")));
            if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: this, extra_checks: Dm.@new<datum_callback>(this, nameof(can_install_card_reader), user))))
                return ITEM_INTERACT_BLOCKING;
            qdel(tool);
            card_reader_installed = TRUE;
            balloon_alert(user, I18n.T("card reader installed"));
            return ITEM_INTERACT_SUCCESS;
        }

        obj_item_card_id id = null;
        if (!Dm.truthy(state_open) && Dm.truthy(is_operational) && Dm.truthy(card_reader_installed) && (id = tool.GetID() as obj_item_card_id) != null)
        {
            if (Dm.truthy(panel_open))
            {
                balloon_alert(user, I18n.T("close panel!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.truthy(locked))
            {
                balloon_alert(user, I18n.T("unlock first!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.truthy(access_locked))
            {
                balloon_alert(user, I18n.T("access panel locked!"));
                return ITEM_INTERACT_BLOCKING;
            }

            // change the access type
            // var/static/list/choices = list("Personal", "Departmental", "None")
            // var/choice = tgui_input_list(user, "Set Access Type", "Access Type", choices)
            Dm.Deferred("code/game/machinery/suit_storage_unit.dm:664", "item_interaction: tgui_input_list(user, \"Set Access Type\", \"Access Type\", list(\"Personal\", \"Departmental\", \"None\")), the card reader's access type (tgui, M10+): no choice");
            object choice = null;
            if (choice == null)
                return ITEM_INTERACT_BLOCKING;
            id_card = null;
            object sw__1 = choice;
            if (Dm.eq(sw__1, "Personal")) // only the player who swiped their id has access
            {
                id_card = WEAKREF(id) as datum_weakref;
                name = Dm.text("[] suit storage unit", id.registered_name);
                desc = Dm.text("Owned by []. []", id.registered_name, Dm.initial(this, (atom p) => p.desc));
            }
            else if (Dm.eq(sw__1, "Departmental")) // anyone who has the same access permissions as this id has access
            {
                name = Dm.text("[] suit storage unit", id.assignment);
                desc = Dm.text("Its a [] suit storage unit. []", id.assignment, Dm.initial(this, (atom p) => p.desc));
                set_access(id.GetAccess() as DmList);
            }
            else if (Dm.eq(sw__1, "None")) // free for all
            {
                name = Dm.initial(this, (atom p) => p.name);
                desc = Dm.initial(this, (atom p) => p.desc);
                req_access = Dm.list();
                req_one_access = null;
                set_access(Dm.list());
            }
            if (id_card != null)
                balloon_alert(user, I18n.T("now owned by []", id.registered_name));
            else
                balloon_alert(user, I18n.T("set to []", choice));
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(state_open) && Dm.truthy(is_operational))
            return await try_insert_item(user, tool);

        if (Dm.truthy(panel_open))
        {
            // if(is_wire_tool(tool)) wires.interact(user); return ITEM_INTERACT_SUCCESS
            Dm.Deferred("code/game/machinery/suit_storage_unit.dm:693", "item_interaction: is_wire_tool(tool) and wires.interact(user), the unit's wires window (hacking, M7; tgui, M10+)");
            if (Dm.eq(tool.tool_behaviour, TOOL_CROWBAR))
                return await default_deconstruction_crowbar(user, tool);
        }

        if (Dm.truthy(Dm.bitand(await default_pry_open(user, tool), ITEM_INTERACT_SUCCESS)))
        {
            await dump_inventory_contents();
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    /* ref tg-git issue #45036
        screwdriving it open while it's running a decontamination sequence without closing the panel prior to finish
        causes the SSU to break due to state_open being set to TRUE at the end, and the panel becoming inaccessible.
    */
    // tg: code/game/machinery/suit_storage_unit.dm:707
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(state_open))
            return NONE;
        if (Dm.truthy(uv) || Dm.truthy(locked))
        {
            to_chat(user, span_warning(I18n.T("You can't open the panel while its []", Dm.truthy(locked) ? I18n.T("locked") : I18n.T("decontaminating"))));
            return ITEM_INTERACT_BLOCKING;
        }

        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/suit_storage_unit.dm:716
    public override object can_crowbar_pry_open()
    {
        return Dm.num(Dm.truthy(base.can_crowbar_pry_open()) && !Dm.truthy(locked));
    }

    // tg: code/game/machinery/suit_storage_unit.dm:719
    public override object can_crowbar_deconstruct()
    {
        return Dm.num(Dm.truthy(base.can_crowbar_deconstruct()) && !Dm.truthy(locked));
    }

    // tg: code/game/machinery/suit_storage_unit.dm:722
    public override object rename_checks(mob_living user = null)
    {
        object dot = TRUE;
        if (Dm.truthy(locked))
        {
            balloon_alert(user, I18n.T("unlock first!"));
            return FALSE;
        }
        if (!Dm.truthy(access_check(user)))
        {
            balloon_alert(user, I18n.T("not yours to rename!"));
            return FALSE;
        }
        return dot;
    }

    /// If the SSU needs to have any communications wires cut.
    // tg: code/game/machinery/suit_storage_unit.dm:732
    public virtual void disable_modlink()
    {
        if (mod_value == null)
            return;

        // mod.disable_modlink() (gaps MO1). The slot can hold a backpack (the GAP(types) note above), which has no such proc: tg
        // runtimes there, the port skips it.
        (mod_value as obj_item_mod_control)?.disable_modlink();
    }

    // tg: code/game/machinery/suit_storage_unit.dm:738
    public virtual async DmTask<object> try_insert_item(mob_living user = null, obj_item to_insert = null)
    {
        if (istype(to_insert, typeof(obj_item_clothing_suit)))
        {
            if (Dm.truthy(suit_value))
            {
                to_chat(user, span_warning(I18n.T("The unit already contains a suit!")));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(to_insert, this)))
                return ITEM_INTERACT_BLOCKING;

            suit_value = to_insert;
        }
        else if (istype(to_insert, typeof(obj_item_clothing_head)))
        {
            if (Dm.truthy(helmet_value))
            {
                to_chat(user, span_warning(I18n.T("The unit already contains a helmet!")));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(to_insert, this)))
                return ITEM_INTERACT_BLOCKING;

            helmet_value = to_insert;
        }
        else if (istype(to_insert, typeof(obj_item_clothing_mask)))
        {
            if (Dm.truthy(mask_value))
            {
                to_chat(user, span_warning(I18n.T("The unit already contains a mask!")));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(to_insert, this)))
                return ITEM_INTERACT_BLOCKING;

            mask_value = to_insert;
        }
        else if (istype(to_insert, typeof(obj_item_storage_backpack)) || istype(to_insert, typeof(obj_item_mod_control)))
        {
            if (Dm.truthy(mod_value))
            {
                to_chat(user, span_warning(I18n.T("The unit already contains a backpack or MOD!")));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(to_insert, this)))
                return ITEM_INTERACT_BLOCKING;

            mod_value = to_insert;
        }
        else
        {
            if (Dm.truthy(storage))
            {
                to_chat(user, span_warning(I18n.T("The auxiliary storage compartment is full!")));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(to_insert, this)))
                return ITEM_INTERACT_BLOCKING;

            storage = to_insert;
        }
        visible_message(span_notice(I18n.T("[] inserts [] into []", user, to_insert, this)), span_notice(I18n.T("You load [] into [].", to_insert, this)));
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/suit_storage_unit.dm:338  /obj/machinery/suit_storage_unit/interact's var/static/list/items
    internal DmList obj_machinery_suit_storage_unit__interact__items;
}
