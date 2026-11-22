using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/RemoteRobotControl.jsx (silicons SI4, D326): the AI's
// remote control of the station's bots.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class RemoteRobotControl
{
    // tg: RemoteRobotControl.jsx:12
    public static UiNode Render(TguiContext c) => c.Window(500, 500, "Remote Robot Control", Content(c));

    // tg: RemoteRobotControl.jsx:22
    public static UiNode Content(TguiContext c)
    {
        IReadOnlyList<UiValue> robots = c.Data.Get("robots")?.Items ?? Array.Empty<UiValue>();
        if (robots.Count == 0)
            return new UiNode(UiKind.Section).With(new UiNode(UiKind.NoticeBox) { TextAlign = "center", Text = "No robots detected" });
        return new UiNode(UiKind.Fragment).With(robots.Select(robot =>
        {
            string robotRef = robot.Str("ref");
            string mode = TguiFormat.DecodeHtmlEntities(robot.Str("mode"));
            return new UiNode(UiKind.Section)
            {
                Key = robotRef,
                Title = robot.Str("name") + " (" + robot.Str("model") + ")",
                Buttons = new UiNode(UiKind.Fragment).With(
                    new UiNode(UiKind.Button) { Key = "interface", Icon = "tools", Text = "Interface", OnClick = () => c.Act("interface", ("ref", robotRef)) },
                    new UiNode(UiKind.Button) { Key = "call", Icon = "phone-alt", Text = "Call", OnClick = () => c.Act("callbot", ("ref", robotRef)) }),
            }.With(new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Label = "Status" }.With(
                    new UiNode(UiKind.Box) { Key = "mode", Color = mode == "Inactive" ? "bad" : mode == "Idle" ? "average" : "good", Text = mode },
                    robot.Truthy("hacked") ? new UiNode(UiKind.Box) { Key = "hacked", Color = "bad", Text = " (HACKED)" } : null),
                new UiNode(UiKind.LabeledItem) { Label = "Location", Text = robot.Str("location") }));
        }));
    }
}
