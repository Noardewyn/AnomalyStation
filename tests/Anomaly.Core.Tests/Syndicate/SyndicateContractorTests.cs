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
/// Syndicate stage X1a (D316): the contractor. The contract kit's tablet has the Syndicate Contractor Uplink; a traitor's login makes
/// the contractor hub with tg's contracts on the crew (six, fewer for a small crew) (their payouts at least 30 TC, a wanted message, a dropoff); an outsider is
/// refused. A contract accepted, its target sent into the extraction pod pays the contract's TC (with the bonus while alive), strips
/// the target to the uniform and shoes, and ships them off; the TC is claimed as telecrystals in the hand. The contract reroll rolls
/// the open contracts again.
/// </summary>
public class SyndicateContractorTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob, mob_living_carbon_human Alice, mob_living_carbon_human Carol) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        mob_living_carbon_human alice = r.Join(5, 4, "Alice");
        mob_living_carbon_human carol = r.Join(7, 4, "Carol");
        foreach (var who in new[] { bob, alice, carol })
            r.In(() => Waiting.NoSleep(who.equipOutfit(typeof(datum_outfit_job_assistant))));
        r.In(() =>
        {
            // the crew's records (the manifest's inject needs a crew job, which the room's minds have not)
            foreach (var who in new[] { bob, alice, carol })
            {
                Dm.@new<datum_record_crew>(Dm.named("name", who.real_name), Dm.named("rank", "Assistant"));
                Dm.@new<datum_record_locked>(Dm.named("name", who.real_name), Dm.named("rank", "Assistant"), Dm.named("locked_dna", who.dna), Dm.named("mind_ref", who.mind));
            }
            GLOB.the_station_areas = Dm.list(((area)get_area(bob)).type); // the toy room's area is the station
        });
        r.Step(Second);
        return (r, feed, bob, alice, carol);
    }

    private static datum_antagonist_traitor MakeTraitor(AudioRoom r, mob who)
    {
        var traitor = r.In(() => Waiting.NoSleep(who.mind.add_antag_datum(typeof(datum_antagonist_traitor))) as datum_antagonist_traitor);
        r.Step(4);
        Assert.NotNull(traitor);
        return traitor;
    }

    private static (obj_item_modular_computer_pda_syndicate_contract_uplink Tablet, datum_computer_file_program_contract_uplink App) Tablet(AudioRoom r, mob_living_carbon_human who)
    {
        var tablet = r.In(() =>
        {
            var made = Dm.@new<obj_item_modular_computer_pda_syndicate_contract_uplink>(who.loc);
            Waiting.NoSleep(who.put_in_active_hand(made));
            return made;
        });
        var app = r.In(() => Dm.locate<datum_computer_file_program_contract_uplink>(tablet.stored_files));
        Assert.NotNull(app);
        return (tablet, app);
    }

    private static object Act(AudioRoom r, mob who, datum_computer_file_program_contract_uplink app, string action, DmList @params = null)
    {
        var ui = r.In(() => Dm.@new<datum_tgui>(who, app, "SyndicateContractor", "Syndicate Contractor Uplink"));
        return r.In(() => Waiting.NoSleep(app.ui_act(action, @params ?? Dm.list(), ui, null)));
    }

    [Fact]
    public void TheContractKitIsLive()
    {
        foreach (Type type in new[]
        {
            typeof(datum_contractor_hub), typeof(datum_syndicate_contract), typeof(datum_computer_file_program_contract_uplink),
            typeof(datum_uplink_item_contractor_reroll), typeof(datum_lazy_template), typeof(datum_lazy_template_ninja_holding_facility),
            typeof(obj_effect_landmark_holding_facility), typeof(obj_item_storage_box_syndicate_contract_kit),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
        var r = new AudioRoom(5, 5, seed: 3);
        Assert.False(r.In(() => uplink_item_hidden((datum_uplink_item)Dm.@new(typeof(datum_uplink_item_bundles_tc_contract_kit)))));
        Assert.False(r.In(() => uplink_item_hidden((datum_uplink_item)Dm.@new(typeof(datum_uplink_item_contractor_reroll)))));
        Assert.NotNull(r.In(() => GLOB.lazy_templates[LAZY_TEMPLATE_KEY_NINJA_HOLDING_FACILITY] as datum_lazy_template_ninja_holding_facility));
    }

    [Fact]
    public void ATraitorsLoginMakesTheHubWithContractsOnTheCrew()
    {
        var (r, feed, bob, alice, carol) = Room(141);
        var (_, app) = Tablet(r, bob);
        // an outsider is refused
        Act(r, bob, app, "PRG_login");
        Assert.Equal("UNAUTHORIZED USER", r.In(() => app.error));
        var traitor = MakeTraitor(r, bob);
        Act(r, bob, app, "PRG_login");
        datum_contractor_hub hub = r.In(() => traitor.uplink_handler.contractor_hub);
        Assert.NotNull(hub);
        // tg's trim of the six for a small crew (contractor_hub.dm:32): Cut(1, three records) leaves four
        Assert.Equal(4, r.In(() => hub.assigned_contracts.len));
        var contracts = r.In(() => Dm.each<datum_syndicate_contract>(hub.assigned_contracts).ToList());
        var targets = r.In(() => contracts.Select(c => c.contract.target).Where(t => t != null).ToList());
        Assert.Contains(r.In(() => alice.mind), targets);
        Assert.Contains(r.In(() => carol.mind), targets);
        Assert.DoesNotContain(r.In(() => bob.mind), targets);
        Assert.True(r.In(() => contracts.Sum(c => c.contract.payout + c.contract.payout_bonus)) >= 30, "the payouts are under tg's threshold");
        Assert.All(contracts, c => Assert.False(string.IsNullOrEmpty(r.In(() => Dm.str(c.wanted_message)))));
        Assert.All(contracts, c => Assert.NotNull(r.In(() => c.contract.dropoff)));
        Assert.Equal(Enumerable.Range(1, 4).Select(i => (float)i), contracts.Select(c => Dm.num(c.id)));
        // the window's data: logged in, the contracts with their target's name
        DmList data = r.In(() => Waiting.NoSleep(app.ui_data(bob)) as DmList);
        Assert.Equal(TRUE, r.In(() => data["logged_in"]));
        Assert.Equal(4, r.In(() => (data["contracts"] as DmList).len));
        // the reroll rolls the open contracts again
        var reroll = r.In(() => (datum_uplink_item)Dm.@new(typeof(datum_uplink_item_contractor_reroll)));
        r.In(() => Waiting.NoSleep(reroll.spawn_item(null, bob, traitor.uplink_handler, bob)));
        Assert.Equal(4, r.In(() => hub.assigned_contracts.len));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ATargetSentIntoTheExtractionPodPaysTheContractAndIsStripped()
    {
        var (r, feed, bob, alice, carol) = Room(142);
        var (_, app) = Tablet(r, bob);
        var traitor = MakeTraitor(r, bob);
        Act(r, bob, app, "PRG_login");
        datum_contractor_hub hub = r.In(() => traitor.uplink_handler.contractor_hub);
        var contract = r.In(() => Dm.each<datum_syndicate_contract>(hub.assigned_contracts).First(c => c.contract.target == alice.mind));
        Act(r, bob, app, "PRG_contract-accept", r.In(() => Dm.list(Dm.kv("contract_id", Dm.str(contract.id)))));
        Assert.Same(contract, r.In(() => hub.current_contract));
        Assert.Equal((float)CONTRACT_STATUS_ACTIVE, r.In(() => contract.status));

        // both at the dropoff (the room's one area); the pod's launch from CentCom is the gameplay test's (the room has no CentCom).
        // She goes into an extraction pod that the contract watches, as its launch_extraction_pod does
        Assert.Equal(1f, r.In(() => Dm.num(contract.contract.dropoff_check(bob, alice))));
        var pod = r.In(() => Dm.@new<obj_structure_closet_supplypod_extractionpod>(locate(6, 6, 1)));
        r.In(() => contract.RegisterSignal(pod, COMSIG_ATOM_ENTERED, nameof(datum_syndicate_contract.enter_check)));
        float expected = r.In(() => contract.contract.payout + contract.contract.payout_bonus);
        var backpack = r.In(() => alice.back);
        Assert.NotNull(backpack);
        r.In(() => Waiting.NoSleep(alice.forceMove(pod)));
        Assert.Equal((float)CONTRACT_STATUS_COMPLETE, r.In(() => contract.status));
        Assert.Null(r.In(() => hub.current_contract));
        Assert.Equal(1f, r.In(() => hub.contracts_completed));
        Assert.Equal(expected, r.In(() => hub.contract_TC_to_redeem)); // alive: the bonus too
        Assert.Null(r.In(() => alice.back)); // stripped to the uniform and shoes
        Assert.NotNull(r.In(() => alice.w_uniform));
        r.AssertNoRuntimes();
        Assert.True(r.In(() => HAS_TRAIT(alice.mind, TRAIT_HAS_BEEN_KIDNAPPED)));
        Assert.True(feed.Any("One of your crew was captured by a rival organisation"));

        // the TC is claimed as telecrystals in the hand
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(bob.get_active_held_item() as obj_item)));
        Act(r, bob, app, "PRG_redeem_TC");
        var crystals = r.In(() => bob.get_active_held_item() as obj_item_stack_telecrystal ?? bob.get_inactive_held_item() as obj_item_stack_telecrystal);
        Assert.NotNull(crystals);
        Assert.Equal(expected, r.In(() => Dm.num(crystals.amount)));
        Assert.Equal(0f, r.In(() => hub.contract_TC_to_redeem));
        Assert.Equal(expected, r.In(() => hub.contract_TC_payed_out));
        Assert.True(feed.Any("Your payment materializes into your hands!"));
    }
}
