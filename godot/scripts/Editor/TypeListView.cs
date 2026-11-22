using System.Linq;
using Godot;
using Anomaly.Client.Dev;
using Anomaly.Client.Shared.Dev;

namespace Anomaly.Client.Editor;

/// <summary>
/// The map editor's list of tg's types (docs/ai/specs/map-editor.md 5.2): a picture, a name and a path on each row, a star for a favourite. As a
/// tree by the types' paths (<see cref="SpawnTree"/>) a row stands in by its depth and a branch has an arrow that folds what stands under it.
/// Only the rows in view are drawn, so the list of every type (some twenty thousand) scrolls as a short one does.
/// </summary>
public sealed partial class TypeListView : Control
{
    private readonly VScrollBar _scroll = new();
    private IReadOnlyList<SpawnRow> _rows = Array.Empty<SpawnRow>();
    private int _hovered = -1;

    /// <summary>The thumbnails of the types' pictures.</summary>
    public IconThumbs Thumbs { get; set; }

    /// <summary>The selected type (drawn highlighted), or null.</summary>
    public SpawnEntry Selected { get; set; }

    /// <summary>Whether a type is a favourite (its star is filled).</summary>
    public Func<SpawnEntry, bool> IsFavourite { get; set; } = _ => false;

    /// <summary>The rows are a tree's: a row is named by the last part of its path under its name, and a branch says how many types it holds.</summary>
    public bool Tree { get; set; }

    /// <summary>A type's row was clicked.</summary>
    public event Action<SpawnEntry> Picked;

    /// <summary>A row's star was clicked.</summary>
    public event Action<SpawnEntry> StarClicked;

    /// <summary>A branch was clicked on its arrow (anywhere, when it is no type itself) or twice on its row: it folds or unfolds.</summary>
    public event Action<string> Toggled;

    private float RowHeight => Mathf.Max(34f, GetThemeDefaultFontSize() * 2.5f);

    // A level of the tree, and the place of a branch's arrow before its picture.
    private float Step => RowHeight * 0.42f;

    // Where a row's picture begins: after the levels it stands under and its arrow's place.
    private float LeftOf(SpawnRow row) => Tree ? (row.Depth + 1) * Step : 0;

