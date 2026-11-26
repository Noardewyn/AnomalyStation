using System.Globalization;
using System.Linq;
using Godot;
using Anomaly.Client.Shared;

namespace Anomaly.Client.Render;

/// <summary>
/// The shaders' warm-up under the join's loading screen (docs/ai/specs/multiplayer.md, the client's hitches). The effects attach most of the game's
/// shaders as material overrides on their first event, which Godot's load-time pipeline precompilation does not cover, so their first sight
/// compiled pipelines in the frame. Its docs' advice is followed: draw each once behind a cover. Every spatial shader of <c>res://shaders</c> is
/// drawn on a small quad in front of the camera, the canvas ones on a small rectangle under the cover, the particle one on a particle system, and
/// the real effect nodes are made once (the shots, the flash, fire, smoke, the blast, foam, the hit sparks); all of it stays <see cref="Frames"/>
/// frames and goes. Godot keeps the compiled pipelines in its cache (<c>user://vulkan</c>) for the next start as well.
/// </summary>
public sealed class ShaderWarmUp
{
    private const string Folder = "res://shaders";
    private const int Frames = 3;

    private Node3D _root;
    private CanvasLayer _canvas;
    private int _frames;
    private string _before;

    public bool Done { get; private set; }

    /// <summary>Godot's pipeline compilations so far, by kind (Performance's monitors).</summary>
    public static string Pipelines()
    {
        static long M(Performance.Monitor m) => (long)Performance.GetMonitor(m);
        return string.Create(CultureInfo.InvariantCulture,
            $"canvas {M(Performance.Monitor.PipelineCompilationsCanvas)}, mesh {M(Performance.Monitor.PipelineCompilationsMesh)}, surface {M(Performance.Monitor.PipelineCompilationsSurface)}, draw {M(Performance.Monitor.PipelineCompilationsDraw)}, specialization {M(Performance.Monitor.PipelineCompilationsSpecialization)}");
    }

    /// <summary>Once a frame until <see cref="Done"/>: the first makes everything, the next frames draw it, then it is freed.</summary>
    public void Step(Node scene, Camera3D camera, int coverLayer)
    {
        if (Done)
            return;
        if (_root == null)
        {
            _before = Pipelines();
            Begin(scene, camera, coverLayer);
            return;
        }
        if (++_frames < Frames)
            return;
        _root.QueueFree();
        _canvas.QueueFree();
        Done = true;
        GD.Print($"[anomaly] shaders: warmed up; pipeline compilations before: {_before}; after: {Pipelines()}");
    }

    private void Begin(Node scene, Camera3D camera, int coverLayer)
    {
        // in front of the eye, small, so every quad is in the view and none covers another's pixels for long
        _root = new Node3D { Name = "ShaderWarmUp", Position = new Vector3(0, 0, -1.2f) };
        camera.AddChild(_root);
        _canvas = new CanvasLayer { Name = "ShaderWarmUp2D", Layer = coverLayer - 1 };
        scene.AddChild(_canvas);
        var quad = new QuadMesh { Size = new Vector2(0.04f, 0.04f) };
        int spatial = 0, canvas = 0;
        foreach (string file in DirAccess.GetFilesAt(Folder).Where(f => f.EndsWith(".gdshader", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            var shader = GD.Load<Shader>(Folder + "/" + file);
            if (shader == null)
                continue;
            var material = new ShaderMaterial { Shader = shader };
            switch (shader.GetMode())
            {
                case Shader.Mode.Spatial:
                    _root.AddChild(new MeshInstance3D { Mesh = quad, MaterialOverride = material, Position = new Vector3((spatial % 9 - 4) * 0.05f, (spatial / 9 - 2) * 0.05f, 0) });
                    spatial++;
                    break;
                case Shader.Mode.CanvasItem:
                    _canvas.AddChild(new ColorRect { Material = material, Size = new Vector2(8, 8), Position = new Vector2(8 + 10 * canvas++, 8), MouseFilter = Control.MouseFilterEnum.Ignore });
                    break;
                case Shader.Mode.Particles:
                    _root.AddChild(new GpuParticles3D { ProcessMaterial = material, DrawPass1 = quad, Amount = 4, Lifetime = 0.5f, Emitting = true });
                    break;
            }
        }
        // the effects as they are made in play, with their own meshes and materials
        foreach (ShotKind kind in Enum.GetValues<ShotKind>().Where(k => k != ShotKind.None))
            ShotVisual.Create(_root, kind);
        ShotFlash.Spawn(_root, _root.GlobalPosition, Colors.Orange, 0.3f, 2f, 1f, 1.0);
        FireVolume.Create(_root).Set(FireVolume.Stage.Medium, FireVolume.DefaultTint, 1f);
        SmokeVolume.Create(_root).Set(Colors.Gray, 0.8f, false);
        BlastVolume.Create(_root);
        FoamVolume.Create(_root, true);
        FoamVolume.Create(_root, false);
        GD.Print($"[anomaly] shaders: warming up {spatial} spatial, {canvas} canvas shaders and the effects under the loading screen");
    }
}
