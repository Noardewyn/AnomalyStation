using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Engineering;

/// <summary>
/// Engineering package E14: the station blueprints (blueprints.dm) and tg's area helpers (__HELPERS/areas.dm) in a booted toy room. A
/// wall splits the room; the east part is a station area ("Test Wing") with an air alarm. From inside it the blueprints make a new area
/// of the sealed room (detect_room's turfs, the custom_area component, GLOB.custom_areas; the emptied old area is deleted) and the air
/// alarm follows it (my_area, its name), or expand an existing area over the room; the room too big for an area is refused with tg's
/// percentage; renaming renames the area's machines; the structural view gives the roundstart cable's image to the viewer's client; the
/// wire legend reads GLOB.wire_color_directory.
/// </summary>
public class BlueprintsTests
{
    private const int Second = 20;
    private const int WallX = 8; // the splitting wall; the wing is x 9 to 12, y 2 to 8

    private sealed class Feed : IMessageSink
    {
        public List<(object Target, string Html)> Chat { get; } = new();

        void IMessageSink.Chat(object target, DmList message) => Chat.Add((target, Dm.str(message?["html"])));

        public void Output(object target, string text) { }

        public void Balloon(object viewer, object atom, string text) => Chat.Add((viewer, "balloon: " + text));

        public bool Any(string text) => Chat.Any(c => c.Html != null && c.Html.Contains(text, StringComparison.Ordinal));
    }

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Bob, obj_item_blueprints Prints, area Wing, Feed Feed);

    // a 13 x 9 room split by a wall at x 8; the east part is a fresh station area named "Test Wing" (requires_power, as /area)
    private static Rig Room(ulong seed, int width = 13, int height = 9, bool split = true, Action<World> mapExtras = null)
    {
        var r = new AudioRoom(width, height, seed: seed, mapExtras: mapExtras);
        var feed = new Feed();
        PresentationSinks.Of(r.World).Messages = feed;
        area wing = null;
        if (split)
        {
            r.In(() =>
            {
                for (int y = 2; y <= height - 1; y++)
                    Waiting.NoSleep(((turf)locate(WallX, y, 1)).ChangeTurf(typeof(turf_closed_indestructible)));
                wing = Dm.@new<area>();
                wing.setup("Test Wing");
                DmList turfs = Dm.list();
                for (int x = WallX + 1; x <= width - 1; x++)
                    for (int y = 2; y <= height - 1; y++)
                        Dm.append(turfs, locate(x, y, 1));
                set_turfs_to_area(turfs, wing);
                wing.reg_in_areas_in_z();
            });
        }
        mob_living_carbon_human bob = r.Join(split ? 10 : 4, 4, "Bob");
        r.Step(Second);
        obj_item_blueprints prints = r.In(() =>
        {
            var p = Dm.@new<obj_item_blueprints>(bob.loc);
            Waiting.NoSleep(bob.put_in_hands(p));
            return p;
        });
        r.Step(2);
        return new Rig(r, bob, prints, wing, feed);
    }

    private static DmList Data(Rig g) => g.R.In(() => (DmList)Waiting.NoSleep(g.Prints.ui_data(g.Bob)));

    private static DmList Static(Rig g) => g.R.In(() => (DmList)Waiting.NoSleep(g.Prints.ui_static_data(g.Bob)));

    // an act of the blueprints' window; create_area and edit_area wait for tg's input windows, so the act runs detached
    private static void Act(Rig g, string action, params (string Key, object Value)[] args) => g.R.In(() =>
    {
        usr = g.Bob;
        datum_tgui ui = (datum_tgui)SStgui.get_open_ui(g.Bob, g.Prints);
        if (ui == null)
        {
            Waiting.NoSleep(g.Prints.ui_interact(g.Bob));
            ui = (datum_tgui)SStgui.get_open_ui(g.Bob, g.Prints);
        }
        DmList parameters = Dm.list();
        foreach ((string key, object value) in args)
            parameters[key] = value;
        spawn(-1, async () => await g.Prints.ui_act(action, parameters, ui, null));
    });

    // the answer to tg's input window of that interface (ListInputWindow, TextInputModal) the player has open
    private static void Answer(Rig g, string iface, object entry)
    {
        g.R.Step(2);
        string window = g.R.In(() => Dm.str(Dm.each_cast<datum_tgui>(g.Bob.tgui_open_uis).Last(u => Dm.str(u.@interface) == iface).window.id));
        g.R.Command(() => ClientSeam.TguiMessage(g.Bob.client, window, "act/submit", Dm.list(Dm.kv("entry", entry))));
        g.R.Step(4);
    }

    private static DmList ListItems(Rig g) => g.R.In(() =>
    {
        datum_tgui ui = Dm.each_cast<datum_tgui>(g.Bob.tgui_open_uis).Last(u => Dm.str(u.@interface) == "ListInputWindow");
        return ((datum_tgui_list_input)ui.src_object).items;
    });

    private static area AreaAt(Rig g, int x, int y) => g.R.In(() => get_area(locate(x, y, 1)) as area);

    [Fact]
    public void TheWindowReadsTheAreaTheHolderStandsIn()
    {
        Rig g = Room(14001);
        DmList data = Data(g);
        Assert.Equal("You are now in Test Wing", data["area_notice"]);
        Assert.Equal("Test Wing", data["area_name"]);
        Assert.Equal((0f, "off", 0f), (Dm.num(data["area_allows_shuttle_docking"]), Dm.str(data["legend"]), Dm.num(data["viewing"])));
        DmList statics = Static(g);
        Assert.Equal(("watching_list", "off"), (Dm.str(statics["legend_viewing_list"]), Dm.str(statics["legend_off"])));
        Assert.Equal("Property of Nanotrasen. For heads of staff only. Store in high-secure storage.", statics["fluff_notice"]);
        Assert.Equal(g.R.In(() => Dm.str(station_name())), statics["station_name"]);
        g.R.In(() => Assert.Equal(AREA_STATION_VALUE, Dm.num(g.Prints.get_area_type(g.Bob))));

        // the docking toggle flips the area's allow_shuttle_docking
        Act(g, "toggle_allow_shuttle_docking");
        g.R.Step(2);
        Assert.Equal(1f, g.R.In(() => g.Wing.allow_shuttle_docking));
        g.R.AssertNoRuntimes();
    }

    private const float AREA_STATION_VALUE = 1; // blueprints.dm:2

    [Fact]
    public void ANewAreaTakesTheSealedRoomAndItsAirAlarmFollows()
    {
        Rig g = Room(14002);
        var alarm = g.R.In(() => Dm.@new<obj_machinery_airalarm>(locate(12, 5, 1)));
        g.R.Step(Second);
        Assert.Same(g.Wing, g.R.In(() => alarm.my_area));
        Assert.Equal("Test Wing Air Alarm", g.R.In(() => alarm.name));

        Act(g, "create_area");
        // tg's choice: a new area or the station areas of the room (the test room needs no power: not offered)
        Assert.Equal(new[] { "New Area", "Test Wing" }, g.R.In(() => Dm.each(ListItems(g)).Select(Dm.str).ToArray()));
        Answer(g, "ListInputWindow", "New Area");
        Answer(g, "TextInputModal", "Engine Den");

        area den = AreaAt(g, 10, 4);
        Assert.NotSame(g.Wing, den);
        Assert.Equal("Engine Den", g.R.In(() => den.name));
        g.R.In(() =>
        {
            // every tile of the room, and the walls around it, moved into the new area; the rest of the test room stays
            for (int x = WallX + 1; x <= 12; x++)
                for (int y = 2; y <= 8; y++)
                    Assert.Same(den, get_area(locate(x, y, 1)));
            Assert.Same(den, get_area(locate(WallX, 5, 1)));
            Assert.IsType<area_misc_testroom>(get_area(locate(4, 4, 1)));
            Assert.Equal(typeof(area), den.type);
            var custom = den.GetComponent(typeof(datum_component_custom_area)) as datum_component_custom_area;
            Assert.NotNull(custom);
            Assert.Same(g.Wing, custom.previous_areas[locate(10, 4, 1)]);
            Assert.True(Dm.truthy(GLOB.custom_areas[den]));
            // the old area lost every turf: deleted (areas.dm:203)
            Assert.True(QDELETED(g.Wing));
            // the air alarm left the old area and entered the new one (_air_alarm.dm:174, :187)
            Assert.Same(den, alarm.my_area);
            Assert.Equal("Engine Den Air Alarm", alarm.name);
            Assert.Equal(0f, g.Prints.in_use);
        });
        Assert.True(g.Feed.Any("You have created a new area, named Engine Den. It is now weather proof, and constructing an APC will allow it to be powered."));

        // renaming: the area and the machines named after it (set_area_machinery_title)
        Act(g, "edit_area");
        Answer(g, "TextInputModal", "Den Two");
        Assert.Equal("Den Two", g.R.In(() => den.name));
        Assert.Equal("Den Two Air Alarm", g.R.In(() => alarm.name));
        Assert.True(g.Feed.Any("balloon: area renamed to Den Two"));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AnExistingAreaExpandsOverTheRoomAndTheSameAreaIsRefused()
    {
        Rig g = Room(14003);
        // the room's west half goes back to the test room's area, then the wing expands over the whole room again
        area testroom = AreaAt(g, 4, 4);
        g.R.In(() =>
        {
            DmList west = Dm.list();
            for (int x = WallX + 1; x <= 10; x++)
                for (int y = 2; y <= 8; y++)
                    Dm.append(west, locate(x, y, 1));
            set_turfs_to_area(west, testroom);
        });
        Assert.Same(testroom, AreaAt(g, 10, 4));
        Act(g, "create_area");
        Answer(g, "ListInputWindow", "Test Wing");
        g.R.In(() =>
        {
            for (int x = WallX + 1; x <= 12; x++)
                for (int y = 2; y <= 8; y++)
                    Assert.Same(g.Wing, get_area(locate(x, y, 1)));
            Assert.False(QDELETED(testroom)); // it keeps the west room
            Assert.Null(g.Wing.GetComponent(typeof(datum_component_custom_area)));
        });

        // standing in the wing, choosing the wing changes nothing
        Act(g, "create_area");
        Answer(g, "ListInputWindow", "Test Wing");
        Assert.True(g.Feed.Any("Selected choice is same as the area your standing in. No area changes were requested."));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ARoomTooBigForAnAreaIsRefusedWithTgsPercentage()
    {
        // 18 x 18 tiles and their 76 walls: 400 turfs, a third more than BP_MAX_ROOM_SIZE (300)
        Rig g = Room(14004, width: 20, height: 20, split: false);
        Act(g, "create_area");
        g.R.Step(4);
        Assert.True(g.Feed.Any("The room you're in is too big. It is 33.3333% larger than allowed."), string.Join(" | ", g.Feed.Chat.Select(c => c.Html)));
        Assert.Empty(g.R.In(() => Dm.each_cast<datum_tgui>(g.Bob.tgui_open_uis).Where(u => Dm.str(u.@interface) == "ListInputWindow").ToList()));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void DetectRoomStopsAtWallsAndFailsOnSpace()
    {
        Rig g = Room(14005);
        g.R.In(() =>
        {
            DmList room = Waiting.NoSleep(detect_room((turf)locate(10, 4, 1))) as DmList;
            // the 28 tiles of the wing and the 26 walls around them (the diagonal corners too)
            Assert.Equal(28 + 26, Dm.length(room));
            Assert.True(Dm.@in(locate(WallX, 1, 1), room));
            Assert.False(Dm.@in(locate(7, 4, 1), room));
            // a wall's own room is the wall
            Assert.Equal(1f, Dm.length(Waiting.NoSleep(detect_room((turf)locate(WallX, 4, 1))) as DmList));
            // break_if_found: a space tile in the room fails the search
            Waiting.NoSleep(((turf)locate(11, 6, 1)).ChangeTurf(typeof(turf_open_space)));
            Assert.Equal(FALSE, Dm.num(Waiting.NoSleep(detect_room((turf)locate(10, 4, 1), typecacheof(typeof(turf_open_space)) as DmList))));
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheStructuralViewShowsTheRoundstartCablesToTheViewerOnly()
    {
        // a cable laid before SSicon_smooth initializes takes its blueprint image (turf.dm:622, icon_smooth.dm:56)
        Rig g = Room(14006, mapExtras: _ => Dm.@new<obj_structure_cable>(locate(10, 5, 1)));
        image shown = g.R.In(() =>
        {
            turf at = (turf)locate(10, 5, 1);
            Assert.Equal(1f, Dm.length(at.blueprint_data));
            var I = (image)at.blueprint_data[1];
            obj_structure_cable cable = Dm.locate<obj_structure_cable>(at);
            Assert.Equal((128f, cable.icon_state, at), (I.alpha, I.icon_state, I.loc));
            return I;
        });
        Act(g, "view_blueprints");
        g.R.Step(2);
        g.R.In(() =>
        {
            Assert.Contains(shown, Dm.each(g.Bob.client.images));
            Assert.Same(g.Bob.client, g.Prints.viewing);
        });
        Assert.Equal(1f, Dm.num(Data(g)["viewing"]));
        Assert.True(g.Feed.Any("balloon: flips blueprints over"));

        Act(g, "hide_blueprints");
        g.R.Step(2);
        g.R.In(() =>
        {
            Assert.DoesNotContain(shown, Dm.each(g.Bob.client.images));
            Assert.Null(g.Prints.viewing);
        });

        // dropping the blueprints clears the view too (blueprints.dm:50)
        Act(g, "view_blueprints");
        g.R.Step(2);
        g.R.In(() => Waiting.NoSleep(g.Bob.dropItemToGround(g.Prints)));
        g.R.In(() => Assert.DoesNotContain(shown, Dm.each(g.Bob.client.images)));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheWireLegendListsTheRoundsWireSetsWithoutTheDuds()
    {
        Rig g = Room(14007);
        g.R.In(() => Dm.@new<obj_machinery_airalarm>(locate(12, 5, 1)));
        g.R.Step(2);
        DmList devices = Static(g)["wire_devices"] as DmList;
        DmList alarm = Dm.each_cast<DmList>(devices).Single(d => Dm.eq(d["ref"], typeof(obj_machinery_airalarm)));
        Assert.Equal("Air Alarm", alarm["name"]);

        Act(g, "view_legend");
        g.R.Step(2);
        Assert.Equal("watching_list", Data(g)["legend"]);
        // the key is the holder's type (_wires.dm:88), its text the window's ref
        Act(g, "view_wireset", ("view_wireset", "/obj/machinery/airalarm"));
        g.R.Step(2);
        DmList data = Data(g);
        Assert.Equal("/obj/machinery/airalarm", data["legend"]);
        Assert.Equal("Air Alarm", data["wires_name"]);
        DmList colors = g.R.In(() => GLOB.wire_color_directory[typeof(obj_machinery_airalarm)] as DmList);
        var expected = g.R.In(() => Dm.each(colors).Where(c => !Dm.truthy(findtext(colors[c], WIRE_DUD_PREFIX))).Select(c => (Dm.str(c), Dm.str(colors[c]))).ToList());
        Assert.Equal(expected, Dm.each_cast<DmList>(data["wire_data"] as DmList).Select(w => (Dm.str(w["color"]), Dm.str(w["message"]))).ToList());
        Assert.NotEmpty(expected);

        Act(g, "exit_legend");
        g.R.Step(2);
        Assert.Equal("off", Data(g)["legend"]);
        g.R.AssertNoRuntimes();
    }
}
