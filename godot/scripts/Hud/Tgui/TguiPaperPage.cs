using System.Linq;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;

namespace Anomaly.Client.Hud.Tgui;

// Gaps S3 (D298): PaperSheet's page (UiKind.PaperPage): the paper's HTML as the chat draws it, as tall as its text, on the paper's colour,
// tg's stamps over it at their places and turns, and tg's PaperSheetStamper: the held stamp follows the mouse half-transparent (Shift
// turns it toward the mouse, Stamper.tsx findStampPosition) and a left click stamps there.

/// <summary>The kit's <see cref="UiKind.PaperPage"/>.</summary>
public partial class TguiPaperPage : Control
{
    private const float Padding = 10; // tg: Preview.tsx:532 the page's p="10px"

    public UiNode Node;
    private RichTextLabel _text;
    private Control _ghost;
    private double _x, _y, _rotation;

    public static Control Build(UiNode node, TguiRenderEnv env)
    {
        var page = new TguiPaperPage { Node = node, MouseFilter = node.Ghost != null ? MouseFilterEnum.Stop : MouseFilterEnum.Pass, ClipContents = true };
        page._text = new RichTextLabel
        {
            BbcodeEnabled = false,
            FitContent = true,
            ScrollActive = false,
            SelectionEnabled = node.Ghost == null,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = node.Ghost != null ? MouseFilterEnum.Ignore : MouseFilterEnum.Pass,
        };
        ChatText.Style(page._text, 13);
        page._text.AddThemeColorOverride("default_color", Colors.Black);
        var links = new List<ChatLink>();
        var spans = new List<ChatSpan>();
        string plain = HtmlText.ToPlain(node.TextValue ?? "", links, spans);
        ChatText.Append(page._text, plain, spans, links, 13);
        page.AddChild(page._text);
        page._text.MinimumSizeChanged += page.UpdateMinimumSize;
        foreach (UiNode child in node.Children)
        {
            Control stamp = TguiRender.Build(child, env);
            if (stamp == null)
                continue;
            stamp.MouseFilter = MouseFilterEnum.Ignore;
            Place(stamp, child.PosX, child.PosY, child);
            page.AddChild(stamp);
        }
        if (node.Ghost != null)
        {
            page._ghost = TguiRender.Build(node.Ghost, env);
            page._ghost.MouseFilter = MouseFilterEnum.Ignore;
            page._ghost.Visible = false;
            Place(page._ghost, 0, 0, node.Ghost);
            page.AddChild(page._ghost);
        }
        return page;
    }

    private static void Place(Control control, double x, double y, UiNode node)
    {
        Vector2 size = UiSize.Of(node.Width) is UiSize { Pixels: > 0 } w && UiSize.Of(node.Height) is UiSize { Pixels: > 0 } h
            ? new Vector2((float)w.Pixels, (float)h.Pixels) : control.GetCombinedMinimumSize();
        control.Size = size;
        control.Position = new Vector2((float)x, (float)y);
        control.PivotOffset = size / 2;
        control.RotationDegrees = (float)node.Rotation;
    }

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Resized += Layout;
        Layout();
    }

    private void Layout()
    {
        _text.Position = new Vector2(Padding, Padding);
        _text.Size = new Vector2(Math.Max(0, Size.X - 2 * Padding), _text.GetCombinedMinimumSize().Y);
    }

    public override Vector2 _GetMinimumSize()
        => new(0, (_text?.GetCombinedMinimumSize().Y ?? 0) + 2 * Padding);

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), Node.Background != null ? TguiTheme.Hex(Node.Background) : Colors.White);
    }

    public override void _GuiInput(InputEvent input)
    {
        if (_ghost == null)
            return;
        if (input is InputEventMouseMotion motion)
        {
            // tg: Stamper.tsx:96 findStampPosition: the stamp's bottom middle at the mouse; Shift keeps it in place and turns it toward the mouse
            Vector2 size = _ghost.Size;
            bool rotating = motion.ShiftPressed;
            double currentHeight = rotating ? _y : motion.Position.Y - size.Y;
            double currentWidth = rotating ? _x : motion.Position.X - size.X / 2;
            double radians = Math.Atan2(currentWidth + size.X / 2 - motion.Position.X, currentHeight + size.Y - motion.Position.Y);
            _rotation = rotating ? radians * (180 / Math.PI) * -1 : _rotation;
            _x = Math.Clamp(currentWidth, 0, Math.Max(0, Size.X - size.X));
            _y = Math.Clamp(currentHeight, 0, Math.Max(0, Size.Y - size.Y));
            _ghost.Position = new Vector2((float)_x, (float)_y);
            _ghost.RotationDegrees = (float)_rotation;
            _ghost.Visible = true;
            AcceptEvent();
        }
        else if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            // tg: Stamper.tsx:66 handleMouseClick: add_stamp at the ghost's place (the page scrolls with the stamp: no y offset to add)
            Node.OnStamp?.Invoke(Math.Round(_x), Math.Round(_y), Math.Round(_rotation));
            AcceptEvent();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit && _ghost != null)
            _ghost.Visible = false;
    }
}
