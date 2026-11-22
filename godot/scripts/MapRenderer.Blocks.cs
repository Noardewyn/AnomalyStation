using System.Linq;
using Godot;
using Anomaly.Client.Render;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Client;

// D152 (block windows, BlockFaces): full-tile windows, grilles and girders drawn as tile-sized blocks, each face an instance
// with tg's picture of that face: the sides the lone pane's (the frame round every block), the top the block's own junction; a grille is its bottom only.
public partial class MapRenderer
{
    // A grille block is only its bottom (the owner, 2026-10-16: the bars lie under the glass, the sides stay clear), lying on the floor like a floor tile, just over
    // the floor layers (FloorLayers: under 5 cm). Under a window it lies on the window's floor: a flat dark steel just under it (WindowFloorLift) that keeps D147's
    // point, the plating's pipes and cables hidden, without D147's raised base, whose sides fought with the grille (the owner's play test).
    private const float GrilleLift = 0.055f, WindowFloorLift = 0.047f;

    // A window block's bottom shows its top's picture lying over the grille (the owner, 2026-10-16: the frame runs round the run's foot on the floor too), its glass left
    // clear so the grille shows through.
    private const float WindowRimLift = 0.065f;

    // How far over the wall height a block's top lies (metres): off the ceiling's plane. Under it, as a wall's top (WorldGeometry.TopSink): over it, a window's top
    // came up through the floor of the level above on a multi-z map (D325).
    private const float TopLift = -WorldGeometry.TopSink;

    // What shows between the bars on the top of a grille with no window on it, 0xRRGGBBAA.
    private const uint GrilleTopColour = 0x4A4A52FFu;

    // The share of a window block's faint glass pixels drawn (prop_atlas.gdshader's v_pane: a stipple of their alpha times this): a light veil, so the pane reads as glass.
    private const float PaneShare = 0.6f;

    // A tinted or frosted window's veil (the owner, 2026-10-16: dark but see-through): its own pane pixels at their whole alpha, about half of them drawn. tg makes the
    // full-tile tinted window opaque (window.dm:840); D152 draws it translucent (DV55).
    // DEVIATION(DV55): tg makes the full-tile tinted window opaque; it is drawn translucent.
    private const float TintedPaneShare = 1f;

    // How far a see-through block's side face stands inside its tile (metres).
    private const float WallInset = 0.006f;

    private static bool IsBlock(in Placed p) => WorldScale.BlockPieces && p.Kind == PlacedKind.Piece && BlockFaces.Applies(p.Shape);

    private int GrilleTopPicture(StaticRecord record)
        => _atlas.TryLibraryPictureOver("icons/obj/structures.dmi", record.SeenState, GrilleTopColour, out int id) ? id : _atlas.Swatch(GrilleTopColour);

