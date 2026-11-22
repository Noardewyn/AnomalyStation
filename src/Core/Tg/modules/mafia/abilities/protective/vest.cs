// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/abilities/protective/vest.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): The fugitive's vest, two nights of protection. The var block is generated (D111).

/**
 * Vest
 *
 * During the night, Vesting will prevent the user from dying.
 */
// tg: code/modules/mafia/abilities/protective/vest.dm:9
public partial class datum_mafia_ability_vest : datum_mafia_ability
{
    // tg: code/modules/mafia/abilities/protective/vest.dm:16
    public override object set_target(datum_mafia_role new_target = null)
    {
        object dot = base.set_target(new_target);
        if (!Dm.truthy(dot))
            return FALSE;
        if (Dm.truthy(using_ability))
            RegisterSignal(host_role, COMSIG_MAFIA_ON_KILL, nameof(self_defense));
        else
            UnregisterSignal(host_role, COMSIG_MAFIA_ON_KILL);
        return TRUE;
    }

    // tg: code/modules/mafia/abilities/protective/vest.dm:26
    public override async DmTask<object> perform_action_target(datum_mafia_controller game = null, datum_mafia_role day_target = null)
    {
        object dot = await base.perform_action_target(game, day_target);
        if (!Dm.truthy(dot))
            return FALSE;

        charges--;
        return TRUE;
    }

    // tg: code/modules/mafia/abilities/protective/vest.dm:34
    [SignalHandler]
    public virtual object self_defense(datum source = null, datum_mafia_controller game = null, object attacker = null, object lynch = null)
    {
        host_role.send_message_to_player(span_greentext(I18n.T("Your vest saved you!")));
        return MAFIA_PREVENT_KILL;
    }

    // tg: code/modules/mafia/abilities/protective/vest.dm:39
    [SignalHandler]
    public virtual void end_protection(datum_mafia_controller game = null)
    {
        UnregisterSignal(host_role, COMSIG_MAFIA_ON_KILL);
    }

    // tg: code/modules/mafia/abilities/protective/vest.dm:44
    [SignalHandler]
    public override void clean_action_refs(datum_mafia_controller game = null)
    {
        if (!Dm.truthy(charges))
        {
            Dm.remove(ref host_role.role_unique_actions, this);
            qdel(this);
        }
        base.clean_action_refs(game);
    }
}
