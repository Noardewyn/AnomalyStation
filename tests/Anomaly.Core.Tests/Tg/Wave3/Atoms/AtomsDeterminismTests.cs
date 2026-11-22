using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave3.Atoms;

/// <summary>
/// Determinism (rules 15): the same atoms scenario twice gives the same world state hash and the same order of events,
/// and the hash sees the atoms package's state.
/// </summary>
public class AtomsDeterminismTests
{
    // A map whose atoms wait for SSatoms, its Initialize with every hint, runtime-created atoms, a map template after
    // world time 10 with a qdel-before-init and a queued deletion. keep holds the scenario's objects until the hash is
    // taken (the registry's entries are weak, object-model.md 6).
    private static (ulong hash, List<string> trace) Scenario(ulong seed = 5, bool extra = false)
    {
        var keep = new List<object>();
        var t = new AtomsTestWorld(seed, maxx: 3, maxy: 2, maxz: 1);
        using (t.World.Enter())
        {
            keep.Add(Dm.@new<obj_test_atoms_probe_lateload>(locate(1, 1, 1)));
            keep.Add(Dm.@new<obj_test_atoms_probe>(locate(2, 1, 1)));
            keep.Add(Dm.@new<obj_test_atoms_probe_qdel>(locate(3, 1, 1)));
            keep.Add(Dm.@new<obj_test_atoms_probe_no_parent>(locate(1, 2, 1)));
            keep.Add(Dm.@new<obj_test_atoms_probe_self_deleting>(locate(2, 2, 1)));
            keep.Add(Dm.@new<obj_test_atoms_immediate_probe_lateload>(locate(3, 2, 1)));
            keep.Add(Dm.@new<obj_test_atoms_probe_spawner>(locate(3, 2, 1)));
            AtomsTestWorld.InitializeSSatoms();
            keep.Add(Dm.@new<obj_test_atoms_probe_lateload>(locate(1, 1, 1)));
            keep.Add(Dm.@new<obj_test_atoms_probe_no_hint>(locate(2, 1, 1)));
            var dense = Dm.@new<obj_test_atoms_probe>(locate(2, 2, 1));
            dense.set_density(TRUE);
            Waiting.NoSleep(dense.setDir(WEST));
            keep.Add(dense);
            if (extra)
                keep.Add(Dm.@new<obj_test_atoms_probe>(locate(1, 2, 1)));
        }
        t.World.StepDeciseconds(10);
        using (t.World.Enter())
        {
            SSatoms.map_loader_begin("template");
            var gone = Dm.@new<obj_test_atoms_probe>(locate(1, 1, 1));
            var doomed = Dm.@new<obj_test_atoms_probe_lateload>(locate(1, 1, 1));
            var stays = Dm.@new<obj_test_atoms_probe_lateload>(locate(2, 1, 1));
            qdel(gone);
            SSatoms.prepare_deletion(doomed);
            SSatoms.map_loader_stop("template");
            SSatoms.InitializeAtoms(Dm.list(gone, doomed, stays));
            keep.AddRange(new object[] { gone, doomed, stays });
        }
        ulong hash = t.World.ComputeStateHash();
        GC.KeepAlive(keep);
        return (hash, t.Trace.ToList());
    }

    [Fact]
    public void TheSameScenarioTwiceGivesTheSameStateHashAndTrace()
    {
        var first = Scenario();
        var second = Scenario();
        Assert.Equal(first.hash, second.hash);
        Assert.Equal(first.trace, second.trace);
        Assert.NotEmpty(first.trace);
    }

    [Fact]
    public void TheHashSeesOneMoreAtom()
    {
        Assert.NotEqual(Scenario().hash, Scenario(extra: true).hash);
    }

    [Fact]
    public void TheHashSeesTheBadInitRecords()
    {
        ulong Hash(bool record)
        {
            var t = new AtomsTestWorld(3);
            using (t.World.Enter())
            {
                AtomsTestWorld.InitializeSSatoms();
                if (record)
                    SSatoms.BadInitializeCalls[typeof(obj_test_atoms_probe)] = BAD_INIT_NO_HINT;
            }
            return t.World.ComputeStateHash();
        }
        Assert.Equal(Hash(false), Hash(false));
        Assert.NotEqual(Hash(false), Hash(true));
    }
}
