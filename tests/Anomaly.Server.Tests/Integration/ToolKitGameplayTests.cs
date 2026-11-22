using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M7 slice T1 (D164): the electrical tool kit on MetaStation through the real server. A player turns a T-ray scanner on (Z, tg's
/// attack_self) and his client holds an image of every hidden cable, pipe and disposal pipe within three tiles at that thing's tile, at
/// tg's alpha 128, for tg's 8 deciseconds after each pulse (SSobj, every 2 s); a second player standing among cables of his own never
/// gets one (the private block of the holder alone, D40). A player wears a full engineering belt and his client sees the seven
/// tools on it (the belt's overlays of belt_overlays.dmi in the slot's picture); one taken out and the overlay goes. A player takes a
/// multitool out and reads a cable's network with it.
/// </summary>
public class ToolKitGameplayTests(ITestOutputHelper output)
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

    private static RealHeadlessWorld Meta(int seed) => new(new LoopbackOptions { Seed = (ulong)seed, TimeoutSteps = 12000 },
        MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);

    /// <summary>Hidden things per tile of the first level (the T-ray's trait), to find where the pulse has something to show.</summary>
    private static Dictionary<(int X, int Y), int> HiddenPerTile(RealHeadlessWorld w) => In(w, () =>
    {
        var counts = new Dictionary<(int, int), int>();
        foreach (obj o in Dm.each<obj>(Dm.world))
        {
            if (o.loc is not turf t || !Dm.eq(t.z, 1) || !HAS_TRAIT(o, TRAIT_T_RAY_VISIBLE))
                continue;
            var key = ((int)t.x, (int)t.y);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }
        return counts;
    });

    private static int Around(Dictionary<(int X, int Y), int> hidden, int x, int y)
    {
        int sum = 0;
        for (int dx = -3; dx <= 3; dx++)
            for (int dy = -3; dy <= 3; dy++)
                sum += hidden.GetValueOrDefault((x + dx, y + dy));
        return sum;
    }

    // An open floor tile with a hidden cable in it that has the most hidden things around, at least minDistance tiles from `away` (if any).
    private static (int X, int Y) RichSpot(RealHeadlessWorld w, Dictionary<(int X, int Y), int> hidden, (int X, int Y)? away)
    {
        var candidates = In(w, () => Dm.each<obj_structure_cable>(Dm.world)
            .Where(c => c.loc is turf_open_floor_iron t && Dm.eq(t.z, 1) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density)) && HAS_TRAIT(c, TRAIT_T_RAY_VISIBLE))
            .Select(c => ((int)c.x, (int)c.y)).Distinct().ToList());
        return candidates.Where(c => away == null || Math.Max(Math.Abs(c.Item1 - away.Value.X), Math.Abs(c.Item2 - away.Value.Y)) >= 14)
            .OrderByDescending(c => Around(hidden, c.Item1, c.Item2)).ThenBy(c => c.Item1).ThenBy(c => c.Item2).First();
    }

    [MetaServerFact]
    public void A_t_ray_scanner_shows_its_holder_the_hidden_cables_around_him_steadily_and_a_second_player_none()
    {
        using RealHeadlessWorld w = Meta(61);
        RealHeadlessWorld.Player bobP = w.Join("Bob");
        RealHeadlessWorld.Player eveP = w.Join("Eve");
        var bob = (mob_living_carbon_human)w.MobOf(bobP);
        var eve = (mob_living_carbon_human)w.MobOf(eveP);
        var hidden = HiddenPerTile(w);
        (int X, int Y) bobAt = RichSpot(w, hidden, null);
        (int X, int Y) eveAt = RichSpot(w, hidden, bobAt);
        output.WriteLine($"Bob at {bobAt} with {Around(hidden, bobAt.X, bobAt.Y)} hidden things within 3 tiles, Eve at {eveAt} with {Around(hidden, eveAt.X, eveAt.Y)}");
        In(w, () =>
        {
            Waiting.NoSleep(bob.forceMove(locate(bobAt.X, bobAt.Y, 1)));
            Waiting.NoSleep(eve.forceMove(locate(eveAt.X, eveAt.Y, 1)));
        });
        w.Run(Second);
        Assert.True(w.World.RuntimeErrorCount >= 0);
        int bootRuntimes = w.World.RuntimeErrorCount;
        ReplicaWorld bobSees = bobP.Session.Replica, eveSees = eveP.Session.Replica;
        var heard = new List<SoundEvent>();
        bobSees.SoundsReceived += events => heard.AddRange(events);
        var visuals = new List<ReplicaEntity>();
        bobSees.Spawned += e => { if (e.Id < 0) visuals.Add(e); };

        // the hidden things are not on the wire: the replica of neither player holds a cable under a tile near Bob
        List<obj> expectedObjects = In(w, () => Dm.each<obj>(orange(3, bob)).Where(o => HAS_TRAIT(o, TRAIT_T_RAY_VISIBLE)).ToList());
        Assert.True(expectedObjects.Count >= 8, "hidden things around Bob: " + expectedObjects.Count);
        Assert.All(expectedObjects, o => Assert.DoesNotContain(bobSees.Entities, e => e.Id == o.dm_id));
        Assert.Empty(bobSees.Images);

        w.Host.EnqueueConsoleLine("give Bob /obj/item/t_scanner");
        w.Run(5);
        var scanner = In(w, () => bob.get_active_held_item() as obj_item_t_scanner);
        Assert.NotNull(scanner);
        Assert.Equal("t-ray0", In(w, () => scanner.icon_state));
        bobP.Session.Keybinding(KeybindingCode.ActivateInhand); // Z: attack_self, toggle_on
        w.Run(3);
        Assert.True(In(w, () => Dm.truthy(scanner.on)));
        Assert.Equal("t-ray1", In(w, () => scanner.icon_state));

        // the pulses: SSobj runs the scanner every 2 s; each replaces the images of the last for the next scan's time (DV91)
        var present = new List<bool>();
        List<ImageEntry> pulse = null;
        for (int tick = 0; tick < Second * 5; tick++)
        {
            w.Run(1);
            bool any = bobSees.Images.Values.Any(i => i.TurfX > 0);
            present.Add(any);
            if (any && pulse == null)
                pulse = bobSees.Images.Values.Where(i => i.TurfX > 0).ToList();
            Assert.Empty(eveSees.Images); // not for the second player, at any tick
        }
        Assert.NotNull(pulse);
        Assert.Contains(heard, h => h.Kind == SoundKind.Play && h.File != null && (h.File ?? "").Contains("industrial_scan", StringComparison.Ordinal)); // the toggle's sound (t_scanner.dm:26)
        int first = present.IndexOf(true);
        int span = present.Skip(first).TakeWhile(p => p).Count();
        output.WriteLine($"the first scan at tick {first} of the run shows for {span} ticks and holds {pulse.Count} images (hidden things in range {expectedObjects.Count})");
        Assert.Equal(present.Count - first, span); // DV91 (D173): once shown the images stay at every tick across the scans (tg's pulse blinks)

        // one image for each hidden thing in range, at its tile, with its icon state, at tg's alpha 128
        var shown = pulse.Select(i =>
        {
            Assert.True(bobSees.TryGetAppearance(i.Appearance, out AppearanceEntry look));
            Assert.Equal(128, look.Alpha);
            Assert.Equal((byte)1, (byte)(i.TurfZ));
            return (i.TurfX, i.TurfY, State: bobSees.ResolveString(look.IconState), Icon: bobSees.ResolveResource(look.Icon));
        }).OrderBy(t => t.TurfX).ThenBy(t => t.TurfY).ThenBy(t => t.State, StringComparer.Ordinal).ToList();
        var expected = In(w, () => expectedObjects.Select(o => ((int)o.x, (int)o.y, State: o.icon_state)).OrderBy(t => t.Item1).ThenBy(t => t.Item2).ThenBy(t => t.State, StringComparer.Ordinal).ToList());
        Assert.Equal(expected.Count, shown.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Item1, shown[i].TurfX);
            Assert.Equal(expected[i].Item2, shown[i].TurfY);
            Assert.Equal(expected[i].State, shown[i].State);
        }
        Assert.Contains(shown, t => t.Icon.Contains("cable", StringComparison.Ordinal));
        Assert.All(shown, t => Assert.InRange(Math.Max(Math.Abs(t.TurfX - bobAt.X), Math.Abs(t.TurfY - bobAt.Y)), 0, 3));
        Assert.NotEmpty(visuals); // the replica draws each as a visual on its tile (the entity layer)
        Assert.All(visuals, v => Assert.Equal(0, v.State.VisHost));

        // switched off: no more pulses
        bobP.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second * 3);
        Assert.False(In(w, () => Dm.truthy(scanner.on)));
        Assert.Equal("t-ray0", In(w, () => scanner.icon_state));
        w.Run(Second * 4);
        Assert.Empty(bobSees.Images);
        Assert.Empty(eveSees.Images);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    // A belt hangs on a uniform (tg's mob_can_equip for the belt slot).
    private static void Dress(RealHeadlessWorld w, mob_living_carbon_human me)
        => In(w, () =>
        {
            if (me.w_uniform == null)
                Assert.True(Dm.truthy(Waiting.NoSleep(me.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(me.loc), ITEM_SLOT_ICLOTHING))));
            if (me.belt != null)
                qdel(me.belt);
        });

    private static List<ScreenEntry> HoldersOf(ReplicaWorld replica, long id)
        => replica.Screen.Values.Where(e => e.VisChildren != null && e.VisChildren.Contains(id)).ToList();

    [MetaServerFact]
    public void A_full_engineering_belt_worn_shows_its_tools_to_the_wearers_client_and_one_taken_out_goes_from_it()
    {
        using RealHeadlessWorld w = Meta(62);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        In(w, () => Waiting.NoSleep(me.forceMove(locate(46, 122, 1))));
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount;

        Dress(w, me);
        obj_item_storage_belt_utility_full_engi belt = In(w, () => Dm.@new<obj_item_storage_belt_utility_full_engi>(me.loc));
        w.Run(2);
        Assert.True(In(w, () => Dm.truthy(Waiting.NoSleep(me.equip_to_slot_if_possible(belt, ITEM_SLOT_BELT)))));
        w.Run(5);
        Assert.Same(belt, In(w, () => me.belt));
        Assert.Equal(7, In(w, () => belt.contents.Count));

        // the belt's slot picture holds the belt, and the belt's own look lists one overlay for each tool
        ScreenEntry slot = Assert.Single(HoldersOf(replica, belt.dm_id));
        ScreenChild child = Assert.Single(slot.VisChildren.Items);
        Assert.True(replica.TryGetAppearance(child.Appearance, out AppearanceEntry look));
        Assert.Equal("utility", replica.ResolveString(look.IconState));
        List<(string Icon, string State)> Tools() => replica.TryGetAppearance(HoldersOf(replica, belt.dm_id).Single().VisChildren.Items[0].Appearance, out AppearanceEntry l)
            ? l.Overlays.Select(o => replica.TryGetAppearance(o, out AppearanceEntry e) ? (Icon: replica.ResolveResource(e.Icon), State: replica.ResolveString(e.IconState)) : default).ToList()
            : new();
        var tools = Tools();
        output.WriteLine("the belt's overlays on the client: " + string.Join(", ", tools.Select(t => t.Icon + ":" + t.State)));
        Assert.Equal(7, tools.Count);
        Assert.Equal(5, tools.Count(t => t.Icon.EndsWith("belt_overlays.dmi", StringComparison.Ordinal))); // the screwdriver's and the wirecutters' colours are GAGS pictures
        Assert.Equal(2, tools.Count(t => t.Icon.StartsWith("gags:", StringComparison.Ordinal)));
        foreach (string state in new[] { "wrench", "crowbar", "multitool" })
            Assert.Contains(tools, t => t.State == state);

        // the multitool taken out of the belt: its overlay goes
        var multitool = In(w, () => Dm.each(belt.contents).OfType<obj_item_multitool>().Single());
        In(w, () => Assert.True(Dm.truthy(Waiting.NoSleep(belt.atom_storage.remove_single(me, multitool, me.loc)))));
        w.Run(5);
        tools = Tools();
        Assert.Equal(6, tools.Count);
        Assert.DoesNotContain(tools, t => t.State == "multitool");

        // and put back
        In(w, () => Assert.True(Dm.truthy(belt.atom_storage.attempt_insert(multitool, me))));
        w.Run(5);
        Assert.Equal(7, Tools().Count);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    [MetaServerFact]
    public void A_multitool_taken_out_of_the_belt_reads_a_cables_network_for_the_engineer()
    {
        using RealHeadlessWorld w = Meta(63);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        var texts = new List<string>();
        p.Client.ServerTextReceived += t => texts.Add(t.Html);
        // a cable on bare plating (nothing to pry) of the biggest network
        obj_structure_cable cable = In(w, () => Dm.each<obj_structure_cable>(Dm.world)
            .Where(c => c.loc is turf_open_floor_plating t && Dm.eq(t.z, 1) && c.powernet != null && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density) || Dm.truthy(Dm.bitand(a.flags_1, PREVENT_CLICK_UNDER_1))))
            .OrderByDescending(c => c.powernet.cables.Count).ThenBy(c => c.dm_id).First());
        int x = In(w, () => (int)cable.x), y = In(w, () => (int)cable.y);
        In(w, () => Waiting.NoSleep(me.forceMove(locate(x, y, 1))));
        w.Run(10 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        Assert.True(In(w, () => Dm.num(cable.powernet.avail) > 0), "the network has power");

        // the belt on, the multitool out of it into a hand
        Dress(w, me);
        obj_item_storage_belt_utility_full_engi belt = In(w, () => Dm.@new<obj_item_storage_belt_utility_full_engi>(me.loc));
        In(w, () => Assert.True(Dm.truthy(Waiting.NoSleep(me.equip_to_slot_if_possible(belt, ITEM_SLOT_BELT)))));
        var multitool = In(w, () => Dm.each(belt.contents).OfType<obj_item_multitool>().Single());
        In(w, () => Assert.True(Dm.truthy(Waiting.NoSleep(belt.atom_storage.remove_single(me, multitool, me.loc)))));
        In(w, () => Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(multitool)))));
        w.Run(2 * Second);
        Assert.Same(multitool, In(w, () => me.get_active_held_item()));
        texts.Clear();

        Session session = w.Host.Sessions.Single(s => s.Client != null && ReferenceEquals(s.Client.mob, me));
        int rejected = session.ClicksRejected;
        Assert.True(p.Session.Replica.Entities.Any(e => e.Id == cable.dm_id), "the exposed cable is on the client");
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, cable.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Equal(rejected, session.ClicksRejected);
        output.WriteLine(string.Join(" | ", texts));
        Assert.Contains(texts, t => t.Contains("Total power:", StringComparison.Ordinal) && t.Contains("Load:", StringComparison.Ordinal) && t.Contains("Excess power:", StringComparison.Ordinal));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
