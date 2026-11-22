using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AirlockController.tsx (package E8: the incinerators'
// airlock consoles). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AirlockController
{
    // tg: AirlockController.tsx:20
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string airlockState = data.Str("airlockState");
        string pumpStatus = data.Str("pumpStatus");
        string interiorStatus = data.Str("interiorStatus");
        string exteriorStatus = data.Str("exteriorStatus");
        (string primary, string icon, string color) = GetAirlockStatus(airlockState);

        // tg: AirlockController.tsx:28  <Window width={500} height={190}>
        return c.Window(500, 190, null,
            new UiNode(UiKind.Section) { Title = "Airlock Status", Buttons = AirLockButtons(c, airlockState) }.With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Label = "Current Status" }.With(new UiNode(UiKind.Box) { Key = "status", Text = primary }),
                    // tg: AirlockController.tsx:36  <PressureIndicator currentStatus={currentStatus} />
                    new UiNode(UiKind.LabeledItem) { Label = "Chamber Pressure" }.With(PressureIndicator(data, icon, color)),
                    new UiNode(UiKind.LabeledItem) { Label = "Control Pump" }.With(new UiNode(UiKind.Box) { Key = "pump", Text = NameToUpperCase(pumpStatus) }),
                    new UiNode(UiKind.LabeledItem) { Label = "Interior Door" }.With(
                        new UiNode(UiKind.Box) { Key = "interior", Color = interiorStatus == "open" ? "good" : null, Text = NameToUpperCase(interiorStatus) }),
                    new UiNode(UiKind.LabeledItem) { Label = "Exterior Door" }.With(
                        new UiNode(UiKind.Box) { Key = "exterior", Color = exteriorStatus == "open" ? "good" : null, Text = NameToUpperCase(exteriorStatus) }))));
    }

    // tg: AirlockController.tsx:24  str.replace(/^\w/, (c) => c.toUpperCase())
    private static string NameToUpperCase(string str) =>
        str.Length > 0 && (char.IsAsciiLetterOrDigit(str[0]) || str[0] == '_') ? char.ToUpperInvariant(str[0]) + str[1..] : str;

    /// <summary>Displays the buttons on top of the window to cycle the airlock</summary>
    // tg: AirlockController.tsx:60
    private static UiNode AirLockButtons(TguiContext c, string airlockState)
    {
        UiNode Button(string key, string icon, string text, string action) =>
            new UiNode(UiKind.Button) { Key = key, Icon = icon, Text = text, OnClick = () => c.Act(action) };

        switch (airlockState)
        {
            case "pressurize":
            case "depressurize":
                return Button("abort", "stop-circle", "Abort", "abort");
            case "closed":
                return new UiNode(UiKind.Fragment).With(
                    Button("cycleInterior", "lock-open", "Open Interior Airlock", "cycleInterior"),
                    Button("cycleExterior", "lock-open", "Open Exterior Airlock", "cycleExterior"));
            case "inopen":
                return new UiNode(UiKind.Fragment).With(
                    Button("cycleClosed", "lock", "Close Interior Airlock", "cycleClosed"),
                    Button("cycleExterior", "sync", "Cycle to Exterior Airlock", "cycleExterior"));
            case "outopen":
                return new UiNode(UiKind.Fragment).With(
                    Button("cycleClosed", "lock", "Close Exterior Airlock", "cycleClosed"),
                    Button("cycleInterior", "sync", "Cycle to Interior Airlock", "cycleInterior"));
            default:
                return null;
        }
    }

    /// <summary>Displays the numeric pressure alongside an icon for the user</summary>
    // tg: AirlockController.tsx:107
    private static UiNode PressureIndicator(UiValue data, string icon, string color)
    {
        string sensorPressure = data.Str("sensorPressure");
        bool spin = icon == "fan";

        var box = new UiNode(UiKind.Box) { Key = "pressure", Color = color, Text = sensorPressure + " kPa " };
        // tg: {icon && <Icon name={icon} spin={spin} />}
        return string.IsNullOrEmpty(icon)
            ? box
            : new UiNode(UiKind.Stack) { Align = "center" }.With(box, new UiNode(UiKind.Icon) { Key = "pressure-icon", Icon = icon, Color = color, Spin = spin });
    }

    /// <summary>Displays the current status as two text strings, depending on door state.</summary>
    // tg: AirlockController.tsx:125
    private static (string Primary, string Icon, string Color) GetAirlockStatus(string airlockState) => airlockState switch
    {
        "inopen" => ("Interior Airlock Open", "", "good"),
        "pressurize" => ("Cycling to Interior Airlock", "fan", "average"),
        "closed" => ("Inactive", "", "white"),
        "depressurize" => ("Cycling to Exterior Airlock", "fan", "average"),
        "outopen" => ("Exterior Airlock Open", "exclamation-triangle", "bad"),
        _ => ("Unknown", "", "average"),
    };
}
