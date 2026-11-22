// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/visual_effects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the floating animation macros (DO_FLOATING_ANIM, STOP_FLOATING_ANIM: datums/elements/movetype_handler.dm
// uses them, M2 P1) and UPDATE_TRANSFORM_ANIMATION_TIME (living_update_icons.dm, M2 P3) translated (M2 integration: rules
// 11.1 and 11.4, a function-like macro and a global #define outside code/__DEFINES live in the mirror of their file), and
// SpinAnimation with do_spin_animation (the orbiter component's orbit, M7 D169), spasm_animation (the monkey plushie, syndicate G3c), makeHologram
// (the holopads' holograms, silicons SI4; its emissive glow is DV12's). The other procs of this file (the spasm, shake
// and jitter animations, the hologram, ...) come with their users.

public static partial class GlobalProcs
{
    /**
     * Causes the passed atom / image to appear floating,
     * playing a simple animation where they move up and down by 2 pixels (looping)
     *
     * In most cases you should NOT call this manually, instead use [/datum/element/movetype_handler]!
     * This is just so you can apply the animation to things which can be animated but are not movables (like images)
     */
    // tg: code/__HELPERS/visual_effects.dm:8  #define DO_FLOATING_ANIM(target) (a statement macro, rules 11.1)
    public static void DO_FLOATING_ANIM(object target)
    {
        animate(target, Dm.named("pixel_z", 2), Dm.named("time", 1 * SECONDS), Dm.named("loop", -1), Dm.named("flags", ANIMATION_RELATIVE));
        animate(Dm.named("pixel_z", -2), Dm.named("time", 1 * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
    }

    /**
     * Stops the passed atom / image from appearing floating
     * (Living mobs also have a 'body_position_pixel_y_offset' variable that has to be taken into account here)
     *
     * In most cases you should NOT call this manually, instead use [/datum/element/movetype_handler]!
     * This is just so you can apply the animation to things which can be animated but are not movables (like images)
     */
    // tg: code/__HELPERS/visual_effects.dm:19  #define STOP_FLOATING_ANIM(target) (a statement macro, rules 11.1)
    public static void STOP_FLOATING_ANIM(object target)
    {
        float __final_pixel_z = 0;
        if (ismovable(target))
        {
            atom_movable __movable_target = (atom_movable)target;
            __final_pixel_z += __movable_target.base_pixel_z;
        }
        if (isliving(target))
        {
            mob_living __living_target = (mob_living)target;
            __final_pixel_z += Dm.num(__living_target.has_offset(pixel: PIXEL_Z_OFFSET));
        }
        animate(target, Dm.named("pixel_z", __final_pixel_z), Dm.named("time", 1 * SECONDS));
    }

    /// The duration of the animate call in mob/living/update_transform
    // tg: code/__HELPERS/visual_effects.dm:32  #define UPDATE_TRANSFORM_ANIMATION_TIME (0.2 SECONDS) (a global #define outside
    // code/__DEFINES, which the defines converter does not emit: rules 11.4)
    public const float UPDATE_TRANSFORM_ANIMATION_TIME = (0.2f * SECONDS);
}

public partial class atom : datum
{
    // tg: code/__HELPERS/visual_effects.dm:56
    public virtual void spasm_animation(float? loops = null)
    {
        loops ??= -1;
        DmList transforms = Dm.list(
            Dm.matrix(transform).Translate(-1, 0),
            Dm.matrix(transform).Translate(0, 1),
            Dm.matrix(transform).Translate(1, 0),
            Dm.matrix(transform).Translate(0, -1),
            Dm.matrix(transform));

        animate(this, Dm.named("transform", transforms[1]), Dm.named("time", 0.1f), Dm.named("loop", loops));
        animate(Dm.named("transform", transforms[2]), Dm.named("time", 0.1f));
        animate(Dm.named("transform", transforms[3]), Dm.named("time", 0.2f));
        animate(Dm.named("transform", transforms[4]), Dm.named("time", 0.3f));
        animate(Dm.named("transform", transforms[5]), Dm.named("time", 0.1f));
    }

    ///Animates source spinning around itself. For docmentation on the args, check atom/proc/SpinAnimation()
    // tg: code/__HELPERS/visual_effects.dm:35
    public virtual void do_spin_animation(float? speed = null, float? loops = null, float? segments = null, float? angle = null, float? parallel = null, object tag = null)
    {
        speed ??= 1 * SECONDS;
        loops ??= -1;
        segments ??= 3;
        angle ??= 120;
        parallel ??= TRUE;
        DmList matrices = Dm.list();
        for (float i = 1, i__end = segments.Value - 1; i <= i__end; i++)
        {
            matrix segment_matrix = matrix(transform);
            segment_matrix.Turn(angle.Value * i);
            Dm.append(ref matrices, segment_matrix);
        }
        matrix last = matrix(transform);
        Dm.append(ref matrices, last);

        speed /= segments;

        if (Dm.truthy(parallel))
            animate(this, Dm.named("transform", matrices[1]), Dm.named("time", speed), Dm.named("loop", loops), Dm.named("flags", ANIMATION_PARALLEL), Dm.named("tag", tag));
        else
            animate(this, Dm.named("transform", matrices[1]), Dm.named("time", speed), Dm.named("loop", loops));
        for (float i = 2, i__end = segments.Value; i <= i__end; i++) //2 because 1 is covered above
        {
            animate(Dm.named("transform", matrices[i]), Dm.named("time", speed));
            //doesn't have an object argument because this is "Stacking" with the animate call above
            //3 billion% intentional
        }
    }

    /**
     * Proc called when you want the atom to spin around the center of its icon (or where it would be if its transform var is translated)
     * By default, it makes the atom spin forever and ever at a speed of 60 rpm.
     *
     * Arguments:
     * * speed: how much it takes for the atom to complete one 360° rotation
     * * loops: how many times do we want the atom to rotate
     * * clockwise: whether the atom ought to spin clockwise or counter-clockwise
     * * segments: in how many animate calls the rotation is split. Probably unnecessary, but you shouldn't set it lower than 3 anyway.
     * * parallel: whether the animation calls have the ANIMATION_PARALLEL flag, necessary for it to run alongside concurrent animations.
     * * tag: animation tag to use, for parralel animations only
     */
    // tg: code/__HELPERS/visual_effects.dm:83
    public virtual void SpinAnimation(float? speed = null, float? loops = null, float? clockwise = null, float? segments = null, float? parallel = null, object tag = null)
    {
        speed ??= 1 * SECONDS;
        loops ??= -1;
        clockwise ??= TRUE;
        segments ??= 3;
        parallel ??= TRUE;
        if (!Dm.truthy(segments))
            return;
        float segment = 360 / segments.Value;
        if (!Dm.truthy(clockwise))
            segment = -segment;
        if (Dm.truthy(_listen_lookup?[COMSIG_ATOM_SPIN_ANIMATION]))
            _SendSignal(COMSIG_ATOM_SPIN_ANIMATION, Dm.list(this, speed, loops, segments, segment));
        do_spin_animation(speed, loops, segments, segment, parallel, tag);
    }
}

// tg: code/__HELPERS/visual_effects.dm:98
public partial class atom
{
    /// The degree of the opacity is optional, based off the opacity arg (0 -> 1)
    /// An optional color_override replaces the default blue tint — either a hex string (opacity applied on top) or a color matrix/list (passed through as-is; caller owns its own alpha)
    /// Returns the glow mutable_appearance so callers can cut the overlay later
    // tg: code/__HELPERS/visual_effects.dm:98
    public virtual object makeHologram(float opacity = 0.5f, object color_override = null)
    {
        // First, we'll make things blue (roughly) and sorta transparent
        object color_value;
        if (color_override is DmList)
            color_value = color_override;
        else
        {
            float r = 125;
            float g = 180;
            float b = 225;
            if (Dm.truthy(color_override))
            {
                DmList rgb_list = Dm.rgb2num(Dm.str(color_override));
                r = Dm.num(rgb_list[1]);
                g = Dm.num(rgb_list[2]);
                b = Dm.num(rgb_list[3]);
            }
            color_value = Dm.rgb(r, g, b, opacity * 255);
        }
        add_filter("HOLO: Color and Transparent", 1, color_matrix_filter(color_value) as DmList);
        // Now we're gonna do a scanline effect
        // Gonna take this atom and give it a render target, then use it as a source for a filter
        // (We use an atom because it seems as if setting render_target on an MA is just invalid. I hate this engine)
        atom_movable scanline = Dm.@new<atom_movable>(null);
        scanline.icon = Dm.resource("icons/effects/effects.dmi");
        scanline.icon_state = "scanline";
        scanline.appearance_flags = Dm.bitor(scanline.appearance_flags, RESET_TRANSFORM);
        // * so it doesn't render
        scanline.render_target = Dm.text("*HoloScanline []", TgState.Current.atom__makeHologram_uid_scan); // var/static/uid_scan
        TgState.Current.atom__makeHologram_uid_scan++;
        // Now we add it as a filter, and overlay the appearance so the render source is always around
        add_filter("HOLO: Scanline", 2, alpha_mask_filter(render_source: scanline.render_target) as DmList);
        add_overlay(scanline);
        qdel(scanline);
        // Annd let's make the sucker emissive, so it glows in the dark
        if (!Dm.truthy(render_target))
        {
            render_target = Dm.text("HOLOGRAM []", TgState.Current.atom__makeHologram_uid); // var/static/uid
            TgState.Current.atom__makeHologram_uid++;
        }
        // DEVIATION(DV12): the render step that copies the atom to the emissive plane (var/static/atom/movable/render_step/emissive/glow),
        // its overlay and update_overlays_on_z: the emissive glow
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/__HELPERS/visual_effects.dm:121  var/static/uid_scan
    internal float atom__makeHologram_uid_scan;
    // tg: code/__HELPERS/visual_effects.dm:130  var/static/uid
    internal float atom__makeHologram_uid;
}

