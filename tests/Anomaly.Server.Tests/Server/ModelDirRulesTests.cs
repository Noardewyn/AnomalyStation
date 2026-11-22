using System.IO;

namespace Anomaly.Server.Tests.Server;

/// <summary>The server's share of the 3D model catalog (DV56): the rules it reads from the repository's catalog.</summary>
public sealed class ModelDirRulesTests
{
    private static ModelDirRules Catalog()
    {
        string path = ModelDirRules.Locate(null);
        Assert.NotNull(path);
        return ModelDirRules.Parse(File.ReadAllText(path));
    }

    [Fact]
    public void TheServerReadsTheClosetsAndCratesRulesFromTheCatalog()
    {
        ModelDirRules rules = Catalog();
        ModelDirRules.Rule[] storage = rules.Rules.Where(r => r.Icons.Contains("icons/obj/storage/closet.dmi") || r.Icons.Contains("icons/obj/storage/crates.dmi")).ToArray();
        Assert.Equal(12, rules.Rules.Count(r => r.Icons.Contains("icons/obj/storage/closet.dmi")));
        Assert.Equal(16, rules.Rules.Count(r => r.Icons.Contains("icons/obj/storage/crates.dmi")));
        Assert.All(storage, r => Assert.True(r.AwayFromWall && r.KeepOnPush && r.Rotate));
        Assert.Contains(rules.Rules, r => r.Draws("icons/obj/storage/closet.dmi", "emergency"));
        Assert.Contains(rules.Rules, r => r.Draws("icons/obj/storage/closet.dmi", "cabinet"));
        Assert.Contains(rules.Rules, r => r.Draws("icons/obj/storage/closet.dmi", "mining")); // the miner's locker has its model (2026-10-26)
        Assert.Contains(rules.Rules, r => r.Draws("icons/obj/storage/crates.dmi", "securecrateopen"));
    }

    [Fact]
    public void EveryModelledThingKeepsItsFacingWhenPushedAndAFreeStandingOneTurnsByHand()
    {
        // The owner (2026-10-20): a pushed or pulled thing keeps its facing; anything free-standing can be turned from the radial (tg's simple_rotation).
        ModelDirRules rules = Catalog();
        Assert.All(rules.Rules, r => Assert.True(r.KeepOnPush, r.Id));
        ModelDirRules.Rule canister = rules.Rules.First(r => r.Draws("gags:/datum/greyscale_config/canister|#c6c0b5", ""));
        Assert.True(canister.Rotate && canister.AwayFromWall);
        ModelDirRules.Rule chair = rules.Rules.First(r => r.Draws("icons/obj/chairs.dmi", "chair"));
        Assert.True(chair.Rotate); // tg's own element is already there: ApplyTo adds none (HasOwnAltClick)
        Assert.False(chair.AwayFromWall);
        Assert.False(rules.Rules.First(r => r.Draws("icons/obj/smooth_structures/table.dmi", "table-38")).Rotate);
        Assert.False(rules.Rules.First(r => r.Draws("icons/obj/doors/airlocks/station/maintenance.dmi", "closed")).Rotate);
    }

    [Fact]
    public void AnEntryMayOptOut()
    {
        ModelDirRules plain = ModelDirRules.Parse("""{ "models": [ { "id": "x", "icons": [ "a.dmi" ], "rule": "single", "texture": "model", "parts": { "model": "x.glb" } } ] }""");
        Assert.True(plain.Rules.Single() is { KeepOnPush: true, Rotate: true, AwayFromWall: false });
        ModelDirRules wall = ModelDirRules.Parse("""{ "models": [ { "id": "w", "icons": [ "a.dmi" ], "rule": "wall", "texture": "model", "parts": { "model": "x.glb" } } ] }""");
        Assert.False(wall.Rules.Single().Rotate);
        ModelDirRules opted = ModelDirRules.Parse("""{ "models": [ { "id": "y", "icons": [ "a.dmi" ], "rule": "single", "rotate": false, "dir": { "keepOnPush": false }, "texture": "model", "parts": { "model": "x.glb" } } ] }""");
        Assert.True(opted.Rules.Single() is { KeepOnPush: false, Rotate: false });
    }
}
