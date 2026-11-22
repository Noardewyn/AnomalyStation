using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;

namespace Anomaly.Core.DmShim;

// Owner: dynamic; written by M3 P1 (lead-authorised, M3 P1 perf; docs/ai/specs/types-converter.md 9).

/// <summary>
/// BYOND's <c>T::var</c> is a compile-time read: for a <c>Type</c>, <see cref="Dm.initial{T, TResult}"/> asks the generated
/// table of the Tg assembly's <c>TypeOrder</c> class (its internal static <c>Constant(path, var, out value)</c>, found
/// through <see cref="DmTypeOrderAttribute"/>) before it builds a prototype. The var is the field or property the read
/// lambda loads (<c>(C p) =&gt; p.var</c>, read from its IL once per lambda). Built once per process; its caches only grow.
/// </summary>
internal sealed class DmTypeConstants
{
    private delegate int ConstantReader(string path, string var, out object value);

    private static readonly Lazy<DmTypeConstants> s_instance = new(() => new DmTypeConstants());

    internal static DmTypeConstants Instance => s_instance.Value;

    private readonly ConstantReader _reader;
    private readonly ConcurrentDictionary<MethodInfo, string> _varOfLambda = new();

    private DmTypeConstants()
    {
        foreach (Assembly assembly in DmAssemblyScan.Assemblies)
        {
            Type order = assembly.GetCustomAttribute<DmTypeOrderAttribute>()?.TypeOrder;
            MethodInfo method = order?.GetMethod("Constant", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (method != null) _reader = (ConstantReader)Delegate.CreateDelegate(typeof(ConstantReader), method, throwOnBindFailure: false);
        }
    }

    /// <summary>
    /// The compile-time value of the var <paramref name="read"/> loads, for <paramref name="type"/>; false when the table
    /// does not answer (then the caller reads a prototype). <paramref name="prototypeType"/> is the class whose prototype
    /// answers instead when the table stops at a hand-written var block (the same value, without building
    /// <paramref name="type"/>'s own prototype); null to use <paramref name="type"/>'s.
    /// </summary>
    internal bool TryRead<T, TResult>(Type type, Func<T, TResult> read, out TResult value, out Type prototypeType)
    {
        value = default;
        prototypeType = null;
        if (_reader == null) return false;
        string var = _varOfLambda.GetOrAdd(read.Method, VarOf);
        if (var.Length == 0) return false;
        string path = DmTypes.PathOf(type);
        if (path == null) return false;
        int found = _reader(path, var, out object constant);
        if (found == 0 && constant is string stop && DmTypes.FromPath(stop) is Type stopType && typeof(T).IsAssignableFrom(stopType))
        {
            prototypeType = stopType;
            return false;
        }
        if (found != 1) return false;
        switch (constant)
        {
            case null when default(TResult) == null:
                return true;
            case TResult typed:
                value = typed;
                return true;
            case int i when typeof(TResult) == typeof(float) || typeof(TResult) == typeof(float?):
                value = (TResult)(object)(float)i;
                return true;
            default:
                return false; // a value of another C# type than the member: the prototype decides
        }
    }

    // The member a read lambda loads: "ldarg.0|1; ldfld F | call(virt) get_P; ret", else "" (not a plain member read).
    private static string VarOf(MethodInfo method)
    {
        byte[] il;
        try { il = method.GetMethodBody()?.GetILAsByteArray(); }
        catch (Exception) { return ""; }
        if (il == null || il.Length < 7) return "";
        int i = 0;
        byte arg = il[i++];
        if (arg != OpCodes.Ldarg_0.Value && arg != OpCodes.Ldarg_1.Value) return "";
        if (method.IsStatic != (arg == OpCodes.Ldarg_0.Value)) return "";
        byte op = il[i++];
        int token = BitConverter.ToInt32(il, i);
        i += 4;
        if (i >= il.Length || il[i] != OpCodes.Ret.Value || i != il.Length - 1) return "";
        try
        {
            Module module = method.Module;
            Type[] typeArgs = method.DeclaringType?.IsGenericType == true ? method.DeclaringType.GetGenericArguments() : null;
            if (op == OpCodes.Ldfld.Value)
                return module.ResolveField(token, typeArgs, null)?.Name ?? "";
            if (op == OpCodes.Callvirt.Value || op == OpCodes.Call.Value)
            {
                MethodBase getter = module.ResolveMethod(token, typeArgs, null);
                if (getter != null && getter.IsSpecialName && getter.Name.StartsWith("get_", StringComparison.Ordinal) && getter.GetParameters().Length == 0)
                    return getter.Name.Substring(4);
            }
        }
        catch (Exception)
        {
            // an unresolvable token: not a plain member read
        }
        return "";
    }
}
