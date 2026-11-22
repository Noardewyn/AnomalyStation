namespace Anomaly.Tools.Types;

/// <summary>How the generator treats a type (types-converter.md 3.2).</summary>
internal enum Mode
{
    /// <summary>No class: the root, OpenDream's <c>/x/var</c> artefacts, a type the shim implements, a builtin without an engine part, or a subtype of one.</summary>
    Skip,
    /// <summary>A hand-written class with a hand-written var block: the generator adds only the vars it does not declare (the complement) and checks the rest (the conformance test).</summary>
    Hand,
    /// <summary>A hand-written class without a var block (procs only): the generated part is its var block.</summary>
    HandProcs,
    /// <summary>A generated class.</summary>
    Generated,
}

/// <summary>One type of the tree with everything the generator decides about it.</summary>
internal sealed class Node
{
    public TypeInfo Info;
    public Node Parent;
    public readonly List<Node> Children = new();
    public string Path => Info.Path;
    public string ClassName;
    public Mode Mode;
    public string SkipReason;
    public HandWritten.ClassParts Hand;
    /// <summary>The C# class (from the Tg or shim assembly) when one exists already.</summary>
    public Type Existing;
    public bool Inert;
    public bool InMeta;
    /// <summary>Procs at this type that live-partial.txt stubs (types-converter.md 5.3).</summary>
    public readonly List<(ProcDef Proc, string Milestone, string Owner)> Stubs = new();
    /// <summary>The C# type of each var this type declares (generated or hand-written), by DM name.</summary>
    public readonly Dictionary<string, CsType> Members = new(StringComparer.Ordinal);
    /// <summary>The vars this type declares that the generator emits as fields (Hand: the complement).</summary>
    public readonly List<VarDecl> EmittedVars = new();
    public int Depth;

    public bool HasClass => Mode != Mode.Skip;
    public bool IsHandWritten => Mode is Mode.Hand or Mode.HandProcs;

    public IEnumerable<Node> Ancestors()
    {
        for (Node n = Parent; n != null; n = n.Parent) yield return n;
    }

    public IEnumerable<Node> SelfAndDescendants()
    {
        yield return this;
        foreach (Node c in Children)
            foreach (Node d in c.SelfAndDescendants()) yield return d;
    }

    public override string ToString() => Path;
}

/// <summary>A C# member type as the generator sees it (types-converter.md 4.1).</summary>
internal sealed class CsType
{
    public enum K { Float, NullableFloat, String, Type, List, Object, Class, Other }

    public K Kind;
    /// <summary>The class name for <see cref="K.Class"/>; the C# spelling for <see cref="K.Other"/>.</summary>
    public string Name;
    /// <summary>The DM node of a <see cref="K.Class"/> type when it is a DM class (its descendants are assignable).</summary>
    public Node ClassNode;

    public static readonly CsType Float = new() { Kind = K.Float };
    public static readonly CsType NullableFloat = new() { Kind = K.NullableFloat };
    public static readonly CsType String = new() { Kind = K.String };
    public static readonly CsType TypeT = new() { Kind = K.Type };
    public static readonly CsType List = new() { Kind = K.List };
    public static readonly CsType Object = new() { Kind = K.Object };

    public string CSharp => Kind switch
    {
        K.Float => "float",
        K.NullableFloat => "float?",
        K.String => "string",
        K.Type => "Type",
        K.List => "DmList",
        K.Object => "object",
        _ => Name,
    };

    public static CsType FromClr(Type t, Func<Type, Node> nodeOf)
    {
        if (t == typeof(float)) return Float;
        if (t == typeof(float?)) return NullableFloat;
        if (t == typeof(string)) return String;
        if (t == typeof(Type)) return TypeT;
        if (t == typeof(object)) return Object;
        if (t.Name == "DmList") return List;
        Node n = nodeOf(t);
        if (n != null) return new CsType { Kind = K.Class, Name = t.Name, ClassNode = n };
        return new CsType { Kind = K.Other, Name = t.Name };
    }
}
