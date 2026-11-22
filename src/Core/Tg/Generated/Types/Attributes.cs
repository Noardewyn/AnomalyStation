namespace Anomaly.Core.Tg;

// The markers of the generated type classes (D111, D112; docs/ai/specs/types-converter.md sections 3.6, 5 and 8), beside the
// generated parts that use them. Owner: M3 P1. The inert mark itself is Anomaly.Core.DmShim.DmInertAttribute.

/// <summary>
/// A class that is all generated (types-converter.md 8): no translator has written a part of it yet, so it holds tg's
/// var defaults and nothing else. The boot sites that create one object per type of a <c>typesof</c> list skip these
/// classes through <see cref="GlobalProcs.typesof_written"/> (DV25). Written by the types converter; not inherited.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class DmGeneratedOnlyAttribute : Attribute
{
}

/// <summary>
/// A method the types converter generated for a row of <c>docs/ai/specs/live-partial.txt</c> (types-converter.md 5). The
/// converter reads the rows, not these methods, when it decides the inert marks, so removing a row takes effect on the
/// next run.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class DmGeneratedStubAttribute : Attribute
{
}
