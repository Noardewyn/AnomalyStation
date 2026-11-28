using Anomaly.Core.DmShim;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: ui-overhaul.md section 6 (package 2, slice A). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>One prompt beside the crosshair: the key chip's label and the server's hint text.</summary>
public readonly record struct PromptLine(string Key, string Text);

/// <summary>
/// Keys as a player reads them, and the prompts for what the crosshair is on (ui-overhaul.md section 6): every key shown comes from
/// the player's bindings (<see cref="KeyBindings"/>), never a literal.
/// </summary>
public static class KeyChips
{
    /// <summary>The most click and key lines drawn beside the crosshair; the stop-pulling line comes on top of them (<see cref="MaxPromptLines"/>).</summary>
    public const int MaxClickLines = 6;

    /// <summary>The most lines in all: <see cref="MaxClickLines"/> and the stop-pulling line.</summary>
    public const int MaxPromptLines = MaxClickLines + 1;

    /// <summary>How far right of the free pointer's hot spot its arrow reaches, screen pixels (the system pointer at its usual size).</summary>
    public const float PointerReach = 20f;

    /// <summary>
    /// A binding name as a player reads it: <c>MouseLeft</c> is <c>LMB</c>, <c>Ctrl_Shift_T</c> is <c>Ctrl+Shift+T</c>, <c>Key1</c> and
    /// <c>Kp1</c> are <c>1</c>, a punctuation key is its character, <c>Escape</c> is <c>Esc</c>; any other key keeps its Godot name.
    /// </summary>
    public static string Label(string binding)
    {
        if (string.IsNullOrEmpty(binding))
            return string.Empty;
        string[] parts = binding.Split('_');
        for (int i = 0; i < parts.Length; i++)
            parts[i] = KeyLabel(parts[i]);
        return string.Join("+", parts);
    }

    private static string KeyLabel(string key) => key switch
    {
        "MouseLeft" => "LMB",
        "MouseRight" => "RMB",
        "MouseMiddle" => "MMB",
        "Semicolon" => ";",
        "Apostrophe" => "'",
        "Bracketleft" => "[",
        "Bracketright" => "]",
        "Quoteleft" => "`",
        "Backslash" => "\\",
        "Comma" => ",",
        "Period" => ".",
        "Slash" => "/",
        "Minus" => "-",
        "Equal" => "=",
        "Escape" => "Esc",
        "Capslock" => "Caps Lock",
        _ when key.Length == 4 && key.StartsWith("Key", StringComparison.Ordinal) && char.IsAsciiDigit(key[3]) => key[3..],
        _ when key.Length == 3 && key.StartsWith("Kp", StringComparison.Ordinal) && char.IsAsciiDigit(key[2]) => key[2..],
        _ => key,
    };

    /// <summary>The label of <paramref name="action"/>'s first bound key, or null when it is unbound.</summary>
    public static string KeyOf(KeyBindings bindings, PlayAction action)
    {
        IReadOnlyList<string> keys = bindings.KeysOf(action);
        return keys.Count == 0 ? null : Label(keys[0]);
    }

    /// <summary>
    /// The label of the first plain mouse button bound to <paramref name="action"/>, or null: the game acts on the primary and secondary
    /// click only from the mouse (GameScene.OnMouseButton), so a keyboard key bound to them is not what the player presses.
    /// </summary>
    public static string MouseButtonOf(KeyBindings bindings, PlayAction action)
    {
        foreach (string key in bindings.KeysOf(action))
        {
            if (KeyBindings.IsMouseButton(key))
                return Label(key);
        }
        return null;
    }

