using System.Collections.Frozen;

namespace Anomaly.Core.DmShim;

// Owner: presentation. M6 slice 7 (D155): BYOND's /dm_filter, the value filter() makes, and the filters list of an atom
// or an image (BYOND ref "filters", "/proc/filter"; OpenDream DreamFilter.cs and DreamFilterList in DreamList.cs).

/// <summary>
/// BYOND <c>/dm_filter</c>: one graphical filter, a type and its named parameters (BYOND ref "filters"). Made by
/// <see cref="Dm.filter"/>; held by an atom's or an image's <c>filters</c>. The server draws nothing: the replicator sends
/// the filters the client draws (docs/ai/specs/protocol.md section 34). <c>animate()</c> of a filter assigns its vars
/// (<see cref="Get"/>, <see cref="Set"/>) as it assigns an atom's.
/// </summary>
[DmPath("/dm_filter")]
public sealed class dm_filter
{
    /// <summary>The filter types BYOND knows (OpenDream's <c>DreamFilter.FilterTypes</c>).</summary>
    public static readonly FrozenSet<string> Types = new HashSet<string>(StringComparer.Ordinal)
    {
        "alpha", "angular_blur", "bloom", "blur", "color", "displace", "drop_shadow", "layer", "motion_blur", "outline",
        "radial_blur", "rays", "ripple", "wave", "greyscale",
    }.ToFrozenSet(StringComparer.Ordinal);

    private readonly List<KeyValuePair<string, object>> _vars = new();
    private WeakReference<object> _owner;

    internal dm_filter(string type) => this.type = type;

    /// <summary>
    /// M6 slice 7b (D156): the atom or image whose filters list holds this filter last (<see cref="Dm.filter_list"/> with an owner), or
    /// null; <c>animate(filter, ...)</c> animates that owner's filter at this filter's position (protocol.md section 35.2).
    /// </summary>
    public object Owner => _owner != null && _owner.TryGetTarget(out object o) ? o : null;

    internal void SetOwner(object owner) => _owner = owner == null ? null : new WeakReference<object>(owner);

    /// <summary>Whether <paramref name="other"/> is the same filter by value: its type and every parameter (in any order) equal (<see cref="Dm.eq"/>).</summary>
    public bool SameAs(dm_filter other)
    {
        if (other == null || type != other.type || _vars.Count != other._vars.Count) return false;
        foreach (KeyValuePair<string, object> pair in _vars)
        {
            if (!other.Has(pair.Key)) return false;
            object theirs = other.Get(pair.Key);
            if (pair.Value is matrix m && theirs is matrix n)
            {
                if (m.a != n.a || m.b != n.b || m.c != n.c || m.d != n.d || m.e != n.e || m.f != n.f) return false;
                continue;
            }
            if (pair.Value is DmList la && theirs is DmList lb)
            {
                if (la.Count != lb.Count) return false;
                for (int i = 1; i <= la.Count; i++)
                    if (!Dm.eq(la[(float)i], lb[(float)i])) return false;
                continue;
            }
            if (!Dm.eq(pair.Value, theirs)) return false;
        }
        return true;
    }

    // A new filter of the same type and parameters (BYOND: an atom's filters are copies of the filters assigned).
    internal dm_filter Copy()
    {
        var copy = new dm_filter(type);
        foreach (KeyValuePair<string, object> pair in _vars)
            copy._vars.Add(pair.Value is matrix m ? new KeyValuePair<string, object>(pair.Key, m.Copy()) : pair);
        return copy;
    }

    /// <summary>BYOND <c>type</c>: the filter's kind (<c>"outline"</c>, <c>"alpha"</c>, ...).</summary>
    public string type { get; }

    /// <summary>BYOND <c>name</c>: the key <c>filters[name]</c> finds it by (tg's <c>add_filter</c> passes it), or null.</summary>
    public string name => Get("name") as string;

    /// <summary>The parameters as given, in order (the type excluded, the name included).</summary>
    public IReadOnlyList<KeyValuePair<string, object>> Vars => _vars;

    /// <summary>A parameter's value, null when it was not given (the drawer applies BYOND's default).</summary>
    public object Get(string key)
    {
        foreach (KeyValuePair<string, object> pair in _vars)
            if (pair.Key == key) return pair.Value;
        return null;
    }

    /// <summary>Whether the parameter was given.</summary>
    public bool Has(string key)
    {
        foreach (KeyValuePair<string, object> pair in _vars)
            if (pair.Key == key) return true;
        return false;
    }

