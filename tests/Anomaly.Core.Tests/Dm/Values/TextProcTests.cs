using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tests.DmValues;

/// <summary>
/// The text builtins and their <c>_char</c> variants (docs/ai/translation-rules.md 5.5, 5.6; DV4): BYOND ref
/// <c>/proc/copytext</c>, <c>findtext</c>, <c>findlasttext</c>, <c>replacetext</c>, <c>splicetext</c>, <c>spantext</c>,
/// <c>nonspantext</c>, <c>splittext</c>, <c>jointext</c>, <c>sorttext</c>, <c>cmptext</c>, <c>ckey</c>, <c>ckeyEx</c>,
/// <c>uppertext</c>, <c>trimtext</c>, <c>ascii2text</c>, <c>text2ascii</c>, <c>text2num</c>, <c>length</c> with their
/// examples; OpenDream's BYOND-checked tests (Text/findtext, findlasttext, Splittext; Builtins/Copytext, Replacetext,
/// splicetext, splicetext_runtime, Spantext, Nonspantext, ckey, cmptext, text2num, trimtext, text2ascii2text).
/// </summary>
public class TextProcTests
{
    private const string Emoji = "\U0001F600"; // one character, two UTF-16 code units

    [Fact]
    public void Length()
    {
        // BYOND ref /proc/length: length("Hi") is 2, length(list(1,2,3)) is 3; DV4: UTF-16 code units.
        Assert.Equal(2f, Dm.length("Hi"));
        Assert.Equal(3f, Dm.length(Dm.list(1f, 2f, 3f)));
        Assert.Equal(0f, Dm.length(null));
        Assert.Equal(0f, Dm.length(5f));
        Assert.Equal(0f, Dm.length(new PlainDatum()));
        Assert.Equal(2f, Dm.length(Emoji));
        Assert.Equal(1f, Dm.length_char(Emoji));
        Assert.Equal(6f, Dm.length("привет"));         // Cyrillic: one code unit per letter (BYOND: 12 bytes, DV4)
        Assert.Equal(6f, Dm.length_char("привет"));
        Assert.Equal(3f, Dm.length_char(Dm.list(1f, 2f, 3f)));
    }

    [Fact]
    public void Copytext()
    {
        // BYOND ref /proc/copytext examples; OpenDream test Builtins/Copytext and NativeProc_copytext.
        Assert.Equal("Hi", Dm.copytext("Hi there", 1, 3));
        Assert.Equal("there", Dm.copytext("Hi there", 4));
        Assert.Equal("there", Dm.copytext("Hi there", -5));
        Assert.Equal("a lubba dub dub", Dm.copytext("wubba lubba dub dub}", 5, -1));
        Assert.Equal("}", Dm.copytext_char("wubba lubba dub dub}", -1));
        Assert.Equal("", Dm.copytext("abc", 0));
        Assert.Equal("", Dm.copytext("abc", 3, 2));
        Assert.Equal("", Dm.copytext("abc", 5));
        Assert.Equal("abc", Dm.copytext("abc", 1, 10));
        Assert.Null(Dm.copytext(null));
        Assert.Equal("", Dm.copytext(null, 1, 3));
        Assert.Equal("ab", Dm.copytext_char(Emoji + "ab", 2));
        Assert.Equal("\uDE00ab", Dm.copytext(Emoji + "ab", 2));   // a UTF-16 position splits the pair (DV4)
        Assert.Equal(Emoji, Dm.copytext_char("x" + Emoji + "y", 2, 3));
    }

