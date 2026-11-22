using System.Reflection;
using System.Text;

namespace Anomaly.Tools.Types;

internal sealed partial class Generator
{
    public const string TypesDir = "src/Core/Tg/Generated/Types";

    // File split (types-converter.md 3.3): a subtree of at most Cap types is one file; a bigger one keeps its root and
    // its small child subtrees (at most Small types each) in its own file and splits its big children the same way.
    // A new type changes only the file of its subtree.
    private const int Cap = 600;
    private const int Small = 60;

    private readonly Dictionary<Node, int> _size = new();

    private int Size(Node n)
    {
        if (_size.TryGetValue(n, out int s)) return s;
        s = 1 + n.Children.Sum(Size);
        _size[n] = s;
        return s;
    }

    private void Assign(Node n, string file, Dictionary<string, List<Node>> files)
    {
        if (!files.TryGetValue(file, out var list)) files[file] = list = new();
        if (Size(n) <= Cap)
        {
            list.AddRange(n.SelfAndDescendants());
            return;
        }
        list.Add(n);
        foreach (Node c in n.Children)
        {
            if (Size(c) <= Small) list.AddRange(c.SelfAndDescendants());
            else Assign(c, FileOf(c), files);
        }
    }

    private static string FileOf(Node n)
    {
        // One flat folder: a folder named after the top segment would be "obj/", which .gitignore ignores.
        string[] segs = n.Path.TrimStart('/').Split('/');
        return TypesDir + "/" + string.Join(".", segs) + ".g.cs";
    }

    private void EmitClasses(Outputs outputs)
    {
        var files = new Dictionary<string, List<Node>>(StringComparer.Ordinal);
        foreach (Node root in _preorder.Where(n => n.Parent == null)) Assign(root, FileOf(root), files);
        foreach (var (file, nodes) in files.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            var sb = new StringBuilder();
            int classes = 0;
            foreach (Node n in nodes)
            {
                string text = ClassText(n);
                if (text == null) continue;
                sb.Append('\n').Append(text);
                classes++;
            }
            if (classes == 0) continue;
            var head = new StringBuilder();
            head.Append(Header("the var blocks of the DM types in this file (D111)"));
            head.Append("using Anomaly.Core.DmShim;\nusing Anomaly.Core.Kernel;\n\nnamespace Anomaly.Core.Tg;\n");
            outputs.Files[file] = head.Append(sb).ToString();
        }
    }

    private static string Loc(string file, int line) => file == null ? "(implied by a subtype's path)" : file + ":" + line;

