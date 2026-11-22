using System.Linq.Expressions;
using System.Reflection;

namespace Anomaly.Core.DmShim;

// Owner: dynamic. The conversions of docs/ai/translation-rules.md 3.5 for values that arrive as `object` and must
// fit a C# parameter, field or local of another type: the binder's arguments, Dm.arg<T>, Dm.set_var.

/// <summary>
/// Rules 3.5 as code: <c>float</c> by <c>Dm.num</c>, <c>float?</c> by <c>Dm.num_or_null</c>, <c>string</c> by a
/// checked cast (text or null; anything else is a "type mismatch" runtime error), a class, <c>DmList</c> or
/// <c>Type</c> by <c>as</c> (a value of another kind becomes null, the DM implicit downcast), <c>object</c> as is. A
/// C# <c>bool</c> (some shim builtins take one) is DM truthiness; other C# numeric types go through <c>Dm.num</c>.
/// </summary>
internal static class DmConvert
{
    private static readonly MethodInfo NumMethod = typeof(Dm).GetMethod(nameof(Dm.num), new[] { typeof(object) });
    private static readonly MethodInfo NumOrNullMethod = typeof(Dm).GetMethod(nameof(Dm.num_or_null), new[] { typeof(object) });
    private static readonly MethodInfo TruthyMethod = typeof(Dm).GetMethod(nameof(Dm.truthy), new[] { typeof(object) });
    private static readonly MethodInfo ToTextMethod = typeof(DmConvert).GetMethod(nameof(ToText), BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly MethodInfo ToStructMethod = typeof(DmConvert).GetMethod(nameof(ToStruct), BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly MethodInfo BoolToValueMethod = typeof(DmConvert).GetMethod(nameof(BoolToValue), BindingFlags.NonPublic | BindingFlags.Static);

    private static readonly object One = 1f;
    private static readonly object Zero = 0f;

    /// <summary>A value as text by rules 3.5 (<c>(string)x</c>): text and null pass, anything else is a type mismatch.</summary>
    internal static string ToText(object value) => value switch
    {
        null => null,
        string text => text,
        _ => throw Dm.TypeMismatch(value, "text"),
    };

    /// <summary>A C# <c>bool</c> as a DM value: the number 1 or 0 (rules 3.1), boxed once.</summary>
    internal static object BoolToValue(bool value) => value ? One : Zero;

    // A struct parameter of a builtin that rules 3.5 does not cover: null is its default, a wrong kind a mismatch.
    private static T ToStruct<T>(object value) where T : struct => value switch
    {
        null => default,
        T typed => typed,
        _ => throw Dm.TypeMismatch(value, typeof(T).Name),
    };

    /// <summary>An expression converting <paramref name="value"/> (of type <c>object</c>) to <paramref name="target"/>.</summary>
    internal static Expression FromObject(Expression value, Type target)
    {
        if (target == typeof(object)) return value;
        if (target == typeof(float)) return Expression.Call(NumMethod, value);
        if (target == typeof(float?)) return Expression.Call(NumOrNullMethod, value);
        if (target == typeof(string)) return Expression.Call(ToTextMethod, value);
        if (target == typeof(bool)) return Expression.Call(TruthyMethod, value);
        if (target == typeof(int) || target == typeof(long) || target == typeof(double) || target == typeof(short)
            || target == typeof(byte) || target == typeof(uint) || target == typeof(ulong) || target == typeof(decimal))
            return Expression.Convert(Expression.Call(NumMethod, value), target);
        if (!target.IsValueType) return Expression.TypeAs(value, target);
        if (Nullable.GetUnderlyingType(target) != null) return Expression.TypeAs(value, target);
        return Expression.Call(ToStructMethod.MakeGenericMethod(target), value);
    }

    /// <summary>An expression boxing a value of type <paramref name="type"/> as a DM value (<c>bool</c> becomes 1 or 0).</summary>
    internal static Expression ToObject(Expression value, Type type)
    {
        if (type == typeof(bool)) return Expression.Call(BoolToValueMethod, value);
        return type == typeof(object) ? value : Expression.Convert(value, typeof(object));
    }

    /// <summary>The cached converter from <c>object</c> to <typeparamref name="T"/> (<c>Dm.arg&lt;T&gt;</c>).</summary>
    internal static Func<object, T> To<T>() => Converter<T>.Convert;

    /// <summary>A converter from <c>object</c> to <paramref name="target"/>, boxed again (<c>Dm.set_var</c>).</summary>
    internal static Func<object, object> Boxed(Type target)
    {
        ParameterExpression value = Expression.Parameter(typeof(object), "value");
        Expression body = ToObject(FromObject(value, target), target);
        return Expression.Lambda<Func<object, object>>(body, value).Compile();
    }

    // One compiled converter per T: an immutable per-type cache.
    private static class Converter<T>
    {
        internal static readonly Func<object, T> Convert = Build();

        private static Func<object, T> Build()
        {
            ParameterExpression value = Expression.Parameter(typeof(object), "value");
            return Expression.Lambda<Func<object, T>>(FromObject(value, typeof(T)), value).Compile();
        }
    }
}
