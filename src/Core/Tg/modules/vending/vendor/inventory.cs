// Translated from tgstation (snapshot 1d99bd7f): code/modules/vending/vendor/inventory.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package S2: the whole file; the market crash's inflation and the purchase log came with the economy (gaps P4a, D327).

// tg: code/modules/vending/vendor/_vending.dm:37
public partial class obj_machinery_vending
{
    //================================STOCKING IN ITEMS=================================

    /**
     * Build the inventory of the vending machine from its product and record lists
     *
     * This builds up a full set of /datum/data/vending_products from the product list of the vending machine type
     * Arguments:
     * * productlist - the list of products that need to be converted
     * * recordlist - the list containing /datum/data/vending_product datums
     * * categories - A list in the format of product_categories to source category from
     * * startempty - should we set vending_product record amount from the product list (so it's prefilled at roundstart)
     * * premium - Whether the ending products shall have premium or default prices
     */
    // tg: code/modules/vending/vendor/inventory.dm:14
    [DmPrivate]
    public virtual void build_inventory(DmList productlist = null, DmList recordlist = null, DmList categories = null, float? start_empty = null, float? premium = null)
    {
        start_empty ??= FALSE;
        premium ??= FALSE;
        float inflation_value = HAS_TRAIT(SSeconomy, TRAIT_MARKET_CRASHING) ? SSeconomy.inflation_value__proc() : 1;
        default_price = round(Dm.initial(this, (obj_machinery_vending p) => p.default_price) * inflation_value);
        extra_price = round(Dm.initial(this, (obj_machinery_vending p) => p.extra_price) * inflation_value);

        QDEL_LIST(recordlist);

        DmList product_to_category = Dm.list();
        foreach (DmList category in Dm.each_cast<DmList>(categories))
            foreach (object product_key in Dm.each(category["products"]))
                product_to_category[product_key] = category;

        foreach (object typepath in Dm.each(productlist))
        {
            object amount = productlist[typepath];

            Type temp = typepath as Type;
            datum_data_vending_product new_record = Dm.@new<datum_data_vending_product>();
            new_record.name = Dm.initial(temp, (obj_item p) => p.name);
            new_record.product_path = typepath;
            if (!Dm.truthy(start_empty))
                new_record.amount = Dm.num(amount);
            new_record.max_amount = Dm.num(amount);

            ///Prices of vending machines are all increased uniformly.
            float custom_price = round(Dm.num(Dm.initial(temp, (obj_item p) => p.custom_price)) * inflation_value);
            if (!Dm.truthy(premium))
                new_record.price = custom_price != 0 ? custom_price : default_price;
            else
            {
                float premium_custom_price = round(Dm.num(Dm.initial(temp, (obj_item p) => p.custom_premium_price)) * inflation_value);
                if (premium_custom_price == 0 && custom_price != 0) //For some ungodly reason, some premium only items only have a custom_price
                    new_record.price = extra_price + custom_price;
                else
                    new_record.price = premium_custom_price != 0 ? premium_custom_price : extra_price;
            }

            new_record.age_restricted = Dm.initial(temp, (obj_item p) => p.age_restricted);
            new_record.colorable = Dm.truthy(Dm.initial(temp, (atom p) => p.greyscale_config)) && Dm.truthy(Dm.initial(temp, (atom p) => p.greyscale_colors))
                && Dm.truthy(Dm.bitand(Dm.initial(temp, (atom p) => p.flags_1), IS_PLAYER_COLORABLE_1)) ? TRUE : FALSE;
            new_record.category = product_to_category[typepath] as DmList;
            Dm.append(ref recordlist, new_record);
        }
    }

    /**
     * Builds all available inventories for the vendor - standard, contraband and premium
     *
     * Arguments
     * start_empty - bool to pass into build_inventory that determines whether a product entry starts with available stock or not
    */
    // tg: code/modules/vending/vendor/inventory.dm:61
    public virtual void build_inventories(float? start_empty = null)
    {
        start_empty ??= FALSE;
        build_inventory(products, product_records, product_categories, start_empty);
        build_inventory(contraband, hidden_records, Dm.list(Dm.list(Dm.kv("name", "Contraband"), Dm.kv("icon", "mask"), Dm.kv("products", contraband))), start_empty, premium: TRUE);
        build_inventory(premium, coin_records, Dm.list(Dm.list(Dm.kv("name", "Premium"), Dm.kv("icon", "coins"), Dm.kv("products", premium))), start_empty, premium: TRUE);
    }

