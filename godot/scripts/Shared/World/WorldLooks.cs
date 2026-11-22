using System.Linq;
using System.Text.Json;

namespace Anomaly.Client.Shared.World;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// The owner (2026-11-20): some things on the floor were drawn the wrong way (a pool of blood or guts standing up as a billboard, a cut-off leg
// too small); a config sets each type's look by hand (godot/assets/world_looks.json; the dev menu's Tools tab picks a thing and edits it).

/// <summary>How a tuned thing is drawn: as the rules decide, standing (turned to the eye), or flat on the floor.</summary>
public enum WorldLookMode : byte
{
    Auto,
    Billboard,
    Floor,
}

/// <summary>
/// A type's own look in the world: how it is drawn, its size (0 as drawn, else times the size the rules give it) and its move from where the
/// rules put it, metres: <paramref name="X"/> east, <paramref name="Y"/> up, <paramref name="Z"/> north.
/// </summary>
public readonly record struct WorldLook(WorldLookMode Mode = WorldLookMode.Auto, float Scale = 0f, float X = 0f, float Y = 0f, float Z = 0f)
{
    public bool IsNone => Mode == WorldLookMode.Auto && Size == 1f && X == 0f && Y == 0f && Z == 0f;

    /// <summary>The size factor, 1 as drawn.</summary>
    public float Size => Scale > 0f ? Scale : 1f;
}

/// <summary>
/// The things' looks by DM type, a subtype taking its nearest tuned ancestor's: <c>{ "/obj/effect/decal/cleanable/blood": { "mode": "floor" },
/// "/obj/item/bodypart/leg": { "scale": 1.3 } }</c>. Remembers what was last loaded or saved, so the dev menu can reset a type to it and tell what
/// is not saved yet; <see cref="Version"/> changes on every edit, so the renderers know to draw the things again.
/// </summary>
public sealed class WorldLooks
{
    private readonly Dictionary<string, WorldLook> _looks = new(StringComparer.Ordinal);
    private Dictionary<string, WorldLook> _saved = new(StringComparer.Ordinal);

    /// <summary>The looks the game draws with (the client loads them at its start; the tests set their own).</summary>
    public static WorldLooks Active { get; set; } = new();

    public int Count => _looks.Count;

    /// <summary>Changes on every <see cref="Set"/>.</summary>
    public int Version { get; private set; }

    /// <summary>Whether anything differs from what was last loaded or saved.</summary>
    public bool Unsaved => _looks.Count != _saved.Count || _looks.Any(p => !_saved.TryGetValue(p.Key, out WorldLook s) || s != p.Value);

    /// <summary>What is now set becomes what was saved (written to the file).</summary>
    public void MarkSaved() => _saved = new Dictionary<string, WorldLook>(_looks, StringComparer.Ordinal);

    /// <summary>A type's own look as last loaded or saved, none if it had none.</summary>
    public WorldLook Saved(string type) => _saved.GetValueOrDefault(type);

    /// <summary>Whether the type has a look of its own (not only an ancestor's).</summary>
    public bool HasOwn(string type) => type != null && _looks.ContainsKey(type);

    /// <summary>The type whose look <paramref name="type"/> takes: itself or its nearest tuned ancestor; null when none is tuned.</summary>
    public string Source(string type)
    {
        if (_looks.Count == 0)
            return null;
        for (string t = type; !string.IsNullOrEmpty(t); t = t[..Math.Max(0, t.LastIndexOf('/'))])
            if (_looks.ContainsKey(t))
                return t;
        return null;
    }

    /// <summary>The look of a type: its own, else its nearest tuned ancestor's, else none.</summary>
    public WorldLook Find(string type) => Source(type) is string s ? _looks[s] : default;

    /// <summary>Sets a type's own look (none: removed, so it takes its ancestor's again).</summary>
    public void Set(string type, WorldLook look)
    {
        if (look.IsNone)
            _looks.Remove(type);
        else
            _looks[type] = look;
        Version++;
    }

    /// <summary>The config's looks; a broken file is no looks (the things are drawn by the rules).</summary>
    public static WorldLooks Parse(string json)
    {
        var looks = new WorldLooks();
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                foreach (JsonProperty entry in doc.RootElement.EnumerateObject())
                {
                    if (entry.Value.ValueKind != JsonValueKind.Object)
                        continue;
                    float Number(string name) => entry.Value.TryGetProperty(name, out JsonElement v) && v.TryGetSingle(out float f) ? f : 0f;
                    WorldLookMode mode = entry.Value.TryGetProperty("mode", out JsonElement m) && m.ValueKind == JsonValueKind.String
                        && Enum.TryParse(m.GetString(), ignoreCase: true, out WorldLookMode parsed) ? parsed : WorldLookMode.Auto;
                    looks._looks[entry.Name] = new WorldLook(mode, Number("scale"), Number("x"), Number("y"), Number("z"));
                }
            }
            catch (JsonException)
            {
            }
        }
        looks.MarkSaved();
        return looks;
    }

    public string ToJson()
    {
        var root = new SortedDictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
        foreach ((string type, WorldLook l) in _looks)
        {
            var fields = new Dictionary<string, object>();
            if (l.Mode != WorldLookMode.Auto) fields["mode"] = l.Mode.ToString().ToLowerInvariant();
            if (l.Size != 1f) fields["scale"] = l.Size;
            if (l.X != 0f) fields["x"] = l.X;
            if (l.Y != 0f) fields["y"] = l.Y;
            if (l.Z != 0f) fields["z"] = l.Z;
            root[type] = fields;
        }
        return JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }
}
