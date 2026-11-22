using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// E2 (D287): the client's Supermatter window (tg's Supermatter.tsx, the observers' window) and NtosSupermatter (NtosSupermatter.tsx,
/// NT CIMS on the engineering consoles) without the engine. Each reads tg's sm_ui_data and gas_metadata the way the TSX does: the bars
/// with their ranges and texts, the chevrons that show a factor list, the gases sorted by share with tg's labels and colours and the
/// Show All Gases toggle, the console's list with its focus bell and Details, and tg's acts.
/// </summary>
public class SupermatterWindowsTests
{
    private const string Sm = "{\"uid\":1,\"area_name\":\"Supermatter Engine\",\"integrity\":97.5,"
        + "\"integrity_factors\":[{\"name\":\"Heat Damage\",\"amount\":-0.5}],"
        + "\"internal_energy\":1500,\"internal_energy_coefficient\":1.5,\"internal_energy_unit\":\" GeV\","
        + "\"internal_energy_factors\":[{\"name\":\"Gas\",\"amount\":12,\"unit\":\" MeV\"}],"
        + "\"temp_limit\":313.15,\"temp_limit_factors\":[],\"waste_multiplier\":1.25,\"waste_multiplier_factors\":[{\"name\":\"Gas\",\"amount\":0.25}],"
        + "\"zap_transmission_factors\":[{\"name\":\"Base\",\"amount\":1.56,\"unit\":\" MW\"}],"
        + "\"zap_transmission\":1560000,\"zap_transmission_coefficient\":1.56,\"zap_transmission_unit\":\" MW\",\"absorbed_ratio\":0.25,"
        + "\"gas_composition\":{\"/datum/gas/oxygen\":0.2,\"/datum/gas/nitrogen\":0.75,\"/datum/gas/plasma\":0,\"/datum/gas/water_vapor\":0.05},"
        + "\"gas_temperature\":290.5,\"gas_total_moles\":950}";

