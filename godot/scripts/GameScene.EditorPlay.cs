using System.IO;
using System.Linq;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Dev;
using Anomaly.Client.Shared.Editor;
using Anomaly.Core.Tg;

namespace Anomaly.Client;

// The map editor's Play and Stop (docs/ai/specs/map-editor.md 5.7). The editor's side starts a round of the map as it is (PlayWorld: the game's own
// server inside the program) and joins it; the scene then changes to the game's, on that round's connection, and Stop changes it back to the
// editor's, on the map's own world, which stood meanwhile.
public partial class GameScene
{
    private PlayWorld _playStarting; // the round that is starting
    private GameClient _playJoining; // the connection that joins it, once it takes joins
    private double _playAskedAt;
    private Button _playButton;
    private OptionButton _playStartOption;
    private bool _playArrive; // where Play starts the player: as a new arrival (the arrivals shuttle, a late-join landmark), and not on the selected tile

    // What the editor's scene is at, kept while a round is played (App.EditorResume).
    private sealed record EditorResumeState(Vector3 Position, float Yaw, float Pitch, bool Top, (float Yaw, float Pitch) TopFrom, int Level, EditorTool Tool, MapShape Draw,
        SpawnTab Tab, string Type, int Dir, MapMember? Brush, (int X, int Y, int Z, int Member)? Selected, (int X0, int Y0, int X1, int Y1, int Z)? Region,
        EditorPlaceMode Mode, int Height, bool Above);

    // ---- the editor's side ----

