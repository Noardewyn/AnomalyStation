using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: M4a P7 (client interaction). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// docs/ai/specs/interaction.md 9, D128: the feedback until M9: tg's chat lines, balloon alerts, the progress bar.

/// <summary>One line of chat as drawn: the plain text, how opaque it is now, (protocol version 24, D154) its links, or null, and (M9) its runs in tg's chat styles, or null for the plain chat colour.</summary>
public readonly record struct ChatLine(string Text, float Alpha, IReadOnlyList<ChatLink> Links = null, IReadOnlyList<ChatSpan> Spans = null)
{
    /// <summary>The style of the character at <paramref name="index"/> (tg's chat colour where no span says otherwise).</summary>
    public ChatStyle StyleAt(int index)
    {
        if (Spans != null)
        {
            foreach (ChatSpan span in Spans)
            {
                if (index >= span.Start && index < span.Start + span.Length)
                    return span.Style;
            }
        }
        return ChatStyles.Base;
    }
}

/// <summary>
/// The last few lines of tg's <c>to_chat</c> (<see cref="ServerText"/>) at the lower left (D128): the newest at the bottom, each fading out after
/// <see cref="StaySeconds"/>. Balloon alerts (<c>type == "balloon"</c>) are not chat: they go to <see cref="BalloonModel"/>.
/// </summary>
public sealed class ChatFeed
{
    /// <summary>How many lines are kept (interaction.md 9: the last 6).</summary>
    public const int Capacity = 6;

    public const double StaySeconds = 10.0;
    public const double FadeSeconds = 2.0;

    /// <summary>The characters a line keeps; a line with links keeps them up to its last link's end, at most <see cref="MaxLinkedChars"/>.</summary>
    public const int MaxChars = 240;

    /// <summary>Version 24: the characters a line with links keeps at most (a self-check's report holds its embedded objects' links last).</summary>
    public const int MaxLinkedChars = 1024;

    /// <summary>A kept line (D319: the handle <see cref="Retext"/> takes).</summary>
    public sealed class Line
    {
        internal string Text;
        internal double At;
        internal IReadOnlyList<ChatLink> Links;
        internal IReadOnlyList<ChatSpan> Spans;
    }

    private readonly List<Line> _lines = new();

    /// <summary>Adds a line (the HTML made plain; an empty one is dropped) with its links and styles (<see cref="HtmlText.ToPlain(string, List{ChatLink}, List{ChatSpan})"/>), if any.</summary>
    public Line Add(string plain, double now, IReadOnlyList<ChatLink> links = null, IReadOnlyList<ChatSpan> spans = null)
    {
        if (string.IsNullOrWhiteSpace(plain))
            return null;
        var line = new Line { At = now };
        Fill(line, plain, links, spans);
        _lines.Add(line);
        while (_lines.Count > Capacity)
            _lines.RemoveAt(0);
        return line;
    }

    /// <summary>D319: a line's text changes (its translation came), its time kept; nothing when it is gone.</summary>
    public void Retext(Line line, string plain, IReadOnlyList<ChatLink> links, IReadOnlyList<ChatSpan> spans)
    {
        if (line != null && _lines.Contains(line))
            Fill(line, plain, links, spans);
    }

    private static void Fill(Line line, string plain, IReadOnlyList<ChatLink> links, IReadOnlyList<ChatSpan> spans)
    {
        int keep = MaxChars;
        if (links != null && links.Count > 0)
            keep = Math.Min(MaxLinkedChars, Math.Max(MaxChars, links.Max(l => l.Start + l.Length)));
        string text = plain.Length > keep ? plain.Substring(0, keep) : plain;
        List<ChatLink> kept = links?.Where(l => l.Start + l.Length <= text.Length).ToList();
        List<ChatSpan> styled = spans?.Where(sp => sp.Start < text.Length).Select(sp => sp with { Length = Math.Min(sp.Length, text.Length - sp.Start) }).ToList();
        line.Text = text;
        line.Links = kept != null && kept.Count > 0 ? kept : null;
        line.Spans = styled != null && styled.Count > 0 ? styled : null;
    }

    /// <summary>The lines still shown at <paramref name="now"/>, oldest first.</summary>
    public List<ChatLine> Visible(double now)
    {
        var shown = new List<ChatLine>();
        foreach (Line line in _lines)
        {
            double age = now - line.At;
            if (age >= StaySeconds + FadeSeconds)
                continue;
            shown.Add(new ChatLine(line.Text, age <= StaySeconds ? 1f : (float)(1.0 - (age - StaySeconds) / FadeSeconds), line.Links, line.Spans));
        }
        return shown;
    }

