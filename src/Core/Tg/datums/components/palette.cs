// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/palette.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI4d (D326): the whole file (the paint palette's and the spray can's colours). The var block is generated.

/**
 * A component that stores colors for use by painting-related items like spraycans or palettes
 * which can be accessed through a radial menu by right clicking the item while it's held by the user mob.
 * Right-clicking a color will open a color input prompt to edit it. Left clicking will instead select it
 * and call set_painting_tool_color() on the parent for more specific object behavior.
 */
// tg: code/datums/components/palette.dm:7
public partial class datum_component_palette : datum_component
{
    // tg: code/datums/components/palette.dm:24
    [DmParams("max_colors", "selected_color")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object max_colors = Dm.arg(__extra, 0);
        object selected_color = Dm.arg(__extra, 1);
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        if (!isnum(max_colors) || Dm.num(max_colors) < 1) /// This component means nothing if there's no space for colors
        {
            stack_trace("palette component initialized without a proper value for the max_colors arg", "code/datums/components/palette.dm", 29);
            return COMPONENT_INCOMPATIBLE;
        }

        this.max_colors = max_colors;
        this.selected_color = Dm.truthy(selected_color) ? selected_color : "#ffffff";

        RegisterSignal(parent, COMSIG_ITEM_ATTACK_SELF_SECONDARY, nameof(on_attack_self_secondary));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_PAINTING_TOOL_SET_COLOR, nameof(on_painting_tool_set_color));
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/datums/components/palette.dm:39
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref color_picker_menu);
        QDEL_LIST(menu_choices);
        UnregisterSignal(parent, Dm.list(COMSIG_ITEM_ATTACK_SELF_SECONDARY, COMSIG_ATOM_EXAMINE,
            COMSIG_ITEM_DROPPED, COMSIG_PAINTING_TOOL_SET_COLOR, COMSIG_PAINTING_TOOL_GET_ADDITIONAL_DATA));
        return base.Destroy(force);
    }

    // tg: code/datums/components/palette.dm:46
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        examine_list.Add(span_notice(I18n.T("<b>Right-Click</b> this item while it's in your active hand to open/close its color picker menu.")));
        examine_list.Add(span_notice(I18n.T("In the color picker, <b>Left-Click</b> a color button to pick it or <b>Right-Click</b> to remove it.")));
    }

    // tg: code/datums/components/palette.dm:52
    public virtual object on_attack_self_secondary(datum source = null, mob user = null)
    {
        if (color_picker_menu == null)
            Waiting.Detach(open_radial_menu(user));
        else
            close_radial_menu();

        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/datums/components/palette.dm:61
    public virtual async DmTask open_radial_menu(mob user = null)
    {
        DmList choices = build_radial_list();

        color_picker_menu = show_radial_menu_persistent(user, parent as atom, choices, select_proc: Dm.@new<datum_callback>(this, nameof(choice_selected), user), tooltips: TRUE, radial_slice_icon: "palette_bg");

        RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(close_radial_menu));
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/palette.dm:68
    public virtual DmList build_radial_list()
    {
        DmList radial_list = Dm.list();
        float color_count = Dm.length(colors);
        LAZYSETLEN(ref menu_choices, Math.Max(color_count + 1, 0));
        if (color_count < Dm.num(max_colors) && !Dm.@in(selected_color, colors))
        {
            var add_option = peek(menu_choices) as datum_radial_menu_choice;
            if (add_option == null)
            {
                add_option = Dm.@new<datum_radial_menu_choice>();
                menu_choices[color_count + 1] = add_option;
            }
            image element = image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "palette_element");
            element.color = selected_color;
            image plus = image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "palette_add");
            plus.appearance_flags = Dm.bitor(Dm.initial(typeof(image), (image i) => i.appearance_flags), RESET_COLOR);
            Dm.combine(ref element.overlays, plus); // /image/proc/add_overlay(x) is `overlays |= x` (overlays.dm:113)
            add_option.image = element;
            add_option.name = Dm.text("Add Color ([])", selected_color);
            radial_list["add"] = add_option;
        }
        for (int index = 1; index <= Dm.length(colors); index++)
        {
            object hexcolor = colors[index];
            var option = menu_choices[index] as datum_radial_menu_choice;
            if (option == null)
            {
                option = Dm.@new<datum_radial_menu_choice>();
                menu_choices[index] = option;
            }
            string icon_state_to_use = Dm.eq(hexcolor, selected_color) ? "palette_selected" : "palette_element";
            image element = image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: icon_state_to_use);
            element.color = hexcolor;
            option.image = element;
            // We want only the name/tooltip to show the hexcolor to avoid having multiple choices with same ids (identical colors).
            option.name = hexcolor;
            radial_list[Dm.text("[]", index)] = option;
        }
        return radial_list;
    }

    // tg: code/datums/components/palette.dm:101
    public virtual void close_radial_menu(params object[] __extra)
    {
        QDEL_NULL(ref color_picker_menu);
        UnregisterSignal(parent, COMSIG_ITEM_DROPPED);
    }

    // tg: code/datums/components/palette.dm:107
    public virtual void update_radial_list()
    {
        if (QDELETED(color_picker_menu))
            return;
        DmList choices = build_radial_list();
        color_picker_menu.change_choices(choices, tooltips: TRUE, keep_same_page: TRUE);
    }

    // tg: code/datums/components/palette.dm:113
    public virtual void choice_selected(mob user = null, object choice = null, object @params = null)
    {
        if (!Dm.truthy(choice) || Dm.truthy(user.incapacitated)) // center button or incapacitated but still holding on the item.
        {
            close_radial_menu();
            return;
        }
        object is_right_clicking = LAZYACCESS(params2list(@params), RIGHT_CLICK);
        if (Dm.eq(choice, "add"))
        {
            if (Dm.length(colors) < Dm.num(max_colors))
            {
                colors.Add(selected_color);
                update_radial_list();
            }
            return;
        }
        float index = Dm.num(text2num(choice));
        if (Dm.truthy(is_right_clicking))
        {
            colors.Cut(index, index + 1);
            update_radial_list();
        }
        else
        {
            var parent_item = parent as obj_item;
            parent_item.set_painting_tool_color(colors[index]); // This will send a signal back to us. See below.
        }
    }

    // tg: code/datums/components/palette.dm:132
    public virtual void on_painting_tool_set_color(datum source = null, object chosen_color = null)
    {
        selected_color = chosen_color;
        update_radial_list();
    }
}
