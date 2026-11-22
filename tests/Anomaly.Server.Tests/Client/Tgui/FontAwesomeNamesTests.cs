using System.IO;
using Anomaly.Client.Shared.Tgui;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>M9 slice C (the owner's playtest: the emote panel's icons were dots): tgui's icon names read from tg's Font Awesome stylesheet.</summary>
public class FontAwesomeNamesTests
{
    [Fact]
    public void TheStylesheetsRulesGiveEveryNameItsCharacter()
    {
        Dictionary<string, string> map = FontAwesomeNames.Parse(
            ".fa-eye:before{content:\"\\f06e\"}.fa-volume-high:before,.fa-volume-up:before{content:\"\\f028\"}.fa-exclamation:before{content:\"!\"}.fa-2x{font-size:2em}");
        Assert.Equal("\uf06e", map["eye"]);
        Assert.Equal("\uf028", map["volume-up"]);
        Assert.Equal("\uf028", map["volume-high"]);
        Assert.Equal("!", map["exclamation"]);
        Assert.Equal(4, map.Count);
    }

    [Fact]
    public void TgsStylesheetNamesTheEmotePanelsAndTitleBarsIcons()
    {
        string css = null;
        for (string up = AppContext.BaseDirectory; css == null && !string.IsNullOrEmpty(up); up = Path.GetDirectoryName(up))
        {
            string candidate = Path.Combine(up, "tgstation", "html", "font-awesome", "css", "all.min.css");
            if (File.Exists(candidate))
                css = candidate;
        }
        if (css == null)
            return; // no tg checkout beside the repository
        Dictionary<string, string> map = FontAwesomeNames.Parse(File.ReadAllText(css));
        foreach (string name in new[] { "eye", "eye-slash", "comment", "volume-up", "hand-paper", "crosshairs", "search", "tools", "cog", "times" })
            Assert.True(map.ContainsKey(name), name);
        Assert.True(map.Count > 1000);
    }
}
