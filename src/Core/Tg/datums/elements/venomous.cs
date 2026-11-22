// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/venomous.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

/**
 * Venomous element; which makes the attacks of the simplemob attached to poison the enemy.
 */
// tg: code/datums/elements/venomous.dm:7
public partial class datum_element_venomous : datum_element
{
    // Hand-declared object (generated as list): tg's own var holds a number or a list.
    // tg: code/datums/elements/venomous.dm:15
    public object amount_added;

    // tg: code/datums/elements/venomous.dm:7
    public datum_element_venomous()
    {
        element_flags = ELEMENT_BESPOKE;
        argument_hash_start_idx = 2;
    }

    // tg: code/datums/elements/venomous.dm:18
    [DmParams("target", "poison_type", "amount_added", "injection_flags", "thrown_effect")] // gaps P2: the bees pass thrown_effect by name
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        this.reagents = Dm.arg(__extra, 0) as Type;
        this.amount_added = Dm.arg(__extra, 1);
        this.injection_flags = Dm.arg(__extra, 2) ?? NONE;
        this.thrown_effect = Dm.num(Dm.arg(__extra, 3) ?? FALSE);
        await target.AddElementTrait(TRAIT_ON_HIT_EFFECT, REF(this), typeof(datum_element_on_hit_effect));
        RegisterSignal(target, COMSIG_ON_HIT_EFFECT, nameof(do_venom));
        return dot;
    }

    // tg: code/datums/elements/venomous.dm:28
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, COMSIG_ON_HIT_EFFECT);
        REMOVE_TRAIT(source, TRAIT_ON_HIT_EFFECT, REF(this));
        return base.Detach(source, __extra);
    }

    ///Signal proc for [COMSIG_ON_HIT_EFFECT]
    // tg: code/datums/elements/venomous.dm:33
    public virtual async DmTask do_venom(datum element_owner = null, atom venom_source = null, mob_living target = null, object hit_zone = null, object throw_hit = null)
    {
        if ((Dm.truthy(throw_hit) && !Dm.truthy(thrown_effect)) || target == null)
            return;
        if (target.stat == DEAD)
            return;
        if (isliving(element_owner) && !Dm.truthy(target.try_inject(element_owner as mob, hit_zone, injection_flags)))
            return;
        object final_amount_added;
        if (amount_added is DmList amount_range)
            final_amount_added = rand(Dm.num(amount_range[1]), Dm.num(amount_range[2]));
        else
            final_amount_added = amount_added;
        datum_reagents tmp_holder = Dm.@new<datum_reagents>(final_amount_added);
        // tmp_holder.my_atom = src: an element, not an atom, in a slot the C# types as atom; nothing the injection reads uses it.
        await tmp_holder.add_reagent(reagents, final_amount_added);
        await tmp_holder.trans_to(
            target: target,
            amount: Dm.num(tmp_holder.total_volume),
            multiplier: 1,
            methods: INJECT,
            transferred_by: ismob(element_owner) ? element_owner as mob : null,
            show_message: FALSE
        );
        qdel(tmp_holder);
    }
}
