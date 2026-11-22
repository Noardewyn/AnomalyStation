// Engine ("BYOND builtin") part of tg's /datum (docs/ai/translation-rules.md 1.1; docs/ai/specs/object-model.md sections
// 1.1, 2, 3, 4 and 8). Not translated from a tg file: BYOND itself declares /datum, its builtin vars (type, tag,
// parent_type, vars) and its builtin procs (New, Del, Topic, ...). tg's own vars and procs of /datum are in the
// mirror files of the .dm files that declare them (datums/datum.cs, datums/signals.cs, ...).
using System.Reflection;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

// RULE-GAP: rules 2.3 put [DmPath] on the part in the earliest .dme file that declares the type; for a BYOND builtin
// type (/datum, /atom, /mob, ...) BYOND's own declaration precedes tgstation.dme, so the engine part carries it.
[DmPath("/datum")]
public partial class datum : IDmTagIndexed, IStateHashable
{
    // ---- identity (object-model.md section 3) ----

    // The world id; hashed by the registry itself (object-model.md section 8), so not again as a field.
    [NotHashed]
    private long _dm_id;

    /// <summary>The world id (<c>World.AllocateId()</c>); 0 for a prototype or an object not created by <c>Dm.@new</c>.</summary>
    public long dm_id => _dm_id;

    /// <summary>Called once by <c>Dm.@new</c>; a second call throws (object-model.md section 1.1).</summary>
    public void dm_assign_id(long id)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id), id, "World ids are positive.");
        if (_dm_id != 0) throw new InvalidOperationException("dm_assign_id: " + GetType().Name + " already has the id " + _dm_id + ".");
        _dm_id = id;
        if (!string.IsNullOrEmpty(_tag)) World.Current.Objects.SetTag(id, _tag); // a tag it was constructed with (M3 I1b)
    }

    /// <summary>
    /// The pre-New hook (object-model.md section 4, step 5): the object-creating var initializers of every class,
    /// ancestors first (<see cref="dm_init_vars"/>). <c>atom</c> adds the silent placement at its initial location.
    /// </summary>
    public virtual void dm_pre_new(DmArgs args)
    {
        dm_init_vars();
    }

    /// <summary>
    /// The object-creating and non-constant var defaults of this class (rules 10.2); every override calls
    /// <c>base.dm_init_vars()</c> first, as OpenDream's per-type init proc calls its parent's.
    /// </summary>
    protected internal virtual void dm_init_vars()
    {
    }

    // ---- builtin vars (object-model.md section 2; rules 10.7) ----

    /// <summary>DM <c>type</c>: the object's typepath (read-only).</summary>
    public Type type => GetType();

    // The value of tag; hashed (object-model.md section 8: builtin properties through their backing fields).
    private string _tag;

    /// <summary>
    /// DM <c>tag</c> (BYOND ref <c>/datum/var/tag</c>): <c>locate(tag)</c> finds the object, and a tagged object is
    /// held strongly by the world registry (a tag is an implicit reference in BYOND). An object that is not registered
    /// (<c>dm_id</c> 0) only stores the value (object-model.md section 2).
    /// </summary>
    // RULE-GAP: rules 10.7 call tag a field; object-model.md section 2 makes it a property with engine behaviour (the
    // strong hold); the spec that owns object identity is followed.
    public string tag
    {
        get => _tag;
        set
        {
            _tag = value;
            if (_dm_id != 0)
            {
                World.Current.Objects.HoldStrongly(_dm_id, !string.IsNullOrEmpty(value));
                World.Current.Objects.SetTag(_dm_id, value); // locate(tag)'s index (M3 I1b)
            }
        }
    }

    // ---- builtin procs ----

    /// <summary>
    /// BYOND's <c>/datum/New</c>: does nothing. The root of the <c>/datum</c> <c>New</c> slot
    /// (<c>docs/ai/specs/generated/slots.tsv</c>: <c>DmTask</c>, because some overrides sleep); <c>Dm.@new</c> calls it
    /// through the binder after the pre-New hook.
    /// </summary>
    public virtual DmTask New(params object[] __extra) => DmTask.CompletedTask;

    /// <summary>
    /// BYOND's builtin <c>/datum/Topic(href, href_list[])</c>: called by the client's default <c>Topic</c> for its <c>src</c>; does
    /// nothing. tg's <c>/datum/Topic</c> (datum.dm:105) is the slot's root and reaches this with <c>..()</c> (rules 7.5, 956); D154: a
    /// chat link reaches tg's through <c>ClientSeam.Topic</c>.
    /// </summary>
    public object Topic__d0(object href = null, DmList href_list = null) => null;

    // ---- state hashing (object-model.md section 8) ----

    /// <summary>The datum's simulation state, for <c>World.ComputeStateHash</c>: its DM type path, then its fields.</summary>
    public void HashState(ref StateHasher hasher) => DmFieldHasher.HashDatum(this, ref hasher);
}

