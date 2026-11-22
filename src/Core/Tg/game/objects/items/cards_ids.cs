// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/cards_ids.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P5 (the lead): the card and ID card procs of P5's list (docs/ai/specs/m4a-ownership.md): the access and wildcard
// procs, the equip and drop paths that give a worn or held card's access to its holder (COMSIG_MOB_RETRIEVE_ACCESS), the
// label, the advanced card's overlays (the trim's department and job icons), the context tips. The bank account, the
// holopay and money procs came with the economy (gaps P4a, D327); the chat icons (icon2html, getFlatIcon) are M9; the chameleon card came with
// the syndicate campaign G1b (D316), the cardboard card comes after the core version; the prisoner and plainclothes cards are gaps J4.

/*
 * DATA CARDS - Used for the IC data card reader
 */
// tg: code/game/objects/items/cards_ids.dm:21
[DmPath("/obj/item/card")]
public partial class obj_item_card : obj_item
{
    // tg: code/game/objects/items/cards_ids.dm:43
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        cached_flat_icon = null;
        return dot;
    }

    /// Called to get what name this card represents
    // tg: code/game/objects/items/cards_ids.dm:47
    public virtual object get_displayed_name(float? honorifics = null)
    {
        honorifics ??= FALSE;
        return null;
    }

    /// If no cached_flat_icon exists, this proc creates it and crops it. This proc then returns the cached_flat_icon. Intended for use displaying ID card icons in chat.
    // tg: code/game/objects/items/cards_ids.dm:51
    public virtual object get_cached_flat_icon()
    {
        Dm.NotTranslated("code/game/objects/items/cards_ids.dm:51", "get_cached_flat_icon: getFlatIcon, the card's picture in chat (chat icons, M10+)");
        return default;
    }
}

/*
 * ID CARDS
 */
/// "Retro" ID card that renders itself as the icon state with no overlays.
// tg: code/game/objects/items/cards_ids.dm:62
[DmPath("/obj/item/card/id")]
public partial class obj_item_card_id : obj_item_card
{
    /// List of logos available for holopay customization - via font awesome 5
    // tg: code/game/objects/items/cards_ids.dm:83  var/static/list/available_logos (a type static, rules 10.6; gaps P4a)
    /// Max time interval between projecting holopays
    // tg: code/game/objects/items/cards_ids.dm:5  #define HOLOPAY_PROJECTION_INTERVAL (7 SECONDS) (file-local, #undef at :2059; gaps P4a)
    private const float HOLOPAY_PROJECTION_INTERVAL = 7 * SECONDS;

    internal static DmList available_logos => TgState.Current.obj_item_card_id__available_logos ??= Dm.list("angry", "ankh", "bacon", "band-aid", "cannabis", "cat", "cocktail", "coins", "comments-dollar",
        "cross", "cut", "dog", "donate", "dna", "fist-raised", "flask", "glass-cheers", "glass-martini-alt", "hamburger", "hand-holding-usd",
        "hat-wizard", "head-side-cough-slash", "heart", "heart-broken", "laugh-beam", "leaf", "money-check-alt", "music", "piggy-bank",
        "pizza-slice", "prescription-bottle-alt", "radiation", "robot", "smile", "skull-crossbones", "smoking", "space-shuttle", "tram",
        "trash", "user-ninja", "utensils", "wrench");

    /// Trim datum associated with the card. Controls which job icon is displayed on the card and which accesses do not require wildcards.
    // tg: code/game/objects/items/cards_ids.dm:107  var/datum/id_trim/trim
    // M4a P5: object, not the generator's Type (rules 3.2 V3): the card types give it a trim typepath and Initialize
    // replaces it with the trim's singleton (SSid_access.apply_trim_to_card), so it holds a typepath or a datum (V7). A
    // hand-written var of a hand-written class (types-converter.md 3, "Hand"): the generator leaves it out.
    public object trim;

    // tg: code/game/objects/items/cards_ids.dm:63  The type's overrides (rules 10.2): a hand-written var block, so they are
    // hand-written too (types-converter.md 3, "Hand"; the conformance test compares them with tg's).
    public obj_item_card_id()
    {
        name = "retro identification card";
        desc = "A card used to provide ID and determine access across the station.";
        icon_state = "card_grey";
        worn_icon_state = "nothing";
        slot_flags = ITEM_SLOT_ID;
        interaction_flags_click = FORBID_TELEKINESIS_REACH;
        armor_type = typeof(datum_armor_card_id);
        resistance_flags = Dm.bitor(FIRE_PROOF, ACID_PROOF);
        item_flags = Dm.bitor(Dm.num(Dm.initial(typeof(obj_item_card), (obj_item p) => p.item_flags)), NO_MAT_REDEMPTION); // parent_type::item_flags | NO_MAT_REDEMPTION
    }

