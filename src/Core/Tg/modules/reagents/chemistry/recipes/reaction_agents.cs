// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/recipes/reaction_agents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P5): the prefactor B reaction's bluespace boost and failures (and its speed agent subtype); the buffers'
// and other agents' reactions are var-only, live under a live parent.

// tg: code/modules/reagents/chemistry/recipes/reaction_agents.dm:63
public partial class datum_chemical_reaction_prefactor_b : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/reaction_agents.dm:83
    public override async DmTask<object> reaction_step(datum_reagents holder = null, datum_equilibrium reaction = null, object delta_t = null, object delta_ph = null, object step_reaction_vol = null)
    {
        object dot = await base.reaction_step(holder, reaction, delta_t, delta_ph, step_reaction_vol);
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_bluespace))))
        {
            await holder.remove_reagent(typeof(datum_reagent_bluespace), 1);
            reaction.delta_t = Dm.num(reaction.delta_t) * 5;
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/recipes/reaction_agents.dm:89
    public override async DmTask<object> overheated(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null, params object[] __extra)
    {
        object dot = await base.overheated(holder, equilibrium, step_volume_added, __extra);
        await explode_shockwave(holder, equilibrium);
        float vol = Dm.max(20, holder.total_volume / 5); //Not letting you have more than 5
        await clear_reagents(holder, vol); //Lest we explode forever
        return dot;
    }

    // tg: code/modules/reagents/chemistry/recipes/reaction_agents.dm:95
    public override async DmTask<object> overly_impure(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null)
    {
        await explode_fire(holder, equilibrium);
        float vol = Dm.max(20, holder.total_volume / 5); //Not letting you have more than 5
        await clear_reagents(holder, vol);
        return null;
    }
}
