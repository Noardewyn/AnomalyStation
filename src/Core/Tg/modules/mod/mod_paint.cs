// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/mod_paint.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO1 (D333): the file whole. The MOD paint kit: a left click on an inactive control unit picks one of its theme's skins from
// a radial menu; a right click opens the MODpaint window, a preview of the suit (a map view) recoloured live by a colour matrix
// whose channel sums and overall sum must stay within bounds before it is applied. The skin applier gives an inactive suit of the
// right theme one skin and is used up; the honkerative applier also installs a waddle module (modules_service.dm, gaps MO2). The var
// blocks are generated (D111).

// tg: code/modules/mod/mod_paint.dm:8
public partial class obj_item_mod_paint : obj_item_mod
{
    // tg: code/modules/mod/mod_paint.dm:1-6 (file-local, #undef at :135-140)
    private const float MODPAINT_MAX_COLOR_VALUE = 1.25f;
    private const float MODPAINT_MIN_COLOR_VALUE = 0;
    private const float MODPAINT_MAX_SECTION_COLORS = 2;
    private const float MODPAINT_MIN_SECTION_COLORS = 0.25f;
    private const float MODPAINT_MAX_OVERALL_COLORS = 4;
    private const float MODPAINT_MIN_OVERALL_COLORS = 1.5f;

    // COLOR_MATRIX_IDENTITY (__DEFINES/matrices.dm:5, a list literal: a new list at each use)
    private static DmList COLOR_MATRIX_IDENTITY => Dm.list(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0);

    // tg: code/modules/mod/mod_paint.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        current_color = COLOR_MATRIX_IDENTITY;
        return dot;
    }

    // tg: code/modules/mod/mod_paint.dm:22
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("<b>Left-click</b> a MODsuit to change skin.")));
        Dm.append(ref dot, span_notice(I18n.T("<b>Right-click</b> a MODsuit to recolor.")));
        return dot;
    }

    // tg: code/modules/mod/mod_paint.dm:27
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (editing_mod == null)
            return null;
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "MODpaint", name);
            await ui.open();
            proxy_view.display_to(user, ui.window);
        }
        return null;
    }

    // tg: code/modules/mod/mod_paint.dm:36
    public override object ui_host(mob user = null)
    {
        return editing_mod;
    }

    // tg: code/modules/mod/mod_paint.dm:39
    public override void ui_close(mob user = null)
    {
        base.ui_close(user);
        editing_mod = null;
        QDEL_NULL(ref proxy_view);
        current_color = COLOR_MATRIX_IDENTITY;
    }

    // tg: code/modules/mod/mod_paint.dm:45
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (Dm.truthy(check_menu(editing_mod, user)))
            return await base.ui_status(user, state);
        return UI_CLOSE;
    }

    // tg: code/modules/mod/mod_paint.dm:50
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["mapRef"] = proxy_view.assigned_map;
        return data;
    }

    // tg: code/modules/mod/mod_paint.dm:55
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["currentColor"] = current_color;
        return data;
    }

    // tg: code/modules/mod/mod_paint.dm:60
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.eq(action, "transition_color"))
        {
            current_color = @params["color"] as DmList;
            animate(proxy_view, Dm.named("time", 0.5f * SECONDS), Dm.named("color", current_color));
        }
        else if (Dm.eq(action, "confirm"))
        {
            if (!Dm.eq(length(current_color), 20)) //20 is the length of a matrix identity list
                return dot;
            foreach (object color_value in Dm.each(current_color))
            {
                if (isnum(color_value))
                    continue;
                return dot;
            }
            float total_color_value = 0;
            DmList total_colors = current_color.Copy();
            total_colors.Cut(13, Dm.num(length(total_colors))); // 13 to 20 are just a and c, dont want to count them
            float red_value = Dm.num(current_color[1]) + Dm.num(current_color[5]) + Dm.num(current_color[9]); //rr + gr + br
            float green_value = Dm.num(current_color[2]) + Dm.num(current_color[6]) + Dm.num(current_color[10]); //rg + gg + bg
            float blue_value = Dm.num(current_color[3]) + Dm.num(current_color[7]) + Dm.num(current_color[11]); //rb + gb + bb
            if (red_value > MODPAINT_MAX_SECTION_COLORS)
            {
                balloon_alert(usr, I18n.T("total red too high! ([]%/[]%)", red_value * 100, MODPAINT_MAX_SECTION_COLORS * 100));
                return dot;
            }
            else if (red_value < MODPAINT_MIN_SECTION_COLORS)
            {
                balloon_alert(usr, I18n.T("total red too low! ([]%/[]%)", red_value * 100, MODPAINT_MIN_SECTION_COLORS * 100));
                return dot;
            }
            if (green_value > MODPAINT_MAX_SECTION_COLORS)
            {
                balloon_alert(usr, I18n.T("total green too high! ([]%/[]%)", green_value * 100, MODPAINT_MAX_SECTION_COLORS * 100));
                return dot;
            }
            else if (green_value < MODPAINT_MIN_SECTION_COLORS)
            {
                balloon_alert(usr, I18n.T("total green too low! ([]%/[]%)", green_value * 100, MODPAINT_MIN_SECTION_COLORS * 100));
                return dot;
            }
            if (blue_value > MODPAINT_MAX_SECTION_COLORS)
            {
                balloon_alert(usr, I18n.T("total blue too high! ([]%/[]%)", blue_value * 100, MODPAINT_MAX_SECTION_COLORS * 100));
                return dot;
            }
            else if (blue_value < MODPAINT_MIN_SECTION_COLORS)
            {
                balloon_alert(usr, I18n.T("total blue too low! ([]%/[]%)", blue_value * 100, MODPAINT_MIN_SECTION_COLORS * 100));
                return dot;
            }
            foreach (object color_value in Dm.each(total_colors))
            {
                total_color_value += Dm.num(color_value);
                if (Dm.num(color_value) > MODPAINT_MAX_COLOR_VALUE)
                {
                    balloon_alert(usr, I18n.T("one of colors too high! ([]%/[]%", Dm.num(color_value) * 100, MODPAINT_MAX_COLOR_VALUE * 100));
                    return dot;
                }
                else if (Dm.num(color_value) < MODPAINT_MIN_COLOR_VALUE)
                {
                    balloon_alert(usr, I18n.T("one of colors too low! ([]%/[]%", Dm.num(color_value) * 100, MODPAINT_MIN_COLOR_VALUE * 100));
                    return dot;
                }
            }
            if (total_color_value > MODPAINT_MAX_OVERALL_COLORS)
            {
                balloon_alert(usr, I18n.T("total colors too high! ([]%/[]%)", total_color_value * 100, MODPAINT_MAX_OVERALL_COLORS * 100));
                return dot;
            }
            else if (total_color_value < MODPAINT_MIN_OVERALL_COLORS)
            {
                balloon_alert(usr, I18n.T("total colors too low! ([]%/[]%)", total_color_value * 100, MODPAINT_MIN_OVERALL_COLORS * 100));
                return dot;
            }
            await editing_mod.set_mod_color(current_color);
            SStgui.close_uis(this);
        }
        return dot;
    }

    // tg: code/modules/mod/mod_paint.dm:116
    public virtual async DmTask paint_skin(obj_item_mod_control mod = null, mob user = null)
    {
        datum_mod_theme theme = mod.theme as datum_mod_theme;
        if (Dm.num(length(theme.variants)) <= 1)
        {
            balloon_alert(user, I18n.T("no alternate skins!"));
            return;
        }
        DmList skins = Dm.list();
        foreach (object mod_skin_name in Dm.each(theme.variants))
        {
            DmList mod_skin = theme.variants[mod_skin_name] as DmList;
            skins[mod_skin_name] = Dm.image(icon: mod_skin[MOD_ICON_OVERRIDE] is var or__1 && Dm.truthy(or__1) ? or__1 : mod.icon, icon_state: Dm.text("[]-control", mod_skin_name));
        }
        object pick = await show_radial_menu(user, mod, skins, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), mod, user), require_near: TRUE);
        if (!Dm.truthy(pick))
        {
            balloon_alert(user, I18n.T("no skin picked!"));
            return;
        }
        await theme.set_skin(mod, pick);
    }

    // tg: code/modules/mod/mod_paint.dm:130
    public virtual object check_menu(obj_item_mod_control mod = null, mob user = null)
    {
        if (Dm.truthy(user.incapacitated) || !Dm.truthy(user.is_holding(this)) || mod == null || Dm.truthy(mod.active) || Dm.truthy(mod.activating))
            return FALSE;
        return TRUE;
    }
}

