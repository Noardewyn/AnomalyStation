using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Anomaly.Core.DmShim;

// Owner: dynamic. Runtime var access by name: DM `a:b`, `a?:b`, `a:b = v` and `initial(x.var)` with an untyped x
// (docs/ai/translation-rules.md 3.8, 4.8, 10.3).

/// <summary>
/// Cached accessors of DM vars by name: a DM var is a public instance field (rules 3.8) or, for the engine's builtin
/// vars, a public property (<c>loc</c>, <c>contents</c>, <c>type</c>, ...). One compiled getter and setter per
/// (runtime type, name), built on first use; no reflection per access. The most derived declaration wins (a var a
/// subtype hides with <c>new</c>). Writes convert the value by rules 3.5 to the member's C# type.
/// </summary>
internal static class DmVars
{
    internal sealed class Accessor
    {
        internal static readonly Accessor Missing = new(null, null, null, null);

        internal Accessor(string name, Type varType, Func<object, object> get, Action<object, object> set)
        {
            Name = name;
            VarType = varType;
            Get = get;
            Set = set;
        }

        internal string Name { get; }
        internal Type VarType { get; }
        internal Func<object, object> Get { get; }

        /// <summary>Null for a read-only member.</summary>
        internal Action<object, object> Set { get; }
    }

    private static readonly ConcurrentDictionary<(Type Type, string Name), Accessor> s_cache = new();

    /// <summary>The accessor of var <paramref name="name"/> on <paramref name="type"/>, or <see cref="Accessor.Missing"/>.</summary>
    internal static Accessor Find(Type type, string name) => s_cache.GetOrAdd((type, name), static key => Build(key.Type, key.Name));

    /// <summary>DM <c>obj:name</c>: the var's value; the engine's <c>type</c> and <c>parent_type</c> when not declared.</summary>
    internal static object Read(object obj, string name)
    {
        if (obj == null) throw new DmRuntimeException("Cannot read null." + name);
        name = DmBinder.DmName(name);
        Accessor accessor = Find(obj.GetType(), name);
        if (accessor.Get != null) return accessor.Get(obj);
        if (!DmTypes.Registry.IsDmClass(obj.GetType())) throw UndefinedVar(obj, name);
        switch (name)
        {
            case "type":
                return DmTypes.Registry.DmTypeOf(obj.GetType()) ?? obj.GetType();
            case "parent_type":
                return DmTypes.ParentOf(DmTypes.Registry.DmTypeOf(obj.GetType()) ?? obj.GetType());
            case "vars":
                Dm.NotTranslated("docs/ai/translation-rules.md 10.7", "datum.vars");
                return null;
        }
        throw UndefinedVar(obj, name);
    }

    /// <summary>DM <c>obj:name = value</c>: assigns the var, converted by rules 3.5; returns <paramref name="value"/>.</summary>
    internal static object Write(object obj, string name, object value)
    {
        if (obj == null) throw new DmRuntimeException("Cannot modify null." + name);
        name = DmBinder.DmName(name);
        Accessor accessor = Find(obj.GetType(), name);
        if (accessor.Get == null) throw UndefinedVar(obj, name);
        if (accessor.Set == null) throw new DmRuntimeException("Cannot modify " + Describe(obj) + "." + name + ": it is read-only");
        accessor.Set(obj, value);
        return value;
    }

    private static DmRuntimeException UndefinedVar(object obj, string name)
        => new("undefined variable " + Describe(obj) + "." + name);

    private static string Describe(object obj) => DmTypes.PathOf(DmTypes.Registry.DmTypeOf(obj.GetType())) ?? obj.GetType().Name;

    private static Accessor Build(Type type, string name)
    {
        if (!DmTypes.Registry.IsDmClass(type)) return Accessor.Missing; // a number, a Type, a host object: no DM vars
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        for (Type level = type; level != null && level != typeof(object); level = level.BaseType)
        {
            FieldInfo field = level.GetField(name, flags);
            if (field != null) return ForField(field);
            PropertyInfo property = level.GetProperties(flags).FirstOrDefault(p => p.Name == name && p.GetIndexParameters().Length == 0);
            if (property != null && property.GetMethod != null && property.GetMethod.IsPublic && !property.PropertyType.IsByRef && !property.PropertyType.IsByRefLike)
                return ForProperty(property);
        }
        return Accessor.Missing;
    }

    private static Accessor ForField(FieldInfo field)
    {
        ParameterExpression obj = Expression.Parameter(typeof(object), "obj");
        ParameterExpression value = Expression.Parameter(typeof(object), "value");
        Expression typed = Expression.Convert(obj, field.DeclaringType);
        MemberExpression member = Expression.Field(typed, field);
        Func<object, object> get = Expression.Lambda<Func<object, object>>(DmConvert.ToObject(member, field.FieldType), obj).Compile();
        Action<object, object> set = null;
        if (!field.IsInitOnly && !field.IsLiteral)
        {
            set = Expression.Lambda<Action<object, object>>(
                Expression.Assign(Expression.Field(typed, field), DmConvert.FromObject(value, field.FieldType)), obj, value).Compile();
        }
        return new Accessor(field.Name, field.FieldType, get, set);
    }

    private static Accessor ForProperty(PropertyInfo property)
    {
        ParameterExpression obj = Expression.Parameter(typeof(object), "obj");
        ParameterExpression value = Expression.Parameter(typeof(object), "value");
        Expression typed = Expression.Convert(obj, property.DeclaringType);
        Func<object, object> get = Expression.Lambda<Func<object, object>>(
            DmConvert.ToObject(Expression.Property(typed, property), property.PropertyType), obj).Compile();
        Action<object, object> set = null;
        if (property.SetMethod != null && property.SetMethod.IsPublic)
        {
            set = Expression.Lambda<Action<object, object>>(
                Expression.Assign(Expression.Property(typed, property), DmConvert.FromObject(value, property.PropertyType)), obj, value).Compile();
        }
        return new Accessor(property.Name, property.PropertyType, get, set);
    }
}
