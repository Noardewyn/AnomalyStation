using System.Linq;
using Godot;
using Anomaly.Client.Hud.Tgui;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Play;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Protocol;

namespace Anomaly.Client.Hud;

// DEVIATION(DV387): BYOND's popup windows with a map (tg's create_popup, setup_popup: the AI's multicamera windows popped out, the
// detective's spyglasses) and tg's picture-in-picture screen objects (the AI's multicamera windows) are the client's floating windows,
// each with a second 3D view of the tiles the map shows (protocol version 78's view areas), seen from above under the level's
// ceiling. A popup's close button sends tg's on-close command; a multicamera window's own buttons (close, bigger, smaller, pop out) are
// clicks on tg's component buttons in its vis_contents, a click on its view a click on the window (it becomes the main one). The
// windows keep their place in the options file, as the game's other windows.

/// <summary>A second 3D view of a rectangle of tiles, from above (<see cref="ViewArea"/>).</summary>
public partial class AreaView : SubViewportContainer
{
    private readonly SubViewport _viewport;
    private readonly Camera3D _camera;
    private readonly ColorRect _blank;

    /// <summary>The tiles to show; null shows tg's static (a dark screen).</summary>
    public ViewArea? Area { get; set; }

    /// <summary>A left press on the view.</summary>
    public event Action Clicked;

    public AreaView()
    {
        Stretch = true;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        CustomMinimumSize = new Vector2(96, 96);
        MouseFilter = MouseFilterEnum.Stop;
        _viewport = new SubViewport { HandleInputLocally = false, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        _camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Near = 0.05f, Current = true, KeepAspect = Camera3D.KeepAspectEnum.Height };
        _viewport.AddChild(_camera);
        AddChild(_viewport);
        _blank = new ColorRect { Color = new Color(0.07f, 0.08f, 0.09f), MouseFilter = MouseFilterEnum.Ignore };
        _blank.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_blank);
        GuiInput += e =>
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                Clicked?.Invoke();
        };
    }

    public override void _Process(double delta)
    {
        if (Area is not ViewArea area)
        {
            _blank.Visible = true;
            _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
            return;
        }
        _blank.Visible = false;
        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        float x = (TileSpace.WorldX(area.X1) + TileSpace.WorldX(area.X2)) / 2f;
        float z = (TileSpace.WorldZ(area.Y1) + TileSpace.WorldZ(area.Y2)) / 2f;
        // under the level's ceiling, looking straight down: the ceiling is behind the camera
        _camera.Position = new Vector3(x, TileSpace.WorldY(area.Z) + TileSpace.LevelHeight * 0.95f, z);
        _camera.Rotation = new Vector3(-Mathf.Pi / 2f, 0f, 0f);
        _camera.Far = TileSpace.LevelHeight * 1.5f;
        int tilesHigh = area.Y2 - area.Y1 + 1, tilesWide = area.X2 - area.X1 + 1;
        float aspect = Size.Y > 0 ? Size.X / Size.Y : 1f;
        // the rectangle fits the view whichever side is the longer
        _camera.Size = Math.Max(tilesHigh, tilesWide / Math.Max(aspect, 0.01f)) * TileSpace.TileSize;
    }
}

/// <summary>The popup windows and the multicamera windows (<see cref="ReplicaWorld.Popups"/>, <see cref="ReplicaWorld.ViewAreas"/>).</summary>
public partial class ViewWindows : Node
{
    private ReplicaWorld _replica;
    private CanvasLayer _layer;
    private PlayOptions _options;
    private Action _saved;
    private Func<PlaySession> _session;
    private readonly Dictionary<string, (PanelContainer Window, AreaView View, HBoxContainer Buttons)> _windows = new();

    public void Bind(ReplicaWorld replica, CanvasLayer layer, PlayOptions options, Action saved, Func<PlaySession> session)
    {
        _replica = replica;
        _layer = layer;
        _options = options;
        _saved = saved;
        _session = session;
        Name = "ViewWindows";
    }

