using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Boot;

/// <summary>
/// The configuration (code/controllers/configuration/configuration.dm, config_entry.dm, entries/general.dm), written from
/// the DM source: entry creation and defaults, Get and Set with their abstract and missing-entry CRASHes, the value parsing
/// of each entry kind, and file loading over the host file seam (includes, locks, resets, deprecations, the legacy files).
/// </summary>
public class ConfigurationTests
{
    private static object Get<T>() => config.Get(typeof(T));

    private static object Set<T>(object value) => Waiting.NoSleep(config.Set(typeof(T), value));

    private static datum_config_entry Entry(string name) => (datum_config_entry)config.entries[name];

    private static object[] Items(object list) => Dm.each(list).Cast<object>().ToArray();

    [Fact]
    public void LoadingWithoutFilesGivesTgsDefaults()
    {
        using var w = new BootPieces();
        datum_controller_configuration cfg = w.LoadConfig();

        Assert.True(Dm.truthy(cfg.loaded));
        Assert.Equal("config", cfg.directory);
        Assert.True(Dm.eq(Get<datum_config_entry_number_fps>(), 20));
        Assert.True(Dm.eq(Get<datum_config_entry_number_ticklag>(), 0.5f)); // 10 / initial(fps.default), general.dm:238
        Assert.True(Dm.eq(Get<datum_config_entry_number_lobby_countdown>(), 120));
        Assert.True(Dm.eq(Get<datum_config_entry_number_vote_delay>(), 6000)); // 10 MINUTES
        Assert.True(Dm.eq(Get<datum_config_entry_number_drift_dump_threshold>(), 40)); // 4 SECONDS
        Assert.True(Dm.eq(Get<datum_config_entry_number_mc_tick_rate_high_pop_mc_tick_rate>(), 1.1f));
        Assert.True(Dm.eq(Get<datum_config_entry_number_rounds_until_hard_restart>(), -1));
        Assert.True(Dm.eq(Get<datum_config_entry_flag_log_as_human_readable>(), TRUE));
        Assert.True(Dm.eq(Get<datum_config_entry_flag_log_game>(), FALSE));
        Assert.True(Dm.eq(Get<datum_config_entry_flag_resume_after_initializations>(), FALSE));
        Assert.Equal("Game Master", Get<datum_config_entry_string_autoadmin_rank>());
        Assert.Equal("", Get<datum_config_entry_string_servername>());
        Assert.Null(Get<datum_config_entry_number_soft_popcap>()); // default = null
        // auto_lag_switch_pop overrides config_entry_value, not default: New's set_default (config_entry.dm:30) resets it
        // to the number default 0.
        Assert.True(Dm.eq(Get<datum_config_entry_number_auto_lag_switch_pop>(), 0));
        // A list default is copied into the value (config_entry.dm:43-45), so each entry has its own list.
        Assert.Empty(Items(Get<datum_config_entry_str_list_motd>()));
        Assert.NotSame(Entry("motd").@default, Get<datum_config_entry_str_list_motd>());
        Assert.Empty(Items(Get<datum_config_entry_number_list_ignored_cids>()));
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void EveryConcreteEntryTypeGetsOneEntryNamedAfterItsLastPathSegment()
    {
        using var w = new BootPieces();
        datum_controller_configuration cfg = w.LoadConfig();

        object[] concrete = Items(valid_subtypesof(typeof(datum_config_entry)));
        Assert.Equal(concrete.Length, cfg.entries.Count);
        Assert.Equal(concrete.Length, cfg.entries_by_type.Count);
        foreach (Type type in concrete)
        {
            var entry = (datum_config_entry)cfg.entries_by_type[type];
            Assert.Equal(type, entry.type);
            Assert.Same(entry, cfg.entries[entry.name]);
            Assert.Equal(DmTypes.PathOf(type)[(DmTypes.PathOf(type).LastIndexOf('/') + 1)..].ToLowerInvariant(), entry.name);
        }
        // The abstract kinds (abstract_type == their own type) are not entries.
        foreach (Type abstractType in new[] { typeof(datum_config_entry), typeof(datum_config_entry_string), typeof(datum_config_entry_number), typeof(datum_config_entry_flag), typeof(datum_config_entry_str_list), typeof(datum_config_entry_number_list), typeof(datum_config_entry_keyed_list), typeof(datum_config_entry_number_mc_tick_rate) })
            Assert.Null(cfg.entries_by_type[abstractType]);
        Assert.Same(cfg.entries["base_mc_tick_rate"], cfg.entries_by_type[typeof(datum_config_entry_number_mc_tick_rate_base_mc_tick_rate)]);
        // protection and its saved default (config_entry.dm:29)
        Assert.True(Dm.eq(Entry("autoadmin").protection, CONFIG_ENTRY_LOCKED));
        Assert.True(Dm.eq(Entry("invoke_youtubedl").protection, CONFIG_ENTRY_LOCKED | CONFIG_ENTRY_HIDDEN));
        Assert.True(Dm.eq(Entry("invoke_youtubedl").default_protection, CONFIG_ENTRY_LOCKED | CONFIG_ENTRY_HIDDEN));
        Assert.True(Dm.eq(Entry("servername").protection, NONE));
    }

    [Fact]
    public void GetAndSetOfAnAbstractEntryCrash()
    {
        using var w = new BootPieces();
        w.LoadConfig();

        var get = Assert.Throws<DmCrashException>(() => Get<datum_config_entry_number>());
        Assert.Equal("Tried to retrieve an abstract config_entry: /datum/config_entry/number", get.Message);
        Assert.Equal("code/controllers/configuration/configuration.dm", get.DmFile);
        Assert.Equal(305, get.DmLine);
        Assert.Throws<DmCrashException>(() => Get<datum_config_entry_number_mc_tick_rate>());
        var set = Assert.Throws<DmCrashException>(() => Set<datum_config_entry_flag>(1));
        Assert.Equal("Tried to set an abstract config_entry: /datum/config_entry/flag", set.Message);
    }

    [Fact]
    public void GetAndSetOfARemovedEntryCrash()
    {
        using var w = new BootPieces();
        w.LoadConfig();
        datum_config_entry fps = Entry("fps");

        qdel(fps); // config_entry/Destroy removes it from the configuration (config_entry.dm:33)

        Assert.Null(config.entries["fps"]);
        var get = Assert.Throws<DmCrashException>(() => Get<datum_config_entry_number_fps>());
        Assert.Equal("Missing config entry for /datum/config_entry/number/fps!", get.Message);
        Assert.Throws<DmCrashException>(() => Set<datum_config_entry_number_fps>(30));
    }

    [Fact]
    public void LoadingTwiceCrashes()
    {
        using var w = new BootPieces();
        w.LoadConfig();
        var e = Assert.Throws<DmCrashException>(() => Waiting.NoSleep(config.Load(null)));
        Assert.Equal("/datum/controller/configuration/Load() called more than once!", e.Message);
    }

    [Fact]
    public void SetValidatesNumbersAndKeepsFpsAndTicklagInStep()
    {
        using var w = new BootPieces();
        w.LoadConfig();

        Assert.True(Dm.eq(Set<datum_config_entry_number_fps>(25), TRUE));
        Assert.True(Dm.eq(Get<datum_config_entry_number_fps>(), 25));
        Assert.True(Dm.eq(Get<datum_config_entry_number_ticklag>(), 0.4f)); // fps/ValidateAndSet sets ticklag to 10 / fps

        Assert.True(Dm.eq(Set<datum_config_entry_number_ticklag>("0.25"), TRUE));
        Assert.True(Dm.eq(Get<datum_config_entry_number_fps>(), 40)); // and back
        Assert.True(Dm.eq(Get<datum_config_entry_number_ticklag>(), 0.25f));

        Assert.True(Dm.eq(Set<datum_config_entry_number_ticklag>("0"), FALSE)); // text2num(str_val) > 0 && ..()
        Assert.True(Dm.eq(Set<datum_config_entry_number_fps>("fast"), FALSE)); // not a number
        Assert.True(Dm.eq(Get<datum_config_entry_number_fps>(), 40));

        Assert.True(Dm.eq(Set<datum_config_entry_number_inactivity_period>("30"), TRUE));
        Assert.True(Dm.eq(Get<datum_config_entry_number_inactivity_period>(), 300)); // documented as seconds
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void SetClampsAndRoundsNumbers()
    {
        // A changed value is logged with log_config (the logging package): reported, not thrown, until the merge.
        using var w = new BootPieces(notTranslatedThrows: false);
        w.LoadConfig();

        Assert.True(Dm.eq(Set<datum_config_entry_number_fps>(1000), TRUE));
        Assert.True(Dm.eq(Get<datum_config_entry_number_fps>(), 100)); // max_val
        Assert.True(Dm.eq(Set<datum_config_entry_number_fps>(0), TRUE));
        Assert.True(Dm.eq(Get<datum_config_entry_number_fps>(), 1)); // min_val
        Assert.True(Dm.eq(Set<datum_config_entry_number_error_limit>("12.7"), TRUE));
        Assert.True(Dm.eq(Get<datum_config_entry_number_error_limit>(), 12)); // integer: round() floors
        Assert.True(Dm.eq(Set<datum_config_entry_number_ipintel_rating_bad>("0.5"), TRUE));
        Assert.True(Dm.eq(Get<datum_config_entry_number_ipintel_rating_bad>(), 0.5f)); // integer = FALSE
    }

    [Fact]
    public void SetParsesFlagsStringsAndLists()
    {
        using var w = new BootPieces();
        w.LoadConfig();

        Set<datum_config_entry_flag_log_game>("1");
        Assert.True(Dm.eq(Get<datum_config_entry_flag_log_game>(), TRUE));
        Set<datum_config_entry_flag_log_game>(0);
        Assert.True(Dm.eq(Get<datum_config_entry_flag_log_game>(), FALSE));
        Set<datum_config_entry_flag_log_game>("yes"); // text2num("yes") is null, and null != 0
        Assert.True(Dm.eq(Get<datum_config_entry_flag_log_game>(), TRUE));

        Set<datum_config_entry_string_servername>("  Test Station  ");
        Assert.Equal("Test Station", Get<datum_config_entry_string_servername>()); // auto_trim

        Set<datum_config_entry_str_list_motd>("motd.txt");
        Set<datum_config_entry_str_list_motd>(" extra.txt ");
        Set<datum_config_entry_str_list_motd>("");
        Assert.Equal(new object[] { "motd.txt", "extra.txt" }, Items(Get<datum_config_entry_str_list_motd>()));

        Assert.True(Dm.eq(Set<datum_config_entry_number_list_ignored_cids>("1 2 3"), TRUE));
        Assert.True(Dm.eq(Set<datum_config_entry_number_list_ignored_cids>("4 x"), FALSE));
        object[] cids = Items(Get<datum_config_entry_number_list_ignored_cids>());
        Assert.Equal(3, cids.Length);
        Assert.True(Dm.eq(cids[0], 1) && Dm.eq(cids[1], 2) && Dm.eq(cids[2], 3));

        Assert.True(Dm.eq(Set<datum_config_entry_flag_allow_respawn>("2"), TRUE));
        Assert.True(Dm.eq(Get<datum_config_entry_flag_allow_respawn>(), RESPAWN_FLAG_NEW_CHARACTER));
        Assert.True(Dm.eq(Set<datum_config_entry_flag_allow_respawn>("5"), FALSE));

        Assert.True(Dm.eq(Set<datum_config_entry_string_panic_server_name>("[Put the name here]"), FALSE));
        Assert.True(Dm.eq(Set<datum_config_entry_string_ipintel_email>("ch@nge.me"), FALSE));
        Assert.True(Dm.eq(Set<datum_config_entry_string_ipintel_email>("nobody"), 0)); // findtext(str_val, "@") is 0
        Assert.True(Dm.eq(Set<datum_config_entry_string_ipintel_email>("admin@example.org"), TRUE));
        Assert.Equal("admin@example.org", Get<datum_config_entry_string_ipintel_email>());
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void LoadReadsTheConfigFilesOfTheFileSeam()
    {
        using var w = new BootPieces(notTranslatedThrows: false); // LoadEntries logs with log_config (logging package)
        w.Files.WriteText("config/config.txt", string.Join("\n",
            "# a comment",
            "FPS 25",
            "SERVERNAME   Test Station  ",
            "@LOG_GAME",
            "$include extra.txt",
            "UNKNOWN_SETTING 5",
            "",
            "LOG_LAW 1"));
        w.Files.WriteText("config/extra.txt", "MOTD motd.txt\nROUNDS_UNTIL_HARD_RESTART 3\n$include extra.txt");

        w.LoadConfig();

        Assert.True(Dm.eq(Get<datum_config_entry_number_fps>(), 25));
        Assert.True(Dm.eq(Get<datum_config_entry_number_ticklag>(), 0.4f));
        Assert.Equal("Test Station", Get<datum_config_entry_string_servername>());
        Assert.True(Dm.eq(Get<datum_config_entry_flag_log_game>(), TRUE)); // a flag with no value: text2num("") != 0
        Assert.True(Dm.eq(Entry("log_game").protection, CONFIG_ENTRY_LOCKED)); // '@' locks it
        Assert.True(Dm.eq(Entry("servername").protection, NONE));
        Assert.True(Dm.eq(Get<datum_config_entry_flag_log_silicon>(), TRUE)); // log_law is deprecated by log_silicon
        Assert.True(Dm.eq(Get<datum_config_entry_flag_log_law>(), FALSE));
        Assert.Equal(new object[] { "motd.txt" }, Items(Get<datum_config_entry_str_list_motd>()));
        Assert.True(Dm.eq(Get<datum_config_entry_number_rounds_until_hard_restart>(), 3));
        Assert.Equal("extra.txt", Entry("motd").resident_file);
        Assert.Equal("config.txt", Entry("fps").resident_file);
        Assert.True(Dm.eq(Entry("fps").modified, TRUE));
        Assert.True(Dm.eq(Entry("lobby_countdown").modified, FALSE));
        Assert.True(Dm.eq(config.warned_deprecated_configs, TRUE));
        // extra.txt including itself is caught by the include stack.
        Assert.Contains(Items(datum_controller_configuration.configuration_errors), e => ((string)e).StartsWith("Warning: Config recursion detected", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadEntriesCountsTheFileAndItsIncludes()
    {
        using var w = new BootPieces(notTranslatedThrows: false);
        w.Files.WriteText("config/config.txt", "$include a.txt\n$include b.txt\nFPS 30");
        w.Files.WriteText("config/a.txt", "LOBBY_COUNTDOWN 60");
        w.Files.WriteText("config/b.txt", "");
        w.LoadConfig();

        Assert.True(Dm.eq(Waiting.NoSleep(config.LoadEntries("config.txt")), 3)); // ++. per include and once at the end
        Assert.True(Dm.eq(Waiting.NoSleep(config.LoadEntries("b.txt")), 1));
    }

    [Fact]
    public void AConfigTxtWithoutIncludesLoadsTheLegacyFiles()
    {
        using var w = new BootPieces(notTranslatedThrows: false);
        w.Files.WriteText("config/config.txt", "FPS 30");
        w.Files.WriteText("config/game_options.txt", "LOBBY_COUNTDOWN 60");
        w.Files.WriteText("config/comms.txt", "SERVERNAME Legacy");
        w.LoadConfig();

        Assert.True(Dm.eq(Get<datum_config_entry_number_fps>(), 30));
        Assert.True(Dm.eq(Get<datum_config_entry_number_lobby_countdown>(), 60));
        Assert.Equal("Legacy", Get<datum_config_entry_string_servername>());
    }

    [Fact]
    public void AResetDirectiveRestoresTheDefaultAndADirectoryOverridesTheFolder()
    {
        using var w = new BootPieces(notTranslatedThrows: false);
        w.Files.WriteText("other/config.txt", "$include more.txt\nMOTD a.txt\nMOTD b.txt\n$reset motd\nMOTD c.txt\n@FPS 10\n$reset FPS");
        w.Files.WriteText("other/more.txt", "");
        w.LoadConfig("other");

        Assert.Equal("other", config.directory);
        Assert.Equal(new object[] { "c.txt" }, Items(Get<datum_config_entry_str_list_motd>()));
        // $reset restores the protection too (set_default, config_entry.dm:48), and the fps it reset keeps ticklag's value.
        Assert.True(Dm.eq(Get<datum_config_entry_number_fps>(), 20));
        Assert.True(Dm.eq(Entry("fps").protection, NONE));
        Assert.True(Dm.eq(Get<datum_config_entry_number_ticklag>(), 1));
    }

    [Fact]
    public void FullWipeDeletesTheEntriesAndDestroyClearsTheGlobal()
    {
        using var w = new BootPieces();
        datum_controller_configuration cfg = w.LoadConfig();
        datum_config_entry fps = Entry("fps");

        qdel(cfg);

        Assert.Null(config);
        Assert.Null(cfg.entries);
        Assert.Equal(0, cfg.entries_by_type.Count);
        Assert.True(QDELETED(fps));
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void TheDeferredLoadersAreRecordedAndNothingElseIsSkipped()
    {
        using var w = new BootPieces();
        w.LoadConfig();
        IReadOnlyList<string> sites = DmWorldState.Of(w.World).DeferredSites;
        Assert.Contains("code/controllers/configuration/configuration.dm:378", sites); // loadmaplist (M3)
        Assert.Contains("code/controllers/configuration/configuration.dm:327", sites); // LoadMOTD (M9)
        Assert.Contains("code/controllers/configuration/configuration.dm:366", sites); // LoadPolicy (M9)
        Assert.Contains("code/controllers/configuration/configuration.dm:443", sites); // LoadChatFilter (M9)
        Assert.DoesNotContain("code/controllers/configuration/configuration.dm:539", sites); // validate_job_config: load_jobs_from_txt is off
    }
}
