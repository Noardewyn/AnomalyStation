using System.Linq;
using Godot;
using Anomaly.Client.Render;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Play;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Client;

/// <summary>What <see cref="EntityLayer.TryPick"/> found under the crosshair.</summary>
/// <param name="EntityId">The entity's world id (what a click action names, <c>ActionMessage.Target</c>).</param>
/// <param name="Distance">Along the ray, in metres.</param>
/// <param name="Point">Where the ray met it.</param>
public readonly record struct EntityPick(long EntityId, float Distance, Vector3 Point);

/// <summary>
/// One node per replicated entity (D104). A mob or an item with an appearance is a <see cref="Billboard"/>: tg's own picture composed from its
/// appearance (<see cref="AppearanceTextures"/>), a Y-axis billboard whose frame is chosen by the angle between the viewer and the mob's facing
/// (the replicated yaw), or tg's rotated sprite on a floor quad when it lies (<c>Posture</c>). An entity that has no appearance (or whose icons
/// are not converted) keeps M1's placeholder: a capsule with a nose for a mob, a box for anything else. Poses come from <see cref="Poses"/>
/// (M2 P8's source: the interpolated samples, the predicted own capsule) or, until one is set, from the replica's own samples. The own
/// capsule can be hidden (first person, D101). Names are never drawn above heads (D56); a debug overlay can show them (F4).
/// <para>
/// <see cref="TryPick"/> is the pick interface of the click ray (M2 P8): the entity whose picture the ray hits, walls in front of it hiding it.
/// </para>
/// </summary>
public partial class EntityLayer : Node3D
{
    private static readonly Color[] Palette =
    {
        new(0.90f, 0.35f, 0.30f), new(0.30f, 0.65f, 0.90f), new(0.95f, 0.75f, 0.25f), new(0.45f, 0.80f, 0.40f),
        new(0.75f, 0.45f, 0.90f), new(0.95f, 0.55f, 0.20f), new(0.35f, 0.85f, 0.75f), new(0.90f, 0.50f, 0.70f),
    };

    private readonly Dictionary<long, Entry> _entries = new();
    private readonly List<Entry> _list = new();
    private readonly HashSet<long> _seenOthers = new();
    private readonly HashSet<long> _movedOthers = new();
    private readonly Dictionary<long, Entry> _visuals = new(); // M4v slice 3 (protocol version 10): tg's short-lived visuals (NetField.VisHost)
    private readonly List<long> _emptyHosts = new();
    private readonly Dictionary<long, List<Entry>> _insets = new(); // M8x (D210): a host's visual helpers (/atom/movable/visual), drawn into its picture
    private VisInsets _insetLooks;

    private IWorldFeed _feed;
    private ReplicaWorld _replica;
    private ReplicaTables _tables;
    private CapsuleMesh _capsule;
    private BoxMesh _nose;
    private BoxMesh _marker;
    private bool _namesBuilt;
    private double _lastUpdateAt = -1;
    private MapRenderer _map;
    private readonly ThrowClock _throws = new();
    private static Shader _spinShader;
    private static Shader _animateShader;
    private double _renderTick; // the render tick of this Update (animate() chains play on the server's clock)
    private long _ownId; // the own mob of this Update
    private bool _hideOwn; // first person: the own mob is not drawn

    /// <summary>Where the mirror that reflects the view now sees the world from (tile coordinates; null: no mirror): the own body, not drawn in first person, shows it its side.</summary>
    public Vector2? MirrorViewer { get; set; }

    /// <summary>
    /// shortcut: a mob farther than this from the mirror's own eye (tiles) keeps the main camera's facing in the mirror too (a long reach
    /// costs a second picture for every mob it covers, every frame; a mirror's own small room rarely has one this far in view anyway).
    /// </summary>
    private const float MirrorReflectRangeTiles = 16f;

    /// <summary>D176: the own mob's gun fired (tg's firing effect on the own mob), with the flash's colour: the first-person gun kicks and flashes (ViewModel).</summary>
    public Func<Color, ShotFlash> OwnFired { get; set; }

    /// <summary>D285: where the own first-person gun's muzzle looks to be (ViewModel.TryMuzzle), false without one.</summary>
    public delegate bool MuzzleQuery(out Vector3 world);

    /// <summary>D285: the own bolts start at this muzzle and join their true line (from the eye, DV94) over <see cref="BoltBlendMetres"/>.</summary>
    public MuzzleQuery OwnMuzzle { get; set; }

    /// <summary>
    /// D285: how far a bolt flies from the gun to its line, how near the muzzle its line must start to be the own (metres), and how near a mob's
    /// middle it must start to be that mob's (metres: its line starts at its eye, DV94).
    /// </summary>
    private const float MuzzleBlendMetres = 6f, OwnShotReach = 3f, ShooterReach = 1f;
    private static readonly Vector3 OwnMuzzleFallback = new(0.2f, -0.25f, -0.5f); // in the camera's space

    /// <summary>D285: a shot and a flash of one firing: the flash takes the shot's colour (the owner, 2026-10-28: a laser's flash was not the laser's colour).</summary>
    private const double SameFiringSeconds = 0.3;

    private const float SameFiringMetres = 3f;

    private readonly List<(ShotFlash Flash, Vector3 At, double Time)> _recentFlashes = new();
    private readonly List<(Vector3 Origin, Color Colour, double Time)> _recentShots = new();

    /// <summary>How many mobs and other entities are drawn.</summary>
    public int MobCount { get; private set; }

    public int OtherCount => _list.Count - MobCount;

    /// <summary>How many short-lived visuals (tg's <c>flick_overlay_view</c>, protocol version 10) are drawn now.</summary>
    public int VisualCount => _visuals.Count;

    /// <summary>Distinct other mobs seen so far, and how many of them were seen to move (the run summary).</summary>
    public int OthersSeen => _seenOthers.Count;

    public int OthersMoved => _movedOthers.Count;

    /// <summary>
    /// The fastest drawn speed of any entity over one frame, in metres per second, ignoring teleports (a step of more than
    /// 2.5 tiles in a frame): a walk of one tile per 3 ticks is 13.3 m/s, so a value near it (and not much above) means the
    /// motion is smooth; a jump from tick to tick would show as a spike.
    /// </summary>
    public double PeakSpeed { get; private set; }

    /// <summary>The icons and compositor behind the pictures (M2 P8's HUD draws screen objects through the same instance); null before <see cref="Bind"/>.</summary>
    public AppearanceTextures Textures { get; private set; }

    /// <summary>How many sides the pictures of an appearance have (1, 4 or 8); 1 while its icons are being read.</summary>
    public int DirectionsOf(Anomaly.Protocol.TableRef appearance) => Textures != null && _tables != null ? Textures.Directions(_tables, appearance) : 1;

    /// <summary>Where a thing at rest on a tile is drawn (a closet's shelf, a table's top: <see cref="MapRenderer.TryRest"/>); null: on the floor.</summary>
    public delegate bool RestResolver(long entityId, int x, int y, int z, out Vector3 world, out bool onSlot);

    /// <summary>The resolver of resting places (docs/ai/specs/models.md); null: things lie on the floor.</summary>
    public RestResolver Rest { get; set; }

    /// <summary>The height a body lying on a tile is drawn at, above the floor (a bed's top: <see cref="MapRenderer.TryLieOn"/>, DV127); false: on the floor.</summary>
    public delegate bool LieResolver(int x, int y, int z, out float height);

    /// <summary>The resolver of where lying bodies lie; null: on the floor.</summary>
    public LieResolver LieOn { get; set; }

    /// <summary>D239: the resolver of where standing mobs stand (a table's or a crate's top: <see cref="MapRenderer.TryStandOn"/>); null: on the floor.</summary>
    public LieResolver StandOn { get; set; }

    /// <summary>D325: the height a thing on a ramp is drawn at, by where on the tile it is (tg's stairs: <see cref="MapRenderer.TryRampOn"/>, DV370).</summary>
    public delegate bool RampResolver(float x, float y, int z, out float height);
    public RampResolver RampOn { get; set; }
    public Func<bool> OwnCanStandOn { get; set; }

    /// <summary>The pose source (M2 P8's <see cref="IPoseSource"/>); null: the layer reads the replica's samples itself.</summary>
    public IPoseSource Poses { get; set; }

    /// <summary>Entities drawn as pictures (the rest are placeholders), for the run summary.</summary>
    public int PictureCount { get; private set; }

    private IStaticPropHost _host;

    /// <summary>
    /// Starts following <paramref name="feed"/>. <paramref name="textures"/> (null: the layer makes its own) is the picture source the map
    /// renderer shares; <paramref name="host"/> (the map renderer, M3 P6) draws the static props itself: an entity it claims gets no node
    /// until it moves (the static-versus-moving rule), when the host hands it back.
    /// </summary>
    public void Bind(IWorldFeed feed, AppearanceTextures textures = null, IStaticPropHost host = null)
    {
        _feed = feed;
        _host = host;
        _capsule = new CapsuleMesh { Radius = TileSpace.CapsuleRadius, Height = TileSpace.CapsuleHeight };
        _nose = new BoxMesh { Size = new Vector3(0.18f, 0.18f, 0.3f) };
        _marker = new BoxMesh { Size = new Vector3(0.6f, 0.5f, 0.6f) };
        try
        {
            _replica = feed.Replica;
        }
        catch (NotImplementedException)
        {
            _replica = null; // a feed of version 1 has no appearance tables: placeholders only
        }
        if (_replica != null)
        {
            Textures = textures ?? new AppearanceTextures();
            _tables = Textures.TablesOf(_replica);
            _insetLooks = new VisInsets(_tables);
            if (!IconPaths.Exists)
                GD.PushWarning($"[anomaly] icons: {IconPaths.Root} holds no converted icons (run tools/Anomaly.Tools.Icons); entities are drawn as placeholders");
        }
        // the join's things too are built over the first frames, nearest first (mobs and visuals at once, as in play)
        foreach (ReplicaEntity entity in feed.Entities)
            Enqueue(entity);
        feed.Spawned += Enqueue;
        feed.Despawned += Remove;
        if (_replica != null)
            _replica.SoundsReceived += HearFootsteps;
        if (_host != null)
            _host.PropMoved += AddNode;
    }

    public override void _ExitTree()
    {
        if (_feed != null)
        {
            _feed.Spawned -= Enqueue;
            _feed.Despawned -= Remove;
        }
        if (_replica != null)
            _replica.SoundsReceived -= HearFootsteps;
        if (_host != null)
            _host.PropMoved -= AddNode;
    }

    // A point two tiles before the eye (tile coordinates), on the ground; looking straight down, the top of the screen is before it.
    private static (double X, double Y) FrontViewer(Camera3D camera, double viewerX, double viewerY)
    {
        Vector3 ahead = -camera.GlobalBasis.Z;
        if (new Vector2(ahead.X, ahead.Z).Length() < 0.1f)
            ahead = camera.GlobalBasis.Y;
        Vector2 flat = new Vector2(ahead.X, -ahead.Z).Normalized() * 2f;
        return (viewerX + flat.X, viewerY + flat.Y);
    }

    private static readonly byte[] SelfSideDirs = { ByondDir.South, ByondDir.East, ByondDir.North, ByondDir.West };
    private static readonly StringName[] SelfSideParams = { "anomaly_self_south", "anomaly_self_east", "anomaly_self_north", "anomaly_self_west" };
    private readonly ImageTexture[] _selfSides = new ImageTexture[4];
    private readonly ImageTexture[] _selfPublishedSides = new ImageTexture[4];
    private float _selfYaw;
    private bool _selfPublished;

    // The own hidden body's four sides and facing for the reflecting surfaces (PublishSelfReflection): a pane beside the body shows its side,
    // not the front (the owner, 2026-11-18). A lying body or a still icon shows its one picture on every side.
    // shortcut: four sides even for an eight-direction icon (the mobs' are four); its diagonals need four more globals.
    private void NoteSelfSides(IAppearanceSource tables, TableRef appearance, double seconds, Dictionary<AppearanceEntry, IReadOnlyList<FilterEntry>> filterLooks,
        GodotPicture picture, bool lying, byte yaw)
    {
        bool turns = !lying && Textures.Directions(tables, appearance) > 1;
        for (int i = 0; i < SelfSideDirs.Length; i++)
            _selfSides[i] = turns && Textures.TryGet(tables, appearance, SelfSideDirs[i], seconds, out _, out GodotPicture side, rootOpaque: false, filterLooks: filterLooks)
                ? side.Texture : picture.Texture;
        _selfYaw = yaw * MathF.Tau / 256f;
    }

    // The own hidden body's picture and place for the reflecting surfaces' shaders (shaders/self_reflection.gdshaderinc); null: none (third
    // person, no body, nothing drawn).
    private void PublishSelfReflection(Entry own)
    {
        Billboard board = own?.Billboard;
        GodotObject picture = board != null && own.Node.Visible ? board.PictureTexture.AsGodotObject() : null;
        if (picture == null || _selfSides[0] == null)
        {
            if (_selfPublished)
                RenderingServer.GlobalShaderParameterSet("anomaly_self_at", Vector4.Zero);
            _selfPublished = false;
            return;
        }
        for (int i = 0; i < _selfSides.Length; i++)
        {
            if (ReferenceEquals(_selfSides[i], _selfPublishedSides[i]))
                continue;
            RenderingServer.GlobalShaderParameterSet(SelfSideParams[i], _selfSides[i]);
            _selfPublishedSides[i] = _selfSides[i];
        }
        RenderingServer.GlobalShaderParameterSet("anomaly_self_yaw", _selfYaw);
        RenderingServer.GlobalShaderParameterSet("anomaly_self_sway", board.Sway);
        Vector3 at = board.GlobalPosition;
        SpriteRect rect = board.Rect;
        RenderingServer.GlobalShaderParameterSet("anomaly_self_at", new Vector4(at.X, at.Y, at.Z, 1f));
        RenderingServer.GlobalShaderParameterSet("anomaly_self_rect", new Vector4((float)rect.Left, (float)rect.Right, (float)rect.Bottom, (float)rect.Top));
        _selfPublished = true;
    }

