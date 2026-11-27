// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/mobs/meteors.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

// tg: code/datums/actions/mobs/meteors.dm:1
public partial class datum_action_cooldown_mob_cooldown_meteors : datum_action_cooldown_mob_cooldown
{
    // tg: code/datums/actions/mobs/meteors.dm:8
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        disable_cooldown_actions();
        create_meteors(target_atom);
        StartCooldown();
        enable_cooldown_actions();
        return TRUE;
    }

    // tg: code/datums/actions/mobs/meteors.dm:15
    public virtual void create_meteors(atom target = null)
    {
        if (target == null)
            return;
        target.visible_message(span_boldwarning(I18n.T("Fire rains from the sky!")));
        turf targetturf = get_turf(target);
        foreach (object turf__it in Dm.each(RANGE_TURFS(9, targetturf)))
        {
            turf turf = (turf)turf__it;
            if (prob(11))
                Dm.@new<obj_effect_temp_visual_target>(turf);
        }
    }
}
