using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The Outpost (maps/map_files/Outpost with its own departure shuttle, maps/shuttles/emergency/emergency_outpost), a small station of four levels
/// made wholly in the map editor, through the real server with the lobby: a round on it lives on its own. Its crew start at their posts; its
/// generators feed the SMES and every room's APC, lamps and machines, with power to spare; the gravity generator holds every level; no room
/// leaks into space, and the canisters of atmos refill a room through the pipes; the suit storage unit beside the pad's airlock dresses the
/// engineer for the way out to the generators; the lift and the ladder join engineering and atmos; a late joiner arrives in the arrivals
/// shuttle at its dock, and the shuttle called at the communications console takes the crew from its dock to CentCom.
/// (Slow, a whole round each: they run with the MetaStation tests, Category=Meta.)
/// </summary>
[Trait("Category", "Meta")]
public class OutpostRoundTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const int GameStatePregame = 1, GameStatePlaying = 3;
    private const int Width = 48, Height = 48, Levels = 4;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static mob_living_carbon_human MobOf(RealHeadlessWorld w, RealHeadlessWorld.Player p)
        => w.World.Objects.TryGet(w.Host.AvatarOf(p.Name), out object o) ? o as mob_living_carbon_human : null;

    private static (int X, int Y, int Z) Tile(RealHeadlessWorld w, atom a) => In(w, () => ((int)Dm.num(a.x), (int)Dm.num(a.y), (int)Dm.num(a.z)));

    private static TguiWindow Window(TguiWindows windows, ReplicaWorld replica, string @interface)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Open.FirstOrDefault(x => x.Interface == @interface);
    }

    private static TguiWindows WindowsOf(RealHeadlessWorld.Player p) => new()
    {
        SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
        SendClose = window => p.Session.UiClose(window),
    };

    private static bool Click(RealHeadlessWorld w, RealHeadlessWorld.Player p, atom target, float seconds = 0.5f, ClickButton button = ClickButton.Left)
    {
        bool sent = p.Session.Click(new ClickTarget(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 16), button, ClickModifiers.None);
        w.Run((int)(seconds * Second));
        return sent;
    }

    private static void ClickScreen(RealHeadlessWorld.Player p, long id, ClickModifiers modifiers = ClickModifiers.None)
        => Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, id, 0, 0, 0, 16, 16), ClickButton.Left, modifiers));

    // The player picks the slice of that name from the radial shown to them (tg's suit storage unit asks by a radial).
    private static void Pick(RealHeadlessWorld w, RealHeadlessWorld.Player p, string name)
    {
        RadialMenuMessage menu = p.Session.Replica.TopRadial;
        Assert.True(menu != null, $"no radial is shown to pick '{name}' from");
        int slice = menu.Elements.FindIndex(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        Assert.True(slice >= 0, $"'{name}' is not among {string.Join(", ", menu.Elements.Select(e => e.Name))}");
        Assert.True(p.Session.RadialPick(menu.MenuId, slice + 1));
        w.Run(Second / 2);
    }

    // The player at the suit storage unit: off with what they wear on the head, the unit opened by its radial, and out of it the EVA suit, its
    // helmet and the breath mask, each put on with E; the oxygen tank's valve is opened with an alt-click in the hand and a click on the suit's
    // storage slot hangs the tank on the suit; the suit's thermal regulator is switched on; the unit is shut again. (SpaceSuitGameplayTests has
    // the same clicks with items from the dev console.)
    private static void SuitUp(RealHeadlessWorld w, RealHeadlessWorld.Player p, mob_living_carbon_human me, obj_machinery_suit_storage_unit unit)
    {
        ReplicaWorld replica = p.Session.Replica;
        var hat = In(w, () => me.head);
        if (hat != null)
        {
            if (!replica.Screen.Values.Any(e => e.Id == hat.dm_id)) // the head's slot is in the part of the inventory that folds
            {
                ClickScreen(p, In(w, () => ((atom)me.hud_used.screen_objects[HUD_HUMAN_TOGGLE_INVENTORY]).dm_id));
                w.Run(Second);
            }
            ClickScreen(p, hat.dm_id);
            w.Run(Second);
            Assert.Same(hat, In(w, () => me.get_active_held_item()));
            p.Session.Keybinding(KeybindingCode.DropItem);
            w.Run(Second);
            Assert.Null(In(w, () => me.head));
        }
        Click(w, p, unit);
        Pick(w, p, "open");
        obj_item Take(string slot)
        {
            Pick(w, p, slot);
            var held = In(w, () => me.get_active_held_item()) as obj_item;
            Assert.True(held != null, $"the unit gives its {slot} into the hand");
            return held;
        }
        void PutOn()
        {
            p.Session.Keybinding(KeybindingCode.QuickEquip);
            w.Run(Second);
        }
        obj_item suit = Take("suit");
        PutOn();
        Assert.IsType<obj_item_clothing_suit_space_eva>(suit);
        Assert.Same(suit, In(w, () => me.wear_suit));
        obj_item helmet = Take("helmet");
        PutOn();
        Assert.Same(helmet, In(w, () => me.head));
        obj_item mask = Take("mask");
        PutOn();
        Assert.Same(mask, In(w, () => me.wear_mask));
        obj_item tank = Take("storage");
        Assert.IsType<obj_item_tank_internals_oxygen>(tank);
        ClickScreen(p, tank.dm_id, ClickModifiers.Alt);
        w.Run(Second);
        Assert.Same(tank, In(w, () => me.@internal));
        ClickScreen(p, In(w, () => ((atom)me.hud_used.screen_objects[Dm.text("item_slot:[]", ITEM_SLOT_SUITSTORE)]).dm_id));
        w.Run(Second);
        Assert.Same(tank, In(w, () => me.s_store));
        // tg's rule for a space suit: its thermal regulator is off until the wearer switches it on at the HUD's suit display, and without it
        // the suit does not keep out the cold of space
        var display = In(w, () => me.hud_used.screen_objects[HUD_MOB_SPACESUIT] as atom_movable_screen_spacesuit);
        Assert.NotNull(display);
        ClickScreen(p, display.dm_id);
        w.Run(3 * Second);
        Assert.Equal(1f, In(w, () => ((obj_item_clothing_suit_space)suit).thermal_on));
        Pick(w, p, "close");
        Assert.True(In(w, () => !Dm.truthy(unit.state_open) && Dm.truthy(unit.density)), "the unit is shut again");
    }

    // How full the SMES are, in per cent of their capacity.
    private static string Held(RealHeadlessWorld w, List<obj_machinery_power_smes> smes)
        => In(w, () => string.Join(" and ", smes.Select(s => $"{100 * Dm.num(s.total_charge()) / s.total_capacity:0.00} %")));

    private static void RunUntil(RealHeadlessWorld w, Func<bool> until, int maxTicks, string what)
    {
        for (int i = 0; i < maxTicks && !In(w, until); i++)
            w.Run(1);
        Assert.True(In(w, until), what);
    }

    // The round on the Outpost with the lobby: the named players take the given jobs and the round starts.
    private RealHeadlessWorld Round(ulong seed, (string Name, string Job)[] crew, out RealHeadlessWorld.Player[] players)
    {
        var config = new ServerConfig
        {
            Seed = 7, Console = false, StatusEveryTicks = 100000, Map = "outpost", MapsFolder = MultizDebugBreachTests.RepoMaps(), MoveSpeedScale = 1f,
            Lobby = true, OpenJobs = true, NoInterest = true,
        };
        var w = new RealHeadlessWorld(new LoopbackOptions { Seed = seed, TimeoutSteps = 200000 }, config, strict: false);
        for (int i = 0; i < 10 * Second && In(w, () => SSticker.current_state) < GameStatePregame; i++)
            w.Run(1);
        Assert.Equal(0, w.World.RuntimeErrorCount); // the map and its shuttles load with no runtime error
        RealHeadlessWorld.Player[] joined = crew.Select(c => w.Join(c.Name)).ToArray();
        w.Run(Second / 2);
        In(w, () =>
        {
            foreach ((string name, string job) in crew)
                ((client)GLOB.directory[Dm.ckey(name)]).prefs.job_preferences[job] = JP_HIGH;
            return true;
        });
        foreach (RealHeadlessWorld.Player p in joined)
            Window(WindowsOf(p), p.Session.Replica, Limits.LobbyUiInterface).Context.Act("ready_toggle");
        w.Run(Second / 2);
        joined[0].Session.DevCommand("startnow");
        for (int i = 0; i < 30 * Second && In(w, () => SSticker.current_state) != GameStatePlaying; i++)
            w.Run(1);
        w.Run(3 * Second);
        Assert.Equal(GameStatePlaying, In(w, () => SSticker.current_state));
        for (int i = 0; i < crew.Length; i++)
            Assert.Equal(crew[i].Job, In(w, () => Dm.str(MobOf(w, joined[i]).mind.assigned_role.title)));
        players = joined;
        return w;
    }

    // Steers the player to (x, y) from its own predicted position, as a player would (PullGameplayTests); returns the ticks it took.
    private static int WalkTo(RealHeadlessWorld w, RealHeadlessWorld.Player p, double x, double y, int maxTicks = 8 * Second)
    {
        int ticks = 0;
        while (ticks < maxTicks)
        {
            MotionState predicted = p.Session.Prediction.Predicted;
            double dx = x - predicted.X / 256.0, dy = y - predicted.Y / 256.0;
            if (Math.Sqrt(dx * dx + dy * dy) < 0.15)
                break;
            p.Keys = MoveKeys.Forward;
            p.Yaw = Math.Atan2(-dx, dy); // Godot's yaw: 0 faces north, +90 degrees west
            w.Run(1);
            ticks++;
        }
        p.Keys = MoveKeys.None;
        w.Run(6);
        return ticks;
    }

    // The corners of the shortest way over the floor from a mob to a tile of its level: over open floor with nothing dense on it, through airlocks.
    private static List<(int X, int Y)> Way(RealHeadlessWorld w, atom from, int tx, int ty)
    {
        using (w.World.Enter())
        {
            int z = (int)Dm.num(from.z), sx = (int)Dm.num(from.x), sy = (int)Dm.num(from.y);
            bool Open(int x, int y)
            {
                if (x < 1 || y < 1 || x > Width || y > Height || locate(x, y, z) is not turf_open t || t is turf_open_space or turf_open_openspace)
                    return false;
                foreach (object thing in Dm.each(t.contents))
                {
                    if (thing is obj o && Dm.truthy(o.density) && o is not obj_machinery_door_airlock)
                        return false;
                }
                return true;
            }
            var came = new Dictionary<(int, int), (int, int)> { [(sx, sy)] = (sx, sy) };
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue((sx, sy));
            while (queue.Count > 0 && !came.ContainsKey((tx, ty)))
            {
                (int x, int y) = queue.Dequeue();
                foreach ((int nx, int ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                {
                    if (came.ContainsKey((nx, ny)) || !Open(nx, ny))
                        continue;
                    came[(nx, ny)] = (x, y);
                    queue.Enqueue((nx, ny));
                }
            }
            if (!came.ContainsKey((tx, ty)))
                return null;
            var tiles = new List<(int X, int Y)>();
            for ((int, int) at = (tx, ty); at != (sx, sy); at = came[at])
                tiles.Add(at);
            tiles.Reverse();
            // the corners alone: where the way turns, and its end
            var corners = new List<(int X, int Y)>();
            (int X, int Y) before = (sx, sy);
            for (int i = 0; i < tiles.Count; i++)
            {
                if (i == tiles.Count - 1 || (tiles[i + 1].X - tiles[i].X, tiles[i + 1].Y - tiles[i].Y) != (tiles[i].X - before.X, tiles[i].Y - before.Y))
                    corners.Add(tiles[i]);
                before = tiles[i];
            }
            return corners;
        }
    }

    // The player walks to a tile of its level by the keys, opening the airlocks on the way by walking into them.
    private static void Walk(RealHeadlessWorld w, RealHeadlessWorld.Player p, int tx, int ty)
    {
        mob me = MobOf(w, p);
        List<(int X, int Y)> way = Way(w, me, tx, ty);
        Assert.True(way != null, $"{p.Name} at {Tile(w, me)} has no way to {tx}, {ty}");
        foreach ((int x, int y) in way)
            WalkTo(w, p, x, y);
        (int ax, int ay, int _) = Tile(w, me);
        Assert.True((ax, ay) == (tx, ty), $"{p.Name} walked to {ax}, {ay}, not to {tx}, {ty}");
    }

    private static turf_open Floor(int x, int y, int z) => locate(x, y, z) as turf_open;

    // What a powernet's cycle made or used, in kilowatts.
    private static double Kilowatts(float energy) => (double)Dm.num(energy_to_power(energy)) / 1000;

    private static double Pressure(RealHeadlessWorld w, int x, int y, int z) => In(w, () => (double)Dm.num(Floor(x, y, z).air.return_pressure()));

    // Every open turf of the station's areas, on its four levels.
    private static IEnumerable<turf_open> StationFloors()
    {
        for (int z = 1; z <= Levels; z++)
            for (int y = 1; y <= Height; y++)
                for (int x = 1; x <= Width; x++)
                    if (locate(x, y, z) is turf_open t && t is not turf_open_space && get_area(t) is area_station && t.air != null)
                        yield return t;
    }

    private static double StationAir(RealHeadlessWorld w) => In(w, () => StationFloors().Sum(t => (double)Dm.num(t.air.total_moles())));

    // The station's turfs whose air meets space: a leak.
    private static List<string> Leaks(RealHeadlessWorld w) => In(w, () =>
    {
        var leaks = new List<string>();
        foreach (turf_open t in StationFloors())
        {
            if (t.atmos_adjacent_turfs == null)
                continue;
            foreach (object other in Dm.each(t.atmos_adjacent_turfs))
            {
                if (other is turf_open_space space)
                    leaks.Add($"{t.x},{t.y},{t.z} -> {space.x},{space.y},{space.z}");
            }
        }
        return leaks;
    });

    [Fact]
    public void The_Outpost_has_its_crew_at_their_posts_and_power_light_gravity_and_air_that_stays_in()
    {
        using RealHeadlessWorld w = Round(91, new[] { ("Alice", JOB_CHIEF_ENGINEER), ("Bob", JOB_STATION_ENGINEER), ("Carol", JOB_ATMOSPHERIC_TECHNICIAN), ("Dave", JOB_ASSISTANT) },
            out RealHeadlessWorld.Player[] players);
        RealHeadlessWorld.Player alice = players[0], bob = players[1], carol = players[2], dave = players[3];
        mob_living_carbon_human aliceMob = MobOf(w, alice), bobMob = MobOf(w, bob), carolMob = MobOf(w, carol), daveMob = MobOf(w, dave);

        // ---- the crew start at their posts: the start landmarks of the map
        Assert.Equal((12, 32, 1), Tile(w, aliceMob));
        Assert.Contains(Tile(w, bobMob), new[] { (15, 31, 1), (15, 32, 1), (16, 31, 1) });
        Assert.Contains(Tile(w, carolMob), new[] { (14, 30, 3), (15, 30, 3) });
        Assert.Contains(Tile(w, daveMob), new[] { (15, 18, 1), (16, 18, 1), (15, 17, 1) });
        Assert.IsType<area_station_engineering_atmos>(In(w, () => get_area(carolMob)));
        Assert.IsType<area_station_hallway_secondary_entry>(In(w, () => get_area(daveMob)));

        // ---- gravity: the generator is charged and on, and holds the lowest level and atmos two levels above it
        obj_machinery_gravity_generator_main gravity = In(w, () => Dm.each<obj_machinery_gravity_generator_main>(Dm.world).Single(g => g.z == 1));
        Assert.True(In(w, () => Dm.truthy(gravity.on) && Dm.num(gravity.charge_count) >= 100), "the gravity generator is on");
        Assert.True(In(w, () => Dm.truthy(aliceMob.has_gravity()) && Dm.truthy(carolMob.has_gravity())), "the crew on levels 1 and 3 have gravity");

        // ---- the air stays in: no station turf's air meets space, and a minute later the station holds what it held
        Assert.Empty(Leaks(w));
        double airAtStart = StationAir(w);
        foreach ((int x, int y, int z) in new[] { (15, 30, 1), (7, 31, 1), (15, 12, 1), (30, 30, 1), (15, 30, 3) })
            Assert.InRange(Pressure(w, x, y, z), 99, 104);
        w.Run(60 * Second);
        double airAfterAMinute = StationAir(w);
        output.WriteLine($"air: {airAtStart:0} mol at the start, {airAfterAMinute:0} mol a minute later");
        Assert.InRange(airAfterAMinute, airAtStart * 0.995, airAtStart * 1.005);

        // ---- power: the sixteen RTGs of the pad feed the SMES' terminals and make more than the station uses; both SMES feed the rooms; every
        // APC has outside power and a charged cell
        List<obj_machinery_power_rtg> rtgs = In(w, () => Dm.each<obj_machinery_power_rtg>(Dm.world).Where(r => r.z == 1).ToList());
        Assert.Equal(16, rtgs.Count);
        datum_powernet feed = In(w, () => rtgs[0].powernet as datum_powernet);
        Assert.NotNull(feed);
        List<obj_machinery_power_smes> smes = In(w, () => Dm.each<obj_machinery_power_smes>(Dm.world).Where(s => s.z == 1).ToList());
        Assert.Equal(2, smes.Count);
        In(w, () =>
        {
            Assert.All(rtgs, r => Assert.Same(feed, r.powernet));
            Assert.All(smes, s => Assert.Same(feed, s.terminal.powernet));
            Assert.All(smes, s => Assert.True(Dm.truthy(s.outputting) && Dm.num(s.total_charge()) > 0.9 * s.total_capacity, "an SMES is charged and gives power"));
            Assert.NotSame(feed, smes[0].powernet);
            Assert.Same(smes[0].powernet, smes[1].powernet);
            return true;
        });
        double made = In(w, () => Kilowatts(feed.avail));
        datum_powernet station = In(w, () => smes[0].powernet as datum_powernet);
        double used = In(w, () => Kilowatts(Dm.num(station.load)));
        double stored = In(w, () => smes.Sum(s => (double)Dm.num(s.total_charge()))), capacity = In(w, () => smes.Sum(s => (double)s.total_capacity));
        output.WriteLine($"power with the RTGs alone: {made:0.0} kW made, the station uses {used:0.0} kW; the SMES hold {Held(w, smes)}");
        Assert.True(made > 1.5 * used, $"the RTGs alone make power to spare ({made:0.0} kW made, {used:0.0} kW used)");
        List<obj_machinery_power_apc> apcs = In(w, () => Dm.each<obj_machinery_power_apc>(Dm.world).Where(a => get_area(a) is area_station).ToList());
        Assert.Equal(5, apcs.Count);
        In(w, () =>
        {
            foreach (obj_machinery_power_apc apc in apcs)
            {
                string where = get_area(apc).GetType().Name;
                Assert.True(Dm.truthy(apc.operating), where + ": the APC is on");
                Assert.True(apc.main_status == 2, where + $": the APC has outside power (main_status {apc.main_status})");
                Assert.True(apc.cell != null && Dm.num(apc.cell.charge) > 0, where + ": the APC's cell is charged");
                Assert.Same(station, apc.terminal.powernet);
            }
            return true;
        });

        // ---- light and machines: every lamp of the station is lit, its rooms are bright, and no machine of the station lacks power
        In(w, () =>
        {
            List<obj_machinery_light> lamps = Dm.each<obj_machinery_light>(Dm.world).Where(l => get_area(l) is area_station).ToList();
            Assert.Equal(24, lamps.Count);
            Assert.All(lamps, l => Assert.True(Dm.truthy(l.on), $"the lamp at {l.x},{l.y},{l.z} is lit"));
            foreach ((int x, int y, int z) in new[] { (15, 30, 1), (7, 31, 1), (15, 12, 1), (30, 30, 1), (15, 30, 3) })
                Assert.True(Dm.num(Floor(x, y, z).get_lumcount()) > 0.3, $"the floor at {x},{y},{z} is lit ({Floor(x, y, z).get_lumcount()})");
            List<string> dead = Dm.each<obj_machinery>(Dm.world).Where(m => get_area(m) is area_station && Dm.truthy(Dm.bitand(m.machine_stat, NOPOWER)))
                .Select(m => $"{m.GetType().Name} at {m.x},{m.y},{m.z}").ToList();
            Assert.Empty(dead);
            return true;
        });

        // ---- the air is supplied: the atmos technician takes the wrench from the table and joins the six canisters to their connectors
        // (tg's rule: a mapped canister stands loose on its connector); the pump then fills the supply pipes of every level
        obj_item_wrench wrench = In(w, () => Dm.each<obj_item_wrench>(Floor(12, 27, 3)).Single());
        Walk(w, carol, 13, 27);
        Click(w, carol, wrench);
        Assert.Same(wrench, In(w, () => carolMob.get_active_held_item()));
        List<obj_machinery_portable_atmospherics_canister> canisters = In(w, () => Enumerable.Range(13, 6)
            .Select(x => Dm.each<obj_machinery_portable_atmospherics_canister>(Floor(x, 23, 3)).Single()).ToList());
        Assert.All(canisters, c => Assert.Null(c.connected_port));
        double canned = In(w, () => canisters.Sum(c => (double)Dm.num(c.air_contents.total_moles())));
        for (int i = 0; i < canisters.Count; i++)
        {
            Walk(w, carol, 13 + i, 24);
            Click(w, carol, canisters[i]);
        }
        Assert.All(canisters, c => Assert.NotNull(c.connected_port));
        carol.Session.Keybinding(KeybindingCode.SwapHands); // to her empty hand: what she clicks next is pressed, not wrenched
        w.Run(2);
        obj_machinery_atmospherics_pipe supply = In(w, () => Dm.each<obj_machinery_atmospherics_pipe>(Floor(16, 12, 1)).Single(p => p.piping_layer == 4));
        RunUntil(w, () => Dm.num(supply.parent.air.return_pressure()) > 150, 60 * Second, "the supply pipe under the arrival corridor is filled from atmos");
        output.WriteLine($"supply: {In(w, () => Dm.num(supply.parent.air.return_pressure())):0} kPa in the pipe under the arrival corridor");

        // a room that lost most of its air is filled again through its vent: the gravity generator's room, emptied to a fifth
        In(w, () =>
        {
            foreach (turf_open t in StationFloors().Where(t => get_area(t) is area_station_engineering_gravity_generator))
            {
                t.air.remove_ratio(0.8);
                t.air_update_turf(0, 0);
            }
            return true;
        });
        Assert.InRange(Pressure(w, 7, 31, 1), 15, 25);
        long emptiedAt = w.Ticks;
        RunUntil(w, () => Dm.num(Floor(7, 31, 1).air.return_pressure()) > 95, 240 * Second, "the gravity generator's room is filled again");
        double cannedAfter = In(w, () => canisters.Sum(c => (double)Dm.num(c.air_contents.total_moles())));
        output.WriteLine($"refill: the room is back at {Pressure(w, 7, 31, 1):0} kPa after {(w.Ticks - emptiedAt) / (double)Second:0} s; the canisters hold {cannedAfter:0} of {canned:0} mol");
        Assert.True(cannedAfter < canned - 1000, "the air came from the canisters");
        Assert.Empty(Leaks(w));

        // ---- some minutes later, on the RTGs alone, the SMES hold no less than they held (a cycle's ripple apart): the station does not live
        // off its stored charge (the APCs' cells take the most in the round's first minute, and an SMES gets that back afterwards)
        double storedLater = In(w, () => smes.Sum(s => (double)Dm.num(s.total_charge())));
        output.WriteLine($"some minutes on the RTGs alone: the SMES hold {Held(w, smes)}");
        Assert.True(storedLater >= stored - 0.0005 * capacity, $"on the RTGs alone the SMES lose no charge ({100 * stored / capacity:0.00} % -> {100 * storedLater / capacity:0.00} %)");

        // ---- the P.A.C.M.A.N. on the pad: the engineer dresses at the suit storage unit beside the airlock, goes out by the airlock, whose
        // doors cycle, switches the generator on, stands twenty seconds in the vacuum of the pad and comes back unhurt; engineering keeps its
        // air, and the P.A.C.M.A.N. adds its ten kilowatts to the RTGs'
        obj_machinery_suit_storage_unit unit = In(w, () => Dm.each<obj_machinery_suit_storage_unit>(Floor(16, 36, 1)).Single());
        Walk(w, bob, 15, 36);
        SuitUp(w, bob, bobMob, unit);
        obj_machinery_power_port_gen_pacman pacman = In(w, () => Dm.each<obj_machinery_power_port_gen_pacman>(Dm.world).Single(g => g.z == 1));
        Assert.False(In(w, () => Dm.truthy(pacman.active)));
        Walk(w, bob, 15, 42);
        Click(w, bob, pacman);
        TguiWindow generator = Window(WindowsOf(bob), bob.Session.Replica, "PortableGenerator");
        Assert.NotNull(generator);
        generator.Context.Act("toggle_power");
        w.Run(Second / 2);
        Assert.True(In(w, () => Dm.truthy(pacman.active)), "the P.A.C.M.A.N. runs");
        w.Run(20 * Second);
        Assert.True(Pressure(w, 15, 42, 1) < 1, "the pad is in vacuum");
        Walk(w, bob, 15, 36);
        (double brute, double burn, double choked) = In(w, () => ((double)Dm.num(bobMob.get_brute_loss()), (double)Dm.num(bobMob.get_fire_loss()), (double)Dm.num(bobMob.get_oxy_loss())));
        output.WriteLine($"the engineer is back inside from the pad: brute {brute}, burn {burn}, oxygen loss {choked}, health {In(w, () => bobMob.health)}; engineering at {Pressure(w, 15, 30, 1):0} kPa");
        Assert.True(brute == 0 && burn == 0 && choked == 0, "the suit and its tank kept the engineer unhurt in the vacuum");
        Assert.InRange(Pressure(w, 15, 30, 1), 97, 104);
        w.Run(30 * Second);
        double madeWithPacman = In(w, () => Kilowatts(feed.avail));
        output.WriteLine($"power with the P.A.C.M.A.N.: {madeWithPacman:0.0} kW made, the station uses {In(w, () => Kilowatts(Dm.num(station.load))):0.0} kW; the SMES hold {Held(w, smes)}");
        Assert.True(madeWithPacman > made + 8, $"the P.A.C.M.A.N. adds its power ({made:0.0} -> {madeWithPacman:0.0} kW)");

        // ---- the lift: called to atmos by its button, it comes up and opens its doors; its panel takes the technician down to engineering
        obj_machinery_elevator_control_panel panel = In(w, () => Dm.each<obj_machinery_elevator_control_panel>(Dm.world).Single());
        obj_machinery_button_elevator button = In(w, () => Dm.each<obj_machinery_button_elevator>(Floor(17, 37, 3)).Single());
        Assert.Equal(1, Tile(w, panel).Z);
        Walk(w, carol, 17, 37);
        Click(w, carol, button);
        RunUntil(w, () => panel.z == 3 && !Dm.each<obj_machinery_door_poddoor>(Floor(18, 37, 3)).Any(d => Dm.truthy(d.density)), 30 * Second, "the lift comes up to atmos and opens");
        WalkTo(w, carol, 18, 37);
        WalkTo(w, carol, 19, 37);
        Assert.Equal((19, 37, 3), Tile(w, carolMob));
        Click(w, carol, panel);
        TguiWindow cabin = Window(WindowsOf(carol), carol.Session.Replica, "ElevatorPanel");
        Assert.NotNull(cabin);
        cabin.Context.Act("move_lift", ("z", 1));
        RunUntil(w, () => carolMob.z == 1 && !Dm.each<obj_machinery_door_poddoor>(Floor(18, 37, 1)).Any(d => Dm.truthy(d.density)), 30 * Second, "the lift takes the technician down to engineering");
        WalkTo(w, carol, 18, 37);
        WalkTo(w, carol, 16, 37);
        Assert.Equal((16, 37, 1), Tile(w, carolMob));

        // ---- the ladder beside it: the assistant walks in from the arrival corridor and climbs to the gallery and on to atmos
        Walk(w, dave, 17, 37);
        for (int level = 1; level <= 2; level++)
        {
            obj_structure_ladder ladder = In(w, () => Dm.each<obj_structure_ladder>(Floor(17, 37, level)).Single());
            Click(w, dave, ladder);
            RunUntil(w, () => daveMob.z == level + 1, 10 * Second, $"the assistant climbs from level {level}");
        }
        Assert.Equal((17, 37, 3), Tile(w, daveMob));
        Assert.IsType<area_station_engineering_atmos>(In(w, () => get_area(daveMob)));

        output.WriteLine(w.Err.ToString());
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void A_late_joiner_arrives_in_the_arrivals_shuttle_and_the_called_shuttle_takes_the_crew_from_its_dock_to_centcom()
    {
        using RealHeadlessWorld w = Round(92, new[] { ("Alice", JOB_CHIEF_ENGINEER), ("Bob", JOB_ASSISTANT) }, out RealHeadlessWorld.Player[] players);
        RealHeadlessWorld.Player alice = players[0], bob = players[1];
        mob_living_carbon_human aliceMob = MobOf(w, alice), bobMob = MobOf(w, bob);
        double airAtStart = StationAir(w);

        // ---- the arrivals shuttle stands at the station's dock, its two east doors at the corridor's airlocks
        In(w, () =>
        {
            var dock = Assert.IsAssignableFrom<obj_docking_port_stationary>(SSshuttle.arrivals.get_docked());
            Assert.Equal(("arrival_stationary", 10, 5, 1), (Dm.str(dock.shuttle_id), (int)Dm.num(dock.x), (int)Dm.num(dock.y), (int)Dm.num(dock.z)));
            foreach (int y in new[] { 7, 14 })
            {
                Assert.IsType<area_shuttle_arrival>(get_area(Floor(13, y, 1)));
                Assert.Single(Dm.each<obj_machinery_door_airlock>(Floor(13, y, 1)));
                Assert.Single(Dm.each<obj_machinery_door_airlock>(Floor(14, y, 1)));
            }
            return true;
        });

        // ---- Erin joins the round in progress: tg seats her, buckled, in the docked arrivals shuttle; she gets up and walks into the station
        RealHeadlessWorld.Player erin = w.Join("Erin");
        w.Run(Second / 2);
        TguiWindows lobby = WindowsOf(erin);
        Window(lobby, erin.Session.Replica, Limits.LobbyUiInterface).Context.Act("join", ("ctrlClick", false));
        w.Run(Second / 2);
        TguiWindow jobs = Window(lobby, erin.Session.Replica, "JobSelection");
        Assert.NotNull(jobs);
        jobs.Context.Act("select_job", ("job", JOB_ASSISTANT));
        w.Run(2 * Second);
        mob_living_carbon_human erinMob = MobOf(w, erin);
        Assert.NotNull(erinMob);
        Assert.IsType<area_shuttle_arrival>(In(w, () => get_area(erinMob)));
        var seat = Assert.IsAssignableFrom<obj_structure_chair>(In(w, () => erinMob.buckled));
        output.WriteLine($"Erin arrives in the seat at {Tile(w, seat)}");
        Click(w, erin, seat);
        Assert.Null(In(w, () => erinMob.buckled));
        Walk(w, erin, 15, 14);
        Assert.IsType<area_station_hallway_secondary_entry>(In(w, () => get_area(erinMob)));

        // ---- the chief engineer walks to the communications console at the end of the departure corridor, logs in with her ID and calls the shuttle
        obj_machinery_computer_communications console = In(w, () => Dm.each<obj_machinery_computer_communications>(Dm.world).Single(c => c.z == 1 && get_area(c) is area_station));
        using (w.World.Enter())
        {
            SSshuttle.emergency_call_time = 300;   // tg's ten minutes, three and two: 30, 90 and 10 seconds here
            SSshuttle.emergency_dock_time = 900;
            SSshuttle.emergency_escape_time = 100;
            // tg's shuttle_refuel_delay: no call in the round's first 20 minutes (canEvac); the round started 20 minutes ago
            SSticker.round_start_time = Dm.num(Dm.world.time) - 20 * 60 * 10;
            var home = Assert.IsAssignableFrom<obj_docking_port_stationary>(SSshuttle.getDock("emergency_home"));
            Assert.Equal((36, 32, 1), ((int)Dm.num(home.x), (int)Dm.num(home.y), (int)Dm.num(home.z)));
            Assert.NotSame(home, SSshuttle.emergency.get_docked()); // it waits at CentCom
            Assert.Equal("Outpost emergency shuttle", Dm.str(SSshuttle.emergency.name));
        }
        var texts = new List<string>();
        alice.Client.ServerTextReceived += t => texts.Add(t.Html);
        Walk(w, alice, 40, 30);
        TguiWindows windows = WindowsOf(alice);
        Click(w, alice, console);
        TguiWindow comms = Window(windows, alice.Session.Replica, "CommunicationsConsole");
        Assert.NotNull(comms);
        comms.Context.Act("toggleAuthentication");
        w.Run(Second / 2);
        comms = Window(windows, alice.Session.Replica, "CommunicationsConsole");
        Assert.True(comms.Context.Data.Truthy("authenticated"), "the chief engineer's ID logs in at the console");
        comms.Context.Act("callShuttle", ("reason", "The test of the Outpost is over and its crew go home."));
        w.Run(Second / 2);
        Assert.Equal(SHUTTLE_CALL, In(w, () => Dm.str(SSshuttle.emergency.mode)));
        Assert.Contains(texts, t => t.Contains("emergency shuttle has been called", StringComparison.OrdinalIgnoreCase));

        // ---- it docks at the station's own dock, its two doors at the corridor's airlocks, under a roof of its own; the crew walk aboard
        RunUntil(w, () => Dm.eq(SSshuttle.emergency.mode, SHUTTLE_DOCKED), 60 * Second, "the shuttle docks");
        In(w, () =>
        {
            Assert.Same(SSshuttle.getDock("emergency_home"), SSshuttle.emergency.get_docked());
            foreach (int x in new[] { 36, 37 })
            {
                Assert.IsAssignableFrom<area_shuttle_escape>(get_area(Floor(x, 32, 1)));
                Assert.Single(Dm.each<obj_machinery_door_airlock>(Floor(x, 32, 1)));
                Assert.Single(Dm.each<obj_machinery_door_airlock>(Floor(x, 31, 1)));
            }
            Assert.Equal(7, Dm.each<obj_structure_chair>(Dm.world).Count(c => c.z == 1 && get_area(c) is area_shuttle_escape));
            Assert.IsNotType<turf_open_space_openspace>(locate(36, 34, 2)); // tg roofs a docked shuttle on the level above
            return true;
        });
        Walk(w, alice, 36, 34);
        Walk(w, bob, 37, 34);
        Walk(w, erin, 38, 34);
        foreach (mob_living_carbon_human rider in new[] { aliceMob, bobMob, erinMob })
            Assert.IsAssignableFrom<area_shuttle_escape>(In(w, () => get_area(rider)));
        output.WriteLine($"aboard: the shuttle's air {Pressure(w, 36, 34, 1):0} kPa, the corridor's {Pressure(w, 36, 30, 1):0} kPa");
        Assert.InRange(Pressure(w, 36, 30, 1), 97, 104);

        // ---- it leaves with them, flies through transit and docks at CentCom: the round is over
        RunUntil(w, () => Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ESCAPE), 120 * Second, "the shuttle leaves");
        w.Run(Second);
        Assert.All(new[] { aliceMob, bobMob, erinMob }, rider => Assert.True(In(w, () => Dm.truthy(is_reserved_level(rider.z))), "the crew ride through transit"));
        Assert.Empty(Leaks(w)); // the dock's airlocks hold the corridor's air after the shuttle left
        double airAtEnd = StationAir(w);
        output.WriteLine($"air: {airAtStart:0} mol at the start, {airAtEnd:0} mol after the shuttle left");
        Assert.InRange(airAtEnd, airAtStart * 0.98, airAtStart * 1.02);
        RunUntil(w, () => Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ENDGAME), 30 * Second, "the shuttle docks at CentCom");
        In(w, () =>
        {
            Assert.True(Dm.truthy(SSticker.check_finished()), "the round's state is the endgame");
            foreach (mob_living_carbon_human rider in new[] { aliceMob, bobMob, erinMob })
            {
                Assert.IsAssignableFrom<area_shuttle_escape>(get_area(rider));
                Assert.True(Dm.truthy(is_centcom_level(rider.z)), "the crew are at CentCom");
                Assert.True(Dm.num(rider.stat) < SOFT_CRIT);
            }
            return true;
        });
        output.WriteLine(w.Err.ToString());
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}
