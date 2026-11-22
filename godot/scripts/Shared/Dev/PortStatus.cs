using System.IO;
using System.Linq;

namespace Anomaly.Client.Shared.Dev;

// The port status overlay (the owner, 2026-10-28: "a circle over every object, coloured by how much of its mechanics is ported,
// and what works and what does not on hover"). Engine-free. It reads the translation tracker, docs/ai/specs/generated/
// types-index.tsv (types-converter.md 3.9): a type's state (base, live, inert) and, per tg proc, whether the class declares it
// and what its body is. It knows code only: an icon state, a sound or a tgui window that is missing does not show here.

/// <summary>How much of a type's own mechanics is ported.</summary>
public enum PortLevel
{
    /// <summary>Not in the index (a client-only or generated type).</summary>
    Unknown,

    /// <summary>Inert, or one of tg's interaction entry points (<see cref="PortIndex.KeyProcs"/>) is not fully translated.</summary>
    Red,

    /// <summary>Live, but some of its own procs are stubs, skeletons, partial or deferred.</summary>
    Yellow,

    /// <summary>Every proc of its own is fully translated.</summary>
    Green,
}

/// <summary>A tg proc of a type, one <c>proc</c> row of the index.</summary>
/// <param name="Owner">The type that defines it.</param>
/// <param name="Where">tg's <c>file:line</c>.</param>
/// <param name="Translated"><c>yes</c>, <c>stub</c> or <c>no</c>.</param>
/// <param name="Body"><c>full</c>, <c>partial</c>, <c>skeleton</c>, <c>deferred</c>, <c>not_translated</c> or <c>-</c>.</param>
public readonly record struct PortProc(string Owner, string Name, string Where, string Translated, string Body)
{
    public bool Ported => Translated == "yes" && Body == "full";

    /// <summary>What the tooltip says of it.</summary>
    public string Status => Ported ? "ported" : Translated switch
    {
        "no" => "not translated",
        "stub" => "stub (" + Body + ")",
        _ => Body,
    };
}

/// <summary>A type's status.</summary>
/// <param name="Own">The procs of the type and of its ancestors below the nearest base type (the M0-M2 framework layer, which
/// <see cref="BaseGaps"/> counts apart so that it does not turn every item yellow).</param>
/// <param name="BaseGaps">The procs of the base ancestors that are not fully translated.</param>
public sealed record PortReport(string Path, string State, PortLevel Level, IReadOnlyList<PortProc> Own, int BaseGaps);

/// <summary>The tracker, read once.</summary>
public sealed class PortIndex
{
    /// <summary>tg's interaction entry points: one not fully translated leaves the object without its main use.</summary>
    public static readonly HashSet<string> KeyProcs = new(StringComparer.Ordinal)
    {
        "attack_hand", "attack_self", "attackby", "item_interaction", "interact_with_atom", "interact", "ui_interact", "ui_act",
        "attack", "afterattack", "process", "process_atmos", "Bumped",
    };

    private readonly Dictionary<string, TypeRow> _types = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PortReport> _reports = new(StringComparer.Ordinal);

    public int TypeCount => _types.Count;

    /// <summary>Reads the index's <c>type</c> and <c>proc</c> rows (comments and the global procs are skipped).</summary>
    public static PortIndex Parse(TextReader reader)
    {
        var index = new PortIndex();
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length == 0 || line[0] == '#')
                continue;
            string[] c = line.Split('\t');
            if (c[0] == "type" && c.Length >= 5)
                index._types[c[1]] = new TypeRow(c[2], c[4]);
            else if (c[0] == "proc" && c.Length >= 7 && index._types.TryGetValue(c[1], out TypeRow owner))
                owner.Procs.Add(new PortProc(c[1], c[2], c[3], c[5], c[6]));
        }
        return index;
    }

    /// <summary>The status of <paramref name="path"/> (cached).</summary>
    public PortReport Of(string path)
    {
        if (path == null)
            return new PortReport("?", "-", PortLevel.Unknown, Array.Empty<PortProc>(), 0);
        if (_reports.TryGetValue(path, out PortReport cached))
            return cached;
        PortReport report;
        if (!_types.TryGetValue(path, out TypeRow row))
        {
            report = new PortReport(path, "-", PortLevel.Unknown, Array.Empty<PortProc>(), 0);
        }
        else
        {
            var own = new List<PortProc>(row.Procs);
            TypeRow t = Parent(row);
            if (row.State != "base")
                for (; t != null && t.State != "base"; t = Parent(t))
                    own.AddRange(t.Procs);
            int baseGaps = 0;
            for (; t != null; t = Parent(t))
                baseGaps += t.Procs.Count(p => !p.Ported);
            report = new PortReport(path, row.State, LevelOf(row.State, own), own, baseGaps);
        }
        _reports[path] = report;
        return report;
    }

    private static PortLevel LevelOf(string state, List<PortProc> own)
    {
        if (state is "inert" or "none" || own.Any(p => !p.Ported && KeyProcs.Contains(p.Name)))
            return PortLevel.Red;
        return own.All(p => p.Ported) ? PortLevel.Green : PortLevel.Yellow;
    }

    private TypeRow Parent(TypeRow row) => _types.GetValueOrDefault(row.Parent);

    private sealed class TypeRow(string parent, string state)
    {
        public readonly string Parent = parent;
        public readonly string State = state;
        public readonly List<PortProc> Procs = new();
    }
}
