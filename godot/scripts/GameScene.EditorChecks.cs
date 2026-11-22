using System.IO;
using System.IO.Compression;
using System.Linq;
using Godot;
using Anomaly.Client.Editor;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Editor;
using Anomaly.Core.Tg;

namespace Anomaly.Client;

// The map editor's Checks panel and its map package (docs/ai/specs/map-editor.md 5.13). The panel lists what MapChecks finds on the map, tg's own
// lint rules and the editor's; a row goes to its tile, and the tiles are marked in the view while the panel is in front. The package is the map
// with what it needs, in a maps folder's layout, as one file to hand to a server's owner.
public partial class GameScene
{
    private const int CheckRows = 500; // the most problems the panel lists

    private static readonly Color MarkProblem = new(1f, 0.25f, 0.2f, 0.25f);

    private Tree _checkTree;
    private Label _checkCount;
    private List<MapProblem> _checkFound = new();
    private long _checkRevision = -1;
    private double _checkNextAt;
    private EditorCells _editorProblemMarks;

    private Control BuildChecksPanel(float s)
    {
        var column = new VBoxContainer();
        _checkCount = new Label { Text = "The map is checked while this panel is in front.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _checkCount.AddThemeColorOverride("font_color", new Color(0.62f, 0.63f, 0.66f));
        column.AddChild(_checkCount);
        _checkTree = new Tree { HideRoot = true, Columns = 3, SizeFlagsVertical = Control.SizeFlags.ExpandFill, SelectMode = Tree.SelectModeEnum.Row, FocusMode = Control.FocusModeEnum.Click };
        _checkTree.SetColumnExpand(0, false);
        _checkTree.SetColumnCustomMinimumWidth(0, Mathf.RoundToInt(96 * s));
        _checkTree.SetColumnExpandRatio(1, 3);
        _checkTree.SetColumnExpandRatio(2, 2);
        _checkTree.ItemSelected += () =>
        {
            if (_checkTree.GetSelected() is { } item && (int)item.GetMetadata(0) is int index && index < _checkFound.Count)
            {
                MapProblem problem = _checkFound[index];
                EditorGoTo(new MapFound(problem.X, problem.Y, problem.Z, problem.Member));
            }
        };
        column.AddChild(_checkTree);
        _checkRevision = -1;
        return column;
    }

    // The list follows the map: checked again when the map changed, a few times a second at most, while the panel is in front.
    private void UpdateChecksPanel(double now)
    {
        MapDocument map = _editor.Document;
        if (_editorProblemMarks == null)
            AddChild(_editorProblemMarks = new EditorCells(WorldScale.WallHeight / 2f, 12, new BoxMesh { Size = new Vector3(1.7f, WorldScale.WallHeight + 0.2f, 1.7f) }) { Name = "EditorProblems" });
        bool shown = _checkTree.IsVisibleInTree();
        if (shown && _checkFound.Count > 0)
            _editorProblemMarks.Show(Inv($"problems {_checkRevision} {_checkFound.Count}"), () => _checkFound.Select(p => (p.X, p.Y, p.Z)).Distinct().Where(c => map.Contains(c.X, c.Y, c.Z)).Select(c => (c.X, c.Y, c.Z, MarkProblem)));
        else
            _editorProblemMarks.Clear();
        if (!shown || _checkRevision == map.Revision || now < _checkNextAt || map.InStep)
            return;
        _checkNextAt = now + 0.5;
        _checkRevision = map.Revision;
        _checkFound = MapChecks.Run(map, CheckRows, out int total);
        _checkCount.Text = total == 0 ? "Nothing found: tg's lint rules of a tile, and the editor's own, pass."
            : total > _checkFound.Count ? Inv($"{total} found, the first {_checkFound.Count} listed. A row goes to its tile.") : Inv($"{total} found. A row goes to its tile.");
        _checkTree.Clear();
        TreeItem root = _checkTree.CreateItem();
        for (int i = 0; i < _checkFound.Count; i++)
        {
            MapProblem problem = _checkFound[i];
            TreeItem item = _checkTree.CreateItem(root);
            item.SetText(0, Inv($"{problem.X}, {problem.Y}, {problem.Z}"));
            item.SetText(1, problem.Text);
            item.SetText(2, problem.Member >= 0 && map.Contains(problem.X, problem.Y, problem.Z) && problem.Member < map[problem.X, problem.Y, problem.Z].Members.Count
                ? map[problem.X, problem.Y, problem.Z].Members[problem.Member].Path : problem.Rule);
            item.SetTooltipText(1, problem.Rule + ": " + problem.Text);
            item.SetMetadata(0, i);
        }
        GD.Print(Inv($"[anomaly] editor: checks: {total} found"));
    }

    // ---- the map package ----

    private void EditorExportPackage()
    {
        if (_editor.NeedsSave)
        {
            EditorNotice("Save the map first: the package holds the files as they are saved.");
            return;
        }
        EditorFileDialog(FileDialog.FileModeEnum.SaveFile, "The map's package", new[] { "*.zip ; Map packages" }, path =>
            EditorPackage(path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? path : path + ".zip"));
    }

    // The map with what it needs, as one zip in a maps folder's layout (tg's): its json, its file, its sky's picture, and the shuttles its json names
    // with their own jsons. Unpacked into a server's maps folder, the map is chosen by its json's name.
    private void EditorPackage(string zipPath)
    {
        try
        {
            string file = _editor.FilePath, stem = Path.GetFileNameWithoutExtension(file), folder = _editor.MapsFolder;
            string mapPath = "map_files/" + stem;
            JsonText(out string json, mapPath, stem);
            if (File.Exists(zipPath))
                File.Delete(zipPath);
            int files = 0;
            using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                void Add(string source, string name)
                {
                    zip.CreateEntryFromFile(source, name);
                    files++;
                }
                ZipArchiveEntry config = zip.CreateEntry("map_jsons/" + EditorFeed.JsonName(stem) + ".json");
                using (var writer = new StreamWriter(config.Open(), new System.Text.UTF8Encoding(false)))
                    writer.Write(json);
                files++;
                Add(file, mapPath + "/" + stem + MapText.Extension);
                if (_editor.SkyImagePath is { } picture && File.Exists(picture))
                    Add(picture, mapPath + "/" + Path.GetFileName(picture));
                // the shuttles the json names, from the maps folder
                foreach (string slot in EditorShuttleSlots)
                {
                    string id = _editor.JsonString("shuttles", slot);
                    string shuttle = id == null || folder == null ? null : Path.Combine(folder, "shuttles", slot, id + MapText.Extension);
                    if (shuttle == null || !File.Exists(shuttle))
                        continue;
                    Add(shuttle, "shuttles/" + slot + "/" + id + MapText.Extension);
                    if (File.Exists(Path.ChangeExtension(shuttle, ".json")))
                        Add(Path.ChangeExtension(shuttle, ".json"), "shuttles/" + slot + "/" + id + ".json");
                }
            }
            EditorNotice(Inv($"Packed {files} files into {zipPath}"));
            GD.Print(Inv($"[anomaly] editor: packed {files} files into {zipPath}"));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        {
            EditorNotice("Not packed: " + e.Message);
            GD.PrintErr("[anomaly] editor: not packed: " + e.Message);
        }
    }

    // The map json as the package holds it: the map's own, with the package's place of the map's file.
    private void JsonText(out string json, string mapPath, string stem)
    {
        System.Text.Json.Nodes.JsonObject root = MapSettings.ParseObject(_editor.JsonText);
        (root["map_path"], root["map_file"]) = (mapPath, stem + ".dmm");
        json = MapSettings.Read(_editor.JsonText).Write(root.ToJsonString());
    }
}
