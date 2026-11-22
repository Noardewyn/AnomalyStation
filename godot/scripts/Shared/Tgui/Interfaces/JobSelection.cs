using System.Globalization;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/JobSelection.tsx (M10 slice F: the lobby's late join
// menu, GLOB.latejoin_menu's window). Engine-free (compiled into the tests too). The departments' CSS columns (columns: 20em) are three
// columns filled in order, each about as long as the others; a job's tooltip is its unavailability or its description.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class JobSelection
{
    private const int Columns = 3;

    // tg: JobSelection.tsx:187
    public static UiNode Render(TguiContext c)
    {
        if (c.Data.Get("departments_static") == null)
            return new UiNode(UiKind.Fragment); // Stop TGUI whitescreens with TGUI-dev!

        // toMerged(data.departments, data.departments_static)
        IReadOnlyList<KeyValuePair<string, UiValue>> statics = c.Data.Get("departments_static")?.Fields ?? Array.Empty<KeyValuePair<string, UiValue>>();
        UiValue live = c.Data.Get("departments");
        var departments = statics.Select(d => (Name: d.Key, Static: d.Value, Live: live?.Get(d.Key))).ToList();

        string shuttle = c.Data.Str("shuttle_status", null);
        string duration = c.Data.Str("round_duration");

        var title = new UiNode(UiKind.Stack) { Vertical = true };
        if (!string.IsNullOrEmpty(shuttle))
            title.With(new UiNode(UiKind.NoticeBox) { Key = "shuttle", Text = shuttle, Color = "info" });
        title.With(new UiNode(UiKind.Box) { Key = "duration", Color = "label", Text = "It is currently " + duration + " into the shift." });

        // columns: 20em, filled in order and balanced by their jobs
        int total = departments.Sum(d => 1 + (d.Static.Get("jobs")?.Fields.Count ?? 0));
        var columns = new List<UiNode>();
        var column = new UiNode(UiKind.Stack) { Vertical = true };
        int filled = 0, at = 0;
        foreach (var department in departments)
        {
            int size = 1 + (department.Static.Get("jobs")?.Fields.Count ?? 0);
            if (filled > 0 && filled + size / 2 > (total * (at + 1)) / Columns && at < Columns - 1)
            {
                columns.Add(new UiNode(UiKind.StackItem) { Key = "col:" + at, Grow = 1, MinWidth = "30%" }.With(column));
                column = new UiNode(UiKind.Stack) { Vertical = true };
                at++;
            }
            column.With(new UiNode(UiKind.StackItem) { Key = "dept:" + department.Name }.With(DepartmentEntry(c, department.Name, department.Static, department.Live)));
            filled += size;
        }
        columns.Add(new UiNode(UiKind.StackItem) { Key = "col:" + at, Grow = 1, MinWidth = "30%" }.With(column));

        var random = new UiNode(UiKind.Button)
        {
            Key = "random", Text = "Random Job!",
            Tooltip = "Roll target random job. You can re-roll or cancel your random job if you don't like it.",
            OnClick = () => c.Act("select_job", ("job", "Random")),
        };

        return c.Window(1012, string.IsNullOrEmpty(shuttle) ? 666 : 690 /* Hahahahahaha */, null,
            new UiNode(UiKind.Section) { Key = "jobs", Fill = true, Scrollable = true, LabelNode = title, Buttons = random }.With(
                new UiNode(UiKind.Stack) { Key = "columns" }.With(columns)));
    }

    // tg: JobSelection.tsx:129 DepartmentEntry
    private static UiNode DepartmentEntry(TguiContext c, string name, UiValue statics, UiValue live)
    {
        string color = statics.Str("color", "#808080");
        UiValue openSlots = live?.Get("open_slots");
        string slots = openSlots?.Text ?? TguiFormat.JsNumber(openSlots?.Number ?? 0);
        bool one = openSlots?.Number == 1;
        var jobs = (statics.Get("jobs")?.Fields ?? Array.Empty<KeyValuePair<string, UiValue>>()).Select(job =>
            new UiNode(UiKind.StackItem) { Key = "job:" + job.Key }.With(JobEntry(c, job.Key, job.Value, live?.Get("jobs")?.Get(job.Key), color)));
        return new UiNode(UiKind.Section)
        {
            Title = name,
            Background = color,
            Color = Darken(color, 80),
            Buttons = new UiNode(UiKind.Box) { Key = "slots", Color = Darken(color, 60), Text = slots + (one ? " slot" : " slots") + " available" },
        }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(jobs));
    }

    // tg: JobSelection.tsx:52 JobEntry
    private static UiNode JobEntry(TguiContext c, string jobName, UiValue statics, UiValue live, string departmentColor)
    {
        string unavailable = live?.Str("unavailable_reason", null);
        bool prioritized = live?.Truthy("prioritized") == true;
        string icon = live?.Str("jobIcon", null);
        var row = new UiNode(UiKind.Stack) { Fill = true };
        if (!string.IsNullOrEmpty(icon))
            row.With(new UiNode(UiKind.StackItem) { Key = "icon" }.With(new UiNode(UiKind.Icon) { Icon = icon }));
        row.With(
            new UiNode(UiKind.StackItem) { Key = "name", Grow = 1 }.With(new UiNode(UiKind.Box) { Text = jobName, Bold = statics.Truthy("command") }),
            new UiNode(UiKind.StackItem) { Key = "slots" }.With(new UiNode(UiKind.Box)
            {
                Text = Number(live?.Get("used_slots")) + " / " + Number(live?.Get("open_slots")),
            }));
        return new UiNode(UiKind.Button)
        {
            Fluid = true,
            // Try not to think too hard about this one.
            Background = unavailable != null ? "#949494" : prioritized ? "#16fc0f" : Darken(departmentColor, 10),
            Color = unavailable != null ? "#616161" : Darken(departmentColor, 90),
            FontSize = "1.1rem",
            Tooltip = unavailable ?? (prioritized ? "The HoP wants more people in this job!\n" + statics.Str("description") : statics.Str("description")),
            OnClick = () =>
            {
                if (unavailable == null)
                    c.Act("select_job", ("job", jobName));
            },
        }.With(row);
    }

    private static string Number(UiValue v) => v?.Text ?? TguiFormat.JsNumber(v?.Number ?? 0);

    // tgui-core's Color.darken(percent): each channel less that percent of itself
    internal static string Darken(string hex, double percent)
    {
        string h = hex.TrimStart('#');
        if (h.Length < 6 || !int.TryParse(h[..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
            return hex;
        double k = 1 - percent / 100;
        int r = (int)((rgb >> 16 & 0xFF) * k), g = (int)((rgb >> 8 & 0xFF) * k), b = (int)((rgb & 0xFF) * k);
        return "#" + r.ToString("x2", CultureInfo.InvariantCulture) + g.ToString("x2", CultureInfo.InvariantCulture) + b.ToString("x2", CultureInfo.InvariantCulture);
    }

    // tgui-core's Color.lighten(percent): each channel that percent of the way to white
    internal static string Lighten(string hex, double percent)
    {
        string h = hex.TrimStart('#');
        if (h.Length < 6 || !int.TryParse(h[..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
            return hex;
        double k = percent / 100;
        int Up(int channel) => (int)(channel + (255 - channel) * k);
        int r = Up(rgb >> 16 & 0xFF), g = Up(rgb >> 8 & 0xFF), b = Up(rgb & 0xFF);
        return "#" + r.ToString("x2", CultureInfo.InvariantCulture) + g.ToString("x2", CultureInfo.InvariantCulture) + b.ToString("x2", CultureInfo.InvariantCulture);
    }
}
