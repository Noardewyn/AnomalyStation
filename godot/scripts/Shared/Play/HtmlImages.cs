using System.Globalization;
using System.Text.RegularExpressions;

namespace Anomaly.Client.Shared;

/// <summary>A piece of a browse() page: its HTML up to the next picture (<see cref="Html"/>), or a picture (<see cref="Source"/>).</summary>
/// <param name="Html">The HTML of a text piece, null for a picture.</param>
/// <param name="Source">The picture's resource (a runtime image <c>rsc/MD5.png</c> or an icon recipe), null for text.</param>
/// <param name="Width">The picture's width attribute in pixels, 0 when it names none.</param>
/// <param name="Height">The picture's height attribute in pixels, 0 when it names none.</param>
public readonly record struct HtmlPiece(string Html, string Source, int Width, int Height);

/// <summary>
/// DV685: the pictures of a browse() page (tg's photo window, <c>&lt;img src='tmp_photo.png' width='480'&gt;</c>, whose source the server's
/// browse_rsc made the picture's resource). The page is split at each <c>&lt;img&gt;</c> whose source is one the client draws (a runtime
/// image or an icon recipe); any other picture stays in the text, which drops it. Engine-free (compiled into the tests too).
/// </summary>
public static class HtmlImages
{
    private static readonly Regex Img = new("<img\\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Attribute = new("\\b(src|width|height)\\s*=\\s*(['\"])([^'\"]*)\\2", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>The pieces of <paramref name="html"/> in order; a page with no picture is one text piece.</summary>
    public static List<HtmlPiece> Split(string html)
    {
        var pieces = new List<HtmlPiece>();
        html ??= "";
        int at = 0;
        foreach (Match tag in Img.Matches(html))
        {
            string source = null;
            int width = 0, height = 0;
            foreach (Match a in Attribute.Matches(tag.Value))
            {
                string value = a.Groups[3].Value;
                switch (a.Groups[1].Value.ToLowerInvariant())
                {
                    case "src": source = value; break;
                    case "width": width = Pixels(value); break;
                    case "height": height = Pixels(value); break;
                }
            }
            if (!Drawn(source))
                continue;
            if (tag.Index > at)
                pieces.Add(new HtmlPiece(html.Substring(at, tag.Index - at), null, 0, 0));
            pieces.Add(new HtmlPiece(null, source, width, height));
            at = tag.Index + tag.Length;
        }
        if (at < html.Length || pieces.Count == 0)
            pieces.Add(new HtmlPiece(html.Substring(at), null, 0, 0));
        return pieces;
    }

    /// <summary>Whether the client draws a picture of <paramref name="source"/>: a runtime image or an icon recipe.</summary>
    public static bool Drawn(string source)
        => source != null && (source.StartsWith("rsc/", StringComparison.Ordinal) || source.StartsWith("iconops:", StringComparison.Ordinal));

    private static int Pixels(string value)
        => int.TryParse(value.Trim().TrimEnd('x', 'p'), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n is > 0 and <= 4096 ? n : 0;
}