    /// <summary>Places every entity at <paramref name="renderTick"/>.</summary>
    public void Update(double renderTick, double nowSeconds, long ownId, bool hideOwn, bool showNames)
    {
        if (showNames && !_namesBuilt)
            BuildNameLabels();
        // The real time between frames (the engine's own frame delta is smoothed and not the elapsed time).
        double deltaSeconds = _lastUpdateAt < 0 ? 0 : nowSeconds - _lastUpdateAt;
        _renderTick = renderTick;
        _lastUpdateAt = nowSeconds;
        _ownId = ownId;
        _hideOwn = hideOwn;
        Camera3D camera = GetViewport()?.GetCamera3D();
        double viewerX = 0, viewerY = 0;
        bool hasViewer = camera != null;
        if (hasViewer)
        {
            Vector3 eye = camera.GlobalPosition;
            viewerX = eye.X / TileSpace.TileSize + 0.5;
            viewerY = -eye.Z / TileSpace.TileSize + 0.5;
        }
        using (Dev.FrameProbe.Scope("entities/AddPending"))
            AddPending(viewerX, viewerY);

        // DV46: where the own body is drawn and the own mob's server tile, for the particles of the own body (NearEyeFade.OwnShift).
        bool hasOwn = _entries.TryGetValue(ownId, out Entry ownEntry) && TryPose(ownEntry.Entity, renderTick, out _ownPose);
        FindInsets();
        int pictures = 0;
        foreach (Entry e in _list)
        {
            if (!TryPose(e.Entity, renderTick, out RenderPose pose))
                continue;
            if (NearEyeFade.Applies(e.Entity.TypePath))
            {
                if (!e.ShiftKnown && hasOwn)
                {
                    EntityState mine = ownEntry.Entity.State;
                    EntityState at = e.Entity.State;
                    (e.ShiftX, e.ShiftY) = NearEyeFade.OwnShift((int)at.X, (int)at.Y, at.Z, (int)mine.X, (int)mine.Y, mine.Z, _ownPose.X, _ownPose.Y);
                    e.ShiftKnown = true;
                }
                pose = pose with { X = pose.X + e.ShiftX, Y = pose.Y + e.ShiftY };
            }
            Entry pointedAt = null;
            if (e.Entity.TypePath?.StartsWith(PointArrowType, StringComparison.Ordinal) == true
                && _tables.TryGetAppearance(e.Entity.State.Appearance, out AppearanceEntry pointAppearance))
            {
                if (!e.HasLast)
                {
                    // tg's slide (point.dm: animate to the target, 1.7 ds, EASE_OUT) starts at the pointer: its tile at the hand's height
                    e.PointStart = new Vector3(TileSpace.WorldX(pose.X), TileSpace.WorldY(pose.Z) + PointHandHeight, TileSpace.WorldZ(pose.Y));
                    e.PointBorn = nowSeconds;
                }
                (float pointX, float pointY) = PointArrowPlacement.Target(pose.X, pose.Y, pointAppearance);
                pose = pose with { X = pointX, Y = pointY };
                // DV232: the server's aimed point (the arrow's shot line); an arrow on a thing follows it, kept at the same offset from it
                if (!e.HasLast && e.Entity.Shot is ShotState aimed)
                {
                    const float m = TileSpace.TileSize / 32f;
                    e.PointSpot = new Vector3((float)(aimed.OriginX / 32.0 - 0.5) * TileSpace.TileSize, TileSpace.WorldY(pose.Z) + (float)aimed.HeightPixels * m,
                        -(float)(aimed.OriginY / 32.0 - 0.5) * TileSpace.TileSize);
                    if (PointedEntry(e, pointX, pointY, pose.Z) is Entry thing)
                        (e.PointFollow, e.PointSpot) = (thing.Entity.Id, e.PointSpot - thing.LastPosition);
                }
                pointedAt = PointedEntry(e, pointX, pointY, pose.Z);
            }
            // DV434: a standing mob's picture steps as it moves over the ground
            bool steps = e.Entity.IsMob() && !PostureBits.IsLying(pose.Posture) && !PostureBits.IsIncorporeal(pose.Posture);
            if (steps && e.HasPose && deltaSeconds > 0)
                (e.Walk ??= new WalkSway()).Step(Math.Sqrt((pose.X - e.Pose.X) * (pose.X - e.Pose.X) + (pose.Y - e.Pose.Y) * (pose.Y - e.Pose.Y)) * TileSpace.TileSize, deltaSeconds,
                    e.Entity.IsExosuit() ? WalkSway.ExosuitStepMetres : e.Entity.HasPos ? WalkSway.CapsuleStepMetres : WalkSway.TileStepMetres);
            e.Pose = new EntityPose(pose.X, pose.Y, pose.Z, pose.Dir);
            e.HasPose = true;
            var position = new Vector3(TileSpace.WorldX(pose.X), TileSpace.WorldY(pose.Z) + pose.Rise, TileSpace.WorldZ(pose.Y)); // DV371: a level change's rise
            // DEVIATION(DV370): on the stairs' flight a thing is drawn up it
            if (RampOn != null && RampOn(pose.X, pose.Y, (int)MathF.Round(pose.Z), out float ramp))
                position.Y += ramp;
            if (e.HasLast && deltaSeconds > 0.0005)
            {
                float step = position.DistanceTo(e.LastPosition);
                if (step < 2.5f * TileSpace.TileSize)
                {
                    PeakSpeed = Math.Max(PeakSpeed, step / deltaSeconds);
                }
            }
            e.LastPosition = position;
            e.HasLast = true;
            // A thing at rest on a tile lies on what is there: a closet's shelf (its own offset is not used), a table's top.
            e.OnSlot = false;
            if (Rest != null && !e.Entity.IsMob() && PropKinds.IsItem(e.Entity.TypePath) && (AtRest(pose, out int tx, out int ty) || RestsWhereLanded(e.Entity, pose, out tx, out ty))
                && Rest(e.Entity.Id, tx, ty, (int)MathF.Round(pose.Z), out Vector3 rest, out bool onSlot))
            {
                position = onSlot ? rest : new Vector3(position.X, rest.Y, position.Z);
                e.OnSlot = onSlot;
            }
            // DEVIATION(DV127): a body lying on a bed, a stretcher or a table lies on its top (by the tile it is drawn over, so a patient wheeled on a stretcher stays on it)
            // (a buckled one by its server tile, its bed's: drawn with the bed mid-step, the tile it is drawn over may not be the bed's yet)
            else if (LieOn != null && e.Entity.IsMob() && PostureBits.IsLying(pose.Posture)
                && (PostureBits.IsBuckled(e.Entity.State.Posture)
                    ? LieOn(e.Entity.State.X, e.Entity.State.Y, e.Entity.State.Z, out float lieHeight)
                    : LieOn((int)MathF.Round(pose.X), (int)MathF.Round(pose.Y), (int)MathF.Round(pose.Z), out lieHeight)))
                position += new Vector3(0f, lieHeight, 0f);
            // DEVIATION(DV154): a standing mob on a table, a crate or another structure he climbed stands on its top (tg's elevation)
            else if (StandOn != null && e.Entity.IsMob() && !PostureBits.IsLying(pose.Posture) && !PostureBits.IsIncorporeal(pose.Posture)
                && (e.Entity.Id != ownId || OwnCanStandOn?.Invoke() != false)
                && StandOn((int)MathF.Round(pose.X), (int)MathF.Round(pose.Y), (int)MathF.Round(pose.Z), out float standHeight))
                position += new Vector3(0f, standHeight, 0f);
            // the owner (2026-10-26): the arrow stands over the thing pointed at where the 3D view draws it (on a table, a mob's head)
            if (e.PointSpot is Vector3 pointSpot)
            {
                // DV232: on the aimed point (or the followed thing's), drawn over everything (Billboard.OnTop), after tg's slide from the pointer
                position = e.PointFollow == 0 ? pointSpot
                    : _entries.TryGetValue(e.PointFollow, out Entry followed) && followed.HasLast ? followed.LastPosition + pointSpot : e.LastPosition;
                position = e.PointStart.Lerp(position, PointArrowPlacement.Slide(nowSeconds - e.PointBorn));
                // the same size on the screen near or far, as tg's 32-pixel arrow (the owner, 2026-10-31: a near door's was huge, a far floor's small)
                if (hasViewer)
                    e.Node.Scale = Vector3.One * PointArrowPlacement.ScreenScale(camera.GlobalPosition.DistanceTo(position), PointArrowScale);
            }
            else if (pointedAt != null)
                position = pointedAt.LastPosition + Vector3.Up * (pointedAt.Entity.IsMob() ? PointOverMob : PointOverThing);
            bool own = e.Entity.Id == ownId;
            // the own hidden body shows the side the mirror before it sees (MirrorViewer), not its back
            bool mirrored = own && hideOwn && MirrorViewer is Vector2;
            // with no mirror it shows its front (its shadow's side); the windows and wet floors show the side each turns to (NoteSelfSides)
            (double eyeX, double eyeY) = mirrored ? (MirrorViewer.Value.X, MirrorViewer.Value.Y) : own && hideOwn && hasViewer ? FrontViewer(camera, viewerX, viewerY) : (viewerX, viewerY);
            bool shownAsPicture = UpdateVisual(e, pose, position, nowSeconds, hasViewer, eyeX, eyeY);
            if (shownAsPicture)
                pictures++;
            if (e.Billboard != null)
            {
                bool swaying = steps && e.Walk != null && shownAsPicture && e.Billboard.Visible;
                e.Billboard.Sway = swaying ? (float)e.Walk.Roll : 0f;
                if (swaying)
                    e.Billboard.Position += Vector3.Up * (float)e.Walk.Lift;
            }
            e.Node.Position = position;
            if (e.Placeholder != null)
            {
                e.Placeholder.Visible = !shownAsPicture;
                e.Placeholder.Rotation = new Vector3(0, TileSpace.DirYaw(pose.Dir), 0);
            }
            // D288 (the owner, 2026-10-29): in first person the own body is not drawn, but its picture still casts its shadow
            bool throughWalls = !own && SeeMobs && e.Entity.IsMob();
            if (e.Billboard != null)
            {
                e.Billboard.ShadowOnly = own && hideOwn; // reset when no longer own: an exosuit the pilot left stayed shadow-only
                e.Billboard.ThroughWalls = throughWalls;
            }
            bool ownShadow = own && hideOwn && shownAsPicture; // its shadow, and its reflection in a mirror (Billboard.ShadowOnly)
            // DV374: a mob the own SEE_MOBS shows is drawn whatever the third-person sight hides
            e.Node.Visible = own ? !hideOwn || ownShadow : throughWalls || InSight == null || InSight(pose.X, pose.Y, (int)MathF.Round(pose.Z));
            if (e.Label != null)
            {
                bool label = showNames && !(own && hideOwn);
                e.Label.Visible = label;
                if (label)
                    e.Label.Text = LabelText(e.Entity, own);
            }
            // the own mob's tag is hidden, not left as it was: a body one was a ghost of kept the lying body's tag in its face once revived
            if (e.Entity.IsMob())
                UpdateNameTag(e, !own && PlayerNames && !showNames);
            if (!own && e.Entity.IsMob())
            {
                if (_seenOthers.Add(e.Entity.Id))
                {
                    e.StartX = pose.X;
                    e.StartY = pose.Y;
                }
                else if (Math.Abs(pose.X - e.StartX) > 0.25f || Math.Abs(pose.Y - e.StartY) > 0.25f)
                    _movedOthers.Add(e.Entity.Id);
            }
        }
        UpdateVisuals(nowSeconds, camera);
        PublishSelfReflection(hasOwn && hideOwn ? ownEntry : null);
        PictureCount = pictures;
        if (!_placeholdersLogged && pictures > 0 && nowSeconds - _firstPictureAt > 4.0)
            LogPlaceholders();
        else if (_firstPictureAt == 0 && pictures > 0)
            _firstPictureAt = nowSeconds;
    }

    private double _firstPictureAt;
    private bool _placeholdersLogged;
    private RenderPose _ownPose;

    // One line in the log, once, a few seconds into the game: which things the layer could not draw as pictures and shows as boxes or capsules (a type the server sends with no
    // appearance, or a picture the icons cannot compose), most frequent first.
    private void LogPlaceholders()
    {
        _placeholdersLogged = true;
        var byType = new Dictionary<string, int>();
        foreach (Entry e in _list)
        {
            if (e.Placeholder != null && e.Placeholder.Visible && e.Node.Visible)
                byType[e.Entity.TypePath ?? "?"] = byType.GetValueOrDefault(e.Entity.TypePath ?? "?") + 1;
        }
        int total = byType.Values.Sum();
        string top = string.Join(", ", byType.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Take(12).Select(p => $"{p.Key} x{p.Value}"));
        GD.Print($"[anomaly] placeholders: {total} of {_list.Count} entities drawn as boxes or capsules{(total > 0 ? ": " + top : string.Empty)}");
    }

    /// <summary>How long after the player's own click its aimed point is where the next hit's burst starts.</summary>
    public const double AimedHitSeconds = 1.0;
    private readonly Dictionary<long, (Vector3 Point, double At)> _aimedMelee = new();

    /// <summary>
    /// The player clicked entity <paramref name="id"/> where the crosshair met its picture, at <paramref name="world"/> (world
    /// space): if the click hits, the burst starts there (the owner, 2026-10-10), kept relative to the picture in case it moves.
    /// </summary>
    public void RememberAimedHit(long id, Vector3 world, double now)
    {
        _aimedMelee[id] = (world, now);
        if (_entries.TryGetValue(id, out Entry e) && e.Billboard != null)
        {
            e.AimOffset = world - e.Billboard.GlobalPosition;
            e.AimAt = now;
        }
    }

    /// <summary>
    /// M9 (DV160): the point over an entity's drawn picture where its runechat lines start (tg's maptext_height over the atom), and whether the
    /// entity is drawn this frame; false for an entity the layer does not have.
    /// </summary>
    public bool TryGetChatPoint(long id, out Vector3 world, out bool shown)
    {
        world = default;
        shown = false;
        if (!_entries.TryGetValue(id, out Entry e) || e.Node == null || !e.Node.IsInsideTree())
            return false;
        float up = e.Entity.IsMob() ? TileSpace.EyeHeight + 0.45f : 0.85f;
        if (e.Billboard != null && e.Billboard.Visible && e.Billboard.Raster != null)
            up = e.Billboard.Position.Y + (float)e.Billboard.Rect.Top + 0.1f;
        world = e.Node.GlobalPosition + new Vector3(0, up, 0);
        shown = e.Node.Visible && e.Billboard?.ShadowOnly != true;
        return true;
    }

    /// <summary>
    /// D346: the height of the visible top of an entity's picture over its node (its floor), metres, as the name tag measures it (the
    /// picture's transparent rows above it left out); false without a drawn picture. The own body's is there too (its shadow-only picture).
    /// </summary>
    public bool TryGetPictureTop(long id, out float top)
    {
        top = 0f;
        if (!_entries.TryGetValue(id, out Entry e) || e.Billboard?.Raster == null)
            return false;
        if (!ReferenceEquals(e.TopRaster, e.Billboard.Raster))
        {
            e.TopRaster = e.Billboard.Raster;
            AtlasBuilder.TrimBounds(e.TopRaster.Image, out _, out e.TopTransparent, out _, out _);
        }
        top = e.Billboard.Position.Y + (float)(e.Billboard.Rect.Top - e.TopTransparent * e.Billboard.Rect.PixelSize);
        return true;
    }

    public bool TryGetPose(long id, out EntityPose pose)
    {
        if (_entries.TryGetValue(id, out Entry e) && e.HasPose)
        {
            pose = e.Pose;
            return true;
        }
        pose = default;
        return false;
    }

    /// <summary>
    /// The pick interface of the click ray (D104, M2 P8): the nearest entity along the ray from <paramref name="origin"/> in the unit direction
    /// <paramref name="direction"/> within <paramref name="maxDistance"/> metres whose picture the ray hits on an opaque pixel (a lying mob: its
    /// quad on the floor), skipping <paramref name="ignoreId"/> (the own mob) and, with <paramref name="mobsOnly"/>, everything but mobs. A wall
    /// nearer than the hit hides it. Entities drawn as placeholders are picked by their box.
    /// </summary>
    public bool TryPick(Vector3 origin, Vector3 direction, float maxDistance, long ignoreId, bool mobsOnly, out EntityPick pick)
    {
        pick = default;
        Camera3D camera = GetViewport()?.GetCamera3D();
        Basis basis = camera != null ? camera.GlobalTransform.Basis : Basis.Identity;
        float limit = maxDistance;
        _map ??= GetParent()?.GetNodeOrNull<MapRenderer>("Map");
        if (_map != null)
            limit = Math.Min(limit, _map.RayDistance(origin, direction, maxDistance));
        float best = limit;
        bool found = false;
        foreach (Entry e in _list)
        {
            if (e.Entity.Id == ignoreId || !e.Node.Visible || !e.HasPose || (mobsOnly && !e.Entity.IsMob()))
                continue;
            float distance;
            Vector3 point;
            bool hit;
            if (e.Billboard != null && e.Billboard.Visible && e.Billboard.Raster != null)
                hit = e.Billboard.TryPick(origin, direction, best, basis, out distance, out point);
            else
                hit = PickPlaceholder(e, origin, direction, best, out distance, out point);
            if (hit && distance <= best)
            {
                best = distance;
                found = true;
                pick = new EntityPick(e.Entity.Id, distance, point);
            }
        }
        return found;
    }

    private static bool PickPlaceholder(Entry e, Vector3 origin, Vector3 direction, float max, out float distance, out Vector3 point)
    {
        // A slab test against the placeholder's box around the feet.
        bool mob = e.Entity.IsMob();
        Vector3 feet = e.Node.GlobalPosition;
        Vector3 min = feet + (mob ? new Vector3(-TileSpace.CapsuleRadius, 0, -TileSpace.CapsuleRadius) : new Vector3(-0.3f, 0, -0.3f));
        Vector3 maxCorner = feet + (mob ? new Vector3(TileSpace.CapsuleRadius, TileSpace.CapsuleHeight, TileSpace.CapsuleRadius) : new Vector3(0.3f, 0.5f, 0.3f));
        float tMin = 0, tMax = max;
        for (int axis = 0; axis < 3; axis++)
        {
            float o = origin[axis], d = direction[axis];
            if (Math.Abs(d) < 1e-9f)
            {
                if (o < min[axis] || o > maxCorner[axis])
                {
                    distance = 0;
                    point = default;
                    return false;
                }
                continue;
            }
            float t1 = (min[axis] - o) / d, t2 = (maxCorner[axis] - o) / d;
            if (t1 > t2)
                (t1, t2) = (t2, t1);
            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
            if (tMin > tMax)
            {
                distance = 0;
                point = default;
                return false;
            }
        }
        distance = tMin;
        point = origin + direction * tMin;
        return true;
    }

    // DV434: a footstep heard now lands the step of the walking mob it came from: the nearest swaying mob within a tile and a half of the sound's
    // tile (the drawn mob trails the server's by the interpolation), the own mob for a sound on the listener's own tile (tg sends it unplaced).
    private void HearFootsteps(IReadOnlyList<SoundEvent> events)
    {
        foreach (SoundEvent sound in events)
        {
            if (sound.Kind != SoundKind.Play || sound.File == null || !IsStepSound(sound.File))
                continue;
            Entry best = null;
            if (!sound.Positional)
                _entries.TryGetValue(_ownId, out best);
            else
            {
                float bestDistance = 1.5f;
                foreach (Entry e in _list)
                {
                    if (e.Walk == null || !e.HasPose || MathF.Round(e.Pose.Z) != sound.Z)
                        continue;
                    float d = MathF.Sqrt((e.Pose.X - sound.X) * (e.Pose.X - sound.X) + (e.Pose.Y - sound.Y) * (e.Pose.Y - sound.Y));
                    if (d <= bestDistance)
                        (best, bestDistance) = (e, d);
                }
            }
            best?.Walk?.Land();
        }
    }

    // a mob's footstep, a hardsuit's, or an exosuit's stomp (tg's mecha stepsound: mechstep, the Ripley's powerloader_step, the Vim's servostep)
    private static bool IsStepSound(string file) => file.Contains("/footstep/", StringComparison.Ordinal) || file.Contains("/rigstep", StringComparison.Ordinal)
        || file.Contains("/mechstep", StringComparison.Ordinal) || file.Contains("/powerloader_step", StringComparison.Ordinal) || file.Contains("/servostep", StringComparison.Ordinal);

    // The pose of an entity at the render time: the pose source's, else the replica's samples (a capsule mob's continuous position
    // lerped between the two samples around the render tick, anything else by the tile glide).
    private bool TryPose(ReplicaEntity entity, double renderTick, out RenderPose pose)
    {
        if (Poses != null)
            return Poses.TryGetPose(entity, out pose);
        if (entity.HasPos && entity.TrySample(renderTick, out EntitySample from, out EntitySample to, out float t))
        {
            float x = Mathf.Lerp(from.PosX, to.PosX, t) / 256f, y = Mathf.Lerp(from.PosY, to.PosY, t) / 256f;
            pose = new RenderPose(x, y, to.Z, to.Dir, from.Yaw, to.Posture);
            return true;
        }
        if (entity.TryInterpolate(renderTick, out EntityPose glide))
        {
            pose = new RenderPose(glide.X, glide.Y, glide.Z, glide.Dir, BillboardMath.YawOfDir(glide.Dir), entity.State.Posture);
            return true;
        }
        pose = default;
        return false;
    }

