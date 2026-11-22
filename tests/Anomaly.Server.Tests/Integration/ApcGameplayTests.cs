using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Models;
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
/// A test stand-in for the grid's power source (fixture, rules 15) until the SMES slice: a power machine that, in its process step
/// (after the APC steps of SSmachines, machines.dm:114-139), adds to its network what the network drew this cycle plus the room for
/// one APC's capped charge (CHARGELEVEL of an upgraded megacell over a cycle, apc_main.dm:709), as tg's generators add their output
/// (port_gen.dm). Following the demand keeps the network's excess near zero, so the SMES's restore of an unused surplus
/// (powernet.dm:87, the SMES slice) is not reached on Meta, whose grid holds its SMESes.
/// </summary>
[DmPath("/obj/machinery/power/test_m7apc_grid")]
public class obj_machinery_power_test_m7apc_grid : obj_machinery_power
{
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (powernet is datum_powernet net)
            add_avail(Dm.num(net.load) + 60000);
        return null;
    }
}

/// <summary>
/// M7 slice 3 (tg's APC, apc_main.dm, apc_power_proc.dm, apc_appearance.dm; machines.dm:114-139) on MetaStation through the real
/// server. The APCs start on tg's charged cells (start_charge 90 %, map load) and run their rooms; a grid source on the station's
/// network charges them. An engineer pries the floor tile under a room's APC, cuts the cable to its terminal with wirecutters (the
/// APC reads no external power), the APC's cell runs out (a test hook leaves it 1 J): its channels go off at tg's thresholds, the
/// room's lights go to their emergency light, its airlock and its vent lose their power, and his client's APC model shows the
/// channels off. He lays the cable back: the APC charges at tg's capped rate and brings the environment back at once, the lights
/// at 15 % and the equipment at 30 % of its cell, within SSmachines cycles. A second engineer opens the APC's window, swipes a
/// debug ID and switches the lighting channel off and back to automatic through the translated window.
/// </summary>
public class ApcGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static ModelCatalog Catalog()
    {
        for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "godot", "assets", "models", ModelCatalog.FileName);
            if (File.Exists(candidate))
                return ModelCatalog.Parse(File.ReadAllText(candidate));
        }
        throw new InvalidOperationException("godot/assets/models/models.json not found above the test's folder");
    }

    private sealed record Room(obj_machinery_power_apc Apc, area Area, obj_structure_cable Cable, obj_machinery_door_airlock Door,
        obj_machinery_atmospherics_components_unary_vent_pump Vent, List<obj_machinery_light> Lights);

    // A station room whose APC hangs over one exposed-able cable that links to the grid (the floor tile pried off, or plating),
    // with lit fixtures, a powered airlock and a running vent of its own.
    private static Room PickRoom()
    {
        var lights = Dm.each<obj_machinery_light>(Dm.world).Where(l => Dm.eq(l.z, 1) && Dm.truthy(l.on) && Dm.eq(l.status, LIGHT_OK) && l.loc is turf)
            .GroupBy(l => get_area(l) as area).Where(g => g.Key != null).ToDictionary(g => g.Key, g => g.ToList());
        var doors = Dm.each<obj_machinery_door_airlock>(Dm.world).Where(d => d.loc is turf && !Dm.truthy(Dm.bitand(d.machine_stat, NOPOWER)))
            .GroupBy(d => get_area(d) as area).Where(g => g.Key != null).ToDictionary(g => g.Key, g => g.First());
        var vents = Dm.each<obj_machinery_atmospherics_components_unary_vent_pump>(Dm.world).Where(v => Dm.truthy(v.on) && v.icon_state != "vent_off" && v.loc is turf)
            .GroupBy(v => get_area(v) as area).Where(g => g.Key != null).ToDictionary(g => g.Key, g => g.First());
        foreach (obj_machinery_power_apc apc in Dm.each<obj_machinery_power_apc>(Dm.world).OrderBy(a => a.dm_id))
        {
            area room = apc.area;
            if (room == null || !Dm.eq(apc.z, 1) || !Dm.@in(room.type, GLOB.the_station_areas) || !Dm.truthy(room.requires_power))
                continue;
            if (apc.loc is not turf_open t || apc.terminal?.powernet == null || apc.cell == null)
                continue;
            var cables = Dm.each_cast<atom>(t.contents).OfType<obj_structure_cable>().ToList();
            if (cables.Count != 1 || cables[0].powernet != apc.terminal.powernet || !Dm.truthy(cables[0].linked_dirs))
                continue;
            if (Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density)))
                continue;
            if (!lights.TryGetValue(room, out var lit) || lit.Count < 2 || !doors.TryGetValue(room, out var door) || !vents.TryGetValue(room, out var vent))
                continue;
            return new Room(apc, room, cables[0], door, vent, lit);
        }
        return null;
    }

    [MetaServerFact]
    public void A_cut_apc_cable_drains_the_rooms_apc_and_turns_its_channels_off_and_the_cable_laid_back_brings_them_back()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 79, TimeoutSteps = 12000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), seed: 202), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        ModelCatalog catalog = Catalog();

        Room room = In(w, PickRoom);
        Assert.NotNull(room);
        obj_machinery_power_apc apc = room.Apc;
        int x = In(w, () => (int)apc.x), y = In(w, () => (int)apc.y), z = In(w, () => (int)apc.z);
        output.WriteLine($"{In(w, () => Dm.str(room.Area.name))}: the APC at {x},{y}, {room.Lights.Count} lit fixtures, the door at {In(w, () => room.Door.x)},{In(w, () => room.Door.y)}, the vent at {In(w, () => room.Vent.x)},{In(w, () => room.Vent.y)}");

        // the round's start (Initialize, apc_main.dm:209-215): tg's charged cell, all channels on automatic, the room powered
        In(w, () =>
        {
            Assert.Same(apc, room.Area.apc);
            Assert.Equal(APC_ELECTRONICS_SECURED, apc.has_electronics);
            Assert.InRange(Dm.num(apc.cell.percent()), 80, 90);
            Assert.Equal((3f, 3f, 3f), (Dm.num(apc.equipment), apc.lighting, apc.environ));
            Assert.True(Dm.truthy(room.Area.power_light) && Dm.truthy(room.Area.power_equip) && Dm.truthy(room.Area.power_environ));
        });

        // the grid's source on the station network, beside the APC's cable
        obj_structure_cable neighbour = In(w, () => Dm.each_cast<obj_structure_cable>(room.Cable.get_cable_connections()).First());
        In(w, () =>
        {
            var source = Dm.@new<obj_machinery_power_test_m7apc_grid>(neighbour.loc);
            Assert.True(Dm.truthy(source.connect_to_network()));
            Assert.Same(neighbour.powernet, source.powernet);
        });
        In(w, () => Waiting.NoSleep(me.forceMove(locate(x, y, z))));
        w.Run(8 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        AppearanceEntry Look(atom a)
        {
            ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
            return seen != null && replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? look : null;
        }
        List<string> OverlaysOf(AppearanceEntry look) => look.Overlays
            .Select(o => replica.TryGetAppearance(o, out AppearanceEntry e) ? replica.ResolveString(e.IconState) : null).Where(s => s != null).ToList();
        string ModelOf(atom a)
        {
            AppearanceEntry look = Look(a);
            return look == null ? null : catalog.Find(replica.ResolveResource(look.Icon), replica.ResolveString(look.IconState), OverlaysOf(look))?.Id;
        }
        ReplicaEntity Seen(atom a) => replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
        uint emergency = CellState.ParseColor(COLOR_VIVID_RED); // bulb_low_power_colour

        // with the grid: external power (the stand-in source leaves no surplus once the APCs have charged, so low or full,
        // late_process :628-633); the client draws tg's working APC: apc0, the lock, the charge light and the three channels on
        // automatic (apco0-3, apco1-3, apco2-3)
        Assert.NotEqual(APC_NO_POWER, In(w, () => apc.main_status));
        output.WriteLine($"the client's APC: {replica.ResolveString(Look(apc).IconState)}, overlays {string.Join(" ", OverlaysOf(Look(apc)))}, model {ModelOf(apc)}");
        Assert.Equal("apc0", replica.ResolveString(Look(apc).IconState));
        Assert.Contains("apcox-1", OverlaysOf(Look(apc)));
        Assert.Contains("apco1-3", OverlaysOf(Look(apc)));
        Assert.Contains(ModelOf(apc), new[] { "apc_charging", "apc_not_charging", "apc_charged" });

        // the floor tile pried off, the cable to the terminal cut
        In(w, () => Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(Dm.@new<obj_item_crowbar>(me.loc))))));
        if (In(w, () => locate(x, y, z).underfloor_accessibility < UNDERFLOOR_INTERACTABLE))
        {
            Assert.True(p.Session.Click(new ClickTarget(TargetKind.Turf, 0, x, y, z), ClickButton.Left, ClickModifiers.None));
            w.Run(Second);
        }
        Assert.True(In(w, () => locate(x, y, z).underfloor_accessibility >= UNDERFLOOR_INTERACTABLE), "the cable is exposed");
        In(w, () => { foreach (object held in Dm.each(me.held_items.Copy())) if (held is obj_item i) qdel(i); });
        In(w, () => Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(Dm.@new<obj_item_wirecutters>(me.loc))))));
        // the live cable shocks a bare hand half the time (cable.dm:215, the shocks slice: ShockGameplayTests); the engineer's
        // insulated gloves keep this test on the APC
        In(w, () => Assert.True(Dm.truthy(Waiting.NoSleep(me.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_gloves_color_yellow>(me.loc), ITEM_SLOT_GLOVES)))));
        w.Run(Second);
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, room.Cable.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.True(In(w, () => QDELETED(room.Cable)), "the cable is cut");
        Assert.Null(In(w, () => apc.terminal.powernet));
        w.Run(3 * Second);
        Assert.Equal(APC_NO_POWER, In(w, () => apc.main_status)); // late_process (apc_main.dm:628): no avail

        // the cell runs out (the test hook leaves 1 J): every channel is forced off at the next cycles (apc_main.dm:638)
        In(w, () => apc.cell.charge = 1);
        w.Run(6 * Second);
        In(w, () =>
        {
            Assert.Equal(0f, apc.cell.charge);
            Assert.Equal((1f, 1f, 1f), (Dm.num(apc.equipment), apc.lighting, apc.environ));
            Assert.False(Dm.truthy(room.Area.power_light) || Dm.truthy(room.Area.power_equip) || Dm.truthy(room.Area.power_environ));
            Assert.True(Dm.truthy(Dm.bitand(room.Door.machine_stat, NOPOWER)), "the airlock is unpowered");
            Assert.False(Dm.truthy(room.Door.hasPower()));
            Assert.True(Dm.truthy(Dm.bitand(room.Vent.machine_stat, NOPOWER)), "the vent is unpowered");
        });
        foreach (obj_machinery_light lamp in room.Lights)
        {
            ReplicaEntity e = Seen(lamp);
            if (e == null)
                continue;
            Assert.True(e.Light.On, "the emergency light is on");
            Assert.Equal(emergency, e.Light.Rgba);
        }
        Assert.Equal("vent_off", replica.ResolveString(Look(room.Vent).IconState)); // update_icon_nopipes: not operational
        Assert.Contains("apco1-1", OverlaysOf(Look(apc)));
        Assert.Equal("apc_channels_auto_off", ModelOf(apc));
        output.WriteLine($"drained: the APC model {ModelOf(apc)}, overlays {string.Join(" ", OverlaysOf(Look(apc)))}");

        // the cable laid back: the piece picked up, a click on the plating
        obj_item_stack_cable_coil piece = In(w, () => Dm.locate<obj_item_stack_cable_coil>(locate(x, y, z)));
        Assert.NotNull(piece);
        In(w, () => { foreach (object held in Dm.each(me.held_items.Copy())) if (held is obj_item i) qdel(i); });
        w.Run(2);
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, piece.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Same(piece, In(w, () => me.get_active_held_item()));
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Turf, 0, x, y, z), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        obj_structure_cable laid = In(w, () => Dm.locate<obj_structure_cable>(locate(x, y, z)));
        Assert.NotNull(laid);
        Assert.Same(In(w, () => laid.powernet), In(w, () => apc.terminal.powernet)); // the terminal on the grid again (cable.dm:367)

        // the next cycles: charging at tg's capped rate, the environment back at once, the door and the vent powered again
        w.Run(6 * Second);
        In(w, () =>
        {
            Assert.NotEqual(APC_NO_POWER, apc.main_status);
            Assert.True(apc.cell.charge > 0, "the cell charges");
            Assert.Equal(APC_CHANNEL_AUTO_ON, apc.environ);
            Assert.True(Dm.truthy(room.Area.power_environ));
            Assert.False(Dm.truthy(Dm.bitand(room.Door.machine_stat, NOPOWER)), "the airlock is powered again");
            Assert.False(Dm.truthy(Dm.bitand(room.Vent.machine_stat, NOPOWER)), "the vent runs again");
        });
        Assert.NotEqual("vent_off", replica.ResolveString(Look(room.Vent).IconState));
        // the lights at 15 %, the equipment at 30 % (50 kJ a cycle: 15 cycles of 2 s)
        w.Run(30 * Second);
        In(w, () =>
        {
            Assert.InRange(Dm.num(apc.cell.percent()), 30, 40);
            Assert.Equal((3f, 3f, 3f), (Dm.num(apc.equipment), apc.lighting, apc.environ));
            Assert.True(Dm.truthy(room.Area.power_light) && Dm.truthy(room.Area.power_equip) && Dm.truthy(room.Area.power_environ));
        });
        foreach (obj_machinery_light lamp in room.Lights)
        {
            ReplicaEntity e = Seen(lamp);
            if (e == null)
                continue;
            Assert.True(e.Light.On);
            Assert.NotEqual(emergency, e.Light.Rgba);
        }
        Assert.Contains(ModelOf(apc), new[] { "apc_charging", "apc_not_charging" });
        output.WriteLine($"restored: {In(w, () => Dm.num(apc.cell.percent()))} %, the APC model {ModelOf(apc)}");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount); // nothing the cut, the drain or the lay ran into a runtime
    }

    [MetaServerFact]
    public void The_apc_window_unlocked_with_an_id_switches_the_lighting_channel_off_and_back()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 83, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        UiNode Shown(int index)
        {
            windows.Sync(replica.UiWindows, 1280, 720, 1);
            return windows.Get(index)?.Content;
        }

        Room room = In(w, PickRoom);
        Assert.NotNull(room);
        obj_machinery_power_apc apc = room.Apc;
        In(w, () => Waiting.NoSleep(me.forceMove(apc.loc)));
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;

        // an empty hand opens the window (attack_hand, ui_interact: tg's "Apc" interface), locked
        Assert.True(p.Session.Click(apc.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("Apc", window.Interface);
        Assert.Equal(1, window.Merged.Num("locked"));
        UiNode content = Shown(window.Window);
        Assert.Contains("Swipe an ID card to unlock this interface.", content.AllText());
        Assert.Contains("Main Breaker", content.AllText());
        Assert.Contains("Lighting", content.AllText());
        Assert.Equal(450, windows.Get(window.Window).Context.WindowWidth);
        Assert.True(content.Walk().Where(n => n.Kind == UiKind.Button && n.Text == "Off").All(n => n.Disabled), "locked: the channel buttons are disabled");

        // a debug ID swipes it open (item_interaction's GetID, togglelock)
        In(w, () => Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(Dm.@new<obj_item_card_id_advanced_debug>(me.loc))))));
        Assert.True(p.Session.Click(apc.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Equal(0f, In(w, () => apc.locked));
        content = Shown(window.Window);
        Assert.Contains("Swipe an ID card to lock this interface.", content.AllText());

        // the lighting channel's Off (setsubsystem(1), apc_power_proc.dm:118): the room's lights go to their emergency light
        UiNode lighting = content.Walk().First(n => n.Kind == UiKind.LabeledItem && n.Label == "Lighting");
        lighting.Buttons.Walk().First(n => n.Key == "off").OnClick();
        w.Run(5 * Second);
        In(w, () =>
        {
            Assert.Equal(APC_CHANNEL_OFF, apc.lighting);
            Assert.False(Dm.truthy(room.Area.power_light));
            Assert.All(room.Lights, l => Assert.True(Dm.truthy(l.low_power_mode)));
        });
        Assert.Equal(0, replica.UiWindows[window.Window].Merged.Arr("powerChannels")[1].Num("status"));
        uint emergency = CellState.ParseColor(COLOR_VIVID_RED);
        Assert.Contains(room.Lights, l => replica.Entities.FirstOrDefault(e => e.Id == l.dm_id) is { } e && e.Light.On && e.Light.Rgba == emergency);
        content = Shown(window.Window);
        lighting = content.Walk().First(n => n.Kind == UiKind.LabeledItem && n.Label == "Lighting");
        Assert.True(lighting.Buttons.Walk().First(n => n.Key == "off").Selected);

        // back to automatic: the lights again
        lighting.Buttons.Walk().First(n => n.Key == "auto").OnClick();
        w.Run(3 * Second);
        In(w, () =>
        {
            Assert.Equal(APC_CHANNEL_AUTO_ON, apc.lighting);
            Assert.True(Dm.truthy(room.Area.power_light));
            Assert.All(room.Lights, l => Assert.True(Dm.truthy(l.on)));
        });
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
