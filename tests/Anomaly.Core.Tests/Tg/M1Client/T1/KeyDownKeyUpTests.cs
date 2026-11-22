using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.M1Client.T1.Dirs;

namespace Anomaly.Core.Tests.Tg.M1Client.T1;

/// <summary>
/// T1: tg's <c>keyDown</c> and <c>keyUp</c> verbs (code/modules/keybindings/bindings_client.dm) and <c>calculate_move_dir</c>
/// (bindings_atom.dm:40): what a key event does to <c>keys_held</c>, <c>intended_direction</c>, <c>next_move_dir_add</c> and
/// <c>next_move_dir_sub</c>, and the step that SSinput's <c>keyLoop</c> takes from them. Written from the DM; the expected
/// values are traced by hand from the .dm lines named in each test.
/// </summary>
public class KeyDownKeyUpTests
{
    private static float N(object v) => Dm.num(v);

    private static List<string> Held(client c) => Dm.each(c.keys_held).Select(k => Dm.str(k)).ToList();

    [Fact]
    public void KeyDownStoresTheKeyWithTheWorldTimeOfThePress()
    {
        // bindings_client.dm:54  keys_held[_key] = world.time
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        float pressed = 0;
        room.Act(() => { pressed = world.time; room.Down(c, "Space"); });
        room.Step();
        room.In(() =>
        {
            Assert.Equal(new[] { "Space" }, Held(c));
            Assert.Equal(pressed, N(c.keys_held["Space"]));
            Assert.True(pressed > 0);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AKeyThatIsNotAMovementKeyMovesNothingAndLeavesTheIntendedDirectionAlone()
    {
        // bindings_client.dm:55-59: `var/movement = movement_keys[_key]` is null for "Space", so no move direction work
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() => room.Down(c, "Space"));
        room.Step(4);
        room.In(() =>
        {
            Assert.Equal(0f, N(c.intended_direction));
            Assert.False(Dm.truthy(c.next_move_dir_add));
            Assert.Equal("8,8", T1Room.Where(avatar));
        });
        room.AssertNoRuntimes();
    }

    [Theory]
    [InlineData("W", 1)]
    [InlineData("North", 1)]
    [InlineData("S", 2)]
    [InlineData("South", 2)]
    [InlineData("D", 4)]
    [InlineData("East", 4)]
    [InlineData("A", 8)]
    [InlineData("West", 8)]
    public void EachDefaultMovementKeyIsAMovementKeyOfItsDirection(string key, int direction)
    {
        // keyDown :55-59 reads movement_keys[_key]; the keys are tg's default movement hotkeys (datums/keybinding/movement.dm:5-31,
        // proposed deviation DV14: preferences are not translated until M9)
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() => Assert.Equal(direction, (int)N(c.movement_keys[key])));
        room.Act(() => room.Down(c, key));
        room.Step();
        room.In(() =>
        {
            Assert.Equal(direction, (int)N(c.intended_direction)); // calculate_move_dir (bindings_atom.dm:40-44)
            Assert.Equal(direction, (int)N(c.next_move_dir_add)); // :58  next_move_dir_add |= movement
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ADefaultMovementKeyTableHoldsExactlyTheEightHotkeysOfTheFourDirections()
    {
        // DV14: with no preferences the table is W/North, S/South, A/West, D/East and nothing else (the z-level keys
        // "Northeast" and "Southeast" bind no movement_keys entry: update_special_keybinds, client_procs.dm:1044-1051)
        var room = new T1Room();
        var (_, c) = room.JoinNew("Tester");
        room.In(() =>
        {
            Assert.Equal(8f, c.movement_keys.len);
            Assert.False(Dm.truthy(c.movement_keys["Northeast"]));
            Assert.False(Dm.truthy(c.movement_keys["Southeast"]));
            Assert.False(Dm.truthy(c.movement_keys["Space"]));
        });
    }

    [Fact]
    public void TwoHeldMovementKeysCombineIntoOneIntendedDirection()
    {
        // calculate_move_dir (bindings_atom.dm:40-44): movement_dir |= movement_keys[_key] over keys_held
        var room = new T1Room();
        var (_, c) = room.JoinNew("Tester");
        room.Act(() => { room.Down(c, "W"); room.Down(c, "D"); });
        room.Step();
        room.In(() =>
        {
            Assert.Equal(North | East, (int)N(c.intended_direction));
            Assert.Equal(North | East, (int)N(c.next_move_dir_add));
        });
    }

    [Fact]
    public void TwoKeysOfTheSameDirectionAreOneDirection()
    {
        var room = new T1Room();
        var (_, c) = room.JoinNew("Tester");
        room.Act(() => { room.Down(c, "W"); room.Down(c, "North"); });
        room.Step();
        room.In(() =>
        {
            Assert.Equal(2f, c.keys_held.len);
            Assert.Equal(North, (int)N(c.intended_direction));
        });
        room.Act(() => room.Up(c, "W"));
        room.Step();
        room.In(() => Assert.Equal(North, (int)N(c.intended_direction))); // North is still held
    }

    [Fact]
    public void KeyUpRemovesTheKeyAndRecalculatesTheIntendedDirection()
    {
        // keyUp :83-96: keys_held -= _key, then calculate_move_dir()
        var room = new T1Room();
        var (_, c) = room.JoinNew("Tester");
        room.Act(() => { room.Down(c, "W"); room.Down(c, "D"); });
        room.Step();
        room.Act(() => room.Up(c, "D"));
        room.Step();
        room.In(() =>
        {
            Assert.Equal(new[] { "W" }, Held(c));
            Assert.Equal(North, (int)N(c.intended_direction));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void KeyUpOfAKeyThatWasPressedAndAlreadyConsumedRecordsTheSubtraction()
    {
        // :98-99  if(!movement_locked && !(next_move_dir_add & movement)) next_move_dir_sub |= movement. Held W was consumed by
        // the step (client/Move clears next_move_dir_add, mob_movement.dm:46), so add has no North bit when W goes up.
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() => room.Down(c, "W"));
        room.Step(2); // tick 1 phase: pressed; tick 2 fire: moved, add cleared
        room.In(() => Assert.False(Dm.truthy(c.next_move_dir_add)));
        room.Act(() => room.Up(c, "W"));
        room.Step();
        room.In(() =>
        {
            Assert.Equal(North, (int)N(c.next_move_dir_sub));
            Assert.Equal(0f, N(c.intended_direction));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void KeyUpOfAKeyWhoseTapIsStillPendingDoesNotSubtractIt()
    {
        // :98  !(next_move_dir_add & movement): the tap still waits in add, so no subtraction is recorded
        var room = new T1Room();
        var (_, c) = room.JoinNew("Tester");
        room.Act(() => { room.Down(c, "W"); room.Up(c, "W"); });
        room.Step();
        room.In(() =>
        {
            Assert.Equal(North, (int)N(c.next_move_dir_add));
            Assert.False(Dm.truthy(c.next_move_dir_sub));
            Assert.Equal(0f, N(c.intended_direction));
            Assert.Empty(Held(c));
        });
    }

    [Fact]
    public void KeyUpOfAKeyThatIsNotHeldReturnsWithoutDoingAnything()
    {
        // :90-91  if(!keys_held[_key]) return: calculate_move_dir is not reached, so a planted intended_direction survives
        var room = new T1Room();
        var (_, c) = room.JoinNew("Tester");
        room.In(() => c.intended_direction = East);
        room.Act(() => room.Up(c, "W"));
        room.Step();
        room.In(() =>
        {
            Assert.Equal(East, (int)N(c.intended_direction));
            Assert.False(Dm.truthy(c.next_move_dir_sub));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ATapShorterThanATickStillMovesOneStep()
    {
        // press and release before SSinput's next fire (the tap): keyLoop sees movement_dir = intended (0) | add (North)
        // (bindings_atom.dm:15) and moves once; client/Move then clears add and sub (mob_movement.dm:46-47)
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() => { room.Down(c, "W"); room.Up(c, "W"); });
        room.Step(); // the phase
        room.In(() => Assert.Equal("8,8", T1Room.Where(avatar)));
        room.Step(); // the fire
        room.In(() =>
        {
            Assert.Equal("8,9", T1Room.Where(avatar));
            Assert.Equal(0f, N(c.next_move_dir_add));
            Assert.Equal(0f, N(c.next_move_dir_sub));
        });
        room.Step(5); // nothing is held: no more steps
        room.In(() => Assert.Equal("8,9", T1Room.Where(avatar)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TwoTapsInTwoTicksMoveTwoSteps()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() => { room.Down(c, "D"); room.Up(c, "D"); });
        room.Step(2);
        room.Act(() => { room.Down(c, "D"); room.Up(c, "D"); });
        room.Step(2);
        room.In(() => Assert.Equal("10,8", T1Room.Where(avatar)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AKeyReleasedBeforeTheNextFireStopsTheMobAndTheFireForgetsTheSubtraction()
    {
        // The timeline (tick: Master fire, then command phase):
        //  t1 phase: W down (add = N)       t2 fire: step 1 (add cleared)     t3 fire: step 2 (the key is still held)
        //  t4 fire: step 3                  t4 phase: W up (add has no N, so sub = N; intended = 0)
        //  t5 fire: movement_dir = 0 | 0 = 0 -> "No input == our removal would have done nothing": sub = NONE, no Move
        // (bindings_atom.dm:15-21)
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() => room.Down(c, "W"));
        room.Step(3);
        room.Act(() => room.Up(c, "W"));
        room.Step();
        room.In(() =>
        {
            Assert.Equal("8,11", T1Room.Where(avatar)); // t2, t3 and t4
            Assert.Equal(North, (int)N(c.next_move_dir_sub));
        });
        room.Step();
        room.In(() =>
        {
            Assert.Equal("8,11", T1Room.Where(avatar)); // t5: no step
            Assert.Equal(0f, N(c.next_move_dir_sub)); // forgotten
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TgQuirkReleaseAndRepressWithinATickLosesThatTicksStep()
    {
        // The held key moves every tick. A release and a re-press in one command phase: keyUp records sub = N (add has no N,
        // :98-99) and clears intended; keyDown sets intended = N again but, with the N bit in sub, does NOT add it (:57-58).
        // The next fire sees movement_dir = N, takes N away again (sub, bindings_atom.dm:23-24) and calls Move(get_step(src, 0), 0),
        // which clears sub and refuses (no direction, mob_movement.dm:49). One tick without a step; the tick after steps again.
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() => room.Down(c, "W"));
        room.Step(2); // moved once (8,9)
        room.In(() => Assert.Equal("8,9", T1Room.Where(avatar)));
        room.Act(() => { room.Up(c, "W"); room.Down(c, "W"); });
        room.Step();
        room.In(() =>
        {
            Assert.Equal(North, (int)N(c.next_move_dir_sub));
            Assert.Equal(North, (int)N(c.intended_direction));
            Assert.False(Dm.truthy(c.next_move_dir_add));
            Assert.Equal("8,10", T1Room.Where(avatar)); // the fire of this very tick still saw the old state and moved
        });
        room.Step(); // the fire after the re-press: no step
        room.In(() =>
        {
            Assert.Equal("8,10", T1Room.Where(avatar));
            Assert.Equal(0f, N(c.next_move_dir_sub));
        });
        room.Step(); // and the key still held moves again
        room.In(() => Assert.Equal("8,11", T1Room.Where(avatar)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void OppositeKeysCancelAndTheMobDoesNotMove()
    {
        // :26-27  if((movement_dir & North) && (movement_dir & South)) movement_dir &= ~(North|South)
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() => { room.Down(c, "W"); room.Down(c, "S"); });
        room.Step(5);
        room.In(() =>
        {
            Assert.Equal(North | South, (int)N(c.intended_direction)); // the keys are still held
            Assert.Equal("8,8", T1Room.Where(avatar));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void OppositeKeysEastAndWestCancelToo()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() => { room.Down(c, "A"); room.Down(c, "D"); });
        room.Step(5);
        room.In(() => Assert.Equal("8,8", T1Room.Where(avatar)));
    }

    [Fact]
    public void AllFourKeysCancelEachOther()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() => { room.Down(c, "W"); room.Down(c, "A"); room.Down(c, "S"); room.Down(c, "D"); });
        room.Step(5);
        room.In(() => Assert.Equal("8,8", T1Room.Where(avatar)));
    }

    [Theory]
    [InlineData("W", "A", 7, 9)] // north-west
    [InlineData("W", "D", 9, 9)] // north-east
    [InlineData("S", "A", 7, 7)] // south-west
    [InlineData("S", "D", 9, 7)] // south-east
    public void TwoPerpendicularKeysMoveTheMobDiagonallyOneTilePerAxisPerTick(string a, string b, int x, int y)
    {
        // bindings_atom.dm:27-28 keeps both axes; atoms_movable.dm:773-820 splits the diagonal Move into two cardinal steps
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() => { room.Down(c, a); room.Down(c, b); });
        room.Step(2);
        room.In(() => Assert.Equal(x + "," + y, T1Room.Where(avatar)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ThreeKeysWithAnOpposedPairMoveAlongTheFreeAxisOnly()
    {
        // N + S cancel, E stays: "Sanity checks in case you hold left and right and up to make sure you only go up"
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() => { room.Down(c, "W"); room.Down(c, "S"); room.Down(c, "D"); });
        room.Step(2);
        room.In(() => Assert.Equal("9,8", T1Room.Where(avatar)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void KeyDownOfAHeldKeyAgainKeepsOneEntryAndUpdatesTheTime()
    {
        var room = new T1Room();
        var (_, c) = room.JoinNew("Tester");
        float first = 0, second = 0;
        room.Act(() => { first = world.time; room.Down(c, "Space"); });
        room.Step(3);
        room.Act(() => { second = world.time; room.Down(c, "Space"); });
        room.Step();
        room.In(() =>
        {
            Assert.Equal(1f, c.keys_held.len);
            Assert.Equal(second, N(c.keys_held["Space"]));
            Assert.True(second > first);
        });
    }

    [Fact]
    public void TheHeldKeyBufferDropsTheFirstKeyWhenAnotherOneWouldOverflowIt()
    {
        // :48-49  if(length(keys_held) >= HELD_KEY_BUFFER_LENGTH && !keys_held[_key]) keyUp(keys_held[1], ...)
        var room = new T1Room();
        var (_, c) = room.JoinNew("Tester");
        var keys = Enumerable.Range(1, 16).Select(i => "K" + i).ToList();
        room.Act(() =>
        {
            for (int i = 0; i < 15; i++)
                room.Down(c, keys[i]);
        });
        room.Step();
        room.In(() =>
        {
            Assert.Equal(15f, c.keys_held.len);
            Assert.Equal("K1", Dm.str(c.keys_held[1]));
        });
        room.Act(() => room.Down(c, "K16"));
        room.Step();
        room.In(() =>
        {
            Assert.Equal(15f, c.keys_held.len);
            Assert.False(Dm.truthy(c.keys_held["K1"]));
            Assert.True(Dm.truthy(c.keys_held["K2"]));
            Assert.True(Dm.truthy(c.keys_held["K16"]));
            Assert.Equal("K2", Dm.str(c.keys_held[1])); // the next one is first in line now
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ARepeatedHeldKeyDoesNotDropAnythingFromAFullBuffer()
    {
        var room = new T1Room();
        var (_, c) = room.JoinNew("Tester");
        room.Act(() =>
        {
            for (int i = 1; i <= 15; i++)
                room.Down(c, "K" + i);
            room.Down(c, "K7"); // already held: !keys_held[_key] is false
        });
        room.Step();
        room.In(() =>
        {
            Assert.Equal(15f, c.keys_held.len);
            Assert.True(Dm.truthy(c.keys_held["K1"]));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void WhenTheBufferDropsAHeldMovementKeyTheDropIsAnOrdinaryKeyUp()
    {
        // keyUp(keys_held[1]) is the whole keyUp: "North" is first in keys_held, so intended_direction is recalculated
        var room = new T1Room();
        var (_, c) = room.JoinNew("Tester");
        room.Act(() =>
        {
            room.Down(c, "North");
            for (int i = 1; i <= 14; i++)
                room.Down(c, "K" + i);
        });
        room.Step();
        room.In(() => Assert.Equal(North, (int)N(c.intended_direction)));
        room.Act(() => room.Down(c, "K15"));
        room.Step();
        room.In(() =>
        {
            Assert.False(Dm.truthy(c.keys_held["North"]));
            Assert.Equal(0f, N(c.intended_direction));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ASixteenCharacterKeyIsNotTooLongAndASeventeenCharacterKeyKicksTheClient()
    {
        // :38-45  if(LAZYLEN(_key) > MAX_KEYPRESS_COMMANDLENGTH) { to_chat(...); log_admin(...); message_admins(...); qdel(src); return }
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        string sixteen = new string('k', 16);
        string seventeen = new string('x', 17);
        room.Act(() => room.Down(c, sixteen));
        room.Step();
        room.In(() =>
        {
            Assert.True(Dm.truthy(c.keys_held[sixteen]));
            Assert.Null(c.gc_destroyed);
        });
        room.Act(() => room.Down(c, seventeen));
        room.Step();
        room.In(() =>
        {
            Assert.False(Dm.truthy(c.keys_held[seventeen])); // `return` came before keys_held was written
            Assert.NotNull(c.gc_destroyed);
            Assert.Null(avatar.client);
            Assert.Contains(room.Chat.To(c), t => t.Contains("Invalid KeyDown detected!", StringComparison.Ordinal));
            Assert.Contains(Dm.each(GLOB.admin_activities), a => Dm.str(a).Contains("Client tester just attempted to send an invalid keypress. Keymessage was over 16 characters", StringComparison.Ordinal));
        });
        Assert.Equal(new[] { c }, room.Host.Deleted);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void KeyDownWithModifiersRecordsTheComboAndKeyUpClearsIt()
    {
        // :61-69 (full key) and :84-88 (keyUp of the combo)
        var room = new T1Room();
        var (_, c) = room.JoinNew("Tester");
        room.Act(() => { room.Down(c, "Shift"); room.Down(c, "A"); });
        room.Step();
        room.In(() =>
        {
            Assert.Equal("ShiftA", Dm.str(c.key_combos_held["A"]));
            Assert.False(Dm.truthy(c.key_combos_held["Shift"])); // a modifier is no combo
            Assert.Equal(2f, c.keys_held.len);
        });
        room.Act(() => room.Up(c, "A"));
        room.Step();
        room.In(() =>
        {
            Assert.Equal(0f, c.key_combos_held.len);
            Assert.Equal(new[] { "Shift" }, Held(c));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ModifierOrderInAComboIsAltCtrlShift()
    {
        // :56-58  full_key = "[AltMod][CtrlMod][ShiftMod][_key]"
        var room = new T1Room();
        var (_, c) = room.JoinNew("Tester");
        room.Act(() => { room.Down(c, "Shift"); room.Down(c, "Ctrl"); room.Down(c, "Alt"); room.Down(c, "B"); });
        room.Step();
        room.In(() => Assert.Equal("AltCtrlShiftB", Dm.str(c.key_combos_held["B"])));
        room.Act(() => { room.Down(c, "C"); });
        room.Step();
        room.In(() => Assert.Equal("AltCtrlShiftC", Dm.str(c.key_combos_held["C"])));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AMovementKeyHeldUnderAModifierStillMovesAndItsComboFallsAwayOnRelease()
    {
        // movement_keys is read by the bare key (:55); the combo record only feeds the keybinding datums
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() => { room.Down(c, "Shift"); room.Down(c, "W"); });
        room.Step(2);
        room.In(() =>
        {
            Assert.Equal("ShiftW", Dm.str(c.key_combos_held["W"]));
            Assert.Equal("8,9", T1Room.Where(avatar));
        });
        room.Act(() => room.Up(c, "W"));
        room.Step();
        room.In(() =>
        {
            Assert.Equal(0f, c.key_combos_held.len);
            Assert.False(Dm.truthy(c.keys_held["W"]));
            Assert.Equal(0f, N(c.intended_direction));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ResetHeldKeysReleasesEveryHeldKeyAndComboAndStopsTheMob()
    {
        // setup.dm:39-46: `for(var/key in keys_held) keyUp(key)` (DM iterates a copy, so every key goes), then the combos
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() => { room.Down(c, "Shift"); room.Down(c, "A"); room.Down(c, "W"); room.Down(c, "Space"); });
        room.Step();
        room.In(() => Assert.Equal(4f, c.keys_held.len));
        room.Act(() => { Waiting.Detach(c.reset_held_keys()); });
        room.Step();
        room.In(() =>
        {
            Assert.Equal(0f, c.keys_held.len);
            Assert.Equal(0f, c.key_combos_held.len);
            Assert.Equal(0f, N(c.intended_direction));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void WithHotkeysOffAMultiCharacterKeyIsStillTreatedAsAKey()
    {
        // :34-37  the chat focus failsafe takes only keys of length 1 that are not Alt, Ctrl or Shift
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() => c.hotkeys = FALSE);
        room.Act(() => room.Down(c, "North"));
        room.Step(2);
        room.In(() => Assert.Equal("8,9", T1Room.Where(avatar)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void WithMovementLockedAKeyDownRecordsNothingInNextMoveDirAddAndKeyUpNothingInSub()
    {
        // :57 and :98: `!movement_locked &&` guards both
        var room = new T1Room();
        var (_, c) = room.JoinNew("Tester");
        room.In(() => c.movement_locked = TRUE);
        room.Act(() => room.Down(c, "W"));
        room.Step();
        room.In(() =>
        {
            Assert.Equal(North, (int)N(c.intended_direction)); // calculate_move_dir does not look at the lock
            Assert.False(Dm.truthy(c.next_move_dir_add));
        });
        room.Act(() => room.Up(c, "W"));
        room.Step();
        room.In(() => Assert.False(Dm.truthy(c.next_move_dir_sub)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void KeyDownKeepsTheMobsKeyFocusWorkingForAMobWithoutFocus()
    {
        // `mob.focus?.key_down(...)` and `mob.focus?.key_up(...)`: a mob with a null focus is no error
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() => avatar.focus = null);
        room.Act(() => { room.Down(c, "Space"); room.Up(c, "Space"); });
        room.Step();
        room.AssertNoRuntimes();
    }

    [Fact]
    public void KeyDownTellsTheMobsFocusTheKeyTheClientAndTheFullKeyWithModifiersFirst()
    {
        // :61-69 build full_key, :79-81 `mob.focus?.key_down(_key, src, full_key)`; /mob/key_down (mob.dm:1613-1615) sends
        // COMSIG_MOB_KEYDOWN (key, client, full_key). A modifier key alone has the modifiers held as its full key.
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var listener = room.In(() => { var l = Dm.@new<datum_test_m1t1_listener>(); l.WatchKeyDown(avatar); return l; });
        string tag = room.In(() => Dm.str(avatar.tag));
        room.Act(() =>
        {
            room.Down(c, "A");
            room.Down(c, "Shift");
            room.Down(c, "B");
            room.Down(c, "Ctrl");
            room.Down(c, "C");
        });
        room.Step();
        Assert.Equal(new[]
        {
            "KEYDOWN A as A by Tester on " + tag,
            "KEYDOWN Shift as Shift by Tester on " + tag,
            "KEYDOWN B as ShiftB by Tester on " + tag,
            "KEYDOWN Ctrl as CtrlShift by Tester on " + tag,
            "KEYDOWN C as CtrlShiftC by Tester on " + tag,
        }, listener.Trace);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void KeyUpSendsNoKeyDownSignalAndAKickedKeyDownSendsNone()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var listener = room.In(() => { var l = Dm.@new<datum_test_m1t1_listener>(); l.WatchKeyDown(avatar); return l; });
        room.Act(() => { room.Down(c, "A"); room.Up(c, "A"); room.Down(c, new string('x', 17)); });
        room.Step();
        Assert.Single(listener.Trace); // only the first press
        room.AssertNoRuntimes();
    }

    [Fact]
    public void KeyDownGoesToTheFocusAndNotToTheMobWhenTheTwoDiffer()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var other = room.NewMob(4, 4);
        var listener = room.In(() =>
        {
            var l = Dm.@new<datum_test_m1t1_listener>();
            l.WatchKeyDown(avatar);
            l.WatchKeyDown(other);
            avatar.focus = other;
            return l;
        });
        string otherTag = room.In(() => Dm.str(other.tag));
        room.Act(() => room.Down(c, "A"));
        room.Step();
        Assert.Equal(new[] { "KEYDOWN A as A by Tester on " + otherTag }, listener.Trace);
    }
}