    /// <summary>tg's gravitational singularity (singularity.dm): its picture with a lensing disc behind it (<see cref="SingularityLens"/>, DV50).</summary>
    public const string SingularityType = SingularityDrift.Type;

    // M8z (D237): tg's point arrow and its hologram (/obj/effect/temp_visual/point, point/holo), and the height its picture's bottom is drawn at
    private const string PointArrowType = StaticPropRule.PointArrowPrefix;
    private const float PointOverMob = 1.05f * TileSpace.TileSize, PointOverThing = 0.3f * TileSpace.TileSize;

    // The thing tg's arrow points at: drawn where the arrow's pixel offset ends (its tile plus its own pixel offset), the topmost by layer.
    private Entry PointedEntry(Entry arrow, float x, float y, float z)
    {
        Entry best = null;
        float bestLayer = float.NegativeInfinity;
        foreach (Entry c in _entries.Values)
        {
            if (c == arrow || !c.HasPose || !c.HasLast || c.Entity.TypePath?.StartsWith(PointArrowType, StringComparison.Ordinal) == true
                || MathF.Round(c.Pose.Z) != MathF.Round(z) || !_tables.TryGetAppearance(c.Entity.State.Appearance, out AppearanceEntry a))
                continue;
            float reach = c.Entity.IsMob() ? 0.5f : 0.05f; // a walking mob is between tiles
            if (Math.Abs(c.Pose.X + a.PixelX / 32f - x) > reach || Math.Abs(c.Pose.Y + a.PixelY / 32f - y) > reach || a.Layer < bestLayer)
                continue;
            best = c;
            bestLayer = a.Layer;
        }
        return best;
    }
    private const float PointArrowLift = 1.2f, PointHandHeight = 0.9f, PointArrowScale = 1f / 3f;
    // the clear rows under the lowest of the arrow's bobbing frames (screen_gen.dmi "arrow": drawn down to row 17 of 32), so its tip bobs down to the
    // point and up from it as tg's does, not trimmed frame by frame
    private const int PointArrowTrim = 14;

    /// <summary>tg's hotspot, a burning tile (LINDA_fire.dm): drawn as burning air (<see cref="FireVolume"/>), not as its flat sprite.</summary>
    public const string HotspotType = StaticPropRule.HotspotType;

    // Chooses and shows the picture of an entity; false when it has none (the placeholder stays).
    private bool UpdateVisual(Entry e, RenderPose pose, Vector3 position, double now, bool hasViewer, double viewerX, double viewerY)
    {
        if (e.Entity.TypePath == HotspotType)
            return UpdateFire(e);
        if (SmokeVolume.Applies(e.Entity.TypePath))
            return UpdateSmoke(e);
        if (FoamVolume.Applies(e.Entity.TypePath))
            return UpdateFoam(e, now);
        if (e.Entity.TypePath == BlastVolume.BlastType)
        {
            e.Blast ??= BlastVolume.Create(e.Node);
            if (_tables.TryGetAppearance(e.Entity.State.Appearance, out AppearanceEntry blastLook) && blastLook.Transform is { Length: >= 6 } scaled)
                e.Blast.SetScale(scaled[0]);
            // DV578: a blast at the top of its cell (a shot at the ceiling) carries its height on the shot's line: drawn under the ceiling
            if (e.Entity.State.Shot is ShotState high)
                e.Blast.SetLift((float)high.HeightPixels * TileSpace.TileSize / 32f);
            return true;
        }
        ShotKind shotKind = ShotLook.KindOf(e.Entity.TypePath);
        if (shotKind != ShotKind.None)
            return UpdateShot(e, shotKind, position, now);
        if (BeamSegment.Applies(e.Entity.TypePath))
            return UpdateBeam(e, now);
        if (Textures == null || e.Billboard == null && !EnsureBillboard(e))
            return false;
        TableRef own = e.Entity.State.Appearance;
        if (own.IsNone)
        {
            e.Billboard.Visible = false;
            return false;
        }
        // M4v slice 2 (D142): the entity's last flick (NetField.Flick) plays its states once from the tick it started in, then its own picture is back
        // The own picture's icon states play from when its icon or icon state last changed (BYOND restarts an animation with a new icon
        // state; a turn, which changes the appearance's dir, or a new overlay does not)
        if (_tables.TryGetAppearance(own, out AppearanceEntry ownEntry) && (ownEntry.Icon, ownEntry.IconState) != e.OwnSeen)
        {
            e.OwnSeen = (ownEntry.Icon, ownEntry.IconState);
            e.OwnSince = now;
        }
        TableRef appearance = own;
        double seconds = now - e.OwnSince;
        if (FlickNow(e, out TableRef flicked, out double age))
        {
            appearance = flicked;
            seconds = age;
        }
        IAppearanceSource tables = _tables;
        if (_insets.Count > 0 && _insets.TryGetValue(e.Entity.Id, out List<Entry> insets) && insets.Count > 0)
        {
            foreach (Entry inset in insets)
            {
                if (Inset(inset, appearance, out TableRef withInset))
                {
                    appearance = withInset;
                    tables = _insetLooks;
                }
            }
        }
        bool lying = PostureBits.IsLying(pose.Posture);
        // The type's own look (WorldLooks, the dev menu's world things): standing or flat, its size, its move. A thing of the floor's layers lies flat
        // as the batch draws it (a gib that slid off its tile was handed here and stood up as a billboard).
        WorldLook look = e.IsVisual || e.OnSlot ? default : WorldLooks.Active.Find(e.Entity.TypePath);
        float flatLift = FloorLayers.TunedLift;
        bool liesFlat = !e.IsVisual && !e.OnSlot && (look.Mode == WorldLookMode.Floor || look.Mode == WorldLookMode.Auto && !e.Entity.IsMob()
            && _tables.TryGetAppearance(own, out AppearanceEntry layers) && FloorLayers.TryLift(layers.Plane, layers.Layer, e.Entity.TypePath, out flatLift));
        byte dir;
        // A lying mob is drawn like a standing one from the side it is seen from (its facing is the way it crawls, or the side it lies on).
        // The replicated yaw is the player's look: a body on the floor keeps the way it fell (LyingFacing). A conscious one lies face down (its back
        // is seen), a limp one front or back by the viewer's side (BillboardMath.ChooseLyingDir).
        byte yaw = e.Entity.IsMob() ? e.Facing.Update(lying, pose.Yaw) : pose.Yaw;
        if (liesFlat || (!e.Entity.HasPos && !e.Entity.IsMob()) || !hasViewer) // a mob with an AI has no capsule, only its dir, and is seen from each side too (its yaw is the dir's)
            dir = pose.Dir == 0 ? ByondDir.South : pose.Dir;
        else if (lying)
            dir = BillboardMath.ChooseLyingDir(yaw, pose.X, pose.Y, viewerX, viewerY, Textures.Directions(tables, appearance), PostureBits.IsLimp(pose.Posture));
        else if (PropKinds.IsItem(e.Entity.TypePath))
            dir = BillboardMath.ChooseSideDir(yaw, pose.X, pose.Y, viewerX, viewerY, Textures.Directions(tables, appearance));
        else
            dir = BillboardMath.ChooseDir(yaw, pose.X, pose.Y, viewerX, viewerY, Textures.Directions(tables, appearance));
        // Version 26 (D156): its filters' animate() chains (tg's high gravity blurs a mob, life.dm:129) recompose the picture as they play.
        Dictionary<AppearanceEntry, IReadOnlyList<FilterEntry>> filterLooks = null;
        if (e.Entity.Animation != null)
            FilterPlayback.TryEvaluate(tables, appearance, e.Entity.Animation, _renderTick, Anomaly.Core.Motion.MotionMath.TicksPerSecond, out filterLooks);
        if (!Textures.TryGet(tables, appearance, dir, seconds, out ComposedRaster raster, out GodotPicture picture, rootOpaque: e.IsVisual, filterLooks: filterLooks))
        {
            e.Billboard.Visible = false;
            // BYOND draws nothing for an icon state its icon lacks (with no default state): tg's trail particles blank themselves so after their
            // flick (effects_trail.dm:72). Such an entity has no placeholder either; only a missing icon file keeps it.
            return Textures.Compositor.IsBlank(tables, appearance, dir, rootOpaque: e.IsVisual);
        }
        // DV46: a particle effect fades out near the own eye (NearEyeFade).
        e.EyeFade = hasViewer && NearEyeFade.Applies(e.Entity.TypePath) ? NearEyeFade.Alpha(pose.X - (float)viewerX, pose.Y - (float)viewerY) : 1f;
        if (e.EyeFade <= 0f)
        {
            e.Billboard.Visible = false;
            return true;
        }
        // D121: a thing a hand can hold is drawn at the item scale, its drawn pixels resting on the floor (the same reading as the static items of the map renderer).
        bool pointArrow = e.Entity.TypePath != null && e.Entity.TypePath.StartsWith(PointArrowType, StringComparison.Ordinal);
        bool item = (!e.Entity.IsMob() && PropKinds.IsItem(e.Entity.TypePath)) || pointArrow;
        float scale = item ? WorldScale.ItemScale : e.Entity.IsMob() ? e.Entity.MobDrawScale() : 1f;
        if (!item && !lying && !e.Entity.IsMob() && !e.IsVisual)
        {
            scale = TallFit(e, raster, own);
            item = scale != 1f; // drawn about the bottom centre of its drawn pixels, as an item
        }
        if (e.IsVisual)
        {
            // M4v slice 3: a visual takes its host's scale (VisualAnchor): a pickup image on the floor is drawn as the item lay there.
            scale = e.VisualScale;
            item = scale != 1f;
        }
        scale *= look.Size;
        bool miningPing = e.Entity.TypePath == StaticPropRule.MiningPingType;
        e.Billboard.OnTop = pointArrow || miningPing; // DV232: tg's POINT_PLANE; DV250: a mining scanner's ping shows through the rock it stands in
        if (miningPing)
            e.Billboard.Fullbright = 1f; // (the inside of a rock has no light)
        e.Billboard.LiesFlat = liesFlat;
        e.Billboard.SetPicture(raster, picture, lying, scale, e.IsVisual ? e.HostTrim : pointArrow ? PointArrowTrim : null);
        // The mirrors' reflection (MirrorPass.cs): a mob or an item often faces a different side there than the main camera sees (the owner,
        // 2026-11-13), so it gets its own picture, chosen with the mirror's own eye instead of the real one. The own hidden body's whole
        // computation above already used it (nothing else sees that body), so its main picture is reused as is; everything else that reflects
        // needs a second lookup. A point arrow, a mining ping and a visual keep the one side (a small, transient effect of little weight).
        bool isOwnMob = e.Entity.Id == _ownId;
        if (isOwnMob && _hideOwn)
            NoteSelfSides(tables, appearance, seconds, filterLooks, picture, lying, yaw);
        bool reflectable = (e.Entity.IsMob() || PropKinds.IsItem(e.Entity.TypePath)) && !e.IsVisual && !pointArrow && !miningPing;
        if (MirrorViewer is Vector2 mirrorEye && reflectable
            && MathF.Abs(mirrorEye.X - pose.X) <= MirrorReflectRangeTiles && MathF.Abs(mirrorEye.Y - pose.Y) <= MirrorReflectRangeTiles)
        {
            if (isOwnMob && _hideOwn)
                e.Billboard.SetReflection(picture);
            else
            {
                byte mirrorDir = lying ? BillboardMath.ChooseLyingDir(yaw, pose.X, pose.Y, mirrorEye.X, mirrorEye.Y, Textures.Directions(tables, appearance), PostureBits.IsLimp(pose.Posture))
                    : PropKinds.IsItem(e.Entity.TypePath) ? BillboardMath.ChooseSideDir(yaw, pose.X, pose.Y, mirrorEye.X, mirrorEye.Y, Textures.Directions(tables, appearance))
                    : BillboardMath.ChooseDir(yaw, pose.X, pose.Y, mirrorEye.X, mirrorEye.Y, Textures.Directions(tables, appearance));
                e.Billboard.SetReflection(Textures.TryGet(tables, appearance, mirrorDir, seconds, out _, out GodotPicture mirrorPicture, rootOpaque: false, filterLooks: filterLooks)
                    ? mirrorPicture : null);
            }
        }
        else
            e.Billboard.SetReflection(null);
        // A mob over the things on its tile (tg's MOB_LAYER), seen from every side; a visual over its host, on the host's anchor
        bool onMob = e.IsVisual ? _entries.TryGetValue(e.Entity.VisHost, out Entry visHost) && visHost.Entity.IsMob() : e.Entity.IsMob();
        // D329: an item lies at its own point on the tile (put down anywhere, landed from a throw): its depth is its own, not its tile centre's
        // (pushed back along the eye's ray to the centre's depth, an item nearer than the centre sank into the floor or the table)
        bool looseItem = !e.IsVisual && !e.Entity.IsMob() && PropKinds.IsItem(e.Entity.TypePath);
        e.Billboard.SetDepthRank(e.IsVisual ? DepthRank.Of(float.PositiveInfinity, e.Entity.Id) : DepthRank.Of(_tables.TryGetAppearance(own, out AppearanceEntry layered) ? layered.Layer : 0f, e.Entity.Id), !onMob && !looseItem);
        // the owner (2026-11-20): a lying body faces the eye as an item does, tilted back about its lowest pixels; a standing one turns about the vertical
        e.Billboard.FacesEye = looseItem || lying && e.Entity.IsMob() && !e.IsVisual;
        e.DrawnDir = dir;
        e.Billboard.Visible = true;
        // The root appearance's own pixel_x and pixel_y are not in the picture (M3 R2): they are the ground offset of the node's picture, east = +x,
        // north = -z, 32 pixels a tile (the same reading as the planner's for a static prop), so a held or dropped thing with a small offset sits
        // where tg draws it and is moved once.
        Vector3 ground = Vector3.Zero;
        bool hasRoot = _tables.TryGetAppearance(own, out AppearanceEntry root);
        // D329: an item's offset is where tg put it on its tile (a table's placing at the clicked pixel, the drop key's point), not scaled with
        // its picture; a thing on a flight's arc or at its rest point is drawn at that point (DV416), its offset not added
        float offsetScale = !e.IsVisual && !e.Entity.IsMob() && PropKinds.IsItem(e.Entity.TypePath) ? 1f : scale;
        if (hasRoot && (root.PixelX != 0 || root.PixelY != 0) && e.Entity.State.Flight.IsNone)
            ground = new Vector3(root.PixelX * (TileSpace.TileSize / 32f) * offsetScale, 0f, -root.PixelY * (TileSpace.TileSize / 32f) * offsetScale);
        if (e.VisualFlat)
        {
            // A visual on a wall decal lies in the decal's plane, centred on its face point; its pixel offsets move it along the wall and up.
            SpriteRect flat = e.Billboard.Rect;
            float m = TileSpace.TileSize / 32f;
            ground = new Vector3(-(float)(flat.Left + flat.Right) / 2f + (hasRoot ? root.PixelX * m : 0f), -(float)(flat.Bottom + flat.Top) / 2f + (hasRoot ? root.PixelY * m : 0f), 0f);
        }
        float across = 0f;
        if (!e.VisualFlat && !liesFlat && hasRoot && !item && !lying && e.Billboard.Rect.IsCentredBigIcon(root.PixelX, root.PixelY))
        {
            // DV49: a big icon centred by its own offsets (tg's explosion, the singularity) stands centred on its tile, its pixel_y read as height
            // and its pixel_x moving the picture across the view (the picture turns to the camera; a ground offset east would put it off its
            // tile seen from any side but the south, and four tiles off seen from the north: the owner's play test, 2026-10-16)
            float m = TileSpace.TileSize / 32f;
            ground = new Vector3(0f, root.PixelY * m, 0f);
            across = root.PixelX * m;
        }
        else if (!e.VisualFlat && !liesFlat && hasRoot && !lying && !e.IsVisual && !pointArrow && (e.Entity.IsMob() || !PropKinds.IsItem(e.Entity.TypePath))
                 && e.Billboard.Rect.IsCentredWide(root.PixelX))
        {
            // DV49: a wide picture standing on the floor that tg centres by its pixel_x alone (the colossus, a tree): that pixel_x moves it
            // across the view, at its drawn scale; its pixel_y stays a ground offset
            ground.X = 0f;
            across = root.PixelX * (TileSpace.TileSize / 32f) * scale;
        }
        e.Billboard.ShiftAcross(across);
        if (liesFlat)
            ground.Y = flatLift;
        ground += new Vector3(look.X, look.Y, -look.Z);
        // protocol version 34: an impact a shot left stands with its picture's middle at the shot's height there
        if (e.Entity.Shot is ShotState impact && !e.OnSlot)
            ground.Y = (float)(impact.HeightPixels * TileSpace.TileSize / 32.0) - (float)(e.Billboard.Rect.Bottom + e.Billboard.Rect.Top) / 2f;
        if (pointArrow)
        {
            // DEVIATION(DV159, D237, DV232): tg's point arrow (POINT_PLANE, over everything) is drawn at a third of the item scale with its tip on the
            // aimed point (without one, at chest height over the pointed thing's tile), glowing in the dark, after its 0.17 s slide from the pointer
            ground = e.PointSpot != null ? Vector3.Zero : new Vector3(0f, PointArrowLift / PointArrowScale, 0f); // (the node's scale shrinks the lift too)
            e.Billboard.Fullbright = 1f;
            if (e.PointSpot == null)
                e.Node.Scale = Vector3.One * PointArrowScale; // the owner (2026-10-31): a third of the item scale
        }
        e.Billboard.Position = e.OnSlot ? Vector3.Zero : ground;
        if (e.MeleeVisual)
            e.Billboard.Position = new Vector3(0, -(float)(e.Billboard.Rect.Bottom + e.Billboard.Rect.Top) / 2f, 0);
        if (e.Entity.IsMob() && now - e.HitStart < 0.3)
            e.Billboard.Position += Vector3.Right * (float)(Math.Sin((now - e.HitStart) * 65) * 0.1 * Math.Exp(-(now - e.HitStart) * 12));
        if (e.Entity.TypePath != null && e.Entity.TypePath.StartsWith(SingularityType, StringComparison.Ordinal))
        {
            // DV50: the singularity's lensing disc, behind its picture's middle
            SpriteRect drawn = e.Billboard.Rect;
            e.Lens ??= SingularityLens.Create(e.Node);
            // (centred across the view: the picture's own pixel_x shift is ShiftAcross's, so the disc sits on the node's vertical line)
            Vector3 middle = new(ground.X, ground.Y + (float)(drawn.Bottom + drawn.Top) / 2f, ground.Z);
            e.Lens.Set(middle, (float)drawn.Width);
            // DV67: drawn above the lighting (tg's ABOVE_LIGHTING_PLANE), and its light in its own colours, seen from afar
            e.Billboard.Fullbright = SingularityGlow.PictureGain;
            e.Glow ??= SingularityGlow.Create(e.Node, e.Entity.Id);
            if (e.Glow.Set(middle, raster.Image))
                e.Lens.SetHalo(e.Glow.LightColor);
        }
        ApplyThrow(e, picture, lying, now);
        ApplyLunge(e, picture, lying, scale, now);
        if (!pointArrow)
            ApplyAnimation(e, picture, lying, item, scale, hasRoot ? root : null);
        ApplyHit(e, now);
        return true;
    }

