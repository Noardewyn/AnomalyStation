// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/colors.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice C: the whole file (the containers' fill colour).

public static partial class GlobalProcs
{
    // tg: code/modules/reagents/chemistry/colors.dm:1
    public static object mix_color_from_reagents(object reagent_list = null)
    {
        if (!(reagent_list is DmList))
            return null;

        object mixcolor = null;
        float vol_counter = 0;
        float vol_temp;

        foreach (datum_reagent R in Dm.each<datum_reagent>(reagent_list))
        {
            vol_temp = Dm.num(R.volume);
            vol_counter += vol_temp;
            // A runtime in tg: seven reagents' colours lack the # ("90560B"), which a colour or a blend refuses.
            object reagent_color = R.color is string text && text.Length > 0 && text[0] != '#' ? "#" + text : R.color;

            if (!Dm.truthy(mixcolor))
                mixcolor = reagent_color;

            else if (Dm.length(mixcolor) >= Dm.length(reagent_color))
                mixcolor = BlendRGB(mixcolor, reagent_color, vol_temp / vol_counter);
            else
                mixcolor = BlendRGB(reagent_color, mixcolor, vol_temp / vol_counter);
        }

        return mixcolor;
    }

    // tg: code/modules/reagents/chemistry/colors.dm:23
    public static object reagent_threshold_overlay(datum_reagents reagents = null, object fill_icon = null, object fill_prefix = null, DmList fill_icon_thresholds = null)
    {
        object threshold = null;
        for (float i = 1; i <= fill_icon_thresholds.len; i++)
        {
            if (ROUND_UP(100 * reagents.total_volume / reagents.maximum_volume) >= Dm.num(fill_icon_thresholds[i]))
                threshold = i;
        }

        if (Dm.truthy(threshold))
        {
            string fill_name = Dm.text("[][]", fill_prefix, fill_icon_thresholds[threshold]);
            mutable_appearance filling = (mutable_appearance)mutable_appearance(fill_icon, fill_name);
            filling.color = mix_color_from_reagents(reagents.reagent_list);
            return filling;
        }

        return null;
    }
}
