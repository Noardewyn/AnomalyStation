using System.Globalization;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: M2 P8 (client play). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
//
// D107 and D70: the health doll is tg's own screen object, computed on the server by translated tg code and replicated
// privately as an appearance whose icon states are tg's own ("head3", "l_arm0", "chest6", "r_legDEAD"; code/_onclick/hud/
// screen_objects/screen_objects.dm, healthdoll/human/update_icon_state). The client adds, from that same state and nothing
// else, a text label per zone, a hatching per level and a colourblind palette; no number leaves the server for it.

/// <summary>What a doll zone's icon state says.</summary>
public enum ZoneKind : byte
{
    /// <summary>Damage level 0 (none) to 5 (worst): tg's <c>clamp(ceil(damage / max_damage * 5), 0, 5)</c>.</summary>
    Level,

    /// <summary>The limb is missing or a stump (tg's icon key 6).</summary>
    Missing,

    /// <summary>The limb is disabled (tg's icon key 7).</summary>
    Disabled,

    /// <summary>The owner is dead (tg's <c>DEAD</c> key).</summary>
    Dead,
}

/// <summary>One zone of the health doll as its icon state gives it.</summary>
/// <param name="Zone">tg's body zone: <c>head</c>, <c>chest</c>, <c>l_arm</c>, <c>r_arm</c>, <c>l_leg</c>, <c>r_leg</c>.</param>
/// <param name="Kind">What the state means.</param>
/// <param name="Level">The damage level for <see cref="ZoneKind.Level"/> (0 to 5), else 0.</param>
public readonly record struct ZoneState(string Zone, ZoneKind Kind, int Level);

/// <summary>The hatching drawn over a zone (D70's shape cue: the level is readable without colour).</summary>
public enum HatchStyle : byte
{
    None = 0,

    /// <summary>Level 1: sparse diagonal lines.</summary>
    Light = 1,

    /// <summary>Level 2: denser diagonal lines.</summary>
    Medium = 2,

    /// <summary>Level 3: a sparse cross-hatch.</summary>
    Cross = 3,

    /// <summary>Level 4: a dense cross-hatch.</summary>
    DenseCross = 4,

    /// <summary>Level 5: nearly solid.</summary>
    Solid = 5,

    /// <summary>Missing: dots.</summary>
    Dots = 6,

    /// <summary>Disabled: vertical bars.</summary>
    Bars = 7,

    /// <summary>Dead: horizontal stripes.</summary>
    Stripes = 8,
}

/// <summary>The zones of tg's health doll and what the client makes of their icon states.</summary>
public static class HealthZones
{
    /// <summary>tg's six body zones in the order the labels list them.</summary>
    public static readonly string[] Zones = { "head", "chest", "l_arm", "r_arm", "l_leg", "r_leg" };

    /// <summary>The worst damage level of a doll zone.</summary>
    public const int MaxLevel = 5;

    /// <summary>Reads a doll icon state (<c>"head3"</c>, <c>"r_legDEAD"</c>, <c>"chest6"</c>); false for any other state.</summary>
    public static bool TryParseState(string iconState, out ZoneState state)
    {
        state = default;
        if (string.IsNullOrEmpty(iconState))
            return false;
        foreach (string zone in Zones)
        {
            if (!iconState.StartsWith(zone, StringComparison.Ordinal))
                continue;
            string rest = iconState.Substring(zone.Length);
            if (rest == "DEAD")
            {
                state = new ZoneState(zone, ZoneKind.Dead, 0);
                return true;
            }
            if (rest.Length == 1 && char.IsAsciiDigit(rest[0]))
            {
                int key = rest[0] - '0';
                state = key <= MaxLevel ? new ZoneState(zone, ZoneKind.Level, key)
                    : key == 6 ? new ZoneState(zone, ZoneKind.Missing, 0)
                    : key == 7 ? new ZoneState(zone, ZoneKind.Disabled, 0)
                    : default;
                return key <= 7;
            }
            return false; // "head" followed by something else (tg has icon states like "headDEAD0"? none): not a doll state
        }
        return false;
    }

    /// <summary>Reads a health indicator state (<c>"health0"</c> to <c>"health7"</c>, tg's <c>healths</c>); the number, or -1.</summary>
    public static int TryParseIndicator(string iconState)
    {
        if (iconState != null && iconState.Length == 7 && iconState.StartsWith("health", StringComparison.Ordinal) && char.IsAsciiDigit(iconState[6]))
            return iconState[6] - '0';
        return -1;
    }

    /// <summary>The words of a zone for a label.</summary>
    public static string ZoneName(string zone) => zone switch
    {
        "head" => "Head",
        "chest" => "Chest",
        "l_arm" => "L arm",
        "r_arm" => "R arm",
        "l_leg" => "L leg",
        "r_leg" => "R leg",
        _ => zone,
    };

