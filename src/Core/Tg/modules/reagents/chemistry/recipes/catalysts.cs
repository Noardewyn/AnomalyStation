// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/recipes/catalysts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P5): the catalyst precursor's gold boost and the medical speed catalyst's failures; the var-only catalyst
// reactions are live under a live parent.

// tg: code/modules/reagents/chemistry/recipes/catalysts.dm:21
public partial class datum_chemical_reaction_catalyst_precursor_ph : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/catalysts.dm:40
    public override async DmTask<object> reaction_step(datum_reagents holder = null, datum_equilibrium reaction = null, object delta_t = null, object delta_ph = null, object step_reaction_vol = null)
    {
        object dot = await base.reaction_step(holder, reaction, delta_t, delta_ph, step_reaction_vol);
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_gold))))
        {
            await holder.remove_reagent(typeof(datum_reagent_gold), 1);
            reaction.delta_t = Dm.num(reaction.delta_t) * 5;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/recipes/catalysts.dm:75
public partial class datum_chemical_reaction_medical_speed_catalyst : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/catalysts.dm:94
    public override async DmTask<object> overheated(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null, params object[] __extra)
    {
        await explode_invert_smoke(holder, equilibrium); //Will be better when the inputs have proper invert chems
        return null;
    }

    // tg: code/modules/reagents/chemistry/recipes/catalysts.dm:97
    public override async DmTask<object> overly_impure(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null)
    {
        await explode_invert_smoke(holder, equilibrium);
        return null;
    }
}
