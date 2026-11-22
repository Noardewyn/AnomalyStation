using System.Reflection;
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
/// Gaps package X5 (D333): the xenomorphs (code/modules/mob/living/carbon/alien/**, structures/aliens.dm, the nest, the xeno
/// antagonist) in a booted toy room. A facehugger leaps at a face and leaves an embryo; the embryo grows through its stages and a
/// ghost who signs up bursts out as the larva, a xenomorph of the hive; the larva grows and evolves into a caste that keeps its mind,
/// name number and organs; the castes' powers spend and regain plasma (the weeds, the acid, the neurotoxin); the hivemind reaches
/// only the hive; the queen's death strikes her children and her royal parasite makes a praetorian; aliens slash, pry airlocks, devour
/// and nest their prey; eggs grow and burst at a host.
/// </summary>
public class XenomorphsX5Tests
{
    private const int W = 17, H = 11;

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(W, H, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    /// <summary>A mob of type T on (x, y), joined by a player of that key (the host's join, as AudioRoom.Join).</summary>
    private static T JoinAs<T>(AudioRoom r, int x, int y, string key) where T : mob
    {
        T body = r.In(() => Dm.@new<T>(locate(x, y, 1)));
        r.Command(() =>
        {
            ClientSeam.KeyMob(body, key);
            ClientSeam.Connect(key);
        });
        r.Step(2);
        Assert.NotNull(r.In(() => body.client));
        return body;
    }

    private static mob_dead_observer Ghost(AudioRoom r, int x, int y, string key)
    {
        mob_living_carbon_human body = r.Join(x, y, key);
        client player = body.client;
        r.In(() => body.ghostize(TRUE));
        r.Step(2);
        return Assert.IsType<mob_dead_observer>(player.mob);
    }

    private static atom_movable_screen_alert_poll_alert AlertOf(AudioRoom r, mob who)
        => r.In(() => Dm.each(who.alerts).Select(k => who.alerts[k]).OfType<atom_movable_screen_alert_poll_alert>().SingleOrDefault());

    private static float Plasma(AudioRoom r, mob_living_carbon who) => r.In(() => Dm.num(who.getPlasma()));

    private static TAction Action<TAction>(AudioRoom r, mob who) where TAction : datum_action
        => r.In(() => Dm.locate<TAction>(who.actions));

    [Fact]
    public void EveryXenomorphTypeIsLive()
    {
        var notLive = new List<string>();
        string[] prefixes =
        {
            "/mob/living/carbon/alien", "/obj/item/organ/alien", "/obj/item/organ/body_egg", "/obj/item/clothing/mask/facehugger",
            "/obj/structure/alien", "/obj/structure/bed/nest", "/datum/action/cooldown/alien", "/datum/antagonist/xeno", "/datum/team/xeno",
            "/datum/hud/alien", "/datum/hud/larva", "/atom/movable/screen/alien", "/datum/saymode/xeno", "/obj/item/queen_promotion",
            "/obj/projectile/neurotoxin", "/datum/component/itempicky", "/datum/status_effect/nest_sustenance",
            "/datum/action/cooldown/mob_cooldown/sneak", "/obj/effect/spawner/xeno_egg_delivery", "/datum/objective/escape_captivity",
            "/datum/objective/advance_hive", "/datum/dynamic_ruleset/midround/from_ghosts/xenomorph", "/datum/blood_type/xeno",
            "/obj/item/organ/tongue/alien", "/obj/item/organ/stomach/alien", "/obj/item/organ/tail/xeno_queen",
            "/datum/bodypart_overlay/mutant/tail/xeno",
        };
        foreach (Type type in typeof(mob_living_carbon_alien).Assembly.GetTypes())
        {
            string path = type.GetCustomAttribute<DmPathAttribute>()?.Path;
            if (path == null || !prefixes.Any(p => path == p || path.StartsWith(p + "/", StringComparison.Ordinal)))
                continue;
            // the changeling's headslug egg comes with the changeling; the spider's sneak with the spiders
            if (path.Contains("changeling_egg") || path.Contains("sneak/spider"))
                continue;
            if (Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false))
                notLive.Add(path);
        }
        Assert.True(notLive.Count == 0, "inert: " + string.Join(", ", notLive));
    }

