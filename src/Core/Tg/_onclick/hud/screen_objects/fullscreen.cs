// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/screen_objects/fullscreen.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The procs of the file and the types the M2 paths throw (M2 P3, D107): the crit, critvision, oxy and brute overlays of
// carbon/update_damage_hud, blindness (status_effects/debuffs/vision/blindness.dm:60-64) and the tint (carbon.dm:469). The
// fullscreen types of other milestones (flash, static, cinematic, the lighting backdrops) are not declared; the drugs' rainbow
// (high) since gaps RG2 (D339).

// tg: code/_onclick/hud/screen_objects/fullscreen.dm:1
public partial class mob : atom_movable
{
    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:1
    public virtual object overlay_fullscreen(object category = null, object type = null, object severity = null)
    {
        atom_movable_screen_fullscreen screen = screens?[category] as atom_movable_screen_fullscreen;
        if (!Dm.truthy(screen) || screen.type != (type as Type))
        {
            // needs to be recreated
            clear_fullscreen(category, FALSE);
            screens[category] = screen = Dm.@new((Type)type) as atom_movable_screen_fullscreen;
        }
        else if ((!Dm.truthy(severity) || Dm.eq(severity, screen.severity)) && (!Dm.truthy(client) || screen.screen_loc != "CENTER-7,CENTER-7" || Dm.eq(screen.view, client.view)))
        {
            // doesn't need to be updated
            return screen;
        }

        screen.icon_state = Dm.text("[][]", Dm.initial(screen, (atom p) => p.icon_state), severity);
        screen.severity = Dm.num(severity);
        if (Dm.truthy(client) && Dm.truthy(screen.should_show_to(this)))
        {
            screen.update_for_view(client.view);
            Dm.append(client.screen, screen);
        }

        if (Dm.truthy(screen.needs_offsetting))
            SET_PLANE_EXPLICIT(screen, Dm.num(PLANE_TO_TRUE(screen.plane)), this);

        return screen;
    }

    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:22
    public virtual void clear_fullscreen(object category = null, float? animated = null)
    {
        animated ??= 10;
        atom_movable_screen_fullscreen screen = screens?[category] as atom_movable_screen_fullscreen;
        if (!Dm.truthy(screen))
            return;

        Dm.remove(ref screens, category);

        if (!QDELETED(this) && Dm.truthy(animated))
        {
            animate(screen, Dm.named("alpha", 0), Dm.named("time", animated));
            _addtimer(Dm.@new<datum_callback>(this, nameof(clear_fullscreen_after_animate), screen), animated, TIMER_CLIENT_TIME, file: "code/_onclick/hud/screen_objects/fullscreen.dm", line: 33);
        }
        else
        {
            if (Dm.truthy(client))
                Dm.remove(client.screen, screen);
            qdel(screen);
        }
    }

    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:37
    public virtual void clear_fullscreen_after_animate(atom_movable_screen_fullscreen screen = null)
    {
        if (Dm.truthy(client))
            Dm.remove(client.screen, screen);
        qdel(screen);
    }

    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:42
    public virtual void clear_fullscreens()
    {
        foreach (object category in Dm.each(screens))
            clear_fullscreen(category);
    }

    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:46
    public virtual void hide_fullscreens()
    {
        if (Dm.truthy(client))
        {
            foreach (object category in Dm.each(screens))
                Dm.remove(client.screen, screens[category]);
        }
    }

    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:51
    public virtual void reload_fullscreen()
    {
        if (Dm.truthy(client))
        {
            atom_movable_screen_fullscreen screen = null;
            foreach (object category in Dm.each(screens))
            {
                screen = screens[category] as atom_movable_screen_fullscreen;
                if (Dm.truthy(screen.should_show_to(this)))
                {
                    screen.update_for_view(client.view);
                    Dm.combine(client.screen, screen);
                }
                else
                    Dm.remove(client.screen, screen);
            }
        }
    }

    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:62
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
        if (!Dm.truthy(same_z_layer))
            relayer_fullscreens();
    }

    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:67
    public virtual void relayer_fullscreens()
    {
        turf our_lad = get_turf(this);
        object offset = GET_TURF_PLANE_OFFSET(our_lad);
        foreach (object category in Dm.each(screens))
        {
            atom_movable_screen_fullscreen screen = screens[category] as atom_movable_screen_fullscreen;
            if (Dm.truthy(screen.needs_offsetting))
                screen.plane = GET_NEW_PLANE(Dm.initial(screen, (atom p) => p.plane), Dm.num(offset));
        }
    }
}

