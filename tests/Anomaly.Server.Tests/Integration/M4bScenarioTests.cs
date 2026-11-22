using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// ROADMAP M4b's demo check through the real server on MetaStation, end to end, with two headless Godot clients
/// (<see cref="PlaySession"/>) over the loopback. Meta's Fitness Room (x 137..141, y 183..185) has a reinforced window to
/// space on its west wall (136,184), a public glass airlock with a firelock on its south wall (138,182) to the Recreation
/// Area, a fire alarm (141,183), an air vent and a scrubber. The engineer walks there (footsteps) and opens and closes the
/// airlock, heard with tg's falloff by himself and, with the station graph's occlusion, by a listener in the sealed
/// Recreational Holodeck east (150,180). The dev console removes the window: the room drains into space, the space wind pulls
/// a paper and the engineer toward the hole (tg's space_wind sound), the airflow reaches the client, the cold air closes the
/// firelock and its siren is heard quieter and muffled as the room empties; the engineer suffocates and takes the low
/// pressure's damage, which his HUD shows. The console patches the hole (the turf and the window the breach removed come back), the listener opens an air canister's valve (the dev command; since M5 slice 3 the canister's own tgui window does it, as the M5 check opens it) and
/// the room fills again. Then the listener holds the cold-alarmed firelock open with a crowbar (tg's try_to_crowbar), opens the
/// airlock, lights a welder and, the lighting of the room's and the Recreation Area's APCs off, throws it into plasma the console added to the room: the plasma fire starts (the client sees
/// the hotspots, the fire puff and the crackle loop). The listener has taken the fire extinguisher from the Recreation Area's
/// cabinet (the client sees the cabinet open and empty, the click and the slosh are heard); with its safety off he sprays the
/// burning tiles from the door (each spray's extinguish.ogg is heard, the client sees the water puffs) until the fire is out (or, a big
/// fire outlasting the 20 sprays, until it burns out);
/// the welder still lit on the floor relights the plasma left, so he walks in, picks it up, turns it off and sprays what it
/// relit until the fire stays out, and the crackle stops.
/// </summary>
public class M4bScenarioTests
{
    private const int Second = 20; // ticks per second

    // Meta's Fitness Room and its neighbours (z 1)
    private const int HoleX = 136, HoleY = 184; // the reinforced window to space
    private const int DoorX = 138, DoorY = 182; // the public glass airlock and its firelock
    private const int AlarmX = 141, AlarmY = 183; // the fire alarm
    private const int RoomX = 139, RoomY = 184; // the middle of the room (its scrubber)
    private const int HoloX = 150, HoloY = 180; // the listener's tile in the Recreational Holodeck, behind closed doors
    private const int CabinetX = 137, CabinetY = 177; // the Recreation Area's extinguisher cabinet (on the west wall)

    private const string Airlock = "sound/machines/airlock/airlock.ogg";
    private const string AirlockClose = "sound/machines/airlock/airlockclose.ogg";
    private const string Footstep = "sound/effects/footstep/";
    private const string SpaceWind = "sound/effects/space_wind.ogg";
    private const string Siren = "sound/machines/fire_alarm/fire_alarm";
    private const string Valve = "sound/effects/valve_opening.ogg";
    private const string WelderOn = "sound/items/tools/welderactivate.ogg";
    private const string WelderOff = "sound/items/tools/welderdeactivate.ogg";
    private const string FirePuff = "sound/effects/fire_puff.ogg";
    private const string Crackle = "sound/effects/fireclip";
    private const string SpraySound = "sound/effects/extinguish.ogg";
    private const string CabinetClick = "sound/machines/click.ogg";
    private const string Slosh = "sound/items/handling/reagent_containers/default/default_liquid_slosh";

    private const double North = 0, West = Math.PI / 2, South = Math.PI; // Godot yaws (ViewInput)

    private readonly ITestOutputHelper _output;

    public M4bScenarioTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Put(RealHeadlessWorld w, mob m, int x, int y)
    {
        using (w.World.Enter())
            Waiting.NoSleep(m.forceMove(Builtins.locate(x, y, 1)));
        w.Run(Second);
    }

    private static T Find<T>(RealHeadlessWorld w, int x, int y) where T : class
        => In(w, () => Dm.each_cast<atom>(Builtins.locate(x, y, 1).contents).OfType<T>().FirstOrDefault());

    private static string AreaOf(RealHeadlessWorld w, int x, int y) => In(w, () => Dm.str(((area)Builtins.locate(x, y, 1).loc).name));

    private static float Kpa(RealHeadlessWorld w, int x, int y)
        => In(w, () => Builtins.locate(x, y, 1) is turf_open t && t.air != null ? Dm.num(t.air.return_pressure()) : -1f);

    private static float Kelvin(RealHeadlessWorld w, int x, int y) => In(w, () => ((turf_open)Builtins.locate(x, y, 1)).air.temperature);

    private static (int X, int Y) At(RealHeadlessWorld w, atom a) => In(w, () => ((int)a.x, (int)a.y));

    private static bool Dense(RealHeadlessWorld w, atom a) => In(w, () => Dm.truthy(a.density));

    /// <summary>
    /// tg's volume at a distance before the pressure factor: playsound_local's falloff, <c>vol - CALCULATE_SOUND_VOLUME_RATIO *
    /// (vol - min_volume)</c> (sound.dm, __DEFINES/sound.dm:32) with playsound's range <c>SOUND_RANGE + extrarange</c>, its
    /// falloff_distance 0, exponent 2.5 and playsound_local's min_volume 5.
    /// </summary>
    private static double TgVolume(double vol, double distance, double extraRange = 0, double falloffDistance = 0)
    {
        double max = 15 + extraRange;
        return vol - Math.Pow(Math.Max(distance - falloffDistance, 0) / (Math.Max(max, distance) - falloffDistance), 1 / 2.5) * (vol - 5);
    }

