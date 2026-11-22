// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/screen_objects/picture_in_picture.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI4, D326): the picture-in-picture screen object, the AI's multicamera windows. The server keeps tg's screen
// object, its turfs in vis_contents and its buttons; the 3D client draws each one on the player's screen as a floating window with a second
// 3D view of those turfs (as the camera console's map view, M10 slice D), and the server's interest takes them as a further eye. The var
// block is generated (D111).

// tg: code/_onclick/hud/screen_objects/picture_in_picture.dm:1
public partial class atom_movable_screen_movable_pic_in_pic : atom_movable_screen_movable
{
    // tg: code/_onclick/hud/screen_objects/picture_in_picture.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        make_backgrounds();
        RegisterSignal(SSmapping, COMSIG_PLANE_OFFSET_INCREASE, nameof(multiz_offset_increase));
        multiz_offset_increase(SSmapping);
        popup_screen = Dm.@new<atom_movable_screen_map_view>();
        popup_screen.generate_view(Dm.text("camera-[]_map", REF(this)));
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/picture_in_picture.dm:27
    public virtual void multiz_offset_increase(datum source = null)
    {
        plane = GET_NEW_PLANE(Dm.num(Dm.initial(GetType(), (atom a) => a.plane)), SSmapping.max_plane_offset); // SET_PLANE_W_SCALAR
    }

    // tg: code/_onclick/hud/screen_objects/picture_in_picture.dm:31
    public override object Destroy(float? force = null)
    {
        foreach (object C in Dm.each(shown_to.Copy()))
            unshow_to(C as client);
        QDEL_NULL(ref button_x);
        QDEL_NULL(ref button_shrink);
        QDEL_NULL(ref button_expand);
        QDEL_NULL(ref button_pop);
        QDEL_NULL(ref popup_screen);
        return base.Destroy(force);
    }

    // tg: code/_onclick/hud/screen_objects/picture_in_picture.dm:41
    public override async DmTask component_click(atom_movable_screen_component_button component = null, object @params = null)
    {
        if (component == button_x)
        {
            (usr as mob)?.client?.close_popup(Dm.text("camera-[]", REF(this)));
            qdel(this);
        }
        else if (component == button_expand)
            set_view_size(width + 1, height + 1);
        else if (component == button_shrink)
            set_view_size(width - 1, height - 1);
        else if (component == button_pop)
            pop_to_screen();
        await DmTask.CompletedTask;
    }

    // tg: code/_onclick/hud/screen_objects/picture_in_picture.dm:52
    public virtual void make_backgrounds()
    {
        standard_background = Dm.@new<mutable_appearance>();
        standard_background.icon = Dm.resource("icons/hud/pic_in_pic.dmi");
        standard_background.icon_state = "background";
        standard_background.layer = LOWER_FLOOR_LAYER;
        standard_background.appearance_flags = PIXEL_SCALE;
    }

    // tg: code/_onclick/hud/screen_objects/picture_in_picture.dm:59
    public virtual void add_buttons()
    {
        mutable_appearance move_tab = TgState.Current.atom_movable_screen_movable_pic_in_pic__move_tab; // var/static/mutable_appearance/move_tab
        if (move_tab == null)
        {
            move_tab = Dm.@new<mutable_appearance>();
            //all these properties are always the same, and since adding something to the overlay
            //list makes a copy, there is no reason to make a new one each call
            move_tab.icon = Dm.resource("icons/hud/pic_in_pic.dmi");
            move_tab.icon_state = "move";
            move_tab.plane = HUD_PLANE;
            TgState.Current.atom_movable_screen_movable_pic_in_pic__move_tab = move_tab;
        }
        matrix M = matrix();
        M.Translate(0, (height + 0.25f) * ICON_SIZE_Y);
        move_tab.transform = M;
        add_overlay(move_tab);

        if (button_x == null)
        {
            button_x = Dm.@new<atom_movable_screen_component_button>(null, this);
            mutable_appearance MA = Dm.@new<mutable_appearance>();
            MA.name = "close";
            MA.icon = Dm.resource("icons/hud/pic_in_pic.dmi");
            MA.icon_state = "x";
            MA.plane = HUD_PLANE;
            button_x.appearance = MA;
        }
        M = matrix();
        M.Translate((max(4, width) - 0.75f) * ICON_SIZE_X, (height + 0.25f) * ICON_SIZE_Y);
        button_x.transform = M;
        DmList vis_button_x = vis_contents; // a list: changed in place
        Dm.append(ref vis_button_x, button_x);

        if (button_expand == null)
        {
            button_expand = Dm.@new<atom_movable_screen_component_button>(null, this);
            mutable_appearance MA = Dm.@new<mutable_appearance>();
            MA.name = "expand";
            MA.icon = Dm.resource("icons/hud/pic_in_pic.dmi");
            MA.icon_state = "expand";
            MA.plane = HUD_PLANE;
            button_expand.appearance = MA;
        }
        M = matrix();
        M.Translate(ICON_SIZE_X, (height + 0.25f) * ICON_SIZE_Y);
        button_expand.transform = M;
        DmList vis_button_expand = vis_contents; // a list: changed in place
        Dm.append(ref vis_button_expand, button_expand);

        if (button_shrink == null)
        {
            button_shrink = Dm.@new<atom_movable_screen_component_button>(null, this);
            mutable_appearance MA = Dm.@new<mutable_appearance>();
            MA.name = "shrink";
            MA.icon = Dm.resource("icons/hud/pic_in_pic.dmi");
            MA.icon_state = "shrink";
            MA.plane = HUD_PLANE;
            button_shrink.appearance = MA;
        }
        M = matrix();
        M.Translate(2 * ICON_SIZE_X, (height + 0.25f) * ICON_SIZE_Y);
        button_shrink.transform = M;
        DmList vis_button_shrink = vis_contents; // a list: changed in place
        Dm.append(ref vis_button_shrink, button_shrink);

        if (button_pop == null)
        {
            button_pop = Dm.@new<atom_movable_screen_component_button>(null, this);
            mutable_appearance MA = Dm.@new<mutable_appearance>();
            MA.name = "pop";
            MA.icon = Dm.resource("icons/hud/pic_in_pic.dmi");
            MA.icon_state = "pop";
            MA.plane = HUD_PLANE;
            button_pop.appearance = MA;
        }
        M = matrix();
        M.Translate((max(4, width) - 0.75f) * ICON_SIZE_X, (height + 0.25f) * ICON_SIZE_Y + 16);
        button_pop.transform = M;
        DmList vis_button_pop = vis_contents; // a list: changed in place
        Dm.append(ref vis_button_pop, button_pop);
    }

