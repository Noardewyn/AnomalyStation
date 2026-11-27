using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Gaps;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Silicons;

/// <summary>
/// Silicons SI6b (D326): the newscaster in a booted toy room. The network opens with its two locked channels and an announcement
/// files a story there that every newscaster calls out; a crewman makes a channel, writes a story in tg's markdown and comments on
/// it through the window and its prompts; the curator censors and security posts and clears the wanted issue; a printed newspaper
/// keeps the issue of its moment; the bounty board's request is applied for and paid between bank accounts; the Newscaster and
/// Bounty Board apps share the network; the frame comes off the wall with a wrench and a broken newscaster is welded whole.
/// </summary>
public class NewscasterTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob, obj_machinery_newscaster Caster) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => (get_area(locate(4, 4, 1)) as area).requires_power = FALSE);
        r.Step(Second);
        VendingTests.Funds(r, bob, 1000);
        var caster = Place<obj_machinery_newscaster>(r, 4, 5);
        return (r, feed, bob, caster);
    }

    private static void Trim(AudioRoom r, mob_living_carbon_human who, Type trim) => r.In(() =>
    {
        Waiting.NoSleep(SSid_access.apply_trim_to_card((obj_item_card_id)who.wear_id, trim));
        return 0;
    });

    private static string Open(AudioRoom r, mob who, atom caster)
    {
        Take(who);
        Click(r, who, caster);
        JsonElement payload = LastUpdate(Take(who), out string window);
        Assert.Equal("PhysicalNewscaster", InterfaceOf(payload));
        return window;
    }

    // the prompt a newscaster act opened (tgui_alert's AlertModal, tgui_input_text's TextInputModal) answered through its window
    private static void Answer(AudioRoom r, mob who, string @interface, string action, string key, string value)
    {
        r.Step(4);
        string window = null;
        foreach (TguiOutput o in Take(who))
        {
            if (o.Kind != TguiOutputKind.Message)
                continue;
            using JsonDocument doc = JsonDocument.Parse(o.Json);
            if (doc.RootElement.GetProperty("type").GetString() == "update" && InterfaceOf(doc.RootElement.GetProperty("payload")) == @interface)
                window = o.WindowId;
        }
        Assert.NotNull(window);
        GapsHarness.Act(r, who, window, action, (key, value));
    }

    private static datum_feed_channel Channel(AudioRoom r, string name) => r.In(() => GLOB.news_network.network_channels_by_name[name] as datum_feed_channel);

    [Fact]
    public void The_network_opens_with_its_channels_and_an_announcement_files_a_story()
    {
        var (r, feed, bob, caster) = Room(9901);
        var announcements = Channel(r, NEWSCASTER_STATION_ANNOUNCEMENTS);
        Assert.NotNull(announcements);
        Assert.Equal((TRUE, "SS13"), (r.In(() => announcements.locked), r.In(() => announcements.author)));
        Assert.Equal(TRUE, r.In(() => Channel(r, NEWSCASTER_SPACE_BETTING).locked));
        Assert.Contains(caster, r.In(() => Dm.each<obj_machinery_newscaster>(GLOB.allCasters).ToList()));

        r.In(() => priority_announce("The station is fine.", "Status"));
        var story = r.In(() => (datum_feed_message)announcements.messages[announcements.messages.len]);
        Assert.Equal("Status<br><br>The station is fine.", r.In(() => story.body));
        Assert.Equal(r.In(() => Dm.str(command_name())), r.In(() => story.author));
        Assert.True(feed.Any("Breaking news from Station Announcements!"), "the newscaster calls out the story");
        Assert.Equal(TRUE, r.In(() => caster.alert));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/beep/twobeep_high.ogg");
        r.Step(51 * Second); // the alert lasts 50 seconds
        Assert.Equal(FALSE, r.In(() => caster.alert));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_crewman_makes_a_channel_writes_a_story_and_comments_on_it()
    {
        var (r, feed, bob, caster) = Room(9902);
        string holder = r.In(() => Dm.str(((obj_item_card_id)bob.wear_id).registered_account.account_holder));
        string window = Open(r, bob, caster);
        JsonElement data = Data(r, bob, out _);
        Assert.Equal(holder, data.GetProperty("user").GetProperty("name").GetString());

        GapsHarness.Act(r, bob, window, "startCreateChannel");
        Assert.Equal(TRUE, r.In(() => caster.creating_channel));
        GapsHarness.Act(r, bob, window, "setChannelName", ("channeltext", "Bob News"));
        GapsHarness.Act(r, bob, window, "setChannelDesc", ("channeldesc", "All the news about Bob."));
        GapsHarness.Act(r, bob, window, "createChannel", ("lockedmode", 1), ("cross_sector", 0));
        Answer(r, bob, "AlertModal", "choose", "choice", "Confirm");
        var channel = Channel(r, "Bob News");
        Assert.NotNull(channel);
        Assert.Equal((holder, "All the news about Bob.", TRUE), (r.In(() => channel.author), r.In(() => channel.channel_desc), r.In(() => channel.locked)));
        Assert.Equal(FALSE, r.In(() => caster.creating_channel));

        // a second channel by the same author is refused
        GapsHarness.Act(r, bob, window, "startCreateChannel");
        Answer(r, bob, "AlertModal", "choose", "choice", "Okay");
        Assert.Equal(FALSE, r.In(() => caster.creating_channel));

        // a story in tg's markdown, then a comment on it
        float id = r.In(() => Dm.num(channel.channel_id));
        GapsHarness.Act(r, bob, window, "setChannel", ("channel", id));
        GapsHarness.Act(r, bob, window, "createStory", ("current", id));
        Answer(r, bob, "TextInputModal", "submit", "entry", "Hello *world*");
        var story = r.In(() => Dm.each<datum_feed_message>(channel.messages).Single());
        Assert.Equal("<font face=\"Verdana\">Hello <i>world</i></font>", r.In(() => story.body));
        Assert.Equal(holder, r.In(() => story.author));
        Assert.True(feed.Any("Breaking news from Bob News!"), "the story alerts the newscasters");

        float messageId = r.In(() => Dm.num(story.message_id));
        GapsHarness.Act(r, bob, window, "startComment", ("messageID", messageId));
        Assert.Equal(TRUE, r.In(() => caster.creating_comment));
        GapsHarness.Act(r, bob, window, "setCommentBody", ("commenttext", "Nice story."));
        GapsHarness.Act(r, bob, window, "createComment");
        var comment = r.In(() => Dm.each<datum_feed_comment>(story.comments).Single());
        Assert.Equal((holder, "Nice story."), (r.In(() => comment.author), r.In(() => comment.body)));

        // the window shows the channel's stories with the comment
        data = Data(r, bob, out _, 20);
        JsonElement shown = data.GetProperty("messages").EnumerateArray().Single();
        Assert.Contains("<i>world</i>", shown.GetProperty("body").GetString());
        Assert.Equal("Nice story.", shown.GetProperty("comments").EnumerateArray().Single().GetProperty("body").GetString());
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_curator_censors_and_security_posts_the_wanted_issue()
    {
        var (r, feed, bob, caster) = Room(9903);
        var announcements = Channel(r, NEWSCASTER_STATION_ANNOUNCEMENTS);
        r.In(() => priority_announce("Hello.", "Greeting"));
        float messageId = r.In(() => Dm.num(((datum_feed_message)announcements.messages[announcements.messages.len]).message_id));
        string window = Open(r, bob, caster);
        float channelId = r.In(() => Dm.num(announcements.channel_id));
        GapsHarness.Act(r, bob, window, "setChannel", ("channel", channelId));

        // an assistant has no clearance
        GapsHarness.Act(r, bob, window, "storyCensor", ("messageID", messageId));
        Assert.True(feed.Any("Clearance not found."), "the assistant is refused");
        var story = r.In(() => (datum_feed_message)announcements.messages[announcements.messages.len]);
        Assert.False(r.In(() => Dm.truthy(story.body_censor)));

        // the curator (library access) censors the story and its author, then the channel
        Trim(r, bob, typeof(datum_id_trim_job_curator));
        GapsHarness.Act(r, bob, window, "storyCensor", ("messageID", messageId));
        GapsHarness.Act(r, bob, window, "authorCensor", ("messageID", messageId));
        Assert.True(r.In(() => Dm.truthy(story.body_censor) && Dm.truthy(story.author_censor)), "the story and its author are censored");
        Assert.Equal("[REDACTED]", r.In(() => story.return_body(-1)));
        GapsHarness.Act(r, bob, window, "channelDNotice", ("channel", channelId));
        Assert.Equal(TRUE, r.In(() => announcements.censored));

        // security posts the wanted issue: every newscaster calls it out and shows it on its screen
        Trim(r, bob, typeof(datum_id_trim_job_security_officer));
        GapsHarness.Act(r, bob, window, "toggleWanted");
        Assert.Equal(TRUE, r.In(() => caster.viewing_wanted));
        GapsHarness.Act(r, bob, window, "setCriminalName");
        Answer(r, bob, "TextInputModal", "submit", "entry", "John Doe");
        GapsHarness.Act(r, bob, window, "setCrimeData");
        Answer(r, bob, "TextInputModal", "submit", "entry", "Stole the spare ID.");
        GapsHarness.Act(r, bob, window, "submitWantedIssue");
        var wanted = r.In(() => GLOB.news_network.wanted_issue);
        Assert.True(r.In(() => Dm.truthy(wanted.active)), "the wanted issue is out");
        Assert.Equal(("John Doe", "Stole the spare ID."), (r.In(() => Dm.str(wanted.criminal)), r.In(() => Dm.str(wanted.body))));
        Assert.True(feed.Any("Attention! Wanted issue distributed!"), "the newscasters call it out");
        Assert.Contains("newscaster_wanted", r.In(() => Dm.each(Waiting.NoSleep(caster.update_overlays()) as DmList).Select(o => o is string s ? s : Dm.str((o as mutable_appearance)?.icon_state)).ToList()));
        GapsHarness.Act(r, bob, window, "clearWantedIssue");
        Assert.False(r.In(() => Dm.truthy(wanted.active)), "security clears it");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_printed_newspaper_keeps_the_issue_of_its_moment()
    {
        var (r, feed, bob, caster) = Room(9904);
        var announcements = Channel(r, NEWSCASTER_STATION_ANNOUNCEMENTS);
        r.In(() => priority_announce("Old news.", "First"));
        string window = Open(r, bob, caster);
        GapsHarness.Act(r, bob, window, "printNewspaper");
        Assert.Empty(r.In(() => Dm.each<obj_item_newspaper>(bob.loc.contents).ToList()));
        var paper = r.In(() =>
        {
            var p = Dm.@new<obj_item_paper>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(p));
            return p;
        });
        r.In(() => Waiting.NoSleep(caster.item_interaction(bob, paper)));
        Assert.Equal(1f, r.In(() => caster.paper_remaining));
        GapsHarness.Act(r, bob, window, "printNewspaper");
        Assert.Equal(0f, r.In(() => caster.paper_remaining));
        var newspaper = r.In(() => (obj_item_newspaper)bob.get_active_held_item() ?? Dm.each<obj_item_newspaper>(bob.loc.contents).First());
        Assert.NotNull(newspaper);

        // a later story and a later censorship are not in this issue
        r.In(() => priority_announce("New news.", "Second"));
        var first = r.In(() => Dm.each<datum_feed_message>(announcements.messages).First(m => m.body.Contains("Old news.")));
        r.In(() => first.toggle_censor_body());
        int page = (int)r.In(() => newspaper.news_content.Find(announcements));
        r.In(() => newspaper.current_page = page);
        DmList data = r.In(() => Waiting.NoSleep(newspaper.ui_data(bob)) as DmList);
        var messages = r.In(() => Dm.each<DmList>((DmList)((DmList)((DmList)data["channel_data"])[1])["channel_messages"]).Select(m => Dm.str(m["message"])).ToList());
        Assert.Contains("-First<br><br>Old news.", messages);
        Assert.DoesNotContain(messages, m => m.Contains("New news."));

        // the pages turn up to the last (the wanted page) and back
        object turned = r.In(() => Waiting.NoSleep(newspaper.ui_act("next_page", Dm.list(), Dm.@new<datum_tgui>(bob, newspaper, "Newspaper"), null)));
        Assert.Equal(TRUE, turned);
        Assert.Equal(page + 1f, r.In(() => newspaper.current_page));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_bounty_request_is_applied_for_and_paid()
    {
        var (r, feed, bob, caster) = Room(9905);
        mob_living_carbon_human alice = r.Join(5, 4, "Alice");
        datum_bank_account alicesAccount = VendingTests.Funds(r, alice, 0);
        datum_bank_account bobsAccount = r.In(() => ((obj_item_card_id)bob.wear_id).registered_account);

        float bobsStart = r.In(() => bobsAccount.account_balance);
        float alicesStart = r.In(() => alicesAccount.account_balance);
        string window = Open(r, bob, caster);
        GapsHarness.Act(r, bob, window, "bountyText", ("bountytext", "Bring me a donut."));
        GapsHarness.Act(r, bob, window, "bountyVal", ("bountyval", 150));
        GapsHarness.Act(r, bob, window, "createBounty");
        var request = r.In(() => Dm.each<datum_station_request>(GLOB.request_list).Single());
        Assert.Equal((150f, "Bring me a donut."), (r.In(() => Dm.num(request.value)), r.In(() => Dm.str(request.description))));
        Assert.True(feed.Any("New bounty added!"), "the boards call out the new bounty");

        // Alice applies at the same newscaster
        string aliceWindow = Open(r, alice, caster);
        object accNumber = r.In(() => request.req_number);
        // the newscaster keeps one current user, the last its window data was made for (ui_data, newscaster_machine.dm:136):
        // Alice's, right before her act, whenever SStgui last refreshed Bob's open window
        r.In(() => Waiting.NoSleep(caster.ui_data(alice)));
        GapsHarness.Act(r, alice, aliceWindow, "apply", ("request", accNumber));
        Assert.Same(alicesAccount, r.In(() => Dm.each<datum_bank_account>(request.applicants).Single()));

        // Bob pays her (his window's data right before his act, as for Alice's)
        Data(r, bob, out _);
        r.In(() => Waiting.NoSleep(caster.ui_data(bob)));
        GapsHarness.Act(r, bob, window, "payApplicant", ("request", accNumber), ("applicant", r.In(() => alicesAccount.account_id)));
        Assert.Equal(alicesStart + 150, r.In(() => alicesAccount.account_balance));
        Assert.Equal(bobsStart - 150, r.In(() => bobsAccount.account_balance));
        Assert.Equal(0, r.In(() => GLOB.request_list.len));
        Assert.True(feed.Any("Paid out 150 credits."), "the newscaster says so");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_apps_share_the_network()
    {
        var (r, feed, bob, caster) = Room(9906);
        var (pda, news, board) = r.In(() =>
        {
            var p = Dm.@new<obj_item_modular_computer_pda>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(p));
            Waiting.NoSleep(p.insert_id((obj_item_card_id)bob.wear_id, bob));
            var n = Dm.@new<datum_computer_file_program_newscaster>();
            var b = Dm.@new<datum_computer_file_program_bounty_board>();
            p.store_file(n);
            p.store_file(b);
            return (p, n, b);
        });
        // the Newscaster app is a newscaster of its own
        DmList statics = r.In(() => Waiting.NoSleep(news.ui_static_data(bob)) as DmList);
        Assert.Contains(NEWSCASTER_STATION_ANNOUNCEMENTS, r.In(() => Dm.each<DmList>((DmList)statics["channels"]).Select(c => Dm.str(c["name"])).ToList()));

        // the board app posts a request the newscaster lists
        DmList data = r.In(() => Waiting.NoSleep(board.ui_data(bob)) as DmList);
        Assert.Equal(r.In(() => ((obj_item_card_id)pda.stored_id).registered_account.account_holder), r.In(() => Dm.str(data["accountName"])));
        Act(r, bob, board, "bountyText", r.In(() => Dm.list(Dm.kv("bountytext", "Fix the vending machine."))));
        Act(r, bob, board, "createBounty", null);
        Assert.Equal(1, r.In(() => GLOB.request_list.len));
        Assert.True(feed.Any("New bounty has been added!"), "every board calls it out");
        DmList casterData = r.In(() => Waiting.NoSleep(caster.ui_data(bob)) as DmList);
        Assert.Equal("Fix the vending machine.", r.In(() => Dm.str(((DmList)((DmList)casterData["requests"])[1])["description"])));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_wrench_takes_the_newscaster_off_and_a_welder_mends_a_broken_one()
    {
        var (r, feed, bob, caster) = Room(9907);
        r.In(() => { Waiting.NoSleep(caster.take_damage(170, BRUTE)); return 0; }); // no armour flag: tg's melee armour would halve it
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(caster.machine_stat, BROKEN))), "the newscaster breaks");
        Assert.Contains("crack3", r.In(() => Dm.each(Waiting.NoSleep(caster.update_overlays()) as DmList).OfType<string>().ToList()));
        var welder = r.In(() =>
        {
            var w = Dm.@new<obj_item_weldingtool>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(w));
            w.switched_on(bob);
            return w;
        });
        r.Command(() => Waiting.Detach(caster.welder_act(bob, welder)));
        r.Step(6 * Second);
        Assert.False(r.In(() => Dm.truthy(Dm.bitand(caster.machine_stat, BROKEN))), "welded whole: " + string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.Equal(r.In(() => caster.max_integrity), r.In(() => caster.atom_integrity));

        var wrench = r.In(() =>
        {
            var w = Dm.@new<obj_item_wrench>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(w));
            return w;
        });
        r.Command(() => Waiting.Detach(caster.wrench_act(bob, wrench)));
        r.Step(8 * Second);
        Assert.True(r.In(() => QDELETED(caster)), "the newscaster comes off");
        Assert.Single(r.In(() => Dm.each<obj_item_wallframe_newscaster>(locate(4, 5, 1).contents).ToList()));
        r.AssertNoRuntimes();
    }

    private static object Act(AudioRoom r, mob user, datum host, string action, DmList @params) => r.In(() =>
    {
        World.Current.usr = user;
        return Waiting.NoSleep(host.ui_act(action, @params ?? Dm.list(), Dm.@new<datum_tgui>(user, host, "Test"), null));
    });
}
