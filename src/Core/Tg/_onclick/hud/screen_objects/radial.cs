// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/screen_objects/radial.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4c slice 4 (D146 moves tg's radial menu from M9 into M4c; protocol version 15, DV40). show_radial_menu, the menu datum's
// choices, pages, waiting and answer are tg's. What tg draws with screen objects (the slices and the centre button around the
// anchor, /atom/movable/screen/radial, in a holder image of client.images) the client draws at its crosshair instead (DV40):
// show_to lists the menu in client.dm_radial_menus, update_screen_objects writes each element of the current page as a record
// (RadialElement: SetElement's name, choice, next-page mark and picture) instead of setting a slice's vars, and the client's
// pick runs the slice's or the centre's Click (ClientSeam.RadialPick). The slice and centre screen types stay inert (rows).

/// <summary>
/// DV40: one element of a radial menu's current page as tg's <c>SetElement</c> (radial.dm:238) sets up its slice: the name, the
/// choice id, the next-page mark, the choice's picture (the slice's overlay) and the slice's background pictures, plus what
/// tg's <c>update_screen_objects</c> decides for it (its angle, its animation order, click on hover). Read by the replicator.
/// </summary>
public sealed class RadialElement
{
    /// <summary>The slice's name (tg's <c>E.name</c>).</summary>
    public string Name;

    /// <summary>The choice id (<c>"c_0"</c>, ...), null for a next-page slice.</summary>
    public object Choice;

    /// <summary>tg's <c>E.next_page</c>.</summary>
    public bool NextPage;

    /// <summary>The choice's picture, the slice's overlay (<c>choices_icons[choice_id]</c>, or <c>"radial_next"</c>), null for none.</summary>
    public mutable_appearance Icon;

    /// <summary>The slice itself: <c>radial.dmi</c> with the slice's icon state, and the state it shows while hovered (MouseEntered).</summary>
    public mutable_appearance Background;

    public mutable_appearance BackgroundFocus;

    /// <summary>tg's <c>choice_datum.info</c> (the info button's text), null for none.</summary>
    public string Info;

    /// <summary>tg's <c>choice_datum.tooltip_theme</c>.</summary>
    public string TooltipTheme;

    /// <summary>The slice's angle on tg's ring (degrees clockwise from north) and its slide-in order (1 first).</summary>
    public float Angle;

    public int AnimOrder;

    /// <summary>tg's <c>element.click_on_hover</c>.</summary>
    public bool ClickOnHover;
}

public partial class datum_controller_global_vars
{
    // tg: code/_onclick/hud/screen_objects/radial.dm:1  GLOBAL_LIST_EMPTY(radial_menus)
    public DmList radial_menus;

    // tg: code/_onclick/hud/screen_objects/radial.dm:1  (GLOBAL_MANAGED(radial_menus, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalradial_menus()
    {
        radial_menus = Dm.list();
        Dm.append(ref gvars_datum_init_order, "radial_menus");
    }
}

// tg: code/_onclick/hud/screen_objects/radial.dm:76
public partial class datum_radial_menu : datum
{
    /// <summary>DV40: the elements of the current page (tg's slices), set by <see cref="update_screen_objects"/>.</summary>
    [NotHashed]
    public readonly List<RadialElement> dm_elements = new();

    /// <summary>DV40: counts the changes of <see cref="dm_elements"/> (the replicator sends a menu again when it changes).</summary>
    public int dm_serial;

    /// <summary>DV40: the centre button's pictures (tg's <c>close_button</c>: <c>radial_center</c>, hovered <c>radial_center_focus</c>), null without one.</summary>
    [NotHashed]
    public mutable_appearance dm_close_button;

    [NotHashed]
    public mutable_appearance dm_close_button_focus;

    /// <summary>DV40: the tooltips and click-on-hover arguments of the last <see cref="setup_menu"/> (tg sets them on each slice).</summary>
    public float dm_tooltips;

    public float dm_click_on_hover;

