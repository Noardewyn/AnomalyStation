using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps R1: the station goals' windows (tg's BluespaceArtillery.tsx, DnaVault.tsx and SatelliteControl.tsx) without the engine. Each
/// reads tg's ui_data the way the TSX does and sends tg's acts: the artillery console's deployment, then its target and fire button
/// (locked until the heads authorize it); the DNA vault's samples and its two gene therapy choices once full; the satellite console's
/// toggles and the meteor shield's coverage.
/// </summary>
public class StationGoalWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(TguiInterface render, string name, string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, name, "device", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private static bool Has(UiNode tree, string key) => tree.Walk().Any(n => n.Key == key);

    [Fact]
    public void TheArtilleryWindowDeploysTheCannonThenAimsAndFiresIt()
    {
        // no cannon: the deployment, then its confirmation
        var (context, tree, acts) = Render(BluespaceArtillery.Render, "BluespaceArtillery", "{\"connected\":null,\"notice\":null,\"unlocked\":0,\"visualizing\":0}");
        Assert.Equal((400.0, 220.0), (context.WindowWidth, context.WindowHeight));
        Assert.False(Has(tree, "notice"));
        Assert.Equal("Maintenance", tree.Walk().Single(n => n.Kind == UiKind.LabeledItem).Label);
        Assert.Equal(("Begin Deployment", "wrench"), (ByKey(tree, "visualize").Text, ByKey(tree, "visualize").Icon));
        ByKey(tree, "visualize").OnClick();
        var (_, preview, previewActs) = Render(BluespaceArtillery.Render, "BluespaceArtillery", "{\"connected\":null,\"notice\":\"Parts misaligned!\",\"unlocked\":0,\"visualizing\":1}");
        Assert.Equal((UiKind.NoticeBox, "Parts misaligned!"), (ByKey(preview, "notice").Kind, ByKey(preview, "notice").Text));
        Assert.Equal(("Confirm Position", "good"), (ByKey(preview, "build").Text, ByKey(preview, "build").Color));
        Assert.Equal(("Cancel", "bad"), (ByKey(preview, "unvisualize").Text, ByKey(preview, "unvisualize").Color));
        ByKey(preview, "build").OnClick();
        ByKey(preview, "unvisualize").OnClick();
        Assert.Equal(new (string, string)[] { ("visualize", null) }, acts);
        Assert.Equal(new (string, string)[] { ("build", null), ("unvisualize", null) }, previewActs);

        // a cannon, locked: the crosshairs are disabled and the window says why
        var (_, locked, _) = Render(BluespaceArtillery.Render, "BluespaceArtillery", "{\"connected\":\"Bluespace Artillery\",\"notice\":null,\"unlocked\":0,\"visualizing\":0}");
        Assert.True(ByKey(locked, "recalibrate").Disabled);
        Assert.Equal(("crosshairs", "Target"), (ByKey(locked, "recalibrate").Icon, ByKey(locked, "target").Title));
        Assert.Equal(("No Target Set", "bad", "25px"), (ByKey(locked, "target-name").Text, ByKey(locked, "target-name").Color, (string)ByKey(locked, "target-name").FontSize));
        Assert.Equal("Bluespace artillery is currently locked.", ByKey(locked, "locked").Text);
        Assert.Contains("two station heads", ByKey(locked, "authorization").Text);
        Assert.False(Has(locked, "fire"));

        // unlocked with no target: FIRE is disabled; with one, it fires
        var (_, unlocked, _) = Render(BluespaceArtillery.Render, "BluespaceArtillery", "{\"connected\":\"Bluespace Artillery\",\"unlocked\":1,\"visualizing\":0}");
        Assert.True(ByKey(unlocked, "fire").Disabled);
        var (_, aimed, aimedActs) = Render(BluespaceArtillery.Render, "BluespaceArtillery", "{\"connected\":\"Bluespace Artillery\",\"unlocked\":1,\"visualizing\":0,\"target\":\"COM0\"}");
        Assert.Equal(("COM0", "average"), (ByKey(aimed, "target-name").Text, ByKey(aimed, "target-name").Color));
        UiNode fire = ByKey(aimed, "fire");
        Assert.Equal(("FIRE", "bad", "30px", "center", true), (fire.Text, fire.Color, (string)fire.FontSize, fire.TextAlign, fire.Fluid));
        Assert.False(fire.Disabled);
        ByKey(aimed, "recalibrate").OnClick();
        fire.OnClick();
        Assert.Equal(new (string, string)[] { ("recalibrate", null), ("fire", null) }, aimedActs);
    }

    [Fact]
    public void TheVaultWindowShowsTheSamplesAndOffersItsTwoTreatmentsOnceFull()
    {
        var (context, tree, _) = Render(DnaVault.Render, "DnaVault",
            "{\"plants\":3,\"plants_max\":12,\"animals\":0,\"animals_max\":17,\"dna\":5,\"dna_max\":10,\"completed\":0,\"used\":1,\"choiceA\":\"\",\"choiceB\":\"\"}");
        Assert.Equal((350.0, 400.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("DNA Vault Database", ByKey(tree, "database").Title);
        Assert.Equal(new[] { "Human DNA", "Plant DNA", "Animal DNA" }, tree.Walk().Where(n => n.Kind == UiKind.LabeledItem).Select(n => n.Label));
        Assert.Equal((0.5, "5 / 10 Samples"), (ByKey(tree, "dna").Value, ByKey(tree, "dna").Text));
        Assert.Equal((0.25, "3 / 12 Samples"), (ByKey(tree, "plants").Value, ByKey(tree, "plants").Text));
        Assert.Equal((0.0, "0 / 17 Samples"), (ByKey(tree, "animals").Value, ByKey(tree, "animals").Text));
        Assert.False(Has(tree, "therapy"));

        var (_, full, acts) = Render(DnaVault.Render, "DnaVault",
            "{\"plants\":12,\"plants_max\":12,\"animals\":17,\"animals_max\":17,\"dna\":10,\"dna_max\":10,\"completed\":1,\"used\":0,\"choiceA\":\"Tough\",\"choiceB\":\"Quick\"}");
        Assert.Equal("Personal Gene Therapy", ByKey(full, "therapy").Title);
        Assert.Equal("Applicable Gene Therapy Treatments", ByKey(full, "therapy-title").Text);
        Assert.Equal(("Tough", "Quick"), (ByKey(full, "choiceA").Text, ByKey(full, "choiceB").Text));
        ByKey(full, "choiceB").OnClick();
        Assert.Equal(new (string, string)[] { ("gene", "{\"choice\":\"Quick\"}") }, acts);

        // used: the treatments are gone
        var (_, used, _) = Render(DnaVault.Render, "DnaVault",
            "{\"plants\":12,\"plants_max\":12,\"animals\":17,\"animals_max\":17,\"dna\":10,\"dna_max\":10,\"completed\":1,\"used\":1,\"choiceA\":\"\",\"choiceB\":\"\"}");
        Assert.False(Has(used, "therapy"));
    }

    [Fact]
    public void TheSatelliteWindowTogglesEachSatelliteAndShowsTheShieldsCoverage()
    {
        var (context, tree, acts) = Render(SatelliteControl.Render, "SatelliteControl",
            "{\"satellites\":[{\"id\":0,\"active\":1,\"mode\":\"M-SHIELD\"},{\"id\":3,\"active\":0,\"mode\":\"M-SHIELD\"}],\"notice\":null,\"meteor_shield\":1,\"meteor_shield_coverage\":150,\"meteor_shield_coverage_max\":500}");
        Assert.Equal((400.0, 305.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Satellite Controls", ByKey(tree, "satellites").Title);
        Assert.Equal(("#0 M-SHIELD", true), (ByKey(tree, "satellite:0").Text, ByKey(tree, "satellite:0").Checkbox));
        Assert.Equal(("#3 M-SHIELD", false), (ByKey(tree, "satellite:3").Text, ByKey(tree, "satellite:3").Checkbox));
        UiNode coverage = ByKey(tree, "coverage");
        Assert.Equal(0.3, coverage.Value, 6);
        Assert.Equal(new[] { "good", "average", "bad" }, coverage.Ranges.Select(r => r.Color));
        Assert.Equal("Coverage", tree.Walk().Single(n => n.Kind == UiKind.LabeledItem).Label);
        ByKey(tree, "satellite:3").OnClick();
        Assert.Equal(new (string, string)[] { ("toggle", "{\"id\":3}") }, acts);

        // without the shield goal there is no coverage
        var (_, plain, _) = Render(SatelliteControl.Render, "SatelliteControl", "{\"satellites\":[],\"notice\":null}");
        Assert.False(Has(plain, "shield"));
        Assert.True(Has(plain, "satellites"));
    }

    [Fact]
    public void TheThreeWindowsAreRegisteredUnderTheirInterfaceNames()
    {
        Assert.Equal((TguiInterface)BluespaceArtillery.Render, TguiInterfaces.Find("BluespaceArtillery"));
        Assert.Equal((TguiInterface)DnaVault.Render, TguiInterfaces.Find("DnaVault"));
        Assert.Equal((TguiInterface)SatelliteControl.Render, TguiInterfaces.Find("SatelliteControl"));
    }
}
