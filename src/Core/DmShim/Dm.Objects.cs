using System.Globalization;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.DmShim;

// Owner: dynamic (named and arglist were written by contracts). Object creation, calls by name, argument plumbing of
// variadic slots, initial() and runtime var access (docs/ai/translation-rules.md 3.5, 4.8, 7.2-7.7, 10.1-10.4;
// docs/ai/specs/object-model.md sections 3-6 and 12). The machinery lives in DmBinder, DmPrototypes, DmVars and
// DmTypes; conformance rows: docs/ai/specs/dm-conformance.md, "Dynamic calls and types".
public static partial class Dm
{
    /// <summary>
    /// DM <c>new T(args)</c> with a <c>Type</c> value (BYOND ref <c>/proc/new</c>; rules 10.1; object-model.md
    /// section 4): construct, assign the world id and register, run the pre-New hook, call <c>New</c> through the
    /// binder, return the object. Arguments may contain <c>Dm.named</c> items and one <c>Dm.arglist</c>. Builtin value
    /// types (<c>/list</c>, <c>/sound</c>, <c>/matrix</c>, <c>/exception</c>, ...) are built by their own factory and get
    /// no id. If <c>New</c> sleeps, the no-sleep rule applies (DV5).
    /// </summary>
    public static object @new(Type type, params object[] args)
    {
        args ??= OneNull();
        object obj = Create(type, args, out DmTask<object> newCall);
        // A completed New: a plain call (a captured error is rethrown); a sleeping one: the no-sleep rule (DV5).
        Waiting.NoSleep(newCall, newCall.IsCompleted ? null : "new " + (DmTypes.PathOf(type) ?? type.Name));
        return obj;
    }

    /// <summary>DM <c>new /path(args)</c> and the implied-type <c>new(args)</c>: <see cref="@new(Type, object[])"/> for <typeparamref name="T"/>.</summary>
    public static T @new<T>(params object[] args) where T : class => (T)@new(typeof(T), args);

    /// <summary>
    /// <c>new</c> of a type whose <c>New</c> may sleep (<c>docs/ai/specs/generated/sleeping-new.tsv</c>), inside a coloured
    /// proc: <c>await Dm.new_async&lt;T&gt;(...)</c>; completes when <c>New</c> returns (rules 10.1).
    /// </summary>
    public static DmTask<T> new_async<T>(params object[] args) where T : class
    {
        args ??= OneNull();
        try
        {
            T obj = (T)Create(typeof(T), args, out DmTask<object> newCall);
            if (!newCall.IsCompleted) return AwaitNew(newCall, obj);
            return newCall.IsFaulted ? DmTask<T>.FromException(newCall.Exception) : DmTask<T>.FromResult(obj);
        }
        catch (Exception e)
        {
            return DmTask<T>.FromException(e);
        }
    }

    /// <summary><c>new T(...)</c> with a computed <c>Type</c> inside an async proc: <c>await Dm.new_async(T, ...)</c>.</summary>
    public static DmTask<object> new_async(Type type, params object[] args)
    {
        args ??= OneNull();
        try
        {
            object obj = Create(type, args, out DmTask<object> newCall);
            if (!newCall.IsCompleted) return AwaitNew(newCall, obj);
            return newCall.IsFaulted ? DmTask<object>.FromException(newCall.Exception) : DmTask<object>.FromResult(obj);
        }
        catch (Exception e)
        {
            return DmTask<object>.FromException(e);
        }
    }

    // The creating proc waits while New sleeps, then gets the object (BYOND ref /proc/new; rules 10.1).
    private static async DmTask<T> AwaitNew<T>(DmTask<object> newCall, T obj)
    {
        await newCall;
        return obj;
    }

    /// <summary>A named argument of a dynamic call, <c>Dm.@new</c> or a variadic macro (rules 7.3): <c>Dm.named("_z", a.z)</c>.</summary>
    public static DmNamedArg named(string name, object value) => new DmNamedArg(name, value);

    /// <summary>DM <c>arglist(L)</c> (BYOND ref <c>/proc/arglist</c>; rules 7.3): only ever the whole argument list of a call.</summary>
    public static DmArgList arglist(object L) => new DmArgList(L);

