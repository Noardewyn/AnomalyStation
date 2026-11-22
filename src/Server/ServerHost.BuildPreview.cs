using System.Globalization;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server;

// Protocol version 70 (D320, DV352): the build preview. After the tick, what the item in the session's mob's active hand would build
// (obj_item.build_preview: the RPD's next pipe) goes to its client when it changed, for the ghost on the aimed tile.
public sealed partial class ServerHost
{
    private void SendBuildPreview(Session s)
    {
        BuildPreviewMessage now = BuildPreviewMessage.None;
        BuildPreview? preview = null;
        if (s.Client?.mob is mob m && m.gc_destroyed == null)
        {
            using (World.Enter()) // after the tick: tg's procs read the ambient world (GLOB)
                preview = m.get_active_held_item() is obj_item held && held.gc_destroyed == null ? held.build_preview(m) : null;
        }
        if (preview is BuildPreview p && !string.IsNullOrEmpty(p.Icon) && p.Place is turf place && place.gc_destroyed == null)
        {
            now = new BuildPreviewMessage(Cut(p.Icon), Cut(p.State ?? ""), (byte)Math.Clamp(p.Dir, 0, 10), Rgba(p.Color),
                (sbyte)Math.Clamp(p.PixelX, -128, 127), (sbyte)Math.Clamp(p.PixelY, -128, 127), (int)place.x, (int)place.y, (int)place.z);
        }
        if (now == s.BuildPreview)
            return;
        s.BuildPreview = now;
        Send(s, Channel.State, Codec.Encode(now));
    }

    private static string Cut(string text) => text.Length > Limits.MaxBuildPreviewTextBytes / 4 ? text[..(Limits.MaxBuildPreviewTextBytes / 4)] : text; // 4 bytes a char at most

    // tg's "#rrggbb" or "#rrggbbaa" as RGBA; white for none or anything else
    private static uint Rgba(string color)
    {
        if (color is { Length: 7 or 9 } && color[0] == '#' && uint.TryParse(color.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v))
            return color.Length == 7 ? (v << 8) | 0xFF : v;
        return 0xFFFFFFFF;
    }
}