    /// <summary>Lines kept (shown or faded).</summary>
    public int Count => _lines.Count;
}

/// <summary>The balloon alert under the crosshair (tg's <c>balloon_alert</c>, D128): the newest text for a couple of seconds.</summary>
public sealed class BalloonModel
{
    public const double StaySeconds = 2.2;

    private string _text;
    private double _at;

    /// <summary>The <see cref="ServerText.Type"/> the host gives a balloon alert.</summary>
    public const string TextType = "balloon";

    public void Add(string plain, double now)
    {
        if (string.IsNullOrWhiteSpace(plain))
            return;
        _text = plain;
        _at = now;
    }

    /// <summary>The text to show at <paramref name="now"/>, with its alpha; null when none.</summary>
    public string Current(double now, out float alpha)
    {
        alpha = 0;
        if (_text == null)
            return null;
        double age = now - _at;
        if (age >= StaySeconds)
            return null;
        alpha = age < StaySeconds - 0.5 ? 1f : (float)((StaySeconds - age) / 0.5);
        return _text;
    }
}

/// <summary>One timed action's progress bar (tg's <c>/datum/progressbar</c> image, in the private images).</summary>
/// <param name="ImageId">The private image's id.</param>
/// <param name="AttachedTo">The entity it hangs on (0: under the crosshair).</param>
/// <param name="Fraction">0 to 1.</param>
/// <param name="Holder">Version 27: the atom it hangs on wherever that is (an air tank on the back), 0 for none.</param>
/// <param name="HolderName">Version 27: that atom's name, or null.</param>
public readonly record struct ProgressBarInfo(long ImageId, long AttachedTo, double Fraction, long Holder = 0, string HolderName = null);

/// <summary>Reads tg's progress bars out of the replica's private images (<c>prog_bar_NN</c>, NN the percent in steps of 5).</summary>
public static class ProgressBars
{
    private const string Prefix = "prog_bar_";