    [Fact]
    public void Findtext()
    {
        // OpenDream test Text/findtext (every assertion); BYOND ref example findtext("Hi There", "there").
        Assert.Equal(4f, Dm.findtext("Hi There", "there"));
        Assert.Equal(1f, Dm.findtext("/mob", "/"));
        Assert.Equal(6f, Dm.findtext("abcdefg", "f", 6));
        Assert.Equal(0f, Dm.findtext("abcdefg", "f", 7));
        Assert.Equal(3f, Dm.findtext("Banana", "na"));
        Assert.Equal(3f, Dm.findtext("Banana", "NA"));
        Assert.Equal(6f, Dm.findtext("abcdefg", "f", 1, -1));
        Assert.Equal(0f, Dm.findtext("abcdefg", "g", 1, -1));
        Assert.Equal(0f, Dm.findtext("pipe gun", " (Wielded)", -10));
        Assert.Equal(1f, Dm.findtext("pipe gun", "pipe", -10));
        Assert.Equal(5f, Dm.findtext("Banana", "na", -3));
        Assert.Equal(3f, Dm.findtextEx("Banana", "na"));
        Assert.Equal(0f, Dm.findtextEx("Banana", "NA"));
        Assert.Equal(6f, Dm.findtextEx("abcdefg", "f", 6));
        Assert.Equal(0f, Dm.findtextEx("abcdefg", "f", 7));
        Assert.Equal(16f, Dm.findtextEx("pipe gun (Wielded)", "ed", -100));
        Assert.Equal(16f, Dm.findtextEx("pipe gun (Wielded)", "ed", -100, -1));
        Assert.Equal(0f, Dm.findtextEx("pipe gun (Wielded)", "ed", -100, -2));
        Assert.Equal(0f, Dm.findtext("abc", ""));
        Assert.Equal(0f, Dm.findtext(null, "a"));
        Assert.Equal(3f, Dm.findtext_char("a" + Emoji + "b", "b"));
        Assert.Equal(4f, Dm.findtext("a" + Emoji + "b", "b"));
        Assert.Equal(2f, Dm.findtext("ПРИВЕТ", "рив"));     // case-insensitive beyond ASCII
        V.Error(() => Dm.findtext("abc", new PlainDatum()));  // a needle that is neither text nor a /regex
    }

    [Fact]
    public void Findlasttext()
    {
        // OpenDream test Text/findlasttext (every assertion); BYOND ref: findlasttext("Banana", "na", -3) "will skip
        // over the last "na"".
        Assert.Equal(1f, Dm.findlasttext("/mob", "/", 0, 1));
        Assert.Equal(1f, Dm.findlasttext("/mob", "/"));
        Assert.Equal(0f, Dm.findlasttext("abcdefg", "f", 0, -1));
        Assert.Equal(6f, Dm.findlasttext("abcdefg", "f", 0, -2));
        Assert.Equal(6f, Dm.findlasttext("abcdefg", "f", 6));
        Assert.Equal(0f, Dm.findlasttext("abcdefg", "f", 5));
        Assert.Equal(2f, Dm.findlasttext("abcdefg", "bc", 2));
        Assert.Equal(1f, Dm.findlasttext("abcdefg", "ab", 10));
        Assert.Equal(3f, Dm.findlasttext("Banana", "na", -3));
        Assert.Equal(0f, Dm.findlasttext("Banana", "na", -5));
        Assert.Equal(5f, Dm.findlasttext("Banana", "na", 0));
        Assert.Equal(3f, Dm.findlasttext("Banana", "na", 3));
        Assert.Equal(0f, Dm.findlasttext("Banana", "na", 2));
        Assert.Equal(5f, Dm.findlasttext("Banana", "na", 5));
        Assert.Equal(5f, Dm.findlasttext("Banana", "na", 50));
        Assert.Equal(0f, Dm.findlasttext("Banana", "na", -50));
        Assert.Equal(6f, Dm.findlasttext("abcdefg", "f", 6, -50));
        Assert.Equal(0f, Dm.findlasttext("abcdefg", "f", 6, 50));
        Assert.Equal(5f, Dm.findlasttext("BANANA", "na"));
        Assert.Equal(3f, Dm.findlasttext_char(Emoji + "ab", "b"));
    }

