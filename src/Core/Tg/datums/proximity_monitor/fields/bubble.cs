// Translated from tgstation (snapshot 1d99bd7f): code/datums/proximity_monitor/fields/bubble.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326: the peacekeeper cyborg's hyperkinetic dampening field): a square field drawn by edge effects in
// a holder that follows the host. The var blocks are generated (D111).

//Only use square radius for this!
// tg: code/datums/proximity_monitor/fields/bubble.dm:7
public partial class datum_proximity_monitor_advanced_bubble : datum_proximity_monitor_advanced
{
    private const string CHANGING_OFFSET = "changing_offset";
    private const string OVERLAY_DATA = "overlay_data";
    private const string STARTING_POSITION = "starting_position";
    private const float ANIMATE_DAMPENER_TIME = 1.5f * SECONDS;

    // tg: code/datums/proximity_monitor/fields/bubble.dm:17
    [DmParams("_host", "range", "_ignore_if_not_on_turf", "projector")]
    public override async DmTask New(params object[] __extra)
    {
        atom _host = Dm.arg<atom>(__extra, 0);
        atom projector = Dm.arg<atom>(__extra, 3);
        await base.New(__extra);
        setup_effect_directions();
        if (_host != projector)
            RegisterSignal(projector, COMSIG_QDELETING, nameof(on_projector_del));
        atom_movable movable_host = _host as atom_movable;
        my_movable = Dm.@new<atom_movable_field_effect_holder>(get_turf(movable_host));
        my_movable.transform = (my_movable.transform as matrix).Scale(Dm.num(current_range), Dm.num(current_range));
        my_movable.set_glide_size(movable_host.glide_size);
        draw_effect();
    }

