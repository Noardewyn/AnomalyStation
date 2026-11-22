// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/client_colour.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice T3 (D164, the mesons' tint): the whole file. The var block is generated but for `priority` (the hand-written part keeps
// it from the wave-3 skeleton). tg filters a player's game view by colour matrix filters on its hud's game plane masters
// (update_client_colour, animate_client_colour); the port has no plane masters (rules 13, plane_master_controller.cs), so the loops over
// them run over the game plane master controller, whose own filters reach the client (protocol version 29: the eye blur's blur
// goes the same way) and the client draws the colour matrices over its 3D view (ViewBlurLayer).

// tg: code/modules/client/client_colour.dm:1  #define CLIENT_COLOR_VALUE_INDEX 1 and :2 #define CLIENT_COLOR_PRIORITY_INDEX 2 (file-local, #undef at :292-293): private consts of /mob below

// tg: code/modules/client/client_colour.dm:4
[DmPath("/datum/client_colour")]
public partial class datum_client_colour : datum
{
    // tg: code/modules/client/client_colour.dm:10
    public float priority = CLIENT_COLOR_FILTER_PRIORITY;

    // tg: code/modules/client/client_colour.dm:22
    [DmParams("owner")]
    public override async DmTask New(params object[] __extra)
    {
        mob owner = Dm.arg<mob>(__extra, 0);
        this.owner = owner;
    }

    // tg: code/modules/client/client_colour.dm:25
    public override object Destroy(float? force = null)
    {
        if (!QDELETED(owner))
        {
            Dm.remove(owner.client_colours, this);
            owner.animate_client_colour(fade_out);
        }
        owner = null;
        return base.Destroy(force);
    }

    ///Sets a new color, then updates the owner's screen color.
    // tg: code/modules/client/client_colour.dm:33
    public virtual void update_color(object new_color = null, object anim_time = null, object easing = null)
    {
        easing ??= 0;
        color = new_color;
        owner.animate_client_colour(anim_time, easing);
    }
}

public partial class mob
{
    private const int CLIENT_COLOR_VALUE_INDEX = 1;
    private const int CLIENT_COLOR_PRIORITY_INDEX = 2;

    /**
     * Add a color filter to the client
     * new_color - client_colour datum or typepath to be added
     * source - associated source for the client color
     * force - if TRUE, colors of the same source will be replaced even if it is of the same type
     */
    // tg: code/modules/client/client_colour.dm:40
    public virtual object add_client_colour(object new_color = null, object source = null, float? force = null)
    {
        force ??= FALSE;
        if (QDELING(this))
            return null;

        if (ispath(new_color))
            new_color = Dm.@new((Type)new_color, this);

        if (new_color is not datum_client_colour new_colour)
            throw Dm.CRASH(Dm.text("Invalid color type or datum for add_client_colour: []", Dm.truthy(new_color) ? Dm.text("[] ([])", new_color, ((datum)new_color).type) : null), "code/modules/client/client_colour.dm", 48);

        // Ensure that if a color with this source is already present, we either abort or get rid of it
        datum_client_colour existing_color = get_client_colour(source) as datum_client_colour;
        if (Dm.truthy(existing_color))
        {
            if (existing_color.type == new_colour.type && !Dm.truthy(force))
                return existing_color;
            qdel(existing_color);
        }
        client_colours[new_colour] = source;
        animate_client_colour(new_colour.fade_in);
        return new_colour;
    }

    /**
     * Removes a color type from a specific source from mob's client_colours list
     * source - color source to remove
    */
    // tg: code/modules/client/client_colour.dm:70
    public virtual object remove_client_colour(object source = null)
    {
        datum_client_colour existing_color = get_client_colour(source) as datum_client_colour;
        if (!Dm.truthy(existing_color))
            return FALSE;
        qdel(existing_color);
        return TRUE;
    }

    // tg: code/modules/client/client_colour.dm:77
    public virtual object get_client_colour(object source = null)
    {
        foreach (datum_client_colour color in Dm.each_cast<datum_client_colour>(client_colours))
        {
            if (Dm.eq(client_colours[color], source))
                return color;
        }
        return null;
    }

    // tg: code/modules/client/client_colour.dm:82
    public virtual object get_client_colour_filters()
    {
        DmList dot = null;
        dot = Dm.list();
        // sortTim sorts the passed list instead of making the copy, and so does reverse_range
        DmList used_colors = reverse_range(sortTim(client_colours.Copy(), "/proc/" + nameof(GlobalProcs.cmp_client_colours)) as DmList) as DmList;
        object current_color = null;
        float color_num = 0;
        float color_prio = 1;

        foreach (datum_client_colour client_color in Dm.each_cast<datum_client_colour>(used_colors))
        {
            color_num += 1;

            DmList filter_color = null;
            if (islist(client_color.color))
            {
                filter_color = client_color.color as DmList;
                // If our list has "type" in it then its a filter
                if (!Dm.truthy(filter_color["type"]))
                    filter_color = null;
            }

            if (Dm.truthy(client_color.split_filters) || filter_color != null)
            {
                if (Dm.truthy(current_color))
                {
                    Dm.append(ref dot, Dm.list(Dm.list(color_matrix_filter(current_color), color_prio)));
                    color_prio += 1;
                    current_color = null;
                }

                Dm.append(ref dot, Dm.list(Dm.list(filter_color ?? color_matrix_filter(client_color.color), color_prio)));
                color_prio += 1;
                continue;
            }

            if (!Dm.truthy(current_color))
            {
                current_color = client_color.color;
                if (Dm.truthy(client_color.@override))
                    break;
                continue;
            }

            object color_list = current_color;
            if (!islist(color_list))
                color_list = color_to_full_rgba_matrix(color_list);
            DmList cur_list = color_to_full_rgba_matrix(client_color.color) as DmList;

            for (float i = 1; i <= 20; i++)
                ((DmList)color_list)[i] = Dm.div(Dm.add(Dm.mul(((DmList)color_list)[i], color_num - 1), cur_list[i]), color_num);
            current_color = color_list;

            if (Dm.truthy(client_color.@override))
                break;
        }

        if (Dm.truthy(current_color))
            Dm.append(ref dot, Dm.list(Dm.list(color_matrix_filter(current_color), color_prio)));
        return dot;
    }

