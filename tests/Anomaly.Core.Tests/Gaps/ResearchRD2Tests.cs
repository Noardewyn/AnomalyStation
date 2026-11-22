using System.Text.Json;
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
/// Gaps package RD2 (D333, the R&amp;D consoles and servers) in a booted toy room with Bob as a player: the R&amp;D server mines the
/// station techweb's points while it works and halts when its control console disables it or an EMP hits it; the R&amp;D console opens
/// its Techweb window, researches a node with the web's points (logged, announced as said), queues and dequeues one, reads and writes
/// the technology disk and uploads a design disk; the server control console lists the servers and consoles and halts a server once
/// emagged; the master server's hard drive comes out after its four screws, the housing and six wires, halving the web's income;
/// the Nanotrasen Science Hub app researches like the console.
/// </summary>
public class ResearchRD2Tests
{
    private sealed record Lab(AudioRoom R, Feed Feed, mob_living_carbon_human Bob, datum_techweb Web);

    private static Lab Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        r.In(() => (get_area(locate(5, 4, 1)) as area).requires_power = FALSE); // the toy room has no APC
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        datum_techweb web = r.In(() => Dm.locate<datum_techweb_science>(SSresearch.techwebs));
        Assert.NotNull(web);
        return new Lab(r, feed, bob, web);
    }

    private static object Act(AudioRoom r, mob user, datum target, string action, DmList @params = null) => r.In(() =>
    {
        World w = World.Current;
        object caller = w.usr;
        w.usr = user;
        try
        {
            var ui = Dm.@new<datum_tgui>(user, target, "Test"); // an unopened window: the machines' ui_act reads its user (_machinery.dm:713)
            return Waiting.NoSleep(target.ui_act(action, @params ?? Dm.list(), ui, ui.state));
        }
        finally
        {
            w.usr = caller;
        }
    });

    private static float Points(Lab l) => l.R.In(() => Dm.num(l.Web.research_points[TECHWEB_POINT_TYPE_GENERIC]));

    // an available node that costs points and needs no experiment
    private static Type AvailableNode(Lab l, Type except = null) => l.R.In(() =>
    {
        foreach (object path in Dm.each(l.Web.available_nodes))
        {
            if (path is not Type type || type == except || Dm.truthy(l.Web.researched_nodes[path]))
                continue;
            var node = SSresearch.techweb_nodes[path] as datum_techweb_node;
            if (node != null && !node.is_free(l.Web) && l.Web.have_experiments_for_node(node))
                return type;
        }
        return null;
    });

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    [Fact]
    public void TheServerMinesTheWebsPointsUntilItIsDisabledOrHitByAnEmp()
    {
        Lab l = Room(7601);
        var server = Place<obj_machinery_rnd_server>(l.R, 7, 4);
        l.R.In(() =>
        {
            Assert.Contains(server, Dm.each(l.Web.techweb_servers));
            Assert.Same(l.Web, server.stored_research);
            Assert.Matches("R&D Server [0-9a-f]{1,4}$", DmTextRender.Plain(server.name));
            Assert.Equal(TRUE, server.working);
            Assert.Equal("Nominal", server.get_status_text());
            Assert.Equal("RD-server-on", server.icon_state);
        });

        // a working server: SSresearch's fire pays the web its income for the time since the last
        l.R.In(() => Waiting.NoSleep(SSresearch.fire()));
        float before = Points(l);
        l.R.Step(2 * Second);
        l.R.In(() => Waiting.NoSleep(SSresearch.fire()));
        Assert.True(Points(l) > before);

        // halted by its control console: no income, the halt picture
        l.R.In(() => server.toggle_disable(l.Bob));
        l.R.In(() =>
        {
            Assert.Equal(FALSE, server.working);
            Assert.Equal("Offline - Server Control Disabled", server.get_status_text());
            Assert.Equal("RD-server-halt", server.icon_state);
            Waiting.NoSleep(SSresearch.fire());
        });
        before = Points(l);
        l.R.Step(2 * Second);
        l.R.In(() => Waiting.NoSleep(SSresearch.fire()));
        Assert.Equal(before, Points(l));
        l.R.In(() => server.toggle_disable(l.Bob));
        Assert.Equal(TRUE, l.R.In(() => server.working));

        // an EMP scrambles it for a minute
        l.R.In(() => Waiting.NoSleep(server.emp_act(EMP_HEAVY)));
        l.R.In(() =>
        {
            Assert.Equal("O&F@I*$ - R3*&O$T R@U!R%D", server.get_status_text());
            Assert.Equal(FALSE, server.working);
        });
        l.R.Step(61 * Second);
        l.R.In(() =>
        {
            Assert.Equal("Nominal", server.get_status_text());
            Assert.Equal(TRUE, server.working);
        });
        l.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheConsoleOpensItsTechwebWindowAndResearchesQueuesAndDequeuesNodes()
    {
        Lab l = Room(7602);
        var console = Place<obj_machinery_computer_rdconsole>(l.R, 5, 4);
        l.R.In(() =>
        {
            Assert.Same(l.Web, console.stored_research);
            Assert.Contains(console, Dm.each(l.Web.consoles_accessing));
        });

        // the window: its static data carry the compressed node and design caches
        Take(l.Bob);
        Click(l.R, l.Bob, console);
        JsonElement payload = LastUpdate(Take(l.Bob), out _);
        Assert.Equal("Techweb", InterfaceOf(payload));
        JsonElement data = payload.GetProperty("data");
        Assert.True(data.GetProperty("stored_research").GetInt32() == 1);
        Assert.True(data.GetProperty("locked").GetInt32() == 1); // the board starts locked
        JsonElement staticData = payload.GetProperty("static_data").GetProperty("static_data");
        Assert.True(staticData.GetProperty("node_cache").EnumerateObject().Count() > 100);
        Assert.True(staticData.GetProperty("design_cache").EnumerateObject().Count() > 1000);
        Assert.True(data.GetProperty("nodes").GetArrayLength() > 0);

        // locked: nothing but the lock, which wants research access
        Type node = AvailableNode(l);
        Assert.NotNull(node);
        Assert.Equal(TRUE, Act(l.R, l.Bob, console, "researchNode", Dm.list(Dm.kv("node_path", Dm.text("[]", node)))));
        Assert.False(l.R.In(() => Dm.truthy(l.Web.researched_nodes[node])));
        Act(l.R, l.Bob, console, "toggleLock");
        Assert.Contains(l.Feed.Chat, m => m.Html.Contains("Unauthorized Access."));
        l.R.In(() => ((obj_item_circuitboard_computer_rdconsole)console.circuit_value).locked = FALSE);

        // too poor, then researched with the web's points: logged with its researcher and place
        l.R.In(() => l.Web.research_points[TECHWEB_POINT_TYPE_GENERIC] = 0);
        Act(l.R, l.Bob, console, "researchNode", Dm.list(Dm.kv("node_path", Dm.text("[]", node))));
        Assert.False(l.R.In(() => Dm.truthy(l.Web.researched_nodes[node])));
        l.R.In(() => l.Web.adjust_points(TECHWEB_POINT_TYPE_GENERIC, 1000000));
        Act(l.R, l.Bob, console, "researchNode", Dm.list(Dm.kv("node_path", Dm.text("[]", node))));
        l.R.Step(4);
        string name = l.R.In(() => Dm.str(((datum_techweb_node)SSresearch.techweb_nodes[node]).display_name));
        l.R.In(() =>
        {
            Assert.True(Dm.truthy(l.Web.researched_nodes[node]));
            DmList log = Dm.each(l.Web.research_logs).Last() as DmList;
            Assert.Equal(name, log["node_name"]);
            Assert.Equal("Unknown", log["node_researcher"]);
        });
        Assert.Contains(l.Feed.Chat, m => m.Html.Contains("Successfully researched " + name + "."));

        // a queue: enqueued by Bob, then dequeued
        Type queued = AvailableNode(l, except: node);
        Assert.NotNull(queued);
        Act(l.R, l.Bob, console, "enqueueNode", Dm.list(Dm.kv("node_path", Dm.text("[]", queued))));
        Assert.True(l.R.In(() => Dm.truthy(l.Web.research_queue_nodes[queued])));
        Act(l.R, l.Bob, console, "dequeueNode", Dm.list(Dm.kv("node_path", Dm.text("[]", queued))));
        Assert.False(l.R.In(() => Dm.truthy(l.Web.research_queue_nodes[queued])));
        l.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheConsoleReadsAndWritesATechnologyDiskAndUploadsADesignDisk()
    {
        Lab l = Room(7603);
        var console = Place<obj_machinery_computer_rdconsole>(l.R, 5, 4);
        l.R.In(() => ((obj_item_circuitboard_computer_rdconsole)console.circuit_value).locked = FALSE);

        // the technology disk starts with the starter nodes; inserted, then written with the web's researched nodes
        var techDisk = l.R.In(() => Dm.@new<obj_item_disk_tech_disk>(l.Bob.loc));
        l.R.Step(2);
        l.R.In(() =>
        {
            Assert.Equal(Dm.length(SSresearch.techweb_nodes_starting), Dm.length(techDisk.stored_nodes));
            Waiting.NoSleep(l.Bob.put_in_hands(techDisk));
        });
        Assert.Equal(ITEM_INTERACT_SUCCESS, l.R.In(() => Waiting.NoSleep(console.item_interaction(l.Bob, techDisk))));
        Assert.Same(techDisk, l.R.In(() => console.t_disk));
        Act(l.R, l.Bob, console, "loadTech");
        l.R.In(() =>
        {
            foreach (object researched in Dm.each(l.Web.researched_nodes))
                Assert.True(Dm.truthy(techDisk.stored_nodes[researched]));
        });
        Assert.Contains(l.Feed.Chat, m => m.Html.Contains("Downloading to technology disk."));
        Act(l.R, l.Bob, console, "loadTech"); // the copy cooldown
        Assert.Contains(l.Feed.Chat, m => m.Html.Contains("Servers busy!"));
        Act(l.R, l.Bob, console, "ejectDisk", Dm.list(Dm.kv("type", "tech")));
        l.R.In(() =>
        {
            Assert.Null(console.t_disk);
            Assert.Same(get_turf(console), techDisk.loc);
        });

        // a design disk's blueprints join the web's designs
        Type design = typeof(datum_design_knight_armour);
        var designDisk = l.R.In(() => Dm.@new<obj_item_disk_design_disk_knight_gear>(l.Bob.loc));
        l.R.Step(2);
        l.R.In(() => Waiting.NoSleep(l.Bob.put_in_hands(designDisk)));
        Assert.Equal(ITEM_INTERACT_SUCCESS, l.R.In(() => Waiting.NoSleep(console.item_interaction(l.Bob, designDisk))));
        Assert.False(l.R.In(() => Dm.truthy(l.Web.researched_designs[design])));
        Act(l.R, l.Bob, console, "uploadDisk", Dm.list(Dm.kv("type", "design")));
        Assert.True(l.R.In(() => Dm.truthy(l.Web.researched_designs[design])));
        Assert.Contains(l.Feed.Chat, m => m.Html.Contains("Uploading blueprints from disk."));
        l.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheServerControlConsoleListsServersAndConsolesAndHaltsAServerOnceEmagged()
    {
        Lab l = Room(7604);
        var server = Place<obj_machinery_rnd_server>(l.R, 8, 4);
        var console = Place<obj_machinery_computer_rdconsole>(l.R, 8, 6);
        var control = Place<obj_machinery_computer_rdservercontrol>(l.R, 5, 4);
        Assert.Same(l.Web, l.R.In(() => control.stored_research));

        Take(l.Bob);
        Click(l.R, l.Bob, control);
        JsonElement payload = LastUpdate(Take(l.Bob), out _);
        Assert.Equal("ServerControl", InterfaceOf(payload));
        JsonElement data = payload.GetProperty("data");
        Assert.Equal(1, data.GetProperty("server_connected").GetInt32());
        JsonElement listed = data.GetProperty("servers").EnumerateArray().Single();
        Assert.Equal("Nominal", listed.GetProperty("server_details").GetString());
        Assert.Contains("R&D Server", listed.GetProperty("server_name").GetString());
        Assert.Equal("R&D Console", data.GetProperty("consoles").EnumerateArray().Single().GetProperty("console_name").GetString());

        // no research director's access: refused; emagged: the server halts
        string serverRef = l.R.In(() => Dm.str(REF(server)));
        Act(l.R, l.Bob, control, "lockdown_server", Dm.list(Dm.kv("selected_server", serverRef)));
        Assert.Equal(FALSE, l.R.In(() => server.research_disabled));
        l.R.In(() => Waiting.NoSleep(control.emag_act(l.Bob, null)));
        Act(l.R, l.Bob, control, "lockdown_server", Dm.list(Dm.kv("selected_server", serverRef)));
        l.R.In(() =>
        {
            Assert.Equal(TRUE, server.research_disabled);
            Assert.Equal("RD-server-halt", server.icon_state);
        });

        // and it locks a console
        string consoleRef = l.R.In(() => Dm.str(REF(console)));
        bool lockedBefore = l.R.In(() => Dm.truthy(((obj_item_circuitboard_computer_rdconsole)console.circuit_value).locked));
        Act(l.R, l.Bob, control, "lock_console", Dm.list(Dm.kv("selected_console", consoleRef)));
        Assert.NotEqual(lockedBefore, l.R.In(() => Dm.truthy(((obj_item_circuitboard_computer_rdconsole)console.circuit_value).locked)));
        l.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheMasterServersHardDriveComesOutAfterItsScrewsHousingAndWiresAndHalvesTheIncome()
    {
        Lab l = Room(7605);
        var master = Place<obj_machinery_rnd_server_master>(l.R, 5, 4);
        l.R.In(() =>
        {
            string master_name = DmTextRender.Plain(master.name); // tg's "\improper Master " + name: the text macros' markers stay in the text
            Assert.Contains("Master ", master_name);
            Assert.Contains("R&D Server", master_name);
            Assert.NotNull(master.source_code_hdd);
        });
        Assert.Contains("RD-server-objective-stripes", Overlays(l.R, master));
        float income = l.R.In(() => l.Web.income_modifier);

        // only an antagonist knows where the hatch is
        var screwdriver = l.R.In(() => Dm.@new<obj_item_screwdriver>(l.Bob.loc));
        l.R.Step(2);
        l.R.In(() => Waiting.NoSleep(l.Bob.put_in_hands(screwdriver)));
        l.R.Step(Second);
        Click(l.R, l.Bob, master);
        l.R.Step(10 * Second);
        Assert.Equal(0f, l.R.In(() => master.deconstruction_state));

        // the four screws (each breaks), the housing pried, the six wires cut
        l.R.In(() => Waiting.Detach(master.screwdriver_act(l.Bob, screwdriver)));
        l.R.Step(32 * Second);
        Assert.Equal(1f, l.R.In(() => master.deconstruction_state));
        Assert.Contains("RD-server-hdd-panel-open", Overlays(l.R, master));
        Assert.Contains(l.Feed.Chat, m => m.Html.Contains("The screw breaks as you remove it. Only 3 left..."));

        var crowbar = l.R.In(() => Dm.@new<obj_item_crowbar>(l.Bob.loc));
        l.R.In(() => Waiting.Detach(master.crowbar_act(l.Bob, crowbar)));
        l.R.Step(16 * Second);
        Assert.Equal(2f, l.R.In(() => master.deconstruction_state));

        obj_item_disk_computer_hdd_theft hdd = l.R.In(() => master.source_code_hdd);
        var wirecutters = l.R.In(() => Dm.@new<obj_item_wirecutters>(l.Bob.loc));
        l.R.In(() => Waiting.Detach(master.wirecutter_act(l.Bob, wirecutters)));
        l.R.Step(47 * Second);
        l.R.In(() =>
        {
            Assert.Equal(3f, master.deconstruction_state);
            Assert.Null(master.source_code_hdd);
            Assert.Same(l.Bob, hdd.loc);
            Assert.Equal(income * 0.5f, l.Web.income_modifier);
        });
        Assert.Contains(l.Feed.Chat, m => m.Html.Contains("You cut the final wire and remove"));
        l.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheScienceHubAppResearchesANodeOnItsTechweb()
    {
        Lab l = Room(7606);
        var pda = l.R.In(() => Dm.@new<obj_item_modular_computer_pda_crew_science>(locate(4, 4, 1)));
        l.R.Step(Second);
        var app = l.R.In(() =>
        {
            var found = Dm.each<datum_computer_file_program_science>(pda.stored_files).FirstOrDefault();
            if (found == null)
            {
                found = Dm.@new<datum_computer_file_program_science>();
                Assert.True(Dm.truthy(pda.store_file(found)));
            }
            return found;
        });
        Assert.Same(l.Web, l.R.In(() => app.stored_research));

        Type node = AvailableNode(l);
        l.R.In(() => l.Web.adjust_points(TECHWEB_POINT_TYPE_GENERIC, 1000000));
        Assert.Equal(TRUE, Act(l.R, l.Bob, app, "researchNode", Dm.list(Dm.kv("node_path", Dm.text("[]", node)))));
        Assert.True(l.R.In(() => Dm.truthy(l.Web.researched_nodes[node])));

        // its own lock wants the inserted ID's research access
        l.R.In(() => app.locked = TRUE);
        Act(l.R, l.Bob, app, "toggleLock");
        Assert.Equal(TRUE, l.R.In(() => app.locked));
        Assert.Contains(l.Feed.Chat, m => m.Html.Contains("Unauthorized Access. Please insert research ID card."));
        l.R.In(() =>
        {
            DmList data = Waiting.NoSleep(app.ui_data(l.Bob)) as DmList;
            Assert.Equal(TRUE, data["locked"]);
            Assert.Null(data["t_disk"]);
        });
        l.R.AssertNoRuntimes();
    }
}
