// Translated from tgstation (snapshot 1d99bd7f): code/game/atom/atom_color.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/*
	Atom Colour Priority System
	A System that gives finer control over which atom colour to colour the atom with.
	The "highest priority" one is always displayed as opposed to the default of
	"whichever was set last is displayed"

	It can also be used for color filters, since some effects (using non-RGB space matrices)
	are impossible to achieve with just the color variable
*/

// The vars atom_colours and cached_color_filter (atom_color.dm:17-19) are in the generated part (Generated/Types/atom.g.cs).
// M3 P4 (the lead's assignment): the procs the inert init and the live map types reach are translated; color_atom_overlay
// waits for filters (presentation, M3 appearance or later).

// tg: code/game/atom/atom_color.dm:22
public partial class atom : datum
{
    ///Adds an instance of colour_type to the atom's atom_colours list
    // tg: code/game/atom/atom_color.dm:22
    public virtual async DmTask add_atom_colour(object coloration = null, object colour_priority = null)
    {
        if (!Dm.truthy(atom_colours) || !Dm.truthy(atom_colours.len))
        {
            atom_colours = Dm.list();
            atom_colours.len = COLOUR_PRIORITY_AMOUNT; //four priority levels currently.
        }
        if (!Dm.truthy(coloration))
            return;
        if (Dm.gt(colour_priority, atom_colours.len))
            return;
        string color_type = ATOM_COLOR_TYPE_NORMAL;
        if (islist(coloration))
        {
            DmList color_matrix = coloration as DmList;
            if (Dm.eq(color_matrix["type"], "color"))
                color_type = ATOM_COLOR_TYPE_FILTER;
        }
        atom_colours[colour_priority] = Dm.list(coloration, color_type);
        await update_atom_colour();
    }

    ///Removes an instance of colour_type from the atom's atom_colours list
    // tg: code/game/atom/atom_color.dm:39
    public virtual async DmTask remove_atom_colour(object colour_priority = null, object coloration = null)
    {
        if (!Dm.truthy(atom_colours))
            return;
        if (Dm.gt(colour_priority, atom_colours.len))
            return;
        if (Dm.truthy(coloration) && Dm.truthy(atom_colours[colour_priority]))
        {
            if (Dm.eq(((DmList)atom_colours[colour_priority])[ATOM_COLOR_TYPE_INDEX], ATOM_COLOR_TYPE_NORMAL))
            {
                if (!Dm.eq(((DmList)atom_colours[colour_priority])[ATOM_COLOR_VALUE_INDEX], coloration))
                    return; //if we don't have the expected color (for a specific priority) to remove, do nothing
            }
            else
            {
                if (!islist(coloration) || !Dm.truthy(compare_list(coloration as DmList, ((DmList)((DmList)atom_colours[colour_priority])[ATOM_COLOR_VALUE_INDEX])["color"] as DmList)))
                    return;
            }
        }
        atom_colours[colour_priority] = null;
        await update_atom_colour();
    }

    /**
     * Checks if this atom has the passed color
     * Can optionally be supplied with a range of priorities, IE only checking "washable" or above
     */
    // tg: code/game/atom/atom_color.dm:58
    public virtual object is_atom_colour(object looking_for_color = null, float? min_priority_index = null, float? max_priority_index = null)
    {
        min_priority_index ??= 1;
        max_priority_index ??= COLOUR_PRIORITY_AMOUNT;
        // make sure uppertext hex strings don't mess with LOWER_TEXT hex strings
        if (!islist(looking_for_color))
            looking_for_color = LOWER_TEXT(looking_for_color);

        if (!Dm.truthy(LAZYLEN(atom_colours)))
        {
            // no atom colors list has been set up, just check the color var
            if (!islist(color))
                return Dm.num(Dm.eq(LOWER_TEXT(color), looking_for_color));
            if (!islist(looking_for_color))
                return FALSE;
            return compare_list(color as DmList, looking_for_color as DmList);
        }

        for (float i = Dm.num(min_priority_index), i__end = Dm.num(max_priority_index); i <= i__end; i++)
        {
            if (!Dm.truthy(atom_colours[i]))
                continue;

            if (!islist(looking_for_color))
            {
                if (islist(((DmList)atom_colours[i])[ATOM_COLOR_VALUE_INDEX]))
                    continue;

                if (Dm.eq(LOWER_TEXT(((DmList)atom_colours[i])[ATOM_COLOR_VALUE_INDEX]), looking_for_color))
                    return TRUE;

                continue;
            }

            object compared_matrix = ((DmList)atom_colours[i])[ATOM_COLOR_VALUE_INDEX];
            if (Dm.eq(((DmList)atom_colours[i])[ATOM_COLOR_TYPE_INDEX], ATOM_COLOR_TYPE_FILTER))
                compared_matrix = ((DmList)((DmList)atom_colours[i])[ATOM_COLOR_VALUE_INDEX])["color"];

            if (Dm.truthy(compare_list(looking_for_color as DmList, compared_matrix as DmList)))
                return TRUE;
        }

        return FALSE;
    }

