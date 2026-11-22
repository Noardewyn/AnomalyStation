// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/portal_storm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2c (D327): the whole file: after a bluespace warning, green energy flashes open all over the station and syndicate
// shocktroops (the troopers, gaps E2c) pour out of them, their stormtroopers last. The Nar'Sie storm (weight 0: forced by admins or
// wizards only) runs too, but its hostile constructs are inert until the cult's constructs are ported (not-translated.md). The var
// blocks are generated (D111).

// tg: code/modules/events/portal_storm.dm:34
public partial class datum_round_event_portal_storm : datum_round_event
{
    // tg: code/modules/events/portal_storm.dm:50
    public override async DmTask<object> setup()
    {
        storm_appearances = Dm.list();
        for (int offset = 0; offset <= SSmapping.max_plane_offset; offset++)
        {
            var storm = (mutable_appearance)mutable_appearance("icons/obj/machines/engine/energy_ball.dmi", "energy_ball_fast", FLY_LAYER);
            storm.plane = GET_NEW_PLANE(ABOVE_GAME_PLANE, offset); // SET_PLANE_W_SCALAR(storm, ABOVE_GAME_PLANE, offset)
            storm.color = COLOR_VIBRANT_LIME;
            Dm.append(storm_appearances, storm);
        }

        float bosses = 0;
        foreach (object boss in Dm.each(boss_types))
            bosses += Dm.num(boss_types[boss]);
        number_of_bosses = bosses;

        float hostiles = 0;
        foreach (object hostile in Dm.each(hostile_types))
            hostiles += Dm.num(hostile_types[hostile]);
        number_of_hostiles = hostiles;

        while (bosses > boss_spawn.len)
            Dm.append(boss_spawn, get_random_station_turf());

        while (hostiles > hostiles_spawn.len)
            Dm.append(hostiles_spawn, get_random_station_turf());

        next_boss_spawn = start_when + ceil(2 * hostiles / bosses);
        return null;
    }

    // tg: code/modules/events/portal_storm.dm:72
    public override async DmTask announce__proc(object fake = null)
    {
        // set waitfor = 0
        Waiting.Detach(announce_storm());
    }

    // the body of announce (set waitfor = 0): its sleeps run apart from the event's tick
    private async DmTask announce_storm()
    {
        sound_to_playing_players("sound/effects/magic/lightning_chargeup.ogg");
        await sleep(8 * SECONDS);
        priority_announce(Dm.text("Massive bluespace anomaly detected en route to []. Brace for impact.", station_name()));
        await sleep(2 * SECONDS);
        sound_to_playing_players("sound/effects/magic/lightningbolt.ogg");
    }

    // tg: code/modules/events/portal_storm.dm:80
    public override async DmTask tick()
    {
        spawn_effects(get_random_station_turf() as turf);

        if (spawn_hostile() && Dm.length(hostile_types) > 0)
        {
            object type = pick(hostile_types);
            hostile_types[type] = Dm.num(hostile_types[type]) - 1;
            spawn_mob(type, hostiles_spawn);
            if (!Dm.truthy(hostile_types[type]))
                Dm.remove(hostile_types, type);
        }

        if (spawn_boss() && Dm.length(boss_types) > 0)
        {
            object type = pick(boss_types);
            boss_types[type] = Dm.num(boss_types[type]) - 1;
            spawn_mob(type, boss_spawn);
            if (!Dm.truthy(boss_types[type]))
                Dm.remove(boss_types, type);
        }

        time_to_end();
    }

    // tg: code/modules/events/portal_storm.dm:100
    public virtual void spawn_mob(object type = null, DmList spawn_list = null)
    {
        if (!Dm.truthy(type))
            return;
        var T = pick_n_take(spawn_list) as turf;
        if (T == null)
            return;
        Dm.@new((Type)type, T);
        spawn_effects(T);
    }

    // tg: code/modules/events/portal_storm.dm:109
    public virtual void spawn_effects(turf T = null)
    {
        if (T == null)
        {
            log_game("Portal Storm failed to spawn effect due to an invalid location.");
            return;
        }
        T = get_step(T, SOUTHWEST) as turf; //align center of image with turf
        if (T == null)
            return;
        Waiting.Detach(T.flick_overlay_static(storm_appearances[Dm.num(GET_TURF_PLANE_OFFSET(T)) + 1], 15));
        playsound(T, "sound/effects/magic/lightningbolt.ogg", rand(80, 100), TRUE);
    }

    // tg: code/modules/events/portal_storm.dm:117
    public virtual bool spawn_hostile()
    {
        if (hostile_types == null || hostile_types.len == 0)
            return false;
        return activeFor % 2 == 0; // ISMULTIPLE(activeFor, 2)
    }

    // tg: code/modules/events/portal_storm.dm:122
    public virtual bool spawn_boss()
    {
        if (boss_types == null || boss_types.len == 0)
            return false;

        if (activeFor == Dm.num(next_boss_spawn))
        {
            next_boss_spawn = Dm.num(next_boss_spawn) + ceil(Dm.num(number_of_hostiles) / Dm.num(number_of_bosses));
            return true;
        }
        return false;
    }

    // tg: code/modules/events/portal_storm.dm:131
    public virtual void time_to_end()
    {
        if (hostile_types.len == 0 && boss_types.len == 0)
            end_when = activeFor;

        if (!Dm.truthy(number_of_hostiles) && Dm.truthy(number_of_bosses))
            end_when = activeFor;
    }
}