    //Better would be to make constructable child
    // tg: code/modules/vending/vendor/inventory.dm:67
    public override async DmTask<object> RefreshParts()
    {
        // SHOULD_CALL_PARENT(FALSE)

        //compress all product categories into an linear list
        if (Dm.truthy(product_categories))
        {
            products.Cut();
            foreach (DmList category in Dm.each_cast<DmList>(product_categories))
                Dm.combine(ref products, category["products"]);
        }

        //locate canister
        obj_item_vending_refill canister = refill_canister != null ? Dm.locate_type(refill_canister, component_parts) as obj_item_vending_refill : null;

        //build the records, if we have a canister make the records empty so we can refill it from the canister else make it max amount
        build_inventories(start_empty: canister != null ? TRUE : FALSE);

        //fill the records if we have an canister
        if (canister != null)
            restock(canister);
        return null;
    }

    /**
     * Refill a vending machine from a refill canister
     *
     * This takes the products from the refill canister and then fills the products, contraband and premium product categories
     *
     * Arguments:
     * * canister - the vending canister we are refilling from
     */
    // tg: code/modules/vending/vendor/inventory.dm:94
    public virtual float restock(obj_item_vending_refill canister = null)
    {
        float dot = 0;

        //to initialize product category & cargo ordered canisters for the 1st time
        if (!Dm.truthy(canister.products))
        {
            canister.products = products.Copy();
            canister.contraband = contraband.Copy();
            canister.premium = premium.Copy();
        }

        DmList record_list = null;
        DmList canister_list = null;

        for (float i = 1; i <= 3; i++)
        {
            switch (i)
            {
                case 1:
                    record_list = product_records;
                    canister_list = canister.products;
                    break;
                case 2:
                    record_list = hidden_records;
                    canister_list = canister.contraband;
                    break;
                default:
                    record_list = coin_records;
                    canister_list = canister.premium;
                    break;
            }
            if (record_list.len == 0 || canister_list.len == 0)
                continue;

            foreach (datum_data_vending_product record in Dm.each_cast<datum_data_vending_product>(record_list))
            {
                float diff = min(record.max_amount - Dm.num(record.amount), Dm.num(canister_list[record.product_path] is var held && Dm.truthy(held) ? held : 0));
                if (diff != 0)
                {
                    canister_list[record.product_path] = Dm.num(canister_list[record.product_path]) - diff;
                    if (!Dm.truthy(canister_list[record.product_path]))
                        Dm.remove(ref canister_list, record.product_path);
                    record.amount += diff;
                    dot += diff;
                }
            }
        }
        return dot;
    }

