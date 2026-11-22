// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/cups/_glass_datum.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H3 (D333, the bar): the whole file: the glass style singletons (a drink's own glass picture, name and description in a
// drinking glass, a shot glass, a juice box or a soup bowl) and their global table. The styles of each drink are declared with its
// reagent (generated var blocks, D111).

// tg: code/modules/reagents/reagent_containers/cups/_glass_datum.dm:2  GLOBAL_LIST_INIT(glass_style_singletons, create_glass_styles())
public partial class datum_controller_global_vars : datum_controller
{
    /// Global list of all glass style singletons created. See [/proc/create_glass_styles] for list format.
    // tg: code/modules/reagents/reagent_containers/cups/_glass_datum.dm:2
    public DmList glass_style_singletons;

    // tg: code/modules/reagents/reagent_containers/cups/_glass_datum.dm:2  (GLOBAL_MANAGED, __DEFINES/__globals.dm:5)
    public virtual void InitGlobalglass_style_singletons()
    {
        glass_style_singletons = create_glass_styles() as DmList;
        Dm.append(ref gvars_datum_init_order, "glass_style_singletons");
    }
}

public static partial class GlobalProcs
{
    /**
     * Constructs a nested list of glass style singletons
     *
     * List format:
     * * list(glasses = list(beer = style datum, vodka = style datum), shot glasses = list(vodka = different style datum))
     *
     * Where
     * * "glasses" and "shotglasses" are item typepaths
     * * "beer" and "vodka" are reagent typepaths
     * * "style datum" is a glass style singleton datum
     *
     * Returns the list.
     */
    // tg: code/modules/reagents/reagent_containers/cups/_glass_datum.dm:17
    public static object create_glass_styles()
    {
        DmList final_list = Dm.list();
        foreach (Type style in Dm.each<Type>(typesof(typeof(datum_glass_style))))
        {
            if (Dm.initial(style, (datum_glass_style s) => s.required_drink_type) == null || Dm.initial(style, (datum_glass_style s) => s.required_container_type) == null)
                continue;

            var new_style = (datum_glass_style)Dm.@new(style);
            Type container_type = new_style.required_container_type;
            Type reagent_type = new_style.required_drink_type;
            if (final_list[container_type] is not DmList)
                final_list[container_type] = Dm.list();
            // Check that our slot is free. If it's not free, this is an error
            if (Dm.truthy((final_list[container_type] as DmList)[reagent_type]))
                stack_trace(Dm.text("[] collided with another glass style singleton during instantiation. This means its reagent ([]) has two styles set for the same container type. This is invalid - please correct this.", style, reagent_type),
                    "code/modules/reagents/reagent_containers/cups/_glass_datum.dm", 30);

            (final_list[container_type] as DmList)[reagent_type] = new_style;
        }

        return final_list;
    }
}

/**
 * ## Glass style singleton
 *
 * Used by [/datum/component/takes_reagent_appearance], and a few other places,
 * to modify the looks of a reagent container (not /reagent_containers, any atom with reagents)
 * when certain types of reagents are put into and become the majority reagent of the container
 *
 * For example, pouring Vodka into a glass will change its icon to look like the vodka glass sprite
 * while pouring it into a shot glass will change to the vodka shot glass sprite
 *
 * A reagent type can have multiple glass styles so long as each style is linked to a different container type,
 * this allows one reagent to style multiple things across a variety of icon files
 */
// tg: code/modules/reagents/reagent_containers/cups/_glass_datum.dm:51
public partial class datum_glass_style : datum
{
    /// Helper to apply the entire style to something.
    // tg: code/modules/reagents/reagent_containers/cups/_glass_datum.dm:68
    public virtual void set_all(obj_item thing = null)
    {
        set_name(thing);
        set_desc(thing);
        set_appearance(thing);
    }

    /// Sets the passed item to our name.
    // tg: code/modules/reagents/reagent_containers/cups/_glass_datum.dm:74
    public virtual void set_name(obj_item thing = null)
    {
        thing.name = Dm.str(name);
    }

    /// Sets the passed item to our description.
    // tg: code/modules/reagents/reagent_containers/cups/_glass_datum.dm:78
    public virtual void set_desc(obj_item thing = null)
    {
        thing.desc = Dm.str(desc);
    }

    /// Sets the passed item to our icon and icon state.
    // tg: code/modules/reagents/reagent_containers/cups/_glass_datum.dm:82
    public virtual void set_appearance(obj_item thing = null)
    {
        thing.icon = icon;
        thing.icon_state = Dm.str(icon_state);
    }
}

// tg: code/modules/reagents/reagent_containers/cups/_glass_datum.dm:106
public partial class datum_glass_style_has_foodtype_soup : datum_glass_style_has_foodtype
{
    // tg: code/modules/reagents/reagent_containers/cups/_glass_datum.dm:110
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        // By default: If name or description is unset, it will inherent from the soup reagent set
        if (!Dm.truthy(name))
            name = Dm.initial(required_drink_type, (datum_reagent r) => r.name);
        if (!Dm.truthy(desc))
            desc = Dm.initial(required_drink_type, (datum_reagent r) => r.description);
    }
}
