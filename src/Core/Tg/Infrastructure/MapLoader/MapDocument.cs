using System.Text;

namespace Anomaly.Core.Tg;

// Owner: the map editor (docs/ai/specs/map-editor.md 4, D336). The editable form of a .as3dmap file
// (docs/ai/specs/map-format.md 4): the header and the model of every tile, changed tile by tile in undoable steps and written
// back in the format's stable order, so a map that was read and not edited is written byte for byte as it was read.
// Pure: no World, no tg code, no statics.

/// <summary>One tile whose model an edit, an undo or a redo changed.</summary>
/// <param name="X">The tile's x, from 1.</param>
/// <param name="Y">The tile's y, from 1.</param>
/// <param name="Z">The tile's level, from 1.</param>
/// <param name="Before">The model the tile had.</param>
/// <param name="After">The model the tile has now.</param>
public readonly record struct MapTileChange(int X, int Y, int Z, MapModel Before, MapModel After);

/// <summary>
/// A map held in memory for editing. Tiles change only inside a step (<see cref="Begin"/> to <see cref="End"/>, or
/// <see cref="Edit"/> for a whole one), which <see cref="Undo"/> takes back as one; <see cref="ToText"/> writes the file.
/// Models are interned by their text, so equal tiles share one <see cref="MapModel"/> and compare by reference.
/// </summary>
public sealed class MapDocument
{
    /// <summary>The widest map the format holds (<c>size</c>, map-format.md 4.1).</summary>
    public const int MaxWidth = 255;

    /// <summary>The highest map the format holds.</summary>
    public const int MaxHeight = 255;

    /// <summary>The most levels the format holds.</summary>
    public const int MaxLevels = 64;

    /// <summary>The most steps the history keeps; the oldest is forgotten first.</summary>
    public const int HistoryLimit = 1000;

    private const string MembersSource = "<members>";

    private readonly MapModel[] _tiles;
    private readonly Dictionary<string, MapModel> _models = new(StringComparer.Ordinal);
    private readonly List<Step> _undo = new();
    private readonly List<Step> _redo = new();
    private readonly List<MapTileChange> _pending = new();
    private Step _open;
    private bool _batch;
    private long _nextStepId = 1, _baseId, _savedId;

    // One undoable step: the tiles it changed, in the order they first changed, each with what it was and what it became.
    private sealed class Step
    {
        public long Id;
        public string Label;
        public readonly List<int> Order = new();
        public readonly Dictionary<int, (MapModel Before, MapModel After)> Tiles = new();
        // a step of what is kept beside the tiles (the map's settings): what takes it back and what does it again, and what a later one joins it by
        public Action Back, Again;
        public string Key;
        public double At;
    }

    private const double JoinSeconds = 1.0; // steps of one setting that follow each other this closely are one step (a slider dragged)

    private MapDocument(int width, int height, int levels, MapModel defaultModel)
    {
        Width = width;
        Height = height;
        Levels = levels;
        Default = defaultModel;
        _models[defaultModel.Text] = defaultModel;
        _tiles = new MapModel[checked(width * height * levels)];
        Array.Fill(_tiles, defaultModel);
    }

    /// <summary>
    /// A new map of the given size whose every tile is <paramref name="defaultMembers"/> (a member list, map-format.md 4.3); with
    /// <paramref name="upperMembers"/> the tiles of the levels above the first are those.
    /// </summary>
    public static MapDocument Create(int width, int height, int levels, string defaultMembers, string upperMembers = null)
    {
        if (width < 1 || width > MaxWidth) throw new ArgumentOutOfRangeException(nameof(width), width, "a map is 1 to " + MaxWidth + " tiles wide");
        if (height < 1 || height > MaxHeight) throw new ArgumentOutOfRangeException(nameof(height), height, "a map is 1 to " + MaxHeight + " tiles high");
        if (levels < 1 || levels > MaxLevels) throw new ArgumentOutOfRangeException(nameof(levels), levels, "a map has 1 to " + MaxLevels + " levels");
        ArgumentNullException.ThrowIfNull(defaultMembers);
        var document = new MapDocument(width, height, levels, ParseModel(defaultMembers));
        if (upperMembers != null && levels > 1)
            Array.Fill(document._tiles, document.Model(upperMembers), width * height, width * height * (levels - 1));
        return document;
    }

