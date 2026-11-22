// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/stock_parts/stock_part_datum.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 7 (machine frames, D148): the whole file. A machine's stock parts are singletons of /datum/stock_part (one per
// part type and tier) in its component_parts; the frame turns an inserted part item into its datum and a deconstructed
// machine turns the datum back into a new part item. The subtypes are var-only. The var blocks are generated.

/// Represents the concept of a stock part.
/// One is created for every stock part type for every level.
/// Machines have these inside their component_parts.
/// For example, scanning modules use /datum/stock_part/scanning_module.
/// In machines, you can perform a loop through something like
/// `for (var/datum/stock_part/scanning_module/part in component_parts)`
// tg: code/modules/research/stock_parts/stock_part_datum.dm:7
public partial class datum_stock_part : datum
{
    // tg: code/modules/research/stock_parts/stock_part_datum.dm:24
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        physical_object_reference = Dm.@new(physical_object_type) as obj_item;
    }

    // tg: code/modules/research/stock_parts/stock_part_datum.dm:27
    public override object Destroy(float? force = null)
    {
        stack_trace(Dm.text("[] is trying to Destroy. It is a singleton, this should not be happening", GetType()), "code/modules/research/stock_parts/stock_part_datum.dm", 29);
        return QDEL_HINT_LETMELIVE;
    }

    /// Returns the name of the physical object
    // tg: code/modules/research/stock_parts/stock_part_datum.dm:33
    public virtual object name()
    {
        return Dm.initial(physical_object_type, (obj_item p) => p.name);
    }

    /// Returns the energy rating of the stock part given a level.
    /// The higher this is, the more power machines with these parts will consume.
    // tg: code/modules/research/stock_parts/stock_part_datum.dm:57
    public virtual object energy_rating()
    {
        object sw__1 = tier;
        if (Dm.eq(sw__1, 1))
            return 1;
        else if (Dm.eq(sw__1, 2))
            return 3;
        else if (Dm.eq(sw__1, 3))
            return 5;
        else if (Dm.eq(sw__1, 4))
            return 10;
        else
            throw Dm.CRASH(Dm.text("Invalid level given to energy_rating: []", tier), "code/modules/research/stock_parts/stock_part_datum.dm", 68);
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/research/stock_parts/stock_part_datum.dm:42
    public static object generate_stock_part_datums()
    {
        DmList stock_part_datums = Dm.list();

        foreach (Type stock_part_type in Dm.each_cast<Type>(subtypesof(typeof(datum_stock_part))))
        {
            datum_stock_part singleton = Dm.@new(stock_part_type) as datum_stock_part;
            stock_part_datums[stock_part_type] = singleton;

            // Relying on GLOB ordering here somewhat.
            // If this changes, it'll error in CI.
            GLOB.stock_part_datums_per_object[Dm.initial(stock_part_type, (datum_stock_part p) => p.physical_object_type)] = singleton;
        }

        return stock_part_datums;
    }
}

// tg: code/modules/research/stock_parts/stock_part_datum.dm:36
public partial class datum_controller_global_vars
{
    /// Map of physical stock part types to their /datum/stock_part
    // tg: code/modules/research/stock_parts/stock_part_datum.dm:36  GLOBAL_LIST_EMPTY_TYPED(stock_part_datums_per_object, /datum/stock_part)
    public DmList stock_part_datums_per_object;

    // tg: code/modules/research/stock_parts/stock_part_datum.dm:36
    public virtual void InitGlobalstock_part_datums_per_object()
    {
        stock_part_datums_per_object = Dm.list();
        Dm.append(ref gvars_datum_init_order, "stock_part_datums_per_object");
    }

    /// Map of stock part type to their singleton
    // tg: code/modules/research/stock_parts/stock_part_datum.dm:39  GLOBAL_LIST_INIT(stock_part_datums, generate_stock_part_datums())
    public DmList stock_part_datums;

    // tg: code/modules/research/stock_parts/stock_part_datum.dm:39
    public virtual void InitGlobalstock_part_datums()
    {
        stock_part_datums = generate_stock_part_datums() as DmList;
        Dm.append(ref gvars_datum_init_order, "stock_part_datums");
    }
}
