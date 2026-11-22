using System.Linq;
using System.Text.Json;

namespace Anomaly.Client.Shared;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// D285 (the owner, 2026-10-28): many held things showed their picture turned the wrong way; a config turns each one by hand
// (godot/assets/held_items.json, the dev panel's held things section edits and saves it).

/// <summary>
/// A held thing's own turn, degrees in its own frame after it is posed: yaw about its up (180 shows its other side), pitch, roll in its plane; and its
/// own size (the owner, 2026-10-30: tg draws a pistol nearly as big as an SMG), 0 as drawn; and its own move from the kind's rest pose (the owner,
/// 2026-11-06), metres in the camera's space as HeldOffset's, the right hand's (the left's is mirrored).
/// </summary>
public readonly record struct HeldTurn(float Yaw, float Pitch, float Roll, float Scale = 0f, float X = 0f, float Y = 0f, float Z = 0f, float Recoil = 0f)
{
    public bool IsNone => Yaw == 0f && Pitch == 0f && Roll == 0f && Size == 1f && X == 0f && Y == 0f && Z == 0f && Kick == 1f;

    /// <summary>DV96: a gun's own recoil tuning, times the kick the server sends (1 as sent; the file's <c>recoil</c>).</summary>
    public float Kick => Recoil > 0f ? Recoil : 1f;

    /// <summary>The size to draw it at, 1 as drawn.</summary>
    public float Size => Scale > 0f ? Scale : 1f;
}

/// <summary>The tuning of every held thing (the owner, 2026-11-20): the view model's field of view, size and shift (metres, the right hand's).</summary>
public readonly record struct HeldGlobal(float Fov = HeldGlobal.DefaultFov, float Size = 1f, float X = 0f, float Y = 0f, float Z = 0f)
{
    public const float DefaultFov = 62f;

    public HeldGlobal() : this(DefaultFov) { }
}

/// <summary>
/// The held things' turns by tg picture: <c>{ "_all": { "fov": 62, "scale": 1 }, "icons/obj/tools.dmi:crowbar": { "yaw": 180, "scale": 0.8, "y": 0.05 } }</c>.
/// Remembers what was last loaded or saved, so the dev panel can reset a thing to it and tell what is not saved yet.
/// </summary>
public sealed class HeldTurns
{
    /// <summary>The entry of <see cref="All"/> in the file.</summary>
    public const string AllKey = "_all";

    private readonly Dictionary<string, HeldTurn> _turns = new(StringComparer.Ordinal);
    private Dictionary<string, HeldTurn> _saved = new(StringComparer.Ordinal);
    private HeldGlobal _savedAll = new();

    public int Count => _turns.Count;

    /// <summary>Every held thing's tuning.</summary>
    public HeldGlobal All { get; set; } = new();

    /// <summary>What was last loaded or saved of <see cref="All"/>.</summary>
    public HeldGlobal SavedAll => _savedAll;

    /// <summary>Whether anything differs from what was last loaded or saved.</summary>
    public bool Unsaved => All != _savedAll || _turns.Count != _saved.Count
        || _turns.Any(p => !_saved.TryGetValue(p.Key, out HeldTurn s) || s != p.Value);

    /// <summary>What is now set becomes what was saved (written to the file).</summary>
    public void MarkSaved()
    {
        _saved = new Dictionary<string, HeldTurn>(_turns, StringComparer.Ordinal);
        _savedAll = All;
    }

    /// <summary>A picture's own turn as last loaded or saved, none if it had none.</summary>
    public HeldTurn Saved(string icon, string state) => _saved.GetValueOrDefault(Key(icon, state));

    /// <summary>Whether the picture has a turn of its own (not only a look of the same thing's).</summary>
    public bool HasOwn(string icon, string state) => _turns.ContainsKey(Key(icon, state));

    public static string Key(string icon, string state) => icon + ":" + state;

    /// <summary>The config's turns; a broken file is no turns (the held things are drawn as they come).</summary>
    public static HeldTurns Parse(string json)
    {
        var turns = new HeldTurns();
        if (string.IsNullOrWhiteSpace(json))
            return turns;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            foreach (JsonProperty entry in doc.RootElement.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.Object)
                    continue;
                float Angle(string name, float none = 0f) => entry.Value.TryGetProperty(name, out JsonElement v) && v.TryGetSingle(out float f) ? f : none;
                if (entry.Name == AllKey)
                    turns.All = new HeldGlobal(Angle("fov", HeldGlobal.DefaultFov), Angle("scale", 1f), Angle("x"), Angle("y"), Angle("z"));
                else
                    turns._turns[entry.Name] = new HeldTurn(Angle("yaw"), Angle("pitch"), Angle("roll"), Angle("scale"), Angle("x"), Angle("y"), Angle("z"), Angle("recoil"));
            }
        }
        catch (JsonException)
        {
        }
        turns.MarkSaved();
        return turns;
    }

    /// <summary>The turn of a picture: its own, else one of a look of the same thing (<see cref="HeldHands.SameThing"/>: a welder lit or not).</summary>
    public HeldTurn Find(string icon, string state)
    {
        if (icon == null || string.IsNullOrEmpty(state))
            return default; // an empty state starts every state: it would take any thing's tuning
        if (_turns.TryGetValue(Key(icon, state), out HeldTurn turn))
            return turn;
        // the nearest look (the owner, 2026-11-20: the first match was any other thing whose state starts the same, so a thing took another's tuning)
        HeldTurn best = default;
        int bestGap = int.MaxValue;
        foreach ((string key, HeldTurn other) in _turns)
        {
            int colon = key.LastIndexOf(':');
            if (colon <= 0 || !HeldHands.SameThing(icon, state, key[..colon], key[(colon + 1)..]))
                continue;
            int gap = Math.Abs(key.Length - colon - 1 - state.Length);
            if (gap < bestGap)
                (best, bestGap) = (other, gap);
        }
        return best;
    }

    /// <summary>Sets a picture's turn (none: removed).</summary>
    public void Set(string icon, string state, HeldTurn turn)
    {
        if (turn.IsNone)
            _turns.Remove(Key(icon, state));
        else
            _turns[Key(icon, state)] = turn;
    }

    public string ToJson()
    {
        var root = new SortedDictionary<string, Dictionary<string, float>>(StringComparer.Ordinal);
        if (All != new HeldGlobal())
        {
            var all = new Dictionary<string, float> { ["fov"] = All.Fov, ["scale"] = All.Size };
            if (All.X != 0f) all["x"] = All.X;
            if (All.Y != 0f) all["y"] = All.Y;
            if (All.Z != 0f) all["z"] = All.Z;
            root[AllKey] = all;
        }
        foreach ((string key, HeldTurn t) in _turns)
        {
            var angles = new Dictionary<string, float>();
            if (t.Yaw != 0f) angles["yaw"] = t.Yaw;
            if (t.Pitch != 0f) angles["pitch"] = t.Pitch;
            if (t.Roll != 0f) angles["roll"] = t.Roll;
            if (t.Size != 1f) angles["scale"] = t.Size;
            if (t.X != 0f) angles["x"] = t.X;
            if (t.Y != 0f) angles["y"] = t.Y;
            if (t.Z != 0f) angles["z"] = t.Z;
            if (t.Kick != 1f) angles["recoil"] = t.Kick;
            root[key] = angles;
        }
        return JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }
}
