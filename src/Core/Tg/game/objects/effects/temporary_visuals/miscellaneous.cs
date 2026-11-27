// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/temporary_visuals/miscellaneous.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: completed with its module (a later milestone). Holds the [DmPath] of a type that an M2 slot signature or
// var block names, in the earliest .dme file that declares it (rules 2.3); written by the M2 contracts step
// (docs/ai/specs/m2-ownership.md). Only the type is declared.
// M6 slice 1: the blood splatter of a hit; M6 slice 5 (gibbing): the gibbing animation of a human and of an animal
// (:319-328); the M7x slice B: the gun's firing effect (:61); the M7 projectiles slice: a projectile's impact (:383); the M7 supermatter slice, part 2: the circle wave (:782, a
// gravitational anomaly's detonation); E13: the RCD family's constructing effect (:518); syndicate X2: the decoys (:236-262); gaps R1: the
// bluespace artillery's splash (:173); gaps B1: the bluespace fissure (:310, the bluespace prophet's streams); gaps OD2: the focus ring (:823, the
// House Edge's wind-up); gaps MO2: the sonar ping (:613, the MOD active sonar's pings, DV541); the Lavaland fauna: the withering spawn (:720, the despawn
// of the hivelord's and the legion's minions); the megafauna: the cleaving saw's bleed and its burst (:456-480); the tendrils' loot: the warp cube's flash (:483). The other visuals of the file come with their mechanics.

// tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:691
[DmPath("/obj/effect/temp_visual/block")]
public partial class obj_effect_temp_visual_block : obj_effect_temp_visual
{
}

// tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:2 (interleaved types, rules 1.9; M6 slice 1: the blood
// splatter of a hit, create_splatter)
public partial class obj_effect_temp_visual_dir_setting_bloodsplatter : obj_effect_temp_visual_dir_setting
{
    // set_color arg can be either a color string or a singleton /datum/blood_type to pull the color from
    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:12
    [DmParams("mapload", "set_dir", "set_color")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object set_dir = Dm.arg(__extra, 0);
        object set_color = Dm.arg(__extra, 1) ?? BLOOD_COLOR_RED;
        if (Dm.truthy(set_color))
        {
            if (set_color is datum_blood_type blood_type)
                color = blood_type.color;
            else
                color = set_color;
        }
        if (Dm.truthy(ISDIAGONALDIR(Dm.num(set_dir))))
            icon_state = Dm.text("[][]", base_icon_state, pick(1, 2, 6));
        else
            icon_state = Dm.text("[][]", base_icon_state, pick(3, 4, 5));
        object dot = base.Initialize(mapload, set_dir);
        float target_pixel_x = 0;
        float target_pixel_y = 0;
        float sw__1 = Dm.num(set_dir);
        if (sw__1 == NORTH)
            target_pixel_y = 16;
        else if (sw__1 == SOUTH)
        {
            target_pixel_y = -16;
            layer = ABOVE_MOB_LAYER;
        }
        else if (sw__1 == EAST)
            target_pixel_x = 16;
        else if (sw__1 == WEST)
            target_pixel_x = -16;
        else if (sw__1 == NORTHEAST)
        {
            target_pixel_x = 16;
            target_pixel_y = 16;
        }
        else if (sw__1 == NORTHWEST)
        {
            target_pixel_x = -16;
            target_pixel_y = 16;
        }
        else if (sw__1 == SOUTHEAST)
        {
            target_pixel_x = 16;
            target_pixel_y = -16;
            layer = ABOVE_MOB_LAYER;
        }
        else if (sw__1 == SOUTHWEST)
        {
            target_pixel_x = -16;
            target_pixel_y = -16;
            layer = ABOVE_MOB_LAYER;
        }
        animate(this, Dm.named("pixel_x", target_pixel_x), Dm.named("pixel_y", target_pixel_y), Dm.named("alpha", 0), Dm.named("time", duration), Dm.named("flags", Dm.bitor(CUBIC_EASING, EASE_OUT)));
        return dot;
    }
}

// tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:61  (M7x slice B: the muzzle flash of a gun's shot; the vars are generated, D111)
public partial class obj_effect_temp_visual_dir_setting_firing_effect : obj_effect_temp_visual_dir_setting
{
    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:65
    [DmParams("mapload", "set_dir")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (ismovable(loc))
        {
            atom_movable spawned_inside = loc as atom_movable;
            Dm.append(spawned_inside.vis_contents, this);
        }
        return dot;
    }

    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:71
    public override async DmTask setDir(object newdir = null)
    {
        if (Dm.eq(newdir, NORTH))
        {
            layer = BELOW_MOB_LAYER;
            pixel_x = rand(-3, 3);
            pixel_y = rand(4, 6);
        }
        else if (Dm.eq(newdir, SOUTH))
        {
            pixel_x = rand(-3, 3);
            pixel_y = rand(-1, 1);
        }
        else
        {
            pixel_x = rand(-1, 1);
            pixel_y = rand(-1, 1);
        }
        await base.setDir(newdir);
    }
}

// tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:319  (M6 slice 5; the vars are generated, D111)
public partial class obj_effect_temp_visual_gib_animation : obj_effect_temp_visual
{
    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:323
    [DmParams("mapload", "gib_icon")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        icon_state = Dm.arg(__extra, 0) as string; // Needs to be before ..() so icon is correct
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:380  (the M7 projectiles slice: a projectile's impact, on_hit; the
// vars are generated, D111)
public partial class obj_effect_temp_visual_impact_effect : obj_effect_temp_visual
{
    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:383
    [DmParams("mapload", "x", "y")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object x = Dm.arg(__extra, 0);
        object y = Dm.arg(__extra, 1);
        pixel_x = Dm.num(x);
        pixel_y = Dm.num(y);
        return base.Initialize(mapload, __extra);
    }
}

/// Visual effect spawned when the bioscrambler scrambles your bio
// tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:782
public partial class obj_effect_temp_visual_circle_wave : obj_effect_temp_visual
{
    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:792
    [DmParams("mapload", "color")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object color = Dm.arg(__extra, 0);
        transform = Dm.@new<matrix>().Scale(0.1f);
        Dm.animate(this, Dm.named("transform", Dm.@new<matrix>().Scale(amount_to_scale)), Dm.named("time", duration), Dm.named("flags", ANIMATION_PARALLEL));
        Dm.animate(this, Dm.named("alpha", max_alpha), Dm.named("time", Dm.num(duration) * 0.6f), Dm.named("flags", ANIMATION_PARALLEL));
        Dm.animate(Dm.named("alpha", 0), Dm.named("time", Dm.num(duration) * 0.4f));
        apply_wibbly_filters(this);
        if (!Dm.truthy(this.color))
            this.color = color;
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:518 (interleaved types, rules 1.9; E13: the RCD's constructing effect)
public partial class obj_effect_constructing_effect : obj_effect
{
    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:529
    [DmParams("mapload", "rcd_delay", "rcd_status", "rcd_upgrades")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object rcd_delay = Dm.arg(__extra, 0);
        object rcd_status = Dm.arg(__extra, 1);
        object rcd_upgrades = Dm.arg(__extra, 2);
        object dot = base.Initialize(mapload, __extra);
        status = Dm.num(rcd_status);
        delay = Dm.num(rcd_delay);
        if (status == RCD_DECONSTRUCT)
        {
            _addtimer(Dm.@new<datum_callback>(this, nameof(update_appearance)), 1.1f * SECONDS, file: "code/game/objects/effects/temporary_visuals/miscellaneous.dm", line: 534);
            delay -= 11;
            icon_state = "rcd_end_reverse";
        }
        else
            update_appearance();

        if (Dm.truthy(Dm.bitand(Dm.num(rcd_upgrades), RCD_UPGRADE_ANTI_INTERRUPT)))
        {
            color = Dm.list(
                1.0f, 0.5f, 0.5f, 0.0f,
                0.1f, 0.0f, 0.0f, 0.0f,
                0.1f, 0.0f, 0.0f, 0.0f,
                0.0f, 0.0f, 0.0f, 1.0f,
                0.0f, 0.0f, 0.0f, 0.0f);

            mouse_opacity = MOUSE_OPACITY_TRANSPARENT;
            obj_flags = Dm.bitand(obj_flags, Dm.bitnot(CAN_BE_HIT));
        }
        return dot;
    }

    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:552
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);

        if (status == RCD_DECONSTRUCT)
            name = "deconstruction effect";
        else
            name = "construction effect";
        return dot;
    }

    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:560
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = "rcd";
        if (delay < 10)
        {
            icon_state += "_shortest";
            return await base.update_icon_state(__extra);
        }
        if (delay < 20)
        {
            icon_state += "_shorter";
            return await base.update_icon_state(__extra);
        }
        if (delay < 37)
        {
            icon_state += "_short";
            return await base.update_icon_state(__extra);
        }
        if (status == RCD_DECONSTRUCT)
            icon_state += "_reverse";
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:575
    public virtual void end_animation()
    {
        if (status == RCD_DECONSTRUCT)
            qdel(this);
        else
        {
            mouse_opacity = MOUSE_OPACITY_TRANSPARENT;
            obj_flags = Dm.bitand(obj_flags, Dm.bitnot(CAN_BE_HIT));
            icon_state = "rcd_end";
            _addtimer(Dm.@new<datum_callback>(this, nameof(end)), 1.5f * SECONDS, file: "code/game/objects/effects/temporary_visuals/miscellaneous.dm", line: 582);
        }
    }

    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:584
    public virtual void end()
    {
        qdel(this);
    }

    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:587
    public virtual async DmTask attacked(mob user = null)
    {
        await user.do_attack_animation(this, ATTACK_EFFECT_PUNCH);
        user.changeNext_move(CLICK_CD_MELEE);
        playsound(loc as atom, Dm.resource("sound/items/weapons/egloves.ogg"), vol: 80, vary: TRUE);
        end();
    }

    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:593
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        await attacked(user);
        return null;
    }

    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:596
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        await attacked(user);
        return null;
    }
}

// tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:433
public partial class obj_effect_temp_visual_heart : obj_effect_temp_visual
{
    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:439
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        pixel_x = rand(-4, 4);
        pixel_y = rand(-4, 4);
        Dm.animate(this, Dm.named("pixel_y", Dm.num(pixel_y) + 32), Dm.named("alpha", 0), Dm.named("time", 25));
        return dot;
    }
}

// tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:331
public partial class obj_effect_temp_visual_heal : obj_effect_temp_visual
{
    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:336
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object set_color = Dm.arg(__extra, 0);
        if (Dm.truthy(set_color))
            Waiting.NoSleep(add_atom_colour(set_color, FIXED_COLOUR_PRIORITY));
        object dot = base.Initialize(mapload, __extra);
        pixel_x = rand(-12, 12);
        pixel_y = rand(-9, 0);
        return dot;
    }
}

// tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:236  (syndicate X2: the charger holoparasite's charge)
public partial class obj_effect_temp_visual_decoy : obj_effect_temp_visual
{
    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:239
    [DmParams("mapload", "mimiced_atom")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        atom mimiced_atom = Dm.arg<atom>(__extra, 0);
        object dot = base.Initialize(mapload);
        alpha = Dm.initial(this, (atom p) => p.alpha);
        if (mimiced_atom != null)
        {
            name = mimiced_atom.name;
            appearance = mimiced_atom.appearance;
            Waiting.NoSleep(setDir(mimiced_atom.dir));
            mouse_opacity = MOUSE_OPACITY_TRANSPARENT;
        }
        return dot;
    }
}

// tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:248
public partial class obj_effect_temp_visual_decoy_fading : obj_effect_temp_visual_decoy
{
    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:248
    [DmParams("mapload", "mimiced_atom", "start_alpha")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object start_alpha = Dm.arg(__extra, 1);
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(start_alpha))
            alpha = Dm.num(start_alpha);
        animate(this, Dm.named("alpha", 0), Dm.named("time", duration));
        return dot;
    }
}

// tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:703  (syndicate X3b: boxing's critical hit)
public partial class obj_effect_temp_visual_crit : obj_effect_temp_visual
{
    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:708
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.animate(this, Dm.named("pixel_y", Dm.num(pixel_y) + 16), Dm.named("alpha", 0), Dm.named("time", duration));
        return dot;
    }
}

// tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:173  (gaps R1: the bluespace artillery's muzzle splash)
public partial class obj_effect_temp_visual_bsa_splash : obj_effect_temp_visual
{
    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:183
    [DmParams("mapload", "dir")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dir = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if (Dm.eq(dir, WEST))
            icon_state = "beam_splash_w";
        else if (Dm.eq(dir, EAST))
            icon_state = "beam_splash_e";
        return dot;
    }
}

// tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:310  (gaps B1: the bluespace prophet's streams; the vars are generated, D111)
public partial class obj_effect_temp_visual_bluespace_fissure : obj_effect_temp_visual
{
    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:315
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        apply_wibbly_filters(this);
        return dot;
    }
}

// tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:823  (gaps OD2: the House Edge's wind-up; the vars are generated, D111)
public partial class obj_effect_temp_visual_focus_ring : obj_effect_temp_visual
{
    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:832
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        animate(this, Dm.named("alpha", 100), Dm.named("time", 2 * SECONDS), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_IN)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(dissipate)), 2 * SECONDS, file: "code/game/objects/effects/temporary_visuals/miscellaneous.dm", line: 835);
        return dot;
    }

    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:838
    public virtual void dissipate()
    {
        animate(this, Dm.named("alpha", 0), Dm.named("time", 0.5f * SECONDS), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_OUT)));
    }
}

// tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:613  (gaps MO2: the MOD active sonar's pings; the vars are generated, D111)
public partial class obj_effect_temp_visual_sonar_ping : obj_effect_temp_visual
{
    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:629
    [DmParams("mapload", "looker", "creature", "ping_state", "follow_creatures")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        mob_living looker = Dm.arg(__extra, 0) as mob_living;
        mob_living creature = Dm.arg(__extra, 1) as mob_living;
        object ping_state = Dm.arg(__extra, 2);
        object follow_creatures = Dm.arg(__extra, 3) ?? TRUE;
        object dot = base.Initialize(mapload, __extra);
        if (looker == null || creature == null)
            return INITIALIZE_HINT_QDEL;
        if (Dm.truthy(ping_state))
            real_icon_state = ping_state as string;
        follow_creature = Dm.num(follow_creatures);

        // DEVIATION(DV541): tg hangs the image on the looker's tile and shifts it by the pixel distance to the creature
        // (pixel_w = (creature.x - looker.x) * ICON_SIZE_X + creature.pixel_w, pixel_z likewise), a 2D map's way to draw it on the
        // creature's tile; the first-person client draws an image on the tile it hangs on, so it hangs on the creature's tile
        modsuit_image = image(icon: icon, loc: get_turf(creature), icon_state: real_icon_state, layer: ABOVE_ALL_MOB_LAYER);
        modsuit_image.plane = ABOVE_LIGHTING_PLANE;
        SET_PLANE_EXPLICIT(modsuit_image, ABOVE_LIGHTING_PLANE, creature);
        if (ishuman(creature))
            modsuit_image.color = COLOR_SOFT_RED;
        mod_man = WEAKREF(looker) as datum_weakref;
        pinged_person = WEAKREF(creature) as datum_weakref;
        if (looker?.client != null)
            Dm.combine(looker.client.images, modsuit_image);
        RegisterSignal(looker, COMSIG_MOVABLE_MOVED, nameof(on_user_moved));
        if (Dm.truthy(follow_creature))
            RegisterSignal(creature, COMSIG_MOVABLE_MOVED, nameof(on_target_moved));
        return dot;
    }

    /// If the user moves, we want to move the effect on top of them so they always see it
    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:652
    [SignalHandler]
    public virtual void on_user_moved(atom_movable source = null, atom oldloc = null, object direction = null, object forced = null, DmList old_locs = null)
    {
        mob_living looker = mod_man?.resolve() as mob_living;
        if (looker == null)
            return;
        // DEVIATION(DV541): tg moves the image to the looker's tile and shifts it back onto the creature's; here it hangs on the
        // creature's tile already, so it moves only with a followed creature
        if (Dm.truthy(follow_creature))
        {
            mob_living creature = pinged_person?.resolve() as mob_living;
            if (creature == null)
                return;
            modsuit_image.loc = get_turf(creature);
        }
    }

    /// If the target moves & we have follow_creature = TRUE, then we want to also update the location of the ping when the creature moves
    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:667
    [SignalHandler]
    public virtual void on_target_moved(atom_movable source = null, atom oldloc = null, object direction = null, object forced = null, DmList old_locs = null)
    {
        mob_living looker = mod_man?.resolve() as mob_living;
        mob_living creature = pinged_person?.resolve() as mob_living;
        if (looker == null || creature == null)
            return;
        // Long pings follow, short pings stay put. (DEVIATION(DV541): the image hangs on the creature's tile, not shifted from the looker's)
        modsuit_image.loc = get_turf(creature);
    }

    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:679
    public override object Destroy(float? force = null)
    {
        mob_living previous_user = mod_man?.resolve() as mob_living;
        if (previous_user != null)
        {
            if (previous_user?.client != null)
                Dm.remove(previous_user.client.images, modsuit_image);
            UnregisterSignal(previous_user, COMSIG_MOVABLE_MOVED);
        }
        mob_living creature = pinged_person?.resolve() as mob_living;
        if (creature != null)
            UnregisterSignal(creature, COMSIG_MOVABLE_MOVED);
        // Null so we don't shit the bed when we delete
        QDEL_NULL(ref modsuit_image);
        return base.Destroy(force);
    }
}

