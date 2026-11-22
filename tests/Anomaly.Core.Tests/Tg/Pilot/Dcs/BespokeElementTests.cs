using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Dcs;

/// <summary>
/// ELEMENT_BESPOKE (__DEFINES/dcs/declarations.dm:23): one element per distinct argument list, keyed by
/// SSdcs.GetIdFromArguments (controllers/subsystem/dcs.dm:56-90) through GetElement (:35-48).
/// </summary>
public class BespokeElementTests
{
    private static object Id(params object[] arguments) => SSdcs.GetIdFromArguments(Dm.list(arguments));

    [Fact]
    public void EqualArgumentsShareAnElementAndDifferentOnesGetTheirOwn()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            test_dcs_host h2 = t.Host("H2");

            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_bespoke), 1, "x"));
            Waiting.NoSleep(h2.AddElement(typeof(test_dcs_bespoke), 1, "x"));
            Waiting.NoSleep(h2.AddElement(typeof(test_dcs_bespoke), 2, "x"));
            Waiting.NoSleep(h2.AddElement(typeof(test_dcs_bespoke), "x", 1));

            // list2params(list(type, 1, "x")): the hashed arguments in order, after the type (dcs.dm:58-90).
            Assert.Equal("%2fdatum%2felement%2ftest_dcs_bespoke&1&x", Id(typeof(test_dcs_bespoke), 1, "x"));
            Assert.Equal(3f, SSdcs.elements_by_type.len);
            object shared = SSdcs.elements_by_type[Id(typeof(test_dcs_bespoke), 1, "x")];
            Assert.IsType<test_dcs_bespoke>(shared);
            Assert.NotSame(shared, SSdcs.elements_by_type[Id(typeof(test_dcs_bespoke), 2, "x")]);
            Assert.NotSame(shared, SSdcs.elements_by_type[Id(typeof(test_dcs_bespoke), "x", 1)]);
            Assert.Same(shared, SSdcs.GetElement(Dm.list(typeof(test_dcs_bespoke), 1, "x")));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void WithoutArgumentsTheIdIsTheTypesText()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_bespoke)));

            // dcs.dm:43: length(arguments) == 1 ? "[arguments[1]]" : GetIdFromArguments(arguments)
            Assert.IsType<test_dcs_bespoke>(SSdcs.elements_by_type["/datum/element/test_dcs_bespoke"]);
            Assert.Null(SSdcs.elements_by_type[typeof(test_dcs_bespoke)]);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void NamedArgumentsComeAfterThePositionalOnesSortedByName()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");

            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_bespoke), 1, Dm.named("b", "y"), Dm.named("a", "x")));
            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_bespoke), 1, Dm.named("a", "x"), Dm.named("b", "y")));
            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_bespoke), 1, Dm.named("a", "x"), Dm.named("b", "z")));

            // dcs.dm:63-77, :86-88: named arguments are collected, sorted with cmp_text_asc and added last as key=value.
            object id = Id(typeof(test_dcs_bespoke), 1, Dm.kv("b", "y"), Dm.kv("a", "x"));
            Assert.Equal("%2fdatum%2felement%2ftest_dcs_bespoke&1&a=x&b=y", id);
            Assert.Equal(2f, SSdcs.elements_by_type.len);
            Assert.NotNull(SSdcs.elements_by_type[id]);
            Assert.NotNull(SSdcs.elements_by_type["%2fdatum%2felement%2ftest_dcs_bespoke&1&a=x&b=z"]);
            // Attach got them by name.
            Assert.Equal(new[]
            {
                "H1 Attach test_dcs_bespoke args=1,null,x,y",
                "H1 Attach test_dcs_bespoke args=1,null,x,y",
                "H1 Attach test_dcs_bespoke args=1,null,x,z",
            }, t.Trace);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void DatumArgumentsAreIdentifiedByTheirRef()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            datum d1 = Dm.@new<datum>();
            datum d2 = Dm.@new<datum>();

            // dcs.dm:68-71 (a named value that is neither text nor a number) and :84 (a positional one): REF().
            string ref1 = list2params(Dm.list(REF(d1))); // the ref text as list2params encodes it
            Assert.Equal("%2fdatum%2felement%2ftest_dcs_bespoke&" + ref1, Id(typeof(test_dcs_bespoke), d1));
            Assert.Equal("%2fdatum%2felement%2ftest_dcs_bespoke&a=" + ref1, Id(typeof(test_dcs_bespoke), Dm.kv("a", d1)));
            Assert.Equal(Id(typeof(test_dcs_bespoke), d1), Id(typeof(test_dcs_bespoke), d1));
            Assert.NotEqual(Id(typeof(test_dcs_bespoke), d1), Id(typeof(test_dcs_bespoke), d2));

            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_bespoke), d1));
            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_bespoke), d1));
            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_bespoke), d2));
            Assert.Equal(2f, SSdcs.elements_by_type.len);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void OnlyTheArgumentsInsideTheHashWindowCount()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");

            // argument_hash_start_idx = 2, argument_hash_end_idx = 3 (dcs.dm:60)
            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_bespoke_window), 1, 2, "first"));
            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_bespoke_window), 1, 2, "second"));
            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_bespoke_window), 1, 3, "first"));

            Assert.Equal("%2fdatum%2felement%2ftest_dcs_bespoke_window&1&2", Id(typeof(test_dcs_bespoke_window), 1, 2, "ignored"));
            Assert.Equal(2f, SSdcs.elements_by_type.len);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ArgumentsBeforeTheStartIndexDoNotCount()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            // A start index past the arguments hashes nothing but the type (INFINITY by default, :20).
            Assert.Equal("%2fdatum%2felement%2ftest_dcs_simple", SSdcs.GetIdFromArguments(Dm.list(typeof(test_dcs_simple), 1, 2)));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TgQuirkANumberAndItsTextAndANullNamedArgumentAndItsNameGiveTheSameId()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            // A positional text argument is looked up as a key (dcs.dm:63-66): with no value it goes into the id as is,
            // so it is indistinguishable from the number list2params writes the same way.
            Assert.Equal(Id(typeof(test_dcs_bespoke), 1), Id(typeof(test_dcs_bespoke), "1"));
            // "We assume that no one will pass in a named argument with a value of null" (:54): such an argument is
            // its bare name.
            Assert.Equal(Id(typeof(test_dcs_bespoke), "a"), Id(typeof(test_dcs_bespoke), Dm.kv("a", null)));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AListOrTypepathArgumentIsIdentifiedByItsRef()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            test_dcs_host h2 = t.Host("H2");

            // tg identifies a list argument by REF(list) like any datum (dcs.dm:71, :84; unit_tests/dcs_check_list_arguments.dm:2-4),
            // and a typepath argument by REF(typepath), BYOND's ref of the type. The port gives a list a per-world ref
            // number on its first ref() and a typepath a fixed one (object-model.md section 3), so the same list or
            // typepath shares an element and an equal but different list does not (what dcs_check_list_arguments warns of).
            DmList shared = Dm.list("a");
            DmList equal = Dm.list("a");
            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_bespoke), shared));
            Waiting.NoSleep(h2.AddElement(typeof(test_dcs_bespoke), shared));
            Waiting.NoSleep(h2.AddElement(typeof(test_dcs_bespoke), equal));
            Assert.Equal(2f, SSdcs.elements_by_type.len);
            Assert.Equal("%2fdatum%2felement%2ftest_dcs_bespoke&" + list2params(Dm.list(@ref(shared))), Id(typeof(test_dcs_bespoke), shared));
            Assert.NotEqual(Id(typeof(test_dcs_bespoke), shared), Id(typeof(test_dcs_bespoke), equal));
            Assert.Same(SSdcs.elements_by_type[Id(typeof(test_dcs_bespoke), shared)], SSdcs.GetElement(Dm.list(typeof(test_dcs_bespoke), shared)));

            // A named list argument is REF'd too (dcs.dm:71), and a typepath has one fixed ref.
            Assert.Equal("%2fdatum%2felement%2ftest_dcs_bespoke&" + list2params(Dm.list(Dm.kv("a", @ref(shared)))), Id(typeof(test_dcs_bespoke), Dm.kv("a", shared)));
            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_bespoke), typeof(datum)));
            Waiting.NoSleep(h2.AddElement(typeof(test_dcs_bespoke), typeof(datum)));
            Assert.Equal(3f, SSdcs.elements_by_type.len);
            Assert.Equal("%2fdatum%2felement%2ftest_dcs_bespoke&" + list2params(Dm.list(@ref(typeof(datum)))), Id(typeof(test_dcs_bespoke), typeof(datum)));

            // A null argument has a ref, "[0x0]" (BYOND's, Dm.ref).
            Assert.Equal("%2fdatum%2felement%2ftest_dcs_bespoke&" + list2params(Dm.list("[0x0]")), Id(typeof(test_dcs_bespoke), null));
            Assert.Empty(t.Runtimes);
        }
    }
}
