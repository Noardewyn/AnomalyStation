// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/food/fried_item.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): what the deep fryer does to what it fries. The static fried_colors is a TgState
// field (rules 10.6).

/// Items fried through the deep fryer.
// tg: code/datums/elements/food/fried_item.dm:2
public partial class datum_element_fried_item : datum_element
{
    /// List of colors to apply the element target.
    /// Each index corresponds to a different level.
    // tg: code/datums/elements/food/fried_item.dm:5  var/static/list/fried_colors (rules 10.6)
    public static ref DmList fried_colors => ref TgState.Current.datum_element_fried_item__fried_colors;

    // tg: code/datums/elements/food/fried_item.dm:7
    [DmParams("target", "fry_time")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object fry_time = Dm.arg(__extra, 0);
        object dot = await base.Attach(target, __extra);
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;

        if (fried_colors == null)
        {
            fried_colors = Dm.list(
                color_transition_filter(COLOR_LIGHT_BROWN, SATURATION_OVERRIDE),
                color_transition_filter(COLOR_BROWNER_BROWN, SATURATION_OVERRIDE),
                color_transition_filter(COLOR_DARKER_BROWN, SATURATION_OVERRIDE),
                color_transition_filter(COLOR_BLACK, SATURATION_OVERRIDE)
            );
        }

        atom this_food = (atom)target;

        object sw__1 = fry_time;
        if (Dm.in_to(sw__1, 0, FRYING_TIME_FRIED))
        {
            await this_food.add_atom_colour(fried_colors[1], FIXED_COLOUR_PRIORITY);
            this_food.name = I18n.T("lightly-fried []", this_food.name);
            this_food.desc += I18n.T(" It's been lightly fried in a deep fryer.");
        }
        else if (Dm.in_to(sw__1, FRYING_TIME_FRIED, FRYING_TIME_PERFECT))
        {
            await this_food.add_atom_colour(fried_colors[2], FIXED_COLOUR_PRIORITY);
            this_food.name = I18n.T("fried []", this_food.name);
            this_food.desc += I18n.T(" It's been fried, increasing its tastiness value by []%.", rand(1, 75));
        }
        else if (Dm.in_to(sw__1, FRYING_TIME_PERFECT, FRYING_TIME_BURNT))
        {
            await this_food.add_atom_colour(fried_colors[3], FIXED_COLOUR_PRIORITY);
            this_food.name = I18n.T("deep-fried []", this_food.name);
            this_food.desc += I18n.T(" Deep-fried to perfection.");
        }
        else if (Dm.in_to(sw__1, FRYING_TIME_BURNT, INFINITY))
        {
            await this_food.add_atom_colour(fried_colors[4], FIXED_COLOUR_PRIORITY);
            this_food.name = I18n.T("\\proper the physical manifestation of the very concept of fried foods");
            this_food.desc = I18n.T("A heavily-fried... something. Who can tell anymore?");
        }

        ADD_TRAIT(this_food, TRAIT_FOOD_FRIED, ELEMENT_TRAIT(type));
        // Already edible items will inherent these parameters
        // Otherwise, we will become edible.
        await this_food.AddComponentFrom(
            SOURCE_EDIBLE_FRIED,
            typeof(datum_component_edible),
            Dm.named("bite_consumption", 2),
            Dm.named("food_flags", FOOD_FINGER_FOOD),
            Dm.named("junkiness", 10),
            Dm.named("foodtypes", FRIED)
        );
        if (Dm.truthy(this_food._listen_lookup?[COMSIG_ITEM_FRIED]))
            this_food._SendSignal(COMSIG_ITEM_FRIED, Dm.list(this_food, fry_time));
        return dot;
    }

    // tg: code/datums/elements/food/fried_item.dm:56
    public override object Detach(datum source = null, params object[] __extra)
    {
        atom source__atom = (atom)source;
        foreach (object color in Dm.each(fried_colors))
            Waiting.NoSleep(source__atom.remove_atom_colour(FIXED_COLOUR_PRIORITY, color));
        source__atom.name = Dm.initial(source__atom, (atom a) => a.name);
        source__atom.desc = Dm.initial(source__atom, (atom a) => a.desc);
        REMOVE_TRAIT(source, TRAIT_FOOD_FRIED, ELEMENT_TRAIT(type));
        source.RemoveComponentSource(SOURCE_EDIBLE_FRIED, typeof(datum_component_edible));
        return base.Detach(source, __extra);
    }
}

public sealed partial class TgState
{
    // tg: code/datums/elements/food/fried_item.dm:5  /datum/element/fried_item's var/static/list/fried_colors
    internal DmList datum_element_fried_item__fried_colors;
}