    // DV122: a standing picture taller than the walls (tg's 32 x 64 cryo cell) is drawn at the scale that ends its drawn pixels under the ceiling;
    // a big icon tg centres on its tile (DV49) keeps its size. Found once per picture.
    private float TallFit(Entry e, ComposedRaster raster, TableRef own)
    {
        if (ReferenceEquals(raster, e.FitRaster))
            return e.FitScale;
        e.FitRaster = raster;
        e.FitScale = 1f;
        AtlasBuilder.TrimBounds(raster.Image, out _, out int top, out _, out int drawnRows);
        if (drawnRows == 0 || raster.Image.Height <= 32)
            return e.FitScale;
        if (_tables.TryGetAppearance(own, out AppearanceEntry root) && AtlasBuilder.PictureRect(raster, false, 1, null, out _).IsCentredBigIcon(root.PixelX, root.PixelY))
            return e.FitScale;
        float drawnTop = (raster.Image.Height - top - raster.OriginY) * (TileSpace.TileSize / 32f);
        e.FitScale = WorldScale.FitUnderCeiling(drawnTop, WorldScale.WallHeight);
        return e.FitScale;
    }

    // The flick that plays on the entity at the render tick (FlickPlayback) and the seconds into it. Its length (the flicked states played
    // once) is found once per flick; while an icon is missing or loading it is looked for again.
    private bool FlickNow(Entry e, out TableRef flicked, out double seconds)
    {
        EntityState s = e.Entity.State;
        flicked = s.FlickAppearance;
        seconds = 0;
        if (flicked.IsNone)
            return false;
        if (flicked != e.FlickSeen || s.FlickTick != e.FlickSeenTick || e.FlickLength <= 0)
        {
            e.FlickSeen = flicked;
            e.FlickSeenTick = s.FlickTick;
            e.FlickLength = Textures.Compositor.TryCompose(_tables, flicked, ByondDir.South, 0, out _)
                ? FlickPlayback.LengthOf(Textures.Compositor.AnimatedStates(_tables, flicked, ByondDir.South))
                : 0;
        }
        return FlickPlayback.TryAge(s.FlickTick, _renderTick, Anomaly.Core.Motion.MotionMath.TicksPerSecond, e.FlickLength, out seconds);
    }

    // M4v slice 1 (protocol version 9, D143): tg's animate() chains (the entity's Animation) over the composed picture, as
    // AnimationPlayback reads them: pixel_x and pixel_y move the picture on the ground (as the placement reads the appearance's own),
    // pixel_z up (up the screen is up for a standing picture), pixel_w to the side in its plane; color and alpha scale its colour; the
    // transform turns and scales it in its plane after its own, about the icon's centre (an item's or a lying mob's: its picture's middle).
    // The lunge and the throw own the material while they play.
    private void ApplyAnimation(Entry e, GodotPicture picture, bool lying, bool item, float scale, AppearanceEntry root)
    {
        MeshInstance3D quad = e.Billboard.GetChildCount() > 0 ? e.Billboard.GetChild(0) as MeshInstance3D : null;
        EntityAnimation animation = e.Entity.Animation;
        AnimatedLook look = AnimatedLook.None;
        // A short-lived visual's picture is composed opaque (its chain ends at alpha 0: tg's pickup and attack images), so its alpha is
        // the look's own over 255, and it keeps the animation's material (its alpha; a wall face's flat plane) when no chain plays.
        // (M4v slice 4: the chain is played against the appearance's own alpha and its shown alpha taken over the opaque picture; a base of 255
        // added the chain's way down to 255, which clamped to no change, so a visual never faded.)
        AnimatedBase start = root == null ? default : AnimatedBase.Of(root);
        bool playing = animation != null && root != null
            && AnimationPlayback.TryEvaluate(animation, _renderTick, Anomaly.Core.Motion.MotionMath.TicksPerSecond, start, out look, e.IsVisual ? 255 : double.NaN)
            && (e.IsVisual || !look.IsNone); // a visual's picture is opaque: a look of no change (a chain not started yet) is not its end alpha
        if (!playing && e.IsVisual && root != null && (root.Alpha < 255 || e.VisualFlat))
        {
            look = AnimatedLook.None with { Alpha = root.Alpha / 255.0 };
            playing = true;
        }
        if (e.EyeFade < 1f)
        {
            // DV46: the fade near the own eye scales whatever alpha the picture is drawn with.
            look = (playing ? look : AnimatedLook.None) with { Alpha = (playing ? look.Alpha : 1.0) * e.EyeFade };
            playing = true;
        }
        if (!playing)
        {
            if (e.Animating && !e.Spinning && !e.Lunging && quad != null)
                quad.MaterialOverride = picture.Standing; // (Billboard.PictureMaterial is the quad's override now: the animation's own)
            e.Animating = false;
            return;
        }
        float metresPerPixel = TileSpace.TileSize / 32f * scale;
        if (!e.MeleeVisual && e.Entity.TypePath?.StartsWith(PointArrowType, StringComparison.Ordinal) != true)
            e.Billboard.Position += e.VisualFlat
                ? new Vector3((float)look.PixelX * metresPerPixel, (float)(look.PixelY + look.PixelZ) * metresPerPixel, 0f)
                : new Vector3((float)look.PixelX * metresPerPixel, (float)look.PixelZ * metresPerPixel, -(float)look.PixelY * metresPerPixel);
        if (e.Spinning || e.Lunging || quad == null)
            return;
        _animateShader ??= GD.Load<Shader>("res://shaders/picture_billboard_animate.gdshader");
        e.AnimateMaterial ??= new ShaderMaterial { Shader = _animateShader };
        // A see-through picture keeps its blended material while it animates (the water puff's fade, effects_water.dm:59).
        Shader shader = picture.Translucent ? GodotPicture.TranslucentShader : _animateShader;
        if (e.AnimateMaterial.Shader != shader)
            e.AnimateMaterial.Shader = shader;
        ShaderMaterial source = picture.Standing;
        e.AnimateMaterial.SetShaderParameter("picture", source.GetShaderParameter("picture"));
        e.AnimateMaterial.SetShaderParameter("scissor", source.GetShaderParameter("scissor"));
        Affine t = look.Transform;
        e.AnimateMaterial.SetShaderParameter("affine", new Vector4((float)t.A, (float)t.B, (float)t.D, (float)t.E));
        e.AnimateMaterial.SetShaderParameter("offset", e.MeleeVisual ? Vector2.Zero : new Vector2((float)(t.C + look.PixelW) * metresPerPixel, (float)t.F * metresPerPixel));
        SpriteRect rect = e.Billboard.Rect;
        Vector2 pivot = lying || item || e.VisualFlat
            ? new Vector2((float)(rect.Left + rect.Right) / 2f, (float)(rect.Bottom + rect.Top) / 2f)
            : new Vector2(0f, TileSpace.TileSize / 2f);
        e.AnimateMaterial.SetShaderParameter("pivot", pivot);
        e.AnimateMaterial.SetShaderParameter("fixed_plane", e.VisualFlat);
        e.AnimateMaterial.SetShaderParameter("modulate", new Vector4((float)look.Red, (float)look.Green, (float)look.Blue, (float)Math.Min(look.Alpha, 1.0)));
        quad.MaterialOverride = e.AnimateMaterial;
        e.Animating = true;
    }

    /// <summary>M8x (D210): tg's visual helpers (the cryo cell's occupant), drawn into their holder's picture (<see cref="VisInsets"/>).</summary>
    public const string VisualHelperType = "/atom/movable/visual";

    /// <summary>M8y: a unique vis overlay (tg's <c>SSvis_overlays</c>: the stasis bed's mattress, which fades in and out), drawn into its holder's picture as a helper is.</summary>
    public const string VisOverlayType = "/obj/effect/overlay/vis";

    /// <summary>M10x: tg's eyelid effects (the blink: alpha 255 for a moment, then 0 again), drawn into the human's picture as a helper is.</summary>
    public const string EyelidType = "/obj/effect/abstract/eyelid_effect";

    /// <summary>E16: the big manipulator's arm (the claw, turned toward its task's tile), drawn into the manipulator's picture as a helper is.</summary>
    public const string ManipulatorArmType = "/obj/effect/big_manipulator_arm";

    private static bool IsVisualHelper(string type) => IsPath(type, VisualHelperType) || IsPath(type, VisOverlayType) || IsPath(type, EyelidType) || IsPath(type, ManipulatorArmType);

    private static bool IsPath(string type, string path)
        => type != null && type.StartsWith(path, StringComparison.Ordinal) && (type.Length == path.Length || type[path.Length] == '/');

    // M8x (D210): each frame, the visual helpers by their hosts; a host the map renderer batches is handed to the layer, which composes it
    // with the helper as the helper's chain moves it (tg's cryo occupant bobs 2 pixels).
    private void FindInsets()
    {
        if (_insets.Count > 0)
        {
            // the lists are kept for the next frame (a human's two eyelids every frame); a host left with none is dropped
            _emptyHosts.Clear();
            foreach ((long host, List<Entry> list) in _insets)
            {
                if (list.Count == 0)
                    _emptyHosts.Add(host);
                list.Clear();
            }
            foreach (long host in _emptyHosts)
                _insets.Remove(host);
        }
        if (_visuals.Count == 0)
            return;
        foreach (Entry v in _visuals.Values)
        {
            long host = v.Entity.VisHost;
            if (!IsVisualHelper(v.Entity.TypePath) || host == 0)
                continue;
            if (!_insets.TryGetValue(host, out List<Entry> insets))
                _insets[host] = insets = new List<Entry>();
            insets.Add(v);
            if (!_entries.ContainsKey(host))
                (_host as MapRenderer)?.HandOver(host);
        }
    }

    // The holder's picture with the helper in it: the helper's own pixel offsets plus its chain's now, in whole pixels (as BYOND draws them
    // at the map's scale), and its chain's alpha now (M8y: the stasis bed's mattress fades over 5 s); false while its appearance is unknown.
    private bool Inset(Entry inset, TableRef host, out TableRef composed)
    {
        composed = host;
        TableRef look = inset.Entity.State.Appearance;
        if (look.IsNone || !_tables.TryGetAppearance(look, out AppearanceEntry root))
            return false;
        double dx = 0, dy = 0;
        int alpha = root.Alpha;
        if (inset.Entity.Animation != null
            && AnimationPlayback.TryEvaluate(inset.Entity.Animation, _renderTick, Anomaly.Core.Motion.MotionMath.TicksPerSecond, AnimatedBase.Of(root), out AnimatedLook moved, 255))
        {
            dx = moved.PixelX;
            dy = moved.PixelY;
            alpha = (int)Math.Round(Math.Clamp(moved.Alpha, 0, 1) * 255);
        }
        if (alpha == 0)
            return false; // an invisible helper changes nothing (an eyelid between blinks)
        composed = _insetLooks.With(host, look, root.PixelX + (int)Math.Round(dx), root.PixelY + (int)Math.Round(dy), alpha);
        return true;
    }

    // M4v slice 3 (protocol version 10): tg's short-lived visuals (flick_overlay_view: a flick visual in another atom's vis_contents) are drawn
    // over their host as VisualAnchor places them: on a moving entity's node, on a static prop of the map renderer (a wall decal's face, a
    // billboard's resting point), or on the turf's floor; each with its own appearance, flick and animate() chain, until the server deletes it.
    /// <summary>DV356: the own client has T-ray copies now, so the pick looks into the walls (<see cref="WorldPicker.SeeIntoWalls"/>).</summary>
    public bool TRayShown { get; private set; }

    /// <summary>DV96: tg's smart SMG lock reticle, drawn on the mob it locks onto (its host), this far toward the viewer (metres).</summary>
    private const string SmartLockType = "/obj/effect/temp_visual/smartgun_target";
    private const float SmartLockLift = 0.4f;

    private void UpdateVisuals(double now, Camera3D camera)
    {
        TRayShown = false;
        if (_visuals.Count == 0)
            return;
        Vector3 eye = camera != null ? camera.GlobalPosition : Vector3.Zero;
        var viewer = new System.Numerics.Vector3(eye.X, eye.Y, eye.Z);
        // Version 35 (DV114): an override image (tg's delusions, an unseen effect) is drawn in place of the entity it hangs on: the entity's own
        // picture is not drawn while the image lives, and the image is drawn as a billboard where the entity stands.
        foreach (Entry o in _visuals.Values)
        {
            if (o.Entity.IsOverride && o.Entity.VisHost != 0 && _entries.TryGetValue(o.Entity.VisHost, out Entry replaced))
                replaced.Node.Visible = false;
        }
        MapRenderer map = _host as MapRenderer ?? (_map ??= GetParent()?.GetNodeOrNull<MapRenderer>("Map"));
        foreach (Entry e in _visuals.Values)
        {
            ReplicaEntity v = e.Entity;
            EntityState state = v.State;
            long host = v.VisHost;
            if (IsVisualHelper(v.TypePath))
            {
                e.Node.Visible = false; // drawn into its host's picture (FindInsets)
                continue;
            }
            // Version 30 (DV75): a T-ray image (the replica numbers it below zero) of something that lies on the floor lies on its tile.
            if (v.Id < 0 && host == 0 && UpdateFlatImage(e, now))
                continue;
            // M8 slice F (D185): a data HUD image on a mob (the replica numbers it below zero and names the mob as its host) is drawn over that
            // mob as a billboard, at the mob's scale and the image's own pixel offsets, where the mob is seen; on the own body only where it is
            // drawn (third person): tg's delusion and fake-shock images override the own picture, which had hidden the body with nothing in its place
            if (v.Id < 0 && host != 0)
            {
                if (host == _ownId && _hideOwn || !_entries.TryGetValue(host, out Entry hostEntry) || !hostEntry.HasPose)
                {
                    e.Node.Visible = false;
                    continue;
                }
                EntityState at = hostEntry.Entity.State;
                Vector3 feet = hostEntry.Node.Position;
                float scale = hostEntry.Entity.IsMob() ? hostEntry.Entity.MobDrawScale() : PropKinds.IsItem(hostEntry.Entity.TypePath) ? WorldScale.ItemScale : 1f;
                VisualPlacement spot = VisualAnchor.OnBillboard(new System.Numerics.Vector3(feet.X, feet.Y, feet.Z), scale, viewer);
                e.VisualScale = spot.Scale;
                e.VisualFlat = false;
                e.HostTrim = hostEntry.Billboard?.BottomTrim ?? 0; // tg draws it in the mob's cell: its rows over the head stay there
                e.Node.Position = new Vector3(spot.Origin.X, spot.Origin.Y, spot.Origin.Z);
                e.Node.Rotation = Vector3.Zero;
                bool pic = UpdateVisual(e, new RenderPose(at.X, at.Y, at.Z, at.Dir, 0, 0), e.Node.Position, now, false, 0, 0);
                bool drawn = pic && (InSight == null || InSight(at.X, at.Y, at.Z));
                if (drawn && e.Billboard?.Raster != null && _tables.TryGetAppearance(v.State.Appearance, out AppearanceEntry look))
                {
                    // DV231: the job icon goes over the head's top right
                    (int right, int up) = DataHudPlacement.Shift(e.Billboard.Raster);
                    float m = TileSpace.TileSize / 32f * spot.Scale;
                    e.Billboard.ShiftAcross((look.PixelW + right) * m);
                    e.Billboard.Position += new Vector3(0f, (look.PixelZ + up) * m, 0f);
                }
                e.Node.Visible = drawn;
                continue;
            }
            // A thing a model draws with this overlay as a variant of it (a button's success light): the model shows it, not a picture of its own.
            if (v.TypePath == "/atom/movable/flick_visual" && state.Shot is ShotState melee)
            {
                e.MeleeVisual = true;
                if (e.MeleeStart < 0) e.MeleeStart = now;
                Vector3 start = new(TileSpace.WorldX((float)(melee.OriginX / 32.0)), TileSpace.WorldY(state.Z) + (float)melee.HeightPixels * TileSpace.TileSize / 32f,
                    TileSpace.WorldZ((float)(melee.OriginY / 32.0)));
                Vector3 end = new(TileSpace.WorldX(state.X), TileSpace.WorldY(state.Z) + (float)melee.HeightAt(state.X * 32, state.Y * 32) * TileSpace.TileSize / 32f, TileSpace.WorldZ(state.Y));
                if (host != 0 && _aimedMelee.TryGetValue(host, out var aimed) && now - aimed.At <= AimedHitSeconds)
                    end = aimed.Point;
                else if (host != 0 && map != null && map.TryVisualAnchor(host, eye, out VisualPlacement anchor))
                    end = new Vector3(anchor.Origin.X, end.Y, anchor.Origin.Z);
                float progress = Mathf.Clamp((float)((now - e.MeleeStart) / 0.18), 0f, 1f);
                e.Node.Position = start.Lerp(end, progress);
                e.Node.Rotation = Vector3.Zero;
                e.VisualFlat = false;
                e.VisualScale = WorldScale.ItemScale;
                e.Node.Visible = UpdateVisual(e, new RenderPose(state.X, state.Y, state.Z, state.Dir, 0, 0), e.Node.Position, now, false, 0, 0)
                    && (InSight == null || InSight(state.X, state.Y, state.Z));
                continue;
            }
            if (host != 0 && map != null && !_entries.ContainsKey(host) && map.TryAbsorbVisual(host, v.Id, state.Appearance))
            {
                e.AbsorbedBy = host;
                e.Node.Visible = false;
                continue;
            }
            VisualPlacement place;
            if (host != 0 && _entries.TryGetValue(host, out Entry h) && h.HasPose)
            {
                Vector3 feet = h.Node.Position;
                float hostScale = h.Entity.IsMob() ? h.Entity.MobDrawScale() : PropKinds.IsItem(h.Entity.TypePath) ? WorldScale.ItemScale : 1f;
                place = VisualAnchor.OnBillboard(new System.Numerics.Vector3(feet.X, feet.Y, feet.Z), hostScale, viewer);
                // DV96: the smart SMG's lock reticle stands well in front of the mob its bullets home on, so the body never hides it
                if (v.TypePath == SmartLockType)
                    place = place with { Origin = place.Origin + VisualAnchor.TowardViewer(place.Origin, viewer) * SmartLockLift };
            }
            else if (host == 0 || map == null || !map.TryVisualAnchor(host, eye, out place))
            {
                place = VisualAnchor.OnTurf(state.X, state.Y, state.Z, WorldScale.ItemScale, viewer);
            }
            e.VisualScale = place.Scale;
            e.VisualFlat = place.Surface == VisualSurface.WallFace;
            e.Node.Position = new Vector3(place.Origin.X, place.Origin.Y, place.Origin.Z);
            e.Node.Rotation = e.VisualFlat ? new Vector3(0f, place.Yaw, 0f) : Vector3.Zero;
            var pose = new RenderPose(state.X, state.Y, state.Z, state.Dir, 0, 0);
            e.Node.Visible = UpdateVisual(e, pose, e.Node.Position, now, false, 0, 0) && (InSight == null || InSight(state.X, state.Y, state.Z));
        }
    }

