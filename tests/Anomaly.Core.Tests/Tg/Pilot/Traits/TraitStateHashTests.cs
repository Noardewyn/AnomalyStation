using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Traits;

/// <summary>
/// Traits in the world state hash (translation-rules.md 15: the same scenario twice gives the same
/// <c>World.ComputeStateHash()</c>): _status_traits is a datum var like any other, its trait and source order included.
/// </summary>
public class TraitStateHashTests
{
    private const string TRAIT_A = "pilot_trait_a";
    private const string TRAIT_B = "pilot_trait_b";

    // keep holds the scenario's objects until the hash is taken (the registry's entries are weak, object-model.md 6).
    private static ulong Scenario(Action<datum, datum> change = null)
    {
        var keep = new List<object>();
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            datum target = Dm.@new<datum>();
            datum owner = Dm.@new<test_traits_source_kind>();
            test_traits_listener listener = Dm.@new<test_traits_listener>();
            listener.Listen(target, TRAIT_A);
            ADD_TRAIT(target, TRAIT_A, "source_text");
            ADD_TRAIT(target, TRAIT_A, owner);
            ADD_TRAIT(target, TRAIT_B, owner.type);
            ADD_TRAIT(target, TRAIT_B, REF(owner));
            change?.Invoke(target, owner);
            keep.AddRange(new object[] { target, owner, listener });
            Assert.Empty(t.Runtimes);
        }
        ulong hash = t.World.ComputeStateHash();
        GC.KeepAlive(keep);
        return hash;
    }

    [Fact]
    public void TheSameTraitScenarioTwiceGivesTheSameHash()
    {
        Assert.Equal(Scenario(), Scenario());
        Assert.Equal(Scenario((target, owner) => REMOVE_TRAIT(target, TRAIT_A, null)), Scenario((target, owner) => REMOVE_TRAIT(target, TRAIT_A, null)));
    }

    public static IEnumerable<object[]> Changes() => new[]
    {
        new object[] { "another source", (Action<datum, datum>)((target, owner) => ADD_TRAIT(target, TRAIT_A, "source_more")) },
        new object[] { "a removed source", (Action<datum, datum>)((target, owner) => REMOVE_TRAIT(target, TRAIT_A, owner)) },
        new object[] { "a removed trait", (Action<datum, datum>)((target, owner) => REMOVE_TRAITS_IN(target, Dm.list(owner.type, REF(owner)))) },
        new object[] { "the order of the sources", (Action<datum, datum>)((target, owner) =>
        {
            REMOVE_TRAIT(target, TRAIT_A, "source_text");
            ADD_TRAIT(target, TRAIT_A, "source_text");
        }) },
        new object[] { "the order of the traits", (Action<datum, datum>)((target, owner) =>
        {
            REMOVE_TRAIT(target, TRAIT_A, null);
            ADD_TRAIT(target, TRAIT_A, "source_text");
            ADD_TRAIT(target, TRAIT_A, owner);
        }) },
    };

    [Theory]
    [MemberData(nameof(Changes))]
    public void AChangedTraitListChangesTheHash(string what, Action<datum, datum> change)
    {
        Assert.NotEqual(Scenario(), Scenario(change));
        Assert.Equal(Scenario(change), Scenario(change));
        Assert.NotNull(what);
    }

    [Fact]
    public void RemovingEveryTraitLeavesTheDatumAsIfItNeverHadOne()
    {
        // The removal macros drop an emptied _status_traits (target._status_traits = null, _traits.dm:43, :67, :86,
        // :111), so a datum whose traits all went hashes like one that never had any.
        static ulong Run(bool withTraits)
        {
            var keep = new List<object>();
            var t = new TraitTestWorld();
            using (t.World.Enter())
            {
                datum target = Dm.@new<datum>();
                datum other = Dm.@new<datum>();
                if (withTraits)
                {
                    ADD_TRAIT(target, TRAIT_A, "s1");
                    ADD_TRAIT(target, TRAIT_B, "s1");
                    ADD_TRAIT(other, TRAIT_A, "s2");
                    ADD_TRAIT(other, TRAIT_A, "s3");
                    REMOVE_TRAIT(target, TRAIT_A, "s1");
                    REMOVE_TRAITS_IN(target, "s1");
                    REMOVE_TRAIT_NOT_FROM(other, TRAIT_A, "s2");
                    REMOVE_TRAITS_NOT_IN(other, "s9");
                    Assert.Null(target._status_traits);
                    Assert.Null(other._status_traits);
                }
                keep.Add(target);
                keep.Add(other);
            }
            ulong hash = t.World.ComputeStateHash();
            GC.KeepAlive(keep);
            return hash;
        }

        Assert.Equal(Run(false), Run(true));
    }
}
