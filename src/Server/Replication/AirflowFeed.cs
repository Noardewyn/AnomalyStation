using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Replication;

// The airflow streams (D141, DEVIATION DV34; docs/ai/specs/replication.md section 16). The sink of AirflowSeam collects the
// flow of each SSair cycle (tg's pressure_difference and pressure_direction of the turfs it pushes atoms on) and publishes
// it at the cycle's end; after each tick the host asks it for every joined session's AirflowUpdate: the changes, since that
// session's last update, of the cells within the session's interest square around its mob (one level above and below too).
// Bandwidth: nothing while no air moves near the player; a level only goes out when it moved by two steps or more (about
// half again), a direction or a start or an end of flow always; runs of one row pack a byte per cell, with up to
// MaxRunGap unchanged cells inside a run instead of a new run's header.

/// <summary>What a session was sent of the air flow (the host keeps one per session, <see cref="Session.Airflow"/>).</summary>
public sealed class AirflowView
{
    internal readonly Dictionary<int, byte> Sent = new(); // key (AirflowFeed.Key) to the byte the client has
    internal readonly Dictionary<int, byte> SentVents = new(); // version 7: the floor devices' bytes the client has
    internal int Version = -1;
    internal int CenterKey = -1;
    internal bool Pending; // the last update hit the cell cap: the rest goes in the next one

    /// <summary>Cells the client has a flow for.</summary>
    public int Count => Sent.Count;
}

/// <summary>The server's airflow sink and sender.</summary>
public sealed class AirflowFeed : IAirflowSink
{
    /// <summary>Unchanged cells a run may hold between two changed ones (one byte each; a new run costs about four).</summary>
    public const int MaxRunGap = 3;

    /// <summary>Changes one update takes at most (the nearest first); with the gaps and the run headers they stay within the message's caps.</summary>
    public const int MaxChangesPerUpdate = Limits.MaxAirflowCells / 2;

    /// <summary>The level steps a cell's flow must move before a client hears of it (its direction or a start or an end always counts).</summary>
    public const int LevelHysteresis = 2;

    private Dictionary<int, byte> _building = new();
    private Dictionary<int, byte> _published = new();
    private readonly Dictionary<int, float> _ventBuilding = new(); // signed kPa summed over the cell's devices this cycle
    private Dictionary<int, byte> _ventPublished = new();
    private readonly List<AirflowVent> _ventChanges = new();
    private readonly List<(int Key, byte Cell)> _changes = new();

    /// <param name="radius">The half side of each session's interest square in tiles (ServerConfig.AirflowRadius).</param>
    public AirflowFeed(int radius)
    {
        if (radius < 1) throw new ArgumentOutOfRangeException(nameof(radius), radius, "at least 1");
        Radius = radius;
    }

    /// <summary>The half side of each session's interest square, in tiles.</summary>
    public int Radius { get; }

    /// <summary>Bumped when a cycle's flow differs from the last one's.</summary>
    public int Version { get; private set; }

    /// <summary>The cells of the last published cycle that have a flow.</summary>
    public int PublishedCount => _published.Count;

    /// <summary>Installs the sink in <paramref name="world"/> (the host, before the world runs).</summary>
    public void Attach(World world) => AirflowSeam.Install(world, this);

    /// <summary>The key of a cell (x and y 1..255, z 1..16): independent of the map's size.</summary>
    public static int Key(int x, int y, int z) => (z << 16) | (y << 8) | x;

    private static (int X, int Y, int Z) Unkey(int key) => (key & 0xFF, (key >> 8) & 0xFF, key >> 16);

    void IAirflowSink.Flow(turf_open turf, float difference, int direction) =>
        Record((int)Dm.num(turf.x), (int)Dm.num(turf.y), (int)Dm.num(turf.z), difference, direction);

    void IAirflowSink.CycleEnd() => EndCycle();

    void IAirflowSink.Vent(turf_open turf, float kpa) =>
        RecordVent((int)Dm.num(turf.x), (int)Dm.num(turf.y), (int)Dm.num(turf.z), kpa);

    /// <summary>A floor device moved gas on a cell this cycle: the pressure it makes in the cell's air, positive out of the device (several devices on one cell add up).</summary>
    public void RecordVent(int x, int y, int z, float kpa)
    {
        if (!float.IsFinite(kpa) || x < 1 || y < 1 || z < 1 || x > Limits.MaxMapSize || y > Limits.MaxMapSize || z > Limits.MaxMapLevels) return;
        int key = Key(x, y, z);
        _ventBuilding[key] = _ventBuilding.GetValueOrDefault(key) + kpa;
    }

