using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Syndicate X1a (D316) through the real server on MetaStation: a traitor logs into the contract tablet and takes the contract on the
/// other player. At the dropoff with the target, the extraction is called: the pod falls from CentCom and lands in the dropoff area; the
/// target put into it pays the contract and flies to the holding facility (the ninja den, lazily loaded into a reservation), where the
/// pod's ordeal begins; the return pod brings the target back to the dropoff.
/// </summary>
public class ContractorGameplayTests
{
    private const int Second = 20;

    private readonly ITestOutputHelper _output;

    public ContractorGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action f)
    {
        using (w.World.Enter())
            f();
    }

    [MetaServerFact]
    public void A_contract_target_is_extracted_to_the_holding_facility_and_returned()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 41, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        RealHeadlessWorld.Player t = w.Join("Target");
        var me = (mob_living_carbon_human)w.MobOf(p);
        var target = (mob_living_carbon_human)w.MobOf(t);
        var chat = new List<ServerText>();
        t.Client.ServerTextReceived += chat.Add;
        w.Run(Second);

        var traitor = In(w, () => Waiting.NoSleep(me.mind.add_antag_datum(typeof(datum_antagonist_traitor))) as datum_antagonist_traitor);
        Assert.NotNull(traitor);
        var tablet = In(w, () => Dm.@new<obj_item_modular_computer_pda_syndicate_contract_uplink>(me.loc));
        var app = In(w, () => Dm.locate<datum_computer_file_program_contract_uplink>(tablet.stored_files));
        object Act(string action, DmList @params = null) => In(w, () => Waiting.NoSleep(app.ui_act(action, @params ?? Dm.list(),
            Dm.@new<datum_tgui>(me, app, "SyndicateContractor", "Syndicate Contractor Uplink"), null)));
        // the traitor's own objectives may already name the only other crew member, whom a contract then skips (tg's is_unique_objective),
        // and nobody on the arrivals shuttle is a target (objective.dm:137): both step off it into the hallway
        In(w, () => traitor.objectives.Cut());
        In(w, () =>
        {
            var hall = Dm.each<turf>((GLOB.areas_by_type[typeof(area_station_hallway_secondary_entry)] as area).contents)
                .Where(f => f is turf_open && !Dm.truthy(Waiting.NoSleep(f.is_blocked_turf()))).OrderBy(f => f.x * 1000 + f.y).First();
            Waiting.NoSleep(me.forceMove(hall));
            Waiting.NoSleep(target.forceMove(hall));
        });
        Act("PRG_login");
        datum_contractor_hub hub = In(w, () => traitor.uplink_handler.contractor_hub);
        Assert.NotNull(hub);
        var contract = In(w, () => Dm.each<datum_syndicate_contract>(hub.assigned_contracts).FirstOrDefault(c => c.contract.target == target.mind));
        Assert.NotNull(contract);
        _output.WriteLine($"contracts {In(w, () => hub.assigned_contracts.len)}, dropoff {In(w, () => contract.contract.dropoff.name)}, {In(w, () => Dm.str(contract.wanted_message))}");
        Act("PRG_contract-accept", In(w, () => Dm.list(Dm.kv("contract_id", Dm.str(contract.id)))));

        // both go to the dropoff, and the extraction is called
        var spot = In(w, () => Dm.each<turf>(contract.contract.dropoff.contents)
            .Where(f => f is turf_open && !Dm.truthy(Waiting.NoSleep(f.is_blocked_turf())))
            .OrderBy(f => f.x * 1000 + f.y).First());
        In(w, () =>
        {
            Waiting.NoSleep(me.forceMove(spot));
            Waiting.NoSleep(target.forceMove(spot));
        });
        w.Run(5);
        Act("PRG_call_extraction");
        Assert.Equal("", In(w, () => app.error));
        Assert.Equal((float)Defines.CONTRACT_STATUS_EXTRACTING, In(w, () => contract.status));

        // the pod lands in the dropoff area
        obj_structure_closet_supplypod_extractionpod pod = null;
        for (int i = 0; i < 60 && pod == null; i++)
        {
            w.Run(Second / 2);
            pod = In(w, () => Dm.each<obj_structure_closet_supplypod_extractionpod>(Dm.world.contents)
                .FirstOrDefault(found => found.loc is turf && get_area(found) == contract.contract.dropoff));
        }
        Assert.NotNull(pod);
        for (int i = 0; i < 40 && !In(w, () => Dm.truthy(pod.opened)); i++)
            w.Run(Second / 4); // it opens once it has landed
        Assert.True(In(w, () => Dm.truthy(pod.opened)), "the pod did not open");

        // the target is put in: the contract pays, and the pod takes them away to the holding facility
        In(w, () => Waiting.NoSleep(target.forceMove(pod)));
        Assert.Equal((float)Defines.CONTRACT_STATUS_COMPLETE, In(w, () => contract.status));
        Assert.True(In(w, () => hub.contract_TC_to_redeem) > 0);
        area holding = null;
        for (int i = 0; i < 60 && holding == null; i++)
        {
            w.Run(Second / 2);
            holding = In(w, () => get_area(target) as area_centcom_central_command_areas_holding);
        }
        Assert.NotNull(holding);
        var held = In(w, () => (get_turf(target) as turf));
        _output.WriteLine($"held at {In(w, () => held.x)},{In(w, () => held.y)},{In(w, () => held.z)}; holding facility landmarks {In(w, () => GLOB.holdingfacility.len)}");
        Assert.True(In(w, () => GLOB.holdingfacility.len) > 0);
        w.Run(8 * Second);
        Assert.Contains(chat, c => c.Html.Contains("You feel strange...", StringComparison.Ordinal));

        // the pod home (tg's six minutes, here at once): back at the dropoff
        In(w, () => Waiting.Detach(contract.return_victim(target)));
        area back = null;
        for (int i = 0; i < 60 && back != contract.contract.dropoff; i++)
        {
            w.Run(Second / 2);
            back = In(w, () => get_area(target) as area);
        }
        Assert.Same(In(w, () => contract.contract.dropoff), back);
    }
}
