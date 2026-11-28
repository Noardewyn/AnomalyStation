using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Interaction;

/// <summary>M4a P7: the radial's hold and tap timing and aiming (interaction.md 4.5, 6.3), the HUD pick (4.4), the feedback models (9) and the throw visual (D127).</summary>
public class ModelTests
{
    private static ContextHintsMessage Hints(params (ClickButton Button, ClickModifiers Modifiers, string Label)[] hints)
    {
        var message = new ContextHintsMessage { TargetKind = TargetKind.Entity, Target = 7, Name = "toolbox" };
        foreach ((ClickButton button, ClickModifiers modifiers, string label) in hints)
            message.Hints.Add(new ContextHint(button, modifiers, label));
        return message;
    }

    // ---- the radial ----

    [Fact]
    public void ARightPressReleasedWithinTheTapTimeIsATap()
    {
        var radial = new RadialModel();
        radial.Press(10.0);
        Assert.False(radial.Update(10.1));
        Assert.Equal(RadialPhase.Pressed, radial.Phase);
        Assert.Equal(RadialOutcome.Tap, radial.Release(10.2, out _));
        Assert.Equal(RadialPhase.Closed, radial.Phase);
        Assert.Equal(RadialOutcome.None, radial.Release(10.3, out _));
    }

    [Fact]
    public void AHoldOpensTheRadialOnceAndTheReleaseChoosesWhatTheMouseAimedAt()
    {
        var radial = new RadialModel();
        radial.Press(5.0);
        Assert.False(radial.Update(5.2));
        Assert.True(radial.Update(5.25));
        Assert.False(radial.Update(5.3)); // only once
        radial.SetEntries(RadialModel.Build(Hints((ClickButton.Left, ClickModifiers.None, "Pick up"), (ClickButton.Right, ClickModifiers.None, "Open")), pileCount: 1, targetIsEntity: true));
        // Entries: Pick up, Open, Examine, Throw at, Swap hand, Cancel: six, the first at the top, clockwise.
        Assert.Equal(6, radial.Entries.Count);
        radial.Motion(0, -60); // up
        Assert.Equal(0, radial.Highlight);
        radial.Motion(87, 10); // now 87 right and 50 up: 60 degrees clockwise from the top, the second of six
        Assert.Equal(1, radial.Highlight);
        Assert.Equal(RadialOutcome.Choice, radial.Release(6.0, out RadialEntry choice));
        Assert.Equal(("Open", RadialKind.Hint, ClickButton.Right), (choice.Label, choice.Kind, choice.Button));
    }

    [Fact]
    public void EveryClientEntryHasAnIconAndAPileItemShowsItsSprite()
    {
        List<RadialEntry> entries = RadialModel.Build(Hints((ClickButton.Left, ClickModifiers.None, "Pick up"), (ClickButton.Left, ClickModifiers.Alt, "Rename")), pileCount: 2, targetIsEntity: true);
        Assert.All(entries, e => Assert.NotNull(e.IconName));
        Assert.Equal(new[] { "hand-holding", "hand-pointer", "eye" }, entries.Take(3).Select(e => e.IconName));
        Assert.Null(new RadialEntry(RadialKind.Item, "crate", ItemId: 3).IconName);
    }

    [Fact]
    public void Version36HintActionsBecomeTheirRadialKinds()
    {
        var hints = new ContextHintsMessage { TargetKind = TargetKind.Entity, Target = 7, Name = "chair" };
        hints.Hints.Add(new ContextHint(ClickButton.Left, ClickModifiers.None, "Buckle", ContextHintAction.DropSelf));
        hints.Hints.Add(new ContextHint(ClickButton.Left, ClickModifiers.Ctrl, "Pull"));
        hints.Hints.Add(new ContextHint(ClickButton.Left, ClickModifiers.None, "Stop pulling", ContextHintAction.StopPulling));
        List<RadialEntry> entries = RadialModel.Build(hints, 1, true);
        Assert.Equal(new[] { RadialKind.DropSelf, RadialKind.Hint, RadialKind.StopPulling }, entries.Take(3).Select(e => e.Kind));
        Assert.Equal(("", "Ctrl-LMB"), (entries[0].Chord, entries[1].Chord));
    }

