using System.Linq;
using System.Text.RegularExpressions;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

/// <summary>
/// M10x slice G6 (DV216): a window of tg's browse() (BYOND's browser control: a ticket panel, the ticket lists), the page's HTML drawn
/// as tg's chat lines are (<see cref="UiKind.Html"/>) in the window size browse() asked for; a link click is a tg chat link, sent to the
/// server's Topic. The page's head (its title is the window's), styles and scripts are not drawn.
/// </summary>
public static class Browser
{
    private static readonly Regex Hidden = new("<(head|style|script)\\b.*?</\\1\\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    public static UiNode Render(TguiContext context)
    {
        UiValue data = context.Data;
        string html = string.Concat(data.Get("html")?.Items.Select(piece => piece.Text) ?? Enumerable.Empty<string>());
        double width = data.Get("width")?.Number ?? 0, height = data.Get("height")?.Number ?? 0;
        return context.Window(width > 0 ? width : 500, height > 0 ? height : 400, null,
            new UiNode(UiKind.Html) { TextValue = Hidden.Replace(html, ""), OnInput = context.Topic });
    }
}
