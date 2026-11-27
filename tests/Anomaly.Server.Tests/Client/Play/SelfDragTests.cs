using Anomaly.Client.Shared;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// DV691 (ui-overhaul.md section 17): the first-person drag of oneself. A plain left press with the crosshair on the own body sends nothing
/// and is held; the release on a thing drops the own mob onto it, on the own body is the click on oneself with the press's zone, on a cell,
/// nothing or a thing out of reach is nothing. Any other press, a modified press and cursor mode keep the click on the press. Tab, a lost
/// view and the right button drop the held drag.
/// </summary>
public class SelfDragTests
{
    private const long Own = 25;
    private static readonly ClickTarget OwnLeg = new(TargetKind.Entity, Own, 0, 0, 0, 17, 2, AimZone.LeftLeg, (3, 4));
    private static readonly ClickTarget OwnGroin = new(TargetKind.Entity, Own, 0, 0, 0, 17, 14, AimZone.Groin, (3, 4));
    private static readonly ClickTarget Table = new(TargetKind.Entity, 40, 0, 0, 0, 16, 20);
    private static readonly ClickTarget Floor = new(TargetKind.Turf, 0, 19, 21, 1, 16, 20);
    private static readonly ClickTarget Ceiling = new(TargetKind.Ceiling, 0, 19, 21, 1);

    [Fact]
    public void APlainPressOnTheOwnBodyIsHeldAndOnlyThere()
    {
        var drag = new SelfDrag();
        Assert.True(drag.Press(OwnLeg, Own, false, ClickModifiers.None, 1.0));
        Assert.True(drag.Holding);
        Assert.Equal(OwnLeg, drag.Pressed);

        // anything else, a modified press (examine, the grasp, Alt) or cursor mode: the click on the press, nothing held
        Assert.False(drag.Press(Table, Own, false, ClickModifiers.None, 2.0));
        Assert.False(drag.Holding);
        Assert.False(drag.Press(Floor, Own, false, ClickModifiers.None, 2.0));
        Assert.False(drag.Press(ClickTarget.None, Own, false, ClickModifiers.None, 2.0));
        foreach (ClickModifiers modified in new[] { ClickModifiers.Shift, ClickModifiers.Ctrl, ClickModifiers.Alt, ClickModifiers.Alt | ClickModifiers.Shift })
            Assert.False(drag.Press(OwnLeg, Own, false, modified, 2.0));
        Assert.False(drag.Press(OwnLeg, Own, true, ClickModifiers.None, 2.0));
        Assert.False(drag.Press(OwnLeg, 0, false, ClickModifiers.None, 2.0)); // no own mob known
        Assert.False(drag.Holding);
    }

    [Fact]
    public void LetGoOnAThingDropsTheOwnMobOntoIt()
    {
        var drag = new SelfDrag();
        drag.Press(OwnLeg, Own, false, ClickModifiers.None, 1.0);
        Assert.Equal(SelfDragOutcome.Drop, drag.Release(Table, Own, false, 1.83, out DragRelease drop));
        Assert.Equal((OwnLeg, Table, ClickButton.Left, ClickModifiers.None, (byte)8), (drop.Source, drop.Over, drop.Button, drop.Modifiers, drop.DragTime));
        Assert.False(drag.Holding);
        // the release is the end of it: a second release sends nothing
        Assert.Equal(SelfDragOutcome.None, drag.Release(Table, Own, false, 2.0, out _));
    }

    [Fact]
    public void LetGoStillOnTheOwnBodyIsTheClickWithThePressesZone()
    {
        var drag = new SelfDrag();
        drag.Press(OwnLeg, Own, false, ClickModifiers.None, 1.0);
        // the crosshair drifted to another part of the body: the click keeps the part pressed
        Assert.Equal(SelfDragOutcome.Click, drag.Release(OwnGroin, Own, false, 1.2, out DragRelease click));
        Assert.Equal((OwnLeg, OwnLeg, ClickButton.Left, ClickModifiers.None), (click.Source, click.Over, click.Button, click.Modifiers));
        Assert.Equal(AimZone.LeftLeg, click.Source.Aim);
        Assert.Equal(((byte)17, (byte)2), (click.Source.IconX, click.Source.IconY)); // the press's pixel, which gives the zone sent
    }

