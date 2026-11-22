using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Anomaly.Tools.Icons;

// Owner: M2 P7. tg's GAGS (code/datums/greyscale): a greyscale config is a DM type (code/datums/greyscale/config_types) naming an
// icon file and a JSON file of icon states, each a list of layers. The conversion writes every config as one JSON file the
// client composes (godot/scripts/Shared/Icons/GagsConfig.cs, the D104 deviation "GAGS icons are composed on the client"):
//
//   gags/<type path without /datum/greyscale_config/>.json  =  { "type": "/datum/greyscale_config/jumpsuit/worn", "name": "Jumpsuit (Worn)",
//       "icon_file": "icons/mob/clothing/under/color.dmi", "json_config": "code/datums/greyscale/json_configs/jumpsuit_worn.json",
//       "states": { <the config's own JSON: state name -> layers> } }
//   gags/index.json  =  [ { "type": ..., "file": "jumpsuit/worn.json", "name": ..., "icon_file": ... } ]
//
// The DM type tree gives a type the icon_file and json_config of its nearest ancestor that sets one (the cerulean "basic" config
// sets only its json_config); a type with no json_config is a base type and is not written.

/// <summary>One greyscale config type of tg's DM code.</summary>
public sealed class GagsType
{
    /// <summary>The type path, <c>/datum/greyscale_config/jumpsuit/worn</c>.</summary>
    public string Path = string.Empty;

    /// <summary>The user-friendly name.</summary>
    public string Name;

    /// <summary>The icon file as written (<c>icons/...dmi</c> or a #define's name before it is resolved).</summary>
    public string IconFile;

    /// <summary>The JSON config's path relative to the tgstation root.</summary>
    public string JsonConfig;
}

/// <summary>Finds and resolves tg's greyscale config types.</summary>
public static class GagsReader
{
    /// <summary>The path every config type starts with.</summary>
    public const string Root = "/datum/greyscale_config";

    private static readonly Regex TypeLine = new(@"^(/datum/greyscale_config[A-Za-z0-9_/]*)\s*(//.*)?$", RegexOptions.Compiled);
    private static readonly Regex VarLine = new(@"^\s+(name|icon_file|json_config)\s*=\s*(""[^""]*""|'[^']*'|[A-Za-z_][A-Za-z0-9_]*)\s*(//.*)?$", RegexOptions.Compiled);
    private static readonly Regex DefineLine = new(@"^#define\s+([A-Z][A-Z0-9_]*)\s+'([^']+)'", RegexOptions.Compiled);

