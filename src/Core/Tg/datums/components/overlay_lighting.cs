// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/overlay_lighting.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the flashlight, M4a's light in a hand). The holder tracking, the range, power, colour and direction, the
// directional cast and the dynamic luminosity are tg's. The light overlays themselves are drawn by the client (D24): tg's
// `current_holder.underlays += visible_mask` (and the cone) is the presentation call IAppearanceSink.LightChanged on the
// holder, which the replicator turns into the holder entity's light (docs/ai/specs/light-grid.md section 1, DV61); the images
// are kept with tg's values, nothing draws them. The var block is generated (D111).

///For switchable lights, is it on and currently emitting light?
// tg: code/datums/components/overlay_lighting.dm:2  #define LIGHTING_ON (1<<0)
///Is the parent attached to something else, its loc? Then we need to keep an eye of this.
// tg: code/datums/components/overlay_lighting.dm:4  #define LIGHTING_ATTACHED (1<<1)
// tg: code/datums/components/overlay_lighting.dm:6  #define GET_PARENT (parent_attached_to || parent)
// tg: code/datums/components/overlay_lighting.dm:8  #define SHORT_CAST 2

/**
 * Movable atom overlay-based lighting component.
 *
 * * Component works by applying a visual object to the parent target.
 *
 * * The component tracks the parent's loc to determine the current_holder.
 * * The current_holder is either the parent or its loc, whichever is on a turf. If none, then the current_holder is null and the light is not visible.
 */
// tg: code/datums/components/overlay_lighting.dm:25
public partial class datum_component_overlay_lighting : datum_component
{
    private const int LIGHTING_ON = 1 << 0;
    private const int LIGHTING_ATTACHED = 1 << 1;
    private const int SHORT_CAST = 2;

    private atom_movable GET_PARENT => parent_attached_to ?? parent as atom_movable;

    // tg: code/datums/components/overlay_lighting.dm:76
    [DmParams("_range", "_power", "_color", "starts_on", "is_directional", "is_beam", "force")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object _range = Dm.arg(__extra, 0);
        object _power = Dm.arg(__extra, 1);
        object _color = Dm.arg(__extra, 2);
        object starts_on = Dm.arg(__extra, 3);
        object is_directional = Dm.arg(__extra, 4);
        object is_beam = Dm.arg(__extra, 5);
        object force = Dm.arg(__extra, 6);
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;

        var movable_parent = (atom_movable)parent;
        float system = movable_parent.light_system;
        if (!Dm.truthy(force) && !(system == OVERLAY_LIGHT || system == OVERLAY_LIGHT_DIRECTIONAL || system == OVERLAY_LIGHT_BEAM)) // IS_OVERLAY_LIGHT_SYSTEM, inline
        {
            stack_trace(Dm.text("[] added to [], with [] value for the light_system var. Use [], [] or [] instead.", GetType(), parent, movable_parent.light_system, OVERLAY_LIGHT, OVERLAY_LIGHT_DIRECTIONAL, OVERLAY_LIGHT_BEAM), "code/datums/components/overlay_lighting.dm", 82);
            return COMPONENT_INCOMPATIBLE;
        }

        object dot = await base.Initialize(__extra);

        visible_mask = image(Dm.resource("icons/effects/light_overlays/light_32.dmi"), icon_state: "light");
        SET_PLANE_EXPLICIT(visible_mask, O_LIGHTING_VISUAL_PLANE, movable_parent);
        visible_mask.appearance_flags = Dm.num(Dm.bitor(Dm.bitor(RESET_COLOR, RESET_ALPHA), RESET_TRANSFORM));
        visible_mask.alpha = 0;
        visible_mask.blend_mode = BLEND_ADD;
        if (Dm.truthy(is_directional))
        {
            directional = TRUE;
            cone = image(Dm.resource("icons/effects/light_overlays/light_cone.dmi"), icon_state: "light");
            SET_PLANE_EXPLICIT(cone, O_LIGHTING_VISUAL_PLANE, movable_parent);
            cone.appearance_flags = Dm.num(Dm.bitor(Dm.bitor(RESET_COLOR, RESET_ALPHA), RESET_TRANSFORM));
            cone.alpha = 110;
            cone.blend_mode = BLEND_ADD;
            cone.transform = ((matrix)cone.transform).Translate(-32, -32);
            set_direction(movable_parent.dir);
        }
        if (Dm.truthy(is_beam))
            beam = TRUE;
        if (_range != null)
            movable_parent.set_light_range(_range);
        set_range(movable_parent, movable_parent.light_range);
        if (_power != null)
            movable_parent.set_light_power(_power);
        set_power(movable_parent, movable_parent.light_power);
        if (_color != null)
            movable_parent.set_light_color(_color);
        set_color(movable_parent, movable_parent.light_color);
        if (starts_on != null)
            await movable_parent.set_light_on(starts_on);
        set_light_render_source(movable_parent, "");
        return dot;
    }