    // The actions a line is pressed with, in the order the prompt lists them (ui-overhaul.md section 12, "The hints, improved"): the left click with
    // the held item, the world interact key (tg's empty-hand click), then tg's screentip keys (code/__DEFINES/screentips.dm: RMB, Alt-LMB, Ctrl-LMB,
    // Shift-LMB), the move-pulled key, and the port's stop pulling.
    private static readonly PlayAction[] Order =
    {
        PlayAction.PrimaryClick, PlayAction.WorldInteract, PlayAction.SecondaryClick, PlayAction.AltInteract, PlayAction.Pull, PlayAction.Examine,
        PlayAction.MovePulled, PlayAction.StopPulling,
    };

    private static PlayAction? ActionOf(ContextHint hint) => hint.Action switch
    {
        ContextHintAction.Click => (hint.Button, hint.Modifiers) switch
        {
            (ClickButton.Left, ClickModifiers.None) => PlayAction.PrimaryClick,
            (ClickButton.Right, ClickModifiers.None) => PlayAction.SecondaryClick,
            (ClickButton.Left, ClickModifiers.Shift) => PlayAction.Examine,
            (ClickButton.Left, ClickModifiers.Ctrl) => PlayAction.Pull,
            (ClickButton.Left, ClickModifiers.Alt) => PlayAction.AltInteract, // tg's alt-click: Alt+left click, or the alt interact key
            _ => null, // Alt+right, Ctrl+right, Ctrl+Shift: no action of their own to name a key
        },
        ContextHintAction.StopPulling => PlayAction.StopPulling,
        _ => null, // the radial's drags and extras are no key press
    };

    private static string ChipOf(KeyBindings bindings, PlayAction action) => action switch
    {
        PlayAction.PrimaryClick or PlayAction.SecondaryClick => MouseButtonOf(bindings, action),
        // tg's alt-click is also Alt with the primary click, which is not rebindable: its chip when the alt interact key is unbound
        PlayAction.AltInteract => KeyOf(bindings, action) ?? (MouseButtonOf(bindings, PlayAction.PrimaryClick) is string click ? "Alt+" + click : null),
        _ => KeyOf(bindings, action),
    };

    /// <summary>
    /// The prompt lines for the hovered target: one per way to act on it, in <see cref="Order"/>, each with the player's own key for it (the primary
    /// and secondary click's mouse button, <see cref="MouseButtonOf"/>; the first key of the others): tg's screentip entries, the first hint of each
    /// chord; the world interact key with the server's empty-hand line (<see cref="ContextHintsMessage.EmptyHand"/>, sent only while an item is held);
    /// the move-pulled key's "Move here" on a turf the server marks (<see cref="ContextHintsMessage.MoveHere"/>). A chord no action is pressed with, or
    /// whose action has no key, is left out. At most <see cref="MaxClickLines"/> lines, and the stop-pulling line after them when the server sent
    /// one, so it is never the line cut.
    /// </summary>
    public static List<PromptLine> Prompts(ContextHintsMessage hints, KeyBindings bindings)
    {
        var found = new PromptLine?[Order.Length];
        void Found(PlayAction action, string label)
        {
            int rank = Array.IndexOf(Order, action);
            if (found[rank] == null && !string.IsNullOrEmpty(label) && ChipOf(bindings, action) is string key)
                found[rank] = new PromptLine(key, label);
        }
        if (hints != null)
        {
            foreach (ContextHint hint in hints.Hints)
            {
                if (ActionOf(hint) is PlayAction action)
                    Found(action, hint.Label);
            }
            Found(PlayAction.WorldInteract, hints.EmptyHand);
            if (hints.MoveHere)
                Found(PlayAction.MovePulled, I18n.T("Move here"));
        }
        var lines = new List<PromptLine>(MaxPromptLines);
        for (int rank = 0; rank < Order.Length; rank++)
        {
            if (found[rank] is PromptLine line && (Order[rank] == PlayAction.StopPulling || lines.Count < MaxClickLines))
                lines.Add(line);
        }
        return lines;
    }

