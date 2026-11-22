using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Traits;

// Test datums for the behaviour tests of the traits package (wave 2 pilot), written from the DM source of
// code/__DEFINES/traits/_traits.dm and code/__HELPERS/traits.dm. They are shaped like translated tg types (snake_case
// DM names, a fixture [DmPath] /datum/<kind>/test_traits_<name> of translation-rules.md 15, which no tg path starts
// with) and record what happens to them in a shared trace, the recording harness of translation-rules.md 15. A signal
// handler runs inside _SendSignal's isolation boundary (signals.dm:113), so handlers only record and the tests assert
// afterwards.

/// <summary>A world for one trait test; runtimes are collected instead of thrown.</summary>
/// <summary>
/// A mind for the bare trait worlds (the ghosts, D151): tg's /datum/mind/New reads SSjob (_mind.dm:116), which a world without
/// the Master and the configuration lacks; the trait tests need only the datum.
/// </summary>
[DmPath("/datum/mind/test_bare_mind")]
public class datum_mind_test_bare_mind : datum_mind
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

internal sealed class TraitTestWorld
{
    public readonly World World;
    public readonly List<Exception> Runtimes = new();

    public TraitTestWorld(ulong seed = 11)
    {
        World = new World(seed) { RuntimeErrorSink = Runtimes.Add };
        AtomsStandIn.Install(World); // GLOB and SSatoms, which /atom/New reads (wave 3)
    }

    /// <summary>The runtime messages reported so far, "Type: message".</summary>
    public List<string> Messages => Runtimes.Select(e => e.GetType().Name + ": " + e.Message).ToList();
}

/// <summary>
/// Listens for SIGNAL_ADDTRAIT / SIGNAL_REMOVETRAIT and records, for each signal, the arguments and the trait state
/// of the source as the macro left it at the moment it sent the signal.
/// </summary>
[DmPath("/datum/test_traits_listener")]
public class test_traits_listener : datum
{
    [NotHashed]
    public List<string> trace;

    public string label = "listener";

    /// <summary>A trait the reacting handlers add to the source.</summary>
    public object react_trait;

    /// <summary>The source the reacting handlers add it from.</summary>
    public object react_source;

    /// <summary>Registers this listener for both trait signals of <paramref name="trait"/> on <paramref name="target"/>.</summary>
    public void Listen(datum target, object trait)
    {
        RegisterSignal(target, SIGNAL_ADDTRAIT(trait), nameof(on_addtrait));
        RegisterSignal(target, SIGNAL_REMOVETRAIT(trait), nameof(on_removetrait));
    }

    [SignalHandler]
    public virtual void on_addtrait(datum source = null, object trait = null)
    {
        trace?.Add(label + " add " + State(source, trait));
    }

    [SignalHandler]
    public virtual void on_removetrait(datum source = null, object trait = null)
    {
        trace?.Add(label + " remove " + State(source, trait));
    }

    /// <summary>The usr each <see cref="on_removetrait_usr"/> call saw.</summary>
    [NotHashed]
    public List<object> seen_usr;

    [SignalHandler]
    public virtual void on_removetrait_usr(datum source = null, object trait = null)
    {
        trace?.Add(label + " remove " + State(source, trait));
        seen_usr?.Add(usr);
    }

    /// <summary>On SIGNAL_REMOVETRAIT: records, then adds <see cref="react_trait"/> to the source.</summary>
    [SignalHandler]
    public virtual void on_removetrait_add_other(datum source = null, object trait = null)
    {
        trace?.Add(label + " remove " + State(source, trait));
        ADD_TRAIT(source, react_trait, react_source);
    }

    /// <summary>On SIGNAL_ADDTRAIT: records, then adds the same trait again from <see cref="react_source"/>.</summary>
    [SignalHandler]
    public virtual void on_addtrait_add_again(datum source = null, object trait = null)
    {
        trace?.Add(label + " add " + State(source, trait));
        ADD_TRAIT(source, trait, react_source);
    }

    /// <summary>A list the appending handler changes.</summary>
    public DmList react_list;

    /// <summary>On SIGNAL_ADDTRAIT: records, then appends <see cref="react_trait"/> to <see cref="react_list"/>.</summary>
    [SignalHandler]
    public virtual void on_addtrait_append(datum source = null, object trait = null)
    {
        trace?.Add(label + " add " + State(source, trait));
        react_list.Add(react_trait);
    }

    /// <summary>
    /// "trait_a source=#3 has=1 sources=[a,b] status=1": the signal's arguments, HAS_TRAIT, the trait's source list and
    /// the length of _status_traits ("null" once the macro has dropped the list).
    /// </summary>
    public static string State(datum source, object trait)
        => Dm.str(trait) + " source=" + Describe(source)
            + " has=" + Dm.str(Dm.num(HAS_TRAIT(source, trait)))
            + " sources=" + Sources(source, trait)
            + " status=" + (source._status_traits == null ? "null" : Dm.str(source._status_traits.len));

    /// <summary>The trait's source list in order, "[a,b]", or "none".</summary>
    public static string Sources(datum target, object trait)
    {
        object sources = target._status_traits?[trait];
        if (sources == null)
            return "none";
        return "[" + string.Join(",", Dm.each(sources).Select(Describe)) + "]";
    }

    public static string Describe(object value) => value switch
    {
        null => "null",
        datum d => "#" + d.dm_id,
        Type t => "path:" + Dm.str(t),
        _ => Dm.str(value),
    };
}

/// <summary>A datum subtype that stands for a typepath source (tg passes <c>type</c> as a trait source).</summary>
[DmPath("/datum/test_traits_source_kind")]
public class test_traits_source_kind : datum
{
}

/// <summary>A datum whose text (its <c>operator""</c>, rules 7.10) counts how often it is rendered.</summary>
[DmPath("/datum/test_traits_text_counter")]
public class test_traits_text_counter : datum
{
    [NotHashed]
    public int renders;

    public object operator__text()
    {
        renders++;
        return "counter";
    }
}