    /// <summary>The cells of the last published cycle whose floor devices moved air.</summary>
    public int PublishedVentCount => _ventPublished.Count;

    /// <summary>A cell's flow this cycle: tg's pressure difference (kPa) toward a BYOND dir; under level 1 or another dir is none.</summary>
    public void Record(int x, int y, int z, float difference, int direction)
    {
        AirflowDir dir = Airflow.DirOfByond(direction);
        int level = Airflow.LevelOfKpa(difference);
        if (dir == AirflowDir.None || level == 0) return;
        if (x < 1 || y < 1 || z < 1 || x > Limits.MaxMapSize || y > Limits.MaxMapSize || z > Limits.MaxMapLevels) return;
        _building[Key(x, y, z)] = Airflow.Pack(dir, level);
    }

    /// <summary>The cycle's end: what was recorded since the last end becomes the published flow.</summary>
    public void EndCycle()
    {
        var vents = new Dictionary<int, byte>();
        foreach ((int key, float kpa) in _ventBuilding)
        {
            int level = Airflow.LevelOfKpa(Math.Abs(kpa));
            if (level > 0) vents[key] = AirflowVents.Pack(kpa > 0 ? AirflowVentKind.Out : AirflowVentKind.In, level);
        }
        _ventBuilding.Clear();
        if (!Same(_building, _published) || !Same(vents, _ventPublished))
            Version++;
        (_published, _building) = (_building, _published);
        _building.Clear();
        _ventPublished = vents;
    }

    private static bool Same(Dictionary<int, byte> a, Dictionary<int, byte> b)
    {
        if (a.Count != b.Count) return false;
        foreach ((int key, byte cell) in a)
        {
            if (!b.TryGetValue(key, out byte other) || other != cell) return false;
        }
        return true;
    }

    /// <summary>
    /// The update for a session whose mob stands at (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) (z 0:
    /// no mob, everything the client has is cleared), or null when nothing changed for it. Updates <paramref name="view"/>
    /// as if the client applied it.
    /// </summary>
    public AirflowUpdate Build(AirflowView view, int x, int y, int z)
    {
        ArgumentNullException.ThrowIfNull(view);
        int center = z < 1 ? 0 : Key(x, y, z);
        if (!view.Pending && view.Version == Version && view.CenterKey == center) return null;
        view.Version = Version;
        view.CenterKey = center;

        _changes.Clear();
        if (z >= 1)
        {
            foreach ((int key, byte cell) in _published)
            {
                if (!Near(key, x, y, z)) continue;
                view.Sent.TryGetValue(key, out byte had);
                if (Differs(had, cell)) _changes.Add((key, cell));
            }
        }
        foreach ((int key, byte _) in view.Sent)
        {
            if (z < 1 || !Near(key, x, y, z) || !_published.ContainsKey(key)) _changes.Add((key, Airflow.Clear));
        }

        // the floor devices (version 7): few, all go at once (within the cap, the nearest first)
        _ventChanges.Clear();
        if (z >= 1)
        {
            foreach ((int key, byte cell) in _ventPublished)
            {
                if (!Near(key, x, y, z)) continue;
                view.SentVents.TryGetValue(key, out byte had);
                if (VentDiffers(had, cell)) _ventChanges.Add(VentAt(key, cell));
            }
        }
        foreach ((int key, byte _) in view.SentVents)
        {
            if (z < 1 || !Near(key, x, y, z) || !_ventPublished.ContainsKey(key)) _ventChanges.Add(VentAt(key, Airflow.Clear));
        }
        bool ventsCut = _ventChanges.Count > Limits.MaxAirflowVents;
        if (ventsCut)
        {
            _ventChanges.Sort((a, b) => Distance(Key(a.X, a.Y, a.Z), x, y, z).CompareTo(Distance(Key(b.X, b.Y, b.Z), x, y, z)));
            _ventChanges.RemoveRange(Limits.MaxAirflowVents, _ventChanges.Count - Limits.MaxAirflowVents);
        }
        foreach (AirflowVent vent in _ventChanges)
        {
            if (vent.Cell == Airflow.Clear) view.SentVents.Remove(Key(vent.X, vent.Y, vent.Z));
            else view.SentVents[Key(vent.X, vent.Y, vent.Z)] = vent.Cell;
        }

        if (_changes.Count == 0)
        {
            view.Pending = ventsCut;
            if (_ventChanges.Count == 0) return null;
            var onlyVents = new AirflowUpdate();
            onlyVents.Vents.AddRange(_ventChanges);
            return onlyVents;
        }

        // When they cannot all fit, the nearest half-cap go now and the rest next tick; then row order for the runs. Only the
        // changes the runs hold count as sent.
        bool cut = _changes.Count > MaxChangesPerUpdate;
        if (cut)
        {
            _changes.Sort((a, b) => Distance(a.Key, x, y, z).CompareTo(Distance(b.Key, x, y, z)));
            _changes.RemoveRange(MaxChangesPerUpdate, _changes.Count - MaxChangesPerUpdate);
        }
        _changes.Sort((a, b) => a.Key.CompareTo(b.Key));
        AirflowUpdate update = Pack(_changes, out int packed);
        update.Vents.AddRange(_ventChanges);
        view.Pending = cut || packed < _changes.Count || ventsCut;
        for (int i = 0; i < packed; i++)
        {
            (int key, byte cell) = _changes[i];
            if (cell == Airflow.Clear) view.Sent.Remove(key);
            else view.Sent[key] = cell;
        }
        return update;
    }