    // tg: code/datums/components/overlay_lighting.dm:116
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        if (Dm.truthy(directional))
            RegisterSignal(parent, COMSIG_ATOM_DIR_CHANGE, nameof(on_parent_dir_change));
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_LIGHT_RANGE, nameof(set_range));
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_LIGHT_POWER, nameof(set_power));
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_LIGHT_COLOR, nameof(set_color));
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_LIGHT_ON, nameof(on_toggle));
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_LIGHT_FLAGS, nameof(on_light_flags_change));
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_LIGHT_RENDER_SOURCE, nameof(set_light_render_source));
        RegisterSignal(parent, COMSIG_ATOM_USED_IN_CRAFT, nameof(on_parent_crafted));
        RegisterSignal(parent, COMSIG_LIGHT_EATER_QUEUE, nameof(on_light_eater));
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(on_parent_moved));
        RegisterSignal(parent, COMSIG_MOVABLE_Z_CHANGED, nameof(on_z_move));
        RegisterSignal(parent, COMSIG_ITEM_BEFORE_PICKUP_ANIMATION, nameof(on_pickup_anim));
        var movable_parent = (atom_movable)parent;
        if (Dm.truthy(Dm.bitand(movable_parent.light_flags, LIGHT_ATTACHED)))
        {
            overlay_lighting_flags = Dm.num(Dm.bitor(overlay_lighting_flags, LIGHTING_ATTACHED));
            set_parent_attached_to(ismovable(movable_parent.loc) ? (atom_movable)movable_parent.loc : null);
        }
        check_holder();
        if (Dm.truthy(movable_parent.light_on))
            turn_on();
    }

    // tg: code/datums/components/overlay_lighting.dm:140
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        overlay_lighting_flags = Dm.num(Dm.bitand(overlay_lighting_flags, ~LIGHTING_ATTACHED));
        set_parent_attached_to(null);
        set_holder(null);
        UnregisterSignal(parent, Dm.list(
            COMSIG_MOVABLE_MOVED,
            COMSIG_MOVABLE_Z_CHANGED,
            COMSIG_ATOM_UPDATE_LIGHT_RANGE,
            COMSIG_ATOM_UPDATE_LIGHT_POWER,
            COMSIG_ATOM_UPDATE_LIGHT_COLOR,
            COMSIG_ATOM_UPDATE_LIGHT_ON,
            COMSIG_ATOM_UPDATE_LIGHT_FLAGS,
            COMSIG_ATOM_USED_IN_CRAFT,
            COMSIG_LIGHT_EATER_QUEUE));
        if (Dm.truthy(directional))
            UnregisterSignal(parent, COMSIG_ATOM_DIR_CHANGE);
        if (Dm.truthy(Dm.bitand(overlay_lighting_flags, LIGHTING_ON)))
            turn_off();
        await base.UnregisterFromParent(__extra);
    }

    // tg: code/datums/components/overlay_lighting.dm:162
    public override object Destroy(float? force = null)
    {
        set_parent_attached_to(null);
        set_holder(null);
        visible_mask = null;
        cone = null;
        parent_attached_to = null;
        return base.Destroy(force);
    }

    /// Clears ourselves from spatial grid's dynlights lists
    // tg: code/datums/components/overlay_lighting.dm:171
    public virtual void clean_old_cells(atom holder_loc = null)
    {
        turf holder_turf = get_turf(holder_loc);
        if (holder_turf == null)
            return;
        foreach (object entry in Dm.each(SSspatial_grid.get_cells_in_range(holder_turf, lumcount_range)))
        {
            var grid_cell = (datum_spatial_grid_cell)entry;
            GRID_CELL_REMOVE(ref grid_cell.dynamic_light_sources, this);
        }
    }

    /// Populates the affected_turfs lazylist, adding to its contents the effects of being near the light.
    // tg: code/datums/components/overlay_lighting.dm:179
    public virtual void register_new_cells()
    {
        if (current_holder == null)
            return;
        turf holder_turf = get_turf(current_holder);
        if (holder_turf == null)
            return;
        foreach (object entry in Dm.each(SSspatial_grid.get_cells_in_range(holder_turf, lumcount_range)))
        {
            var grid_cell = (datum_spatial_grid_cell)entry;
            GRID_CELL_ASSOC_SET(ref grid_cell.dynamic_light_sources, this, lum_power);
        }
    }

    /// Clears the old affected cells and populates the new ones.
    // tg: code/datums/components/overlay_lighting.dm:189
    public virtual void make_luminosity_update(atom old_loc = null)
    {
        if (old_loc != null)
            clean_old_cells(old_loc);
        if (!isturf(current_holder?.loc))
            return;
        if (Dm.truthy(directional))
            cast_directional_light();
        else
            show_to_holder();
        register_new_cells();
    }

    /// Adds the luminosity and source for the affected movable atoms to keep track of their visibility.
    // tg: code/datums/components/overlay_lighting.dm:201
    public virtual void add_dynamic_lumi()
    {
        LAZYSET(ref current_holder.affected_dynamic_lights, this, lumcount_range + 1);
        show_to_holder();
        current_holder.update_dynamic_luminosity();
    }

    /// Removes the luminosity and source for the affected movable atoms to keep track of their visibility.
    // tg: code/datums/components/overlay_lighting.dm:207
    public virtual void remove_dynamic_lumi()
    {
        LAZYREMOVE(ref current_holder.affected_dynamic_lights, this);
        hide_from_holder();
        current_holder.update_dynamic_luminosity();
    }

    /// Adds our overlays to our holder, assuming everything's setup proper
    // tg: code/datums/components/overlay_lighting.dm:213
    public virtual void show_to_holder()
    {
        if (Dm.truthy(currently_displaying))
            return;
        if (current_holder == null || !Dm.truthy(Dm.bitand(overlay_lighting_flags, LIGHTING_ON)))
        {
            currently_displaying = FALSE;
            return;
        }
        // current_holder.underlays += visible_mask (and += cone when directional): the overlay light is the client's (D24);
        // its appearance on the holder is the presentation call (DV61).
        holder_light_changed();
        currently_displaying = TRUE;
        // These are very intentionally copies so recipients cannot
        // Accidentially brick lighting overlays by mutating them
        Dm.Deferred("code/datums/components/overlay_lighting.dm:224", "show_to_holder: COMSIG_ATOM_OVERLAY_LIGHT_APPLIED and COMSIG_ATOM_HOLDER_OVERLAY_LIGHT_APPLIED with the mask and cone copies (their listeners, the light middleman and the delusions, are not translated)");
    }

    /// Removes our overlay from our holder, assuming everything's setup proper
    /// MUST be called before modifying cone or visible_mask, or you will cause stuck lighting
    // tg: code/datums/components/overlay_lighting.dm:232
    public virtual void hide_from_holder()
    {
        if (!Dm.truthy(currently_displaying))
            return;
        if (current_holder == null || !Dm.truthy(Dm.bitand(overlay_lighting_flags, LIGHTING_ON)))
            return;
        // current_holder.underlays -= visible_mask (and -= cone): the presentation call, as in show_to_holder (DV61).
        currently_displaying = FALSE;
        holder_light_changed();
        Dm.Deferred("code/datums/components/overlay_lighting.dm:241", "hide_from_holder: COMSIG_ATOM_OVERLAY_LIGHT_REMOVED and COMSIG_ATOM_HOLDER_OVERLAY_LIGHT_REMOVED (their listeners are not translated)");
    }

    /// Called to change the value of parent_attached_to.
    // tg: code/datums/components/overlay_lighting.dm:245
    public virtual object set_parent_attached_to(atom_movable new_parent_attached_to = null)
    {
        if (new_parent_attached_to == parent_attached_to)
            return null;

        object dot = parent_attached_to;
        parent_attached_to = new_parent_attached_to;
        if (dot != null)
        {
            var old_parent_attached_to = (atom_movable)dot;
            UnregisterSignal(old_parent_attached_to, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_MOVED, COMSIG_LIGHT_EATER_QUEUE));
            if (old_parent_attached_to == current_holder)
            {
                RegisterSignal(old_parent_attached_to, COMSIG_QDELETING, nameof(on_holder_qdel));
                RegisterSignal(old_parent_attached_to, COMSIG_MOVABLE_MOVED, nameof(on_holder_moved));
                RegisterSignal(old_parent_attached_to, COMSIG_LIGHT_EATER_QUEUE, nameof(on_light_eater));
            }
        }
        if (parent_attached_to != null)
        {
            if (parent_attached_to == current_holder)
                UnregisterSignal(current_holder, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_MOVED, COMSIG_LIGHT_EATER_QUEUE));
            RegisterSignal(parent_attached_to, COMSIG_QDELETING, nameof(on_parent_attached_to_qdel));
            RegisterSignal(parent_attached_to, COMSIG_MOVABLE_MOVED, nameof(on_parent_attached_to_moved));
            RegisterSignal(parent_attached_to, COMSIG_LIGHT_EATER_QUEUE, nameof(on_light_eater));
        }
        check_holder();
        return dot;
    }

    /// Called to change the value of current_holder.
    // tg: code/datums/components/overlay_lighting.dm:268
    public virtual void set_holder(atom_movable new_holder = null)
    {
        if (new_holder == current_holder)
            return;

        atom old_loc = null;
        if (current_holder != null)
        {
            old_loc = get_turf(current_holder);
            if (current_holder != parent && current_holder != parent_attached_to)
            {
                UnregisterSignal(current_holder, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_MOVED, COMSIG_LIGHT_EATER_QUEUE));
                if (Dm.truthy(directional))
                    UnregisterSignal(current_holder, COMSIG_ATOM_DIR_CHANGE);
            }
            if (Dm.truthy(Dm.bitand(overlay_lighting_flags, LIGHTING_ON)))
                remove_dynamic_lumi();
        }

        current_holder = new_holder;

        if (new_holder == null)
        {
            clean_old_cells(old_loc);
            return;
        }

        if (new_holder != parent && new_holder != parent_attached_to)
        {
            RegisterSignal(new_holder, COMSIG_QDELETING, nameof(on_holder_qdel));
            RegisterSignal(new_holder, COMSIG_LIGHT_EATER_QUEUE, nameof(on_light_eater));
            if (Dm.truthy(Dm.bitand(overlay_lighting_flags, LIGHTING_ON)))
                RegisterSignal(new_holder, COMSIG_MOVABLE_MOVED, nameof(on_holder_moved));
            if (Dm.truthy(directional))
                RegisterSignal(new_holder, COMSIG_ATOM_DIR_CHANGE, nameof(on_holder_dir_change));
        }

        if (Dm.truthy(directional) && !Dm.eq(current_direction, new_holder.dir))
            current_direction = new_holder.dir;

        if (Dm.truthy(Dm.bitand(overlay_lighting_flags, LIGHTING_ON)))
        {
            add_dynamic_lumi();
            make_luminosity_update(old_loc);
        }
    }

    /// Used to determine the new valid current_holder from the parent's loc.
    // tg: code/datums/components/overlay_lighting.dm:304
    public virtual void check_holder()
    {
        atom_movable movable_parent = GET_PARENT;
        if (QDELETED(movable_parent))
        {
            set_holder(null);
            return;
        }
        if (isturf(movable_parent.loc))
        {
            set_holder(movable_parent);
            return;
        }
        var inside = movable_parent.loc as atom; //Parent's loc
        if (inside == null)
        {
            set_holder(null);
            return;
        }
        if (isturf(inside.loc))
        {
            // storage items block light, also don't be moving into a qdeleted item
            if (QDELETED(inside) || inside is obj_item_storage)
                set_holder(null);
            else
                set_holder(inside as atom_movable);
            return;
        }
        set_holder(null);
    }

    /// Called when the current_holder is qdeleted, to remove the light effect.
    // tg: code/datums/components/overlay_lighting.dm:327
    [SignalHandler]
    public virtual void on_holder_qdel(atom_movable source = null, object force = null)
    {
        if (QDELETED(current_holder))
        {
            set_holder(null);
            return;
        }
        UnregisterSignal(current_holder, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_MOVED));
        if (Dm.truthy(directional))
            UnregisterSignal(current_holder, COMSIG_ATOM_DIR_CHANGE);
        set_holder(null);
    }

    /// Called when current_holder changes loc.
    // tg: code/datums/components/overlay_lighting.dm:339
    [SignalHandler]
    public virtual void on_holder_moved(atom_movable source = null, atom old_loc = null, object dir = null, object forced = null)
    {
        if (!Dm.truthy(Dm.bitand(overlay_lighting_flags, LIGHTING_ON)))
            return;
        make_luminosity_update(old_loc);
    }

    ///Called when parent changes loc.
    // tg: code/datums/components/overlay_lighting.dm:346
    [SignalHandler]
    public virtual void on_parent_moved(atom_movable source = null, atom old_loc = null, object dir = null, object forced = null)
    {
        var movable_parent = (atom_movable)parent;
        clean_old_cells(old_loc); // Clean first, as we might swap holders
        if (Dm.truthy(Dm.bitand(overlay_lighting_flags, LIGHTING_ATTACHED)))
            set_parent_attached_to(ismovable(movable_parent.loc) ? (atom_movable)movable_parent.loc : null);
        check_holder();
        if (!Dm.truthy(Dm.bitand(overlay_lighting_flags, LIGHTING_ON)) || current_holder == null)
            return;
        make_luminosity_update();
    }

    // tg: code/datums/components/overlay_lighting.dm:357
    [SignalHandler]
    public virtual void on_z_move(atom source = null)
    {
        hide_from_holder();
        SET_PLANE_EXPLICIT(visible_mask, O_LIGHTING_VISUAL_PLANE, source);
        if (cone != null)
            SET_PLANE_EXPLICIT(cone, O_LIGHTING_VISUAL_PLANE, source);
        show_to_holder();
    }

    /// Avoids duplicate overlays (one from our NEXT holder, selected after the animation, one from the pickup animation)
    // tg: code/datums/components/overlay_lighting.dm:366
    [SignalHandler]
    public virtual void on_pickup_anim(atom source = null)
    {
        hide_from_holder();
    }

    /// Called when the current_holder is qdeleted, to remove the light effect.
    // tg: code/datums/components/overlay_lighting.dm:371
    [SignalHandler]
    public virtual void on_parent_attached_to_qdel(atom_movable source = null, object force = null)
    {
        if (parent_attached_to == null)
            return;
        UnregisterSignal(parent_attached_to, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_MOVED));
        if (Dm.truthy(directional))
            UnregisterSignal(parent_attached_to, COMSIG_ATOM_DIR_CHANGE);
        if (parent_attached_to == current_holder)
            set_holder(null);
        set_parent_attached_to(null);
    }

    /// Called when parent_attached_to changes loc.
    // tg: code/datums/components/overlay_lighting.dm:384
    [SignalHandler]
    public virtual void on_parent_attached_to_moved(atom_movable source = null, atom old_loc = null, object dir = null, object forced = null)
    {
        clean_old_cells(old_loc); // Clean first, as we might swap holders
        check_holder();
        if (!Dm.truthy(Dm.bitand(overlay_lighting_flags, LIGHTING_ON)) || current_holder == null)
            return;
        make_luminosity_update();
    }

    ///Changes the range which the light reaches. 0 means no light, 6 is the maximum value.
    // tg: code/datums/components/overlay_lighting.dm:394
    [SignalHandler]
    public virtual void set_range(atom source = null, object old_range = null)
    {
        float new_range = source.light_range;
        if (range == new_range)
            return;
        if (new_range == 0)
            turn_off();
        range = clamp(CEILING(new_range, 0.5f), 1, 6);
        float pixel_bounds = ((range.Value - 1) * 64) + 32;
        lumcount_range = ceil(range.Value);
        hide_from_holder();

        visible_mask.icon = TgState.Current.datum_component_overlay_lighting__light_overlays[Dm.text("[]", pixel_bounds)];
        if (pixel_bounds == 32)
        {
            visible_mask.transform = null;
        }
        else
        {
            float offset = (pixel_bounds - 32) * 0.5f;
            var transform = new matrix();
            transform.Translate(-offset, -offset);
            visible_mask.transform = transform;
        }

        show_to_holder();
        if (Dm.truthy(directional))
        {
            if (Dm.truthy(beam))
                cast_range = Math.Max(round(new_range * 0.5f), 1);
            else
                cast_range = clamp(round(new_range * 0.5f), 1, 3);
        }
        if (Dm.truthy(Dm.bitand(overlay_lighting_flags, LIGHTING_ON)))
            make_luminosity_update(current_holder);
    }

    /// Changes the intensity/brightness of the light by altering the visual object's alpha.
    // tg: code/datums/components/overlay_lighting.dm:425
    [SignalHandler]
    public virtual void set_power(atom source = null, object old_power = null)
    {
        float new_power = source.light_power;
        lum_power = new_power >= 0 ? 0.5f : -0.5f;
        set_alpha = Math.Min(230, (Math.Abs(new_power) * 120) + 30);
        hide_from_holder();
        visible_mask.alpha = set_alpha;
        visible_mask.blend_mode = new_power > 0 ? BLEND_ADD : BLEND_SUBTRACT;
        if (Dm.truthy(directional))
        {
            cone.alpha = Math.Min(120, (Math.Abs(new_power) * 60) + 15);
            cone.blend_mode = new_power > 0 ? BLEND_ADD : BLEND_SUBTRACT;
        }
        show_to_holder();
    }

    /// Changes the light's color, pretty straightforward.
    // tg: code/datums/components/overlay_lighting.dm:439
    [SignalHandler]
    public virtual void set_color(atom source = null, object old_color = null)
    {
        string new_color = source.light_color;
        hide_from_holder();
        visible_mask.color = new_color;
        if (Dm.truthy(directional))
            cone.color = new_color;
        show_to_holder();
    }

    /// Toggles the light on and off.
    // tg: code/datums/components/overlay_lighting.dm:449
    [SignalHandler]
    public virtual void on_toggle(atom source = null, object old_value = null)
    {
        float new_value = source.light_on;
        if (Dm.truthy(new_value)) //Truthy value input, turn on.
        {
            turn_on();
            return;
        }
        turn_off(); //Falsey value, turn off.
    }

    /// Triggered right after the parent light flags change.
    // tg: code/datums/components/overlay_lighting.dm:458
    [SignalHandler]
    public virtual void on_light_flags_change(atom source = null, object old_flags = null)
    {
        float new_flags = source.light_flags;
        var movable_parent = (atom_movable)parent;
        if (!Dm.truthy(Dm.bitand(Dm.bitxor(new_flags, old_flags), LIGHT_ATTACHED)))
            return;

        if (Dm.truthy(Dm.bitand(new_flags, LIGHT_ATTACHED))) // Gained the [LIGHT_ATTACHED] property
        {
            overlay_lighting_flags = Dm.num(Dm.bitor(overlay_lighting_flags, LIGHTING_ATTACHED));
            if (ismovable(movable_parent.loc))
                set_parent_attached_to((atom_movable)movable_parent.loc);
        }
        else // Lost the [LIGHT_ATTACHED] property
        {
            overlay_lighting_flags = Dm.num(Dm.bitand(overlay_lighting_flags, ~LIGHTING_ATTACHED));
            set_parent_attached_to(null);
        }
    }

    /// Changes the light's color, pretty straightforward.
    // tg: code/datums/components/overlay_lighting.dm:474
    [SignalHandler]
    public virtual void set_light_render_source(atom source = null, object old_render_source = null)
    {
        string new_source = source.light_render_source;
        hide_from_holder();
        visible_mask.render_source = new_source;
        if (Dm.truthy(directional))
        {
            string new_cone_source = "";
            if (Dm.truthy(new_source))
                new_cone_source = Dm.text("[]_cone", new_source);
            cone.render_source = new_cone_source;
        }
        show_to_holder();
    }

    /// Toggles the light on.
    // tg: code/datums/components/overlay_lighting.dm:487
    public virtual void turn_on()
    {
        if (Dm.truthy(Dm.bitand(overlay_lighting_flags, LIGHTING_ON)))
            return;
        overlay_lighting_flags = Dm.num(Dm.bitor(overlay_lighting_flags, LIGHTING_ON));
        if (current_holder != null)
        {
            add_dynamic_lumi();
            if (Dm.truthy(directional))
                cast_directional_light();
        }
        if (current_holder != null && current_holder != parent && current_holder != parent_attached_to)
            RegisterSignal(current_holder, COMSIG_MOVABLE_MOVED, nameof(on_holder_moved));
        register_new_cells();
    }

    /// Toggles the light off.
    // tg: code/datums/components/overlay_lighting.dm:500
    public virtual void turn_off()
    {
        if (!Dm.truthy(Dm.bitand(overlay_lighting_flags, LIGHTING_ON)))
            return;
        if (current_holder != null)
            remove_dynamic_lumi();
        overlay_lighting_flags = Dm.num(Dm.bitand(overlay_lighting_flags, ~LIGHTING_ON));
        if (current_holder != null && current_holder != parent && current_holder != parent_attached_to)
            UnregisterSignal(current_holder, COMSIG_MOVABLE_MOVED);
        clean_old_cells(current_holder);
    }

    /// Here we append the behavior associated to changing lum_power.
    // tg: code/datums/components/overlay_lighting.dm:511
    public virtual void cast_directional_light()
    {
        float final_distance = cast_range;
        //Lower the distance by 1 if we're not looking at a cardinal direction, and we're not a short cast
        if (final_distance > SHORT_CAST && !Dm.truthy(Dm.bitand(ALL_CARDINALS, current_direction)))
            final_distance -= 1;
        turf scanning = get_turf(current_holder);
        for (int i = 1; i <= final_distance; i++)
        {
            turf next_turf = get_step(scanning, Dm.num(current_direction));
            if (next_turf == null || next_turf.directional_opacity == ALL_CARDINALS) // IS_OPAQUE_TURF, inline
            {
                final_distance = i;
                break;
            }
            scanning = next_turf;
        }

        hide_from_holder();

        float translate_x = -((range.Value - 1) * 32);
        float translate_y = translate_x;
        float scale_x = 1;
        float scale_y = 1;
        switch (Dm.num(current_direction))
        {
            case NORTH:
                translate_y += 32 * final_distance;
                if (Dm.truthy(beam) && range > 1)
                    scale_x = 1 / (range.Value - (range.Value / 5));
                break;
            case SOUTH:
                translate_y += -32 * final_distance;
                if (Dm.truthy(beam) && range > 1)
                    scale_x = 1 / (range.Value - (range.Value / 5));
                break;
            case EAST:
                translate_x += 32 * final_distance;
                if (Dm.truthy(beam) && range > 1)
                    scale_y = 1 / (range.Value - (range.Value / 5));
                break;
            case WEST:
                translate_x += -32 * final_distance;
                if (Dm.truthy(beam) && range > 1)
                    scale_y = 1 / (range.Value - (range.Value / 5));
                break;
        }

        if (!Dm.eq(directional_offset_x, translate_x) || !Dm.eq(directional_offset_y, translate_y))
        {
            directional_offset_x = translate_x;
            directional_offset_y = translate_y;
            var transform = new matrix();
            if (Dm.truthy(beam) && range > 1)
                transform.Scale(scale_x, scale_y);
            transform.Translate(translate_x, translate_y);
            visible_mask.transform = transform;
        }

        show_to_holder();
    }

    /// Called when current_holder changes loc.
    // tg: code/datums/components/overlay_lighting.dm:560
    [SignalHandler]
    public virtual void on_holder_dir_change(atom_movable source = null, object olddir = null, object newdir = null)
    {
        set_direction(newdir);
    }

    /// Called when parent changes loc.
    // tg: code/datums/components/overlay_lighting.dm:565
    [SignalHandler]
    public virtual void on_parent_dir_change(atom_movable source = null, object olddir = null, object newdir = null)
    {
        set_direction(newdir);
    }

    /// Sets a new direction for the directional cast, then updates luminosity
    // tg: code/datums/components/overlay_lighting.dm:570
    public virtual void set_direction(object newdir = null)
    {
        if (!Dm.truthy(newdir))
            return;
        if (Dm.eq(current_direction, newdir))
            return;
        current_direction = newdir;
        if (Dm.truthy(Dm.bitand(overlay_lighting_flags, LIGHTING_ON)))
            make_luminosity_update(current_holder);
    }

    // tg: code/datums/components/overlay_lighting.dm:579
    [SignalHandler]
    public virtual void on_parent_crafted(datum source = null, object new_craft = null)
    {
        if (new_craft is not atom_movable crafted)
            return;

        UnregisterSignal(parent, COMSIG_ATOM_USED_IN_CRAFT);
        RegisterSignal(crafted, COMSIG_ATOM_USED_IN_CRAFT, nameof(on_parent_crafted));
        set_parent_attached_to(crafted);
    }

    /// Handles putting the source for overlay lights into the light eater queue since we aren't tracked by [/atom/var/light_sources]
    // tg: code/datums/components/overlay_lighting.dm:590
    [SignalHandler]
    public virtual object on_light_eater(datum source = null, DmList light_queue = null, datum light_eater = null)
    {
        light_queue[parent] = TRUE;
        return NONE;
    }

    // The presentation call that stands where tg changes the holder's underlays (DV61): the holder's light changed. Not a tg proc.
    private void holder_light_changed()
    {
        if (current_holder == null || !PresentationSinks.Reportable(current_holder))
            return;
        DmWorldState.Current.Sinks.Appearance.LightChanged(current_holder);
    }
}

