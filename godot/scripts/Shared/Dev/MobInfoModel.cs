namespace Anomaly.Client.Shared.Dev;

// The dev menu's player panel (the owner, 2026-10-13; D140), the engine-free half: the server's mobinfo answer (a dev_mob
// ServerText of "section<TAB>key<TAB>value" lines, the online players first) read into the panel's sections.

/// <summary>An online player of a mobinfo answer: the mob's world id and the player's key.</summary>
public readonly record struct MobInfoPlayer(long Id, string Key);

/// <summary>D346: a mob round the requester the dev menu can possess: its world id, name, tg type, player key (empty: none), distance in tiles.</summary>
public readonly record struct MobInfoNearby(long Id, string Name, string TypePath, string Player, int Distance, bool Dead)
{
    /// <summary>The dev command that possesses it (<c>force</c> takes it from its player).</summary>
    public string PossessCommand => "possess #" + Id.ToString(System.Globalization.CultureInfo.InvariantCulture) + (Player.Length > 0 ? " force" : "");
}

/// <summary>One name and value of a section.</summary>
public readonly record struct MobInfoRow(string Key, string Value);

/// <summary>A mobinfo answer.</summary>
public sealed class MobInfoModel
{
    /// <summary>The ServerText type of the answer (the server's <c>ServerHost.MobInfoTextType</c>).</summary>
    public const string TextType = "dev_mob";

    /// <summary>The online players, in the server's session order.</summary>
    public List<MobInfoPlayer> Players { get; } = new();

    /// <summary>D346: the mobs round the requester, nearest first.</summary>
    public List<MobInfoNearby> Nearby { get; } = new();

    /// <summary>D346: the body <c>unpossess</c> goes back to ("ghost": none, out as a ghost), or null when the requester possesses nothing.</summary>
    public string PossessReturn => Value("Possess", "return");

    /// <summary>The sections in the server's order, each with its rows.</summary>
    public List<(string Name, List<MobInfoRow> Rows)> Sections { get; } = new();

    /// <summary>Why there is no mob (an unknown WHO), or null.</summary>
    public string Error { get; private set; }

    /// <summary>The value of a row, or null.</summary>
    public string Value(string section, string key)
    {
        foreach ((string name, List<MobInfoRow> rows) in Sections)
        {
            if (name != section) continue;
            foreach (MobInfoRow row in rows)
            {
                if (row.Key == key) return row.Value;
            }
        }
        return null;
    }

    /// <summary>True for a ghost without a body: no numbers, only the ghost's commands (revive, the teleports).</summary>
    public bool IsBodilessGhost => Value("Ghost", "body") == "none";

    /// <summary>True when a dev switch row (godmode, fly, noclip) is on.</summary>
    public bool IsOn(string key) => Value("Dev", key) == "on";

    /// <summary>The section of the mob's status effects: each row's key is the effect's type path, its value the id and time left.</summary>
    public const string EffectsSection = "Status effects";

    /// <summary>The section that lists the status effect types live in the port (one row, the paths below /datum/status_effect/).</summary>
    public const string EffectTypesSection = "Effect types";

    /// <summary>The mob's status effects: (type path, "id: time left").</summary>
    public List<MobInfoRow> StatusEffects
    {
        get
        {
            foreach ((string name, List<MobInfoRow> rows) in Sections)
            {
                if (name == EffectsSection) return rows;
            }
            return new List<MobInfoRow>();
        }
    }

    /// <summary>The status effect types the server can apply (paths below /datum/status_effect/), in its order.</summary>
    public List<string> EffectTypes
    {
        get
        {
            string live = Value(EffectTypesSection, "live");
            return string.IsNullOrWhiteSpace(live) ? new List<string>() : new List<string>(live.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }
    }

    /// <summary>The dev command that applies a status effect type to WHO (N: seconds or stacks; 0 or less: tg's defaults).</summary>
    public static string AddEffectCommand(string who, string type, float number)
        => number > 0f
            ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"effect {who} add {type} {number:0.#}")
            : $"effect {who} add {type}";

    /// <summary>The dev command that removes a status effect (its type path) from WHO.</summary>
    public static string RemoveEffectCommand(string who, string typePath) => $"effect {who} remove {typePath}";

    /// <summary>Reads an answer's plain text (the ServerText's HTML decoded).</summary>
    public static MobInfoModel Parse(string text)
    {
        var model = new MobInfoModel();
        if (string.IsNullOrEmpty(text)) return model;
        foreach (string line in text.Split('\n'))
        {
            if (line.Length == 0) continue;
            string[] parts = line.Split('\t');
            if (parts.Length < 3) continue;
            string section = parts[0], key = parts[1], value = string.Join("\t", parts, 2, parts.Length - 2);
            switch (section)
            {
                case "players":
                    if (key.StartsWith('#') && long.TryParse(key.AsSpan(1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long id))
                        model.Players.Add(new MobInfoPlayer(id, value));
                    break;
                case "nearby":
                {
                    string[] f = value.Split('|');
                    if (f.Length == 5 && key.StartsWith('#') && long.TryParse(key.AsSpan(1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long mob)
                        && int.TryParse(f[3], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int distance))
                        model.Nearby.Add(new MobInfoNearby(mob, f[0], f[1], f[2], distance, f[4] == "dead"));
                    break;
                }
                case "error":
                    model.Error = value;
                    break;
                default:
                    List<MobInfoRow> rows = null;
                    foreach ((string name, List<MobInfoRow> existing) in model.Sections)
                    {
                        if (name == section) rows = existing;
                    }
                    if (rows == null)
                    {
                        rows = new List<MobInfoRow>();
                        model.Sections.Add((section, rows));
                    }
                    rows.Add(new MobInfoRow(key, value));
                    break;
            }
        }
        return model;
    }
}
