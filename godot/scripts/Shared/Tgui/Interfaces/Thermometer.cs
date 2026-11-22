using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Thermometer.jsx (gaps package P5). The window reads the
// thermometer's ui_data (items.dm:350: the attached holder's temperature). tg draws a CSS thermometer (a grey tube with a red column
// as high as the temperature's share of 1000 K over a red bulb with the reading) on a checkered background: the port draws the tube
// as a vertical ProgressBar-like column of two boxes and the bulb as a red box with the reading; the transparent window colour (a
// BYOND winset) is not ported. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Thermometer
{
    // tg: Thermometer.jsx:20
    public static UiNode Render(TguiContext c)
    {
        double temperature = c.Data.Num("Temperature");
        const double maxTemperature = 1000;
        double share = Math.Clamp(temperature / maxTemperature, 0, 1);
        const double tube = 340;
        // tg: Thermometer.jsx:45  ThermometerIcon: the tube (22 x 340 px, #595959, a #363636 border) filled from the bottom with #bd2020
        UiNode column = new UiNode(UiKind.Stack) { Key = "tube", Vertical = true, Width = "22px", Height = tube + "px", Background = "#595959" }.With(
            new UiNode(UiKind.StackItem) { Key = "empty", Height = (tube * (1 - share)) + "px" },
            new UiNode(UiKind.StackItem) { Key = "fill", Height = (tube * share) + "px", Background = "#bd2020" });
        UiNode bulb = new UiNode(UiKind.Box) { Key = "bulb", Width = "56px", Background = "#bd2020", TextAlign = "center", Text = TguiFormat.JsNumber(temperature) + "K" };
        // tg: Thermometer.jsx:22  <Window width={70} height={430}><Stack fill align="center" justify="space-around" backgroundColor="#242322">
        return c.Window(70, 430, null,
            new UiNode(UiKind.Stack) { Key = "root", Fill = true, Vertical = true, Align = "center", Justify = "space-around", Background = "#242322" }.With(
                new UiNode(UiKind.StackItem).With(column),
                new UiNode(UiKind.StackItem).With(bulb)));
    }
}