public sealed partial class TgState
{
    ///Cache of the possible light overlays, according to size.
    // tg: code/datums/components/overlay_lighting.dm:39
    internal DmList datum_component_overlay_lighting__light_overlays = Dm.list(
        Dm.kv("32", Dm.resource("icons/effects/light_overlays/light_32.dmi")),
        Dm.kv("64", Dm.resource("icons/effects/light_overlays/light_64.dmi")),
        Dm.kv("96", Dm.resource("icons/effects/light_overlays/light_96.dmi")),
        Dm.kv("128", Dm.resource("icons/effects/light_overlays/light_128.dmi")),
        Dm.kv("160", Dm.resource("icons/effects/light_overlays/light_160.dmi")),
        Dm.kv("192", Dm.resource("icons/effects/light_overlays/light_192.dmi")),
        Dm.kv("224", Dm.resource("icons/effects/light_overlays/light_224.dmi")),
        Dm.kv("256", Dm.resource("icons/effects/light_overlays/light_256.dmi")),
        Dm.kv("288", Dm.resource("icons/effects/light_overlays/light_288.dmi")),
        Dm.kv("320", Dm.resource("icons/effects/light_overlays/light_320.dmi")),
        Dm.kv("352", Dm.resource("icons/effects/light_overlays/light_352.dmi")));
}