    [Fact]
    public void LetGoOnACellNothingOrAThingOutOfReachSendsNothing()
    {
        var drag = new SelfDrag();
        foreach (ClickTarget over in new[] { Floor, Ceiling, ClickTarget.None })
        {
            drag.Press(OwnLeg, Own, false, ClickModifiers.None, 1.0);
            Assert.Equal(SelfDragOutcome.None, drag.Release(over, Own, false, 1.5, out DragRelease none));
            Assert.Equal(default, none);
            Assert.False(drag.Holding);
        }
        drag.Press(OwnLeg, Own, false, ClickModifiers.None, 1.0);
        Assert.Equal(SelfDragOutcome.None, drag.Release(Table, Own, true, 1.5, out _));
        // out of reach does not stop the click on oneself
        drag.Press(OwnLeg, Own, false, ClickModifiers.None, 1.0);
        Assert.Equal(SelfDragOutcome.Click, drag.Release(OwnLeg, Own, true, 1.5, out _));
    }

    [Fact]
    public void TabALostViewOrTheRightButtonDropTheDragWithNothingSent()
    {
        var drag = new SelfDrag();
        drag.Press(OwnLeg, Own, false, ClickModifiers.None, 1.0);
        drag.Cancel(); // what the scene does on Tab, a window, the right button or a lost view
        Assert.False(drag.Holding);
        Assert.Equal(SelfDragOutcome.None, drag.Release(Table, Own, false, 1.5, out _));
        Assert.Equal(SelfDragOutcome.None, drag.Release(OwnLeg, Own, false, 1.5, out _));

        // what ends a held drag; looking away from the body does not (it is the gesture)
        drag.Press(OwnLeg, Own, false, ClickModifiers.None, 1.0);
        Assert.False(drag.Lost(Own, firstPerson: true, cursorMode: false, panels: false, bodyShowable: true, overlay: false, buttonDown: true));
        Assert.True(drag.Lost(Own, firstPerson: true, cursorMode: true, panels: false, bodyShowable: true, overlay: false, buttonDown: true)); // Tab or a window
        Assert.True(drag.Lost(Own, firstPerson: true, cursorMode: false, panels: true, bodyShowable: true, overlay: false, buttonDown: true));
        Assert.True(drag.Lost(Own, firstPerson: false, cursorMode: false, panels: false, bodyShowable: true, overlay: false, buttonDown: true));
        Assert.True(drag.Lost(Own, firstPerson: true, cursorMode: false, panels: false, bodyShowable: false, overlay: false, buttonDown: true)); // dead or stunned: lying
        // nothing is lost while nothing is held
        drag.Cancel();
        Assert.False(drag.Lost(Own, firstPerson: false, cursorMode: true, panels: true, bodyShowable: false, overlay: true, buttonDown: false));
    }

    [Fact]
    public void AnOverlayTakingTheMouseOrAReleaseThatWentElsewhereEndsTheDrag()
    {
        var drag = new SelfDrag();
        drag.Press(OwnLeg, Own, false, ClickModifiers.None, 1.0);
        // the emote wheel, a radial, the dev menu or a text field took the mouse
        Assert.True(drag.Lost(Own, firstPerson: true, cursorMode: false, panels: false, bodyShowable: true, overlay: true, buttonDown: true));
        // the left button is up but the release never ended the drag (an overlay took it, or the window lost the focus and Godot let it go)
        Assert.True(drag.Lost(Own, firstPerson: true, cursorMode: false, panels: false, bodyShowable: true, overlay: false, buttonDown: false));
    }