    // tg: code/game/objects/items/cards_ids.dm:71, :112, :115; code/modules/reta/reta_id_card.dm:8, :10
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        custom_materials = Dm.list(Dm.kv(typeof(datum_material_iron), SMALL_MATERIAL_AMOUNT * 2), Dm.kv(typeof(datum_material_glass), SMALL_MATERIAL_AMOUNT));
        access = Dm.list();
        wildcard_slots = Dm.list();
        reta_temp_access = Dm.list();
        reta_timers = Dm.list();
    }

    // tg: code/game/objects/items/cards_ids.dm:143
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        // tg: :146  (SSjob?: a test harness world that creates items without Master has no SSjob, as SSid_access below, rules 10.6)
        var blank_bank_account = Dm.@new<datum_bank_account>(I18n.T("Unassigned"), SSjob?.get_job_type(typeof(datum_job_unassigned)), Dm.named("player_account", FALSE));
        set_account(blank_bank_account);
        registered_account.replaceable = TRUE;

        // Applying the trim updates the label and icon, so don't do this twice.
        // (SSid_access?: a test harness world that creates items without Master has no SSid_access, rules 10.6's
        // exception for harness worlds; a booted world always has it, and there a trim path is always applied.)
        if (ispath(trim) && SSid_access != null)
            Waiting.NoSleep(SSid_access.apply_trim_to_card(this, trim));
        else
        {
            update_label();
            update_appearance();
        }

        // Apply any active RETA grants to this new ID card
        // This will only do something if there are active grants, so it's safe to call always
        apply_active_reta_grants_to_card(this);

        register_item_context();
        register_context();

        RegisterSignal(this, COMSIG_ATOM_UPDATED_ICON, nameof(update_in_wallet));
        if (prob(1))
            ADD_TRAIT(this, TRAIT_TASTEFULLY_THICK_ID_CARD, ROUNDSTART_TRAIT);
        return dot;
    }

    // tg: code/game/objects/items/cards_ids.dm:168
    public override object Destroy(float? force = null)
    {
        // tg: code/modules/reta/reta_id_card.dm:196 (compiled after this file, so its override runs first)
        reta_destroy();
        clear_account();
        QDEL_NULL(ref my_store);
        if (isitem(loc))
            UnregisterSignal(loc, Dm.list(COMSIG_ITEM_EQUIPPED, COMSIG_ITEM_DROPPED));
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/cards_ids.dm:175
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        if (isitem(old_loc))
            unequip_from_item_loc(old_loc);
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (isitem(loc))
            equip_to_item_loc(loc);
        return dot;
    }

    // tg: code/game/objects/items/cards_ids.dm:182
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_ID)))
        {
            RegisterSignal(user, COMSIG_MOVABLE_POINTED, nameof(on_pointed));
            if (ishuman(user))
            {
                mob_living_carbon_human as_human = user as mob_living_carbon_human;
                as_human.update_visible_name();
            }
        }
        if (Dm.truthy(Dm.bitand(slot, Dm.bitor(ITEM_SLOT_ID, ITEM_SLOT_HANDS))))
            RegisterSignal(user, COMSIG_MOB_RETRIEVE_ACCESS, nameof(retrieve_access));
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_POCKETS)))
        {
            //putting it in your pocket doesn't let you use it as access.
            UnregisterSignal(user, COMSIG_MOB_RETRIEVE_ACCESS);
        }
    }

    // The earlier of tg's two definitions of dropped at this type (rules 2.5, 7.5): the later one's ..() calls it.
    // tg: code/game/objects/items/cards_ids.dm:195
    public async DmTask<object> dropped__d0(mob user = null, float? silent = null, params object[] __extra)
    {
        UnregisterSignal(user, Dm.list(COMSIG_MOVABLE_POINTED, COMSIG_MOB_RETRIEVE_ACCESS));
        if (isitem(loc))
            equip_to_item_loc(loc); // "dropped" into an item, like a worn wallet
        if (ishuman(user))
        {
            mob_living_carbon_human as_human = user as mob_living_carbon_human;
            as_human.update_visible_name();
        }
        return await base.dropped(user, silent, __extra);
    }

    // tg: code/game/objects/items/cards_ids.dm:204
    [DmParams("user", "silent")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        silent ??= FALSE;
        object dot = await dropped__d0(user, silent, __extra);
        if (ishuman(user))
        {
            mob_living_carbon_human as_human = user as mob_living_carbon_human;
            as_human.update_visible_name();
        }
        return dot;
    }

    /// Getter for the registered name, with optional honorifics
    // tg: code/game/objects/items/cards_ids.dm:211
    public override object get_displayed_name(float? honorifics = null)
    {
        honorifics ??= FALSE;
        if (Dm.truthy(honorifics) && honorific_position != HONORIFIC_POSITION_NONE && Dm.truthy(honorific_title))
            return honorific_title;
        return registered_name;
    }

    /// ID card is being equipped to an item (like a pda or wallet)
    // tg: code/game/objects/items/cards_ids.dm:217
    public virtual void equip_to_item_loc(atom new_loc = null)
    {
        RegisterSignal(new_loc, COMSIG_ITEM_EQUIPPED, nameof(on_loc_equipped), @override: TRUE);
        RegisterSignal(new_loc, COMSIG_ITEM_DROPPED, nameof(on_loc_dropped), @override: TRUE);
        if (ismob(new_loc.loc))
        {
            mob wearer = new_loc.loc as mob;
            // Equip chain shenanigans
            UnregisterSignal(wearer, Dm.list(COMSIG_MOVABLE_POINTED, COMSIG_MOB_RETRIEVE_ACCESS));
            on_loc_equipped(new_loc, wearer, wearer.get_slot_by_item(new_loc as obj_item));
        }
    }

    /// ID card is being unequipped from an item (like a pda or wallet)
    // tg: code/game/objects/items/cards_ids.dm:227
    public virtual void unequip_from_item_loc(atom old_loc = null)
    {
        UnregisterSignal(old_loc, Dm.list(COMSIG_ITEM_EQUIPPED, COMSIG_ITEM_DROPPED));
        if (ismob(old_loc.loc))
            UnregisterSignal(old_loc.loc, Dm.list(COMSIG_MOVABLE_POINTED, COMSIG_MOB_RETRIEVE_ACCESS));
    }

    // tg: code/game/objects/items/cards_ids.dm:232
    [SignalHandler]
    public virtual void on_loc_equipped(datum source = null, mob equipper = null, object slot = null)
    {
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_ID)))
            RegisterSignal(equipper, COMSIG_MOVABLE_POINTED, nameof(on_pointed));
        if (Dm.truthy(Dm.bitand(slot, Dm.bitor(ITEM_SLOT_ID, ITEM_SLOT_HANDS))))
            RegisterSignal(equipper, COMSIG_MOB_RETRIEVE_ACCESS, nameof(retrieve_access));
    }

    // tg: code/game/objects/items/cards_ids.dm:240
    [SignalHandler]
    public virtual void on_loc_dropped(datum source = null, mob dropper = null)
    {
        UnregisterSignal(dropper, Dm.list(COMSIG_MOVABLE_POINTED, COMSIG_MOB_RETRIEVE_ACCESS));
    }

    ///Called when we're being used as access.
    // tg: code/game/objects/items/cards_ids.dm:245
    [SignalHandler]
    public virtual void retrieve_access(datum source = null, DmList player_access = null)
    {
        Dm.append(player_access, GetAccess());
    }

    // tg: code/game/objects/items/cards_ids.dm:249
    [SignalHandler]
    public virtual void on_pointed(mob_living user = null, atom pointed = null, obj_effect_temp_visual_point point = null)
    {
        if ((!Dm.truthy(big_pointer) && !Dm.truthy(pointer_color)) || HAS_TRAIT(user, TRAIT_UNKNOWN_APPEARANCE))
            return;
        if (!Dm.eq(point.icon_state, Dm.initial(typeof(obj_effect_temp_visual_point), (obj_effect_temp_visual_point p) => p.icon_state))) //it differs from the original icon_state already.
            return;
        if (loc != user)
        {
            if (!isitem(loc))
                return;
            obj_item as_item = loc as obj_item;
            if (as_item.GetID() != this)
                return;
        }
        if (Dm.truthy(big_pointer))
            point.icon_state = "arrow_large";
        if (Dm.truthy(pointer_color))
        {
            point.icon_state = Dm.text("[]_white", point.icon_state);
            point.color = pointer_color;
            mutable_appearance highlight = mutable_appearance(point.icon, Dm.text("[]_highlights", point.icon_state), appearance_flags: RESET_COLOR) as mutable_appearance;
            point.add_overlay(highlight);
        }
    }

    // tg: code/game/objects/items/cards_ids.dm:269
    public override object get_id_examine_strings(mob user = null)
    {
        object dot = base.get_id_examine_strings(user);
        // . += list("[icon2html(get_cached_flat_icon(), user, extra_classes = "hugeicon")]"): the card's picture in the chat line
        Dm.Deferred("code/game/objects/items/cards_ids.dm:271", "get_id_examine_strings: icon2html of the card's flat icon (the 3D client's chat is text: the chat icons)");
        return dot;
    }

    // tg: code/game/objects/items/cards_ids.dm:273
    public override object get_examine_icon(mob user = null)
    {
        Dm.Deferred("code/game/objects/items/cards_ids.dm:274", "get_examine_icon: icon2html of the card's flat icon (the 3D client's chat is text: the chat icons)");
        return null;
    }

    /**
     * Helper proc, checks whether the ID card can hold any given set of wildcards.
     *
     * Returns TRUE if the card can hold the wildcards, FALSE otherwise.
     * Arguments:
     * * wildcard_list - List of accesses to check.
     * * try_wildcard - If not null, will attempt to add wildcards for this wildcard specifically and will return FALSE if the card cannot hold all wildcards in this slot.
     */
    // tg: code/game/objects/items/cards_ids.dm:284
    public virtual object can_add_wildcards(DmList wildcard_list = null, object try_wildcard = null)
    {
        if (!Dm.truthy(length(wildcard_list)))
            return TRUE;

        DmList new_wildcard_limits = Dm.list();

        foreach (object flag_name in Dm.each(wildcard_slots))
        {
            if (Dm.truthy(try_wildcard) && !Dm.eq(flag_name, try_wildcard))
                continue;
            DmList wildcard_info = wildcard_slots[flag_name] as DmList;
            new_wildcard_limits[flag_name] = Dm.sub(wildcard_info["limit"], length(wildcard_info["usage"]));
        }

        if (!Dm.truthy(length(new_wildcard_limits)))
            return FALSE;

        bool wildcard_allocated;
        foreach (object wildcard in Dm.each(wildcard_list))
        {
            object wildcard_flag = SSid_access.get_access_flag(wildcard);
            wildcard_allocated = false;
            foreach (object flag_name in Dm.each(new_wildcard_limits))
            {
                object limit_flags = SSid_access.wildcard_flags_by_wildcard[flag_name];
                if (!Dm.truthy(Dm.bitand(wildcard_flag, limit_flags)))
                    continue;
                // Negative limits mean infinite slots. Positive limits mean limited slots still available. 0 slots means no slots.
                if (Dm.eq(new_wildcard_limits[flag_name], 0))
                    continue;
                new_wildcard_limits[flag_name] = Dm.sub(new_wildcard_limits[flag_name], 1);
                wildcard_allocated = true;
                break;
            }
            if (!wildcard_allocated)
                return FALSE;
        }

        return TRUE;
    }

    /**
     * Attempts to add the given wildcards to the ID card.
     *
     * Arguments:
     * * wildcard_list - List of accesses to add.
     * * try_wildcard - If not null, will attempt to add all wildcards to this wildcard slot only.
     * * mode - The method to use when adding wildcards. See define for ERROR_ON_FAIL
     */
    // tg: code/game/objects/items/cards_ids.dm:326
    public virtual void add_wildcards(DmList wildcard_list = null, object try_wildcard = null, float? mode = null)
    {
        mode ??= ERROR_ON_FAIL;
        bool wildcard_allocated;
        // Iterate through each wildcard in our list. Get its access flag. Then iterate over wildcard slots and try to fit it in.
        foreach (object wildcard in Dm.each(wildcard_list))
        {
            object wildcard_flag = SSid_access.get_access_flag(wildcard);
            wildcard_allocated = false;
            foreach (object flag_name in Dm.each(wildcard_slots))
            {
                if (Dm.eq(flag_name, WILDCARD_NAME_FORCED))
                    continue;

                if (Dm.truthy(try_wildcard) && !Dm.eq(flag_name, try_wildcard))
                    continue;

                object limit_flags = SSid_access.wildcard_flags_by_wildcard[flag_name];

                if (!Dm.truthy(Dm.bitand(wildcard_flag, limit_flags)))
                    continue;

                DmList wildcard_info = wildcard_slots[flag_name] as DmList;
                object wildcard_limit = wildcard_info["limit"];
                DmList wildcard_usage = wildcard_info["usage"] as DmList;

                object wildcard_count = Dm.sub(wildcard_limit, length(wildcard_usage));

                // Negative limits mean infinite slots. Positive limits mean limited slots still available. 0 slots means no slots.
                if (Dm.eq(wildcard_count, 0))
                    continue;

                Dm.combine(ref wildcard_usage, wildcard);
                Dm.combine(ref access, wildcard);
                wildcard_allocated = true;
                break;
            }
            // Fallback for if we couldn't allocate the wildcard for some reason.
            if (!wildcard_allocated)
            {
                if (mode == ERROR_ON_FAIL)
                    throw Dm.CRASH(Dm.text("Wildcard ([]) could not be added to [].", wildcard, this), "code/game/objects/items/cards_ids.dm", 362);

                if (mode == TRY_ADD_ALL)
                    continue;

                // If the card has no info for historic forced wildcards, create the list.
                if (!Dm.truthy(wildcard_slots[WILDCARD_NAME_FORCED]))
                    wildcard_slots[WILDCARD_NAME_FORCED] = Dm.list(Dm.kv("limit", 0), Dm.kv("usage", Dm.list()));

                DmList wildcard_info = wildcard_slots[WILDCARD_NAME_FORCED] as DmList;
                DmList wildcard_usage = wildcard_info["usage"] as DmList;
                Dm.combine(ref wildcard_usage, wildcard);
                Dm.combine(ref access, wildcard);
                wildcard_info["limit"] = length(wildcard_usage);
            }
        }
    }

    /**
     * Removes wildcards from the ID card.
     *
     * Arguments:
     * * wildcard_list - List of accesses to remove.
     */
    // tg: code/game/objects/items/cards_ids.dm:382
    public virtual void remove_wildcards(DmList wildcard_list = null)
    {
        bool wildcard_removed;
        // Iterate through each wildcard in our list. Get its access flag. Then iterate over wildcard slots and try to remove it.
        foreach (object wildcard in Dm.each(wildcard_list))
        {
            wildcard_removed = false;
            foreach (object flag_name in Dm.each(wildcard_slots))
            {
                if (Dm.eq(flag_name, WILDCARD_NAME_FORCED))
                    continue;

                DmList wildcard_info = wildcard_slots[flag_name] as DmList;
                DmList wildcard_usage = wildcard_info["usage"] as DmList;

                if (!Dm.@in(wildcard, wildcard_usage))
                    continue;

                Dm.remove(ref wildcard_usage, wildcard);
                Dm.remove(ref access, wildcard);
                wildcard_removed = true;
                break;
            }
            // Fallback to see if this was a force-added wildcard.
            if (!wildcard_removed)
            {
                // If the card has no info for historic forced wildcards, that's an error state.
                if (!Dm.truthy(wildcard_slots[WILDCARD_NAME_FORCED]))
                    stack_trace(Dm.text("Wildcard ([]) could not be removed from []. This card has no forced wildcard data and the wildcard is not in this card's wildcard lists.", wildcard, this), "code/game/objects/items/cards_ids.dm", 406);

                DmList wildcard_info = wildcard_slots[WILDCARD_NAME_FORCED] as DmList;
                DmList wildcard_usage = wildcard_info["usage"] as DmList;

                if (!Dm.@in(wildcard, wildcard_usage))
                    stack_trace(Dm.text("Wildcard ([]) could not be removed from []. This access is not a wildcard on this card.", wildcard, this), "code/game/objects/items/cards_ids.dm", 412);

                Dm.remove(ref wildcard_usage, wildcard);
                Dm.remove(ref access, wildcard);
                wildcard_info["limit"] = length(wildcard_usage);

                if (!Dm.truthy(wildcard_info["limit"]))
                    Dm.remove(ref wildcard_slots, WILDCARD_NAME_FORCED);
            }
        }
    }

    /**
     * Attempts to add the given accesses to the ID card as non-wildcards.
     *
     * Depending on the mode, may add accesses as wildcards or error if it can't add them as non-wildcards.
     * Arguments:
     * * add_accesses - List of accesses to check.
     * * try_wildcard - If not null, will attempt to add all accesses that require wildcard slots to this wildcard slot only.
     * * mode - The method to use when adding accesses. See define for ERROR_ON_FAIL
     */
    // tg: code/game/objects/items/cards_ids.dm:429
    public virtual object add_access(DmList add_accesses = null, object try_wildcard = null, float? mode = null)
    {
        mode ??= ERROR_ON_FAIL;
        DmList wildcard_access = Dm.list();
        DmList normal_access = Dm.list();

        build_access_lists(add_accesses, normal_access, wildcard_access);

        // Check if we can add the wildcards.
        if (mode == ERROR_ON_FAIL)
        {
            if (!Dm.truthy(can_add_wildcards(wildcard_access, try_wildcard)))
                throw Dm.CRASH(Dm.text("Cannot add wildcards from \\[[]\\] to []", add_accesses.Join(","), this), "code/game/objects/items/cards_ids.dm", 438);
        }

        // All clear to add the accesses.
        Dm.combine(ref access, normal_access);
        if (mode != TRY_ADD_ALL_NO_WILDCARD)
            add_wildcards(wildcard_access, try_wildcard, mode: mode);

        return TRUE;
    }

    /**
     * Removes the given accesses from the ID Card.
     *
     * Will remove the wildcards if the accesses given are on the card as wildcard accesses.
     * Arguments:
     * * rem_accesses - List of accesses to remove.
     */
    // tg: code/game/objects/items/cards_ids.dm:454
    public virtual void remove_access(DmList rem_accesses = null)
    {
        DmList wildcard_access = Dm.list();
        DmList normal_access = Dm.list();

        build_access_lists(rem_accesses, normal_access, wildcard_access);

        Dm.remove(ref access, normal_access);
        remove_wildcards(wildcard_access);
    }

    /**
     * Attempts to set the card's accesses to the given accesses, clearing all accesses not in the given list.
     *
     * Depending on the mode, may add accesses as wildcards or error if it can't add them as non-wildcards.
     * Arguments:
     * * new_access_list - List of all accesses that this card should hold exclusively.
     * * mode - The method to use when setting accesses. See define for ERROR_ON_FAIL
     */
    // tg: code/game/objects/items/cards_ids.dm:471
    public virtual object set_access(DmList new_access_list = null, float? mode = null)
    {
        mode ??= ERROR_ON_FAIL;
        DmList wildcard_access = Dm.list();
        DmList normal_access = Dm.list();

        if (Dm.truthy(length(new_access_list)))
            build_access_lists(new_access_list, normal_access, wildcard_access);

        // Check if we can add the wildcards.
        if (mode == ERROR_ON_FAIL)
        {
            if (!Dm.truthy(can_add_wildcards(wildcard_access)))
                throw Dm.CRASH(Dm.text("Cannot add wildcards from \\[[]\\] to []", new_access_list.Join(","), this), "code/game/objects/items/cards_ids.dm", 481);
        }

        clear_access();

        access = normal_access.Copy();

        if (mode != TRY_ADD_ALL_NO_WILDCARD)
            add_wildcards(wildcard_access, mode: mode);

        return TRUE;
    }

    /// Clears all accesses from the ID card - both wildcard and normal.
    // tg: code/game/objects/items/cards_ids.dm:493
    public virtual void clear_access()
    {
        // Go through the wildcards and reset them.
        foreach (object flag_name in Dm.each(wildcard_slots))
        {
            DmList wildcard_info = wildcard_slots[flag_name] as DmList;
            DmList wildcard_usage = wildcard_info["usage"] as DmList;
            wildcard_usage.Cut();
        }

        // Hard reset access
        access.Cut();
    }

    /// Sets the bank account for the ID card.
    // tg: code/game/objects/items/cards_ids.dm:504
    public virtual void set_account(datum_bank_account account = null, object transfer_funds = null)
    {
        transfer_funds ??= FALSE;
        if (registered_account == account)
            return;
        if (registered_account != null)
        {
            if (Dm.truthy(transfer_funds))
                account?.transfer_money(registered_account, registered_account.account_balance, I18n.T("Account transfer"));
            clear_account();
        }
        if (account == null)
            return;

        if (Dm.@in(this, account.bank_cards))
            stack_trace(Dm.text("Despite [] not being registered to [], the account already has it within the bank_cards list.", this, account), "code/game/objects/items/cards_ids.dm", 515);

        registered_account = account;
        LAZYOR(ref registered_account.bank_cards, this);
        registered_account.civilian_bounty?.on_selected(this);
    }

    /// Clears the economy account from the ID card.
    // tg: code/game/objects/items/cards_ids.dm:522
    public virtual void clear_account()
    {
        if (registered_account == null)
            return;

        registered_account.civilian_bounty?.on_reset(this);
        LAZYREMOVE(ref registered_account.bank_cards, this);
        registered_account = null;
    }

    /**
     * Helper proc. Creates access lists for the access procs.
     *
     * Takes the accesses list and compares it with the trim. Any basic accesses that match the trim are
     * added to basic_access_list and the rest are added to wildcard_access_list.

     * This proc directly modifies the lists passed in as args. It expects these lists to be instantiated.
     * There is no return value.
     * Arguments:
     * * accesses - List of accesses you want to stort into basic_access_list and wildcard_access_list. Should not be null.
     * * basic_access_list - Mandatory argument. The proc modifies the list passed in this argument and adds accesses the trim supports to it.
     * * wildcard_access_list - Mandatory argument. The proc modifies the list passed in this argument and adds accesses the trim does not support to it.
     */
    // tg: code/game/objects/items/cards_ids.dm:544
    public virtual void build_access_lists(DmList accesses = null, DmList basic_access_list = null, DmList wildcard_access_list = null)
    {
        if (!Dm.truthy(length(accesses)) || basic_access_list == null || wildcard_access_list == null)
            throw Dm.CRASH("Invalid parameters passed to build_access_lists", "code/game/objects/items/cards_ids.dm", 546);

        DmList trim_accesses = (trim as datum_id_trim)?.access;

        // Populate the lists.
        foreach (object new_access in Dm.each(accesses))
        {
            if (Dm.@in(new_access, trim_accesses))
            {
                Dm.combine(ref basic_access_list, new_access);
                continue;
            }

            Dm.combine(ref wildcard_access_list, new_access);
        }
    }

    /// Helper proc that determines if a card can be used in certain types of payment transactions.
    // tg: code/game/objects/items/cards_ids.dm:559
    public virtual object can_be_used_in_payment(mob_living user = null)
    {
        if (QDELETED(this) || registered_account?.account_job == null || !isliving(user))
            return FALSE;

        return TRUE;
    }

    // tg: code/game/objects/items/cards_ids.dm:565
    [DmParams("user")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(await Adjacent(user)))
        {
            object minor = null;
            if (Dm.truthy(registered_name) && Dm.truthy(registered_age) && registered_age < AGE_MINOR)
                minor = I18n.T(" <b>(MINOR)</b>");
            // GAP(lead): icon2html(src, viewers(user)) (__HELPERS/icons.dm), the card's picture in the chat line, is not
            // declared (chat icons, M9); the line names the card without it.
            user.visible_message(span_notice(I18n.T("[] shows you: [][].", user, name, minor)), span_notice(I18n.T("You show \\the [][].", name, minor)));
        }
        // tg: code/game/objects/items/cards_ids.dm:571
        add_fingerprint(user);
        return null;
    }

    // tg: code/game/objects/items/cards_ids.dm:573
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(check_allowed_items(interacting_with)) || !isfloorturf(interacting_with))
            return NONE;
        await try_project_paystand(user, interacting_with as turf);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/cards_ids.dm:579
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        object dot = await base.attack_self_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;
        await try_project_paystand(user);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/items/cards_ids.dm:586
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);

        context[SCREENTIP_CONTEXT_RMB] = I18n.T("Project pay stand");

        if (held_item == null || (held_item == this))
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Show ID");
        else if (iscash(held_item) || held_item is obj_item_storage_bag_money)
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Insert");
        else if (held_item is obj_item_rupee)
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Insert?");

        if (registered_account == null || Dm.truthy(registered_account.replaceable)) //Same check we use when we check if we can assign an account
            context[SCREENTIP_CONTEXT_ALT_RMB] = I18n.T("Assign account");
        else if (Dm.num(registered_account.account_balance) > 0)
            context[SCREENTIP_CONTEXT_ALT_LMB] = I18n.T("Withdraw []", MONEY_NAME);
        if (Dm.truthy(trim) && Dm.truthy(length((trim as datum_id_trim)?.honorifics)))
            context[SCREENTIP_CONTEXT_CTRL_LMB] = I18n.T("Toggle honorific");
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/game/objects/items/cards_ids.dm:606
    public override object add_item_context(obj_item source = null, DmList context = null, atom target = null, mob_living user = null)
    {
        object dot = base.add_item_context(source, context, target, user);
        if (iscash(target))
        {
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Insert into card");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/game/objects/items/cards_ids.dm:612
    public virtual async DmTask try_project_paystand(mob user = null, turf target = null)
    {
        if (!(last_holopay_projection <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm), inline (rules 11.1)
        {
            balloon_alert(user, I18n.T("still recharging"));
            return;
        }
        if (!Dm.truthy(can_be_used_in_payment(user as mob_living)))
        {
            balloon_alert(user, I18n.T("no account!"));
            to_chat(user, span_warning(I18n.T("You need a valid bank account to do this.")));
            return;
        }
        /// Determines where the holopay will be placed based on tile contents
        turf projection = null;
        var step_ahead = get_step(user, user.dir) as turf;
        var user_loc = user.loc as turf;
        if (target != null && Dm.truthy(can_proj_holopay(target)))
            projection = target;
        else if (Dm.truthy(can_proj_holopay(step_ahead)))
            projection = step_ahead;
        else if (Dm.truthy(can_proj_holopay(user_loc)))
            projection = user_loc;
        if (projection == null)
        {
            balloon_alert(user, I18n.T("no space"));
            to_chat(user, span_warning(I18n.T("You need to be standing on or near an open tile to do this.")));
            return;
        }
        /// Success: Valid tile for holopay placement
        if (my_store != null)
            my_store.dissipate();
        var new_store = Dm.@new<obj_structure_holopay>(projection);
        if (new_store != null && Dm.truthy(await new_store.assign_card(projection, this)))
        {
            last_holopay_projection = world.time + HOLOPAY_PROJECTION_INTERVAL; // COOLDOWN_START (__DEFINES/cooldowns.dm), inline (rules 11.1)
            playsound(projection, "sound/effects/empulse.ogg", 40, TRUE);
            my_store = new_store;
        }
    }

    /**
     * Determines whether a new holopay can be placed on the given turf.
     * Checks if there are dense contents, too many contents, or another
     * holopay already exists on the turf.
     *
     * Arguments:
     * * turf/target - The target turf to be checked for dense contents
     * Returns:
     * * TRUE if the target is a valid holopay location, FALSE otherwise.
     */
    // tg: code/game/objects/items/cards_ids.dm:653
    public virtual object can_proj_holopay(turf target = null)
    {
        if (!isfloorturf(target))
            return FALSE;
        if (Dm.truthy(target.density))
            return FALSE;
        if (length(target.contents) > 5)
            return FALSE;
        foreach (obj checked_obj in Dm.each<obj>(target.contents))
        {
            if (Dm.truthy(checked_obj.density))
                return FALSE;
            if (checked_obj is obj_structure_holopay)
                return FALSE;
        }
        return TRUE;
    }

    /**
     * Setter for the shop logo on linked holopays
     *
     * Arguments:
     * * new_logo - The new logo to be set.
     */
    // tg: code/game/objects/items/cards_ids.dm:673
    public virtual void set_holopay_logo(object new_logo = null)
    {
        if (!Dm.truthy(available_logos.Find(new_logo)))
            throw Dm.CRASH("User input a holopay shop logo that didn't exist.", "code/game/objects/items/cards_ids.dm", 675);
        holopay_logo = Dm.str(new_logo);
    }

    /**
     * Setter for changing the force fee on a holopay.
     *
     * Arguments:
     * * new_fee - The new fee to be set.
     */
    // tg: code/game/objects/items/cards_ids.dm:684
    public virtual void set_holopay_fee(object new_fee = null)
    {
        if (!isnum(new_fee))
            throw Dm.CRASH("User input a non number into the holopay fee field.", "code/game/objects/items/cards_ids.dm", 686);
        if (Dm.num(new_fee) < holopay_min_fee || Dm.num(new_fee) > holopay_max_fee)
            throw Dm.CRASH("User input a number outside of the valid range into the holopay fee field.", "code/game/objects/items/cards_ids.dm", 688);
        holopay_fee = Dm.num(new_fee);
    }

    /**
     * Setter for changing the holopay name.
     *
     * Arguments:
     * * new_name - The new name to be set.
     */
    // tg: code/game/objects/items/cards_ids.dm:697
    public virtual void set_holopay_name(object name = null)
    {
        if (length(name) < 3 || length(name) > MAX_NAME_LEN)
            to_chat(usr, span_warning(I18n.T("Must be between 3 - 42 characters.")));
        else
            holopay_name = html_encode(trim(Dm.str(name), MAX_NAME_LEN));
    }

    // tg: code/game/objects/items/cards_ids.dm:715
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_rupee)
        {
            to_chat(user, span_warning(I18n.T("Your ID smartly rejects the strange shard of glass. Who knew, apparently it's not ACTUALLY valuable!")));
            return ITEM_INTERACT_BLOCKING;
        }
        else if (iscash(tool))
            return Dm.truthy(insert_money(tool, user)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
        else if (tool is obj_item_storage_bag_money money_bag)
        {
            DmList money_contained = money_bag.contents;
            object money_added = mass_insert_money(money_contained, user);
            if (!Dm.truthy(money_added))
                return ITEM_INTERACT_BLOCKING;
            to_chat(user, span_notice(I18n.T("You stuff the contents into the card! They disappear in a puff of bluespace smoke, adding [] worth of [] to the linked account.", money_added, MONEY_NAME)));
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/game/objects/items/cards_ids.dm:740
    public virtual object insert_money(obj_item money = null, mob user = null)
    {
        bool physical_currency = false;
        if (money is obj_item_stack_spacecash || money is obj_item_coin || money is obj_item_poker_chip)
            physical_currency = true;

        if (registered_account == null)
        {
            to_chat(user, span_warning(I18n.T("[] doesn't have a linked account to deposit [] into!", this, money)));
            return FALSE;
        }
        object cash_money = money.get_item_credit_value();
        if (!Dm.truthy(cash_money))
        {
            to_chat(user, span_warning(I18n.T("[] doesn't seem to be worth anything!", money)));
            return FALSE;
        }
        registered_account.adjust_money(cash_money, I18n.T("System: Deposit"));
        SSblackbox.record_feedback("amount", "credits_inserted", cash_money);
        log_econ(Dm.text("[] [] were inserted into [] owned by []", cash_money, MONEY_NAME, this, this.registered_name));
        if (physical_currency)
            to_chat(user, span_notice(I18n.T("You stuff [] into []. It disappears in a small puff of bluespace smoke, adding [] [] to the linked account.", money, this, cash_money, MONEY_NAME)));
        else
            to_chat(user, span_notice(I18n.T("You insert [] into [], adding [] [] to the linked account.", money, this, cash_money, MONEY_NAME)));

        to_chat(user, span_notice(I18n.T("The linked account now reports a balance of [] [].", registered_account.account_balance, MONEY_SYMBOL)));
        qdel(money);
        return TRUE;
    }

    // tg: code/game/objects/items/cards_ids.dm:771
    public virtual object mass_insert_money(DmList money = null, mob user = null)
    {
        if (registered_account == null)
        {
            to_chat(user, span_warning(I18n.T("[] doesn't have a linked account to deposit into!", this)));
            return FALSE;
        }

        if (money == null || !(length(money) > 0))
            return FALSE;

        float total = 0;

        foreach (obj_item physical_money in Dm.each<obj_item>(money.Copy()))
        {
            total += Dm.num(physical_money.get_item_credit_value());
            CHECK_TICK();
        }

        registered_account.adjust_money(total, I18n.T("System: Deposit"));
        SSblackbox.record_feedback("amount", "credits_inserted", total);
        log_econ(Dm.text("[] [] were inserted into [] owned by []", total, MONEY_NAME, this, this.registered_name));
        QDEL_LIST(money);

        return total;
    }

    // tg: code/game/objects/items/cards_ids.dm:793
    public virtual object alt_click_can_use_id(mob_living user = null)
    {
        if (!isliving(user))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/items/cards_ids.dm:799
    public virtual async DmTask<object> set_new_account(mob_living user = null)
    {
        if (loc != user)
        {
            to_chat(user, span_warning(I18n.T("You must be holding the ID to continue!")));
            return FALSE;
        }
        DmList user_memories = user.mind.memories;
        var user_key = user_memories[typeof(datum_memory_key_account)] as datum_memory_key_account;
        object default_account = (user_key != null && Dm.truthy(user_key.remembered_id)) ? user_key.remembered_id : 11111;
        object new_bank_id = await tgui_input_number(user, I18n.T("Enter the account ID to associate with this card."), I18n.T("Link Bank Account"), default_account, 999999, 111111);
        if (!Dm.truthy(new_bank_id) || QDELETED(user) || QDELETED(this) || issilicon(user) || !Dm.truthy(alt_click_can_use_id(user)) || loc != user)
            return FALSE;
        if (Dm.eq(registered_account?.account_id, new_bank_id))
        {
            to_chat(user, span_warning(I18n.T("The account ID was already assigned to this card.")));
            return FALSE;
        }
        var account = SSeconomy.bank_accounts_by_id[Dm.text("[]", new_bank_id)] as datum_bank_account;
        if (account == null)
        {
            to_chat(user, span_warning(I18n.T("The account ID number provided is invalid.")));
            return FALSE;
        }
        set_account(account, transfer_funds: TRUE);
        to_chat(user, span_notice(I18n.T("The provided account has been linked to this ID card. It contains [] [].", account.account_balance, MONEY_NAME)));
        return TRUE;
    }

    // tg: code/game/objects/items/cards_ids.dm:821
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        var living_user = user as mob_living;
        if (!Dm.truthy(alt_click_can_use_id(living_user)))
            return NONE;
        if (LAZYLEN(registered_account.being_dumped) > 0)
        {
            await registered_account.bank_card_talk(span_warning("内部服务器错误"), TRUE);
            return CLICK_ACTION_SUCCESS;
        }
        if (Dm.truthy(registered_account.account_debt))
        {
            object choice = await tgui_alert(user, I18n.T("Choose An Action"), I18n.T("Bank Account"), Dm.list(I18n.T("Withdraw"), I18n.T("Pay Debt")));
            if (!Dm.truthy(choice) || QDELETED(user) || QDELETED(this) || !Dm.truthy(alt_click_can_use_id(living_user)) || loc != user)
                return CLICK_ACTION_BLOCKING;
            if (Dm.eq(choice, I18n.T("Pay Debt")))
            {
                await pay_debt(user);
                return CLICK_ACTION_SUCCESS;
            }
        }
        if (loc != user)
        {
            to_chat(user, span_warning(I18n.T("You must be holding the ID to continue!")));
            return CLICK_ACTION_BLOCKING;
        }
        if (Dm.truthy(registered_account.replaceable) && !Dm.truthy(registered_account.account_balance))
        {
            object choice = await tgui_alert(user, I18n.T("This card's account is unassigned. Would you like to link a bank account?"), I18n.T("Bank Account"), Dm.list(I18n.T("Link Account"), I18n.T("Leave Unassigned")));
            if (!Dm.truthy(choice) || QDELETED(user) || QDELETED(this) || !Dm.truthy(alt_click_can_use_id(living_user)) || loc != user)
                return CLICK_ACTION_BLOCKING;
            if (Dm.eq(choice, I18n.T("Link Account")))
            {
                await set_new_account(living_user);
                return CLICK_ACTION_SUCCESS;
            }
        }
        object amount_to_remove = await tgui_input_number(user, I18n.T("How much do you want to withdraw? (Max: [] [])", registered_account.account_balance, MONEY_SYMBOL), I18n.T("Withdraw Funds"), max_value: registered_account.account_balance);
        if (!Dm.truthy(amount_to_remove) || QDELETED(user) || QDELETED(this) || issilicon(user) || loc != user)
            return CLICK_ACTION_BLOCKING;
        if (!Dm.truthy(alt_click_can_use_id(living_user)))
            return CLICK_ACTION_BLOCKING;
        if (!Dm.truthy(registered_account.adjust_money(-Dm.num(amount_to_remove), I18n.T("System: Withdrawal"))))
        {
            float difference = Dm.num(amount_to_remove) - registered_account.account_balance;
            // MONEY_NAME_AUTOPURAL(difference) (__DEFINES/economy.dm:99), inline (rules 11.1)
            await registered_account.bank_card_talk(span_warning(I18n.T("ERROR: The linked account requires [] more [] to perform that withdrawal.", difference, Dm.text("credit[]", difference == 1 ? "" : "s"))), TRUE);
            return CLICK_ACTION_BLOCKING;
        }
        var holochip = Dm.@new<obj_item_holochip>(user.drop_location(), amount_to_remove);
        await user.put_in_hands(holochip);
        to_chat(user, span_notice(I18n.T("You withdraw [] [] into a holochip.", amount_to_remove, MONEY_NAME)));
        SSblackbox.record_feedback("amount", "credits_removed", amount_to_remove);
        log_econ(Dm.text("[] [] were removed from [] owned by []", amount_to_remove, MONEY_NAME, this, registered_name));
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/cards_ids.dm:861
    public override async DmTask<object> click_alt_secondary(mob user = null)
    {
        if (!Dm.truthy(alt_click_can_use_id(user as mob_living)))
            return null;
        if (registered_account == null || Dm.truthy(registered_account.replaceable))
            await set_new_account(user as mob_living);
        return null;
    }

    // tg: code/game/objects/items/cards_ids.dm:867
    public virtual async DmTask pay_debt(object user = null)
    {
        object amount_to_pay = await tgui_input_number(user, I18n.T("How much do you want to pay? (Max: [] [])", registered_account.account_balance, MONEY_SYMBOL), I18n.T("Debt Payment"), max_value: min(registered_account.account_balance, registered_account.account_debt));
        if (!Dm.truthy(amount_to_pay) || QDELETED(this) || loc != user || !Dm.truthy(alt_click_can_use_id(user as mob_living)))
            return;
        float prev_debt = registered_account.account_debt;
        float amount_paid = registered_account.pay_debt(amount_to_pay);
        if (Dm.truthy(amount_paid))
        {
            string message = span_notice(I18n.T("You pay [] [] of a [] [] debt. [] [] to go.", amount_to_pay, MONEY_NAME, prev_debt, MONEY_SYMBOL, registered_account.account_debt, MONEY_SYMBOL));
            if (!Dm.truthy(registered_account.account_debt))
                message = span_nicegreen(I18n.T("You pay the last [] [] of your debt, extinguishing it. Congratulations!", amount_to_pay, MONEY_NAME));
            to_chat(user, message);
        }
    }

    // tg: code/game/objects/items/cards_ids.dm:879
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(user.can_read(this)))
            return dot;

        if (Dm.truthy(registered_account) && registered_account.account_id != null)
        {
            Dm.append(ref dot, I18n.T("The account linked to the ID belongs to '[]' and reports a balance of [] [].", registered_account.account_holder, registered_account.account_balance, MONEY_SYMBOL));
            if (Dm.@in(ACCESS_COMMAND, access))
            {
                datum_bank_account linked_dept = SSeconomy.get_dep_account(registered_account.account_job.paycheck_department);
                Dm.append(ref dot, I18n.T("The [] linked to the ID reports a balance of [] [].", linked_dept.account_holder, linked_dept.account_balance, MONEY_SYMBOL));
            }
        }
        else
            Dm.append(ref dot, span_notice(I18n.T("Alt-Right-Click the ID to set the linked bank account.")));

        if (HAS_TRAIT(user, TRAIT_ID_APPRAISER))
        {
            Dm.append(ref dot, HAS_TRAIT(this, TRAIT_JOB_FIRST_ID_CARD) ? span_boldnotice(I18n.T("Hmm... yes, this ID was issued from Central Command!")) : span_boldnotice(I18n.T("This ID was created in this sector, not by Central Command.")));
            if (HAS_TRAIT(this, TRAIT_TASTEFULLY_THICK_ID_CARD) && (Dm.truthy(user.is_holding(this)) || (Dm.truthy(await IsReachableBy(user)) && Dm.truthy(await user.put_in_hands(this, ignore_animation: FALSE)))))
            {
                ADD_TRAIT(this, TRAIT_NODROP, "psycho");
                Dm.append(ref dot, span_hypnophrase(I18n.T("Look at that subtle coloring... The tasteful thickness of it. Oh my God, it even has a watermark...")));
                sound slowbeat = sound(Dm.resource("sound/effects/health/slowbeat.ogg"), repeat: TRUE);
                user.playsound_local(get_turf(this), slowbeat, 40, 0, channel: CHANNEL_HEARTBEAT, use_reverb: FALSE);
                if (isliving(user))
                {
                    mob_living living_user = (mob_living)user;
                    await living_user.adjust_jitter(10 * SECONDS);
                }
                _addtimer(Dm.@new<datum_callback>(this, nameof(drop_card), user), 10 * SECONDS, file: "code/game/objects/items/cards_ids.dm", line: 901);
            }
        }
        Dm.append(ref dot, span_notice(I18n.T("<i>There's more information below, you can look again to take a closer look...</i>")));
        return dot;
    }

    // tg: code/game/objects/items/cards_ids.dm:916  (M9 slice G)
    public override async DmTask<object> examine_more(mob user = null)
    {
        DmList dot = await base.examine_more(user) as DmList;
        if (!Dm.truthy(user.can_read(this)))
            return dot;

        Dm.append(ref dot, span_notice(I18n.T("<i>You examine [] closer, and note the following...</i>", this)));

        if (Dm.truthy(registered_age))
            Dm.append(ref dot, I18n.T("The card indicates that the holder is [] years old. []", registered_age, (registered_age < AGE_MINOR) ? I18n.T("There's a holographic stripe that reads <b>[]</b> along the bottom of the card.", span_danger(I18n.T("'MINOR: DO NOT SERVE ALCOHOL OR TOBACCO'"))) : ""));
        if (Dm.truthy(registered_account))
        {
            if (Dm.truthy(registered_account.mining_points))
                Dm.append(ref dot, I18n.T("There's [] mining point[] loaded onto the card's bank account.", registered_account.mining_points, registered_account.mining_points == 1 ? "" : "s"));
            Dm.append(ref dot, I18n.T("The account linked to the ID belongs to '[]' and reports a balance of [] [].", registered_account.account_holder, registered_account.account_balance, MONEY_SYMBOL));
            if (Dm.truthy(registered_account.account_debt))
                Dm.append(ref dot, span_warning(I18n.T("The account is currently indebted for [] []. []% of all earnings will go towards extinguishing it.", registered_account.account_debt, MONEY_SYMBOL, 100 * DEBT_COLLECTION_COEFF)));
            if (registered_account.account_job != null)
            {
                datum_bank_account D = SSeconomy.get_dep_account(registered_account.account_job.paycheck_department);
                if (D != null)
                    Dm.append(ref dot, I18n.T("The [] reports a balance of [] [].", D.account_holder, D.account_balance, MONEY_SYMBOL));
            }
            Dm.append(ref dot, span_info(I18n.T("Alt-Click the ID to pull money from the linked account in the form of holochips.")));
            Dm.append(ref dot, span_info(I18n.T("You can insert [] into the linked account by pressing holochips, cash, or coins against the ID.", MONEY_NAME)));
            if (Dm.truthy(registered_account.replaceable))
                Dm.append(ref dot, span_info(I18n.T("Alt-Right-Click the ID to change the linked bank account.")));
            if (registered_account.civilian_bounty != null)
            {
                Dm.append(ref dot, span_info(I18n.T("<b>There is an active civilian bounty.</b>")));
                Dm.append(ref dot, span_info(Dm.text("<i>[]</i>", registered_account.bounty_text())));
                Dm.append(ref dot, span_info(I18n.T("Quantity: []", registered_account.bounty_num())));
                Dm.append(ref dot, span_info(I18n.T("Reward: []", registered_account.bounty_value())));
            }
            if (Dm.eq(registered_account.account_holder, user.real_name))
                Dm.append(ref dot, span_boldnotice(I18n.T("If you lose this ID card, you can reclaim your account by Alt-Clicking a blank ID card while holding it and entering your account ID number.")));
        }
        else
            Dm.append(ref dot, span_info(I18n.T("There is no registered account linked to this card. Alt-Click to add one.")));

        return dot;
    }

    // tg: code/game/objects/items/cards_ids.dm:905
    public virtual async DmTask drop_card(mob user = null)
    {
        user.stop_sound_channel(CHANNEL_HEARTBEAT);
        REMOVE_TRAIT(this, TRAIT_NODROP, "psycho");
        if (Dm.truthy(user.is_holding(this)))
            await user.dropItemToGround(this);
        foreach (mob_living_carbon_human viewing_mob in Dm.each<mob_living_carbon_human>(viewers(2, user)))
        {
            if (IS_UNCONSCIOUS_OR_CRIT(viewing_mob) || viewing_mob == user)
                continue;
            await viewing_mob.say(I18n.T("Is something wrong? []... you're sweating.", first_name(user.name)), forced: "psycho");
            break;
        }
    }

    // tg: code/game/objects/items/cards_ids.dm:951
    public override object GetAccess()
    {
        DmList total_access = access.Copy();

        // Add all RETA temporary access from all departments - code/modules/reta/reta_system.dm
        foreach (object dept in Dm.each(reta_temp_access))
        {
            if (Dm.truthy(reta_temp_access[dept]))
                Dm.combine(ref total_access, reta_temp_access[dept]);
        }

        return total_access;
    }

    // tg: code/game/objects/items/cards_ids.dm:961
    public override object GetID()
    {
        return this;
    }

    // tg: code/game/objects/items/cards_ids.dm:964
    public override async DmTask<object> remove_id(params object[] __extra)
    {
        return this;
    }

    /// Called on COMSIG_ATOM_UPDATED_ICON. Updates the visuals of the wallet this card is in.
    // tg: code/game/objects/items/cards_ids.dm:968
    [SignalHandler]
    public virtual void update_in_wallet()
    {
        if (istype(loc, typeof(obj_item_storage_wallet)))
        {
            var powergaming = (obj_item_storage_wallet)loc;
            if (powergaming.front_id == this)
            {
                powergaming.update_label();
                powergaming.update_appearance();
            }
        }
    }

    /// Updates the name based on the card's vars and state.
    // tg: code/game/objects/items/cards_ids.dm:978
    public virtual void update_label()
    {
        object name_string;
        if (Dm.truthy(registered_name))
        {
            if (Dm.truthy(trim) && Dm.truthy(Dm.bitand(honorific_position, Dm.bitnot(HONORIFIC_POSITION_NONE))))
                name_string = I18n.T("[]'s ID Card", update_honorific());
            else
                name_string = I18n.T("[]'s ID Card", registered_name);
        }
        else
            name_string = Dm.initial(this, (atom p) => p.name);

        object assignment_string;

        if (Dm.truthy(is_intern))
        {
            if (Dm.truthy(assignment))
            {
                object intern_alt_name = (trim as datum_id_trim)?.intern_alt_name;
                assignment_string = Dm.truthy(intern_alt_name) ? intern_alt_name : I18n.T("Intern []", assignment);
            }
            else
                assignment_string = I18n.T("Intern");
        }
        else
            assignment_string = assignment;

        name = I18n.T("[] ([])", name_string, assignment_string);

        if (ishuman(loc))
        {
            mob_living_carbon_human human = loc as mob_living_carbon_human;
            human.update_visible_name();
        }
    }

    /// Re-generates the honorific title. Returns the compiled honorific_title value
    // tg: code/game/objects/items/cards_ids.dm:1005
    public virtual object update_honorific()
    {
        float sw__1 = honorific_position;
        if (sw__1 == HONORIFIC_POSITION_FIRST)
        {
            // first_name(registered_name) (__HELPERS/names.dm:379): a static regex helper, not declared; a card's honorific
            // is chosen with Ctrl-click (item_ctrl_click, M10), so no M4a card reaches this branch
            Dm.NotTranslated("code/game/objects/items/cards_ids.dm:1008", "update_honorific: first_name (__HELPERS/names.dm, with the honorific toggle, M10+)");
        }
        else if (sw__1 == HONORIFIC_POSITION_LAST)
            Dm.NotTranslated("code/game/objects/items/cards_ids.dm:1010", "update_honorific: last_name (__HELPERS/names.dm, with the honorific toggle, M10+)");
        else if (sw__1 == HONORIFIC_POSITION_FIRST_FULL)
            honorific_title = I18n.T("[] []", chosen_honorific, registered_name);
        else if (sw__1 == HONORIFIC_POSITION_LAST_FULL)
            honorific_title = I18n.T("[][]", registered_name, chosen_honorific);
        return honorific_title;
    }

    /// Returns the trim assignment name.
    // tg: code/game/objects/items/cards_ids.dm:1018
    public virtual object get_trim_assignment()
    {
        object trim_assignment = (trim as datum_id_trim)?.assignment;
        return Dm.truthy(trim_assignment) ? trim_assignment : assignment;
    }

    /// Returns the trim sechud icon state.
    // tg: code/game/objects/items/cards_ids.dm:1022
    public virtual object get_trim_sechud_icon()
    {
        object sechud_icon = (trim as datum_id_trim)?.sechud_icon;
        return Dm.truthy(sechud_icon) ? sechud_icon : Dm.resource("icons/mob/huds/hud.dmi"); // DEFAULT_HUDS_DMI (__DEFINES/atom_hud.dm:75), a resource expanded inline (rules 3.7)
    }

    /// Returns the trim sechud icon state.
    // tg: code/game/objects/items/cards_ids.dm:1026
    public virtual object get_trim_sechud_icon_state()
    {
        object sechud_icon_state = (trim as datum_id_trim)?.sechud_icon_state;
        return Dm.truthy(sechud_icon_state) ? sechud_icon_state : SECHUD_UNKNOWN;
    }

    // tg: code/game/objects/items/cards_ids.dm:1029
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (iscash(interacting_with))
            return Dm.truthy(insert_money(interacting_with as obj_item, user)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
        return NONE;
    }
}

// tg: code/game/objects/items/cards_ids.dm:1135 (gaps P4a: the departmental budget cards, the quartermaster's cargo card among them)
public partial class obj_item_card_id_departmental_budget : obj_item_card_id
{
    // tg: code/game/objects/items/cards_ids.dm:1143
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_bank_account department_account = SSeconomy?.get_dep_account(department_ID); // SSeconomy?: a harness world without Master (rules 10.6)
        if (department_account != null)
        {
            set_account(department_account);
            name = I18n.T("departmental card ([])", department_name);
            desc = I18n.T("Provides access to the [].", department_name);
        }
        if (SSeconomy != null)
            Dm.append(ref SSeconomy.dep_cards, this);
        return dot;
    }

    // tg: code/game/objects/items/cards_ids.dm:1152
    public override object Destroy(float? force = null)
    {
        SSeconomy?.dep_cards?.Remove(this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/cards_ids.dm:1156
    public override void update_label()
    {
        return;
    }

    // tg: code/game/objects/items/cards_ids.dm:1164
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await registered_account.bank_card_talk(span_warning(I18n.T("Withdrawing is not compatible with this card design.")), TRUE); //prevents the vault bank machine being useless and putting money from the budget to your card to go over personal crates
        return CLICK_ACTION_BLOCKING;
    }
}

/// An ID card capable of shapeshifting to other IDs given by the Agent ID card
// tg: code/game/objects/items/cards_ids.dm:1168
[DmPath("/obj/item/card/id/advanced")]
public partial class obj_item_card_id_advanced : obj_item_card_id
{
    // tg: code/game/objects/items/cards_ids.dm:1199
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ITEM_EQUIPPED, nameof(update_intern_status));
        RegisterSignal(this, COMSIG_ITEM_DROPPED, nameof(remove_intern_status));
        return dot;
    }

    // tg: code/game/objects/items/cards_ids.dm:1204
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(this, Dm.list(COMSIG_ITEM_EQUIPPED, COMSIG_ITEM_DROPPED));

        return base.Destroy(force);
    }

    // tg: code/game/objects/items/cards_ids.dm:1209
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        if (held_item is obj_item_toy_crayon)
        {
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Recolor ID");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/game/objects/items/cards_ids.dm:1215
    public virtual async DmTask<object> after_input_check(mob user = null)
    {
        if (QDELETED(user) || QDELETED(this) || user.client == null || !Dm.truthy(await user.can_perform_action(this, Dm.bitor(NEED_DEXTERITY, FORBID_TELEKINESIS_REACH))))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/items/cards_ids.dm:1220
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;

        if (tool is obj_item_toy_crayon)
            return await recolor_id(user, tool as obj_item_toy_crayon);
        return dot;
    }

    // tg: code/game/objects/items/cards_ids.dm:1228
    public virtual async DmTask<object> recolor_id(mob_living user = null, obj_item_toy_crayon our_crayon = null)
    {
        Dm.NotTranslated("code/game/objects/items/cards_ids.dm:1228", "recolor_id: recolouring the card with a crayon (the colour input, tgui, M10+)");
        return default;
    }

    // tg: code/game/objects/items/cards_ids.dm:1256
    public override void on_loc_equipped(datum source = null, mob equipper = null, object slot = null)
    {
        base.on_loc_equipped(source, equipper, slot);
        if (loc is obj_item_storage_wallet || loc is obj_item_modular_computer)
            update_intern_status(source, equipper, slot);
    }

    // tg: code/game/objects/items/cards_ids.dm:1261
    public override void on_loc_dropped(datum source = null, mob dropper = null)
    {
        base.on_loc_dropped(source, dropper);
        if (loc is obj_item_storage_wallet || loc is obj_item_modular_computer)
            remove_intern_status(source, dropper);
    }

    // tg: code/game/objects/items/cards_ids.dm:1266
    [SignalHandler]
    public virtual void update_intern_status(datum source = null, mob user = null, object slot = null)
    {
        if (!Dm.truthy(user?.client))
            return;
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_use_exp_tracking))))
            return;
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_use_low_living_hour_intern))))
            return;
        // SSdbcore.Connect(), the intern threshold and the client's living playtime (:1276-1289): the database and the
        // playtime tracking (M10); both config flags are off in tg's default config
        Dm.NotTranslated("code/game/objects/items/cards_ids.dm:1276", "update_intern_status: the living playtime from the database (SSdbcore, M10+)");
    }

    // tg: code/game/objects/items/cards_ids.dm:1292
    [SignalHandler]
    public virtual void remove_intern_status(datum source = null, mob user = null)
    {
        if (!Dm.truthy(is_intern))
            return;

        is_intern = FALSE;
        update_label();
    }

    // tg: code/game/objects/items/cards_ids.dm:1301
    public override void update_label()
    {
        if (Dm.truthy(inherent_assigned_name) && Dm.eq(registered_name, inherent_assigned_name))
        {
            name = I18n.T("[][]", Dm.initial(this, (atom p) => p.name), (!Dm.truthy(assignment) || Dm.eq(assignment, inherent_assigned_name)) ? "" : I18n.T(" ([])", assignment));
            return;
        }

        base.update_label();
    }

    // tg: code/game/objects/items/cards_ids.dm:1308
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (Dm.truthy(registered_name) && !Dm.eq(registered_name, "Captain"))
            Dm.append(ref dot, mutable_appearance(icon, assigned_icon_state));

        datum_id_trim card_trim = trim as datum_id_trim;
        object trim_icon_file = Dm.truthy(trim_icon_override) ? trim_icon_override : card_trim?.trim_icon;
        object trim_icon_state = Dm.truthy(trim_state_override) ? trim_state_override : card_trim?.trim_state;
        object trim_department_color = Dm.truthy(department_color_override) ? department_color_override : card_trim?.department_color;
        object trim_department_state = Dm.truthy(department_state_override) ? department_state_override : card_trim?.department_state;
        object trim_subdepartment_color = Dm.truthy(subdepartment_color_override) ? subdepartment_color_override : card_trim?.subdepartment_color;

        if (!Dm.truthy(trim_icon_file) || !Dm.truthy(trim_icon_state) || !Dm.truthy(trim_department_color) || !Dm.truthy(trim_subdepartment_color) || !Dm.truthy(trim_department_state))
            return dot;

        /// We handle department and subdepartment overlays first, so the job icon is always on top.
        mutable_appearance department_overlay = mutable_appearance(trim_icon_file, trim_department_state as string) as mutable_appearance;
        department_overlay.color = trim_department_color;
        Dm.append(ref dot, department_overlay);

        mutable_appearance subdepartment_overlay = mutable_appearance(trim_icon_file, "subdepartment") as mutable_appearance;
        subdepartment_overlay.color = trim_subdepartment_color;
        Dm.append(ref dot, subdepartment_overlay);

        /// Then we handle the job's icon here.
        Dm.append(ref dot, mutable_appearance(trim_icon_file, trim_icon_state as string));
        return dot;
    }

    // tg: code/game/objects/items/cards_ids.dm:1335
    public override object get_trim_assignment()
    {
        if (Dm.truthy(trim_assignment_override))
            return trim_assignment_override;

        if (ispath(trim))
        {
            datum_id_trim trim_singleton = SSid_access.trim_singletons_by_path[trim] as datum_id_trim;
            return trim_singleton.assignment;
        }

        return base.get_trim_assignment();
    }

    /// Returns the trim sechud icon state.
    // tg: code/game/objects/items/cards_ids.dm:1346
    public override object get_trim_sechud_icon_state()
    {
        return Dm.truthy(sechud_icon_state_override) ? sechud_icon_state_override : base.get_trim_sechud_icon_state();
    }
}

