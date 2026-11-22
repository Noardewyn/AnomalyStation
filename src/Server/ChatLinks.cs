using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server;

/// <summary>
/// Version 24 (D154, DV59): the hrefs of the links of tg's chat (<c>&lt;a href='byond://?...'&gt;</c>) that the server sent to one session,
/// the newest <see cref="Capacity"/> of them. A Topic action is taken only for one of these: BYOND takes any href a client sends and
/// leaves the checks to tg's Topic procs; the port answers only the links it showed that player.
/// </summary>
public sealed class ChatLinks
{
    /// <summary>How many distinct hrefs a session keeps (the oldest is forgotten first).</summary>
    public const int Capacity = 128;

    private readonly Queue<string> _order = new();
    private readonly HashSet<string> _known = new(StringComparer.Ordinal);

    /// <summary>How many hrefs are kept now.</summary>
    public int Count => _known.Count;

    /// <summary>Whether <paramref name="href"/> is one of the kept links.</summary>
    public bool Contains(string href) => href != null && _known.Contains(href);

    /// <summary>Keeps the BYOND links of a chat line's HTML (<see cref="Hrefs"/>).</summary>
    public void Record(string html)
    {
        foreach (string href in Hrefs(html))
        {
            if (!_known.Add(href))
                continue;
            _order.Enqueue(href);
            while (_order.Count > Capacity)
                _known.Remove(_order.Dequeue());
        }
    }

    private static readonly char[] UnquotedEnd = { ' ', '\t', '\n', '\r', '>' };

    /// <summary>
    /// The hrefs of the BYOND links in <paramref name="html"/>: each <c>href=</c> attribute, single or double quoted or unquoted (M10x slice C), whose value starts
    /// with <c>byond://?</c> (<see cref="ClientSeam.TopicLinkPrefix"/>) and fits <see cref="Limits.MaxTopicHrefBytes"/>. A longer one is not a
    /// link the client may send back, so the client does not offer it either (<c>HtmlText.Links</c>).
    /// </summary>
    public static IEnumerable<string> Hrefs(string html)
    {
        if (string.IsNullOrEmpty(html))
            yield break;
        int i = 0;
        while ((i = html.IndexOf("href=", i, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            i += 5;
            if (i >= html.Length)
                yield break;
            char quote = html[i];
            string href;
            if (quote != '\'' && quote != '"')
            {
                // M10x slice C: an unquoted value (tg's FOLLOW_LINK and TURF_LINK, __DEFINES/say.dm) runs to a space or the tag's end, as HTML's
                int stop = html.IndexOfAny(UnquotedEnd, i);
                if (stop < 0)
                    yield break;
                href = html.Substring(i, stop - i);
                i = stop;
            }
            else
            {
                int end = html.IndexOf(quote, i + 1);
                if (end < 0)
                    yield break;
                href = html.Substring(i + 1, end - i - 1);
                i = end + 1;
            }
            if (ClientSeam.IsLink(href) && System.Text.Encoding.UTF8.GetByteCount(href) <= Limits.MaxTopicHrefBytes)
                yield return href;
        }
    }
}
