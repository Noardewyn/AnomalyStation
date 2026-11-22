using System.Globalization;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

/// <summary>What a map's sky is (docs/ai/specs/map-editor.md 5.10).</summary>
public enum MapSkyKind : byte
{
    /// <summary>The game's own: space, with its stars, the planet under the station, its moon and the rocks.</summary>
    Space = 0,

    /// <summary>The stars alone.</summary>
    Stars = 1,

    /// <summary>A planet's sky of two colours: one overhead, one at the horizon.</summary>
    Colours = 2,

    /// <summary>The mapper's own picture, a panorama laid around the map (<see cref="MapSettings.SkyImage"/>).</summary>
    Image = 3,
}

/// <summary>
/// The settings of a map that tg's map json has not (docs/ai/specs/map-editor.md 5.10; R7 and R8): the station's name, the sky, the sun, and
/// the air a room and the outside start with. They are one key of the map json, <see cref="Key"/>, which tg's <c>LoadConfig</c> does not
/// read. A map without the key has the game's own of each: <see cref="Default"/>.
/// </summary>
public sealed record MapSettings
{
    /// <summary>The key of the map json that holds the settings.</summary>
    public const string Key = "anomaly";

    /// <summary>The key's name before the game was named Anomaly Station (D347): still read, and taken away when the settings are written.</summary>
    public const string OldKey = "ss3d";

    /// <summary>The most bytes of a sky's picture.</summary>
    public const int MaxSkyImageBytes = 4 * 1024 * 1024;

    /// <summary>The station's name; null: the game picks one, as tg does.</summary>
    public string StationName { get; init; }

    public MapSkyKind Sky { get; init; }

    /// <summary>The colour overhead and the colour at the horizon of <see cref="MapSkyKind.Colours"/>, as <c>#rrggbb</c>.</summary>
    public string SkyTop { get; init; } = "#27518f";

    public string SkyHorizon { get; init; } = "#a9c4de";

    /// <summary>The file of <see cref="MapSkyKind.Image"/>'s picture (png or jpg), beside the map's files; null: none chosen.</summary>
    public string SkyImage { get; init; }

    /// <summary>How bright the stars are, 0 to 4 (1: the game's own).</summary>
    public float Stars { get; init; } = 1f;

    /// <summary>Whether the map has a sun: its light, its disc in the sky and its flare.</summary>
    public bool Sun { get; init; } = true;

    public string SunColour { get; init; } = "#fff5e0";

    /// <summary>The strength of the sun's light, 0 to 8 (0.7: the game's own).</summary>
    public float SunEnergy { get; init; } = 0.7f;

    /// <summary>The radius of the sun's disc in degrees, 0.2 to 30.</summary>
    public float SunSize { get; init; } = 2f;

    /// <summary>How high the sun stands over the map's plane, 1 to 90 degrees.</summary>
    public float SunElevation { get; init; } = 35f;

    /// <summary>Where the sun stands, in degrees clockwise from north; null: it goes round the map as tg's does (<c>SSsun</c>).</summary>
    public float? SunAzimuth { get; init; }

    /// <summary>
    /// The air a floor of the station's levels starts with in the place of tg's default (<c>OPENTURF_DEFAULT_ATMOS</c>), as tg writes a gas
    /// mix (<c>o2=22;n2=82;TEMP=293.15</c>); null: tg's.
    /// </summary>
    public string AirInside { get; init; }

    /// <summary>The air of the station's levels' turfs that are open to a planet's sky (tg's <c>planetary_atmos</c>); null: each type's own.</summary>
    public string AirOutside { get; init; }

    /// <summary>
    /// A shuttle's map (docs/ai/specs/map-editor.md 5.11): the name and the description of its template, which tg writes in a type of its own for every
    /// shuttle; null: the shuttle's id. They are the json beside the shuttle's file.
    /// </summary>
    public string ShuttleName { get; init; }

    public string ShuttleDescription { get; init; }

    /// <summary>What the shuttle costs at the communications console, in credits; null: it is not for sale.</summary>
    public float? ShuttleCost { get; init; }

    /// <summary>The game's own of everything.</summary>
    public static MapSettings Default { get; } = new();

