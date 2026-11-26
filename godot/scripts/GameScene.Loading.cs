using System.Diagnostics;
using System.Globalization;
using Godot;
using Anomaly.Client.Hud;
using Anomaly.Client.Render;
using Anomaly.Client.Shared;

namespace Anomaly.Client;

// The join's loading screen (docs/ai/specs/multiplayer.md, the client's hitches). The scene shows the connect screen's loading look over its world
// from _Ready on, builds the map over the next frames in steps of about BuildBudgetMs, so the bar moves and the window answers, and makes the rest of
// the scene when the build is done (FinishReady). Then the server is told the client has loaded (DEVIATION(DV597): without the lobby the server
// late-joins the new player only then). The world is never shown half made: the cover stays until the own body stands in it with what the eye
// first sees built (the things queued near it, the models warming up), or the lobby's window is open, and goes after CoverTimeoutSeconds whatever
// is left.
public partial class GameScene
{
    // The frame time the map's build may take under the loading screen: long enough to finish soon, short enough for the window to answer.
    private const double BuildBudgetMs = 40;

    // The things queued near the eye (tiles) that are built before the world is shown, and the most the loading screen waits.
    private const double CoverNearTiles = 12;
    private const double CoverTimeoutSeconds = 30;

    // Frames drawn under the cover once everything is in, before it goes: the first frames after the build are long (the light's first pass).
    private const int CoverSettleFrames = 3;

    // The bar's shares: the connection fills it to LoadingCover.ConnectShare, the start's preload (when it is not done yet) PreloadShare more,
    // the build to BuildEnd, the rest of the loading to the end.
    private const double PreloadShare = 0.05;
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
        if (!AssetPreload.FilesDone)
        {
            // the map's models and sheets come from the start's preload: it is waited for, not read a second time beside it
            _cover.Progress = LoadingCover.ConnectShare + PreloadShare * AssetPreload.Progress;
            _cover.Status = string.Create(CultureInfo.InvariantCulture, $"Loading the game's files... {AssetPreload.Progress:P0}");
            return true;
        }
        long start = Stopwatch.GetTimestamp();
        bool more;
        using (Dev.FrameProbe.Scope("loading/build"))
            while ((more = _building.MoveNext()) && Stopwatch.GetElapsedTime(start).TotalMilliseconds < BuildBudgetMs)
            {
            }
        if (more)
        {
            _cover.Progress = LoadingCover.ConnectShare + PreloadShare + (BuildEnd - LoadingCover.ConnectShare - PreloadShare) * _building.Current;
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

    // After the build: the client's own loading, then Loaded to the server (DV597: a client whose Hello said it loads first is late-joined only
    // then), then the cover goes once the own body stands in the world with its surroundings in, or the lobby's window is open.
    private void StepCover()
    {
        double now = App.NowSeconds;
        string waiting = LoadingWaitsFor();
        if (!_loadedSent && (waiting == null || now - _builtAt > CoverTimeoutSeconds))
        {
            if (waiting != null)
                GD.PrintErr($"[anomaly] loading: still {waiting} after {CoverTimeoutSeconds:0} s; going on");
            SendLoaded(now);
        }
        if (_loadedSent)
        {
            waiting = CoverWaitsFor(now);
            if (waiting == null && ++_settleFrames > CoverSettleFrames)
            {
                Reveal("ready");
                return;
            }
            if (now - _loadedAt > CoverTimeoutSeconds)
            {
                Reveal("timed out: " + waiting);
                return;
            }
        }
        if (waiting != null)
        {
            _settleFrames = 0;
            _cover.Status = waiting;
        }
        double t = now - _builtAt;
        _cover.Progress = BuildEnd + (1 - BuildEnd) * Math.Min(0.95, t / (t + 1.5));
    }

    private bool _loadedSent;
    private double _loadedAt;

    // What the client's own loading still waits for (the status line), or null when it is done and the server may be told.
    private string LoadingWaitsFor()
    {
        if (!AssetPreload.SoundsDone)
            return string.Create(CultureInfo.InvariantCulture, $"Loading the sounds... {AssetPreload.Progress:P0}");
        return null;
    }

    private void SendLoaded(double now)
    {
        _loadedSent = true;
        _loadedAt = now;
        if (_feed is GameClient { WaitsForLoad: true })
        {
            _session.Loaded();
            GD.Print(string.Create(CultureInfo.InvariantCulture, $"[anomaly] loading: loaded {now - _builtAt:0.00} s after the map was built; the server is told"));
        }
    }

    // What the cover waits for once the server was told (its status line), or null for nothing.
    private string CoverWaitsFor(double now)
    {
        App app = App.Instance;
        if (app.Editing)
            return null; // the map editor has no player: its world is shown once built
        if (InLobby)
            return null; // tg's lobby: its window is the screen
        if (!_ownSeen)
            return now - _loadedAt > 1.5 ? "Waiting for the round to start..." : "Joining the round...";
        RenderPose pose = default;
        if (_feed.Replica.TryGet(_feed.OwnEntity, out var own) && _session.Poses.TryGetPose(own, out pose)
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
