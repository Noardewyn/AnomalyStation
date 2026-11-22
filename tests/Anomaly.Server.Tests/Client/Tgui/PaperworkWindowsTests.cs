using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps package S3: the client's paperwork windows without the engine: PaperSheet (the paper's markdown, its fields and stamps, the
/// writing area and the stamper), Folder, Clipboard, FilingCabinet, NoticeBoard, Photocopier, Fax and NtosFaxBond read tg's ui_data the
/// way the TSX does and send tg's acts.
/// </summary>
public class PaperworkWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts, TguiLocalState Local) Render(TguiInterface view, string name, string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        local ??= new TguiLocalState();
        var context = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local);
        return (context, view(context), acts, local);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private const string Paper = "\"paper_name\":\"paper\",\"paper_color\":\"#FFFFFF\",\"default_pen_font\":\"Verdana\",\"default_pen_color\":\"#000000\",\"max_length\":5000,\"max_input_field_length\":42,"
        + "\"raw_text_input\":[{\"raw_text\":\"# Orders\\nBuy **milk** and [___].\\n- one\\n- two\",\"font\":\"Verdana\",\"color\":\"#0000FF\",\"bold\":0,\"advanced_html\":0}],"
        + "\"raw_field_input\":[],\"raw_stamp_input\":[{\"class\":\"stamp-cap\",\"x\":40,\"y\":60,\"rotation\":12}]";

    [Fact]
    public void A_read_paper_shows_its_markdown_its_open_field_and_its_stamp()
    {
        var (context, tree, _, _) = Render(PaperSheet.Render, "PaperSheet", "{" + Paper + "}");
        Assert.Equal((420.0, 500.0, "paper"), (context.WindowWidth, context.WindowHeight, context.WindowTheme));
        UiNode page = ByKey(tree, "page");
        Assert.Contains("<h1><b>Orders</b></h1>", page.TextValue);
        Assert.Contains("Buy <b>milk</b> and [___].", page.TextValue);
        Assert.Contains("• one", page.TextValue);
        Assert.Contains("color:#0000FF", page.TextValue);
        UiNode stamp = ByKey(tree, "stamp:0");
        Assert.Equal((PaperSheet.StampIcon, "paper_stamp-cap", 40.0, 60.0, 12.0), (stamp.DmIcon, stamp.DmIconState, stamp.PosX, stamp.PosY, stamp.Rotation));
        Assert.Null(page.Ghost);
        Assert.DoesNotContain(tree.Walk(), n => n.Kind == UiKind.TextArea);
    }

    [Fact]
    public void A_held_pen_writes_and_fills_the_open_field()
    {
        string json = "{" + Paper + ",\"held_item_details\":{\"interaction_mode\":1,\"font\":\"Segoe Script\",\"color\":\"#FF0000\",\"use_bold\":0}}";
        var (context, tree, acts, local) = Render(PaperSheet.Render, "PaperSheet", json);
        Assert.Equal(700.0, context.WindowHeight);
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.TextArea && n.Key == "paper_text");
        ByKey(tree, "paper_text").OnInput("Signed: *me*");
        ByKey(tree, "field:0").OnInput("eggs");
        tree = PaperSheet.Render(new TguiContext(1, "PaperSheet", "PaperSheet", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local));
        Assert.Contains("Signed: <i>me</i>", ByKey(tree, "page").TextValue); // the preview of the typed text, in the pen's colour
        Assert.Contains(tree.Walk(), n => n.Text == "56 / 5000"); // 44 on the paper and the 12 typed
        UiNode save = tree.Walk().Single(n => n.Key == "save");
        save.OnClick(); // Button.Confirm: the first click asks
        tree = PaperSheet.Render(new TguiContext(1, "PaperSheet", "PaperSheet", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local));
        tree.Walk().Single(n => n.Key == "save").OnClick();
        Assert.Equal(("add_text", "{\"text\":\"Signed: *me*\"}"), acts[0]);
        Assert.Equal(("fill_input_field", "{\"field_data\":{\"0\":\"eggs\"}}"), acts[1]);
    }

    [Fact]
    public void A_held_stamp_follows_the_mouse_and_stamps_where_clicked()
    {
        string json = "{" + Paper + ",\"held_item_details\":{\"interaction_mode\":2,\"stamp_class\":\"stamp-ok\",\"stamp_icon_state\":\"stamp-ok\"}}";
        var (_, tree, acts, _) = Render(PaperSheet.Render, "PaperSheet", json);
        UiNode page = ByKey(tree, "page");
        Assert.Equal(("paper_stamp-ok", 0.5), (page.Ghost.DmIconState, page.Ghost.Opacity));
        page.OnStamp(100, 220, -30);
        Assert.Equal(("add_stamp", "{\"x\":100,\"y\":220,\"rotation\":-30}"), acts[0]);
    }

    [Fact]
    public void A_filled_field_and_a_signature_show_in_the_text()
    {
        string json = "{" + Paper.Replace("\"raw_field_input\":[]", "\"raw_field_input\":[{\"field_index\":\"0\",\"field_data\":{\"raw_text\":\"Jane Doe\",\"font\":\"Times New Roman\",\"color\":\"#000000\"},\"is_signature\":1}]") + "}";
        var (_, tree, _, _) = Render(PaperSheet.Render, "PaperSheet", json);
        Assert.Contains("[<span style=\"color:#000000;font-weight: bold;font-style: italic;\">Jane Doe</span>]", ByKey(tree, "page").TextValue);
        Assert.Equal("Fields <b>a</b> <i>b</i> “q”—[__]", PaperSheet.MarkdownToHtml("Fields **a** _b_ \"q\"---[__]").Replace("<p>", "").Replace("</p>", ""));
    }

    [Fact]
    public void The_folder_lists_its_papers_and_sends_examine_and_remove()
    {
        var (context, tree, acts, _) = Render(Folder.Render, "Folder", "{\"bg_color\":\"#355e9f\",\"folder_name\":\"folder\",\"contents\":[\"The paper\"],\"contents_ref\":[\"[0x5]\"]}");
        Assert.Equal((400.0, 500.0, "folder"), (context.WindowWidth, context.WindowHeight, context.WindowTitle));
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.StackItem && n.Background == "#355e9f");
        Assert.Contains(tree.Walk(), n => n.Text == "The paper");
        ByKey(tree, "examine:[0x5]").OnClick();
        ByKey(tree, "remove:[0x5]").OnClick();
        Assert.Equal(new[] { ("examine", "{\"ref\":\"[0x5]\"}"), ("remove", "{\"ref\":\"[0x5]\"}") }, acts);
        (_, tree, _, _) = Render(Folder.Render, "Folder", "{\"bg_color\":\"#7f7f7f\",\"folder_name\":\"folder\",\"contents\":[],\"contents_ref\":[]}");
        Assert.Contains(tree.Walk(), n => n.Text == "This folder is empty!");
    }

    [Fact]
    public void The_clipboard_shows_its_pen_and_papers_and_moves_one_to_the_top()
    {
        var (_, tree, acts, _) = Render(Clipboard.Render, "Clipboard", "{\"pen\":\"the pen\",\"integrated_pen\":0,\"top_paper\":\"The paper\",\"top_paper_ref\":\"[0x1]\",\"paper\":[\"The memo\"],\"paper_ref\":[\"[0x2]\"]}");
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.LabeledItem && n.Label == "Pen");
        Assert.Equal("pen", ByKey(tree, "edit:[0x1]").Icon);
        ByKey(tree, "remove_pen").OnClick();
        ByKey(tree, "move_top:[0x2]").OnClick();
        ByKey(tree, "rename:[0x1]").OnClick();
        Assert.Equal(new[] { ("remove_pen", (string)null), ("move_top_paper", "{\"ref\":\"[0x2]\"}"), ("rename_paper", "{\"ref\":\"[0x1]\"}") }, acts);
        (_, tree, _, _) = Render(Clipboard.Render, "Clipboard", "{\"pen\":\"\",\"integrated_pen\":0,\"top_paper\":\"\",\"top_paper_ref\":\"[0x0]\",\"paper\":[],\"paper_ref\":[]}");
        Assert.Contains(tree.Walk(), n => n.Text == "No pen attached!");
        Assert.Contains(tree.Walk(), n => n.Text == "The clipboard is empty!");
    }

    [Fact]
    public void The_cabinet_and_the_notice_board_list_their_papers()
    {
        var (context, tree, acts, _) = Render(FilingCabinet.Render, "FilingCabinet", "{\"cabinet_name\":\"filing cabinet\",\"contents\":[\"The paper\"],\"contents_ref\":[\"[0x3]\"]}");
        Assert.Equal((350.0, 300.0), (context.WindowWidth, context.WindowHeight));
        ByKey(tree, "remove:[0x3]").OnClick();
        Assert.Equal(("remove_object", "{\"ref\":\"[0x3]\"}"), acts[0]);
        (_, tree, acts, _) = Render(NoticeBoard.Render, "NoticeBoard", "{\"allowed\":0,\"items\":[{\"name\":\"paper\",\"ref\":\"[0x4]\"}]}");
        Assert.True(ByKey(tree, "remove:[0x4]").Disabled);
        ByKey(tree, "examine:[0x4]").OnClick();
        Assert.Equal(("examine", "{\"ref\":\"[0x4]\"}"), acts[0]);
        (_, tree, _, _) = Render(NoticeBoard.Render, "NoticeBoard", "{\"allowed\":1,\"items\":[]}");
        Assert.Contains(tree.Walk(), n => n.Text == "The notice board is empty!");
    }

    [Fact]
    public void The_photocopier_copies_and_prints_a_chosen_blank()
    {
        string json = "{\"has_item\":1,\"has_toner\":1,\"is_photo\":0,\"isAI\":0,\"num_copies\":1,\"max_copies\":10,\"copies_left\":0,\"current_toner\":5,\"max_toner\":5,"
            + "\"paper_count\":30,\"max_paper_count\":60,\"created_paper\":\"/obj/item/paper\",\"categories\":[\"Command Department\"],"
            + "\"blanks\":[{\"name\":\"Access Request\",\"category\":\"Command Department\",\"code\":\"NT-CMD-AR\"}],"
            + "\"paper_types\":[{\"name\":\"paper\",\"icon\":\"icons/obj/service/bureaucracy.dmi\",\"icon_state\":\"paper\",\"amount\":30,\"type\":\"/obj/item/paper\"}]}";
        var (context, tree, acts, local) = Render(Photocopier.Render, "Photocopier", json);
        Assert.Equal(325.0, context.WindowWidth);
        Assert.True(ByKey(tree, "paper:/obj/item/paper").Selected);
        ByKey(tree, "make_copy").OnClick();
        ByKey(tree, "copies").OnChange(3);
        ByKey(tree, "category:Command Department").OnClick();
        var context2 = new TguiContext(1, "Photocopier", "Photocopier", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local);
        tree = Photocopier.Render(context2);
        Assert.Equal(550.0, context2.WindowWidth);
        ByKey(tree, "blank:NT-CMD-AR").OnClick();
        tree = Photocopier.Render(new TguiContext(1, "Photocopier", "Photocopier", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local));
        Assert.Contains(tree.Walk(), n => n.Text == "NT-CMD-AR");
        ByKey(tree, "print_blank").OnClick();
        Assert.Equal(new[] { ("make_copy", (string)null), ("set_copies", "{\"num_copies\":3}"), ("print_blank", "{\"code\":\"NT-CMD-AR\"}") }, acts);
    }

    [Fact]
    public void The_fax_lists_the_visible_faxes_and_sends()
    {
        string json = "{\"fax_name\":\"Captain's Office\",\"fax_id\":\"a1\",\"visible\":1,\"has_paper\":1,\"syndicate_network\":0,"
            + "\"faxes\":[{\"fax_name\":\"Quartermaster's Office\",\"fax_id\":\"b2\",\"visible\":1,\"has_paper\":0,\"syndicate_network\":0},"
            + "{\"fax_name\":\"Hidden\",\"fax_id\":\"c3\",\"visible\":0,\"has_paper\":0,\"syndicate_network\":0}],"
            + "\"fax_history\":[{\"history_type\":\"Receive\",\"history_fax_name\":\"HoP\",\"history_time\":\"12:01\"}],"
            + "\"special_faxes\":[{\"fax_name\":\"NT HR Department\",\"fax_id\":\"central_command\",\"color\":\"teal\",\"emag_needed\":0},{\"fax_name\":\"Sabotage Department\",\"fax_id\":\"syndicate\",\"color\":\"red\",\"emag_needed\":1}]}";
        var (context, tree, acts, _) = Render(Fax.Render, "Fax", json);
        Assert.Equal((340.0, 540.0), (context.WindowWidth, context.WindowHeight));
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "send:c3");
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "special:syndicate");
        Assert.Contains(tree.Walk(), n => n.Text == "HoP - 12:01");
        ByKey(tree, "send:b2").OnClick();
        ByKey(tree, "special:central_command").OnClick();
        Assert.Equal(new[] { ("send", "{\"id\":\"b2\",\"name\":\"Quartermaster\\u0027s Office\"}"), ("send_special", "{\"id\":\"central_command\",\"name\":\"NT HR Department\"}") }, acts);
    }

    [Fact]
    public void Faxbond_lists_the_connected_faxes_and_mutes_one()
    {
        var (_, tree, acts, _) = Render(NtosFaxBond.Render, "NtosFaxBond", "{\"faxes_info\":[{\"id\":\"a1\",\"name\":\"Captain's Office\",\"location\":\"Captain's Office\",\"muted\":0}],\"PC_device_theme\":\"ntos\"}");
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.NoticeBox && n.Text == "Scan any fax to be notified when it receives a message.");
        Assert.Equal("bell", ByKey(tree, "mute:a1").Icon);
        ByKey(tree, "mute:a1").OnClick();
        Assert.Equal(("mute", "{\"id\":\"a1\"}"), acts[0]);
    }
}
