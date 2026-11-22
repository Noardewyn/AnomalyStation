using System.Globalization;
using System.Text;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Replication;

/// <summary>The replicator's locomotion view for the tests: the capsule states the test sets, and the plain solidity predicate.</summary>
internal sealed class FakeLocomotion : ILocomotionView
{
    public Dictionary<long, MotionState> Capsules { get; } = new();

    public bool TryGetCapsule(World world, long mobId, out MotionState state) => Capsules.TryGetValue(mobId, out state);

    public bool IsSolid(World world, int x, int y, int z) => DenseSolidity.IsSolid(world, x, y, z);
}

/// <summary>One connected player of a <see cref="V2Run"/>: its tg client and mob, its replica, and every byte the server sent it.</summary>
internal sealed class SessionView
{
    public int Ordinal { get; init; }

    public client Client { get; init; }

    public mob Avatar { get; init; }

    public ReplicaWorld Replica { get; } = new();

    /// <summary>Every message the server sent this session (the join's MapGrid and Snapshot, then one Delta per tick), as bytes.</summary>
    public List<byte[]> Bytes { get; } = new();

    /// <summary>Version 42: the Motion messages the server sent this session after each Delta, as bytes.</summary>
    public List<byte[]> MotionBytes { get; } = new();

    /// <summary>Version 42: the Motion entries of the last tick.</summary>
    public int LastMotionEntries { get; set; }

    public long JoinedTick { get; set; }

    public uint LastInputSeq { get; set; }

    public byte InputBufferDepth { get; set; }

    public long TotalBytes => Bytes.Sum(b => (long)b.Length) + MotionBytes.Sum(b => (long)b.Length);
}

/// <summary>
/// The server host's loop around a replicator, in a test: step the world, build the Delta body once, a private block per
/// session, put each session's Delta together (<c>Codec.EncodeDelta</c> with its block) and apply the bytes to its replica,
/// recording them. Sessions join after a tick's Delta, as the host's joins do.
/// </summary>
internal sealed class V2Run
{
    private byte[] _buffer = new byte[4096];
    private int _ordinal;

    public ReplicationWorld World { get; }

    public Replicator Replicator { get; }

    public FakeLocomotion Locomotion { get; } = new();

    public List<SessionView> Sessions { get; } = new();

    public long BodyBytes { get; private set; }

    public int Deltas { get; private set; }

    public DeltaFrame LastFrame { get; private set; }

    /// <summary>The full-scan oracle; it scans after every tick (from the attach on), so its observation of moves is complete.</summary>
    public WorldOracleV2 Oracle { get; } = new();

    /// <summary>The oracle's scan of the last tick.</summary>
    public SortedDictionary<long, EntityViewV2> Truth { get; private set; }

    public V2Run(ReplicationWorld world, int sweepDivisor = 64, int minSweep = 32, int cellSweep = 16, int screenSweep = 20)
    {
        World = world;
        Replicator = new Replicator(new ReplicatorOptions
        {
            SweepDivisor = sweepDivisor, MinSweepPerTick = minSweep, CellSweepPerTick = cellSweep, ScreenSweepPeriodTicks = screenSweep,
            Locomotion = Locomotion,
        });
        Replicator.Attach(world.World);
        Truth = Oracle.Scan(world.World, Locomotion);
    }

    /// <summary>Boots a quiet room (no scenario, no movers): the test puts in what it needs.</summary>
    public static V2Run Quiet(int size = 16, int seed = 7, int sweepDivisor = 64, int minSweep = 32, int cellSweep = 16, int screenSweep = 20)
    {
        ReplicationWorld world = ReplicationWorld.Boot(size: size, movers: 0, seed: seed, containers: 0, notTranslatedThrows: false);
        world.ScenarioOn = false;
        return new V2Run(world, sweepDivisor, minSweep, cellSweep, screenSweep);
    }

    public void In(Action action)
    {
        using (World.World.Enter()) action();
    }

    public T In<T>(Func<T> func)
    {
        using (World.World.Enter()) return func();
    }

    /// <summary>One tick: the world, the Delta body, each session's private block and Delta, applied to each session's replica.</summary>
    private readonly List<MotionMessage> _motion = new();

