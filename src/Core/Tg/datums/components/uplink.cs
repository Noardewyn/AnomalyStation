// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/uplink.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A2 (D316): the whole file: the uplink hidden in a PDA (the messenger's ringtone), a radio (a code said on the uplink
// channel), a pen (its twist) or an implant (the implant item is G3's), its examine, locking, telecrystals and refunds, the Uplink
// window (DV44: the uplink's item list, tg's /datum/asset/json/uplink, comes with the window's static data), the failsafe. The admins'
// message and the attack log of the failsafe are deferred leaves (not-translated.md section 131). The var block is generated.

/**
 * Uplinks
 *
 * All /obj/item(s) have a hidden_uplink var. By default it's null. Give the item one with 'new(src') (it must be in its contents). Then add 'uses.'
 * Use whatever conditionals you want to check that the user has an uplink, and then call interact() on their uplink.
 * You might also want the uplink menu to open if active. Check if the uplink is 'active' and then interact() with it.
**/
// tg: code/datums/components/uplink.dm:10
public partial class datum_component_uplink : datum_component
{
    // tg: code/datums/components/uplink.dm:1  #define PEN_ROTATIONS 2 (file-local, #undef at :433)
    private const int PEN_ROTATIONS = 2;

    // tg: code/datums/components/uplink.dm:42
    [DmParams("owner", "lockable", "enabled", "uplink_flag", "starting_tc", "uplink_handler_override")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object owner = Dm.arg(__extra, 0);
        object lockable = Dm.arg(__extra, 1) ?? TRUE;
        object enabled = Dm.arg(__extra, 2) ?? FALSE;
        object uplink_flag = Dm.arg(__extra, 3) ?? UPLINK_TRAITORS;
        object starting_tc = Dm.arg(__extra, 4) ?? TELECRYSTALS_DEFAULT;
        datum_uplink_handler uplink_handler_override = Dm.arg(__extra, 5) as datum_uplink_handler;

        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        RegisterSignal(parent, COMSIG_ATOM_ATTACKBY, nameof(OnAttackBy));
        RegisterSignal(parent, COMSIG_ITEM_ATTACK_SELF, nameof(interact));
        if (parent is obj_item_implant)
        {
            RegisterSignal(parent, COMSIG_IMPLANT_ACTIVATED, nameof(implant_activation));
            RegisterSignal(parent, COMSIG_IMPLANT_IMPLANTING, nameof(implanting));
            RegisterSignal(parent, COMSIG_IMPLANT_OTHER, nameof(old_implant));
            RegisterSignal(parent, COMSIG_IMPLANT_EXISTING_UPLINK, nameof(new_implant));
        }
        else if (parent is obj_item_modular_computer)
        {
            RegisterSignal(parent, COMSIG_TABLET_CHANGE_ID, nameof(new_ringtone));
            RegisterSignal(parent, COMSIG_TABLET_CHECK_DETONATE, nameof(check_detonate));
        }
        else if (parent is obj_item_radio)
            RegisterSignal(parent, COMSIG_RADIO_NEW_MESSAGE, nameof(new_message));
        else if (parent is obj_item_pen)
            RegisterSignal(parent, COMSIG_PEN_ROTATED, nameof(pen_rotation));

        if (Dm.truthy(owner))
        {
            this.owner = owner;
            LAZYINITLIST(ref GLOB.uplink_purchase_logs_by_key);
            if (Dm.truthy(GLOB.uplink_purchase_logs_by_key[owner]))
                purchase_log = GLOB.uplink_purchase_logs_by_key[owner] as datum_uplink_purchase_log;
            else
                purchase_log = await Dm.new_async<datum_uplink_purchase_log>(owner, this);
            RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        }
        this.lockable = Dm.num(lockable);
        active = Dm.num(enabled);
        if (uplink_handler_override == null)
        {
            uplink_handler = await Dm.new_async<datum_uplink_handler>();
            uplink_handler.uplink_flag = Dm.num(uplink_flag);
            uplink_handler.telecrystals = Dm.num(starting_tc);
            uplink_handler.purchase_log = purchase_log;
        }
        else
            uplink_handler = uplink_handler_override;
        RegisterSignal(uplink_handler, COMSIG_UPLINK_HANDLER_ON_UPDATE, nameof(handle_uplink_handler_update));
        if (!Dm.truthy(this.lockable))
        {
            active = TRUE;
            locked = FALSE;
        }

        previous_attempts = Dm.list();
        return null;
    }

