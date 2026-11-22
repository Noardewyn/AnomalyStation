// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/taste.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M8 slice A: the taste of what a mob swallows or breathes).

public static partial class GlobalProcs
{
    // tg: code/modules/reagents/chemistry/taste.dm:1  #define TEXT_NO_TASTE "something indescribable" (file-local, #undef at :86)
    private const string TEXT_NO_TASTE = "something indescribable";

    //=============================== TASTE GRAPH ====================================
    //                                                                              //
    // Flavor Intensity Thresholds (Relative to Detection Threshold % 'DT'):        //
    // 0%             1x DT              2x DT              4x DT             100%  //
    // |----------------|------------------|------------------|-----------------|   //
    // |   Undetected   |       Weak       |       Mild       |      Strong     |   //
    // |----------------|------------------|------------------|-----------------|   //
    //                                                                              //
    //==============================================================================//

    /**
     * Returns what the reagents in our given list taste like
     *
     * Arguments:
     * * list/reagent_list - List of reagents to taste.
     * * mob/living/taster - Who is doing the tasting. Some mobs can pick up specific flavours.
     * * detection_threshold_percent - The minimum relative percentage a flavor must reach to be tasted.
     */
    // tg: code/modules/reagents/chemistry/taste.dm:22
    public static object generate_reagents_taste_message(DmList reagent_list = null, mob_living taster = null, object detection_threshold_percent = null)
    {
        // We can't taste anything
        if (Dm.num(detection_threshold_percent) > 100)
            return TEXT_NO_TASTE;

        // Associative list of our tastes - list("taste description" = strength)
        DmList tastes = Dm.list();
        float total_taste_strength = 0;

        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(reagent_list))
        {
            if (!Dm.truthy(reagent.taste_mult))
                continue;

            DmList taste_data = reagent.get_taste_description(taster) as DmList;
            foreach (object taste_desc in Dm.each(taste_data))
            {
                float taste_strength = Dm.num(taste_data[taste_desc]) * Dm.num(reagent.volume) * reagent.taste_mult;
                tastes[taste_desc] = Dm.num(tastes[taste_desc]) + taste_strength;
                total_taste_strength += taste_strength;
            }
        }

        // None of our reagents had any flavour
        if (total_taste_strength <= 0)
            return TEXT_NO_TASTE;

        // If we have exactly one taste, don't bother with relative strengths
        if (length(tastes) == 1)
            return tastes[1];

        // Sort tastes descending by strength, so strong flavours come first
        sortTim(tastes, "/proc/" + nameof(cmp_numeric_dsc), associative: TRUE);

        // Lazylists for different taste strength categories, no need to initialize if we don't have such flavors
        DmList strong_tastes = null;
        DmList mild_tastes = null;
        DmList weak_tastes = null;

        foreach (object taste_desc in Dm.each(tastes))
        {
            float relative_taste_percent = (Dm.num(tastes[taste_desc]) / total_taste_strength) * 100;

            // From weakest to strongest
            if (relative_taste_percent < Dm.num(detection_threshold_percent))
                continue; // too weak to detect
            else if (relative_taste_percent <= Dm.num(detection_threshold_percent) * 2)
                LAZYADD(ref weak_tastes, taste_desc);
            else if (relative_taste_percent <= Dm.num(detection_threshold_percent) * 4)
                LAZYADD(ref mild_tastes, taste_desc);
            else
                LAZYADD(ref strong_tastes, taste_desc);
        }

        DmList taste_messages = Dm.list();

        if (Dm.truthy(LAZYLEN(strong_tastes)))
            taste_messages.Add(I18n.T("the strong flavor of []", english_list(strong_tastes, TEXT_NO_TASTE)));
        if (Dm.truthy(LAZYLEN(mild_tastes)))
            // Prefix "some " if there are strong flavors to avoid seeming like a strong flavor
            taste_messages.Add(I18n.T("[][]", Dm.truthy(LAZYLEN(strong_tastes)) ? I18n.T("some ") : "", english_list(mild_tastes, TEXT_NO_TASTE)));
        if (Dm.truthy(LAZYLEN(weak_tastes)))
            taste_messages.Add(I18n.T("a hint of []", english_list(weak_tastes, TEXT_NO_TASTE)));

        return english_list(taste_messages, TEXT_NO_TASTE);
    }
}
