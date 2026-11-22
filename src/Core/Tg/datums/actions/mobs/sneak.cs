// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/mobs/sneak.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file, the sneak the alien sentinel has (its fade is the mob's alpha, which the client draws). The var
// block is generated (D111).

// tg: code/datums/actions/mobs/sneak.dm:1
public partial class datum_action_cooldown_mob_cooldown_sneak : datum_action_cooldown_mob_cooldown
{
    // tg: code/datums/actions/mobs/sneak.dm:16
    [DmParams("remove_from")]
    public override async DmTask Remove(mob remove_from = null)
    {
        if (HAS_TRAIT(remove_from, TRAIT_SNEAK))
        {
            remove_from.alpha = Dm.initial(remove_from, (atom p) => p.alpha);
            REMOVE_TRAIT(remove_from, TRAIT_SNEAK, ACTION_TRAIT);
        }

        await base.Remove(remove_from);
    }

    // tg: code/datums/actions/mobs/sneak.dm:23
    public override async DmTask<object> Activate(atom target = null)
    {
        if (HAS_TRAIT(owner, TRAIT_SNEAK))
        {
            // It's safest to go to the initial alpha of the mob.
            // Otherwise we get permanent invisbility exploits.
            animate(owner, Dm.named("alpha", Dm.initial(owner, (atom p) => p.alpha)), Dm.named("time", animation_time));
            owner.balloon_alert(owner, I18n.T("you reveal yourself"));
            REMOVE_TRAIT(owner, TRAIT_SNEAK, ACTION_TRAIT);
        }

        else
        {
            animate(owner, Dm.named("alpha", sneak_alpha), Dm.named("time", animation_time));
            owner.balloon_alert(owner, I18n.T("you blend into the environment"));
            ADD_TRAIT(owner, TRAIT_SNEAK, ACTION_TRAIT);
        }

        return TRUE;
    }
}