    [Fact]
    public void Replacetext()
    {
        // OpenDream test Builtins/Replacetext (its text-needle assertions) and BYOND ref replacetext: "If the Needle is a
        // lowercase word, the replacement will be changed to uppercase or all caps if the found text is uppercase or
        // all caps" (replacetext("One on one", "one", "two") outputs "Two on two").
        const string str = "ABCDEF";
        Assert.Equal("GBCDEF", Dm.replacetext(str, "A", "G"));
        Assert.Equal("GBCDEF", Dm.replacetext(str, "A", "G", 1));
        Assert.Equal("ABCDEF", Dm.replacetext(str, "A", "G", 2));
        Assert.Equal("ABCDEF", Dm.replacetext(str, "A", "G", -5));
        Assert.Equal("GBCDEF", Dm.replacetext(str, "A", "G", -6));
        Assert.Equal("GBCDEF", Dm.replacetext(str, "A", "G", -100));
        Assert.Equal("ABCDEG", Dm.replacetext(str, "F", "G"));
        Assert.Equal("ABCDEG", Dm.replacetext(str, "F", "G", 1, 7));
        Assert.Equal("ABCDEF", Dm.replacetext(str, "F", "G", 1, 6));
        Assert.Equal("AGBGCGDGEGF", Dm.replacetext(str, null, "G"));
        Assert.Equal("AHIBHICHIDHIEHIF", Dm.replacetext(str, null, "HI"));
        Assert.Equal("AGBGCGDGEGF", Dm.replacetext(str, null, "G", 1));
        Assert.Equal("AGBGCGDGEGF", Dm.replacetext(str, null, "G", 2));
        Assert.Equal("ABGCGDGEGF", Dm.replacetext(str, null, "G", 3));
        Assert.Equal("ABCGDGEGF", Dm.replacetext(str, null, "G", 4));
        Assert.Equal("AGBGCGDEF", Dm.replacetext(str, null, "G", 1, 4));
        Assert.Equal("AGBGCGDGEF", Dm.replacetext(str, null, "G", 1, 5));
        Assert.Equal("AGBGCGDGEGF", Dm.replacetext(str, null, "G", 1, 6));
        Assert.Equal("AGBGCGDGEGF", Dm.replacetext(str, null, "G", 1, 7));
        Assert.Equal("ABCDGF", Dm.replacetext(str, "E", "G", 1, 0));
        Assert.Equal("ABCDGF", Dm.replacetext(str, "E", "G", 1, -1));
        Assert.Equal("ABCDEF", Dm.replacetext(str, "E", "G", 1, -2));
        Assert.Equal("ABCDEF", Dm.replacetext(str, "E", "G", 1, -200));
        Assert.Equal("Two on two", Dm.replacetext("One on one", "one", "two"));
        Assert.Equal("TWO on two", Dm.replacetext("ONE on one", "one", "two"));
        Assert.Equal("two on one", Dm.replacetextEx("One on one", "One", "two"));
        Assert.Equal("One on two", Dm.replacetextEx("One on one", "one", "two"));
        Assert.Equal("a-b-c", Dm.replacetext("a b c", " ", "-"));
        Assert.Equal("x5y", Dm.replacetext("xzy", "z", 5));   // a number replacement renders as text
        Assert.Null(Dm.replacetext(null, "a", "b"));
        Assert.Equal("a" + Emoji + "z", Dm.replacetext_char("a" + Emoji + "b", "b", "z", 3));
    }

    [Fact]
    public void Splicetext()
    {
        // BYOND ref splicetext example ("balaclava"); OpenDream tests splicetext and splicetext_runtime.
        Assert.Equal("balaclava", Dm.splicetext("banana", 3, 6, "laclav"));
        Assert.Equal("laclava", Dm.splicetext("banana", 1, 6, "laclav"));
        Assert.Equal("balaclav", Dm.splicetext("banana", 3, 0, "laclav"));
        Assert.Equal("ab", Dm.splicetext("abcdef", 3));
        Assert.Equal("abef", Dm.splicetext("abcdef", 3, 5));
        Assert.Equal("abef", Dm.splicetext("abcdef", 3, 5, null));
        Assert.Equal("abcdtestef", Dm.splicetext("abcdef", -2, 5, "test"));
        Assert.Equal("abcdtest", Dm.splicetext("abcdef", -2, 7, "test"));
        Assert.Equal("abtestf", Dm.splicetext("abcdef", 3, -1, "test"));
        Assert.Equal("atest", Dm.splicetext("abcdef", 2, 33, "test"));
        Assert.Equal("atestf", Dm.splicetext("abcdef", 2, -1, "test"));
        Assert.Null(Dm.splicetext(null, 4, 5));
        Assert.Equal("", Dm.splicetext("", 4, 5));
        Assert.Equal("test", Dm.splicetext(null, 4, 5, "test"));
        Assert.Equal("test", Dm.splicetext("", 4, 5, "test"));
        V.Error(() => Dm.splicetext("banana", 12, -1, "test"));
        V.Error(() => Dm.splicetext("banana", 3, -5, "test"));
        V.Error(() => Dm.splicetext("banana", 0, 6, "laclav"));
        V.Error(() => Dm.splicetext("abcdef", 4, 3, "test"));
        Assert.Equal("ba" + Emoji + "a", Dm.splicetext_char("banana", 3, 6, Emoji));
        Assert.Equal("x" + Emoji, Dm.splicetext_char("x" + Emoji + Emoji, 2, 3));
    }

