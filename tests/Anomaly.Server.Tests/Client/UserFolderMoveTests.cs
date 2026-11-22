using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Server.Admin;

namespace Anomaly.Server.Tests.Client;

/// <summary>D347: the client's and the server's files named after the old name move to the new names once.</summary>
public class UserFolderMoveTests
{
    [Fact]
    public void TheOldUserFolderMovesOnceAndKeepsWhatTheNewOneHas()
    {
        string root = Path.Combine(Path.GetTempPath(), "anomaly-move-" + Path.GetRandomFileName());
        string old = UserFolderMove.OldFolder(root, linux: false), fresh = Path.Combine(root, "AnomalyStation");
        try
        {
            Directory.CreateDirectory(Path.Combine(old, "logs"));
            Directory.CreateDirectory(Path.Combine(old, "speech", "model"));
            File.WriteAllText(Path.Combine(old, "ss3d-options.cfg"), "fov=90");
            File.WriteAllText(Path.Combine(old, "reconnect-tokens.txt"), "old");
            File.WriteAllText(Path.Combine(old, "speech", "model", "a.bin"), "x");
            Directory.CreateDirectory(fresh);
            File.WriteAllText(Path.Combine(fresh, "reconnect-tokens.txt"), "new");

            Assert.Equal(2, UserFolderMove.Run(old, fresh)); // the options (renamed) and the speech folder
            Assert.Equal("fov=90", File.ReadAllText(Path.Combine(fresh, "options.cfg")));
            Assert.True(File.Exists(Path.Combine(fresh, "speech", "model", "a.bin")));
            Assert.Equal("new", File.ReadAllText(Path.Combine(fresh, "reconnect-tokens.txt"))); // the new folder's own file stays
            Assert.False(Directory.Exists(Path.Combine(fresh, "logs")));
            Assert.Equal(0, UserFolderMove.Run(old, fresh));
            Assert.Equal(0, UserFolderMove.Run(Path.Combine(root, "none"), fresh));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TheOldDatabaseFileTakesTheNewName()
    {
        string folder = Path.Combine(Path.GetTempPath(), "anomaly-db-" + Path.GetRandomFileName());
        try
        {
            using (var db = new ServerDatabase(Path.Combine(folder, "ss3d.sqlite")))
                Assert.True(db.SchemaVersion > 0);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            using (var db = new ServerDatabase(Path.Combine(folder, "anomaly.sqlite")))
                Assert.True(db.SchemaVersion > 0);
            Assert.False(File.Exists(Path.Combine(folder, "ss3d.sqlite")));
            Assert.True(File.Exists(Path.Combine(folder, "anomaly.sqlite")));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(folder, recursive: true);
        }
    }
}
