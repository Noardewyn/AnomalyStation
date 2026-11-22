namespace Anomaly.Core.DmShim;

// Owner: M3 P1 (docs/ai/specs/m3-ownership.md: the one DmShim file a package adds). D112, docs/ai/specs/types-converter.md 4.3.

/// <summary>
/// An inert DM type (D112, types-converter.md section 4): some tg proc defined at the type, or at an ancestor below its
/// nearest live ancestor, is not declared by the type's class. A map object of an inert type is a real instance of its
/// class, but <c>SSatoms.InitAtom</c> initializes it through <c>InertSeam</c> and <c>dm_inert_initialize</c> instead of
/// its <c>Initialize</c> (types-converter.md 7). Written by the types converter (<c>tools/Anomaly.Tools.Types</c>) on the
/// generated part of every inert type and on no other class; never written or removed by hand. Not inherited: every inert
/// class carries its own mark, so the runtime type alone answers.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class DmInertAttribute : Attribute
{
}