// tg: code/modules/mod/mod_paint.dm:142
public partial class obj_item_mod_skin_applier : obj_item_mod
{
    // tg: code/modules/mod/mod_paint.dm:149
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = I18n.T("\\improper MOD [] skin applier", skin);
        return dot;
    }

    // tg: code/modules/mod/mod_paint.dm:153
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        atom attacked_atom = interacting_with;
        if (!istype(attacked_atom, typeof(obj_item_mod_control)))
            return NONE;
        obj_item_mod_control mod = attacked_atom as obj_item_mod_control;
        if (Dm.truthy(mod.active) || Dm.truthy(mod.activating))
        {
            balloon_alert(user, I18n.T("unit active!"));
            return ITEM_INTERACT_BLOCKING;
        }
        datum_mod_theme theme = mod.theme as datum_mod_theme;
        if (!Dm.@in(skin, theme.variants))
        {
            balloon_alert(user, I18n.T("wrong theme for skin!"));
            return ITEM_INTERACT_BLOCKING;
        }
        await theme.set_skin(mod, skin);
        balloon_alert(user, I18n.T("skin applied"));
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/mod/mod_paint.dm:168
public partial class obj_item_mod_skin_applier_honkerative : obj_item_mod_skin_applier
{
    // tg: code/modules/mod/mod_paint.dm:172
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        obj_item_mod_control controlunit = interacting_with as obj_item_mod_control;
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (!Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_SUCCESS)))
            return dot;
        await controlunit.install(Dm.@new<obj_item_mod_module_waddle>(get_turf(user)), user);
        return dot;
    }
}
