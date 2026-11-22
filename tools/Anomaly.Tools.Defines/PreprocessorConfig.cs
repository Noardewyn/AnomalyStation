namespace Anomaly.Tools.Defines;

/// <summary>
/// The compile configuration the converter preprocesses tg with (translation-rules.md 1.7): tg's production
/// build. <c>code/_compile_options.dm</c> and the <c>.dme</c>'s own <c>#define</c>s are read from the files as
/// shipped; this class only holds what the compiler itself predefines.
/// </summary>
public sealed class PreprocessorConfig
{
    /// <summary>
    /// <c>DM_VERSION</c>: the BYOND major version tg builds with (<c>dependencies.sh</c>: <c>BYOND_MAJOR=516</c>;
    /// BYOND ref "DM_VERSION macro").
    /// </summary>
    public int DmVersion { get; init; } = 516;

    /// <summary>
    /// <c>DM_BUILD</c>: the BYOND minor version tg builds with (<c>dependencies.sh</c>: <c>BYOND_MINOR=1685</c>;
    /// BYOND ref "DM_BUILD macro"). <c>__byond_version_compat.dm</c> only rejects builds below 1659 and 1660 itself.
    /// </summary>
    public int DmBuild { get; init; } = 1685;

    /// <summary>
    /// Symbols defined as if by <c>-D</c> on the command line: <c>CBT</c>, which tg's own build passes to every
    /// production compile (<c>tools/build/build.ts:229</c>, <c>defines: ['CBT', ...]</c>; a TGS build gets it from
    /// <c>_compile_options.dm:188-191</c>) and which selects the compile-time side of <c>MAP_SWITCH</c>,
    /// <c>WHEN_MAP</c> and <c>WHEN_COMPILE</c> (<c>__DEFINES/map_switch.dm</c>); and, by translation-rules.md 1.7,
    /// <c>DISABLE_DREAMLUAU</c> (the Lua admin scripting is not ported; tg defines it itself for OpenDream,
    /// <c>_compile_options.dm:133</c>). Not defined, as in a production build: <c>OPENDREAM</c>,
    /// <c>SPACEMAN_DMM</c>, <c>CIBUILDING</c>, <c>TGS</c>, <c>UNIT_TESTS</c>, <c>TESTING</c> and the other debug
    /// switches.
    /// </summary>
    public IReadOnlyList<string> CommandLineSymbols { get; init; } = new[] { "CBT", "DISABLE_DREAMLUAU" };

    public static PreprocessorConfig Production { get; } = new();
}
