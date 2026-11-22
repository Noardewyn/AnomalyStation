using System.Linq;
using Godot;
using Anomaly.Client.Render;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Models;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Client;

// Overlays drawn as 3D models on what they are stuck to (ModelRule.Attach, docs/ai/specs/models.md): C-4's charge (plastic.dm:150, plastic-explosive2) on the
// face of a wall it was planted on, on the side of a door, lying on a floor. The overlay's dir is the side the planter stood on (DV63); the turf's picture
// leaves the overlay out (TileKit.Hidden), so a wall no longer shows the charge on all four of its sides.
public partial class MapRenderer
{
    /// <summary>The height above the floor of a charge on a wall or a door (metres): an alarm's.</summary>
    private static float AttachHeight => WorldScale.PanelHeight;

    /// <summary>How far above a floor a charge lying on it sits (metres), so that its back does not fight with the tile.</summary>
    private const float FloorLift = 0.01f;

    // One overlay's model on the thing it is stuck to: its instances, the ones that show the overlay's picture (its live frames) and the overlay's animation.
    private sealed class Attachment
    {
        public ChunkBatch Batch;
        public readonly List<PropInstance> Instances = new();
        public readonly List<PropInstance> Live = new();
        public readonly List<PropInstance> Flash = new(); // ModelEntry.FlashMaterial's pieces: they glow on its flash frames
        public int[] FlashFrames = Array.Empty<int>();
        public AtlasAnimation Animation;
        public double Start;
        public int Shown = -1;
    }

    private readonly Dictionary<(int X, int Y, int Z), List<Attachment>> _turfAttachments = new();
    private readonly Dictionary<StaticRecord, List<Attachment>> _recordAttachments = new();
    private readonly HashSet<Attachment> _attachedAnimated = new();
    private readonly Dictionary<(int X, int Y, int Z), (List<TileCell> Tiles, ChunkBatch Batch)> _turfAttachRetry = new();
    private int _attachDecodesSeen;

    /// <summary>The model an overlay is drawn as on what it is stuck to (<see cref="ModelRule.Attach"/>); null for an overlay drawn in the picture.</summary>
    private ModelEntry AttachModelOf(TableRef overlay)
    {
        if (_models == null || !_replica.TryGetAppearance(overlay, out AppearanceEntry o) || o.Icon.IsNone)
            return null;
        ModelEntry model = _models.Catalog.Find(_replica.ResolveResource(o.Icon), _replica.ResolveString(o.IconState));
        return model?.Rule == ModelRule.Attach ? model : null;
    }

    private bool IsAttachedOverlay(TableRef overlay) => AttachModelOf(overlay) != null;

    // DV397: tg's ladder hole rim (ladders.dm add_ladder_rim: the turf's "ladder_rim" overlay) outlines the whole 2D hole; the 3D hatch's frame is the ladder model's
    private bool IsLadderRim(TableRef overlay)
        => _replica != null && _replica.TryGetAppearance(overlay, out AppearanceEntry o) && _replica.ResolveResource(o.Icon) == "icons/obj/structures.dmi"
            && _replica.ResolveString(o.IconState) is string state && state.StartsWith("ladder", StringComparison.Ordinal) && state.EndsWith("_rim", StringComparison.Ordinal);

    private const float MiningCrackLayer = 2.06f; // tg's BULLET_HOLE_LAYER

    // DV214: tg's wall dents (walls.dm add_dent: effects.dmi's bullet_hole and impact1-3) are drawn on the face their dir names, not on every side
    private bool IsWallDent(TableRef overlay)
    {
        if (_replica == null || !_replica.TryGetAppearance(overlay, out AppearanceEntry o))
            return false;
        string icon = _replica.ResolveResource(o.Icon), state = _replica.ResolveString(o.IconState);
        if (icon == "icons/turf/overlays.dmi")
            return IsMiningCrack(o);
        return icon == "icons/effects/effects.dmi" && (state == "bullet_hole" || state is "impact1" or "impact2" or "impact3");
    }

    // DV348: a mining crack is tg's torn wall crack on BULLET_HOLE_LAYER; the torn wall's own stays in the wall's picture
    private bool IsMiningCrack(AppearanceEntry o)
        => _replica.ResolveResource(o.Icon) == "icons/turf/overlays.dmi" && _replica.ResolveString(o.IconState) == "explodable"
            && Mathf.IsEqualApprox(o.Layer, MiningCrackLayer);

