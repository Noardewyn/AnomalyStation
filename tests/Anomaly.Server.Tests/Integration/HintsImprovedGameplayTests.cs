using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// ui-overhaul.md section 12, "The hints, improved" (DV697, protocol version 100's ContextHints tail), through a real server and a headless client:
/// a human holding a welding tool looks at a closed closet next to her: the answer carries tg's welder line (LMB "Weld") and the world interact key's
/// line, tg's empty-hand line "Open", and the welder's own lines for the hint strip (its attack_self, and quick equip would put it on the belt slot
/// or in a pocket, asked without moving it); she drops the welder with the drop key and the client's change of held item hovers the closet again at
/// once (no new look, well inside the half-second repeat): the fresh answer's left click is the empty hand's "Open" and there is no empty-hand line
/// or held line any more. (An airlock shows no lines in the port: tg's door screentips, add_context, are deferred with the other objects', D264, D291.)
/// </summary>
public class HintsImprovedGameplayTests
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    [Fact]
    public void A_welder_on_a_closet_shows_weld_and_the_empty_hands_open_and_a_drop_refreshes_the_hints_without_a_new_look()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 4501, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var body = (mob_living_carbon_human)w.MobOf(alice);
        int z = (int)In(w, () => Dm.num(body.z));
        const int X = 10, Y = 10;
        (obj_structure_closet closet, obj_item_weldingtool welder) = In(w, () =>
        {
            for (int x = X - 1; x <= X + 2; x++)
            {
                for (int y = Y - 1; y <= Y + 1; y++)
                {
                    turf cell = locate(x, y, z);
                    foreach (obj thing in Dm.each_cast<obj>(cell).ToList())
                        qdel(thing);
                    if (cell is not turf_open_floor_iron)
                        Waiting.NoSleep(cell.ChangeTurf(typeof(turf_open_floor_iron)));
                }
            }
            foreach (obj_item item in new[] { body.belt, body.back, body.l_store, body.r_store, body.s_store, body.get_active_held_item() as obj_item, body.get_inactive_held_item() as obj_item })
                if (item != null)
                    qdel(item);
            if (body.w_uniform == null)
                Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(body), ITEM_SLOT_ICLOTHING))));
            Waiting.NoSleep(body.forceMove(locate(X, Y, z)));
            var box = Dm.@new<obj_structure_closet>(locate(X + 1, Y, z));
            var tool = Dm.@new<obj_item_weldingtool>(locate(X, Y, z));
            Assert.True(Dm.truthy(Waiting.NoSleep(body.put_in_active_hand(tool))));
            return (box, tool);
        });
        w.Run(Second);
        Assert.False(In(w, () => Dm.truthy(closet.opened)), "the closet stands closed");
        int runtimes = w.World.RuntimeErrorCount;
        var hints = new List<ContextHintsMessage>();
        alice.Client.ContextHintsReceived += hints.Add;
        alice.Client.ContextHintsReceived += alice.Session.HintsArrived; // as the game scene's OnHints hands them to the session
        var target = new ClickTarget(TargetKind.Entity, closet.dm_id, 0, 0, 0, 16, 16);
        ContextHintsMessage Newest() => hints.LastOrDefault(h => h.TargetKind == TargetKind.Entity && h.Target == closet.dm_id);
        int hand = In(w, () => (int)Dm.num(body.active_hand_index));
        alice.Session.HandsNow(hand, welder.dm_id, false); // what the client's HUD shows: the welder in the active hand

        // the look: tg's welder line and the empty hand's line, the welder's own lines
        Assert.True(alice.Session.Hover(target, w.Now));
        w.Run(4);
        ContextHintsMessage holding = Newest();
        Assert.NotNull(holding);
        Assert.Contains(holding.Hints, h => h.Button == ClickButton.Left && h.Modifiers == ClickModifiers.None && h.Label == "Weld");
        Assert.Equal("Open", holding.EmptyHand);
        Assert.NotNull(holding.HeldUse); // tg's welder lights on its attack_self
        Assert.True(holding.HeldEquips, "tg's quick equip would put the welder on the belt slot or in a pocket");
        Assert.Same(welder, In(w, () => body.get_active_held_item())); // the question moved nothing
        Assert.False(In(w, () => Dm.truthy(closet.opened)));
        List<PromptLine> lines = KeyChips.Prompts(holding, KeyBindings.Defaults());
        Assert.Equal(new PromptLine("LMB", "Weld"), lines[0]);
        Assert.Equal(new PromptLine("E", "Open"), lines[1]);
        List<PromptLine> strip = LiveHud.HintLines(KeyBindings.Defaults(), alice.Session.Held);
        Assert.Equal("Z", strip[0].Key);
        Assert.Contains(new PromptLine("Q", "Drop"), strip);
        Assert.Contains(new PromptLine("R", "Equip"), strip);

        // the drop key; the client sees its hand empty and asks again at once, still looking at the closet
        alice.Session.Keybinding(KeybindingCode.DropItem);
        w.Run(2);
        Assert.Null(In(w, () => body.get_active_held_item()));
        Assert.False(alice.Session.Hover(target, w.Now), "within the repeat interval the same look sends nothing by itself");
        int answers = hints.Count;
        alice.Session.HandsNow(hand, 0, false);
        Assert.Null(alice.Session.Held);
        Assert.True(alice.Session.Hover(target, w.Now), "the change of held item hovers the closet again");
        w.Run(4);
        Assert.True(hints.Count > answers, "a fresh answer came");
        ContextHintsMessage empty = Newest();
        Assert.Contains(empty.Hints, h => h.Button == ClickButton.Left && h.Modifiers == ClickModifiers.None && h.Label == "Open");
        Assert.DoesNotContain(empty.Hints, h => h.Label == "Weld");
        Assert.Null(empty.EmptyHand);
        Assert.Null(empty.HeldUse);
        Assert.False(empty.HeldEquips);
        Assert.Equal(new PromptLine("LMB", "Open"), KeyChips.Prompts(empty, KeyBindings.Defaults())[0]);
        Assert.DoesNotContain(KeyChips.Prompts(empty, KeyBindings.Defaults()), l => l.Key == "E");
        Assert.Equal(runtimes, w.World.RuntimeErrorCount);
    }
}
