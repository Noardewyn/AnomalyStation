using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Text;

namespace Anomaly.Tools.Icons;

// Silicons SI4 (D326): the icon state table (src/Core/Tg/Generated/IconStates.bin, committed): every .dmi under tg's icons/ and every
// GAGS json config with the names of its states, which tg's icon_exists asks for (rust-g's icon metadata, icons.dm:1214). The server reads no .dmi (D104), so it
// reads this table, as ShotGeometry reads the body-part masks (D179).

/// <summary>Writes the icon state table: deflated UTF-8 lines, a file's path (from tg's root) then its state names, tab-separated.</summary>
public static class IconStateTable
{
    public static int Run(string tgRoot, string outputFile, TextWriter output, TextWriter error)
    {
        string icons = Path.Combine(tgRoot, "icons");
        if (!Directory.Exists(icons))
        {
            error.WriteLine("Anomaly.Tools.Icons: no " + icons);
            return 1;
        }
        var files = Directory.EnumerateFiles(icons, "*.dmi", SearchOption.AllDirectories).ToList();
        files.Sort(StringComparer.Ordinal);
        var text = new StringBuilder();
        int states = 0, skipped = 0;
        foreach (string file in files)
        {
            DmiFile dmi;
            try
            {
                dmi = DmiReader.Read(File.ReadAllBytes(file));
            }
            catch (InvalidDataException)
            {
                skipped++;
                continue;
            }
            text.Append(Path.GetRelativePath(tgRoot, file).Replace('\\', '/'));
            foreach (string name in dmi.States.Select(s => s.Name).Distinct(StringComparer.Ordinal))
            {
                text.Append('\t').Append(name.Replace('\t', ' ').Replace('\n', ' '));
                states++;
            }
            text.Append('\n');
        }
        // the GAGS configs: a generated GAGS icon has the states its json config names (greyscale_config.dm's Generate), so icon_exists
        // asks the config's json (DV19: the client composes the icon)
        string gags = Path.Combine(tgRoot, "code", "datums", "greyscale", "json_configs");
        int configs = 0;
        foreach (string file in Directory.Exists(gags) ? Directory.EnumerateFiles(gags, "*.json").Order(StringComparer.Ordinal) : Enumerable.Empty<string>())
        {
            using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(file));
            text.Append(Path.GetRelativePath(tgRoot, file).Replace('\\', '/'));
            foreach (System.Text.Json.JsonProperty state in json.RootElement.EnumerateObject())
            {
                text.Append('\t').Append(state.Name.Replace('\t', ' ').Replace('\n', ' '));
                states++;
            }
            text.Append('\n');
            configs++;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputFile)));
        using (FileStream stream = File.Create(outputFile))
        using (var deflate = new DeflateStream(stream, CompressionLevel.SmallestSize))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text.ToString());
            deflate.Write(bytes, 0, bytes.Length);
        }
        output.WriteLine($"Anomaly.Tools.Icons: {files.Count - skipped} icon files and {configs} GAGS configs, {states} states -> {outputFile} ({new FileInfo(outputFile).Length} bytes; {skipped} unreadable)");
        return 0;
    }
}
