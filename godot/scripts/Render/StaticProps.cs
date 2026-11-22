using System.Linq;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Models;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

/// <summary>
/// The static-versus-moving rule (docs/ai/specs/m3-ownership.md, "Things every package should know"): an entity that is not a mob, has
/// no capsule and has received no x, y or z change since it spawned is a <b>static prop</b>, drawn in its chunk's batched MultiMesh;
/// the first position change (or a despawn and respawn) takes it out of the batch for good, as a node of the <see cref="EntityLayer"/>.
/// Mobs are always nodes.
/// </summary>
public interface IStaticPropHost
{
    /// <summary>An entity spawned (or exists at the start). True: the host draws it, the layer makes no node.</summary>
    bool OnSpawned(ReplicaEntity entity);

    /// <summary>An entity despawned. True: it was the host's.</summary>
    bool OnDespawned(ReplicaEntity entity);

    /// <summary>An entity left the batch because it moved: the layer makes a node for it now.</summary>
    event Action<ReplicaEntity> PropMoved;
}

/// <summary>One instance of a chunk's MultiMesh: its transform, colour and custom data (the prop shader's inputs).</summary>
internal sealed class PropInstance
{
    public Transform3D Transform;
    public Color Color;
    public Color Custom;
    public int Index;

    /// <summary>The model mesh this instance draws (<see cref="ModelLibrary"/>), or -1 for a picture quad or a shaped piece.</summary>
    public int Mesh = -1;

    /// <summary>It casts every lamp's shadow, as a wall does (D288: an airlock or a blast door, which shut is opaque in tg); the other pieces and models are things.</summary>
    public bool Wall;
}

/// <summary>One static entity the renderer draws as part of the world.</summary>
internal sealed class StaticRecord
{
    public ReplicaEntity Entity;
    public Placed Placed;
    public ChunkBatch Batch;
    public (int X, int Y, int Z) Cell;
    public TableRef SeenAppearance;

    /// <summary>The dir the entity had when its instances were last built (M3 R5: a flat thing is turned by it, and smart pipes change theirs when the atmos init gives them their connections).</summary>
    public byte SeenDir;
    public bool SeenSolid;
    /// <summary>A lamp: whether it was lit when its instances were last built (its picture is tg's glow then, D142).</summary>
    public bool SeenLit;
    /// <summary>A lamp: its box's half width and height as drawn (metres), for the pick box.</summary>
    public float LampHalfWidth = WorldGeometry.LampHalfWidth, LampHalfHeight = WorldGeometry.LampHalfHeight;
    public bool PictureIsFallback;
    /// <summary>A piece whose picture is its own appearance (M4v slice 2: a flick plays on it; not a swatch, a counterpart or a fallback).</summary>
    public bool PieceOwnPicture;
    // D142: the icon state animation of the picture (null: still), when it started (BYOND restarts it with a new icon state), and the
    // last flick: its one-shot animation, its start on the renderer's clock and the server tick it carried; the picture shown now.
    public AtlasAnimation Animation;
    public double AnimationStart;
    public AtlasAnimation Flick;
    public double FlickStart;
    public long SeenFlickTick;
    public int ShownId = -1;
    // M4v slice 1 (protocol version 9): the animate() chains last seen on the entity, and the instance they move with its resting transform.
    public EntityAnimation SeenAnimation;
    public PropInstance LookInstance;
    public Transform3D LookRest;
    public int PictureId = -1;
    public int Frames = 1;
    public readonly List<PropInstance> Instances = new();
    public bool IsDoor;
    public DoorAnimState Door;

    /// <summary>
    /// The icon states of tg's short-lived visuals on it (flick_overlay_view: a button's success or error light) that its model draws as overlays
    /// (MapRenderer.TryAbsorbVisual), by the visual's id; empty when none.
    /// </summary>
    public readonly Dictionary<long, string> VisualOverlays = new();

    /// <summary>A flick a model of each frame draws (ModelEntry.FlickFrame): its state and icon while it plays, else null; the model that stands in now.</summary>
    public string FlickModelState, FlickModelIcon;
    public Anomaly.Client.Shared.Models.ModelEntry ModelOverride;

    /// <summary>The 3D model that draws the thing (docs/ai/specs/models.md), or null.</summary>
    public Anomaly.Client.Shared.Models.ModelEntry Model;

