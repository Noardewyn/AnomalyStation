using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.DmShim;

// Owner: dynamic. The binder behind Dm.Call, Dm.@new's New call, hascall and the builtin factories
// (docs/ai/specs/object-model.md section 12; docs/ai/translation-rules.md 7.2, 7.3, 7.7).

/// <summary>
/// Calls by name. <b>Resolve</b>: the proc is looked up on the receiver's runtime type, most derived declaration first
/// (an override, or a method that hides an inherited one with <c>new</c>, as <c>/client/New</c> does), under its C#
/// name or its DM name (<c>x</c> finds the renamed <c>x__proc</c>, rules 2.4; a proc path such as
/// <c>"/datum/x/proc/y"</c> means its last segment; reflection already reports <c>@event</c> as <c>event</c>); a null
/// or text receiver means a global proc (<c>GLOBAL_PROC</c>), looked up on the classes named by <c>[assembly:
/// DmGlobalProcs]</c>; on <c>world</c>, a proc of <c>/world</c> is one of those classes' extension methods of
/// <see cref="DmWorld"/>, called with <c>world</c> first (rules 1.5). <b>Bind</b> (OpenDream <c>DMProc.ToProcArguments</c>): each argument goes to the DM parameter at
/// its position in the call, a named one (<see cref="DmNamedArg"/>, or an <c>arglist</c> entry whose text item is a
/// key of the list, whatever its value) to the parameter of that name, a later one overwriting an earlier one; DM
/// parameters past the C# fixed ones live in the trailing <c>params object[] __extra</c>, and excess positional
/// arguments go there or are dropped when the method has none; missing ones are null (the callee's prologue applies DM
/// defaults; a shim builtin's C# default applies to an omitted or null argument). DM parameter names are the resolved
/// definition's <see cref="DmParamsAttribute"/>, else its C# parameter names (BYOND ref "named arguments": the names
/// of the overriding definition are the ones matched). An unknown name is a runtime error. The port's own methods
/// (<see cref="IsPortName"/>) are no procs. <b>Convert</b> by rules 3.5 and <b>invoke</b>
/// through a delegate compiled once per (runtime type, name), which returns the callee's result as a
/// <c>DmTask&lt;object&gt;</c>: a coloured callee's task through <see cref="IDmTask.AsObjectTask"/>, a synchronous
/// callee's value as a completed task (void gives null, a C# bool gives 1 or 0). Exceptions keep their type: the
/// compiled call does not wrap them.
/// </summary>
internal static class DmBinder
{
    /// <summary>A resolved callable and its binding metadata.</summary>
    internal sealed class Target
    {
        internal static readonly Target Missing = new();

        private Target()
        {
        }

