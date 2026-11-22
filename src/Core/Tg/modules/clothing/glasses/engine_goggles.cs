// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/glasses/engine_goggles.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 (D170): the engineering scanner goggles whole but their shuttle region view (show_shuttle: shuttles, M10) and the admin
// goggles' blueprint view (show_blueprints: blueprints, M10+). The modes show their images to the wearer's client only
// (client.images, protocol versions 30 and 31): the T-ray pulse of t_scanner.dm, the pipe connection arrows and the thermal air
// overlay; the meson mode is the meson trait and its sight (_glasses.dm, living.dm update_sight).

// tg: code/modules/clothing/glasses/engine_goggles.dm:16
public partial class obj_item_clothing_glasses_meson_engine : obj_item_clothing_glasses_meson
{
    // tg: code/modules/clothing/glasses/engine_goggles.dm:3-9  #define MODE_NONE ... MODE_AREA_BLUEPRINTS
    private const string MODE_NONE = "";
    private const string MODE_MESON = "meson";
    private const string MODE_TRAY = "t-ray";
    private const string MODE_SHUTTLE = "shuttle";
    private const string MODE_PIPE_CONNECTABLE = "connectable";
    private const string MODE_ATMOS_THERMAL = "atmospheric-thermal";
    private const string MODE_AREA_BLUEPRINTS = "area-blueprints";