    private static AirflowVent VentAt(int key, byte cell)
    {
        (int vx, int vy, int vz) = Unkey(key);
        return new AirflowVent(vx, vy, vz, cell);
    }

    // As Differs, for a device's byte: its kind changing always counts.
    private static bool VentDiffers(byte had, byte now)
    {
        if (had == now) return false;
        if (had == Airflow.Clear || now == Airflow.Clear) return true;
        if (AirflowVents.KindOf(had) != AirflowVents.KindOf(now)) return true;
        return Math.Abs(Airflow.LevelOf(had) - Airflow.LevelOf(now)) >= LevelHysteresis;
    }

    // Whether the client should hear of a cell going from `had` to `now`.
    private static bool Differs(byte had, byte now)
    {
        if (had == now) return false;
        if (had == Airflow.Clear || now == Airflow.Clear) return true;
        if (Airflow.DirOf(had) != Airflow.DirOf(now)) return true;
        return Math.Abs(Airflow.LevelOf(had) - Airflow.LevelOf(now)) >= LevelHysteresis;
    }

    private bool Near(int key, int x, int y, int z)
    {
        (int cx, int cy, int cz) = Unkey(key);
        return Math.Abs(cx - x) <= Radius && Math.Abs(cy - y) <= Radius && Math.Abs(cz - z) <= 1;
    }

    private static int Distance(int key, int x, int y, int z)
    {
        (int cx, int cy, int cz) = Unkey(key);
        return Math.Max(Math.Max(Math.Abs(cx - x), Math.Abs(cy - y)), Math.Abs(cz - z));
    }

    // The changes (sorted by key: level, row, column) as runs of rows; a gap of up to MaxRunGap cells stays in a run as
    // Unchanged bytes. Stops before a run or a cell past the message's caps: `packed` is how many changes went in (a prefix).
    private static AirflowUpdate Pack(List<(int Key, byte Cell)> changes, out int packed)
    {
        var update = new AirflowUpdate();
        var cells = new List<byte>();
        int runX0 = 0, runY = 0, runZ = 0, lastX = 0, total = 0;
        packed = 0;
        foreach ((int key, byte cell) in changes)
        {
            (int x, int y, int z) = Unkey(key);
            bool extend = cells.Count > 0 && z == runZ && y == runY && x - lastX - 1 <= MaxRunGap && x - runX0 < Limits.MaxAirflowRunCells
                && total + (x - lastX) <= Limits.MaxAirflowCells;
            if (!extend)
            {
                if (cells.Count > 0) Flush();
                if (update.Runs.Count >= Limits.MaxAirflowRuns || total + 1 > Limits.MaxAirflowCells) break;
                runX0 = x;
                runY = y;
                runZ = z;
            }
            else
            {
                for (int gap = lastX + 1; gap < x; gap++)
                {
                    cells.Add(Airflow.Unchanged);
                    total++;
                }
            }
            cells.Add(cell);
            total++;
            lastX = x;
            packed++;
        }
        if (cells.Count > 0) Flush();
        return update;

        void Flush()
        {
            update.Runs.Add(new AirflowRun(runX0, runY, runZ, cells.ToArray()));
            cells.Clear();
        }
    }
}