    [Fact]
    public void AFacehuggerLeapsAtAFaceAndLeavesAnEmbryo()
    {
        var (r, feed, bob) = Room(7501);
        var hugger = Place<obj_item_clothing_mask_facehugger>(r, 6, 5);
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(hugger.leap_to(bob)))), "the hugger leaps: " + string.Join(" | ", feed.Chat.Select(c => c.Html).TakeLast(5)) + string.Join("\n", r.Room.RuntimeText));
        Assert.Same(hugger, r.In(() => bob.wear_mask));
        Assert.True(feed.Any("leaps at your face"));
        // latched on: it goes idle and will not come off
        Assert.Equal("facehugger_inactive", r.In(() => hugger.icon_state));
        Assert.False(r.In(() => Dm.truthy(hugger.can_mob_unequip(bob))));
        Assert.True(feed.Any("is latched on too tight"));

        // within 10 to 15 seconds it violates the face, dies and leaves the embryo
        r.Step(16 * Second);
        var embryo = r.In(() => bob.get_organ_by_type(typeof(obj_item_organ_body_egg_alien_embryo)) as obj_item_organ_body_egg_alien_embryo);
        Assert.NotNull(embryo);
        Assert.Equal(1f, r.In(() => embryo.stage));
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_XENO_HOST)));
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_XENO_IMMUNE)));
        Assert.True(r.In(() => hugger.facehugger_state) == 3f, "dead: state " + r.In(() => hugger.facehugger_state) + " icon " + r.In(() => hugger.icon_state) + " " + string.Join(" | ", feed.Chat.Select(c => c.Html).TakeLast(6)) + string.Join("\n", r.Room.RuntimeText));
        Assert.Equal("facehugger_impregnated", r.In(() => hugger.icon_state));
        // (the hugger covers Bob's eyes: tint 3, so its visible messages do not reach him)
        // a host is immune to a second hugger
        var second = Place<obj_item_clothing_mask_facehugger>(r, 4, 5);
        Assert.False(r.In(() => Dm.truthy(second.valid_to_attach(bob))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEmbryoGrowsAndAGhostBurstsOutAsTheLarva()
    {
        var (r, feed, bob) = Room(7502);
        mob_dead_observer gina = Ghost(r, 10, 8, "Gina");
        r.In(() => Dm.append(ref gina.client.prefs.be_special, ROLE_ALIEN)); // Gina wants the alien role (the lobby's Antagonists page)
        var embryo = r.In(() => Dm.@new<obj_item_organ_body_egg_alien_embryo>(bob));
        r.Step(2);
        Assert.Same(bob, r.In(() => embryo.owner));
        // a minute a stage: the stages run faster for the test
        r.In(() =>
        {
            for (int i = 0; i < 5; i++)
                embryo.advance_embryo_stage();
        });
        Assert.Equal(6f, r.In(() => embryo.stage));
        Assert.Equal("Stage: 6/6", r.In(() => Dm.str(embryo.get_status_appendix(SCANPOWER_SUPER))));

        // the burst polls the ghosts for twenty seconds; Gina signs up through her alert
        r.In(() => Waiting.Detach(embryo.attempt_grow()));
        r.Step(2);
        var alert = AlertOf(r, gina);
        Assert.NotNull(alert);
        Click(r, gina, alert);
        r.Step(22 * Second);
        mob_living_carbon_alien_larva larva = r.In(() => Dm.each<mob_living_carbon_alien_larva>(GLOB.mob_living_list).SingleOrDefault());
        Assert.True(larva != null, "no larva: " + string.Join("\n", r.Room.RuntimeText));
        Assert.Equal("gina", r.In(() => larva.ckey));
        Assert.True(r.In(() => QDELETED(bob)), "the host is gibbed");
        Assert.True(feed.Any("bursts out of"));
        // Gina's larva is a xenomorph of the hive, with the hive's objective, a xenomorph's job
        Assert.NotNull(r.In(() => larva.mind.has_antag_datum(typeof(datum_antagonist_xeno))));
        Assert.Equal("Xenomorph", r.In(() => Dm.str(larva.mind.assigned_role.title)));
        Assert.True(feed.Any("You are an alien larva."));
        // it can move again after the burst's 0.6 seconds
        Assert.False(r.In(() => HAS_TRAIT(larva, TRAIT_IMMOBILIZED)));
        Assert.Equal("larva0", r.In(() => larva.icon_state));
    }

    [Fact]
    public void ALarvaGrowsAndEvolvesIntoACasteThatKeepsItsMindAndNumber()
    {
        var (r, feed, _) = Room(7503);
        var larva = JoinAs<mob_living_carbon_alien_larva>(r, 8, 5, "Lara");
        r.Step(Second);
        string larvaName = r.In(() => larva.name);
        Assert.StartsWith("alien larva (", larvaName);
        var evolve = Action<datum_action_cooldown_alien_larva_evolve>(r, larva);
        Assert.NotNull(evolve);
        Assert.False(r.In(() => Dm.truthy(Waiting.NoSleep(evolve.IsAvailable()))), "not grown yet");
        float grown = r.In(() => larva.amount_grown);
        r.Step(10 * Second);
        Assert.True(r.In(() => larva.amount_grown) > grown, "the larva grows");

        r.In(() =>
        {
            larva.amount_grown = XENOMORPH_MAX_GROWTH;
            Waiting.NoSleep(larva.update_icons());
        });
        Assert.Equal("larva2", r.In(() => larva.icon_state));
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(evolve.IsAvailable()))), "grown");

        // the evolution (the radial's pick): a drone with the larva's mind and number
        mob_living_carbon_alien_adult_drone drone = r.In(() => Dm.@new<mob_living_carbon_alien_adult_drone>(larva.loc));
        r.In(() => Waiting.NoSleep(larva.alien_evolve(drone)));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(larva)));
        Assert.Equal("lara", r.In(() => drone.ckey));
        Assert.Equal(larvaName.Replace("alien larva", "alien drone"), r.In(() => drone.name));
        Assert.NotNull(r.In(() => drone.get_organ_by_type(typeof(obj_item_organ_alien_resinspinner))));
        Assert.NotNull(r.In(() => drone.get_organ_by_type(typeof(obj_item_organ_alien_plasmavessel_large))));
        Assert.NotNull(Action<datum_action_cooldown_alien_make_structure_plant_weeds>(r, drone));
        Assert.NotNull(Action<datum_action_cooldown_alien_evolve_to_praetorian>(r, drone));
        Assert.True(feed.Any("You begin to evolve!"));
        Assert.Equal("aliend", r.In(() => drone.icon_state));
        // the adult alien's HUD: the plasma display and the queen sense
        Assert.NotNull(r.In(() => drone.hud_used?.screen_objects[HUD_ALIEN_PLASMA_DISPLAY]));
        Assert.NotNull(r.In(() => drone.hud_used?.screen_objects[HUD_ALIEN_QUEEN_FINDER]));
    }

    [Fact]
    public void WeedsSpreadFromTheirNodeAndHealAndFeedTheAliens()
    {
        var (r, _, _) = Room(7504);
        var drone = JoinAs<mob_living_carbon_alien_adult_drone>(r, 8, 5, "Dana");
        r.Step(Second);
        float before = Plasma(r, drone);
        var weeds = Action<datum_action_cooldown_alien_make_structure_plant_weeds>(r, drone);
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(weeds.PreActivate(drone)))));
        var node = r.In(() => Dm.locate<obj_structure_alien_weeds_node>(drone.loc));
        Assert.NotNull(node);
        Assert.Equal(before - 50, Plasma(r, drone), 1);
        Assert.Equal(4f, r.In(() => node.light_range)); // the blue glow
        // the node grows weeds around it within ten seconds (and SSobj's next fire)
        r.Step(13 * Second);
        int spread = r.In(() => Dm.each<obj_structure_alien_weeds>(range(1, node)).Count(w => w != node));
        Assert.True(spread > 0, "the weeds spread: growtime " + r.In(() => node.growtime) + " now " + r.In(() => world.time) + " processing " + r.In(() => Dm.@in(node, SSobj.processing))
            + " adjacent " + r.In(() => Dm.length(((turf)node.loc).get_atmos_adjacent_turfs())) + " parent " + r.In(() => node.parent_node == node) + string.Join("\n", r.Room.RuntimeText));
        // a hurt alien on weeds heals and regains plasma
        r.In(() => Waiting.NoSleep(drone.adjust_brute_loss(30)));
        float plasma = Plasma(r, drone);
        r.Step(6 * Second);
        Assert.True(r.In(() => Dm.num(drone.get_brute_loss())) < 30, "it heals on weeds");
        Assert.True(Plasma(r, drone) > plasma, "its plasma grows");
        // a second node on the same weeds is refused
        Assert.False(r.In(() => Dm.truthy(weeds.check_for_duplicate())));
    }

    [Fact]
    public void TheCorrosiveAcidMeltsAnObjectAndTheNeurotoxinTiresAPerson()
    {
        var (r, feed, bob) = Room(7505);
        var drone = JoinAs<mob_living_carbon_alien_adult_drone>(r, 8, 5, "Dana");
        r.Step(Second);
        var crate = Place<obj_structure_closet_crate>(r, 9, 5);
        var acid = Action<datum_action_cooldown_alien_acid_corrosion>(r, drone);
        r.In(() => Waiting.NoSleep(drone.adjustPlasma(500)));
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(acid.PreActivate(crate)))));
        Assert.NotNull(r.In(() => crate.GetComponent(typeof(datum_component_acid))));
        Assert.True(feed.Any("You vomit globs of acid over"));
        // the acid does not work on creatures
        Assert.False(r.In(() => Dm.truthy(Waiting.NoSleep(acid.PreActivate(bob)))));

        var sentinel = JoinAs<mob_living_carbon_alien_adult_sentinel>(r, 8, 7, "Sena");
        r.Step(Second);
        var spit = Action<datum_action_cooldown_alien_acid_neurotoxin>(r, sentinel);
        Assert.NotNull(spit);
        r.In(() => Waiting.NoSleep(spit.set_click_ability(sentinel)));
        Assert.Equal("alien_neurotoxin_1", r.In(() => spit.button_icon_state));
        Assert.Equal(sentinel.click_intercept, (datum)spit);
        float plasma = Plasma(r, sentinel);
        r.In(() => Waiting.NoSleep(spit.InterceptClickOn(sentinel, "icon-x=16;icon-y=16;left=1", bob)));
        r.Step(2 * Second);
        Assert.True(r.In(() => Dm.num(bob.get_stamina_loss())) > 0, "the neurotoxin hits");
        Assert.True(Plasma(r, sentinel) < plasma - 40, "the spit costs 50 plasma (the vessel regains a little meanwhile)");
        Assert.True(feed.Any("spits neurotoxin!"));
        // the sentinel's sneak fades it
        var sneak = Action<datum_action_cooldown_mob_cooldown_sneak_alien>(r, sentinel);
        r.In(() => Waiting.NoSleep(sneak.Activate()));
        Assert.True(r.In(() => HAS_TRAIT(sentinel, TRAIT_SNEAK)));
    }

    [Fact]
    public void TheHivemindReachesOnlyTheHive()
    {
        var (r, feed, bob) = Room(7506);
        var drone = JoinAs<mob_living_carbon_alien_adult_drone>(r, 8, 5, "Dana");
        var hunter = JoinAs<mob_living_carbon_alien_adult_hunter>(r, 10, 5, "Hank");
        r.Step(Second);
        r.In(() => drone.alien_talk("The host is in medbay"));
        r.Step(2);
        Assert.Contains(feed.Chat, c => (c.Target == hunter || c.Target == r.In(() => hunter.client)) && c.Html.Contains("Hivemind") && c.Html.Contains("The host is in medbay"));
        Assert.DoesNotContain(feed.Chat, c => (c.Target == bob || c.Target == r.In(() => bob.client)) && c.Html.Contains("The host is in medbay"));
        // the say mode of ":a" is the xenos' (tg's handle_message subtype takes the key), open to the hive only
        Assert.True(r.In(() => Dm.truthy(drone.hivecheck())));
        Assert.False(r.In(() => Dm.truthy(bob.hivecheck())));
        Assert.True(r.In(() => SSradio.get_available_say_mode(drone, MODE_KEY_ALIEN)) is datum_saymode_xeno_handle_message,
            "the say modes: " + r.In(() => string.Join(", ", Dm.each(SSradio.saymodes).Select(k => k + "=" + (SSradio.saymodes[k] as datum)?.GetType().Name)))
            + " | written: " + r.In(() => string.Join(", ", Dm.each(subtypesof_written(typeof(datum_saymode))).Select(t => ((Type)t).Name))));
        Assert.Null(r.In(() => SSradio.get_available_say_mode(bob, MODE_KEY_ALIEN)));
        r.In(() => Waiting.NoSleep(hunter.say(":a Flank them")));
        r.Step(2);
        Assert.Contains(feed.Chat, c => (c.Target == drone || c.Target == r.In(() => drone.client)) && c.Html.Contains("Hivemind") && c.Html.Contains("Flank them"));
        Assert.DoesNotContain(feed.Chat, c => (c.Target == bob || c.Target == r.In(() => bob.client)) && c.Html.Contains("Flank them"));
        // both are xenomorphs of one hive
        var dTeam = r.In(() => (drone.mind.has_antag_datum(typeof(datum_antagonist_xeno)) as datum_antagonist_xeno).get_team());
        var hTeam = r.In(() => (hunter.mind.has_antag_datum(typeof(datum_antagonist_xeno)) as datum_antagonist_xeno).get_team());
        Assert.NotNull(dTeam);
        Assert.Same(dTeam, hTeam);
    }

    [Fact]
    public void TheQueensDeathStrikesHerChildrenAndHerParasiteMakesAPraetorian()
    {
        var (r, feed, _) = Room(7507);
        var queen = JoinAs<mob_living_carbon_alien_adult_royal_queen>(r, 6, 5, "Quinn");
        var drone = JoinAs<mob_living_carbon_alien_adult_drone>(r, 7, 5, "Dana");
        r.Step(Second);
        Assert.Equal("alien queen", r.In(() => queen.name.Split(" (")[0]));
        // the royal parasite: the promotion takes 500 plasma
        r.In(() => Waiting.NoSleep(queen.adjustPlasma(500)));
        var promote = Action<datum_action_cooldown_alien_promote>(r, queen);
        Assert.Equal("Create Royal Parasite (500P)", r.In(() => promote.name));
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(promote.PreActivate(queen)))));
        var parasite = r.In(() => Dm.locate<obj_item_queen_promotion>(queen.held_items));
        Assert.NotNull(parasite);
        r.In(() => Waiting.NoSleep(parasite.attack__proc(drone, queen)));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(drone)));
        mob_living_carbon_alien_adult_royal_praetorian prae = r.In(() => Dm.each<mob_living_carbon_alien_adult_royal_praetorian>(GLOB.mob_living_list).Single());
        Assert.Equal("dana", r.In(() => prae.ckey));
        Assert.True(feed.Any("You have promoted"));

        // the queen dies: the hive loses its hivemind for a while
        r.In(() => Waiting.NoSleep(queen.death()));
        r.Step(2);
        var node = r.In(() => prae.get_organ_by_type(typeof(obj_item_organ_alien_hivenode)) as obj_item_organ_alien_hivenode);
        Assert.True(r.In(() => Dm.truthy(node.recent_queen_death)));
        Assert.False(r.In(() => Dm.truthy(prae.hivecheck())));
        Assert.True(feed.Any("Your Queen has been struck down!"));
        Assert.NotNull(r.In(() => prae.alerts[ALERT_XENO_NOQUEEN]));
        r.Step(241 * Second);
        Assert.False(r.In(() => Dm.truthy(node.recent_queen_death)));
        Assert.True(feed.Any("The pain of the queen's death is easing."));
    }

    [Fact]
    public void AnAlienSlashesAPersonPriesAnAirlockAndDevoursItsPrey()
    {
        var (r, feed, bob) = Room(7508);
        var hunter = JoinAs<mob_living_carbon_alien_adult_hunter>(r, 6, 5, "Hank");
        r.Step(Second);
        r.In(() => hunter.combat_mode = TRUE);
        float health = r.In(() => bob.health);
        // a hit or a lunge: tg's 90 % chance; three slashes land one at least
        for (int i = 0; i < 3; i++)
            r.In(() => Waiting.NoSleep(bob.attack_alien(hunter, Dm.list())));
        Assert.True(r.In(() => bob.health) < health, "the slashes hurt");
        Assert.True(feed.Any("slashes at you!") || feed.Any("lunges at you!"));

        // an unpowered airlock pries open in half a second
        var airlock = Place<obj_machinery_door_airlock>(r, 6, 7);
        r.In(() => hunter.combat_mode = FALSE);
        r.In(() => Waiting.Detach(airlock.attack_alien(hunter, Dm.list())));
        r.Step(7 * Second); // a powered airlock takes five seconds
        Assert.False(r.In(() => Dm.truthy(airlock.density)), "the airlock is open");
        Assert.True(feed.Any("You begin digging your claws into"));

        // devouring: an aggressive grab, facing the prey, thirteen and a half seconds
        var drone = JoinAs<mob_living_carbon_alien_adult_drone>(r, 4, 5, "Dana");
        r.Step(Second);
        r.In(() =>
        {
            Waiting.NoSleep(drone.start_pulling(bob, GRAB_AGGRESSIVE));
            Waiting.NoSleep(drone.setGrabState(GRAB_AGGRESSIVE));
            Waiting.NoSleep(drone.setDir(EAST));
        });
        Assert.True(r.In(() => Dm.truthy(drone.can_consume(bob))));
        r.In(() => Waiting.Detach(drone.devour_lad(bob)));
        r.Step(15 * Second);
        var stomach = r.In(() => drone.get_organ_slot(ORGAN_SLOT_STOMACH) as obj_item_organ_stomach_alien);
        Assert.True(r.In(() => Dm.@in(bob, stomach.stomach_contents)), "Bob is devoured: " + string.Join("\n", r.Room.RuntimeText));
        Assert.True(feed.Any("devours you!"));
        // and regurgitated
        var barf = Action<datum_action_cooldown_alien_regurgitate>(r, drone);
        r.In(() => Waiting.NoSleep(barf.Activate()));
        r.Step(2 * Second);
        Assert.False(r.In(() => Dm.@in(bob, stomach.stomach_contents)));
        Assert.IsAssignableFrom<turf>(r.In(() => bob.loc));
    }

    [Fact]
    public void AnEggGrowsAndBurstsAtAHostAndTheNestSustainsTheHost()
    {
        var (r, feed, bob) = Room(7509);
        var egg = Place<obj_structure_alien_egg_grown>(r, 9, 5);
        Assert.Equal("egg", r.In(() => egg.icon_state));
        var hugger = r.In(() => egg.child);
        Assert.NotNull(hugger);
        // Bob steps up to it: it opens and drops its hugger (tg's finish_bursting moves the hugger out first, and the egg's Exited
        // forgets its child, so the leap of :434-437 never runs, as in tg); stepping onto the hugger's tile, Bob is leapt at
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(8, 5, 1))));
        r.Step(3 * Second);
        Assert.Equal("burst", r.In(() => egg.status));
        Assert.Equal("egg_hatched", r.In(() => egg.icon_state));
        Assert.Same(r.In(() => locate(9, 5, 1)), r.In(() => hugger.loc));
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(9, 5, 1))));
        r.Step(2);
        Assert.Same(hugger, r.In(() => bob.wear_mask));

        // a drone nests him: the host is sustained
        var drone = JoinAs<mob_living_carbon_alien_adult_drone>(r, 8, 5, "Dana");
        var nest = Place<obj_structure_bed_nest>(r, 9, 5);
        r.Step(Second);
        r.In(() => Waiting.NoSleep(nest.user_buckle_mob(bob, drone)));
        Assert.Same(nest, r.In(() => bob.buckled));
        Assert.True(feed.Any("secretes a thick vile goo"));
        Assert.NotNull(r.In(() => bob.has_status_effect(typeof(datum_status_effect_nest_sustenance))));
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_HANDS_BLOCKED)));
        // a growing egg grows in 90 to 150 seconds
        var growing = Place<obj_structure_alien_egg>(r, 12, 8);
        Assert.Equal("egg_growing", r.In(() => growing.icon_state));
        r.Step(151 * Second);
        Assert.Equal("grown", r.In(() => growing.status));
        Assert.Equal("egg", r.In(() => growing.icon_state));
    }

    [Fact]
    public void AliensBreathePlasmaShapeResinAndTheLarvaHides()
    {
        var (r, feed, _) = Room(7511);
        var drone = JoinAs<mob_living_carbon_alien_adult_drone>(r, 8, 5, "Dana");
        r.Step(Second);
        // a breath of plasma becomes plasma in the vessel and oxygen out
        r.In(() => Waiting.NoSleep(drone.adjustPlasma(-200)));
        float plasma = Plasma(r, drone);
        datum_gas_mixture breath = r.In(() =>
        {
            var mix = Dm.@new<datum_gas_mixture>(BREATH_VOLUME);
            mix.assert_gases(typeof(datum_gas_plasma), typeof(datum_gas_oxygen));
            mix.moles[typeof(datum_gas_plasma)] = 0.1f;
            mix.temperature = T20C;
            return mix;
        });
        r.In(() => Waiting.NoSleep(drone.check_breath(breath)));
        Assert.Equal(plasma + 25, Plasma(r, drone), 1);
        Assert.NotNull(r.In(() => drone.alerts[ALERT_XENO_PLASMA]));
        Assert.Equal(0f, r.In(() => Dm.num(breath.moles[typeof(datum_gas_plasma)])), 3);
        Assert.Equal(0.1f, r.In(() => Dm.num(breath.moles[typeof(datum_gas_oxygen)])), 3);

        // the resin: one shape a tile; a wall blocks and is airtight, a membrane lets light through
        var resin = Action<datum_action_cooldown_alien_make_structure_resin>(r, drone);
        Assert.NotNull(resin);
        Assert.True(r.In(() => Dm.truthy(resin.check_for_duplicate())));
        var wall = Place<obj_structure_alien_resin_wall>(r, 8, 5);
        Assert.False(r.In(() => Dm.truthy(resin.check_for_duplicate())));
        Assert.True(feed.Any("There is already a resin structure there!"));
        Assert.True(r.In(() => Dm.truthy(wall.density) && Dm.truthy(wall.opacity)));
        var membrane = Place<obj_structure_alien_resin_membrane>(r, 9, 5);
        Assert.False(r.In(() => Dm.truthy(membrane.opacity)));
        Assert.Equal(1f, r.In(() => Dm.num(wall.block_superconductivity())));

        // the larva hides under things (its layer drops) and peeks up again
        var larva = JoinAs<mob_living_carbon_alien_larva>(r, 11, 5, "Lara");
        r.Step(Second);
        var hide = Action<datum_action_cooldown_alien_hide>(r, larva);
        float layer = r.In(() => larva.layer);
        r.In(() => Waiting.NoSleep(hide.Activate()));
        Assert.Equal(ABOVE_NORMAL_TURF_LAYER, r.In(() => larva.layer), 2);
        Assert.True(feed.Any("You are now hiding."));
        r.In(() => Waiting.NoSleep(hide.Activate()));
        Assert.Equal(layer, r.In(() => larva.layer), 2);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheInfestationRulesetMakesALarvaAndAnnouncesTheLifesigns()
    {
        var (r, _, _) = Room(7510);
        var ruleset = r.In(() => Dm.@new<datum_dynamic_ruleset_midround_from_ghosts_xenomorph>(Dm.list()));
        Assert.True(r.In(() => Dm.num(ruleset.max_antag_cap)) is 1f or 2f);
        // no vent network of twenty machines in the toy room: tg's ruleset cannot be picked
        Assert.False(r.In(() => Dm.truthy(ruleset.can_be_selected())));
        Assert.IsType<mob_living_carbon_alien_larva>(r.In(() => ruleset.create_ruleset_body()));
        Assert.False(Attribute.IsDefined(typeof(datum_dynamic_ruleset_midround_from_ghosts_xenomorph), typeof(DmInertAttribute), inherit: false));
    }
}
