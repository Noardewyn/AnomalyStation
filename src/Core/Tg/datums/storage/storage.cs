// Translated from tgstation (snapshot 1d99bd7f): code/datums/storage/storage.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P3: the storage datum (docs/ai/specs/m4a-ownership.md, P3's list for storage.dm). The procs of later milestones stay
// Dm.NotTranslated roots with their milestone (M5 slice 1 translated on_deconstruct): the bags' collection (handle_mass_pickup,
// collect_on_turf, collect_on_turf_loop: M9; the gather-mode action's action_trigger, action_deleted and
// toggle_collection_mode came with the action buttons, M4c slice 3),
// drag and drop (mousedrop_receive, on_mousedrop_onto, dump_content_at, on_mousedropped_onto) is
// translated with BYOND's MouseDrop (protocol version 17).
// update_actions is translated beside the list (it answers COMSIG_ITEM_EQUIPPED, which every equip of a storage item
// sends; reported to the lead). The sounds are translated (M4b audio slice 6); the viewers' messages (oviewers, M9) are deferred leaves.
// The interaction guard of D132 (DV27) is at the top of attempt_insert and open_storage: a storage datum whose own type
// is inert (a subtype whose tg procs are not translated) refuses with tg's "not ported yet" (interaction.md 7).

// tg: code/datums/storage/storage.dm:8
[DmPath("/datum/storage")]
public partial class datum_storage : datum
{
}

// tg: code/datums/storage/storage.dm:135
public partial class datum_storage : datum
{
    // tg: code/datums/storage/storage.dm:135
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        atom parent = Dm.arg<atom>(__extra, 0);
        object max_slots = Dm.arg(__extra, 1);
        object max_specific_storage = Dm.arg(__extra, 2);
        object max_total_storage = Dm.arg(__extra, 3);
        object rustle_sound = Dm.arg(__extra, 4);
        object remove_rustle_sound = Dm.arg(__extra, 5);
        max_slots ??= this.max_slots;
        max_specific_storage ??= this.max_specific_storage;
        max_total_storage ??= this.max_total_storage;
        rustle_sound ??= this.rustle_sound;
        remove_rustle_sound ??= this.remove_rustle_sound;

        if (!(parent is atom))
        {
            stack_trace(Dm.text("Storage datum ([]) created without a []!", this.type, (parent == null ? "null parent" : Dm.text("invalid parent ([])", Dm.arg(__extra, 0) is datum d ? d.type : null))), "code/datums/storage/storage.dm", 145);
            qdel(this);
            return;
        }

        set_parent(parent);
        await set_real_location(parent);

