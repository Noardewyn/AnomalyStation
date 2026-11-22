using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Icons;

// Owner: M2 P7. The turf kit (D104; scope 2j A.4): walls are boxes of 2 x 2 x 2 m (D119; M2 had 3 m) carrying the turf's own icon state on every face
// (the 32 x 32 sprite covers 2 x 2 m, once), floors are tiles with their icon state, the ceiling is plating darkened. This is the engine-free part: which cells draw what, grouped by picture so that the client makes one mesh per picture.

/// <summary>The cells that share a kind and a picture: the renderer makes one multimesh of them.</summary>
public sealed class TurfGroup
{
    /// <summary>Wall (a box) or floor (a tile).</summary>
    public TurfKind Kind { get; init; }

    /// <summary>The cells' turf appearance (the public table), none when the server sent none.</summary>
    public TableRef Appearance { get; init; }

    /// <summary>The index into <c>MapGrid.TurfTypes</c> when there is no appearance (the group is then tinted by the type, M1's look), else -1.</summary>
    public int TypeIndex { get; init; } = -1;

    /// <summary>The turf type of the group's first cell (the tint when the picture's icons are missing).</summary>
    public int FirstType { get; init; }

    /// <summary>The cells (BYOND x, y, z from 1), in map order.</summary>
    public List<(int X, int Y, int Z)> Cells { get; } = new();
}

/// <summary>Plans the turf kit of a map.</summary>
public static class TurfKitPlan
{
    /// <summary>Metres one turf picture covers (a 32 pixel sprite is one 2 m tile).</summary>
    public const double PictureMetres = 2.0;

    /// <summary>How many times a wall's picture repeats over its height (the wall height over 2 m per picture): 1 at D119's 2 m, so one square sprite per face.</summary>
    public static double WallVerticalRepeats => WorldScale.WallHeight / PictureMetres;

    /// <summary>
    /// Groups the map's cells. A cell draws as <see cref="TurfStyle.Classify"/> says (open space draws nothing); its picture is
    /// <paramref name="appearanceOf"/> (a cell without one is grouped by its turf type and drawn with M1's tint).
    /// </summary>
    public static List<TurfGroup> Build(MapGrid map, Func<int, int, int, TableRef> appearanceOf)
    {
        ushort[] cells = map.ExpandCells();
        var kinds = new TurfKind[map.TurfTypes.Count];
        for (int i = 0; i < kinds.Length; i++)
            kinds[i] = TurfStyle.Classify(map.TurfTypes[i]);

        var groups = new List<TurfGroup>();
        var byKey = new Dictionary<(TurfKind, uint, int), TurfGroup>();
        int index = 0;
        for (int z = 1; z <= map.Levels; z++)
        {
            for (int y = 1; y <= map.Height; y++)
            {
                for (int x = 1; x <= map.Width; x++, index++)
                {
                    int type = cells[index];
                    if (type >= kinds.Length || kinds[type] == TurfKind.Void)
                        continue;
                    TableRef appearance = appearanceOf == null ? TableRef.None : appearanceOf(x, y, z);
                    var key = (kinds[type], appearance.Index, appearance.IsNone ? type : -1);
                    if (!byKey.TryGetValue(key, out TurfGroup group))
                    {
                        group = new TurfGroup { Kind = kinds[type], Appearance = appearance, TypeIndex = key.Item3, FirstType = type };
                        byKey[key] = group;
                        groups.Add(group);
                    }
                    group.Cells.Add((x, y, z));
                }
            }
        }
        return groups;
    }
}
