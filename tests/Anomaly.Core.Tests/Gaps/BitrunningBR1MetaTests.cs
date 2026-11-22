using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package BR1 (D339, D341) on the converted MetaStation: Meta's bitrunning den boots live (its three netpods and the console
/// linked to its quantum server, the byteforge in reach, the order console), and the server deploys the grasslands hunt from its
/// converted map: the modular mob segments spawn their deer, a slain deer is worth 3.5 points towards the main crate, and the domain is
/// scrubbed after the reset, with no runtime from bitrunning's files.
/// </summary>
public class BitrunningBR1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public BitrunningBR1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private void Step(int ticks)
    {
        for (int tick = 0; tick < ticks; tick++)
            _meta.World.Step();
    }

    [MetaFact]
    public void Metas_den_boots_linked_and_deploys_the_grasslands_hunt()
    {
        Step(40);
        obj_machinery_quantum_server server;
        int runtimesBefore = _meta.Log.Runtimes.Count;
        using (_meta.World.Enter())
        {
            server = Dm.each<obj_machinery_quantum_server>().Single();
            var pods = Dm.each<obj_machinery_netpod>().ToList();
            var console = Dm.each<obj_machinery_computer_quantum_console>().Single();
            var forges = Dm.each<obj_machinery_byteforge>().ToList();
            _output.WriteLine($"Meta's den: {pods.Count} netpods, {forges.Count} byteforges, area {(get_area(server) as area)?.type}");
            Assert.Equal(3, pods.Count);
            Assert.All(pods, pod => Assert.Same(server, pod.find_server()));
            Assert.Same(server, console.find_server());
            Assert.NotNull(server.get_random_nearby_forge());
            Assert.Single(Dm.each<obj_machinery_computer_order_console_bitrunning>());
            Assert.All(pods, pod => Assert.Equal("netpod_open_active", pod.icon_state));
            Assert.False(InertSeam.IsInert(server));
            Waiting.Detach(server.cold_boot_map("grasslands_hunt", FALSE));
        }
        datum_lazy_template_virtual_domain_grasslands_hunt domain = null;
        for (int i = 0; i < 60 && domain == null; i++)
        {
            Step(10);
            using (_meta.World.Enter())
                domain = Dm.truthy(server.is_ready) ? server.generated_domain as datum_lazy_template_virtual_domain_grasslands_hunt : null;
        }
        Assert.NotNull(domain);
        using (_meta.World.Enter())
        {
            var deer = Dm.each<mob_living_basic_deer>().Where(d => is_area_virtual(get_area(d))).ToList();
            _output.WriteLine($"grasslands hunt: {server.exit_turfs.len} exits, {deer.Count} deer, crate goal {domain.main_crate_point_goal}");
            Assert.True(server.exit_turfs.len >= 1, "no exit");
            Assert.NotEmpty(deer); // deer: max 1 per segment, SPAWN_LIKELY (85%)
            Assert.Equal(0f, domain.main_crate_points);
            Waiting.NoSleep(deer[0].death(FALSE));
            Assert.Equal(3.5f, domain.main_crate_points);
            server.reset(fast: TRUE);
            Assert.Null(server.generated_domain);
        }
        var runtimes = _meta.Log.Runtimes.Skip(runtimesBefore).Where(e => (e.ToString() ?? "").Contains("bitrunning", StringComparison.Ordinal)).ToList();
        foreach (var e in runtimes)
            _output.WriteLine("runtime: " + (e.Message.Length > 300 ? e.Message[..300] : e.Message));
        Assert.Empty(runtimes);
    }
}
