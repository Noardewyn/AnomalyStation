#if ANOMALY_OPENDREAM
using System.Diagnostics;
using System.Reflection;
using DMCompiler;
using DMCompiler.DM;
using DMCompiler.DM.Expressions;
using OdString = DMCompiler.DM.Expressions.String;
using OdList = DMCompiler.DM.Expressions.List;

namespace Anomaly.Tools.Types;

/// <summary>
/// OpenDream's DMCompiler front end as a library (D110; types-converter.md section 2): compiles tg's .dme in a scratch
/// folder and reads the object tree, the code tree's declaration order and every proc into a <see cref="TgModel"/>.
/// </summary>
internal static class OdFrontEnd
{
    public static TgModel Compile(string dme, IReadOnlyDictionary<string, string> defines, int dmVersion, int dmBuild, TextWriter log)
    {
        var sw = Stopwatch.StartNew();
        var compiler = new DMCompiler.DMCompiler();
        var settings = new DMCompilerSettings
        {
            Files = new List<string> { dme },
            SuppressUnimplementedWarnings = true,
            MacroDefines = new Dictionary<string, string>(defines),
            DMVersion = dmVersion,
            DMBuild = dmBuild,
            StoreMessages = true,
        };
        bool ok = compiler.Compile(settings);
        double seconds = sw.Elapsed.TotalSeconds;
        log.WriteLine($"OpenDream compile: ok={ok}, {seconds:F1} s, {compiler.DMObjectTree.AllObjects.Count} types, {compiler.DMObjectTree.AllProcs.Count} procs");
        if (!ok) throw new InvalidOperationException("OpenDream failed to compile tg (see its output above)");

        var model = new TgModel { CompileSeconds = seconds };
        foreach (string m in compiler.CompilerMessages) // the stored messages (StoreMessages): "Warning OD2800 at ...: ..."
        {
            string[] words = m.Split(' ', 3);
            string key = words.Length >= 2 ? words[0] + " " + words[1] : m;
            model.Diagnostics[key] = model.Diagnostics.GetValueOrDefault(key) + 1;
        }
        Read(compiler, model);
        return model;
    }

    private static void Read(DMCompiler.DMCompiler compiler, TgModel model)
    {
        DMObjectTree tree = compiler.DMObjectTree;

        // Declaration order (types-converter.md 2.3): the code tree's var nodes in parse order, per type.
        var order = new Dictionary<string, List<DMCodeTree.AnomalyVarNode>>(StringComparer.Ordinal);
        foreach (DMCodeTree.AnomalyVarNode node in compiler.DMCodeTree.AnomalyVarNodes())
        {
            if (node.Location.InDMStandard || node.IsStatic) continue;
            string owner = node.Owner.PathString;
            if (!order.TryGetValue(owner, out var list)) order[owner] = list = new();
            list.Add(node);
        }

        foreach (DMObject o in tree.AllObjects)
        {
            string path = o.Path.PathString;
            var t = new TypeInfo { Path = path, Parent = o.Parent?.Path.PathString, Id = o.Id };
            order.TryGetValue(path, out var nodes);
            nodes ??= new();

            // Vars BYOND declares here (DMStandard), and tg's own declarations in parse order.
            var declaredHere = new HashSet<string>(StringComparer.Ordinal);
            foreach (DMCodeTree.AnomalyVarNode n in nodes)
                if (n.Kind == 'v') declaredHere.Add(n.Name);
            foreach ((string name, DMVariable v) in o.Variables)
                if (!declaredHere.Contains(name)) t.BuiltinVars.Add(name);
            t.BuiltinVars.Sort(StringComparer.Ordinal);

            var seenDecl = new HashSet<string>(StringComparer.Ordinal);
            foreach (DMCodeTree.AnomalyVarNode n in nodes)
            {
                if (n.Kind != 'v' || !seenDecl.Add(n.Name)) continue;
                if (!o.Variables.TryGetValue(n.Name, out DMVariable v)) continue; // a rejected redefinition (OpenDream warned)
                t.Vars.Add(new VarDecl
                {
                    Name = n.Name,
                    DeclType = v.Type?.PathString,
                    Value = Val(compiler, v.Value),
                    Tmp = v.IsTmp,
                    Const = v.IsConst,
                    Final = v.IsFinal,
                    File = Rel(n.Location),
                    Line = n.Location.Line ?? 0,
                });
            }

            // Overrides: one per var, at the place of its last override (the last value wins in DM).
            var lastOverride = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].Kind == 'o') lastOverride[nodes[i].Name] = i;
            foreach (var (name, index) in lastOverride.OrderBy(p => p.Value))
            {
                if (!o.VariableOverrides.TryGetValue(name, out DMVariable v)) continue; // parent_type, rejected overrides
                t.Overrides.Add(new VarSet { Name = name, Value = Val(compiler, v.Value), File = Rel(nodes[index].Location), Line = nodes[index].Location.Line ?? 0 });
            }

