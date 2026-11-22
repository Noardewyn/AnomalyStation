// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/tiny_mob_hunter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

/// Deals bonus brute damage to smaller mobs
// tg: code/datums/elements/tiny_mob_hunter.dm:3
public partial class datum_element_tiny_mob_hunter : datum_element
{
    // tg: code/datums/elements/tiny_mob_hunter.dm:10
    [DmParams("target", "target_size", "bonus_damage")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object target_size = Dm.arg(__extra, 0) ?? MOB_SIZE_TINY;
        object bonus_damage = Dm.arg(__extra, 1) ?? 10f;
        object dot = await base.Attach(target, __extra);
        if (!isanimal_or_basicmob(target)) // No post-attack signal for carbons, you can add one if you really want to put this on one
            return ELEMENT_INCOMPATIBLE;

        this.target_size = target_size;
        this.bonus_damage = bonus_damage;
        RegisterSignal(target, COMSIG_HOSTILE_POST_ATTACKINGTARGET, nameof(on_attacked_target));
        return dot;
    }

    // tg: code/datums/elements/tiny_mob_hunter.dm:19
    public override object Detach(datum target = null, params object[] __extra)
    {
        UnregisterSignal(target, COMSIG_HOSTILE_POST_ATTACKINGTARGET);
        return base.Detach(target, __extra);
    }

    /// Applies a bonus following the attack
    // tg: code/datums/elements/tiny_mob_hunter.dm:24
    [SignalHandler]
    public virtual void on_attacked_target(mob_living hunter = null, atom target = null)
    {
        if (!isliving(target))
            return;
        mob_living prey = target as mob_living;
        if (Dm.num(prey.mob_size) > Dm.num(target_size))
            return;
        Waiting.NoSleep(prey.apply_damage(Dm.num(bonus_damage), BRUTE, hunter.zone_selected));
    }
}