    // The appearance a thing's pictures are made from: without its attached overlays when it is drawn in 3D (a shaped piece, a model), which carries them as
    // models (AttachToRecord); a thing drawn as a picture (an item, a sign) keeps them in it.
    private TableRef Look(StaticRecord record)
    {
        if (HiddenOverlay(record, out TableRef overlay))
            return overlay;
        TableRef appearance = record.Entity.State.Appearance;
        if (_stripper == null || !_stripper.HasHidden(appearance) || !DrawnIn3D(record))
            return appearance;
        return _stripper.Strip(appearance);
    }

    // A part another thing's model draws (ModelEntry.Hidden) shows only its overlay, which that model has not (the gravity generator's centre: its field's animation).
    private bool HiddenOverlay(StaticRecord record, out TableRef overlay)
    {
        overlay = default;
        if (record.Model is not { Hidden: true } || !_replica.TryGetAppearance(record.Entity.State.Appearance, out AppearanceEntry entry) || entry.OverlayCount == 0)
            return false;
        overlay = entry.Overlays.First();
        return true;
    }

    private bool DrawnIn3D(StaticRecord record)
    {
        if (record.Placed.Kind == PlacedKind.Piece)
            return record.Placed.Shape != RenderKind.LightFixture;
        if (record.Placed.Kind is PlacedKind.Item or PlacedKind.FloorDecal || _models == null
            || !_replica.TryGetAppearance(record.Entity.State.Appearance, out AppearanceEntry entry))
            return false;
        ModelEntry model = _models.Catalog.Find(_replica.ResolveResource(entry.Icon), record.SeenState, OverlayStates(entry));
        return model != null && model.Rule != ModelRule.Attach;
    }

    // A chunk's walls and floors: the models of their attached overlays, made again with the chunk's turfs.
    private void AttachTurfs((int X, int Y, int Z) key, IEnumerable<TileCell> tiles, ChunkBatch batch)
    {
        if (_turfAttachments.Remove(key, out List<Attachment> old))
            Detach(old);
        _turfAttachRetry.Remove(key);
        List<Attachment> made = null;
        foreach (TileCell tile in tiles)
        {
            // DV577: a ceiling's tile shows the shots' dents on its cell's top, which its cell's turf holds (dir UP)
            TableRef look = tile.Shape == TileShape.Ceiling ? _replica.TurfAppearance(tile.X, tile.Y, tile.Z) : tile.Appearance;
            if (look.IsNone || !_replica.TryGetAppearance(look, out AppearanceEntry turf) || turf.OverlayCount == 0)
                continue;
            foreach (TableRef overlay in turf.Overlays)
            {
                if (tile.Shape != TileShape.Wall && IsWallDent(overlay))
                {
                    bool top = tile.Shape == TileShape.Ceiling;
                    if (_replica.TryGetAppearance(overlay, out AppearanceEntry hole) && hole.Dir == (top ? DentOnTop : DentOnFloor)
                        && TrySurfaceDent(batch, tile, overlay, top, out Attachment dent))
                        (made ??= new List<Attachment>()).Add(dent);
                    continue;
                }
                if (tile.Shape is not (TileShape.Wall or TileShape.Floor))
                    continue;
                if (tile.Shape == TileShape.Wall && IsWallDent(overlay))
                {
                    // DV354: a mining crack shows on every open face of the rock at once, a dent only on the face it was shot at
                    bool everyFace = _replica.TryGetAppearance(overlay, out AppearanceEntry d) && IsMiningCrack(d);
                    foreach (byte side in everyFace ? OpenSides(tile.X, tile.Y, tile.Z) : new[] { d.Dir })
                        if (TryDent(batch, tile, overlay, side, out Attachment dent))
                            (made ??= new List<Attachment>()).Add(dent);
                    continue;
                }
                ModelEntry model = AttachModelOf(overlay);
                if (model == null || !_replica.TryGetAppearance(overlay, out AppearanceEntry o))
                    continue;
                Transform3D at = tile.Shape == TileShape.Wall ? OnWall(tile.X, tile.Y, tile.Z, o.Dir) : OnFloor(tile.X, tile.Y, tile.Z, o.Dir);
                if (TryAttach(batch, model, overlay, at, out Attachment attachment))
                    (made ??= new List<Attachment>()).Add(attachment);
                else
                    _turfAttachRetry[key] = (tiles.ToList(), batch); // its textures are still decoding (TryPieces)
            }
        }
        if (made != null)
        {
            _turfAttachments[key] = made;
            _dirty.Add(batch);
        }
    }