    [Fact]
    public void SpantextAndNonspantext()
    {
        // BYOND ref examples (spantext("apples, oranges", ", ", 7) is 2; nonspantext("apples, oranges", ", ") is 6);
        // OpenDream tests Spantext and Nonspantext.
        Assert.Equal(2f, Dm.spantext("apples, oranges", ", ", 7));
        Assert.Equal(0f, Dm.spantext("", "b", 1));
        Assert.Equal(0f, Dm.spantext("a", "", 1));
        Assert.Equal(1f, Dm.spantext("aaaaba", "bb", 5));
        Assert.Equal(3f, Dm.spantext("aaa", "a", 1));
        Assert.Equal(1f, Dm.spantext("aaa", "a", -1));
        Assert.Equal(3f, Dm.spantext("aaa", "a", -4));
        Assert.Equal(0f, Dm.spantext("aaa", "a", 0));
        Assert.Equal(2f, Dm.spantext_char("apples, oranges", ", ", 7));
        Assert.Equal(3f, Dm.spantext_char("aaa", "a", -4));
        Assert.Equal(2f, Dm.spantext_char("aa" + Emoji + Emoji + "bb", Emoji, 3));
        Assert.Equal(6f, Dm.nonspantext("apples, oranges", ", "));
        Assert.Equal(1f, Dm.nonspantext("Hello, World!", ", ", -1));
        Assert.Equal(3f, Dm.nonspantext("Hello, World!", ", ", 3));
        Assert.Equal(0f, Dm.nonspantext("Hello, World!", ", ", 7));
        Assert.Equal(0f, Dm.nonspantext("Hello, World!", ", ", 14));
        Assert.Equal(0f, Dm.nonspantext("Hello, World!", ", ", 0));
    }

    [Fact]
    public void Splittext()
    {
        // OpenDream test Text/Splittext (its text-delimiter cases); BYOND ref splittext: "splittext("a,,b,c", ",") would
        // return a list with four elements"; "Splitting empty text returns an empty list".
        const string text = "The average of 1, 2, 3, 4, 5 is: 3";
        V.Items(Dm.splittext(text, " "), "The", "average", "of", "1,", "2,", "3,", "4,", "5", "is:", "3");
        V.Items(Dm.splittext(text, " ", 5), "The average", "of", "1,", "2,", "3,", "4,", "5", "is:", "3");
        V.Items(Dm.splittext(text, " ", 5, 10), "The average of 1, 2, 3, 4, 5 is: 3");
        V.Items(Dm.splittext(text, " ", 10, 20), "The average", "of", "1,", "2, 3, 4, 5 is: 3");
        V.Items(Dm.splittext(text, " ", 10, 20, 1), "The average", " ", "of", " ", "1,", " ", "2, 3, 4, 5 is: 3");
        V.Items(Dm.splittext("apples,oranges,bananas", ","), "apples", "oranges", "bananas");
        V.Items(Dm.splittext("a,,b,c", ","), "a", "", "b", "c");
        Assert.Equal(0f, Dm.splittext("", ",").len);
        Assert.Equal(0f, Dm.splittext(null, ",").len);
        V.Items(Dm.splittext_char("a" + Emoji + "b" + Emoji + "c", Emoji), "a", "b", "c");
        // an empty delimiter splits into characters (tg's PIN puzzle, puzzle_pieces.dm:591, gaps OD1)
        V.Items(Dm.splittext("123456", ""), "1", "2", "3", "4", "5", "6");
        V.Items(Dm.splittext("abcd", "", 2, 4), "ab", "cd"); // the text after End joins the last piece, as with any delimiter
        V.Items(Dm.splittext_char("a" + Emoji + "b", ""), "a", Emoji, "b");
        V.Error(() => Dm.splittext("a,b", new PlainDatum()));
    }

