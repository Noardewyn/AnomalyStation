// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/orders/order_items/order_datum.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4d (D298): the whole file, the order consoles' orderable item, and the one item with a proc of its own (the luxury medipen,
// `order_items/mining/order_consumables.dm:13`); the other orderable items are var blocks (generated).

///A datum for chef ordering options from the chef's computer.
// tg: code/game/machinery/computer/orders/order_items/order_datum.dm:2
public partial class datum_orderable_item : datum
{
    // tg: code/game/machinery/computer/orders/order_items/order_datum.dm:16
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (!Dm.truthy(category_index))
            throw Dm.CRASH(Dm.text("[] doesn't have a category_index assigned!", type), "code/game/machinery/computer/orders/order_items/order_datum.dm", 19);
        if (purchase_path == null)
            throw Dm.CRASH(Dm.text("[] orderable item datum with no item path was created!", type), "code/game/machinery/computer/orders/order_items/order_datum.dm", 21);
        if (!Dm.truthy(name))
            name = Dm.initial(purchase_path, (atom p) => p.name);
        if (!Dm.truthy(desc))
            desc = Dm.initial(purchase_path, (atom p) => p.desc);
        if (!Dm.truthy(max_per_order))
            max_per_order = cost_per_order <= 10 ? 50f : (cost_per_order <= 25 ? 20f : 10f);
    }

    // tg: code/game/machinery/computer/orders/order_items/order_datum.dm:29
    public virtual object get_purchased_item(mob_living user = null, obj_item_card_id card = null) => purchase_path;
}

// tg: code/game/machinery/computer/orders/order_items/mining/order_consumables.dm:8
public partial class datum_orderable_item_consumables_luxury_pen : datum_orderable_item_consumables
{
    // slime accounts get ourple pen :3
    // tg: code/game/machinery/computer/orders/order_items/mining/order_consumables.dm:13
    public override object get_purchased_item(mob_living user = null, obj_item_card_id card = null)
    {
        var record = find_record(card.registered_account?.account_holder) as datum_record_crew;
        if (Dm.truthy(cmptext(trimtext(record?.species), Dm.initial(typeof(datum_species_jelly), (datum_species p) => p.name))))
            return typeof(obj_item_reagent_containers_hypospray_medipen_survival_luxury_purple);
        return base.get_purchased_item(user, card);
    }
}
