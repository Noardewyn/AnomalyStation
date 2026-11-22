using Godot;
using Anomaly.Client.Render;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client.Hud;

/// <summary>
/// A stand-in for M2 P7's atlas (<see cref="IAppearanceTextures"/>) until it is plugged in (<c>App.Textures</c>): it draws the
/// few screen objects of the health HUD as flat shapes, by their icon states, so the HUD (layout, the zone labels, the
/// hatching, the palettes, the fullscreen limit) can be run and looked at without tg's icons. The doll's zones are boxes in
/// the places of a body whose colour runs from green to red with the level; the indicator is a disc; the damage overlay a
/// red vignette. Everything else is a grey square. Not tg's art: D107's sprites come from P7's atlas.
/// </summary>
public sealed class PlaceholderTextures : IAppearanceTextures
{
    private readonly Dictionary<string, ComposedAppearance> _cache = new();

    private static readonly Color[] Levels =
    {
        new(0.20f, 0.80f, 0.30f), new(0.55f, 0.80f, 0.20f), new(0.90f, 0.85f, 0.20f),
        new(0.95f, 0.60f, 0.15f), new(0.90f, 0.30f, 0.15f), new(0.70f, 0.05f, 0.10f),
    };

    public bool TryCompose(ReplicaWorld replica, TableRef appearance, byte dir, double seconds, out ComposedAppearance composed)
    {
        composed = default;
        if (!replica.TryGetAppearance(appearance, out AppearanceEntry entry))
            return false;
        string key = Signature(replica, entry, 0);
        if (_cache.TryGetValue(key, out composed))
            return true;
        string icon = replica.ResolveResource(entry.Icon) ?? string.Empty;
        bool full = icon.Contains("screen_full", StringComparison.Ordinal);
        int pixels = full ? 96 : 32;
        Image image = Image.CreateEmpty(pixels, pixels, false, Image.Format.Rgba8);
        image.Fill(new Color(0, 0, 0, 0));
        Draw(replica, entry, image, full, 0);
        composed = new ComposedAppearance(ImageTexture.CreateFromImage(image), full ? 480 : 32, full ? 480 : 32, 0, 0);
        _cache[key] = composed;
        return true;
    }

    public void Clear() => _cache.Clear();

    private static string Signature(ReplicaWorld replica, AppearanceEntry entry, int depth)
    {
        string text = (replica.ResolveResource(entry.Icon) ?? "") + "#" + (replica.ResolveString(entry.IconState) ?? "") + "#" + entry.Alpha;
        if (depth < 3)
        {
            foreach (TableRef over in entry.Overlays)
            {
                if (replica.TryGetAppearance(over, out AppearanceEntry o))
                    text += "|" + Signature(replica, o, depth + 1);
            }
        }
        return text;
    }

    private static void Draw(ReplicaWorld replica, AppearanceEntry entry, Image image, bool full, int depth)
    {
        string state = replica.ResolveString(entry.IconState);
        float alpha = entry.Alpha / 255f;
        if (state != null)
        {
            if (HealthZones.TryParseState(state, out ZoneState zone))
                DrawZone(image, zone, alpha);
            else if (HealthZones.TryParseIndicator(state) is int n and >= 0)
                DrawDisc(image, n, alpha);
            else if (state == "brutedamageoverlay" || state == "oxydamageoverlay")
                DrawVignette(image, alpha);
            else if (state == "running" || state == "walking")
                DrawArrows(image, state == "running", alpha);
            else if (state != "healthdoll" && !full)
                DrawBox(image, 0, 0, 32, 32, new Color(0.5f, 0.5f, 0.55f, 0.5f * alpha));
        }
        if (depth >= 3)
            return;
        foreach (TableRef over in entry.Underlays)
        {
            if (replica.TryGetAppearance(over, out AppearanceEntry u))
                Draw(replica, u, image, full, depth + 1);
        }
        foreach (TableRef over in entry.Overlays)
        {
            if (replica.TryGetAppearance(over, out AppearanceEntry o))
                Draw(replica, o, image, full, depth + 1);
        }
    }

    private static void DrawZone(Image image, ZoneState zone, float alpha)
    {
        Color c = zone.Kind switch
        {
            ZoneKind.Level => Levels[Math.Clamp(zone.Level, 0, 5)],
            ZoneKind.Missing => new Color(0.40f, 0.40f, 0.40f),
            ZoneKind.Disabled => new Color(0.30f, 0.40f, 0.70f),
            _ => new Color(0.15f, 0.15f, 0.15f),
        };
        c.A = alpha;
        (int x, int y, int w, int h) = zone.Zone switch
        {
            "head" => (12, 3, 8, 8),
            "chest" => (11, 12, 10, 10),
            "l_arm" => (6, 12, 4, 10),
            "r_arm" => (22, 12, 4, 10),
            "l_leg" => (11, 23, 4, 8),
            _ => (17, 23, 4, 8),
        };
        DrawBox(image, x, y, w, h, c);
    }

    private static void DrawDisc(Image image, int number, float alpha)
    {
        Color c = number >= 7 ? new Color(0.1f, 0.1f, 0.1f) : Levels[Math.Clamp(number * 5 / 6, 0, 5)];
        c.A = alpha;
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dx = x - 15.5f, dy = y - 15.5f;
                if (dx * dx + dy * dy <= 11f * 11f)
                    image.SetPixel(x, y, c);
            }
        }
    }

    private static void DrawVignette(Image image, float alpha)
    {
        int n = image.GetWidth();
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float edge = Math.Clamp((MathF.Max(MathF.Abs(dx), MathF.Abs(dy)) - 0.45f) / 0.55f, 0f, 1f);
                image.SetPixel(x, y, new Color(0.8f, 0.02f, 0.02f, edge * edge * alpha));
            }
        }
    }

    private static void DrawArrows(Image image, bool running, float alpha)
    {
        var c = new Color(0.9f, 0.9f, 0.95f, alpha);
        int count = running ? 2 : 1;
        for (int k = 0; k < count; k++)
        {
            int x0 = (running ? 6 : 12) + k * 9;
            for (int row = 0; row < 12; row++)
            {
                int half = row < 6 ? row : 11 - row;
                for (int w = 0; w <= half; w++)
                    image.SetPixel(Math.Clamp(x0 + w, 0, 31), 10 + row, c);
            }
        }
    }

    private static void DrawBox(Image image, int x, int y, int w, int h, Color c)
    {
        for (int yy = y; yy < y + h && yy < image.GetHeight(); yy++)
        {
            for (int xx = x; xx < x + w && xx < image.GetWidth(); xx++)
                image.SetPixel(xx, yy, c);
        }
    }
}