    /// <summary>
    /// The settings a map json holds: <see cref="Default"/> for a text that is no json object or has not the key, and for each value that
    /// is missing, of another kind or outside its range.
    /// </summary>
    public static MapSettings Read(string mapJson)
    {
        try
        {
            return ParseObject(mapJson) is { } root && (root[Key] ?? root[OldKey]) is JsonObject held ? From(held) : Default;
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    // tg's map jsons are read with comments and trailing commas let through (the host's MapSeam reads them so)
    private static readonly JsonDocumentOptions Lenient = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    /// <summary>
    /// A map json's text as an object, read as the host reads it: comments and trailing commas are let through. Null for a text that is json but no
    /// object; a text that is no json throws <see cref="JsonException"/>.
    /// </summary>
    public static JsonObject ParseObject(string mapJson) => JsonNode.Parse(mapJson ?? "null", null, Lenient) as JsonObject;

    /// <summary>
    /// The map json's text with these settings as its key (the key is taken away when they are the game's own), its other keys as they
    /// were, in tg's style of a tab a level. A text that is no json object throws <see cref="JsonException"/>.
    /// </summary>
    public string Write(string mapJson)
    {
        if (ParseObject(mapJson) is not { } root)
            throw new JsonException("a map json is an object");
        root.Remove(Key);
        root.Remove(OldKey);
        if (this != Default)
            root[Key] = ToJson();
        return root.ToJsonString(Written) + "\n";
    }

    private static readonly JsonSerializerOptions Written = new() { WriteIndented = true, IndentCharacter = '\t', IndentSize = 1, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static MapSettings From(JsonObject held)
    {
        MapSettings d = Default;
        JsonObject sky = held["sky"] as JsonObject, sun = held["sun"] as JsonObject, air = held["air"] as JsonObject, shuttle = held["shuttle"] as JsonObject;
        return new MapSettings
        {
            StationName = Text(held["station_name"], 64),
            Sky = Text(sky?["kind"], 16) switch { "stars" => MapSkyKind.Stars, "colours" => MapSkyKind.Colours, "image" => MapSkyKind.Image, _ => MapSkyKind.Space },
            SkyTop = Colour(sky?["top"]) ?? d.SkyTop,
            SkyHorizon = Colour(sky?["horizon"]) ?? d.SkyHorizon,
            SkyImage = FileName(Text(sky?["image"], 128)),
            Stars = Number(sky?["stars"], 0f, 4f) ?? d.Stars,
            Sun = sun?["on"] is not JsonValue on || !on.TryGetValue(out bool shines) || shines,
            SunColour = Colour(sun?["color"]) ?? d.SunColour,
            SunEnergy = Number(sun?["energy"], 0f, 8f) ?? d.SunEnergy,
            SunSize = Number(sun?["size"], 0.2f, 30f) ?? d.SunSize,
            SunElevation = Number(sun?["elevation"], 1f, 90f) ?? d.SunElevation,
            SunAzimuth = Number(sun?["azimuth"], 0f, 360f),
            AirInside = GasMix(Text(air?["inside"], 256)),
            AirOutside = GasMix(Text(air?["outside"], 256)),
            ShuttleName = Text(shuttle?["name"], 64),
            ShuttleDescription = Text(shuttle?["description"], 512),
            ShuttleCost = Number(shuttle?["credit_cost"], 0f, 1e9f),
        };
    }

    private JsonObject ToJson()
    {
        MapSettings d = Default;
        var held = new JsonObject();
        if (StationName != null)
            held["station_name"] = StationName;
        var sky = new JsonObject();
        if (Sky != MapSkyKind.Space)
            sky["kind"] = Sky.ToString().ToLowerInvariant();
        if (SkyTop != d.SkyTop)
            sky["top"] = SkyTop;
        if (SkyHorizon != d.SkyHorizon)
            sky["horizon"] = SkyHorizon;
        if (SkyImage != null)
            sky["image"] = SkyImage;
        if (Stars != d.Stars)
            sky["stars"] = Rounded(Stars);
        if (sky.Count > 0)
            held["sky"] = sky;
        var sun = new JsonObject();
        if (!Sun)
            sun["on"] = false;
        if (SunColour != d.SunColour)
            sun["color"] = SunColour;
        if (SunEnergy != d.SunEnergy)
            sun["energy"] = Rounded(SunEnergy);
        if (SunSize != d.SunSize)
            sun["size"] = Rounded(SunSize);
        if (SunElevation != d.SunElevation)
            sun["elevation"] = Rounded(SunElevation);
        if (SunAzimuth is { } azimuth)
            sun["azimuth"] = Rounded(azimuth);
        if (sun.Count > 0)
            held["sun"] = sun;
        var air = new JsonObject();
        if (AirInside != null)
            air["inside"] = AirInside;
        if (AirOutside != null)
            air["outside"] = AirOutside;
        if (air.Count > 0)
            held["air"] = air;
        var shuttle = new JsonObject();
        if (ShuttleName != null)
            shuttle["name"] = ShuttleName;
        if (ShuttleDescription != null)
            shuttle["description"] = ShuttleDescription;
        if (ShuttleCost is { } cost)
            shuttle["credit_cost"] = Rounded(cost);
        if (shuttle.Count > 0)
            held["shuttle"] = shuttle;
        return held;
    }

    private static double Rounded(float value) => Math.Round(value, 3);

    private static string Text(JsonNode node, int most)
        => node is JsonValue value && value.TryGetValue(out string text) && !string.IsNullOrWhiteSpace(text) && text.Length <= most ? text.Trim() : null;

    private static float? Number(JsonNode node, float least, float most)
        => node is JsonValue value && value.TryGetValue(out double number) && double.IsFinite(number) && number >= least && number <= most ? (float)number : null;

    /// <summary>A colour as <c>#rrggbb</c> in lower case, or null for a text that is none.</summary>
    public static string Colour(string text)
    {
        text = text?.Trim();
        return text is { Length: 7 } && text[0] == '#' && uint.TryParse(text.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _) ? text.ToLowerInvariant() : null;
    }

    private static string Colour(JsonNode node) => Colour(Text(node, 7));

    /// <summary>A colour's red, green and blue as one number, 0xRRGGBB (white for a text that is no colour).</summary>
    public static uint Rgb(string colour) => Colour(colour) is { } hex ? uint.Parse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture) : 0xFFFFFF;

    // A picture's file is beside the map's files: a bare name, no way out of their folder.
    private static string FileName(string text)
        => text != null && text.IndexOfAny(new[] { '/', '\\', ':' }) < 0 && text != "." && text != ".." && (text.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || text.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || text.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)) ? text : null;

    /// <summary>
    /// A gas mix as tg writes one (<c>id=moles;id=moles;TEMP=kelvin</c>: SSair's parse_gas_string), or null for a text that is none: pairs of a
    /// name and a number that is not below zero, the names letters, digits and underscores.
    /// </summary>
    public static string GasMix(string text)
    {
        text = text?.Trim().TrimEnd(';');
        if (string.IsNullOrEmpty(text))
            return null;
        foreach (string pair in text.Split(';'))
        {
            int equals = pair.IndexOf('=');
            if (equals < 1 || !pair.Substring(0, equals).Trim().All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
                return null;
            if (!float.TryParse(pair.AsSpan(equals + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float amount) || !float.IsFinite(amount) || amount < 0)
                return null;
        }
        return string.Join(';', text.Split(';').Select(pair => pair.Trim().Replace(" ", string.Empty)));
    }
}

/// <summary>
/// A world's map settings (<see cref="MapSettings"/>): what the maps folder's json holds, read when the map is mounted
/// (<see cref="MapSeam.Mount"/>), with the bytes of the sky's picture. The host sends the sky and the sun with the map, sets the
/// station's name and the sun's place, and the floors of the station's levels read their air here.
/// </summary>
public static class MapSettingsSeam
{
    private sealed class SeamState
    {
        internal MapSettings Settings = MapSettings.Default;
        internal byte[] SkyImage;
    }

    private static SeamState State(World world) => world.GetExtension(static _ => new SeamState());

    /// <summary>Gives <paramref name="world"/> its map's settings, before <c>TgWorld.Boot</c>.</summary>
    public static void Set(World world, MapSettings settings, byte[] skyImage)
    {
        ArgumentNullException.ThrowIfNull(world);
        SeamState state = State(world);
        (state.Settings, state.SkyImage) = (settings ?? MapSettings.Default, skyImage);
    }

    /// <summary>The settings of <paramref name="world"/>'s map (the game's own when none were given).</summary>
    public static MapSettings Of(World world) => State(world).Settings;

    /// <summary>The bytes of the sky's picture, a png or a jpg file's; null: none.</summary>
    public static byte[] SkyImageOf(World world) => State(world).SkyImage;

    /// <summary>The settings of the world a tg proc runs in.</summary>
    public static MapSettings Current => World.CurrentOrNull is World world ? State(world).Settings : MapSettings.Default;
}