        internal Target(MethodInfo method, Type receiverType, bool worldProc = false)
        {
            Method = method;
            IsWorldProc = worldProc;
            ParameterInfo[] all = method.GetParameters();
            // A /world proc is an extension method of DmWorld: its first C# parameter is the receiver, not a DM one.
            ParameterInfo[] parameters = worldProc ? all[1..] : all;
            HasExtra = parameters.Length > 0
                && parameters[^1].ParameterType == typeof(object[])
                && parameters[^1].IsDefined(typeof(ParamArrayAttribute), inherit: false);
            Fixed = HasExtra ? parameters.Length - 1 : parameters.Length;
            ParameterCount = parameters.Length;
            Defaults = new object[Fixed];
            for (int i = 0; i < Fixed; i++)
                Defaults[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
            DmParamsAttribute names = method.GetCustomAttribute<DmParamsAttribute>(inherit: false);
            DmNames = names != null
                ? names.Names.Select(ArgName).ToArray()
                : parameters.Take(Fixed).Select(p => ArgName(p.Name)).ToArray();
            string owner = worldProc ? "/world" : method.IsStatic ? null : DmTypes.PathOf(DmTypes.Registry.DmTypeOf(receiverType));
            Display = (owner ?? (method.IsStatic ? "" : "/" + receiverType.Name)) + "/proc/" + DmName(method.Name);
            Invoke = Compile(method, all, worldProc);
        }

        internal MethodInfo Method { get; }

        /// <summary>
        /// A proc of <c>/world</c>: a <c>GlobalProcs</c> extension method of <see cref="DmWorld"/> (rules 1.5;
        /// object-model.md section 9), called with the receiver as its first C# argument.
        /// </summary>
        internal bool IsWorldProc { get; }

        /// <summary>The C# parameters before the trailing params array.</summary>
        internal int Fixed { get; }

        internal int ParameterCount { get; }

        /// <summary>The last C# parameter is <c>params object[]</c> (<c>__extra</c>).</summary>
        internal bool HasExtra { get; }

        /// <summary>
        /// The boxed C# default of each fixed parameter (null when it has none, as for every parameter of translated
        /// code, rules 3.3). It replaces an omitted or null argument: in DM a default applies to an argument that is
        /// omitted or null (rules 7.2), which for a shim builtin with a C# default (<c>Cut(Start = 1, End = 0)</c>) is
        /// that default.
        /// </summary>
        internal object[] Defaults { get; }

        /// <summary>The DM parameter names in order; names past <see cref="Fixed"/> are positions in <c>__extra</c>.</summary>
        internal string[] DmNames { get; }

        /// <summary>"/type/proc/name", for runtime error messages.</summary>
        internal string Display { get; }

        /// <summary>Calls the method on a receiver (ignored when static) with the bound C# arguments.</summary>
        internal Func<object, object[], DmTask<object>> Invoke { get; }

        internal int IndexOfName(string name)
        {
            for (int i = 0; i < DmNames.Length; i++)
            {
                if (string.Equals(DmNames[i], name, StringComparison.Ordinal)) return i;
            }
            return -1;
        }
    }

    // Resolved targets per (runtime type, name); the key type for global procs is GlobalProcsKey. Missing entries
    // are cached too (hascall asks repeatedly). Append-only, immutable entries (object-model.md section 12).
    private static readonly ConcurrentDictionary<(Type Type, string Name), Target> s_targets = new();

    // Builtin factories per (type, positional argument count).
    private static readonly ConcurrentDictionary<(Type Type, int Arity), Target> s_factories = new();

    private sealed class GlobalProcsKey
    {
    }

    private static readonly MethodInfo FromResultMethod = typeof(DmTask<object>).GetMethod(nameof(DmTask<object>.FromResult));

    /// <summary>
    /// A DM name from a C# or call-site name: a leading <c>@</c> dropped (rules 2.2) and, for a proc path such as
    /// <c>"/datum/x/proc/y"</c> (what <c>typesof(/x/proc)</c> lists), its last segment.
    /// </summary>
    internal static string DmName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        int slash = name.LastIndexOf('/');
        if (slash >= 0) name = name.Substring(slash + 1);
        return name.Length > 0 && name[0] == '@' ? name.Substring(1) : name;
    }

    /// <summary>
    /// Whether <paramref name="name"/> is a global proc path, <c>"/proc/x"</c>: <c>call(obj, /proc/x)()</c> runs the
    /// global proc whatever the receiver (tg's <c>CALLBACK(src, GLOBAL_PROC_REF(x))</c> invokes
    /// <c>call(object, delegate)</c>, datums/callback.dm; OpenDream calls the proc value it is given).
    /// </summary>
    internal static bool IsGlobalProcPath(string name)
        => name.StartsWith("/proc/", StringComparison.Ordinal) && name.IndexOf('/', "/proc/".Length) < 0;

    /// <summary>
    /// The proc <paramref name="name"/> of <paramref name="receiver"/> (a global proc for a null or text receiver, or
    /// for a global proc path), or <see cref="Target.Missing"/>.
    /// </summary>
    internal static Target Resolve(object receiver, string name)
    {
        if (string.IsNullOrEmpty(name)) return Target.Missing;
        Type key = receiver == null || receiver is string || IsGlobalProcPath(name) ? typeof(GlobalProcsKey) : receiver.GetType();
        if (s_targets.TryGetValue((key, name), out Target cached)) return cached;
        return s_targets.GetOrAdd((key, name), static k => k.Type == typeof(GlobalProcsKey) ? BuildGlobal(k.Name) : BuildInstance(k.Type, k.Name));
    }

