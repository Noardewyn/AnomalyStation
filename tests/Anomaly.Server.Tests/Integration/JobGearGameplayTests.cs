using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps package J5 (D298) on MetaStation: a doctor's surgical tools and folded body bag, and an engineer's power tools: the client sees the bag unfold and the crew monitor's window.
/// </summary>
public class JobGearGameplayTests
{
    private const int Second = 20; // ticks per second

    private readonly ITestOutputHelper _output;

    public JobGearGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void eng_lights(RealHeadlessWorld w, RealHeadlessWorld.Player q)
    {
        q.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        q.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
    }

    private static ClickTarget On(atom a) => new ClickTarget(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    [MetaServerFact]
    public void A_doctor_unfolds_a_body_bag_and_reads_the_crew_monitor_and_an_engineer_switches_his_hand_drill()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 16, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Doctor");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;

        // the body bag: Z in hand unfolds it on my tile; the client sees it with tg's sprite
        w.Host.EnqueueConsoleLine("give Doctor /obj/item/bodybag");
        w.Run(5);
        Assert.IsType<obj_item_bodybag>(In(w, () => me.get_active_held_item()));
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        var bag = In(w, () => Dm.each<obj_structure_closet_body_bag>(get_turf(me)).Single());
        Assert.True(In(w, () => Dm.truthy(bag.opened)));
        Assert.True(replica.TryGet(In(w, () => (long)bag.dm_id), out ReplicaEntity seen), "the client does not see the bag");
        _output.WriteLine("bag: " + string.Join(", ", CrewOutfitTests.Icons(replica, seen.State.Appearance).Select(i => i.State)));
        Assert.Contains(CrewOutfitTests.Icons(replica, seen.State.Appearance), i => (i.State ?? "").StartsWith("bodybag"));

        // the handheld crew monitor opens its window, with this doctor's own sensors listed
        w.Host.EnqueueConsoleLine("give Doctor /obj/item/sensor_device");
        w.Run(5);
        Assert.IsType<obj_item_sensor_device>(In(w, () => me.get_active_held_item()));
        In(w, () => { (me.w_uniform as obj_item_clothing_under)?.set_sensor_mode(SENSOR_COORDS); return 0; });
        w.Run(2);
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("CrewConsole", window.Interface);
        Assert.NotEmpty(window.Merged.Arr("sensors"));
        p.Session.UiClose(window.Window);
        w.Run(Second);

        // a scalpel from the medkit is a scalpel
        In(w, () => { qdel(me.get_active_held_item()); return 0; });
        w.Run(2);
        w.Host.EnqueueConsoleLine("give Doctor /obj/item/storage/medkit/surgery");
        w.Run(5);
        var kit = In(w, () => me.get_active_held_item()) as atom;
        Assert.NotNull(In(w, () => Dm.each<obj_item_scalpel>(kit.contents).FirstOrDefault()));
        Assert.Equal(TOOL_SCALPEL, In(w, () => Dm.each<obj_item_scalpel>(kit.contents).First().tool_behaviour));

        // the engineer: the hand drill from the chief's belt, Z swaps its bit, the balloon tells him
        RealHeadlessWorld.Player q = w.Join("Engineer");
        var eng = (mob_living_carbon_human)w.MobOf(q);
        w.Run(2 * Second);
        w.Host.EnqueueConsoleLine("give Engineer /obj/item/screwdriver/power");
        w.Run(5);
        var drill = In(w, () => eng.get_active_held_item()) as obj_item_screwdriver_power;
        Assert.NotNull(drill);
        Assert.Equal(TOOL_SCREWDRIVER, In(w, () => drill.tool_behaviour));
        q.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        Assert.Equal(TOOL_WRENCH, In(w, () => drill.tool_behaviour));

        // the experimental welder refills on the station too
        In(w, () => { qdel(drill); return 0; });
        w.Run(2);
        w.Host.EnqueueConsoleLine("give Engineer /obj/item/weldingtool/experimental");
        w.Run(5);
        var welder = In(w, () => eng.get_active_held_item()) as obj_item_weldingtool_experimental;
        Assert.NotNull(welder);
        eng_lights(w, q); // lit once it keeps processing, on or off (can_off_process)
        In(w, () => Waiting.NoSleep(((datum_reagents)welder.reagents).remove_all(20)));
        float low = In(w, () => Dm.num(welder.get_fuel()));
        w.Run(5 * Second);
        Assert.True(In(w, () => Dm.num(welder.get_fuel())) > low);

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
