using Anomaly.Tools.Defines;

namespace Anomaly.Core.Tests.Tools;

/// <summary>
/// The preprocessor: string-aware comments, line splices, doc comments, conditional compilation, #undef and
/// redefinition, function-like macro expansion (BYOND ref "#define directive"; OpenDream DMPreprocessorLexer).
/// </summary>
public class DefinesPreprocessorTests
{
    [Fact]
    public void CommentMarkersInsideText_AreText()
    {
        // vv.dm and admin.dm hold byond:// links in define bodies; a naive '//' strip cuts them.
        const string dm = "#define TEST_LINK \"<a href='byond://?_src_=vars;x=1'>x</a>\" // a comment\n";
        DefineResult r = Snippet.Define(dm, "TEST_LINK");
        Assert.Equal("<a href='byond://?_src_=vars;x=1'>x</a>", r.Value);
        Assert.Equal("a comment", r.Definition.Doc);
    }

    [TgFact]
    public void CommentMarkersInsideText_SurviveInTheClone()
    {
        // admin.dm:62  ADMIN_SET_SD_CODE "(<a href='byond://?_src_=holder;[HrefToken(forceGlobal = TRUE)];set_selfdestruct_code=1'>SETCODE</a>)"
        DefineResult sd = TgClone.Result("ADMIN_SET_SD_CODE");
        Assert.EndsWith("set_selfdestruct_code=1'>SETCODE</a>)\"", sd.Definition.BodyText, StringComparison.Ordinal);
        Assert.Equal("runtime", sd.Kind); // HrefToken() runs at the use site
        // vv.dm:41  VV_HREF_TARGET_INTERNAL(target, href_key) "byond://?_src_=vars;[HrefToken()];[href_key]=TRUE;[VV_HK_TARGET]=[REF(target)]"
        Assert.EndsWith("=[REF(target)]\"", TgClone.Result("VV_HREF_TARGET_INTERNAL").Definition.BodyText, StringComparison.Ordinal);
    }

    [Fact]
    public void BlockComments_Nest()
    {
        const string dm = "/* outer /* inner */ still a comment\n#define HIDDEN 1\n*/\n#define SHOWN 2\n";
        ConverterOutput output = Snippet.Convert(dm);
        Assert.Null(output.Find("HIDDEN"));
        Assert.Equal(2, output.Find("SHOWN").Value);
    }

    [Fact]
    public void DirectivesInsideDocumentStrings_AreText()
    {
        const string dm = "var/x = {\"\n#define NOT_A_DEFINE 1\n\"}\n#define REAL 3\n";
        ConverterOutput output = Snippet.Convert(dm);
        Assert.Null(output.Find("NOT_A_DEFINE"));
        Assert.Equal(3, output.Find("REAL").Value);
    }

    [Fact]
    public void LineSplices_JoinTheBody()
    {
        const string dm = "#define SPLICED (1 + \\\n    2)\n";
        Assert.Equal(3, Snippet.Value(dm, "SPLICED"));
    }

    [Fact]
    public void DocComments_AboveAndTrailing()
    {
        const string dm = "/// Above, line one\n/// line two\n#define DOCUMENTED 1 // trailing\n\n/// separated by a blank line\n\n#define UNDOCUMENTED 2\n/**\n * Block doc\n */\n#define BLOCK_DOC 3\n";
        ConverterOutput output = Snippet.Convert(dm);
        Assert.Equal("Above, line one\nline two\ntrailing", output.Find("DOCUMENTED").Definition.Doc);
        Assert.Null(output.Find("UNDOCUMENTED").Definition.Doc);
        Assert.Equal("Block doc", output.Find("BLOCK_DOC").Definition.Doc);
    }

    [Fact]
    public void ConditionalCompilation_UsesTheProductionConfiguration()
    {
        const string dm =
            "#ifdef UNIT_TESTS\n#define BRANCH 1\n#else\n#define BRANCH 2\n#endif\n" +
            "#ifdef CBT\n#define COMPILING 1\n#endif\n" +
            "#if DM_VERSION >= 515\n#define NEW_BYOND 1\n#elif DM_VERSION >= 500\n#define NEW_BYOND 2\n#else\n#define NEW_BYOND 3\n#endif\n" +
            "#if defined(OPENDREAM) || !defined(DISABLE_DREAMLUAU)\n#define LUA 1\n#endif\n";
        ConverterOutput output = Snippet.Convert(dm);
        Assert.Equal(2, output.Find("BRANCH").Value);
        Assert.Equal(1, output.Find("COMPILING").Value);
        Assert.Equal(1, output.Find("NEW_BYOND").Value);
        Assert.False(output.Find("LUA").Emitted);
        Assert.Equal("inactive", output.Find("LUA").Kind);
        // The skipped definitions are in the manifest with the directive that skipped them.
        DefineResult skipped = output.Results.Single(r => r.Name == "BRANCH" && !r.Definition.Active);
        Assert.Equal("#ifdef UNIT_TESTS", skipped.Definition.InactiveCondition);
        Assert.Equal(2, output.Results.Count(r => r.Name == "NEW_BYOND" && r.Kind == "inactive"));
    }

