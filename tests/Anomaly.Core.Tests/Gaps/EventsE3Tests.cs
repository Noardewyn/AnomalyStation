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
/// Gaps package E3 (D327): the cargo and shuttle events in a booted toy room whose level and area are the station's. A stray cargo pod
/// lands in the room with an unlocked crate (the syndicate one: an emagged syndicate crate of uplink items in a syndicate pod); the
/// shuttle insurance's offer reaches the communications console and, bought, takes its price from the cargo budget, so a shuttle
/// catastrophe then pays the budget instead; a shuttle loan, accepted, pays the budget and sends the supply shuttle (a stand-in in the
/// room's east columns) back with what its situation ships: the corpses of the virus samples, the ticking bomb, the Spider Clan's gift.
/// </summary>
public class EventsE3Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed, int width = 15, int height = 11)
    {
        var r = new AudioRoom(width, height, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.In(() =>
        {
            GLOB.station_levels_cache.len = 1;
            GLOB.station_levels_cache[1] = TRUE;
            SSmapping.z_list = Dm.list(Dm.@new<datum_space_level>(1, "station", Dm.list(Dm.kv(ZTRAIT_STATION, TRUE))));
            var room = (area)get_area(bob);
            room.requires_power = FALSE; // the toy room has no APC
            GLOB.the_station_areas = Dm.list(room.type);
            // CentCom's holding area for pods in transit (tg's supplypod_temp_holding), as MarketTests' room: its corner turf
            var lane = GLOB.areas_by_type[typeof(area_centcom_central_command_areas_supplypod_supplypod_temp_holding)] as area
                ?? Dm.@new<area_centcom_central_command_areas_supplypod_supplypod_temp_holding>();
            set_turf_to_area(locate(1, 1, 1), lane);
        });
        r.Step(Second);
        for (int i = 0; i < 30 && r.In(() => SSeconomy.times_fired) < 1; i++)
            r.Step(Second);
        return (r, feed, bob);
    }

    private static T Control<T>(AudioRoom r) where T : datum_round_event_control =>
        r.In(() => (T)Dm.each<datum_round_event_control>(SSevents.control).First(c => c.GetType() == typeof(T)));

    private static T Start<T>(AudioRoom r, datum_round_event_control control) where T : datum_round_event
    {
        DmTask<object> run = default;
        r.In(() =>
        {
            run = control.run_event(announce_chance_override: 100);
            Waiting.Detach(run);
        });
        r.Step(1);
        // run_event returns its event: one that ends at its first tick (the insured catastrophe) may have left SSevents.running already
        // (gaps R2: SSstation fires since its traits, so the MC's spread of the subsystems' first fires draws one more random offset)
        return r.In(() => run.TryGetResult(out object made) && made is T e ? e : Dm.each<T>(SSevents.running).First());
    }

    // can_spawn_event's own checks, past the round's earliest start (the toy room's round has just begun)
    private static bool CanSpawn<T>(AudioRoom r) where T : datum_round_event_control => r.In(() =>
    {
        T control = (T)Dm.each<datum_round_event_control>(SSevents.control).First(c => c.GetType() == typeof(T));
        control.earliest_start = -1;
        return Dm.truthy(control.can_spawn_event(1));
    });

    private static float Budget(AudioRoom r) => r.In(() => SSeconomy.get_dep_account(ACCOUNT_CAR).account_balance);

    // the supply shuttle's stand-in: an /area/shuttle/supply over iron floor in the room's east columns, as CargoEconomyTests' room
    private static (obj_docking_port_mobile_supply Port, area_shuttle_supply Area) SupplyShuttle(AudioRoom r, int x1, int y1, int x2, int y2) => r.In(() =>
    {
        var a = Dm.@new<area_shuttle_supply>();
        a.requires_power = FALSE;
        foreach (turf t in Dm.each<turf>(block(x1, y1, 1, x2, y2, 1)))
        {
            t.change_area((area)t.loc, a);
            Waiting.NoSleep(t.ChangeTurf(typeof(turf_open_floor_iron)));
        }
        var port = Dm.@new<obj_docking_port_mobile_supply>(locate((x1 + x2) / 2, (y1 + y2) / 2, 1));
        port.shuttle_areas = Dm.list(Dm.kv(a, TRUE));
        SSshuttle.supply = port;
        return (port, a);
    });

    private static obj_docking_port_mobile_emergency EmergencyShuttle(AudioRoom r) => r.In(() =>
    {
        var port = Dm.@new<obj_docking_port_mobile_emergency>(locate(2, 9, 1));
        SSshuttle.emergency = port;
        return port;
    });

    private static List<T> InArea<T>(AudioRoom r, area a) where T : atom =>
        r.In(() => Dm.each<turf>(a.get_turfs_from_all_zlevels()).SelectMany(t => Dm.each<T>(t.contents)).ToList());

    [Fact]
    public void TheE3EventsAreLiveAndInThePool()
    {
        foreach (Type type in new[]
        {
            typeof(datum_round_event_control_stray_cargo), typeof(datum_round_event_stray_cargo), typeof(datum_round_event_control_stray_cargo_syndicate),
            typeof(datum_round_event_stray_cargo_syndicate), typeof(datum_round_event_control_shuttle_catastrophe), typeof(datum_round_event_shuttle_catastrophe),
            typeof(datum_round_event_control_shuttle_insurance), typeof(datum_round_event_shuttle_insurance), typeof(datum_round_event_control_shuttle_loan),
            typeof(datum_round_event_shuttle_loan), typeof(datum_shuttle_loan_situation_antidote), typeof(datum_shuttle_loan_situation_department_resupply),
            typeof(datum_shuttle_loan_situation_syndiehijacking), typeof(datum_shuttle_loan_situation_lots_of_bees), typeof(datum_shuttle_loan_situation_jc_a_bomb),
            typeof(datum_shuttle_loan_situation_papers_please), typeof(datum_shuttle_loan_situation_pizza_delivery), typeof(datum_shuttle_loan_situation_russian_party),
            typeof(datum_shuttle_loan_situation_spider_gift), typeof(datum_shuttle_loan_situation_mail_strike), typeof(datum_supply_pack_misc_syndicate),
            typeof(obj_machinery_syndicatebomb_shuttle_loan), typeof(obj_effect_mob_spawn_corpse_human_assistant_beesease_infection),
            typeof(obj_effect_mob_spawn_corpse_human_assistant_brainrot_infection), typeof(obj_effect_mob_spawn_corpse_human_assistant_spanishflu_infection),
            typeof(obj_effect_mob_spawn_corpse_human_bee_terrorist), typeof(obj_effect_mob_spawn_corpse_human_cargo_tech),
            typeof(obj_effect_mob_spawn_corpse_human_nanotrasensoldier), typeof(obj_effect_mob_spawn_corpse_human_cat_butcher),
            typeof(datum_component_temporary_description), typeof(mob_living_basic_spider_giant_nurse), typeof(mob_living_basic_spider_giant_hunter),
            typeof(datum_action_cooldown_mob_cooldown_lay_web_sealer), typeof(obj_structure_spider_stickyweb_sealed), typeof(obj_item_clothing_mask_balaclava),
        })
            Assert.False(InertSeam.IsInertType(type), type.Name + " is inert");
        var (r, _, _) = Room(4301);
        var names = r.In(() => Dm.each<datum_round_event_control>(SSevents.control).Select(c => Dm.str(c.name)).ToList());
        Assert.Contains("Stray Cargo Pod", names);
        Assert.Contains("Stray Syndicate Cargo Pod", names);
        Assert.Contains("Shuttle Catastrophe", names);
        Assert.Contains("Shuttle Insurance", names);
        Assert.Contains("Shuttle Loan", names);
    }

    [Fact]
    public void AStrayCargoPodLandsInTheStationWithAnUnlockedCrate()
    {
        var (r, feed, _) = Room(4302);
        var cargo = Start<datum_round_event_stray_cargo>(r, Control<datum_round_event_control_stray_cargo>(r));
        area room = r.In(() => cargo.impact_area);
        Assert.NotNull(room);
        Assert.InRange(r.In(() => cargo.start_when), 20, 40);
        // the pods' pool leaves out the special packs and keeps the generated ones (a text id)
        r.In(() =>
        {
            DmList packs = datum_round_event_stray_cargo.stray_spawnable_supply_packs;
            Assert.True(packs.len > 50, "tg's packs");
            Assert.DoesNotContain(Dm.each(packs).Cast<object>(), p => p is Type t && Dm.truthy(Dm.bitand(Dm.initial(t, (datum_supply_pack s) => s.order_flags), ORDER_SPECIAL)));
            Assert.Contains(Dm.each(packs).Cast<object>(), p => p is string);
        });
        // a secure crate, so its unlocking shows
        r.In(() => cargo.possible_pack_types = Dm.list(typeof(datum_supply_pack_security_supplies)));
        r.Step(5 * Second);
        Assert.True(feed.Any("Stray cargo pod detected on long-range scanners. Expected location of impact: " + r.In(() => room.name) + "."));

        obj_effect_pod_landingzone marker = null;
        for (int i = 0; i < 90 && marker == null; i++)
        {
            r.Step(Second);
            marker = r.In(() => Dm.each<obj_effect_pod_landingzone>(Dm.world).FirstOrDefault());
        }
        Assert.NotNull(marker);
        Assert.Same(room, r.In(() => get_area(marker)));
        obj_structure_closet_crate_secure crate = null;
        for (int i = 0; i < 30 && crate == null; i++)
        {
            r.Step(Second);
            crate = r.In(() => Dm.each<obj_structure_closet_crate_secure>(Dm.world).FirstOrDefault(c => c.loc is turf));
        }
        Assert.NotNull(crate);
        Assert.Same(room, r.In(() => get_area(crate)));
        Assert.Equal(FALSE, r.In(() => crate.locked));
        Assert.True(r.In(() => crate.contents.len) > 0, "the pack's contents");
    }

    [Fact]
    public void AStraySyndicatePodBringsAnEmaggedCrateOfUplinkItems()
    {
        var (r, feed, _) = Room(4303);
        var cargo = Start<datum_round_event_stray_cargo_syndicate>(r, Control<datum_round_event_control_stray_cargo_syndicate>(r));
        var pod = r.In(() => (obj_structure_closet_supplypod)cargo.make_pod());
        Assert.Equal(typeof(datum_pod_style_syndicate), r.In(() => pod.style));
        r.In(() => qdel(pod));
        // the crate itself, as start makes it
        var crate = r.In(() => (obj_structure_closet_crate_secure_syndicate)Waiting.NoSleep(cargo.get_contents()));
        Assert.NotNull(crate);
        Assert.Equal(TRUE, r.In(() => crate.broken)); // emagged: its lock broken
        Assert.Equal(FALSE, r.In(() => crate.locked));
        var items = r.In(() => Dm.each<obj_item>(crate.contents).ToList());
        Assert.NotEmpty(items);
        // thirty telecrystals of the traitors' uplink, by tg's surplus rules
        float tc = r.In(() => items.Sum(i => Dm.each<datum_uplink_item>(SStraitor.uplink_items).Where(u => u.item == i.GetType()).Select(u => u.cost).DefaultIfEmpty(0).First()));
        Assert.True(tc > 0 && tc <= 30, "the crate's worth: " + tc);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void BoughtShuttleInsuranceMakesTheCatastrophePayTheBudget()
    {
        var (r, feed, _) = Room(4304);
        var emergency = EmergencyShuttle(r);
        var console = Place<obj_machinery_computer_communications>(r, 6, 8);
        r.In(() => SSeconomy.get_dep_account(ACCOUNT_CAR).adjust_money(10000));
        Assert.True(CanSpawn<datum_round_event_control_shuttle_insurance>(r), "the insurance can run");

        var insurance = Start<datum_round_event_shuttle_insurance>(r, Control<datum_round_event_control_shuttle_insurance>(r));
        r.Step(5 * Second);
        Assert.True(feed.Any("Incoming subspace communication. Secure channel opened at all communication consoles."));
        Assert.Equal(5000f, r.In(() => insurance.insurance_evaluation)); // no template is the emergency shuttle's
        var message = r.In(() => Dm.each<datum_comm_message>(console.messages).Single());
        Assert.Equal("Shuttle Insurance", r.In(() => Dm.str(message.title)));
        Assert.Contains("we're offering 5000 to cover for your shuttle", r.In(() => Dm.str(message.content)));
        Assert.Contains("this is the " + r.In(() => insurance.ship_name), r.In(() => Dm.str(message.content)));

        // the console's answer: Purchase Insurance.
        float before = Budget(r);
        r.In(() =>
        {
            message.answered = 1;
            message.answer_callback.InvokeAsync();
        });
        Assert.True(feed.Any("Thank you for purchasing shuttle insurance!"));
        Assert.Equal(TRUE, r.In(() => SSshuttle.shuttle_insurance));
        Assert.Equal(before - 5000, Budget(r));

        // the catastrophe now pays instead of replacing the shuttle
        before = Budget(r);
        var catastrophe = Start<datum_round_event_shuttle_catastrophe>(r, Control<datum_round_event_control_shuttle_catastrophe>(r));
        Assert.Null(r.In(() => catastrophe.new_shuttle));
        r.Step(5 * Second);
        Assert.True(feed.Any("Luckily, your shuttle insurance has covered the costs of repair! You have been awarded a bonus from"));
        Assert.Equal(before + 8000, Budget(r));
        Assert.Same(emergency, r.In(() => SSshuttle.emergency));
        Assert.NotEqual((float)SHUTTLEPURCHASE_FORCED, r.In(() => SSshuttle.shuttle_purchased));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnInsuranceBoughtTooLateOrUnpaidIsRefused()
    {
        var (r, feed, _) = Room(4305);
        var emergency = EmergencyShuttle(r);
        Place<obj_machinery_computer_communications>(r, 6, 8);
        Assert.True(CanSpawn<datum_round_event_control_shuttle_catastrophe>(r), "the catastrophe can run");
        var insurance = Start<datum_round_event_shuttle_insurance>(r, Control<datum_round_event_control_shuttle_insurance>(r));
        r.Step(5 * Second);
        // an empty budget cannot pay
        r.In(() =>
        {
            var budget = SSeconomy.get_dep_account(ACCOUNT_CAR);
            budget.adjust_money(-budget.account_balance);
            insurance.insurance_message.answered = 1;
            Waiting.NoSleep(insurance.answered());
        });
        Assert.True(feed.Any("is considered scamming."), string.Join(" | ", feed.Chat.Select(c => c.Html))); // the announcement encodes its apostrophes
        Assert.Equal(FALSE, r.In(() => SSshuttle.shuttle_insurance));
        // a docked shuttle is too late
        r.In(() =>
        {
            emergency.mode = SHUTTLE_DOCKED;
            Waiting.NoSleep(insurance.answered());
        });
        Assert.True(feed.Any("You are definitely too late to purchase insurance, my friends."));
        Assert.False(CanSpawn<datum_round_event_control_shuttle_catastrophe>(r), "no catastrophe once it docked");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnAcceptedShuttleLoanPaysAndBringsTheBombBack()
    {
        var (r, feed, _) = Room(4306);
        var (shuttle, supplyArea) = SupplyShuttle(r, 8, 2, 14, 10);
        var loan = Start<datum_round_event_shuttle_loan>(r, Control<datum_round_event_control_shuttle_loan>(r));
        Type picked = r.In(() => loan.situation.GetType());
        Assert.Contains(picked, r.In(() => Dm.each<Type>(Control<datum_round_event_control_shuttle_loan>(r).unavailable_situations).ToList()));
        Assert.False(CanSpawn<datum_round_event_control_shuttle_loan>(r), "one loan at a time");
        r.Step(7 * Second);
        Assert.Same(loan, r.In(() => SSshuttle.shuttle_loan));
        Assert.True(feed.Any("Cargo: " + r.In(() => loan.situation.announcement_text)));

        // the ticking bomb's loan, for a known cargo
        r.In(() => loan.situation = Dm.@new<datum_shuttle_loan_situation_jc_a_bomb>());
        float before = Budget(r);
        r.In(() => Waiting.NoSleep(loan.loan_shuttle()));
        Assert.True(feed.Any("Live explosive ordnance incoming via supply shuttle. Evacuating cargo bay is recommended."));
        Assert.Equal(TRUE, r.In(() => loan.dispatched));
        Assert.Equal(before + CARGO_CRATE_VALUE * 225, Budget(r));
        Assert.Equal(SHUTTLE_CALL, r.In(() => shuttle.mode));
        Assert.Equal(3000f, r.In(() => Dm.num(shuttle.timer) - Dm.world.time)); // five minutes
        Assert.Contains("Live explosive ordnance incoming. Exercise extreme caution.", r.In(() => SSshuttle.centcom_message));

        // the next event tick loads the shuttle, still at CentCom with its five minutes to go (the toy room's SSshuttle does not fire),
        // so the bomb's timer of eight to ten minutes leaves three to five after it docks
        r.Step(6 * Second);
        Assert.Equal(SHUTTLE_CALL, r.In(() => shuttle.mode));
        Assert.DoesNotContain(loan, r.In(() => Dm.each<datum_round_event_shuttle_loan>(SSevents.running).ToList()));
        Assert.Null(r.In(() => SSshuttle.shuttle_loan));
        var bomb = Assert.Single(InArea<obj_machinery_syndicatebomb_shuttle_loan>(r, supplyArea));
        Assert.Equal(TRUE, r.In(() => bomb.active));
        Assert.Equal(TRUE, r.In(() => bomb.anchored));
        Assert.InRange(r.In(() => bomb.timer_set), 480, 600);
        Assert.Single(InArea<obj_item_paper_fluff_cargo_bomb>(r, supplyArea));
    }

    [Fact]
    public void AnUnacceptedLoanEndsWithoutCargo()
    {
        var (r, feed, _) = Room(4307);
        var (_, supplyArea) = SupplyShuttle(r, 8, 2, 14, 10);
        var loan = Start<datum_round_event_shuttle_loan>(r, Control<datum_round_event_control_shuttle_loan>(r));
        r.Step(7 * Second);
        Assert.Same(loan, r.In(() => SSshuttle.shuttle_loan));
        r.In(() => Waiting.NoSleep(loan.end()));
        Assert.Null(r.In(() => SSshuttle.shuttle_loan));
        Assert.DoesNotContain(InArea<atom_movable>(r, supplyArea), a => a is not obj_docking_port);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheLoansShipTheirCorpsesCreaturesAndCargo()
    {
        var (r, _, _) = Room(4308, 21, 15);
        var (_, supplyArea) = SupplyShuttle(r, 8, 2, 20, 14);
        DmList Floors() => r.In(() => Dm.list(Dm.each<turf_open_floor>(supplyArea.get_turfs_from_all_zlevels()).Where(t => !Dm.truthy(Waiting.NoSleep(t.is_blocked_turf()))).Cast<object>().ToArray()));

        // the virus samples: ten dead assistants and the bottles
        var spawn = r.In(() => Dm.list());
        r.In(() => Waiting.NoSleep(Dm.@new<datum_shuttle_loan_situation_antidote>().spawn_items(spawn, Floors(), Dm.list())));
        r.Step(Second);
        var dead = InArea<mob_living_carbon_human>(r, supplyArea);
        Assert.Equal(10, dead.Count);
        Assert.All(dead, h => Assert.Equal(DEAD, r.In(() => h.stat)));
        Assert.All(dead, h => Assert.Equal(JOB_ASSISTANT, r.In(() => Dm.str(h.job))));
        Assert.Empty(InArea<obj_effect_mob_spawn>(r, supplyArea)); // the spawners leave
        Assert.True(r.In(() => Dm.@in(typeof(obj_item_reagent_containers_cup_bottle_pierrot_throat), spawn) && Dm.@in(typeof(obj_structure_closet_crate), spawn)));
        // the viruses (gaps VI1): the corpse spawner kills its body before the subtype's special gives the virus (mob_spawn.dm:356), and a
        // dead body takes no disease that does not process the dead (_MobProcs.dm:11), so tg's assistants lie without theirs too
        Assert.All(dead, h => Assert.Equal(0f, r.In(() => Dm.length(h.diseases))));
        Assert.DoesNotContain(DmWorldState.Of(r.World).DeferredSites, site => site.StartsWith("code/modules/mob_spawn/corpses/job_corpses.dm", StringComparison.Ordinal));
        r.In(() => { foreach (var h in dead) qdel(h); });

        // the bees: their keepers' corpses, a toxin swarm and the beekeeping crate
        spawn = r.In(() => Dm.list());
        r.In(() => Waiting.NoSleep(Dm.@new<datum_shuttle_loan_situation_lots_of_bees>().spawn_items(spawn, Floors(), Dm.list())));
        Assert.Equal(8, r.In(() => Dm.each<Type>(spawn).Count(t => t == typeof(mob_living_basic_bee_toxin))));
        Assert.Contains(typeof(obj_effect_mob_spawn_corpse_human_bee_terrorist), r.In(() => Dm.each<Type>(spawn).ToList()));
        Assert.Equal(10, InArea<obj_item_ammo_casing_spent>(r, supplyArea).Count);
        r.In(() => { foreach (Type t in Dm.each<Type>(spawn).Where(t => ispath(t, typeof(obj_effect_mob_spawn)))) Dm.@new(t, Floors()[1]); });
        r.Step(Second);
        Assert.Equal(4, InArea<mob_living_carbon_human>(r, supplyArea).Count(h => r.In(() => h.stat) == DEAD));

        // the Spider Clan's gift: live spiders, a victim's remains and webs
        spawn = r.In(() => Dm.list());
        r.In(() => Waiting.NoSleep(Dm.@new<datum_shuttle_loan_situation_spider_gift>().spawn_items(spawn, Floors(), Dm.list())));
        Assert.Single(InArea<obj_item_clothing_mask_balaclava>(r, supplyArea));
        Assert.Single(InArea<obj_item_clothing_shoes_jackboots_fast>(r, supplyArea));
        Assert.True(InArea<obj_structure_spider_stickyweb>(r, supplyArea).Count >= 5);
        Assert.Contains(typeof(mob_living_basic_spider_giant_nurse), r.In(() => Dm.each<Type>(spawn).ToList()));
        var nurse = r.In(() => Dm.@new<mob_living_basic_spider_giant_nurse>(Floors()[1]));
        Assert.NotNull(r.In(() => nurse.GetComponent(typeof(datum_component_healing_touch))));
        Assert.True(r.In(() => HAS_TRAIT(nurse, TRAIT_MEDICAL_HUD)));

        // the syndicate hijackers and the Russian party: tg's troopers (live with gaps E2c) and the party crate
        spawn = r.In(() => Dm.list());
        r.In(() => Waiting.NoSleep(Dm.@new<datum_shuttle_loan_situation_syndiehijacking>().spawn_items(spawn, Floors(), Dm.list())));
        Assert.InRange(r.In(() => Dm.each<Type>(spawn).Count(t => t == typeof(mob_living_basic_trooper_syndicate_ranged_infiltrator))), 2, 4);
        spawn = r.In(() => Dm.list());
        r.In(() => Waiting.NoSleep(Dm.@new<datum_shuttle_loan_situation_russian_party>().spawn_items(spawn, Floors(), Dm.list())));
        Assert.Contains(typeof(mob_living_basic_trooper_russian_ranged), r.In(() => Dm.each<Type>(spawn).ToList()));
        Assert.Contains(typeof(mob_living_basic_bear_russian), r.In(() => Dm.each<Type>(spawn).ToList()));

        // the paperwork, the pizza and the department resupply
        spawn = r.In(() => Dm.list());
        r.In(() => Waiting.NoSleep(Dm.@new<datum_shuttle_loan_situation_papers_please>().spawn_items(spawn, Floors(), Dm.list())));
        Assert.True(r.In(() => spawn.len) > 3);
        Assert.DoesNotContain(r.In(() => Dm.each<Type>(spawn).ToList()), t => t == typeof(obj_item_paperwork_photocopy) || t.IsSubclassOf(typeof(obj_item_paperwork_ancient)) || t == typeof(obj_item_paperwork_ancient));
        spawn = r.In(() => Dm.list());
        r.In(() => Waiting.NoSleep(Dm.@new<datum_shuttle_loan_situation_pizza_delivery>().spawn_items(spawn, Floors(), Dm.list())));
        Assert.Equal(6, r.In(() => Dm.each<Type>(spawn).Count(t => t.IsSubclassOf(typeof(obj_item_pizzabox)))));
        int crates = InArea<obj_structure_closet>(r, supplyArea).Count;
        spawn = r.In(() => Dm.list());
        r.In(() => Waiting.NoSleep(Dm.@new<datum_shuttle_loan_situation_department_resupply>().spawn_items(spawn, Floors(), Dm.list())));
        Assert.Equal(crates + 9, InArea<obj_structure_closet>(r, supplyArea).Count);

        // the mail strike: four to seven crates of mail
        spawn = r.In(() => Dm.list());
        r.In(() => Waiting.NoSleep(Dm.@new<datum_shuttle_loan_situation_mail_strike>().spawn_items(spawn, Floors(), Dm.list())));
        Assert.InRange(r.In(() => Dm.each<Type>(spawn).Count(t => t == typeof(obj_structure_closet_crate_mail_full_mail_strike))), 4, 7);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACorpseSpawnerLeavesADeadBodyWithItsStory()
    {
        var (r, _, bob) = Room(4309);
        var body = r.In(() =>
        {
            var spawner = Dm.@new<obj_effect_mob_spawn_corpse_human_cargo_tech>(locate(7, 7, 1), TRUE);
            spawner.corpse_description = "A crate fell on them.";
            spawner.brute_damage = 40;
            return (mob_living_carbon_human)spawner.create();
        });
        r.Step(Second);
        Assert.Equal(DEAD, r.In(() => body.stat));
        Assert.Equal(JOB_CARGO_TECHNICIAN, r.In(() => Dm.str(body.job)));
        Assert.True(r.In(() => Dm.num(body.get_brute_loss())) >= 40);
        Assert.NotNull(r.In(() => body.w_uniform));
        Assert.Empty(r.In(() => Dm.each<obj_effect_mob_spawn>(locate(7, 7, 1)).ToList()));
        var examine = r.In(() => Dm.each<object>(Waiting.NoSleep(body.examine(bob)) as DmList).Select(Dm.str).ToList());
        Assert.Contains(examine, line => line.Contains("A crate fell on them.", StringComparison.Ordinal));
        // a revived body forgets its story
        r.In(() => body._SendSignal(COMSIG_LIVING_REVIVE, Dm.list(body)));
        Assert.Null(r.In(() => body.GetComponent(typeof(datum_component_temporary_description))));
        r.AssertNoRuntimes();
    }
}