    [Fact]
    public void Version37DropOntoSelfBecomesTheStripKind()
    {
        var hints = new ContextHintsMessage { TargetKind = TargetKind.Entity, Target = 7, Name = "man" };
        hints.Hints.Add(new ContextHint(ClickButton.Left, ClickModifiers.Ctrl, "Pull"));
        hints.Hints.Add(new ContextHint(ClickButton.Left, ClickModifiers.None, "Strip", ContextHintAction.DropOntoSelf));
        List<RadialEntry> entries = RadialModel.Build(hints, 1, true);
        Assert.Equal(new[] { RadialKind.Hint, RadialKind.DropOntoSelf }, entries.Take(2).Select(e => e.Kind));
        Assert.Equal(("Strip", ""), (entries[1].Label, entries[1].Chord));
    }

    [Fact]
    public void ReleasingInTheCentreOrWithoutAimingCancels()
    {
        var radial = new RadialModel();
        radial.Press(0);
        radial.Update(0.3);
        radial.SetEntries(RadialModel.Build(null, 1, false));
        radial.Motion(5, 5);
        Assert.Equal(-1, radial.Highlight);
        Assert.Equal(RadialOutcome.Cancel, radial.Release(1.0, out _));
        // Held past the tap time but released before the scene saw the frame that opens it: still no tap.
        radial.Press(2.0);
        Assert.Equal(RadialOutcome.Cancel, radial.Release(2.3, out _));
    }

    [Fact]
    public void TheAimingVectorIsClampedAndEntriesAreSpreadFromTheTop()
    {
        var radial = new RadialModel();
        radial.Press(0);
        radial.Update(1);
        radial.SetEntries(RadialModel.Build(null, 3, true));
        radial.Motion(100000, 0);
        Assert.Equal(RadialModel.MaxReach, radial.Aim.X, 6);
        Assert.Equal(0, RadialModel.IndexAt(0, -50, 4, RadialModel.DeadZone));
        Assert.Equal(1, RadialModel.IndexAt(50, 0, 4, RadialModel.DeadZone));
        Assert.Equal(2, RadialModel.IndexAt(0, 50, 4, RadialModel.DeadZone));
        Assert.Equal(3, RadialModel.IndexAt(-50, 0, 4, RadialModel.DeadZone));
        (double x, double y) = RadialModel.PositionOf(0, 4, 100);
        Assert.Equal((0.0, -100.0), (Math.Round(x, 6), Math.Round(y, 6)));
    }

    /// <summary>
    /// Slice T4c: the input script's radial=LABEL aims the open radial at the entry of that label (case and outer spaces ignored), so the release
    /// chooses exactly that entry, with the dispatch a mouse release has (here the Extra entry the server offers, Turn over); an absent label
    /// aims at the centre and the release cancels; a radial that is not open aims at nothing.
    /// </summary>
    [Fact]
    public void AnInputScriptPicksTheRadialsEntryByItsLabel()
    {
        var hints = new ContextHintsMessage { TargetKind = TargetKind.Entity, Target = 7 };
        hints.Hints.Add(new ContextHint(ClickButton.Left, ClickModifiers.None, "Hug"));
        hints.Hints.Add(new ContextHint(ClickButton.Left, ClickModifiers.None, "Turn over", ContextHintAction.Extra));
        hints.Hints.Add(new ContextHint(ClickButton.Left, ClickModifiers.Ctrl, "Pull"));
        var radial = new RadialModel();
        Assert.False(radial.AimAtLabel("Turn over")); // closed
        radial.Press(0);
        Assert.False(radial.AimAtLabel("Turn over")); // pressed, not open yet
        radial.Update(0.3);
        radial.SetEntries(RadialModel.Build(hints, 1, true));
        Assert.True(radial.AimAtLabel("  turn OVER "));
        Assert.Equal(RadialOutcome.Choice, radial.Release(0.5, out RadialEntry choice));
        Assert.Equal((RadialKind.Extra, "Turn over"), (choice.Kind, choice.Label));

        // every entry, whichever its place on the circle, is the one aimed
        List<RadialEntry> entries = RadialModel.Build(hints, 3, true);
        foreach (RadialEntry wanted in entries)
        {
            radial.Press(1);
            radial.Update(1.3);
            radial.SetEntries(entries);
            Assert.True(radial.AimAtLabel(wanted.Label), wanted.Label);
            Assert.Equal(RadialOutcome.Choice, radial.Release(1.5, out choice));
            Assert.Equal(wanted, choice);
        }

        // a label not offered: no entry, the release cancels, even after the mouse had aimed somewhere
        radial.Press(2);
        radial.Update(2.3);
        radial.SetEntries(RadialModel.Build(hints, 1, true));
        radial.Motion(100, 0);
        Assert.NotEqual(-1, radial.Highlight);
        Assert.False(radial.AimAtLabel("Strip"));
        Assert.Equal(-1, radial.Highlight);
        Assert.Equal(RadialOutcome.Cancel, radial.Release(2.5, out _));
    }

