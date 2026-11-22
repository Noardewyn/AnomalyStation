using System.Text.Json;
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
/// Gaps package CM1 (D333): the communications console's leftovers in a booted toy room on the station's level. A captain logged in
/// at the console sees the shuttle catalogue (the purchase access, the emag-only shuttles, the departmental lock, the prerequisites)
/// and is refused what he may not buy; the messages to CentCom and the Syndicate, the nuke codes' request and the spare ID safe code's
/// drop pod; the station's status displays show the console's message and picture; the console hack and the syndicate console.
/// The purchase itself (a shuttle loaded at the emergency dock) is <see cref="CommsCM1MetaTests"/>'s.
/// </summary>
public class CommsCM1Tests
{
    private const string Interface = "CommunicationsConsole";

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob, obj_machinery_computer_communications Console) Room(ulong seed, Type trim = null)
    {
        var r = new AudioRoom(15, 11, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        r.In(() =>
        {
            GLOB.station_levels_cache.len = 1;
            GLOB.station_levels_cache[1] = TRUE;
            SSmapping.z_list = Dm.list(Dm.@new<datum_space_level>(1, "station", Dm.list(Dm.kv(ZTRAIT_STATION, TRUE))));
            var room = (area)get_area(bob);
            room.requires_power = FALSE; // the toy room has no APC
            GLOB.the_station_areas = Dm.list(room.type);
            // CentCom's holding area for pods in transit (tg's supplypod_temp_holding), as EventsE3Tests' room: its corner turf
            var lane = GLOB.areas_by_type[typeof(area_centcom_central_command_areas_supplypod_supplypod_temp_holding)] as area
                ?? Dm.@new<area_centcom_central_command_areas_supplypod_supplypod_temp_holding>();
            set_turf_to_area(locate(1, 1, 1), lane);
            // the map's config (a host-built room reads none) and an idle emergency shuttle
            SSmapping.current_map = Dm.@new<datum_map_config>();
            SSshuttle.emergency = Dm.@new<obj_docking_port_mobile_emergency>(locate(2, 9, 1));
            // the ID card in the ID slot (which hangs on a jumpsuit), with its job's trim: a gold card, as the captain's, whose
            // wildcard slots hold any trim's (a grey card refuses the heads' wildcards, cards_ids.dm can_add_wildcards)
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(bob.loc), ITEM_SLOT_ICLOTHING))));
            var card = Dm.@new<obj_item_card_id_advanced_gold>(bob.loc);
            Assert.Equal(TRUE, Waiting.NoSleep(SSid_access.apply_trim_to_card(card, trim ?? typeof(datum_id_trim_job_captain))));
            card.registered_name = "Bob";
            Waiting.NoSleep(bob.equip_to_slot_or_del(card, ITEM_SLOT_ID));
            Assert.Same(card, bob.wear_id);
        });
        r.Step(Second);
        for (int i = 0; i < 30 && r.In(() => SSeconomy.times_fired) < 1; i++)
            r.Step(Second);
        var console = Place<obj_machinery_computer_communications>(r, 6, 5);
        return (r, feed, bob, console);
    }

    /// <summary>The shuttle catalogue: tg's templates of the box, monastery (emag-only), engineering (departmental), medisim (a
    /// prerequisite) and construction (the chief engineer may buy it too) shuttles, and the purchase access they give.</summary>
    private static void Catalogue(AudioRoom r)
    {
        r.In(() =>
        {
            foreach (Type type in new[]
            {
                typeof(datum_map_template_shuttle_emergency_box), typeof(datum_map_template_shuttle_emergency_monastery),
                typeof(datum_map_template_shuttle_emergency_departmental_engineering), typeof(datum_map_template_shuttle_emergency_medisim),
                typeof(datum_map_template_shuttle_emergency_construction), typeof(datum_map_template_shuttle_emergency_backup),
            })
            {
                var template = (datum_map_template_shuttle)Waiting.NoSleep(Dm.new_async(type));
                SSmapping.shuttle_templates[template.shuttle_id] = template;
            }
            SSshuttle.has_purchase_shuttle_access = SSshuttle.init_has_purchase_shuttle_access() as DmList;
        });
    }

    private static datum_map_template_shuttle Template(AudioRoom r, string id) => r.In(() => (datum_map_template_shuttle)SSmapping.shuttle_templates[id]);

    private static float Budget(AudioRoom r) => r.In(() => SSeconomy.get_dep_account(ACCOUNT_CAR).account_balance);

    /// <summary>The window, opened by a click with an empty hand, and the login with the card in the ID slot.</summary>
    private static string LogIn(AudioRoom r, mob who, atom console, out JsonElement data)
    {
        Take(who);
        Click(r, who, console);
        JsonElement payload = LastUpdate(Take(who), out string window);
        Assert.Equal(Interface, InterfaceOf(payload));
        Assert.Equal(0, payload.GetProperty("data").GetProperty("authenticated").GetInt32());
        Act(r, who, window, "toggleAuthentication");
        data = Data(r, who, out _);
        Assert.Equal(1, data.GetProperty("authenticated").GetInt32());
        return window;
    }

    private static List<JsonElement> Shuttles(JsonElement data) => data.GetProperty("shuttles").EnumerateArray().ToList();

    private static JsonElement Shuttle(JsonElement data, string name) => Shuttles(data).Single(s => s.GetProperty("name").GetString() == name);

    [Fact]
    public void TheCaptainSeesTheShuttleCatalogueHeMayBuyFrom()
    {
        var (r, feed, bob, console) = Room(5101);
        Catalogue(r);
        Assert.Equal(new[] { "captain", "ce" }, r.In(() => Dm.each(SSshuttle.has_purchase_shuttle_access).Select(a => Dm.str(a)).OrderBy(a => a).ToArray()));
        string window = LogIn(r, bob, console, out JsonElement data);
        Assert.Equal(1, data.GetProperty("canBuyShuttles").GetInt32());

        Act(r, bob, window, "setState", ("state", "buying_shuttle"));
        data = Data(r, bob, out _);
        Assert.Equal("buying_shuttle", data.GetProperty("page").GetString());
        Assert.Equal(Budget(r), data.GetProperty("budget").GetSingle());
        // the priced templates the captain's access buys: not the backup shuttle (no price) nor the emag-only monastery
        Assert.Equal(new[] { "Box Station Emergency Shuttle", "Build your own shuttle kit", "Engineering Emergency Shuttle", "Medieval Reality Simulation Dome" },
            Shuttles(data).Select(s => s.GetProperty("name").GetString()).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        JsonElement box = Shuttle(data, "Box Station Emergency Shuttle");
        Assert.Equal(800, box.GetProperty("creditCost").GetInt32()); // CARGO_CRATE_VALUE * 4
        Assert.Equal(800, box.GetProperty("initial_cost").GetInt32());
        Assert.Equal("45", box.GetProperty("occupancy_limit").GetString());
        Assert.Equal(0, box.GetProperty("emagOnly").GetInt32());
        Assert.Equal(0, box.GetProperty("department_locked").GetInt32());
        Assert.Equal(r.In(() => REF(Template(r, "emergency_box"))), box.GetProperty("ref").GetString());
        Assert.Equal("A special holodeck simulation must be loaded before this shuttle can be purchased.", Shuttle(data, "Medieval Reality Simulation Dome").GetProperty("prerequisites").GetString());
        // the engineering shuttle is locked while its department has no crew
        JsonElement engineering = Shuttle(data, "Engineering Emergency Shuttle");
        Assert.Equal("Engineering", engineering.GetProperty("department_name").GetString());
        Assert.Equal(1, engineering.GetProperty("department_locked").GetInt32());

        // an emagged console sells the Syndicate's shuttles too
        r.In(() => Waiting.NoSleep(console.emag_act(bob, Dm.@new<obj_item_card_emag>(bob.loc))));
        Assert.True(feed.Any("balloon: routing circuits scrambled"));
        data = Data(r, bob, out _, steps: 2 * Second);
        JsonElement monastery = Shuttle(data, "Grand Corporate Monastery");
        Assert.Equal(1, monastery.GetProperty("emagOnly").GetInt32());
        Assert.Equal(Template(r, "emergency_monastery").credit_cost, monastery.GetProperty("creditCost").GetSingle());
        Assert.Equal(1, data.GetProperty("emagged").GetInt32());

        // the engineering crew outnumbering the other departments unlocks its shuttle
        r.In(() =>
        {
            if (!Dm.truthy(length(SSjob.joinable_departments_by_type)))
                SSjob.setup_occupations();
            ((datum_job)SSjob.get_job_type(typeof(datum_job_station_engineer))).current_positions = 3;
        });
        data = Data(r, bob, out _, steps: 2 * Second);
        Assert.Equal(0, Shuttle(data, "Engineering Emergency Shuttle").GetProperty("department_locked").GetInt32());
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APurchaseIsRefusedWithoutTheAccessThePrerequisiteTheCrewOrTheMoney()
    {
        var (r, feed, bob, console) = Room(5102);
        Catalogue(r);
        string window = LogIn(r, bob, console, out _);
        Act(r, bob, window, "setState", ("state", "buying_shuttle"));
        Data(r, bob, out _);

        // the medisim's holodeck program was never run
        Act(r, bob, window, "purchaseShuttle", ("shuttle", r.In(() => REF(Template(r, "emergency_medisim")))));
        Assert.True(feed.Any("You have not met the requirements for purchasing this shuttle."));
        // no engineer aboard
        Act(r, bob, window, "purchaseShuttle", ("shuttle", r.In(() => REF(Template(r, "emergency_dep_eng")))));
        Assert.True(feed.Any("This shuttle can be buyed only if this department has most employees."));
        // the monastery is the Syndicate's: an honest console does not sell it
        Act(r, bob, window, "purchaseShuttle", ("shuttle", r.In(() => REF(Template(r, "emergency_monastery")))));
        Assert.Equal((float)SHUTTLEPURCHASE_PURCHASABLE, r.In(() => SSshuttle.shuttle_purchased));
        // an empty cargo budget buys nothing
        r.In(() =>
        {
            var budget = SSeconomy.get_dep_account(ACCOUNT_CAR);
            budget.adjust_money(-budget.account_balance);
        });
        Act(r, bob, window, "purchaseShuttle", ("shuttle", r.In(() => REF(Template(r, "emergency_box")))));
        Assert.Equal((float)SHUTTLEPURCHASE_PURCHASABLE, r.In(() => SSshuttle.shuttle_purchased));
        Assert.Equal(0f, Budget(r));
        JsonElement data = Data(r, bob, out _);
        Assert.Equal("buying_shuttle", data.GetProperty("page").GetString());

        // the reasons the console gives instead of the catalogue
        Act(r, bob, window, "setState", ("state", "main"));
        r.In(() => SSshuttle.emergency.mode = SHUTTLE_CALL);
        data = Data(r, bob, out _, steps: 2 * Second);
        Assert.Equal("The shuttle is already in transit.", data.GetProperty("canBuyShuttles").GetString());
        Act(r, bob, window, "purchaseShuttle", ("shuttle", r.In(() => REF(Template(r, "emergency_box")))));
        Assert.True(feed.Any("The shuttle is already in transit."));
        r.In(() =>
        {
            SSshuttle.emergency.mode = SHUTTLE_IDLE;
            SSshuttle.shuttle_purchased = SHUTTLEPURCHASE_PURCHASED;
        });
        data = Data(r, bob, out _, steps: 2 * Second);
        Assert.Equal("A replacement shuttle has already been purchased.", data.GetProperty("canBuyShuttles").GetString());
        r.In(() => SSshuttle.shuttle_purchased = SHUTTLEPURCHASE_FORCED);
        data = Data(r, bob, out _, steps: 2 * Second);
        Assert.Equal("Due to unforseen circumstances, shuttle purchasing is no longer available.", data.GetProperty("canBuyShuttles").GetString());
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnAssistantCannotBuyAndTheSyndicateConsoleSellsNothing()
    {
        var (r, _, bob, _) = Room(5103, typeof(datum_id_trim_job_assistant));
        Catalogue(r);
        var console = Place<obj_machinery_computer_communications>(r, 4, 5);
        Take(bob);
        Click(r, bob, console);
        LastUpdate(Take(bob), out string window);
        Act(r, bob, window, "toggleAuthentication");
        JsonElement data = Data(r, bob, out _);
        Assert.Equal(0, data.GetProperty("authenticated").GetInt32()); // an assistant's card has no command access
        r.In(() =>
        {
            // logged in by hand with the assistant's access: no purchase access, no shuttle
            console.authenticated = TRUE;
            console.authorize_access = ((obj_item_card_id)bob.wear_id).access.Copy();
            Assert.Equal(FALSE, console.can_buy_shuttles(bob));
            Assert.Equal(FALSE, console.can_purchase_this_shuttle(Template(r, "emergency_box")));
        });

        var syndicate = Place<obj_machinery_computer_communications_syndicate>(r, 7, 7);
        r.In(() =>
        {
            Assert.False(InertSeam.IsInert(syndicate), "the syndicate console is live");
            syndicate.authenticated = TRUE;
            syndicate.authorize_access = SSid_access.get_region_access_list(Dm.list(REGION_ALL_STATION)) as DmList;
            Assert.Equal(FALSE, syndicate.can_buy_shuttles(bob));
            Assert.Equal(FALSE, syndicate.authenticated_as_silicon_or_captain(bob));
            Assert.Equal(FALSE, Waiting.NoSleep(syndicate.emag_act(bob, Dm.@new<obj_item_card_emag>(bob.loc))));
            Assert.False(Dm.truthy(Dm.bitand(syndicate.obj_flags, EMAGGED)), "the syndicate console cannot be emagged");
            Assert.Equal(TRUE, syndicate.has_communication());
            // its announcements reach the players on its own level (and the dead): Bob, on the same level
            Assert.Contains(bob, Dm.each<mob>(syndicate.get_communication_players()).ToList());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void MessagesToCentComAndTheSyndicateAreTransmittedOnceAMinute()
    {
        var (r, feed, bob, console) = Room(5104);
        string window = LogIn(r, bob, console, out JsonElement data);
        Assert.Equal(1, data.GetProperty("canMessageAssociates").GetInt32());
        Assert.Equal(1, data.GetProperty("importantActionReady").GetInt32());
        r.Sounds.Heard.Clear();

        Act(r, bob, window, "messageAssociates", ("message", "Send more coffee"));
        Assert.True(feed.Any("Message transmitted to Central Command."));
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("terminal_prompt_confirm.ogg", StringComparison.Ordinal));
        data = Data(r, bob, out _);
        Assert.Equal(0, data.GetProperty("importantActionReady").GetInt32());
        // a second message waits out the minute
        int lines = feed.Chat.Count(c => (c.Html ?? "").Contains("Message transmitted", StringComparison.Ordinal));
        Act(r, bob, window, "messageAssociates", ("message", "And biscuits"));
        Assert.Equal(lines, feed.Chat.Count(c => (c.Html ?? "").Contains("Message transmitted", StringComparison.Ordinal)));
        r.Step(61 * Second);
        data = Data(r, bob, out _, steps: 2 * Second);
        Assert.Equal(1, data.GetProperty("importantActionReady").GetInt32());

        // emagged, the routing goes to the Syndicate
        r.In(() => Waiting.NoSleep(console.emag_act(bob, Dm.@new<obj_item_card_emag>(bob.loc))));
        Act(r, bob, window, "messageAssociates", ("message", "Hello friends"));
        Assert.True(feed.Any("SYSERR @l(19833)of(transmit.dm): !@$ MESSAGE TRANSMITTED TO SYNDICATE COMMAND."));
        // and the backup routing data restores it
        r.In(() => console.override_cooldown());
        Act(r, bob, window, "restoreBackupRoutingData");
        Assert.True(feed.Any("Backup routing data restored."));
        Assert.False(r.In(() => Dm.truthy(Dm.bitand(console.obj_flags, EMAGGED))), "restored");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheNukeCodesRequestIsAnnouncedToTheCrew()
    {
        var (r, feed, bob, console) = Room(5105);
        string window = LogIn(r, bob, console, out JsonElement data);
        Assert.Equal(1, data.GetProperty("canRequestNuke").GetInt32());
        r.Sounds.Heard.Clear();
        Act(r, bob, window, "requestNukeCodes", ("reason", "The disk is lost"));
        Assert.True(feed.Any("Request sent."));
        string name = r.In(() => Dm.str(bob.name));
        Assert.True(feed.Any("The codes for the on-station nuclear self-destruct have been requested by " + name + ". Confirmation or denial of this request will be sent shortly."), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.True(feed.Any("Nuclear Self-Destruct Codes Requested"));
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("terminal_prompt.ogg", StringComparison.Ordinal));
        data = Data(r, bob, out _);
        Assert.Equal(0, data.GetProperty("importantActionReady").GetInt32());
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSpareIdSafeCodeComesByDropPodWhenNoCaptainIsAboard()
    {
        var (r, feed, bob, console) = Room(5106);
        Take(bob);
        Click(r, bob, console);
        JsonElement payload = LastUpdate(Take(bob), out string window);
        JsonElement data = payload.GetProperty("data");
        Assert.Equal(1, data.GetProperty("canRequestSafeCode").GetInt32()); // offered before the login
        r.In(() => SSjob.assigned_captain = TRUE);
        Act(r, bob, window, "requestSafeCodes");
        Assert.True(feed.Any("There is already an assigned Captain or Acting Captain on deck!"));
        r.In(() => SSjob.assigned_captain = FALSE);

        Act(r, bob, window, "requestSafeCodes");
        string area = r.In(() => ((area)get_area(console)).name);
        // (the announcement encodes its apostrophe: the Captain's)
        Assert.True(feed.Any("Due to staff shortages, your station has been approved for delivery of access codes to secure the Captain"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.True(feed.Any("Spare ID. Delivery via drop pod at " + area + ". ETA 120 seconds."));
        data = Data(r, bob, out _);
        Assert.Equal(0, data.GetProperty("canRequestSafeCode").GetInt32());
        Assert.InRange(data.GetProperty("safeCodeDeliveryWait").GetSingle(), 1100, 1200);
        Assert.Equal(area, data.GetProperty("safeCodeDeliveryArea").GetString());
        Act(r, bob, window, "requestSafeCodes");
        Assert.True(feed.Any("The safe code has already been requested and is being delivered to your station!"));

        // two minutes on, the CentCom pod lands on the console's tile with the code's biscuit card
        obj_item_folder_biscuit_confidential_emergency_spare_id_safe_code card = null;
        for (int i = 0; i < 160 && card == null; i++)
        {
            r.Step(Second);
            card = r.In(() => Dm.each<obj_item_folder_biscuit_confidential_emergency_spare_id_safe_code>(Dm.world).FirstOrDefault(c => c.loc is turf));
        }
        Assert.NotNull(card);
        Assert.Same(r.In(() => get_turf(console)), r.In(() => get_turf(card)));
        Assert.Null(r.In(() => SSjob.safe_code_timer_id));
        Assert.Null(r.In(() => SSjob.safe_code_request_loc));
        Act(r, bob, window, "requestSafeCodes");
        Assert.True(feed.Any("The safe code has already been requested and delivered to your station!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheStatusDisplaysShowTheConsolesMessageAndPicture()
    {
        var (r, _, bob, console) = Room(5107);
        var display = Place<obj_machinery_status_display_evac>(r, 8, 8);
        string window = LogIn(r, bob, console, out _);
        Act(r, bob, window, "setState", ("state", "changing_status"));
        JsonElement data = Data(r, bob, out _);
        Assert.Equal("changing_status", data.GetProperty("page").GetString());

        Act(r, bob, window, "setStatusMessage", ("upperText", "Evac at"), ("lowerText", "noon"));
        data = Data(r, bob, out _);
        Assert.Equal("Evac at", data.GetProperty("upperText").GetString());
        Assert.Equal("noon", data.GetProperty("lowerText").GetString());
        Assert.Equal(("EVAC AT", "NOON"), r.In(() => (display.message1, display.message2)));
        Assert.Equal(SD_MESSAGE, r.In(() => display.current_mode));

        Act(r, bob, window, "setStatusPicture", ("picture", "redalert"));
        r.Step(Second);
        Assert.Equal(SD_PICTURE, r.In(() => display.current_mode));
        Assert.Equal("redalert", r.In(() => display.current_picture));
        // the current alert's picture on green alert
        Act(r, bob, window, "setStatusPicture", ("picture", "currentalert"));
        r.Step(Second);
        Assert.Equal("greenalert", r.In(() => display.current_picture));
        // a picture outside tg's list is refused
        Act(r, bob, window, "setStatusPicture", ("picture", "ai_bsod"));
        r.Step(Second);
        Assert.Equal("greenalert", r.In(() => display.current_picture));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHackedConsoleBroadcastsTheSleeperSignal()
    {
        var (r, feed, bob, console) = Room(5108);
        r.In(() => ((area)get_area(console)).area_flags = Dm.bitor(((area)get_area(console)).area_flags, VALID_TERRITORY)); // a station area's
        Assert.Equal(TRUE, r.In(() => console.can_hack(bob)));
        r.In(() => Waiting.NoSleep(console.set_machine_stat(Dm.bitor(console.machine_stat, BROKEN))));
        Assert.Equal(FALSE, r.In(() => console.can_hack(bob, feedback: TRUE)));
        Assert.True(feed.Any("balloon: can't hack!"));
        r.In(() => Waiting.NoSleep(console.set_machine_stat(Dm.bitand(console.machine_stat, Dm.bitnot(BROKEN)))));

        // no ghosts: the only result is the sleeper agents' signal
        r.In(() => Waiting.Detach(console.hack_console(bob)));
        r.Step(2 * Second);
        Assert.True(feed.Any("Attention crew, it appears that someone on your station has hijacked your telecommunications and broadcasted an unknown signal."), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.True(feed.Any("High-Priority Update"));
        bool sleeper = r.In(() => Dm.truthy(bob.mind?.has_antag_datum(typeof(datum_antagonist_traitor))))
            || r.In(() => Dm.each<datum_dynamic_ruleset_latejoin_traitor>(SSdynamic.queued_rulesets).Any());
        Assert.True(sleeper, "a sleeper agent was made or a latejoin traitor queued");
        r.AssertNoRuntimes();
    }
}
