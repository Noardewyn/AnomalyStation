namespace Anomaly.Client.Shared;

// ui-overhaul.md section 13: the chat in first person. Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// The round's chat window and the chat lines keep clear of the bottom row (the hands' row and the hint strip over it, LiveHud.Hands); the
// window is see-through while the player is not typing and shows its buttons and input only while the player types.

/// <summary>Where the round's chat window and the chat lines stand against the bottom row, and how see-through the window is.</summary>
public static class ChatPlace
{
    /// <summary>The chat window's least size, at its scale 1.</summary>
    public const float MinWidth = 360f, MinHeight = 240f;

    /// <summary>The round's chat window's default place and size as shares of the screen: at the bottom left (its top's share applies where no part of the bottom row stands under it).</summary>
    public const float RoundLeft = 0.006f, RoundTop = 0.631f, RoundWidth = 0.39f, RoundHeight = 0.319f;

    /// <summary>
    /// The lowest a block of the chat spanning <paramref name="left"/> to <paramref name="right"/> (screen pixels) may reach: <see cref="UiTokens.ChatRowGap"/>
    /// above the top of what of the bottom row stands under it (the hands' row with both boxes at the active size, and the hint strip's largest place,
    /// <see cref="LiveHud.HintSpan"/>, whatever it names); null where none of it does. It depends on the screen and the scale alone.
    /// </summary>
    public static float? BottomClear(in HudMetrics metrics, float left, float right)
    {
        HandsLayout row = LiveHud.Hands(metrics, leftActive: true, rightActive: true);
        HudRect strip = LiveHud.HintSpan(metrics, row);
        float top = metrics.Height;
        foreach (HudRect part in new[] { row.Left, row.Hotbar, row.Right, strip })
        {
            if (left < part.Right && part.Left < right)
                top = Math.Min(top, part.Top);
        }
        return top < metrics.Height ? top - MathF.Round(UiTokens.ChatRowGap * UiTokens.Unit(metrics)) : null;
    }

    /// <summary>
    /// The round's chat window's place with no saved place or size, screen pixels, whole: its shares of the screen (<see cref="RoundWidth"/>,
    /// <see cref="RoundHeight"/>, at least its least size at <paramref name="windowScale"/>) at the left, its bottom <see cref="UiTokens.ChatRowGap"/>
    /// above what of the bottom row stands under it (<see cref="BottomClear"/>), so the bar is never under it; with none under it, its top at
    /// <see cref="RoundTop"/> (kept on the screen). A window that would leave the screen at the top is made lower instead.
    /// </summary>
    public static HudRect RoundDefault(in HudMetrics metrics, float windowScale)
    {
        float left = MathF.Round(RoundLeft * metrics.Width);
        float width = MathF.Round(Math.Max(MinWidth * windowScale, RoundWidth * metrics.Width));
        float height = MathF.Round(Math.Max(MinHeight * windowScale, RoundHeight * metrics.Height));
        float bottom = BottomClear(metrics, left, left + width) is float clear
            ? clear
            : Math.Min(metrics.Height, MathF.Round(RoundTop * metrics.Height) + height);
        height = Math.Min(height, bottom);
        return new HudRect(left, bottom - height, width, height);
    }

    /// <summary>
    /// Where the chat window stands, screen pixels: at the place (<paramref name="share"/>: its top left as a share of the screen) and size
    /// (<paramref name="size"/>, at scale 1, times <paramref name="scale"/>) the player gave it, each where there is one, else at
    /// <paramref name="fallback"/>'s; its top left moved as little as keeps it on the screen.
    /// </summary>
    public static HudRect Placed(HudRect fallback, float screenWidth, float screenHeight, float scale, (float X, float Y)? share, (float Width, float Height)? size)
    {
        float width = size.HasValue ? size.Value.Width * scale : fallback.Width, height = size.HasValue ? size.Value.Height * scale : fallback.Height;
        float left = share.HasValue ? share.Value.X * screenWidth : fallback.Left, top = share.HasValue ? share.Value.Y * screenHeight : fallback.Top;
        return new HudRect(Math.Clamp(left, 0, Math.Max(0, screenWidth - width)), Math.Clamp(top, 0, Math.Max(0, screenHeight - height)), width, height);
    }

    /// <summary>
    /// Slice 4A: where the round's chat window stands: docked at <paramref name="dock"/> (<see cref="TabInventory.ChatDock"/>, the Tab inventory's
    /// left column) while there is one, else at its own place <paramref name="own"/> (its default or the player's), which docking leaves as it was.
    /// Whether the window is shown is its own: docking neither opens nor closes it.
    /// </summary>
    public static HudRect Docked(HudRect own, HudRect? dock) => dock ?? own;

    /// <summary>
    /// Whether a press and release on the chat window's title bar or edge moved or resized it (its place and its size at scale 1 before and after):
    /// only then is the place and size saved as the player's own; a plain click saves nothing, so the default place keeps following the screen.
    /// </summary>
    public static bool GeometryChanged(HudRect before, HudRect after) => before != after;

