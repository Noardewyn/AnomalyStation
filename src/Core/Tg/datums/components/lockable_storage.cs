// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/lockable_storage.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G2 (D274): the whole file (the secure safes' keypad, tg's LockedSafe window). The var block is generated (D111).

/**
 * ##lockable_storage
 * Adds a UI to the object that triggers when you use it in hand (if item) or attack (everything else).
 * The UI is a lock that, when unlocked, allows you to access the contents inside of it.
 * When using this, make sure you have icons for `on_update_icon_state`.
 */
// tg: code/datums/components/lockable_storage.dm:6
public partial class datum_component_lockable_storage : datum_component
{
    // tg: code/datums/components/lockable_storage.dm:19
    [DmParams("lock_code", "can_hack_open")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object lock_code = Dm.arg(__extra, 0);
        object can_hack_open = Dm.arg(__extra, 1) ?? TRUE;
        if (!isobj(parent))
            return COMPONENT_INCOMPATIBLE;

        var atom_parent = (atom)parent;
        Dm.combine(ref atom_parent.flags_1, HAS_CONTEXTUAL_SCREENTIPS_1);

        if (atom_parent.atom_storage == null)
        {
            await atom_parent.create_storage(
                max_specific_storage: WEIGHT_CLASS_GIGANTIC,
                max_total_storage: 14,
                canthold: Dm.list(typeof(obj_item_storage_briefcase_secure)));
        }

        this.can_hack_open = Dm.truthy(can_hack_open) ? TRUE : FALSE;
        set_lock_code(lock_code);
        return null;
    }