    public override void _Process(double delta)
    {
        if (_replica == null)
            return;
        var wanted = new HashSet<string>();
        foreach (PopupWindow popup in _replica.Popups)
        {
            string key = "popup:" + popup.Name;
            wanted.Add(key);
            var (_, view, _) = Ensure(key, popup.Title, new Vector2(popup.Width, popup.Height), () => _session()?.ClosePopup(popup.Name));
            view.Area = _replica.ViewAreas.FirstOrDefault(a => a.Map == popup.Name + "_map") is { Z: > 0 } area ? area : null;
        }
        foreach (ViewArea area in _replica.ViewAreas)
        {
            if (area.ScreenId == 0 || !_replica.Screen.TryGetValue(area.ScreenId, out ScreenEntry pip))
                continue;
            string key = "pip:" + area.ScreenId;
            wanted.Add(key);
            long pipId = area.ScreenId;
            var (window, view, buttons) = Ensure(key, "Camera", new Vector2(240, 240), null, () => ClickScreen(pipId));
            view.Area = area;
            SyncButtons(buttons, pip);
        }
        foreach (string key in _windows.Keys.Where(k => !wanted.Contains(k)).ToList())
        {
            _windows[key].Window.QueueFree();
            _windows.Remove(key);
        }
    }

    private (PanelContainer Window, AreaView View, HBoxContainer Buttons) Ensure(string key, string title, Vector2 size, Action close, Action clicked = null)
    {
        if (_windows.TryGetValue(key, out var found))
            return found;
        var window = new PanelContainer { Name = key.Replace(':', '_') };
        window.AddThemeStyleboxOverride("panel", TguiRender.Flat(TguiTheme.WindowBg, 1));
        var column = new VBoxContainer();
        window.AddChild(column);
        // the multicamera windows share one place in the options (they are made and removed often); a popup keeps its own
        var chrome = new WindowChrome(window, key.StartsWith("pip:", StringComparison.Ordinal) ? "multicam" : key, _options, _saved) { MinSize = new Vector2(120, 120) };
        column.AddChild(chrome.TitleBar(string.IsNullOrEmpty(title) ? "View" : title, close ?? (() => { })));
        var buttons = new HBoxContainer();
        column.AddChild(buttons);
        var view = new AreaView();
        if (clicked != null)
            view.Clicked += clicked;
        column.AddChild(view);
        column.AddChild(chrome.Grip());
        _layer.AddChild(window);
        chrome.Place(new Vector2(320 + _windows.Count * 24, 120 + _windows.Count * 24), size + new Vector2(0, (float)TguiWindows.TitleBarHeight + 8));
        var made = (window, view, buttons);
        _windows[key] = made;
        return made;
    }

    // tg's picture-in-picture buttons are component buttons in the window's vis_contents (picture_in_picture.dm:59): one button each,
    // named by its icon state, a press a click on it
    private void SyncButtons(HBoxContainer row, ScreenEntry pip)
    {
        ScreenChild[] children = pip.VisChildren?.Items ?? Array.Empty<ScreenChild>();
        string signature = string.Join(",", children.Select(c => c.Id));
        if (row.HasMeta("children") && (string)row.GetMeta("children") == signature)
            return;
        row.SetMeta("children", signature);
        foreach (Node child in row.GetChildren())
        {
            row.RemoveChild(child);
            child.QueueFree();
        }
        foreach (ScreenChild child in children)
        {
            string state = _replica.TryGetAppearance(child.Appearance, out AppearanceEntry look) ? _replica.ResolveString(look.IconState) : null;
            string label = state switch { "x" => "Close", "expand" => "Bigger", "shrink" => "Smaller", "pop" => "Pop out", _ => null };
            if (label == null)
                continue;
            long id = child.Id;
            var button = new Button { Text = label, FocusMode = Control.FocusModeEnum.None };
            button.Pressed += () => ClickScreen(id);
            row.AddChild(button);
        }
    }

    private void ClickScreen(long id) => _session()?.Click(new ClickTarget(TargetKind.Screen, id, 0, 0, 0), ClickButton.Left, ClickModifiers.None);
}
