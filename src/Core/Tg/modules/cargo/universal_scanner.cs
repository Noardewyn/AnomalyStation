// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/universal_scanner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J6 (D298): the universal scanner as an item: its modes and radial menu, the price tag, paper, barcodes and the profit split; the
// export reading (export_scan) with gaps P4b. The screentips are a row.

// tg: code/modules/cargo/universal_scanner.dm:1  #define PAPER_PER_SHEET 10

// tg: code/modules/cargo/universal_scanner.dm:4
public partial class obj_item_universal_scanner : obj_item
{
    private const float PAPER_PER_SHEET = 10;

    // tg: code/modules/cargo/universal_scanner.dm:36
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        scale_mode = sort_list(Dm.list(
            Dm.kv("export scanner", Dm.image(icon, null, "export scanner")),
            Dm.kv("price tagger", Dm.image(icon, null, "price tagger")),
            Dm.kv("sales tagger", Dm.image(icon, null, "sales tagger")))) as DmList;
        register_context();
        return dot;
    }

    // tg: code/modules/cargo/universal_scanner.dm:45
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await base.attack_self(user, modifiers, __extra);
        object choice = await show_radial_menu(user, this, scale_mode, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), radius: 36, require_near: TRUE);
        if (!Dm.truthy(choice))
            return FALSE;
        if (Dm.eq(icon_state, Dm.str(choice)))
            return FALSE;
        switch (choice)
        {
            case "export scanner":
                scanning_mode = SCAN_EXPORTS;
                break;
            case "price tagger":
                scanning_mode = SCAN_PRICE_TAG;
                break;
            case "sales tagger":
                scanning_mode = SCAN_SALES_TAG;
                break;
        }
        icon_state = Dm.str(choice);
        playsound(this, Dm.resource("sound/machines/click.ogg"), 40, TRUE);
        return null;
    }

    // tg: code/modules/cargo/universal_scanner.dm:62
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isobj(interacting_with))
            return NONE;
        if (Dm.eq(scanning_mode, SCAN_EXPORTS))
        {
            await export_scan(interacting_with as obj, user);
            return ITEM_INTERACT_SUCCESS;
        }
        if (Dm.eq(scanning_mode, SCAN_PRICE_TAG))
        {
            price_tag(interacting_with as obj, user);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/modules/cargo/universal_scanner.dm:73
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.eq(scanning_mode, SCAN_SALES_TAG) && isidcard(tool))
        {
            obj_item_card_id potential_acc = tool as obj_item_card_id;
            if (potential_acc.registered_account == null)
            {
                to_chat(user, span_warning(I18n.T("This ID card has no account registered!")));
                return ITEM_INTERACT_BLOCKING;
            }

            if (payments_acc == potential_acc.registered_account)
            {
                to_chat(user, span_notice(I18n.T("ID card already registered.")));
                return ITEM_INTERACT_BLOCKING;
            }

            payments_acc = potential_acc.registered_account;
            playsound(this, Dm.resource("sound/machines/ping.ogg"), 40, TRUE);
            to_chat(user, span_notice(I18n.T("[] registers the ID card. Tag a wrapped item to create a barcode.", this)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_paper)))
        {
            if (paper_count >= max_paper_count)
            {
                to_chat(user, span_notice(I18n.T("[]'s paper supply is full.", this)));
                return ITEM_INTERACT_BLOCKING;
            }

            paper_count += PAPER_PER_SHEET;
            qdel(tool);
            if (paper_count >= max_paper_count)
            {
                paper_count = max_paper_count;
                to_chat(user, span_notice(I18n.T("[]'s paper supply is now full.", this)));
                return ITEM_INTERACT_SUCCESS;
            }

            to_chat(user, span_notice(I18n.T("You refill []'s paper supply, you have [] left.", this, paper_count)));
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/cargo/universal_scanner.dm:106
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        object dot = await base.attack_self_secondary(user, modifiers);
        if (Dm.eq(scanning_mode, SCAN_SALES_TAG))
        {
            if (paper_count <= 0)
            {
                to_chat(user, span_warning(I18n.T("You're out of paper!'.")));
                return dot;
            }
            if (payments_acc == null)
            {
                to_chat(user, span_warning(I18n.T("You need to swipe [] with an ID card first.", this)));
                return dot;
            }
            paper_count--;
            playsound(this, Dm.resource("sound/machines/click.ogg"), 40, TRUE);
            to_chat(user, span_notice(I18n.T("You print a new barcode.")));
            obj_item_barcode new_barcode = Dm.@new<obj_item_barcode>(this);
            new_barcode.payments_acc = payments_acc;		// The sticker gets the scanner's registered account.
            new_barcode.cut_multiplier = cut_multiplier;		// Also the registered percent cut.
            await user.put_in_hands(new_barcode);
        }
        if (Dm.eq(scanning_mode, SCAN_PRICE_TAG))
        {
            if (loc != user)
            {
                to_chat(user, span_warning(I18n.T("You must be holding \\the [] to continue!", this)));
                return dot;
            }
            object chosen_price = await tgui_input_number(user, I18n.T("Set price"), I18n.T("Price"), new_custom_price);
            if (!Dm.truthy(chosen_price) || QDELETED(user) || QDELETED(this) || !Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)) || loc != user)
                return dot;
            new_custom_price = Dm.num(chosen_price);
            to_chat(user, span_notice(I18n.T("[] will now give things a [] [] tag.", this, new_custom_price, MONEY_SYMBOL)));
        }
        return dot;
    }

    // tg: code/modules/cargo/universal_scanner.dm:132
    public override async DmTask<object> item_ctrl_click(mob user = null)
    {
        if (Dm.eq(scanning_mode, SCAN_SALES_TAG))
        {
            payments_acc = null;
            to_chat(user, span_notice(I18n.T("You clear the registered account.")));
            return CLICK_ACTION_SUCCESS;
        }
        return CLICK_ACTION_BLOCKING;
    }

    // tg: code/modules/cargo/universal_scanner.dm:139
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        // !scanning_mode == SCAN_SALES_TAG in tg: ! binds first and the mode is never 0, so this never blocks (kept)
        if (Dm.eq(Dm.truthy(scanning_mode) ? FALSE : TRUE, SCAN_SALES_TAG))
            return CLICK_ACTION_BLOCKING;
        object potential_cut = await input(user, I18n.T("How much would you like to pay out to the registered card?"), I18n.T("Percentage Profit ([]% - []%)", round(cut_min * 100), round(cut_max * 100)));
        if (!Dm.truthy(potential_cut))
            cut_multiplier = Dm.num(Dm.initial(type, (obj_item_universal_scanner s) => s.cut_multiplier));
        cut_multiplier = clamp(round(Dm.num(potential_cut) / 100, cut_min), cut_min, cut_max);
        to_chat(user, span_notice(I18n.T("[]% profit will be received if a package with a barcode is sold.", round(cut_multiplier * 100))));
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/cargo/universal_scanner.dm:149
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("It has []/[] available barcodes. Refill with paper.", paper_count, max_paper_count)));

        if (Dm.eq(scanning_mode, SCAN_SALES_TAG))
        {
            Dm.append(ref dot, span_notice(I18n.T("Profit split on sale is currently set to []%. <b>Alt-click</b> to change.", round(cut_multiplier * 100))));
            if (payments_acc != null)
                Dm.append(ref dot, span_notice(I18n.T("<b>Ctrl-click</b> to clear the registered account.")));
        }

        if (Dm.eq(scanning_mode, SCAN_PRICE_TAG))
            Dm.append(ref dot, span_notice(I18n.T("The current custom price is set to [] []. <b>Right-click</b> to change.", new_custom_price, MONEY_SYMBOL)));
        return dot;
    }

    /**
     * Scans an object, target, and provides its export value based on selling to the cargo shuttle, to mob/user.
     */
    // tg: code/modules/cargo/universal_scanner.dm:177
    public virtual async DmTask export_scan(obj target = null, mob user = null)
    {
        datum_export_report report = await export_item_and_contents(target, dry_run: TRUE);
        float price = 0;
        foreach (object exported_datum in Dm.each(report.total_amount))
            price += Dm.num(report.total_value[exported_datum]);

        string message = I18n.T("Scanned []", target);
        bool warning = false;
        if (Dm.truthy(length(target.contents)))
        {
            message = I18n.T("Scanned [] and its contents", target);
            if (price != 0)
                message += I18n.T(", total value: <b>[]</b> []", price, MONEY_NAME);
            else
            {
                message += I18n.T(", no export values");
                warning = true;
            }
            if (!Dm.truthy(report.all_contents_scannable))
                message += I18n.T(" (Undeterminable value detected, final value may differ)");
            message += ".";
        }
        else
        {
            if (!Dm.truthy(report.all_contents_scannable))
            {
                message += I18n.T(", unable to determine value.");
                warning = true;
            }
            else if (price != 0)
                message += I18n.T(", value: <b>[]</b> [].", price, MONEY_NAME);
            else
            {
                message += I18n.T(", no export value.");
                warning = true;
            }
        }
        if (warning)
            to_chat(user, span_warning(message));
        else
            to_chat(user, span_notice(message));

        if (price != 0)
            playsound(this, Dm.resource("sound/machines/terminal/terminal_select.ogg"), 50, vary: TRUE);

        if (target is obj_item_delivery parcel)
        {
            if (parcel.sticker == null)
                return;
            obj_item_barcode parcel_code = parcel.sticker;
            to_chat(user, span_notice(I18n.T("Export barcode detected! This parcel, upon export, will pay out to [], with a []% split to them (already reflected in above recorded value).", parcel_code.payments_acc.account_holder, parcel_code.cut_multiplier * 100)));
        }

        if (target is obj_item_barcode our_code)
            to_chat(user, span_notice(I18n.T("Export barcode detected! This barcode, if attached to a parcel, will pay out to [], with a []% split to them.", our_code.payments_acc.account_holder, our_code.cut_multiplier * 100)));

        if (user is mob_living_carbon_human scan_human)
        {
            if (target is obj_item_bounty_cube cube)
            {
                var scanner_account = scan_human.get_bank_account() as datum_bank_account;

                if (get_area(cube) is not area_shuttle_supply)
                    to_chat(user, span_warning(I18n.T("Shuttle placement not detected. Handling tip not registered.")));

                else if (cube.bounty_handler_account != null)
                    to_chat(user, span_warning(I18n.T("Bank account for handling tip already registered!")));

                else if (scanner_account != null)
                {
                    await cube.AddComponent(typeof(datum_component_pricetag), Dm.list(scanner_account), cube.handler_tip, FALSE);

                    cube.bounty_handler_account = scanner_account;
                    await cube.bounty_handler_account.bank_card_talk(I18n.T("Bank account for []handling tip successfully registered.", price != 0 ? Dm.text("<b>[]</b> [] ", price * cube.handler_tip, MONEY_NAME_SINGULAR) : ""));

                    foreach (datum_bank_account shareholder in Dm.each<datum_bank_account>(cube.bounty_holder_accounts))
                    {
                        if (shareholder != cube.bounty_handler_account) //No need to send a tracking update to the person scanning it
                            await shareholder.bank_card_talk(I18n.T("<b>[]</b> was scanned in \\the <b>[]</b> by <b>[] ([])</b>.", cube, get_area(cube), scan_human, scan_human.job));
                    }
                }

                else
                    to_chat(user, span_warning(I18n.T("Bank account not detected. Handling tip not registered.")));
            }
        }
    }

    /// Scans an object, target, and sets its custom_price variable to new_custom_price, presenting it to the user.
    // tg: code/modules/cargo/universal_scanner.dm:253
    public virtual void price_tag(obj target = null, mob user = null)
    {
        if (isitem(target))
        {
            obj_item selected_target = target as obj_item;
            selected_target.custom_price = new_custom_price;
            to_chat(user, span_notice(I18n.T("You set the price of [] to [] [].", selected_target, new_custom_price, MONEY_SYMBOL)));
        }
    }

    /// check_menu: Checks if we are allowed to interact with a radial menu
    // tg: code/modules/cargo/universal_scanner.dm:265
    public virtual object check_menu(mob_living user = null)
    {
        if (!istype(user, typeof(mob_living)))
            return FALSE;
        if (Dm.truthy(user.incapacitated))
            return FALSE;
        return TRUE;
    }
}
