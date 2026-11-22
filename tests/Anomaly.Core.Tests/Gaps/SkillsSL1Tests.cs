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
/// Gaps package SL1 (D333), in a booted toy room with Bob as a player: the skills (experience, levels and tg's level messages; a drilled
/// rock trains mining and the level speeds it; a punching bag trains athletics and exercises; soap trains cleaning; climbing trains
/// athletics), the skill station implanting, switching and extracting a chip from its SkillStation window, the musical chip's singing,
/// the legendary skill's cape sent by pod and worn only by the worthy, the sign language and origami books, and the skill tracker's
/// and the admin skill panel's data.
/// </summary>
public class SkillsSL1Tests
{
    private static string Diag(Feed feed, AudioRoom r) => "chat: " + string.Join(" | ", feed.Chat.Select(c => c.Html)) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        r.In(() => Assert.NotNull(bob.mind));
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T item = r.In(() => Dm.@new(typeof(T), who.loc) as T);
        r.Step(2);
        r.In(() =>
        {
            foreach (object held in Dm.each(who.held_items.Copy()))
                if (held is obj_item old && old != item)
                    qdel(old);
            Waiting.NoSleep(who.put_in_hands(item));
        });
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    /// <summary>CentCom's holding area for pods in transit (tg's supplypod_temp_holding), which the toy room lacks: its corner turf.</summary>
    private static void HoldingArea(AudioRoom r) => r.In(() =>
    {
        var lane = GLOB.areas_by_type[typeof(area_centcom_central_command_areas_supplypod_supplypod_temp_holding)] as area
            ?? Dm.@new<area_centcom_central_command_areas_supplypod_supplypod_temp_holding>();
        set_turf_to_area(locate(1, 1, 1), lane);
    });

    private static float Exp(AudioRoom r, mob who, Type skill) => r.In(() => Dm.num(who.mind.get_skill_exp(skill)));

    private static float Level(AudioRoom r, mob who, Type skill) => r.In(() => Dm.num(who.mind.get_skill_level(skill)));

