// Translated from tgstation (snapshot 1d99bd7f): code/datums/beam.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: /datum/beam, /datum/beam/varied, /obj/effect/ebeam and /atom/proc/Beam translated (the M7 zaps slice: a tesla zap's
// lightning, energy_ball.dm:325). /obj/effect/ebeam/reacting (beams that react to what enters them: the infrared tripwire,
// heretic and stun beams) come with their users and stay inert; /datum/beam/held (a fishing line, the vacuum hose) came with the
// janitor cyborg's vacuum (silicons SI2). tg's
// BYOND vectors in Draw are written as pairs of numbers, <name>__x and <name>__y (the shim has no /vector; every vector here is a
// 2D sum, difference, scale or size). The var blocks are generated (D111).

/** # Beam Datum and Effect
 *
 * **IF YOU ARE LAZY AND DO NOT WANT TO READ, GO TO THE BOTTOM OF THE FILE AND USE THAT PROC!**
 *
 * This is the beam datum! It's a really neat effect for the game in general so that people don't have to rely on mutable appearances and such.
 * It handles creating, deleting, and updating the beam, and keeps track of it for you.
 */
// tg: code/datums/beam.dm:14
public partial class datum_beam : datum
{
    // tg: code/datums/beam.dm:80
    [DmParams("origin", "target", "icon", "icon_state", "time", "max_distance", "beam_type", "beam_color", "emissive", "emissive_alpha", "animate", "override_origin_pixel_x", "override_origin_pixel_y", "override_target_pixel_x", "override_target_pixel_y", "beam_layer", "alpha")]
    public override async DmTask New(params object[] __extra)
    {
        atom origin = Dm.arg<atom>(__extra, 0);
        atom target = Dm.arg<atom>(__extra, 1);
        object icon = Dm.arg(__extra, 2) ?? Dm.resource("icons/effects/beam.dmi");
        object icon_state = Dm.arg(__extra, 3) ?? "b_beam";
        object time = Dm.arg(__extra, 4) ?? INFINITY;
        object max_distance = Dm.arg(__extra, 5) ?? INFINITY;
        object beam_type = Dm.arg(__extra, 6) ?? typeof(obj_effect_ebeam);
        object beam_color = Dm.arg(__extra, 7);
        object emissive = Dm.arg(__extra, 8) ?? TRUE;
        object emissive_alpha = Dm.arg(__extra, 9) ?? 255;
        object animate = Dm.arg(__extra, 10) ?? TRUE;
        object override_origin_pixel_x = Dm.arg(__extra, 11);
        object override_origin_pixel_y = Dm.arg(__extra, 12);
        object override_target_pixel_x = Dm.arg(__extra, 13);
        object override_target_pixel_y = Dm.arg(__extra, 14);
        object beam_layer = Dm.arg(__extra, 15) ?? ABOVE_ALL_MOB_LAYER;
        object alpha = Dm.arg(__extra, 16) ?? 255;
        this.origin = origin;
        this.target = target;
        this.icon = icon;
        this.icon_state = icon_state as string;
        this.alpha = Dm.num(alpha);
        this.max_distance = Dm.num(max_distance);
        this.beam_type = beam_type as Type;
        this.beam_color = beam_color;
        this.emissive = Dm.num(emissive);
        this.emissive_alpha = Dm.num(emissive_alpha);
        this.animate = Dm.num(animate);
        this.override_origin_pixel_x = override_origin_pixel_x;
        this.override_origin_pixel_y = override_origin_pixel_y;
        this.override_target_pixel_x = override_target_pixel_x;
        this.override_target_pixel_y = override_target_pixel_y;
        this.beam_layer = beam_layer;
        if (Dm.lt(time, INFINITY))
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), Dm.gt(time, GC_FILTER_QUEUE) ? (object)WEAKREF(this) : this), Dm.num(time), file: "code/datums/beam.dm", line: 116); // QDEL_IN(src, time), inline (rules 11.1 (f))
        await DmTask.CompletedTask;
    }

    /**
     * Proc called by the atom Beam() proc. Sets up signals, and draws the beam for the first time.
     */
    // tg: code/datums/beam.dm:121
    public virtual async DmTask Start()
    {
        visuals = Dm.@new(beam_type) as obj_effect_ebeam;
        set_up_effect(visuals, icon_state);
        await Draw();
        RegisterSignals(origin, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_QDELETING), nameof(redrawing));
        RegisterSignals(target, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_QDELETING), nameof(redrawing));
    }

    /**
     * Triggered by signals set up when the beam is set up. If it's still sane to create a beam, it removes the old beam, creates a new one. Otherwise it kills the beam.
     *
     * Arguments:
     * mover: either the origin of the beam or the target of the beam that moved.
     * oldloc: from where mover moved.
     * direction: in what direction mover moved from.
     */
    // tg: code/datums/beam.dm:136
    [SignalHandler]
    public virtual void redrawing(atom_movable mover = null, atom oldloc = null, object direction = null)
    {
        if (QDELING(this))
            return;
        if (QDELETED(origin) || QDELETED(target) || get_dist(origin, target) >= max_distance || origin.z != target.z)
        {
            qdel(this);
            return;
        }
        float queued_time = 0;
        if (Dm.truthy(animate) && mover is atom_movable)
            queued_time = ICON_SIZE_ALL / max(mover.glide_size, MIN_GLIDE_SIZE) * world.tick_lag;
        if (queued_time > pending_animate_time)
            pending_animate_time = queued_time;
        // Synchronous: deferring via INVOKE_ASYNC would start animate() one render frame after the mob's
        // BYOND-managed glide, making the beam trail. Draw() doesn't sleep, so calling it here is safe.
        Waiting.NoSleep(Draw());
    }

    /** Returns the last drawn endpoints for reuse by inherit_glide(), or null if undrawn. */
    // tg: code/datums/beam.dm:153
    public virtual object get_last_geometry()
    {
        if (!Dm.truthy(last_draw_valid))
            return null;
        return Dm.list(
            Dm.kv("origin_x", last_origin_x),
            Dm.kv("origin_y", last_origin_y),
            Dm.kv("origin_px", last_origin_px),
            Dm.kv("origin_py", last_origin_py),
            Dm.kv("target_x", last_target_x),
            Dm.kv("target_y", last_target_y),
            Dm.kv("target_px", last_target_px),
            Dm.kv("target_py", last_target_py)
        );
    }

    /** Seeds the next Draw() from saved geometry so rebuilt beams glide instead of snapping. */
    // tg: code/datums/beam.dm:168
    public virtual void inherit_glide(DmList geometry = null, object animate_time = null)
    {
        if (!Dm.truthy(geometry) || Dm.num(animate_time) <= 0)
            return;
        last_origin_x = Dm.num(geometry["origin_x"]);
        last_origin_y = Dm.num(geometry["origin_y"]);
        last_origin_px = Dm.num(geometry["origin_px"]);
        last_origin_py = Dm.num(geometry["origin_py"]);
        last_target_x = Dm.num(geometry["target_x"]);
        last_target_y = Dm.num(geometry["target_y"]);
        last_target_px = Dm.num(geometry["target_px"]);
        last_target_py = Dm.num(geometry["target_py"]);
        // Mirror into the anim "from" frame; with anim_duration 0 the next Draw() treats progress as 1 and
        // seeds segments exactly at these endpoints, then animates to the (new) live position.
        anim_from_origin_x = last_origin_x;
        anim_from_origin_y = last_origin_y;
        anim_from_origin_px = last_origin_px;
        anim_from_origin_py = last_origin_py;
        anim_from_target_x = last_target_x;
        anim_from_target_y = last_target_y;
        anim_from_target_px = last_target_px;
        anim_from_target_py = last_target_py;
        anim_duration = 0;
        anim_start_time = world.time;
        last_draw_valid = TRUE;
        pending_animate_time = Dm.num(animate_time);
    }

    // tg: code/datums/beam.dm:194
    public override object Destroy(float? force = null)
    {
        QDEL_LIST(elements);
        QDEL_NULL(ref visuals);
        UnregisterSignal(origin, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_QDELETING));
        UnregisterSignal(target, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_QDELETING));
        target = null;
        origin = null;
        return base.Destroy(force);
    }

    /**
     * Creates the beam effects and places them in a line from the origin to the target. Sets their rotation to make the beams face the target, too.
     */
    // tg: code/datums/beam.dm:206
    public virtual async DmTask Draw(atom_movable mover = null, atom oldloc = null)
    {
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_BEAM_BEFORE_DRAW]) ? (object)NONE : this._SendSignal(COMSIG_BEAM_BEFORE_DRAW, Dm.list(this))), BEAM_CANCEL_DRAW)))
            return;
        float animate_time = pending_animate_time;
        pending_animate_time = 0;
        if (!Dm.truthy(animate))
            animate_time = 0;
        float origin_px = (override_origin_pixel_x == null ? origin.pixel_x : Dm.num(override_origin_pixel_x)) + origin.pixel_w;
        float origin_py = (override_origin_pixel_y == null ? origin.pixel_y : Dm.num(override_origin_pixel_y)) + origin.pixel_z;
        float target_px = (override_target_pixel_x == null ? target.pixel_x : Dm.num(override_target_pixel_x)) + target.pixel_w;
        float target_py = (override_target_pixel_y == null ? target.pixel_y : Dm.num(override_target_pixel_y)) + target.pixel_z;

        // Seed from where segments visually are *now*, not where the last Draw asked them to end up.
        // If the previous animation is still in flight (e.g. consecutive-tick or mid-diagonal moves),
        // using the cached destination teleports segments forward then animates back — the diagonal jump.
        // Lerp last from→to by elapsed time to get the real current frame.
        float progress = 1;
        if (Dm.truthy(last_draw_valid) && anim_duration > 0)
            progress = clamp((world.time - anim_start_time) / anim_duration, 0, 1);
        float old_origin_x_f = Dm.truthy(last_draw_valid) ? (anim_from_origin_x + (last_origin_x - anim_from_origin_x) * progress) : origin.x;
        float old_origin_y_f = Dm.truthy(last_draw_valid) ? (anim_from_origin_y + (last_origin_y - anim_from_origin_y) * progress) : origin.y;
        float old_origin_px_f = Dm.truthy(last_draw_valid) ? (anim_from_origin_px + (last_origin_px - anim_from_origin_px) * progress) : origin_px;
        float old_origin_py_f = Dm.truthy(last_draw_valid) ? (anim_from_origin_py + (last_origin_py - anim_from_origin_py) * progress) : origin_py;
        float old_target_x_f = Dm.truthy(last_draw_valid) ? (anim_from_target_x + (last_target_x - anim_from_target_x) * progress) : target.x;
        float old_target_y_f = Dm.truthy(last_draw_valid) ? (anim_from_target_y + (last_target_y - anim_from_target_y) * progress) : target.y;
        float old_target_px_f = Dm.truthy(last_draw_valid) ? (anim_from_target_px + (last_target_px - anim_from_target_px) * progress) : target_px;
        float old_target_py_f = Dm.truthy(last_draw_valid) ? (anim_from_target_py + (last_target_py - anim_from_target_py) * progress) : target_py;
        if (!Dm.truthy(last_draw_valid))
            animate_time = 0;

        // Endpoints in absolute world-pixel coordinates.
        float origin_world__x = origin.x * ICON_SIZE_X + origin_px, origin_world__y = origin.y * ICON_SIZE_Y + origin_py;
        float target_world__x = target.x * ICON_SIZE_X + target_px, target_world__y = target.y * ICON_SIZE_Y + target_py;
        float old_origin_world__x = old_origin_x_f * ICON_SIZE_X + old_origin_px_f, old_origin_world__y = old_origin_y_f * ICON_SIZE_Y + old_origin_py_f;
        float old_target_world__x = old_target_x_f * ICON_SIZE_X + old_target_px_f, old_target_world__y = old_target_y_f * ICON_SIZE_Y + old_target_py_f;

        float Angle = Dm.num(get_angle_raw(origin.x, origin.y, origin_px, origin_py, target.x, target.y, target_px, target_py));
        float beam_direction__x = sin(Angle), beam_direction__y = cos(Angle);
        // Old angle from the interpolated endpoints.
        float old_beam_delta__x = old_target_world__x - old_origin_world__x, old_beam_delta__y = old_target_world__y - old_origin_world__y;
        float OLD_DX_F = old_beam_delta__x;
        float OLD_DY_F = old_beam_delta__y;
        float old_angle;
        if (!Dm.truthy(OLD_DY_F))
            old_angle = (OLD_DX_F >= 0) ? 90 : 270;
        else
        {
            old_angle = arctan(OLD_DX_F / OLD_DY_F);
            if (OLD_DY_F < 0)
                old_angle += 180;
            else if (OLD_DX_F < 0)
                old_angle += 360;
        }
        matrix rot_matrix = Dm.matrix();
        matrix old_rot_matrix = Dm.matrix();
        turf origin_turf = get_turf(origin);
        rot_matrix.Turn(Angle);
        old_rot_matrix.Turn(old_angle);
        float raw_angle_delta = abs(Angle - old_angle);
        if (raw_angle_delta > 180) // Normalize to shortest-path angle across the 0/360 seam.
            raw_angle_delta = 360 - raw_angle_delta;
        // Byond doesn't handle 180 degree rotations well
        bool animate_rotation = Dm.truthy(animate_time) && raw_angle_delta < 90;

        float beam_delta__x = target_world__x - origin_world__x, beam_delta__y = target_world__y - origin_world__y;
        float DX = beam_delta__x;
        float DY = beam_delta__y;
        float N = 0;
        float length = round(sqrt(beam_delta__x * beam_delta__x + beam_delta__y * beam_delta__y)); // round(beam_delta.size)
        float old_length = round(sqrt(old_beam_delta__x * old_beam_delta__x + old_beam_delta__y * old_beam_delta__y)); // round(old_beam_delta.size)
        float old_beam_direction__x = sin(old_angle), old_beam_direction__y = cos(old_angle);

        DmList old_elements = elements;
        DmList new_elements = Dm.list();

        for (float N__it = 0, N__end = length - 1; N__it <= N__end; N__it += 32)
        {
            N = N__it;
            if (QDELETED(this))
                break;
            // Map each new segment to the same offset on the interpolated old beam.
            float old_pos = clamp(N + 16, 0, old_length);
            obj_effect_ebeam segment = Dm.@new(beam_type, origin_turf, this) as obj_effect_ebeam;
            Dm.append(ref new_elements, segment);

            icon terminal_icon = null;
            if (N + 32 > length)
            {
                terminal_icon = Dm.icon(icon, icon_state);
                float cut_row = length - N;
                terminal_icon.DrawBox(null, 1, cut_row, 32, 32);
                // Soft alpha falloff so the tip isn't a hard line.
                float fade_height = min(4, cut_row - 1);
                if (fade_height > 0)
                {
                    icon alpha_mask = Dm.icon(icon, icon_state);
                    alpha_mask.DrawBox(rgb(255, 255, 255, 255), 1, 1, 32, 32);
                    float band_start = cut_row - fade_height;
                    for (float y = band_start, y__end = cut_row - 1; y <= y__end; y++)
                    {
                        float from_tip = (cut_row - 1) - y; // 0 at the tip row, fade_height-1 furthest back
                        float a = round(255 * (from_tip + 1) / (fade_height + 1), 1);
                        alpha_mask.DrawBox(rgb(255, 255, 255, a), 1, y, 32, y);
                    }
                    alpha_mask.DrawBox(null, 1, cut_row, 32, 32);
                    terminal_icon.Blend(alpha_mask, ICON_MULTIPLY);
                }
                segment.icon = terminal_icon;
                segment.color = beam_color;
            }
            else
                set_subsegment_appearance(segment);
            if (animate_rotation)
                segment.transform = old_rot_matrix;
            else
                segment.transform = rot_matrix;

            float Pixel_x = (DX == 0) ? 0 : round(sin(Angle) + 32 * sin(Angle) * (N + 16) / 32, 1);
            float Pixel_y = (DY == 0) ? 0 : round(cos(Angle) + 32 * cos(Angle) * (N + 16) / 32, 1);

            float final_x = segment.x;
            float final_y = segment.y;
            if (abs(Pixel_x) > 32)
            {
                final_x += Pixel_x > 0 ? round(Pixel_x / 32) : ceil(Pixel_x / 32);
                Pixel_x = Dm.mod(Pixel_x, 32);
            }
            if (abs(Pixel_y) > 32)
            {
                final_y += Pixel_y > 0 ? round(Pixel_y / 32) : ceil(Pixel_y / 32);
                Pixel_y = Dm.mod(Pixel_y, 32);
            }
            await segment.forceMove(locate(final_x, final_y, segment.z));
            segment.alpha = Dm.num(alpha);
            segment.layer = Dm.num(beam_layer);
            float new_pixel_x = origin_px + Pixel_x;
            float new_pixel_y = origin_py + Pixel_y;
            if (Dm.truthy(animate_time))
            {
                // Seed from interpolated old endpoints so consecutive redraws don't snap.
                float old_visual__x = old_origin_world__x + old_beam_direction__x * old_pos, old_visual__y = old_origin_world__y + old_beam_direction__y * old_pos;
                float new_visual_x = final_x * ICON_SIZE_X + new_pixel_x;
                float new_visual_y = final_y * ICON_SIZE_Y + new_pixel_y;
                segment.pixel_x = new_pixel_x + round(old_visual__x - new_visual_x, 1);
                segment.pixel_y = new_pixel_y + round(old_visual__y - new_visual_y, 1);
                // Segments past the old beam's end fade in instead of popping.
                if (N >= old_length)
                {
                    segment.alpha = 0;
                    Dm.animate(segment, Dm.named("alpha", alpha), Dm.named("time", animate_time), Dm.named("flags", ANIMATION_PARALLEL));
                }
                if (animate_rotation)
                    Dm.animate(segment, Dm.named("pixel_x", new_pixel_x), Dm.named("pixel_y", new_pixel_y), Dm.named("transform", rot_matrix), Dm.named("time", animate_time), Dm.named("flags", ANIMATION_PARALLEL));
                else
                    Dm.animate(segment, Dm.named("pixel_x", new_pixel_x), Dm.named("pixel_y", new_pixel_y), Dm.named("time", animate_time), Dm.named("flags", ANIMATION_PARALLEL));
            }
            else
            {
                segment.pixel_x = new_pixel_x;
                segment.pixel_y = new_pixel_y;
            }
            // if(emissive) segment.add_overlay(emissive_appearance(terminal_icon ? terminal_icon : icon, terminal_icon ? "" : icon_state,
            // segment, alpha = segment.alpha * emissive_alpha / 255, effect_type = emissive)): DEVIATION(DV12), the glow on the emissive plane
        }

        elements = new_elements;
        // Fade out extra segments before deleting them so shrinking the beam does not pop the tail.
        float old_count = Dm.length(old_elements);
        float new_count = Dm.length(new_elements);
        if (Dm.truthy(animate_time) && old_count > new_count && progress >= 1)
        {
            for (float i = 1, i__end = new_count; i <= i__end; i++)
                qdel(old_elements[i]);
            for (float i = new_count + 1, i__end = old_count; i <= i__end; i++)
            {
                obj_effect_ebeam dying = old_elements[i] as obj_effect_ebeam;
                // Project the dying segment onto the new beam and clamp it to the tip.
                float proj_pos = clamp((i - 1) * ICON_SIZE_ALL + 16, 0, length);
                float proj_world__x = origin_world__x + beam_direction__x * proj_pos, proj_world__y = origin_world__y + beam_direction__y * proj_pos;
                float dying_world_x = dying.x * ICON_SIZE_X;
                float dying_world_y = dying.y * ICON_SIZE_Y;
                float target_px_anim = round(proj_world__x - dying_world_x, 1);
                float target_py_anim = round(proj_world__y - dying_world_y, 1);
                dying.cut_overlays(); // Remove emissive overlay so it doesn't glow while the segment fades out.
                if (animate_rotation)
                    Dm.animate(dying, Dm.named("pixel_x", target_px_anim), Dm.named("pixel_y", target_py_anim), Dm.named("transform", rot_matrix), Dm.named("alpha", 0), Dm.named("time", animate_time), Dm.named("flags", ANIMATION_PARALLEL));
                else
                    Dm.animate(dying, Dm.named("pixel_x", target_px_anim), Dm.named("pixel_y", target_py_anim), Dm.named("alpha", 0), Dm.named("time", animate_time), Dm.named("flags", ANIMATION_PARALLEL));
                _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), animate_time > GC_FILTER_QUEUE ? (object)WEAKREF(dying) : dying), animate_time, file: "code/datums/beam.dm", line: 371); // QDEL_IN(dying, animate_time), inline (rules 11.1 (f))
            }
        }
        else
            QDEL_LIST(old_elements);

        // Cache this draw's seed and destination so the next Draw() can lerp mid-animation.
        anim_from_origin_x = old_origin_x_f;
        anim_from_origin_y = old_origin_y_f;
        anim_from_origin_px = old_origin_px_f;
        anim_from_origin_py = old_origin_py_f;
        anim_from_target_x = old_target_x_f;
        anim_from_target_y = old_target_y_f;
        anim_from_target_px = old_target_px_f;
        anim_from_target_py = old_target_py_f;
        last_origin_x = origin.x;
        last_origin_y = origin.y;
        last_origin_px = origin_px;
        last_origin_py = origin_py;
        last_target_x = target.x;
        last_target_y = target.y;
        last_target_px = target_px;
        last_target_py = target_py;
        anim_start_time = world.time;
        anim_duration = animate_time;
        last_draw_valid = TRUE;
    }

    // tg: code/datums/beam.dm:396
    public virtual void set_up_effect(obj_effect_ebeam beam_effect = null, object effect_icon_state = null)
    {
        beam_effect.icon = icon;
        beam_effect.icon_state = effect_icon_state as string;
        beam_effect.color = beam_color;
        beam_effect.vis_flags = Dm.bitor(VIS_INHERIT_PLANE, VIS_INHERIT_LAYER);
        beam_effect.emissive = emissive;
        beam_effect.layer = Dm.num(beam_layer);
        beam_effect.update_appearance();
    }

    ///sets the sprite of the segment, using the more performant viscontents by default
    // tg: code/datums/beam.dm:406
    public virtual void set_subsegment_appearance(obj_effect_ebeam segment = null)
    {
        //Assign our single visual ebeam to each ebeam's vis_contents
        // segment.vis_contents += visuals: DEVIATION(DV77), the segment shows the visual's appearance as its own (the replicator draws
        // an entity's vis_contents only for tg's flick visuals), the picture tg's vis_contents gives it
        set_up_effect(segment, icon_state);
    }
}