            // The init proc's assignments, in declaration order (the position of the var's node at this type).
            var position = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < nodes.Count; i++) position[nodes[i].Name] = i;
            var inits = new List<(int Pos, VarSet Set)>();
            foreach ((string name, Assignment assignment) in o.InitializationProcAssignments)
            {
                DMExpression rhs = Rhs(assignment);
                int pos = position.TryGetValue(name, out int p) ? p : int.MaxValue;
                inits.Add((pos, new VarSet { Name = name, Value = Val(compiler, rhs), File = Rel(rhs?.Location ?? assignment.Location), Line = (rhs?.Location ?? assignment.Location).Line ?? 0 }));
            }
            // A var assigned twice keeps its last assignment (OpenDream runs both; the last one wins).
            var lastInit = new Dictionary<string, (int Pos, VarSet Set)>(StringComparer.Ordinal);
            foreach (var x in inits) lastInit[x.Set.Name] = x;
            t.Inits.AddRange(lastInit.Values.OrderBy(x => x.Pos).ThenBy(x => x.Set.Line).Select(x => x.Set));

            // Procs and verbs defined here in tg.
            foreach ((string name, List<int> ids) in o.Procs)
            {
                foreach (int id in ids)
                {
                    DMProc proc = tree.AllProcs[id];
                    if (proc.Location.InDMStandard) continue;
                    t.Procs.Add(new ProcDef
                    {
                        Name = name,
                        Verb = proc.IsVerb,
                        Override = o.Parent != null && o.Parent.HasProc(name),
                        File = Rel(proc.Location),
                        Line = proc.Location.Line ?? 0,
                        Args = new List<string>(proc.Parameters),
                    });
                }
            }
            t.Procs.Sort((a, b) => CompareLocation(a.File, a.Line, b.File, b.Line));

            // The type's place: the first var node or proc in tg's code.
            var places = new List<(string File, int Line)>();
            if (nodes.Count > 0) places.Add((Rel(nodes[0].Location), nodes[0].Location.Line ?? 0));
            foreach (ProcDef p in t.Procs) places.Add((p.File, p.Line));
            t.Builtin = IsBuiltinType(o);
            if (places.Count > 0)
            {
                (string file, int line) = places.OrderBy(x => x.File == null ? 1 : 0).First();
                t.File = file;
                t.Line = line;
            }
            model.Types.Add(t);
        }

        foreach ((string name, int id) in tree.GlobalProcs)
        {
            DMProc proc = tree.AllProcs[id];
            if (proc.Location.InDMStandard) continue;
            model.GlobalProcs.Add(new ProcDef { Name = name, File = Rel(proc.Location), Line = proc.Location.Line ?? 0, Args = new List<string>(proc.Parameters) });
        }
        model.GlobalProcs.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
    }

    // Procs of one type come from several files; the generator only needs a stable order, so files compare by name
    // (the index lists file:line, not the include position).
    private static int CompareLocation(string fa, int la, string fb, int lb)
    {
        int c = string.CompareOrdinal(fa, fb);
        return c != 0 ? c : la.CompareTo(lb);
    }

    private static bool IsBuiltinType(DMObject o)
    {
        // A type BYOND declares: OpenDream creates it for DMStandard. tg's own types have a tg location somewhere.
        return o.Path.PathString is "/" or "/datum" or "/atom" or "/atom/movable" or "/obj" or "/mob" or "/turf" or "/area"
            or "/client" or "/world" or "/list" or "/alist" or "/savefile" or "/sound" or "/image" or "/mutable_appearance"
            or "/icon" or "/matrix" or "/regex" or "/database" or "/database/query" or "/exception" or "/generator"
            or "/particles" or "/callee" or "/vector" or "/dm_filter" or "/pixloc";
    }

    private static string Rel(Location location)
    {
        string f = location.SourceFile;
        if (string.IsNullOrEmpty(f) || f.StartsWith('<')) return null;
        return f.Replace('\\', '/');
    }

    private static readonly PropertyInfo RhsProperty = typeof(BinaryOp).GetProperty("RHS", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
    private static readonly FieldInfo RhsField = typeof(BinaryOp).GetField("<RHS>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);

    private static DMExpression Rhs(Assignment assignment)
        => (DMExpression)(RhsProperty?.GetValue(assignment) ?? RhsField?.GetValue(assignment));

    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private static object Captured(object o, string name)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType)
        {
            FieldInfo f = t.GetField("<" + name + ">P", Private) ?? t.GetField("_" + name, Private) ?? t.GetField(name, Private);
            if (f != null) return f.GetValue(o);
        }
        return null;
    }

    private static List<DmItem> Args(DMCompiler.DMCompiler c, ArgumentList arguments)
    {
        var items = new List<DmItem>();
        if (arguments == null) return items;
        foreach ((string name, DMExpression expr) in arguments.Expressions)
            items.Add(new DmItem { Name = name, Value = Val(c, expr) });
        return items;
    }

    /// <summary>Converts a folded value or an initializer expression (types-converter.md 2.2).</summary>
    internal static DmVal Val(DMCompiler.DMCompiler c, DMExpression e)
    {
        switch (e)
        {
            case null:
            case Null:
                return DmVal.Null();
            case Number n:
                if (float.IsPositiveInfinity(n.Value)) return new DmVal { K = "pinf" };
                if (float.IsNegativeInfinity(n.Value)) return new DmVal { K = "ninf" };
                return new DmVal { K = "num", N = n.Value };
            case OdString s:
                return new DmVal { K = "str", S = s.Value };
            case Resource r:
                r.TryAsJsonRepresentation(c, out object json);
                return new DmVal { K = "res", S = ((Dictionary<string, object>)json)["resourcePath"].ToString().Replace('\\', '/') };
            case ConstantTypeReference t:
                return new DmVal { K = "type", S = t.Value.Path.PathString };
            case ConstantProcReference p:
                return new DmVal { K = "proc", S = p.Path?.PathString ?? p.Value.Name };
            case ConstantProcStub stub:
                stub.TryAsJsonRepresentation(c, out object stubJson);
                return new DmVal { K = "str", S = (string)stubJson };
            case OdList list:
            {
                var values = ((DMExpression Key, DMExpression Value)[])Captured(list, "values");
                return new DmVal { K = "list", L = values.Select(v => new DmItem { Key = v.Key == null ? null : Val(c, v.Key), Value = Val(c, v.Value) }).ToList() };
            }
            case AList alist:
            {
                var values = ((DMExpression Key, DMExpression Value)[])Captured(alist, "values");
                return new DmVal { K = "alist", L = values.Select(v => new DmItem { Key = Val(c, v.Key), Value = Val(c, v.Value) }).ToList() };
            }
            case NewPath np:
                return new DmVal { K = "new", S = np.Path?.PathString, L = Args(c, (ArgumentList)Captured(np, "arguments")) };
            case DimensionalList dim:
            {
                var sizes = (DMExpression[])Captured(dim, "sizes");
                return new DmVal { K = "dimlist", L = sizes.Select(x => new DmItem { Value = Val(c, x) }).ToList() };
            }
            case ProcCall call:
                return new DmVal { K = "call", S = call.ToString(), L = Args(c, (ArgumentList)Captured(call, "arguments")) };
        }
        if (e.TryAsConstant(c, out Constant constant) && constant is not null && !ReferenceEquals(constant, e))
            return Val(c, constant);
        return new DmVal { K = "code", S = e.GetType().Name };
    }
}
#endif