    //===========================VENDING OUT ITEMS================================
    // tg: code/modules/vending/vendor/inventory.dm:130
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        foreach (datum_data_vending_product record in Dm.each<datum_data_vending_product>(Dm.add(Dm.add(product_records, coin_records), hidden_records)))
        {
            if (Dm.@in(gone, record.returned_products))
            {
                Dm.remove(ref record.returned_products, gone);
                record.amount -= 1;
                break;
            }
        }
        return dot;
    }

    /**
     * The entire shebang of vending the picked item. Processes the vending and initiates the payment for the item.
     * arguments:
     * greyscale_colors - greyscale config for the item we're about to vend, if any
     */
    // tg: code/modules/vending/vendor/inventory.dm:143
    [DmProtected]
    public virtual async DmTask<object> vend(DmList @params = null, mob user = null, DmList greyscale_colors = null)
    {
        object dot = TRUE;
        datum_data_vending_product item_record = Dm.locate_ref(@params["ref"]) as datum_data_vending_product;
        DmList record_to_check = Dm.add(product_records, coin_records) as DmList;
        if (Dm.truthy(extended_inventory))
            record_to_check = Dm.add(Dm.add(product_records, coin_records), hidden_records) as DmList;
        if (item_record == null || !Dm.truthy(item_record.product_path))
            return dot;
        object price_to_use = item_record.price;
        if (Dm.@in(item_record, hidden_records))
        {
            if (!Dm.truthy(extended_inventory))
                return dot;
        }
        else if (!Dm.@in(item_record, record_to_check))
        {
            message_admins(Dm.text("Vending machine exploit attempted by []!", ADMIN_LOOKUPFLW(user)));
            return dot;
        }
        if (Dm.num(item_record.amount) <= 0)
        {
            await speak(I18n.T("Sold out of [].", item_record.name));
            flick(icon_deny, this);
            return dot;
        }
        if (!Dm.truthy(all_products_free))
        {
            // Here we do additional handing ahead of the payment component's logic, such as age restrictions and additional logging
            obj_item_card_id card_used = null;
            mob_living living_user = null;
            if (isliving(user))
            {
                living_user = user as mob_living;
                card_used = living_user.get_idcard(TRUE) as obj_item_card_id;
            }
            if (QDELETED(card_used))
            {
                await speak(I18n.T("You do not possess an ID to purchase [].", item_record.name));
                return dot;
            }

            if (Dm.truthy(age_restrictions) && Dm.truthy(item_record.age_restricted) && (!Dm.truthy(card_used.registered_age) || Dm.num(card_used.registered_age) < AGE_MINOR))
            {
                await speak(I18n.T("You are not of legal age to purchase [].", item_record.name));
                if (!Dm.@in(user, GLOB.narcd_underages))
                {
                    await aas_config_announce(typeof(datum_aas_config_entry_vendomat_age_control), Dm.list(
                        Dm.kv("PERSON", usr?.name),
                        Dm.kv("LOCATION", get_area_name(this)),
                        Dm.kv("VENDOR", name),
                        Dm.kv("PRODUCT", item_record.name)
                    ), this, Dm.list(RADIO_CHANNEL_SECURITY));
                    Dm.append(ref GLOB.narcd_underages, user);
                }
                flick(icon_deny, this);
                return dot;
            }

            if (!Dm.truthy(await proceed_payment(card_used, living_user, item_record, price_to_use, @params["premium"])))
                return dot;
        }

        if (!Dm.eq(last_shopper, REF(user)) || Dm.num(purchase_message_cooldown) < world.time)
        {
            object vend_response = Dm.truthy(vend_reply) ? vend_reply : I18n.T("Thank you for shopping with []!", this);
            await speak(vend_response);
            purchase_message_cooldown = world.time + 5 * SECONDS;
            //This is not the best practice, but it's safe enough here since the chances of two people using a machine with the same ref in 5 seconds is fuck low
            last_shopper = REF(user);
        }
        if (Dm.truthy(icon_vend)) //Show the vending animation if needed
            flick(icon_vend, this);

        // Always give out free returned stuff first, e.g. to avoid walling a traitor objective in a bag behind paid items
        obj_item vended_item = await dispense(item_record, get_turf(this), dispense_returned: LAZYLEN(item_record.returned_products)) as obj_item;
        if (vended_item == null)
            return dot;

        if (Dm.truthy(greyscale_colors))
            await vended_item.set_greyscale(colors: greyscale_colors);

        // SEND_SIGNAL(user, COMSIG_MOB_VENDING_PURCHASE, src, vended_item), expanded inline (rules 11.1 (e))
        object sigreturn = Dm.truthy(user._listen_lookup?[COMSIG_MOB_VENDING_PURCHASE]) ? user._SendSignal(COMSIG_MOB_VENDING_PURCHASE, Dm.list(user, this, vended_item)) : NONE;
        if (!Dm.truthy(Dm.bitand(sigreturn, VENDING_NO_PICKUP)) && Dm.truthy(await IsReachableBy(user)) && Dm.truthy(await user.put_in_hands(vended_item)))
        {
            to_chat(user, span_notice(I18n.T("You take [] out of the slot.", item_record.name)));
            vended_item.do_pickup_animation(user, get_turf(this)); // tg passes src as the source; the animation starts from its turf
        }
        else
            to_chat(user, span_warning(I18n.T("[] falls onto the floor!", capitalize(format_text(item_record.name)))));

        SSblackbox.record_feedback("nested tally", "vending_machine_usage", 1, Dm.list(Dm.text("[]", GetType()), Dm.text("[]", item_record.product_path)));
        return dot;
    }

    /**
     * Common proc that dispenses an item. Called when the item is vended, or gotten some other way.
     *
     * Arguments
     * * datum/data/vending_product/item_record - the vending record which contains the information of the item to dispense
     * * atom/spawn_location - location to dispense the item to
     * * silent - should we play the vending sound
     * * dispense_returned - are we vending out an returned item
    */
    // tg: code/modules/vending/vendor/inventory.dm:226
    [ShouldNotOverride]
    public async DmTask<object> dispense(datum_data_vending_product item_record = null, atom spawn_location = null, float? silent = null, float? dispense_returned = null)
    {
        silent ??= FALSE;
        dispense_returned ??= FALSE;
        if (!Dm.truthy(silent))
            playsound(this, Dm.resource("sound/machines/machine_vend.ogg"), 50, TRUE, extrarange: -3);

        obj_item vended_item = null;
        if (Dm.truthy(dispense_returned))
        {
            vended_item = LAZYACCESS(item_record.returned_products, LAZYLEN(item_record.returned_products)) as obj_item; //first in, last out
            await vended_item.forceMove(spawn_location);
        }
        else if (Dm.truthy(item_record.amount))
        {
            vended_item = Dm.@new((Type)item_record.product_path, spawn_location) as obj_item;
            if (Dm.@in(vended_item.GetType(), contraband))
                ADD_TRAIT(vended_item, TRAIT_CONTRABAND, INNATE_TRAIT);
            item_record.amount--;
        }

        await on_dispense(vended_item, dispense_returned);
        await use_energy(active_power_usage);

        // SEND_SIGNAL(src, COMSIG_VENDING_DISPENSED, vended_item), expanded inline (rules 11.1 (e))
        if (Dm.truthy(_listen_lookup?[COMSIG_VENDING_DISPENSED]))
            _SendSignal(COMSIG_VENDING_DISPENSED, Dm.list(this, vended_item));

        return vended_item;
    }

    /**
     * A proc meant to perform custom behavior on newly dispensed items.
     *
     * Arguments
     * * obj/item/vended_item - the item that has just been dispensed
     * * dispense_returned - is this item an returned product
    */
    // tg: code/modules/vending/vendor/inventory.dm:256
    [DmProtected]
    public virtual async DmTask on_dispense(obj_item vended_item = null, float? dispense_returned = null)
    {
        return;
    }

    /**
     * Handles payment processing: discounts, logging, balance change etc.
     * arguments:
     * paying_id_card - the id card that will be billed for the product.
     * mob_paying - the mob that is trying to purchase the item.
     * product_to_vend - the product record of the item we're trying to vend.
     * price_to_use - price of the item we're trying to vend.
     * premium - whether or not to apply premium pricing
     */
    // tg: code/modules/vending/vendor/inventory.dm:270
    [DmProtected]
    public virtual async DmTask<object> proceed_payment(obj_item_card_id paying_id_card = null, mob_living mob_paying = null, datum_data_vending_product product_to_vend = null, object price_to_use = null, object premium = null)
    {
        //returned items are free
        if (Dm.truthy(LAZYLEN(product_to_vend.returned_products)))
            return TRUE;

        //account to use. optional cause we handle cash on hand transfers as well
        datum_bank_account account = paying_id_card.registered_account as datum_bank_account;

        //deduct money from person
        if (Dm.truthy(discount_check(paying_id_card, premium)))
            price_to_use = max(round(Dm.num(price_to_use) * DEPARTMENT_DISCOUNT), 1); //No longer free, but signifigantly cheaper.
        if (Dm.truthy(Dm.bitand(attempt_charge(this, mob_paying, Dm.num(price_to_use)), COMPONENT_OBJ_CANCEL_CHARGE)))
        {
            await speak(I18n.T("You do not possess the funds to purchase [].", product_to_vend.name));
            flick(icon_deny, this);
            return FALSE;
        }

        //transfer money to machine
        SSblackbox.record_feedback("amount", "vending_spent", price_to_use);
        log_econ(Dm.text("[] [] were inserted into [] by [] to buy [].", price_to_use, MONEY_NAME, this, account.account_holder, product_to_vend));
        credits_contained += round(Dm.num(price_to_use) * VENDING_CREDITS_COLLECTION_AMOUNT);
        return TRUE;
    }

    // tg: code/modules/vending/vendor/inventory.dm:294
    [DmProtected]
    public virtual object discount_check(obj_item_card_id paying_id_card = null, object premium = null)
    {
        if (Dm.truthy(premium))
            return FALSE;
        datum_bank_account account = paying_id_card.registered_account as datum_bank_account;
        //captain gets a discount in every department
        return istype(paying_id_card.trim, big_boss_trim) || Dm.eq((account.account_job as datum_job)?.paycheck_department, payment_department) ? TRUE : FALSE;
    }
}
