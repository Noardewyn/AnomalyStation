using Godot;

namespace Anomaly.Client.Hud;

/// <summary>
/// The loading screen's look: a picture over the whole window, the program's name in its top left corner, and a bar along the bottom with what is
/// being done written over it. The connect screen shows it while a connection is made and the map editor's world starts; the game scene shows the
/// same over its world while that is built and loaded, so the hand-over from one to the other shows no seam. In the small window of the map
/// editor's start it has larger letters for its size and an edge of its own (a window with no border is lost on a dark desktop without one).
/// </summary>
public partial class LoadingCover : Control
{
    public const string EditorPicture = "res://assets/ui/editor_loading.webp";
    public const string GamePicture = "res://assets/ui/lobby_title.webp";

    /// <summary>The bar's share the connection (or the map editor's world starting) fills; the game scene's loading fills the rest.</summary>
    public const double ConnectShare = 0.25;

    private readonly Label _title;
    private readonly Panel _edge;
    private readonly VBoxContainer _bottom;
    private readonly ProgressBar _bar;

    public LoadingCover() : this(string.Empty, GamePicture) // (Godot's own instancing of a script needs one)
    {
    }

    public LoadingCover(string title, string picture)
    {
        MouseFilter = MouseFilterEnum.Stop; // nothing under it takes a click while it is shown
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(Picture(picture));
        _title = new Label { Text = title, MouseFilter = MouseFilterEnum.Ignore };
        _title.AddThemeColorOverride("font_color", new Color(0.92f, 0.95f, 1f));
        AddChild(_title);
        var edge = new StyleBoxFlat { DrawCenter = false, BorderColor = new Color(1f, 1f, 1f, 0.16f) };
        edge.SetBorderWidthAll(2);
        _edge = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        _edge.AddThemeStyleboxOverride("panel", edge);
        _edge.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_edge);
        StatusLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StatusLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.85f, 0.95f));
        _bar = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0, ShowPercentage = false, MouseFilter = MouseFilterEnum.Ignore };
        _bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(1f, 1f, 1f, 0.08f) });
        _bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(0.45f, 0.68f, 1f) });
        var line = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        line.AddChild(StatusLabel);
        // (laid out before the status line has its width, the line wraps a letter a row and makes the box tall; a box never shrinks back, so its
        // contents keep to its bottom edge)
        _bottom = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, GrowVertical = GrowDirection.Begin, Alignment = BoxContainer.AlignmentMode.End };
        _bottom.AddChild(line);
        _bottom.AddChild(_bar);
        AddChild(_bottom);
        Resized += Layout;
    }

    /// <summary>The line of what is being done (the connect screen colours it red for an error).</summary>
    public Label StatusLabel { get; }

    public string Status
    {
        get => StatusLabel.Text;
        set => StatusLabel.Text = value;
    }

    /// <summary>How far the wait is, 0 to 1.</summary>
    public double Progress
    {
        get => _bar.Value;
        set => _bar.Value = value;
    }

    /// <summary>False when nothing is loading (the connect screen saying why a start failed).</summary>
    public bool BarVisible
    {
        set => _bar.Visible = value;
    }

    public override void _Ready() => Layout();

    /// <summary>A loading picture over the whole window, cut to fill it.</summary>
    public static TextureRect Picture(string path)
    {
        var picture = new TextureRect
        {
            Texture = GD.Load<Texture2D>(path),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        picture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        return picture;
    }

    // Sized by the window's height, so the screen is the same picture on a small display and on a large one.
    private void Layout()
    {
        if (!IsInsideTree())
            return;
        bool small = App.Instance.SplashWindow;
        float unit = GetViewportRect().Size.Y / 100f;
        int margin = Math.Max(8, (int)(unit * (small ? 6f : 3f)));
        _edge.Visible = small;
        _title.AddThemeFontSizeOverride("font_size", Math.Max(16, (int)(unit * (small ? 11f : 8f))));
        _title.Size = Vector2.Zero; // shrink to the new sizes before the anchors place it
        _title.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft, LayoutPresetMode.Minsize, margin);
        StatusLabel.AddThemeFontSizeOverride("font_size", Math.Max(10, (int)(unit * (small ? 4.2f : 2f))));
        _bar.CustomMinimumSize = new Vector2(0, Math.Max(4f, unit * (small ? 1.6f : 1.2f)));
        _bottom.AddThemeConstantOverride("separation", Math.Max(2, (int)(unit * (small ? 2f : 1f))));
        _bottom.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide, LayoutPresetMode.Minsize, margin);
    }
}
