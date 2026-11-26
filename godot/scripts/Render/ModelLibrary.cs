using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Models;

namespace Anomaly.Client.Render;

/// <summary>
/// The 3D models that replace tg's 2D pictures (docs/ai/specs/models.md): the catalog (<see cref="ModelCatalog"/>) and the files in <c>godot/assets/models</c>, read as plain
/// files the way the converted icons are (the folder is <c>.gdignore</c>d; <c>ANOMALY_MODEL_ROOT</c> overrides it). Everything is made once and on first use: a .glb is
/// read once, a part's mesh is built once (one <see cref="ArrayMesh"/> per part, drawn by one MultiMesh per chunk that has it), a model's texture is put in the prop
/// atlas once (the same image in several files is stored once).
/// </summary>
public sealed class ModelLibrary
{
    private readonly string _root;
    private readonly PropAtlas _atlas;
    private readonly Dictionary<string, GlbFile> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Entry, string Part), (int Mesh, int Picture, Vector3 Pivot)> _parts = new();
    private readonly Dictionary<string, System.Numerics.Vector3[]> _slots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _backs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _tops = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _lieTops = new(StringComparer.Ordinal); // DV127: the top over the tile's centre
    private readonly Dictionary<string, float> _fronts = new(StringComparer.Ordinal); // gaps W1: the frontmost z of its meshes (FrontOf)
    private readonly Dictionary<string, (float MinX, float MinY, float MaxX, float MaxY)> _faces = new(StringComparer.Ordinal); // gaps W1: FaceOf
    private readonly Dictionary<string, int> _pictures = new(StringComparer.Ordinal);
    private readonly HashSet<(string Entry, string Part)> _failed = new();
    private readonly HashSet<(string Family, uint Tint, string Gags)> _prepared = new();
    private readonly Dictionary<(string Entry, string Part, uint Tint, int Junction, string Gags), ModelPiece[]> _pieces = new();
    // A material's mesh does not depend on the tint or the GAGS key (only its picture does): a new lamp colour reuses the family's meshes.
    private readonly Dictionary<(string Entry, string Part, int Material, int Junction), (int Mesh, Vector3 Pivot)> _pieceMeshes = new();
    private readonly Dictionary<string, byte[]> _images = new(StringComparer.Ordinal);
    private readonly Dictionary<byte[], string> _pngHashes = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<byte[], string> _frameHashes = new(ReferenceEqualityComparer.Instance);
    private byte[] _lastPng;
    private Raster _lastDecoded;
    // A family's warm-up (PrepareFamily) runs over frames: its textures decode on the thread pool, its parts are built when they are in.
    private sealed class Warm
    {
        public ModelEntry Variant;
        public string Part;
        public uint Tint;
        public string Gags;
        public byte[][] Images; // null until its decodes are started
    }

    private const int DecodesInFlight = 8;
    private readonly List<Warm> _warm = new();

    /// <summary>The parts queued by <see cref="PrepareFamily"/> and not built yet.</summary>
    public int WarmUpCount => _warm.Count;
    private readonly Dictionary<byte[], (Task<Raster> Task, int Users)> _decoding = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, (byte[] Png, IconSheet Sheet)> _sheets = new(StringComparer.Ordinal);
    private readonly Dictionary<byte[], Task<string>> _hashing = new(ReferenceEqualityComparer.Instance);
    private readonly List<ArrayMesh> _meshes = new();
    private readonly Dictionary<int, List<(System.Numerics.Vector3 Min, System.Numerics.Vector3 Max)>> _pickBoxes = new(); // ModelEntry.PickApart
    private readonly Dictionary<int, Material> _materials = new();
    private readonly Dictionary<int, ArrayMesh> _hulls = new();
    private readonly Dictionary<string, AttachPoint[]> _attach = new(StringComparer.Ordinal);

    /// <summary>
    /// A model's own attach points in its fitted frame (docs/ai/specs/models.md, "Attach points"): the catalog's, then its files' <c>Attach_*</c> nodes scaled as its
    /// meshes are; none when it names none (the thing's bounds give them then). A file that cannot be read adds none.
    /// </summary>
    public AttachPoint[] AttachPointsOf(ModelEntry entry)
    {
        if (_attach.TryGetValue(entry.Id, out AttachPoint[] known))
            return known;
        var points = new List<AttachPoint>(entry.AttachPoints);
        (float horizontal, float vertical) = ModelMesh.Scale(entry, TileSpace.TileSize, WorldScale.WallHeight, WorldScale.TableTop);
        foreach (string file in entry.Parts.Values.Select(p => p.File).Where(f => f != null).Distinct(StringComparer.Ordinal))
        {
            try
            {
                points.AddRange(AttachPoints.OfFile(ReadFile(file), horizontal, vertical));
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                GD.PushWarning($"[anomaly] model {entry.Id}: no attach points from {file}: {e.Message}");
            }
        }
        return _attach[entry.Id] = points.ToArray();
    }

    /// <summary>The folder of the models (it may not exist: then no model replaces anything).</summary>
    public static string Root
    {
        get
        {
            string overrideRoot = System.Environment.GetEnvironmentVariable("ANOMALY_MODEL_ROOT");
            return string.IsNullOrEmpty(overrideRoot) ? DataPaths.Find("assets/models") : overrideRoot;
        }
    }

    public ModelLibrary(PropAtlas atlas, string root = null)
    {
        _atlas = atlas;
        _root = root ?? Root;
        string path = Path.Combine(_root, ModelCatalog.FileName);
        if (!File.Exists(path))
        {
            Catalog = new ModelCatalog();
            return;
        }
        try
        {
            Catalog = ModelCatalog.Parse(File.ReadAllText(path));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or ArgumentException or KeyNotFoundException or InvalidOperationException)
        {
            GD.PushWarning($"ModelLibrary: {path} is not read ({e.Message}); no 3D model replaces a picture.");
            Catalog = new ModelCatalog();
        }
    }

    /// <summary>The catalog.</summary>
    public ModelCatalog Catalog { get; }

    /// <summary>Prepare all states and hidden parts of a family before it can change in view: queued, built over the next frames (<see cref="StepWarmUp"/>).</summary>
    public void PrepareFamily(ModelEntry entry, uint tint, string gags)
    {
        if (entry.Family.Length == 0 || !_prepared.Add((entry.Family, tint, gags)))
            return;
        ModelEntry[] variants = Catalog.Entries.Where(e => e.Family == entry.Family && e.Texture == ModelTexture.Model).ToArray();
        // shortcut: a family of many variants (the bar sign's 50 signs, each its own texture: 5 s of warm-up) is not prepared whole; its variant is built
        // when it shows. Prepare its likely neighbours if such a family's changes start to stall.
        if (variants.Length > MaxWarmVariants)
            return;
        foreach (ModelEntry variant in variants)
            foreach (string part in variant.Parts.Keys)
                _warm.Add(new Warm { Variant = variant, Part = part, Tint = tint, Gags = gags });
    }

    private const int MaxWarmVariants = 16;

    /// <summary>
    /// True (the join's build): a part whose textures are not decoded waits for them. False (in play): it starts their decodes on the thread pool and
    /// is not built (<see cref="TryPieces"/> fails and counts <see cref="PendingTextures"/>); the caller builds it again once <see cref="DecodesDone"/> moved.
    /// </summary>
    public bool WaitForTextures { get; set; } = true;

    /// <summary>How many builds so far found their textures still decoding (a caller compares it before and after its build).</summary>
    public int PendingTextures { get; private set; }

    /// <summary>How many texture decodes have finished on the thread pool (a caller waiting for textures compares it with the last it saw).</summary>
    public int DecodesDone => Volatile.Read(ref _decodesDone);

    private int _decodesDone;
    // when each build's own decode started: one whose thing went before it was built is dropped after a while (StepWarmUp), not kept (16 MB a 2k texture)
    private readonly Dictionary<byte[], long> _ownDecodes = new(ReferenceEqualityComparer.Instance);
    private readonly List<byte[]> _dropped = new();
    private const int TexturePending = -2;

    private Task<Raster> Decode(byte[] png) => Counted(AssetPreload.TryDecoded(png, out Raster kept) ? Task.FromResult(kept) : Task.Run(() => PngDecoder.Decode(png)));

    // A decode is counted in DecodesDone only once its task is complete, so a caller that saw the count move finds its result in. Counted inside
    // the task, the count could move before IsCompleted did: a retry in between found the work still pending and took the new count as seen,
    // and a thing whose last wait that was had nothing left to move the count and wake it.
    private Task<T> Counted<T>(Task<T> task)
    {
        task.ContinueWith(_ => Interlocked.Increment(ref _decodesDone), TaskContinuationOptions.ExecuteSynchronously);
        return task;
    }

    /// <summary>
    /// Once a frame: starts the queued warm-up parts' texture decodes on the thread pool (a 1-2k texture takes tens of ms; a lamp family has dozens, which
    /// stalled the frame its first lamp came into view in) and builds the parts whose textures are in, for at most <paramref name="budgetMs"/>.
    /// </summary>
    public void StepWarmUp(double budgetMs)
    {
        long start = Stopwatch.GetTimestamp();
        if (_ownDecodes.Count > 0)
        {
            _dropped.Clear();
            foreach ((byte[] png, long at) in _ownDecodes)
                if (!_decoding.TryGetValue(png, out var d) || (d.Users == 0 && d.Task.IsCompleted && Stopwatch.GetElapsedTime(at, start).TotalSeconds > 10))
                    _dropped.Add(png); // gone with a warm-up part that took it over, or left unused
            foreach (byte[] png in _dropped)
            {
                if (_decoding.TryGetValue(png, out var d) && d.Users == 0)
                    _decoding.Remove(png);
                _ownDecodes.Remove(png);
            }
        }
        int inFlight = _decoding.Values.Count(d => !d.Task.IsCompleted);
        for (int i = 0; i < _warm.Count && Stopwatch.GetElapsedTime(start).TotalMilliseconds < budgetMs;)
        {
            Warm w = _warm[i];
            if (w.Images == null)
            {
                if (inFlight >= DecodesInFlight)
                    break;
                w.Images = PartImages(w.Variant, w.Part);
                foreach (byte[] png in w.Images)
                {
                    if (_decoding.TryGetValue(png, out var known))
                    {
                        _decoding[png] = (known.Task, known.Users + 1);
                        continue;
                    }
                    _decoding[png] = (Decode(png), 1);
                    inFlight++;
                }
            }
            if (w.Images.Any(png => !_decoding[png].Task.IsCompleted))
            {
                i++;
                continue;
            }
            _warm.RemoveAt(i);
            long one = Stopwatch.GetTimestamp();
            TryPieces(w.Variant, w.Part, w.Tint, out _, gags: w.Gags);
            Anomaly.Client.Dev.FrameProbe.Item("warm", w.Variant.Id + "/" + w.Part, Stopwatch.GetElapsedTime(one).TotalMilliseconds);
            foreach (byte[] png in w.Images)
            {
                var d = _decoding[png];
                if (d.Users <= 1)
                    _decoding.Remove(png); // its raster goes with the last part that needs it
                else
                    _decoding[png] = (d.Task, d.Users - 1);
            }
        }
    }

    // The textures a part's pieces cut their pictures from (TryPieces' choice); none when its file cannot be read (TryPieces reports that).
    private byte[][] PartImages(ModelEntry entry, string part)
    {
        if (!entry.Parts.TryGetValue(part, out ModelPart spec))
            return [];
        try
        {
            if (entry.Image != null)
                return [ImageBytes(entry.Image)];
            GlbFile file = ReadFile(spec.File);
            return ModelMesh.MaterialsOf(file, spec)
                .Select(i => i >= 0 && i < file.Materials.Count ? file.Materials[i] : null)
                .Where(m => m != null && m.Image >= 0 && m.Image < file.Images.Count && file.Images[m.Image] != null)
                .Select(m => file.Images[m.Image]).Distinct(ReferenceEqualityComparer.Instance).Cast<byte[]>().ToArray();
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or KeyNotFoundException or IndexOutOfRangeException or ArgumentException)
        {
            return [];
        }
    }

    /// <summary>The meshes so far, by index (a chunk's model MultiMesh draws one).</summary>
    public Mesh MeshAt(int index) => _meshes[index];

    /// <summary>A mesh's pick boxes in its own frame (<see cref="ModelEntry.PickApart"/>); null: its bounds.</summary>
    public List<(System.Numerics.Vector3 Min, System.Numerics.Vector3 Max)> PickBoxesAt(int index) => _pickBoxes.GetValueOrDefault(index);

    /// <summary>The material a mesh is drawn by when it is not the prop shader (a part's <see cref="ModelPart.Shader"/>: the supermatter's crystal); null: the prop atlas's.</summary>
    public Material MaterialAt(int index) => _materials.GetValueOrDefault(index);

    /// <summary>Whether a mesh is a crystal part (drawn by the crystal shader): an outline filter rims these (MapRenderer.Filters).</summary>
    public bool IsCrystal(int index) => _hulls.ContainsKey(index);

    /// <summary>A crystal part's mesh with smooth normals (<see cref="ModelMesh.SmoothNormals"/>): the outline's inverted hull.</summary>
    public Mesh HullAt(int index) => _hulls[index];

    /// <summary>Parts built so far.</summary>
    public int MeshCount => _meshes.Count;

    /// <summary>
    /// The mesh of a part of a model and its own texture's picture in the prop atlas (-1 for a model that takes the entity's pictures). False when the part cannot be
    /// built (a file is missing or broken: reported once, the thing keeps its 2D picture).
    /// </summary>
    public bool TryPart(ModelEntry entry, string part, out int mesh, out int picture) => TryPart(entry, part, out mesh, out picture, out _);

    /// <summary>
    /// Forgets what was built for entry <paramref name="id"/> (its meshes, tops, shelves, attach points), so its next use builds it again from the entry
    /// as it is now: the dev menu's Tools tab changes an entry's scale and offset live.
    /// </summary>
    // shortcut: the old meshes stay in _meshes (a dev tool's handful of rebuilds); free them if tuning ever runs outside the dev menu.
    public void Forget(string id)
    {
        foreach (var key in _parts.Keys.Where(k => k.Entry == id).ToList())
            _parts.Remove(key);
        foreach (var key in _pieces.Keys.Where(k => k.Entry == id).ToList())
            _pieces.Remove(key);
        foreach (var key in _pieceMeshes.Keys.Where(k => k.Entry == id).ToList())
            _pieceMeshes.Remove(key);
        _failed.RemoveWhere(k => k.Entry == id);
        _slots.Remove(id);
        _backs.Remove(id);
        _tops.Remove(id);
        _lieTops.Remove(id);
        _fronts.Remove(id);
        _faces.Remove(id);
        _attach.Remove(id);
    }

    /// <summary>As <see cref="TryPart(ModelEntry, string, out int, out int)"/>, with the part's pivot in the fitted model (a door's hinge; zero for most parts).</summary>
    public bool TryPart(ModelEntry entry, string part, out int mesh, out int picture, out Vector3 pivot)
    {
        mesh = -1;
        picture = -1;
        pivot = Vector3.Zero;
        var key = (entry.Id, part);
        if (_parts.TryGetValue(key, out var built))
        {
            (mesh, picture, pivot) = built;
            return true;
        }
        if (_failed.Contains(key) || !entry.Parts.TryGetValue(part, out ModelPart spec))
            return false;
        try
        {
            GlbFile file = ReadFile(spec.File);
            ModelMeshData data = ModelMesh.Build(file, entry, spec, TileSpace.TileSize, WorldScale.WallHeight, WorldScale.TableTop);
            if (entry.Texture == ModelTexture.Model && entry.Image != null)
                picture = Picture(ImageBytes(entry.Image)); // a kit's variant: its own texture in the kit's uv
            else if (entry.Texture == ModelTexture.Model)
            {
                if (data.Image < 0 || data.Image >= file.Images.Count || file.Images[data.Image] == null)
                    throw new InvalidDataException($"{spec.File} has no embedded texture");
                picture = Picture(file.Images[data.Image]);
            }
            mesh = _meshes.Count;
            _meshes.Add(ToMesh(data));
            if (data.PickBoxes != null)
                _pickBoxes[mesh] = data.PickBoxes;
            pivot = new Vector3(data.Pivot.X, data.Pivot.Y, data.Pivot.Z);
            float top = data.Positions.Max(v => v.Y) + data.Pivot.Y;
            _tops[entry.Id] = Math.Max(top, _tops.TryGetValue(entry.Id, out float seen) ? seen : 0f);
            NoteLieTop(entry.Id, data);
            NoteFront(entry, data);
            _parts[key] = (mesh, picture, pivot);
            // A closet's things rest on the shelves of its body, a crate's on the floor of its inside.
            if (part == (entry.Rule == ModelRule.Crate ? ModelRules.Inside : ModelRules.Body) && !_slots.ContainsKey(entry.Id))
                _slots[entry.Id] = Slots(entry, data);
            if (part == ModelRules.Body && !_backs.ContainsKey(entry.Id))
                _backs[entry.Id] = -data.Positions.Min(v => v.Z);
            return true;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or KeyNotFoundException or IndexOutOfRangeException or ArgumentException)
        {
            GD.PushWarning($"ModelLibrary: model {entry.Id}, part {part}: {e.Message}; it keeps its 2D picture.");
            _failed.Add(key);
            return false;
        }
    }

    /// <summary>
    /// A <see cref="ModelTexture.Model"/> part as the pieces it is drawn with, one per material it uses: its mesh and its picture in the prop atlas (the material's
    /// image with its tile transform, the entry's own image for a kit's variant, or a swatch of a plain material's colour: glass, water). A material whose name has
    /// <c>Tint</c> takes the image multiplied by <paramref name="tint"/> (tg's <c>color</c> on a greyscale sprite: a comfy chair's upholstery). A material that shows one
    /// frame of the entry's own tg sheet is a live frame (<see cref="ModelFrame"/>): its uv are local to the frame, and the renderer draws it with the thing's picture.
    /// A plain material's swatch is <see cref="ModelColors.Swatch"/>'s: a <c>Gags</c>N material takes that colour of <paramref name="gags"/>, the thing's GAGS key
    /// (a canister's shell and stripes by its gas). False when the part cannot be built (reported once; the thing keeps its 2D picture).
    /// </summary>
    public bool TryPieces(ModelEntry entry, string part, uint tint, out ModelPiece[] pieces, int junction = -1, string gags = null)
    {
        pieces = null;
        // shortcut: each GAGS key builds its own meshes of the part (a few canister gases a map); share the meshes across keys if many keys appear.
        var key = (entry.Id, part, tint, junction, gags);
        if (_pieces.TryGetValue(key, out pieces))
            return true;
        if (_failed.Contains((entry.Id, part)) || !entry.Parts.TryGetValue(part, out ModelPart spec))
            return false;
        try
        {
            GlbFile file = ReadFile(spec.File);
            var built = new List<ModelPiece>();
            bool framesPending = false;
            foreach (int index in ModelMesh.MaterialsOf(file, spec))
            {
                GlbMaterial material = index >= 0 && index < file.Materials.Count ? file.Materials[index] : null;
                ModelFrame frame = entry.Image == null ? FrameOf(entry, file, spec, index, material, ref framesPending) : null;
                if (framesPending)
                    continue; // which sheet's frame it shows is still being worked out: nothing of it is made yet
                byte[] image = entry.Image != null ? ImageBytes(entry.Image)
                    : material != null && material.Image >= 0 && material.Image < file.Images.Count ? file.Images[material.Image] : null;
                bool tinted = material != null && (spec.TintOverlay != null || material.Name.Contains("Tint", StringComparison.Ordinal)) && tint != 0xFFFFFFFFu;
                System.Numerics.Vector4 c = material?.BaseColor ?? System.Numerics.Vector4.One;
                int picture;
                if (entry.Image != null)
                    picture = Picture(ImageBytes(entry.Image), tinted ? tint : 0xFFFFFFFFu);
                else if (material != null && material.Image >= 0 && material.Image < file.Images.Count && file.Images[material.Image] != null)
                    // a frame's uv are local to it: its own pixels; the material's colour (glTF's linear factor, back to the image's sRGB) tints its image: a first
                    // aid station's reagent bars
                    picture = Picture(file.Images[material.Image], tinted ? tint : Rgba(Srgb(c.X), Srgb(c.Y), Srgb(c.Z)), frame, spec.Opaque, spec.Fill);
                else
                    picture = _atlas.Swatch(tinted && spec.TintOverlay != null ? tint : ModelColors.Swatch(entry.FlatColors ? "Flat_" + material?.Name : material?.Name, c, gags, tint, spec.Color)); // its alpha is the glass's own, in the mesh
                (int mesh, Vector3 pivot) = PieceMesh(file, entry, part, spec, index, frame, junction, image, c);
                // a flat-colour material (Flat: no shading painted into it) is shaded by its faces' normals in the prop shader
                bool shaded = image == null && material != null && (entry.FlatColors || material.Name.Contains("Flat", StringComparison.Ordinal));
                built.Add(new ModelPiece(mesh, picture, pivot, frame, material?.Name ?? string.Empty, material?.Glow ?? 1f, shaded, material?.Emissive ?? false));
            }
            if (framesPending || built.Any(p => p.Picture == TexturePending))
            {
                PendingTextures++; // its textures decode on the thread pool: built again when they are in
                pieces = null;
                return false;
            }
            pieces = built.ToArray();
            _pieces[key] = pieces;
            return true;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or KeyNotFoundException or IndexOutOfRangeException or ArgumentException)
        {
            GD.PushWarning($"ModelLibrary: model {entry.Id}, part {part}: {e.Message}; it keeps its 2D picture.");
            _failed.Add((entry.Id, part));
            return false;
        }
    }

    private static uint Rgba(float r, float g, float b)
        => (Byte(r) << 24) | (Byte(g) << 16) | (Byte(b) << 8) | 0xFFu;

    private static uint Byte(float v) => (uint)MathF.Round(Math.Clamp(v, 0f, 1f) * 255f);

    private static float Srgb(float linear)
        => linear <= 0.0031308f ? 12.92f * linear : 1.055f * MathF.Pow(linear, 1f / 2.4f) - 0.055f;

    private static float Linear(float srgb)
        => srgb <= 0.04045f ? srgb / 12.92f : MathF.Pow((srgb + 0.055f) / 1.055f, 2.4f);

    // A part drawn by a shader of its own (ModelPart.Shader): its colour (glTF's linear factor; a field's alpha), halo and tg's frames (the pulse) as the shader's uniforms.
    private static ShaderMaterial ShaderMaterialOf(ModelPart spec, System.Numerics.Vector4 colour)
    {
        bool field = spec.Shader == ModelPart.FieldShader;
        var m = new ShaderMaterial
        {
            Shader = GD.Load<Shader>(field ? "res://shaders/prop_field.gdshader" : spec.Shader == ModelPart.NozzleShader ? "res://shaders/prop_nozzle.gdshader" : "res://shaders/prop_crystal.gdshader"),
            RenderPriority = field ? 1 : 0, // the field is blended over the crystal it holds
        };
        m.SetShaderParameter("tint", new Vector3(colour.X, colour.Y, colour.Z));
        if (field)
            m.SetShaderParameter("opacity", colour.W);
        if (spec.Halo is System.Numerics.Vector3 halo)
            m.SetShaderParameter("halo", new Vector3(Linear(halo.X), Linear(halo.Y), Linear(halo.Z)));
        var seconds = new float[ModelPart.MaxPulse];
        var strengths = new float[ModelPart.MaxPulse];
        var scales = new Vector3[ModelPart.MaxPulse];
        for (int i = 0; i < spec.Pulse.Length; i++)
        {
            seconds[i] = spec.Pulse[i].Seconds;
            strengths[i] = spec.Pulse[i].Strength;
            scales[i] = new Vector3(spec.Pulse[i].Scale.X, spec.Pulse[i].Scale.Y, spec.Pulse[i].Scale.Z);
        }
        m.SetShaderParameter("pulse_count", spec.Pulse.Length);
        m.SetShaderParameter("pulse_seconds", seconds);
        m.SetShaderParameter("pulse_strength", strengths);
        m.SetShaderParameter("pulse_scale", scales);
        return m;
    }

    /// <summary>The highest point of a model's parts built so far (fitted metres above the floor): a table model's top, which things on it rest on; 0 before.</summary>
    public float TopOf(ModelEntry entry) => _tops.TryGetValue(entry.Id, out float top) ? top : 0f;

    /// <summary>DV127: the height a body lies at on the model: its top over the tile's centre (a bed's mattress, not its headrest), else <see cref="TopOf"/>.</summary>
    public float LieTopOf(ModelEntry entry) => _lieTops.TryGetValue(entry.Id, out float top) ? top : TopOf(entry);

    private void NoteLieTop(string id, ModelMeshData data)
    {
        float top = ModelMesh.TopAtCentre(data);
        if (!float.IsNaN(top))
            _lieTops[id] = Math.Max(top, _lieTops.TryGetValue(id, out float seen) ? seen : float.MinValue);
    }

    /// <summary>How far a model's body reaches behind its origin (fitted metres, unturned: toward -Z), once its body is built; 0 before.</summary>
    public float BackOf(ModelEntry entry) => _backs.TryGetValue(entry.Id, out float back) ? back : 0f;

    /// <summary>Gaps W1: how far in front of its origin the model's frontmost face is (its meshes' largest z, metres), 0 before one is built.</summary>
    public float FrontOf(ModelEntry entry) => _fronts.TryGetValue(entry.Id, out float front) ? front : 0f;

    /// <summary>Gaps W1: the rectangle its meshes cover seen from the front (x and y in its own frame, metres); false before one is built.</summary>
    public bool TryFaceOf(ModelEntry entry, out (float MinX, float MinY, float MaxX, float MaxY) face) => _faces.TryGetValue(entry.Id, out face);

    private void NoteFront(ModelEntry entry, ModelMeshData data)
    {
        if (data.Positions.Length == 0)
            return;
        float front = data.Positions.Max(v => v.Z) + data.Pivot.Z;
        _fronts[entry.Id] = _fronts.TryGetValue(entry.Id, out float seen) ? Math.Max(front, seen) : front;
        var face = (data.Positions.Min(v => v.X) + data.Pivot.X, data.Positions.Min(v => v.Y) + data.Pivot.Y, data.Positions.Max(v => v.X) + data.Pivot.X, data.Positions.Max(v => v.Y) + data.Pivot.Y);
        if (_faces.TryGetValue(entry.Id, out var had))
            face = (Math.Min(face.Item1, had.MinX), Math.Min(face.Item2, had.MinY), Math.Max(face.Item3, had.MaxX), Math.Max(face.Item4, had.MaxY));
        _faces[entry.Id] = face;
    }

    /// <summary>Where things on the tile of a model rest (fitted metres, the model unturned), once its body is built; empty for a model with no shelves.</summary>
    public System.Numerics.Vector3[] SlotsOf(ModelEntry entry)
        => _slots.TryGetValue(entry.Id, out System.Numerics.Vector3[] slots) ? slots : Array.Empty<System.Numerics.Vector3>();

    // The catalog's slots fitted as the model is, else the shelves found in the body.
    private static System.Numerics.Vector3[] Slots(ModelEntry entry, ModelMeshData body)
    {
        if (entry.Slots.Length == 0)
            return entry.Rule == ModelRule.Crate ? ModelMesh.Floor(body) : ModelMesh.Shelves(body);
        (float sx, float sy) = ModelMesh.Scale(entry, TileSpace.TileSize, WorldScale.WallHeight, WorldScale.TableTop);
        return entry.Slots.Select(v => new System.Numerics.Vector3(v.X * sx, v.Y * sy, v.Z * sx)).ToArray();
    }

    // The frame of the entry's own tg sheet a material shows (its image is the sheet byte for byte, its uv in one frame of it), or of the sheet the entry names
    // as its picture (ModelEntry.FrameSheet); null for any other.
    // pending: a hash it needs is still being worked out on the thread pool (in play); it is asked again when that is done.
    private ModelFrame FrameOf(ModelEntry entry, GlbFile file, ModelPart part, int index, GlbMaterial material, ref bool pending)
    {
        if (material == null || material.Image < 0 || material.Image >= file.Images.Count || file.Images[material.Image] == null)
            return null;
        byte[] png = file.Images[material.Image];
        foreach ((string icon, bool own) in entry.Icons.Select(i => (i, true)).Append((entry.FrameSheet, false)))
        {
            if (icon == null || !TrySheet(icon, out (byte[] Png, IconSheet Sheet) sheet))
                continue;
            // only a sheet of the image's own size can be it (the pngs' headers): the pixels, a decode and a hash each, are compared for those alone
            if (PngSize(sheet.Png) != PngSize(png))
                continue;
            string hash = PixelHash(png), sheetHash = PixelHash(sheet.Png);
            if (hash == null || sheetHash == null)
            {
                pending = true;
                return null;
            }
            if (sheetHash != hash)
                continue;
            (int width, int height) = ModelMesh.SheetSize(file, material);
            return ModelFrame.Of(material, ModelMesh.UvsOf(file, part, index), sheet.Sheet, width, height, own);
        }
        return null;
    }

    // A png's size from its header (IHDR: width and height, big-endian, at bytes 16 and 20).
    private static (int Width, int Height) PngSize(byte[] png)
        => png.Length < 24 ? (-1, -1) : ((png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19], (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23]);

    // An image's pixel hash (ModelFrame.ImageHash: a decode and a SHA-256 of its pixels), kept. In play it is worked out on the thread pool: null until then.
    private string PixelHash(byte[] png)
    {
        if (_frameHashes.TryGetValue(png, out string hash))
            return hash;
        if (_hashing.TryGetValue(png, out Task<string> task) && (task.IsCompleted || WaitForTextures))
        {
            _hashing.Remove(png);
            return _frameHashes[png] = task.GetAwaiter().GetResult();
        }
        if (WaitForTextures)
            return _frameHashes[png] = ModelFrame.ImageHash(png);
        _hashing.TryAdd(png, Counted(Task.Run(() => ModelFrame.ImageHash(png))));
        return null;
    }

    // A converted tg icon's sheet (its png and its description), read once; false for a GAGS key or an icon not converted.
    private bool TrySheet(string icon, out (byte[] Png, IconSheet Sheet) sheet)
    {
        if (_sheets.TryGetValue(icon, out sheet))
            return sheet.Sheet != null;
        sheet = (null, null);
        if (icon.EndsWith(".dmi", StringComparison.Ordinal) && !icon.Contains("..", StringComparison.Ordinal))
        {
            string stem = Path.Combine(IconPaths.Root, icon[..^4]);
            try
            {
                if (AssetPreload.TryIconBytes(icon[..^4] + ".png", out byte[] png) && AssetPreload.TryIconBytes(icon[..^4] + ".json", out byte[] json))
                    sheet = (png, IconSheet.Parse(System.Text.Encoding.UTF8.GetString(json), null));
                else if (File.Exists(stem + ".png") && File.Exists(stem + ".json"))
                    sheet = (File.ReadAllBytes(stem + ".png"), IconSheet.Parse(File.ReadAllText(stem + ".json"), null));
            }
            catch (Exception e) when (e is IOException or InvalidDataException)
            {
                GD.PushWarning($"ModelLibrary: the sheet of {icon} is not read ({e.Message}); its models keep their frames still.");
            }
        }
        _sheets[icon] = sheet;
        return sheet.Sheet != null;
    }

    // An entry's image: a tg icon's converted sheet (".dmi"), else a PNG in the models folder.
    // ModelEntry.SpriteTop: the uv rectangle (glTF's v down) of tg's junction state in the image a table's material shows: the entry's image's sheet, or its own
    // icon's sheet when the file's embedded image is that sheet. Null for a junction tg has no state for, or an image that is no converted sheet.
    private Func<int, System.Numerics.Vector4?> TopCells(ModelEntry entry, byte[] image)
    {
        string icon = entry.Image ?? entry.Icons.FirstOrDefault();
        if (image == null || image.Length < 24 || icon == null || !TrySheet(icon, out (byte[] Png, IconSheet Sheet) sheet))
            return null;
        // as before (2026-10-29): the sheet's pixel hash was compared with the embedded image's byte hash here, which never matched, so only an
        // entry with its own image gets its junction's cells
        if (entry.Image == null)
            return null;
        // the png's own size (its IHDR: width and height, big-endian, at bytes 16 and 20)
        float width = (image[16] << 24) | (image[17] << 16) | (image[18] << 8) | image[19];
        float height = (image[20] << 24) | (image[21] << 16) | (image[22] << 8) | image[23];
        return junction =>
        {
            IconState state = sheet.Sheet.States.FirstOrDefault(s => s.Name.EndsWith("-" + junction, StringComparison.Ordinal) && s.Rects.Length > 0);
            if (state == null)
                return null;
            (int x, int y, int w, int h) = state.Rects[0];
            return new System.Numerics.Vector4(x / width, y / height, (x + w) / width, (y + h) / height);
        };
    }

    private byte[] ImageBytes(string image)
    {
        if (_images.TryGetValue(image, out byte[] bytes))
            return bytes;
        bytes = image.EndsWith(".dmi", StringComparison.Ordinal)
            ? AssetPreload.TryIconBytes(Inside(image)[..^4] + ".png", out byte[] sheet) ? sheet : File.ReadAllBytes(Path.Combine(IconPaths.Root, Inside(image)[..^4] + ".png"))
            : AssetPreload.TryModelImage(Inside(image), out byte[] own) && _root == Root ? own : File.ReadAllBytes(Path.Combine(_root, Inside(image)));
        _images[image] = bytes;
        return bytes;
    }

    // The mesh of one material of a part, built once whatever the tint (TryPieces).
    private (int Mesh, Vector3 Pivot) PieceMesh(GlbFile file, ModelEntry entry, string part, ModelPart spec, int index, ModelFrame frame, int junction, byte[] image,
        System.Numerics.Vector4 color)
    {
        var key = (entry.Id, part, index, junction);
        if (_pieceMeshes.TryGetValue(key, out var known))
            return known;
        long start = Stopwatch.GetTimestamp();
        ModelMeshData data = ModelMesh.Build(file, entry, spec, TileSpace.TileSize, WorldScale.WallHeight, WorldScale.TableTop, index, frame, junction,
            entry.SpriteTop && frame == null ? TopCells(entry, image) : null);
        Anomaly.Client.Dev.FrameProbe.Item("mesh", entry.Id + "/" + part, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        int mesh = _meshes.Count;
        long made = Stopwatch.GetTimestamp();
        _meshes.Add(ToMesh(data));
        if (data.PickBoxes != null)
            _pickBoxes[mesh] = data.PickBoxes;
        Anomaly.Client.Dev.FrameProbe.Item("arraymesh", entry.Id + "/" + part, Stopwatch.GetElapsedTime(made).TotalMilliseconds);
        if (spec.Shader != null)
            _materials[mesh] = ShaderMaterialOf(spec, spec.Color is System.Numerics.Vector3 c ? new System.Numerics.Vector4(Linear(c.X), Linear(c.Y), Linear(c.Z), color.W) : color);
        if (spec.Shader == ModelPart.CrystalShader)
            _hulls[mesh] = ToMesh(new ModelMeshData
            {
                Positions = data.Positions, Normals = ModelMesh.SmoothNormals(data.Positions, data.Normals), Uvs = data.Uvs, Uv2 = data.Uv2, Indices = data.Indices,
            });
        float top = data.Positions.Max(v => v.Y) + data.Pivot.Y;
        _tops[entry.Id] = Math.Max(top, _tops.TryGetValue(entry.Id, out float seen) ? seen : 0f);
        NoteLieTop(entry.Id, data);
        NoteFront(entry, data);
        if (entry.RestOnShelves && !_slots.ContainsKey(entry.Id))
            _slots[entry.Id] = entry.Slots.Length > 0 ? Slots(entry, data) : ModelMesh.AllLevels(data);
        var built = (mesh, new Vector3(data.Pivot.X, data.Pivot.Y, data.Pivot.Z));
        _pieceMeshes[key] = built;
        return built;
    }

    private static string Inside(string relative)
        => relative.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)
            ? throw new InvalidDataException("a model file must be inside the models folder: " + relative)
            : relative;

    private GlbFile ReadFile(string relative)
    {
        if (_files.TryGetValue(relative, out GlbFile file))
            return file;
        long start = Stopwatch.GetTimestamp();
        if (_root != Root || !AssetPreload.TryGlb(Inside(relative), out file))
        {
            file = GlbFile.Read(System.IO.File.ReadAllBytes(Path.Combine(_root, Inside(relative))), uri => GlbFile.TgIcon(uri) is string icon ? ImageBytes(icon) : null);
            AssetPreload.Miss("model", relative, Stopwatch.GetElapsedTime(start).TotalMilliseconds, blocked: true);
        }
        Anomaly.Client.Dev.FrameProbe.Item("glb", relative, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        _files[relative] = file;
        return file;
    }

    // One atlas entry per distinct image (and tint): the kit's modules each embed the same atlas.
    private int Picture(byte[] png, uint tint = 0xFFFFFFFFu, ModelFrame frame = null, bool opaque = false, bool fill = false)
    {
        if (!_pngHashes.TryGetValue(png, out string pngHash))
            _pngHashes[png] = pngHash = Convert.ToHexString(SHA256.HashData(png));
        string hash = pngHash + (tint == 0xFFFFFFFFu ? string.Empty : "*" + tint.ToString("X8"))
                      + (frame == null ? string.Empty : $"@{frame.X},{frame.Y},{frame.Width},{frame.Height}") + (opaque ? fill ? "#filled" : "#opaque" : string.Empty);
        if (_pictures.TryGetValue(hash, out int id))
            return id;
        long start = Stopwatch.GetTimestamp();
        try
        {
            return NewPicture(png, tint, frame, hash, opaque, fill);
        }
        finally
        {
            Anomaly.Client.Dev.FrameProbe.Item("texture", hash[..8], Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
    }

    private int NewPicture(byte[] png, uint tint, ModelFrame frame, string hash, bool opaque, bool fill)
    {
        // a texture the warm-up decodes is waited for, not decoded again; else the last one decoded is kept for the next frame or tint cut from it.
        // shortcut: only the last texture is kept (a 2k one is 16 MB); keep more if families start to interleave their textures.
        if (_decoding.TryGetValue(png, out var decoding))
        {
            if (!decoding.Task.IsCompleted && !WaitForTextures)
                return TexturePending;
            (_lastPng, _lastDecoded) = (png, decoding.Task.GetAwaiter().GetResult());
            if (decoding.Users == 0)
            {
                _decoding.Remove(png); // a build's own decode (no warm-up part waits for it): the last raster keeps it for the next frame or tint
                _ownDecodes.Remove(png);
            }
        }
        else if (!ReferenceEquals(png, _lastPng))
        {
            if (!WaitForTextures)
            {
                _decoding[png] = (Decode(png), 0); // decoded on the thread pool, not in the frame (a 1-2k texture: 20-60 ms)
                _ownDecodes[png] = Stopwatch.GetTimestamp();
                return TexturePending;
            }
            (_lastPng, _lastDecoded) = (png, AssetPreload.TryDecoded(png, out Raster kept) ? kept : PngDecoder.Decode(png));
        }
        // the atlas copies the picture: the decoded raster itself is cut from or laid in, copied first only when it is tinted
        Raster raster = frame != null ? _lastDecoded.Crop(frame.X, frame.Y, frame.Width, frame.Height) : tint != 0xFFFFFFFFu || opaque ? _lastDecoded.Clone() : _lastDecoded;
        if (tint != 0xFFFFFFFFu)
            raster.Multiply(tint);
        if (opaque && fill)
            raster.FillTransparent();
        if (opaque)
            for (int i = 3; i < raster.Pixels.Length; i += 4)
                raster.Pixels[i] = 255; // glTF's OPAQUE, as Blender draws it: the colour under a transparent texel shows
        int id = _atlas.AddImage(raster);
        _pictures[hash] = id;
        return id;
    }

    private static ArrayMesh ToMesh(ModelMeshData data)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = data.Positions.Select(v => new Vector3(v.X, v.Y, v.Z)).ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = data.Normals.Select(v => new Vector3(v.X, v.Y, v.Z)).ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = data.Uvs.Select(v => new Vector2(v.X, v.Y)).ToArray();
        arrays[(int)Mesh.ArrayType.TexUV2] = data.Uv2.Select(v => new Vector2(v.X, v.Y)).ToArray();
        arrays[(int)Mesh.ArrayType.Index] = data.Indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}

/// <summary>
/// One piece of a model with its own textures (<see cref="ModelLibrary.TryPieces"/>): its mesh, its picture in the prop atlas, its pivot, and the frame of the
/// thing's own sheet it shows (null: its picture as made) and its material's name; <c>Shaded</c>: a flat-colour piece the shader shades by its normals.
/// </summary>
public sealed record ModelPiece(int Mesh, int Picture, Vector3 Pivot, ModelFrame Frame, string Material, float Glow = 1f, bool Shaded = false, bool Emissive = false);
