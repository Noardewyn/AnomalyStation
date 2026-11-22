using System.Linq;
using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Cryo.tsx (M8x slice B, D205). The window reads tg's
// ui_data of the cryo cell (cryo.dm:543-585); AnimatedNumber renders as its text.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Cryo
{
    // tg: Cryo.tsx:15
    private static readonly (string Label, string Type)[] DamageTypes =
    {
        ("Brute", "bruteLoss"),
        ("Respiratory", "oxyLoss"),
        ("Toxin", "toxLoss"),
        ("Burn", "fireLoss"),
    };

    // tg: Cryo.tsx:36  stat_to_color
    private static string StatColor(string stat) => stat switch
    {
        "Dead" => "bad",
        "Conscious" => "bad",
        "Unconscious" => "good",
        _ => null,
    };

    // tg: Cryo.tsx:61
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue occupant = data.Field("occupant");
        bool hasOccupant = occupant?.Truthy ?? false;
        bool isOperating = data.Truthy("isOperating");
        bool isOpen = data.Truthy("isOpen");
        bool autoEject = data.Truthy("autoEject");

        // tg: Cryo.tsx:65  <Window width={400} height={550}> <Window.Content scrollable>
        return c.Window(400, 550, null,
            // tg: Cryo.tsx:67
            new UiNode(UiKind.Section) { Title = "Occupant" }.With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Label = "Occupant" }.With(
                        new UiNode(UiKind.Box) { Key = "name", Text = hasOccupant && occupant.Truthy("name") ? occupant.Str("name") : "No Occupant" })).With(
                    hasOccupant ? OccupantRows(data, occupant) : Array.Empty<UiNode>())),
            // tg: Cryo.tsx:118
            new UiNode(UiKind.Section) { Title = "Cell" }.With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Label = "Power" }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "power",
                            Icon = isOperating ? "power-off" : "times",
                            Disabled = isOpen,
                            OnClick = () => c.Act("power"),
                            Color = isOperating ? "green" : null,
                            Text = isOperating ? "On" : "Off",
                        }),
                    new UiNode(UiKind.LabeledItem) { Label = "Temperature" }.With(
                        new UiNode(UiKind.Box) { Key = "cell-temperature", Text = TguiFormat.JsNumber(TguiFormat.Round(data.Num("cellTemperature"), 0)) + " K" }),
                    new UiNode(UiKind.LabeledItem) { Label = "Door" }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "door",
                            Icon = isOpen ? "unlock" : "lock",
                            OnClick = () => c.Act("door"),
                            Text = isOpen ? "Open" : "Closed",
                        },
                        new UiNode(UiKind.Button)
                        {
                            Key = "autoeject",
                            Icon = autoEject ? "sign-out-alt" : "sign-in-alt",
                            OnClick = () => c.Act("autoeject"),
                            Text = autoEject ? "Auto" : "Manual",
                        }))),
            // tg: Cryo.tsx:155
            BeakerDisplay.SectionDisplay(c, data.Field("beaker"), showPH: false));
    }

    // tg: Cryo.tsx:73  the rows under the occupant's name: state, temperature, health and the four damages
    private static IEnumerable<UiNode> OccupantRows(UiValue data, UiValue occupant)
    {
        string stat = occupant.Str("stat");
        double bodyTemperature = occupant.Num("bodyTemperature");
        double health = occupant.Num("health");
        double maxHealth = occupant.Num("maxHealth");
        return new[]
        {
            new UiNode(UiKind.LabeledItem) { Label = "State" }.With(
                new UiNode(UiKind.Box) { Key = "stat", Color = StatColor(stat), Text = stat }),
            // tg: Cryo.tsx:84  green if the mob can actually be healed by cryoxadone
            new UiNode(UiKind.LabeledItem) { Label = "Temperature" }.With(
                new UiNode(UiKind.Box)
                {
                    Key = "body-temperature",
                    Color = bodyTemperature < data.Num("T0C") ? "good" : "bad",
                    Text = TguiFormat.JsNumber(TguiFormat.Round(bodyTemperature, 0)) + " K",
                }),
            // tg: Cryo.tsx:92
            new UiNode(UiKind.LabeledItem) { Label = "Health" }.With(
                new UiNode(UiKind.ProgressBar)
                {
                    Key = "health",
                    Value = TguiFormat.Round(health / maxHealth, 2),
                    Color = health > 0 ? "good" : "average",
                    Text = TguiFormat.JsNumber(TguiFormat.Round(health, 0)),
                }),
        }.Concat(
            // tg: Cryo.tsx:102
            DamageTypes.Select(damage => new UiNode(UiKind.LabeledItem) { Label = damage.Label }.With(
                new UiNode(UiKind.ProgressBar)
                {
                    Key = damage.Type,
                    Value = TguiFormat.Round(occupant.Num(damage.Type) / 100, 2),
                    Text = TguiFormat.JsNumber(TguiFormat.Round(occupant.Num(damage.Type), 0)),
                })));
    }
}
