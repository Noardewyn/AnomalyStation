// Translated from tgstation (snapshot 1d99bd7f): code/modules/vending/vendor/interaction.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package S2: the whole file; the cargo account's half of the restock reward came with the economy (gaps P4a). Deferred leaf: the part replacer's
// effect (the RPED is inert, D132).

// tg: code/modules/vending/vendor/_vending.dm:37
public partial class obj_machinery_vending
{
    //===============================HAND INTERACTION===================================
    // tg: code/modules/vending/vendor/interaction.dm:2
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        if (seconds_electrified != 0 && !Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            if (Dm.truthy(await shock__proc(user as mob_living, 100)))
                return null;
        }

        if (Dm.truthy(tilted) && !Dm.truthy(user.buckled))
        {
            to_chat(user, span_notice(I18n.T("You begin righting [].", this)));
            if (Dm.truthy(await do_after(user, 5 * SECONDS, target: this)))
                await untilt(user);
            return null;
        }

        return await base.interact(user, __extra);
    }

    //================================TOOL ACTS==============================================
    // tg: code/modules/vending/vendor/interaction.dm:16
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(component_parts))
            return ITEM_INTERACT_FAILURE;
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/vending/vendor/interaction.dm:21
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = NONE;
        if (!Dm.truthy(panel_open))
            return ITEM_INTERACT_FAILURE;
        if (Dm.truthy(await default_unfasten_wrench(user, tool, time: 6 * SECONDS)))
        {
            unbuckle_all_mobs(TRUE);
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/modules/vending/vendor/interaction.dm:29
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(anchored))
            return default_deconstruction_screwdriver(user, tool);
        to_chat(user, span_warning(I18n.T("You must first secure [].", this)));
        return ITEM_INTERACT_FAILURE;
    }

    // tg: code/modules/vending/vendor/interaction.dm:35
    public override void on_set_panel_open(object old_value = null)
    {
        update_appearance(UPDATE_OVERLAYS);
    }

    //=======================================RESTOCKING==========================================
    /**
     * Is the passed in user allowed to load this vending machines compartments? This only is ran if we are using a /obj/item/storage/bag to load the vending machine, and not a dedicated restocker.
     *
     * Arguments:
     * * user - mob that is doing the loading of the vending machine
     */
    // tg: code/modules/vending/vendor/interaction.dm:45
    [DmProtected]
    public virtual async DmTask<object> compartmentLoadAccessCheck(mob user = null)
    {
        return !Dm.truthy(req_access) || Dm.truthy(await allowed__proc(user)) || Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) || !Dm.truthy(scan_id) ? TRUE : FALSE;
    }

    /**
     * Are we able to load the item passed in
     *
     * Arguments:
     * * loaded_item - the item being loaded
     * * user - the user doing the loading
     */
    // tg: code/modules/vending/vendor/interaction.dm:57
    [DmProtected]
    public virtual object canLoadItem(obj_item loaded_item = null, mob user = null, float? send_message = null)
    {
        send_message ??= TRUE;
        Type loaded_type = loaded_item.GetType();
        if (!Dm.truthy(length(loaded_item.contents)) && (Dm.truthy(products[loaded_type]) || Dm.truthy(premium[loaded_type]) || Dm.truthy(contraband[loaded_type])))
            return TRUE;
        if (Dm.truthy(send_message))
            to_chat(user, span_warning(I18n.T("[] does not accept []!", this, loaded_item)));
        return FALSE;
    }

    /**
     * Tries to insert the item into the vendor, and depending on whether the product is a part of the vendor's
     * stock or not, increments an already present product entry's available amount or creates a new entry.
     * arguments:
     * inserted_item - the item we're trying to insert
     * user - mob who's trying to insert the item
     */
    // tg: code/modules/vending/vendor/interaction.dm:74
    [DmProtected]
    public virtual async DmTask<object> loadingAttempt(obj_item inserted_item = null, mob user = null)
    {
        object dot = TRUE;
        if (!Dm.truthy(canLoadItem(inserted_item, user)))
            return FALSE;

        foreach (datum_data_vending_product product_datum in Dm.each<datum_data_vending_product>(Dm.add(Dm.add(product_records, coin_records), hidden_records)))
        {
            if (!Dm.eq(inserted_item.GetType(), product_datum.product_path))
                continue;

            if (Dm.num(product_datum.amount) == product_datum.max_amount)
            {
                to_chat(user, span_warning(I18n.T("[] can't accept any more [][]!", this, inserted_item.name, inserted_item.p_s())));
                return FALSE;
            }

            if (!Dm.truthy(await user.transferItemToLoc(inserted_item, this)))
            {
                to_chat(user, span_warning(I18n.T("[] is stuck in your hand!", inserted_item)));
                return FALSE;
            }

            product_datum.amount++;
            LAZYADD(ref product_datum.returned_products, inserted_item);
            to_chat(user, span_notice(I18n.T("You insert [] into []'s input compartment.", inserted_item, this)));
            break;
        }
        return dot;
    }

    // tg: code/modules/vending/vendor/interaction.dm:98
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        obj_item attack_item = tool;
        object dot = await base.item_interaction(user, attack_item, modifiers, __extra);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;

        if (Dm.truthy(panel_open) && Dm.truthy(is_wire_tool(attack_item)))
        {
            await wires.interact(user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (refill_canister != null && istype(attack_item, refill_canister))
        {
            dot = ITEM_INTERACT_FAILURE;
            if (!Dm.truthy(panel_open))
                to_chat(user, span_warning("You should probably unscrew the service panel first!"));
            else if (!Dm.truthy(is_operational))
                to_chat(user, span_warning(I18n.T("[] does not respond.", this)));
            else
            {
                obj_item_vending_refill canister = (obj_item_vending_refill)attack_item;
                if (Dm.num(canister.get_part_rating()) == 0)
                    to_chat(user, span_warning(I18n.T("[] is empty!", canister)));
                else
                {
                    await post_restock(user, restock(canister));
                    return ITEM_INTERACT_SUCCESS;
                }
            }
        }

        if (Dm.truthy(await compartmentLoadAccessCheck(user)) && !Dm.truthy(user.combat_mode))
        {
            dot = ITEM_INTERACT_FAILURE;
            if (!Dm.truthy(is_operational))
                to_chat(user, span_warning(I18n.T("[] does not respond.", this)));
            else if (istype(attack_item, typeof(obj_item_storage_bag))) //trays USUALLY
            {
                obj_item storage_item = attack_item;
                float loaded = 0;
                float denied_items = 0;
                foreach (obj_item the_item in Dm.each<obj_item>(storage_item.contents))
                {
                    if (Dm.truthy(await loadingAttempt(the_item, user)))
                        loaded++;
                    else
                        denied_items++;
                }
                if (denied_items != 0)
                    to_chat(user, span_warning(I18n.T("[] refuses some items!", this)));
                if (loaded != 0)
                {
                    to_chat(user, span_notice(I18n.T("You insert [] dishes into []'s compartment.", loaded, this)));
                    return ITEM_INTERACT_SUCCESS;
                }
            }
            else
                return Dm.truthy(await loadingAttempt(attack_item, user)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_FAILURE;
        }
        return dot;
    }

    /**
     * After-effects of refilling a vending machine from a refill canister
     *
     * This takes the amount of products restocked and gives the user our contained credits if needed,
     * sending the user a fitting message.
     *
     * Arguments:
     * * user - the user restocking us
     * * restocked - the amount of items we've been refilled with
     */
    // tg: code/modules/vending/vendor/interaction.dm:152
    [DmProtected]
    public virtual async DmTask post_restock(mob_living user = null, float restocked = 0)
    {
        if (restocked == 0)
        {
            to_chat(user, span_warning("There's nothing to restock!"));
            return;
        }

        to_chat(user, span_notice(I18n.T("You loaded [] items in [][]", restocked, this, credits_contained > 0 ? I18n.T(", and are rewarded [] [].", credits_contained, MONEY_NAME) : ".")));
        datum_bank_account cargo_account = SSeconomy.get_dep_account(ACCOUNT_CAR);
        cargo_account.adjust_money(round(credits_contained * 0.5f), I18n.T("Vending: Restock"));
        if (credits_contained >= 1)
        {
            obj_item_holochip payday = Dm.@new<obj_item_holochip>(this, credits_contained);
            await try_put_in_hand(payday, user);
        }
        credits_contained = 0;
    }

    // tg: code/modules/vending/vendor/interaction.dm:167
    public override async DmTask<object> exchange_parts(mob user = null, obj_item_storage_part_replacer replacer_tool = null)
    {
        obj_item_storage_part_replacer replacer = replacer_tool;
        if (replacer == null || !Dm.truthy(component_parts) || refill_canister == null)
            return FALSE;

        bool works_from_distance = istype(replacer, typeof(obj_item_storage_part_replacer_bluespace));

        if (!Dm.truthy(panel_open) || works_from_distance)
            to_chat(user, display_parts(user));

        if (!Dm.truthy(panel_open) && !works_from_distance)
            return FALSE;

        float restocked = 0;
        foreach (object replacer_item in Dm.each(replacer))
        {
            if (istype(replacer_item, refill_canister))
                restocked += restock((obj_item_vending_refill)replacer_item);
        }
        await post_restock(user as mob_living, restocked);
        if (restocked > 0)
            replacer.play_rped_effect(); // gaps OD3: the RPED is live
        return TRUE;
    }

    //=======================================ATTACKS================================================
    /**
     * Dispenses free items from the standard stock.
     *
     * Arguments:
     * freebies - number of free items to vend
     */
    // tg: code/modules/vending/vendor/interaction.dm:195
    [DmPrivate]
    public virtual async DmTask freebie(float freebies = 0)
    {
        visible_message(span_notice(I18n.T("[] yields [][]!", this, freebies > 1 ? "several free goodies" : "a free goody", credits_contained > 0 ? I18n.T(" and some []", MONEY_NAME) : "")));

        for (float i = 1, i__end = freebies; i <= i__end; i++)
        {
            playsound(this, Dm.resource("sound/machines/machine_vend.ogg"), 50, TRUE, extrarange: -3);
            foreach (datum_data_vending_product record in Dm.each<datum_data_vending_product>(shuffle(product_records)))
            {
                if (Dm.num(record.amount) <= 0) //Try to use a record that actually has something to dump.
                    continue;
                // Always give out new stuff that costs before free returned stuff, because of the risk getting gibbed involved
                bool only_returned_left = Dm.num(record.amount) <= LAZYLEN(record.returned_products);
                await dispense(record, get_turf(this), silent: TRUE, dispense_returned: only_returned_left ? TRUE : FALSE);
                break;
            }
        }

        if (credits_contained > 0)
        {
            float credits_to_remove = min(CREDITS_DUMP_THRESHOLD, round(credits_contained));
            obj_item_holochip holochip = Dm.@new<obj_item_holochip>(loc, credits_to_remove);
            playsound(this, Dm.resource("sound/effects/cashregister.ogg"), 40, TRUE);
            credits_contained = max(0, credits_contained - credits_to_remove);
            SSblackbox.record_feedback("amount", "vending machine looted", holochip.credits);
        }
    }

    // tg: code/modules/vending/vendor/interaction.dm:217
    public override async DmTask<object> attacked_by(obj_item attacking_item = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.attacked_by(attacking_item, user, modifiers, attack_modifiers);
        if (!Dm.truthy(tiltable) || Dm.truthy(tilted) || Dm.num(dot) <= 0)
            return dot;
        if (isclosedturf(get_turf(user))) //If the attacker is inside of a wall, immediately fall in the other direction, with no chance for goodies.
        {
            await tilt(get_turf(get_step(this, REVERSE_DIR(get_dir(this, user)))));
            return dot;
        }

        float roll = rand(1, 100);
        if (roll >= 1 && roll <= 5)
            await freebie(3);
        else if (roll >= 6 && roll <= 15)
            await freebie(2);
        else if (roll >= 16 && roll <= 25)
            await freebie(1);
        else if (roll >= 26 && roll <= 75)
        {
            // pass()
        }
        else if (roll >= 76 && roll <= 100)
            await tilt(user);
        return dot;
    }

    // tg: code/modules/vending/vendor/interaction.dm:237
    public override async DmTask<object> attack_tk_grab(mob user = null) // the /obj root is not declared (telekinesis): this is the slot's first declaration
    {
        to_chat(user, span_warning(I18n.T("[] seems to resist your mental grasp!", this)));
        return null;
    }
}

// The AI's and cyborgs' clicks (silicons SI2, D326)
// tg: code/modules/vending/vendor/interaction.dm:240
public partial class obj_machinery_vending : obj_machinery
{
    // tg: code/modules/vending/vendor/interaction.dm:240
    public override async DmTask<object> attack_robot_secondary(mob user = null, DmList modifiers = null)
    {
        object _ = await base.attack_robot_secondary(user, modifiers);
        if (!Dm.truthy(await Adjacent(user, this)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        return _;
    }
}