// tg: code/game/objects/items/cards_ids.dm:1380
public partial class obj_item_card_id_advanced_platinum : obj_item_card_id_advanced
{
    // tg: code/game/objects/items/cards_ids.dm:1388
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_TASTEFULLY_THICK_ID_CARD, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/game/objects/items/cards_ids.dm:1392
public partial class obj_item_card_id_advanced_gold : obj_item_card_id_advanced
{
    // tg: code/game/objects/items/cards_ids.dm:1400
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_TASTEFULLY_THICK_ID_CARD, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/game/objects/items/cards_ids.dm:1507
public partial class obj_item_card_id_advanced_debug : obj_item_card_id_advanced
{
    // tg: code/game/objects/items/cards_ids.dm:1515
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_account(Dm.@new<datum_bank_account>(Dm.named("player_account", FALSE)));
        registered_account.account_id = ADMIN_ACCOUNT_ID; // this is so bank_card_talk() can work.
        registered_account.account_job = SSjob?.get_job_type(typeof(datum_job_admin)) as datum_job; // SSjob?: as the card's own Initialize
        registered_account.account_balance += 999999; // MONEY! We add more money to the account every time we spawn because it's a debug item and infinite money whoopie
        return dot;
    }

    // tg: code/game/objects/items/cards_ids.dm:1522
    public override object alt_click_can_use_id(mob_living user = null)
    {
        object dot = base.alt_click_can_use_id(user);
        if (!Dm.truthy(dot) || user.client?.holder == null) // admins only as a safety so people don't steal all the dollars. spawn in a holochip if you want them to get some dosh
        {
            Waiting.Detach(registered_account.bank_card_talk(span_warning(I18n.T("Only authorized representatives of Nanotrasen may use this card.")), force: TRUE));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/game/objects/items/cards_ids.dm:1530
    public override object can_be_used_in_payment(mob_living user = null)
    {
        object dot = base.can_be_used_in_payment(user);
        if (!Dm.truthy(dot) || user.client?.holder == null)
        {
            Waiting.Detach(registered_account.bank_card_talk(span_warning(I18n.T("Only authorized representatives of Nanotrasen may use this card.")), force: TRUE));
            return FALSE;
        }

        return TRUE;
    }
}

// tg: code/game/objects/items/cards_ids.dm:1539
public partial class obj_item_card_id_advanced_prisoner : obj_item_card_id_advanced
{
    // tg: code/game/objects/items/cards_ids.dm:1566
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;

        if (isidcard(tool))
            return await set_sentence_time(user, tool as obj_item_card_id);
        return dot;
    }

    // tg: code/game/objects/items/cards_ids.dm:1574
    public virtual async DmTask<object> set_sentence_time(mob_living user = null, obj_item_card_id our_card = null)
    {
        DmList id_access = our_card.GetAccess() as DmList;
        if (!Dm.@in(ACCESS_BRIG, id_access))
        {
            balloon_alert(user, I18n.T("access denied!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(user.is_holding(this)))
        {
            to_chat(user, span_warning(I18n.T("You must be holding the ID to continue!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(timed)) // If we already have a time set, reset the card
        {
            timed = FALSE;
            time_to_assign = Dm.initial(this, (obj_item_card_id_advanced_prisoner p) => p.time_to_assign);
            registered_name = Dm.initial(this, (obj_item_card_id_advanced_prisoner p) => p.registered_name);
            STOP_PROCESSING(SSobj, this);
            to_chat(user, I18n.T("Resetting prisoner ID to default parameters."));
            return ITEM_INTERACT_SUCCESS;
        }

        object choice = await tgui_input_number(user, I18n.T("Sentence time in seconds"), I18n.T("Sentencing"));
        if (choice == null || QDELETED(user) || QDELETED(this) || !Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)) || !Dm.truthy(user.is_holding(this)))
            return ITEM_INTERACT_BLOCKING;
        time_to_assign = choice;
        to_chat(user, I18n.T("You set the sentence time to [].", DisplayTimeText(Dm.num(time_to_assign) * 10)));
        timed = TRUE;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/cards_ids.dm:1599
    public virtual async DmTask start_timer()
    {
        await say(I18n.T("Sentence started, welcome to the corporate rehabilitation center!"));
        START_PROCESSING(SSobj, this);
    }

    // tg: code/game/objects/items/cards_ids.dm:1603
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!Dm.truthy(dot))
            return dot;

        if (Dm.truthy(timed))
        {
            if (Dm.num(time_to_assign) > 0)
                dot.Add(span_notice(I18n.T("The digital timer on the card is set to []. The timer will start once the prisoner passes through the prison gate scanners.", DisplayTimeText(Dm.num(time_to_assign) * 10))));
            else if (Dm.num(time_left) <= 0)
                dot.Add(span_notice(I18n.T("The digital timer on the card has zero seconds remaining. You leave a changed man, but a free man nonetheless.")));
            else
                dot.Add(span_notice(I18n.T("The digital timer on the card has [] remaining. Don't do the crime if you can't do the time.", DisplayTimeText(Dm.num(time_left) * 10))));
        }

        dot.Add(span_notice(I18n.T("[] a security ID on the card to []set the genpop sentence time.", EXAMINE_HINT(I18n.T("Swipe")), Dm.truthy(timed) ? "re" : "")));
        dot.Add(span_notice(I18n.T("Remember to [] the card on a genpop locker to link it.", EXAMINE_HINT(I18n.T("swipe")))));
        return dot;
    }

    // tg: code/game/objects/items/cards_ids.dm:1619
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(timed))
            return null;
        time_left = Dm.num(time_left) - Dm.num(seconds_per_tick);
        if (Dm.num(time_left) <= 0)
        {
            Waiting.Detach(say(I18n.T("Sentence time has been served. Thank you for your cooperation in our corporate rehabilitation program!")));
            STOP_PROCESSING(SSobj, this);
        }
        return null;
    }

    // tg: code/game/objects/items/cards_ids.dm:1627
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        to_chat(usr, span_notice(I18n.T("You have accumulated [] out of the [] points you need for freedom.", points, goal)));
        return null;
    }
}

// tg: code/game/objects/items/cards_ids.dm:1680
public partial class obj_item_card_id_advanced_plainclothes : obj_item_card_id_advanced
{
    // tg: code/game/objects/items/cards_ids.dm:1690
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(trim_assignment_override))
            dot.Add(span_smallnotice(I18n.T("it's currently under plainclothes identity.")));
        else
            dot.Add(span_smallnotice(I18n.T("flip it to switch to the plainclothes identity.")));
        return dot;
    }

