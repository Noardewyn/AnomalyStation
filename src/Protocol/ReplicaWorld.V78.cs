namespace Anomaly.Protocol;

// Version 78 (silicons SI4, MessagesV78.cs): the camera chunks' static the own eye sees (DV386), the popup windows (DV387) and the own
// mob's command verbs, each the server's last word.

public sealed partial class ReplicaWorld
{
    private readonly Dictionary<(int X, int Y, int Z), ulong> _static = new();

    /// <summary>Version 78 (DV386): the chunks the own camera eye looks into, keyed by their first tile, with their obscured tiles' bits.</summary>
    public IReadOnlyDictionary<(int X, int Y, int Z), ulong> StaticChunks => _static;

    /// <summary>Version 78 (DV386): bumped at each change of <see cref="StaticChunks"/>.</summary>
    public int StaticVersion { get; private set; }

    /// <summary>Version 78 (DV387): the popup windows open on the client.</summary>
    public IReadOnlyList<PopupWindow> Popups { get; private set; } = Array.Empty<PopupWindow>();

    /// <summary>Version 78: the own mob's command verbs.</summary>
    public IReadOnlyList<VerbEntry> Verbs { get; private set; } = Array.Empty<VerbEntry>();

    /// <summary>Version 78 (DV387): the second views' areas (the popup maps' and the multicamera windows').</summary>
    public IReadOnlyList<ViewArea> ViewAreas { get; private set; } = Array.Empty<ViewArea>();

    /// <summary>Version 78 (DV386): whether the tile is in a chunk the own camera eye sees and no camera sees it (tg's static there).</summary>
    public bool IsObscured(int x, int y, int z)
    {
        if (_static.Count == 0 || x < 1 || y < 1) return false;
        int cx = ChunkCoord(x), cy = ChunkCoord(y);
        return _static.TryGetValue((cx, cy, z), out ulong mask) && (mask & (1UL << ((y - cy) * 8 + (x - cx)))) != 0;
    }

    /// <summary>tg's <c>GET_CHUNK_COORD</c>: the first tile of the chunk a tile's coordinate is in (<c>max(FLOOR(v, 8), 1)</c>: 1, 8, 16, ...).</summary>
    public static int ChunkCoord(int v) => Math.Max(v / 8 * 8, 1);

    private void ApplyOwnV78(OwnBlock block, bool isSnapshot)
    {
        if (block.StaticChunks != null || isSnapshot)
        {
            _static.Clear();
            if (block.StaticChunks != null)
                foreach (StaticChunk chunk in block.StaticChunks)
                {
                    if (ChunkCoord(chunk.X) != chunk.X || ChunkCoord(chunk.Y) != chunk.Y)
                        throw Bad("a static chunk that does not start a chunk");
                    _static[(chunk.X, chunk.Y, chunk.Z)] = chunk.Obscured;
                }
            StaticVersion++;
        }
        if (block.Popups != null || isSnapshot)
            Popups = block.Popups ?? new List<PopupWindow>();
        if (block.Verbs != null || isSnapshot)
            Verbs = block.Verbs ?? new List<VerbEntry>();
        if (block.ViewAreas != null || isSnapshot)
            ViewAreas = block.ViewAreas ?? new List<ViewArea>();
    }
}