    // The faces of a block: one instance per side face not covered by its neighbour, and the top; a window keeps its base (D147). Every face is turned to
    // nothing (the block is built in world directions); the picture is the face's own smoothing state, or the entity's picture when it has none.
    private void BuildBlock(StaticRecord record, Vector3 center, int ownId)
    {
        Placed p = record.Placed;
        if (p.Shape == RenderKind.Grille && _plan.Index.Windows.Contains((p.X, p.Y, p.Z)))
        {
            AddBlockFace(record, Basis.Identity, center + new Vector3(0, GrilleLift, 0), PieceParts.BlockBottom, ownId);
            record.Batch.Dirty = true;
            _dirty.Add(record.Batch);
            return;
        }
        // A grille with no window on it (tg's maintenance grilles, dense: grille.dm:10) is a fence of bars: its sides show tg's grille sprite, the room behind seen
        // between the bars (the owner, 2026-10-17); it falls through to the side faces below, its top tg's bars over a metal cap (the owner, 2026-11-08: without a top it
        // looked hollow, and a plain cap should show the bars).
        byte covered = BlockFaces.Covered(p.X, p.Y, p.Z, p.Shape, record.SeenState ?? StateOf(record.Entity.State.Appearance), IsClosedAt, SameKindAt(p.Shape));
        Basis basis = Basis.Identity;
        // tg's windows smooth only with windows (window.dm:754): a run's end against a wall keeps its frame. A girder smooths with walls too (girders.dm:13).
        Func<int, int, int, bool> sameKind = SameKindAt(p.Shape);
        int joined = p.Shape == RenderKind.FullWindow ? NeighboursOf(p, sameKind) : 15;
        int diagonals = p.Shape == RenderKind.FullWindow ? BlockFaces.DiagonalsOf(p.X, p.Y, p.Z, sameKind) : 0;
        bool window = p.Shape == RenderKind.FullWindow;
        bool up = window && WindowStacked(p.X, p.Y, p.Z), down = window && WindowStacked(p.X, p.Y, p.Z - 1);
        foreach (byte face in BlockFaces.Sides)
        {
            if ((covered & face) != 0)
                continue;
            // a see-through block's side a hair inside its tile: a wall's side and every model fitted to the tile's width (a door's frame, a table's top) lie in the tile's
            // edge plane and flickered through the glass (the owner, 2026-11-17); a false wall stays flush with the walls it passes for (its joints are hidden)
            Vector3 at = center;
            (int dx, int dy) = face switch { ByondDirConst.North => (0, 1), ByondDirConst.South => (0, -1), ByondDirConst.East => (1, 0), _ => (-1, 0) };
            if (p.Shape != RenderKind.FalseWall)
                at -= new Vector3(dx, 0, -dy) * WallInset;
            AddBlockFace(record, basis, at, BlockFaces.PartOf(face), FacePicture(record, face, joined, ownId, window ? FaceStack(p, face, up, down) : 0, diagonals), Pane(record));
        }
        // the top a hair over the ceiling's plane, which it would fight: from below the ceiling hides it, from above it shows; a window joined to the one above has none
        // a step's inner corner: the top (the rim under it) is framed toward a joined neighbour that goes on up (down) a level
        if (!up)
            AddBlockFace(record, basis, center + new Vector3(0, TopLift, 0), PieceParts.BlockTop, p.Shape == RenderKind.Grille ? GrilleTopPicture(record)
                : FacePicture(record, 0, window ? joined & ~StepsOf(p, joined, 0) : joined, ownId), Pane(record));
        if (window && !down)
        {
            // D147's plate hides the plating's pipes and cables: a window standing on open space has none to hide (the owner, 2026-11-10)
            if (KindOf(_replica.TurfTypeIndexAt(p.X, p.Y, p.Z)) != TurfKind.Void)
                AddBlockFace(record, Basis.Identity, center + new Vector3(0, WindowFloorLift, 0), PieceParts.BlockBottom, _atlas.Swatch(WindowBaseColour));
            AddBlockFace(record, Basis.Identity, center + new Vector3(0, WindowRimLift, 0), PieceParts.BlockBottom, FacePicture(record, 0, joined & ~StepsOf(p, joined, -1), ownId));
        }
        record.Batch.Dirty = true;
        _dirty.Add(record.Batch);
    }

    private Func<int, int, int, bool> SameKindAt(RenderKind shape) => shape switch
    {
        RenderKind.FullWindow => (x, y, z) => _plan.Index.Windows.Contains((x, y, z)),
        RenderKind.Grille => (x, y, z) => _plan.Index.Grilles.Contains((x, y, z)),
        _ => (x, y, z) => _plan.Index.Girders.Contains((x, y, z)),
    };

    private static float Pane(StaticRecord record)
        => record.Placed.Shape is not (RenderKind.FullWindow or RenderKind.DirectionalWindow) ? 0f
            : record.Entity.TypePath.Contains("/tinted", StringComparison.Ordinal) ? TintedPaneShare : PaneShare;

    private void AddBlockFace(StaticRecord record, Basis basis, Vector3 center, int part, int pictureId, float pane = 0f)
    {
        var instance = new PropInstance
        {
            Transform = new Transform3D(basis, center),
            Color = new Color(PieceParts.ModePiece / 8f, part / 32f, 0f, pane),
            Custom = new Color(pictureId % 2048, pictureId / 2048, 0, 1),
        };
        record.Instances.Add(instance);
        record.Batch.Pieces.Add(instance);
    }

