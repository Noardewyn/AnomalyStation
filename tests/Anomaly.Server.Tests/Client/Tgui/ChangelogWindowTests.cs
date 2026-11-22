using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>M10x slice H (D281, DV200): CHANGELOG.md read as tg's changelog, and tg's Changelog window drawn from what the datum sends.</summary>
public class ChangelogWindowTests
{
    private const string Text = "# Changelog\n\nintro\n\n## [Unreleased]\n\n## [1.0.0-pre-alpha] - 2026-10-27\n\n### Added\n\n- Chat in the lobby.\n- A long line\n  that goes on.\n\n### Fixed\n\n- A fix.\n\n## [0.9.7] - 2026-10-26\n\n### Changed\n\n- A tweak.\n";

    [Fact]
    public void TheVersionsAreTgsDatesNewestFirstWithTypedChanges()
    {
        var versions = datum_changelog.Parse(Text);
        Assert.Equal(new[] { "1.0.0-pre-alpha", "0.9.7" }, versions.Select(v => v.Version)); // the empty Unreleased is left out
        Assert.Equal("2026-10-27", versions[0].Date);
        Assert.Equal(new[] { ("rscadd", "Chat in the lobby."), ("rscadd", "A long line that goes on."), ("bugfix", "A fix.") }, versions[0].Changes);
        Assert.Equal(("tweak", "A tweak."), Assert.Single(versions[1].Changes));
    }

    [Fact]
    public void TheWindowShowsThePickedVersionAndAsksForAnother()
    {
        var acts = new List<(string, string)>();
        var c = new TguiContext(1, "Changelog", "Changelog", 2, UiValue.FromJson("{\"dates\":[\"1.0.0-pre-alpha\",\"0.9.7\"],\"selected\":\"1.0.0-pre-alpha\",\"date\":\"2026-10-27\",\"changes\":[{\"type\":\"rscadd\",\"text\":\"Chat in the lobby.\"},{\"type\":\"bugfix\",\"text\":\"A fix.\"}]}"),
            (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        UiNode tree = Changelog.Render(c);
        Assert.Equal("1.0.0-pre-alpha (27 October 2026)", tree.Walk().Single(n => n.Key == "changes:1.0.0-pre-alpha").Title);
        Assert.Equal(new[] { "check-circle", "bug" }, tree.Walk().Where(n => n.Kind == UiKind.Icon).Select(n => n.Icon));
        tree.Walk().First(n => n.Key == "next").Children.Single().OnClick();
        Assert.Equal(("get_month", "{\"date\":\"0.9.7\"}"), acts.Single());
        Assert.Contains(Changelog.Render(c).Walk(), n => n.Key == "loading"); // until the server's data has the picked version
    }
}
