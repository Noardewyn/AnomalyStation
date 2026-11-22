using System.Globalization;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AnomalousWeatherTower.tsx (engineering package E16). The
// window reads tg's ui_data and ui_static_data of the doppler radar tower with a weather anomaly core (mining_weather_monitor.dm:141,
// :159) and sends its acts, clear_weather and summon_weather. AnomalyTower.scss: the charge is large text on black, the summon button is
// a round red button (100 x 100) on a yellow and black hazard box (drawn as a yellow box).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AnomalousWeatherTower
{
    // tg: AnomalousWeatherTower.tsx:28
    private static string FormatCharges(double charges, double maxCharges) =>
        (charges / maxCharges * 100).ToString("R", CultureInfo.InvariantCulture) + "%";

    // tg: AnomalousWeatherTower.tsx:32
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double coreCharges = data.Num("core_charges");
        double maxCoreCharge = data.Num("max_core_charge");
        double weatherChargeCost = data.Num("weather_charge_cost");
        bool canSummon = data.Truthy("can_summon_weather");
        bool canClear = data.Truthy("can_clear_weather");
        IReadOnlyList<UiValue> summonable = data.Arr("summonable_weather_types");
        IReadOnlyList<UiValue> active = data.Arr("active_weather_on_z");

        // tg: AnomalousWeatherTower.tsx:45  useSharedState('selectedWeather', summonable_weather_types[0])
        string selectedId = c.UseState("selectedWeather", summonable.Count > 0 ? summonable[0].Str("id") : null);
        UiValue selected = summonable.FirstOrDefault(w => w.Str("id") == selectedId);

        int winHeight = 420 + active.Count * 30;

        string chargeColor = "green";
        if (coreCharges / maxCoreCharge <= 0.25)
            chargeColor = "red";
        else if (coreCharges / maxCoreCharge <= 0.5)
            chargeColor = "yellow";

        // tg: AnomalousWeatherTower.tsx:63
        var activeSection = new UiNode(UiKind.Section) { Key = "active", Title = "Active Weather" };
        if (active.Count > 0)
        {
            foreach (UiValue weather in active)
            {
                string id = weather.Str("id");
                activeSection.With(new UiNode(UiKind.StackItem) { Key = "weather:" + id }.With(
                    new UiNode(UiKind.Stack) { Align = "center" }.With(
                        new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Box) { Text = weather.Str("name") }),
                        new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                        {
                            Key = "clear:" + id,
                            Icon = "times",
                            Color = "red",
                            Disabled = !canClear,
                            Tooltip = canClear ? null : "Weather inhibitors are recharging.",
                            OnClick = () => c.Act("clear_weather", ("weather_ref", id)),
                        }))));
            }
        }
        else
            activeSection.With(new UiNode(UiKind.StackItem).With(new UiNode(UiKind.NoticeBox) { Key = "none", Text = "None" }));

        // tg: AnomalousWeatherTower.tsx:59  <Window width={250} height={winHeight} theme="ntos_darkmode">
        c.WindowTheme = "ntos_darkmode";
        return c.Window(250, winHeight, null,
            new UiNode(UiKind.Stack) { Vertical = true }.With(
                // tg: AnomalousWeatherTower.tsx:63
                new UiNode(UiKind.StackItem).With(
                    new UiNode(UiKind.Section) { Key = "charge" }.With(
                        new UiNode(UiKind.Stack) { Vertical = true, Align = "center" }.With(
                            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { FontSize = "18px", Text = "Core charge" }),
                            new UiNode(UiKind.StackItem) { Width = "50%" }.With(new UiNode(UiKind.Box)
                            {
                                Key = "charges",
                                FontSize = "24px",
                                Color = chargeColor,
                                Background = "black",
                                TextAlign = "center",
                                Text = FormatCharges(coreCharges, maxCoreCharge),
                            })))),
                new UiNode(UiKind.StackItem).With(activeSection),
                // tg: AnomalousWeatherTower.tsx:113
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                    new UiNode(UiKind.Section) { Key = "summon", Title = "Summon Weather", Fill = true }.With(
                        new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Dropdown)
                            {
                                Key = "weather",
                                Options = summonable.Select(w => w.Str("name")).ToArray(),
                                TextValue = selected?.Str("name"),
                                OnSelected = name =>
                                {
                                    UiValue weather = summonable.FirstOrDefault(w => w.Str("name") == name);
                                    if (weather != null)
                                        c.SetState("selectedWeather", weather.Str("id"));
                                },
                            }),
                            // tg: AnomalousWeatherTower.tsx:133  the hazard box and the round red button
                            new UiNode(UiKind.StackItem) { Align = "center" }.With(
                                new UiNode(UiKind.Box) { Key = "hazard", Background = "#ffc107" }.With(new UiNode(UiKind.Button)
                                {
                                    Key = "summon_weather",
                                    Color = "red",
                                    Circular = true,
                                    Width = "100px",
                                    Height = "100px",
                                    Disabled = !canSummon || coreCharges < weatherChargeCost,
                                    Tooltip = coreCharges < weatherChargeCost
                                        ? "Not enough charges to summon weather."
                                        : canSummon ? null : "Weather coils are recharging.",
                                    OnClick = () => c.Act("summon_weather", ("weather_type", selected?.Str("id"))),
                                })))))));
    }
}
