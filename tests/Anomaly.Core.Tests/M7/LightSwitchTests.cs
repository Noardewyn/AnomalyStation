using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// M7 lighting slice L1 (D168): the wall light switch (lightswitch.dm). A hand on the switch flips its area's lightswitch, tells
/// every machine of the area through power_change (the fixtures go dark or light, lightswitch.dm:92-104, light.dm:722), updates
/// the area's other switches, plays the magazine sounds (:78-81), and the switch shows its state with its icon states and a
/// 1-tile light while the lights are off (:58-65). A screwdriver takes it off the wall into its wall frame (:83-90).
/// </summary>
public class LightSwitchTests
{
    private const int Second = 20;
    private const string Left = "icon-x=16;icon-y=16;left=1;button=left";

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Bob, obj_machinery_light_switch Switch, obj_machinery_light_switch Other, obj_machinery_light A, obj_machinery_light B, area Room);

    private static Rig Build(ulong seed)
    {
        obj_machinery_light_switch sw = null, other = null;
        obj_machinery_light a = null, b = null;
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: w =>
        {
            sw = Dm.@new<obj_machinery_light_switch_directional_south>(locate(5, 2, 1));
            other = Dm.@new<obj_machinery_light_switch_directional_south>(locate(6, 2, 1));
            a = Dm.@new<obj_machinery_light_directional_south>(locate(3, 2, 1));
            b = Dm.@new<obj_machinery_light_directional_south>(locate(9, 2, 1));
            a.allow_break_on_init = FALSE;
            b.allow_break_on_init = FALSE;
        });
        mob_living_carbon_human bob = r.Join(5, 3, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        r.In(() =>
        {
            foreach (obj_machinery_light l in new[] { a, b })
            {
                if (!Dm.eq(l.status, LIGHT_OK))
                    Waiting.NoSleep(l.fix());
            }
        });
        return new Rig(r, bob, sw, other, a, b, r.In(() => (area)get_area(sw)));
    }

    private static void Click(AudioRoom r, mob who, atom target, float seconds = 1)
    {
        r.Command(() => ClientSeam.Click(who.client, target, Left));
        r.Step(Math.Max(2, (int)(seconds * Second)));
    }

    [Fact]
    public void ASwitchStartsOnAndNamesItselfAfterItsArea()
    {
        var g = Build(9001);
        g.R.In(() =>
        {
            Assert.Same(g.Room, g.Switch.area);
            Assert.Equal(Dm.str(g.Room.name), Dm.str(g.Switch.name).Replace("light switch (", "").TrimEnd(')')); // autoname, lightswitch.dm:38
            Assert.True(Dm.truthy(g.Room.lightswitch));
            Assert.Equal("light-on", g.Switch.icon_state);          // update_icon_state, :64
            Assert.Equal(0f, Dm.num(g.Switch.light_range));            // set_light(area.lightswitch ? 0 : light_on_range)
            Assert.Equal(1f, Dm.num(g.Switch.luminosity));              // update_appearance, :55
            Assert.True(Dm.truthy(g.A.on));
            Assert.True(Dm.truthy(g.B.on));
            string examine = string.Join("|", Dm.each(Waiting.NoSleep(g.Switch.examine(g.Bob))).Select(Dm.str));
            Assert.Contains("It is on.", examine, StringComparison.Ordinal);
            Assert.Contains("screwed", examine, StringComparison.Ordinal);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AClickTurnsTheAreasFixturesOffAndOnWithTgsSoundsAndUpdatesTheOtherSwitch()
    {
        var g = Build(9002);
        Click(g.R, g.Bob, g.Switch);
        g.R.In(() =>
        {
            Assert.False(Dm.truthy(g.Room.lightswitch));
            Assert.False(Dm.truthy(g.A.on));
            Assert.False(Dm.truthy(g.B.on));
            Assert.Equal(0f, Dm.num(g.A.light_range)); // the fixture's light is out
            Assert.False(Dm.truthy(g.A.low_power_mode), "a switched-off fixture does not take the emergency light (turned_off, light.dm:504)");
            Assert.Equal("light-off", g.Switch.icon_state);
            Assert.Equal("light-off", g.Other.icon_state);              // set_lights updates every switch of the area, :99
            Assert.Equal(MINIMUM_USEFUL_LIGHT_RANGE, Dm.num(g.Switch.light_range)); // lit while the lights are off: light_on_range 1, raised to the least useful range by set_light, :15
            string examine = string.Join("|", Dm.each(Waiting.NoSleep(g.Switch.examine(g.Bob))).Select(Dm.str));
            Assert.Contains("It is off.", examine, StringComparison.Ordinal);
        });
        Assert.Contains(g.R.Sounds.Heard, h => h.File != null && h.File.Contains("magout", StringComparison.Ordinal)); // sound_off, :19

        Click(g.R, g.Bob, g.Other); // the area's other switch turns them on again
        g.R.In(() =>
        {
            Assert.True(Dm.truthy(g.Room.lightswitch));
            Assert.True(Dm.truthy(g.A.on));
            Assert.True(Dm.truthy(g.B.on));
            Assert.Equal(8f, Dm.num(g.A.light_range)); // the tube's brightness
            Assert.Equal("light-on", g.Switch.icon_state);
            Assert.Equal(0f, Dm.num(g.Switch.light_range));
        });
        Assert.Contains(g.R.Sounds.Heard, h => h.File != null && h.File.Contains("magin", StringComparison.Ordinal)); // sound_on, :17
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ASwitchWithoutPowerShowsNopowerAndAnEmpDoesNotBreakIt()
    {
        var g = Build(9003);
        g.R.In(() =>
        {
            g.Room.requires_power = TRUE;
            g.Room.power_light = FALSE;
            g.Room.power_equip = FALSE; // the switch is on the lighting channel (power_channel = AREA_USAGE_LIGHT)
            g.Room.power_change();
            Assert.True(Dm.truthy(Dm.bitand(g.Switch.machine_stat, NOPOWER)));
            Assert.Equal("light-nopower", g.Switch.icon_state);          // :61
            Assert.Equal(0f, Dm.num(g.Switch.luminosity));                // :55
            Assert.Contains("unpowered", string.Join("|", Dm.each(Waiting.NoSleep(g.Switch.examine(g.Bob))).Select(Dm.str)), StringComparison.Ordinal);
            Waiting.NoSleep(g.Switch.emp_act(1)); // :111, power_change() of a powered switch only; runs without throwing
            g.Room.power_light = TRUE;
            g.Room.power_change();
            Assert.False(Dm.truthy(Dm.bitand(g.Switch.machine_stat, NOPOWER)));
            Assert.Equal("light-on", g.Switch.icon_state);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AScrewdriverTakesTheSwitchOffTheWallIntoItsWallFrame()
    {
        var g = Build(9004);
        var screwdriver = g.R.In(() =>
        {
            var s = Dm.@new<obj_item_screwdriver>(get_turf(g.Bob));
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Bob.put_in_hands(s, merge_stacks: FALSE))));
            return s;
        });
        turf wall_turf = g.R.In(() => get_turf(g.Switch) as turf);
        Click(g.R, g.Bob, g.Switch, seconds: 5); // use_tool(40): 4 seconds
        g.R.In(() =>
        {
            Assert.True(QDELETED(g.Switch));
            var frame = Assert.Single(Dm.each(wall_turf.contents).OfType<obj_item_wallframe_light_switch>());
            Assert.Equal("light switch", Dm.str(frame.name));
            Assert.Equal(26f, Dm.num(frame.pixel_shift));
            Assert.Equal(typeof(obj_machinery_light_switch), frame.result_path);
        });
        _ = screwdriver;
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ASwitchFrameMountedOnTheWallIsAWorkingSwitchOfTheRoom()
    {
        var g = Build(9005);
        var chat = new BalloonChat();
        PresentationSinks.Of(g.R.World).Messages = chat;
        turf wall = g.R.In(() => locate(8, 1, 1) as turf);
        g.R.In(() => Dm.@new<turf_open_floor_plating>(locate(8, 2, 1))); // a frame is mounted from a floor tile
        turf stand = g.R.In(() => locate(8, 2, 1) as turf);
        g.R.In(() =>
        {
            Waiting.NoSleep(g.Bob.forceMove(stand));
            Waiting.NoSleep(g.Bob.setDir(SOUTH));
            var frame = Dm.@new<obj_item_wallframe_light_switch>(stand);
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Bob.put_in_hands(frame, merge_stacks: FALSE))));
        });
        g.R.Step(Second);
        Click(g.R, g.Bob, wall);
        obj_machinery_light_switch added = g.R.In(() => Dm.each(stand.contents).OfType<obj_machinery_light_switch>().SingleOrDefault());
        Assert.True(added != null, string.Join("|", chat.Html.Concat(chat.Balloons)));
        g.R.In(() =>
        {
            Assert.Equal(-26f, Dm.num(added.pixel_y));              // pixel_shift 26 towards the wall it faces (wall_mounted.dm:62)
            Assert.Same(g.Room, added.area);
            Assert.True(Dm.truthy(added.area is area a && Dm.truthy(a.lightswitch)));
        });
        Click(g.R, g.Bob, added);
        g.R.In(() =>
        {
            Assert.False(Dm.truthy(g.Room.lightswitch));
            Assert.False(Dm.truthy(g.A.on));
            Assert.Equal("light-off", g.Switch.icon_state);
            Assert.Equal("light-off", added.icon_state);
        });
        g.R.AssertNoRuntimes();
    }
}

internal sealed class BalloonChat : IMessageSink
{
    public List<string> Html { get; } = new List<string>();
    public List<string> Balloons { get; } = new List<string>();
    public void Chat(object target, DmList message) => Html.Add(Dm.str(message?["html"]));
    public void Output(object target, string text) { }
    public void Balloon(object viewer, object atom, string text) => Balloons.Add(text);
}
