// Engine part: rust-g's map generation functions (code/__DEFINES/rust_g.dm), written natively in C# (docs/ai/translation-rules.md 13),
// D304. Sources, rust-g 7.0.0 (tg's dependencies.sh; MIT, Copyright (c) 2022 Bjorn Neergaard, rust-g contributors):
// src/cave_system_generator.rs and src/dbpnoise.rs, with the dbpnoise crate 0.1.2 (MIT, thisisedge0@gmail.com) it calls.
// rust-g draws from its own generators (rand::rng(), a Pcg32 seeded from the seed string); here each call runs a stream of the
// kernel's Rng (D31): the cave from one draw of the world's stream, the noise from the seed string alone, as dbpnoise's is.
using System.Globalization;
using System.Text;
using System.Text.Json;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/rust_g.dm:121  #define rustg_cave_system_generator_generate(width, height, prefabs_json, min_bsp_size, max_ratio, padding, room_fill_percent, corridor_width, loop_percent, noise_percent, ca_steps, birth_limit, survival_limit, edge_is_alive)
    /// <summary>
    /// rust-g's <c>cave_system_generator_generate</c>: a BSP of rooms joined by a minimum spanning tree of corridors (with some
    /// loops), noise in the rest, cellular automaton smoothing, then the pockets unreachable from the first room filled.
    /// The result is <c>width * height</c> characters, row by row from y 0 (<c>'1'</c> floor, <c>'0'</c> wall), or null when
    /// the width, height or prefab json does not parse (rust-g's <c>.ok()</c>).
    /// </summary>
    public static string rustg_cave_system_generator_generate(object width, object height, object prefabs_json, object min_bsp_size, object max_ratio,
        object padding, object room_fill_percent, object corridor_width, object loop_percent, object noise_percent, object ca_steps, object birth_limit,
        object survival_limit, object edge_is_alive)
    {
        var cfg = new CaveConfig(
            UParse(min_bsp_size, 20), FParse(max_ratio, 2.5), UParse(padding, 2), Math.Clamp(UParse(room_fill_percent, 80) / 100.0, 0, 1),
            Math.Max(UParse(corridor_width, 1), 1), UParse(loop_percent, 15), UParse(noise_percent, 48), UParse(ca_steps, 6), UParse(birth_limit, 5),
            UParse(survival_limit, 4), (byte.TryParse(Str(edge_is_alive), NumberStyles.None, CultureInfo.InvariantCulture, out byte e) ? e : 0) != 0);
        if (!int.TryParse(Str(width), NumberStyles.None, CultureInfo.InvariantCulture, out int w)
            || !int.TryParse(Str(height), NumberStyles.None, CultureInfo.InvariantCulture, out int h))
            return null;
        List<(int X, int Y, int W, int H, bool Enclosed)> prefabs;
        try
        {
            prefabs = ParsePrefabs(Str(prefabs_json));
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException or OverflowException)
        {
            return null;
        }
        if (w == 0 || h == 0)
            return "";
        return CaveSystem.Generate(w, h, prefabs, cfg, new Rng(World.Current.Rng.NextUInt64()));
    }

    // tg: code/__DEFINES/rust_g.dm:153  #define rustg_dbp_generate(seed, accuracy, stamp_size, world_size, lower_range, upper_range)
    /// <summary>
    /// rust-g's <c>dbp_generate</c> (dbpnoise's <c>gen_noise</c>): perlin-like noise from <c>accuracy</c> precomputed gradient
    /// stamps, true where the value is in [lower, upper). The result is <c>world_size</c> squared characters; dbpnoise's
    /// outer index is x, so character <c>x * world_size + y</c> is cell (x, y), as rust-g writes it. Null when a number does
    /// not parse.
    /// </summary>
    public static string rustg_dbp_generate(object seed, object accuracy, object stamp_size, object world_size, object lower_range, object upper_range)
    {
        if (!int.TryParse(Str(accuracy), NumberStyles.None, CultureInfo.InvariantCulture, out int acc)
            || !int.TryParse(Str(stamp_size), NumberStyles.None, CultureInfo.InvariantCulture, out int stamp)
            || !int.TryParse(Str(world_size), NumberStyles.None, CultureInfo.InvariantCulture, out int size)
            || !float.TryParse(Str(lower_range), NumberStyles.Float, CultureInfo.InvariantCulture, out float lower)
            || !float.TryParse(Str(upper_range), NumberStyles.Float, CultureInfo.InvariantCulture, out float upper))
            return null;
        bool[,] map = DbpNoise.Generate(Str(seed), acc, stamp, size, lower, upper);
        var sb = new StringBuilder(size * size);
        for (int x = 0; x < map.GetLength(0); x++)
            for (int y = 0; y < map.GetLength(1); y++)
                sb.Append(map[x, y] ? '1' : '0');
        return sb.ToString();
    }

    private static string Str(object value) => Anomaly.Core.DmShim.Dm.text("[]", value);

    // Rust's str::parse::<usize>().unwrap_or(d)
    private static int UParse(object value, int fallback)
        => int.TryParse(Str(value), NumberStyles.None, CultureInfo.InvariantCulture, out int v) ? v : fallback;

    private static double FParse(object value, double fallback)
        => double.TryParse(Str(value), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : fallback;

    private static List<(int, int, int, int, bool)> ParsePrefabs(string json)
    {
        var result = new List<(int, int, int, int, bool)>();
        if (json.Length == 0 || json == "[]")
            return result;
        using JsonDocument doc = JsonDocument.Parse(json);
        foreach (JsonElement p in doc.RootElement.EnumerateArray())
        {
            bool enclosed = p.TryGetProperty("isEnclosed", out JsonElement e) && e.GetByte() != 0;
            result.Add((p.GetProperty("x").GetInt32(), p.GetProperty("y").GetInt32(), p.GetProperty("w").GetInt32(), p.GetProperty("h").GetInt32(), enclosed));
        }
        return result;
    }

    internal readonly record struct CaveConfig(int MinBspSize, double MaxRatio, int Padding, double SizeScale, int CorridorWidth, int LoopPercent,
        int NoisePercent, int CaSteps, int BirthLimit, int SurvivalLimit, bool EdgeIsAlive);

    /// <summary>rust-g src/cave_system_generator.rs, step by step.</summary>
    internal static class CaveSystem
    {
        private const byte DEAD = 0; // Dynamic wall
        private const byte ALIVE = 1; // Dynamic floor
        private const byte DEF_ALIVE = 2; // Static floor (doesn't change during CA)
        private const byte DEF_DEAD = 3; // Static wall/indestructible (doesn't change during CA)

        private sealed class Node(int x, int y, int w, int h)
        {
            public readonly int X = x, Y = y, W = w, H = h;
            public Node Left, Right;
            public Room? Room;
        }

        private readonly record struct Room(int X, int Y, int W, int H, int Cx, int Cy);

        private readonly record struct Edge(int U, int V, double Dist);

        // rand's Uniform::new(lo, hi) over usize: [lo, hi)
        private static int Uniform(Rng rng, int lo, int hi) => lo + (int)(rng.NextUInt64() % (ulong)(hi - lo));

        private static bool Bernoulli(Rng rng, double p) => (rng.NextUInt64() >> 11) * (1.0 / (1UL << 53)) < p;

        public static string Generate(int width, int height, List<(int X, int Y, int W, int H, bool Enclosed)> prefabs, CaveConfig cfg, Rng rng)
        {
            var grid = new byte[width, height];
            var fix = new bool[width, height];

            // Step 1: Apply prefabs first
            foreach (var prefab in prefabs)
                ApplyPrefab(grid, fix, prefab, width, height);

            // Step 2: BSP partitioning
            var root = new Node(0, 0, width, height);
            Split(root, cfg.MinBspSize, cfg.MaxRatio, rng);
            var leaves = new List<Node>();
            CollectLeaves(root, leaves);
            if (leaves.Count == 0)
                leaves.Add(new Node(0, 0, width, height));

            // Step 3: Create rooms in each leaf
            foreach (Node leaf in leaves)
                leaf.Room = GenerateRoom(leaf, cfg.Padding, cfg.SizeScale, rng);

            // Step 4: Adjacency edges + Kruskal MST
            List<Edge> mst = KruskalMst(leaves.Count, AdjacencyEdges(leaves), cfg.LoopPercent, rng);

            // Step 5: Apply rooms to grid as DEF_ALIVE, skipping prefab-fixed cells
            foreach (Node leaf in leaves)
            {
                if (leaf.Room is not Room room)
                    continue;
                for (int dx = 0; dx < room.W; dx++)
                    for (int dy = 0; dy < room.H; dy++)
                    {
                        int gx = room.X + dx, gy = room.Y + dy;
                        if (gx < width && gy < height && !fix[gx, gy])
                        {
                            grid[gx, gy] = DEF_ALIVE;
                            fix[gx, gy] = true;
                        }
                    }
            }

            // Step 6: Carve corridors between rooms along edges
            foreach (Edge edge in mst)
            {
                if (leaves[edge.U].Room is Room ra && leaves[edge.V].Room is Room rb)
                    CarveCorridor(grid, fix, (ra.Cx, ra.Cy), (rb.Cx, rb.Cy), cfg.CorridorWidth, rng);
            }

            // Step 7: Apply noise only to unfixed cells
            double prob = Math.Clamp(cfg.NoisePercent / 100.0, 0, 1);
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (!fix[x, y])
                        grid[x, y] = Bernoulli(rng, prob) ? ALIVE : DEAD;

            // Step 8: CA smoothing
            for (int i = 0; i < cfg.CaSteps; i++)
                grid = CaStep(grid, width, height, cfg.BirthLimit, cfg.SurvivalLimit, cfg.EdgeIsAlive);

            // Step 9: BFS flood-fill island removal from first room center
            foreach (Node leaf in leaves)
            {
                if (leaf.Room is Room first)
                {
                    FloodFillIslandRemoval(grid, width, height, (first.Cx, first.Cy));
                    break;
                }
            }

            var sb = new StringBuilder(width * height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    sb.Append(grid[x, y] is ALIVE or DEF_ALIVE ? '1' : '0');
            return sb.ToString();
        }

        private static void ApplyPrefab(byte[,] grid, bool[,] fix, (int X, int Y, int W, int H, bool Enclosed) prefab, int width, int height)
        {
            int px = Math.Max(prefab.X - 1, 0), py = Math.Max(prefab.Y - 1, 0);
            int pw = Math.Min(prefab.W, Math.Max(width - px, 0)), ph = Math.Min(prefab.H, Math.Max(height - py, 0));
            for (int dy = 0; dy < ph; dy++)
                for (int dx = 0; dx < pw; dx++)
                {
                    int gx = px + dx, gy = py + dy;
                    if (gx < width && gy < height)
                    {
                        grid[gx, gy] = prefab.Enclosed ? DEF_DEAD : DEF_ALIVE;
                        fix[gx, gy] = true;
                    }
                }
        }

        private static void Split(Node node, int minSize, double maxRatio, Rng rng)
        {
            bool canSplitH = node.H > minSize * 2;
            bool canSplitV = node.W > minSize * 2;
            if (!canSplitH && !canSplitV)
                return;
            bool splitHorizontal = Bernoulli(rng, 0.5); // true = split by Y
            if (node.H > 0 && (double)node.W / node.H >= maxRatio)
                splitHorizontal = false; // too wide: split by X
            if (node.W > 0 && (double)node.H / node.W >= maxRatio)
                splitHorizontal = true; // too tall: split by Y
            if (splitHorizontal && !canSplitH)
                splitHorizontal = false;
            else if (!splitHorizontal && !canSplitV)
                splitHorizontal = true;
            if (splitHorizontal && !canSplitH)
                return;
            if (!splitHorizontal && !canSplitV)
                return;
            if (splitHorizontal)
            {
                int splitY = Uniform(rng, minSize, node.H - minSize);
                node.Left = new Node(node.X, node.Y, node.W, splitY);
                node.Right = new Node(node.X, node.Y + splitY, node.W, node.H - splitY);
            }
            else
            {
                int splitX = Uniform(rng, minSize, node.W - minSize);
                node.Left = new Node(node.X, node.Y, splitX, node.H);
                node.Right = new Node(node.X + splitX, node.Y, node.W - splitX, node.H);
            }
            Split(node.Left, minSize, maxRatio, rng);
            Split(node.Right, minSize, maxRatio, rng);
        }

        private static void CollectLeaves(Node node, List<Node> leaves)
        {
            if (node.Left == null && node.Right == null)
            {
                leaves.Add(new Node(node.X, node.Y, node.W, node.H));
                return;
            }
            if (node.Left != null)
                CollectLeaves(node.Left, leaves);
            if (node.Right != null)
                CollectLeaves(node.Right, leaves);
        }

        private static Room? GenerateRoom(Node leaf, int padding, double sizeScale, Rng rng)
        {
            int maxW = Math.Max(leaf.W - padding * 2, 0), maxH = Math.Max(leaf.H - padding * 2, 0);
            if (maxW < 3 || maxH < 3)
                return null;
            int Side(int max)
            {
                int lo = Math.Max((int)(max * 0.3), 1), hi = (int)(max * sizeScale);
                return Math.Min(Math.Max(hi > lo ? Uniform(rng, lo, hi) : lo, 3), max);
            }
            int rw = Side(maxW);
            int rh = Side(maxH);
            int Offset(int span, int size)
            {
                int hi = Math.Max(span - (size + padding), 0);
                return hi > padding ? Uniform(rng, padding, hi) : padding;
            }
            int rx = leaf.X + Offset(leaf.W, rw);
            int ry = leaf.Y + Offset(leaf.H, rh);
            return new Room(rx, ry, rw, rh, rx + rw / 2, ry + rh / 2);
        }

        private static List<Edge> AdjacencyEdges(List<Node> leaves)
        {
            var edges = new List<Edge>();
            for (int i = 0; i < leaves.Count; i++)
                for (int j = i + 1; j < leaves.Count; j++)
                {
                    if (!Adjacent(leaves[i], leaves[j]) || leaves[i].Room is not Room ra || leaves[j].Room is not Room rb)
                        continue;
                    double dx = ra.Cx - rb.Cx, dy = ra.Cy - rb.Cy;
                    edges.Add(new Edge(i, j, Math.Sqrt(dx * dx + dy * dy)));
                }
            return edges;
        }

        private static bool Adjacent(Node a, Node b)
        {
            int aRight = a.X + a.W, aBottom = a.Y + a.H, bRight = b.X + b.W, bBottom = b.Y + b.H;
            return ((a.X == bRight || b.X == aRight) && !(a.Y >= bBottom || b.Y >= aBottom))
                || ((a.Y == bBottom || b.Y == aBottom) && !(a.X >= bRight || b.X >= aRight));
        }

        private static List<Edge> KruskalMst(int n, List<Edge> edges, int loopPercent, Rng rng)
        {
            var sorted = new List<Edge>(edges);
            sorted.Sort((a, b) => a.Dist.CompareTo(b.Dist)); // a stable sort, as Rust's sort_by
            for (int i = 1; i < sorted.Count; i++) // List.Sort is not stable: restore the input order among equal distances
            {
                for (int k = i; k > 0 && sorted[k - 1].Dist == sorted[k].Dist && Order(sorted[k - 1]) > Order(sorted[k]); k--)
                    (sorted[k - 1], sorted[k]) = (sorted[k], sorted[k - 1]);
            }
            int[] parent = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Range(0, n));
            int Find(int x)
            {
                while (parent[x] != x)
                    x = parent[x];
                return x;
            }
            double loop = Math.Clamp(loopPercent / 100.0, 0, 1);
            var result = new List<Edge>(sorted.Count);
            foreach (Edge edge in sorted)
            {
                int ru = Find(edge.U), rv = Find(edge.V);
                if (ru != rv)
                {
                    parent[ru] = rv;
                    result.Add(edge);
                }
                else if (Bernoulli(rng, loop))
                {
                    result.Add(edge);
                }
            }
            return result;
            static long Order(Edge e) => ((long)e.U << 32) | (uint)e.V;
        }

        private static void CarveCorridor(byte[,] grid, bool[,] fix, (int X, int Y) start, (int X, int Y) end, int cw, Rng rng)
        {
            bool xFirst = Bernoulli(rng, 0.5);
            int cx = start.X, cy = start.Y;
            for (int leg = 0; leg < 2; leg++)
            {
                if (xFirst == (leg == 0))
                {
                    while (cx != end.X)
                    {
                        cx += Math.Sign(end.X - cx);
                        Paint(grid, fix, cx, cy, cw);
                    }
                }
                else
                {
                    while (cy != end.Y)
                    {
                        cy += Math.Sign(end.Y - cy);
                        Paint(grid, fix, cx, cy, cw);
                    }
                }
            }
        }

        private static void Paint(byte[,] grid, bool[,] fix, int cx, int cy, int cw)
        {
            for (int i = 0; i < cw; i++)
                for (int j = 0; j < cw; j++)
                {
                    int nx = cx + i, ny = cy + j;
                    if (nx >= 0 && ny >= 0 && nx < grid.GetLength(0) && ny < grid.GetLength(1) && grid[nx, ny] != DEF_DEAD)
                    {
                        grid[nx, ny] = DEF_ALIVE;
                        fix[nx, ny] = true;
                    }
                }
        }

        private static byte[,] CaStep(byte[,] grid, int width, int height, int birth, int survival, bool edgeAlive)
        {
            var next = new byte[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    byte cell = grid[x, y];
                    if (cell is DEF_ALIVE or DEF_DEAD)
                    {
                        next[x, y] = cell;
                        continue;
                    }
                    int count = 0;
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0)
                                continue;
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                                count += grid[nx, ny] is ALIVE or DEF_ALIVE ? 1 : 0;
                            else if (edgeAlive)
                                count++;
                        }
                    next[x, y] = cell == ALIVE ? (count >= survival ? ALIVE : DEAD) : (count >= birth ? ALIVE : DEAD);
                }
            return next;
        }

        private static readonly System.Collections.Immutable.ImmutableArray<(int, int)> Cardinals = [(0, 1), (0, -1), (1, 0), (-1, 0)];

        // kills unreachable ALIVE; DEF_DEAD/DEF_ALIVE is left untouched, and a pocket holding DEF_ALIVE keeps its ALIVE
        private static void FloodFillIslandRemoval(byte[,] grid, int width, int height, (int X, int Y) start)
        {
            if (start.X >= width || start.Y >= height)
                return;
            var visited = new bool[width, height];
            var queue = new Queue<(int, int)>();
            visited[start.X, start.Y] = true;
            queue.Enqueue(start);
            while (queue.TryDequeue(out var c))
            {
                foreach ((int ddx, int ddy) in Cardinals)
                {
                    int nx = c.Item1 + ddx, ny = c.Item2 + ddy;
                    if (nx >= 0 && nx < width && ny >= 0 && ny < height && !visited[nx, ny] && grid[nx, ny] != DEAD)
                    {
                        visited[nx, ny] = true;
                        queue.Enqueue((nx, ny));
                    }
                }
            }
            var component = new bool[width, height];
            var alive = new List<(int, int)>();
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    if (visited[x, y] || component[x, y] || grid[x, y] is not (ALIVE or DEF_ALIVE))
                        continue;
                    alive.Clear();
                    bool hasDefAlive = false;
                    component[x, y] = true;
                    queue.Enqueue((x, y));
                    while (queue.TryDequeue(out var c))
                    {
                        if (grid[c.Item1, c.Item2] == DEF_ALIVE)
                            hasDefAlive = true;
                        else if (grid[c.Item1, c.Item2] == ALIVE)
                            alive.Add(c);
                        foreach ((int ddx, int ddy) in Cardinals)
                        {
                            int nx = c.Item1 + ddx, ny = c.Item2 + ddy;
                            if (nx >= 0 && nx < width && ny >= 0 && ny < height && !visited[nx, ny] && !component[nx, ny]
                                && grid[nx, ny] is ALIVE or DEF_ALIVE)
                            {
                                component[nx, ny] = true;
                                queue.Enqueue((nx, ny));
                            }
                        }
                    }
                    if (!hasDefAlive)
                        foreach ((int ax, int ay) in alive)
                            grid[ax, ay] = DEAD;
                }
        }
    }

    /// <summary>The dbpnoise crate 0.1.2's gen_noise.</summary>
    internal static class DbpNoise
    {
        // dbpnoise's sigmoid stand-in for smoothstep
        private static float Smooth(float x) => (MathF.Tanh(8f * x - 3f) + 1f) / 2f;

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        public static bool[,] Generate(string seed, int accuracy, int stampSize, int worldSize, float lower, float upper)
        {
            int realStamp = stampSize * 2;
            // Seeder::from(seed).make_rng::<Pcg32>(): a stream determined by the seed string alone (FNV-1a here)
            ulong hash = 0xcbf29ce484222325UL;
            foreach (byte b in Encoding.UTF8.GetBytes(seed))
                hash = (hash ^ b) * 0x100000001b3UL;
            var rng = new Rng(hash);

            var stamps = new float[accuracy][,];
            float step = 2f * MathF.PI / accuracy;
            float fsize = realStamp;
            for (int i = 0; i < accuracy; i++)
            {
                float vx = MathF.Cos(i * step), vy = MathF.Sin(i * step);
                var data = new float[realStamp, realStamp];
                float scale = 1f / (fsize / 2f * MathF.Sqrt(2f));
                for (int x = 0; x < realStamp; x++)
                    for (int y = 0; y < realStamp; y++)
                        data[x, y] = ((x - fsize / 2f + 0.5f) * scale * vx) + ((y - fsize / 2f + 0.5f) * scale * vy);
                stamps[i] = data;
            }
            int realWorld = (int)(MathF.Ceiling(worldSize / (float)stampSize) * stampSize + realStamp);
            int cells = realWorld / stampSize;
            var stampOf = new int[cells, cells];
            for (int x = 0; x < cells; x++)
                for (int y = 0; y < cells; y++)
                    stampOf[x, y] = (int)(rng.NextUInt64() % (ulong)stamps.Length);

            int side = Math.Min(realWorld - realStamp, worldSize);
            var result = new bool[Math.Max(side, 0), Math.Max(side, 0)];
            for (int x = realStamp; x < realStamp + side; x++)
                for (int y = realStamp; y < realStamp + side; y++)
                {
                    int xdiv = x / stampSize, ydiv = y / stampSize;
                    if (xdiv < 1 || xdiv >= cells || ydiv < 1 || ydiv >= cells) // the crate's "> len" would index past the end
                        continue;
                    float x1 = stamps[stampOf[xdiv, ydiv]][x - xdiv * stampSize, y - ydiv * stampSize];
                    float x2 = stamps[stampOf[xdiv - 1, ydiv]][x - (xdiv - 1) * stampSize, y - ydiv * stampSize];
                    float x3 = stamps[stampOf[xdiv, ydiv - 1]][x - xdiv * stampSize, y - (ydiv - 1) * stampSize];
                    float x4 = stamps[stampOf[xdiv - 1, ydiv - 1]][x - (xdiv - 1) * stampSize, y - (ydiv - 1) * stampSize];
                    float ux = Smooth((x - xdiv * stampSize) / (float)stampSize), uy = Smooth((y - ydiv * stampSize) / (float)stampSize);
                    float value = Lerp(Lerp(x4, x3, ux), Lerp(x2, x1, ux), uy);
                    result[x - realStamp, y - realStamp] = value >= lower && value < upper;
                }
            return result;
        }
    }
}
