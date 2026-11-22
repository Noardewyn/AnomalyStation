using System.Text;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Speech;

namespace Anomaly.Server.Tests.Client.Speech;

/// <summary>D319: the chat translator's engine-free half: the tokenizer, the route, the words of a line and the translated line.</summary>
public class ChatTranslationTests
{
    [Fact]
    public void ALineSplitsIntoTheBestScoredPieces()
    {
        const char s = SentencePiece.Space;
        var model = new SentencePiece(new[] { ($"{s}hello", -1f), ($"{s}he", -2f), ("llo", -2f), ($"{s}", -3f), ("w", -4f), ("orld", -2f), ($"{s}world", -1.5f) });
        Assert.Equal(new[] { $"{s}hello", $"{s}world" }, model.Encode("hello   world"));
        // a character no piece covers is an unknown piece of its own
        Assert.Equal(new[] { $"{s}hello", $"{s}", "#", "w", "orld" }, model.Encode("hello #world"));
        Assert.Equal("hello world", SentencePiece.Decode(new[] { $"{s}hello", $"{s}wor", "ld" }));
    }

    [Fact]
    public void TheModelProtoIsRead()
    {
        // a ModelProto: an unknown piece, a control piece, two normal pieces, then a trainer spec longer than 127 bytes (a two-byte length)
        var proto = new List<byte>();
        void Piece(string text, float score, int type)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(text);
            var body = new List<byte> { 0x0A, (byte)utf8.Length };
            body.AddRange(utf8);
            body.Add(0x15);
            body.AddRange(BitConverter.GetBytes(score));
            body.AddRange(new byte[] { 0x18, (byte)type });
            proto.AddRange(new byte[] { 0x0A, (byte)body.Count });
            proto.AddRange(body);
        }
        Piece("<unk>", 0, 2);
        Piece("</s>", 0, 3);
        Piece("▁hi", -1, 1);
        Piece("▁", -5, 1);
        proto.AddRange(new byte[] { 0x12, 0x96, 0x01 }); // field 2, 150 bytes
        proto.AddRange(new byte[150]);
        Piece("▁there", -1, 1); // after the long field: read only when the skip lands right
        SentencePiece model = SentencePiece.Load(proto.ToArray());
        Assert.Equal(new[] { "▁hi", "▁there" }, model.Encode("hi there"));
        Assert.Equal(new[] { "▁", "<", "/", "s", ">" }, model.Encode("</s>")); // a control piece never splits text
    }

    [Fact]
    public void ALineTakesTheRouteToTheChosenLanguage()
    {
        Assert.Empty(ChatTranslation.Route("Привет, как дела?", "ru"));
        Assert.Equal(new[] { "en-zle" }, ChatTranslation.Route("Hello, how are you?", "ru"));
        Assert.Equal(new[] { "zle-en" }, ChatTranslation.Route("Привет, как дела?", "en"));
        Assert.Equal(new[] { "mul-en", "en-zle" }, ChatTranslation.Route("Bonjour, où est la cuisine ?", "ru"));
        Assert.Empty(ChatTranslation.Route("!!! ...", "ru"));
        // Ukrainian into Russian: through the any-language model the Russian target already has; the big model takes the target token
        Assert.Equal(new[] { "mul-en", "en-zle" }, ChatTranslation.Route("Привіт, де їжак? Що це?", "ru"));
        Assert.Equal(new[] { "mul-en", "zle-en", "en-de" }, ChatTranslation.ModelsFor("de").Select(m => m.Folder.Replace("opus-mt-tc-big-", "").Replace("opus-mt-", "")));
        Assert.Equal(">>ukr<<", ChatTranslation.TargetToken("en-zle", "uk"));
        Assert.Null(ChatTranslation.TargetToken("en-de", "de"));
        Assert.Equal(new[] { "Help!", "I'm hit.", "Where?" }, ChatTranslation.Sentences(" Help! I'm hit. Where? "));
    }

    [Fact]
    public void TheTargetsHaveTheirModels()
    {
        Assert.Contains(ChatTranslation.Targets, t => t.Code == "en");
        Assert.Contains(ChatTranslation.Targets, t => t.Code == "ru");
        foreach ((string code, _) in ChatTranslation.Targets)
            Assert.Equal(code is "en" or "ru" or "uk" ? 2 : 3, ChatTranslation.ModelsFor(code).Count);
        foreach (SpeechModel model in ChatTranslation.Models.Values)
        {
            Assert.Equal(5, model.Files.Count);
            Assert.All(model.Files, f => Assert.Matches("^([0-9a-f]{40}|[0-9a-f]{64})$", f.Sha256));
            Assert.StartsWith("https://huggingface.co/", model.BaseUrl);
        }
    }

    [Theory]
    [InlineData(ChatLog.TypeLocalChat, "Bob says, \"Привет всем\"", "Привет всем")]
    [InlineData(ChatLog.TypeRadio, "[Common] Bob asks, \"Где капитан?\"", "Где капитан?")]
    [InlineData(ChatLog.TypeOoc, "OOC: bob: hi all: ready?", "hi all: ready?")]
    [InlineData(ChatLog.TypeLocalChat, "Bob waves.", null)]
    [InlineData(ChatLog.TypeCombat, "Bob hits you, \"ow\"", null)]
    public void TheWordsOfALineAreFound(string type, string plain, string words)
    {
        bool found = ChatTranslation.TryWords(type, plain, out int start, out int length);
        Assert.Equal(words != null, found);
        if (found)
            Assert.Equal(words, plain.Substring(start, length));
    }

    [Theory]
    [InlineData("<span class='maptext'><span style='color: #fff'><span class='center '>Привет, <i>друг</i></span></span></span>", "Привет, <i>друг</i>")]
    [InlineData("<span class='maptext'><span style='color: #fff'><span class='center yell'>Help!!</span></span></span>", "Help!!")]
    [InlineData("<span class='maptext'><span style='color: #fff'><span class='center emote italics'>waves.</span></span></span>", null)]
    [InlineData("<span class='maptext'>hi</span>", null)]
    public void TheWordsOverTheHeadAreFound(string maptext, string words)
    {
        bool found = ChatTranslation.TryMaptextWords(maptext, out int start, out int length);
        Assert.Equal(words != null, found);
        if (found)
            Assert.Equal(words, maptext.Substring(start, length));
    }

    [Fact]
    public void OwnLinesAndUnchangedTranslationsAreKnown()
    {
        Assert.True(ChatTranslation.Mine("Hello there", new[] { ";hello there" }));
        Assert.False(ChatTranslation.Mine("Hello there", new[] { "bye" }));
        Assert.True(ChatTranslation.Same("Hello, there!", "hello there"));
    }

    [Fact]
    public void TheTranslationStandsOverTheOriginal()
    {
        // "Bob says, "Привет" [link]": the name bold, the words in a span, a link after them
        string plain = "Bob says, \"Привет\" (look)";
        var bold = new ChatStyle(0xFFFFFFFF, true, false);
        var spans = new[] { new ChatSpan(0, 3, bold), new ChatSpan(11, 6, ChatStyles.Base with { Italic = true }) };
        var links = new[] { new ChatLink(20, 4, "?look") };
        var (text, styled, linked) = ChatTranslation.Compose(plain, spans, links, 11, 6, "Hello there");
        Assert.Equal("Bob says, \"Hello there\" (look)\n" + plain, text);
        Assert.Contains(new ChatSpan(0, 3, bold), styled);
        Assert.Contains(new ChatSpan(11, 11, ChatStyles.Base with { Italic = true }), styled);
        Assert.Equal(new[] { new ChatLink(25, 4, "?look") }, linked);
        // the original: every character in a span at the smaller size, none over another
        int origin = text.IndexOf('\n') + 1;
        List<ChatSpan> under = styled.Where(sp => sp.Start >= origin).OrderBy(sp => sp.Start).ToList();
        Assert.All(under, sp => Assert.Equal(ChatTranslation.OriginalScale, sp.Style.Scale));
        Assert.Equal(0xFFFFFF8Cu, under[0].Style.Rgba); // the bold name, faded to 55 %
        Assert.All(under, sp => Assert.True((sp.Style.Rgba & 0xFF) < 0xFF));
        Assert.Equal(plain.Length, under.Sum(sp => sp.Length));
        Assert.Equal(Enumerable.Range(0, under.Count - 1).Select(i => under[i].Start + under[i].Length), under.Skip(1).Select(sp => sp.Start));
    }

    [Fact]
    public void TheTranslationOptionsAreKept()
    {
        var options = new PlayOptions { ChatTranslation = true, ChatTranslationLanguage = "ru" };
        var errors = new List<string>();
        PlayOptions read = PlayOptions.Parse(options.ToText(), errors);
        Assert.Empty(errors);
        Assert.True(read.ChatTranslation);
        Assert.Equal("ru", read.ChatTranslationLanguage);
        PlayOptions.Parse("chat_translation_language=xx\n", errors);
        Assert.Single(errors);
    }

    [Fact]
    public void ATranslatedLineIsRedrawnInTheLogAndTheFeed()
    {
        var log = new ChatLog();
        ChatEntry entry = log.Add(ChatLog.TypeLocalChat, "<span class='say'>Bob says, \"Привет\"</span>", 0);
        log.TakeChanges();
        log.Retext(entry, "Bob says, \"Hello\"\nBob says, \"Привет\"", null, null);
        Assert.Equal(0, log.TakeChanges());
        Assert.Null(log.Add(ChatLog.TypeLocalChat, "<span class='say'>Bob says, \"Привет\"</span>", 1)); // a repeat counts on the line
        var feed = new ChatFeed();
        ChatFeed.Line line = feed.Add("Bob says, \"Привет\"", 0);
        feed.Retext(line, "Bob says, \"Hello\"", null, null);
        Assert.Equal("Bob says, \"Hello\"", feed.Visible(1).Single().Text);
    }
}
