// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/paint.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI2 (D326): the paint cans, the adaptive paint (the cyborg's) and the paint remover, whole. The var blocks are generated (D111).

//NEVER USE THIS IT SUX -PETETHEGOAT
//IT SUCKS A BIT LESS -GIACOM

// tg: code/game/objects/items/paint.dm:4
public partial class obj_item_paint : obj_item
{
    // tg: code/game/objects/items/paint.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_falling_hazard), Dm.named("damage", 20), Dm.named("wound_bonus", 5), Dm.named("hardhat_safety", TRUE), Dm.named("crushes", FALSE))); // You ever watched home alone?
        return dot;
    }

    // tg: code/game/objects/items/paint.dm:115
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isturf(interacting_with) || isspaceturf(interacting_with))
            return NONE;
        if (Dm.num(paintleft) <= 0)
            return NONE;
        paintleft = Dm.num(paintleft) - 1;
        string color_type = SATURATION_MULTIPLY;
        if (Dm.truthy(modifiers?[RIGHT_CLICK]))
            color_type = SATURATION_OVERRIDE;
        await interacting_with.add_atom_colour(color_transition_filter(paint_color, color_type), WASHABLE_COLOUR_PRIORITY);
        if (Dm.num(paintleft) <= 0)
            icon_state = "paint_empty";
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/objects/items/paint.dm:58
public partial class obj_item_paint_anycolor : obj_item_paint
{
    // tg: code/game/objects/items/paint.dm:66
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.num(paintleft) <= 0)
        {
            balloon_alert(user, I18n.T("no paint left!"));
            return null; // Don't do any of the following because there's no paint left to be able to change the color of
        }
        DmList possible_colors = Dm.list(
            Dm.kv("black", image(icon: icon, icon_state: "paint_black")),
            Dm.kv("blue", image(icon: icon, icon_state: "paint_blue")),
            Dm.kv("green", image(icon: icon, icon_state: "paint_green")),
            Dm.kv("red", image(icon: icon, icon_state: "paint_red")),
            Dm.kv("violet", image(icon: icon, icon_state: "paint_violet")),
            Dm.kv("white", image(icon: icon, icon_state: "paint_white")),
            Dm.kv("yellow", image(icon: icon, icon_state: "paint_yellow")));
        object picked_color = await show_radial_menu(user, this, possible_colors, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), radius: 38, require_near: TRUE);
        if (Dm.eq(picked_color, "black"))
            paint_color = COLOR_ALMOST_BLACK;
        else if (Dm.eq(picked_color, "blue"))
            paint_color = COLOR_BLUE;
        else if (Dm.eq(picked_color, "green"))
            paint_color = COLOR_VIBRANT_LIME;
        else if (Dm.eq(picked_color, "red"))
            paint_color = COLOR_RED;
        else if (Dm.eq(picked_color, "violet"))
            paint_color = COLOR_MAGENTA;
        else if (Dm.eq(picked_color, "white"))
            paint_color = COLOR_WHITE;
        else if (Dm.eq(picked_color, "yellow"))
            paint_color = COLOR_YELLOW;
        else
            return null;
        icon_state = Dm.text("paint_[]", picked_color);
        add_fingerprint(user);
        return null;
    }

    /**
     * Checks if we are allowed to interact with a radial menu
     *
     * Arguments:
     * * user The mob interacting with the menu
     */
    // tg: code/game/objects/items/paint.dm:106
    public virtual object check_menu(mob user = null)
    {
        if (user == null)
            return FALSE;
        if (!Dm.truthy(user.is_holding(this)))
            return FALSE;
        if (Dm.truthy(user.incapacitated))
            return FALSE;
        return TRUE;
    }
}

// tg: code/game/objects/items/paint.dm:129
public partial class obj_item_paint_paint_remover : obj_item_paint
{
    // tg: code/game/objects/items/paint.dm:136
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isturf(interacting_with) || !isobj(interacting_with))
            return NONE;
        if (!Dm.eq(interacting_with.color, Dm.initial(interacting_with, (atom p) => p.color)))
        {
            await interacting_with.remove_atom_colour(WASHABLE_COLOUR_PRIORITY);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }
}
