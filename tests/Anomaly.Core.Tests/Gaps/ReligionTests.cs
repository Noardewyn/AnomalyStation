using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The chapel: the high priest picks a sect at the altar of the gods and the sect converts its holy people; favour comes from
/// sacrifices and the sect's own deeds and pays for the rites; the bible's blessing goes through the sect; sparring under a
/// signed contract; the honorbound and burdened traumas; dreams and the dream sect; the soulstone and its shade.
/// </summary>
public class ReligionTests
{
    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(24, 9, seed: seed);
        r.In(() => TgStrings.Mount(DmWorldState.Of(r.World).Files, TgClone.Root));
        return r;
    }

    private static mob_living_carbon_human Chaplain(AudioRoom r, int x, int y, string key = "Bob")
    {
        mob_living_carbon_human who = r.Join(x, y, key);
        r.In(() => Waiting.Detach(SSjob.equip_rank(who, SSjob.get_job_type(typeof(datum_job_chaplain)) as datum_job, who.client)));
        r.Step(5);
        return who;
    }

    private static void RunFor(AudioRoom r, float deciseconds)
    {
        float end = r.In(() => world.time) + deciseconds;
        while (r.In(() => world.time) < end)
            r.Step();
    }

    private static (obj_structure_altar_of_gods Altar, datum_component_religious_tool Tool) Altar(AudioRoom r, int x, int y)
    {
        var altar = Place<obj_structure_altar_of_gods>(r, x, y);
        var tool = r.In(() => altar.GetComponent(typeof(datum_component_religious_tool)) as datum_component_religious_tool);
        Assert.NotNull(tool);
        return (altar, tool);
    }

    private static void PickSect(AudioRoom r, datum_component_religious_tool tool, mob_living chaplain, Type sect)
    {
        r.In(() => Waiting.Detach(tool.select_sect(chaplain, Dm.text("[]", sect))));
        r.Step(4);
        Assert.IsType(sect, r.In(() => GLOB.religious_sect));
    }

    public static IEnumerable<object[]> Sects() => new[]
    {
        typeof(datum_religion_sect_mechanical), typeof(datum_religion_sect_pyre), typeof(datum_religion_sect_greed),
        typeof(datum_religion_sect_burden), typeof(datum_religion_sect_honorbound), typeof(datum_religion_sect_maintenance),
        typeof(datum_religion_sect_spar), typeof(datum_religion_sect_music), typeof(datum_religion_sect_dreams),
    }.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(Sects))]
    public void The_high_priest_picks_a_sect_at_the_altar_and_it_converts_him(Type sect)
    {
        AudioRoom r = Room(14001);
        mob_living_carbon_human bob = Chaplain(r, 4, 4);
        var (altar, tool) = Altar(r, 5, 4);
        Assert.Null(r.In(() => GLOB.religious_sect));
        PickSect(r, tool, bob, sect);
        r.Step(20);
        r.In(() =>
        {
            datum_religion_sect chosen = GLOB.religious_sect;
            Assert.Contains(Dm.each<object>(GLOB.chaplain_altars), a => a == altar);
            // the altar shows the sect: its icon, when the sect has one
            if (chosen.altar_icon_state != null)
                Assert.Equal(Dm.str(chosen.altar_icon_state), altar.icon_state);
            // every sect but the ones with their own deaconize offers it
            Assert.NotEmpty(Dm.each(chosen.rites_list));
            if (chosen is datum_religion_sect_burden)
                Assert.NotNull(bob.has_trauma_type(typeof(datum_brain_trauma_special_burdened)));
            if (chosen is datum_religion_sect_honorbound)
                Assert.NotNull(bob.has_trauma_type(typeof(datum_brain_trauma_special_honorbound)));
            if (chosen is datum_religion_sect_music)
                Assert.NotNull(Dm.each<obj_item_choice_beacon_music>(bob.loc).FirstOrDefault());
            if (chosen is datum_religion_sect_dreams)
                Assert.True(Dm.truthy(bob._listen_lookup?[COMSIG_PRE_DREAMING]));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Only_the_high_priest_picks_and_only_once()
    {
        AudioRoom r = Room(14002);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = Chaplain(r, 4, 4);
        mob_living_carbon_human carl = Chaplain(r, 6, 4, "Carl"); // the second chaplain is a priest, not the high priest
        var (_, tool) = Altar(r, 5, 4);
        r.In(() => Waiting.Detach(tool.select_sect(carl, Dm.text("[]", typeof(datum_religion_sect_pyre)))));
        r.Step(4);
        Assert.Null(r.In(() => GLOB.religious_sect));
        Assert.True(feed.Any("You are not the high priest"));
        PickSect(r, tool, bob, typeof(datum_religion_sect_maintenance));
        // the priest is converted with the high priest's choice
        Assert.True(r.In(() => Dm.truthy(carl.mind.holy_role)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_pyre_sect_gains_favour_from_a_candle_and_spends_it_on_a_rite()
    {
        AudioRoom r = Room(14003);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = Chaplain(r, 4, 4);
        var (altar, tool) = Altar(r, 5, 4);
        PickSect(r, tool, bob, typeof(datum_religion_sect_pyre));
        var sect = r.In(() => (datum_religion_sect_pyre)GLOB.religious_sect);

        // a lit candle sacrificed at the altar: favour for its wax
        var candle = r.In(() => Dm.@new<obj_item_flashlight_flare_candle>(bob.loc));
        r.In(() =>
        {
            Waiting.NoSleep(candle.ignition(bob));
            Waiting.NoSleep(bob.put_in_active_hand(candle));
        });
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(candle.light_on)), "the candle is lit");
        r.In(() => tool.AttemptActions(altar, candle, bob));
        r.Step(4);
        float favour = r.In(() => sect.favor);
        Assert.True(favour > 0, "the candle earned favour");
        Assert.True(r.In(() => QDELETED(candle)));

        // the eternal candle rite: it costs its favour and leaves its candles at the altar
        r.In(() => sect.set_favor(1000));
        float cost = r.In(() => Dm.initial(typeof(datum_religion_rites_infinite_candle), (datum_religion_rites p) => p.favor_cost));
        r.In(() => Waiting.Detach(tool.perform_rite(bob, typeof(datum_religion_rites_infinite_candle))));
        RunFor(r, r.In(() => Dm.initial(typeof(datum_religion_rites_infinite_candle), (datum_religion_rites p) => p.ritual_length)) + 2 * Second);
        Assert.Equal(1000 - cost, r.In(() => sect.favor));
        Assert.NotEmpty(r.In(() => Dm.each<obj_item_flashlight_flare_candle_infinite>(altar.loc).ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_maintenance_god_raises_its_totem_from_wood_on_the_altar()
    {
        AudioRoom r = Room(14004);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = Chaplain(r, 4, 4);
        mob_living_carbon_human alice = r.Join(6, 4, "Alice");
        var (altar, tool) = Altar(r, 5, 4);
        PickSect(r, tool, bob, typeof(datum_religion_sect_maintenance));
        r.In(() => GLOB.religious_sect.set_favor(1000));
        var wood = r.In(() => Dm.@new<obj_item_stack_sheet_mineral_wood>(altar.loc, 5f));
        r.In(() => Waiting.Detach(tool.perform_rite(bob, typeof(datum_religion_rites_ritual_totem))));
        RunFor(r, r.In(() => Dm.initial(typeof(datum_religion_rites_ritual_totem), (datum_religion_rites p) => p.ritual_length)) + 2 * Second);
        var totem = r.In(() => Dm.each<obj_item_ritual_totem>(altar.loc).FirstOrDefault());
        Assert.NotNull(totem);
        Assert.Equal(4f, r.In(() => Dm.num(wood.amount)));
        Assert.Equal(900f, r.In(() => GLOB.religious_sect.favor));
        Assert.NotNull(r.In(() => totem.GetComponent(typeof(datum_component_religious_tool))));
        Assert.False(r.In(() => Dm.truthy(totem.can_be_pulled(bob))), "nobody drags it");

        // Alice is not holy and is told so (tg's pickup drops the totem before the hand takes it, so it still ends in her
        // hand: kept as tg has it); Bob lifts it
        r.In(() => Waiting.NoSleep(alice.drop_all_held_items()));
        Click(r, alice, totem);
        r.Step(2);
        Assert.True(feed.Any("seemingly unable to pick"));
        r.In(() => Waiting.NoSleep(alice.dropItemToGround(totem)));
        r.In(() => Waiting.NoSleep(totem.forceMove(altar.loc)));
        r.In(() => Waiting.NoSleep(bob.drop_all_held_items()));
        Click(r, bob, totem);
        r.Step(2);
        Assert.Same(bob, r.In(() => totem.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_bible_blesses_through_the_sect()
    {
        AudioRoom r = Room(14005);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = Chaplain(r, 4, 4);
        mob_living_carbon_human alice = r.Join(3, 4, "Alice");
        var (_, tool) = Altar(r, 5, 4);
        obj_item_book_bible bible = r.In(() => Dm.each<obj_item_book_bible>((bob.get_all_contents() as DmList)).First());

        // the dream god does not bless the waking
        PickSect(r, tool, bob, typeof(datum_religion_sect_dreams));
        r.In(() => Waiting.NoSleep(alice.apply_damage(30, BRUTE, BODY_ZONE_CHEST)));
        float before = r.In(() => Dm.num(alice.get_brute_loss()));
        Assert.Equal(BLESSING_IGNORED, r.In(() => Waiting.NoSleep(bible.bless(alice, bob))));
        Assert.True(feed.Any("has no interest in blessing the waking"));
        Assert.Equal(before, r.In(() => Dm.num(alice.get_brute_loss())));

        // asleep, the blessing heals her and sends her dreaming
        r.In(() => Waiting.NoSleep(alice.SetSleeping(60 * Second)));
        r.Step(2);
        Assert.Equal(BLESSING_SUCCESS, r.In(() => Waiting.NoSleep(bible.bless(alice, bob))));
        Assert.True(r.In(() => Dm.num(alice.get_brute_loss())) < before, "the blessing healed her");
        Assert.True(r.In(() => HAS_TRAIT(alice, TRAIT_DREAMING)), "she dreams");
        // blessing a dreamer earns favour
        float favour = r.In(() => GLOB.religious_sect.favor);
        r.In(() => Waiting.NoSleep(bible.bless(alice, bob)));
        Assert.True(r.In(() => GLOB.religious_sect.favor) > favour, "blessing a dreamer earned favour");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_dead_body_is_smacked_and_the_dream_god_watches_over_it()
    {
        AudioRoom r = Room(14006);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = Chaplain(r, 4, 4);
        mob_living_carbon_human corpse = r.Dummy(3, 4);
        var (_, tool) = Altar(r, 5, 4);
        obj_item_book_bible bible = r.In(() => Dm.each<obj_item_book_bible>((bob.get_all_contents() as DmList)).First());
        r.In(() => Waiting.NoSleep(corpse.death()));
        r.Step(2);
        PickSect(r, tool, bob, typeof(datum_religion_sect_dreams));
        float favour = r.In(() => GLOB.religious_sect.favor);
        r.In(() =>
        {
            Waiting.NoSleep(bob.put_in_active_hand(bible));
            Waiting.NoSleep(bible.attack__proc(corpse, bob));
        });
        r.Step(2);
        Assert.True(r.In(() => GLOB.religious_sect.favor) > favour, "the dream god's dead blessing earned favour");
        Assert.True(feed.Any("watches over"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Two_fighters_sign_the_sparring_contract_and_the_loser_in_crit_ends_the_match()
    {
        AudioRoom r = Room(14007);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = Chaplain(r, 4, 4);
        mob_living_carbon_human alice = r.Join(3, 4, "Alice");
        var (_, tool) = Altar(r, 5, 4);
        PickSect(r, tool, bob, typeof(datum_religion_sect_spar));
        var sect = r.In(() => (datum_religion_sect_spar)GLOB.religious_sect);
        var contract = r.In(() => sect.existing_contract = Dm.@new<obj_item_sparring_contract>(bob.loc));

        // the toy room's own area is the arena
        string arenaName = r.In(() =>
        {
            area here = get_area(bob) as area;
            sect.arenas[here.name] = here.GetType();
            GLOB.areas_by_type[here.GetType()] = here;
            contract.arena_condition = here.GetType();
            return here.name;
        });

        // Bob opens the contract and signs on the left; Alice signs the same terms on the right
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(contract)));
        r.In(() => Waiting.Detach(contract.attack_self(bob)));
        r.Step(4);
        var opened = Take(bob);
        Assert.Equal("SparringContract", InterfaceOf(LastFullUpdate(opened, out string bobWindow)));
        Act(r, bob, bobWindow, "sign", ("weapon", 1f), ("area", arenaName), ("stakes", (float)STAKES_HOLY_MATCH), ("sign_position", "left"));
        r.In(() =>
        {
            Waiting.NoSleep(bob.dropItemToGround(contract));
            Waiting.NoSleep(alice.put_in_active_hand(contract));
        });
        r.Step(2);
        Take(bob);
        r.In(() => Waiting.Detach(contract.attack_self(alice)));
        r.Step(4);
        LastUpdate(Take(alice), out string aliceWindow);
        Act(r, alice, aliceWindow, "sign", ("weapon", 1f), ("area", arenaName), ("stakes", (float)STAKES_HOLY_MATCH));
        Assert.True(r.In(() => contract.signed_by[1] != null && contract.signed_by[2] != null), "both signed");
        var data = Data(r, alice, out _);
        Assert.Equal(r.In(() => Dm.str(bob.real_name)), data.GetProperty("left_sign").GetString());
        Assert.Equal(r.In(() => Dm.str(alice.real_name)), data.GetProperty("right_sign").GetString());
        Assert.Equal(1, data.GetProperty("in_area").GetInt32());

        // the fight: both spar, then Alice goes down
        Act(r, alice, aliceWindow, "fight");
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_SPARRING) && HAS_TRAIT(alice, TRAIT_SPARRING)), "the match began");
        Assert.True(r.In(() => QDELETED(contract)));
        r.In(() => Waiting.NoSleep(alice.adjust_brute_loss(150)));
        r.Step(4);
        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_SPARRING)), "the match ended");
        Assert.True(feed.Any("HAS WON!"));
        Assert.Equal(1f, r.In(() => sect.favor));
        Assert.Contains(r.In(() => Dm.each<datum_weakref>(sect.past_opponents).Select(w => w.resolve()).ToList()), o => o == alice);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_outsider_who_strikes_the_fighters_flubs_the_match()
    {
        AudioRoom r = Room(14008);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = Chaplain(r, 4, 4);
        mob_living_carbon_human alice = r.Join(3, 4, "Alice");
        mob_living_carbon_human carl = r.Join(4, 5, "Carl");
        var (_, tool) = Altar(r, 5, 4);
        PickSect(r, tool, bob, typeof(datum_religion_sect_spar));
        var match = r.In(() => Dm.@new<datum_sparring_match>(1f, get_area(bob), 1f, bob, alice));
        r.Step(2);
        Assert.True(r.In(() => HAS_TRAIT(alice, TRAIT_SPARRING)));
        // Carl punches Bob twice: each blow is an interference the god punishes, the second ends the match
        r.In(() => carl.set_combat_mode(TRUE));
        Click(r, carl, bob);
        r.Step(Second);
        Assert.Equal(1f, r.In(() => match.flubs));
        Click(r, carl, bob);
        r.Step(Second);
        Assert.True(feed.Any("The match was flub'd!"));
        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_SPARRING) || HAS_TRAIT(alice, TRAIT_SPARRING)), "the match ended");
        Assert.True(feed.Any("interfering with"), "Carl was punished");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_honorbound_chaplain_refuses_to_strike_the_innocent_until_they_strike_first()
    {
        AudioRoom r = Room(14009);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = Chaplain(r, 4, 4);
        mob_living_carbon_human alice = r.Join(3, 4, "Alice");
        var (_, tool) = Altar(r, 5, 4);
        PickSect(r, tool, bob, typeof(datum_religion_sect_honorbound));
        var honor = r.In(() => bob.has_trauma_type(typeof(datum_brain_trauma_special_honorbound)) as datum_brain_trauma_special_honorbound);
        Assert.NotNull(honor);
        Assert.NotNull(r.In(() => Dm.locate<datum_action_cooldown_spell_pointed_declare_evil>(bob.actions)));

        // empty-handed, Bob's punch at innocent Alice is refused
        r.In(() => Waiting.NoSleep(bob.drop_all_held_items()));
        float health = r.In(() => Dm.num(alice.health));
        r.In(() => bob.set_combat_mode(TRUE));
        Click(r, bob, alice);
        r.Step(4);
        Assert.Equal(health, r.In(() => Dm.num(alice.health)));

        // she hits him: she is guilty now, and he may answer
        r.In(() => alice.set_combat_mode(TRUE));
        Click(r, alice, bob);
        r.Step(4);
        Assert.Contains(r.In(() => Dm.each<object>(honor.guilty).ToList()), g => g == alice);
        r.Step(Second);
        Click(r, bob, alice);
        r.Step(4);
        Assert.True(r.In(() => Dm.num(alice.health)) < health, "Bob struck the guilty");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_burdened_chaplain_grows_his_burden_with_every_limb_he_loses()
    {
        AudioRoom r = Room(14010);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = Chaplain(r, 4, 4);
        var (_, tool) = Altar(r, 5, 4);
        PickSect(r, tool, bob, typeof(datum_religion_sect_burden));
        var burden = r.In(() => bob.has_trauma_type(typeof(datum_brain_trauma_special_burdened)) as datum_brain_trauma_special_burdened);
        Assert.NotNull(burden);
        float start = r.In(() => burden.burden_level);
        r.In(() => Waiting.NoSleep((bob.get_bodypart(BODY_ZONE_L_ARM) as obj_item_bodypart).drop_limb()));
        r.Step(2);
        Assert.Equal(start + 1, r.In(() => burden.burden_level));
        r.In(() => Waiting.NoSleep((bob.get_bodypart(BODY_ZONE_R_LEG) as obj_item_bodypart).drop_limb()));
        r.Step(2);
        Assert.Equal(start + 2, r.In(() => burden.burden_level));
        Assert.True(feed.Any("burden"), "he is told of his burden");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_sleeper_dreams_fragment_by_fragment_and_the_dream_god_rewards_the_chaplain()
    {
        AudioRoom r = Room(14011);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = Chaplain(r, 4, 4);
        var (_, tool) = Altar(r, 5, 4);
        PickSect(r, tool, bob, typeof(datum_religion_sect_dreams));
        Assert.NotEmpty(r.In(() => Dm.each<datum_dream>(GLOB.dreams).ToList()));
        r.In(() => Waiting.NoSleep(bob.SetSleeping(60 * Second)));
        r.Step(2);
        float favour = r.In(() => GLOB.religious_sect.favor);
        r.In(() => bob.dream());
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_DREAMING)));
        RunFor(r, 30 * Second);
        Assert.True(feed.Chat.Count(c => c.Target == bob && c.Html.Contains("<i>... ")) >= 2, "the dream came in fragments");
        Assert.True(r.In(() => GLOB.religious_sect.favor) > favour, "the dream god approved of the slumber");
        Assert.True(feed.Any("approves of your slumber"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Everyone_asleep_dreams_now_and_then()
    {
        AudioRoom r = Room(14012);
        Feed feed = Listen(r);
        mob_living_carbon_human alice = r.Join(4, 4, "Alice");
        r.In(() => Waiting.NoSleep(alice.SetSleeping(120 * Second)));
        RunFor(r, 100 * Second);
        Assert.Contains(feed.Chat, c => c.Target == alice && c.Html.Contains("<i>... "));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_soulstone_takes_a_dying_soul_into_a_shade_bound_to_its_holder()
    {
        AudioRoom r = Room(14013);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        mob_living_carbon_human alice = r.Join(5, 4, "Alice");
        var stone = r.In(() => Dm.@new<obj_item_soulstone_anybody_chaplain>(bob.loc));
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(stone)));
        r.In(() => Waiting.NoSleep(alice.death()));
        r.Step(2);
        r.In(() => Waiting.Detach(stone.attack__proc(alice, bob)));
        RunFor(r, 5 * Second);
        var shade = r.In(() => Dm.locate<mob_living_basic_shade>(stone.contents));
        Assert.NotNull(shade);
        Assert.True(r.In(() => QDELETED(alice)), "the body turned to dust");
        string aliceName = r.In(() => Dm.str(alice.real_name));
        Assert.Equal("Shade of " + aliceName, r.In(() => Dm.str(shade.real_name)));
        Assert.Equal("mysterious old shard: Shade of " + aliceName, r.In(() => stone.name));
        var minion = r.In(() => shade.mind?.has_antag_datum(typeof(datum_antagonist_shade_minion)) as datum_antagonist_shade_minion);
        Assert.NotNull(minion);
        Assert.Equal(r.In(() => Dm.str(bob.real_name)), r.In(() => minion.master_name));

        // released, the one-use shard goes dull and the shade stands beside Bob
        r.In(() => Waiting.Detach(stone.attack_self(bob)));
        r.Step(4);
        Assert.Equal(r.In(() => bob.loc), r.In(() => shade.loc));
        Assert.True(r.In(() => Dm.truthy(stone.spent)));
        Assert.StartsWith("dull ", r.In(() => stone.name));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_bible_exorcises_a_soulstone()
    {
        AudioRoom r = Room(14014);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = Chaplain(r, 4, 4);
        var stone = r.In(() => Dm.@new<obj_item_soulstone_anybody>(bob.loc));
        obj_item_book_bible bible = r.In(() => Dm.each<obj_item_book_bible>((bob.get_all_contents() as DmList)).First());
        r.In(() =>
        {
            Waiting.NoSleep(bob.put_in_active_hand(bible));
            Waiting.Detach(bible.interact_with_atom(stone, bob));
        });
        RunFor(r, 6 * Second);
        Assert.Equal(THEME_HOLY, r.In(() => stone.theme));
        Assert.Equal("purified_soulstone", r.In(() => stone.icon_state));
        Assert.True(feed.Any("purifies"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Every_newly_live_chapel_type_is_created_and_deleted()
    {
        AudioRoom r = Room(14015);
        mob_living_carbon_human bob = Chaplain(r, 4, 4);
        var (_, tool) = Altar(r, 5, 4);
        PickSect(r, tool, bob, typeof(datum_religion_sect_spar));
        Type[] types =
        {
            typeof(obj_structure_altar_of_gods), typeof(obj_item_ritual_totem), typeof(obj_item_soulstone), typeof(obj_item_soulstone_anybody),
            typeof(obj_item_soulstone_anybody_chaplain), typeof(obj_item_soulstone_anybody_chaplain_sparring), typeof(obj_item_soulstone_anybody_purified),
            typeof(obj_item_soulstone_mystic), typeof(obj_item_ectoplasm), typeof(obj_item_ectoplasm_angelic), typeof(obj_item_ectoplasm_mystic),
            typeof(mob_living_basic_shade), typeof(obj_item_sparring_contract), typeof(obj_item_ceremonial_blade), typeof(obj_item_paper_holy_writ),
            typeof(obj_item_gun_ballistic_revolver_chaplain), typeof(obj_item_instrument_violin_festival), typeof(obj_item_organ_brain_psyker),
            typeof(obj_item_bodypart_head_psyker), typeof(obj_item_organ_heart_evolved_sacred_dreamer),
        };
        foreach (Type type in types)
        {
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
            var thing = r.In(() => Dm.@new(type, locate(12, 4, 1)) as datum);
            r.Step(2);
            r.In(() => qdel(thing));
            r.Step(2);
        }
        Type[] datums =
        {
            typeof(datum_religion_sect_mechanical), typeof(datum_religion_sect_pyre), typeof(datum_religion_sect_greed), typeof(datum_religion_sect_burden),
            typeof(datum_religion_sect_honorbound), typeof(datum_religion_sect_maintenance), typeof(datum_religion_sect_spar), typeof(datum_religion_sect_music),
            typeof(datum_religion_sect_dreams), typeof(datum_component_religious_tool), typeof(datum_brain_trauma_special_burdened),
            typeof(datum_brain_trauma_special_honorbound), typeof(datum_status_effect_dream_protection), typeof(datum_status_effect_slumber_party),
            typeof(datum_status_effect_dream_projection), typeof(datum_dream_random), typeof(datum_dream_hear_something), typeof(datum_dream_specific_portent),
            typeof(datum_antagonist_shade_minion), typeof(datum_sparring_match), typeof(datum_component_soulstoned), typeof(datum_component_sect_nullrod_bonus),
            typeof(datum_component_unbreakable), typeof(datum_element_tenacious), typeof(datum_component_smooth_tunes),
            typeof(datum_status_effect_song_antimagic), typeof(datum_status_effect_song_light), typeof(mob_eye_imaginary_friend_dream_projection),
        };
        foreach (Type type in datums)
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
        foreach (Type rite in Dm.each_cast<Type>(r.In(() => subtypesof(typeof(datum_religion_rites)))))
            Assert.False(Attribute.IsDefined(rite, typeof(DmInertAttribute), inherit: false), rite.Name + " is inert");
        r.AssertNoRuntimes();
    }
}
