using Anomaly.Client.Shared.Models;
using Anomaly.Client.Shared.World;
using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// The entities of a <see cref="FakeMap"/> as a join's Snapshot carries them: what the scripted server and the map editor's feed
/// send for a map that has no world behind it.
/// </summary>
public static class FakeMapWire
{
    /// <summary>
    /// Every entity of <paramref name="file"/> with its wire state, in the map's order. DV56, as the server does once its map is loaded: the
    /// owner's map patches move the things they name, a thing whose model's catalog rule says so faces away from the wall behind it, then the
    /// patches turn the things they name. A type the list has not got is added to <paramref name="types"/>.
    /// </summary>
    public static List<(FakeMapEntity Source, EntityState Wire)> Entities(FakeMap file, FakeMapBinding binding, List<string> types)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(types);
        MapGrid grid = binding.Grid;
        ushort[] cells = grid.ExpandCells();
        int width = file.Width, height = file.Height;
        bool Closed(int x, int y, int z) => x >= 1 && y >= 1 && x <= width && y <= height
            && grid.TurfTypes[cells[(x - 1) + (y - 1) * width + (z - 1) * width * height]].StartsWith("/turf/closed", StringComparison.Ordinal);
        ModelCatalog catalog = LoadCatalog();
        var typeIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < types.Count; i++)
            typeIndex.TryAdd(types[i], i);

        var entities = new List<(FakeMapEntity Source, EntityState Wire)>(file.Entities.Count);
        for (int i = 0; i < file.Entities.Count; i++)
        {
            FakeMapEntity e = file.Entities[i];
            // (as the server: a thing whose map says the way it faces keeps it, DV508)
            if (!e.DirSet && catalog?.Find(e.Icon, e.IconState) is { AwayFromWall: true })
            {
                FakeMapEntity at = e;
                e = e with { Dir = ModelRules.FaceAwayFromWall(d => Closed(at.X + (d == 4 ? 1 : d == 8 ? -1 : 0), at.Y + (d == 1 ? 1 : d == 2 ? -1 : 0), at.Z)) };
            }
            if (!typeIndex.TryGetValue(e.TypePath, out int type))
            {
                type = types.Count;
                types.Add(e.TypePath);
                typeIndex[e.TypePath] = type;
            }
            string name = e.Name ?? string.Empty;
            if (name.Length > 100)
                name = name.Substring(0, 100);
            var wire = new EntityState(FakeServer.WireId(e), type, e.X, e.Y, e.Z, e.Dir, name, 0f, 0)
            {
                PosX = e.X * MotionMath.SubunitsPerTile,
                PosY = e.Y * MotionMath.SubunitsPerTile,
                Appearance = binding.EntityAppearances[i],
                Light = e.Light,
            };
            entities.Add((e, wire));
        }
        return entities;
    }

    // The model catalog, or null when the project has none to find or it cannot be read (the things then keep the way the map gave them).
    private static ModelCatalog LoadCatalog()
    {
        string path = ModelCatalog.Locate();
        if (path == null)
            return null;
        try
        {
            return ModelCatalog.Parse(System.IO.File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is System.IO.IOException or System.IO.InvalidDataException or System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