    [Fact]
    public void Undef_EndsADefinition_AndUsesAfterItSeeNothing()
    {
        // B uses A after A's #undef (the end of the include order): A is not a macro there, so B is not a constant.
        const string dm = "#define A 1\n#define LOCAL_USE (A + 1)\n#undef A\n#define B (A + 1)\n";
        ConverterOutput output = Snippet.Convert(dm);
        Assert.Equal(1, output.Find("A").Value);
        Assert.Contains("#undef", output.Find("A").Notes.Single());
        Assert.Equal("unresolved", output.Find("B").Kind);
        // LOCAL_USE is live at the end too, so it sees no A either.
        Assert.Equal("unresolved", output.Find("LOCAL_USE").Kind);
    }

    [Fact]
    public void Redefinition_AfterUndef_IsEmittedOnce()
    {
        const string dm = "#define TWICE 1\n#undef TWICE\n#define TWICE 2\n";
        ConverterOutput output = Snippet.Convert(dm);
        Assert.Single(output.Results, r => r.Name == "TWICE" && r.Emitted);
        Assert.Equal(2, output.Find("TWICE").Value);
        Assert.Contains(output.Results, r => r.Name == "TWICE" && r.Kind == "superseded");
    }

    [Fact]
    public void Redefinition_WithoutUndef_TheLaterCounts()
    {
        const string dm = "#define AGAIN 1\n#define AGAIN 2\n";
        ConverterOutput output = Snippet.Convert(dm);
        Assert.Equal(2, output.Find("AGAIN").Value);
        Assert.Equal("superseded", output.Results.First(r => r.Name == "AGAIN").Kind);
    }

    [Fact]
    public void LaterDefinitions_AreVisible()
    {
        // DM expands where the macro is used, so a define may name one defined further down.
        Assert.Equal(5, Snippet.Value("#define EARLY (LATE + 1)\n#define LATE 4\n", "EARLY"));
    }

    [Fact]
    public void FunctionLikeMacros_ExpandInsideBodies()
    {
        const string dm =
            "#define DOUBLE(x) ((x) * 2)\n#define USES_DOUBLE DOUBLE(3 + 1)\n" +
            "#define NAME(x) \"pre_[x]\"\n#define USES_NAME NAME(7)\n" +
            "#define PASTE(x) VALUE_##x\n#define VALUE_ONE 11\n#define USES_PASTE PASTE(ONE)\n" +
            "#define TEXT_OF(x) #x\n#define USES_TEXT TEXT_OF(hello)\n" +
            "#define LIST_OF(first, rest...) list(first, ##rest)\n#define USES_LIST LIST_OF(1)\n";
        ConverterOutput output = Snippet.Convert(dm);
        Assert.Equal(8, output.Find("USES_DOUBLE").Value);
        Assert.Equal("pre_7", output.Find("USES_NAME").Value);
        Assert.Equal(11, output.Find("USES_PASTE").Value);
        Assert.Equal("hello", output.Find("USES_TEXT").Value);
        Assert.Equal("list", output.Find("USES_LIST").Kind);
        Assert.Equal("function", output.Find("DOUBLE").Kind);
    }

    [Fact]
    public void FunctionFeatures_FollowTheMacrosTheExpansionNames()
    {
        // translation-rules.md 11.1 (d), (f): the form of a macro depends on what it expands to where it is used, and
        // DM rescans the expansion. subsystems.dm:218 addtimer(args...) is _addtimer(args, file = __FILE__, line =
        // __LINE__), so qdel.dm:61 QDEL_IN_STOPPABLE (shortened here), which calls addtimer, takes the use site's
        // file and line too.
        const string dm =
            "#define addtimer(args...) _addtimer(args, file = __FILE__, line = __LINE__)\n" +
            "#define QDEL_IN_STOPPABLE(item, time) addtimer(CALLBACK(GLOBAL_PROC, GLOBAL_PROC_REF(qdel), item), time, TIMER_STOPPABLE)\n" +
            "#define STATE_CHECK (src.state != 1)\n" +
            "#define USES_STATE(x) (STATE_CHECK && x)\n" +
            // admin.dm:66: a parameter named src is the argument, not the caller's src.
            "#define COORD(src) \"[src ? src.Admin_Coordinates_Readable() : \"nonexistent location\"]\"\n" +
            // vv.dm:56 appends to the caller's return value '.'.
            "#define VV_DROPDOWN_OPTION(href_key, name) . += \"<a href='byond://?_src_=vars;[href_key]=TRUE'>[name]</a>\"\n" +
            "#define MEMBER_AND_PATH(x) (x.y || .proc/z || ..())\n" +
            // A function-like name without '(' is not expanded, so it brings nothing.
            "#define NAMES_ONLY(x) addtimer\n";
        ConverterOutput output = Snippet.Convert(dm);
        Assert.Contains("location", output.Find("addtimer").Features);
        Assert.Contains("location", output.Find("QDEL_IN_STOPPABLE").Features);
        Assert.Contains("src", output.Find("USES_STATE").Features);
        Assert.DoesNotContain("src", output.Find("COORD").Features);
        Assert.Contains("dot", output.Find("VV_DROPDOWN_OPTION").Features);
        Assert.DoesNotContain("dot", output.Find("MEMBER_AND_PATH").Features);
        Assert.Empty(output.Find("NAMES_ONLY").Features);
    }

