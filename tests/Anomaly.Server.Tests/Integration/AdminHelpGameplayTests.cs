using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M10x slice G6 (D282): tg's admin help through the real server. A player's Esc menu Admin Help opens tg's Adminhelp window; the
/// ticket reaches the admins with tg's sound and links, and the verb waits; the admin's reply link asks for a message that reaches the
/// player in tg's box with the sound, the Esc menu blinks, and View Latest Ticket shows tg's player panel (a browse() window, DV216);
/// the player answers through the admin's name; the admin's console "tickets" lists the ticket, its link opens tg's ticket panel and
/// RSLVE resolves it, which gives the verb back. With no admin on, the ticket goes to the Discord webhook.
/// </summary>
public sealed class AdminHelpGameplayTests : IDisposable
{
    private const int Second = 20;
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "anomaly-ahelp-" + Guid.NewGuid().ToString("N"));

    public AdminHelpGameplayTests()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "admin_ranks.txt"), "Name = Host\nInclude = EVERYTHING\nExclude =\nEdit = EVERYTHING\n");
        File.WriteAllText(Path.Combine(_folder, "admins.txt"), "Boss = Host\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private ServerConfig Config(string webhook = null) => new()
    {
        Seed = 7, RoomWidth = 24, RoomHeight = 24, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f,
        DevCommands = false, ConfigFile = Path.Combine(_folder, "server.txt"), AdminhelpWebhook = webhook,
    };

    private static UiWindowState Menu(RealHeadlessWorld.Player p) => p.Session.Replica.UiWindows.GetValueOrDefault(Limits.EscapeMenuUiWindow);

    private static UiWindowState Window(RealHeadlessWorld.Player p, string @interface) => p.Session.Replica.UiWindows.Values.SingleOrDefault(v => v.Interface == @interface);

    // the href of a chat or page link whose query has the given part, as the client sends it back
    private static string Href(string html, string part)
    {
        Match m = Regex.Match(html, "href='(byond://[^']*" + Regex.Escape(part) + "[^']*)'");
        return m.Success ? m.Groups[1].Value.Replace("&amp;", "&", StringComparison.Ordinal) : null;
    }

    private static string Page(UiWindowState window) => string.Concat(window.Merged.Get("html").Items.Select(i => i.Text));

    private static void Submit(RealHeadlessWorld w, RealHeadlessWorld.Player p, string text)
    {
        UiWindowState input = Window(p, "TextInputModal");
        Assert.NotNull(input);
        Assert.True(p.Session.UiAct(input.Window, "submit", UiValue.ObjectOf(("entry", UiValue.Of(text)))));
        w.Run(Second);
    }

    [Fact]
    public void A_ticket_goes_to_the_admin_and_back_and_the_admin_resolves_it()
    {
        using var w = new RealHeadlessWorld(config: Config(), strict: false);
        RealHeadlessWorld.Player boss = w.Join("Boss");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var bossTexts = new List<string>();
        var bobTexts = new List<string>();
        boss.Client.ServerTextReceived += t => bossTexts.Add(t.Html);
        bob.Client.ServerTextReceived += t => bobTexts.Add(t.Html);
        var bossSounds = new List<SoundEvent>();
        var bobSounds = new List<SoundEvent>();
        boss.Session.Replica.SoundsReceived += e => bossSounds.AddRange(e);
        bob.Session.Replica.SoundsReceived += e => bobSounds.AddRange(e);
        w.Run(4 * Second); // tg's escape menu comes 3 s after the connection
        int bootRuntimes = w.World.RuntimeErrorCount;
        Assert.True(Menu(bob).Data.Get("canAdminHelp").Truthy);

        // the Esc menu's Admin Help, Create Admin Ticket: tg's Adminhelp window, drawn by the client
        Assert.True(bob.Session.UiAct(Limits.EscapeMenuUiWindow, "create_ticket", null));
        w.Run(Second);
        UiWindowState box = Window(bob, "Adminhelp");
        Assert.NotNull(box);
        var context = new TguiContext(box.Window, box.Interface, box.Title, 2, box.Merged, (_, _) => { }, new TguiLocalState());
        Assert.Contains(Anomaly.Client.Shared.Tgui.Interfaces.Adminhelp.Render(context).Walk(), n => n.Kind == UiKind.Button && n.Text == "Submit");
        Assert.Equal(1, box.Merged.Num("adminCount"));
        Assert.True(bob.Session.UiAct(box.Window, "ahelp", UiValue.ObjectOf(("message", UiValue.Of("The clown stole my shoes")), ("urgent", UiValue.Of(false)))));
        w.Run(Second);
        Assert.Null(Window(bob, "Adminhelp")); // ui.close()
        Assert.Contains(bobTexts, t => t.Contains("PM to-<b>Admins</b>", StringComparison.Ordinal) && t.Contains("The clown stole my shoes", StringComparison.Ordinal));
        string ticket = Assert.Single(bossTexts, t => t.Contains("The clown stole my shoes", StringComparison.Ordinal));
        Assert.Contains("Ticket", ticket);
        Assert.Contains(bossSounds, e => e.File == "sound/effects/adminhelp.ogg");
        Assert.False(Menu(bob).Data.Get("canAdminHelp").Truthy); // TimeoutVerb: the verb waits 2 minutes

        // the admin's reply link: tg's text input, the player's admin PM box and the sound
        Assert.True(boss.Session.Topic(Href(ticket, "ahelp_action=reply")));
        w.Run(Second);
        Submit(w, boss, "Give them back");
        Assert.Contains(bobTexts, t => t.Contains("Admin PM from-", StringComparison.Ordinal) && t.Contains("Give them back", StringComparison.Ordinal));
        Assert.Contains(bobSounds, e => e.File == "sound/effects/adminhelp.ogg");
        Assert.True(bob.Session.UiAct(Limits.EscapeMenuUiWindow, "opened", null));
        w.Run(Second);
        Assert.True(Menu(bob).Data.Get("hasTicketNotification").Truthy); // Admin Help blinks (DV218)

        // View Latest Ticket: tg's player panel, a browse() window the client draws as tg's chat draws its lines
        Assert.True(bob.Session.UiAct(Limits.EscapeMenuUiWindow, "view_ticket", null));
        w.Run(Second);
        UiWindowState panel = Window(bob, Limits.BrowseUiInterface);
        Assert.NotNull(panel);
        Assert.InRange(panel.Window, Limits.BrowseUiWindowFirst, Limits.EscapeMenuUiWindow - 1);
        Assert.Equal("Player Ticket", panel.Title);
        string html = Page(panel);
        Assert.Contains("Give them back", html);
        var page = new TguiContext(panel.Window, panel.Interface, panel.Title, 2, panel.Merged, (_, _) => { }, new TguiLocalState());
        UiNode drawn = Assert.Single(Anomaly.Client.Shared.Tgui.Interfaces.Browser.Render(page).Walk(), n => n.Kind == UiKind.Html);
        string plain = HtmlText.ToPlain(drawn.TextValue);
        Assert.Contains("State: OPEN", plain);
        Assert.DoesNotContain("<", plain);
        Assert.Equal(620, page.WindowWidth);

        // the player answers through the admin's name in the box (key_name's priv_msg link)
        string answer = bobTexts.Last(t => t.Contains("Admin PM from-", StringComparison.Ordinal));
        Assert.True(bob.Session.Topic(Href(answer, "priv_msg=")));
        w.Run(Second);
        Submit(w, bob, "Thanks");
        Assert.Contains(bossTexts, t => t.Contains("Reply PM from-", StringComparison.Ordinal) && t.Contains("Thanks", StringComparison.Ordinal));

        // the admin's console: the active tickets, the ticket's panel and its RSLVE
        boss.Session.DevCommand("tickets");
        w.Run(Second);
        UiWindowState list = Assert.Single(boss.Session.Replica.UiWindows.Values, v => v.Interface == Limits.BrowseUiInterface && v.Title == "Active Tickets");
        Assert.True(boss.Session.Topic(Href(Page(list), "ahelp_action=ticket")));
        w.Run(Second);
        UiWindowState ticketPanel = Assert.Single(boss.Session.Replica.UiWindows.Values, v => v.Interface == Limits.BrowseUiInterface && v.Title == "Ticket #1");
        Assert.Contains("Thanks", Page(ticketPanel));
        Assert.True(boss.Session.Topic(Href(Page(ticketPanel), "ahelp_action=resolve")));
        w.Run(Second);
        Assert.Contains(bobTexts, t => t.Contains("Your ticket has been resolved by an admin.", StringComparison.Ordinal));
        Assert.Contains(bossTexts, t => t.Contains("resolved by", StringComparison.Ordinal));
        w.Run(5 * Second);
        Assert.True(Menu(bob).Data.Get("canAdminHelp").Truthy); // giveadminhelpverb 5 s after the resolve

        // a browse() window closed by the player is forgotten, and goes from the client's screen
        Assert.True(bob.Session.UiClose(panel.Window));
        w.Run(Second);
        Assert.Null(Window(bob, Limits.BrowseUiInterface));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    [Fact]
    public async Task With_no_admin_on_a_ticket_goes_to_the_webhook()
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add("http://127.0.0.1:7793/ahelp/");
        listener.Start();
        Task<string> received = Task.Run(async () =>
        {
            HttpListenerContext context = await listener.GetContextAsync();
            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            string body = await reader.ReadToEndAsync();
            string head = context.Request.HttpMethod + " " + context.Request.ContentType; // read before Close, which disposes the request
            context.Response.StatusCode = 204;
            context.Response.Close();
            return head + "\n" + body;
        });
        using var w = new RealHeadlessWorld(config: Config("http://127.0.0.1:7793/ahelp/"), strict: false);
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var bobTexts = new List<string>();
        bob.Client.ServerTextReceived += t => bobTexts.Add(t.Html);
        w.Run(4 * Second);

        Assert.True(bob.Session.UiAct(Limits.EscapeMenuUiWindow, "create_ticket", null));
        w.Run(Second);
        UiWindowState box = Window(bob, "Adminhelp");
        Assert.Equal(0, box.Merged.Num("adminCount"));
        Assert.True(bob.Session.UiAct(box.Window, "ahelp", UiValue.ObjectOf(("message", UiValue.Of("Nobody is here")), ("urgent", UiValue.Of(false)))));
        w.Run(Second);
        Assert.Contains(bobTexts, t => t.Contains("No active admins are online", StringComparison.Ordinal));

        string post = await received.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.StartsWith("POST application/json", post);
        Assert.Contains("\"embeds\"", post);
        Assert.Contains("Ticket #", post);
        Assert.Contains("Nobody is here", post);
        Assert.Contains("\"author\":{\"name\":\"bob", post); // DV219: the embed's author
    }
}
