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
/// Syndicate package A2 (D316) through the real server: an admin makes Bob, who holds an assistant's PDA, a traitor from the traitor
/// panel (tg's Show Traitor Panel, a browse() page); Bob's AntagInfoTraitor window shows his code words and the uplink's code; he uses
/// the PDA in hand, runs the messenger, types the code as his ringtone (tg's text input), the Uplink window opens with 20 TC, he buys
/// C-4 and holds it; at the round's end the report lists the purchase and the code words.
/// </summary>
public sealed class TraitorGameplayTests : IDisposable
{
    private const int Second = 20;
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "anomaly-traitor-" + Guid.NewGuid().ToString("N"));

    public TraitorGameplayTests()
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
        Seed = 11, RoomWidth = 24, RoomHeight = 24, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f,
        DevCommands = false, ConfigFile = Path.Combine(_folder, "server.txt"),
    };

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static UiWindowState Window(RealHeadlessWorld.Player p, string @interface) => p.Session.Replica.UiWindows.Values.SingleOrDefault(v => v.Interface == @interface);

    private static UiWindowState Needed(RealHeadlessWorld w, RealHeadlessWorld.Player p, string @interface)
    {
        UiWindowState window = Window(p, @interface);
        Assert.True(window != null, "no " + @interface + ": " + string.Join(", ", p.Session.Replica.UiWindows.Values.Select(v => v.Interface)) + "\n" + w.Err);
        return window;
    }

    private static string Html(UiWindowState window) => string.Concat(window.Merged.Get("html").Items.Select(i => i.Text));

    private static string Href(string html, string part)
    {
        Match m = Regex.Match(html, "href='(byond://[^']*" + Regex.Escape(part) + "[^']*)'");
        Assert.True(m.Success, "no link with " + part);
        return m.Groups[1].Value.Replace("&amp;", "&", StringComparison.Ordinal);
    }

    [Fact]
    public void An_admin_makes_Bob_a_traitor_who_unlocks_his_pda_uplink_and_buys_c4()
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

        // Bob takes his PDA in hand (the uplink goes to the first PDA he carries: this one alone). The room has no emergency shuttle
        // (tg always has one): his escape counts by tg's forced escape, so an escape objective's check never looks for the shuttle.
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var pda = In(w, () =>
        {
            foreach (obj_item_modular_computer_pda other in Dm.each<obj_item_modular_computer_pda>(bobMob.get_all_contents()).ToList())
                GlobalProcs.qdel(other);
            var made = Dm.@new<obj_item_modular_computer_pda_crew_assistant>(bobMob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(bobMob.put_in_hands(made))));
            bobMob.mind.force_escaped = 1;
            return made;
        });
        w.Run(Second);

        // the console's traitor-panel, the Traitor category's link: Bob is a traitor with his greeting and AntagInfoTraitor
        boss.Session.DevCommand("traitor-panel Bob");
        w.Run(Second);
        UiWindowState panel = boss.Session.Replica.UiWindows.Values.Single(v => v.Interface == Limits.BrowseUiInterface && Html(v).Contains("Mind currently owned by key: Bob", StringComparison.Ordinal));
        Assert.True(boss.Session.Topic(Href(Html(panel), "add_antag=/datum/antagonist/traitor")));
        w.Run(2 * Second);
        datum_antagonist_traitor traitor = In(w, () => bobMob.mind.has_antag_datum(typeof(datum_antagonist_traitor)) as datum_antagonist_traitor);
        Assert.NotNull(traitor);
        UiWindowState info = Needed(w, bob, "AntagInfoTraitor");
        string code = info.Merged.Str("code");
        Assert.Matches(new Regex(@"^\d{3} [\w-]+$"), code);
        Assert.Equal(In(w, () => Dm.jointext(GlobalProcs.SStraitor.syndicate_code_phrase, ", ")), info.Merged.Str("phrases"));
        Assert.True(info.Merged.Str("responses").Length > 0);
        var context = new TguiContext(info.Window, info.Interface, info.Title, 2, info.Merged, (_, _) => { }, new TguiLocalState());
        List<UiNode> drawn = Anomaly.Client.Shared.Tgui.Interfaces.AntagInfoTraitor.Render(context).Walk().ToList();
        Assert.Contains(drawn, n => n.Text == "Code: " + code);
        Assert.Contains(drawn, n => n.Text == info.Merged.Str("phrases") && n.Color == "blue");

        // Bob uses the PDA in hand, runs the messenger and sets his ringtone to the code (tg's text input)
        bob.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        UiWindowState ntos = bob.Session.Replica.UiWindows.Values.First(v => v.Interface.StartsWith("Ntos", StringComparison.Ordinal));
        Assert.True(bob.Session.UiAct(ntos.Window, "PC_runprogram", UiValue.ObjectOf(("name", UiValue.Of("nt_messenger")))));
        w.Run(Second);
        UiWindowState messenger = Needed(w, bob, "NtosMessenger");
        Assert.True(bob.Session.UiAct(messenger.Window, "PDA_ringSet"));
        w.Run(Second);
        UiWindowState input = Needed(w, bob, "TextInputModal");
        Assert.True(bob.Session.UiAct(input.Window, "submit", UiValue.ObjectOf(("entry", UiValue.Of(code)))));
        w.Run(2 * Second);
        Assert.Contains(bobTexts, t => t.Contains("The computer softly beeps.", StringComparison.Ordinal));

        // the Uplink window: 20 TC; C-4 bought lands in Bob's hand
        UiWindowState uplink = Needed(w, bob, "Uplink");
        Assert.Equal(20, uplink.Merged.Num("telecrystals"));
        Assert.Contains(uplink.Merged.Get("uplink_json").Arr("items"), i => i.Str("id") == "/datum/uplink_item/explosives/c4");
        Assert.True(bob.Session.UiAct(uplink.Window, "buy", UiValue.ObjectOf(("path", UiValue.Of("/datum/uplink_item/explosives/c4")))));
        w.Run(2 * Second);
        Assert.Equal(19, Needed(w, bob, "Uplink").Merged.Num("telecrystals"));
        Assert.True(In(w, () => Dm.each<object>(bobMob.held_items).Any(o => o is obj_item_grenade_c4)), "Bob does not hold the C-4");
        Assert.Contains(bobTexts, t => t.Contains("materializes into your hands!", StringComparison.Ordinal));

        // the round's end: the report lists the TC used and the purchase, and the code words
        using (w.World.Enter())
            Waiting.Detach(GlobalProcs.SSticker.declare_completion());
        w.Run(Second);
        string report = Assert.Single(bossTexts, t => t.Contains("boxed_message", StringComparison.Ordinal));
        Assert.Contains("The traitors were:", report);
        Assert.Contains("(used 1 TC)", report);
        Assert.Contains("Composition C-4", report);
        Assert.Contains("The code phrases were:", report);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