    [Fact]
    public void ABodySwapWhileHeldEndsTheDragAndTheOldBodyIsNeverSent()
    {
        const long newBody = 77;
        var drag = new SelfDrag();
        drag.Press(OwnLeg, Own, false, ClickModifiers.None, 1.0);
        Assert.True(drag.Lost(newBody, firstPerson: true, cursorMode: false, panels: false, bodyShowable: true, overlay: false, buttonDown: true));
        Assert.True(drag.Lost(0, firstPerson: true, cursorMode: false, panels: false, bodyShowable: true, overlay: false, buttonDown: true));
        // a release in the same frame as the swap, before the frame's check: nothing, neither the click on the old body nor its drop
        Assert.Equal(SelfDragOutcome.None, drag.Release(OwnLeg, newBody, false, 1.5, out DragRelease click));
        Assert.Equal(default, click);
        drag.Press(OwnLeg, Own, false, ClickModifiers.None, 2.0);
        Assert.Equal(SelfDragOutcome.None, drag.Release(Table, newBody, false, 2.5, out DragRelease drop));
        Assert.Equal(default, drop);
        Assert.False(drag.Holding);
    }

    [Fact]
    public void TheHintStripsPictureIsThePressedBodysAsTheDragMarkShowsIt()
    {
        var replica = new ReplicaWorld();
        replica.Apply(Msg.FlatMap(8, 8));
        Snapshot snapshot = Msg.Snapshot(10, Own, 0, new[] { "/mob/living/carbon/human" }, Msg.State(Own, 0, 3, 3) with { Appearance = TableRef.Public(0) });
        snapshot.Resources.Add("icons/mob/human/human.dmi");
        snapshot.Strings.Add("body");
        snapshot.Appearances.Add(new AppearanceEntry { Icon = TableRef.Public(0), IconState = TableRef.Public(0) });
        replica.Apply(snapshot);

        var drag = new SelfDrag();
        Assert.True(drag.HintPicture(replica).IsNone); // nothing held
        drag.Press(OwnLeg, Own, false, ClickModifiers.None, 1.0);
        Assert.Equal(TableRef.Public(0), drag.HintPicture(replica));
        Assert.Equal(DragModel.IconOf(replica, drag.Pressed), drag.HintPicture(replica));
        drag.Cancel();
        Assert.True(drag.HintPicture(replica).IsNone);
    }

    [Fact]
    public void TheBodyIsShowableAtAnyPitchUnlessItCannotBeShownAtAll()
    {
        // standing in first person: showable whatever the pitch (only drawn when looking down far enough)
        Assert.True(LookDown.CanShowOwnBody(true, true, false, false, false, false, false, false));
        Assert.False(LookDown.ShowsOwnBody(0, 90, 0.05, 1.6, true, true, false, false, false, false, false, false));
        Assert.False(LookDown.CanShowOwnBody(true, true, false, false, true, false, false, false)); // lying (dead, stunned)
        Assert.False(LookDown.CanShowOwnBody(true, true, false, false, false, true, false, false)); // limp
        Assert.False(LookDown.CanShowOwnBody(false, true, false, false, false, false, false, false)); // not first person
        Assert.False(LookDown.CanShowOwnBody(true, false, false, false, false, false, false, false)); // inside a locker
        Assert.False(LookDown.CanShowOwnBody(true, true, true, false, false, false, false, false)); // a ghost
    }

    [Fact]
    public void TheHintStripReadsYourselfAndReleaseOnAnObjectWithTheButtonsCap()
    {
        Assert.Equal("Yourself", SelfDrag.HintName);
        KeyBindings keys = KeyBindings.Defaults();
        Assert.Equal(new[] { new PromptLine("LMB", "Release on an object") }, SelfDrag.HintLines(keys));
        keys.Set(PlayAction.PrimaryClick, "MouseRight");
        Assert.Equal(new[] { new PromptLine("RMB", "Release on an object") }, SelfDrag.HintLines(keys));
        keys.Set(PlayAction.PrimaryClick);
        Assert.Empty(SelfDrag.HintLines(keys));
    }

    [Fact]
    public void ThePromptNamesOnlyAThingInReach()
    {
        Assert.Equal("table", SelfDrag.PromptName(Table, "table"));
        Assert.Null(SelfDrag.PromptName(Table, "")); // DV687: the server's out of reach
        Assert.Null(SelfDrag.PromptName(Table, null)); // no answer yet
        Assert.Null(SelfDrag.PromptName(Floor, "floor")); // a cell takes no drop
        Assert.Null(SelfDrag.PromptName(ClickTarget.None, "x"));
    }
}
