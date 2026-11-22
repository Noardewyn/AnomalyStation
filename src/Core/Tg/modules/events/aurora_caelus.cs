// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/aurora_caelus.dm; code/__DEFINES/gradient.dm (hsl_gradient)
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2c (D327): the whole file: a harmless ion cloud turns the starlight green and blue and lights the kitchens with it, with
// its music for those who keep the MIDI volume up; on April Fools (or 1 in 100) the kitchens' ovens catch fire and the cooks
// mourn their roasts. The var blocks are generated (D111).

// tg: code/modules/events/aurora_caelus.dm:1
public partial class datum_round_event_control_aurora_caelus : datum_round_event_control
{
    // tg: code/modules/events/aurora_caelus.dm:10
    public override object can_spawn_event(object players_amt = null, float? allow_magic = null)
    {
        if (SSmapping.empty_space == null)
            return FALSE;
        return base.can_spawn_event(players_amt, allow_magic);
    }
}

// tg: code/modules/events/aurora_caelus.dm:15
public partial class datum_round_event_aurora_caelus : datum_round_event
{
    // tg: code/modules/events/aurora_caelus.dm:20
    public override async DmTask announce__proc(object fake = null)
    {
        priority_announce(Dm.text("[]: A harmless cloud of ions is approaching your station, and will exhaust their energy battering the hull. Nanotrasen has approved a short break for all employees to relax and observe this very rare event. During this time, starlight will be bright but gentle, shifting between quiet green and blue colors. Any staff who would like to view these lights for themselves may proceed to the area nearest to them with viewing ports to open space. We hope you enjoy the lights.", station_name()),
            sound: Dm.resource("sound/announcer/notice/notice2.ogg"),
            sender_override: "Nanotrasen Meteorology Division");
        if (Dm.truthy(fake))
            return;
        foreach (object V in Dm.each(GLOB.player_list))
        {
            var M = (mob)V;
            float pref_volume = Dm.num(M.client.prefs.read_preference(typeof(datum_preference_numeric_volume_sound_midi)));
            if (pref_volume > 0 && Dm.truthy(is_station_level(M.z)))
                M.playsound_local(M, Dm.resource("sound/ambience/aurora_caelus/aurora_caelus.ogg"), 20 * (pref_volume / 100), FALSE, pressure_affected: FALSE);
        }
        Waiting.Detach(fade_space(fade_in: true));
        Waiting.Detach(fade_kitchen(fade_in: true));
    }

    // tg: code/modules/events/aurora_caelus.dm:34
    public override async DmTask<object> start()
    {
        if (!prob(1) && !Dm.truthy(check_holidays(APRIL_FOOLS)))
            return null;
        var human_blacklist = Dm.list();
        foreach (area_station_service_kitchen affected_area in Dm.each<area_station_service_kitchen>(GLOB.areas))
        {
            var roast_ruiner = Dm.locate<obj_machinery_oven>(affected_area);
            if (roast_ruiner != null)
            {
                roast_ruiner.balloon_alert_to_viewers(I18n.T("oh egads!"));
                turf ruined_roast = get_turf(roast_ruiner);
                ruined_roast.atmos_spawn_air(Dm.text("[]=100;[]", GAS_PLASMA, TURF_TEMPERATURE(1000)));
                message_admins(Dm.text("Aurora Caelus event caused an oven to ignite at [].", ADMIN_VERBOSEJMP(ruined_roast)));
                log_game(Dm.text("Aurora Caelus event caused an oven to ignite at [].", loc_name(ruined_roast)));
                announce_to_ghosts(roast_ruiner);
                foreach (mob_living_carbon_human seymour in Dm.each<mob_living_carbon_human>(viewers(7, roast_ruiner)))
                {
                    if (Dm.@in(seymour, human_blacklist))
                        continue;
                    Dm.append(human_blacklist, seymour);
                    if (seymour.mind != null && seymour.mind.assigned_role is datum_job_cook)
                    {
                        await seymour.say(I18n.T("My roast is ruined!!!"), forced: "ruined roast");
                        await seymour.emote("scream");
                    }
                }
            }
        }
        return null;
    }

    // tg: code/modules/events/aurora_caelus.dm:58
    public override async DmTask tick()
    {
        if (activeFor % 8 != 0)
            return;
        string aurora_color = hsl_gradient((activeFor - start_when) / (end_when - start_when), 0, "#A2FF80", 1, "#A2FFEE");
        await set_starlight(aurora_color);

        foreach (area_station_service_kitchen affected_area in Dm.each<area_station_service_kitchen>(GLOB.areas))
        {
            foreach (turf_open kitchen_floor in Dm.each<turf_open>(affected_area.get_turfs_from_all_zlevels()))
                await kitchen_floor.set_light(l_color: aurora_color);
        }
    }

