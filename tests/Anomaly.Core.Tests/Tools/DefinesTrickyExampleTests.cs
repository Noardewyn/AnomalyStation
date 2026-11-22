using Anomaly.Core.Tg;
using Anomaly.Tools.Defines;

namespace Anomaly.Core.Tests.Tools;

/// <summary>
/// The 25 trickiest defines of docs/ai/specs/m0-reading/defines.md section 9 (and its honourable mentions). Expected
/// values are computed by hand from the DM source quoted next to each check; the generated constants are read from
/// the compiled <see cref="Defines"/> class, and what is not a constant from the converter's result for the clone.
/// </summary>
public class DefinesTrickyExampleTests
{
    private static void NotEmitted(string name, string kind)
    {
        DefineResult r = TgClone.Result(name);
        Assert.False(r.Emitted, $"{name} should not be emitted");
        Assert.Equal(kind, r.Kind);
        Assert.False(string.IsNullOrEmpty(r.Reason), $"{name} has no reason");
    }

    private static void Function(string name, params string[] features)
    {
        DefineResult r = TgClone.Result(name);
        Assert.True(r.Definition.IsFunctionLike, $"{name} should be function-like");
        Assert.Equal("function", r.Kind);
        Assert.False(r.Emitted);
        foreach (string feature in features)
            Assert.Contains(feature, r.Features);
    }

    [Fact]
    public void Example01_MessageTypeEntertainment_BodyStartsWithEquals()
    {
        // chat.dm:14  #define MESSAGE_TYPE_ENTERTAINMENT = "entertainment"  (tg typo; defines.md: emit the text with a diagnostic)
        Assert.Equal("entertainment", TgClone.Constant("MESSAGE_TYPE_ENTERTAINMENT"));
    }

    [TgFact]
    public void Example01_MessageTypeEntertainment_CarriesTheTypoNote()
    {
        Assert.Contains(TgClone.Result("MESSAGE_TYPE_ENTERTAINMENT").Notes, n => n.Contains("typo", StringComparison.Ordinal));
    }

    [TgFact]
    public void Example02_TurfLayer_IsPoison()
    {
        // layers.dm:160  #define TURF_LAYER 2 #error TURF_LAYER is no longer supported, please be more specific
        NotEmitted("TURF_LAYER", "poison");
        Assert.False(TgClone.HasConstant("TURF_LAYER"));
    }

    [Fact]
    public void Example03_TimeUnits_AreTheirMultipliers()
    {
        // time.dm:65-76  MILLISECONDS *0.01, DECISECONDS *1, SECONDS *10, MINUTES SECONDS*60, HOURS MINUTES*60,
        // DAYS HOURS*24, YEARS DAYS*365. 1 *10*60 = 600, *60 = 36000, *24 = 864000, *365 = 315360000 (> 2^24: float).
        Assert.Equal(0.01f, TgClone.Constant("MILLISECONDS"));
        Assert.Equal(1, TgClone.Constant("DECISECONDS"));
        Assert.Equal(10, TgClone.Constant("SECONDS"));
        Assert.Equal(600, TgClone.Constant("MINUTES"));
        Assert.Equal(36000, TgClone.Constant("HOURS"));
        Assert.Equal(864000, TgClone.Constant("DAYS"));
        Assert.Equal(315360000f, TgClone.Constant("YEARS"));
        // time.dm:78  TICKS *world.tick_lag is a run-time value, never a constant (translation-rules.md 11.2).
        Assert.False(TgClone.HasConstant("TICKS"));
    }

