using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Pulling;

/// <summary>
/// Pulling (2026-10-15): tg's pull in a booted toy room with joined players. A ctrl-click (tg's <c>CtrlClickOn</c>, the living
/// <c>base_click_ctrl</c>: <c>grab</c> skips a non-mob, then the <c>pulled</c> verb and <c>/mob/living/start_pulling</c>) pulls a crate
/// with tg's thudswoosh and the HUD's pull button lit; the crate follows every <c>Move</c> (a diagonal one too, where tg leaves it
/// diagonal), a distance of more than a tile breaks the pull, anchored things refuse and anchoring breaks it, a pulled mob is shifted
/// toward the puller (<c>set_pull_offsets</c>), a lying mob and a locker slow the puller (<c>bulky_drag</c>), the stop key and the HUD
/// button release. A reached <c>Dm.NotTranslated</c> throws here.
/// </summary>
public class PullTests
{
    private const int Second = 20;
    private const string CtrlClick = "left=1;ctrl=1;button=left;icon-x=16;icon-y=16";
    private const string Thud = "sound/items/weapons/thudswoosh.ogg";

    private static T Place<T>(AudioRoom r, int x, int y) where T : atom => r.In(() => Dm.@new<T>(locate(x, y, 1)));

    private static void Click(AudioRoom r, mob who, atom target, string @params = CtrlClick)
    {
        r.Command(() => ClientSeam.Click(who.client, target, @params));
        r.Step(Second + 2); // CLICK_CD_GRABBING (1 s) passes before the next click
    }

    private static void Key(AudioRoom r, mob who, byte code)
    {
        r.Command(() => ClientSeam.Keybinding(who.client, code, true));
        r.Step();
    }

