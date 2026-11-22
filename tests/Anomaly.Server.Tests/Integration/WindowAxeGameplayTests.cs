using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
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
/// M5 slice 5 through the real server on MetaStation, with a headless Godot client (<see cref="PlaySession"/>): the M4 breach
/// redone with a real axe and real glass. A player on the bridge breaks the glass of the fire axe cabinet (115,134; locked,
/// fireaxe.dm) with a toolbox (glasshit each hit, the client sees the cracks glass1..glass4, glassbr3 and two shards), takes the
/// axe, walks to the Fitness Room and wields it (Z: the two handed component, "fireaxe1", the offhand): one hit destroys the
/// reinforced window to space (136,184; fireaxe.dm:68 atom_destruction: shards, rods, the grille stays), the room vents through
/// the grille (the client sees the air stream west, the space wind is heard) and the neighbouring window's smoothing lets the
/// hole go. Two sheets of reinforced glass on the grille make an unanchored window (grille.dm:248-305) that does not seal and
/// that its neighbour does not join; a screwdriver fastens it (window.dm:228) and seals the hole, and the neighbour joins it
/// again; an air canister's valve, opened from its window, refills the room.
/// </summary>
public class WindowAxeGameplayTests
{
    private const int Second = 20; // ticks per second
    private const int HoleX = 136, HoleY = 184; // the reinforced window to space
    private const int NeighbourX = 136, NeighbourY = 185; // the window north of it
    private const int RoomX = 139, RoomY = 184; // the middle of the room
    private const int CabinetX = 115, CabinetY = 134; // the bridge's fire axe cabinet (on the south wall)

    private const string GlassHit = "sound/effects/glass/glasshit.ogg";
    private const string CabinetBreak = "sound/effects/glass/glassbr3.ogg";
    private const string Shatter = "sound/effects/glass/glassbr";
    private const string SpaceWind = "sound/effects/space_wind.ogg";
    private const string Screwdriver = "sound/items/tools/screwdriver";

    private readonly ITestOutputHelper _output;

    public WindowAxeGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static T Find<T>(RealHeadlessWorld w, int x, int y) where T : class
        => In(w, () => Dm.each_cast<atom>(Builtins.locate(x, y, 1).contents).OfType<T>().FirstOrDefault());

    private static int Count<T>(RealHeadlessWorld w, int x, int y)
        => In(w, () => Enumerable.Range(x - 12, 13).SelectMany(cx => Enumerable.Range(y - 3, 7).SelectMany(cy => Dm.each_cast<atom>(Builtins.locate(cx, cy, 1).contents))).OfType<T>().Count());

    private static float Kpa(RealHeadlessWorld w, int x, int y)
        => In(w, () => Builtins.locate(x, y, 1) is turf_open t && t.air != null ? Dm.num(t.air.return_pressure()) : -1f);

    private static void Put(RealHeadlessWorld w, mob m, int x, int y)
    {
        using (w.World.Enter())
            Waiting.NoSleep(m.forceMove(Builtins.locate(x, y, 1)));
    }

