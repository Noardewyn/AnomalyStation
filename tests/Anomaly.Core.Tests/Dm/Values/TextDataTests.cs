using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tests.DmValues;

/// <summary>
/// Text encodings and formats (docs/ai/translation-rules.md 5; DV3): BYOND ref <c>/proc/json_encode</c>,
/// <c>json_decode</c>, <c>params2list</c>, <c>list2params</c>, <c>html_encode</c>, <c>html_decode</c>, <c>time2text</c>,
/// <c>rgb</c>, <c>rgb2num</c>, "HTML colors"; OpenDream's BYOND-checked tests Builtins/JsonEncode, json_decode,
/// params2list, rgb/rgb, rgb/rgb2num.
/// </summary>
public class TextDataTests
{
    [Fact]
    public void JsonEncode()
    {
        // OpenDream test JsonEncode (every assertion without a resource or a proc path).
        Assert.Equal("7", Dm.json_encode(7f));
        Assert.Equal("15.5", Dm.json_encode(15.5f));
        Assert.Equal("{\"__number__\":\"NaN\"}", Dm.json_encode(float.NaN));
        Assert.Equal("{\"__number__\":\"Infinity\"}", Dm.json_encode(float.PositiveInfinity));
        Assert.Equal("{\"__number__\":\"-Infinity\"}", Dm.json_encode(float.NegativeInfinity));
        Assert.Equal("\"A\"", Dm.json_encode("A"));
        Assert.Equal("[1,3,5,7,11]", Dm.json_encode(Dm.list(1f, 3f, 5f, 7f, 11f)));
        Assert.Equal("{\"A\":3,\"B\":5}", Dm.json_encode(Dm.list(Dm.kv("A", 3f), Dm.kv("B", 5f))));
        Assert.Equal("[1,2,3,4,5,6]", Dm.json_encode(Dm.matrix(1, 2, 3, 4, 5, 6)));
        DmList fridge = Dm.list(Dm.kv("name", "fridge"), Dm.kv("power", 12f));
        Assert.True(Dm.json_encode(fridge).Length < Dm.json_encode(fridge, Dm.JSON_PRETTY_PRINT).Length);
        Assert.Equal("null", Dm.json_encode(null));
        Assert.Equal("\"/list\"", Dm.json_encode(typeof(DmList)));
        Assert.Equal("{\"a\":null,\"b\":1}", Dm.json_encode(Dm.list("a", Dm.kv("b", 1f))));
        Assert.Equal("\"say \\\"hi\\\" привет\"", Dm.json_encode("say \"hi\" привет"));
        Assert.Equal("\"CPU\"", Dm.json_encode(Dm.text("\\improper CPU")));
        Assert.Equal("[[\"x\"],\"Bob\"]", Dm.json_encode(Dm.list(Dm.list("x"), new Anomaly.Core.Tests.Contracts.TestAtom("Bob"))));
        DmList deep = Dm.list();
        DmList cursor = deep;
        for (int i = 0; i < 30; i++)
        {
            var next = Dm.list();
            cursor.AppendItem(next);
            cursor = next;
        }
        // "In parity with DM, we give up and just print a 'null' at the maximum recursion" (OpenDream JsonEncode).
        Assert.Equal(new string('[', 20) + "null" + new string(']', 20), Dm.json_encode(deep));
    }

    [Fact]
    public void JsonDecode()
    {
        // OpenDream test json_decode (JSON(e) == json_decode(json_encode(e))).
        V.Eq(0f, Dm.json_decode(Dm.json_encode(0f)));
        V.Eq(5f, Dm.json_decode(Dm.json_encode(5f)));
        Assert.True(float.IsNaN((float)Dm.json_decode(Dm.json_encode(float.NaN))));
        V.Eq(float.NegativeInfinity, Dm.json_decode(Dm.json_encode(float.NegativeInfinity)));
        V.Eq(float.PositiveInfinity, Dm.json_decode(Dm.json_encode(float.PositiveInfinity)));
        V.Items((DmList)Dm.json_decode(Dm.json_encode(Dm.list(1f, 2f, 3f, 4f, 5f))), 1f, 2f, 3f, 4f, 5f);
        var assoc = (DmList)Dm.json_decode(Dm.json_encode(Dm.list(Dm.kv("a", 1f), Dm.kv("b", 2f), Dm.kv("c", 5f))));
        V.Items(assoc, "a", "b", "c");
        V.Eq(2f, assoc["b"]);
        V.Eq(5f, assoc["c"]);
        V.Eq(1f, Dm.json_decode("true"));
        V.Eq(0f, Dm.json_decode("false"));
        Assert.Null(Dm.json_decode("null"));
        V.Eq("x", Dm.json_decode("\"x\""));
        Assert.Equal(0f, ((DmList)Dm.json_decode("{}")).len);
        var nested = (DmList)Dm.json_decode("{\"list\":[1,{\"k\":\"v\"}]}");
        var inner = (DmList)nested["list"];
        V.Eq("v", ((DmList)inner[2])["k"]);
        V.Error(() => Dm.json_decode("{not json"));
        V.Error(() => Dm.json_decode(null));
    }

