using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Syndicate;

/// <summary>
/// Syndicate stage N1b (D316): the NTOS radar (modular_computers/file_system/programs/radar.dm) as the operatives' Fission360 on their
/// nuclear PDA in a booted toy room. Started, it scans the nukes, the disk and the infiltrator; the disk picked, it tracks it (its
/// place on the radar's grid, the PDA's screen and facing); a nuke armed makes the PDA vibrate with its alarm.
/// </summary>
public class Fission360Tests
{
    [Fact]
    public void TheRadarsAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(datum_computer_file_program_radar), typeof(datum_computer_file_program_radar_lifeline),
            typeof(datum_computer_file_program_radar_custodial_locator), typeof(datum_computer_file_program_radar_fission360),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void TheFission360FindsTheNukeAndTheDiskTracksTheDiskAndHearsANukeArmed()
    {
        var r = new AudioRoom(11, 9, seed: 1501);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.In(() =>
        {
            // the toy room's level is the station's (as SyndicateNukeTests): the disk stays where it is put
            GLOB.station_levels_cache.len = 1;
            GLOB.station_levels_cache[1] = TRUE;
            GLOB.the_station_areas = Dm.list(((area)get_area(bob)).type);
        });
        var nuke = r.In(() => Dm.@new<obj_machinery_nuclearbomb_syndicate>(locate(9, 4, 1)));
        var disk = r.In(() => Dm.@new<obj_item_disk_nuclear>(locate(5, 6, 1)));
        var pda = r.In(() =>
        {
            var made = Dm.@new<obj_item_modular_computer_pda_nukeops>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(made));
            return made;
        });
        r.Step(Second);

        var radar = r.In(() => Dm.locate<datum_computer_file_program_radar_fission360>(pda.stored_files));
        Assert.NotNull(radar);
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(pda.open_program(bob, radar, FALSE)))));
        var data = r.In(() => (DmList)Waiting.NoSleep(radar.ui_data(bob)));
        List<string> names = r.In(() => Dm.each<DmList>(data["object"] as DmList).Select(o => Dm.str(o["name"])).ToList());
        Assert.Contains(r.In(() => Dm.str(nuke.name)), names);
        Assert.Contains("Nuke Auth. Disk", names);
        Assert.Contains("Infiltrator", names);
        Assert.Equal(FALSE, r.In(() => Dm.num(data["scanning"])));

        // the disk picked: two east and two north of the PDA on the radar's 48 by 48 grid, the PDA's screen close and facing it
        string diskRef = r.In(() => Dm.str(REF(disk)));
        r.In(() => Waiting.NoSleep(radar.ui_act("selecttarget", Dm.list(Dm.kv("ref", diskRef)))));
        r.Step(Second);
        DmList target = r.In(() => ((DmList)Waiting.NoSleep(radar.ui_data(bob)))["target"] as DmList);
        Assert.Equal(26f, r.In(() => Dm.num(target["locx"])));
        Assert.Equal(22f, r.In(() => Dm.num(target["locy"])));
        Assert.Equal("crosshairs", r.In(() => Dm.str(target["pointer"])));
        Assert.Equal("red", r.In(() => Dm.str(target["color"])));
        Assert.Equal("radarsyndicateclose", r.In(() => Dm.str(radar.program_open_overlay)));
        Assert.Equal((float)NORTHEAST, r.In(() => (float)pda.dir));

        // a scan right after another waits out its two seconds
        r.In(() => Waiting.NoSleep(radar.ui_act("scan", Dm.list())));
        Assert.Equal(TRUE, r.In(() => Dm.num(((DmList)Waiting.NoSleep(radar.ui_data(bob)))["scanning"])));

        // the nuke armed: the PDA in Bob's hand vibrates with its alarm
        r.In(() => nuke.arm_nuke(bob));
        r.Step(2);
        Assert.True(feed.Any("vibrates and lets out an ominous alarm. Uh oh."));
        r.AssertNoRuntimes();
    }
}