    [TgFact]
    public void Example03_Ticks_IsARunTimeUnit()
    {
        DefineResult r = TgClone.Result("TICKS");
        Assert.Equal("unit", r.Kind);
        Assert.False(r.Emitted);
        Assert.Contains("world", r.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Example04_UnitChaining()
    {
        // movement.dm:143  INERTIA_FORCE_CAP 25 NEWTONS  with  movement.dm:139 NEWTONS *1  ->  25 *1 = 25
        Assert.Equal(25, TgClone.Constant("INERTIA_FORCE_CAP"));
        // combat.dm:398  JOULES_PER_DAMAGE (25 KILO JOULES)  with  si.dm:12 KILO * 1e3, power.dm:20 JOULES * JOULE,
        // power.dm:16 JOULE 1  ->  (25 * 1e3 * 1) = 25000
        Assert.Equal(25000, TgClone.Constant("JOULES_PER_DAMAGE"));
        Assert.Equal(1000, TgClone.Constant("KILO"));
    }

    [Fact]
    public void Example05_UnparenthesisedOr_FoldsToTheFullMask()
    {
        // _flags.dm:236  ZAP_DEFAULT_FLAGS ZAP_MOB_STUN | ZAP_MOB_DAMAGE | ZAP_OBJ_DAMAGE  =  (1<<4)|(1<<3)|(1<<2) = 28
        Assert.Equal(28, TgClone.Constant("ZAP_DEFAULT_FLAGS"));
        // _flags.dm:239  ZAP_GIZMO_FLAGS ZAP_MOB_DAMAGE | ZAP_OBJ_DAMAGE = 8|4 = 12
        Assert.Equal(12, TgClone.Constant("ZAP_GIZMO_FLAGS"));
    }

    [TgFact]
    public void Example05_UnparenthesisedBody_IsFlaggedForTheTranslator()
    {
        Assert.Contains(TgClone.Result("ZAP_DEFAULT_FLAGS").Notes, n => n.StartsWith("unparenthesised", StringComparison.Ordinal));
        Assert.DoesNotContain(TgClone.Result("ZAP_OBJ_DAMAGE").Notes, n => n.StartsWith("unparenthesised", StringComparison.Ordinal));
    }

    [Fact]
    public void Example05_TextualExpansion_BindsLikeDm()
    {
        // A define that uses an unparenthesised one is folded with the text DM sees: (1 & 16 | 8 | 4) is
        // ((1 & 16) | 8) | 4 = 12, not 1 & 28 = 0.
        const string dm = "#define ZAP_DEFAULT_FLAGS 16 | 8 | 4\n#define TEST_MASKED (1 & ZAP_DEFAULT_FLAGS)\n";
        Assert.Equal(12, Snippet.Value(dm, "TEST_MASKED"));
    }

    [Fact]
    public void Example06_UnparenthesisedSum()
    {
        // layers.dm:200  TOPDOWN_WATER_LEVEL_LAYER 100 + TOPDOWN_LAYER, BYOND stddef TOPDOWN_LAYER 10000 -> 10100
        Assert.Equal(10100, TgClone.Constant("TOPDOWN_WATER_LEVEL_LAYER"));
        // admin.dm:46  R_EVERYTHING (1<<15)-1 = 32767
        Assert.Equal(32767, TgClone.Constant("R_EVERYTHING"));
    }

    [Fact]
    public void Example07_BitNot_Is24Bit()
    {
        // _flags.dm:1  ALL (~0) -> 2^24 - 1 (BYOND ref "~ operator": 24 bits; OpenDream BitNot masks 0xFFFFFF)
        Assert.Equal(16777215, TgClone.Constant("ALL"));
        // mobs.dm:1035  HEAL_ALL ~(HEAL_ADMIN|HEAL_RESTRAINTS), mobs.dm:989 HEAL_ADMIN (1<<0), mobs.dm:1025
        // HEAL_RESTRAINTS (1<<18): 0xFFFFFF & ~0x40001 = 16777215 - 262145 = 16515070
        Assert.Equal(16515070, TgClone.Constant("HEAL_ALL"));
    }

    [TgFact]
    public void Example08_SendSignal_IsAVariadicMacro() => Function("SEND_SIGNAL", "variadic", "paste");

    [TgFact]
    public void Example09_AddTrait_IsAStatementMacro() => Function("ADD_TRAIT", "statement");

    [Fact]
    public void Example10_LazyAdd_AssignsTheCallersList()
    {
        // __HELPERS/_lists.dm:62 (outside code/__DEFINES, converted here as a snippet)
        DefineResult r = Snippet.Define("#define LAZYADD(L, I) L ||= list(); L += I;\n", "LAZYADD");
        Assert.Equal("function", r.Kind);
        Assert.False(r.Emitted);
        Assert.Contains("statement", r.Features);
        Assert.Equal(new[] { "L", "I" }, r.Definition.Parameters);
    }

    [TgFact]
    public void Example11_QdelNullAndQdelIn_AreStatementMacros()
    {
        Function("QDEL_NULL", "statement");
        // qdel.dm:59 QDEL_IN calls addtimer, subsystems.dm:218 _addtimer(args, file = __FILE__, line = __LINE__):
        // its expansion carries the use site's location (translation-rules.md 11.1 (f)).
        Function("QDEL_IN", "statement", "location");
        Assert.DoesNotContain("location", TgClone.Result("QDEL_NULL").Features);
    }

    [TgFact]
    public void FeaturesOfNestedMacros_AndOfTheCallersDot()
    {
        // cooldowns.dm:77 TIMER_COOLDOWN_START(...) LAZYSET(cd_source.cooldowns, cd_index, addtimer(...)): location.
        Function("TIMER_COOLDOWN_START", "location");
        // vv.dm:56 VV_DROPDOWN_OPTION(href_key, name) . += "...[REF(src)]...": the caller's '.' and src.
        Function("VV_DROPDOWN_OPTION", "dot", "src");
        // admin.dm:66 COORD(src): src is its own parameter, not the caller's.
        Assert.DoesNotContain("src", TgClone.Result("COORD").Features);
    }

    [TgFact]
    public void Example12_InvokeAsync() => Function("INVOKE_ASYNC", "variadic", "paste", "statement");

    [Fact]
    public void Example13_GlobalProc_IsText()
    {
        // callbacks.dm:1  GLOBAL_PROC "some_magic_bullshit"
        Assert.Equal("some_magic_bullshit", TgClone.Constant("GLOBAL_PROC"));
        Assert.False(TgClone.HasConstant("CALLBACK"));
    }

    [TgFact]
    public void Example13_Callback_IsTheStartOfANew() => NotEmitted("CALLBACK", "runtime");

    [TgFact]
    public void Example14_ProcRef_PastesItsArgument() => Function("PROC_REF", "paste");

    [TgFact]
    public void Example15_McTickCheck_ReadsRunTimeState() => NotEmitted("MC_TICK_CHECK", "runtime");

    [TgFact]
    public void Example16_SubsystemDef() => Function("SUBSYSTEM_DEF", "paste", "statement");

    [TgFact]
    public void Example17_GlobalListInit() => Function("GLOBAL_LIST_INIT", "paste", "statement");

    [TgFact]
    public void Example18_DefineBitfield() => Function("DEFINE_BITFIELD", "stringify", "paste");

    [TgFact]
    public void Example19_MappingDirectionalHelpers() => Function("MAPPING_DIRECTIONAL_HELPERS", "paste");

    [TgFact]
    public void Example20_ResolveActiveMovement_IsAStatement() => NotEmitted("RESOLVE_ACTIVE_MOVEMENT", "statement");

    [Fact]
    public void Example21_StringifyingMacroCall_FoldsToText()
    {
        // icon_smoothing.dm:71  S_TURF(num) (#num + ",");  :75  SMOOTH_GROUP_TURF_OPEN S_TURF(0)  ->  ("0" + ",") = "0,"
        Assert.Equal("0,", TgClone.Constant("SMOOTH_GROUP_TURF_OPEN"));
    }

    [TgFact]
    public void Example21_UnwrapSmoothingGroups()
    {
        Function("UNWRAP_SMOOTHING_GROUPS", "paste", "statement");
        Function("S_TURF", "stringify");
    }

    [TgFact]
    public void Example22_StatStartStopwatch_DeclaresAVar() => NotEmitted("STAT_START_STOPWATCH", "statement");

    [TgFact]
    public void Example23_IsBlind_DiscardsItsArguments()
    {
        // status_effects.dm:80  #define is_blind(...) has_status_effect(/datum/status_effect/grouped/blindness)
        Function("is_blind", "variadic");
        Assert.Equal(new[] { "..." }, TgClone.Result("is_blind").Definition.Parameters);
    }

    [TgFact]
    public void Example24_Assert_RedefinesTheBuiltin()
    {
        // assert.dm:1 #undef ASSERT, :4 #define ASSERT(condition, message...)
        Function("ASSERT", "variadic", "stringify", "paste", "location", "statement");
    }

    [TgFact]
    public void Example25_MagazineType_IsAVarOverrideFragment() => NotEmitted("MAGAZINE_TYPE_INCENDIARY", "statement");

    [TgFact]
    public void Honourable_IsEven_CommaSplice_VarFinal_TgsTopic()
    {
        Function("ISEVEN");
        NotEmitted("ID_ICON_BORDERS", "comma_splice");
        NotEmitted("TGS_TOPIC", "statement");
        // spaceman_dmm.dm:56 (SPACEMAN_DMM only) and :71 (production): the active one is "var".
        DefineResult varFinal = TgClone.Result("VAR_FINAL");
        Assert.Equal(71, varFinal.Definition.Line);
        Assert.Equal("statement", varFinal.Kind);
        Assert.Contains(TgClone.Output.Results, r => r.Name == "VAR_FINAL" && r.Kind == "inactive" && r.Definition.Line == 56);
    }
}
