// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/reagents/catalyst_reagents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P5): the catalyst agents' speed, pH and heat modifiers on a reacting holder (equilibrium.dm:141), the
// catalyst precursors and the general and medicine catalysts. The var blocks are generated.

///These alter reaction conditions while they're in the beaker
// tg: code/modules/reagents/chemistry/reagents/catalyst_reagents.dm:2
public partial class datum_reagent_catalyst_agent : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/catalyst_reagents.dm:11
    public virtual object consider_catalyst(datum_equilibrium equilibrium = null)
    {
        foreach (object _product in Dm.each(equilibrium.reaction.results))
        {
            if (ispath(_product, target_reagent_type))
                return TRUE;
        }
        return FALSE;
    }
}

// tg: code/modules/reagents/chemistry/reagents/catalyst_reagents.dm:17
public partial class datum_reagent_catalyst_agent_speed : datum_reagent_catalyst_agent
{
    // tg: code/modules/reagents/chemistry/reagents/catalyst_reagents.dm:20
    public override object consider_catalyst(datum_equilibrium equilibrium = null)
    {
        object dot = base.consider_catalyst(equilibrium);
        if (Dm.truthy(dot))
        {
            equilibrium.speed_mod = Dm.num(creation_purity) * modifier; //So a purity 1 = the modifier, and a purity 0 = the inverse modifier. For this we don't want a negative speed_mod (I have no idea what happens if we do)
            equilibrium.time_deficit = Dm.num(equilibrium.time_deficit) + (Dm.num(creation_purity)) * (0.05f * modifier); //give the reaction a little boost too (40% faster)
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/catalyst_reagents.dm:26
public partial class datum_reagent_catalyst_agent_ph : datum_reagent_catalyst_agent
{
    // tg: code/modules/reagents/chemistry/reagents/catalyst_reagents.dm:29
    public override object consider_catalyst(datum_equilibrium equilibrium = null)
    {
        object dot = base.consider_catalyst(equilibrium);
        if (Dm.truthy(dot))
            equilibrium.h_ion_mod = ((Dm.num(creation_purity) - 0.5f) * 2) * modifier; //So a purity 1 = the modifier, and a purity 0 = the inverse modifier
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/catalyst_reagents.dm:34
public partial class datum_reagent_catalyst_agent_temperature : datum_reagent_catalyst_agent
{
    // tg: code/modules/reagents/chemistry/reagents/catalyst_reagents.dm:37
    public override object consider_catalyst(datum_equilibrium equilibrium = null)
    {
        object dot = base.consider_catalyst(equilibrium);
        if (Dm.truthy(dot))
            equilibrium.thermic_mod = ((Dm.num(creation_purity) - 0.5f) * 2) * modifier; //So a purity 1 = the modifier, and a purity 0 = the inverse modifier
        ///Note from confused guy: It looks like this makes endothermic reactions suddenly become violently exothermic and visa versa at low purities. Wacky.
        return dot;
    }
}
