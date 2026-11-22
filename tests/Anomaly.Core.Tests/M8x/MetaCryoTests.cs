using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M8x;

/// <summary>
/// M8x slice A on the booted MetaStation (docs/ai/specs/cryo.md risk R1): the two cryo cells of the Cryo room (80,111 and 82,111)
/// find their cyan manifold through their connector at the map load and share its pipenet. As in tg, the room's two anesthetic
/// canisters stand unconnected on the loop's ports at the round start (a portable connects only when wrenched,
/// portable_atmospherics.dm:280; nothing connects one at mapload), so the loop and both cells are empty; a canister connected to
/// its port fills the loop, and each cell then holds more than CRYO_MIN_GAS_MOLES (5) and can run.
/// </summary>
public class MetaCryoTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;

    public MetaCryoTests(MetaWorldFixture meta) => _meta = meta;

    private void StepAir(int fires)
    {
        float Fired()
        {
            using (_meta.World.Enter())
                return SSair.times_fired;
        }
        float start = Fired();
        for (int t = 0; t < 60 * fires && Fired() < start + fires; t++)
            _meta.World.Step();
        Assert.True(Fired() >= start + fires, $"SSair fired {fires} times");
    }

    private static obj_machinery_cryo_cell Cell(int x) => Dm.locate<obj_machinery_cryo_cell>(locate(x, 111, 1));

    private static float Moles(int x) => Dm.num(((datum_gas_mixture)Cell(x).internal_connector.gas_connector.airs[1]).total_moles());

    [MetaFact]
    public void BothCellsJoinTheCryoLoopAndHoldItsGasOnceACanisterIsConnected()
    {
        TgTestWorld.RunLevel(_meta.World, RUNLEVEL_GAME);
        StepAir(10);
        using (_meta.World.Enter())
        {
            foreach (int x in new[] { 80, 82 })
            {
                var cell = Cell(x);
                Assert.NotNull(cell);
                var connector = cell.internal_connector.gas_connector;
                Assert.Equal(SOUTH, connector.dir);
                var node = connector.nodes[1] as obj_machinery_atmospherics_pipe;
                Assert.NotNull(node);
                Assert.Same(locate(x, 110, 1), node.loc);
                Assert.NotNull(connector.parents[1]);
                Assert.Same(node.parent, connector.parents[1]);
                Assert.Equal("pod-off", cell.icon_state);
                Assert.Equal(0f, Moles(x)); // tg's round start: the canisters are not connected
            }
            Assert.Same(Cell(80).internal_connector.gas_connector.parents[1], Cell(82).internal_connector.gas_connector.parents[1]);

            // a doctor wrenches the canister at 81,107 onto its port (portable_atmospherics.dm:296 connect)
            var canister = Dm.locate<obj_machinery_portable_atmospherics_canister_anesthetic_mix>(locate(81, 107, 1));
            var port = Dm.locate<obj_machinery_atmospherics_components_unary_portables_connector>(locate(81, 107, 1));
            Assert.NotNull(canister);
            Assert.NotNull(port);
            Assert.True(Dm.truthy(Waiting.NoSleep(canister.connect(port))));
        }
        StepAir(10);
        using (_meta.World.Enter())
        {
            foreach (int x in new[] { 80, 82 })
                Assert.True(Moles(x) > 5, $"cell {x},111 holds {Moles(x)} moles");
        }
    }
}
