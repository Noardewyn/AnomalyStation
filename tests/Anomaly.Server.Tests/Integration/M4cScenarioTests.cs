using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// ROADMAP M4c's demo check through the real server on MetaStation, end to end (suit up, cycle the airlock, float to the target
/// tile with the jetpack, anchor with boots, come back): a headless Godot client (<see cref="PlaySession"/>) clicks and holds keys
/// as a player does. With the Captain's ID (EVA access; every player joins as an assistant until the jobs, M9/M10) it opens a
/// suit storage unit in EVA Storage through tg's radial and takes the EVA suit, helmet and breath mask, an oxygen tank from the
/// dispenser, the magboots and a CO2 jetpack from behind the EVA windoors, wears them with E and HUD clicks, opens the tank's
/// valve with an alt-click and turns the suit's regulator on with its action button. At the Auxiliary Airlock it walks into the
/// chamber and clicks the bolted outer door: tg's cycling pump empties the chamber and opens the outer doors. The jetpack's
/// buttons light it and its stabilizer; it flies to the target tile (44,113) and the stabilizer holds it there. tg's magboots do
/// not hold over open space (`_species.dm:1365`, isgroundlessturf), so it flies on to the nearest weightless floor, the airless
/// plating by the space hut (41,95), where the magboots' button anchors it: a throw does not push it, it has gravity and its
/// steps sound tg's rigstep. Magboots off, it flies back, opens the outer door by flying into it, clicks the bolted inner door
/// (the pump fills the chamber from the distro and opens it) and takes the helmet off. Over the run: no oxygen loss, no pressure
/// or cold damage, the oxygen tank lost pressure, and the client saw the worn overlays, the action buttons, the ion trail and the
/// pump's states, and heard the valve, the bolts, the rigsteps and the breathing loop.
/// </summary>
public class M4cScenarioTests
{
    private const int Second = 20; // ticks per second
    private const string InternalsOn = "sound/items/internals/internals_on.ogg";
    private const string BoltsUp = "sound/machines/airlock/boltsup.ogg";
    private const string Chonk = "sound/items/modsuit/rigstep_chonk.ogg";
    private const string Breathing = "sound/mobs/humanoids/breathing/internals_breathing";
    private const string TrailPath = "/obj/effect/particle_effect/ion_trails";

    private readonly ITestOutputHelper _output;

    public M4cScenarioTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Put(RealHeadlessWorld w, mob m, int x, int y)
    {
        using (w.World.Enter())
            Waiting.NoSleep(m.forceMove(locate(x, y, m.z)));
        w.Run(Second);
    }

    private static T Find<T>(RealHeadlessWorld w, int x, int y, int z) where T : class
        => In(w, () => Dm.each_cast<atom>(((turf)locate(x, y, z)).contents).OfType<T>().FirstOrDefault());

    private static List<string> States(ReplicaWorld replica, long id)
        => CrewOutfitTests.Icons(replica, replica.Entities.Single(e => e.Id == id).State.Appearance).Select(i => i.State).ToList();

    private static List<string> ScreenStates(ReplicaWorld replica, long id)
        => CrewOutfitTests.Icons(replica, replica.Screen.Values.Single(e => e.Id == id).Appearance).Select(i => i.State).ToList();

    private static string State(ReplicaWorld replica, atom a)
        => replica.Entities.Single(e => e.Id == a.dm_id).State.Appearance is { } ap ? CrewOutfitTests.Icons(replica, ap).FirstOrDefault().State : null;

    private static void Click(RealHeadlessWorld w, RealHeadlessWorld.Player p, atom target, ClickButton button = ClickButton.Left)
    {
        Assert.True(p.Session.Click(target.dm_id, button, ClickModifiers.None));
        w.Run(Second);
    }

