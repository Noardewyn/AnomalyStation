using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// A tiny map drawn as text for the M3 R3 tests: row 0 of the text is the TOP (the highest y), a row of n lines is y = n .. 1. <c>#</c> is a closed wall, <c>.</c> an open floor, a space is space;
/// every other character is a floor with the things <paramref name="things"/> names for it (<c>'W'</c>: a full-tile window, <c>'A'</c>: an airlock, ...). A thing whose type is in
/// <paramref name="dense"/> also sets the cell's solidity bit, as tg's dense machines (a hydroponics tray) do in the real map's grid.
/// </summary>
internal sealed class AsciiWorld
{
    public const string Window = "/obj/structure/window/fulltile", Reinforced = "/obj/structure/window/reinforced/fulltile", Grille = "/obj/structure/grille",
        Airlock = "/obj/machinery/door/airlock", PodDoor = "/obj/machinery/door/poddoor/shutters", Firelock = "/obj/machinery/door/firedoor", Tray = "/obj/machinery/hydroponics/constructable";

    public static readonly Dictionary<char, string[]> DefaultThings = new()
    {
        ['W'] = new[] { Window },
        ['R'] = new[] { Reinforced },
        ['G'] = new[] { Grille },
        ['D'] = new[] { Window, Grille },
        ['A'] = new[] { Airlock },
        ['P'] = new[] { PodDoor },
        ['F'] = new[] { Firelock },
        ['T'] = new[] { Tray },
    };

    public readonly MapGrid Grid;
    public readonly List<PlanEntity> Entities = new();
    public readonly WorldPlan Plan;
    public readonly int Width, Height;

    public AsciiWorld(string[] rows, Dictionary<char, string[]> things = null, params string[] dense)
    {
        things ??= DefaultThings;
        Height = rows.Length;
        Width = rows.Max(r => r.Length);
        Grid = new MapGrid { Width = Width, Height = Height, Levels = 1 };
        Grid.TurfTypes.Add("/turf/open/floor/iron");
        Grid.TurfTypes.Add("/turf/closed/wall");
        Grid.TurfTypes.Add("/turf/open/space/basic");
        var solid = new byte[(Width * Height + 7) / 8];
        var densePaths = new HashSet<string>(dense.Length == 0 ? new[] { Tray } : dense);
        long id = 0;
        for (int y = 1; y <= Height; y++)
        {
            string row = rows[Height - y];
            for (int x = 1; x <= Width; x++)
            {
                char c = x <= row.Length ? row[x - 1] : ' ';
                int type = c == '#' ? 1 : c == ' ' ? 2 : 0;
                Grid.Runs.Add(new TurfRun(1, type));
                if (type == 1)
                    SetSolid(solid, x, y);
                if (things.TryGetValue(c, out string[] paths))
                {
                    foreach (string path in paths)
                    {
                        id++;
                        Entities.Add(new PlanEntity(id, path, x, y, 1, 2, TableRef.Public((uint)id), 0, 0, LightState.None, null));
                        if (densePaths.Contains(path) || path.Contains("window", StringComparison.Ordinal) || path == Grille)
                            SetSolid(solid, x, y);
                    }
                }
            }
        }
        Grid.SolidBits = solid;
        Plan = ChunkPlanner.Plan(Grid, null, Entities);
    }

    private void SetSolid(byte[] solid, int x, int y)
    {
        int i = (x - 1) + (y - 1) * Width;
        solid[i >> 3] |= (byte)(1 << (i & 7));
    }

    /// <summary>The pieces and billboards placed on the cell.</summary>
    public IEnumerable<Placed> At(int x, int y) => Plan.Chunks.SelectMany(c => c.Placed).Where(p => p.X == x && p.Y == y);

    /// <summary>The one piece of the shape on the cell.</summary>
    public Placed Piece(int x, int y, RenderKind shape) => At(x, y).Single(p => p.Kind == PlacedKind.Piece && p.Shape == shape);
}