    // tg: code/datums/components/lockable_storage.dm:37
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        if (Dm.truthy(can_hack_open))
        {
            RegisterSignal(parent, COMSIG_ATOM_TOOL_ACT(TOOL_SCREWDRIVER), nameof(on_screwdriver_act));
            RegisterSignal(parent, COMSIG_ATOM_TOOL_ACT(TOOL_MULTITOOL), nameof(on_multitool_act));
        }

        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, nameof(on_requesting_context_from_item));
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_ICON_STATE, nameof(on_update_icon_state));
        RegisterSignal(parent, COMSIG_ATOM_EMAG_ACT, nameof(on_emag));

        if (isitem(parent))
            RegisterSignal(parent, COMSIG_ITEM_ATTACK_SELF, nameof(on_interact));
        else
            RegisterSignal(parent, COMSIG_ATOM_ATTACK_HAND, nameof(on_interact));
    }

    // tg: code/datums/components/lockable_storage.dm:52
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        if (Dm.truthy(can_hack_open))
            UnregisterSignal(parent, Dm.list(COMSIG_ATOM_TOOL_ACT(TOOL_SCREWDRIVER), COMSIG_ATOM_TOOL_ACT(TOOL_MULTITOOL)));
        UnregisterSignal(parent, Dm.list(COMSIG_ATOM_EXAMINE, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, COMSIG_ATOM_UPDATE_ICON_STATE, COMSIG_ATOM_EMAG_ACT));

        if (isitem(parent))
            UnregisterSignal(parent, COMSIG_ITEM_ATTACK_SELF);
        else
            UnregisterSignal(parent, COMSIG_ATOM_ATTACK_HAND);
        await base.UnregisterFromParent(__extra);
    }

    /**
     * Adds context screentips to the locked item.
     */
    // tg: code/datums/components/lockable_storage.dm:80
    [SignalHandler]
    public virtual object on_requesting_context_from_item(datum source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item == null)
        {
            if (Dm.@in(source, user.held_items))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Open storage";
                return CONTEXTUAL_SCREENTIP_SET;
            }
            context[SCREENTIP_CONTEXT_RMB] = "Open storage";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (Dm.truthy(can_hack_open))
        {
            if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
            {
                context[SCREENTIP_CONTEXT_LMB] = Dm.text("[] panel", Dm.truthy(panel_open) ? "Close" : "Open");
                return CONTEXTUAL_SCREENTIP_SET;
            }
            if (Dm.eq(held_item.tool_behaviour, TOOL_MULTITOOL))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Hack panel open";
                return CONTEXTUAL_SCREENTIP_SET;
            }
        }

        return NONE;
    }

    ///Called when examining the storage item.
    // tg: code/datums/components/lockable_storage.dm:100
    [SignalHandler]
    public virtual void on_examine(atom source = null, mob user = null, DmList examine_list = null)
    {
        if (Dm.truthy(can_hack_open))
            Dm.append(ref examine_list, Dm.text("The service panel is currently <b>[]</b>.", Dm.truthy(panel_open) ? "unscrewed" : "screwed shut"));
    }

    /**
     * Called when a screwdriver is used on the parent, if it's hackable.
     */
    // tg: code/datums/components/lockable_storage.dm:108
    [SignalHandler]
    public virtual object on_screwdriver_act(atom source = null, mob user = null, obj_item tool = null)
    {
        if (!Dm.truthy(can_hack_open) || !Dm.truthy(source.atom_storage.locked))
            return NONE;

        panel_open = Dm.truthy(panel_open) ? FALSE : TRUE;
        tool.play_tool_sound(source);
        source.balloon_alert(user, Dm.text("panel []", Dm.truthy(panel_open) ? "opened" : "closed"));
        return ITEM_INTERACT_SUCCESS;
    }

    /**
     * Called when a multitool is used on the parent, if it's hackable.
     * Checks if we can start hacking and, if so, will begin the hacking process.
     */
    // tg: code/datums/components/lockable_storage.dm:122
    [SignalHandler]
    public virtual object on_multitool_act(atom source = null, mob user = null, obj_item tool = null)
    {
        if (!Dm.truthy(can_hack_open) || !Dm.truthy(source.atom_storage.locked))
            return NONE;
        if (!Dm.truthy(panel_open))
        {
            source.balloon_alert(user, "panel closed!");
            return ITEM_INTERACT_BLOCKING;
        }
        source.balloon_alert(user, "hacking...");
        Waiting.Detach(hack_open(source, user, tool)); // INVOKE_ASYNC
        return ITEM_INTERACT_SUCCESS;
    }

    ///Does a do_after to hack the storage open, takes a long time cause idk.
    // tg: code/datums/components/lockable_storage.dm:135
    public virtual async DmTask hack_open(atom source = null, mob user = null, obj_item tool = null)
    {
        if (!Dm.truthy(await tool.use_tool((atom)parent, user as mob_living, 40 * SECONDS, volume: 50)))
            return;
        source.balloon_alert(user, "hacked");
        set_lock_code(null);
    }

    // tg: code/datums/components/lockable_storage.dm:141
    public virtual void break_lock()
    {
        can_hack_open = FALSE; // since it's broken for good
        lock_code = null;

        var source = (obj)parent;
        source.obj_flags = Dm.bitor(source.obj_flags, EMAGGED);
        source.atom_storage.locked = STORAGE_NOT_LOCKED;
        // SEND_SIGNAL(source, COMSIG_LOCKABLE_STORAGE_SET_CODE, lock_code) (rules 11.1 (e), a statement)
        if (Dm.truthy(source._listen_lookup?[COMSIG_LOCKABLE_STORAGE_SET_CODE]))
            source._SendSignal(COMSIG_LOCKABLE_STORAGE_SET_CODE, Dm.list(source, lock_code));
        source.update_appearance();
    }

    // tg: code/datums/components/lockable_storage.dm:151
    [SignalHandler]
    public virtual object on_emag(obj source = null, mob user = null, obj_item_card_emag emag_card = null)
    {
        if (!Dm.truthy(source.atom_storage.locked))
            return FALSE;

        if (Dm.truthy(Dm.bitand(source.obj_flags, EMAGGED)))
            return FALSE;

        source.visible_message(span_warning(Dm.text("Sparks fly from []!", source)), blind_message: span_hear("You hear a faint electrical spark."));
        source.balloon_alert(user, "lock destroyed");
        playsound(source, SFX_SPARKS, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        break_lock();
        return ITEM_INTERACT_SUCCESS;
    }

    /**
     * Sets the lock code for this storage and updates the locked state accordingly
     * Arguments:
     * * new_code - The new lock code to set, can be null to remove the code
     */
    // tg: code/datums/components/lockable_storage.dm:172
    public virtual object set_lock_code(object new_code = null, mob_living user = null)
    {
        var source = (obj)parent;

        // Can't set lock code if the electronics are emagged
        if (Dm.truthy(Dm.bitand(source.obj_flags, EMAGGED)))
            return FALSE;

        lock_code = new_code;
        // SEND_SIGNAL(source, COMSIG_LOCKABLE_STORAGE_SET_CODE, new_code) (rules 11.1 (e), a statement)
        if (Dm.truthy(source._listen_lookup?[COMSIG_LOCKABLE_STORAGE_SET_CODE]))
            source._SendSignal(COMSIG_LOCKABLE_STORAGE_SET_CODE, Dm.list(source, new_code));
        float lock_state = Dm.truthy(lock_code) ? STORAGE_FULLY_LOCKED : STORAGE_NOT_LOCKED;
        source.atom_storage.set_locked(lock_state);
        source.update_appearance();

        if (user != null && Dm.truthy(new_code))
            to_chat(user, span_notice(Dm.text("You set the [] pincode to [].", source, lock_code)));

        return TRUE;
    }

    ///Updates the icon state depending on if we're locked or not.
    // tg: code/datums/components/lockable_storage.dm:191
    [SignalHandler]
    public virtual void on_update_icon_state(obj source = null)
    {
        if (Dm.truthy(Dm.bitand(source.obj_flags, EMAGGED)))
            source.icon_state = Dm.text("[]_broken", source.base_icon_state);
        else
            source.icon_state = Dm.text("[][]", source.base_icon_state, Dm.truthy(source.atom_storage.locked) ? "_locked" : null);
    }

    ///Called when interacted with in-hand or on attack, opens the UI.
    // tg: code/datums/components/lockable_storage.dm:200
    [SignalHandler]
    public virtual void on_interact(atom source = null, mob user = null)
    {
        Waiting.Detach(ui_interact(user)); // INVOKE_ASYNC
    }

    // tg: code/datums/components/lockable_storage.dm:205
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        var source = (obj)parent;
        if (Dm.truthy(Dm.bitand(source.obj_flags, EMAGGED)))
        {
            if (ui != null)
                ui.close();
            return null;
        }

        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "LockedSafe", parent);
            await ui.open();
        }
        return null;
    }

    // tg: code/datums/components/lockable_storage.dm:215
    public override async DmTask<object> ui_data(mob user = null)
    {
        var source = (atom)parent;
        DmList data = Dm.list();
        data["input_code"] = Dm.truthy(numeric_input) ? numeric_input : "*****";
        data["locked"] = source.atom_storage.locked;
        data["lock_code"] = Dm.truthy(lock_code) ? TRUE : FALSE; //we just need to know if it has one.
        return data;
    }

    // tg: code/datums/components/lockable_storage.dm:223
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        await base.ui_act(action, @params, ui, state);
        if (!Dm.eq(action, "keypad"))
            return TRUE;

        var source = (atom)parent;
        playsound(source, SFX_TERMINAL_TYPE, 50, FALSE);

        string digit = Dm.str(@params["digit"]);
        switch (digit)
        {
            //locking it back up
            case "C":
                numeric_input = "";
                //you can't lock it if it's already locked or lacks a lock code.
                if (Dm.truthy(source.atom_storage.locked) || lock_code == null)
                    return TRUE;
                source.atom_storage.set_locked(STORAGE_FULLY_LOCKED);
                return TRUE;
            //setting a password & unlocking
            case "E":
                //inputting a new code if there isn't one set.
                if (!Dm.truthy(lock_code))
                {
                    if (!Dm.eq(length(numeric_input), 5f))
                        return TRUE;
                    set_lock_code(numeric_input, usr as mob_living);
                    numeric_input = "";
                    return TRUE;
                }
                //unlocking the current code.
                if (!Dm.eq(numeric_input, lock_code))
                    return TRUE;
                source.atom_storage.set_locked(STORAGE_NOT_LOCKED);
                numeric_input = "";
                return TRUE;
            //putting digits in.
            case "0": case "1": case "2": case "3": case "4": case "5": case "6": case "7": case "8": case "9":
                if (Dm.eq(length(numeric_input), 5f))
                    return null;
                numeric_input = Dm.text("[][]", numeric_input, digit);
                return TRUE;
        }
        return null;
    }
}