// Gaps RG2 (D339): the curse visuals (the curse hands of helgrasp, the inverse of helbital, and the necropolis curse's grasp).

// tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:144
public partial class obj_effect_temp_visual_dir_setting_curse : obj_effect_temp_visual_dir_setting
{
    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:148
    [DmParams("mapload", "set_dir")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(fades))
            Dm.animate(this, Dm.named("alpha", 0), Dm.named("time", 32));
        return dot;
    }
}

/// Plays a dispersing animation on hivelord and legion minions so they don't just vanish
// tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:720
public partial class obj_effect_temp_visual_despawn_effect : obj_effect_temp_visual
{
    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:724
    [DmParams("mapload", "copy_from")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        atom copy_from = Dm.arg<atom>(__extra, 0);
        if (copy_from == null)
        {
            base.Initialize(mapload, __extra);
            return INITIALIZE_HINT_QDEL;
        }
        icon = copy_from.icon;
        icon_state = copy_from.icon_state;
        pixel_x = copy_from.pixel_x;
        pixel_y = copy_from.pixel_y;
        duration = rand(0.5f * SECONDS, 1 * SECONDS);
        matrix transformation = Dm.matrix(transform);
        transformation.Turn(rand(-70, 70));
        transformation.Scale(0.7f, 0.7f);
        animate(
            this,
            Dm.named("pixel_x", rand(-5, 5)),
            Dm.named("pixel_y", -5),
            Dm.named("transform", transformation),
            Dm.named("color", "#44444400"),
            Dm.named("time", duration),
            Dm.named("flags", ANIMATION_RELATIVE));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:456
public partial class obj_effect_temp_visual_bleed : obj_effect_temp_visual
{
    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:463
    [DmParams("mapload", "size_calc_target")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        atom size_calc_target = Dm.arg(__extra, 0) as atom;
        object dot = base.Initialize(mapload, __extra);
        object size_matrix = Dm.matrix();
        if (size_calc_target != null)
        {
            layer = Dm.num(size_calc_target.layer) + 0.01f;
            size_matrix = Dm.mul(Dm.matrix(), Dm.num(size_calc_target.get_visual_height()) / ICON_SIZE_Y);
            transform = size_matrix; //scale the bleed overlay's size based on the target's icon size
        }
        object M = transform;
        if (Dm.truthy(shrink))
            M = Dm.mul(size_matrix, 0.1f);
        else
            M = Dm.mul(size_matrix, 2);
        animate(this, Dm.named("alpha", 20), Dm.named("transform", M), Dm.named("time", duration), Dm.named("flags", ANIMATION_PARALLEL));
        return dot;
    }
}


// tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:482
public partial class obj_effect_temp_visual_warp_cube : obj_effect_temp_visual
{
    // tg: code/game/objects/effects/temporary_visuals/miscellaneous.dm:486
    [DmParams("mapload", "teleporting_atom", "warp_color", "new_outgoing")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        atom teleporting_atom = Dm.arg<atom>(__extra, 0);
        object warp_color = Dm.arg(__extra, 1);
        object new_outgoing = Dm.arg(__extra, 2);
        object dot = base.Initialize(mapload, __extra);
        if (teleporting_atom != null)
        {
            outgoing = Dm.num(new_outgoing);
            appearance = teleporting_atom.appearance;
            Waiting.NoSleep(setDir(teleporting_atom.dir));
            if (Dm.truthy(warp_color))
            {
                color = Dm.list(warp_color, warp_color, warp_color, Dm.list(0, 0, 0));
                Waiting.NoSleep(set_light(1.4f, 1, warp_color));
            }
            mouse_opacity = MOUSE_OPACITY_TRANSPARENT;
            matrix skew = transform as matrix;
            skew = skew.Turn(180);
            skew = skew.Interpolate(transform as matrix, 0.5f);
            if (!Dm.truthy(outgoing))
            {
                transform = Dm.mul(skew, 2);
                skew = teleporting_atom.transform as matrix;
                alpha = 0;
                animate(this, Dm.named("alpha", teleporting_atom.alpha), Dm.named("transform", skew), Dm.named("time", duration));
            }
            else
            {
                skew = Dm.mul(skew, 2) as matrix;
                animate(this, Dm.named("alpha", 0), Dm.named("transform", skew), Dm.named("time", duration));
            }
        }
        else
            return INITIALIZE_HINT_QDEL;
        return dot;
    }
}
