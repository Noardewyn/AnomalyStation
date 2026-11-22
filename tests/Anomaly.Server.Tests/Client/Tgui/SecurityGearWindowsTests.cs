using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps package J4: the client's forensic, keycard and detective's board windows without the engine: ForensicScanner, ForensicsSpoofer.tsx, KeycardAuth.jsx and DetectiveBoard read tg's ui_data the way
/// the TSX does and send tg's acts.
/// </summary>
public class SecurityGearWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts, TguiLocalState Local) Render(TguiInterface view, string name, string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        local ??= new TguiLocalState();
        var context = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local);
        return (context, view(context), acts, local);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private const string Categories = "\"categories\":{\"Prints\":{\"name\":\"Fingerprints\",\"uiIcon\":\"fingerprint\",\"uiIconColor\":\"yellow\"},\"Blood\":{\"name\":\"Blood DNA, Type\",\"uiIcon\":\"droplet\",\"uiIconColor\":\"red\"},\"Reagents\":{\"name\":\"Reagents\",\"uiIcon\":\"flask\",\"uiIconColor\":\"blue\"}}";

    [Fact]
    public void An_empty_scanner_says_so()
    {
        var (context, tree, _, _) = Render(ForensicScanner.Render, "ForensicScanner", "{\"logs\":[]," + Categories + "}");
        Assert.Equal((512.0, 512.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.NoticeBox && n.Text == "Log empty.");
    }

    [Fact]
    public void A_scan_shows_its_prints_blood_and_reagents_newest_first_and_sends_tgs_acts()
    {
        const string json = "{\"logs\":["
            + "{\"scanTarget\":\"shard\",\"scanTime\":\"00:01:00\",\"dataEntries\":[{\"category\":\"Prints\",\"data\":{\"abc\":\"abc\"}},{\"category\":\"Blood\",\"data\":{\"dna1\":\"O+\"}},{\"category\":\"Reagents\",\"data\":{\"Water\":5}}]},"
            + "{\"scanTarget\":\"bob\",\"scanTime\":\"00:02:00\",\"dataEntries\":[{\"category\":\"Prints\",\"data\":[\"handprint\"]}]}"
            + "]," + Categories + "}";
        var (_, tree, acts, _) = Render(ForensicScanner.Render, "ForensicScanner", json);
        string[] titles = tree.Walk().Where(n => n.Kind == UiKind.Section && n.Key?.StartsWith("log-") == true).Select(n => n.Title).ToArray();
        Assert.Equal(new[] { "Bob scan at 00:02:00 ", "Shard scan at 00:01:00 " }, titles);
        string text = tree.AllText();
        Assert.Contains("abc", text);
        Assert.Contains("DNA1, O+", text);
        Assert.Contains("5 u.", text);
        Assert.Contains("handprint", text);
        ByKey(tree, "print").OnClick();
        ByKey(tree, "delete-0").OnClick();
        Assert.Equal("print", acts[0].Action);
        Assert.Equal(("delete", "{\"index\":0}"), acts[1]);
    }

    [Fact]
    public void The_spoofer_lists_prints_with_their_owners_and_fibres_on_the_second_tab()
    {
        const string json = "{\"silent\":0,\"scanmode\":1,\"fibers\":{\"grey cloth\":\"\"},\"fingerprints\":{\"0123456789012345678901234567890\":\"Alice\"},\"chosen_fiber\":null,\"chosen_fingerprint\":null,\"max_storage\":5}";
        var (context, tree, acts, local) = Render(ForensicsSpoofer.Render, "ForensicsSpoofer", json);
        Assert.Equal("syndicate", context.WindowTheme);
        Assert.Equal("Mode: Scan", ByKey(tree, "scanmode").Text);
        Assert.Equal("Fingerprints 1/5", ByKey(tree, "tab-prints").Text);
        Assert.Contains("0123456789012345678901234", tree.AllText());
        Assert.Contains("(Alice)", tree.AllText());
        ByKey(tree, "choose-0123456789012345678901234567890").OnClick();
        Assert.Equal("choose", acts[0].Action);
        ByKey(tree, "tab-fibers").OnClick();
        (_, tree, _, _) = Render(ForensicsSpoofer.Render, "ForensicsSpoofer", json, local);
        Assert.Contains("grey cloth", tree.AllText());
    }

    [Fact]
    public void The_keycard_device_shows_its_choices_its_wait_and_the_authorize_button()
    {
        var (context, tree, acts, _) = Render(KeycardAuth.Render, "KeycardAuth", "{\"waiting\":0,\"auth_required\":0,\"red_alert\":0,\"emergency_maint\":0,\"bsa_unlock\":0}");
        Assert.Equal((375.0, 145.0), (context.WindowWidth, context.WindowHeight));
        foreach (string key in new[] { "red_alert", "emergency_maint", "bsa_unlock", "give_janitor_access" })
            ByKey(tree, key).OnClick();
        Assert.Equal(new[] { "red_alert", "emergency_maint", "bsa_unlock", "give_janitor_access" }, acts.Select(a => a.Action));

        (_, tree, _, _) = Render(KeycardAuth.Render, "KeycardAuth", "{\"waiting\":1,\"auth_required\":0}");
        Assert.Contains("Waiting for another device", tree.AllText());
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "red_alert");

        (_, tree, acts, _) = Render(KeycardAuth.Render, "KeycardAuth", "{\"waiting\":0,\"auth_required\":\"Red Alert\"}");
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "red_alert");
        ByKey(tree, "auth_swipe").OnClick();
        Assert.Equal("auth_swipe", acts[0].Action);
    }

    [Fact]
    public void The_board_asks_for_a_first_case_then_places_its_evidence_and_ropes()
    {
        var (context, tree, acts, _) = Render(DetectiveBoard.Render, "DetectiveBoard", "{\"cases\":[],\"current_case\":1}");
        Assert.Equal((1200.0, 800.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains("You have no cases!", tree.AllText());
        ByKey(tree, "add_case").OnClick();
        Assert.Equal("add_case", acts[0].Action);

        const string json = "{\"current_case\":1,\"cases\":[{\"ref\":\"[c1]\",\"name\":\"The bar\",\"color\":\"red\",\"connections\":[{\"color\":\"red\",\"from\":{\"x\":15,\"y\":15},\"to\":{\"x\":315,\"y\":215}}],\"evidences\":["
            + "{\"ref\":\"[e1]\",\"name\":\"A\",\"type\":\"photo\",\"description\":\"the bar\",\"x\":0,\"y\":0,\"connections\":[\"[e2]\"]},"
            + "{\"ref\":\"[e2]\",\"name\":\"B\",\"type\":\"paper\",\"text\":\"hello\",\"description\":\"note\",\"x\":300,\"y\":200,\"connections\":[\"[e1]\"]},"
            + "{\"ref\":\"[e3]\",\"name\":\"C\",\"type\":\"paper\",\"text\":\"\",\"description\":\"\",\"x\":10,\"y\":400,\"connections\":[]}]}]}";
        TguiLocalState local;
        (_, tree, acts, local) = Render(DetectiveBoard.Render, "DetectiveBoard", json);
        UiNode board = tree.Walk().Single(n => n.Kind == UiKind.Board);
        Assert.Equal((15.0, 15.0, 315.0, 215.0, "red"), Assert.Single(board.BoardLines));
        Assert.Equal((300.0, 200.0), (board.Children[1].PosX, board.Children[1].PosY));
        board.Children[1].OnMoved(40, 50);
        Assert.Equal("set_evidence_cords", acts[0].Action);
        Assert.Contains("\"rel_x\":40", acts[0].Params);

        // a rope: the first pin, then another, one not yet tied to it
        ByKey(tree, "pin-[e1]").OnClick();
        (_, tree, acts, _) = Render(DetectiveBoard.Render, "DetectiveBoard", json, local);
        ByKey(tree, "pin-[e3]").OnClick();
        Assert.Equal(("add_connection", "{\"from_ref\":\"[e1]\",\"to_ref\":\"[e3]\"}"), acts[0]);
        ByKey(tree, "look-[e2]").OnClick();
        Assert.Equal("look_evidence", acts[1].Action);
    }
}