    private static obj_item Hand(RealHeadlessWorld w, mob_living_carbon_human me, string typePath, int amount = 0)
    {
        using (w.World.Enter())
        {
            foreach (object held in Dm.each(me.held_items.Copy()))
            {
                if (held is obj_item old)
                    qdel(old);
            }
            Type type = DmTypes.FromPath(typePath);
            obj_item item = (amount > 0 ? Dm.@new(type, me.loc, amount) : Dm.@new(type, me.loc)) as obj_item;
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(item))));
            return item;
        }
    }

    private static List<string> States(ReplicaWorld replica, atom a)
    {
        ReplicaEntity seen = replica.Entities.Single(e => e.Id == a.dm_id);
        return CrewOutfitTests.Icons(replica, seen.State.Appearance).Select(i => i.State).ToList();
    }

    // The client's root icon state of a replicated entity.
    private static string RootState(ReplicaWorld replica, atom a)
    {
        ReplicaEntity seen = replica.Entities.Single(e => e.Id == a.dm_id);
        return replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? replica.ResolveString(look.IconState) : null;
    }

    private static bool Click(RealHeadlessWorld w, RealHeadlessWorld.Player p, atom target, float seconds = 1)
    {
        bool sent = p.Session.Click(new ClickTarget(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None);
        w.Run((int)(seconds * Second));
        return sent;
    }

    // The arm of the window at (x, y) toward the hole, as the client's planner places it from what the replica shows (WindowJunction, D121's joints).
    private static bool ArmTowardHole(ReplicaWorld replica, atom window, int x, int y)
    {
        ReplicaEntity seen = replica.Entities.Single(e => e.Id == window.dm_id);
        string state = replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? replica.ResolveString(look.IconState) : null;
        var plan = new PlanEntity(seen.Id, seen.TypePath, x, y, 1, seen.State.Dir, seen.State.Appearance, 0, 0, default, state);
        var probe = new PlanProbe
        {
            IsWall = (cx, cy, cz) => false,
            Connects = (cx, cy, cz) => (cx, cy) == (HoleX, HoleY) && replica.Entities.Any(e => e.State.X == HoleX && e.State.Y == HoleY && PropKinds.Classify(e.TypePath) is RenderKind.FullWindow or RenderKind.Grille),
            IsFullWindow = (cx, cy, cz) => (cx, cy) == (HoleX, HoleY) && replica.Entities.Any(e => e.State.X == HoleX && e.State.Y == HoleY && PropKinds.Classify(e.TypePath) == RenderKind.FullWindow),
        };
        Assert.True(ChunkPlanner.TryPlace(plan, probe, out Placed placed));
        return (placed.Joints & Joints.South) != 0 && probe.IsFullWindow(HoleX, HoleY, 1);
    }

    [MetaServerFact]
    public void The_bridge_fire_axe_breaks_the_Fitness_window_the_room_vents_and_glass_and_a_screwdriver_seal_it_again()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 17, TimeoutSteps = 9000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<(long Tick, SoundEvent E)>();
        replica.SoundsReceived += events => heard.AddRange(events.Select(e => (w.Ticks, e)));
        List<SoundEvent> Plays(string file, long from) => heard.Where(h => h.Tick >= from && h.E.Kind == SoundKind.Play && h.E.File != null && h.E.File.StartsWith(file, StringComparison.Ordinal)).Select(h => h.E).ToList();

        // ---- 1. The bridge's fire axe cabinet: locked, its glass broken by a toolbox, the axe taken.
        var cabinet = Find<obj_structure_fireaxecabinet>(w, CabinetX, CabinetY);
        Assert.NotNull(cabinet);
        var axe = In(w, () => cabinet.held_item_value as obj_item_fireaxe);
        Assert.NotNull(axe);
        Assert.Same(axe, In(w, () => GLOB.bridge_axe)); // fireaxe.dm:40: the bridge's axe
        Put(w, me, CabinetX - 1, CabinetY);
        w.Run(Second);
        List<string> closedLook = States(replica, cabinet);
        _output.WriteLine("the cabinet as the client sees it: " + string.Join(", ", closedLook));
        Assert.Contains("axe", closedLook);
        Assert.Contains("glass", closedLook);
        Assert.Contains("locked", closedLook);

        Hand(w, me, "/obj/item/storage/toolbox/mechanical");
        p.Session.ToggleCombatMode();
        w.Run(Second);
        long hitsFrom = w.Ticks;
        var cracks = new List<string>();
        int hits = 0;
        while (!In(w, () => Dm.truthy(cabinet.broken)) && hits < 60)
        {
            Click(w, p, cabinet);
            hits++;
            string glass = States(replica, cabinet).FirstOrDefault(s => s.StartsWith("glass", StringComparison.Ordinal));
            if (glass != null && (cracks.Count == 0 || cracks[^1] != glass))
                cracks.Add(glass);
        }
        _output.WriteLine($"{hits} toolbox hits broke the cabinet's glass; the client saw {string.Join(", ", cracks)}");
        Assert.True(In(w, () => Dm.truthy(cabinet.broken)));
        Assert.Equal("glass4", cracks[^1]);
        Assert.Contains("glass3", cracks);
        Assert.True(Plays(GlassHit, hitsFrom).Count >= hits - 1, "each hit is heard");
        Assert.Single(Plays(CabinetBreak, hitsFrom)); // fireaxe.dm:139
        Assert.Equal(2, In(w, () => Dm.each_cast<atom>(Builtins.locate(CabinetX, CabinetY, 1).contents).OfType<obj_item_shard>().Count()));

        p.Session.ToggleCombatMode();
        p.Session.Keybinding(KeybindingCode.DropItem);
        w.Run(Second);
        Assert.Null(In(w, () => me.get_active_held_item()));
        Click(w, p, cabinet); // attack_hand: the broken cabinet gives the axe (fireaxe.dm:157)
        Assert.Same(axe, In(w, () => me.get_active_held_item()));
        Assert.DoesNotContain("axe", States(replica, cabinet));

        // ---- 2. At the Fitness window, wielded: one hit destroys it.
        var window = Find<obj_structure_window_reinforced_fulltile>(w, HoleX, HoleY);
        var neighbour = Find<obj_structure_window_reinforced_fulltile>(w, NeighbourX, NeighbourY);
        var grille = Find<obj_structure_grille>(w, HoleX, HoleY);
        Assert.NotNull(window);
        Assert.NotNull(neighbour);
        Assert.NotNull(grille);
        Put(w, me, HoleX + 1, HoleY);
        // The engineer wears magboots with the mag-pulse on (the M4c suit test puts them on by hand): without them the wind through
        // the hole pushes him off the window (living.dm:1113 experience_pressure_difference) and interrupts every do_after.
        using (w.World.Enter())
        {
            if (me.shoes != null)
                qdel(me.shoes);
            var boots = Dm.@new<obj_item_clothing_shoes_magboots>(me.loc);
            Waiting.NoSleep(boots.attack_self(me));
            Waiting.NoSleep(me.equip_to_slot_or_del(boots, Defines.ITEM_SLOT_FEET));
        }
        w.Run(Second);
        Assert.True(In(w, () => HAS_TRAIT(me, Defines.TRAIT_NEGATES_GRAVITY)));
        Assert.Equal(101.3f, Kpa(w, RoomX, RoomY), 0.5f);
        string neighbourJoined = RootState(replica, neighbour);
        _output.WriteLine($"the neighbouring window as the client sees it: {neighbourJoined}; the hole's window: {RootState(replica, window)}");
        Assert.True(ArmTowardHole(replica, neighbour, NeighbourX, NeighbourY), "the client joins the two windows (their smoothing junctions)");

        p.Session.Keybinding(KeybindingCode.ActivateInhand); // Z: wield (twohanded.dm:198)
        w.Run(Second / 2);
        Assert.True(In(w, () => HAS_TRAIT(axe, Defines.TRAIT_WIELDED)));
        Assert.Equal(24f, In(w, () => axe.force));
        Assert.IsType<obj_item_offhand>(In(w, () => me.get_inactive_held_item()));
        // the wielded icon state the client draws in the hand slot (tg's slot vis_contents, protocol 12), and the offhand in the other
        ScreenEntry axeSlot = replica.Screen.Values.Single(e => e.VisChildren != null && e.VisChildren.Contains(axe.dm_id));
        ScreenChild axeShown = axeSlot.VisChildren.Items.Single(c => c.Id == axe.dm_id);
        Assert.True(replica.TryGetAppearance(axeShown.Appearance, out AppearanceEntry axeLook));
        Assert.Equal("fireaxe1", replica.ResolveString(axeLook.IconState));
        long offhandId = In(w, () => ((obj_item)me.get_inactive_held_item()).dm_id);
        Assert.Contains(replica.Screen.Values, e => e.VisChildren != null && e.VisChildren.Contains(offhandId));

        p.Session.ToggleCombatMode();
        w.Run(Second / 2);
        long brokeAt = w.Ticks;
        Click(w, p, window);
        Assert.True(In(w, () => QDELETED(window)), "one wielded hit destroys the window");
        Assert.DoesNotContain(replica.Entities, e => e.Id == window.dm_id);
        Assert.NotEmpty(Plays(Shatter, brokeAt));
        Assert.Equal(2, Count<obj_item_shard>(w, HoleX, HoleY));
        Assert.True(Count<obj_item_stack_rods>(w, HoleX, HoleY) >= 1);
        Assert.False(In(w, () => QDELETED(grille)), "the grille stays");

        // ---- 3. The room vents through the grille.
        int westFlows = 0;
        for (int t = 0; t < 25 * Second; t++)
        {
            w.Run(1);
            westFlows = Math.Max(westFlows, replica.AirflowCells.Count(c => c.X >= HoleX && c.X <= 141 && c.Y >= 183 && c.Y <= 185 && Airflow.DirOf(c.Cell) == AirflowDir.West));
        }
        float vented = Kpa(w, RoomX, RoomY);
        _output.WriteLine($"25 s after the hit: {vented:0.0} kPa, most westward airflow cells {westFlows}, wind heard {Plays(SpaceWind, brokeAt).Count} times; the neighbour now {RootState(replica, neighbour)}");
        Assert.True(vented < 60f, "the room vents: " + vented);
        Assert.True(westFlows >= 3, "the client sees the air streaming west: " + westFlows);
        Assert.NotEmpty(Plays(SpaceWind, brokeAt));
        Assert.False(ArmTowardHole(replica, neighbour, NeighbourX, NeighbourY), "the neighbour lets the hole go");

        // ---- 4. Reinforced glass on the grille: an unanchored window that does not seal and is not joined.
        p.Session.ToggleCombatMode();
        Put(w, me, HoleX + 1, HoleY);
        p.Session.Keybinding(KeybindingCode.DropItem); // dropping unwields (twohanded.dm:181 on_drop)
        w.Run(Second / 2);
        Assert.False(In(w, () => HAS_TRAIT(axe, Defines.TRAIT_WIELDED)), "the axe, dropped, is unwielded");
        Assert.Equal(5f, In(w, () => axe.force));
        Assert.Null(In(w, () => me.get_inactive_held_item()));
        Hand(w, me, "/obj/item/stack/sheet/rglass", 10);
        Click(w, p, grille, 3);
        var built = Find<obj_structure_window_reinforced_fulltile>(w, HoleX, HoleY);
        Assert.NotNull(built);
        Assert.False(In(w, () => Dm.truthy(built.anchored)));
        Assert.Contains(replica.Entities, e => e.Id == built.dm_id);
        w.Run(2 * Second);
        Assert.False(ArmTowardHole(replica, neighbour, NeighbourX, NeighbourY), "an unanchored window is not smoothed with (icon_smoothing.dm:201)");
        Assert.True(Kpa(w, RoomX, RoomY) < 60f);

        // ---- 5. The screwdriver fastens it: sealed, joined.
        Hand(w, me, "/obj/item/screwdriver");
        long screwedFrom = w.Ticks;
        int tries = 0;
        while (!In(w, () => Dm.truthy(built.anchored)) && tries < 3)
        {
            Put(w, me, HoleX + 1, HoleY);
            Click(w, p, built, 4);
            tries++;
        }
        _output.WriteLine($"the screwdriver fastened the window on try {tries} at {Kpa(w, RoomX, RoomY):0.0} kPa");
        Assert.True(In(w, () => Dm.truthy(built.anchored)), "the screwdriver fastens the frame to the floor");
        Assert.NotEmpty(Plays(Screwdriver, screwedFrom));
        w.Run(Second);
        _output.WriteLine($"the new window {RootState(replica, built)}, the neighbour {RootState(replica, neighbour)}");
        Assert.True(ArmTowardHole(replica, neighbour, NeighbourX, NeighbourY), "the fastened window is joined again");

        // ---- 6. An air canister refills the sealed room (its tgui valve).
        w.Host.EnqueueConsoleLine("spawn /obj/machinery/portable_atmospherics/canister/air 141 185 1");
        w.Run(Second);
        var canister = Find<obj_machinery_portable_atmospherics_canister_air>(w, 141, 185);
        Assert.NotNull(canister);
        Put(w, me, 140, 185);
        using (w.World.Enter())
            foreach (object held in Dm.each(me.held_items.Copy()))
                if (held is obj_item o) qdel(o);
        Click(w, p, canister);
        UiWindowState ui = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("Canister", ui.Interface);
        Assert.True(p.Session.UiAct(ui.Window, "valve"));
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(canister.valve_open)));
        float refilled = 0;
        for (int s = 0; s < 90 && refilled < 90f; s++)
        {
            w.Run(Second);
            refilled = Kpa(w, RoomX, RoomY);
        }
        _output.WriteLine($"refilled to {refilled:0.0} kPa; at the new window {Kpa(w, HoleX + 1, HoleY):0.0} kPa");
        Assert.True(refilled >= 90f, "the canister refills the sealed room: " + refilled);
    }
}
