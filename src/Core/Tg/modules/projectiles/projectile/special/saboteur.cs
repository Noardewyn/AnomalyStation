// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/special/saboteur.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The file whole: the /atom root of on_saboteur (silicons SI2, D326: a cyborg's headlamp answers the light disruptor); gaps GU1 (D333):
// the SC/FISHER disruptor's bolt (hitscan, no damage: it disrupts the target and, on a living target, all its gear) and its melee and
// thrown variant (a longer disruption). The var blocks are generated (D111).

// tg: code/modules/projectiles/projectile/special/saboteur.dm:2
public partial class atom
{
    ///Override on subtype to add behaviour. Whatever happens when we are sabotaged
    // tg: code/modules/projectiles/projectile/special/saboteur.dm:2
    public virtual async DmTask<object> on_saboteur(datum source = null, object disrupt_duration = null)
    {
        if (Dm.truthy(Dm.bitand(!Dm.truthy(_listen_lookup?[COMSIG_ATOM_SABOTEUR_ACT]) ? NONE : _SendSignal(COMSIG_ATOM_SABOTEUR_ACT, Dm.list(this, disrupt_duration)), COMSIG_SABOTEUR_SUCCESS))) //Signal handles datums for the most part
            return TRUE;
        return null;
    }
}

// tg: code/modules/projectiles/projectile/special/saboteur.dm:7
public partial class obj_projectile_energy_fisher : obj_projectile_energy
{
    // tg: code/modules/projectiles/projectile/special/saboteur.dm:19
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        DmList things_to_disrupt = Dm.list(target);
        if (isliving(target))
        {
            mob_living live_target = target as mob_living;
            Dm.append(ref things_to_disrupt, live_target.get_all_gear(Dm.bitor(Dm.bitor(INCLUDE_PROSTHETICS, INCLUDE_ABSTRACT), INCLUDE_ACCESSORIES)));
        }

        float success = FALSE;
        foreach (object disrupted in Dm.each(things_to_disrupt))
        {
            if (Dm.truthy(await ((atom)disrupted).on_saboteur(this, disrupt_duration)))
                success = TRUE;
        }

        if (Dm.truthy(success) && ismob(firer))
            target.balloon_alert(firer as mob, I18n.T("disrupted"));
        return dot;
    }
}