//for when you don't want each segment to look identital
// tg: code/datums/beam.dm:413
public partial class datum_beam_varied : datum_beam
{
    // tg: code/datums/beam.dm:417
    // DEVIATION(DV77): tg's list lacks emissive_alpha, which Beam passes, so tg's icon_state_variants takes Beam's alpha (255) and most
    // segments name a state beam.dmi lacks ("tesla137": the tesla cannon's lightning shown in pieces); the list takes it, as datum/beam's does
    [DmParams("origin", "target", "icon", "icon_state", "time", "max_distance", "beam_type", "beam_color", "emissive", "emissive_alpha", "animate", "override_origin_pixel_x", "override_origin_pixel_y", "override_target_pixel_x", "override_target_pixel_y", "beam_layer", "alpha", "icon_state_variants")]
    public override async DmTask New(params object[] __extra)
    {
        object icon_state_variants = Dm.arg(__extra, 17) ?? 1;
        await base.New(__extra);

        this.icon_state_variants = Dm.num(icon_state_variants);
    }

    // tg: code/datums/beam.dm:439
    public override void set_subsegment_appearance(obj_effect_ebeam segment = null)
    {
        //we use reall ass icon states here.
        set_up_effect(segment, Dm.text("[][]", icon_state, rand(1, icon_state_variants)));
    }
}

