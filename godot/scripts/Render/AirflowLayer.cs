using Godot;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

// The airflow streams (D141, DEVIATION DV34; docs/ai/specs/apps.md 2.19). tg's SSair pushes atoms by each turf's pressure
// difference toward its pressure direction and shows nothing of it; the server sends the client that flow for the cells
// around the player (protocol version 6, ReplicaWorld.AirflowAt) and this draws it as thin streams of air.
//
// The flow field: every cell with a flow points at a neighbour. A cell whose neighbour has no flow is where the air leaves
// (a sink face): through the wall square between the two cells when the neighbour is a breached wall, through the floor
// square of the neighbour when it is a hole in the floor (space with open floor on two or more sides), through the cell's
// own floor or ceiling for a flow down or up. Sink faces whose holes touch are one hole; every flow cell belongs to the
// hole its chain of directions ends in.
//
// The budget: at most MaxStreams streams live, at most MaxPerHole on one hole, shared by the holes near the camera (within
// SpawnDistance) whose area is in view, in proportion to their strength; a weak draft gets fewer. Nothing spawns for a hole
// out of view or far away; living streams finish their lives.
//
// A stream: born at a random point of a flow cell of its hole (stronger cells more often), its path traced once through the
// smoothed field to the point of the hole's face nearest to where it starts (with a little jitter), through the face and on
// into space; a short piece of the path slides along it at the air's speed (tg's push force grows with the square root of
// the difference, so does the speed) as a camera-facing ribbon: fading in, semi-transparent in the middle of its life,
// fading out, thinner at both ends. Its colour is white, or the colour of the gas haze of the cell it starts in.

/// <summary>The airflow streams around the player.</summary>
public partial class AirflowLayer : Node3D
{
    /// <summary>tg's push threshold for a human (pressure_resistance 25 in human_defines.dm; experience_pressure_difference moves it when the difference over 25 * 75 - 25 passes 25, i.e. above 16.7 kPa): the debug arrows mark it.</summary>
    public const float HumanPushKpa = 16.7f;

    private const float Step = 0.12f; // tiles per path step
    private const int MaxPathPoints = 160;
    private const int Segments = 12; // ribbon segments of the visible piece
    private const float FaceMargin = 0.12f; // tiles kept from a face's edge
    private const float FaceJitter = 0.12f; // tiles of jitter around the nearest point
    private const float FadeIn = 0.45f, FadeOut = 0.6f; // seconds
    // The rhythm of a hole's streams: each is born on its own, the gap to the next its share of the hole's rate (the target
    // count over the mean life) times a random factor 1 +- SpawnJitter; lives are random in these ranges (seconds), so the
    // streams neither come in waves nor die together (the owner, 2026-10-27).
    private const float SpawnJitter = 0.8f;
    private const float HoleLifeMin = 1.2f, HoleLifeMax = 3.4f;
    private const float VentLifeMin = 0.9f, VentLifeMax = 2.6f;
    // How long a vent's or scrubber's streams are against the first cut (their reach from the grate and their visible piece):
    // the owner, 2026-10-27, asked for shorter ones.
    private const float VentLengthScale = 0.55f;

    private ReplicaWorld _replica;
    private GasLayer _gas;
    private readonly Dictionary<int, FlowCell> _cells = new();
    private readonly Dictionary<int, Vent> _vents = new();
    private readonly List<Hole> _holes = new();
    private readonly List<Stream> _streams = new();
    private readonly RandomNumberGenerator _rng = new();
    private bool _topologyDirty;
    private int _width, _height;
    private MeshInstance3D _ribbons;
    private ArrayMesh _mesh;
    private MeshInstance3D _debug;
    private ImmediateMesh _debugMesh;
    private Vector3[] _vertices = Array.Empty<Vector3>();
    private Color[] _colors = Array.Empty<Color>();
    private Vector2[] _uvs = Array.Empty<Vector2>();
    private int _lastUsed;
    private Vector3 _boundsMin, _boundsMax;
    private readonly Vector3[] _samples = new Vector3[Segments + 1];
    private readonly float[] _sampleTaper = new float[Segments + 1];

    /// <summary>Whether the streams are drawn and spawned (the dev menu; ANOMALY_AIRFLOW=0 turns them off at start).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The most streams alive at once, over all holes.</summary>
    public int MaxStreams { get; set; } = 880;

    /// <summary>The most streams alive at once for one hole.</summary>
    public int MaxPerHole { get; set; } = 126;

    /// <summary>The most streams alive at once for one floor device (a vent or a scrubber).</summary>
    public int MaxPerVent { get; set; } = 37;

    /// <summary>Floor devices moving air now.</summary>
    public int VentCount => _vents.Count;

    /// <summary>Holes farther than this from the camera, in tiles, spawn nothing (nor do flow cells this far).</summary>
    public int SpawnDistance { get; set; } = 20;

    /// <summary>Draws the flow of every cell as an arrow on the floor and every hole's faces (amber arrows: above <see cref="HumanPushKpa"/>).</summary>
    public bool ShowDebug { get; set; }

    /// <summary>Cells with a flow now.</summary>
    public int FlowCellCount => _cells.Count;

    /// <summary>Holes now, and how many of them spawn (near and in view) this frame.</summary>
    public int HoleCount => _holes.Count;

    /// <summary>Holes that spawn this frame (near the camera and in view).</summary>
    public int ActiveHoles { get; private set; }

    /// <summary>Streams alive now.</summary>
    public int StreamCount => _streams.Count;

    private sealed class FlowCell
    {
        public int X, Y, Z;
        public AirflowDir Dir;
        public float Kpa;
        public int Hole = -1; // index into _holes
        public FlowCell Sink; // the cell whose exits this cell's air leaves by (itself when it has exits)
        public List<Face> Exits; // where the air leaves this cell (null: it goes on to a neighbour)
    }

    private enum FaceKind { Wall, Floor, Ceiling, Fade }

    private sealed class Face
    {
        public FaceKind Kind;
        public int X, Y, Z; // the wall face: the cell the air leaves; the floor or ceiling face: the cell whose square it is
        public int Dx, Dy; // the wall face: the direction through it
        public int Ox, Oy; // the cell it opens into (the floor face of a hole in the floor: that cell itself)
        public float Kpa;
    }

    private sealed class Hole
    {
        public readonly List<Face> Faces = new();
        public readonly List<FlowCell> Cells = new();
        public float[] Weights = Array.Empty<float>(); // cumulative spawn weights of Cells
        public float Strength; // the strongest face's kPa
        public Aabb Bounds; // the hole's cells and faces, in world metres
        public int Alive;
        public int Budget;
        public float SpawnTimer; // seconds to the next stream
        public Vent Vent; // a floor device's air instead of a hole (version 7)
    }