    [Fact]
    public void TheEntriesAreTgsContextThenTheFixedOnes()
    {
        var entries = RadialModel.Build(Hints((ClickButton.Left, ClickModifiers.None, "Pick up"), (ClickButton.Left, ClickModifiers.Shift, "Look")), pileCount: 3, targetIsEntity: true);
        Assert.Equal(new[] { RadialKind.Hint, RadialKind.Hint, RadialKind.Examine, RadialKind.ThrowAt, RadialKind.SwapHand, RadialKind.ThingsHere, RadialKind.Cancel }, entries.Select(e => e.Kind));
        Assert.Equal(ClickModifiers.Shift, entries[2].Modifiers); // Examine is a Shift-left click
        var bare = RadialModel.Build(null, pileCount: 1, targetIsEntity: false); // a floor cell, one thing or none
        Assert.Equal(new[] { RadialKind.ThrowAt, RadialKind.SwapHand, RadialKind.Sleep, RadialKind.Succumb, RadialKind.Cancel }, bare.Select(e => e.Kind)); // D240: the verbs over a turf
        Assert.Equal("Shift-LMB", entries[1].Chord);
        Assert.Equal("Ctrl-Alt-RMB", new RadialEntry(RadialKind.Hint, "x", ClickButton.Right, ClickModifiers.Ctrl | ClickModifiers.Alt).Chord);
        Assert.Equal(string.Empty, entries[3].Chord);
    }

    // ---- the HUD pick ----

    private sealed class Surface : IHudSurface
    {
        public List<HudDrawn> Drawn { get; } = new();

        public void Collect(List<HudDrawn> into) => into.AddRange(Drawn);
    }

    [Fact]
    public void TheHudPickTakesTheTopmostOpaquePixelAndGivesTgsPixelInTheIcon()
    {
        var surface = new Surface();
        surface.Drawn.Add(new HudDrawn(1, new HudRect(0, 0, 64, 64), Raster.Solid(32, 32, 0xFFFFFFFF), 0, 0));
        // A second entry on top, whose upper left quarter is clear.
        var top = new Raster(32, 32);
        for (int row = 16; row < 32; row++)
        {
            for (int col = 0; col < 32; col++)
                top.Pixels[(row * 32 + col) * 4 + 3] = 255;
        }
        surface.Drawn.Add(new HudDrawn(2, new HudRect(0, 0, 64, 64), top, 0, 0));
        var picker = new HudPicker(surface);
        PickHit lower = picker.PickScreen(10, 10); // the top entry is clear there: the one below
        Assert.Equal((PickSurface.Screen, TargetKind.Screen, 1L), (lower.Surface, lower.Kind, lower.Id));
        PickHit upper = picker.PickScreen(10, 50);
        Assert.Equal(2, upper.Id);
        // 64 pixels for 32: a point 10 across and 50 down is picture pixel 5 of 32, row 25 from the top, so 7 from the bottom (+1).
        Assert.Equal((6, 7), (upper.IconX, upper.IconY));
        Assert.False(picker.PickScreen(100, 10).IsHit);
    }

    [Fact]
    public void TheIconPixelCountsFromTheIconSquareNotThePicture()
    {
        var surface = new Surface();
        // A 40 x 40 picture whose icon square starts 4 pixels in from the left and 2 up from the bottom, drawn at one pixel per pixel.
        surface.Drawn.Add(new HudDrawn(5, new HudRect(100, 100, 40, 40), Raster.Solid(40, 40, 0xFFFFFFFF), 4, 2));
        PickHit hit = new HudPicker(surface).PickScreen(100 + 4, 100 + 40 - 2 - 1); // the icon square's lower left pixel
        Assert.Equal((1, 1), (hit.IconX, hit.IconY));
    }

    // Protocol version 12 (BYOND's vis_contents rule): a hand slot (id 10) drawn with its held item (id 400123), a 32 x 32 picture at two
    // screen pixels per pixel; the item covers the left half of the square only.
    private static (Surface Surface, Raster Item) HandWithItem(bool wholeBox = false, int shiftX = 0, Raster holderImage = null)
    {
        var item = new Raster(32, 32);
        for (int row = 0; row < 32; row++)
        {
            for (int col = 0; col < 16; col++)
                item.Pixels[(row * 32 + col) * 4 + 3] = (byte)(col == 0 ? 1 : 255); // its edge column is nearly clear, still opaque to BYOND
        }
        var surface = new Surface();
        surface.Drawn.Add(new HudDrawn(10, new HudRect(0, 0, 64, 64), holderImage ?? Raster.Solid(32, 32, 0xFFFFFFFF), 0, 0,
            new[] { new HudChildDrawn(400123, item, 0, 0, shiftX, 0, wholeBox) }));
        return (surface, item);
    }