    [Fact]
    public void ExperienceRaisesAndLowersLevelsWithTgsMessages()
    {
        var (r, feed, bob) = Room(9601);
        Assert.Equal((float)SKILL_LEVEL_NONE, Level(r, bob, typeof(datum_skill_mining)));
        r.In(() => Assert.Equal(0f, Dm.num(bob.mind.exp_needed_to_level_up(typeof(datum_skill_mining))) - SKILL_EXP_NOVICE));

        // 100 experience is a novice: tg's level up line
        r.In(() => bob.mind.adjust_experience(typeof(datum_skill_mining), SKILL_EXP_NOVICE));
        r.Step(2);
        Assert.Equal((float)SKILL_LEVEL_NOVICE, Level(r, bob, typeof(datum_skill_mining)));
        Assert.True(feed.Any("I'm starting to figure out what Mining really is!"), Diag(feed, r));
        r.In(() => Assert.Equal("Novice", Dm.str(bob.mind.get_skill_level_name(typeof(datum_skill_mining)))));

        // a set level: master, with its message; then down to apprentice, with the master's level down line
        r.In(() => bob.mind.set_level(typeof(datum_skill_mining), SKILL_LEVEL_MASTER));
        r.Step(2);
        Assert.Equal((float)SKILL_LEVEL_MASTER, Level(r, bob, typeof(datum_skill_mining)));
        Assert.Equal((float)SKILL_EXP_MASTER, Exp(r, bob, typeof(datum_skill_mining)));
        Assert.True(feed.Any("I now consider myself a master Miner."), Diag(feed, r));
        r.In(() => bob.mind.adjust_experience(typeof(datum_skill_mining), -(SKILL_EXP_MASTER - SKILL_EXP_APPRENTICE)));
        r.Step(2);
        Assert.Equal((float)SKILL_LEVEL_APPRENTICE, Level(r, bob, typeof(datum_skill_mining)));
        Assert.True(feed.Any("I feel like I'm losing my mastery of Mining."), Diag(feed, r));

        // experience never goes below zero; a silent change says nothing
        int lines = feed.Chat.Count;
        r.In(() => bob.mind.adjust_experience(typeof(datum_skill_cleaning), SKILL_EXP_JOURNEYMAN, silent: TRUE));
        r.In(() => bob.mind.adjust_experience(typeof(datum_skill_gaming), -50));
        r.Step(2);
        Assert.Equal((float)SKILL_LEVEL_JOURNEYMAN, Level(r, bob, typeof(datum_skill_cleaning)));
        Assert.Equal(0f, Exp(r, bob, typeof(datum_skill_gaming)));
        Assert.Equal(lines, feed.Chat.Count);

        // print_levels lists the skills above none
        r.In(() => bob.mind.print_levels(bob));
        r.Step(2);
        Assert.True(feed.Any("Mining - Apprentice"), Diag(feed, r));
        Assert.True(feed.Any("Cleaning - Journeyman"), Diag(feed, r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADrilledRockTrainsMiningAndTheLevelSpeedsMining()
    {
        var (r, feed, bob) = Room(9602);
        turf_closed_mineral rock = r.In(() => (turf_closed_mineral)Waiting.NoSleep(((turf)locate(5, 4, 1)).ChangeTurf(typeof(turf_closed_mineral))));
        r.Step(Second);
        float expected = r.In(() => rock.mineral_type != null && rock.mineral_amt > 0
            ? Dm.initial((Type)rock.mineral_type, (obj_item_stack_ore p) => p.mine_experience) * rock.mineral_amt
            : 4f);
        r.In(() => Waiting.NoSleep(rock.gets_drilled(bob, 1)));
        r.Step(4);
        Assert.Equal(expected, Exp(r, bob, typeof(datum_skill_mining)), 3);
        r.In(() => Assert.IsAssignableFrom<turf_open>(locate(5, 4, 1)));

        // the speed modifier follows the level (tg's mining list: 1 at none, 0.6 at master)
        r.In(() => Assert.Equal(1f, Dm.num(bob.mind.get_skill_modifier(typeof(datum_skill_mining), SKILL_SPEED_MODIFIER)), 3));
        r.In(() => bob.mind.set_level(typeof(datum_skill_mining), SKILL_LEVEL_MASTER, silent: TRUE));
        r.In(() => Assert.Equal(0.6f, Dm.num(bob.mind.get_skill_modifier(typeof(datum_skill_mining), SKILL_SPEED_MODIFIER)), 3));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APunchingBagTrainsAthleticsAndExercises()
    {
        var (r, feed, bob) = Room(9603);
        var bag = Place<obj_structure_punching_bag>(r, 5, 4);
        r.In(() => Assert.Null(bob.has_status_effect(typeof(datum_status_effect_exercised))));
        Click(r, bob, bag);
        r.Step(4);
        Assert.True(Exp(r, bob, typeof(datum_skill_athletics)) > 0, Diag(feed, r));
        r.In(() => Assert.NotNull(bob.has_status_effect(typeof(datum_status_effect_exercised))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void SoapCleaningADecalTrainsCleaning()
    {
        var (r, feed, bob) = Room(9604);
        var blood = Place<obj_effect_decal_cleanable_blood>(r, 5, 4);
        Hold<obj_item_soap>(r, bob);
        Click(r, bob, blood);
        r.Step(10 * Second);
        r.In(() => Assert.True(QDELETED(blood), Diag(feed, r)));
        Assert.True(Exp(r, bob, typeof(datum_skill_cleaning)) > 0, Diag(feed, r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ClimbingATableTrainsAthletics()
    {
        var (r, feed, bob) = Room(9605);
        var table = Place<obj_structure_table>(r, 5, 4);
        r.Command(() => ClientSeam.Drop(bob.client, bob, table, "left=1;button=left;drag=left", "left=1;button=left;drag=left", 5));
        r.Step(5 * Second);
        r.In(() => Assert.Same(table.loc, bob.loc));
        // ATHLETICS_SKILL_MISC_EXP over (fitness_level || 1), fitness 0 at none
        Assert.Equal((float)ATHLETICS_SKILL_MISC_EXP, Exp(r, bob, typeof(datum_skill_athletics)), 3);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSkillStationImplantsSwitchesAndExtractsAChip()
    {
        var (r, feed, bob) = Room(9606);
        var station = Place<obj_machinery_skill_station>(r, 5, 4);
        r.In(() => Assert.Equal("implantchair_open", station.icon_state));
        var chip = Hold<obj_item_skillchip_musical>(r, bob);

        // the chip goes into the open station; Bob steps in and shuts it: the window opens for him
        Click(r, bob, station);
        r.In(() => Assert.Same(chip, station.inserted_skillchip));
        r.In(() => Waiting.NoSleep(bob.forceMove(station.loc)));
        r.Step(2);
        Take(bob);
        Click(r, bob, station);
        r.Step(4);
        JsonElement payload = LastUpdate(Take(bob), out string window);
        Assert.Equal("SkillStation", InterfaceOf(payload));
        r.In(() => Assert.Same(bob, station.occupant));
        r.In(() => Assert.Equal("implantchair_occupied", station.icon_state));
        JsonElement data = payload.GetProperty("data");
        Assert.Equal(1, data.GetProperty("skillchip_ready").GetInt32());
        Assert.Equal("Memory of a Musical", data.GetProperty("skill_name").GetString());

        // implant: fifteen seconds of work, then the chip is in Bob's brain
        Act(r, bob, window, "implant");
        r.In(() => Assert.True(Dm.truthy(station.working)));
        r.Step(16 * Second);
        Assert.True(feed.Any("Operation complete!"), Diag(feed, r));
        obj_item_organ_brain brain = r.In(() => (obj_item_organ_brain)bob.get_organ_slot(ORGAN_SLOT_BRAIN));
        r.In(() => Assert.Contains(chip, Dm.each<obj_item_skillchip>(brain.skillchips)));
        data = Data(r, bob, out window);
        Assert.Equal(1, data.GetProperty("current").GetArrayLength());
        string chipRef = data.GetProperty("current")[0].GetProperty("ref").GetString();

        // switched on from the window: Bob now sings
        Act(r, bob, window, "toggle_activate", ("ref", chipRef));
        r.In(() => Assert.True(Dm.truthy(chip.active), Diag(feed, r)));
        r.In(() => Waiting.Detach(bob.say("Hello there.")));
        r.Step(4);
        Assert.True(feed.Any("theeeereeee!!"), Diag(feed, r));

        // extracted after its cooldown (cut short here): fifteen seconds and the chip leaves the brain
        r.In(() => chip.chip_cooldown = 0);
        Act(r, bob, window, "remove", ("ref", chipRef));
        r.Step(16 * Second);
        r.In(() => Assert.DoesNotContain(chip, Dm.each<obj_item_skillchip>(brain.skillchips)));
        r.In(() => Assert.False(QDELETED(chip)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALegendarySkillSendsItsCapeByPodAndOnlyTheWorthyWearIt()
    {
        var (r, feed, bob) = Room(9607);
        HoldingArea(r);
        r.In(() => bob.mind.set_level(typeof(datum_skill_mining), SKILL_LEVEL_LEGENDARY));
        r.Step(2);
        Assert.True(feed.Any("I'm finally able to consider myself a legendary Miner!"), Diag(feed, r));
        r.In(() => Waiting.Detach(((datum_skill)GetSkillRef(typeof(datum_skill_mining))).try_skill_reward(bob.mind, SKILL_LEVEL_LEGENDARY)));
        r.Step(2);
        Assert.True(feed.Any("It seems they are sending me a status symbol to commemorate my abilities."), Diag(feed, r));
        r.Step(30 * Second);
        var cape = r.In(() => Dm.each<obj_item_clothing_neck_cloak_skill_reward_mining>(range(2, bob)).FirstOrDefault());
        Assert.True(cape != null, Diag(feed, r));
        r.In(() => Assert.Contains(typeof(datum_skill_mining), Dm.each<Type>(bob.mind.skills_rewarded)));

        // a second request is refused
        r.In(() => Waiting.Detach(((datum_skill)GetSkillRef(typeof(datum_skill_mining))).try_skill_reward(bob.mind, SKILL_LEVEL_LEGENDARY)));
        r.Step(2);
        Assert.True(feed.Any("won't send me another status symbol"), Diag(feed, r));

        // Alice, no miner, cannot wear it; Bob can
        mob_living_carbon_human alice = r.Join(6, 4, "Alice");
        r.Step(Second);
        r.In(() => Waiting.NoSleep(alice.equip_to_slot_if_possible(cape, ITEM_SLOT_NECK)));
        r.Step(2);
        r.In(() => Assert.NotSame(cape, alice.wear_neck));
        Assert.True(feed.Any("You feel completely and utterly unworthy to even touch"), Diag(feed, r));
        r.In(() => Waiting.NoSleep(bob.equip_to_slot_if_possible(cape, ITEM_SLOT_NECK)));
        r.Step(2);
        r.In(() => Assert.Same(cape, bob.wear_neck));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSignLanguageAndOrigamiBooksTeachTheirArts()
    {
        var (r, feed, bob) = Room(9608);
        var book = Hold<obj_item_book_granter_sign_language>(r, bob);
        r.In(() => Waiting.Detach(book.attack_self(bob)));
        r.Step(30 * Second);
        var signing = r.In(() => bob.GetComponent(typeof(datum_component_sign_language)) as datum_component_sign_language);
        Assert.True(signing != null, Diag(feed, r));

        // the toggle: signing changes the say verbs and the bubble
        r.In(() => Waiting.NoSleep(signing.linked_action.Activate()));
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(bob, TRAIT_SIGN_LANG));
            Assert.Equal("signs", bob.verb_ask);
            Assert.Equal("signlang", bob.bubble_icon);
        });
        r.In(() => Waiting.NoSleep(signing.linked_action.Deactivate()));
        r.In(() => Assert.Equal("asks", bob.verb_ask));

        var origami = Hold<obj_item_book_granter_action_origami>(r, bob);
        r.In(() => Waiting.Detach(origami.attack_self(bob)));
        r.Step(30 * Second);
        var fold = r.In(() => Dm.locate<datum_action_innate_origami>(bob.actions) as datum_action_innate_origami);
        Assert.True(fold != null, Diag(feed, r));
        r.In(() => Waiting.NoSleep(fold.Activate()));
        r.In(() => Assert.True(HAS_TRAIT(bob, TRAIT_PAPER_MASTER)));
        Assert.True(feed.Any("You will now fold origami planes."), Diag(feed, r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSkillTrackerAndTheAdminPanelReportTheSkills()
    {
        var (r, feed, bob) = Room(9609);
        r.In(() => bob.mind.set_level(typeof(datum_skill_cleaning), SKILL_LEVEL_LEGENDARY, silent: TRUE));
        r.In(() => bob.mind.adjust_experience(typeof(datum_skill_mining), 175, silent: TRUE));
        DmList tracker = r.In(() =>
        {
            var program = Dm.@new<datum_computer_file_program_skill_tracker>();
            return Waiting.NoSleep(program.ui_data(bob)) as DmList;
        });
        r.In(() =>
        {
            DmList skills = tracker["skills"] as DmList;
            Assert.Equal(length(GLOB.skill_types), length(skills));
            DmList cleaning = Dm.each<DmList>(skills).Single(s => Dm.str(s["name"]) == "Cleaning");
            Assert.Equal("LEGENDARY", Dm.str(cleaning["lvl_name"]));
            Assert.Equal(1f, Dm.num(cleaning["reward"]));
            DmList mining = Dm.each<DmList>(skills).Single(s => Dm.str(s["name"]) == "Mining");
            Assert.Equal("NOVICE", Dm.str(mining["lvl_name"]));
            Assert.Equal(0.5f, Dm.num(mining["progress_percent"]), 3); // 75 of the 150 between novice and apprentice
        });

        // the admin panel: one entry per skill, with the experience the UI shows
        DmList panel = r.In(() =>
        {
            var sp = Waiting.NoSleep(Dm.new_async<datum_skill_panel>(bob.client, bob.mind));
            return Waiting.NoSleep(sp.ui_data(bob)) as DmList;
        });
        r.In(() =>
        {
            DmList skills = panel["skills"] as DmList;
            Assert.Equal(length(GLOB.skill_types), length(skills));
            DmList mining = Dm.each<DmList>(skills).Single(s => Dm.str(s["name"]) == "Mining");
            Assert.Equal(175f, Dm.num(mining["exp"]));
            Assert.Equal("NOVICE", Dm.str(mining["lvl"]));
            Assert.Equal(150f, Dm.num(mining["exp_req"]));
            Assert.Equal((float)SKILL_EXP_LEGENDARY, Dm.num(mining["max_exp"]));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EverySkillHasItsSingletonAndEveryMindItsEntry()
    {
        var (r, feed, bob) = Room(9610);
        r.In(() =>
        {
            Assert.NotNull(SSskills);
            Assert.Equal(length(GLOB.skill_types), length(SSskills.all_skills));
            foreach (object type in Dm.each(GLOB.skill_types))
                Assert.IsAssignableFrom<datum_skill>(GetSkillRef(type));
            Assert.Equal((float)length(GLOB.skill_types), (float)length(bob.mind.known_skills));
        });
        r.AssertNoRuntimes();
    }
}