    private static void Move(AudioRoom r, mob_living who, int x, int y, float dir)
        => r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(who.Move(locate(x, y, 1), dir))), "the move succeeds"));

    private static (int X, int Y) At(AudioRoom r, atom a) => r.In(() => ((int)Dm.num(a.x), (int)Dm.num(a.y)));

    private static atom_movable_screen_pull PullButton(AudioRoom r, mob who) => r.In(() => who.hud_used.screen_objects[HUD_MOB_PULL] as atom_movable_screen_pull);

    private const byte StopPullingKey = 9; // KeybindingCode.StopPulling (protocol version 20)

    [Fact]
    public void ACtrlClickPullsACrateThatFollowsEveryMoveAndADiagonalOneLeavesItDiagonal()
    {
        var r = new AudioRoom(12, 12, seed: 7101);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        var crate = Place<obj_structure_closet_crate>(r, 5, 4);
        atom_movable_screen_pull button = PullButton(r, bob);
        Assert.NotNull(button);
        r.In(() => Assert.Contains(button, Dm.each(bob.client.screen).Cast<object>()));
        Assert.Equal(("pull", "#606060"), r.In(() => (Dm.str(button.icon_state), Dm.str(button.color)))); // DV422: unlit, dimmed // tg: "[base_icon_state][pulling ? null : 0]"
        r.Sounds.Heard.Clear();

        Click(r, bob, crate);
        r.In(() =>
        {
            Assert.Same(crate, bob.pulling);
            Assert.Same(bob, crate.pulledby);
            Assert.Equal(GRAB_PASSIVE, bob.grab_state);
            Assert.Equal("pull", button.icon_state);
        });
        Assert.Contains(r.Sounds.Heard, h => ReferenceEquals(h.Target, bob) && h.File == Thud);

        // a crate has no drag slowdown (crates.dm:17, drag_slowdown = 0): no bulky_drag
        float speed = r.In(() => Dm.num(bob.cached_multiplicative_slowdown));
        Move(r, bob, 5, 6, NORTH);
        Assert.Equal((5, 5), At(r, crate)); // into the tile bob left
        Assert.Equal(speed, r.In(() => Dm.num(bob.cached_multiplicative_slowdown)));
        Move(r, bob, 6, 6, EAST);
        Assert.Equal((5, 6), At(r, crate));
        Move(r, bob, 6, 5, SOUTH);
        Assert.Equal((6, 6), At(r, crate));

        // a diagonal Move is two cardinal steps (atoms_movable.dm:774): the first drags the crate after it, the second leaves it
        // diagonal (moving_diagonally == SECOND_DIAG_STEP, :849)
        Move(r, bob, 7, 4, SOUTHEAST);
        Assert.Equal((7, 4), At(r, bob));
        Assert.Equal((6, 5), At(r, crate));
        r.In(() => Assert.Same(crate, bob.pulling));
        Move(r, bob, 8, 4, EAST);
        Assert.Equal((7, 4), At(r, crate)); // from a diagonal it takes bob's old tile
        r.AssertNoRuntimes();
    }

    [Fact]
    public void DistanceAnchoringAndAnAnchoredThingBreakOrRefuseThePull()
    {
        var r = new AudioRoom(12, 12, seed: 7102);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        var crate = Place<obj_structure_closet_crate>(r, 5, 4);
        Click(r, bob, crate);
        r.In(() => Assert.Same(crate, bob.pulling));

        // more than a tile apart: check_pulling at the next Move stops it (atoms_movable.dm:651)
        r.In(() => Waiting.NoSleep(crate.abstract_move(locate(5, 2, 1))));
        r.In(() => Assert.Same(crate, bob.pulling)); // nothing noticed yet
        Move(r, bob, 6, 5, EAST);
        r.In(() =>
        {
            Assert.Null(bob.pulling);
            Assert.Null(crate.pulledby);
        });
        Assert.Equal((5, 2), At(r, crate));
        Assert.Equal(("pull", "#606060"), r.In(() => (Dm.str(PullButton(r, bob).icon_state), Dm.str(PullButton(r, bob).color)))); // DV422: unlit, dimmed

        // anchoring a pulled thing stops the pull (set_anchored, atoms_movable.dm:1131)
        r.In(() => Waiting.NoSleep(crate.forceMove(locate(6, 4, 1))));
        Click(r, bob, crate);
        r.In(() => Assert.Same(crate, bob.pulling));
        r.In(() => Waiting.NoSleep(crate.set_anchored(TRUE)));
        r.In(() => Assert.Null(bob.pulling));

        // an anchored thing cannot be pulled (can_be_pulled, atoms_movable.dm:1728)
        Click(r, bob, crate);
        r.In(() => Assert.Null(bob.pulling));
        r.In(() => Waiting.NoSleep(crate.set_anchored(FALSE)));

        // the teleported puller lets go (living forceMove, living.dm:1971)
        Click(r, bob, crate);
        r.In(() => Assert.Same(crate, bob.pulling));
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(9, 9, 1))));
        r.In(() => Assert.Null(bob.pulling));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APulledMobIsShiftedTowardThePullerAndALyingOneSlowsThePuller()
    {
        var r = new AudioRoom(12, 12, seed: 7103);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        mob_living_carbon_human ann = r.Join(5, 4, "Ann");
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        float speed = r.In(() => Dm.num(bob.cached_multiplicative_slowdown));

        Click(r, bob, ann);
        r.In(() =>
        {
            Assert.Same(ann, bob.pulling);
            Assert.Equal(NORTH, Dm.num(ann.dir)); // set_pull_offsets turns the pulled mob toward the puller
            Assert.Equal(GRAB_PIXEL_SHIFT_PASSIVE, Dm.num(((DmList)ann.offsets[PIXEL_Y_OFFSET])[GRABBING_TRAIT]));
            Assert.Equal(speed, Dm.num(bob.cached_multiplicative_slowdown)); // a standing mob is no bulky drag
        });
        r.Step(2);
        Assert.Contains(chat.Html, l => l.Contains("You grab") && l.Contains("passively"));

        // bob steps east: ann follows into his old tile, now west of him, and is shifted east toward him
        Move(r, bob, 6, 5, EAST);
        Assert.Equal((5, 5), At(r, ann));
        r.In(() =>
        {
            Assert.Equal(EAST, Dm.num(ann.dir));
            Assert.Equal(GRAB_PIXEL_SHIFT_PASSIVE, Dm.num(((DmList)ann.offsets[PIXEL_X_OFFSET])[GRABBING_TRAIT]));
            Assert.Equal(0f, Dm.num(((DmList)ann.offsets[PIXEL_Y_OFFSET])[GRABBING_TRAIT])); // set_pull_offsets' EAST branch: y_add = 0
        });

        // lying down: the puller is slowed by PULL_PRONE_SLOWDOWN (living_movement.dm:101) at his next move
        r.In(() => Waiting.NoSleep(ann.set_resting(TRUE, TRUE, TRUE)));
        r.Step(2);
        Assert.True(r.In(() => ann.body_position == LYING_DOWN), "ann lies down");
        Move(r, bob, 7, 5, EAST);
        Assert.Equal((6, 5), At(r, ann));
        Assert.Equal(speed + PULL_PRONE_SLOWDOWN, r.In(() => Dm.num(bob.cached_multiplicative_slowdown)), 3);

        // the stop key lets go: the offsets and the slowdown go with the pull
        Key(r, bob, StopPullingKey);
        r.In(() =>
        {
            Assert.Null(bob.pulling);
            Assert.Null(ann.pulledby);
            Assert.True(ann.offsets == null || ann.offsets[PIXEL_X_OFFSET] == null, "the grab offsets are reset");
            Assert.Equal(speed, Dm.num(bob.cached_multiplicative_slowdown));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALockerSlowsThePullerFromHisFirstStepAndTheHudButtonAndTheStopKeyRelease()
    {
        var r = new AudioRoom(12, 12, seed: 7104);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        var locker = Place<obj_structure_closet>(r, 5, 4);
        var crate = Place<obj_structure_closet_crate>(r, 6, 5);
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        float speed = r.In(() => Dm.num(bob.cached_multiplicative_slowdown));

        Click(r, bob, locker);
        r.In(() => Assert.Same(locker, bob.pulling));
        // tg updates the drag slowdown of a pulled object in living/Move (living.dm:1061), before the step's delay
        Move(r, bob, 5, 6, NORTH);
        Assert.Equal(speed + 1.5f, r.In(() => Dm.num(bob.cached_multiplicative_slowdown)), 3); // closets.dm:13, drag_slowdown = 1.5
        Assert.Equal((5, 5), At(r, locker));

        // DV66: a ctrl-click on what one already pulls lets go of it (tg does nothing, living.dm:411: "Then just stop here"); a second one pulls again
        Click(r, bob, locker);
        r.In(() => Assert.Null(bob.pulling));
        Click(r, bob, locker);
        r.In(() => Assert.Same(locker, bob.pulling));

        // a ctrl-click on another thing pulls that one instead
        Move(r, bob, 6, 6, EAST);
        Click(r, bob, crate);
        r.In(() =>
        {
            Assert.Same(crate, bob.pulling);
            Assert.Null(locker.pulledby);
        });

        // the HUD's pull button lets go (screen_objects.dm:564)
        atom_movable_screen_pull button = PullButton(r, bob);
        Assert.Equal("pull", r.In(() => button.icon_state));
        Click(r, bob, button, "left=1;button=left;icon-x=16;icon-y=16");
        r.In(() =>
        {
            Assert.Null(bob.pulling);
            Assert.Equal(("pull", "#606060"), (Dm.str(button.icon_state), Dm.str(button.color))); // DV422: unlit, dimmed
            Assert.Equal(speed, Dm.num(bob.cached_multiplicative_slowdown));
        });

        // the stop key with nothing pulled (keybinding/mob.dm:17)
        chat.Html.Clear();
        Key(r, bob, StopPullingKey);
        r.Step(2);
        Assert.Contains(chat.Html, l => l.Contains("You are not pulling anything."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APullTakesTheThingFromAnotherPuller()
    {
        var r = new AudioRoom(12, 12, seed: 7105);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        mob_living_carbon_human cid = r.Join(7, 4, "Cid");
        var crate = Place<obj_structure_closet_crate>(r, 6, 4);
        Click(r, bob, crate);
        r.In(() => Assert.Same(crate, bob.pulling));
        Click(r, cid, crate);
        r.In(() =>
        {
            Assert.Same(crate, cid.pulling);
            Assert.Same(cid, crate.pulledby);
            Assert.Null(bob.pulling); // "an object can't be pulled by two mobs at once" (living.dm:422)
            Assert.Equal(("pull", "#606060"), (Dm.str(PullButton(r, bob).icon_state), Dm.str(PullButton(r, bob).color))); // DV422: unlit, dimmed
        });
        r.AssertNoRuntimes();
    }

    /// <summary>
    /// DV66 (the owner, 2026-10-18): a ctrl-click on what the mob pulls lets go of it; a ctrl-click on another thing drops the old one
    /// and pulls the new one (tg's start_pulling).
    /// </summary>
    [Fact]
    public void ACtrlClickOnThePulledThingLetsGoAndOnAnotherThingPullsThatOne()
    {
        var r = new AudioRoom(12, 12, seed: 7106);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        mob_living_carbon_human ann = r.Join(4, 5, "Ann");
        var crate = Place<obj_structure_closet_crate>(r, 5, 4);
        var other = Place<obj_structure_closet_crate>(r, 6, 5);

        Click(r, bob, crate);
        r.In(() => Assert.Same(crate, bob.pulling));
        Click(r, bob, other);
        r.In(() =>
        {
            Assert.Same(other, bob.pulling);
            Assert.Null(crate.pulledby);
        });
        Click(r, bob, other);
        r.In(() =>
        {
            Assert.Null(bob.pulling);
            Assert.Null(other.pulledby);
            Assert.Equal(("pull", "#606060"), (Dm.str(PullButton(r, bob).icon_state), Dm.str(PullButton(r, bob).color))); // DV422: unlit, dimmed
        });

        // a pulled mob out of combat mode is let go too (in combat mode tg's grabbedby makes the grab stronger: grippedby, M9)
        Click(r, bob, ann);
        r.In(() => Assert.Same(ann, bob.pulling));
        Click(r, bob, ann);
        r.In(() =>
        {
            Assert.Null(bob.pulling);
            Assert.Null(ann.pulledby);
        });
        r.AssertNoRuntimes();
    }
}
