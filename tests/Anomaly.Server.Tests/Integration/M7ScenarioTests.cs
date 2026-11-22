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
/// ROADMAP M7's demo check through the real server on MetaStation, end to end, with two headless Godot clients
/// (<see cref="PlaySession"/>): an engineer and a watcher standing in the room. The room is found at run time, the first by the APCs'
/// order that has an APC hanging over a single cable, its feeder from the station's grid (powered from round start by tg's charged
/// SMESes, D162), lit fixtures, a powered airlock with free floor on both sides, a running vent and scrubber (on Meta: the Auxiliary
/// Base Construction room, the M5 check's). The engineer (the Captain's spare as the access, the M4c and M5
/// checks' stand-in; insulated gloves on, the shock is the shocks slice's) swipes the spare on the APC (togglelock), disengages the
/// cover lock in tg's Apc window and pries the cover open with a crowbar; he takes the APC's battery out by hand. As tg's
/// late_process dictates (apc_main.dm:671-676) an APC without a cell switches every channel off at its next cycle, grid or not:
/// the room goes dark at once. tg's APC runs on its cell when the grid is cut and turns its channels off only as the cell drops
/// (apc_main.dm:637-669), and the 2.5 MJ battery would keep the room lit long after a cut; so he puts a standard 10 kJ power cell in,
/// the room comes back on it, and he closes the cover. Then he cuts the APC's feeder cable with wirecutters: over SSmachines cycles (2 s each)
/// the small cell runs down, the lights go to their emergency light (the fixtures off, the area's power_light 0;
/// the watcher's client sees the fixtures' lights change and the tile's lumcount falls), the airlock no longer opens for him
/// bumping it (unpowered), and the vent and the scrubber stop (NOPOWER, tg's power_change and update_icon_nopipes: "vent_off",
/// "scrub_off" in the client). He lays the cable back with the piece the cut left: the grid charges the cell at tg's capped rate,
/// the environment comes back at once (the door opens for him, its sound heard, the vents run) and the lights at 15 % of the cell.
/// Last he swipes the spare again to lock the interface. The APC's icon and overlays in the client (D142) are checked at each step,
/// and the cycles each transition took are measured and printed (seed 77: the battery out, dark at the next cycle; from the cut,
/// the lights off in 19 cycles and the environment in 30, the room drawing about 220 W, so the 2.25 MJ battery would have lasted
/// about 2.3 h; from the lay, the environment on in 1 cycle, the lights in 9, the equipment in 17).
/// </summary>
public class M7ScenarioTests
{
    private const int Second = 20;
    private const ulong Seed = 77;

    private const string AirlockOpen = "sound/machines/airlock/airlock.ogg";

    private readonly ITestOutputHelper _output;

    public M7ScenarioTests(ITestOutputHelper output) => _output = output;

    private sealed class Player
    {
        public RealHeadlessWorld.Player P;
        public mob_living_carbon_human Mob;
        public ReplicaWorld Replica => P.Session.Replica;
        public readonly List<(long Tick, SoundEvent E)> Heard = new();
    }

    private sealed record Room(obj_machinery_power_apc Apc, area Area, obj_structure_cable Cable, obj_machinery_door_airlock Door, turf DoorSide,
        obj_machinery_atmospherics_components_unary_vent_pump Vent, obj_machinery_atmospherics_components_unary_vent_scrubber Scrubber,
        List<obj_machinery_light> Lights, turf WatchSpot);

    private RealHeadlessWorld _w;

    private T In<T>(Func<T> f)
    {
        using (_w.World.Enter())
            return f();
    }

    private void Do(Action a)
    {
        using (_w.World.Enter())
            a();
    }

    private void Run(float seconds) => _w.Run((int)(seconds * Second));

