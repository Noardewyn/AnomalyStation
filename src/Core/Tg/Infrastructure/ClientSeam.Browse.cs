using System.Text.RegularExpressions;
using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tg;

// M10x slice G6 (DV216): BYOND's browse() to a window of its own, tg's HTML pages (the ticket panels and lists of adminhelp.dm,
// /datum/browser). tgui's own windows already write their browse() into client.dm_tgui_output (DV44); these pages go there too, as a
// TguiOutputKind.Browse record the replicator draws as a "Browser" window (its links are tg's chat links, D154). BYOND's browser
// control and its skin options (can_close, titlebar, the asset cache's stylesheets) have no counterpart: the client's window has the
// game's look.

public static partial class ClientSeam
{
    private static readonly Regex TitleTag = new("<title>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    /// <summary>
    /// <c>target &lt;&lt; browse(html, options)</c> (tg's <c>DIRECT_OUTPUT(user, browse(...))</c>): a client, or a mob's client, gets the
    /// page in the window <c>options</c> names (<c>window=NAME</c>, BYOND's default name when it names none) with its <c>size=WxH</c>;
    /// a null <paramref name="html"/> closes that window. The window's title is the page's <c>&lt;title&gt;</c>, as BYOND's.
    /// </summary>
    public static void Browse(object target, object html, object options)
    {
        client player = target as client ?? (target as mob)?.client;
        if (player == null)
            return;
        DmList opts = Dm.params2list(Dm.str(options ?? ""));
        string window = Dm.str(opts["window"] ?? "");
        if (window.Length == 0)
            window = "browser"; // BYOND: browse() without a window shows the page in the client's default browser control
        if (html == null)
        {
            player.dm_tgui_write(new TguiOutput(window, TguiOutputKind.Close, null));
            return;
        }
        string page = Dm.str(html);
        string[] size = Dm.str(opts["size"] ?? "").Split('x');
        Match title = TitleTag.Match(page);
        DmList json = Dm.list();
        json["html"] = page;
        json["title"] = title.Success ? title.Groups[1].Value.Trim() : "";
        json["width"] = size.Length == 2 && int.TryParse(size[0], out int w) ? w : 0;
        json["height"] = size.Length == 2 && int.TryParse(size[1], out int h) ? h : 0;
        player.dm_tgui_write(new TguiOutput(window, TguiOutputKind.Browse, Dm.json_encode(json)));
    }
}