    /// <summary>A model's origin and yaw as built (a closet's slots and hinge are placed from them).</summary>
    public Vector3 ModelOrigin;
    /// <summary>The owner's play test (2026-10-16): while a thing a model draws is moved (pulled, pushed, blown), how far it is drawn from where it was placed.</summary>
    public Vector3 CarryOffset;
    /// <summary>The transforms its model instances had where it was placed (CarryOffset moves them from these).</summary>
    public List<Transform3D> CarryBase;
    public float ModelYaw;

    /// <summary>
    /// A model's live frames (Anomaly.Client.Shared.Models.ModelFrame): the instances drawn with the thing's own picture, composed for one dir, the state of the frame they
    /// were made from (a flick of another shape is not drawn on them) and that dir. Its animation and flicks step these instances.
    /// </summary>
    public readonly List<PropInstance> LiveFrames = new();
    /// <summary>Overlay surfaces with independent timelines; updated in place without rebuilding chunks.</summary>
    public readonly List<(PropInstance Instance, AtlasAnimation Animation)> OverlayFrames = new();
    public string LiveState;
    public byte LiveDir;
    public bool LiveSquare;

    /// <summary>
    /// A closet or a crate (ModelRule.Closet, ModelRule.Crate): how far its door or lid is open (0 shut .. 1), whether it is opening, whether the open state was ever
    /// seen, and a closet's door state (<c>blue</c> of <c>blue_door</c>).
    /// </summary>
    public float ClosetOpen;
    public bool ClosetOpening, ClosetSeen, ClosetRestOpen, ClosetOverlaySeen;
    public string ClosetDoor;

    /// <summary>
    /// A closet's door or a crate's lid instances: their hinge in the model, its axis, the swing at open fraction 1 (degrees) and the slide (a drawer); a part a
    /// flick moves (ModelPart.Flick, null for none) and its move fitted to the world.
    /// </summary>
    public readonly List<(PropInstance Instance, Vector3 Pivot, Vector3 Axis, float Degrees, Vector3 Move, ModelFlick Flick, Vector3 FlickMove, Vector3 OpenScale)> Hinged = new();

    /// <summary>The flick that moves the parts a flick moves (ModelPart.Flick), when it started on the renderer's clock, and how far they are toward its farthest pose.</summary>
    public AtlasAnimation PartFlick;
    public double PartFlickStart;
    public float FlickOpen;

    /// <summary>A model's turning parts (ModelPart.Spin: a gravity generator's field ring): their pivot, axis and degrees per second.</summary>
    public readonly List<(PropInstance Instance, Vector3 Pivot, Vector3 Axis, float Speed)> Spinning = new();

    /// <summary>A solar panel's or tracker's head instances (ModelRule.Sun): their tilt pivot and the catalog's turn of their part; the aim they show.</summary>
    public readonly List<(PropInstance Instance, Vector3 Pivot, float YawOffset)> SunHeads = new();
    public Anomaly.Client.Shared.Models.SunHeadMotion SunMotion;

    /// <summary>A model's blinking parts (ModelPart.BlinkOn: a canister's low-pressure light): their instances, where they stand lit, and their part.</summary>
    public readonly List<(PropInstance Instance, Transform3D At, Anomaly.Client.Shared.Models.ModelPart Part, Color LitColor, Color LitCustom, int OffPicture)> Blinking = new();
    public string SeenState;
}

/// <summary>
/// The MultiMeshes of one 16 x 16 chunk (scope 2i): the picture quads (static props and wall decals), the shaped pieces, and one per 3D model part the
/// chunk has (docs/ai/specs/models.md: a model part's instances draw only its own vertices). Instances are kept on the CPU and written into the MultiMeshes
/// by <see cref="Rebuild"/> when the chunk changed; a door's animation updates only its instances' colour.
/// </summary>
internal sealed class ChunkBatch
{
    private sealed class ModelGroup
    {
        public readonly List<PropInstance> Instances = new();
        public MultiMeshInstance3D Node, WallNode;
    }

    /// <summary>
    /// The map editor's far view (docs/ai/specs/map-editor.md 5.6): a model part is not drawn from further than this many times its radius, where it is
    /// a few pixels (470: about four on a screen 1440 pixels high). From high above a station most parts are that small, and each is a draw call
    /// of every chunk that has one. 0, the game's: every part is drawn.
    /// </summary>
    public static float SmallPartRange { get; set; }

