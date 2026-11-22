// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/scanners/scanner_wand.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MD1: the whole file. The medical kiosk's scanner wand targets a human-like patient (a clumsy user may target themself), which
// the kiosk takes as its patient when the wand snaps back on. The var block is generated (D111).

// tg: code/game/objects/items/devices/scanners/scanner_wand.dm:1
public partial class obj_item_scanner_wand : obj_item
{
    // tg: code/game/objects/items/devices/scanners/scanner_wand.dm:15
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        mob_living M = target_mob;
        flick(Dm.text("[]_active", icon_state), this); //nice little visual flash when scanning someone else.

        if ((HAS_TRAIT(user, TRAIT_CLUMSY) || HAS_TRAIT(user, TRAIT_DUMB)) && prob(25))
        {
            // tg passes to_chat's (null) result as the self message: the user reads only the backwards line
            to_chat(user, span_info(I18n.T("You try scanning [], before realizing you're holding the scanner backwards. Whoops.", M)));
            user.visible_message(span_warning(I18n.T("[] targets himself for scanning.", user)), null);
            selected_target = user;
            return null;
        }

        if (!ishuman(M))
        {
            to_chat(user, span_info(I18n.T("You can only scan human-like, non-robotic beings.")));
            selected_target = null;
            return null;
        }

        user.visible_message(span_notice(I18n.T("[] targets [] for scanning.", user, M)),
            span_notice(I18n.T("You target [] vitals.", M)));
        selected_target = M;
        return null;
    }

    // tg: code/game/objects/items/devices/scanners/scanner_wand.dm:34
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        to_chat(user, span_info(I18n.T("You clear the scanner's target.")));
        selected_target = null;
        return null;
    }

    // tg: code/game/objects/items/devices/scanners/scanner_wand.dm:38
    public virtual object return_patient()
    {
        object returned_target = selected_target;
        return returned_target;
    }
}