/// <summary>
/// The object field hasher of docs/ai/specs/object-model.md section 8, used by <see cref="datum.HashState"/> and by the
/// <see cref="TgState"/> hash contributor. A datum hashes as its DM type path followed by every instance field (any
/// visibility) declared by a class that carries <c>[DmPath]</c>, in ordinal order of field name, except fields marked
/// <c>[NotHashed]</c>. Values: a number by its bits (-0 folded into +0), text by content, null by a marker, a reference
/// to a registered datum by its id, a reference to a deleted datum by its id and a "deleted" marker, a typepath by its
/// DM path, a list by its length and then every item with its associated value, a builtin value object (a DM class with
/// no world id: <c>sound</c>, <c>matrix</c>, <c>exception</c>, <c>/image</c>, ...) by its own fields in name order;
/// lists and value objects recursively, with a cycle guard. Hashing is a test and diagnostics operation: it reflects on
/// every call and keeps no cache (no mutable static state, object-model.md section 10).
/// </summary>
// SHIM-GAP: object-model.md section 8 calls this "the shim's field hasher"; the shim is not edited by the pilot, so it
// lives here, next to its only user, and reads the shim only through public members (DmTypes.PathOf, Dm.pairs).
internal static class DmFieldHasher
{
    // Kind markers, one before every value, so that values of different kinds never hash alike.
    private const ulong KindNull = 0x5353334400000001UL;
    private const ulong KindNumber = 0x5353334400000002UL;
    private const ulong KindText = 0x5353334400000003UL;
    private const ulong KindRef = 0x5353334400000004UL;
    private const ulong KindDeletedRef = 0x5353334400000005UL;
    private const ulong KindType = 0x5353334400000006UL;
    private const ulong KindList = 0x5353334400000007UL;
    private const ulong KindValue = 0x5353334400000008UL;
    private const ulong KindCycle = 0x5353334400000009UL;
    private const ulong KindOther = 0x535333440000000AUL;
    private const ulong KindResource = 0x535333440000000BUL;
    private const ulong KindAppearance = 0x535333440000000CUL;

    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>A datum's own state (the registry has already added its id).</summary>
    internal static void HashDatum(datum d, ref StateHasher hasher)
    {
        var visiting = new HashSet<object>(ReferenceEqualityComparer.Instance) { d };
        hasher.Add(DmTypes.PathOf(d.GetType()) ?? d.GetType().FullName);
        foreach (FieldInfo field in Fields(d.GetType(), dmClassesOnly: true))
        {
            hasher.Add(field.Name);
            HashValue(field.GetValue(d), ref hasher, visiting);
        }
    }

    /// <summary>
    /// The per-world state of <paramref name="state"/>: every field that is not at its default (null, zero, an empty
    /// list), as its name and its value, in ordinal order of name. A state with only defaults adds nothing, so creating
    /// <see cref="TgState"/> never changes the world's hash (World.AddStateHasher).
    /// </summary>
    internal static void HashFields(object state, ref StateHasher hasher)
    {
        var visiting = new HashSet<object>(ReferenceEqualityComparer.Instance) { state };
        foreach (FieldInfo field in Fields(state.GetType(), dmClassesOnly: false))
        {
            object value = field.GetValue(state);
            if (IsDefault(value)) continue;
            hasher.Add(field.Name);
            HashValue(value, ref hasher, visiting);
        }
    }

    private static bool IsDefault(object value) => value switch
    {
        null => true,
        DmList list => list.Count == 0,
        bool b => !b,
        _ => Dm.isnum(value) && Dm.num(value) == 0f,
    };

