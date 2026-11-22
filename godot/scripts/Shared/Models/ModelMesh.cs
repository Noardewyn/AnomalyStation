using System.Linq;
using System.IO;
using System.Globalization;
using System.Numerics;

namespace Anomaly.Client.Shared.Models;

/// <summary>
/// A model part's mesh as the renderer draws it: positions and normals in the world's metres about the tile's centre on the floor, uv over the part's picture (0..1,
/// v UP, as the prop shader reads every piece), UV2 (the part's slide along its x and up when fully open, in world metres; a glass face's y has
/// <see cref="GlassMark"/> added), indices (Godot's front faces).
/// </summary>
public sealed class ModelMeshData
{
    /// <summary>
    /// A glass face's UV2.y has this added k times (k 1 .. 101, far below any slide, which is within a tile): the shader stipples it by the picture's alpha times
    /// 1 - (k - 1) / 100, the glass's own alpha (a table's 0.42; 1 for a door, whose picture's alpha is tg's glass overlay).
    /// </summary>
    public const float GlassMark = -16f;

    /// <summary>True when a UV2 marks a glass face.</summary>
    public static bool IsGlass(Vector2 uv2) => uv2.Y < GlassMark / 2f;

    private static int GlassSteps(Vector2 uv2) => IsGlass(uv2) ? (int)MathF.Floor((-uv2.Y - GlassMark / 2f) / -GlassMark) : 0;

    /// <summary>The slide a UV2 holds (without the glass mark).</summary>
    public static Vector2 SlideOf(Vector2 uv2) => new(uv2.X, uv2.Y - GlassSteps(uv2) * GlassMark);

    /// <summary>A glass face's own alpha (1 for a face that is not glass).</summary>
    public static float GlassAlphaOf(Vector2 uv2) => IsGlass(uv2) ? 1f - (GlassSteps(uv2) - 1) / 100f : 1f;

    /// <summary>UV2.y of a face: its slide up, and the glass mark with its alpha.</summary>
    public static float Uv2Y(float slideY, bool glass, float alpha)
        => slideY + (glass ? GlassMark * (1 + (int)MathF.Round(100f * (1f - Math.Clamp(alpha, 0f, 1f)))) : 0f);

    public Vector3[] Positions = Array.Empty<Vector3>();
    public Vector3[] Normals = Array.Empty<Vector3>();
    public Vector2[] Uvs = Array.Empty<Vector2>();
    public Vector2[] Uv2 = Array.Empty<Vector2>();
    public int[] Indices = Array.Empty<int>();

    /// <summary>The image (in the file's images) the part's own texture is, for <see cref="ModelTexture.Model"/>; -1: none.</summary>
    public int Image = -1;

    /// <summary>A part built about its node's origin (<see cref="ModelPart.Pivot"/>): where that origin is in the fitted model (a door's hinge); zero otherwise.</summary>
    public Vector3 Pivot;

    /// <summary>The pick boxes (min, max) of a model with <see cref="ModelEntry.PickApart"/>, in the mesh's frame: the rest's, then each such node's; null: the mesh's bounds.</summary>
    public List<(Vector3 Min, Vector3 Max)> PickBoxes;

    /// <summary>Triangles.</summary>
    public int Triangles => Indices.Length / 3;
}

