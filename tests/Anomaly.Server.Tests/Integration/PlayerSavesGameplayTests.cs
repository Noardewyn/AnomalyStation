using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// tg's character savefile through the real server: a player's character and preferences are written to the server's player saves
/// folder (tg's data/player_saves/C/CKEY/preferences.json) and loaded again by the next server on that folder.
/// </summary>
public sealed class PlayerSavesGameplayTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "anomaly-saves-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private ServerConfig Config() => new()
    {
        Seed = 7, RoomWidth = 24, RoomHeight = 24, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f,
        DevCommands = false, PlayerSavesFolder = _folder,
    };

    private static T In<T>(RealHeadlessWorld w, Func<T> read)
    {
        using (w.World.Enter())
            return read();
    }

    private static datum_preferences PrefsOf(string key)
    {
        foreach (client c in Dm.each<client>(GlobalProcs.GLOB.clients))
        {
            if (c.key as string == key)
                return c.prefs;
        }
        return null;
    }

    [Fact]
    public void A_players_character_is_saved_to_the_player_saves_folder_and_loaded_by_the_next_server()
    {
        using (var first = new RealHeadlessWorld(config: Config()))
        {
            first.Join("Alice");
            In(first, () =>
            {
                datum_preferences prefs = PrefsOf("Alice");
                Assert.True(Dm.truthy(prefs.update_preference(GlobalProcs.GLOB.preference_entries[typeof(datum_preference_numeric_age)] as datum_preference, 57)));
                // tg's ui_close: the character into the savefile's tree, then the savefile to the disk
                prefs.save_character();
                prefs.save_preferences();
                return 0;
            });
        }
        Assert.True(File.Exists(Path.Combine(_folder, "a", "alice", "preferences.json")));

        using var second = new RealHeadlessWorld(config: Config());
        second.Join("Alice");
        Assert.Equal(57f, In(second, () => Dm.num(PrefsOf("Alice").read_preference(typeof(datum_preference_numeric_age)))));
    }
}
