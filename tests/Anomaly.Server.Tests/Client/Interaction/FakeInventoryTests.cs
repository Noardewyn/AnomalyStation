using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Interaction;

/// <summary>
/// M4a P7: the fake server's version 4 (FakeServerOptions.Inventory) end to end through the codec, the replica and the play session: tg's hands and
/// slots as screen entries with their groups, a pick-up, swap, drop, an open toolbox (the container layout from tg's own storage screen_locs), a throw
/// flagged Thrown, the welding progress bar, and the hints of a hover.
/// </summary>
public class FakeInventoryTests
{
    private static (PlayRig Rig, FakeFeed Feed, FakeServer Server) Start(int bots = 0)
    {
        var feed = new FakeFeed("Tester", seed: 1, size: 32, bots: bots, inventory: true);
        var rig = new PlayRig(feed);
        rig.Run(0.5);
        return (rig, feed, feed.Server);
    }

    private static ClickTarget Entity(long id) => new(TargetKind.Entity, id, 0, 0, 0, 16, 16);

    private static ClickTarget Screen(long id) => new(TargetKind.Screen, id, 0, 0, 0, 16, 16);

    private static void Click(PlayRig rig, ClickTarget target, ClickButton button = ClickButton.Left, ClickModifiers modifiers = ClickModifiers.None, double seconds = 0.4)
    {
        Assert.True(rig.Session.Click(target, button, modifiers));
        rig.Run(seconds);
    }

    private static void Key(PlayRig rig, KeybindingCode code, bool up = false)
    {
        rig.Session.Keybinding(code, up);
        rig.Run(0.3);
    }

    [Fact]
    public void TheHandsAndTheFifteenSlotsArriveAsTgsScreenEntriesInTheirGroups()
    {
        (PlayRig rig, FakeFeed feed, _) = Start();
        ReplicaWorld world = feed.Replica;
        Assert.Equal(new long[] { 2001, 2002, 2003, 2004 }, world.ScreenGroup(HudGroup.Static).Select(e => e.Id));
        Assert.Equal(new long[] { 2005 }, world.ScreenGroup(HudGroup.Hotkeys).Select(e => e.Id));
        IReadOnlyList<ScreenEntry> slots = world.ScreenGroup(HudGroup.ToggleableInventory);
        Assert.Equal(15, slots.Count);
        Assert.StartsWith("CENTER+-1:", world.Screen[2001].ScreenLoc, StringComparison.Ordinal); // ui_hand_position with a negative offset: the left hand (DV212)
        Assert.True(ScreenLoc.TryParse(world.Screen[2001].ScreenLoc, out ScreenLocSpec hand));
        Assert.Equal(-1, hand.X.Tile);
        Assert.Empty(world.ScreenGroup(HudGroup.Storage));

        var inventory = new ContainerLayout(HudGroup.ToggleableInventory);
        inventory.Rebuild(slots, world);
        Assert.True(inventory.IsOpen);
        Assert.Equal(15, inventory.Cells.Count);
        Assert.All(inventory.Cells, c => Assert.Equal(ContainerCellRole.Slot, c.Role));
        Assert.All(inventory.Cells, c => Assert.NotEqual(string.Empty, c.Label)); // D70: every slot has a text label
        Assert.Contains(inventory.Cells, c => c.Label == "Belt");
        Assert.Contains(inventory.Cells, c => c.Label == "Left pocket" || c.Label == "Pocket");
        // D70: no two slots share a grid position.
        Assert.Equal(inventory.Cells.Count, inventory.Cells.Select(c => (c.Column, c.Row)).Distinct().Count());
        _ = rig;
    }

