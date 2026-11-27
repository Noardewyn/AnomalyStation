// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/vegetables.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the tator tot came with the Donk pinata (syndicate N2b, D316); the fries' dunking with the restaurant. The other
// vegetables keep the base. The var blocks are
// generated (D111).

// Potatoes
// tg: code/modules/food_and_drinks/food/vegetables.dm:44
public partial class obj_item_food_tatortot : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/vegetables.dm:55
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_dunkable), 10));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/vegetables.dm:105
public partial class obj_item_food_fries : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/vegetables.dm:117
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_dunkable), 10));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/vegetables.dm:121
public partial class obj_item_food_cheesyfries : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/vegetables.dm:136
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_dunkable), 10));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/vegetables.dm:140
public partial class obj_item_food_carrotfries : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/vegetables.dm:152
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_dunkable), 10));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/vegetables.dm:156
public partial class obj_item_food_poutine : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/vegetables.dm:168
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_dunkable), 10));
        return dot;
    }
}
