// Translated from tgstation (snapshot 1d99bd7f): code/modules/paperwork/carbonpaper.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S3, D298). The var blocks are generated.

// tg: code/modules/paperwork/carbonpaper.dm:1
public partial class obj_item_paper_carbon : obj_item_paper
{
    // tg: code/modules/paperwork/carbonpaper.dm:8
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(copied))
            icon_state = "paper";
        else
            icon_state = "paper_stack";
        if (Dm.truthy(get_total_length()))
            icon_state = Dm.text("[]_words", icon_state);
        // tg's own: ..() is /obj/item/paper's, which sets the state again (show_written_words is FALSE: initial, "paper_stack")
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/paperwork/carbonpaper.dm:17
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(copied))
            return dot;
        Dm.append(ref dot, span_notice(I18n.T("Right-click to tear off the carbon-copy (you must use both hands).")));
        return dot;
    }

    // tg: code/modules/paperwork/carbonpaper.dm:23
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(copied))
        {
            to_chat(user, span_notice(I18n.T("Take off the carbon copy first.")));
            return CLICK_ACTION_BLOCKING;
        }
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/paperwork/carbonpaper.dm:29
    public virtual async DmTask removecopy(mob_living user = null)
    {
        if (Dm.truthy(copied))
        {
            to_chat(user, span_notice(I18n.T("There are no more carbon copies attached to this paper!")));
            return;
        }

        obj_item_paper copy = this.copy(typeof(obj_item_paper_carbon_copy), loc.drop_location() as atom, FALSE) as obj_item_paper;
        copy.name = I18n.T("\\improper Copy - []", name);
        to_chat(user, span_notice(I18n.T("You tear off the carbon-copy!")));
        copied = TRUE;
        await update_icon_state();
        await user.put_in_hands(copy);
    }

    // tg: code/modules/paperwork/carbonpaper.dm:41
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;

        if (loc == user && Dm.truthy(user.is_holding(this)))
        {
            await removecopy(user as mob_living);
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        return dot;
    }
}
