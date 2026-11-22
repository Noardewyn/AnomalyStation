// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/rainbow_bunch.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/rainbow_bunch.dm:23
public partial class obj_item_food_grown_flower_rainbow_flower : obj_item_food_grown_flower
{
    // tg: code/modules/hydroponics/grown/rainbow_bunch.dm:42
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.eq(greyscale_colors, Dm.initial(this, "greyscale_colors")))
            return dot;

        datum_reagents holder = reagents as datum_reagents;
        float flower_color = rand(1, 8);
        switch ((int)flower_color)
        {
            case 1:
                Waiting.NoSleep(set_greyscale("#c50b0b"));
                Waiting.NoSleep(holder.add_reagent(typeof(datum_reagent_colorful_reagent_powder_red), 3));
                dye_color = DYE_RED;
                desc += " This one is in a bright red color.";
                break;
            case 2:
                Waiting.NoSleep(set_greyscale("#f76f07"));
                Waiting.NoSleep(holder.add_reagent(typeof(datum_reagent_colorful_reagent_powder_orange), 3));
                dye_color = DYE_ORANGE;
                desc += " This one is in a citrus orange color.";
                break;
            case 3:
                Waiting.NoSleep(set_greyscale("#d8ce13"));
                Waiting.NoSleep(holder.add_reagent(typeof(datum_reagent_colorful_reagent_powder_yellow), 3));
                dye_color = DYE_YELLOW;
                desc += " This one is in a bright yellow color.";
                break;
            case 4:
                Waiting.NoSleep(set_greyscale("#a0da23"));
                Waiting.NoSleep(holder.add_reagent(typeof(datum_reagent_colorful_reagent_powder_green), 3));
                dye_color = DYE_GREEN;
                desc += " This one is in a grassy green color.";
                break;
            case 5:
                Waiting.NoSleep(set_greyscale("#0862c1"));
                Waiting.NoSleep(holder.add_reagent(typeof(datum_reagent_colorful_reagent_powder_blue), 3));
                dye_color = DYE_BLUE;
                desc += " This one is in a soothing blue color.";
                break;
            case 6:
                Waiting.NoSleep(set_greyscale("#ad00cc"));
                Waiting.NoSleep(holder.add_reagent(typeof(datum_reagent_colorful_reagent_powder_purple), 3));
                dye_color = DYE_PURPLE;
                desc += " This one is in a vibrant purple color.";
                break;
            case 7:
                Waiting.NoSleep(set_greyscale("#161616"));
                Waiting.NoSleep(holder.add_reagent(typeof(datum_reagent_colorful_reagent_powder_black), 3));
                dye_color = DYE_BLACK;
                desc += " This one is in a midnight black color.";
                break;
            case 8:
                Waiting.NoSleep(set_greyscale(COLOR_WHITE));
                Waiting.NoSleep(holder.add_reagent(typeof(datum_reagent_colorful_reagent_powder_white), 3));
                dye_color = DYE_WHITE;
                desc += " This one is in a pure white color.";
                break;
        }
        return dot;
    }
}