    // tg: code/modules/clothing/glasses/engine_goggles.dm:36
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        START_PROCESSING(SSfastprocess, this); // DEVIATION(DV91): D289, rescans on a step (process)
        update_appearance();
        return dot;
    }

    // tg: code/modules/clothing/glasses/engine_goggles.dm:42
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSfastprocess, this);
        return base.Destroy(force);
    }

    // tg: code/modules/clothing/glasses/engine_goggles.dm:46
    [DmParams("user", "voluntary")]
    public virtual async DmTask toggle_mode(mob user = null, object voluntary = null)
    {
        mode_index = WRAP_UP(mode_index, modes.len);
        mode = (string)modes[mode_index];
        to_chat(user, I18n.T("<span class='[]'>[] []</span>", Dm.truthy(voluntary) ? "notice" : "warning",
            Dm.truthy(voluntary) ? I18n.T("You turn the goggles") : I18n.T("The goggles turn"),
            (Dm.truthy(mode) ? I18n.T("to [] mode", mode) : I18n.T("off")) + (Dm.truthy(voluntary) ? "." : "!")));
        if (Dm.truthy(connection_images.len))
            connection_images.Cut();
        switch (mode)
        {
            case MODE_MESON:
                attach_clothing_traits(TRAIT_MESON_VISION);
                color_cutoffs = Dm.list(15, 12, 0);
                change_glass_color(typeof(datum_client_colour_glass_colour_yellow));
                break;

            case MODE_TRAY:
                detach_clothing_traits(TRAIT_MESON_VISION); //undoes the last mode, meson
                color_cutoffs = null;
                change_glass_color(typeof(datum_client_colour_glass_colour_lightblue));
                break;

            case MODE_PIPE_CONNECTABLE:
                change_glass_color(typeof(datum_client_colour_glass_colour_lightblue));
                break;

            case MODE_SHUTTLE:
                change_glass_color(typeof(datum_client_colour_glass_colour_red));
                break;

            case MODE_AREA_BLUEPRINTS:
                change_glass_color(typeof(datum_client_colour_glass_colour_lightyellow));
                break;

            case MODE_NONE:
                change_glass_color(Dm.initial(this, (obj_item_clothing_glasses_meson_engine p) => p.glass_colour_type));
                break;
        }

        if (ishuman(user))
        {
            mob_living_carbon_human H = user as mob_living_carbon_human;
            if (H.glasses == this)
                H.update_sight();
        }

        update_appearance();
        await update_item_action_buttons();
    }

    // DEVIATION(DV91): port-only, the tile of the last refresh and when the next one is due on the same tile
    [NotHashed]
    public turf dm_scanned_from;
    [NotHashed]
    public float dm_next_scan;

    // tg: code/modules/clothing/glasses/engine_goggles.dm:83
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await toggle_mode(user, TRUE);
        return null;
    }

    // tg: code/modules/clothing/glasses/engine_goggles.dm:86
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!ishuman(loc))
            return null;
        mob_living_carbon_human user = loc as mob_living_carbon_human;
        if (user.glasses != this || !Dm.truthy(user.client))
            return null;
        // DEVIATION(DV91): D289, tg refreshes every SSobj fire (2 s); the port also the moment the wearer reaches another tile
        if (!dm_rescan_due(this, ref dm_scanned_from, ref dm_next_scan))
            return null;
        switch (mode)
        {
            case MODE_TRAY:
                t_ray_scan(user, 8, range);
                break;
            case MODE_SHUTTLE:
                show_shuttle();
                break;
            case MODE_PIPE_CONNECTABLE:
                show_connections();
                break;
            case MODE_ATMOS_THERMAL:
                atmos_thermal(user);
                break;
            case MODE_AREA_BLUEPRINTS:
                show_blueprints(user);
                break;
        }
        return null;
    }

    // tg: code/modules/clothing/glasses/engine_goggles.dm:104
    public virtual void show_shuttle()
    {
        // the shuttle's areas around the wearer, drawn as green and red overlays: the shuttle region view (SSshuttle, shuttles, M10)
        Dm.NotTranslated("code/modules/clothing/glasses/engine_goggles.dm:104", "show_shuttle: the shuttle region view (shuttles, M10+)");
    }

    // tg: code/modules/clothing/glasses/engine_goggles.dm:122
    public virtual void show_connections()
    {
        mob_living_carbon_human user = loc as mob_living_carbon_human;
        DmList arrows = Dm.list(); // DEVIATION(DV91): the arrows of this refresh, shown as one steady set below

        foreach (obj_machinery_atmospherics_pipe_smart smart in Dm.each<obj_machinery_atmospherics_pipe_smart>(connection_images))
        {
            if (get_dist(loc, smart.loc) > range)
                Dm.remove(ref connection_images, smart);
        }

        foreach (obj_machinery_atmospherics_pipe_smart smart in Dm.each<obj_machinery_atmospherics_pipe_smart>(orange(range, user)))
        {
            if (!Dm.truthy(connection_images[smart]))
                connection_images[smart] = Dm.list();
            foreach (object direction in Dm.each(GLOB.cardinals))
            {
                if (!Dm.truthy(Dm.bitand(smart.get_init_directions(), direction)))
                    continue;
                if (!Dm.truthy(((DmList)connection_images[smart])[dir2text(direction)]))
                {
                    image arrow;
                    // new /image(icon, loc, icon_state) is image(icon, loc, icon_state)
                    arrow = image(Dm.resource("icons/obj/pipes_n_cables/simple.dmi"), get_turf(smart), "connection_overlay");
                    arrow.dir = Dm.num(direction);
                    arrow.layer = smart.layer;
                    arrow.color = smart.pipe_color;
                    PIPING_LAYER_DOUBLE_SHIFT(arrow, smart.piping_layer);
                    ((DmList)connection_images[smart])[dir2text(direction)] = arrow;
                }
                if (Dm.truthy(connection_images.len))
                    Dm.append(ref arrows, ((DmList)connection_images[smart])[dir2text(direction)]);
            }
        }
        steady_flick_overlay(user.client, "connections", arrows, 1.5f * SECONDS);
    }

    // tg: code/modules/clothing/glasses/engine_goggles.dm:145
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = (string)(inhand_icon_state = Dm.text("trayson-[]", mode));
        return await base.update_icon_state(__extra);
    }
}

// tg: code/modules/clothing/glasses/engine_goggles.dm:150
public partial class obj_item_clothing_glasses_meson_engine_tray : obj_item_clothing_glasses_meson_engine
{
    // tg: code/modules/clothing/glasses/engine_goggles.dm:159
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.dropped(user, silent, __extra);
        if (Dm.truthy(connection_images.len))
            connection_images.Cut();
        return dot;
    }
}

