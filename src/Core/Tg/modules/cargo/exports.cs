// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/exports.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4b (D327): the whole file: the export report, the export loops that sell a thing and its contents, and the export datum with
// its elasticity, its boost and its printout. The export kinds are cargo/exports/*. The var blocks are generated.

/* How it works:
The shuttle arrives at CentCom dock and calls sell(), which recursively loops through all the shuttle contents that are unanchored.

Each object in the loop is checked for applies_to() of various export datums, except the invalid ones.
*/

// tg: code/modules/cargo/exports.dm:43 (global procs; interleaved types, rules 1.9)
public static partial class GlobalProcs
{
    /*
        * Handles exporting a movable atom and its contents
        * Arguments:
        ** apply_elastic: if the price will change based on amount sold, where applicable
        ** delete_unsold: if the items that were not sold should be deleted
        ** dry_run: if the item should be actually sold, or if it's just a pirce test
        ** external_report: works as "transaction" object, pass same one in if you're doing more than one export in single go
        ** ignore_typecache: typecache containing types that should be completely ignored
        ** export_markets: Defines the market that the items are being sold to.
    */
    // tg: code/modules/cargo/exports.dm:43
    public static async DmTask<datum_export_report> export_item_and_contents(atom_movable exported_atom = null, object apply_elastic = null, object delete_unsold = null, object dry_run = null, datum_export_report external_report = null, DmList ignore_typecache = null, object export_markets = null)
    {
        apply_elastic ??= TRUE;
        delete_unsold ??= TRUE;
        dry_run ??= FALSE;
        export_markets ??= Dm.list(EXPORT_MARKET_STATION);
        if (!islist(export_markets))
            export_markets = Dm.list(export_markets);

        if (external_report == null)
            external_report = Dm.@new<datum_export_report>();

        DmList contents = exported_atom.get_all_contents_ignoring(ignore_typecache) as DmList;

        // We go backwards, so it'll be innermost objects sold first. We also make sure nothing is accidentally delete before everything is sold.
        DmList to_delete = Dm.list();
        foreach (atom_movable thing in Dm.each_cast<atom_movable>(reverse_range(contents) as DmList))
        {
            object sold = await _export_loop(thing, apply_elastic, dry_run, external_report, export_markets as DmList);
            if (!Dm.truthy(dry_run) && (Dm.truthy(sold) || Dm.truthy(delete_unsold)) && !Dm.eq(sold, EXPORT_SOLD_DONT_DELETE))
            {
                if (ismob(thing))
                    thing.investigate_log("deleted through cargo export", INVESTIGATE_CARGO);
                Dm.append(ref to_delete, thing);
            }
        }

        foreach (atom_movable thing in Dm.each_cast<atom_movable>(to_delete))
        {
            if (!QDELETED(thing))
                qdel(thing);
        }

        return external_report;
    }

    /// It works like export_item_and_contents(), however it ignores the contents. Meaning only `exported_atom` will be valued.
    // tg: code/modules/cargo/exports.dm:68
    public static async DmTask<datum_export_report> export_single_item(atom_movable exported_atom = null, object apply_elastic = null, object delete_unsold = null, object dry_run = null, datum_export_report external_report = null, object export_markets = null)
    {
        apply_elastic ??= TRUE;
        delete_unsold ??= TRUE;
        dry_run ??= FALSE;
        export_markets ??= Dm.list(EXPORT_MARKET_STATION);
        if (external_report == null)
            external_report = Dm.@new<datum_export_report>();

        object sold = await _export_loop(exported_atom, apply_elastic, dry_run, external_report, export_markets as DmList);
        if (!Dm.truthy(dry_run) && (Dm.truthy(sold) || Dm.truthy(delete_unsold)) && !Dm.eq(sold, EXPORT_SOLD_DONT_DELETE))
        {
            if (ismob(exported_atom))
                exported_atom.investigate_log("deleted through cargo export", INVESTIGATE_CARGO);
            qdel(exported_atom);
        }

        return external_report;
    }

    /// The main bit responsible for selling the item. Shared by export_single_item() and export_item_and_contents()
    // tg: code/modules/cargo/exports.dm:81
    public static async DmTask<object> _export_loop(atom_movable exported_atom = null, object apply_elastic = null, object dry_run = null, datum_export_report external_report = null, DmList export_markets = null)
    {
        apply_elastic ??= TRUE;
        dry_run ??= FALSE;
        object sold = EXPORT_NOT_SOLD;
        foreach (datum_export export in Dm.each_cast<datum_export>(GLOB.exports_list))
        {
            if (Dm.truthy(export.applies_to(exported_atom as obj, apply_elastic, export_markets)))
            {
                if (!Dm.truthy(dry_run) && Dm.truthy(Dm.bitand(exported_atom.SEND_SIGNAL(COMSIG_ITEM_PRE_EXPORT, Dm.list(exported_atom)), COMPONENT_STOP_EXPORT)))
                    break;
                //Don't add value of unscannable items for a dry run report
                if (Dm.truthy(dry_run) && !Dm.truthy(export.scannable))
                {
                    external_report.all_contents_scannable = FALSE;
                    break;
                }
                sold = await export.sell_object(exported_atom as obj, external_report, dry_run, apply_elastic);
                break;
            }
        }
        return sold;
    }
}

