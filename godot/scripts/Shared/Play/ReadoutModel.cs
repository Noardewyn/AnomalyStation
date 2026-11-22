namespace Anomaly.Client.Shared;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// D184: the scan readout panel by the chat. A chat message whose HTML is tg's <c>boxed_message</c> (<c>__DEFINES/chat.dm:48</c>:
/// the health analyzer's scans, the chemical and wound scans, the penlight's and the stethoscope's examinations, the gas analyzer's
/// readout) opens whole in the panel, since six chat lines of 240 characters cannot show a scan. The server sends it to its one
/// recipient only (D40); the panel shows the newest one, until Esc closes it or the next one replaces it.
/// </summary>
public sealed class ReadoutModel
{
    /// <summary>The plain text shown (<see cref="HtmlText.ToPlain(string)"/> of the message), empty before the first.</summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>Whether the panel is shown.</summary>
    public bool Open { get; private set; }

    /// <summary>Counts every change, for the view to redraw.</summary>
    public int Version { get; private set; }

    /// <summary>Whether <paramref name="html"/> is tg's boxed message (its <c>div</c> of class <c>boxed_message</c>, with or without more classes),
    /// except the role greeting (<c>get_spawn_message</c>, an <c>infoplain</c> span) and the tip of the round (<c>send_tip_of_the_round</c>,
    /// a <c>purple_box</c>): they read fine whole in the chat.</summary>
    public static bool IsReadout(string html)
        => html != null && (html.Contains("class='boxed_message", StringComparison.Ordinal) || html.Contains("class=\"boxed_message", StringComparison.Ordinal))
            && !html.Contains("class='infoplain'", StringComparison.Ordinal) && !html.Contains("purple_box", StringComparison.Ordinal);

    /// <summary>The chat line of a readout: its first line only, the whole is in the panel.</summary>
    public static string Headline(string plain)
    {
        if (string.IsNullOrEmpty(plain))
            return string.Empty;
        int end = plain.IndexOf('\n');
        return end < 0 ? plain : plain.Substring(0, end);
    }

    /// <summary>Opens the panel on a chat message when it is a readout; false (nothing changes) otherwise.</summary>
    public bool Offer(string html)
    {
        if (!IsReadout(html))
            return false;
        string plain = HtmlText.ToPlain(html);
        if (plain.Length == 0)
            return false;
        Show(plain);
        return true;
    }

    /// <summary>Opens the panel on <paramref name="plain"/> (the dev command <c>analyze</c>'s reply too).</summary>
    public void Show(string plain)
    {
        Text = plain ?? string.Empty;
        Open = true;
        Version++;
    }

    /// <summary>The panel's close button: closes it; false when it was not open.</summary>
    public bool Close()
    {
        if (!Open)
            return false;
        Open = false;
        Version++;
        return true;
    }

    /// <summary>The panel's key (F9): shows the last readout again or hides it.</summary>
    public void Toggle()
    {
        Open = !Open;
        Version++;
    }
}