    //If we swap to vis_contens inventory these will need a redo
    // tg: code/_onclick/hud/screen_objects/radial.dm:120
    public virtual void check_screen_border(mob user = null)
    {
        atom_movable AM = anchor as atom_movable;
        if (!istype(AM, typeof(atom_movable)))
            return;

        bool in_screen = Dm.truthy(AM.screen_loc) && Dm.@in(AM, user.client.screen);
        if (!in_screen)
        {
            DmList check_locs = AM.vis_locs.Copy();
            int i = 1;
            while (i <= Dm.length(check_locs))
            {
                atom_movable other_check = check_locs[(float)i] as atom_movable;
                i += 1;
                if (!istype(other_check, typeof(atom_movable)))
                    continue;
                if (Dm.truthy(other_check.screen_loc) && Dm.@in(other_check, user.client.screen))
                {
                    in_screen = true;
                    break;
                }
                Dm.combine(ref check_locs, other_check.vis_locs);
            }

            if (!in_screen)
            {
                if (Dm.truthy(hudfix_method) && Dm.truthy(AM.loc))
                    anchor = get_atom_on_turf(anchor as atom_movable) as atom;
                return;
            }
        }

        if (Dm.truthy(hudfix_method))
        {
            anchor = user;
            return;
        }

        py_shift = 32;
        restrict_to_dir(NORTH); //I was going to parse screen loc here but that's more effort than it's worth.
    }

    //Sets defaults
    //These assume 45 deg min_angle
    // tg: code/_onclick/hud/screen_objects/radial.dm:153
    public virtual void restrict_to_dir(object dir = null)
    {
        object sw__1 = dir;
        if (Dm.eq(sw__1, NORTH))
        {
            starting_angle = 270;
            ending_angle = 135;
        }
        else if (Dm.eq(sw__1, SOUTH))
        {
            starting_angle = 90;
            ending_angle = 315;
        }
        else if (Dm.eq(sw__1, EAST))
        {
            starting_angle = 0;
            ending_angle = 225;
        }
        else if (Dm.eq(sw__1, WEST))
        {
            starting_angle = 180;
            ending_angle = 45;
        }
    }

    // tg: code/_onclick/hud/screen_objects/radial.dm:168
    public virtual void setup_menu(object use_tooltips = null, float? set_page = null, float? click_on_hover = null)
    {
        set_page ??= 1;
        click_on_hover ??= FALSE;
        if (ending_angle > starting_angle)
            zone = ending_angle - starting_angle;
        else
            zone = 360 - starting_angle + ending_angle;

        max_elements = Dm.round(zone / min_angle);
        bool paged = Dm.num(max_elements) < choices.len;
        // DEVIATION (DV40): tg creates the max_elements slices here (elements, new /atom/movable/screen/radial/slice with the
        // tooltips flag, set_parent, alpha 0 for BUTTON_FADE_IN); the client draws them, so their settings are kept on the menu.
        dm_tooltips = Dm.num(use_tooltips);
        dm_click_on_hover = click_on_hover.Value;

        float page = 1;
        page_data = Dm.list(new object[] { null });
        DmList current = Dm.list();
        DmList choices_left = choices.Copy();
        while (Dm.truthy(choices_left.len))
        {
            if (current.len == Dm.num(max_elements))
            {
                page_data[page] = current;
                page++;
                page_data.len++;
                current = Dm.list();
            }
            if (paged && current.len == Dm.num(max_elements) - 1)
            {
                Dm.append(ref current, NEXT_PAGE_ID);
                continue;
            }
            else
            {
                Dm.append(ref current, popleft(choices_left));
            }
        }
        if (paged && current.len < Dm.num(max_elements))
            Dm.append(ref current, NEXT_PAGE_ID);

        page_data[page] = current;
        pages = page;
        current_page = Dm.clamp(set_page.Value, 1, pages);
        update_screen_objects(button_animation_flags, click_on_hover);
    }

