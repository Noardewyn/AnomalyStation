using System.Linq;
using System.Text.Json;

namespace Anomaly.Client.Shared;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// D285: a tg picture is drawn for a side view, so many held things would show it turned the wrong way; a config turns each one by hand
// (godot/assets/held_items.json, the dev panel's held things section edits and saves it).

/// <summary>
/// A held thing's own turn, degrees in its own frame after it is posed: yaw about its up (180 shows its other side), pitch, roll in its plane; and its
/// own size (tg draws a pistol nearly as big as an SMG), 0 as drawn; and its own move from the kind's rest pose, metres in the camera's space as
/// HeldOffset's, the right hand's (the left's is mirrored). Its picture can be mirrored before it is made solid (<paramref name="FlipX"/> left to
/// right, <paramref name="FlipY"/> top to bottom), which turns a thing drawn the other way round in one step; and a wielded thing can have its own
/// move from the rest pose (<paramref name="Wield"/>) and its own flips while wielded (<paramref name="WieldFlip"/>) in place of the common ones
/// (HeldTurns.Wield, HeldTurns.WieldFlip). Its melee hit can play a clip chosen for it (HeldClip.Names), one-handed (<paramref name="Attack"/>) and
/// wielded (<paramref name="WieldAttack"/>), over the defaults (HeldTurns.ClipOf); null for the default.
/// </summary>
public readonly record struct HeldTurn(float Yaw, float Pitch, float Roll, float Scale = 0f, float X = 0f, float Y = 0f, float Z = 0f, float Recoil = 0f,
    bool FlipX = false, bool FlipY = false, HeldOffset? Wield = null, HeldFlip WieldFlip = default, string Attack = null, string WieldAttack = null)
{
    public bool IsNone => Yaw == 0f && Pitch == 0f && Roll == 0f && Size == 1f && X == 0f && Y == 0f && Z == 0f && Kick == 1f && !FlipX && !FlipY
        && Wield == null && WieldFlip.IsNone && Attack == null && WieldAttack == null;

    /// <summary>Its own flips.</summary>
    public HeldFlip Flip => new(FlipX, FlipY);

    /// <summary>DV96: a gun's own recoil tuning, times the kick the server sends (1 as sent; the file's <c>recoil</c>).</summary>
    public float Kick => Recoil > 0f ? Recoil : 1f;

    /// <summary>The size to draw it at, 1 as drawn.</summary>
    public float Size => Scale > 0f ? Scale : 1f;
}

/// <summary>A picture mirrored left to right (<paramref name="X"/>) and/or top to bottom (<paramref name="Y"/>) before it is made solid.</summary>
public readonly record struct HeldFlip(bool X = false, bool Y = false)
{
    public bool IsNone => !X && !Y;

    /// <summary>Two flips one after the other: a flip of the same way twice undoes it.</summary>
    public static HeldFlip operator ^(HeldFlip a, HeldFlip b) => new(a.X ^ b.X, a.Y ^ b.Y);
}