// tg: code/modules/cargo/exports.dm:95
public partial class datum_export : datum
{
    // tg: code/modules/cargo/exports.dm:129
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        original_export_types = (export_types as DmList)?.Copy();
        export_types = typecacheof(export_types, only_root_path: !Dm.truthy(include_subtypes) ? TRUE : FALSE);
        exclude_types = typecacheof(exclude_types) as DmList;
    }

    // tg: code/modules/cargo/exports.dm:135
    public override object Destroy(float? force = null)
    {
        return base.Destroy(force);
    }

    // tg: code/modules/cargo/exports.dm:138
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        k_elasticity += Dm.num(seconds_per_tick) / k_recovery_time;
        if (k_elasticity >= 1)
        {
            k_elasticity = 1;
            return PROCESS_KILL;
        }
        return null;
    }

    /**
     * Returns the cost of 1 unit of the exported item
     *
     * Arguments
     * * obj/exported_item - the item we whos base cost we are trying to compute
    */
    // tg: code/modules/cargo/exports.dm:150
    public virtual object get_base_cost(obj exported_item = null)
    {
        return cost;
    }

    /*
     * Returns the amount of exportable in object. Credits in the bill, sheets in the stack, etc.
     * Usually acts as a multiplier for a cost, so item that has 0 amount will be skipped in export.
     *
     * Arguments
     * * obj/exported_item - the amount of units in this exported item
    */
    // tg: code/modules/cargo/exports.dm:160
    public virtual object get_amount(obj exported_item = null)
    {
        return 1;
    }

    /**
     * Returns the cost of the exported item i.e. amount * base cost * elasticity if TRUE
     *
     * Arguments
     * * obj/exported_item - the item we are trying to export
     * * apply_elastic - should we use elasticity
    */
    // tg: code/modules/cargo/exports.dm:171  (SHOULD_NOT_OVERRIDE)
    public float get_cost(obj exported_item = null, object apply_elastic = null)
    {
        apply_elastic ??= TRUE;
        float total = Dm.num(get_base_cost(exported_item)) * Dm.num(get_amount(exported_item));
        if (Dm.truthy(apply_elastic) && Dm.initial(this, (datum_export p) => p.k_elasticity) > 0)
            total *= k_elasticity;
        if (Dm.@in(type, SSeconomy.boosted_exports))
            total *= EXPORT_BOOST_MULT;
        return ROUND_UP(total);
    }

    /// Checks if the item is fit for export datum.
    // tg: code/modules/cargo/exports.dm:182
    public virtual object applies_to(obj exported_item = null, object apply_elastic = null, DmList export_markets = null)
    {
        apply_elastic ??= TRUE;
        foreach (object found_market in Dm.each(export_markets))
        {
            if (!Dm.truthy(is_type_in_typecache(exported_item, export_types as DmList)))
                continue;
            if (Dm.truthy(include_subtypes) && Dm.truthy(is_type_in_typecache(exported_item, exclude_types)))
                continue;
            if (!Dm.truthy(get_cost(exported_item, apply_elastic)))
                continue;
            if (!Dm.eq(found_market, sales_market))
                continue;
            if (Dm.truthy(Dm.bitand(exported_item.flags_1, HOLOGRAM_1)))
                continue;
            return TRUE;
        }
        return null;
    }

    /**
     * Calculates the exact export value of the object, while factoring in all the relivant variables.
     *
     * Called only once, when the object is actually sold by the datum.
     * Adds item's cost and amount to the current export cycle.
     * get_cost, get_amount and applies_to do not neccesary mean a successful sale.
     *
     */
    // tg: code/modules/cargo/exports.dm:204
    public virtual async DmTask<object> sell_object(obj sold_item = null, datum_export_report report = null, object dry_run = null, object apply_elastic = null)
    {
        dry_run ??= TRUE;
        apply_elastic ??= TRUE;
        ///This is the value of the object, as derived from export datums.
        float export_value = get_cost(sold_item, apply_elastic);
        ///Quantity of the object in question.
        float export_amount = Dm.num(get_amount(sold_item));

        if (export_amount <= 0 || (export_value <= 0 && !Dm.truthy(allow_negative_cost)))
            return EXPORT_NOT_SOLD;

        // If we're not doing a dry run, send COMSIG_ITEM_EXPORTED to the sold item
        object export_result = null;
        if (!Dm.truthy(dry_run))
            export_result = sold_item.SEND_SIGNAL(COMSIG_ITEM_EXPORTED, Dm.list(sold_item, this, report, export_value));

        // If the signal handled adding it to the report, don't do it now
        if (!Dm.truthy(Dm.bitand(export_result, COMPONENT_STOP_EXPORT_REPORT)))
        {
            report.total_value[this] = Dm.add(report.total_value[this], export_value);
            report.total_amount[this] = Dm.add(report.total_amount[this], export_amount * amount_report_multiplier);
            Dm.append(ref report.exported_atoms, Dm.text("[]", sold_item.name));
        }

        if (!Dm.truthy(dry_run))
        {
            if (Dm.truthy(apply_elastic) && k_elasticity > 0)
            {
                k_elasticity -= export_amount * k_hit_percentile;
                if (k_elasticity < 0)
                    k_elasticity = 0;
                Dm.combine(ref SSprocessing.processing, this);
            }
            SSblackbox.record_feedback("nested tally", "export_sold_cost", 1, Dm.list(Dm.text("[]", sold_item.type), Dm.text("[]", export_value)));
        }
        return EXPORT_SOLD;
    }

    /*
    * Total printout for the cargo console.
    * Called before the end of current export cycle.
    * It must always return something if the datum adds or removes any credtis.
    */
    // tg: code/modules/cargo/exports.dm:238
    public virtual object total_printout(datum_export_report ex = null, object notes = null)
    {
        notes ??= TRUE;
        if (!Dm.truthy(ex.total_amount[this]) || !Dm.truthy(ex.total_value[this]))
            return "";

        object total_value = ex.total_value[this];
        object total_amount = ex.total_amount[this];

        string msg = I18n.T("[] []: Received [] ", total_value, MONEY_NAME, total_amount);
        if (Dm.num(total_value) > 0)
            msg = "+" + msg;

        if (Dm.truthy(unit_name))
        {
            msg += unit_name;
            if (Dm.num(total_amount) > 1)
                msg += plural_s(unit_name);
            if (Dm.truthy(message))
                msg += " ";
        }

        if (Dm.truthy(message))
            msg += message;

        msg += ".";
        return msg;
    }
}