    /// <summary>The generated part of one class, or null when it has none.</summary>
    private string ClassText(Node n)
    {
        if (n.Mode == Mode.Skip) return null;
        var fields = new List<string>();
        var ctor = new List<string>();
        var init = new List<string>();
        var stubs = new List<string>();

        foreach (VarDecl v in n.EmittedVars)
        {
            CsType t = n.Members[v.Name];
            bool runtime = n.Info.Inits.Any(i => i.Name == v.Name);
            string initializer = "";
            if (!runtime && v.Value != null && v.Value.K != "null")
            {
                string e = _values.Emit(v.Value, t, out string why);
                if (e != null) initializer = " = " + e;
                else Gap("declaration", $"{n.Path}.{v.Name} = {v.Value} ({v.File}:{v.Line}): {why}");
            }
            else if (!runtime && t.Kind == CsType.K.Float && v.Value?.K == "null")
            {
                Gap("declaration", $"{n.Path}.{v.Name}: null default typed float");
            }
            fields.Add($"    public {t.CSharp} {Ident(v.Name)}{initializer}; // tg: {Loc(v.File, v.Line)}");
            Record(n, v.Name, initializer.Length > 0 ? v.Value : null, t);
        }

        var initNames = new HashSet<string>(n.Info.Inits.Select(i => i.Name), StringComparer.Ordinal);
        string ctorLoc = null;
        if (n.Mode is Mode.Generated or Mode.HandProcs)
        {
            foreach (VarSet o in n.Info.Overrides)
            {
                if (initNames.Contains(o.Name)) continue; // OpenDream moved it to the init proc (rules 10.2)
                (CsType t, Node owner) = MemberOf(n, o.Name);
                ctorLoc ??= Loc(o.File, o.Line);
                if (t == null)
                {
                    ctor.Add($"        // GAP(types): {o.Name} = {Short(o.Value)} ({Loc(o.File, o.Line)}): no C# member declares {o.Name}");
                    Gap("override without member", $"{n.Path}.{o.Name}");
                    continue;
                }
                string e = _values.Emit(o.Value ?? DmVal.Null(), t, out string why);
                if (e == null && o.Value?.K == "num" && o.Value.N == 0 && owner.IsHandWritten
                    && t.Kind is CsType.K.Type or CsType.K.Class or CsType.K.List or CsType.K.String)
                {
                    // Types-converter.md 3.5: tg's NONE/FALSE (0) into a hand-written reference-typed var, which the generator
                    // cannot widen, is stored as null: both are falsy and neither is the default.
                    ctor.Add($"        {Ident(o.Name)} = null; // GAP(types): tg sets 0 ({Loc(o.File, o.Line)}); the hand-written {owner.ClassName}.{o.Name} is {t.CSharp}");
                    Record(n, o.Name, DmVal.Null(), t);
                    Gap("override 0 as null", $"{n.Path}.{o.Name} = 0 ({Loc(o.File, o.Line)}): the hand-written {owner.ClassName}.{o.Name} is {t.CSharp}");
                    continue;
                }
                if (e == null)
                {
                    ctor.Add($"        // GAP(types): {o.Name} = {Short(o.Value)} ({Loc(o.File, o.Line)}): {why}");
                    Gap("override", $"{n.Path}.{o.Name} = {Short(o.Value)} ({Loc(o.File, o.Line)}): {why}");
                    continue;
                }
                ctor.Add($"        {Ident(o.Name)} = {e};");
                Record(n, o.Name, o.Value ?? DmVal.Null(), t);
            }
        }

        string initLoc = null;
        bool canInit = n.Mode is Mode.Generated or Mode.HandProcs || (n.Mode == Mode.Hand && !n.Hand.HasInitVars);
        foreach (VarSet i in n.Info.Inits)
        {
            bool emitted = n.EmittedVars.Any(v => v.Name == i.Name);
            if (n.Mode == Mode.Hand && !emitted) continue; // the hand-written block owns it (the conformance test checks it)
            if (!canInit)
            {
                fields.Add($"    // GAP(types): {i.Name} = {Short(i.Value)} ({Loc(i.File, i.Line)}): to be initialized in the hand-written dm_init_vars of {n.ClassName}");
                Gap("init in hand-written class", $"{n.Path}.{i.Name} = {Short(i.Value)} ({Loc(i.File, i.Line)}): the hand-written {n.ClassName} has its own dm_init_vars");
                continue;
            }
            (CsType t, Node owner) = MemberOf(n, i.Name);
            initLoc ??= Loc(i.File, i.Line);
            if (t == null)
            {
                init.Add($"        // GAP(types): {i.Name} = {Short(i.Value)} ({Loc(i.File, i.Line)}): no C# member declares {i.Name}");
                Gap("init without member", $"{n.Path}.{i.Name}");
                continue;
            }
            string e = _values.Emit(i.Value, t, out string why);
            if (e == null)
            {
                // Types-converter.md 4.4: a non-constant initializer that the generator does not translate is deferred.
                init.Add($"        Dm.Deferred({CSharpValues.Literal(Loc(i.File, i.Line))}, {CSharpValues.Literal($"{n.Path}.{i.Name} = {Short(i.Value)}: initializer not generated ({why})")});");
                Gap("deferred init", $"{n.Path}.{i.Name} = {Short(i.Value)} ({Loc(i.File, i.Line)}): {why}");
                continue;
            }
            if (!i.Value.IsConstant) Report.Add($"translated init: {n.Path}.{i.Name} = {Short(i.Value)} ({Loc(i.File, i.Line)}) -> {(e.Length > 90 ? e.Substring(0, 87) + "..." : e)}");
            init.Add($"        {Ident(i.Name)} = {e}; // tg: {Loc(i.File, i.Line)}");
        }

        bool dmPath = n.Mode == Mode.Generated || n.Hand?.DmPath == null;
        if (n.Mode == Mode.Hand && fields.Count == 0 && init.Count == 0 && !n.Inert && !dmPath) return null;
        if (n.Mode == Mode.HandProcs && fields.Count == 0 && ctor.Count == 0 && init.Count == 0 && !n.Inert && !dmPath) return null;

        var sb = new StringBuilder();
        string what = n.Mode switch
        {
            Mode.Hand => " (the vars the hand-written class does not declare)",
            Mode.HandProcs => " (the var block of a hand-written class)",
            _ => "",
        };
        sb.Append($"// tg: {Loc(n.Info.File, n.Info.Line)}{what}\n");
        var attrs = new List<string>();
        if (dmPath) attrs.Add($"DmPath({CSharpValues.Literal(n.Path)})");
        if (n.Mode == Mode.Generated) attrs.Add("DmGeneratedOnly");
        if (n.Inert) attrs.Add("DmInert");
        if (attrs.Count > 0) sb.Append('[').Append(string.Join(", ", attrs)).Append("]\n");
        string baseName = n.Parent?.ClassName;
        string decl = $"public partial class {n.ClassName}" + (baseName != null && n.Mode == Mode.Generated ? " : " + baseName : "");
        if (fields.Count == 0 && ctor.Count == 0 && init.Count == 0 && stubs.Count == 0)
            return sb.Append(decl).Append(" { }\n").ToString();
        sb.Append(decl).Append("\n{\n");
        foreach (string f in fields) sb.Append(f).Append('\n');
        if (ctor.Count > 0)
        {
            if (fields.Count > 0) sb.Append('\n');
            sb.Append($"    // tg: {ctorLoc}\n");
            sb.Append($"    public {n.ClassName}()\n    {{\n");
            foreach (string c in ctor) sb.Append(c).Append('\n');
            sb.Append("    }\n");
        }
        if (init.Count > 0)
        {
            if (fields.Count > 0 || ctor.Count > 0) sb.Append('\n');
            sb.Append($"    // tg: {initLoc}\n");
            sb.Append("    protected internal override void dm_init_vars()\n    {\n        base.dm_init_vars();\n");
            foreach (string c in init) sb.Append(c).Append('\n');
            sb.Append("    }\n");
        }
        foreach (string s in stubs)
        {
            sb.Append('\n').Append(s);
        }
        sb.Append("}\n");
        return sb.ToString();
    }

