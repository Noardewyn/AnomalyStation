using System.Globalization;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tests.Contracts;

namespace Anomaly.Core.Tests.DmValues;

/// <summary>
/// How values render as text (docs/ai/translation-rules.md 3.9, 5.2-5.5): BYOND ref "text" (embedded expressions),
/// "macros (text)", <c>/proc/text</c>, <c>/proc/num2text</c>; OpenDream <c>DreamValue.Stringify</c>, <c>FormatString</c>
/// and its BYOND-checked tests (Math/Text, Text/StringInterpolation1-7, Text/HoleEmbeddedExpression,
/// Expression/String/unicode, Expression/Constants/stringify_inf).
/// </summary>
public class TextRenderTests
{
    [Fact]
    public void NumbersHaveSixSignificantDigits()
    {
        // OpenDream test Math/Text (every case), rules 5.5.
        Assert.Equal("1e-06", Dm.str(0.000001f));
        Assert.Equal("-1e-06", Dm.str(-0.000001f));
        Assert.Equal("1e+08", Dm.str(99999999f));
        Assert.Equal("1.67772e+07", Dm.str(16777218f));
        Assert.Equal("1e+06", Dm.str(999999.9f));
        Assert.Equal("0.0001", Dm.str(0.0001f));
        Assert.Equal("167321", Dm.str(167321.28791236212f));
        Assert.Equal("167.288", Dm.str(167.28791236212f));
        Assert.Equal("16777216", Dm.str(16777216f));
        Assert.Equal("1.67772e+06", Dm.str(1677720.5f));
        Assert.Equal("1000000", Dm.str(1000000f));    // a whole number up to 2^24 keeps its digits
        Assert.Equal("-1e+06", Dm.str(-1000000f));   // positive ones only, as in OpenDream (rules 5.5; unverified on BYOND 516)
        Assert.Equal("0.5", Dm.str(0.5f));
        Assert.Equal("5", Dm.str(5));
        Assert.Equal("0.333333", Dm.str(1f / 3f));
        Assert.Equal("123457", Dm.str(123456.7f));
        Assert.Equal("inf", Dm.str(float.PositiveInfinity));
        Assert.Equal("-inf", Dm.str(float.NegativeInfinity));
        Assert.Equal("nan", Dm.str(float.NaN));
    }

    [Fact]
    public void Num2textUsesSignificantDigitsAndRadixes()
    {
        // BYOND ref /proc/num2text examples; OpenDream test Text/num2text.
        Assert.Equal("12", Dm.num2text(12));
        Assert.Equal("0b", Dm.num2text(11, 2, 16));
        Assert.Equal("1000", Dm.num2text(343, 0, 7));
        Assert.Equal("0999", Dm.num2text(999.23123f, 4, 10));
        Assert.Equal("06b3", Dm.num2text(999.23123f, 4, 12));
        Assert.Equal("1101000", Dm.num2text(999.23123f, 4, 3));
        Assert.Equal("00000000000000012444", Dm.num2text(999.23123f, 20, 5));
        Assert.Equal("14", Dm.num2text(20, 0, 16));
        Assert.Equal("-14", Dm.num2text(-20, 0, 16));
        Assert.Equal("1e+03", Dm.num2text(999.23123f, 2));
        Assert.Equal("999.231201171875", Dm.num2text(999.23123f, 25));
        Assert.Equal("999999", Dm.num2text(999999));
        Assert.Equal("1e+06", Dm.num2text(999999.9f));
        Assert.Equal("1e+06", Dm.num2text(1000000));
        Assert.Equal("1", Dm.num2text(1, 0, 10));
        Assert.Equal("1", Dm.num2text(1, -1, 10));
        Assert.Equal("0", Dm.num2text(0, 0, 16));
        V.Error(() => Dm.num2text(1, 0, 37));
    }

