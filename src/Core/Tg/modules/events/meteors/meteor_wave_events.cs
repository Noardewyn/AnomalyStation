// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/meteors/meteor_wave_events.dm, stray_meteor_event.dm,
// dark_matteor_event.dm; code/modules/events/dust.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2b (D327): the meteor events whole: the meteor waves (normal, threatening, catastrophic, meaty, the major space dust), the
// stray meteor, the dark matt-eor (admin only, weight 0) and the minor space dust. The admins' setups (the stray meteor's type, the
// dark matt-eor's warning) wait for the admin event panel as the other events' do (not-translated.md). The var blocks are generated
// (D111).

// tg: code/modules/events/meteors/meteor_wave_events.dm:13
public partial class datum_round_event_meteor_wave : datum_round_event
{
    // tg: code/modules/events/meteors/meteor_wave_events.dm:21
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (wave_type == null)
            determine_wave_type();
    }

    // tg: code/modules/events/meteors/meteor_wave_events.dm:26
    public virtual void determine_wave_type()
    {
        if (!Dm.truthy(wave_name))
            wave_name = (string)pick_weight(Dm.list(
                Dm.kv("normal", 50),
                Dm.kv("threatening", 40),
                Dm.kv("catastrophic", 10)));
        switch (wave_name)
        {
            case "normal":
                wave_type = GLOB.meteors_normal;
                break;
            case "threatening":
                wave_type = GLOB.meteors_threatening;
                break;
            case "catastrophic":
                if (Dm.truthy(check_holidays(HALLOWEEN)))
                    wave_type = GLOB.meteorsSPOOKY;
                else
                    wave_type = GLOB.meteors_catastrophic;
                break;
            case "meaty":
                wave_type = GLOB.meateors;
                break;
            case "space dust":
                wave_type = GLOB.meteors_dust;
                break;
            case "halloween":
                wave_type = GLOB.meteorsSPOOKY;
                break;
            default:
                // WARNING(MSG) (__HELPERS/logging/_logging.dm:2), expanded inline (rules 11.1 (d), (f): src, usr, __FILE__, __LINE__)
                warning(Dm.text("[] in [] at line [] src: [] usr: [].", Dm.text("Wave name of [] not recognised.", wave_name),
                    "code/modules/events/meteors/meteor_wave_events.dm", 50, this, usr));
                kill();
                break;
        }
    }

    // tg: code/modules/events/meteors/meteor_wave_events.dm:53
    public override async DmTask announce__proc(object fake = null)
    {
        priority_announce("Meteors have been detected on collision course with the station.", "Meteor Alert", ANNOUNCER_METEORS);
    }

    // tg: code/modules/events/meteors/meteor_wave_events.dm:56
    public override async DmTask tick()
    {
        if (activeFor % 3 == 0) // ISMULTIPLE(activeFor, 3)
            spawn_meteors(5, wave_type); //meteor list types defined in gamemode/meteor/meteors.dm
    }
}

// tg: code/modules/events/meteors/meteor_wave_events.dm:95
public partial class datum_round_event_meteor_wave_meaty : datum_round_event_meteor_wave
{
    // tg: code/modules/events/meteors/meteor_wave_events.dm:98
    public override async DmTask announce__proc(object fake = null)
    {
        priority_announce("Meaty ores have been detected on collision course with the station.", "Oh crap, get the mop.", ANNOUNCER_METEORS);
    }
}

// tg: code/modules/events/meteors/meteor_wave_events.dm:110
public partial class datum_round_event_meteor_wave_dust_storm : datum_round_event_meteor_wave
{
    // tg: code/modules/events/meteors/meteor_wave_events.dm:114
    public override async DmTask announce__proc(object fake = null)
    {
        var reasons = Dm.list();

        Dm.append(reasons, Dm.text("[] is passing through a debris cloud, expect minor damage to external fittings and fixtures.", station_name()));

        Dm.append(reasons, Dm.text("Nanotrasen Superweapons Division is testing a new prototype [] [], some mild debris is expected.",
            pick("field", "projection", "nova", "super-colliding", "reactive"),
            pick("cannon", "artillery", "tank", "cruiser", "[REDACTED]")));

        Dm.append(reasons, "A neighbouring station is throwing rocks at you. (Perhaps they've grown tired of your messages.)");

        Dm.append(reasons, Dm.text("[]'s orbit is passing through a cloud of remnants from an asteroid mining operation. Minor hull damage is to be expected.", station_name()));

        Dm.append(reasons, Dm.text("A large meteoroid on intercept course with [] has been demolished. Residual debris may impact the station exterior.", station_name()));

        Dm.append(reasons, Dm.text("[] has hit a particularly rough patch of space. Please mind any turbulence or damage from debris.", station_name()));

        priority_announce(pick(reasons), "Collision Alert");
    }
}

// tg: code/modules/events/meteors/stray_meteor_event.dm:15
public partial class datum_round_event_stray_meteor : datum_round_event
{
    // tg: code/modules/events/meteors/stray_meteor_event.dm:21
    public override async DmTask<object> start()
    {
        if (Dm.truthy(chosen_meteor))
        {
            var chosen_meteor_list = Dm.list();
            chosen_meteor_list[chosen_meteor] = 1;
            spawn_meteor(chosen_meteor_list);
        }
        else
            spawn_meteor(GLOB.meteors_stray);
        return null;
    }

    // tg: code/modules/events/meteors/stray_meteor_event.dm:29
    public override async DmTask announce__proc(object fake = null)
    {
        if (Dm.length(GLOB.meteor_list) > 0)
        {
            var detected_meteor = (obj_effect_meteor)pick(GLOB.meteor_list); //If we accidentally pick a meteor not spawned by the event, we're still technically not wrong
            object sensor_name = detected_meteor.signature;
            priority_announce(Dm.text("Our [] sensors have detected an incoming signature approaching []. Please brace for impact.", sensor_name, GLOB.station_name), "Meteor Alert");
        }
    }
}

// tg: code/modules/events/meteors/dark_matteor_event.dm:11
public partial class datum_round_event_dark_matteor : datum_round_event
{
    // tg: code/modules/events/meteors/dark_matteor_event.dm:14
    public override async DmTask<object> start()
    {
        mob_living target = null;
        foreach (mob_living potential_target in Dm.each_cast<mob_living>(GLOB.mob_living_list))
        {
            if (!Dm.truthy(is_station_level(potential_target.z)))
                continue;
            turf target_turf = get_turf(potential_target);
            if (isgroundlessturf(target_turf))
                continue;
            target = potential_target;
            break;
        }
        //if target was never chosen the target is null aka the matteor will act as spacedust (and can technically miss)
        spawn_meteor(Dm.list(Dm.kv(typeof(obj_effect_meteor_dark_matteor), 1)), null, target, distance_from_edge: 10);
        return null;
    }

    // tg: code/modules/events/meteors/dark_matteor_event.dm:27
    public override async DmTask announce__proc(object fake = null)
    {
        priority_announce(Dm.text("Warning. Excessive tampering of meteor satellites has attracted a dark matt-eor. Signature approaching []. Please brace for impact.", GLOB.station_name),
            "Meteor Alert", Dm.resource("sound/announcer/alarm/airraid.ogg"));
    }
}

// tg: code/modules/events/dust.dm:12
public partial class datum_round_event_space_dust : datum_round_event
{
    // tg: code/modules/events/dust.dm:17
    public override async DmTask<object> start()
    {
        spawn_meteors(1, GLOB.meteors_dust);
        return null;
    }
}
