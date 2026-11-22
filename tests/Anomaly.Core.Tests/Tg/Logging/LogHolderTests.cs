using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Logging;

/// <summary>
/// The log holder (code/modules/logging/log_holder.dm), its categories and entries (log_category.dm, log_entry.dm,
/// categories/*.dm), written from the DM source: Log queues until init_logging (:297-298), init_logging builds the
/// category tree, disables the categories whose config flag is off, writes each category's file header over the file
/// seam and replays the queue (:147-149); entries go to the readable and the json file of their master category.
/// </summary>
public class LogHolderTests
{
    private const string Dir = LoggingTestWorld.LogDirectory;

    // An entry's json line holds world.get_world_state_for_logging() (log_entry.dm:90), a /world proc of the boot package;
    // until it lands the tests run without throwing for it and do not read that field.
    private static void AllowWorldStateSkeleton() => DmWorldState.Current.NotTranslatedThrows = false;

    private static IEnumerable<Exception> OwnRuntimes(LoggingTestWorld t)
        => t.Runtimes.Where(e => e is not DmNotTranslatedException n || n.Where != "code/game/world.dm:219");

    [Fact]
    public void LogQueuesBeforeInitLoggingAndInitLoggingFlushesTheQueueInOrder()
    {
        var t = new LoggingTestWorld();
        using (t.World.Enter())
        {
            AllowWorldStateSkeleton();
            t.Setup(round_id: 7);
            log_game("first");
            log_world("second");
            log_game("third", Dm.list(Dm.kv("who", "bob")));
            Assert.Equal(3f, logger.waiting_log_calls.len);
            Assert.Null(t.Read(Dir + "/game.log"));

            logger.init_logging();

            Assert.Equal(1f, logger.initialized);
            Assert.Equal(0f, logger.waiting_log_calls.len); // waiting_log_calls?.Cut() (:149)
            Assert.Equal(7, Dm.num(logger.round_id));
            // human_readable_enabled = CONFIG_GET(flag/log_as_human_readable) (:122): every flag of the fixture is TRUE.
            Assert.Equal(1f, logger.human_readable_enabled);

            var game = (datum_log_category)logger.log_categories[LOG_CATEGORY_GAME];
            Assert.Equal(new object[] { "first", "third" }, Dm.each(game.entries).Cast<datum_log_entry>().Select(e => e.message).ToArray());
            Assert.Equal(2f, game.entry_count);
            var runtime = (datum_log_category)logger.log_categories[LOG_CATEGORY_RUNTIME];
            Assert.Equal("second", Assert.Single(Dm.each(runtime.entries).Cast<datum_log_entry>()).message);
            // The ids follow creation order (next_id++, log_entry.dm:42).
            Assert.Equal(new object[] { 0f, 2f }, Dm.each(game.entries).Cast<datum_log_entry>().Select(e => e.id).ToArray());

            // The readable file: the header of init_category_file (:234), then each entry through WRITE_LOG (rust-g's
            // timestamp and to_readable_text(format = FALSE), log_entry.dm:125).
            string stamp = t.Stamp();
            Assert.Equal(
                "[2026-10-06 12:00:00.000] Starting up round ID 7.\n - -------------------------\n"
                + stamp + " GAME: first\n"
                + stamp + " GAME: third\n",
                t.Read(Dir + "/game.log"));

            // The json file: the category header (:225), then one json line per entry (log_entry.dm:83-94, :116).
            string[] json = t.Read(Dir + "/game.log.json").Split('\n');
            Assert.StartsWith("{\"ts\":\"1791288000.000000\",\"round-id\":7,\"secret\":0,\"cat-list\":[", json[0]);
            Assert.EndsWith(",\"game\"],\"cat\":\"game\"}", json[0]);
            Assert.Contains("\"game-say\"", json[0]);
            Assert.StartsWith("{\"ts\":\"2026-10-06 12:00:00.000\",\"cat\":\"game\",\"msg\":\"first\",\"data\":null,\"w-state\":", json[1]);
            Assert.EndsWith(",\"s-store\":null,\"id\":0,\"s-ver\":\"1.0.0\"}", json[1]);
            Assert.StartsWith("{\"ts\":\"2026-10-06 12:00:00.000\",\"cat\":\"game\",\"msg\":\"third\",\"data\":{\"who\":\"bob\"},\"w-state\":", json[2]);
            Assert.EndsWith(",\"s-store\":[],\"id\":2,\"s-ver\":\"1.0.0\"}", json[2]);
            Assert.Equal("", json[3]);

            // world._initialize_log_files() at the end of init_logging (:155) names the plain logs and starts reta.log.
            Assert.Equal(Dir + "/config_error.log", GLOB.config_error_log);
            Assert.Equal(Dir + "/perf.log", GLOB.perf_log);
            Assert.Equal(Dir + "/reta.log", GLOB.reta_log);
            Assert.Equal(stamp + " Starting up round ID 7.\n - -------------------------\n", t.Read(Dir + "/reta.log"));
        }
        Assert.Empty(OwnRuntimes(t));
    }