    // A floor device moving air on its cell (a vent, a scrubber, an injector, a passive vent): out of it or into it.
    private sealed class Vent
    {
        public int X, Y, Z;
        public AirflowVentKind Kind;
        public float Kpa;
    }

    private sealed class Stream
    {
        public Vector3[] Points;
        public float[] Lengths; // cumulative, metres
        public float[] Speeds; // metres per second at each point
        public float Total;
        public float Head, Window, Age, Life, Alpha, Width;
        public Color Color;
        public int Hole;
        public int SinkKey = -1; // the cell whose exit it goes out by: its hole again after the holes are rebuilt
        public int VentKey = -1; // or the floor device's cell it comes out of or goes into
        public int Hint; // the last point index found for the head
    }

    public void Bind(ReplicaWorld replica, GasLayer gas)
    {
        _replica = replica;
        _gas = gas;
        _replica.AirflowChanged += OnAirflowChanged;
        _rng.Randomize();
        _mesh = new ArrayMesh();
        _ribbons = new MeshInstance3D
        {
            Name = "AirflowRibbons",
            Mesh = _mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/airflow_streams.gdshader") },
            ExtraCullMargin = 16f,
        };
        AddChild(_ribbons);
        _debugMesh = new ImmediateMesh();
        _debug = new MeshInstance3D
        {
            Name = "AirflowDebug",
            Mesh = _debugMesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // drawn over everything (the floor's own surface lies a little above its level, and 1-pixel lines were lost on
            // a light floor: the owner, 2026-10-14), see-through enough to read the floor under it
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = true,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                RenderPriority = 10,
            },
            Visible = false,
        };
        AddChild(_debug);
        if (System.Environment.GetEnvironmentVariable("ANOMALY_AIRFLOW") == "0") Enabled = false;
        if (System.Environment.GetEnvironmentVariable("ANOMALY_AIRFLOW_DEBUG") == "1") ShowDebug = true;
        Reload();
    }

    public override void _ExitTree()
    {
        if (_replica != null)
            _replica.AirflowChanged -= OnAirflowChanged;
    }

    /// <summary>Takes every cell from the replica again.</summary>
    public void Reload()
    {
        _cells.Clear();
        _vents.Clear();
        if (_replica?.Map == null) return;
        _width = _replica.Map.Width;
        _height = _replica.Map.Height;
        foreach ((int x, int y, int z, byte cell) in _replica.AirflowCells)
            Take(x, y, z, cell);
        foreach ((int x, int y, int z, byte cell) in _replica.AirflowVentCells)
            TakeVent(x, y, z, cell);
        _topologyDirty = true;
    }

    private void OnAirflowChanged(IReadOnlyList<(int X, int Y, int Z)> cells)
    {
        if (_replica.Map == null) return;
        if (_replica.Map.Width != _width || _replica.Map.Height != _height)
        {
            Reload();
            return;
        }
        foreach ((int x, int y, int z) in cells)
        {
            Take(x, y, z, _replica.AirflowAt(x, y, z));
            TakeVent(x, y, z, _replica.AirflowVentAt(x, y, z));
        }
        _topologyDirty = true;
    }

    private void TakeVent(int x, int y, int z, byte cell)
    {
        int key = Key(x, y, z);
        if (cell == Airflow.Clear)
        {
            _vents.Remove(key);
            return;
        }
        if (!_vents.TryGetValue(key, out Vent vent))
            _vents[key] = vent = new Vent { X = x, Y = y, Z = z };
        vent.Kind = AirflowVents.KindOf(cell);
        vent.Kpa = Airflow.KpaOf(Airflow.LevelOf(cell));
    }

    private void Take(int x, int y, int z, byte cell)
    {
        int key = Key(x, y, z);
        if (cell == Airflow.Clear)
        {
            _cells.Remove(key);
            return;
        }
        if (!_cells.TryGetValue(key, out FlowCell flow))
            _cells[key] = flow = new FlowCell { X = x, Y = y, Z = z };
        flow.Dir = Airflow.DirOf(cell);
        flow.Kpa = Airflow.KpaOf(Airflow.LevelOf(cell));
    }

    private int Key(int x, int y, int z) => (x - 1) + (y - 1) * _width + (z - 1) * _width * _height;

    private FlowCell CellAt(int x, int y, int z) =>
        x >= 1 && y >= 1 && z >= 1 && x <= _width && y <= _height && _cells.TryGetValue(Key(x, y, z), out FlowCell c) ? c : null;

    private static (int Dx, int Dy) Delta(AirflowDir dir) => dir switch
    {
        AirflowDir.North => (0, 1),
        AirflowDir.South => (0, -1),
        AirflowDir.East => (1, 0),
        AirflowDir.West => (-1, 0),
        _ => (0, 0),
    };

    // ---- the holes ----

    private bool IsSpace(int x, int y, int z)
    {
        string type = _replica.TurfTypeAt(x, y, z);
        return type != null && type.StartsWith("/turf/open/space", StringComparison.Ordinal);
    }

    private bool IsOpenFloor(int x, int y, int z)
    {
        string type = _replica.TurfTypeAt(x, y, z);
        return type != null && type.StartsWith("/turf/open/", StringComparison.Ordinal)
            && !type.StartsWith("/turf/open/space", StringComparison.Ordinal) && !type.StartsWith("/turf/open/openspace", StringComparison.Ordinal);
    }

    // A space cell with open floor on two or more sides is a hole in the floor; one with less is a breach in a wall.
    private bool IsFloorHole(int x, int y, int z)
    {
        if (!IsSpace(x, y, z)) return false;
        int open = 0;
        if (IsOpenFloor(x + 1, y, z)) open++;
        if (IsOpenFloor(x - 1, y, z)) open++;
        if (IsOpenFloor(x, y + 1, z)) open++;
        if (IsOpenFloor(x, y - 1, z)) open++;
        return open >= 2;
    }

    private void RebuildTopology()
    {
        _topologyDirty = false;
        _holes.Clear();

        // The exits. tg keeps one direction per turf (the strongest share of the cycle), so a cell beside a hole often points
        // along it rather than into it, and a cell open to space on two sides names only one (the owner, 2026-10-14: no
        // streams into the middle of three breached tiles). So a flow cell's exits are every space neighbour, besides the
        // cell its own direction names when that one has no flow, and the floor or ceiling for a flow down or up.
        var faces = new List<Face>();
        foreach (FlowCell c in _cells.Values)
        {
            c.Hole = -1;
            c.Sink = null;
            c.Exits = null;
            List<Face> exits = null;
            void Add(Face face)
            {
                face.Kpa = c.Kpa;
                (exits ??= new List<Face>()).Add(face);
                faces.Add(face);
            }
            if (c.Dir == AirflowDir.Down || c.Dir == AirflowDir.Up)
                Add(new Face { Kind = c.Dir == AirflowDir.Down ? FaceKind.Floor : FaceKind.Ceiling, X = c.X, Y = c.Y, Z = c.Z, Ox = c.X, Oy = c.Y });
            (int ddx, int ddy) = Delta(c.Dir);
            for (int k = 0; k < 4; k++)
            {
                (int dx, int dy) = k switch { 0 => (0, 1), 1 => (0, -1), 2 => (1, 0), _ => (-1, 0) };
                int bx = c.X + dx, by = c.Y + dy;
                if (CellAt(bx, by, c.Z) != null) continue; // the air goes on there
                bool named = dx == ddx && dy == ddy && (ddx != 0 || ddy != 0);
                bool space = IsSpace(bx, by, c.Z);
                if (!named && !space) continue;
                // the air going on into a cell that is not space and has no flow of its own (where a spreading gas thins
                // out, a door, a vacuum filling): no hole there, the streams run on into it and fade
                if (!space)
                    Add(new Face { Kind = FaceKind.Fade, X = c.X, Y = c.Y, Z = c.Z, Dx = dx, Dy = dy, Ox = bx, Oy = by });
                else
                    Add(IsFloorHole(bx, by, c.Z)
                        ? new Face { Kind = FaceKind.Floor, X = bx, Y = by, Z = c.Z, Ox = bx, Oy = by }
                        : new Face { Kind = FaceKind.Wall, X = c.X, Y = c.Y, Z = c.Z, Dx = dx, Dy = dy, Ox = bx, Oy = by });
            }
            if (exits == null) continue;
            c.Exits = exits;
            c.Sink = c;
        }

        // The holes: faces of one cell, and faces whose cells they open into touch (diagonals too), are one hole.
        var parent = new int[faces.Count];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        int Find(int i)
        {
            while (parent[i] != i) i = parent[i] = parent[parent[i]];
            return i;
        }
        void Union(int a, int b) => parent[Find(a)] = Find(b);
        var faceIndex = new Dictionary<Face, int>(ReferenceEqualityComparer.Instance);
        var byOpen = new Dictionary<int, int>();
        for (int i = 0; i < faces.Count; i++)
        {
            faceIndex[faces[i]] = i;
            Face f = faces[i];
            for (int ny = f.Oy - 1; ny <= f.Oy + 1; ny++)
                for (int nx = f.Ox - 1; nx <= f.Ox + 1; nx++)
                    if (byOpen.TryGetValue(OpenKey(nx, ny, f.Z), out int other)) Union(i, other);
            byOpen.TryAdd(OpenKey(f.Ox, f.Oy, f.Z), i);
        }
        foreach (FlowCell c in _cells.Values)
            if (c.Exits != null)
                for (int i = 1; i < c.Exits.Count; i++) Union(faceIndex[c.Exits[0]], faceIndex[c.Exits[i]]);
        var holeOfRoot = new Dictionary<int, int>();
        for (int i = 0; i < faces.Count; i++)
        {
            int root = Find(i);
            if (!holeOfRoot.TryGetValue(root, out int hole))
            {
                hole = _holes.Count;
                holeOfRoot[root] = hole;
                _holes.Add(new Hole());
            }
            _holes[hole].Faces.Add(faces[i]);
        }
        foreach (FlowCell c in _cells.Values)
            if (c.Exits != null) c.Hole = holeOfRoot[Find(faceIndex[c.Exits[0]])];

        // Every other flow cell to the exits its chain of directions reaches first (cycles and dead ends belong to none).
        var chain = new List<FlowCell>();
        foreach (FlowCell start in _cells.Values)
        {
            if (start.Sink != null) continue;
            chain.Clear();
            FlowCell c = start;
            FlowCell sink = null;
            for (int guard = 0; guard < 512 && c != null; guard++)
            {
                if (c.Sink != null)
                {
                    sink = c.Sink;
                    break;
                }
                if (chain.Contains(c)) break;
                chain.Add(c);
                (int dx, int dy) = Delta(c.Dir);
                c = CellAt(c.X + dx, c.Y + dy, c.Z);
            }
            foreach (FlowCell link in chain)
            {
                link.Sink = sink;
                link.Hole = sink?.Hole ?? -1;
            }
        }

        foreach (FlowCell c in _cells.Values)
            if (c.Hole >= 0) _holes[c.Hole].Cells.Add(c);
        foreach (Hole hole in _holes)
        {
            hole.Strength = 0;
            foreach (Face f in hole.Faces) hole.Strength = Math.Max(hole.Strength, f.Kpa);
            hole.Weights = new float[hole.Cells.Count];
            float sum = 0;
            Aabb bounds = default;
            bool first = true;
            for (int i = 0; i < hole.Cells.Count; i++)
            {
                FlowCell c = hole.Cells[i];
                sum += MathF.Pow(c.Kpa, 1.2f);
                hole.Weights[i] = sum;
                Aabb box = CellBox(c.X, c.Y, c.Z);
                bounds = first ? box : bounds.Merge(box);
                first = false;
            }
            hole.Bounds = bounds;
        }
        // the floor devices: each its own source of streams, within a little more than its reach
        var ventHoles = new Dictionary<int, int>();
        foreach ((int key, Vent vent) in _vents)
        {
            ventHoles[key] = _holes.Count;
            float reach = (VentReach(vent) + 0.5f) * TileSpace.TileSize;
            Aabb box = CellBox(vent.X, vent.Y, vent.Z);
            _holes.Add(new Hole { Vent = vent, Strength = vent.Kpa, Bounds = box.Grow(reach) });
        }

        // living streams count again for the hole their exit belongs to now (a rebuild comes with every update, twice a
        // second: forgetting them let each hole spawn its whole budget again every time)
        foreach (Stream s in _streams)
        {
            s.Hole = s.VentKey >= 0 ? (ventHoles.TryGetValue(s.VentKey, out int ventHole) ? ventHole : -1)
                : _cells.TryGetValue(s.SinkKey, out FlowCell sinkCell) ? sinkCell.Hole : -1;
            if (s.Hole >= 0) _holes[s.Hole].Alive++;
        }
        _debugDirty = true;
    }

    // One of the sink's exits for a stream starting at `from` (tile space): the nearer an exit, the likelier (the owner,
    // 2026-10-14: a stream goes to the part of the hole nearest to where it starts).
    private Face PickExit(FlowCell sink, Vector3 from)
    {
        List<Face> exits = sink.Exits;
        if (exits.Count == 1) return exits[0];
        Span<float> weights = stackalloc float[Math.Min(exits.Count, 8)];
        float total = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            Face f = exits[i];
            Vector2 at = f.Kind is FaceKind.Wall or FaceKind.Fade ? new Vector2(f.X + f.Dx * 0.5f, f.Y + f.Dy * 0.5f) : new Vector2(f.X, f.Y);
            float d = at.DistanceTo(new Vector2(from.X, from.Y));
            weights[i] = 1f / (d * d + 0.2f);
            total += weights[i];
        }
        float pick = _rng.Randf() * total;
        for (int i = 0; i < weights.Length; i++)
        {
            pick -= weights[i];
            if (pick <= 0) return exits[i];
        }
        return exits[weights.Length - 1];
    }

    private int OpenKey(int x, int y, int z) => (z << 18) ^ (y << 9) ^ x;

    private static Aabb CellBox(int x, int y, int z)
    {
        float t = TileSpace.TileSize;
        return new Aabb(new Vector3(TileSpace.WorldX(x) - t / 2, TileSpace.WorldY(z), TileSpace.WorldZ(y) - t / 2), new Vector3(t, TileSpace.LevelHeight, t));
    }

    // ---- the frame ----

    public override void _Process(double delta)
    {
        using var frameProbe = Anomaly.Client.Dev.FrameProbe.Scope("AirflowLayer._Process");
        if (_mesh == null) return;
        float dt = (float)Math.Min(delta, 0.1);
        if (_topologyDirty) RebuildTopology();
        _debug.Visible = ShowDebug;
        if (ShowDebug && _debugDirty) DrawDebug();
        else if (!ShowDebug) _debugDirty = true;

        Camera3D camera = GetViewport()?.GetCamera3D();
        if (Enabled && camera != null) Spawn(camera, dt);

        for (int i = _streams.Count - 1; i >= 0; i--)
        {
            Stream s = _streams[i];
            s.Age += dt;
            s.Head += SpeedAt(s, s.Head) * dt;
            if (s.Age >= s.Life || s.Head - s.Window >= s.Total)
            {
                if (s.Hole >= 0 && s.Hole < _holes.Count) _holes[s.Hole].Alive--;
                _streams.RemoveAt(i);
            }
        }

        if (!Enabled)
        {
            _streams.Clear();
            foreach (Hole h in _holes) h.Alive = 0;
        }
        Build(camera);
    }

    private bool _debugDirty = true;

    private void Spawn(Camera3D camera, float dt)
    {
        Vector3 eye = camera.GlobalPosition;
        float reach = SpawnDistance * TileSpace.TileSize;
        Godot.Collections.Array<Plane> frustum = camera.GetFrustum();
        var open = new List<Hole>();
        float total = 0;
        foreach (Hole hole in _holes)
        {
            hole.Budget = 0;
            if ((hole.Vent == null && (hole.Cells.Count == 0 || hole.Faces.Count == 0)) || Weight(hole) <= 0) continue;
            if (ClampToBox(eye, hole.Bounds).DistanceTo(eye) > reach || !InFrustum(frustum, hole.Bounds)) continue;
            open.Add(hole);
            total += Weight(hole);
        }
        ActiveHoles = open.Count;
        if (total <= 0) return;

        // The shares: in proportion to strength, at most MaxPerHole each, what a capped hole leaves going to the others.
        int left = Math.Max(0, MaxStreams);
        float weightLeft = total;
        for (int pass = 0; pass < 3 && open.Count > 0 && left > 0; pass++)
        {
            int given = 0;
            float weightNext = 0;
            var next = new List<Hole>();
            foreach (Hole hole in open)
            {
                int share = (int)MathF.Round(left * Weight(hole) / weightLeft);
                int cap = hole.Vent != null ? MaxPerVent : MaxPerHole;
                int room = cap - hole.Budget;
                int take = Math.Min(share, room);
                hole.Budget += take;
                given += take;
                if (hole.Budget < cap)
                {
                    next.Add(hole);
                    weightNext += Weight(hole);
                }
            }
            left -= given;
            open = next;
            weightLeft = weightNext;
            if (given == 0) break;
        }

        int alive = _streams.Count;
        for (int h = 0; h < _holes.Count; h++)
        {
            Hole hole = _holes[h];
            // a weak draft keeps fewer streams than a breach that throws people about
            int target = (int)(hole.Budget * (hole.Vent != null ? Math.Max(0.4f, VentStrength(hole.Vent)) : Math.Clamp(hole.Strength / 30f, 0.2f, 1f)));
            float meanLife = hole.Vent != null ? (VentLifeMin + VentLifeMax) / 2 : (HoleLifeMin + HoleLifeMax) / 2;
            float gap = meanLife / Math.Max(1, target);
            hole.SpawnTimer -= dt;
            for (int n = 0; n < 8 && hole.SpawnTimer <= 0 && hole.Alive < target && alive < MaxStreams; n++)
            {
                hole.SpawnTimer += gap * _rng.RandfRange(1f - SpawnJitter, 1f + SpawnJitter);
                if (hole.Vent != null ? TrySpawnVent(h, hole, eye, reach) : TrySpawn(h, hole, eye, reach))
                {
                    hole.Alive++;
                    alive++;
                }
            }
            // a hole at its count (or a frame too long) owes at most one gap: no burst when room frees up
            hole.SpawnTimer = Math.Max(hole.SpawnTimer, -gap);
        }
    }

    private static float Weight(Hole hole) =>
        MathF.Sqrt(Math.Max(hole.Strength, 0f)) * (hole.Vent != null ? 1f : MathF.Sqrt(hole.Faces.Count));

    // How far, in tiles, a floor device's streams reach from its grate: further for more air.
    private static float VentReach(Vent vent) => VentLengthScale * (0.8f + 1.4f * VentStrength(vent));

    // A floor device moves far less air per cycle than a breach (a scrubber at work makes 1 to 5 kPa in its cell, a vent
    // refilling a vacuum tens): its own scale, so a working scrubber is seen at all.
    private static float VentStrength(Vent vent) => Math.Clamp(vent.Kpa / 6f, 0f, 1f);

    // ---- a floor device's streams (tile space, as a hole's) ----

    // Out of the grate: born on it, rising a little and spreading over the floor in all directions (bent by the room's flow
    // where there is one), slowing and fading. Into the grate: born in the air around it (in the same room: open floor all the
    // way), drawn to the point of the grate nearest to where it starts, faster as it nears it, and down through the floor.
    private bool TrySpawnVent(int index, Hole hole, Vector3 eye, float reach)
    {
        Vent vent = hole.Vent;
        float top = TileSpace.LevelHeight / TileSpace.TileSize;
        float strength = VentStrength(vent);
        float span = VentReach(vent);
        for (int attempt = 0; attempt < 4; attempt++)
        {
            var points = new List<Vector3>(48);
            var speeds = new List<float>(48);
            float speed = 0.35f + 0.35f * MathF.Sqrt(vent.Kpa); // tiles per second
            if (vent.Kind == AirflowVentKind.Out)
            {
                float angle = _rng.Randf() * MathF.Tau, radius = MathF.Sqrt(_rng.Randf()) * 0.28f;
                var p = new Vector3(vent.X + MathF.Cos(angle) * radius, vent.Y + MathF.Sin(angle) * radius, 0.02f);
                var radial = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                float rise = _rng.RandfRange(0.6f, 1.4f), curl = _rng.RandfRange(-0.25f, 0.25f);
                int steps = (int)(span * _rng.RandfRange(0.7f, 1.2f) / Step);
                for (int i = 0; i <= steps; i++)
                {
                    points.Add(p);
                    speeds.Add(speed);
                    (Vector2 field, _) = Sample(p.X, p.Y, vent.Z);
                    Vector2 flat = radial + (field.LengthSquared() > 1e-6f ? field.Normalized() * 0.6f : Vector2.Zero);
                    flat = flat.Rotated(curl * i * Step);
                    var dir = new Vector3(flat.X, flat.Y, rise).Normalized();
                    Vector3 next = p + dir * Step;
                    next.Z = Math.Clamp(next.Z, 0.01f, top - 0.05f);
                    if (!IsOpenFloor((int)MathF.Round(next.X), (int)MathF.Round(next.Y), vent.Z)) break;
                    p = next;
                    rise *= 0.82f;
                    speed *= 0.97f;
                }
            }
            else
            {
                float angle = _rng.Randf() * MathF.Tau, radius = _rng.RandfRange(0.5f, span + 0.5f); // outside the grate
                var p = new Vector3(vent.X + MathF.Cos(angle) * radius, vent.Y + MathF.Sin(angle) * radius,
                    (0.04f + MathF.Pow(_rng.Randf(), 1.4f) * 0.7f) * top);
                if (!LineOpen(p, vent)) continue;
                // the grate is about half the tile: the point of it nearest to the start, jittered
                var target = new Vector3(vent.X + Math.Clamp(p.X - vent.X, -0.22f, 0.22f) + _rng.RandfRange(-0.06f, 0.06f),
                    vent.Y + Math.Clamp(p.Y - vent.Y, -0.22f, 0.22f) + _rng.RandfRange(-0.06f, 0.06f), 0f);
                for (int i = 0; i < 80; i++)
                {
                    points.Add(p);
                    Vector3 to = target - p;
                    float distance = to.Length();
                    speeds.Add(speed * (1f + 0.8f / (distance + 0.4f)));
                    if (distance < Step) break;
                    (Vector2 field, _) = Sample(p.X, p.Y, vent.Z);
                    float pull = Math.Clamp(1.1f - distance / (span + 1f), 0.6f, 1f);
                    Vector3 flow = field.LengthSquared() > 1e-6f ? new Vector3(field.X, field.Y, 0).Normalized() : Vector3.Zero;
                    p += ((to / distance) * pull + flow * (1 - pull)).Normalized() * Step;
                }
                for (int i = 1; i <= 3; i++) // down through the grate
                {
                    points.Add(target + new Vector3(0, 0, -0.1f * i));
                    speeds.Add(speed * 1.5f);
                }
            }
            if (points.Count < 4) continue;
            Stream s = MakeStream(points, speeds, vent.Z);
            if (s.Points[0].DistanceTo(eye) > reach) continue;
            s.Hole = index;
            s.VentKey = Key(vent.X, vent.Y, vent.Z);
            s.Window = VentLengthScale * (0.35f + 0.8f * strength + _rng.RandfRange(0f, 0.3f)) * TileSpace.TileSize;
            s.Life = _rng.RandfRange(VentLifeMin, VentLifeMax);
            s.Alpha = 0.2f + 0.3f * strength; // a scrubber at its everyday work is a gentle draught, but one that is seen
            s.Width = 0.012f + 0.01f * strength;
            s.Color = ColorOfCell(vent.X, vent.Y, vent.Z);
            _streams.Add(s);
            return true;
        }
        return false;
    }

    // Open floor on every cell between a point and a device's cell (a stream into a grate comes from its own room).
    private bool LineOpen(Vector3 from, Vent vent)
    {
        for (int i = 0; i <= 6; i++)
        {
            float t = i / 6f;
            int x = (int)MathF.Round(from.X + (vent.X - from.X) * t), y = (int)MathF.Round(from.Y + (vent.Y - from.Y) * t);
            if (!IsOpenFloor(x, y, vent.Z)) return false;
        }
        return true;
    }

    // A stream from points in tile space and their speeds in tiles per second.
    private static Stream MakeStream(List<Vector3> points, List<float> speeds, int z)
    {
        var s = new Stream
        {
            Points = new Vector3[points.Count],
            Lengths = new float[points.Count],
            Speeds = new float[points.Count],
        };
        float length = 0;
        for (int i = 0; i < points.Count; i++)
        {
            s.Points[i] = ToWorld(points[i], z);
            if (i > 0) length += s.Points[i].DistanceTo(s.Points[i - 1]);
            s.Lengths[i] = length;
            s.Speeds[i] = speeds[i] * TileSpace.TileSize;
        }
        s.Total = length;
        return s;
    }

    private static Vector3 ClampToBox(Vector3 p, Aabb box) => p.Clamp(box.Position, box.End);

    private static bool InFrustum(Godot.Collections.Array<Plane> planes, Aabb box)
    {
        Vector3 c = box.GetCenter(), e = box.Size / 2;
        foreach (Plane p in planes)
        {
            // Godot's frustum planes face outward: a box wholly on the outer side of one is out of view
            float r = e.X * Math.Abs(p.Normal.X) + e.Y * Math.Abs(p.Normal.Y) + e.Z * Math.Abs(p.Normal.Z);
            if (p.DistanceTo(c) > r) return false;
        }
        return true;
    }

    private bool TrySpawn(int index, Hole hole, Vector3 eye, float reach)
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            float pick = _rng.Randf() * hole.Weights[^1];
            int i = Array.BinarySearch(hole.Weights, pick);
            if (i < 0) i = ~i;
            FlowCell cell = hole.Cells[Math.Min(i, hole.Cells.Count - 1)];
            if (cell.Sink?.Exits == null) continue;
            Vector3 from = new(cell.X + _rng.RandfRange(-0.45f, 0.45f), cell.Y + _rng.RandfRange(-0.45f, 0.45f),
                (0.04f + _rng.Randf() * 0.92f) * TileSpace.LevelHeight / TileSpace.TileSize); // floor to ceiling: a breach is open all the way up
            Vector3 world = ToWorld(from, cell.Z);
            if (world.DistanceTo(eye) > reach) continue;
            Stream s = Trace(from, cell, PickExit(cell.Sink, from));
            if (s == null) continue;
            s.Hole = index;
            s.SinkKey = Key(cell.Sink.X, cell.Sink.Y, cell.Sink.Z);
            float strength = Math.Clamp(cell.Kpa / 35f, 0f, 1f);
            s.Window = (0.6f + 1.8f * Math.Clamp(cell.Kpa / 40f, 0f, 1f) + _rng.RandfRange(0f, 0.4f)) * TileSpace.TileSize;
            s.Life = _rng.RandfRange(HoleLifeMin, HoleLifeMax);
            s.Alpha = 0.07f + 0.3f * strength;
            s.Width = 0.008f + 0.014f * strength; // metres, half the ribbon's width
            s.Color = ColorOf(cell);
            _streams.Add(s);
            return true;
        }
        return false;
    }

    // White, or the colour of the gas haze where the stream starts (tg's gas sprites' mean colour, more saturated: the
    // haze itself lies over the stream and would wash a pale tint out to white). Even a thin haze colours it clearly.
    private Color ColorOf(FlowCell cell) => ColorOfCell(cell.X, cell.Y, cell.Z);

    private Color ColorOfCell(int x, int y, int z)
    {
        var white = new Color(0.93f, 0.96f, 1f);
        if (_gas == null || !_gas.TryGetLook(x, y, z, out Color gas, out float opacity) || opacity < 0.02f) return white;
        Color vivid = Color.FromHsv(gas.H, Math.Min(1f, gas.S * 1.8f + 0.15f), Math.Max(gas.V, 0.75f));
        return white.Lerp(vivid, Math.Clamp(0.5f + opacity * 2f, 0f, 0.95f));
    }

    // ---- a stream's path (tile space: x east, y north, z up in tiles above the level's floor) ----

    private static Vector3 ToWorld(Vector3 p, int z) =>
        new(TileSpace.WorldX(p.X), TileSpace.WorldY(z) + p.Z * TileSpace.TileSize, TileSpace.WorldZ(p.Y));

    private Stream Trace(Vector3 from, FlowCell start, Face face)
    {
        float top = TileSpace.LevelHeight / TileSpace.TileSize;
        (Vector3 target, Vector3 exit) = FacePoint(face, from, top);
        var points = new List<Vector3>(64) { from };
        var kpa = new List<float>(64) { start.Kpa };
        Vector3 p = from;
        Vector3 dir = Vector3.Zero;
        bool through = false;
        for (int i = 0; i < MaxPathPoints && !through; i++)
        {
            Vector3 toTarget = target - p;
            float distance = toTarget.Length();
            (Vector2 field, float strength) = Sample(p.X, p.Y, start.Z);
            float pull = Math.Clamp(1.2f - distance / 2f, 0.15f, 1f);
            Vector3 flow = field.LengthSquared() > 1e-6f ? new Vector3(field.X, field.Y, 0).Normalized() : Vector3.Zero;
            dir = (flow * (1 - pull) + (distance > 1e-4f ? toTarget / distance : exit) * pull).Normalized();
            Vector3 next = p + dir * Step;
            // a step into a cell the air does not flow through (round a corner): the cell's own direction instead
            int nx = (int)MathF.Round(next.X), ny = (int)MathF.Round(next.Y);
            if (pull < 0.9f && CellAt(nx, ny, start.Z) == null && !(nx == face.Ox && ny == face.Oy))
            {
                FlowCell here = CellAt((int)MathF.Round(p.X), (int)MathF.Round(p.Y), start.Z);
                if (here == null) break;
                (int dx, int dy) = Delta(here.Dir);
                dir = new Vector3(dx, dy, (target.Z - p.Z) * 0.2f).Normalized();
                next = p + dir * Step;
            }
            // only over a hole in the floor (or the ceiling) may the path leave the room through it: a clamp to the floor
            // everywhere kept every stream to a floor hole from getting through, so none was ever drawn (the owner, 2026-10-14)
            bool overHole = face.Kind is FaceKind.Floor or FaceKind.Ceiling
                && (int)MathF.Round(next.X) == face.Ox && (int)MathF.Round(next.Y) == face.Oy;
            next.Z = Math.Clamp(next.Z, overHole && face.Kind == FaceKind.Floor ? -1f : 0.01f, overHole && face.Kind == FaceKind.Ceiling ? top + 1f : top - 0.01f);
            p = next;
            points.Add(p);
            kpa.Add(Math.Max(strength, start.Kpa * 0.25f));
            through = (p - target).Dot(exit) >= 0f && distance < 0.6f;
        }
        if (!through || points.Count < 3) return null;

        // on through the face and out, speeding up
        float last = kpa[^1];
        // through a ceiling or a floor at most one tile, through a wall at most one and a half (the owner, 2026-10-31)
        int outSteps = (int)(face.Kind switch
        {
            FaceKind.Fade => _rng.RandfRange(0.3f, 0.7f),
            FaceKind.Ceiling or FaceKind.Floor => _rng.RandfRange(0.6f, 1f),
            _ => _rng.RandfRange(0.9f, 1.5f),
        } / Step);
        Vector3 outDir = (exit * 0.85f + dir * 0.15f).Normalized();
        for (int i = 0; i < outSteps; i++)
        {
            p += outDir * Step;
            points.Add(p);
            last *= 1.05f;
            kpa.Add(last);
        }

        var s = new Stream
        {
            Points = new Vector3[points.Count],
            Lengths = new float[points.Count],
            Speeds = new float[points.Count],
        };
        float length = 0;
        for (int i = 0; i < points.Count; i++)
        {
            s.Points[i] = ToWorld(points[i], start.Z);
            if (i > 0) length += s.Points[i].DistanceTo(s.Points[i - 1]);
            s.Lengths[i] = length;
            // tiles per second 0.3 + 0.42 * sqrt(kPa): tg's push force is sqrt(difference) * MOVE_FORCE_DEFAULT / 5
            s.Speeds[i] = (0.3f + 0.42f * MathF.Sqrt(kpa[i])) * TileSpace.TileSize;
        }
        s.Total = length;
        return s;
    }

    // The point of the face nearest to `from` (kept off its edges, jittered a little) and the direction out through it.
    private (Vector3 Point, Vector3 Exit) FacePoint(Face face, Vector3 from, float top)
    {
        float lo = -0.5f + FaceMargin, hi = 0.5f - FaceMargin;
        float J() => _rng.RandfRange(-FaceJitter, FaceJitter);
        switch (face.Kind)
        {
            case FaceKind.Wall:
            {
                float h = Math.Clamp(from.Z + J(), FaceMargin, top - FaceMargin); // at the stream's own height: the upper part of the hole too
                if (face.Dx != 0)
                {
                    float v = face.Y + Math.Clamp(from.Y - face.Y + J(), lo, hi);
                    return (new Vector3(face.X + face.Dx * 0.5f, v, h), new Vector3(face.Dx, 0, 0));
                }
                float u = face.X + Math.Clamp(from.X - face.X + J(), lo, hi);
                return (new Vector3(u, face.Y + face.Dy * 0.5f, h), new Vector3(0, face.Dy, 0));
            }
            case FaceKind.Fade:
            {
                // a point in the cell the air goes on into, at about the stream's height
                float h = Math.Clamp(from.Z + J(), FaceMargin, top - FaceMargin);
                var at = new Vector3(face.Ox + _rng.RandfRange(-0.35f, 0.35f), face.Oy + _rng.RandfRange(-0.35f, 0.35f), h);
                return (at, new Vector3(face.Dx, face.Dy, 0));
            }
            case FaceKind.Ceiling:
            {
                float u = face.X + Math.Clamp(from.X - face.X + J(), lo, hi);
                float v = face.Y + Math.Clamp(from.Y - face.Y + J(), lo, hi);
                return (new Vector3(u, v, top), new Vector3(0, 0, 1));
            }
            default:
            {
                // the floor square: the nearest point, drawn a little toward the middle so the air pours over the near
                // side rather than along the rim
                float u = face.X + Math.Clamp((from.X - face.X) * 0.7f + J(), lo, hi);
                float v = face.Y + Math.Clamp((from.Y - face.Y) * 0.7f + J(), lo, hi);
                return (new Vector3(u, v, 0f), new Vector3(0, 0, -1));
            }
        }
    }

    // The flow at a point: the directions of the four nearest flow cells blended (bilinear, missing cells left out), each
    // weighted by the square root of its difference; and the blended difference.
    private (Vector2 Flow, float Kpa) Sample(float u, float v, int z)
    {
        int x0 = (int)MathF.Floor(u), y0 = (int)MathF.Floor(v);
        float tx = u - x0, ty = v - y0;
        Vector2 sum = Vector2.Zero;
        float kpa = 0, weight = 0;
        for (int j = 0; j <= 1; j++)
            for (int i = 0; i <= 1; i++)
            {
                FlowCell c = CellAt(x0 + i, y0 + j, z);
                if (c == null) continue;
                float w = (i == 0 ? 1 - tx : tx) * (j == 0 ? 1 - ty : ty);
                (int dx, int dy) = Delta(c.Dir);
                sum += new Vector2(dx, dy) * MathF.Sqrt(c.Kpa) * w;
                kpa += c.Kpa * w;
                weight += w;
            }
        return weight > 0 ? (sum / weight, kpa / weight) : (Vector2.Zero, 0f);
    }

    private static float SpeedAt(Stream s, float at)
    {
        int i = Index(s, Math.Clamp(at, 0, s.Total));
        return s.Speeds[i];
    }

    private static int Index(Stream s, float at)
    {
        int i = Math.Clamp(s.Hint, 0, s.Lengths.Length - 2);
        while (i > 0 && s.Lengths[i] > at) i--;
        while (i < s.Lengths.Length - 2 && s.Lengths[i + 1] < at) i++;
        s.Hint = i;
        return i;
    }

    private static Vector3 PointAt(Stream s, float at)
    {
        at = Math.Clamp(at, 0, s.Total);
        int i = Index(s, at);
        float span = s.Lengths[i + 1] - s.Lengths[i];
        float t = span > 1e-5f ? (at - s.Lengths[i]) / span : 0f;
        return s.Points[i].Lerp(s.Points[i + 1], Math.Clamp(t, 0f, 1f));
    }

    // ---- the ribbons ----

    private void Build(Camera3D camera)
    {
        int need = _streams.Count * Segments * 6;
        if (need == 0 || camera == null)
        {
            if (_lastUsed > 0) _mesh.ClearSurfaces();
            _lastUsed = 0;
            _ribbons.Visible = false;
            return;
        }
        int capacity = Math.Max(need, Math.Max(MaxStreams, 64) * Segments * 6);
        if (_vertices.Length < capacity)
        {
            _vertices = new Vector3[capacity];
            _colors = new Color[capacity];
            _uvs = new Vector2[capacity];
            _lastUsed = capacity;
        }
        Vector3 eye = camera.GlobalPosition;
        _boundsMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        _boundsMax = -_boundsMin;
        int v = 0;
        foreach (Stream s in _streams)
        {
            float envelope = Math.Min(Math.Min(s.Age / FadeIn, (s.Life - s.Age) / FadeOut), (s.Total - (s.Head - s.Window)) / (0.8f * TileSpace.TileSize));
            envelope = Math.Clamp(envelope, 0f, 1f);
            float a0 = Math.Max(0, s.Head - s.Window), a1 = Math.Min(s.Head, s.Total);
            if (envelope <= 0.001f || a1 - a0 < 0.01f)
            {
                v = Degenerate(v, Segments * 6);
                continue;
            }
            for (int k = 0; k <= Segments; k++)
            {
                _samples[k] = PointAt(s, a0 + (a1 - a0) * k / Segments);
                float u = (float)k / Segments;
                // sin(pi) is a hair below 0 in floats, and a fractional power of it is NaN: the last vertex of every ribbon was
                // NaN, which made the mesh's box a point and culled the streams wherever the origin was out of view (2026-10-14)
                _sampleTaper[k] = MathF.Pow(MathF.Max(0f, MathF.Sin(MathF.PI * MathF.Pow(u, 0.8f))), 1.3f);
            }
            for (int k = 0; k < Segments; k++)
            {
                Vector3 p0 = _samples[k], p1 = _samples[k + 1];
                Vector3 side0 = Side(_samples[Math.Max(k - 1, 0)], _samples[k + 1], p0, eye) * s.Width * (0.5f + 0.5f * _sampleTaper[k]);
                Vector3 side1 = Side(_samples[k], _samples[Math.Min(k + 2, Segments)], p1, eye) * s.Width * (0.5f + 0.5f * _sampleTaper[k + 1]);
                Color c0 = new(s.Color, s.Alpha * envelope * _sampleTaper[k]);
                Color c1 = new(s.Color, s.Alpha * envelope * _sampleTaper[k + 1]);
                float u0 = (float)k / Segments, u1 = (float)(k + 1) / Segments;
                Put(v++, p0 - side0, c0, new Vector2(u0, 0));
                Put(v++, p0 + side0, c0, new Vector2(u0, 1));
                Put(v++, p1 + side1, c1, new Vector2(u1, 1));
                Put(v++, p0 - side0, c0, new Vector2(u0, 0));
                Put(v++, p1 + side1, c1, new Vector2(u1, 1));
                Put(v++, p1 - side1, c1, new Vector2(u1, 0));
            }
        }
        // what last frame used beyond this frame's end collapses to nothing
        if (v < _lastUsed) Degenerate(v, _lastUsed - v);
        _lastUsed = v;

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = _vertices;
        arrays[(int)Mesh.ArrayType.Color] = _colors;
        arrays[(int)Mesh.ArrayType.TexUV] = _uvs;
        _mesh.ClearSurfaces();
        _mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        // the box the culling uses, from the ribbons themselves (a tight one, and never a NaN's point at the origin)
        if (_boundsMin.X <= _boundsMax.X && float.IsFinite(_boundsMin.X + _boundsMin.Y + _boundsMin.Z + _boundsMax.X + _boundsMax.Y + _boundsMax.Z))
            _ribbons.CustomAabb = new Aabb(_boundsMin, _boundsMax - _boundsMin).Grow(0.25f);
        _ribbons.Visible = true;
    }

    private void Put(int i, Vector3 p, Color c, Vector2 uv)
    {
        _boundsMin = _boundsMin.Min(p);
        _boundsMax = _boundsMax.Max(p);
        _vertices[i] = p;
        _colors[i] = c;
        _uvs[i] = uv;
    }

    private int Degenerate(int from, int count)
    {
        int end = Math.Min(from + count, _vertices.Length);
        for (int i = from; i < end; i++)
        {
            _vertices[i] = Vector3.Zero;
            _colors[i] = new Color(0, 0, 0, 0);
        }
        return end;
    }

    // The ribbon's half-width direction at p: across the path and across the line of sight.
    private static Vector3 Side(Vector3 before, Vector3 after, Vector3 p, Vector3 eye)
    {
        Vector3 tangent = after - before;
        Vector3 side = tangent.Cross(eye - p);
        float length = side.Length();
        return length > 1e-6f ? side / length : Vector3.Up;
    }

    // ---- the debug view ----

    private void DrawDebug()
    {
        _debugDirty = false;
        _debugMesh.ClearSurfaces();
        if (_cells.Count == 0 && _vents.Count == 0) return;
        _debugMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        float t = TileSpace.TileSize;
        var weak = new Color(0.2f, 0.85f, 0.7f, 0.9f);
        var strong = new Color(1f, 0.62f, 0.1f, 0.95f);
        foreach (FlowCell c in _cells.Values)
        {
            Color color = c.Kpa >= HumanPushKpa ? strong : weak;
            var center = new Vector3(TileSpace.WorldX(c.X), TileSpace.WorldY(c.Z) + 0.05f, TileSpace.WorldZ(c.Y));
            float size = 0.35f + 0.65f * Math.Clamp(c.Kpa / 40f, 0f, 1f); // a stronger flow, a bigger arrow
            if (c.Dir == AirflowDir.Down || c.Dir == AirflowDir.Up)
            {
                // a diamond: the air goes through the floor or the ceiling here
                float r = 0.3f * t * size;
                Quad(center + new Vector3(-r, 0, 0), center + new Vector3(0, 0, -r), center + new Vector3(r, 0, 0), center + new Vector3(0, 0, r), color);
                continue;
            }
            (int dx, int dy) = Delta(c.Dir);
            var d = new Vector3(dx, 0, -dy); // north is -Z
            var side = new Vector3(-d.Z, 0, d.X);
            float half = 0.42f * t * size, shaft = 0.05f * t, head = 0.16f * t * Math.Max(size, 0.6f), headLength = 0.22f * t * Math.Max(size, 0.6f);
            Vector3 tail = center - d * half, tip = center + d * half, neck = tip - d * headLength;
            Quad(tail - side * shaft, tail + side * shaft, neck + side * shaft, neck - side * shaft, color);
            Triangle(neck - side * head, neck + side * head, tip, color);
        }
        foreach (Vent vent in _vents.Values)
        {
            // a ring of four arrows at the grate: outward for air out of it, inward for air into it
            var center = new Vector3(TileSpace.WorldX(vent.X), TileSpace.WorldY(vent.Z) + 0.05f, TileSpace.WorldZ(vent.Y));
            Color color = vent.Kind == AirflowVentKind.Out ? new Color(0.45f, 1f, 0.45f, 0.95f) : new Color(0.45f, 0.65f, 1f, 0.95f);
            float size = 0.5f + 0.5f * VentStrength(vent);
            foreach (Vector3 d in new[] { new Vector3(1, 0, 0), new Vector3(-1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 0, -1) })
            {
                var side = new Vector3(-d.Z, 0, d.X);
                Vector3 near = center + d * 0.12f * t, far = center + d * 0.42f * t * size;
                Vector3 tip = vent.Kind == AirflowVentKind.Out ? far : near, back = vent.Kind == AirflowVentKind.Out ? near : far;
                Vector3 along = (tip - back).Normalized();
                Triangle(tip, tip - along * 0.12f * t + side * 0.08f * t, tip - along * 0.12f * t - side * 0.08f * t, color);
                Quad(back - side * 0.025f * t, back + side * 0.025f * t, tip - along * 0.1f * t + side * 0.025f * t, tip - along * 0.1f * t - side * 0.025f * t, color);
            }
        }
        var red = new Color(0.95f, 0.2f, 0.2f, 0.35f);
        var redEdge = new Color(1f, 0.25f, 0.25f, 0.95f);
        float top = TileSpace.LevelHeight, edge = 0.04f;
        foreach (Hole hole in _holes)
            foreach (Face f in hole.Faces)
            {
                if (f.Kind == FaceKind.Fade) continue; // no hole: the air just runs on
                float y0 = TileSpace.WorldY(f.Z);
                if (f.Kind == FaceKind.Wall)
                {
                    float bx = TileSpace.WorldX(f.X + f.Dx * 0.5f), bz = TileSpace.WorldZ(f.Y + f.Dy * 0.5f);
                    var along = new Vector3(f.Dy != 0 ? t / 2 : 0, 0, f.Dx != 0 ? t / 2 : 0);
                    var a = new Vector3(bx, y0, bz) - along;
                    var b = new Vector3(bx, y0, bz) + along;
                    var up = new Vector3(0, top, 0);
                    Quad(a, b, b + up, a + up, red);
                    var unit = along.Normalized();
                    Quad(a, b, b + Vector3.Up * edge, a + Vector3.Up * edge, redEdge);
                    Quad(a + up, b + up, b + up - Vector3.Up * edge, a + up - Vector3.Up * edge, redEdge);
                    Quad(a, a + unit * edge, a + unit * edge + up, a + up, redEdge);
                    Quad(b, b - unit * edge, b - unit * edge + up, b + up, redEdge);
                }
                else
                {
                    float y = f.Kind == FaceKind.Floor ? y0 + 0.06f : y0 + top - 0.06f;
                    float x0 = TileSpace.WorldX(f.X) - t / 2, z0 = TileSpace.WorldZ(f.Y) - t / 2;
                    Quad(new Vector3(x0, y, z0), new Vector3(x0 + t, y, z0), new Vector3(x0 + t, y, z0 + t), new Vector3(x0, y, z0 + t), red);
                }
            }
        _debugMesh.SurfaceEnd();
    }

    private void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        _debugMesh.SurfaceSetColor(color);
        _debugMesh.SurfaceAddVertex(a);
        _debugMesh.SurfaceSetColor(color);
        _debugMesh.SurfaceAddVertex(b);
        _debugMesh.SurfaceSetColor(color);
        _debugMesh.SurfaceAddVertex(c);
    }

    private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        Triangle(a, b, c, color);
        Triangle(a, c, d, color);
    }
}
