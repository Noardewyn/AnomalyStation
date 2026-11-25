using System.Diagnostics;
using System.Globalization;
using Godot;
using Anomaly.Client.Hud;
using Anomaly.Client.Render;
using Anomaly.Client.Shared;

namespace Anomaly.Client;

// The join's loading screen (docs/ai/specs/multiplayer.md, the client's hitches). The scene shows the connect screen's loading look over its world
// from _Ready on, builds the map over the next frames in steps of about BuildBudgetMs, so the bar moves and the window answers, and makes the rest of
// the scene when the build is done (FinishReady). The world is never shown half made: the cover stays until what the eye first sees is built
// (the things queued near it, the models warming up) and goes then, or after CoverTimeoutSeconds whatever is left.
public partial class GameScene
{
    // The frame time the map's build may take under the loading screen: long enough to finish soon, short enough for the window to answer.
    private const double BuildBudgetMs = 40;

    // The things queued near the eye (tiles) that are built before the world is shown, and the most the loading screen waits.
    private const double CoverNearTiles = 12;
    private const double CoverTimeoutSeconds = 30;

    // Frames drawn under the cover once everything is in, before it goes: the first frames after the build are long (the light's first pass).
    private const int CoverSettleFrames = 3;

    // The bar's shares: the connection fills it to LoadingCover.ConnectShare, the build to BuildEnd, the rest of the loading to the end.
    private const double BuildEnd = 0.8;

    private LoadingCover _cover;
    private IEnumerator<float> _building;
    private AppearanceTextures _buildTextures;
    private double _builtAt;
    private int _settleFrames;

    // _Ready, once the scene's settings are read: the cover goes up and the map's build starts.
    private void BeginLoading(AppearanceTextures textures)
    {
        App app = App.Instance;
        // the keys and the mouse wait for the scene (FinishReady turns them on)
        SetProcessInput(false);
        SetProcessUnhandledInput(false);
        var layer = new CanvasLayer { Name = "LoadingCover", Layer = 129 }; // over App's held picture of the editor's loading screen (128)
        _cover = app.HasEditor
            ? new LoadingCover(ClientArgs.EditorName, LoadingCover.EditorPicture)
            : new LoadingCover("Anomaly Station", LoadingCover.GamePicture);
        _cover.Progress = LoadingCover.ConnectShare;
        _cover.Status = "Building the scene...";
        layer.AddChild(_cover);
        AddChild(layer);
        _buildTextures = textures;
        _building = _map.BuildSteps(_feed.Map, _feed.Replica, textures);
    }

    // First in each frame: true while the map is built (the frame does nothing else). Once it is, the rest of the scene is made, and every frame
    // after that looks whether the cover may go.
    private bool StepBuild()
    {
        if (_building == null)
        {
            if (_cover != null && !_leaving)
                StepCover();
            return false;
        }
        if (_leaving)
            return true;
        Dev.FrameProbe.BeginFrame();
        Dev.FrameProbe.Record();
        long start = Stopwatch.GetTimestamp();
        bool more;
        using (Dev.FrameProbe.Scope("loading/build"))
            while ((more = _building.MoveNext()) && Stopwatch.GetElapsedTime(start).TotalMilliseconds < BuildBudgetMs)
            {
            }
        if (more)
        {
            _cover.Progress = LoadingCover.ConnectShare + (BuildEnd - LoadingCover.ConnectShare) * _building.Current;
            _cover.Status = _map.BuildPhase + "...";
            return true;
        }
        _building = null;
        FinishReady(_buildTextures);
        _buildTextures = null;
        _builtAt = App.NowSeconds;
        _cover.Progress = BuildEnd;
        _cover.Status = "Loading the world...";
        _entities.SpawnBudget = BuildBudgetMs; // under the cover the queued things are built as fast as the frame allows
        _map.WarmUpBudget = BuildBudgetMs;
        return true;
    }

    // After the build: the cover goes once the eye's surroundings are in.
    private void StepCover()
    {
        double now = App.NowSeconds;
        string waiting = CoverWaitsFor();
        if (waiting == null && ++_settleFrames > CoverSettleFrames)
        {
            Reveal("ready");
            return;
        }
        if (now - _builtAt > CoverTimeoutSeconds)
        {
            Reveal("timed out waiting for " + waiting);
            return;
        }
        if (waiting != null)
        {
            _settleFrames = 0;
            _cover.Status = waiting;
        }
        _cover.Progress = BuildEnd + (1 - BuildEnd) * Math.Min(0.95, (now - _builtAt) / ((now - _builtAt) + 1.5));
    }

    // What the cover still waits for (its status line), or null for nothing.
    private string CoverWaitsFor()
    {
        App app = App.Instance;
        if (app.Editing)
            return null; // the map editor has no player: its world is shown once built
        if (!_ownSeen && !InLobby)
            return "Joining the round...";
        RenderPose pose = default;
        if (_ownSeen && _feed.Replica.TryGet(_feed.OwnEntity, out var own) && _session.Poses.TryGetPose(own, out pose)
            && _entities.PendingNear(pose.X, pose.Y, CoverNearTiles) > 0)
            return "Placing the things around you...";
        if (_map.WarmUpPending > 0)
            return "Preparing the models...";
        return null;
    }

    private void Reveal(string why)
    {
        App app = App.Instance;
        double now = App.NowSeconds;
        _cover.GetParent().QueueFree();
        _cover = null;
        _entities.SpawnBudget = EntityLayer.SpawnBudgetMs;
        _map.WarmUpBudget = MapRenderer.WarmUpBudgetMs;
        // the script's clock and --screenshot-after count from here, as they counted from the scene's start when the map was built in _Ready
        double covered = now - _startedAt;
        _scriptBase += covered;
        if (app.HasEditor)
            app.ScriptStartedAt = _scriptBase;
        _startedAt = now;
        if (app.Args.Bench)
            _bench = new BenchRunner(app.Args, _feed, _map, _rig, GetViewport(), _light, _map.LoadSeconds, _options.CustomGraphics);
        app.GameStarted();
        Dev.FrameProbe.Restart();
        GD.Print(string.Create(CultureInfo.InvariantCulture,
            $"[anomaly] loading: the world is shown ({why}): map built in {_map.LoadSeconds:0.00} s, then {now - _builtAt:0.00} s of loading"));
    }
}