    [Fact]
    public void AnEntryAfterInitIsWrittenAtOnceAndSubCategoriesShareTheirMastersFiles()
    {
        var t = new LoggingTestWorld();
        using (t.World.Enter())
        {
            AllowWorldStateSkeleton();
            t.Setup(round_id: 1);
            logger.init_logging();
        }
        t.World.Step(4);
        using (t.World.Enter())
        {
            logger.Log(LOG_CATEGORY_GAME_SAY, "hello there");
            // game-say is a sub category of game: get_output_file goes to the master's files (log_category.dm:58-59).
            Assert.EndsWith(t.Stamp() + " GAME-SAY: hello there\n", t.Read(Dir + "/game.log"));
            Assert.Null(t.Read(Dir + "/game-say.log"));
            Assert.Contains("\"cat\":\"game-say\",\"msg\":\"hello there\"", t.Read(Dir + "/game.log.json"));

            // A secret category writes under secret/ (log_category.dm:60-61).
            logger.Log(LOG_CATEGORY_ADMIN_PRIVATE, "psst");
            Assert.EndsWith(t.Stamp() + " ADMINPRIVATE: psst\n", t.Read(Dir + "/secret/adminprivate.log"));

            // ENTRY_USE_DATA_W_READABLE (the qdel category) adds the pretty-printed data to the readable line.
            logger.Log(LOG_CATEGORY_QDEL, "gone", Dm.list(Dm.kv("count", 2)));
            // WRITE_LOG puts every further line of the text after " - " (rust-g log_write).
            string[] lines = ("QDEL: gone" + Dm.json_encode(Dm.list(Dm.kv("count", 2)), JSON_PRETTY_PRINT)).Split('\n');
            Assert.True(lines.Length > 1);
            Assert.EndsWith(t.Stamp() + " " + string.Join("\n - ", lines) + "\n", t.Read(Dir + "/qdel.log"));
        }
        Assert.Empty(OwnRuntimes(t));
    }