    [Fact]
    public void ParamsLists()
    {
        // OpenDream test params2list; BYOND ref list2params / params2list (associative "a=1;b=2" text, URL-encoded).
        Assert.True(Dm.equiv(Dm.params2list("a;b;c"), Dm.list(Dm.kv("a", ""), Dm.kv("b", ""), Dm.kv("c", ""))));
        Assert.Equal("{\"a\":[\"\",\"\",\"\"]}", Dm.json_encode(Dm.params2list("a;a;a")));
        Assert.True(Dm.equiv(Dm.params2list("a=1;b=2"), Dm.list(Dm.kv("a", "1"), Dm.kv("b", "2"))));
        Assert.True(Dm.equiv(Dm.params2list("a=1;a=2")["a"], Dm.list("1", "2")));
        DmList href = Dm.params2list("src=%5B0x2%5D&name=John+Smith&action=start");
        V.Eq("[0x2]", href["src"]);
        V.Eq("John Smith", href["name"]);
        V.Eq("start", href["action"]);
        Assert.Equal(0f, Dm.params2list(null).len);
        Assert.Equal("a=1&b", Dm.list2params(Dm.list(Dm.kv("a", "1"), "b")));
        Assert.Equal("a+b=c%26d", Dm.list2params(Dm.list(Dm.kv("a b", "c&d"))));
        Assert.Equal("n=5", Dm.list2params(Dm.list(Dm.kv("n", 5f))));
        Assert.Equal("", Dm.list2params("not a list"));
    }

    [Fact]
    public void HtmlEncoding()
    {
        // BYOND ref html_encode ("< is produced by the code &lt; and > is produced by the code &gt;") and html_decode.
        Assert.Equal("&lt;b&gt;&quot;x&quot; &amp; &#39;y&#39;&lt;/b&gt;", Dm.html_encode("<b>\"x\" & 'y'</b>"));
        Assert.Equal("привет é", Dm.html_encode("привет é"));
        Assert.Equal("", Dm.html_encode(null));
        Assert.Equal("<b>&\"' </b>", Dm.html_decode("&lt;b&gt;&amp;&quot;&#39;&nbsp;&lt;/b&gt;"));
        Assert.Equal("<b>", Dm.html_decode(Dm.html_encode("<b>")));
    }

    [Fact]
    public void Time2textFormatsWithoutAWallClock()
    {
        // BYOND ref time2text: deciseconds since 00:00 UTC, January 1, 2000; the default format "DDD MMM DD hh:mm:ss
        // YYYY"; values below 864000 are a time of the current date. January 1, 2000 was a Saturday. DV3: the date of
        // world.realtime and UTC replace the wall clock and the host's time zone.
        Assert.Equal("2000-01-01 00:00:00", Dm.time2text(0, "YYYY-MM-DD hh:mm:ss"));
        Assert.Equal("Sat Jan 01 00:00:00 2000", Dm.time2text(0));
        Assert.Equal("Saturday, January 01 00", Dm.time2text(0, "Day, Month DD YY"));
        Assert.Equal("2000-02-01", Dm.time2text(864000f * 31, "YYYY-MM-DD"));
        Assert.Equal("2001-01-01", Dm.time2text(864000f * 366, "YYYY-MM-DD"));
        Assert.Equal("01:30:15", Dm.time2text(54150, "hh:mm:ss"));
        Assert.Equal("03", Dm.time2text(0, "hh", 3));
        Assert.Equal("19", Dm.time2text(864000f * 31, "hh", -5));
        Assert.Equal("at 12 o'clock", Dm.time2text(432000, "at hh o'clock"));
        var w = new World(1);
        using (w.Enter())
        {
            DmWorldState.Current.RealtimeStart = 864000d * 31 + 36000;   // Feb 1, 2000, 01:00
            Assert.Equal("2000-02-01 02:00", Dm.time2text(72000, "YYYY-MM-DD hh:mm"));
            Assert.Equal("2000-02-01 01:00", Dm.time2text(Dm.world.realtime, "YYYY-MM-DD hh:mm"));
        }
    }

