using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M5 slice 6 through the real server on MetaStation: tg's airlock built and taken apart in the Auxiliary Base Construction zone
/// (plan section 4). The player takes a cable coil and an airlock electronics from the aux base rack (50,149) and sets the
/// electronics' access to "Auxiliary Base" in tg's "AirlockElectronics" window (protocol 16: the window's own buttons send tg's
/// ui_act); takes an iron stack from the table (51,149) and builds the "standard airlock assembly" recipe at (48,157); the wrench
/// anchors it, the cable wires it, the electronics go in, the screwdriver finishes the airlock (door_assembly.dm:86-384). The
/// client sees each stage (tg's construction overlays panel_c1..3, drawn as the door assembly piece). The airlock refuses the
/// player without the access and opens with the captain's spare; then, unpowered (the zone's environment channel off, as an
/// APC's breaker does), welded shut and with its panel open, the crowbar takes it apart into its assembly and electronics; the
/// wirecutters, wrench and welder take the assembly back to the cable and 4 iron.
/// </summary>
public class AirlockBuildGameplayTests
{
    private const int Second = 20; // ticks per second
    private const int TableX = 51, TableY = 149; // the aux base table: iron x2 fifty
    private const int RackX = 50, RackY = 149; // the aux base rack: airlock electronics and cable coils
    private const int DoorX = 48, DoorY = 157; // the zone's floor
    private const int StandX = 49, StandY = 157;

    private readonly ITestOutputHelper _output;

    public AirlockBuildGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Put(RealHeadlessWorld w, mob m, int x, int y)
    {
        using (w.World.Enter())
            Waiting.NoSleep(m.forceMove(Builtins.locate(x, y, m.z)));
        w.Run(Second);
    }

    private static List<T> On<T>(RealHeadlessWorld w, int x, int y, int z)
        => In(w, () => Dm.each_cast<atom>(((turf)Builtins.locate(x, y, z)).contents).OfType<T>().ToList());

    /// <summary>A tool (dev give) in the active hand, the hands' tools deleted first (the materials are dropped, not deleted).</summary>
    private static T Tool<T>(RealHeadlessWorld w, mob_living_carbon_human me, string path) where T : obj_item
    {
        using (w.World.Enter())
        {
            EmptyHands(me);
            DevCommands.Give(me, path);
            return me.get_active_held_item() as T;
        }
    }

    private static void EmptyHands(mob_living_carbon_human me)
    {
        foreach (object held in Dm.each(me.held_items.Copy()))
        {
            if (held is obj_item_stack or obj_item_electronics_airlock)
                Waiting.NoSleep(me.dropItemToGround((obj_item)held));
            else if (held is obj_item old)
                GlobalProcs.qdel(old);
        }
    }

    private static bool Click(RealHeadlessWorld w, RealHeadlessWorld.Player p, atom target, float seconds = 1, ClickButton button = ClickButton.Left)
    {
        bool sent = p.Session.Click(new ClickTarget(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 16), button, ClickModifiers.None);
        w.Run((int)(seconds * Second));
        return sent;
    }