    // The picture of one face (0: the top): a window's or girder's smoothing state for that face (BlockFaces.FaceState), with a damaged window's cracks over it;
    // a grille, or a state its icon lacks, shows the entity's own picture.
    private static int NeighboursOf(in Placed p, Func<int, int, int, bool> there)
        => (there(p.X, p.Y + 1, p.Z) ? ByondDirConst.North : 0) | (there(p.X, p.Y - 1, p.Z) ? ByondDirConst.South : 0)
            | (there(p.X + 1, p.Y, p.Z) ? ByondDirConst.East : 0) | (there(p.X - 1, p.Y, p.Z) ? ByondDirConst.West : 0);

    // A full-tile window at (x, y, z) joined to a window straight above it: the levels are linked and the upper one stands on open space, no floor or plating between
    // them (the owner, 2026-11-10).
    private bool WindowStacked(int x, int y, int z)
        => z >= 1 && z < _levels && InMap(x, y, z) && _replica.IsLinkedBelow(z + 1) && _plan.Index.Windows.Contains((x, y, z)) && _plan.Index.Windows.Contains((x, y, z + 1))
            && KindOf(_replica.TurfTypeIndexAt(x, y, z + 1)) == TurfKind.Void;

    // The joined cardinal neighbours (BYOND bits) whose window joins the one above it (dz 0) or below it (dz -1): a step up or down there.
    private int StepsOf(in Placed p, int joined, int dz)
    {
        int steps = 0;
        foreach ((int dx, int dy, int bit) in new[] { (0, 1, ByondDirConst.North), (0, -1, ByondDirConst.South), (1, 0, ByondDirConst.East), (-1, 0, ByondDirConst.West) })
        {
            if ((joined & bit) != 0 && WindowStacked(p.X + dx, p.Y + dy, p.Z + dz))
                steps |= bit;
        }
        return steps;
    }

    // A window side face's stack bits (BlockFaces.Stacked): its own joins up and down, and its neighbours' along the row for the corners.
    private int FaceStack(in Placed p, byte face, bool up, bool down)
    {
        // a step's inner corner (the owner, 2026-11-10): a window before the face a level down (up) covers that joined face, so this face's bottom (top) edge is framed
        (int fx, int fy) = BlockFaces.FrontOf(face);
        down &= !_plan.Index.Windows.Contains((p.X + fx, p.Y + fy, p.Z - 1));
        up &= !_plan.Index.Windows.Contains((p.X + fx, p.Y + fy, p.Z + 1));
        if (!up && !down)
            return 0;
        (int lx, int ly) = BlockFaces.LeftOf(face);
        int stack = (up ? Smoothing.Above : 0) | (down ? Smoothing.Below : 0);
        if (up && WindowStacked(p.X + lx, p.Y + ly, p.Z)) stack |= BlockFaces.UpLeft;
        if (up && WindowStacked(p.X - lx, p.Y - ly, p.Z)) stack |= BlockFaces.UpRight;
        if (down && WindowStacked(p.X + lx, p.Y + ly, p.Z - 1)) stack |= BlockFaces.DownLeft;
        if (down && WindowStacked(p.X - lx, p.Y - ly, p.Z - 1)) stack |= BlockFaces.DownRight;
        return stack;
    }

    // The full-tile windows whose stacking a change at (x, y, z) may change, with their neighbours along the row (the corners): those on the levels from z + fromDz to z + toDz.
    private void RestackWindows(int x, int y, int z, int fromDz = -1, int toDz = 0)
    {
        if (!WorldScale.BlockPieces || _byCell == null)
            return;
        for (int dz = fromDz; dz <= toDz; dz++)
        {
            for (int n = -1; n < Neighbours.Length; n++)
            {
                (int dx, int dy) = n < 0 ? (0, 0) : Neighbours[n];
                if (!_byCell.TryGetValue((x + dx, y + dy, z + dz), out List<StaticRecord> there))
                    continue;
                foreach (StaticRecord other in there.ToArray())
                {
                    if (other.Placed.Kind == PlacedKind.Piece && other.Placed.Shape == RenderKind.FullWindow)
                        Replace(other, rebuildAlways: true);
                }
            }
        }
    }