    [Fact]
    public void Jointext()
    {
        // BYOND ref jointext example and "a single-item list is the same as converting that one item to text, and an
        // empty list becomes an empty string".
        Assert.Equal("apples, oranges, bananas", Dm.jointext(Dm.list("apples", "oranges", "bananas"), ", "));
        Assert.Equal("5", Dm.jointext(Dm.list(5f), ", "));
        Assert.Equal("", Dm.jointext(Dm.list(), ", "));
        Assert.Equal("cat and Bob", Dm.jointext(Dm.list(new Anomaly.Core.Tests.Contracts.TestAtom("cat"), new Anomaly.Core.Tests.Contracts.TestAtom("Bob")), " and "));
        Assert.Equal("2;3", Dm.jointext(Dm.list(1f, 2f, 3f, 4f, 5f), ";", -4, -2));
        Assert.Equal("", Dm.jointext(null, ","));
        V.Error(() => Dm.jointext("text", ","));
    }

    [Fact]
    public void SorttextAndCmptext()
    {
        // BYOND ref sorttext example (sorttext("A","B") is 1, ascending) and cmptext ("Hi" and "HI" are equal);
        // OpenDream test cmptext.
        Assert.Equal(1f, Dm.sorttext("A", "B"));
        Assert.Equal(-1f, Dm.sorttext("B", "A"));
        Assert.Equal(1f, Dm.sorttext("a", "B"));      // case-insensitive
        Assert.Equal(0f, Dm.sorttext("a", "A"));
        Assert.Equal(1f, Dm.sorttext("a", "b", "c"));
        Assert.Equal(0f, Dm.sorttext("a", "c", "b"));
        Assert.Equal(1f, Dm.sorttext(null, "a"));
        Assert.Equal(1f, Dm.cmptext("Hi", "HI"));
        Assert.Equal(1f, Dm.cmptext("Test", "test", "tEsT"));
        Assert.Equal(0f, Dm.cmptext("foo", "bar"));
        Assert.Equal(0f, Dm.cmptext("a", null));
    }

    [Fact]
    public void CkeyAndCase()
    {
        // OpenDream test ckey; BYOND ref ckeyEx ("case is preserved as are the '-' and '_' characters"), uppertext
        // example, trimtext (OpenDream test trimtext).
        Assert.Equal("@abcdef", Dm.ckey("!@#$%^&*()-=[];'\\,./_+{}:|<>?`~ABC def\uFFFD\"\n\t"));
        Assert.Equal("johnsmith", Dm.ckey("John Smith"));
        Assert.Null(Dm.ckey(null));
        Assert.Equal("John_Smith-2", Dm.ckeyEx("John_Smith-2!"));
        Assert.Equal("HI THERE", Dm.uppertext("hi there"));
        Assert.Equal("hi there", Dm.lowertext("HI THERE"));
        Assert.Equal("ПРИВЕТ", Dm.uppertext("привет"));
        Assert.Null(Dm.uppertext(null));
        Assert.Equal("test", Dm.trimtext("   test"));
        Assert.Equal("test", Dm.trimtext("test   "));
        Assert.Equal("test", Dm.trimtext("   test   "));
        Assert.Equal("test", Dm.trimtext("\u00a0test\u2003"));   // "All Unicode whitespace characters are counted"
    }

    [Fact]
    public void AsciiConversions()
    {
        // BYOND ref examples (text2ascii("A") is 65, text2ascii("HAPPY", 2) is 65, ascii2text(65) is "A"); OpenDream test
        // text2ascii2text.
        Assert.Equal(65f, Dm.text2ascii("A"));
        Assert.Equal(65f, Dm.text2ascii("HAPPY", 2));
        Assert.Equal("A", Dm.ascii2text(65));
        Assert.Equal(102f, Dm.text2ascii_char("abcdéfg", 6));
        Assert.Equal("a", Dm.ascii2text(Dm.text2ascii("a")));
        Assert.Equal("\u00A9", Dm.ascii2text(0x00A9));
        Assert.Equal("\u2122", Dm.ascii2text(0x2122));
        Assert.Equal("\u3299", Dm.ascii2text(0x3299));
        Assert.Equal(Emoji, Dm.ascii2text(0x1F600));
        Assert.Equal(0x1F600, Dm.text2ascii_char(Emoji));
        Assert.Equal(0xD83D, Dm.text2ascii(Emoji));     // DV4: the high surrogate
        Assert.Equal(67f, Dm.text2ascii("ABC", -1));
        Assert.Equal(65f, Dm.text2ascii("ABC", 0));
        Assert.Equal(0f, Dm.text2ascii("ABC", 4));
        Assert.Equal(0f, Dm.text2ascii(null));
        V.Error(() => Dm.ascii2text(-1));
    }