    // The icon states of a replicated entity's appearance tree, as the client composes its picture.
    private static List<string> States(ReplicaWorld replica, atom a)
    {
        ReplicaEntity seen = replica.Entities.Single(e => e.Id == a.dm_id);
        return CrewOutfitTests.Icons(replica, seen.State.Appearance).Select(i => i.State).ToList();
    }

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    [MetaServerFact]
    public void An_airlock_is_built_from_the_aux_base_materials_keeps_its_access_and_is_taken_apart_again()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 56, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        MetaServer.ClearAuxBase(w.World); // the test works in the empty construction zone
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        int z = In(w, () => (int)me.z);
        int bootRuntimes = w.World.RuntimeErrorCount;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };

        // ---- 1. The rack: a cable coil and an airlock electronics into the hands; the electronics' window (Z) sets its access.
        Put(w, me, RackX, RackY - 1);
        obj_item_stack_cable_coil cable = On<obj_item_stack_cable_coil>(w, RackX, RackY, z).First();
        obj_item_electronics_airlock electronics = On<obj_item_electronics_airlock>(w, RackX, RackY, z).First();
        Assert.True(Click(w, p, cable));
        Assert.True(In(w, () => Dm.truthy(me.is_holding(cable))), "the cable is in hand");
        p.Session.Keybinding(KeybindingCode.SwapHands);
        w.Run(2);
        Assert.True(Click(w, p, electronics));
        Assert.True(In(w, () => Dm.truthy(me.is_holding(electronics))), "the electronics are in hand");
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        _output.WriteLine($"window {window.Window}: {window.Interface} '{window.Title}' status {window.Status}");
        Assert.Equal("AirlockElectronics", window.Interface);
        UiNode content = Shown(windows, replica, window.Window);
        Assert.NotNull(content);
        // the region holding "Auxiliary Base" (tg's REGION_ACCESS_SUPPLY): its tab, then the access's own button
        string region = window.Merged.Arr("regions").First(r => r.Arr("accesses").Any(a => a.Str("desc") == "Auxiliary Base")).Str("name");
        _output.WriteLine($"Auxiliary Base is in the region '{region}'");
        content.Walk().First(n => n.Kind == UiKind.Tab && n.Key == region).OnClick();
        content = Shown(windows, replica, window.Window);
        content.Walk().First(n => n.Kind == UiKind.Button && n.Key == "Auxiliary Base").OnClick();
        w.Run(Second);
        Assert.Equal(new object[] { Defines.ACCESS_AUX_BASE }, In(w, () => Dm.each(electronics.accesses).ToArray()));
        Assert.Contains(replica.UiWindows[window.Window].Merged.Arr("accesses"), a => a.Text == Defines.ACCESS_AUX_BASE); // ui_data follows
        Assert.True(p.Session.UiClose(window.Window));
        w.Run(Second);
        Assert.DoesNotContain(replica.UiWindows.Values, v => v.Interface == "AirlockElectronics");
        // carried into the zone and put down beside the build spot
        Put(w, me, StandX, StandY);
        using (w.World.Enter())
            EmptyHands(me);
        w.Run(Second);

        // ---- 2. The iron: "standard airlock assembly" from tg's StackCrafting window builds the assembly on the player's tile.
        Put(w, me, TableX - 1, TableY - 1);
        obj_item_stack_sheet_iron iron = On<obj_item_stack_sheet_iron>(w, TableX, TableY, z).First();
        Assert.True(Click(w, p, iron));
        // DV65: built where the crosshair aims (nothing dense on the builder's own tile): from the tile south of it
        Put(w, me, DoorX, DoorY - 1);
        p.Session.Hover(new ClickTarget(TargetKind.Turf, 0, DoorX, DoorY, z), w.Now);
        w.Run(Second / 2);
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        UiWindowState stack = replica.UiWindows.Values.Single(v => v.Interface == "StackCrafting");
        Shown(windows, replica, stack.Window).Walk().First(n => n.Key == "standard airlock assembly").OnClick();
        w.Run(6 * Second);
        obj_structure_door_assembly assembly = Assert.Single(On<obj_structure_door_assembly>(w, DoorX, DoorY, z));
        Assert.Equal(46f, In(w, () => Dm.num(iron.amount)));
        p.Session.UiClose(stack.Window);
        Put(w, me, StandX, StandY);
        using (w.World.Enter())
            EmptyHands(me);
        w.Run(Second);
        ReplicaEntity seenAssembly = replica.Entities.Single(e => e.Id == assembly.dm_id);
        Assert.Equal(RenderKind.DoorAssembly, PropKinds.Classify(seenAssembly.TypePath));
        List<string> looks = States(replica, assembly);
        _output.WriteLine($"the new assembly: {string.Join(", ", looks)}");
        Assert.Contains("construction", looks);
        Assert.Contains("fill_construction", looks);
        Assert.Contains("panel_c1", looks);

        // ---- 3. Wrench (anchored), cable (wired), electronics (in), each 4 s, each stage seen by the client.
        Tool<obj_item_wrench>(w, me, "/obj/item/wrench");
        Assert.True(Click(w, p, assembly, 5));
        Assert.True(In(w, () => Dm.truthy(assembly.anchored)));
        Assert.Equal("secured Airlock assembly", In(w, () => assembly.name));
        Assert.Contains(heard, h => h.Kind == SoundKind.Play && h.File != null && (h.File ?? "").StartsWith("sound/items/tools/ratchet", StringComparison.Ordinal));

        using (w.World.Enter())
            EmptyHands(me);
        Assert.True(Click(w, p, cable));
        Assert.True(Click(w, p, assembly, 5));
        Assert.Equal(Defines.AIRLOCK_ASSEMBLY_NEEDS_ELECTRONICS, In(w, () => Dm.num(assembly.state)));
        Assert.Contains("panel_c2", States(replica, assembly));

        using (w.World.Enter())
            EmptyHands(me);
        Assert.True(Click(w, p, electronics));
        Assert.True(Click(w, p, assembly, 5));
        Assert.Equal(Defines.AIRLOCK_ASSEMBLY_NEEDS_SCREWDRIVER, In(w, () => Dm.num(assembly.state)));
        Assert.Same(assembly, In(w, () => electronics.loc));
        Assert.Contains("panel_c3", States(replica, assembly));

        // ---- 4. The screwdriver finishes the airlock: closed, with the electronics' access.
        Tool<obj_item_screwdriver>(w, me, "/obj/item/screwdriver");
        Assert.True(Click(w, p, assembly, 5));
        obj_machinery_door_airlock door = Assert.Single(On<obj_machinery_door_airlock>(w, DoorX, DoorY, z));
        Assert.Empty(On<obj_structure_door_assembly>(w, DoorX, DoorY, z));
        Assert.DoesNotContain(replica.Entities, e => e.Id == assembly.dm_id);
        Assert.Contains(replica.Entities, e => e.Id == door.dm_id);
        Assert.Equal(RenderKind.Airlock, PropKinds.Classify(replica.Entities.Single(e => e.Id == door.dm_id).TypePath));
        In(w, () =>
        {
            Assert.True(Dm.truthy(door.density), "the finished airlock is closed");
            Assert.Equal(new object[] { Defines.ACCESS_AUX_BASE }, Dm.each((DmList)door.req_access).ToArray());
            Assert.Same(door, electronics.loc);
            return 0;
        });

        // ---- 5. The airlock refuses the assistant (no aux base access) and opens to the captain's spare.
        using (w.World.Enter())
            EmptyHands(me);
        Assert.True(Click(w, p, door, 2));
        Assert.True(In(w, () => Dm.truthy(door.density)), "the airlock refuses a player without the access");
        Tool<obj_item_card_id>(w, me, "/obj/item/card/id/advanced/gold/captains_spare");
        Assert.True(Click(w, p, door, 2));
        Assert.False(In(w, () => Dm.truthy(door.density)), "the airlock opens to the access");
        w.Run(10 * Second); // it closes by itself (autoclose)
        Assert.True(In(w, () => Dm.truthy(door.density)), "the airlock closed again");

        // ---- 6. Taken apart: the panel (screwdriver), power off (the zone's environment channel, as an APC's breaker: the APC is
        // M7), welded shut (right click), the crowbar: the assembly (wired, anchored) and the electronics with their access.
        Tool<obj_item_screwdriver>(w, me, "/obj/item/screwdriver");
        Assert.True(Click(w, p, door, 2));
        Assert.True(In(w, () => Dm.truthy(door.panel_open)));
        using (w.World.Enter())
        {
            var zone = (area)GlobalProcs.get_area(door);
            zone.power_environ = 0;
            zone.power_change();
        }
        w.Run(Second);
        Assert.False(In(w, () => Dm.truthy(door.hasPower())));
        var welder = Tool<obj_item_weldingtool>(w, me, "/obj/item/weldingtool");
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        Assert.True(Click(w, p, door, 6, ClickButton.Right));
        Assert.True(In(w, () => Dm.truthy(door.welded)), "the airlock is welded shut");
        Tool<obj_item_crowbar>(w, me, "/obj/item/crowbar");
        Assert.True(Click(w, p, door, 6));
        Assert.Empty(On<obj_machinery_door_airlock>(w, DoorX, DoorY, z));
        Assert.DoesNotContain(replica.Entities, e => e.Id == door.dm_id);
        obj_structure_door_assembly back = Assert.Single(On<obj_structure_door_assembly>(w, DoorX, DoorY, z));
        In(w, () =>
        {
            Assert.True(Dm.truthy(back.anchored));
            Assert.Equal(Defines.AIRLOCK_ASSEMBLY_NEEDS_ELECTRONICS, Dm.num(back.state));
            Assert.Equal(new object[] { Defines.ACCESS_AUX_BASE }, Dm.each(electronics.accesses).ToArray());
            Assert.IsAssignableFrom<turf>(electronics.loc);
            return 0;
        });
        Assert.Contains("panel_c2", States(replica, back));

        // ---- 7. The wirecutters (the cable back), the wrench (loose), the welder: 4 iron.
        Tool<obj_item_wirecutters>(w, me, "/obj/item/wirecutters");
        Assert.True(Click(w, p, back, 5));
        Assert.Equal(Defines.AIRLOCK_ASSEMBLY_NEEDS_WIRES, In(w, () => Dm.num(back.state)));
        Tool<obj_item_wrench>(w, me, "/obj/item/wrench");
        Assert.True(Click(w, p, back, 5));
        Assert.False(In(w, () => Dm.truthy(back.anchored)));
        Tool<obj_item_weldingtool>(w, me, "/obj/item/weldingtool");
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        Assert.True(Click(w, p, back, 5));
        Assert.Empty(On<obj_structure_door_assembly>(w, DoorX, DoorY, z));
        float ironBack = On<obj_item_stack_sheet_iron>(w, DoorX, DoorY, z).Sum(s => In(w, () => Dm.num(s.amount)));
        Assert.Equal(4f, ironBack);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
