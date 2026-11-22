using Anomaly.Core.DmShim;
using Anomaly.Core.Tests.Contracts;

namespace Anomaly.Core.Tests.DmValues;

/// <summary>
/// The text builtins take any value for their text arguments (rules 3.5 v1.2, lead decisions 40 and C2: tg calls them
/// with mobs, typepaths, numbers and nulls, cmp_text_asc over sort_list's default, __HELPERS/mobs.dm:415). A value that
/// is not text behaves as in OpenDream's <c>DreamProcNativeRoot</c>, which reads every text argument with
/// <c>TryGetValueAsString</c>: mostly like null; <c>uppertext</c>/<c>lowertext</c> return it unchanged,
/// <c>html_encode</c>/<c>html_decode</c> stringify it, <c>json_decode</c> raises (docs/ai/specs/dm-conformance.md, "Values").
/// </summary>
public class TextNonTextArgumentTests
{
    private static readonly NamedDatum Mob = new("Bob");
    private static readonly object Five = 5f;

    [Fact]
    public void SorttextPutsEveryNonTextValueBeforeText()
    {
        // OpenDream NativeProc_sorttext: T1 not text and T2 text gives 1, T1 text and T2 not text -1, neither 0.
        Assert.Equal(1f, Dm.sorttext(Mob, "a"));
        Assert.Equal(-1f, Dm.sorttext("a", Mob));
        Assert.Equal(0f, Dm.sorttext(Mob, typeof(DmList)));
        Assert.Equal(1f, Dm.sorttext(Five, "5"));
        Assert.Equal(-1f, Dm.sorttext("a", null));
        Assert.Equal(0f, Dm.sorttext(null, Mob, "a"));   // null and Mob are equal neighbours: neither order
    }

    [Fact]
    public void SorttextExIsCaseSensitive()
    {
        // BYOND ref /proc/sorttextEx ("case sensitive"); OpenDream NativeProc_sorttextEx compares ordinally.
        Assert.Equal(1f, Dm.sorttextEx("A", "B"));
        Assert.Equal(-1f, Dm.sorttextEx("B", "A"));
        Assert.Equal(1f, Dm.sorttextEx("B", "a"));      // upper case sorts before lower case
        Assert.Equal(-1f, Dm.sorttextEx("a", "B"));
        Assert.Equal(1f, Dm.sorttextEx("A", "a"));
        Assert.Equal(0f, Dm.sorttext("A", "a"));         // equal for the case-insensitive one
        Assert.Equal(0f, Dm.sorttextEx("a", "a"));
        Assert.Equal(1f, Dm.sorttextEx("a", "b", "c"));
        Assert.Equal(1f, Dm.sorttextEx(Mob, "a"));
        Assert.Equal(0f, Dm.sorttextEx("only"));
    }

    [Fact]
    public void CmptextGivesZeroForAnyNonTextValue()
    {
        Assert.Equal(0f, Dm.cmptext(Mob, Mob));
        Assert.Equal(0f, Dm.cmptext("5", Five));
        Assert.Equal(0f, Dm.cmptext(Five, "5"));
        Assert.Equal(1f, Dm.cmptext((object)"Hi", "HI"));
        Assert.Equal(0f, Dm.cmptextEx("Hi", "HI"));
        Assert.Equal(1f, Dm.cmptextEx("Hi", "Hi", "Hi"));
        Assert.Equal(0f, Dm.cmptextEx("Hi", "Hi", "hi"));
        Assert.Equal(1f, Dm.cmptextEx(Dm.text("\\improper CPU"), "CPU")); // the markers are not text, as for cmptext
        Assert.Equal(0f, Dm.cmptextEx(typeof(DmList), "/list"));
    }

    [Fact]
    public void PositionalTextProcsTreatANonTextValueAsNull()
    {
        Assert.Null(Dm.copytext(Five));                  // OpenDream NativeProc_copytext: null when End is 0, else ""
        Assert.Equal("", Dm.copytext(Mob, 1, 2));
        Assert.Equal(0f, Dm.findtext(Five, "5"));        // one bad argument: 0
        Assert.Equal(1f, Dm.findtext(Five, Five));       // two bad arguments: 1 (OpenDream failCount)
        Assert.Equal(0f, Dm.findtextEx(Mob, "Bob"));
        Assert.Equal(0f, Dm.findlasttext(Five, "5"));
        Assert.Null(Dm.replacetext(Mob, "B", "R"));      // OpenDream HandleReplaceText: null for a non-text haystack
        Assert.Equal("x", Dm.splicetext(Five, 1, 0, "x"));
        Assert.Equal("ab", Dm.splicetext("ab", 2, 2, Five)); // a non-text Insert inserts nothing
        Assert.Equal(0f, Dm.spantext(Five, "5"));
        Assert.Equal(0f, Dm.spantext("55", Five));
        Assert.Equal(0f, Dm.nonspantext(Five, "x"));
        Assert.Equal(1f, Dm.nonspantext("abc", Five));   // OpenDream NativeProc_nonspantext: 1 for bad needles
        Assert.Empty(Dm.each(Dm.splittext(Five, ",")));
        Assert.Equal(0f, Dm.text2ascii(Five));
        Assert.Equal(0f, Dm.text2ascii_char(Mob));
        Assert.Null(Dm.trimtext(Five));
        Assert.Null(Dm.ckey(Five));
        Assert.Null(Dm.ckeyEx(Mob));
        Assert.Equal("ab", Dm.jointext(Dm.list("a", "b"), Five)); // a non-text Glue joins with nothing
        Assert.Empty(Dm.each(Dm.params2list(Mob)));
    }

    [Fact]
    public void CaseProcsReturnANonTextValueUnchanged()
    {
        // OpenDream NativeProc_uppertext and NativeProc_lowertext: "if (!arg.TryGetValueAsString(out var text)) return arg".
        Assert.Same(Five, Dm.uppertext(Five));
        Assert.Same(Mob, Dm.lowertext((object)Mob));
        Assert.Null(Dm.uppertext((object)null));
        Assert.Equal("AB", Dm.uppertext((object)"ab"));
        string typed = Dm.lowertext("AB");                // the text overload keeps the C# type
        Assert.Equal("ab", typed);
    }

    [Fact]
    public void HtmlProcsStringifyAndJsonDecodeRaises()
    {
        // OpenDream NativeProc_html_encode and html_decode: Stringify() of the argument; json_decode: "Unknown value".
        Assert.Equal("5", Dm.html_encode(Five));
        Assert.Equal("Bob", Dm.html_encode(Mob));
        Assert.Equal("", Dm.html_encode(null));
        Assert.Equal("5", Dm.html_decode(Five));
        V.Error(() => Dm.json_decode(Five));
        V.Error(() => Dm.json_decode(null));
    }
}
