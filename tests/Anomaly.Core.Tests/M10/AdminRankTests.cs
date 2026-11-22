using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M10;

/// <summary>
/// M10 slice I (D275), written from tg's admin_ranks.dm and holder2.dm: world/ConfigLoaded's load_admins reads tg's admin_ranks.txt
/// (Include, Exclude and the @ of the previous rank) and admins.txt; an admin's rights are its ranks' flags, check_rights_for asks a
/// client's holder, and a rank without AUTOADMIN starts deadmined.
/// </summary>
public class AdminRankTests
{
    private const string Ranks = """
        #Admin Rank format is as follows:
        Name = Moderator
        Include = ADMIN
        Exclude =
        Edit =

        Name = Trial Admin
        Include = @ SPAWN VAREDIT BAN
        Exclude =
        Edit =

        Name = Admin Observer
        Include =
        Exclude = AUTOADMIN
        Edit =

        Name = Host
        Include = EVERYTHING
        Exclude =
        Edit = EVERYTHING
        """;

    private const string Admins = """
        #List users here to be made admins with the format: ckey = rank name.
        Big Boss = Host
        Trial_Guy = Trial Admin
        Watcher = Admin Observer
        Mixed = Moderator+Admin Observer
        Lost = No Such Rank
        """;

    private static World Boot() => TgTestWorld.Start(configure: w =>
    {
        DmWorldState.Of(w).Files.WriteText("config/admin_ranks.txt", Ranks);
        DmWorldState.Of(w).Files.WriteText("config/admins.txt", Admins);
    });

    private static int Flags(object ckey) => (int)Dm.num(((datum_admins)(GLOB.admin_datums[ckey] ?? GLOB.deadmins[ckey])).rank_flags());

    [Fact]
    public void LoadAdminsReadsTgsRankKeywordsAndTheAdminsFile()
    {
        World w = Boot();
        using (w.Enter())
        {
            Assert.Equal(4, GLOB.admin_ranks.len);
            Assert.Equal(R_EVERYTHING, Flags("bigboss"));
            // @ takes the previous rank's includes: Moderator's ADMIN, with SPAWN VAREDIT BAN, and R_DEFAULT's AUTOADMIN
            Assert.Equal(R_AUTOADMIN | R_ADMIN | R_SPAWN | R_VAREDIT | R_BAN, Flags("trialguy"));
            Assert.Equal(NONE, Flags("watcher"));
            Assert.Equal(R_ADMIN, Flags("mixed") & ~R_AUTOADMIN);
            Assert.Equal("Moderator+Admin Observer", Dm.str(((datum_admins)GLOB.admin_datums["mixed"]).rank_names()));
        }
    }

    [Fact]
    public void ARankWithoutAutoadminStartsDeadminedAndAnUnknownRankGivesNoRights()
    {
        World w = Boot();
        using (w.Enter())
        {
            Assert.Null(GLOB.admin_datums["watcher"]);
            Assert.NotNull(GLOB.deadmins["watcher"]);
            Assert.NotNull(GLOB.protected_admins["bigboss"]);
            var lost = (datum_admins)GLOB.deadmins["lost"];
            Assert.Equal(0, lost.ranks.len);
        }
    }

    [Fact]
    public void CheckRightsForAsksTheClientsHolderForOneOfTheRights()
    {
        World w = Boot();
        using (w.Enter())
        {
            var trial = (datum_admins)GLOB.admin_datums["trialguy"];
            var c = new client { ckey = "trialguy" };
            trial.associate(c);
            Assert.Same(trial, c.holder);
            Assert.Contains(c, Dm.each_cast<client>(GLOB.admins));
            Assert.True(Dm.truthy(check_rights_for(c, R_BAN)));
            Assert.True(Dm.truthy(check_rights_for(c, R_SERVER | R_SPAWN)));
            Assert.False(Dm.truthy(check_rights_for(c, R_SERVER)));
            Assert.False(Dm.truthy(check_rights_for(new client { ckey = "nobody" }, 0)));
            Assert.Equal(" +SERVER +DEBUG", Dm.str(rights2text(R_SERVER | R_DEBUG, " ")));
            trial.disassociate();
            Assert.Null(c.holder);
        }
    }
}

/// <summary>The files a real server ships (config/admin_ranks.txt, config/admins.txt) with a listed admin, as cloud-init appends it.</summary>
public class ShippedAdminFilesTests
{
    [Fact]
    public void AListedNameInTheShippedFilesIsAHost()
    {
        string root = Anomaly.Core.Tests.Tg.M3Maps.P2.MapFixtures.RepoRoot();
        string ranks = File.ReadAllText(Path.Combine(root, "config", "admin_ranks.txt")).Replace("\r\n", "\n");
        string admins = File.ReadAllText(Path.Combine(root, "config", "admins.txt")).Replace("\r\n", "\n") + "\nSome Admin = Host\n";
        World w = TgTestWorld.Start(configure: x =>
        {
            DmWorldState.Of(x).Files.WriteText("config/admin_ranks.txt", ranks);
            DmWorldState.Of(x).Files.WriteText("config/admins.txt", admins);
        });
        using (w.Enter())
        {
            Assert.NotNull(GLOB.admin_datums["someadmin"]);
            Assert.Equal(R_EVERYTHING, (int)Dm.num(((datum_admins)GLOB.admin_datums["someadmin"]).rank_flags()));
        }
    }
}
