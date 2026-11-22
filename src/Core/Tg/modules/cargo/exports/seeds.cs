// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/exports/seeds.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4b (D327): the whole file: a new plant species sample pays by its rarity, an improved one by its potency gain. The var blocks are generated.

// tg: code/modules/cargo/exports/seeds.dm:1
public partial class datum_export_seed : datum_export
{
    // tg: code/modules/cargo/exports/seeds.dm:9  var/static/list/discovered_plants (a type static, rules 10.6)
    private static DmList discovered_plants => TgState.Current.datum_export_seed__discovered_plants ??= Dm.list();

    // tg: code/modules/cargo/exports/seeds.dm:13
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        foreach (Type seed in Dm.each_cast<Type>(subtypesof(typeof(obj_item_seeds)) as DmList))
        {
            float rarity = Dm.initial(seed, (obj_item_seeds p) => p.rarity);
            if (rarity > highest_rarity)
                highest_rarity = rarity;
        }
    }

    // tg: code/modules/cargo/exports/seeds.dm:19
    public override object get_base_cost(obj exported_item = null)
    {
        var S = (obj_item_seeds)exported_item;
        object discovered = discovered_plants[S.type];
        if (!Dm.truthy(needs_discovery) && Dm.truthy(discovered))
            return 0;
        if (Dm.truthy(needs_discovery) && !Dm.truthy(discovered))
            return 0;
        return Dm.num(base.get_base_cost(exported_item)) * (S.rarity / highest_rarity); // That's right, no bonus for potency. Send a crappy sample first to "show improvement" later.
    }

    // tg: code/modules/cargo/exports/seeds.dm:27
    public override async DmTask<object> sell_object(obj sold_item = null, datum_export_report report = null, object dry_run = null, object apply_elastic = null)
    {
        object dot = await base.sell_object(sold_item, report, dry_run, apply_elastic);
        if (Dm.truthy(dot) && !Dm.truthy(dry_run))
            discovered_plants[sold_item.type] = ((obj_item_seeds)sold_item).potency;
        return dot;
    }
}

// tg: code/modules/cargo/exports/seeds.dm:32
public partial class datum_export_seed_potency : datum_export_seed
{
    // tg: code/modules/cargo/exports/seeds.dm:37
    public override object get_base_cost(obj exported_item = null)
    {
        var S = (obj_item_seeds)exported_item;
        return Dm.num(base.get_base_cost(exported_item)) * (Dm.num(S.potency) - Dm.num(TgState.Current.datum_export_seed__discovered_plants?[S.type]));
    }
}

public sealed partial class TgState
{
    // tg: code/modules/cargo/exports/seeds.dm:9  var/static/list/discovered_plants
    internal DmList datum_export_seed__discovered_plants;
}
