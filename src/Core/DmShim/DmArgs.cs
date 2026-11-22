namespace Anomaly.Core.DmShim;

// Owner: dynamic. The argument carriers of calls by name (docs/ai/translation-rules.md 7.3; object-model.md
// sections 4 and 12). Contracts wrote the plain data holders; the binder that fills them is DmBinder.

/// <summary>A named argument of a dynamic call, <c>Dm.@new</c> or a variadic macro: <c>Dm.named("name", value)</c> (rules 7.3).</summary>
public sealed class DmNamedArg
{
    public DmNamedArg(string name, object value)
    {
        Name = name;
        Value = value;
    }

    /// <summary>The DM parameter name.</summary>
    public string Name { get; }

    /// <summary>The argument value.</summary>
    public object Value { get; }
}

/// <summary>
/// DM <c>arglist(L)</c> (BYOND ref <c>/proc/arglist</c>): the whole argument list of a call given as a list:
/// <c>Dm.arglist(L)</c>. When bound, items are positional arguments in list order, and a text item that the list holds
/// as a key (with an associated value, null included: OpenDream <c>DMProc.ToProcArguments</c> tests
/// <c>ContainsKey</c>) is a named argument (rules 7.3). A non-list (null included) gives no arguments (OpenDream; tg's
/// <c>/datum/callback/Invoke</c> passes <c>arglist(null)</c> for a callback without arguments); a non-text key is a
/// runtime error.
/// </summary>
public sealed class DmArgList
{
    public DmArgList(object list) => List = list;

    /// <summary>The list given to <c>arglist</c> (a non-list gives no arguments).</summary>
    public object List { get; }
}

/// <summary>
/// The bound arguments of a <c>new</c>, as the pre-New hook sees them (<see cref="IDmObject.dm_pre_new"/>,
/// object-model.md section 4): positional arguments in order and named arguments, with any <c>arglist</c> already
/// expanded. An atom's initial location is <c>Positional[0]</c>, or the argument named "loc" when there are no
/// positional arguments.
/// </summary>
public sealed class DmArgs
{
    private readonly object[] _positional;
    private readonly KeyValuePair<string, object>[] _named;

    /// <summary>No arguments.</summary>
    public static readonly DmArgs Empty = new DmArgs(null, null);

    public DmArgs(object[] positional, KeyValuePair<string, object>[] named)
    {
        _positional = positional ?? Array.Empty<object>();
        _named = named ?? Array.Empty<KeyValuePair<string, object>>();
    }

    /// <summary>The positional arguments in order.</summary>
    public IReadOnlyList<object> Positional => _positional;

    /// <summary>The named arguments in call order.</summary>
    public IReadOnlyList<KeyValuePair<string, object>> Named => _named;

    /// <summary>
    /// An atom's initial location (BYOND ref "New proc (atom)"): the first positional argument, or the argument named
    /// <c>loc</c> when there are no positional arguments, whatever the <c>New</c> definition calls its first
    /// parameter; null when there is neither. The pre-New hook of <c>atom</c> places the atom there.
    /// </summary>
    public object InitialLocation
    {
        get
        {
            if (_positional.Length > 0) return _positional[0];
            return TryGetNamed("loc", out object loc) ? loc : null;
        }
    }

    /// <summary>The value of the named argument <paramref name="name"/> (DM names are case-sensitive; the last one wins).</summary>
    public bool TryGetNamed(string name, out object value)
    {
        for (int i = _named.Length - 1; i >= 0; i--)
        {
            if (string.Equals(_named[i].Key, name, StringComparison.Ordinal))
            {
                value = _named[i].Value;
                return true;
            }
        }
        value = null;
        return false;
    }
}
