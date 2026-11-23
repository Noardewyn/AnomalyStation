using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// ui-overhaul.md section 5, a press does one thing: an action on Ctrl or Shift alone (down a level, walk and run) acts when the key is let go, and
/// only when it was alone: no other modifier down as it went down, no key or mouse button going down while it was held.
/// </summary>
public class ModifierTapTests
{
    private static readonly KeyBindings Keys = KeyBindings.Defaults();
    private const long LeftButton = 1, RightButton = 2;

    // GameScene's order: every press is seen first (_Input), then the press's action is resolved and maybe held back (OnKey), with the event's
    // own modifier flags and the mouse buttons down
    private static PlayAction? Press(ModifierTap tap, string key, bool shift = false, bool ctrl = false, bool alt = false, long mouse = 0)
    {
        tap.AnyPress(key);
        PlayAction? action = Keys.ActionOfPress(key, shift, ctrl);
        return tap.Wait(key, action, false, ModifierTap.OtherModifiers(key, shift, ctrl, alt, false), mouse) ? null : action;
    }

    private static bool Wait(ModifierTap tap, string key, PlayAction? action, bool blocked = false) => tap.Wait(key, action, blocked, false, 0);

    [Fact]
    public void CtrlTappedAloneGoesDownALevelOnceOnTheRelease()
    {
        var tap = new ModifierTap();
        Assert.Null(Press(tap, "Ctrl", ctrl: true)); // nothing on the press
        Assert.Equal(PlayAction.MoveDownLevel, tap.Release("Ctrl", false));
        Assert.Null(tap.Release("Ctrl", false)); // once
    }

    [Fact]
    public void CtrlQThrowsAndDoesNotGoDownALevel()
    {
        var tap = new ModifierTap();
        Assert.Null(Press(tap, "Ctrl", ctrl: true));
        Assert.Equal(PlayAction.ThrowMode, Press(tap, "Q", ctrl: true));
        Assert.Null(tap.Release("Q", false));
        Assert.Null(tap.Release("Ctrl", false));
    }

    [Fact]
    public void CtrlLeftClickPullsAndDoesNotGoDownALevel()
    {
        var tap = new ModifierTap();
        Assert.Null(Press(tap, "Ctrl", ctrl: true));
        tap.AnyPress("MouseLeft"); // GameScene._Input sees the button
        Assert.Equal(PlayAction.Pull, Keys.ActionOfPress("MouseLeft", shift: false, ctrl: true));
        Assert.Null(tap.Release("Ctrl", false));
        // and a later lone tap still works
        Assert.Null(Press(tap, "Ctrl", ctrl: true));
        Assert.Equal(PlayAction.MoveDownLevel, tap.Release("Ctrl", false));
    }

    [Fact]
    public void ShiftTappedAloneSwitchesWalkAndRunOnceOnTheRelease()
    {
        var tap = new ModifierTap();
        Assert.Null(Press(tap, "Shift", shift: true)); // nothing on the press
        Assert.Equal(PlayAction.ToggleMoveIntent, tap.Release("Shift", false)); // tg's toggle, once
        Assert.Null(tap.Release("Shift", false));
    }

    [Fact]
    public void ShiftLeftClickExaminesAndDoesNotSwitchWalkAndRun()
    {
        var tap = new ModifierTap();
        Assert.Null(Press(tap, "Shift", shift: true));
        tap.AnyPress("MouseLeft"); // GameScene._Input sees the button
        Assert.Equal(PlayAction.Examine, Keys.ActionOfPress("MouseLeft", shift: true));
        Assert.Null(tap.Release("Shift", false));
    }

    [Fact]
    public void ShiftThenAnotherKeyDoesNotSwitchWalkAndRun()
    {
        var tap = new ModifierTap();
        Assert.Null(Press(tap, "Shift", shift: true));
        Assert.Equal(PlayAction.ChatWindow, Press(tap, "T", shift: true)); // Shift+T: the pair's action only
        Assert.Null(tap.Release("T", false));
        Assert.Null(tap.Release("Shift", false));
    }

    [Fact]
    public void AModifierPressedWhileAnotherIsDownIsNotAlone()
    {
        // Alt+Shift (Windows' layout switch) does not switch walk and run
        var tap = new ModifierTap();
        Assert.Null(Press(tap, "Shift", shift: true, alt: true));
        Assert.Null(tap.Release("Shift", false));
        // Ctrl down, then a Shift tap: neither switches walk and run nor goes down a level
        Assert.Null(Press(tap, "Ctrl", ctrl: true));
        Assert.Null(Press(tap, "Shift", shift: true, ctrl: true));
        Assert.Null(tap.Release("Shift", false));
        Assert.Null(tap.Release("Ctrl", false));
        // Shift down, then a Ctrl tap: no level change, no walk switch
        Assert.Null(Press(tap, "Shift", shift: true));
        Assert.Null(Press(tap, "Ctrl", shift: true, ctrl: true));
        Assert.Null(tap.Release("Ctrl", false));
        Assert.Null(tap.Release("Shift", false));
        Assert.True(ModifierTap.OtherModifiers("Shift", shift: true, ctrl: false, alt: false, meta: true));
        Assert.False(ModifierTap.OtherModifiers("Shift", shift: true, ctrl: false, alt: false, meta: false)); // a key's own flag is not another
    }