    private static void HashValue(object value, ref StateHasher hasher, HashSet<object> visiting)
    {
        switch (value)
        {
            case null:
                hasher.Add(KindNull);
                return;
            case string text:
                hasher.Add(KindText);
                hasher.Add(text);
                return;
            case DmResource resource:
                hasher.Add(KindResource);
                hasher.Add(resource.Path);
                return;
            case Type typepath:
                hasher.Add(KindType);
                hasher.Add(DmTypes.PathOf(typepath) ?? typepath.FullName);
                return;
            case DmList list:
                HashList(list, ref hasher, visiting);
                return;
            case DmAppearance appearance:
                // M2 integration (P3's SHIM-GAP): an overlay snapshot hashes by its vars, so an atom's overlays are world
                // state (the snapshot's own hash is .NET's randomized HashCode, not stable across runs).
                HashAppearance(appearance, ref hasher, visiting);
                return;
            case IDmObject obj when obj.dm_id != 0:
                // Every path of the port that removes a datum from the world registry (SSgarbage.Queue, HardDelete, the
                // QDEL_HINT_IWILLGC branch of qdel) sets gc_destroyed first, so a referenced datum is deleted exactly
                // when its gc_destroyed is set; reading it needs no ambient World (hashes are taken outside world.Enter).
                hasher.Add(obj is datum d && d.gc_destroyed != null ? KindDeletedRef : KindRef);
                hasher.Add(obj.dm_id);
                return;
        }
        if (Dm.isnum(value))
        {
            hasher.Add(KindNumber);
            hasher.Add(Dm.num(value));
            return;
        }
        Type type = value.GetType();
        string path = DmTypes.PathOf(type);
        if (path == null)
        {
            // Not a DM value (a host object): only its kind, never its content (a delegate holds native pointers).
            hasher.Add(KindOther);
            hasher.Add(type.FullName);
            return;
        }
        // A builtin value object, or a datum without a world id: its own fields, recursively.
        if (!visiting.Add(value))
        {
            hasher.Add(KindCycle);
            return;
        }
        hasher.Add(KindValue);
        hasher.Add(path);
        foreach (FieldInfo field in Fields(type, dmClassesOnly: value is datum))
        {
            hasher.Add(field.Name);
            HashValue(field.GetValue(value), ref hasher, visiting);
        }
        visiting.Remove(value);
    }

    private static void HashAppearance(DmAppearance a, ref StateHasher hasher, HashSet<object> visiting)
    {
        if (!visiting.Add(a))
        {
            hasher.Add(KindCycle);
            return;
        }
        hasher.Add(KindAppearance);
        HashValue(a.icon, ref hasher, visiting);
        HashValue(a.icon_state, ref hasher, visiting);
        hasher.Add(a.dir);
        hasher.Add(a.layer);
        hasher.Add(a.plane);
        hasher.Add(a.alpha);
        HashValue(a.color, ref hasher, visiting);
        hasher.Add(a.pixel_x);
        hasher.Add(a.pixel_y);
        hasher.Add(a.pixel_w);
        hasher.Add(a.pixel_z);
        HashValue(a.transform, ref hasher, visiting);
        hasher.Add(a.appearance_flags);
        hasher.Add(a.blend_mode);
        hasher.Add(a.invisibility);
        HashList(a.overlays, ref hasher, visiting);
        HashList(a.underlays, ref hasher, visiting);
        visiting.Remove(a);
    }

    private static void HashList(DmList list, ref StateHasher hasher, HashSet<object> visiting)
    {
        if (!visiting.Add(list))
        {
            hasher.Add(KindCycle);
            return;
        }
        hasher.Add(KindList);
        hasher.Add(list.Count);
        foreach ((object key, object value) in Dm.pairs(list))
        {
            HashValue(key, ref hasher, visiting);
            HashValue(value, ref hasher, visiting);
        }
        visiting.Remove(list);
    }

    // The instance fields of type and its base classes in ordinal order of name (the declaring class's depth breaks a
    // tie between a field and a hidden base field of the same name), without [NotHashed] fields; for a datum only the
    // fields of classes that carry [DmPath].
    private static List<FieldInfo> Fields(Type type, bool dmClassesOnly)
    {
        var fields = new List<(FieldInfo Field, int Depth)>();
        int depth = 0;
        for (Type level = type; level != null && level != typeof(object); level = level.BaseType, depth++)
        {
            if (dmClassesOnly && !level.IsDefined(typeof(DmPathAttribute), inherit: false)) continue;
            foreach (FieldInfo field in level.GetFields(InstanceFields))
            {
                if (field.IsDefined(typeof(NotHashedAttribute), inherit: false)) continue;
                fields.Add((field, depth));
            }
        }
        fields.Sort(static (a, b) =>
        {
            int byName = string.CompareOrdinal(a.Field.Name, b.Field.Name);
            return byName != 0 ? byName : a.Depth.CompareTo(b.Depth);
        });
        var result = new List<FieldInfo>(fields.Count);
        foreach ((FieldInfo field, int _) in fields) result.Add(field);
        return result;
    }
}