    private static int HalfUnits(double volume) => (int)Math.Round(volume * 2, MidpointRounding.AwayFromZero);

    private static double Distance(int x1, int y1, int x2, int y2) => Math.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));

    /// <summary>What one client heard: each event of its Sounds messages with the tick it arrived in.</summary>
    private sealed class Ear
    {
        public readonly List<(long Tick, SoundEvent E)> Heard = new();

        public Ear(RealHeadlessWorld w, RealHeadlessWorld.Player p) => p.Session.Replica.SoundsReceived += events =>
        {
            foreach (SoundEvent e in events) Heard.Add((w.Ticks, e));
        };

        public List<SoundEvent> Plays(string file, long from = 0) => Heard
            .Where(h => h.Tick >= from && h.E.Kind == SoundKind.Play && h.E.File != null && h.E.File.StartsWith(file, StringComparison.Ordinal))
            .Select(h => h.E).ToList();
    }

    private void Log(string who, IEnumerable<SoundEvent> events)
    {
        foreach (SoundEvent e in events)
            _output.WriteLine($"  {who}: {e.Kind} ch {e.Channel} {e.File} volume {e.Volume} muffle {e.Muffle} occlusion {e.Occlusion} from {(e.Positional ? $"{e.X},{e.Y}" : "own tile")}");
    }

    private static List<string> ScreenStates(ReplicaWorld replica) => replica.Screen.Values
        .SelectMany(e => CrewOutfitTests.Icons(replica, e.Appearance)).Select(i => i.State).Where(s => s != null).Distinct().ToList();

    private static List<string> States(ReplicaWorld replica, atom a)
    {
        ReplicaEntity seen = replica.Entities.Single(e => e.Id == a.dm_id);
        return CrewOutfitTests.Icons(replica, seen.State.Appearance).Select(i => i.State).ToList();
    }

    private static int SeenHotspots(RealHeadlessWorld w, ReplicaWorld replica)
        => In(w, () => replica.Entities.Count(e => w.World.Objects.TryGet(e.Id, out object o) && o is obj_effect_hotspot));

    private static int SeenPuffs(RealHeadlessWorld w, ReplicaWorld replica)
        => In(w, () => replica.Entities.Count(e => w.World.Objects.TryGet(e.Id, out object o) && o is obj_effect_particle_effect_water_extinguisher));

    private static void Walk(RealHeadlessWorld w, RealHeadlessWorld.Player p, double yaw, Func<bool> until, int maxTicks = 3 * Second)
    {
        p.Yaw = yaw;
        p.Keys = MoveKeys.Forward;
        for (int i = 0; i < maxTicks && !In(w, until); i++) w.Run(1);
        p.Keys = MoveKeys.None;
        w.Run(5);
    }

    [MetaServerFact]
    public void A_window_breach_decompresses_a_room_a_canister_refills_it_and_an_extinguisher_puts_out_a_plasma_fire_heard_by_two_clients()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 4000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        MetaServer.SettleAir(w);
        using (w.World.Enter())
            LocomotionSeam.SetFeel(w.World, default); // the walks below stop on the tile a key-up finds them on: no coast (DV263)
        RealHeadlessWorld.Player p = w.Join("Engineer");
        RealHeadlessWorld.Player q = w.Join("Listener");
        var me = (mob_living_carbon_human)w.MobOf(p);
        var other = (mob_living_carbon_human)w.MobOf(q);
        ReplicaWorld replica = p.Session.Replica, replicaQ = q.Session.Replica;
        var earP = new Ear(w, p);
        var earQ = new Ear(w, q);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        var runtimes = new List<string>();
        Action<Exception> hostSink = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e =>
        {
            hostSink?.Invoke(e);
            runtimes.Add(e.Message);
        };

        Assert.EndsWith("Fitness Room", AreaOf(w, RoomX, RoomY));
        Assert.EndsWith("Recreational Holodeck", AreaOf(w, HoloX, HoloY));
        Assert.EndsWith("Recreation Area", AreaOf(w, DoorX, DoorY - 1));
        Assert.NotNull(Find<obj_structure_window_reinforced_fulltile>(w, HoleX, HoleY));
        Assert.IsAssignableFrom<turf_open_space>(In(w, () => Builtins.locate(HoleX - 1, HoleY, 1)));
        var door = Find<obj_machinery_door_airlock_public_glass>(w, DoorX, DoorY);
        var firelock = Find<obj_machinery_door_firedoor>(w, DoorX, DoorY);
        var fireAlarm = Find<obj_machinery_firealarm>(w, AlarmX, AlarmY);
        Assert.NotNull(door);
        Assert.NotNull(firelock);
        Assert.NotNull(fireAlarm);
        Assert.True(Dense(w, door) && !Dense(w, firelock), "Meta maps the airlock closed and the firelock open");

        // ---- 1. The audio base: footsteps, an airlock heard with tg's falloff, and behind walls with the occlusion.
        Put(w, me, 140, 184);
        Put(w, other, HoloX, HoloY);
        long walked = w.Ticks;
        Walk(w, p, West, () => me.x <= 138);
        Walk(w, p, South, () => me.y <= 183);
        Assert.Equal((138, 183), At(w, me));
        List<SoundEvent> steps = earP.Plays(Footstep, walked);
        Log("engineer", steps);
        Assert.NotEmpty(steps); // tg's footstep element: every other step of a human on a floor
        Assert.All(steps, e => Assert.Equal(0, e.Muffle)); // the room's full air
        int fullStep = steps[0].Volume;
        Assert.True(fullStep > 0);

        long opened = w.Ticks;
        Assert.True(p.Session.Click(door.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second);
        Assert.False(Dense(w, door), "a hand opens the public airlock");
        Assert.Contains("open", States(replica, door));
        SoundEvent open = Assert.Single(earP.Plays(Airlock, opened));
        SoundEvent openFar = Assert.Single(earQ.Plays(Airlock, opened));
        Log("engineer", new[] { open });
        Log("listener", new[] { openFar });
        // airlock.dm:663 playsound(src, doorOpen, 30, TRUE): one tile away 21.54 (43 half units), open air, no muffle
        Assert.Equal(HalfUnits(TgVolume(30, 1)), open.Volume);
        Assert.Equal((0, 0), (open.Muffle, open.Occlusion));
        Assert.Equal((DoorX, DoorY), (open.X, open.Y));
        // the holodeck, 12.2 tiles away inside tg's range 15, behind its closed doors: tg's volume, sealed off by the station graph
        Assert.Equal(HalfUnits(TgVolume(30, Distance(HoloX, HoloY, DoorX, DoorY))), openFar.Volume);
        Assert.Equal(255, openFar.Occlusion);
        Assert.Equal(0, openFar.Muffle);

        long closed = w.Ticks;
        Assert.True(p.Session.Click(door.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second);
        Assert.True(Dense(w, door), "a second click closes it");
        Assert.Equal(HalfUnits(TgVolume(30, 1)), Assert.Single(earP.Plays(AirlockClose, closed)).Volume);
        Assert.Equal(255, Assert.Single(earQ.Plays(AirlockClose, closed)).Occlusion);

        // ---- 2. The breach: the window removed by the dev console, the room drains, the wind pulls, the firelock closes. The engineer
        // stands a tile west of the room's middle: tg pushes a mob only from a 25 kPa difference (LINDA_turf_tile.dm:404, sqrt(diff) * 200
        // against its move_resist 1000), which the drain's wind passes nearer the hole
        Put(w, me, RoomX - 1, RoomY);
        w.Host.EnqueueConsoleLine("spawn /obj/item/paper 140 184 1");
        w.Host.EnqueueConsoleLine("spawn /obj/item/paper 138 184 1");
        w.Run(2);
        var paperEast = Find<obj_item_paper>(w, 140, 184);
        var paperWest = Find<obj_item_paper>(w, 138, 184);
        Assert.NotNull(paperEast);
        Assert.NotNull(paperWest);
        Assert.Equal(101.3f, Kpa(w, RoomX, RoomY), 0.5f);

        List<string> alarmBefore = States(replica, fireAlarm);
        long breached = w.Ticks;
        w.Host.EnqueueConsoleLine($"breach {HoleX} {HoleY} 1");
        int westFlows = 0;
        int closedAt = -1;
        for (int t = 0; t < 60 * Second && closedAt < 0; t++)
        {
            w.Run(1);
            westFlows = Math.Max(westFlows, replica.AirflowCells.Count(c => c.X >= HoleX && c.X <= 141 && c.Y >= 183 && c.Y <= 185 && Airflow.DirOf(c.Cell) == AirflowDir.West));
            if (Dense(w, firelock)) closedAt = t;
        }
        float atClose = Kpa(w, RoomX, RoomY);
        _output.WriteLine($"the firelock closed {closedAt / (float)Second:0.0} s after the breach at {atClose:0.0} kPa, {Kelvin(w, DoorX, DoorY + 1):0} K by it; engineer at {At(w, me)}, papers at {At(w, paperWest)} and {At(w, paperEast)}; most westward airflow cells seen {westFlows}");
        Assert.IsAssignableFrom<turf_open_space>(In(w, () => Builtins.locate(HoleX, HoleY, 1)));
        Assert.Null(Find<obj_structure_window>(w, HoleX, HoleY));
        Assert.True(closedAt > 0, "the cold air closes the firelock (firedoor.dm:287, BODYTEMP_COLD_DAMAGE_LIMIT)");
        Assert.True(atClose < 25f, "the room drains: " + atClose);
        Assert.True(westFlows >= 3, "the client sees the air streaming west to the hole: " + westFlows);
        // the space wind pulls the engineer and the paper by him west, toward the hole, and the client sees the paper there
        Assert.True(At(w, me).X < RoomX - 1, "the engineer was pulled west: " + At(w, me));
        Assert.True(At(w, paperWest).X < 138 || At(w, paperEast).X < 140, $"a paper was pulled west: {At(w, paperWest)} {At(w, paperEast)}");
        foreach (obj_item_paper paper in new[] { paperWest, paperEast })
        {
            (int px, int py) = At(w, paper);
            if (In(w, () => paper.loc is turf_open_space)) continue; // a paper blown out into space can leave the client's view
            ReplicaEntity seen = replica.Entities.Single(e => e.Id == paper.dm_id);
            Assert.Equal((px, py), (seen.State.X, seen.State.Y));
        }
        List<SoundEvent> wind = earP.Plays(SpaceWind, breached);
        Log("engineer", wind.Take(3));
        Assert.NotEmpty(wind); // living.dm:1112 experience_pressure_difference: playsound(src, 'sound/effects/space_wind.ogg', 50, TRUE)

        // the footsteps of the pulled engineer: tg's pressure factor (1 - muffle) times the full volume, 0.15 at least on the own tile
        List<SoundEvent> drainSteps = earP.Plays(Footstep, breached);
        Log("engineer", drainSteps);
        foreach (SoundEvent e in drainSteps)
            Assert.InRange(e.Volume, fullStep * Math.Max(1 - e.Muffle / 255.0, 0.15) - 2, fullStep * Math.Max(1 - e.Muffle / 255.0, 0.15) + 2);

        // the closed firelock as the client sees it, and its siren (firedoor.dm:332 soundloop.start, /datum/looping_sound/firealarm)
        w.Run(5 * Second);
        List<string> lockStates = States(replica, firelock);
        _output.WriteLine("the firelock as the client sees it: " + string.Join(", ", lockStates));
        Assert.Contains("door_closed", lockStates);
        Assert.Contains("firelock_alarm_type_cold", lockStates); // firedoor.dm update_overlays: the alarm's hazard lights
        List<string> alarmAfter = States(replica, fireAlarm);
        _output.WriteLine($"the fire alarm as the client sees it: {string.Join(", ", alarmBefore)} before, {string.Join(", ", alarmAfter)} after");
        Assert.DoesNotContain("fire_alerting", alarmBefore);
        Assert.Contains("fire_alerting", alarmAfter); // firealarm.dm update_overlays: the area's active firelocks light the panel
        List<SoundEvent> sirenP = earP.Plays(Siren, breached), sirenQ = earQ.Plays(Siren, breached);
        Log("engineer", sirenP);
        Log("listener", sirenQ);
        Assert.NotEmpty(sirenQ);
        Assert.All(sirenQ, e => Assert.Equal((DoorX, DoorY, 255, 0), (e.X, e.Y, e.Occlusion, e.Muffle))); // the listener's holodeck has its air
        Assert.Equal(HalfUnits(TgVolume(30, Distance(HoloX, HoloY, DoorX, DoorY))), sirenQ[0].Volume);
        // in the draining room: quieter by tg's pressure factor, the muffle 1 minus it, a vacuum's 0 volume and full muffle
        Assert.NotEmpty(sirenP);
        double sirenNear = TgVolume(30, Distance(At(w, me).X, At(w, me).Y, DoorX, DoorY));
        Assert.All(sirenP, e => Assert.True(e.Muffle > 0, "a siren heard in the thin air is muffled"));
        Assert.All(sirenP, e => Assert.InRange(e.Volume, HalfUnits(sirenNear * (1 - e.Muffle / 255.0)) - 1, HalfUnits(sirenNear * (1 - e.Muffle / 255.0)) + 1));
        Assert.Contains(sirenP, e => e.Muffle == 255 && e.Volume == 0);

        // ---- 3. The engineer suffocates and the low pressure hurts him; his HUD shows it.
        float oxy = In(w, () => Dm.num(me.get_oxy_loss())), brute = In(w, () => Dm.num(me.get_brute_loss()));
        List<string> hud = ScreenStates(replica);
        var zones = new List<FoundZone>();
        foreach (ScreenEntry entry in replica.Screen.Values) HudReader.Collect(replica, entry.Appearance, zones);
        _output.WriteLine($"the engineer: oxy {oxy:0.#}, brute {brute:0.#}, health {In(w, () => me.health):0.#}, stat {In(w, () => me.stat)}; his HUD: {string.Join(", ", hud.Where(s => s.Contains("oxy") || s.Contains("press") || s.Contains("health") || s.Contains("cold")))}; doll {string.Join(" ", zones.Select(z => $"{z.Zone.Zone}:{z.Zone.Level}"))}");
        Assert.True(oxy > 0, "no air to breathe (lungs.dm: oxyloss)");
        Assert.True(brute > 0, "the low pressure's brute damage (LOW_PRESSURE_DAMAGE)");
        Assert.Contains("not_enough_oxy", hud); // /atom/movable/screen/alert/not_enough_oxy
        Assert.Contains(hud, s => s.StartsWith("lowpressure", StringComparison.Ordinal)); // /atom/movable/screen/alert/lowpressure
        Assert.Contains(hud, s => s.StartsWith("health", StringComparison.Ordinal) && s != "health0"); // the health HUD's hurt state
        Assert.Contains(zones, z => z.Zone.Level >= 1); // the health doll's hurt limbs

        // ---- 4. The hole patched by the dev console, the room refilled from an air canister by the listener's valve.
        w.Host.EnqueueConsoleLine($"patch {HoleX} {HoleY} 1");
        w.Host.EnqueueConsoleLine("spawn /obj/machinery/portable_atmospherics/canister/air 141 185 1");
        w.Run(5);
        Assert.IsNotType<turf_open_space>(In(w, () => Builtins.locate(HoleX, HoleY, 1))); // patch puts back the turf and the window
        Assert.NotNull(Find<obj_structure_window_reinforced_fulltile>(w, HoleX, HoleY));
        var canister = Find<obj_machinery_portable_atmospherics_canister_air>(w, 141, 185);
        Assert.NotNull(canister);
        Put(w, other, 140, 184);
        string gaugeFull = States(replicaQ, canister).Single(s => s.StartsWith("can-", StringComparison.Ordinal));
        long valve = w.Ticks;
        q.Session.DevCommand("valve 141 185 1"); // the dev command for the canister's valve button (its tgui window came with M5 slice 3): toggle_valve(user)
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(canister.valve_open)));
        SoundEvent valveSound = Assert.Single(earQ.Plays(Valve, valve)); // canister.dm:694 playsound(src, valve_opening.ogg, 50, TRUE)
        Log("listener", new[] { valveSound });
        Assert.Equal((141, 185), (valveSound.X, valveSound.Y));
        // heard in the still empty room: tg's volume 50 at 1.4 tiles times the pressure factor, nearly all muffled
        Assert.True(valveSound.Muffle > 200, "the valve opens in a near vacuum");
        Assert.InRange(valveSound.Volume, HalfUnits(TgVolume(50, Math.Sqrt(2)) * (1 - valveSound.Muffle / 255.0)) - 1, HalfUnits(TgVolume(50, Math.Sqrt(2)) * (1 - valveSound.Muffle / 255.0)) + 1);
        float refilled = 0;
        for (int s = 0; s < 60 && refilled < 95f; s++)
        {
            w.Run(Second);
            refilled = Kpa(w, RoomX, RoomY);
        }
        string gaugeAfter = States(replicaQ, canister).Single(s => s.StartsWith("can-", StringComparison.Ordinal));
        _output.WriteLine($"refilled to {refilled:0.0} kPa at {Kelvin(w, DoorX, DoorY + 1):0} K in {(w.Ticks - valve) / (float)Second:0} s; the canister at {In(w, () => Dm.num(canister.air_contents.return_pressure())):0} kPa, its gauge {gaugeFull} then {gaugeAfter}; firelock closed {Dense(w, firelock)}");
        Assert.True(refilled >= 95f, "the canister refills the patched room: " + refilled);
        Assert.Equal(101.3f, Kpa(w, HoleX + 1, HoleY), 6f); // the patched hole holds
        Assert.NotEqual(gaugeFull, gaugeAfter); // the client sees the canister's pressure light drop
        List<SoundEvent> refillSiren = earQ.Plays(Siren, valve);
        Log("listener", refillSiren.Take(2).Concat(refillSiren.TakeLast(2)));
        Assert.True(refillSiren[^1].Muffle < refillSiren[0].Muffle && refillSiren[^1].Volume > refillSiren[0].Volume, "the siren comes back as the air does");

        // ---- 5. A plasma fire put out with an extinguisher: the listener takes the extinguisher from the Recreation Area's
        // cabinet, crowbars the cold firelock open, opens the airlock, throws a lit welder into plasma and sprays the fire out.
        Put(w, me, 144, 180); // the hurt engineer is taken out of the way (there is no carrying in the scenario)
        var cabinet = Find<obj_structure_extinguisher_cabinet>(w, CabinetX, CabinetY);
        Assert.NotNull(cabinet);
        Assert.True(In(w, () => cabinet.stored_extinguisher != null && !Dm.truthy(cabinet.opened)), "Meta maps the cabinet closed, with an extinguisher (extinguisher.dm:20)");
        Put(w, other, CabinetX + 1, CabinetY);
        List<string> cabinetClosed = States(replicaQ, cabinet);
        long took = w.Ticks;
        Assert.True(q.Session.Click(cabinet.dm_id, ClickButton.Left, ClickModifiers.None)); // extinguisher.dm:99 attack_hand
        w.Run(Second);
        var extinguisher = In(w, () => other.get_active_held_item() as obj_item_extinguisher);
        Assert.NotNull(extinguisher);
        Assert.Null(In(w, () => cabinet.stored_extinguisher));
        List<string> cabinetOpen = States(replicaQ, cabinet);
        _output.WriteLine($"the cabinet as the client sees it: {string.Join(", ", cabinetClosed)} before, {string.Join(", ", cabinetOpen)} after");
        Assert.Contains("extinguisher_default", cabinetClosed); // update_icon_state, update_overlays: the closed door
        Assert.Contains("door", cabinetClosed);
        Assert.Contains("extinguisher", cabinetOpen);
        Assert.Contains("door_open", cabinetOpen);
        SoundEvent cabinetClick = Assert.Single(earQ.Plays(CabinetClick, took)); // playsound(loc, 'sound/machines/click.ogg', 15, TRUE, -3)
        Log("listener", new[] { cabinetClick });
        Assert.NotEmpty(earQ.Plays(Slosh, took)); // the full extinguisher taken in hand (equipped: SFX_DEFAULT_LIQUID_SLOSH)
        Assert.Equal(100f, In(w, () => ((datum_reagents)extinguisher.reagents).total_volume));

        Put(w, other, DoorX, DoorY - 1);
        Assert.True(Dense(w, firelock), "the room's air is still below tg's 260 K, so the firelock keeps its cold alarm");
        w.Host.EnqueueConsoleLine("give Listener /obj/item/crowbar");
        w.Run(5);
        q.Session.Keybinding(KeybindingCode.SwapHands); // the crowbar went to the free hand
        w.Run(5);
        Assert.IsAssignableFrom<obj_item_crowbar>(In(w, () => other.get_active_held_item()));
        Assert.True(q.Session.Click(firelock.dm_id, ClickButton.Left, ClickModifiers.None)); // firedoor.dm:546 try_to_crowbar: held open
        w.Run(2 * Second);
        Assert.False(Dense(w, firelock), "the crowbar holds the firelock open");
        q.Session.Keybinding(KeybindingCode.DropItem); // the firelock stays held while the listener stands by it (firedoor.dm:578)
        w.Run(5);
        Assert.Null(In(w, () => other.get_active_held_item()));
        Assert.True(q.Session.Click(door.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second);
        Assert.False(Dense(w, door), "the empty hand opens the airlock");
        long lit = w.Ticks;
        w.Host.EnqueueConsoleLine("give Listener /obj/item/weldingtool");
        w.Run(5);
        var welder = In(w, () => other.get_active_held_item() as obj_item_weldingtool);
        Assert.NotNull(welder);
        q.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(welder.welding)), "the welder is lit");
        SoundEvent welderOn = Assert.Single(earQ.Plays(WelderOn, lit)); // weldingtool.dm:256 playsound(loc, activation_sound, 50, TRUE)
        Log("listener", new[] { welderOn });
        // on the own tile in the Recreation Area's air (a breath under one atmosphere since the room's door was open): vol 50 times the pressure factor
        Assert.True(welderOn.Muffle < 20);
        Assert.InRange(welderOn.Volume, HalfUnits(TgVolume(50, 0) * (1 - welderOn.Muffle / 255.0)) - 1, HalfUnits(TgVolume(50, 0) * (1 - welderOn.Muffle / 255.0)) + 1);

        // the lighting channels of the room's and the Recreation Area's APCs go off first, as the APC window's lighting "Off" does
        // (apc_main.dm:511 ui_act "channel": setsubsystem(1), update): a tube the fire's heat breaks (light.dm:733) sparks while
        // its area has light power (light.dm:510 has_power, :315 broken_sparks), and the sparks would light the plasma again and
        // again (the Fitness Room has no light switch)
        using (w.World.Enter())
        {
            foreach (var at in new[] { Builtins.locate(RoomX, RoomY, 1), Builtins.locate(DoorX, DoorY - 1, 1) })
            {
                var room = (area)GlobalProcs.get_area(at);
                var apc = Dm.each_cast<obj_machinery_power_apc>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_apc))).Single(a => ReferenceEquals(a.area, room));
                apc.lighting = Dm.num(apc.setsubsystem(1));
                apc.update_appearance();
                apc.update();
                Assert.False(Dm.truthy(room.power_light), "the lights of " + Dm.str(room.name) + " are off");
            }
        }
        long fired = w.Ticks;
        w.Host.EnqueueConsoleLine("air add plasma 20 140 184 1 1"); // 20 moles on each open tile of the room's east part
        w.Run(2);
        q.Session.Keybinding(KeybindingCode.ToggleThrowMode);
        w.Run(2);
        Assert.True(q.Session.Click(new ClickTarget(TargetKind.Turf, 0, RoomX, RoomY, 1), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        _output.WriteLine($"the welder landed at {At(w, welder)} ({In(w, () => welder.loc.GetType().Name)})");
        // the extinguisher's hand, its safety off (extinguisher.dm:228 attack_self: the sprite shows the nozzle)
        q.Session.Keybinding(KeybindingCode.SwapHands);
        w.Run(5);
        Assert.Same(extinguisher, In(w, () => other.get_active_held_item()));
        q.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(5);
        Assert.Equal(0f, In(w, () => extinguisher.safety));
        Assert.Equal("fire_extinguisher1", In(w, () => extinguisher.icon_state));

        // per tick, the drop-off distances of the burning groups' crackles (update_sound: falloff_distance = extra_range = the group's size)
        var dropOffs = new Dictionary<long, HashSet<float>>();
        int peak = 0, seenPeak = 0, seenPuffs = 0, sprays = 0, doorOpenings = 0;
        long nextSpray = 0;
        bool plasmaSeen = false;
        // the listener's tile per tick: a fire that reaches him through the door moves him
        var ears = new Dictionary<long, (int X, int Y)>();
        (int X, int Y) byTheDoorTile = At(w, other);
        (int X, int Y) Ear(long tick) => ears.TryGetValue(tick, out var at) || ears.TryGetValue(tick - 1, out at) ? at : byTheDoorTile;
        // one tick of the fire: the hotspots now, what the clients see
        int Burning()
        {
            w.Run(1);
            int spots = In(w, () => (int)SSair.hotspots.len);
            ears[w.Ticks] = At(w, other);
            dropOffs[w.Ticks] = In(w, () => Dm.each_cast<obj_effect_hotspot>(SSair.hotspots).Select(h => h.our_hot_group?.sound).Where(l => l != null)
                .Select(l => Dm.num(l.falloff_distance)).ToHashSet());
            peak = Math.Max(peak, spots);
            seenPuffs = Math.Max(seenPuffs, SeenPuffs(w, replicaQ));
            if (w.Ticks % Second == 0)
            {
                seenPeak = Math.Max(seenPeak, SeenHotspots(w, replicaQ));
                plasmaSeen |= replicaQ.GasAt(RoomX + 1, RoomY, 1).Any(g => replicaQ.GasKinds[g.Kind - 1] == "plasma");
            }
            return spots;
        }
        // the listener sprays the burning tile nearest to him every 1.3 s (the extinguisher's 1.2 s cooldown,
        // extinguisher.dm:296), reopening the airlock when it has closed itself (the other hand is free)
        void SprayNearest()
        {
            if (w.Ticks < nextSpray)
                return;
            nextSpray = w.Ticks + 26;
            // an empty extinguisher does not spray (extinguisher.dm:291): a fire that grows past its 20 sprays burns out by
            // itself (how far it spreads is tg's chance: the spray's tiles, extinguisher.dm:325, and the groups' merges, LINDA_fire.dm:432)
            if (In(w, () => Dm.num(((datum_reagents)extinguisher.reagents).total_volume)) < 1)
                return;
            if (In(w, () => other.stat != 0 /* CONSCIOUS */ || extinguisher.loc != other)) // the fire reached him through the door
                return;
            if (Dense(w, door))
            {
                q.Session.Keybinding(KeybindingCode.SwapHands);
                w.Run(2);
                Assert.True(q.Session.Click(door.dm_id, ClickButton.Left, ClickModifiers.None));
                w.Run(2);
                q.Session.Keybinding(KeybindingCode.SwapHands);
                w.Run(1);
                doorOpenings++;
                nextSpray = w.Ticks + Second; // past the click cooldown
                return;
            }
            (int X, int Y) me2 = At(w, other);
            (int X, int Y) burning = In(w, () => Dm.each_cast<obj_effect_hotspot>(SSair.hotspots).Select(h => ((int)h.x, (int)h.y))
                .OrderBy(c => Distance(c.Item1, c.Item2, me2.X, me2.Y)).First());
            Assert.True(q.Session.Click(new ClickTarget(TargetKind.Turf, 0, burning.X, burning.Y, 1), ClickButton.Left, ClickModifiers.None));
            sprays++;
        }

        // the fire spreads, then the listener sprays it from the door until it is out
        long outTick = -1;
        for (int t = 0; t < 240 * Second && outTick < 0; t++) // a fire that outgrows the extinguisher takes minutes to burn out
        {
            int spots = Burning();
            if (peak > 0 && spots == 0)
                outTick = w.Ticks;
            else if (peak >= 4)
                SprayNearest();
        }
        float waterAtOut = In(w, () => ((datum_reagents)extinguisher.reagents).total_volume);
        _output.WriteLine($"the fire: at most {peak} hotspots ({seenPeak} seen by the listener's client), out after {(outTick - fired) / (float)Second:0.0} s and {sprays} sprays ({waterAtOut} units left, {seenPuffs} puffs seen at once by the client, the airlock reopened {doorOpenings} times), {Kelvin(w, RoomX, RoomY):0} K in the room");
        Assert.True(peak > 2, "the lit welder starts a plasma fire that spreads: " + peak);
        Assert.True(seenPeak > 0, "the client sees the hotspots");
        Assert.True(sprays > 0, "the listener sprays the fire");
        Assert.True(outTick > 0, "the fire goes out");
        Assert.True(plasmaSeen, "the client sees the plasma's gas overlay");
        Assert.True(seenPuffs > 0, "the client sees the extinguisher's water puffs (/obj/effect/particle_effect/water/extinguisher)");
        Assert.Equal(0, SeenHotspots(w, replicaQ)); // the hotspots go from the listener's client too
        int wet = Math.Min(sprays, 20); // a click on an empty extinguisher sprays nothing and is silent
        Assert.Equal(100f - 5f * wet, waterAtOut, 0.01f); // each spray: five puffs of one unit (extinguisher.dm:330)
        List<SoundEvent> spraySounds = earQ.Plays(SpraySound, fired);
        Log("listener", spraySounds.Take(2));
        Assert.Equal(wet, spraySounds.Count); // extinguisher.dm:301 playsound(src.loc, 'sound/effects/extinguish.ogg', 75, TRUE, -3)
        Assert.All(spraySounds, e => Assert.InRange(e.Volume, HalfUnits(TgVolume(75, 0, extraRange: -3) * (1 - e.Muffle / 255.0)) - 1, HalfUnits(TgVolume(75, 0, extraRange: -3) * (1 - e.Muffle / 255.0)) + 1));

        // the welder still lit on the floor relights the plasma left (weldingtool.dm:114 open_flame): the listener walks in
        // with his free hand, picks it up and turns it off (weldingtool.dm attack_self), then sprays what it relit
        long retrieved = w.Ticks;
        // a fire that ran away through the door burns the listener standing there (he falls and drops the extinguisher): the
        // welder then burns on in the room until the plasma is gone
        bool standing = In(w, () => other.stat == 0 /* CONSCIOUS */ && extinguisher.loc == other);
        _output.WriteLine($"the listener after the fire: {In(w, () => $"stat {other.stat}, {Dm.num(other.get_fire_loss()):0} burn")}");
        if (standing)
        {
            q.Session.Keybinding(KeybindingCode.SwapHands);
            w.Run(2);
            if (Dense(w, firelock)) // a long fire's heat closes the firelock again, and the crowbar dropped by it may have burnt (obj fire_act)
            {
                w.Host.EnqueueConsoleLine("give Listener /obj/item/crowbar");
                w.Run(5);
                Assert.IsAssignableFrom<obj_item_crowbar>(In(w, () => other.get_active_held_item()));
                Assert.True(q.Session.Click(firelock.dm_id, ClickButton.Left, ClickModifiers.None));
                w.Run(2 * Second);
                Assert.False(Dense(w, firelock), "the crowbar opens the firelock again");
                q.Session.Keybinding(KeybindingCode.DropItem);
                w.Run(5);
            }
            if (Dense(w, door)) // a long fire outlasts the airlock's autoclose
            {
                Assert.True(q.Session.Click(door.dm_id, ClickButton.Left, ClickModifiers.None));
                w.Run(2 * Second);
            }
            Walk(w, q, North, () => other.y >= DoorY + 1);
            Assert.True(In(w, () => Dm.truthy(Waiting.NoSleep(welder.Adjacent(other)))), $"the listener at {At(w, other)} reaches the welder at {At(w, welder)} (airlock closed {Dense(w, door)}, firelock closed {Dense(w, firelock)})");
            Assert.True(q.Session.Click(welder.dm_id, ClickButton.Left, ClickModifiers.None));
            w.Run(5);
            Assert.Same(welder, In(w, () => other.get_active_held_item()));
            q.Session.Keybinding(KeybindingCode.ActivateInhand);
            w.Run(5);
            Assert.False(In(w, () => Dm.truthy(welder.welding)), "the listener turns the welder off");
            Assert.Single(earQ.Plays(WelderOff, retrieved)); // weldingtool.dm switched_off: playsound(loc, deactivation_sound, 50, TRUE)
            q.Session.Keybinding(KeybindingCode.SwapHands);
            w.Run(2);
            Assert.Same(extinguisher, In(w, () => other.get_active_held_item()));
        }
        nextSpray = 0;
        long cold = -1;
        int relit = 0;
        for (int t = 0; t < 120 * Second && cold < 0; t++)
        {
            int spots = Burning();
            relit = Math.Max(relit, spots);
            if (spots > 0)
            {
                outTick = -1;
                SprayNearest();
            }
            else if (outTick < 0)
                outTick = w.Ticks;
            else if (w.Ticks - outTick >= 5 * Second)
                cold = w.Ticks;
        }
        float waterLeft = In(w, () => ((datum_reagents)extinguisher.reagents).total_volume);
        _output.WriteLine($"after the welder: at most {relit} hotspots relit, out for good {(outTick - fired) / (float)Second:0.0} s after the fire started, {sprays} sprays in all ({waterLeft} units left); firelock closed {Dense(w, firelock)}, door closed {Dense(w, door)}, {Kelvin(w, RoomX, RoomY):0} K in the room, {In(w, () => Dm.num(((turf_open)Builtins.locate(RoomX, RoomY, 1)).air.moles[typeof(datum_gas_plasma)])):0.0} mol of plasma left at {RoomX},{RoomY}");
        Assert.True(cold > 0, "the fire stays out");
        Assert.Equal(0, In(w, () => (int)SSair.hotspots.len));
        Assert.Equal(0, SeenHotspots(w, replicaQ));
        Assert.Equal(100f - 5f * Math.Min(sprays, 20), waterLeft, 0.01f); // an empty extinguisher sprays nothing (extinguisher.dm:292)
        Assert.Equal(Math.Min(sprays, 20), earQ.Plays(SpraySound, fired).Count);

        List<SoundEvent> puffs = earQ.Plays(FirePuff, fired).Concat(earP.Plays(FirePuff, fired)).ToList();
        List<SoundEvent> crackleQ = earQ.Plays(Crackle, fired), crackleP = earP.Plays(Crackle, fired);
        Log("listener", puffs.Take(2));
        Log("listener", crackleQ.Take(3));
        Log("engineer", crackleP.Take(3));
        Assert.NotEmpty(puffs); // LINDA_fire.dm:144 playsound(our_turf, 'sound/effects/fire_puff.ogg', 30)
        // the listener's ears while he stood by the door (until he walked in for the welder)
        foreach ((long tick, SoundEvent e) in earQ.Heard.Where(h => h.Tick >= fired && h.Tick < retrieved && h.E.Kind == SoundKind.Play && h.E.File.StartsWith(FirePuff, StringComparison.Ordinal)))
        {
            // a puff on his own tile is not positional; one played in the tick he was moved used his tile before it
            Assert.True(new[] { Ear(tick), Ear(tick - 1) }.Any(at => Math.Abs(e.Volume - HalfUnits(TgVolume(30, e.Positional ? Distance(at.X, at.Y, e.X, e.Y) : 0) * (1 - e.Muffle / 255.0))) <= 1),
                $"a fire puff of volume {e.Volume} (muffle {e.Muffle}) from {e.X},{e.Y}, heard at {Ear(tick)}");
        }
        // the crackle: /datum/looping_sound/fire's volume 30 with the group's size as its falloff_distance and extra_range
        // (LINDA_fire.dm:448 update_sound), through the open door
        Assert.NotEmpty(crackleQ);
        int checkedCrackles = 0, byTheDoor = 0, matched = 0;
        foreach ((long tick, SoundEvent e) in earQ.Heard.Where(h => h.Tick >= fired && h.Tick < retrieved && h.E.Kind == SoundKind.Play && h.E.File.StartsWith(Crackle, StringComparison.Ordinal)))
        {
            byTheDoor++;
            var sizes = Enumerable.Range(-3, 5).SelectMany(d => dropOffs.TryGetValue(tick + d, out var set) ? set : new HashSet<float>()).Distinct().ToList();
            if (sizes.Count == 0) continue; // heard before the burning loop began to watch the groups
            // the loop's first crackle, played by a group that grew later in the same tick (no group sound in the ticks before it;
            // M5x slice 1's ticker subsystem moved SSair's fire within the tick): the sizes after it are not the one it played with
            if (Enumerable.Range(-3, 3).All(d => !dropOffs.TryGetValue(tick + d, out var before) || before.Count == 0)) continue;
            checkedCrackles++;
            double[] distances = new[] { Ear(tick), Ear(tick - 1) }.Select(at => e.Positional ? Distance(at.X, at.Y, e.X, e.Y) : 0).ToArray();
            double distance = distances[0];
            if (sizes.Any(d => distances.Any(r => Math.Abs(e.Volume - HalfUnits(TgVolume(30, r, extraRange: d, falloffDistance: d) * (1 - e.Muffle / 255.0))) <= 1)))
                matched++;
            else
                _output.WriteLine($"a crackle of volume {e.Volume} (muffle {e.Muffle}) at {distance:0.##} tiles, the groups' sizes {string.Join(",", sizes)}");
        }
        Assert.True(checkedCrackles * 2 >= byTheDoor, $"{checkedCrackles} of {byTheDoor} crackles checked");
        // most crackles match a group's size in the ticks around them; in a big fire the groups merge and split from tick to tick
        // (LINDA_fire.dm:432), so a crackle can carry the size of a group that no longer is
        Assert.True(matched * 4 >= checkedCrackles * 3, $"{matched} of {checkedCrackles} crackles match a group's size");
        Assert.Empty(earQ.Plays(Crackle, outTick + 1).Concat(earP.Plays(Crackle, outTick + 1))); // the crackle stopped with the fire

        _output.WriteLine("the engineer's client heard: " + string.Join(", ", earP.Heard.Where(h => h.E.File != null).GroupBy(h => h.E.File).Select(g => $"{g.Key} x{g.Count()}")));
        _output.WriteLine("the listener's client heard: " + string.Join(", ", earQ.Heard.Where(h => h.E.File != null).GroupBy(h => h.E.File).Select(g => $"{g.Key} x{g.Count()}")));
        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, "nothing the players did ran into a runtime: " + string.Join(" | ", runtimes)); // nothing the players did ran into a runtime
    }
}