/// <summary>Builds a model part's mesh from its .glb (<see cref="GlbFile"/>) by its catalog entry (<see cref="ModelEntry"/>).</summary>
public static class ModelMesh
{
    /// <summary>
    /// The normals of a flat-shaded mesh (its corners split per face) made smooth: each vertex takes the mean of the normals of every vertex at its place (a
    /// hundredth of a millimetre), one per face meeting there. An outline's inverted hull pushes its vertices along these, so the faces stay joined (along the
    /// faces' own normals they part into loose plates).
    /// </summary>
    public static Vector3[] SmoothNormals(IReadOnlyList<Vector3> positions, IReadOnlyList<Vector3> normals)
    {
        var sums = new Dictionary<(long, long, long), Vector3>();
        (long, long, long) Key(Vector3 p) => ((long)MathF.Round(p.X * 1e5f), (long)MathF.Round(p.Y * 1e5f), (long)MathF.Round(p.Z * 1e5f));
        for (int i = 0; i < positions.Count; i++)
            sums[Key(positions[i])] = sums.GetValueOrDefault(Key(positions[i])) + normals[i];
        var smooth = new Vector3[positions.Count];
        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 sum = sums[Key(positions[i])];
            smooth[i] = sum.LengthSquared() > 1e-12f ? Vector3.Normalize(sum) : normals[i];
        }
        return smooth;
    }

    /// <summary>The source width in sprite pixels: tg's icons are 32 pixels wide, so a source frame of 2 m has a pixel of 1/16 m.</summary>
    public const int SpritePixels = 32;

    /// <summary>The scale from the source frame to the world: x and z, then y.</summary>
    public static (float Horizontal, float Vertical) Scale(ModelEntry entry, float tile, float wallHeight, float tableTop)
    {
        float height = entry.FitHeight switch
        {
            "wall" => wallHeight,
            "table" => tableTop,
            _ => float.Parse(entry.FitHeight, NumberStyles.Float, CultureInfo.InvariantCulture),
        };
        float vertical = height / entry.SourceHeight;
        float horizontal = entry.FitWidth is "tile" or "row" ? tile / entry.SourceWidth : vertical;
        return (horizontal * entry.Scale, vertical * entry.Scale);
    }

    /// <summary>
    /// The sprite uv of a source point on a face with <paramref name="normal"/>: the pixel of the front view of the source frame it stands on, taken a quarter of a
    /// pixel inside the face (a side face takes the pixel at the edge of the thing, not the empty one beyond it). Not half: a face on a half pixel (a hatch's
    /// teeth) then sat on the line between two pixels and showed them both, speckled (the owner, 2026-10-27).
    /// </summary>
    public static Vector2 SpriteUv(Vector3 position, Vector3 normal, float width, float height)
    {
        float pixel = width / SpritePixels;
        Vector3 inside = position - normal * (pixel / 4f);
        return new Vector2(Math.Clamp((inside.X + width / 2f) / width, 0f, 1f), Math.Clamp(inside.Y / height, 0f, 1f));
    }

    /// <summary>The mesh of <paramref name="part"/> (its node, or every mesh node of the file) fitted to the world.</summary>
    /// <summary>
    /// The mesh of <paramref name="part"/> (its node, or every mesh node of the file) fitted to the world; with <paramref name="onlyMaterial"/> (a material index), only
    /// that material's faces (a model with several textures or plain colours is drawn as one piece per material, <see cref="MaterialsOf"/>). <paramref name="frame"/>
    /// maps the uv into that frame of the sheet, 0 to 1 across it (a live frame, <see cref="ModelFrame"/>: drawn with the thing's own picture).
    /// </summary>
    public static ModelMeshData Build(GlbFile file, ModelEntry entry, ModelPart part, float tile, float wallHeight, float tableTop, int onlyMaterial = -1,
        ModelFrame frame = null, int junction = -1, Func<int, Vector4?> topCell = null)
    {
        (float sx, float sy) = Scale(entry, tile, wallHeight, tableTop);
        // "row": only its width is stretched over the tile (a sofa module joins the next one's); its depth keeps the height's scale
        float sz = entry.FitWidth == "row" ? sy : sx;
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var cellUvs = new List<Vector2>();
        var glass = new List<bool>();
        var glassAlpha = new List<float>();
        Vector3 pivot = Vector3.Zero;
        var indices = new List<int>();
        // ModelEntry.PickApart: where each node's vertices start, and its own box (-1: the rest's box)
        var nodeRanges = new List<(int Box, int Start)>();
        int image = -1;
        bool found = false;
        foreach (GlbMeshNode node in file.MeshNodes)
        {
            if (!Takes(part, node.Name))
                continue;
            found = true;
            if (entry.PickApart != null)
            {
                int apart = Array.FindIndex(entry.PickApart, prefix => node.Name.StartsWith(prefix, StringComparison.Ordinal));
                nodeRanges.Add((apart < 0 ? -1 : nodeRanges.Count, positions.Count));
            }
            Matrix4x4 world = part.NodeScale is Vector3 scale ? Matrix4x4.CreateScale(scale / node.Scale) * node.World : node.World;
            if (part.Pivot)
                pivot = (part.PivotAt ?? world.Translation) + part.Offset;
            foreach (GlbPrimitive p in node.Primitives)
            {
                GlbMaterial material = p.Material >= 0 && p.Material < file.Materials.Count ? file.Materials[p.Material] : null;
                if (part.Material != null && (material == null || !material.Name.StartsWith(part.Material, StringComparison.Ordinal)))
                    continue;
                if (onlyMaterial >= 0 && p.Material != onlyMaterial)
                    continue;
                if (image < 0 && material != null)
                    image = material.Image;
                // KHR_texture_transform: uv local to a tile of the image, which the material's transform selects.
                Vector2[] primitiveUvs = material == null || material.UvScale == Vector2.One && material.UvOffset == Vector2.Zero
                    ? p.Uvs
                    : p.Uvs.Select(uv => uv * material.UvScale + material.UvOffset).ToArray();
                if (frame != null && material != null)
                {
                    (int sheetWidth, int sheetHeight) = SheetSize(file, material);
                    primitiveUvs = primitiveUvs.Select(uv => frame.Local(uv, sheetWidth, sheetHeight)).ToArray();
                }
                if (entry.Texture == ModelTexture.Cell)
                    cellUvs.AddRange(CellUvs(primitiveUvs, SheetSize(file, material), entry.CellWidth, entry.CellHeight));
                int start = positions.Count;
                for (int i = 0; i < p.Positions.Length; i++)
                {
                    positions.Add(Vector3.Transform(p.Positions[i], world) + part.Offset);
                    Vector3 n = Vector3.TransformNormal(p.Normals[i], world);
                    normals.Add(n.LengthSquared() > 0f ? Vector3.Normalize(n) : Vector3.UnitY);
                    // glTF's v runs down the image; the shader's runs up.
                    uvs.Add(new Vector2(primitiveUvs[i].X, 1f - primitiveUvs[i].Y));
                    glass.Add(material?.Blend == true);
                    // A model's own glass keeps its material's alpha (a glass table's 0.42); a door's glass takes tg's glass overlay from its picture.
                    glassAlpha.Add(entry.Texture == ModelTexture.Sprite || material == null ? 1f : material.BaseColor.W);
                }
                // glTF's front faces wind counter-clockwise, Godot's clockwise.
                for (int t = 0; t + 2 < p.Indices.Length; t += 3)
                {
                    indices.Add(start + p.Indices[t]);
                    indices.Add(start + p.Indices[t + 2]);
                    indices.Add(start + p.Indices[t + 1]);
                }
            }
        }
        if (!found)
            throw new InvalidDataException($"model {entry.Id}: {part.File} has no mesh node '{part.Node}'");
        if (positions.Count == 0)
            throw new InvalidDataException($"model {entry.Id}: {part.File} has no faces of the material '{part.Material}'");
        var data = new ModelMeshData
        {
            Positions = new Vector3[positions.Count],
            Normals = normals.ToArray(),
            Uvs = new Vector2[positions.Count],
            Uv2 = new Vector2[positions.Count],
            Indices = indices.ToArray(),
            Image = entry.Texture == ModelTexture.Model ? image : -1,
            Pivot = new Vector3(pivot.X * sx, pivot.Y * sy, pivot.Z * sz),
        };
        Vector3[] rigidCentre = entry.RigidBelow > 0f ? RigidCentres(positions, indices, entry.RigidBelow) : null;
        bool kit = entry.Footprint < 1f && entry.Rule == ModelRule.Smooth4;
        // ModelEntry.SpriteTop: built for tg's junction, turned and drawn in in the world's frame (a placement unturned), its empty inner corners cut out
        int turns = kit && junction >= 0 ? ModelRules.Smooth4(junction).Turns : 0;
        int open = kit ? junction >= 0 ? ~ModelRules.Mask(junction) & AllSides : OpenSides(part) : 0;
        List<(int X, int Z)> notches = kit && junction >= 0 ? Notches(junction) : null;
        float half = tile / 2f;
        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 at = Turned(positions[i], turns);
            normals[i] = Turned(normals[i], turns);
            data.Uvs[i] = entry.Texture switch
            {
                ModelTexture.Sprite => SpriteUv(at, normals[i], entry.SourceWidth, entry.SourceHeight),
                ModelTexture.Cell => cellUvs[i],
                _ => uvs[i],
            };
            at -= pivot;
            data.Uv2[i] = new Vector2(part.Slide.X * sx, ModelMeshData.Uv2Y(part.Slide.Y * sy, glass[i], glassAlpha[i]));
            if (rigidCentre != null && !float.IsNaN(rigidCentre[i].X))
            {
                // A rigid piece keeps its proportions: its centre moves with the stretch, its shape is scaled as the height is.
                Vector3 c = Turned(rigidCentre[i], turns) - pivot;
                Vector3 moved = Inset(new Vector3(c.X * sx, 0f, c.Z * sz), open, entry.Footprint, kit ? half : 0f, notches);
                data.Positions[i] = new Vector3(moved.X + (at.X - c.X) * sy, at.Y * sy, moved.Z + (at.Z - c.Z) * sy);
            }
            else
                data.Positions[i] = Inset(new Vector3(at.X * sx, at.Y * sy, at.Z * sz), open, entry.Footprint, kit ? half : 0f);
        }
        data.Normals = normals.ToArray();
        if (sx != sy)
        {
            // A stretch tilts the normals of slanted faces; the models are flat-shaded and axis-aligned, so this is exact for them and close for the rest.
            for (int i = 0; i < data.Normals.Length; i++)
            {
                Vector3 n = data.Normals[i];
                data.Normals[i] = Vector3.Normalize(new Vector3(n.X / sx, n.Y / sy, n.Z / sz));
            }
        }
        if (entry.Tip != 0f)
        {
            // Turned about the floor's middle, then lifted and centred by the fitted size, not each piece's bounds: an entry's pieces (one per material) stay together.
            Matrix4x4 tipped = Matrix4x4.CreateRotationX(entry.Tip * MathF.PI / 180f) * Matrix4x4.CreateTranslation(0f, sz * entry.SourceWidth / 2f, -sy * entry.SourceHeight / 2f);
            for (int i = 0; i < data.Positions.Length; i++)
            {
                data.Positions[i] = Vector3.Transform(data.Positions[i], tipped);
                data.Normals[i] = Vector3.TransformNormal(data.Normals[i], tipped);
            }
        }
        if (nodeRanges.Count > 0)
            data.PickBoxes = PickBoxesOf(data.Positions, nodeRanges);
        if (notches is { Count: > 0 })
            data = CutNotches(data, notches, half * entry.Footprint, half + JoinOverlap);
        if (entry.SpriteTop && topCell != null && kit && junction >= 0)
            data = LayTop(data, junction, open, half, entry.Footprint, topCell, notches);
        return data;
    }

    private const int AllSides = ModelRules.North | ModelRules.East | ModelRules.South | ModelRules.West;

    // ModelEntry.PickApart: the bounds of each box's vertices (a node's range runs to the next node's start).
    private static List<(Vector3 Min, Vector3 Max)> PickBoxesOf(Vector3[] positions, List<(int Box, int Start)> ranges)
    {
        var boxes = new SortedDictionary<int, (Vector3 Min, Vector3 Max)>();
        for (int r = 0; r < ranges.Count; r++)
        {
            int end = r + 1 < ranges.Count ? ranges[r + 1].Start : positions.Length;
            for (int i = ranges[r].Start; i < end; i++)
                boxes[ranges[r].Box] = boxes.TryGetValue(ranges[r].Box, out var b) ? (Vector3.Min(b.Min, positions[i]), Vector3.Max(b.Max, positions[i])) : (positions[i], positions[i]);
        }
        return boxes.Values.ToList();
    }

    // A point turned quarter turns counter-clockwise seen from above, as a placement's positive yaw (east goes north, -z).
    private static Vector3 Turned(Vector3 p, int turns)
    {
        for (int t = 0; t < (turns % 4 + 4) % 4; t++)
            p = new Vector3(p.Z, p.Y, -p.X);
        return p;
    }

    // The inner corners of tg's junction a table's top must leave empty: a diagonal whose two cardinals are joined and itself is not (an L's inside corner, a
    // tee's or a cross's open diagonal), as signs of the corner's x and z (north is -z).
    private static List<(int X, int Z)> Notches(int junction)
    {
        var notches = new List<(int X, int Z)>();
        void Check(int a, int b, int diagonal, int x, int z)
        {
            if ((junction & a) != 0 && (junction & b) != 0 && (junction & diagonal) == 0)
                notches.Add((x, z));
        }
        Check(ModelRules.TgNorth, ModelRules.TgEast, ModelRules.TgNorthEast, 1, -1);
        Check(ModelRules.TgSouth, ModelRules.TgEast, ModelRules.TgSouthEast, 1, 1);
        Check(ModelRules.TgSouth, ModelRules.TgWest, ModelRules.TgSouthWest, -1, 1);
        Check(ModelRules.TgNorth, ModelRules.TgWest, ModelRules.TgNorthWest, -1, -1);
        return notches;
    }

    private struct Vertex
    {
        public Vector3 P, N;
        public Vector2 Uv, Uv2;

        public static Vertex Lerp(Vertex a, Vertex b, float t) => new()
        {
            P = Vector3.Lerp(a.P, b.P, t), N = a.N, Uv = Vector2.Lerp(a.Uv, b.Uv, t), Uv2 = Vector2.Lerp(a.Uv2, b.Uv2, t),
        };
    }

    // The faces cut back out of each inner corner (the square past the footprint on both of the corner's sides), and the two walls of the cut put in, textured as
    // the top's other sides: the neighbours' drawn-in edges meet there, as tg's inner corner outline.
    private static ModelMeshData CutNotches(ModelMeshData data, List<(int X, int Z)> notches, float reach, float edge)
    {
        List<List<Vertex>> faces = Faces(data);
        foreach ((int x, int z) in notches)
        {
            var kept = new List<List<Vertex>>();
            foreach (List<Vertex> face in faces)
            {
                // outside the corner's square: on the near side of its x edge, or past it and on the near side of its z edge
                List<Vertex> nearX = Clip(face, v => reach - x * v.P.X);
                List<Vertex> farX = Clip(Clip(face, v => x * v.P.X - reach), v => reach - z * v.P.Z);
                if (nearX.Count >= 3) kept.Add(nearX);
                if (farX.Count >= 3) kept.Add(farX);
            }
            faces = kept;
        }
        float top = data.Positions.Max(p => p.Y);
        float under = data.Positions.Where(p => p.Y < top - 1e-3f).Select(p => p.Y).DefaultIfEmpty(top).Max();
        // a side's uv at the top and at the underside (the kit's outer sides: tg's rim)
        Vector2 SideUv(float y, Vector2 fallback)
        {
            for (int i = 0; i < data.Positions.Length; i++)
                if (MathF.Abs(data.Normals[i].Y) < 0.1f && MathF.Abs(data.Positions[i].Y - y) < 1e-3f)
                    return data.Uvs[i];
            return fallback;
        }
        Vector2 high = SideUv(top, data.Uvs.FirstOrDefault()), low = SideUv(under, high);
        foreach ((int x, int z) in notches)
        {
            Vertex V(float px, float pz, float y, Vector3 n) => new() { P = new Vector3(px, y, pz), N = n, Uv = y >= top ? high : low, Uv2 = data.Uv2.FirstOrDefault() };
            // the wall along x = reach (facing +x out of the cut) and the one along z = reach
            var nx = new Vector3(x, 0f, 0f);
            faces.Add(new List<Vertex> { V(x * reach, z * reach, under, nx), V(x * reach, z * edge, under, nx), V(x * reach, z * edge, top, nx), V(x * reach, z * reach, top, nx) });
            var nz = new Vector3(0f, 0f, z);
            faces.Add(new List<Vertex> { V(x * reach, z * reach, under, nz), V(x * edge, z * reach, under, nz), V(x * edge, z * reach, top, nz), V(x * reach, z * reach, top, nz) });
        }
        return FromFaces(faces, data);
    }

    // A convex face clipped to the side where inside(v) >= 0 (Sutherland-Hodgman).
    private static List<Vertex> Clip(List<Vertex> face, Func<Vertex, float> inside)
    {
        var result = new List<Vertex>(face.Count + 2);
        for (int i = 0; i < face.Count; i++)
        {
            Vertex a = face[i], b = face[(i + 1) % face.Count];
            float da = inside(a), db = inside(b);
            if (da >= 0f)
                result.Add(a);
            if (da >= 0f != db >= 0f)
                result.Add(Vertex.Lerp(a, b, da / (da - db)));
        }
        return result;
    }

    // tg's top-down picture of a table (its junction state, 32 x 32): the outline of an open west, north or east side 2 pixels in from the cell's edge, an open
    // south side's outline on row 24 (below it tg draws the table's front and legs, which the model's sides and legs are); a joined side runs to the cell's edge.
    private const float SpriteCell = 32f, OpenEdge = 2f, OpenSouthEdge = 25f, SouthHighlight = 22f, SouthOutline = 24f;

    // The top face's uv over the cell of tg's junction state (topCell: the cell's uv rectangle in the material's image, glTF's v down, for a junction; null: no
    // such state): each half of the top maps to its half of the cell, an open side's edge to tg's outline, a joined one to the cell's edge.
    private static ModelMeshData LayTop(ModelMeshData data, int junction, int open, float half, float footprint, Func<int, Vector4?> topCell,
        List<(int X, int Z)> notches)
    {
        // tg's own state for the junction, else the one of its cardinals (a diagonal tg's sheet has no state for)
        Vector4? cell = topCell(junction) ?? topCell(junction & (ModelRules.TgNorth | ModelRules.TgSouth | ModelRules.TgEast | ModelRules.TgWest));
        if (cell == null || data.Positions.Length == 0)
            return data;
        float top = data.Positions.Max(p => p.Y);
        float reach = half * footprint;
        bool IsTop(List<Vertex> face) => face.All(v => v.N.Y >= 0.9f && v.P.Y >= top - 1e-4f);
        // The mapping below bends at the centre and at the footprint: the top is cut along those lines first, so each piece's uv run straight across it.
        List<List<Vertex>> faces = Faces(data);
        // a side's outline is its last pixel: one of the 14 between the centre and tg's outline (16 to 2)
        float rim = reach * (1f - 1f / (16f - OpenEdge));
        foreach (float line in new[] { -reach, -rim, 0f, rim, reach })
        {
            faces = faces.SelectMany(f => IsTop(f)
                ? new[] { Clip(f, v => line - v.P.X), Clip(f, v => v.P.X - line) }.Where(g => g.Count >= 3)
                : new[] { f }).ToList();
            faces = faces.SelectMany(f => IsTop(f)
                ? new[] { Clip(f, v => line - v.P.Z), Clip(f, v => v.P.Z - line) }.Where(g => g.Count >= 3)
                : new[] { f }).ToList();
        }
        // From the cell's centre (16) out to the footprint: tg's outline (where an open side ends, or an inner corner's cut); a joined side goes on past it to
        // the cell's edge, so the neighbour's cell goes on from there. outer: the piece lies past the footprint.
        float Map(float v, bool outer, float openEdge, float cellEdge)
            => outer ? openEdge + MathF.Min((MathF.Abs(v) - reach) / (half - reach), 1f) * (cellEdge - openEdge)
                     : 16f + MathF.Min(MathF.Abs(v) / reach, 1f) * (openEdge - 16f);
        // tg draws a table's south edge as its front in perspective (a highlight, a shadow and the outline, rows 22 to 24); a 3D top shows the same edge on
        // every side (the model's side is the front): the top's own rows to the highlight, then the outline row on the side's last pixel (the owner,
        // 2026-10-19: the south edge was wider than the others). Past the footprint (a joined side in a cut corner's quarter) tg's rows below.
        float SouthEdge(float z, float piece)
            => piece > reach ? OpenSouthEdge + MathF.Min((MathF.Abs(z) - reach) / (half - reach), 1f) * (SpriteCell - OpenSouthEdge)
               : piece > rim ? SouthOutline + MathF.Min((MathF.Abs(z) - rim) / (reach - rim), 1f)
               : 16f + MathF.Min(MathF.Abs(z) / rim, 1f) * (SouthHighlight - 16f);
        Vector4 c = cell.Value;
        foreach (List<Vertex> face in faces.Where(IsTop))
        {
            float cx = face.Average(v => v.P.X), cz = face.Average(v => v.P.Z);
            bool east = cx >= 0f, south = cz >= 0f;
            // tg's inner corner at the south draws the inner arm's outline down through the front's highlight (table-6: column 29, rows 22 to 24) and shades
            // the highlight's first pixel; in the 3D top the front's strip runs on to the corner instead, so those columns are left out of the quarter
            bool southNotch = notches != null && south && notches.Contains((east ? 1 : -1, 1));
            float eastEdge = SpriteCell - OpenEdge - (southNotch ? 1f : 0f), westEdge = OpenEdge + (southNotch ? 1f : 0f);
            for (int k = 0; k < face.Count; k++)
            {
                Vertex v = face[k];
                bool outerX = MathF.Abs(cx) > reach, outerZ = MathF.Abs(cz) > reach;
                // past the cut the front's strip is tg's last column (table-6's column 30 is the top's own colour on the highlight row)
                float px = east ? Map(v.P.X, outerX, outerX ? SpriteCell - OpenEdge + (southNotch ? 1f : 0f) : eastEdge, SpriteCell)
                    : Map(v.P.X, outerX, outerX ? OpenEdge - (southNotch ? 1f : 0f) : westEdge, 0f);
                float py = !south ? Map(v.P.Z, outerZ, OpenEdge, 0f)
                    : (open & ModelRules.South) != 0 || southNotch ? SouthEdge(v.P.Z, MathF.Abs(cz))
                    : Map(v.P.Z, outerZ, OpenSouthEdge, SpriteCell);
                float u = c.X + px / SpriteCell * (c.Z - c.X), w = c.Y + py / SpriteCell * (c.W - c.Y);
                v.Uv = new Vector2(u, 1f - w); // glTF's v runs down the image; the shader's runs up
                face[k] = v;
            }
        }
        return FromFaces(faces, data);
    }

    private static List<List<Vertex>> Faces(ModelMeshData data)
    {
        var faces = new List<List<Vertex>>();
        for (int t = 0; t + 2 < data.Indices.Length; t += 3)
            faces.Add(new List<Vertex>(new[] { data.Indices[t], data.Indices[t + 1], data.Indices[t + 2] }
                .Select(k => new Vertex { P = data.Positions[k], N = data.Normals[k], Uv = data.Uvs[k], Uv2 = data.Uv2[k] })));
        return faces;
    }

    private static ModelMeshData FromFaces(List<List<Vertex>> faces, ModelMeshData like)
    {
        var vertices = new List<Vertex>();
        var indices = new List<int>();
        foreach (List<Vertex> face in faces)
        {
            int start = vertices.Count;
            vertices.AddRange(face);
            for (int k = 1; k + 1 < face.Count; k++)
            {
                indices.Add(start);
                indices.Add(start + k);
                indices.Add(start + k + 1);
            }
        }
        return new ModelMeshData
        {
            Positions = vertices.Select(v => v.P).ToArray(),
            Normals = vertices.Select(v => v.N).ToArray(),
            Uvs = vertices.Select(v => v.Uv).ToArray(),
            Uv2 = vertices.Select(v => v.Uv2).ToArray(),
            Indices = indices.ToArray(),
            Image = like.Image,
            Pivot = like.Pivot,
        };
    }

    // A smoothing kit's module's sides that join no neighbour (ModelRules' North, East, ...), in the module's own frame: the rule's joins of its name, turned back by
    // the part's own turn (a kit whose module joins north where the rule's joins east turns it -90 degrees).
    private static int OpenSides(ModelPart part)
    {
        int joins = ModelRules.JoinsOf(part.Name);
        int lefts = ((int)MathF.Round(-part.Turn / 90f) % 4 + 4) % 4;
        for (int i = 0; i < lefts; i++)
            joins = ModelRules.TurnLeft(joins);
        return ~joins & (ModelRules.North | ModelRules.East | ModelRules.South | ModelRules.West);
    }

    // A point drawn in toward the tile's centre on the open sides (north is -z): to the footprint's share of the way out. On a joined side a point on the tile's
    // edge (half from the centre) goes a hair past it, so the neighbour's top overlaps this one and no crack of the floor shows along the join.
    public const float JoinOverlap = 0.003f;

    private static Vector3 Inset(Vector3 p, int open, float share, float half, List<(int X, int Z)> notches = null)
    {
        if (open == 0 && half <= 0f)
            return p;
        // a leg in an inner corner that is cut out stands in from both of its sides, as a leg at an open corner does
        if (notches != null)
            foreach ((int x, int z) in notches)
                if (x * p.X > 0f && z * p.Z > 0f)
                    return new Vector3(p.X * share, p.Y, p.Z * share);
        p.X = Side(p.X, (open & (p.X > 0f ? ModelRules.East : ModelRules.West)) != 0);
        p.Z = Side(p.Z, (open & (p.Z < 0f ? ModelRules.North : ModelRules.South)) != 0);
        return p;

        float Side(float v, bool openSide)
        {
            if (openSide)
                return v * share;
            return half > 0f && MathF.Abs(v) >= half - 1e-4f ? MathF.Sign(v) * (half + JoinOverlap) : v;
        }
    }

    /// <summary>Whether a part draws a mesh node: its own node, or for a whole-file part every node but a later frame of a clip's frame meshes
    /// (<c>X_Frame_N</c>, N &gt; 0: the exporter hides them in the rest pose; frame 0's live picture plays the state's frames).</summary>
    public static bool Takes(ModelPart part, string node)
        => part.Node != null ? node == part.Node : !LaterFrame.IsMatch(node);

    private static readonly System.Text.RegularExpressions.Regex LaterFrame = new(@"_Frame_[1-9][0-9]*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>The materials a part's faces use (indices into the file's materials; -1 for faces with none), in the order they first appear.</summary>
    public static int[] MaterialsOf(GlbFile file, ModelPart part)
    {
        var used = new List<int>();
        foreach (GlbMeshNode node in file.MeshNodes)
        {
            if (!Takes(part, node.Name))
                continue;
            foreach (GlbPrimitive p in node.Primitives)
            {
                GlbMaterial material = p.Material >= 0 && p.Material < file.Materials.Count ? file.Materials[p.Material] : null;
                if (part.Material != null && (material == null || !material.Name.StartsWith(part.Material, StringComparison.Ordinal)))
                    continue;
                if (!used.Contains(p.Material))
                    used.Add(p.Material);
            }
        }
        return used.ToArray();
    }

    /// <summary>The uv of a material's faces in a part, as the file has them (before the material's transform).</summary>
    public static IEnumerable<Vector2> UvsOf(GlbFile file, ModelPart part, int material)
    {
        foreach (GlbMeshNode node in file.MeshNodes)
        {
            if (!Takes(part, node.Name))
                continue;
            foreach (GlbPrimitive p in node.Primitives)
                if (p.Material == material)
                    foreach (Vector2 uv in p.Uvs)
                        yield return uv;
        }
    }

    /// <summary>The pixel size of the sheet a material shows (its embedded PNG's header); (0, 0) when it has none.</summary>
    public static (int Width, int Height) SheetSize(GlbFile file, GlbMaterial material)
    {
        byte[] png = material != null && material.Image >= 0 && material.Image < file.Images.Count ? file.Images[material.Image] : null;
        if (png == null || png.Length < 24)
            return (0, 0);
        // The IHDR chunk comes first: width and height, big-endian, at bytes 16 and 20.
        return ((png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19], (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23]);
    }

    /// <summary>
    /// The uv of a primitive over the one icon frame of the sheet it shows (<paramref name="cellWidth"/> by <paramref name="cellHeight"/> pixels, the frame its uv lie in):
    /// 0..1 over the frame, v up. A primitive that is not inside one frame is mapped by the frame of its lowest corner.
    /// </summary>
    public static Vector2[] CellUvs(Vector2[] uvs, (int Width, int Height) sheet, int cellWidth, int cellHeight)
    {
        var result = new Vector2[uvs.Length];
        if (uvs.Length == 0 || sheet.Width <= 0 || sheet.Height <= 0)
            return result;
        float minX = uvs.Min(u => u.X) * sheet.Width, minY = uvs.Min(u => u.Y) * sheet.Height;
        float originX = MathF.Floor((minX + 0.01f) / cellWidth) * cellWidth, originY = MathF.Floor((minY + 0.01f) / cellHeight) * cellHeight;
        for (int i = 0; i < uvs.Length; i++)
        {
            float u = (uvs[i].X * sheet.Width - originX) / cellWidth;
            float v = (uvs[i].Y * sheet.Height - originY) / cellHeight;
            result[i] = new Vector2(Math.Clamp(u, 0f, 1f), Math.Clamp(1f - v, 0f, 1f));
        }
        return result;
    }

    /// <summary>
    /// Where things rest on a model's shelves (fitted metres, the model unturned): the upward faces inside it, below its top, grouped by height (a shelf, a rack, a floor
    /// tray), each with up to four places along its width, the highest shelf first.
    /// </summary>
    public static Vector3[] Shelves(ModelMeshData body) => Levels(body, belowTop: true);

    /// <summary>Where things rest on a model with open shelves (a rack): every upward level, its top too, the highest first.</summary>
    public static Vector3[] AllLevels(ModelMeshData model) => Levels(model, belowTop: false);

    /// <summary>
    /// The height of the model's highest face straight above the tile's centre (a bed's mattress, not its headrest: DV127's lying body), in the tile's
    /// frame; NaN when no face is over it.
    /// </summary>
    public static float TopAtCentre(ModelMeshData model)
    {
        float best = float.NaN;
        float px = -model.Pivot.X, pz = -model.Pivot.Z; // the tile's centre in the mesh's own frame
        Vector3[] v = model.Positions;
        for (int i = 0; i + 2 < model.Indices.Length; i += 3)
        {
            Vector3 a = v[model.Indices[i]], b = v[model.Indices[i + 1]], c = v[model.Indices[i + 2]];
            float d = (b.X - a.X) * (c.Z - a.Z) - (c.X - a.X) * (b.Z - a.Z);
            if (MathF.Abs(d) < 1e-9f)
                continue; // a wall seen edge on from above
            float u = ((px - a.X) * (c.Z - a.Z) - (c.X - a.X) * (pz - a.Z)) / d;
            float w = ((b.X - a.X) * (pz - a.Z) - (px - a.X) * (b.Z - a.Z)) / d;
            if (u < 0f || w < 0f || u + w > 1f)
                continue;
            float y = a.Y + u * (b.Y - a.Y) + w * (c.Y - a.Y);
            if (float.IsNaN(best) || y > best)
                best = y;
        }
        return float.IsNaN(best) ? best : best + model.Pivot.Y;
    }

    /// <summary>Where things rest inside a crate (fitted metres, the model unturned): the lowest upward level of its inside (its floor), up to four places along it.</summary>
    public static Vector3[] Floor(ModelMeshData inside)
    {
        Vector3[] all = Levels(inside, belowTop: false);
        if (all.Length == 0)
            return all;
        float lowest = all.Min(s => s.Y);
        return all.Where(s => s.Y == lowest).ToArray();
    }

    // The upward levels of a mesh (below its top when belowTop), the highest first, each with up to four places along its width.
    private static Vector3[] Levels(ModelMeshData body, bool belowTop)
    {
        if (body.Positions.Length == 0)
            return Array.Empty<Vector3>();
        float top = belowTop ? body.Positions.Max(p => p.Y) : float.PositiveInfinity;
        var levels = new SortedDictionary<int, (Vector3 Min, Vector3 Max, float Area)>();
        for (int t = 0; t + 2 < body.Indices.Length; t += 3)
        {
            Vector3 a = body.Positions[body.Indices[t]], b = body.Positions[body.Indices[t + 1]], c = body.Positions[body.Indices[t + 2]];
            Vector3 cross = Vector3.Cross(b - a, c - a);
            float area = cross.Length() / 2f;
            Vector3 n = body.Normals[body.Indices[t]];
            if (area < 1e-6f || n.Y < 0.9f || MathF.Abs(a.Y - b.Y) > 0.005f || MathF.Abs(a.Y - c.Y) > 0.005f || a.Y > top - 0.05f)
                continue;
            int key = (int)MathF.Round(a.Y * 100f);
            Vector3 min = Vector3.Min(a, Vector3.Min(b, c)), max = Vector3.Max(a, Vector3.Max(b, c));
            levels[key] = levels.TryGetValue(key, out var level) ? (Vector3.Min(level.Min, min), Vector3.Max(level.Max, max), level.Area + area) : (min, max, area);
        }
        var slots = new List<Vector3>();
        foreach (var level in levels.Values.Reverse())
        {
            float width = level.Max.X - level.Min.X, depth = level.Max.Z - level.Min.Z;
            if (level.Area < 0.03f || width < 0.15f || depth < 0.1f)
                continue; // a ledge or a rim, not a shelf
            int count = Math.Clamp((int)(width / 0.22f), 1, 4);
            float z = (level.Min.Z + level.Max.Z) / 2f;
            for (int k = 0; k < count; k++)
                slots.Add(new Vector3(level.Min.X + width * (k + 0.5f) / count, level.Max.Y, z));
        }
        return slots.ToArray();
    }

    // The centre of the connected piece each vertex belongs to when the whole piece stays at or below the height (NaN otherwise). Flat-shaded faces have their own
    // vertices, so the pieces are found by the position (welded within a tenth of a millimetre), not by the shared vertex.
    private static Vector3[] RigidCentres(List<Vector3> positions, List<int> indices, float below)
    {
        var weld = new Dictionary<(long, long, long), int>();
        var root = new int[positions.Count];
        int Key(Vector3 v)
        {
            var k = ((long)MathF.Round(v.X * 10000f), (long)MathF.Round(v.Y * 10000f), (long)MathF.Round(v.Z * 10000f));
            if (!weld.TryGetValue(k, out int id))
                weld[k] = id = weld.Count;
            return id;
        }
        for (int i = 0; i < positions.Count; i++)
            root[i] = Key(positions[i]);
        var parent = Enumerable.Range(0, weld.Count).ToArray();
        int Find(int a)
        {
            while (parent[a] != a)
                a = parent[a] = parent[parent[a]];
            return a;
        }
        for (int t = 0; t + 2 < indices.Count; t += 3)
        {
            int a = Find(root[indices[t]]);
            foreach (int k in new[] { indices[t + 1], indices[t + 2] })
            {
                int b = Find(root[k]);
                if (a != b)
                    parent[b] = a;
            }
        }
        var min = new Dictionary<int, Vector3>();
        var max = new Dictionary<int, Vector3>();
        for (int i = 0; i < positions.Count; i++)
        {
            int c = Find(root[i]);
            min[c] = min.TryGetValue(c, out Vector3 lo) ? Vector3.Min(lo, positions[i]) : positions[i];
            max[c] = max.TryGetValue(c, out Vector3 hi) ? Vector3.Max(hi, positions[i]) : positions[i];
        }
        var centres = new Vector3[positions.Count];
        for (int i = 0; i < positions.Count; i++)
        {
            int c = Find(root[i]);
            centres[i] = max[c].Y <= below ? (min[c] + max[c]) / 2f : new Vector3(float.NaN);
        }
        return centres;
    }
}