    /// <summary>
    /// Whether a call by <paramref name="name"/> on an object of <paramref name="type"/> resolves to a proc (the same
    /// resolution as <see cref="Resolve"/>, cached): what <c>typesof(/t/proc)</c> lists of the generated proc order
    /// (W3.0, S3).
    /// </summary>
    internal static bool ResolvesOn(Type type, string name)
    {
        if (type == null || string.IsNullOrEmpty(name)) return false;
        if (s_targets.TryGetValue((type, name), out Target cached)) return cached != Target.Missing;
        return s_targets.GetOrAdd((type, name), static k => BuildInstance(k.Type, k.Name)) != Target.Missing;
    }

    private static Target BuildInstance(Type type, string name)
    {
        if (!DmTypes.Registry.IsDmClass(type)) return Target.Missing; // a number, a Type, a host object: no DM procs
        string dmName = DmName(name);
        MethodInfo method = FindMethod(type, dmName, BindingFlags.Instance);
        if (method == null && !dmName.EndsWith("__proc", StringComparison.Ordinal)) method = FindMethod(type, dmName + "__proc", BindingFlags.Instance);
        if (method == null && typeof(DmWorld).IsAssignableFrom(type)) return BuildWorldProc(dmName);
        return method == null ? Target.Missing : new Target(method, type);
    }

    private static Target BuildGlobal(string name)
    {
        string dmName = DmName(name);
        foreach (Type holder in DmAssemblyScan.GlobalProcHolders)
        {
            MethodInfo method = FindMethod(holder, dmName, BindingFlags.Static);
            if (method != null) return new Target(method, holder);
        }
        return Target.Missing;
    }

    // A /world proc by name (rules 1.5; object-model.md section 9): tg's procs on /world are extension methods of
    // DmWorld in the classes named by [assembly: DmGlobalProcs], so that world.push_usr(...) reads as in DM; a call by
    // name on world (call(world, "x")(...), world.x(arglist(...))) finds them here and passes world first. As for an
    // instance proc, the DM name finds a proc renamed x__proc (rules 2.4).
    private static Target BuildWorldProc(string dmName)
    {
        foreach (Type holder in DmAssemblyScan.GlobalProcHolders)
        {
            MethodInfo method = FindWorldProc(holder, dmName);
            if (method == null && !dmName.EndsWith("__proc", StringComparison.Ordinal)) method = FindWorldProc(holder, dmName + "__proc");
            if (method != null) return new Target(method, typeof(DmWorld), worldProc: true);
        }
        return Target.Missing;
    }

    // The /world proc `name` of a holder: the public static extension method of DmWorld with the most parameters, then
    // the first declared.
    private static MethodInfo FindWorldProc(Type holder, string name)
    {
        MethodInfo best = null;
        int bestCount = -1;
        foreach (MethodInfo method in holder.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (method.Name != name || !IsWorldExtension(method) || !IsBindable(method)) continue;
            int count = method.GetParameters().Length;
            if (best == null || count > bestCount || (count == bestCount && method.MetadataToken < best.MetadataToken))
            {
                best = method;
                bestCount = count;
            }
        }
        return best;
    }

