using System.Linq;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Anomaly.Client.Shared.Models;

// Engine-free. The binary glTF (.glb) reader of the 3D models (docs/ai/specs/models.md): the client reads the model files itself, as it reads tg's converted icons,
// so that Godot never imports or recompresses them. It reads what the models use: the node tree (TRS or matrix), triangle meshes with positions, normals, the
// first uv set and indices, the materials (their name, alpha mode, base colour and texture) and the images (PNG bytes): embedded, or tg's converted sheets by uri;
// and the animations' channels (the first-person held swings, HeldClip).

/// <summary>One triangle list of a mesh: positions and normals in the node's space, the first uv set (glTF's: v runs down the image), indices.</summary>
public sealed class GlbPrimitive
{
    public Vector3[] Positions = Array.Empty<Vector3>();
    public Vector3[] Normals = Array.Empty<Vector3>();
    public Vector2[] Uvs = Array.Empty<Vector2>();
    public int[] Indices = Array.Empty<int>();

    /// <summary>The material index (-1: none).</summary>
    public int Material = -1;
}

/// <summary>A material: its name, whether it blends (glTF's <c>alphaMode</c> BLEND: glass), its base colour and the image its base colour texture shows (-1: none).</summary>
public sealed class GlbMaterial
{
    public string Name = string.Empty;
    public bool Blend;
    public Vector4 BaseColor = Vector4.One;
    public int Image = -1;

    /// <summary>
    /// How strongly a light layer glows (0..1): glTF's emissive factor times <c>KHR_materials_emissive_strength</c> over the models' full 0.4 (the art's light
    /// masks are tg's mask alpha times 0.4: a button's soft halo); 1 without an emissive factor, 0 for glass without one.
    /// </summary>
    public float Glow = 1f;

    /// <summary>Explicit glTF emission also lights materials whose names do not contain Overlay or Emission.</summary>
    public bool Emissive;

    /// <summary>glTF's <c>KHR_texture_transform</c> of the base colour texture (uv' = uv * scale + offset; a model whose uv are local to one tile selects it so).</summary>
    public Vector2 UvOffset = Vector2.Zero, UvScale = Vector2.One;
}

/// <summary>A node that holds a mesh, with its transform from the file's root (System.Numerics' row-vector convention: <c>Vector3.Transform(v, World)</c>).</summary>
public sealed class GlbMeshNode
{
    public string Name = string.Empty;
    public Matrix4x4 World = Matrix4x4.Identity;

    /// <summary>The node's own scale (its rest pose's; a zero one read as 1, <see cref="GlbReader"/>).</summary>
    public Vector3 Scale = Vector3.One;

    public readonly List<GlbPrimitive> Primitives = new();
}

/// <summary>One animated property of a node: its keys' times (seconds) and values (3 floats for a translation or a scale, 4 for a rotation's x, y, z, w).</summary>
public sealed class GlbChannel
{
    public string Node = string.Empty;

    /// <summary>glTF's target path: <c>translation</c>, <c>rotation</c>, <c>scale</c> or <c>weights</c>.</summary>
    public string Path = string.Empty;

    /// <summary>glTF's interpolation: <c>LINEAR</c>, <c>STEP</c> or <c>CUBICSPLINE</c> (whose values hold an in-tangent, the value and an out-tangent per key).</summary>
    public string Interpolation = "LINEAR";

    public float[] Times = Array.Empty<float>();
    public float[][] Values = Array.Empty<float[]>();
}

/// <summary>A named animation: its channels.</summary>
public sealed class GlbAnimation
{
    public string Name = string.Empty;
    public readonly List<GlbChannel> Channels = new();
}

/// <summary>A read .glb file.</summary>
public sealed class GlbFile
{
    public readonly List<GlbAnimation> Animations = new();

    public readonly List<GlbMeshNode> MeshNodes = new();
    public readonly List<GlbMaterial> Materials = new();

    /// <summary>The attach points the artist put in the file: nodes named <c>Attach_&lt;name&gt;</c> (an empty, or any node), with their transforms (<see cref="AttachPoints"/>).</summary>
    public readonly List<(string Name, Matrix4x4 World)> AttachNodes = new();