    /// <summary>The label of a zone: its name and its level (D70: a text label per zone with its level and name).</summary>
    public static string Label(ZoneState state)
    {
        string name = ZoneName(state.Zone);
        return state.Kind switch
        {
            ZoneKind.Level => string.Create(CultureInfo.InvariantCulture, $"{name} {state.Level}/{MaxLevel}"),
            ZoneKind.Missing => name + " missing",
            ZoneKind.Disabled => name + " disabled",
            _ => name + " dead",
        };
    }

    /// <summary>The hatching of a zone.</summary>
    public static HatchStyle Hatch(ZoneState state) => state.Kind switch
    {
        ZoneKind.Level => state.Level switch
        {
            0 => HatchStyle.None,
            1 => HatchStyle.Light,
            2 => HatchStyle.Medium,
            3 => HatchStyle.Cross,
            4 => HatchStyle.DenseCross,
            _ => HatchStyle.Solid,
        },
        ZoneKind.Missing => HatchStyle.Dots,
        ZoneKind.Disabled => HatchStyle.Bars,
        _ => HatchStyle.Stripes,
    };

    /// <summary>
    /// The colour a zone is drawn in under a colourblind palette; null for <see cref="ColorblindMode.Off"/> (tg's own sprite
    /// colours stay). The ramps are those of Okabe and Ito's colour-universal set, ordered so that each level differs from
    /// its neighbours in lightness as well as hue; the hatching carries the level on its own besides.
    /// </summary>
    public static Rgb? PaletteColor(ColorblindMode mode, ZoneState state)
    {
        if (mode == ColorblindMode.Off)
            return null;
        switch (state.Kind)
        {
            case ZoneKind.Missing: return new Rgb(0.45f, 0.45f, 0.45f);
            case ZoneKind.Disabled: return new Rgb(0.35f, 0.40f, 0.60f);
            case ZoneKind.Dead: return new Rgb(0.12f, 0.12f, 0.12f);
        }
        int level = Math.Clamp(state.Level, 0, MaxLevel);
        float t = level / (float)MaxLevel;
        switch (mode)
        {
            case ColorblindMode.RedGreen:
                return RedGreenRamp[level];
            case ColorblindMode.BlueYellow:
                return BlueYellowRamp[level];
            default:
                float grey = 0.88f - 0.74f * t;
                return new Rgb(grey, grey, grey);
        }
    }

    private static readonly Rgb[] RedGreenRamp =
    {
        new(0.00f, 0.45f, 0.70f), // blue
        new(0.34f, 0.71f, 0.91f), // sky blue
        new(0.94f, 0.89f, 0.26f), // yellow
        new(0.90f, 0.62f, 0.00f), // orange
        new(0.60f, 0.25f, 0.00f), // dark vermilion
        new(0.25f, 0.05f, 0.00f), // near black
    };

    private static readonly Rgb[] BlueYellowRamp =
    {
        new(0.00f, 0.60f, 0.53f), // teal
        new(0.40f, 0.76f, 0.65f), // light teal
        new(0.90f, 0.90f, 0.90f), // near white
        new(0.96f, 0.65f, 0.75f), // pink
        new(0.84f, 0.24f, 0.47f), // magenta
        new(0.51f, 0.00f, 0.24f), // dark red
    };
}

/// <summary>A zone state found in a screen entry's appearance, with the appearance that carries it (so the client can draw that one on its own).</summary>
/// <param name="Node">The appearance whose own icon state is the zone's.</param>
/// <param name="Zone">The state.</param>
public readonly record struct FoundZone(TableRef Node, ZoneState Zone);

/// <summary>Reads the health doll's zones and the health indicator out of the replicated screen entries (D107).</summary>
public static class HudReader
{
    private const int MaxDepth = 4;
    private const int MaxNodes = 256;

    /// <summary>
    /// Adds to <paramref name="zones"/> every doll zone state in <paramref name="root"/>'s appearance (its own icon state, its
    /// overlays and underlays, recursively), in the order met; returns the health indicator's number when one is found
    /// (tg's <c>health0</c> to <c>health7</c>), else -1.
    /// </summary>
    public static int Collect(ReplicaWorld replica, TableRef root, List<FoundZone> zones)
    {
        int indicator = -1;
        int visited = 0;
        Walk(replica, root, 0, zones, ref indicator, ref visited);
        return indicator;
    }

    private static void Walk(ReplicaWorld replica, TableRef node, int depth, List<FoundZone> zones, ref int indicator, ref int visited)
    {
        if (depth > MaxDepth || visited >= MaxNodes || node.IsNone || !replica.TryGetAppearance(node, out AppearanceEntry entry))
            return;
        visited++;
        string state = replica.ResolveString(entry.IconState);
        if (HealthZones.TryParseState(state, out ZoneState zone))
            zones.Add(new FoundZone(node, zone));
        else
        {
            int number = HealthZones.TryParseIndicator(state);
            if (number >= 0)
                indicator = number;
        }
        foreach (TableRef under in entry.Underlays)
            Walk(replica, under, depth + 1, zones, ref indicator, ref visited);
        foreach (TableRef over in entry.Overlays)
            Walk(replica, over, depth + 1, zones, ref indicator, ref visited);
    }
}
