// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/apc/apc_appearance.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice 3 (the APC): the whole file. The icon states and the overlays of tg's wallmounts.dmi reach the client as the APC's
// appearance (the APC models of godot/assets/models/models.json are chosen by them); the screen light is set_light. The
// emissive copies of the overlays are DEVIATION DV12 (deferred leaves).

// tg: code/modules/power/apc/apc_appearance.dm:3
public partial class obj_machinery_power_apc : obj_machinery_power
{
    // update the APC icon to show the three base states
    // also add overlays for indicator lights
    // tg: code/modules/power/apc/apc_appearance.dm:3
    public override object update_appearance(float? updates = null)
    {
        updates ??= Dm.num_or_null(check_updates());
        object dot = null;
        icon_update_needed = FALSE;
        if (!Dm.truthy(updates))
            return dot;

        dot = base.update_appearance(updates);
        // And now, separately for cleanness, the lighting changing
        if (!Dm.truthy(update_state))
        {
            object sw__1 = charging;
            if (Dm.eq(sw__1, APC_NOT_CHARGING))
                set_light_color(COLOR_SOFT_RED);
            else if (Dm.eq(sw__1, APC_CHARGING))
                set_light_color(LIGHT_COLOR_BLUE);
            else if (Dm.eq(sw__1, APC_FULLY_CHARGED))
                set_light_color(LIGHT_COLOR_GREEN);
            Waiting.NoSleep(set_light(light_on_range));
            return dot;
        }

        Waiting.NoSleep(set_light(0));
        return dot;
    }

