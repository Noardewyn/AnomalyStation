// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tools/medical/defib.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice E: the whole file. The defibrillator unit (cell, safety, emag, EMP, the paddles' toggle, charge and cooldown), its
// compact, CMO and combat subtypes, and the paddles (the two-handed wield, the range snap back, do_help's revival and
// cardiac restart, do_harm's heart attack, do_disarm). Deferred leaves: the patient's and the puller's emotes (emotes, M9;
// not-translated.md section 53). The var blocks are generated; `cell` is declared here (the generator types it Type).

// tg: code/game/objects/items/tools/medical/defib.dm:6
public partial class obj_item_defibrillator : obj_item
{
    // tg: code/game/objects/items/tools/medical/defib.dm:2  #define HALFWAYCRITDEATH ((HEALTH_THRESHOLD_CRIT + HEALTH_THRESHOLD_DEAD) * 0.5) (file-local)
    internal const float HALFWAYCRITDEATH = (HEALTH_THRESHOLD_CRIT + HEALTH_THRESHOLD_DEAD) * 0.5f;
    // tg: code/game/objects/items/tools/medical/defib.dm:4  #define DEFIB_RESET_TIMER 3 MINUTES (file-local)
    private const float DEFIB_RESET_TIMER = 3 * MINUTES;