    /// <summary>The images' bytes (PNG), by image index; null for one that is not in the binary chunk and that <see cref="Read"/>'s <c>external</c> did not give.</summary>
    public readonly List<byte[]> Images = new();

    private const uint Magic = 0x46546C67; // "glTF"
    private const uint JsonChunk = 0x4E4F534A, BinChunk = 0x004E4942;

    /// <summary>The tg icon (<c>icons/obj/x.dmi</c>) an image's uri shows (Blender's export of a model on tg's sheet: <c>../../../tg/icons/obj/x.png</c>); null for any other uri.</summary>
    public static string TgIcon(string uri)
    {
        int at = uri.IndexOf("tg/icons/", StringComparison.Ordinal);
        return at >= 0 && uri.EndsWith(".png", StringComparison.Ordinal) ? uri[(at + 3)..^4] + ".dmi" : null;
    }

    /// <summary>Reads a .glb; <paramref name="external"/> gives the bytes of an image by its uri. Throws <see cref="InvalidDataException"/> on a file it cannot read.</summary>
    public static GlbFile Read(byte[] glb, Func<string, byte[]> external = null)
    {
        if (glb == null || glb.Length < 20 || BitConverter.ToUInt32(glb, 0) != Magic)
            throw new InvalidDataException("not a binary glTF file");
        int offset = 12;
        string json = null;
        ReadOnlyMemory<byte> bin = ReadOnlyMemory<byte>.Empty;
        while (offset + 8 <= glb.Length)
        {
            int length = BitConverter.ToInt32(glb, offset);
            uint type = BitConverter.ToUInt32(glb, offset + 4);
            if (length < 0 || offset + 8 + length > glb.Length)
                throw new InvalidDataException("a chunk runs past the end of the file");
            if (type == JsonChunk)
                json = Encoding.UTF8.GetString(glb, offset + 8, length);
            else if (type == BinChunk)
                bin = new ReadOnlyMemory<byte>(glb, offset + 8, length);
            offset += 8 + length;
        }
        if (json == null)
            throw new InvalidDataException("no JSON chunk");
        using JsonDocument document = JsonDocument.Parse(json);
        var file = new GlbFile();
        new Parser(document.RootElement, bin, file, external).Run();
        return file;
    }

    private sealed class Parser
    {
        private readonly JsonElement _root;
        private readonly ReadOnlyMemory<byte> _bin;
        private readonly GlbFile _file;
        private readonly Func<string, byte[]> _external;

        public Parser(JsonElement root, ReadOnlyMemory<byte> bin, GlbFile file, Func<string, byte[]> external)
        {
            _external = external;
            _root = root;
            _bin = bin;
            _file = file;
        }

        public void Run()
        {
            ReadImages();
            ReadMaterials();
            if (!_root.TryGetProperty("nodes", out JsonElement nodes))
                return;
            ReadAnimations(nodes);
            // The roots: the default scene's nodes, else every node that is no child.
            var roots = new List<int>();
            if (_root.TryGetProperty("scenes", out JsonElement scenes) && scenes.GetArrayLength() > 0)
            {
                int scene = _root.TryGetProperty("scene", out JsonElement s) ? s.GetInt32() : 0;
                if (scenes[scene].TryGetProperty("nodes", out JsonElement sceneNodes))
                    roots.AddRange(sceneNodes.EnumerateArray().Select(n => n.GetInt32()));
            }
            else
            {
                var children = new HashSet<int>();
                foreach (JsonElement node in nodes.EnumerateArray())
                {
                    if (node.TryGetProperty("children", out JsonElement c))
                        children.UnionWith(c.EnumerateArray().Select(n => n.GetInt32()));
                }
                roots.AddRange(Enumerable.Range(0, nodes.GetArrayLength()).Where(i => !children.Contains(i)));
            }
            foreach (int root in roots)
                Walk(nodes, root, Matrix4x4.Identity, 0);
        }

