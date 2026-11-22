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
/// Syndicate package X2 (D316): the ghosts' polls (SSpolling, candidate_poll.dm, the poll alert) and the holoparasites
/// (mob/living/basic/guardian whole) in a booted toy room. A poll throws its alert on every ghost that takes ghost roles, with tg's
/// prompt sound and chat line; a click signs up, an alt-click says never for this round, the opted-out ghost gets nothing, and a poll
/// nobody answers times out empty. The tech injector's radial and poll make the chosen ghost the guardian bound to the user, which
/// names and colours itself, manifests, recalls, shares its damage with the host and is pulled back by the leash. Then one ability of
/// each type. (The role bans cannot be exercised: nobody is role banned, DV194.)
/// </summary>
public class HoloparasiteTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed, int width = 30)
    {
        var r = new AudioRoom(width, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, feed, bob);
    }

    /// <summary>A player who joins on (x, y) and ghosts: their client is on the observer.</summary>
    private static mob_dead_observer Ghost(AudioRoom r, int x, int y, string key)
    {
        mob_living_carbon_human body = r.Join(x, y, key);
        client player = body.client;
        r.In(() => body.ghostize(TRUE));
        r.Step(2);
        return Assert.IsType<mob_dead_observer>(player.mob);
    }

    // a chat line's target: the mob or its client
    private static bool For(AudioRoom r, object target, mob who) => target == who || target == r.In(() => who.client);

    private static atom_movable_screen_alert_poll_alert AlertOf(AudioRoom r, mob who)
        => r.In(() => Dm.each(who.alerts).Select(k => who.alerts[k]).OfType<atom_movable_screen_alert_poll_alert>().SingleOrDefault());

    /// <summary>Runs a poll of the ghosts as a detached proc (it sleeps until its time is up); the result lands in the box.</summary>
    private static object[] StartPoll(AudioRoom r, object ignore = null, string role = "Test Role")
    {
        var box = new object[1];
        async DmTask Run()
        {
            box[0] = await SSpolling.poll_ghost_candidates(check_jobban: ROLE_PAI, poll_time: 10 * SECONDS, ignore_category: ignore, role_name_text: role, amount_to_pick: 1);
        }
        r.In(() => Waiting.Detach(Run()));
        r.Step(2);
        return box;
    }

    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(datum_controller_subsystem_polling), typeof(datum_candidate_poll), typeof(atom_movable_screen_alert_poll_alert),
            typeof(obj_item_guardian_creator_tech), typeof(obj_item_guardian_creator_carp), typeof(obj_item_guardian_creator_miner), typeof(obj_item_guardian_creator_wizard),
            typeof(mob_living_basic_guardian_standard), typeof(mob_living_basic_guardian_assassin), typeof(mob_living_basic_guardian_charger), typeof(mob_living_basic_guardian_dextrous),
            typeof(mob_living_basic_guardian_explosive), typeof(mob_living_basic_guardian_gaseous), typeof(mob_living_basic_guardian_gravitokinetic), typeof(mob_living_basic_guardian_lightning),
            typeof(mob_living_basic_guardian_protector), typeof(mob_living_basic_guardian_ranged), typeof(mob_living_basic_guardian_support), typeof(datum_hud_guardian),
            typeof(datum_component_life_link), typeof(datum_component_leash), typeof(datum_component_damage_chain), typeof(datum_component_direct_explosive_trap),
            typeof(datum_component_ranged_mob_full_auto), typeof(datum_guardian_fluff_tech), typeof(datum_guardian_fluff_carp), typeof(datum_guardian_fluff_miner),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void APollOffersTheRoleToTheGhostsTheSignedUpOneIsChosenTheOptedOutAndNeverOnesAreNotAndAnUnansweredPollTimesOut()
    {
        var (r, feed, bob) = Room(7201, width: 13);
        mob_dead_observer gina = Ghost(r, 8, 2, "Gina");
        mob_dead_observer hal = Ghost(r, 9, 2, "Hal");
        mob_dead_observer ivy = Ghost(r, 10, 2, "Ivy");
        // Hal takes no ghost roles (the universal opt-out of tg's game preferences)
        r.In(() => hal.client.prefs.value_cache[typeof(datum_preference_toggle_ghost_roles)] = FALSE);

        object[] result = StartPoll(r, POLL_IGNORE_HOLOPARASITE);
        atom_movable_screen_alert_poll_alert ginas = AlertOf(r, gina);
        atom_movable_screen_alert_poll_alert ivys = AlertOf(r, ivy);
        Assert.True(ginas != null, "no alert: " + string.Join("\n", r.Room.RuntimeText) + " | polls " + r.In(() => SSpolling == null ? "no SSpolling" : Dm.length(SSpolling.currently_polling).ToString()) + " | " + r.In(() => string.Join(",", Dm.each(gina.alerts).Select(k => Dm.str(k)))));
        Assert.NotNull(ivys);
        Assert.Null(AlertOf(r, hal));
        Assert.Null(AlertOf(r, bob));
        Assert.Contains(r.In(() => Dm.each(gina.client.screen).ToList()), o => o == ginas);
        // tg's prompt (Calm, half volume by default), the boxed chat line with the sign-up and never links, the role over the alert
        Assert.Single(r.Sounds.Of(gina, "sound/misc/prompt1.ogg"));
        Assert.True(feed.Chat.Any(c => For(r, c.Target, gina) && c.Html.Contains("Do you want to play as") && c.Html.Contains("[Sign Up]") && c.Html.Contains("[Never For This Round]")), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.Equal("Test Role", r.In(() => ginas.poll.role));
        Assert.True(r.In(() => Dm.each(ginas.overlays).OfType<IDmAppearance>().Any(o => Dm.str(o.maptext).Contains("Test Role"))), "the role's maptext");

        Click(r, gina, ginas);
        Assert.True(feed.Any("You have signed up for Test Role!"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.True(r.In(() => Dm.@in(gina, ginas.poll.signed_up)));
        Click(r, ivy, ivys, "alt=1");
        Assert.True(feed.Any("Choice registered: Never for this round."));
        Assert.True(r.In(() => Dm.@in("ivy", GLOB.poll_ignore[POLL_IGNORE_HOLOPARASITE])));

        r.Step(12 * Second);
        Assert.Same(gina, result[0]);
        Assert.True(feed.Chat.Any(c => For(r, c.Target, ivy) && c.Html.Contains("was selected.")), "announced to the polled");
        Assert.Null(AlertOf(r, gina)); // the poll's alerts are cleared when it ends

        // a second poll of the same category: Ivy is never asked again this round; nobody signs up and it times out empty
        object[] second = StartPoll(r, POLL_IGNORE_HOLOPARASITE);
        Assert.NotNull(AlertOf(r, gina));
        Assert.Null(AlertOf(r, ivy));
        r.Step(12 * Second);
        Assert.Null(second[0]);
        Assert.Null(AlertOf(r, gina));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTechInjectorPollsTheChosenGhostBecomesTheGuardianThatManifestsRecallsSharesDamageAndIsLeashed()
    {
        var (r, feed, bob) = Room(7202);
        mob_dead_observer gina = Ghost(r, 20, 2, "Gina");
        client ginaClient = r.In(() => gina.client);
        obj_item_guardian_creator_tech injector = r.In(() => Dm.@new<obj_item_guardian_creator_tech>(bob.loc));
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(injector)))));

        // the radial of tg's types (all but dextrous), then the poll
        r.In(() => Waiting.Detach(injector.attack_self(bob)));
        r.Step(2);
        (long menuId, int index, List<string> names) = r.In(() =>
        {
            datum_radial_menu menu = Dm.each_cast<datum_radial_menu>(bob.client.dm_radial_menus).Single();
            List<string> slices = menu.dm_elements.Select(e => e.Name).ToList();
            Assert.Equal(10, Dm.length(menu.choices)); // tg's types but the dextrous, over two pages
            Assert.DoesNotContain("Dextrous", slices);
            return (menu.dm_id, slices.FindIndex(n => n == "Charger") + 1, slices);
        });
        Assert.True(index > 0, "Charger is not on the first page: " + string.Join(", ", names));
        r.Command(() => ClientSeam.RadialPick(bob.client, menuId, index));
        r.Step(2);
        Assert.True(feed.Any("You start to power on the injector..."));
        atom_movable_screen_alert_poll_alert alert = AlertOf(r, gina);
        Assert.NotNull(alert);
        Assert.Equal("Charger", r.In(() => alert.poll.role));
        Click(r, gina, alert);

        // the poll's 10 seconds; the guardian spawns in Bob, Gina's client moves in and names and colours it
        r.Step(12 * Second);
        mob_living_basic_guardian_charger guardian = r.In(() => ginaClient.mob as mob_living_basic_guardian_charger);
        Assert.NotNull(guardian);
        Assert.Same(bob, r.In(() => guardian.summoner));
        Assert.Same(bob, r.In(() => guardian.loc));
        Assert.True(feed.Any("Boot sequence complete. Overclocking motive engines. Holoparasite swarm online."), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.True(feed.Any("Holoparasite</b> is now online!"));
        string bobsName = r.In(() => Dm.str(bob.real_name));
        Assert.True(feed.Any("bound to serve " + bobsName), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.Contains(r.In(() => Dm.each(bob.actions).ToList()), a => a is datum_action_cooldown_mob_cooldown_recall_guardian);
        LastUpdate(Take(guardian), out string nameWindow);
        Act(r, guardian, nameWindow, "submit", ("entry", "Stardust"));
        LastUpdate(Take(guardian), out string colourWindow);
        Act(r, guardian, colourWindow, "submit", ("entry", "#33cc33"));
        Assert.Equal("Stardust", r.In(() => guardian.real_name));
        Assert.Equal("#33cc33", r.In(() => Dm.str(guardian.guardian_colour)));
        Assert.Equal("techbase", r.In(() => guardian.icon_state));
        Assert.Contains(r.In(() => Dm.each(guardian.overlays).OfType<IDmAppearance>().ToList()), o => Dm.str(o.icon_state) == "tech");

        // manifest beside the host (a phase-in flash), recall into him
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(guardian.manifest()))));
        Assert.Same(bob.loc, r.In(() => guardian.loc));
        Assert.Contains(r.In(() => Dm.each(((atom)bob.loc).contents).ToList()), o => o is obj_effect_temp_visual_guardian_phase);
        r.Step(Second + 1);
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(guardian.recall()))));
        Assert.Same(bob, r.In(() => guardian.loc));
        r.Step(Second + 1);
        r.In(() => Waiting.NoSleep(guardian.manifest()));

        // its damage goes to the host (the guardian's own physiology applied), its own health stays infinite
        float before = r.In(() => Dm.num(bob.get_brute_loss()));
        r.In(() => Waiting.NoSleep(guardian.adjust_brute_loss(20)));
        r.Step(2);
        Assert.True(r.In(() => Dm.num(bob.get_brute_loss())) > before, "the host took the guardian's damage");
        Assert.True(r.In(() => guardian.health) >= INFINITY);
        Assert.True(feed.Any("is under attack! You take damage!"));

        // the leash: the host goes 15 tiles away, the guardian is pulled back within its 10
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(19, 4, 1))));
        r.Step(Second);
        Assert.True(r.In(() => Dm.num(get_dist(guardian, bob))) <= 10, "pulled back: " + r.In(() => get_dist(guardian, bob)));
        r.AssertNoRuntimes();
    }

    // A mindless guardian of the type, themed tech, bound to Bob and manifested beside him.
    private static T Guardian<T>(AudioRoom r, mob_living_carbon_human bob) where T : mob_living_basic_guardian
    {
        T guardian = r.In(() =>
        {
            T made = Dm.@new<T>(bob, GLOB.guardian_themes[GUARDIAN_THEME_TECH]);
            Waiting.NoSleep(made.set_summoner(bob, different_person: TRUE));
            Waiting.NoSleep(made.set_guardian_colour("#3366ff"));
            return made;
        });
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(guardian.manifest()))));
        r.Step(2);
        return guardian;
    }

    [Fact]
    public void EachTypesAbility()
    {
        var (r, feed, bob) = Room(7203);
        mob_living_carbon_human alice = r.Join(6, 4, "Alice");
        r.Step(Second);

        // assassin: stealth fades it out and arms the next blow
        var assassin = Guardian<mob_living_basic_guardian_assassin>(r, bob);
        r.In(() => Waiting.NoSleep(assassin.toggle_modes()));
        r.Step(Second);
        Assert.True(r.In(() => Dm.truthy(assassin.has_status_effect(typeof(datum_status_effect_guardian_stealth)))));
        Assert.Equal(50, r.In(() => assassin.melee_damage_upper));
        Assert.Equal(15, r.In(() => assassin.alpha));
        r.In(() => qdel(assassin));

        // protector: the shield mode cuts its damage and slows it, with the coloured shield overlay
        var protector = Guardian<mob_living_basic_guardian_protector>(r, bob);
        r.In(() => Waiting.NoSleep(protector.toggle_modes()));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(protector.has_status_effect(typeof(datum_status_effect_protector_shield)))));
        Assert.Equal(0.05f, r.In(() => Dm.num(protector.physiology[BRUTE])), 3);
        Assert.Contains(r.In(() => Dm.each(protector.overlays).OfType<IDmAppearance>().ToList()), o => Dm.str(o.icon_state) == "shield-grey");
        r.In(() => qdel(protector));

        // ranged: a surveillance snare tells the summoner when Alice crosses it
        var ranged = Guardian<mob_living_basic_guardian_ranged>(r, bob);
        datum_action_cooldown_mob_cooldown_guardian_alarm_snare snare = r.In(() => Dm.each(ranged.actions).OfType<datum_action_cooldown_mob_cooldown_guardian_alarm_snare>().Single());
        r.In(() => Waiting.NoSleep(ranged.forceMove(locate(8, 4, 1))));
        r.In(() => Waiting.Detach(snare.Trigger()));
        r.Step(2);
        Assert.Single(r.In(() => Dm.each(snare.placed_snares).ToList()));
        r.In(() => Waiting.NoSleep(ranged.forceMove(locate(8, 6, 1))));
        r.In(() => Waiting.NoSleep(alice.Move(locate(7, 4, 1), EAST)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(alice.Move(locate(8, 4, 1), EAST)));
        r.Step(2);
        Assert.True(feed.Chat.Any(c => For(r, c.Target, bob) && c.Html.Contains("has crossed Test Room snare")), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        r.In(() => qdel(ranged));

        // support: a right-click heals, the beacon marks the floor
        r.In(() => Waiting.NoSleep(alice.adjust_brute_loss(30)));
        var support = Guardian<mob_living_basic_guardian_support>(r, bob);
        r.In(() => Waiting.NoSleep(alice.forceMove(locate(6, 4, 1))));
        r.In(() => Waiting.NoSleep(support.forceMove(locate(7, 4, 1))));
        r.Step(2);
        float hurt = r.In(() => Dm.num(alice.get_brute_loss()));
        r.In(() => Waiting.NoSleep(support.UnarmedAttack(alice, TRUE, Dm.list(Dm.kv(RIGHT_CLICK, "1")))));
        r.Step(Second);
        Assert.True(r.In(() => Dm.num(alice.get_brute_loss())) < hurt, "healed");
        datum_action_cooldown_mob_cooldown_guardian_bluespace_beacon beacon = r.In(() => Dm.each(support.actions).OfType<datum_action_cooldown_mob_cooldown_guardian_bluespace_beacon>().Single());
        // the beacon wants a floor (the toy room's indestructible ground is none): an iron tile
        r.In(() => Waiting.NoSleep(((turf)locate(9, 6, 1)).ChangeTurf(typeof(turf_open_floor_iron))));
        r.In(() => Waiting.NoSleep(support.forceMove(locate(9, 6, 1))));
        r.In(() => Waiting.Detach(beacon.Trigger()));
        r.Step(2);
        Assert.NotNull(r.In(() => beacon.beacon));
        r.In(() => qdel(support));

        // explosive: a planted bomb goes off on the next hand that touches it
        var explosive = Guardian<mob_living_basic_guardian_explosive>(r, bob);
        obj_item_toy_crayon_red crayon = Place<obj_item_toy_crayon_red>(r, 5, 4);
        r.In(() => Waiting.NoSleep(explosive.bomb.Trigger(null, null, crayon)));
        r.Step(2);
        Assert.NotNull(r.In(() => crayon.GetComponent(typeof(datum_component_direct_explosive_trap))));
        float aliceBefore = r.In(() => Dm.num(alice.get_brute_loss()) + Dm.num(alice.get_fire_loss()));
        r.In(() => Waiting.NoSleep(alice.forceMove(locate(6, 4, 1))));
        r.In(() => Waiting.NoSleep(crayon.attack_hand(alice)));
        r.Step(2);
        Assert.True(feed.Any("was boobytrapped!"));
        Assert.True(r.In(() => Dm.num(alice.get_brute_loss()) + Dm.num(alice.get_fire_loss())) > aliceBefore);
        r.In(() => qdel(explosive));

        // lightning: a punch chains Alice
        var lightning = Guardian<mob_living_basic_guardian_lightning>(r, bob);
        r.In(() => Waiting.NoSleep(lightning.forceMove(locate(5, 4, 1))));
        r.In(() => Waiting.NoSleep(lightning.melee_attack(alice, ignore_cooldown: TRUE)));
        r.Step(2);
        Assert.True(r.In(() => Dm.@in(alice, lightning.enemy_chains)));
        Assert.NotNull(r.In(() => lightning.summoner_chain));
        r.In(() => qdel(lightning));

        // gravitokinetic: a punch makes Alice heavy
        var gravity = Guardian<mob_living_basic_guardian_gravitokinetic>(r, bob);
        r.In(() => Waiting.NoSleep(gravity.forceMove(locate(5, 4, 1))));
        r.In(() => Waiting.NoSleep(gravity.melee_attack(alice, ignore_cooldown: TRUE)));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(gravity.gravity_targets[alice])));
        Assert.True(feed.Any("Everything feels really heavy!"));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/gravhit.ogg");
        r.In(() => qdel(gravity));
        r.Step(2);

        // gaseous: the chosen gas streams out every life tick (the gas list's window goes to the guardian's player; this one has none,
        // so the end of tg's Activate is set as a pick of oxygen sets it)
        var gaseous = Guardian<mob_living_basic_guardian_gaseous>(r, bob);
        r.In(() => Waiting.NoSleep(((turf)locate(10, 6, 1)).ChangeTurf(typeof(turf_open_floor_iron))));
        r.Step(2);
        r.In(() => Waiting.NoSleep(gaseous.forceMove(locate(10, 6, 1))));
        float o2 = r.In(() => Dm.num(((turf_open)gaseous.loc).air.moles[typeof(datum_gas_oxygen)]));
        r.In(() =>
        {
            gaseous.gas.active_gas = typeof(datum_gas_oxygen);
            gaseous.gas.RegisterSignal(gaseous, COMSIG_LIVING_LIFE, nameof(datum_action_cooldown_mob_cooldown_expel_gas.on_life));
        });
        r.Step(5 * Second);
        Assert.True(r.In(() => Dm.num(((turf_open)gaseous.loc).air.moles[typeof(datum_gas_oxygen)])) > o2 + 10, "oxygen released: " + o2 + " -> " + r.In(() => Dm.num(((turf_open)gaseous.loc).air.moles[typeof(datum_gas_oxygen)])) + " " + string.Join("\n", r.Room.RuntimeText));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheChargerChargesTheDextrousHoldsAndStores()
    {
        var (r, feed, bob) = Room(7204);
        mob_living_carbon_human alice = r.Join(9, 4, "Alice");
        r.Step(Second);
        obj_item_toy_crayon_red held = r.In(() => Dm.@new<obj_item_toy_crayon_red>(alice.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(alice.put_in_hands(held)));

        // charger: the charge knocks Alice down and out of her hold
        var charger = Guardian<mob_living_basic_guardian_charger>(r, bob);
        datum_action_cooldown_mob_cooldown_charge_basic_charge_guardian charge = r.In(() => Dm.each(charger.actions).OfType<datum_action_cooldown_mob_cooldown_charge_basic_charge_guardian>().Single());
        r.In(() => Waiting.Detach(charge.Trigger(null, null, alice)));
        r.Step(2 * Second);
        Assert.False(r.In(() => Dm.truthy(alice.is_holding(held))), "dropped by the charge");
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/modsuit/loader_launch.ogg");
        r.In(() => qdel(charger));

        // dextrous: hands and a pocket; recalling drops what it holds and keeps what it stores
        var dextrous = Guardian<mob_living_basic_guardian_dextrous>(r, bob);
        obj_item_toy_crayon_red one = r.In(() => Dm.@new<obj_item_toy_crayon_red>(dextrous.loc));
        obj_item_toy_crayon_red two = r.In(() => Dm.@new<obj_item_toy_crayon_red>(dextrous.loc));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(dextrous.put_in_hands(one)))));
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(dextrous.put_in_hands(two)))));
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(dextrous.equip_to_slot(two, ITEM_SLOT_DEX_STORAGE)))));
        Assert.Same(two, r.In(() => dextrous.internal_storage));
        r.Step(Second + 1);
        r.In(() => Waiting.NoSleep(dextrous.recall()));
        r.Step(2);
        Assert.Same(two, r.In(() => dextrous.internal_storage));
        Assert.False(r.In(() => Dm.truthy(dextrous.is_holding(one))));
        Assert.True(r.In(() => HAS_TRAIT(dextrous, TRAIT_HANDS_BLOCKED)));
        r.AssertNoRuntimes();
    }
}