    // tg: code/game/objects/items/tools/medical/defib.dm:7-21  The type's overrides (rules 10.2): a hand-written var (`cell`),
    // so they are hand-written too (types-converter.md 3, "Hand"; the conformance test compares them with tg's).
    public obj_item_defibrillator()
    {
        name = "defibrillator";
        desc = "A device that delivers powerful shocks to detachable paddles that resuscitate incapacitated patients. Has a rear bracket for attachments to wall mounts and medical cyborgs.";
        icon = Dm.resource("icons/obj/medical/defib.dmi");
        icon_state = "defibunit";
        inhand_icon_state = "defibunit";
        lefthand_file = Dm.resource("icons/mob/inhands/equipment/medical_lefthand.dmi");
        righthand_file = Dm.resource("icons/mob/inhands/equipment/medical_righthand.dmi");
        slot_flags = ITEM_SLOT_BACK;
        force = 5;
        throwforce = 6;
        w_class = WEIGHT_CLASS_BULKY;
        armor_type = typeof(datum_armor_item_defibrillator);
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:19-21  the list overrides
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        actions_types = Dm.list(typeof(datum_action_item_action_toggle_paddles));
        custom_materials = Dm.list(Dm.kv(typeof(datum_material_iron), SHEET_MATERIAL_AMOUNT * 4), Dm.kv(typeof(datum_material_glass), SHEET_MATERIAL_AMOUNT * 2), Dm.kv(typeof(datum_material_silver), SHEET_MATERIAL_AMOUNT * 1.5f), Dm.kv(typeof(datum_material_gold), SHEET_MATERIAL_AMOUNT * 1.5f));
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:34
    public obj_item_stock_parts_power_store_cell cell;

    // tg: code/game/objects/items/tools/medical/defib.dm:54
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        return cell;
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:57
    public override object Initialize(object mapload = null, params object[] __extra) //starts without a cell for rnd
    {
        object dot = base.Initialize(mapload, __extra);
        paddles = Dm.@new(paddle_type, this) as obj_item_shockpaddles;
        update_power();
        RegisterSignal(paddles, COMSIG_DEFIBRILLATOR_SUCCESS, nameof(on_defib_success));
        Waiting.NoSleep(AddElement(typeof(datum_element_drag_pickup)));
        return dot;
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:69
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(cell_removable))
            return dot;
        if (cell != null)
            Dm.append(ref dot, span_notice(I18n.T("Use a screwdriver to remove the cell.")));
        else
            Dm.append(ref dot, span_warning(I18n.T("It has no power cell!")));
        return dot;
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:78
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        object dot = await base.fire_act(exposed_temperature, exposed_volume);
        if (paddles?.loc == this)
            await paddles.fire_act(exposed_temperature, exposed_volume);
        return dot;
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:83
    public override async DmTask<object> extinguish()
    {
        object dot = await base.extinguish();
        if (paddles?.loc == this)
            await paddles.extinguish();
        return dot;
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:88
    public virtual void update_power()
    {
        if (!QDELETED(cell))
        {
            if (QDELETED(paddles) || cell.charge < paddles.revivecost)
                powered = FALSE;
            else
                powered = TRUE;
        }
        else
        {
            powered = FALSE;
        }
        update_appearance();
        if (istype(loc, typeof(obj_machinery_defibrillator_mount)))
            loc.update_appearance();
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:100
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (!Dm.truthy(on) && Dm.truthy(paddle_state))
            Dm.append(ref dot, paddle_state);
        if (Dm.truthy(powered) && Dm.truthy(powered_state))
        {
            Dm.append(ref dot, powered_state);
            if (!QDELETED(cell) && Dm.truthy(charge_state))
            {
                float ratio = cell.charge / cell.maxcharge;
                ratio = ceil(ratio * 4) * 25;
                Dm.append(ref dot, Dm.text("[][]", charge_state, ratio));
            }
        }
        if (cell == null && Dm.truthy(nocell_state))
            Dm.append(ref dot, Dm.text("[]", nocell_state));
        if (!Dm.truthy(safety) && Dm.truthy(emagged_state))
            Dm.append(ref dot, emagged_state);
        return dot;
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:116
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        await base.on_craft_completion(components, current_recipe, crafter);
        cell = Dm.locate_type(typeof(obj_item_stock_parts_power_store), contents) as obj_item_stock_parts_power_store_cell;
        update_power();
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:121
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        Waiting.Detach(toggle_paddles(user as mob_living));
        return null;
    }

    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/game/objects/items/tools/medical/defib.dm:125
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (loc == user)
        {
            if (Dm.truthy(Dm.bitand(user.get_slot_by_item(this), slot_flags)))
                await ui_action_click(user, modifiers);
            else
                balloon_alert(user, I18n.T("equip the unit first!"));
            return null;
        }
        else if (istype(loc, typeof(obj_machinery_defibrillator_mount)))
        {
            await ui_action_click(user, modifiers); //checks for this are handled in defibrillator.mount.dm
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:136
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (cell == null || !Dm.truthy(cell_removable))
            return FALSE;

        await cell.forceMove(get_turf(this));
        balloon_alert(user, I18n.T("removed []", cell));
        cell = null;
        tool.play_tool_sound(this, 50);
        update_power();
        return TRUE;
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:147
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool == paddles)
        {
            await toggle_paddles(user);
            return NONE;
        }
        if (!istype(tool, typeof(obj_item_stock_parts_power_store_cell)))
            return NONE;

        obj_item_stock_parts_power_store_cell new_cell = tool as obj_item_stock_parts_power_store_cell;
        if (cell != null)
        {
            to_chat(user, span_warning(I18n.T("[] already has a cell!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (new_cell.maxcharge < paddles.revivecost)
        {
            to_chat(user, span_notice(I18n.T("[] requires a higher capacity cell.", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(new_cell, this)))
            return NONE;
        cell = new_cell;
        to_chat(user, span_notice(I18n.T("You install a cell in [].", this)));
        update_power();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:169
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        Dm.combine(ref obj_flags, EMAGGED);
        toggle_safety(user as mob_living);

        return TRUE;
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:175
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (cell != null && !Dm.truthy(Dm.bitand(dot, EMP_PROTECT_CONTENTS)))
            await deductcharge(STANDARD_CELL_CHARGE / Dm.num(severity));
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        update_power();
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) || Dm.truthy(recently_emped))
            return dot;
        toggle_safety();
        recently_emped = TRUE;
        return dot;
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:187
    public virtual void toggle_safety(mob_living user = null, float? force_safety = null)
    {
        force_safety ??= FALSE;
        if (Dm.truthy(force_safety) && !Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            safety = TRUE;
        else
            safety = FALSE;

        string enabled_or_disabled = Dm.truthy(safety) ? "enabled" : "disabled";
        if (user != null)
            balloon_alert(user, I18n.T("safety protocols []", enabled_or_disabled));

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return;

        _addtimer(Dm.@new<datum_callback>(this, nameof(force_safety_on)), DEFIB_RESET_TIMER, file: "code/game/objects/items/tools/medical/defib.dm", line: 200);
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:202
    public virtual void force_safety_on()
    {
        recently_emped = FALSE;
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return;

        toggle_safety(null, TRUE);
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:209
    public virtual async DmTask toggle_paddles(mob_living user = null)
    {
        on = Dm.truthy(on) ? FALSE : TRUE;
        if (Dm.truthy(on))
        {
            //Detach the paddles into the user's hands
            if (!Dm.truthy(await user.put_in_hands(paddles)))
            {
                on = FALSE;
                to_chat(user, span_warning(I18n.T("You need a free hand to hold the paddles!")));
                update_power();
                return;
            }
        }
        else
        {
            //Remove from their hands and back onto the defib unit
            await remove_paddles(user);
        }

        update_power();
        await update_item_action_buttons();
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:226
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.truthy(Dm.bitand(slot_flags, slot)))
        {
            await remove_paddles(user);
            update_power();
        }
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:232
    public virtual async DmTask remove_paddles(mob user = null) //this fox the bug with the paddles when other player stole you the defib when you have the paddles equiped
    {
        if (ismob(paddles.loc))
        {
            mob M = paddles.loc as mob;
            await M.dropItemToGround(paddles, TRUE);
        }
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:238
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(on))
        {
            mob M = get(paddles, typeof(mob)) as mob;
            Waiting.NoSleep(remove_paddles(M));
        }
        QDEL_NULL(ref paddles);
        QDEL_NULL(ref cell);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:246
    public virtual async DmTask deductcharge(object chrgdeductamt = null)
    {
        if (QDELETED(cell))
            return;

        if (cell.charge < (paddles.revivecost + Dm.num(chrgdeductamt)))
            powered = FALSE;
        if (!Dm.truthy(await cell.use(chrgdeductamt)))
            powered = FALSE;

        update_power();
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:257
    public virtual void cooldowncheck()
    {
        _addtimer(Dm.@new<datum_callback>(this, nameof(finish_charging)), cooldown_duration, file: "code/game/objects/items/tools/medical/defib.dm", line: 258);
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:260
    public virtual void finish_charging()
    {
        if (cell != null)
        {
            if (cell.charge >= paddles.revivecost)
            {
                visible_message(span_notice(I18n.T("[] beeps: Unit ready.", this)));
                playsound(this, Dm.resource("sound/machines/defib/defib_ready.ogg"), 50, FALSE);
            }
            else
            {
                visible_message(span_notice(I18n.T("[] beeps: Charge depleted.", this)));
                playsound(this, Dm.resource("sound/machines/defib/defib_failed.ogg"), 50, FALSE);
            }
        }
        paddles.cooldown = FALSE;
        paddles.update_appearance();
        update_power();
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:272
    [SignalHandler]
    public virtual object on_defib_success(obj_item_shockpaddles source = null)
    {
        Waiting.NoSleep(deductcharge(source.revivecost));
        source.cooldown = TRUE;
        cooldowncheck();
        return COMPONENT_DEFIB_STOP;
    }
}

// tg: code/game/objects/items/tools/medical/defib.dm:64
public partial class obj_item_defibrillator_loaded : obj_item_defibrillator
{
    // tg: code/game/objects/items/tools/medical/defib.dm:64
    public override object Initialize(object mapload = null, params object[] __extra) //starts with hicap
    {
        object dot = base.Initialize(mapload, __extra);
        cell = Dm.@new<obj_item_stock_parts_power_store_cell>(this);
        update_power();
        return dot;
    }
}

// tg: code/game/objects/items/tools/medical/defib.dm:294
public partial class obj_item_defibrillator_compact_loaded : obj_item_defibrillator_compact
{
    // tg: code/game/objects/items/tools/medical/defib.dm:294
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        cell = Dm.@new<obj_item_stock_parts_power_store_cell>(this);
        update_power();
        return dot;
    }
}

// tg: code/game/objects/items/tools/medical/defib.dm:324
public partial class obj_item_defibrillator_compact_combat_loaded : obj_item_defibrillator_compact_combat
{
    // tg: code/game/objects/items/tools/medical/defib.dm:324
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        cell = Dm.@new<obj_item_stock_parts_power_store_cell_infinite>(this);
        update_power();
        return dot;
    }
}

//paddles

// tg: code/game/objects/items/tools/medical/defib.dm:340
public partial class obj_item_shockpaddles : obj_item
{
    // tg: code/game/objects/items/tools/medical/defib.dm:3  #define DEFIB_CAN_HURT(source) (source.combat || (source.req_defib && !source.defib.safety)) (file-local)
    private static bool DEFIB_CAN_HURT(obj_item_shockpaddles source) => Dm.truthy(source.combat) || (Dm.truthy(source.req_defib) && !Dm.truthy(source.defib.safety));

    // tg: code/game/objects/items/tools/medical/defib.dm:364  (the earlier of the type's two Initialize definitions, rules 2.5)
    public object Initialize__d0(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob), ITEM_SLOT_BACK));
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed), Dm.named("force_unwielded", 8), Dm.named("force_wielded", 12)));
        RegisterSignal(this, COMSIG_HUMAN_NON_STORAGE_HOTKEY, nameof(on_non_storage_hotkey));
        return dot;
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:370
    public override object Destroy(float? force = null)
    {
        defib = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:374
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.truthy(req_defib))
            return;
        RegisterSignal(user, COMSIG_MOVABLE_MOVED, nameof(check_range));
        RegisterSignal(defib, COMSIG_MOVABLE_MOVED, nameof(check_range));
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:381
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        check_range();
        return dot;
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:385
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        object dot = await base.fire_act(exposed_temperature, exposed_volume);
        if ((Dm.truthy(req_defib) && defib != null) && loc != defib)
            await defib.fire_act(exposed_temperature, exposed_volume);
        return dot;
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:390
    [SignalHandler]
    public virtual void check_range()
    {
        if (!Dm.truthy(req_defib) || defib == null)
            return;
        if (!in_range(this, defib))
        {
            if (isliving(loc))
            {
                mob_living user = loc as mob_living;
                to_chat(user, span_warning(I18n.T("[]'s paddles overextend and come out of your hands!", defib)));
            }
            else
            {
                visible_message(span_notice(I18n.T("[] snap back into [].", this, defib)));
            }
            snap_back();
        }
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:403
    public virtual void recharge(object time = null)
    {
        time ??= 0;
        if (Dm.truthy(req_defib))
            return;
        cooldown = TRUE;
        update_appearance();
        _addtimer(Dm.@new<datum_callback>(this, nameof(finish_recharge)), Dm.num(time), file: "code/game/objects/items/tools/medical/defib.dm", line: 408);
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:410
    public virtual void finish_recharge()
    {
        turf current_turf = get_turf(this);
        current_turf.audible_message(span_notice(I18n.T("[] beeps: Unit is recharged.", this)));
        playsound(this, Dm.resource("sound/machines/defib/defib_ready.ogg"), 50, FALSE);
        cooldown = FALSE;
        update_appearance();
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:417  (the later definition: its ..() is Initialize__d0)
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = Initialize__d0(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NO_STORAGE_INSERT, TRAIT_GENERIC); //stops shockpaddles from being inserted in BoH
        if (!Dm.truthy(req_defib))
            return dot; //If it doesn't need a defib, just say it exists
        if (loc == null || !istype(loc, typeof(obj_item_defibrillator))) //To avoid weird issues from admin spawns
            return INITIALIZE_HINT_QDEL;
        defib = loc as obj_item_defibrillator;
        busy = FALSE;
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:428
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_danger(I18n.T("[] is putting the live paddles on [] chest! It looks like [] trying to commit suicide!", user, user.p_their(), user.p_theyre())));
        if (Dm.truthy(req_defib))
            await defib.deductcharge(revivecost);
        playsound(this, Dm.resource("sound/machines/defib/defib_zap.ogg"), 50, TRUE, -1);
        return OXYLOSS;
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:435
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, HAS_TRAIT(this, TRAIT_WIELDED) ? 1 : 0);
        inhand_icon_state = icon_state;
        if (Dm.truthy(cooldown))
            icon_state = Dm.text("[][]_cooldown", base_icon_state, HAS_TRAIT(this, TRAIT_WIELDED) ? 1 : 0);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:442
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        if (!Dm.truthy(req_defib))
            return await base.dropped(user, silent, __extra);
        UnregisterSignal(defib, COMSIG_MOVABLE_MOVED);
        if (user != null)
        {
            UnregisterSignal(user, COMSIG_MOVABLE_MOVED);
            to_chat(user, span_notice(I18n.T("The paddles snap back into the main unit.")));
        }
        snap_back();
        return await base.dropped(user, silent, __extra);
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:452
    public virtual void snap_back()
    {
        if (defib == null)
            return;
        defib.on = FALSE;
        Waiting.NoSleep(forceMove(defib));
        defib.update_power();
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:459
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob_living M = target_mob;
        if (Dm.truthy(busy))
            return null;
        defib?.update_power();
        if (Dm.truthy(req_defib) && !Dm.truthy(defib.powered))
        {
            user.visible_message(span_warning(I18n.T("[] beeps: Not enough charge!", defib)));
            playsound(this, Dm.resource("sound/machines/defib/defib_failed.ogg"), 50, FALSE);
            return null;
        }
        if (!HAS_TRAIT(this, TRAIT_WIELDED))
        {
            if (iscyborg(user))
                to_chat(user, span_warning(I18n.T("You must activate the paddles in your active module before you can use them on someone!")));
            else
                to_chat(user, span_warning(I18n.T("You need to wield the paddles in both hands before you can use them on someone!")));
            return null;
        }
        if (Dm.truthy(cooldown))
        {
            if (Dm.truthy(req_defib))
                to_chat(user, span_warning(I18n.T("[] is recharging!", defib)));
            else
                to_chat(user, span_warning(I18n.T("[] are recharging!", this)));
            return null;
        }

        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
        {
            await do_disarm(M, user);
            return null;
        }

        if (!iscarbon(M))
        {
            if (Dm.truthy(req_defib))
                to_chat(user, span_warning(I18n.T("The instructions on [] don't mention how to revive that...", defib)));
            else
                to_chat(user, span_warning(I18n.T("You aren't sure how to revive that...")));
            return null;
        }
        mob_living_carbon H = M as mob_living_carbon;

        if (!Dm.eq(user.zone_selected, BODY_ZONE_CHEST))
        {
            to_chat(user, span_warning(I18n.T("You need to target your patient's chest with []!", this)));
            return null;
        }

        if (Dm.truthy(user.combat_mode))
        {
            await do_harm(H, user);
            return null;
        }

        if (Dm.eq(H.can_defib(), DEFIB_POSSIBLE))
        {
            H.notify_revival(I18n.T("Your heart is being defibrillated!"));
            H.grab_ghost(); // Shove them back in their body.
        }

        await do_help(H, user);
        return null;
    }

    /// Called whenever the paddles successfully shock something
    // tg: code/game/objects/items/tools/medical/defib.dm:507
    public virtual void do_success()
    {
        if (Dm.truthy(busy))
            busy = FALSE;

        update_appearance();
        if (Dm.truthy(Dm.bitand(!Dm.truthy(_listen_lookup?[COMSIG_DEFIBRILLATOR_SUCCESS]) ? (object)NONE : _SendSignal(COMSIG_DEFIBRILLATOR_SUCCESS, Dm.list(this)), COMPONENT_DEFIB_STOP)))
            return;
        recharge(recharge_time);
    }

    /// Called whenever the paddles fail to shock something after a do_x proc
    // tg: code/game/objects/items/tools/medical/defib.dm:517
    public virtual void do_cancel()
    {
        if (Dm.truthy(busy))
            busy = FALSE;

        update_appearance();
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:523
    public virtual async DmTask shock_pulling(object dmg = null, mob H = null)
    {
        if (isliving(H.pulledby)) //CLEAR!
        {
            mob_living M = H.pulledby as mob_living;
            if (Dm.truthy(await M.electrocute_act(dmg, H)))
            {
                M.visible_message(span_danger(I18n.T("[] is electrocuted by [] contact with []!", M, M.p_their(), H)));
                await M.emote("scream");
            }
        }
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:530
    public virtual async DmTask do_disarm(mob_living M = null, mob_living user = null)
    {
        if (!DEFIB_CAN_HURT(this))
            return;
        busy = TRUE;
        M.visible_message(span_danger(I18n.T("[] touches [] with []!", user, M, this)),
            span_userdanger(I18n.T("[] touches [] with []!", user, M, this)));
        await M.adjust_stamina_loss(60);
        await M.Knockdown(75);
        await M.set_jitter_if_lower(100 * SECONDS);
        await M.apply_status_effect(typeof(datum_status_effect_convulsing));
        playsound(this, Dm.resource("sound/machines/defib/defib_zap.ogg"), 50, TRUE, -1);
        if (HAS_TRAIT(M, MOB_ORGANIC)) // tg tests a mob biotype flag as a trait: never true
        {
            await M.emote("gasp");
        }
        log_combat(user, M, "zapped", this);
        do_success();
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:546
    public virtual async DmTask do_harm(mob_living_carbon H = null, mob_living user = null)
    {
        if (!DEFIB_CAN_HURT(this))
            return;
        user.visible_message(span_warning(I18n.T("[] begins to place [] on []'s chest.", user, this, H)),
            span_warning(I18n.T("You overcharge the paddles and begin to place them onto []'s chest...", H)));
        busy = TRUE;
        update_appearance();
        if (Dm.truthy(await do_after(user, 1.5f * SECONDS, H, extra_checks: Dm.@new<datum_callback>(this, nameof(is_wielded)))))
        {
            user.visible_message(span_notice(I18n.T("[] places [] on []'s chest.", user, this, H)),
                span_warning(I18n.T("You place [] on []'s chest and begin to charge them.", this, H)));
            turf T = get_turf(defib);
            playsound(this, Dm.resource("sound/machines/defib/defib_charge.ogg"), 50, FALSE);
            if (Dm.truthy(req_defib))
                T.audible_message(span_warning(I18n.T("\\The [] lets out an urgent beep and lets out a steadily rising hum...", defib)));
            else
                user.audible_message(span_warning(I18n.T("[] let out an urgent beep.", this)));
            if (Dm.truthy(await do_after(user, 1.5f * SECONDS, H, extra_checks: Dm.@new<datum_callback>(this, nameof(is_wielded))))) //Takes longer due to overcharging
            {
                if (H == null)
                {
                    do_cancel();
                    return;
                }
                if (H != null && H.stat == DEAD)
                {
                    to_chat(user, span_warning(I18n.T("[] is dead.", H)));
                    playsound(this, Dm.resource("sound/machines/defib/defib_failed.ogg"), 50, FALSE);
                    do_cancel();
                    return;
                }
                user.visible_message(span_bolddanger(I18n.T("<i>[] shocks [] with \\the []!", user, H, this)), span_warning(I18n.T("You shock [] with \\the []!", H, this)));
                playsound(this, Dm.resource("sound/machines/defib/defib_zap.ogg"), 100, TRUE, -1);
                playsound(this, Dm.resource("sound/items/weapons/egloves.ogg"), 100, TRUE, -1);
                await H.emote("scream");
                await shock_pulling(45, H);
                if (Dm.truthy(H.can_heartattack()) && !Dm.truthy(H.undergoing_cardiac_arrest()))
                {
                    if (!IS_UNCONSCIOUS_OR_CRIT(H))
                    {
                        H.visible_message(span_warning(I18n.T("[] thrashes wildly, clutching at [] chest!", H, H.p_their())),
                            span_userdanger(I18n.T("You feel a horrible agony in your chest!")));
                    }
                    H.set_heartattack(TRUE);
                }
                await H.apply_damage(50, BURN, BODY_ZONE_CHEST);
                log_combat(user, H, "overloaded the heart of", defib);
                await H.Paralyze(100);
                await H.set_jitter_if_lower(200 * SECONDS);
                do_success();
                return;
            }
        }
        do_cancel();
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:589
    public virtual async DmTask do_help(mob_living_carbon H = null, mob_living user = null)
    {
        user.visible_message(span_warning(I18n.T("[] begins to place [] on []'s chest.", user, this, H)), span_warning(I18n.T("You begin to place [] on []'s chest...", this, H)));
        busy = TRUE;
        update_appearance();
        object speaker = Dm.truthy(req_defib) ? defib : this; // [req_defib ? "[defib]" : "[src]"]
        if (Dm.truthy(await do_after(user, 3 * SECONDS, H, extra_checks: Dm.@new<datum_callback>(this, nameof(is_wielded))))) //beginning to place the paddles on patient's chest to allow some time for people to move away to stop the process
        {
            user.visible_message(span_notice(I18n.T("[] places [] on []'s chest.", user, this, H)), span_warning(I18n.T("You place [] on []'s chest.", this, H)));
            playsound(this, Dm.resource("sound/machines/defib/defib_charge.ogg"), 75, FALSE);
            obj_item_organ heart = H.get_organ_by_type(typeof(obj_item_organ_heart)) as obj_item_organ;
            if (Dm.truthy(await do_after(user, 2 * SECONDS, H, extra_checks: Dm.@new<datum_callback>(this, nameof(is_wielded))))) //placed on chest and short delay to shock for dramatic effect, revive time is 5sec total
            {
                if ((!Dm.truthy(combat) && !Dm.truthy(req_defib)) || (Dm.truthy(req_defib) && !Dm.truthy(defib.combat)))
                {
                    foreach (obj_item_clothing C in Dm.each<obj_item_clothing>(H.get_equipped_items()))
                    {
                        if (Dm.truthy(Dm.bitand(C.body_parts_covered, CHEST)) && Dm.truthy(Dm.bitand(C.clothing_flags, THICKMATERIAL))) //check to see if something is obscuring their chest.
                        {
                            user.audible_message(span_warning(I18n.T("[] buzzes: Patient's chest is obscured. Operation aborted.", speaker)));
                            playsound(this, Dm.resource("sound/machines/defib/defib_failed.ogg"), 50, FALSE);
                            do_cancel();
                            return;
                        }
                    }
                }
                if (Dm.truthy(Dm.bitand(!Dm.truthy(H._listen_lookup?[COMSIG_DEFIBRILLATOR_PRE_HELP_ZAP]) ? (object)NONE : H._SendSignal(COMSIG_DEFIBRILLATOR_PRE_HELP_ZAP, Dm.list(H, user, this)), COMPONENT_DEFIB_STOP)))
                {
                    do_cancel();
                    return;
                }
                if (H.stat == DEAD)
                {
                    H.visible_message(span_warning(I18n.T("[]'s body convulses a bit.", H)));
                    playsound(this, SFX_BODYFALL, 50, TRUE);
                    playsound(this, Dm.resource("sound/machines/defib/defib_zap.ogg"), 75, TRUE, -1);
                    await shock_pulling(30, H);

                    object defib_result = H.can_defib();
                    string fail_reason = null;

                    switch (Dm.num(defib_result))
                    {
                        case DEFIB_FAIL_SUICIDE:
                            fail_reason = I18n.T("Recovery of patient impossible. Further attempts futile.");
                            break;
                        case DEFIB_FAIL_NO_HEART:
                            fail_reason = I18n.T("Patient's heart is missing.");
                            break;
                        case DEFIB_FAIL_FAILING_HEART:
                            fail_reason = I18n.T("Patient's heart too damaged, replace or repair and try again.");
                            break;
                        case DEFIB_FAIL_TISSUE_DAMAGE:
                            fail_reason = I18n.T("Tissue damage too severe, repair and try again.");
                            break;
                        case DEFIB_FAIL_HUSK:
                            fail_reason = I18n.T("Patient's body is a mere husk, repair and try again.");
                            break;
                        case DEFIB_FAIL_FAILING_BRAIN:
                            fail_reason = I18n.T("Patient's brain is too damaged, repair and try again.");
                            break;
                        case DEFIB_FAIL_NO_INTELLIGENCE:
                            fail_reason = I18n.T("No intelligence pattern can be detected in patient's brain. Further attempts futile.");
                            break;
                        case DEFIB_FAIL_NO_BRAIN:
                            fail_reason = I18n.T("Patient's brain is missing. Further attempts futile.");
                            break;
                        case DEFIB_FAIL_BLACKLISTED:
                            fail_reason = I18n.T("Patient has been blacklisted from revival. Further attempts futile.");
                            break;
                        case DEFIB_FAIL_GOLEM:
                            fail_reason = I18n.T("Patient is constructed from inorganic materials. Further attempts futile, though manual reconstruction is possible.");
                            break;
                    }

                    if (Dm.truthy(fail_reason))
                    {
                        user.visible_message(span_warning(I18n.T("[] buzzes: Resuscitation failed - []", speaker, fail_reason)));
                        playsound(this, Dm.resource("sound/machines/defib/defib_failed.ogg"), 50, FALSE);
                    }
                    else
                    {
                        float total_brute = Dm.num(H.get_brute_loss());
                        float total_burn = Dm.num(H.get_fire_loss());

                        float need_mob_update = FALSE;
                        //If the body has been fixed so that they would not be in crit when defibbed, give them oxyloss to put them back into crit
                        if (H.health > obj_item_defibrillator.HALFWAYCRITDEATH)
                        {
                            need_mob_update += Dm.num(await H.adjust_oxy_loss(H.health - obj_item_defibrillator.HALFWAYCRITDEATH, updating_health: FALSE));
                        }
                        else
                        {
                            float overall_damage = total_brute + total_burn + Dm.num(H.get_tox_loss()) + Dm.num(H.get_oxy_loss());
                            float mobhealth = H.health;
                            need_mob_update += Dm.num(await H.adjust_oxy_loss((mobhealth - obj_item_defibrillator.HALFWAYCRITDEATH) * Dm.num(Dm.div(H.get_oxy_loss(), overall_damage)), updating_health: FALSE));
                            need_mob_update += Dm.num(await H.adjust_tox_loss((mobhealth - obj_item_defibrillator.HALFWAYCRITDEATH) * Dm.num(Dm.div(H.get_tox_loss(), overall_damage)), updating_health: FALSE, forced: TRUE)); // force tox heal for toxin lovers too
                            need_mob_update += Dm.num(await H.adjust_fire_loss((mobhealth - obj_item_defibrillator.HALFWAYCRITDEATH) * Dm.num(Dm.div(total_burn, overall_damage)), updating_health: FALSE));
                            need_mob_update += Dm.num(await H.adjust_brute_loss((mobhealth - obj_item_defibrillator.HALFWAYCRITDEATH) * Dm.num(Dm.div(total_brute, overall_damage)), updating_health: FALSE));
                        }
                        if (Dm.truthy(need_mob_update))
                            await H.updatehealth(); // Previous "adjust" procs don't update health, so we do it manually.
                        user.visible_message(span_notice(I18n.T("[] pings: Resuscitation successful.", speaker)));
                        playsound(this, Dm.resource("sound/machines/defib/defib_success.ogg"), 50, FALSE);
                        H.set_heartattack(FALSE);
                        if (Dm.eq(defib_result, DEFIB_POSSIBLE))
                            H.grab_ghost();
                        await H.revive();
                        await H.emote("gasp");
                        await H.set_jitter_if_lower(200 * SECONDS);
                        if (Dm.truthy(H._listen_lookup?[COMSIG_LIVING_MINOR_SHOCK]))
                            H._SendSignal(COMSIG_LIVING_MINOR_SHOCK, Dm.list(H));
                        if (HAS_MIND_TRAIT(user, TRAIT_MORBID))
                            await user.add_mood_event("morbid_saved_life", typeof(datum_mood_event_morbid_saved_life));
                        else
                            await user.add_mood_event("saved_life", typeof(datum_mood_event_saved_life));
                        log_combat(user, H, "revived", defib);
                    }
                    do_success();
                    return;
                }
                else if (!Dm.truthy(H.get_organ_by_type(typeof(obj_item_organ_heart))))
                {
                    user.visible_message(span_warning(I18n.T("[] buzzes: Patient's heart is missing. Operation aborted.", speaker)));
                    playsound(this, Dm.resource("sound/machines/defib/defib_failed.ogg"), 50, FALSE);
                }
                else if (Dm.truthy(H.undergoing_cardiac_arrest()))
                {
                    playsound(this, Dm.resource("sound/machines/defib/defib_zap.ogg"), 50, TRUE, -1);
                    if (!Dm.truthy(Dm.bitand(heart.organ_flags, ORGAN_FAILING)))
                    {
                        H.set_heartattack(FALSE);
                        do_success();
                        user.visible_message(span_notice(I18n.T("[] pings: Patient's heart is now beating again.", speaker)));
                    }
                    else
                    {
                        user.visible_message(span_warning(I18n.T("[] buzzes: Resuscitation failed, heart damage detected.", speaker)));
                    }
                }
                else if (Dm.truthy(H.has_status_effect(typeof(datum_status_effect_heart_attack))))
                {
                    user.visible_message(span_notice(I18n.T("[] pings: Patient's heart has stabilized, further applications may be necessary.", speaker)));
                    if (Dm.truthy(H._listen_lookup?[COMSIG_HEARTATTACK_DEFIB]))
                        H._SendSignal(COMSIG_HEARTATTACK_DEFIB, Dm.list(H));
                    playsound(this, Dm.resource("sound/machines/defib/defib_zap.ogg"), 50, TRUE, -1);
                    do_success();
                }
                else
                {
                    user.visible_message(span_warning(I18n.T("[] buzzes: Patient is not in a valid state. Operation aborted.", speaker)));
                    playsound(this, Dm.resource("sound/machines/defib/defib_failed.ogg"), 50, FALSE);
                }
            }
        }
        do_cancel();
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:696
    public virtual object is_wielded()
    {
        return HAS_TRAIT(this, TRAIT_WIELDED) ? TRUE : FALSE;
    }

    // tg: code/game/objects/items/tools/medical/defib.dm:699
    [SignalHandler]
    public virtual object on_non_storage_hotkey(datum source = null, mob_living_carbon_human user = null, obj_item possible_storage = null)
    {
        if (possible_storage == defib)
        {
            Waiting.NoSleep(user.dropItemToGround(this));
            return COMPONENT_STORAGE_HOTKEY_HANDLED;
        }
        return NONE;
    }
}

// tg: code/game/objects/items/tools/medical/defib.dm:706
public partial class obj_item_shockpaddles_cyborg : obj_item_shockpaddles
{
    // tg: code/game/objects/items/tools/medical/defib.dm:713
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (iscyborg(user))
        {
            mob_living_silicon_robot R = user as mob_living_silicon_robot;
            if (Dm.truthy(R.emagged))
                combat = TRUE;
            else
                combat = FALSE;
        }
        else
        {
            combat = FALSE;
        }

        return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
    }
}
