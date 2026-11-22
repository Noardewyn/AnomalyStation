// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/chameleon_skin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: Chameleon Skin, the changeling's chameleon mutation toggled on and off. The var block is
// generated (D111).

// tg: code/modules/antagonists/changeling/powers/chameleon_skin.dm:1
public partial class datum_action_changeling_chameleon_skin : datum_action_changeling
{
    // tg: code/modules/antagonists/changeling/powers/chameleon_skin.dm:11
    [DmParams("user")]
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        mob_living_carbon_human cling = user as mob_living_carbon_human; //SHOULD always be human, because req_human = TRUE
        if (!(cling is mob_living_carbon_human)) // req_human could be done in can_sting stuff.
            return null;
        await base.sting_action(user, target);
        if (Dm.truthy(cling.dna.get_mutation(typeof(datum_mutation_chameleon_changeling))))
            await cling.dna.remove_mutation(typeof(datum_mutation_chameleon_changeling), MUTATION_SOURCE_CHANGELING);
        else
            await cling.dna.add_mutation(typeof(datum_mutation_chameleon_changeling), MUTATION_SOURCE_CHANGELING);
        return TRUE;
    }

    // tg: code/modules/antagonists/changeling/powers/chameleon_skin.dm:22
    public override async DmTask Remove(mob user = null)
    {
        if (Dm.truthy(user.has_dna()))
        {
            mob_living_carbon cling = user as mob_living_carbon;
            await cling.dna.remove_mutation(typeof(datum_mutation_chameleon_changeling), MUTATION_SOURCE_CHANGELING);
        }
        await base.Remove(user);
    }
}