    // Once per frame: the turf attachments whose model's textures were still decoding, made again when more have come in.
    private void RetryTurfAttachments()
    {
        int decodes = _models?.DecodesDone ?? 0;
        if (_turfAttachRetry.Count == 0 || decodes == _attachDecodesSeen)
            return;
        _attachDecodesSeen = decodes;
        foreach (var (key, (tiles, batch)) in _turfAttachRetry.ToList())
        {
            _turfAttachRetry.Remove(key);
            AttachTurfs(key, tiles, batch);
        }
    }

    // DV214: a dent as a flat picture on its wall's face (its dir; one against another wall goes to the nearest open face), where tg's offsets put it
    // in the face's picture: its x along the face, its pixel_z up from the face's middle.
    private bool TryDent(ChunkBatch batch, TileCell tile, TableRef overlay, byte dir, out Attachment attachment)
    {
        attachment = null;
        if (_atlas == null || !_atlas.TryPictureFacing(_tables, overlay, ByondDir.South, trim: true, out int id))
            return false;
        Transform3D wall = OnWall(tile.X, tile.Y, tile.Z, dir == 0 ? ByondDirConst.South : dir);
        // OnWall's point is the face's middle at the attach height, its basis turns +Z out of the face
        Vector3 normal = wall.Basis * Vector3.Back;
        Vector3 at = wall.Origin - normal * FloorLift + normal * WallMounts.DecalLift;
        at.Y = TileCenter(tile.X, tile.Y, tile.Z).Y + _atlas.CentreHeightOf(id);
        var instance = new PropInstance
        {
            Transform = new Transform3D(Basis.Identity, at),
            Color = new Color(PieceParts.ModeDecal / 8f, 0, 0, YawTurns(Mathf.Atan2(normal.X, normal.Z))),
            Custom = new Color(id % 2048, id / 2048, 0, 1),
        };
        batch.Quads.Add(instance);
        batch.Dirty = true;
        attachment = new Attachment { Batch = batch };
        attachment.Instances.Add(instance);
        return true;
    }

    // DV577: a shot's dent on its cell's floor (BYOND's DOWN) or on its top (UP: the ceiling, or the floor of the level over it) is a turf overlay
    private const byte DentOnTop = 16, DentOnFloor = 32;

    // DV577: a dent as a flat picture lying on the floor, or under the ceiling, of its cell, where its offsets put it in the tile (its x east,
    // its pixel_z north, as a floor's picture)
    private bool TrySurfaceDent(ChunkBatch batch, TileCell tile, TableRef overlay, bool top, out Attachment attachment)
    {
        attachment = null;
        if (_atlas == null || !_atlas.TryPictureFacing(_tables, overlay, ByondDir.South, trim: false, out int id))
            return false;
        float lift = top ? WorldScale.WallHeight - FloorLift : FloorLift;
        var instance = new PropInstance
        {
            Transform = new Transform3D(Basis.Identity, TileCenter(tile.X, tile.Y, tile.Z)),
            Color = new Color(PieceParts.ModeFloorDecal / 8f, lift * PieceParts.LiftScale, 0, 0),
            Custom = new Color(id % 2048, id / 2048, 0, 1),
        };
        batch.Quads.Add(instance);
        batch.Dirty = true;
        attachment = new Attachment { Batch = batch };
        attachment.Instances.Add(instance);
        return true;
    }

    // A thing's attached overlays (a charge stuck to a window, a firelock, a locker, a vending machine), when the thing is drawn in 3D (a model, or a shaped
    // piece: a door, a window, a grille): on its attach point that faces the planter (AttachPoints); a thing drawn as a picture shows its overlays in it.
    private void AttachToRecord(StaticRecord record)
    {
        if (_models == null || record.Instances.Count == 0 || !_replica.TryGetAppearance(record.Entity.State.Appearance, out AppearanceEntry look) || look.OverlayCount == 0)
            return;
        bool solid = record.Instances.Exists(i => i.Mesh >= 0) || record.Placed.Kind == PlacedKind.Piece && record.Placed.Shape != RenderKind.LightFixture;
        if (!solid)
            return;
        List<Attachment> made = null;
        AttachPoint[] points = null;
        foreach (TableRef overlay in look.Overlays)
        {
            ModelEntry model = AttachModelOf(overlay);
            if (model == null || !_replica.TryGetAppearance(overlay, out AppearanceEntry o))
                continue;
            points ??= AttachPointsOf(record);
            if (AttachPoints.Choose(points, o.Dir) is not AttachPoint point)
                break;
            if (TryAttach(record.Batch, model, overlay, At(point, o.Dir), out Attachment attachment))
                (made ??= new List<Attachment>()).Add(attachment);
        }
        if (made != null)
        {
            _recordAttachments[record] = made;
            _dirty.Add(record.Batch);
        }
    }