    /// <summary>
    /// Reads every config type of <paramref name="tgRoot"/>'s <c>code/</c> and resolves inheritance and defines. The result is
    /// sorted by type path; base types without a JSON config are left out.
    /// </summary>
    public static List<GagsType> Read(string tgRoot)
    {
        string code = System.IO.Path.Combine(tgRoot, "code");
        var defines = new Dictionary<string, string>(StringComparer.Ordinal);
        string defineDir = System.IO.Path.Combine(code, "__DEFINES");
        if (Directory.Exists(defineDir))
        {
            foreach (string file in Directory.EnumerateFiles(defineDir, "*.dm", SearchOption.AllDirectories))
            {
                foreach (string line in File.ReadLines(file))
                {
                    Match m = DefineLine.Match(line);
                    if (m.Success)
                        defines[m.Groups[1].Value] = m.Groups[2].Value;
                }
            }
        }

        var types = new Dictionary<string, GagsType>(StringComparer.Ordinal);
        string configDir = System.IO.Path.Combine(code, "datums", "greyscale");
        if (Directory.Exists(configDir))
        {
            foreach (string file in Directory.EnumerateFiles(configDir, "*.dm", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
                ReadFile(file, types, defines);
        }

        var result = new List<GagsType>();
        foreach (GagsType type in types.Values.OrderBy(t => t.Path, StringComparer.Ordinal))
        {
            // Inheritance by path: the nearest ancestor that sets a variable gives it.
            string name = type.Name, icon = type.IconFile, json = type.JsonConfig;
            string parent = type.Path;
            while ((name == null || icon == null || json == null) && TryParent(parent, out parent))
            {
                if (!types.TryGetValue(parent, out GagsType ancestor))
                    continue;
                name ??= ancestor.Name;
                icon ??= ancestor.IconFile;
                json ??= ancestor.JsonConfig;
            }
            if (json == null)
                continue;
            result.Add(new GagsType { Path = type.Path, Name = name ?? type.Path, IconFile = icon, JsonConfig = json });
        }
        return result;
    }

    /// <summary>The file name (relative to <c>gags/</c>, with '/') of a config type: <c>/datum/greyscale_config/jumpsuit/worn</c> is <c>jumpsuit/worn.json</c>.</summary>
    public static string FileOf(string typePath)
    {
        string relative = typePath.StartsWith(Root + "/", StringComparison.Ordinal) ? typePath.Substring(Root.Length + 1) : typePath.TrimStart('/');
        return relative + ".json";
    }

    private static bool TryParent(string path, out string parent)
    {
        int slash = path.LastIndexOf('/');
        parent = slash > 0 ? path.Substring(0, slash) : null;
        return parent != null;
    }

    private static void ReadFile(string file, Dictionary<string, GagsType> types, Dictionary<string, string> defines)
    {
        GagsType current = null;
        foreach (string raw in File.ReadLines(file))
        {
            Match type = TypeLine.Match(raw);
            if (type.Success)
            {
                string path = type.Groups[1].Value;
                if (!types.TryGetValue(path, out current))
                    types[path] = current = new GagsType { Path = path };
                continue;
            }
            if (raw.Length > 0 && !char.IsWhiteSpace(raw[0]))
            {
                // Another definition (a proc, a different type): the variables that follow are not a config's.
                current = null;
                continue;
            }
            if (current == null)
                continue;
            Match var = VarLine.Match(raw);
            if (!var.Success)
                continue;
            string value = var.Groups[2].Value;
            bool quoted = value[0] == '"';
            bool resource = value[0] == '\'';
            if (quoted || resource)
                value = value.Substring(1, value.Length - 2);
            else if (!defines.TryGetValue(value, out value))
                continue;
            switch (var.Groups[1].Value)
            {
                case "name":
                    current.Name = value;
                    break;
                case "icon_file":
                    current.IconFile = value;
                    break;
                case "json_config":
                    current.JsonConfig = value;
                    break;
            }
        }
    }

    /// <summary>The JSON text of the config <paramref name="type"/> (its own file embedded as <c>states</c>); throws <see cref="InvalidDataException"/> on a bad or missing file.</summary>
    public static string Convert(string tgRoot, GagsType type)
    {
        string path = System.IO.Path.Combine(tgRoot, type.JsonConfig);
        if (!File.Exists(path))
            throw new InvalidDataException($"{type.Path}: the config file {type.JsonConfig} does not exist");
        if (type.IconFile == null)
            throw new InvalidDataException($"{type.Path}: no icon_file");
        byte[] bytes = File.ReadAllBytes(path);
        using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"{type.JsonConfig}: the root is not an object");
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteString("type", type.Path);
            json.WriteString("name", type.Name);
            json.WriteString("icon_file", type.IconFile);
            json.WriteString("json_config", type.JsonConfig.Replace('\\', '/'));
            json.WritePropertyName("states");
            document.RootElement.WriteTo(json);
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>The index file's text for the written configs.</summary>
    public static string WriteIndex(IReadOnlyList<GagsType> written)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartArray();
            foreach (GagsType type in written)
            {
                json.WriteStartObject();
                json.WriteString("type", type.Path);
                json.WriteString("file", FileOf(type.Path));
                json.WriteString("name", type.Name);
                json.WriteString("icon_file", type.IconFile);
                json.WriteEndObject();
            }
            json.WriteEndArray();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
