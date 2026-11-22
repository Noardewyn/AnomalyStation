using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Anomaly.Core.DmShim;

// Owner: dynamic. Construction factories and the prototypes behind initial() (docs/ai/specs/object-model.md
// sections 4 step 2 and 5; translation-rules.md 10.2, 10.3).

/// <summary>
/// The cached factory of each type (its public parameterless constructor, compiled once) and the prototype of each
/// type: one instance per type built by the factory alone, so it holds the field initializers and the
/// constructor-assigned overrides of its type and nothing else (no world id, no registration, no
/// <c>dm_init_vars</c>, no <c>New</c>). Constructors never touch the World (object-model.md section 3), so a
/// prototype can be built without an ambient world and building one changes no world. Both caches are append-only
/// and process-wide (allowed immutable statics, object-model.md section 10); a prototype is never handed out to
/// translated code, which only reads it through <c>Dm.initial</c>.
/// </summary>
internal static class DmPrototypes
{
    private static readonly ConcurrentDictionary<Type, Func<object>> s_factories = new();
    private static readonly ConcurrentDictionary<Type, Lazy<object>> s_prototypes = new();

    /// <summary>A new instance from the type's parameterless constructor (field initializers and constructor bodies only).</summary>
    internal static object Construct(Type type) => s_factories.GetOrAdd(type, CompileFactory)();

    /// <summary>The prototype of <paramref name="type"/>, built on first use.</summary>
    internal static object Prototype(Type type)
        => s_prototypes.GetOrAdd(type, static t => new Lazy<object>(() => Construct(t))).Value;

    private static Func<object> CompileFactory(Type type)
    {
        string what = DmTypes.PathOf(type) ?? type.FullName;
        if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters)
            return () => throw new DmRuntimeException("cannot create an object of type " + what);
        ConstructorInfo constructor = type.GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        if (constructor == null)
            return () => throw new DmRuntimeException("cannot create an object of type " + what + ": it has no public parameterless constructor");
        return () => Activator.CreateInstance(type);
    }
}
