using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;

namespace Anomaly.Analyzers;

/// <summary>
/// The kernel and shim types the analyzer knows, found by full metadata name in one compilation. A compilation that
/// does not see <c>Anomaly.Core.Kernel.DmTask</c> has nothing to check.
/// </summary>
internal sealed class KernelSymbols
{
    public const string DmTaskName = "Anomaly.Core.Kernel.DmTask";
    public const string DmTaskOfTName = "Anomaly.Core.Kernel.DmTask`1";
    public const string IDmTaskName = "Anomaly.Core.Kernel.IDmTask";
    public const string IDmAwaiterName = "Anomaly.Core.Kernel.IDmAwaiter";
    public const string WaitingName = "Anomaly.Core.Kernel.Waiting";
    public const string SignalHandlerName = "Anomaly.Core.DmShim.SignalHandlerAttribute";
    public const string ShouldNotSleepName = "Anomaly.Core.DmShim.ShouldNotSleepAttribute";

    public readonly INamedTypeSymbol DmTask;
    public readonly INamedTypeSymbol DmTaskOfT;
    public readonly INamedTypeSymbol IDmTask;
    public readonly INamedTypeSymbol IDmAwaiter;
    public readonly INamedTypeSymbol Waiting;

    /// <summary>Null in a compilation that does not reference the shim (the kernel itself).</summary>
    public readonly INamedTypeSymbol SignalHandler;
    public readonly INamedTypeSymbol ShouldNotSleep;

    private readonly ConcurrentDictionary<IMethodSymbol, SyncMandate> _mandates = new(SymbolEqualityComparer.Default);

    private KernelSymbols(Compilation c)
    {
        DmTask = c.GetTypeByMetadataName(DmTaskName);
        DmTaskOfT = c.GetTypeByMetadataName(DmTaskOfTName);
        IDmTask = c.GetTypeByMetadataName(IDmTaskName);
        IDmAwaiter = c.GetTypeByMetadataName(IDmAwaiterName);
        Waiting = c.GetTypeByMetadataName(WaitingName);
        SignalHandler = c.GetTypeByMetadataName(SignalHandlerName);
        ShouldNotSleep = c.GetTypeByMetadataName(ShouldNotSleepName);
    }

    public static KernelSymbols TryCreate(Compilation compilation)
    {
        var k = new KernelSymbols(compilation);
        return k.DmTask != null && k.DmTaskOfT != null && k.IDmTask != null && k.IDmAwaiter != null ? k : null;
    }

    /// <summary><c>DmTask</c> or <c>DmTask&lt;T&gt;</c>: the return types of a coloured proc (the DmTask builders).</summary>
    public bool IsDmTaskReturn(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named) return false;
        return SymbolEqualityComparer.Default.Equals(named, DmTask)
            || SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, DmTaskOfT);
    }

    /// <summary>A task value: <c>DmTask</c>, <c>DmTask&lt;T&gt;</c>, <c>IDmTask</c>, or a nullable of one of them.</summary>
    public bool IsTask(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments.Length: 1 } nullable)
            type = nullable.TypeArguments[0];
        return IsTaskType(type);
    }

    /// <summary><c>DmTask</c>, <c>DmTask&lt;T&gt;</c> or <c>IDmTask</c> itself (not a nullable of one).</summary>
    public bool IsTaskType(ITypeSymbol type) =>
        IsDmTaskReturn(type) || SymbolEqualityComparer.Default.Equals(type, IDmTask);

    /// <summary>An awaiter (or any type) that implements <c>IDmAwaiter</c>.</summary>
    public bool IsKernelAwaiter(ITypeSymbol type)
    {
        if (type == null) return false;
        if (SymbolEqualityComparer.Default.Equals(type, IDmAwaiter)) return true;
        foreach (var i in type.AllInterfaces)
            if (SymbolEqualityComparer.Default.Equals(i, IDmAwaiter)) return true;
        return false;
    }

    /// <summary>A static member of <c>Anomaly.Core.Kernel.Waiting</c>.</summary>
    public bool IsWaitingMethod(IMethodSymbol method) =>
        method != null && Waiting != null && SymbolEqualityComparer.Default.Equals(method.ContainingType, Waiting);

    /// <summary>
    /// The <c>[SignalHandler]</c> or <c>[ShouldNotSleep]</c> that makes <paramref name="method"/> synchronous, its own or
    /// one on a method it overrides (the attributes are <c>Inherited = false</c> in metadata, tg's rule is "own or
    /// inherited", waiting-model.md 4.4); null when there is none.
    /// </summary>
    public SyncMandate FindSyncMandate(IMethodSymbol method)
    {
        if (method == null || (SignalHandler == null && ShouldNotSleep == null)) return null;
        method = method.OriginalDefinition;
        if (_mandates.TryGetValue(method, out var cached)) return cached;
        SyncMandate found = null;
        for (var m = method; m != null && found == null; m = m.OverriddenMethod)
        {
            foreach (var a in m.GetAttributes())
            {
                var cls = a.AttributeClass;
                if (cls == null) continue;
                if (SymbolEqualityComparer.Default.Equals(cls, SignalHandler) || SymbolEqualityComparer.Default.Equals(cls, ShouldNotSleep))
                {
                    found = new SyncMandate(cls.Name.Substring(0, cls.Name.Length - "Attribute".Length), m.OriginalDefinition);
                    break;
                }
            }
        }
        _mandates.TryAdd(method, found);
        return found;
    }
}

/// <summary>The attribute (without its "Attribute" suffix) that makes a method synchronous, and where it is declared.</summary>
internal sealed class SyncMandate
{
    public readonly string Attribute;
    public readonly IMethodSymbol DeclaredOn;

    public SyncMandate(string attribute, IMethodSymbol declaredOn)
    {
        Attribute = attribute;
        DeclaredOn = declaredOn;
    }
}