    // A thing's attach points in the world: its model's own (the catalog's, its file's Attach_* nodes) where the model stands, else the faces and top of its
    // bounds.
    private AttachPoint[] AttachPointsOf(StaticRecord record)
    {
        if (record.Model != null && _models.AttachPointsOf(record.Model) is { Length: > 0 } own)
        {
            var origin = new System.Numerics.Vector3(record.ModelOrigin.X, record.ModelOrigin.Y, record.ModelOrigin.Z);
            return own.Select(p => AttachPoints.Placed(p, origin, record.ModelYaw)).ToArray();
        }
        if (BoundsOf(record) is not Aabb box)
            return Array.Empty<AttachPoint>();
        float floor = TileCenter(record.Placed.X, record.Placed.Y, record.Placed.Z).Y;
        return AttachPoints.OfBox(N(box.Position), N(box.End), floor + AttachHeight);
    }

    // What a thing occupies in the world: its model parts' meshes where they stand, or a shaped piece's pick boxes (PieceBoxes, as the crosshair finds it).
    private Aabb? BoundsOf(StaticRecord record)
    {
        Aabb? box = null;
        foreach (PropInstance instance in record.Instances)
        {
            if (instance.Mesh < 0)
                continue;
            Aabb part = instance.Transform * ModelMeshAt(instance.Mesh).GetAabb();
            box = box?.Merge(part) ?? part;
        }
        if (box != null || record.Placed.Kind != PlacedKind.Piece)
            return box;
        Placed p = record.Placed;
        Vector3 center = TileCenter(p.X, p.Y, p.Z);
        bool open = PieceOpen(record);
        bool standalone = p.Shape == RenderKind.Grille && !_plan.Index.Windows.Contains((p.X, p.Y, p.Z));
        foreach (PieceBox b in AcrossGlass(p) ? PieceBoxes.AcrossGlass(p, WorldScale.WallHeight, open) : PieceBoxes.For(p, WorldScale.WallHeight, open, standalone))
        {
            // the pick's frame: east, north, up around the tile centre; the world's: east, up, south
            var part = new Aabb(center + new Vector3((float)b.MinEast, (float)b.MinUp, -(float)b.MaxNorth),
                new Vector3((float)(b.MaxEast - b.MinEast), (float)(b.MaxUp - b.MinUp), (float)(b.MaxNorth - b.MinNorth)));
            box = box?.Merge(part) ?? part;
        }
        return box;
    }

    // Where an attached model goes on a point: its back on the thing, its front (+Z) along the point's normal; on a top, lying front up with its top toward the
    // planter's side.
    private static Transform3D At(AttachPoint point, byte dir)
    {
        var at = new Vector3(point.Position.X, point.Position.Y, point.Position.Z);
        var normal = new Vector3(point.Normal.X, point.Normal.Y, point.Normal.Z);
        if (point.IsTop)
        {
            byte toward = WallMounts.IsCardinal(dir) ? dir : ByondDirConst.South;
            // The model's +Z (its front) turned up, then its +Y (its top) turned from north toward the planter.
            var lying = new Basis(Vector3.Up, TileSpace.DirYaw(toward)) * new Basis(Vector3.Right, -Mathf.Pi / 2f);
            return new Transform3D(lying, at + normal * FloorLift);
        }
        return new Transform3D(new Basis(Vector3.Up, Mathf.Atan2(normal.X, normal.Z)), at + normal * FloorLift);
    }

    private static System.Numerics.Vector3 N(Vector3 v) => new(v.X, v.Y, v.Z);

    private void DetachFromRecord(StaticRecord record)
    {
        if (_recordAttachments.Remove(record, out List<Attachment> old))
            Detach(old);
    }

    private void Detach(List<Attachment> attachments)
    {
        foreach (Attachment a in attachments)
        {
            a.Batch.RemoveAll(new HashSet<PropInstance>(a.Instances));
            a.Batch.Dirty = true;
            _dirty.Add(a.Batch);
            _attachedAnimated.Remove(a);
        }
    }