    public DeltaFrame Step()
    {
        World.Step();
        long tick = World.World.Tick;
        DeltaFrame frame = Replicator.BuildDelta(tick);
        LastFrame = frame;
        BodyBytes += frame.Body.Length;
        Deltas++;
        foreach (SessionView s in Sessions.ToArray())
        {
            if (s.JoinedTick >= tick) continue;
            PrivateFrame own = Replicator.BuildPrivate(tick, Inputs(s));
            int need = Codec.MaxDeltaHeaderBytes + frame.Body.Length + own.Bytes.Length;
            if (_buffer.Length < need) _buffer = new byte[need * 2];
            int length = Codec.EncodeDelta(tick, s.LastInputSeq, s.Avatar.dm_id, frame.Body.Span, own.Bytes.Span, _buffer);
            byte[] message = _buffer.AsSpan(0, length).ToArray();
            s.Bytes.Add(message);
            Wire.Apply(s.Replica, message);
            // Version 42: the capsule mobs' positions, as the host sends them after the Delta.
            Replicator.BuildMotion(s.Ordinal, _motion);
            s.LastMotionEntries = 0;
            foreach (MotionMessage motion in _motion)
            {
                byte[] bytes = Codec.Encode(motion);
                s.MotionBytes.Add(bytes);
                s.LastMotionEntries += motion.Entries.Count;
                Wire.Apply(s.Replica, bytes);
            }
        }
        Truth = Oracle.Scan(World.World, Locomotion);
        return frame;
    }

    public PrivateInputs Inputs(SessionView s)
    {
        bool capsule = Locomotion.Capsules.TryGetValue(s.Avatar.dm_id, out MotionState state);
        return new PrivateInputs(s.Ordinal, s.Client.dm_id, s.Avatar.dm_id, s.LastInputSeq, capsule, state, capsule ? MotionParams.DefaultRun : default, s.InputBufferDepth);
    }

    /// <summary>A player connects: tg's client/New runs for a mob keyed with <paramref name="key"/>, then the session joins (MapGrid and Snapshot with its private block).</summary>
    public SessionView Connect(string key, mob avatar)
    {
        ClientConnection connection = null;
        In(() =>
        {
            ClientSeam.KeyMob(avatar, key);
            connection = ClientSeam.Connect(key);
        });
        for (int i = 0; i < 20 && !connection.Done; i++) Step();
        Assert.True(connection.Accepted, "the login did not finish: " + connection.Error);
        var session = new SessionView { Ordinal = ++_ordinal, Client = connection.Client, Avatar = avatar, JoinedTick = World.World.Tick };
        JoinMessages join = Replicator.BuildJoin(World.World.Tick, Inputs(session));
        session.Bytes.Add(join.MapGrid);
        session.Bytes.Add(join.Snapshot);
        Wire.Apply(session.Replica, join.MapGrid);
        Wire.Apply(session.Replica, join.Snapshot);
        Sessions.Add(session);
        return session;
    }

    public void Leave(SessionView session)
    {
        Sessions.Remove(session);
        Replicator.Forget(session.Ordinal);
    }

    public turf Turf(int x, int y) => locate(x, y, 1);
}

/// <summary>
/// The tests' own reading of appearances, written apart from the replicator's interner: a canonical text of an appearance
/// as the world has it (<see cref="Of(IDmAppearance, bool)"/>) and of the same appearance as a replica resolves it from its
/// tables (<see cref="Of(ReplicaWorld, TableRef)"/>). Equal text means the same picture. Only values the tests set are
/// converted (colour texts as <c>#rrggbb</c> or <c>#rrggbbaa</c>).
/// </summary>
internal static class AppearanceText
{
    public static string Of(IDmAppearance a, bool top, bool keepDir = false)
    {
        var overlays = new List<string>();
        var underlays = new List<string>();
        foreach (object item in Items(a.overlays)) AddItem(overlays, item);
        foreach (object item in Items(a.underlays)) AddItem(underlays, item);
        string icon = IconPath(a.icon);
        if (top && icon == null && overlays.Count == 0 && underlays.Count == 0) return "none";
        // an icon object shows its own state when the appearance names none (AppearanceTables.Fill, M4c slice 4; M10x D2: the hair)
        string state = string.IsNullOrEmpty(a.icon_state) && a.icon is Anomaly.Core.DmShim.icon made ? made.dm_state : a.icon_state;
        return Format(icon, string.IsNullOrEmpty(state) ? null : state, top && !keepDir ? 0 : WireDir(a.dir), Color(a.color), (int)Math.Round(a.alpha),
            a.layer, (int)a.plane, (int)Math.Round(a.pixel_x), (int)Math.Round(a.pixel_y), (int)Math.Round(a.pixel_w), (int)Math.Round(a.pixel_z),
            Transform(a.transform), (int)a.appearance_flags & 0xFFFF, (int)a.blend_mode, overlays, underlays);
    }

