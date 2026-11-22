using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The owner (2026-11-08): tg's votes (SSvote, the restart and custom votes, VotePanel) through the real server. Alice's Esc menu Vote
/// (BYOND's winset command link, DV364) opens the vote window, where the restart vote can be started and the custom vote cannot (admins
/// only); she starts it, both read tg's purple box with its "click here" link and get the vote action button; Bob opens the window by
/// the link, both vote to restart, and at the vote's end tg's result box names the winner and the round is forced to end. Another vote
/// is refused for the cooldown.
/// </summary>
public class VoteGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static UiWindowState Window(RealHeadlessWorld.Player p) => p.Session.Replica.UiWindows.Values.SingleOrDefault(v => v.Interface == "VotePanel");

    private static UiNode Drawn(UiWindowState window)
        => Anomaly.Client.Shared.Tgui.Interfaces.VotePanel.Render(new TguiContext(window.Window, window.Interface, window.Title, 2, window.Merged, (_, _) => { }, new TguiLocalState()));

    [Fact]
    public void A_restart_vote_is_started_from_the_vote_window_voted_and_ends_the_round()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 41, TimeoutSteps = 4000 }, strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice"), bob = w.Join("Bob");
        var aliceTexts = new List<string>();
        var bobTexts = new List<string>();
        alice.Client.ServerTextReceived += t => aliceTexts.Add(t.Html);
        bob.Client.ServerTextReceived += t => bobTexts.Add(t.Html);
        w.Run(Second);
        Assert.Equal(new[] { "Custom", "Restart" }, In(w, () => Dm.each<object>(SSvote.possible_votes).Select(k => Dm.str(k)).OrderBy(k => k, StringComparer.Ordinal).ToArray())); // no map rotation: no map vote
        In(w, () => Waiting.NoSleep(config.Set(typeof(datum_config_entry_number_vote_period), 40))); // 4 s, as an admin's config would

        // ---- 1. The Esc menu's Vote opens the vote window: Restart can be started, Custom only by an admin.
        Assert.True(alice.Session.Topic(HtmlText.VoteCommand));
        w.Run(4);
        UiWindowState window = Window(alice);
        Assert.NotNull(window);
        UiNode[] buttons = Drawn(window).Walk().Where(n => n.Kind == UiKind.Button).ToArray();
        Assert.False(Assert.Single(buttons, b => b.Key == "call-Restart").Disabled);
        Assert.True(Assert.Single(buttons, b => b.Key == "call-Custom").Disabled);
        Assert.Contains(Drawn(window).Walk(), n => n.Text == "No vote active!");

        // ---- 2. She starts it: tg's box with the link to everyone, a vote button for each player.
        Assert.True(alice.Session.UiAct(window.Window, "callVote", UiValue.ObjectOf(("voteName", UiValue.Of("Restart")))));
        w.Run(4);
        string started = Assert.Single(bobTexts, t => t.Contains("Restart vote started by Alice.", StringComparison.Ordinal));
        Assert.Contains("byond://winset?command=vote", started);
        Assert.Contains(aliceTexts, t => t.Contains("Restart vote started by Alice.", StringComparison.Ordinal));
        Assert.True(In(w, () => Dm.each<datum_action>(w.MobOf(bob).actions).Any(a => a is datum_action_vote && Dm.str(a.name) == "Vote: Restart")));

        // ---- 3. Bob opens the window by the link; both vote to restart.
        Assert.True(bob.Session.Topic(HtmlText.VoteCommand));
        w.Run(4);
        UiWindowState bobWindow = Window(bob);
        Assert.NotNull(bobWindow);
        Assert.Contains(Drawn(bobWindow).Walk(), n => n.Kind == UiKind.Button && n.Key == "vote-Restart Round" && !n.Disabled);
        Assert.True(bob.Session.UiAct(bobWindow.Window, "voteSingle", UiValue.ObjectOf(("voteOption", UiValue.Of("Restart Round")))));
        Assert.True(alice.Session.UiAct(Window(alice).Window, "voteSingle", UiValue.ObjectOf(("voteOption", UiValue.Of("Restart Round")))));
        w.Run(4);
        Assert.Equal(2f, In(w, () => Dm.num(SSvote.current_vote.choices["Restart Round"])));
        Assert.Contains(Drawn(Window(bob)).Walk(), n => n.Kind == UiKind.Icon && n.Icon == "vote-yea"); // his pick is marked
        Assert.Contains(Drawn(Window(bob)).Walk(), n => n.Kind == UiKind.Button && n.Key == "vote-Restart Round" && n.Disabled); // and cannot be picked again

        // ---- 4. The vote ends: the result box, the round forced to end, the buttons and windows gone.
        w.Run(Second * 5);
        Assert.Contains(bobTexts, t => t.Contains("Vote Result: Restart Round", StringComparison.Ordinal));
        output.WriteLine(bobTexts.Last(t => t.Contains("Vote Result", StringComparison.Ordinal)));
        Assert.Null(In(w, () => SSvote.current_vote));
        Assert.False(In(w, () => Dm.each<datum_action>(w.MobOf(bob).actions).Any(a => a is datum_action_vote)));
        Assert.Null(Window(bob));
        Assert.Equal((float)FORCE_END_ROUND, In(w, () => Dm.num(SSticker.force_ending)));

        // ---- 5. Another vote waits for tg's 10 minute cooldown.
        Assert.True(alice.Session.Topic(HtmlText.VoteCommand));
        w.Run(4);
        Assert.True(alice.Session.UiAct(Window(alice).Window, "callVote", UiValue.ObjectOf(("voteName", UiValue.Of("Restart")))));
        w.Run(4);
        Assert.Contains(aliceTexts, t => t.Contains("A vote was initiated recently.", StringComparison.Ordinal));
        Assert.Null(In(w, () => SSvote.current_vote));
    }
}