    [Fact]
    public void AnOrdinaryKeyHeldFromBeforeDoesNotCount()
    {
        // walking forward (W held, its repeats are no presses) and a Shift tap still switches walk and run
        var tap = new ModifierTap();
        Assert.Equal(PlayAction.MoveForward, Press(tap, "W"));
        Assert.Null(Press(tap, "Shift", shift: true));
        Assert.Equal(PlayAction.ToggleMoveIntent, tap.Release("Shift", false));
    }

    [Fact]
    public void ASecondKeyOfTheSameNameCancelsAndNeverRearms()
    {
        // left Shift down, a click cancels, right Shift down, both up: no switch
        var tap = new ModifierTap();
        Assert.Null(Press(tap, "Shift", shift: true));
        tap.AnyPress("MouseLeft");
        Assert.Null(Press(tap, "Shift", shift: true));
        Assert.Null(tap.Release("Shift", false));
        Assert.Null(tap.Release("Shift", false));
        // the two Ctrl keys after Ctrl+click: no level change
        Assert.Null(Press(tap, "Ctrl", ctrl: true));
        tap.AnyPress("MouseLeft");
        Assert.Null(Press(tap, "Ctrl", ctrl: true));
        Assert.Null(tap.Release("Ctrl", false));
        Assert.Null(tap.Release("Ctrl", false));
        // the second key alone cancels too
        Assert.Null(Press(tap, "Shift", shift: true));
        Assert.Null(Press(tap, "Shift", shift: true));
        Assert.Null(tap.Release("Shift", false));
    }

    [Fact]
    public void AWalkKeyReboundToAPlainKeySwitchesOnThePressAsBefore()
    {
        // an old options file's toggle_move_intent=C: a plain key is no lone modifier, so the toggle goes on the press
        var keys = PlayOptions.Parse("bind.toggle_move_intent=C\nbind.rest=U\n").Bindings;
        var tap = new ModifierTap();
        tap.AnyPress("C");
        PlayAction? action = keys.ActionOfPress("C", shift: false);
        Assert.Equal(PlayAction.ToggleMoveIntent, action);
        Assert.False(Wait(tap, "C", action));
        Assert.Null(tap.Release("C", false));
    }

    [Fact]
    public void ALoneModifierTappedWhileTypingDoesNothing()
    {
        // ui-overhaul.md section 5 (playtest 2026-11-23): Shift in the chat input switched walk and run, Ctrl took a ghost down a level
        var tap = new ModifierTap();
        Assert.True(Wait(tap, "Shift", PlayAction.ToggleMoveIntent, blocked: true)); // held back: not run on the press
        Assert.Null(tap.Release("Shift", blocked: true));
        Assert.True(Wait(tap, "Ctrl", PlayAction.MoveDownLevel, blocked: true));
        Assert.Null(tap.Release("Ctrl", blocked: false)); // nothing armed, even if the field let the keyboard go meanwhile
        // a wait armed before the field took the keyboard does not fire after it did
        Assert.True(Wait(tap, "Shift", PlayAction.ToggleMoveIntent));
        Assert.Null(tap.Release("Shift", blocked: true));
        // and an ordinary tap afterwards works
        Assert.True(Wait(tap, "Shift", PlayAction.ToggleMoveIntent));
        Assert.Equal(PlayAction.ToggleMoveIntent, tap.Release("Shift", blocked: false));
    }

    [Fact]
    public void OnlyAMouseButtonThatGoesDownDuringTheHoldCancelsTheWait()
    {
        var tap = new ModifierTap();
        // a button held from before (a drag, holding fire) does not cancel
        Assert.Null(Press(tap, "Shift", shift: true, mouse: LeftButton));
        tap.Frame(LeftButton, keyDown: true);
        Assert.Equal(PlayAction.ToggleMoveIntent, tap.Release("Shift", false));
        // one pressed during the hold does, whoever took its press (the dev menu): the frame sees it
        Assert.Null(Press(tap, "Shift", shift: true));
        tap.Frame(0, keyDown: true);
        tap.Frame(RightButton, keyDown: true);
        Assert.Null(tap.Release("Shift", false));
        // held from before, let go and pressed again during the hold: pressed
        Assert.Null(Press(tap, "Shift", shift: true, mouse: LeftButton));
        tap.Frame(LeftButton, keyDown: true);
        tap.Frame(0, keyDown: true);
        tap.Frame(LeftButton, keyDown: true);
        Assert.Null(tap.Release("Shift", false));
    }