    private static void ClickScreen(RealHeadlessWorld w, RealHeadlessWorld.Player p, long id, ClickModifiers modifiers = ClickModifiers.None)
    {
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, id, 0, 0, 0, 16, 16), ClickButton.Left, modifiers));
        w.Run(Second);
    }

    private static long SlotId(RealHeadlessWorld w, mob_living_carbon_human me, object slot)
        => In(w, () => ((atom)me.hud_used.screen_objects[Dm.text("item_slot:[]", slot)]).dm_id);

    private static long ButtonOf<T>(RealHeadlessWorld w, mob_living_carbon_human me, obj_item item) where T : datum_action
        => In(w, () => ((atom)Dm.each_cast<datum_action>(item.actions).First(a => a is T).viewers[me.hud_used]).dm_id);

    private static void QuickEquip(RealHeadlessWorld w, RealHeadlessWorld.Player p)
    {
        p.Session.Keybinding(KeybindingCode.QuickEquip);
        w.Run(Second);
    }

    // The client picks the slice named name of the radial it draws (the newest open menu of its replica).
    private void PickSlice(RealHeadlessWorld w, RealHeadlessWorld.Player p, string name)
    {
        RadialMenuMessage menu = p.Session.Replica.TopRadial;
        Assert.NotNull(menu);
        int element = menu.Elements.FindIndex(e => e.Name == name) + 1;
        _output.WriteLine($"radial #{menu.MenuId}: {string.Join(", ", menu.Elements.Select(e => e.Name))}; picking {name}");
        Assert.True(element > 0, name + " is not on the radial");
        Assert.True(p.Session.RadialPick(menu.MenuId, element));
        w.Run(Second / 2);
    }

    private static (int X, int Y) Tile(RealHeadlessWorld w, mob me) => In(w, () => ((int)me.x, (int)me.y));

    // Godot's yaw for a world direction (MetaWalkTests: 0 faces north, +90 degrees west).
    private static double YawOf(int dx, int dy) => Math.Atan2(-dx, dy);

    // Holds the forward key toward each waypoint in turn (the view turned toward it every tick, as a player steers), tile by tile;
    // on the last one it lets go and, like S6's flight, steers back if a step already sent carries it on, until it has stayed
    // there for half a second. Returns the tiles passed. Watch runs every tick.
    private List<(int X, int Y)> Steer(RealHeadlessWorld w, RealHeadlessWorld.Player p, mob me, Action<long> watch, int maxTicks, params (int X, int Y)[] waypoints)
    {
        var path = new List<(int X, int Y)> { Tile(w, me) };
        int next = 0, still = 0;
        for (int t = 0; t < maxTicks && still < Second / 2; t++)
        {
            (int X, int Y) at = Tile(w, me);
            if (at != path[^1])
                path.Add(at);
            while (next < waypoints.Length && at == waypoints[next])
                next++;
            (int X, int Y) to = waypoints[Math.Min(next, waypoints.Length - 1)];
            if (next >= waypoints.Length && at == to)
            {
                p.Keys = MoveKeys.None;
                still++;
            }
            else
            {
                p.Yaw = YawOf(Math.Sign(to.X - at.X), Math.Sign(to.Y - at.Y));
                p.Keys = MoveKeys.Forward;
                still = 0;
            }
            w.Run(1, watch);
        }
        p.Keys = MoveKeys.None;
        (int X, int Y) end = Tile(w, me);
        if (end != path[^1])
            path.Add(end);
        _output.WriteLine("steered: " + string.Join(" ", path));
        return path;
    }

    [MetaServerFact]
    public void A_captain_suits_up_cycles_out_flies_to_the_target_anchors_with_magboots_and_comes_back()
    {
        var config = new ServerConfig { Seed = 41, Console = false, StatusEveryTicks = 100000, Map = "metastation", MapsFolder = MetaServer.Folder(), DevCommands = true };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 43, TimeoutSteps = 20000 }, config, strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        var allHeard = new List<SoundEvent>();
        replica.SoundsReceived += events => allHeard.AddRange(events);
        int z = In(w, () => (int)me.z);
        int bootRuntimes = w.World.RuntimeErrorCount;

        // Damage over the whole run, sampled every tick of every step below.
        float maxOxy = 0, maxBrute = 0, maxBurn = 0, minTemp = float.MaxValue;
        var trails = new HashSet<long>();
        var fades = new HashSet<long>();
        var pumpStates = new HashSet<string>();
        obj_machinery_atmospherics_components_unary_airlock_pump pump = Find<obj_machinery_atmospherics_components_unary_airlock_pump>(w, 39, 119, z);
        Assert.NotNull(pump);
        void Watch(long _)
        {
            (float oxy, float brute, float burn, float temp) = In(w, () => (Dm.num(me.get_oxy_loss()), Dm.num(me.get_brute_loss()), Dm.num(me.get_fire_loss()), Dm.num(me.bodytemperature)));
            maxOxy = Math.Max(maxOxy, oxy);
            maxBrute = Math.Max(maxBrute, brute);
            maxBurn = Math.Max(maxBurn, burn);
            minTemp = Math.Min(minTemp, temp);
            foreach (ReplicaEntity e in replica.Entities.Where(e => e.TypePath == TrailPath))
            {
                trails.Add(e.Id);
                if (!e.State.FlickAppearance.IsNone && replica.TryGetAppearance(e.State.FlickAppearance, out AppearanceEntry look)
                    && replica.ResolveString(look.IconState) == "ion_fade")
                    fades.Add(e.Id);
            }
            if (replica.Entities.Any(e => e.Id == pump.dm_id))
                pumpStates.Add(State(replica, pump));
        }
        void Run(int ticks) => w.Run(ticks, Watch);

        // ---- 1. Suit up in E.V.A. Storage.
        // The Captain's ID in the ID slot (tg's captain trim: every station access, EVA among them); the assistant's own card goes to
        // a pocket (the thing thrown at the magboots step).
        obj_item oldId = null;
        In(w, () =>
        {
            oldId = me.wear_id as obj_item;
            if (oldId != null)
            {
                Waiting.NoSleep(me.temporarilyRemoveItemFromInventory(oldId, force: Dm.TRUE));
                Assert.True(Dm.truthy(Waiting.NoSleep(me.equip_to_slot_if_possible(oldId, Defines.ITEM_SLOT_LPOCKET))), "the old card goes to a pocket");
            }
            var card = Dm.@new<obj_item_card_id_advanced_gold_captains_spare>(me.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(me.equip_to_slot_if_possible(card, Defines.ITEM_SLOT_ID))), "the Captain's ID goes on");
            return 0;
        });
        Assert.NotNull(oldId);

        // The suit storage unit at (91,124): its radial (open), then the suit, the helmet and the mask, each worn with E.
        var unit = Find<obj_machinery_suit_storage_unit_standard_unit>(w, 91, 124, z);
        Assert.NotNull(unit);
        Put(w, me, 92, 124);
        Click(w, p, unit);
        PickSlice(w, p, "open");
        Assert.True(In(w, () => Dm.truthy(unit.state_open)));
        obj_item suit = In(w, () => unit.suit_value), helmet = In(w, () => unit.helmet_value), mask = In(w, () => unit.mask_value);
        PickSlice(w, p, "suit");
        QuickEquip(w, p);
        Assert.Same(suit, In(w, () => me.wear_suit));
        PickSlice(w, p, "helmet");
        QuickEquip(w, p);
        Assert.Same(helmet, In(w, () => me.head));
        PickSlice(w, p, "mask");
        QuickEquip(w, p);
        Assert.Same(mask, In(w, () => me.wear_mask));
        Assert.True(p.Session.RadialPick(replica.TopRadial.MenuId, 0)); // the centre closes the menu
        Run(Second);
        Assert.Null(replica.TopRadial);

        // The oxygen tank from the dispenser (93,121) with a right click; its valve opened with an alt-click in hand (internals on,
        // tank_types.dm:15), then hung on the suit storage slot with a click on that slot.
        Put(w, me, 94, 121);
        var dispenser = Find<obj_structure_tank_dispenser_oxygen>(w, 93, 121, z);
        Click(w, p, dispenser, ClickButton.Right);
        var tank = In(w, () => me.get_active_held_item()) as obj_item_tank_internals_oxygen;
        Assert.NotNull(tank);
        heard.Clear();
        ClickScreen(w, p, tank.dm_id, ClickModifiers.Alt);
        Assert.Same(tank, In(w, () => me.@internal));
        Assert.Contains(heard, e => e.File == InternalsOn);
        ClickScreen(w, p, SlotId(w, me, Defines.ITEM_SLOT_SUITSTORE));
        Assert.Same(tank, In(w, () => me.s_store));
        float tankStart = In(w, () => Dm.num(tank.air_contents.return_pressure()));

        // The old shoes off (the inventory opened with its button, a click on the shoes, Q), the magboots from behind the
        // "Magboot Storage" windoor (95,122) onto the feet with E.
        obj_item oldShoes = In(w, () => me.shoes);
        if (oldShoes != null)
        {
            if (!In(w, () => Dm.truthy(me.hud_used.inventory_shown))) // the shoe slot is in the toggleable inventory (human.dm:15)
                ClickScreen(w, p, In(w, () => ((atom)me.hud_used.screen_objects[Defines.HUD_HUMAN_TOGGLE_INVENTORY]).dm_id));
            ClickScreen(w, p, oldShoes.dm_id);
            Assert.Same(oldShoes, In(w, () => me.get_active_held_item()));
            p.Session.Keybinding(KeybindingCode.DropItem);
            Run(Second);
            Assert.Null(In(w, () => me.shoes));
        }
        Put(w, me, 94, 122);
        var magWindoor = Find<obj_machinery_door_window>(w, 95, 122, z);
        Click(w, p, magWindoor);
        Run(Second);
        Assert.False(In(w, () => Dm.truthy(magWindoor.density)), "the windoor opens for EVA access");
        var boots = Find<obj_item_clothing_shoes_magboots>(w, 95, 122, z);
        Click(w, p, boots);
        QuickEquip(w, p);
        Assert.Same(boots, In(w, () => me.shoes));

        // The jetpack from behind the "Jetpack Storage" windoor (95,120) onto the back with E. The backpack comes off first: tg
        // takes a worn backpack off by dragging it to a hand (MouseDrop, protocol version 17: the client's Drop of the item drawn in
        // the back slot onto the empty inactive hand slot), then that hand drops it (X, Q, X).
        var bag = In(w, () => me.back as obj_item);
        Assert.NotNull(bag);
        int inactive = In(w, () => (int)me.active_hand_index) == 1 ? 2 : 1;
        Assert.Null(In(w, () => me.get_item_for_held_index(inactive)));
        long handSlot = In(w, () => ((atom)me.hud_used.screen_objects[Dm.text("hand_slot:[]", inactive)]).dm_id);
        Assert.True(p.Session.Drop(new DragRelease(new ClickTarget(TargetKind.Screen, bag.dm_id, 0, 0, 0, 16, 16), new ClickTarget(TargetKind.Screen, handSlot, 0, 0, 0, 16, 16),
            ClickButton.Left, ClickModifiers.None, 4)));
        Run(Second);
        Assert.Null(In(w, () => me.back));
        Assert.Same(bag, In(w, () => me.get_item_for_held_index(inactive)));
        p.Session.Keybinding(KeybindingCode.SwapHands);
        Run(5);
        p.Session.Keybinding(KeybindingCode.DropItem);
        Run(5);
        p.Session.Keybinding(KeybindingCode.SwapHands);
        Run(5);
        Assert.Null(In(w, () => me.get_item_for_held_index(inactive)));
        Put(w, me, 94, 120);
        var jetWindoor = Find<obj_machinery_door_window>(w, 95, 120, z);
        Click(w, p, jetWindoor);
        Run(Second);
        var jet = Find<obj_item_tank_jetpack_carbondioxide>(w, 95, 120, z);
        Click(w, p, jet);
        Assert.Same(jet, In(w, () => me.get_active_held_item()));
        QuickEquip(w, p);
        Assert.Same(jet, In(w, () => me.back));

        // The suit's thermal regulator on with its action button (toggle_spacesuit, _spacesuits.dm:327).
        long regulator = ButtonOf<datum_action_item_action_toggle_spacesuit>(w, me, suit);
        ClickScreen(w, p, regulator);
        Assert.Equal(1f, In(w, () => ((obj_item_clothing_suit_space)suit).thermal_on));
        Assert.Contains("thermal_on", ScreenStates(replica, regulator));

        // What the client sees: the suit and helmet on the body (space, twice), the jetpack on the back, and the buttons.
        List<string> body = States(replica, me.dm_id);
        _output.WriteLine("the suited body as the client sees it: " + string.Join(", ", body));
        Assert.True(body.Count(s => s == "space") >= 2, "the EVA suit and helmet are drawn on the body");
        Assert.Contains("jetpack-black", body);
        long internalsButton = ButtonOf<datum_action_item_action_set_internals>(w, me, tank);
        long magButton = ButtonOf<datum_action_item_action_toggle>(w, me, boots);
        long jetToggle = ButtonOf<datum_action_item_action_toggle_jetpack>(w, me, jet);
        long jetStabilizer = ButtonOf<datum_action_item_action_jetpack_stabilization>(w, me, jet);
        foreach (long button in new[] { regulator, internalsButton, magButton, jetToggle, jetStabilizer })
            Assert.Contains(replica.Screen.Values, e => e.Id == button);
        _output.WriteLine($"buttons on screen: regulator {string.Join("/", ScreenStates(replica, regulator))}, internals {string.Join("/", ScreenStates(replica, internalsButton))}, magboots {string.Join("/", ScreenStates(replica, magButton))}, jetpack {string.Join("/", ScreenStates(replica, jetToggle))}");
        Assert.Contains("template_active", ScreenStates(replica, internalsButton)); // the tank is the internals

        // ---- 2. The Auxiliary Airlock: walk through the bolted-open inner doors (38,120) into the chamber (38,119).
        var inner = new[] { Find<obj_machinery_door_airlock_external>(w, 38, 120, z), Find<obj_machinery_door_airlock_external>(w, 39, 120, z) };
        var outer = new[] { Find<obj_machinery_door_airlock_external>(w, 38, 118, z), Find<obj_machinery_door_airlock_external>(w, 39, 118, z) };
        Assert.All(outer, d => Assert.True(In(w, () => Dm.truthy(d.density) && Dm.truthy(d.locked)), "the outer doors are bolted shut"));
        Put(w, me, 38, 121);
        Steer(w, p, me, Watch, 10 * Second, (38, 119));
        Assert.Equal((38, 119), Tile(w, me));
        Assert.Contains("lights_bolts", States(replica, outer[0].dm_id));

        // ---- 3. Cycle out: a click on the bolted outer door; the pump siphons the chamber below its allowed error, then the outer
        // doors open (airlock.dm:1329, airlock_pump.dm:259).
        heard.Clear();
        pumpStates.Clear();
        float allowed = In(w, () => pump.allowed_pressure_error);
        float lowest = float.MaxValue;
        bool outerOpenedBeforeEmpty = false;
        Assert.True(p.Session.Click(outer[0].dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(15 * Second, t =>
        {
            Watch(t);
            float chamber = In(w, () => Dm.num(((turf_open)locate(38, 119, z)).air.return_pressure()));
            lowest = Math.Min(lowest, chamber);
            if (In(w, () => !Dm.truthy(outer[0].density)) && lowest > allowed + 0.01f)
                outerOpenedBeforeEmpty = true;
        });
        _output.WriteLine($"cycle out: lowest chamber pressure {lowest} kPa (allowed error {allowed}); pump states {string.Join(", ", pumpStates)}; the player at {Tile(w, me)}");
        Assert.True(lowest <= allowed + 0.01f, "the pump emptied the chamber: " + lowest);
        Assert.False(outerOpenedBeforeEmpty, "the outer door opened only after the chamber was empty");
        Assert.Contains("vent_in", pumpStates);
        Assert.All(outer, d => Assert.False(In(w, () => Dm.truthy(d.density)), "the outer doors opened"));
        Assert.All(inner, d => Assert.True(In(w, () => Dm.truthy(d.density) && Dm.truthy(d.locked)), "the inner doors are bolted shut"));
        Assert.Contains(heard, e => e.File == BoltsUp);

        // ---- 4. The jetpack: its toggle button lights it, the stabilization button holds the flier; fly to the target tile (44,113).
        ClickScreen(w, p, jetToggle);
        Assert.Equal(1f, In(w, () => jet.on));
        Assert.Contains("jetpack-black-on", ScreenStates(replica, jetToggle));
        Assert.Contains("jetpack-black-on", States(replica, me.dm_id));
        ClickScreen(w, p, jetStabilizer);
        Assert.Equal(1f, In(w, () => jet.stabilize));
        float co2 = In(w, () => Dm.num(jet.air_contents.total_moles()));
        (int X, int Y) target = (44, 113);
        Steer(w, p, me, Watch, 30 * Second, (38, 117), target);
        Assert.Equal(target, Tile(w, me));
        Assert.Equal(MotionDriver.ZeroG, w.ServerState(p).Driver);
        Run(Second);
        Assert.Equal(target, Tile(w, me)); // the stabilizer holds the flier on the target tile
        Assert.Null(In(w, () => me.drift_handler));
        ReplicaEntity own = replica.Entities.Single(e => e.Id == me.dm_id);
        Assert.Equal(target, ((int)own.State.X, (int)own.State.Y));
        float co2AtTarget = In(w, () => Dm.num(jet.air_contents.total_moles()));
        _output.WriteLine($"at the target {target}: CO2 {co2} -> {co2AtTarget} mol; trail entities seen {trails.Count}, with ion_fade {fades.Count}");
        Assert.True(co2AtTarget < co2, "the flight used the jetpack's CO2");
        Assert.True(trails.Count >= 3 && fades.Count >= 3, "the client saw the ion trail and its flick");

        // ---- 5. Anchor with the magboots. tg's magboots negate gravity only on ground (_species.dm:1365, !isgroundlessturf), so
        // over open space they do not hold (as tg): fly on to the nearest weightless floor, the airless plating by the space hut
        // (41,95, /area/space/nearstation), coming in from the west between its railings.
        Assert.False(In(w, () => Dm.truthy(me.has_gravity())), "the target tile is weightless");
        Steer(w, p, me, Watch, 40 * Second, (39, 100), (39, 95), (41, 95));
        Assert.Equal((41, 95), Tile(w, me));
        Assert.False(In(w, () => Dm.truthy(me.has_gravity())), "the airless plating has no gravity of its own");
        ClickScreen(w, p, magButton);
        Assert.Equal(1f, In(w, () => ((obj_item_clothing_shoes_magboots)boots).magpulse));
        Assert.True(In(w, () => Dm.truthy(me.has_gravity())), "the magboots give gravity on the floor");
        Assert.Contains("magboots1", ScreenStates(replica, magButton));
        // The jetpack off with its button: the magboots alone hold the player.
        ClickScreen(w, p, jetToggle);
        Assert.Equal(0f, In(w, () => jet.on));
        Assert.NotEqual(MotionDriver.ZeroG, w.ServerState(p).Driver);

        // A throw (the old card from the pocket, throw mode, a click on a tile to the west): no push, no drift.
        ClickScreen(w, p, oldId.dm_id);
        Assert.Same(oldId, In(w, () => me.get_active_held_item()));
        p.Session.Keybinding(KeybindingCode.ToggleThrowMode);
        Run(5);
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Turf, 0, 36, 95, z), ClickButton.Left, ClickModifiers.None));
        Run(2 * Second);
        Assert.NotSame(me, In(w, () => oldId.loc));
        Assert.Null(In(w, () => me.drift_handler));
        Assert.Equal((41, 95), Tile(w, me));
        _output.WriteLine($"the card thrown to {In(w, () => ((int)oldId.x, (int)oldId.y))}; the player stays at {Tile(w, me)}");

        // A step east and back on the boots: tg's rigstep (shoe_footstep, every other step).
        heard.Clear();
        Steer(w, p, me, Watch, 5 * Second, (42, 95), (41, 95));
        Assert.Equal((41, 95), Tile(w, me));
        Assert.Contains(heard, e => e.File == Chonk);

        // ---- 6. Magboots off, the jetpack on, fly back to the outer door; fly into it (it opens: unbolted after the cycle) and on
        // into the chamber; a click on the bolted inner door fills the chamber from the distro and opens it.
        ClickScreen(w, p, magButton);
        Assert.Equal(0f, In(w, () => ((obj_item_clothing_shoes_magboots)boots).magpulse));
        ClickScreen(w, p, jetToggle);
        Assert.Equal(1f, In(w, () => jet.on));
        Assert.Equal(1f, In(w, () => jet.stabilize));
        Steer(w, p, me, Watch, 40 * Second, (39, 95), (39, 116), (38, 117), (38, 118), (38, 119));
        Assert.Equal((38, 119), Tile(w, me));

        // Meta's distro fills from the air mix after the boot (AirlockCycleGameplayTests): the pump fills only from >= its minimum.
        int waited = 0;
        while (waited < 120 * Second && In(w, () => Dm.num(((datum_gas_mixture)pump.airs[1]).return_pressure())) < 50f)
        {
            Run(Second);
            waited += Second;
        }
        _output.WriteLine($"waited {waited / Second} s for the distro: {In(w, () => Dm.num(((datum_gas_mixture)pump.airs[1]).return_pressure()))} kPa");
        heard.Clear();
        pumpStates.Clear();
        Assert.True(In(w, () => Dm.truthy(inner[0].locked)), "the inner door is bolted");
        Assert.True(p.Session.Click(inner[0].dm_id, ClickButton.Left, ClickModifiers.None));
        Run(15 * Second);
        float refilled = In(w, () => Dm.num(((turf_open)locate(38, 119, z)).air.return_pressure()));
        _output.WriteLine($"cycle in: chamber {refilled} kPa; pump states {string.Join(", ", pumpStates)}");
        Assert.Contains("vent_out", pumpStates);
        Assert.True(refilled >= 95f, "the chamber is pressurized: " + refilled);
        Assert.All(inner, d => Assert.False(In(w, () => Dm.truthy(d.density)), "the inner doors opened"));
        Assert.All(outer, d => Assert.True(In(w, () => Dm.truthy(d.density) && Dm.truthy(d.locked)), "the outer doors are bolted shut"));
        Assert.Contains(heard, e => e.File == BoltsUp);

        // The helmet off: a click on it in its slot with the empty hand.
        Assert.Null(In(w, () => me.get_active_held_item()));
        if (!In(w, () => Dm.truthy(me.hud_used.inventory_shown))) // the head slot is in the toggleable inventory
            ClickScreen(w, p, In(w, () => ((atom)me.hud_used.screen_objects[Defines.HUD_HUMAN_TOGGLE_INVENTORY]).dm_id));
        ClickScreen(w, p, helmet.dm_id);
        Assert.Same(helmet, In(w, () => me.get_active_held_item()));
        Assert.Null(In(w, () => me.head));
        Run(Second);
        List<string> unhelmeted = States(replica, me.dm_id);
        _output.WriteLine("the body without the helmet: " + string.Join(", ", unhelmeted));
        Assert.Equal(1, unhelmeted.Count(s => s == "space"));

        // ---- 7. Over the run.
        float tankEnd = In(w, () => Dm.num(tank.air_contents.return_pressure()));
        PredictionCounters c = p.Counters;
        int breaths = allHeard.Count(e => e.File != null && e.File.StartsWith(Breathing, StringComparison.Ordinal));
        _output.WriteLine($"sounds heard: internals_on {allHeard.Count(e => e.File == InternalsOn)}, boltsup {allHeard.Count(e => e.File == BoltsUp)}, rigstep_chonk {allHeard.Count(e => e.File == Chonk)}, breathing {breaths}");
        Assert.True(breaths >= 3, "the client heard the breathing loop on internals (DV41)");
        _output.WriteLine($"oxy {maxOxy}, brute {maxBrute}, burn {maxBurn}, lowest body temperature {minTemp} K; O2 tank {tankStart} -> {tankEnd} kPa");
        _output.WriteLine($"prediction: reconciliations {c.Reconciliations}, corrections {c.Corrections}, snaps {c.Snaps}, mismatches {c.Mismatches}, max error {c.MaxErrorTiles:0.000} tiles");
        Assert.Equal(0f, maxOxy);
        Assert.Equal(0f, maxBrute); // no pressure damage (and no crushing)
        Assert.Equal(0f, maxBurn); // no cold burns
        Assert.True(minTemp > Defines.BODYTEMP_COLD_DAMAGE_LIMIT, "the body never got cold enough to hurt: " + minTemp);
        Assert.True(tankEnd < tankStart, "the oxygen tank lost pressure");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount); // nothing the player did ran into a runtime
    }
}
