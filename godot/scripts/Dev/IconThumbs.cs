using System.Diagnostics;
using Godot;
using Anomaly.Client.Shared.Icons;

namespace Anomaly.Client.Dev;

// The dev menu's icons (the owner, 2026-10-13: "the icons of the items and gases visible in these tools"): the picture a
// type shows on the map before anything changes it, the south-facing first frame of its icon state (BYOND's display rule:
// the named state, else the icon's default one), cut from the client's converted .dmi sheets (or a GAGS composition) and
// registered with the ImGui layer. Made on the main thread within a time budget per frame, the rows asking for them first.

/// <summary>One icon picture registered with ImGui: its id and its size in pixels (an id of zero: the type has no picture).</summary>
public readonly record struct Thumb(IntPtr Id, int Width, int Height)
{
    public bool HasPicture => Id != IntPtr.Zero;

    /// <summary>The picture itself, for the engine's own controls (the map editor's lists); null when the type has no picture.</summary>
    public Texture2D Texture { get; init; }
}

/// <summary>A cache of icon thumbnails for the dev menu.</summary>
public sealed class IconThumbs
{
    /// <summary>The most thumbnails waiting to be made (the oldest requests are dropped: they scrolled away).</summary>
    public const int MaxWaiting = 256;

    private readonly IconLibrary _icons;
    private readonly ImGuiLayer _imgui;
    private readonly Dictionary<(string Icon, string State), Thumb> _made = new();
    private readonly LinkedList<(string Icon, string State)> _waiting = new();
    private readonly HashSet<(string Icon, string State)> _queued = new();

    public IconThumbs(IconLibrary icons, ImGuiLayer imgui)
    {
        _icons = icons;
        _imgui = imgui;
    }

    /// <summary>How many thumbnails are made.</summary>
    public int Count => _made.Count;

    /// <summary>The thumbnail of an icon and state; false (and queued, newest first) when it is not made yet.</summary>
    public bool TryGet(string icon, string state, out Thumb thumb)
    {
        var key = (icon ?? string.Empty, state ?? string.Empty);
        if (_made.TryGetValue(key, out thumb))
            return true;
        if (key.Item1.Length == 0)
        {
            _made[key] = thumb = default;
            return true;
        }
        if (_queued.Add(key))
        {
            _waiting.AddFirst(key);
            if (_waiting.Count > MaxWaiting)
            {
                _queued.Remove(_waiting.Last.Value);
                _waiting.RemoveLast();
            }
        }
        return false;
    }

    /// <summary>Makes waiting thumbnails until <paramref name="budgetMs"/> is spent (one at least).</summary>
    public void Pump(double budgetMs)
    {
        long start = Stopwatch.GetTimestamp();
        while (_waiting.Count > 0)
        {
            (string Icon, string State) key = _waiting.First.Value;
            _waiting.RemoveFirst();
            _queued.Remove(key);
            _made[key] = Make(key.Icon, key.State);
            if (Stopwatch.GetElapsedTime(start).TotalMilliseconds >= budgetMs)
                break;
        }
    }

    private Thumb Make(string icon, string state)
    {
        try
        {
            if (!_icons.TryGetIcon(icon, out IIcon source) || !source.TryGetShown(state, out IIconState shown))
                return default;
            Raster raster = shown.Get(ByondDir.Index(ByondDir.South, shown.Dirs), 0);
            if (raster == null || raster.Width == 0 || raster.Height == 0)
                return default;
            Image image = Image.CreateFromData(raster.Width, raster.Height, false, Image.Format.Rgba8, raster.Pixels);
            ImageTexture texture = ImageTexture.CreateFromImage(image);
            return new Thumb(_imgui.AddTexture(texture), raster.Width, raster.Height) { Texture = texture };
        }
        catch (Exception e)
        {
            GD.PushWarning($"dev menu: no icon for {icon} [{state}]: {e.Message}");
            return default;
        }
    }
}
