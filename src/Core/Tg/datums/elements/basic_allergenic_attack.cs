// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/basic_allergenic_attack.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

/// Attach to basic mobs, when they attack, they may trigger a food-based allergic reaction in the target.
// tg: code/datums/elements/basic_allergenic_attack.dm:2
public partial class datum_element_basic_allergenic_attack : datum_element
{
    // tg: code/datums/elements/basic_allergenic_attack.dm:12
    [DmParams("target", "allergen", "allergen_chance", "histamine_add")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isbasicmob(target))
            return ELEMENT_INCOMPATIBLE;

        this.allergen = Dm.num(Dm.arg(__extra, 0) ?? NONE);
        this.allergen_chance = Dm.num(Dm.arg(__extra, 1) ?? 100f);
        this.histamine_add = Dm.num(Dm.arg(__extra, 2) ?? 0f);
        RegisterSignal(target, COMSIG_HOSTILE_POST_ATTACKINGTARGET, nameof(trigger_allergy));
        return dot;
    }

    // tg: code/datums/elements/basic_allergenic_attack.dm:22
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_HOSTILE_POST_ATTACKINGTARGET);
        return dot;
    }

    // tg: code/datums/elements/basic_allergenic_attack.dm:26
    [SignalHandler]
    public virtual void trigger_allergy(mob_living source = null, atom target_ = null, object result = null)
    {
        if (Dm.num(result) <= 0 || target_ is not mob_living target)
            return;
        if (!Dm.truthy(target.can_inject(source)))
            return;

        Waiting.Detach(target.check_allergic_reaction(allergen, allergen_chance, histamine_add));
    }
}
