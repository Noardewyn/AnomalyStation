// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/cell.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file; the ninja drain procs (modules_ninja.dm) are rows of docs/ai/specs/live-partial.txt (M10+). The var blocks are
// generated.

/**
 * # Power cell
 *
 * Power cells, used primarily for handheld and portable things. Holds a reasonable amount of power.
 */
// tg: code/modules/power/cell.dm:7
public partial class obj_item_stock_parts_power_store_cell : obj_item_stock_parts_power_store
{
    // tg: code/modules/power/cell.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_FISHING_BAIT, INNATE_TRAIT);
        ADD_TRAIT(this, TRAIT_POISONOUS_BAIT, INNATE_TRAIT); //bro is fishing using lithium...
        // tg: code/modules/power/cell.dm:27  var/static/list/slapcraft_recipe_list (a type-level static, rules 10.6)
        // tg: code/modules/power/cell.dm:28
        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), TgState.Current.obj_item_stock_parts_power_store_cell__slapcraft_recipe_list));
        return dot;
    }

    // tg: code/modules/power/cell.dm:33
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_lithium), 15), Dm.kv(typeof(datum_reagent_iron), 5), Dm.kv(typeof(datum_reagent_silicon), 5));
    }
}

// tg: code/modules/power/cell.dm:248 (engineering E12a: the crystallizer's crystal cell)
public partial class obj_item_stock_parts_power_store_cell_crystal_cell : obj_item_stock_parts_power_store_cell
{
    // tg: code/modules/power/cell.dm:258
    public override async DmTask<object> grind_results()
    {
        return null;
    }
}

// tg: code/modules/power/cell.dm:234
public partial class obj_item_stock_parts_power_store_cell_emergency_light : obj_item_stock_parts_power_store_cell
{
    // tg: code/modules/power/cell.dm:241
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        area area = get_area(this) as area;
        if (Dm.truthy(area))
        {
            if (!Dm.truthy(area.lightswitch) || !Dm.truthy(area.light_power))
                charge = 0; //For naturally depowered areas, we start with no power
        }
        return dot;
    }
}

// tg: code/modules/power/cell.dm:209
public partial class obj_item_stock_parts_power_store_cell_emproof : obj_item_stock_parts_power_store_cell
{
    // tg: code/modules/power/cell.dm:214
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), EMP_PROTECT_SELF));
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/power/cell.dm:221
    public override void corrupt(object force = null)
    {
        return;
    }
}

// tg: code/modules/power/cell.dm:60
public partial class obj_item_stock_parts_power_store_cell_upgraded : obj_item_stock_parts_power_store_cell
{
    // tg: code/modules/power/cell.dm:63
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_blocker)));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/power/cell.dm:41
public partial class obj_item_stock_parts_power_store_cell_crap : obj_item_stock_parts_power_store_cell
{
    // tg: code/modules/power/cell.dm:47
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_blocker)));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/power/cell.dm:168
public partial class obj_item_stock_parts_power_store_cell_infinite : obj_item_stock_parts_power_store_cell
{
    // tg: code/modules/power/cell.dm:176
    [DmParams("used", "force")]
    public override async DmTask<object> use(object used = null, params object[] __extra)
    {
        return used;
    }
}

// tg: code/modules/power/cell.dm:180
public partial class obj_item_stock_parts_power_store_cell_infinite_abductor : obj_item_stock_parts_power_store_cell_infinite
{
    // tg: code/modules/power/cell.dm:187
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_blocker)));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/power/cell.dm:192
public partial class obj_item_stock_parts_power_store_cell_potato : obj_item_stock_parts_power_store_cell
{
    // tg: code/modules/power/cell.dm:204
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        charge = maxcharge * 0.3f;
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/power/cell.dm:262
public partial class obj_item_stock_parts_power_store_cell_ethereal : obj_item_stock_parts_power_store_cell
{
    // tg: code/modules/power/cell.dm:272
    public override async DmTask<object> grind_results()
    {
        return null;
    }

    // tg: code/modules/power/cell.dm:275
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        await base.examine(user, __extra);
        throw Dm.CRASH(Dm.text("[] got examined by []", type, user), "code/modules/power/cell.dm", 277);
    }
}