    private static void AddItem(List<string> into, object item)
    {
        switch (item)
        {
            case IDmAppearance ap: into.Add(Of(ap, false)); break;
            case string s when s.Length > 0: into.Add(Format(null, s, 0, "#ffffffff", 255, 0f, 0, 0, 0, 0, 0, "-", 0, 0, new List<string>(), new List<string>())); break;
            case DmResource r: into.Add(Format(r.Path, null, 0, "#ffffffff", 255, 0f, 0, 0, 0, 0, 0, "-", 0, 0, new List<string>(), new List<string>())); break;
        }
    }

    public static string Of(ReplicaWorld w, TableRef r)
    {
        if (r.IsNone) return "none";
        Assert.True(w.TryGetAppearance(r, out AppearanceEntry e), "an appearance " + r + " that the tables do not hold");
        var overlays = new List<string>();
        var underlays = new List<string>();
        foreach (TableRef o in e.Overlays) overlays.Add(Of(w, o));
        foreach (TableRef o in e.Underlays) underlays.Add(Of(w, o));
        return Format(w.ResolveResource(e.Icon), w.ResolveString(e.IconState), e.Dir, e.ColorMatrix != null ? "matrix" : "#" + e.Rgba.ToString("x8", CultureInfo.InvariantCulture), e.Alpha,
            e.Layer, e.Plane, e.PixelX, e.PixelY, e.PixelW, e.PixelZ,
            e.Transform == null ? "-" : string.Join(",", e.Transform.Select(f => f.ToString("R", CultureInfo.InvariantCulture))), e.AppearanceFlags, e.BlendMode, overlays, underlays);
    }

    private static string Format(string icon, string state, int dir, string color, int alpha, float layer, int plane, int px, int py, int pw, int pz,
        string transform, int flags, int blend, List<string> overlays, List<string> underlays) =>
        $"[icon={icon};state={state};dir={dir};color={color};alpha={alpha};layer={layer.ToString("R", CultureInfo.InvariantCulture)};plane={plane};px={px},{py};pw={pw},{pz};tf={transform};fl={flags};bm={blend};ov=({string.Join("|", overlays)});un=({string.Join("|", underlays)})]";

    private static IEnumerable<object> Items(DmList list)
    {
        for (int i = 1; i <= list.Count; i++) yield return list[(float)i];
    }

    private static string IconPath(object icon) => icon switch
    {
        DmResource r => r.Path,
        string s when s.Length > 0 => s,
        Anomaly.Core.DmShim.icon made => made.dm_resource(), // an /icon object: its file, or its Blend recipe (M6 slice 7, D155)
        _ => null,
    };

    private static int WireDir(float dir) => dir is 1 or 2 or 4 or 5 or 6 or 8 or 9 or 10 ? (int)dir : 0;

    private static string Color(object color)
    {
        if (color is DmList list)
        {
            // BYOND's 3 x 3 identity is no tint at all.
            bool identity = list.Count == 9;
            for (int i = 1; identity && i <= 9; i++)
                identity = list[(float)i] is float f && f == (i == 1 || i == 5 || i == 9 ? 1f : 0f);
            return identity ? "#ffffffff" : "matrix";
        }
        if (color is not string text || text.Length == 0) return "#ffffffff";
        string hex = text.TrimStart('#');
        uint rgba = hex.Length == 6 ? (Convert.ToUInt32(hex, 16) << 8) | 0xFF : Convert.ToUInt32(hex, 16);
        return "#" + rgba.ToString("x8", CultureInfo.InvariantCulture);
    }

    private static string Transform(object transform)
    {
        if (transform is not matrix m) return "-";
        float[] v = { m.a, m.b, m.c, m.d, m.e, m.f };
        if (v.SequenceEqual(new[] { 1f, 0f, 0f, 0f, 1f, 0f })) return "-";
        return string.Join(",", v.Select(f => f.ToString("R", CultureInfo.InvariantCulture)));
    }
}

/// <summary>An entity as a replica has it in version 2: the version-1 fields, the capsule fields and the resolved appearance.</summary>
internal readonly record struct EntityViewV2(EntityView Base, bool HasPos, int PosX, int PosY, byte Yaw, byte Posture, string Appearance)
{
    public override string ToString() => $"{Base} pos {(HasPos ? "capsule " : "")}{PosX},{PosY} yaw {Yaw} posture {Posture} {Appearance}";
}

