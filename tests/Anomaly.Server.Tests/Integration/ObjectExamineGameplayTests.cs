using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The object examines (D291, package X2) through the real server on MetaStation. Bob shift-clicks what is next to him and reads
/// tg's lines in his chat: a grille's fastenings, a window's screws and its damage, the plating under him, a rack, a stun baton's
/// "See combat information." link with the lore a second look prints, and the readout the link prints when he follows it.
/// </summary>
public class ObjectExamineGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static string Lines(IEnumerable<ServerText> lines) => string.Join(" | ", lines.Select(t => t.Html));

    [MetaServerFact]
    public void Bob_shift_clicks_a_grille_a_window_a_rack_the_plating_and_a_stun_baton_and_reads_tgs_lines()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 64, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var him = (mob_living_carbon_human)w.MobOf(bob);
        var chat = new List<ServerText>();
        bob.Client.ServerTextReceived += chat.Add;
        w.Run(2 * Second);
        (int x, int y, int z) = In(w, () => ((int)him.x, (int)him.y, (int)him.z));

        // Things on the floor around him (the arrivals hall is open floor; the east tile is its own).
        atom grille, window, rack, baton;
        turf floor;
        using (w.World.Enter())
        {
            grille = Dm.@new<obj_structure_grille>(Builtins.locate(x + 1, y, z));
            window = Dm.@new<obj_structure_window_reinforced>(Builtins.locate(x - 1, y, z));
            rack = Dm.@new<obj_structure_rack>(Builtins.locate(x, y + 1, z));
            baton = Dm.@new<obj_item_melee_baton_security_loaded>(Builtins.locate(x, y - 1, z));
            floor = (turf)Waiting.NoSleep(Builtins.locate(x, y, z).ChangeTurf(typeof(turf_open_floor_plating)));
            window.atom_integrity = ((obj_structure_window_reinforced)window).max_integrity * 0.4f;
        }
        w.Run(Second);

        string Examine(atom what, int wait = Second)
        {
            chat.Clear();
            Assert.True(what is turf t
                ? bob.Session.Click(new ClickTarget(TargetKind.Turf, 0, (int)t.x, (int)t.y, (int)t.z), ClickButton.Left, ClickModifiers.Shift)
                : bob.Session.Click(what.dm_id, ClickButton.Left, ClickModifiers.Shift));
            w.Run(wait);
            ServerText box = chat.LastOrDefault(t => t.Html.Contains("boxed_message", StringComparison.Ordinal));
            Assert.True(box.Html != null, "no examine box: " + Lines(chat) + "\n" + w.Err);
            output.WriteLine(In(w, () => Dm.str(what.name)) + ": " + box.Html);
            return box.Html;
        }

        string text = Examine(grille);
        Assert.Contains("secured in place with", text, StringComparison.Ordinal);
        Assert.Contains("screws", text, StringComparison.Ordinal);
        text = Examine(window);
        Assert.Contains("you'd need to", text, StringComparison.Ordinal);
        Assert.Contains("heat them", text, StringComparison.Ordinal);
        Assert.Contains("It appears heavily damaged.", text, StringComparison.Ordinal);
        text = Examine(rack);
        Assert.Contains("held together by a couple of", text, StringComparison.Ordinal);
        text = Examine(floor);
        Assert.Contains("attachment holes for a new", text, StringComparison.Ordinal);

        // The baton: the link, then the readout the link prints.
        text = Examine(baton);
        Assert.Contains("See combat information.", text, StringComparison.Ordinal);
        Assert.Contains("look closer", text, StringComparison.Ordinal);
        var links = new List<ChatLink>();
        HtmlText.ToPlain(text, links);
        ChatLink combat = links.Single(l => l.Href.Contains("examine=1", StringComparison.Ordinal));
        chat.Clear();
        Assert.True(bob.Session.Topic(combat.Href.Replace("&amp;", "&", StringComparison.Ordinal)));
        w.Run(Second);
        output.WriteLine("combat information: " + Lines(chat));
        Assert.Contains(chat, t => t.Html.Contains("melee hit", StringComparison.Ordinal) && t.Html.Contains("to stun an enemy", StringComparison.Ordinal));

        // A second look within a second is tg's closer look: the baton's lore.
        Examine(baton, 4); // tg's client/Click drops a click within a decisecond of the last
        chat.Clear();
        Assert.True(bob.Session.Click(baton.dm_id, ClickButton.Left, ClickModifiers.Shift));
        w.Run(Second);
        output.WriteLine("closer: " + Lines(chat));
        Assert.Contains(chat, t => t.Html.Contains("an unholy union of mace and cattleprod", StringComparison.Ordinal));
    }
}
