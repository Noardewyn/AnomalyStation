using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package R4 (D327): the security levels' effects in a booted toy room on the station's level. On red alert a red-alert airlock
/// lifts its access with a whirr, a fire alarm shows the level's picture and light, a defibrillator mount's clamps open to any ID, and a
/// computer's alert relevancy rises; the supermatter's emergency raises delta.
/// </summary>
public class SecurityLevelsR4Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(15, 11, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.In(() =>
        {
            GLOB.station_levels_cache.len = 1;
            GLOB.station_levels_cache[1] = TRUE;
            SSmapping.z_list = Dm.list(Dm.@new<datum_space_level>(1, "station", Dm.list(Dm.kv(ZTRAIT_STATION, TRUE))));
            ((area)get_area(bob)).requires_power = FALSE; // the toy room has no APC: its machines run unpowered otherwise
        });
        r.Step(Second);
        return (r, feed, bob);
    }

    private static int Level(AudioRoom r) => r.In(() => (int)Dm.num(SSsecurity_level.get_current_level_as_number()));

    [Fact]
    public void ARedAlertAirlockLiftsItsAccessWithAWhirr()
    {
        var (r, feed, _) = Room(4701);
        var airlock = r.In(() =>
        {
            var a = Dm.@new<obj_machinery_door_airlock>(locate(5, 4, 1));
            a.req_access = Dm.list(ACCESS_CAPTAIN);
            a.red_alert_access = TRUE;
            return a;
        });
        r.Step(Second);
        Assert.False(r.In(() => Dm.truthy(airlock.check_access_list(Dm.list()))), "green: the captain's access is needed");
        r.In(() => SSsecurity_level.set_level(SEC_LEVEL_RED));
        r.Step(Second);
        Assert.Equal(SEC_LEVEL_RED, Level(r));
        Assert.True(feed.Any("automatically lift"), "the airlock whirrs as it lifts its access");
        Assert.True(r.In(() => Dm.truthy(airlock.check_access_list(Dm.list()))), "red: anyone passes");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFireAlarmShowsTheLevelAndADefibMountOpensToAnyIdOnRed()
    {
        var (r, _, bob) = Room(4702);
        var alarm = Place<obj_machinery_firealarm>(r, 6, 6);
        r.In(() =>
        {
            // an alarm made outside the map load is an empty frame with its panel open (firealarm.dm:65): finish it as a mapped one
            alarm.buildstage = FIRE_ALARM_BUILD_SECURED;
            alarm.set_panel_open(FALSE);
        });
        Assert.True(r.In(() => Dm.each<mutable_appearance>(Waiting.NoSleep(alarm.update_overlays()) as DmList).Any(o => Dm.str(o.icon_state) == "fire_0")),
            r.In(() => Dm.text("panel [] stat [] detect [] station [] overlays []", alarm.panel_open, alarm.machine_stat, alarm.my_area?.fire_detect, is_station_level(alarm.z),
                string.Join(",", Dm.each(Waiting.NoSleep(alarm.update_overlays()) as DmList).Select(o => o?.GetType().Name)))));
        var mount = Place<obj_machinery_defibrillator_mount_loaded>(r, 8, 6);
        r.In(() => SSsecurity_level.set_level(SEC_LEVEL_RED));
        r.Step(Second);
        Assert.Contains("fire_2", r.In(() => Dm.each<mutable_appearance>(Waiting.NoSleep(alarm.update_overlays()) as DmList).Select(o => Dm.str(o.icon_state)).ToList()));
        Assert.Equal(r.In(() => SSsecurity_level.current_security_level.fire_alarm_light_color), r.In(() => alarm.light_color));
        var lines = r.In(() => Dm.each((DmList)Waiting.NoSleep(mount.examine(bob))).Select(Dm.str).ToList());
        Assert.Contains(lines, l => l.Contains("Due to a security situation, its locking clamps can be toggled by swiping any ID."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AComputersAlertRelevancyFollowsTheLevelAndTheSupermattersEmergencyRaisesDelta()
    {
        var (r, _, _) = Room(4703);
        var pda = r.In(() => Dm.@new<obj_item_modular_computer_pda>(locate(4, 4, 1)));
        Assert.Equal(0f, r.In(() => Dm.num(pda.get_security_level_relevancy())));
        r.In(() => SSsecurity_level.set_level(SEC_LEVEL_BLUE));
        Assert.Equal(1f, r.In(() => Dm.num(pda.get_security_level_relevancy())));
        var delam = r.In(() => Dm.@new<datum_sm_delam>());
        r.In(() => Waiting.NoSleep(delam.effect_emergency_state()));
        Assert.Equal(SEC_LEVEL_DELTA, Level(r));
        Assert.Equal(2f, r.In(() => Dm.num(pda.get_security_level_relevancy())));
        r.AssertNoRuntimes();
    }
}
