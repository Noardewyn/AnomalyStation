// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/lifesteal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E4b (D327): the whole file (the venus human trap heals by its bites). The var block is generated (D111).

/**
 * Heals the user (if attached to an item) or the mob itself (if attached to a hostile simple mob)
 * by a flat amount whenever a successful attack is performed against another living mob.
 */
// tg: code/datums/elements/lifesteal.dm:5
public partial class datum_element_lifesteal : datum_element
{
    /// static list shared that tells which order of damage types to prioritize (var/static/list, rules 10.6)
    // tg: code/datums/elements/lifesteal.dm:11
    public static ref DmList damage_heal_order => ref TgState.Current.datum_element_lifesteal__damage_heal_order;

    // tg: code/datums/elements/lifesteal.dm:13
    [DmParams("target", "flat_heal")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object flat_heal = Dm.arg(__extra, 0) ?? 10;
        object dot = await base.Attach(target, __extra);
        this.flat_heal = flat_heal;
        await target.AddElementTrait(TRAIT_ON_HIT_EFFECT, REF(this), typeof(datum_element_on_hit_effect));
        RegisterSignal(target, COMSIG_ON_HIT_EFFECT, nameof(do_lifesteal));
        return dot;
    }

    // tg: code/datums/elements/lifesteal.dm:19
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, COMSIG_ON_HIT_EFFECT);
        REMOVE_TRAIT(source, TRAIT_ON_HIT_EFFECT, REF(this));
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/lifesteal.dm:24
    [SignalHandler]
    public virtual void do_lifesteal(datum source = null, atom heal_target = null, atom damage_target = null, object hit_zone = null, object throw_hit = null)
    {
        if (isliving(heal_target) && isliving(damage_target))
        {
            mob_living healing = heal_target as mob_living;
            mob_living damaging = damage_target as mob_living;
            if (damaging.stat != DEAD)
                Waiting.NoSleep(healing.heal_ordered_damage(flat_heal, damage_heal_order));
        }
    }
}

public sealed partial class TgState
{
    // tg: code/datums/elements/lifesteal.dm:11  var/static/list/damage_heal_order = list(BRUTE, BURN, OXY)
    internal DmList datum_element_lifesteal__damage_heal_order = Dm.list(BRUTE, BURN, OXY);
}