    [Fact]
    public void AHoverGetsTgsContextForTheTargetAndTheHeldItem()
    {
        (PlayRig rig, FakeFeed feed, FakeServer server) = Start();
        var received = new List<ContextHintsMessage>();
        feed.ContextHintsReceived += received.Add;
        Assert.True(rig.Session.Hover(Entity(server.ToolboxId), rig.Now));
        rig.Run(0.3);
        ContextHintsMessage toolbox = Assert.Single(received);
        Assert.Equal((TargetKind.Entity, server.ToolboxId, "mechanical toolbox"), (toolbox.TargetKind, toolbox.Target, toolbox.Name));
        Assert.Equal(new[] { "Pick up", "Open" }, toolbox.Hints.Select(h => h.Label));
        Assert.Equal(ClickButton.Right, toolbox.Hints[1].Button);

        // The airlock offers "Weld shut" only to a lit welder in the active hand.
        received.Clear();
        rig.Session.Hover(Entity(server.AirlockId), rig.Now);
        rig.Run(0.3);
        Assert.Equal(new[] { "Open" }, Assert.Single(received).Hints.Select(h => h.Label));

        // The session sends one hover per change, no faster than ten a second.
        int sent = rig.Session.HoversSent;
        for (int i = 0; i < 6; i++)
            rig.Session.Hover(i % 2 == 0 ? Entity(server.ToolboxId) : Entity(server.AirlockId), rig.Now + i * 0.01);
        Assert.True(rig.Session.HoversSent - sent <= 1);
    }

    [Fact]
    public void ALeftClickPicksUpIntoTheActiveHandAndTheDropKeyPutsItBackOnTheFloor()
    {
        (PlayRig rig, FakeFeed feed, FakeServer server) = Start();
        long toolbox = server.ToolboxId;
        Assert.True(feed.Replica.TryGet(toolbox, out _));
        Click(rig, Entity(toolbox));
        Assert.Equal(toolbox, server.HeldId(0));
        Assert.False(feed.Replica.TryGet(toolbox, out _)); // in a hand it is not on a turf: not replicated
        // The hand slot's entry now shows the toolbox (a new private appearance with an overlay).
        Assert.True(feed.Replica.TryGetAppearance(feed.Replica.Screen[2001].Appearance, out AppearanceEntry handApp));
        Assert.True(handApp.OverlayCount >= 1);

        Key(rig, KeybindingCode.SwapHands);
        Assert.Equal(1, server.ActiveHand);
        Assert.Equal(0, server.HeldId(1));
        Key(rig, KeybindingCode.SwapHands);
        Assert.Equal(0, server.ActiveHand);

        Key(rig, KeybindingCode.DropItem);
        Assert.Equal(0, server.HeldId(0));
        Assert.True(feed.Replica.TryGet(toolbox, out ReplicaEntity dropped));
        Assert.Equal(feed.Replica.Entities.Single(e => e.Id == feed.OwnEntity).State.X, dropped.State.X); // dropped on the player's tile
        Assert.True(feed.Replica.TryGetAppearance(dropped.State.Appearance, out AppearanceEntry look));
        Assert.InRange(look.PixelX, -6, 6);
        Assert.InRange(look.PixelY, -6, 6);
    }