    // tg: code/datums/components/uplink.dm:101
    [SignalHandler]
    public virtual void handle_uplink_handler_update()
    {
        SStgui?.update_uis(this); // rules 10.6: a test harness world may lack SStgui (a syndrone sets its uplink's crystals in its Initialize)
    }

    // tg: code/datums/components/uplink.dm:105
    [DmParams("uplink")]
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        datum_component_uplink uplink = C as datum_component_uplink;
        lockable = Dm.num(Dm.bitor(lockable, uplink.lockable));
        active = Dm.num(Dm.bitor(active, uplink.active));
        uplink_handler.uplink_flag = Dm.num(Dm.bitor(uplink_handler.uplink_flag, uplink.uplink_handler.uplink_flag));
    }

    // tg: code/datums/components/uplink.dm:110
    public override object Destroy(float? force = null)
    {
        purchase_log = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/uplink.dm:114
    public virtual async DmTask load_tc(mob user = null, obj_item_stack_telecrystal telecrystals = null, float? silent = null)
    {
        silent ??= FALSE;
        if (!Dm.truthy(silent))
            to_chat(user, span_notice(I18n.T("You slot [] into [] and charge its internal uplink.", telecrystals, parent)));
        float amt = Dm.num(telecrystals.amount);
        uplink_handler.add_telecrystals(amt);
        await telecrystals.use(amt);
        log_uplink(Dm.text("[] loaded [] telecrystals into []'s uplink", key_name(user), amt, parent));
    }

    // tg: code/datums/components/uplink.dm:122
    [SignalHandler]
    public virtual void OnAttackBy(datum source = null, obj_item item = null, mob user = null)
    {
        if (!Dm.truthy(active))
            return; //no hitting everyone/everything just to try to slot tcs in!

        if (item is obj_item_stack_telecrystal telecrystals)
            Waiting.NoSleep(load_tc(user, telecrystals));

        if (item == null)
            return;

        if (Dm.truthy(item._listen_lookup?[COMSIG_ITEM_ATTEMPT_TC_REIMBURSE]))
            item._SendSignal(COMSIG_ITEM_ATTEMPT_TC_REIMBURSE, Dm.list(item, user, this));
    }

    // tg: code/datums/components/uplink.dm:135
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        if (!Dm.eq(user, owner))
            return;
        Dm.append(ref examine_list, span_warning(I18n.T("[] contains your hidden uplink[].", parent,
            Dm.truthy(unlock_code) ? I18n.T(", the code to unlock it is []", span_boldwarning(Dm.text("[]", unlock_code))) : null)));

        if (Dm.truthy(failsafe_code))
            Dm.append(ref examine_list, span_warning(I18n.T("The failsafe code is [].", span_boldwarning(Dm.text("[]", failsafe_code)))));
    }

    // tg: code/datums/components/uplink.dm:146
    [SignalHandler]
    public virtual object interact(datum source = null, mob user = null)
    {
        if (Dm.truthy(locked))
            return null;
        active = TRUE;
        if (user != null)
            Waiting.Detach(ui_interact(user)); // INVOKE_ASYNC(src, PROC_REF(ui_interact), user)
        // an unlocked uplink blocks also opening the PDA or headset menu
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/datums/components/uplink.dm:158
    public override object ui_state(mob user = null)
    {
        return GLOB.inventory_state;
    }

    // tg: code/datums/components/uplink.dm:161
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        active = TRUE;
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "Uplink", name);
            // This UI is only ever opened by one person,
            // and never is updated outside of user input.
            ui.set_autoupdate(FALSE);
            await ui.open();
        }
        return null;
    }

    // tg: code/datums/components/uplink.dm:171
    public override async DmTask<object> ui_data(mob user = null)
    {
        if (user.mind == null)
            return null;
        DmList data = Dm.list();
        data["telecrystals"] = uplink_handler.telecrystals;
        data["joined_population"] = length(GLOB.joined_player_list);

        if (uplink_handler.primary_objectives != null)
        {
            DmList primary_objectives = Dm.list();
            foreach (datum_objective task in Dm.each<datum_objective>(uplink_handler.primary_objectives))
            {
                DmList task_data = Dm.list();
                if (Dm.num(length(primary_objectives)) > Dm.num(length(GLOB.phonetic_alphabet)))
                    task_data["task_name"] = Dm.text("DIRECTIVE []", Dm.num(length(primary_objectives)) + 1); //The english alphabet is WEAK
                else
                    task_data["task_name"] = Dm.text("DIRECTIVE []", uppertext(GLOB.phonetic_alphabet[Dm.num(length(primary_objectives)) + 1]));
                task_data["task_text"] = task.explanation_text;
                Dm.append(primary_objectives, Dm.list(task_data));
            }
            data["primary_objectives"] = primary_objectives;
        }

        DmList stock_list = uplink_handler.item_stock.Copy();
        DmList extra_purchasable_stock = Dm.list();
        DmList extra_purchasable = Dm.list();
        foreach (datum_uplink_item item in Dm.each<datum_uplink_item>(uplink_handler.extra_purchasable))
        {
            if (Dm.@in(item.stock_key, stock_list))
                extra_purchasable_stock[REF(item)] = stock_list[item.stock_key];
            Type actual_item = item.item;
            Dm.append(extra_purchasable, Dm.list(Dm.list(
                Dm.kv("id", item.type),
                Dm.kv("name", item.name),
                Dm.kv("icon", Dm.initial(actual_item, (atom p) => p.icon)),
                Dm.kv("icon_state", Dm.initial(actual_item, (atom p) => p.icon_state)),
                Dm.kv("cost", item.cost),
                Dm.kv("desc", item.desc),
                Dm.kv("category", item.category != null ? Dm.initial(item.category, (datum_uplink_category p) => p.name) : null),
                Dm.kv("purchasable_from", item.purchasable_from),
                Dm.kv("restricted", item.restricted),
                Dm.kv("limited_stock", item.limited_stock),
                Dm.kv("restricted_roles", item.restricted_roles),
                Dm.kv("restricted_species", item.restricted_species),
                Dm.kv("population_minimum", item.population_minimum),
                Dm.kv("ref", REF(item)))));
        }

        DmList remaining_stock = Dm.list();
        foreach (object item in Dm.each(stock_list))
            remaining_stock[item] = stock_list[item];
        data["extra_purchasable"] = extra_purchasable;
        data["extra_purchasable_stock"] = extra_purchasable_stock;
        data["current_stock"] = remaining_stock;
        data["shop_locked"] = uplink_handler.shop_locked;
        data["purchased_items"] = length(uplink_handler.purchase_log?.purchase_log);
        data["can_renegotiate"] = Dm.num(user.mind == uplink_handler.owner && Dm.eq(uplink_handler.can_replace_objectives == null ? null : await uplink_handler.can_replace_objectives.Invoke(), TRUE));
        return data;
    }

    // tg: code/datums/components/uplink.dm:229
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["uplink_flag"] = uplink_handler.uplink_flag;
        data["lockable"] = lockable;
        data["assigned_role"] = uplink_handler.assigned_role;
        data["assigned_species"] = uplink_handler.assigned_species;
        data["debug"] = uplink_handler.debug_mode;
        // DEVIATION(DV44): tg's page fetches the uplink's items and categories from an asset (ui_assets: /datum/asset/json/uplink,
        // asset_cache/assets/uplink.dm); the native window takes them with the static data (DV343: without the hidden entries)
        data["uplink_json"] = uplink_json;
        return data;
    }

    // tg: code/datums/components/uplink.dm:238
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/json/uplink)): the asset transport is the client's (DV44); the items come with the static data
        return Dm.list();
    }

    // tg: code/datums/components/uplink.dm:243
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(active))
            return null;
        switch (action)
        {
            case "buy":
            {
                datum_uplink_item item;
                if (Dm.truthy(@params["ref"]))
                {
                    item = Dm.locate_ref(@params["ref"]) is datum_uplink_item found && Dm.@in(found, uplink_handler.extra_purchasable) ? found : null;
                    if (item == null)
                        return null;
                }
                else
                {
                    object item_path = text2path(Dm.str(@params["path"]));
                    if (!ispath(item_path, typeof(datum_uplink_item)))
                        return null;
                    item = SStraitor.uplink_items_by_type[item_path] as datum_uplink_item;
                }
                await uplink_handler.purchase_item(ui.user, item, parent as atom_movable);
                break;
            }
            case "buy_raw_tc":
            {
                if (uplink_handler.telecrystals <= 0)
                    return null;
                object desired_amount = await tgui_input_number(ui.user, I18n.T("How many raw telecrystals to buy?"), I18n.T("Buy Raw TC"), @default: uplink_handler.telecrystals, max_value: uplink_handler.telecrystals);
                if (!Dm.truthy(desired_amount) || Dm.num(desired_amount) < 1)
                    return null;
                await uplink_handler.purchase_raw_tc(ui.user, desired_amount, parent as atom_movable);
                break;
            }
            case "lock":
                if (!Dm.truthy(lockable))
                    return TRUE;
                lock_uplink();
                break;
            case "renegotiate_objectives":
                if (uplink_handler.replace_objectives != null)
                    await uplink_handler.replace_objectives.Invoke();
                SStgui.update_uis(this);
                break;
        }
        return TRUE;
    }

    /// Proc that locks uplinks
    // tg: code/datums/components/uplink.dm:276
    public virtual void lock_uplink()
    {
        active = FALSE;
        locked = TRUE;
        SStgui.close_uis(this);
    }

    // Implant signal responses
    // tg: code/datums/components/uplink.dm:282
    [SignalHandler]
    public virtual void implant_activation()
    {
        obj_item_implant implant = parent as obj_item_implant;
        locked = FALSE;
        interact(null, implant.imp_in);
    }

    // tg: code/datums/components/uplink.dm:289
    [SignalHandler]
    public virtual void implanting(datum source = null, DmList arguments = null)
    {
        mob user = arguments[2] as mob;
        owner = user?.key;
        if (Dm.truthy(owner) && purchase_log == null)
        {
            LAZYINITLIST(ref GLOB.uplink_purchase_logs_by_key);
            if (Dm.truthy(GLOB.uplink_purchase_logs_by_key[owner]))
                purchase_log = GLOB.uplink_purchase_logs_by_key[owner] as datum_uplink_purchase_log;
            else
                purchase_log = Dm.@new<datum_uplink_purchase_log>(owner, this);
        }
    }

    // tg: code/datums/components/uplink.dm:301
    [SignalHandler]
    public virtual object old_implant(datum source = null, DmList arguments = null, obj_item_implant new_implant = null)
    {
        // It kinda has to be weird like this until implants are components
        return !Dm.truthy(new_implant._listen_lookup?[COMSIG_IMPLANT_EXISTING_UPLINK]) ? NONE : new_implant._SendSignal(COMSIG_IMPLANT_EXISTING_UPLINK, Dm.list(new_implant, this));
    }

    // tg: code/datums/components/uplink.dm:307
    [SignalHandler]
    public virtual object new_implant(datum source = null, datum_component_uplink uplink = null)
    {
        return COMPONENT_DELETE_NEW_IMPLANT;
    }

    // PDA signal responses

    // tg: code/datums/components/uplink.dm:314
    [SignalHandler]
    public virtual object new_ringtone(datum source = null, mob_living user = null, object new_ring_text = null)
    {
        if (!Dm.eq(trim(LOWER_TEXT(new_ring_text)), trim(LOWER_TEXT(unlock_code))))
        {
            if (Dm.eq(trim(LOWER_TEXT(new_ring_text)), trim(LOWER_TEXT(failsafe_code))))
            {
                Waiting.Detach(failsafe(user));
                return COMPONENT_STOP_RINGTONE_CHANGE;
            }
            return null;
        }
        locked = FALSE;
        if (ismob(user))
        {
            interact(null, user);
            to_chat(user, span_hear(I18n.T("The computer softly beeps.")));
        }
        return COMPONENT_STOP_RINGTONE_CHANGE;
    }

    // tg: code/datums/components/uplink.dm:327
    [SignalHandler]
    public virtual object check_detonate()
    {
        return COMPONENT_TABLET_NO_DETONATE;
    }

    // Radio signal responses

    // tg: code/datums/components/uplink.dm:334  (registered by no one in tg: the radio's uplink unlocks by new_message)
    [SignalHandler]
    public virtual void new_frequency(datum source = null, DmList arguments = null)
    {
        obj_item_radio master = parent as obj_item_radio;
        object frequency = arguments[1];
        if (!Dm.eq(frequency, unlock_code))
        {
            if (Dm.eq(frequency, failsafe_code))
                Waiting.Detach(failsafe(master.loc));
            return;
        }
        locked = FALSE;
        if (ismob(master.loc))
            interact(null, master.loc as mob);
    }

    // tg: code/datums/components/uplink.dm:341
    [SignalHandler]
    public virtual object new_message(datum source = null, mob_living user = null, object message = null, object channel = null)
    {
        if (!Dm.eq(channel, RADIO_CHANNEL_UPLINK))
            return null;

        if (!Dm.truthy(findtext(LOWER_TEXT(message), LOWER_TEXT(unlock_code))))
        {
            if (Dm.truthy(failsafe_code) && Dm.truthy(findtext(LOWER_TEXT(message), LOWER_TEXT(failsafe_code))))
                Waiting.Detach(failsafe(user)); // no point returning cannot radio, youre probably ded
            return null;
        }
        locked = FALSE;
        interact(null, user);
        to_chat(user, I18n.T("As you whisper the code into your headset, a soft chime fills your ears."));
        return COMPONENT_CANNOT_USE_RADIO;
    }

    // Pen signal responses

    // tg: code/datums/components/uplink.dm:357
    [SignalHandler]
    public virtual void pen_rotation(datum source = null, object degrees = null, mob_living_carbon user = null)
    {
        obj_item_pen master = parent as obj_item_pen;
        Dm.append(previous_attempts, degrees);
        if (Dm.num(length(previous_attempts)) > PEN_ROTATIONS)
            popleft(previous_attempts);

        if (Dm.truthy(compare_list(previous_attempts, unlock_code as DmList)))
        {
            locked = FALSE;
            previous_attempts.Cut();
            master.degrees = 0;
            interact(null, user);
            to_chat(user, span_warning(I18n.T("Your pen makes a clicking noise, before quickly rotating back to 0 degrees!")));
        }

        else if (Dm.truthy(compare_list(previous_attempts, failsafe_code as DmList)))
            Waiting.Detach(failsafe(user));
    }

    // tg: code/datums/components/uplink.dm:375
    public virtual void setup_unlock_code()
    {
        unlock_code = generate_code();
        obj_item P = parent as obj_item;
        if (parent is obj_item_modular_computer)
            unlock_note = Dm.text("<B>Uplink Passcode:</B> [] ([]).", unlock_code, P.name);
        else if (parent is obj_item_radio)
            unlock_note = Dm.text("<B>Radio Passcode:</B> [] ([], [] channel).", unlock_code, P.name, RADIO_TOKEN_UPLINK);
        else if (parent is obj_item_pen)
            unlock_note = Dm.text("<B>Uplink Degrees:</B> [] ([]).", english_list(unlock_code as DmList), P.name);
    }

    // tg: code/datums/components/uplink.dm:385
    public virtual object generate_code()
    {
        object returnable_code = "";

        if (parent is obj_item_modular_computer)
            returnable_code = Dm.text("[] []", rand(100, 999), pick(GLOB.phonetic_alphabet));

        else if (parent is obj_item_radio)
            returnable_code = Dm.text("[] [] []", pick(GLOB.phonetic_alphabet), pick(GLOB.phonetic_alphabet), pick(GLOB.phonetic_alphabet));

        else if (parent is obj_item_pen)
        {
            DmList code = Dm.list();
            for (float i = 1, i__end = PEN_ROTATIONS; i <= i__end; i++)
                Dm.append(code, rand(1, 360));
            returnable_code = code;
        }

        if (!Dm.truthy(unlock_code)) // assume the unlock_code is our "base" code that we don't want to duplicate, and if we don't have an unlock code, immediately return out of it since there's nothing to compare to.
            return returnable_code;

        // duplicate checking, re-run the proc if we get a dupe to prevent the failsafe explodey code being the same as the unlock code.
        if (islist(returnable_code))
        {
            if (Dm.eq(english_list(returnable_code as DmList), english_list(unlock_code as DmList))) // we pass english_list to the user anyways and for later processing, so we can just compare the english_list of the two lists.
                return generate_code();
        }

        else if (Dm.eq(unlock_code, returnable_code))
            return generate_code();

        return returnable_code;
    }

    // tg: code/datums/components/uplink.dm:412
    public virtual async DmTask failsafe(atom source = null)
    {
        if (parent == null)
            return;
        turf T = get_turf(parent);
        if (T == null)
            return;
        string user_deets = "an uplink failsafe explosion has been triggered";
        if (ismob(source))
        {
            user_deets = Dm.text("[] has triggered an uplink failsafe explosion", ADMIN_LOOKUPFLW(source));
            source.log_message(Dm.text("triggered an uplink failsafe explosion. Uplink owner: [].", key_name(owner)), LOG_ATTACK);
        }
        else if (istype(source, typeof(obj_item_circuit_component)))
        {
            obj_item_circuit_component circuit = source as obj_item_circuit_component;
            user_deets = Dm.text("[] has triggered an uplink failsafe explosion", circuit.parent.get_creator_admin()); // gaps CI1
        }
        else
            source?.log_message(Dm.text("somehow triggered an uplink failsafe explosion. Uplink owner: [].", key_name(owner)), LOG_ATTACK);
        message_admins(Dm.text("[] at [] The owner of the uplink was [].", user_deets, AREACOORD(T), ADMIN_LOOKUPFLW(owner)));

        await explosion(parent as atom, devastation_range: 1, heavy_impact_range: 2, light_impact_range: 3);
        qdel(parent); //Alternatively could brick the uplink.
    }
}
