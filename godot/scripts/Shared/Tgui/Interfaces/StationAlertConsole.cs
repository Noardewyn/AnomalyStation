using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/StationAlertConsole.jsx (the engineering package E6,
// D287: the station alert consoles and the Canary program). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class StationAlertConsole
{
    // tg: StationAlertConsole.jsx:7
    public static UiNode Render(TguiContext c)
    {
        bool cameraView = c.Data.Truthy("cameraView");
        // <Window.Content scrollable>: the content in the window's own area
        return c.Window(cameraView ? 390 : 345, 587, null, Content(c));
    }

    // tg: StationAlertConsole.jsx:24
    private static readonly Dictionary<string, int> SortingKey = new()
    {
        ["Fire"] = 0,
        ["Atmosphere"] = 1,
        ["Power"] = 2,
        ["Burglar"] = 3,
        ["Motion"] = 4,
        ["Camera"] = 5,
    };

    // tg: StationAlertConsole.jsx:19
    public static UiNode Content(TguiContext c)
    {
        bool cameraView = c.Data.Truthy("cameraView");

        // tg: StationAlertConsole.jsx:33  sortBy(data.alarms, [alarm => sortingKey[alarm.name]]): a stable sort, an unknown name last
        IEnumerable<UiValue> sortedAlarms = c.Data.Arr("alarms").OrderBy(a => SortingKey.TryGetValue(a.Str("name"), out int k) ? k : int.MaxValue);

        var content = new UiNode(UiKind.Fragment);
        foreach (UiValue category in sortedAlarms)
        {
            string name = category.Str("name");
            IReadOnlyList<UiValue> alerts = category.Arr("alerts");
            // <ul>: each <li> a line with its bullet
            var section = new UiNode(UiKind.Section) { Key = name, Title = $"{name} Alarms" };
            if (alerts.Count == 0)
                section.With(new UiNode(UiKind.Box) { Key = "nominal", Color = "good", Text = "• Systems nominal" });
            foreach (UiValue alert in alerts)
            {
                string alertName = alert.Str("name");
                double sources = alert.Num("sources");
                var row = new UiNode(UiKind.Stack) { Key = "alert:" + alertName, Height = "30px", Align = "baseline" }.With(
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Box)
                    {
                        Key = "name",
                        Color = "average",
                        Text = "• " + alertName + (cameraView && sources > 1 ? $" ({TguiFormat.JsNumber(sources)} sources)" : ""),
                    }));
                if (cameraView)
                {
                    double cameras = alert.Num("cameras");
                    UiValue alertRef = alert.Field("ref");
                    row.With(new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "camera",
                        TextAlign = "center",
                        Width = "100px",
                        Icon = cameras != 0 ? "video" : "",
                        Disabled = cameras == 0,
                        Text = cameras == 1 ? $"{TguiFormat.JsNumber(cameras)} Camera" : cameras > 1 ? $"{TguiFormat.JsNumber(cameras)} Cameras" : "No Camera",
                        OnClick = () => c.Act("select_camera", ("alert", alertRef)),
                    }));
                }
                section.With(row);
            }
            content.With(section);
        }
        return content;
    }
}
