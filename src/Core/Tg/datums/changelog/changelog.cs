// Translated from tgstation (snapshot 1d99bd7f): code/datums/changelog/changelog.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10x slice H (D281): the whole file, tg's changelog window (the lobby's Changelog button, the client's changelog verb).
// DEVIATION(DV200): tg's changelog is html/changelogs/archive/YYYY-MM.yml, one file a month of dates, authors and typed changes, which the
// window fetches as an asset (get_month: SSassets' send_asset). The port's changelog is CHANGELOG.md (docs/versioning.md): a "## [VERSION] -
// DATE" heading per version with "### Added/Changed/Fixed/Removed" lists, read from the world's files (the host mounts it). So the
// dates are the versions, newest first, get_month picks one and ui_data sends its changes (tg's change types: Added rscadd, Changed
// tweak, Fixed bugfix, Removed rscdel), without authors.

// tg: code/datums/changelog/changelog.dm:1
[DmPath("/datum/changelog")]
public partial class datum_changelog : datum
{
    /// The port's changelog file in the world's files (DV200).
    public const string ChangelogFile = "CHANGELOG.md";

    // tg: code/datums/changelog/changelog.dm:2  var/static/list/changelog_items: the month assets (DV200: the parsed versions instead)
    private (string Version, string Date, List<(string Type, string Text)> Changes)[] _versions;
    private string _parsedText;

    /// The version get_month picked (DV200: tg's window keeps the picked month itself and fetches its file).
    public object selected;

    // tg: code/datums/changelog/changelog.dm:4
    public override object ui_state(mob user = null)
    {
        return GLOB.always_state;
    }

    // tg: code/datums/changelog/changelog.dm:7
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Changelog");
            await ui.open();
        }
        return null;
    }

    // tg: code/datums/changelog/changelog.dm:13
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.eq(action, "get_month"))
        {
            // DEVIATION(DV200): the version's changes go out in ui_data instead of tg's month asset
            selected = @params?["date"];
            return TRUE;
        }
        return null;
    }

    // tg: code/datums/changelog/changelog.dm:24
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list(Dm.kv("dates", Dm.list()));
        // DEVIATION(DV200): the versions of CHANGELOG.md, newest first, for flist("html/changelogs/archive/")'s months
        DmList dates = (DmList)data["dates"];
        foreach ((string version, _, _) in Versions())
            dates.Add(version);
        return data;
    }

    // DEVIATION(DV200): tg's window loads the picked month's file; here the picked version's changes (the newest until one is picked)
    public override async DmTask<object> ui_data(mob user = null)
    {
        var versions = Versions();
        int index = Array.FindIndex(versions, v => Dm.eq(v.Version, selected));
        if (index < 0)
            index = 0;
        DmList changes = Dm.list();
        if (versions.Length > 0)
        {
            foreach ((string type, string text) in versions[index].Changes)
                Dm.append(ref changes, Dm.list(Dm.list(Dm.kv("type", type), Dm.kv("text", text)))); // DM's += of a list adds its items: one wrapped
        }
        return Dm.list(
            Dm.kv("selected", versions.Length > 0 ? versions[index].Version : null),
            Dm.kv("date", versions.Length > 0 ? versions[index].Date : null),
            Dm.kv("changes", changes));
    }

    // CHANGELOG.md's versions (DV200), parsed once a text
    private (string Version, string Date, List<(string Type, string Text)> Changes)[] Versions()
    {
        string text = file2text(ChangelogFile) ?? "";
        if (_versions != null && text == _parsedText)
            return _versions;
        _parsedText = text;
        _versions = Parse(text);
        return _versions;
    }

    /// <summary>
    /// CHANGELOG.md's versions newest first (DV200): each "## [VERSION]" heading with its " - DATE", and the "- " items of its "###" lists
    /// typed as tg's changes; an item's continuation lines join it; a version with no items is left out.
    /// </summary>
    public static (string Version, string Date, List<(string Type, string Text)> Changes)[] Parse(string text)
    {
        var versions = new List<(string Version, string Date, List<(string Type, string Text)> Changes)>();
        List<(string Type, string Text)> current = null;
        string type = "unknown";
        foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            string line = raw.TrimEnd();
            if (line.StartsWith("## [", StringComparison.Ordinal))
            {
                int close = line.IndexOf(']');
                string version = close > 4 ? line.Substring(4, close - 4) : line.Substring(3);
                int dash = line.IndexOf(" - ", close > 0 ? close : 0, StringComparison.Ordinal);
                current = new List<(string Type, string Text)>();
                versions.Add((version, dash > 0 ? line.Substring(dash + 3).Trim() : null, current));
                type = "unknown";
            }
            else if (current != null && line.StartsWith("### ", StringComparison.Ordinal))
            {
                type = line.Substring(4).Trim() switch
                {
                    "Added" => "rscadd",
                    "Changed" => "tweak",
                    "Fixed" => "bugfix",
                    "Removed" => "rscdel",
                    _ => "unknown",
                };
            }
            else if (current != null && line.StartsWith("- ", StringComparison.Ordinal))
                current.Add((type, line.Substring(2).Trim()));
            else if (current != null && current.Count > 0 && line.StartsWith("  ", StringComparison.Ordinal) && line.Trim().Length > 0)
                current[^1] = (current[^1].Type, current[^1].Text + " " + line.Trim());
        }
        return versions.Where(v => v.Changes.Count > 0).ToArray();
    }
}
