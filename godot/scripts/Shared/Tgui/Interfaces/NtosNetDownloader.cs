using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosNetDownloader.tsx (M9 slice E: the NT Software Hub).
// Engine-free (compiled into the tests too). The Input's placeholder is its tooltip (as in AtmosHandbook).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosNetDownloader
{
    // tg: NtosNetDownloader.tsx:49
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double diskSize = data.Num("disk_size");
        double diskUsed = data.Num("disk_used");
        double completion = data.Num("downloadcompletion");
        bool downloading = data.Truthy("downloading");
        string downloadName = data.Str("downloadname");
        double downloadSize = data.Num("downloadsize");
        UiValue errorValue = data.Get("error");
        string error = errorValue is { Kind: UiValueKind.String } && errorValue.Text != "" ? errorValue.Text : null;
        bool emagged = data.Truthy("emagged");
        IReadOnlyList<UiValue> categories = data.Get("categories")?.Items ?? Array.Empty<UiValue>();
        IReadOnlyList<UiValue> programs = data.Get("programs")?.Items ?? Array.Empty<UiValue>();
        string percentage = TguiFormat.ToFixed(downloadSize == 0 ? 0 : completion / downloadSize * 100);
        string selectedCategory = c.UseState("category", categories.Count > 0 ? categories[0].Text : null);
        string searchItem = c.UseState("search", "");
        Func<UiValue, bool> search = TguiFormat.CreateSearch<UiValue>(searchItem, program => program.Str("filedesc"));

        IEnumerable<UiValue> items = searchItem.Length > 0
            ? programs.Where(search) // If we have a query, search everything for it.
            : programs.Where(program => program.Str("category") == selectedCategory); // Otherwise, show respective programs for the category.
        // This sorts all programs in the lists by name and compatibility
        items = items.OrderBy(program => program.Truthy("compatible") ? 0 : 1).ThenBy(program => program.Str("filedesc"), Comparer<string>.Create(TguiFormat.LocaleCompare));
        if (!emagged) // This filters the list to only contain verified programs
            items = items.Where(program => program.Num("verifiedsource") == 1);
        double freeSpace = downloading ? diskSize - double.Parse(TguiFormat.ToFixed(diskUsed + completion), System.Globalization.CultureInfo.InvariantCulture) : diskSize - diskUsed;

        var content = new List<UiNode>();
        if (error != null)
        {
            content.Add(new UiNode(UiKind.NoticeBox) { Key = "error" }.With(
                new UiNode(UiKind.Box) { Text = error },
                new UiNode(UiKind.Button) { Key = "reset", Text = "Reset", OnClick = () => c.Act("PRG_reseterror") }));
        }
        UiNode driveButton = downloading
            ? new UiNode(UiKind.Button) { Key = "downloading", Icon = "spinner", Tooltip = "Download: " + downloadName + ".prg (" + percentage + "%)" }
            : !string.IsNullOrEmpty(downloadName) ? new UiNode(UiKind.Button) { Key = "downloaded", Color = "good", Icon = "download", Tooltip = downloadName + ".prg downloaded" } : null;
        content.Add(new UiNode(UiKind.Section) { Key = "drive" }.With(new UiNode(UiKind.LabeledList).With(
            new UiNode(UiKind.LabeledItem) { Label = "Hard drive", Buttons = driveButton }.With(
                new UiNode(UiKind.ProgressBar) { Key = "disk", Value = downloading ? diskUsed + completion : diskUsed, MinValue = 0, MaxValue = diskSize, Text = TguiFormat.JsNumber(freeSpace) + " GQ free of " + TguiFormat.JsNumber(diskSize) + " GQ" }))));
        content.Add(new UiNode(UiKind.Section) { Key = "searchsection" }.With(new UiNode(UiKind.Input)
        {
            Key = "search",
            AutoFocus = true,
            Tooltip = "Search program name...",
            Fluid = true,
            TextValue = searchItem,
            OnInput = v => c.SetState("search", v),
        }));
        content.Add(new UiNode(UiKind.Stack) { Key = "store" }.With(
            new UiNode(UiKind.StackItem) { Width = "105px" }.With(new UiNode(UiKind.Tabs) { Vertical = true }.With(categories.Select(category => new UiNode(UiKind.Tab)
            {
                Key = "tab:" + category.Text,
                Text = category.Text,
                Selected = category.Text == selectedCategory,
                OnClick = () => c.SetState("category", category.Text),
            }))),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(items.Select(program => Program(c, program)))));

        return NtosWindow.Render(c, 600, 600, null, new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Grow = 1, Scrollable = true }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(content))));
    }

    // tg: NtosNetDownloader.tsx:178
    private static UiNode Program(TguiContext c, UiValue program)
    {
        UiValue data = c.Data;
        double diskFree = data.Num("disk_size") - data.Num("disk_used");
        bool downloading = data.Truthy("downloading");
        string filename = program.Str("filename");
        bool installed = program.Truthy("installed");
        bool compatible = program.Truthy("compatible");
        bool access = program.Truthy("access");

        UiNode action;
        if (downloading && filename == data.Str("downloadname"))
            action = new UiNode(UiKind.ProgressBar) { Key = "progress", Color = "good", MinValue = 0, MaxValue = program.Num("size"), Value = data.Num("downloadcompletion") };
        else if (!installed && compatible && access && program.Num("size") < diskFree)
        {
            action = new UiNode(UiKind.Button)
            {
                Key = "download",
                Bold = true,
                Icon = "download",
                Text = "Download",
                Disabled = downloading,
                Tooltip = downloading ? "Awaiting download completion..." : null,
                OnClick = () => c.Act("PRG_downloadfile", ("filename", filename)),
            };
        }
        else
        {
            action = new UiNode(UiKind.Button)
            {
                Key = "state",
                Bold = true,
                Icon = installed ? "check" : "times",
                Color = installed ? "good" : !compatible ? "bad" : "grey",
                Text = installed ? "Installed" : !compatible ? "Incompatible" : !access ? "No Access" : "No Space",
            };
        }

        var section = new UiNode(UiKind.Section) { Key = "program:" + filename }.With(
            new UiNode(UiKind.Stack) { Align = "baseline" }.With(
                new UiNode(UiKind.StackItem) { Grow = 1, Bold = true, Icon = program.Str("icon"), Text = program.Str("filedesc") },
                new UiNode(UiKind.StackItem) { Width = "48px", TextAlign = "right", Color = "label", Text = TguiFormat.JsNumber(program.Num("size")) + " GQ" },
                new UiNode(UiKind.StackItem) { Width = "134px", TextAlign = "right" }.With(action)),
            new UiNode(UiKind.Box) { Key = "info", Wrap = true, Color = "label", Text = program.Str("fileinfo") });
        if (!program.Truthy("verifiedsource"))
            section.With(new UiNode(UiKind.NoticeBox) { Key = "unverified", Color = "bad", Text = "Unverified source. Please note that Nanotrasen does not recommend download and usage of software from non-official servers." });
        return section;
    }
}
