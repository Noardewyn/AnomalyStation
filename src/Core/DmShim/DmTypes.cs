namespace Anomaly.Core.DmShim;

/// <summary>
/// The DM type registry (docs/ai/specs/object-model.md section 9): built once per process, lazily and thread-safely,
/// by reflection from the <see cref="DmPathAttribute"/>s of the shim and of every assembly that references it (the
/// Tg assembly, a host, tests), then immutable. Maps DM path and <c>Type</c> both ways, knows each type's DM parent
/// and its ordinal in DM object-tree order, and serves <c>typesof</c>, tg's <c>subtypesof</c> and
/// <c>typecacheof</c>, <c>ispath</c>, <c>istype</c> with a <c>Type</c>, <c>text2path</c>, <c>"[type]"</c> and
/// <c>parent_type</c>.
/// <para>
/// Order: depth-first preorder of the type tree, children in compile order, read from the generated
/// <c>Anomaly.Core.Tg.TypeOrder</c> (<c>src/Core/Tg/Generated/TypeOrder.g.cs</c>), which the Tg assembly names with
/// <c>[assembly: DmTypeOrder]</c> (<see cref="DmTypeOrderAttribute"/>); a type the generated list does not name
/// sorts after every listed one in a path-ordered preorder (object-model.md section 9): a preorder of the tree with
/// siblings by ordinal path, the same as ordinal path order wherever a path extends its parent's (<c>'/'</c> sorts
/// before every identifier character), and still a preorder for a type that <c>parent_type</c> places elsewhere (tg's
/// <c>/client</c> under <c>/datum</c>).
/// </para>
/// <para>
/// Parents: C# inheritance is DM inheritance (rules 2.3), so a class's DM parent is its nearest registered C# base
/// class. A top-level class with no registered base whose path is not one of BYOND's roots (<c>/datum</c>,
/// <c>/world</c>, <c>/list</c>, <c>/alist</c>, <c>/savefile</c>, <c>/callee</c>, <c>/vector</c>) has <c>/datum</c> as
/// its parent once a class claims <c>/datum</c> (BYOND ref <c>/datum/var/parent_type</c>): that places the shim's
/// <c>/sound</c>, <c>/exception</c> and <c>/matrix</c>, which cannot derive from tg's <c>datum</c> in C#, where DM
/// has them (<c>ispath(/sound, /datum)</c> is true, while <c>x is datum</c> stays false for a sound).
/// </para>
/// Owner: dynamic (the frozen surface is <see cref="PathOf"/>, <see cref="FromPath"/>, <see cref="ParentOf"/>,
/// <see cref="Subtypes"/>, <see cref="TreeOrdinal"/>; <see cref="IsSubtypeOrSame"/> is dynamic's addition).
/// </summary>
public static class DmTypes
{
    private static readonly Lazy<DmTypeRegistry> s_registry = new(DmTypeRegistry.Discover);

    /// <summary>The process-wide registry (built on first use).</summary>
    internal static DmTypeRegistry Registry => s_registry.Value;

    /// <summary>The DM path of a type (<c>"/obj/item"</c>), or null for a type without <see cref="DmPathAttribute"/>.</summary>
    public static string PathOf(Type type) => Registry.PathOf(type);

    /// <summary>The type with the DM path <paramref name="path"/> (exact, case-sensitive), or null.</summary>
    public static Type FromPath(string path) => Registry.FromPath(path);

    /// <summary>
    /// The DM parent of a type (null for <c>/datum</c> and the other roots): its nearest registered C# base class,
    /// or <c>/datum</c> for a top-level builtin such as <c>/sound</c> (see the class remarks).
    /// </summary>
    public static Type ParentOf(Type type) => Registry.ParentOf(type);

    /// <summary>
    /// <paramref name="root"/>'s subtree in DM type-tree order, <paramref name="root"/> first when
    /// <paramref name="includeRoot"/> (<c>typesof</c>) or without it (<c>subtypesof</c>). Read-only and cached.
    /// </summary>
    public static IReadOnlyList<Type> Subtypes(Type root, bool includeRoot) => Registry.Subtypes(root, includeRoot);

    /// <summary>The position of a type in DM type-tree order (object-model.md section 9); -1 for an unregistered type.</summary>
    public static int TreeOrdinal(Type type) => Registry.TreeOrdinal(type);

    /// <summary>
    /// Whether <paramref name="type"/> is <paramref name="ancestor"/> or derives from it in the DM type tree
    /// (<c>ispath(type, ancestor)</c>). C# derivation always counts.
    /// </summary>
    public static bool IsSubtypeOrSame(Type type, Type ancestor) => Registry.IsSubtypeOrSame(type, ancestor);
}