    /// <summary>The server's newest hints when they answer a hover of <paramref name="target"/> (its id, or its cell for a turf or ceiling), else null.</summary>
    public static ContextHintsMessage AnswerFor(ContextHintsMessage newest, in ClickTarget target)
    {
        if (newest == null || !target.IsSet || newest.TargetKind != target.Kind)
            return null;
        return target.Kind is TargetKind.Ceiling or TargetKind.Turf
            ? newest.TurfX == target.X && newest.TurfY == target.Y && newest.TurfZ == target.Z ? newest : null
            : newest.Target == target.Id ? newest : null;
    }

    /// <summary>
    /// DV691: the name the prompt shows beside the crosshair, null for none. <paramref name="answer"/> is the server's answer for the target
    /// (<see cref="AnswerFor"/>), <paramref name="ownName"/> the client's name for it. Until the answer comes, a real server's player sees nothing
    /// (the thing may be out of reach); the fake server, which answers few hovers, shows the client's name. A zero-length name is the server's
    /// "out of reach": bare. A null name is tg's "no screentip" (every floor): the client's name.
    /// </summary>
    public static string PromptName(ContextHintsMessage answer, bool realServer, string ownName)
        => answer == null ? (realServer ? null : ownName) : answer.Name is { Length: 0 } ? "" : ownName;

    /// <summary>
    /// The thing's name the server gave, null for none: tg's "no screentip" (null) and DV691's "out of reach" (zero-length) give none, so the
    /// radial's title and the dev tools fall back to the client's own name; only the prompt goes bare (<see cref="PromptName"/>).
    /// </summary>
    public static string ServerName(ContextHintsMessage answer) => string.IsNullOrEmpty(answer?.Name) ? null : answer.Name;

    /// <summary>The prompt block's height in reference pixels: the name line and <paramref name="lines"/> prompt lines.</summary>
    public static float PromptHeight(int lines) => UiTokens.PromptNameLine + Math.Clamp(lines, 0, MaxPromptLines) * UiTokens.PromptLine;

    /// <summary>
    /// How far right of the screen's middle the drawn crosshair reaches, screen pixels (Hud/OptionsPanel.cs, Crosshair, draws from the same
    /// numbers): the target crosshair's arms at the options' size <paramref name="scale"/>, or with a gun (DV96) the sight's lines out from
    /// the gap of the gun's cone <paramref name="coneDegrees"/> with a shot's whole bloom (<see cref="GunSightGap.Pixels"/>), so the prompts
    /// do not move with each shot; the lines' shadow included.
    /// </summary>
    public static float SightReach(bool gun, float coneDegrees, float fovDegrees, float heightPixels, float scale) => gun
        ? GunSightGap.Pixels(coneDegrees, fovDegrees, heightPixels, 0) + (GunSightGap.LinePixels + GunSightGap.ShadowPixels) * scale
        : (GunSightGap.ArmEndPixels + GunSightGap.ShadowPixels) * scale;

