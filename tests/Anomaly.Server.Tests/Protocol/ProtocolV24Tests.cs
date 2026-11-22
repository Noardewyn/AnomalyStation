using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Server;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 24 (D154, DV59, docs/ai/specs/protocol.md section 33): the Topic action (kind 14), a click on a link of tg's chat. Bytes,
/// round trips, refusals; the host keeping the links it sent to a session and taking a Topic only for one of them (a forged or forgotten
/// one is dropped, never a strike); the command log's round trip; the client's plain text with its link spans; a version-23 client refused.
/// </summary>
public class ProtocolV24Tests
{
    private const string Href = "byond://?src=[0x2000001];embedded_object=[0x2000002];embedded_limb=[0x2000003]";

    private static ActionMessage Topic(string href, uint seq = 0) => new(seq, ActionKind.Topic, 0, ClickButton.None, ClickModifiers.None, href);

    [Fact]
    public void TheVersionIsTwentyFourAndTopicIsKindFourteen()
    {
        Assert.True(ProtocolVersion.Current >= 24);
        Assert.Equal(14, (int)ActionKind.Topic);
        Assert.True(ActionKinds.Last >= ActionKind.Topic); // version 34's mouse kinds follow it
        Assert.Equal(Limits.MaxDevCommandBytes, Limits.MaxTopicHrefBytes); // the action's cap holds it unchanged
    }

    [Fact]
    public void ATopicIsItsHrefAndRoundTrips()
    {
        byte[] bytes = Codec.Encode(Topic("byond://?src=[0x1]", seq: 3));
        // [id] [seq 3] [kind 14] [str of 19 bytes]
        Assert.Equal(new byte[] { (byte)MessageId.Action, 3, 14, 19 }.Concat("byond://?src=[0x1]"u8.ToArray()).ToArray(), bytes);
        Assert.True(Codec.TryDecode(bytes, out ActionMessage back, out _));
        Assert.Equal(Topic("byond://?src=[0x1]", seq: 3), back);
        Assert.False(back.IsTargeted);

        Assert.True(Codec.TryDecode(Codec.Encode(Topic(Href)), out ActionMessage back2, out _));
        Assert.Equal(Href, back2.Text);
    }

