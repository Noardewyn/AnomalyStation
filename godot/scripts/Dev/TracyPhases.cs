using Godot;

namespace Anomaly.Client.Dev;

/// <summary>
/// The engine's frame phases as Tracy zones (<see cref="Tracy"/>): the physics step, the process pass over every node and the renderer's
/// draw (between RenderingServer's <c>frame_pre_draw</c> and <c>frame_post_draw</c>, the present's wait included), so the time outside
/// the game's own zones shows where it went. Two nodes open and close each pass: one runs before every other node, one after.
/// </summary>
public partial class TracyPhases : Node
{
    private static Tracy.Zone _process, _physics, _render;
    private bool _first;

    public static void Install(Node root)
    {
        // the engine's own client zones these passes itself, and its zones would not nest with ones opened in one callback and closed in another
        if (!Tracy.On || Tracy.InEngine)
            return;
        root.AddChild(new TracyPhases { _first = true, ProcessPriority = int.MinValue, ProcessPhysicsPriority = int.MinValue, Name = "TracyPhasesFirst" });
        root.AddChild(new TracyPhases { _first = false, ProcessPriority = int.MaxValue, ProcessPhysicsPriority = int.MaxValue, Name = "TracyPhasesLast" });
        RenderingServer.FramePreDraw += () => _render = Tracy.Begin("Godot: render");
        RenderingServer.FramePostDraw += () => Tracy.End(_render);
    }

    public override void _Process(double delta)
    {
        if (_first)
            _process = Tracy.Begin("Godot: process");
        else
            Tracy.End(_process);
    }

    // the program's end, after its last frame (with no zone left open, the profiler's shutdown hands it the rest of the data and returns)
    public override void _ExitTree()
    {
        if (!_first)
            Tracy.Stop();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_first)
            _physics = Tracy.Begin("Godot: physics");
        else
            Tracy.End(_physics);
    }
}
