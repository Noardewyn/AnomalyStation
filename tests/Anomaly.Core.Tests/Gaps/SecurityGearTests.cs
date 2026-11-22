using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package J4 (D298), the job gear of security and the detective in a booted toy room: the forensic scanner reads prints, fibres, blood and a person's hand; an evidence bag holds an item and shows
/// it; the prisoner ID counts points and its timer starts at the gate; two keycard devices raise a red alert and open the maintenance doors, and the head's card sends the janitor's key a department;
/// the hailer speaks; a riot shield blocks; the seclite mount takes a light; every type spawns live and is destroyed with no runtimes.
/// </summary>
public class SecurityGearTests
{
    private static readonly Type[] AllTypes =
    {
        typeof(obj_item_detective_scanner), typeof(obj_item_evidencebag), typeof(obj_item_forensics_spoofer), typeof(obj_item_wallframe_detectiveboard),
        typeof(obj_item_card_id_advanced_plainclothes), typeof(obj_item_card_id_advanced_prisoner), typeof(obj_item_card_id_advanced_prisoner_one), typeof(obj_item_card_id_advanced_prisoner_seven),
        typeof(obj_item_clothing_mask_gas_sechailer), typeof(obj_item_clothing_mask_gas_sechailer_swat), typeof(obj_item_clothing_mask_gas_sechailer_swat_spacepol), typeof(obj_item_clothing_mask_whistle),
        typeof(obj_item_clothing_gloves_tackler), typeof(obj_item_clothing_head_helmet_alt), typeof(obj_item_clothing_head_helmet_sec), typeof(obj_item_clothing_head_helmet_toggleable_riot),
        typeof(obj_item_clothing_head_fedora_inspector_hat), typeof(obj_item_clothing_suit_hooded_ablative), typeof(obj_item_clothing_suit_armor_riot), typeof(obj_item_clothing_suit_armor_vest_det_suit),
        typeof(obj_item_storage_bag_garment_hos), typeof(obj_item_storage_bag_garment_warden), typeof(obj_item_clothing_head_hats_warden_drill), typeof(obj_item_clothing_head_hats_hos_cap), typeof(obj_item_clothing_suit_armor_hos_hos_formal), typeof(obj_item_clothing_neck_cloak_hos), typeof(obj_item_storage_lockbox), typeof(obj_item_storage_lockbox_loyalty), typeof(obj_item_storage_lockbox_medal), typeof(obj_item_storage_lockbox_medal_sec),
        typeof(obj_item_storage_photo_album), typeof(obj_item_storage_photo_album_hos), typeof(obj_item_storage_box_rxglasses_spyglasskit), typeof(obj_item_clothing_glasses_sunglasses_spy), typeof(obj_item_clothing_accessory_spy_bug),
        typeof(obj_item_grenade_flashbang), typeof(obj_item_grenade_stingbang), typeof(obj_item_binoculars), typeof(obj_item_shield_buckler), typeof(obj_item_shield_riot), typeof(obj_item_shield_riot_tele),
        typeof(obj_item_pinpointer), typeof(obj_item_pinpointer_crew), typeof(obj_item_pinpointer_nuke), typeof(obj_item_megaphone), typeof(obj_item_megaphone_sec), typeof(obj_item_door_remote_head_of_security), typeof(obj_item_door_remote_captain),
        typeof(obj_item_stamp_head_hos), typeof(obj_item_gun_energy_disabler), typeof(obj_item_gun_energy_e_gun), typeof(obj_item_gun_energy_laser),
    };

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Make<T>(AudioRoom r, atom where) where T : atom
    {
        T thing = r.In(() => Dm.@new<T>(where));
        r.Step(2);
        return thing;
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T thing = Make<T>(r, r.In(() => who.loc as atom));
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(thing)))));
        return thing;
    }

    // A card with the trim's access, in hand: the keycard device and the head's grant read it.
    private static obj_item_card_id_advanced_gold Card(AudioRoom r, mob_living_carbon_human who, Type trim)
    {
        obj_item_card_id_advanced_gold card = Make<obj_item_card_id_advanced_gold>(r, who.loc as atom);
        r.In(() =>
        {
            Waiting.NoSleep(SSid_access.apply_trim_to_card(card, trim));
            Assert.IsType(trim, card.trim);
            Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(card))));
        });
        r.Step(2);
        return card;
    }

    private static string Open(AudioRoom r, mob who, atom thing, string expected)
    {
        Take(who);
        r.In(() => Waiting.NoSleep(thing.ui_interact(who)));
        JsonElement payload = LastUpdate(Take(who), out string window);
        Assert.Equal(expected, InterfaceOf(payload));
        return window;
    }

    [Fact]
    public void EveryTypeSpawnsLiveAndIsDestroyedWithNoRuntimes()
    {
        var (r, _, bob) = Room(9401);
        var failures = new List<string>();
        foreach (Type type in AllTypes)
        {
            obj_item item = r.In(() => (obj_item)Dm.@new(type, bob.loc));
            r.Step(2);
            if (r.In(() => InertSeam.IsInert(item)))
                failures.Add(type.Name + " is inert");
            r.In(() => Waiting.NoSleep(item.examine(bob)));
            r.In(() => qdel(item));
            r.Step(2);
        }
        foreach (Type type in new[] { typeof(obj_machinery_keycard_auth), typeof(obj_machinery_prisongate), typeof(obj_structure_detectiveboard), typeof(obj_structure_closet_secure_closet_brig_genpop), typeof(obj_structure_closet_secure_closet_labor_camp_security) })
        {
            atom thing = r.In(() => (atom)Dm.@new(type, locate(7, 4, 1)));
            r.Step(2);
            if (r.In(() => InertSeam.IsInert(thing)))
                failures.Add(type.Name + " is inert");
            r.In(() => qdel(thing));
            r.Step(2);
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheScannerReportsPrintsFibresAndBloodOnAnItemAndAPersonsHand()
    {
        var (r, feed, bob) = Room(9402);
        obj_item_detective_scanner scanner = Hold<obj_item_detective_scanner>(r, bob);
        obj_item_shard shard = Make<obj_item_shard>(r, r.In(() => locate(5, 4, 1)));
        mob_living_carbon_human alice = r.Dummy(6, 4);
        r.Step(2);
        r.In(() =>
        {
            alice.dna.unique_identity = "aliceidentity";
            shard.add_fingerprint_list(Dm.list(Dm.kv("print-of-alice", "print-of-alice")));
            shard.add_fiber_list(Dm.list(Dm.kv("Material from a grey jumpsuit.", "Material from a grey jumpsuit.")));
            Waiting.NoSleep(shard.add_blood_DNA(Dm.list(Dm.kv("dna-of-alice", "O+"))));
        });

        r.In(() => Waiting.Detach(scanner.safe_scan(bob, shard)));
        r.Step(1);
        Assert.True(r.In(() => Dm.truthy(scanner.scanner_busy)));
        r.In(() => Waiting.Detach(scanner.safe_scan(bob, alice))); // busy: nothing new
        r.Step(Second * 4);
        Assert.False(r.In(() => Dm.truthy(scanner.scanner_busy)));
        r.In(() => Waiting.Detach(scanner.safe_scan(bob, alice)));
        r.Step(Second * 4);
        Assert.Equal(2, r.In(() => length(scanner.log_data)));

        string window = Open(r, bob, scanner, "ForensicScanner");
        JsonElement data = r.In(() => JsonDocument.Parse(json_encode(Waiting.NoSleep(scanner.ui_data(bob)))).RootElement.Clone());
        JsonElement item = data.GetProperty("logs")[0];
        Assert.Equal("shard", item.GetProperty("scanTarget").GetString());
        string entries = item.GetProperty("dataEntries").ToString();
        Assert.Contains("print-of-alice", entries);
        Assert.Contains("Material from a grey jumpsuit.", entries);
        Assert.Contains("dna-of-alice", entries);
        Assert.Contains("O+", entries);
        // sorted by display order: prints, blood, fibres
        string[] categories = item.GetProperty("dataEntries").EnumerateArray().Select(e => e.GetProperty("category").GetString()).ToArray();
        Assert.Equal(new[] { DETSCAN_CATEGORY_FINGERS, DETSCAN_CATEGORY_BLOOD, DETSCAN_CATEGORY_FIBER }, categories);

        // an ungloved person's prints are the hash of their identity
        string alicePrint = r.In(() => md5(alice.dna.unique_identity));
        Assert.Contains(alicePrint, data.GetProperty("logs")[1].GetProperty("dataEntries").ToString());

        // deleting a log and printing the rest: the report's paper in hand, the logs gone
        Act(r, bob, window, "delete", ("index", 1));
        Assert.Equal(1, r.In(() => length(scanner.log_data)));
        Act(r, bob, window, "print");
        r.Step(Second * 4);
        obj_item_paper paper = r.In(() => Dm.each<obj_item_paper>(bob).Where(p => true).FirstOrDefault() ?? Dm.each<obj_item_paper>(bob.loc as atom).Where(p => true).FirstOrDefault());
        Assert.NotNull(paper);
        Assert.Equal("FR-1 'Forensic Record'", r.In(() => paper.name));
        Assert.Equal(0, r.In(() => length(scanner.log_data)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnEvidenceBagTakesAnItemAndShowsIt()
    {
        var (r, feed, bob) = Room(9403);
        obj_item_evidencebag bag = Hold<obj_item_evidencebag>(r, bob);
        obj_item_shard shard = Make<obj_item_shard>(r, bob.loc as atom);
        Assert.Equal("evidenceobj", r.In(() => bag.icon_state));
        r.In(() => Assert.True(Dm.truthy(bag.atom_storage.attempt_insert(shard, bob))));
        r.Step(2);
        Assert.Same(bag, r.In(() => shard.loc));
        Assert.Equal("evidence", r.In(() => bag.icon_state));
        Assert.Contains("shard", r.In(() => bag.desc));
        Assert.Equal(r.In(() => shard.w_class), r.In(() => bag.w_class));
        Assert.Contains(r.In(() => Dm.each(bag.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList()), s => s == "evidence");

        r.In(() => Waiting.NoSleep(bag.attack_self(bob)));
        r.Step(2);
        Assert.NotSame(bag, r.In(() => shard.loc));
        Assert.Equal("evidenceobj", r.In(() => bag.icon_state));
        Assert.Equal(WEIGHT_CLASS_TINY, r.In(() => bag.w_class));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePrisonerIdCountsPointsAndItsTimerStartsAtTheGate()
    {
        var (r, feed, bob) = Room(9404);
        obj_item_card_id_advanced_prisoner id = Hold<obj_item_card_id_advanced_prisoner>(r, bob);
        r.In(() =>
        {
            id.goal = 200;
            id.points = 35;
            World.Current.usr = bob;
            Waiting.NoSleep(id.attack_self(bob));
        });
        r.Step(2);
        Assert.True(feed.Any("You have accumulated 35 out of the 200 points"));

        // a brig card sets the sentence; the gate holds the prisoner to it, until it is served
        obj_machinery_prisongate gate = Make<obj_machinery_prisongate>(r, r.In(() => locate(5, 4, 1)));
        r.In(() =>
        {
            id.time_to_assign = 2;
            id.timed = TRUE;
        });
        Assert.Equal("allow_exit", r.In(() => Waiting.NoSleep(gate.allow_prisoner_id(id))));
        r.Step(2);
        Assert.Equal(2f, r.In(() => Dm.num(id.time_left)));
        Assert.Equal("block_exit", r.In(() => Waiting.NoSleep(gate.allow_prisoner_id(id))));
        r.Step(Second * 4);
        Assert.True(r.In(() => Dm.num(id.time_left)) <= 0);
        Assert.Equal("allow_exit", r.In(() => Waiting.NoSleep(gate.allow_prisoner_id(id))));
        Assert.False(r.In(() => Dm.truthy(id.timed)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePlainclothesIdFlipsToItsCover()
    {
        var (r, feed, bob) = Room(9405);
        obj_item_card_id_advanced_plainclothes id = Hold<obj_item_card_id_advanced_plainclothes>(r, bob);
        r.In(() => SSid_access.apply_trim_override(id, id.alt_trim));
        r.In(() => id.update_label());
        Assert.Equal("Assistant", r.In(() => Dm.str(id.trim_assignment_override)));
        Assert.Contains("(Assistant)", r.In(() => id.name));
        r.In(() => SSid_access.remove_trim_override(id));
        Assert.Null(r.In(() => id.trim_assignment_override));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TwoKeycardDevicesRaiseARedAlertAndGrantMaintenanceAccessAndTheKeyGetsItsDepartment()
    {
        var (r, feed, bob) = Room(9406);
        mob_living_carbon_human alice = r.Join(6, 4, "Alice");
        r.Step(Second);
        Card(r, bob, typeof(datum_id_trim_job_chief_engineer));
        Card(r, alice, typeof(datum_id_trim_job_captain));
        obj_machinery_keycard_auth one = Make<obj_machinery_keycard_auth>(r, r.In(() => locate(4, 5, 1)));
        obj_machinery_keycard_auth two = Make<obj_machinery_keycard_auth>(r, r.In(() => locate(6, 5, 1)));

        string bobWindow = Open(r, bob, one, "KeycardAuth");
        string aliceWindow = Open(r, alice, two, "KeycardAuth");

        Act(r, bob, bobWindow, "red_alert");
        Assert.Equal(TRUE, r.In(() => one.waiting));
        Assert.Same(one, r.In(() => two.event_source));
        Assert.Same(one, r.In(() => one.event_source)); // tg's event reaches the sender too
        Assert.Contains(r.In(() => Dm.each(two.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList()), s => s == "auth_on");
        Act(r, alice, aliceWindow, "auth_swipe");
        r.Step(2);
        Assert.True(r.In(() => Dm.num(SSsecurity_level.get_current_level_as_number())) >= SEC_LEVEL_RED);
        r.Step(Second * 3); // the two second windows pass
        Assert.Null(r.In(() => two.event_source));
        Assert.False(r.In(() => Dm.truthy(one.waiting)));

        // emergency maintenance access: the same dance
        Act(r, bob, bobWindow, "emergency_maint");
        Act(r, alice, aliceWindow, "auth_swipe");
        r.Step(4);
        Assert.True(r.In(() => Dm.truthy(GLOB.emergency_access)));

        // the chief engineer's card sends the janitor's key their department, and the key opens its door
        r.Step(Second * 3);
        obj_item_access_key key = Hold<obj_item_access_key>(r, alice);
        Act(r, bob, bobWindow, "give_janitor_access");
        Assert.Equal(REGION_ENGINEERING, r.In(() => Dm.str(key.department_access_value)));
        obj_machinery_door_airlock door = Make<obj_machinery_door_airlock>(r, r.In(() => locate(7, 4, 1)));
        r.In(() =>
        {
            door.req_access = Dm.list(ACCESS_ENGINEERING);
            door.req_one_access = null;
        });
        r.Step(Second);
        Assert.True(r.In(() => Dm.truthy(door.density)));
        r.In(() => Waiting.Detach(key.attempt_open_door(alice, door)));
        r.Step(Second * 6);
        Assert.False(r.In(() => Dm.truthy(door.density)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHailerSpeaksOverheatsAndBreaks()
    {
        var (r, feed, bob) = Room(9407);
        obj_item_clothing_mask_gas_sechailer mask = Hold<obj_item_clothing_mask_gas_sechailer>(r, bob);
        for (int i = 0; i < 6; i++)
        {
            r.Command(() =>
            {
                World.Current.usr = bob;
                Waiting.Detach(mask.halt());
            });
            r.Step(Second * 3 + 1); // over the phrase cooldown, inside the overuse window
        }
        Assert.True(feed.Any("Compli-o-Nator"));
        Assert.True(r.In(() => Dm.truthy(mask.broken_hailer)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMountTakesASecliteAndTheRiotHelmetShowsIt()
    {
        var (r, feed, bob) = Room(9408);
        obj_item_clothing_head_helmet_sec helmet = Make<obj_item_clothing_head_helmet_sec>(r, bob.loc as atom);
        obj_item_flashlight_seclite light = Make<obj_item_flashlight_seclite>(r, bob.loc as atom);
        var mount = r.In(() => helmet.GetComponent(typeof(datum_component_seclite_attachable)) as datum_component_seclite_attachable);
        Assert.NotNull(mount);
        r.In(() => mount.add_light(light));
        r.Step(2);
        Assert.Same(helmet, r.In(() => light.loc));
        Assert.Contains("-flight", r.In(() => helmet.icon_state));
        r.In(() => qdel(helmet));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(light)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void GlovesLeapAtATargetWithTheRightClickInThrowModeAndTackleIt()
    {
        var (r, feed, bob) = Room(9414);
        mob_living_carbon_human alice = r.Dummy(7, 4);
        r.Step(2);
        obj_item_clothing_gloves_tackler gloves = Make<obj_item_clothing_gloves_tackler>(r, r.In(() => bob.loc as atom));
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(gloves, ITEM_SLOT_GLOVES, disable_warning: TRUE, bypass_equip_delay_self: TRUE)))));
        r.Step(2);
        datum_component_tackler tackler = r.In(() => bob.GetComponent(typeof(datum_component_tackler)) as datum_component_tackler);
        Assert.NotNull(tackler);
        r.Step(Second * 2); // the first second of the gloves is the cooldown
        Assert.False(r.In(() => Dm.truthy(tackler.tackling)));
        r.In(() => bob.toggle_throw_mode());
        Click(r, bob, alice, "right=1");
        r.Step(Second * 2);
        Assert.True(feed.Any("You leap at"));
        Assert.True(r.In(() => bob.loc != locate(4, 4, 1)));
        // the tackle lands: one of the roll's outcomes staggers or floors the target, or the sacker
        Assert.True(feed.Any("tackle"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMegaphoneShoutsInTheCommandSpanAndNeedsToRecharge()
    {
        var (r, feed, bob) = Room(9416);
        obj_item_megaphone_sec megaphone = Hold<obj_item_megaphone_sec>(r, bob);
        r.Step(2);
        DmList speech = r.In(() => Dm.list("hello", null, null));
        DmList treat = null;
        r.In(() =>
        {
            DmList args = Dm.list("hello", null, Dm.list());
            megaphone.handle_speech(bob, args);
            speech = args;
            // D308: living/treat_message's arguments (message, tts message, tts filter, capitalize): the megaphone's ffmpeg filter
            treat = Dm.list("hello", "hello", Dm.list(), TRUE);
            megaphone.add_tts_filter(bob, treat);
        });
        Assert.True(r.In(() => Dm.@in(SPAN_COMMAND, speech[SPEECH_SPANS])));
        Assert.True(r.In(() => Dm.@in("acrusher=samples=2:level_out=6,aecho=delays=90:decays=0.3,aemphasis=type=cd,acontrast=30,crystalizer=i=5", treat[TREAT_TTS_FILTER_ARG])));
        r.In(() => megaphone.handle_speech(bob, Dm.list("again", null, Dm.list())));
        Assert.True(feed.Any("needs to recharge"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDrillHatShoutsAndItsBrokenCircuitSpeaksCanadian()
    {
        var (r, feed, bob) = Room(9417);
        obj_item_clothing_head_hats_warden_drill hat = Make<obj_item_clothing_head_hats_warden_drill>(r, r.In(() => bob.loc as atom));
        string Say(string text)
        {
            DmList args = Dm.list(text, null, null);
            r.In(() => hat.handle_speech(bob, args));
            return Dm.str(args[SPEECH_MESSAGE]);
        }
        Assert.Equal("Halt", Say("Halt"));
        r.In(() => hat.mode = "yelling");
        Assert.Equal("Halt!!", Say("Halt"));
        r.In(() => hat.mode = "canadian");
        Assert.Contains("Halt", Say("Halt there"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARiotShieldBlocksAThrownThing()
    {
        var (r, feed, bob) = Room(9415);
        obj_item_shield_riot shield = Hold<obj_item_shield_riot>(r, bob);
        obj_item_shard shard = Make<obj_item_shard>(r, r.In(() => bob.loc as atom));
        r.In(() =>
        {
            shield.block_chance = 100;
            Assert.True(Dm.truthy(Waiting.NoSleep(shield.hit_reaction(bob, shard, "the shard", 100, 10, THROWN_PROJECTILE_ATTACK, BRUTE))));
        });
        Assert.True(r.In(() => Dm.num(shield.atom_integrity)) < 75);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFlashbangStunsAndDeafensItsHearers()
    {
        var (r, feed, bob) = Room(9409);
        mob_living_carbon_human alice = r.Dummy(5, 4);
        r.Step(2);
        obj_item_grenade_flashbang bang = Make<obj_item_grenade_flashbang>(r, r.In(() => locate(5, 5, 1)));
        r.In(() => Waiting.Detach(bang.detonate()));
        r.Step(4);
        Assert.True(r.In(() => QDELETED(bang)));
        Assert.True(r.In(() => Dm.truthy(alice.IsKnockdown()) || Dm.truthy(alice.IsParalyzed())));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCrewPinpointerFollowsItsTargetsSensors()
    {
        var (r, feed, bob) = Room(9410);
        obj_item_pinpointer_crew pin = Hold<obj_item_pinpointer_crew>(r, bob);
        r.In(() =>
        {
            pin.target = bob;
            pin.active = FALSE;
            pin.toggle_on();
        });
        Assert.True(r.In(() => Dm.truthy(pin.active)));
        r.Step(4);
        // no suit sensors on a naked man: the pinpointer loses him at its scan and stops
        Assert.False(r.In(() => Dm.truthy(pin.active)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDoorRemoteOnlyCommandsItsDoors()
    {
        var (r, feed, bob) = Room(9411);
        obj_item_door_remote_head_of_security remote = Hold<obj_item_door_remote_head_of_security>(r, bob);
        r.Step(2);
        Assert.Equal("remote_security_open", r.In(() => remote.icon_state));
        obj_machinery_door_airlock door = Make<obj_machinery_door_airlock>(r, r.In(() => locate(6, 4, 1)));
        r.In(() => door.req_access = Dm.list(ACCESS_SECURITY));
        r.Step(Second);
        r.In(() => Waiting.NoSleep(remote.attack_self(bob)));
        Assert.Equal("remote_security_bolt", r.In(() => remote.icon_state));
        r.In(() => Waiting.Detach(remote.ranged_interact_with_atom(door, bob)));
        r.Step(4);
        Assert.True(r.In(() => Dm.truthy(door.locked)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDetectivesOutfitLightsHisCigarette()
    {
        var (r, feed, bob) = Room(9412);
        mob_living_carbon_human det = r.Dummy(6, 4);
        r.Step(2);
        r.In(() => Waiting.NoSleep(det.equipOutfit(typeof(datum_outfit_job_detective))));
        r.Step(2);
        obj_item_cigarette cig = r.In(() => det.wear_mask as obj_item_cigarette);
        Assert.NotNull(cig);
        Assert.True(r.In(() => Dm.truthy(cig.lit)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBoardHoldsCasesAndEvidenceOfAPhoto()
    {
        var (r, feed, bob) = Room(9413);
        obj_structure_detectiveboard board = Make<obj_structure_detectiveboard>(r, r.In(() => locate(5, 4, 1)));
        obj_item_photo photo = Hold<obj_item_photo>(r, bob);
        string window = Open(r, bob, board, "DetectiveBoard");
        // the case's name and colour come from the player's inputs; the case itself
        datum_case a_case = r.In(() =>
        {
            datum_case c = Dm.@new<datum_case>("The body in the bar", "red");
            Dm.append(ref board.cases, c);
            return c;
        });
        datum_evidence evidence = r.In(() =>
        {
            Waiting.NoSleep(bob.transferItemToLoc(photo, board));
            datum_evidence e = Dm.@new<datum_evidence>("Exhibit A", "the bar", photo);
            Dm.append(ref a_case.evidences, e);
            a_case.notices++;
            return e;
        });
        r.In(() => board.update_appearance());
        r.Step(2);
        Assert.Equal("photo", r.In(() => evidence.evidence_type));
        Assert.Contains(r.In(() => Dm.each(board.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList()), s => s == "notices_1");

        string caseRef = r.In(() => Dm.str(REF(a_case))), evidenceRef = r.In(() => Dm.str(REF(evidence)));
        Act(r, bob, window, "set_evidence_cords", ("case_ref", caseRef), ("evidence_ref", evidenceRef), ("rel_x", 50), ("rel_y", 60));
        Assert.Equal(50f, r.In(() => evidence.x));
        Act(r, bob, window, "remove_evidence", ("evidence_ref", evidenceRef));
        Assert.NotSame(board, r.In(() => photo.loc));
        Assert.Equal(0, r.In(() => length(a_case.evidences)));
        r.AssertNoRuntimes();
    }
}