    [Fact]
    public void ConfigFlagsDisableCategoriesAndAMasterWithEnabledSubCategoriesStillExists()
    {
        var t = new LoggingTestWorld();
        using (t.World.Enter())
        {
            AllowWorldStateSkeleton();
            test_logging_config cfg = t.Setup();
            cfg.values[typeof(datum_config_entry_flag_log_game)] = FALSE;
            cfg.values[typeof(datum_config_entry_flag_log_say)] = FALSE;
            cfg.values[typeof(datum_config_entry_flag_log_uplink)] = FALSE;
            cfg.values[typeof(datum_config_entry_flag_log_as_human_readable)] = FALSE;
            logger.init_logging();

            DmList disabled = logger.disabled_categories;
            Assert.Equal(1f, Dm.num(disabled[LOG_CATEGORY_GAME]));
            Assert.Equal(1f, Dm.num(disabled[LOG_CATEGORY_GAME_SAY]));
            // log_uplink is the flag of the uplink master and of every uplink sub category: all disabled, so the master
            // has no sub category left and is not initialized (:139-140).
            Assert.Equal(1f, Dm.num(disabled[LOG_CATEGORY_UPLINK]));
            Assert.Equal(1f, Dm.num(disabled[LOG_CATEGORY_UPLINK_SPY]));
            Assert.Null(logger.log_categories[LOG_CATEGORY_UPLINK]);
            Assert.Null(logger.log_categories[LOG_CATEGORY_UPLINK_SPY]);
            // game's other sub categories are enabled, so game itself is initialized although it is disabled (:141-142);
            // its disabled sub category game-say is left out of its list (:133).
            Assert.NotNull(logger.log_categories[LOG_CATEGORY_GAME]);
            Assert.NotNull(logger.log_categories[LOG_CATEGORY_GAME_EMOTE]);
            Assert.Null(logger.log_categories[LOG_CATEGORY_GAME_SAY]);
            Assert.Null(disabled[LOG_CATEGORY_GAME_EMOTE]);

            // human readable logs are off: no readable header (:227-228), json only.
            Assert.Equal(0f, logger.human_readable_enabled);
            Assert.Null(t.Read(Dir + "/game.log"));
            Assert.NotNull(t.Read(Dir + "/game.log.json"));

            // A disabled category logs nothing (:301-302), an enabled sub category of it does.
            string before = t.Read(Dir + "/game.log.json");
            logger.Log(LOG_CATEGORY_GAME, "dropped");
            logger.Log(LOG_CATEGORY_GAME_SAY, "dropped too");
            Assert.Equal(before, t.Read(Dir + "/game.log.json"));
            logger.Log(LOG_CATEGORY_GAME_EMOTE, "kept");
            Assert.Contains("\"msg\":\"kept\"", t.Read(Dir + "/game.log.json"));
            // The fixture's config answers log_as_human_readable FALSE, so write_entry wrote no readable line either.
            Assert.Null(t.Read(Dir + "/game.log"));
        }
        Assert.Empty(OwnRuntimes(t));
    }

