using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tests.DmValues;

/// <summary>
/// The shim's <c>/regex</c> (M6 slice 2; BYOND ref <c>/regex</c>, <c>/proc/regex</c>): <c>Find</c> with its <c>match</c>,
/// <c>group</c>, <c>index</c> and the <c>"g"</c> flag's <c>next</c>, <c>Replace</c> with text and group references, the first
/// match or every match, BYOND's <c>\l</c> escape, the flags and <c>new /regex</c>; OpenDream's BYOND-checked test
/// Regex/regex_find_replace.
/// </summary>
public class RegexTests
{
    [Fact]
    public void FindSetsTheMatchItsGroupsAndItsIndex()
    {
        regex r = Dm.regex(@"(\d+)-(\d+)");
        Assert.Equal(5f, r.Find("abc 12-34 x"));
        Assert.Equal("12-34", r.match);
        Assert.Equal(5f, r.index);
        Assert.Equal(new object[] { "12", "34" }, Dm.each(r.group).ToArray());
        Assert.Equal("abc 12-34 x", r.text);
        Assert.Equal(0f, r.Find("nothing here"));
        Assert.Null(r.next); // no "g" flag: next is not kept
    }

    [Fact]
    public void TheGlobalFlagResumesAtNext()
    {
        regex r = Dm.regex(@"\d", "g");
        Assert.Equal(2f, r.Find("a1b2c3"));
        Assert.Equal(3f, r.next);
        Assert.Equal(4f, r.Find("a1b2c3")); // the same haystack without Start resumes at next
        Assert.Equal(6f, r.Find("a1b2c3"));
        Assert.Equal(0f, r.Find("a1b2c3"));
        Assert.Null(r.next);
        Assert.Equal(2f, r.Find("a1b2c3", 1)); // an explicit Start searches from it
    }

    [Fact]
    public void AGlobalFindMovesPastAnEmptyMatch()
    {
        // tg's chatter (modules/chatter/chatter.dm:12) counts the letters of each word of speech this way
        regex r = Dm.regex("([\\l\\d]*)([^\\l\\d\\s])?", "g");
        var groups = new List<string>();
        int finds = 0;
        while (!Dm.eq(r.Find("Hi there!"), 0) && finds++ < 20)
            groups.Add(Dm.str(r.group[1]) + "|" + Dm.str(r.group[2]));
        Assert.True(finds < 20, "the search never ended");
        Assert.Equal(new[] { "Hi|", "|", "there|!" }, groups);
    }

    [Fact]
    public void StartAndEndBoundTheSearchAsFindtextDoes()
    {
        regex r = Dm.regex("b");
        Assert.Equal(4f, r.Find("abcb", 3));
        Assert.Equal(0f, r.Find("abcb", 3, 4)); // End is the position after the last character searched
        Assert.Equal(4f, r.Find("abcb", 3, 5));
    }

    [Fact]
    public void ReplaceReplacesTheFirstMatchOrEveryMatchWithGroups()
    {
        Assert.Equal("x-b-a", Dm.regex("a").Replace("a-b-a", "x"));
        Assert.Equal("x-b-x", Dm.regex("a", "g").Replace("a-b-a", "x"));
        Assert.Equal("34-12", Dm.regex(@"(\d+)-(\d+)").Replace("12-34", "$2-$1"));
        Assert.Equal("A1 A2", Dm.regex("a", "gi").Replace("a1 A2", "A"));
        regex r = Dm.regex("o");
        Assert.Equal("fxo", r.Replace("foo", "x"));
        Assert.Equal("fxo", r.text);
        Assert.Equal(3f, r.next); // the position after the replacement
    }

    [Fact]
    public void BYONDsLetterEscapeAndANewRegex()
    {
        Assert.Equal(3f, Dm.regex(@"\l+").Find("12abc"));
        Assert.Equal("12", Dm.regex(@"\l", "g").Replace("1a2b", ""));
        var copy = (regex)Dm.@new(typeof(regex), Dm.regex("b+", "g"));
        Assert.Equal("b+", copy.name);
        Assert.Equal("g", copy.flags);
        Assert.Equal("a-c", copy.Replace("abbc", "-"));
        Assert.Throws<DmRuntimeException>(() => Dm.regex(null));
    }

    [Fact]
    public void FindAndReplaceWalkAQuotedMapModel()
    {
        // OpenDream Content.Tests/DMProject/Tests/Regex/regex_find_replace.dm
        string models = "/obj/cable/brown{\n\ticon_state = \"2-8\"\n\t},\n/obj/cable/brown{\n\ticon_state = \"4-8\"\n\t},\n/turf/simulated/floor/orangeblack,\n/area/station/devzone";
        const string result_match = "/obj/cable/brown{\n\ticon_state = \"1\"\n\t},\n/obj/cable/brown{\n\ticon_state = \"2\"\n\t},\n/turf/simulated/floor/orangeblack,\n/area/station/devzone";
        var original_strings = new Dictionary<string, string>();
        regex no_strings = Dm.regex(@"([""])(?:(?=(\\?))\2(.|\n))*?\1");
        int string_index = 1;
        object found;
        do
        {
            found = no_strings.Find(models, no_strings.next);
            if (Dm.truthy(found))
            {
                string index_text = "\"" + string_index + "\"";
                string_index++;
                string match = Dm.copytext((string)no_strings.match, 2, -1); // Strip quotes
                models = (string)no_strings.Replace(models, index_text, found);
                original_strings[index_text] = match;
            }
        }
        while (Dm.truthy(found));
        Assert.Equal(result_match, models);
        Assert.Equal(new Dictionary<string, string> { ["\"1\""] = "2-8", ["\"2\""] = "4-8" }, original_strings);
        Assert.Equal(3, string_index);
    }

    [Fact]
    public void QuotingEscapesTheMetacharacters()
    {
        Assert.Equal(1f, Dm.regex(Dm.REGEX_QUOTE("a.b[c]")).Find("a.b[c]"));
        Assert.Equal(0f, Dm.regex(Dm.REGEX_QUOTE("a.b")).Find("axb"));
        Assert.Equal("$1", Dm.regex("x").Replace("x", Dm.REGEX_QUOTE_REPLACEMENT("$1")));
    }
}