    [Fact]
    public void AFunctionLikeName_WithoutParentheses_StaysAName()
    {
        Assert.Equal("unresolved", Snippet.Define("#define F(x) (x)\n#define G F\n", "G").Kind);
    }

    [Fact]
    public void SelfReference_Terminates()
    {
        Assert.Equal("unresolved", Snippet.Define("#define LOOP (LOOP + 1)\n", "LOOP").Kind);
    }

    [Fact]
    public void TextEscapes()
    {
        // BYOND ref "macros (text)": \n, \t, \", \\, \[ give the character; \< and \> give &lt; and &gt;;
        // \xNN gives the character with that code; a letter run that is not a macro starts with \n or \t (tg's songs).
        Assert.Equal("a\nb\t\"c\\[]", Snippet.Value("#define T \"a\\nb\\t\\\"c\\\\\\[\\]\"\n", "T"));
        Assert.Equal("&lt;b&gt;", Snippet.Value("#define T \"\\<b\\>\"\n", "T"));
        Assert.Equal("A", Snippet.Value("#define T \"\\x41\"\n", "T"));
        Assert.Equal("\nC4", Snippet.Value("#define T \"\\nC4\"\n", "T"));
    }

    [Fact]
    public void TextMacros_AreNotConstants()
    {
        DefineResult r = Snippet.Define("#define T \"\\improper CPU\"\n", "T");
        Assert.False(r.Emitted);
        Assert.Equal("text_macro", r.Kind);
    }

    [Fact]
    public void RawAndDocumentStrings()
    {
        Assert.Equal("a\\b[c]", Snippet.Value("#define T @\"a\\b[c]\"\n", "T"));
        Assert.Equal("one\ntwo", Snippet.Value("#define T {\"one\ntwo\"}\n", "T"));
    }

    [Fact]
    public void KindsOfNonConstants()
    {
        const string dm =
            "#define L list(1, 2)\n#define P /obj/item\n#define V /obj/item::name\n#define R 'icons/x.dmi'\n" +
            "#define W world.time\n#define S var/x = 1\n#define C 1, 2\n#define E\n#define N null\n";
        ConverterOutput output = Snippet.Convert(dm);
        Assert.Equal("list", output.Find("L").Kind);
        Assert.Equal("typepath", output.Find("P").Kind);
        Assert.Equal("type_var", output.Find("V").Kind);
        Assert.Equal("resource", output.Find("R").Kind);
        Assert.Equal("runtime", output.Find("W").Kind);
        Assert.Equal("statement", output.Find("S").Kind);
        Assert.Equal("comma_splice", output.Find("C").Kind);
        Assert.Equal("empty", output.Find("E").Kind);
        Assert.Equal("null", output.Find("N").Kind);
        Assert.All(output.Results, r => Assert.False(r.Emitted));
        Assert.All(output.Results, r => Assert.False(string.IsNullOrEmpty(r.Reason)));
    }

    [Fact]
    public void SpanHelpers_BecomeStringMethods()
    {
        const string dm =
            "#define span_notice(str) (\"<span class='notice'>\" + str + \"</span>\")\n" +
            "#define span_class(class, str) (\"<span class='[class]'>\" + str + \"</span>\")\n";
        ConverterOutput output = Snippet.Convert(dm);
        DefineResult notice = output.Find("span_notice");
        Assert.True(notice.Emitted);
        Assert.Equal("string span_notice(string str)", notice.CsSignature);
        Assert.Equal("\"<span class='notice'>\" + str + \"</span>\"", notice.CsValue);
        // An embedded expression formats its argument like DM text, which a string concatenation would not.
        Assert.False(output.Find("span_class").Emitted);
        Assert.Contains("public static partial class GlobalProcs", output.Files["__DEFINES/snippet.g.cs"]);
    }

    [Fact]
    public void Lexer_KeepsCommentsSeparateFromText()
    {
        List<Token> tokens = DmLexer.Lex("x = \"a // b\" // c\n");
        Assert.Contains(tokens, t => t.Kind == TokenKind.String && t.Text == "\"a // b\"");
        Assert.Contains(tokens, t => t.Kind == TokenKind.Comment && t.CommentBody == " c");
    }
}
