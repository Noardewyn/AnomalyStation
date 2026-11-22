namespace Anomaly.Core.DmShim;

// Markers that translated code carries (docs/ai/translation-rules.md sections 2.3, 3.8, 7.2 and 7.9). Except for
// [DmPath], [DmParams] and [NotHashed], which the type registry, the binder and the state hasher read, they have
// no runtime effect: they keep tg's lint intent visible and checkable by tests and a later analyzer.

/// <summary>
/// The DM type path of a class (<c>[DmPath("/atom/movable")]</c>), exactly once per class, on the part in the
/// earliest file in <c>tgstation.dme</c> include order (rules 2.3). The type registry <c>DmTypes</c> is built from
/// these attributes; the shim's builtin value types carry it too (<c>/list</c>, <c>/sound</c>, ...).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class DmPathAttribute : Attribute
{
    public DmPathAttribute(string path) => Path = path;

    /// <summary>The DM path, starting with '/'.</summary>
    public string Path { get; }
}

/// <summary>
/// All DM parameter names, in order, of an override whose DM names differ from the slot's C# parameter names
/// (renamed or extra parameters unpacked from <c>__extra</c>); the binder matches named arguments against it
/// (rules 7.2, 7.3). Listed first among a method's attributes.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class DmParamsAttribute : Attribute
{
    public DmParamsAttribute(params string[] names) => Names = names ?? Array.Empty<string>();

    /// <summary>The DM parameter names in declaration order.</summary>
    public IReadOnlyList<string> Names { get; }
}

/// <summary>tg <c>SHOULD_CALL_PARENT(TRUE)</c> (rules 7.9).</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ShouldCallParentAttribute : Attribute
{
}

/// <summary>
/// tg <c>SHOULD_NOT_SLEEP(TRUE)</c>: the method is synchronous and calls procs of <c>DmTask</c> slots through
/// <c>Waiting.NoSleep</c> (rules 7.9, waiting-model.md 4.4).
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ShouldNotSleepAttribute : Attribute
{
}

/// <summary>tg <c>SIGNAL_HANDLER</c>: a synchronous signal handler (rules 7.9, waiting-model.md 4.4).</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class SignalHandlerAttribute : Attribute
{
}

/// <summary>tg <c>SHOULD_NOT_OVERRIDE(TRUE)</c>; the method stays <c>public virtual</c> (rules 7.9).</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ShouldNotOverrideAttribute : Attribute
{
}

/// <summary>tg <c>SHOULD_BE_PURE(TRUE)</c> (rules 7.9).</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ShouldBePureAttribute : Attribute
{
}

/// <summary>
/// tg <c>PROTECTED_PROC(TRUE)</c> on a proc or <c>VAR_PROTECTED</c> on a var; the member stays <c>public</c>
/// (rules 3.8, 7.9).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class DmProtectedAttribute : Attribute
{
}

/// <summary>
/// tg <c>PRIVATE_PROC(TRUE)</c> on a proc or <c>VAR_PRIVATE</c> on a var; the member stays <c>public</c>
/// (rules 3.8, 7.9).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class DmPrivateAttribute : Attribute
{
}

/// <summary>tg <c>VAR_FINAL</c> on a var; the field stays <c>public</c> (rules 3.8).</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class DmFinalAttribute : Attribute
{
}

/// <summary>
/// A field the state hash skips: an engine cache or bookkeeping that is not simulation state
/// (docs/ai/specs/object-model.md section 8).
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class NotHashedAttribute : Attribute
{
}

/// <summary>
/// Names the class that holds tg's global procs (<c>[assembly: DmGlobalProcs(typeof(GlobalProcs))]</c> in the Tg
/// assembly), so calls by name with a null receiver or <c>GLOBAL_PROC</c> resolve there
/// (docs/ai/specs/object-model.md section 11).
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class DmGlobalProcsAttribute : Attribute
{
    public DmGlobalProcsAttribute(Type type) => Type = type;

    /// <summary>The static class with the global procs.</summary>
    public Type Type { get; }
}

/// <summary>
/// Registers a class of the order source's assembly whose DM path the generated table does not list: a type of the
/// port's own (a deviation), which the registry would otherwise skip, since it registers that assembly from the table
/// only. It sorts after the listed types, as every unlisted type does.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class DmPortTypeAttribute : Attribute
{
    public DmPortTypeAttribute(Type type) => Type = type;

    /// <summary>The class, with its own <see cref="DmPathAttribute"/>.</summary>
    public Type Type { get; }
}

/// <summary>
/// Names the generated classes that hold the DM object-tree order (<c>[assembly: DmTypeOrder(typeof(TypeOrder),
/// typeof(ProcOrder))]</c> in the Tg assembly, docs/ai/specs/object-model.md section 9). The type registry reads
/// <paramref name="typeOrder"/>'s public static <c>Paths</c> (the DM type paths in order) and
/// <paramref name="procOrder"/>'s public static <c>Procs</c> and <c>Verbs</c> (type path to the proc or verb names that
/// <c>typesof(/t/proc)</c> and <c>typesof(/t/verb)</c> list); <paramref name="procOrder"/> may be null. Without this
/// attribute on any assembly the registry has no order source: types sort in a path-ordered preorder and
/// <c>typesof(/t/proc)</c> reads the class's methods. At most one assembly carries it.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class DmTypeOrderAttribute : Attribute
{
    public DmTypeOrderAttribute(Type typeOrder, Type procOrder)
    {
        TypeOrder = typeOrder;
        ProcOrder = procOrder;
    }

    /// <summary>The static class with <c>Paths</c>.</summary>
    public Type TypeOrder { get; }

    /// <summary>The static class with <c>Procs</c> and <c>Verbs</c>, or null.</summary>
    public Type ProcOrder { get; }
}
