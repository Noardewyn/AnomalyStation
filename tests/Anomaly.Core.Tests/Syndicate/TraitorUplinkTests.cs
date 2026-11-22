using System.Text.Json;
using System.Text.RegularExpressions;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Syndicate;

/// <summary>
/// Syndicate package A2 (D316): the traitor and the uplink (datum_traitor.dm, uplink_handler.dm, components/uplink.dm, modules/uplink,
/// telecrystal.dm, codeword_hearing.dm) in a booted toy room. Bob, holding an assistant's PDA, becomes a traitor: tg's objective count
/// from the config plus the ending one, an employer with its flavor, the code words heard highlighted, the AntagInfoTraitor window and a
/// PDA uplink whose ringtone code unlocks the Uplink window; C-4 bought there costs its TC and lands in his hand; an entry whose item is
/// inert is not listed and cannot be bought; a sale's discount and a refund work as tg's; telecrystals load; the radio and pen uplinks
/// unlock by their codes; the round-end report lists the purchase.
/// </summary>
public class TraitorUplinkTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob, mob_living_carbon_human Alice, obj_item_modular_computer_pda Pda) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        mob_living_carbon_human alice = r.Join(5, 4, "Alice");
        r.Step(Second);
        obj_item_modular_computer_pda pda = r.In(() =>
        {
            var made = Dm.@new<obj_item_modular_computer_pda_crew_assistant>(locate(4, 4, 1));
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(made))));
            return made;
        });
        r.Step(2);
        return (r, feed, bob, alice, pda);
    }

    private static datum_antagonist_traitor MakeTraitor(AudioRoom r, mob who)
    {
        datum_antagonist_traitor traitor = r.In(() => Waiting.NoSleep(who.mind.add_antag_datum(typeof(datum_antagonist_traitor))) as datum_antagonist_traitor);
        r.Step(4);
        Assert.NotNull(traitor);
        return traitor;
    }

    // the newest update of a window of that interface: its id and payload
    // The last update of the window; with static data: the last full update (tgui's send_full_update; SStgui's process updates after it carry none)
    private static JsonElement Window(List<TguiOutput> outputs, string @interface, out string windowId, bool withStaticData = false)
    {
        windowId = null;
        JsonElement found = default;
        foreach (TguiOutput o in outputs.Where(o => o.Kind == TguiOutputKind.Message))
        {
            using JsonDocument doc = JsonDocument.Parse(o.Json);
            if (doc.RootElement.GetProperty("type").GetString() != "update" || InterfaceOf(doc.RootElement.GetProperty("payload")) != @interface)
                continue;
            if (withStaticData && !doc.RootElement.GetProperty("payload").TryGetProperty("static_data", out _))
                continue;
            found = doc.RootElement.GetProperty("payload").Clone();
            windowId = o.WindowId;
        }
        Assert.True(found.ValueKind == JsonValueKind.Object, "no " + @interface + " update");
        return found;
    }

    [Fact]
    public void TheTypesAreLiveAndTheUplinkKeepsEveryEntry()
    {
        foreach (Type type in new[]
        {
            typeof(datum_antagonist_traitor), typeof(datum_component_uplink), typeof(datum_uplink_handler), typeof(datum_uplink_item),
            typeof(datum_uplink_item_explosives_c4), typeof(datum_component_codeword_hearing), typeof(datum_uplink_purchase_log),
            typeof(datum_element_uplink_reimburse), typeof(obj_item_stack_telecrystal), typeof(obj_item_uplink), typeof(obj_item_pen_uplink),
            typeof(datum_preference_choiced_uplink_location),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");

        var r = new AudioRoom(6, 6, seed: 7201);
        r.In(() =>
        {
            // SStraitor keeps every tg entry (the asset's generate); the window's list hides the entries that are not live (DV343)
            Assert.Equal(subtypesof(typeof(datum_uplink_item)).len, SStraitor.uplink_items.len);
            var listed = Dm.each<DmList>(uplink_json["items"] as DmList).Select(i => i["id"] as Type).ToList();
            Assert.Contains(typeof(datum_uplink_item_explosives_c4), listed);
            Assert.Contains(typeof(datum_uplink_item_suits_modsuit), listed); // the MODsuit with its antag modules (gaps MO2)
            Assert.Contains(typeof(datum_uplink_item_implants_uplink), listed); // its implanter is live (G3)
            Assert.DoesNotContain(typeof(datum_uplink_item_bundles_tc_bundle_a), listed); // its kits hold later packages' items (UplinkWaits)
            Assert.DoesNotContain(typeof(datum_uplink_item_stealthy_tools_blackout), listed); // its own spawn_item (the round events) is inert
            Assert.Equal("Weapon Kits (Recommended)", (uplink_json["categories"] as DmList)[1]); // tg's category order, the heaviest first (weight 100)
            // the code words: two to five each, highlighted by their regexes
            Assert.InRange(((DmList)SStraitor.syndicate_code_phrase).len, 2, 5);
            Assert.InRange(((DmList)SStraitor.syndicate_code_response).len, 2, 5);
            Assert.NotNull(SStraitor.syndicate_code_phrase_regex);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ATraitorGetsObjectivesAnEmployerCodeWordsAndAPdaUplinkThatSellsCFourIntoHisHand()
    {
        var (r, feed, bob, alice, pda) = Room(7202);
        datum_antagonist_traitor traitor = MakeTraitor(r, bob);

        // the objectives: tg's count from the config (traitor_objectives_amount, 2) and the ending one, escape or martyrdom
        r.In(() =>
        {
            Assert.Equal(Dm.num(config.Get(typeof(datum_config_entry_number_traitor_objectives_amount))) + 1, traitor.objectives.len);
            Assert.True(traitor.ending_objective is datum_objective_escape or datum_objective_martyr);
            Assert.Same(traitor.ending_objective, traitor.objectives[traitor.objectives.len]);
            Assert.True(Dm.@in(traitor.employer, GLOB.syndicate_employers) || Dm.@in(traitor.employer, GLOB.nanotrasen_employers));
            Assert.NotNull(traitor.traitor_flavor?["introduction"]);
        });

        // the PDA's hidden uplink: tg's ringtone code ("123 Alpha"), the handler's sales, the code in the antagonist's memory
        datum_component_uplink uplink = r.In(() => pda.GetComponent(typeof(datum_component_uplink)) as datum_component_uplink);
        Assert.NotNull(uplink);
        string code = r.In(() => Dm.str(uplink.unlock_code));
        Assert.Matches(new Regex(@"^\d{3} [\w-]+$"), code);
        r.In(() =>
        {
            Assert.True(Dm.truthy(uplink.locked));
            Assert.Equal(TELECRYSTALS_DEFAULT, uplink.uplink_handler.telecrystals);
            Assert.Same(traitor.uplink_handler, uplink.uplink_handler);
            Assert.Same(bob.mind, uplink.uplink_handler.owner);
            Assert.True(uplink.uplink_handler.extra_purchasable.len >= 1, "no sales");
            Assert.Contains("<B>Uplink Passcode:</B> " + code, traitor.antag_memory);
            Assert.Same(uplink, traitor.uplink_ref.resolve());
        });

        // the AntagInfoTraitor window: the code words, the uplink's code and how to unlock it
        JsonElement info = Window(Take(bob), "AntagInfoTraitor", out _, withStaticData: true).GetProperty("static_data");
        Assert.Equal(code, info.GetProperty("code").GetString());
        Assert.Contains("ring tone", info.GetProperty("uplink_unlock_info").GetString());
        string phrases = info.GetProperty("phrases").GetString();
        Assert.Equal(r.In(() => jointext(SStraitor.syndicate_code_phrase, ", ")), phrases);

        // the code words: Alice says the first phrase, Bob hears it in tg's blue span, Alice hears it plain
        string phrase = r.In(() => Dm.str(((DmList)SStraitor.syndicate_code_phrase)[1]));
        int before = feed.Chat.Count;
        r.In(() => Waiting.Detach(alice.say("so, " + phrase)));
        r.Step(4);
        Assert.Contains(feed.Chat.Skip(before), c => c.Target == bob && c.Html.Contains("<span class='blue'>", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(feed.Chat.Skip(before), c => c.Target == alice && c.Html.Contains("<span class='blue'>", StringComparison.Ordinal));

        // a wrong ringtone changes the ringtone; the code unlocks the uplink and opens the Uplink window
        datum_computer_file_program_messenger messenger = r.In(() => Dm.each<datum_computer_file_program_messenger>(pda.stored_files).Single());
        Assert.True(r.In(() => Dm.truthy(messenger.set_ringtone("beep", bob))));
        Assert.True(r.In(() => Dm.truthy(uplink.locked)));
        Assert.False(r.In(() => Dm.truthy(messenger.set_ringtone(code.ToLowerInvariant(), bob)))); // COMPONENT_STOP_RINGTONE_CHANGE
        r.Step(4);
        Assert.False(r.In(() => Dm.truthy(uplink.locked)));
        Assert.True(feed.Any("The computer softly beeps."));
        JsonElement window = Window(Take(bob), "Uplink", out string windowId, withStaticData: true);
        Assert.Equal(20, window.GetProperty("data").GetProperty("telecrystals").GetDouble());
        // the C-4 entry with its picture (the item's icon file and state, which the client draws: DmIcon)
        JsonElement c4Entry = window.GetProperty("static_data").GetProperty("uplink_json").GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("id").GetString() == "/datum/uplink_item/explosives/c4");
        Assert.Equal(("icons/obj/weapons/grenade.dmi", "plastic-explosive0", 1.0), (c4Entry.GetProperty("icon").GetString(), c4Entry.GetProperty("icon_state").GetString(), c4Entry.GetProperty("cost").GetDouble()));
        Assert.Equal(0, window.GetProperty("data").GetProperty("current_stock").GetProperty("uplink_shared_stock_kits").GetDouble() - 1);
        Assert.Equal(3, window.GetProperty("data").GetProperty("primary_objectives").GetArrayLength());
        Assert.StartsWith("DIRECTIVE ALPHA", window.GetProperty("data").GetProperty("primary_objectives")[0].GetProperty("task_name").GetString(), StringComparison.Ordinal);

        // buying C-4: 1 TC, the kaching, the C-4 in his free hand, the purchase in the log
        Act(r, bob, windowId, "buy", ("path", "/datum/uplink_item/explosives/c4"));
        JsonElement after = Data(r, bob, out _);
        Assert.Equal(19, after.GetProperty("telecrystals").GetDouble());
        Assert.Equal(1, after.GetProperty("purchased_items").GetDouble());
        Assert.Contains(r.In(() => Dm.each<object>(bob.held_items).ToList()), o => o is obj_item_grenade_c4);
        Assert.True(feed.Any("materializes into your hands!"));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/kaching.ogg");

        // a hidden entry (DV343: the blackout's own spawn_item, the round events, is inert; the MODsuit, hidden here until gaps MO2,
        // is sold now) is not sold, even by its path
        Act(r, bob, windowId, "buy", ("path", "/datum/uplink_item/stealthy_tools/blackout"));
        Assert.Equal(19, r.In(() => uplink.uplink_handler.telecrystals));

        // the round-end report: the TC spent and the purchase; the footer names the code words (the toy room has no shuttle: Bob escapes
        // by tg's forced escape)
        r.In(() => bob.mind.force_escaped = TRUE);
        string report = r.In(() => Dm.str(traitor.roundend_report()));
        Assert.Contains("(used 1 TC)", report);
        Assert.Contains("Composition C-4", report);
        Assert.Contains(Dm.str(r.In(() => traitor.traitor_flavor["roundend_report"])), report);
        Assert.Contains("The code phrases were:", r.In(() => Dm.str(traitor.roundend_report_footer())));

        // locking closes the uplink; the PDA opens as a PDA again until the code is entered
        Act(r, bob, windowId, "lock");
        Assert.True(r.In(() => Dm.truthy(uplink.locked)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASaleIsDiscountedATelecrystalLoadsAndARefundReturnsItsTc()
    {
        var (r, feed, bob, _, pda) = Room(7203);
        MakeTraitor(r, bob);
        datum_component_uplink uplink = r.In(() => pda.GetComponent(typeof(datum_component_uplink)) as datum_component_uplink);
        r.In(() => Assert.Equal(COMPONENT_STOP_RINGTONE_CHANGE, Dm.num(uplink.new_ringtone(pda, bob, uplink.unlock_code)))); // the window opens
        r.Step(4);
        Window(Take(bob), "Uplink", out string windowId);

        // a sale of the revolver (13 TC): tg's 25, 50 or 75 % off, its own stock key, the price it was in the name and desc
        datum_uplink_item sale = r.In(() =>
        {
            DmList sales = create_uplink_sales(1, typeof(datum_uplink_category_discounts), -1, Dm.list(SStraitor.uplink_items_by_type[typeof(datum_uplink_item_dangerous_revolver)]));
            var made = (datum_uplink_item)sales[1];
            Dm.append(uplink.uplink_handler.extra_purchasable, made);
            return made;
        });
        float cost = r.In(() => sale.cost);
        Assert.Contains(cost, new[] { 9f, 6f, 3f });
        r.In(() =>
        {
            Assert.Contains(Dm.text("([]% off!)", round((13 - cost) / 13 * 100)), sale.name);
            Assert.Contains("Normally costs 13 TC. All sales final.", sale.desc);
            Assert.Same(typeof(datum_uplink_category_discounts), sale.category);
            Assert.IsType<datum_weakref>(sale.stock_key);
        });
        Act(r, bob, windowId, "buy", ("ref", r.In(() => Dm.str(REF(sale)))));
        Assert.Equal(20 - cost, r.In(() => uplink.uplink_handler.telecrystals));
        Assert.Contains(r.In(() => Dm.each<object>(bob.held_items).ToList()), o => o is obj_item_gun_ballistic_revolver);

        // five telecrystals slotted into the unlocked PDA
        obj_item_stack_telecrystal crystals = r.In(() => Dm.@new<obj_item_stack_telecrystal>(locate(4, 4, 1), 5));
        r.Step(2);
        r.In(() => Waiting.NoSleep(pda.attackby(crystals, bob)));
        r.Step(2);
        Assert.Equal(25 - cost, r.In(() => uplink.uplink_handler.telecrystals));
        Assert.True(r.In(() => QDELETED(crystals)));
        Assert.True(feed.Any("and charge its internal uplink."));

        // a refundable purchase (tg's uplink_reimburse, as spawn_item adds it) tapped on the uplink returns its TC and vanishes
        obj_item_grenade_c4 c4 = r.In(() => Dm.@new<obj_item_grenade_c4>(locate(4, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(c4.AddElement(typeof(datum_element_uplink_reimburse), 3)));
        r.In(() => Waiting.NoSleep(pda.attackby(c4, bob)));
        r.Step(2);
        Assert.Equal(28 - cost, r.In(() => uplink.uplink_handler.telecrystals));
        Assert.True(r.In(() => QDELETED(c4)));
        Assert.True(feed.Any("disappears in a puff of red smoke!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRadioAndPenUplinksUnlockByTheirCodes()
    {
        var r = new AudioRoom(8, 6, seed: 7204);
        mob_living_carbon_human bob = r.Join(3, 3, "Bob");
        r.Step(Second);

        // a radio uplink: three phonetic words said on the uplink channel (:z)
        obj_item_radio radio = r.In(() => Dm.@new<obj_item_radio>(locate(3, 3, 1)));
        r.Step(2);
        datum_component_uplink radio_uplink = r.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(radio))));
            var made = Waiting.NoSleep(radio.AddComponent(typeof(datum_component_uplink), bob.key)) as datum_component_uplink;
            made.setup_unlock_code();
            return made;
        });
        string words = r.In(() => Dm.str(radio_uplink.unlock_code));
        Assert.Matches(new Regex(@"^[\w-]+ [\w-]+ [\w-]+$"), words);
        Assert.Contains(", :z channel).", r.In(() => Dm.str(radio_uplink.unlock_note)));
        r.In(() => radio.talk_into(bob, "hello there", RADIO_CHANNEL_UPLINK));
        Assert.True(r.In(() => Dm.truthy(radio_uplink.locked)));
        object said = r.In(() => radio.talk_into(bob, "uh, " + words.ToUpperInvariant(), RADIO_CHANNEL_UPLINK));
        Assert.Equal(NONE, Dm.num(said)); // COMPONENT_CANNOT_USE_RADIO: the code is not broadcast
        Assert.False(r.In(() => Dm.truthy(radio_uplink.locked)));

        // a pen uplink: its two twists in order
        obj_item_pen pen = r.In(() => Dm.@new<obj_item_pen>(locate(3, 3, 1)));
        r.Step(2);
        datum_component_uplink pen_uplink = r.In(() =>
        {
            var made = Waiting.NoSleep(pen.AddComponent(typeof(datum_component_uplink), bob.key)) as datum_component_uplink;
            made.setup_unlock_code();
            return made;
        });
        DmList twists = r.In(() => (DmList)pen_uplink.unlock_code);
        Assert.Equal(2, twists.len);
        Assert.StartsWith("<B>Uplink Degrees:</B> ", r.In(() => Dm.str(pen_uplink.unlock_note)));
        r.In(() =>
        {
            pen.degrees = 90;
            pen._SendSignal(COMSIG_PEN_ROTATED, Dm.list(pen, twists[2], bob)); // the second twist first: still locked
            pen._SendSignal(COMSIG_PEN_ROTATED, Dm.list(pen, twists[1], bob));
            Assert.True(Dm.truthy(pen_uplink.locked));
            pen._SendSignal(COMSIG_PEN_ROTATED, Dm.list(pen, twists[2], bob));
            Assert.False(Dm.truthy(pen_uplink.locked));
            Assert.Equal(0, pen.degrees);
        });
        r.AssertNoRuntimes();
    }
}