    [Fact]
    public void ABadTopicIsRefused()
    {
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(Topic(null)));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(Topic("")));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(Topic("byond://?" + new string('a', Limits.MaxTopicHrefBytes))));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(Topic(Href) with { Target = 5, TargetKind = TargetKind.Entity }));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(new ActionMessage(1, ActionKind.Click, 7, ClickButton.Left, ClickModifiers.None, Href) { TargetKind = TargetKind.Entity }));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(new ActionMessage(1, (ActionKind)15, 0, ClickButton.None, ClickModifiers.None, null)));

        byte[] bytes = Codec.Encode(Topic(Href));
        Assert.False(Codec.TryDecode(bytes.AsSpan(0, bytes.Length - 1), out ActionMessage _, out _));       // a cut href
        Assert.False(Codec.TryDecode(bytes.Concat(new byte[] { 0 }).ToArray(), out ActionMessage _, out _));   // a byte after it
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.Action, 1, 14, 0 }, out ActionMessage _, out _)); // an empty href
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.Action, 1, 15 }, out ActionMessage _, out _));    // no kind 15
    }

    [Fact]
    public void TheHostKeepsTheBYONDLinksOfAChatLine()
    {
        string html = "<span class='notice'><a href='" + Href + "'>There is a star</a> and <a href=\"byond://?src=[0x9];x=1\">x</a>"
            + " <a href='https://example.com'>web</a> <a href='byond://?" + new string('b', Limits.MaxTopicHrefBytes) + "'>long</a></span>";
        Assert.Equal(new[] { Href, "byond://?src=[0x9];x=1" }, ChatLinks.Hrefs(html).ToArray());

        var links = new ChatLinks();
        for (int i = 0; i < ChatLinks.Capacity + 3; i++)
            links.Record($"<a href='byond://?src=[0x{i:x}]'>{i}</a>");
        Assert.Equal(ChatLinks.Capacity, links.Count);
        Assert.False(links.Contains("byond://?src=[0x0]"), "the oldest link is forgotten first");
        Assert.True(links.Contains($"byond://?src=[0x{ChatLinks.Capacity + 2:x}]"));
        links.Record($"<a href='byond://?src=[0x{ChatLinks.Capacity + 2:x}]'>again</a>");
        Assert.Equal(ChatLinks.Capacity, links.Count);
    }

    [Fact]
    public void TheClientsPlainTextKeepsTheLinkSpans()
    {
        var spans = new List<ChatLink>();
        string plain = HtmlText.ToPlain("<span class='boldwarning'>\t<a href='" + Href + "'>There is  a throwing star embedded in your right leg!</a></span><br>"
            + "<a href='https://example.com'>web</a> and <a href='byond://?src=[0x5];gauze_limb=[0x6]'>a gauze</a>", spans);
        Assert.Equal("There is  a throwing star embedded in your right leg!\nweb and a gauze", plain);
        Assert.Equal(2, spans.Count);
        Assert.Equal(new ChatLink(0, "There is  a throwing star embedded in your right leg!".Length, Href), spans[0]);
        Assert.Equal("a gauze", plain.Substring(spans[1].Start, spans[1].Length));
        Assert.Equal("byond://?src=[0x5];gauze_limb=[0x6]", spans[1].Href);
        Assert.Equal(plain, HtmlText.ToPlain("<span class='boldwarning'>\t<a href='" + Href + "'>There is  a throwing star embedded in your right leg!</a></span><br>"
            + "<a href='https://example.com'>web</a> and <a href='byond://?src=[0x5];gauze_limb=[0x6]'>a gauze</a>"));

        var feed = new ChatFeed();
        feed.Add(new string('x', 300) + "link", 0, new List<ChatLink> { new(300, 4, Href) });
        ChatLine line = feed.Visible(0).Single();
        Assert.Equal(304, line.Text.Length); // a line keeps its text up to its last link
        Assert.Equal(Href, line.Links.Single().Href);
    }

    // Alice at (4, 4).
    private static (TestServer Server, FakeRemote Alice, mob AliceMob) One()
    {
        TestServer server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        server.Host.EnqueueConsoleLine("teleport Alice 4 4");
        alice.Motion(0, 0);
        server.Tick(2);
        return (server, alice, server.MobOf("Alice"));
    }

    [Fact]
    public void TheHostTakesOnlyALinkItSentToThatPlayerAndLogsIt()
    {
        var (server, alice, aliceMob) = One();
        using (server)
        {
            string href;
            using (server.World.Enter())
            {
                href = "byond://?src=" + Dm.str(GlobalProcs.REF(aliceMob)) + ";run_examinate=" + Dm.str(GlobalProcs.REF(aliceMob));
                GlobalProcs.to_chat(aliceMob, "<a href='" + href + "'>Look at yourself</a>");
            }
            server.Tick(2);
            Session s = server.Host.SessionOf(1);
            Assert.True(s.ChatLinks.Contains(href), "the session keeps the link it was sent");

            alice.ActionV4(Topic(href));
            alice.ActionV4(Topic(Href)); // never sent: forged
            server.Tick(2);
            var topic = Assert.Single(server.Clients.Topics);
            Assert.Equal(href, topic.Href);
            Assert.Same(aliceMob, topic.Usr);
            Assert.Equal((1, 1, 0), (s.TopicsApplied, s.TopicsRefused, s.Strikes));

            QueuedCommand logged = server.Host.Log.Commands.Single(c => c.Kind == CommandKind.Action && c.Action == ActionKind.Topic);
            Assert.Equal(href, logged.Text);
            CommandLog parsed = CommandLog.Parse(server.Host.Log.ToText(), out string error);
            Assert.Null(error);
            Assert.Equal(server.Host.Log.Commands.Where(c => c.Action == ActionKind.Topic), parsed.Commands.Where(c => c.Action == ActionKind.Topic));
        }
    }

    [Fact]
    public void A_client_of_version_23_is_refused()
    {
        using var server = TestServer.Start();
        FakeRemote remote = server.Connect("Alice", protocol: 23);
        server.Tick();
        Assert.Equal(DisconnectReason.WrongVersion, remote.Rejected?.Reason);
    }
}