// tg: code/_onclick/hud/screen_objects/fullscreen.dm:75
[DmPath("/atom/movable/screen/fullscreen")]
public partial class atom_movable_screen_fullscreen : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:76-81
    public atom_movable_screen_fullscreen()
    {
        icon = Dm.resource("icons/hud/screen_full.dmi");
        icon_state = "default";
        screen_loc = "CENTER-7,CENTER-7";
        layer = FULLSCREEN_LAYER;
        plane = FULLSCREEN_PLANE;
        mouse_opacity = MOUSE_OPACITY_TRANSPARENT;
    }

    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:82 (object, not V4's float?: update_for_view stores the client's
    // view in it, :90, which is text such as "15x15" as often as a number; rules 3.5, a "-" cell, M2 P3)
    public object view = 7;
    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:83
    public float severity = 0;
    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:84
    public float show_when_dead = FALSE;
    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:85
    public float needs_offsetting = TRUE;
}

// tg: code/_onclick/hud/screen_objects/fullscreen.dm:87
public partial class atom_movable_screen_fullscreen : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:87
    public virtual void update_for_view(object client_view = null)
    {
        if (screen_loc == "CENTER-7,CENTER-7" && !Dm.eq(view, client_view))
        {
            // var/list/actualview = getviewsize(client_view): GAP(lead, __HELPERS/view.cs): /proc/getviewsize
            // (__HELPERS/view.dm:1) has no mirror yet; its body is written here (a number is a radius, text "WxH" a size).
            DmList actualview;
            if (isnum(client_view))
            {
                float view__1 = Dm.num(client_view);
                float totalviewrange = (view__1 < 0 ? -1 : 1) + 2 * view__1;
                actualview = view__1 == 0 ? Dm.list(0, 0) : Dm.list(totalviewrange, totalviewrange);
            }
            else
            {
                DmList viewrangelist = splittext(client_view, "x") as DmList;
                actualview = Dm.list(text2num(viewrangelist[1]), text2num(viewrangelist[2]));
            }
            view = client_view;
            transform = Dm.matrix(Dm.num(actualview[1]) / FULLSCREEN_OVERLAY_RESOLUTION_X, 0, 0, 0, Dm.num(actualview[2]) / FULLSCREEN_OVERLAY_RESOLUTION_Y, 0);
        }
    }

    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:93
    public virtual object should_show_to(mob mymob = null)
    {
        if (!Dm.truthy(show_when_dead) && mymob.stat == DEAD)
            return FALSE;
        return TRUE;
    }

    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:98
    public override object Destroy(float? force = null)
    {
        object dot = null;
        severity = 0;
        dot = base.Destroy(force);
        return dot;
    }
}

// tg: code/_onclick/hud/screen_objects/fullscreen.dm:102
[DmPath("/atom/movable/screen/fullscreen/brute")]
public partial class atom_movable_screen_fullscreen_brute : atom_movable_screen_fullscreen
{
    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:103-105
    public atom_movable_screen_fullscreen_brute()
    {
        icon_state = "brutedamageoverlay";
        layer = UI_DAMAGE_LAYER;
        plane = FULLSCREEN_PLANE;
    }
}

// tg: code/_onclick/hud/screen_objects/fullscreen.dm:107
[DmPath("/atom/movable/screen/fullscreen/oxy")]
public partial class atom_movable_screen_fullscreen_oxy : atom_movable_screen_fullscreen
{
    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:108-110
    public atom_movable_screen_fullscreen_oxy()
    {
        icon_state = "oxydamageoverlay";
        layer = UI_DAMAGE_LAYER;
        plane = FULLSCREEN_PLANE;
    }
}