    /// <summary>The document of a parsed file.</summary>
    public static MapDocument From(ParsedMapText parsed)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        var document = new MapDocument(parsed.Width, parsed.Height, parsed.Levels, parsed.Default) { Source = parsed.Source };
        foreach (MapModel model in parsed.Models) document._models.TryAdd(model.Text, model);
        int[] tiles = parsed.dm_tiles;
        for (int i = 0; i < tiles.Length; i++) document._tiles[i] = parsed.Models[tiles[i]];
        return document;
    }

    /// <summary>Reads a file's text. Throws <see cref="MapTextException"/> as <see cref="MapText.Parse"/> does.</summary>
    public static MapDocument Parse(string text, string sourceName) => From(MapText.Parse(text, sourceName));

    /// <summary>The width (tiles, x).</summary>
    public int Width { get; }

    /// <summary>The height (y).</summary>
    public int Height { get; }

    /// <summary>The levels (z).</summary>
    public int Levels { get; }

    /// <summary>The <c>source</c> header line's text (informative), or null for a map that has none.</summary>
    public string Source { get; set; }

    /// <summary>The model of every tile the file has no line for.</summary>
    public MapModel Default { get; }

    /// <summary>
    /// Another empty tile for a level than the default one, given by the document's holder (the map editor: the level's base turf,
    /// docs/ai/specs/map-editor.md 5.17); null, or null for a level: the default tile.
    /// </summary>
    public Func<int, MapModel> EmptyTile { get; set; }

    /// <summary>The tile of a level with nothing on it: what a tool leaves where it takes a tile away.</summary>
    public MapModel Empty(int z) => EmptyTile?.Invoke(z) ?? Default;

    /// <summary>Counts every change of a tile (an edit, an undo, a redo): a view that caches what it drew compares it.</summary>
    public long Revision { get; private set; }

    /// <summary>The tiles that changed, after every change: one tile of an open step, or all of an <see cref="Edit"/>, an <see cref="Undo"/> or a <see cref="Redo"/>.</summary>
    public event Action<IReadOnlyList<MapTileChange>> Changed;

    /// <summary>True when (x, y, z) is a tile of the map (from 1).</summary>
    public bool Contains(int x, int y, int z) => x >= 1 && y >= 1 && z >= 1 && x <= Width && y <= Height && z <= Levels;

    /// <summary>The model of the tile (x, y, z), from 1. Throws outside the map.</summary>
    public MapModel this[int x, int y, int z] => _tiles[IndexOf(x, y, z)];

    /// <summary>
    /// The document's model of a member list (map-format.md 4.3: <c>/obj/item/crowbar{pixel_x = -4} | /turf/open/floor/iron | /area/space</c>),
    /// the same object for the same text. Throws <see cref="MapTextException"/> for a text the format does not allow.
    /// </summary>
    public MapModel Model(string members)
    {
        ArgumentNullException.ThrowIfNull(members);
        if (_models.TryGetValue(members, out MapModel known)) return known;
        MapModel model = ParseModel(members);
        _models[members] = model;
        return model;
    }

    /// <summary>The document's model of these members, in this order.</summary>
    public MapModel Model(IReadOnlyList<MapMember> members) => Model(TextOf(members));

    /// <summary>The text of a member list as the file holds it: the members joined by <c> | </c>, each with its overrides as <c>{var = value; var = value}</c>.</summary>
    public static string TextOf(IReadOnlyList<MapMember> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        var text = new StringBuilder();
        for (int i = 0; i < members.Count; i++)
        {
            if (i > 0) text.Append(" | ");
            text.Append(members[i].Path);
            IReadOnlyList<MapOverride> overrides = members[i].Overrides;
            if (overrides == null || overrides.Count == 0) continue;
            text.Append('{');
            for (int k = 0; k < overrides.Count; k++)
            {
                if (k > 0) text.Append("; ");
                text.Append(overrides[k].Var).Append(" = ").Append(overrides[k].Value.Literal);
            }
            text.Append('}');
        }
        return text.ToString();
    }

    private static MapModel ParseModel(string members)
    {
        // the file's line rules (map-format.md 4): one line, no tab, no space at the end; the reader checks the rest
        if (members.Length == 0 || members[^1] == ' ' || members.AsSpan().IndexOfAny('\n', '\r', '\t') >= 0)
            throw new MapTextException(MembersSource, 1, "a member list is one line with no tab and no space at its end");
        return new MapModel { Members = MapTextReader.ParseMembers(members, MembersSource, 1), Text = members };
    }

    // ---- steps ----

    /// <summary>True between <see cref="Begin"/> and <see cref="End"/>.</summary>
    public bool InStep => _open != null;

    /// <summary>Opens a step: the tiles set until <see cref="End"/> are undone together. <paramref name="label"/> names it for the interface ("Place wall").</summary>
    public void Begin(string label)
    {
        if (_open != null) throw new InvalidOperationException("a step is already open: " + _open.Label);
        _open = new Step { Label = label ?? string.Empty };
    }

    /// <summary>Gives the tile (x, y, z) this model, inside an open step. False when the tile already has it.</summary>
    public bool Set(int x, int y, int z, MapModel model)
    {
        if (_open == null) throw new InvalidOperationException("no step is open (Begin, or Edit)");
        ArgumentNullException.ThrowIfNull(model);
        int index = IndexOf(x, y, z);
        model = Intern(model);
        MapModel before = _tiles[index];
        if (ReferenceEquals(before, model)) return false;
        _tiles[index] = model;
        if (_open.Tiles.TryGetValue(index, out (MapModel Before, MapModel After) entry))
        {
            _open.Tiles[index] = (entry.Before, model);
        }
        else
        {
            _open.Tiles[index] = (before, model);
            _open.Order.Add(index);
        }
        Revision++;
        _pending.Add(new MapTileChange(x, y, z, before, model));
        if (!_batch) Flush();
        return true;
    }

    /// <summary>Closes the open step. False when it changed nothing in the end (it then leaves no trace in the history).</summary>
    public bool End()
    {
        if (_open == null) throw new InvalidOperationException("no step is open");
        Step step = _open;
        _open = null;
        step.Order.RemoveAll(index => ReferenceEquals(step.Tiles[index].Before, step.Tiles[index].After)); // set back to what it was
        if (step.Order.Count == 0) return false;
        step.Id = _nextStepId++;
        Push(step);
        return true;
    }

    private void Push(Step step)
    {
        _redo.Clear();
        _undo.Add(step);
        if (_undo.Count > HistoryLimit)
        {
            _baseId = _undo[0].Id;
            _undo.RemoveAt(0);
        }
    }

    /// <summary>
    /// A step of something kept beside the tiles (the map's settings): it stands in the history among the tiles' steps, and <paramref name="undo"/>
    /// or <paramref name="redo"/> is called when it is taken back or done again. With a <paramref name="key"/>, a step that follows the last one
    /// of the same key within a second by the caller's clock <paramref name="now"/> joins it (a slider dragged is one step: its first undo and its
    /// last redo hold), unless the document was saved between them.
    /// </summary>
    public void Record(string label, Action undo, Action redo, string key = null, double now = 0)
    {
        if (_open != null) throw new InvalidOperationException("a step is open: " + _open.Label);
        ArgumentNullException.ThrowIfNull(undo);
        ArgumentNullException.ThrowIfNull(redo);
        if (key != null && _redo.Count == 0 && _undo.Count > 0 && _undo[^1] is { Back: not null } last && last.Key == key && last.Id != _savedId
            && Math.Abs(now - last.At) < JoinSeconds)
        {
            (last.Again, last.At) = (redo, now);
            return;
        }
        Push(new Step { Id = _nextStepId++, Label = label ?? string.Empty, Back = undo, Again = redo, Key = key, At = now });
    }

    /// <summary>
    /// One whole step: sets every given tile and raises <see cref="Changed"/> once. False when nothing changed. A tile outside the map
    /// throws and leaves the document as it was.
    /// </summary>
    public bool Edit(string label, IEnumerable<(int X, int Y, int Z, MapModel Model)> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        Begin(label);
        _batch = true;
        try
        {
            foreach ((int x, int y, int z, MapModel model) in tiles) Set(x, y, z, model);
        }
        catch
        {
            foreach (int index in _open.Order) _tiles[index] = _open.Tiles[index].Before;
            _pending.Clear();
            _open = null;
            throw;
        }
        finally
        {
            _batch = false;
        }
        bool changed = End();
        Flush();
        return changed;
    }

    /// <summary>True when there is a step to take back.</summary>
    public bool CanUndo => _open == null && _undo.Count > 0;

    /// <summary>True when there is an undone step to do again.</summary>
    public bool CanRedo => _open == null && _redo.Count > 0;

    /// <summary>The label of the step <see cref="Undo"/> would take back, or null.</summary>
    public string UndoLabel => _undo.Count > 0 ? _undo[^1].Label : null;

    /// <summary>The label of the step <see cref="Redo"/> would do again, or null.</summary>
    public string RedoLabel => _redo.Count > 0 ? _redo[^1].Label : null;

    /// <summary>Takes back the last step. False when there is none.</summary>
    public bool Undo()
    {
        if (_open != null) throw new InvalidOperationException("a step is open: " + _open.Label);
        if (_undo.Count == 0) return false;
        Step step = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        for (int k = step.Order.Count - 1; k >= 0; k--)
        {
            int index = step.Order[k];
            (MapModel before, MapModel after) = step.Tiles[index];
            _tiles[index] = before;
            _pending.Add(ChangeOf(index, after, before));
        }
        step.Back?.Invoke();
        _redo.Add(step);
        Revision++;
        Flush();
        return true;
    }

    /// <summary>Does the last undone step again. False when there is none.</summary>
    public bool Redo()
    {
        if (_open != null) throw new InvalidOperationException("a step is open: " + _open.Label);
        if (_redo.Count == 0) return false;
        Step step = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        foreach (int index in step.Order)
        {
            (MapModel before, MapModel after) = step.Tiles[index];
            _tiles[index] = after;
            _pending.Add(ChangeOf(index, before, after));
        }
        step.Again?.Invoke();
        _undo.Add(step);
        Revision++;
        Flush();
        return true;
    }

    /// <summary>True when the tiles, or what <see cref="Record"/> keeps beside them, differ from what <see cref="MarkSaved"/> last saw (a new or a read document counts as saved).</summary>
    public bool IsDirty => _open is { Order.Count: > 0 } || StateId != _savedId;

    /// <summary>The document was written to its file as it is now.</summary>
    public void MarkSaved() => _savedId = StateId;

    private long StateId => _undo.Count > 0 ? _undo[^1].Id : _baseId;

    private MapModel Intern(MapModel model)
    {
        if (_models.TryGetValue(model.Text, out MapModel known)) return known;
        _models[model.Text] = model;
        return model;
    }

    private void Flush()
    {
        if (_pending.Count == 0) return;
        MapTileChange[] changes = _pending.ToArray();
        _pending.Clear();
        Changed?.Invoke(changes);
    }

    // x fastest, then y, then z: the reader's order (ParsedMapText.dm_tiles).
    private int IndexOf(int x, int y, int z)
    {
        if (!Contains(x, y, z))
            throw new ArgumentOutOfRangeException(nameof(x), $"({x}, {y}, {z}) is outside the map's size {Width} x {Height} x {Levels}");
        return ((z - 1) * Height + (y - 1)) * Width + (x - 1);
    }

    private MapTileChange ChangeOf(int index, MapModel before, MapModel after)
        => new(index % Width + 1, index / Width % Height + 1, index / (Width * Height) + 1, before, after);

    // ---- the file (map-format.md 4) ----

    /// <summary>
    /// The file's text: the header, then the chunks by z, y, x, each with its lines by y decreasing and x increasing; a tile that
    /// has the default model and a chunk with no line are not written.
    /// </summary>
    public string ToText()
    {
        if (Source != null && Source.AsSpan().IndexOfAny('\n', '\r') >= 0) throw new InvalidOperationException("the source line holds a line end");
        const int C = MapText.ChunkSize;
        var text = new StringBuilder(1 << 16);
        text.Append("anomaly-map ").Append(MapText.FormatVersion).Append('\n');
        if (!string.IsNullOrEmpty(Source)) text.Append("source ").Append(Source).Append('\n');
        text.Append("size ").Append(Width).Append(' ').Append(Height).Append(' ').Append(Levels).Append('\n');
        text.Append("chunk ").Append(C).Append('\n');
        text.Append("default ").Append(Default.Text).Append('\n');
        for (int z = 1; z <= Levels; z++)
        {
            for (int cy = 1; cy <= Height; cy += C)
            {
                for (int cx = 1; cx <= Width; cx += C)
                {
                    bool header = false;
                    for (int y = Math.Min(cy + C - 1, Height); y >= cy; y--)
                    {
                        int row = ((z - 1) * Height + (y - 1)) * Width;
                        for (int x = cx; x <= Math.Min(cx + C - 1, Width); x++)
                        {
                            MapModel model = _tiles[row + x - 1];
                            if (ReferenceEquals(model, Default)) continue;
                            if (!header)
                            {
                                text.Append('\n').Append("[chunk ").Append(cx).Append(' ').Append(cy).Append(' ').Append(z).Append("]\n");
                                header = true;
                            }
                            text.Append(x).Append(',').Append(y).Append(": ").Append(model.Text).Append('\n');
                        }
                    }
                }
            }
        }
        return text.ToString();
    }
}