    private MultiMeshInstance3D _quads, _pieces, _wallPieces;
    private readonly List<PropInstance> _things = new(), _walls = new();
    private readonly Mesh _quadMesh, _pieceMesh;
    private readonly Func<int, Mesh> _modelMesh;
    private readonly Func<int, Material> _modelMaterial;
    private readonly Material _material;
    private readonly Dictionary<int, ModelGroup> _models = new();

    public ChunkBatch(ChunkPlan plan, Node3D root, Mesh quadMesh, Mesh pieceMesh, Material material, Aabb bounds, Func<int, Mesh> modelMesh = null,
        Func<int, Material> modelMaterial = null)
    {
        Plan = plan;
        Root = root;
        Bounds = bounds;
        _quadMesh = quadMesh;
        _pieceMesh = pieceMesh;
        _material = material;
        _modelMesh = modelMesh;
        _modelMaterial = modelMaterial;
    }

    public ChunkPlan Plan { get; }

    public Node3D Root { get; }

    public Aabb Bounds { get; private set; }

    /// <summary>
    /// DEVIATION(DV403): the box the batch is culled by takes in <paramref name="more"/> (the whole way a tram carries some of its things), so
    /// the carried things are drawn where they ride and not cut with the chunk they were planned in.
    /// </summary>
    public void Reach(Aabb more)
    {
        if (Bounds.Encloses(more))
            return;
        Bounds = Bounds.Merge(more);
        foreach (MultiMeshInstance3D node in new[] { _quads, _pieces, _wallPieces }.Concat(_models.Values.SelectMany(g => new[] { g.Node, g.WallNode })))
        {
            if (node == null)
                continue;
            node.CustomAabb = Bounds; // the node's box is the one the frustum culls by
            if (node.Multimesh != null)
                node.Multimesh.CustomAabb = Bounds;
        }
    }

    public readonly List<PropInstance> Quads = new();

    public readonly List<PropInstance> Pieces = new();

    public bool Dirty { get; set; }

    /// <summary>Instances now (quads, pieces and model parts), for the statistics.</summary>
    public int InstanceCount => Quads.Count + Pieces.Count + _models.Values.Sum(g => g.Instances.Count);

    /// <summary>Adds an instance of a model part (its <see cref="PropInstance.Mesh"/>).</summary>
    public void AddModel(PropInstance instance)
    {
        if (!_models.TryGetValue(instance.Mesh, out ModelGroup group))
            _models[instance.Mesh] = group = new ModelGroup();
        group.Instances.Add(instance);
    }

    /// <summary>Removes instances of any kind.</summary>
    public void RemoveAll(HashSet<PropInstance> gone)
    {
        Quads.RemoveAll(gone.Contains);
        Pieces.RemoveAll(gone.Contains);
        foreach (int mesh in gone.Where(i => i.Mesh >= 0).Select(i => i.Mesh).Distinct())
        {
            if (_models.TryGetValue(mesh, out ModelGroup group))
                group.Instances.RemoveAll(gone.Contains);
        }
    }

    /// <summary>Writes the instance lists into the MultiMeshes.</summary>
    public void Rebuild()
    {
        Dirty = false;
        Fill(ref _quads, Quads, _quadMesh, "Props", castShadow: false);
        FillShapes(ref _pieces, ref _wallPieces, Pieces, _pieceMesh, "Pieces");
        foreach ((int mesh, ModelGroup group) in _models)
            FillShapes(ref group.Node, ref group.WallNode, group.Instances, _modelMesh(mesh), "Model" + mesh, _modelMaterial?.Invoke(mesh)); // a crystal's own shader
    }

    // The things' instances and the walls' (PropInstance.Wall) in their own MultiMeshes, on their own layers.
    private void FillShapes(ref MultiMeshInstance3D things, ref MultiMeshInstance3D walls, List<PropInstance> list, Mesh mesh, string name, Material material = null)
    {
        _things.Clear();
        _walls.Clear();
        foreach (PropInstance p in list)
            (p.Wall ? _walls : _things).Add(p);
        Fill(ref things, _things, mesh, name, castShadow: true, material);
        Fill(ref walls, _walls, mesh, name + "Wall", castShadow: true, material, wall: true);
    }