    // The model's pieces at the place: the ones whose material is a frame of the model's sheet show the overlay's own picture and play its animation (the
    // charge's blinking display), the others keep theirs.
    private bool TryAttach(ChunkBatch batch, ModelEntry model, TableRef overlay, Transform3D at, out Attachment attachment)
    {
        attachment = null;
        if (!_models.TryPieces(model, "model", 0xFFFFFFFFu, out ModelPiece[] pieces))
            return false;
        bool hasPicture = _atlas.TryPictureFacing(_tables, overlay, ByondDir.South, trim: false, out int picture);
        attachment = new Attachment { Batch = batch };
        foreach (ModelPiece piece in pieces)
        {
            bool live = piece.Frame != null && hasPicture;
            int id = live ? picture : piece.Picture;
            var instance = new PropInstance
            {
                Transform = at,
                Color = new Color(PieceParts.ModeModel / 8f, 0f, 0f, 0f),
                Custom = new Color(id % 2048, id / 2048, 0, 1),
                Mesh = piece.Mesh,
            };
            batch.AddModel(instance);
            attachment.Instances.Add(instance);
            if (live)
                attachment.Live.Add(instance);
            if (model.FlashMaterial != null && piece.Material.StartsWith(model.FlashMaterial, StringComparison.Ordinal))
                attachment.Flash.Add(instance);
        }
        attachment.FlashFrames = model.FlashFrames;
        batch.Dirty = true;
        if (attachment.Live.Count > 0 && _atlas.TryAnimation(_tables, overlay, ByondDir.South, trim: false, once: false, out attachment.Animation))
        {
            attachment.Start = _clock;
            _attachedAnimated.Add(attachment);
        }
        return true;
    }

    // Once per frame: an attached model's live pieces show its overlay's picture now.
    private void StepAttachments()
    {
        foreach (Attachment a in _attachedAnimated)
        {
            int frame = a.Animation.Timeline.FrameAt(_clock - a.Start);
            int id = a.Animation.Ids[frame];
            if (id == a.Shown)
                continue;
            a.Shown = id;
            foreach (PropInstance part in a.Live)
                a.Batch.SetPieceCustom(part, new Color(id % 2048, id / 2048, part.Custom.B, part.Custom.A));
            // DV64: tg's blink is four display pixels a shade brighter; the flash pieces light up on its bright frame, as a model's light layer does.
            bool lit = Array.IndexOf(a.FlashFrames, frame) >= 0;
            foreach (PropInstance part in a.Flash)
                a.Batch.SetPieceColor(part, new Color(PieceParts.ModeModel / 8f, lit ? PieceParts.Light / 32f : 0f, lit ? 1f : 0f, 0f));
        }
    }

    // On the wall cell's face toward the planter (DV63) among its open sides (a face against another wall is inside the wall), else its south face.
    private Transform3D OnWall(int x, int y, int z, byte dir)
    {
        Vector3 c = TileCenter(x, y, z);
        float half = TileSpace.TileSize / 2f;
        System.Numerics.Vector3 min = N(c - new Vector3(half, 0f, half)), max = N(c + new Vector3(half, WorldScale.WallHeight, half));
        AttachPoint[] faces = AttachPoints.OfBox(min, max, c.Y + AttachHeight, side => OpenSide(x, y, z, side)).Where(p => !p.IsTop).ToArray();
        if (faces.Length == 0)
            faces = AttachPoints.OfBox(min, max, c.Y + AttachHeight, side => side == ByondDirConst.South).Where(p => !p.IsTop).ToArray();
        return At(AttachPoints.Choose(faces, dir).Value, dir);
    }

    // A wall cell's side that is not against another wall (or the map's edge).
    private bool OpenSide(int x, int y, int z, byte side)
    {
        (int dx, int dy) = WallMounts.Step(side);
        return InMap(x + dx, y + dy, z) && !_wall[CellIndex(x + dx, y + dy, z)];
    }

    private IEnumerable<byte> OpenSides(int x, int y, int z)
        => new[] { ByondDirConst.North, ByondDirConst.South, ByondDirConst.East, ByondDirConst.West }.Where(side => OpenSide(x, y, z, side));

    // Lying on the floor in the middle of the tile, its top toward the planter.
    private Transform3D OnFloor(int x, int y, int z, byte dir)
        => At(new AttachPoint(AttachPoints.Top, N(TileCenter(x, y, z)), System.Numerics.Vector3.UnitY), dir);
}
