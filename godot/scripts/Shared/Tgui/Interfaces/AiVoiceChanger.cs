using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AiVoiceChanger.tsx (silicons SI8, D326): the malfunctioning
// AI's voice changer (power, accent, verb, loudness, fake name). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AiVoiceChanger
{
    // tg: AiVoiceChanger.tsx:21
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool loud = data.Truthy("loud"), on = data.Truthy("on");
        var voices = data.Arr("voices").Select(v => v.AsStr()).ToList();
        string selected = data.Str("selected");
        return c.Window(400, 200, "Voice changer settings",
            new UiNode(UiKind.Section) { Key = "settings", Fill = true }.With(new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Key = "power", Label = "Power" }.With(new UiNode(UiKind.Button)
                {
                    Key = "power", Icon = on ? "power-off" : "times", Selected = on, Text = on ? "On" : "Off", OnClick = () => c.Act("power"),
                }),
                new UiNode(UiKind.LabeledItem) { Key = "accent", Label = "Accent" }.With(new UiNode(UiKind.Dropdown)
                {
                    Key = "accent", Options = voices, TextValue = selected, OnSelected = value => c.Act("look", ("look", value)),
                }),
                new UiNode(UiKind.LabeledItem) { Key = "verb", Label = "Verb" }.With(new UiNode(UiKind.Input)
                {
                    Key = "verb", TextValue = data.Str("say_verb"), OnCommit = value => c.Act("verb", ("verb", value)),
                }),
                new UiNode(UiKind.LabeledItem) { Key = "volume", Label = "Volume" }.With(new UiNode(UiKind.Button)
                {
                    Key = "loud", Icon = loud ? "power-off" : "times", Selected = loud, Text = loud ? "Loudmode on" : "Loudmode Off", OnClick = () => c.Act("loud"),
                }),
                new UiNode(UiKind.LabeledItem) { Key = "name", Label = "Fake name" }.With(new UiNode(UiKind.Input)
                {
                    Key = "name", TextValue = data.Str("name"), OnCommit = value => c.Act("name", ("name", value)),
                }))));
    }
}