        private void Walk(JsonElement nodes, int index, Matrix4x4 parent, int depth)
        {
            if (depth > 64)
                throw new InvalidDataException("the node tree is too deep");
            JsonElement node = nodes[index];
            Matrix4x4 world = Local(node) * parent;
            string name = node.TryGetProperty("name", out JsonElement named) ? named.GetString() ?? string.Empty : string.Empty;
            if (name.StartsWith(AttachPoints.NodePrefix, StringComparison.Ordinal))
                _file.AttachNodes.Add((name.Substring(AttachPoints.NodePrefix.Length), world));
            if (node.TryGetProperty("mesh", out JsonElement mesh))
            {
                var meshNode = new GlbMeshNode { Name = node.TryGetProperty("name", out JsonElement n) ? n.GetString() ?? string.Empty : string.Empty, World = world, Scale = NodeScale(node) };
                foreach (JsonElement primitive in _root.GetProperty("meshes")[mesh.GetInt32()].GetProperty("primitives").EnumerateArray())
                {
                    GlbPrimitive p = ReadPrimitive(primitive);
                    if (p != null)
                        meshNode.Primitives.Add(p);
                }
                _file.MeshNodes.Add(meshNode);
            }
            if (node.TryGetProperty("children", out JsonElement children))
            {
                foreach (JsonElement child in children.EnumerateArray())
                    Walk(nodes, child.GetInt32(), world, depth + 1);
            }
        }

        private static Matrix4x4 Local(JsonElement node)
        {
            if (node.TryGetProperty("matrix", out JsonElement m))
            {
                // glTF's matrix is column-major for column vectors: read as rows it is the row-vector matrix System.Numerics uses.
                float[] v = m.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                return new Matrix4x4(v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8], v[9], v[10], v[11], v[12], v[13], v[14], v[15]);
            }
            Vector3 t = node.TryGetProperty("translation", out JsonElement te) ? V3(te) : Vector3.Zero;
            Quaternion r = node.TryGetProperty("rotation", out JsonElement re) ? new Quaternion(re[0].GetSingle(), re[1].GetSingle(), re[2].GetSingle(), re[3].GetSingle()) : Quaternion.Identity;
            return Matrix4x4.CreateScale(NodeScale(node)) * Matrix4x4.CreateFromQuaternion(r) * Matrix4x4.CreateTranslation(t);
        }

        // A zero scale is how the wave models' exporter hides one state's mesh in the rest pose (its clips show it): the catalog chooses the state, so it is 1.
        // The required models hide theirs at 1e-5 instead: any scale under HiddenScale is that.
        private static Vector3 NodeScale(JsonElement node)
        {
            Vector3 s = node.TryGetProperty("scale", out JsonElement se) ? V3(se) : Vector3.One;
            return MathF.Abs(s.X) < HiddenScale || MathF.Abs(s.Y) < HiddenScale || MathF.Abs(s.Z) < HiddenScale ? Vector3.One : s;
        }

        private const float HiddenScale = 1e-3f;

        private static Vector3 V3(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());

        private GlbPrimitive ReadPrimitive(JsonElement primitive)
        {
            int mode = primitive.TryGetProperty("mode", out JsonElement m) ? m.GetInt32() : 4;
            if (mode != 4)
                return null; // triangles only
            JsonElement attributes = primitive.GetProperty("attributes");
            if (!attributes.TryGetProperty("POSITION", out JsonElement position))
                return null;
            var p = new GlbPrimitive
            {
                Positions = Floats(position.GetInt32(), 3).Select(f => new Vector3(f[0], f[1], f[2])).ToArray(),
                Material = primitive.TryGetProperty("material", out JsonElement mat) ? mat.GetInt32() : -1,
            };
            p.Normals = attributes.TryGetProperty("NORMAL", out JsonElement normal)
                ? Floats(normal.GetInt32(), 3).Select(f => new Vector3(f[0], f[1], f[2])).ToArray()
                : new Vector3[p.Positions.Length];
            p.Uvs = attributes.TryGetProperty("TEXCOORD_0", out JsonElement uv)
                ? Floats(uv.GetInt32(), 2).Select(f => new Vector2(f[0], f[1])).ToArray()
                : new Vector2[p.Positions.Length];
            p.Indices = primitive.TryGetProperty("indices", out JsonElement indices)
                ? Ints(indices.GetInt32())
                : Enumerable.Range(0, p.Positions.Length).ToArray();
            if (!attributes.TryGetProperty("NORMAL", out _))
            {
                // glTF specifies flat face normals when NORMAL is absent. Split shared corners at each triangle.
                Vector3[] positions = p.Indices.Select(i => p.Positions[i]).ToArray();
                Vector2[] uvs = p.Indices.Select(i => p.Uvs[i]).ToArray();
                p.Normals = new Vector3[positions.Length];
                for (int i = 0; i + 2 < positions.Length; i += 3)
                {
                    Vector3 face = Vector3.Cross(positions[i + 1] - positions[i], positions[i + 2] - positions[i]);
                    face = face.LengthSquared() > 1e-12f ? Vector3.Normalize(face) : Vector3.UnitY;
                    p.Normals[i] = p.Normals[i + 1] = p.Normals[i + 2] = face;
                }
                p.Positions = positions;
                p.Uvs = uvs;
                p.Indices = Enumerable.Range(0, positions.Length).ToArray();
            }
            return p;
        }

