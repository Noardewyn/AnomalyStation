using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
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
/// M6 slice 3b (embedding) through the real server on MetaStation with a headless client, in the corridor east of the wall at (42,155):
/// a person throws a throwing star at the player, which lodges itself in one of her limbs (the wound's sound, tg's embedded object
/// alert on her HUD); she clicks the alert, which runs tg's self-check, and the chat line of the star carries tg's rip-out link; the
/// client sends the link back (protocol version 24, D154) and after tg's rip time the star is out and in her hand, the alert gone.
/// </summary>
public class EmbeddingGameplayTests
{
    private const int Second = 20;
    private const int X = 43, Y = 155;

    private readonly ITestOutputHelper _output;

    public EmbeddingGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static turf At(int x, int y) => Builtins.locate(x, y, 1);

    private static List<string> ScreenStates(ReplicaWorld replica) => replica.Screen.Values
        .SelectMany(e => CrewOutfitTests.Icons(replica, e.Appearance)).Select(i => i.State).Where(s => s != null).Distinct().ToList();

    [MetaServerFact]
    public void A_thrown_star_embeds_and_the_self_checks_link_rips_it_out_into_the_hand()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 64, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        var texts = new List<string>();
        p.Client.ServerTextReceived += t => texts.Add(t.Html);
        int bootRuntimes = w.World.RuntimeErrorCount;
        Assert.All(Enumerable.Range(X, 4), x => Assert.True(In(w, () => At(x, Y) is turf_open && !Dm.truthy(At(x, Y).density)), $"({x},{Y}) is open floor"));

        mob_living_carbon_human bob = In(w, () =>
        {
            Waiting.NoSleep(me.forceMove(At(X, Y)));
            return Dm.@new<mob_living_carbon_human>(At(X + 3, Y));
        });
        w.Run(Second);

        // Bob throws a throwing star at Alice: it embeds (tg's chance is 100) in one of her limbs.
        obj_item_throwing_star star = In(w, () =>
        {
            var made = Dm.@new<obj_item_throwing_star>(bob.loc);
            Waiting.Detach(made.throw_at(me, 10, made.throw_speed, bob));
            return made;
        });
        w.Run(Second);
        datum_embedding embed = In(w, () => (datum_embedding)star.get_embed());
        Assert.Same(me, In(w, () => embed.owner));
        string zone = In(w, () => Dm.str(embed.owner_limb.plaintext_zone));
        _output.WriteLine($"the star embedded in Alice's {zone}; her brute {In(w, () => Dm.num(me.get_brute_loss()))}");
        _output.WriteLine("heard: " + string.Join(", ", heard.Select(h => h.File)) + "; HUD: " + string.Join(", ", ScreenStates(replica)));
        Assert.Contains(heard, h => h.File == "sound/items/weapons/bladeslice.ogg");
        var alert = In(w, () => me.alerts[ALERT_EMBEDDED_OBJECT] as atom_movable_screen_alert_embeddedobject);
        Assert.NotNull(alert);
        Assert.Contains("embeddedobject", ScreenStates(replica)); // tg's alert, its overlay state on the HUD
        Assert.Contains(replica.Screen.Values, e => e.Id == alert.dm_id);

        // She clicks the alert: tg's self-check, whose line of the star carries the rip-out link.
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, alert.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        string report = texts.LastOrDefault(t => t.Contains("embedded_object=", StringComparison.Ordinal));
        Assert.NotNull(report);
        var links = new List<ChatLink>();
        string plain = HtmlText.ToPlain(report, links);
        _output.WriteLine("the self-check: " + plain.Replace('\n', '|'));
        ChatLink rip = links.Single(l => l.Href.Contains("embedded_object=", StringComparison.Ordinal));
        Assert.Contains($"throwing star embedded in your {zone}!", plain.Substring(rip.Start, rip.Length), StringComparison.Ordinal);

        // The client sends the link back: tg's rip_out, 6 seconds for a star (rip_time 3 s * w_class 2), then the star is in her hand.
        Assert.True(p.Session.Topic(rip.Href));
        w.Run(3 * Second);
        Assert.Same(me, In(w, () => star.loc));
        w.Run(4 * Second);
        Assert.True(In(w, () => Dm.@in(star, me.held_items)), "the ripped out star is in her hand");
        Assert.Null(In(w, () => embed.owner));
        Assert.Null(In(w, () => me.alerts[ALERT_EMBEDDED_OBJECT]));
        Assert.DoesNotContain(replica.Screen.Values, e => e.Id == alert.dm_id);
        Assert.Contains(texts, t => t.Contains($"You successfully remove the throwing star from your {zone}.", StringComparison.Ordinal));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