    // tg: code/datums/proximity_monitor/fields/bubble.dm:28
    public override object Destroy(float? force = null)
    {
        foreach (object coordinates in Dm.each(edgeturf_effects.Copy()))
        {
            obj_effect_overlay_vis_field effect_to_remove = edgeturf_effects[coordinates] as obj_effect_overlay_vis_field;
            Dm.remove(ref edgeturf_effects, coordinates);
            effect_to_remove.set_wobbly(wobble_duration: ANIMATE_DAMPENER_TIME);
            animate(effect_to_remove, Dm.named("alpha", 0), Dm.named("time", ANIMATE_DAMPENER_TIME), Dm.named("flags", ANIMATION_PARALLEL));
        }
        // QDEL_IN(my_movable, ANIMATE_DAMPENER_TIME) (__DEFINES/qdel.dm:59, inline, rules 11.1 (f))
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), ANIMATE_DAMPENER_TIME > GC_FILTER_QUEUE ? WEAKREF(my_movable) : my_movable), ANIMATE_DAMPENER_TIME, file: "code/datums/proximity_monitor/fields/bubble.dm", line: 35);
        my_movable = null;
        return base.Destroy(force);
    }

    // tg: code/datums/proximity_monitor/fields/bubble.dm:38
    public virtual void setup_effect_directions()
    {
        effect_direction_images = Dm.list(
            Dm.kv(Dm.text("[]", SOUTH), image(Dm.resource("icons/effects/fields.dmi"), icon_state: "projectile_dampen_south")),
            Dm.kv(Dm.text("[]", NORTH), image(Dm.resource("icons/effects/fields.dmi"), icon_state: "projectile_dampen_north")),
            Dm.kv(Dm.text("[]", WEST), image(Dm.resource("icons/effects/fields.dmi"), icon_state: "projectile_dampen_west")),
            Dm.kv(Dm.text("[]", EAST), image(Dm.resource("icons/effects/fields.dmi"), icon_state: "projectile_dampen_east")),
            Dm.kv(Dm.text("[]", NORTHWEST), image(Dm.resource("icons/effects/fields.dmi"), icon_state: "projectile_dampen_northwest")),
            Dm.kv(Dm.text("[]", SOUTHWEST), image(Dm.resource("icons/effects/fields.dmi"), icon_state: "projectile_dampen_southwest")),
            Dm.kv(Dm.text("[]", NORTHEAST), image(Dm.resource("icons/effects/fields.dmi"), icon_state: "projectile_dampen_northeast")),
            Dm.kv(Dm.text("[]", SOUTHEAST), image(Dm.resource("icons/effects/fields.dmi"), icon_state: "projectile_dampen_southeast"))
        );
    }

    // tg: code/datums/proximity_monitor/fields/bubble.dm:50
    public override void on_moved(atom_movable source = null, atom old_loc = null)
    {
        base.on_moved(source, old_loc);
        Waiting.NoSleep(my_movable.forceMove(get_turf(source)));
    }

    // tg: code/datums/proximity_monitor/fields/bubble.dm:54
    public override void on_z_change()
    {
        recalculate_field(full_recalc: TRUE);
    }

    ///rendering all the field visuals. first we render the corners, then we connect them
    // tg: code/datums/proximity_monitor/fields/bubble.dm:58
    public virtual void draw_effect()
    {
        float max_pixel_offset = Dm.num(current_range) * ICON_SIZE_ALL;
        DmList top_right_corner = Dm.list(effect_direction_images[Dm.text("[]", NORTHEAST)], max_pixel_offset, max_pixel_offset);
        DmList top_left_corner = Dm.list(effect_direction_images[Dm.text("[]", NORTHWEST)], -max_pixel_offset, max_pixel_offset);
        DmList bottom_left_corner = Dm.list(effect_direction_images[Dm.text("[]", SOUTHWEST)], -max_pixel_offset, -max_pixel_offset);
        DmList bottom_right_corner = Dm.list(effect_direction_images[Dm.text("[]", SOUTHEAST)], max_pixel_offset, -max_pixel_offset);

        DmList corners = Dm.list(top_right_corner, top_left_corner, bottom_left_corner, bottom_right_corner);
        foreach (DmList corner in Dm.each_cast<DmList>(corners))
            draw_corner(corner);

        DmList corners_to_connect = Dm.list(
            Dm.list(Dm.kv(OVERLAY_DATA, effect_direction_images[Dm.text("[]", NORTH)]), Dm.kv(CHANGING_OFFSET, "x_offset"), Dm.kv(STARTING_POSITION, max_pixel_offset)),
            Dm.list(Dm.kv(OVERLAY_DATA, effect_direction_images[Dm.text("[]", SOUTH)]), Dm.kv(CHANGING_OFFSET, "x_offset"), Dm.kv(STARTING_POSITION, -max_pixel_offset)),
            Dm.list(Dm.kv(OVERLAY_DATA, effect_direction_images[Dm.text("[]", WEST)]), Dm.kv(CHANGING_OFFSET, "y_offset"), Dm.kv(STARTING_POSITION, -max_pixel_offset)),
            Dm.list(Dm.kv(OVERLAY_DATA, effect_direction_images[Dm.text("[]", EAST)]), Dm.kv(CHANGING_OFFSET, "y_offset"), Dm.kv(STARTING_POSITION, max_pixel_offset))
        );
        foreach (DmList direction in Dm.each_cast<DmList>(corners_to_connect))
            draw_edge(direction, max_pixel_offset);
    }

    ///rendering the corners
    // tg: code/datums/proximity_monitor/fields/bubble.dm:79
    public virtual void draw_corner(DmList corner_data = null)
    {
        obj_effect_overlay_vis_field corner_effect = Dm.@new<obj_effect_overlay_vis_field>();
        image image_overlay = corner_data[1] as image;
        corner_effect.icon = image_overlay.icon;
        corner_effect.icon_state = image_overlay.icon_state;
        corner_effect.alpha = 0;
        corner_effect.pixel_x = Dm.num(corner_data[2]);
        corner_effect.pixel_y = Dm.num(corner_data[3]);
        add_effect_to_host(corner_effect);
    }

    ///connecting the corners to one another
    // tg: code/datums/proximity_monitor/fields/bubble.dm:90
    public virtual void draw_edge(DmList edge_data = null, object target_offset = null)
    {
        float starting_offset = Dm.num(edge_data[STARTING_POSITION]);
        float current_offset = (-1 * Dm.num(target_offset)) + ICON_SIZE_ALL;
        image overlay = edge_data[OVERLAY_DATA] as image;
        while (current_offset != Dm.num(target_offset))
        {
            obj_effect_overlay_vis_field edge_effect = Dm.@new<obj_effect_overlay_vis_field>();
            edge_effect.alpha = 0;
            edge_effect.icon = overlay.icon;
            edge_effect.icon_state = overlay.icon_state;

            if (Dm.eq(edge_data[CHANGING_OFFSET], "x_offset"))
            {
                edge_effect.pixel_y = starting_offset;
                edge_effect.pixel_x = current_offset;
            }
            else
            {
                edge_effect.pixel_x = starting_offset;
                edge_effect.pixel_y = current_offset;
            }
            add_effect_to_host(edge_effect);
            current_offset += ICON_SIZE_ALL;
        }
    }

    ///handles adding the visual effect's data
    // tg: code/datums/proximity_monitor/fields/bubble.dm:110
    public virtual void add_effect_to_host(obj_effect_overlay_vis_field effect_to_add = null)
    {
        Dm.append(my_movable.vis_contents, effect_to_add);
        float coordinate_x = effect_to_add.pixel_x / ICON_SIZE_ALL;
        float coordinate_y = effect_to_add.pixel_y / ICON_SIZE_ALL;
        effect_to_add.transform = (effect_to_add.transform as matrix).Scale(1 / Dm.num(current_range), 1 / Dm.num(current_range));
        edgeturf_effects[Dm.text("[],[]", coordinate_x, coordinate_y)] = effect_to_add;
        effect_to_add.set_wobbly(wobble_duration: ANIMATE_DAMPENER_TIME);
        animate(effect_to_add, Dm.named("alpha", 255), Dm.named("time", ANIMATE_DAMPENER_TIME), Dm.named("flags", ANIMATION_PARALLEL));
    }

    // tg: code/datums/proximity_monitor/fields/bubble.dm:119
    [SignalHandler]
    public virtual void on_projector_del(datum source = null)
    {
        qdel(this);
    }
}

