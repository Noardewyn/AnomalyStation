using System.Globalization;
using System.Linq;

namespace Anomaly.Client.Shared.Editor;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>A key with the modifiers held with it. <see cref="Key"/> is the engine's code of the physical key (the interface names it).</summary>
public readonly record struct KeyCombo(int Key, bool Ctrl = false, bool Shift = false, bool Alt = false, bool Meta = false)
{
    /// <summary>The combo as the settings file keeps it: a letter for each modifier, then the key's number (<c>M+S+90</c>).</summary>
    public string ToText() => (Ctrl ? "C+" : string.Empty) + (Shift ? "S+" : string.Empty) + (Alt ? "A+" : string.Empty) + (Meta ? "M+" : string.Empty)
        + Key.ToString(CultureInfo.InvariantCulture);

    /// <summary>The combo of a text <see cref="ToText"/> wrote; null for any other text.</summary>
    public static KeyCombo? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        string[] parts = text.Trim().Split('+');
        if (!int.TryParse(parts[^1], NumberStyles.None, CultureInfo.InvariantCulture, out int key) || key <= 0)
            return null;
        var combo = new KeyCombo(key);
        foreach (string modifier in parts.Take(parts.Length - 1))
        {
            switch (modifier)
            {
                case "C": combo = combo with { Ctrl = true }; break;
                case "S": combo = combo with { Shift = true }; break;
                case "A": combo = combo with { Alt = true }; break;
                case "M": combo = combo with { Meta = true }; break;
                default: return null;
            }
        }
        return combo;
    }
}

/// <summary>
/// One function of the map editor that a key can start (docs/ai/specs/map-editor.md 5.16). <see cref="Held"/>: the function goes on while its key is
/// down (the camera's flight), so its key is taken without modifiers and is asked for, not looked up when it is pressed.
/// </summary>
public sealed record HotkeyAction(string Id, string Section, string Title, KeyCombo[] Defaults, bool Held = false);

/// <summary>
/// The map editor's keys: which key starts which function, as the mapper set them over the defaults. A key belongs to one function: given to
/// another, the first loses it. A held function's key is the view's while no command key is down, so it is the same key as that key pressed alone or
/// with Shift, and another than that key with Ctrl, Alt or the command key (S flies back and Ctrl + S saves).
/// </summary>
public sealed class HotkeyMap
{
    private readonly Dictionary<string, HotkeyAction> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<KeyCombo>> _keys = new(StringComparer.Ordinal);

    public HotkeyMap(IEnumerable<HotkeyAction> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        Actions = actions.ToList();
        foreach (HotkeyAction action in Actions)
        {
            _byId.Add(action.Id, action);
            _keys[action.Id] = action.Defaults.ToList();
        }
    }

    /// <summary>Every function, in the order the interface lists them.</summary>
    public IReadOnlyList<HotkeyAction> Actions { get; }

    /// <summary>The keys of a function (none: it has no key).</summary>
    public IReadOnlyList<KeyCombo> Of(string action) => _keys.TryGetValue(action, out List<KeyCombo> keys) ? keys : Array.Empty<KeyCombo>();

    /// <summary>The function a pressed key starts, or null. A held function is started by no press: its keys are asked for (<see cref="Of"/>).</summary>
    public string Find(KeyCombo pressed)
    {
        foreach (HotkeyAction action in Actions)
        {
            if (!action.Held && _keys[action.Id].Contains(pressed))
                return action.Id;
        }
        return null;
    }

    // Whether two functions' keys are one key.
    private static bool Same(HotkeyAction a, KeyCombo x, HotkeyAction b, KeyCombo y)
    {
        if (!a.Held && !b.Held)
            return x == y;
        KeyCombo other = a.Held ? y : x; // (of two held functions either: neither key has a modifier)
        return x.Key == y.Key && !(other.Ctrl || other.Alt || other.Meta);
    }

    /// <summary>
    /// Gives a function this key alone (null: no key). Returns the function that had the key and lost it, or null. A held function's key is taken
    /// without its modifiers.
    /// </summary>
    public string Set(string action, KeyCombo? key)
    {
        if (!_byId.TryGetValue(action, out HotkeyAction known))
            throw new ArgumentException("no such function: " + action, nameof(action));
        if (key is { } combo && known.Held)
            key = new KeyCombo(combo.Key);
        string lost = null;
        if (key is { } taken)
        {
            foreach (HotkeyAction other in Actions)
            {
                if (other.Id != action && _keys[other.Id].RemoveAll(k => Same(other, k, known, taken)) > 0)
                    lost = other.Id;
            }
        }
        _keys[action] = key is { } one ? new List<KeyCombo> { one } : new List<KeyCombo>();
        return lost;
    }

    /// <summary>True when a function has the keys it started with.</summary>
    public bool IsDefault(string action) => _byId.TryGetValue(action, out HotkeyAction known) && known.Defaults.SequenceEqual(_keys[action]);

    /// <summary>A function gets its own keys back; another that has one of them loses it. Returns the functions that lost a key.</summary>
    public List<string> Reset(string action)
    {
        var lost = new List<string>();
        if (!_byId.TryGetValue(action, out HotkeyAction known))
            return lost;
        foreach (KeyCombo key in known.Defaults)
        {
            foreach (HotkeyAction other in Actions)
            {
                if (other.Id != action && _keys[other.Id].RemoveAll(k => Same(other, k, known, key)) > 0)
                    lost.Add(other.Id);
            }
        }
        _keys[action] = known.Defaults.ToList();
        return lost;
    }

    /// <summary>Every function gets its own keys back.</summary>
    public void ResetAll()
    {
        foreach (HotkeyAction action in Actions)
            _keys[action.Id] = action.Defaults.ToList();
    }

    /// <summary>What the mapper changed, for the settings file: a function's id and its keys joined by a space (an empty text: no key).</summary>
    public Dictionary<string, string> Changed()
        => Actions.Where(a => !IsDefault(a.Id)).ToDictionary(a => a.Id, a => string.Join(" ", _keys[a.Id].Select(k => k.ToText())), StringComparer.Ordinal);

    /// <summary>
    /// Takes what <see cref="Changed"/> wrote, over the defaults. A function the file does not name keeps its own keys, but one of them that the
    /// file gives to another function; a text that is no key is passed over.
    /// </summary>
    public void Load(IReadOnlyDictionary<string, string> saved)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ResetAll();
        foreach ((string id, string text) in saved)
        {
            if (!_byId.TryGetValue(id, out HotkeyAction known))
                continue;
            var keys = (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(KeyCombo.Parse).Where(k => k != null)
                .Select(k => known.Held ? new KeyCombo(k.Value.Key) : k.Value).ToList();
            foreach (KeyCombo key in keys)
            {
                foreach (HotkeyAction other in Actions)
                {
                    if (other.Id != id)
                        _keys[other.Id].RemoveAll(k => Same(other, k, known, key));
                }
            }
            _keys[id] = keys;
        }
    }

    /// <summary>The pairs of functions that start with one key between them (the defaults' own check): none in a sound table.</summary>
    public List<(string First, string Second)> Clashes()
    {
        var clashes = new List<(string, string)>();
        for (int i = 0; i < Actions.Count; i++)
        {
            for (int j = i + 1; j < Actions.Count; j++)
            {
                HotkeyAction a = Actions[i], b = Actions[j];
                if (_keys[a.Id].Any(x => _keys[b.Id].Any(y => Same(a, x, b, y))))
                    clashes.Add((a.Id, b.Id));
            }
        }
        return clashes;
    }
}