    /// <summary>
    /// A call by name (rules 7.7; object-model.md section 12): <c>call(obj, name)(args)</c>, <c>PROC_REF</c> targets,
    /// <c>arglist</c> calls. Resolves the C# or DM name on the runtime type (virtual dispatch; a null receiver or
    /// <c>GLOBAL_PROC</c> means a global proc, and so does a global proc path <c>"/proc/x"</c> with any receiver, as
    /// <c>call(object, delegate)</c> of tg's <c>CALLBACK(src, GLOBAL_PROC_REF(x))</c>), binds positional, named and
    /// arglist arguments (missing ones null, extras to <c>__extra</c> or dropped), converts them by rules 3.5 and
    /// invokes. Always a
    /// <see cref="DmTask{T}"/> (completed for a synchronous callee): use it with <c>await</c>,
    /// <c>Waiting.NoSleep</c> or <c>Waiting.Detach</c>. An unknown proc or argument name is a runtime error.
    /// <para>
    /// Like a call of a <c>DmTask</c> slot, the call expression never throws: a runtime error of the binder or of the
    /// callee, synchronous or not, is captured in the returned task with its original exception, and <c>await</c> or
    /// <c>Waiting.NoSleep</c> rethrows it in the caller while <c>Waiting.Detach</c> reports it (INVOKE_ASYNC of a
    /// proc that fails never stops its caller, waiting-model.md 5.2). A text receiver is <c>GLOBAL_PROC</c>
    /// (BYOND's text first argument named an external library, which tg does not use through <c>call()</c>).
    /// </para>
    /// </summary>
    public static DmTask<object> Call(object src, string proc, params object[] args)
    {
        args ??= OneNull();
        try
        {
            DmBinder.Target target = DmBinder.Resolve(src, proc);
            if (target == DmBinder.Target.Missing) throw DmBinder.UndefinedProc(src, proc);
            return target.Invoke(src, DmBinder.BindAny(target, args));
        }
        catch (Exception e)
        {
            return DmTask<object>.FromException(e);
        }
    }