    // tg: code/modules/events/aurora_caelus.dm:68
    public override async DmTask end()
    {
        Waiting.Detach(fade_space());
        Waiting.Detach(fade_kitchen());
        priority_announce(Dm.text("The aurora caelus event is now ending. Starlight conditions will slowly return to normal. When this has concluded, please return to your workplace and continue work as normal. Have a pleasant shift, [], and thank you for watching with us.", station_name()),
            sound: Dm.resource("sound/announcer/notice/notice2.ogg"),
            sender_override: "Nanotrasen Meteorology Division");
    }

    // tg: code/modules/events/aurora_caelus.dm:75
    public virtual async DmTask fade_space(bool fade_in = false)
    {
        // set waitfor = FALSE
        // iterate all glass tiles
        object start_color = hsl_gradient(1, 0, "#A2FF80", 1, "#A2FFEE");
        float start_range = Dm.num(GLOB.starlight_range) * 1.75f;
        float start_power = Dm.num(GLOB.starlight_power) * 0.6f;
        object end_color = GLOB.base_starlight_color;
        float end_range = Dm.num(GLOB.starlight_range);
        float end_power = Dm.num(GLOB.starlight_power);

        if (fade_in)
        {
            end_color = hsl_gradient(0, 0, "#A2FF80", 1, "#A2FFEE");
            end_range = start_range;
            end_power = start_power;
            start_color = GLOB.base_starlight_color;
            start_range = Dm.num(GLOB.starlight_range);
            start_power = Dm.num(GLOB.starlight_power);
        }

        for (int i = 1; i <= 5; i++)
        {
            string walked_color = hsl_gradient(i / 5f, 0, start_color, 1, end_color);
            float walked_range = LERP(start_range, end_range, i / 5f);
            float walked_power = LERP(start_power, end_power, i / 5f);
            await set_starlight(walked_color, walked_range, walked_power);
            await sleep(8 * SECONDS);
        }
        await set_starlight(end_color, end_range, end_power);
    }

    // tg: code/modules/events/aurora_caelus.dm:103
    public virtual async DmTask fade_kitchen(bool fade_in = false)
    {
        // set waitfor = FALSE
        object start_color = hsl_gradient(1, 0, "#A2FF80", 1, "#A2FFEE");
        float start_range = 1;
        float start_power = 0.75f;
        object end_color = COLOR_BLACK;
        float end_range = 0.5f;
        float end_power = 0;

        if (fade_in)
        {
            end_color = hsl_gradient(0, 0, "#A2FF80", 1, "#A2FFEE");
            end_range = start_range;
            end_power = start_power;
            start_color = COLOR_BLACK;
            start_range = 0.5f;
            start_power = 0;
        }

        for (int i = 1; i <= 5; i++)
        {
            string walked_color = hsl_gradient(i / 5f, 0, start_color, 1, end_color);
            float walked_range = LERP(start_range, end_range, i / 5f);
            float walked_power = LERP(start_power, end_power, i / 5f);
            foreach (area_station_service_kitchen affected_area in Dm.each<area_station_service_kitchen>(GLOB.areas))
            {
                foreach (turf_open kitchen_floor in Dm.each<turf_open>(affected_area.get_turfs_from_all_zlevels()))
                    await kitchen_floor.set_light(walked_range, walked_power, walked_color);
            }
            await sleep(8 * SECONDS);
        }
        foreach (area_station_service_kitchen affected_area in Dm.each<area_station_service_kitchen>(GLOB.areas))
        {
            foreach (turf_open kitchen_floor in Dm.each<turf_open>(affected_area.get_turfs_from_all_zlevels()))
                await kitchen_floor.set_light(end_range, end_power, end_color);
        }
    }
}

public static partial class GlobalProcs
{
    /// <summary>
    /// tg: code/__DEFINES/gradient.dm:3  #define hsl_gradient(index, args...) UNLINT(gradient(args, space = COLORSPACE_HSL, index)), for the
    /// two-stop form tg calls (aurora_caelus.dm): the colour at <paramref name="index"/> between the stop at <paramref name="p0"/> and
    /// the one at <paramref name="p1"/>, blended in HSL with the hue along the shorter arc (BYOND's gradient in a hue colour space;
    /// OpenDream's CalculateGradient), as "#rrggbb".
    /// </summary>
    public static string hsl_gradient(float index, float p0, object c0, float p1, object c1)
    {
        float t = p1 == p0 ? 1 : (index - p0) / (p1 - p0);
        t = t < 0 ? 0 : t > 1 ? 1 : t;
        DmList a = Dm.rgb2num(Dm.str(c0), COLORSPACE_HSL), b = Dm.rgb2num(Dm.str(c1), COLORSPACE_HSL);
        float h0 = Dm.num(a[1]), h1 = Dm.num(b[1]);
        float delta = h1 - h0;
        if (delta > 180)
            h0 += 360;
        else if (delta < -180)
            h1 += 360;
        float h = (h0 + (h1 - h0) * t) % 360;
        float s = Dm.num(a[2]) + (Dm.num(b[2]) - Dm.num(a[2])) * t;
        float l = Dm.num(a[3]) + (Dm.num(b[3]) - Dm.num(a[3])) * t;
        return Dm.rgb(h, s, l, null, COLORSPACE_HSL);
    }
}