    /// <summary>Whether a static method is a /world proc: an extension method whose receiver is <see cref="DmWorld"/>.</summary>
    internal static bool IsWorldExtension(MethodInfo method)
    {
        if (!method.IsStatic || !method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), inherit: false)) return false;
        ParameterInfo[] parameters = method.GetParameters();
        return parameters.Length > 0 && parameters[0].ParameterType == typeof(DmWorld);
    }

    // The most derived public declaration named `name` that the binder can call. Among overloads declared at one
    // level (only shim builtins have them) the one with the most parameters wins, then the first declared.
    private static MethodInfo FindMethod(Type type, string name, BindingFlags kind)
    {
        BindingFlags flags = BindingFlags.Public | BindingFlags.DeclaredOnly | kind;
        for (Type level = type; level != null && level != typeof(object); level = level.BaseType)
        {
            MethodInfo best = null;
            int bestCount = -1;
            foreach (MethodInfo method in level.GetMethods(flags))
            {
                if (method.Name != name || !IsBindable(method)) continue;
                if (kind == BindingFlags.Static && IsWorldExtension(method)) continue; // a /world proc is no global proc
                int count = method.GetParameters().Length;
                if (best == null || count > bestCount || (count == bestCount && method.MetadataToken < best.MetadataToken))
                {
                    best = method;
                    bestCount = count;
                }
            }
            if (best != null) return best;
            if (kind == BindingFlags.Static) return null; // global procs are not inherited
        }
        return null;
    }

    /// <summary>
    /// Whether a method is a DM proc the binder may call by name. Not: C# object members (ToString, Equals,
    /// GetHashCode and their overrides); the port's own members, which no DM name reaches (see
    /// <see cref="IsPortName"/>); members that a DM value cannot be passed to or returned from (by-reference, pointer,
    /// and byref-like types such as spans, which compiled invokers cannot even hold).
    /// </summary>
    internal static bool IsBindable(MethodInfo method)
        => !method.IsSpecialName
           && !IsPortName(method.Name)
           && method.GetBaseDefinition().DeclaringType != typeof(object)
           && !method.IsGenericMethodDefinition
           && !method.ReturnType.IsByRef
           && !method.ReturnType.IsByRefLike
           && method.GetParameters().All(p => !p.ParameterType.IsByRef && !p.ParameterType.IsByRefLike && !p.ParameterType.IsPointer && !p.IsOut);

    /// <summary>
    /// A C# name that is the port's, never a DM proc's: the <see cref="IDmObject"/> hooks <c>dm_*</c> (object-model.md
    /// 1.1; tg has no proc named so at the snapshot, and calling <c>dm_assign_id</c> or <c>dm_pre_new</c> again would
    /// re-identify or re-initialize the object) and an earlier duplicate definition <c>name__d0</c>, <c>name__d1</c>, ...
    /// (rules 2.5; tg defines <c>/world/_initialize_log_files</c> four times, _globalvars/logging.dm), which only the
    /// next definition's <c>..()</c> calls.
    /// </summary>
    internal static bool IsPortName(string name)
        => name.StartsWith("dm_", StringComparison.Ordinal) || IsEarlierDefinition(name);

    // name__d<digits>
    private static bool IsEarlierDefinition(string name)
    {
        int i = name.Length;
        while (i > 0 && char.IsAsciiDigit(name[i - 1])) i--;
        return i < name.Length && i >= 3 && string.CompareOrdinal(name, i - 3, "__d", 0, 3) == 0;
    }

    /// <summary>
    /// The factory of a builtin value type for <c>new T(args)</c> (object-model.md section 4, step 1): the
    /// <c>Dm</c> builtin proc named like the type's last path segment that returns the type (<c>/sound</c>:
    /// <c>sound(file, repeat, wait, channel, volume)</c>, the same parameters as BYOND's <c>/sound/New</c>), the
    /// overload with exactly <paramref name="arity"/> parameters when there is one, else the smallest that takes
    /// them all. <see cref="Target.Missing"/> when the shim has no such proc.
    /// </summary>
    internal static Target Factory(Type type, int arity)
        => s_factories.GetOrAdd((type, arity), static k => BuildFactory(k.Type, k.Arity));

    private static Target BuildFactory(Type type, int arity)
    {
        string path = DmTypes.PathOf(type);
        if (path == null) return Target.Missing;
        string name = path.Substring(path.LastIndexOf('/') + 1);
        MethodInfo[] candidates = typeof(Dm).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == name && m.ReturnType == type && IsBindable(m))
            .OrderBy(m => m.GetParameters().Length)
            .ThenBy(m => m.MetadataToken)
            .ToArray();
        MethodInfo chosen = candidates.FirstOrDefault(m => m.GetParameters().Length == arity)
            ?? candidates.FirstOrDefault(m => m.GetParameters().Length >= arity)
            ?? candidates.LastOrDefault();
        return chosen == null ? Target.Missing : new Target(chosen, typeof(Dm));
    }

    // ---- arguments ----

    /// <summary>Whether the call needs the general binder (named arguments or an arglist).</summary>
    internal static bool IsPlain(object[] args)
    {
        foreach (object arg in args)
        {
            if (arg is DmNamedArg || arg is DmArgList) return false;
        }
        return true;
    }

    /// <summary>
    /// A DM parameter name from a call-site or <see cref="DmParamsAttribute"/> name: a leading <c>@</c> dropped
    /// (rules 2.2), nothing else (a text key such as <c>"x/b"</c> names no parameter, unlike a proc path).
    /// </summary>
    internal static string ArgName(string name)
        => !string.IsNullOrEmpty(name) && name[0] == '@' ? name.Substring(1) : name;

    /// <summary>
    /// The arguments of a call in call order as (name, value): a <see cref="DmNamedArg"/> is named; an
    /// <see cref="DmArgList"/> is replaced by its list's items, where a text item that the list holds as a key (it has
    /// an associated value, null included) is a named argument (rules 7.3; BYOND ref <c>/proc/arglist</c>; OpenDream
    /// <c>DMProc.ToProcArguments</c> tests <c>argList.ContainsKey</c>, and tg's <c>AddComponent(..., message = null)</c>
    /// needs it, voidwalker/sunwalker_mob.dm:40), any other key is a runtime error ("List contains a non-string key,
    /// and cannot be used as an arglist", OpenDream), and a non-list gives no arguments (OpenDream: "Using a non-list
    /// gives you no arguments"; tg's callbacks pass <c>arglist(null)</c> when they hold no arguments, callback.dm).
    /// </summary>
    internal static List<KeyValuePair<string, object>> Expand(object[] args)
    {
        var entries = new List<KeyValuePair<string, object>>(args.Length);
        foreach (object arg in args)
        {
            switch (arg)
            {
                case DmNamedArg named:
                    entries.Add(new KeyValuePair<string, object>(ArgName(named.Name), named.Value));
                    break;
                case DmArgList arglist:
                    if (arglist.List is not DmList list) break;
                    for (int i = 0; i < list._count; i++)
                    {
                        object item = list._items[i]; // by position, also in alist mode (where a number is a key)
                        if (list.TryGetAssociated(item, out object value))
                        {
                            if (item is not string key) throw new DmRuntimeException("arglist(): the list has a non-text key and cannot be used as an argument list");
                            entries.Add(new KeyValuePair<string, object>(ArgName(key), value));
                        }
                        else
                        {
                            entries.Add(new KeyValuePair<string, object>(null, item));
                        }
                    }
                    break;
                default:
                    entries.Add(new KeyValuePair<string, object>(null, arg));
                    break;
            }
        }
        return entries;
    }

    /// <summary>The bound arguments as the pre-New hook sees them (positional in order, named in call order).</summary>
    internal static DmArgs ToDmArgs(List<KeyValuePair<string, object>> entries)
    {
        if (entries.Count == 0) return DmArgs.Empty;
        var positional = new List<object>(entries.Count);
        List<KeyValuePair<string, object>> named = null;
        foreach (KeyValuePair<string, object> entry in entries)
        {
            if (entry.Key == null) positional.Add(entry.Value);
            else (named ??= new List<KeyValuePair<string, object>>()).Add(entry);
        }
        return new DmArgs(positional.ToArray(), named?.ToArray());
    }

    /// <summary>Binds plain positional arguments (no names, no arglist): the fast path of every call.</summary>
    internal static object[] BindPositional(Target target, object[] args)
    {
        var bound = new object[target.ParameterCount];
        Array.Copy(target.Defaults, bound, target.Fixed);
        int count = Math.Min(args.Length, target.Fixed);
        for (int i = 0; i < count; i++)
            bound[i] = args[i] ?? target.Defaults[i];
        if (target.HasExtra)
        {
            int extra = args.Length - target.Fixed;
            if (extra > 0)
            {
                var extras = new object[extra];
                Array.Copy(args, target.Fixed, extras, 0, extra);
                bound[target.Fixed] = extras;
            }
            else
            {
                bound[target.Fixed] = Array.Empty<object>();
            }
        }
        return bound;
    }

    /// <summary>
    /// Binds arguments in call order (OpenDream <c>DMProc.ToProcArguments</c>): a positional argument to the DM
    /// parameter at its position in the call, a named one to the parameter of that name; later ones overwrite.
    /// With <paramref name="locIsFirst"/> (an atom's <c>New</c>) the name <c>loc</c> means the first parameter when
    /// the definition calls it otherwise (BYOND ref "New proc (atom)": the loc parameter name is hard-coded).
    /// </summary>
    internal static object[] Bind(Target target, List<KeyValuePair<string, object>> entries, bool locIsFirst)
    {
        var bound = new object[target.ParameterCount];
        Array.Copy(target.Defaults, bound, target.Fixed);
        List<object> extras = null;
        for (int position = 0; position < entries.Count; position++)
        {
            KeyValuePair<string, object> entry = entries[position];
            int index = position;
            if (entry.Key != null)
            {
                index = target.IndexOfName(entry.Key);
                if (index < 0 && locIsFirst && entry.Key == "loc") index = 0;
                if (index < 0) throw new DmRuntimeException(target.Display + " has no argument named \"" + entry.Key + "\"");
            }
            if (index < target.Fixed)
            {
                bound[index] = entry.Value ?? target.Defaults[index];
            }
            else if (target.HasExtra)
            {
                extras ??= new List<object>();
                while (extras.Count <= index - target.Fixed) extras.Add(null);
                extras[index - target.Fixed] = entry.Value;
            }
        }
        if (target.HasExtra) bound[target.Fixed] = extras?.ToArray() ?? Array.Empty<object>();
        return bound;
    }

    /// <summary>Binds <paramref name="args"/> (any mix of positional, named and arglist items) to <paramref name="target"/>.</summary>
    internal static object[] BindAny(Target target, object[] args)
        => IsPlain(args) ? BindPositional(target, args) : Bind(target, Expand(args), locIsFirst: false);

    /// <summary>A runtime error for a proc that does not exist ("undefined proc").</summary>
    internal static DmRuntimeException UndefinedProc(object receiver, string name)
    {
        string where = receiver == null || receiver is string
            ? "global proc"
            : (DmTypes.PathOf(DmTypes.Registry.DmTypeOf(receiver.GetType())) ?? receiver.GetType().Name);
        return new DmRuntimeException("undefined proc: " + DmName(name) + " (" + where + ")");
    }

    // ---- invokers ----

    // A /world proc (worldProc) takes the receiver as its first C# argument; the bound DM arguments fill the others.
    private static Func<object, object[], DmTask<object>> Compile(MethodInfo method, ParameterInfo[] parameters, bool worldProc)
    {
        ParameterExpression receiver = Expression.Parameter(typeof(object), "receiver");
        ParameterExpression bound = Expression.Parameter(typeof(object[]), "bound");
        var arguments = new Expression[parameters.Length];
        int first = worldProc ? 1 : 0;
        if (worldProc) arguments[0] = Expression.Convert(receiver, typeof(DmWorld));
        for (int i = first; i < parameters.Length; i++)
        {
            Expression slot = Expression.ArrayIndex(bound, Expression.Constant(i - first));
            arguments[i] = parameters[i].ParameterType == typeof(object[])
                ? Expression.Convert(slot, typeof(object[]))
                : DmConvert.FromObject(slot, parameters[i].ParameterType);
        }
        Expression call = method.IsStatic
            ? Expression.Call(method, arguments)
            : Expression.Call(Expression.Convert(receiver, method.DeclaringType), method, arguments);
        return Expression.Lambda<Func<object, object[], DmTask<object>>>(AsTask(call, method.ReturnType), receiver, bound).Compile();
    }

    // The callee's result as a DmTask<object> (waiting-model.md 4.5).
    private static Expression AsTask(Expression call, Type returnType)
    {
        if (returnType == typeof(void)) return Expression.Block(call, Expression.Default(typeof(DmTask<object>)));
        if (returnType == typeof(DmTask) || (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(DmTask<>)))
            return Expression.Call(call, returnType.GetMethod(nameof(IDmTask.AsObjectTask), Type.EmptyTypes));
        return Expression.Call(FromResultMethod, DmConvert.ToObject(call, returnType));
    }
}
