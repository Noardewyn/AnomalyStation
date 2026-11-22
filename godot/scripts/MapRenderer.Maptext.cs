using System.Linq;
using Godot;
using Anomaly.Client.Render;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Maptext;
using Anomaly.Client.Shared.Models;
using Anomaly.Client.Shared.World;
using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Client;

// Gaps W1 (protocol version 74): the maptext of the things the map renderer draws (Render/WorldMaptext.cs). A model's texts lie on its front face, the source
// frame's pixels mapped as its sprite is (ModelMesh.SpriteUv: the frame's width across, its height up); a billboard's stand in its picture's plane and turn
// with it. Built with the thing's pieces (BuildInstances) and gone with them (ClearInstances); a text whose maptext_x animates scrolls each frame.
public partial class MapRenderer
{
    private const float TextLift = 0.004f; // metres in front of the face or the picture

    private readonly Dictionary<StaticRecord, List<WorldTextQuad>> _texts = new();
    private readonly HashSet<StaticRecord> _textsScrolling = new();
    private readonly MaptextCollector _textCollector = new();
    private ReplicaWorld _textReplica;
    private WorldMaptext _worldMaptext;

    /// <summary>The world maptext quads shown (a test's count).</summary>
    public int WorldTextCount => _texts.Values.Sum(q => q.Count);

    // The texts are the renderer's own children, not their chunk's: each shows while its chunk does (SetSight: the third person's sight, the levels above
    // hidden, the other stacks), so an elevator lantern's floor number on a hidden deck does not hang in the air (the owner, 2026-11-13).
    private void SyncMaptextSight()
    {
        foreach ((StaticRecord record, List<WorldTextQuad> quads) in _texts)
        {
            bool show = record.Batch?.Root.Visible ?? true;
            foreach (WorldTextQuad q in quads)
                if (q.Node.Visible != show)
                    q.Node.Visible = show;
        }
    }

    private void ClearMaptext(StaticRecord record)
    {
        _textsScrolling.Remove(record);
        if (_texts.Remove(record, out List<WorldTextQuad> quads))
            _worldMaptext.Release(quads);
    }

    private void AddMaptext(StaticRecord record)
    {
        if (_replica == null || _textures == null || record.Instances.Count == 0)
            return;
        if (!ReferenceEquals(_textReplica, _replica))
        {
            _textReplica = _replica;
            _textCollector.Clear();
        }
        // the raw tables: an animation track names the text's entry as the wire has it
        MaptextItem[] items = _textCollector.Collect(_textures.TablesOf(_replica), record.Entity.State.Appearance, rootOffset: false);
        if (items.Length == 0)
            return;
        Placed p = record.Placed;
        bool billboard = record.Model == null && p.Kind is PlacedKind.Billboard or PlacedKind.Item or PlacedKind.Resting;
        bool decal = record.Model == null && p.Kind == PlacedKind.WallDecal && !p.Fin;
        if (record.Model == null && !billboard && !decal)
            return; // shortcut: fins, floor decals and pieces draw no maptext yet
        if (_worldMaptext == null)
        {
            _worldMaptext = new WorldMaptext(_textures.Library);
            AddChild(_worldMaptext);
        }
        List<WorldTextQuad> quads = _worldMaptext.Make(items, billboard);
        if (quads.Count == 0)
            return;
        if (record.Model != null)
            PlaceOnModel(record, quads);
        else if (!(decal ? PlaceOnWallDecal(record, quads) : PlaceOnBillboard(record, quads)))
        {
            _worldMaptext.Release(quads);
            return;
        }
        foreach (WorldTextQuad q in quads)
        {
            q.Node.Visible = record.Batch?.Root.Visible ?? true; // its chunk's sight (SyncMaptextSight)
            AddChild(q.Node);
        }
        _texts[record] = quads;
        if (record.Entity.State.Animation != null)
            _textsScrolling.Add(record);
    }

    // On the model's front face: its base sprite's opaque bounds stretched over the model's front rectangle (WorldMaptextPlacement.FaceMapping); without
    // them, the source frame's 32 pixels across its width, centred, and up its height from the bottom (ModelMesh.SpriteUv's frame).
    private void PlaceOnModel(StaticRecord record, List<WorldTextQuad> quads)
    {
        ModelEntry model = record.Model;
        (float sx, float sy) = ModelMesh.Scale(model, TileSpace.TileSize, WorldScale.WallHeight, WorldScale.TableTop);
        float perX = model.SourceWidth / ModelMesh.SpritePixels * sx, perY = model.SourceHeight / ModelMesh.SpritePixels * sy;
        float offX = -ModelMesh.SpritePixels / 2f * perX, offY = 0f;
        if (_replica.TryGetAppearance(record.Entity.State.Appearance, out AppearanceEntry root)
            && _worldMaptext.OpaqueBoundsOf(_replica.ResolveResource(root.Icon), _replica.ResolveString(root.IconState)) is MaskBounds sprite
            && _models.TryFaceOf(model, out var face))
            (offX, offY, perX, perY) = WorldMaptextPlacement.FaceMapping(sprite, face.MinX, face.MinY, face.MaxX, face.MaxY);
        float front = _models.FrontOf(model) + TextLift;
        var placed = new Transform3D(new Basis(Vector3.Up, record.ModelYaw), record.ModelOrigin + record.CarryOffset);
        foreach (WorldTextQuad q in quads)
        {
            WorldTextRegion r = q.Region;
            q.Quad.Size = new Vector2(r.Width * perX, r.Height * perY);
            q.Node.Transform = placed * new Transform3D(Basis.Identity, new Vector3(offX + r.CenterX * perX, offY + r.CenterY * perY, front));
        }
    }