    [Fact]
    public void AnOpenToolboxIsTgsStorageScreenObjectsAndTheLayoutReadsThem()
    {
        (PlayRig rig, FakeFeed feed, FakeServer server) = Start();
        ReplicaWorld world = feed.Replica;
        Click(rig, Entity(server.ToolboxId), ClickButton.Right); // a right click opens the box where it lies
        Assert.Equal(server.ToolboxId, server.OpenStorageId);
        IReadOnlyList<ScreenEntry> entries = world.ScreenGroup(HudGroup.Storage);
        Assert.Equal(9 + 5, entries.Count); // tg's nine interface objects and the five tools
        // tg's own strings: the cells entry spans 7 columns of the one row, an item is "4:16,2:16" and on, the close button sits past the last column.
        Assert.Equal("5:-16,3:-16 to 11:-16,3:-16", world.Screen[2040].ScreenLoc);
        Assert.Equal("11:11,2:16", world.Screen[2048].ScreenLoc);

        var view = new ContainerLayout(HudGroup.Storage);
        view.Rebuild(entries, world);
        Assert.True(view.IsOpen);
        Assert.Equal((7, 1), (view.Columns, view.Rows));
        Assert.Equal(new[] { "Screwdriver", "Wrench", "Welder", "Crowbar", "Cutters" }, view.Cells.Where(c => c.Role == ContainerCellRole.Item).OrderBy(c => c.Column).Select(c => c.Label));
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, view.Cells.Where(c => c.Role == ContainerCellRole.Item).OrderBy(c => c.Column).Select(c => c.Column));
        Assert.Single(view.Cells, c => c.Role == ContainerCellRole.Slot);
        ContainerCell close = Assert.Single(view.Cells, c => c.Role == ContainerCellRole.Close);
        Assert.Equal(7, close.Column);
        Assert.Equal(7, view.Cells.Count(c => c.Role == ContainerCellRole.Other)); // the four corners, the two row joins and the up button
    }

    [Fact]
    public void ItemsGoOutAndInThroughTheirScreenEntriesAndTheCloseButtonShutsTheWindow()
    {
        (PlayRig rig, FakeFeed feed, FakeServer server) = Start();
        ReplicaWorld world = feed.Replica;
        Click(rig, Entity(server.ToolboxId), ClickButton.Right);
        long screwdriver = world.ScreenGroup(HudGroup.Storage).First(e => e.Id >= 400000).Id;
        Click(rig, Screen(screwdriver)); // the active (left) hand is empty: the screwdriver is taken
        Assert.Equal(screwdriver, server.HeldId(0));
        Assert.DoesNotContain(world.ScreenGroup(HudGroup.Storage), e => e.Id == screwdriver);
        Click(rig, Screen(2040)); // the cells with an item in hand: it goes back in
        Assert.Equal(0, server.HeldId(0));
        Assert.Contains(world.ScreenGroup(HudGroup.Storage), e => e.Id == screwdriver);
        Click(rig, Screen(2048)); // the close button
        Assert.Equal(0, server.OpenStorageId);
        Assert.Empty(world.ScreenGroup(HudGroup.Storage));
    }

    [Fact]
    public void TheHeldToolboxIsTheHandEntrysChildAndAScreenClickOnItWithTheOtherHandOpensIt()
    {
        (PlayRig rig, FakeFeed feed, FakeServer server) = Start();
        ReplicaWorld world = feed.Replica;
        Click(rig, Entity(server.ToolboxId));
        Assert.Equal(server.ToolboxId, server.HeldId(0));
        // Version 12: the left hand's entry lists the toolbox as its vis_contents child, drawn with the overlay's appearance.
        ScreenChild child = Assert.Single(world.Screen[2001].VisChildren.Items);
        Assert.Equal(server.ToolboxId, child.Id);
        Assert.True(world.TryGetAppearance(world.Screen[2001].Appearance, out AppearanceEntry hand));
        Assert.Contains(child.Appearance, hand.Overlays);
        Assert.Null(world.Screen[2002].VisChildren);

        Key(rig, KeybindingCode.SwapHands);
        Click(rig, Screen(server.ToolboxId)); // the item's own Click with the other hand empty: tg's storage opens
        Assert.Equal(server.ToolboxId, server.OpenStorageId);
        Assert.Equal(server.ToolboxId, server.HeldId(0));
        Assert.NotEmpty(world.ScreenGroup(HudGroup.Storage));
    }

    [Fact]
    public void ThrowModeThrowsTheHeldItemFlaggedThrownUntilItLands()
    {
        (PlayRig rig, FakeFeed feed, FakeServer server) = Start();
        ReplicaWorld world = feed.Replica;
        long welder = server.WelderId;
        Click(rig, Entity(welder));
        Assert.Equal(welder, server.HeldId(0));
        Key(rig, KeybindingCode.ToggleThrowMode);
        Assert.True(server.ThrowMode);
        ReplicaEntity own = world.Entities.Single(e => e.Id == feed.OwnEntity);
        int tx = own.State.X + 5, ty = own.State.Y;
        Assert.True(rig.Session.Click(new ClickTarget(TargetKind.Turf, 0, tx, ty, own.State.Z, 16, 16), ClickButton.Left, ClickModifiers.None));
        bool sawThrown = false;
        rig.Run(0.8, _ => sawThrown |= world.TryGet(welder, out ReplicaEntity e) && e.IsThrown);
        Assert.True(sawThrown, "the entity was never flagged Thrown");
        Assert.True(world.TryGet(welder, out ReplicaEntity landed));
        Assert.False(landed.IsThrown);
        Assert.Equal((tx, ty), (landed.State.X, landed.State.Y));
        Assert.False(server.ThrowMode); // throwing ends throw mode
        Assert.Equal(0, server.HeldId(0));
    }

    [Fact]
    public void WeldingTheAirlockShutShowsAProgressBarImageThatEnds()
    {
        (PlayRig rig, FakeFeed feed, FakeServer server) = Start();
        ReplicaWorld world = feed.Replica;
        Click(rig, Entity(server.WelderId));
        Key(rig, KeybindingCode.ActivateInhand); // the welder lights
        var received = new List<ContextHintsMessage>();
        feed.ContextHintsReceived += received.Add;
        rig.Session.Hover(Entity(server.AirlockId), rig.Now);
        rig.Run(0.3);
        Assert.Contains(Assert.Single(received).Hints, h => h.Label == "Weld shut" && h.Button == ClickButton.Right);

        Click(rig, Entity(server.AirlockId), ClickButton.Right, seconds: 0.5);
        List<ProgressBarInfo> bars = ProgressBars.Of(world);
        ProgressBarInfo bar = Assert.Single(bars);
        Assert.Equal(server.AirlockId, bar.AttachedTo);
        double first = bar.Fraction;
        rig.Run(0.6);
        Assert.True(Assert.Single(ProgressBars.Of(world)).Fraction > first);
        rig.Run(1.2);
        Assert.Empty(ProgressBars.Of(world));
        Assert.True(server.AirlockWelded);
    }

    [Fact]
    public void AClickTooFarAwayIsRefusedWithABalloonAlert()
    {
        (PlayRig rig, FakeFeed feed, FakeServer server) = Start();
        var texts = new List<ServerText>();
        feed.ServerTextReceived += texts.Add;
        server.Place(1, 3, 3); // far from the items near the spawn
        rig.Run(0.5);
        Click(rig, Entity(server.ToolboxId));
        Assert.Contains(texts, t => t.Type == BalloonModel.TextType);
        Assert.Equal(0, server.HeldId(0));
    }

    [Fact]
    public void TheOldOptionKeysOfHelpAndShoveStillReadAndTheFileIsWrittenWithTheNewOnes()
    {
        var errors = new List<string>();
        PlayOptions options = PlayOptions.Parse("bind.help=MouseMiddle\nbind.shove=MouseLeft\nhead_bob=false\n", errors);
        Assert.Empty(errors);
        Assert.Equal(new[] { "MouseMiddle" }, options.Bindings.KeysOf(PlayAction.PrimaryClick));
        Assert.Equal(new[] { "MouseLeft" }, options.Bindings.KeysOf(PlayAction.SecondaryClick));
        Assert.Empty(options.Bindings.KeysOf(PlayAction.MiddleClick)); // MouseMiddle went to the primary click
        string text = options.ToText();
        Assert.Contains("bind.primary_click=MouseMiddle", text);
        Assert.Contains("bind.secondary_click=MouseLeft", text);
        Assert.DoesNotContain("bind.help", text);
        Assert.DoesNotContain("bind.shove", text);
        // The round trip of the new file, and --bind=help=... of M2's scripts.
        PlayOptions again = PlayOptions.Parse(text);
        Assert.Equal(new[] { "MouseMiddle" }, again.Bindings.KeysOf(PlayAction.PrimaryClick));
        var fresh = new PlayOptions();
        Assert.True(fresh.TrySetBinding("shove", "MouseMiddle", out _));
        Assert.Equal(new[] { "MouseMiddle" }, fresh.Bindings.KeysOf(PlayAction.SecondaryClick));
        // A file written by M2 holds no key for the new actions: their tg defaults stand.
        PlayOptions m2 = PlayOptions.Parse("bind.help=MouseLeft\nbind.shove=MouseRight\n");
        Assert.Equal(new[] { "Q" }, m2.Bindings.KeysOf(PlayAction.DropItem));
        Assert.Equal(new[] { "R" }, m2.Bindings.KeysOf(PlayAction.ThrowMode));
        Assert.Equal(new[] { "X" }, m2.Bindings.KeysOf(PlayAction.SwapHand));
        Assert.Equal(new[] { "Z" }, m2.Bindings.KeysOf(PlayAction.ActivateInHand));
        Assert.Equal(new[] { "E" }, m2.Bindings.KeysOf(PlayAction.QuickEquip));
        Assert.Equal(new[] { "Space" }, m2.Bindings.KeysOf(PlayAction.MoveUpLevel)); // DV422: Space, which is hold throw mode for a mob that cannot change level
        Assert.Equal(new[] { "MouseMiddle" }, m2.Bindings.KeysOf(PlayAction.MiddleClick));
    }

    [Fact]
    public void EveryActionHasATitleAndAnId()
    {
        foreach (PlayAction action in Enum.GetValues<PlayAction>())
        {
            Assert.NotEmpty(KeyBindings.TitleOf(action));
            Assert.Equal(action, KeyBindings.ActionOfId(KeyBindings.IdOf(action)));
        }
    }
}