// tg: code/_globalvars/lists/cargo.dm:1 (interleaved types, rules 1.9)
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/_globalvars/lists/cargo.dm:3  GLOBAL_LIST_INIT(exports_list, init_Exports())
    public DmList exports_list;

    // tg: code/_globalvars/lists/cargo.dm:3  (GLOBAL_MANAGED(exports_list, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalexports_list()
    {
        exports_list = init_Exports();
        Dm.append(ref gvars_datum_init_order, "exports_list");
    }

    // tg: code/_globalvars/lists/cargo.dm:1  GLOBAL_LIST_INIT(discountable_packs, init_discountable_packs())
    public DmList discountable_packs;

    // tg: code/_globalvars/lists/cargo.dm:1  (GLOBAL_MANAGED(discountable_packs, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldiscountable_packs()
    {
        discountable_packs = init_discountable_packs();
        Dm.append(ref gvars_datum_init_order, "discountable_packs");
    }

    // tg: code/_globalvars/lists/cargo.dm:5  GLOBAL_LIST_INIT(pack_discount_odds, list(...))
    public DmList pack_discount_odds;

    // tg: code/_globalvars/lists/cargo.dm:5  (GLOBAL_MANAGED(pack_discount_odds, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpack_discount_odds()
    {
        pack_discount_odds = Dm.list(
            Dm.kv(SUPPLY_PACK_STD_DISCOUNTABLE, 45),
            Dm.kv(SUPPLY_PACK_UNCOMMON_DISCOUNTABLE, 4),
            Dm.kv(SUPPLY_PACK_RARE_DISCOUNTABLE, 1));
        Dm.append(ref gvars_datum_init_order, "pack_discount_odds");
    }
}

// tg: code/_globalvars/lists/cargo.dm:13 (global procs)
public static partial class GlobalProcs
{
    /// Caled when the global discount pack list is initialized
    // tg: code/_globalvars/lists/cargo.dm:14
    public static DmList init_discountable_packs()
    {
        DmList packs = Dm.list();
        foreach (Type prototype in Dm.each_cast<Type>(subtypesof(typeof(datum_supply_pack))))
        {
            object discountable = Dm.initial(prototype, (datum_supply_pack p) => p.discountable);
            if (Dm.truthy(discountable))
            {
                var bucket = packs[discountable] as DmList;
                LAZYADD(ref bucket, prototype);
                packs[discountable] = bucket;
            }
        }
        return packs;
    }

    /// Called when the global exports_list is empty, and sets it up.
    // tg: code/_globalvars/lists/cargo.dm:23
    public static DmList init_Exports()
    {
        DmList exports = Dm.list();
        foreach (Type subtype in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_export)) as DmList))
            Dm.append(ref exports, Dm.@new(subtype));
        return exports;
    }
}