    [Fact]
    public void AClickOnTheItemsPixelInAHandIsOnTheItemAndOnAClearPixelOnTheSlot()
    {
        var picker = new HudPicker(HandWithItem().Surface);
        PickHit onItem = picker.PickScreen(10, 50);
        Assert.Equal((TargetKind.Screen, 400123L), (onItem.Kind, onItem.Id));
        Assert.Equal((6, 7), (onItem.IconX, onItem.IconY)); // the pixel within the item's icon, which sits on the slot's square
        Assert.Equal(400123, picker.PickScreen(1, 1).Id); // alpha 1 is opaque to BYOND's mouse
        PickHit onSlot = picker.PickScreen(50, 50); // the item is clear there: the slot itself
        Assert.Equal((TargetKind.Screen, 10L), (onSlot.Kind, onSlot.Id));

        // mouse_opacity 2: the item's whole box takes the click.
        Assert.Equal(400123, new HudPicker(HandWithItem(wholeBox: true).Surface).PickScreen(50, 50).Id);
        // A holder moved by its pixel offset moves its child with it: 8 pixels right, the item's left half now covers the slot's pixels 8 to 23.
        var shifted = new HudPicker(HandWithItem(shiftX: 8).Surface);
        Assert.Equal(10, shifted.PickScreen(10, 50).Id);
        Assert.Equal(400123, shifted.PickScreen(40, 50).Id);
        // A clear slot picture with no item pixel there is no hit at all; over the item, the item is hit.
        var clear = new HudPicker(HandWithItem(holderImage: new Raster(32, 32)).Surface);
        Assert.False(clear.PickScreen(50, 50).IsHit);
        Assert.Equal(400123, clear.PickScreen(10, 50).Id);
    }

    [Fact]
    public void AWindowCellsWornItemIsHitBeforeTheCellsBox()
    {
        var item = Raster.Solid(16, 16, 0xFFFFFFFF); // a small item in the icon square's lower left corner
        var surface = new Surface();
        surface.Drawn.Add(new HudDrawn(2014, new HudRect(0, 0, 32, 32), null, 0, 0, new[] { new HudChildDrawn(400200, item, 0, 0) }));
        var picker = new HudPicker(surface);
        Assert.Equal(400200, picker.PickScreen(4, 28).Id); // the lower left
        Assert.Equal(2014, picker.PickScreen(28, 4).Id); // the cell's box elsewhere
    }

