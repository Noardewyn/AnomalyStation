// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/living_update_icons.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M2 P3): the lying matrix (update_transform) and the pixel offsets (the -6 px PIXEL_Y_OFFSET_LYING of
// /mob/living/on_lying_down goes through add_offsets).

// tg: code/modules/mob/living/living_update_icons.dm:5
public partial class mob_living : mob
{
    /**
     * Called whenever the mob is to be resized or when lying/standing up for carbons.
     * IMPORTANT: Multiple animate() calls do not stack well, so try to do them all at once if you can.
     */
    // tg: code/modules/mob/living/living_update_icons.dm:5
    public virtual async DmTask<object> update_transform(float? resize = null)
    {
        resize ??= RESIZE_DEFAULT_SIZE;
        matrix ntransform = Dm.matrix(transform);
        object current_translate = get_transform_translation_size(current_size);
        float final_dir = dir;
        float changed = FALSE;

        if (lying_angle != lying_prev && Dm.truthy(rotate_on_lying))
        {
            changed = TRUE;
            if (Dm.truthy(lying_angle) && lying_prev == 0)
            {
                if (Dm.truthy(current_translate))
                    ntransform.Translate(0, -Dm.num(current_translate));
                // Standing to lying and facing east or west
                if (Dm.truthy(Dm.bitand(dir, Dm.bitor(EAST, WEST))))
                    // ...So you fall on your side, rather than your face or ass
                    final_dir = Dm.num(pick(NORTH, SOUTH));
            }
            else
            {
                if (Dm.truthy(current_translate) && !Dm.truthy(lying_angle) && lying_prev != 0)
                    ntransform.Translate(Dm.num(current_translate) * (lying_prev == 270 ? -1 : 1), 0);
            }
            // Done last, as it can mess with the translation.
            ntransform.TurnTo(lying_prev, lying_angle);
        }

        if (Dm.num(resize) != RESIZE_DEFAULT_SIZE)
        {
            changed = TRUE;
            float is_vertical = Dm.num(!Dm.truthy(lying_angle) || !Dm.truthy(rotate_on_lying));
            object new_translation = get_transform_translation_size(Dm.num(resize) * current_size);
            // scaling also affects translation, so we've to undo the old translate beforehand.
            if (Dm.truthy(is_vertical) && Dm.truthy(current_translate))
                ntransform.Translate(0, -Dm.num(current_translate));

            ntransform.Scale(Dm.num(resize));
            current_size *= Dm.num(resize);
            // Update the height of the maptext according to the size of the mob so they don't overlap.
            float old_maptext_offset = body_maptext_height_offset;
            body_maptext_height_offset = Dm.initial(this, (atom p) => p.maptext_height) * (current_size - 1) * 0.5f;
            maptext_height += body_maptext_height_offset - old_maptext_offset;
            // and update the new translation
            if (Dm.truthy(is_vertical) && Dm.truthy(new_translation))
                ntransform.Translate(0, Dm.num(new_translation));
        }

        if (!Dm.truthy(changed)) //Nothing has been changed, nothing has to be done.
            return FALSE;

        // ensures the floating animation doesn't mess with our animation
        if (HAS_TRAIT(this, TRAIT_MOVE_FLOATING))
        {
            ADD_TRAIT(this, TRAIT_NO_FLOATING_ANIM, UPDATE_TRANSFORM_TRAIT);
            _addtimer(TRAIT_CALLBACK_REMOVE(this, TRAIT_NO_FLOATING_ANIM, UPDATE_TRANSFORM_TRAIT), 0.3f * SECONDS, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/modules/mob/living/living_update_icons.dm", line: 50);
        }
        //if true, we want to avoid any animation time, it'll tween and not rotate at all otherwise.
        float is_opposite_angle = Dm.num(SIMPLIFY_DEGREES(lying_angle + 180) == lying_prev); // REVERSE_ANGLE(lying_angle) (__DEFINES/maths.dm:140), inline (rules 11.1)
        float animate_time = Dm.truthy(is_opposite_angle) ? 0 : UPDATE_TRANSFORM_ANIMATION_TIME;
        animate(this, Dm.named("transform", ntransform), Dm.named("time", animate_time), Dm.named("dir", final_dir), Dm.named("easing", SINE_EASING));
        readjust_atom_huds(animate_time);

        if (Dm.truthy(this._listen_lookup?[COMSIG_LIVING_POST_UPDATE_TRANSFORM]))
            this._SendSignal(COMSIG_LIVING_POST_UPDATE_TRANSFORM, Dm.list(this, resize, lying_angle, is_opposite_angle));
        if (unconscious_appearance != null)
            unconscious_appearance.transform = ntransform;
        return TRUE;
    }

    // tg: code/modules/mob/living/living_update_icons.dm:61
    public virtual void readjust_atom_huds(object animate_time = null)
    {
        foreach (object hud_key in Dm.each(hud_list))
        {
            image hud_image = hud_list[hud_key] as image;
            if (hud_image is image)
            {
                adjust_hud_position(hud_image, animate_time: animate_time);
            }
        }
    }

    /// Calculates how far vertically the mob's transform should translate according to its size (1 being "default")
    // tg: code/modules/mob/living/living_update_icons.dm:68
    public virtual object get_transform_translation_size(object value = null)
    {
        return (Dm.num(value) - 1) * 16;
    }

    /**
     * Adds an offset to the mob's pixel position.
     *
     * * source: The source of the offset, a string
     * * w_add: pixel_w offset
     * * x_add: pixel_x offset
     * * y_add: pixel_y offset
     * * z_add: pixel_z offset
     * * animate: If TRUE, the mob will animate to the new position. If FALSE, it will instantly move.
     */
    // tg: code/modules/mob/living/living_update_icons.dm:81
    public virtual void add_offsets(object source = null, object w_add = null, object x_add = null, object y_add = null, object z_add = null, float? animate = null)
    {
        animate ??= TRUE;
        LAZYINITLIST(ref offsets);
        // LAZYSET(offsets[X], source, value) assigns the list element (rules 11.1): the element is bound to a temp, set,
        // and stored back, which is what the macro's `if(!L) L = list(); L[K] = V` does to it.
        if (isnum(w_add))
        {
            DmList t__1 = offsets[PIXEL_W_OFFSET] as DmList;
            LAZYSET(ref t__1, source, w_add);
            offsets[PIXEL_W_OFFSET] = t__1;
        }
        if (isnum(x_add))
        {
            DmList t__2 = offsets[PIXEL_X_OFFSET] as DmList;
            LAZYSET(ref t__2, source, x_add);
            offsets[PIXEL_X_OFFSET] = t__2;
        }
        if (isnum(y_add))
        {
            DmList t__3 = offsets[PIXEL_Y_OFFSET] as DmList;
            LAZYSET(ref t__3, source, y_add);
            offsets[PIXEL_Y_OFFSET] = t__3;
        }
        if (isnum(z_add))
        {
            DmList t__4 = offsets[PIXEL_Z_OFFSET] as DmList;
            LAZYSET(ref t__4, source, z_add);
            offsets[PIXEL_Z_OFFSET] = t__4;
        }
        update_offsets(animate);
    }

    /**
     * Goes through all pixel adjustments and removes any tied to the passed source.
     *
     * * source: The source of the offset to remove
     * * animate: If TRUE, the mob will animate to the position with any offsets removed. If FALSE, it will instantly move.
     */
    // tg: code/modules/mob/living/living_update_icons.dm:99
    public virtual void remove_offsets(object source = null, float? animate = null)
    {
        animate ??= TRUE;
        foreach (object offset in Dm.each(offsets))
        {
            // LAZYREMOVE(offsets[offset], source): the element through a temp, as in add_offsets (rules 11.1)
            DmList t__1 = offsets[offset] as DmList;
            LAZYREMOVE(ref t__1, source);
            offsets[offset] = t__1;
            ASSOC_UNSETEMPTY(ref offsets, offset);
        }
        UNSETEMPTY(ref offsets);
        update_offsets(animate);
    }

    /**
     * Updates the mob's pixel position according to the offsets.
     *
     * * animate: If TRUE, the mob will animate to the new position. If FALSE, it will instantly move.
     *
     * Returns TRUE if the mob's position has changed, FALSE otherwise.
     */
    // tg: code/modules/mob/living/living_update_icons.dm:113
    public virtual object update_offsets(float? animate = null)
    {
        animate ??= FALSE;
        float new_w = base_pixel_w;
        float new_x = base_pixel_x;
        float new_y = base_pixel_y;
        float new_z = base_pixel_z;

        foreach (object offset_key in Dm.each(LAZYACCESS(offsets, PIXEL_W_OFFSET)))
            new_w += Dm.num(((DmList)offsets[PIXEL_W_OFFSET])[offset_key]);
        foreach (object offset_key in Dm.each(LAZYACCESS(offsets, PIXEL_X_OFFSET)))
            new_x += Dm.num(((DmList)offsets[PIXEL_X_OFFSET])[offset_key]);
        foreach (object offset_key in Dm.each(LAZYACCESS(offsets, PIXEL_Y_OFFSET)))
            new_y += Dm.num(((DmList)offsets[PIXEL_Y_OFFSET])[offset_key]);
        foreach (object offset_key in Dm.each(LAZYACCESS(offsets, PIXEL_Z_OFFSET)))
            new_z += Dm.num(((DmList)offsets[PIXEL_Z_OFFSET])[offset_key]);

        if (new_w == pixel_w && new_x == pixel_x && new_y == pixel_y && new_z == pixel_z)
            return FALSE;

        if (Dm.truthy(this._listen_lookup?[COMSIG_LIVING_UPDATE_OFFSETS]))
            this._SendSignal(COMSIG_LIVING_UPDATE_OFFSETS, Dm.list(this, new_x, new_y, new_w, new_z, animate));

        if (!Dm.truthy(animate))
        {
            pixel_w = new_w;
            pixel_x = new_x;
            pixel_y = new_y;
            pixel_z = new_z;
            return TRUE;
        }

        // ensures the floating animation doesn't mess with our animation
        if (HAS_TRAIT(this, TRAIT_MOVE_FLOATING))
        {
            ADD_TRAIT(this, TRAIT_NO_FLOATING_ANIM, UPDATE_OFFSET_TRAIT);
            _addtimer(TRAIT_CALLBACK_REMOVE(this, TRAIT_NO_FLOATING_ANIM, UPDATE_OFFSET_TRAIT), 0.3f * SECONDS, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/modules/mob/living/living_update_icons.dm", line: 143);
        }

        Dm.animate(this,
            Dm.named("pixel_w", new_w),
            Dm.named("pixel_x", new_x),
            Dm.named("pixel_y", new_y),
            Dm.named("pixel_z", new_z),
            Dm.named("flags", ANIMATION_PARALLEL),
            Dm.named("time", UPDATE_TRANSFORM_ANIMATION_TIME));
        return TRUE;
    }

    /**
     * Checks if we are offset by the passed source for the passed pixel.
     *
     * * source: The source of the offset
     * If not supplied, it will report the total offset of the passed pixel.
     * * pixel: Optional, The pixel to check.
     * If not supplied, just reports if it's offset by the source at all (returning the first offset found).
     *
     * Returns the offset if we are, 0 otherwise.
     */
    // tg: code/modules/mob/living/living_update_icons.dm:165
    public virtual object has_offset(object source = null, object pixel = null)
    {
        if (source == null && pixel == null)
        {
            stack_trace("has_offset() requires at least one argument.", "code/modules/mob/living/living_update_icons.dm", 167);
            return 0;
        }

        if (source == null)
        {
            if (!Dm.truthy(length(offsets?[pixel])))
                return 0;

            object total_found_offset = 0;
            foreach (object found_offset in Dm.each(offsets[pixel]))
                Dm.append(ref total_found_offset, has_offset(found_offset, pixel));
            return total_found_offset;
        }

        if (pixel == null)
        {
            foreach (object found_pixel in Dm.each(offsets))
            {
                object found_offset = has_offset(source, found_pixel);
                if (Dm.truthy(found_offset))
                    return found_offset;
            }

            return 0;
        }

        return (((DmList)offsets?[pixel])?[source] is var or__1 && Dm.truthy(or__1) ? or__1 : 0);
    }

    // Updates offsets if base pixel changes
    // Future TODO: move base pixel onto /obj and make mobs just set a base pixel using a source
    // tg: code/modules/mob/living/living_update_icons.dm:191
    public override object set_base_pixel_x(object new_value = null)
    {
        object dot = null;
        dot = base.set_base_pixel_x(new_value);
        update_offsets();
        return dot;
    }

    // tg: code/modules/mob/living/living_update_icons.dm:195
    public override object set_base_pixel_y(object new_value = null)
    {
        object dot = null;
        dot = base.set_base_pixel_y(new_value);
        update_offsets();
        return dot;
    }
}
