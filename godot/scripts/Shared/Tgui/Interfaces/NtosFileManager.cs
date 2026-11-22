using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosFileManager.tsx (M9 slice E: the File Manager app).
// Engine-free (compiled into the tests too). Button.Input is an input that commits the new name. The print dialog (PrintDialog,
// :41-205) prints an image file: the image files come with the camera, so no file is printable and the dialog never opens.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosFileManager
{
    // tg: NtosFileManager.tsx:207
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool usbConnected = data.Truthy("usbconnected");
        IReadOnlyList<UiValue> files = data.Get("files")?.Items ?? Array.Empty<UiValue>();
        IReadOnlyList<UiValue> usbFiles = data.Get("usbfiles")?.Items ?? Array.Empty<UiValue>();

        var content = new List<UiNode>
        {
            new UiNode(UiKind.Section) { Key = "files" }.With(FileTable(c, "files", files, usbConnected, false,
                upload: name => c.Act("PRG_copytousb", ("name", name)),
                delete: name => c.Act("PRG_deletefile", ("name", name)),
                rename: (name, newName) => c.Act("PRG_renamefile", ("name", name), ("new_name", newName)),
                toggleSilence: name => c.Act("PRG_togglesilence", ("name", name)))),
        };
        if (usbConnected)
        {
            content.Add(new UiNode(UiKind.Section) { Key = "usb", Title = "Data Disk" }.With(FileTable(c, "usb", usbFiles, usbConnected, true,
                upload: name => c.Act("PRG_copyfromusb", ("name", name)),
                delete: name => c.Act("PRG_usbdeletefile", ("name", name)),
                rename: (name, newName) => c.Act("PRG_usbrenamefile", ("name", name), ("new_name", newName)),
                toggleSilence: null)));
        }

        return NtosWindow.Render(c, 575, 700, null, new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Grow = 1, Scrollable = true }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(content))));
    }

    // tg: NtosFileManager.tsx:298  FileTable
    private static UiNode FileTable(TguiContext c, string table, IReadOnlyList<UiValue> files, bool usbConnected, bool usbMode,
        Action<string> upload, Action<string> delete, Action<string, string> rename, Action<string> toggleSilence)
    {
        var rows = new List<UiNode>
        {
            new UiNode(UiKind.TableRow) { Key = "head" }.With(
                new UiNode(UiKind.TableCell) { Bold = true, Text = "File" },
                new UiNode(UiKind.TableCell) { Bold = true, Text = "Type" },
                new UiNode(UiKind.TableCell) { Bold = true, Text = "Size" }),
        };
        foreach (UiValue file in files)
        {
            string name = file.Str("name");
            bool undeletable = file.Truthy("undeletable");
            var buttons = new List<UiNode>();
            if (file.Truthy("alert_able"))
            {
                bool silenced = file.Truthy("alert_silenced");
                buttons.Add(new UiNode(UiKind.Button) { Key = "silence", Icon = silenced ? "bell-slash" : "bell", Color = silenced ? "red" : "default", Tooltip = silenced ? "Unmute Alerts" : "Mute Alerts", OnClick = () => toggleSilence?.Invoke(name) });
            }
            if (!undeletable)
            {
                buttons.Add(c.ButtonConfirm(table + ":delete:" + name, new UiNode(UiKind.Button) { Icon = "trash", Tooltip = "Delete", OnClick = () => delete(name) }));
                if (usbConnected)
                    buttons.Add(new UiNode(UiKind.Button) { Key = "transfer", Icon = usbMode ? "download" : "upload", Tooltip = usbMode ? "Download" : "Upload", OnClick = () => upload(name) });
            }
            rows.Add(new UiNode(UiKind.TableRow) { Key = "file:" + name, Candystripe = true }.With(
                new UiNode(UiKind.TableCell).With(!undeletable
                    ? new UiNode(UiKind.Input) { Key = "rename", Fluid = true, TextValue = name, OnCommit = v => rename(name, v) }
                    : new UiNode(UiKind.Box) { Key = "name", Text = name }),
                new UiNode(UiKind.TableCell) { Text = file.Str("type") },
                new UiNode(UiKind.TableCell) { Text = TguiFormat.JsNumber(file.Num("size")) },
                new UiNode(UiKind.TableCell).With(buttons)));
        }
        return new UiNode(UiKind.Table).With(rows);
    }
}
