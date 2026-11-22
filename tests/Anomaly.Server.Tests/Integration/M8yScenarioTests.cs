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
using Actor = Anomaly.Server.Tests.Integration.M8ScenarioTests.Actor;
using Scenario = Anomaly.Server.Tests.Integration.M8ScenarioTests.Scenario;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// ROADMAP M8y's done-when (D221, D223-D226) through the real server on MetaStation with three real clients: Alice the medic, Bob the
/// patient, Carol the friend and watcher.
/// Buckling: Alice drags Carol onto the medbay lobby's chair (93,103) and stands her up by hand, then onto the hallway's roller bed
/// (91,113), from which Carol resists out (her Buckled alert, uncuffed: at once).
/// Drag gestures, in the cursor mode: a pen from the floor into Alice's free hand slot, another onto her pocket slot on the doll; the
/// held pen onto a table, from the table into her hand again and into her worn backpack; the pocketed pen onto the floor beside her.
/// Stasis: Bob gets a severe cut on his chest, 60 oxygen damage and epinephrine by dev commands, and Alice, in tg's medical doctor outfit,
/// drags him onto the Treatment Center's powered bed (79,105): for a minute his health, blood, oxygen damage and the reagent stay as they
/// were, while Carol sees the mattress and the ripple and hears the spray. Alice bandages his cut on the bed with her surgery kit's gauze;
/// the room's APC breaker off stops the bed and he worsens again; on, he is in stasis again; stood up from it by her hand, he is thawed;
/// dragged back on and dead, his time of death waits. A second test runs it all twice from a fresh boot and compares the state hashes.
/// </summary>
public class M8yScenarioTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const ulong Seed = 82;
    private const int ChairX = 93, ChairY = 103, RollerX = 91, RollerY = 113, BedX = 79, BedY = 105;

    private static T At<T>(Scenario s, int x, int y) where T : atom
        => s.In(() => Dm.each_cast<atom>(((turf)locate(x, y, 1)).contents).OfType<T>().First(t => t.GetType() == typeof(T)));

    private static void Drag(Scenario s, ClickTarget source, ClickTarget over, float seconds = 1)
    {
        s.Do(() => s.Alice.Mob.next_click = -1);
        Assert.True(s.Alice.P.Session.Drop(new DragRelease(source, over, ClickButton.Left, ClickModifiers.None, 4)));
        s.Run(seconds);
    }

    private static bool Told(Actor a, string text) => a.Texts.Any(t => t.Contains(text, StringComparison.Ordinal));

    // A friend dragged onto a chair and a roller bed (tg's 2 s mouse_drop_receive buckle), stood up by hand and resisting out.
    private static void BucklingPart(Scenario s)
    {
        Actor alice = s.Alice, carol = s.Carol;
        var chair = At<obj_structure_chair>(s, ChairX, ChairY);
        s.Place(carol, s.In(() => locate(ChairX, ChairY + 1, 1)));
        s.Place(alice, s.In(() => locate(ChairX + 1, ChairY + 1, 1)));
        Drag(s, DragGestureGameplayTests.Entity(carol.Mob.dm_id), DragGestureGameplayTests.Entity(chair.dm_id), 2.5f);
        Assert.Same(chair, s.In(() => carol.Mob.buckled));
        Assert.True(Told(carol, "sits you down on"));
        s.Click(chair);
        Assert.Null(s.In(() => carol.Mob.buckled));
        Assert.True(Told(carol, "stands you up from"));
        s.Out.WriteLine("chair ok");

        var roller = At<obj_structure_bed_medical_emergency>(s, RollerX, RollerY);
        s.Place(carol, s.In(() => locate(RollerX - 1, RollerY, 1)));
        s.Place(alice, s.In(() => locate(RollerX - 1, RollerY + 1, 1)));
        Drag(s, DragGestureGameplayTests.Entity(carol.Mob.dm_id), DragGestureGameplayTests.Entity(roller.dm_id), 2.5f);
        Assert.Same(roller, s.In(() => carol.Mob.buckled));
        Assert.Equal(LYING_DOWN, s.In(() => Dm.num(carol.Mob.body_position)));
        Assert.True(Told(carol, "lays you down on"));
        long alert = s.In(() => ((datum)carol.Mob.alerts[ALERT_BUCKLED]).dm_id);
        Assert.True(carol.P.Session.Click(new ClickTarget(TargetKind.Screen, alert, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        s.Run(0.5f);
        Assert.Null(s.In(() => carol.Mob.buckled));
        Assert.True(Told(carol, "You get up from")); // the bed's own unbuckle line (bed.dm)
        s.Out.WriteLine("roller bed ok");
    }

    // The cursor mode's drags of items (D224-D226): floor to hand and to a doll slot, hand to a table and a container, slot to the floor.
    private static void DragPart(Scenario s)
    {
        Actor alice = s.Alice;
        mob_living_carbon_human me = alice.Mob;
        var (table, tx, ty) = DragGestureGameplayTests.TableNear(s.W, ChairX, ChairY, 1);
        Assert.NotNull(table);
        s.Place(alice, s.In(() => locate(tx, ty, 1)));
        turf here = s.In(() => (turf)me.loc), tableTile = s.In(() => (turf)table.loc);
        int inactive = s.In(() => (int)me.active_hand_index) == 1 ? 2 : 1;
        ClickTarget hand = DragGestureGameplayTests.Screen(DragGestureGameplayTests.ScreenId(s.W, me, Dm.text("hand_slot:[]", inactive)));
        ClickTarget pocket = DragGestureGameplayTests.Screen(DragGestureGameplayTests.ScreenId(s.W, me, Dm.text("item_slot:[]", ITEM_SLOT_LPOCKET)));
        ClickTarget back = DragGestureGameplayTests.Screen(DragGestureGameplayTests.ScreenId(s.W, me, Dm.text("item_slot:[]", ITEM_SLOT_BACK)));
        var pack = s.In(() => (obj_item_storage)me.back);
        Assert.NotNull(pack);
        var pen = s.In(() => Dm.@new<obj_item_pen>(here));
        var other = s.In(() => Dm.@new<obj_item_pen>(here));
        s.Run(0.2f);

        Drag(s, DragGestureGameplayTests.Entity(pen.dm_id), hand);
        Assert.Same(pen, s.In(() => me.get_item_for_held_index(inactive)));
        Drag(s, DragGestureGameplayTests.Entity(other.dm_id), pocket);
        Assert.Same(other, s.In(() => me.l_store));
        Drag(s, DragGestureGameplayTests.Screen(pen.dm_id), DragGestureGameplayTests.Entity(table.dm_id));
        Assert.Same(tableTile, s.In(() => pen.loc));
        Drag(s, DragGestureGameplayTests.Entity(pen.dm_id), hand);
        Assert.Same(me, s.In(() => pen.loc));
        Drag(s, DragGestureGameplayTests.Screen(pen.dm_id), back);
        Assert.Same(pack, s.In(() => pen.loc));
        turf next = s.In(() => Dm.each<turf_open>(range(1, me)).OrderBy(t => t.dm_id).First(t => t != me.loc && !Dm.each_cast<atom>(t.contents).Any(x => Dm.truthy(x.density))
            && Dm.truthy(Waiting.NoSleep(t.IsReachableBy(me)))));
        Drag(s, DragGestureGameplayTests.Screen(other.dm_id), DragGestureGameplayTests.Cell(next));
        Assert.Same(next, s.In(() => other.loc));
        s.Out.WriteLine($"drags ok at the table ({tx + 1},{ty})");
    }

    // The patient on the stasis bed (D221).
    private static void StasisPart(Scenario s)
    {
        RealHeadlessWorld w = s.W;
        Actor alice = s.Alice, bob = s.Bob, carol = s.Carol;
        mob_living_carbon_human him = bob.Mob;
        var bed = At<obj_machinery_stasis>(s, BedX, BedY);
        Assert.True(s.In(() => Dm.truthy(bed.stasis_running())), "the bed is on and powered at the round's start");

        alice.P.Session.DevCommand("outfit Alice /datum/outfit/job/doctor");
        s.Run(1);
        var kit = s.In(() => Dm.each_cast<atom>(alice.Mob.held_items).OfType<obj_item_storage_medkit_surgery>().Single());
        if (s.Held() == kit)
            s.Key(KeybindingCode.SwapHands); // the kit in the other hand, as FromKit leaves it
        s.Place(carol, s.In(() => locate(BedX + 2, BedY - 1, 1)));
        s.Place(alice, s.In(() => locate(BedX + 1, BedY, 1)));
        s.Place(bob, s.In(() => locate(BedX + 1, BedY - 1, 1)));

        // ---- The emergency, by dev commands (as D193)
        foreach (string line in new[] { "wound Bob chest slash severe", "damage Bob oxy 60", "reagent Bob /datum/reagent/medicine/epinephrine 15" })
        {
            alice.P.Session.DevCommand(line);
            w.Run(1);
        }
        s.Run(1);
        float Blood() => s.In(() => Dm.num(him.blood_volume));
        float Dose() => s.In(() => Dm.num(((datum_reagents)him.reagents).get_reagent_amount(typeof(datum_reagent_medicine_epinephrine))));
        bool Stasis() => s.In(() => HAS_TRAIT(him, TRAIT_STASIS));
        datum_wound_slash Cut() => s.Wounds.OfType<datum_wound_slash>().SingleOrDefault();
        float blood0 = Blood(), dose0 = Dose();
        s.Run(3);
        Assert.True(Blood() < blood0 && Dose() < dose0, "off the bed he bleeds and metabolizes");
        s.Out.WriteLine("the emergency: " + s.State());

        // ---- Dragged onto the bed: buckled lying, in stasis; Carol sees the mattress and the ripple and hears the spray
        Drag(s, DragGestureGameplayTests.Entity(him.dm_id), DragGestureGameplayTests.Entity(bed.dm_id), 2.5f);
        Assert.Same(bed, s.In(() => him.buckled));
        Assert.True(Stasis());
        ReplicaWorld seen = carol.P.Session.Replica;
        AppearanceEntry Look(ReplicaEntity e) => seen.TryGetAppearance(e.State.Appearance, out AppearanceEntry a) ? a : null;
        ReplicaEntity Mattress() => seen.Entities.Single(e => e.IsVisual && e.VisHost == bed.dm_id && e.TypePath == "/obj/effect/overlay/vis");
        Assert.Equal(255, Look(Mattress()).Alpha);
        Assert.Contains(Look(seen.Entities.Single(e => e.Id == him.dm_id)).Filters ?? new List<FilterEntry>(), f => f.Kind == FilterKind.Ripple);
        Assert.Contains(carol.Heard, h => h.File == "sound/effects/spray.ogg");

        // ---- A minute on the bed: as he was
        (float health, float blood, float dose, float oxy) = (s.Health, Blood(), Dose(), s.Oxy);
        s.Run(60);
        Assert.Equal((health, blood, dose, oxy), (s.Health, Blood(), Dose(), s.Oxy));
        s.Measured["stasis"] = $"a minute on the bed: health {health}, blood {blood}, epinephrine {dose}, oxy {oxy}";
        s.Out.WriteLine("a minute later: " + s.State());

        // ---- Alice bandages his cut on the bed with her surgery kit's gauze (other_delay 2 s); in stasis the wound does not process, so the
        // gauze neither seeps nor slows the bleeding until the bed stops (slash.dm handle_process)
        float flow = s.Flow(Cut());
        M8ScenarioTests.FromKit<obj_item_stack_medical_wrap_gauze_twelve>(s, kit);
        s.Click(him, AimZone.Chest, seconds: 3);
        Assert.NotNull(s.In(() => ((obj_item_bodypart)him.get_bodypart(BODY_ZONE_CHEST)).applied_items?[LIMB_ITEM_GAUZE]));
        Assert.True(Stasis());
        Assert.Equal(flow, s.Flow(Cut()));
        s.Measured["gauze"] = $"the cut bandaged on the bed at flow {flow:0.##}";
        s.Out.WriteLine("bandaged: " + s.State());
        M8ScenarioTests.PutKitDown(s, kit);

        // ---- The room's APC breaker off: the bed stops, he worsens again; on again, stasis again
        var apc = s.In(() => (obj_machinery_power_apc)((area)get_area(bed)).apc);
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => alice.P.Session.UiAct(window, action, parameters),
            SendClose = window => alice.P.Session.UiClose(window),
        };
        UiNode Breaker()
        {
            windows.Sync(alice.P.Session.Replica.UiWindows, 1280, 720, 1);
            return windows.Get(alice.P.Session.Replica.UiWindows.Tgui().Single().Window)?.Content.Walk().First(n => n.Kind == UiKind.LabeledItem && n.Label == "Main Breaker").Buttons.Walk().First(n => n.Key == "breaker");
        }
        s.Place(alice, s.In(() => (turf)apc.loc));
        var card = s.In(() => Dm.@new<obj_item_card_id_advanced_debug>(alice.Mob.loc)); // a set-up step: the APC wants engineering access
        s.Click(apc); // its window, locked
        s.Do(() => Waiting.NoSleep(alice.Mob.put_in_hands(card)));
        s.Click(apc); // the card unlocks it
        (blood, dose, health) = (Blood(), Dose(), s.Health);
        Breaker().OnClick();
        s.Run(10);
        Assert.False(s.In(() => Dm.truthy(bed.stasis_running())));
        Assert.False(Stasis());
        Assert.True(Dose() < dose, "thawed, he metabolizes again");
        Assert.True(Blood() < blood, "thawed, he bleeds through the gauze again");
        Assert.True(s.Flow(Cut()) < flow, "the gauze slows the cut as it processes again");
        s.Measured["power cut"] = $"10 s: blood {blood:0.#} -> {Blood():0.#}, epinephrine {dose:0.##} -> {Dose():0.##}, health {health:0.#} -> {s.Health:0.#}";
        Breaker().OnClick();
        s.Run(6);
        Assert.True(Stasis());
        alice.P.Session.UiClose(alice.P.Session.Replica.UiWindows.Tgui().Single().Window);
        s.Key(KeybindingCode.DropItem);
        s.Out.WriteLine("power ok: " + s.State());

        // ---- Stood up from the bed by her hand: thawed
        s.Place(alice, s.In(() => locate(BedX + 1, BedY, 1)));
        s.Click(bed);
        Assert.Null(s.In(() => him.buckled));
        Assert.False(Stasis());
        dose = Dose();
        s.Run(3);
        Assert.True(Dose() < dose, "off the bed his reagent metabolizes");

        // ---- Dragged back on and dead: his time of death moves with the clock
        Drag(s, DragGestureGameplayTests.Entity(him.dm_id), DragGestureGameplayTests.Entity(bed.dm_id), 2.5f);
        Assert.Same(bed, s.In(() => him.buckled));
        alice.P.Session.DevCommand("kill Bob");
        s.Run(1);
        Assert.Equal((float)DEAD, s.Stat);
        float Dead() => s.In(() => Dm.num(world.time) - him.timeofdeath);
        s.Run(30);
        Assert.True(Dead() <= 2 * SECONDS, $"dead for {Dead()} ds on the bed");
        s.Out.WriteLine($"dead on the bed: {Dead()} ds after 30 s");
    }

    private static void All(Scenario s)
    {
        BucklingPart(s);
        DragPart(s);
        StasisPart(s);
    }

    [MetaServerFact]
    public void A_medic_buckles_a_friend_drags_items_and_keeps_a_dying_man_in_stasis_on_metas_bed()
    {
        Scenario s = M8ScenarioTests.Start(output, Seed);
        using (s.W)
        {
            All(s);
            foreach (KeyValuePair<string, string> m in s.Measured)
                output.WriteLine($"{m.Key}: {m.Value}");
            M8ScenarioTests.AssertClean(s);
        }
    }

    [MetaServerFact]
    public void The_m8y_scenario_run_twice_from_a_fresh_boot_ends_with_the_same_state_hash()
    {
        (ulong Hash, long Tick) Once()
        {
            Scenario s = M8ScenarioTests.Start(output, Seed);
            using (s.W)
            {
                All(s);
                return (s.W.World.ComputeStateHash(), s.W.Ticks);
            }
        }

        var first = Once();
        var second = Once();
        output.WriteLine($"first run: {first.Hash:x16} at tick {first.Tick}; second: {second.Hash:x16} at tick {second.Tick}");
        Assert.Equal(first.Tick, second.Tick);
        Assert.Equal(first.Hash, second.Hash);
    }
}