    /// <summary>
    /// Where the prompt block goes, in screen pixels: to the lower right of its anchor (the crosshair at the screen's middle, or the free
    /// pointer in cursor mode; the wireframes' Mockup 1), past what is drawn there (<paramref name="reach"/>, screen pixels right of the
    /// anchor: <see cref="SightReach"/> or <see cref="PointerReach"/>), as wide as its widest line (<paramref name="width"/>) up to
    /// <see cref="UiTokens.PromptMaxWidth"/>. It stays on the screen: where the right side has no room it goes to the anchor's left, mirrored,
    /// else it is narrowed; it is moved up or down to fit the screen's height. At the screen's middle its bottom stays above the balloon
    /// alert's row (<see cref="BalloonRow"/>) at every HUD scale.
    /// <paramref name="rightEdge"/> (the vitals' left edge less its gap, in the captured view; ui-overhaul.md section 6): the block never
    /// reaches past it; one wider is narrowed to end there, not moved (the caller cuts its text, <see cref="Ellipsize"/>), unless less than
    /// <see cref="UiTokens.PromptMinWidth"/> is left there (a very large HUD scale on a small screen), when it is placed as without the panel.
    /// </summary>
    public static HudRect PromptBlock(HudMetrics metrics, int lines, float width, float anchorX, float anchorY, float reach, float rightEdge = float.PositiveInfinity)
    {
        float u = UiTokens.Unit(metrics);
        float margin = UiTokens.ScreenMargin * u;
        float height = PromptHeight(lines) * u;
        float gap = Math.Max(0f, reach) + UiTokens.PromptOffsetX * u;
        float w = Math.Clamp(width, 0f, Math.Min(UiTokens.PromptMaxWidth * u, Math.Max(0f, metrics.Width - 2 * margin)));
        float left = anchorX + gap;
        float rightRoom = Math.Min(metrics.Width - margin, rightEdge) - left, leftRoom = anchorX - gap - margin;
        if (w > rightRoom && !float.IsPositiveInfinity(rightEdge) && rightRoom >= UiTokens.PromptMinWidth * u)
        {
            w = rightRoom;
        }
        else if (w > rightRoom)
        {
            if (w <= leftRoom)
                left = anchorX - gap - w;
            else if (leftRoom > rightRoom + 1f) // the right side unless the left has clearly more room (at the screen's middle they are even)
                (left, w) = (margin, Math.Max(0f, leftRoom));
            else
                w = Math.Max(0f, rightRoom);
        }
        left = Math.Clamp(left, 0f, Math.Max(0f, metrics.Width - margin - w)); // only a screen narrower than the sight moves it over the anchor
        // a block of more than four lines is raised so its bottom stays where a four-line block's is (above the balloon row under the crosshair)
        float top = Math.Min(anchorY + UiTokens.PromptOffsetY * u, anchorY + (UiTokens.PromptOffsetY + PromptHeight(LinesBelowAnchor)) * u - height);
        top = Math.Max(margin, Math.Min(top, metrics.Height - margin - height));
        return new HudRect(left, top, w, height);
    }

    // The most lines a block holds below its usual top; a taller one grows upwards.
    private const int LinesBelowAnchor = 4;

    /// <summary>The mark a cut text ends with.</summary>
    public const string Ellipsis = "…";

    /// <summary>
    /// <paramref name="text"/> as it fits in <paramref name="width"/> pixels by <paramref name="measure"/>: whole, or its longest start that fits
    /// with <see cref="Ellipsis"/> after it (just the mark, or nothing, when even that does not fit).
    /// </summary>
    public static string Ellipsize(string text, float width, Func<string, float> measure)
    {
        if (string.IsNullOrEmpty(text) || measure(text) <= width)
            return text ?? string.Empty;
        int low = 0, high = text.Length - 1; // the longest start (low characters) that fits with the mark
        while (low < high)
        {
            int mid = (low + high + 1) / 2;
            if (measure(text.Substring(0, mid).TrimEnd() + Ellipsis) <= width)
                low = mid;
            else
                high = mid - 1;
        }
        if (low == 0)
            return measure(Ellipsis) <= width ? Ellipsis : string.Empty;
        return text.Substring(0, low).TrimEnd() + Ellipsis;
    }

    /// <summary>The row the balloon alert under the crosshair takes, in screen pixels (ChatLines draws it from the same tokens).</summary>
    public static HudRect BalloonRow(HudMetrics metrics)
    {
        float u = UiTokens.Unit(metrics);
        float width = Math.Min(UiTokens.BalloonWidth * u, metrics.Width - 32f);
        float baseline = metrics.Height / 2f + UiTokens.BalloonBaseline * u;
        // the fallback font's ascent is about 1.07 of its size and its descent about 0.29: the row takes a little more of each
        float size = UiTokens.BalloonFont * u;
        return new HudRect((metrics.Width - width) / 2f, baseline - size * 1.1f, width, size * 1.45f);
    }
}
