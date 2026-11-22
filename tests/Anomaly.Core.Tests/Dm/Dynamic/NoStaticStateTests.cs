using System.Collections;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.Dynamic;

/// <summary>
/// object-model.md section 10: no class of the Tg or DmShim assembly has mutable static state (parallel tests use
/// separate worlds; all DM global and static state lives in per-world containers). A static field is either a
/// constant, or <c>readonly</c> and of an immutable type; the process-wide caches that are append-only maps keyed by
/// <c>Type</c> (prototypes, factories, binder targets, var accessors) are the allow-list.
/// </summary>
public class NoStaticStateTests
{
    // (declaring type, field): append-only caches of immutable entries keyed by Type (object-model.md sections 5, 10, 12).
    private static readonly HashSet<(Type, string)> AllowedCaches = new()
    {
        (typeof(DmPrototypes), "s_factories"),
        (typeof(DmPrototypes), "s_prototypes"),
        (typeof(DmBinder), "s_targets"),
        (typeof(DmBinder), "s_factories"),
        (typeof(DmVars), "s_cache"),
        // M5x slice 1: the interaction guard's answers per type and proc (DV27), computed from the types alone
        (typeof(InertSeam), "UndeclaredCache"),
        (typeof(InertSeam), "StubCache"),
        // silicons SI4: a mob type's command verbs, computed from the types alone
        (typeof(CommandVerbs), "s_byType"),
    };

    [Fact]
    public void NoTgOrShimClassHasAMutableStaticField()
    {
        var offenders = new List<string>();
        foreach (Assembly assembly in new[] { typeof(Dm).Assembly, typeof(TgState).Assembly })
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)) continue; // lambda caches
                foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.IsLiteral) continue;
                    string name = type.FullName + "." + field.Name;
                    if (!field.IsInitOnly)
                        offenders.Add(name + " is not readonly");
                    else if (IsMutableCollection(field.FieldType) && !AllowedCaches.Contains((type, field.Name)))
                        offenders.Add(name + " is a mutable " + field.FieldType.Name);
                }
            }
        }
        Assert.True(offenders.Count == 0, "mutable static state: " + string.Join("; ", offenders));
    }

    private static bool IsMutableCollection(Type type)
    {
        if (type.IsArray) return true;
        if (type == typeof(string)) return false;
        if (type.IsGenericType)
        {
            Type definition = type.GetGenericTypeDefinition();
            if (definition == typeof(ReadOnlyCollection<>) || definition == typeof(FrozenSet<>) || definition == typeof(FrozenDictionary<,>))
                return false;
            if (definition.Namespace == typeof(ImmutableArray).Namespace) return false;
        }
        return typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(IEnumerable);
    }
}
