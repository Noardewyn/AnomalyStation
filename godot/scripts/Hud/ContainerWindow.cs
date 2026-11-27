using System.Linq;
using Godot;
using Anomaly.Client.Hud.Maptext;
using Anomaly.Client.Render;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Protocol;

namespace Anomaly.Client.Hud;

/// <summary>
/// M4a P7 (D131, interaction.md 5): a native window over one HUD group's screen entries. The server stays 100 % tg: an open storage's cells, close button and items
/// (<see cref="HudGroup.Storage"/>) are tg's own <c>client.screen</c> objects; this window lays them out
/// (<see cref="ContainerLayout"/>), draws their pictures from the replica's tables, gives every cell a text label (D70) and never sends anything itself: a click on a cell is a
/// <see cref="TargetKind.Screen"/> click on its entry id, found through <see cref="IHudSurface"/> by the scene.
/// </summary>
public partial class ContainerWindow : Control, IHudSurface
{
    private readonly List<(ContainerCell Cell, Rect2 Rect)> _rects = new();
    private ContainerLayout _layout;
    private ReplicaWorld _replica;
    private IAppearanceTextures _textures;
    private bool _dirty = true;
    private Rect2 _frame;
    private double _now;
    // D282: the title strip drags the window, the corner grip scales it; both kept in the options (window.KEY, window_scale.KEY)
    private PlayOptions _options;
    private Action _saved;
    private string _key;
    private Control _bar;
    private ResizeGrip _grip;
    private bool _dragging;
    private Vector2? _placed; // the frame's top left the player chose, saved pixels (PlayOptions.WindowScreen): its share of any screen
    private Vector2 _dragLast;
    private float _zoom = 1f;

    /// <summary>The window's title (drawn over it).</summary>
    protected string Title = "Container";

    /// <summary>The layout the window shows (for the tests and the scene).</summary>
    public IContainerView View => _layout;

    /// <summary>The cells' rectangles on the screen now.</summary>
    public IReadOnlyList<(ContainerCell Cell, Rect2 Rect)> CellRects => _rects;

    public bool IsOpen => _layout != null && _layout.IsOpen && Visible;

    /// <summary>D292: the screen entry of tg's close button while the window is open (a click on it closes the storage), else 0.</summary>
    public long CloseEntry => IsOpen ? _layout.Cells.FirstOrDefault(c => c.Role == ContainerCellRole.Close).EntryId : 0;

    public void Bind(ReplicaWorld replica, IAppearanceTextures textures, byte group, string title, string key, PlayOptions options, Action saved)
    {
        _replica = replica;
        _textures = textures;
        _key = key;
        _options = options;
        _saved = saved;
        _zoom = options.WindowScales.TryGetValue(key, out int percent) ? percent / 100f : 1f;
        if (options.WindowPositions.TryGetValue(key, out (int X, int Y) at))
            _placed = new Vector2(at.X, at.Y);
        _bar = new Control { MouseDefaultCursorShape = CursorShape.Move, TooltipText = "Drag to move" };
        _bar.GuiInput += OnBarInput;
        AddChild(_bar);
        _grip = new ResizeGrip { TooltipText = "Drag to scale" };
        _grip.Dragged += delta => _zoom = Mathf.Clamp(_zoom * (_frame.Size.X + delta.X) / Math.Max(1f, _frame.Size.X), PlayOptions.MinWindowScale / 100f, PlayOptions.MaxWindowScale / 100f);
        _grip.Released += () =>
        {
            _options.WindowScales[_key] = (int)MathF.Round(_zoom * 100);
            _saved();
        };
        AddChild(_grip);
        _layout = new ContainerLayout(group);
        Title = title;
        Name = title.Replace(' ', '_');
        ZIndex = 5;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        replica.ScreenEntryChanged += OnChanged;
        replica.ScreenEntryRemoved += OnRemoved;
    }