    ///Resets the atom's color to null, and then sets it to the highest priority colour available
    // tg: code/game/atom/atom_color.dm:94
    public virtual async DmTask update_atom_colour()
    {
        DmList old_filter = cached_color_filter;
        object old_color = color;
        color = null;
        cached_color_filter = null;
        remove_filter(ATOM_PRIORITY_COLOR_FILTER);
        REMOVE_KEEP_TOGETHER(this, ATOM_COLOR_TRAIT);

        if (!Dm.truthy(atom_colours))
        {
            if (!Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_COLOR_UPDATED]) ? NONE : this._SendSignal(COMSIG_ATOM_COLOR_UPDATED, Dm.list(this, (old_color is var or__1 && Dm.truthy(or__1) ? or__1 : old_filter)))), COMPONENT_CANCEL_COLOR_APPEARANCE_UPDATE)) && Dm.truthy(old_filter))
                update_appearance();
            return;
        }

        foreach (DmList checked_color in Dm.each<DmList>(atom_colours))
        {
            if (Dm.eq(checked_color[ATOM_COLOR_TYPE_INDEX], ATOM_COLOR_TYPE_FILTER))
            {
                add_filter(ATOM_PRIORITY_COLOR_FILTER, ATOM_PRIORITY_COLOR_FILTER_PRIORITY, checked_color[ATOM_COLOR_VALUE_INDEX] as DmList);
                cached_color_filter = checked_color[ATOM_COLOR_VALUE_INDEX] as DmList;
                break;
            }

            if (Dm.truthy(length(checked_color[ATOM_COLOR_VALUE_INDEX])))
            {
                color = checked_color[ATOM_COLOR_VALUE_INDEX];
                break;
            }
        }

        ADD_KEEP_TOGETHER(this, ATOM_COLOR_TRAIT);
        if (!Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_COLOR_UPDATED]) ? NONE : this._SendSignal(COMSIG_ATOM_COLOR_UPDATED, Dm.list(this, Dm.num(!Dm.eq(old_color, color) || old_filter != cached_color_filter)))), COMPONENT_CANCEL_COLOR_APPEARANCE_UPDATE)) && cached_color_filter != old_filter)
            update_appearance();
    }

    /// Same as update_atom_color, but simplifies overlay coloring
    // tg: code/game/atom/atom_color.dm:122
    // M6 slice 7b (D156): the colour filter reaches the client (protocol version 25, its colour matrix).
    public virtual object color_atom_overlay(mutable_appearance overlay = null)
    {
        DmList color_filter = cached_color_filter;
        if (islist(color))
        {
            overlay.color = null;
            color_filter = Dm.truthy(color_filter) ? color_filter : color_matrix_filter(color_to_full_rgba_matrix(color), FILTER_COLOR_RGB) as DmList;
        }
        else
            overlay.color = color;

        if (!Dm.truthy(color_filter))
            return overlay;
        // Apply the atom's color filter to the overlay using named filters so that
        // later calls to add_filter/update_filters (e.g., height displacement filters)
        // do not wipe out our coloration. Mirror prior behavior by propagating to
        // child overlays unless KEEP_TOGETHER is present.
        overlay.add_filter(ATOM_PRIORITY_COLOR_FILTER, ATOM_PRIORITY_COLOR_FILTER_PRIORITY, color_filter);

        if (!Dm.truthy(Dm.bitand(overlay.appearance_flags, KEEP_TOGETHER)))
        {
            // Recursively ensure any nested overlays/underlays also get the color filter
            foreach (mutable_appearance child_overlay in Dm.each_cast<mutable_appearance>(overlay.overlays))
                if (!Dm.truthy(Dm.bitand(child_overlay.appearance_flags, KEEP_APART)))
                    color_atom_overlay(child_overlay);
            foreach (mutable_appearance child_underlay in Dm.each_cast<mutable_appearance>(overlay.underlays))
                if (!Dm.truthy(Dm.bitand(child_underlay.appearance_flags, KEEP_APART)))
                    color_atom_overlay(child_underlay);
        }

        return overlay;
    }
}
