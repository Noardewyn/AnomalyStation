// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/bounties/chef.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4c (D298): the whole file; the chef's bounties are var blocks (generated) but the soup's. Its wanted reagent is a typepath
// (DV409, `reagent.cs`).

// tg: code/modules/cargo/bounties/chef.dm:10
public partial class datum_bounty_reagent_chef_soup : datum_bounty_reagent_chef
{
    // tg: code/modules/cargo/bounties/chef.dm:14
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        required_volume = Dm.num(pick(10f, 15f, 20f, 25f));
        wanted_reagent = pick(subtypesof(typeof(datum_reagent_consumable_nutriment_soup)) as DmList);
        reward = CARGO_CRATE_VALUE * round(required_volume / 3);
        // In the future there could be tiers of soup bounty corresponding to soup difficulty
        // (IE, stew is harder to make than tomato soup, so it should reward more)
        description = Dm.text("[][]", description, I18n.T(" Send us [] units of [].", required_volume, Dm.initial((Type)wanted_reagent, (datum_reagent p) => p.name)));
    }
}
