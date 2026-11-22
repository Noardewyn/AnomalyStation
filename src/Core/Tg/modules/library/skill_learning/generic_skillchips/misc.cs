// Translated from tgstation (snapshot 1d99bd7f): code/modules/library/skill_learning/generic_skillchips/misc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the file's procs: the suspicious brainwashing chip's examine line and headache, and the Mast-Angl-Er chip's
// fishing tip action. The other chips of the file are var blocks (their traits), generated (D111).

// tg: code/modules/library/skill_learning/generic_skillchips/misc.dm:86
public partial class obj_item_skillchip_brainwashing : obj_item_skillchip
{
    // tg: code/modules/library/skill_learning/generic_skillchips/misc.dm:95
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_warning(I18n.T("It seems to have been corroded over time, putting this in your head may not be the best idea...")));
        return dot;
    }

    // tg: code/modules/library/skill_learning/generic_skillchips/misc.dm:99
    public override async DmTask on_activate(mob_living_carbon user = null, float? silent = null)
    {
        silent ??= FALSE;
        to_chat(user, span_danger(I18n.T("You get a pounding headache as the chip sends corrupt memories into your head!")));
        await user.adjust_organ_loss(ORGAN_SLOT_BRAIN, 20);
        await base.on_activate(user, silent);
    }
}

// tg: code/modules/library/skill_learning/generic_skillchips/misc.dm:145
public partial class datum_action_cooldown_fishing_tip : datum_action_cooldown
{
    // tg: code/modules/library/skill_learning/generic_skillchips/misc.dm:154
    public override async DmTask<object> Activate(atom target = null)
    {
        object dot = await base.Activate(target);
        send_tip_of_the_round(owner, pick(GLOB.fishing_tips), source: I18n.T("Ancient fishing wisdom"));
        return dot;
    }
}
