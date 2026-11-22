// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/floor/light_floor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps G2 (D291): the whole file. A screwdriver turns the light floor off and on, a multitool picks its colour from tg's radial
// menu of ten, a broken one flickers or goes dark and is fixed with a light bulb, an EMP scrambles its colour.

// tg: code/game/turfs/open/floor/light_floor.dm:6
public partial class turf_open_floor_light : turf_open_floor
{
    // tg: code/game/turfs/open/floor/light_floor.dm:1-4  #define LIGHTFLOOR_FINE 0 .. LIGHTFLOOR_BROKEN 3 (#undef at the end)
    private const int LIGHTFLOOR_FINE = 0, LIGHTFLOOR_FLICKER = 1, LIGHTFLOOR_BREAKING = 2, LIGHTFLOOR_BROKEN = 3;

    ///list of colours to choose
    // tg: code/game/turfs/open/floor/light_floor.dm:17  var/static/list/coloredlights
    public static ref DmList coloredlights => ref TgState.Current.turf_open_floor_light__coloredlights;

    ///icons for radial menu
    // tg: code/game/turfs/open/floor/light_floor.dm:24  var/static/list/lighttile_designs
    public static ref DmList lighttile_designs => ref TgState.Current.turf_open_floor_light__lighttile_designs;

    // tg: code/game/turfs/open/floor/light_floor.dm:28
    public override object broken_states()
    {
        return Dm.list("light_broken");
    }