// tg: code/datums/proximity_monitor/fields/bubble.dm:123
public partial class obj_effect_overlay_vis_field : obj_effect_overlay_vis
{
    // tg: code/datums/proximity_monitor/fields/bubble.dm:129
    public virtual void set_wobbly(object wobble_duration = null)
    {
        if (Dm.truthy(wobbling_effect))
            return;
        wobbling_effect = TRUE;
        apply_wibbly_filters(this);
        _addtimer(Dm.@new<datum_callback>(this, nameof(remove_wobbly)), Dm.num(wobble_duration), file: "code/datums/proximity_monitor/fields/bubble.dm", line: 137);
    }

    // tg: code/datums/proximity_monitor/fields/bubble.dm:136
    public virtual void remove_wobbly()
    {
        if (QDELETED(this))
            return;
        remove_wibbly_filters(this, remove_duration: 0.25f * SECONDS);
        _addtimer(Dm.@new<datum_callback>(this, nameof(set_wobbling_effect_false)), 0.25f * SECONDS, file: "code/datums/proximity_monitor/fields/bubble.dm", line: 143); // VARSET_CALLBACK(src, wobbling_effect, FALSE)
    }

    // the VARSET_CALLBACK(src, wobbling_effect, FALSE) of remove_wobbly (rules: VARSET_CALLBACK is a named method)
    public void set_wobbling_effect_false()
    {
        wobbling_effect = FALSE;
    }
}
