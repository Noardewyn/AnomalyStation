using Anomaly.Tools.Defines;

namespace Anomaly.Core.Tests.Tools;

/// <summary>
/// DM numeric and text semantics the folder must reproduce (docs/ai/specs/m0-reading/defines.md section 10,
/// translation-rules.md 3.1, 4.3, 4.4, 4.11, 5.5, 11.2). Expected values are worked out by hand from the BYOND
/// reference and OpenDream, quoted at each case.
/// </summary>
public class DefinesNumericTrapTests
{
    private static object Fold(string body) => Snippet.Value($"#define TEST_VALUE {body}\n", "TEST_VALUE");

    private static DefineResult Result(string body) => Snippet.Define($"#define TEST_VALUE {body}\n", "TEST_VALUE");

    [Theory]
    // DM '/' is real division (BYOND ref "/ operator"); C# int division would give 0.
    [InlineData("(1 / 64)", 0.015625f)] // lighting.dm:35 LIGHTING_ROUND_VALUE
    [InlineData("(1/4)", 0.25f)]
    [InlineData("(3 / 2)", 1.5f)]
    public void Division_IsReal(string body, float expected) => Assert.Equal(expected, Fold(body));

    [Fact]
    public void Division_RoundsToBinary32()
    {
        // (50 / 100000) and (111 / 100000) (DNA.dm), (1/6) (supermatter.dm): one binary32 division each.
        float fifty = 50f;
        float hundredThousand = 100000f;
        float one = 1f;
        float six = 6f;
        Assert.Equal(fifty / hundredThousand, Fold("(50 / 100000)"));
        Assert.Equal(111f / hundredThousand, Fold("(111 / 100000)"));
        Assert.Equal(one / six, Fold("(1/6)"));
    }

    [Fact]
    public void Division_ThatComesOutWhole_IsAnInt()
    {
        // A whole value within 2^24 is a const int (translation-rules.md 3.1: integer literals and flags are int).
        Assert.Equal(64, Fold("(128 / 2)"));
    }

    [Fact]
    public void DivisionByZero_IsNotAConstant()
    {
        Assert.Equal("error", Result("(1 / 0)").Kind);
    }

    [Theory]
    // '%' truncates both operands to integers, the result has the dividend's sign (translation-rules.md 4.3).
    [InlineData("(7 % 3)", 1)]
    [InlineData("(-7 % 3)", -1)]
    [InlineData("(7 % -3)", 1)]
    [InlineData("(7.9 % 3)", 1)]
    [InlineData("(7 % 2.9)", 1)]
    // '%%' is B * fract(A / B) with the dividend's sign (BYOND ref "%% operator"; defines.md section 10).
    [InlineData("(-180 %% 360)", -180)]
    [InlineData("(-1 %% 3)", -1)]
    [InlineData("(540 %% 360)", 180)]
    public void Modulo(string body, int expected) => Assert.Equal(expected, Fold(body));

    [Fact]
    public void FloatModulo_KeepsTheFraction()
    {
        // 5.5 %% 2 = 2 * fract(2.75) = 2 * 0.75 = 1.5
        Assert.Equal(1.5f, Fold("(5.5 %% 2)"));
    }

    [Theory]
    // '**' binds tighter than '*' and is right-associative (OpenDream ExpressionPower).
    [InlineData("(2 ** 10)", 1024)]
    [InlineData("(2 ** 3 ** 2)", 512)]
    [InlineData("(3 * 2 ** 2)", 12)]
    public void Power(string body, int expected) => Assert.Equal(expected, Fold(body));

    [Theory]
    // 24-bit bit operators (BYOND ref "~ operator", "<< operator"; translation-rules.md 4.4).
    [InlineData("(~0)", 16777215)]
    [InlineData("(~1)", 16777214)]
    [InlineData("(~(1<<23))", 8388607)]
    [InlineData("(1<<23)", 8388608)]
    [InlineData("(0xFFFFFF << 1)", 16777214)]
    [InlineData("(0xFF >> 4)", 15)]
    [InlineData("(5 & 3)", 1)]
    [InlineData("(5 | 3)", 7)]
    [InlineData("(5 ^ 3)", 6)]
    public void BitOperations(string body, int expected) => Assert.Equal(expected, Fold(body));

    [Fact]
    public void ShiftBy24_IsAHardError()
    {
        // BYOND ref "<< shift operator": bits shifted beyond the 24 low bits are lost, so (1<<24) is 0 in DM. tg never
        // shifts that far and a flag that folds to 0 would be a tg bug: the generator refuses to write (defines.md 5.2).
        ConverterOutput output = Snippet.Convert("#define TEST_VALUE (1<<24)\n");
        Assert.NotEmpty(output.Errors);
        Assert.False(output.Find("TEST_VALUE").Emitted);
    }

    [Fact]
    public void ShiftsBindMoreLooselyThanComparisons()
    {
        // DM: 1 << (2 < 3) = 1 << 1 = 2; C would read (1 << 2) < 3 = 0 (translation-rules.md 4.11).
        Assert.Equal(2, Fold("(1 << 2 < 3)"));
    }

    [Fact]
    public void Binary32_RoundsEveryStep()
    {
        // (16777216 + 1) is 16777216 in binary32, so the difference is 0; double arithmetic would give 1.
        Assert.Equal(0, Fold("((16777216 + 1) - 16777216)"));
        // A literal above 2^24 is stored as the nearest binary32 value: 16777217 -> 16777216.
        Assert.Equal(16777216, Fold("16777217"));
    }

    [Fact]
    public void LargeWholeValues_AreFloats()
    {
        // Beyond 2^24 an integer is not exact in DM, so it is a float constant (1e9 = 1000000000 is exact in binary32).
        Assert.Equal(1e9f, Fold("1e9"));
        Assert.Equal(16777216, Fold("16777216"));
    }

