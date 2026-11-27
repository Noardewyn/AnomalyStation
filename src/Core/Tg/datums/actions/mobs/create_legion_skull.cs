// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/mobs/create_legion_skull.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

// tg: code/datums/actions/mobs/create_legion_skull.dm:1
public partial class datum_action_cooldown_mob_cooldown_create_legion_skull : datum_action_cooldown_mob_cooldown
{
    // tg: code/datums/actions/mobs/create_legion_skull.dm:8
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        disable_cooldown_actions();
        create(target_atom);
        StartCooldown();
        enable_cooldown_actions();
        return TRUE;
    }

    /// Creates a new skull assigned to the owner of this action
    // tg: code/datums/actions/mobs/create_legion_skull.dm:16
    public virtual void create(atom target = null)
    {
        mob_living_basic_mining_legion_brood minion = Dm.@new<mob_living_basic_mining_legion_brood>(owner.loc);
        minion.assign_creator(owner as mob_living);
        (minion.ai_controller as datum_ai_controller).set_blackboard_key(BB_CURRENT_TARGET, target);
    }
}