    // tg: code/game/objects/items/cards_ids.dm:1697
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object popup_input = await tgui_input_list(user, I18n.T("Choose Action"), I18n.T("Two-Sided ID"), Dm.list("Show", "Flip"));
        if (!Dm.truthy(popup_input) || !Dm.truthy(await after_input_check(user)))
            return TRUE;
        if (Dm.eq(popup_input, "Show"))
            return await base.attack_self(user, modifiers, __extra);
        balloon_alert(user, I18n.T("flipped"));
        if (Dm.truthy(trim_assignment_override))
            SSid_access.remove_trim_override(this);
        else
            SSid_access.apply_trim_override(this, alt_trim);
        update_label();
        update_appearance();
        return null;
    }

    // tg: code/game/objects/items/cards_ids.dm:1711
    public override void update_label()
    {
        if (!Dm.truthy(trim_assignment_override))
        {
            base.update_label();
            return;
        }
        object name_string = Dm.truthy(registered_name) ? I18n.T("[]'s ID Card", registered_name) : Dm.initial(this, (obj_item_card_id_advanced_plainclothes p) => p.name);
        datum_id_trim fake = SSid_access.trim_singletons_by_path[alt_trim] as datum_id_trim;
        name = Dm.text("[] ([])", name_string, fake.assignment);
    }
}

