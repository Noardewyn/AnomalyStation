// Translated from tgstation (snapshot 1d99bd7f): code/modules/transport/tram/tram_displays.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The tram port (2026-11-12): the whole file but add_context (the screentips stay deferred, live-partial.txt). The sign face's
// emissive overlay is lighting (DEVIATION DV12); the sign's own light (set_light) is tg's.

// tg: code/modules/transport/tram/tram_displays.dm:1
public partial class obj_machinery_transport_destination_sign : obj_machinery_transport
{
    // tg: code/modules/transport/tram/tram_displays.dm:16  var/static/list/available_faces (rules 10.6)
    public static ref DmList available_faces => ref TgState.Current.obj_machinery_transport_destination_sign__available_faces;

    // tg: code/modules/transport/tram/tram_displays.dm:47
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(SStransport, COMSIG_TRANSPORT_UPDATED, nameof(update_sign));
        Dm.append(ref SStransport.displays, this);
        available_faces = Dm.list(TRAMSTATION_LINE_1);
        Waiting.NoSleep(set_light(l_dir: REVERSE_DIR(dir)));
        return dot;
    }

    // tg: code/modules/transport/tram/tram_displays.dm:56
    public override object Destroy(float? force = null)
    {
        Dm.remove(SStransport.displays, this);
        return base.Destroy(force);
    }

    // tg: code/modules/transport/tram/tram_displays.dm:86
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        atom drop = drop_location() as atom;
        if (Dm.truthy(disassembled))
        {
            Dm.@new<obj_item_wallframe_indicator_display>(drop);
        }
        else
        {
            Dm.@new<obj_item_stack_sheet_mineral_titanium>(drop, 2);
            Dm.@new<obj_item_stack_sheet_iron>(drop);
            Dm.@new<obj_item_shard>(drop);
            Dm.@new<obj_item_shard>(drop);
        }
        return null;
    }

    // tg: code/modules/transport/tram/tram_displays.dm:107
    public virtual void update_sign(datum source = null, datum_transport_controller_linear_tram controller = null, object controller_active = null, object controller_status = null, object travel_direction = null, obj_effect_landmark_transport_nav_beacon_tram_platform destination_platform = null)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
        {
            sign_face = null;
            update_appearance();
            return;
        }

        if (controller != null && !Dm.eq(controller.specific_transport_id, configured_transport_id))
            return;

        if (controller == null || !Dm.truthy(controller.controller_operational) || destination_platform == null)
        {
            sign_face = Dm.text("[]_NIS", base_icon_state);
            sign_color = COLOR_DISPLAY_RED;
            update_appearance();
            return;
        }

        if ((controller.status_bits & EMERGENCY_STOP) != 0 || (controller.status_bits & SYSTEM_FAULT) != 0)
        {
            sign_face = Dm.text("[]_NIS", base_icon_state);
            sign_color = COLOR_DISPLAY_RED;
            update_appearance();
            return;
        }

        string face = "";
        face += Dm.text("[]_", base_icon_state);
        if (!Dm.truthy(LAZYFIND(available_faces, controller.specific_transport_id)))
            face += Dm.text("[]", TRAMSTATION_LINE_1);
        else
            face += Dm.text("[]", controller.specific_transport_id);

        face += Dm.text("[]", controller_active);
        face += Dm.text("[]", destination_platform.platform_code);
        face += Dm.text("[]", travel_direction);
        sign_face = face;
        sign_color = COLOR_DISPLAY_BLUE;

        update_appearance();
    }

    // tg: code/modules/transport/tram/tram_displays.dm:139
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (sign_face == null)
        {
            icon_state = Dm.text("[]_blank", base_icon_state);
            return dot;
        }
        else
        {
            icon_state = Dm.text("[]", sign_face);
        }
        return dot;
    }

    // tg: code/modules/transport/tram/tram_displays.dm:147
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (sign_face == null)
        {
            await set_light(l_on: FALSE);
            return dot;
        }

        await set_light(l_on: TRUE, l_color: sign_color);
        // . += emissive_appearance(icon, "[sign_face]_e", src, alpha = src.alpha): the glow on the emissive plane (DV12)
        return dot;
    }
}

// tg: code/modules/transport/tram/tram_displays.dm:27
public partial class obj_machinery_transport_destination_sign_indicator : obj_machinery_transport_destination_sign
{
    // tg: code/modules/transport/tram/tram_displays.dm:60
    public override async DmTask setDir(object newdir = null)
    {
        await base.setDir(newdir);
        await set_light(l_dir: REVERSE_DIR(dir));
    }

    // tg: code/modules/transport/tram/tram_displays.dm:64
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        link_tram();
    }

    // tg: code/modules/transport/tram/tram_displays.dm:79
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        if (Dm.truthy(panel_open))
            Dm.append(ref dot, span_notice(I18n.T("It is secured to the tram wall with []", EXAMINE_HINT("bolts."))));
        return dot;
    }

    // tg: code/modules/transport/tram/tram_displays.dm:97
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act_secondary(user, tool);
        balloon_alert(user, I18n.T("[]securing...", Dm.truthy(anchored) ? "un" : ""));
        tool.play_tool_sound(this);
        if (Dm.truthy(await tool.use_tool(this, user, 6 * SECONDS)))
        {
            playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, vary: TRUE);
            balloon_alert(user, I18n.T("[]secured", Dm.truthy(anchored) ? "un" : ""));
            await deconstruct();
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/transport/tram/tram_displays.dm:155
    public override object power_change()
    {
        object dot = base.power_change();
        datum_transport_controller_linear_tram tram = transport_ref?.resolve() as datum_transport_controller_linear_tram;
        if (tram == null)
            return dot;

        update_sign(this, tram, tram.controller_active, tram.controller_status, tram.travel_direction, tram.destination_platform);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/transport/tram/tram_displays.dm:16  var/static/list/available_faces
    internal DmList obj_machinery_transport_destination_sign__available_faces = Dm.list();
}
