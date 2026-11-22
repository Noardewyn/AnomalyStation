namespace Anomaly.Protocol;

// Protocol version 19 (tg's tooltips; docs/ai/specs/protocol.md section 28; DV47). tg shows a tooltip for its HUD objects: an alert's or
// an action button's MouseEntered calls openToolTip(usr, src, params, title = name, content = desc, theme), which hands them to the
// client's /datum/tooltip (code/modules/tooltip/tooltip.dm); that datum writes "<h1>title</h1><p>content</p>" (or one of them as a
// <p>) and a theme (a CSS class of tooltip.html) into a browser window of the BYOND client, whose JavaScript sizes, places and shows
// it; MouseExited's closeToolTip hides the window (winshow). The server keeps tg's DM side whole; the window's output becomes a
// Tooltip message (shown with tg's text and theme, or hidden), and the client draws the box itself in tooltip.html's look.
//
// There is no new action kind: the last action kind is still ActionKinds.Last (MessagesV17.cs).

/// <summary>What a <see cref="TooltipMessage"/> does.</summary>
public enum TooltipKind : byte
{
    /// <summary>tg's <c>tooltip.update</c>: the tooltip shows this text in this theme (a new one replaces what was shown).</summary>
    Show = 0,

    /// <summary>tg's <c>do_hide</c> (<c>winshow(owner, control, FALSE)</c>): the tooltip is hidden.</summary>
    Hide = 1,
}

/// <summary>
/// Version 19, server to client on the State channel after the tick's Delta: the session's tooltip was shown (tg's HTML text, as
/// <c>/datum/tooltip/show</c> builds it: <c>"&lt;h1&gt;title&lt;/h1&gt;&lt;p&gt;content&lt;/p&gt;"</c>, a title alone as
/// <c>"&lt;p&gt;title&lt;/p&gt;"</c>, a content alone as <c>"&lt;p&gt;content&lt;/p&gt;"</c>; and the theme, a class of tg's
/// <c>tooltip.html</c>: <c>"default"</c>, <c>"midnight"</c>, <c>"alien"</c>, ...) or hidden. A session has one tooltip (tg's
/// <c>client.tooltips</c>); the server sends a message only when what the client shows changes.
/// </summary>
public sealed class TooltipMessage
{
    public TooltipKind Kind { get; set; }

    /// <summary>For <see cref="TooltipKind.Show"/>: tg's HTML text (1 to <see cref="Limits.MaxTooltipTextBytes"/> bytes); else null.</summary>
    public string Text { get; set; }

    /// <summary>
    /// For <see cref="TooltipKind.Show"/>: tg's theme (1 to <see cref="Limits.MaxTooltipThemeBytes"/> bytes of lowercase letters, digits,
    /// '-' and '_': a CSS class name); else null. A theme the client does not know is drawn as <c>"default"</c>, as the page's CSS does.
    /// </summary>
    public string Theme { get; set; }

    /// <summary>True when <paramref name="theme"/> is a theme the wire carries (see <see cref="Theme"/>).</summary>
    public static bool IsValidTheme(string theme)
    {
        if (string.IsNullOrEmpty(theme) || theme.Length > Limits.MaxTooltipThemeBytes)
            return false;
        foreach (char c in theme)
        {
            if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_'))
                return false;
        }
        return true;
    }
}
