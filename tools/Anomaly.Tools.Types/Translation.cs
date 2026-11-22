using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Anomaly.Core.DmShim;

namespace Anomaly.Tools.Types;

/// <summary>
/// What the Tg assembly translates (types-converter.md section 5), by reflection: every <c>[DmPath]</c> class, the
/// methods it declares under their DM names (rules 2.2-2.5: <c>@</c> stripped, <c>__proc</c> and <c>__d&lt;n&gt;</c>
/// mapped back), and for each method whether its body is translated, partly translated, a whole-body
/// <c>Dm.NotTranslated</c> stub or a whole-body <c>Dm.Deferred</c> leaf, read from its IL (an async method's from its
/// state machine). Methods the generator itself emitted (<c>[DmGeneratedStub]</c>) do not count.
/// </summary>
internal sealed class Translation
{
    public enum State { No, Stub, Deferred, Partial, Yes }

    /// <summary>The index's <c>body</c> column (types-converter.md 6): what a declared method's IL does.</summary>
    public static string Body(State s) => s switch
    {
        State.Yes => "full",
        State.Partial => "partial",
        State.Deferred => "deferred",
        State.Stub => "skeleton",
        _ => "-",
    };

    /// <summary>Declared, for the inert rule (types-converter.md 4.1): the class declares the method, whatever its body.</summary>
    public static bool Counts(State s) => s != State.No;

    public readonly Dictionary<string, Type> ClassByPath = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, Dictionary<string, State>> _methods = new();
    private readonly Dictionary<string, State> _globalProcs = new(StringComparer.Ordinal);
    private readonly MethodInfo _notTranslated;
    private readonly MethodInfo _deferred;

    public Assembly TgAssembly { get; }

    public Translation()
    {
        TgAssembly = typeof(Anomaly.Core.Tg.datum).Assembly;
        _notTranslated = typeof(Dm).GetMethod(nameof(Dm.NotTranslated), BindingFlags.Public | BindingFlags.Static);
        _deferred = typeof(Dm).GetMethod(nameof(Dm.Deferred), BindingFlags.Public | BindingFlags.Static);
        foreach (Assembly a in new[] { typeof(Dm).Assembly, TgAssembly })
        {
            foreach (Type t in a.GetTypes())
            {
                var p = t.GetCustomAttribute<DmPathAttribute>(false);
                if (p != null && !ClassByPath.ContainsKey(p.Path)) ClassByPath[p.Path] = t;
            }
        }
        Type globals = TgAssembly.GetType("Anomaly.Core.Tg.GlobalProcs");
        if (globals != null)
        {
            foreach (MethodInfo m in globals.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (m.IsSpecialName || m.IsDefined(typeof(CompilerGeneratedAttribute))) continue;
                string dm = DmName(m.Name);
                State s = Classify(m);
                if (!_globalProcs.TryGetValue(dm, out State old) || s > old) _globalProcs[dm] = s;
            }
        }
    }

    /// <summary>The DM name of a C# method name (rules 2.2, 2.4, 2.5).</summary>
    public static string DmName(string name)
    {
        if (name.StartsWith('@')) name = name[1..];
        if (name.EndsWith("__proc", StringComparison.Ordinal)) name = name[..^"__proc".Length];
        return name;
    }

    /// <summary>
    /// The state of the proc <paramref name="dmName"/> defined at <paramref name="path"/>: the method of that DM name the
    /// class declares (types-converter.md 4.1; an earlier duplicate, <c>__d0</c>, does not count, and every definition of
    /// the name shares the plain name's state).
    /// </summary>
    public State ProcState(string path, string dmName)
    {
        if (path == "/") return _globalProcs.GetValueOrDefault(dmName);
        if (!ClassByPath.TryGetValue(path, out Type cls)) return State.No;
        return Methods(cls).GetValueOrDefault(dmName);
    }

    /// <summary>The C# type of a field or property that <paramref name="cls"/> itself declares, or null.</summary>
    public static Type DeclaredMemberType(Type cls, string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        FieldInfo f = cls.GetField(name, flags);
        if (f != null) return f.FieldType;
        PropertyInfo p = cls.GetProperty(name, flags);
        return p?.PropertyType;
    }

    private Dictionary<string, State> Methods(Type cls)
    {
        if (_methods.TryGetValue(cls, out var found)) return found;
        var map = new Dictionary<string, State>(StringComparer.Ordinal);
        foreach (MethodInfo m in cls.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (m.IsSpecialName || m.IsDefined(typeof(CompilerGeneratedAttribute))) continue;
            if (m.GetCustomAttributes(false).Any(a => a.GetType().Name == "DmGeneratedStubAttribute")) continue;
            string dm = DmName(m.Name);
            State s = Classify(m);
            if (!map.TryGetValue(dm, out State old) || s > old) map[dm] = s;
        }
        _methods[cls] = map;
        return map;
    }

    /// <summary>Reads the calls a method makes (its state machine's for an async method).</summary>
    private State Classify(MethodInfo m)
    {
        MethodBase body = m;
        var sm = m.GetCustomAttribute<StateMachineAttribute>();
        if (sm != null)
        {
            MethodInfo moveNext = sm.StateMachineType.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (moveNext != null) body = moveNext;
        }
        bool notTranslated = false, deferred = false, other = false;
        foreach (MethodBase callee in Calls(body))
        {
            if (callee == null) { other = true; continue; }
            if (callee == _notTranslated) notTranslated = true;
            else if (callee == _deferred) deferred = true;
            else if (!IsPlumbing(callee)) other = true;
        }
        if (notTranslated) return other ? State.Partial : State.Stub;
        if (deferred && !other) return State.Deferred;
        return State.Yes;
    }

    // Calls that any body makes, translated or not: the async machinery, DmTask's completed tasks, System.
    private static bool IsPlumbing(MethodBase callee)
    {
        Type t = callee.DeclaringType;
        if (t == null) return false;
        string ns = t.Namespace ?? "";
        if (ns.StartsWith("System", StringComparison.Ordinal)) return true;
        string n = t.Name;
        return n.StartsWith("DmTask", StringComparison.Ordinal) || n.Contains("MethodBuilder", StringComparison.Ordinal)
            || n.Contains("Awaiter", StringComparison.Ordinal);
    }

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null))
        .ToDictionary(o => o.Value);

    private static IEnumerable<MethodBase> Calls(MethodBase body)
    {
        byte[] il = body.GetMethodBody()?.GetILAsByteArray();
        if (il == null) yield break;
        Module module = body.Module;
        int i = 0;
        while (i < il.Length)
        {
            short value = il[i] == 0xFE && i + 1 < il.Length ? (short)(0xFE00 | il[i + 1]) : il[i];
            i += value > 0xFF || value < 0 ? 2 : 1;
            if (!OpCodesByValue.TryGetValue(value, out OpCode op)) yield break;
            int size = op.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i),
                _ => 4,
            };
            if (op.OperandType == OperandType.InlineMethod && (op == OpCodes.Call || op == OpCodes.Callvirt || op == OpCodes.Newobj))
            {
                int token = BitConverter.ToInt32(il, i);
                MethodBase callee;
                try
                {
                    callee = module.ResolveMethod(token, body.DeclaringType?.GetGenericArguments(), (body as MethodInfo)?.GetGenericArguments());
                }
                catch (Exception)
                {
                    callee = null;
                }
                yield return callee;
            }
            i += size;
        }
    }
}