    public override void _Ready()
    {
        ClipContents = true;
        FocusMode = FocusModeEnum.Click;
        MouseFilter = MouseFilterEnum.Stop;
        _scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.RightWide);
        _scroll.ValueChanged += _ => QueueRedraw();
        AddChild(_scroll);
        Resized += Layout;
        MouseExited += () =>
        {
            _hovered = -1;
            QueueRedraw();
        };
    }

    /// <summary>Shows other rows; the view goes back to its top unless <paramref name="keepScroll"/>.</summary>
    public void SetRows(IReadOnlyList<SpawnRow> rows, bool keepScroll = false)
    {
        _rows = rows ?? Array.Empty<SpawnRow>();
        if (!keepScroll)
            _scroll.Value = 0;
        Layout();
    }

    /// <summary>Scrolls so that a type's row is in the middle of the view.</summary>
    public void ScrollTo(SpawnEntry entry)
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (ReferenceEquals(_rows[i].Entry, entry))
            {
                _scroll.Value = Mathf.Max(0, i * RowHeight - (Size.Y - RowHeight) / 2);
                break;
            }
        }
        QueueRedraw();
    }

    private void Layout()
    {
        _scroll.MaxValue = _rows.Count * RowHeight;
        _scroll.Page = Size.Y;
        _scroll.Visible = _scroll.MaxValue > Size.Y;
        QueueRedraw();
    }

    // a picture that was not made when its row was drawn is made by the next frames: the rows in view are drawn again until they have theirs
    public override void _Process(double delta)
    {
        if (_waiting && IsVisibleInTree())
            QueueRedraw();
    }

    private bool _waiting;

    public override void _Draw()
    {
        _waiting = false;
        Font font = GetThemeDefaultFont();
        int size = GetThemeDefaultFontSize();
        float row = RowHeight, width = Size.X - (_scroll.Visible ? _scroll.Size.X : 0);
        Color text = GetThemeColor("font_color", "Label"), dim = new(text, 0.55f);
        int first = Math.Max(0, (int)(_scroll.Value / row)), last = Math.Min(_rows.Count - 1, (int)((_scroll.Value + Size.Y) / row));
        for (int i = first; i <= last; i++)
        {
            SpawnRow shown = _rows[i];
            SpawnEntry entry = shown.Entry;
            float y = i * row - (float)_scroll.Value, left = LeftOf(shown);
            if (entry != null && ReferenceEquals(entry, Selected))
                DrawRect(new Rect2(0, y, width, row - 1), new Color(0.29f, 0.53f, 0.78f, 0.55f));
            else if (i == _hovered)
                DrawRect(new Rect2(0, y, width, row - 1), new Color(1, 1, 1, 0.06f));
            if (shown.Branch)
                DrawArrow(new Vector2(left - Step / 2, y + row / 2), Step * 0.3f, shown.Open, i == _hovered ? text : dim);
            float icon = row - 6;
            // (a branch that is no type of the list has no picture)
            if (entry != null && Thumbs != null && Thumbs.TryGet(entry.Icon, entry.IconState, out Thumb thumb))
            {
                if (thumb.Texture != null)
                {
                    // tg's pixel art at the largest whole scale that fits
                    float fit = icon / Math.Max(thumb.Width, thumb.Height), scale = fit >= 1 ? Mathf.Floor(fit) : fit;
                    var drawn = new Vector2(thumb.Width * scale, thumb.Height * scale);
                    DrawTextureRect(thumb.Texture, new Rect2(new Vector2(left + 3, y + 3) + (new Vector2(icon, icon) - drawn) / 2, drawn), false);
                }
            }
            else if (entry != null)
            {
                _waiting = true;
                DrawRect(new Rect2(left + 3, y + 3, icon, icon), new Color(1, 1, 1, 0.05f));
            }
            float tx = left + icon + 12, star = row * 0.5f;
            float textWidth = Math.Max(10, width - tx - star - 10);
            // flat: the name over the whole path. In a tree the path is the way down to the row: the last part of it, and what a branch holds
            string held = shown.Types == 1 ? "1 type" : shown.Types + " types";
            string under = !Tree ? entry?.Path : !shown.Branch ? shown.Leaf : entry == null ? held : shown.Leaf + "  ·  " + held;
            DrawString(font, new Vector2(tx, y + row * 0.42f), entry?.Name ?? shown.Leaf, HorizontalAlignment.Left, textWidth, size, text);
            DrawString(font, new Vector2(tx, y + row * 0.86f), under ?? string.Empty, HorizontalAlignment.Left, textWidth, Math.Max(9, size - 2), dim);
            if (entry == null)
                continue;
            bool favourite = IsFavourite(entry);
            if (favourite || i == _hovered)
                DrawStar(new Vector2(width - star * 0.5f - 6, y + row / 2), star * 0.42f, favourite);
        }
    }

    // A branch's arrow: to the right when it is folded, down when it shows what stands under it.
    private void DrawArrow(Vector2 centre, float radius, bool open, Color colour)
    {
        Vector2[] points = open
            ? new[] { centre + new Vector2(-radius, -radius * 0.6f), centre + new Vector2(radius, -radius * 0.6f), centre + new Vector2(0, radius * 0.8f) }
            : new[] { centre + new Vector2(-radius * 0.6f, -radius), centre + new Vector2(radius * 0.8f, 0), centre + new Vector2(-radius * 0.6f, radius) };
        DrawColoredPolygon(points, colour);
    }

    private void DrawStar(Vector2 centre, float radius, bool filled)
    {
        var points = new Vector2[10];
        for (int k = 0; k < 10; k++)
        {
            float r = k % 2 == 0 ? radius : radius * 0.45f, angle = -Mathf.Pi / 2 + k * Mathf.Pi / 5;
            points[k] = centre + new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
        }
        if (filled)
            DrawColoredPolygon(points, new Color(1f, 0.8f, 0.2f));
        else
            DrawPolyline(points.Append(points[0]).ToArray(), new Color(1, 1, 1, 0.5f), 1.2f, true);
    }

    public override void _GuiInput(InputEvent @event)
    {
        float row = RowHeight;
        switch (@event)
        {
            case InputEventMouseMotion motion:
                int over = (int)((motion.Position.Y + _scroll.Value) / row);
                over = over >= 0 && over < _rows.Count ? over : -1;
                if (over != _hovered)
                {
                    _hovered = over;
                    TooltipText = over >= 0 ? _rows[over].Entry?.Description ?? string.Empty : string.Empty;
                    QueueRedraw();
                }
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel:
                _scroll.Value += (wheel.ButtonIndex == MouseButton.WheelUp ? -3 : 3) * row * (wheel.Factor > 0 ? wheel.Factor : 1);
                AcceptEvent();
                break;
            case InputEventPanGesture pan:
                _scroll.Value += pan.Delta.Y * row * 0.5f; // the trackpad's two-finger scroll
                AcceptEvent();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                int at = (int)((click.Position.Y + _scroll.Value) / row);
                if (at < 0 || at >= _rows.Count)
                    break;
                SpawnRow hit = _rows[at];
                float width = Size.X - (_scroll.Visible ? _scroll.Size.X : 0);
                // a branch folds by its arrow, by its whole row when it is no type itself, and by a second click on a type's row (the first picked it)
                if (hit.Branch && (hit.Entry == null || click.Position.X < LeftOf(hit) || click.DoubleClick))
                    Toggled?.Invoke(hit.Path);
                else if (hit.Entry == null)
                    break;
                else if (click.Position.X > width - row * 0.5f - 12)
                    StarClicked?.Invoke(hit.Entry);
                else
                    Picked?.Invoke(hit.Entry);
                QueueRedraw();
                AcceptEvent();
                break;
        }
    }
}