    private Player Join(string name)
    {
        var p = new Player { P = _w.Join(name) };
        p.Mob = (mob_living_carbon_human)_w.MobOf(p.P);
        p.Replica.SoundsReceived += events => p.Heard.AddRange(events.Select(s => (_w.Ticks, s)));
        return p;
    }

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    // A station room whose APC hangs over a single cable, its feeder from the grid, with lit fixtures, a closed powered airlock
    // with free floor on both sides, a running vent and a running scrubber of its own, and a free tile beside a fixture.
    private static Room PickRoom()
    {
        Dictionary<area, List<T>> ByArea<T>(Func<T, bool> keep) where T : atom
            => Dm.each<T>(Dm.world).Where(a => a.loc is turf && Dm.eq(a.z, 1) && keep(a)).GroupBy(a => get_area(a) as area).Where(g => g.Key != null).ToDictionary(g => g.Key, g => g.ToList());
        var lights = ByArea<obj_machinery_light>(l => Dm.truthy(l.on) && Dm.eq(l.status, LIGHT_OK));
        var doors = ByArea<obj_machinery_door_airlock>(d => !Dm.truthy(Dm.bitand(d.machine_stat, NOPOWER)) && Dm.truthy(d.density) && !Dm.truthy(d.locked) && !Dm.truthy(d.welded));
        var vents = ByArea<obj_machinery_atmospherics_components_unary_vent_pump>(v => Dm.truthy(v.on) && v.icon_state != "vent_off");
        var scrubbers = ByArea<obj_machinery_atmospherics_components_unary_vent_scrubber>(s => Dm.truthy(s.on) && s.icon_state != "scrub_off");
        foreach (obj_machinery_power_apc apc in Dm.each<obj_machinery_power_apc>(Dm.world).OrderBy(a => a.dm_id))
        {
            area room = apc.area;
            if (room == null || !Dm.eq(apc.z, 1) || !Dm.@in(room.type, GLOB.the_station_areas) || !Dm.truthy(room.requires_power))
                continue;
            if (apc.loc is not turf_open t || apc.terminal?.powernet == null || apc.cell == null || !Clear(t))
                continue;
            var cables = Dm.each_cast<atom>(t.contents).OfType<obj_structure_cable>().ToList();
if (cables.Count != 1 || cables[0].powernet != apc.terminal.powernet || !Dm.truthy(cables[0].linked_dirs))
                continue;
            if (!lights.TryGetValue(room, out var lit) || lit.Count < 2 || !doors.TryGetValue(room, out var roomDoors)
                || !vents.TryGetValue(room, out var vent) || !scrubbers.TryGetValue(room, out var scrubber))
                continue;
            (obj_machinery_door_airlock door, turf side) = roomDoors.Select(d =>
            {
                foreach (float dir in new[] { NORTH, EAST })
                {
                    turf a = get_step(d, dir), b = get_step(d, REVERSE_DIR(dir));
                    if (a != null && b != null && Clear(a) && Clear(b))
                        return (d, ReferenceEquals(get_area(a), room) ? a : ReferenceEquals(get_area(b), room) ? b : null);
                }
                return (d, (turf)null);
            }).FirstOrDefault(x => x.Item2 != null);
            if (door == null)
                continue;
            turf spot = lit.SelectMany(l => Dm.each_cast<turf>(RANGE_TURFS(1, (turf)l.loc)))
                .FirstOrDefault(s => Clear(s) && ReferenceEquals(get_area(s), room) && s != t && s != side);
            if (spot == null)
                continue;
            return new Room(apc, room, cables[0], door, side, vent[0], scrubber[0], lit, spot);
        }
        return null;
    }