        this.max_slots = Dm.num(max_slots);
        this.max_specific_storage = Dm.num(max_specific_storage);
        this.max_total_storage = Dm.num(max_total_storage);
        this.rustle_sound = rustle_sound;
        this.remove_rustle_sound = remove_rustle_sound;
    }

    // tg: code/datums/storage/storage.dm:160
    public override object Destroy(float? force = null)
    {
        force ??= FALSE;
        foreach (mob person in Dm.each_cast<mob>(is_using))
            hide_contents(person);

        LAZYCLEARLIST(is_using);
        QDEL_LIST_ASSOC_VAL(storage_interfaces);

        parent = null;
        real_location = null;

        return base.Destroy(force);
    }

    // tg: code/datums/storage/storage.dm:171
    public virtual void on_deconstruct()
    {
        // SIGNAL_HANDLER (M5 slice 1: a storage object destroyed drops what it holds)
        Waiting.NoSleep(remove_all(update_storage: FALSE));
    }

    /// Ran on items instantiated inside the storage, basically a chopped down version of handle_enter
    // tg: code/datums/storage/storage.dm:177
    public virtual void item_init(datum source = null, obj_item inited = null)
    {
        if (!(inited is obj_item))
            return;

        Dm.combine(ref inited.item_flags, IN_STORAGE);
        RegisterSignal(inited, COMSIG_MOUSEDROPPED_ONTO, nameof(mousedrop_receive));
    }

    /// Automatically ran on all object insertions: flag marking and view refreshing.
    // tg: code/datums/storage/storage.dm:187
    public virtual void handle_enter(datum source = null, obj_item arrived = null)
    {
        if (!(arrived is obj_item))
            return;

        Dm.combine(ref arrived.item_flags, IN_STORAGE);
        arrived.on_enter_storage(this);
        RegisterSignal(arrived, COMSIG_MOUSEDROPPED_ONTO, nameof(mousedrop_receive));
        if (Dm.truthy(arrived._listen_lookup?[COMSIG_ITEM_STORED]))
            arrived._SendSignal(COMSIG_ITEM_STORED, Dm.list(arrived, this));
        if (!Dm.truthy(block_insert_remove_updates))
        {
            Waiting.NoSleep(refresh_views());
            parent.update_appearance();
        }
    }

    /// Automatically ran on all object removals: flag marking and view refreshing.
    // tg: code/datums/storage/storage.dm:202
    public virtual void handle_exit(datum source = null, obj_item gone = null)
    {
        if (!(gone is obj_item))
            return;

        Dm.mask(ref gone.item_flags, Dm.bitnot(IN_STORAGE));
        gone.on_exit_storage(this);
        UnregisterSignal(gone, COMSIG_MOUSEDROPPED_ONTO);
        if (Dm.truthy(gone._listen_lookup?[COMSIG_ITEM_UNSTORED]))
            gone._SendSignal(COMSIG_ITEM_UNSTORED, Dm.list(gone, this));

        // resets relevant variables as it goes
        foreach (mob user in Dm.each_cast<mob>(is_using))
        {
            if (Dm.truthy(user.hud_used?.screen_groups[HUD_GROUP_STORAGE]))
                Dm.remove_at(user.hud_used.screen_groups, (float)HUD_GROUP_STORAGE, gone);

            if (user.client != null)
                Dm.remove(user.client.screen, gone);
        }

        gone.layer = Dm.initial<obj_item, float>(gone, g => g.layer);
        SET_PLANE_IMPLICIT(gone, Dm.initial<obj_item, float>(gone, g => g.plane));
        gone.mouse_opacity = Dm.initial<obj_item, float>(gone, g => g.mouse_opacity);
        gone.screen_loc = null;
        if (Dm.truthy(numerical_stacking))
            gone.maptext = "";

        if (!Dm.truthy(block_insert_remove_updates))
        {
            Waiting.NoSleep(refresh_views());
            parent.update_appearance();
        }
    }

    /// Set the passed atom as the parent
    // tg: code/datums/storage/storage.dm:232
    public virtual void set_parent(atom new_parent = null)
    {
        if (!(parent == null)) throw Dm.CRASH(assertion_message("code/datums/storage/storage.dm", 235, "isnull(parent)"), "code/datums/storage/storage.dm", 235);

        parent = new_parent;
        ADD_TRAIT(parent, TRAIT_COMBAT_MODE_SKIP_INTERACTION, REF(this));
        // a few of theses should probably be on the real_location rather than the parent
        RegisterSignals(parent, Dm.list(COMSIG_ATOM_ATTACK_PAW, COMSIG_ATOM_ATTACK_HAND), nameof(on_attack));
        RegisterSignal(parent, COMSIG_MOUSEDROP_ONTO, nameof(on_mousedrop_onto));
        RegisterSignal(parent, COMSIG_MOUSEDROPPED_ONTO, nameof(on_mousedropped_onto));
        RegisterSignal(parent, COMSIG_ITEM_PRE_ATTACK, nameof(on_preattack));
        RegisterSignal(parent, COMSIG_ITEM_ATTACK_SELF, nameof(mass_empty));
        RegisterSignals(parent, Dm.list(COMSIG_ATOM_ATTACK_GHOST, COMSIG_ATOM_ATTACK_HAND_SECONDARY), nameof(open_storage_on_signal));
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(close_distance));
        RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(update_actions));
        RegisterSignal(parent, COMSIG_TOPIC, nameof(topic_handle));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(handle_examination));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE_MORE, nameof(handle_extra_examination));
        RegisterSignal(parent, COMSIG_OBJ_DECONSTRUCT, nameof(on_deconstruct));
        RegisterSignal(parent, COMSIG_ATOM_EMP_ACT, nameof(on_emp_act));
        RegisterSignal(parent, COMSIG_ATOM_CONTENTS_WEIGHT_CLASS_CHANGED, nameof(contents_changed_w_class));
        RegisterSignal(parent, COMSIG_CLICK_ALT, nameof(on_click_alt));
        RegisterSignal(parent, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(item_init));
    }

    /**
     * Sets where items are physically being stored in the case it shouldn't be on the parent.
     *
     * Does not handle moving any existing items, that must be done manually.
     *
     * Arguments
     * * atom/new_real_location - the new real location of the datum
     * * should_drop - if TRUE, all the items in the old real location will be dropped.
     */
    // tg: code/datums/storage/storage.dm:266
    public virtual async DmTask set_real_location(atom new_real_location = null, float? should_drop = null)
    {
        should_drop ??= FALSE;
        if (!(real_location == null))
        {
            UnregisterSignal(real_location, Dm.list(
                COMSIG_ATOM_ENTERED,
                COMSIG_ATOM_EXITED,
                COMSIG_QDELETING,
                COMSIG_ATOM_EMP_ACT
            ));
            Dm.mask(ref real_location.flags_1, Dm.bitnot(HAS_DISASSOCIATED_STORAGE_1));
            if (Dm.truthy(should_drop))
                await remove_all();
        }

        if (new_real_location == null)
            return;

        real_location = new_real_location;
        if (real_location != parent)
            Dm.combine(ref real_location.flags_1, HAS_DISASSOCIATED_STORAGE_1);
        RegisterSignal(real_location, COMSIG_ATOM_ENTERED, nameof(handle_enter));
        RegisterSignal(real_location, COMSIG_ATOM_EXITED, nameof(handle_exit));
        RegisterSignal(real_location, COMSIG_QDELETING, nameof(real_location_deleted));
    }

    /// Signal handler for when the real location is deleted.
    // tg: code/datums/storage/storage.dm:289
    public virtual void real_location_deleted(datum deleting_real_location = null)
    {
        Waiting.NoSleep(set_real_location(null));
    }

    // tg: code/datums/storage/storage.dm:294
    public virtual void topic_handle(datum source = null, object user = null, object href_list = null)
    {
        if (can_hold_description == null)
            return;

        if (Dm.truthy(((DmList)href_list)["show_valid_pocket_items"]))
            to_chat(user, span_notice(I18n.T("[] can hold: []", source, can_hold_description)));
    }

    // tg: code/datums/storage/storage.dm:303
    public virtual void handle_examination(datum source = null, mob user = null, DmList examine_list = null)
    {
        if (can_hold_description == null)
            return;

        Dm.append(examine_list, span_notice(I18n.T("You can examine this further to check what kind of extra items it can hold.")));
    }

    // tg: code/datums/storage/storage.dm:311
    public virtual void handle_extra_examination(datum source = null, mob user = null, DmList examine_list = null)
    {
        if (can_hold_description == null)
            return;

        Dm.append(examine_list, span_notice(I18n.T("[] can hold: []", source, can_hold_description)));
    }

    /**
     * Sets what type of contents this storage supports
     * Arguments
     *
     * * list/can_hold_list - The list of item types whitelisted in this storage rejecting everything else
     * * list/cant_hold_list - The list of item types blacklisted in this storage accepting everything else
     * * list/exception_hold_list - The list of items that can exceed `max_specific_storage`. It can only fit `exception_count` of such items
     */
    // tg: code/datums/storage/storage.dm:336
    public virtual void set_holdable(object can_hold_list = null, object cant_hold_list = null, object exception_hold_list = null)
    {
        can_hold = null;
        if (!(can_hold_list == null))
        {
            if (!islist(can_hold_list))
                can_hold_list = Dm.list(can_hold_list);

            string unique_key = ((DmList)can_hold_list).Join("-");
            if (!Dm.truthy(GLOB.cached_storage_typecaches[unique_key]))
                GLOB.cached_storage_typecaches[unique_key] = typecacheof(can_hold_list);
            can_hold = GLOB.cached_storage_typecaches[unique_key] as DmList;
        }

        cant_hold = null;
        if (!(cant_hold_list == null))
        {
            if (!islist(cant_hold_list))
                cant_hold_list = Dm.list(cant_hold_list);

            string unique_key = ((DmList)cant_hold_list).Join("-");
            if (!Dm.truthy(GLOB.cached_storage_typecaches[unique_key]))
                GLOB.cached_storage_typecaches[unique_key] = typecacheof(cant_hold_list);
            cant_hold = GLOB.cached_storage_typecaches[unique_key] as DmList;
        }

        exception_hold = null;
        if (!(exception_hold_list == null))
        {
            if (!islist(exception_hold_list))
                exception_hold_list = Dm.list(exception_hold_list);

            string unique_key = ((DmList)exception_hold_list).Join("-");
            if (!Dm.truthy(GLOB.cached_storage_typecaches[unique_key]))
                GLOB.cached_storage_typecaches[unique_key] = typecacheof(exception_hold_list);
            exception_hold = GLOB.cached_storage_typecaches[unique_key] as DmList;
        }

        can_hold_description = null;
        if (Dm.truthy(Dm.length(can_hold_list)))
        {
            DmList desc = Dm.list();
            foreach (object valid_item in Dm.each(can_hold_list))
                Dm.append(desc, I18n.T("\\a []", Dm.initial(valid_item, "name")));
            can_hold_description = Dm.text("\n\t[]", span_notice(desc.Join("\n\t")));
        }
    }

    /// Updates the action button for toggling collectmode.
    // tg: code/datums/storage/storage.dm:375
    public virtual void update_actions(atom source = null, mob equipper = null, object slot = null)
    {
        // M4a P3: translated beside the list (every equip of a storage item sends COMSIG_ITEM_EQUIPPED); the action's
        // own procs (action_trigger, action_deleted, toggle_collection_mode) came with M4c slice 3.
        if (!Dm.truthy(allow_quick_gather))
        {
            QDEL_NULL(ref modeswitch_action);
            return;
        }
        if (!(modeswitch_action == null))
            return;
        if (!isitem(parent))
            return;

        obj_item item_parent = parent as obj_item;
        modeswitch_action = Waiting.NoSleep(item_parent.add_item_action(typeof(datum_action_item_action_storage_gather_mode))) as datum_action_item_action_storage_gather_mode;
        RegisterSignal(modeswitch_action, COMSIG_ACTION_TRIGGER, nameof(action_trigger));
        RegisterSignal(modeswitch_action, COMSIG_QDELETING, nameof(action_deleted));
    }

    /**
     * Checks if an item is capable of being inserted into the storage.
     *
     * Arguments
     * * obj/item/to_insert - the item we're checking
     * * messages - if TRUE, will print out a message if the item is not valid
     * * force - bypass locked storage up to a certain level. See [code/__DEFINES/storage.dm]
     */
    // tg: code/datums/storage/storage.dm:399
    public virtual object can_insert(obj_item to_insert = null, mob user = null, float? messages = null, float? force = null)
    {
        messages ??= TRUE;
        force ??= STORAGE_NOT_LOCKED;
        if (QDELETED(to_insert) || !(to_insert is obj_item))
            return FALSE;

        //stops you from putting stuff like off-hand thingy inside. Hologram storages can accept only hologram items
        if (Dm.truthy(Dm.bitand(to_insert.item_flags, ABSTRACT)))
            return FALSE;
        if (Dm.truthy(Dm.bitand(parent.flags_1, HOLOGRAM_1)))
        {
            if (!Dm.truthy(Dm.bitand(to_insert.flags_1, HOLOGRAM_1)))
                return FALSE;
        }
        else if (Dm.truthy(Dm.bitand(to_insert.flags_1, HOLOGRAM_1)))
            return FALSE;

        if (locked > Dm.num(force))
        {
            if (Dm.truthy(messages) && Dm.truthy(user))
                user.balloon_alert(user, I18n.T("closed!"));
            return FALSE;
        }

        if ((to_insert == parent) || (to_insert == real_location))
            return FALSE;

        if (to_insert.w_class > max_specific_storage)
        {
            if (!Dm.truthy(is_type_in_typecache(to_insert, exception_hold)))
            {
                if (Dm.truthy(messages) && Dm.truthy(user))
                    user.balloon_alert(user, I18n.T("too big!"));
                return FALSE;
            }
            if (exception_max <= Dm.num(get_exception_count()))
            {
                if (Dm.truthy(messages) && Dm.truthy(user))
                    user.balloon_alert(user, I18n.T("no room!"));
                return FALSE;
            }
        }

        if (real_location.contents.len >= max_slots)
        {
            if (Dm.truthy(messages) && Dm.truthy(user) && !Dm.truthy(silent_for_user))
                user.balloon_alert(user, I18n.T("no room!"));
            return FALSE;
        }

        if (to_insert.w_class + Dm.num(get_total_weight()) > max_total_storage)
        {
            if (Dm.truthy(messages) && Dm.truthy(user) && !Dm.truthy(silent_for_user))
                user.balloon_alert(user, I18n.T("no room!"));
            return FALSE;
        }

        if (Dm.truthy(to_insert.anchored))
        {
            if (Dm.truthy(messages) && Dm.truthy(user) && !Dm.truthy(silent_for_user))
                user.balloon_alert(user, I18n.T("anchored!"));
            return FALSE;
        }

        bool can_hold_it = can_hold == null || Dm.truthy(is_type_in_typecache(to_insert, can_hold)) || Dm.truthy(is_type_in_typecache(to_insert, exception_hold));
        object cant_hold_it = is_type_in_typecache(to_insert, cant_hold);
        bool trait_says_no = HAS_TRAIT(to_insert, TRAIT_NO_STORAGE_INSERT);
        if (!can_hold_it || Dm.truthy(cant_hold_it) || trait_says_no)
        {
            if (Dm.truthy(messages) && Dm.truthy(user))
                user.balloon_alert(user, I18n.T("can't hold!"));
            return FALSE;
        }

        if (HAS_TRAIT(to_insert, TRAIT_NODROP))
        {
            if (Dm.truthy(messages) && Dm.truthy(user))
                user.balloon_alert(user, I18n.T("stuck on your hand!"));
            return FALSE;
        }

        // this is valid if the container our location is being held in is a storage item
        datum_storage bigger_fish = parent.loc.atom_storage;
        if (Dm.truthy(bigger_fish) && bigger_fish.max_specific_storage < max_specific_storage)
        {
            if (Dm.truthy(messages) && Dm.truthy(user))
                user.balloon_alert(user, I18n.T("[] is in the way!", LOWER_TEXT(parent.loc.name)));
            return FALSE;
        }

        if (isitem(parent))
        {
            obj_item item_parent = parent as obj_item;
            datum_storage smaller_fish = to_insert.atom_storage;
            if (Dm.truthy(smaller_fish) && !Dm.truthy(allow_big_nesting) && to_insert.w_class >= item_parent.w_class)
            {
                if (Dm.truthy(messages) && Dm.truthy(user))
                    user.balloon_alert(user, I18n.T("too big!"));
                return FALSE;
            }
        }

        return TRUE;
    }

    /// Returns a count of how many items held due to exception_hold we have
    // tg: code/datums/storage/storage.dm:476
    public virtual object get_exception_count()
    {
        float count = 0;
        foreach (obj_item thing in Dm.each<obj_item>(real_location))
        {
            if (thing.w_class > max_specific_storage && Dm.truthy(is_type_in_typecache(thing, exception_hold)))
                count += 1;
        }
        return count;
    }

    /// Returns a sum of all of our content's weight classes
    // tg: code/datums/storage/storage.dm:484
    public virtual object get_total_weight()
    {
        float total_weight = 0;
        foreach (obj_item thing in Dm.each<obj_item>(real_location))
            total_weight += thing.w_class;
        return total_weight;
    }

    /**
     * Attempts to insert an item into the storage
     *
     * Arguments
     * * obj/item/to_insert - the item we're inserting
     * * mob/user - (optional) the user who is inserting the item.
     * * override - skip feedback, only do the animation
     * * force - bypass locked storage up to a certain level. See [code/__DEFINES/storage.dm]
     * * messages - if TRUE, we will create balloon alerts for the user.
     */
    // tg: code/datums/storage/storage.dm:500
    public virtual object attempt_insert(obj_item to_insert = null, mob user = null, float? @override = null, float? force = null, float? messages = null)
    {
        @override ??= FALSE;
        force ??= STORAGE_NOT_LOCKED;
        messages ??= TRUE;
        // DEVIATION(DV27): the interaction guard (D132) before tg's first line; FALSE is "not inserted".
        if (StorageGuard("attempt_insert", user))
            return FALSE;

        if (!Dm.truthy(can_insert(to_insert, user, messages: messages, force: force)))
            return FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(parent._listen_lookup?[COMSIG_ATOM_PRE_STORED_ITEM]) ? (object)NONE : parent._SendSignal(COMSIG_ATOM_PRE_STORED_ITEM, Dm.list(parent, to_insert, user, force, messages))), BLOCK_STORAGE_INSERT)))
            return FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(to_insert._listen_lookup?[COMSIG_ITEM_PRE_STORAGE_INSERTION]) ? (object)NONE : to_insert._SendSignal(COMSIG_ITEM_PRE_STORAGE_INSERTION, Dm.list(to_insert, parent, user, force, messages))), BLOCK_STORAGE_INSERT)))
            return FALSE;

        if (Dm.truthy(parent._listen_lookup?[COMSIG_ATOM_STORED_ITEM]))
            parent._SendSignal(COMSIG_ATOM_STORED_ITEM, Dm.list(parent, to_insert, user, force));
        if (Dm.truthy(this._listen_lookup?[COMSIG_STORAGE_STORED_ITEM]))
            this._SendSignal(COMSIG_STORAGE_STORED_ITEM, Dm.list(this, to_insert, user, force));
        if (ismob(to_insert.loc))
        {
            mob item_carrier = to_insert.loc as mob;
            Waiting.NoSleep(item_carrier.transferItemToLoc(to_insert, real_location, animated: FALSE)); // This allows has_unequipped() to be properly called.
        }
        else
            Waiting.NoSleep(to_insert.forceMove(real_location));
        if (get(real_location, typeof(mob)) != user)
        {
            // GAP(lead): slots.tsv types do_pickup_animation's source as turf where tg passes the user (P2 reported it);
            // the user cannot be passed, so the item's own turf is the source (none inside a container: no animation).
            to_insert.do_pickup_animation(real_location, (object)user as turf);
        }
        if (Dm.truthy(messages))
            item_insertion_feedback(user, to_insert, @override);
        return TRUE;
    }

    /// Since items inside storages ignore transparency for QOL reasons, we're tracking when things are dropped onto them instead of our UI elements
    // tg: code/datums/storage/storage.dm:524
    public virtual void mousedrop_receive(atom dropped_onto = null, atom_movable target = null, mob user = null, object @params = null)
    {
        if (this != user.active_storage)
            return;

        if (!Dm.truthy(Waiting.NoSleep(user.can_perform_action(parent, FORBID_TELEKINESIS_REACH))))
            return;

        if (target.loc != real_location) // what even
            return;

        if (Dm.truthy(numerical_stacking))
            return;

        float drop_index = real_location.contents.Find(dropped_onto);
        Dm.remove(real_location.contents, target);
        // Use an empty list if we're dropping onto the last item
        DmList to_move = Dm.length(real_location.contents) >= drop_index ? real_location.contents.Copy(drop_index) : Dm.list();
        Dm.remove(real_location.contents, to_move);
        Dm.append(real_location.contents, target);
        Dm.append(real_location.contents, to_move);
        Waiting.NoSleep(refresh_views());
    }

    // tg: code/datums/storage/storage.dm:559
    public virtual async DmTask<object> handle_mass_pickup(mob user = null, object pick_up_type = null, atom thing_loc = null, DmList rejections = null, datum_progressbar progress = null, DmList success = null)
    {
        Dm.NotTranslated("code/datums/storage/storage.dm:559", "handle_mass_pickup: the bags' collection (item actions, M10+)");
        return default;
    }

    /**
     * Provides visual feedback in chat for an item insertion
     *
     * Arguments
     * * mob/user - the user who is inserting the item
     * * obj/item/thing - the item we're inserting
     * * override - skip feedback, only do animation check
     */
    // tg: code/datums/storage/storage.dm:591
    public virtual void item_insertion_feedback(mob user = null, obj_item thing = null, float? @override = null)
    {
        @override ??= FALSE;
        if (Dm.truthy(animated))
            animate_parent();

        if (Dm.truthy(@override))
            return;

        if (Dm.truthy(silent))
            return;

        if (Dm.truthy(do_rustle) && Dm.truthy(rustle_sound))
            playsound(parent, rustle_sound, 50, rustle_vary, -5); // tg: code/datums/storage/storage.dm:602

        if (!Dm.truthy(silent_for_user))
            to_chat(user, span_notice(I18n.T("You put [] []to [].", thing, insert_preposition, parent)));

        foreach (mob viewing in Dm.each<mob>(oviewers(Dm.world.view, user)))
        {
            if (in_range(user, viewing) || (thing != null && Dm.num(thing.w_class) >= WEIGHT_CLASS_NORMAL))
                viewing.show_message(span_notice(I18n.T("[] puts [] []to [].", user, thing, insert_preposition, parent)), MSG_VISUAL);
        }
    }

    /**
     * Attempts to remove an item from the storage
     * Ignores removal do_afters. Only use this if you're doing it as part of a dumping action
     *
     * Arguments
     * * obj/item/thing - the object we're removing
     * * atom/remove_to_loc - where we're placing the item
     * * silent - if TRUE, we won't play any exit sounds
     * * visual_updates - if TRUE we update storage views & animate parent appearance
     */
    // tg: code/datums/storage/storage.dm:622
    public virtual object attempt_remove(obj_item thing = null, atom remove_to_loc = null, float? silent = null, float? visual_updates = null)
    {
        silent ??= FALSE;
        visual_updates ??= TRUE;
        if ((thing is obj_item) && ismob(parent.loc))
        {
            mob mob_parent = parent.loc as mob;
            Waiting.NoSleep(thing.dropped(mob_parent, /*silent = */TRUE));
        }

        if (Dm.truthy(remove_to_loc))
        {
            Waiting.NoSleep(thing.forceMove(remove_to_loc));

            if (!Dm.truthy(silent) && Dm.truthy(do_rustle))
            {
                if (Dm.truthy(remove_rustle_sound))
                    playsound(parent, remove_rustle_sound, 50, TRUE, -5); // tg: code/datums/storage/storage.dm:634
                else if (Dm.truthy(rustle_sound))
                    playsound(parent, rustle_sound, 50, TRUE, -5); // tg: code/datums/storage/storage.dm:636
            }
        }
        else
            Waiting.NoSleep(thing.moveToNullspace());

        if (Dm.truthy(visual_updates) && Dm.truthy(animated))
            animate_parent();

        if (Dm.truthy(parent._listen_lookup?[COMSIG_ATOM_REMOVED_ITEM]))
            parent._SendSignal(COMSIG_ATOM_REMOVED_ITEM, Dm.list(parent, thing, remove_to_loc, silent));
        if (Dm.truthy(this._listen_lookup?[COMSIG_STORAGE_REMOVED_ITEM]))
            this._SendSignal(COMSIG_STORAGE_REMOVED_ITEM, Dm.list(this, thing, remove_to_loc, silent));
        return TRUE;
    }

    /**
     * Removes everything inside of our storage
     *
     * Arguments
     * * atom/drop_loc - where we're placing the item
     * * update_storage - should we update the parent to show visual effects
     */
    // tg: code/datums/storage/storage.dm:654
    public virtual async DmTask remove_all(atom drop_loc = null, float? update_storage = null)
    {
        drop_loc ??= parent.drop_location() as atom;
        update_storage ??= TRUE;
        block_insert_remove_updates = TRUE;
        foreach (obj_item thing in Dm.each<obj_item>(real_location))
        {
            if (!Dm.truthy(attempt_remove(thing, drop_loc, silent: TRUE, visual_updates: FALSE)))
                continue;
            thing.pixel_x = thing.base_pixel_x + rand(-8, 8);
            thing.pixel_y = thing.base_pixel_y + rand(-8, 8);
        }

        block_insert_remove_updates = FALSE;
        if (Dm.truthy(update_storage))
        {
            if (Dm.truthy(animated))
                animate_parent();
            await refresh_views();
            parent.update_appearance();
        }
    }

    /**
     * Allows a mob to attempt to remove a single item from the storage
     * Allows for hooks into things like removal delays
     *
     * Arguments
     * * mob/removing - the mob doing the removing
     * * obj/item/thing - the object we're removing
     * * atom/remove_to_loc - where we're placing the item
     * * silent - if TRUE, we won't play any exit sounds
     */
    // tg: code/datums/storage/storage.dm:679
    public virtual async DmTask<object> remove_single(mob removing = null, obj_item thing = null, atom remove_to_loc = null, float? silent = null)
    {
        silent ??= FALSE;
        return attempt_remove(thing, remove_to_loc, silent);
    }

    /**
     * Removes only a specific type of item from our storage
     *
     * Arguments
     * * type - the type of item to remove
     * * amount - how many we should attempt to pick up at one time
     * * check_adjacent - if TRUE, we'll check adjacent locations for the item type
     * * force - if TRUE, we'll bypass the check_adjacent check all together
     * * mob/user - the user who is removing the items
     * * list/inserted - (optional) allows consumers to pass a list to be filled with all removed items.
     */
    // tg: code/datums/storage/storage.dm:693
    public virtual async DmTask<object> remove_type(object type = null, atom destination = null, float? amount = null, float? check_adjacent = null, float? force = null, mob user = null, DmList inserted = null)
    {
        amount ??= INFINITY;
        check_adjacent ??= FALSE;
        force ??= FALSE;
        if (!Dm.truthy(force) && Dm.truthy(check_adjacent))
        {
            if (user == null || !Dm.truthy(await destination.IsReachableBy(user)) || !Dm.truthy(await parent.IsReachableBy(user)))
                return FALSE;
        }

        DmList taking = typecache_filter_list(real_location.contents, typecacheof(type) as DmList) as DmList;
        if (taking.len > Dm.num(amount))
            taking.len = Dm.num(amount);

        if (Dm.truthy(inserted)) //duplicated code for performance, don't bother checking retval/checking for list every item.
        {
            foreach (object i in Dm.each(taking))
            {
                if (Dm.truthy(attempt_remove(i as obj_item, destination)))
                    Dm.combine(ref inserted, i);
            }
        }
        else
        {
            foreach (object i in Dm.each(taking))
                attempt_remove(i as obj_item, destination);
        }

        return TRUE;
    }

    /// Signal handler for remove_all()
    // tg: code/datums/storage/storage.dm:713
    public virtual void mass_empty(datum source = null, mob user = null)
    {
        if (!Dm.truthy(allow_quick_empty))
            return;

        Waiting.NoSleep(remove_all(user.drop_location() as atom));
    }

    /**
     * Recursive proc to get absolutely EVERYTHING inside a storage item, including the contents of inner items.
     *
     * Arguments
     * * recursive - whether or not we're checking inside of inner items
     */
    // tg: code/datums/storage/storage.dm:727
    public virtual object return_inv(float? recursive = null)
    {
        recursive ??= TRUE;
        DmList ret = Dm.list();

        foreach (atom found_thing in Dm.each_cast<atom>(real_location))
        {
            Dm.combine(ref ret, found_thing);
            if (Dm.truthy(recursive) && Dm.truthy(found_thing.atom_storage))
                Dm.combine(ref ret, found_thing.atom_storage.return_inv(recursive: TRUE));
        }

        return ret;
    }

    /// Signal handler for emp_act to emp all contents
    // tg: code/datums/storage/storage.dm:738
    [SignalHandler]
    public virtual void on_emp_act(datum source = null, object severity = null, object protection = null)
    {
        // SIGNAL_HANDLER
        // COMSIG_ATOM_EMP_ACT is also the string of COMSIG_ATOM_PRE_EMP_ACT in tg (signals_atom_x_act.dm:12,14), so this handler
        // runs for both signals of atom/emp_act; on the first one `protection` is not passed (null) and the contents are pulsed.

        if (Dm.truthy(Dm.bitand(protection, EMP_PROTECT_CONTENTS)))
            return;

        foreach (obj_item thing in Dm.each<obj_item>(real_location))
            Waiting.NoSleep(thing.emp_act(severity));
    }

    /// Signal handler for preattack from an object.
    // tg: code/datums/storage/storage.dm:748
    public virtual object on_preattack(datum source = null, obj_item thing = null, mob user = null, DmList modifiers = null)
    {
        if (!(thing is obj_item) || thing == parent.loc || !Dm.truthy(allow_quick_gather) || Dm.truthy(thing.atom_storage))
            return null;

        if (collection_mode == COLLECT_ONE)
        {
            attempt_insert(thing, user);
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }

        if (!isturf(thing.loc))
            return COMPONENT_CANCEL_ATTACK_CHAIN;

        Waiting.DetachSync(() => collect_on_turf(thing, user));
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/datums/storage/storage.dm:770
    public virtual void collect_on_turf(obj_item thing = null, mob user = null)
    {
        Dm.NotTranslated("code/datums/storage/storage.dm:770", "collect_on_turf: the bags' collection (item actions, M10+)");
    }

    // tg: code/datums/storage/storage.dm:787
    public virtual async DmTask collect_on_turf_loop(atom holder = null, mob user = null, datum_progressbar progress = null, DmList rejections = null, object pick_up_type = null, DmList success = null)
    {
        Dm.NotTranslated("code/datums/storage/storage.dm:787", "collect_on_turf_loop: the bags' collection (item actions, M10+)");
    }

    // tg: code/datums/storage/storage.dm:801
    public virtual object on_mousedrop_onto(datum source = null, atom over_object = null, mob user = null)
    {
        if (Dm.truthy(Dm.bitand(!Dm.truthy(parent._listen_lookup?[COMSIG_STORAGE_DUMP_PRE_TRANSFER]) ? (object)NONE : parent._SendSignal(COMSIG_STORAGE_DUMP_PRE_TRANSFER, Dm.list(parent, this, over_object, user)), CANCEL_STORAGE_DUMP)))
            return COMPONENT_CANCEL_MOUSEDROP_ONTO;

        if (ismecha(user.loc) || Dm.truthy(user.incapacitated) || !Dm.truthy(user.canUseStorage()))
            return NONE;

        if (over_object is atom_movable_screen_inventory_hand)
        {
            if (parent.loc != user || !Dm.truthy(Waiting.NoSleep(user.can_perform_action(parent, Dm.bitor(FORBID_TELEKINESIS_REACH, ALLOW_RESTING)))))
                return NONE;

            if (isitem(parent))
            {
                obj_item item_parent = parent as obj_item;
                if (!Dm.truthy(item_parent.can_mob_unequip(user)))
                    return COMPONENT_CANCEL_MOUSEDROP_ONTO;
            }

            atom_movable_screen_inventory_hand hand = over_object as atom_movable_screen_inventory_hand;
            Waiting.NoSleep(user.putItemFromInventoryInHandIfPossible(parent as obj_item, hand.held_index));
            // tg: code/datums/storage/storage.dm:821
            parent.add_fingerprint(user);
            return COMPONENT_CANCEL_MOUSEDROP_ONTO;
        }

        if (over_object == user)
        {
            if (!Dm.truthy(Waiting.NoSleep(user.can_perform_action(parent, Dm.bitor(FORBID_TELEKINESIS_REACH, ALLOW_RESTING)))))
                return NONE;

            // tg: code/datums/storage/storage.dm:828
            parent.add_fingerprint(user);
            Waiting.Detach(open_storage(user));
            return COMPONENT_CANCEL_MOUSEDROP_ONTO;
        }

        if (over_object is atom_movable_screen || ismob(over_object))
            return NONE;

        if (!Dm.truthy(Waiting.NoSleep(user.can_perform_action(over_object, FORBID_TELEKINESIS_REACH))))
            return NONE;

        // tg: code/datums/storage/storage.dm:838
        parent.add_fingerprint(user);

        atom dump_loc = over_object.get_dumping_location() as atom;
        if (dump_loc == null)
            return NONE;

        /// Don't dump *onto* objects in the same storage as ourselves
        if (over_object.loc == parent.loc && parent.loc?.atom_storage != null && over_object.atom_storage == null)
            return NONE;

        Waiting.Detach(dump_content_at(over_object, dump_loc, user));
        return COMPONENT_CANCEL_MOUSEDROP_ONTO;
    }

    /**
     * Dumps all of our contents at a specific location.
     *
     * @param atom/dest_object where to dump to
     * @param mob/user the user who is dumping the contents
     */
    // tg: code/datums/storage/storage.dm:857
    public virtual async DmTask dump_content_at(atom dest_object = null, object dump_loc = null, mob user = null)
    {
        if (Dm.truthy(locked))
        {
            user.balloon_alert(user, I18n.T("closed!"));
            return;
        }
        if (!Dm.truthy(await parent.IsReachableBy(user)) || !Dm.truthy(await dest_object.IsReachableBy(user)))
            return;

        if (Dm.truthy(Dm.bitand(!Dm.truthy(dest_object._listen_lookup?[COMSIG_STORAGE_DUMP_CONTENT]) ? (object)NONE : dest_object._SendSignal(COMSIG_STORAGE_DUMP_CONTENT, Dm.list(dest_object, this, user)), STORAGE_DUMP_HANDLED)))
            return;

        // Storage to storage transfer is instant
        if (dest_object.atom_storage != null)
        {
            to_chat(user, span_notice(I18n.T("You dump the contents of [] into [].", parent, dest_object)));

            if (Dm.truthy(do_rustle) && Dm.truthy(rustle_sound))
                playsound(parent, rustle_sound, 50, TRUE, -5); // tg: code/datums/storage/storage.dm:873

            foreach (obj_item to_dump in Dm.each<obj_item>(real_location))
                dest_object.atom_storage.attempt_insert(to_dump, user);
            parent.update_appearance();
            if (Dm.truthy(this._listen_lookup?[COMSIG_STORAGE_DUMP_POST_TRANSFER]))
                this._SendSignal(COMSIG_STORAGE_DUMP_POST_TRANSFER, Dm.list(this, dest_object, user));
            return;
        }

        // Storage to loc transfer requires a do_after
        to_chat(user, span_notice(I18n.T("You start dumping out the contents of [] onto []...", parent, dest_object)));
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: dest_object)))
            return;

        await remove_all(dump_loc as atom);
    }

    /// Signal handler for whenever something gets mouse-dropped onto us.
    // tg: code/datums/storage/storage.dm:888
    public virtual object on_mousedropped_onto(datum source = null, obj_item dropping = null, mob user = null)
    {
        if (!(dropping is obj_item))
            return null;
        if (dropping != user.get_active_held_item())
            return null;
        if (!Dm.truthy(Waiting.NoSleep(user.can_perform_action(source as atom, FORBID_TELEKINESIS_REACH))))
            return null;
        if (dropping.atom_storage != null) // If it has storage it should be trying to dump, not insert.
            return null;
        if (!iscarbon(user) && !isdrone(user))
            return null;
        attempt_insert(dropping, user);
        return COMPONENT_CANCEL_MOUSEDROPPED_ONTO;
    }

    /// Called directly from the attack chain if [insert_on_attack] is TRUE.
    /// Handles inserting an item into the storage when clicked.
    // tg: code/datums/storage/storage.dm:906
    public virtual object item_interact_insert(mob_living user = null, obj_item thing = null)
    {
        attempt_insert(thing, user);
        return ITEM_INTERACT_SUCCESS;
    }

    /// Signal handler for whenever we're attacked by a mob.
    // tg: code/datums/storage/storage.dm:911
    public virtual object on_attack(datum source = null, mob user = null)
    {
        if (!Dm.truthy(attack_hand_interact))
            return null;
        if (user.active_storage == this && parent.loc == user)
        {
            user.active_storage.hide_contents(user);
            hide_contents(user);
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }
        if (ishuman(user))
        {
            mob_living_carbon_human hum = user as mob_living_carbon_human;
            if (hum.l_store == parent || hum.r_store == parent)
                return null;
        }
        if (parent.loc == user)
        {
            Waiting.Detach(open_storage(user));
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }
        return null;
    }

    /// Generates the numbers on an item in storage to show stacking.
    // tg: code/datums/storage/storage.dm:929
    public virtual object process_numerical_display()
    {
        DmList toreturn = Dm.list();

        foreach (obj_item thing in Dm.each<obj_item>(real_location))
        {
            object total_amnt = 1;

            if (isstack(thing))
            {
                obj_item_stack things = thing as obj_item_stack;
                total_amnt = things.amount;
            }

            if (!Dm.truthy(toreturn[Dm.text("[]-[]", thing.type, thing.name)]))
                toreturn[Dm.text("[]-[]", thing.type, thing.name)] = Dm.@new<datum_numbered_display>(thing, total_amnt);
            else
            {
                datum_numbered_display numberdisplay = toreturn[Dm.text("[]-[]", thing.type, thing.name)] as datum_numbered_display;
                numberdisplay.number = Dm.add(numberdisplay.number, total_amnt);
            }
        }

        return toreturn;
    }

    /// Signal handler to open up the storage when we receive a signal.
    // tg: code/datums/storage/storage.dm:948
    public virtual object open_storage_on_signal(datum source = null, mob to_show = null)
    {
        Waiting.Detach(open_storage(to_show));
        if (Dm.truthy(display_contents))
            return COMPONENT_NO_AFTERATTACK;
        return null;
    }

    /// Alt click on the storage item. Default: Open the storage.
    // tg: code/datums/storage/storage.dm:957
    public virtual object on_click_alt(datum source = null, mob user = null)
    {
        if (!Dm.truthy(click_alt_open))
            return null;

        return Dm.truthy(open_storage_on_signal(source, user)) ? CLICK_ACTION_SUCCESS : NONE;
    }

    /// Opens the storage to the mob, showing them the contents to their UI.
    // tg: code/datums/storage/storage.dm:966
    public virtual async DmTask<object> open_storage(object to_show = null)
    {
        // DEVIATION(DV27): the interaction guard (D132) before tg's first line; FALSE is "not opened".
        if (StorageGuard("open_storage", to_show as mob))
            return FALSE;

        if (isobserver(to_show))
        {
            await show_contents(to_show as mob);
            return FALSE;
        }

        if (!isliving(to_show) || !Dm.truthy(await ((mob)to_show).can_perform_action(parent, Dm.bitor(ALLOW_RESTING, FORBID_TELEKINESIS_REACH))))
            return FALSE;

        //because of the check above, it's safe to assume we're living now.
        mob_living living_to_show = (mob_living)to_show;
        if (!Dm.truthy(Dm.bitand(living_to_show.mobility_flags, MOBILITY_STORAGE)))
            return FALSE;

        if (Dm.truthy(locked))
        {
            if (!Dm.truthy(silent))
                parent.balloon_alert(living_to_show, I18n.T("closed!"));
            return FALSE;
        }

        // If we're quickdrawing boys
        if (Dm.truthy(quickdraw) && !Dm.truthy(living_to_show.get_active_held_item()))
        {
            obj_item to_remove = Dm.locate<obj_item>(real_location);
            if (!Dm.truthy(to_remove))
                return TRUE;

            if (Dm.truthy(await remove_single(living_to_show, to_remove)))
            {
                Waiting.Detach(put_in_hands_async(living_to_show, to_remove));
                if (!Dm.truthy(silent))
                {
                    living_to_show.visible_message(
                        span_warning(I18n.T("[] draws [] from []!", living_to_show, to_remove, parent)),
                        span_notice(I18n.T("You draw [] from [].", to_remove, parent))
                    );
                }
                return TRUE;
            }
        }

        // If nothing else, then we want to open the thing, so do that
        if (!Dm.truthy(await show_contents(living_to_show)))
            return FALSE;

        if (Dm.truthy(animated))
            animate_parent();

        if (Dm.truthy(do_rustle) && !Dm.truthy(silent))
            playsound(parent, (Dm.truthy(open_sound) ? open_sound : SFX_RUSTLE), 50, open_sound_vary, -5); // tg: code/datums/storage/storage.dm:1006

        return TRUE;
    }

    /// Async version of putting something into a mobs hand.
    // tg: code/datums/storage/storage.dm:1012
    public virtual async DmTask<object> put_in_hands_async(mob to_show = null, obj_item toremove = null)
    {
        if (!Dm.truthy(await to_show.put_in_hands(toremove)))
        {
            if (!Dm.truthy(silent))
                toremove.balloon_alert(to_show, I18n.T("fumbled!"));
            return TRUE;
        }
        return null;
    }

    /// Signal handler for whenever a mob walks away with us, close if they can't reach us.
    // tg: code/datums/storage/storage.dm:1019
    public virtual void close_distance(datum source = null)
    {
        foreach (mob user in Dm.each<mob>(can_see_contents()))
        {
            if (!Dm.truthy(Waiting.NoSleep(can_be_reached_by(user))))
                hide_contents(user);
        }
    }

    /// Relay for parent.IsReachableBy
    // tg: code/datums/storage/storage.dm:1027
    public virtual async DmTask<object> can_be_reached_by(mob user = null)
    {
        return await parent.IsReachableBy(user);
    }

    /// Close the storage UI for everyone viewing us.
    // tg: code/datums/storage/storage.dm:1031
    public virtual void close_all()
    {
        foreach (mob user in Dm.each_cast<mob>(is_using))
            hide_contents(user);
    }

    /// Close the storage UI for everyone viewing us except if a viewer is holding us directly (and obsevers)
    // tg: code/datums/storage/storage.dm:1036
    public virtual void close_all_non_wearers()
    {
        foreach (mob user in Dm.each_cast<mob>(is_using))
        {
            if (parent.loc != user && !isobserver(user))
                hide_contents(user);
        }
    }

    /// Closes the storage UIs of this and everything inside the parent for everyone viewing them.
    // tg: code/datums/storage/storage.dm:1042
    public virtual void close_all_recursive()
    {
        close_all();
        foreach (atom_movable movable in Dm.each_cast<atom_movable>(parent.get_all_contents()))
            movable.atom_storage?.close_all();
    }

    /// Refresh the views of everyone currently viewing the storage.
    // tg: code/datums/storage/storage.dm:1048
    public virtual async DmTask refresh_views()
    {
        foreach (mob user in Dm.each<mob>(can_see_contents()))
            await show_contents(user);
    }

    /// Checks who is currently capable of viewing our storage (and is.)
    // tg: code/datums/storage/storage.dm:1053
    public virtual object can_see_contents()
    {
        DmList seeing = Dm.list();
        foreach (mob user in Dm.each<mob>(is_using))
        {
            if (user.active_storage == this && Dm.truthy(user.client))
                Dm.append(seeing, user);
            else
                hide_contents(user);
        }
        return seeing;
    }

    /**
     * Show our storage to a mob.
     *
     * Arguments
     * * mob/to_show - the mob to show the storage to
     *
     * Returns
     * * FALSE if the show failed
     * * TRUE otherwise
     */
    // tg: code/datums/storage/storage.dm:1072
    public virtual async DmTask<object> show_contents(mob to_show = null)
    {
        if (!Dm.truthy(to_show.client))
            return FALSE;

        // You can only inspect hidden contents if you're an observer
        if (!isobserver(to_show) && !Dm.truthy(display_contents))
            return FALSE;

        if (to_show.active_storage != this && (!IS_UNCONSCIOUS_OR_CRIT(to_show)))
        {
            foreach (obj_item thing in Dm.each<obj_item>(real_location))
            {
                if (Dm.truthy(await thing.on_found(to_show)))
                {
                    to_show.active_storage?.hide_contents(to_show);
                    return FALSE;
                }
            }
        }

        to_show.active_storage?.hide_contents(to_show);

        to_show.active_storage = this;

        if (ismovable(real_location))
        {
            atom_movable movable_loc = real_location as atom_movable;
            become_active_storage__gap(movable_loc, this);
        }

        LAZYINITLIST(ref storage_interfaces);

        // to_show.client?.prefs?.read_preference(/datum/preference/choiced/ui_style): the preferences are not translated
        // (GAP(lead), as in hud.cs's /datum/hud/New): ui_style2icon's default style.
        object ui_style = ui_style2icon(null);

        if (storage_interfaces[to_show] == null)
            storage_interfaces[to_show] = await Dm.new_async(storage_type, ui_style, this, to_show);

        orient_storage();

        LAZYOR(ref is_using, to_show);

        datum_storage_interface shown = storage_interfaces[to_show] as datum_storage_interface;
        // Don't add to screen_objects as that one gets its contents actually deleted
        DmList t__1 = to_show.hud_used.screen_groups[HUD_GROUP_STORAGE] as DmList;
        LAZYOR(ref t__1, shown.list_ui_elements());
        to_show.hud_used.screen_groups[HUD_GROUP_STORAGE] = t__1;
        Dm.combine(to_show.client.screen, shown.list_ui_elements());
        DmList t__2 = to_show.hud_used.screen_groups[HUD_GROUP_STORAGE] as DmList;
        LAZYOR(ref t__2, real_location.contents);
        to_show.hud_used.screen_groups[HUD_GROUP_STORAGE] = t__2;
        Dm.combine(to_show.client.screen, real_location.contents);

        return TRUE;
    }

    /**
     * Hide our storage from a mob.
     *
     * Arguments
     * * mob/to_hide - the mob to hide the storage from
     */
    // tg: code/datums/storage/storage.dm:1119
    public virtual object hide_contents(mob to_hide = null)
    {
        if (to_hide.active_storage == this)
            to_hide.active_storage = null;

        if (!Dm.truthy(LAZYLEN(is_using)) && ismovable(real_location))
        {
            atom_movable movable_loc = real_location as atom_movable;
            lose_active_storage__gap(movable_loc, this);
        }

        if (!Dm.truthy(LAZYLEN(storage_interfaces)) || storage_interfaces[to_hide] == null)
            return TRUE;

        if (Dm.truthy(LAZYLEN(is_using)))
            Dm.remove(ref is_using, to_hide);

        datum_storage_interface hidden = storage_interfaces[to_hide] as datum_storage_interface;
        if (Dm.truthy(to_hide.client))
        {
            Dm.remove(to_hide.client.screen, hidden.list_ui_elements());
            Dm.remove(to_hide.client.screen, real_location.contents);
        }
        if (Dm.truthy(to_hide.hud_used.screen_groups[HUD_GROUP_STORAGE]))
        {
            Dm.remove_at(to_hide.hud_used.screen_groups, (float)HUD_GROUP_STORAGE, hidden.list_ui_elements());
            Dm.remove_at(to_hide.hud_used.screen_groups, (float)HUD_GROUP_STORAGE, real_location.contents);
        }
        // QDEL_NULL(storage_interfaces[to_hide]): the list element through a temp (the macro assigns it, rules 11.1)
        datum_storage_interface t__1 = hidden;
        QDEL_NULL(ref t__1);
        storage_interfaces[to_hide] = t__1;
        Dm.remove(ref storage_interfaces, to_hide);

        return TRUE;
    }

    // tg: code/datums/storage/storage.dm:1145
    [SignalHandler]
    public virtual object action_trigger(datum source = null, datum_action triggered = null)
    {
        // M4c slice 3: the action buttons are live, so the gather-mode button is reachable (the collection itself,
        // handle_mass_pickup, stays M9).
        toggle_collection_mode(triggered.owner);

        return COMPONENT_ACTION_BLOCK_TRIGGER;
    }

    // tg: code/datums/storage/storage.dm:1152
    [SignalHandler]
    public virtual void action_deleted(datum source = null)
    {
        modeswitch_action = null;
    }

    /// Updates views of all objects in storage and stretches UI to appropriate size
    // tg: code/datums/storage/storage.dm:1158
    public virtual void orient_storage()
    {
        float adjusted_contents = Dm.length(real_location.contents);
        DmList numbered_contents = null;
        if (Dm.truthy(numerical_stacking))
        {
            numbered_contents = process_numerical_display() as DmList;
            adjusted_contents = Dm.length(numbered_contents);
        }

        //if the ammount of contents reaches some multiplier of the final column (and its not the last slot), let the player view an additional row
        float additional_row = (!Dm.truthy(Dm.mod(adjusted_contents, screen_max_columns)) && adjusted_contents < max_slots) ? 1 : 0;

        float columns = clamp(max_slots, 1, screen_max_columns);
        float rows = clamp(ceil(adjusted_contents / columns) + additional_row, 1, screen_max_rows);

        foreach (mob ui_user in Dm.each_cast<mob>(storage_interfaces))
        {
            if (storage_interfaces[ui_user] == null)
                continue;
            ((datum_storage_interface)storage_interfaces[ui_user]).update_position(
                screen_start_x,
                screen_pixel_x,
                screen_start_y,
                screen_pixel_y,
                columns,
                rows,
                ui_user,
                real_location,
                numbered_contents
            );
        }
    }

    // tg: code/datums/storage/storage.dm:1191
    public virtual void toggle_collection_mode(mob user = null)
    {
        collection_mode = Dm.mod(collection_mode + 1, 3);
        switch (collection_mode)
        {
            case COLLECT_SAME:
                parent.balloon_alert(user, I18n.T("will now only pick up a single type"));
                break;
            case COLLECT_EVERYTHING:
                parent.balloon_alert(user, I18n.T("will now pick up everything"));
                break;
            case COLLECT_ONE:
                parent.balloon_alert(user, I18n.T("will now pick up one at a time"));
                break;
        }
    }

    /// Gives a spiffy animation to our parent to represent opening and closing.
    // tg: code/datums/storage/storage.dm:1202
    public virtual void animate_parent()
    {
        object old_matrix = parent.transform;
        animate(parent, Dm.named("time", 1.5f), Dm.named("loop", 0), Dm.named("transform", ((matrix)parent.transform).Scale(1.07f, 0.9f)));
        animate(Dm.named("time", 2), Dm.named("transform", old_matrix));
    }

    /// Signal proc for [COMSIG_ATOM_CONTENTS_WEIGHT_CLASS_CHANGED] to drop items out of our storage if they're suddenly too heavy.
    // tg: code/datums/storage/storage.dm:1208
    public virtual void contents_changed_w_class(datum source = null, obj_item changed = null, object old_w_class = null, object new_w_class = null)
    {
        // If old weight already overloaded the storage, don't drop the item out just in case we're inside of a premade box
        if (Dm.num(new_w_class) <= max_specific_storage && (Dm.num(get_total_weight()) <= max_total_storage || Dm.num(get_total_weight()) - Dm.num(new_w_class) + Dm.num(old_w_class) > max_total_storage))
            return;

        if (!Dm.truthy(attempt_remove(changed, parent.drop_location() as atom)))
            return;

        changed.visible_message(span_warning(I18n.T("[] falls out of []!", changed, parent)), vision_distance: COMBAT_MESSAGE_RANGE);
    }

    ///Assign a new value to the locked variable. If it's higher than NOT_LOCKED, close the UIs and update the appearance of the parent.
    // tg: code/datums/storage/storage.dm:1221
    public virtual void set_locked(object new_locked = null)
    {
        if (Dm.eq(locked, new_locked))
            return;
        locked = Dm.num(new_locked);
        if (Dm.num(new_locked) > STORAGE_NOT_LOCKED)
            close_all_recursive();
        parent.update_appearance();
    }

    // DEVIATION(DV27): the interaction guard of D132 for the storage datum (docs/ai/specs/interaction.md 7, P3's row). A storage
    // datum whose own type is inert (a /datum/storage subtype whose tg procs nobody declared, so its overrides of the
    // insert and open hooks would silently run the base's) refuses the action as InertSeam.MissingOverride does: one host
    // log line per type and proc and a count through Dm.Deferred, tg's "not ported yet" balloon to the user, true.
    // GAP(P1): InertSeam.MissingOverride guards only GuardedProcs at atoms; a datum overload would replace this helper.
    private bool StorageGuard(string proc, mob user)
    {
        if (!InertSeam.IsInertType(GetType()))
            return false;
        string path = DmTypes.PathOf(GetType()) ?? GetType().Name;
        Dm.Deferred("inert guard: " + path + " " + proc, "not ported yet: the storage type's own tg " + proc + " is not translated (D132, DV27)");
        if (user != null && parent != null)
            parent.balloon_alert(user, I18n.T("not ported yet"));
        return true;
    }

    // GAP(lead): /atom/movable/become_active_storage (atoms_movable.dm:1122) is a Dm.NotTranslated root in the lead's
    // atoms_movable.cs; a local copy of its body until the lead translates it there.
    // tg: code/game/atoms_movable.dm:1122
    private static void become_active_storage__gap(atom_movable src, datum_storage source)
    {
        if (!HAS_TRAIT(src, TRAIT_ACTIVE_STORAGE))
        {
            DmList locations = Dm.add(get_nested_locs(src), src) as DmList;
            foreach (atom_movable location in Dm.each_cast<atom_movable>(locations))
                LAZYADDASSOCLIST(ref location.important_recursive_contents, RECURSIVE_CONTENTS_ACTIVE_STORAGE, src);
        }
        ADD_TRAIT(src, TRAIT_ACTIVE_STORAGE, REF(source));
    }

    // GAP(lead): /atom/movable/lose_active_storage (atoms_movable.dm:1129), as above.
    // tg: code/game/atoms_movable.dm:1129
    private static void lose_active_storage__gap(atom_movable src, datum_storage source)
    {
        if (!HAS_TRAIT(src, TRAIT_ACTIVE_STORAGE))
            return;
        REMOVE_TRAIT(src, TRAIT_ACTIVE_STORAGE, REF(source));
        if (HAS_TRAIT(src, TRAIT_ACTIVE_STORAGE))
            return;

        DmList locations = Dm.add(get_nested_locs(src), src) as DmList;
        foreach (atom_movable location in Dm.each_cast<atom_movable>(locations))
            LAZYREMOVEASSOC(ref location.important_recursive_contents, RECURSIVE_CONTENTS_ACTIVE_STORAGE, src);
    }

}

public partial class datum_controller_global_vars
{
    /// Almost 100% of the time the lists passed into set_holdable are reused for each instance
    // tg: code/datums/storage/storage.dm:328  GLOBAL_LIST_EMPTY(cached_storage_typecaches)
    public DmList cached_storage_typecaches;

    // tg: code/datums/storage/storage.dm:328  (GLOBAL_MANAGED(cached_storage_typecaches, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcached_storage_typecaches()
    {
        cached_storage_typecaches = Dm.list();
        Dm.append(ref gvars_datum_init_order, "cached_storage_typecaches");
    }
}