/// <summary>
/// The full-scan oracle of version 2: what every entity should look like to a client, read from the world and from the test's
/// capsule states, independent of the replicator. Capsule-ness is sticky, as the replicator's: once a mob has been a capsule
/// its records carry Pos.
/// </summary>
internal sealed class WorldOracleV2
{
    private readonly WorldOracle _base = new();
    private readonly HashSet<long> _capsules = new();

    public SortedDictionary<long, EntityViewV2> Scan(World world, FakeLocomotion locomotion)
    {
        SortedDictionary<long, EntityView> v1 = _base.Scan(world);
        var result = new SortedDictionary<long, EntityViewV2>();
        foreach (EntityView e in v1.Values)
        {
            world.Objects.TryGet(e.Id, out object obj);
            var movable = (atom_movable)obj;
            bool capsule = movable is mob && locomotion.Capsules.ContainsKey(e.Id);
            if (capsule) _capsules.Add(e.Id);
            bool sticky = _capsules.Contains(e.Id);
            int px = e.X * 256, py = e.Y * 256;
            byte yaw = 0;
            if (capsule)
            {
                MotionState s = locomotion.Capsules[e.Id];
                px = s.X;
                py = s.Y;
                yaw = (byte)(s.Yaw >> 8);
            }
            else if (sticky)
            {
                yaw = _lastYaw.GetValueOrDefault(e.Id);
            }

            _lastYaw[e.Id] = yaw;
            byte posture = movable is mob_living living ? ReplicatedFields.WirePosture(living) : (byte)0;
            result.Add(e.Id, new EntityViewV2(e, sticky, px, py, sticky ? yaw : (byte)0, posture, AppearanceText.Of(movable, true)));
        }

        return result;
    }

    private readonly Dictionary<long, byte> _lastYaw = new();
}

internal static class V2Views
{
    public static EntityViewV2 ViewV2(this ReplicaWorld replica, ReplicaEntity e) =>
        new(e.View(), e.State.HasPos, e.State.PosX, e.State.PosY, e.State.Yaw, e.State.Posture, AppearanceText.Of(replica, e.State.Appearance));

    public static List<string> Differences(ReplicaWorld replica, SortedDictionary<long, EntityViewV2> truth, SessionView viewer = null, World world = null)
    {
        var differences = new List<string>();
        foreach (ReplicaEntity r in replica.Entities)
        {
            // the truth's scan is the version 1 entity set: the visuals of version 10 (a human's eyelids since M10x) have their own tests
            if (r.IsVisual)
                continue;
            EntityViewV2 e = replica.ViewV2(r);
            if (!truth.TryGetValue(r.Id, out EntityViewV2 t))
            {
                // Protocol 22 (D151): a session whose mob sees the observer layer (a ghost) holds its entities too; the truth is
                // what a living viewer sees.
                if (!(viewer != null && world != null && SeesLayerEntity(viewer, world, r.Id)))
                    differences.Add($"extra {e}");
            }
            else if (t != e) differences.Add($"replica {e} / world {t}");
        }

        foreach (EntityViewV2 t in truth.Values)
            if (!replica.TryGet(t.Base.Id, out _)) differences.Add($"missing {t}");
        return differences;
    }

    private static bool SeesLayerEntity(SessionView viewer, World world, long id)
    {
        mob eye = viewer.Client?.mob;
        return eye != null && world.Objects.TryGet(id, out object o) && o is atom_movable m
            && m.invisibility > Defines.SEE_INVISIBLE_LIVING && m.invisibility <= eye.see_invisible;
    }