    [Fact]
    public void InTheFreeCameraALoneModifierSendsNothing()
    {
        // ui-overhaul.md section 5: Shift is the free camera's fly-faster key; GameScene tells Wait and Release it is blocked there
        var tap = new ModifierTap();
        Assert.True(Wait(tap, "Shift", PlayAction.ToggleMoveIntent, blocked: true)); // not run on the press
        Assert.Null(tap.Release("Shift", blocked: true)); // nor on the release
        Assert.True(Wait(tap, "Ctrl", PlayAction.MoveDownLevel, blocked: true));
        Assert.Null(tap.Release("Ctrl", blocked: true));
        // armed before the camera went free (F1 pressed during the hold cancels it anyway), let go in the free camera: nothing
        Assert.True(Wait(tap, "Shift", PlayAction.ToggleMoveIntent));
        Assert.Null(tap.Release("Shift", blocked: true));
    }

    [Fact]
    public void AWaitWhoseKeyIsNoLongerDownIsDroppedSoTheNextTapWorks()
    {
        // the release went to another part of the interface (the options panel, the dev menu's field): the frame sees the key up
        var tap = new ModifierTap();
        Assert.Null(Press(tap, "Shift", shift: true));
        tap.Frame(0, keyDown: true);
        tap.Frame(0, keyDown: false);
        Assert.Null(tap.Waiting);
        Assert.Null(Press(tap, "Shift", shift: true)); // not taken for a second Shift key
        tap.Frame(0, keyDown: true);
        Assert.Equal(PlayAction.ToggleMoveIntent, tap.Release("Shift", false));
    }

    [Fact]
    public void ThePanelDropsAModifierLostWithTheFocusAndCountsEveryMousePress()
    {
        var capture = new ModifierTap();
        // the focus lost with Shift held (the engine clears its key state): the next lone Shift tap binds Shift
        capture.Arm("Shift", false, 0);
        capture.Frame(0, keyDown: false);
        capture.Arm("Shift", false, 0);
        Assert.True(capture.ReleaseAlone("Shift"));
        // a back button or a wheel notch while Shift is held: it does not bind itself (OptionsPanel counts every button, "Mouse" + its index)
        foreach (string button in new[] { "MouseXbutton1", "MouseWheelUp" })
        {
            capture.Arm("Shift", false, 0);
            capture.AnyPress(button);
            Assert.False(capture.ReleaseAlone("Shift"));
        }
    }

    [Fact]
    public void ALostFocusDropsTheWaitingAction()
    {
        var tap = new ModifierTap();
        Assert.Null(Press(tap, "Ctrl", ctrl: true));
        tap.Drop(); // keys pressed in another window are not seen
        Assert.Null(tap.Release("Ctrl", false));
    }

    [Fact]
    public void OnlyALoneModifiersPressOnlyActionWaits()
    {
        var tap = new ModifierTap();
        Assert.False(Wait(tap, "Q", PlayAction.DropItem)); // not a modifier
        Assert.False(Wait(tap, "Ctrl", null)); // unbound
        Assert.False(Wait(tap, "Shift", PlayAction.ViewPetCommands)); // held: its own release (an older layout's Shift)
        Assert.False(Wait(tap, "Ctrl", PlayAction.HoldThrow));
        Assert.True(Wait(tap, "Shift", PlayAction.Rest)); // a press-only action rebound to Shift
        Assert.Equal(PlayAction.Rest, tap.Release("Shift", false));
    }

    [Fact]
    public void ThePanelBindsAModifierItselfOnlyWhenItWasAlone()
    {
        // ui-overhaul.md section 5: OptionsPanel's capture (Arm on the modifier's press, AnyPress on any other press, ReleaseAlone on its release)
        var capture = new ModifierTap();
        capture.Arm("Shift", otherModifiers: false, 0);
        Assert.True(capture.ReleaseAlone("Shift")); // Shift pressed and let go alone: binds Shift
        // Shift held, a right click the panel refuses for Examine, Shift let go: keeps waiting
        capture.Arm("Shift", false, 0);
        capture.AnyPress("MouseRight");
        Assert.False(capture.ReleaseAlone("Shift"));
        // Ctrl held, a refused left click while capturing Say, Ctrl let go: keeps waiting
        capture.Arm("Ctrl", false, 0);
        capture.AnyPress("MouseLeft");
        Assert.False(capture.ReleaseAlone("Ctrl"));
        // Ctrl pressed while Alt is down: not alone
        capture.Arm("Ctrl", otherModifiers: true, 0);
        Assert.False(capture.ReleaseAlone("Ctrl"));
        // a modifier held from before the capture began (the panel drops it then) does not bind on its release
        capture.Arm("Shift", false, 0);
        capture.Drop();
        Assert.False(capture.ReleaseAlone("Shift"));
    }
}
