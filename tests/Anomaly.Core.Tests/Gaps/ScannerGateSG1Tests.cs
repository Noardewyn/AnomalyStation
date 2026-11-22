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
/// Gaps package SG1 (D333): the scanner gate in a booted toy room with players. Walking under a gun-scanning gate with a laser
/// gun buzzes it, says what it found and flashes its alarm scanline, an ID with weapons access gets through with a "bypassed",
/// a clean pass flashes the scan; the window sets the mode and reverses it; an ID swipe locks the controls (the window closes to
/// a locked user), a swipe without access cannot unlock, an emag can; a pulse of the accept wire lights the green light. The
/// hypnochair closes on a body dropped in, strobes it blind and deaf for 45 seconds and implants its trigger phrase; an
/// interrupted session confuses and dizzies instead.
/// </summary>
public class ScannerGateSG1Tests
{
    private static string Diag(Feed feed, AudioRoom r) => "chat: " + string.Join(" | ", feed.Chat.Select(c => c.Html)) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);

    private static List<string> VisStates(AudioRoom r, atom thing) =>
        r.In(() => Dm.each<atom>(thing.vis_contents).Select(a => Dm.str(a.icon_state)).ToList());

    private static void Walk(AudioRoom r, mob who, int x, int y, float dir)
    {
        r.Step(Second);
        r.In(() => Waiting.NoSleep(who.Move(locate(x, y, 1), dir)));
        r.Step(4);
    }

    private static obj_item_card_id Card(AudioRoom r, mob_living_carbon_human wearer, Type trim) => r.In(() =>
    {
        var card = Dm.@new<obj_item_card_id_advanced>(wearer.loc);
        Waiting.NoSleep(SSid_access.apply_trim_to_card(card, trim));
        if (wearer.w_uniform == null) // the ID slot hangs on a jumpsuit: a joined player wears none
            Waiting.NoSleep(wearer.equip_to_slot_or_del(Dm.@new<obj_item_clothing_under_color_grey>(wearer.loc), ITEM_SLOT_ICLOTHING));
        Waiting.NoSleep(wearer.equip_to_slot_or_del(card, ITEM_SLOT_ID));
        Assert.Same(card, wearer.wear_id);
        return (obj_item_card_id)card;
    });

    [Fact]
    public void A_gun_scanning_gate_buzzes_at_a_gun_and_lets_weapons_access_through()
    {
        var r = new AudioRoom(13, 9, seed: 9961);
        Feed feed = Listen(r);
        var gate = Place<obj_machinery_scanner_gate_preset_guns>(r, 6, 4);
        r.In(() => gate.base_false_beep = 0); // no false positives in a test
        var bob = r.Join(5, 4, "Bob");
        r.Step(Second);
        Assert.Contains("passive", VisStates(r, gate));
        Assert.Equal("Guns", r.In(() => gate.scangate_mode));
        Assert.Equal(1f, r.In(() => gate.locked));

        // a clean pass: the scan flashes, no buzz
        Walk(r, bob, 6, 4, EAST);
        Assert.Contains("scanning", VisStates(r, gate));
        Assert.DoesNotContain(r.Sounds.Heard, s => (s.File ?? "").EndsWith("scanbuzz.ogg", StringComparison.Ordinal));
        r.Step(2 * Second);
        Assert.Contains("passive", VisStates(r, gate));

        // with a laser gun: the buzz and the alarm
        Walk(r, bob, 5, 4, WEST);
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(Dm.@new<obj_item_gun_energy_laser>(bob.loc))));
        Walk(r, bob, 6, 4, EAST);
        Assert.True(feed.Any("Weapons detected!!"), Diag(feed, r));
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("scanbuzz.ogg", StringComparison.Ordinal));
        Assert.Contains("alarm", VisStates(r, gate));

        // weapons access: bypassed
        Card(r, bob, typeof(datum_id_trim_job_bartender));
        Walk(r, bob, 5, 4, WEST);
        r.Step(3 * Second); // the beep's cooldown
        Walk(r, bob, 6, 4, EAST);
        Assert.True(feed.Any("Weapons detection bypassed."), Diag(feed, r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_window_sets_the_mode_an_id_locks_it_and_an_emag_breaks_the_lock()
    {
        var r = new AudioRoom(13, 9, seed: 9962);
        Feed feed = Listen(r);
        var gate = Place<obj_machinery_scanner_gate>(r, 6, 4);
        var bob = r.Join(5, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);

        Click(r, bob, gate);
        var payload = LastUpdate(Take(bob), out string windowId);
        Assert.Equal("ScannerGate", InterfaceOf(payload));
        Assert.Equal("Off", payload.GetProperty("data").GetProperty("scan_mode").GetString());
        Assert.Contains(payload.GetProperty("static_data").GetProperty("available_species").EnumerateArray(), s => s.GetProperty("specie_id").GetString() == "human");

        Act(r, bob, windowId, "set_mode", ("new_mode", "Mindshield"));
        Assert.Equal("Mindshield", r.In(() => gate.scangate_mode));
        Act(r, bob, windowId, "toggle_reverse");
        Assert.Equal(1f, r.In(() => gate.reverse));
        Act(r, bob, windowId, "set_mode", ("new_mode", "Bogus"));
        Assert.Equal("Mindshield", r.In(() => gate.scangate_mode));
        Act(r, bob, windowId, "set_target_nutrition", ("new_nutrition", "Starving"));
        Assert.Equal((float)NUTRITION_LEVEL_STARVING, r.In(() => gate.detect_nutrition));

        // the reversed mindshield mode buzzes for Bob, who has none
        r.In(() => gate.base_false_beep = 0);
        r.In(() => Waiting.NoSleep(gate.perform_scan(bob)));
        Assert.True(feed.Any("Mindshield not detected!!"), Diag(feed, r));

        // an engineer's card locks it; a cardless swipe can't unlock it; the emag can
        var card = r.In(() =>
        {
            var c = Dm.@new<obj_item_card_id_advanced>(bob.loc);
            Waiting.NoSleep(SSid_access.apply_trim_to_card(c, typeof(datum_id_trim_job_station_engineer)));
            Waiting.NoSleep(bob.put_in_active_hand(c));
            return (obj_item_card_id)c;
        });
        r.In(() => Waiting.NoSleep(gate.item_interaction(bob, card)));
        Assert.Equal(1f, r.In(() => gate.locked));
        Assert.True(r.In(() => Dm.@in(ACCESS_ENGINEERING, gate.req_access)));
        Assert.False(r.In(() => Dm.truthy(Waiting.NoSleep(gate.can_interact(bob)))));

        var other = r.In(() =>
        {
            var c = Dm.@new<obj_item_card_id_advanced>(bob.loc);
            Waiting.NoSleep(SSid_access.apply_trim_to_card(c, typeof(datum_id_trim_job_cook)));
            return (obj_item_card_id)c;
        });
        r.In(() =>
        {
            Waiting.NoSleep(bob.dropItemToGround(card));
            Waiting.NoSleep(bob.put_in_active_hand(other));
            Assert.Equal(ITEM_INTERACT_BLOCKING, Waiting.NoSleep(gate.item_interaction(bob, other)));
        });
        Assert.Equal(1f, r.In(() => gate.locked));

        r.In(() => Waiting.NoSleep(gate.emag_act(bob, Dm.@new<obj_item_card_emag>(bob.loc))));
        Assert.Equal(0f, r.In(() => gate.locked));
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(gate.obj_flags, EMAGGED))));
        Assert.Equal(0, r.In(() => Dm.length(gate.req_access)));

        // the accept wire's pulse lights the green light
        r.In(() => Waiting.NoSleep(gate.wires.on_pulse(WIRE_ACCEPT, bob)));
        Assert.Equal(1f, r.In(() => gate.light_pass));
        Assert.Contains("The Green light is on.", r.In(() => Dm.each(gate.wires.get_status()).Select(Dm.str).ToList()));

        // the screwdriver opens the panel: the open picture
        r.In(() =>
        {
            var screwdriver = Dm.@new<obj_item_screwdriver>(bob.loc);
            Waiting.NoSleep(gate.screwdriver_act(bob, screwdriver));
        });
        r.Step(2);
        Assert.Equal("scangate_open", r.In(() => Dm.str(gate.icon_state)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_hypnochair_implants_its_trigger_phrase_and_an_interrupted_session_confuses()
    {
        var r = new AudioRoom(13, 9, seed: 9963);
        Feed feed = Listen(r);
        var chair = Place<obj_machinery_hypnochair>(r, 6, 4);
        var bob = r.Join(5, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        var victim = r.Dummy(6, 5);
        r.Step(Second);
        Assert.Equal(1f, r.In(() => chair.state_open));
        Assert.Equal("hypnochair_open", r.In(() => Dm.str(chair.icon_state)));

        r.In(() => Waiting.NoSleep(chair.mouse_drop_receive(victim, bob, null)));
        r.Step(2);
        Assert.Same(chair, r.In(() => victim.loc));
        Assert.Equal("hypnochair_occupied", r.In(() => Dm.str(chair.icon_state)));

        Click(r, bob, chair);
        var payload = LastUpdate(Take(bob), out string windowId);
        Assert.Equal("HypnoChair", InterfaceOf(payload));
        Assert.Equal(1, payload.GetProperty("data").GetProperty("occupied").GetInt32());

        // no phrase: the buzz
        Act(r, bob, windowId, "interrogate");
        Assert.Equal(0f, r.In(() => chair.interrogating));
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("buzz-sigh.ogg", StringComparison.Ordinal));

        Act(r, bob, windowId, "set_phrase", ("phrase", "banana"));
        Act(r, bob, windowId, "interrogate");
        r.In(() =>
        {
            Assert.Equal(1f, chair.interrogating);
            Assert.True(Dm.truthy(victim.is_blind()));
            Assert.True(HAS_TRAIT(victim, TRAIT_DEAF));
        });
        Assert.Equal("hypnochair_active", r.In(() => Dm.str(chair.icon_state)));

        r.Step(46 * Second);
        r.In(() =>
        {
            Assert.Equal(0f, chair.interrogating);
            Assert.Equal("", chair.trigger_phrase);
            Assert.False(Dm.truthy(victim.is_blind()));
            Assert.False(HAS_TRAIT(victim, TRAIT_DEAF));
            Assert.True(Dm.truthy(victim.has_trauma_type(typeof(datum_brain_trauma_severe_hypnotic_trigger))) || Dm.truthy(victim.has_trauma_type(typeof(datum_brain_trauma_severe_hypnotic_stupor))),
                "no hypnotic trauma");
        });
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("ping.ogg", StringComparison.Ordinal));
        Assert.True(feed.Any("pings!"), Diag(feed, r));

        // a second session interrupted at five seconds: confusion and dizziness
        Act(r, bob, windowId, "set_phrase", ("phrase", "apple"));
        Act(r, bob, windowId, "interrogate");
        r.Step(5 * Second);
        Act(r, bob, windowId, "interrogate");
        r.In(() =>
        {
            Assert.Equal(0f, chair.interrogating);
            Assert.Null(chair.victim);
            Assert.NotNull(victim.has_status_effect(typeof(datum_status_effect_confusion)));
            Assert.NotNull(victim.has_status_effect(typeof(datum_status_effect_dizziness)));
        });

        // the door opens the chair again
        Act(r, bob, windowId, "door");
        Assert.Equal(1f, r.In(() => chair.state_open));
        r.AssertNoRuntimes();
    }
}
