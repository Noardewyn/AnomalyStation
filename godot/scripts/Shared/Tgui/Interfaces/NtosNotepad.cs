using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosNotepad.tsx (M9 slice E: the Notepad app). Engine-free
// (compiled into the tests too). The menu bar's dropdowns are buttons that open their items under the bar; the Edit menu's cut, copy,
// paste and delete (tg's document.execCommand) are the text area's own (its context menu and Ctrl+X, C, V); the status bar counts to
// the text's end (tg follows the caret); the dialogs are modals. The menu's Save sends "Save" (tg's handleSave sends "save", which the
// app does not answer).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosNotepad
{
    // tg: NtosNotepad.tsx:27
    private const string DefaultDocumentName = "Untitled";
    private const string TextKey = "text"; // the text area's key: its text is the local state "input:text"

    // tg: NtosNotepad.tsx:67  enum Dialogs
    private const int DialogNone = 0;
    private const int DialogUnsavedChanges = 1;
    private const int DialogAbout = 3;
    private const int DialogSelectFile = 4;
    private const int DialogSave = 5;

    // tg: NtosNotepad.tsx:258  getStatusCounts
    private static (int Line, int Column) StatusCounts(string text)
    {
        string[] lines = text.Split('\n');
        return (lines.Length, lines[^1].Length + 1);
    }

    // tg: NtosNotepad.tsx:392
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string note = data.Str("note") ?? "";
        string backendName = data.Str("documentName") ?? DefaultDocumentName;
        IReadOnlyList<UiValue> files = data.Get("files")?.Items ?? Array.Empty<UiValue>();

        // tg's useEffect on [note, documentName]: a new note from the app replaces the text, the original text and the name
        if (c.UseState<string>("server:note", null) != note || c.UseState<string>("server:name", null) != backendName)
        {
            c.Local.SetQuiet("server:note", note);
            c.Local.SetQuiet("server:name", backendName);
            c.Local.SetQuiet("original", note);
            c.Local.SetQuiet("input:" + TextKey, note);
            c.Local.SetQuiet("documentName", backendName);
        }
        string documentName = c.UseState("documentName", backendName);
        string originalText = c.UseState("original", note);
        string text = c.UseState("input:" + TextKey, note);
        int activeDialog = c.UseState("dialog", DialogNone);
        bool showStatusBar = c.UseState("statusBar", true);
        bool wordWrap = c.UseState("wordWrap", true);
        string openMenu = c.UseState<string>("menu", null);
        double? selectedUid = c.UseState<double?>("selectedFile", null);
        string saveName = c.UseState("saveName", DefaultDocumentName);
        string CurrentText() => c.UseState("input:" + TextKey, note);

        void CloseDialog() => c.SetState("dialog", DialogNone);
        void Save()
        {
            c.Act("Save", ("note", CurrentText()));
            c.SetState("dialog", DialogNone);
        }
        // tg: ensureUnsavedChangesAreHandled: the unsaved changes dialog instead of losing the text (its retry action is never run)
        bool Unsaved()
        {
            if (c.UseState("original", note) != CurrentText())
            {
                c.SetState("dialog", DialogUnsavedChanges);
                return true;
            }
            return false;
        }
        void Exit()
        {
            if (!Unsaved())
                c.Act("PC_exit");
        }
        void NewNote()
        {
            if (Unsaved())
                return;
            c.Local.SetQuiet("original", "");
            c.Local.SetQuiet("input:" + TextKey, "");
            c.SetState("documentName", DefaultDocumentName);
        }
        void OpenNote()
        {
            if (Unsaved())
                return;
            c.Local.SetQuiet("selectedFile", (double?)null);
            c.SetState("dialog", DialogSelectFile);
        }

        UiNode MenuItem(string key, string text, Action run, bool? toggled = null) => new UiNode(UiKind.Button)
        {
            Key = "item:" + key,
            Fluid = true,
            Color = "transparent",
            Icon = toggled == null ? null : toggled.Value ? "check" : "square-o",
            Text = text,
            OnClick = () =>
            {
                c.Local.SetQuiet("menu", (string)null);
                run();
            },
        };
        UiNode Menu(string entry, string label) => new UiNode(UiKind.Button)
        {
            Key = "menu:" + entry,
            Color = "transparent",
            Selected = openMenu == entry,
            Text = label,
            OnClick = () => c.SetState("menu", openMenu == entry ? null : entry),
        };

        // tg: NtosNotepad.tsx:87  NtosNotepadMenuBar
        var items = new List<UiNode>
        {
            new UiNode(UiKind.StackItem) { Key = "menubar" }.With(new UiNode(UiKind.Stack).With(
                Menu("file", "File"), Menu("format", "Format"), Menu("view", "View"), Menu("help", "Help"))),
        };
        if (openMenu != null)
        {
            UiNode[] entries = openMenu switch
            {
                "file" => new[]
                {
                    MenuItem("new", "New", NewNote),
                    MenuItem("open", "Open", OpenNote),
                    MenuItem("save", "Save", Save),
                    MenuItem("save_as", "Save As", () => c.SetState("dialog", DialogSave)),
                    new UiNode(UiKind.Divider) { Key = "firstSep", Rule = true },
                    MenuItem("exit", "Exit...", Exit),
                },
                "format" => new[] { MenuItem("wordWrap", "Word Wrap", () => c.SetState("wordWrap", !wordWrap), wordWrap) },
                "view" => new[] { MenuItem("statusBar", "Status Bar", () => c.SetState("statusBar", !showStatusBar), showStatusBar) },
                _ => new[] { MenuItem("aboutNotepad", "About Notepad", () => c.SetState("dialog", DialogAbout)) },
            };
            items.Add(new UiNode(UiKind.StackItem) { Key = "menuitems" }.With(new UiNode(UiKind.Section) { Width = "22rem" }.With(entries)));
        }
        items.Add(new UiNode(UiKind.StackItem) { Key = "area", Grow = 1 }.With(new UiNode(UiKind.Section) { Fill = true }.With(
            new UiNode(UiKind.TextArea) { Key = TextKey, AutoFocus = true, TextValue = text, Wrap = wordWrap, Height = "600px" })));
        if (showStatusBar)
        {
            (int line, int column) = StatusCounts(text);
            items.Add(new UiNode(UiKind.StackItem) { Key = "statusbar" }.With(new UiNode(UiKind.Stack) { Key = "status" }.With(
                new UiNode(UiKind.StackItem) { Width = "25rem", Text = "Press shift-enter to insert new line" },
                new UiNode(UiKind.StackItem) { Width = "15rem", Text = "Ln " + line + ", Col " + column },
                new UiNode(UiKind.StackItem) { Width = "5rem", Text = "100%" },
                new UiNode(UiKind.StackItem) { Width = "12rem", Text = "NtOS (LF)" },
                new UiNode(UiKind.StackItem) { Width = "12rem", Text = "UTF-8" })));
        }

        UiNode dialog = null;
        if (activeDialog == DialogSelectFile)
        {
            UiValue selected = files.FirstOrDefault(f => f.Num("uid") == selectedUid);
            dialog = new UiNode(UiKind.Modal) { Key = "openfile" }.With(new UiNode(UiKind.Section) { Title = "Open File" }.With(
                new UiNode(UiKind.Section) { Title = "Text Files" }.With(files.Count > 0
                    ? new UiNode(UiKind.Dropdown)
                    {
                        Key = "file",
                        Options = files.Select(f => f.Str("displayName")).ToList(),
                        TextValue = selected?.Str("displayName"),
                        Tooltip = "Pick a file...",
                        OnSelected = name => c.SetState<double?>("selectedFile", (files.FirstOrDefault(f => f.Str("displayName") == name) ?? files[0]).Num("uid")),
                    }
                    : new UiNode(UiKind.Box) { Text = "No text files available." }),
                new UiNode(UiKind.Stack).With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "open",
                        Text = "Open",
                        OnClick = () =>
                        {
                            if (selected == null)
                                return;
                            c.Act("Open", ("uid", selected.Num("uid")), ("onDisk", selected.Truthy("onDisk")), ("name", selected.Str("name")));
                            CloseDialog();
                        },
                    },
                    new UiNode(UiKind.Button) { Key = "cancel", Text = "Cancel", OnClick = CloseDialog })));
        }
        else if (activeDialog == DialogSave)
        {
            dialog = new UiNode(UiKind.Modal) { Key = "saveas" }.With(new UiNode(UiKind.Section) { Title = "Save As" }.With(
                new UiNode(UiKind.Stack) { Justify = "space-between" }.With(
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Input) { Key = "saveName", AutoFocus = true, Fluid = true, TextValue = saveName, OnInput = v => c.Local.SetQuiet("saveName", v) }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Dropdown) { Key = "extension", Options = new[] { "TXT" }, TextValue = "TXT", Tooltip = "Pick an extension..." })),
                new UiNode(UiKind.Stack).With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "save",
                        Text = "Save",
                        OnClick = () =>
                        {
                            c.Act("SaveAs", ("name", c.UseState("saveName", DefaultDocumentName)), ("extension", "TXT"), ("note", CurrentText()));
                            CloseDialog();
                        },
                    },
                    new UiNode(UiKind.Button) { Key = "cancel", Text = "Cancel", OnClick = CloseDialog })));
        }
        else if (activeDialog == DialogUnsavedChanges)
        {
            dialog = new UiNode(UiKind.Modal) { Key = "unsaved" }.With(new UiNode(UiKind.Section) { Title = "Notepad" }.With(
                new UiNode(UiKind.Box) { Text = "Do you want to save changes to " + documentName + "?" },
                new UiNode(UiKind.Stack).With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "save",
                        Text = "Save",
                        OnClick = () =>
                        {
                            c.Act("Save", ("note", CurrentText()));
                            CloseDialog();
                        },
                    },
                    new UiNode(UiKind.Button) { Key = "dontsave", Text = "Don't Save", OnClick = CloseDialog },
                    new UiNode(UiKind.Button) { Key = "cancel", Text = "Cancel", OnClick = CloseDialog })));
        }
        else if (activeDialog == DialogAbout)
        {
            // tg: NtosNotepad.tsx:320  AboutDialog
            UiValue login = data.Get("login");
            dialog = new UiNode(UiKind.Modal) { Key = "about" }.With(new UiNode(UiKind.Section) { Title = "About Notepad" }.With(
                new UiNode(UiKind.Box) { FontSize = "32px", Bold = true, Text = "NtOS" },
                new UiNode(UiKind.Divider) { Rule = true },
                new UiNode(UiKind.Box) { Text = "Nanotrasen NtOS" },
                new UiNode(UiKind.Box) { Text = "Version 7815696ecbf1c96e6894b779456d330e" },
                new UiNode(UiKind.Box) { Text = "© NT Corporation. All rights reserved." },
                new UiNode(UiKind.Box) { Wrap = true, Text = "The NtOS operating system and its user interface are protected by trademark and other pending or existing intellectual property rights in the Sol system and other regions." },
                new UiNode(UiKind.Box) { Text = "This product is licensed under the NT Corporation Terms to:" },
                new UiNode(UiKind.Box) { Text = data.Truthy("show_imprint") ? login?.Str("IDName") : "Unknown" },
                new UiNode(UiKind.Button) { Key = "ok", Text = "Ok", OnClick = CloseDialog }));
        }
        if (dialog != null)
            items.Add(dialog);

        // MS Notepad displays an asterisk when there's unsaved changes
        string unsavedAsterisk = text != originalText ? "*" : "";
        return NtosWindow.Render(c, 840, 900, unsavedAsterisk + documentName + " - Notepad", new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(items));
    }
}