    /// <summary>
    /// The share of its opacity the round's chat window's backing (its panel, title bar and tabs) is drawn at: whole while the player types in it
    /// (<paramref name="typing"/>) or the free pointer is over it in cursor mode, <see cref="UiTokens.ChatRestAlpha"/> otherwise. The log's text is
    /// always whole.
    /// </summary>
    public static float BackingAlpha(bool typing, bool cursorMode, bool pointerOver)
        => typing || (cursorMode && pointerOver) ? 1f : UiTokens.ChatRestAlpha;

    /// <summary>The chat lines' left edge, screen pixels, at their scale <paramref name="scale"/> (the HUD's text scale times their own share).</summary>
    public static float LinesLeft(float scale) => 16f * scale;

    /// <summary>The chat lines' width: at most a third of the screen; their text and boxes wrap there.</summary>
    public static float LinesWidth(float screenWidth, float scale) => Math.Max(200f, screenWidth / 3f - 16f * scale);

    /// <summary>
    /// Where the chat lines' newest block ends, screen pixels: 20 of their units under 0.8 of the screen's height, and with
    /// <paramref name="bottomRow"/> (in the round) no lower than <see cref="BottomClear"/> where their block's width reaches over the bottom row.
    /// </summary>
    public static float LinesBottom(in HudMetrics metrics, float scale, bool bottomRow)
    {
        float bottom = metrics.Height - MathF.Round(metrics.Height * 0.2f) + 20f * scale;
        float left = LinesLeft(scale);
        return bottomRow && BottomClear(metrics, left, left + LinesWidth(metrics.Width, scale)) is float clear ? Math.Min(bottom, clear) : bottom;
    }
}

/// <summary>
/// Whether the chat window's row of chat buttons and its input row are shown (ui-overhaul.md section 13). In the round (<see cref="Round"/>) they are
/// hidden until the player goes to type (a chat key, or a click in the window under Tab) and hidden again when the input is sent, cancelled or loses
/// focus; the lobby's window always shows them. Whenever the input has the keyboard, however it got it, they are shown: a change of place never hides
/// them under a focused input.
/// </summary>
public sealed class ChatInputRows
{
    private bool _typing;

    /// <summary>The window is the round's (false: the lobby's, or one placed nowhere yet: the rows always show).</summary>
    public bool Round { get; set; }

    public bool Shown => !Round || _typing;

    /// <summary>The input took the keyboard (a chat key, a button of the window that puts the caret in it, a click into it), in either place.</summary>
    public void Type() => _typing = true;

    /// <summary>
    /// A click (a press and release, not a drag) in cursor mode on the window's own body, not on one of its buttons or scroll bars nor on anything
    /// drawn over it (<paramref name="onBody"/>): it shows the rows (true: the input takes the keyboard).
    /// </summary>
    public bool Click(bool cursorMode, bool onBody)
    {
        if (cursorMode && onBody)
            _typing = true;
        return cursorMode && onBody;
    }

    /// <summary>A line was sent: true when the input then lets the keyboard go (the round); in the lobby it keeps it for the next line.</summary>
    public bool Sent()
    {
        if (Round)
            _typing = false;
        return Round;
    }

    /// <summary>The input lost the keyboard (Esc, a click elsewhere, the window closed).</summary>
    public void Leave()
    {
        _typing = false;
        _pending = false;
    }

    private bool _pending;

    /// <summary>
    /// The input lost the keyboard because the interface was hidden under it (a cinematic, the round starting): the line being typed stays, and
    /// <see cref="Resume"/> decides whether it takes the keyboard back.
    /// </summary>
    public void LostWhileHidden() => _pending = _typing;

    /// <summary>
    /// Per frame after <see cref="LostWhileHidden"/>: another control holding the keyboard (<paramref name="otherFocused"/>: the player typed somewhere
    /// else meanwhile) drops the take-back and ends the typing, as any focus loss; otherwise the input takes the keyboard back once it is visible
    /// again (<paramref name="inputVisible"/>), and waits while it is not.
    /// </summary>
    public ChatResume Resume(bool inputVisible, bool otherFocused)
    {
        if (!_pending)
            return ChatResume.None;
        if (otherFocused)
        {
            Leave();
            return ChatResume.Dropped;
        }
        if (!inputVisible)
            return ChatResume.Waiting;
        _pending = false;
        return ChatResume.TakeBack;
    }
}

/// <summary>What <see cref="ChatInputRows.Resume"/> decided.</summary>
public enum ChatResume : byte
{
    /// <summary>Nothing pending.</summary>
    None,

    /// <summary>Still hidden: the take-back waits.</summary>
    Waiting,

    /// <summary>The input takes the keyboard back.</summary>
    TakeBack,

    /// <summary>Another control took the keyboard: the take-back is dropped and the rows hide.</summary>
    Dropped,
}
