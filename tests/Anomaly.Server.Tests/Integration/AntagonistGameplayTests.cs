using System.IO;
using System.Text.RegularExpressions;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Syndicate package A1 (D316) through the real server: an admin opens Bob's traitor panel with the console's traitor-panel (tg's Show
/// Traitor Panel, a browse() page, DV216), makes him a custom antagonist through the panel's link (tg's name prompt), adds a custom
/// objective (tg's type list and objective prompt, DV340), marks it complete and announces it; Bob gets tg's greeting and the
/// AntagInfoGeneric window that lists the objective; the admin's Check Antagonists lists him; at the round's end every player's
/// report shows the antagonist with tg's green text.
/// </summary>
public sealed class AntagonistGameplayTests : IDisposable
{
    private const int Second = 20;
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "anomaly-antag-" + Guid.NewGuid().ToString("N"));

    public AntagonistGameplayTests()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "admin_ranks.txt"), "Name = Host\nInclude = EVERYTHING\nExclude =\nEdit = EVERYTHING\n");
        File.WriteAllText(Path.Combine(_folder, "admins.txt"), "Boss = Host\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private ServerConfig Config() => new()
    {
        Seed = 7, RoomWidth = 24, RoomHeight = 24, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f,
        DevCommands = false, ConfigFile = Path.Combine(_folder, "server.txt"),
    };

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static UiWindowState Window(RealHeadlessWorld.Player p, string @interface) => p.Session.Replica.UiWindows.Values.SingleOrDefault(v => v.Interface == @interface);

    private static UiWindowState Page(RealHeadlessWorld.Player p, string part) =>
        p.Session.Replica.UiWindows.Values.Single(v => v.Interface == Limits.BrowseUiInterface && Html(v).Contains(part, StringComparison.Ordinal));

    private static string Html(UiWindowState window) => string.Concat(window.Merged.Get("html").Items.Select(i => i.Text));

    // the href of a page link whose query has the given part, as the client sends it back
    private static string Href(string html, string part)
    {
        Match m = Regex.Match(html, "href='(byond://[^']*" + Regex.Escape(part) + "[^']*)'");
        Assert.True(m.Success, "no link with " + part);
        return m.Groups[1].Value.Replace("&amp;", "&", StringComparison.Ordinal);
    }

    private static void Submit(RealHeadlessWorld w, RealHeadlessWorld.Player p, string @interface, string entry)
    {
        UiWindowState input = Window(p, @interface);
        Assert.True(input != null, "no " + @interface + ": " + string.Join(", ", p.Session.Replica.UiWindows.Values.Select(v => v.Interface)) + "\n" + w.Err);
        Assert.True(p.Session.UiAct(input.Window, "submit", UiValue.ObjectOf(("entry", UiValue.Of(entry)))));
        w.Run(Second);
    }

    [Fact]
    public void An_admin_makes_Bob_a_custom_antagonist_whose_window_lists_the_objective_and_the_round_end_reports_him()
    {
        using var w = new RealHeadlessWorld(config: Config(), strict: false);
        RealHeadlessWorld.Player boss = w.Join("Boss");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var bossTexts = new List<string>();
        var bobTexts = new List<string>();
        boss.Client.ServerTextReceived += t => bossTexts.Add(t.Html);
        bob.Client.ServerTextReceived += t => bobTexts.Add(t.Html);
        w.Run(4 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        if (!In(w, () => Dm.truthy(GlobalProcs.SSticker.HasRoundStarted())))
            In(w, () => GlobalProcs.SSticker.current_state = Defines.GAME_STATE_PLAYING); // the room's world has no lobby: its round is the room

        // the console's traitor-panel: tg's traitor panel of Bob's mind
        boss.Session.DevCommand("traitor-panel Bob");
        w.Run(Second);
        UiWindowState panel = Page(boss, "Mind currently owned by key: Bob");
        string html = Html(panel);
        Assert.Contains("<i><b>Custom:</b></i>", html);

        // the Custom category's link: tg's name prompt, then Bob is the antagonist
        Assert.True(boss.Session.Topic(Href(html, "add_antag=/datum/antagonist/custom")));
        w.Run(Second);
        Submit(w, boss, "TextInputModal", "Saboteur");
        Assert.Contains(bobTexts, t => t.Contains("You are Saboteur.", StringComparison.Ordinal));
        Assert.Contains(bossTexts, t => t.Contains("into Saboteur.", StringComparison.Ordinal));
        UiWindowState info = Window(bob, "AntagInfoGeneric");
        Assert.NotNull(info);
        Assert.Equal("Saboteur", info.Merged.Str("antag_name"));
        Assert.Empty(info.Merged.Arr("objectives"));

        // the refreshed panel's Add objective: tg's type list, the custom objective's text
        html = Html(Page(boss, "Mind currently owned by key: Bob"));
        Assert.Contains("Saboteur", html);
        Assert.True(boss.Session.Topic(Href(html, "obj_add=1")));
        w.Run(Second);
        Submit(w, boss, "ListInputWindow", "custom");
        Submit(w, boss, "TextInputModal", "Steal the bike horn");
        html = Html(Page(boss, "Mind currently owned by key: Bob"));
        Assert.Contains("Steal the bike horn", html);

        // Mark as complete, then Announce objectives: Bob's chat and his window's objectives
        Assert.True(boss.Session.Topic(Href(html, "obj_completed=")));
        w.Run(Second);
        html = Html(Page(boss, "Mind currently owned by key: Bob"));
        Assert.Contains("Mark as incomplete", html);
        Assert.True(boss.Session.Topic(Href(html, "obj_announce=1")));
        w.Run(Second);
        Assert.Contains(bobTexts, t => t.Contains("Your current objectives:", StringComparison.Ordinal));
        Assert.Contains(bobTexts, t => t.Contains("<B>Objective #1</B>: Steal the bike horn", StringComparison.Ordinal));
        info = Window(bob, "AntagInfoGeneric");
        var context = new TguiContext(info.Window, info.Interface, info.Title, 2, info.Merged, (_, _) => { }, new TguiLocalState());
        List<UiNode> drawn = Anomaly.Client.Shared.Tgui.Interfaces.AntagInfoGeneric.Render(context).Walk().ToList();
        Assert.Contains(drawn, n => n.Text == "You are the Saboteur!");
        Assert.Contains(drawn, n => n.Text == "#1: Steal the bike horn");

        // the console's check-antagonists: the Round Status page lists Bob under his category
        boss.Session.DevCommand("check-antagonists");
        w.Run(Second);
        UiWindowState status = Page(boss, "<h1><B>Round Status</B></h1>");
        Assert.Contains("Other antagonists", Html(status));
        Assert.Contains("(Saboteur)", Html(status));

        // the round's end: the report in every player's chat shows the antagonist and its green text
        using (w.World.Enter())
            Waiting.Detach(GlobalProcs.SSticker.declare_completion());
        w.Run(Second);
        string report = Assert.Single(bossTexts, t => t.Contains("boxed_message", StringComparison.Ordinal));
        Assert.Contains("The other antagonists were:", report);
        Assert.Contains("<b>Bob</b> was <b>", report);
        Assert.Contains("The Saboteur was successful!", report);
        Assert.Contains(bobTexts, t => t.Contains("The Saboteur was successful!", StringComparison.Ordinal));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
