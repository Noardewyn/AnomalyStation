// Translated from tgstation (snapshot 1d99bd7f): code/datums/mutations/webbing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): Webbing production: the geneticists' web and its weaver's passage. The var blocks are generated (D111).

//spider webs
// tg: code/datums/mutations/webbing.dm:2
public partial class datum_mutation_webbing : datum_mutation
{
    // tg: code/datums/mutations/webbing.dm:11
    public override async DmTask<object> setup()
    {
        object dot = await base.setup();
        if (dot is not datum_action_cooldown_mob_cooldown_lay_web_genetic to_modify) // null or invalid
            return dot;

        if (GET_MUTATION_ENERGY(this) == 1) // Energetic chromosome outputs a value less than 1 when present, 1 by default
        {
            to_modify.webbing_time = Dm.initial(to_modify, (datum_action_cooldown_mob_cooldown_lay_web_genetic w) => w.webbing_time);
            return dot;
        }
        to_modify.webbing_time = 2 * SECONDS; // Spin webs faster but not more often
        return dot;
    }

    // tg: code/datums/mutations/webbing.dm:23
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        ADD_TRAIT(owner, TRAIT_WEB_WEAVER, GENETIC_MUTATION);
        return dot;
    }

    // tg: code/datums/mutations/webbing.dm:29
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        REMOVE_TRAIT(owner, TRAIT_WEB_WEAVER, GENETIC_MUTATION);
        return dot;
    }
}