    // tg: code/_onclick/hud/screen_objects/radial.dm:209
    public virtual void update_screen_objects(float? anim_flag = null, float? click_on_hover = null)
    {
        anim_flag ??= NONE;
        click_on_hover ??= FALSE;
        DmList page_choices = page_data[current_page] as DmList;
        float angle_per_element = Dm.round(zone / page_choices.len);
        // DEVIATION (DV40): tg walks its slices (elements), hides those past the page (HideElement) and sets up the others
        // (SetElement); the port writes a record of each element of the page for the client, which draws them.
        dm_elements.Clear();
        for (int i = 1; i <= page_choices.len; i++)
        {
            float angle = WRAP(starting_angle + (i - 1) * angle_per_element, 0, 360);
            RadialElement element = dm_element(page_choices[(float)i], angle, anim_flag.Value, i);
            // Only activate click on hover after the animation plays
            // (tg: addtimer(VARSET_CALLBACK(element, click_on_hover, TRUE), i * 0.5) with an animation, at once without: the
            // client turns it on after its slide-in.)
            element.ClickOnHover = Dm.truthy(click_on_hover);
            dm_elements.Add(element);
        }
        dm_serial++;
    }

    // tg: code/_onclick/hud/screen_objects/radial.dm:228
    public virtual void HideElement(atom_movable_screen_radial_slice E = null)
    {
        // DEVIATION (DV40): the slices are the client's; update_screen_objects leaves the hidden ones out of the page's records.
        Dm.NotTranslated("code/_onclick/hud/screen_objects/radial.dm:228", "HideElement: never as such (DV40): the client draws the radial's slices");
    }

    // tg: code/_onclick/hud/screen_objects/radial.dm:238
    public virtual void SetElement(atom_movable_screen_radial_slice E = null, object choice_id = null, object angle = null, object anim_flag = null, object anim_order = null)
    {
        // DEVIATION (DV40): the slices are the client's; dm_element writes the same settings as a record.
        Dm.NotTranslated("code/_onclick/hud/screen_objects/radial.dm:238", "SetElement: never as such (DV40): dm_element writes the slice's settings for the client");
    }

    // DV40: tg's SetElement (radial.dm:238) for the client's slice: what it sets on E, written to a record. The position
    // (pixel_x/pixel_y on the ring of radius + py_shift) and the slide and fade animations are the client's drawing.
    private RadialElement dm_element(object choice_id, float angle, float anim_flag, int anim_order)
    {
        var E = new RadialElement { Angle = angle, AnimOrder = anim_order };
        // icon_state: set_parent gives a slice the menu's radial_slice_icon (radial.dm:31), a next-page slice "radial_slice"
        string slice_state = radial_slice_icon as string ?? "radial_slice";
        if (Dm.eq(choice_id, NEXT_PAGE_ID))
        {
            E.Name = "Next Page";
            E.NextPage = true;
            slice_state = "radial_slice"; // Resets the bg icon state to the default for next page buttons.
            E.Icon = dm_radial_picture("radial_next");
            E.Background = dm_radial_picture(slice_state);
            E.BackgroundFocus = dm_radial_picture("radial_slice_focus"); // MouseEntered (radial.dm:35-36)
            return E;
        }
        //This isn't granted to exist, so use the ?. operator for conditionals that use it.
        datum_radial_menu_choice choice_datum = choice_datums[choice_id] as datum_radial_menu_choice;
        if (Dm.truthy(choice_datum?.name))
            E.Name = Dm.str(choice_datum.name);
        else if (istext(choices_values[choice_id]))
            E.Name = choices_values[choice_id] as string;
        else if (ispath(choices_values[choice_id], typeof(atom)))
            E.Name = Dm.str(Dm.initial((Type)choices_values[choice_id], (atom A) => A.name));
        else
        {
            atom_movable AM = choices_values[choice_id] as atom_movable; //Movables only
            E.Name = Dm.str(AM.name);
        }
        E.Choice = choice_id;
        E.TooltipTheme = choice_datum?.tooltip_theme as string;
        E.NextPage = false;
        if (Dm.truthy(choices_icons[choice_id]))
            E.Icon = choices_icons[choice_id] as mutable_appearance;
        if (Dm.truthy(choice_datum?.info))
        {
            // tg: an /obj/effect/abstract/info button in the slice's vis_contents, named "Info: [E.name]" (radial.dm:286-291);
            // DV40: the client draws it from the text.
            E.Info = Dm.str(choice_datum.info);
        }
        E.Background = dm_radial_picture(slice_state);
        E.BackgroundFocus = dm_radial_picture(slice_state + "_focus"); // MouseEntered (radial.dm:38)
        return E;
    }

