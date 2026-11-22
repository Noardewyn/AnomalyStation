using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Traits;

/// <summary>
/// Trait sources and the trait queries of code/__DEFINES/traits/_traits.dm, written from the DM source: what each
/// removal macro does with the sources it is given (null, one value, a list), ROUNDSTART_TRAIT, the kinds of values tg
/// uses as sources (text, a typepath, REF() text, a datum), and the queries on null.
/// </summary>
public class TraitSourceTests
{
    private const string TRAIT_A = "pilot_trait_a";
    private const string TRAIT_B = "pilot_trait_b";

    private static string Sources(datum target, object trait) => test_traits_listener.Sources(target, trait);

    [Fact]
    public void RemoveTraitWithNullSourcesRemovesEverySourceButRoundstart()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum target = Dm.@new<datum>();
            test_traits_listener listener = Dm.@new<test_traits_listener>();
            listener.trace = trace;
            listener.Listen(target, TRAIT_A);
            ADD_TRAIT(target, TRAIT_A, "source_1");
            ADD_TRAIT(target, TRAIT_A, ROUNDSTART_TRAIT);
            ADD_TRAIT(target, TRAIT_A, "source_2");
            trace.Clear();

            // if ((!_S && (_T != ROUNDSTART_TRAIT)) || (_T in _S)) _L[trait] -= _T   (:34-35)
            REMOVE_TRAIT(target, TRAIT_A, null);
            Assert.Equal("[roundstart]", Sources(target, TRAIT_A));
            Assert.True(HAS_TRAIT(target, TRAIT_A));
            Assert.True(HAS_TRAIT_FROM_ONLY(target, TRAIT_A, ROUNDSTART_TRAIT));
            Assert.Empty(trace);

            // Nothing else to remove: the roundstart source stays and no signal is sent.
            REMOVE_TRAIT(target, TRAIT_A, null);
            Assert.Equal("[roundstart]", Sources(target, TRAIT_A));
            Assert.Empty(trace);

            // Named explicitly, ROUNDSTART_TRAIT is removed like any source.
            REMOVE_TRAIT(target, TRAIT_A, ROUNDSTART_TRAIT);
            Assert.False(HAS_TRAIT(target, TRAIT_A));
            Assert.Null(target._status_traits);
            Assert.Single(trace);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void OnlyRemoveTraitProtectsRoundstart()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            datum target = Dm.@new<datum>();
            ADD_TRAIT(target, TRAIT_A, ROUNDSTART_TRAIT);
            ADD_TRAIT(target, TRAIT_A, "source_1");

            // REMOVE_TRAIT_NOT_FROM has no ROUNDSTART_TRAIT case: with null sources nothing is "in" them (:58).
            REMOVE_TRAIT_NOT_FROM(target, TRAIT_A, null);
            Assert.False(HAS_TRAIT(target, TRAIT_A));
            Assert.Null(target._status_traits);

            // REMOVE_TRAITS_IN with null sources removes nothing: _L[_T] -= null (:103).
            ADD_TRAIT(target, TRAIT_A, ROUNDSTART_TRAIT);
            ADD_TRAIT(target, TRAIT_A, "source_1");
            REMOVE_TRAITS_IN(target, null);
            Assert.Equal("[roundstart,source_1]", Sources(target, TRAIT_A));

            // REMOVE_TRAITS_NOT_IN keeps exactly the given sources, roundstart or not (:78).
            REMOVE_TRAITS_NOT_IN(target, Dm.list("source_1"));
            Assert.Equal("[source_1]", Sources(target, TRAIT_A));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void RemoveTraitTakesOneSourceOrAListOfSources()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            datum target = Dm.@new<datum>();
            foreach (string source in new[] { "s1", "s2", "s3", "s4" })
                ADD_TRAIT(target, TRAIT_A, source);