    /// <summary>The cells a replica holds against the world's (solidity by the plain predicate, the turf appearance as text).</summary>
    public static List<string> CellDifferences(ReplicaWorld replica, World world)
    {
        var differences = new List<string>();
        int size = replica.Map.Width;
        for (int y = 1; y <= replica.Map.Height; y++)
        {
            for (int x = 1; x <= size; x++)
            {
                bool solid = DenseSolidity.IsSolid(world, x, y, 1);
                if (replica.IsSolid(x, y, 1) != solid) differences.Add($"cell {x},{y} solid: replica {replica.IsSolid(x, y, 1)}, world {solid}");
                turf t = MapView.TurfAt(world, x, y, 1);
                string expected = AppearanceText.Of(t, true);
                string have = AppearanceText.Of(replica, replica.TurfAppearance(x, y, 1));
                if (expected != have) differences.Add($"cell {x},{y} appearance: replica {have}, world {expected}");
                // Version 3: the faces, the opacity and the starlight against the raw predicates (the same ones the replicator reads).
                TileEdges faces = LocomotionSeam.FacesOf(world, x, y, 1);
                if (replica.FacesOf(x, y, 1) != faces) differences.Add($"cell {x},{y} faces: replica {replica.FacesOf(x, y, 1)}, world {faces}");
                if (replica.IsOpaque(x, y, 1) != CellState.IsOpaque(world, x, y, 1)) differences.Add($"cell {x},{y} opaque: replica {replica.IsOpaque(x, y, 1)}");
                if (replica.IsStarlit(x, y, 1) != CellState.IsStarlit(world, x, y, 1)) differences.Add($"cell {x},{y} starlit: replica {replica.IsStarlit(x, y, 1)}");
            }
        }

        return differences;
    }

    public static string Describe(long tick, List<string> differences)
    {
        var text = new StringBuilder($"tick {tick}: {differences.Count} differences");
        foreach (string line in differences.Take(10)) text.Append("\n  ").Append(line);
        return text.ToString();
    }
}

/// <summary>
/// Humans that move continuously (a fake capsule each, the tile following the capsule through tg's <c>forceMove</c>) and
/// take damage: their hidden numbers (<see cref="HiddenValues"/>) are written into tg's damage vars and their visible look
/// (tg's damage overlays per zone) into <c>overlays</c>, as tg's update_damage_overlays does. A stand-in for the real
/// medical code (M2 P1, P2, P3), which the integration step runs through the same oracle. Deterministic from its seed and
/// independent of any replicator.
/// </summary>
internal sealed class HumanScenario
{
    /// <summary>Numbers that must never reach a client: distinctive floats (exactly representable), so a byte scan means something.</summary>
    public static readonly float[] HiddenValues = { 37.25f, 12.875f, 91.5f, 0.3125f, 61.0625f, 148.75f, 7.40625f, 203.125f };

    public static readonly string[] Zones = { "head", "chest", "l_arm", "r_arm", "l_leg", "r_leg" };

    private readonly ReplicationWorld _world;
    private readonly FakeLocomotion _locomotion;
    private readonly Random _random;
    private readonly double _moveChance;
    private readonly bool _damage;
    private readonly bool _churn;
    private readonly int _protected;
    private readonly bool _real;
    private int _names;

    public List<mob_living_carbon_human> Humans { get; } = new();

    /// <param name="realMedicine">M2 integration: the damage and the posture are tg's own (apply_damage of a zone, the toxin and
    /// oxygen procs, Knockdown and getting up), so the overlays, the posture and the hidden numbers are what tg makes of
    /// them; otherwise the vars and overlays are written as P5's stand-in.</param>
    public HumanScenario(ReplicationWorld world, FakeLocomotion locomotion, int count, int seed, double moveChance = 0.6, bool damage = true, bool churn = true, int protectedHumans = 2, bool realMedicine = false)
    {
        _real = realMedicine;
        _world = world;
        _locomotion = locomotion;
        _random = new Random(seed);
        _moveChance = moveChance;
        _damage = damage;
        _churn = churn;
        _protected = protectedHumans;
        // tg's strings/ files, as the server mounts them (ServerHost.MountTgStrings): a full heal turns tg's wounds into scars,
        // whose texts come from strings/wounds (M6)
        if (realMedicine && TgStrings.Locate(AppContext.BaseDirectory, System.IO.Directory.GetCurrentDirectory()) is string tg)
            TgStrings.Mount(DmWorldState.Of(world.World).Files, tg);
        using (world.World.Enter())
        {
            for (int i = 0; i < count; i++) Humans.Add(Spawn());
        }
    }

    private mob_living_carbon_human Spawn()
    {
        int x = _random.Next(3, _world.Size - 2), y = _random.Next(3, _world.Size - 2);
        var h = Dm.@new<mob_living_carbon_human>(Builtins.locate(x, y, 1));
        h.name = "Human " + (++_names);
        _locomotion.Capsules[h.dm_id] = MotionState.AtTile(x, y, 1, (ushort)_random.Next(65536));
        return h;
    }