    private int FacePicture(StaticRecord record, byte face, int joined, int ownId, int stack = 0, int diagonals = 0)
    {
        // a false wall's sides are framed per face, as a wall's are (BlockFaces.FaceState; the owner, 2026-11-10), its top the junction picture
        if (record.Placed.Shape == RenderKind.Grille || !_tables.TryGetAppearance(record.Entity.State.Appearance, out AppearanceEntry entry))
            return ownId;
        string state = BlockFaces.FaceState(_tables.ResolveString(entry.IconState), face, joined, stack, diagonals);
        string icon = _tables.ResolveResource(entry.Icon);
        if (state == null || icon == null)
            return ownId;
        if (record.Placed.Shape == RenderKind.FullWindow
            && WindowCracks.TryFind(_tables, Look(record), out string crackIcon, out string crackState)
            && _atlas.TryLibraryPictureWithOverlay(icon, state, crackIcon, crackState, trim: false, out int cracked))
            return cracked;
        return _atlas.TryLibraryPicture(icon, state, trim: false, out int id) ? id : ownId;
    }

    // The share of the pod door slab's thickness a curtain keeps (a 24 cm slab becomes a 3 cm sheet).
    private const float CurtainThin = 0.125f;

    // How far out of the glass a door that shares a window block's cell stands (metres past the block's face, besides its own half depth).
    private const float DoorGap = 0.01f;

    // The share of its thickness a door piece keeps out of the glass (a pod door's 24 cm slab is a 6 cm sheet on each face).
    private const float DoorThin = 0.25f;

    // A door, shutters or a firelock that shares its cell with a window block (tg's /preopen shutters behind the R&D and bridge windows) would stand inside the glass:
    // it is drawn on both faces of the block across its plane instead, a hair outside the glass (the owner, 2026-10-16), each copy sliding as the door does.
    private void SplitAcrossGlass(StaticRecord record)
    {
        Placed p = record.Placed;
        if (!AcrossGlass(p))
            return;
        Vector3 cell = TileCenter(p.X, p.Y, p.Z);
        foreach (PropInstance instance in record.Instances.ToArray())
        {
            Basis basis = instance.Transform.Basis;
            Vector3 normal = (basis * Vector3.Back).Normalized();
            float half;
            if (instance.Mesh >= 0)
            {
                Aabb box = ModelMeshAt(instance.Mesh).GetAabb();
                half = Mathf.Max(Mathf.Abs(box.Position.Z), Mathf.Abs(box.End.Z));
            }
            else
            {
                basis = basis * Basis.FromScale(new Vector3(1f, 1f, DoorThin));
                half = (WorldGeometry.FirelockOffset + WorldGeometry.FirelockThickness) * DoorThin;
            }
            // the plane through the cell's centre, then out to either face of the glass
            Vector3 origin = instance.Transform.Origin - normal * (instance.Transform.Origin - cell).Dot(normal);
            float reach = TileSpace.TileSize / 2f + half + DoorGap;
            instance.Transform = new Transform3D(basis, origin + normal * reach);
            var copy = new PropInstance { Transform = new Transform3D(basis, origin - normal * reach), Color = instance.Color, Custom = instance.Custom, Mesh = instance.Mesh, Wall = instance.Wall };
            record.Instances.Add(copy);
            if (copy.Mesh >= 0)
                record.Batch.AddModel(copy);
            else
                record.Batch.Pieces.Add(copy);
        }
        record.Batch.Dirty = true;
        _dirty.Add(record.Batch);
    }

    // An undirected sign in a window block's cell (tg's electric-shock signs on the hull windows) shows on both faces of the glass, as tg's sprite over the window tile is seen
    // from either side: the second face is the opposite of the one the planner chose, when that side is not a wall.
    private bool TryOtherGlassFace(in Placed p, out Placed other)
    {
        other = p;
        if (!WorldScale.BlockPieces || !p.InWall || !_plan.Index.Windows.Contains((p.X, p.Y, p.Z)))
            return false;
        byte back = p.Face switch { ByondDirConst.North => ByondDirConst.South, ByondDirConst.South => ByondDirConst.North, ByondDirConst.East => ByondDirConst.West, _ => ByondDirConst.East };
        (int dx, int dy) = back switch { ByondDirConst.North => (0, 1), ByondDirConst.South => (0, -1), ByondDirConst.East => (1, 0), _ => (-1, 0) };
        if (Probe.IsWall(p.X + dx, p.Y + dy, p.Z))
            return false;
        other = p with { Face = back };
        return true;
    }

