// Translated from tgstation (snapshot 1d99bd7f): code/datums/mutations/active.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): Adrenaline rush and its action. The var blocks are generated (D111).

// tg: code/datums/mutations/active.dm:1
public partial class datum_mutation_adrenaline_rush : datum_mutation
{
    // tg: code/datums/mutations/active.dm:13
    public override async DmTask<object> setup()
    {
        object dot = await base.setup();
        if (dot is not datum_action_cooldown_adrenaline to_modify) // null or invalid
            return dot;

        to_modify.adrenaline_amount = 10 * GET_MUTATION_POWER(this);
        to_modify.comedown_amount = 7 / GET_MUTATION_SYNCHRONIZER(this);
        return dot;
    }
}

// tg: code/datums/mutations/active.dm:22
public partial class datum_action_cooldown_adrenaline : datum_action_cooldown
{
    // tg: code/datums/mutations/active.dm:36
    public override async DmTask<object> Activate(atom target = null)
    {
        mob_living_carbon cast_on = target as mob_living_carbon;
        await base.Activate(target);
        to_chat(cast_on, span_userdanger(I18n.T("You feel pumped up! It's time to GO!")));
        await ((datum_reagents)cast_on.reagents).add_reagent(typeof(datum_reagent_drug_pumpup), adrenaline_amount);
        await ((datum_reagents)cast_on.reagents).add_reagent(typeof(datum_reagent_medicine_synaptizine), adrenaline_amount);
        await ((datum_reagents)cast_on.reagents).add_reagent(typeof(datum_reagent_determination), adrenaline_amount);
        _addtimer(Dm.@new<datum_callback>(this, nameof(get_tired), cast_on), 25 * SECONDS, TIMER_UNIQUE | TIMER_OVERRIDE, file: "code/datums/mutations/active.dm", line: 42);
        return TRUE;
    }

    // tg: code/datums/mutations/active.dm:45
    public virtual async DmTask get_tired(mob_living_carbon cast_on = null)
    {
        to_chat(cast_on, span_danger(I18n.T("Your adrenaline rush makes way for a bout of nausea and a deep feeling of exhaustion in your muscles.")));
        await ((datum_reagents)cast_on.reagents).add_reagent(typeof(datum_reagent_peaceborg_tire), comedown_amount);
        await ((datum_reagents)cast_on.reagents).add_reagent(typeof(datum_reagent_peaceborg_confuse), comedown_amount);
        await cast_on.set_dizzy_if_lower(10 * SECONDS);
    }
}
