// Translated from tgstation (snapshot 1d99bd7f): code/datums/mutations/passive.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the clever mutation (syndicate G4c, D316: crewlike_monkify locks it on the monkey agent) and biotech compatibility
// (gaps P3, D327).

// tg: code/datums/mutations/passive.dm:1
public partial class datum_mutation_biotechcompat : datum_mutation
{
    // tg: code/datums/mutations/passive.dm:7
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        await owner.adjust_skillchip_complexity_modifier(1);
        return dot;
    }

    // tg: code/datums/mutations/passive.dm:13
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        await owner.adjust_skillchip_complexity_modifier(-1);
        return await base.on_losing(owner);
    }
}

// tg: code/datums/mutations/passive.dm:17
public partial class datum_mutation_clever : datum_mutation
{
    // tg: code/datums/mutations/passive.dm:25
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        owner.add_traits(Dm.list(TRAIT_ADVANCEDTOOLUSER, TRAIT_LITERATE), GENETIC_MUTATION);
        return dot;
    }

    // tg: code/datums/mutations/passive.dm:31
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        owner.remove_traits(Dm.list(TRAIT_ADVANCEDTOOLUSER, TRAIT_LITERATE), GENETIC_MUTATION);
        return dot;
    }
}