    [Fact]
    public void AnEntryWithNoPictureGivesTheIconPixelUnderEveryScreenPixelOnBothAxes()
    {
        // a window cell (ContainerWindow) or the live HUD's doll square: no picture, the rectangle is the icon square, here at 2 screen pixels per
        // tg pixel; a screen pixel on the top line of a row is in that row (it came back as the row above)
        var surface = new Surface();
        surface.Drawn.Add(new HudDrawn(2014, new HudRect(10, 20, 64, 64), null, 0, 0));
        var picker = new HudPicker(surface);
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                PickHit hit = picker.PickScreen(10 + x, 20 + y);
                Assert.Equal(((byte)(x / 2 + 1), (byte)(32 - y / 2)), (hit.IconX, hit.IconY));
            }
        }
    }

    [Fact]
    public void AnItemTooBigForItsSlotIsClickedWhereItIsDrawnFittedIntoTheSquare()
    {
        // DV575: a 64 x 64 item drawn at half its size in the slot's square; only its top right quarter has pixels
        var pixels = new byte[64 * 64 * 4];
        for (int row = 0; row < 32; row++)
            for (int col = 32; col < 64; col++)
                pixels[(row * 64 + col) * 4 + 3] = 255;
        Assert.Equal(0.5f, HudChildDrawn.FitOf(64, 64));
        Assert.Equal(0f, HudChildDrawn.FitOf(32, 32));
        var surface = new Surface();
        surface.Drawn.Add(new HudDrawn(2015, new HudRect(0, 0, 32, 32), null, 0, 0,
            new[] { new HudChildDrawn(400201, new Raster(64, 64, pixels), 0, 0, Fit: HudChildDrawn.FitOf(64, 64)) }));
        var picker = new HudPicker(surface);
        Assert.Equal(400201, picker.PickScreen(28, 4).Id); // the square's top right: the item's top right, shrunk into it
        Assert.Equal(2015, picker.PickScreen(4, 28).Id); // its clear lower left is the slot's
        Assert.Equal(2015, picker.PickScreen(31, 31).Id);
    }

    // ---- the feedback ----

    [Fact]
    public void ChatKeepsTheLastSixLinesAndFadesThem()
    {
        var chat = new ChatFeed();
        for (int i = 1; i <= 8; i++)
            chat.Add("line " + i, i);
        Assert.Equal(ChatFeed.Capacity, chat.Count);
        List<ChatLine> shown = chat.Visible(8);
        Assert.Equal("line 3", shown[0].Text);
        Assert.Equal("line 8", shown[^1].Text);
        Assert.All(shown, l => Assert.Equal(1f, l.Alpha));
        List<ChatLine> fading = chat.Visible(8 + ChatFeed.StaySeconds + 1.0);
        Assert.Contains(fading, l => l.Alpha is > 0f and < 1f);
        Assert.Empty(chat.Visible(8 + ChatFeed.StaySeconds + ChatFeed.FadeSeconds + 1));
        chat.Add("   ", 20);
        Assert.Equal(ChatFeed.Capacity, chat.Count);
    }

    [Fact]
    public void ABalloonShowsForAWhileAndTheNewestReplacesTheOld()
    {
        var balloon = new BalloonModel();
        Assert.Null(balloon.Current(0, out _));
        balloon.Add("Too far away!", 1.0);
        Assert.Equal("Too far away!", balloon.Current(2.0, out float alpha));
        Assert.Equal(1f, alpha);
        balloon.Add("Your hand is full", 2.5);
        Assert.Equal("Your hand is full", balloon.Current(3.0, out _));
        Assert.Null(balloon.Current(2.5 + BalloonModel.StaySeconds + 0.1, out _));
    }

    [Theory]
    [InlineData("prog_bar_0", true, 0.0)]
    [InlineData("prog_bar_45", true, 0.45)]
    [InlineData("prog_bar_100", true, 1.0)]
    [InlineData("prog_bar_45_fail", false, 0.0)]
    [InlineData("screwdriver", false, 0.0)]
    [InlineData(null, false, 0.0)]
    public void TgsProgressBarStatesAreRead(string state, bool ok, double fraction)
    {
        Assert.Equal(ok, ProgressBars.TryFraction(state, out double read));
        Assert.Equal(fraction, read, 6);
    }

    [Fact]
    public void TheThrownArcRisesAndFallsAndTheSpinNeverVanishes()
    {
        Assert.Equal(0.0, ThrowVisual.Height(0), 6);
        Assert.Equal(ThrowVisual.PeakMetres, ThrowVisual.Height(ThrowVisual.ArcSeconds / 2), 6);
        Assert.Equal(0.0, ThrowVisual.Height(ThrowVisual.ArcSeconds + 0.1), 6);
        Assert.True(ThrowVisual.Height(0.1) > 0 && ThrowVisual.Height(0.1) < ThrowVisual.Height(0.2));
        for (double t = 0; t < 2; t += 0.013)
            Assert.InRange(ThrowVisual.SpinScale(t), 0.2, 1.0);
        var clock = new ThrowClock();
        Assert.Equal(0, clock.Age(5, true, 10.0));
        Assert.Equal(0.5, clock.Age(5, true, 10.5), 6);
        Assert.Equal(1, clock.InFlight);
        Assert.Equal(0, clock.Age(5, false, 11.0));
        Assert.Equal(0, clock.InFlight);
    }

    [Fact]
    public void AnOldClickTargetIsASetTargetWithItsPixelAndTheSameThingIgnoresThePixel()
    {
        var hit = new PickHit(PickSurface.Floor, TargetKind.Turf, 0, 12, 34, 1, 3, 0, 0, 0, 9, 21);
        ClickTarget turf = ClickTarget.Of(hit);
        Assert.Equal((TargetKind.Turf, 12, 34, 1, (byte)9, (byte)21), (turf.Kind, turf.X, turf.Y, turf.Z, turf.IconX, turf.IconY));
        Assert.True(turf.SameThing(turf with { IconX = 30 }));
        Assert.False(turf.SameThing(turf with { X = 13 }));
        Assert.False(ClickTarget.Of(PickHit.None).IsSet);
        Assert.Equal(new ClickTarget(TargetKind.Entity, 77, 0, 0, 0, 3, 4), ClickTarget.Of(new PickHit(PickSurface.Billboard, TargetKind.Entity, 77, 5, 5, 1, 1, 0, 0, 0, 3, 4)));
    }
}