    [Theory]
    // Literal values are kept as binary32 (maths.dm:8-11): PI is 3.1416, INFINITY is 1e31, not float.Infinity.
    [InlineData("3.1416", 3.1416f)]
    [InlineData("1e31", 1e31f)]
    [InlineData("2.71828183", 2.71828183f)]
    [InlineData("57.2957795", 57.2957795f)]
    [InlineData("-0.5", -0.5f)]
    public void FloatLiterals(string body, float expected) => Assert.Equal(expected, Fold(body));

    [Fact]
    public void HexLiterals() => Assert.Equal(31, Fold("0x1F"));

    [Theory]
    // Numbers embedded in text: 6 significant digits, %g style (translation-rules.md 5.5).
    [InlineData("\"[1000000]\"", "1e+06")]
    [InlineData("\"[100000]\"", "100000")]
    [InlineData("\"[0.1]\"", "0.1")]
    [InlineData("\"[1.5]\"", "1.5")]
    [InlineData("\"[123456789]\"", "1.23457e+08")]
    [InlineData("\"[1/3]\"", "0.333333")]
    [InlineData("\"[0.0001]\"", "0.0001")]
    [InlineData("\"[0.00001]\"", "1e-05")]
    [InlineData("\"x[NORTH]y\"", "x1y")]
    public void NumbersInText(string body, string expected) => Assert.Equal(expected, Fold(body));

    [Theory]
    // Comparisons give 1 or 0; '&&' and '||' return an operand (BYOND ref "&& operator", "|| operator").
    [InlineData("(1 == TRUE)", 1)]
    [InlineData("(2 == TRUE)", 0)]
    [InlineData("(0 || 7)", 7)]
    [InlineData("(3 && 4)", 4)]
    [InlineData("(0 && 4)", 0)]
    [InlineData("(TRUE ? 5 : 6)", 5)]
    [InlineData("(!0)", 1)]
    [InlineData("(!5)", 0)]
    public void LogicAndComparison(string body, int expected) => Assert.Equal(expected, Fold(body));

    [Fact]
    public void OrReturnsTheFirstTrueText() => Assert.Equal("x", Fold("(\"\" || \"x\")"));

    [Fact]
    public void Null_IsZeroInArithmetic_AndNothingInText()
    {
        // BYOND ref "+ operator": num or null (0); OpenDream Add: null + x is x, "text" + null is "text".
        Assert.Equal(5, Fold("(null + 5)"));
        Assert.Equal("a", Fold("(\"a\" + null)"));
    }

    [Fact]
    public void TextPlusNumber_IsATypeMismatch()
    {
        DefineResult r = Result("(\"a\" + 1)");
        Assert.False(r.Emitted);
        Assert.Equal("error", r.Kind);
    }

    [Fact]
    public void TextConcatenation()
    {
        const string dm = "#define GAS_O2 \"o2\"\n#define TEST_VALUE GAS_O2 + \"=22;TEMP=293.15\"\n";
        Assert.Equal("o2=22;TEMP=293.15", Snippet.Value(dm, "TEST_VALUE"));
    }

    [Fact]
    public void Rgb_GivesLowerCaseHex()
    {
        // BYOND ref "rgb proc": rgb(255,0,128) becomes "#ff0080". colors.dm:534 HUSK_COLOR_TONE rgb(96, 88, 80).
        Assert.Equal("#ff0080", Fold("rgb(255,0,128)"));
        Assert.Equal("#605850", Fold("rgb(96, 88, 80)"));
    }

    [Theory]
    // Builtin calls are not folded (rgb() aside): round(), floor(), min(), ... stay calls for the translator, who
    // writes the shim's round (translation-rules.md 4.6: one-argument round is floor, round(A, B) ties round up).
    [InlineData("round(2.5)")]
    [InlineData("round(5, 2)")]
    [InlineData("max(1, 2)")]
    [InlineData("sqrt(4)")]
    public void BuiltinCalls_AreNotFolded(string body)
    {
        DefineResult r = Result(body);
        Assert.False(r.Emitted);
        Assert.Equal("runtime", r.Kind);
    }

    [Fact]
    public void UnparenthesisedDefines_ExpandTextually()
    {
        // A 1 + 2, B A * 3: DM sees 1 + 2 * 3 = 7, not 9.
        Assert.Equal(7, Snippet.Value("#define A 1 + 2\n#define B A * 3\n", "B"));
    }

    [Fact]
    public void PostfixUnits_KeepTextualPrecedence()
    {
        // translation-rules.md 11.2: x / 8 SECONDS is (x / 8) * 10: 80 / 8 *10 = 100, not 80 / 80 = 1.
        const string dm = "#define SECONDS *10\n#define TEST_VALUE (80 / 8 SECONDS)\n#define HALF (0.25 SECONDS)\n";
        Assert.Equal(100, Snippet.Value(dm, "TEST_VALUE"));
        Assert.Equal(2.5f, Snippet.Value(dm, "HALF"));
        Assert.Equal(10, Snippet.Value(dm, "SECONDS"));
    }

    [Fact]
    public void Units_ChainFromLeftToRight()
    {
        // si.dm:12 KILO * 1e3; power.dm:14-20 WATT 1, JOULE 1, WATTS * WATT, JOULES * JOULE; combat.dm:398.
        const string dm = "#define KILO * 1e3\n#define JOULE 1\n#define JOULES * JOULE\n#define TEST_VALUE (25 KILO JOULES)\n";
        Assert.Equal(25000, Snippet.Value(dm, "TEST_VALUE"));
        Assert.Equal(1000, Snippet.Value(dm, "KILO"));
    }
}