    // DV40: a picture of icons/hud/radial.dmi as the slices and the centre show it (their icon, ABOVE_HUD_PLANE, radial.dm:4-5).
    protected static mutable_appearance dm_radial_picture(string state)
    {
        mutable_appearance MA = Dm.@new<mutable_appearance>();
        MA.icon = Dm.resource("icons/hud/radial.dmi");
        MA.icon_state = state;
        MA.plane = ABOVE_HUD_PLANE;
        return MA;
    }

    // tg: code/_onclick/hud/screen_objects/radial.dm:293
    [DmParams("display_close_button")]
    public override async DmTask New(params object[] __extra)
    {
        object display_close_button = Dm.arg(__extra, 0);
        await base.New(__extra);
        if (!Dm.truthy(display_close_button))
            return;
        // close_button = new; close_button.set_parent(src)
        // DEVIATION (DV40): the centre button is the client's; its pictures (radial.dm:62, :66) are kept for it.
        dm_close_button = dm_radial_picture("radial_center");
        dm_close_button_focus = dm_radial_picture("radial_center_focus");
    }

    // tg: code/_onclick/hud/screen_objects/radial.dm:299
    public virtual void Reset()
    {
        choices.Cut();
        choices_icons.Cut();
        choices_values.Cut();
        choice_datums.Cut();
        current_page = 1;
    }

    // tg: code/_onclick/hud/screen_objects/radial.dm:306
    public virtual async DmTask element_chosen(object choice_id = null, mob user = null, params object[] __extra)
    {
        selected_choice = choices_values[choice_id];
    }

    // tg: code/_onclick/hud/screen_objects/radial.dm:309
    public virtual object get_next_id()
    {
        return Dm.text("c_[]", choices.len);
    }

    // tg: code/_onclick/hud/screen_objects/radial.dm:312
    public virtual void set_choices(DmList new_choices = null, object use_tooltips = null, float? click_on_hover = null, float? set_page = null)
    {
        click_on_hover ??= FALSE;
        set_page ??= 1;
        if (Dm.truthy(choices.len))
            Reset();
        foreach (object E in Dm.each(new_choices))
        {
            object id = get_next_id();
            Dm.append(ref choices, id);
            choices_values[id] = E;
            if (Dm.truthy(new_choices[E]))
            {
                object I = extract_image(new_choices[E]);
                if (Dm.truthy(I))
                    choices_icons[id] = I;

                if (istype(new_choices[E], typeof(datum_radial_menu_choice)))
                    choice_datums[id] = new_choices[E];
            }
        }
        setup_menu(use_tooltips, set_page, click_on_hover);
    }

    // tg: code/_onclick/hud/screen_objects/radial.dm:328
    public virtual object extract_image(object to_extract_from = null)
    {
        if (to_extract_from is datum_radial_menu_choice choice)
            to_extract_from = choice.image;

        mutable_appearance MA = Dm.@new<mutable_appearance>(to_extract_from);
        if (Dm.truthy(MA))
        {
            SET_PLANE_EXPLICIT(MA, ABOVE_HUD_PLANE, anchor);
            MA.layer = RADIAL_CONTENT_LAYER;
            MA.appearance_flags = Dm.bitor(MA.appearance_flags, RESET_TRANSFORM);
        }
        return MA;
    }

    // tg: code/_onclick/hud/screen_objects/radial.dm:341
    public virtual void next_page()
    {
        if (pages > 1)
        {
            current_page = WRAP(current_page + 1, 1, pages + 1);
            update_screen_objects();
        }
    }