    /// <summary>The fraction an icon state names ("prog_bar_45" is 0.45); false for any other state.</summary>
    public static bool TryFraction(string iconState, out double fraction)
    {
        fraction = 0;
        if (iconState == null || !iconState.StartsWith(Prefix, StringComparison.Ordinal))
            return false;
        if (!int.TryParse(iconState.AsSpan(Prefix.Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int percent))
            return false;
        fraction = Math.Clamp(percent, 0, 100) / 100.0;
        return true;
    }

    /// <summary>The progress bars among the replica's private images now, in image-id order.</summary>
    public static List<ProgressBarInfo> Of(ReplicaWorld world)
    {
        var bars = new List<ProgressBarInfo>();
        foreach (ImageEntry image in world.Images.Values.OrderBy(i => i.Id))
        {
            if (!world.TryGetAppearance(image.Appearance, out AppearanceEntry entry))
                continue;
            if (TryFraction(world.ResolveString(entry.IconState), out double fraction))
                bars.Add(new ProgressBarInfo(image.Id, image.AttachedTo, fraction, image.Holder, image.HolderName));
        }
        return bars;
    }

    /// <summary>
    /// What a bar is for, as its label says (the owner, 2026-10-18: a bare bar did not tell what it counted): the name of what it
    /// hangs on, with a capital ("Airlock" while welding it, "Oxygen tank" while breathing from it); "Action" when it hangs on the
    /// player's own mob (getting up, rolling on the floor, a do_after on oneself) or on nothing named.
    /// </summary>
    public static string Label(in ProgressBarInfo bar, long ownEntity)
    {
        string name = bar.HolderName?.Trim();
        if (string.IsNullOrEmpty(name) || (ownEntity != 0 && bar.Holder == ownEntity))
            return "Action";
        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }

    /// <summary>The bar's percent as drawn ("45%").</summary>
    public static string Percent(double fraction)
        => Math.Round(Math.Clamp(fraction, 0, 1) * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";
}

/// <summary>One progress bar placed on the screen (<see cref="ProgressBarLayout"/>), in viewport pixels.</summary>
/// <param name="Bar">The bar's frame.</param>
/// <param name="Fraction">How full, 0 to 1.</param>
/// <param name="Label">The label over the bar's left end, or null (a bar under its item's action button needs none: the button shows the item).</param>
/// <param name="Percent">The percent over the bar's right end, or null.</param>
/// <param name="LabelBox">Where the label is drawn (its left, top, the width it may take and its height).</param>
/// <param name="PercentBox">Where the percent is drawn, right aligned in it.</param>
public readonly record struct PlacedBar(HudRect Bar, double Fraction, string Label, string Percent, HudRect LabelBox, HudRect PercentBox);

/// <summary>
/// Where the progress bars go (the owner, 2026-10-18): a bar of an item that has an action button (an air tank's volume, version 27's
/// <see cref="Protocol.ScreenEntry.ActionTarget"/>) under that button at the top left, as wide as it; every other bar in a column at the
/// top edge's middle, with its label at the left and its percent at the right on a line above it, the two never sharing pixels (the
/// label is cut to the width the percent leaves).
/// </summary>
public static class ProgressBarLayout
{
    /// <summary>A bar of the top column, in units of the HUD's text scale (k, 2 at 1080p: 200 by 28 pixels, up from 136 by 24).</summary>
    public const float Width = 100, Height = 14, Gap = 8, TextGap = 2;

    /// <summary>A bar under an action button: its height and its distance below the button and from the next bar of that button.</summary>
    public const float ItemHeight = 5, ItemGap = 2;

    /// <summary>The label's and percent's font size before the scale.</summary>
    public const float FontSize = 12;

    /// <summary>
    /// Places <paramref name="bars"/>. <paramref name="buttonOf"/> gives the screen rectangle of the action button of an item id (false when it
    /// has none on the screen); <paramref name="measure"/> the width of a text at the font size in pixels, <paramref name="lineHeight"/> the height
    /// of a line. k is the HUD's text scale, <paramref name="viewWidth"/> the screen's width.
    /// </summary>
    public static List<PlacedBar> Place(IReadOnlyList<ProgressBarInfo> bars, long ownEntity, float k, float viewWidth, float lineHeight,
        Func<long, (bool Found, HudRect Rect)> buttonOf, Func<string, float> measure)
    {
        var placed = new List<PlacedBar>(bars.Count);
        var underButton = new Dictionary<long, float>();
        float width = Width * k, height = Height * k;
        float y = 24 * k; // the column's first label line: clear of the top edge
        foreach (ProgressBarInfo bar in bars)
        {
            if (bar.Holder != 0 && bar.Holder != ownEntity && buttonOf(bar.Holder) is (true, HudRect button))
            {
                float top = underButton.TryGetValue(bar.Holder, out float next) ? next : button.Bottom + ItemGap * k;
                underButton[bar.Holder] = top + (ItemHeight + ItemGap) * k;
                var frame = new HudRect(button.Left + 2 * k, top, Math.Max(1f, button.Width - 4 * k), ItemHeight * k);
                placed.Add(new PlacedBar(frame, bar.Fraction, null, null, default, default));
                continue;
            }
            string percent = ProgressBars.Percent(bar.Fraction);
            float percentWidth = measure(percent);
            float left = (viewWidth - width) / 2f;
            var percentBox = new HudRect(left + width - percentWidth, y, percentWidth, lineHeight);
            // the label takes what the percent leaves, less a space: a long name is cut, never drawn over the digits
            float labelRoom = Math.Max(0f, width - percentWidth - 6 * k);
            string label = Fit(ProgressBars.Label(bar, ownEntity), labelRoom, measure);
            var labelBox = new HudRect(left, y, labelRoom, lineHeight);
            var frameTop = new HudRect(left, y + lineHeight + TextGap * k, width, height);
            placed.Add(new PlacedBar(frameTop, bar.Fraction, label, percent, labelBox, percentBox));
            y = frameTop.Bottom + Gap * k;
        }
        return placed;
    }

    /// <summary><paramref name="text"/> cut with an ellipsis to fit <paramref name="room"/> pixels (whole when it fits; empty when nothing does).</summary>
    public static string Fit(string text, float room, Func<string, float> measure)
    {
        if (string.IsNullOrEmpty(text) || measure(text) <= room)
            return text ?? string.Empty;
        for (int n = text.Length - 1; n > 0; n--)
        {
            string cut = text.Substring(0, n).TrimEnd() + "…";
            if (measure(cut) <= room)
                return cut;
        }
        return string.Empty;
    }
}
