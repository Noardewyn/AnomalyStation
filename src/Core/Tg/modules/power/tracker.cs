// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/tracker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice G1 (Meta's solars, D163): the whole file. The var block is generated (D111). As the panel's (solar.cs), the dish is two
// vis_contents overlays that animate() turns; the emissive blocker is DEVIATION(DV12).

//Solar tracker

//Machine that tracks the sun and reports its direction to the solar controllers
//As long as this is working, solar panels on same powernet will track automatically

// tg: code/modules/power/tracker.dm:10
public partial class obj_machinery_power_tracker : obj_machinery_power
{
    // tg: code/modules/power/tracker.dm:1  #define TRACKER_Z_OFFSET 13, TRACKER_EDGE_Z_OFFSET (TRACKER_Z_OFFSET - 2): file-local, #undef'd at :165
    private const float TRACKER_Z_OFFSET = 13;
    private const float TRACKER_EDGE_Z_OFFSET = TRACKER_Z_OFFSET - 2;

    // tg: code/modules/power/tracker.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        obj_item_solar_assembly S = Dm.arg(__extra, 0) as obj_item_solar_assembly;

        tracker_dish_edge = add_panel_overlay("tracker_edge", TRACKER_EDGE_Z_OFFSET);
        tracker_dish = add_panel_overlay("tracker", TRACKER_Z_OFFSET);

        Make(S);
        connect_to_network();
        RegisterSignal(SSsun, COMSIG_SUN_MOVED, nameof(sun_update));
        return dot;
    }

    // tg: code/modules/power/tracker.dm:37
    public override object Destroy(float? force = null)
    {
        unset_control(); //remove from control computer
        return base.Destroy(force);
    }

    // tg: code/modules/power/tracker.dm:41
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
        if (Dm.truthy(same_z_layer))
            return;
        SET_PLANE(tracker_dish_edge, Dm.num(PLANE_TO_TRUE(tracker_dish_edge.plane)), new_turf);
        SET_PLANE(tracker_dish, Dm.num(PLANE_TO_TRUE(tracker_dish.plane)), new_turf);
    }

    // tg: code/modules/power/tracker.dm:55
    public virtual obj_effect_overlay_tracker add_panel_overlay(string icon_state, float z_offset)
    {
        obj_effect_overlay_tracker overlay = Dm.@new<obj_effect_overlay_tracker>(this);
        overlay.icon_state = icon_state;
        SET_PLANE_EXPLICIT(overlay, ABOVE_GAME_PLANE, this);
        overlay.pixel_z = z_offset;
        Dm.append(vis_contents, overlay);
        return overlay;
    }

    // tg: code/modules/power/tracker.dm:63
    public virtual void set_control(obj_machinery_power_solar_control SC = null)
    {
        unset_control();
        control = SC;
        SC.connected_tracker = this;
    }

    //set the control of the tracker to null and removes it from the previous control computer if needed
    // tg: code/modules/power/tracker.dm:69
    public virtual void unset_control()
    {
        if (control != null)
        {
            if (control.track == SOLAR_TRACK_AUTO)
                control.track = SOLAR_TRACK_OFF;
            control.connected_tracker = null;
            control = null;
        }
    }

    /**
     * Get the 2.5D transform for the tracker, given an angle
     * Arguments:
     * * angle - the angle the panel is facing
     */
    // tg: code/modules/power/tracker.dm:84
    public virtual matrix get_tracker_transform(float angle = 0)
    {
        // 2.5D solar tracker works by using a magic combination of transforms
        matrix turner = Dm.matrix();
        // Rotate towards sun
        turner.Turn(angle);
        // Make it skinny on the floor plane
        turner.Scale(1, 0.5f);

        return turner;
    }

    // tg: code/modules/power/tracker.dm:94
    public virtual void visually_turn_part(obj_effect_overlay part = null, float angle = 0)
    {
        float mid_azimuth = (Dm.num(azimuth_current) + angle) / 2;

        // actually flip to other direction?
        if (abs(angle - Dm.num(azimuth_current)) > 180)
            mid_azimuth = SIMPLIFY_DEGREES(mid_azimuth + 180); // REVERSE_ANGLE(mid_azimuth) (__DEFINES/maths.dm:140), expanded inline

        // Split into 2 parts so it doesn't distort on large changes
        animate(part,
            Dm.named("transform", get_tracker_transform(mid_azimuth)),
            Dm.named("time", 2.5f * SECONDS), Dm.named("easing", Dm.bitor(CUBIC_EASING, EASE_IN)));
        animate(
            Dm.named("transform", get_tracker_transform(angle)),
            Dm.named("time", 2.5f * SECONDS), Dm.named("easing", Dm.bitor(CUBIC_EASING, EASE_OUT)));
    }

    ///Tell the controller to turn the solar panels
    // tg: code/modules/power/tracker.dm:111
    [SignalHandler]
    public virtual void sun_update(datum source = null, object azimuth = null)
    {
        float angle = Dm.num(azimuth);
        visually_turn_part(tracker_dish, angle);
        visually_turn_part(tracker_dish_edge, angle);
        azimuth_current = azimuth;

        if (control != null && control.track == SOLAR_TRACK_AUTO)
            control.set_panels(angle);
    }

    // tg: code/modules/power/tracker.dm:121
    public virtual void Make(obj_item_solar_assembly S = null)
    {
        if (S == null)
        {
            S = Dm.@new<obj_item_solar_assembly>(this);
            S.glass_type = typeof(obj_item_stack_sheet_glass);
            S.tracker = 1;
            S.update_appearance();
            Waiting.NoSleep(S.set_anchored(TRUE));
        }
        Waiting.NoSleep(S.forceMove(this));
    }

    // tg: code/modules/power/tracker.dm:130
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(await tool.use_tool(this, user, 0)))
        {
            playsound(this.loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            user.visible_message(span_notice(I18n.T("[] takes the glass off [].", user, this)), span_notice(I18n.T("You take the glass off [].", this)));
            await deconstruct(TRUE);
        }
        return TRUE;
    }

    // tg: code/modules/power/tracker.dm:137
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = null;
        dot = await base.atom_break(damage_flag);
        if (Dm.truthy(dot))
        {
            playsound(loc, Dm.resource("sound/effects/glass/glassbr3.ogg"), 100, TRUE);
            unset_control();
        }
        return dot;
    }

    // tg: code/modules/power/tracker.dm:143
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        Type material_type = typeof(datum_material_glass);
        if (Dm.truthy(disassembled))
        {
            obj_item_solar_assembly assembly = Dm.locate<obj_item_solar_assembly>(this);
            if (assembly != null)
            {
                await assembly.forceMove(loc);
                if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
                {
                    Dm.@new(Dm.initial(material_type, (datum_material m) => m.shard_type), get_turf(this));
                    Dm.@new(Dm.initial(material_type, (datum_material m) => m.shard_type), get_turf(this));
                }
                else
                {
                    Dm.@new(Dm.initial(material_type, (datum_material m) => m.sheet_type), get_turf(this));
                    Dm.@new(Dm.initial(material_type, (datum_material m) => m.sheet_type), get_turf(this));
                }
            }
        }
        else
        {
            //When smashed to bits
            playsound(this, SFX_SHATTER, 70, TRUE);

            Dm.@new(Dm.initial(material_type, (datum_material m) => m.shard_type), get_turf(this));
            Dm.@new(Dm.initial(material_type, (datum_material m) => m.shard_type), get_turf(this));
        }
        return null;
    }
}
