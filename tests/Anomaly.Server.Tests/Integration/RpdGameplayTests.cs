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
/// M7 slice D172 (RPD.dm) on MetaStation through the real server and a client's own stack: an engineer in a maintenance corridor with a
/// rapid pipe dispenser and two lone pipes three tiles apart (the plating shows its pipes). He opens the RPD's window by using it in hand,
/// picks red paint, then, through the window's tabs and previews, a smart pipe, a pressure pump facing east and a smart pipe again, and
/// lays them by clicking the three floors between the pipes: after tg's 0.4 s each fitting appears and is wrenched down (the Connect
/// mode). The run is one pipeline on each side of the pump; his client sees the new pipes and the pump red on layer 3 and hears the RPD's
/// switch and dispense sounds. D320 (DV352): the window has one button per recipe; he turns the pump east with Shift and the mouse wheel,
/// and his client's build preview (protocol version 70, the ghost on the aimed tile) follows the recipe, its colour and its direction.
/// </summary>
public class RpdGameplayTests(ITestOutputHelper output)
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

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private static bool Clear(turf t) => t is turf_open_floor_plating && !Dm.truthy(t.density)
        && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density) || a is obj_machinery_atmospherics || a is obj_item || a is mob);

    // five clear plating tiles in a row with a clear one north of the middle (a maintenance corridor), in id order
    private static (int X, int Y) Corridor(RealHeadlessWorld w) => In(w, () =>
    {
        foreach (turf_open_floor_plating t in Dm.each<turf_open_floor_plating>(Dm.world).OrderBy(t => t.dm_id))
        {
            if (!Dm.eq(t.z, 1))
                continue;
            int tx = (int)t.x, ty = (int)t.y;
            if (Enumerable.Range(0, 5).All(i => locate(tx + i, ty, 1) is turf r && Clear(r)) && locate(tx + 2, ty + 1, 1) is turf s && Clear(s))
                return (tx, ty);
        }
        return (0, 0);
    });

    private static float PIPE_LAYER(float n) => Dm.shl(1, n - 1);

    private static uint Rgba(string css) => (Convert.ToUInt32(css.TrimStart('#'), 16) << 8) | 0xFF;

    [MetaServerFact]
    public void An_engineer_lays_a_red_pipe_run_with_a_pump_between_two_pipes_from_the_rpd_window()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 172, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        // Meta's own RPDs (the atmospherics and engineering storage; the atmospheric technicians' and the CE's lockers) are live
        List<obj_item_pipe_dispenser> mapped = In(w, () => Dm.each<obj_item_pipe_dispenser>(Dm.world).ToList());
        output.WriteLine($"{mapped.Count} RPDs on the station");
        Assert.True(mapped.Count >= 6);
        In(w, () => Assert.All(mapped, r => Assert.Same(obj_item_pipe_dispenser.first_atmos, r.recipe)));

        (int x, int y) = Corridor(w);
        Assert.NotEqual(0, x);
        output.WriteLine($"the run from {x},{y} to {x + 4},{y}");

        // the two lone pipes at the ends, as a map template's (omni coloured: they join any colour)
        var ends = In(w, () =>
        {
            var west = Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(x, y, 1), TRUE, EAST);
            var east = Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(x + 4, y, 1), TRUE, EAST);
            SSair.setup_template_machinery(Dm.list(west, east));
            return (west, east);
        });
        In(w, () => Waiting.NoSleep(me.forceMove(locate(x + 2, y + 1, 1))));
        In(w, () => ADD_TRAIT(me, TRAIT_ADVANCEDTOOLUSER, "test")); // the engineer's hands (a fresh joined human lacks the job's traits)
        var rpd = In(w, () => Dm.@new<obj_item_pipe_dispenser>(me.loc));
        In(w, () => Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_active_hand(rpd)))));
        w.Run(2 * Second);

        // the RPD in hand: its window
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("RapidPipeDispenser", window.Interface);
        UiNode Shown()
        {
            windows.Sync(replica.UiWindows, 1280, 720, 1);
            return windows.Get(window.Window).Content;
        }
        UiNode content = Shown();
        Assert.Equal(550, windows.Get(window.Window).Context.WindowWidth);
        Assert.Contains("Atmospherics", content.AllText());
        Assert.Equal(TguiSpritesheets.Pipes32, ByKey(content, "preview-Pipe").DmIcon);
        // D320: the RPD in hand in its Dispense mode: the ghost of the first recipe, tg's smart pipe fitting, on the hovered floor next to him
        Assert.False(p.Client.BuildPreview.IsSet); // nothing hovered yet
        p.Session.Hover(new ClickTarget(TargetKind.Turf, 0, x + 2, y, 1), 1000);
        w.Run(2);
        Assert.Equal((x + 2, y, 1), (p.Client.BuildPreview.X, p.Client.BuildPreview.Y, p.Client.BuildPreview.Z));
        Assert.Equal(("icons/obj/pipes_n_cables/pipe_item.dmi", "manifold4w", (byte)2), (p.Client.BuildPreview.Icon, p.Client.BuildPreview.State, p.Client.BuildPreview.Dir)); // dir 2: its one preview faces south

        // red paint
        heard.Clear();
        ByKey(content, "color-red").OnClick();
        w.Run(Second);
        Assert.Equal("red", In(w, () => rpd.paint_color));
        content = Shown();
        Assert.True(ByKey(content, "color-red").Selected);
        Assert.Contains(heard, e => e.File == "sound/items/tools/tool_switch.ogg"); // ui_act's switch
        Assert.DoesNotContain(heard, e => e.File == "sound/effects/pop.ogg"); // the colour act returns before the sparks and the pop

        void Lay(int tx)
        {
            Assert.True(p.Session.Click(new ClickTarget(TargetKind.Turf, 0, tx, y, 1), ClickButton.Left, ClickModifiers.None));
            w.Run(Second);
        }

        // the smart pipe (the first recipe), the pump facing east from the Binary tab, the smart pipe again from the Pipes tab
        heard.Clear();
        Lay(x + 1);
        ByKey(content, "tab-Binary").OnClick();
        content = Shown();
        ByKey(content, "preview-Pressure Pump").OnClick();
        w.Run(Second);
        Assert.Equal(typeof(obj_machinery_atmospherics_components_binary_pump), In(w, () => rpd.recipe.id));
        Assert.Equal(NORTH, In(w, () => rpd.p_dir));
        // D320: Shift and the wheel turn it through its directions until it faces east; the ghost turns with it, red
        for (int turn = 0; turn < 8 && In(w, () => rpd.p_dir) != EAST; turn++)
        {
            p.Session.MouseWheel(new ClickTarget(TargetKind.Turf, 0, x + 2, y, 1), up: false, ClickModifiers.Shift);
            w.Run(2);
            Assert.Equal((byte)In(w, () => rpd.p_dir), p.Client.BuildPreview.Dir);
        }
        Assert.Equal(EAST, In(w, () => rpd.p_dir));
        Assert.Equal("pump", p.Client.BuildPreview.State);
        Assert.Equal(Rgba(COLOR_RED), p.Client.BuildPreview.Color);
        // the wheel alone is tg's: the layer (from 3 down to 4), and back
        p.Session.MouseWheel(new ClickTarget(TargetKind.Turf, 0, x + 2, y, 1), up: false, ClickModifiers.None);
        w.Run(2);
        Assert.Equal(PIPE_LAYER(4), In(w, () => rpd.pipe_layers));
        Assert.Equal(5, p.Client.BuildPreview.PixelY); // tg's PIPING_LAYER_SHIFT of an east fitting on layer 4
        p.Session.MouseWheel(new ClickTarget(TargetKind.Turf, 0, x + 2, y, 1), up: true, ClickModifiers.None);
        w.Run(2);
        Assert.Equal(PIPE_LAYER(3), In(w, () => rpd.pipe_layers));
        Assert.Contains(heard, e => e.File == "sound/effects/pop.ogg"); // the pipe_type act's sparks and pop (RPD.dm:286)
        Lay(x + 2);
        content = Shown();
        ByKey(content, "tab-Pipes").OnClick();
        content = Shown();
        ByKey(content, "preview-Pipe").OnClick();
        w.Run(Second);
        Lay(x + 3);

        var run = In(w, () => Enumerable.Range(1, 3).Select(i => Dm.each<obj_machinery_atmospherics>(locate(x + i, y, 1)).Single()).ToList());
        In(w, () =>
        {
            Assert.IsType<obj_machinery_atmospherics_pipe_smart>(run[0]);
            var pump = Assert.IsType<obj_machinery_atmospherics_components_binary_pump>(run[1]);
            Assert.IsType<obj_machinery_atmospherics_pipe_smart>(run[2]);
            Assert.Equal(EAST, Dm.num(pump.dir));
            Assert.All(run, m => Assert.Equal((COLOR_RED, 3f), (m.pipe_color, m.piping_layer)));
            Assert.Same(run[0], pump.nodes[1]);
            Assert.Same(run[2], pump.nodes[2]);
            Assert.Same(ends.west.parent, ((obj_machinery_atmospherics_pipe)run[0]).parent);
            Assert.Same(ends.east.parent, ((obj_machinery_atmospherics_pipe)run[2]).parent);
            Assert.Same(pump.parents[1], ends.west.parent);
            Assert.Same(pump.parents[2], ends.east.parent);
            Assert.Empty(Dm.each<obj_item_pipe>(locate(x + 2, y, 1))); // each fitting was wrenched down
        });

        // what his client sees: the two pipes red on layer 3 ("<connections>_3"); the pump's body is tg's grey and its two pipe ends (its
        // underlays) red
        foreach (obj_machinery_atmospherics m in run)
        {
            ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == m.dm_id);
            Assert.NotNull(seen);
            Assert.True(replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look));
            string state = replica.ResolveString(look.IconState);
            var under = look.Underlays.Select(u => replica.TryGetAppearance(u, out AppearanceEntry a) ? a : null).Where(a => a != null).ToList();
            output.WriteLine($"{m.GetType().Name}: {replica.ResolveResource(look.Icon)} {state} c{look.Rgba:X8}; underlays "
                + string.Join(", ", under.Select(a => $"{replica.ResolveString(a.IconState)} c{a.Rgba:X8}")));
            if (m is obj_machinery_atmospherics_pipe_smart)
            {
                Assert.Equal(Rgba(COLOR_RED), look.Rgba);
                Assert.EndsWith("_3", state);
            }
            else
            {
                Assert.Equal(In(w, () => Dm.str(m.icon_state)), state);
                Assert.Equal(2, under.Count(a => a.Rgba == Rgba(COLOR_RED)));
            }
        }
        // and hears the RPD: the switch before each build, the dispense after its do_after
        Assert.True(heard.Count(e => e.File == "sound/items/tools/tool_switch.ogg") >= 3);
        Assert.Equal(3, heard.Count(e => e.File == "sound/items/deconstruct.ogg"));

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    /// <summary>
    /// The temperature gate (temperature_gate.dm), one of the four devices that were inert before: the engineer picks it in the RPD's Binary tab
    /// and lays it between the two pipes; it is wrenched down and joins both pipelines. With the RPD away he clicks it with his empty hand: tg's
    /// AtmosTempGate window opens, its power button turns the gate on and its number input sets the temperature he types.
    /// </summary>
    [MetaServerFact]
    public void An_engineer_lays_a_temperature_gate_with_the_rpd_and_opens_its_window()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 173, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        int bootRuntimes = w.World.RuntimeErrorCount;
        UiNode Shown(UiWindowState window)
        {
            windows.Sync(replica.UiWindows, 1280, 720, 1);
            return windows.Get(window.Window).Content;
        }

        (int x, int y) = Corridor(w);
        Assert.NotEqual(0, x);
        var ends = In(w, () =>
        {
            var west = Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(x, y, 1), TRUE, EAST);
            var east = Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(x + 2, y, 1), TRUE, EAST);
            SSair.setup_template_machinery(Dm.list(west, east));
            return (west, east);
        });
        In(w, () => Waiting.NoSleep(me.forceMove(locate(x + 1, y + 1, 1))));
        In(w, () => ADD_TRAIT(me, TRAIT_ADVANCEDTOOLUSER, "test"));
        var rpd = In(w, () => Dm.@new<obj_item_pipe_dispenser>(me.loc));
        In(w, () => Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_active_hand(rpd)))));
        w.Run(2 * Second);

        // the RPD's window, the Binary tab, the temperature gate facing east
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        UiWindowState rpdWindow = Assert.Single(replica.UiWindows.Tgui());
        ByKey(Shown(rpdWindow), "tab-Binary").OnClick();
        ByKey(Shown(rpdWindow), "preview-Temperature Gate").OnClick();
        w.Run(Second);
        for (int turn = 0; turn < 8 && In(w, () => rpd.p_dir) != EAST; turn++) // D320: Shift and the wheel turn it east
        {
            p.Session.MouseWheel(default, up: true, ClickModifiers.Shift);
            w.Run(2);
        }
        Assert.Equal(EAST, In(w, () => rpd.p_dir));
        Assert.Equal(typeof(obj_machinery_atmospherics_components_binary_temperature_gate), In(w, () => rpd.recipe.id));
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Turf, 0, x + 1, y, 1), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        var gate = In(w, () => Dm.each<obj_machinery_atmospherics_components_binary_temperature_gate>(locate(x + 1, y, 1)).Single());
        In(w, () =>
        {
            Assert.Empty(Dm.each<obj_item_pipe>(locate(x + 1, y, 1))); // wrenched down
            Assert.Same(ends.west, gate.nodes[1]);
            Assert.Same(ends.east, gate.nodes[2]);
            Assert.Same(gate.parents[1], ends.west.parent);
            Assert.Same(gate.parents[2], ends.east.parent);
            Assert.False(Dm.truthy(gate.on));
        });
        p.Session.UiClose(rpdWindow.Window);
        w.Run(2);

        // the RPD put down, an empty hand on the gate opens its window
        In(w, () => Waiting.NoSleep(me.dropItemToGround(rpd)));
        w.Run(Second);
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, gate.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = replica.UiWindows.Values.Single(v => v.Interface == "AtmosTempGate");
        UiNode content = Shown(window);
        Assert.Equal("Off", ByKey(content, "power").Text);
        Assert.Equal(273.0, ByKey(content, "temperature").Value); // round(T0C)
        ByKey(content, "power").OnClick();
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(gate.on)));
        Assert.Equal("On", ByKey(Shown(window), "power").Text);
        ByKey(Shown(window), "temperature").OnChange(350);
        w.Run(Second);
        Assert.Equal(350f, In(w, () => gate.target_temperature));
        Assert.Equal(350.0, ByKey(Shown(window), "temperature").Value);
        ByKey(Shown(window), "temperature-max").OnClick();
        w.Run(Second);
        Assert.Equal(4500f, In(w, () => gate.target_temperature));
        Assert.True(ByKey(Shown(window), "temperature-max").Disabled);
        p.Session.UiClose(window.Window);
        w.Run(2);

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