    [Fact]
    public void ConversionsIgnoreTheProcessCulture()
    {
        // rules 3.9: "A shim test sets CultureInfo.CurrentCulture = new("pl-PL") and checks Dm.str(0.5f) == "0.5" and
        // text2num("0.5") == 0.5f". The build runs with invariant globalization, so the test builds a culture with
        // Polish number formats when pl-PL itself is not available.
        CultureInfo saved = CultureInfo.CurrentCulture, savedUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo polish;
            try
            {
                polish = new CultureInfo("pl-PL");
            }
            catch (CultureNotFoundException)
            {
                polish = null;
            }
            if (polish == null || polish.NumberFormat.NumberDecimalSeparator != ",")
            {
                polish = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                polish.NumberFormat.NumberDecimalSeparator = ",";
                polish.NumberFormat.NumberGroupSeparator = " ";
            }
            CultureInfo.CurrentCulture = polish;
            CultureInfo.CurrentUICulture = polish;
            Assert.Equal("0,5", 0.5f.ToString());       // the culture is in effect
            Assert.Equal("0.5", Dm.str(0.5f));
            Assert.Equal(0.5f, Dm.text2num("0.5"));
            Assert.Equal("1.5e+07", Dm.num2text(15000000, 2));
            Assert.Equal("x 2.25 y", Dm.text("x [] y", 2.25f));
            Assert.Equal("[1.5,2]", Dm.json_encode(Dm.list(1.5f, 2f)));
            Assert.Equal(2.5f, Dm.num(Dm.json_decode("2.5")));
            Assert.Equal("0.25", Dm.list(0.25f).Join(","));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
            CultureInfo.CurrentUICulture = savedUi;
        }
    }

    [Fact]
    public void ValuesOfEveryKind()
    {
        // rules 5.2; BYOND ref "text": "An object expression will display the object's name preceded by the text macro
        // \the or \The if no other article has been specified"; OpenDream tests StringInterpolation1-3.
        Assert.Equal("", Dm.str(null));
        Assert.Equal("text", Dm.str("text"));
        Assert.Equal("/list", Dm.str(typeof(DmList)));
        Assert.Equal("/list", Dm.str(Dm.list(1f)));
        Assert.Equal("/matrix", Dm.str(Dm.matrix()));
        Assert.Equal("/datum/values_test_plain", Dm.str(new PlainDatum()));
        Assert.Equal("The cat", Dm.str(new TestAtom("cat")));
        Assert.Equal("Bob", Dm.str(new TestAtom("Bob")));
        Assert.Equal("You see the cat.", Dm.text("You see [].", new TestAtom("cat")));
        Assert.Equal("The cat: hi everybody!", Dm.text("[]: []", new TestAtom("cat"), "hi everybody!"));
        Assert.Equal("Bill: hi everybody!", Dm.text("[]: []", new TestAtom("Bill"), "hi everybody!"));
        Assert.Equal("Nobody likes Blombo!", Dm.text("Nobody likes []!", new NamedDatum("Blombo")));
        Assert.Equal("World finished in 0.5 seconds!", Dm.text("World finished in []!", "0.5 seconds"));
        Assert.Equal("overloaded text", Dm.str(new TextOverloadDatum()));
        Assert.Equal("a overloaded text b", Dm.text("a [] b", new TextOverloadDatum()));
    }

    [Fact]
    public void TemplatesTakeTheirArgumentsInOrder()
    {
        // BYOND ref /proc/text: "a set of arguments that corresponds to the number of empty embedded expressions";
        // OpenDream test HoleEmbeddedExpression.
        Assert.Equal("TEST", Dm.text("[]", "TEST"));
        Assert.Equal("TEST", Dm.text("[ ]", "TEST"));
        Assert.Equal("1 is 2", Dm.text("[] is []", 1, 2));
        Assert.Equal("1 is ", Dm.text("[] is []", 1));     // a missing argument is null
        Assert.Equal("only", Dm.text("only", "extra"));
        Assert.Equal("", Dm.text(null));
        Assert.Equal("cooldown_3", Dm.text("cooldown_[]", 3));
        Assert.Equal("/list (x)", Dm.text("[] ([])", typeof(DmList), "x"));
    }

    [Fact]
    public void EscapesInsertCharacters()
    {
        // BYOND ref "macros (text)"; OpenDream tests NestedEscapedBracket, Expression/String/unicode, dotdotdot.
        Assert.Equal("a\nb\tc", Dm.text("a\\nb\\tc"));
        Assert.Equal("say \"hi\" \\ ok", Dm.text("say \\\"hi\\\" \\\\ ok"));
        Assert.Equal("[x]", Dm.text("\\[x\\]"));
        Assert.Equal("[", Dm.text("[]", "["));
        Assert.Equal("&lt;b&gt;", Dm.text("\\<b\\>"));
        Assert.Equal("U", Dm.text("\\x55"));
        Assert.Equal("ᅕ", Dm.text("\\u1155"));
        Assert.Equal("ሴ56", Dm.text("\\u123456"));
        Assert.Equal("done", Dm.text("done\\..."));
        Assert.Equal("a b", Dm.text("a\\ b"));
        Assert.Equal("\nfoo", Dm.text("\\nfoo"));
    }

    [Fact]
    public void ArticleMacros()
    {
        // OpenDream test StringInterpolation7 (\a on datums and texts); BYOND ref example "[usr] finds \himself \an [O].".
        Assert.Equal("a thing", Dm.text("\\a []", new NamedDatum("thing")));
        Assert.Equal("Thing", Dm.text("\\a []", new NamedDatum("Thing")));
        Assert.Equal("thing", Dm.text("\\a []", new NamedDatum(Dm.text("\\proper thing"))));
        Assert.Equal("some things", Dm.text("\\a []", new NamedDatum("things", Dm.PLURAL)));
        Assert.Equal("A thing", Dm.text("\\A []", new NamedDatum("thing")));
        Assert.Equal("Thing", Dm.text("\\A []", new NamedDatum("Thing")));
        Assert.Equal("Some things", Dm.text("\\A []", new NamedDatum("things", Dm.PLURAL)));
        Assert.Equal("a thing", Dm.text("\\a []", "thing"));
        Assert.Equal("Thing", Dm.text("\\a []", "Thing"));
        Assert.Equal("thing", Dm.text("\\a []", Dm.text("\\proper thing")));
        Assert.Equal("A thing", Dm.text("\\A []", "thing"));
        Assert.Equal("", Dm.text("\\a []", 123));
        Assert.Equal("", Dm.text("\\A []", 123));
        Assert.Equal("an apple", Dm.text("\\an []", new TestAtom("apple")));
        Assert.Equal("an apple", Dm.text("\\a []", new TestAtom("apple")));
        Assert.Equal("the cat", Dm.text("\\the []", new TestAtom("cat")));
        Assert.Equal("The cat", Dm.text("\\The []", new TestAtom("cat")));
        Assert.Equal("Bob", Dm.text("\\the []", new TestAtom("Bob")));
        var bob = new TestAtom("Bob") { gender = Dm.MALE };
        Assert.Equal("Bob finds himself an apple.", Dm.text("[] finds \\himself \\an [].", bob, new TestAtom("apple")));
    }

    [Fact]
    public void ProperAndImproperMarkers()
    {
        // BYOND ref "macros (text)": name = "\improper CPU" "prevent[s] capitalization from causing proper-noun
        // behavior"; rules 5.4: the shim stores the markers as private-use characters that display code strips.
        string cpu = Dm.text("\\improper CPU");
        Assert.Equal("CPU", cpu);
        Assert.Equal("CPU", cpu.Substring(1));
        Assert.Equal("The CPU", Dm.str(new TestAtom(cpu)));
        Assert.Equal("a CPU", Dm.text("\\a []", new TestAtom(cpu)));
        Assert.Equal("thing", Dm.text("\\proper   thing"));
        Assert.Equal("thing", Dm.str(new TestAtom(Dm.text("\\proper thing"))));
        Assert.Equal(1f, Dm.cmptext(cpu, "cpu"));    // cmptext ignores \proper and \improper
    }

    [Fact]
    public void PronounMacrosFollowThePreviousGender()
    {
        // BYOND ref "macros (text)": \he, \his, \him, \himself, \hers by the previous []'s gender (BYOND ref
        // /atom/var/gender: "neuter", "male", "female", "plural").
        TestAtom With(string gender) => new TestAtom("X") { gender = gender };
        Assert.Equal("X he his him himself his", Dm.text("[] \\he \\his \\him \\himself \\hers", With(Dm.MALE)));
        Assert.Equal("X she her her herself hers", Dm.text("[] \\she \\his \\him \\herself \\hers", With(Dm.FEMALE)));
        Assert.Equal("X they their them themself theirs", Dm.text("[] \\he \\his \\him \\himself \\hers", With(Dm.PLURAL)));
        Assert.Equal("X it its it itself its", Dm.text("[] \\he \\his \\him \\himself \\hers", With(Dm.NEUTER)));
        Assert.Equal("X He His His", Dm.text("[] \\He \\His \\Hers", With(Dm.MALE)));
        Assert.Equal("X She Her Hers", Dm.text("[] \\She \\His \\Hers", With(Dm.FEMALE)));
        Assert.Equal("x  ", Dm.text("[] \\he \\his", "x"));   // text has no gender
    }

    [Fact]
    public void PluralAndOrdinalSuffixes()
    {
        // OpenDream tests StringInterpolation4 (\s) and StringInterpolation5 (\th); BYOND ref example "The [DayCount]\th
        // day dawns.".
        Assert.Equal("1s", Dm.text("[]\\s", "1"));
        Assert.Equal("0s", Dm.text("[]\\s", 0));
        Assert.Equal("s", Dm.text("[]\\s", (object)null));
        Assert.Equal("1", Dm.text("[]\\s", 1));
        Assert.Equal("1", Dm.text("[]\\s", 1.00000001f));
        Assert.Equal("1.0001s", Dm.text("[]\\s", 1.0001f));
        Assert.Equal("0th", Dm.text("[]\\th", 0));
        Assert.Equal("1st", Dm.text("[]\\th", 1));
        Assert.Equal("2nd", Dm.text("[]\\th", 2));
        Assert.Equal("3rd", Dm.text("[]\\th", 3));
        Assert.Equal("4th", Dm.text("[]\\th", 4));
        Assert.Equal("-1th", Dm.text("[]\\th", -1));
        Assert.Equal("4th", Dm.text("[]\\th", 4.52f));
        Assert.Equal("the fitness 1st is a", Dm.text("the fitness []\\th is a", 1.7f));
        Assert.Equal("the fitness 100000000th is a", Dm.text("the fitness []\\th is a", 99999999f));
        Assert.Equal("0th", Dm.text("[]\\th", (object)null));
        Assert.Equal("0th", Dm.text("[]\\th", new PlainDatum()));
        Assert.Equal("0th", Dm.text("[]\\th", "bar"));
        Assert.Equal("The 21st day dawns.", Dm.text("The []\\th day dawns.", 21));
        Assert.Equal("11th 12th 13th 22nd", Dm.text("[]\\th []\\th []\\th []\\th", 11, 12, 13, 22));
    }

    [Fact]
    public void RomanNumerals()
    {
        // OpenDream test StringInterpolation6.
        Assert.Equal("i", Dm.text("\\roman[]", 1.5f));
        Assert.Equal(" i", Dm.text("\\roman []", 1.5f));
        Assert.Equal("I", Dm.text("\\Roman[]", 1.5f));
        Assert.Equal(" I", Dm.text("\\Roman []", 1.5f));
        Assert.Equal(" shitposts i", Dm.text("\\roman shitposts []", 1));
        Assert.Equal(" shitposts i  3s", Dm.text("\\roman shitposts [] \\the [] []\\s", 1, 2, 3));
        Assert.Equal("inf", Dm.text("\\roman[]", float.PositiveInfinity));
        Assert.Equal("-inf", Dm.text("\\roman[]", float.NegativeInfinity));
        Assert.Equal(" -inf", Dm.text("\\roman []", float.NegativeInfinity));
        Assert.Equal("-", Dm.text("\\roman[]", float.NaN));
        Assert.Equal("mcmxcix", Dm.text("\\roman[]", 1999));
    }

    [Fact]
    public void RefMacroInsertsTheReferenceText()
    {
        // BYOND ref "\ref text macro": "inserts a unique identification number or text string for the following
        // embedded object"; object-model.md section 3: "[0x" + dm_id in lowercase hex + "]" (Dm.@ref).
        var w = new Anomaly.Core.Kernel.World(1);
        using (w.Enter())
        {
            for (int i = 0; i < 25; i++) w.AllocateId();
            var datum = new TestDatum().Register<TestDatum>(w);
            Assert.Equal("<a href=?src=[0x1a];action=start>here</a>", Dm.text("<a href=?src=\\ref[];action=start>here</a>", datum));
            Assert.Equal("[0x1a] and 5s", Dm.text("\\ref[] and []\\s", datum, 5));   // \ref does not count for \s
        }
    }

    [Fact]
    public void I18nTReturnsTheSameEnglishText()
    {
        // rules 5.3: I18n.T returns the same English string that Dm.text would.
        var cat = new TestAtom("cat");
        Assert.Equal(Dm.text("You start prying [] with []...", cat, "a crowbar"), I18n.T("You start prying [] with []...", cat, "a crowbar"));
        Assert.Equal("You start prying the cat with a crowbar...", I18n.T("You start prying [] with []...", cat, "a crowbar"));
        Assert.Equal("can't reach!", I18n.T("can't reach!"));
        Assert.Equal("broken CPU", I18n.T("broken []", "CPU"));
    }

    [Fact]
    public void NumberTextAtTheEdgesOfFloat()
    {
        // %g with six significant digits outside the whole numbers 1e6..2^24 (rules 5.5; OpenDream DreamValue.Stringify
        // is ToString("g6"), C's %g gives the same digits): negative zero keeps its sign as %g writes it, the largest
        // float and the subnormals use the exponent form, a literal above 2^24 is already the nearest float.
        Assert.Equal("-0", Dm.str(-0f));
        Assert.Equal("0", Dm.str(0f));
        Assert.Equal("-0", Dm.num2text(-0f));
        Assert.Equal("3.40282e+38", Dm.str(float.MaxValue));
        Assert.Equal("-3.40282e+38", Dm.str(float.MinValue));
        Assert.Equal("1.17549e-38", Dm.str(1.17549435e-38f));
        Assert.Equal("1.4013e-45", Dm.str(float.Epsilon));
        Assert.Equal("1e-05", Dm.str(0.00001f));
        Assert.Equal("2.5e-07", Dm.str(0.00000025f));
        Assert.Equal("100000", Dm.str(100000f));
        Assert.Equal("999999", Dm.str(999999f));
        Assert.Equal("1234567", Dm.str(1234567f));
        Assert.Equal("16777216", Dm.str(16777217f));
        Assert.Equal("3.35544e+07", Dm.str(33554432f));
        Assert.Equal("0.1", Dm.str(0.1f));
        Assert.Equal("-0.5", Dm.str(-0.5f));
        Assert.Equal("1e+20", Dm.num2text(1e20f));
        Assert.Equal("1.23457e+06", Dm.num2text(1234567f));
        Assert.Equal("1234567", Dm.num2text(1234567f, 7));
    }

    [Fact]
    public void ConversionsIgnoreAHostileProcessCulture()
    {
        // rules 3.9: every number conversion is culture-invariant, also under a culture that changes the decimal and
        // group separators, the minus sign and the names of the special numbers.
        CultureInfo saved = CultureInfo.CurrentCulture, savedUi = CultureInfo.CurrentUICulture;
        try
        {
            var hostile = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            hostile.NumberFormat.NumberDecimalSeparator = ",";
            hostile.NumberFormat.NumberGroupSeparator = ".";
            hostile.NumberFormat.NegativeSign = "\u2212";
            hostile.NumberFormat.PositiveSign = "#";
            hostile.NumberFormat.NaNSymbol = "NaN?";
            hostile.NumberFormat.PositiveInfinitySymbol = "oo";
            hostile.NumberFormat.NegativeInfinitySymbol = "-oo";
            CultureInfo.CurrentCulture = hostile;
            CultureInfo.CurrentUICulture = hostile;
            Assert.Equal("\u22121,5", (-1.5f).ToString());   // the culture is in effect
            Assert.Equal("-1.5", Dm.str(-1.5f));
            Assert.Equal("-1e-07", Dm.num2text(-1e-7f));
            Assert.Equal("1e+07", Dm.num2text(1e7f));
            Assert.Equal("1.5e+07", Dm.num2text(15000000, 2));
            Assert.Equal("-ff", Dm.num2text(-255, 0, 16));
            Assert.Equal("nan inf -inf", Dm.text("[] [] []", float.NaN, float.PositiveInfinity, float.NegativeInfinity));
            Assert.Equal(-1.5f, Dm.text2num("-1.5"));
            Assert.Equal(1f, Dm.text2num("1,5"));
            Assert.Equal("[-1.5,0.25]", Dm.json_encode(Dm.list(-1.5f, 0.25f)));
            Assert.Equal(-1.5f, Dm.num(Dm.json_decode("-1.5")));
            Assert.Equal("-0.25;1000000", Dm.list(-0.25f, 1000000f).Join(";"));
            Assert.Equal("#ff8000", Dm.rgb(255, 128, 0));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
            CultureInfo.CurrentUICulture = savedUi;
        }
    }
}
