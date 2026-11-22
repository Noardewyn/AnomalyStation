namespace Anomaly.Core.DmShim;

/// <summary>
/// What the shim needs from every DM object (<c>/datum</c> and its subtypes), implemented by tg's <c>datum</c> in
/// its engine part (docs/ai/specs/object-model.md section 1.1). The shim never references tg types directly.
/// </summary>
/// <summary>
/// A DM object whose <c>tag</c> setter keeps the registry's tag index (<c>ObjectRegistry.SetTag</c>, M3 I1b), so that
/// <c>locate(tag)</c> finds it without a scan (tg's <c>datum</c>). A world that registers any other object (the shim's own
/// test fixtures) falls back to the scan of the live objects.
/// </summary>
public interface IDmTagIndexed : IDmObject
{
}

public interface IDmObject
{
    /// <summary>
    /// The world id (<c>World.AllocateId()</c>): the object's identity for <c>REF()</c>, <c>locate(ref)</c>,
    /// replication and state hashing. 0 while the object is a prototype or not yet created by <c>Dm.@new</c>;
    /// setters with engine behaviour do not touch the World for such an object.
    /// </summary>
    long dm_id { get; }

    /// <summary>Called once by <c>Dm.@new</c> with the new id; a second call throws.</summary>
    void dm_assign_id(long id);

    /// <summary>
    /// The pre-New hook (object-model.md section 4, step 5): <c>datum</c> runs <c>dm_init_vars()</c> (the
    /// object-creating var initializers, ancestors first); <c>atom</c> then places itself silently at its
    /// initial location (the first positional argument, or the argument named <c>loc</c> when there are no
    /// positional arguments).
    /// </summary>
    void dm_pre_new(DmArgs args);
}

/// <summary>
/// What the shim needs from atoms (implemented by tg's <c>atom</c>): <c>contents</c> for <c>in</c>,
/// <c>for (... in atom)</c> and <c>locate(T) in atom</c>; <c>name</c> and <c>gender</c> for rendering an atom in
/// text (<c>\the</c>, articles, <c>\he</c>/<c>\his</c>, proper nouns). <c>name</c> and <c>gender</c> are tg
/// fields, so <c>atom</c> implements them explicitly (<c>string IDmAtom.name =&gt; name;</c>).
/// </summary>
public interface IDmAtom : IDmObject
{
    /// <summary>The engine-maintained contents list; never null.</summary>
    DmList contents { get; }

    /// <summary>What <c>for (x in atom)</c> walks: its contents, an area adding what stands on its turfs (BYOND ref /atom/var/contents).</summary>
    DmList dm_each_contents => contents;

    /// <summary>DM <c>name</c>.</summary>
    string name { get; }

    /// <summary>DM <c>gender</c> (<c>MALE</c>, <c>FEMALE</c>, <c>NEUTER</c>, <c>PLURAL</c>).</summary>
    string gender { get; }

    /// <summary>
    /// The atom's place in the world's order (<c>world.contents</c>, <c>for (x in world)</c>): 0 a mob, 1 another movable,
    /// 2 an area, 3 a turf. OpenDream <c>AtomManager.EnumerateAtoms</c>: "Mobs + Other Movables + Areas + Turfs"; tg's
    /// <c>SSatoms.InitializeAtoms</c> relies on it (objects are initialized before the turf whose <c>levelupdate</c> hides them).
    /// tg's <c>atom</c> implements it by its builtin type; another atom (a test fixture) counts as a movable.
    /// </summary>
    int dm_world_order => 1;
}

/// <summary>
/// The engine owner of a builtin list (<see cref="DmList.dm_owner"/>): an atom whose <c>contents</c>, <c>overlays</c>,
/// <c>underlays</c> or <c>vis_contents</c> DM code edits with list operators and procs. <see cref="ListChanged"/> runs
/// after every write to the list, possibly in the middle of a list operation that makes several writes (<c>Swap</c>,
/// <c>Splice</c>, <c>Add</c> of several items): it may only record that the list changed, never read the list's
/// items as final or write to the list.
/// </summary>
public interface IDmListOwner
{
    /// <summary>Called after a write to <paramref name="list"/>, whose <see cref="DmList.dm_owner"/> is this object.</summary>
    void ListChanged(DmList list);
}