            // if (sources && !islist(sources)) _S = list(sources) else _S = sources   (:27-31)
            REMOVE_TRAIT(target, TRAIT_A, "s2");
            Assert.Equal("[s1,s3,s4]", Sources(target, TRAIT_A));
            REMOVE_TRAIT(target, TRAIT_A, Dm.list("s4", "s1", "s9"));
            Assert.Equal("[s3]", Sources(target, TRAIT_A));
            // The caller's list is not changed.
            DmList given = Dm.list("s3");
            REMOVE_TRAIT(target, TRAIT_A, given);
            Assert.Equal(1f, given.len);
            Assert.Null(target._status_traits);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void RemoveTraitNotFromKeepsOnlyTheGivenSources()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            datum target = Dm.@new<datum>();
            foreach (string source in new[] { "s1", "s2", "s3" })
                ADD_TRAIT(target, TRAIT_A, source);

            REMOVE_TRAIT_NOT_FROM(target, TRAIT_A, Dm.list("s3", "s1"));
            Assert.Equal("[s1,s3]", Sources(target, TRAIT_A));
            REMOVE_TRAIT_NOT_FROM(target, TRAIT_A, "s3");
            Assert.Equal("[s3]", Sources(target, TRAIT_A));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TheMultiTraitRemovalsTakeOneSourceOrAList()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            datum target = Dm.@new<datum>();
            ADD_TRAIT(target, TRAIT_A, "s1");
            ADD_TRAIT(target, TRAIT_A, "s2");
            ADD_TRAIT(target, TRAIT_A, "s3");
            ADD_TRAIT(target, TRAIT_B, "s2");
            ADD_TRAIT(target, TRAIT_B, "s3");

            // REMOVE_TRAITS_NOT_IN takes sources as they are: _L[_T] &= _S (:74, :78), a single value keeps that value.
            REMOVE_TRAITS_NOT_IN(target, Dm.list("s1", "s2"));
            Assert.Equal("[s1,s2]", Sources(target, TRAIT_A));
            Assert.Equal("[s2]", Sources(target, TRAIT_B));
            REMOVE_TRAITS_NOT_IN(target, "s2");
            Assert.Equal("[s2]", Sources(target, TRAIT_A));

