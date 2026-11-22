// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/scanners/t_scanner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice T1 (D164): the T-ray scanner whole (its suicide_act: gaps DV2) but t_ray_scan's client images.
// Its pulse is a list of images, one per hidden object in range, shown to the holder's client only (client.images, protocol
// version 30: an image on a turf is drawn there, DV75). The mining scanners (/adv_mining_scanner) and the drone's
// (/drone) are the miners' and the drones' (after the core version); the cyborg's unequip since silicons SI2.

// tg: code/game/objects/items/devices/scanners/t_scanner.dm:2
public partial class obj_item_t_scanner : obj_item
{
    // tg: code/game/objects/items/devices/scanners/t_scanner.dm:21  (gaps DV2)
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins to emit terahertz-rays into [] brain with []! It looks like [] trying to commit suicide!", user, user.p_their(), this, user.p_theyre())));
        return TOXLOSS;
    }

    // tg: code/game/objects/items/devices/scanners/t_scanner.dm:25
    public virtual void toggle_on()
    {
        playsound(this, SFX_INDUSTRIAL_SCAN, 20, TRUE, -2, TRUE, FALSE);
        on = !Dm.truthy(on) ? TRUE : FALSE;
        icon_state = copytext_char(icon_state, 1, -1) + Dm.text("[]", on);
        // DEVIATION(DV91): SSfastprocess, so a step to another tile rescans at once (process throttles it to tg's 2 s otherwise)
        if (Dm.truthy(on))
            START_PROCESSING(SSfastprocess, this);
        else
            STOP_PROCESSING(SSfastprocess, this);
    }

    // tg: code/game/objects/items/devices/scanners/t_scanner.dm:34
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        toggle_on();
        return null;
    }

    // tg: code/game/objects/items/devices/scanners/t_scanner.dm:37
    public override async DmTask cyborg_unequip(mob user = null)
    {
        if (!Dm.truthy(on))
            return;
        toggle_on();
    }

    // tg: code/game/objects/items/devices/scanners/t_scanner.dm:42
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(on))
            return PROCESS_KILL;
        // DEVIATION(DV91): tg scans every SSobj fire (2 s); the port also scans the moment the scanner reaches another tile
        if (!dm_rescan_due(this, ref dm_scanned_from, ref dm_next_scan))
            return null;
        scan();
        return null;
    }

    // DEVIATION(DV91): port-only, the tile of the last scan and when the next one is due on the same tile
    [NotHashed]
    public turf dm_scanned_from;
    [NotHashed]
    public float dm_next_scan;

    // tg: code/game/objects/items/devices/scanners/t_scanner.dm:47
    public virtual void scan()
    {
        t_ray_scan(loc as mob);
    }
}

public static partial class GlobalProcs
{
    // DEVIATION(DV91): port-only, for the scans that process on SSfastprocess (the T-ray scanner, the scanner goggles): due when
    // the scanning thing reaches another tile, otherwise every 2 s as tg's SSobj fires
    public static bool dm_rescan_due(atom scanner, ref turf scanned_from, ref float next_scan)
    {
        turf here = get_turf(scanner);
        if (ReferenceEquals(here, scanned_from) && world.time < next_scan)
            return false;
        scanned_from = here;
        next_scan = world.time + 2 * SECONDS;
        return true;
    }

    // tg: code/game/objects/items/devices/scanners/t_scanner.dm:50
    public static void t_ray_scan(mob viewer = null, float? flick_time = null, float? distance = null)
    {
        flick_time ??= 8;
        distance ??= 3;
        if (!ismob(viewer) || !Dm.truthy(viewer.client))
            return;
        DmList t_ray_images = Dm.list();
        foreach (obj O in Dm.each<obj>(orange(distance, viewer)))
        {
            if (HAS_TRAIT(O, TRAIT_T_RAY_VISIBLE) || dm_in_wall(O))
            {
                image I = image(loc: get_turf(O));
                mutable_appearance MA = Dm.@new<mutable_appearance>(O);
                MA.alpha = 128;
                MA.dir = O.dir;
                I.appearance = MA;
                Dm.append(ref t_ray_images, I);
            }
        }
        // DEVIATION(DV91): D173, a steady view (see steady_flick_overlay)
        steady_flick_overlay(viewer.client, "t_ray", t_ray_images, flick_time.Value);
    }

    // DEVIATION(DV356): port-only. tg draws the visible pipes, valves and gas meters mapped into a wall over the wall's icon; the 3D wall
    // buries them, so the T-ray shows them as it shows the hidden ones.
    public static bool dm_in_wall(obj O)
        => O.loc is turf_closed && (O is obj_machinery_atmospherics || O is obj_machinery_meter);

    // DEVIATION(DV356): port-only. What is taken out of a wall drops on the tile of who took it out (in 3D an item in a wall cell is out of reach).
    public static atom dm_out_of_wall(atom spot, mob user)
        => spot is turf_closed && get_turf(user) is turf feet ? feet : spot;
}
