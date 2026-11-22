using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Doors.T1;

// T1: the light fixture (modules/power/lighting/light.dm and light_mapping_helpers.dm): the data of every fixture type
// against tg's own var blocks, what a lit fixture sets on its atom, the state changes, and that they reach LightChanged.

public class LightTests
{
    private static readonly string[] Compared =
    {
        "brightness", "bulb_power", "bulb_colour", "status", "fitting", "base_state", "light_type", "nightshift_allowed",
        "nightshift_brightness", "nightshift_light_color", "no_low_power", "fire_brightness", "fire_colour", "start_with_cell",
        "break_if_moved", "light_angle",
    };

    private static object PortValue(object value)
        => value switch
        {
            null => null,
            float f => (double)f,
            double d => d,
            int i => (double)i,
            string s => s,
            Type t => DmTypes.PathOf(t),
            _ => value,
        };

    [TgFact]
    public void EveryFixtureTypeHasTgsVarData()
    {
        var parsed = TgLightData.Parse();
        string[] rels = parsed.Keys.Where(k => k != "").OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Assert.True(rels.Length >= 25, $"the parse found {rels.Length} light types");
        var made = new Dictionary<string, obj_machinery_light>();
        var room = T1.Room(width: 34, height: 6, mapExtras: w =>
        {
            int x = 2;
            foreach (string rel in rels.Prepend(""))
            {
                Type type = typeof(obj_machinery_light).Assembly.GetType("Anomaly.Core.Tg.obj_machinery_light" + rel.Replace('/', '_'));
                Assert.True(type != null, "no type for /obj/machinery/light" + rel);
                var lamp = (obj_machinery_light)Dm.@new(type, locate(x++, 2, 1));
                lamp.allow_break_on_init = FALSE; // light.dm:131: otherwise 2% (tube) / 5% (bulb) of them break at init
                made[rel] = lamp;
            }
        });
        var wrong = new List<string>();
        room.In(() =>
        {
            foreach (var (rel, lamp) in made)
            {
                foreach (string name in Compared)
                {
                    string raw = TgLightData.Lookup(parsed, rel, name);
                    if (raw == null) continue; // tg does not set it on this type: its root default, checked on the base
                    object expected = TgLightData.Resolve(raw);
                    object actual = PortValue(lamp.GetType().GetField(name)?.GetValue(lamp));
                    bool same = expected is double e && actual is double a ? Math.Abs(e - a) < 1e-4 : Equals(expected, actual);
                    if (!same) wrong.Add($"/obj/machinery/light{rel} {name}: tg {expected} port {actual}");
                }
            }
        });
        Assert.True(wrong.Count == 0, string.Join("\n", wrong.Take(15)) + $"\n({wrong.Count} wrong)");
        T1.NoRuntimes(room);
    }

    [TgFact]
    public void ALitFixtureSetsItsAtomsLightFromTheBulbAndBrokenOnesAreDark()
    {
        // light.dm:316-333 update(): on -> set_light(brightness, bulb_power, bulb_colour); LIGHT_BROKEN / BURNED / EMPTY -> on = FALSE
        // and set_light(l_range = 0) (:341-345); power_change (:722) turns it on for an area with light power and the switch on
        var parsed = TgLightData.Parse();
        string[] rels = { "", "/warm", "/cold/dim", "/red/dim", "/blacklight", "/small", "/small/red", "/broken", "/burned", "/empty", "/small/broken" };
        var made = new Dictionary<string, obj_machinery_light>();
        var room = T1.Room(width: 16, height: 6, seed: 5, mapExtras: w =>
        {
            int x = 2;
            foreach (string rel in rels)
            {
                Type type = typeof(obj_machinery_light).Assembly.GetType("Anomaly.Core.Tg.obj_machinery_light" + rel.Replace('/', '_'));
                var lamp = (obj_machinery_light)Dm.@new(type, locate(x++, 2, 1));
                lamp.allow_break_on_init = FALSE; // light.dm:131: no 2% / 5% break on init
                made[rel] = lamp;
            }
        });
        room.In(() =>
        {
            foreach (var (rel, lamp) in made)
            {
                bool ok = Dm.eq(lamp.status, LIGHT_OK);
                string at = "/obj/machinery/light" + rel;
                if (ok)
                {
                    Assert.True(Dm.truthy(lamp.on), at);
                    Assert.True(Dm.eq(lamp.light_range, (double)TgLightData.Resolve(TgLightData.Lookup(parsed, rel, "brightness") ?? "8")) , at);
                    Assert.True(Dm.eq(lamp.light_power, (double)TgLightData.Resolve(TgLightData.Lookup(parsed, rel, "bulb_power") ?? "1")), at);
                    Assert.Equal((string)TgLightData.Resolve(TgLightData.Lookup(parsed, rel, "bulb_colour") ?? "LIGHT_COLOR_DEFAULT"), lamp.light_color);
                    Assert.True(Dm.eq(lamp.light_dir, REVERSE_DIR(lamp.dir)), at); // light.dm:131 set_light(l_dir = REVERSE_DIR(dir))
                    Assert.True(Dm.truthy(lamp.light_on), at);
                }
                else
                {
                    Assert.False(Dm.truthy(lamp.on), at);
                    Assert.True(Dm.eq(lamp.light_range, 0), at);
                }
            }
        });
        T1.NoRuntimes(room);
    }