// tg: code/modules/clothing/glasses/engine_goggles.dm:172
public partial class obj_item_clothing_glasses_meson_engine_atmos_imaging : obj_item_clothing_glasses_meson_engine
{
    // tg: code/modules/clothing/glasses/engine_goggles.dm:179
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = (string)(inhand_icon_state = Dm.text("trayson-[]", mode));
        return await base.update_icon_state(__extra);
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/clothing/glasses/engine_goggles.dm:10-13  #define TEMP_SHADE_CYAN ... TEMP_SHADE_RED
    private const float TEMP_SHADE_CYAN = 273.15f;
    private const float TEMP_SHADE_GREEN = 283.15f;
    private const float TEMP_SHADE_YELLOW = 300;
    private const float TEMP_SHADE_RED = 500;

    // tg: code/modules/clothing/glasses/engine_goggles.dm:189
    public static void show_blueprints(mob viewer = null, float? range = null, float? duration = null)
    {
        range ??= 7;
        duration ??= 10;
        Dm.NotTranslated("code/modules/clothing/glasses/engine_goggles.dm:189", "show_blueprints: the admin goggles' blueprint view (the areas' blueprint overlays, blueprints M10+)");
    }

    // tg: code/modules/clothing/glasses/engine_goggles.dm:201
    public static void atmos_thermal(mob viewer = null, float? range = null, float? duration = null)
    {
        range ??= 5;
        duration ??= 10;
        if (!ismob(viewer) || !Dm.truthy(viewer.client))
            return;
        DmList overlays = Dm.list(); // DEVIATION(DV91): this refresh's overlays, shown as one steady set below
        foreach (turf open in Dm.each<turf>(view(range, viewer)))
        {
            if (Dm.truthy(open.blocks_air))
                continue;
            datum_gas_mixture environment = Waiting.NoSleep(open.return_air()) as datum_gas_mixture;
            float temp = round(Dm.num(environment.return_temperature()));
            image turf_overlay = image(Dm.resource("icons/turf/overlays.dmi"), open, "greyOverlay", ABOVE_OPEN_TURF_LAYER);
            // Lower than TEMP_SHADE_CYAN should be deep blue
            float sw__1 = temp;
            if (sw__1 >= -INFINITY && sw__1 <= TEMP_SHADE_CYAN)
                turf_overlay.color = COLOR_STRONG_BLUE;
            // Between TEMP_SHADE_CYAN and TEMP_SHADE_GREEN
            else if (sw__1 >= TEMP_SHADE_CYAN && sw__1 <= TEMP_SHADE_GREEN)
                turf_overlay.color = BlendRGB(COLOR_DARK_CYAN, COLOR_LIME, max(round((temp - TEMP_SHADE_CYAN) / (TEMP_SHADE_GREEN - TEMP_SHADE_CYAN), 0.01f), 0));
            // Between TEMP_SHADE_GREEN and TEMP_SHADE_YELLOW
            else if (sw__1 >= TEMP_SHADE_GREEN && sw__1 <= TEMP_SHADE_YELLOW)
                turf_overlay.color = BlendRGB(COLOR_LIME, COLOR_YELLOW, clamp(round((temp - TEMP_SHADE_GREEN) / (TEMP_SHADE_YELLOW - TEMP_SHADE_GREEN), 0.01f), 0, 1));
            // Between TEMP_SHADE_YELLOW and TEMP_SHADE_RED
            else if (sw__1 >= TEMP_SHADE_YELLOW && sw__1 <= TEMP_SHADE_RED)
                turf_overlay.color = BlendRGB(COLOR_YELLOW, COLOR_RED, clamp(round((temp - TEMP_SHADE_YELLOW) / (TEMP_SHADE_RED - TEMP_SHADE_YELLOW), 0.01f), 0, 1));
            // Over TEMP_SHADE_RED should be red
            else if (sw__1 >= TEMP_SHADE_RED && sw__1 <= INFINITY)
                turf_overlay.color = COLOR_RED;
            turf_overlay.mouse_opacity = MOUSE_OPACITY_TRANSPARENT;
            turf_overlay.alpha = 200;
            Dm.append(ref overlays, turf_overlay);
        }
        steady_flick_overlay(viewer.client, "thermal", overlays, duration.Value);
    }
}