            // REMOVE_TRAITS_IN wraps a single value in a list (:95-96) and removes the listed sources everywhere (:103).
            ADD_TRAIT(target, TRAIT_A, "s4");
            ADD_TRAIT(target, TRAIT_B, "s4");
            REMOVE_TRAITS_IN(target, "s2");
            Assert.Equal("[s4]", Sources(target, TRAIT_A));
            Assert.Equal("[s4]", Sources(target, TRAIT_B));
            REMOVE_TRAITS_IN(target, Dm.list("s9", "s4"));
            Assert.Null(target._status_traits);
            Assert.Empty(t.Runtimes);
        }
    }

    /// <summary>
    /// The kinds of values tg passes as a source: text (most defines), a typepath (<c>type</c>), the text of a
    /// <c>REF()</c> and a datum (<c>src</c>). Every macro compares them with DM <c>==</c> (<c>in</c>, <c>-=</c>,
    /// <c>|=</c>, <c>&amp;=</c>): text by value, a typepath and a datum by identity.
    /// </summary>
    [Theory]
    [InlineData("text")]
    [InlineData("typepath")]
    [InlineData("ref")]
    [InlineData("datum")]
    public void SourcesOfEveryKindAreAddedQueriedAndRemoved(string kind)
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum target = Dm.@new<datum>();
            test_traits_listener listener = Dm.@new<test_traits_listener>();
            listener.trace = trace;
            listener.Listen(target, TRAIT_A);
            datum owner = Dm.@new<test_traits_source_kind>();
            datum stranger = Dm.@new<test_traits_source_kind>();

            (object source, object same, object other) = kind switch
            {
                "text" => ((object)"source_text", (object)new string("source_text".AsSpan()), (object)"source_other"),
                "typepath" => (owner.type, typeof(test_traits_source_kind), (object)typeof(datum)),
                // REF() renders a new text each time; DM compares text by value.
                "ref" => (REF(owner), new string(((string)REF(owner)).AsSpan()), REF(stranger)),
                _ => (owner, owner, stranger),
            };

            ADD_TRAIT(target, TRAIT_A, source);
            ADD_TRAIT(target, TRAIT_A, same); // the same source again: |= keeps one (:16)
            Assert.Equal(1f, COUNT_TRAIT_SOURCES(target, TRAIT_A));
            Assert.True(HAS_TRAIT_FROM(target, TRAIT_A, same));
            Assert.True(HAS_TRAIT_FROM_ONLY(target, TRAIT_A, same));
            Assert.False(HAS_TRAIT_NOT_FROM(target, TRAIT_A, same));
            Assert.False(HAS_TRAIT_FROM(target, TRAIT_A, other));
            Assert.True(HAS_TRAIT_NOT_FROM(target, TRAIT_A, other));

            ADD_TRAIT(target, TRAIT_A, other);
            Assert.False(HAS_TRAIT_FROM_ONLY(target, TRAIT_A, same));
            REMOVE_TRAIT(target, TRAIT_A, same);
            Assert.True(HAS_TRAIT_FROM_ONLY(target, TRAIT_A, other));
            REMOVE_TRAIT_NOT_FROM(target, TRAIT_A, other);
            Assert.True(HAS_TRAIT_FROM_ONLY(target, TRAIT_A, other));
            REMOVE_TRAITS_IN(target, other);
            Assert.False(HAS_TRAIT(target, TRAIT_A));

            ADD_TRAIT(target, TRAIT_A, source);
            ADD_TRAIT(target, TRAIT_A, other);
            REMOVE_TRAITS_NOT_IN(target, same);
            Assert.True(HAS_TRAIT_FROM_ONLY(target, TRAIT_A, source));

            // A typepath source is not its text, and a REF() text is not the datum: different DM kinds never compare equal.
            if (kind == "typepath")
                Assert.False(HAS_TRAIT_FROM(target, TRAIT_A, Dm.text("[]", source)));
            if (kind == "ref")
                Assert.False(HAS_TRAIT_FROM(target, TRAIT_A, owner));

            REMOVE_TRAIT(target, TRAIT_A, same);
            Assert.Null(target._status_traits);
            Assert.Equal(new[] { "add", "remove", "add", "remove" }, trace.Select(line => line.Split(' ')[1]));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ARemovalListMayMixSourceKinds()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            datum target = Dm.@new<datum>();
            datum owner = Dm.@new<test_traits_source_kind>();
            ADD_TRAIT(target, TRAIT_A, "source_text");
            ADD_TRAIT(target, TRAIT_A, owner.type);
            ADD_TRAIT(target, TRAIT_A, REF(owner));
            ADD_TRAIT(target, TRAIT_A, owner);
            ADD_TRAIT(target, TRAIT_A, "source_kept");

            REMOVE_TRAIT(target, TRAIT_A, Dm.list(owner, REF(owner), "source_text", typeof(test_traits_source_kind)));
            Assert.Equal("[source_kept]", Sources(target, TRAIT_A));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TheQueriesOnNullAndOnADatumWithoutTheTraitAreFalse()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            // #define HAS_TRAIT(target, trait) (target?._status_traits?[trait] ? TRUE : FALSE)   (:116)
            Assert.False(HAS_TRAIT(null, TRAIT_A));
            Assert.False(HAS_TRAIT_FROM(null, TRAIT_A, "s1"));
            Assert.False(HAS_TRAIT_FROM_ONLY(null, TRAIT_A, "s1"));
            Assert.False(HAS_TRAIT_NOT_FROM(null, TRAIT_A, "s1"));
            // (target?._status_traits?[trait] || list())   (:122): a new empty list each time.
            object none = GET_TRAIT_SOURCES(null, TRAIT_A);
            Assert.IsType<DmList>(none);
            Assert.Equal(0f, ((DmList)none).len);
            Assert.NotSame(none, GET_TRAIT_SOURCES(null, TRAIT_A));
            Assert.Equal(0f, COUNT_TRAIT_SOURCES(null, TRAIT_A));

            // A datum that never had a trait (_status_traits is null) and one that has others.
            datum bare = Dm.@new<datum>();
            datum other = Dm.@new<datum>();
            ADD_TRAIT(other, TRAIT_B, "s1");
            foreach (datum target in new[] { bare, other })
            {
                Assert.False(HAS_TRAIT(target, TRAIT_A));
                Assert.False(HAS_TRAIT_FROM(target, TRAIT_A, "s1"));
                Assert.False(HAS_TRAIT_FROM_ONLY(target, TRAIT_A, "s1"));
                Assert.False(HAS_TRAIT_NOT_FROM(target, TRAIT_A, "s1"));
                Assert.Equal(0f, COUNT_TRAIT_SOURCES(target, TRAIT_A));
            }
            Assert.Null(bare._status_traits);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void HasTraitIsTrueNotTheSourceListAndGetTraitSourcesIsTheLiveList()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            datum target = Dm.@new<datum>();
            ADD_TRAIT(target, TRAIT_A, "s1");
            ADD_TRAIT(target, TRAIT_A, "s2");

            Assert.True(HAS_TRAIT(target, TRAIT_A));
            // (c ? TRUE : FALSE) is a condition, a bool member (rules 11.1, v1.2); as a DM value it is TRUE or FALSE
            // (rules 4.1: Dm.num), never the source list.
            Assert.Equal(TRUE, Dm.num(HAS_TRAIT(target, TRAIT_A)));
            Assert.Equal(FALSE, Dm.num(HAS_TRAIT(target, TRAIT_B)));
            Assert.Equal(TRUE, Dm.num(HAS_MIND_TRAIT(target, TRAIT_A)));
            // GET_TRAIT_SOURCES hands out the stored list itself ("Only useful for wacko cases and internal futzing").
            Assert.Same(target._status_traits[TRAIT_A], GET_TRAIT_SOURCES(target, TRAIT_A));
            Assert.Equal(2f, COUNT_TRAIT_SOURCES(target, TRAIT_A));
            // HAS_TRAIT_NOT_FROM: some source other than the given one (:119).
            Assert.True(HAS_TRAIT_NOT_FROM(target, TRAIT_A, "s1"));
            REMOVE_TRAIT(target, TRAIT_A, "s2");
            Assert.False(HAS_TRAIT_NOT_FROM(target, TRAIT_A, "s1"));
            Assert.True(HAS_TRAIT_NOT_FROM(target, TRAIT_A, "s2"));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void HasMindTraitChecksTheMobThenItsMind()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            // (HAS_TRAIT(target, trait) || (target.mind ? HAS_TRAIT(target.mind, trait) : FALSE))   (:126)
            mob M = Dm.@new<mob>();
            Assert.False(HAS_MIND_TRAIT(M, TRAIT_A));
            M.mind = Dm.@new<datum_mind_test_bare_mind>(); // a bare world has no SSjob for a mind's New (TraitFixtures)
            Assert.False(HAS_MIND_TRAIT(M, TRAIT_A));
            ADD_TRAIT(M.mind, TRAIT_A, "s1");
            Assert.True(HAS_MIND_TRAIT(M, TRAIT_A));
            Assert.False(HAS_TRAIT(M, TRAIT_A));
            ADD_TRAIT(M, TRAIT_B, "s1");
            M.mind = null;
            Assert.True(HAS_MIND_TRAIT(M, TRAIT_B));
            Assert.False(HAS_MIND_TRAIT(M, TRAIT_A));
            Assert.Empty(t.Runtimes);
        }
    }
}
