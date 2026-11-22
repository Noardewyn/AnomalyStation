// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/marker_beacons.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI2 (D326): the marker beacons, whole (the miners' and the mining cyborg's). The var blocks are generated (D111).

public partial class datum_controller_global_vars
{
    /*****************Marker Beacons**************************/
    // tg: code/modules/mining/equipment/marker_beacons.dm:2  GLOBAL_LIST_INIT(marker_beacon_colors, sort_list(list(...)))
    public DmList marker_beacon_colors;

    // tg: code/modules/mining/equipment/marker_beacons.dm:2  (GLOBAL_MANAGED(marker_beacon_colors, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmarker_beacon_colors()
    {
        marker_beacon_colors = sort_list(Dm.list(
            Dm.kv("Random", FALSE), //not a true color, will pick a random color
            Dm.kv("Burgundy", LIGHT_COLOR_FLARE),
            Dm.kv("Bronze", LIGHT_COLOR_ORANGE),
            Dm.kv("Yellow", LIGHT_COLOR_DIM_YELLOW),
            Dm.kv("Lime", LIGHT_COLOR_SLIME_LAMP),
            Dm.kv("Olive", LIGHT_COLOR_GREEN),
            Dm.kv("Jade", LIGHT_COLOR_BLUEGREEN),
            Dm.kv("Teal", LIGHT_COLOR_LIGHT_CYAN),
            Dm.kv("Cerulean", LIGHT_COLOR_BLUE),
            Dm.kv("Indigo", LIGHT_COLOR_DARK_BLUE),
            Dm.kv("Purple", LIGHT_COLOR_PURPLE),
            Dm.kv("Violet", LIGHT_COLOR_LAVENDER),
            Dm.kv("Fuchsia", LIGHT_COLOR_PINK))) as DmList;
        Dm.append(ref gvars_datum_init_order, "marker_beacon_colors");
    }
}

// tg: code/modules/mining/equipment/marker_beacons.dm:17
public partial class obj_item_stack_marker_beacon : obj_item_stack
{
    // tg: code/modules/mining/equipment/marker_beacons.dm:36
    [DmParams("mapload", "new_amount", "merge", "mat_override", "mat_amt")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        return dot;
    }

    // tg: code/modules/mining/equipment/marker_beacons.dm:40
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Use in-hand to place a [].\nAlt-click to select a color. Current color is [].", singular_name, picked_color)));
        return dot;
    }

    // tg: code/modules/mining/equipment/marker_beacons.dm:44
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", Dm.initial(this, (obj_item_stack_marker_beacon p) => p.icon_state), LOWER_TEXT(picked_color));
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/mining/equipment/marker_beacons.dm:48
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!isturf(user.loc))
        {
            to_chat(user, span_warning(I18n.T("You need more space to place a [] here.", singular_name)));
            return null;
        }
        if (Dm.locate<obj_structure_marker_beacon>(user.loc) != null)
        {
            to_chat(user, span_warning(I18n.T("There is already a [] here.", singular_name)));
            return null;
        }
        if (Dm.truthy(await use(1)))
        {
            to_chat(user, span_notice(I18n.T("You activate and anchor [] [] in place.", Dm.truthy(amount) ? "a" : "the", singular_name)));
            playsound(user, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
            obj_structure_marker_beacon M = Dm.@new<obj_structure_marker_beacon>(user.loc, picked_color);
            transfer_fingerprints_to(M);
        }
        return null;
    }

    // tg: code/modules/mining/equipment/marker_beacons.dm:61
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        object input_color = await tgui_input_list(user, I18n.T("Choose a color"), I18n.T("Beacon Color"), GLOB.marker_beacon_colors);
        if (input_color == null || !Dm.truthy(await user.can_perform_action(this)))
            return CLICK_ACTION_BLOCKING;
        picked_color = input_color as string;
        update_appearance();
        return CLICK_ACTION_SUCCESS;
    }
}

// tg: code/modules/mining/equipment/marker_beacons.dm:69
public partial class obj_structure_marker_beacon : obj_structure
{
    // tg: code/modules/mining/equipment/marker_beacons.dm:92
    [DmParams("mapload", "set_color")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object set_color = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(set_color))
            picked_color = set_color;
        update_appearance();
        return dot;
    }

    // tg: code/modules/mining/equipment/marker_beacons.dm:98
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        obj_item_stack_marker_beacon beacon = Dm.@new<obj_item_stack_marker_beacon>(loc);
        beacon.picked_color = picked_color as string;
        beacon.update_appearance();
    }

    // tg: code/modules/mining/equipment/marker_beacons.dm:103
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Alt-click to select a color. Current color is [].", picked_color)));
        return dot;
    }

    // tg: code/modules/mining/equipment/marker_beacons.dm:107
    public override object update_appearance(float? updates = null)
    {
        while (!Dm.truthy(picked_color) || !Dm.truthy(GLOB.marker_beacon_colors[picked_color]))
            picked_color = pick(GLOB.marker_beacon_colors);

        object dot = base.update_appearance(updates);
        Waiting.NoSleep(set_light(light_range, light_power, GLOB.marker_beacon_colors[picked_color]));
        return dot;
    }

    // tg: code/modules/mining/equipment/marker_beacons.dm:114
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]-on", icon_prefix, LOWER_TEXT(picked_color));
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/mining/equipment/marker_beacons.dm:118
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        to_chat(user, span_notice(I18n.T("You start picking [] up...", this)));
        if (Dm.truthy(await do_after(user, remove_speed, target: this)))
        {
            obj_item_stack_marker_beacon M = Dm.@new<obj_item_stack_marker_beacon>(loc);
            M.picked_color = picked_color as string;
            M.update_appearance();
            transfer_fingerprints_to(M);
            if (Dm.truthy(await user.put_in_hands(M, TRUE))) //delete the beacon if it fails
            {
                playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
                qdel(this); //otherwise delete us
            }
        }
        return dot;
    }

    // tg: code/modules/mining/equipment/marker_beacons.dm:132
    public override async DmTask<object> attack_tk(mob user = null)
    {
        return null;
    }

    // tg: code/modules/mining/equipment/marker_beacons.dm:135
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_stack_marker_beacon collection)
        {
            to_chat(user, span_notice(I18n.T("You start picking [] up...", this)));
            if (!Dm.truthy(await do_after(user, remove_speed, target: this)) || Dm.num(collection.amount) + 1 > Dm.num(collection.max_amount))
                return ITEM_INTERACT_BLOCKING;

            await collection.add(1);
            playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            qdel(this);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_light_eater)
        {
            obj_effect_decal_cleanable_ash A = Dm.@new<obj_effect_decal_cleanable_ash>(drop_location() as atom);
            A.desc += I18n.T("\nLooks like this used to be [] some time ago.", Dm.text("\\a []", this));
            visible_message(span_danger(I18n.T("[] is disintegrated by []!", this, tool)));
            playsound(this, Dm.resource("sound/items/tools/welder.ogg"), 50, TRUE);
            qdel(this);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/mining/equipment/marker_beacons.dm:157
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        object input_color = await tgui_input_list(user, I18n.T("Choose a color"), I18n.T("Beacon Color"), GLOB.marker_beacon_colors);
        if (input_color == null || !Dm.truthy(await user.can_perform_action(this)))
            return CLICK_ACTION_BLOCKING;
        picked_color = input_color;
        update_appearance();
        return CLICK_ACTION_SUCCESS;
    }
}
