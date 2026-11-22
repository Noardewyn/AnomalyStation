using System.IO;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Editor;
using Anomaly.Core.Tg;

namespace Anomaly.Client;

// The map editor's autosave (docs/ai/specs/map-editor.md 5.21): while the open map has changes that are not saved, a copy of it is written to a
// folder of the user's own every minute (MapAutosave), and taken away when the map is saved or left on purpose. A copy that is there when the
// editor starts was left by an editor that crashed or was stopped: a window offers to restore it, as changes that are not saved.
public partial class GameScene
{
    private const double AutosaveEvery = 60; // seconds between two copies

    // (kept from scene to scene: a round that is played, and the editor after it, are the same program with the same copy)
    private static string _autosaveFolder;
    private static string _autosaveCopyOf; // the map's file this program has a copy of; null: none
    private static (long Revision, string Json) _autosaveKept; // what that copy holds
    private static bool _autosaveOffered; // the copies a dead editor left were offered: once a run of the program

    private bool _autosaveArmed; // the map has something to save: its next copy has a time
    private double _autosaveNextAt;
    private bool _restoreAsked; // the window that offers the kept copies is to be shown
    private AcceptDialog _restoreDialog;
    private ItemList _restoreList;
    private List<KeptMap> _restoreLeft = new();

    // The copies' folder is beside the editor's settings file: a scripted check's own, so that it leaves the mapper's copies alone.
    private void StartEditorAutosave(ClientArgs args)
    {
        string settings = ProjectSettings.GlobalizePath(string.IsNullOrWhiteSpace(args.EditorSettingsPath) ? "user://map_editor.cfg" : args.EditorSettingsPath);
        _autosaveFolder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(settings)) ?? ".", "map_editor_autosave");
        // (a scripted run is asked nothing: the console's editor restore shows the window)
        _restoreAsked = !_autosaveOffered && !args.Automated;
        _autosaveOffered = true;
    }

    // Every frame: the copy is written when its time has come and the map has changed since the last one, and never in the middle of a stroke.
    private void UpdateEditorAutosave(double now)
    {
        if (_restoreAsked)
        {
            _restoreAsked = false;
            EditorOfferRestore(always: false);
        }
        if (!_editor.NeedsSave)
        {
            _autosaveArmed = false;
            DropAutosave(); // (saved: the copy is of no use)
            return;
        }
        if (!_autosaveArmed)
            (_autosaveArmed, _autosaveNextAt) = (true, now + AutosaveEvery);
        if (now < _autosaveNextAt || _editor.Document.InStep || EditorDragging || Input.IsMouseButtonPressed(MouseButton.Left))
            return;
        _autosaveNextAt = now + AutosaveEvery;
        WriteAutosave();
    }

    // The copy is written now, unless it holds the map as it is.
    private void WriteAutosave()
    {
        string json = _editor.JsonText;
        if (_autosaveCopyOf == _editor.FilePath && _autosaveKept == (_editor.Document.Revision, json))
            return;
        try
        {
            if (_autosaveCopyOf != null && _autosaveCopyOf != _editor.FilePath)
                DropAutosave(); // (the map was saved under another name: the copy is of its file now)
            MapAutosave.Write(_autosaveFolder, _editor, System.Environment.ProcessId, DateTime.UtcNow);
            (_autosaveCopyOf, _autosaveKept) = (_editor.FilePath, (_editor.Document.Revision, json));
            GD.Print("[anomaly] editor: kept the changes that are not saved in " + _autosaveFolder);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            GD.PrintErr("[anomaly] editor: the changes were not kept in " + _autosaveFolder + ": " + e.Message);
        }
    }

    private static void DropAutosave()
    {
        if (_autosaveCopyOf == null)
            return;
        MapAutosave.Drop(_autosaveFolder, _autosaveCopyOf, System.Environment.ProcessId);
        (_autosaveCopyOf, _autosaveKept) = (null, default);
    }

    // Whether the editor that wrote a copy still runs: its process is there and is this program.
    private static bool EditorRuns(int process)
    {
        try
        {
            using var other = System.Diagnostics.Process.GetProcessById(process);
            using var own = System.Diagnostics.Process.GetCurrentProcess();
            return !other.HasExited && other.ProcessName == own.ProcessName;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return false;
        }
    }

    private void BuildRestoreDialog(float s)
    {
        _restoreDialog = new AcceptDialog { Title = "Changes that were not saved", OkButtonText = "Later", Theme = _shell.UiTheme };
        var body = new VBoxContainer();
        body.AddChild(new Label
        {
            Text = "The editor was closed without saving these maps: it crashed, or was stopped. Their changes were kept. Restore a map to go on with it: it opens with the changes as they were, not yet saved. Discard takes its kept changes away for good.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(460 * s, 0), // (a label that wraps is as tall as its width makes it)
        });
        _restoreList = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 110 * s) };
        _restoreList.ItemActivated += index => EditorRestore((int)index);
        body.AddChild(_restoreList);
        _restoreDialog.AddChild(body);
        _restoreDialog.AddButton("Restore", true, "restore");
        _restoreDialog.AddButton("Discard", true, "discard");
        _restoreDialog.CustomAction += action =>
        {
            int[] selected = _restoreList.GetSelectedItems();
            if (selected.Length == 0)
                return;
            if (action.ToString() == "restore")
            {
                EditorRestore(selected[0]);
                return;
            }
            MapAutosave.Drop(_restoreLeft[selected[0]]);
            EditorOfferRestore(always: false);
        };
        _shell.AddChild(_restoreDialog);
    }

    // The window that lists what a dead editor left; with nothing left it is not shown (always: it says so in the status bar).
    private void EditorOfferRestore(bool always)
    {
        _restoreLeft = MapAutosave.Left(_autosaveFolder, System.Environment.ProcessId, EditorRuns);
        // (this program's own copy is of the open map: nothing to restore)
        _restoreLeft.RemoveAll(kept => kept.Process == System.Environment.ProcessId && kept.MapPath == _autosaveCopyOf);
        GD.Print(Inv($"[anomaly] editor: {_restoreLeft.Count} kept {(_restoreLeft.Count == 1 ? "copy" : "copies")} of unsaved changes in {_autosaveFolder}"));
        if (_restoreLeft.Count == 0)
        {
            _restoreDialog.Hide();
            if (always)
                EditorNotice("No kept changes: nothing was left unsaved by an editor that stopped.", 4);
            return;
        }
        _restoreList.Clear();
        foreach (KeptMap kept in _restoreLeft)
        {
            GD.Print(Inv($"[anomaly] editor:   {kept.MapPath}, kept {kept.At.ToLocalTime():yyyy-MM-dd HH:mm:ss}{(kept.Older ? ", its file was saved later" : string.Empty)}"));
            _restoreList.AddItem(Inv($"{Path.GetFileName(kept.MapPath)}   kept {kept.At.ToLocalTime():yyyy-MM-dd HH:mm}{(kept.Older ? "   (its file was saved after this: restoring takes that save back)" : string.Empty)}   {kept.MapPath}"));
        }
        _restoreList.Select(0);
        if (!_restoreDialog.Visible)
            _restoreDialog.PopupCenteredClamped(new Vector2I(Mathf.RoundToInt(700 * _shell.UiScale), Mathf.RoundToInt(300 * _shell.UiScale)), 0.86f);
    }

    // A kept copy becomes the open map, with its changes as changes that are not saved.
    private void EditorRestore(int index)
    {
        if (index < 0 || index >= _restoreLeft.Count)
            return;
        KeptMap kept = _restoreLeft[index];
        EditorSession next;
        try
        {
            next = EditorSession.Restore(kept);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or MapTextException or ArgumentException)
        {
            EditorNotice("Not restored: " + e.Message);
            return;
        }
        _restoreDialog.Hide();
        EditorGuard(() => _sceneChange = () =>
        {
            // the copy is this program's from now on: written under its own process first, then the dead editor's is taken away
            try
            {
                MapAutosave.Write(_autosaveFolder, next, System.Environment.ProcessId, DateTime.UtcNow);
                (_autosaveCopyOf, _autosaveKept) = (next.FilePath, (next.Document.Revision, next.JsonText));
                if (kept.Process != System.Environment.ProcessId)
                    MapAutosave.Drop(kept);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                GD.PrintErr("[anomaly] editor: the restored changes were not kept again: " + e.Message);
            }
            GD.Print("[anomaly] editor: restored the kept changes of " + next.FilePath);
            App.Instance.EditorNotice = "Restored the changes that were not saved. Save the map to keep them.";
            App.Instance.OpenEditor(next);
        });
    }
}