    [Fact]
    public void Text2numReadsByondsDecimalExponent()
    {
        // BYOND writes large numbers with an exponent ("2e+005", MetaStation's SMES charge) and reads it back (M3 I1b).
        Assert.Equal(200000f, Dm.text2num("2e+005"));
        Assert.Equal(100000f, Dm.text2num("1e5"));
        Assert.Equal(1.5e6f, Dm.text2num("1.5E+006"));
        Assert.Equal(0.0025f, Dm.text2num("2.5e-3")!.Value, 6);
        Assert.Equal(-300f, Dm.text2num("-3e2"));
        Assert.Equal(2f, Dm.text2num("2e")); // no exponent digits: the number ends at the "e"
        Assert.Equal(2f, Dm.text2num("2e+"));
        Assert.Equal(46f, Dm.text2num("0x2E")); // hex digits, not an exponent
        Assert.Null(Dm.text2num("e5"));
    }

    [Fact]
    public void Text2num()
    {
        // OpenDream test Builtins/text2num (every assertion); BYOND ref text2num("123") is 123.
        Assert.Null(Dm.text2num(null));
        Assert.Null(Dm.text2num(""));
        Assert.Null(Dm.text2num("."));
        Assert.Null(Dm.text2num("-"));
        Assert.Null(Dm.text2num("+"));
        Assert.Equal(123f, Dm.text2num("123"));
        Assert.Equal(12f, Dm.text2num("12"));
        Assert.Equal(12f, Dm.text2num("+12"));
        Assert.Equal(-12f, Dm.text2num("-12"));
        Assert.Equal(0f, Dm.text2num("0"));
        Assert.Equal(1f, Dm.text2num("1.0"));
        Assert.Equal(1.2344f, Dm.text2num("1.2344"));
        Assert.Equal(0f, Dm.text2num("0.0"));
        Assert.Equal(0.4f, Dm.text2num(".4"));
        Assert.Equal(35f, Dm.text2num("Z", 36));
        Assert.Equal(10f, Dm.text2num("A", 16));
        Assert.Equal(35f, Dm.text2num("z", 36));
        Assert.Equal(10f, Dm.text2num("a", 16));
        Assert.Equal(15.625f, Dm.text2num("F.A", 16));
        Assert.Equal(15.625f, Dm.text2num("f.a", 16));
        Assert.Null(Dm.text2num("F.A", 15));
        Assert.Equal(35f, Dm.text2num("Z.0", 36));
        Assert.Equal(15f, Dm.text2num("F..A", 16));
        Assert.Equal(4f, Dm.text2num("4 2"));
        Assert.Equal(10f, Dm.text2num("0xA"));
        Assert.Equal(10f, Dm.text2num("0xA", 16));
        Assert.Equal(0f, Dm.text2num("0xA", 15));
        Assert.Equal(1198f, Dm.text2num("0xA", 36));
        Assert.Null(Dm.text2num("nan"));
        Assert.Null(Dm.text2num(" -nansomething"));
        Assert.Null(Dm.text2num("a"));
        Assert.Null(Dm.text2num("A"));
        Assert.Equal(10f, Dm.text2num("a", 11));
        Assert.Equal(10f, Dm.text2num("A", 11));
        Assert.Equal(7f, Dm.text2num(7f));             // a number passes through
        Assert.Equal(7f, Dm.text2num((object)7));
        Assert.Equal(42f, Dm.text2num("  42  "));
        V.Error(() => Dm.text2num(Dm.list()));
        V.Error(() => Dm.text2num("1", 1));
    }

