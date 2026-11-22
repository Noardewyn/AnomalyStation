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
/// Gaps package SC1 (D333): the requests console in a booted toy room with players: an emergency call from an engineering console
/// through its window grants the security officer's card engineering's door access for five minutes (RETA), lights the engineering
/// door and lets the officer through it, keeps the department pair on cooldown, and takes it all back at the expiry; a department
/// message arrives at its console over the request consoles' subspace signal with its priority on the console's screen and in its
/// window, the window clears it, and a console with no message server in reach reports the failed send.
/// </summary>
public class RequestsConsoleSC1Tests
{
    private static string Diag(Feed feed, AudioRoom r) => "chat: " + string.Join(" | ", feed.Chat.Select(c => c.Html)) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);

    private static List<string> OverlayStates(AudioRoom r, atom thing) =>
        r.In(() => Dm.each(thing.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    private static obj_item_card_id Card(AudioRoom r, mob_living_carbon_human wearer, Type trim, string name) => r.In(() =>
    {
        var card = Dm.@new<obj_item_card_id_advanced>(wearer.loc);
        Waiting.NoSleep(SSid_access.apply_trim_to_card(card, trim));
        card.registered_name = name;
        if (wearer.w_uniform == null) // the ID slot hangs on a jumpsuit: a joined player wears none
            Waiting.NoSleep(wearer.equip_to_slot_or_del(Dm.@new<obj_item_clothing_under_color_grey>(wearer.loc), ITEM_SLOT_ICLOTHING));
        Waiting.NoSleep(wearer.equip_to_slot_or_del(card, ITEM_SLOT_ID));
        Assert.Same(card, wearer.wear_id);
        return (obj_item_card_id)card;
    });

    [Fact]
    public void An_emergency_call_grants_the_called_departments_cards_the_callers_doors_until_it_expires()
    {
        obj_machinery_requests_console atmos = null;
        var r = new AudioRoom(13, 9, seed: 9901, mapExtras: w =>
        {
            atmos = Dm.@new<obj_machinery_requests_console_directional_south>(locate(5, 2, 1));
            atmos.name = "Atmospherics"; // RETA's origin department: Engineering
        });
        Feed feed = Listen(r);
        var bob = r.Join(5, 3, "Bob");
        var officer = r.Join(10, 6, "Officer");
        var card = Card(r, officer, typeof(datum_id_trim_job_security_officer), "Olga Officer");
        var door = r.In(() => Dm.@new<obj_machinery_door_airlock_engineering>(locate(8, 6, 1)));
        r.In(() => door.req_access = Dm.list(ACCESS_ENGINEERING));
        r.Step(Second);

        // the job subsystem filled RETA's tables at boot (job.dm:103)
        r.In(() =>
        {
            Assert.True(Dm.@in(typeof(datum_id_trim_job_security_officer), GLOB.reta_job_trims["Security"]));
            Assert.True(Dm.@in(ACCESS_ATMOSPHERICS, GLOB.reta_dept_grants["Engineering"]));
            Assert.False(Dm.truthy(door.check_access(card)), "the officer's own card opens engineering");
            Assert.False(Dm.truthy(door.has_active_reta_access()));
        });

        // Bob calls security from the console's window
        Click(r, bob, atmos);
        Data(r, bob, out string windowId);
        Act(r, bob, windowId, "set_emergency", ("emergency", "Security"));
        r.Step(Second);

        r.In(() =>
        {
            Assert.Equal("Security", Dm.str(atmos.emergency));
            Assert.True(Dm.@in(ACCESS_ENGINEERING, card.access), Diag(feed, r));
            Assert.True(Dm.@in(ACCESS_ATMOSPHERICS, card.access));
            Assert.True(card.has_reta_access_for_dept("Engineering"));
            Assert.True(Dm.@in(card, GLOB.reta_active_cards));
            Assert.True(Dm.num(((DmList)GLOB.reta_active_grants["Security"])["Engineering"]) > Dm.num(world.time));
            Assert.True(reta_on_cooldown("Engineering", "Security"));
            Assert.True(Dm.truthy(door.check_access(card)), "the granted card does not open engineering");
            Assert.Equal("security", Dm.str(door.has_active_reta_access()));
        });
        Assert.Contains("lights_reta", OverlayStates(r, door));
        Assert.Contains("req_comp3", OverlayStates(r, atmos));
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("cryo_warning.ogg", StringComparison.Ordinal));

        // the window shows the call and the cooldown
        var data = Data(r, bob, out _);
        Assert.Equal("Security", data.GetProperty("emergency").GetString());
        Assert.Equal(1, data.GetProperty("reta_cooldowns").GetProperty("Security").GetInt32());

        // the pair's cooldown ends after 15 seconds; the grant lasts five minutes
        r.Step(16 * Second);
        Assert.False(r.In(() => reta_on_cooldown("Engineering", "Security")));
        Assert.True(r.In(() => Dm.@in(ACCESS_ENGINEERING, card.access)));

        r.Step(5 * 60 * Second);
        r.In(() =>
        {
            Assert.False(Dm.@in(ACCESS_ENGINEERING, card.access), "the grant did not expire");
            Assert.False(Dm.@in(ACCESS_ATMOSPHERICS, card.access));
            Assert.False(card.has_any_reta_access());
            Assert.False(Dm.@in(card, GLOB.reta_active_cards));
            Assert.False(Dm.truthy(GLOB.reta_active_grants["Security"]));
            Assert.False(Dm.truthy(door.has_active_reta_access()));
            Assert.False(Dm.truthy(atmos.emergency));
            Assert.True(Dm.truthy(card.access) && Dm.@in(ACCESS_SECURITY, card.access), "the card lost its own access");
        });
        Assert.DoesNotContain("lights_reta", OverlayStates(r, door));
        Assert.True(feed.Any("Emergency access to Engineering has expired."), Diag(feed, r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_self_call_is_refused_and_a_card_made_during_a_grant_gets_it_for_the_time_left()
    {
        obj_machinery_requests_console medbay = null;
        var r = new AudioRoom(13, 9, seed: 9902, mapExtras: w =>
        {
            medbay = Dm.@new<obj_machinery_requests_console_directional_south>(locate(5, 2, 1));
            medbay.department = "Medbay"; // RETA's origin department: Medical
        });
        Feed feed = Listen(r);
        var bob = r.Join(5, 3, "Bob");
        r.Step(Second);

        // a doctor cannot call medical from medbay
        r.In(() => bob.mind.set_assigned_role(SSjob.get_job_type(typeof(datum_job_doctor)) as datum_job));
        Click(r, bob, medbay);
        Data(r, bob, out string windowId);
        Act(r, bob, windowId, "set_emergency", ("emergency", "Medical"));
        Assert.False(r.In(() => Dm.truthy(medbay.emergency)));
        Assert.True(feed.Any("You cannot call your own department for emergency assistance."), Diag(feed, r));

        // engineering from medbay: the engineer on duty is granted medical's doors
        var engineer = r.Join(10, 6, "Engineer");
        var card = Card(r, engineer, typeof(datum_id_trim_job_station_engineer), "Ed Engineer");
        Act(r, bob, windowId, "set_emergency", ("emergency", "Engineering"));
        r.Step(Second);
        r.In(() =>
        {
            Assert.Equal("Engineering", Dm.str(medbay.emergency));
            Assert.True(Dm.@in(ACCESS_MEDICAL, card.access), Diag(feed, r));
            Assert.True(Dm.@in(ACCESS_SURGERY, card.access));
            Assert.True(Dm.truthy(((DmList)GLOB.reta_active_grants["Engineering"])["Medical"]));
        });

        // a minute in, a second engineer's new card gets the grant too, for the four minutes left
        r.Step(60 * Second);
        var late = r.Join(10, 7, "Late");
        var lateCard = Card(r, late, typeof(datum_id_trim_job_station_engineer), "Lee Late");
        r.In(() =>
        {
            Assert.True(Dm.@in(ACCESS_MEDICAL, lateCard.access), "the new card did not get the active grant");
            Assert.True(lateCard.has_reta_access(ACCESS_SURGERY));
            Assert.Single(Dm.each(lateCard.get_reta_summary()));
        });
        r.Step(4 * 60 * Second + 2 * Second);
        r.In(() =>
        {
            Assert.False(Dm.@in(ACCESS_MEDICAL, card.access), "the grant did not expire");
            Assert.False(Dm.@in(ACCESS_MEDICAL, lateCard.access), "the new card's grant did not expire with the grant's time left");
            Assert.False(Dm.truthy(GLOB.reta_active_grants["Engineering"]));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_department_message_arrives_over_the_signal_and_the_window_shows_and_clears_it()
    {
        obj_machinery_requests_console bar = null, medbay = null;
        var r = new AudioRoom(13, 9, seed: 9903, mapExtras: w =>
        {
            bar = Dm.@new<obj_machinery_requests_console_directional_south>(locate(3, 2, 1));
            bar.department = "Bar";
            medbay = Dm.@new<obj_machinery_requests_console_directional_south>(locate(8, 2, 1));
            medbay.department = "Medbay";
            Dm.@new<obj_effect_mapping_helpers_requests_console_assistance>(locate(8, 2, 1));
        });
        Feed feed = Listen(r);
        var bob = r.Join(8, 3, "Bob");
        r.Step(Second);
        Assert.Contains("req_comp0", OverlayStates(r, medbay));

        // the bar's message as the relays deliver it (message_server.dm:233)
        r.In(() =>
        {
            var signal = Dm.@new<datum_signal_subspace_messaging_rc>(bar, Dm.list(
                Dm.kv("sender_department", "Bar"),
                Dm.kv("recipient_department", "Medbay"),
                Dm.kv("message", "A patron fell over"),
                Dm.kv("verified", ""),
                Dm.kv("stamped", ""),
                Dm.kv("priority", REQ_HIGH_MESSAGE_PRIORITY),
                Dm.kv("notify_channel", null),
                Dm.kv("request_type", ASSISTANCE_REQUEST)));
            signal.broadcast();
        });
        r.Step(Second);
        r.In(() =>
        {
            Assert.Equal(1, Dm.length(medbay.messages));
            Assert.Equal(0, Dm.length(bar.messages));
            Assert.Equal((float)REQ_HIGH_MESSAGE_PRIORITY, medbay.new_message_priority);
        });
        Assert.Contains("req_comp2", OverlayStates(r, medbay));
        Assert.True(feed.Any("Message from Bar"), Diag(feed, r));
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("twobeep_high.ogg", StringComparison.Ordinal));

        // the window lists it; opening the messages clears the screen
        Click(r, bob, medbay);
        var data = Data(r, bob, out string windowId);
        var message = data.GetProperty("messages")[0];
        Assert.Equal("A patron fell over", message.GetProperty("content").GetString());
        Assert.Equal("Bar", message.GetProperty("sender_department").GetString());
        Assert.Equal(2, message.GetProperty("priority").GetInt32());
        Assert.Equal(2, data.GetProperty("new_message_priority").GetInt32());
        Act(r, bob, windowId, "clear_message_status");
        r.Step(Second);
        Assert.Equal(0f, r.In(() => medbay.new_message_priority));
        Assert.Contains("req_comp0", OverlayStates(r, medbay));

        // a reply with no message server in reach: the send fails and the console buzzes
        Act(r, bob, windowId, "send_message", ("recipient", "Bar"), ("message", "On our way"), ("priority", REQ_NORMAL_MESSAGE_PRIORITY), ("request_type", REPLY_REQUEST));
        r.Step(Second);
        Assert.Equal(1f, r.In(() => medbay.has_mail_send_error));
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("buzz-two.ogg", StringComparison.Ordinal));
        Assert.True(Data(r, bob, out _).GetProperty("has_mail_send_error").GetInt32() == 1);

        // the speaker toggle
        Act(r, bob, windowId, "toggle_silent");
        Assert.Equal(1f, r.In(() => medbay.silent));
        r.AssertNoRuntimes();
    }
}