    // tg: code/modules/client/client_colour.dm:126
    public virtual void update_client_colour()
    {
        if (hud_used == null)
            return;

        foreach (datum game_plane in Dm.each_cast<datum>(client_colour_planes()))
        {
            foreach (object filter_id in Dm.each(color_filter_store))
                game_plane.remove_filter(filter_id);
        }

        color_filter_store.Cut();
        DmList applied_filters = get_client_colour_filters() as DmList;

        foreach (DmList color_filter in Dm.each_cast<DmList>(applied_filters))
        {
            DmList added_color = color_filter[CLIENT_COLOR_VALUE_INDEX] as DmList;
            object filter_priority = color_filter[CLIENT_COLOR_PRIORITY_INDEX];
            foreach (datum game_plane in Dm.each_cast<datum>(client_colour_planes()))
            {
                string filter_id = Dm.text("client_colour_[]", filter_priority);
                game_plane.add_filter(filter_id, filter_priority, added_color);
                Dm.combine(color_filter_store, filter_id);
            }
        }
    }

    /// Works similarly to 'update_client_colour', but animated.
    // tg: code/modules/client/client_colour.dm:149
    public virtual void animate_client_colour(object anim_time = null, object anim_easing = null)
    {
        anim_time ??= 1 * SECONDS;
        anim_easing ??= NONE;
        if (hud_used == null)
            return;

        if (Dm.le(anim_time, -1))
        {
            update_client_colour();
            return;
        }

        foreach (datum game_plane in Dm.each_cast<datum>(client_colour_planes()))
        {
            foreach (object filter_id in Dm.each(color_filter_store))
                game_plane.remove_filter(filter_id);
        }

        color_filter_store.Cut();
        DmList applied_filters = get_client_colour_filters() as DmList;

        foreach (DmList color_filter in Dm.each_cast<DmList>(applied_filters))
        {
            DmList added_color = color_filter[CLIENT_COLOR_VALUE_INDEX] as DmList;
            object filter_priority = color_filter[CLIENT_COLOR_PRIORITY_INDEX];
            foreach (datum game_plane in Dm.each_cast<datum>(client_colour_planes()))
            {
                string filter_id = Dm.text("client_colour_[]", filter_priority);
                game_plane.add_filter(filter_id, filter_priority, color_matrix_filter() as DmList);
                game_plane.transition_filter(filter_id, added_color, anim_time, anim_easing);
                Dm.combine(color_filter_store, filter_id);
            }
        }
    }

    // The port's seam for `hud_used.get_true_plane_masters(RENDER_PLANE_GAME)` (:131, :154 and :158): the game plane masters, which the
    // port does not have (rules 13), and the game plane master controller, which carries the filters to the client (protocol version 29).
    private DmList client_colour_planes()
    {
        DmList planes = Dm.list();
        foreach (datum game_plane in Dm.each_cast<datum>(hud_used.get_true_plane_masters(RENDER_PLANE_GAME)))
            Dm.append(planes, game_plane);
        if (hud_used.plane_master_controllers[PLANE_MASTERS_GAME] is atom_movable_plane_master_controller controller)
            Dm.append(planes, controller);
        return planes;
    }
}

// Color types

///we want it to be less harsh for players to take blindness quirk, this adds enough color to not cause too much eye strain
// tg: code/modules/client/client_colour.dm:174  (a var-only type: the generated one)

// tg: code/modules/client/client_colour.dm:204
public partial class datum_client_colour_monochrome : datum_client_colour
{
    // tg: code/modules/client/client_colour.dm:211
    [DmParams("owner")]
    public override async DmTask New(params object[] __extra)
    {
        mob owner = Dm.arg<mob>(__extra, 0);
        await base.New(Dm.forward(__extra, owner));
        if (Dm.truthy(owner))
            ADD_TRAIT(owner, TRAIT_COLORBLIND, type);
    }

    // tg: code/modules/client/client_colour.dm:216
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(owner))
            REMOVE_TRAIT(owner, TRAIT_COLORBLIND, type);
        return base.Destroy(force);
    }
}

// tg: code/modules/client/client_colour.dm:228
public partial class datum_client_colour_bloodlust : datum_client_colour
{
    // tg: code/modules/client/client_colour.dm:233
    [DmParams("owner")]
    public override async DmTask New(params object[] __extra)
    {
        mob owner = Dm.arg<mob>(__extra, 0);
        await base.New(Dm.forward(__extra, owner));
        if (Dm.truthy(owner))
            _addtimer(Dm.@new<datum_callback>(this, nameof(update_color), Dm.list(/*R*/ 1, 0, 0, /*G*/ 0.8f, 0.2f, 0, /*B*/ 0.8f, 0, 0.2f, /*C*/ 0.1f, 0, 0), 10, Dm.bitor(SINE_EASING, EASE_OUT)), 0.1f * SECONDS, file: "code/modules/client/client_colour.dm", line: 236);
    }
}
