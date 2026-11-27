using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
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
/// Package E16 on MetaStation through the real server and a client's own stack: the two barometric monitors in cargo show the all-clear
/// screen and green light (no mining level is loaded); a big manipulator built in a free spot is set up through its BigManipulator window
/// (a pickup on the tile west of it, a drop on the tile east) and run, its arm (a visual on the manipulator) turns and a crowbar put west
/// arrives east; a sort-router on a cable with a smelter beside it sends an ore into the smelter, which smelts it, and the
/// ManufacturingSorter window (a multitool's) turns its filter; a portable gravity unit switched on with a right click lays its field (the field tiles
/// reach the client) and its Portagrav window raises the gravity around it to 2.
/// </summary>
public class ManipulatorManufacturingGameplayTests(ITestOutputHelper output)
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

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private static bool Clear(turf t) => t is turf_open && t is not turf_open_space && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    /// <summary>The centre of the nearest clear 5 by 3 block of the station around <paramref name="near"/> with no machine or cable in it.</summary>
    private static turf Block(atom near, ICollection<turf> taken)
    {
        bool Free(turf t) => t != null && Clear(t) && !taken.Contains(t)
            && !Dm.each_cast<atom>(t.contents).Any(a => a is obj_machinery_atmospherics || a is obj_structure_cable || a is obj_machinery || a is obj_structure);
        for (int r = 2; r <= 80; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    turf c = locate(near.x + dx, near.y + dy, near.z);
                    if (c == null || get_area(c) is not area_station || (Math.Abs(dx) != r && Math.Abs(dy) != r))
                        continue;
                    bool ok = true;
                    for (int i = -2; i <= 2 && ok; i++)
                        for (int j = -1; j <= 1 && ok; j++)
                            ok = Free(locate(c.x + i, c.y + j, c.z));
                    if (ok)
                        return c;
                }
            }
        }
        return null;
    }

    private static void Take(RealHeadlessWorld w, turf c, ICollection<turf> taken) => In(w, () =>
    {
        for (int i = -2; i <= 2; i++)
            for (int j = -1; j <= 1; j++)
                taken.Add(locate(c.x + i, c.y + j, c.z));
    });

    private static ClickTarget On(atom a) => new ClickTarget(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static AppearanceEntry Look(ReplicaWorld replica, long id)
    {
        ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == id);
        return seen != null && replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? look : null;
    }

    private static List<string> OverlaysOf(ReplicaWorld replica, atom a)
    {
        var shown = new List<string>();
        AppearanceEntry look = Look(replica, a.dm_id);
        if (look == null)
            return shown;
        foreach (TableRef o in look.Overlays)
        {
            if (replica.TryGetAppearance(o, out AppearanceEntry overlay))
                shown.Add(replica.ResolveString(overlay.IconState));
        }
        return shown;
    }

    private static string IconStateOf(ReplicaWorld replica, atom a)
    {
        AppearanceEntry look = Look(replica, a.dm_id);
        return look == null ? null : replica.ResolveString(look.IconState);
    }

    private UiWindowState OpenBy(RealHeadlessWorld w, RealHeadlessWorld.Player p, ReplicaWorld replica, atom target, string iface, ClickButton button = ClickButton.Left)
    {
        Assert.True(p.Session.Click(On(target), button, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = replica.UiWindows.Tgui().SingleOrDefault(win => win.Interface == iface);
        Assert.True(window != null, $"the {iface} window: {string.Join(", ", replica.UiWindows.Tgui().Select(win => win.Interface))}");
        return window;
    }

    private sealed class Tables(ReplicaWorld replica) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }

    [MetaServerFact]
    public void Metas_weather_monitors_show_all_clear_a_manipulator_moves_an_item_a_smelter_smelts_and_a_portagrav_raises_the_gravity()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 71, TimeoutSteps = 12000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        var runtimes = new List<string>();
        Action<Exception> previous = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.ToString()); previous?.Invoke(e); };
        In(w, () => ADD_TRAIT(me, TRAIT_ADVANCEDTOOLUSER, ADMIN_TRAIT));

        // ---- the two barometric monitors (cargo): the weather announcer's all-clear screen and green light, seen by a client beside them
        // (cargo's: Lavaland's ruins hold more, as their seeding rolls)
        List<obj_machinery_mining_weather_monitor> monitors = In(w, () => Dm.each<obj_machinery_mining_weather_monitor>(Dm.world).Where(m => Dm.truthy(is_station_level(m.z))).ToList());
        Assert.Equal(2, monitors.Count);
        In(w, () => Waiting.NoSleep(me.forceMove(monitors[0].loc)));
        w.Run(2 * Second);
        // what each shows follows the announcer's warning level (weatherannouncer.dm:65, :117): all clear, or Lavaland's ash storm
        // when SSweather has rolled one in the round's first seconds
        string shown = null;
        foreach (var monitor in monitors)
        {
            In(w, () =>
            {
                Assert.IsType<obj_machinery_mining_weather_monitor_directional_east>(monitor);
                var announcer = (datum_component_weather_announcer)monitor.GetComponent(typeof(datum_component_weather_announcer));
                bool danger = Dm.truthy(announcer.is_weather_dangerous);
                (string color, string state) = Dm.num(announcer.warning_level) switch
                {
                    0f => (LIGHT_COLOR_GREEN, "wallgreen"),
                    1f => (LIGHT_COLOR_DIM_YELLOW, "wallyellow"),
                    _ => danger ? (LIGHT_COLOR_INTENSE_RED, "wallred") : (LIGHT_COLOR_DIM_YELLOW, "wallyellow"),
                };
                Assert.Equal(color, Dm.str(monitor.light_color));
                shown ??= state;
            });
        }
        Assert.Contains(shown, OverlaysOf(replica, monitors[0]));

        // ---- a big manipulator in a free spot of the station, set up through its window: pick up west, drop east
        var taken = new List<turf>();
        turf mc = In(w, () => Block(me, taken));
        Assert.NotNull(mc);
        Take(w, mc, taken);
        output.WriteLine($"the manipulator at {In(w, () => $"{mc.x},{mc.y}")}");
        In(w, () => Waiting.NoSleep(me.forceMove(locate(mc.x, mc.y + 1, mc.z))));
        var manipulator = In(w, () => Dm.@new<obj_machinery_big_manipulator>(mc));
        w.Run(Second);
        UiWindowState window = OpenBy(w, p, replica, manipulator, "BigManipulator");
        UiNode content = Shown(windows, replica, window.Window);
        ByKey(content, "create_task").OnClick(); // the default type: pickup
        w.Run(Second);
        ByKey(Shown(windows, replica, window.Window), "task_type").OnSelected("Drop...");
        ByKey(Shown(windows, replica, window.Window), "create_task").OnClick();
        w.Run(Second);
        IReadOnlyList<UiValue> tasks = replica.UiWindows[window.Window].Merged.Arr("tasks_data");
        Assert.Equal(new[] { "pickup", "drop" }, tasks.Select(t => t.Str("task_type")));
        // each task's tile through the editor's point grid: button 4 is west, 6 is east
        foreach ((string id, int button) in new[] { (tasks[0].Str("id"), 4), (tasks[1].Str("id"), 6) })
        {
            ByKey(Shown(windows, replica, window.Window), "edit:" + id).OnClick();
            ByKey(Shown(windows, replica, window.Window), "point:" + button).OnClick();
            w.Run(Second);
            ByKey(Shown(windows, replica, window.Window), "close").OnClick();
        }
        tasks = replica.UiWindows[window.Window].Merged.Arr("tasks_data");
        Assert.Equal(new[] { "-1,0", "1,0" }, tasks.Select(t => t.Str("turf")));
        ByKey(Shown(windows, replica, window.Window), "run_cycle").OnClick();
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(manipulator.on)), "the manipulator runs (Meta's area power)");
        Assert.Equal("Stop", ByKey(Shown(windows, replica, window.Window), "run_cycle").Text);

        // the arm is a visual on the manipulator
        ReplicaEntity arm = replica.Entities.SingleOrDefault(e => e.IsVisual && e.VisHost == manipulator.dm_id);
        Assert.NotNull(arm);
        Assert.Equal("/obj/effect/big_manipulator_arm", arm.TypePath);

        // a crowbar put on the pickup tile goes to the drop tile; the arm's turned transform reaches the client
        var crowbar = In(w, () => Dm.@new<obj_item_crowbar>(locate(mc.x - 1, mc.y + 1, mc.z)));
        In(w, () => Waiting.NoSleep(crowbar.forceMove(locate(mc.x - 1, mc.y, mc.z))));
        var transforms = new HashSet<string>();
        for (int n = 0; n < 8; n++)
        {
            w.Run(Second / 2);
            AppearanceEntry look = Look(replica, arm.Id);
            if (look?.Transform != null)
                transforms.Add(string.Join(",", look.Transform.Select(f => MathF.Round(f, 2))));
        }
        output.WriteLine($"the arm's transforms seen: {string.Join(" | ", transforms)}");
        Assert.Equal((mc.x + 1f, (float)mc.y), In(w, () => (((atom)crowbar.loc).x, ((atom)crowbar.loc).y)));
        Assert.True(transforms.Count >= 2, "the arm turned toward the pickup and the drop tiles");

        // the client draws the arm into the manipulator's picture (EntityLayer: VisInsets), over the core
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root != null)
        {
            var compositor = new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(root)));
            var insets = new VisInsets(new Tables(replica));
            ReplicaEntity core = replica.Entities.Single(e => e.Id == manipulator.dm_id);
            ReplicaEntity seenArm = replica.Entities.Single(e => e.Id == arm.Id);
            Assert.True(compositor.TryCompose(new Tables(replica), core.State.Appearance, ByondDir.South, 0, out ComposedRaster bare), string.Join("; ", compositor.Library.Problems));
            TableRef withArm = insets.With(core.State.Appearance, seenArm.State.Appearance, 0, 0, 255);
            Assert.True(compositor.TryCompose(insets, withArm, ByondDir.South, 0, out ComposedRaster drawn), string.Join("; ", compositor.Library.Problems));
            output.WriteLine($"the manipulator's picture: {bare.Image.Width} x {bare.Image.Height}, with its arm {drawn.Image.Width} x {drawn.Image.Height}");
            Assert.True(drawn.Image.Width > bare.Image.Width, $"the arm's 96 x 96 claw widens the picture ({bare.Image.Width} -> {drawn.Image.Width})");
        }
        ByKey(Shown(windows, replica, window.Window), "run_cycle").OnClick();
        p.Session.UiClose(window.Window);
        w.Run(2 * Second);

        // ---- a sort-router and a smelter on a fresh cable with a generator: ore onto the router goes into the smelter and is smelted
        turf sc = In(w, () => Block(me, taken));
        Assert.NotNull(sc);
        Take(w, sc, taken);
        In(w, () => Waiting.NoSleep(me.forceMove(locate(sc.x, sc.y + 1, sc.z))));
        w.Run(Second);
        var (sorter, smelter) = In(w, () =>
        {
            for (int i = -2; i <= 1; i++)
                Dm.@new<obj_structure_cable>(locate(sc.x + i, sc.y, sc.z)).propagate_if_no_network(); // a fresh cable's own powernet (cable.dm:434)
            Dm.@new<obj_machinery_power_rtg_debug>(locate(sc.x - 2, sc.y, sc.z));
            var router = Dm.@new<obj_machinery_power_manufacturing_sorter>(sc);
            var oven = Dm.@new<obj_machinery_power_manufacturing_smelter>(locate(sc.x + 1, sc.y, sc.z));
            Waiting.NoSleep(oven.setDir(WEST)); // its back (the input side) is east of the router
            var filter = Dm.@new<datum_sortrouter_filter_is_ore>(router);
            filter.dir_target = WEST;
            Dm.append(router.sort_filters, filter);
            return (router, oven);
        });
        w.Run(5 * Second); // the generator's power reaches the cable
        // the window (a multitool opens it: tg's sorter has no INTERACT_ATOM_UI_INTERACT, its multitool_act calls ui_interact): the filter
        // listed; its output turned from west to east
        var multitool = In(w, () => Dm.@new<obj_item_multitool>(me.loc));
        In(w, () => Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(multitool)))));
        w.Run(2);
        window = OpenBy(w, p, replica, sorter, "ManufacturingSorter");
        content = Shown(windows, replica, window.Window);
        Assert.Equal("1. input is ore", content.Walk().First(n => n.Kind == UiKind.LabeledItem).Label);
        string filterRef = replica.UiWindows[window.Window].Merged.Arr("filters")[0].Str("ref");
        Assert.Equal("arrow-left", ByKey(content, "rotate:" + filterRef).Icon);
        ByKey(content, "rotate:" + filterRef).OnClick(); // west to north
        w.Run(Second / 2);
        ByKey(Shown(windows, replica, window.Window), "rotate:" + filterRef).OnClick(); // north to south
        w.Run(Second / 2);
        ByKey(Shown(windows, replica, window.Window), "rotate:" + filterRef).OnClick(); // south to east
        w.Run(Second);
        Assert.Equal("arrow-right", ByKey(Shown(windows, replica, window.Window), "rotate:" + filterRef).Icon);
        p.Session.UiClose(window.Window);
        In(w, () => qdel(multitool));
        var ore = In(w, () => Dm.@new<obj_item_stack_ore_silver>(locate(sc.x, sc.y + 1, sc.z)));
        In(w, () => Waiting.NoSleep(ore.forceMove(sc)));
        w.Run(Second / 2);
        Assert.Same(smelter, In(w, () => ore.loc));
        var icons = new HashSet<string>();
        for (int n = 0; n < 6; n++)
        {
            w.Run(Second / 2);
            icons.Add(IconStateOf(replica, smelter));
        }
        Assert.True(In(w, () => QDELETED(ore)), "the smelter smelted the ore");
        Assert.Contains("smelter_on", icons);
        Assert.Contains(OverlaysOf(replica, smelter), s => s == "unres_8" || s == "unres_4"); // its in and out arrows

        // ---- a portable gravity unit: a right click switches it on, its field tiles reach the client, its window raises the gravity to 2
        turf pc = In(w, () => Block(me, taken));
        Assert.NotNull(pc);
        Take(w, pc, taken);
        In(w, () => Waiting.NoSleep(me.forceMove(locate(pc.x + 1, pc.y, pc.z))));
        var portagrav = In(w, () => Dm.@new<obj_machinery_power_portagrav_anchored>(pc));
        w.Run(Second);
        Assert.True(p.Session.Click(On(portagrav), ClickButton.Right, ClickModifiers.None));
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(portagrav.on)), "the right click switched it on");
        Assert.True(In(w, () => HAS_TRAIT(me.loc, TRAIT_FORCED_GRAVITY)));
        Assert.Contains(replica.Entities, e => e.TypePath == "/obj/effect/gravity_fluff_field");
        Assert.Contains("activated", OverlaysOf(replica, portagrav));
        window = OpenBy(w, p, replica, portagrav, "Portagrav");
        Assert.EndsWith("% - 1G", ByKey(Shown(windows, replica, window.Window), "power").Text); // the cell's charge, the strength
        ByKey(Shown(windows, replica, window.Window), "grav_up").OnClick();
        w.Run(Second);
        Assert.Equal(2f, In(w, () => portagrav.grav_strength));
        Assert.Equal(2f, In(w, () => Dm.num(me.has_gravity())));
        Assert.EndsWith("% - 2G", ByKey(Shown(windows, replica, window.Window), "power").Text);
        p.Session.UiClose(window.Window);
        w.Run(2);

        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, string.Join("\n", runtimes));
    }
}
