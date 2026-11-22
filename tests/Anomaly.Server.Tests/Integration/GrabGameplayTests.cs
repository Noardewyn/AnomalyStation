using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M9 slice F's grabs through the real server on MetaStation: Alice, in combat mode (DV66), ctrl-clicks Bob (tg's pull), again (tg's grippedby: an
/// aggressive grab at once, with tg's thudswoosh and lines, Bob incapacitated by the grab), again (the neck grab after tg's 3 s
/// do_after) and again (the kill grab: Bob misses his breaths and takes oxygen damage). The screentip says tg's "Choke" while the grab is aggressive.
/// Bob resists (tg's resist_grab: he breaks free or struggles), Alice grabs him aggressively again and throws him west with throw
/// mode (tg's throw_item of a grabbed mob).
/// </summary>
public class GrabGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const string Thud = "sound/items/weapons/thudswoosh.ogg";

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static bool Said(List<ServerText> lines, string text) => lines.Any(t => t.Html.Contains(text, StringComparison.Ordinal));

    [MetaServerFact]
    public void Alice_grabs_Bob_up_to_a_strangle_he_resists_and_she_throws_him()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 61, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        var them = (mob_living_carbon_human)w.MobOf(bob);
        var aliceChat = new List<ServerText>();
        var bobChat = new List<ServerText>();
        alice.Client.ServerTextReceived += aliceChat.Add;
        bob.Client.ServerTextReceived += bobChat.Add;
        var heard = new List<SoundEvent>();
        alice.Session.Replica.SoundsReceived += heard.AddRange;
        (int sx, int sy, int z) = In(w, () => ((int)me.x, (int)me.y, (int)me.z));
        using (w.World.Enter())
            Waiting.NoSleep(them.forceMove(Builtins.locate(sx - 1, sy, z)));
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        string bobName = In(w, () => Dm.str(them.name));

        void CtrlClickBob(int seconds)
        {
            Assert.True(alice.Session.Click(them.dm_id, ClickButton.Left, ClickModifiers.Ctrl));
            w.Run(seconds * Second);
        }

        // In combat mode (DV66: outside it a ctrl-click on the pulled mob lets go), the pull, then the aggressive grab (the first upgrade
        // is instant).
        alice.Session.ToggleCombatMode();
        w.Run(5);
        CtrlClickBob(1);
        Assert.Same(them, In(w, () => me.pulling));
        Assert.Equal(GRAB_PASSIVE, In(w, () => me.grab_state));
        heard.Clear();
        CtrlClickBob(1);
        Assert.Equal(GRAB_AGGRESSIVE, In(w, () => me.grab_state));
        Assert.True(Said(bobChat, "grabs you aggressively!"), string.Join(" | ", bobChat.Select(t => t.Html)));
        Assert.True(Said(aliceChat, "You grab " + bobName + " aggressively!"));
        Assert.Contains(heard, e => e.File == Thud);
        Assert.True(In(w, () => ((int)them.incapacitated & INCAPABLE_GRAB) != 0));
        Assert.Equal("Choke", In(w, () =>
        {
            DmList context = Dm.list();
            them.add_context(them, context, null, me);
            return Dm.str(context[SCREENTIP_CONTEXT_CTRL_LMB]);
        }));

        // The neck grab and the kill grab, each after tg's 3 s do_after.
        CtrlClickBob(4);
        Assert.True(Said(bobChat, "starts to tighten"));
        Assert.Equal(GRAB_NECK, In(w, () => me.grab_state));
        Assert.True(Said(bobChat, "grabs you by the neck!"));
        CtrlClickBob(4);
        Assert.Equal(GRAB_KILL, In(w, () => me.grab_state));
        Assert.True(Said(bobChat, "is strangling you!"));
        // tg's breathe: a choked mob misses its breaths (losebreath++ then --), so it takes oxygen damage
        float oxyBefore = In(w, () => Dm.num(them.get_oxy_loss()));
        w.Run(8 * Second);
        float oxy = In(w, () => Dm.num(them.get_oxy_loss()));
        output.WriteLine($"Bob's oxygen damage after 8 s of strangling: {oxyBefore} -> {oxy}");
        Assert.True(oxy > oxyBefore);

        // Bob resists: he breaks free or struggles (tg's chance by the grab state).
        bob.Session.Keybinding(KeybindingCode.Resist);
        w.Run(2 * Second);
        Assert.True(Said(bobChat, "break free of") || Said(bobChat, "fail to break free"), string.Join(" | ", bobChat.Select(t => t.Html)));

        // Grabbed aggressively again (from a pull or from the grab he could not break), then thrown west, away from Alice.
        using (w.World.Enter())
        {
            Waiting.NoSleep(me.stop_pulling());
            Waiting.NoSleep(them.forceMove(Builtins.locate(sx - 1, sy, z)));
        }
        w.Run(Second);
        CtrlClickBob(1);
        CtrlClickBob(1);
        Assert.Equal(GRAB_AGGRESSIVE, In(w, () => me.grab_state));
        int bobX = In(w, () => (int)them.x);
        alice.Session.Keybinding(KeybindingCode.ToggleThrowMode);
        w.Run(5);
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Turf, 0, sx - 6, sy, z), ClickButton.Left, ClickModifiers.None));
        w.Run(3 * Second);
        Assert.Null(In(w, () => me.pulling));
        int thrownX = In(w, () => (int)them.x);
        output.WriteLine($"Bob thrown from x {bobX} to x {thrownX}");
        Assert.True(thrownX < bobX);
        Assert.True(Said(aliceChat, bobName) && aliceChat.Any(t => t.Html.Contains("You ", StringComparison.Ordinal) && t.Html.Contains(bobName, StringComparison.Ordinal)));

        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, w.Err.ToString());
    }
}
