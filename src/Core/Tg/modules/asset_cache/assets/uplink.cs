// Translated from tgstation (snapshot 1d99bd7f): code/modules/asset_cache/assets/uplink.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A2 (D316): a seam for tg's uplink asset (DV44: the asset transport is the client's). tg's SSearly_assets makes
// /datum/asset/json/uplink, whose generate() fills SStraitor's uplink entries and sends the uplink's items and categories to the
// browser; the port runs that body from SStraitor's Initialize (load_uplink_asset) and keeps its data for the Uplink window's static
// data (the asset type itself stays inert: /datum/asset is not ported).

public static partial class GlobalProcs
{
    /// The uplink asset's data (tg's uplink.json: "items" and "categories"), made once per world by SStraitor's Initialize.
    public static DmList uplink_json => TgState.Current.uplink_json;

    /// Sends information needed for uplinks
    // tg: code/modules/asset_cache/assets/uplink.dm:6  /datum/asset/json/uplink/generate()
    public static async DmTask load_uplink_asset()
    {
        DmList data = Dm.list();
        DmList categories = Dm.list();
        DmList items = Dm.list();
        foreach (object category in Dm.each(subtypesof(typeof(datum_uplink_category))))
            Dm.append(categories, category);
        sortTim(categories, "/proc/" + nameof(cmp_uplink_category_desc));

        DmList new_categories = Dm.list();
        foreach (object category in Dm.each(categories))
            Dm.append(new_categories, Dm.initial((Type)category, (datum_uplink_category p) => p.name));
        categories = new_categories;

        foreach (Type item_path in Dm.each<Type>(subtypesof(typeof(datum_uplink_item))))
        {
            datum_uplink_item item = await Dm.new_async(item_path) as datum_uplink_item;
            Type actual_item = item.item;
            // DEVIATION(DV343): the uplink lists no entry whose item, procs or system are not live (uplink_item_hidden)
            if (item.item != null && !uplink_item_hidden(item))
            {
                Dm.append(items, Dm.list(Dm.list(
                    Dm.kv("id", item_path),
                    Dm.kv("name", item.name),
                    Dm.kv("icon", Dm.initial(actual_item, (atom p) => p.icon)),
                    Dm.kv("icon_state", Dm.initial(actual_item, (atom p) => p.icon_state)),
                    Dm.kv("cost", item.cost),
                    Dm.kv("desc", item.desc),
                    Dm.kv("category", item.category != null ? Dm.initial(item.category, (datum_uplink_category p) => p.name) : null),
                    Dm.kv("purchasable_from", item.purchasable_from),
                    Dm.kv("restricted", item.restricted),
                    Dm.kv("limited_stock", item.limited_stock),
                    Dm.kv("stock_key", item.stock_key),
                    Dm.kv("restricted_roles", item.restricted_roles),
                    Dm.kv("restricted_species", item.restricted_species),
                    Dm.kv("population_minimum", item.population_minimum),
                    Dm.kv("cost_override_string", item.cost_override_string),
                    Dm.kv("lock_other_purchases", item.lock_other_purchases))));
            }
            Dm.append(SStraitor.uplink_items, item);
            SStraitor.uplink_items_by_type[item_path] = item;
        }

        data["items"] = items;
        data["categories"] = categories;
        TgState.Current.uplink_json = data;
    }
}

public sealed partial class TgState
{
    // the uplink asset's data (DV44), load_uplink_asset
    internal DmList uplink_json;
}
