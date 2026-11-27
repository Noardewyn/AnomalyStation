// Translated from tgstation (snapshot 1d99bd7f): code/modules/library/barcode_scanner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J3 (D298): the scanner's item procs; with the library's consoles its scans check books in and out of the book management
// console it is linked to and add them to its inventory. The context tips are live-partial rows. The var blocks are generated.

// tg: code/modules/library/barcode_scanner.dm:6
public partial class obj_item_barcodescanner : obj_item
{
    private const string BARCODE_SCANNER_CHECKIN = "check_in";
    private const string BARCODE_SCANNER_CHECKOUT = "check_out";
    private const string BARCODE_SCANNER_INVENTORY = "inventory";

    // tg: code/modules/library/barcode_scanner.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // register_item_context(), register_context(): the context tips (screentips, not now)
        return dot;
    }

    // tg: code/modules/library/barcode_scanner.dm:43
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (interacting_with is obj_item_book)
            return interact_with_book((obj_item_book)interacting_with, user);
        return NONE;
    }

    // tg: code/modules/library/barcode_scanner.dm:48
    public virtual object interact_with_book(obj_item_book target_book = null, mob_living user = null)
    {
        obj_machinery_computer_libraryconsole_bookmanagement linked_computer = computer_ref?.resolve() as obj_machinery_computer_libraryconsole_bookmanagement;
        if (linked_computer == null)
        {
            user.balloon_alert(user, I18n.T("not connected to computer!"));
            return ITEM_INTERACT_BLOCKING;
        }

        switch (scan_mode)
        {
            case BARCODE_SCANNER_CHECKIN:
            {
                DmList checkouts = linked_computer.checkouts;
                foreach (object checkout_ref in Dm.each(checkouts))
                {
                    datum_borrowbook maybe_ours = checkouts[checkout_ref] as datum_borrowbook;
                    if (!Dm.truthy(target_book.book_data.compare(maybe_ours.book_data)))
                        continue;
                    Dm.remove(ref checkouts, checkout_ref);
                    linked_computer.checkout_update();
                    balloon_alert(user, I18n.T("checked in"));
                    playsound(this, Dm.resource("sound/items/barcodebeep.ogg"), 20, FALSE);
                    return ITEM_INTERACT_SUCCESS;
                }

                user.balloon_alert(user, I18n.T("isn't checked out!"));
                return ITEM_INTERACT_BLOCKING;
            }

            case BARCODE_SCANNER_CHECKOUT:
            {
                DmList checkouts = linked_computer.checkouts;
                foreach (object checkout_ref in Dm.each(checkouts))
                {
                    datum_borrowbook maybe_ours = checkouts[checkout_ref] as datum_borrowbook;
                    if (Dm.truthy(target_book.book_data.compare(maybe_ours.book_data)))
                    {
                        user.balloon_alert(user, I18n.T("already checked out!"));
                        return ITEM_INTERACT_BLOCKING;
                    }
                }
                foreach (object copy_ref in Dm.each(linked_computer.inventory))
                {
                    if (!Dm.truthy(target_book.book_data.compare(linked_computer.inventory[copy_ref] as datum_book_info)))
                        continue;
                    linked_computer.checking_out_book = target_book.book_data;
                    balloon_alert(user, I18n.T("set for check out"));
                    playsound(this, Dm.resource("sound/items/barcodebeep.ogg"), 20, FALSE);
                    return ITEM_INTERACT_SUCCESS;
                }
                user.balloon_alert(user, I18n.T("not in inventory!"));
                return ITEM_INTERACT_BLOCKING;
            }

            case BARCODE_SCANNER_INVENTORY:
            {
                datum_book_info our_copy = target_book.book_data.return_copy() as datum_book_info;
                linked_computer.inventory[@ref(our_copy)] = our_copy;
                linked_computer.inventory_update();
                balloon_alert(user, I18n.T("added to inventory"));
                playsound(this, Dm.resource("sound/items/barcodebeep.ogg"), 20, FALSE);
                return ITEM_INTERACT_SUCCESS;
            }
        }

        return NONE;
    }

    // tg: code/modules/library/barcode_scanner.dm:97
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(computer_ref?.resolve()))
        {
            balloon_alert(user, I18n.T("connect to computer!"));
            return dot;
        }
        switch (scan_mode)
        {
            case BARCODE_SCANNER_CHECKIN:
                scan_mode = BARCODE_SCANNER_CHECKOUT;
                balloon_alert(user, I18n.T("check-out mode"));
                break;
            case BARCODE_SCANNER_CHECKOUT:
                scan_mode = BARCODE_SCANNER_INVENTORY;
                balloon_alert(user, I18n.T("inventory adding mode"));
                break;
            case BARCODE_SCANNER_INVENTORY:
                scan_mode = BARCODE_SCANNER_CHECKIN;
                balloon_alert(user, I18n.T("check-in mode"));
                break;
        }
        playsound(loc, Dm.resource("sound/items/click.ogg"), 20, TRUE);
        return dot;
    }
}
