using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ThermoElectricGenerator.tsx (engineering package E11). The
// window reads tg's ui_data of the thermoelectric generator (thermoelectric_generator.dm:140): an error, or the last output and the cold
// and hot loops' inlet and outlet temperatures and pressures. It sends no act. The kit has no margin (the loop titles' m={1}).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ThermoElectricGenerator
{
    // tg: tgui/packages/tgui/interfaces/ThermoElectricGenerator.tsx:20
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string error = data.Str("error_message");
        // tg: ThermoElectricGenerator.tsx:28
        if (!string.IsNullOrEmpty(error))
            return c.Window(320, 100, null,
                new UiNode(UiKind.Section).With(new UiNode(UiKind.Box) { Key = "error", Text = "ERROR: " + error }));

        // tg: ThermoElectricGenerator.tsx:38  <Window width={350} height={195}>
        return c.Window(350, 195, null,
            new UiNode(UiKind.Section).With(
                new UiNode(UiKind.Box).With(
                    new UiNode(UiKind.Box) { Key = "output", Text = "Last Output: " + data.Str("last_power_output") },
                    new UiNode(UiKind.Divider),
                    new UiNode(UiKind.Box) { Key = "cold", Color = "cyan", Bold = true, Text = "Cold Loop" },
                    Loop("cold", data.Arr("cold_data"))),
                new UiNode(UiKind.Box).With(
                    new UiNode(UiKind.Box) { Key = "hot", Color = "red", Bold = true, Text = "Hot loop " },
                    Loop("hot", data.Arr("hot_data")))));
    }

    // tg: ThermoElectricGenerator.tsx:47  {cold_data.map((data, index) => ...)}, :64 the same for hot_data
    private static UiNode Loop(string loop, IReadOnlyList<UiValue> circulators)
    {
        var all = new UiNode(UiKind.Fragment);
        if (circulators == null)
            return all;
        for (int index = 0; index < circulators.Count; index++)
        {
            UiValue d = circulators[index];
            all.Children.Add(new UiNode(UiKind.Box) { Key = loop + index }.With(
                new UiNode(UiKind.Box) { Key = loop + index + "-temperature", Text = "Temperature Inlet: " + Num(d, "temperature_inlet") + " K / Outlet: " + Num(d, "temperature_outlet") + " K" },
                new UiNode(UiKind.Box) { Key = loop + index + "-pressure", Text = "Pressure Inlet: " + Num(d, "pressure_inlet") + " kPa / Outlet: " + Num(d, "pressure_outlet") + " kPa" }));
        }
        return all;
    }

    private static string Num(UiValue d, string key) => TguiFormat.JsNumber(d.Num(key));
}