    public override void _ExitTree()
    {
        if (_replica != null)
        {
            _replica.ScreenEntryChanged -= OnChanged;
            _replica.ScreenEntryRemoved -= OnRemoved;
        }
    }

    private void OnChanged(ScreenEntry entry)
    {
        if (entry.Group == _layout.Group || _layout.Cells.Any(c => c.EntryId == entry.Id))
            _dirty = true;
    }

    private void OnRemoved(long id)
    {
        if (_layout.Cells.Any(c => c.EntryId == id))
            _dirty = true;
    }

    /// <summary>The cell size in pixels for this screen.</summary>
    private static float CellSize(in HudMetrics metrics) => Mathf.Round(metrics.Tile * 0.95f);

    /// <summary>Where the window's frame goes on the screen (the origin of cell (0, 0) is its top left plus a margin).</summary>
    private static Vector2 Origin(in HudMetrics metrics, float cell, int columns, int rows)
        => new((metrics.Width - columns * cell) / 2f, metrics.Height - metrics.Tile * 2.35f - rows * cell - 18f);

    public void Update(double now, in HudMetrics metrics)
    {
        _now = now;
        if (_replica == null)
            return;
        if (_dirty)
        {
            _dirty = false;
            _layout.Rebuild(_replica.ScreenGroup(_layout.Group), _replica);
        }
        bool show = _layout.IsOpen;
        if (Visible != show)
            Visible = show;
        _rects.Clear();
        if (!show)
            return;
        float cell = Mathf.Round(CellSize(metrics) * _zoom);
        int columns = Math.Max(1, _layout.Columns), rows = Math.Max(1, _layout.Rows);
        bool storage = _layout.Group == HudGroup.Storage;
        var frameSize = new Vector2(columns * cell + (storage ? cell * 1.1f : 0) + 20, rows * (cell + 12) + 38);
        Vector2 origin = Origin(metrics, cell, columns, rows);
        if (_placed is Vector2 saved)
        {
            (double px, double py) = _options.WindowOnScreen(saved.X, saved.Y, metrics.Width, metrics.Height);
            (double x, double y) = TguiWindows.KeepOnScreen(px, py, frameSize.X, metrics.Width, metrics.Height, 24, 1);
            origin = new Vector2((float)x, (float)y) + new Vector2(10, 26);
        }
        _frame = new Rect2(origin - new Vector2(10, 26), frameSize);
        // with the view holding the pointer the strip and the grip take no clicks (the screen's centre is a click at the world)
        MouseFilterEnum handles = Input.MouseMode == Input.MouseModeEnum.Captured ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
        _bar.MouseFilter = _grip.MouseFilter = handles;
        _bar.Position = _frame.Position;
        _bar.Size = new Vector2(_frame.Size.X, 24);
        _grip.Position = _frame.End - _grip.CustomMinimumSize;
        _grip.Size = _grip.CustomMinimumSize;
        foreach (ContainerCell c in _layout.Cells)
        {
            Rect2 r;
            switch (c.Role)
            {
                case ContainerCellRole.Slot when storage:
                    r = new Rect2(origin, new Vector2(columns * cell, rows * cell)); // tg's one stretched cell sprite is the whole grid
                    break;
                case ContainerCellRole.Close:
                    r = new Rect2(origin + new Vector2(columns * cell + 6, 0), new Vector2(cell, cell) * 0.8f);
                    break;
                case ContainerCellRole.Other:
                    continue;
                default:
                    r = new Rect2(origin + new Vector2(c.Column * cell, c.Row * (storage ? cell : cell + 12)), new Vector2(cell, cell));
                    break;
            }
            _rects.Add((c, r));
        }
        QueueRedraw();
    }