        // The bytes of an accessor's elements: (start of the first, the stride, the count, the component type).
        private (int Start, int Stride, int Count, int Component) View(int accessorIndex, int components)
        {
            JsonElement accessor = _root.GetProperty("accessors")[accessorIndex];
            int count = accessor.GetProperty("count").GetInt32();
            int component = accessor.GetProperty("componentType").GetInt32();
            if (!accessor.TryGetProperty("bufferView", out JsonElement viewIndex))
                throw new InvalidDataException("a sparse or empty accessor is not supported");
            JsonElement view = _root.GetProperty("bufferViews")[viewIndex.GetInt32()];
            if (view.TryGetProperty("buffer", out JsonElement buffer) && buffer.GetInt32() != 0)
                throw new InvalidDataException("only the binary chunk's buffer is supported");
            int start = (view.TryGetProperty("byteOffset", out JsonElement vo) ? vo.GetInt32() : 0) + (accessor.TryGetProperty("byteOffset", out JsonElement ao) ? ao.GetInt32() : 0);
            int size = component switch { 5126 or 5125 => 4, 5123 or 5122 => 2, 5121 or 5120 => 1, _ => throw new InvalidDataException("component type " + component) };
            int stride = view.TryGetProperty("byteStride", out JsonElement bs) ? bs.GetInt32() : size * components;
            if (count > 0 && start + stride * (count - 1) + size * components > _bin.Length)
                throw new InvalidDataException("an accessor runs past the binary chunk");
            return (start, stride, count, component);
        }

        private float[][] Floats(int accessorIndex, int components)
        {
            (int start, int stride, int count, int component) = View(accessorIndex, components);
            if (component != 5126)
                throw new InvalidDataException("a vertex attribute that is not float is not supported");
            ReadOnlySpan<byte> bytes = _bin.Span;
            var result = new float[count][];
            for (int i = 0; i < count; i++)
            {
                var f = new float[components];
                for (int k = 0; k < components; k++)
                    f[k] = BitConverter.ToSingle(bytes.Slice(start + i * stride + k * 4, 4));
                result[i] = f;
            }
            return result;
        }

        private int[] Ints(int accessorIndex)
        {
            (int start, int stride, int count, int component) = View(accessorIndex, 1);
            ReadOnlySpan<byte> bytes = _bin.Span;
            var result = new int[count];
            for (int i = 0; i < count; i++)
            {
                int at = start + i * stride;
                result[i] = component switch
                {
                    5125 => (int)BitConverter.ToUInt32(bytes.Slice(at, 4)),
                    5123 => BitConverter.ToUInt16(bytes.Slice(at, 2)),
                    5121 => bytes[at],
                    _ => throw new InvalidDataException("index component type " + component),
                };
            }
            return result;
        }