    // Version 30 (DV75): tg's T-ray pulse (t_scanner.dm:50) shows a copy of each hidden cable, pipe and disposal pipe at alpha 128 at its tile (and the engineering goggles'
    // thermal overlay and pipe connection arrows, D170, are images of the floor's layers too). Their
    // layers say they lie on the floor (FloorLayers), so the copy is a flat picture on the tile, a hair above the floor tile that hides the thing,
    // at the copy's alpha; anything else (a hidden box) stays a visual standing there. False: draw it as a visual.
    private bool UpdateFlatImage(Entry e, double now)
    {
        EntityState s = e.Entity.State;
        if (Textures == null || s.Appearance.IsNone || !_tables.TryGetAppearance(s.Appearance, out AppearanceEntry root) || !FloorLayers.TryLift(root.Plane, root.Layer, null, out float lift))
            return false;
        byte dir = s.Dir == 0 ? ByondDir.South : s.Dir;
        if (!Textures.TryGet(_tables, s.Appearance, dir, now, out ComposedRaster raster, out GodotPicture picture, rootOpaque: true))
        {
            e.Node.Visible = false;
            return true;
        }
        bool tRay = root.Alpha == TRayPulse.ImageAlpha;
        TRayShown |= tRay;
        if (e.Flat == null)
        {
            e.Flat = new MeshInstance3D { Name = "Flat", CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            e.Node.AddChild(e.Flat);
        }
        // D289: the walls' occluders would cull a copy behind them before its see-through material is drawn
        e.Flat.IgnoreOcclusionCulling = tRay;
        if (!ReferenceEquals(raster, e.FlatRaster))
        {
            e.FlatRaster = raster;
            float metres = TileSpace.TileSize / 32f;
            e.Flat.Mesh = new QuadMesh { Size = new Vector2(raster.Image.Width * metres, raster.Image.Height * metres), Orientation = PlaneMesh.OrientationEnum.Y };
            // D289: a T-ray copy shows through the wall over its tile (the cables and pipes under the walls), but not over the things in the hands
            e.Flat.MaterialOverride = tRay
                ? Render.MesonLayer.SeeThrough(new Color(1f, 1f, 1f, root.Alpha / 255f), picture.Texture)
                : new StandardMaterial3D
                {
                    AlbedoTexture = picture.Texture,
                    AlbedoColor = new Color(1f, 1f, 1f, root.Alpha / 255f),
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                };
        }
        if (tRay)
            ((ShaderMaterial)e.Flat.MaterialOverride).SetShaderParameter("tint", new Color(1f, 1f, 1f, root.Alpha / 255f * TRayPulse.Opacity(now)));
        // The copy's own pixel offsets move it on its tile (east = +x, north = -z): the engineering goggles' pipe connection arrows sit on their pipe's layer (PIPING_LAYER_DOUBLE_SHIFT).
        float offset = TileSpace.TileSize / 32f;
        e.Node.Position = new Vector3(TileSpace.WorldX(s.X) + root.PixelX * offset, TileSpace.WorldY(s.Z) + lift + 0.03f, TileSpace.WorldZ(s.Y) - root.PixelY * offset);
        e.Node.Rotation = Vector3.Zero;
        // D289: a T-ray copy is seen through the walls, so the third-person sight does not hide it (the tiles under and behind the walls are out of sight)
        e.Node.Visible = tRay || InSight == null || InSight(s.X, s.Y, s.Z);
        return true;
    }

    private void AddVisual(ReplicaEntity entity)
    {
        if (_visuals.Remove(entity.Id, out Entry old))
            old.Node.QueueFree();
        var root = new Node3D { Name = "V" + entity.Id };
        AddChild(root);
        _visuals[entity.Id] = new Entry { Entity = entity, Node = root, IsVisual = true, VisualScale = 1f };
    }

    // The M7 zaps slice (DV77): a beam segment (a tesla zap's lightning) is a quad along its way at a sprite's middle height, turned to
    // the camera; its picture is its appearance without the transform, placed by its pixel offsets (east = +x, north = -z). The M7
    // projectiles slice: a projectile's tracer (as long as its transform's scale), muzzle and impact, and the projectile itself (DV78).
    /// <summary>How big a flying projectile drawn from tg's picture is: its 32-pixel frame this many tiles across (the owner, 2026-11-20: a whole tile was 2 to 3 times too big).</summary>
    private const float ProjectilePictureTiles = 0.4f;

    private bool UpdateBeam(Entry e, double now)
    {
        if (Textures == null)
            return false;
        if (e.Billboard != null)
            e.Billboard.Visible = false;
        TableRef own = e.Entity.State.Appearance;
        if (own.IsNone || !_tables.TryGetAppearance(own, out AppearanceEntry root))
            return false;
        var unturned = new BeamSegment.Unturned(_tables, own);
        if (!Textures.TryGet(unturned, own, ByondDir.South, now - e.OwnSince, out _, out GodotPicture picture))
        {
            // BYOND shows nothing of a state its icon lacks (no placeholder box)
            bool blank = Textures.Compositor.IsBlank(unturned, own, ByondDir.South, rootOpaque: e.IsVisual);
            if (blank && e.Beam != null)
                e.Beam.Visible = false;
            return blank;
        }
        e.Beam ??= BeamSegment.Create(e.Node);
        e.Beam.Visible = true;
        float m = TileSpace.TileSize / 32f;
        bool projectile = e.Entity.TypePath.StartsWith(BeamSegment.ProjectileType, StringComparison.Ordinal);
        Vector3 way = BeamSegment.WayOf(root.Transform);
        if (projectile)
        {
            // The owner (2026-11-20): a projectile drawn from tg's picture flies as the 3D shots do (ShotFlight: the server's exact places, out
            // of the gun), its picture facing the eye at ProjectilePictureTiles (a 40mm grenade's shell among them).
            Vector3 at = Vector3.Zero;
            bool shown = true;
            if (e.Entity.Shot is ShotState flown)
            {
                shown = ShotFlight(e, flown, ref way, out Vector3 body, out _, out _);
                at = body - e.Node.GlobalPosition;
            }
            e.Beam.Visible = shown;
            bool shell = ShotLook.IsShell(e.Entity.TypePath);
            e.Beam.SetFacing(picture.Texture, at, way, ProjectilePictureTiles * (shell ? ShotLook.ShellSizeShare : 1f), root.Alpha / 255f);
            if (shell)
            {
                // the owner (2026-11-21): a launcher's shell trails fire
                e.ShellTrail ??= ShellTrail.Create(e.Node);
                e.ShellTrail.Follow(at, way, shown);
            }
            return true;
        }
        var middle = new Vector3(root.PixelX * m, 16 * m, -root.PixelY * m);
        float length = BeamSegment.LengthOf(root.Transform);
        if (e.Entity.Shot is ShotState shot)
            (middle, way, length) = ShotPlacement(e, shot, middle, way, length, false);
        e.Beam.Set(picture.Texture, middle, way, length, root.Alpha / 255f);
        return true;
    }

    /// <summary>The owner (2026-11-20): the own shot's light in the world: how far ahead of the eye and below it, its reach (metres), energy and life.</summary>
    private const float OwnFlashAhead = 1f, OwnFlashBelow = 0.5f, OwnFlashRange = 3f, OwnFlashEnergy = 2.5f;
    private const double OwnFlashSeconds = 0.12;

    /// <summary>D176: a muzzle flash's star (metres across) and its light's reach (tiles) and energy; the hand's height of another shooter's flash
    /// (the muzzle star: the owner, 2026-11-21, twice the first 0.45 m).</summary>
    private const float FlashStarMetres = 0.9f, FlashLightTiles = 1.5f, FlashEnergy = 1.5f, ImpactLightTiles = 1.5f, ImpactEnergy = 3f, ImpactStarMetres = 0.35f, HandHeight = 0.75f, HandReach = 0.45f, HandSide = 0.22f;

    // D176: tg's projectiles and their effects are the client's own 3D shots (ShotLook's kinds): a projectile or a tracer is a ShotVisual on the
    // shot's 3D line (ShotPlacement, DV78), a muzzle flash, a firing effect or an impact a ShotFlash (and an impact's HitBurst) once where it
    // appears; the own gun's firing effect kicks and flashes the first-person gun instead (OwnFired). tg's sprite of them is not drawn.
    private bool UpdateShot(Entry e, ShotKind kind, Vector3 position, double now)
    {
        if (Textures == null)
            return false;
        if (e.Billboard != null)
            e.Billboard.Visible = false;
        TableRef own = e.Entity.State.Appearance;
        if (own.IsNone || !_tables.TryGetAppearance(own, out AppearanceEntry root))
            return false;
        if (!e.ShotColoured)
        {
            // tg's picture gives the colour when the shot has neither a light nor a colour; one tg draws nothing of (the emitter's hitscan) stays unseen
            var unturned = new BeamSegment.Unturned(_tables, own);
            if (!Textures.TryGet(unturned, own, ByondDir.South, 0, out ComposedRaster raster, out _))
                return Textures.Compositor.IsBlank(unturned, own, ByondDir.South, rootOpaque: e.IsVisual);
            (float r, float g, float b) = ShotLook.Colour(e.Entity.State.Light, root.Rgba, raster.Image.Pixels);
            e.ShotColour = new Color(r, g, b);
            e.ShotColoured = true;
            if (kind is not (ShotKind.Muzzle or ShotKind.Impact or ShotKind.Firing) && e.Entity.Shot is ShotState fired)
                ShotColoured(LineStart(e, fired), e.ShotColour);
        }
        e.Node.Position = position; // the shot's line is read from the node (ShotPlacement); a new node is still at the origin
        float m = TileSpace.TileSize / 32f;
        bool projectile = ShotLook.IsProjectile(kind);
        var middle = projectile ? new Vector3(0f, 16 * m, 0f) : new Vector3(root.PixelX * m, 16 * m, -root.PixelY * m);
        Vector3 way = BeamSegment.WayOf(root.Transform);
        float length = BeamSegment.LengthOf(root.Transform);
        if (kind is ShotKind.Muzzle or ShotKind.Impact or ShotKind.Firing)
        {
            if (!e.ShotFlashed)
            {
                e.ShotFlashed = true;
                Flash(e, kind, root, middle, way, length);
            }
            return true;
        }
        float metres = length * TileSpace.TileSize;
        ShotState? line = e.Entity.Shot;
        float calibre = ShotLook.Calibre(e.Entity.TypePath);
        if (line is ShotState shot)
        {
            (middle, way, length) = ShotPlacement(e, shot, middle, way, length, projectile);
            metres = length * TileSpace.TileSize;
            if (projectile)
                return FlyShot(e, kind, shot, way, root, calibre);
            if (kind == ShotKind.Tracer)
                (middle, way, metres) = TracerFromGun(e, shot, middle, way, metres);
        }
        e.Shot ??= ShotVisual.Create(e.Node, kind);
        e.Shot.Calibre = calibre;
        e.Shot.Set(e.ShotColour, middle, way, metres, root.Alpha / 255f, e.Entity.State.Light);
        return true;
    }

    // A projectile on its line (ShotFlight), with its tracer from the muzzle (D285, ShotTrace).
    private bool FlyShot(Entry e, ShotKind kind, ShotState shot, Vector3 way, AppearanceEntry root, float calibre)
    {
        e.Shot ??= ShotVisual.Create(e.Node, kind);
        e.Shot.Calibre = calibre;
        bool shown = ShotFlight(e, shot, ref way, out Vector3 body, out float drawn, out float newest);
        e.Shot.Visible = shown;
        if (!shown)
            return true;
        e.Shot.Set(e.ShotColour, body - e.Node.GlobalPosition, way, 0f, root.Alpha / 255f, e.Entity.State.Light);
        // D285: the shot's tracer, a world effect of its own from the gun's muzzle to the head, which outlives the shot (ShotTrace)
        Vector3 start = LineStart(e, shot), ahead = Ahead(shot);
        Vector3 head = body + way * (ShotLook.Size(kind).Length * calibre / 2f);
        (float wide, float fade) = ShotLook.Trail(kind);
        e.Trace ??= ShotTrace.Create(this, e.Node, e.ShotTraceFrom ?? start + e.ShotFromMuzzle, start, ahead, e.ShotColour, wide * calibre, fade);
        // the server's newest place of the shot: where the tracer ends if the shot goes with no hit effect
        e.Trace.Follow(head, start + ahead * Math.Max(newest, drawn));
        return true;
    }

    // Where a projectile on its line is drawn at the render tick (world space, its body): where the server's exact places of it put it
    // (ShotLook.AlongAt: an even flight, not the tiles' stand-and-jump nor a pace guessed from them), coming out of the gun (MuzzleOffset); its
    // way there; how far along the line it is drawn and the server's newest place, metres. False while it is not drawn: its first metres (the
    // muzzle flash is there) and before its places give its pace.
    private bool ShotFlight(Entry e, ShotState shot, ref Vector3 way, out Vector3 body, out float drawn, out float newest)
    {
        Vector3 start = LineStart(e, shot), ahead = Ahead(shot);
        // The shot turned in flight (a ricochet, a reflection: the server starts its line again there, set_angle): its tracer ends at the
        // turn and a new one starts from it, and the shot is no longer drawn as leaving the gun. (A line that only changed its height
        // passed into another level and is the same line.)
        if (e.ShotLine is ShotState was && (was.OriginX != shot.OriginX || was.OriginY != shot.OriginY || was.Angle != shot.Angle))
        {
            // A homing shot's line turns a little every tick (DV96) and starts at its newest place, ahead of where it is drawn: its tracer goes
            // on from its drawn body (a chain along the arc), its places before keep giving the pace, and it is drawn along its own points.
            bool homing = ShotLook.SameFlight(was.AngleDegrees, shot.AngleDegrees);
            Vector3 turnAt = homing && e.ShotDrawnAt is Vector3 drawnAt ? drawnAt : start;
            e.Trace?.Finish(turnAt);
            e.Trace = null;
            e.ShotTraceFrom = turnAt;
            e.ShotTurned = true;
            if (homing)
            {
                e.ShotHoming = true;
                e.ShotFlownBefore += start.DistanceTo(LineStart(e, was));
            }
            else
            {
                e.ShotHoming = false;
                e.ShotFromMuzzle = Vector3.Zero;
                e.ShotFlownBefore = 0f;
                e.ShotPath?.Clear();
                e.ShotLineTick = e.Entity.Sample(0).Tick;
            }
        }
        e.ShotLine = shot;
        if (!e.ShotMuzzleKnown)
        {
            e.ShotFromMuzzle = GunPoint(shot.AngleDegrees, start, out Vector3 gun) ? gun - start : Vector3.Zero;
            e.ShotMuzzleKnown = true;
        }
        double slope = shot.SlopeValue, a = shot.AngleDegrees * Math.PI / 180.0, ux = Math.Sin(a), uy = Math.Cos(a);
        float perPixel = (float)(TileSpace.TileSize / 32.0 * Math.Sqrt(1 + slope * slope)); // metres along the 3D line per pixel along the map
        int count = 0;
        Span<(double, double)> samples = stackalloc (double, double)[e.Entity.SampleCount];
        for (int age = e.Entity.SampleCount - 1; age >= 0; age--)
        {
            EntitySample s = e.Entity.Sample(age);
            if (s.Tick < e.ShotLineTick || count > 0 && s.Tick == samples[count - 1].Item1)
                continue;
            _tables.TryGetAppearance(s.Appearance, out AppearanceEntry look);
            double px = s.X * 32.0 + (look?.PixelX ?? 0f), py = s.Y * 32.0 + (look?.PixelY ?? 0f);
            samples[count++] = (s.Tick, (px - shot.OriginX) * ux + (py - shot.OriginY) * uy);
        }
        double? along = ShotLook.AlongAt(samples[..count], _renderTick);
        drawn = along is double d ? (float)ShotLook.ClampAlong(d, shot.HeightPixels, slope) * perPixel : 0f;
        newest = count > 0 ? (float)ShotLook.ClampAlong(samples[count - 1].Item2, shot.HeightPixels, slope) * perPixel : 0f;
        float flown = e.ShotFlownBefore + drawn;
        body = start + ahead * drawn + MuzzleOffset(e, flown);
        if (flown < BoltBlendMetres) // still on its way from the muzzle to its line
            way = (ahead - e.ShotFromMuzzle / BoltBlendMetres).Normalized();
        if (count > 0)
            RememberShotPoint(e, samples[count - 1].Item1, start + ahead * newest + MuzzleOffset(e, e.ShotFlownBefore + newest));
        if (e.ShotHoming && ShotPathAt(e.ShotPath, _renderTick, out Vector3 onPath, out Vector3 pathWay))
        {
            body = onPath;
            if (pathWay != Vector3.Zero)
                way = pathWay;
        }
        e.ShotDrawnAt = body;
        double exit = ShotLook.ClampAlong(double.MaxValue, shot.HeightPixels, slope) * perPixel; // where the line leaves the level (its ceiling, its floor)
        return along != null && (e.ShotTurned || drawn >= ShotLook.HiddenNear(exit)); // a turned shot is far from the muzzle flash
    }

    // A hitscan's first tracer starts at its shooter's eye (DV94): seen from that eye it was a dot where it ended, as if it came from far ahead
    // (the owner, 2026-11-20); its tail is drawn at the gun (GunPoint, found once as for a bolt) instead.
    private (Vector3 Middle, Vector3 Way, float Metres) TracerFromGun(Entry e, ShotState shot, Vector3 middle, Vector3 way, float metres)
    {
        Vector3 node = e.Node.GlobalPosition, start = LineStart(e, shot);
        Vector3 a = node + middle - way * (metres / 2f), b = node + middle + way * (metres / 2f);
        (Vector3 tail, Vector3 head) = a.DistanceSquaredTo(start) <= b.DistanceSquaredTo(start) ? (a, b) : (b, a);
        if (tail.DistanceTo(start) > TracerAtEyeMetres)
            return (middle, way, metres);
        if (!e.ShotMuzzleKnown)
        {
            e.ShotFromMuzzle = GunPoint(shot.AngleDegrees, start, out Vector3 gun) ? gun - start : Vector3.Zero;
            e.ShotMuzzleKnown = true;
        }
        tail = start + e.ShotFromMuzzle;
        Vector3 span = head - tail;
        float length = span.Length();
        return length < 1e-3f ? (middle, way, metres) : ((head + tail) / 2f - node, span / length, length);
    }

    private const float TracerAtEyeMetres = 1f;

    // D285: a bolt's line starts at its shooter's eye (DV94); it is drawn coming out of the gun (GunPoint: the own first-person muzzle, else the
    // hand that holds the shooter's gun), on a straight way from the muzzle to its true line BoltBlendMetres out, then on that line (the owner,
    // 2026-11-20: joining it over 6 m bent the shot visibly).
    // A shot's place at its newest tick, on the line it had then (a homing shot's line changes every tick: ShotPathAt draws it between them).
    private static void RememberShotPoint(Entry e, double tick, Vector3 at)
    {
        List<(double Tick, Vector3 At)> path = e.ShotPath ??= new List<(double, Vector3)>();
        if (path.Count > 0 && path[^1].Tick >= tick)
            return;
        path.Add((tick, at));
        if (path.Count > ShotPathPoints)
            path.RemoveAt(0);
    }

    private const int ShotPathPoints = 8;

    // A homing shot's body at the render tick: between its ticks' own points (ShotLook.AlongAt on each axis), so it flies its arc evenly instead
    // of jumping to each new line's start; its way is along the arc there.
    private static bool ShotPathAt(List<(double Tick, Vector3 At)> path, double renderTick, out Vector3 at, out Vector3 way)
    {
        at = way = Vector3.Zero;
        if (path == null || path.Count == 0)
            return false;
        Span<(double, double)> xs = stackalloc (double, double)[path.Count], ys = stackalloc (double, double)[path.Count], zs = stackalloc (double, double)[path.Count];
        for (int i = 0; i < path.Count; i++)
            (xs[i], ys[i], zs[i]) = ((path[i].Tick, path[i].At.X), (path[i].Tick, path[i].At.Y), (path[i].Tick, path[i].At.Z));
        Vector3? Point(double tick, Span<(double, double)> x, Span<(double, double)> y, Span<(double, double)> z)
            => ShotLook.AlongAt(x, tick) is double px ? new Vector3((float)px, (float)ShotLook.AlongAt(y, tick)!, (float)ShotLook.AlongAt(z, tick)!) : null;
        if (Point(renderTick, xs, ys, zs) is not Vector3 now)
            return false;
        at = now;
        if (Point(renderTick - 0.5, xs, ys, zs) is Vector3 before && Point(renderTick + 0.5, xs, ys, zs) is Vector3 after && after.DistanceSquaredTo(before) > 1e-6f)
            way = (after - before).Normalized();
        return true;
    }

    private static Vector3 MuzzleOffset(Entry e, float flown) => e.ShotFromMuzzle * Mathf.Clamp(1f - flown / BoltBlendMetres, 0f, 1f);

    private const float BoltBlendMetres = 20f;

    // A shot's way ahead in 3D (its heading and its slope).
    private static Vector3 Ahead(ShotState shot)
    {
        float a = (float)(shot.AngleDegrees * Math.PI / 180.0);
        return new Vector3(Mathf.Sin(a), (float)shot.SlopeValue, -Mathf.Cos(a)).Normalized();
    }

    // Where a shot's line starts in the world (its shooter's eye for an aimed shot, DV94).
    private static Vector3 LineStart(Entry e, ShotState shot)
    {
        const float m = TileSpace.TileSize / 32f;
        return new Vector3((shot.OriginX - 16) * m, e.Node.GlobalPosition.Y + (float)(shot.HeightPixels * m), -(shot.OriginY - 16) * m);
    }

    // D285: a shot was coloured: the flashes of its firing take its colour, and a flash that comes after it finds it (FiringColour)
    private void ShotColoured(Vector3 origin, Color colour)
    {
        double now = _lastUpdateAt;
        _recentShots.RemoveAll(s => now - s.Time > SameFiringSeconds);
        _recentShots.Add((origin, colour, now));
        _recentFlashes.RemoveAll(f => now - f.Time > SameFiringSeconds || !IsInstanceValid(f.Flash));
        foreach ((ShotFlash flash, Vector3 at, _) in _recentFlashes)
        {
            if (at.DistanceTo(origin) < SameFiringMetres)
                flash.Recolour(colour);
        }
    }

    // The colour of the shot fired with a flash at the point: the shot's when it came first, else the flash's own (ShotColoured fixes it later).
    private Color FiringColour(Vector3 at, Color own)
    {
        double now = _lastUpdateAt;
        for (int i = _recentShots.Count - 1; i >= 0; i--)
        {
            if (now - _recentShots[i].Time <= SameFiringSeconds && _recentShots[i].Origin.DistanceTo(at) < SameFiringMetres)
                return _recentShots[i].Colour;
        }
        return own;
    }

    private void Remember(ShotFlash flash, Vector3 at)
    {
        if (flash != null)
            _recentFlashes.Add((flash, at, _lastUpdateAt));
    }

    // Where a bolt from the line's start comes out of a gun: the own first-person muzzle, else the gun hand of the mob whose eye it starts at.
    private bool GunPoint(double angleDegrees, Vector3 start, out Vector3 gun)
    {
        Entry shooter = null;
        float best = ShooterReach;
        foreach (Entry m in _entries.Values)
        {
            if (!m.HasPose || !m.Entity.IsMob() || m.Entity.Id == _ownId && _hideOwn)
                continue;
            Vector3 p = m.Node.GlobalPosition;
            float d = new Vector2(p.X - start.X, p.Z - start.Z).Length();
            if (d < best)
                (best, shooter) = (d, m);
        }
        // The line of the own shot starts at the eye of the click (DV94), which a runner has left by the time the shot is seen: the muzzle
        // is taken as it stands beside the eye now and put beside the start of the line, so the shot leaves along its line, not at an angle
        // from where the gun has got to (the owner, 2026-11-12). It is the own shot when the eye is nearer the start than any other mob.
        if (_hideOwn && GetViewport()?.GetCamera3D() is Camera3D eye)
        {
            Vector3 from = eye.GlobalPosition;
            float d = new Vector2(from.X - start.X, from.Z - start.Z).Length();
            if (d < OwnShotReach && (shooter == null || d < best))
            {
                // no first-person gun with a muzzle shown (none found in its picture, a mech's): below and right of the eye, not from its middle
                if (OwnMuzzle == null || !OwnMuzzle(out gun))
                    gun = from + eye.GlobalBasis * OwnMuzzleFallback;
                gun = start + (gun - from);
                return true;
            }
        }
        gun = default;
        if (shooter == null)
            return false;
        float a = (float)(angleDegrees * Math.PI / 180.0);
        var forward = new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a));
        gun = shooter.Entity.IsExosuit() ? ExosuitArm(shooter, forward)
            : shooter.Node.GlobalPosition + new Vector3(0f, HandHeight, 0f) + forward * HandReach + forward.Cross(Vector3.Up) * (GunInLeftHand(shooter) ? -HandSide : HandSide);
        return true;
    }

    // DV361: an exosuit's gun arm, higher and wider than a man's hand: the side of the arm that fired is its firing effect's line start
    // (ShotGeometry.MarkArm on the server), the right arm when no such effect is seen
    private const float ExosuitArmHeight = 1.1f, ExosuitArmReach = 0.55f, ExosuitArmSide = 0.6f;

    private Vector3 ExosuitArm(Entry mech, Vector3 forward)
    {
        const float m = TileSpace.TileSize / 32f;
        Vector3 feet = mech.Node.GlobalPosition, right = forward.Cross(Vector3.Up);
        float side = ExosuitArmSide;
        foreach (Entry fx in _entries.Values)
        {
            if (fx.Entity.VisHost != mech.Entity.Id || fx.Entity.Shot is not ShotState line)
                continue;
            var start = new Vector3((line.OriginX - 16) * m, feet.Y, -(line.OriginY - 16) * m);
            side = right.Dot(start - feet) < 0f ? -ExosuitArmSide : ExosuitArmSide;
            break;
        }
        return feet + new Vector3(0f, ExosuitArmHeight, 0f) + forward * ExosuitArmReach + right * side;
    }

    // tg's in-hand pictures of guns (icons/mob/inhands/weapons/guns_lefthand.dmi and _righthand) on the mob: a gun in its left hand only
    private bool GunInLeftHand(Entry mob)
    {
        if (!_tables.TryGetAppearance(mob.Entity.State.Appearance, out AppearanceEntry look))
            return false;
        bool left = false, right = false;
        foreach (TableRef overlay in look.Overlays)
        {
            if (!_tables.TryGetAppearance(overlay, out AppearanceEntry o) || _tables.ResolveResource(o.Icon) is not string icon || !icon.Contains("/guns", StringComparison.Ordinal))
                continue;
            left |= icon.Contains("lefthand", StringComparison.Ordinal);
            right |= icon.Contains("righthand", StringComparison.Ordinal);
        }
        return left && !right;
    }

    // D176: the flash of a muzzle, a firing effect or an impact where it appears, if the viewer can see that tile.
    private void Flash(Entry e, ShotKind kind, AppearanceEntry root, Vector3 middle, Vector3 way, float length)
    {
        EntityState state = e.Entity.State;
        long host = e.Entity.VisHost;
        if (kind == ShotKind.Firing && host != 0 && host == _ownId)
        {
            Camera3D camera = GetViewport()?.GetCamera3D();
            Vector3 eye = camera?.GlobalPosition ?? e.Node.GlobalPosition;
            Color fired = FiringColour(eye, e.ShotColour);
            Remember(OwnFired?.Invoke(fired), eye);
            // the owner (2026-11-20): the muzzle's flash lights the world for a moment, a little ahead of the shooter and below the eye
            if (camera != null)
            {
                Vector3 ahead = -camera.GlobalBasis.Z with { Y = 0f };
                Vector3 lit = eye + (ahead.LengthSquared() > 1e-6f ? ahead.Normalized() : Vector3.Zero) * OwnFlashAhead + Vector3.Down * OwnFlashBelow;
                Remember(ShotFlash.Spawn(this, lit, fired, 0f, OwnFlashRange, OwnFlashEnergy, OwnFlashSeconds), eye);
            }
            return;
        }
        if (InSight != null && !InSight(state.X, state.Y, state.Z))
            return;
        Color colour = e.ShotColour;
        Vector3 at;
        ShotFlash flash;
        switch (kind)
        {
            case ShotKind.Firing:
                // another shooter's gun: at the hand's height, a little out along the shot (the effect's dir: tg's firing_dir)
                Vector3 feet = host != 0 && _entries.TryGetValue(host, out Entry h) && h.HasPose ? h.Node.GlobalPosition : e.Node.GlobalPosition;
                Vector3 forward = e.Entity.Shot is ShotState aim
                    ? new Vector3(Mathf.Sin((float)(aim.AngleDegrees * Math.PI / 180)), 0f, -Mathf.Cos((float)(aim.AngleDegrees * Math.PI / 180)))
                    : Vector3.Forward.Rotated(Vector3.Up, TileSpace.DirYaw(state.Dir == 0 ? ByondDir.South : state.Dir));
                Entry holder = host != 0 && _entries.TryGetValue(host, out Entry hostEntry) ? hostEntry : null;
                if (holder != null && holder.HasPose && holder.Entity.IsExosuit())
                    at = ExosuitArm(holder, forward); // DV361: a mech's gun arm
                else
                {
                    bool left = holder != null && GunInLeftHand(holder);
                    at = feet + new Vector3(0f, HandHeight, 0f) + forward * HandReach + forward.Cross(Vector3.Up) * (left ? -HandSide : HandSide);
                }
                flash = ShotFlash.Spawn(this, at, FiringColour(at, colour), FlashStarMetres, FlashLightTiles * TileSpace.TileSize, FlashEnergy, ShotLook.FlashSeconds);
                Remember(flash, at);
                return;
            case ShotKind.Muzzle:
                if (e.Entity.Shot is ShotState line)
                    (middle, _, _) = ShotPlacement(e, line, middle, way, length, false);
                at = e.Node.GlobalPosition + middle;
                LightState light = state.Light;
                flash = ShotFlash.Spawn(this, at, FiringColour(at, colour), FlashStarMetres, (light.IsNone ? FlashLightTiles : Mathf.Max(light.Range, 1f)) * TileSpace.TileSize,
                    light.IsNone ? FlashEnergy : light.Power, ShotLook.FlashSeconds);
                Remember(flash, at);
                return;
            default:
                // an impact: where the shot's line ended (protocol version 34: its point and its height there), else at its picture's middle
                const float m = TileSpace.TileSize / 32f;
                at = e.Entity.Shot is ShotState end
                    ? new Vector3((float)(end.OriginX / 32.0 - 0.5) * TileSpace.TileSize, TileSpace.WorldY(state.Z) + (float)end.HeightPixels * m,
                        -(float)(end.OriginY / 32.0 - 0.5) * TileSpace.TileSize)
                    : e.Node.GlobalPosition + middle;
                // the owner (2026-10-29): the hit surface's debris (a wall, else a structure on the tile; none off a body) and sparks in the shot's colour
                if (ImpactSurface(state.X, state.Y, state.Z) is { } surface)
                {
                    HitBurst.Spawn(this, at, HitMaterials.Look(surface));
                    // the owner (2026-11-11): the hit stays seen, a bullet's hole (tg's plain impact_effect) or an energy shot's scorch (none on a body)
                    ShotMark.Spawn(this, at, ShotLook.MarkRadius(e.Entity.TypePath == "/obj/effect/temp_visual/impact_effect"));
                }
                HitBurst.Sparks(this, at, colour, 12);
                ShotTrace.Land(at);
                // the star a little toward the eye, out of the face it hit
                Vector3 toEye = (GetViewport()?.GetCamera3D()?.GlobalPosition ?? at) - at;
                Vector3 starAt = at + (toEye.LengthSquared() > 0.01f ? toEye.Normalized() * 0.1f : Vector3.Zero);
                ShotFlash.Spawn(this, starAt, colour, ImpactStarMetres, ImpactLightTiles * TileSpace.TileSize, ImpactEnergy, ShotLook.ImpactLightSeconds);
                return;
        }
    }

    // What a shot's impact on a tile hit (tg puts the impact on the wall's turf, or on the hit thing's): the closed turf, else the first
    // structure or machine there, else nothing when a body is there (its blood is tg's own splatter), else the floor.
    private HitMaterial? ImpactSurface(int x, int y, int z)
    {
        string turf = _replica?.TurfTypeAt(x, y, z);
        if (turf != null && turf.StartsWith("/turf/closed", StringComparison.Ordinal))
            return HitMaterials.OfTurf(turf);
        bool body = false;
        foreach (Entry other in _entries.Values)
        {
            var s = other.Entity.State;
            if (s.X != x || s.Y != y || s.Z != z || other.Entity.TypePath is not string path)
                continue;
            if (path.StartsWith("/obj/structure", StringComparison.Ordinal) || path.StartsWith("/obj/machinery", StringComparison.Ordinal))
                return HitMaterials.OfThing(path);
            body |= other.Entity.IsMob();
        }
        return body ? null : HitMaterials.OfTurf(turf);
    }

    // Protocol version 34 (D177, DV78 revised): a shot's segment at its 3D line's height. A moving projectile's node glides along its tiles
    // (DV67), so its middle is moved onto the true line (the nearest point of the line); a tracer, a muzzle or an impact keeps its own place.
    // The segment's two ends take the line's heights there, so a rising or falling shot is drawn tilted.
    private (Vector3 Middle, Vector3 Way, float Length) ShotPlacement(Entry e, ShotState shot, Vector3 middle, Vector3 way, float length, bool projectile)
    {
        const float m = TileSpace.TileSize / 32f;
        Vector3 node = e.Node.GlobalPosition;
        double px = (node.X + middle.X) / TileSpace.TileSize * 32.0 + 16.0, py = -(node.Z + middle.Z) / TileSpace.TileSize * 32.0 + 16.0;
        double angle = shot.AngleDegrees * Math.PI / 180.0, ux = Math.Sin(angle), uy = Math.Cos(angle);
        if (projectile)
        {
            // (kept ahead of the line's start and under the ceiling: the node's tile is not on the line, ShotLook.ClampAlong)
            double along = ShotLook.ClampAlong((px - shot.OriginX) * ux + (py - shot.OriginY) * uy, shot.HeightPixels, shot.SlopeValue);
            (double lx, double ly) = (shot.OriginX + ux * along, shot.OriginY + uy * along);
            middle += new Vector3((float)((lx - px) * m), 0f, (float)(-(ly - py) * m));
            (px, py) = (lx, ly);
        }
        double half = length * 16.0; // half the segment's length along its way, tile pixels
        double ax = px + way.X * half, ay = py - way.Z * half, bx = px - way.X * half, by = py + way.Z * half;
        // the heights by the signed distance along the heading: an end behind the line's start is lower on a rising shot, not higher
        double ha = shot.HeightPixels + shot.SlopeValue * ((ax - shot.OriginX) * ux + (ay - shot.OriginY) * uy);
        double hb = shot.HeightPixels + shot.SlopeValue * ((bx - shot.OriginX) * ux + (by - shot.OriginY) * uy);
        var span = new Vector3((float)((ax - bx) * m), (float)((ha - hb) * m), (float)(-(ay - by) * m));
        middle.Y = (float)((ha + hb) / 2 * m);
        float spanLength = span.Length();
        return spanLength < 1e-4f ? (middle, way, length) : (middle, span / spanLength, spanLength / TileSpace.TileSize);
    }

    // M4b slice 3 (D18, D334): a hotspot's burning air follows its icon state (tg's fire stage), its light colour (update_color's glow) and its alpha.
    private bool UpdateFire(Entry e)
    {
        e.Fire ??= FireVolume.Create(e.Node);
        string state = null;
        if (_tables.TryGetAppearance(e.Entity.State.Appearance, out AppearanceEntry root))
            state = _tables.ResolveString(root.IconState);
        LightState light = e.Entity.State.Light;
        Color tint = light.IsNone ? FireVolume.DefaultTint : new Color(light.Rgba) { A = 1f };
        e.Fire.Set(FireVolume.StageOf(state), tint, root != null ? root.Alpha / 255f : 1f);
        // a very hot fire's overlays (update_color): sparkles, lightning, fusion
        float sparkle = 0f, fusion = 0f;
        bool lightning = false;
        if (root != null)
        {
            foreach (TableRef overlay in root.Overlays)
            {
                if (!_tables.TryGetAppearance(overlay, out AppearanceEntry o))
                    continue;
                switch (_tables.ResolveString(o.IconState))
                {
                    case "shieldsparkles": sparkle = o.Alpha / 255f; break;
                    case "overcharged": lightning = true; break;
                    case "fusion_gas": fusion = o.Alpha / 255f; break;
                }
            }
        }
        e.Fire.SetExtras(sparkle, lightning, fusion);
        return true;
    }

    // D303: tg's smoke follows its colour and its alpha, which tg's fade_out animates.
    private bool UpdateSmoke(Entry e)
    {
        e.Smoke ??= SmokeVolume.Create(e.Node);
        Color tint = Colors.White;
        float alpha = 1f;
        if (_tables.TryGetAppearance(e.Entity.State.Appearance, out AppearanceEntry root))
        {
            tint = new Color(root.Rgba) { A = 1f };
            alpha = root.Alpha / 255f;
            if (e.Entity.Animation != null
                && AnimationPlayback.TryEvaluate(e.Entity.Animation, _renderTick, Anomaly.Core.Motion.MotionMath.TicksPerSecond, AnimatedBase.Of(root), out AnimatedLook moved, 255))
                alpha = (float)Math.Clamp(moved.Alpha, 0, 1);
        }
        e.Smoke.Set(tint, alpha, SmokeVolume.IsThin(e.Entity.TypePath));
        return true;
    }

    // DV247 (gaps F1): tg's foam and foamed metal as a volume on their tile (Render/FoamVolume.cs), each face the entity's picture now: its own
    // icon state, or the flick that plays (the foam's dissolve), with its colour and alpha.
    private bool UpdateFoam(Entry e, double now)
    {
        if (Textures == null)
            return false;
        TableRef own = e.Entity.State.Appearance;
        if (own.IsNone)
            return false;
        if (_tables.TryGetAppearance(own, out AppearanceEntry ownEntry) && (ownEntry.Icon, ownEntry.IconState) != e.OwnSeen)
        {
            e.OwnSeen = (ownEntry.Icon, ownEntry.IconState);
            e.OwnSince = now;
        }
        TableRef appearance = own;
        double seconds = now - e.OwnSince;
        if (FlickNow(e, out TableRef flicked, out double age))
        {
            appearance = flicked;
            seconds = age;
        }
        if (!Textures.TryGet(_tables, appearance, ByondDir.South, seconds, out ComposedRaster _, out GodotPicture picture))
        {
            if (e.Foam != null)
                e.Foam.Visible = false;
            return Textures.Compositor.IsBlank(_tables, appearance, ByondDir.South, rootOpaque: false);
        }
        e.Foam ??= FoamVolume.Create(e.Node, FoamVolume.IsBlock(e.Entity.TypePath));
        e.Foam.Visible = true;
        e.Foam.Set(picture.Texture, picture.Translucent);
        return true;
    }

    // The owner's play test (2026-10-09): tg showed its attack visual on the entity (EntityFlags.Hit rises for the tick of the
    // attack): a burst of pixel particles, blood red on a mob, grey chips on a thing. It starts where the player's own click
    // met the picture when that click was just now (the owner, 2026-10-10), else from the middle of the picture.
    private void ApplyHit(Entry e, double now)
    {
        bool hit = e.Entity.WasHit;
        if (hit && !e.HitSeen)
        {
            e.HitStart = now;
            Vector3 at;
            if (now - e.AimAt <= AimedHitSeconds)
                at = e.Billboard.GlobalPosition + e.AimOffset;
            else
            {
                SpriteRect rect = e.Billboard.Rect;
                at = e.Billboard.GlobalPosition + new Vector3(0, (float)(rect.Bottom + rect.Top) / 2f, 0);
            }
            e.AimAt = double.NegativeInfinity;
            HitBurst.Spawn(this, at, HitMaterials.Look(HitMaterials.OfThing(e.Entity.TypePath)));
        }
        e.HitSeen = hit;
    }

    // The owner's play test (2026-10-09): tg's attack lunge (EntityFlags.Lunge rises for the tick of an attack): the picture moves 8 tg pixels
    // toward the target and back over 0.3 s, turning 15 degrees (LungeVisual).
    private void ApplyLunge(Entry e, GodotPicture picture, bool lying, float scale, double now)
    {
        byte dir = e.Entity.LungeDir;
        if (dir != 0 && !e.LungeSeen)
        {
            e.LungeStart = now;
            e.LungeDir = dir;
            e.LungeTurn = LungeVisual.TurnSign(dir, GD.Randi() % 2 == 0);
        }
        e.LungeSeen = dir != 0;
        MeshInstance3D quad = e.Billboard.GetChildCount() > 0 ? e.Billboard.GetChild(0) as MeshInstance3D : null;
        double age = e.LungeStart < 0 ? LungeVisual.Seconds : now - e.LungeStart;
        if (age >= LungeVisual.Seconds)
        {
            e.LungeStart = -1;
            if (e.Lunging && !e.Spinning && quad != null)
                quad.MaterialOverride = e.Billboard.PictureMaterial;
            e.Lunging = false;
            return;
        }
        (double east, double north) = LungeVisual.Offset(e.LungeDir, age);
        float metresPerPixel = TileSpace.TileSize / 32f * scale;
        e.Billboard.Position += new Vector3((float)east * metresPerPixel, 0f, -(float)north * metresPerPixel);
        if (lying || e.Spinning || quad == null)
            return;
        _spinShader ??= GD.Load<Shader>("res://shaders/picture_billboard_spin.gdshader");
        e.SpinMaterial ??= new ShaderMaterial { Shader = _spinShader };
        ShaderMaterial source = picture.Standing;
        e.SpinMaterial.SetShaderParameter("picture", source.GetShaderParameter("picture"));
        e.SpinMaterial.SetShaderParameter("scissor", source.GetShaderParameter("scissor"));
        // BYOND turns clockwise; the shader's roll is counter-clockwise in the quad's plane
        e.SpinMaterial.SetShaderParameter("spin", (float)(-LungeVisual.Turn(e.LungeTurn, age) * Math.PI / 180.0));
        SpriteRect rect = e.Billboard.Rect;
        e.SpinMaterial.SetShaderParameter("pivot", new Vector2((float)(rect.Left + rect.Right) / 2f, (float)(rect.Bottom + rect.Top) / 2f));
        quad.MaterialOverride = e.SpinMaterial;
        e.Lunging = true;
    }

    // M4a P7 (D127): a thing flagged Thrown rises and falls over a short arc and tumbles; the tiles it crosses and where it lands are the server's.
    private void ApplyThrow(Entry e, GodotPicture picture, bool lying, double now)
    {
        bool thrown = e.Entity.IsThrown && !e.Entity.IsMob();
        double age = _throws.Age(e.Entity.Id, thrown, now);
        MeshInstance3D quad = e.Billboard.GetChildCount() > 0 ? e.Billboard.GetChild(0) as MeshInstance3D : null;
        if (!thrown)
        {
            if (e.Spinning && quad != null)
                quad.MaterialOverride = e.Billboard.PictureMaterial;
            e.Spinning = false;
            return;
        }
        if (e.Entity.State.Flight.IsNone) // DV416: a free motion's arc is the server's own (in the pose); D127's drawn arc only without one
            e.Billboard.Position += new Vector3(0, (float)ThrowVisual.Height(age), 0);
        if (lying || quad == null)
            return;
        _spinShader ??= GD.Load<Shader>("res://shaders/picture_billboard_spin.gdshader");
        e.SpinMaterial ??= new ShaderMaterial { Shader = _spinShader };
        ShaderMaterial source = picture.Standing;
        e.SpinMaterial.SetShaderParameter("picture", source.GetShaderParameter("picture"));
        e.SpinMaterial.SetShaderParameter("scissor", source.GetShaderParameter("scissor"));
        e.SpinMaterial.SetShaderParameter("spin", (float)ThrowVisual.Spin(age));
        SpriteRect rect = e.Billboard.Rect;
        e.SpinMaterial.SetShaderParameter("pivot", new Vector2((float)(rect.Left + rect.Right) / 2f, (float)(rect.Bottom + rect.Top) / 2f));
        quad.MaterialOverride = e.SpinMaterial;
        e.Spinning = true;
    }

    /// <summary>
    /// DV30 (the owner's play test, 2026-10-09): the body zone of entity <paramref name="id"/>'s picture, as it is drawn now, under tg's
    /// (<paramref name="iconX"/>, <paramref name="iconY"/>) of a click (<see cref="BodyZoneAim"/>); None for a thing that is not a mob or has no body part there
    /// (package T, slice T1: a mob built from body parts is picked on its body alone, <see cref="CollectPickShapes"/>, so a click on it has one).
    /// </summary>
    public AimZone AimZoneAt(long id, byte iconX, byte iconY, (int X, int Y)? picture = null)
    {
        if (Textures == null || _tables == null)
            return AimZone.None;
        foreach (Entry e in _list)
        {
            if (e.Entity.Id != id)
                continue;
            if (!e.Entity.IsMob() || e.Billboard == null || !e.Billboard.Visible)
                return AimZone.None;
            // the pick's unclamped pixel when it has one: a giant's or a lying body's parts reach past the icon square the click's pixel is clamped to
            return picture is (int x, int y)
                ? BodyZoneAim.ZoneAtPixel(_tables, Textures.Compositor, e.Entity.State.Appearance, e.DrawnDir, x, y)
                : BodyZoneAim.ZoneAt(_tables, Textures.Compositor, e.Entity.State.Appearance, e.DrawnDir, iconX, iconY);
        }
        return AimZone.None;
    }

    /// <summary>
    /// M4a P7 (interaction.md 4.1): the pick shapes of everything this layer draws as a node (mobs, items that moved, thrown things): a picture as a
    /// standing or flat quad on its opaque pixels, a placeholder as a box. <paramref name="exclude"/> is the own mob.
    /// </summary>
    /// <summary>Whether <see cref="ClickThrough"/>'s types are left out of the pick shapes (the game: yes; the map editor, which places and selects them: no).</summary>
    public bool EffectsClickThrough { get; set; } = true;

    public void CollectPickShapes(List<PickShape> into, long exclude)
    {
        foreach (Entry e in _list)
        {
            if (e.Entity.Id == exclude || !e.Node.Visible || !e.HasPose)
                continue;
            // tg's particle effects are mouse-transparent (effect_system.dm:13, effects_water.dm:7: MOUSE_OPACITY_TRANSPARENT): a click goes
            // through a water puff or a trail to what is behind it.
            if (NearEyeFade.Applies(e.Entity.TypePath))
                continue;
            // tg's short-lived visuals (the attack's flick visual, temp visuals, a shot's effects) are mouse-transparent too (ClickThrough); not for the map editor
            if (EffectsClickThrough && ClickThrough.IsType(e.Entity.TypePath))
                continue;
            int z = e.Pose.Z;
            double floor = TileSpace.WorldY(z);
            int tx = (int)Math.Round(e.Pose.X), ty = (int)Math.Round(e.Pose.Y);
            PickSurface surface = PickSurface.Billboard;
            // DV247: a foamed metal wall drawn as a block is clicked anywhere on its tile's block (tg's 32-pixel opaque tile)
            if (e.Foam != null && e.Foam.Visible && FoamVolume.IsBlock(e.Entity.TypePath))
            {
                Vector3 centre = e.Node.Position;
                double halfTile = TileSpace.TileSize / 2.0;
                into.Add(PickShape.BoxOf(e.Entity.Id, surface, tx, ty, z, centre.X - halfTile, -centre.Z - halfTile, centre.Y - floor, centre.X + halfTile, -centre.Z + halfTile, centre.Y - floor + TileSpace.LevelHeight, WorldScale.WallHeight));
                continue;
            }
            if (e.Billboard != null && e.Billboard.Visible && e.Billboard.Raster != null)
            {
                Vector3 at = e.Billboard.GlobalPosition;
                PickShape picture = PickShape.StandingPicture(e.Entity.Id, surface, tx, ty, z, at.X, -at.Z, at.Y - floor, e.Billboard.Rect, e.Billboard.Raster.Image, e.Billboard.Raster.OriginX, e.Billboard.Raster.OriginY);
                picture.OnTop = e.OnSlot; // a thing put on a rack's shelf is clicked over the rack (PickShape.OnTop)
                picture.FacesEye = e.Billboard.FacesEye;
                picture.HitMask = e.Entity.IsMob() ? BodyMaskOf(e) : null; // package T, slice T1: a carbon is hit on its body, not its clothes or carried things
                into.Add(picture);
                continue;
            }
            if (e.Placeholder != null && !e.Placeholder.Visible)
                continue; // drawn as nothing (a blank picture)
            // A placeholder: the box around the feet.
            Vector3 feet = e.Node.Position;
            bool mob = e.Entity.IsMob();
            double half = mob ? TileSpace.CapsuleRadius : 0.3, height = mob ? TileSpace.CapsuleHeight : 0.5;
            into.Add(PickShape.BoxOf(e.Entity.Id, surface, tx, ty, z, feet.X - half, -feet.Z - half, feet.Y - floor, feet.X + half, -feet.Z + half, feet.Y - floor + height, WorldScale.WallHeight));
        }
        // DV114: an override image (a delusion) is drawn in place of its entity, which is hidden; BYOND sends a click on the image's pixels to the
        // atom it hangs on, so the image's picture is that entity's pick shape
        foreach (Entry o in _visuals.Values)
        {
            long host = o.Entity.VisHost;
            if (!o.Entity.IsOverride || host == 0 || host == exclude || !o.Node.Visible || o.Billboard == null || !o.Billboard.Visible || o.Billboard.Raster == null
                || !_entries.TryGetValue(host, out Entry hidden) || !hidden.HasPose)
                continue;
            int z = hidden.Pose.Z;
            Vector3 at = o.Billboard.GlobalPosition;
            into.Add(PickShape.StandingPicture(host, PickSurface.Billboard, (int)Math.Round(hidden.Pose.X), (int)Math.Round(hidden.Pose.Y), z, at.X, -at.Z, at.Y - TileSpace.WorldY(z),
                o.Billboard.Rect, o.Billboard.Raster.Image, o.Billboard.Raster.OriginX, o.Billboard.Raster.OriginY));
        }
    }

    // Package T, slice T1: the body-only hit mask of a mob's drawn picture (BodyZoneAim.BodyMask; null: no body parts, the whole picture is hit), made
    // again only when the picture, the appearance or the frame changed.
    private Raster BodyMaskOf(Entry e)
    {
        if (Textures == null || _tables == null)
            return null;
        ComposedRaster raster = e.Billboard.Raster;
        TableRef look = e.Entity.State.Appearance;
        if (!ReferenceEquals(raster, e.MaskRaster) || look != e.MaskLook || e.DrawnDir != e.MaskDir)
        {
            e.Mask = BodyZoneAim.BodyMask(_tables, Textures.Compositor, look, e.DrawnDir, raster);
            (e.MaskRaster, e.MaskLook, e.MaskDir) = (raster, look, e.DrawnDir);
        }
        return e.Mask;
    }

    /// <summary>D288: whether the mobs' pictures cast shadows (the preset's character shadows).</summary>
    public bool MobShadows
    {
        get => _mobShadows;
        set
        {
            _mobShadows = value;
            foreach (Entry e in _entries.Values)
                if (e.Billboard != null)
                    e.Billboard.CastsShadow = value && e.Entity.IsMob();
        }
    }

    private bool _mobShadows = true;

    private bool EnsureBillboard(Entry e)
    {
        if (e.Billboard != null)
            return true;
        bool pointArrowType = e.Entity.TypePath != null && e.Entity.TypePath.StartsWith(PointArrowType, StringComparison.Ordinal);
        e.Billboard = new Billboard
        {
            Name = "Picture", Visible = false, CastsShadow = MobShadows && e.Entity.IsMob(),
            Reflects = e.Entity.IsMob() || (PropKinds.IsItem(e.Entity.TypePath) && !pointArrowType),
        };
        e.Node.AddChild(e.Billboard);
        return true;
    }

    // Things that arrive while playing are built over the next frames, nearest first, within SpawnBudgetMs a frame: walking into a new part
    // of the map brings hundreds at once (M10's interest), and building them in one frame was a visible stall. Mobs and visuals come at once.
    private const double SpawnBudgetMs = 4;
    private readonly Dictionary<long, ReplicaEntity> _pending = new();
    private ReplicaEntity[] _pendingOrder = [];
    private double[] _pendingDistance = [];

    private void Enqueue(ReplicaEntity entity)
    {
        if (entity.IsVisual || entity.IsMob())
            Add(entity);
        else
            _pending[entity.Id] = entity;
    }

    private void AddPending(double viewerX, double viewerY)
    {
        if (_pending.Count == 0)
            return;
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        int count = _pending.Count;
        if (_pendingOrder.Length < count)
        {
            _pendingOrder = new ReplicaEntity[count * 2];
            _pendingDistance = new double[count * 2];
        }
        _pending.Values.CopyTo(_pendingOrder, 0);
        for (int i = 0; i < count; i++)
        {
            double dx = _pendingOrder[i].State.X - viewerX, dy = _pendingOrder[i].State.Y - viewerY;
            _pendingDistance[i] = dx * dx + dy * dy;
        }
        // Each distance once and the runtime's key sort: a comparison delegate over a few thousand pending things took the editor's Debug
        // build past the budget alone, so one thing was built a frame and the queue only grew (performance.md section 5).
        Array.Sort(_pendingDistance, _pendingOrder, 0, count);
        for (int i = 0; i < count; i++)
        {
            ReplicaEntity entity = _pendingOrder[i];
            _pending.Remove(entity.Id);
            long one = System.Diagnostics.Stopwatch.GetTimestamp();
            Add(entity);
            Dev.FrameProbe.Item("spawn", entity.TypePath, System.Diagnostics.Stopwatch.GetElapsedTime(one).TotalMilliseconds);
            if (System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds >= SpawnBudgetMs)
                break;
        }
        Array.Clear(_pendingOrder, 0, count); // holds no despawned entity
    }

    private void Add(ReplicaEntity entity)
    {
        if (entity.IsVisual)
        {
            AddVisual(entity);
            return;
        }
        if (_entries.ContainsKey(entity.Id))
            Remove(entity);
        if (_host != null && _host.OnSpawned(entity))
            return;
        AddNode(entity);
    }

    private static Shader _placeholderShader;

    // The stand-in shapes take the light grid's term too (M3 I1c): a StandardMaterial3D with no term was black in a lit room (the cameras and canisters the real server sends with no picture).
    private static ShaderMaterial PlaceholderMaterial(Color color)
    {
        _placeholderShader ??= GD.Load<Shader>("res://shaders/placeholder_lit.gdshader");
        var material = new ShaderMaterial { Shader = _placeholderShader };
        material.SetShaderParameter("color", color);
        return material;
    }

    // The node of an entity: a mob, a mover, or a static prop that left the host's batch.
    private void AddNode(ReplicaEntity entity)
    {
        if (_entries.ContainsKey(entity.Id))
            return;
        var root = new Node3D { Name = "E" + entity.Id };
        Color color = Palette[(int)(((entity.Id % Palette.Length) + Palette.Length) % Palette.Length)];
        var material = PlaceholderMaterial(color);
        var placeholder = new Node3D { Name = "Placeholder" };
        if (entity.IsMob())
        {
            placeholder.AddChild(new MeshInstance3D { Mesh = _capsule, MaterialOverride = material, Position = new Vector3(0, TileSpace.CapsuleHeight / 2, 0) });
            // The front of a node is -Z: the nose points the way the mob faces.
            placeholder.AddChild(new MeshInstance3D { Mesh = _nose, MaterialOverride = PlaceholderMaterial(color.Lightened(0.5f)), Position = new Vector3(0, 1.45f, -0.38f) });
            MobCount++;
        }
        else
        {
            var grey = PlaceholderMaterial(new Color(0.62f, 0.55f, 0.42f));
            placeholder.AddChild(new MeshInstance3D { Mesh = _marker, MaterialOverride = grey, Position = new Vector3(0, 0.25f, 0) });
        }
        root.AddChild(placeholder);
        var entry = new Entry { Entity = entity, Node = root, Placeholder = placeholder };
        if (_namesBuilt)
            AttachLabel(entry);
        AddChild(root);
        _entries[entity.Id] = entry;
        entry.ListIndex = _list.Count;
        _list.Add(entry);
    }

    private void Remove(ReplicaEntity entity)
    {
        _pending.Remove(entity.Id);
        if (_visuals.Remove(entity.Id, out Entry visual))
        {
            visual.Node.QueueFree(); // its duration ended: the server deleted it (tg's QDEL_IN_CLIENT_TIME)
            if (visual.AbsorbedBy != 0)
                (_host as MapRenderer ?? _map)?.ReleaseVisual(visual.AbsorbedBy, entity.Id);
            return;
        }
        _throws.Forget(entity.Id);
        using (Dev.FrameProbe.Scope("map/OnDespawned"))
            if (_host != null && !_entries.ContainsKey(entity.Id) && _host.OnDespawned(entity))
                return;
        if (!_entries.Remove(entity.Id, out Entry entry))
            return;
        // the last entry takes its place: a walk's despawns come hundreds at once, and List.Remove scanned the list for each
        Entry last = _list[^1];
        _list[entry.ListIndex] = last;
        last.ListIndex = entry.ListIndex;
        _list.RemoveAt(_list.Count - 1);
        if (entity.IsMob())
            MobCount--;
        entry.Node.QueueFree();
    }

    private void BuildNameLabels()
    {
        _namesBuilt = true;
        foreach (Entry e in _list)
            AttachLabel(e);
    }

    private static void AttachLabel(Entry e)
    {
        e.Label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            PixelSize = 0.005f,
            FontSize = 48,
            OutlineSize = 12,
            Position = new Vector3(0, e.Entity.IsMob() ? 2.15f : 0.85f, 0),
            Visible = false,
        };
        e.Node.AddChild(e.Label);
    }

    /// <summary>The other mobs' names over their heads (the options' "Names over other players' heads"; the owner, 2026-10-09).</summary>
    public bool PlayerNames { get; set; } = true;

    /// <summary>
    /// The third-person view's line of sight (FovMask; null in the other views): a thing whose tile (x, y, z) is out of the own mob's
    /// sight is not drawn, as Space Station 14 hides what is outside the field of view. The own mob is always drawn.
    /// </summary>
    public Func<float, float, int, bool> InSight { get; set; }

    /// <summary>DV374: the own mob sees the other mobs through the walls (its SEE_MOBS: <see cref="ObserverVision.SeesMobs"/>).</summary>
    public bool SeeMobs { get; set; }

    // A mob's name tag: its replicated name over the head, hidden behind walls (unlike the names overlay's labels, F4). It sits above
    // the top of the drawn picture (the owner's play test, 2026-10-10: at the eye height it covered the head), low over a
    // lying mob, at the old height over a placeholder.
    private static void UpdateNameTag(Entry e, bool show)
    {
        if (!show)
        {
            if (e.NameTag != null)
                e.NameTag.Visible = false;
            return;
        }
        if (e.NameTag == null)
        {
            e.NameTag = new Label3D
            {
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                PixelSize = 0.004f,
                FontSize = 40,
                OutlineSize = 10,
                VerticalAlignment = VerticalAlignment.Bottom,
                Modulate = new Color(1f, 1f, 0.92f),
                Position = new Vector3(0, TileSpace.EyeHeight + 0.45f, 0),
            };
            e.Node.AddChild(e.NameTag);
        }
        float up = TileSpace.EyeHeight + 0.45f;
        if (e.Billboard != null && e.Billboard.Visible && e.Billboard.Raster != null)
        {
            if (!ReferenceEquals(e.NameRaster, e.Billboard.Raster))
            {
                e.NameRaster = e.Billboard.Raster;
                AtlasBuilder.TrimBounds(e.NameRaster.Image, out _, out e.NameTransparentTop, out _, out _);
            }
            float spriteTop = e.Billboard.Position.Y + (float)(e.Billboard.Rect.Top - e.NameTransparentTop * e.Billboard.Rect.PixelSize);
            float ceiling = TileSpace.WorldY(e.Entity.State.Z) + WorldScale.WallHeight - e.Node.Position.Y;
            up = NameTagPlacement.Height(spriteTop, ceiling);
        }
        e.NameTag.Position = new Vector3(0, up, 0);
        string name = e.Entity.State.Name;
        e.NameTag.Visible = !string.IsNullOrEmpty(name);
        if (e.NameTag.Text != name)
            e.NameTag.Text = name ?? string.Empty;
    }

    private static string LabelText(ReplicaEntity entity, bool own)
    {
        string name = string.IsNullOrEmpty(entity.State.Name) ? "?" : entity.State.Name;
        return own ? $"{name} (you) #{entity.Id}" : $"{name} #{entity.Id}";
    }

    // A thing whose pose is on a tile's centre (not moving between tiles): that tile.
    // DV416: a thing resting where its free motion left it (a rest point above the floor: it landed on a table) lies on what is on its tile
    private static bool RestsWhereLanded(ReplicaEntity entity, RenderPose pose, out int x, out int y)
    {
        x = (int)MathF.Round(pose.X);
        y = (int)MathF.Round(pose.Y);
        Anomaly.Core.Motion.FlightArc arc = entity.State.Flight;
        return !arc.IsNone && !arc.IsMoving && arc.Lift0 > 1f;
    }

    private static bool AtRest(RenderPose pose, out int x, out int y)
    {
        x = (int)MathF.Round(pose.X);
        y = (int)MathF.Round(pose.Y);
        return MathF.Abs(pose.X - x) < 0.02f && MathF.Abs(pose.Y - y) < 0.02f;
    }

    private sealed class Entry
    {
        public int ListIndex; // where it is in _list
        /// <summary>Drawn on a resting slot (a closet's shelf): its own pixel offset is not used.</summary>
        public bool OnSlot;

        public ReplicaEntity Entity;
        public Node3D Node;
        public Node3D Placeholder;
        public Billboard Billboard;
        public Label3D Label;
        public Label3D NameTag;
        public ComposedRaster NameRaster;
        public int NameTransparentTop;
        public ComposedRaster TopRaster; // D346: the picture TryGetPictureTop last measured, and its transparent rows on top
        public int TopTransparent;
        public EntityPose Pose;
        public bool HasPose;
        public bool HasLast;
        public Vector3 LastPosition;
        public float StartX, StartY;
        public bool Spinning;
        public ShaderMaterial SpinMaterial;
        public byte DrawnDir;
        public Raster Mask; // BodyMaskOf's cache and what it was made for
        public ComposedRaster MaskRaster;
        public TableRef MaskLook;
        public byte MaskDir;
        public bool HitSeen;
        public double HitStart = double.NegativeInfinity;
        public WalkSway Walk;
        public bool MeleeVisual;
        public double MeleeStart = -1;
        public Vector3 AimOffset;
        public Vector3? PointSpot; // DV232: the arrow's aimed point (from the followed thing when PointFollow is set)
        public long PointFollow;
        public Vector3 PointStart; // DV232: where tg's slide starts (the pointer's tile, the hand's height) and when the arrow came
        public double PointBorn;
        public double AimAt = double.NegativeInfinity;
        public bool LungeSeen;
        public double LungeStart = -1;
        public byte LungeDir;
        public int LungeTurn;
        public bool Lunging;
        public bool Animating;
        public ShaderMaterial AnimateMaterial;
        public FireVolume Fire;
        public SmokeVolume Smoke;
        public FoamVolume Foam; // DV247: tg's foam or foamed metal drawn as a volume
        public BlastVolume Blast;
        public BeamSegment Beam;
        public ShotVisual Shot; // D176: a projectile or a tracer drawn in 3D
        public ShotTrace Trace; // D285: a bullet's tracer, a world effect that outlives it
        public ShellTrail ShellTrail; // the owner (2026-11-21): a launcher shell's fire trail
        public bool ShotFlashed; // D176: its muzzle flash or impact was shown (once)
        public bool ShotColoured;
        public Color ShotColour;
        public long ShotLineTick; // the tick the shot's line last turned (a ricochet): its places before it are on the old line (FlyShot)
        public ShotState? ShotLine; // the line the shot was last drawn on: a new one is a turn in flight (a ricochet)
        public Vector3 ShotFromMuzzle; // D285: an own bolt's start, from its line's start to the first-person muzzle
        public bool ShotMuzzleKnown; // ShotFromMuzzle was found (once, when the shot is first drawn)
        public bool ShotTurned; // the shot's line turned in flight (ShotFlight)
        public bool ShotHoming; // its line turns a little every tick (a homing shot, DV96): drawn along ShotPath
        public List<(double Tick, Vector3 At)> ShotPath; // the shot's places at its last ticks, each on the line it had then (ShotPathAt)
        public float ShotFlownBefore; // metres a homing shot flew on its earlier lines: its muzzle offset fades over all of them
        public Vector3? ShotDrawnAt; // where its body was last drawn (a homing turn's tracer goes on from there)
        public Vector3? ShotTraceFrom; // where its next tracer starts after a turn, instead of the muzzle
        public SingularityLens Lens;
        public SingularityGlow Glow;
        public (TableRef Icon, TableRef State) OwnSeen;
        public double OwnSince;
        public TableRef FlickSeen;
        public long FlickSeenTick;
        public double FlickLength;
        public LyingFacing Facing = new();
        public bool IsVisual; // M4v slice 3: a short-lived visual (NetField.VisHost), drawn by UpdateVisuals
        public ComposedRaster FitRaster; // DV122: the picture TallFit last measured, and its scale
        public float FitScale = 1f;
        public MeshInstance3D Flat; // version 30: a T-ray image that lies on the floor (UpdateFlatImage)
        public ComposedRaster FlatRaster; // the picture the flat quad shows now
        public float VisualScale = 1f; // its host's scale (VisualAnchor)
        public int? HostTrim; // a data HUD image: its mob's dropped rows (Billboard.BottomTrim), so it keeps its rows in the mob's cell
        public long AbsorbedBy; // the static thing whose model draws this visual as its overlay (MapRenderer.TryAbsorbVisual), else 0
        public bool VisualFlat; // on a wall decal's face, not turned to the viewer
        public float EyeFade = 1f; // DV46: a particle effect's alpha near the own eye (NearEyeFade)
        public bool ShiftKnown; // DV46: a particle of the own body is drawn from the drawn body (NearEyeFade.OwnShift), fixed when it appears
        public float ShiftX, ShiftY;
    }
}
