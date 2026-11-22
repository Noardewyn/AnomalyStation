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
/// Gaps BR1 (D339, D341) through the real server on MetaStation: a bitrunner in Meta's den deploys the gondola asteroid from the
/// quantum console, climbs into a netpod and arrives in the domain as an avatar (the player's client follows the avatar), then climbs
/// the safehouse's hololadder and is back in the pod's body.
/// </summary>
public class BitrunningGameplayTests
{
    private const int Second = 20;

    private readonly ITestOutputHelper _output;

    public BitrunningGameplayTests(ITestOutputHelper output) => _output = output;

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
    public void A_bitrunner_enters_a_netpod_and_arrives_in_the_domain_as_an_avatar()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 10551, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Bitrunner");
        var me = (mob_living_carbon_human)w.MobOf(p);
        var chat = new List<ServerText>();
        p.Client.ServerTextReceived += chat.Add;
        w.Run(Second);

        // Meta's den: its server, the console beside it and a netpod
        var server = In(w, () => Dm.each<obj_machinery_quantum_server>(Dm.world.contents).Single());
        var console = In(w, () => Dm.each<obj_machinery_computer_quantum_console>(Dm.world.contents).Single());
        var pod = In(w, () => Dm.each<obj_machinery_netpod>(Dm.world.contents).First(n => n.find_server() == server));
        Assert.Same(server, In(w, () => console.find_server()));
        In(w, () => Waiting.NoSleep(me.forceMove(get_turf(pod))));
        w.Run(5);

        // the gondola asteroid deployed from the console
        object Act(string action, DmList @params = null) => In(w, () => Waiting.NoSleep(console.ui_act(action, @params ?? Dm.list(),
            Dm.@new<datum_tgui>(me, console, "QuantumConsole"), null)));
        Act("set_domain", In(w, () => Dm.list(Dm.kv("id", "gondola_asteroid"))));
        for (int i = 0; i < 40 && In(w, () => server.generated_domain == null || !Dm.truthy(server.is_ready)); i++)
            w.Run(Second / 2);
        Assert.Equal("gondola_asteroid", In(w, () => Dm.str(server.generated_domain?.key)));
        _output.WriteLine($"exits {In(w, () => server.exit_turfs.len)}, points {In(w, () => server.points)}");

        // into the pod: the connection, and the client now follows the avatar
        In(w, () => Waiting.Detach(pod.mouse_drop_receive(me, me)));
        mob avatar = null;
        for (int i = 0; i < 40 && (avatar == null || avatar == me); i++)
        {
            w.Run(Second / 2);
            avatar = w.MobOf(p);
        }
        Assert.NotSame(me, avatar);
        Assert.True(In(w, () => is_area_virtual(get_area(avatar))), "the avatar is not in the domain");
        Assert.Same(me, In(w, () => pod.occupant));
        Assert.NotNull(In(w, () => avatar.GetComponent(typeof(datum_component_avatar_connection))));
        Assert.Equal("netpod_closed_active", In(w, () => pod.icon_state));
        w.Run(2 * Second);

        // the safehouse's ladder: a safe way out
        var ladder = In(w, () => Dm.each<obj_structure_hololadder>(Dm.world.contents).First(l => is_area_virtual(get_area(l))));
        In(w, () => Waiting.Detach(ladder.disconnect(avatar)));
        for (int i = 0; i < 40 && w.MobOf(p) != me; i++)
            w.Run(Second / 2);
        Assert.Same(me, w.MobOf(p));
        Assert.Equal(0f, In(w, () => Dm.num(me.get_organ_loss(Defines.ORGAN_SLOT_BRAIN))));
    }
}