    [Fact]
    public void TheRealCategoryTreeHasItsMastersAndSubCategories()
    {
        var t = new LoggingTestWorld();
        using (t.World.Enter())
        {
            t.Setup();
            DmList tree = logger.assemble_log_category_tree() as DmList;
            Assert.Same(tree, logger.assemble_log_category_tree()); // the static cache (:165-167)
            Assert.Equal(new object[] { typeof(datum_log_category_admin_dsay), typeof(datum_log_category_admin_circuit) },
                Dm.each(tree[typeof(datum_log_category_admin)]).ToArray());
            Assert.Equal(new object[] { typeof(datum_log_category_admin_asay) }, Dm.each(tree[typeof(datum_log_category_admin_private)]).ToArray());
            // debug_runtime is a master on purpose (log_category_debug.dm:16).
            Assert.Equal(0f, ((DmList)tree[typeof(datum_log_category_debug_runtime)]).len);
            Assert.False(Dm.@in(typeof(datum_log_category_game_say), tree));
            Assert.True(Dm.@in(typeof(datum_log_category_game_say), tree[typeof(datum_log_category_game)]));
            Assert.True(Dm.@in(typeof(datum_log_category_game_compat), tree[typeof(datum_log_category_game)]));
            foreach (object master in Dm.each(tree))
                Assert.Null(Dm.initial((Type)master, (datum_log_category p) => p.master_category));
        }
        // The real categories pass every check: no stack traces.
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void TheCategoryTreeChecksSubCategoriesSecretsAndSchemaVersionsButNeverDuplicates()
    {
        var t = new LoggingTestWorld();
        using (t.World.Enter())
        {
            AllowWorldStateSkeleton();
            t.Setup();
            Type[] fixtures =
            {
                typeof(datum_log_category_test_logging_master), typeof(datum_log_category_test_logging_secret_sub),
                typeof(datum_log_category_test_logging_sub_of_sub), typeof(datum_log_category_test_logging_duplicate),
                typeof(datum_log_category_test_logging_no_schema), typeof(datum_log_category_test_logging_bad_schema),
            };
            TgState.Current.LogCategoryTypeFilter = fixtures.Contains;
            DmList tree = logger.assemble_log_category_tree() as DmList;

            // tg never adds to known_categories (:171-178), so the duplicate category name is not detected: the duplicate
            // is a master of its own. The type without a schema version is skipped (:181-183); a secret status that
            // differs is reported but kept (:197-199); the sub category of a sub category has an invalid master
            // (:193-195). (Fixture types are not in the generated type order: they sort by path, object-model.md 9.)
            Assert.Equal(new object[]
            {
                typeof(datum_log_category_test_logging_bad_schema),
                typeof(datum_log_category_test_logging_duplicate),
                typeof(datum_log_category_test_logging_master),
            }, Dm.each(tree).ToArray());
            Assert.Equal(new object[] { typeof(datum_log_category_test_logging_secret_sub) }, Dm.each(tree[typeof(datum_log_category_test_logging_master)]).ToArray());
            Assert.Equal(new[]
            {
                "log category type '/datum/log_category/test_logging_no_schema' does not have a valid schema version, skipping",
                "log category /datum/log_category/test_logging_secret_sub has a secret status that differs from its master category /datum/log_category/test_logging_master",
                "log category /datum/log_category/test_logging_secret_sub is an invalid master category as it's a sub category",
            }, t.Runtimes.Select(Message).ToArray());
            t.Runtimes.Clear();

            // init_logging: of two categories with one name, the one initialized later (master, after duplicate in the
            // tree's order) replaces the other in log_categories (:256); a schema version that is no semver is reported
            // and reset (:258-260).
            logger.init_logging();
            var named = (datum_log_category)logger.log_categories["test-master"];
            Assert.IsType<datum_log_category_test_logging_master>(named);
            var bad = (datum_log_category)logger.log_categories["test-bad-schema"];
            Assert.Equal(LOG_CATEGORY_SCHEMA_VERSION_NOT_SET, bad.schema_version);
            var sub = (datum_log_category)logger.log_categories["test-secret-sub"];
            Assert.IsType<datum_log_category_test_logging_master>(sub.master_category); // the master's instance (:247)
            Assert.Equal(new[] { "log category test-bad-schema has an invalid schema version 'one'" }, OwnRuntimes(t).Select(Message).ToArray());
        }
    }

    // stack_trace's message is tg's _stack_trace text; the tests compare its first line.
    private static string Message(Exception e) => e.Message.Split('\n')[0];

    [Fact]
    public void LogRepairsBadArgumentsAndCrashesForAnUnknownCategory()
    {
        var t = new LoggingTestWorld();
        using (t.World.Enter())
        {
            AllowWorldStateSkeleton();
            t.Setup();
            logger.Log(LOG_CATEGORY_GAME, 12, "not a list");
            logger.Log(null, "no category");
            object[] first = Dm.each(logger.waiting_log_calls[1]).ToArray();
            Assert.Equal(LOG_CATEGORY_GAME, first[0]);
            Assert.Equal("12", first[1]); // message = "[message]" (:286)
            Assert.Equal("not a list", ((DmList)first[2])["data"]); // data = list("data" = data) (:294)
            Assert.Equal(LOG_CATEGORY_INTERNAL_CATEGORY_NOT_FOUND, Dm.each(logger.waiting_log_calls[2]).First());
            Assert.Equal(new[]
            {
                "Logging with a non-text message",
                "Logging with data this is not a list, it will be converted to a list with a single key 'data'",
                "Logging with a null or empty category",
            }, t.Runtimes.Select(Message).ToArray());

            logger.init_logging();
            t.Runtimes.Clear();
            // An unknown category: the message goes to the internal category, then Log CRASHes (:305-307).
            var crash = Assert.Throws<DmCrashException>(() => logger.Log("no-such-category", "lost"));
            Assert.Equal("Attempted to log to a category that doesn't exist! no-such-category", crash.Message);
            Assert.Equal(307, crash.DmLine);
            Assert.Contains("\"cat\":\"internal-category-not-found\",\"msg\":\"lost\"", t.Read(Dir + "/internal-error.log.json"));

            // init_logging twice CRASHes (:114-115).
            var twice = Assert.Throws<DmCrashException>(() => logger.init_logging());
            Assert.Equal("Attempted to call init_logging twice!", twice.Message);
        }
    }

    [Fact]
    public void RecursiveJsonifyKeepsNullsRecursesIntoListsAndDropsEmptyLists()
    {
        var t = new LoggingTestWorld();
        using (t.World.Enter())
        {
            t.Setup();
            DmList semvers = Dm.list();
            DmList result = logger.recursive_jsonify(Dm.list(
                Dm.kv("a", null),
                Dm.kv("b", Dm.list(Dm.kv("c", 1))),
                Dm.kv("d", Dm.list()),
                Dm.kv("e", "text")), semvers) as DmList;
            Assert.Equal("{\"a\":null,\"b\":{\"c\":1},\"e\":\"text\"}", Dm.json_encode(result));
            Assert.Equal(new[] { "recursive_jsonify got an empty list after serialization" }, t.Runtimes.Select(Message).ToArray());
            Assert.Null(logger.recursive_jsonify(null, semvers));
        }
    }

    [Fact]
    public void AMissingLogFileIsWrittenAgainThroughTheErrorRecovery()
    {
        var t = new LoggingTestWorld();
        using (t.World.Enter())
        {
            AllowWorldStateSkeleton();
            t.Setup();
            logger.init_logging();
            Dm.fdel(Dir + "/game.log.json");
            Dm.fdel(Dir + "/game.log");

            // CHECK_AND_TRY_FILE_ERROR_RECOVERY (log_entry.dm:98-111): log the recovery to the internal error category,
            // write the category's file again, then call the proc again with its arguments.
            log_game("after the loss");
            // The recovery's own log line resets the shared in_error_recovery static (log_entry.dm:111), and
            // init_category_file writes both game files, so only the readable file needed a recovery.
            Assert.EndsWith(" INTERNAL-ERROR: attempting to perform file error recovery: " + Dir + "/game.log\n", t.Read(Dir + "/internal-error.log"));
            Assert.DoesNotContain("game.log.json", t.Read(Dir + "/internal-error.log"));
            Assert.Equal("[2026-10-06 12:00:00.000] Starting up round ID .\n - -------------------------\n" + t.Stamp() + " GAME: after the loss\n", t.Read(Dir + "/game.log"));
            Assert.Contains("\"msg\":\"after the loss\"", t.Read(Dir + "/game.log.json"));
            Assert.Empty(OwnRuntimes(t));
        }
    }

    [Fact]
    public void TheSameLoggingTwiceGivesTheSameStateHash()
    {
        ulong Run()
        {
            var t = new LoggingTestWorld(seed: 11);
            using (t.World.Enter())
            {
                AllowWorldStateSkeleton();
                t.Setup(round_id: 3);
                log_world("one");
                logger.init_logging();
                log_game("two", Dm.list(Dm.kv("x", 1)));
            }
            t.World.Step(2);
            using (t.World.Enter())
                log_signal("three");
            return t.World.ComputeStateHash();
        }
        Assert.Equal(Run(), Run());
    }
}