    [Fact]
    public void RgbBuildsColorText()
    {
        // OpenDream test rgb/rgb (positional and space arguments) and BYOND ref rgb.
        Assert.Equal("#ffffff", Dm.rgb(255, 255, 255));
        Assert.Equal("#ff0000", Dm.rgb(255, 0, 0));
        Assert.Equal("#0000ff", Dm.rgb(0, 0, 255));
        Assert.Equal("#12f5e6", Dm.rgb(18, 245, 230));
        Assert.Equal("#12f5e680", Dm.rgb(18, 245, 230, 128));
        Assert.Equal("#ca60db", Dm.rgb(202, 96, 219, space: Dm.COLORSPACE_RGB));
        Assert.Equal("#ca60db", Dm.rgb(291.70734f, 56.164383f, 85.882355f, space: Dm.COLORSPACE_HSV));
        Assert.Equal("#ca60db", Dm.rgb(291.70734f, 63.07692f, 61.764706f, space: Dm.COLORSPACE_HSL));
        Assert.Equal("#ca60db80", Dm.rgb(291.70734f, 63.07692f, 61.764706f, 128, Dm.COLORSPACE_HSL));
        Assert.Equal("#010203", Dm.rgb(1, 2, 3));
        Assert.Equal("#ff0012", Dm.rgb(300, -5, 18.9f));   // clamped and truncated
    }

    [Fact]
    public void Rgb2numParsesColors()
    {
        // OpenDream test rgb/rgb2num (its BYOND-checked part), BYOND ref rgb2num examples (#ff8000; "#5af" in HSL is
        // hue 210, saturation 100, luminance 66.6667) and "HTML colors" (the named colors).
        V.Items(Dm.rgb2num(null), 255f, 255f, 255f);
        V.Items(Dm.rgb2num("#fff"), 255f, 255f, 255f);
        V.Items(Dm.rgb2num("#f00"), 255f, 0f, 0f);
        V.Items(Dm.rgb2num("#00f"), 0f, 0f, 255f);
        V.Items(Dm.rgb2num("#ffffff"), 255f, 255f, 255f);
        V.Items(Dm.rgb2num("#ff0000"), 255f, 0f, 0f);
        V.Items(Dm.rgb2num("#0000ff"), 0f, 0f, 255f);
        V.Items(Dm.rgb2num("#12f5e6"), 18f, 245f, 230f);
        V.Items(Dm.rgb2num("#ca60db", Dm.COLORSPACE_RGB), 202f, 96f, 219f);
        V.Items(Dm.rgb2num("#ff8000"), 255f, 128f, 0f);
        DmList hsl = Dm.rgb2num("#5af", Dm.COLORSPACE_HSL);
        Assert.Equal("210", Dm.str(hsl[1]));
        Assert.Equal("100", Dm.str(hsl[2]));
        Assert.Equal("66.6667", Dm.str(hsl[3]));
        V.Items(Dm.rgb2num("#ff000080"), 255f, 0f, 0f, 128f);
        V.Items(Dm.rgb2num("#f008"), 255f, 0f, 0f, 136f);
        V.Items(Dm.rgb2num("red"), 255f, 0f, 0f);
        V.Items(Dm.rgb2num("green"), 0f, 192f, 0f);
        V.Items(Dm.rgb2num("Grey"), 128f, 128f, 128f);
        V.Error(() => Dm.rgb2num("#12345"));
        V.Error(() => Dm.rgb2num("not a color"));
        DmList hsv = Dm.rgb2num("#ca60db", Dm.COLORSPACE_HSV);
        Assert.Equal("#ca60db", Dm.rgb(Dm.num(hsv[1]), Dm.num(hsv[2]), Dm.num(hsv[3]), space: Dm.COLORSPACE_HSV));
    }

    [Fact]
    public void JsonEncodeWritesNullForAListInsideItself()
    {
        // BYOND ref /proc/json_encode: "Situations where a list has a reference to itself will cause the nested version
        // to print a null value instead." A list that only appears twice, without nesting in itself, is written twice.
        DmList L = Dm.list(1f, 2f);
        L.len = 3;
        L[3] = L;
        Assert.Equal("[1,2,null]", Dm.json_encode(L));
        DmList A = Dm.list(Dm.kv("name", "a"));
        A["self"] = A;
        Assert.Equal("{\"name\":\"a\",\"self\":null}", Dm.json_encode(A));
        DmList outer = Dm.list(0f);
        DmList inner = Dm.list(outer);
        outer[1] = inner;                                // outer holds inner, which holds outer
        Assert.Equal("[[null]]", Dm.json_encode(outer));
        Assert.Equal("[[null]]", Dm.json_encode(inner));
        DmList shared = Dm.list(1f);
        Assert.Equal("[[1],[1]]", Dm.json_encode(Dm.list(shared, shared)));
        Assert.Equal("{\"a\":[1],\"b\":[1]}", Dm.json_encode(Dm.list(Dm.kv("a", shared), Dm.kv("b", shared))));
    }