    [Fact]
    public void NegativePositionsCountFromTheEndInTheUnitsOfTheProc()
    {
        // BYOND ref /proc/copytext, /proc/findtext, /proc/findlasttext, /proc/splicetext: "If the start or end position
        // is negative, it counts backwards from the end of the string"; the _char procs count characters, the others
        // UTF-16 code units (rules 5.6, DV4), so the same negative position lands on different text when a character
        // outside the BMP precedes it.
        const string e = "\U0001F600";                  // one character, two UTF-16 units
        string s = "a" + e + "b" + e + "c";              // characters: a 1, e 2, b 3, e 4, c 5; units: a 1, e 2-3, b 4, e 5-6, c 7
        Assert.Equal(5f, Dm.length_char(s));
        Assert.Equal(7f, Dm.length(s));
        Assert.Equal("c", Dm.copytext_char(s, -1));
        Assert.Equal(e + "c", Dm.copytext_char(s, -2));
        Assert.Equal("b" + e, Dm.copytext_char(s, 3, -1));
        Assert.Equal(e + "b", Dm.copytext_char(s, -4, -2));
        Assert.Equal(e + "c", Dm.copytext(s, -3));
        Assert.Equal("b", Dm.copytext(s, -4, -3));
        Assert.Equal(s, Dm.copytext_char(s, -10));       // a start before the beginning is the beginning
        Assert.Equal("", Dm.copytext_char(s, 3, 2));
        Assert.Equal(4f, Dm.findtext_char(s, e, -2));
        Assert.Equal(2f, Dm.findtext_char(s, e, -10));
        Assert.Equal(0f, Dm.findtext_char(s, "c", 1, -1));
        Assert.Equal(5f, Dm.findtext(s, e, -3));
        Assert.Equal(2f, Dm.findlasttext_char(s, e, -3));
        Assert.Equal(4f, Dm.findlasttext_char(s, e));
        Assert.Equal(0f, Dm.findlasttext_char(s, e, 0, -1));
        Assert.Equal("a" + e + "bxc", Dm.replacetext_char(s, e, "x", 3));
        Assert.Equal("ax" + "b" + e + "c", Dm.replacetext_char(s, e, "x", 1, -2));
        Assert.Equal("a" + e + "b" + "Z", Dm.splicetext_char(s, -2, 0, "Z"));
        Assert.Equal(0x1F600, (int)Dm.text2ascii_char(s, -2));
        Assert.Equal((int)'c', (int)Dm.text2ascii(s, -1));
        Assert.Equal(3f, Dm.spantext_char(s, e + "b", -4));   // e, b, e from character 2
        V.Items(Dm.splittext_char(s, e, 3), "a" + e + "b", "c");
        V.Items(Dm.splittext_char(s, e, -3, -1), "a" + e + "b", "c");
    }

    [Fact]
    public void RegexNeedle()
    {
        // BYOND ref findtext and replacetext: a /regex needle is the same as Needle.Find(Haystack, Start, End) and
        // Needle.Replace(Haystack, Replacement, Start, End), with the regex's own case and global flags (the lizard tongue's
        // speechmod, _tongue.dm:210)
        Assert.Equal("yesss sssir", Dm.replacetextEx("yes sir", Dm.regex("s+", "g"), "sss"));
        Assert.Equal("yesss sir", Dm.replacetextEx("yes sir", Dm.regex("s+"), "sss"));
        Assert.Equal("akss", Dm.replacetextEx("ax", Dm.regex(@"(\w)x", "g"), "$1kss"));
        Assert.Equal("ecks-ray", Dm.replacetextEx("x-ray", Dm.regex(@"\bx([\-|r|R]|\b)", "g"), "ecks$1"));
        Assert.Equal(3f, Dm.findtext("abcabc", Dm.regex("c")));
        Assert.Equal(0f, Dm.findtextEx("abc", Dm.regex("C")));
        Assert.Equal(3f, Dm.findtext("abc", Dm.regex("C", "i")));
    }

    [Fact]
    public void TextIndex()
    {
        // DM 515 T[i] (rules 5.6): the character at a UTF-16 position; a character outside the BMP is its two units; out of range is a runtime error
        Assert.Equal("H", Dm.textchar("Hi", 1));
        Assert.Equal("i", Dm.textchar("Hi", 2));
        Assert.Equal("п", Dm.textchar("привет", 1));
        Assert.Equal(Emoji, Dm.textchar("a" + Emoji + "b", 2));
        Assert.Equal("b", Dm.textchar("a" + Emoji + "b", 4));
        Assert.Throws<DmRuntimeException>(() => Dm.textchar("Hi", 3));
        Assert.Throws<DmRuntimeException>(() => Dm.textchar("Hi", 0));
        Assert.Throws<DmRuntimeException>(() => Dm.textchar(null, 1));
    }
}