    // tg: code/game/turfs/open/floor/light_floor.dm:31
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("There's a <b>small crack</b> on the edge of it.")));
        Dm.append(ref dot, span_notice(I18n.T("Use a multitool on it to change colors.")));
        Dm.append(ref dot, span_notice(I18n.T("Use a screwdriver to turn it off or on.")));
        if (Dm.truthy(state)) ///check if broken
            Dm.append(ref dot, span_danger(I18n.T("The light bulb seems fried!")));
        return dot;
    }

    ///create radial menu
    // tg: code/game/turfs/open/floor/light_floor.dm:40
    public virtual void populate_lighttile_designs()
    {
        lighttile_designs = Dm.list(
            Dm.kv(LIGHT_COLOR_CYAN, image(icon: this.icon, icon_state: "light_on-1")),
            Dm.kv(COLOR_SOFT_RED, image(icon: this.icon, icon_state: "light_on-2")),
            Dm.kv(LIGHT_COLOR_ORANGE, image(icon: this.icon, icon_state: "light_on-3")),
            Dm.kv(LIGHT_COLOR_GREEN, image(icon: this.icon, icon_state: "light_on-4")),
            Dm.kv(LIGHT_COLOR_DIM_YELLOW, image(icon: this.icon, icon_state: "light_on-5")),
            Dm.kv(LIGHT_COLOR_DARK_BLUE, image(icon: this.icon, icon_state: "light_on-6")),
            Dm.kv(LIGHT_COLOR_LAVENDER, image(icon: this.icon, icon_state: "light_on-7")),
            Dm.kv(COLOR_WHITE, image(icon: this.icon, icon_state: "light_on-8")),
            Dm.kv(LIGHT_COLOR_SLIME_LAMP, image(icon: this.icon, icon_state: "light_on-9")),
            Dm.kv(LIGHT_COLOR_FIRE, image(icon: this.icon, icon_state: "light_on-10")));
    }

    // tg: code/game/turfs/open/floor/light_floor.dm:54
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        if (!Dm.truthy(Dm.length(lighttile_designs)))
            populate_lighttile_designs();
        return dot;
    }

    // tg: code/game/turfs/open/floor/light_floor.dm:60
    public override object break_tile()
    {
        base.break_tile();
        state = Dm.num(pick(LIGHTFLOOR_FLICKER, LIGHTFLOOR_BREAKING, LIGHTFLOOR_BROKEN)); /// pick a broken state
        update_appearance();
        return null;
    }

    // tg: code/game/turfs/open/floor/light_floor.dm:65
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);
        if (!Dm.truthy(on))
        {
            Waiting.NoSleep(set_light(0));
            return dot;
        }

        switch (Dm.num(state))
        {
            case LIGHTFLOOR_FINE:
                set_light_color(currentcolor);
                Waiting.NoSleep(set_light(5));
                light_range = 3;
                break;
            case LIGHTFLOOR_FLICKER:
                set_light_color(currentcolor);
                Waiting.NoSleep(set_light(3));
                light_range = 2;
                break;
            case LIGHTFLOOR_BREAKING:
                Waiting.NoSleep(set_light(1));
                break;
            case LIGHTFLOOR_BROKEN:
                Waiting.NoSleep(set_light(0));
                break;
        }
        return dot;
    }

    // tg: code/game/turfs/open/floor/light_floor.dm:85
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (!Dm.truthy(on))
        {
            icon_state = "light_off";
            return await base.update_icon_state(__extra);
        }

        switch (Dm.num(state))
        {
            case LIGHTFLOOR_FINE:
                if (Dm.truthy(cycle))
                {
                    if (istype(this, typeof(turf_open_floor_light_colour_cycle_dancefloor_a)))
                        icon_state = "light_on-dancefloor_A";
                    else if (istype(this, typeof(turf_open_floor_light_colour_cycle_dancefloor_b)))
                        icon_state = "light_on-dancefloor_B";
                    else
                        icon_state = "light_on-cycle_all";
                }
                else
                    icon_state = Dm.text("light_on-[]", LAZYFIND(coloredlights, currentcolor));
                break;
            case LIGHTFLOOR_FLICKER:
                icon_state = Dm.text("light_on_flicker-[]", LAZYFIND(coloredlights, currentcolor));
                break;
            case LIGHTFLOOR_BREAKING:
                icon_state = "light_on_broken";
                break;
            case LIGHTFLOOR_BROKEN:
                icon_state = "light_off";
                break;
        }
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/turfs/open/floor/light_floor.dm:110
    public override async DmTask<object> ChangeTurf(object path = null, DmList new_baseturfs = null, object flags = null)
    {
        await set_light(0);
        return await base.ChangeTurf(path, new_baseturfs, flags);
    }

    // tg: code/game/turfs/open/floor/light_floor.dm:114
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.screwdriver_act(user, tool);
        if (!Dm.truthy(can_modify_colour) && !Dm.truthy(cycle))
            return dot;
        on = Dm.truthy(on) ? FALSE : TRUE;
        update_appearance();
        return dot;
    }

    // tg: code/game/turfs/open/floor/light_floor.dm:121
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.multitool_act(user, tool);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(can_modify_colour))
            return FALSE;
        object choice = await show_radial_menu(user, this, lighttile_designs, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user, tool), radius: 36, require_near: TRUE);
        if (!Dm.truthy(choice))
            return FALSE;
        currentcolor = (string)choice;
        update_appearance();
        return dot;
    }

    // tg: code/game/turfs/open/floor/light_floor.dm:133
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(Dm.bitand(ITEM_INTERACT_ANY_BLOCKER, dot)))
            return dot;

        if (!istype(tool, typeof(obj_item_light_bulb))) //only for light tiles
            return dot;

        if (Dm.truthy(((obj_item_light_bulb)tool).status)) /// check if broken
        {
            to_chat(user, span_danger(I18n.T("The light bulb is broken!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(state))
        {
            to_chat(user, span_notice(I18n.T("The light bulb seems fine, no need to replace it.")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
            return ITEM_INTERACT_BLOCKING;

        qdel(tool);
        state = LIGHTFLOOR_FINE; //fixing it by bashing it with a light bulb, fun eh?
        update_appearance();
        to_chat(user, span_notice(I18n.T("You replace the light bulb.")));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/turfs/open/floor/light_floor.dm:158
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        currentcolor = (string)pick(coloredlights);
        if (Dm.eq(state, LIGHTFLOOR_BROKEN)) /// he's dead, jim
            return dot;
        if (prob(50))
            state = Dm.num(state) + 1;
        currentcolor = (string)pick(coloredlights);
        update_appearance();
        return dot;
    }

    /**
     * check_menu: Checks if we are allowed to interact with a radial menu
     *
     * Arguments:
     * * user The mob interacting with a menu
     * * multitool The multitool used to interact with a menu
     */
    // tg: code/game/turfs/open/floor/light_floor.dm:192
    public virtual object check_menu(mob_living user = null, obj_item multitool = null)
    {
        if (!istype(user, typeof(mob_living)))
            return FALSE;
        if (Dm.truthy(user.incapacitated))
            return FALSE;
        if (!Dm.truthy(multitool) || !Dm.truthy(user.is_holding(multitool)))
            return FALSE;
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/game/turfs/open/floor/light_floor.dm:17  /turf/open/floor/light's var/static/list/coloredlights
    internal DmList turf_open_floor_light__coloredlights = Dm.list(LIGHT_COLOR_CYAN, COLOR_SOFT_RED, LIGHT_COLOR_ORANGE, LIGHT_COLOR_GREEN, LIGHT_COLOR_DIM_YELLOW, LIGHT_COLOR_DARK_BLUE, LIGHT_COLOR_LAVENDER, COLOR_WHITE, LIGHT_COLOR_SLIME_LAMP, LIGHT_COLOR_FIRE);

    // tg: code/game/turfs/open/floor/light_floor.dm:24  /turf/open/floor/light's var/static/list/lighttile_designs
    internal DmList turf_open_floor_light__lighttile_designs;
}