    // tg: code/_onclick/hud/screen_objects/picture_in_picture.dm:126
    public virtual void add_background()
    {
        if (width > 0 && height > 0)
        {
            matrix M = matrix();
            M.Scale(width + 0.5f, height + 0.5f);
            M.Translate((width - 1) / 2 * ICON_SIZE_X, (height - 1) / 2 * ICON_SIZE_Y);
            standard_background.transform = M;
            add_overlay(standard_background);
        }
    }

    // maximum number of dimensions is 10

    // tg: code/_onclick/hud/screen_objects/picture_in_picture.dm:136
    public virtual void set_view_size(float width = 0, float height = 0, float? do_refresh = null)
    {
        do_refresh ??= TRUE;
        width = clamp(width, 0, 10);
        height = clamp(height, 0, 10);
        this.width = width;
        this.height = height;

        y_off = (-height * ICON_SIZE_Y) - (ICON_SIZE_Y / 2);

        cut_overlays();
        add_background();
        add_buttons();
        if (Dm.truthy(do_refresh))
            refresh_view();
    }

    // tg: code/_onclick/hud/screen_objects/picture_in_picture.dm:150
    public virtual void set_view_center(atom target = null, float? do_refresh = null)
    {
        do_refresh ??= TRUE;
        center = target;
        if (Dm.truthy(do_refresh))
            refresh_view();
    }

    // tg: code/_onclick/hud/screen_objects/picture_in_picture.dm:155
    public virtual void refresh_view()
    {
        DmList shown = vis_contents; // a list: changed in place
        Dm.remove(ref shown, viewing_turfs);
        if (!Dm.truthy(width) || !Dm.truthy(height))
            return;
        viewing_turfs = get_visible_turfs();
        Dm.append(ref shown, viewing_turfs);
        if (popup_screen != null)
        {
            DmList popup_shown = popup_screen.vis_contents; // a list: changed in place
            popup_shown.Cut();
            Dm.append(ref popup_shown, viewing_turfs);
        }
    }

    // tg: code/_onclick/hud/screen_objects/picture_in_picture.dm:165
    public virtual DmList get_visible_turfs()
    {
        turf T = get_turf(center);
        if (T == null)
            return Dm.list();
        turf lowerleft = locate(max(1, T.x - round(width / 2)), max(1, T.y - round(height / 2)), T.z);
        turf upperright = locate(min(Dm.world.maxx, lowerleft.x + width - 1), min(Dm.world.maxy, lowerleft.y + height - 1), lowerleft.z);
        return block(lowerleft, upperright);
    }

    // tg: code/_onclick/hud/screen_objects/picture_in_picture.dm:172
    public virtual void show_to(client C = null)
    {
        if (C != null)
        {
            shown_to[C] = 1;
            DmList on_screen = C.screen; // a list: changed in place
            Dm.append(ref on_screen, this);
        }
    }

    // tg: code/_onclick/hud/screen_objects/picture_in_picture.dm:177
    public virtual void unshow_to(client C = null)
    {
        if (C != null)
        {
            Dm.remove(ref shown_to, C);
            DmList on_screen = C.screen; // a list: changed in place
            Dm.remove(ref on_screen, this);
        }
    }

    // tg: code/_onclick/hud/screen_objects/picture_in_picture.dm:182
    public virtual void pop_to_screen()
    {
        client user_client = (usr as mob)?.client;
        if (Dm.truthy(user_client.screen_maps[Dm.text("camera-[]_map", REF(this))]))
            return;
        user_client.setup_popup(Dm.text("camera-[]", REF(this)), width, height, 2, "1984");
        popup_screen.display_to(usr as mob);
        RegisterSignal(user_client, COMSIG_POPUP_CLEARED, nameof(on_popup_clear));
    }

    // tg: code/_onclick/hud/screen_objects/picture_in_picture.dm:189
    public virtual void on_popup_clear(client source = null, object window = null)
    {
        if (Dm.eq(window, Dm.text("camera-[]", REF(this))))
        {
            UnregisterSignal((usr as mob)?.client, COMSIG_POPUP_CLEARED);
            popup_screen.hide_from(usr as mob);
        }
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:1097
public partial class atom_movable_screen_component_button : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1101
    [DmParams("mapload", "parent")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        parent = Dm.arg<atom_movable_screen>(__extra, 0);
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1105  (tg's Click(params): BYOND's first argument, the location)
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (parent != null)
            await parent.component_click(this, location);
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/_onclick/hud/screen_objects/picture_in_picture.dm:60  var/static/mutable_appearance/move_tab
    internal mutable_appearance atom_movable_screen_movable_pic_in_pic__move_tab;
}
