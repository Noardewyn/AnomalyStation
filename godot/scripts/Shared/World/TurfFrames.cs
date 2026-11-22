using Anomaly.Client.Shared.Icons;

namespace Anomaly.Client.Shared.World;

// Owner: M4v slice 2 (D143). Engine-free. A turf whose icon state animates (MetaStation: the mech bay's recharge floors, the
// park's water, the asteroid's glowing basalt) has its pictures as consecutive layers of the tile kit's Texture2DArray; every tile
// of that picture shows the layer of the frame it is at. All tiles of one picture play in step (BYOND animates an icon state by
// the client's clock), so the renderer writes a new layer only into the tiles of the pictures whose frame changed, and no chunk is rebuilt.

/// <summary>The animated turf pictures and the layer each one shows now.</summary>
public sealed class TurfFrames
{
    private readonly List<(AnimationTimeline Timeline, int[] Layers)> _groups = new();
    private readonly List<int> _shown = new();

    /// <summary>The animated pictures.</summary>
    public int Count => _groups.Count;

    /// <summary>Adds a picture whose timeline's picture <c>i</c> is layer <c>layers[i]</c>; its group number.</summary>
    public int Add(AnimationTimeline timeline, int[] layers)
    {
        if (timeline == null || layers == null || layers.Length != timeline.Count || layers.Length == 0)
            throw new ArgumentException("one layer per picture of the timeline");
        _groups.Add((timeline, layers));
        _shown.Add(0);
        return _groups.Count - 1;
    }

    /// <summary>The layer group <paramref name="group"/> shows now (its first before the first <see cref="Step"/>).</summary>
    public int LayerNow(int group) => _groups[group].Layers[_shown[group]];

    /// <summary>The layer group <paramref name="group"/> shows <paramref name="seconds"/> into its animation.</summary>
    public int LayerAt(int group, double seconds) => _groups[group].Layers[_groups[group].Timeline.FrameAt(seconds)];

    /// <summary>Moves every group to <paramref name="seconds"/> on the clock; the groups whose layer changed are added to <paramref name="changed"/>.</summary>
    public void Step(double seconds, List<int> changed)
    {
        for (int g = 0; g < _groups.Count; g++)
        {
            int frame = _groups[g].Timeline.FrameAt(seconds);
            if (frame == _shown[g])
                continue;
            _shown[g] = frame;
            changed.Add(g);
        }
    }
}
