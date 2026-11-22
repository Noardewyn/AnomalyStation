using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/HypnoChair.jsx (gaps SG1): the enhanced interrogation
// chamber's information, its occupant, the door, the trigger phrase and the interrogation (machinery/hypnochair.dm:45 ui_data).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class HypnoChair
{
    // tg: HypnoChair.jsx:12
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue occupant = data.Get("occupant");
        string occupantName = occupant?.Str("name");
        double stat = occupant?.Num("stat") ?? 0;
        bool open = data.Truthy("open");
        bool interrogating = data.Truthy("interrogating");
        // tg: HypnoChair.jsx:15  <Window width={375} height={480}>
        return c.Window(375, 480, null,
            new UiNode(UiKind.Section) { Key = "information", Title = "Information", Background = "#450F44" }.With(new UiNode(UiKind.Box)
            {
                Text = "The Enhanced Interrogation Chamber is designed to induce a deep-rooted trance trigger into the subject. Once the "
                    + "procedure is complete, by using the implanted trigger phrase, the authorities are able to ensure immediate and complete "
                    + "obedience and truthfulness.",
            }),
            new UiNode(UiKind.Section) { Key = "occupant", Title = "Occupant Information", TextAlign = "center" }.With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Key = "name", Label = "Name", Text = string.IsNullOrEmpty(occupantName) ? "No Occupant" : occupantName },
                    data.Truthy("occupied")
                        ? new UiNode(UiKind.LabeledItem)
                        {
                            Key = "status",
                            Label = "Status",
                            Color = stat == 0 ? "good" : stat == 1 ? "average" : "bad",
                            Text = stat == 0 ? "Conscious" : stat == 1 ? "Unconscious" : "Dead",
                        }
                        : null)),
            new UiNode(UiKind.Section) { Key = "operations", Title = "Operations", TextAlign = "center" }.With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Key = "door_row", Label = "Door" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "door",
                        Icon = open ? "unlock" : "lock",
                        Color = open ? "default" : "red",
                        Text = open ? "Open" : "Closed",
                        OnClick = () => c.Act("door"),
                    }),
                    new UiNode(UiKind.LabeledItem) { Key = "trigger_row", Label = "Trigger Phrase" }.With(new UiNode(UiKind.Input)
                    {
                        Key = "set_phrase",
                        TextValue = data.Str("trigger"),
                        OnCommit = value => c.Act("set_phrase", ("phrase", value)),
                    }),
                    new UiNode(UiKind.LabeledItem) { Key = "interrogate_row", Label = "Interrogate Occupant" }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "interrogate",
                            Icon = "code-branch",
                            Text = interrogating ? "Interrupt Interrogation" : "Begin Enhanced Interrogation",
                            OnClick = () => c.Act("interrogate"),
                        },
                        data.Num("interrogating") == 1 ? new UiNode(UiKind.Icon) { Key = "cog", Icon = "cog", Color = "orange", Spin = true } : null))));
    }
}
