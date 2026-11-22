// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/anomalies/anomalies_bluespace.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1a (D327): the whole file, the bluespace anomaly (it teleports who stands by it or bumps into it, and its detonation moves
// everything around its area to a tracking beacon's), the big one and its circle wave. The var blocks are generated (D111).

// tg: code/game/objects/effects/anomalies/anomalies_bluespace.dm:1
public partial class obj_effect_anomaly_bluespace : obj_effect_anomaly
{
    // tg: code/game/objects/effects/anomalies/anomalies_bluespace.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        apply_wibbly_filters(this);
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_bluespace.dm:17
    public override async DmTask<object> anomalyEffect(object seconds_per_tick = null)
    {
        await base.anomalyEffect(seconds_per_tick);
        foreach (mob_living M in Dm.each<mob_living>(range(teleport_range, this)))
            await do_teleport(M, locate(M.x, M.y, M.z) as atom, teleport_distance, channel: TELEPORT_CHANNEL_BLUESPACE);
        return null;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_bluespace.dm:22
    public override object Bumped(atom_movable bumped_atom = null)
    {
        if (isliving(bumped_atom))
            Waiting.NoSleep(do_teleport(bumped_atom, locate(bumped_atom.x, bumped_atom.y, bumped_atom.z) as atom, 8, channel: TELEPORT_CHANNEL_BLUESPACE));
        return null;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_bluespace.dm:26
    public override async DmTask detonate()
    {
        Dm.@new<obj_effect_temp_visual_circle_wave_bluespace>(get_turf(this));
        playsound(this, "sound/effects/magic/cosmic_energy.ogg", vol: 50);

        var impact_turf = pick(get_area_turfs(impact_area) as DmList) as turf;
        if (impact_turf == null)
            return;

        // Calculate new position (searches through beacons in world)
        obj_item_beacon chosen = null;
        DmList possible = Dm.list();
        foreach (obj_item_beacon beacon in Dm.each<obj_item_beacon>(GLOB.teleportbeacons))
        {
            var turf = get_turf(beacon) as turf;
            if (turf == null)
                continue;
            if (Dm.truthy(is_centcom_level(turf.z)) || Dm.truthy(is_away_level(turf.z)))
                continue;
            if (!Dm.truthy(check_teleport_valid(this, turf)))
                continue;
            Dm.append(ref possible, beacon);
        }

        if (possible.len > 0)
            chosen = pick(possible) as obj_item_beacon;

        if (chosen == null)
            return;

        // Calculate previous position for transition
        var beacon_turf = get_turf(chosen) as turf; // the turf of origin we're travelling TO

        playsound(beacon_turf, "sound/effects/phasein.ogg", 100, TRUE);
        priority_announce("Massive bluespace translocation detected.", "Anomaly Alert");

        DmList flashers = Dm.list();
        foreach (mob_living living in Dm.each<mob_living>(viewers(beacon_turf, null)))
        {
            if (Dm.truthy(await living.flash_act(affect_silicon: TRUE, visual: TRUE, type: typeof(atom_movable_screen_fullscreen_bluespace_sparkle), length: 2 * SECONDS)))
                Dm.append(ref flashers, living);
        }

        float y_distance = beacon_turf.y - impact_turf.y;
        float x_distance = beacon_turf.x - impact_turf.x;
        DmList available_turfs = RANGE_TURFS(12, beacon_turf);
        foreach (atom_movable movable in Dm.each<atom_movable>(urange(12, impact_turf))) // iterate thru list of mobs in the area
        {
            if (movable is obj_item_beacon || iseffect(movable) || iseyemob(movable))
                continue; // Don't mess with beacons, effects or camera mobs (blob, AI eye etc...)
            if (Dm.truthy(movable.anchored))
                continue; // do_teleport doesn't check if the item is anchored or not.

            var newloc = (locate(movable.x + x_distance, movable.y + y_distance, beacon_turf.z) ?? pick(available_turfs)) as turf; // calculate the new place, or pick a random one as fallback
            await do_teleport(movable, newloc, no_effects: TRUE);

            if (isliving(movable) && !Dm.@in(movable, flashers)) // don't flash if we're already doing an effect
            {
                var give_sparkles = (mob_living)movable;
                await give_sparkles.flash_act(affect_silicon: TRUE, visual: TRUE, type: typeof(atom_movable_screen_fullscreen_bluespace_sparkle), length: 2 * SECONDS);
            }
        }
    }

    // tg: code/game/objects/effects/anomalies/anomalies_bluespace.dm:80
    public override void stabilize(float? anchor = null, float? has_core = null)
    {
        base.stabilize(anchor, has_core);

        teleport_range = 0; //bumping already teleports, so this just prevents people from being teleported when they don't expect it when interacting with stable bsanoms
    }
}

///Bigger, meaner, immortal bluespace anomaly
// tg: code/game/objects/effects/anomalies/anomalies_bluespace.dm:86
public partial class obj_effect_anomaly_bluespace_big : obj_effect_anomaly_bluespace
{
    // tg: code/game/objects/effects/anomalies/anomalies_bluespace.dm:92
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        transform = Dm.mul(transform, 3);
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_bluespace.dm:97
    public override object Bumped(atom_movable bumped_atom = null)
    {
        if (iscarbon(bumped_atom))
        {
            var carbon = (mob_living_carbon)bumped_atom;
            if (carbon.reagents is datum_reagents carbon_reagents)
                Waiting.NoSleep(carbon_reagents.add_reagent(typeof(datum_reagent_bluespace), 20));
        }

        if (!isliving(bumped_atom))
            return base.Bumped(bumped_atom);

        var living = (mob_living)bumped_atom;
        Waiting.NoSleep(living.apply_status_effect(typeof(datum_status_effect_teleport_madness)));
        return null;
    }
}
