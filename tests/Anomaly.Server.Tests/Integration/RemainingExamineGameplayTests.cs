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
/// The remaining object examines (D291, package X3) through the real server on MetaStation. Bob shift-clicks what is next to him
/// and reads tg's lines in his chat: a jumpsuit's sensors and adjusting hint, a helmet's armour tag and the protection classes its
/// link prints, a microwave's status display and a severed head's missing organs.
/// </summary>
public class RemainingExamineGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static string Lines(IEnumerable<ServerText> lines) => string.Join(" | ", lines.Select(t => t.Html));

    [MetaServerFact]
    public void Bob_shift_clicks_a_jumpsuit_a_helmet_a_microwave_and_a_head_and_reads_tgs_lines()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 65, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var him = (mob_living_carbon_human)w.MobOf(bob);
        var chat = new List<ServerText>();
        bob.Client.ServerTextReceived += chat.Add;
        w.Run(2 * Second);
        (int x, int y, int z) = In(w, () => ((int)him.x, (int)him.y, (int)him.z));

        atom suit, helmet, microwave, head;
        using (w.World.Enter())
        {
            suit = Dm.@new<obj_item_clothing_under_color_grey>(Builtins.locate(x + 1, y, z));
            helmet = Dm.@new<obj_item_clothing_head_helmet>(Builtins.locate(x - 1, y, z));
            microwave = Dm.@new<obj_machinery_microwave>(Builtins.locate(x, y + 1, z));
            head = Dm.@new<obj_item_bodypart_head>(Builtins.locate(x, y - 1, z));
            ((obj_item_clothing_under)suit).set_sensor_mode(SENSOR_VITALS);
        }
        w.Run(Second);

        string Examine(atom what)
        {
            chat.Clear();
            Assert.True(bob.Session.Click(what.dm_id, ClickButton.Left, ClickModifiers.Shift));
            w.Run(Second);
            ServerText box = chat.LastOrDefault(t => t.Html.Contains("boxed_message", StringComparison.Ordinal));
            Assert.True(box.Html != null, "no examine box: " + Lines(chat) + "\n" + w.Err);
            output.WriteLine(In(w, () => Dm.str(what.name)) + ": " + box.Html);
            return box.Html;
        }

        string text = Examine(suit);
        Assert.Contains("to wear it casually.", text, StringComparison.Ordinal);
        Assert.Contains("Its vital tracker appears to be enabled.", text, StringComparison.Ordinal);

        text = Examine(microwave);
        Assert.Contains("to start cooking cycle.", text, StringComparison.Ordinal);
        Assert.Contains("is empty.", text, StringComparison.Ordinal);
        Assert.Contains("- Power: <b>2500W</b>.", text, StringComparison.Ordinal);

        text = Examine(head);
        Assert.Contains("The brain has been removed from", text, StringComparison.Ordinal);
        Assert.Contains("eyes have been removed.", text, StringComparison.Ordinal);

        // The helmet: the tag link, then the protection classes it prints.
        text = Examine(helmet);
        Assert.Contains("listing its protection classes.", text, StringComparison.Ordinal);
        var links = new List<ChatLink>();
        HtmlText.ToPlain(text, links);
        ChatLink tag = links.Single(l => l.Href.Contains("list_armor=1", StringComparison.Ordinal));
        chat.Clear();
        Assert.True(bob.Session.Topic(tag.Href.Replace("&amp;", "&", StringComparison.Ordinal)));
        w.Run(Second);
        output.WriteLine("protection classes: " + Lines(chat));
        Assert.Contains(chat, t => t.Html.Contains("PROTECTION CLASSES", StringComparison.Ordinal) && t.Html.Contains("MELEE", StringComparison.Ordinal));
    }
}
