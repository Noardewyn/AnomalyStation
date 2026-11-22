using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// M9 slice E: the client's NtOS windows (tg's NtosWindow layout, NtosMain, NtosMessenger, NtosNotepad, NtosCrewManifest,
/// NtosThemeConfigure, NtosNetDownloader, NtosFileManager), the ghosts' CrewManifest, MessageMonitor and NtnetRelay without the engine;
/// the payloads follow the servers' ui_data (computer_ui.dm:88, messenger_program.dm:344, notepad.dm:101, crewmanifest.dm:12,
/// theme_selector.dm:15, ntdownloader.dm:139, file_browser.dm:245, manifest.dm:209, message.dm:92, relays.dm:108).
/// </summary>
public class NtosWindowsTests
{
    private static (TguiContext Context, List<(string Action, string Params)> Acts) Context(string ui, string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, ui, ui, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (context, acts);
    }

    private const string Header = "\"PC_device_theme\":\"ntos_terminal\",\"PC_batteryicon\":\"batt_80.gif\",\"PC_batterypercent\":\"79%\",\"PC_ntneticon\":\"sig_high.gif\","
        + "\"PC_stationtime\":\"00:12:30\",\"PC_stationdate\":\"Fri, October 23, 2566\",\"PC_programheaders\":[{\"icon\":\"ntnrc_idle.gif\"}],\"PC_lowpower_mode\":0";

    [Fact]
    public void TheMainMenuListsTheAppsInTheDevicesTheme()
    {
        var (c, acts) = Context("NtosMain", "{" + Header + ",\"PC_showexitprogram\":0,\"show_imprint\":1,\"has_light\":1,\"light_on\":0,\"comp_light_color\":\"#FFFFFF\","
            + "\"login\":{\"IDName\":\"Alice Doe\",\"IDJob\":\"Assistant\"},\"proposed_login\":{\"IDInserted\":0,\"IDName\":null,\"IDJob\":null},\"removable_media\":[],"
            + "\"programs\":[{\"name\":\"nt_messenger\",\"desc\":\"Direct Messenger\",\"header_program\":1,\"running\":0,\"icon\":\"comment-alt\",\"alert\":0},"
            + "{\"name\":\"notepad\",\"desc\":\"Notepad\",\"header_program\":0,\"running\":1,\"icon\":\"book\",\"alert\":0}],\"alert_style\":0}");
        UiNode tree = NtosMain.Render(c);
        Assert.Equal((400.0, 500.0, "NtOS Main Menu", "ntos_terminal"), (c.WindowWidth, c.WindowHeight, c.WindowTitle, c.WindowTheme));
        Assert.Equal("icons/ui/programs/batt_80.gif", tree.Walk().Single(n => n.Key == "battery").DmIcon);
        Assert.Equal("icons/ui/programs/ntnrc_idle.gif", tree.Walk().Single(n => n.Key == "programheader").DmIcon);
        Assert.Contains(tree.Walk(), n => n.Key == "shutdown"); // no program open: the power button
        Assert.Equal("ID Name: Alice Doe ", tree.Walk().Single(n => n.Key == "idname" && n.Kind == UiKind.TableCell).Text);
        Assert.Contains(tree.Walk(), n => n.Key == "run" && n.Text == "Direct Messenger"); // the header program in the top section
        UiNode notepad = tree.Walk().Single(n => n.Key == "program:notepad");
        notepad.Walk().Single(n => n.Key == "run").OnClick();
        notepad.Walk().Single(n => n.Key == "kill").OnClick();
        tree.Walk().Single(n => n.Key == "shutdown").OnClick();
        Assert.Equal(("PC_runprogram", "{\"name\":\"notepad\"}"), acts[0]);
        Assert.Equal(("PC_killprogram", "{\"name\":\"notepad\"}"), acts[1]);
        Assert.Equal(("PC_shutdown", (string)null), acts[2]);
    }