// tg: code/_onclick/hud/screen_objects/fullscreen.dm:112
[DmPath("/atom/movable/screen/fullscreen/crit")]
public partial class atom_movable_screen_fullscreen_crit : atom_movable_screen_fullscreen
{
    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:113-115
    public atom_movable_screen_fullscreen_crit()
    {
        icon_state = "passage";
        layer = CRIT_LAYER;
        plane = FULLSCREEN_PLANE;
    }
}

// tg: code/_onclick/hud/screen_objects/fullscreen.dm:117
[DmPath("/atom/movable/screen/fullscreen/crit/vision")]
public partial class atom_movable_screen_fullscreen_crit_vision : atom_movable_screen_fullscreen_crit
{
    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:118-119
    public atom_movable_screen_fullscreen_crit_vision()
    {
        icon_state = "oxydamageoverlay";
        layer = BLIND_LAYER;
    }
}

// tg: code/_onclick/hud/screen_objects/fullscreen.dm:124
[DmPath("/atom/movable/screen/fullscreen/blind")]
public partial class atom_movable_screen_fullscreen_blind : atom_movable_screen_fullscreen
{
    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:126-128
    public atom_movable_screen_fullscreen_blind()
    {
        icon_state = "blackimageoverlay";
        layer = BLIND_LAYER;
        plane = FULLSCREEN_PLANE;
    }
}

// tg: code/_onclick/hud/screen_objects/fullscreen.dm:132
[DmPath("/atom/movable/screen/fullscreen/blind/noflicker")]
public partial class atom_movable_screen_fullscreen_blind_noflicker : atom_movable_screen_fullscreen_blind
{
    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:134-136
    public atom_movable_screen_fullscreen_blind_noflicker()
    {
        icon_state = "blackimageoverlaystatic";
        layer = BLIND_LAYER;
        plane = FULLSCREEN_PLANE;
    }
}

// tg: code/_onclick/hud/screen_objects/fullscreen.dm:146
[DmPath("/atom/movable/screen/fullscreen/impaired")]
public partial class atom_movable_screen_fullscreen_impaired : atom_movable_screen_fullscreen
{
    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:147
    public atom_movable_screen_fullscreen_impaired()
    {
        icon_state = "impairedoverlay";
    }
}

// Gaps RG2 (D339): the drugginess' rainbow overlay (earthsblood and the drugs make a mob druggy).

// tg: code/_onclick/hud/screen_objects/fullscreen.dm:165
public partial class atom_movable_screen_fullscreen_high : atom_movable_screen_fullscreen
{
    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:171
    public override void update_for_view(object client_view = null)
    {
        Dm.animate(this, Dm.named("flags", ANIMATION_END_NOW)); //Stop all animations.

        base.update_for_view(client_view);

        color = Dm.list(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0); // COLOR_MATRIX_IDENTITY (__DEFINES/matrices.dm:5, a list: expanded at each use). We convert it early to avoid a sudden weird jitter.
        alpha = 0;

        Dm.animate(this, Dm.named("alpha", 255), Dm.named("time", 5 * SECONDS)); //Fade in.

        _addtimer(Dm.@new<datum_callback>(this, nameof(start_hue_rotation)), 5 * SECONDS, file: "code/_onclick/hud/screen_objects/fullscreen.dm", line: 182);
    }

    // tg: code/_onclick/hud/screen_objects/fullscreen.dm:184
    public virtual void start_hue_rotation()
    {
        Dm.animate(this, Dm.named("color", color_matrix_rotate_hue(1)), Dm.named("loop", -1), Dm.named("time", 2 * SECONDS)); //Start the loop.
        float step_precision = 18; //Larger is more precise rotations.
        for (float current_step = 1; current_step <= step_precision - 1; current_step++) //We do the -1 here because 360 == 0 when it comes to angles.
        {
            Dm.animate(
                Dm.named("color", color_matrix_rotate_hue(current_step * 360 / step_precision)),
                Dm.named("time", 2 * SECONDS));
        }
    }
}
