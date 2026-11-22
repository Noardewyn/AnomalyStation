using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Contracts;

/// <summary>
/// A fixture subsystem of the test assembly (rules 15: a port fixture's path starts with test_): the type registry sees
/// it, so subtypesof(/datum/controller/subsystem) lists it, and only the per-world filter keeps Master/New from
/// creating it in every booted world.
/// </summary>
[DmPath("/datum/controller/subsystem/test_contracts_fixture")]
public sealed class datum_controller_subsystem_test_contracts_fixture : datum_controller_subsystem
{
}

/// <summary>
/// The Tg infrastructure of the wave-3 contracts that Master/New uses (TgState.Subsystems.cs): the per-world
/// subsystem-type filter (lead decision W3 G2) and the global.vars scan over TgState's subsystem fields (G3).
/// </summary>
public class SubsystemSeamTests
{
    [Fact]
    public void TheFilterKeepsOnlyTgSubsystemTypesUnlessATestSetsIt()
    {
        var w = new World();
        using (w.Enter())
        {
            DmList all = subtypesof(typeof(datum_controller_subsystem));
            Assert.True(Dm.@in(typeof(datum_controller_subsystem_test_contracts_fixture), all)); // the registry sees fixtures
            Assert.True(Dm.@in(typeof(datum_controller_subsystem_garbage), all));

            DmList kept = TgState.Current.FilterSubsystemTypes(all);
            Assert.False(Dm.@in(typeof(datum_controller_subsystem_test_contracts_fixture), kept));
            Assert.True(Dm.@in(typeof(datum_controller_subsystem_garbage), kept));
            Assert.True(kept.Count < all.Count);
            var order = Dm.each(all).Cast<object>().ToList();
            var keptItems = Dm.each(kept).Cast<object>().ToList();
            Assert.Equal(keptItems.OrderBy(order.IndexOf).ToList(), keptItems); // the order of subtypesof is kept

            TgState.Current.SubsystemTypeFilter = t => t == typeof(datum_controller_subsystem_test_contracts_fixture);
            DmList fixturesOnly = TgState.Current.FilterSubsystemTypes(all);
            Assert.Equal(new object[] { typeof(datum_controller_subsystem_test_contracts_fixture) }, Dm.each(fixturesOnly).Cast<object>().ToArray());
        }
        using (new World().Enter())
            Assert.Null(TgState.Current.SubsystemTypeFilter); // per world
    }

    [Fact]
    public void TheFilterIsNotWorldState()
    {
        var a = new World(1);
        var b = new World(1);
        using (a.Enter())
            TgState.Current.SubsystemTypeFilter = _ => true;
        using (b.Enter())
            _ = TgState.Current.GLOB;
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
    }

    [Fact]
    public void TheGlobalVarsScanListsTheSubsystemsHeldByTgStateFields()
    {
        var w = new World();
        using (w.Enter())
        {
            Assert.Equal(0, global_vars__subsystems().Count); // a first boot finds nothing (risk R9)
            var garbage = Dm.@new<datum_controller_subsystem_garbage>(); // SUBSYSTEM_DEF's New sets SSgarbage
            var dcs = Dm.@new<datum_controller_subsystem_processing_dcs>();
            DmList found = global_vars__subsystems();
            Assert.Equal(2, found.Count);
            Assert.True(Dm.@in(garbage, found));
            Assert.True(Dm.@in(dcs, found));
            Assert.Same(dcs, found[1]); // in ordinal order of the field names: SSdcs, SSgarbage
            Assert.Same(garbage, found[2]);
            SSgarbage = null;
            Assert.Equal(1, global_vars__subsystems().Count);
        }
    }
}
