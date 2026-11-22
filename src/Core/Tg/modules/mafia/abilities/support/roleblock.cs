// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/abilities/support/roleblock.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): The lawyer's advice, which roleblocks a player for the night. The var block is generated (D111).

/**
 * Roleblock
 *
 * During the night, prevents a player from using their role abilities.
 * This is done before anything else.
 */
// tg: code/modules/mafia/abilities/support/roleblock.dm:7
public partial class datum_mafia_ability_roleblock : datum_mafia_ability
{
    // tg: code/modules/mafia/abilities/support/roleblock.dm:12
    public override async DmTask<object> perform_action_target(datum_mafia_controller game = null, datum_mafia_role day_target = null)
    {
        object dot = await base.perform_action_target(game, day_target);
        if (!Dm.truthy(dot))
            return FALSE;

        target_role.role_flags = Dm.num(Dm.bitor(target_role.role_flags, ROLE_ROLEBLOCKED));
        return TRUE;
    }

    // tg: code/modules/mafia/abilities/support/roleblock.dm:20
    [SignalHandler]
    public override void clean_action_refs(datum_mafia_controller game = null)
    {
        if (target_role != null)
            target_role.role_flags = Dm.num(Dm.bitand(target_role.role_flags, ~ROLE_ROLEBLOCKED));
        base.clean_action_refs(game);
    }
}