    // D288: the 3D shapes (pieces, models) cast the lamps' and the sun's shadows; the flat pictures do not
    private void Fill(ref MultiMeshInstance3D node, List<PropInstance> list, Mesh mesh, string name, bool castShadow, Material material = null, bool wall = false)
    {
        if (list.Count == 0)
        {
            if (node != null)
                node.Visible = false;
            return;
        }
        if (node == null)
        {
            node = new MultiMeshInstance3D
            {
                Name = name,
                MaterialOverride = material ?? _material,
                CastShadow = castShadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
                Layers = castShadow && !wall ? Anomaly.Client.Light.LightLayer.ThingLayer : 1u, // the nearest lamps' shadows alone hold the things (D288)
                CustomAabb = Bounds,
            };
            Root.AddChild(node);
        }
        node.Visible = true;
        if (SmallPartRange > 0f && mesh != _quadMesh && mesh != _pieceMesh)
            node.VisibilityRangeEnd = SeenFrom(mesh, list);
        MultiMesh multi = node.Multimesh;
        if (multi == null)
        {
            // One MultiMesh per node for good, refilled in place: a new one per refill kept its engine RID until the GC freed the old one,
            // and a cascade's crystals, refilling chunks every frame, ran Godot out of them ("Element limit reached", the M7 visual check).
            multi = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true,
                UseCustomData = true,
                Mesh = mesh,
                // its own box (the node's is not enough): without one each buffer upload measures the instances again and tells the lamps
                // around that they changed, so an animation frame anywhere in the chunk redrew their shadows every frame (D288's light)
                CustomAabb = Bounds,
            };
            node.Multimesh = multi;
        }
        multi.InstanceCount = list.Count;
        var buffer = new float[list.Count * MultiMeshBuffer.Stride];
        Span<float> v = stackalloc float[MultiMeshBuffer.Stride];
        for (int i = 0; i < list.Count; i++)
        {
            PropInstance p = list[i];
            p.Index = i;
            Basis b = p.Transform.Basis;
            Vector3 o = p.Transform.Origin;
            Color c = p.Color, u = p.Custom;
            (v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8]) = (b.Row0.X, b.Row0.Y, b.Row0.Z, b.Row1.X, b.Row1.Y, b.Row1.Z, b.Row2.X, b.Row2.Y, b.Row2.Z);
            (v[9], v[10], v[11]) = (o.X, o.Y, o.Z);
            (v[12], v[13], v[14], v[15], v[16], v[17], v[18], v[19]) = (c.R, c.G, c.B, c.A, u.R, u.G, u.B, u.A);
            MultiMeshBuffer.Write(buffer, i, v);
        }
        multi.Buffer = buffer;
        multi.CustomAabb = Bounds; // things came or went: the lamps around draw their shadows again
        _cpu[multi] = buffer;
        _stale.Remove(multi);
        _moved.Remove(multi);
    }

    // How far a model part's instances of this chunk are drawn from (SmallPartRange): by the radius of the largest of them. The engine measures from the
    // middle of the node's box, which is the chunk's, so the chunk's own reach is added: a part is always drawn while the camera is that near to it.
    private float SeenFrom(Mesh mesh, List<PropInstance> list)
    {
        float scale = 0f;
        foreach (PropInstance p in list)
        {
            Basis b = p.Transform.Basis;
            scale = Math.Max(scale, Math.Max(b.X.LengthSquared(), Math.Max(b.Y.LengthSquared(), b.Z.LengthSquared())));
        }
        return SmallPartRange * mesh.GetAabb().Size.Length() * 0.5f * MathF.Sqrt(scale) + Bounds.Size.Length() * 0.5f;
    }

    /// <summary>Updates the custom data of one picture quad in place (D142: the next picture of an animation).</summary>
    public void SetQuadCustom(PropInstance instance, Color custom)
    {
        if (instance.Custom == custom)
            return;
        instance.Custom = custom;
        WriteColor(_quads, instance, 16, custom); // a dirty batch is refilled from Custom
    }

    /// <summary>Updates the transform of one picture quad in place (M4v: an animate() chain moves it, and its basis carries the look).</summary>
    public void SetQuadTransform(PropInstance instance, Transform3D transform)
    {
        if (instance.Transform == transform)
            return;
        instance.Transform = transform;
        WriteTransform(_quads, instance, transform);
    }

    /// <summary>Updates the custom data of one piece or model instance in place (M4v slice 2: the next picture of a piece's animation).</summary>
    public void SetPieceCustom(PropInstance instance, Color custom)
    {
        if (instance.Custom == custom)
            return;
        instance.Custom = custom;
        WriteColor(NodeOf(instance), instance, 16, custom);
    }

    // The MultiMesh a piece or model instance is drawn by.
    private MultiMeshInstance3D NodeOf(PropInstance instance)
        => instance.Mesh >= 0 ? (_models.TryGetValue(instance.Mesh, out ModelGroup group) ? (instance.Wall ? group.WallNode : group.Node) : null)
            : instance.Wall ? _wallPieces : _pieces;

    /// <summary>Updates the transform of one model instance in place (a closet's door on its hinge).</summary>
    public void SetModelTransform(PropInstance instance, Transform3D transform)
    {
        if (instance.Transform == transform)
            return;
        instance.Transform = transform;
        WriteTransform(NodeOf(instance), instance, transform);
    }

    /// <summary>Updates the colour of one piece or model instance in place (a door's open fraction).</summary>
    public void SetPieceColor(PropInstance instance, Color color)
    {
        if (instance.Color == color)
            return;
        instance.Color = color;
        WriteColor(NodeOf(instance), instance, 12, color); // a dirty batch is refilled from Color
    }

    // In-place updates go to the CPU copy of the buffer and reach the GPU once per frame (UploadPending): Godot's SetInstance* on a
    // MultiMesh whose buffer was just set reads the whole buffer back from the GPU first, a 30+ ms stall each time a chunk refilled.
    private static readonly HashSet<ChunkBatch> Pending = new();
    private readonly Dictionary<MultiMesh, float[]> _cpu = new();
    private readonly HashSet<MultiMesh> _stale = new();
    // the stale ones whose instances moved or changed shape (a transform, or the colour a door's open fraction rides in): their shadows are
    // drawn again; a new picture of an animation (the custom data) leaves the lamps' shadows as they were
    private readonly HashSet<MultiMesh> _moved = new();

    private float[] BufferOf(MultiMeshInstance3D node, PropInstance instance, bool moves)
    {
        if (Dirty || node?.Multimesh == null || instance.Index >= node.Multimesh.InstanceCount
            || !_cpu.TryGetValue(node.Multimesh, out float[] buffer))
            return null;
        _stale.Add(node.Multimesh);
        if (moves)
            _moved.Add(node.Multimesh);
        Pending.Add(this);
        return buffer;
    }

    private void WriteColor(MultiMeshInstance3D node, PropInstance instance, int offset, Color c)
    {
        float[] b = BufferOf(node, instance, moves: offset == 12);
        if (b == null)
            return;
        int o = instance.Index * MultiMeshBuffer.Stride + offset;
        (b[o], b[o + 1], b[o + 2], b[o + 3]) = (c.R, c.G, c.B, c.A);
    }

    private void WriteTransform(MultiMeshInstance3D node, PropInstance instance, Transform3D t)
    {
        float[] b = BufferOf(node, instance, moves: true);
        if (b == null)
            return;
        int o = instance.Index * MultiMeshBuffer.Stride;
        for (int row = 0; row < 3; row++)
        {
            Vector3 r = row == 0 ? t.Basis.Row0 : row == 1 ? t.Basis.Row1 : t.Basis.Row2;
            (b[o + row * 4], b[o + row * 4 + 1], b[o + row * 4 + 2], b[o + row * 4 + 3]) = (r.X, r.Y, r.Z, t.Origin[row]);
        }
    }

    /// <summary>Sends the frame's in-place updates of every batch to the GPU, one whole buffer per changed MultiMesh.</summary>
    public static void UploadPending()
    {
        foreach (ChunkBatch batch in Pending)
        {
            foreach (MultiMesh multi in batch._stale)
            {
                if (!batch._cpu.TryGetValue(multi, out float[] buffer) || multi.InstanceCount * MultiMeshBuffer.Stride != buffer.Length)
                    continue;
                multi.Buffer = buffer;
                if (batch._moved.Contains(multi))
                    multi.CustomAabb = batch.Bounds; // tells the lamps around (the box itself is the chunk's, as before)
            }
            batch._stale.Clear();
            batch._moved.Clear();
        }
        Pending.Clear();
    }
}