// tg: code/datums/beam.dm:443
public partial class obj_effect_ebeam : obj_effect
{
    // tg: code/datums/beam.dm:450
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_beam beam_owner = Dm.arg<datum_beam>(__extra, 0);
        owner = beam_owner;
        return base.Initialize(mapload, __extra);
    }

    // tg: code/datums/beam.dm:454
    public override object Destroy(float? force = null)
    {
        owner = null;
        return base.Destroy(force);
    }

    // tg: code/datums/beam.dm:458
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        return null;
    }

    // tg: code/datums/beam.dm:461
    public override async DmTask<object> singularity_act()
    {
        return null;
    }

    // tg: code/datums/beam.dm:464
    public override async DmTask<object> Process_Spacemove(float? movement_dir = null, float? continuous_move = null)
    {
        return TRUE;
    }
}

public partial class atom : datum
{
    /**
     * This is what you use to start a beam. Example: origin.Beam(target, args). **Store the return of this proc if you don't set maxdist or time, you need it to delete the beam.**
     *
     * Unless you're making a custom beam effect (see the beam_type argument), you won't actually have to mess with any other procs. Make sure you store the return of this Proc, you'll need it
     * to kill the beam.
     * **Arguments:**
     * BeamTarget: Where you're beaming from. Where do you get origin? You didn't read the docs, fuck you.
     * icon_state: What the beam's icon_state is. The datum effect isn't the ebeam object, it doesn't hold any icon and isn't type dependent.
     * icon: What the beam's icon file is. Don't change this, man. All beam icons should be in beam.dmi anyways.
     * maxdistance: how far the beam will go before stopping itself. Used mainly for two things: preventing lag if the beam may go in that direction and setting a range to abilities that use beams.
     * beam_type: The type of your custom beam. This is for adding other wacky stuff for your beam only. Most likely, you won't (and shouldn't) change it.
     */
    // tg: code/datums/beam.dm:600
    public virtual object Beam(atom BeamTarget = null, string icon_state = null, object icon = null, float? time = null, float? maxdistance = null, Type beam_type = null, object beam_color = null, float? emissive = null, float? animate = null, object override_origin_pixel_x = null, object override_origin_pixel_y = null, object override_target_pixel_x = null, object override_target_pixel_y = null, float? layer = null, float? icon_state_variants = null, object glide_seed = null, float? glide_time = null, float? alpha = null, float? emissive_alpha = null)
    {
        icon_state ??= "b_beam";
        icon ??= Dm.resource("icons/effects/beam.dmi");
        time ??= INFINITY;
        maxdistance ??= INFINITY;
        beam_type ??= typeof(obj_effect_ebeam);
        emissive ??= TRUE;
        animate ??= TRUE;
        layer ??= ABOVE_ALL_MOB_LAYER;
        icon_state_variants ??= 0;
        glide_time ??= 0;
        alpha ??= 255;
        emissive_alpha ??= 255;
        datum_beam newbeam;

        if (icon_state_variants <= 0)
            newbeam = Dm.@new<datum_beam>(this, BeamTarget, icon, icon_state, time, maxdistance, beam_type, beam_color, emissive, emissive_alpha, animate, override_origin_pixel_x, override_origin_pixel_y, override_target_pixel_x, override_target_pixel_y, layer, alpha);
        else
            newbeam = Dm.@new<datum_beam_varied>(this, BeamTarget, icon, icon_state, time, maxdistance, beam_type, beam_color, emissive, emissive_alpha, animate, override_origin_pixel_x, override_origin_pixel_y, override_target_pixel_x, override_target_pixel_y, layer, alpha, icon_state_variants);
        // Seed the glide before Start()'s first Draw() runs (INVOKE_ASYNC runs it synchronously here since
        // Draw() never sleeps), so a rebuilt beam animates from its predecessor instead of snapping.
        newbeam.inherit_glide(glide_seed as DmList, glide_time);
        Waiting.Detach(newbeam.Start());
        return newbeam;
    }
}

