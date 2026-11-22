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
/// Gaps package V1 (D327): the revolution in a booted toy room on the station's level, with Bob as the head revolutionary, Alice as a
/// crew member and Carol as the captain. Bob starts with a flash, the syndicate security HUD and the heads' code words; his flash
/// converts Alice; a mindshield frees her and Bob resists it; the station wins when the head revolutionaries fall (the converts are
/// freed, a living head becomes an enemy of the state) and the revolution wins when the heads of staff do.
/// </summary>
public class RevolutionV1Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob, mob_living_carbon_human Alice, mob_living_carbon_human Carol) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        mob_living_carbon_human alice = r.Join(5, 4, "Alice");
        mob_living_carbon_human carol = r.Join(9, 4, "Carol");
        r.In(() =>
        {
            GLOB.station_levels_cache.len = 1;
            GLOB.station_levels_cache[1] = TRUE;
            SSmapping.z_list = Dm.list(Dm.@new<datum_space_level>(1, "station", Dm.list(Dm.kv(ZTRAIT_STATION, TRUE))));
            // pockets and a backpack for the flash (tg deletes it on a body with neither)
            Waiting.NoSleep(bob.equipOutfit(typeof(datum_outfit_job_assistant)));
            Waiting.NoSleep(alice.equipOutfit(typeof(datum_outfit_job_assistant)));
            bob.mind.set_assigned_role(SSjob.get_job_type(typeof(datum_job_assistant)) as datum_job);
            alice.mind.set_assigned_role(SSjob.get_job_type(typeof(datum_job_assistant)) as datum_job);
            carol.mind.set_assigned_role(SSjob.get_job_type(typeof(datum_job_captain)) as datum_job);
            // the crew manifest's locked records are tg's crew (get_crewmember_minds): the heads the revolution targets come from it
            GLOB.manifest.inject(bob);
            GLOB.manifest.inject(alice);
            GLOB.manifest.inject(carol);
            // the revolution holds the emergency shuttle (SSshuttle's hostile environment): the room has no map, so one idle in a corner
            SSshuttle.emergency = Dm.@new<obj_docking_port_mobile_emergency>(locate(12, 8, 1));
        });
        r.Step(Second);
        return (r, feed, bob, alice, carol);
    }

    /// The rulesets' own head revolutionary (dynamic_ruleset_roundstart.dm:440): a flash, the HUD, the team started.
    private static datum_antagonist_rev_head MakeHead(AudioRoom r, mob_living_carbon_human who)
    {
        r.In(() =>
        {
            GLOB.revolution_handler ??= Dm.@new<datum_revolution_handler>();
            var head = Dm.@new<datum_antagonist_rev_head>();
            head.give_flash = TRUE;
            head.give_hud = TRUE;
            head.remove_clumsy = TRUE;
            Waiting.NoSleep(who.mind.add_antag_datum(head, GLOB.revolution_handler.revs));
            Waiting.NoSleep(GLOB.revolution_handler.start_revolution());
        });
        r.Step(4);
        var made = r.In(() => who.mind.has_antag_datum(typeof(datum_antagonist_rev_head))) as datum_antagonist_rev_head;
        Assert.NotNull(made);
        return made;
    }

    private static void Flash(AudioRoom r, mob_living_carbon_human user, mob_living_carbon_human target)
    {
        var flash = r.In(() => Dm.locate<obj_item_assembly_flash>(user.get_all_contents()));
        Assert.NotNull(flash);
        r.In(() => Waiting.NoSleep(user.put_in_active_hand(flash, TRUE)));
        r.Step(2);
        r.In(() => { flash.last_trigger = -1000; flash.times_used = 0; flash.burnout_resistance = 1; }); // ready, and no random burnout
        r.In(() => Waiting.NoSleep(flash.attack__proc(target, user)));
        r.Step(4);
    }

    private static bool IsRev(AudioRoom r, mob who) => r.In(() => Dm.truthy(who.mind.has_antag_datum(typeof(datum_antagonist_rev))));

    [Fact]
    public void TheRevolutionIsLive()
    {
        foreach (Type type in new[]
        {
            typeof(datum_antagonist_rev), typeof(datum_antagonist_rev_head), typeof(datum_team_revolution), typeof(datum_revolution_handler),
            typeof(datum_antagonist_enemy_of_the_state), typeof(datum_dynamic_ruleset_roundstart_revolution), typeof(datum_dynamic_ruleset_latejoin_revolution),
            typeof(datum_element_art_rev),
        })
            Assert.False(InertSeam.IsInertType(type), type.Name + " is inert");
    }

    [Fact]
    public void AHeadRevolutionaryConvertsByFlashAndAMindshieldFreesTheConvert()
    {
        var (r, feed, bob, alice, carol) = Room(5101);
        var head = MakeHead(r, bob);
        // the head's kit: a flash on the body, the syndicate security HUD, the code words, the flash from behind
        Assert.NotNull(r.In(() => Dm.locate<obj_item_assembly_flash_handheld>(bob.get_all_contents())));
        Assert.True(r.In(() => bob.get_organ_slot(ORGAN_SLOT_HUD) is obj_item_organ_cyberimp_eyes_hud_security_syndicate), "no syndicate HUD");
        Assert.Equal(2, r.In(() => Dm.length(bob.GetComponents(typeof(datum_component_codeword_hearing)))));
        Assert.NotNull(r.In(() => bob.GetComponent(typeof(datum_component_can_flash_from_behind))));
        // the handler holds the shuttle and makes the captain the team's target
        Assert.True(r.In(() => Dm.@in(GLOB.revolution_handler, SSshuttle.hostile_environments)), "the shuttle is not held");
        Assert.Contains(r.In(() => carol.mind), r.In(() => Dm.each<datum_objective_mutiny>(GLOB.revolution_handler.revs.objectives).Select(o => o.target).ToList()));

        Flash(r, bob, alice);
        Assert.True(IsRev(r, alice), "Alice was not converted");
        Assert.True(r.In(() => Dm.@in(alice.mind, GLOB.revolution_handler.revs.members)));
        Assert.True(r.In(() => Dm.truthy(alice.IsStun())), "the convert is not stunned");

        // a mindshield frees the convert; the head resists it
        r.In(() => Waiting.NoSleep(Dm.@new<obj_item_implant_mindshield>(alice.loc).implant(alice, carol, TRUE)));
        r.Step(2);
        Assert.False(IsRev(r, alice), "the mindshield did not free Alice");
        Assert.True(feed.Any("You are no longer a brainwashed revolutionary!"));
        r.In(() => Waiting.NoSleep(Dm.@new<obj_item_implant_mindshield>(bob.loc).implant(bob, carol, TRUE)));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(bob.mind.has_antag_datum(typeof(datum_antagonist_rev_head)))), "the head did not resist");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheStationWinsWhenTheHeadRevolutionariesFall()
    {
        var (r, feed, bob, alice, _) = Room(5102);
        MakeHead(r, bob);
        Flash(r, bob, alice);
        Assert.True(IsRev(r, alice));
        r.In(() => Waiting.NoSleep(bob.death()));
        r.Step(5 * Second);
        Assert.Equal(STATION_VICTORY, r.In(() => (int)Dm.num(GLOB.revolution_handler.result)));
        Assert.False(IsRev(r, alice), "the convert was not freed");
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_DEFIB_BLACKLISTED)), "the dead head can be revived");
        Assert.True(feed.Any("It appears the mutiny has been quelled."));
        Assert.False(r.In(() => Dm.@in(GLOB.revolution_handler, SSshuttle.hostile_environments)), "the shuttle is still held");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALivingHeadRevolutionaryBecomesAnEnemyOfTheState()
    {
        var (r, feed, bob, _, _) = Room(5103);
        MakeHead(r, bob);
        r.In(() => Waiting.NoSleep(((datum_antagonist_rev)bob.mind.has_antag_datum(typeof(datum_antagonist_rev))).remove_revolutionary(DECONVERTER_STATION_WIN)));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(bob.mind.has_antag_datum(typeof(datum_antagonist_enemy_of_the_state)))));
        Assert.False(r.In(() => Dm.truthy(bob.mind.has_antag_datum(typeof(datum_antagonist_rev_head)))));
        Assert.True(feed.Any("The revolution is dead."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRevolutionWinsWhenTheHeadsOfStaffFall()
    {
        var (r, _, bob, _, carol) = Room(5104);
        MakeHead(r, bob);
        r.In(() => Waiting.NoSleep(carol.death()));
        r.Step(5 * Second);
        Assert.Equal(REVOLUTION_VICTORY, r.In(() => (int)Dm.num(GLOB.revolution_handler.result)));
        r.In(() => SSticker.setup_done = TRUE); // the toy room's round was set up by the host, not by SSticker's setup
        Assert.True(r.In(() => Dm.truthy(SSticker.check_finished())), "the round does not end on the revolution's victory");
        r.AssertNoRuntimes();
    }
}