// The syndicate campaign G1b (D316): the agent card (the chameleon ID): its look and trim actions, the access theft from a card,
// a person or an item through its ChameleonCard window, the forging of its name, job and age, and the camera tracking it hides.
// tg: code/game/objects/items/cards_ids.dm:1719
public partial class obj_item_card_id_advanced_chameleon : obj_item_card_id_advanced
{
    // tg: code/game/objects/items/cards_ids.dm:1734
    public override object Destroy(float? force = null)
    {
        theft_target = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/cards_ids.dm:1738
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_ID)))
            RegisterSignal(user, COMSIG_LIVING_CAN_TRACK, nameof(can_track));
    }

    // tg: code/game/objects/items/cards_ids.dm:1743
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        UnregisterSignal(user, COMSIG_LIVING_CAN_TRACK);
        return await base.dropped(user, silent, __extra);
    }

    // tg: code/game/objects/items/cards_ids.dm:1747
    [SignalHandler]
    public virtual object can_track(datum source = null, mob user = null)
    {
        return COMPONENT_CANT_TRACK;
    }

    // tg: code/game/objects/items/cards_ids.dm:1752
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (interacting_with is obj_item_card_id) // isidcard (__DEFINES/is_helpers.dm)
        {
            theft_target = WEAKREF(interacting_with) as datum_weakref;
            await ui_interact(user);
            return ITEM_INTERACT_SUCCESS;
        }
        return await base.interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/game/objects/items/cards_ids.dm:1759
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        // If we're attacking a human, we want it to be covert. We're not ATTACKING them, we're trying
        // to sneakily steal their accesses by swiping our agent ID card near them. As a result, we
        // return ITEM_INTERACT_BLOCKING to cancel any part of the following the attack chain.
        if (interacting_with is mob_living_carbon_human human_target)
        {
            interacting_with.balloon_alert(user, "scanning ID card...");

            if (!Dm.truthy(await do_after(user, 2 * SECONDS, interacting_with, cog_icon: null)))
            {
                interacting_with.balloon_alert(user, "interrupted!");
                return ITEM_INTERACT_BLOCKING;
            }

            DmList target_id_cards = human_target.get_all_contents_type(typeof(obj_item_card_id)) as DmList;

            if (Dm.length(target_id_cards) == 0)
            {
                interacting_with.balloon_alert(user, "no IDs!");
                return ITEM_INTERACT_BLOCKING;
            }

            object selected_id = pick(target_id_cards);
            interacting_with.balloon_alert(user, "IDs synced");
            theft_target = WEAKREF(selected_id as datum) as datum_weakref;
            await ui_interact(user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (interacting_with is obj_item target_item)
        {
            interacting_with.balloon_alert(user, "scanning ID card...");

            DmList target_id_cards = target_item.get_all_contents_type(typeof(obj_item_card_id)) as DmList;
            object target_item_id = target_item.GetID();

            if (Dm.truthy(target_item_id))
                Dm.combine(ref target_id_cards, target_item_id);

            if (Dm.length(target_id_cards) == 0)
            {
                interacting_with.balloon_alert(user, "no IDs!");
                return ITEM_INTERACT_BLOCKING;
            }

            object selected_id = pick(target_id_cards);
            interacting_with.balloon_alert(user, "IDs synced");
            theft_target = WEAKREF(selected_id as datum) as datum_weakref;
            await ui_interact(user);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/objects/items/cards_ids.dm:1806
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "ChameleonCard", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/cards_ids.dm:1812
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["wildcardFlags"] = SSid_access.wildcard_flags_by_wildcard;
        data["accessFlagNames"] = SSid_access.access_flag_string_by_flag;
        data["accessFlags"] = SSid_access.flags_by_access;
        return data;
    }

    // tg: code/game/objects/items/cards_ids.dm:1819
    public override object ui_host(mob user = null)
    {
        // Hook our UI to the theft target ID card for UI state checks.
        return theft_target?.resolve();
    }

    // tg: code/game/objects/items/cards_ids.dm:1823
    public override object ui_state(mob user = null)
    {
        return GLOB.always_state;
    }

    // tg: code/game/objects/items/cards_ids.dm:1826
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        atom target = theft_target?.resolve() as atom;

        if (target == null)
            return UI_CLOSE;

        float status = Math.Min(Math.Min(
            Dm.num(ui_status_user_strictly_adjacent(user, target)),
            Dm.num(ui_status_user_is_advanced_tool_user(user))),
            Math.Max(
                Dm.num(ui_status_user_is_conscious_and_lying_down(user)),
                Dm.num(ui_status_user_is_abled(user, target))));

        if (status < UI_INTERACTIVE)
            return UI_CLOSE;

        return status;
    }

    // tg: code/game/objects/items/cards_ids.dm:1846
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["showBasic"] = FALSE;

        DmList regions = Dm.list();

        obj_item_card_id target_card = theft_target.resolve() as obj_item_card_id;
        if (target_card != null)
        {
            DmList tgui_region_data = SSid_access.all_region_access_tgui;
            foreach (object region in Dm.each(SSid_access.station_regions))
                Dm.append(regions, tgui_region_data[region]);
        }

        data["accesses"] = regions;
        data["ourAccess"] = access;
        data["ourTrimAccess"] = trim is datum_id_trim our_trim ? our_trim.access : Dm.list();
        data["theftAccess"] = target_card.access.Copy();
        data["wildcardSlots"] = wildcard_slots;
        data["selectedList"] = access;
        data["trimAccess"] = Dm.list();

        return data;
    }

    // tg: code/game/objects/items/cards_ids.dm:1869
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        obj_item_card_id target_card = theft_target?.resolve() as obj_item_card_id;
        if (QDELETED(target_card))
        {
            to_chat(usr, span_notice(I18n.T("The ID card you were attempting to scan is no longer in range.")));
            target_card = null;
            return TRUE;
        }

        // Wireless ID theft!
        turf our_turf = get_turf(this);
        turf target_turf = get_turf(target_card);
        if (!Dm.truthy(await our_turf.Adjacent(target_turf)))
        {
            to_chat(usr, span_notice(I18n.T("The ID card you were attempting to scan is no longer in range.")));
            target_card = null;
            return TRUE;
        }

        if (Dm.eq(action, "mod_access"))
        {
            object access_type = @params["access_target"];
            object try_wildcard = @params["access_wildcard"];
            if (Dm.@in(access_type, access))
            {
                remove_access(Dm.list(access_type));
                LOG_ID_ACCESS_CHANGE(usr, this, Dm.text("removed []", SSid_access.get_access_desc(access_type)));
                return TRUE;
            }

            if (!Dm.@in(access_type, target_card.access))
            {
                to_chat(usr, span_notice(I18n.T("ID error: ID card rejected your attempted access modification.")));
                LOG_ID_ACCESS_CHANGE(usr, this, Dm.text("failed to add [][]", SSid_access.get_access_desc(access_type), Dm.truthy(try_wildcard) ? Dm.text(" with wildcard []", try_wildcard) : ""));
                return TRUE;
            }

            if (!Dm.truthy(can_add_wildcards(Dm.list(access_type), try_wildcard)))
            {
                to_chat(usr, span_notice(I18n.T("ID error: ID card rejected your attempted access modification.")));
                LOG_ID_ACCESS_CHANGE(usr, this, Dm.text("failed to add [][]", SSid_access.get_access_desc(access_type), Dm.truthy(try_wildcard) ? Dm.text(" with wildcard []", try_wildcard) : ""));
                return TRUE;
            }

            if (!Dm.truthy(add_access(Dm.list(access_type), try_wildcard)))
            {
                to_chat(usr, span_notice(I18n.T("ID error: ID card rejected your attempted access modification.")));
                LOG_ID_ACCESS_CHANGE(usr, this, Dm.text("failed to add [][]", SSid_access.get_access_desc(access_type), Dm.truthy(try_wildcard) ? Dm.text(" with wildcard []", try_wildcard) : ""));
                return TRUE;
            }

            if (Dm.@in(access_type, Dm.list(ACCESS_CHANGE_IDS))) // ACCESS_ALERT_ADMINS (__DEFINES/access.dm:213), inline
                message_admins(Dm.text("[] just added [] to an ID card [] []", ADMIN_LOOKUPFLW(usr), SSid_access.get_access_desc(access_type), ADMIN_VV(this), Dm.truthy(registered_name) ? Dm.text("belonging to [].", registered_name) : "with no registered name."));
            LOG_ID_ACCESS_CHANGE(usr, this, Dm.text("added []", SSid_access.get_access_desc(access_type)));
            return TRUE;
        }
        return null;
    }

    // tg: code/game/objects/items/cards_ids.dm:1917
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(await user.can_perform_action(user, Dm.bitor(NEED_DEXTERITY, FORBID_TELEKINESIS_REACH))))
            return await base.attack_self(user, modifiers, __extra);
        object popup_input = await tgui_input_list(user, "Choose Action", "Agent ID", Dm.list("Show", "Forge/Reset", "Change Account ID"));
        if (!Dm.truthy(popup_input) || !Dm.truthy(await after_input_check(user)))
            return TRUE;
        if (Dm.eq(popup_input, "Change Account ID"))
        {
            await set_new_account(user as mob_living);
            return null;
        }
        if (Dm.eq(popup_input, "Show"))
            return await base.attack_self(user, modifiers, __extra);

        ///"Forge/Reset", kept outside the switch() statement to reduce indentation.
        if (Dm.truthy(forged)) //reset the ID if forged
        {
            registered_name = Dm.initial(this, (obj_item_card_id p) => p.registered_name);
            assignment = Dm.initial(this, (obj_item_card_id p) => p.assignment);
            SSid_access.remove_trim_override(this);
            REMOVE_TRAIT(this, TRAIT_MAGNETIC_ID_CARD, CHAMELEON_ITEM_TRAIT);
            user.log_message(Dm.text("reset \\the [] named \"[]\" to default.", Dm.initial(this, (atom p) => p.name), this), LOG_GAME);
            update_label();
            update_appearance();
            forged = FALSE;
            to_chat(user, span_notice(I18n.T("You successfully reset the ID card.")));
            return null;
        }

        ///forge the ID if not forged.s
        object input_name = await tgui_input_text(user, "What name would you like to put on this card? Leave blank to randomise.", "Agent card name", Dm.truthy(registered_name) ? registered_name : (user is mob_living_carbon_human ? user.real_name : user.name), max_length: MAX_NAME_LEN, encode: FALSE);

        if (!Dm.truthy(await after_input_check(user)))
            return TRUE;
        if (Dm.truthy(input_name))
            input_name = await sanitize_name(input_name, allow_numbers: TRUE);
        if (!Dm.truthy(input_name))
        {
            // Invalid/blank names give a randomly generated one.
            if (Dm.eq(user.gender, MALE))
                input_name = Dm.text("[] []", pick(GLOB.first_names_male), pick(GLOB.last_names));
            else if (Dm.eq(user.gender, FEMALE))
                input_name = Dm.text("[] []", pick(GLOB.first_names_female), pick(GLOB.last_names));
            else
                input_name = Dm.text("[] []", pick(GLOB.first_names), pick(GLOB.last_names));
        }

        object target_occupation = await tgui_input_text(user, "What occupation would you like to put on this card?\nNote: This will not grant any access levels.", "Agent card job assignment", Dm.truthy(assignment) ? assignment : "Assistant", max_length: MAX_NAME_LEN);
        if (!Dm.truthy(await after_input_check(user)))
            return TRUE;
        float default_age = AGE_MIN;
        if (user is mob_living_carbon_human human_user)
            default_age = Dm.truthy(human_user.age) ? Math.Clamp(Dm.num(human_user.age), AGE_MIN, AGE_MAX) : AGE_MIN;
        object new_age = await tgui_input_number(user, "Choose the ID's age", "Agent card age", default_age, AGE_MAX, AGE_MIN);
        if (!Dm.truthy(await after_input_check(user)))
            return TRUE;

        object wallet_spoofing = await tgui_alert(user, "Activate wallet ID spoofing, allowing this card to force itself to occupy the visible ID slot in wallets?", "Wallet ID Spoofing", Dm.list("Yes", "No"));
        if (!Dm.truthy(await after_input_check(user)))
            return null;

        registered_name = input_name;
        if (Dm.truthy(target_occupation))
            assignment = sanitize(target_occupation);
        if (Dm.truthy(new_age))
            registered_age = Dm.num(new_age);
        if (Dm.eq(wallet_spoofing, "Yes"))
            ADD_TRAIT(this, TRAIT_MAGNETIC_ID_CARD, CHAMELEON_ITEM_TRAIT);

        update_label();
        update_appearance();
        forged = TRUE;
        to_chat(user, span_notice(I18n.T("You successfully forge the ID card.")));
        user.log_message(Dm.text("forged \\the [] with name \"[]\", occupation \"[]\" and trim \"[]\".", Dm.initial(this, (atom p) => p.name), registered_name, assignment, (trim as datum_id_trim)?.assignment), LOG_GAME);

        if (!ishuman(user) || registered_account != null)
            return null;

        var owner = (mob_living_carbon_human)user;
        var account = SSeconomy.bank_accounts_by_id[Dm.text("[]", owner.account_id)] as datum_bank_account;
        if (account != null)
        {
            set_account(account);
            to_chat(user, span_notice(I18n.T("Your account number has been automatically assigned.")));
        }
        return null;
    }

    // tg: code/game/objects/items/cards_ids.dm:1997
    public override object add_item_context(obj_item source = null, DmList context = null, atom target = null, mob_living user = null)
    {
        object dot = base.add_item_context(source, context, target, user);

        if (!Dm.truthy(in_range(user, target)))
            return dot;
        if (target is obj_item_card_id)
        {
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Copy access");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (target is mob_living_carbon_human)
        {
            context[SCREENTIP_CONTEXT_RMB] = I18n.T("Copy access");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (target is obj_item)
        {
            context[SCREENTIP_CONTEXT_RMB] = I18n.T("Scan for access");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }
}

// tg: code/game/objects/items/cards_ids.dm:2060  #define INDEX_NAME_COLOR 1 (file-local, #undef at :2211)
// tg: code/game/objects/items/cards_ids.dm:2061  #define INDEX_ASSIGNMENT_COLOR 2 (file-local, #undef at :2212)
// tg: code/game/objects/items/cards_ids.dm:2062  #define INDEX_TRIM_COLOR 3 (file-local, #undef at :2213)

/**
 * A fake ID card any silly-willy can craft with wirecutters, cardboard and a writing utensil
 * Beside the gimmick of changing the visible name when worn, they do nothing. They cannot have an account.
 * They don't fit in PDAs nor wallets, They have no access. They won't trick securitrons. They won't work with chameleon masks.
 * Etcetera etcetera. Furthermore, talking, or getting examined on will pretty much give it away.
 */
// tg: code/game/objects/items/cards_ids.dm:2071 (gaps RD4: the relic's disguise hands one out; the card's procs, the var block is generated)
public partial class obj_item_card_cardboard : obj_item_card
{
    private const int INDEX_NAME_COLOR = 1;
    private const int INDEX_ASSIGNMENT_COLOR = 2;
    private const int INDEX_TRIM_COLOR = 3;

    // tg: code/game/objects/items/cards_ids.dm:2091
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    // tg: code/game/objects/items/cards_ids.dm:2095
    public override object get_displayed_name(float? honorifics = null)
    {
        return scribbled_name;
    }

    // tg: code/game/objects/items/cards_ids.dm:2098
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(await user.can_write(tool, TRUE)))
        {
            Waiting.Detach(modify_card(user, tool)); // INVOKE_ASYNC(src, PROC_REF(modify_card), user, tool)
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }

    ///Lets the user write a name, assignment or trim on the card, or reset it. Only the name is important for the component.
    // tg: code/game/objects/items/cards_ids.dm:2104
    public virtual async DmTask modify_card(mob_living user = null, obj_item item = null)
    {
        if (user.mind == null)
            return;
        object popup_input = await tgui_input_list(user, I18n.T("What To Change"), I18n.T("Cardboard ID"), Dm.list("Name", "Assignment", "Trim", "Reset"));
        if (!Dm.truthy(await after_input_check(user, item, popup_input)))
            return;
        object sw__1 = popup_input;
        if (Dm.eq(sw__1, "Name"))
        {
            object raw_input = await tgui_input_text(user, I18n.T("What name would you like to put on this card?"), I18n.T("Cardboard card name"), Dm.truthy(scribbled_name) ? scribbled_name : (ishuman(user) ? user.real_name : user.name), max_length: MAX_NAME_LEN);
            object input_name = await sanitize_name(raw_input, allow_numbers: TRUE);
            if (!Dm.truthy(await after_input_check(user, item, input_name, scribbled_name)))
                return;
            playsound(this, SFX_WRITING_PEN, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, SOUND_FALLOFF_EXPONENT + 3, ignore_walls: FALSE);
            scribbled_name = input_name;
            DmList details = await item.get_writing_implement_details() as DmList;
            details_colors[INDEX_NAME_COLOR] = Dm.truthy(details?["color"]) ? details["color"] : COLOR_BLACK;
        }
        else if (Dm.eq(sw__1, "Assignment"))
        {
            object input_assignment = await tgui_input_text(user, I18n.T("What assignment would you like to put on this card?"), I18n.T("Cardboard card job ssignment"), Dm.truthy(scribbled_assignment) ? scribbled_assignment : "Assistant", max_length: MAX_NAME_LEN);
            if (!Dm.truthy(await after_input_check(user, item, input_assignment, scribbled_assignment)))
                return;
            playsound(this, SFX_WRITING_PEN, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, SOUND_FALLOFF_EXPONENT + 3, ignore_walls: FALSE);
            scribbled_assignment = sanitize(input_assignment);
            DmList details = await item.get_writing_implement_details() as DmList;
            details_colors[INDEX_ASSIGNMENT_COLOR] = Dm.truthy(details?["color"]) ? details["color"] : COLOR_BLACK;
        }
        else if (Dm.eq(sw__1, "Trim"))
        {
            DmList possible_trims = TgState.Current.obj_item_card_cardboard__modify_card__possible_trims; // var/static/list/possible_trims
            if (possible_trims == null)
            {
                possible_trims = TgState.Current.obj_item_card_cardboard__modify_card__possible_trims = Dm.list();
                foreach (object trim_path in Dm.each(typesof(typeof(datum_id_trim))))
                {
                    var trim = SSid_access.trim_singletons_by_path[trim_path] as datum_id_trim;
                    if (Dm.truthy(trim?.trim_state) && Dm.truthy(trim.assignment))
                        Dm.combine(ref possible_trims, replacetext(trim.trim_state, "trim_", ""));
                }
                sortTim(possible_trims, "/proc/" + nameof(cmp_typepaths_asc));
            }
            object input_trim = await tgui_input_list(user, I18n.T("Select trim to apply to your card.\nNote: This will not grant any trim accesses."), I18n.T("Forge Trim"), possible_trims);
            if (!Dm.truthy(input_trim) || !Dm.truthy(await after_input_check(user, item, input_trim, scribbled_trim)))
                return;
            playsound(this, SFX_WRITING_PEN, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, SOUND_FALLOFF_EXPONENT + 3, ignore_walls: FALSE);
            scribbled_trim = Dm.text("cardboard_[]", input_trim);
            DmList details = await item.get_writing_implement_details() as DmList;
            details_colors[INDEX_TRIM_COLOR] = Dm.truthy(details?["color"]) ? details["color"] : COLOR_BLACK;
        }
        else if (Dm.eq(sw__1, "Reset"))
        {
            scribbled_name = null;
            scribbled_assignment = null;
            scribbled_trim = null;
            details_colors = Dm.list(COLOR_BLACK, COLOR_BLACK, COLOR_BLACK);
        }

        update_appearance();
    }

    ///Checks that the conditions to be able to modify the cardboard card are still present after user input calls.
    // tg: code/game/objects/items/cards_ids.dm:2153
    public virtual async DmTask<object> after_input_check(mob_living user = null, obj_item item = null, object input = null, object value = null)
    {
        if (!Dm.truthy(input) || (Dm.truthy(value) && Dm.eq(input, value)))
            return FALSE;
        if (QDELETED(user) || QDELETED(item) || QDELETED(this) || Dm.truthy(user.incapacitated) || !Dm.truthy(user.is_holding(item)) || !Dm.truthy(await IsReachableBy(user)) || !Dm.truthy(await user.can_write(item)))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/items/cards_ids.dm:2160
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(await Adjacent(user)))
            return null;
        // tg's icon2html(src, viewers(user)) puts the card's picture in the chat line: the 3D client's chat is text, so the line
        // keeps only the card's name
        user.visible_message(span_notice(I18n.T("[] shows you: [].", user, name)), span_notice(I18n.T("You show \\the [].", name)));
        add_fingerprint(user);
        return null;
    }

    // tg: code/game/objects/items/cards_ids.dm:2166
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        if (!Dm.truthy(scribbled_name))
        {
            name = Dm.initial(typeof(obj_item_card_cardboard), (obj_item_card_cardboard c) => c.name);
            return dot;
        }
        name = I18n.T("[]'s ID Card ([])", scribbled_name, scribbled_assignment);
        return dot;
    }

    // tg: code/game/objects/items/cards_ids.dm:2173
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(scribbled_name))
        {
            var name_overlay = mutable_appearance(icon, "cardboard_name") as mutable_appearance;
            name_overlay.color = details_colors[INDEX_NAME_COLOR];
            Dm.append(ref dot, name_overlay);
        }
        if (Dm.truthy(scribbled_assignment))
        {
            var assignment_overlay = mutable_appearance(icon, "cardboard_assignment") as mutable_appearance;
            assignment_overlay.color = details_colors[INDEX_ASSIGNMENT_COLOR];
            Dm.append(ref dot, assignment_overlay);
        }
        if (Dm.truthy(scribbled_trim))
        {
            var frame_overlay = mutable_appearance(icon, "cardboard_frame") as mutable_appearance;
            frame_overlay.color = details_colors[INDEX_TRIM_COLOR];
            Dm.append(ref dot, frame_overlay);
            var trim_overlay = mutable_appearance(icon, Dm.str(scribbled_trim)) as mutable_appearance;
            trim_overlay.color = details_colors[INDEX_TRIM_COLOR];
            Dm.append(ref dot, trim_overlay);
        }
        return dot;
    }

    // tg: code/game/objects/items/cards_ids.dm:2191
    public override object get_id_examine_strings(mob user = null)
    {
        object dot = base.get_id_examine_strings(user);
        // . += list("[icon2html(get_cached_flat_icon(), user, extra_classes = "hugeicon")]"): the card's picture in the chat line
        Dm.Deferred("code/game/objects/items/cards_ids.dm:2193", "get_id_examine_strings: icon2html of the cardboard card's flat icon (the 3D client's chat is text: the chat icons)");
        return dot;
    }

    // tg: code/game/objects/items/cards_ids.dm:2195
    public override object get_examine_icon(mob user = null)
    {
        Dm.Deferred("code/game/objects/items/cards_ids.dm:2196", "get_examine_icon: icon2html of the cardboard card's flat icon (the 3D client's chat is text: the chat icons)");
        return null;
    }

    // tg: code/game/objects/items/cards_ids.dm:2198
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("You could use a pen or crayon to forge a name, assignment or trim.")));
        return dot;
    }

    // tg: code/game/objects/items/cards_ids.dm:2202
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        if (held_item == null || held_item == this)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Show ID";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        else if (IS_WRITING_UTENSIL(held_item))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Modify";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/cards_ids.dm:83  var/static/list/available_logos
    internal DmList obj_item_card_id__available_logos;
    // tg: code/game/objects/items/cards_ids.dm:2128  var/static/list/possible_trims (modify_card)
    internal DmList obj_item_card_cardboard__modify_card__possible_trims;
}

