// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/salad.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the kitchen links of the food (make_grillable, make_bakeable, make_microwaveable, make_processable,
// make_dryable) and the bowl (:179-243) translated (gaps package H2, D333); the rest of the file is generated or waits for its
// package.

public partial class obj_item_food_uncooked_rice : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/salad.dm:82
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_boiledrice), rand(15 * SECONDS, 20 * SECONDS), TRUE, TRUE);
    }

    // tg: code/modules/food_and_drinks/food/salad.dm:85
    public override async DmTask make_microwaveable()
    {
        await AddElement(typeof(datum_element_microwavable), typeof(obj_item_food_boiledrice));
    }
}

// tg: code/modules/food_and_drinks/food/salad.dm:179
public partial class obj_item_reagent_containers_cup_bowl : obj_item_reagent_containers_cup
{
    // tg: code/modules/food_and_drinks/food/salad.dm:198
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ATOM_REAGENT_EXAMINE, nameof(reagent_special_examine));
        Waiting.NoSleep(AddComponent(typeof(datum_component_ingredients_holder), typeof(obj_item_food_salad_empty), CUSTOM_INGREDIENT_ICON_FILL, Dm.named("max_ingredients", 6)));
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_takes_reagent_appearance),
            Dm.named("on_icon_changed", Dm.@new<datum_callback>(this, nameof(on_cup_change))),
            Dm.named("on_icon_reset", Dm.@new<datum_callback>(this, nameof(on_cup_reset))),
            Dm.named("base_container_type", typeof(obj_item_reagent_containers_cup_bowl))
        ));
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/salad.dm:209
    public override void on_cup_change(datum_glass_style_has_foodtype style = null)
    {
        base.on_cup_change(style);
        fill_icon_thresholds = null;
    }

    // tg: code/modules/food_and_drinks/food/salad.dm:213
    public override void on_cup_reset()
    {
        base.on_cup_reset();
        fill_icon_thresholds ??= Dm.list(0);
    }

    /**
     * Override standard reagent examine
     * so that anyone examining a bowl of soup sees the soup but nothing else (unless they have sci goggles)
     */
    // tg: code/modules/food_and_drinks/food/salad.dm:221
    [SignalHandler]
    public virtual object reagent_special_examine(datum source = null, mob user = null, DmList examine_list = null, float? can_see_insides = null)
    {
        can_see_insides ??= FALSE;
        datum_reagents reagents = (datum_reagents)this.reagents;
        if (Dm.truthy(can_see_insides) || reagents.total_volume <= 0)
            return null;

        float unknown_volume = 0;
        DmList soups_found = Dm.list();
        foreach (datum_reagent current_reagent in Dm.each_cast<datum_reagent>(reagents.reagent_list))
        {
            if (istype(current_reagent, typeof(datum_reagent_consumable_nutriment_soup)))
                Dm.append(ref soups_found, I18n.T("&bull; [] units of []", round(Dm.num(current_reagent.volume), 0.01f), current_reagent.name));
            else
                unknown_volume += Dm.num(current_reagent.volume);
        }

        if (!Dm.truthy(Dm.length(soups_found)))
            // There was no soup in the pot, do normal examine
            return null;

        Dm.append(ref examine_list, I18n.T("Inside, you can see:"));
        Dm.append(ref examine_list, soups_found);
        if (unknown_volume > 0)
            Dm.append(ref examine_list, I18n.T("&bull; [] units of unknown reagents", round(unknown_volume, 0.01f)));

        return STOP_GENERIC_REAGENT_EXAMINE;
    }
}
