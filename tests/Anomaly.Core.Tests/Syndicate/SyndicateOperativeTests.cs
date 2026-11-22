using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Syndicate;

/// <summary>
/// Syndicate stage N1b (D316): the nuclear operatives in a booted toy room. The roundstart ruleset's own calls make the most experienced
/// candidate the leader and the rest operatives of one team: each is kitted in tg's operative outfit (the combat boots, the emagged nuclear PDA,
/// the Ansem on the belt, the uplink, the implants), renamed for the team, given its nuclear objective and, once the team finds its
/// nuke, its code; the leader gets a title, the code on paper and the war declaration. (The team's result, which reads the emergency
/// shuttle, is NukieGameplayTests' on MetaStation.)
/// </summary>
public class SyndicateOperativeTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob, mob_living_carbon_human Alice) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        mob_living_carbon_human alice = r.Join(5, 4, "Alice");
        r.Step(Second);
        return (r, feed, bob, alice);
    }

    // the roundstart ruleset's execute, without the base it loads 3 seconds later: Bob the most experienced (the first: no playtime tracking)
    private static datum_dynamic_ruleset_roundstart_nukies Ops(AudioRoom r, params mob_living_carbon_human[] who)
    {
        var ruleset = r.In(() =>
        {
            var made = Dm.@new<datum_dynamic_ruleset_roundstart_nukies>(SSdynamic.get_config());
            foreach (mob_living_carbon_human op in who)
                made.selected_minds.Add(op.mind);
            DmList args = Waiting.NoSleep(made.create_execute_args()) as DmList;
            foreach (mob_living_carbon_human op in who)
                Waiting.NoSleep(made.assign_role(op.mind, Dm.each(args).ToArray()));
            return made;
        });
        r.Step(Second);
        return ruleset;
    }

    [Fact]
    public void TheOperativesAndTheirKitAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(datum_dynamic_ruleset_roundstart_nukies), typeof(datum_antagonist_nukeop), typeof(datum_antagonist_nukeop_leader),
            typeof(datum_antagonist_nukeop_reinforcement), typeof(datum_team_nuclear), typeof(datum_outfit_syndicate), typeof(datum_outfit_syndicate_leader),
            typeof(datum_outfit_syndicate_full), typeof(datum_job_nuclear_operative), typeof(obj_docking_port_mobile_infiltrator),
            typeof(obj_machinery_computer_shuttle_syndicate), typeof(obj_item_circuitboard_computer_syndicate_shuttle),
            typeof(obj_structure_closet_syndicate_personal), typeof(obj_item_clothing_shoes_combat), typeof(obj_item_modular_computer_pda_nukeops),
            typeof(obj_item_gun_ballistic_automatic_pistol_clandestine), typeof(obj_item_storage_box_survival_syndie),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void TheRulesetMakesALeaderAndAnOperativeKittedForOneTeam()
    {
        var (r, feed, bob, alice) = Room(1481);
        r.In(() => Dm.@new<obj_machinery_nuclearbomb_syndicate>(locate(8, 4, 1)));
        Ops(r, bob, alice);

        var leader = r.In(() => bob.mind.has_antag_datum(typeof(datum_antagonist_nukeop_leader))) as datum_antagonist_nukeop_leader;
        var op = r.In(() => alice.mind.has_antag_datum(typeof(datum_antagonist_nukeop))) as datum_antagonist_nukeop;
        Assert.NotNull(leader);
        Assert.NotNull(op);
        Assert.IsNotType<datum_antagonist_nukeop_leader>(op);
        datum_team_nuclear team = leader.nuke_team;
        Assert.Same(team, op.nuke_team);
        Assert.Contains(bob.mind, Dm.each_cast<datum_mind>(team.members));
        Assert.Contains(alice.mind, Dm.each_cast<datum_mind>(team.members));
        // the job and the team's objective
        Assert.IsType<datum_job_nuclear_operative>(r.In(() => alice.mind.assigned_role));
        Assert.Contains(Dm.each<datum_objective>(op.objectives), o => o is datum_objective_nuclear);

        // tg's kit: the combat boots, the nuclear PDA (emagged: the syndicate theme), the Ansem on the belt, the uplink with its 25 telecrystals
        Assert.IsType<obj_item_clothing_shoes_combat>(alice.shoes);
        var pda = Assert.IsType<obj_item_modular_computer_pda_nukeops>(alice.l_store);
        Assert.True(Dm.truthy(Dm.bitand(pda.obj_flags, EMAGGED)));
        Assert.Equal(PDA_THEME_SYNDICATE, Dm.str(pda.device_theme));
        Assert.IsType<obj_item_gun_ballistic_automatic_pistol_clandestine>(alice.belt);
        Assert.IsType<obj_item_card_id_advanced_chameleon_elite>(alice.wear_id);
        var uplink = r.In(() => alice.mind.find_syndicate_uplink()) as datum_component_uplink;
        Assert.NotNull(uplink);
        Assert.True(r.In(() => Dm.num(uplink.uplink_handler.telecrystals)) >= 25);
        Assert.Contains(Dm.each<obj_item_implant>(alice.implants), i => i is obj_item_implant_explosive);
        Assert.Contains(Dm.each<obj_item_implant>(alice.implants), i => i is obj_item_implant_weapons_auth);
        // the survival box's tools
        var box = r.In(() => Dm.locate<obj_item_storage_box_survival_syndie>(alice.get_all_contents()));
        Assert.NotNull(box);
        Assert.NotNull(r.In(() => Dm.locate<obj_item_weldingtool_mini>(box.contents)));

        // the team's name on both, the leader's title before his
        string family = Dm.str(team.syndicate_name);
        Assert.EndsWith(" " + family, Dm.str(alice.real_name));
        Assert.StartsWith(Dm.str(leader.title) + " ", Dm.str(bob.real_name));
        // the war declaration in the leader's hand
        Assert.Contains(Dm.each<obj_item>(bob.held_items), item => item is obj_item_nuclear_challenge);

        // five seconds on the team finds its nuke and everyone learns the code; the leader gets it on paper
        r.Step(6 * Second);
        Assert.NotNull(team.tracked_nuke);
        string code = Dm.str(team.memorized_code);
        Assert.Equal(code, Dm.str(((obj_machinery_nuclearbomb)team.tracked_nuke).r_code));
        Assert.Contains(code, Dm.str(op.antag_memory));
        Assert.True(feed.Any("The nuclear authorization code is: <B>" + code + "</B>"));
        var paper = r.In(() => Dm.locate<obj_item_paper>(bob.held_items));
        Assert.NotNull(paper);
        Assert.Equal("nuclear bomb code", Dm.str(paper.name));
    }

    [Fact]
    public void TheBasesKeycardDoorOpensOnlyToItsCard()
    {
        var (r, feed, bob, _) = Room(1483);
        var door = r.In(() => Dm.@new<obj_machinery_door_puzzle_keycard_syndicate_bio>(locate(4, 4, 1)));
        r.Step(Second);
        Assert.True(r.In(() => Dm.truthy(door.density)));

        // a blast does nothing to it; a hand does not open it
        r.In(() => Waiting.NoSleep(EX_ACT(door, EXPLODE_DEVASTATE)));
        Assert.False(r.In(() => QDELETED(door)));
        FurnitureTests.Click(r, bob, door, FurnitureTests.Left, 1);
        Assert.True(r.In(() => Dm.truthy(door.density)));

        // the chemical plant's card buzzes
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(Dm.@new<obj_item_keycard_syndicate_chem>(bob.loc))));
        r.Step(2);
        FurnitureTests.Click(r, bob, door, FurnitureTests.Left, 1);
        Assert.True(feed.Any("buzzes. This must not be the right key."));
        Assert.True(r.In(() => Dm.truthy(door.density)));

        // the bio-weapon laboratory's opens it, and it stays open
        r.In(() =>
        {
            qdel(bob.get_active_held_item());
            Waiting.NoSleep(bob.put_in_active_hand(Dm.@new<obj_item_keycard_syndicate_bio>(bob.loc)));
        });
        r.Step(2);
        FurnitureTests.Click(r, bob, door, FurnitureTests.Left, 2);
        Assert.True(feed.Any("The door beeps, and slides opens."));
        Assert.False(r.In(() => Dm.truthy(door.density)));
        FurnitureTests.Click(r, bob, door, FurnitureTests.Left, 1);
        Assert.True(feed.Any("The door can't seem to be closed."));
        r.AssertNoRuntimes();
    }
}
