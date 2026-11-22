using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
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
/// M8 slice C through the real server on MetaStation with headless clients, with medbay's own things: Alice picks up a regular
/// medkit, opens it from her other hand (tg's contents: gauze, two sutures, two meshes, a medipen, a simple analyzer) and takes the
/// medipen out; a click on Bob injects him at once (his epinephrine rises, the pen turns <c>medipen0</c> on her client, both hear the
/// hypospray). A syringe draws 5 units from one of medbay's epinephrine bottles with a right click: the held syringe's picture on her
/// client has the coloured <c>syringe5</c> fill (what her first-person view model draws), and a click injects Bob after 3 seconds.
/// She feeds him a pill (3 seconds), then hangs one of medbay's blood packs on an IV drip and drags the drip onto him: after the
/// second of attaching, the pack empties at tg's 5 units a second and her client sees the drip's beam to him.
/// </summary>
public class MedicineDeliveryGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    // The first thing of the type (by id) lying in a medical area of the station with a clear tile it can be reached from.
    private static T InMedbay<T>() where T : atom_movable
    {
        for (int x = 1; x <= Dm.num(world.maxx); x++)
        {
            for (int y = 1; y <= Dm.num(world.maxy); y++)
            {
                turf t = locate(x, y, 1);
                if (t == null || get_area(t) is not area_station_medical)
                    continue;
                T found = Dm.each_cast<atom>(t.contents).OfType<T>().Where(a => a.GetType() == typeof(T)).OrderBy(a => a.dm_id).FirstOrDefault();
                if (found != null && ClearNeighbour(found) != null)
                    return found;
            }
        }
        return null;
    }

    private static turf ClearNeighbour(atom a, turf not = null)
        => new[] { NORTH, SOUTH, EAST, WEST, NORTHEAST, NORTHWEST, SOUTHEAST, SOUTHWEST }.Select(d => get_step(a, d) as turf).FirstOrDefault(t => t != null && t != not && Clear(t) && Dm.truthy(Waiting.NoSleep(a.Adjacent(t)))); // reachable: not behind a windoor

    private static ClickTarget On(atom a, AimZone zone = AimZone.None) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16, zone);

    private static ClickTarget Screen(atom a) => new(TargetKind.Screen, a.dm_id, 0, 0, 0, 16, 16);

    // Every (icon, state, colour) of an appearance and its overlays, as the client's compositor walks them.
    private static List<(string Icon, string State, uint Rgba)> Looks(ReplicaWorld replica, TableRef reference, int depth = 0)
    {
        var found = new List<(string, string, uint)>();
        if (depth > 8 || !replica.TryGetAppearance(reference, out AppearanceEntry entry))
            return found;
        found.Add((replica.ResolveResource(entry.Icon) ?? replica.ResolveString(entry.Icon), replica.ResolveString(entry.IconState), entry.Rgba));
        foreach (TableRef overlay in entry.Overlays)
            found.AddRange(Looks(replica, overlay, depth + 1));
        return found;
    }

    // The active hand's item picture on the client, found as the first-person view model finds it (godot/scripts/Shared/Play/HeldHands.cs:
    // the hand slot with the "...handactive" overlay; its other overlay is the held item's appearance).
    private static List<(string Icon, string State, uint Rgba)> Held(ReplicaWorld replica)
    {
        foreach (ScreenEntry entry in replica.Screen.Values)
        {
            if (entry.Group != HudGroup.Static || !replica.TryGetAppearance(entry.Appearance, out AppearanceEntry app))
                continue;
            string state = replica.ResolveString(app.IconState);
            if (state != "hand_l" && state != "hand_r")
                continue;
            bool active = false;
            TableRef item = TableRef.None;
            foreach (TableRef overlay in app.Overlays)
            {
                if (replica.TryGetAppearance(overlay, out AppearanceEntry o) && (replica.ResolveString(o.IconState) ?? string.Empty).EndsWith("handactive", StringComparison.Ordinal))
                    active = true;
                else
                    item = overlay;
            }
            if (active)
                return Looks(replica, item);
        }
        return new();
    }

    [MetaServerFact]
    public void Alice_treats_Bob_with_medbays_medkit_medipen_syringe_pill_and_IV_drip()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 87, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var texts = new List<string>();
        alice.Client.ServerTextReceived += t => texts.Add(t.Html);
        var heardAlice = new List<SoundEvent>();
        var heardBob = new List<SoundEvent>();
        alice.Session.Replica.SoundsReceived += events => heardAlice.AddRange(events);
        bob.Session.Replica.SoundsReceived += events => heardBob.AddRange(events);
        w.Run(Second);
        ReplicaWorld replica = alice.Session.Replica;
        float Epinephrine() => In(w, () => Dm.num(((datum_reagents)bobMob.reagents).get_reagent_amount(typeof(datum_reagent_medicine_epinephrine))));

        // Next to a thing of medbay: Alice on a clear tile beside it, Bob beside her.
        void GoTo(atom a)
        {
            Do(w, () =>
            {
                turf at = ClearNeighbour(a);
                Assert.NotNull(at);
                Waiting.NoSleep(aliceMob.forceMove(at));
                Waiting.NoSleep(bobMob.forceMove(ClearNeighbour(aliceMob, get_turf(a))));
            });
            w.Run(Second);
        }

        obj_item_storage_medkit_regular medkit = In(w, InMedbay<obj_item_storage_medkit_regular>);
        Assert.NotNull(medkit);
        output.WriteLine(In(w, () => $"the medkit at {medkit.x},{medkit.y} in {Dm.str(((area)get_area(medkit)).name)}"));
        GoTo(medkit);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        // 1. The medkit: picked up, opened from the other hand, tg's contents.
        Assert.True(alice.Session.Click(On(medkit), ClickButton.Left, ClickModifiers.None));
        w.Run(5);
        Assert.Same(aliceMob, In(w, () => medkit.loc));
        var contents = In(w, () => Dm.each_cast<atom>(medkit.contents).Select(a => a.GetType()).ToList());
        output.WriteLine("the medkit holds: " + string.Join(", ", contents.Select(t => t.Name)));
        Assert.Single(contents, t => t == typeof(obj_item_stack_medical_wrap_gauze));
        Assert.Equal(2, contents.Count(t => t == typeof(obj_item_stack_medical_suture)));
        Assert.Equal(2, contents.Count(t => t == typeof(obj_item_stack_medical_mesh)));
        Assert.Single(contents, t => t == typeof(obj_item_reagent_containers_hypospray_medipen));
        Assert.Single(contents, t => t == typeof(obj_item_healthanalyzer_simple));
        alice.Session.Keybinding(KeybindingCode.SwapHands);
        w.Run(5);
        Assert.True(alice.Session.Click(Screen(medkit), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(Dm.@in(aliceMob, medkit.atom_storage.is_using))), "the medkit is open");
        var pen = In(w, () => Dm.each(medkit.contents).OfType<obj_item_reagent_containers_hypospray_medipen>().Single());
        Assert.True(alice.Session.Click(Screen(pen), ClickButton.Left, ClickModifiers.None));
        w.Run(5);
        Assert.Same(pen, In(w, () => aliceMob.get_active_held_item()));

        // 2. The medipen into Bob: at once, with the hypospray heard by both; the pen is spent.
        float dose = In(w, () => Dm.num(((datum_reagents)pen.reagents).get_reagent_amount(typeof(datum_reagent_medicine_epinephrine))));
        heardAlice.Clear();
        heardBob.Clear();
        Assert.True(alice.Session.Click(On(bobMob, AimZone.Chest), ClickButton.Left, ClickModifiers.None));
        w.Run(5);
        Assert.Equal(dose, Epinephrine(), 1);
        Assert.Equal("medipen0", In(w, () => pen.icon_state));
        Assert.Contains(heardAlice, h => h.File == "sound/items/hypospray.ogg");
        Assert.Contains(heardBob, h => h.File == "sound/items/hypospray.ogg");
        w.Run(2);
        Assert.Contains(Held(replica), l => l.State == "medipen0");

        // 3. A syringe draws from one of medbay's epinephrine bottles and injects Bob.
        Do(w, () => Waiting.NoSleep(aliceMob.dropItemToGround(pen)));
        obj_item_reagent_containers_cup_bottle_epinephrine bottle = In(w, InMedbay<obj_item_reagent_containers_cup_bottle_epinephrine>);
        obj_item_reagent_containers_syringe syringe = In(w, InMedbay<obj_item_reagent_containers_syringe>);
        Assert.NotNull(bottle);
        Assert.NotNull(syringe);
        GoTo(syringe);
        Assert.True(alice.Session.Click(On(syringe), ClickButton.Left, ClickModifiers.None));
        w.Run(5);
        Assert.Same(syringe, In(w, () => aliceMob.get_active_held_item()));
        GoTo(bottle);
        Assert.True(alice.Session.Click(On(bottle), ClickButton.Right, ClickModifiers.None));
        w.Run(5);
        Assert.Equal(5f, In(w, () => Dm.num(((datum_reagents)syringe.reagents).get_reagent_amount(typeof(datum_reagent_medicine_epinephrine)))), 3);
        Assert.Equal("syringe_5", In(w, () => syringe.icon_state));
        w.Run(2);
        var held = Held(replica);
        output.WriteLine("the held syringe: " + string.Join(", ", held.Select(l => $"{l.Icon}:{l.State}:{l.Rgba:X8}")));
        (string Icon, string State, uint Rgba) fill = Assert.Single(held, l => l.State == "syringe5");
        Assert.Equal("icons/obj/medical/reagent_fillings.dmi", fill.Icon);
        Assert.NotEqual(0xFFFFFFFFu, fill.Rgba); // tinted with epinephrine's colour
        float before = Epinephrine();
        Assert.True(alice.Session.Click(On(bobMob, AimZone.Chest), ClickButton.Left, ClickModifiers.None));
        w.Run(3 * Second + 10);
        Assert.InRange(Epinephrine(), before + 4.5f, before + 5f); // Bob metabolizes some during the 3 seconds
        Assert.Equal("syringe_0", In(w, () => syringe.icon_state));

        // 4. A pill, fed to Bob after 3 seconds.
        Do(w, () => Waiting.NoSleep(aliceMob.dropItemToGround(syringe)));
        obj_item_reagent_containers_applicator_pill pill = In(w, () =>
        {
            var p = Dm.@new<obj_item_reagent_containers_applicator_pill_iron>(aliceMob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(aliceMob.put_in_active_hand(p))));
            return p;
        });
        w.Run(Second);
        Assert.True(alice.Session.Click(On(bobMob, AimZone.Head), ClickButton.Left, ClickModifiers.None));
        w.Run(3 * Second + 10);
        output.WriteLine(In(w, () => $"the pill: destroyed {Dm.truthy(pill.gc_destroyed)}, in {(pill.loc as datum)?.GetType().Name}"));
        Assert.True(In(w, () => Dm.truthy(pill.gc_destroyed) || (pill.loc != aliceMob && pill.loc is not turf)), "the pill is swallowed");
        w.Run(13 * Second); // the stomach digests every 6 seconds: the sugar shell is gone by the second digestion
        Assert.True(In(w, () => Dm.truthy(bobMob.has_reagent(typeof(datum_reagent_iron)))), "Bob has the pill's iron");

        // 5. A blood pack on one of medbay's IV drips, the drip dragged onto Bob.
        obj_machinery_iv_drip drip = In(w, InMedbay<obj_machinery_iv_drip>);
        obj_item_reagent_containers_blood pack = In(w, () =>
        {
            var p = InMedbay<obj_item_reagent_containers_blood_o_minus>();
            return p ?? (obj_item_reagent_containers_blood)Dm.@new<obj_item_reagent_containers_blood_o_minus>(aliceMob.loc);
        });
        Assert.NotNull(drip);
        Do(w, () => Waiting.NoSleep(pack.forceMove(aliceMob.loc)));
        GoTo(drip);
        Do(w, () => Waiting.NoSleep(pack.forceMove(aliceMob.loc)));
        w.Run(2);
        Assert.True(alice.Session.Click(On(pack), ClickButton.Left, ClickModifiers.None));
        w.Run(5);
        Assert.Same(pack, In(w, () => aliceMob.get_active_held_item()));
        Assert.True(alice.Session.Click(On(drip), ClickButton.Left, ClickModifiers.None));
        w.Run(5);
        Assert.Same(pack, In(w, () => drip.reagent_container));
        // Bob beside both the drip and Alice (the needle comes out past one tile, iv_drip.dm:226)
        Do(w, () => Waiting.NoSleep(bobMob.forceMove(Dm.each(orange(1, drip)).OfType<turf>().First(t => t != aliceMob.loc && Clear(t) && Dm.num(get_dist(t, aliceMob)) <= 1))));
        w.Run(Second);
        texts.Clear();
        Assert.True(alice.Session.Drop(new DragRelease(On(drip), On(bobMob), ClickButton.Left, ClickModifiers.None, 4)));
        w.Run(Second + 10);
        output.WriteLine("the drop: " + string.Join(" | ", texts));
        Assert.Same(bobMob, In(w, () => drip.attachment?.attached_to));
        float packed = In(w, () => Dm.num(((datum_reagents)pack.reagents).total_volume));
        w.Run(4 * Second);
        float fed = packed - In(w, () => Dm.num(((datum_reagents)pack.reagents).total_volume));
        output.WriteLine($"the pack fed {fed} units in 4 seconds");
        Assert.InRange(fed, 15f, 25f); // 5 units a second, a machine process every 2 seconds
        Assert.Contains(replica.Entities, e => e.TypePath != null && e.TypePath.StartsWith("/obj/effect/ebeam", StringComparison.Ordinal));
        Assert.True(replica.TryGet(drip.dm_id, out ReplicaEntity dripSeen));
        Assert.Contains(Looks(replica, dripSeen.State.Appearance), l => l.State == "iv_drip_injecting");

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