    // tg: code/_onclick/hud/screen_objects/radial.dm:346
    public virtual void show_to(mob M = null, float? offset_x = null, float? offset_y = null)
    {
        offset_x ??= 0;
        offset_y ??= 0;
        if (Dm.truthy(current_user))
            hide();
        if (!Dm.truthy(M.client) || !Dm.truthy(anchor))
            return;
        current_user = M.client;
        //Blank
        // menu_holder = image('icons/effects/effects.dmi', anchor, "nothing", RADIAL_BACKGROUND_LAYER) at the offsets,
        // ABOVE_HUD_PLANE, KEEP_APART|RESET_ALPHA|RESET_COLOR|RESET_TRANSFORM, vis_contents the slices and the close button.
        // DEVIATION (DV40): no holder image: the client draws the menu at its crosshair, so the anchor's place and the offsets
        // are not used; the menu itself is what the client is shown.
        // current_user.images += menu_holder
        Dm.append(ref current_user.dm_radial_menus, this);
    }

    // tg: code/_onclick/hud/screen_objects/radial.dm:364
    public virtual void hide()
    {
        if (Dm.truthy(current_user))
            Dm.remove(ref current_user.dm_radial_menus, this); // current_user.images -= menu_holder (DV40)
    }

    // tg: code/_onclick/hud/screen_objects/radial.dm:368
    public virtual async DmTask wait(atom user = null, atom anchor = null, float? require_near = null)
    {
        require_near ??= FALSE;
        // BYOND's del of a client nulls every reference to it (a disconnect): a deleted current_user is null here.
        while (Dm.truthy(current_user) && current_user.gc_destroyed == null && !Dm.truthy(finished) && !Dm.truthy(selected_choice))
        {
            if (Dm.truthy(require_near) && !in_range(anchor, user))
                return;
            if (Dm.truthy(custom_check_callback) && next_check < world.time)
            {
                if (!Dm.truthy(await custom_check_callback.Invoke()))
                    return;
                else
                    next_check = world.time + check_delay;
            }
            await stoplag(1);
        }
    }

    // tg: code/_onclick/hud/screen_objects/radial.dm:379
    public virtual void remove_menu()
    {
        if (!Dm.truthy(Dm.bitand(button_animation_flags, BUTTON_FADE_OUT)))
        {
            qdel(this);
            return;
        }
        // for(var/atom/movable/element as anything in elements) animate(element, alpha = 0, time = 0.15 SECONDS)
        // DEVIATION (DV40): the client fades its slices out once the menu is answered.
        // QDEL_IN(src, 0.5 SECONDS) (__DEFINES/qdel.dm:59), inline (rules 11.1 (f))
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), this), 0.5f * SECONDS, file: "code/_onclick/hud/screen_objects/radial.dm", line: 385);
    }

    // tg: code/_onclick/hud/screen_objects/radial.dm:387
    public override object Destroy(float? force = null)
    {
        object dot = null;
        Reset();
        hide();
        custom_check_callback = null;
        dot = base.Destroy(force);
        return dot;
    }
}