    [Fact]
    public void AnUnportedProgramKeepsNtOSsCloseAndMinimize()
    {
        var (c, acts) = Context("NtosRoboControl", "{" + Header + ",\"PC_showexitprogram\":1}");
        UiNode tree = TguiInterfaces.Missing(c);
        Assert.Contains(tree.Walk(), n => n.Text != null && n.Text.Contains("NtosRoboControl", StringComparison.Ordinal));
        tree.Walk().Single(n => n.Key == "minimize").OnClick();
        tree.Walk().Single(n => n.Key == "close").OnClick();
        Assert.Equal(new[] { ("PC_minimize", (string)null), ("PC_exit", (string)null) }, acts);
        Assert.DoesNotContain(TguiInterfaces.Missing(Context("Stool", "{}").Context).Walk(), n => n.Key == "close"); // not NtOS: the plain notice
    }

    private const string Messenger ="\"PC_showexitprogram\":1,\"owner\":{\"name\":\"Alice Doe\",\"job\":\"Assistant\",\"ref\":\"[0x1]\"},\"messengers\":{\"[0x2]\":{\"name\":\"Bob Roe\",\"job\":\"Cook\",\"ref\":\"[0x2]\"}},"
        + "\"sort_by_job\":1,\"alert_silenced\":0,\"alert_able\":1,\"sending_and_receiving\":1,\"can_spam\":0,\"is_silicon\":0,\"remote_silicon\":0,\"on_spam_cooldown\":0,\"stored_photos\":[]";

    [Fact]
    public void TheMessengerListsContactsAndOpensAChat()
    {
        var (c, acts) = Context("NtosMessenger", "{" + Header + "," + Messenger + ",\"saved_chats\":[],\"open_chat\":null}");
        UiNode tree = NtosMessenger.Render(c);
        Assert.Equal((600.0, 850.0), (c.WindowWidth, c.WindowHeight));
        UiNode bob = tree.Walk().Single(n => n.Key == "chat");
        Assert.Equal(" Bob Roe (Cook)", bob.Text);
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "noid");
        bob.OnClick();
        tree.Walk().Single(n => n.Key == "ringer").OnClick();
        Assert.Equal(("PDA_viewMessages", "{\"ref\":\"[0x2]\"}"), acts[0]);
        Assert.Equal(("PDA_toggleAlerts", (string)null), acts[1]);