    /// <summary>Sets a parameter (<c>animate(filter, size = 2)</c> and the like); <c>type</c> cannot change.</summary>
    public void Set(string key, object value)
    {
        if (key == "type") throw new DmRuntimeException("cannot change the type of a filter");
        for (int i = 0; i < _vars.Count; i++)
        {
            if (_vars[i].Key != key) continue;
            _vars[i] = new KeyValuePair<string, object>(key, value);
            return;
        }
        _vars.Add(new KeyValuePair<string, object>(key, value));
    }

    public override string ToString() => "/dm_filter";
}

public static partial class Dm
{
    /// <summary>
    /// DM <c>filter(type = ..., ...)</c> (BYOND ref <c>/proc/filter</c>): a new <see cref="dm_filter"/> from named arguments
    /// (<c>Dm.named</c>) or an <c>arglist(L)</c> of them (tg's <c>filter(arglist(arguments))</c>, datum.dm:358). The type must be
    /// one BYOND knows; a positional argument is a runtime error.
    /// </summary>
    public static dm_filter filter(params object[] args)
    {
        var named = new List<KeyValuePair<string, object>>();
        foreach (object arg in args ?? OneNull())
        {
            switch (arg)
            {
                case DmNamedArg n:
                    named.Add(new KeyValuePair<string, object>(n.Name, n.Value));
                    break;
                case DmArgList { List: DmList list }:
                    for (int i = 1; i <= list.Count; i++)
                    {
                        object key = list[(float)i];
                        if (key is not string text) throw new DmRuntimeException("filter(): the arguments must be named, got " + KindOf(key));
                        named.Add(new KeyValuePair<string, object>(text, list[key]));
                    }
                    break;
                case DmArgList:
                    break;
                default:
                    throw new DmRuntimeException("filter(): the arguments must be named, got " + KindOf(arg));
            }
        }
        string type = null;
        foreach (KeyValuePair<string, object> pair in named)
            if (pair.Key == "type") type = pair.Value as string;
        if (type == null || !dm_filter.Types.Contains(type))
            throw new DmRuntimeException("filter(): unknown filter type " + (type ?? "null"));
        var made = new dm_filter(type);
        foreach (KeyValuePair<string, object> pair in named)
            if (pair.Key != "type") made.Set(pair.Key, pair.Value);
        return made;
    }

    /// <summary>
    /// The value an atom's or an image's <c>filters</c> holds after an assignment (BYOND ref "filters", "filter proc"; OpenDream
    /// DreamObjectAtom's <c>filters</c>): null for none (null, an empty list), else a filters list (<see cref="DmList.IsFilters"/>:
    /// <c>filters[name]</c> finds a filter by its name and <c>filters -= name</c> removes it) of copies of the filters given: a filter, a
    /// list of filter parameters (<c>list("type" = ...)</c>), or a list of filters and parameter lists, in order (a parameter list of a
    /// type BYOND does not know and any other item are skipped). BYOND: "the atom gets a copy of this filter", so the filter that
    /// <c>filter()</c> made stays abstract and the atom's own is the one to animate.
    /// <para>
    /// With an <paramref name="owner"/> (the atom or image whose var is assigned; a snapshot copy passes none) every filter of the new
    /// list takes it as its <see cref="dm_filter.Owner"/> (M6 slice 7b, D156).
    /// </para>
    /// </summary>
    public static DmList filter_list(object value, object owner = null)
    {
        var made = new List<dm_filter>();
        switch (value)
        {
            case null:
                return null;
            case dm_filter one:
                made.Add(one.Copy());
                break;
            case DmList list when list.Count > 0 && list.TryGetAssociated("type", out object _):
                if (FilterOf(list) is dm_filter single)
                    made.Add(single);
                break;
            case DmList list:
                for (int i = 1; i <= list.Count; i++)
                {
                    switch (list[(float)i])
                    {
                        case dm_filter f:
                            made.Add(f.Copy());
                            break;
                        case DmList parameters when FilterOf(parameters) is dm_filter fromList:
                            made.Add(fromList);
                            break;
                    }
                }
                break;
            default:
                throw new DmRuntimeException("filters: " + KindOf(value) + " is not a filter or a list of filters");
        }
        if (made.Count == 0)
            return null;
        DmList result = DmList.NewFilters(made.ToArray());
        if (owner != null)
            foreach (dm_filter f in made)
                f.SetOwner(owner);
        return result;
    }

    // A filter of a parameter list (its text keys and their values), or null when its type is not one BYOND knows (OpenDream ignores it).
    private static dm_filter FilterOf(DmList parameters)
    {
        if (!parameters.TryGetAssociated("type", out object type) || type is not string kind || !dm_filter.Types.Contains(kind))
            return null;
        var made = new dm_filter(kind);
        for (int i = 1; i <= parameters.Count; i++)
        {
            if (parameters[(float)i] is not string key || key == "type") continue;
            made.Set(key, parameters[key]);
        }
        return made;
    }
}