public static partial class GlobalProcs
{
    /*
        Presents radial menu to user anchored to anchor (or user if the anchor is currently in users screen)
        Choices should be a list where list keys are movables or text used for element names and return value
        and list values are movables/icons/images used for element icons
    */
    // tg: code/_onclick/hud/screen_objects/radial.dm:398
    public static async DmTask<object> show_radial_menu(mob user = null, atom anchor = null, DmList choices = null, object uniqueid = null, object radius = null, datum_callback custom_check = null, float? require_near = null, float? tooltips = null, float? no_repeat_close = null, string radial_slice_icon = null, float? autopick_single_option = null, float? button_animation_flags = null, float? click_on_hover = null, float? user_space = null, float? check_delay = null, float? display_close_button = null, object radial_menu_offset = null)
    {
        require_near ??= FALSE;
        tooltips ??= FALSE;
        no_repeat_close ??= FALSE;
        radial_slice_icon ??= "radial_slice";
        autopick_single_option ??= TRUE;
        button_animation_flags ??= BUTTON_SLIDE_IN;
        click_on_hover ??= FALSE;
        user_space ??= FALSE;
        check_delay ??= DEFAULT_CHECK_DELAY;
        display_close_button ??= TRUE;
        radial_menu_offset ??= Dm.list(0, 0);
        if (!Dm.truthy(user) || !Dm.truthy(anchor) || !Dm.truthy(Dm.length(choices)))
            return null;

        if (Dm.length(choices) == 1 && Dm.truthy(autopick_single_option))
            return choices[1f];

        if (!Dm.truthy(uniqueid))
            uniqueid = Dm.text("defmenu_[]_[]", REF(user), REF(anchor));

        if (Dm.truthy(GLOB.radial_menus[uniqueid]))
        {
            if (!Dm.truthy(no_repeat_close))
            {
                datum_radial_menu repeated = GLOB.radial_menus[uniqueid] as datum_radial_menu;
                repeated.finished = TRUE;
            }
            return null;
        }

        datum_radial_menu menu = Dm.@new<datum_radial_menu>(display_close_button);
        menu.button_animation_flags = button_animation_flags.Value;
        menu.check_delay = check_delay.Value;
        GLOB.radial_menus[uniqueid] = menu;
        if (Dm.truthy(radius))
            menu.radius = Dm.num(radius);
        if (istype(custom_check, typeof(datum_callback)))
            menu.custom_check_callback = custom_check;
        menu.anchor = Dm.truthy(user_space) ? user : anchor;
        menu.radial_slice_icon = radial_slice_icon;
        menu.check_screen_border(user); //Do what's needed to make it look good near borders or on hud
        menu.set_choices(choices, tooltips, click_on_hover);
        float offset_x = 0;
        float offset_y = 0;
        if (Dm.truthy(user_space))
        {
            turf user_turf = get_turf(user);
            turf anchor_turf = get_turf(anchor);
            offset_x = (anchor_turf.x - user_turf.x) * ICON_SIZE_X + Dm.num(Dm.get_var(anchor, "pixel_x")) - Dm.num(Dm.get_var(user, "pixel_x"));
            offset_y = (anchor_turf.y - user_turf.y) * ICON_SIZE_Y + Dm.num(Dm.get_var(anchor, "pixel_y")) - Dm.num(Dm.get_var(user, "pixel_y"));
        }
        offset_x += Dm.num(((DmList)radial_menu_offset)[1f]);
        offset_y += Dm.num(((DmList)radial_menu_offset)[2f]);
        menu.show_to(user, offset_x, offset_y);
        await menu.wait(user, anchor, require_near);
        object answer = menu.selected_choice;
        menu.remove_menu();
        Dm.remove(ref GLOB.radial_menus, uniqueid);
        if (Dm.truthy(require_near) && !in_range(anchor, user))
            return null;
        if (istype(custom_check, typeof(datum_callback)))
        {
            if (!Dm.truthy(await custom_check.Invoke()))
                return null;
        }
        return answer;
    }

    // tg: code/__HELPERS/turfs.dm:116
    public static object get_atom_on_turf(atom_movable something_in_turf = null, object stop_type = null, float? return_any = null)
    {
        return_any ??= FALSE;
        if (!istype(something_in_turf, typeof(atom_movable)))
            throw Dm.CRASH(Dm.text("get_atom_on_turf was not passed an /atom/movable! Got []", something_in_turf == null ? "null" : Dm.text("type: []", Dm.get_var(something_in_turf, "type"))), "code/__HELPERS/turfs.dm", 118);

        atom_movable topmost_thing = something_in_turf;

        while (Dm.truthy(topmost_thing?.loc) && !isturf(topmost_thing.loc))
        {
            topmost_thing = topmost_thing.loc as atom_movable;
            if (Dm.truthy(stop_type) && istype(topmost_thing, (Type)stop_type))
                return topmost_thing;
        }

        if (!Dm.truthy(stop_type) || Dm.truthy(return_any))
            return topmost_thing;
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/radial.dm:444
public partial class datum_radial_menu_choice : datum
{
    // tg: code/_onclick/hud/screen_objects/radial.dm:448
    public object image; // an /image; the generator typed it as a Type

    // tg: code/_onclick/hud/screen_objects/radial.dm:459
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        if (image is datum image_datum)
            qdel(image_datum);
        image = null;
        return dot;
    }
}