    private static string Short(DmVal v)
    {
        string s = v?.ToString() ?? "null";
        s = s.Replace('\n', ' ').Replace('\r', ' ');
        return s.Length > 80 ? s.Substring(0, 77) + "..." : s;
    }

    // ---- live-partial stubs (types-converter.md 5) ------------------------------------------------------------------

    public const string LivePartialDir = "src/Core/Tg/Generated/LivePartial";

    private Dictionary<(string Root, string Name), string> _slots;

    /// <summary>The slot signatures of docs/ai/specs/generated/slots.tsv, by root type and DM name.</summary>
    private Dictionary<(string Root, string Name), string> Slots()
    {
        if (_slots != null) return _slots;
        _slots = new();
        string file = Path.Combine(_repo, "docs", "ai", "specs", "generated", "slots.tsv");
        if (!File.Exists(file)) return _slots;
        string[] header = null;
        foreach (string line in File.ReadLines(file))
        {
            string[] f = line.Split('\t');
            if (header == null) { header = f; continue; }
            int root = Array.IndexOf(header, "root_path"), name = Array.IndexOf(header, "name"), sig = Array.IndexOf(header, "signature");
            if (f.Length > sig) _slots[(f[root], f[name])] = f[sig];
        }
        return _slots;
    }

    /// <summary>
    /// The signature line of a stub (types-converter.md 5): an override of the method a C# ancestor declares (by
    /// reflection, the converter's own stubs left out) or of an ancestor's stub of this run; otherwise slots.tsv's root
    /// declaration (<c>public virtual</c>), even where tg's root is higher up and still untranslated.
    /// </summary>
    private string StubSignature(Node n, ProcDef p, out string returns)
    {
        returns = null;
        Node existing = n.Ancestors().FirstOrDefault(a => a.Existing != null);
        MethodInfo slot = existing?.Existing.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(m => Translation.DmName(m.Name) == p.Name && m.IsVirtual && !m.IsFinal && !m.IsPrivate
                && !m.GetCustomAttributes(false).Any(a => a.GetType().Name == "DmGeneratedStubAttribute"))
            .OrderBy(m => m.Name.EndsWith("__proc", StringComparison.Ordinal) ? 1 : 0)
            .FirstOrDefault();
        if (slot != null)
        {
            string access = slot.IsPublic ? "public" : slot.IsFamilyOrAssembly ? "protected internal" : slot.IsFamily ? "protected" : "internal";
            returns = CSharpTypeName(slot.ReturnType);
            var pars = slot.GetParameters().Select(pi =>
            {
                bool isParams = pi.IsDefined(typeof(ParamArrayAttribute));
                return (isParams ? "params " : "") + CSharpTypeName(pi.ParameterType) + " " + Ident(pi.Name) + (isParams ? "" : " = null");
            });
            return $"{access} override {returns} {slot.Name}({string.Join(", ", pars)})";
        }
        bool ancestorStub = n.Ancestors().Any(a => a.Stubs.Any(st => st.Proc.Name == p.Name));
        for (Node a = n; a != null; a = a.Parent)
        {
            if (!Slots().TryGetValue((a.Path, p.Name), out string sig)) continue;
            if (ancestorStub) sig = sig.Replace("public virtual ", "public override ");
            string[] words = sig.Split(' ');
            returns = words.Length > 2 ? words[2] : "void";
            return sig;
        }
        return null;
    }