    private const string Metadata = "{\"/datum/gas/nitrogen\":{\"desc\":\"N2\",\"numeric_data\":[{\"name\":\"Heat Resistance\",\"amount\":0,\"unit\":\"%\",\"positive\":1},"
        + "{\"name\":\"Waste Multiplier\",\"amount\":-150,\"unit\":\"%\",\"positive\":0}]},"
        + "\"/datum/gas/oxygen\":{\"desc\":\"O2\",\"numeric_data\":[]}}";

    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(TguiInterface render, string name, string json, TguiLocalState local)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, name, "device", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local);
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private static string[] EntryTitles(UiNode section) => section.Walk().Where(n => n.Key == "title").Select(n => n.Text).ToArray();

    [Fact]
    public void TheCrystalWindowShowsTgsBarsAndTheGasesByShare()
    {
        var local = new TguiLocalState();
        var (context, tree, _) = Render(Supermatter.Render, "Supermatter", "{\"sm_data\":[" + Sm + "],\"gas_metadata\":" + Metadata + "}", local);
        Assert.Equal((700.0, 400.0, "ntos"), (context.WindowWidth, context.WindowHeight, context.WindowTheme));

        UiNode sm = tree.Walk().First(n => n.Kind == UiKind.Section && n.Key == "sm");
        Assert.Equal("1. Supermatter Engine", sm.Title);
        Assert.Equal(new[] { "Integrity:", "Internal Energy:", "Zap Power Transmission:", "Absorbed Moles:", "Temperature:", "Temperature Limit:", "Waste Multiplier:", "Absorption Ratio:" },
            EntryTitles(sm));
        UiNode integrity = ByKey(tree, "integrity");
        Assert.Equal((0.975, 0.0, 1.0, "97.50 %"), (integrity.Value, integrity.MinValue, integrity.MaxValue, integrity.Text));
        Assert.Equal(("good", 0.9, double.PositiveInfinity), integrity.Ranges[0]);
        Assert.Equal("1.500 GeV", ByKey(tree, "internal_energy").Text);
        Assert.Equal((1560000.0, "1.56 MW"), (ByKey(tree, "zap_transmission").Value, ByKey(tree, "zap_transmission").Text));
        Assert.Equal("950.00 Moles", ByKey(tree, "absorbed_moles").Text);
        UiNode temperature = ByKey(tree, "temperature");
        Assert.Equal((Supermatter.LogScale(290.5), Supermatter.LogScale(10000), "290.50 K"), (temperature.Value, temperature.MaxValue, temperature.Text));
        Assert.Equal("313.15 K", ByKey(tree, "temp_limit").Text);
        Assert.Equal("1.25 x", ByKey(tree, "waste_multiplier").Text);
        Assert.Equal("25%", ByKey(tree, "absorption_ratio").Text);

        // the gases with a share, the largest first, with tg's labels and colours (lightsteelblue is a CSS colour)
        UiNode gases = tree.Walk().First(n => n.Kind == UiKind.Section && n.Key == "gases");
        Assert.Equal(new[] { "N₂:", "O₂:", "H₂O:" }, EntryTitles(gases));
        UiNode[] bars = gases.Walk().Where(n => n.Key == "amount").ToArray();
        Assert.Equal(new[] { ("yellow", "75.00%"), ("blue", "20.00%"), ("lightsteelblue", "5.00%") }, bars.Select(b => (b.Color, b.Text)));
        Assert.Equal("Show All Gases", ByKey(tree, "allgas").Text);
    }

    [Fact]
    public void TheChevronsShowTheFactorsAndTheGasEffectsAndShowAllGasesListsEveryGas()
    {
        var local = new TguiLocalState();
        string json = "{\"sm_data\":[" + Sm + "],\"gas_metadata\":" + Metadata + "}";
        var (_, tree, _) = Render(Supermatter.Render, "Supermatter", json, local);
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "detail:integrity");

        // the integrity chevron shows the damage factors in red, the waste factors are red when they add waste
        ByKey(ByKey(tree, "entry:integrity"), "chevron").OnClick();
        ByKey(ByKey(tree, "entry:waste_multiplier"), "chevron").OnClick();
        ByKey(ByKey(tree, "entry:/datum/gas/nitrogen"), "chevron").OnClick();
        ByKey(ByKey(tree, "entry:/datum/gas/oxygen"), "chevron").OnClick();
        (_, tree, _) = Render(Supermatter.Render, "Supermatter", json, local);
        UiNode integrityFactor = ByKey(tree, "detail:integrity").Walk().Single(n => n.Kind == UiKind.LabeledItem);
        Assert.Equal(("Heat Damage (∆)", "red", "-0.50 %"), (integrityFactor.Label, integrityFactor.Children[0].Color, integrityFactor.Children[0].Text));
        UiNode wasteFactor = ByKey(tree, "detail:waste_multiplier").Walk().Single(n => n.Kind == UiKind.LabeledItem);
        Assert.Equal(("red", "0.25 x"), (wasteFactor.Children[0].Color, wasteFactor.Children[0].Text));
        Assert.Equal("chevron-up", ByKey(ByKey(tree, "entry:integrity"), "chevron").Icon);

        // nitrogen: its non-zero effects at 100% (a negative waste multiplier is good); oxygen: none
        UiNode nitrogen = ByKey(tree, "detail:/datum/gas/nitrogen");
        Assert.Contains("100% Composition", nitrogen.AllText());
        UiNode effect = nitrogen.Walk().Single(n => n.Kind == UiKind.LabeledItem);
        Assert.Equal(("Waste Multiplier", "green", "-150%"), (effect.Label, effect.Children[0].Color, effect.Children[0].Text));
        Assert.Equal("Has no composition effects", ByKey(tree, "detail:/datum/gas/oxygen").AllText());

        // Show All Gases: every gas, the empty ones too
        ByKey(tree, "allgas").OnClick();
        (_, tree, _) = Render(Supermatter.Render, "Supermatter", json, local);
        UiNode gases = tree.Walk().First(n => n.Kind == UiKind.Section && n.Key == "gases");
        Assert.Equal(new[] { "N₂:", "O₂:", "H₂O:", "Plasma:" }, EntryTitles(gases));
        Assert.Equal(("Hide Gases", "times"), (ByKey(tree, "allgas").Text, ByKey(tree, "allgas").Icon));
    }

    [Fact]
    public void TheConsoleListsTheCrystalsWithTheFocusBellAndOpensOneWithDetails()
    {
        var local = new TguiLocalState();
        string json = "{\"PC_device_theme\":\"ntos\",\"sm_data\":[" + Sm + "],\"gas_metadata\":" + Metadata + ",\"focus_uid\":1}";
        var (context, tree, acts) = Render(NtosSupermatter.Render, "NtosSupermatter", json, local);
        Assert.Equal((700.0, 400.0), (context.WindowWidth, context.WindowHeight));
        UiNode list = ByKey(tree, "detected");
        Assert.Equal("Detected Supermatters", list.Title);
        Assert.Equal("1. Supermatter Engine", ByKey(list, "name").Text);
        Assert.Equal(0.975, ByKey(list, "integrity").Value);
        Assert.Equal("yellow", ByKey(list, "focus").Color);

        ByKey(list, "focus").OnClick();
        list.Buttons.OnClick();
        Assert.Equal(new (string, string)[] { ("PRG_focus", "{\"focus_uid\":1}"), ("PRG_refresh", null) }, acts);

        // Details: the crystal's content with a Back button; Back returns to the list
        ByKey(list, "details").OnClick();
        (_, tree, _) = Render(NtosSupermatter.Render, "NtosSupermatter", json, local);
        UiNode sm = tree.Walk().First(n => n.Kind == UiKind.Section && n.Key == "sm");
        Assert.Equal(("1. Supermatter Engine", "Back"), (sm.Title, sm.Buttons.Text));
        sm.Buttons.OnClick();
        (_, tree, _) = Render(NtosSupermatter.Render, "NtosSupermatter", json, local);
        Assert.Contains(tree.Walk(), n => n.Key == "detected");

        // not focused: the bell has no colour
        (_, tree, _) = Render(NtosSupermatter.Render, "NtosSupermatter", json.Replace("\"focus_uid\":1", "\"focus_uid\":null"), new TguiLocalState());
        Assert.Null(ByKey(tree, "focus").Color);
    }

    [Fact]
    public void BothWindowsAreRegisteredAndTheGasesComeFromTheirPaths()
    {
        Assert.Equal((TguiInterface)Supermatter.Render, TguiInterfaces.Find("Supermatter"));
        Assert.Equal((TguiInterface)NtosSupermatter.Render, TguiInterfaces.Find("NtosSupermatter"));
        Assert.Equal(("Hyper-nob", "teal"), TguiFormat.GetGasFromPath("/datum/gas/hypernoblium"));
        Assert.Null(TguiFormat.GetGasFromPath("/datum/gas/unknown"));
    }
}