    private void OnBarInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                _dragging = button.Pressed;
                _dragLast = GetViewport().GetMousePosition();
                if (button.Pressed)
                    _placed = Saved(_frame.Position);
                else if (_placed is Vector2 at)
                {
                    _options.WindowPositions[_key] = ((int)MathF.Round(at.X), (int)MathF.Round(at.Y));
                    _saved();
                }
                _bar.AcceptEvent();
                break;
            case InputEventMouseMotion when _dragging:
                Vector2 now = GetViewport().GetMousePosition();
                _placed = Saved(_frame.Position + (now - _dragLast));
                _dragLast = now;
                _bar.AcceptEvent();
                break;
        }
    }

    private Vector2 Saved(Vector2 onScreen)
    {
        Vector2 screen = GetViewportRect().Size;
        (int w, int h) = _options.WindowScreenFor(screen.X, screen.Y);
        return onScreen * new Vector2(w / screen.X, h / screen.Y);
    }

    /// <inheritdoc />
    public void Collect(List<HudDrawn> bottomToTop)
    {
        if (!IsOpen)
            return;
        // The storage's background cell first, then the items and buttons over it (the topmost last).
        // Version 12: a slot's worn item (its vis_contents child) is hit on its own pixels before the slot's box (the cell is the slot's icon square).
        foreach ((ContainerCell cell, Rect2 r) in _rects.OrderBy(x => x.Cell.Role == ContainerCellRole.Slot && _layout.Group == HudGroup.Storage ? 0 : 1))
        {
            IReadOnlyList<HudChildDrawn> children = _replica.Screen.TryGetValue(cell.EntryId, out ScreenEntry entry) ? HudPictures.ChildrenOf(_replica, _textures, entry, _now) : null;
            bottomToTop.Add(new HudDrawn(cell.EntryId, new HudRect(r.Position.X, r.Position.Y, r.Size.X, r.Size.Y), null, 0, 0, children));
        }
    }

    // Protocol version 18: the clipping canvas items of the cells' maptexts (BYOND clips maptext to its box).
    private readonly MaptextBoxes _maptextBoxes = new();

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
            _maptextBoxes.Free();
    }

    public override void _Draw()
    {
        _maptextBoxes.Begin(GetCanvasItem());
        DrawCells();
        _maptextBoxes.End();
    }

    private void DrawCells()
    {
        if (!Visible || _rects.Count == 0)
            return;
        Font font = ThemeDB.FallbackFont;
        DrawRect(_frame, new Color(0.04f, 0.05f, 0.08f, 0.82f));
        DrawRect(_frame, new Color(0.7f, 0.78f, 0.9f, 0.9f), false, 2f);
        DrawString(font, _frame.Position + new Vector2(10, 17), Title, HorizontalAlignment.Left, -1, 14, new Color(0.85f, 0.92f, 1f));
        bool storage = _layout.Group == HudGroup.Storage;
        // The storage's background cell (the whole grid) first, as Collect orders the pick: drawn in layout order it sorts at row 0,
        // column 0 and covered an item there (a lower id than the storage interface tg makes on the first open) with its boxes.
        foreach ((ContainerCell cell, Rect2 r) in _rects.OrderBy(x => x.Cell.Role == ContainerCellRole.Slot && storage ? 0 : 1))
        {
            if (cell.Role == ContainerCellRole.Slot && storage)
            {
                // The grid: a box per cell.
                float cs = r.Size.X / Math.Max(1, _layout.Columns);
                for (int row = 0; row < _layout.Rows; row++)
                {
                    for (int col = 0; col < _layout.Columns; col++)
                    {
                        var box = new Rect2(r.Position + new Vector2(col * cs, row * cs), new Vector2(cs, cs)).Grow(-2);
                        DrawRect(box, new Color(0.14f, 0.17f, 0.24f, 0.95f));
                        DrawRect(box, new Color(0.45f, 0.52f, 0.64f, 0.9f), false, 1.5f);
                    }
                }
                continue;
            }
            if (cell.Role == ContainerCellRole.Close)
            {
                DrawRect(r, new Color(0.35f, 0.1f, 0.1f, 0.95f));
                DrawRect(r, new Color(1f, 0.7f, 0.7f), false, 2f);
                DrawLine(r.Position + r.Size * 0.28f, r.Position + r.Size * 0.72f, Colors.White, 3f);
                DrawLine(r.Position + new Vector2(r.Size.X * 0.72f, r.Size.Y * 0.28f), r.Position + new Vector2(r.Size.X * 0.28f, r.Size.Y * 0.72f), Colors.White, 3f);
                DrawString(font, r.Position + new Vector2(0, r.Size.Y + 12), "Close", HorizontalAlignment.Left, -1, 11, Colors.White);
                continue;
            }
            if (!storage)
            {
                DrawRect(r, new Color(0.12f, 0.15f, 0.22f, 0.9f));
                DrawRect(r, new Color(0.45f, 0.52f, 0.64f, 0.9f), false, 1.5f);
            }
            // DV575: a slot's item too big for its square is drawn on its own, fitted into it (the slot's picture leaves it out); so is an item in a storage cell
            List<ComposedAppearance> fitted = _replica.Screen.TryGetValue(cell.EntryId, out ScreenEntry held) && _textures is AppearanceTextures
                ? HudPictures.OversizedChildren(_replica, _textures, held, _now) : null;
            if (_replica.Screen.TryGetValue(cell.EntryId, out ScreenEntry entry) && ComposeCell(entry, fitted, out ComposedAppearance c))
            {
                float s = r.Size.X / 32f;
                var rect = new Rect2(r.Position.X - c.OriginX * s, r.Position.Y + r.Size.Y + c.OriginY * s - c.PixelHeight * s, c.PixelWidth * s, c.PixelHeight * s);
                if (cell.Role == ContainerCellRole.Item && HudChildDrawn.FitOf(c.PixelWidth, c.PixelHeight) > 0f)
                    rect = HudPictures.FittedIn(r, c);
                DrawTextureRect(c.Texture, rect, false);
                foreach (ComposedAppearance big in fitted ?? Enumerable.Empty<ComposedAppearance>())
                    DrawTextureRect(big.Texture, HudPictures.FittedIn(r, big), false);
                // Protocol version 18: tg's item count (storage_interface.dm numberdisplay) and a stack's amount are the item's maptext.
                if (_textures is AppearanceTextures real)
                    MaptextRenderer.Shared.Draw(this, real, _replica, entry.Appearance, new Vector2(r.Position.X, r.Position.Y + r.Size.Y), s, _maptextBoxes);
            }
            // D70: every cell keeps its text label.
            if (cell.Label.Length > 0)
            {
                string label = cell.Label.Length > 8 ? cell.Label.Substring(0, 7) + "." : cell.Label;
                DrawString(font, r.Position + new Vector2(1, r.Size.Y + (storage ? 12 : 11)), label, HorizontalAlignment.Left, r.Size.X + 6, 10, cell.Role == ContainerCellRole.Item ? new Color(1f, 0.95f, 0.7f) : new Color(0.75f, 0.82f, 0.92f));
            }
        }
    }

    // A cell's picture: the entry's own, without the items drawn fitted on their own (DV575).
    private bool ComposeCell(in ScreenEntry entry, List<ComposedAppearance> fitted, out ComposedAppearance composed)
    {
        if (fitted is not { Count: > 0 } || _textures is not AppearanceTextures real)
            return _textures.TryCompose(_replica, entry.Appearance, 2, _now, out composed);
        composed = default;
        TableRef stripped = HudPictures.WithoutOversized(real, _replica, entry.Appearance, out IAppearanceSource tables);
        if (!real.TryGet(tables, stripped, 2, _now, out ComposedRaster raster, out GodotPicture picture, rootOffset: true))
            return false;
        composed = new ComposedAppearance(picture.Texture, raster.Image.Width, raster.Image.Height, raster.OriginX, raster.OriginY);
        return true;
    }
}
