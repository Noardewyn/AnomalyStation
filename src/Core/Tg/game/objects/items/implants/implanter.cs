// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/implants/implanter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M8 slice F, D186).

/**
 * Players can use this item to put obj/item/implant's in living mobs. Can be renamed with a pen.
 */
// tg: code/game/objects/items/implants/implanter.dm:4
public partial class obj_item_implanter : obj_item
{
    // tg: code/game/objects/items/implants/implanter.dm:20
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("implanter[]", imp != null ? 1 : 0);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/implants/implanter.dm:24
    public override async DmTask<object> attack__proc(mob_living target = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (!(target != null && user != null && imp != null))
            return null;

        if (target != user)
        {
            target.visible_message(span_warning(I18n.T("[] is attempting to implant [].", user, target)));
            if (!Dm.truthy(await do_after(user, 5 * SECONDS, target)))
                return null;
        }

        if (!(imp != null))
            return null;

        if (Dm.truthy(await imp.implant(target, user)))
        {
            if (target == user)
                to_chat(user, span_notice(I18n.T("You implant yourself.")));
            else
                target.visible_message(span_notice(I18n.T("[] implants [].", user, target)), span_notice(I18n.T("[] implants you.", user)));
            imp = null;
            update_appearance();
        }
        else
            to_chat(user, span_warning(I18n.T("[] fails to implant [].", this, target)));
        return null;
    }

    // tg: code/game/objects/items/implants/implanter.dm:44
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (imp == null && imp_type != null)
            imp = Dm.@new(imp_type, this) as obj_item_implant;
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/implants/implanter.dm:50
    public override async DmTask<object> nameformat(object input = null, mob_living user = null)
    {
        return Dm.text("implanter[]", Dm.truthy(input) ? Dm.text(" ([])", input) : null);
    }
}