    // In the billboard's picture (MapRenderer's quad of the thing: its picture's rectangle about its feet, as it is picked), turned to the camera with it.
    private bool PlaceOnBillboard(StaticRecord record, List<WorldTextQuad> quads)
    {
        Placed p = record.Placed;
        if (!_textures.TryGet(_tables, Look(record), ByondDir.South, 0, out ComposedRaster raster, out _))
            return false;
        Raster image = raster.Image;
        SpriteRect rect = SpriteRect.Of(image.Width, image.Height, raster.OriginX, raster.OriginY);
        if (p.Kind is PlacedKind.Item or PlacedKind.Resting)
        {
            AtlasBuilder.TrimBounds(image, out _, out int trimY, out _, out int trimHeight);
            rect = rect.AsItem(p.Kind == PlacedKind.Item ? WorldScale.ItemScale : 1.0, trimHeight > 0 ? image.Height - (trimY + trimHeight) : 0);
        }
        float pixel = (float)rect.PixelSize;
        float iconLeft = (float)rect.Left + raster.OriginX * pixel, iconBottom = (float)rect.Bottom + raster.OriginY * pixel;
        Vector3 origin = record.Instances[0].Transform.Origin;
        foreach (WorldTextQuad q in quads)
        {
            WorldTextRegion r = q.Region;
            q.Quad.Size = new Vector2(r.Width * pixel, r.Height * pixel);
            q.Quad.CenterOffset = new Vector3(iconLeft + r.CenterX * pixel, iconBottom + r.CenterY * pixel, TextLift);
            q.Node.Position = origin;
        }
        return true;
    }

    // On a wall decal's picture (tg's elevator indicator, its floor number): the frame's pixels as prop_atlas.gdshader lays the decal's quad, its drawn
    // pixels' middle on the placement's origin (WallMounts.DecalPlacement), the picture's front its +Z. shortcut: only the front of a decal on glass
    // (TryOtherGlassFace) shows the text, upgrade when a glass-mounted thing with maptext appears.
    private bool PlaceOnWallDecal(StaticRecord record, List<WorldTextQuad> quads)
    {
        if (record.PictureId < 0 || !_textures.TryGet(_tables, Look(record), ByondDir.South, 0, out ComposedRaster raster, out _))
            return false;
        SpriteRect rect = SpriteRect.Of(raster.Image.Width, raster.Image.Height, raster.OriginX, raster.OriginY);
        float pixel = (float)rect.PixelSize;
        float iconLeft = (float)rect.Left + raster.OriginX * pixel;
        float iconBottom = (float)rect.Bottom + raster.OriginY * pixel - _atlas.CentreHeightOf(record.PictureId);
        (System.Numerics.Vector3 at, float yaw) = WallMounts.DecalPlacement(DecalPlaced(record), _atlas.QuadHeightOf(record.PictureId));
        var placed = new Transform3D(new Basis(Vector3.Up, yaw), new Vector3(at.X, at.Y, at.Z));
        foreach (WorldTextQuad q in quads)
        {
            WorldTextRegion r = q.Region;
            q.Quad.Size = new Vector2(r.Width * pixel, r.Height * pixel);
            q.Node.Transform = placed * new Transform3D(Basis.Identity, new Vector3(iconLeft + r.CenterX * pixel, iconBottom + r.CenterY * pixel, TextLift));
        }
        return true;
    }

    // Once per frame: the texts whose maptext_x animates (tg's status display's long lines).
    private void StepMaptext()
    {
        if (_textsScrolling.Count == 0)
            return;
        double tick = double.IsNaN(RenderTick) ? _replica.Tick : RenderTick;
        List<StaticRecord> done = null;
        foreach (StaticRecord record in _textsScrolling)
        {
            bool playing = false;
            foreach (WorldTextQuad q in _texts[record])
                playing |= q.Step(record.Entity.State.Animation, tick);
            if (!playing && (record.Entity.State.Animation == null || record.Entity.State.Animation.EndTick(MotionMath.TicksPerSecond) <= tick))
                (done ??= new List<StaticRecord>()).Add(record);
        }
        if (done != null)
            foreach (StaticRecord record in done)
                _textsScrolling.Remove(record);
    }
}