        // no owner (no imprint): tg's dimmer asks for an ID
        var (noId, _) = Context("NtosMessenger", "{" + Header + "," + Messenger.Replace("\"owner\":{\"name\":\"Alice Doe\",\"job\":\"Assistant\",\"ref\":\"[0x1]\"}", "\"owner\":null") + ",\"saved_chats\":[],\"open_chat\":null}");
        Assert.Contains(NtosMessenger.Render(noId).Walk(), n => n.Key == "noid");
    }

    [Fact]
    public void AChatShowsItsBubblesAndSendsTheTypedLine()
    {
        string chat = "\"saved_chats\":{\"[0x9]\":{\"ref\":\"[0x9]\",\"recipient\":{\"name\":\"Bob Roe\",\"job\":\"Cook\",\"ref\":\"[0x2]\"},\"messages\":["
            + "{\"message\":\"hi &amp; bye\",\"outgoing\":1,\"photo_path\":null,\"everyone\":0,\"timestamp\":\"00:10\"},"
            + "{\"message\":\"hello\",\"outgoing\":0,\"photo_path\":null,\"everyone\":0,\"timestamp\":\"00:11\"}],"
            + "\"message_draft\":\"\",\"visible\":1,\"can_reply\":1,\"unread_messages\":1}},\"open_chat\":\"[0x9]\"";
        var local = new TguiLocalState();
        var (c, acts) = Context("NtosMessenger", "{" + Header + "," + Messenger + "," + chat + "}", local);
        UiNode tree = NtosMessenger.Render(c);
        Assert.Equal(("PDA_clearUnreads", "{\"ref\":\"[0x9]\"}"), acts[0]); // shown: the unread line is read
        Assert.Equal(new[] { "hi & bye", "hello" }, tree.Walk().Where(n => n.Key == "text").Select(n => n.Text));
        Assert.Contains(tree.Walk(), n => n.Key == "outgoing");
        Assert.Contains(tree.Walk(), n => n.Key == "incoming");
        Assert.Contains(tree.Walk(), n => n.Key == "unread"); // tg's Unread Messages divider over the last one
        tree.Walk().Single(n => n.Key == "chatInput").OnInput("on my way");
        NtosMessenger.Render(c).Walk().Single(n => n.Key == "send").OnClick();
        Assert.Equal(("PDA_sendMessage", "{\"ref\":\"[0x9]\",\"message\":\"on my way\"}"), acts.Last());
        Assert.Single(acts, a => a.Action == "PDA_clearUnreads"); // read once
    }

    [Fact]
    public void TheNotepadSavesItsTextAndOpensItsMenus()
    {
        var local = new TguiLocalState();
        var (c, acts) = Context("NtosNotepad", "{" + Header + ",\"PC_showexitprogram\":1,\"note\":\"line one\\nline two\",\"documentName\":\"Untitled\",\"files\":[{\"uid\":7,\"name\":\"todo\",\"filetype\":\"TXT\",\"onDisk\":0,\"displayName\":\"todo.TXT\"}]}", local);
        UiNode tree = NtosNotepad.Render(c);
        Assert.Equal("Untitled - Notepad", c.WindowTitle);
        Assert.Equal("line one\nline two", tree.Walk().Single(n => n.Kind == UiKind.TextArea).TextValue);
        Assert.Equal("Ln 2, Col 9", tree.Walk().Single(n => n.Key == "status").Children[1].Text);
        local.SetQuiet("input:text", "changed");
        tree = NtosNotepad.Render(c);
        Assert.Equal("*Untitled - Notepad", c.WindowTitle);
        tree.Walk().Single(n => n.Key == "menu:file").OnClick();
        tree = NtosNotepad.Render(c);
        tree.Walk().Single(n => n.Key == "item:save").OnClick();
        Assert.Equal(("Save", "{\"note\":\"changed\"}"), acts.Last());
        // File > Open with unsaved changes: tg's dialog first
        NtosNotepad.Render(c).Walk().Single(n => n.Key == "menu:file").OnClick();
        NtosNotepad.Render(c).Walk().Single(n => n.Key == "item:open").OnClick();
        Assert.Contains(NtosNotepad.Render(c).Walk(), n => n.Key == "unsaved");
    }

    [Fact]
    public void TheCrewManifestsListTheDepartments()
    {
        const string Manifest = "\"manifest\":{\"Service\":[{\"name\":\"Bob Roe\",\"rank\":\"Cook\",\"trim\":\"Cook\"}],\"Misc\":[{\"name\":\"Alice Doe\",\"rank\":\"Assistant\",\"trim\":\"Assistant\"}]}";
        var (app, acts) = Context("NtosCrewManifest", "{" + Header + ",\"PC_showexitprogram\":1," + Manifest + "}");
        UiNode tree = NtosCrewManifest.Render(app);
        Assert.Equal(new[] { "Service", "Misc" }, tree.Walk().Where(n => n.Key?.StartsWith("dept:", StringComparison.Ordinal) == true).Select(n => n.Title));
        tree.Walk().Single(n => n.Key == "print").OnClick();
        Assert.Equal(("PRG_print", (string)null), acts[0]);

        var (ghost, _) = Context("CrewManifest", "{" + Manifest + ",\"positions\":{\"Service\":{\"exceptions\":[\"Cook\"],\"open\":3,\"color\":\"#bbe291\"},\"Misc\":{\"exceptions\":[],\"open\":0,\"color\":\"#ffffff\"}}}");
        tree = CrewManifest.Render(ghost);
        Assert.Equal("Service (3 positions open)", tree.Walk().Single(n => n.Key == "dept:Service").Title);
        Assert.Equal("Misc", tree.Walk().Single(n => n.Key == "dept:Misc").Title);
        Assert.Contains(tree.Walk(), n => n.Key == "infinity"); // the cook's unlimited job
    }

    [Fact]
    public void ThemeifyDownloaderAndFileManagerSendTheirActs()
    {
        var (themes, themeActs) = Context("NtosThemeConfigure", "{" + Header + ",\"themes\":[{\"theme_name\":\"NtOS\",\"theme_ref\":\"ntos\"},{\"theme_name\":\"Terminal\",\"theme_ref\":\"ntos_terminal\"}]}");
        UiNode tree = NtosThemeConfigure.Render(themes);
        Assert.Equal("check-square-o", tree.Walk().Single(n => n.Key == "theme:Terminal").Children[0].Icon);
        tree.Walk().Single(n => n.Key == "theme:NtOS").Children[0].OnClick();
        Assert.Equal(("PRG_change_theme", "{\"selected_theme\":\"NtOS\"}"), themeActs[0]);

        var (hub, hubActs) = Context("NtosNetDownloader", "{" + Header + ",\"downloading\":0,\"error\":0,\"disk_size\":64,\"disk_used\":10,\"emagged\":0,\"categories\":[\"Device Tools\",\"Games\"],"
            + "\"programs\":[{\"icon\":\"gamepad\",\"filename\":\"arcade\",\"filedesc\":\"Nanotrasen Micro Arcade\",\"fileinfo\":\"A port of the classic game.\",\"category\":\"Games\",\"installed\":0,\"compatible\":1,\"size\":6,\"access\":1,\"verifiedsource\":1}]}");
        tree = NtosNetDownloader.Render(hub);
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "program:arcade"); // another category
        tree.Walk().Single(n => n.Key == "tab:Games").OnClick();
        tree = NtosNetDownloader.Render(hub);
        tree.Walk().Single(n => n.Key == "download").OnClick();
        Assert.Equal(("PRG_downloadfile", "{\"filename\":\"arcade\"}"), hubActs[0]);
        Assert.Equal("54 GQ free of 64 GQ", tree.Walk().Single(n => n.Key == "disk").Text);

        var (files, fileActs) = Context("NtosFileManager", "{" + Header + ",\"files\":[{\"name\":\"todo\",\"type\":\"TXT\",\"size\":1,\"undeletable\":0,\"alert_able\":0,\"alert_silenced\":0,\"printable\":0},"
            + "{\"name\":\"nt_messenger\",\"type\":\"PRG\",\"size\":0,\"undeletable\":1,\"alert_able\":1,\"alert_silenced\":0,\"printable\":0}],\"printTypes\":[]}");
        tree = NtosFileManager.Render(files);
        tree.Walk().Single(n => n.Key == "file:nt_messenger").Walk().Single(n => n.Key == "silence").OnClick();
        tree.Walk().Single(n => n.Key == "file:todo").Walk().Single(n => n.Key == "rename").OnCommit("shopping");
        Assert.Equal(("PRG_togglesilence", "{\"name\":\"nt_messenger\"}"), fileActs[0]);
        Assert.Equal(("PRG_renamefile", "{\"name\":\"todo\",\"new_name\":\"shopping\"}"), fileActs[1]);
    }

    [Fact]
    public void TheMessageMonitorAndTheRelaySendTheirActs()
    {
        var (monitor, acts) = Context("MessageMonitor", "{\"screen\":0,\"error_message\":\"\",\"notice_message\":\"\",\"success_message\":\"\",\"auth\":0,\"server_status\":1,\"password\":\"\",\"status\":1,\"is_malf\":0,\"is_on_station\":1}");
        UiNode tree = MessageMonitor.Render(monitor);
        tree.Walk().Single(n => n.Key == "authPassword").OnInput("duckbeer3");
        MessageMonitor.Render(monitor).Walk().Single(n => n.Key == "auth").OnClick();
        Assert.Equal(("auth", "{\"auth_password\":\"duckbeer3\"}"), acts[0]);

        var (logs, _) = Context("MessageMonitor", "{\"screen\":1,\"error_message\":\"\",\"notice_message\":\"\",\"success_message\":\"\",\"auth\":1,\"server_status\":1,"
            + "\"messages\":[{\"ref\":\"[0x5]\",\"sender\":\"Alice Doe (Assistant)\",\"recipient\":\"Bob Roe (Cook)\",\"message\":\"hi\"}]}");
        Assert.Contains(MessageMonitor.Render(logs).Walk(), n => n.Kind == UiKind.TableCell && n.Text == "Bob Roe (Cook)");

        var (relay, relayActs) = Context("NtnetRelay", "{\"enabled\":1,\"dos_capacity\":500,\"dos_overload\":0,\"dos_crashed\":0}");
        tree = NtnetRelay.Render(relay);
        Assert.Equal("NtNet Quantum Relay", relay.WindowTitle);
        tree.Walk().Single(n => n.Key == "toggle").OnClick();
        Assert.Equal(("toggle", (string)null), relayActs[0]);
    }
}
