// Translated from tgstation (snapshot 1d99bd7f): code/modules/vending/vendor/ui_data.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package S2: the whole file; the client's window is godot/scripts/Shared/Tgui/Interfaces/Vending.cs. Deferred leaf: select_colors's
// greyscale colour menu (/datum/greyscale_modify_menu, the GAGS editor, is not ported).

// tg: code/modules/vending/vendor/_vending.dm:37
public partial class obj_machinery_vending
{
    ///Helper to create a typepath to be used in the UI
    // tg: code/modules/vending/vendor/ui_data.dm:2  #define SANITIZED_PATH(path) (file-local, #undef at :185)
    public static string SANITIZED_PATH(object path) => replacetext(replacetext(Dm.text("[]", path), "/obj/item/", ""), "/", "-") as string;

    // tg: code/modules/vending/vendor/ui_data.dm:4
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/spritesheet_batched/vending)): the asset transport is the client's (DV44); the Vending window
        // draws a product from its tg icon and state (the static data's), as tgui's DmIcon does.
        return Dm.list();
    }

    // tg: code/modules/vending/vendor/ui_data.dm:9
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        // SEND_SIGNAL(src, COMSIG_VENDING_UI_INTERACT, user, ui), expanded inline (rules 11.1 (e))
        if (Dm.truthy(_listen_lookup?[COMSIG_VENDING_UI_INTERACT]) && Dm.truthy(Dm.bitand(_SendSignal(COMSIG_VENDING_UI_INTERACT, Dm.list(this, user, ui)), VENDING_DENIED)))
        {
            if (Dm.truthy(icon_deny))
                flick(icon_deny, this);
            return null;
        }

        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Vending", name);
            await ui.open();
        }
        return null;
    }

    /**
     * Returns a list of given product records of the vendor to be used in UI.
     * arguments:
     * records - list of records available
     * categories - list of categories available
     * premium - bool of whether a record should be priced by a custom/premium price or not
     */
    // tg: code/modules/vending/vendor/ui_data.dm:27
    [DmProtected]
    public virtual DmList collect_records_for_static_data(DmList records = null, DmList categories = null, float? premium = null)
    {
        DmList default_category = Dm.list(
            Dm.kv("name", "Products"),
            Dm.kv("icon", "cart-shopping")
        ); // tg: var/static/list/default_category, read only: a fresh copy each call

        DmList out_records = Dm.list();

        foreach (datum_data_vending_product record in Dm.each_cast<datum_data_vending_product>(records))
        {
            DmList static_record = Dm.list(
                Dm.kv("path", SANITIZED_PATH(record.product_path)),
                Dm.kv("name", record.name),
                Dm.kv("price", record.price),
                Dm.kv("ref", REF(record)),
                Dm.kv("colorable", record.colorable)
            );

            object printed = record.product_path;
            // If it's not GAGS and has no innate colors we have to care about, we use DMIcon
            if (ispath(printed, typeof(atom))
                && (!Dm.truthy(Dm.initial(printed, (atom p) => p.greyscale_config)) || !Dm.truthy(Dm.initial(printed, (atom p) => p.greyscale_colors)))
                && !Dm.truthy(Dm.initial(printed, (atom p) => p.color)))
            {
                static_record["icon"] = Dm.initial(printed, (atom p) => p.icon);
                static_record["icon_state"] = Dm.initial(printed, (atom p) => p.icon_state);
            }
            else if (ispath(printed, typeof(atom)))
            {
                // DEVIATION(DV44): tg draws this product from its vending sprite sheet (asset_cache/assets/vending.dm, get_display_icon_for:
                // the GAGS icon, or the icon blended with its colour), which the server does not send: the window draws it from the GAGS
                // name the client composes (DV19's "gags:<config>|<colours>") with tg's preview state, or, for a coloured icon, from its
                // icon and state without the colour
                object preview_state = Dm.truthy(Dm.initial(printed, (obj p) => p.icon_state_preview)) ? Dm.initial(printed, (obj p) => p.icon_state_preview) : Dm.initial(printed, (atom p) => p.icon_state);
                bool has_gags = Dm.truthy(Dm.initial(printed, (atom p) => p.greyscale_config)) && Dm.truthy(Dm.initial(printed, (atom p) => p.greyscale_colors));
                static_record["icon"] = has_gags
                    ? Dm.text("gags:[]|[]", Dm.initial(printed, (atom p) => p.greyscale_config), Dm.initial(printed, (atom p) => p.greyscale_colors))
                    : Dm.initial(printed, (atom p) => p.icon);
                static_record["icon_state"] = preview_state;
            }

            DmList category = record.category ?? default_category;
            if (category != null)
            {
                if (!Dm.@in(category["name"], categories))
                    categories[category["name"]] = Dm.list(Dm.kv("icon", category["icon"]));

                static_record["category"] = category["name"];
            }

            if (Dm.truthy(premium))
                static_record["premium"] = TRUE;

            Dm.append(ref out_records, Dm.list(static_record));
        }

        return out_records;
    }

    // tg: code/modules/vending/vendor/ui_data.dm:69
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        if (ad_list.len > 0)
            data["ad"] = ad_list[rand(1, ad_list.len)];
        data["all_products_free"] = all_products_free;
        data["department"] = payment_department;
        data["jobDiscount"] = DEPARTMENT_DISCOUNT;
        data["product_records"] = Dm.list();
        data["displayed_currency_icon"] = displayed_currency_icon;
        data["displayed_currency_name"] = Dm.text(" []", displayed_currency_name);

        DmList categories = Dm.list();

        data["product_records"] = collect_records_for_static_data(product_records, categories);
        data["coin_records"] = collect_records_for_static_data(coin_records, categories, premium: TRUE);
        data["hidden_records"] = collect_records_for_static_data(hidden_records, categories, premium: TRUE);

        data["categories"] = categories;

        return data;
    }

    /**
     * Returns the balance that the vendor will use for proceeding payment. Most vendors would want to use the user's
     * card's account credits balance.
     * arguments:
     * passed_id - the id card that will be billed for the product
     */
    // tg: code/modules/vending/vendor/ui_data.dm:97
    [DmProtected]
    public virtual object fetch_balance_to_use(obj_item_card_id passed_id = null)
    {
        return (passed_id.registered_account as datum_bank_account).account_balance;
    }

    // tg: code/modules/vending/vendor/ui_data.dm:102
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list();

        obj_item_card_id card_used = null;
        object held_cash = 0;
        if (isliving(user))
        {
            mob_living living_user = (mob_living)user;
            card_used = living_user.get_idcard(TRUE) as obj_item_card_id;
            held_cash = living_user.tally_physical_credits();
        }

        DmList user_data = null;
        if (Dm.truthy(card_used?.registered_account))
        {
            datum_bank_account account = (datum_bank_account)card_used.registered_account;
            user_data = Dm.list();
            user_data["name"] = account.account_holder;
            user_data["cash"] = Dm.add(fetch_balance_to_use(card_used), held_cash);
            user_data["is_captain_card"] = istype(card_used.trim, big_boss_trim) ? TRUE : FALSE;
            if (Dm.truthy(account.account_job))
            {
                user_data["job"] = (account.account_job as datum_job).title;
                user_data["department"] = (account.account_job as datum_job).paycheck_department;
            }
            else
            {
                user_data["job"] = "No Job";
                user_data["department"] = DEPARTMENT_UNASSIGNED;
            }
        }
        dot["user"] = user_data;

        DmList stock = Dm.list();
        dot["stock"] = stock;
        foreach (datum_data_vending_product product_record in Dm.each_cast<datum_data_vending_product>(Dm.add(Dm.add(product_records, coin_records), hidden_records)))
        {
            stock[SANITIZED_PATH(product_record.product_path)] = Dm.list(
                Dm.kv("amount", product_record.amount),
                Dm.kv("free", length(product_record.returned_products))
            );
        }

        if (prob(10) && ad_list.len > 0)
            dot["ad"] = ad_list[rand(1, ad_list.len)];

        dot["extended_inventory"] = extended_inventory;
        return dot;
    }

    // tg: code/modules/vending/vendor/ui_data.dm:138
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "vend":
                dot = await vend(@params, ui.user);
                break;
            case "select_colors":
            {
                datum_data_vending_product product = Dm.locate_ref(@params["ref"]) as datum_data_vending_product;
                if (product == null)
                    return FALSE;
                object fake_atom = product.product_path;
                object config = Dm.initial(fake_atom, (atom p) => p.greyscale_config);
                if (!Dm.truthy(config))
                    return FALSE;
                // the allowed configs (the item's worn and inhand configs) and new /datum/greyscale_modify_menu(src, ui.user, allowed_configs,
                // CALLBACK(src, PROC_REF(_vend_greyscale), params, ui.user), ...).ui_interact(ui.user)
                Dm.Deferred("code/modules/vending/vendor/ui_data.dm:156", "ui_act: select_colors, the greyscale colour menu of a colourable product (/datum/greyscale_modify_menu, the GAGS editor, is not ported)");
                return TRUE;
            }
        }
        return dot;
    }

    /**
     * Vends a greyscale modified item.
     * arguments:
     * menu - greyscale config menu that has been used to vend the item
     */
    // tg: code/modules/vending/vendor/ui_data.dm:178
    [DmPrivate]
    public virtual async DmTask _vend_greyscale(DmList @params = null, mob user = null, datum_greyscale_modify_menu menu = null)
    {
        if (!Dm.eq(user, menu.user))
            return;
        await vend(@params, user, menu.split_colors);
    }
}