    // F5 or the tool bar's button: a round of the map starts, with the player on the given tile (else the one EditorPlayTile finds), or, by the tool
    // bar's choice (arrive: null) or the caller's, as a new arrival on the map's arrivals shuttle; pressed again while it starts, the round is given up.
    private void EditorPlay((int X, int Y, int Z)? at = null, bool? arrive = null)
    {
        if (_playStarting != null)
        {
            EditorPlayGiveUp("Play was given up.");
            return;
        }
        if (arrive ?? (at == null && _playArrive))
        {
            if (!MapChecks.HasArrival(_editor.Document))
            {
                EditorNotice("Play: the map has no arrival point. Place an arrivals shuttle's dock or a late-join landmark, or start on a tile.");
                return;
            }
            at = null;
        }
        else if ((at ??= EditorPlayTile()) == null)
        {
            EditorNotice("Play: the map has no floor to stand on.");
            return;
        }
        try
        {
            _playStarting = new PlayWorld(_editor.Document, _editor.OnDisk ? _editor.FilePath : null, Path.GetFileName(_editor.FilePath), at, _editor.JsonText, _editor.SkyImagePath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            EditorNotice("Play did not start: " + e.Message);
            return;
        }
        _playAskedAt = App.NowSeconds;
        if (_editor.NeedsSave)
            WriteAutosave(); // (a round is a program's worth of the game: what is not saved is kept before it starts)
        GD.Print(at is { } tile ? Inv($"[anomaly] editor: play at {tile.X} {tile.Y} {tile.Z}") : "[anomaly] editor: play as a new arrival");
    }

    private void EditorPlayGiveUp(string notice)
    {
        if (_playStarting != null)
            App.Instance.LastPlayLog = _playStarting.Log;
        _playJoining?.Dispose();
        _playStarting?.Dispose();
        (_playJoining, _playStarting) = (null, null);
        _logShownCount = -1;
        if (notice != null)
            EditorNotice(notice);
    }

    // The tile a round's player starts on: the selected tile, the middle of the selected rectangle, else the tile in the middle of the view; and from
    // there the nearest tile a mob stands on. Null when the level has none.
    private (int X, int Y, int Z)? EditorPlayTile()
    {
        MapDocument map = _editor.Document;
        (int x, int y, int z) = (0, 0, _editorLevel);
        if (_editorSelected is { } s)
            (x, y, z) = (s.X, s.Y, s.Z);
        else if (_editorRegion is { } r)
            (x, y, z) = ((r.X0 + r.X1) / 2, (r.Y0 + r.Y1) / 2, r.Z);
        else
        {
            Vector3 point = EditorFloorPoint(_shell.Centre.GetGlobalRect().GetCenter()) ?? _rig.FreePosition;
            (x, y) = (Mathf.FloorToInt(point.X / TileSpace.TileSize) + 1, Mathf.FloorToInt(-point.Z / TileSpace.TileSize) + 1);
        }
        (x, y) = (Math.Clamp(x, 1, map.Width), Math.Clamp(y, 1, map.Height));
        for (int ring = 0; ring < Math.Max(map.Width, map.Height); ring++)
        {
            for (int dy = -ring; dy <= ring; dy++)
            {
                int step = ring == 0 || Math.Abs(dy) == ring ? 1 : 2 * ring; // the ring's border alone
                for (int dx = -ring; dx <= ring; dx += step)
                {
                    if (map.Contains(x + dx, y + dy, z) && MapEdits.Standable(map[x + dx, y + dy, z]))
                        return (x + dx, y + dy, z);
                }
            }
        }
        return null;
    }

    // Every frame while a round starts: the join once the round takes joins, and the game's scene once the join plays.
    private void UpdateEditorPlay(double now)
    {
        if (_playStarting is not { } round)
            return;
        if (round.Over || _playJoining is { Phase: FeedPhase.Closed })
        {
            string why = round.Failure ?? _playJoining?.CloseReason ?? "the round ended";
            GD.PrintErr("[anomaly] editor: play did not start: " + why);
            foreach (string line in round.Log.TakeLast(30))
                GD.PrintErr("[anomaly] editor:   " + line);
            EditorPlayGiveUp("Play did not start: " + why + " (the Log panel has the round's lines)");
            return;
        }
        if (_playJoining == null)
        {
            if (!round.Ready)
                return;
            _playJoining = new GameClient(GameClient.CreateTransport(), "127.0.0.1", round.Port, App.Instance.Args.Name);
            _playJoining.Connect();
        }
        _playJoining.Poll(now);
        if (_playJoining.Phase != FeedPhase.Playing)
            return;

        GD.Print(Inv($"[anomaly] editor: play: the round is joined after {now - _playAskedAt:0.0} s"));
        WriteEditorSettings();
        App app = App.Instance;
        app.EditorResume = new EditorResumeState(_rig.FreePosition, _rig.Yaw, _rig.Pitch, _editorTop, _editorTopFrom, _editorLevel, _editorTool, _editorDraw,
            _spawnTab, _spawnSelected?.Path, _spawnDirIndex, _editorBrush, _editorSelected, _editorRegion, _editorPlaceMode, _editorHeight, _editorAbove);
        (PlayWorld world, GameClient connection) = (_playStarting, _playJoining);
        (_playStarting, _playJoining) = (null, null);
        _keepFeed = true; // the map's own world is the session's: it stands while the round is played
        _sceneChange = () => app.StartPlay(world, connection);
    }

    // The scene came back from a round: the camera, the tool and the selection it was left with, and what the round said.
    private void ResumeEditor(App app)
    {
        if (app.EditorNotice != null)
            EditorNotice(app.EditorNotice, 12);
        app.EditorNotice = null;
        if (app.EditorResume is not EditorResumeState was)
            return;
        app.EditorResume = null;
        (_editorLevel, _editorTool, _editorDraw, _editorBrush, _editorSelected, _editorRegion) = (was.Level, was.Tool, was.Draw, was.Brush, was.Selected, was.Region);
        (_spawnTab, _spawnDirIndex, _startPick) = (was.Tab, was.Dir, was.Type); // (the palette's type is found again when tg's types are read)
        (_editorPlaceMode, _editorHeight, _editorAbove) = (was.Mode, was.Height, was.Above);
        _rig.PlaceFree(was.Position);
        (_rig.Yaw, _rig.Pitch) = was.Top ? was.TopFrom : (was.Yaw, was.Pitch);
        if (was.Top)
        {
            // the top view again, over the same point and as high
            EditorSetTop(true);
            _rig.PlaceFree(was.Position);
            UpdateEditorCamera();
        }
    }

    // ---- the game's side ----

    // The scene is the game's, on a round of the map editor's: what the editor's scene left set is the game's again, and a bar says how to go back.
    private void StartPlayed(App app)
    {
        if (app.Play == null)
            return;
        GetViewport().DebugDraw = Viewport.DebugDrawEnum.Disabled; // (the editor's simple look draws unshaded)
        AudioServer.SetBusMute(0, false); // (the editor is silent)
        app.VersionTagShown = true;
        string name = Path.GetFileName(app.Editor.FilePath);
        GetWindow().Title = ClientArgs.EditorName + ": playing " + name;

        Vector2 view = GetViewport().GetVisibleRect().Size;
        float scale = new HudMetrics(view.X, view.Y, 1f).TextScale; // (the size of the version tag in the corner)
        var layer = new CanvasLayer { Name = "PlayBar", Layer = 98 };
        var bar = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        bar.AddThemeConstantOverride("separation", Mathf.RoundToInt(8 * scale));
        var label = new Label { Text = "Playing " + name, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(11 * scale));
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        label.AddThemeConstantOverride("outline_size", Mathf.RoundToInt(4 * scale));
        var stop = new Button { Text = "Stop  F8", FocusMode = Control.FocusModeEnum.None, TooltipText = "Back to the editor (F8, or Quit in the Esc menu). The round ends; the map is as you left it." };
        stop.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(11 * scale));
        stop.Pressed += () => StopPlay(null);
        bar.AddChild(label);
        bar.AddChild(stop);
        bar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop, Control.LayoutPresetMode.Minsize, Mathf.RoundToInt(4 * scale));
        layer.AddChild(bar);
        AddChild(layer);
    }

    // Stop: the round ends and the editor's scene comes back, saying the notice (why a round ended by itself).
    private void StopPlay(string notice)
    {
        if (_leaving || _sceneChange != null || App.Instance.Play == null)
            return;
        GD.Print("[anomaly] editor: stop" + (notice == null ? string.Empty : ": " + notice));
        _sceneChange = () => App.Instance.StopPlay(notice); // (the scene's feed is the round's connection: leaving the scene closes it)
    }
}
