using System.IO;
using System.Linq;
using System.Text;
using Anomaly.Client.Shared.Icons;

namespace Anomaly.Tools.Icons;

// M7x slice A (D177, D179: the body parts' pixels reach the server as a committed table; D104 kept: the server reads no icon at run
// time). The opaque pixels of every body-part icon state (tg's get_limb_icon states "<limb id>_<zone>[_<gender>]" and the hands
// "<limb id>_l_hand"/"_r_hand") of tg's body-part icon files, first frame, per direction: 32 rows of 32 bits. The server's
// ShotGeometry reads the table to find the body part a shot crosses (src/Core/Tg/Generated/BodyMasks.bin, an embedded resource).
//
// Format (little end): "ANOMBM1\n", u16 entry count, then per entry: u8 file length, the file path (UTF-8, tg's icons/... path),
// u8 state length, the state, u8 dir count, then per dir: u8 BYOND dir, 32 x u32 rows (row 0 is tg's icon-y 1, the bottom; bit 0
// is icon-x 1). A pixel counts when its alpha is at least the client's pick alpha (BillboardMath.PickAlpha, 40).

/// <summary>The body-part mask table (<c>--masks FILE</c>).</summary>
public static class BodyMaskTable
{
    /// <summary>The icon files read: tg's human body parts and every species' and augment's (paths under the tg clone), the client's list.</summary>
    public static readonly string[] Roots = LimbIcons.Roots;

    /// <summary>
    /// Package T, slice T2: the masks tg cuts body parts with, every state kept: a leg's two images are its icon Blended (ICON_MULTIPLY) with
    /// <c>right_leg</c>/<c>left_leg</c> and the <c>_lower</c> states (carbon_update_icons.dm handle_masking).
    /// </summary>
    public static readonly string[] Cuts = { "icons/mob/leg_masks.dmi" };

    public const string Magic = "ANOMBM1\n";

    /// <summary>Writes the table for the tg clone at <paramref name="tgRoot"/> to <paramref name="outputFile"/>; 0 on success.</summary>
    public static int Run(string tgRoot, string outputFile, TextWriter output, TextWriter error)
    {
        var files = new List<string>();
        foreach (string root in Roots.Concat(Cuts))
        {
            string full = Path.Combine(tgRoot, root);
            if (File.Exists(full))
                files.Add(full);
            else if (Directory.Exists(full))
                files.AddRange(Directory.EnumerateFiles(full, "*.dmi", SearchOption.AllDirectories));
            else
            {
                error.WriteLine("Anomaly.Tools.Icons: no " + full);
                return 1;
            }
        }
        files.Sort(StringComparer.Ordinal);
        var entries = new List<(string File, string State, List<(byte Dir, uint[] Rows)> Dirs)>();
        foreach (string file in files)
        {
            byte[] bytes = File.ReadAllBytes(file);
            DmiFile dmi = DmiReader.Read(bytes);
            if (dmi.Width != 32 || dmi.Height != 32)
                continue;
            Raster sheet = PngDecoder.Decode(bytes);
            string relative = Path.GetRelativePath(tgRoot, file).Replace('\\', '/');
            var seen = new HashSet<string>(StringComparer.Ordinal);
            bool cut = Cuts.Contains(relative);
            foreach (DmiState state in dmi.States)
            {
                if (!(cut || IsLimbState(state.Name)) || !seen.Add(state.Name))
                    continue; // the first state of a name is the one BYOND shows
                var dirs = new List<(byte, uint[])>();
                for (int d = 0; d < state.Dirs; d++)
                {
                    (int x0, int y0) = dmi.FramePosition(state.First + d);
                    var rows = new uint[32];
                    for (int y = 0; y < 32; y++)
                        for (int x = 0; x < 32; x++)
                        {
                            int sx = x0 + x, sy = y0 + (31 - y); // row 0 is the bottom
                            if (sx >= sheet.Width || sy >= sheet.Height)
                                continue;
                            if (sheet.Pixels[(sy * sheet.Width + sx) * 4 + 3] >= BillboardMath.PickAlpha)
                                rows[y] |= 1u << x;
                        }
                    dirs.Add((ByondDir.FromIndex(d), rows));
                }
                entries.Add((relative, state.Name, dirs));
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputFile)));
        using (var stream = File.Create(outputFile))
        using (var w = new BinaryWriter(stream))
        {
            w.Write(Encoding.ASCII.GetBytes(Magic));
            w.Write((ushort)entries.Count);
            foreach ((string file, string state, List<(byte Dir, uint[] Rows)> dirs) in entries)
            {
                WriteText(w, file);
                WriteText(w, state);
                w.Write((byte)dirs.Count);
                foreach ((byte dir, uint[] rows) in dirs)
                {
                    w.Write(dir);
                    foreach (uint row in rows)
                        w.Write(row);
                }
            }
        }
        output.WriteLine("Anomaly.Tools.Icons: " + entries.Count + " body-part states from " + files.Count + " files -> " + outputFile);
        return 0;
    }

    private static void WriteText(BinaryWriter w, string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        w.Write((byte)bytes.Length);
        w.Write(bytes);
    }

    /// <summary>A body-part state: the client's <c>BodyZoneAim.ZoneOfState</c> names a zone for it (<c>human_head_m</c>, <c>lizard_l_hand</c>).</summary>
    public static bool IsLimbState(string state)
    {
        if (string.IsNullOrEmpty(state))
            return false;
        if (state.EndsWith("_m", StringComparison.Ordinal) || state.EndsWith("_f", StringComparison.Ordinal))
            state = state.Substring(0, state.Length - 2);
        foreach (string zone in new[] { "_head", "_chest", "_l_arm", "_l_hand", "_r_arm", "_r_hand", "_l_leg", "_r_leg" })
            if (state.EndsWith(zone, StringComparison.Ordinal))
                return true;
        return false;
    }
}