    private void EmitLivePartial(Outputs outputs)
    {
        var files = new SortedDictionary<string, StringBuilder>(StringComparer.Ordinal);
        foreach (Node n in _preorder.Where(n => n.Stubs.Count > 0))
        {
            string top = n.Path.TrimStart('/').Split('/')[0];
            string file = LivePartialDir + "/" + top + ".g.cs";
            if (!files.TryGetValue(file, out StringBuilder sb)) files[file] = sb = new StringBuilder();
            sb.Append($"\n// tg: {Loc(n.Info.File, n.Info.Line)}\npublic partial class {n.ClassName}\n{{\n");
            bool first = true;
            foreach ((ProcDef p, string milestone, string packed) in n.Stubs)
            {
                string[] kw = packed.Split('\t');
                string kind = kw[0], why = kw[1], section = kw[2].Split(' ')[0];
                string where = Loc(p.File, p.Line);
                string sig = StubSignature(n, p, out string ret);
                if (sig == null) throw new ToolException($"{LivePartialPath}: {n.Path}/{p.Name}: no slot in slots.tsv and no declaring ancestor");
                if (!first) sb.Append('\n');
                first = false;
                sb.Append($"    // tg: {where}\n");
                sb.Append($"    // STUB({milestone}): {LivePartialPath}, section {section}\n");
                sb.Append("    [DmGeneratedStub]\n");
                sb.Append("    ").Append(sig).Append("\n    {\n");
                string call = kind == "deferred" ? "Dm.Deferred" : "Dm.NotTranslated";
                sb.Append($"        {call}({CSharpValues.Literal(where)}, {CSharpValues.Literal($"{p.Name}: {why} ({milestone})")});\n");
                // tg's examine is `. = ..()` plus lines of its own: a deferred one keeps the parent's (the name and description)
                if (kind == "deferred" && p.Name == "examine" && sig.Contains(" override "))
                    sb.Append("        return base.examine(").Append(StubArguments(sig)).Append(");\n");
                else if (ret == "DmTask") sb.Append("        return DmTask.CompletedTask;\n");
                else if (ret != null && ret.StartsWith("DmTask<", StringComparison.Ordinal)) sb.Append($"        return {ret}.FromResult(default);\n");
                else if (ret != "void") sb.Append("        return default;\n");
                sb.Append("    }\n");
            }
            sb.Append("}\n");
        }
        foreach (var (file, body) in files)
            outputs.Files[file] = Header("the live-partial stubs of docs/ai/specs/live-partial.txt (types-converter.md 5)")
                + "using Anomaly.Core.DmShim;\nusing Anomaly.Core.Kernel;\n\n"
                // M4a P1 (types-converter.md 5): a stub generated as the root of its slot (`public virtual`) hides the root a
                // translator declares later (CS0114) until the tool reruns and makes it an `override`; the warning would stop
                // the Tg build, and with it the tool, which references Tg. LivePartialStubTests fails on such a stale stub.
                + "#pragma warning disable CS0114 // a stale stub under a newly declared root compiles until the tool reruns\n\n"
                + "namespace Anomaly.Core.Tg;\n" + body;
    }

    /// <summary>A stub signature's parameters as call arguments: <c>(mob user = null, params object[] __extra)</c> gives <c>user, __extra</c>.</summary>
    private static string StubArguments(string sig)
    {
        string list = sig.Substring(sig.IndexOf('(') + 1, sig.LastIndexOf(')') - sig.IndexOf('(') - 1);
        return string.Join(", ", list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(param => param.Split('=')[0].Trim().Split(' ').Last()));
    }

    private static string CSharpTypeName(Type t)
    {
        if (t == typeof(void)) return "void";
        if (t == typeof(object)) return "object";
        if (t == typeof(float)) return "float";
        if (t == typeof(string)) return "string";
        if (t == typeof(bool)) return "bool";
        if (t == typeof(int)) return "int";
        if (t.IsArray) return CSharpTypeName(t.GetElementType()) + "[]";
        if (Nullable.GetUnderlyingType(t) is Type u) return CSharpTypeName(u) + "?";
        if (t.IsGenericType)
        {
            string name = t.Name.Substring(0, t.Name.IndexOf('`'));
            return name + "<" + string.Join(", ", t.GetGenericArguments().Select(CSharpTypeName)) + ">";
        }
        return t.Name;
    }
}
