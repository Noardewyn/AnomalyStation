using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M1Client.P1;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tests.M2Seams;

/// <summary>
/// The action entry points of the client seam (M2 contracts, <c>ClientSeam.Actions.cs</c>): rest, walk/run and combat
/// mode go through tg's keybinding datums the way tg's <c>keyDown</c> does (<c>can_use</c>, then <c>down</c>), with
/// <c>usr</c> the client's mob; tg's click parameters are BYOND's text. The keybinding procs are translated (M2 P1): the test
/// pins that a press changes the living mob's resting, move intent or combat mode.
/// </summary>
public class ActionEntryPointTests
{
    [Theory]
    [InlineData("rest")]
    [InlineData("intent")]
    [InlineData("combat")]
    public void APressReachesTheKeybindingDatum(string action)
    {
        // M2 P1: the keybinding procs are translated, so a press runs tg's can_use and down: rest toggles resting
        // (toggle_resting), walk/run cycles the move intent (toggle_move_intent), combat mode toggles (set_combat_mode).
        var room = new ClientRoom(seed: 31);
        mob avatar = room.In(() => Dm.@new<mob_living>(Anomaly.Core.Tg.Builtins.locate(5, 5, 1)));
        ClientConnection connection = room.Join(avatar, "Alice");
        Assert.True(connection.Accepted);
        var living = (mob_living)avatar;
        room.In(() => living.mobility_flags = MOBILITY_FLAGS_REST_CAPABLE_DEFAULT); // a mob that can rest (a plain /mob/living cannot: MOBILITY_FLAGS_DEFAULT has no MOBILITY_REST)
        float restingBefore = 0;
        string intentBefore = null;
        float? combatBefore = null;
        object usrSeen = "unset";
        room.Command(() =>
        {
            restingBefore = living.resting;
            intentBefore = living.move_intent;
            combatBefore = living.combat_mode;
            switch (action)
            {
                case "rest": ClientSeam.Rest(connection.Client); break;
                case "intent": ClientSeam.ToggleMoveIntent(connection.Client); break;
                default: ClientSeam.ToggleCombatMode(connection.Client); break;
            }
            usrSeen = World.Current.usr;
        });
        room.Step();
        room.AssertNoRuntimes();
        switch (action)
        {
            case "rest": Assert.NotEqual(restingBefore, living.resting); break;
            case "intent": Assert.NotEqual(intentBefore, living.move_intent); break;
            default: Assert.NotEqual(Dm.truthy(combatBefore), Dm.truthy(living.combat_mode)); break;
        }
        Assert.Null(usrSeen); // the entry point gave the caller's usr back
    }

    [Fact]
    public void AClientThatLeftPressesNothing()
    {
        var room = new ClientRoom(seed: 32);
        mob avatar = room.NewMob(5, 5);
        ClientConnection connection = room.Join(avatar, "Bob");
        room.Command(() => ClientSeam.Disconnect(connection.Client));
        room.Step(2);
        room.Command(() => ClientSeam.Rest(connection.Client));
        room.Step();
        room.AssertNoRuntimes();
    }

    [Theory]
    [InlineData("left", false, false, false, "left=1;button=left")]
    [InlineData("right", false, false, false, "right=1;button=right")]
    [InlineData("right", true, true, true, "right=1;shift=1;ctrl=1;alt=1;button=right")]
    [InlineData("middle", false, true, false, "middle=1;ctrl=1;button=middle")]
    public void ClickParamsAreBYONDsText(string button, bool shift, bool ctrl, bool alt, string expected)
    {
        Assert.Equal(expected, ClientSeam.ClickParams(button, shift, ctrl, alt));
        Assert.EndsWith(BUTTON + "=" + button, expected, StringComparison.Ordinal); // tg's params2list key (code/__DEFINES/_click.dm)
    }

    [Fact]
    public void ClickParamsRefuseAnUnknownButton()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ClientSeam.ClickParams("thumb"));
        Assert.Equal(LEFT_CLICK, "left");
    }
}
