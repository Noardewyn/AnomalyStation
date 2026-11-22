using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M1Client.T1;

/// <summary>
/// T1: the small client procs the login path and the key verbs reach: <c>/mob/proc/reset_perspective</c> (code/modules/mob/mob.dm:506-546)
/// with <c>/client/proc/set_eye</c> (client_procs.dm:1016-1026), <c>update_mouse_pointer</c> (mob.dm:1242-1254), the macro refresh
/// of SSinput (input.dm:44-48, setup.dm:22-46).
/// </summary>
public class PerspectiveAndPointerTests
{
    private static object Reset(T1Room room, mob m, object new_eye) => Waiting.NoSleep(m.reset_perspective(new_eye as atom));

    // ---- reset_perspective and set_eye ----

    [Fact]
    public void AMovableThatIsNotTheMobBecomesTheEyeWithTheEyePerspective()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var other = room.NewMob(10, 10);
        room.In(() =>
        {
            Assert.True(Dm.truthy(Reset(room, avatar, other)));
            Assert.Same(other, c.eye);
            Assert.Equal(EYE_PERSPECTIVE, c.perspective);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheMobItselfPutsTheEyeBackOnTheMobWithTheMobPerspective()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var other = room.NewMob(10, 10);
        room.In(() =>
        {
            Reset(room, avatar, other);
            Reset(room, avatar, avatar);
            Assert.Same(avatar, c.eye);
            Assert.Equal(MOB_PERSPECTIVE, c.perspective);
        });
    }

    [Fact]
    public void ATurfOtherThanTheMobsOwnIsAnEyeAndTheMobsOwnTurfIsTheMobPerspective()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() =>
        {
            Reset(room, avatar, room.At(3, 3));
            Assert.Same(room.At(3, 3), c.eye);
            Assert.Equal(EYE_PERSPECTIVE, c.perspective);
            Reset(room, avatar, avatar.loc);
            Assert.Same(avatar, c.eye);
            Assert.Equal(MOB_PERSPECTIVE, c.perspective);
        });
    }

    [Fact]
    public void NullResetsToTheMobOnATurfAndToTheLocInsideAnObject()
    {
        // mob.dm:535-541: `else { if(isturf(loc)) { set_eye(client.mob); MOB_PERSPECTIVE } else { EYE_PERSPECTIVE; set_eye(loc) } }`
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var other = room.NewMob(10, 10);
        room.In(() =>
        {
            Reset(room, avatar, other);
            Reset(room, avatar, null);
            Assert.Same(avatar, c.eye);
            Assert.Equal(MOB_PERSPECTIVE, c.perspective);
            var box = Dm.@new<obj>(room.At(8, 8));
            Waiting.NoSleep(avatar.forceMove(box));
            Reset(room, avatar, null);
            Assert.Same(box, c.eye);
            Assert.Equal(EYE_PERSPECTIVE, c.perspective);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AnAreaIsNoEyeAndLeavesEverythingAlone()
    {
        // mob.dm:532-533  else return TRUE //no setting eye to stupid things like areas or whatever (no signal after it)
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var listener = room.In(() => { var l = Dm.@new<datum_test_m1t1_listener>(); l.WatchPerspective(avatar); return l; });
        room.In(() =>
        {
            area some_area = avatar.loc.loc as area;
            Assert.True(Dm.truthy(Reset(room, avatar, some_area)));
            Assert.Same(avatar, c.eye);
            Assert.Equal(MOB_PERSPECTIVE, c.perspective);
        });
        Assert.Empty(listener.Trace);
    }

    [Fact]
    public void AResetSendsTheMobPerspectiveSignalOnSuccessAndNothingWithoutAClient()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var idle = room.NewMob(4, 4);
        var listener = room.In(() => { var l = Dm.@new<datum_test_m1t1_listener>(); l.WatchPerspective(avatar); l.WatchPerspective(idle); return l; });
        room.In(() =>
        {
            Reset(room, avatar, null);
            Assert.Null(Reset(room, idle, null)); // `if(!client) return`
        });
        Assert.Equal(new[] { "RESET_PERSPECTIVE" }, listener.Trace);
    }

    [Fact]
    public void SetEyeSendsItsSignalOnlyWhenTheEyeChangesWithTheOldAndTheNewEye()
    {
        // client_procs.dm:1016-1026  if(new_eye == eye) return; ... SEND_SIGNAL(src, COMSIG_CLIENT_SET_EYE, old_eye, new_eye)
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var other = room.NewMob(10, 10);
        var listener = room.In(() => { var l = Dm.@new<datum_test_m1t1_listener>(); l.WatchClientEye(c); return l; });
        string avatarTag = room.In(() => Dm.str(avatar.tag));
        string otherTag = room.In(() => Dm.str(other.tag));
        room.In(() =>
        {
            Waiting.NoSleep(c.set_eye(avatar)); // already the eye
            Waiting.NoSleep(c.set_eye(other));
            Waiting.NoSleep(c.set_eye(other));
            Waiting.NoSleep(c.set_eye(avatar));
        });
        Assert.Equal(new[] { "SET_EYE " + avatarTag + " -> " + otherTag, "SET_EYE " + otherTag + " -> " + avatarTag }, listener.Trace);
        room.AssertNoRuntimes();
    }

    // ---- update_mouse_pointer ----

    [Fact]
    public void HoldingShiftGivesTheExaminePointerAndReleasingItGivesTheDefaultBack()
    {
        // mob.dm:1242-1254, called at the end of keyDown and keyUp
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() => Assert.Null(c.mouse_pointer_icon));
        room.Act(() => room.Down(c, "Shift"));
        room.Step();
        room.In(() => Assert.True(Dm.eq(c.mouse_pointer_icon, avatar.examine_cursor_icon)));
        room.Act(() => room.Down(c, "Space"));
        room.Step();
        room.In(() => Assert.True(Dm.eq(c.mouse_pointer_icon, avatar.examine_cursor_icon))); // still held
        room.Act(() => room.Up(c, "Shift"));
        room.Step();
        room.In(() => Assert.Null(c.mouse_pointer_icon));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AMobWithoutAnExaminePointerKeepsTheDefaultWhileShiftIsHeld()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() => avatar.examine_cursor_icon = null);
        room.Act(() => room.Down(c, "Shift"));
        room.Step();
        room.In(() => Assert.Null(c.mouse_pointer_icon));
    }

    [Fact]
    public void TheClientsOverrideIconWinsOverEverythingElse()
    {
        // mob.dm:1253-1254
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() => c.mouse_override_icon = "override.dmi");
        room.Act(() => room.Down(c, "Shift"));
        room.Step();
        room.In(() => Assert.Equal("override.dmi", Dm.str(c.mouse_pointer_icon)));
        room.In(() => c.mouse_override_icon = null);
        room.Act(() => room.Up(c, "Shift"));
        room.Step();
        room.In(() => Assert.Null(c.mouse_pointer_icon));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void UpdateMousePointerOfAMobWithoutAClientDoesNothing()
    {
        var room = new T1Room();
        var idle = room.NewMob(4, 4);
        room.In(() => idle.update_mouse_pointer());
        room.AssertNoRuntimes();
    }

    // ---- the macro refresh ----

    [Fact]
    public void RefreshingTheMacroSetsReleasesTheHeldKeysAndRebuildsTheMovementKeys()
    {
        // input.dm:44-48 calls set_macros for every client; set_macros (setup.dm:22-37) starts with reset_held_keys() and ends with
        // update_special_keybinds(), which rebuilds movement_keys and runs calculate_move_dir()
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() => { room.Down(c, "W"); room.Down(c, "Shift"); room.Down(c, "A"); });
        room.Step();
        room.In(() =>
        {
            Assert.Equal(3f, c.keys_held.len);
            Assert.Equal(1f, c.key_combos_held.len);
            c.movement_keys = Dm.list(); // as if the table were stale
            SSinput.refresh_client_macro_sets();
        });
        room.Step();
        room.In(() =>
        {
            Assert.Equal(0f, c.keys_held.len);
            Assert.Equal(0f, c.key_combos_held.len);
            Assert.Equal(8f, c.movement_keys.len);
            Assert.Equal(0f, Dm.num(c.intended_direction));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void SSinputInitializeBuildsTheMacroSetAndMarksItselfInitialized()
    {
        var room = new T1Room();
        room.In(() =>
        {
            SSinput.macro_set = null;
            SSinput.initialized = FALSE;
            var result = Waiting.NoSleep(SSinput.Initialize());
            Assert.True(Dm.eq(result, SS_INIT_SUCCESS));
            Assert.True(Dm.truthy(SSinput.initialized));
            Assert.Equal(5f, SSinput.macro_set.len);
        });
        room.AssertNoRuntimes();
    }
}
