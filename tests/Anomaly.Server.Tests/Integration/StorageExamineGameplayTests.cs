using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// ui-overhaul.md section 12, slice 4B-2 (DV692): tg's Shift+click on an item the Tab inventory's panels show examines it, as a Shift+click on tg's
/// storage screen does. Through a real server and raw headless clients: Alice's own stored screwdriver examines (tg's examine box reaches her); the
/// wrench in Bob's backpack, the pen in her locked lockbox and the crayon in a box inside her backpack (two levels deep) do not; a plain, Ctrl, Alt,
/// right or middle click on her own stored item, and a Shift+click naming it as a world entity, are refused as before.
/// </summary>
public class StorageExamineGameplayTests
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    [Fact]
    public void A_shift_click_on_an_item_the_senders_own_storages_show_examines_it_and_nothing_else_is_taken()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7501, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var her = (mob_living_carbon_human)w.MobOf(alice);
        var him = (mob_living_carbon_human)w.MobOf(bob);
        obj_item screwdriver = null, wrench = null, pen = null, crayon = null;
        In(w, () =>
        {
            foreach (mob_living_carbon_human who in new[] { her, him })
            {
                foreach (obj_item item in new[] { who.back, who.belt, who.l_store, who.r_store, who.wear_id, who.s_store,
                    who.get_item_for_held_index(1) as obj_item, who.get_item_for_held_index(2) as obj_item })
                    if (item != null)
                        qdel(item);
                if (who.w_uniform == null)
                    Assert.True(Dm.truthy(Waiting.NoSleep(who.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(who), ITEM_SLOT_ICLOTHING))));
                var bag = Dm.@new<obj_item_storage_backpack>(get_turf(who));
                Assert.True(Dm.truthy(Waiting.NoSleep(who.equip_to_slot_if_possible(bag, ITEM_SLOT_BACK))));
                if (who == her)
                {
                    screwdriver = Dm.@new<obj_item_screwdriver>(bag);
                    var box = Dm.@new<obj_item_storage_box>(bag);
                    crayon = Dm.@new<obj_item_toy_crayon_red>(box);
                    var lockbox = Dm.@new<obj_item_storage_lockbox>(get_turf(who));
                    pen = Dm.@new<obj_item_pen>(lockbox);
                    Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hand(lockbox, 1))));
                    Assert.True(Dm.truthy(lockbox.atom_storage.locked));
                }
                else
                    wrench = Dm.@new<obj_item_wrench>(bag);
            }
            Waiting.NoSleep(him.forceMove(get_step(her, EAST)));
            return 0;
        });
        w.Run(Second);
        int runtimes = w.World.RuntimeErrorCount;
        var chat = new List<ServerText>();
        alice.Client.ServerTextReceived += chat.Add;
        // what Alice's client is told: the screwdriver and the box in her backpack; the lockbox listed with nothing; no crayon, no pen, nothing of Bob's
        IReadOnlyList<CarriedStorage> listed = alice.Session.Storages.Storages;
        Assert.Contains(screwdriver.dm_id, listed.SelectMany(s => s.Items).Select(i => i.Entity));
        Assert.DoesNotContain(listed.SelectMany(s => s.Items), i => i.Entity == pen.dm_id || i.Entity == crayon.dm_id || i.Entity == wrench.dm_id);

        string ExamineBox(obj_item item, ClickButton button = ClickButton.Left, ClickModifiers modifiers = ClickModifiers.Shift, TargetKind kind = TargetKind.Screen)
        {
            chat.Clear();
            Assert.True(alice.Session.Click(new ClickTarget(kind, item.dm_id, 0, 0, 0, 16, 16), button, modifiers));
            w.Run(Second);
            return chat.Select(t => t.Html).LastOrDefault(h => h.Contains("boxed_message", StringComparison.Ordinal));
        }

        // her own stored screwdriver: tg's examine box, naming it
        string box = ExamineBox(screwdriver);
        Assert.True(box != null, "no examine box: " + string.Join(" | ", chat.Select(t => t.Html)) + "\n" + w.Err);
        Assert.Contains("screwdriver", box, StringComparison.OrdinalIgnoreCase);
        // Bob's wrench, the pen in her locked lockbox, the crayon two levels deep: as before, nothing
        foreach (obj_item notShown in new[] { wrench, pen, crayon })
            Assert.Null(ExamineBox(notShown));
        // any other click on her own stored item, and a Shift+click naming it as a world entity: refused as before; the screwdriver stays in her bag
        foreach ((ClickButton button, ClickModifiers modifiers, TargetKind kind) in new[]
        {
            (ClickButton.Left, ClickModifiers.None, TargetKind.Screen), (ClickButton.Left, ClickModifiers.Ctrl, TargetKind.Screen),
            (ClickButton.Left, ClickModifiers.Alt, TargetKind.Screen), (ClickButton.Left, ClickModifiers.Shift | ClickModifiers.Ctrl, TargetKind.Screen),
            (ClickButton.Right, ClickModifiers.Shift, TargetKind.Screen), (ClickButton.Middle, ClickModifiers.Shift, TargetKind.Screen),
            (ClickButton.Left, ClickModifiers.Shift, TargetKind.Entity),
        })
        {
            Assert.Null(ExamineBox(screwdriver, button, modifiers, kind));
            Assert.True(In(w, () => screwdriver.loc == her.back), $"{button}+{modifiers} on {kind} moved it");
        }
        Assert.Equal(runtimes, w.World.RuntimeErrorCount);
    }
}
