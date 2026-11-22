using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Package E3 (docs/ai/specs/engineering.md) on MetaStation through the real server and a client's own stack: an engineer opens the windows of
/// the portable air pump, the portable air scrubber and the pipe scrubber with a click on the machine (tg's PortablePump, PortableScrubber
/// and PipeScrubber interfaces, translated), switches them on, sets the pump's target pressure, picks the gases a scrubber filters and sees
/// the machines work (the gas moves, the picture lights); with a gas tank in hand the secondary use opens the Tank window and its regulator
/// sets the tank's release pressure.
/// </summary>
public class PortableAtmosGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20; // ticks per second
    private const int StandX = 45, StandY = 154; // the Auxiliary Base Construction zone's plating (CanisterVisualsGameplayTests)

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

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private static string IconOf(ReplicaWorld replica, atom a)
    {
        ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
        return seen != null && replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? replica.ResolveString(look.IconState) : null;
    }

    private static List<string> OverlaysOf(ReplicaWorld replica, atom a)
    {
        var shown = new List<string>();
        ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
        if (seen == null || !replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look))
            return shown;
        foreach (TableRef o in look.Overlays)
            if (replica.TryGetAppearance(o, out AppearanceEntry overlay))
                shown.Add(replica.ResolveString(overlay.IconState));
        return shown;
    }

    private static float Moles(datum_gas_mixture mix, Type gas) => Dm.num(mix.moles[gas]);

    private sealed class Setup : IDisposable
    {
        public RealHeadlessWorld W { get; }
        public RealHeadlessWorld.Player P { get; }
        public mob_living_carbon_human Me { get; }
        public ReplicaWorld Replica { get; }
        public TguiWindows Windows { get; }
        public int Z { get; }
        public int BootRuntimes { get; }

        public Setup(ulong seed, ITestOutputHelper output)
        {
            W = new RealHeadlessWorld(new LoopbackOptions { Seed = seed, TimeoutSteps = 12000 },
                MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
            P = W.Join("Engineer");
            Me = (mob_living_carbon_human)W.MobOf(P);
            Replica = P.Session.Replica;
            Windows = new TguiWindows
            {
                SendAct = (window, action, parameters) => P.Session.UiAct(window, action, parameters),
                SendClose = window => P.Session.UiClose(window),
            };
            BootRuntimes = W.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
            W.World.RuntimeErrorSink = e => output.WriteLine("RUNTIME: " + e.Message.Split('\n')[0]);
            Z = In(W, () => (int)Me.z);
            using (W.World.Enter())
                Waiting.NoSleep(Me.forceMove(locate(StandX, StandY, Z)));
        }

        public void Dispose() => W.Dispose();

        // a machine on the open floor beside the engineer (the stand is at StandX, StandY)
        public T Place<T>(int dx, int dy = 0) where T : atom
        {
            T thing = In(W, () =>
            {
                turf at = locate(StandX + dx, StandY + dy, Z) as turf;
                Assert.True(at is turf_open && !Dm.truthy(at.density), "an open floor beside the engineer: " + at);
                return Dm.@new<T>(at);
            });
            W.Run(2 * Second);
            return thing;
        }

        // the open window of an interface (the latest), after a click on the machine
        public UiWindowState Open(atom target, string @interface)
        {
            DropHeld(); // an empty hand opens the window, a tank in it would go into the machine
            Stand(); // the pump's suction pushes a man about the room
            Assert.True(P.Session.Click(new ClickTarget(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
            W.Run(Second);
            return Assert.Single(Replica.UiWindows.Tgui(), v => v.Interface == @interface);
        }

        public void Stand()
        {
            using (W.World.Enter())
                Waiting.NoSleep(Me.forceMove(locate(StandX, StandY, Z)));
        }

        private void DropHeld()
        {
            using (W.World.Enter())
                foreach (object held in Dm.each(Me.held_items.Copy()))
                    if (held is obj_item old)
                        Waiting.NoSleep(Me.dropItemToGround(old));
        }

        public UiNode Content(UiWindowState window) => Shown(Windows, Replica, window.Window);

        // an item made in the empty active hand, then a click with it on the target
        public obj_item UseHeld(string path, atom target)
        {
            DropHeld();
            In(W, () => DevCommands.Give(Me, path));
            obj_item item = In(W, () => Me.get_active_held_item() as obj_item);
            Assert.NotNull(item);
            Assert.True(P.Session.Click(new ClickTarget(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
            W.Run(2 * Second);
            return item;
        }
    }

    [MetaServerFact]
    public void An_engineer_runs_the_portable_pump_the_portable_scrubber_and_the_pipe_scrubber_from_their_windows()
    {
        using var s = new Setup(77, output);
        RealHeadlessWorld w = s.W;

        // ---- the portable air pump ----
        var pump = s.Place<obj_machinery_portable_atmospherics_pump>(1);
        UiWindowState pumpWindow = s.Open(pump, "PortablePump");
        Assert.Equal(0, pumpWindow.Merged.Num("on"));
        Assert.Equal(0, pumpWindow.Merged.Num("direction")); // PUMP_OUT
        Assert.Equal(101, pumpWindow.Merged.Num("targetPressure"));
        Assert.Equal(10, pumpWindow.Merged.Num("minPressure"));
        Assert.Equal(2533, pumpWindow.Merged.Num("maxPressure"));
        Assert.Equal(UiValueKind.Null, pumpWindow.Merged.Get("holding")?.Kind);
        UiNode content = s.Content(pumpWindow);
        Assert.Equal(300, s.Windows.Get(pumpWindow.Window).Context.WindowWidth);
        Assert.Equal("Off", ByKey(content, "power").Text);
        Assert.Equal("Pump → Area", ByKey(content, "direction").Text);
        string off = IconOf(s.Replica, pump);
        output.WriteLine("the pump off: " + off + " " + string.Join(",", OverlaysOf(s.Replica, pump)));

        // the direction button turns the pump to suck from the area, the power button switches it on
        ByKey(content, "direction").OnClick();
        w.Run(Second);
        Assert.Equal(1f, In(w, () => Dm.num(pump.direction)));
        content = s.Content(pumpWindow);
        Assert.Equal("Area → Pump", ByKey(content, "direction").Text);
        ByKey(content, "power").OnClick();
        w.Run(Second);
        Assert.Equal(1f, In(w, () => pump.on));
        Assert.NotEqual(off, IconOf(s.Replica, pump)); // the lit picture of tg's "[initial icon_state]_[on]"
        output.WriteLine("the pump on: " + IconOf(s.Replica, pump));
        content = s.Content(pumpWindow);
        Assert.Equal("On", ByKey(content, "power").Text);

        // a target pressure typed into the field: the pump fills up to it from the room
        ByKey(content, "pressure").OnChange(300);
        w.Run(Second);
        Assert.Equal(300f, In(w, () => pump.target_pressure));
        w.Run(10 * Second);
        float filled = In(w, () => Dm.num(pump.air_contents.return_pressure()));
        output.WriteLine($"the pump filled to {filled} kPa towards 300");
        Assert.True(filled > 10f, "the pump sucks the room's air in: " + filled);
        Assert.True(filled <= 300.5f);
        content = s.Content(pumpWindow);
        Assert.Equal(Math.Floor(filled), s.Replica.UiWindows[pumpWindow.Window].Merged.Num("pressure"));
        Assert.Equal(300, ByKey(content, "pressure").Value);

        // the number is clamped to the pump's limits; the presets set them
        ByKey(content, "pressure").OnChange(99999);
        w.Run(Second);
        Assert.Equal(PUMP_MAX_PRESSURE, In(w, () => pump.target_pressure));
        content = s.Content(pumpWindow);
        Assert.True(ByKey(content, "max").Disabled);
        ByKey(content, "min").OnClick();
        w.Run(Second);
        Assert.Equal(PUMP_MIN_PRESSURE, In(w, () => pump.target_pressure));
        content = s.Content(pumpWindow);
        ByKey(content, "reset").OnClick();
        w.Run(Second);
        Assert.Equal(PUMP_DEFAULT_PRESSURE, In(w, () => pump.target_pressure));

        // a held tank put into the pump shows in the window; the Eject button takes it out (the pump stays on: tg's unregister_holding, which
        // switches it off, runs only when the held tank is deleted)
        obj_item tank = s.UseHeld("/obj/item/tank/internals/oxygen", pump);
        Assert.Same(tank, In(w, () => pump.holding));
        w.Run(Second);
        content = s.Content(pumpWindow);
        Assert.Equal(tank.name, ByKey(content, "holding-name").Text);
        Assert.False(ByKey(content, "eject").Disabled);
        Assert.Contains("siphon-open", OverlaysOf(s.Replica, pump));
        ByKey(content, "eject").OnClick();
        w.Run(Second);
        Assert.Null(In(w, () => pump.holding));
        Assert.Equal(1f, In(w, () => pump.on));
        content = s.Content(pumpWindow);
        Assert.True(ByKey(content, "eject").Disabled);
        Assert.DoesNotContain("siphon-open", OverlaysOf(s.Replica, pump));

        content = s.Content(pumpWindow);
        ByKey(content, "power").OnClick();
        w.Run(Second);
        Assert.Equal(0f, In(w, () => pump.on));
        Assert.Equal(off, IconOf(s.Replica, pump));

        // ---- the portable air scrubber ----
        var scrubber = s.Place<obj_machinery_portable_atmospherics_scrubber>(-1);
        UiWindowState scrubberWindow = s.Open(scrubber, "PortableScrubber");
        content = s.Content(scrubberWindow);
        Assert.Equal(320, s.Windows.Get(scrubberWindow.Window).Context.WindowWidth);
        List<UiNode> filters = content.Walk().Where(n => n.Key?.StartsWith("filter:") == true).ToList();
        Assert.Equal(In(w, () => GLOB.meta_gas_info[META_GAS_ID] is DmList ids ? ids.Count : 0), filters.Count); // a button for each gas tg knows
        Assert.True(ByKey(content, "filter:co2").Selected);
        Assert.False(ByKey(content, "filter:o2").Selected);
        Assert.Contains(typeof(datum_gas_carbon_dioxide), In(w, () => Dm.each(scrubber.scrubbing).Select(g => (Type)g).ToList()));

        // plenty of carbon dioxide in the room's air, then the scrubber on: it takes the gas into itself
        datum_gas_mixture room = In(w, () => (datum_gas_mixture)Waiting.NoSleep(((turf)scrubber.loc).return_air()));
        In(w, () =>
        {
            room.merge(SSair.parse_gas_string("co2=80;TEMP=293.15") as datum_gas_mixture);
            ((turf)scrubber.loc).air_update_turf(Dm.FALSE, Dm.FALSE);
        });
        float co2Room = Moles(room, typeof(datum_gas_carbon_dioxide));
        ByKey(content, "power").OnClick();
        w.Run(Second);
        Assert.Equal(1f, In(w, () => scrubber.on));
        Assert.Equal("scrubber_1", IconOf(s.Replica, scrubber));
        w.Run(8 * Second);
        float taken = Moles(scrubber.air_contents, typeof(datum_gas_carbon_dioxide));
        output.WriteLine($"the room's CO2: {co2Room} -> {Moles(room, typeof(datum_gas_carbon_dioxide))} mol; the scrubber holds {taken} mol");
        Assert.True(taken > 1f, "the scrubber filters carbon dioxide out of the air: " + taken);
        Assert.Equal(0f, Moles(scrubber.air_contents, typeof(datum_gas_oxygen))); // only what is filtered
        content = s.Content(scrubberWindow);
        Assert.True(s.Replica.UiWindows[scrubberWindow.Window].Merged.Num("pressure") > 0);

        // the CO2 button unticks the gas: the scrubber leaves it be
        ByKey(content, "filter:co2").OnClick();
        w.Run(Second);
        Assert.DoesNotContain(typeof(datum_gas_carbon_dioxide), In(w, () => Dm.each(scrubber.scrubbing).Select(g => (Type)g).ToList()));
        content = s.Content(scrubberWindow);
        Assert.False(ByKey(content, "filter:co2").Selected);
        Assert.Equal("square-o", ByKey(content, "filter:co2").Icon);
        float held = Moles(scrubber.air_contents, typeof(datum_gas_carbon_dioxide));
        w.Run(6 * Second);
        Assert.Equal(held, Moles(scrubber.air_contents, typeof(datum_gas_carbon_dioxide)), 3);
        // and a tick again brings it back
        ByKey(content, "filter:co2").OnClick();
        w.Run(Second);
        Assert.Contains(typeof(datum_gas_carbon_dioxide), In(w, () => Dm.each(scrubber.scrubbing).Select(g => (Type)g).ToList()));
        // off with the power button
        content = s.Content(scrubberWindow);
        ByKey(content, "power").OnClick();
        w.Run(Second);
        Assert.Equal(0f, In(w, () => scrubber.on));
        Assert.Equal("scrubber_0", IconOf(s.Replica, scrubber));

        // ---- the pipe scrubber ----
        var pipeScrubber = s.Place<obj_machinery_portable_atmospherics_pipe_scrubber>(0, 1);
        UiWindowState pipeWindow = s.Open(pipeScrubber, "PipeScrubber");
        Assert.True(pipeWindow.Merged.Num("pressureLimitPump") > 0);
        Assert.True(pipeWindow.Merged.Num("pressureLimitTank") > 0);
        Assert.Equal(1, pipeWindow.Merged.Num("direction")); // PUMP_IN: filtering into the tank
        content = s.Content(pipeWindow);
        Assert.Equal(400, s.Windows.Get(pipeWindow.Window).Context.WindowWidth);
        Assert.Equal("Off", ByKey(content, "power").Text);
        Assert.Equal("Buffer → Tank", ByKey(content, "direction").Text);
        Assert.Equal("plug-circle-xmark", ByKey(content, "port").Icon);

        // gas in the buffer (its own air, it is not on a port): carbon dioxide to filter and oxygen to leave
        In(w, () => pipeScrubber.air_contents.merge(SSair.parse_gas_string("co2=20;o2=20;TEMP=293.15") as datum_gas_mixture));
        ByKey(content, "power").OnClick();
        w.Run(Second);
        Assert.Equal(1f, In(w, () => pipeScrubber.on));
        w.Run(6 * Second);
        float stored = Moles(pipeScrubber.internal_tank.air_contents, typeof(datum_gas_carbon_dioxide));
        output.WriteLine($"the pipe scrubber's tank holds {stored} mol of CO2 and {Moles(pipeScrubber.internal_tank.air_contents, typeof(datum_gas_oxygen))} of O2; the buffer {Moles(pipeScrubber.air_contents, typeof(datum_gas_carbon_dioxide))} CO2");
        Assert.True(stored > 5f, "the filtered gas goes into the internal tank: " + stored);
        Assert.Equal(0f, Moles(pipeScrubber.internal_tank.air_contents, typeof(datum_gas_oxygen)));
        content = s.Content(pipeWindow);
        Assert.True(s.Replica.UiWindows[pipeWindow.Window].Merged.Num("pressureTank") > 0);
        Assert.True(ByKey(content, "pressureTank").Value > 0);
        Assert.Contains("_active", IconOf(s.Replica, pipeScrubber) ?? "");

        // the oxygen button: filter it too, and the buffer's oxygen follows
        ByKey(content, "filter:o2").OnClick();
        w.Run(Second);
        Assert.Contains(typeof(datum_gas_oxygen), In(w, () => Dm.each(pipeScrubber.scrubbing).Select(g => (Type)g).ToList()));
        w.Run(8 * Second);
        Assert.True(Moles(pipeScrubber.internal_tank.air_contents, typeof(datum_gas_oxygen)) > 1f);

        // the direction button reverses the flow: the tank empties into the buffer
        float tankBefore = In(w, () => Dm.num(pipeScrubber.internal_tank.air_contents.total_moles()));
        content = s.Content(pipeWindow);
        ByKey(content, "direction").OnClick();
        w.Run(Second);
        Assert.Equal(0f, In(w, () => Dm.num(pipeScrubber.direction)));
        content = s.Content(pipeWindow);
        Assert.Equal("Tank → Buffer", ByKey(content, "direction").Text);
        Assert.DoesNotContain(content.Walk(), n => n.Key?.StartsWith("filter:") == true);
        w.Run(8 * Second);
        Assert.True(In(w, () => Dm.num(pipeScrubber.internal_tank.air_contents.total_moles())) < tankBefore, "the tank empties into the buffer");
        content = s.Content(pipeWindow);
        ByKey(content, "power").OnClick();
        w.Run(Second);
        Assert.Equal(0f, In(w, () => pipeScrubber.on));

        Assert.Equal(s.BootRuntimes, w.World.RuntimeErrorCount);
    }

    [MetaServerFact]
    public void An_engineer_opens_the_tank_in_hand_with_the_secondary_use_and_sets_its_release_pressure()
    {
        using var s = new Setup(78, output);
        RealHeadlessWorld w = s.W;

        // the breath mask on (E) and the emergency tank held, with its valve opened by an alt-click: the internals are on
        In(w, () => DevCommands.Give(s.Me, "/obj/item/clothing/mask/breath"));
        s.W.Run(5);
        s.P.Session.Keybinding(KeybindingCode.QuickEquip);
        s.W.Run(Second);
        Assert.NotNull(In(w, () => s.Me.wear_mask));
        In(w, () => DevCommands.Give(s.Me, "/obj/item/tank/internals/oxygen"));
        var tank = In(w, () => (obj_item_tank)s.Me.get_active_held_item());
        Assert.NotNull(tank);
        w.Run(Second);
        float initialRelease = In(w, () => Dm.num(Dm.initial(tank, "distribute_pressure")));

        // the secondary use (a right-click on the held tank) opens the window: its pressure and the regulator
        Assert.True(s.P.Session.Click(new ClickTarget(TargetKind.Screen, tank.dm_id, 0, 0, 0, 16, 16), ClickButton.Right, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(s.Replica.UiWindows.Tgui());
        Assert.Equal("Tank", window.Interface);
        Assert.Equal(3039, window.Merged.Num("leakPressure")); // round(3039.75): DM's round(x) is a floor
        Assert.Equal(4053, window.Merged.Num("fragmentPressure"));
        Assert.Equal(0, window.Merged.Num("minReleasePressure"));
        Assert.Equal(303, window.Merged.Num("maxReleasePressure"));
        Assert.Equal(16, window.Merged.Num("defaultReleasePressure"));
        Assert.Equal(Math.Floor(In(w, () => Dm.num(tank.air_contents.return_pressure()))), window.Merged.Num("tankPressure"));
        Assert.Equal(Math.Floor(initialRelease), window.Merged.Num("releasePressure"));
        Assert.Equal(0, window.Merged.Num("connected")); // the valve is shut
        UiNode content = s.Content(window);
        Assert.Equal((275, 120), (s.Windows.Get(window.Window).Context.WindowWidth, s.Windows.Get(window.Window).Context.WindowHeight));

        // the regulator's number input, the presets
        ByKey(content, "releasePressure").OnChange(100);
        w.Run(Second);
        Assert.Equal(100f, In(w, () => tank.distribute_pressure));
        Assert.Equal(100, s.Replica.UiWindows[window.Window].Merged.Num("releasePressure"));
        content = s.Content(window);
        Assert.Equal(100, ByKey(content, "releasePressure").Value);
        ByKey(content, "max").OnClick();
        w.Run(Second);
        Assert.Equal(303f, In(w, () => tank.distribute_pressure)); // clamp(round(303.97498)): DM's round(x) is a floor
        ByKey(content, "min").OnClick();
        w.Run(Second);
        Assert.Equal(0f, In(w, () => tank.distribute_pressure));
        ByKey(content, "releasePressure").OnChange(5000);
        w.Run(Second);
        Assert.Equal(TANK_MAX_RELEASE_PRESSURE, In(w, () => tank.distribute_pressure)); // clamped to the regulator's range (the unrounded top)
        ByKey(content, "reset").OnClick();
        w.Run(Second);
        Assert.Equal(initialRelease, In(w, () => tank.distribute_pressure)); // initial(distribute_pressure)

        // the valve: an alt-click on the held tank opens the internals; the window knows the tank is connected
        Assert.True(s.P.Session.Click(new ClickTarget(TargetKind.Screen, tank.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.Alt));
        w.Run(Second);
        Assert.Same(tank, In(w, () => s.Me.@internal));
        w.Run(2 * Second);
        Assert.Equal(1, s.Replica.UiWindows[window.Window].Merged.Num("connected"));

        // the release pressure the tank delivers follows the regulator: the mask is fed the set pressure
        ByKey(content, "releasePressure").OnChange(50);
        w.Run(Second);
        Assert.Equal(50f, In(w, () => tank.distribute_pressure));
        Assert.Equal(s.BootRuntimes, w.World.RuntimeErrorCount);
    }
}