/// <summary>
/// The tuning of every held thing: the view model's field of view, size and shift (metres, the right hand's), a flip of every picture, and the clip
/// every thing's one-handed melee hit plays (HeldClip.Names; null for tg's rule, HeldClip.For).
/// </summary>
public readonly record struct HeldGlobal(float Fov = HeldGlobal.DefaultFov, float Size = 1f, float X = 0f, float Y = 0f, float Z = 0f, HeldFlip Flip = default,
    string Attack = null)
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

    /// <summary>The entries of <see cref="Wield"/> and <see cref="WieldGun"/> in the file.</summary>
    public const string WieldKey = "_wield", WieldGunKey = "_wield_gun";

    private readonly Dictionary<string, HeldTurn> _turns = new(StringComparer.Ordinal);
    private Dictionary<string, HeldTurn> _saved = new(StringComparer.Ordinal);
    private HeldGlobal _savedAll = new();
    private (HeldOffset Melee, HeldOffset Gun) _savedWield = (HeldPose.WieldMelee, HeldPose.WieldGun);
    private (HeldFlip Melee, HeldFlip Gun) _savedWieldFlip;
    private string _savedWieldAttack;

    public int Count => _turns.Count;

    /// <summary>Every held thing's tuning.</summary>
    public HeldGlobal All { get; set; } = new();

    /// <summary>What was last loaded or saved of <see cref="All"/>.</summary>
    public HeldGlobal SavedAll => _savedAll;

    /// <summary>A wielded melee thing's move from its rest pose (the right hand's, HeldPose.Rest).</summary>
    public HeldOffset Wield { get; set; } = HeldPose.WieldMelee;

    /// <summary>A wielded gun's move from its rest pose.</summary>
    public HeldOffset WieldGun { get; set; } = HeldPose.WieldGun;

    /// <summary>What was last loaded or saved of <see cref="Wield"/> and <see cref="WieldGun"/>.</summary>
    public (HeldOffset Melee, HeldOffset Gun) SavedWield => _savedWield;

    /// <summary>A flip of every wielded melee thing's picture, and of every wielded gun's.</summary>
    public HeldFlip WieldFlip { get; set; }

    public HeldFlip WieldGunFlip { get; set; }

    /// <summary>What was last loaded or saved of <see cref="WieldFlip"/> and <see cref="WieldGunFlip"/>.</summary>
    public (HeldFlip Melee, HeldFlip Gun) SavedWieldFlip => _savedWieldFlip;

    /// <summary>The clip every wielded melee thing's hit plays (HeldClip.Names); null for tg's rule.</summary>
    public string WieldAttack { get; set; }

    /// <summary>What was last loaded or saved of <see cref="WieldAttack"/>.</summary>
    public string SavedWieldAttack => _savedWieldAttack;

    /// <summary>
    /// The clip a melee hit with a picture plays: the thing's own choice, else the default (every wielded melee thing's while wielded, every thing's
    /// otherwise), else tg's rule (HeldClip.For: a pointed thing stabs).
    /// </summary>
    public string ClipOf(string icon, string state, bool wielded, bool pointy)
    {
        HeldTurn t = Find(icon, state);
        return (wielded ? t.WieldAttack ?? WieldAttack : t.Attack ?? All.Attack) ?? HeldClip.For(pointy, wielded);
    }

    /// <summary>
    /// How a picture is flipped: every held thing's flip, then its own, then while wielded its own wielded flip when it has its own wielded pose,
    /// else the kind's common wielded flip. Flips add up: a thing flipped the same way as all is drawn unflipped.
    /// </summary>
    public HeldFlip FlipOf(string icon, string state, HeldKind kind, bool wielded)
    {
        HeldTurn t = Find(icon, state);
        HeldFlip flip = All.Flip ^ t.Flip;
        if (wielded)
            flip ^= t.Wield != null ? t.WieldFlip : kind == HeldKind.Gun ? WieldGunFlip : WieldFlip;
        return flip;
    }

    /// <summary>The wielded move of a kind.</summary>
    public HeldOffset WieldOf(HeldKind kind) => kind == HeldKind.Gun ? WieldGun : Wield;

    /// <summary>Whether anything differs from what was last loaded or saved.</summary>
    public bool Unsaved => All != _savedAll || (Wield, WieldGun) != _savedWield || (WieldFlip, WieldGunFlip) != _savedWieldFlip || WieldAttack != _savedWieldAttack || _turns.Count != _saved.Count
        || _turns.Any(p => !_saved.TryGetValue(p.Key, out HeldTurn s) || s != p.Value);

    /// <summary>What is now set becomes what was saved (written to the file).</summary>
    public void MarkSaved()
    {
        _saved = new Dictionary<string, HeldTurn>(_turns, StringComparer.Ordinal);
        _savedAll = All;
        _savedWield = (Wield, WieldGun);
        _savedWieldFlip = (WieldFlip, WieldGunFlip);
        _savedWieldAttack = WieldAttack;
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
                HeldFlip Flips(string prefix = "") => new(Angle(prefix + "flip_x") != 0f, Angle(prefix + "flip_y") != 0f);
                // a clip by name; an unknown one is none (the default plays)
                string Clip(string name) => entry.Value.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String
                    && HeldClip.Names.Contains(v.GetString()) ? v.GetString() : null;
                if (entry.Name == AllKey)
                    turns.All = new HeldGlobal(Angle("fov", HeldGlobal.DefaultFov), Angle("scale", 1f), Angle("x"), Angle("y"), Angle("z"), Flips(), Clip("attack"));
                else if (entry.Name is WieldKey or WieldGunKey)
                {
                    var move = new HeldOffset(Angle("x"), Angle("y"), Angle("z"), Angle("pitch"), Angle("yaw"), Angle("roll"));
                    if (entry.Name == WieldKey)
                        (turns.Wield, turns.WieldFlip, turns.WieldAttack) = (move, Flips(), Clip("attack"));
                    else
                        (turns.WieldGun, turns.WieldGunFlip) = (move, Flips());
                }
                else
                {
                    HeldOffset? wield = WieldNames.Any(n => entry.Value.TryGetProperty(n, out _))
                        ? new HeldOffset(Angle("wield_x"), Angle("wield_y"), Angle("wield_z"), Angle("wield_pitch"), Angle("wield_yaw"), Angle("wield_roll"))
                        : null;
                    turns._turns[entry.Name] = new HeldTurn(Angle("yaw"), Angle("pitch"), Angle("roll"), Angle("scale"), Angle("x"), Angle("y"), Angle("z"),
                        Angle("recoil"), Angle("flip_x") != 0f, Angle("flip_y") != 0f, wield, Flips("wield_"), Clip("attack"), Clip("wield_attack"));
                }
            }
        }
        catch (JsonException)
        {
        }
        turns.MarkSaved();
        return turns;
    }

    private static readonly string[] WieldNames = { "wield_x", "wield_y", "wield_z", "wield_pitch", "wield_yaw", "wield_roll" };

    /// <summary>The wielded move of a picture: its own (<see cref="HeldTurn.Wield"/>), else the kind's common one.</summary>
    public HeldOffset WieldOf(string icon, string state, HeldKind kind) => Find(icon, state).Wield ?? WieldOf(kind);

    /// <summary>The turn of a picture: its own, else one of a look of the same thing (<see cref="HeldHands.SameThing"/>: a welder lit or not).</summary>
    public HeldTurn Find(string icon, string state)
    {
        if (icon == null || string.IsNullOrEmpty(state))
            return default; // an empty state starts every state: it would take any thing's tuning
        if (_turns.TryGetValue(Key(icon, state), out HeldTurn turn))
            return turn;
        // the nearest look: any other thing whose state merely starts the same must not lend it its tuning
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
        // numbers, and a chosen clip's name
        var root = new SortedDictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
        if (All != new HeldGlobal())
        {
            var all = new Dictionary<string, object> { ["fov"] = All.Fov, ["scale"] = All.Size };
            if (All.X != 0f) all["x"] = All.X;
            if (All.Y != 0f) all["y"] = All.Y;
            if (All.Z != 0f) all["z"] = All.Z;
            AddFlips(all, All.Flip);
            AddClip(all, "attack", All.Attack);
            root[AllKey] = all;
        }
        if (Wield != HeldPose.WieldMelee || !WieldFlip.IsNone || WieldAttack != null)
            root[WieldKey] = AddClip(AddFlips(OffsetJson(Wield), WieldFlip), "attack", WieldAttack);
        if (WieldGun != HeldPose.WieldGun || !WieldGunFlip.IsNone)
            root[WieldGunKey] = AddFlips(OffsetJson(WieldGun), WieldGunFlip);
        foreach ((string key, HeldTurn t) in _turns)
        {
            var angles = new Dictionary<string, object>();
            if (t.Yaw != 0f) angles["yaw"] = t.Yaw;
            if (t.Pitch != 0f) angles["pitch"] = t.Pitch;
            if (t.Roll != 0f) angles["roll"] = t.Roll;
            if (t.Size != 1f) angles["scale"] = t.Size;
            if (t.X != 0f) angles["x"] = t.X;
            if (t.Y != 0f) angles["y"] = t.Y;
            if (t.Z != 0f) angles["z"] = t.Z;
            if (t.Kick != 1f) angles["recoil"] = t.Kick;
            AddFlips(angles, t.Flip);
            if (t.Wield is HeldOffset w)
            {
                foreach ((string name, object value) in OffsetJson(w))
                    angles["wield_" + name] = value;
            }
            AddFlips(angles, t.WieldFlip, "wield_");
            AddClip(angles, "attack", t.Attack);
            AddClip(angles, "wield_attack", t.WieldAttack);
            root[key] = angles;
        }
        return JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }

    private static Dictionary<string, object> AddClip(Dictionary<string, object> entry, string name, string clip)
    {
        if (clip != null) entry[name] = clip;
        return entry;
    }

    private static Dictionary<string, object> AddFlips(Dictionary<string, object> entry, HeldFlip flip, string prefix = "")
    {
        if (flip.X) entry[prefix + "flip_x"] = 1f;
        if (flip.Y) entry[prefix + "flip_y"] = 1f;
        return entry;
    }

    private static Dictionary<string, object> OffsetJson(HeldOffset o) => new()
    {
        ["x"] = o.X, ["y"] = o.Y, ["z"] = o.Z, ["pitch"] = o.Pitch, ["yaw"] = o.Yaw, ["roll"] = o.Roll,
    };
}
