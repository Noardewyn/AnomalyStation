using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/SatelliteControl.tsx (gaps R1: the satellite control
// console). The window reads the console's ui_data (satellite_control.dm:36: each satellite's id, active and mode, and the meteor
// shield's coverage) and sends toggle with a satellite's id. The kit has no margin (the satellites' Box mr={-1}). Original copyright and
// license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class SatelliteControl
{
    // tg: SatelliteControl.tsx:26
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool meteorShield = data.Truthy("meteor_shield");

        // tg: SatelliteControl.tsx:31  <Window width={400} height={305}>
        return c.Window(400, 305, null,
            meteorShield ? ShieldInfo(c) : null,
            SatelliteDisplay(c));
    }

    /** Displays coverage info of the meteor shield */
    // tg: SatelliteControl.tsx:41
    private static UiNode ShieldInfo(TguiContext c)
    {
        UiValue data = c.Data;
        double coverage = data.Num("meteor_shield_coverage");
        double coverageMax = data.Num("meteor_shield_coverage_max");

        return new UiNode(UiKind.Section) { Key = "shield" }.With(
            new UiNode(UiKind.LabeledList).With(
                // tg: SatelliteControl.tsx:48
                new UiNode(UiKind.LabeledItem) { Label = "Coverage" }.With(
                    new UiNode(UiKind.ProgressBar)
                    {
                        Key = "coverage",
                        Value = coverageMax != 0 ? coverage / coverageMax : 0,
                        Ranges = new[]
                        {
                            ("good", 1.0, double.PositiveInfinity),
                            ("average", 0.3, 1.0),
                            ("bad", double.NegativeInfinity, 0.3),
                        },
                    })));
    }

    /** Displays a map of satellites and their status */
    // tg: SatelliteControl.tsx:64
    private static UiNode SatelliteDisplay(TguiContext c)
    {
        IReadOnlyList<UiValue> satellites = c.Data.Arr("satellites");

        return new UiNode(UiKind.Section) { Key = "satellites", Title = "Satellite Controls" }.With(
            new UiNode(UiKind.Box).With(
                // tg: SatelliteControl.tsx:71
                satellites.Select(satellite =>
                {
                    UiValue id = satellite.Get("id");
                    string idText = satellite.Str("id");
                    object idValue = id?.Kind == UiValueKind.Number ? id.Number : idText; // act('toggle', { id: satellite.id }): the id as sent
                    return new UiNode(UiKind.Button)
                    {
                        Key = "satellite:" + idText,
                        Checkbox = satellite.Truthy("active"),
                        Text = "#" + idText + " " + satellite.Str("mode"),
                        OnClick = () => c.Act("toggle", ("id", idValue)),
                    };
                })));
    }
}