// Gaps OD1 (D333): the card's suicide, the ID's fantasy bonuses, its var edits and its honorific picker.
// tg: code/game/objects/items/cards_ids.dm:21
public partial class obj_item_card : obj_item
{
    // tg: code/game/objects/items/cards_ids.dm:38
    public override DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins to swipe [] neck with \\the []! It looks like [] trying to commit suicide!", user, user.p_their(), this, user.p_theyre())));
        return DmTask<object>.FromResult(BRUTELOSS);
    }
}

// tg: code/game/objects/items/cards_ids.dm:78
public partial class obj_item_card_id : obj_item_card
{
    // tg: code/game/objects/items/cards_ids.dm:134
    public override async DmTask apply_fantasy_bonuses(object bonus = null)
    {
        await base.apply_fantasy_bonuses(bonus);
        if (Dm.num(bonus) >= 15)
            add_access(SSid_access.get_region_access_list(Dm.list(REGION_ALL_GLOBAL)) as DmList, mode: FORCE_ADD_ALL);
        else if (Dm.num(bonus) >= 10)
            add_access(SSid_access.get_region_access_list(Dm.list(REGION_ALL_STATION)) as DmList, mode: FORCE_ADD_ALL);
        else if (Dm.num(bonus) <= -10)
            clear_access();
    }