    private void Click(Player p, atom target, float seconds = 1)
    {
        Assert.True(p.P.Session.Click(new ClickTarget(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        Run(seconds);
    }

    private void Key(Player p, KeybindingCode code, float seconds = 0.5f)
    {
        p.P.Session.Keybinding(code);
        Run(seconds);
    }

    private obj_item Give(Player p, string path)
    {
        _w.Host.EnqueueConsoleLine($"give {p.P.Name} {path}");
        Run(0.5f);
        var held = In(() => p.Mob.get_active_held_item() as obj_item);
        Assert.Equal(path, held == null ? null : DmTypes.PathOf(held.GetType()));
        return held;
    }

    private void EmptyHands(Player p)
    {
        Do(() => { foreach (object held in Dm.each(p.Mob.held_items.Copy())) if (held is obj_item i) qdel(i); });
        Run(0.1f);
    }

    private static List<SoundEvent> Plays(Player p, string file, long from)
        => p.Heard.Where(h => h.Tick >= from && h.E.Kind == SoundKind.Play && h.E.File != null && h.E.File.StartsWith(file, StringComparison.Ordinal)).Select(h => h.E).ToList();

    private static AppearanceEntry Look(Player p, atom a)
    {
        ReplicaEntity seen = p.Replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
        return seen != null && p.Replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? look : null;
    }

    private static string StateOf(Player p, atom a) => Look(p, a) is { } look ? p.Replica.ResolveString(look.IconState) : null;

    private static List<string> OverlaysOf(Player p, atom a) => Look(p, a) is { } look
        ? look.Overlays.Select(o => p.Replica.TryGetAppearance(o, out AppearanceEntry e) ? p.Replica.ResolveString(e.IconState) : null).Where(s => s != null).Distinct().ToList()
        : new();

    private string ApcSeen(Player p, atom apc) => $"{StateOf(p, apc)} [{string.Join(" ", OverlaysOf(p, apc))}]";

    private float Fired() => In(() => SSmachines.times_fired);

    // Runs tick by tick until done (checked each tick), at most maxCycles SSmachines cycles from `since` (the cut, the lay);
    // returns the cycles since then.
    private float Cycles(Func<bool> done, float since, float maxCycles, string what)
    {
        while (!done() && Fired() - since < maxCycles)
            _w.Run(1);
        float took = Fired() - since;
        Assert.True(done(), $"{what}: not within {maxCycles} SSmachines cycles");
        _output.WriteLine($"{what}: {took} SSmachines cycles (tick {_w.Ticks})");
        return took;
    }

    private static double YawOf(int dx, int dy) => Math.Atan2(-dx, dy);

    // The engineer on the airlock's room side walks into it for 1.5 s; whether it opened while he pushed.
    private bool BumpOpens(Player p, Room room)
    {
        int dx = In(() => (int)room.Door.x - (int)room.DoorSide.x), dy = In(() => (int)room.Door.y - (int)room.DoorSide.y);
        Do(() => Waiting.NoSleep(p.Mob.forceMove(room.DoorSide)));
        Run(0.5f);
        bool opened = false;
        p.P.Yaw = YawOf(dx, dy);
        p.P.Keys = MoveKeys.Forward;
        for (int t = 0; t < 1.5 * Second; t++)
        {
            _w.Run(1);
            opened |= In(() => !Dm.truthy(room.Door.density));
        }
        p.P.Keys = MoveKeys.None;
        Run(0.5f);
        Do(() => Waiting.NoSleep(p.Mob.forceMove(room.DoorSide)));
        return opened;
    }

    [MetaServerFact]
    public void Cutting_an_apcs_feeder_darkens_its_room_and_unpowers_its_door_and_vents_and_laying_it_back_restores_them()
    {
        using (_w = new RealHeadlessWorld(new LoopbackOptions { Seed = Seed, TimeoutSteps = 60000 }, MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false))
        {
            Player engineer = Join("Engineer"), watcher = Join("Watcher");
            int bootRuntimes = _w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
            var runtimes = new List<string>();
            var sink = _w.World.RuntimeErrorSink;
            _w.World.RuntimeErrorSink = e => { runtimes.Add(e.ToString()); sink?.Invoke(e); };
            var windows = new TguiWindows
            {
                SendAct = (window, action, parameters) => engineer.P.Session.UiAct(window, action, parameters),
                SendClose = window => engineer.P.Session.UiClose(window),
            };

            Room room = In(PickRoom);
            Assert.NotNull(room);
            obj_machinery_power_apc apc = room.Apc;
            int x = In(() => (int)apc.x), y = In(() => (int)apc.y), z = In(() => (int)apc.z);
            turf ApcTurf() => locate(x, y, z); // a floor pried to plating is a new turf object here
            _output.WriteLine($"{In(() => Dm.str(room.Area.name))}: the APC at {x},{y} (cell {In(() => DmTypes.PathOf(apc.cell.GetType()))} {In(() => apc.cell.charge)} J), "
                + $"{room.Lights.Count} lit fixtures, the airlock at {In(() => room.Door.x)},{In(() => room.Door.y)}, the vent at {In(() => room.Vent.x)},{In(() => room.Vent.y)}, "
                + $"the scrubber at {In(() => room.Scrubber.x)},{In(() => room.Scrubber.y)}; the feeder's network {In(() => room.Cable.powernet.cables.len)} cables, {In(() => room.Cable.powernet.avail)} W");

            // The kit (set up, as the job's outfit would be): insulated gloves; his assistant's ID gives way to the Captain's spare, given into his hand.
            Do(() => qdel(engineer.Mob.wear_id));
            Do(() => Assert.True(Dm.truthy(Waiting.NoSleep(engineer.Mob.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_gloves_color_yellow>(engineer.Mob.loc), ITEM_SLOT_GLOVES)))));
            Do(() => Waiting.NoSleep(engineer.Mob.forceMove(ApcTurf())));
            Do(() => Waiting.NoSleep(watcher.Mob.forceMove(room.WatchSpot)));
            Run(6);
            _output.WriteLine($"after 6 s: the APC main_status {In(() => apc.main_status)}, charging {In(() => apc.charging)}, the grid avail {In(() => ((datum_powernet)apc.terminal.powernet).avail)} W, load {In(() => ((datum_powernet)apc.terminal.powernet).load)} W, SMESes on it: " + In(() => string.Join(", ", Dm.each_cast<atom>(((datum_powernet)apc.terminal.powernet).nodes).Select(n => DmTypes.PathOf(n.GetType())).GroupBy(s => s).Select(g => g.Key + " " + g.Count()))));
            Assert.True(In(() => apc.main_status) != APC_NO_POWER, "the grid powers the APC at round start");
            Do(() =>
            {
                Assert.Equal((3f, 3f, 3f), (Dm.num(apc.equipment), apc.lighting, apc.environ));
                Assert.True(Dm.truthy(room.Area.power_light) && Dm.truthy(room.Area.power_equip) && Dm.truthy(room.Area.power_environ));
            });
            float lumLit = In(() => Dm.num(room.WatchSpot.get_lumcount()));
            Dictionary<long, LightState> litSeen = room.Lights.Where(l => watcher.Replica.TryGet(l.dm_id, out _))
                .ToDictionary(l => l.dm_id, l => { watcher.Replica.TryGet(l.dm_id, out ReplicaEntity e); return e.Light; });
            _output.WriteLine($"lit: the watcher's tile lumcount {lumLit:0.00}; the watcher sees {litSeen.Count} fixtures: {string.Join(" ", litSeen.Values.Select(l => $"{l.Range:0.#}/{l.Power:0.##}/{l.Rgba:x8}"))}; the APC {ApcSeen(engineer, apc)}");
            Assert.NotEmpty(litSeen);
            Assert.All(litSeen.Values, l => Assert.True(l.On));
            Assert.Equal("apc0", StateOf(engineer, apc));
            Assert.Contains("apcox-1", OverlaysOf(engineer, apc));
            Assert.Contains("apco1-3", OverlaysOf(engineer, apc));

            // 1. The spare swiped on the APC (item_interaction's GetID, togglelock), then worn (E).
            var spare = Give(engineer, "/obj/item/card/id/advanced/gold/captains_spare");
            Click(engineer, apc);
            Assert.Equal(0f, In(() => apc.locked));
            Assert.Contains("apcox-0", OverlaysOf(engineer, apc));
            Key(engineer, KeybindingCode.QuickEquip);
            Assert.Same(spare, In(() => engineer.Mob.wear_id));

            // 2. The cover lock disengaged in tg's Apc window, the cover pried open with a crowbar (crowbar_act, apc_tool_act.dm:251).
            Click(engineer, apc);
            UiWindowState window = Assert.Single(engineer.Replica.UiWindows.Tgui());
            Assert.Equal("Apc", window.Interface);
            windows.Sync(engineer.Replica.UiWindows, 1280, 720, 1);
            windows.Get(window.Window).Content.Walk().First(n => n.Kind == UiKind.Button && n.Key == "cover").OnClick();
            Run(1);
            Assert.False(In(() => Dm.truthy(apc.coverlocked)), "the cover lock is disengaged");
            Assert.True(engineer.P.Session.UiClose(window.Window));
            Run(0.5f);
            var crowbar = Give(engineer, "/obj/item/crowbar");
            Click(engineer, apc);
            Assert.Equal(APC_COVER_OPENED, In(() => apc.opened));
            Assert.Equal("apc2", StateOf(engineer, apc)); // opened, a cell in (apc_appearance.dm:28)
            Assert.Empty(OverlaysOf(engineer, apc));

            // 3. The battery taken out by hand (attack_hand, apc_attack.dm:118): no cell, every channel off at the next cycle.
            Key(engineer, KeybindingCode.SwapHands);
            var battery = In(() => apc.cell);
            Click(engineer, apc, 0.1f);
            Assert.Same(battery, In(() => engineer.Mob.get_active_held_item()));
            Assert.Null(In(() => apc.cell));
            float noCell = Cycles(() => In(() => !Dm.truthy(room.Area.power_light) && !Dm.truthy(room.Area.power_environ)), Fired(), 3, "the battery out: the room's channels off");
            Run(0.5f);
            Assert.Equal("apc1", StateOf(engineer, apc));
            Assert.True(In(() => room.Lights.All(l => !Dm.truthy(l.on))), "the fixtures are off without the APC's cell");

            // 4. A standard 10 kJ cell in its place (cell_act): the room on it again; the battery dropped, the cover closed.
            Key(engineer, KeybindingCode.DropItem);
            var cell = (obj_item_stock_parts_power_store)Give(engineer, "/obj/item/stock_parts/power_store/cell");
            Click(engineer, apc, 0.1f);
            Assert.Same(cell, In(() => apc.cell));
            float back = Cycles(() => In(() => Dm.truthy(room.Area.power_light) && Dm.truthy(room.Area.power_environ) && Dm.truthy(room.Area.power_equip)), Fired(), 3, "the 10 kJ cell in: the room's channels on");
            Run(0.5f);
            Assert.Equal("apc2", StateOf(engineer, apc));
            Key(engineer, KeybindingCode.SwapHands);
            Assert.Same(crowbar, In(() => engineer.Mob.get_active_held_item()));
            Click(engineer, apc);
            Assert.Equal(APC_COVER_CLOSED, In(() => apc.opened));
            Assert.Equal("apc0", StateOf(engineer, apc));
            Assert.Contains("apco1-3", OverlaysOf(engineer, apc));
            Run(4);
            Assert.All(room.Lights, l => Assert.True(In(() => Dm.truthy(l.on))));

            // 5. The feeder cut: the floor tile pried off if the cable is under one, the wirecutters on the cable.
            if (In(() => ApcTurf().underfloor_accessibility < UNDERFLOOR_INTERACTABLE))
            {
                Assert.True(engineer.P.Session.Click(new ClickTarget(TargetKind.Turf, 0, x, y, z), ClickButton.Left, ClickModifiers.None));
                Run(1);
            }
            Assert.True(In(() => ApcTurf().underfloor_accessibility >= UNDERFLOOR_INTERACTABLE), "the cable is exposed");
            EmptyHands(engineer);
            var cutters = Give(engineer, "/obj/item/wirecutters");
            float cellAtCut = In(() => cell.charge);
            Click(engineer, room.Cable, 0.1f);
            Assert.True(In(() => QDELETED(room.Cable)), "the feeder is cut");
            Assert.Null(In(() => apc.terminal.powernet));
            float cut = Fired();
            float toNoGrid = Cycles(() => In(() => apc.main_status) == APC_NO_POWER, cut, 2, "the cut: the APC reads no grid");
            float toDark = Cycles(() => In(() => !Dm.truthy(room.Area.power_light)), cut, 30, "the cut: the lights off on the draining cell");
            float toNoEnviron = Cycles(() => In(() => !Dm.truthy(room.Area.power_environ)), cut, 45, "the cut: the environment off at an empty cell");
            Run(4); // the icon follows at the next cycle's late_process (icon_update_needed, apc_main.dm:595)
            _output.WriteLine($"the 10 kJ cell had {cellAtCut:0} J at the cut; now {In(() => cell.charge):0} J; the APC {ApcSeen(engineer, apc)}");
            Do(() =>
            {
                Assert.Equal((1f, 1f, 1f), (Dm.num(apc.equipment), apc.lighting, apc.environ)); // APC_CHANNEL_AUTO_OFF (autoset, apc_power_proc.dm:94)
                Assert.False(Dm.truthy(room.Area.power_light) || Dm.truthy(room.Area.power_equip) || Dm.truthy(room.Area.power_environ));
                Assert.All(room.Lights, l => Assert.False(Dm.truthy(l.on)));
                Assert.All(room.Lights, l => Assert.True(Dm.truthy(l.low_power_mode), "the fixture's emergency light"));
                Assert.True(Dm.truthy(Dm.bitand(room.Door.machine_stat, NOPOWER)), "the airlock is unpowered");
                Assert.True(Dm.truthy(Dm.bitand(room.Vent.machine_stat, NOPOWER)), "the vent is unpowered");
                Assert.True(Dm.truthy(Dm.bitand(room.Scrubber.machine_stat, NOPOWER)), "the scrubber is unpowered");
            });
            Assert.Contains("apco1-1", OverlaysOf(engineer, apc));
            Assert.Contains("apco2-1", OverlaysOf(engineer, apc));
            Assert.Contains("apco3-0", OverlaysOf(engineer, apc)); // not charging
            // the watcher's client: the fixtures' lights changed to the emergency light (bulb_low_power_colour), the tile darker
            uint emergency = CellState.ParseColor(COLOR_VIVID_RED);
            float lumDark = In(() => Dm.num(room.WatchSpot.get_lumcount()));
            var darkSeen = litSeen.Keys.ToDictionary(id => id, id => { watcher.Replica.TryGet(id, out ReplicaEntity e); return e.Light; });
            _output.WriteLine($"dark: the watcher's tile lumcount {lumDark:0.00} (lit {lumLit:0.00}); the watcher sees {string.Join(" ", darkSeen.Values.Select(l => $"{l.On}:{l.Range:0.#}/{l.Power:0.##}/{l.Rgba:x8}"))}");
            Assert.All(darkSeen.Values, l => Assert.True(!l.On || l.Rgba == emergency, "the fixture's normal light is off in the client"));
            Assert.All(darkSeen, kv => Assert.True(!kv.Value.On || kv.Value.Range < litSeen[kv.Key].Range || kv.Value.Power < litSeen[kv.Key].Power));
            Assert.True(lumDark < lumLit, "the watcher's tile is darker");
            Assert.Equal("vent_off", StateOf(watcher, room.Vent));
            Assert.Equal("scrub_off", StateOf(watcher, room.Scrubber));

            // 6. The airlock does not open for him bumping it, unpowered (airlock open(), hasPower).
            EmptyHands(engineer);
            long from = _w.Ticks;
            Assert.False(BumpOpens(engineer, room), "the unpowered airlock stays shut");
            Assert.Empty(Plays(engineer, AirlockOpen, from));

            // 7. The cable laid back with the piece the cut left (cable_coil place_turf): the grid charges the cell.
            Do(() => Waiting.NoSleep(engineer.Mob.forceMove(ApcTurf())));
            Run(0.5f);
            obj_item_stack_cable_coil piece = In(() => Dm.locate<obj_item_stack_cable_coil>(ApcTurf()));
            Assert.NotNull(piece);
            Click(engineer, piece, 0.5f);
            Assert.Same(piece, In(() => engineer.Mob.get_active_held_item()));
            Assert.True(engineer.P.Session.Click(new ClickTarget(TargetKind.Turf, 0, x, y, z), ClickButton.Left, ClickModifiers.None));
            _w.Run(2);
            obj_structure_cable laid = In(() => Dm.locate<obj_structure_cable>(ApcTurf()));
            Assert.NotNull(laid);
            Assert.Same(In(() => laid.powernet), In(() => apc.terminal.powernet));
            float lay = Fired();
            float toGrid = Cycles(() => In(() => apc.main_status) != APC_NO_POWER, lay, 2, "laid back: the APC reads the grid");
            float toEnviron = Cycles(() => In(() => Dm.truthy(room.Area.power_environ) && !Dm.truthy(Dm.bitand(room.Door.machine_stat, NOPOWER))), lay, 3, "laid back: the environment on (door, vents)");
            Run(0.5f);
            Assert.False(In(() => Dm.truthy(Dm.bitand(room.Vent.machine_stat, NOPOWER)) || Dm.truthy(Dm.bitand(room.Scrubber.machine_stat, NOPOWER))), "the vents run again");
            Assert.NotEqual("vent_off", StateOf(watcher, room.Vent));
            Assert.NotEqual("scrub_off", StateOf(watcher, room.Scrubber));
            EmptyHands(engineer);
            from = _w.Ticks;
            Assert.True(BumpOpens(engineer, room), "the powered airlock opens for him");
            Assert.NotEmpty(Plays(watcher, AirlockOpen, from).Concat(Plays(engineer, AirlockOpen, from)));
            float toLit = Cycles(() => In(() => Dm.truthy(room.Area.power_light)), lay, 30, "laid back: the lights on at 15 % of the cell");
            float toEquip = Cycles(() => In(() => Dm.truthy(room.Area.power_equip)), lay, 40, "laid back: the equipment on at 30 % of the cell");
            Run(2);
            Assert.All(room.Lights, l => Assert.True(In(() => Dm.truthy(l.on))));
            var relit = litSeen.Keys.ToDictionary(id => id, id => { watcher.Replica.TryGet(id, out ReplicaEntity e); return e.Light; });
            float lumBack = In(() => Dm.num(room.WatchSpot.get_lumcount()));
            _output.WriteLine($"restored: the cell at {In(() => Dm.num(cell.percent())):0.#} %; the watcher's tile lumcount {lumBack:0.00}; the watcher sees {string.Join(" ", relit.Values.Select(l => $"{l.On}:{l.Range:0.#}/{l.Power:0.##}/{l.Rgba:x8}"))}; the APC {ApcSeen(engineer, apc)}");
            Assert.All(relit, kv => Assert.Equal(litSeen[kv.Key], kv.Value));
            Assert.True(lumBack > lumDark, "the watcher's tile is lit again");
            Assert.Contains("apco1-3", OverlaysOf(engineer, apc));
            Assert.Contains("apco2-3", OverlaysOf(engineer, apc));

            // 8. Locked again: the spare taken off its slot by a click on it in the HUD, swiped, worn again (E).
            Do(() => Waiting.NoSleep(engineer.Mob.forceMove(ApcTurf())));
            Run(0.5f);
            Assert.True(engineer.P.Session.Click(new ClickTarget(TargetKind.Screen, spare.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
            Run(0.5f);
            Assert.Same(spare, In(() => engineer.Mob.get_active_held_item()));
            Click(engineer, apc);
            Key(engineer, KeybindingCode.QuickEquip);
            Assert.Same(spare, In(() => engineer.Mob.wear_id));
            Assert.Equal(1f, In(() => apc.locked));
            Assert.Contains("apcox-1", OverlaysOf(engineer, apc));

            float watts = (cellAtCut - 0.15f * In(() => cell.maxcharge)) / (toDark * In(() => Dm.num(SSmachines.wait)) / 10);
            _output.WriteLine($"SSmachines cycles: the battery out to dark {noCell}, the 10 kJ cell in to lit {back}; from the cut: no grid {toNoGrid}, the lights off {toDark}, the environment off {toNoEnviron}; "
                + $"from the lay: the grid {toGrid}, the environment on {toEnviron}, the lights on {toLit}, the equipment on {toEquip}. The room drew about {watts:0} W from the cell: "
                + $"its {In(() => battery.charge) / 1e6f:0.##} MJ battery would have kept it lit about {(In(() => battery.charge) - 0.15f * In(() => battery.maxcharge)) / watts / 3600:0.#} h after a cut");
            foreach (string r in runtimes)
                _output.WriteLine("RUNTIME " + r[..Math.Min(r.Length, 1500)]);
            // An ID moved from the hand to the ID slot (E) goes through obj/item/doMove (items.dm:1319, translated in M8 slice C: a
            // held item force-moved leaves its hand through transferItemToLoc), so its dropped() unregisters COMSIG_MOB_RETRIEVE_ACCESS
            // before equipped() registers it again (cards_ids.dm:190): no signals.dm:41 stack_trace any more (it was two before).
            int tgOwn = runtimes.Count(r => r.Contains("retrieve_access overridden", StringComparison.Ordinal));
            Assert.Equal(0, tgOwn);
            Assert.Equal(bootRuntimes + tgOwn, _w.World.RuntimeErrorCount);
        }
    }
}
