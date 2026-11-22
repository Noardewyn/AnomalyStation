// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/netpod/signals.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the netpod's signal handlers: its breaking and damage, the completed domain's points on the
// occupant's account, the scrubbed domain's avatar and the power loss.

// tg: code/modules/bitrunning/netpod/signals.dm:1
public partial class obj_machinery_netpod : obj_machinery
{
    /// Machine has been broken - handles signals and reverting sprites
    // tg: code/modules/bitrunning/netpod/signals.dm:2
    [SignalHandler]
    public virtual void on_broken(datum source = null)
    {
        sever_connection();
    }

    /// Checks the integrity, alerts occupants
    // tg: code/modules/bitrunning/netpod/signals.dm:9
    [SignalHandler]
    public virtual void on_damage_taken(datum source = null, object damage_amount = null)
    {
        if (occupant == null || !Dm.truthy(connected))
            return;

        float total = max_integrity - Dm.num(damage_amount);
        float integrity = (Dm.num(atom_integrity) / total) * 100;
        if (integrity > 50)
            return;

        SEND_SIGNAL(COMSIG_BITRUNNER_NETPOD_INTEGRITY, Dm.list(this));
    }

    /// Puts points on the current occupant's card account
    // tg: code/modules/bitrunning/netpod/signals.dm:24
    [SignalHandler]
    public virtual void on_domain_complete(datum source = null, atom_movable crate = null, object reward_points = null)
    {
        if (occupant == null || !Dm.truthy(connected))
            return;

        mob_living player = occupant as mob_living;

        datum_bank_account account = player.get_bank_account() as datum_bank_account;
        if (account == null)
            return;

        account.bitrunning_points += Dm.num(reward_points) * 100;
    }

    /// The domain has been fully purged, so we should double check our avatar is deleted
    // tg: code/modules/bitrunning/netpod/signals.dm:40
    [SignalHandler]
    public virtual void on_domain_scrubbed(datum source = null)
    {
        mob avatar = avatar_ref?.resolve() as mob;
        if (avatar == null)
            return;

        QDEL_NULL(ref avatar);
    }

    /// Boots out anyone in the machine && opens it
    // tg: code/modules/bitrunning/netpod/signals.dm:51
    [SignalHandler]
    public virtual void on_power_loss(datum source = null)
    {
        if (Dm.truthy(state_open))
            return;

        if (occupant == null || !Dm.truthy(connected))
        {
            connected = FALSE;
            Waiting.NoSleep(open_machine());
            return;
        }

        sever_connection();
    }
}