    // tg: code/game/objects/items/cards_ids.dm:704
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        object dot = await base.vv_edit_var(var_name, var_value);
        if (Dm.truthy(dot))
        {
            switch (Dm.str(var_name))
            {
                case nameof(assignment):
                case nameof(registered_name):
                case nameof(registered_age):
                    update_label();
                    update_appearance();
                    break;
                case nameof(trim):
                    if (ispath(trim))
                        await SSid_access.apply_trim_to_card(this, trim);
                    break;
            }
        }
        return dot;
    }

    // tg: code/game/objects/items/cards_ids.dm:1034
    public override async DmTask<object> item_ctrl_click(mob user = null)
    {
        if (!Dm.truthy(in_contents_of(user)) || Dm.truthy(user.incapacitated)) //Check if the ID is in the ID slot, so it can be changed from there too.
            return null;

        if (!Dm.truthy(trim))
        {
            balloon_alert(user, I18n.T("card has no trim!"));
            return null;
        }

        datum_id_trim id_trim = trim as datum_id_trim;
        if (!Dm.truthy(length(id_trim.honorifics)))
        {
            balloon_alert(user, I18n.T("card has no honorific to use!"));
            return null;
        }

        DmList choices = Dm.list();
        // HONORIFIC_POSITION_BITFIELDS() (__DEFINES/id_cards.dm:63, a list macro expanded inline, rules 11.1 (f))
        DmList readable_names = Dm.list(
            Dm.kv("Honorific + First Name", HONORIFIC_POSITION_FIRST),
            Dm.kv("Honorific + Last Name", HONORIFIC_POSITION_LAST),
            Dm.kv("Honorific + Full Name", HONORIFIC_POSITION_FIRST_FULL),
            Dm.kv("Full Name + Honorific", HONORIFIC_POSITION_LAST_FULL),
            Dm.kv("Disable Honorific", HONORIFIC_POSITION_NONE));
        foreach (object i in Dm.each(readable_names)) //Filter out the options you don't have on your ID.
        {
            if (Dm.truthy(Dm.bitand(id_trim.honorific_positions, readable_names[i]))) //If the positions list has the same bit value as the readable list.
                Dm.append(ref choices, i);
        }

        object chosen_position = await tgui_input_list(user, I18n.T("What position do you want your honorific in?"), I18n.T("Flair!"), choices);
        if (Dm.truthy(user.incapacitated) || !Dm.truthy(in_contents_of(user)))
            return null;
        object honorific_position_to_use = readable_names[chosen_position];

        honorific_position = Dm.initial(this, (obj_item_card_id c) => c.honorific_position); //In case you want to force an honorific on an ID, set a default that won't always be NONE.
        honorific_title = null; //We reset this regardless so that we don't stack titles on accident.

        if (Dm.truthy(Dm.bitand(honorific_position_to_use, HONORIFIC_POSITION_NONE)))
            balloon_alert(user, I18n.T("honorific disabled"));
        else
        {
            object new_honorific = await tgui_input_list(user, I18n.T("What honorific do you want to use?"), I18n.T("Flair!!!"), id_trim.honorifics);
            if (!Dm.truthy(new_honorific) || Dm.truthy(user.incapacitated) || !Dm.truthy(in_contents_of(user)))
                return null;
            chosen_honorific = new_honorific;
            switch (Dm.num(honorific_position_to_use))
            {
                case HONORIFIC_POSITION_FIRST:
                    honorific_position = HONORIFIC_POSITION_FIRST;
                    balloon_alert(user, I18n.T("honorific set: display first name"));
                    break;
                case HONORIFIC_POSITION_LAST:
                    honorific_position = HONORIFIC_POSITION_LAST;
                    balloon_alert(user, I18n.T("honorific set: display last name"));
                    break;
                case HONORIFIC_POSITION_FIRST_FULL:
                    honorific_position = HONORIFIC_POSITION_FIRST_FULL;
                    balloon_alert(user, I18n.T("honorific set: start of full name"));
                    break;
                case HONORIFIC_POSITION_LAST_FULL:
                    honorific_position = HONORIFIC_POSITION_LAST_FULL;
                    balloon_alert(user, I18n.T("honorific set: end of full name"));
                    break;
            }
        }

        update_label();
        return null;
    }
}
