using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps MD1 (the medical kiosk): the client's MedicalKiosk window (tg's MedicalKiosk.jsx) shows the four scans with their paid marks,
/// the price and the patient, sends beginScan_N for an unpaid scan and clearTarget, and shows the chosen paid scan's results; it is
/// registered under its name.
/// </summary>
public class MedicalKioskWindowTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string json, TguiLocalState local)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "MedicalKiosk", "kiosk", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local);
        return (context, MedicalKiosk.Render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private const string Kiosk = "{\"kiosk_cost\":10,\"patient_name\":\"Bob\",\"patient_health\":85.5,\"brute_health\":10,\"burn_health\":4.5,"
        + "\"toxin_health\":0,\"suffocation_health\":0,\"brain_health\":\"Brain patterns normal.\",\"brain_damage\":0,"
        + "\"patient_status\":\"Alive.\",\"trauma_status\":\"Patient is free of unique brain trauma.\",\"patient_illness\":\"Patient does not show signs of disease.\","
        + "\"illness_info\":\"Not Applicable.\",\"bleed_status\":\"Patient is not currently bleeding.\",\"blood_name\":\"Blood\",\"blood_levels\":100,"
        + "\"blood_status\":\"Patient blood levels are currently reading 100%.\",\"blood_alcohol\":0,"
        + "\"chemical_list\":[{\"name\":\"Bicaridine\",\"volume\":5}],\"overdose_list\":[],\"addict_list\":[],"
        + "\"hallucinating_status\":\"Patient is not hallucinating.\","
        + "\"active_status_1\":1,\"active_status_2\":0,\"active_status_3\":0,\"active_status_4\":4}";

    [Fact]
    public void TheMedicalKioskWindowSellsItsScansAndShowsThePaidOnes()
    {
        Assert.NotNull(TguiInterfaces.Find("MedicalKiosk"));
        var local = new TguiLocalState();
        var (context, tree, acts) = Render(Kiosk, local);
        Assert.Equal((575.0, 420.0), (context.WindowWidth, context.WindowHeight));
        string text = tree.AllText();
        Assert.Contains("Diagnosis costs 10 credits.", text);
        Assert.Contains("Patient: Bob", text);
        Assert.Contains("General Health Scan", text);
        Assert.DoesNotContain("Patient Health", text); // no scan chosen yet

        // a paid scan shows its results without an act; an unpaid one is bought
        ByKey(tree, "beginScan_1").OnClick();
        ByKey(tree, "beginScan_2").OnClick();
        ByKey(tree, "clearTarget").OnClick();
        Assert.Equal(new (string, string)[] { ("beginScan_2", null), ("clearTarget", null) }, acts);

        local.Set("scanIndex", 1);
        var (_, general, _) = Render(Kiosk, local);
        Assert.Contains("Patient Health", general.AllText());
        Assert.Equal(0.855, ByKey(general, "total").Value, 3);
        Assert.Equal("85.5%", ByKey(general, "total").Text);
        Assert.True(ByKey(general, "beginScan_1").Selected);

        local.Set("scanIndex", 4);
        var (_, chems, _) = Render(Kiosk, local);
        string chemText = chems.AllText();
        Assert.Contains("5 units of Bicaridine", chemText);
        Assert.Contains("Patient is not overdosing.", chemText);
        Assert.Contains("Patient has no addictions.", chemText);
    }
}
