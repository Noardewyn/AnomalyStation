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
/// Gaps package LC1 (D333, lavaland.md L3): the labour camp's machines in a booted toy room with players. The warden closes the
/// prisoner in the labour camp teleporter, inserts a prisoner ID and sets its goal in the teleporter console's window and
/// processes the prisoner: stripped into the item reclaimer, dressed in the prison jumpsuit, orange shoes and the ID, sent to the
/// camp's beacon; the reclaimer gives the things back once the ID meets its goal. The prisoner management console logs the
/// warden in, shows the prisoner ID and the tracked implants with their buttons, and a chem implant's inject button injects. The
/// plate press pays a prisoner for each plate loaded and presses it in four seconds; the produceporter sends grown food to the
/// produce pad and pays for it.
/// </summary>
public class LaborCampLC1Tests
{
    private static string Diag(Feed feed, AudioRoom r) => "chat: " + string.Join(" | ", feed.Chat.Select(c => c.Html)) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);

    // the ID slot hangs on a jumpsuit: a joined player wears none
    private static void Jumpsuit(mob_living_carbon_human wearer)
    {
        if (wearer.w_uniform == null)
            Waiting.NoSleep(wearer.equip_to_slot_or_del(Dm.@new<obj_item_clothing_under_color_grey>(wearer.loc), ITEM_SLOT_ICLOTHING));
    }

    private static obj_item_card_id Card(AudioRoom r, mob_living_carbon_human wearer, Type trim) => r.In(() =>
    {
        Jumpsuit(wearer);
        var card = Dm.@new<obj_item_card_id_advanced>(wearer.loc);
        Waiting.NoSleep(SSid_access.apply_trim_to_card(card, trim));
        Waiting.NoSleep(wearer.equip_to_slot_or_del(card, ITEM_SLOT_ID));
        Assert.Same(card, wearer.wear_id);
        return (obj_item_card_id)card;
    });

    [Fact]
    public void The_teleporter_console_processes_a_prisoner_and_the_reclaimer_returns_the_things_at_the_goal()
    {
        obj_machinery_computer_prisoner_gulag_teleporter_computer console = null;
        obj_machinery_gulag_teleporter teleporter = null;
        obj_machinery_gulag_item_reclaimer reclaimer = null;
        obj_structure_gulag_beacon beacon = null;
        var r = new AudioRoom(13, 9, seed: 9971, mapExtras: w =>
        {
            reclaimer = Dm.@new<obj_machinery_gulag_item_reclaimer>(locate(2, 2, 1));
            teleporter = Dm.@new<obj_machinery_gulag_teleporter>(locate(6, 4, 1));
            console = Dm.@new<obj_machinery_computer_prisoner_gulag_teleporter_computer>(locate(5, 4, 1));
            beacon = Dm.@new<obj_structure_gulag_beacon>(locate(11, 7, 1));
        });
        Feed feed = Listen(r);
        var warden = r.Join(5, 5, "Warden");
        r.In(() => ADD_TRAIT(warden, TRAIT_ADVANCEDTOOLUSER, "test"));
        Card(r, warden, typeof(datum_id_trim_job_warden));
        var prisoner = r.Join(7, 4, "Prisoner");
        var toolbox = r.In(() =>
        {
            var box = Dm.@new<obj_item_storage_toolbox>(prisoner.loc);
            Waiting.NoSleep(prisoner.put_in_active_hand(box));
            return box;
        });
        r.Step(Second);
        r.In(() =>
        {
            Assert.Same(teleporter, console.teleporter);
            Assert.Same(beacon, console.beacon);
            Assert.Same(reclaimer, teleporter.linked_reclaimer);
            Assert.Same(teleporter, reclaimer.linked_teleporter);
        });

        // the prisoner in the pod, the ID in the console
        r.In(() =>
        {
            Waiting.NoSleep(teleporter.open_machine());
            Waiting.NoSleep(teleporter.close_machine(prisoner));
        });
        Assert.Same(teleporter, r.In(() => prisoner.loc));
        Assert.Equal("implantchair_occupied", r.In(() => Dm.str(teleporter.icon_state)));
        // an empty hand opens the window (a held prisoner ID would go into the console's slot: tg's item_interaction)
        Take(warden); // the lobby's outputs from the join
        Click(r, warden, console);
        var payload = LastUpdate(Take(warden), out string windowId);
        Assert.Equal("GulagTeleporterConsole", InterfaceOf(payload));
        Assert.Equal(r.In(() => Dm.str(prisoner.real_name)), payload.GetProperty("data").GetProperty("prisoner").GetProperty("name").GetString());
        var prisonerId = r.In(() =>
        {
            var id = Dm.@new<obj_item_card_id_advanced_prisoner>(warden.loc);
            id.registered_name = "Pat Prisoner";
            Waiting.NoSleep(warden.put_in_active_hand(id));
            return id;
        });
        Act(r, warden, windowId, "handle_id");
        Assert.Same(prisonerId, r.In(() => console.contained_id));
        Act(r, warden, windowId, "set_goal", ("value", 300));
        Assert.Equal(300f, r.In(() => prisonerId.goal));
        var data = Data(r, warden, out _);
        Assert.Equal(1, data.GetProperty("can_teleport").GetInt32());
        Assert.Equal("Pat Prisoner", data.GetProperty("id_name").GetString());

        Act(r, warden, windowId, "teleport");
        r.Step(Second);
        r.In(() =>
        {
            Assert.Same(get_turf(beacon), prisoner.loc);
            Assert.IsType<obj_item_clothing_under_rank_prisoner>(prisoner.w_uniform);
            Assert.IsType<obj_item_clothing_shoes_sneakers_orange>(prisoner.shoes);
            Assert.Same(prisonerId, prisoner.wear_id);
            Assert.Same(reclaimer, toolbox.loc);
            Assert.True(Dm.@in(toolbox, reclaimer.stored_items[prisoner]));
            Assert.Null(console.contained_id);
            Assert.Equal(1f, teleporter.state_open);
        });
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("emitter.ogg", StringComparison.Ordinal));
        Assert.True(feed.Any("The teleportation makes you a little dizzy."), Diag(feed, r));

        // the reclaimer: not before the goal; then the prisoner's own things back
        r.In(() => Waiting.NoSleep(prisoner.forceMove(locate(3, 2, 1))));
        r.Step(5 * Second); // the travel paralysis
        Click(r, prisoner, reclaimer);
        var reclaim = LastUpdate(Take(prisoner), out string reclaimId);
        Assert.Equal("GulagItemReclaimer", InterfaceOf(reclaim));
        Assert.Equal(0, reclaim.GetProperty("data").GetProperty("can_reclaim").GetInt32());
        string mobRef = reclaim.GetProperty("data").GetProperty("mobs")[0].GetProperty("mob").GetString();
        r.In(() => prisonerId.points = 300);
        Act(r, prisoner, reclaimId, "release_items", ("mobref", mobRef));
        Assert.Same(r.In(() => get_turf(reclaimer)), r.In(() => toolbox.loc));
        Assert.False(r.In(() => Dm.@in(prisoner, reclaimer.stored_items)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_management_console_logs_in_shows_the_id_and_implants_and_injects()
    {
        obj_machinery_computer_prisoner_management console = null;
        var r = new AudioRoom(13, 9, seed: 9972, mapExtras: w =>
        {
            console = Dm.@new<obj_machinery_computer_prisoner_management>(locate(5, 4, 1));
        });
        Feed feed = Listen(r);
        var warden = r.Join(5, 5, "Warden");
        r.In(() => ADD_TRAIT(warden, TRAIT_ADVANCEDTOOLUSER, "test"));
        Card(r, warden, typeof(datum_id_trim_job_warden));
        var prisoner = r.Dummy(9, 6);
        var (chem, tracker) = r.In(() =>
        {
            var c = Dm.@new<obj_item_implant_chem>();
            Waiting.NoSleep(((datum_reagents)c.reagents).add_reagent(typeof(datum_reagent_medicine_morphine), 10));
            Waiting.NoSleep(c.implant(prisoner, warden));
            var t = Dm.@new<obj_item_implant_tracking>();
            Waiting.NoSleep(t.implant(prisoner, warden));
            return (c, t);
        });
        r.Step(Second);

        Click(r, warden, console);
        var payload = LastUpdate(Take(warden), out string windowId);
        Assert.Equal("PrisonerManagement", InterfaceOf(payload));
        Assert.Equal(0, payload.GetProperty("data").GetProperty("authorized").GetInt32());

        Act(r, warden, windowId, "login");
        Assert.Equal(1f, r.In(() => console.authenticated));
        var data = Data(r, warden, out _);
        Assert.Equal(1, data.GetProperty("authorized").GetInt32());
        var implants = data.GetProperty("implants").EnumerateArray().ToList();
        Assert.Equal(2, implants.Count);
        var chemData = implants.First(i => i.GetProperty("ref").GetString() == r.In(() => Dm.str(REF(chem))));
        Assert.Equal("10u", chemData.GetProperty("info").GetProperty("Volume").GetString());
        var buttons = chemData.GetProperty("buttons").EnumerateArray().Select(b => b.GetProperty("action_key").GetString()).ToList();
        Assert.Contains("self_destruct", buttons);
        Assert.Contains("inject", buttons);
        var trackerData = implants.First(i => i.GetProperty("ref").GetString() == r.In(() => Dm.str(REF(tracker))));
        Assert.Contains(trackerData.GetProperty("buttons").EnumerateArray(), b => b.GetProperty("action_key").GetString() == "warn");

        // the inject button: 5 units of morphine into the prisoner
        Act(r, warden, windowId, "handle_implant", ("implant_ref", r.In(() => Dm.str(REF(chem)))), ("implant_action", "inject"), ("amount", 5));
        r.In(() =>
        {
            Assert.Equal(5f, ((datum_reagents)chem.reagents).total_volume, 2);
            Assert.True(Dm.truthy(((datum_reagents)prisoner.reagents).has_reagent(typeof(datum_reagent_medicine_morphine))));
        });

        // a prisoner ID: inserted from the hand, its points reset
        var prisonerId = r.In(() =>
        {
            var id = Dm.@new<obj_item_card_id_advanced_prisoner>(warden.loc);
            id.points = 40;
            id.goal = 100;
            Waiting.NoSleep(warden.put_in_active_hand(id));
            return id;
        });
        Act(r, warden, windowId, "insert_id");
        Assert.Same(prisonerId, r.In(() => console.contained_id));
        var withId = Data(r, warden, out _);
        Assert.Equal(40, withId.GetProperty("inserted_id").GetProperty("points").GetInt32());
        Act(r, warden, windowId, "reset_id");
        Assert.Equal(0f, r.In(() => prisonerId.points));
        r.In(() => Waiting.NoSleep(console.click_alt(warden)));
        Assert.Null(r.In(() => console.contained_id));

        Act(r, warden, windowId, "logout");
        Assert.Equal(0f, r.In(() => console.authenticated));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_plate_press_and_the_produceporter_pay_a_prisoner()
    {
        var r = new AudioRoom(13, 9, seed: 9973);
        Feed feed = Listen(r);
        var press = Place<obj_machinery_plate_press>(r, 5, 4);
        var porter = Place<obj_machinery_produceporter>(r, 7, 4);
        var pad = Place<obj_item_producepad>(r, 11, 7);
        var prisoner = r.Join(6, 5, "Prisoner");
        var id = r.In(() =>
        {
            Jumpsuit(prisoner);
            var card = Dm.@new<obj_item_card_id_advanced_prisoner>(prisoner.loc);
            card.goal = 100;
            Waiting.NoSleep(prisoner.equip_to_slot_or_del(card, ITEM_SLOT_ID));
            Assert.Same(card, prisoner.wear_id);
            return card;
        });
        r.Step(Second);
        Assert.Equal("produceporter", r.In(() => Dm.str(porter.icon_state)));

        // a plate loaded: ten points; pressed: a filled plate
        var plates = r.In(() =>
        {
            var stack = Dm.@new<obj_item_stack_license_plates_empty>(prisoner.loc, 5);
            Waiting.NoSleep(prisoner.put_in_active_hand(stack));
            return stack;
        });
        r.In(() => Waiting.NoSleep(press.item_interaction(prisoner, plates)));
        r.Step(2);
        Assert.Equal((float)PRISON_LABOR_PLATE, r.In(() => id.points));
        Assert.Equal(4f, r.In(() => plates.amount));
        Assert.Equal("online_loaded", r.In(() => Dm.str(press.icon_state)));
        r.Command(() => Waiting.Detach(press.attack_hand(prisoner)));
        r.Step(2);
        Assert.Equal("loop", r.In(() => Dm.str(press.icon_state)));
        r.Step(5 * Second);
        Assert.Equal("online", r.In(() => Dm.str(press.icon_state)));
        Assert.NotNull(r.In(() => Dm.locate<obj_item_stack_license_plates_filled>(locate(5, 4, 1))));
        Assert.True(feed.Any("You finish pressing a new license plate!"), Diag(feed, r));

        // two tomatoes in the porter, the lever: to the pad, four points
        r.In(() =>
        {
            Waiting.NoSleep(prisoner.dropItemToGround(plates));
            for (int i = 0; i < 2; i++)
            {
                var tomato = Dm.@new<obj_item_food_grown_tomato>(prisoner.loc);
                Waiting.NoSleep(prisoner.put_in_active_hand(tomato));
                Waiting.NoSleep(porter.item_interaction(prisoner, tomato));
            }
        });
        Assert.Equal(2f, r.In(() => porter.current_produce));
        float before = r.In(() => id.points);
        r.In(() => Waiting.NoSleep(porter.attack_hand_secondary(prisoner)));
        r.Step(2);
        Assert.Equal(0f, r.In(() => porter.current_produce));
        Assert.Equal(2, r.In(() => Dm.each<obj_item_food_grown_tomato>(locate(11, 7, 1)).Count()));
        Assert.Equal(before + 2 * PRISON_LABOR_CROPS, r.In(() => id.points));
        Assert.True(feed.Any("labor points added!"), Diag(feed, r));
        r.AssertNoRuntimes();
    }
}