    private static obj_machinery_light Lamp(TgToyRoom room, int x = 4, int y = 2)
    {
        var lamp = room.In(() => Dm.@new<obj_machinery_light_directional_south>(locate(x, y, 1)));
        room.In(() => lamp.allow_break_on_init = FALSE);
        // the 2% break may have hit at creation: make it a good fixture
        room.In(() => { if (!Dm.eq(lamp.status, LIGHT_OK)) Waiting.NoSleep(lamp.fix()); });
        return lamp;
    }

    [Fact]
    public void BreakBurnOutAndFixMoveThroughTgsStatesAndEachReachesLightChanged()
    {
        var room = T1.Room();
        var sinks = RecordingSinks.Install(room.World);
        var lamp = Lamp(room);
        int Lines() => sinks.Lines.Count(l => l.EndsWith(" light " + RecordingSinks.Describe(lamp), StringComparison.Ordinal));
        room.In(() =>
        {
            Assert.True(Dm.eq(lamp.status, LIGHT_OK));
            Assert.True(Dm.truthy(lamp.on));
            int before = Lines();
            // burn_out (:393): status BURNED, icon "<base>-burned", off, set_light(l_range = 0)
            Waiting.NoSleep(lamp.burn_out());
            Assert.True(Dm.eq(lamp.status, LIGHT_BURNED));
            Assert.Equal("tube-burned", lamp.icon_state);
            Assert.False(Dm.truthy(lamp.on));
            Assert.True(Dm.eq(lamp.light_range, 0));
            Assert.True(Lines() > before, "burn_out must reach LightChanged");
            // break_light_tube (:690): a burned bulb breaks, BROKEN
            before = Lines();
            Waiting.NoSleep(lamp.break_light_tube(TRUE));
            Assert.True(Dm.eq(lamp.status, LIGHT_BROKEN));
            Assert.Equal("tube-broken", lamp.icon_state);
            // breaking a broken (or empty) one does nothing more (:691)
            int broken = Lines();
            Waiting.NoSleep(lamp.break_light_tube(TRUE));
            Assert.Equal(broken, Lines());
            // set_on cannot light a broken fixture (:405 `on = (turn_on && status == LIGHT_OK)`)
            Waiting.NoSleep(lamp.set_on(TRUE));
            Assert.False(Dm.truthy(lamp.on));
            // fix (:705): OK, brightness back to the initial, on = TRUE, update
            before = Lines();
            Waiting.NoSleep(lamp.fix());
            Assert.True(Dm.eq(lamp.status, LIGHT_OK));
            Assert.True(Dm.truthy(lamp.on));
            Assert.True(Dm.eq(lamp.light_range, 8));
            Assert.Equal("tube", lamp.icon_state);
            Assert.True(Lines() > before, "fix must reach LightChanged");
            // fix on a good fixture does nothing (`if(status == LIGHT_OK) return`)
            before = Lines();
            Waiting.NoSleep(lamp.fix());
            Assert.Equal(before, Lines());
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void TheAreasLightPowerAndSwitchTurnTheFixtureOnAndOff()
    {
        // light.dm:722 power_change: set_on(local_area.lightswitch && local_area.power_light); areas.dm:519 power_change sends
        // COMSIG_AREA_POWER_CHANGE, which the machine registered for in setup_area_power_relationship
        var room = T1.Room();
        var sinks = RecordingSinks.Install(room.World);
        var lamp = Lamp(room);
        room.In(() =>
        {
            area a = (area)get_area(lamp);
            Assert.True(Dm.truthy(a.lightswitch));
            Assert.True(Dm.truthy(a.power_light));
            a.lightswitch = FALSE; // turned off by hand: no emergency light either (turned_off())
            a.power_change();
            Assert.False(Dm.truthy(lamp.on));
            Assert.True(Dm.eq(lamp.light_range, 0));
            Assert.Equal("tube", lamp.icon_state);
            a.lightswitch = TRUE;
            a.power_change();
            Assert.True(Dm.truthy(lamp.on));
            Assert.True(Dm.eq(lamp.light_range, 8));
            a.power_light = FALSE; // no power: the emergency cell takes over (has_mock_cell), low power mode
            a.power_change();
            Assert.False(Dm.truthy(lamp.on));
            Assert.True(Dm.truthy(lamp.low_power_mode));
            Assert.Equal("tube_emergency", lamp.icon_state);
            a.power_light = TRUE;
            a.power_change();
            Assert.True(Dm.truthy(lamp.on));
            Assert.False(Dm.truthy(lamp.low_power_mode));
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ALampMountsOnTheWallItFacesAtMapLoadAndNotWhenMadeLater()
    {
        // light.dm:118 `if(mapload) find_and_mount_on_atom(mark_for_late_init = TRUE)`; get_turfs_to_mount_on (:122) is the step in dir
        obj_machinery_light on_wall = null, floating = null;
        var room = T1.Room(mapExtras: w =>
        {
            on_wall = Dm.@new<obj_machinery_light_directional_south>(locate(4, 2, 1)); // the south wall is the rim, y 1
            floating = Dm.@new<obj_machinery_light_directional_north>(locate(4, 4, 1)); // nothing north of it
        });
        room.In(() =>
        {
            var mount = on_wall.GetComponent(typeof(datum_component_atom_mounted)) as datum_component_atom_mounted;
            Assert.NotNull(mount);
            Assert.IsAssignableFrom<turf_closed>(mount.hanging_support_atom);
            Assert.Equal(locate(4, 1, 1), mount.hanging_support_atom);
        });
        T1.NoRuntimes(room);
    }
}