    // tg: code/modules/power/apc/apc_appearance.dm:23
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (!Dm.truthy(update_state))
        {
            icon_state = "apc0";
            return await base.update_icon_state(__extra);
        }
        if (Dm.truthy(Dm.bitand(update_state, Dm.bitor(UPSTATE_OPENED1, UPSTATE_OPENED2))))
        {
            string basestate = Dm.text("apc[]", cell != null ? 2 : 1);
            if (Dm.truthy(Dm.bitand(update_state, UPSTATE_OPENED1)))
                icon_state = Dm.truthy(Dm.bitand(update_state, Dm.bitor(UPSTATE_MAINT, UPSTATE_BROKE))) ? "apcmaint" : basestate;
            else if (Dm.truthy(Dm.bitand(update_state, UPSTATE_OPENED2)))
                icon_state = Dm.text("[][]-nocover", basestate, (Dm.truthy(Dm.bitand(update_state, UPSTATE_BROKE)) || Dm.truthy(malfhack)) ? "-b" : null);
            return await base.update_icon_state(__extra);
        }
        if (Dm.truthy(Dm.bitand(update_state, UPSTATE_BROKE)))
        {
            icon_state = "apc-b";
            return await base.update_icon_state(__extra);
        }
        if (Dm.truthy(Dm.bitand(update_state, UPSTATE_WIREEXP)))
        {
            icon_state = "apcewires";
            return await base.update_icon_state(__extra);
        }
        if (Dm.truthy(Dm.bitand(update_state, UPSTATE_MAINT)))
            icon_state = "apc0";
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/power/apc/apc_appearance.dm:44
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, MAINT))) || Dm.truthy(update_state))
            return dot;

        Dm.append(ref dot, mutable_appearance(icon, Dm.text("apcox-[]", locked)));
        // DEVIATION(DV12): . += emissive_appearance(icon, "apcox-[locked]", src)
        Dm.Deferred("code/modules/power/apc/apc_appearance.dm:50", "update_overlays: emissive_appearance of the lock light (DEVIATION DV12)");
        Dm.append(ref dot, mutable_appearance(icon, Dm.text("apco3-[]", charging)));
        // DEVIATION(DV12): . += emissive_appearance(icon, "apco3-[charging]", src)
        Dm.Deferred("code/modules/power/apc/apc_appearance.dm:52", "update_overlays: emissive_appearance of the charge light (DEVIATION DV12)");
        if (!Dm.truthy(operating))
            return dot;

        Dm.append(ref dot, mutable_appearance(icon, Dm.text("apco0-[]", equipment)));
        // DEVIATION(DV12): . += emissive_appearance(icon, "apco0-[equipment]", src)
        Dm.Deferred("code/modules/power/apc/apc_appearance.dm:57", "update_overlays: emissive_appearance of the equipment light (DEVIATION DV12)");
        Dm.append(ref dot, mutable_appearance(icon, Dm.text("apco1-[]", lighting)));
        // DEVIATION(DV12): . += emissive_appearance(icon, "apco1-[lighting]", src)
        Dm.Deferred("code/modules/power/apc/apc_appearance.dm:59", "update_overlays: emissive_appearance of the lighting light (DEVIATION DV12)");
        Dm.append(ref dot, mutable_appearance(icon, Dm.text("apco2-[]", environ)));
        // DEVIATION(DV12): . += emissive_appearance(icon, "apco2-[environ]", src)
        Dm.Deferred("code/modules/power/apc/apc_appearance.dm:61", "update_overlays: emissive_appearance of the environment light (DEVIATION DV12)");
        return dot;
    }

    /// Checks for what icon updates we will need to handle
    // tg: code/modules/power/apc/apc_appearance.dm:64
    [SignalHandler]
    public virtual object check_updates()
    {
        object dot = null;
        dot = NONE;

        // Handle icon status:
        float new_update_state = NONE;
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            Dm.combine(ref new_update_state, UPSTATE_BROKE);
        if (Dm.truthy(Dm.bitand(machine_stat, MAINT)))
            Dm.combine(ref new_update_state, UPSTATE_MAINT);

        if (Dm.truthy(opened))
        {
            Dm.combine(ref new_update_state, Dm.shl(opened, UPSTATE_COVER_SHIFT));
            if (cell != null)
                Dm.combine(ref new_update_state, UPSTATE_CELL_IN);
        }

        else if (Dm.truthy(panel_open))
            Dm.combine(ref new_update_state, UPSTATE_WIREEXP);

        if (new_update_state != update_state)
        {
            update_state = new_update_state;
            Dm.combine(ref dot, UPDATE_ICON_STATE);
        }

        // Handle overlay status:
        float new_update_overlay = NONE;
        if (Dm.truthy(operating))
            Dm.combine(ref new_update_overlay, UPOVERLAY_OPERATING);

        if (!Dm.truthy(update_state))
        {
            if (Dm.truthy(locked))
                Dm.combine(ref new_update_overlay, UPOVERLAY_LOCKED);

            Dm.combine(ref new_update_overlay, Dm.shl(charging, UPOVERLAY_CHARGING_SHIFT));
            Dm.combine(ref new_update_overlay, Dm.shl(equipment, UPOVERLAY_EQUIPMENT_SHIFT));
            Dm.combine(ref new_update_overlay, Dm.shl(lighting, UPOVERLAY_LIGHTING_SHIFT));
            Dm.combine(ref new_update_overlay, Dm.shl(environ, UPOVERLAY_ENVIRON_SHIFT));
        }

        if (new_update_overlay != update_overlay)
        {
            update_overlay = new_update_overlay;
            Dm.combine(ref dot, UPDATE_OVERLAYS);
        }
        return dot;
    }


    // Used in process so it doesn't update the icon too much
    // tg: code/modules/power/apc/apc_appearance.dm:107
    public virtual void queue_icon_update()
    {
        icon_update_needed = TRUE;
    }

    // Shows a dark-blue interface for a moment. Shouldn't appear on cameras.
    // tg: code/modules/power/apc/apc_appearance.dm:111
    public virtual void flicker_hacked_icon()
    {
        if (opened != APC_COVER_CLOSED)
            return;
        image hacker_image = image(icon: Dm.resource("icons/obj/machines/wallmounts.dmi"), loc: this, icon_state: "apcemag", layer: FLOAT_LAYER);
        DmList mobs_to_show = Dm.list();
        // Collecting mobs the APC can see for this animation, rather than mobs that can see the APC. Important distinction, intended such that mobs on camera / with XRAY cannot see the flicker.
        foreach (mob viewer in Dm.each<mob>(view(this)))
        {
            if (Dm.truthy(viewer.client))
                Dm.append(ref mobs_to_show, viewer.client);
        }
        if (Dm.truthy(malfai?.client))
            Dm.combine(ref mobs_to_show, malfai.client);
        flick_overlay_global(hacker_image, mobs_to_show, 1 * SECONDS);
        hacked_flicker_counter = rand(3, 5); //The counter is decrimented in the process() proc, which runs every two seconds.
    }
}
