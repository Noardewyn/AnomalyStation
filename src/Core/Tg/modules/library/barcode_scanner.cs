// Translated from tgstation (snapshot 1d99bd7f): code/modules/library/barcode_scanner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps J3, D298): the scanner's item procs; the library's consoles are not ported, so a scan has no computer (a row, interact_with_book). The var blocks are generated.

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
        Dm.Deferred("code/modules/library/barcode_scanner.dm:49", "interact_with_book: checking a book in or out on the library's computer (the library consoles are not ported; the scanner is never linked)");
        user.balloon_alert(user, I18n.T("not connected to computer!"));
        return ITEM_INTERACT_BLOCKING;
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