    // A directional window's pane in D152 (block windows): its counterpart's straight-run picture (BorderCounterparts) turned into the junction of the panes
    // it runs on (BlockFaces.BorderWindowJunction), so a run of panes shows one frame round it; the glass draws as the blocks' veil.
    private static float BorderPane(StaticRecord record) => WorldScale.BlockPieces ? Pane(record) : 0f;

    private int BorderWindowPicture(StaticRecord record, int straightId)
    {
        Placed p = record.Placed;
        if (!_borderPictures.TryGetValue((p.Shape, record.Entity.TypePath, string.Empty), out (string Icon, string State)? picture) || picture == null)
            return straightId;
        int junction = BlockFaces.BorderWindowJunction(p.X, p.Y, p.Z, p.Face, (x, y, z, face) => _plan.Index.BorderWindows.Contains((x, y, z, face)));
        string straight = picture.Value.State;
        int dash = straight.LastIndexOf('-');
        string state = dash > 0 && WindowJunction.TryParse(straight, out _) ? Smoothing.StateOf(straight.Substring(0, dash), junction) : null;
        return state != null && _atlas.TryLibraryPicture(picture.Value.Icon, state, trim: false, out int id) ? id : straightId;
    }

    // A pane built or taken away changes the frames of the panes it runs on: the directional windows on the tiles around it are drawn again.
    private void RedrawBorderWindowsAround(int x, int y, int z, long except)
    {
        if (!WorldScale.BlockPieces)
            return;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (!_byCell.TryGetValue((x + dx, y + dy, z), out List<StaticRecord> there))
                    continue;
                foreach (StaticRecord other in there.ToArray())
                {
                    if (other.Entity.Id != except && other.Placed.Kind == PlacedKind.Piece && other.Placed.Shape == RenderKind.DirectionalWindow)
                        BuildInstances(other);
                }
            }
        }
    }

    private void ForgetBorderWindow(Placed p)
    {
        var key = (p.X, p.Y, p.Z, p.Face);
        bool another = _byCell.TryGetValue((p.X, p.Y, p.Z), out List<StaticRecord> here)
            && here.Any(r => r.Placed.Kind == PlacedKind.Piece && r.Placed.Shape == RenderKind.DirectionalWindow && r.Placed.Face == p.Face);
        if (!another)
            _plan.Index.BorderWindows.Remove(key);
        RedrawBorderWindowsAround(p.X, p.Y, p.Z, except: p.EntityId);
    }

    // A door, shutters or a firelock drawn on both faces of a window block (SplitAcrossGlass).
    private bool AcrossGlass(in Placed p)
        => WorldScale.BlockPieces && p.Kind == PlacedKind.Piece && IsDoorShape(p.Shape) && _plan.Index.Windows.Contains((p.X, p.Y, p.Z));

    // A firelock's model stands at its tile's edge, flush with the walls' faces (models.json: offset 0.87, the owner 2026-10-19), so it showed on one side of the doorway
    // only; the owner (2026-10-16) wants it on both: the model again, turned half round about the tile's centre, which puts it as far behind. Each copy slides as the
    // firelock does (its instances are the record's). Not for a firelock across a window block's glass, which SplitAcrossGlass already doubles.
    private void MirrorFirelock(StaticRecord record)
    {
        Placed p = record.Placed;
        if (p.Kind != PlacedKind.Piece || p.Shape != RenderKind.Firelock || record.Model == null || AcrossGlass(p))
            return;
        Vector3 cell = TileCenter(p.X, p.Y, p.Z);
        var half = new Basis(Vector3.Up, Mathf.Pi);
        foreach (PropInstance instance in record.Instances.ToArray())
        {
            if (instance.Mesh < 0)
                continue;
            var copy = new PropInstance
            {
                Transform = new Transform3D(half * instance.Transform.Basis, cell + half * (instance.Transform.Origin - cell)),
                Color = instance.Color,
                Custom = instance.Custom,
                Mesh = instance.Mesh,
                Wall = instance.Wall,
            };
            record.Instances.Add(copy);
            record.Batch.AddModel(copy);
        }
        record.Batch.Dirty = true;
        _dirty.Add(record.Batch);
    }

    // A directional window's or a windoor's end caps: both drawn, but in D152 (block windows) not at an end where the pane runs on into the next one (its
    // junction's side bit), whose cap would show through the glass as a line across the run.
    private int PaneCaps(int part, in Placed p)
    {
        if (part is not (PieceParts.DirectionalWindow or PieceParts.Windoor))
            return 0;
        const int both = WorldGeometry.PaneCapLow | WorldGeometry.PaneCapHigh;
        if (!WorldScale.BlockPieces)
            return both;
        int junction = BlockFaces.BorderWindowJunction(p.X, p.Y, p.Z, p.Face, (x, y, z, face) => _plan.Index.BorderWindows.Contains((x, y, z, face)));
        return both & ~((junction & Smoothing.West) != 0 ? WorldGeometry.PaneCapLow : 0) & ~((junction & Smoothing.East) != 0 ? WorldGeometry.PaneCapHigh : 0);
    }

    // A wall decal's placement with a split picture's run mirrored (SplitPictures): on a face that looks north, a part of a picture tg splits over neighbouring
    // tiles moves to its mirror's place in the run, so the halves of a station map meet in tg's order.
    private Placed DecalPlaced(StaticRecord record)
    {
        Placed p = record.Placed;
        // the picture looks north: a mount on the wall south of its tile (Face: the way to the wall), or one in the wall cell whose open side is north
        bool looksNorth = p.InWall ? p.Face == ByondDirConst.North : p.Face == ByondDirConst.South;
        // a mount a model draws is not a part of a split picture: its 2D frame's edges joined a fire alarm and the extinguisher cabinet beside it into a run
        // and moved the alarm's pick box onto the cabinet (the owner's report, 2026-10-20)
        if (p.Kind != PlacedKind.WallDecal || p.Fin || !looksNorth || _atlas == null || record.Model != null)
            return p;
        int shift = SplitPictures.MirrorShift(p.X, x => DecalEdgesAt(x, p));
        return shift == 0 ? p : p with { OffsetX = p.OffsetX + shift * TileSpace.TileSize };
    }

    // The frame edges the picture of the wall decal at column x reaches, on the same row, level and face as p (null: none there).
    private (bool West, bool East)? DecalEdgesAt(int x, Placed p)
    {
        if (!_byCell.TryGetValue((x, p.Y, p.Z), out List<StaticRecord> there))
            return null;
        foreach (StaticRecord other in there)
        {
            Placed o = other.Placed;
            if (o.Kind != PlacedKind.WallDecal || o.Fin || o.Face != p.Face || o.InWall != p.InWall || other.Model != null)
                continue;
            int id = other.PictureId;
            if (id < 0 && !_atlas.TryPicture(_tables, Look(other), wantFrames: false, trim: true, out id, out _))
                continue;
            return _atlas.FrameEdgesOf(id);
        }
        return null;
    }

    private bool _rejoiningSplit;

    // A part of a split picture built after its neighbour: the neighbour's run could not see it, so the decals beside it on the same wall face are placed again
    // (once: their own rebuild does not come back here).
    private void RejoinSplitNeighbours(StaticRecord record)
    {
        if (_rejoiningSplit || _atlas == null || record.PictureId < 0)
            return;
        (bool west, bool east) = _atlas.FrameEdgesOf(record.PictureId);
        if (!west && !east)
            return;
        Placed p = record.Placed;
        _rejoiningSplit = true;
        try
        {
            foreach (int dx in new[] { -1, 1 })
            {
                if (dx < 0 ? !west : !east)
                    continue;
                if (!_byCell.TryGetValue((p.X + dx, p.Y, p.Z), out List<StaticRecord> there))
                    continue;
                foreach (StaticRecord other in there.ToArray())
                {
                    if (other.Placed.Kind == PlacedKind.WallDecal && !other.Placed.Fin && other.Placed.Face == p.Face && other.Placed.InWall == p.InWall)
                        BuildInstances(other);
                }
            }
        }
        finally
        {
            _rejoiningSplit = false;
        }
    }
}
