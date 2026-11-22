using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package R5 (D327): the round end's commendations and credits in a booted toy room. Bob thanks Alice through tg's three dialogs
/// and she is hearted at the round's end; the shuttle's departure asks the round's players; without a contributors.dmi no credits roll.
/// </summary>
public class RoundEndR5Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob, mob_living_carbon_human Alice) Room(ulong seed)
    {
        var r = new AudioRoom(15, 11, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        mob_living_carbon_human alice = r.Join(5, 4, "Alice");
        r.Step(Second);
        return (r, feed, bob, alice);
    }

    // an act in the newest window of that interface the player has open (tg's input windows: AlertModal, TextInputModal)
    private static void Act(AudioRoom r, mob who, string iface, string action, string key, object value)
    {
        r.Step(2);
        var open = r.In(() => Dm.each_cast<datum_tgui>(who.tgui_open_uis).Select(u => Dm.str(u.@interface)).ToList());
        Assert.True(open.Contains(iface), "no " + iface + " open; open: " + string.Join(", ", open));
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(who.tgui_open_uis).Last(u => Dm.str(u.@interface) == iface).window.id));
        r.Command(() => ClientSeam.TguiMessage(who.client, window, "act/" + action, Dm.list(Dm.kv(key, value))));
        r.Step(4);
    }

    [Fact]
    public void ACommendationThroughTheDialogsHeartsTheNomineeAtTheRoundsEnd()
    {
        var (r, feed, bob, alice) = Room(4901);
        string first = r.In(() => Dm.str(alice.real_name)).Split(' ')[0];
        r.In(() => Waiting.Detach(bob.query_heart(1)));
        Act(r, bob, "AlertModal", "choose", "choice", "Yes");
        Act(r, bob, "TextInputModal", "submit", "entry", first);
        Act(r, bob, "AlertModal", "choose", "choice", "Yes!");
        Assert.True(feed.Chat.Any(c => c.Target == bob && c.Html.Contains("Commendation sent!")), "no commendation sent");
        Assert.Contains(r.In(() => Dm.str(alice.ckey)), r.In(() => Dm.each(SSticker.hearts).Select(Dm.str).ToList()));
        Assert.False(r.In(() => Dm.truthy(alice.client.prefs.hearted)), "hearted before the round's end");

        r.In(() => SSticker.handle_hearts());
        Assert.True(feed.Chat.Any(c => (c.Target == alice || c.Target == alice.client) && c.Html.Contains("Someone awarded you a heart!")), "Alice was told nothing");
        Assert.True(r.In(() => Dm.truthy(alice.client.prefs.hearted)));
        Assert.True(r.In(() => Dm.num(alice.client.prefs.hearted_until) > world.realtime + 23 * HOURS), "the heart does not last a day");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheShuttlesDepartureAsksTheRoundsPlayersToThankSomeone()
    {
        var (r, _, bob, alice) = Room(4902);
        r.In(() =>
        {
            ((datum_config_entry)config.entries_by_type[typeof(datum_config_entry_number_commendation_percent_poll)]).config_entry_value = 1;
            GLOB.joined_player_list = Dm.list(bob.ckey, alice.ckey);
            SSticker.poll_hearts();
        });
        r.Step(2);
        foreach (var who in new[] { bob, alice })
            Assert.Contains("AlertModal", r.In(() => Dm.each_cast<datum_tgui>(who.tgui_open_uis).Select(u => Dm.str(u.@interface)).ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WithoutAContributorsIconNoCreditsRoll()
    {
        var (r, _, bob, _) = Room(4903);
        Assert.False(r.In(() => Dm.truthy(fexists(client.CREDITS_PATH))), "the test config has a contributors.dmi");
        r.In(() => bob.client.RollCredits());
        r.Step(Second);
        Assert.Null(r.In(() => bob.client.credits));
        r.In(() => bob.client.ClearCredits());
        r.Step(2);
        Assert.False(InertSeam.IsInertType(typeof(atom_movable_screen_credit)));
        r.AssertNoRuntimes();
    }
}