// tg: code/datums/beam.dm:527 (silicons SI2: the janitor cyborg's vacuum hose)
public partial class datum_beam_held : datum_beam
{
    // tg: code/datums/beam.dm:556
    public override async DmTask Start()
    {
        update_offsets(origin.dir);
        await base.Start();
        RegisterSignal(origin, COMSIG_ATOM_DIR_CHANGE, nameof(handle_dir_change));
    }

    // tg: code/datums/beam.dm:561
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(origin, COMSIG_ATOM_DIR_CHANGE);
        return base.Destroy(force);
    }

    // tg: code/datums/beam.dm:565
    [SignalHandler]
    public virtual void handle_dir_change(atom_movable source = null, object olddir = null, object newdir = null)
    {
        update_offsets(newdir);
        redrawing(); // INVOKE_ASYNC(src, TYPE_PROC_REF(/datum/beam, redrawing)): redrawing cannot sleep
    }

    // tg: code/datums/beam.dm:570
    public virtual void update_offsets(object user_dir = null)
    {
        float user_dir__num = Dm.num(user_dir);
        if (user_dir__num == SOUTH)
        {
            override_origin_pixel_x = Dm.truthy(lefthand) ? lefthand_s_px : righthand_s_px;
            override_origin_pixel_y = Dm.truthy(lefthand) ? lefthand_s_py : righthand_s_py;
        }
        else if (user_dir__num == EAST)
        {
            override_origin_pixel_x = Dm.truthy(lefthand) ? lefthand_e_px : righthand_e_px;
            override_origin_pixel_y = Dm.truthy(lefthand) ? lefthand_e_py : righthand_e_py;
        }
        else if (user_dir__num == WEST)
        {
            override_origin_pixel_x = Dm.truthy(lefthand) ? lefthand_w_px : righthand_w_px;
            override_origin_pixel_y = Dm.truthy(lefthand) ? lefthand_w_py : righthand_w_py;
        }
        else if (user_dir__num == NORTH)
        {
            override_origin_pixel_x = Dm.truthy(lefthand) ? lefthand_n_px : righthand_n_px;
            override_origin_pixel_y = Dm.truthy(lefthand) ? lefthand_n_py : righthand_n_py;
        }

        override_origin_pixel_x = Dm.num(override_origin_pixel_x) + origin.pixel_x;
        override_origin_pixel_y = Dm.num(override_origin_pixel_y) + origin.pixel_y;
    }
}
