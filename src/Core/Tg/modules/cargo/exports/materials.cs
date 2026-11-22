// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/exports/materials.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4b (D327): the whole file: materials sell by the sheets of their material in an item, the market's at the stock market's price
// (which a sale lowers, and whose stock it raises), a stock block at its locked or liquid value. The var blocks are generated.

// tg: code/modules/cargo/exports/materials.dm:1
public partial class datum_export_material : datum_export
{
    // tg: code/modules/cargo/exports/materials.dm:42  var/static/list/shared_exports (a proc-level static, rules 10.6)
    private static ref DmList init_export_types__shared_exports => ref TgState.Current.datum_export_material__init_export_types__shared_exports;

    // tg: code/modules/cargo/exports/materials.dm:21
    public override async DmTask New(params object[] __extra)
    {
        object temp_exports = export_types;
        export_types = null;
        await base.New(__extra);
        export_types = init_export_types(temp_exports);
    }

    /**
     * Inits an list of exports for this type.
     * For performance, unless use_shared_exports is FALSE,
     * this returns a static list of types we check for export
     * that is shared by everything under the same abstract type.
     *
     * Arguments
     * * export_data - exports whos type cache we are trying to create
    */
    // tg: code/modules/cargo/exports/materials.dm:36  (PROTECTED_PROC)
    [DmProtected]
    public virtual object init_export_types(object export_data = null)
    {
        if (!Dm.truthy(use_shared_exports))
            return generate_export_typecache(export_data);

        init_export_types__shared_exports ??= Dm.list();
        if (init_export_types__shared_exports[abstract_type] == null)
            init_export_types__shared_exports[abstract_type] = generate_export_typecache(export_data);

        return init_export_types__shared_exports[abstract_type];
    }

    // tg: code/modules/cargo/exports/materials.dm:48
    public virtual object generate_export_typecache(object export_data = null)
    {
        return typecacheof(export_data, only_root_path: !Dm.truthy(include_subtypes) ? TRUE : FALSE);
    }

    // tg: code/modules/cargo/exports/materials.dm:51
    public override object get_amount(obj exported_item = null)
    {
        if (!isitem(exported_item))
            return 0;

        var our_item = (obj_item)exported_item;
        var mat_comp = Waiting.NoSleep(our_item.get_material_composition()) as DmList;
        datum_material mat_ref = null;
        if (ispath(material_id))
        {
            foreach (datum_material candidate in Dm.each<datum_material>(mat_comp)) // locate(material_id) in mat_comp
            {
                if (istype(candidate, material_id))
                {
                    mat_ref = candidate;
                    break;
                }
            }
        }
        else
            mat_ref = Waiting.NoSleep(SSmaterials.get_material(material_id)) as datum_material;
        object amount = mat_ref == null ? null : mat_comp?[mat_ref];
        if (!Dm.truthy(amount))
            return 0;

        float sheets = Dm.num(amount);
        if (our_item is obj_item_stack_ore)
            sheets *= 0.8f; // Station's ore redemption equipment is really goddamn good.
        return round(sheets / SHEET_MATERIAL_AMOUNT);
    }
}

// tg: code/modules/cargo/exports/materials.dm:114
public partial class datum_export_material_market : datum_export_material
{
    // tg: code/modules/cargo/exports/materials.dm:118
    public override object generate_export_typecache(object export_data = null)
    {
        DmList dot = base.generate_export_typecache(export_data) as DmList;
        // Always include the stock block for any market exports.
        dot[typeof(obj_item_stock_block)] = TRUE;
        return dot;
    }

    // tg: code/modules/cargo/exports/materials.dm:124
    public override object get_base_cost(obj exported_item = null)
    {
        float dot = Dm.num(base.get_base_cost(exported_item));
        if (exported_item is not obj_item_stock_block exported_block)
            return dot * Dm.num(SSstock_market.materials_prices[material_id]);

        return dot * (Dm.truthy(exported_block.fluid) ? exported_block.update_value() : exported_block.export_value);
    }

    // tg: code/modules/cargo/exports/materials.dm:132
    public override async DmTask<object> sell_object(obj sold_item = null, datum_export_report report = null, object dry_run = null, object apply_elastic = null)
    {
        object dot = await base.sell_object(sold_item, report, dry_run, apply_elastic);
        if (Dm.truthy(dry_run))
            return dot;
        float sheets = Dm.num(get_amount(sold_item));
        if (!Dm.truthy(sheets))
            return dot;

        //This formula should impact lower quantity materials greater, and higher quantity materials less. Still, it's  a bit rough. Tweaking may be needed.
        //decrease the market price
        SSstock_market.adjust_material_price(material_id, -Dm.num(SSstock_market.materials_prices[material_id]) * (sheets / (sheets + Dm.num(SSstock_market.materials_quantity[material_id]))));
        //increase the stock
        SSstock_market.adjust_material_quantity(material_id, sheets);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/cargo/exports/materials.dm:42  var/static/list/shared_exports
    internal DmList datum_export_material__init_export_types__shared_exports;
}
