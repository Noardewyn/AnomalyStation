using System.IO;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

// DEVIATION(DV386): the AI camera net's static. tg puts a static image (icons/effects/cameravis.dmi's animated state, SScameras'
// obscured_images) on every tile of the camera chunks around a camera eye that no camera sees, in the viewer's client.images, on the
// camera static plane over everything else. The server sends each chunk's obscured tiles as a mask (protocol version 78); the client
// fills each obscured tile, floor to ceiling, with a box showing the same animated static, so nothing on it can be seen from the eye's
// orbit. The boxes are rebuilt when the masks change.

/// <summary>The static over the tiles the own camera eye's cameras do not see (<see cref="ReplicaWorld.StaticChunks"/>).</summary>
public partial class CameraStatic : MultiMeshInstance3D
{
    private const string ShaderCode = @"
shader_type spatial;
render_mode unshaded, cull_back, depth_draw_opaque;
uniform sampler2D sheet : filter_nearest, repeat_disable;
uniform float tile_size = 2.0;
varying vec3 world_pos;
varying vec3 world_normal;
void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}
void fragment() {
    vec3 n = abs(world_normal);
    vec2 p = n.y > 0.5 ? world_pos.xz : (n.x > 0.5 ? world_pos.zy : world_pos.xy);
    vec2 f = fract(p / tile_size);
    // cameravis.dmi's static: 7 frames of 32 x 32 in a 96 x 96 sheet, a tick (0.1 s) each, looping
    int frame = int(mod(floor(TIME * 10.0), 7.0));
    vec2 cell = vec2(float(frame % 3), float(frame / 3));
    vec4 c = texture(sheet, (cell + vec2(f.x, 1.0 - f.y)) / 3.0);
    ALBEDO = c.rgb;
}
";

    private ReplicaWorld _replica;
    private int _builtVersion = -1;
    private readonly MultiMesh _multi = new() { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D };

    public override void _Ready()
    {
        var material = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
        material.SetShaderParameter("tile_size", TileSpace.TileSize);
        Texture2D sheet = LoadSheet();
        if (sheet != null)
            material.SetShaderParameter("sheet", sheet);
        // a box a hair over the tile and the level, so a wall's faces do not fight the static's
        _multi.Mesh = new BoxMesh { Size = new Vector3(TileSpace.TileSize * 1.01f, TileSpace.LevelHeight * 1.01f, TileSpace.TileSize * 1.01f), Material = material };
        Multimesh = _multi;
        CastShadow = ShadowCastingSetting.Off;
    }

    /// <summary>Reads the world's static masks from now on.</summary>
    public void Bind(ReplicaWorld replica)
    {
        _replica = replica;
        _builtVersion = -1;
    }

    public override void _Process(double delta)
    {
        if (_replica == null || _replica.StaticVersion == _builtVersion)
            return;
        _builtVersion = _replica.StaticVersion;
        int count = 0;
        foreach (ulong mask in _replica.StaticChunks.Values)
            count += System.Numerics.BitOperations.PopCount(mask);
        _multi.InstanceCount = count;
        int i = 0;
        foreach (KeyValuePair<(int X, int Y, int Z), ulong> chunk in _replica.StaticChunks)
        {
            ulong mask = chunk.Value;
            while (mask != 0)
            {
                int bit = System.Numerics.BitOperations.TrailingZeroCount(mask);
                mask &= mask - 1;
                int x = chunk.Key.X + bit % 8, y = chunk.Key.Y + bit / 8;
                var origin = new Vector3(TileSpace.WorldX(x), TileSpace.WorldY(chunk.Key.Z) + TileSpace.LevelHeight * 0.5f, TileSpace.WorldZ(y));
                _multi.SetInstanceTransform(i++, new Transform3D(Basis.Identity, origin));
            }
        }
        Visible = count > 0;
    }

    private static Texture2D LoadSheet()
    {
        string path = Path.Combine(IconPaths.Root, "icons", "effects", "cameravis.png");
        if (!File.Exists(path))
            return null;
        var image = new Image();
        if (image.LoadPngFromBuffer(File.ReadAllBytes(path)) != Error.Ok)
            return null;
        return ImageTexture.CreateFromImage(image);
    }
}