    [Fact]
    public void JsonPrettyPrintPutsEachItemOnItsOwnLineIndentedWithTabs()
    {
        // BYOND ref /proc/json_encode: with JSON_PRETTY_PRINT (1) "Spaces will be added after colons and commas.
        // Non-empty arrays and object literals will have a line break and tabs before each item, and a line break with
        // one fewer tab before the closing bracket or brace. (The special formatting for numbers like Infinity, or the
        // list form for matrices, will not be given tabs and line breaks.)" A line break is "\n" on every host (the
        // core gives the same text everywhere, D31).
        string text = Dm.json_encode(Dm.list(Dm.kv("a", 1f), Dm.kv("b", Dm.list(2f, 3f)), Dm.kv("c", Dm.list())), 1);
        Assert.DoesNotContain("\r", text);
        Assert.Equal(
            new[] { "{", "\t\"a\": 1,", "\t\"b\": [", "\t\t2,", "\t\t3", "\t],", "\t\"c\": []", "}" },
            text.Split('\n').Select(line => line.TrimEnd(' ')).ToArray());
        string special = Dm.json_encode(Dm.list(Dm.matrix(), float.NaN), 1);
        string[] lines = special.Split('\n');
        Assert.Equal(4, lines.Length);
        Assert.Equal("[", lines[0]);
        Assert.StartsWith("\t[1,", lines[1]);
        Assert.EndsWith("0],", lines[1].TrimEnd(' '));
        Assert.StartsWith("\t{\"__number__\": \"NaN\"}", lines[2]);
        Assert.Equal("]", lines[3]);
        Assert.Equal("[1,2]", Dm.json_encode(Dm.list(1f, 2f)));     // no flags, no spacing
    }

    [Fact]
    public void JsonDecodeReversesJsonEncode()
    {
        // OpenDream test Builtins/json_decode (JSON(v) == v for numbers, the specials, flat and associative lists),
        // extended to nesting and text escapes: arrays come back as lists, objects as associative lists with text keys
        // (BYOND ref /proc/json_decode), numbers as the same 32-bit floats (json_encode writes the shortest text that
        // reads back as the same float).
        foreach (float f in new[] { 0f, 5f, -2.25f, 0.5f, 1000000f, 16777216f, 0.1f, 1f / 3f, 3.4028235e38f, 1.401298e-45f, -0f })
        {
            object back = Dm.json_decode(Dm.json_encode(f));
            Assert.True(back is float g && BitConverter.SingleToInt32Bits(g) == BitConverter.SingleToInt32Bits(f), "json round trip of " + Dm.num2text(f, 9));
        }
        Assert.True(float.IsNaN(Dm.num(Dm.json_decode(Dm.json_encode(float.NaN)))));
        Assert.Equal(float.PositiveInfinity, Dm.num(Dm.json_decode(Dm.json_encode(float.PositiveInfinity))));
        Assert.Equal(float.NegativeInfinity, Dm.num(Dm.json_decode(Dm.json_encode(float.NegativeInfinity))));
        const string tricky = "quote \" backslash \\ newline \n tab \t slash / Привет 😀";
        Assert.Equal(tricky, Dm.json_decode(Dm.json_encode(tricky)));
        DmList nested = Dm.list(1f, Dm.list(Dm.kv("a", Dm.list(2f, 3f)), Dm.kv("b", null)), "x", null);
        string once = Dm.json_encode(nested);
        Assert.Equal("[1,{\"a\":[2,3],\"b\":null},\"x\",null]", once);
        var decoded = Assert.IsType<DmList>(Dm.json_decode(once));
        Assert.Equal(once, Dm.json_encode(decoded));
        V.Items(decoded, 1f, decoded[2], "x", null);
        var middle = Assert.IsType<DmList>(decoded[2]);
        V.Items(middle, "a", "b");
        V.Items(Assert.IsType<DmList>(middle["a"]), 2f, 3f);
        Assert.Null(middle["b"]);
        Assert.Equal(0f, Dm.length(Dm.json_decode("[]")));
        Assert.Equal(0f, Dm.length(Dm.json_decode("{}")));
    }
}