        private void ReadAnimations(JsonElement nodes)
        {
            if (!_root.TryGetProperty("animations", out JsonElement animations))
                return;
            foreach (JsonElement animation in animations.EnumerateArray())
            {
                var read = new GlbAnimation { Name = animation.TryGetProperty("name", out JsonElement n) ? n.GetString() ?? string.Empty : string.Empty };
                JsonElement samplers = animation.GetProperty("samplers");
                foreach (JsonElement channel in animation.GetProperty("channels").EnumerateArray())
                {
                    JsonElement target = channel.GetProperty("target");
                    if (!target.TryGetProperty("node", out JsonElement node))
                        continue;
                    JsonElement sampler = samplers[channel.GetProperty("sampler").GetInt32()];
                    string path = target.GetProperty("path").GetString() ?? string.Empty;
                    JsonElement nodeElement = nodes[node.GetInt32()];
                    read.Channels.Add(new GlbChannel
                    {
                        Node = nodeElement.TryGetProperty("name", out JsonElement nodeName) ? nodeName.GetString() ?? string.Empty : string.Empty,
                        Path = path,
                        Interpolation = sampler.TryGetProperty("interpolation", out JsonElement i) ? i.GetString() ?? "LINEAR" : "LINEAR",
                        Times = Floats(sampler.GetProperty("input").GetInt32(), 1).Select(f => f[0]).ToArray(),
                        Values = Floats(sampler.GetProperty("output").GetInt32(), path == "rotation" ? 4 : 3),
                    });
                }
                _file.Animations.Add(read);
            }
        }

        private void ReadImages()
        {
            if (!_root.TryGetProperty("images", out JsonElement images))
                return;
            foreach (JsonElement image in images.EnumerateArray())
            {
                if (!image.TryGetProperty("bufferView", out JsonElement viewIndex))
                {
                    _file.Images.Add(image.TryGetProperty("uri", out JsonElement uri) ? _external?.Invoke(uri.GetString()) : null);
                    continue;
                }
                JsonElement view = _root.GetProperty("bufferViews")[viewIndex.GetInt32()];
                int start = view.TryGetProperty("byteOffset", out JsonElement vo) ? vo.GetInt32() : 0;
                int length = view.GetProperty("byteLength").GetInt32();
                if (start + length > _bin.Length)
                    throw new InvalidDataException("an image runs past the binary chunk");
                _file.Images.Add(_bin.Slice(start, length).ToArray());
            }
        }

        private void ReadMaterials()
        {
            if (!_root.TryGetProperty("materials", out JsonElement materials))
                return;
            foreach (JsonElement m in materials.EnumerateArray())
            {
                var material = new GlbMaterial
                {
                    Name = m.TryGetProperty("name", out JsonElement n) ? n.GetString() ?? string.Empty : string.Empty,
                    Blend = m.TryGetProperty("alphaMode", out JsonElement a) && a.GetString() == "BLEND",
                };
                if (material.Blend && !m.TryGetProperty("emissiveFactor", out _))
                    material.Glow = 0f; // a see-through layer without light (a pump's flow marker) is not a light layer
                if (m.TryGetProperty("emissiveFactor", out JsonElement emissive))
                {
                    float strength = m.TryGetProperty("extensions", out JsonElement mx) && mx.TryGetProperty("KHR_materials_emissive_strength", out JsonElement es)
                        && es.TryGetProperty("emissiveStrength", out JsonElement s) ? s.GetSingle() : 1f;
                    float most = Math.Max(emissive[0].GetSingle(), Math.Max(emissive[1].GetSingle(), emissive[2].GetSingle()));
                    material.Glow = Math.Clamp(most * strength / 0.4f, 0f, 1f);
                    material.Emissive = material.Glow > 0f;
                }
                if (m.TryGetProperty("pbrMetallicRoughness", out JsonElement pbr))
                {
                    if (pbr.TryGetProperty("baseColorFactor", out JsonElement f))
                        material.BaseColor = new Vector4(f[0].GetSingle(), f[1].GetSingle(), f[2].GetSingle(), f[3].GetSingle());
                    if (pbr.TryGetProperty("baseColorTexture", out JsonElement t) && _root.TryGetProperty("textures", out JsonElement textures))
                    {
                        JsonElement texture = textures[t.GetProperty("index").GetInt32()];
                        if (texture.TryGetProperty("source", out JsonElement source))
                            material.Image = source.GetInt32();
                        if (t.TryGetProperty("extensions", out JsonElement ext) && ext.TryGetProperty("KHR_texture_transform", out JsonElement transform))
                        {
                            if (transform.TryGetProperty("offset", out JsonElement o))
                                material.UvOffset = new Vector2(o[0].GetSingle(), o[1].GetSingle());
                            if (transform.TryGetProperty("scale", out JsonElement s))
                                material.UvScale = new Vector2(s[0].GetSingle(), s[1].GetSingle());
                        }
                    }
                }
                _file.Materials.Add(material);
            }
        }
    }
}
