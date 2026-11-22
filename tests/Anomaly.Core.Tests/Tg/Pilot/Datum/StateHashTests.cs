using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Datum;

/// <summary>
/// The datum part of the world state hash (docs/ai/specs/object-model.md section 8; translation-rules.md 15: the same
/// scenario twice gives the same <c>World.ComputeStateHash()</c>): every hashed datum var is part of the hash, and
/// nothing else is.
/// </summary>
public class StateHashTests
{
    // A scenario that touches everything of the package: signals both ways, weakrefs, callbacks with a user, a tag,
    // cooldowns, a qdel'd datum still referenced, a LETMELIVE datum, a hard delete, garbage that the GC collects.
    // keep holds the scenario's objects until the hash is taken (the registry's entries are weak, object-model.md 6),
    // so that what is hashed does not depend on how long the JIT keeps locals alive.
    private static ulong Scenario(Action<test_datum_listener> change = null, ulong seed = 11)
    {
        var keep = new List<object>();
        var t = new DatumTestWorld(seed);
        using (t.World.Enter())
        {
            t.World.Step(3);
            datum source = Dm.@new<datum>();
            test_datum_listener a = Dm.@new<test_datum_listener>();
            test_datum_listener b = Dm.@new<test_datum_listener>();
            a.label = "a";
            b.label = "b";
            a.RegisterSignal(source, "pilot_ping", nameof(test_datum_listener.on_signal));
            b.RegisterSignal(source, "pilot_ping", nameof(test_datum_listener.on_signal));
            source.RegisterSignal(a, COMSIG_QDELETING, nameof(datum.selfdelete));
            WEAKREF(a);
            a.tag = "pilot_hash_tag";
            source.cooldowns = Dm.list(Dm.kv("pilot", -1));
            test_datum_user user = Dm.@new<test_datum_user>();
            usr = user;
            a.other = Dm.@new<datum_callback>(b, nameof(test_datum_listener.record_call), 1, "x");
            usr = null;
            DatumTestMacros.SEND_SIGNAL(source, "pilot_ping", "x", 2);

            test_datum_destroyable gone = Dm.@new<test_datum_destroyable>();
            b.other = gone;
            qdel(gone);
            test_datum_destroyable immortal = Dm.@new<test_datum_destroyable>();
            immortal.hint = QDEL_HINT_LETMELIVE;
            qdel(immortal);
            b.other_target = immortal;
            test_datum_destroyable hard = Dm.@new<test_datum_destroyable>();
            hard.hint = QDEL_HINT_HARDDEL_NOW;
            qdel(hard);
            for (int i = 0; i < 5; i++)
                Dm.@new<datum_callback>(a, nameof(test_datum_listener.record_call)); // unreferenced: collected before hashing

            change?.Invoke(a);
            keep.AddRange(new object[] { source, a, b, user, immortal });
        }
        ulong hash = t.World.ComputeStateHash();
        GC.KeepAlive(keep);
        return hash;
    }

    [Fact]
    public void TheSameScenarioTwiceGivesTheSameHash()
    {
        ulong first = Scenario();
        Assert.Equal(first, Scenario());
        Assert.NotEqual(first, Scenario(seed: 12));
    }

    public static IEnumerable<object[]> Changes() => new[]
    {
        new object[] { "a datum var (datum_flags)", (Action<test_datum_listener>)(a => a.datum_flags = DF_ISPROCESSING) },
        new object[] { "a typepath var (abstract_type)", (Action<test_datum_listener>)(a => a.abstract_type = typeof(test_datum_listener)) },
        new object[] { "the tag", (Action<test_datum_listener>)(a => a.tag = "other_tag") },
        new object[] { "a subtype's own var", (Action<test_datum_listener>)(a => a.counter = 1) },
        new object[] { "a text var", (Action<test_datum_listener>)(a => a.label = "renamed") },
        new object[] { "gc_destroyed", (Action<test_datum_listener>)(a => a.gc_destroyed = 0) },
        new object[] { "an item of a list var", (Action<test_datum_listener>)(a => ((DmList)a._signal_procs[a._signal_procs[1]])["pilot_ping"] = "on_signal_no_params") },
        new object[] { "a registration", (Action<test_datum_listener>)(a => a.RegisterSignal(a, "pilot_self", nameof(test_datum_listener.on_signal))) },
        new object[] { "a reference", (Action<test_datum_listener>)(a => a.other_target = a) },
        new object[] { "a var that held null", (Action<test_datum_listener>)(a => a.cooldowns = Dm.list()) },
    };

    [Theory]
    [MemberData(nameof(Changes))]
    public void AChangedVarChangesTheHash(string what, Action<test_datum_listener> change)
    {
        Assert.NotEqual(Scenario(), Scenario(change));
        Assert.Equal(Scenario(change), Scenario(change)); // and the changed scenario is deterministic too ({what})
        Assert.NotNull(what);
    }

    [Fact]
    public void NotHashedFieldsAndUnreferencedGarbageDoNotChangeTheHash()
    {
        ulong baseline = Scenario();
        Assert.Equal(baseline, Scenario(a => a.trace = new List<string> { "host-side trace" }));
        Assert.Equal(baseline, Scenario(a => a.seen_usr = new List<object> { a }));
    }

    [Fact]
    public void TgStateIsHashedButCreatingItAddsNothing()
    {
        // keep holds what the scenario made, so that the registry (weak entries) is the same in every run.
        static ulong Run(Action<List<object>> change)
        {
            var keep = new List<object>();
            var t = new DatumTestWorld(withGarbage: false);
            using (t.World.Enter())
            {
                keep.Add(Dm.@new<datum>());
                change(keep);
            }
            ulong hash = t.World.ComputeStateHash();
            GC.KeepAlive(keep);
            return hash;
        }

        // Reading a TgState field creates TgState. Its fields start at their DM initial values: the C# default, or the
        // initializer of a translated var/static (e.g. cmp_fish_fluid's fluids_priority list), which is real world state
        // as in DM, where statics exist from world start. Creating it the same way twice gives the same hash. (Once the
        // world bootstrap of the controllers wave creates TgState eagerly, lazy creation stops mattering.)
        Assert.Equal(Run(_ => Assert.Null(SSgarbage)), Run(_ => Assert.Null(SSgarbage)));

        // The same registered objects, with TgState's SSgarbage set or not: TgState's own contributor tells them apart.
        ulong withGlobal = Run(keep => keep.Add(Dm.@new<datum_controller_subsystem_garbage>()));
        ulong withoutGlobal = Run(keep =>
        {
            keep.Add(Dm.@new<datum_controller_subsystem_garbage>());
            SSgarbage = null;
        });
        Assert.NotEqual(withGlobal, withoutGlobal);
        Assert.Equal(withoutGlobal, Run(keep =>
        {
            keep.Add(Dm.@new<datum_controller_subsystem_garbage>());
            SSgarbage = null;
        }));
    }
}
