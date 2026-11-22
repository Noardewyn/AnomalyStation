using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Bots;
using Anomaly.Tools.Bots;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M2 F1 through the real server: a joining player is named and dressed by the host's join stand-in (tg's random name, the
/// assistant outfit's random-colour jumpsuit), and the replicated appearance carries the worn jumpsuit's GAGS overlay, so
/// another session sees the jumpsuit and the name. The server finds tg's strings/ files in the tgstation clone
/// (TgStrings.Locate); without the clone the test is skipped.
/// </summary>
public class CrewOutfitTests
{
    private static readonly bool HaveClone = TgStrings.Locate(AppContext.BaseDirectory, Directory.GetCurrentDirectory()) != null;

    // Every icon text in an appearance and its overlays and underlays (nested), as the client's compositor walks them.
    internal static List<(string Icon, string State, float Layer)> Icons(ReplicaWorld replica, TableRef reference, int depth = 0)
    {
        var found = new List<(string, string, float)>();
        if (depth > 8 || !replica.TryGetAppearance(reference, out AppearanceEntry entry))
            return found;
        found.Add((replica.ResolveResource(entry.Icon) ?? replica.ResolveString(entry.Icon), replica.ResolveString(entry.IconState), entry.Layer));
        foreach (TableRef overlay in entry.Overlays)
            found.AddRange(Icons(replica, overlay, depth + 1));
        foreach (TableRef underlay in entry.Underlays)
            found.AddRange(Icons(replica, underlay, depth + 1));
        return found;
    }

    // A crew member's real name and the worn icon and state its jumpsuit is drawn with (update_worn_undersuit: the uniform's
    // icon_state, from its worn_icon; human_update_icons.dm:85, :611).
    private static (string Name, string Icon, string State) Worn(mob_living_carbon_human crew)
    {
        Assert.True(crew.w_uniform is obj_item_clothing_under_color, $"{crew.real_name} wears {crew.w_uniform?.type}");
        object icon = crew.w_uniform.worn_icon;
        return ((string)crew.real_name, icon is DmResource r ? r.Path : (string)icon, crew.w_uniform.icon_state);
    }

    [Fact]
    public void AnotherSessionSeesAJoinedPlayersJumpsuitAndName()
    {
        if (!HaveClone)
            return; // the tgstation clone holds tg's name lists (set ANOMALY_TGSTATION or put it at ../tgstation)

        using var rig = new BotServerRig(strict: true);
        Bot alice = rig.AddBot("Alice", BotBehaviour.Idle);
        Bot bob = rig.AddBot("Bob", BotBehaviour.Idle);
        rig.UntilJoined(alice);
        rig.UntilJoined(bob);
        rig.Tick(10);

        var aliceMob = (mob_living_carbon_human)rig.MobOf("Alice");
        var bobMob = (mob_living_carbon_human)rig.MobOf("Bob");
        (string Name, string Icon, string State) alice_, bob_;
        using (rig.World.Enter())
        {
            alice_ = Worn(aliceMob);
            bob_ = Worn(bobMob);
        }
        Assert.NotEqual("Unknown", alice_.Name);
        Assert.NotEqual("Unknown", bob_.Name);

        // Bob's session sees Alice: her name, and her jumpsuit's worn icon (a GAGS text for a colour jumpsuit, the .dmi of
        // the rainbow one) among her overlays
        ReplicaEntity aliceSeen = bob.Replica.Entities.Single(e => e.Id == aliceMob.dm_id);
        Assert.Equal(alice_.Name, aliceSeen.State.Name);
        List<(string Icon, string State, float Layer)> aliceIcons = Icons(bob.Replica, aliceSeen.State.Appearance);
        Assert.True(aliceIcons.Any(i => i.Icon == alice_.Icon && i.State == alice_.State && i.Layer == -Defines.UNIFORM_LAYER),
            $"Alice wears {alice_.Icon}:{alice_.State}; Bob sees " + string.Join(" ; ", aliceIcons.Select(i => $"{i.Icon}:{i.State}@{i.Layer}")));

        // and Alice's sees Bob
        ReplicaEntity bobSeen = alice.Replica.Entities.Single(e => e.Id == bobMob.dm_id);
        Assert.Equal(bob_.Name, bobSeen.State.Name);
        List<(string Icon, string State, float Layer)> bobIcons = Icons(alice.Replica, bobSeen.State.Appearance);
        Assert.True(bobIcons.Any(i => i.Icon == bob_.Icon && i.State == bob_.State && i.Layer == -Defines.UNIFORM_LAYER),
            $"Bob wears {bob_.Icon}:{bob_.State}; Alice sees " + string.Join(" ; ", bobIcons.Select(i => $"{i.Icon}:{i.State}@{i.Layer}")));

        Assert.Equal(0, rig.World.RuntimeErrorCount);
    }
}