    /// <summary>
    /// The extra DM parameter at <paramref name="index"/> of a variadic slot (rules 7.2):
    /// <c>object a = Dm.arg(__extra, 0);</c>. Null when there are fewer extras (a null <c>__extra</c> is one null).
    /// </summary>
    public static object arg(object[] extra, int index)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index), index, "Extra parameters are numbered from 0.");
        return extra != null && index < extra.Length ? extra[index] : null;
    }

    /// <summary>
    /// The extra DM parameter at <paramref name="index"/> converted to its declared type by rules 3.5 (rules 7.2):
    /// <c>Dm.arg&lt;obj_item&gt;(__extra, 1)</c> (<c>as</c> semantics), <c>Dm.arg&lt;float?&gt;(__extra, 2)</c>
    /// (<c>Dm.num_or_null</c>).
    /// </summary>
    public static T arg<T>(object[] extra, int index) => DmConvert.To<T>()(arg(extra, index));

    /// <summary>
    /// <c>..()</c> without arguments in a variadic definition (rules 7.5): a copy of <paramref name="extra"/>, extended
    /// with nulls to at least <paramref name="values"/>.Length entries, whose first entries are replaced by the current
    /// values of the parameters this definition unpacked.
    /// </summary>
    public static object[] forward(object[] extra, params object[] values)
    {
        extra ??= OneNull();
        values ??= OneNull();
        var result = new object[Math.Max(extra.Length, values.Length)];
        Array.Copy(extra, result, extra.Length);
        Array.Copy(values, result, values.Length);
        return result;
    }

    /// <summary>
    /// DM <c>args</c> (rules 7.6): <c>DmList args = Dm.args(p1, p2, __extra);</c>, a list of the slot's parameters in
    /// order followed by the extras. The last argument is always the extras array (<c>__extra</c> or a
    /// <c>Dm.forward(...)</c>) and is flattened; when it is the only argument C# passes it as the whole array.
    /// </summary>
    public static DmList args(params object[] values)
    {
        values ??= OneNull(); // Dm.args(__extra) with a null __extra: one null extra
        var result = new DmList();
        if (values.Length == 0) return result;
        for (int i = 0; i < values.Length - 1; i++) result.AppendItem(values[i]);
        object last = values[^1];
        if (last is object[] extras)
        {
            foreach (object item in extras) result.AppendItem(item);
        }
        else
        {
            result.AppendItem(last); // the values were the extras array itself, or a null __extra (one null)
        }
        return result;
    }

    /// <summary>
    /// DM <c>initial(x.var)</c>, <c>initial(T.var)</c>, <c>T::var</c> (rules 10.3; object-model.md section 5): reads
    /// <paramref name="read"/> on the cached prototype of <paramref name="x"/>'s runtime type (or of <paramref name="x"/>
    /// itself when it is a <c>Type</c>): <c>Dm.initial(x, (obj_item p) =&gt; p.force)</c>. A runtime-initialized var
    /// reads null, as in DM.
    /// </summary>
    public static TResult initial<T, TResult>(object x, Func<T, TResult> read) where T : class
    {
        ArgumentNullException.ThrowIfNull(read);
        // M3 P1 (lead-authorised, perf; types-converter.md 9): a type's compile-time value comes from the generated table
        // when it has it, so no prototype is built; a hand-written var block's class answers with its own prototype.
        Type stopType = null;
        if (x is Type t && DmTypes.Registry.IsDmClass(t) && DmTypeConstants.Instance.TryRead(t, read, out TResult constant, out stopType))
            return constant;
        object prototype = stopType != null ? DmPrototypes.Prototype(stopType) : PrototypeOf(x);
        if (prototype is not T typed)
            throw new DmRuntimeException("initial(): " + KindOf(prototype) + " is not a " + (DmTypes.PathOf(typeof(T)) ?? typeof(T).Name));
        return read(typed);
    }

    /// <summary>DM <c>initial(x.var)</c> with an untyped <c>x</c> (rules 10.3): <c>Dm.initial(x, "var")</c>.</summary>
    public static object initial(object x, string var) => DmVars.Read(PrototypeOf(x), var);

    // The prototype initial() reads: of the value's runtime type, or of the type itself for a Type value. Only DM types
    // have compile-time values; no object of a host or BCL class is ever built as a prototype.
    private static object PrototypeOf(object x)
    {
        if (x == null) throw new DmRuntimeException("initial(): cannot read a var of null");
        Type type = x as Type ?? x.GetType();
        if (!DmTypes.Registry.IsDmClass(type))
            throw new DmRuntimeException("initial(): " + (x is Type ? type.FullName + " is not a DM type" : KindOf(x) + " has no DM vars"));
        return DmPrototypes.Prototype(type);
    }

    /// <summary>
    /// DM <c>a:b</c> read (rules 4.8): the var named <paramref name="var"/> of <paramref name="obj"/> looked up at run
    /// time; an unknown var or a null <paramref name="obj"/> is a runtime error. (Named <c>get_var</c> because tg has a
    /// global proc <c>/proc/get</c>, __HELPERS/atoms.dm:312.)
    /// </summary>
    public static object get_var(object obj, string var) => DmVars.Read(obj, var);

    /// <summary>DM <c>a:b = v</c> (rules 4.8): assigns the var by name (converted to its C# type by rules 3.5); returns <paramref name="value"/>.</summary>
    public static object set_var(object obj, string var, object value) => DmVars.Write(obj, var, value);

    /// <summary>DM <c>name in obj.vars</c> (syndicate A3: Dynamic's config check, _dynamic_ruleset.dm:96): whether <paramref name="obj"/> declares the var, without reading it (rules 10.7: <c>vars</c> itself is not translated).</summary>
    public static bool has_var(object obj, string var) => obj != null && (var is "type" or "parent_type" or "vars" || DmVars.Find(obj.GetType(), DmBinder.DmName(var)).Get != null);

    /// <summary>DM <c>a?:b</c> (rules 4.8): null for a null <paramref name="obj"/>, else <see cref="get_var"/>.</summary>
    public static object get_var_or_null(object obj, string var) => obj == null ? null : DmVars.Read(obj, var);

    /// <summary>
    /// DM <c>locate(T) in C</c> and <c>locate(T)</c> in the world (rules 10.4): the first <typeparamref name="T"/> in
    /// <paramref name="container"/> (a list, an atom's contents, or <c>world</c>: registry id order); null when there is
    /// none or the container is null. In the world it searches like a loop without <c>in</c> (<see cref="each{T}()"/>):
    /// the atoms of an atom type (world.contents), the registered non-atom datums of a datum type (tg locates no datum
    /// type in the world at the snapshot; the BYOND reference only says "The default is world").
    /// </summary>
    public static T locate<T>(object container) where T : class
    {
        // BYOND ref /proc/locate: "Types are matched in the same manner as istype()"; "the first one found".
        if (container is DmWorld)
        {
            foreach (T item in each<T>())
                return item;
            return null;
        }
        foreach (T item in each<T>(container))
            return item;
        return null;
    }

    /// <summary>
    /// DM <c>locate(T) in C</c> with a typepath value <paramref name="type"/> (rules 10.4; M2 integration, the SHIM-GAP of
    /// M2 P2 and P3): the first item of that type (as <c>istype</c>) in <paramref name="container"/> (a list, an atom's
    /// contents, or <c>world</c>); null when there is none, the container or the type is null.
    /// </summary>
    public static object locate_type(Type type, object container)
    {
        if (type == null || container == null) return null;
        foreach (object item in each(container))
        {
            if (istype(item, type))
                return item;
        }
        return null;
    }

    /// <summary>
    /// DM <c>locate(TextRef)</c> and <c>locate(Tag)</c> (BYOND ref <c>/proc/locate</c>; rules 10.4): the value whose
    /// <c>REF()</c> text (<c>"[0x&lt;hex&gt;]"</c>: a datum, a list, a typepath or <c>world</c>) or tag is
    /// <paramref name="text"/>; null for a missing, collected or qdel'd object and a collected list (object-model.md
    /// section 3).
    /// </summary>
    public static object locate_ref(object text)
    {
        if (text is not string s || s.Length == 0) return null;
        // OpenDream DreamRefManager.LocateRef(string): "[0x...]" is a reference; otherwise, with the brackets
        // stripped, a tag ("Doing locate("[abc]") is the same as locate("abc")").
        if (s.Length > 2 && s[0] == '[' && s[^1] == ']')
        {
            s = s.Substring(1, s.Length - 2);
            if (s.StartsWith("0x", StringComparison.Ordinal)
                && long.TryParse(s.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out long id))
            {
                return DmRefs.Locate(id);
            }
        }
        return FindTagged(s);
    }

    // locate(Tag): the first live registered object, in id order, whose tag var is the text (BYOND ref /datum/var/tag:
    // tags are meant to be unique). The registry's tag index (ObjectRegistry.FindTagged, M3 I1b) replaced a linear scan
    // of every live object; the result is the same.
    private static object FindTagged(string tag)
    {
        ObjectRegistry objects = World.Current.Objects;
        if (!objects.TagIndexIncomplete) return objects.FindTagged(tag);
        long cursor = 0;
        while (objects.TryGetNextLive(cursor, out long id, out object obj))
        {
            cursor = id;
            DmVars.Accessor accessor = DmVars.Find(obj.GetType(), "tag");
            if (accessor.Get != null && accessor.Get(obj) is string value && string.Equals(value, tag, StringComparison.Ordinal))
                return obj;
        }
        return null;
    }

    /// <summary>DM <c>parent_type</c> of a type (rules 10.7): the DM parent's <c>Type</c>, null for <c>/datum</c>.</summary>
    public static Type parent_type(Type type) => DmTypes.ParentOf(type);

    // ---- creation (object-model.md section 4) ----

    // Steps 1-6 of object-model.md section 4. Returns the object and the task of its New call (completed when New
    // did not sleep or there is none).
    private static object Create(Type type, object[] args, out DmTask<object> newCall)
    {
        if (type == null) throw new DmRuntimeException("new: cannot create an object of type null");
        // OpenDream DMOpcodeHandlers.CreateObject: a value that is not a DM type cannot be created. A host or BCL class
        // (no [DmPath] on it or on a base class, no IDmObject) is not one.
        if (!DmTypes.Registry.IsDmClass(type)) throw new DmRuntimeException("new: cannot create an object of type " + type.FullName + ": it is not a DM type");
        newCall = default;
        if (type == typeof(DmList)) return NewList(args);
        if (!typeof(IDmObject).IsAssignableFrom(type)) return NewBuiltin(type, args);

        // 2. Construct: field initializers and constructor bodies only (the World-free part).
        var obj = (IDmObject)DmPrototypes.Construct(type);
        // 3. Identity and registration; a builtin value type implemented as a datum (Tg's /image and
        // /mutable_appearance, which tg extends) gets neither (object-model.md section 3).
        if (!DmTypes.Registry.IsBuiltinValue(type))
        {
            World world = World.Current;
            long id = world.AllocateId();
            while (DmRefs.IsReservedId(id)) id = world.AllocateId(); // ids that would print as a list, type or world ref
            obj.dm_assign_id(id);
            world.Objects.Register(obj.dm_id, obj);
            if (obj is not IDmTagIndexed) world.Objects.TagIndexIncomplete = true; // locate(tag) scans in this world
        }
        // 4. Bind the arguments, 5. the pre-New hook, 6. New through the binder. Plain positional arguments (the
        // common case) need no expansion.
        if (DmBinder.IsPlain(args))
        {
            obj.dm_pre_new(args.Length == 0 ? DmArgs.Empty : new DmArgs(args, null));
            DmBinder.Target plainNew = DmBinder.Resolve(obj, "New");
            if (plainNew != DmBinder.Target.Missing) newCall = plainNew.Invoke(obj, DmBinder.BindPositional(plainNew, args));
            return obj;
        }
        List<KeyValuePair<string, object>> entries = DmBinder.Expand(args);
        obj.dm_pre_new(DmBinder.ToDmArgs(entries));
        DmBinder.Target target = DmBinder.Resolve(obj, "New");
        if (target != DmBinder.Target.Missing)
            newCall = target.Invoke(obj, DmBinder.Bind(target, entries, locIsFirst: obj is IDmAtom));
        return obj;
    }

    // new /list(Size) and new /list(Size1, Size2, ...) (BYOND ref /list: "new/list(size) creates a list with size
    // (null) elements", and "var/grid = new/list(10,5)" creates "a list of 10 lists each having 5 elements"; OpenDream
    // DreamList.Initialize and DMStandard List.dm, New(Size)). tg: new /list(world.maxz, 0), one empty list per
    // z-level (controllers/subsystem/mobs.dm:22, ai_controllers.dm:149).
    private static DmList NewList(object[] args)
    {
        List<KeyValuePair<string, object>> entries = DmBinder.Expand(args);
        var sizes = new List<object>(Math.Max(entries.Count, 1));
        for (int position = 0; position < entries.Count; position++)
        {
            KeyValuePair<string, object> entry = entries[position];
            if (entry.Key != null && entry.Key != "Size")
                throw new DmRuntimeException("/list/proc/New has no argument named \"" + entry.Key + "\"");
            int index = entry.Key != null ? 0 : position; // a later argument for the same position wins
            while (sizes.Count <= index) sizes.Add(null);
            sizes[index] = entry.Value;
        }
        // One size: that many nulls (a non-number gives an empty list, OpenDream); several: one level per size.
        if (sizes.Count <= 1)
            return sizes.Count == 1 && TryGetNumber(sizes[0], out float n) ? list_n(n) : new DmList();
        return NewListLevel(sizes, 0);
    }

    // One level of a multidimensional new /list: "BYOND coerces negative/non-num values to zero for multidimensional
    // lists" (OpenDream DreamList.Initialize, which truncates the others); the last level holds nulls, every other one
    // a new list per item.
    private static DmList NewListLevel(List<object> sizes, int level)
    {
        float n = TryGetNumber(sizes[level], out float number) && number >= 1f ? number : 0f;
        if (n >= int.MaxValue) throw new DmRuntimeException("new /list: list too long");
        int count = (int)n;
        var result = new DmList();
        bool last = level == sizes.Count - 1;
        for (int i = 0; i < count; i++)
            result.AppendItem(last ? null : NewListLevel(sizes, level + 1));
        return result;
    }

    // Builtin value types (object-model.md section 4, step 1): no id, never registered.
    private static object NewBuiltin(Type type, object[] args)
    {
        if (type == typeof(exception)) return NewException(args);
        List<KeyValuePair<string, object>> entries = DmBinder.Expand(args);
        DmBinder.Target factory = DmBinder.Factory(type, entries.Count);
        if (factory != DmBinder.Target.Missing)
            return Waiting.NoSleep(factory.Invoke(null, DmBinder.Bind(factory, entries, locIsFirst: false)), "new " + DmTypes.PathOf(type));
        object obj = DmPrototypes.Construct(type);
        DmBinder.Target newProc = DmBinder.Resolve(obj, "New");
        if (newProc != DmBinder.Target.Missing)
            Waiting.NoSleep(newProc.Invoke(obj, DmBinder.Bind(newProc, entries, locIsFirst: false)), "new " + (DmTypes.PathOf(type) ?? type.Name));
        return obj;
    }

    // BYOND's /exception/New(N, F, L) (OpenDream DMStandard Exception.dm: name = N, file = F, line = L), which the
    // stddef macro EXCEPTION(value) calls as new /exception(value, __FILE__, __LINE__).
    private static exception NewException(object[] args)
    {
        var values = new object[3];
        List<KeyValuePair<string, object>> entries = DmBinder.Expand(args);
        for (int position = 0; position < entries.Count; position++)
        {
            KeyValuePair<string, object> entry = entries[position];
            int index = entry.Key switch
            {
                null => position,
                "N" => 0,
                "F" => 1,
                "L" => 2,
                _ => throw new DmRuntimeException("/exception/proc/New has no argument named \"" + entry.Key + "\""),
            };
            if (index < values.Length) values[index] = entry.Value;
        }
        return new exception { name = values[0], file = values[1] as string, line = num(values[2]) };
    }
}
