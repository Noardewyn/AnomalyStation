// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/cannabis.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/cannabis.dm:92
public partial class obj_item_seeds_cannabis_anti : obj_item_seeds_cannabis
{
    // tg: code/modules/hydroponics/grown/cannabis.dm:106
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(add_atom_colour(COLOR_MATRIX_INVERT(), FIXED_COLOUR_PRIORITY));
        transform = ((matrix)Dm.@new<matrix>(transform)).Turn(180);
        return dot;
    }

    // tg: code/modules/hydroponics/grown/cannabis.dm:111
    public override object get_tray_overlay(object age = null, object status = null, object tray_offset = null)
    {
        mutable_appearance plant = base.get_tray_overlay(age, status, tray_offset) as mutable_appearance;
        plant.color = COLOR_MATRIX_INVERT();
        return plant;
    }
}

// tg: code/modules/hydroponics/grown/cannabis.dm:159
public partial class obj_item_food_grown_cannabis_anti : obj_item_food_grown_cannabis
{
    // tg: code/modules/hydroponics/grown/cannabis.dm:164
    [DmParams("mapload", "new_seed")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(add_atom_colour(COLOR_MATRIX_INVERT(), FIXED_COLOUR_PRIORITY));
        transform = ((matrix)Dm.@new<matrix>(transform)).Turn(180);
        if (prob(0.05f))
            name = "evil cannabis leaf";
        return dot;
    }
}