    /// <summary>One tick of the humans' lives, between two world steps.</summary>
    public void Act()
    {
        using (_world.World.Enter())
        {
            for (int i = 0; i < Humans.Count; i++)
            {
                mob_living_carbon_human h = Humans[i];
                if (_locomotion.Capsules.TryGetValue(h.dm_id, out MotionState s) && _random.NextDouble() < _moveChance)
                {
                    int lo = 256 * 2 + 40, hi = 256 * (_world.Size - 2) - 40;
                    int x = Math.Clamp(s.X + _random.Next(-85, 86), lo, hi), y = Math.Clamp(s.Y + _random.Next(-85, 86), lo, hi);
                    ushort yaw = _random.Next(3) == 0 ? (ushort)_random.Next(65536) : s.Yaw;
                    var next = s with { X = x, Y = y, Yaw = yaw };
                    if (next.TileX != s.TileX || next.TileY != s.TileY)
                        Waiting.NoSleep(h.forceMove(Builtins.locate(next.TileX, next.TileY, 1)));
                    if (_random.Next(8) == 0) Waiting.NoSleep(h.setDir(new float[] { Dm.NORTH, Dm.SOUTH, Dm.EAST, Dm.WEST }[_random.Next(4)]));
                    _locomotion.Capsules[h.dm_id] = next;
                }

                if (_damage && _real)
                {
                    if (_random.Next(20) == 0) HurtTg(h);
                    if (_random.Next(60) == 0) Waiting.NoSleep(h.Knockdown(2 * Defines.SECONDS));
                }
                else if (_damage && _random.Next(20) == 0) Hurt(h);
                if (_damage && !_real && _random.Next(60) == 0)
                {
                    bool lying = h.body_position != Defines.LYING_DOWN;
                    h.body_position = lying ? Defines.LYING_DOWN : Defines.STANDING_UP;
                    h.lying_angle = lying ? (_random.Next(2) == 0 ? 90 : 270) : 0;
                }

                if (_random.Next(150) == 0) h.name = "Human " + (++_names);
            }

            if (_churn && Humans.Count > _protected + 2 && _random.Next(60) == 0)
            {
                int victim = _random.Next(_protected, Humans.Count);
                _locomotion.Capsules.Remove(Humans[victim].dm_id);
                GlobalProcs.qdel(Humans[victim]);
                Humans[victim] = Spawn();
            }
        }
    }

    /// <summary>tg's damage: brute or burn to a zone, toxin or oxygen, now and then a full heal (the overlays are tg's).</summary>
    public void HurtTg(mob_living_carbon_human h)
    {
        float amount = HiddenValues[_random.Next(HiddenValues.Length)] / 4;
        switch (_random.Next(6))
        {
            case 0:
            case 1:
                Waiting.NoSleep(h.apply_damage(amount, Defines.BRUTE, Zones[_random.Next(Zones.Length)]));
                break;
            case 2:
            case 3:
                Waiting.NoSleep(h.apply_damage(amount, Defines.BURN, Zones[_random.Next(Zones.Length)]));
                break;
            case 4:
                Waiting.NoSleep(_random.Next(2) == 0 ? h.adjust_tox_loss(amount) : h.adjust_oxy_loss(amount));
                break;
            default:
                Waiting.NoSleep(h.fully_heal(Defines.HEAL_ALL));
                break;
        }
    }

    /// <summary>Damage of a zone: the hidden numbers into tg's vars, the visible look into the overlays.</summary>
    public void Hurt(mob_living_carbon_human h)
    {
        float brute = HiddenValues[_random.Next(HiddenValues.Length)], burn = HiddenValues[_random.Next(HiddenValues.Length)];
        h.bruteloss = brute;
        h.fireloss = burn;
        h.toxloss = HiddenValues[_random.Next(HiddenValues.Length)];
        h.oxyloss = HiddenValues[_random.Next(HiddenValues.Length)];
        h.health = HiddenValues[_random.Next(HiddenValues.Length)];
        var list = new List<object>();
        foreach (string zone in Zones)
        {
            if (_random.Next(3) == 0) continue;
            int b = _random.Next(0, 4), f = _random.Next(0, 4);
            if (b == 0 && f == 0) continue;
            list.Add(ReplicatorV2Tests.Layered("icons/mob/human/dam_human.dmi", $"{zone}_{b}{f}", layer: -20 + Array.IndexOf(Zones, zone)));
        }

        if (_random.Next(2) == 0) h.overlays = Dm.list(list.ToArray());
        else
        {
            h.overlays.Cut();
            foreach (object o in list) h.overlays.Add(o); // in place: no hint, the sweep finds it
        }
    }
}
