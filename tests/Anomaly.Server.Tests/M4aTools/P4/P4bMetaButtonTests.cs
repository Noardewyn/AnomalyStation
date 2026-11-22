using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Server.Tests.Integration;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.M4aTools.P4;

/// <summary>
/// M4a P4b: on the real server's Meta, a mapped door button (Medbay's pharmacy shutters control, MetaStation.dmm's
/// id = "pharmacy_shutters", req_access = list("medical")) gets its blast door controller at init, and a player's click on
/// it moves every pod door of its id (Meta maps them open: the click closes them); the next click, after the controller's
/// cooldown, moves them back.
/// </summary>
public class P4bMetaButtonTests
{
    private const string Id = "pharmacy_shutters";
    private readonly ITestOutputHelper _output;

    public P4bMetaButtonTests(ITestOutputHelper output) => _output = output;

    [MetaServerFact]
    public void A_Meta_button_moves_its_pod_doors_with_a_click_and_back_with_the_next()
    {
        using var world = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 4000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = world.Join("Presser");
        world.Host.EnqueueConsoleLine("give Presser /obj/item/card/id/advanced/debug");
        world.Run(2);

        obj_machinery_button_door button = null;
        bool startClosed = false;
        var doors = new List<obj_machinery_door_poddoor>();
        mob_living presser = world.MobOf(p);
        using (world.World.Enter())
        {
            int buttons = 0, withControllers = 0;
            foreach (obj_machinery m in Dm.each_cast<obj_machinery>(GlobalProcs.SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_button))))
            {
                if (m is not obj_machinery_button b)
                    continue;
                buttons++;
                if (b.device_instance != null) withControllers++;
                if (button == null && b is obj_machinery_button_door door && Dm.eq(door.id, Id))
                    button = door;
            }
            foreach (obj_machinery_door_poddoor d in Dm.each_cast<obj_machinery_door_poddoor>(GlobalProcs.SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_door_poddoor))))
            {
                if (Dm.eq(d.id, Id))
                    doors.Add(d);
            }
            _output.WriteLine($"buttons {buttons}, with a controller {withControllers}; '{Id}' at ({button?.x},{button?.y}), its pod doors {doors.Count}");
            Assert.True(buttons > 100, "buttons " + buttons); // MetaStation.dmm has 140
            Assert.NotNull(button);
            var control = Assert.IsType<obj_item_assembly_control_blast_door>(button.device_instance);
            Assert.Equal(Id, control.id);
            Assert.NotEmpty(doors);
            // Meta maps them preopen (shutters/preopen, D117: mapped state kept); one state for all of them
            startClosed = Dm.truthy(doors[0].density);
            Assert.All(doors, d => Assert.Equal(startClosed, Dm.truthy(d.density)));
            Assert.False(Dm.truthy(Dm.bitand(button.machine_stat, NOPOWER)), "the button is powered");

            // the presser stands on the button's tile with the debug card in the other hand
            Waiting.NoSleep(presser.forceMove(button.loc as atom));
            presser.swap_hand();
            Assert.Null(presser.get_active_held_item());
        }

        void Click()
        {
            using (world.World.Enter())
                ClientSeam.Click(presser.client, button, "left=1;button=left");
            world.Run(40);
        }

        // the controller syncs the doors to the first one's state (sync_doors): the click flips them all
        Click();
        using (world.World.Enter())
            Assert.All(doors, d => Assert.Equal(!startClosed, Dm.truthy(d.density)));

        Click();
        using (world.World.Enter())
            Assert.All(doors, d => Assert.Equal(startClosed, Dm.truthy(d.density)));

        // nothing of the buttons' files stopped at a runtime or a not-translated stub (deferred leaves are expected) on Meta's boot or the presses
        string[] mine = { "buttons.dm", "doorcontrol.dm", "assembly.dm", "poddoor.dm" };
        string[] lines = (world.Out.ToString() + world.Err.ToString()).Split('\n');
        var hits = lines.Where(l => !l.StartsWith("Deferred", StringComparison.Ordinal) && mine.Any(f => l.Contains(f, StringComparison.Ordinal))).ToList();
        foreach (string l in hits.Take(10)) _output.WriteLine(l);
        Assert.Empty(hits);
    }
}
